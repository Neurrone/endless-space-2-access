# walks/lib.sh -- shared helpers for the regression walk. Sourced, never run.
#
#   . "$(dirname "$0")/../lib.sh" "$@"
#
# $1 of a scenario script is the output directory. The folder the script lives in is its
# FAMILY (one folder per major screen) and the script's own name is its SCENARIO; every
# artifact is named <family>/<scenario>-<name>. Everything here talks to the dev server
# over curl and nothing else -- no Python, no jq.
#
# Naming: a graph dump is a `.txt` artifact under <out>/<family>/; a focused-tooltip probe
# is a `.json` under <out>/tooltips/. Both are diffed. `ghosts.txt`, `routelog.txt`,
# `index.txt` and `skipped.txt` are diagnostics and are NOT diffed (see diffwalks.sh).
#
# WAITING. Every wait is a PREDICATE the dev server evaluates each frame (`POST /wait`),
# so a step returns the frame its condition comes true. The one fixed interval is the 0.4 s
# of speech silence the /input and /type routes wait for before answering, which the dev
# loop measured as the necessary key spacing: a faster loop does not fail loudly, it
# reports a plausible wrong route.
#
# STATE. A scenario never assumes a clean screen and never cleans up after itself: it calls
# an `ensure_*` helper, which proceeds at once when the screen it wants is already up and
# opens it otherwise, so a folder of scenarios pays for opening its screen once. The runner
# (`run.sh`) drains once at the very end of a run so the fixture is left as found.

# A scenario lives one folder down from this file; a root-level tool (by-key.sh) beside it.
HERE="$(cd "$(dirname "$0")" && pwd)"
if [ -f "$HERE/lib.sh" ]; then WALKS_DIR="$HERE"; else WALKS_DIR="$(cd "$HERE/.." && pwd)"; fi
CS="$WALKS_DIR/cs"
. "$WALKS_DIR/fixture.env"
HOST="$WALK_HOST"

# A root-level tool sets FAMILY before sourcing; a scenario takes its folder's name.
FAMILY="${FAMILY:-$(basename "$HERE")}"
SCENARIO="$(basename "$0" .sh)"
OUTROOT="${1:-}"
[ -n "$OUTROOT" ] || { echo "usage: $0 <output-dir>" >&2; exit 2; }
mkdir -p "$OUTROOT" || exit 2
OUTROOT="$(cd "$OUTROOT" && pwd)"
OUT="$OUTROOT/$FAMILY"
TIP="$OUTROOT/tooltips"
TMP="$OUTROOT/.tmp"
SKIPS="$OUTROOT/skipped.txt"
mkdir -p "$OUT" "$TIP" "$TMP"
touch "$OUT/ghosts.txt" "$OUT/routelog.txt"

# ---------------------------------------------------------------- primitives

# waitfor <c# bool expression> [timeout-ms] -- block until the predicate is true, evaluated
# every frame by the dev server; 0 when it came true, 1 on timeout. The server caps one wait
# at ~60 s. The body must be a plain expression (no lambda).
waitfor() {
  printf '%s' "$1" > "$TMP/wait.cs"
  curl -s -X POST --data-binary "@$TMP/wait.cs" "$HOST/wait?timeout=${2:-15000}" > "$TMP/wait.out" 2>&1
  grep -q '"satisfied":true' "$TMP/wait.out"
}

# frame -- let one frame pass (a graph-state change is rendered on the next frame).
frame() { waitfor 'true' 2000; }

# pause [ms] -- a fixed wait, scaled by WALK_PACE. Kept for the rare step with nothing to
# wait on; a step that waits for the game to do something waits on a predicate instead.
pause() { curl -s -X POST --data-raw 'false' "$HOST/wait?timeout=$(( ${1:-400} * WALK_PACE / 100 ))" >/dev/null 2>&1; }

# ev <file.cs> -- run an eval body, echo the reply (goes to the scenario's run log)
ev()  { curl -s -X POST --data-binary "@$1" "$HOST/eval?speech=0"; echo; }

# evq <file.cs> -- run an eval body, speak up only on failure
evq() { curl -s -X POST --data-binary "@$1" "$HOST/eval?speech=0" > "$TMP/ev.out" 2>&1
        grep -q '"ok":true' "$TMP/ev.out" || { echo "EVAL FAIL($1):"; cat "$TMP/ev.out"; echo; }; }

# evs <c# expression> -- run an inline eval body, speak up only on failure
evs() { printf '%s' "$1" > "$TMP/inline.cs"; curl -s -X POST --data-binary "@$TMP/inline.cs" "$HOST/eval?speech=0" > "$TMP/ev.out" 2>&1
        grep -q '"ok":true' "$TMP/ev.out" || { echo "EVAL FAIL(inline):"; cat "$TMP/ev.out"; echo; }; }

# fact <c# string expression> -- echo just the `result` of an eval. For runtime discovery
# of things a dump cannot show (how many systems the empire owns, what the fleets are called).
fact() { printf '%s' "$1" > "$TMP/fact.cs"
         curl -s -X POST --data-binary "@$TMP/fact.cs" "$HOST/eval?speech=0" \
         | sed 's/.*"result":"//; s/","error.*//; s/.*"result":null.*//'; }

# unjson <file> -- an /eval reply's `result` as the text it is. A failed eval does not match
# the prefix and is left whole, so the failure lands in the artifact.
unjson() { sed 's/^{"ok":true,"result":"//; s/","error":.*$//' "$1" | sed 's/\\n/\n/g; s/\\"/"/g'; }

# inp <action> -- inject one mod action at the production dispatch point. The route answers
# only after the injection has run AND speech has been quiet for 400 ms
# (ModRoutes.SettleMilliseconds), which IS the key spacing the dev loop measured as
# necessary -- so nothing is added here. Measured 2026-09-15: a second 400 ms sleep on top
# doubled the cost of every key in every scenario.
inp()  { curl -s -X POST --data-raw "$1" "$HOST/input" > "$TMP/in.out" 2>&1; }
rep()  { n=$1; shift; i=0; while [ "$i" -lt "$n" ]; do inp "$1"; i=$((i+1)); done; }

# ---------------------------------------------------------------- game-state predicates

# The C# fragments the waits are built from. `cur` is the mod's focused screen.
P_CUR='ES2Access.ModEntry.Screens.Current'
P_NOMODAL='((GuiManager)Gui.GuiService).ModalOnTop == null'

# onscreen <screen key> [timeout-ms] -- until the mod reports that screen focused.
onscreen() { waitfor "$P_CUR != null && $P_CUR.Key == \"$1\"" "${2:-15000}"; }

# isonscreen <screen key> -- is it focused right now, with nothing modal over it (no waiting)?
# This is the arrival test for a PAGE. A modal's own window is its own ModalOnTop, so a modal
# screen never answers yes here: ask `isfocused` for one of those.
isonscreen() { waitfor "$P_CUR != null && $P_CUR.Key == \"$1\" && $P_NOMODAL" 50; }

# isfocused <screen key> -- is it the screen the mod is reporting right now (no waiting)? A
# screen buried under a modal is not the one reported, so this needs no modal clause of its own.
isfocused() { waitfor "$P_CUR != null && $P_CUR.Key == \"$1\"" 50; }

# shown <WindowName> / hidden <WindowName> -- until the game's window is (not) shown.
shown()  { waitfor "Gui.GuiService.GetWindow(\"$1\") != null && Gui.GuiService.GetWindow(\"$1\").Shown" "${2:-15000}"; }
hidden() { waitfor "Gui.GuiService.GetWindow(\"$1\") == null || !Gui.GuiService.GetWindow(\"$1\").Shown" "${2:-15000}"; }

# ---------------------------------------------------------------- opening and closing

# tut -- minimise the tutorial popup; expanded, it eats every injection as `unconsumed`.
tut() { evq "$CS/tut.cs"; }

# focused <screen key> -- until the mod reports that screen focused, minimising the tutorial
# popup on the way: opening some screens raises the game's tutorial for them, the mod
# focuses the popup, and the screen behind it is not the one reported until it is minimised
# (measured 2026-09-15: five scenarios each lost a 15 s wait to it). A short wait first, the
# popup minimised, then the full wait.
focused() {
  tut
  onscreen "$1" 3000 && return 0
  tut
  onscreen "$1" "${2:-12000}"
}

# drain -- close every modal and screen the walk knows, put the camera at galaxy overview.
drain() { evq "$CS/drain.cs"; waitfor "$P_NOMODAL" 10000; tut; }

# openwin <WindowName> [screen key] -- show the game's window and wait until it is drawn and,
# where a key is given, until the mod has focused its screen for it.
openwin() {
  printf '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow("%s"); if (w==null) return "NO WINDOW %s"; Gui.GuiService.ShowWindow(w); return "show %s shown="+w.Shown; }))()' "$1" "$1" "$1" > "$TMP/open.cs"
  curl -s -X POST --data-binary "@$TMP/open.cs" "$HOST/eval?speech=0" | grep -oE '"result":"[^"]*"|"error":"[^"]*"'
  shown "$1" || echo "   NOTE: $1 did not show"
  if [ -n "${2:-}" ]; then focused "$2" || echo "   NOTE: $2 was not focused after showing $1"; else tut; fi
}
hidewin() {
  printf '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow("%s"); if (w==null) return "NO WINDOW"; Gui.GuiService.HideWindow(w); return "hide %s shown="+w.Shown; }))()' "$1" "$1" > "$TMP/close.cs"
  curl -s -X POST --data-binary "@$TMP/close.cs" "$HOST/eval?speech=0" >/dev/null; hidden "$1" 10000 || echo "   NOTE: $1 did not hide"
}
# exitwin <WindowName> -- close the window the way Escape does, through the game's own Exit
# handler. `GetWindow` answers a GuiWindow and the Exit handler lives on the modal's
# IInputHandler face, so the cast is what makes this the game's path rather than a call that
# does not compile; the reply is read, never discarded, because a body that fails to compile
# would otherwise look exactly like a window that refuses to close. Where the game still
# leaves it up, hide it, so the next scenario does not start under a modal.
exitwin() {
  printf '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow("%s"); if (w==null) return "NO WINDOW"; var h = w as Amplitude.Unity.Input.IInputHandler; if (h==null) return "NOT AN INPUT HANDLER"; h.HandleInput(InputAction.Exit); return "exit %s shown="+w.Shown; }))()' "$1" "$1" > "$TMP/close.cs"
  curl -s -X POST --data-binary "@$TMP/close.cs" "$HOST/eval?speech=0" > "$TMP/ev.out" 2>&1
  grep -q '"ok":true' "$TMP/ev.out" || { echo "EVAL FAIL(exitwin $1):"; cat "$TMP/ev.out"; echo; }
  hidden "$1" 5000 && return 0
  echo "   NOTE: $1 did not close on Exit - hidden instead"
  hidewin "$1"
}

# ---------------------------------------------------------------- ensure_* (idempotent arrival)

# ensure_screen <screen key> <WindowName> -- a page opened by showing its window. Proceeds at
# once when it is already the focused screen with nothing modal over it.
ensure_screen() {
  isonscreen "$1" && return 0
  drain
  openwin "$2" "$1"
}

# ensure_modal <screen key> <WindowName> [c# opener] -- a modal window. Most bind themselves
# on show; one that needs data is given the opener expression that binds and shows it.
ensure_modal() {
  # A modal's own window is the ModalOnTop, so the no-modal test can never say yes for one.
  isfocused "$1" && return 0
  waitfor "$P_NOMODAL" 50 || drain
  if [ -n "${3:-}" ]; then evs "$3"; shown "$2" || echo "   NOTE: $2 did not show"; focused "$1" || echo "   NOTE: $1 not focused"
  else openwin "$2" "$1"; fi
}

# ensure_galaxy -- the galaxy HUD at overview zoom with no transition in flight.
ensure_galaxy() {
  if isonscreen screen.galaxy && waitfor '!ES2Access.UI.GalaxyViewLevels.ChangingLevel' 50; then return 0; fi
  drain
  onscreen screen.galaxy 20000 || echo "   NOTE: the galaxy HUD did not come back"
  waitfor '!ES2Access.UI.GalaxyViewLevels.ChangingLevel' 20000
}

# The system-management page is WHOLE when the game draws its cards AND its side panels for
# the system asked for (the page declares nothing until then -- docs/planets.md).
P_SYSWHOLE='Gui.GuiService.GetWindow<StarSystemScreen>(false) != null && Gui.GuiService.GetWindow<StarSystemScreen>(false).StarSystemNode != null && Gui.GuiService.GetWindow<StarSystemScreen>(false).StarSystemNode.GUID.ToString() == "@@GUID@@" && Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false).Shown && Gui.GuiService.GetWindow<SidePanelsWindow>(false).Shown'
syswhole() { printf '%s' "$P_SYSWHOLE" | sed "s|@@GUID@@|$1|"; }

# colonized -- `guid:planetCount:name` records for the empire's colonized systems, `|`-joined,
# in the game's own order. Discovery, never written down.
colonized() { fact '((System.Func<string>)(() => { var sb=new System.Text.StringBuilder(); System.Collections.IList css=(System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems; for(int i=0;i<css.Count;i++){ var n=((ColonizedStarSystem)css[i]).Node; System.Collections.IList ps=(System.Collections.IList)n.Planets; if(sb.Length>0) sb.Append("|"); sb.Append(n.GUID).Append(":").Append(ps==null?0:ps.Count).Append(":").Append(n.LocalizedName); } return sb.ToString(); }))()'; }
biggest()  { echo "$1" | tr '|' '\n' | sort -t: -k2,2nr | head -1; }
smallest() { echo "$1" | tr '|' '\n' | sort -t: -k2,2n  | head -1; }
rkey()   { echo "$1" | cut -d: -f1; }
rcount() { echo "$1" | cut -d: -f2; }
rlabel() { echo "$1" | cut -d: -f3-; }

# ensure_system [guid] -- the system-management page, whole, for that system (default: the
# empire's first colonized system). Proceeds at once when it is already up.
ensure_system() {
  g="${1:-$(rkey "$(colonized | tr '|' '\n' | head -1)")}"
  [ -n "$g" ] || { skip "the empire owns no colonized system"; return 1; }
  if isonscreen screen.star-system && waitfor "$(syswhole "$g")" 50 && waitdump 'system:planet/[0-9]+\]' 2; then return 0; fi
  waitfor "$P_NOMODAL" 50 || drain
  evs "((System.Func<string>)(() => { var gm = UnityEngine.Object.FindObjectOfType<GalaxyManager>(); var f = typeof(GalaxyManager).GetField(\"starSystemNodes\", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance); var arr = (StarSystemNode[])f.GetValue(gm); for (int k = 0; k < arr.Length; k++) { if (arr[k].GUID.ToString() == \"$g\") { Gui.GuiGameWindowService.RequestStarSystemManagementViewLevel(arr[k].GUID); return \"opened \" + arr[k].LocalizedName; } } return \"SYSTEM NOT FOUND\"; }))()"
  waitfor "$(syswhole "$g")" 30000 || echo "   NOTE: the system page did not become whole for $g"
  focused screen.star-system || echo "   NOTE: screen.star-system not focused"
  # The page declares nothing until its cards are drawn, and the cards bind after the window
  # shows; the declared planet rows are the whole-ness the mod itself goes by.
  waitdump 'system:planet/[0-9]+\]' || echo "   NOTE: the system page declared no planet row for $g"
  tut
}

# ---------------------------------------------------------------- capture

# reset -- clear the mod's graph state (expansions, cursor seats) so a dump does not depend
# on what an earlier step left open; rendered on the next frame.
reset() { evq "$CS/reset.cs"; frame; frame; }

# snap <path> -- fetch the focused screen's dump WITHOUT recording it as an artifact.
# This is how the walk discovers names: it reads back the tree it is standing in.
snap() { curl -s -o "$1" "$HOST/gui/graph?buffers=1"; }

# waitdump <key ERE> [tries] -- until the mod's own dump holds a node matching the key, one
# frame between looks. This is the wait for a surface the game builds over several frames
# after its window is shown (planet cards bind to their labels frame by frame, and a row is
# a leaf until its card is bound): the mod's dump is the oracle for "declared". 1 on timeout.
waitdump() {
  wd_i=0
  while [ "$wd_i" -lt "${2:-150}" ]; do
    snap "$TMP/wd.txt"
    grep -qE "\[$1" "$TMP/wd.txt" && return 0
    frame; wd_i=$((wd_i+1))
  done
  return 1
}

# dump <name> -- record the focused screen's dump as a diffed artifact
dump() { curl -s -o "$OUT/$SCENARIO-$1.txt" "$HOST/gui/graph?buffers=1"
         printf '%8s  %s/%s-%s\n' "$(wc -c < "$OUT/$SCENARIO-$1.txt" | tr -d ' ')" "$FAMILY" "$SCENARIO" "$1"; }

# capture <name> <ghost label> -- reset the cursor, record where it landed, dump, ghost-audit.
# The reset is what makes two walks comparable, so do NOT use `capture` where the route
# depends on an expansion it just made (the galaxy tree) -- use `dump`.
capture() { reset; at "$1"; dump "$1"; ghosts "$2"; }

# tip <label> -- focused-tooltip capture for whatever the cursor is on. Class-backed tooltip
# text only exists once the tooltip window draws, so the unfocused walk cannot prove it.
tip() { curl -s -o "$TIP/$FAMILY.$SCENARIO.$1.graph.json" "$HOST/gui/graph?buffers=1"
        curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.Tooltip()' "$HOST/eval?speech=0" -o "$TIP/$FAMILY.$SCENARIO.$1.tooltip.json"
        printf '   tip %s\n' "$1"; }

ghosts() { printf '\n=== %s ===\n' "$1" >> "$OUT/ghosts.txt"
           curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.Screen()' "$HOST/eval?speech=0" >> "$OUT/ghosts.txt"; printf '\n' >> "$OUT/ghosts.txt"
           curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.Ghosts()' "$HOST/eval?speech=0" >> "$OUT/ghosts.txt"; printf '\n' >> "$OUT/ghosts.txt"; }

# at <label> -- record where the cursor is. First file to read when a diff looks noisy.
at() { printf '\n--- %s/%s: %s ---\n' "$FAMILY" "$SCENARIO" "$1" >> "$OUT/routelog.txt"
       curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.Screen()' "$HOST/eval?speech=0" >> "$OUT/routelog.txt"; }

# skip <reason> -- a capture this fixture cannot offer. Recorded, never fatal.
skip() { printf '%-28s %s\n' "$FAMILY/$SCENARIO" "$*" >> "$SKIPS"; printf '   SKIP %s\n' "$*"; }

delay0()       { curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.TooltipDelay(0)'  "$HOST/eval?speech=0" >/dev/null; }
delayrestore() { curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.TooltipDelay(-1)' "$HOST/eval?speech=0" >/dev/null; }

# ---------------------------------------------------------------- discovery

# THE CAPTION RULE, which governs every label read below. A region's drawn caption is
# spoken as part of its FIRST row ("Strategic Resources, Titanium, 5, ..."), but the
# type-ahead matches a node's OWN text only -- so the first field of a region's first row
# is a string type-ahead will never find. Two consequences, both load-bearing:
#   * ask for row n=2 of a region whenever "any row of this table" will do;
#   * use label_of only on a node you know is not its region's first row.
# There is no marker in the dump that says "this field is a caption", which is why the
# rule is a discipline rather than a parser.

# label_of <dumpfile> <exact node key> -- the row's own text (first comma-separated field),
# cursor marker stripped. Keys are mod-authored and stable; labels are localized and
# fixture-dependent, which is exactly why they are read rather than written down.
label_of() { grep -F "[$2]" "$1" | head -1 | sed 's/^[ >]*//; s/,.*$//'; }

# label_nth <dumpfile> <key ERE> <n> -- own text of the nth row whose key matches
label_nth() { grep -E "\[$2" "$1" | sed -n "${3}p" | sed 's/^[ >]*//; s/,.*$//'; }

# key_nth <dumpfile> <key ERE> <n> -- the nth matching node key itself. The ERE is the same
# one label_nth/nkeys take: it matches from just after the opening bracket, so end it with
# `\]` when the target is a leaf and not its children.
key_nth() { grep -oE "\[$2" "$1" | sed -n "${3}p" | tr -d '[]'; }

# nkeys <dumpfile> <key ERE> -- how many rows match
nkeys() { n=$(grep -cE "\[$2" "$1" 2>/dev/null); echo "${n:-0}"; }

# ---------------------------------------------------------------- movement

# tland <text> -- land on a node by type-ahead and clear the search. Returns 0 when the
# search had at least one result, 1 when it had none (and the cursor did NOT move --
# never follow a failed tland with ui.click). The /type route answers synchronously.
tland() {
  curl -s -X POST --data-binary "$1" "$HOST/type" > "$TMP/ty.out" 2>&1
  if grep -q '"results":0' "$TMP/ty.out"; then inp ui.back; return 1; fi
  inp ui.back
  return 0
}

# findland <text> [stops] [ui.next|ui.prev] -- land on a node by type-ahead, walking the
# screen's stops in that direction until one of them holds it (a stop that is nearer walking
# backward from the reset cursor is reached in fewer steps). A search reads the FOCUSED stop only. The search drops punctuation
# (parentheses, hyphens, ampersands) out of the QUERY while matching the drawn text, so a
# label holding any is also tried cut at its first punctuation mark: `Foo (Bar)` as `Foo`,
# `Trade & Resources` as `Trade`, `High-Energy Magnetics` as `High` (measured 2026-09-15:
# each such label cost a full stop circuit). Counters are prefixed: sh has no locals, and a
# plain `i` rewrites the caller's counter.
findland() {
  fl_alt=$(printf '%s' "$1" | sed 's/ *[^A-Za-z0-9 ].*$//; s/ *$//')
  fl_i=0
  while [ "$fl_i" -lt "${2:-12}" ]; do
    tland "$1" && return 0
    if [ -n "$fl_alt" ] && [ "$fl_alt" != "$1" ]; then tland "$fl_alt" && return 0; fi
    inp "${3:-ui.next}"
    fl_i=$((fl_i+1))
  done
  return 1
}

# tkey <dumpfile> <key ERE> <n> -- land on the nth node matching the key, by the label it
# reads back. Non-zero when there is no such node, or no stop holds it.
tkey() {
  t=$(label_nth "$1" "$2" "$3")
  [ -n "$t" ] || return 1
  findland "$t"
}

# ---------------------------------------------------------------- the A-B-A rebind check

# A widget the game pools keeps whatever the previous binding left on its components, so a
# dump taken on a fresh binding proves only the fresh case. A scenario over a pooled surface
# reads it (A), rebinds it onto a SECOND subject that SHRINKS the pool (B), returns, reads
# it again (A') and diffs A against A' inside the run. A node whose availability, hint,
# label or tooltip differs between A and A' is carrying B's state.

# gwalk <part> <leg> <key substring> -- the mod's own reading of every node whose key
# contains the substring, with its review buffer, through an /eval walk of the render rather
# than GET /gui/graph (these surfaces run past the dump's 800-line cap). The walk expands
# the subtree it is about to read, so a collapsed card is still in the record.
gwalk() {
  sed "s|@@TARGET@@|$3|" "$CS/rebind-walk.cs" > "$TMP/gw.cs"
  curl -s -X POST --data-binary "@$TMP/gw.cs" "$HOST/eval?speech=0" > "$TMP/gw.out"
  unjson "$TMP/gw.out" > "$OUT/$SCENARIO-$1-$2.txt"
  printf '%8s  %s/%s-%s-%s\n' "$(wc -c < "$OUT/$SCENARIO-$1-$2.txt" | tr -d ' ')" "$FAMILY" "$SCENARIO" "$1" "$2"
}

# gpool <part> <leg> <C# that assigns `t`> -- Visible AND Alpha per pooled child, with the
# first few label texts under it as the bound subject. A retired child parked at Alpha 0
# draws no text, so a graph dump prunes it: this is the half a dump cannot show.
gpool() {
  sed "s|@@CONTAINER@@|$3|" "$CS/rebind-pool.cs" > "$TMP/gp.cs"
  curl -s -X POST --data-binary "@$TMP/gp.cs" "$HOST/eval?speech=0" > "$TMP/gp.out"
  unjson "$TMP/gp.out" > "$OUT/$SCENARIO-$1-$2.txt"
  printf '%8s  %s/%s-%s-%s\n' "$(wc -c < "$OUT/$SCENARIO-$1-$2.txt" | tr -d ' ')" "$FAMILY" "$SCENARIO" "$1" "$2"
}

# aba <part> [part...] -- diff each part's A against its A'. The normaliser is
# walks/normalize.sed and nothing else; a difference that survives it is stale pooled state.
aba() {
  : > "$OUT/$SCENARIO.aba.txt"; aba_n=0
  for part in "$@"; do
    a="$OUT/$SCENARIO-$part-a.txt"; b="$OUT/$SCENARIO-$part-a2.txt"
    if [ ! -f "$a" ] || [ ! -f "$b" ]; then
      printf '%s: not captured on both legs\n' "$part" >> "$OUT/$SCENARIO.aba.txt"; continue
    fi
    sed -f "$WALKS_DIR/normalize.sed" "$a" > "$TMP/na.txt"
    sed -f "$WALKS_DIR/normalize.sed" "$b" > "$TMP/nb.txt"
    d=$(diff "$TMP/na.txt" "$TMP/nb.txt" | grep -c '^[<>]')
    aba_n=$((aba_n+d))
    if [ "$d" -gt 0 ]; then
      printf '===== %s : %s differing lines (A vs A-prime) =====\n' "$part" "$d" >> "$OUT/$SCENARIO.aba.txt"
      diff "$TMP/na.txt" "$TMP/nb.txt" | cut -c1-300 >> "$OUT/$SCENARIO.aba.txt"
    fi
  done
  [ -s "$OUT/$SCENARIO.aba.txt" ] || printf 'A == A-prime for: %s\n' "$*" > "$OUT/$SCENARIO.aba.txt"
  printf '   A-B-A %-26s %s differing lines\n' "$SCENARIO" "$aba_n"
}

# ---------------------------------------------------------------- route shape

# done_ -- the scenario's last line: record the cursor. Nothing is closed; run.sh drains once.
done_() { at done; echo "$FAMILY/$SCENARIO done"; }

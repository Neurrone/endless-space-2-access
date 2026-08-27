# walks/lib.sh -- shared helpers for the regression walk. Sourced, never run.
#
#   FAMILY=galaxy; . "$(dirname "$0")/lib.sh" "$@"
#
# The caller sets FAMILY first; $1 of the family script is the output directory.
# Everything here talks to the dev server over curl and nothing else -- no Python, no jq.
#
# Naming: a graph dump is a `.txt` artifact under <out>/<family>/; a focused-tooltip probe
# is a `.json` under <out>/tooltips/. Both are diffed. `ghosts.txt`, `routelog.txt`,
# `index.txt` and `skipped.txt` are diagnostics and are NOT diffed (see diffwalks.sh).

WALKS_DIR="$(cd "$(dirname "$0")" && pwd)"
CS="$WALKS_DIR/cs"
. "$WALKS_DIR/fixture.env"
HOST="$WALK_HOST"

OUTROOT="${1:-}"
[ -n "$OUTROOT" ] || { echo "usage: $0 <output-dir>" >&2; exit 2; }
mkdir -p "$OUTROOT" || exit 2
OUTROOT="$(cd "$OUTROOT" && pwd)"
OUT="$OUTROOT/$FAMILY"
TIP="$OUTROOT/tooltips"
TMP="$OUTROOT/.tmp"
SKIPS="$OUTROOT/skipped.txt"
mkdir -p "$OUT" "$TIP" "$TMP"
: > "$OUT/ghosts.txt"
: > "$OUT/routelog.txt"

# ---------------------------------------------------------------- primitives

# pause [ms] -- a POST /wait with a `false` body. The one reliable foreground sleep here:
# the Bash tool blocks `sleep`, and a busy loop drifts. Scaled by WALK_PACE.
pause() { curl -s -X POST --data-raw 'false' "$HOST/wait?timeout=$(( ${1:-400} * WALK_PACE / 100 ))" >/dev/null 2>&1; }

# ev <file.cs> -- run an eval body, echo the reply (goes to the family's run log)
ev()  { curl -s -X POST --data-binary "@$1" "$HOST/eval?settle=300"; echo; }

# evq <file.cs> -- run an eval body, speak up only on failure
evq() { curl -s -X POST --data-binary "@$1" "$HOST/eval?settle=300" > "$TMP/ev.out" 2>&1
        grep -q '"ok":true' "$TMP/ev.out" || { echo "EVAL FAIL($1):"; cat "$TMP/ev.out"; echo; }; }

# evs <c# expression> -- run an inline eval body, echo the reply
evs() { printf '%s' "$1" > "$TMP/inline.cs"; curl -s -X POST --data-binary "@$TMP/inline.cs" "$HOST/eval?settle=300" > "$TMP/ev.out" 2>&1
        grep -q '"ok":true' "$TMP/ev.out" || { echo "EVAL FAIL(inline):"; cat "$TMP/ev.out"; echo; }; }

# fact <c# string expression> -- echo just the `result` of an eval. For runtime discovery
# of things a dump cannot show (how many systems the empire owns, what the fleets are called).
fact() { printf '%s' "$1" > "$TMP/fact.cs"
         curl -s -X POST --data-binary "@$TMP/fact.cs" "$HOST/eval?speech=0" \
         | sed 's/.*"result":"//; s/","error.*//; s/.*"result":null.*//'; }

inp()  { curl -s -X POST --data-raw "$1" "$HOST/input" > "$TMP/in.out" 2>&1; pause 400; }
rep()  { n=$1; shift; i=0; while [ "$i" -lt "$n" ]; do inp "$1"; i=$((i+1)); done; }

# ---------------------------------------------------------------- capture

# snap <path> -- fetch the focused screen's dump WITHOUT recording it as an artifact.
# This is how the walk discovers names: it reads back the tree it is standing in.
snap() { curl -s -o "$1" "$HOST/gui/graph?buffers=1"; }

# dump <name> -- record the focused screen's dump as a diffed artifact
dump() { curl -s -o "$OUT/$1.txt" "$HOST/gui/graph?buffers=1"
         printf '%8s  %s/%s\n' "$(wc -c < "$OUT/$1.txt" | tr -d ' ')" "$FAMILY" "$1"; }

# tip <label> -- focused-tooltip capture for whatever the cursor is on. Class-backed tooltip
# text only exists once the tooltip window draws, so the unfocused walk cannot prove it.
tip() { curl -s -o "$TIP/$FAMILY.$1.graph.json" "$HOST/gui/graph?buffers=1"
        curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.Tooltip()' "$HOST/eval?speech=0" -o "$TIP/$FAMILY.$1.tooltip.json"
        printf '   tip %s\n' "$1"; }

ghosts() { printf '\n=== %s ===\n' "$1" >> "$OUT/ghosts.txt"
           curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.Screen()' "$HOST/eval?speech=0" >> "$OUT/ghosts.txt"; printf '\n' >> "$OUT/ghosts.txt"
           curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.Ghosts()' "$HOST/eval?speech=0" >> "$OUT/ghosts.txt"; printf '\n' >> "$OUT/ghosts.txt"; }

# at <label> -- record where the cursor is. First file to read when a diff looks noisy.
at() { printf '\n--- %s ---\n' "$1" >> "$OUT/routelog.txt"
       curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.Screen()' "$HOST/eval?speech=0" >> "$OUT/routelog.txt"; }

# skip <reason> -- a capture this fixture cannot offer. Recorded, never fatal.
skip() { printf '%-14s %s\n' "$FAMILY" "$*" >> "$SKIPS"; printf '   SKIP %s\n' "$*"; }

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
# never follow a failed tland with ui.activate).
tland() {
  curl -s -X POST --data-binary "$1" "$HOST/type" > "$TMP/ty.out" 2>&1
  pause 500
  if grep -q '"results":0' "$TMP/ty.out"; then inp ui.back; return 1; fi
  inp ui.back
  return 0
}

# ---------------------------------------------------------------- windows

# openwin/hidewin/exitwin take a GuiWindow NAME, so a window this build does not have is a
# message rather than a compile error. exitwin does not reliably hide every modal
# (LawsManagementModalWindow, GovernmentModalWindow) -- always pair it with hidewin.
openwin() {
  printf '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow("%s"); if (w==null) return "NO WINDOW %s"; Gui.GuiService.ShowWindow(w); return "show %s shown="+w.Shown; }))()' "$1" "$1" "$1" > "$TMP/open.cs"
  curl -s -X POST --data-binary "@$TMP/open.cs" "$HOST/eval?settle=400" | grep -oE '"result":"[^"]*"|"error":"[^"]*"'
  pause "${2:-1800}"
}
hidewin() {
  printf '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow("%s"); if (w==null) return "NO WINDOW"; Gui.GuiService.HideWindow(w); return "hide %s shown="+w.Shown; }))()' "$1" "$1" > "$TMP/close.cs"
  curl -s -X POST --data-binary "@$TMP/close.cs" "$HOST/eval?settle=300" >/dev/null; pause 1000
}
exitwin() {
  printf '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow("%s"); if (w==null) return "NO WINDOW"; w.HandleInput(InputAction.Exit); return "exit %s shown="+w.Shown; }))()' "$1" "$1" > "$TMP/close.cs"
  curl -s -X POST --data-binary "@$TMP/close.cs" "$HOST/eval?settle=300" >/dev/null; pause 1000
}

# ---------------------------------------------------------------- route shape

# prologue -- every family script's first act, and idempotent: drain any modal left up,
# minimise the tutorial popup (expanded, it eats every injection as `unconsumed`), and
# reset the mod's graph state so the walk starts from one tree shape and one cursor.
prologue() { evq "$CS/drain.cs"; evq "$CS/tut.cs"; pause 1500; evq "$CS/reset.cs"; pause 900; }

# epilogue -- leave the fixture as the next family expects to find it
epilogue() { evq "$CS/drain.cs"; evq "$CS/tut.cs"; pause 1200; at epilogue; echo "$FAMILY route done"; }

# capture <dumpname> <ghost label> -- reset the cursor, record where it landed, dump,
# ghost-audit. The reset is what makes two walks comparable, so do NOT use `capture`
# where the route depends on an expansion it just made (the galaxy tree) -- use `dump`.
capture() { evq "$CS/reset.cs"; pause 900; at "$1"; dump "$1"; ghosts "$2"; }

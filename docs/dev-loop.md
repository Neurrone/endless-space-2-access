# ES2 dev loop — build, reload, verify

Fixtures: **ask `DevProbe.Saves()` first — the named ones are GONE.** As of 2026-08-22 this
install holds five `%AutoSaveFileName` autosaves (turns 3-7) and nothing else; `[Beginner] test`
(turn 21) and `[Midgame] quests fleets` are no longer on disk, and `POST /loadsave` with either
title answers `no save titled …`. An empty body loads the newest.
Screen-by-screen status: `docs/roadmap.md`; working a specific screen and its fixture
limits: `docs/test-recipes.md`. This file is
ONLY the loop: the dev server, the REPL, and the screen-agnostic verification patterns.

| Task | Read first |
|---|---|
| Any screen work, start to finish (the process) | `docs/generic/making-screens-accessible.md` |
| Modeling/navigating mechanics (graph, stops, rows, regions, focus) | `docs/generic/ui-navigation.md` |
| Anything keys: bindings, repeat, stand-down, game collisions | `docs/generic/input.md`, then `docs/interaction.md` |
| ES2 layers, key map, claim rules (building a screen) | `docs/interaction.md` |
| Which helper already exists for X | `docs/helpers.md` |
| Working a specific screen against the live game | `docs/test-recipes.md` (grep the screen) |
| Widget kinds, roles, announcements, activation idioms | `docs/generic/widgets.md` |
| Review buffers / re-readable content | `docs/generic/buffers.md` |
| Tooltips (short/long rule, drawn readback, visual parity) | `docs/generic/tooltips.md` |
| Inline icons / symbols in text | `docs/generic/icons-and-symbols.md` |
| Dev server, REPL, test loops | `docs/generic/dev-server.md`, then §1–2 here |
| Speech pipeline / interruption | `docs/generic/speech.md` |
| Localization / ModStrings / exact game text | `docs/generic/localization.md` |
| Decompiled-code research | `docs/generic/reverse-engineering.md` |
| Hot reload / loader boundaries | `docs/generic/hot-reload.md` |
| Per-frame cost, GC hitches, scan/allocation discipline | `docs/generic/performance.md` |
| New-game bring-up on another title | `docs/generic/new-game-playbook.md`, `project-bootstrap.md` |
| Game-mechanism findings | `docs/es2-facts.md` |
| ES2 architecture / AGE GUI framework orientation | `docs/es2-architecture.md`, `docs/es2-gui-framework.md` |

## 1. Dev server — quick reference

Gates: off by default — `devServer = true` under `[Dev]` in
`BepInEx\config\endless.space2.access.cfg` (`run-game.ps1` writes it; `-NoDev` for off).
`ES2ACCESS_NO_DEV=1` forces off; `ES2ACCESS_DEV_PORT` overrides; `ES2ACCESS_NO_SPEECH=1`
mutes voicing but `/speech` still captures.

- `GET /status` — mod state, `modAssemblyName`, the `keyStandDown` patch tripwire (SIX
  prefixes now: the three key scans, `AgeControlTextField.KeyDown`,
  `InGameChatPanel.HandleInput`, and `AgeManager.set_FocusedControl`)
- `GET /speech?since=N&wait=MS` — spoken ring buffer (resets on reload); `wait` long-polls
- `GET /gui/graph?edges=1&buffers=1` — the focused screen's whole accessible tree
- `GET /gui/graph?screen=KEY` — what an UNFOCUSED registered screen would offer, built without
  focusing it; an inactive one answers `screen inactive: …`, a bogus key 400s with the key list
- `POST /input` — body = one action key (`ui.down`, `buffer.lineDown`…); its key-claim counterpart is
  `/eval ES2Access.Dev.DevProbe.Claims("Escape")` — the latch only lives for the frame an injection
  is consumed (no key was held), so catch it with `POST /wait` on the probe's own text, never a
  second request. `DevProbe.Chord("<chord>")` is the question about a CHORD — `Claims` is asked per
  `KeyCode` and so hides chords entirely; `Claims` also reports `leftToGame`
- `POST /type` — body = characters to TYPE at the focused screen (the type-ahead search), through the
  same gates a keypress passes; answers `taken`/`searching`/`search`/`results`/`focus` plus the speech
  it caused. `/input` cannot carry it: that queue is actions, and typing is text. Neither reaches a
  field the GAME owns — the letters queue against the mod's own type-ahead and fire as a search the
  moment the field lets go, so a game-owned edit is driven by writing its text from `/eval`
- `GET /gui/game?path=&depth=` — Unity hierarchy; `GET /gui/age?window=&depth=&visibleOnly=` —
  AGE widgets with rects (`window=` is the filter; `/gui/game` is the one taking `path=`)
- `window=` matches a registered window, a shown panel, then any named AgeTransform under them,
  and `depth=`/`visibleOnly=`/`fields=` apply from there; an empty answer always carries an
  `error`/`note` line, and a node cut off by `depth=` is kept (`more:true`), never pruned. The
  dump PRUNES any node with no control, no text, no value and no readable tooltip
  (`AgeDump.Node.Speaks`) — the JSON and the `fields=` forms alike — and "readable" means
  class-free: a CLASS-backed tooltip does not save a textless icon from the prune even when it
  DOES carry a content sentence (the minor-faction button's does — `AgeWidgets.Readable`
  rejects by class, not by absence of content). A panel's icon-only controls are found with an `/eval` walk of
  `.Children`, never with a tree dump
- `GET /gui/age?...&fields=name,kind,text,tooltip,rect,interactable,enabled` — flat text, one
  indented line per widget, only those fields, empties omitted
- `POST /eval?settle=MS&speech=0` — C# REPL (gotchas below); response carries caused speech.
  `settle` is honoured ONLY while speech capture is on — with `speech=0` there is no wait at
  all; substitute `POST /wait` with body `false` and a timeout
- `POST /wait?timeout=MS` — body = bool expression, evaluated every frame; the wait is capped at
  ~60 s whatever is asked for, so a longer silence is proved by repeating the poll
- `POST /loadsave` — body = save title (empty = newest); retryable `[not ready]` until it acts —
  except from a LOBBY, where not-ready is the answer until the lobby is left, never a retry.
  Issued while the PLANET-OVERVIEW page is up it can wedge the loading window at "Game launched
  and ready" indefinitely (`wait-game.ps1 ingame` then times out silently with exit 0);
  re-issuing the same `POST /loadsave` recovers in ~8 s
- `POST /key?hold=MS&gap=MS&text=1` — body = a key SEQUENCE pressed as real OS key events at
  the game's window (`Return`, `Ctrl+I`, `Shift+Tab`; `+Name` holds, `-Name` releases;
  `text=1` makes the body characters to type; arrows are `UpArrow`/`DownArrow`/…). The only
  route where a key is physically DOWN — so the only one that can test the consumed-key
  latch, `anyKeyDown`, the engine's KeyDown delivery to a focused control, and "was Return
  still down when the focus left". REFUSES (409, nothing sent) unless the foreground window
  is proved to belong to the game's process, re-checked before every step — a locked
  workstation or unfocused game answers 409 rather than typing into the owner's desktop;
  400 for an unknown key name (the answer lists the vocabulary)
- `GET /log?since=N&grep=TEXT` — no `since` answers only the last 100 entries (`capped:true`);
  `grep` still searches the whole ring; `GET /screenshot`; `POST /quit` — shutdown takes
  20–100 s: poll the PROCESS (not the port) every 2 s and only conclude a hang past 120 s
- **Every route rejects a query parameter it does not declare** — 400 naming it and listing the
  route's own; a typo can no longer look like a broken feature
- `POST /reload` (needs `Content-Length`). Empty-body POSTs (`/reload`, `/quit`): under the
  PowerShell tool `curl.exe --data-raw ''` silently drops the argument — use
  `Invoke-WebRequest -Method Post -Body "" -UseBasicParsing` (without `-UseBasicParsing` it
  fails in NonInteractive mode); from the Bash tool `--data-raw ""` works. `GET /loader/status` —
  `staleBuild`, `failedReloadCount`, `lastReloadError`; confirm reloads here, never by
  assuming a 503 meant failure

During boot/loading, main-thread routes 503 — retry; `/speech` and `/log` keep answering.

### REPL gotchas (`POST /eval`)

- Multi-statement bodies ARE accepted, and top-level `var` declarations PERSIST across
  requests (a handle bank set once serves a whole sweep) — the poisons below still apply
  to every statement. No `using` directives — fully qualify everything.
- Never declare a local whose type is a constructed generic over a game type; **a `foreach`
  over `AgeTransform.Children`, `GetPlayerEmpireGuiNotifications()` or any `List<GameType>`
  declares one implicitly**, and it poisons the WHOLE session — every later request answers
  with a `MakeGenericType` InternalErrorException. Iterate by index or bind as
  `System.Collections.IList` — some collections reject that cast outright; fall back to
  `((IEnumerable)x).GetEnumerator()`. Recover with `POST /reload`. (REPL-only crutch: in shipped
  code, `as IList` over a yield iterator answers null silently — walk the declared
  interface.)
- Bare `Time` binds to `InteractiveBase.Time(Action)`; write `UnityEngine.Time`.
- `/reload` wipes the REPL session (variables, usings) and the speech ring.
- Quote-bearing bodies: a file plus `--data-binary "@file"`, or the Bash tool.
- **Many probes in one request**: wrap them in an immediately-invoked
  `((System.Func<string>)(() => { ... }))()` and return a `StringBuilder`. Still one
  statement, and the body may declare locals and loop — as long as no local is a constructed
  generic over a game type (index the collection, never `foreach` it).
- No captured delegates inside that lambda: assigning a captured `Action`/`Func` local (or
  passing one to a method) answers with an `InternalErrorException`. Keep eval bodies
  delegate-free — inline the code or call a static.
- The IIFE-lambda crutch does NOT work as a `POST /wait` predicate — it silently evaluates
  false every frame. A wait body must be a plain expression; side effects are fine
  (`DevProbe.TooltipTrace(...)` is one), lambdas are not.
- `DevProbe.Notifications()` is the one answer for the notification engine's state
  (mappings + owning assembly, patch owners, subscriptions, table sizes) — ask it before
  hand-probing that machinery.
- SOME descriptor-driven simulation properties shrug off `SetPropertyBaseValue` + `Refresh` —
  the descriptor recomputes them (measured: `Fleet.FreeMovementSpeed` stayed 0) — but not all:
  `Empire.CanUseStrategicForRecipe` took the write and stuck (measured 2026-08-19). Try the
  write with a read-back probe first; if it reverts, grant the DESCRIPTOR's source (tech, law)
  or accept the state as fixture-blocked.


## 2. Verification patterns (screen-agnostic)

**Stage hygiene** (cost scales with tool-call count — ~1.5–2k tokens and ~18 s per call):
fewer, bigger calls. Scope every grep to a named subtree (unscoped greps over
`decompiled/` time out). Grep-before-read for any file > 800 lines; Read only the method
bodies you need via offset. `/gui/age` or `/gui/graph` dump FIRST — it answers layout and
text; decompiled classes only for action paths; re-read the dump already in hand before
probing or walking. Scope `/eval` probes to the one entity in question; bound `/log` with
`since=`; print counts, not enumerations. Python helpers as script files, never
`python -c` (the Bash tool corrupts multiline); `crop-shot.ps1` via the PowerShell tool.
Build from the repo root only; after every reload confirm `modAssemblyName` incremented
before interpreting live results. Repeated-node `ControlId` keys: index-in-parent, never
widget names. Interim narration one line — findings go in the final report; never re-Read
an image.

**Session loop.** `.\run-game.ps1 -NoSpeech -NoWait -LoadSave "[Beginner] test"` —
cold launch to in-game in one command; `.\wait-game.ps1 <menu|ingame|loading|dialog>` blocks
on a state. Boot ≤ 1 min. Both scripts via the PowerShell tool (Bash-invoked PowerShell hits
execution policy). First act in-game: minimize the tutorial popup (recipe in
`test-recipes.md`) — expanded, it eats every injection as `unconsumed`.

**Reload loop.** `dotnet build ES2Access/ES2Access.csproj` → `POST /reload` →
`GET /loader/status` (`staleBuild:false`, `modAssemblyName` incremented). It can answer
BEFORE a queued reload has run (`staleBuild:true`, old name) — poll again, don't rebuild.

**Evidence crop.** A Class-backed tooltip's review buffer reads EMPTY in `/gui/graph?buffers=1`
unless the node is focused first (its words only exist once the tooltip window draws them — see
"Auditing a tooltip" below). `.\crop-shot.ps1 -Rect x,y,w,h [-Out path]` — never Read a full-frame
screenshot into context. Invoke via the PowerShell tool or
`powershell -Command "& './crop-shot.ps1' -Rect x,y,w,h"`; `powershell -File` mangles the
`-Rect` array argument, and the Bash tool's quoting breaks it too.
**On a pooled table the crop is the oracle, not the dump.** A retired row parked at alpha 0 draws
no text, so `/gui/age` prunes it and the dump agrees with whatever the mod declared — parity that
is really a blind spot. Print `Alpha` beside `Visible` in an `/eval` walk and check it against a
`crop-shot.ps1` of the same rect.

**An un-watched announcement part is still ASKED every frame.** `GraphNavigator.FillBuffer`
(:470 → :562-570) recomposes the focused node's whole `LeafText` each tick so the buffer can
notice a control changing underneath the cursor — `watch: false` means "not compared", never "not
asked". An expensive answer (a pathfinding search) needs a memo keyed on the state it was computed
from; prove the fix with a call counter read across ~100 frames, because no transcript or dump
shows the waste (`FleetRoute.Searches`: 121 focused frames → 0 searches).

**The renderer-field oracle.** When the mod recomputes something the game only DRAWS, drive the
game's own renderer from `/eval` and read its private display list by reflection
(`IPathRendererService.ClearPathDataAndRenderPath` → `PathRenderer.pathDatasToDisplay`, then
`ClearPathData()`), and compare classifications, not pixels. It proves parity with the DRAWING
only — when the drawn thing is a prediction, the second oracle is letting the game run and
watching what happens (the map's own turn markers were one low; end-turning caught it).

**Auditing a tooltip.** `DevProbe.TooltipDelay(0)`, focus via `/input`, then all three
(caveat: delay 0 changes WHICH frame the game resolves the request on — a tooltip that
works at 0 is not proof it works at the player's 0.3 s, nor vice versa; a stalled request
is `DevProbe.TooltipPipe()`'s `timer` near 999, invisible to every drawn-window probe):
`/screenshot`, `DevProbe.Tooltip()` (a `features` array — class name, the reader that answered,
the lines it produced — plus the measured rows/rects/assets), `/gui/graph?buffers=1`. A feature
class sitting on `"default"` whose lines divorce a value from its caption is the defect to look
for — but the prefab may already carry the caption as sibling labels, so read the DRAWN
feature before believing the class; nothing about it shows in the spoken lines alone. `shown:false` on a focused node whose
buffer stays empty despite a declared tooltip is the OTHER signature — a mis-aimed pointer (the
2-arg `AgeWidgets.Point`, re-deriving from the control's transform). The parity audit's "points at
one that draws nothing" is a DIFFERENT finding with a different fix: it judges by the audit's own
aim derivation — exact for control nodes since 1016af3, an approximation for drawn rows/cells — so
confirm with `DevProbe.Tooltip()` before touching any pointing call.
`/gui/graph` alone misleads here: it moves no pointer, so a renderer-drawn tooltip is
undrawn and its buffer reads empty on a control that is fine live. Re-probing a still-focused
node after mutating its backing state answers the PRE-mutation content — leave the node and
return before re-probing. `TooltipDelay(-1)` after.


**A card's tooltip is rarely on the card.** `PointerFocus` shows the tooltip of the widget it is
pointed AT, so pointing at a row whose tooltip hangs off a child inside it (the planet card's
anomaly rows) draws nothing while the node still declares the tooltip and its buffer stays empty. Point at
`tooltip.AgeTransform`, not at the row — and prove it with `DevProbe.Tooltip()`, which is the
only thing that catches it. The same trap bites reading, not just aiming: a component's tooltip
may hang off a descendant, so a group walk reading `widget.AgeTooltip` gets silence — read the
component's own Tooltip field.

**A tooltip family's evidence pair.** Focus the control, `DevProbe.Tooltip()` for the typed
reading, then `Gui.GuiService.GetWindow<GuiTooltipWindow>(false).AgeTransform.GetGlobalPosition()`
for the rect and `crop-shot.ps1` on it — the tooltip is anchored to the pointer, so its rect
moves between runs and a crop from an earlier probe lands on empty sky.

**Testing a type-ahead search.** `POST /type` with the letters (`res`), read the `speech` array it
answers with, then drive the results through `/input ui.down|ui.up|ui.home|ui.end` and end with
`/input ui.back` ("Search cleared"). The key-claim half is `DevProbe.Claims("Escape,R,Space")`: with
a search up, all three read `claims:true` and `claimsBack:true`; after Escape clears it, Escape goes
back to the game (`claims:false`) while the letters stay claimed, because type-ahead is armed
whenever a mod screen is focused. Each keystroke re-announces the landing, so `/type "res"` answers
with three identical lines — that is the design, not a stutter. `POST /type` searches only the
FOCUSED stop, and `ui.activate` while a search is live ends the search and then performs the
landing's ordinary action — on a sort header that is a stray sort. Never follow a 0-result
`/type` with `ui.activate`; clear with `ui.back` first.

**Tracing a transition frame by frame.** A screen change is frames long and polling from outside
samples between them, so the frame that moved the cursor is invisible. `POST /wait` evaluates its
predicate EVERY frame and does not block `/eval`, so a predicate that LOGS and returns false is a
per-frame recorder: start `POST /wait?timeout=30000` with body
`ES2Access.Dev.DevProbe.Trace("tag")` in the background, drive the transition with `/eval`, then read
`GET /log?since=0&grep=trace` (collapse runs of identical lines — a 30 s trace is ~1800 of them).
Each line is the stack, the focused screen, the cursor, the node count that screen declared, and the
tutorial/window state. A single-digit node count on an active page is a page declaring somebody
else's content. The same per-frame evaluation makes a PLAIN boolean predicate an existence test over
a whole transition: `satisfied:false` after N frames proves no frame in that window had the property
(154 frames of a popup's arrival, never ready-and-unpainted) — but only once a weaker predicate that
DOES fire proves the window was really sampled.

**Injecting a sequence of keys.** `POST /input` one action key per request, ~0.4 s apart —
a no-delay loop does not fail loudly, it reports a plausible WRONG route (rows appearing
unreachable by Down) — then
read `/speech?since=N` — `next` from a `since=0` read before the sequence is the baseline. The
Bash tool mangles `python -c` here (it injects `|| goto :error`); keep the JSON formatting in a
`.py` file in the scratchpad.

**A behaviour that branches on a key BEING DOWN cannot be tested with `/input`.** An injected
action presses nothing, so `Input.anyKeyDown`, `GetKey`, the consumed-key latch and every
engine dispatch gated on them read as if the keyboard were idle — a green injected run is
silent about the whole press. Use `POST /key`; if it answers 409 the desktop is locked or the
game is not focused, and the claim stays UNPROVEN rather than becoming a manual-test line by
default.

**Silence in `/speech` is only evidence for controls that would have spoken.** An enabled
button's activation is also silent, so a transcript cannot distinguish "refused" from
"acted" for buttons — prove a button refusal with a state probe (queue count, graph dump),
never by absence of speech. Checkbox/slider/combo refusals are provable by silence.

**Proving a two-step mode's confirm when the fixture cannot let the order land.** Watch the MODE
end — the cursor swapped back, the banner gone — not the order's effect, and pair it with the same
key on the same node with no mode up, which must still do the node's own thing.

**Proving a refactor changed no spoken or buffer line.** Walk every reachable screen family
with `POST /input` and save `GET /gui/graph?buffers=1` per family to a scratchpad `before/`,
make the change, walk the identical route into `after/`, and `diff`. Normalise the ids that
carry an instance hash (`droplist:-138580/…`) before diffing. Two things make it work: the
dump is text and stable, and unfocused Class-backed tooltips read EMPTY on both sides, so
they cancel. For a family whose "before" you only realise you need afterwards,
`git stash push -u -- ES2Access ES2Access.Tests` → build → `/reload` → capture → `git stash
pop` → build → `/reload` costs about three minutes and is how `screen.game-menu` and
`screen.rename` got baselines. A **sheet** refactor's baseline must be captured in ONE game
session: `GraphSheet` row keys derive from `GetHashCode()`, which survives a hot reload but not
a process restart — the stash loop, never two launches. For a purely ADDITIVE announcement
change there is a cheaper before: null the injected dependency that produces the new part
(`GraphAnnouncer.Carry = null`) and dump, instead of stashing the source. The stash loop is
UNSAFE while another stage edits the same trees — the push takes their in-flight files too.
A camera-dependent focus visual must be re-committed when the camera moves (`OnFocusVisual`
fires once per focus CHANGE); `Navigator.ClearVisual()` from OnUpdate re-commits same-frame.
`GET /gui/graph?screen=KEY&buffers=1` reaches screens whose
window exists without a game running — out of a session `screen.game-menu` and
`screen.rename` both declare real content, `screen.galaxy` and friends answer "not active".
An INACTIVE screen's by-key dump holds only the shared HUD stops plus stale content — worthless
as a baseline for the screen's own content; open the screen first.

**Sighting a surface the fixture never draws.** Tier zero, before any of the four below: read the
prefab's own fields off the UNSHOWN window instance (`GetWindow<T>(false)`, no Bind, no Show,
nothing to restore) — enough for a caption's tooltip class/content, but beware prefab `%key`
content the game rewrites at bind (read the bind code before believing "no sentence"). Then four
tiers, cheapest first: `Show()` the
game's own pooled widget, read, `Hide()` — the game's next visibility pass restores truth by
itself; or set the game's OWN `Visible` flags and private fields from `/eval`, dump, restore,
and re-diff the dump against the untouched one to prove nothing was left behind (this is how a
whole DLC feature branch gets sighted); or, for a window with data, `Bind` + `Show`, read, then
`Unbind` + hide. A forced-show proves STRUCTURE, never content, and a half-bind can outlive the
probe — restore a monotonic setter through its backing field or private setter, and re-issue
`POST /loadsave` if a window wedges. Never force-show a DLC modal without its data. The fourth
beats all three where the empty widget is generic over an INTERFACE: lend it another
implementor's data (`Bind(otherOwner, client)` + `RefreshNow()`) and the game itself draws real
content into the unreachable panel — the mod's rows, their wording, their ordering and the
pick-up all verified live, with one `Bind` back to restore. A force-show proves structure; only
lent data proves content. Never commit an action while the binding is lent. When a forced show
fights a live per-frame gate, read the underlying authored data (the curve table) instead of
the animated runtime value.

**Proving a watcher stays silent** is a long poll on the watched flag, not a scan of `/speech`:
`POST /wait` on the game's own condition, then read `/speech?since=N` for the window that
elapsed. Because the wait caps at ~60 s, a claim of "silent for minutes" is several polls.

**Splitting one buffer section into two loses `AddLine`'s cross-list dedupe** — nothing
de-duplicates ACROSS sections. After moving a tooltip out of a details function, re-read the
node's buffer FOCUSED: a drawn tooltip repeating a computed line is invisible in the unfocused
dump.

**Opening a game modal from `/eval`** (to measure it without walking there): set what its
opener sets, then show it; close it the way Escape does (`w.HandleInput(InputAction.Exit)` —
Assembly-CSharp's `InputAction`, not `Amplitude.Unity.Input`'s). Worked routes per window —
improvements list, rename box, load/save in Save mode, delegate-installed openers — are in
`docs/test-recipes.md` ("Opening game modals from /eval").

**State restoration etiquette.** Leave the fixture as found: tutorial popup MINIMIZED, no
notifications pending, camera at home (`DevProbe.Camera()` before and after), no text field
holding game focus (`AgeManager.Instance.FocusedControl = null`), `DevProbe.TooltipDelay(-1)`
(a set delay survives reloads on purpose — and so does the restore cache being LOST by a
reload, which makes one `-1` put back whatever was set at the time of the last reload; check
`now` against `registry` in the reply and call it twice if they differ).

## 3. Keeping this file honest

This file is ONLY the loop, and it stays under ~300 lines — over that, a stage moves
something out before adding. Everything else has a chartered home: a new HELPER lands in
`docs/helpers.md` (one row) and its own doc comment; a new LAYER NUMBER or KEY in
`docs/interaction.md`; a PER-SCREEN recipe or fixture note in `docs/test-recipes.md`;
a game-mechanism fact in `docs/es2-facts.md`; screen status in `docs/roadmap.md`;
game-agnostic lessons go to the proposals ledger for the main agent, never into
`docs/generic/` directly. Only a change to the loop itself — a route, a REPL gotcha, a
screen-agnostic verification pattern — lands here. When a design is reversed or content
moves between docs, grep the whole docs tree for the old mechanism's name and for inbound
references before calling the change done — stale rows state reverted designs as current.

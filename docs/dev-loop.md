# ES2 dev loop — build, reload, verify

Fixtures and what each save shows: `docs/test-recipes/fixtures.md` (`DevProbe.Saves()` reports
titles). Screen-by-screen status: `docs/roadmap.md`; working a specific screen and its fixture
limits: `docs/test-recipes/README.md`. This file is
ONLY the loop: the dev server, the REPL, and the screen-agnostic verification patterns.

| Task | Read first |
|---|---|
| Any screen work, start to finish (the process) | `docs/generic/making-screens-accessible.md` |
| Modeling/navigating mechanics (graph, stops, rows, regions, focus) | `docs/generic/ui-navigation.md` |
| A camera-rendered world: map, zoom tiers, fog, places | `docs/generic/world-navigation.md` |
| Anything keys: bindings, repeat, stand-down, game collisions | `docs/generic/input.md`, then `docs/interaction.md` |
| ES2 layers, key map, claim rules (building a screen) | `docs/interaction.md` |
| Which helper already exists for X | grep `ES2Access/` — a helper's contract is its own doc comment; the dev/verification ones are in §1 below |
| Working a specific screen against the live game | `docs/test-recipes/README.md` (grep the screen) |
| Widget kinds, roles, announcements, gesture parity and activation idioms, popups, the confirmation dialog | `docs/generic/widgets.md` |
| Game-mechanism findings | the topic file that fits (`docs/README.md` indexes them) |
| Any other generic concern (speech, buffers, tooltips, icons, localization, hot reload, performance, dev server, decompiled research, bootstrap) | `docs/generic/README.md` indexes the chapters |
| ES2 architecture / AGE GUI framework orientation | `docs/architecture.md`, `docs/gui.md` |

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
  focusing it, GATED like the player's own render (`ungated=1` answers the raw declared tree
  instead); an inactive one answers `screen inactive: …`, a bogus key 400s with the key list
- `POST /input` — body = one action key (`ui.down`, `buffer.lineDown`…); its key-claim counterpart is
  `/eval ES2Access.Dev.DevProbe.Claims("Escape")` — the latch only lives for the frame an injection
  is consumed (no key was held), so catch it with `POST /wait` on the probe's own text, never a
  second request. `DevProbe.Chord("<chord>")` answers per key code too (`LeavesToGame` is its
  extra) — a chord-level claim needs `POST /key` with `hold=250&gap=150`; `Claims` also reports `leftToGame`
- `POST /type` — body = characters to TYPE at the focused screen (the type-ahead search), through the
  same gates a keypress passes; answers `taken`/`searching`/`search`/`results`/`focus` plus the speech
  it caused. `/input` cannot carry it: that queue is actions, and typing is text. Neither reaches a
  field the GAME owns — the letters queue against the mod's own type-ahead and fire as a search the
  moment the field lets go, so a game-owned edit is driven by writing its text from `/eval`
- `GET /gui/game?path=&depth=` — Unity hierarchy; `GET /gui/age?window=&depth=&visibleOnly=` —
  AGE widgets with rects (`window=` is the filter; `/gui/game` is the one taking `path=`)
- `window=` matches a registered window, a shown panel, then any named AgeTransform under them;
  `depth=`/`visibleOnly=`/`fields=` apply from there; an empty answer carries an `error`/`note`
  line; a node cut off by `depth=` is kept (`more:true`). The dump PRUNES any node with no
  control, text, value or readable tooltip (`AgeDump.Node.Speaks`), and "readable" means
  class-free — a CLASS-backed tooltip does not save a textless icon from the prune. Icon-only
  controls are found with an `/eval` walk of `.Children`, never with a tree dump
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
  re-issuing the same `POST /loadsave` recovers in ~8 s — that recovery is the PLANET-OVERVIEW
  case only. NEVER issue it while the end-turn button reads "Pending": into a wedged turn it
  kills the process (why: `install.md`, "The Mono runtime under the REPL")
- `POST /key?hold=MS&gap=MS&text=1` — body = a key SEQUENCE pressed as real OS key events at
  the game's window (`Return`, `Ctrl+I`, `Shift+Tab`; `+Name` holds, `-Name` releases;
  `text=1` types the body; arrows are `UpArrow`/`DownArrow`/…). The only route where a key is
  physically DOWN (the consumed-key latch, `anyKeyDown`, engine KeyDown delivery, "was Return
  still down when the focus left"). REFUSES (409, nothing sent) unless the foreground window is
  the game's, re-checked every step; 400 for an unknown key name (the answer lists the vocabulary)
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

### Verification helpers (`/eval` one-liners)

The full contract of each is its own doc comment; this is the inventory.

| Helper | One line | File |
|---|---|---|
| `DevProbe` | `Screen() Stack() State() Saves() Camera() Windows() Patches() Claims(keys?) TooltipDelay(s) Tooltip() UnknownIcons() Sounds()` (the last: what the carry asked the game to PLAY, which nothing can hear over the wire) | `ES2Access/Dev/DevProbe.cs` |
| `DevProbe.Trace(tag)` | One LOG line per frame (stack, focused screen, cursor, declared node count, tutorial/window state) and always false, so `POST /wait` on it records a whole transition | `ES2Access/Dev/DevProbe.cs` |
| `DevProbe.RowTrace(tag)` | Per-frame recording of what the FOCUSED control would say — off the LAST BUILT render, so a rebuild-less change shows late | `ES2Access/Dev/DevProbe.cs` |
| `DevProbe.TooltipTrace(tag)` / `TooltipPipe()` | The hover-to-tooltip pipeline in one line (`999` timer = the parked request); `TooltipTrace` logs per frame and is always false, `TooltipPipe` answers one poll | `ES2Access/Dev/DevProbe.cs` |
| `DevProbe.Claims("Escape,Return")` | What the input layer claims FROM the game, side-effect-free, per named key (the latch, `claimsBack`, `layerLive`, `leftToGame`) | `ES2Access/Dev/DevProbe.cs` |
| `DevProbe.EndEdit(commit)` / `ArmCommit()` | Fallback levers for a text edit's endings when `POST /key` cannot run (locked desktop, unfocused game); real key events stay the primary route | `ES2Access/Dev/DevProbe.cs` |
| `DevProbe.Notifications()` | The notification engine's live state in one answer — mapping count + owning assembly, patch owners per hooked method, the turn subscription, last-seen table size, and the influence sweep's `ground*` counters | `ES2Access/Dev/DevProbe.cs` |
| `DevProbe.TooltipParity()` | The promised/misaimed/unraised/unaimed/uncovered/misclassed tooltip self-check on the FOCUSED screen, eleven buckets (seven findings, four context), aim read off `NodeVtable.PointsAt` rather than re-derived; `unraised` is the one that asks whether the pointer is ever MOVED there, `unaimed` the one that asks whether the node aimed at all, `misclassed` the one that asks whether the tooltip's KIND decided how it reached the player | `ES2Access/Dev/TooltipAudit.cs` |
| `DevProbe.NotificationParity()` | The notification family's self-check on whichever popup is up — painted-but-unsaid, spoken-but-undrawn, mis-banded, promised/lost tooltips, figures spoken with no caption; also runs by itself on every popup (`/log?grep=parity`) | `ES2Access/Dev/NotificationAudit.cs` |
| `DevProbe.Coverage(wholeTree?)` | What the FOCUSED screen never declared (tooltips AND actions) against everything the engine draws; a COLLAPSED branch reads as uncovered, and `live` roots walk the windows BEHIND the screen too | `ES2Access/Dev/CoverageAudit.cs` |
| `DevProbe.Ghosts()` | Declared vs painted on the FOCUSED screen, split: `droppedByGate` (the gate already withholds these — informational) and `shippedUnpainted` (in the player's render yet unpainted — defects) | `ES2Access/Dev/GhostAudit.cs` |
| `DevProbe.GateDiff()` | The focused screen built gated and ungated in one call — what the gate is dropping; blind to the pre-builder Cells/CardActions path (its doc says why) | `ES2Access/Dev/DevProbe.cs` |
| `NodeGate.Enabled` | The gate's off/on lever — flip via `/eval`, dump, flip, dump, diff: the standard existence-verification primitive, needing no baseline but proving ONLY surfaces actually opened; drops log as `NodeGate drop:` (`/log?grep=NodeGate`), deduped; `NodeGate.Forget()` resets | `ES2Access/UI/NodeGate.cs` |

`POST /input` is `ModInput.Inject` — actions at the production dispatch point; it touches no
physical key state, so game-also-sees-the-key bugs need link-by-link probes (`DevProbe.Claims`
is the layer's end of one).

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
- Descriptor-driven simulation properties may shrug off `SetPropertyBaseValue` + `Refresh`
  (`Fleet.FreeMovementSpeed` stayed 0; `Empire.CanUseStrategicForRecipe` stuck): write with a
  read-back probe first; if it reverts, grant the DESCRIPTOR's source or call it fixture-blocked.
- An order the game posts only from inside a specific turn phase must never be posted from the
  REPL — grant the precondition instead, or spend the turns (what it costs:
  `install.md`, "The Mono runtime under the REPL").


## 2. Verification patterns (screen-agnostic)

**Stage hygiene** (cost scales with tool-call count — ~1.5–2k tokens and ~18 s per call):
fewer, bigger calls. Scope every grep to a named subtree (unscoped greps over
`decompiled/` time out). Grep-before-read for any file > 800 lines; Read only the method
bodies you need via offset. `/gui/age` or `/gui/graph` dump FIRST — it answers layout and
text; decompiled classes only for action paths; re-read the dump already in hand before
probing or walking. Scope `/eval` probes to the one entity in question; bound `/log` with
`since=`; print counts, not enumerations. There is NO Python on this machine — helpers are
`.sh`/`.ps1`/perl script files in the scratchpad; `crop-shot.ps1` via the PowerShell tool.
Build from the repo root only; after every reload confirm `modAssemblyName` incremented
before interpreting live results. Repeated-node `ControlId` keys: index-in-parent, never
widget names. Interim narration one line — findings go in the final report; never re-Read
an image.

**Session loop.** `.\run-game.ps1 -NoSpeech -NoWait -LoadSave "[Beginner] test"` —
cold launch to in-game in one command; `.\wait-game.ps1 <menu|ingame|loading|dialog>` blocks
on a state. Boot ≤ 1 min. Both scripts via the PowerShell tool (Bash-invoked PowerShell hits
execution policy). First act in-game: minimize the tutorial popup (recipe in
`test-recipes/fixtures.md`) — expanded, it eats every injection as `unconsumed`.
A `launcher-x64` orphaned into the *Services* session (session 0) never exits and cannot be killed;
the launch guard skips other sessions, but if a launch still fails,
`tasklist /FI "PID eq <pid>"` tells you which session you are fighting.

**Reload loop.** `dotnet build ES2Access/ES2Access.csproj` → `POST /reload` →
`GET /loader/status` (`staleBuild:false`, `modAssemblyName` incremented). It can answer
BEFORE a queued reload has run (`staleBuild:true`, old name) — poll again, don't rebuild. Reload
before a regression walk after a save load (`GraphState` survives the load) — but NEVER when the
behaviour under test is something a patch captures DURING the load, because the reload installs the
patch after the moment it was watching for and the case reads as unfixed; there, reload FIRST and
leave the load alone. `POST /loadsave` as
soon as a walk's state is suspect; time a transition with a boolean `/wait` predicate, never a
logging probe.

**Evidence crop.** A Class-backed tooltip's review buffer reads EMPTY in `/gui/graph?buffers=1`
unless the node is focused first (its words only exist once the tooltip window draws them — see
"Auditing a tooltip" below). `.\crop-shot.ps1 -Rect x,y,w,h [-Out path]` — never Read a full-frame
screenshot into context. Invoke via the PowerShell tool or
`powershell -Command "& './crop-shot.ps1' -Rect x,y,w,h"`; `powershell -File` mangles the
`-Rect` array argument, and the Bash tool's quoting breaks it too.
**On a pooled table the crop is the oracle, not the dump.** A retired row parked at alpha 0 draws
no text, so `/gui/age` prunes it and the dump agrees with whatever the mod declared — parity that
is really a blind spot. `DevProbe.Ghosts()` names one the mod DECLARED; for anything else, print
`Alpha` beside `Visible` in an `/eval` walk and check it against a `crop-shot.ps1` of the same rect.
A ghost FIX is then proved from both sides without touching game state: set the retired child's
`Alpha` to 1, dump (the real row must declare and announce), set it back to 0 and diff against the
post-fix dump. Where no "before" dump exists, one `/eval` comparing the OLD reader with the NEW one
per widget over the whole window, printing the divergence count, is a regression check that needed
no baseline.

**An un-watched announcement part is still ASKED every frame** (`watch: false` means "not
compared") — an expensive part needs an input-keyed memo, and only a call counter read across
~100 frames proves it; no transcript or dump shows the waste.

**The renderer-field oracle.** When the mod recomputes something the game only DRAWS, drive the
game's renderer from `/eval`, read its private display list by reflection
(`PathRenderer.pathDatasToDisplay` after `ClearPathDataAndRenderPath`), and compare classifications,
not pixels. That proves parity with the DRAWING; a drawn PREDICTION needs the second oracle —
let the game run and watch (the map's turn markers were one low; end-turning caught it).

**Auditing a tooltip.** `DevProbe.TooltipDelay(0)`, focus via `/input`, then all three:
`/screenshot`, `DevProbe.Tooltip()` (class, the reader that answered, the lines, the measured
rows/rects), `/gui/graph?buffers=1`. Caveat: delay 0 changes WHICH frame the request resolves on —
a stalled request is `DevProbe.TooltipPipe()`'s `timer` near 999, invisible to every drawn-window
probe. A feature on `"default"` whose lines divorce a value from its caption is the defect to look
for (but read the DRAWN feature first — the prefab may caption it with sibling labels). `shown:false`
on a focused node whose buffer stays empty despite a declared tooltip = a mis-aimed pointer; confirm
with `DevProbe.Tooltip()` before touching any pointing call. `/gui/graph` alone misleads: it moves
no pointer, so a renderer-drawn tooltip reads empty on a control that is fine live. Re-probing a
still-focused node after mutating its state answers the PRE-mutation content — leave and return.
A LIST ENTRY's tooltip: open the list, step onto the entry, THEN probe. `TooltipDelay(-1)` after.

**The mechanical tooltip check.** `DevProbe.TooltipParity()` on whichever screen is FOCUSED; the
buckets and what each means are in `ES2Access/Dev/TooltipAudit.cs`. Reading a run: the painted half needs
`Screen.RootTransform`, so `"root": null` means declaration-side buckets only, not a clean screen;
COUNTS on a culling surface depend on camera position, so compare buckets, not totals; and a run
taken while a MODAL is focused inherits the screen BEHIND it — subtract findings by root path
before judging, or a clean modal reads as a disaster. A COLLAPSED branch reads as `unread` (the blind
spot `Coverage` shares) — expand and re-run before believing one. `unraised` is the only bucket about
the OTHER promise: a tooltip is DECLARED (`PointsAt`) and RAISED (`OnFocusVisual` moving the
pointer), and one without the other reviews perfectly and never draws — contract at
`GraphNodes.SectionsFor`, which now makes both.

**A card's tooltip is rarely on the card.** Aim at `tooltip.AgeTransform`, never at the row that
contains it, and READ the component's own Tooltip field, never `widget.AgeTooltip` — both fail
silently, and `DevProbe.Tooltip()` is the only probe that catches either (worked example:
`test-recipes/systems-and-planets.md`).

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
`/type` with `ui.activate`; clear with `ui.back` first — and re-read the cursor before
activating: a 0-result search never moved it.

**Tracing a transition frame by frame.** A screen change is frames long and polling from outside
samples between them, so the frame that moved the cursor is invisible. `POST /wait` evaluates its
predicate EVERY frame and does not block `/eval`, so a predicate that LOGS and returns false is a
per-frame recorder: start `POST /wait?timeout=30000` with body
`ES2Access.Dev.DevProbe.Trace("tag")` in the background, drive the transition with `/eval`, then read
`GET /log?since=0&grep=trace` (collapse runs of identical lines — a 30 s trace is ~1800 of them).
Each line is the stack, the focused screen, the cursor, the node count that screen declared, and the
tutorial/window state. A single-digit node count on an active page is a page declaring somebody
else's content. For CUSTOM fields, `var td = new System.Collections.ArrayList();` and wait on
`td.Add(<string>) >= 0 && <defect>` — `Add` returns an index, so one plain expression records every
frame AND stops on the defect; `td` persists for an IIFE to read back deduped. One recording wait,
never two chained around a window — the round trip between them loses frames. That same per-frame
evaluation makes a PLAIN boolean predicate an existence test over a whole transition:
`satisfied:false` after N frames proves no frame had the property — but only once a weaker predicate
that DOES fire, or a recorded transition, proves the window was really sampled.

**Injecting a sequence of keys.** `POST /input` one action key per request, ~0.4 s apart —
a no-delay loop does not fail loudly, it reports a plausible WRONG route (rows appearing
unreachable by Down) — then
read `/speech?since=N` — `next` from a `since=0` read before the sequence is the baseline. Keep
the route in a `.sh` script in the scratchpad so the same walk is replayable. A turn-advance
helper must dismiss the game's own end-turn blockers (empty-queue prompts eat the first press);
"state didn't change" is not "the injection failed".

**Holding a PHYSICAL modifier while a key is pressed** — the only way to reach a modified click's
game branch (Ctrl+click to locate, Alt+click to queue at the head). From a PowerShell script:
bring the game up with `SwitchToThisWindow` plus `AttachThreadInput` + `SetFocus`, then drive the
keys with `keybd_event`. `SetForegroundWindow` ALONE fails silently — the window comes up but Unity
still reads the key as released, so the chord runs unmodified and looks like a wiring bug. Re-focus
before EVERY run, not once per session. And where the surface under test is a game screen shown
UNDER a modal, it never reaches the mod's own stack: probe `Gui.GuiService.GetWindow<T>().Shown`,
not `DevProbe.Stack()`, or a screen that is working reads as absent.

**World position → screen pixel** (checking a spoken direction against the picture; world axes are
in `galaxy-map.md`):
`((GalaxyViewCameraController)Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICameraService>().CameraController).Camera.WorldToScreenPoint((Vector3)node.GalaxyPosition)`.
The galaxy camera hangs off the controller's `Camera` property — `Camera.main` is null in this game
and the controller's own GameObject carries no `Camera` component, so both of those routes answer
nothing. Screen y is Unity's (bottom-origin); `crop-shot.ps1` takes TOP-origin pixels.

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

**Proving a refactor changed no spoken or buffer line.** The scripted walk lives in `walks/`
(`walk-all.sh <dir>` twice — before and after — then `diffwalks.sh`; `walks/README.md` is the
manual, fixture-agnostic by runtime discovery). Hand-rolling the same idea: walk every reachable
screen family with `POST /input`, save `GET /gui/graph?buffers=1` per family to a scratchpad
`before/`, make the change, walk the IDENTICAL route into `after/`, `diff` (normalise
instance-hash ids such as `droplist:-138580/…`). It works because the dump is text and stable, and unfocused Class-backed
tooltips read EMPTY on both sides, so they cancel — and are therefore UNPROVEN by the diff: a
change touching them needs a FOCUSED second pass over the nodes that carry them. A route is only
identical from a normalised cursor AND camera — drive to an edge and re-seat the camera/zoom in the
route's own prologue before the first dump; a screen that remembers its cursor, or a camera-driven
page, makes the two walks start elsewhere. A "before" needed afterwards: `git stash push
-u -- ES2Access ES2Access.Tests` → build → `/reload` → capture → `git stash pop` → build →
`/reload` (~3 min). A **sheet** baseline must come from ONE session — `GraphSheet` row keys
derive from `GetHashCode()`, which survives a hot reload but not a restart — so the stash loop,
never two launches; the loop is UNSAFE while another stage edits the same trees. For a purely
ADDITIVE announcement change, null the injected dependency that produces the new part
(`GraphAnnouncer.Carry = null`) and dump instead of stashing.
`GET /gui/graph?screen=KEY&buffers=1` reaches screens whose window exists without a game
(`screen.game-menu`, `screen.rename`); the dump is GATED, so an inactive screen's own content is
withheld, leaving the shared HUD stops — or, for a window the game is not drawing at all,
`declared no controls`, which `ungated=1` does NOT lift (measured on `screen.main-menu`). Open it first.

**Sighting a surface the fixture never draws** — the tiered forced-show ladder — is
`docs/test-recipes/fixtures.md`.

**Proving a watcher stays silent** is a long poll on the watched flag, not a scan of `/speech`:
`POST /wait` on the game's own condition, then read `/speech?since=N` for the window that
elapsed. Because the wait caps at ~60 s, a claim of "silent for minutes" is several polls.

**Splitting one buffer section into two loses `AddLine`'s cross-list dedupe** — nothing
de-duplicates ACROSS sections; after moving a tooltip between sections, re-read the node's
buffer FOCUSED (the repeat is invisible in the unfocused dump).

**Opening a game modal from `/eval`**: set what its opener sets, then show it; close it with
`Gui.GuiService.HideWindow(w)` or the mod's own keys — NEVER `w.HandleInput(InputAction.Exit)`, which
wedged the screen stack (`test-recipes/fixtures.md`, "Resetting game state"). Worked routes
per window are in `docs/test-recipes/modals-and-outgame.md` ("Opening game modals from /eval").

**State restoration etiquette.** Leave the fixture as found: tutorial popup MINIMIZED, no
notifications pending, camera at home (`DevProbe.Camera()` before and after), no text field
holding game focus (`AgeManager.Instance.FocusedControl = null`), `DevProbe.TooltipDelay(-1)`
(a set delay survives reloads on purpose — and so does the restore cache being LOST by a
reload, which makes one `-1` put back whatever was set at the time of the last reload; check
`now` against `registry` in the reply and call it twice if they differ).

## 3. Keeping this file honest

Only a change to the LOOP itself — a route, a REPL gotcha, a screen-agnostic verification
pattern — lands here, and the file stays under ~350 lines: over that, a stage moves something
out before adding. Every other output has a chartered home; the charters are in `CLAUDE.md`.
When content moves or a design is reversed, grep the whole docs tree for the old name and for
inbound references before calling the change done.

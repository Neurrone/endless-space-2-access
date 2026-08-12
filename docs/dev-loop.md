# ES2 dev loop — build, reload, verify

Fixtures: **`[Beginner] test`** (turn 4 — Dusay a colony, Rigel an outpost; `DevProbe.Saves()`
reports titles) and **`[Midgame] quests fleets`** (turn 3 — the one with a quest pinned).
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

- `GET /status` — mod state, `modAssemblyName`, the `keyStandDown` patch tripwire (FOUR
  prefixes now: the three key scans plus `AgeControlTextField.KeyDown`)
- `GET /speech?since=N&wait=MS` — spoken ring buffer (resets on reload); `wait` long-polls
- `GET /gui/graph?edges=1&buffers=1` — the focused screen's whole accessible tree
- `GET /gui/graph?screen=KEY` — what an UNFOCUSED registered screen would offer, built without
  focusing it; an inactive one answers `screen inactive: …`, a bogus key 400s with the key list
- `POST /input` — body = one action key (`ui.down`, `buffer.lineDown`…); its key-claim counterpart is
  `/eval ES2Access.Dev.DevProbe.Claims("Escape")` — the latch only lives for the frame an injection
  is consumed (no key was held), so catch it with `POST /wait` on the probe's own text, never a
  second request
- `POST /type` — body = characters to TYPE at the focused screen (the type-ahead search), through the
  same gates a keypress passes; answers `taken`/`searching`/`search`/`results`/`focus` plus the speech
  it caused. `/input` cannot carry it: that queue is actions, and typing is text. Neither reaches a
  field the GAME owns — the letters queue against the mod's own type-ahead and fire as a search the
  moment the field lets go, so a game-owned edit is driven by writing its text from `/eval`
- `GET /gui/game?path=&depth=` — Unity hierarchy; `GET /gui/age?window=&depth=&visibleOnly=` —
  AGE widgets with rects (`window=` is the filter; `/gui/game` is the one taking `path=`)
- `window=` matches a registered window, a shown panel, then any named AgeTransform under them,
  and `depth=`/`visibleOnly=`/`fields=` apply from there; an empty answer always carries an
  `error`/`note` line, and a node cut off by `depth=` is kept (`more:true`), never pruned
- `GET /gui/age?...&fields=name,kind,text,tooltip,rect,interactable,enabled` — flat text, one
  indented line per widget, only those fields, empties omitted
- `POST /eval?settle=MS&speech=0` — C# REPL (gotchas below); response carries caused speech
- `POST /wait?timeout=MS` — body = bool expression, evaluated every frame; the wait is capped at
  ~60 s whatever is asked for, so a longer silence is proved by repeating the poll
- `POST /loadsave` — body = save title (empty = newest); retryable `[not ready]` until it acts —
  except from a LOBBY, where not-ready is the answer until the lobby is left, never a retry
- `GET /log?since=N&grep=TEXT` — no `since` answers only the last 100 entries (`capped:true`);
  `grep` still searches the whole ring; `GET /screenshot`; `POST /quit` — shutdown takes
  20–60 s: poll the PROCESS (not the port) every 2 s and only conclude a hang past 60 s
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

- One statement per request. No `using` directives — fully qualify everything.
- Never declare a local whose type is a constructed generic over a game type; **a `foreach`
  over `AgeTransform.Children`, `GetPlayerEmpireGuiNotifications()` or any `List<GameType>`
  declares one implicitly**, and it poisons the WHOLE session — every later request answers
  with a `MakeGenericType` InternalErrorException. Iterate by index or bind as
  `System.Collections.IList`. Recover with `POST /reload`.
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
on a state. Boot ≤ 1 min.

**Reload loop.** `dotnet build ES2Access/ES2Access.csproj` → `POST /reload` →
`GET /loader/status` (`staleBuild:false`, `modAssemblyName` incremented).

**Evidence crop.** A Class-backed tooltip's review buffer reads EMPTY in `/gui/graph?buffers=1`
unless the node is focused first (its words only exist once the tooltip window draws them — see
"Auditing a tooltip" below). `.\crop-shot.ps1 -Rect x,y,w,h [-Out path]` — never Read a full-frame
screenshot into context. Invoke via the PowerShell tool or
`powershell -Command "& './crop-shot.ps1' -Rect x,y,w,h"`; `powershell -File` mangles the
`-Rect` array argument, and the Bash tool's quoting breaks it too.

**Auditing a tooltip.** `DevProbe.TooltipDelay(0)`, focus via `/input`, then all three:
`/screenshot`, `DevProbe.Tooltip()` (a `features` array — class name, the reader that answered,
the lines it produced — plus the measured rows/rects/assets), `/gui/graph?buffers=1`. A feature
class sitting on `"default"` whose lines divorce a value from its caption is the defect to look
for; nothing about it shows in the spoken lines alone. `shown:false` on a control whose readout
says "has tooltip" is the OTHER signature — the pointer was aimed with the 2-arg
`AgeWidgets.Point`, which re-derives the tooltip from the control's own transform instead of using
the one the screen resolved.
`/gui/graph` alone misleads here: it moves no pointer, so a renderer-drawn tooltip is
undrawn and its buffer reads empty on a control that is fine live. `TooltipDelay(-1)` after.


**A card's tooltip is rarely on the card.** `PointerFocus` shows the tooltip of the widget it is
pointed AT, so pointing at a row whose tooltip hangs off a child inside it (the planet card's
anomaly rows) draws nothing while the readout still says "has tooltip". Point at
`tooltip.AgeTransform`, not at the row — and prove it with `DevProbe.Tooltip()`, which is the
only thing that catches it.

**A tooltip family's evidence pair.** Focus the control, `DevProbe.Tooltip()` for the typed
reading, then `Gui.GuiService.GetWindow<GuiTooltipWindow>(false).AgeTransform.GetGlobalPosition()`
for the rect and `crop-shot.ps1` on it — the tooltip is anchored to the pointer, so its rect
moves between runs and a crop from an earlier probe lands on empty sky.

**Reading a tooltip the game only writes for somebody else.** A content-backed tooltip the fixture
leaves empty (the orbital card's `OutpostTooltip`, written only for a FOREIGN outpost) is still
provable: set `.Content` from `/eval` to what the game would write, focus the node, and read
`/gui/graph?buffers=1`. The card's next refresh blanks it again, so nothing is left behind.

**Testing a type-ahead search.** `POST /type` with the letters (`res`), read the `speech` array it
answers with, then drive the results through `/input ui.down|ui.up|ui.home|ui.end` and end with
`/input ui.back` ("Search cleared"). The key-claim half is `DevProbe.Claims("Escape,R,Space")`: with
a search up, all three read `claims:true` and `claimsBack:true`; after Escape clears it, Escape goes
back to the game (`claims:false`) while the letters stay claimed, because type-ahead is armed
whenever a mod screen is focused. Each keystroke re-announces the landing, so `/type "res"` answers
with three identical lines — that is the design, not a stutter.

**Injecting a sequence of keys.** `POST /input` one action key per request, ~0.4 s apart, then
read `/speech?since=N` — `next` from a `since=0` read before the sequence is the baseline. The
Bash tool mangles `python -c` here (it injects `|| goto :error`); keep the JSON formatting in a
`.py` file in the scratchpad.

**Silence in `/speech` is only evidence for controls that would have spoken.** An enabled
button's activation is also silent, so a transcript cannot distinguish "refused" from
"acted" for buttons — prove a button refusal with a state probe (queue count, graph dump),
never by absence of speech. Checkbox/slider/combo refusals are provable by silence.

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
`GET /gui/graph?screen=KEY&buffers=1` reaches screens whose
window exists without a game running — out of a session `screen.game-menu` and
`screen.rename` both declare real content, `screen.galaxy` and friends answer "not active".

**Sighting a surface the fixture never draws.** Three tiers, cheapest first: `Show()` the
game's own pooled widget, read, `Hide()` — the game's next visibility pass restores truth by
itself; or set the game's OWN `Visible` flags and private fields from `/eval`, dump, restore,
and re-diff the dump against the untouched one to prove nothing was left behind (this is how a
whole DLC feature branch gets sighted); or, for a window with data, `Bind` + `Show`, read, then
`Unbind` + hide. A forced-show proves STRUCTURE, never content, and a half-bind can outlive the
probe — restore a monotonic setter through its backing field or private setter, and re-issue
`POST /loadsave` if a window wedges. Never force-show a DLC modal without its data.

**Proving a watcher stays silent** is a long poll on the watched flag, not a scan of `/speech`:
`POST /wait` on the game's own condition, then read `/speech?since=N` for the window that
elapsed. Because the wait caps at ~60 s, a claim of "silent for minutes" is several polls.

**Splitting one buffer section into two loses `AddLine`'s cross-list dedupe** — nothing
de-duplicates ACROSS sections. After moving a tooltip out of a details function, re-read the
node's buffer FOCUSED: a drawn tooltip repeating a computed line is invisible in the unfocused
dump.

**Multi-row tables** need a real fixture with several saves/rows — do not mutate the game's
data structures to fake one.

**Opening a game modal from `/eval`** (to measure it without walking there): set what its
opener sets, then show it — for the improvements list,
`var w = Gui.GuiService.GetWindow<ImprovementsManagementModalWindow>(); w.ColonizedStarSystem =
...ColonizedStarSystems[0]; Gui.GuiService.ShowWindow(w);`. Close it the way Escape does:
`w.HandleInput(InputAction.Exit)` (`InputAction` is Assembly-CSharp's, NOT
`Amplitude.Unity.Input`'s). Escape itself cannot be injected — `POST /input ui.back` only
proves the mod does not consume it.
A modal whose opener installs DELEGATES has to be opened through the opener's own handler —
reflection for a private `Cb(GameObject obj = null)`, since `SendMessage` with no argument
logs an arity error and does nothing (es2-facts). The worked route for
`SystemSelectionModalWindow`, with its never-press warnings, is in `docs/test-recipes.md`.

**Icon-table coverage proof.** Run every `<LocalizationPair>` value in
`<game>\Public\Localization\english\*.xml` through `ES2Access.UI.AgeText.Clean`, then
`DevProbe.UnknownIcons()` — `tokens` must be empty; the expected token counts are es2-facts'
icon numbers.

**Launcher stuck in session 0.** A `launcher-x64` orphaned into the *Services* session
never exits and cannot be killed; the launch guard skips other sessions, but if a launch
still fails, `tasklist /FI "PID eq <pid>"` tells you which session you are fighting.

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

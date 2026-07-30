# ES2 dev loop — the working map

**Current state (2026-07-30):** phase 1 (out-game) done; in-game esc menu, galaxy HUD, and
the popup family done and past manual screen-reader review. Screen-by-screen status:
`docs/roadmap.md`. This file is the toolbox index: what exists in THIS repo and the exact
commands. Patterns and doctrine live in `docs/generic/` — read the relevant chapters BEFORE
touching source, and report what they lacked (CLAUDE.md requires both):

| Task | Read first |
|---|---|
| Any screen work, start to finish (the process) | `docs/generic/making-screens-accessible.md` |
| Modeling/navigating mechanics (graph, stops, rows, regions, focus) | `docs/generic/ui-navigation.md` |
| Anything keys: bindings, repeat, stand-down, game collisions | `docs/generic/input.md` |
| Widget kinds, roles, announcements, activation idioms | `docs/generic/widgets.md` |
| Review buffers / re-readable content | `docs/generic/buffers.md` |
| Tooltips (short/long rule, drawn readback, visual parity) | `docs/generic/tooltips.md` |
| Inline icons / symbols in text | `docs/generic/icons-and-symbols.md` |
| Dev server, REPL, test loops | `docs/generic/dev-server.md`, then §3–4 here |
| Speech pipeline / interruption | `docs/generic/speech.md` |
| Localization / ModStrings / exact game text | `docs/generic/localization.md` |
| Decompiled-code research | `docs/generic/reverse-engineering.md` |
| Hot reload / loader boundaries | `docs/generic/hot-reload.md` |
| Per-frame cost, GC hitches, scan/allocation discipline | `docs/generic/performance.md` |
| New-game bring-up on another title | `docs/generic/new-game-playbook.md`, `project-bootstrap.md` |

Keep this file a map, not a manual — if an entry needs more than two lines, the detail
belongs in the generic docs or the source file's own doc comment.

## 1. Helper inventory

| Helper | One line | File |
|---|---|---|
| `AgeLayout` | Drawn-layout reading: row banding, reading order, alignment tiebreaks | `ES2Access/UI/AgeLayout.cs` |
| `DrawnTooltip` | Reads the rendered tooltip window's rows (labels + named icons) | `ES2Access/UI/DrawnTooltip.cs` |
| `PointerFocus` | Hover/tooltip/flyout parity for keyboard focus; `MoveTo` (button or plain transform), `Unpoint` | `ES2Access/UI/PointerFocus.cs` |
| `GameKeyStandDown` | The input-suppression patches (mod keys win; Escape carved out); watch its counts on `/status` | `ES2Access/UI/Input/GameKeyStandDown.cs` |
| `GraphNodes.ModeFor` | The tooltip short/long rule — never pick a `TooltipMode` by hand | `ES2Access/UI/GraphNodes.cs` |
| `IconNames` + `IconTable` | Icon → `icon.*` key → name; the enumerated 382-token/407-texture table with variant aliases | `ES2Access/UI/IconNames.cs`, `ES2Access/Core/Speech/IconTable.cs` |
| `DevProbe` | Compile-checked one-liners: `Screen() Stack() State() Saves() Camera() Windows() Patches() TooltipDelay(s) Tooltip() UnknownIcons()` | `ES2Access/Dev/DevProbe.cs` |
| `/input` queue | `ModInput.Inject` — actions at the production dispatch point | `ES2Access/UI/Input/ModInput.cs` |

## 2. Layer budget

Static per screen (doctrine: ui-navigation.md "Layers are static"):
`0` main-menu · `10` galaxy · `30` tutorial · `40` notification · `50` game-menu ·
`52` options (one number, above the pause menu that can open it) · `55` load-save ·
`60` loading · `70` drop-list (above options, its owner) · `100` message-box.

ES2 facts with no other home:

- **A collapsed tutorial is a galaxy stop, not a tutorial screen.** The game crops the popup
  to its title bar and hides nothing, so `MinimizeToggle.State` is the only signal;
  `TutorialScreen` stands down while it is set and `BuildCollapsedBar` declares the leftover
  bar in the galaxy's `galaxy:tutorial` stop.
- ES2's icon numbers, for re-verification: 382 registered tokens (single writer
  `AgeManager.CreateSpecialCharactersDictionary` → `AgePrimitiveLabel.SpecialCharacters`,
  keys `"[TOKEN]"` upper-cased), 371 named + 11 nameless colour directives; localization
  corpus 25 821 strings, 1 861 with brackets.

## 3. Dev server — quick reference

Gates: off by default — `devServer = true` under `[Dev]` in
`BepInEx\config\endless.space2.access.cfg` (`run-game.ps1` writes it; `-NoDev` for off).
`ES2ACCESS_NO_DEV=1` forces off; `ES2ACCESS_DEV_PORT` overrides; `ES2ACCESS_NO_SPEECH=1`
mutes voicing but `/speech` still captures.

- `GET /status` — mod state, `modAssemblyName`, the `keyStandDown` patch tripwire
- `GET /speech?since=N&wait=MS` — spoken ring buffer (resets on reload); `wait` long-polls
- `GET /gui/graph?edges=1&buffers=1` — the focused screen's whole accessible tree
- `POST /input` — body = one action key (`ui.down`, `buffer.lineDown`…)
- `GET /gui/game?path=&depth=` — Unity hierarchy; `GET /gui/age` — AGE widgets with rects
- `POST /eval?settle=MS&speech=0` — C# REPL (gotchas below); response carries caused speech
- `POST /wait?timeout=MS` — body = bool expression, evaluated every frame
- `POST /loadsave` — body = save title (empty = newest); retryable `[not ready]` until it acts
- `GET /log?since=N&grep=TEXT`; `GET /screenshot`; `POST /quit` (~10 s, poll at 1 s)
- `POST /reload` (needs `Content-Length` — `--data-raw ""`); `GET /loader/status` —
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

## 4. Recipes (ES2-concrete; rationale in dev-server.md / making-screens-accessible.md)

**Stage hygiene** (cost scales with tool-call count — ~1.5–2k tokens and ~18 s per call):
fewer, bigger calls. Read each source file once (offsets for revisits); `DevProbe`/`
/gui/graph` over `/eval` chains; batch repetitive checks into one script that prints a
table; interim narration one line — findings go in the final report; never re-Read an image.

**Session loop.** `.\run-game.ps1 -NoSpeech -NoWait -LoadSave "[Beginner] access test"` —
cold launch to in-game in one command; `.\wait-game.ps1 <menu|ingame|loading|dialog>` blocks
on a state. Boot ≤ 1 min.

**Reload loop.** `dotnet build ES2Access/ES2Access.csproj` → `POST /reload` →
`GET /loader/status` (`staleBuild:false`, `modAssemblyName` incremented).

**Evidence crop.** `.\crop-shot.ps1 -Rect x,y,w,h [-Out path]` — never Read a full-frame
screenshot into context.

**Auditing a tooltip.** `DevProbe.TooltipDelay(0)`, focus via `/input`, then all three:
`/screenshot`, `DevProbe.Tooltip()` (measured rows/rects/assets), `/gui/graph?buffers=1`.
`/gui/graph` alone misleads here: it moves no pointer, so a renderer-drawn tooltip is
undrawn and its buffer reads empty on a control that is fine live. `TooltipDelay(-1)` after.

**Raising a notification on demand** (the fixture has none pending):
`Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.Event.IEventService>().Notify(new EventEmpireIntroduction(Gui.PlayerEmpire))`
— dismiss afterwards (`Gui.GuiNotificationService.DismissGuiNotification(...)`); minimizing
leaves it in the icon strip, which is a fixture change.

**Icon-table coverage proof.** Run every `<LocalizationPair>` value in
`<game>\Public\Localization\english\*.xml` through `ES2Access.UI.AgeText.Clean`, then
`DevProbe.UnknownIcons()` — `tokens` must be empty; token-by-token expect 371 named / 11
nameless.

**Multi-row tables** need a real fixture with several saves/rows — do not mutate the game's
data structures to fake one.

**Launcher stuck in session 0.** A `launcher-x64` orphaned into the *Services* session
never exits and cannot be killed; the launch guard skips other sessions, but if a launch
still fails, `tasklist /FI "PID eq <pid>"` tells you which session you are fighting.

**State restoration etiquette.** Leave the fixture as found: tutorial popup open on page 1
and not collapsed, no notifications pending, camera at home (`DevProbe.Camera()` before and
after), no text field holding game focus (`AgeManager.Instance.FocusedControl = null`),
`DevProbe.TooltipDelay(-1)` (a set delay survives reloads on purpose).

## 5. Keeping this file honest

Implementation stages update this digest as part of being done — a new helper, route, or
recipe lands here in the same change, and the dated header line moves. Screen status changes
land in `docs/roadmap.md`; game-agnostic lessons land in `docs/generic/`, not here. When a
design is reversed or content moves between docs, grep the whole docs tree for the old
mechanism's name and for inbound references before calling the change done — stale rows
state reverted designs as current.

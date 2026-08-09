# ES2 dev loop — the working map

**Current state (2026-08-09):** the fixture save is **`Beginner test`** (recreated on this
VM — the old `[Beginner] access test` is gone with the previous machine). phase 1 (out-game) is screen-complete: the new game lobby, the
advanced settings, the faction chooser and the custom faction editor over it, and the tutorial
picker all have screens; the lobby family has been through one manual review and its fixes. In-game esc menu, galaxy HUD, and
the popup family done and past manual screen-reader review; the galaxy map's system tree
(planets + starlanes, camera follow), the star system management page (7 stops, action
menus, rename box), the improvements modal opened from it and the planet overview page
landed and await manual review. The system-discovery cutscene has a passive announcer that
the fixture cannot reach at all — its whole run is on the human test script. In the galaxy
tree, expanding a system changes NO camera distance: the zoom is asked for from the system's
action menu, and whichever distance the player chose is what its planet children read (thin
circle readouts when out, drawn orbital cards when in). The panel those cards open
(`PlanetConstructiblePanel`) has a screen the fixture cannot reach.
Screen-by-screen status:
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
| `DrawnTooltip` | The rendered tooltip window, read one PANEL FEATURE at a time; `Features()` is the same reading with each feature's class and the reader that answered | `ES2Access/UI/DrawnTooltip.cs` |
| `TooltipFeatures.Read` | One feature into lines: scoped row banding, repeated items, the ship stat block, separators skipped by the game's own flags, unknowns to the fallback | `ES2Access/UI/TooltipFeatures.cs` |
| `TooltipText` | What a row of tooltip parts SAYS — icon-as-heading vs decoration, caption+value, item strips | `ES2Access/Core/Speech/TooltipText.cs` |
| `PointerFocus` | Hover/tooltip/flyout parity for keyboard focus; `MoveTo` (button or plain transform), `MoveToToggle` (a toggle has no `SimulateHover` — its own `MouseEnter`/`MouseLeave`), `Unpoint` | `ES2Access/UI/PointerFocus.cs` |
| `GameKeyStandDown` | The input-suppression patches (mod keys win; Escape carved out); watch its counts on `/status` | `ES2Access/UI/Input/GameKeyStandDown.cs` |
| `NodeVtable.Sections` | **A control's content, declared ONCE** — an ordered list of `NodeSection` (lines + a `TooltipMode`). The engine derives BOTH surfaces from it: `TooltipParts.Part` the spoken tooltip part, `NodeBuffer.Lines` the review buffer. There is no `DetailLines` any more, and no screen wires an announcement | `ES2Access/Core/UI/Graph/GraphTypes.cs` |
| `TooltipParts.Part(sections)` | The spoken half: the LAST `Announce` section's words, plus "has tooltip" if ANY section is `Indicate`; `None` sections say nothing | `ES2Access/Core/UI/Graph/TooltipParts.cs` |
| `NodeBuffer.Lines` | The buffer half: an AUTO HEAD off the node's own readout (label + state words, no role/position/tooltip), then every section in declared order, first line dropped if it only repeats the label. A node with NO sections still buffers correctly | `ES2Access/Core/UI/Graph/NodeBuffer.cs` |
| `GraphNodes.ModeFor` | The tooltip short/long rule — never pick a `TooltipMode` by hand | `ES2Access/UI/GraphNodes.cs` |
| `GraphNodes.TooltipSection` / `Sections` | A widget's tooltip as a section (mode from `ModeFor` unless overridden), and the null-dropping list builder every factory ends with | `ES2Access/UI/GraphNodes.cs` |
| `SettingRows.RowSections` | Two shapes: `(caption, value, drawn?)` for a caption-then-value row (both speak by rule, the value's wins), and `(widget, said, mode?)` for a row whose tooltips are scattered over its children — only `said` speaks, the rest are reviewable; `said` null = none speaks | `ES2Access/Screens/SettingRows.cs` |
| `AgeWidgets.Readable` | The ONE "are this tooltip's words on the widget" test (empty class or `Simple`). `ModeFor`, `TooltipLines` and every screen ask it; three private copies used to disagree about `Simple` | `ES2Access/UI/AgeWidgets.cs` |
| `GraphNodes.*` factories | **Every node is built by one.** Each ends `(tooltip, tooltipMode?, details?)` and builds `Sections` from them — `tooltipMode` null means "ask `ModeFor`", so a screen passes the tooltip and nothing else. A screen may still SET `vtable.Sections` (that IS the declaration); it can no longer wire an announcement, which is how a row used to end up with a tooltip in one surface and not the other | `ES2Access/UI/GraphNodes.cs` |
| `IconNames` + `IconTable` | Icon → `icon.*` key → name; the enumerated 382-token/407-texture table with variant aliases | `ES2Access/UI/IconNames.cs`, `ES2Access/Core/Speech/IconTable.cs` |
| `GalaxyViewLevels` | Which `GalaxyViewLevel` is up (`At<T>()`, `Overview`, `Scanning`, `LevelThroughTransitions`, `FocusedSystem`) + where the camera is (`ZoomStep`, `AtOrbitalZoom`, `DefaultZoomStep`, all -1/false when the galaxy camera is not the live one) and the routes (`PanTo`, `ZoomTo` — the game's own `ZoomInOnNode`, `ZoomToStep` for coming back out, `OpenSystem`, `OpenPlanet`); stateless, so reload-safe | `ES2Access/UI/GalaxyViewLevels.cs` |
| `AgeWidgets` | The per-widget questions every screen asks: `Visible`/`Enabled`/`Operable` (ancestor-walking), `Raw`/`Readable`/`TooltipLines`/`TooltipTitle` (the `GuiWrapper` name behind a wordless icon), `Press`/`Toggle`/`Choose` (replay the widget's own handler; `Choose` takes a drop-list entry through the list's own `OnSelectionObject`/`Method`), `Point`/`PointAt`, `TextOf` (a group's whole drawn phrase, icons named) | `ES2Access/UI/AgeWidgets.cs` |
| `FieldReadout.Compose` | A panel's fields as one spoken line, blanks dropped; null when there was nothing to say, which is a passive announcer's "not filled in yet" | `ES2Access/Core/Speech/FieldReadout.cs` |
| `RefusalText.Compose` | A blocked button's tooltip trimmed to the refusal itself — leading description off, the game's mouse instruction dropped | `ES2Access/Core/Speech/RefusalText.cs` |
| `GlobalHud` | The four clusters drawn over every view level (empire banners, collapsed tutorial bar, notifications, turn controls) + the turn watcher; every page under them calls `Empire`/`Tutorial`/`Notifications`/`Turn` in drawn order | `ES2Access/Screens/GlobalHud.cs` |
| `Screen.PushChild` / `RemoveChild` | Mod-owned sub-screens: one linear chain, deepest is focused, a covered parent keeps its cursor | `ES2Access/Screens/Screen.cs` |
| `ChoiceSubmenuScreen.Open` | The action menu — snapshot of what is possible now, "menu item" role, empty list answered once here | `ES2Access/Screens/ChoiceSubmenuScreen.cs` |
| `DropListScreen.Open(list, title, choose)` | Any `AgeControlDropList` as a sub-screen; entries fall back to their tooltips when the list is drawn as icons, then to `EmpireColors` when it is drawn as bare swatches; the focused entry is POINTED at, so the game draws its tooltip | `ES2Access/Screens/DropListScreen.cs` |
| `EmpireColors.Name(color)` | What the player's chosen palette (`Public/Mapping/Palettes.xml`) calls a drawn colour — matched by colour, not by list position; `ModStrings` `color.*` keys, falling back to the game's identifier split at its capitals | `ES2Access/UI/EmpireColors.cs` |
| `SettingRows` + `TextFieldEditor` | One game `SettingItem` as a row (every `Gui.ControlType`, the slider's index-stepping write path, `Drawn` = visible AND alpha > 0), plus the shared row shapes every lobby-family screen builds through — `AddCombo`, `AddButton`/`AddButtonRow`, `AddTextField`, `AddReadout` — and the deferred keyboard hand-over to a text editor | `ES2Access/Screens/SettingRows.cs` |
| `DevProbe` | Compile-checked one-liners: `Screen() Stack() State() Saves() Camera() Windows() Patches() Claims(keys?) TooltipDelay(s) Tooltip() UnknownIcons()` | `ES2Access/Dev/DevProbe.cs` |
| `DevProbe.Claims("Escape,Return")` | What the input layer is claiming FROM the game: the consumed-key latch (key + still held), `backClaimed`/`claimsBack`, `layerLive` split into `screenFocused` and `keyboardElsewhere`, and `ClaimsKey`'s side-effect-free answer per named key | `ES2Access/Dev/DevProbe.cs` |
| `/input` queue | `ModInput.Inject` — actions at the production dispatch point; touches no physical key state, so game-also-sees-the-key bugs need separate link-by-link probes (`DevProbe.Claims` is the layer's end of one) | `ES2Access/UI/Input/ModInput.cs` |

## 2. Layer budget

Static per screen (doctrine: ui-navigation.md "Layers are static"):
`0` main-menu and the new-game lobby (never up together — showing one hides the other) ·
`5` advanced settings · `6` faction chooser · `7` custom faction editor (all over the lobby, their
only opener, and never up together; well under the drop list a setting can open and the message
box a Cancel or a Delete confirms in) ·
`10` galaxy, star-system, planet-overview and system-discovery (the four
view levels, never up together) · `20` planet-constructibles (the panel a planet card slides
out under itself) · `30` tutorial ·
`40` notification · `50` game-menu · `52` options (one number, above the pause menu that can
open it) · `55` load-save · `60` loading · `70` drop-list (above options, its owner) ·
`80` rename box · `85` improvements modal (over the star-system page, under its own
confirmation) · `90` tutorial-selection modal (over the new game screen) · `100` message-box. Action menus are CHILD screens and have no layer: the
manager focuses the deepest child of the top screen.

**ES2 key map, in one place** (defaults in `ModEntry.BindKeys`; the generic table is
`docs/generic/input.md`). On top of arrows/Tab/Enter/Backspace/Escape/Home/End, Alt+arrows and
the Ctrl review chords: **Shift+Left/Right** coarse slider step, **Shift+Up/Down** move the
focused ITEM (queue reorder), **Alt+Enter** the control's other activation (queue at the head).

**Escape is the game's, except over a surface the mod invented.** `ModInput.ClaimsKey` used to
exempt Escape unconditionally, so an action menu's `Back()` closed the menu AND the same key
reached the game's `InputsMatch` and raised the pause screen. A screen now answers
`ConsumesBack` (a question asked BEFORE the press — the game's scan can run either side of the
mod's frame, and by the time `Back()` has run the menu is gone), plus a latch in `ModInput` for
the other ordering — generalized since to EVERY key the mod acts on, held until the player lets
go. `ConsumesBack` is NOT a copy of `Back()`: `DropListScreen` handles Escape
and still needs the engine to see it. Probe it live with
`ES2Access.Dev.DevProbe.Claims("Escape")` — `claims` true only where a mod-owned
surface is focused, and the latch says so when the surface has already gone. That probe, not
`/input ui.back`, is what proves the key does not fall through.

ES2 facts with no other home:

- **A collapsed tutorial is a HUD stop, not a tutorial screen.** The game crops the popup
  to its title bar and hides nothing, so `MinimizeToggle.State` is the only signal;
  `TutorialScreen` stands down while it is set and `BuildCollapsedBar` declares the leftover
  bar in `GlobalHud`'s `hud:tutorial` stop, on whichever view level is underneath. The game
  does NOT draw the bar on the planet overview, so that page has no such stop — measured, not
  assumed.
- **A window's own `HandleInput` override can turn its Cancel button into a Confirm.**
  `GuiModalWindow.OnCancelCb` is nothing but `HandleInput(InputAction.Exit)`, so any window that
  overrides Exit to mean something other than "dismiss" silently changes what its Cancel button
  does — and the game's tooltip on that button goes on saying the old thing. Read the override
  before trusting either the key or the button.
- **`Visible` is not "drawn".** `AgeTransform.RefreshChildrenIList/Array` leaves the surplus
  children of a pooled table (a competitor slot an empire count no longer needs) flagged
  `Visible` with `Alpha == 0`. Ask about alpha too — but only `== 0`, since a read-only setting
  is faded to 0.5 and is still drawn.
- **The click sound is not in the handler and not in the control.** It is an `AgeAudio` component
  on the same transform, posting `MouseUpEventID` through the gui audio proxy (`AgeAudio.MouseUp`
  :191-197) when the engine's mouse dispatch tells it about the press. Replaying a wired handler
  reaches neither, so every control the mod worked was silent; `AgeWidgets.Click` posts the
  component's own down/up before dispatching. Measured: main-menu buttons and the faction
  chooser's hull arrows all carry a non-zero `MouseUpEventID` with a live `GuiAudioProxy`.
- **A window's `AgeTransform.Enable` lags its `Shown`/`IsReady` by a frame or two**, both when a
  modal opens (measured: `shown=True ready=False windowEnable=False`, both true two frames later)
  and when one closes over a page underneath. The same lag exists one level down for a PANEL that
  is swapped inside a window it never closes (the faction chooser under the custom faction
  editor): gate on the panel's `Operable` too, or its controls read "unavailable" on arrival. Either way a screen gated only on shown/ready
  declares its controls while the window root is still disabled, and `Operable`'s ancestor walk
  then reads EVERY control as "unavailable" — including the one the arrival announcement lands on.
  Gate arrival on `Operable` as well.
- **A `GuiTable` line is a POOL SLOT, not a row.** `LineNNN` names (and positions) are reassigned
  whenever the table refreshes or re-sorts, so a cursor keyed on either sits on a different thing
  a frame later — measured: picking a trait in the custom-faction editor left the next Enter
  picking whatever the re-sort moved under the cursor. Key a line on `GuiTableLine.Data`; with it
  as `ControlId.Referenced` the cursor even follows an entry from one table into the other.
- **`SendMessage(name, sender)` does not reach a zero-argument handler.** Most of the game's
  widget callbacks take `(GameObject obj = null)`, but not all — `OnPreviousHullCb()` and
  `OnNextHullCb()` take none — and with `DontRequireReceiver` the mismatch is silent, so the
  button simply does nothing. `AgeWidgets.Press`/`Toggle`/`Choose` look the arity up on the
  target's components (cached) and pick the overload; verify a new button by its EFFECT, never by
  the absence of an error.
- **Every tooltip is an ordered list of panel features.** `GuiTooltipWindow.DoBind` resolves
  the tooltip's `Class` through the description database and instantiates one prefab per
  feature under `PanelFeaturesTable`; a feature's SUB-features are added as further siblings in
  the same table, not nested, so the drawn tooltip is always one flat ordered list. `IsSeparator`
  and `IsSpacing` are on Assembly-CSharp's global `GuiPanelFeature`, not on the firstpass
  `Amplitude.Unity.Gui` base — a REPL probe typed to the latter cannot see them.
- **`Gui.GetTitle` can hand back a key that has no translation.** `ShipStatCommandPoints`
  declares `%ShipStatCommandsTitle`, which the corpus no longer has; the engine's own naming
  convention (`"%" + name + "Title"`) resolves it. Anything reading a title through the element
  database needs that fallback, and silence rather than a `%key` as the last resort.
- **A typewriter label already holds all its words.** `AgeModifierTypewriter` does not write
  text a character at a time: it sets the whole string once and advances the label's
  `CurrentLine`/`CurrentCharInLine`, which only the RENDERER honours. So `AgeText.Label` on a
  mid-animation label is complete, and an announcer never has to rebuild the panel's phrasing
  from the model to beat the animation.
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
- `GET /gui/graph?screen=KEY` — what an UNFOCUSED registered screen would offer, built without
  focusing it; an inactive one answers `screen inactive: …`, a bogus key 400s with the key list
- `POST /input` — body = one action key (`ui.down`, `buffer.lineDown`…); its key-claim counterpart is
  `/eval ES2Access.Dev.DevProbe.Claims("Escape")` — the latch only lives for the frame an injection
  is consumed (no key was held), so catch it with `POST /wait` on the probe's own text, never a
  second request
- `GET /gui/game?path=&depth=` — Unity hierarchy; `GET /gui/age?window=&depth=&visibleOnly=` —
  AGE widgets with rects (`window=` is the filter; `/gui/game` is the one taking `path=`)
- `window=` matches a registered window, a shown panel, then any named AgeTransform under them,
  and `depth=`/`visibleOnly=`/`fields=` apply from there; an empty answer always carries an
  `error`/`note` line, and a node cut off by `depth=` is kept (`more:true`), never pruned
- `GET /gui/age?...&fields=name,kind,text,tooltip,rect,interactable,enabled` — flat text, one
  indented line per widget, only those fields, empties omitted
- `POST /eval?settle=MS&speech=0` — C# REPL (gotchas below); response carries caused speech
- `POST /wait?timeout=MS` — body = bool expression, evaluated every frame
- `POST /loadsave` — body = save title (empty = newest); retryable `[not ready]` until it acts
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

## 4. Recipes (ES2-concrete; rationale in dev-server.md / making-screens-accessible.md)

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

**Session loop.** `.\run-game.ps1 -NoSpeech -NoWait -LoadSave "Beginner test"` —
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
for; nothing about it shows in the spoken lines alone.
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

**A panel of wordless readouts.** `SystemManagementScreen`'s generic scrape reads a side panel
off the shape of its widget tree, which cannot name a bare number beside a symbol. `Special()` is
the escape hatch: match the widget by its game COMPONENT (`PopulationCount`,
`SystemRepresentativeItem`) or against a field of the owning `SidePanel` (`HapinessGroup`,
`GrowthGaugeItem`, `PoliticalSensitivityBreakdown`) and return a hand-built cell. `Transparent()`
is its partner, for a group the game made clickable that is really a band of readouts (the
approval box answers a click only in god mode). Names come from the game: `AgeWidgets.TooltipTitle`
for anything with a `GuiWrapper` on its tooltip, `Gui.GetLocalizedTitle(property)` for a measure,
the tooltip's first line for a control that explains itself on hover. Keys must include
`widget.name` — a per-panel suffix alone collides across a repeated row and throws
`Duplicate control id`, which silently empties the WHOLE screen.

**The tutorial picker** is raised by `NewGameScreen.OnBeginShow` and only while
`TutorialManager.IsPlayingForTheFirstTime()` (registry `GameSettings/HasAlreadyPlayedOnce`, which
only `GameClientState_Introduction` ever sets — cancelling leaves it, so the box comes back). Back
to the MAIN MENU is two Escapes, i.e. `window.HandleInput(InputAction.Exit)` on the modal and then
on `NewGameScreen`. Never press Confirm or double-Enter a card in a test: both start a game.

**Working the new game lobby.** Everything is lobby-local and reversible (restore what you
change; `w.Session.GetLobbyData<string>("competitorcount")` etc. is the before/after probe).
**Never press Start** (`OnClickStartCb` launches). **Every way out of `FactionChoiceModalWindow`
COMMITS the highlighted faction** — Escape, Select, and the button labelled "Cancel", because
`GuiModalWindow.OnCancelCb` is `HandleInput(InputAction.Exit)` and this window routes Exit to
`OnValidateCb` (measured: picking Sophons and pressing Cancel left the lobby on Sophons). Opening
it is safe if you put the selection back first; `Gui.GetPlayerLobbySlot(ng.Session).FactionName`
is the before/after probe (fixture: `FactionTerrans`). Selecting a card does NOT commit.
`AdvancedSettingsModalWindow` is a safe open + `HandleInput(InputAction.Exit)` (its Back button is
the same `OnCancelCb`); the lobby stands down while either is up. The advanced window builds a
table per CATEGORY once and shows only `CurrentCategory`'s — read whichever is drawn, never the
container's first child.

**Multi-row tables** need a real fixture with several saves/rows — do not mutate the game's
data structures to fake one.

**Proving a refactor changed no spoken or buffer line.** Walk every reachable screen family
with `POST /input` and save `GET /gui/graph?buffers=1` per family to a scratchpad `before/`,
make the change, walk the identical route into `after/`, and `diff`. Normalise the ids that
carry an instance hash (`droplist:-138580/…`) before diffing. Two things make it work: the
dump is text and stable, and unfocused Class-backed tooltips read EMPTY on both sides, so
they cancel. For a family whose "before" you only realise you need afterwards,
`git stash push -u -- ES2Access ES2Access.Tests` → build → `/reload` → capture → `git stash
pop` → build → `/reload` costs about three minutes and is how `screen.game-menu` and
`screen.rename` got baselines. `GET /gui/graph?screen=KEY&buffers=1` reaches screens whose
window exists without a game running — out of a session `screen.game-menu` and
`screen.rename` both declare real content, `screen.galaxy` and friends answer "not active".

**Silence in `/speech` is only evidence for controls that would have spoken.** An enabled
button's activation is also silent, so a transcript cannot distinguish "refused" from
"acted" for buttons — prove a button refusal with a state probe (queue count, graph dump),
never by absence of speech. Checkbox/slider/combo refusals are provable by silence.

**Moving the galaxy camera.** `GalaxyViewLevels.PanTo/ZoomTo/ZoomToStep/OpenSystem` in the mod (`ZoomToStep(node, 9)` is how a
test puts the fixture's camera back home in one call); from
`/eval`, `((GalaxyViewCameraController)Services.GetService<ICameraService>().CameraController)
.ForceZoomingOnPosition(step, pos)` (fully qualify). There are 13 steps: step 3 draws a system's
name only, step 9 its whole label (name + planet circles), and **only step 12, the last, reaches
the ORBITAL view** — `CanFocusGalaxyEntity()` is `zoomStep == ZoomStepsCount - 1`, and until it is
true `Gui.GuiGameWindowService.FocusedStarSystemNode` stays null and
`PlanetLabelsWindow_SystemOrbital` (one `PlanetLabel_SystemOrbital` card per planet) is never
shown; the camera must also be within `DistanceMinToCatchFocusOnNode` of the node, so zoom AT it.
Step 9 vs step 12 is the evidence-crop pair for the two things a planet child can read. Only ONE system label is
visible at either step (86 exist, all keeping their node and tooltip), so the tree's label lookup
is unaffected — but at step 12 the focused system's own label is pushed off the top of the screen
(y ≈ -230), which is why the system node's pointer goes to
`PlanetLabelsWindow_SystemOrbital.StarTooltip` instead. Never `SetZoomStep()` alone: it swaps the
drawn layer without moving the camera. `DevProbe.Camera()` before and after; the fixture's home is
focus `[68.884, 0, -22.45]`, zoomStep 9.

**Entering a system re-opens the tutorial.** The first time the camera reaches a view level,
the game pops that level's tutorial page — so an Enter-on-a-colony test leaves the popup
un-minimized. Put it back (`TutorialPopupPanel.MinimizeToggle`, then send its `OnSwitchMethod`)
before calling the run done.

**Opening a game modal from `/eval`** (to measure it without walking there): set what its
opener sets, then show it — for the improvements list,
`var w = Gui.GuiService.GetWindow<ImprovementsManagementModalWindow>(); w.ColonizedStarSystem =
...ColonizedStarSystems[0]; Gui.GuiService.ShowWindow(w);`. Close it the way Escape does:
`w.HandleInput(InputAction.Exit)` (`InputAction` is Assembly-CSharp's, NOT
`Amplitude.Unity.Input`'s). Escape itself cannot be injected — `POST /input ui.back` only
proves the mod does not consume it.

**Stepping between planets on the planet overview** re-enters the SAME view level with a new
planet: `Gui.GuiGameWindowService.CurrentGalaxyViewLevel` (what `GalaxyViewLevels.Level` and
`At<T>()` read) goes NULL for a few frames while it happens, and the window unbinds its planet.
A screen gated on either pops and re-pushes on every step. `GalaxyViewLevels.LevelThroughTransitions`
is the view's own answer and does not blink; gate on that and declare nothing while the window
is empty (an empty `Build` leaves the cursor untouched — `KeyGraph.Rerender` returns false).

**A card's tooltip is rarely on the card.** `PointerFocus` shows the tooltip of the widget it is
pointed AT, so pointing at a row whose tooltip hangs off a child inside it (the planet card's
anomaly rows) draws nothing while the readout still says "has tooltip". Point at
`tooltip.AgeTransform`, not at the row — and prove it with `DevProbe.Tooltip()`, which is the
only thing that catches it.

**Opening the star system page.** `GalaxyViewLevels.OpenSystem(Gui.PlayerEmpire.GetAgency
<DepartmentOfTheInterior>().ColonizedStarSystems[0].Node)` from `/eval` (Dusay, GUID 535 in the
fixture; `GameEntityGUID` is NOT in `Amplitude.Unity.Game`, so go through the node). The page
arrives in pieces — the side panels a frame or two before the planet cards — so a screen that
declared the half that existed seated the cursor on the wrong stop for good; the fix is to
declare NOTHING until the late half is drawn.

**What the turn-1 fixture cannot show on the orbital cards**: neither uncolonized planet's
Colonize button is offered — both are tech-blocked, and the game leaves a blocked button
`Visible` AND `Enable` while turning its click into "jump to the missing technology", so
`Gui.IsHintActive(button.AgeTransform)` is the only thing that tells them apart: gate on it, never
on `Enable`. Buy-outpost, minor faction, pirate lair and all five `SecondaryButtonsTable`
buttons are undrawn (measured: `Visible=false`, `Enable=true`, on every card — `Enable` says
nothing here); the whole table is hidden, because every `Refresh*Status` returns before showing
its button when no fleet in the system offers the action, which at turn 1 means no Behemoth. The
one anomaly in the fixture is Multiple Moons on Dusay II. Those five buttons carry CLASS
tooltips and so have no short name on the card — but the game DOES name each of them on the
fleet action it carries out: `%InitiateTerraformPlanetFleetActionTitle`,
`%InitiateRestorePlanetFleetActionTitle`, `%InitiateReduceAnomalyFleetActionTitle`,
`%LaunchMiningProbeFleetActionTitle`, `%DestroyPlanetFleetActionTitle`. Grep the corpus for
`FleetActionTitle` before reaching for `ModStrings`.

**A hint button's tooltip has three parts, in a fixed order**: the button's own description, then
`"\n\n"` and the failure (`Gui.FormatFailure`, Gui.cs:1072), then — only for a missing technology —
`"\n" + %MissingTechnologyClickDescription`, appended by `Gui.FormatButtonHint` (Gui.cs:1207).
So the refusal alone is lines[1..] minus that instruction, which is what `RefusalText.Compose`
does. Measured on Dusay I: "Colonize the planet…" / "Missing technology Maximized Exploitation" /
"Hold Control+Click to locate this technology in the technology tree".

**The card draws FIDSI two different ways** (`PlanetLabel_SystemOrbital.RefreshFIDSI` :1012-1028):
a colony gets `FidsiEnumerator` with numbers, an unsettled world gets `FidsiScoreTable` with pips.
`FidsiProperties` holds SIX entries and `DisplayedProperties` is 5 — the sixth is `Happiness`, not
an output. Read the numbers only where the enumerator is visible, or the buffer describes a card
nobody can see.

**The planet constructible panel has no fixture either.** `PlanetConstructiblePanel` is opened
only by the card's Terraform and Reduce Anomaly buttons
(`PlanetLabelsWindow_SystemOrbital.OnTerraformPlanet` :255-265, `OnReduceAnomaly` :285-295), and
neither button is ever drawn without a Behemoth in the system. What IS testable offline:
`screen.planet-constructibles` registers (`/gui/graph?screen=…` answers "not active"), and its
predicate reads false at the galaxy overview, at the orbital zoom step with the cards drawn, and
on the management page. Opening it from `/eval` is not worth it: `ShowConstructiblePanel` is
private and indexes `fleetByActionDefinitionDictionary`, which at turn 1 holds no fleet.

**What the turn-1 fixture cannot show on the planet overview**: no planet in Dusay has a
curiosity, a resource deposit or any depletion, so those three rows of the card are code-verified
only; and the game's `PopulationModalWindow`, which a population entry's own click opens, has no
screen.

**What the turn-1 fixture cannot show on that page**: the queue is empty (reorder, buyout and
the queue action menu need a queued item), the hangar is empty (ship list, every toolbar
button), there is one colonized planet (population transfer has no destination), colonize is
tech-blocked on both other planets, the home planet is `IsUnique` so **planet rename is not
reachable at all**, and the tutorial disables the representatives' details button. The one
permitted state round-trip is Enter on a cheap constructible then Cancel from the queue line's
menu — check `dust` and `queue=0` before and after (`ConstructionQueue.PendingConstructions`).
The **improvements modal** holds exactly two tiles (Colony Base, Galactic HQ), neither
destructible, so the toggle path, the Scrap button's enabled label and its confirmation, grid
wrapping and scroll-into-view have no fixture. `StarSystemPopulationModalWindow`'s opener is
tutorial-locked.

**The system-discovery cutscene has no fixture at all.** It only runs on a system's FIRST
visit (`GalaxyViewLevel_SystemDiscovery.CanBeActivated`: explored, visible, planets-visible,
not already discovered), so reaching it means exploring — which the fixture forbids. What IS
testable offline: the screen registers, and its predicate reads false at the galaxy,
management and planet view levels (walk the three and call `IsActive()` on the registered
instance). `Application.Preferences.ForceSystemDiscoverySequence` is the game's own re-run
switch, for a human running the manual script on a throwaway save.

**Escape out of a view level** cannot be tested through `/input`: with no screen of ours
focused the injector's action is dropped before the game sees it. What the key reaches is
`StarSystemScreen.HandleInput(InputAction.Exit)` — call that to prove the destination, and
leave the key routing itself for the human test script.

**Launcher stuck in session 0.** A `launcher-x64` orphaned into the *Services* session
never exits and cannot be killed; the launch guard skips other sessions, but if a launch
still fails, `tasklist /FI "PID eq <pid>"` tells you which session you are fighting.

**A tooltip family's evidence pair.** Focus the control, `DevProbe.Tooltip()` for the typed
reading, then `Gui.GuiService.GetWindow<GuiTooltipWindow>(false).AgeTransform.GetGlobalPosition()`
for the rect and `crop-shot.ps1` on it — the tooltip is anchored to the pointer, so its rect
moves between runs and a crop from an earlier probe lands on empty sky.

**Injecting a sequence of keys.** `POST /input` one action key per request, ~0.4 s apart, then
read `/speech?since=N` — `next` from a `since=0` read before the sequence is the baseline. The
Bash tool mangles `python -c` here (it injects `|| goto :error`); keep the JSON formatting in a
`.py` file in the scratchpad.

**State restoration etiquette.** Leave the fixture as found: tutorial popup MINIMIZED, no
notifications pending, camera at home (`DevProbe.Camera()` before and after), no text field
holding game focus (`AgeManager.Instance.FocusedControl = null`), `DevProbe.TooltipDelay(-1)`
(a set delay survives reloads on purpose — and so does the restore cache being LOST by a
reload, which makes one `-1` put back whatever was set at the time of the last reload; check
`now` against `registry` in the reply and call it twice if they differ).

## 5. Keeping this file honest

Implementation stages update this digest as part of being done — a new helper, route, or
recipe lands here in the same change, and the dated header line moves. Screen status changes
land in `docs/roadmap.md`; game-agnostic lessons land in `docs/generic/`, not here. When a
design is reversed or content moves between docs, grep the whole docs tree for the old
mechanism's name and for inbound references before calling the change done — stale rows
state reverted designs as current.

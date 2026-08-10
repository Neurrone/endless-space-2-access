# ES2 dev loop — the working map

**Current state (2026-08-10, gesture-language rework COMPLETE — full parity):** the fixture save is **`[Beginner] test`** (turn 2; the title
`DevProbe.Saves()` reports — the old `[Beginner] access test` is gone with the previous machine), with
**`[Midgame] quests fleets`** (turn 3) as the second fixture — the one that has a quest pinned. The out-game
front — main menu through the whole new-game flow (lobby, advanced settings, faction
chooser, custom faction editor, tutorial picker) — is implemented and past manual review;
the in-game front is the galaxy family, where the system tree, management page, planet
overview and the SELECTED-FLEET PANEL (`FleetPanel`, a CONTRIBUTOR to the galaxy page, not a
screen: its action buttons with the game's refusals, the garrison list, the hero band and the
ships, live-verified through a real merge and a real garrison order) all live.
**The galaxy map now speaks the map's own two clicks** (gesture-language rework, stage 2): there
is NO `galaxy:fleets` stop any more — a fleet is a child of the place the map draws it, a system
or a starlane, after that place's planets and lanes. **Enter is the left click** (a system
force-zooms to the orbital step; a fleet selects; a lane deselects; a planet card opens the
planet's page; the label's management icon, now its own child node, opens the system page) and
**Backslash is the right click** (a place SENDS the selected fleets there; a system with nothing
selected undoes a forced zoom). "Cancel move" is gone — the game has no cancel gesture, and a
fleet is stopped at the next system by backslash on the lane it is already flying. A planet
card's action menu is gone too: the buttons the card draws are child nodes, and a hint-blocked
one is declared REFUSING with the game's sentence. A system's starlane children are walked and
NUMBERED clockwise from north and each says the compass word it leaves on ("Starlane 1 to an
unexplored system, northeast"), and both a system and a lane say WHICH FLEETS the map draws
there in the game's own count phrase ("Dusay, group, colonized, 2 Fleets"; `FleetPresence`), with
the names in the review buffer. **The selected-fleet panel and the quest journal speak the rest of the game's own gestures** (stage 3):
the panel's stops are `fleets:management` → `fleets:ships` → `fleets:actions` (an approved deviation
from drawn order), a fleet line and a ship tile say "selected"/"not selected" every time and take the
game's own three selection clicks on Enter / Ctrl+Enter / Shift+Enter, a ship is moved between fleets by
carrying it with Space, and the quest card's pin is a CHILD NODE of the card ("Pin Quest") rather than
the invented Alt+Enter it used to be. **The follow-up finished the language** (three owner-directed
changes): a gesture key with nothing to do is SILENT rather than saying "Nothing to do here", Enter and
Backslash on a system now say "Zoomed in"/"Zoomed out" (the old "Show system view" sound-collided with
the management node's "Open system"), and the MANAGEMENT PAGE'S PLANET CARD lost its action menu — Enter
is the card's own click (the planet's page), Colonize and Rename are drawn-button child nodes, and a
population unit is moved between planets by carrying it with Space. **The final stage made Enter
click-parity everywhere and turned the carry into the keyboard's DRAG**: the last two action menus
(the research queue item's and the construction queue line's) are gone — Enter is each row's own
click, which dequeues a technology and cancels a construction behind the GAME's own confirmation when
something has been invested — `ChoiceSubmenuScreen` is deleted, a queue line's buy-outs are drawn
child nodes, Shift+Up/Down and the `OnReorder` slot are gone because a queue is reordered by DRAGGING
a row onto another ("Moved ⟨name⟩ to position ⟨n⟩", the game's own drop-index rule), Space now only
picks up / swaps / puts back while **Enter is the drop**, and the phrases are the drag's
("Dragging ⟨item⟩", "Cancelled drag"). The two remaining "nothing to do" cues went silent with
the rest. All of it
awaits manual review (the improvements modal has passed it), plus the research screen
(the technology wheel, implemented and live-verified — including the camera-follows-the-branch rule,
the ring deeds and the recommended-technologies stop — awaiting manual review) and the quest
journal (`QuestJournalScreen`, layer 16, live-verified on the one quest the turn-3 fixture draws —
awaiting manual review).
Screen-by-screen status, including what each fixture cannot show:
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
| `TooltipFeatures.Read` | One feature into lines: scoped row banding, repeated items, four typed readers (ship stats, fleet stats, the military power pair, a hero's card), separators skipped by the game's own flags, unknowns to the fallback | `ES2Access/UI/TooltipFeatures.cs` |
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
| `GalaxyViewLevels` | Which `GalaxyViewLevel` is up (`At<T>()`, `Overview`, `Scanning`, `LevelThroughTransitions`, `FocusedSystem`) + where the camera is (`ZoomStep`, `AtOrbitalZoom`, `DefaultZoomStep`, all -1/false when the galaxy camera is not the live one) and the routes (`PanTo`, `ZoomTo` — the game's own `ZoomInOnNode`, which is what a left click on a system does, `ZoomToStep` for putting a step back by hand, `ZoomForced`/`RestoreZoom` — the map's own record of a click-zoom and its right-click undo, `OpenSystem`, `OpenPlanet`); stateless, so reload-safe | `ES2Access/UI/GalaxyViewLevels.cs` |
| `AgeWidgets` | The per-widget questions every screen asks: `Visible`/`Enabled`/`Operable` (ancestor-walking), `Raw`/`Readable`/`TooltipLines`/`TooltipTitle` (the `GuiWrapper` name behind a wordless icon), `Press`/`Toggle`/`Choose` (replay the widget's own handler; `Choose` takes a drop-list entry through the list's own `OnSelectionObject`/`Method`), `Point`/`PointAt`, `TextOf` (a group's whole drawn phrase, icons named) | `ES2Access/UI/AgeWidgets.cs` |
| `FieldReadout.Compose` | A panel's fields as one spoken line, blanks dropped; null when there was nothing to say, which is a passive announcer's "not filled in yet" | `ES2Access/Core/Speech/FieldReadout.cs` |
| `RefusalText.Compose` | A blocked button's tooltip trimmed to the refusal itself — leading description off, the game's mouse instruction dropped | `ES2Access/Core/Speech/RefusalText.cs` |
| `GlobalHud` | The clusters drawn over every view level (empire banners, pinned quest, collapsed tutorial bar, notifications, turn controls) + the turn and pinned-quest watchers; every page under them calls `Empire`/`Quest`/`Tutorial`/`Notifications`/`Turn` in drawn order | `ES2Access/Screens/GlobalHud.cs` |
| `Screen.PushChild` / `RemoveChild` | Mod-owned sub-screens: one linear chain, deepest is focused, a covered parent keeps its cursor | `ES2Access/Screens/Screen.cs` |
| `DropListScreen.Open(list, title, choose)` | Any `AgeControlDropList` as a sub-screen; entries fall back to their tooltips when the list is drawn as icons, then to `EmpireColors` when it is drawn as bare swatches; the focused entry is POINTED at, so the game draws its tooltip | `ES2Access/Screens/DropListScreen.cs` |
| `EmpireColors.Name(color)` | What the player's chosen palette (`Public/Mapping/Palettes.xml`) calls a drawn colour — matched by colour, not by list position; `ModStrings` `color.*` keys, falling back to the game's identifier split at its capitals | `ES2Access/UI/EmpireColors.cs` |
| `SettingRows` + `TextFieldEditor` | One game `SettingItem` as a row (every `Gui.ControlType`, the slider's index-stepping write path, `Drawn` = visible AND alpha > 0), plus the shared row shapes every lobby-family screen builds through — `AddCombo`, `AddButton`/`AddButtonRow`, `AddTextField`, `AddReadout` — and the deferred keyboard hand-over to a text editor | `ES2Access/Screens/SettingRows.cs` |
| `DevProbe` | Compile-checked one-liners: `Screen() Stack() State() Saves() Camera() Windows() Patches() Claims(keys?) TooltipDelay(s) Tooltip() UnknownIcons()` | `ES2Access/Dev/DevProbe.cs` |
| `DevProbe.Claims("Escape,Return")` | What the input layer is claiming FROM the game: the consumed-key latch (key + still held), `backClaimed`/`claimsBack`, `layerLive` split into `screenFocused` and `keyboardElsewhere`, and `ClaimsKey`'s side-effect-free answer per named key | `ES2Access/Dev/DevProbe.cs` |
| `/input` queue | `ModInput.Inject` — actions at the production dispatch point; touches no physical key state, so game-also-sees-the-key bugs need separate link-by-link probes (`DevProbe.Claims` is the layer's end of one) | `ES2Access/UI/Input/ModInput.cs` |
| `TypeAhead` + `SearchScope` | Type-ahead as the player meets it: the letters typed, where the last result landed, and whether focus has `Strayed` off it; `SearchScope.OverStop` is the default scope (the focused stop, one result per tabular row via `NodeVtable.Column`), a screen overrides it with `Screen.TypeAheadScope` | `ES2Access/Core/UI/TypeAhead.cs`, `SearchScope.cs` |
| `ResearchText` | The research screen's own phrases: an arc between two technologies said from the focused end (source vs target is the whole sentence), a ring's aggregate counts, the cost/turns/queue-position readout shared by a dot and a recommended row, and which of the game's four deed-state words a ring's marker is painted in | `ES2Access/Core/Speech/ResearchText.cs` |
| `ResearchCamera` | Which view opening or closing a branch of the wheel leaves the player in — quadrant, ring, or the whole wheel (closing a stage lands in its quadrant, not at the overview); engine-free, so the rule is tested off-game | `ES2Access/Core/UI/ResearchCamera.cs` |
| `FleetOrders` | Where a fleet is (`Orbit`/`Heading`), which fleets an order would move (`Selected` — the game's own selected-garrisons repository), the route to a destination (`PathTo` a node, `PathToLink` a starlane — one pathfinding search each, so KEYPRESS-only, never per frame) and the order itself (`CanSend`/`Send`), posted the way `GalaxyGarrisonCursor.TryToMoveFleet` posts them. **No cancel**: the map has no such gesture — `PathToLink` on the fleet's OWN lane is the game's stop-at-the-next-node answer | `ES2Access/UI/FleetOrders.cs` |
| `FleetPanel` | The selected-fleet strip as a CONTRIBUTOR (the `GlobalHud` shape: `Baseline`/`Forget`/`Update`/`Build`), so the map stays navigable while a fleet is selected; three stops in the OWNER-APPROVED order `fleets:management` → `fleets:ships` → `fleets:actions` (a deviation from drawn order, recorded on the class), plus the arrive/leave announcement no screen change speaks for any more. A fleet line is a drop target for a carried ship (`DepartmentOfDefense.CanTransferShips` decides, `FleetsScreen.TransferShips` commits) | `ES2Access/Screens/FleetPanel.cs` |
| `CardActions` | The buttons a card or a queue line draws, as child nodes: the drawn-and-operable gate, the hinted-button refusal (declared REFUSING with the game's sentence), the mouse-instruction split and the pointer — one treatment, shared by the orbital card, the management page's card and the construction queue's buy-outs. A screen only says which widgets and what to call them (`AddNamedByMod`/`AddNamedByGame`/`AddNamedByTooltip`, or `AddRefusable` where the game keeps a refused button DRAWN and merely switches it off, then `Emit`) | `ES2Access/UI/CardActions.cs` |
| `Cells` + `Cell` | A panel's controls gathered with the widget they were read off, then emitted in the rows `AgeLayout` says the game drew them in — hoisted out of `SystemManagementScreen`, which still calls it through its own two-line `Add`/`Emit` | `ES2Access/UI/Cells.cs` |
| `ShipRows` | The ships half of a garrison, wherever it is drawn: the toolbar (named by the game's own `%Fleet*Title` action titles) and the ship tiles. `StarSystemHangarPanel` IS a `ShipsManagementPanel`, so the star system page and the fleet panel share one builder — and each inherits the buttons its own fixture never draws. A tile carries all three selection gestures (all one replayed click; the physical modifier picks the game's branch) and, where the caller says a drop is reachable, the pick-up (`ShipRows.ShipKind`) | `ES2Access/UI/ShipRows.cs` |
| `GraphNodes.SelectionItem` | A row of a list the player picks SEVERAL things out of: both membership words (never the radio's silence), a `settled` reader for what to say straight after a chord when the widget's own tick lags the model, and the two chord slots left for the screen to wire | `ES2Access/UI/GraphNodes.cs` |
| `SelectionText` | What such a list says: `Membership(bool)` and `Range(names)` — the whole-selection sentence a range press answers with, null for fewer than two so the caller falls back to the row's own state | `ES2Access/Core/Speech/SelectionText.cs` |
| `GraphNodes.RefusalPart` | A blocked control's reason as an ADDITIVE tooltip part — and only where the tooltip is merely indicated, since an announced tooltip already contains it | `ES2Access/UI/GraphNodes.cs` |
| `GraphNodes.HintSections` | The sections for a control whose tooltip the game may have appended a MOUSE instruction to ("hold Control+click to find the missing technology"): the words up to it speak, the instruction is reviewable only. One declaration, two surfaces, no duplication | `ES2Access/UI/GraphNodes.cs` |
| `Screen.TypeAheadScope(focused, render)` | A screen's own search scope, now handed the standing render so it can EXTEND `SearchScope.OverStop` rather than re-derive the stop (the galaxy adds every fleet buried in a closed system or lane) | `ES2Access/Screens/Screen.cs` |
| `CompassDirections` | Which way one place on the map lies from another, as one of eight words: `Bearing(east, north)` clockwise from north, `KeyForBearing`/`DirectionKey`/`Direction` for the word. The arcs are CENTRED on the compass points (north = 337.5°–22.5°); engine-free, so the boundaries are unit-tested. Named `CompassDirections` because `UnityEngine.Compass` exists | `ES2Access/Core/Speech/CompassDirections.cs` |
| `TypedText.Frame` | The characters typed this frame — the keyboard half of `GraphNavigator.TypedCharacters`; nothing while Ctrl/Alt is held, and `Input.anyKey` gates the allocating `inputString` read | `ES2Access/UI/Input/TypedText.cs` |
| `CarryState` + `CarryActions` | The keyboard's DRAG: what is held (its spoken name CAPTURED at pick-up, never re-derived), the cargo KIND that decides which controls will take it, and TWO decision tables — `Press` is Space and only ever holds things (pick up / swap / put back / silence), `Activate` is Enter and is the only thing that DROPS, through the target's OWN check, answering `NotOurs` everywhere else so the control's own click runs with the drag still live. A refusal speaks the game's words and keeps the drag. Engine-free, so both tables are unit-tested; `ModEntry.Carry` is the live one and `Carry.DropTargetPart(kind)` the live part a target announces itself with | `ES2Access/Core/UI/Carry.cs` |
| `FleetPresence` | Which fleets the map DRAWS at a place: `At`/`On` (a system's docking slots, a lane's in-flight legs) as the game's own relation-aware count phrase (`GuiFleetGroup.Title`), `LinesAt`/`LinesOn` the same groups with their fleet names for the buffer, `FleetsAt`/`FleetsOn` the fleets THEMSELVES (the hangar garrison dropped — it is counted but is not a fleet), which is what the tree hangs under a place. Walks the two label windows' own repositories, so no vision rule is reimplemented. The count phrases never run from `Build` (`ValuePart(..., watch: false)`); the enumerations do, but only for a place the player has OPENED | `ES2Access/UI/FleetPresence.cs` |

## 2. Layer budget

Static per screen (doctrine: ui-navigation.md "Layers are static"):
`0` main-menu and the new-game lobby (never up together — showing one hides the other) ·
`5` advanced settings · `6` faction chooser (both over the lobby, their only opener) ·
`7` custom faction editor (over the chooser, whose own window hosts its panel; the three are
never up together, and all sit well under the drop list a setting can open and the message
box a Cancel or a Delete confirms in) ·
`10` galaxy, star-system, planet-overview and system-discovery (the four
view levels, never up together) · `20` planet-constructibles (the panel a planet card slides
out under itself) · `30` tutorial ·
`40` notification · `50` game-menu · `52` options (one number, above the pause menu that can
open it) · `55` load-save · `60` loading · `70` drop-list (above options, its owner) ·
`15` research (the technology wheel — a GuiScreen overlay drawn over whichever view level is
underneath, so above them and below the planet panel) · `16` quest journal (the other GuiScreen
overlay; the same strip of screen icons opens both, so the two are never up together) ·
`80` rename box · `85` improvements modal (over the star-system page, under its own
confirmation) · `90` tutorial-selection modal (over the new game screen) · `100` message-box. Action menus are CHILD screens and have no layer: the
manager focuses the deepest child of the top screen.

**The selected-fleet panel has NO layer** — it is a contributor to the galaxy page
(`FleetPanel`, above), not a screen, because selecting a fleet changes only the cursor and the
map underneath stays live and has to stay walkable. A layer of its own put it OVER the galaxy
and took the systems, the starlanes and the HUD out of Tab. It is contributed by the galaxy page
alone, which is complete rather than a gap: entering a system swaps the cursor to
`StarSystemCursor` and the game hides the window outright (measured — es2-facts).

**ES2 key map, in one place** (defaults in `ModEntry.BindKeys`; the generic table is
`docs/generic/input.md`). On top of arrows/Tab/Enter/Backspace/Escape/Home/End, Alt+arrows and
the Ctrl review chords: **Shift+Left/Right** coarse slider step, **Alt+Enter** the control's other
activation (queue at the head), **Backslash** the control's right-click command
(`NodeVtable.OnContextual`), **Space** pick up / swap / put back what is being dragged (`OnPickUp`),
**Enter** drop it where it will be taken (`DropKind` + `OnDrop`), **Ctrl+Enter** one item into or out
of the game's own selection (`OnSelectToggle`), **Shift+Enter** extend that selection to here
(`OnSelectRange`). There is NO reorder chord: moving an item within its list is a drag like any other.
The Enter chords pass the PHYSICAL modifier through to the game's handler, which
is how the game's own selection rules apply rather than a copy of them. Wired so far: the fleet
panel's fleet lines and ship tiles (both chords; on a fleet line the game treats Control and Shift
alike, so there is no range there) and the star system page's hangar tiles. The drag is wired for
SHIPS: pick up on a ship tile in the fleet panel, drop on any fleet line including the hangar's — and
deliberately NOT on the hangar page, which draws no fleet lines and so would offer a mode with no
exit — for POPULATION: pick up on a population row under a management-page planet card, drop on
another card of the player's in the same system (only offered where the system has a second colony) —
and for both QUEUES: a research queue item and a construction queue line are each a source and a
target of their own cargo kind, offered only where the queue holds more than one thing, and a drop
puts the carried item at the target's position ("Moved ⟨name⟩ to position ⟨n⟩").

**Enter is click parity everywhere.** Every node's Enter is the click the game itself puts on that
control, including the destructive ones — a research queue item dequeues, a construction queue line
cancels (instantly while nothing is invested, behind the GAME's own confirmation box once something
is). There are no mod-invented action menus left; a control's extra buttons are child nodes opened
with right. The one thing that displaces a node's click is a live drag landing on a control that
takes the cargo, which is what makes Enter the drop key.

Backslash, both Enter chords and **Space** are claimed on every mod screen and are **SILENT where
the control has no such command** — they are pressed speculatively all over a page, and a cue on
every one of them is noise. Silent but still consumed, and never a fall back to plain activation.
Space while something is carried is the same: consumed on a control that will not take it, silent,
carry kept. (Why Space is claimed even where nothing is draggable: the scan-view fact in
`es2-facts.md`.)
**Space is the one key shared with the game**: claimed only while a
search has text (it is a character then), while the focused control has something to pick up, or
while something is being carried — otherwise the game keeps it (`InputAction.ClaimedWhile` is the
mechanism; every other binding is claimed outright). While something is carried, **Escape puts it
down and goes no further** (`claimsBack` reads true only then), and the carry dies silently when the
player leaves the page it started on — a menu opened over that page is still that page.

**Typing a letter searches the focused stop** (no search key: the first printable character starts
one; Up/Down step the matches, Home/End their ends, Escape clears it and goes no further, any other
action ends it and then does its own job). So **A–Z are claimed from the game on every mod screen**
(`GraphNavigator.TakesTypedKey` via `ModInput.ClaimsTypedKey`, asked before the press), and Space
only while a search already has text (its other claim is the carry, above; a space typed into a
live search is text and the carry key stands aside for it). Screens opt out with `AllowsTypeahead` (the rename box) or
`CapturesRawInput` (the frames between asking for a key capture / text editor and the game taking
the keyboard).

**Escape is the game's, except over a surface the mod invented.** A screen answers
`ConsumesBack` (asked BEFORE the press), and `ModInput` latches EVERY consumed key until the
player lets go — the rationale for both is `docs/generic/input.md` (the back-key rules and
the liveness self-race law). `ConsumesBack` is NOT a copy of `Back()`: `DropListScreen`
handles Escape and still needs the engine to see it. Probe live with
`ES2Access.Dev.DevProbe.Claims("Escape")` — `claims` true only where a mod-owned surface is
focused, the latch shown when the surface has already gone. That probe, not
`/input ui.back`, is what proves the key does not fall through. It cannot tell a MODIFIED binding
from its plain one (it is asked per `KeyCode`), so a removed chord is proved by `POST /input` with
the action key instead: an unregistered action 400s and lists the ones that exist.

Game-mechanism findings (window gates, pool slots, tooltip internals, fleet and quest
mechanics, the icon numbers) live in [es2-facts.md](es2-facts.md) — a new fact lands there,
never here.

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
- `POST /type` — body = characters to TYPE at the focused screen (the type-ahead search), through the
  same gates a keypress passes; answers `taken`/`searching`/`search`/`results`/`focus` plus the speech
  it caused. `/input` cannot carry it: that queue is actions, and typing is text
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

**Raising a notification on demand** (the fixture has none pending):
`Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.Event.IEventService>().Notify(new EventEmpireIntroduction(Gui.PlayerEmpire))`
— dismiss afterwards (`Gui.GuiNotificationService.DismissGuiNotification(...)`); minimizing
leaves it in the icon strip, which is a fixture change. **For a notification whose event has
gameplay listeners** (anything on a quest), do NOT go through the event bus: build the
notification and show it directly — `var n = new NotificationQuestBegun(); n.Bind(new
EventQuestBegun(Gui.PlayerEmpire, quest)); Gui.GuiNotificationService.ShowGuiNotification(n);` —
then dismiss with the window's own binding
(`Gui.GuiService.GetWindow<QuestBegunNotificationWindow>().GuiNotification`). `IsAnyNotificationVisible`
is on `Gui.GuiGameWindowService`, not on the notification service. Raising the quest popup also pops
the "Tracking Quests" tutorial page, so re-minimize afterwards.

**World position → screen pixel.**
`((GalaxyViewCameraController)Amplitude.Unity.Framework.Services.GetService
<Amplitude.Unity.View.ICameraService>().CameraController).Camera.WorldToScreenPoint((Vector3)node.GalaxyPosition)`
— the galaxy camera hangs off the controller's `Camera` property; `Camera.main` is null in this
game and the controller's own GameObject carries no `Camera` component, so both of those routes
answer nothing. Screen y is Unity's (bottom-origin); `crop-shot.ps1` takes top-origin pixels.
That is how a spoken direction is checked against the picture (es2-facts, world axes).

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

**Testing a type-ahead search.** `POST /type` with the letters (`res`), read the `speech` array it
answers with, then drive the results through `/input ui.down|ui.up|ui.home|ui.end` and end with
`/input ui.back` ("Search cleared"). The key-claim half is `DevProbe.Claims("Escape,R,Space")`: with
a search up, all three read `claims:true` and `claimsBack:true`; after Escape clears it, Escape goes
back to the game (`claims:false`) while the letters stay claimed, because type-ahead is armed
whenever a mod screen is focused. Each keystroke re-announces the landing, so `/type "res"` answers
with three identical lines — that is the design, not a stutter.

**Working the technology wheel.** Open/close it from `/eval` with
`Gui.GuiService.GetWindow<GameOverlayWindow>().ControlBanner.ToggleScreen("TechnologyScreen")`
(F4 does the same); the first open in a session raises the "Tech Savvy" tutorial popup. The
permitted round trip is queue-then-cancel — probe with
`Gui.PlayerEmpire.GetAgency<DepartmentOfScience>().ResearchQueue.Length` and
`.PendingConstructions[i].ConstructibleElement.Name` before and after — but queueing fires
`EventTutorial_TechnologySelected`, so do it LAST and restore with `POST /loadsave`.

**Round-tripping the pinned quest** (how both halves of the `hud:quest` passive announcement get
proved in one run): stash the quest first — `Quest __pinned = Gui.PlayerEmpire.GetAgency
<DepartmentOfInternalAffairs>().QuestJournal.ActiveQuest;` — then unpin through the mod's own node
(`/input ui.down` onto "Unpin quest", then `ui.activate`) and read `/speech` for "No quest is pinned"
plus a `/gui/graph` with no `hud:quest` stop; put it back with `…QuestJournal.ActiveQuest = __pinned;`,
which is the same assignment the journal's own pin toggle makes (`NarrativeScreen.cs:443`) and
answers with "Pinned quest: …". Opening the journal from the panel node is safe and reversible:
`ControlBanner.ToggleScreen("NarrativeScreen")` closes it again, and the stop comes back with the
cursor still on it.

**Working the quest journal.** Open/close from `/eval` with
`Gui.GuiService.GetWindow<GameOverlayWindow>().ControlBanner.ToggleScreen("NarrativeScreen")`
(F7 and Enter on the pinned-quest panel node do the same; the mod screen is polled, so all
three land identically). Switching the filter is reversible and is the game's own radio group —
`ES2Access.UI.AgeWidgets.Toggle(w.QuestSelectionTogglesTable.Children[i].AgeControl as
AgeControlToggle)`, with `w.QuestFilteringRadioGroup.CurrentSelection` the before/after probe
(fixture: 0 = Current). **The turn-3 fixture draws exactly ONE card under every filter** — the
journal holds 40 in-progress and 13 completed quests and all but one are `QuestDefinition.Hidden`
or narrative events (`NarrativeScreen.cs:279`), so multi-card list navigation and the strip's paging
follow have no fixture at all. The Failed filter draws none, which IS the testable empty case: the
`quests:list` and `quests:detail` stops both disappear. **The pin is a child node of the card**, not a
gesture on it: `ui.right` opens the card, `ui.down` lands on "Pin Quest", Enter toggles, and
`QuestJournal.ActiveQuest` is the probe; unpinning speaks "not checked" from the toggle and "No quest
is pinned" from the HUD's watcher, even with the journal covering the panel. Alt+Enter on a card is
now silent by design (the game has no modified click there).

**Ordering a fleet around** (state-changing — only against a save you can reload, and only after
every read-only check is done). It is two halves: **Enter** on the fleet's own node selects it, then
**`/input ui.contextual`** (backslash) on the DESTINATION — a system node, or a starlane child of one
(expand the system with `ui.right`) — sends it, answering "Send fleet ⟨name⟩ here" or "Nothing to do
here". Post it through the mod's own key rather than from `/eval`, then probe the game:
`fleet.Position.IsInOrbit`, `fleet.Path.Destination` through `IPositioningService.GetGameNode`, and
`empire.GetAgency<DepartmentOfLabour>().EntityActions` (index it - never `foreach`) for the
`GoToFleetAction` whose `Initiator.GUID` is the fleet — the count stays at ONE and the `Id` changes,
which is how a supersede is told from a stack. The under-way `FleetLabel` only exists once a
move is in flight, so that is the moment to measure the pointer path (`AgeManager.Instance
.OverrolledTransform` + the tooltip window's rect). Restore with `POST /loadsave` and re-check
camera, fleets and quest. Note tutorial progress does NOT live in the save: selecting a fleet
advances the tutorial popup, and only re-minimizing puts the fixture back.
**What the turn-3 fixture cannot show is a RE-ROUTE.** Its one known system's three lanes all run
into the dark, so a fleet that leaves is instantly un-re-routable (`NextNodeUnknown`) — measured:
`PathToLink` and `PathTo` answer null for every destination except the lane it is already on. Every
lane also costs 8.3–13.3 against 5–6 movement points, so an ordered fleet always ends the turn
stranded mid-lane rather than discovering anything.

**Testing the selection chords and the drag.** `/input` cannot hold a modifier, so
`ui.selectToggle`/`ui.selectRange` reach the row's own click with NO physical Ctrl or Shift and the
game runs its plain (radio) branch: the injection proves the wiring, the announcement and the
fall-backs, never the modified semantics — those are code-trace plus the manual script. What IS
provable live: flip the panel's model from `/eval` and watch the row's live membership part
(`ShipsManagementPanel.DeselectShips()` plus `Dirty = true` makes a tile read "not selected" under a
standing cursor), then press the chord and read the state the row speaks back. The drag needs no
modifier and so is fully injectable: `DevProbe.Claims("Space")` is true only on a control with
something to pick up (or while dragging), `ModEntry.Carry.IsCarrying`/`.Held.Name`/`.Held.Kind` is the
state probe, a compatible row's readout grows "drop target" while something is held, `ui.carry`
answers "Dragging …" on a source and SILENCE everywhere else — including on a drop target that is not
also a source — with the drag kept, `ui.carry` back on the source it came from and `ui.back` both
answer "Cancelled drag" (`claimsBack` reads true only until it does), and **`ui.activate` is the
drop**: on a control that takes the cargo it announces the drop and the control's own click does NOT
run, on any other control the click runs and the drag survives it (inject Enter on a harmless toggle
to prove that half). Silence is proved with a `/speech?since=N` window, not with the `/input` reply.

**Working the selected-fleet panel.** It is a contributor, so there is no `screen=` key for it and
no screen change to wait for: its three stops simply join the galaxy page's, between the systems
stop and `hud:quest`, and `/speech` says "Fleet panel open for …". Open it the way the player does —
**Enter on a fleet node in the tree**, under the system it is parked at or the lane it is flying —
and check where the cursor actually is before every injected key
(`DevProbe.Screen()`): a blind `ui.next`/`ui.activate` run once landed on the HUD's "Close tutorial"
and raised its confirmation (cancel it with `ui.down` then `ui.activate`; Confirm is irreversible for
the fixture). Close the panel with `Gui.GuiService.GetWindow<FleetsScreen>().HandleInput
(InputAction.Exit)` — the same route the key takes, since Escape itself cannot be injected. The
turn-3 fixture's permitted destructive pair, LAST in a run: "Select all" then Merge (probe
`Gui.PlayerEmpire.GetAgency<DepartmentOfDefense>().Fleets.Count`, 2 → 1), then Garrison (1 → 0, and
the management row swaps Merge for the hangar-only Create). Neither is reversible without
`POST /loadsave`. **The hero band has no fixture** (no hero at turn 3) but can be MEASURED:
`w.FleetHeroPanel.Show()` draws it against a null hero and `.Hide()` puts it back — that is how the
assign/unassign button's naming was found to come from `AssignIcon.Visible` rather than from which of
the two shared-tooltip transforms is up.

**Moving population between planets** (management page). The drag is offered only where the system
has a SECOND colony of the player's (`ColonizedStarSystem.PlanetsColonized.Count > 1`) — with one, the
population rows are declared read-only and there is no pick-up (measured live: `Claims("Space")` reads
false on the row and `ui.carry` answers `unconsumed`), which is what both fixtures show
(Dusay: `planetsColonized=1`, `GetSpaceportSidePanel()` not shown). What IS testable with one colony:
push a drag by hand — `ES2Access.ModEntry.Carry.PickUp(new ES2Access.Core.UI.CarryItem(pop, "Imperials",
"population"), ES2Access.ModEntry.Navigator.Screen)` — and watch the card's readout grow "drop target",
`/input ui.activate` on the card refuse in the mod's fallback words with the drag kept, `ui.carry`
anywhere that is not a source answer silently with the drag kept, and `ui.back` answer
"Cancelled drag".
**`PlanetPopulationEnumerator.CanAcceptPopulationDrop()` THROWS when no drag is in progress**
(`DragInfo.TransitingPopulation` is null), so it can only be called with `PopulationEnumerator.DragInfo`
filled in — and it is a static, read every frame by the enumerator's own refresh, so clear it in a
`finally` or a marker the player is still looking at reads as already gone.

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

**Per-screen blocked-at-turn-1 inventories live in `docs/roadmap.md`'s row notes** (planet
overview, management page, improvements modal…) — one truth, updated with screen status.
What a test SESSION needs here: **the one permitted state round-trip on the management page**
is Enter on a cheap constructible to queue it and Enter on its queue line to cancel it — check `dust`
and the queue's names/order before and after (`ConstructionQueue.PendingConstructions`, indexed
never `foreach`ed). Queue two or three and the line becomes a drag source as well, which is how the
reorder is exercised inside the same round trip; the research queue is the same shape
(`DepartmentOfScience.ResearchQueue`, queued from the wheel's `research:suggested` stop in two key
presses). Both were run against a LIVE owner session and restored exactly. The home planet is
`IsUnique` so planet rename is unreachable; `StarSystemPopulationModalWindow`'s opener is
tutorial-locked; and at turn 3 no buy-out button is drawn at all
(`BuyoutTechnologyNotUnlocked` — es2-facts), so the queue line's buy-out children have no fixture.

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
recipe lands here in the same change, and the dated header line moves. This file is ONLY the
loop: game-mechanism facts land in `docs/es2-facts.md`, screen status changes in
`docs/roadmap.md`, game-agnostic lessons in `docs/generic/` — never here. When a
design is reversed or content moves between docs, grep the whole docs tree for the old
mechanism's name and for inbound references before calling the change done — stale rows
state reverted designs as current.

# ES2 interaction language — layers, keys, claims

The mod's ES2-specific interaction design: the layer budget, the key map, and the claim
rules. Read when BUILDING a screen; the loop itself is `docs/dev-loop.md`, and the generic
doctrine behind these rules is `docs/generic/input.md` / `ui-navigation.md`. A new layer
number or key binding lands here (bindings themselves need owner approval first). What a
particular SCREEN declares — its stops, its regions, its wording — is not here: that is
`docs/test-recipes/README.md`'s family files, and the game mechanisms behind them are
the game-facts topic files indexed in `docs/README.md`.

## Layer budget

Static per screen (doctrine: ui-navigation.md "Layers are static"). **A layer number is
allocated by the main agent when a stage is briefed, never claimed inside a stage** —
pipelined stages cannot see each other's claims, and three of them once picked the same
number independently.

The FULL roster lives in the source — one writer per number: grep `override int Layer`
under `ES2Access/Screens/`. Listed here are only the numbers carrying a CONSTRAINT or a
deliberate share; a bare allocation is recorded by the source alone.

`0` main menu, the new-game lobby, the victory-achieved page (`VictoryScreen`) and the
menu-replacing out-game pages (Credits/DLC/Mods/
Export/Join — registered after `MainMenuScreen`, and equal layers tie-break by registration
order; never up together, since showing one hides the others) ·
`5` advanced settings · `6` faction chooser (both over the lobby, their only opener) ·
`7` custom faction editor (over the chooser, whose own window hosts its panel; the three are
never up together, and all sit well under the drop list a setting can open and the message
box a Cancel or a Delete confirms in) ·
`10` the view levels — galaxy, star-system, planet-overview, system-discovery, plus the two
battle view levels (the space cinematic and the ground battle) — never up together ·
`11` scan view (over the view levels, under everything they raise; one number for every lens,
which are never up together) ·
`15` **every icon-strip screen, shared deliberately** — research, quest journal, senate,
empire, economy, military, academy, diplomacy — because the engine enforces the exclusivity
itself (`BackgroundRenderer` carries `GuiWindowsStackExclusive`, measured), so no two of them
can be shown at once ·
`18` notification (the engine's own ladder: above the screens, below every modal) ·
`20` planet-constructibles (the panel a planet card slides out under itself) ·
`21` troop management · `22` battle-tactics deck (both measured below the tutorial popup via
`AgeScreen.SortingOrder` — ModalRenderer 5 < OverlayRenderer 6) ·
`25` system-selection modal (over the star-system page that opens it
and under BOTH things it can raise itself: the tutorial page it registers a key for, and the
drop list its policy column opens) ·
`28` hero-selection (under the hero-inspection window its own Inspect raises) ·
`35` ship designer (under the hull drop list 70 and the
lose-changes box 100) ·
`36` election, population **and** recipe-creation — a deliberate three-way share: all sit on
the game's exclusive modal stack behind different openers, so no two can be up together, and
a window on such a stack voids any layer constraint against its stack-mates
(ui-navigation.md; owner rule 2026-08-12: mutually exclusive screens may share a number) ·
`45` hero inspection (under rename 80 and the message box 100) · `46` the academy
pair (shared — the two DLC modals, never up together) ·
`52` options (one number, above the pause menu that
can open it) · `53` table-filter menu (over both tables that can open it: the journal 51 and
the custom faction editor 7; under drop-list 70) · `70`
drop-list (above options, its owner) ·
`85` improvements modal (over the star-system page, under its own
confirmation) · `90` tutorial-selection modal (over the new game
screen) · `96` contextual prompt (over the scan view that raises it and the modal stack it draws last
on; under the non-blocking box) · `97` non-blocking box · `98` tutorial popup · `99` error box ·
`100` message-box.
Mod-owned CHILD screens (`Screen.PushChild`) have no layer: the manager focuses the deepest
child of the top screen.

**The tutorial popup sits at 98 — above everything except the two boxes that must be
answerable.** The game itself draws most tutorial popups over its own screens, modals and
notifications (`TutorialPopupLayer`, per page — `docs/gui.md`), so any lower number buries one of
them; what keeps 98 livable is that a collapsed popup stands down and that the mod follows the
panel's own visibility, so a popup the game has hidden holds nothing. The error box (99) and
the message box (100) go ABOVE it, because an error or a confirmation the tutorial buries is
unanswerable. A collapsed popup's bar is declared on the focused page EXACTLY while the game is
drawing it (`TutorialPopupPanel` shown + minimized — the game itself hides the panel for
UnderScreens pages under a cover, so "declared" and "drawn" are one condition), except on the
answer-only surfaces (`Screen.AnswersOnly`: the error box, the message box, the non-blocking box,
the drop list, the loading screen); the eleven HUD-edge pages keep their own placement
(`GraphBuilder.DeclaredStop` suppresses the shared append). Owner's third and final ruling on
this bar, 2026-08-12.

**The selected-fleet panel has NO layer** — it is a contributor to the galaxy page
(`ES2Access/Screens/FleetPanel.cs`), not a screen, because selecting a fleet changes only the cursor and the
map underneath stays live and has to stay walkable. A layer of its own put it OVER the galaxy
and took the systems, the starlanes and the HUD out of Tab. It is contributed by the galaxy page
alone, which is complete rather than a gap: entering a system swaps the cursor to
`StarSystemCursor` and the game hides the window outright (measured — `docs/fleets.md`).

## The key map

**ES2 key map, in one place** (defaults in `ModEntry.BindKeys`; the generic table is
`docs/generic/input.md`). On top of arrows/Tab/Enter/Backspace/Escape/Home/End, Alt+arrows and
the Ctrl review chords: **Shift+Left/Right** coarse slider step — and, while the galaxy's inspect
cursor is driving the map, that mode's skip instead, since a coarse step means nothing on a map
(below); **Shift+Up/Down** are the inspect cursor's alone and are inert everywhere else;
**Alt+Left/Right** are BOTH the inspect cursor's travel keys AND the page-turn pair, and both
actions fire on the same press — see *Place keys, page keys, end turn* below for why that is
safe — **Ctrl+Shift+Enter** the control's
other activation — the game's ALT-click (queue at the head) — **Backslash** the control's right-click command
(`NodeVtable.OnContextual`), **Ctrl+Backslash** the game's Ctrl+right-click — the SAME `Contextual`
action bound as a second chord, never a wired variant, because the game runs one handler for both
clicks and reads the physical modifier inside it (on the map: a free-movement-only route,
`FleetOrders.RequestedFlags`), **Ctrl+Alt+Enter** the control's DOUBLE click (`OnDoubleClick`),
**Space** pick up / swap / put back what is being dragged (`OnPickUp`),
**Enter** drop it where it will be taken (`DropKind` + `OnDrop`), **Ctrl+Enter** one item into or out
of the game's own selection (`OnSelectToggle`), **Shift+Enter** extend that selection to here
(`OnSelectRange`), **Delete** empty the control the cursor is on (`ui.clear` → `NodeVtable.OnClear`,
owner ruling 2026-08-23 — today only a key-binding cell wires one, below). There is NO reorder chord:
moving an item within its list is a drag like any other.
**Each of those keys means the game's own gesture and nothing else** — Backslash is the right click,
Ctrl+Shift+Enter the Alt-click, Ctrl+Enter the Ctrl-click, Ctrl+Alt+Enter the second click.
**The Alt-click is the one chord whose keys are not its gesture's, and that is deliberate**:
Alt+Enter is Unity's own built-in fullscreen toggle, handled inside the player's D3D11 window
code below every managed layer, so the mod's claim never reaches it and nothing suppressible
does either (bug 17). The gesture moved to Ctrl+Shift+Enter, which the game binds nothing to and
which keeps the family: every modified click is still a modified Enter. Owner ruling 2026-08-19.

## Modified clicks and their fall-backs

The three
modified LEFT clicks (Ctrl+Shift+Enter, Ctrl+Enter, Shift+Enter) **fall back to the control's plain click**
where the screen wires no handler of their own (`KeyGraph.ModifiedClick`): the player is physically
holding the modifier, so replaying the click is what lets the GAME's handler branch on it —
Ctrl+click to locate a technology — with no per-screen wiring, and a
handler that ignores modifiers just does its ordinary thing, exactly as a modified mouse click would.
A wired slot stays an OVERRIDE, for the controls where the game runs a genuinely different handler.
**The Alt-click's fall-back is the exception the move cost us**: the modifier the player now holds is
Ctrl+Shift, so a game handler reading `Input.IsAltKeyDown()` inside its own click no longer sees it.
The two queues are unaffected because both wire `OnAlternate` and move the item themselves
(`ResearchScreen.Queue`, `SystemPanels.QueueConstruction`); the game's other Alt-clicks —
`PlanetCard`/`PlanetLabelsWindow_SystemManagement` (a curiosity expedition at the head) and
`NotificationItemsWindow.OnCloseAllCb` (dismiss-all, whose Shift branch the new chord would take
instead) — must be WIRED if they are ever declared, never left to the fall-back. The curiosity one
is wired as of 2026-08-21 (`ES2Access/UI/CuriosityExpeditions.cs`), attached generically in
`CardActions.Emit` off the widget's own `CuriosityInteraction`, so the colony card has it and the
galaxy orbital card's fleet-mode twin does not. Dismiss-all is declared as of 2026-08-23, and NOT as
a chord: it is a BUTTON at the end of the notifications stop whose Enter runs the Alt branch's own
call (`docs/test-recipes/notifications.md`), so nothing has to read a physically held modifier.
Backslash and Ctrl+Alt+Enter keep absent-means-silent: a right click or a second click that does not
exist has nothing to replay. On a `GuiTable` row, the second click is wired by `TableSheet` for EVERY
table and every cell of a row (its `DoubleClickButton`, the row selected first because the game's
handlers all read `GuiTable.SelectedLine`), so the empire page's systems table opens that system's
management page, the military page shows the fleet on the map, and the two selection modals pick and
close — no screen declares any of it. The tables whose client does nothing with the gesture stay
silent, as the mouse does there. The save list's second click ACTS rather than shows — it loads the
row (behind the same confirmation the Load button raises in game) or saves over it (behind
`%LoadSaveConfirmOverwriteDescription`) — and it is carried like every other table's: the game's own
confirmation is the guard on chord and mouse alike (owner ruling 2026-08-14). A selectable table row
speaks its selection state with NO role word — the row's name plus "selected"/"not selected" is the
whole affordance, and a radio word on every row of an announced table is noise (same ruling).
Where the game has left a control switched on only so a click can
explain itself, Ctrl+Enter is that explanation: the jump to the missing technology (`Cells.Add` →
`AgeWidgets.Locate`), wired once for every such control; those controls still announce themselves
unavailable, and Enter on them does nothing, as the mouse's plain click does. (Owner rulings
2026-08-13.) The double-click chord is free because no handler in the game combines Ctrl and Alt with
a click and its own binding matcher is exact-modifier (`InputManager.InputsMatch`); a mod screen
replaying a double click checks that the game's handler does not read the modifiers the player is
still holding.

## Key-binding rows and captures

**A KEY-BINDING ROW IS A THREE-COLUMN TABLE** (owner ruling 2026-08-23, replacing the
Backspace-captures-the-secondary design): column 0 the action's name, column 1 the primary key,
column 2 the secondary — the game's own `PrimaryKeyBindingField`/`SecondaryKeyBindingField`, so the
columns are a fact of its data. Up/Down walk the name column and read the whole row ("Confirm, Enter,
⟨description⟩, 1 of 41"); Left/Right cross to the keys and the crossing names the column ("Primary
key, Enter, button"), the step back onto the name included ("Action, Confirm, …"). **Enter on the
name cell is inert** — the rebinding lives in the key cells, where Enter captures INTO THAT FIELD and
**Delete empties it** (usage hint "Delete to clear this key", shown only on a cell that holds
something). An empty cell says the mod's word for empty under its own caption, which is what retires
the old row's silence about a missing secondary. The three captions are the mod's own words
(`nav.key-binding-action`/`-primary-column`/`-secondary-column`): the game draws no header band over
these columns. Delete is claimed from the game only while the cursor is on a cell that offers a clear
(`GraphNavigator.TakesClearKey`), and the game binds it to nothing at all (measured 2026-08-23).
**Escape during a capture CLEARS the key, exactly as the game's own Controls tab does** (owner
ruling 2026-08-24, retiring the stage-2b cancel): the engine takes the focus away before the field's
scan sees the press, so the field commits the nothing it blanked itself to — Escape is simply not
bindable, in the game or the mod. The mod adds NOTHING to that ending; the clear goes through the
game's own value path, so Apply lights and Cancel puts it back like any other change, and the
capture-end read-back says what the cell holds now instead of falling silent. A row whose OTHER slot
is empty keeps its key, because the game's own equality check reads the empty commit as no change
(`docs/gui.md`, "Escape during a capture") — the game's asymmetry, matched rather than
papered over.
**A chord the mod and the game both answer to is said, not resolved** (ruling 9): committing either
side's row onto the other's chord raises the game's own informative box — "While the mod's ⟨X⟩ is
active, the game's ⟨Y⟩ will not fire" — with TWO buttons since 2026-08-24: Confirm keeps the new
chord, Cancel puts the row back on what it held before the commit, written through the row's own
option so Apply and Cancel stay in step. Either answer ends by reading the cell out. Mod↔mod
overlaps are not checked at all (ruling 10) and stay silently shadowed.
**A capture that lands on the chord the row is already on still SPEAKS** — the game's own commit
skips silently when the captured combination equals either of the row's slots, so the mod re-reads
the cell at every capture end (`OptionsScreen.SayWhatStuck`); a player cannot tell "same chord"
from "new chord" by silence.
Both windows are this same table: the game's Controls tab and the mod's Controls category are the
same `OptionKeyMappingItem` rows read by the same screen.

## Backspace: the screen's second command

**Backspace (`NodeVtable.OnSecondary`) is NOT a right click** — it is the second command on a node
that folded two of the GAME's controls into one. NO NODE wires one today — the options screen's
key-rebind row was the last, until the binding table replaced it (above) — so what the key still
reaches is the SCREEN-level offer below. Anything the
game itself puts on a right click goes on Backslash, never here: the HUD's notification dismiss was
on Backspace until 2026-08-13 and moved, because the game dismisses on right click
(`NotificationItemsWindow.HandleInput` :90-101).
The key is offered to the SCREEN first (`Screen.Secondary`, taking the focused node — mirroring
`Screen.Contextual`), for a second command that belongs to a PANEL rather than to a control: the
galaxy's way back down the starlanes it has travelled is about where the player has BEEN, and wiring
it per node would mean wiring it onto every node that panel will ever declare. A screen answers only
inside the stop it means it in and leaves every other panel's Backspace exactly as it was. And a LIVE
type-ahead search takes the key ahead of both (below).
The Enter chords pass the PHYSICAL modifier through to the game's handler, which
is how the game's own selection rules apply rather than a copy of them. Which screens have the
chords and which cargo kinds the drag carries (ships, population, both queues) is coverage
status — `docs/test-recipes/README.md`'s per-screen paragraphs own it; a drop always puts the carried
item at the target's own position ("Moved ⟨name⟩ to position ⟨n⟩").

## Usage hints

**A control can END ITS REVIEW BUFFER by naming the chord that works there** (2026-08-21;
`NodeHints`/`ChordNames` — `ES2Access/Core/UI/Graph/NodeHint.cs`, `ES2Access/UI/Input/ChordNames.cs`). A **usage hint** is one short sentence, one per
line, appended by `NodeBuffer` after everything the control itself has to say — so the hints are
always the LAST lines of the buffer, in the order the screen declared them, and nothing about them
reaches the focus readout. The chord in the sentence is never written down: a hint names an ACTION
KEY plus a BINDING INDEX and the sentence is a ModStrings template with a `{0}`, so a re-bound
gesture re-words every hint that names it. **The index is load-bearing, not a default** — the map's
off-lane move is the SECOND binding of the same `ui.contextual` action as the ordinary move
(Ctrl+Backslash beside Backslash, above), and a hint naming the action alone could not tell the two
apart. A hint may carry a possibility gate and is silent while it says no.
**Hints are hand-picked, not a policy**: the uniform right-click-goes-back is deliberately
unhinted, and every context whose own game tooltip already states its gesture stays silent — there
is no runtime dedup, so a new hint is an owner decision about that one context. The declared set,
all owner-approved verbatim: a map target with a fleet selected ("move the fleet here", plus "use
off-lane free movement" only where the selection really can — the pathfinder's own
`FreeMovementSpeed` gate); a starlane with a fleet selected ("deselect the fleet", on Enter, only
where the mod's Enter really deselects); a notification row and a turn-log row ("dismiss"); a
research technology, a constructible and a colony curiosity ("queue it first"); a fleet row and a
ship tile in the fleet lists ("add to the selection" / "select up to here"); a control carrying a
LIVE `GuiButtonHint` ("show missing technology" — declared generically wherever `Cells.Add` or
`CardActions.Emit` wires the jump, plus the troop list's own copy); the military page's fleet row
("show and select fleet"), the empire page's systems row ("open system management screen") and the
load list's row in LOAD mode only ("load"). A table's double-click hint is named by the SCREEN
(`TableSheet.DoubleClickHint`) and sits on the row's primary cell alone, though the gesture works
from every cell of the row: what the second click does is a fact about the row, and repeating it
down eight columns is eight sentences for one affordance.
Where each hint can be heard is the recipe file of the screen that draws it
(`docs/test-recipes/`).

## Place keys, page keys, end turn

**Six keys name a PLACE, one ends the turn and one goes to the next idle fleet** (owner-approved 2026-08-22, the idle-fleet chord 2026-08-28; `ModEntry.BindKeys`,
`ModEntry.HudKey`, `GraphNavigator.FocusStop`): **Ctrl+H** the empire banners (`hud:empire`),
**Ctrl+N** the notifications (`hud:notifications`), **Ctrl+T** the turn log (`hud:turn-log`),
**Ctrl+E** the turn controls (`hud:turn`), **Ctrl+G** the galaxy map's own stop (`galaxy:systems`),
and **Ctrl+Alt+E** ends the turn. Each jump lands where Tab would land in that stop (the remembered
position, else the selected member, else the first control) and announces the landing; each is
claimed from the game **exactly while the focused screen's render declares that stop**
(`KeyGraph.DeclaresStop`, which is `StopLanding` answering non-null asked cheaply enough for a key
scan), **and the handler asks the same question again** — a claim is answered before the press, so
the act is never allowed to run on a stale yes. Where the answer is no the key is inert: no speech,
no move, nothing consumed. Ctrl+Alt+E gates on the game's own three end-turn conditions
(`GlobalHud.CanEndTurn`) and replays the game's SHORTCUT path — an armed cursor put back to
`GalaxyCursor` first, then `EndTurnService.Target.TryToEndTurn()` — rather than pressing the button;
success is silent, exactly as pressing the button is, and a refusal speaks the end-turn NODE's own
readout ("End turn (Ctrl+Alt+E), button, Turn N, unavailable"; the game's reason stays in that
node's buffer), because a player pressing it from the far side of the page cannot see the button
grey out. The keys are free in this game and the letters
cost nothing (`docs/gui.md`).
**Ctrl+Alt+F GOES TO THE NEXT IDLE FLEET** (owner-approved 2026-08-28; `UiActions.NextIdleFleet`,
`GlobalHud.NextIdleFleetByKey`), the turn corner's other every-turn button and the one the game gives
no key of its own: its closed set of ~70 bindings holds no idle-fleet action, and
`EndTurnWindow.NextIdleFleetButton` is a plain button. The chord is free — the game binds bare F to
nothing and Ctrl+F to Search, and its matcher is exact-modifier, so a chord also carrying Alt never
reaches Search. Claimed exactly while the turn stop is declared (`TurnStopDeclared`, the end-turn
chord's own condition) and the handler asks again. The act is the same route the button's own Enter
takes, the galaxy page's single-camera-move version included (`GlobalHud.NextIdleFleet` →
`GalaxyHudScreen.GoToNextIdleFleet`); success is silent and the arrival announces itself, and a
refusal speaks that BUTTON's own readout ("Next idle fleet, button, 0 idle fleets, unavailable, …"),
for the same reason the end-turn refusal does. The button's NAME does not carry the chord: that
costing-every-landing exception stays the five it was granted for.
**Ctrl+Alt+A APPLIES THE MOVEMENTS** (owner-approved 2026-08-28; `UiActions.ApplyMovements`,
`GlobalHud.ApplyMovementsByKey`) — the third of the turn corner's every-turn buttons, and the third
the game gives no key for: its closed set of 70 `InputAction` names holds no apply-movements action
at all (the only one carrying the word is `ForceFreeMovement`, Control+Mouse1). Control+Alt+A is
free — bare A IS bound, but only as the battle camera's own secondary
(`EncounterCameraLeft:LeftArrow,A`), and the matcher is exact-modifier, so a chord carrying Control
and Alt never reaches it. Claimed on `TurnStopDeclared` like the two beside it, the handler asks
again, and the act is simply the button's own click: `EndTurnWindow.OnApplyMovementsCb` posts one
`OrderMoveIdleFleets` and touches no cursor, selection or camera, so the key replays the press
rather than doing anything of its own. Silent on success (the arrivals announce themselves through
the notification watchers), and a refusal reads that button's own node ("Apply movements, button,
unavailable, …"). **The three turn-corner chords are deliberately adjacent** in `BindKeys`, so they
are adjacent rows of the Controls tab too (30, 31, 32 of 60).
**Alt+Left/Right turn the PAGE** (`UiActions.PagePrev/PageNext` → `Screen.PagePrev/PageNext`,
non-repeating): the previous/next system on the star-system page, planet on the planet page,
notification on a popup, hero on the academy page — the game's own arrow pair, pressed from anywhere
on that screen. A screen answers the key wherever the pair is DRAWN, switched off included, and says
nothing at an end (the checkbox-at-a-limit convention); a screen that draws no such pair leaves the
key doing nothing at all. **These chords are also the galaxy inspect cursor's travel keys, and BOTH
actions fire on the press** — `ModInput.Tick` delivers every action whose chord matches and has no
first-wins rule — which is safe only because no screen answers both: the map answers the inspect
pair and draws no page pair. Any future action sharing a chord has to be checked the same way.
The star-system page's pair is also DECLARED, as `system:previous`/`system:next` beside the
system's name in the colony panel, and the ACADEMY strip's pair the same way
(`academy:previous`/`academy:next`, in the heroes stop, owner decision 2026-08-22): the game titles
neither pair and gives the academy's no tooltip at all, so both parts of those names are the mod's
("Previous system" / "Next system", "Previous hero" / "Next hero"), and an arrow the game switches
off at its end of the strip reads unavailable. Where each pair sits in reading order is
`docs/test-recipes/galaxy-map.md`, **Keys**.
**A control may end its NAME with the chord that works on it**, not only its buffer's last line
(`ChordNames.Label`, template `label.with-chord`): the four paging pairs and the end-turn button
carry theirs, because the key is the whole reason the pair was worth declaring twice. It is read on
every landing, which is the cost the owner accepted for these five; hints stay buffer-only.
`ChordNames.KeyName` now asks the GAME for the key's name first (`%KeyCode<Name>`, 120 rows in each
of the ten languages) and falls back to the mod's table and then to the engine's `KeyCode` name.

**Ctrl+L GOES TO WHERE A THING HAPPENED** (owner-approved 2026-08-22; `UiActions.GoToLocation`,
`ModEntry.BindKeys`, `GraphNavigator.GoToLocation`/`TakesGoToLocation`): the game's own show-location
button, from the keyboard. Ctrl+L is free in this game — the input manager binds nothing to L at all,
confirmed live with a physical press that moved neither the camera nor a word — and the letter costs
nothing, since A–Z are already claimed by type-ahead. It is **exactly what the mouse does clicking
that button**, and it is offered exactly where the popup DRAWS one: 41 of the 69 prefabs bind a
show-location button their layout never holds (`docs/notifications.md`), and the key follows the layout, not the
binding. On an OPEN popup the SCREEN answers it (`Screen.GoToLocation`, offered before the focused
node's own — the button is drawn in the bottom bar and the key means it from anywhere on the popup,
which is why that node's name carries the chord: "Show Location (Ctrl+L)"). On a notification-strip
row and a turn-log row the NODE answers it (`NodeVtable.OnGoTo`), doing what the window's own
handler does MINUS its last line, which toggles the popup — from a closed row that toggle would open
the popup instead of going anywhere (`NotificationScreen.GoToLocation`; the five overriding window
families are named there). The landing is the map's own (`GalaxyHudScreen.GoTo`, below). The claim is
`ClaimedWhile` anything on the page offers it and **the handler asks again**; where the answer is no
the key is inert — no speech, no move (measured: `TakesGoToLocation` false on the end-turn button,
the physical press silent, the injected action `unconsumed`). Hint on those rows: **"Ctrl+L goes to
location"** (`hint.go-to-location`), BEFORE "Backslash to dismiss" — going somewhere is what the row
is usually pressed for and throwing it away is what is done afterwards.

## Tree arrows

**Right opens a group AND goes inside it, in ONE press; Left comes out AND shuts it** (owner ruling
2026-08-22, `KeyGraph.TreeRight`/`TreeLeft`): the two presses each direction used to need are one,
so a shut group and an open one answer Right identically — the child is announced with its position
and the header's "expanded" word is never heard, while the "collapsed" word rides the parent's own
announcement on the way out. Left on a header itself is still the plain collapse with the cursor
unmoved. **A group that turns out to be EMPTY is left OPEN** and says "Nothing in here": expanding a
galaxy system brings the camera in, and the auto-recollapse this replaces zoomed straight back out.
Right again on it is a consumed leaf; Left shuts it. `OnFollow` leaves (starlanes) are untouched —
they were already one press.
**Expanding a group that MOVES THE CAMERA announces the zoom line first and then the SETTLED first
child** (owner accepted 2026-08-27; `Screen.BetweenViews`, ≤ 12 frames on the galaxy). The descend
still happens on the press, so the camera follows at once, but nothing is said until the page has
bound its new view and the child is then announced from the build that settled — once, with the
count that build has. So a Right into a galaxy system says "Zoom level 13 of 15, System Overview"
and then its first child, in that order, because the row waits and the zoom watcher does not. On a
page that never moves, the settle is the same frame and the key behaves exactly as above; a group
that has lost every child by then is the "Nothing in here" the press was too early to judge, and any
cursor move in between cancels the pending descend.

**Structural keys are PATHS, and that is load-bearing** (2026-08-20): `KeyGraph.AncestorKeys`
reads a landing's ancestry out of the id's own `/`-separated key, so programmatic landings
(locate, scanner go-to, deferred seats) auto-expand collapsed ancestors one level per build.
Renaming a key's HEAD silently breaks landings into that branch; a non-path key simply gets no
ancestry (as reachable as before). A landing in flight is cancelled by the player's own next move.

## Chat keys

**Ctrl+Tab is the GAME's chat key, not a mod binding**: at startup `GameChatKey` moves
`StartChatting` off Enter/Tab to Ctrl+Tab through the game's own options (ONLY while it still has
the shipped default; a customised binding is left alone), and whatever chord the binding sits on is
handed back through the stand-down (`ModInput.LeaveToGame`) — so re-binding chat in the game's
options keeps working. **Open chat is a PLACE, not a stop**: the key opens the mod's child screen
over the page (`ChatScreen`, in every session including single player — `ES2Access/Screens/ChatScreen.cs`) with the
cursor ON the box's node, NOT typing in it — entering the box is Enter on that node, the edit-field
idiom everywhere else (owner ruling 2026-08-14; the game's own answer to the key, keyboard straight
into the box, is intercepted in the panel's input handler — a mouse click or the new-message button
still types at once, since a pointer asked for the box itself). While the panel is up Tab cycles
inside chat and the page underneath is unreachable, exactly as the panel behaves for a mouse; Up from
the box walks the log newest-first. **While typing, the first Escape steps OUT of the box** into that
page — keyboard back, panel still drawn, cursor on the box's own node — and **Escape from the page
closes**, handing the player back to the control they left (the covered page keeps its own cursor). The panel is drawn
only while somebody is typing in it or a pointer rests on it, so the mod holds it open for as long as
its page is up (`ChatHold`) — which is the one thing that lets chat follow the drawing like every
other surface; declared on the window's existence instead, its controls sat in every page's Tab ring
with nothing on screen (owner-reported, 2026-08-14). The one part left on the pages is the
new-message button, because that is the one part the game draws while chat is closed. Escape on the
chat page is an ordinary `ConsumesBack` claim — a mod-owned child screen the game cannot close — but
the first Escape, the step out, is taken from INSIDE the game's own dispatch (`ChatEscape` prefixes
`InGameChatPanel.HandleInput`), because while the box holds the keyboard the whole mod layer is stood
down and the key never reaches it.

## Enter is click parity; Escape is the game's

**Enter is click parity everywhere.** Every node's Enter is the click the game itself puts on that
control, including the destructive ones — a research queue item dequeues, a construction queue line
cancels (instantly while nothing is invested, behind the GAME's own confirmation box once something
is). There are no mod-invented action menus left; a control's extra buttons are child nodes opened
with right. The one thing that displaces a node's click is a live drag landing on a control that
takes the cargo, which is what makes Enter the drop key — and the other is a targeting cursor: while
the game is waiting for a target, Enter on a map node is that confirm and the node's own click waits,
exactly as the mouse's does — and the fleet nodes select nothing, because a click cannot. Backslash
follows the same rule: while a mode is armed it is the map's own right click for that mode and
nothing else (a cancel for seven of the nine cursors, one waypoint back while a hacking operation is
plotted, the prompt closed for the program picker) — the SCREEN takes the key before the focused
node (`Screen.Contextual`, a new hook: the screen is offered the contextual key first, for a mode
the game imposed on the whole page), so it works anywhere and sending fleets / undoing a zoom wait
for as long as the mode is up. Escape stays the game's except under `TakeSystemCursor`, the one mode
the game left with no Escape route: there the mod claims Escape (`ConsumesBack` true only then on
THIS screen) and
runs that cursor's own right-click cancel, so Escape cannot raise the pause menu over a map still
waiting for a target (owner-ruled deviation, 2026-08-12).

**Escape is the game's — the mod claims it over a surface the mod invented, and over a game window
whose own dismissal control it can press instead.** The second is the 2026-08-28 ruling: seven
game-owned modals (hero inspection, laws, ship design, the academy modal, and the academy, minor-faction
and pirate diplomacy windows) answer `ConsumesBack` with "does this window draw a close control",
and their `Back()` presses that control — so the key is handed straight back to the game through
the gesture it already answers, confirmations and page switches included, rather than being taken
from it (`WindowShape.CloseControl`/`PressClose`; the two handler names are an ES2 fact, `gui.md`).
The negotiation table is the deliberate exception — its close POSTS AN ORDER — and the quest
journal draws no such control, so both leave Escape alone. A screen answers
`ConsumesBack` (asked BEFORE the press), and `ModInput` latches EVERY consumed key until the
player lets go — the rationale for both is `docs/generic/input.md` (the back-key rules and
the liveness self-race law). `ConsumesBack` is NOT a copy of `Back()`: `DropListScreen`
handles Escape and still needs the engine to see it. Probe live with
`ES2Access.Dev.DevProbe.Claims("Escape")` — `claims` true where a mod-owned surface is focused AND
on the seven game windows above whenever they are drawing their close control, the latch shown when
the surface has already gone. That probe, not
`/input ui.back`, is what proves the key does not fall through. It cannot tell a MODIFIED binding
from its plain one (it is asked per `KeyCode`), so a removed chord is proved by `POST /input` with
the action key instead: an unregistered action 400s and lists the ones that exist.

## Type-ahead scope: what a search can reach

**TYPING SEARCHES WHAT THE PAGE WOULD DECLARE, not what it has declared** (owner ruling
2026-08-22; `SearchScope.Extend` + `GraphBuilder.ExpandAll` — `ES2Access/Core/UI/SearchScope.cs`). On every screen,
the first keystroke of a search builds the page a SECOND time with every group forced open and adds
everything that build holds for the focused stop and the standing render does not. That covers both
kinds of hidden content at once, because one build declares both: structural children (a closed
system's planets, lanes and fleets; a collapsed dot's unlocks; a card's action row) and the
"Tooltips" dossiers a node hangs (a planet card's, a technology's unlock dossiers, a population's
parties, the minor gauge's bands). Landing on one opens every branch it is inside, outermost first,
and then goes through the ordinary pending-focus wait, so the branch the player is put into is the
branch they can then walk. The scope is built ONCE per search and kept for its life — never per
keystroke, never per frame (measured 32-78 ms for 131 controls on the galaxy, the biggest tree).
The two screens that already had a scope of their own KEEP it and are merged into rather than
replaced: the research wheel's, because it also searches the game's own corpus of keywords and
unlock names (`docs/research.md`, **Research and the technology wheel**), and the
galaxy's, because its systems and fleets have reveal paths already proven.
A screen with such a scope must supply `SearchScope.IdOf` — the pure "which control is result i",
beside the `Land` that has side effects — or its results are offered twice.
**Stepping to another result closes the branches the PREVIOUS landing opened** (owner ruling
2026-08-23) — only those the SEARCH itself opened, so a branch the player had open before typing is
never touched, and the branch the LAST landing opened stays open when the search ends, because that
is where the player has been left (`GraphNavigator.RevealDeep` / `_searchOpened`). One consequence to
expect: a result only the deep build knew about announces one or two frames after the keystroke
rather than on it.
Adding a tree LEVEL obliges the type-ahead scope to grow a range for the newly-hideable tier
(`HiddenSystem`/`RevealSystem` on the galaxy — a scope that does not loses the tier from search
with nothing in any dump to show it).

## The galaxy map's keys and landings

**Six zoom-in fleet actions seat the cursor where their own name says they will**, and **a targeting
cursor arming ENDS a live-or-suspended inspect mode** (a modal target picker ends it by popping the
page). Which six, what suspends rather than forgets a seat, and the expedition sequence to expect are
`docs/test-recipes/fleets.md`, **Ordering a fleet around**.

**Enter on a NAMED starlane travels when the click would be a structural no-op** (owner ruling
2026-08-20, a deliberate exception to Enter-is-click-parity): `LaneClick` runs
`ConfirmAt` → `Deselect()` → travel, so an armed mode still confirms, a carried fleet is still put
down, and only the case the game's own click answers with nothing borrows Right's travel. A dark
lane stays silent. **Enter on a planet card defers to an armed targeting mode** the same way system
nodes and lanes do (`PlanetClick`: `ConfirmAt(system)` first, else the planet page) — before
2026-08-20 the page opened over the mode and silently discarded it. **The map stop names itself
with the game's targeting instruction while a mode is armed** (`MapContext()`; the label reverts
to "Galactic Map" when the mode ends). Arming while the cursor is inside the stop adds no extra utterance
(the announcer diffs on focus change); Tab-away/Tab-back re-speaks the instruction, which is the
point; child node ids and the cursor survive the rename.

**THE CAMERA FOLLOWS THE CURSOR, BY ONE RULE** (owner ruling 2026-08-23; `GalaxyHudScreen`'s
`Screen.OnFocusVisual` override → `Place` → `FollowPlace` — `ES2Access/Screens/GalaxyHudScreen.cs`):
every focus landing on the map stop resolves to a PLACE and to how close the cursor is to it, and the
camera follows that alone. **Enter is unchanged and Backslash remains the way OUT** — the camera is
never taken back out by the rule, so a zoom-out by hand survives the whole of reading that system's
children. **And a camera moved by anybody else — the game leading the player to a fleet they
selected, a landing sliding across open sky, the inspect cell sweeping — makes the rule's record of
where it sent the camera unbelievable, so the next step inside that place brings the camera back in**
(owner-reported 2026-08-26; the counting, and why the zoom keys are deliberately left out of it, are
in `docs/galaxy-map.md`). The rule, what it folded in and its measured behaviours are
`docs/test-recipes/galaxy-map.md`, **Moving the camera, and the camera rule**.

**A starlane is a LEAF and Right on a named one TRAVELS** (`NodeVtable.OnFollow` →
`KeyGraph.TreeMove.Followed`, consumed silently) — never `ZoomIn`, because travelling is not a click
and must not confirm an armed targeting mode (measured: the mode survives Right and is still ended by
Enter on the same lane); a lane into the dark is a silent leaf. **Backspace pops the trail** while
focus is in `galaxy:systems`, again with no words, and an empty trail is consumed and silent. Where
the trail leads and what it collapses is `docs/test-recipes/galaxy-map.md`, **Travelling the
starlanes**.
**Zoom is an adjustable node** on the existing
Left/Right + Shift chords (no new binding), and it lives on BOTH the scan view and the galaxy's
`hud:view-title` stop, in a row of its own (the view-name label and its close-button node are gone —
owner-ruled 2026-08-18, Escape closes screens); its
coarse step is a LAYER-BAND jump rather than ≈10 increments — an owner-approved deviation, since
ten of the camera's thirteen steps would be the whole range.

**ONE landing on the galaxy page** (owner ruling 2026-08-22; `GalaxyHudScreen.GoTo(MapTarget,
MapCamera)`). Five things used to send the player somewhere on the map — a notification's
show-location, the scanner's Alt+Home, travelling a road, the go-to key, a seat after a fleet action
— and each answered the same three questions for itself: does the free inspect cursor stay up, does
the tree cursor move, does the camera zoom or slide. **The decision table is
`MapLandings.Decide`'s own doc comment** (`ES2Access/Core/UI/MapLanding.cs`), off the engine and
unit-tested; it is not repeated here. Two rules that are the mod's rather than the table's:
a point with NOTHING on it is a DEFECT and not a behaviour — a bare position is matched against what
the map draws with a tight coincidence tolerance (1.5 galaxy units, against a 6.7-unit
closest-neighbour), never with a neighbourhood radius, and never "nearest system" as a stand-in; and
Contested Influence's own Alt+Home still ARMS the inspect cursor, since a square of sky is a thing by
design rather than a lost one.

**The galaxy system readout says whose place it is** (owner ruling 2026-08-22): name, coordinates,
`group`, then the OWNER WORD — the controlling empire as the game's own system dossier states it
(`GuiEmpire.GetLeaderName`, so an empire the player has not met reads `%EmpireUnknownTitle`
"Unknown Empire"), the game's `%MarketplaceScreenNoOwnerTitle` "No owner" where the map shows no
colony the player can see, and NOTHING at all for a system of the player's own — then
`%HomeSystemTitle` "Home System" (trailing space trimmed) on any empire's home system whose owner
the player can see, then the rest as before. The POPULATION FIGURE left the spoken readout: it is
a line of the system's own dossier, which is now a node, and it stays in the review buffer.

## Regions on a stop, and which key crosses them

**A node that owns dossiers beyond its own tooltip is an expandable group with TWO regions**
(owner ruling 2026-08-22, batch 2; `ES2Access/UI/TooltipChildren.cs`). Right/Left are the
ordinary group arrows (today's two-press contract). Alt+Up/Down steps between them:
`<key>/actions` FIRST — the node's own buttons and structural children in the order the surface
draws them (absent where it has none) — and `<key>/tooltips` SECOND, labelled with the mod word
**"Tooltips"** (`node.tooltips-region`) as a `PushContext` level, holding one node per dossier keyed
`<key>/tooltip/<i>`. A dossier node is **named by the game's own header line for that dossier**
(`AgeWidgets.TooltipTitle`), its buffer is the drawn tooltip, and **Enter on it is consumed and
silent** — it wires no `OnActivate`, because there is nothing there to do. Positions count within
the region. Which surfaces declare one, and what each one's dossiers are, is the recipe file of the
screen that draws it (`docs/test-recipes/`).

**Which key crosses a multi-region stop** (measured on the empire HUD band, 2026-08-24): Up/Down
step between the stop's REGIONS (rows) and clamp at the last one; Right/Left walk within the row.
Alt+Up/Down jump regions by name where the stop declares them.

**A block's caption names the block, never a row of its own — unless the caption carries a
tooltip** (owner ruling 2026-08-22). The whole rule, and how to find the widget to ask, is the
`Captions` doc comment (`ES2Access/UI/Captions.cs`); which screens have been
converted and which are knowingly left is `docs/roadmap.md`.

## Inspect-mode keys

**Ctrl+I is the galaxy's INSPECT MODE** — a square of galaxy the player moves about the map and hears
the contents of, instead of walking the tree (`ES2Access/Screens/GalaxyInspect.cs`). Ctrl+I is free in
this game (`InputManager` binds nothing at all to I) and is bound outright, so plain I is suppressed
from the game wherever a mod screen is focused, which costs nothing. While the mode is LIVE it takes
its keys at MODE level, ahead of the review chords and of navigation (`Screen.AnyKey` — the same hook
the cutscene uses, and the same displacement the map already lives with under an armed targeting
cursor): **arrows** move the cell by exactly its own size, **Enter** lands on the one thing in the
cell (silent for none or several), **Escape** leaves ("Exited inspect mode"), **`+`/`-`** grow and
shrink it through 1/3/5/7/9/11 units, **Shift+arrows** go to the next INTERESTING cell and
**Alt+Left/Right** travel by what the cell holds. **The cursor opens at 1 by 1** (owner ruling 2026-08-19), and
at either END of that ladder the size key is consumed and SILENT rather than repeating the size it
could not change — the checkbox/slider refusal convention, applied to a mode's own adjust key. The
size is remembered for the rest of the session, so a re-entry opens at whatever it was last set to.
Every other key falls through untouched, the LETTERS excepted (below). **The cell says its CONTENTS
first and its coordinate LAST** (owner ruling 2026-08-26): the things standing in it, then the lanes
crossing it, then the fog, then the pair — and an empty cell is the bare pair alone. **Typing is INERT
for as long as the mode is live**: A–Z stay claimed from the game, so no letter hotkey of its own
fires, but they start no search — Up/Down belong to the cell, and a search whose results could never
be stepped is worse than none. Arming the mode clears an open search SILENTLY (no "Search cleared"),
and the first letter after the exit starts a fresh one. The knob is `Screen.SuspendsTypeahead`
(**Tab, type-ahead and text editing**), overridden by `GalaxyHudScreen` while `GalaxyInspect.Active`.
**Ctrl+I only ARMS
the mode — it is not a toggle**: pressed again while the cursor is up it is taken and does nothing,
silently, on the same ruling as Enter on an empty cell (the key is pressed speculatively mid-sweep,
and dropping the cursor there would cost the player the cell they were standing on). The three ways
out are Escape, a landing Enter made, and the map going away. **A LIVE TYPE-AHEAD SEARCH TAKES
ESCAPE AHEAD OF THE MODE** (owner ruling 2026-08-19): the first Escape clears the search and leaves
the player IN the mode — the square still drawn, the arrows still the cell's — and the next one
exits. The rule is not the mode's own: `ModEntry.Dispatch` routes Escape past `Screen.AnyKey`
whenever `GraphNavigator.SearchIsActive`, so every mode of that shape obeys it and the innermost
mod-invented surface is always the one Escape ends. The claim never changes hands (`claimsBack`
reads true with either alive, or both). Since the inspect mode suspends typing (above) that pairing
is no longer REACHABLE here — the rule stands for the surfaces that still let a search open over
them, the targeting cursor and the carry. **A MODE OF A WIDGET, NOT OF THE
SCREEN (2026-08-17).** Every sentence above holds while the tree cursor is on the MAP stop
(`GalaxyHudScreen.IsMapStop`); off it the mode is SUSPENDED, not ended — Tab and Shift+Tab walk the
screen's other stops as usual and the stop landed on gets every key exactly as if no cursor were
armed. This is not a nicety: the mod's own zoom slider is two stops above the map and lives on the
arrows, so a mode claiming arrows screen-wide made it unusable. The cell, its size and its drawn
square are all kept while suspended, and coming back to the map reads the cell out again (after a
short wait, so the arriving stop's own announcement is not cut off). ARMING obeys the same rule: Ctrl+I
pressed from another stop is NOT claimed and does nothing at all — no focus move, no arming, no speech
(a jump to the map stop was tried and vetoed, 2026-08-17). **That veto is about a key that ARMS A
MODE, not about moving the cursor**: Ctrl+G, whose whole job is to go to the map stop, is exactly the
key the player would have had to press first, and it arms nothing (2026-08-22). Escape and the size
keys are claimed from the game ONLY while the mode is live AND on the map stop
(`GalaxyInspect.KeysClaimed` → `Active` through
`InputAction.ClaimedWhile`, the Space precedent), which is what leaves the game its own KeypadMinus
(`SleepForThisTurn`) and the battle screens their `EncounterSpeedUp`/`EncounterSlowDown` everywhere
else. `+` is THREE chords — bare Equals, Shift+Equals and KeypadPlus (plus `KeyCode.Plus`) — because
the matcher is exact-modifier and "+" is Shift and the equals key on most layouts. The mode cannot
outlive the page: anything that takes the player off the map ends it, and the line saying so is
spoken from the pump once the arriving page's announcement burst has gone quiet.

**Shift+arrows go to the next INTERESTING cell** (2026-08-19; `ES2Access/Core/UI/CellSkip.cs`): the
same cells the plain arrow walks, in the same steps, stopping at the first one that is not what the
player is standing on — where "what" is the identity of everything the cell's reading names plus a
three-state fog bucket, and never the coordinates. Running off the map lands on the last cell still
on it; a walk with not one step possible says "Map edge", the plain arrow's own refusal. Cells passed
over are counted and said FIRST ("Skipped 12 squares" — the mode's own word, counting CURSOR-sized
cells, owner-ruled 2026-08-19) and only where there were any, ahead of the
landing's ordinary cell reading. The chords are **Shift+Left/Right — the existing coarse-step actions,
taken by the mode rather than double-bound** (a coarse step means nothing on the map stop, and the
zoom slider that chord really adjusts is a stop of its own, where the mode is suspended and keeps its
own coarse step) — plus two new actions on **Shift+Up/Down**, all four repeating.
**Alt+Left/Right travel by what the cell holds**, non-repeating, and each ACTS whenever there is no
ambiguity and is otherwise taken and silent: **Alt+Left** goes to the westmost end of the ONE lane in
the cell — the end the cell's own sentence names first — whether or not fleets are there, since a
fleet has no exposed origin at all; **Alt+Right** goes to where the in-transit fleets in the cell are
going (one fleet, or several agreeing, beats the lane it rides), and falls back to that one lane's
eastmost end where no fleet in the cell offers a destination. The destination is the current leg's
goal alone (`GalaxyHudScreen.DestinationOf`), which is the very thing the tree files an in-transit
fleet under: **no foreign fleet's route is ever read out of sim data the map does not draw** (owner
ruling 2026-08-19), and a fleet whose destination the map does not name contributes no candidate and
blocks none. A lane with one end in the dark has a first end and no second, so Alt+Left travels and
Alt+Right is silent. Every landing is the scanner-style arrival on the target's ROUNDED pair —
camera, square, cell reading — and NO refusal ever exits the mode. The four arrow KeyCodes are
already claimed from the game outright, so none of the six chords needed a new claim (measured with
`DevProbe.Chord`); off the map, and with no cursor up, all six are unconsumed and do nothing.

What the mode SAYS — the constellation and influence crossings it names ahead of the cell, which of
the two is part of cell identity for Shift+arrows, the lane it names only where the fog draws it, and
what a targeting cursor's arming re-reads — is `docs/test-recipes/inspect-and-influence.md`, **What a
crossing says**. The zoom BAND word's silence under the game's scan mode and a focused constellation
node's held label are `docs/test-recipes/galaxy-map.md`.

## Scanner keys

**The galaxy's SCANNER is on the Page keys with a modifier, and it is NOT a mode** — no arm key,
nothing to exit, Escape never touches it; the chords are live for as long as the tree cursor stands
on the MAP stop, alongside tree navigation and alongside the inspect cursor (`ES2Access/Screens/GalaxyScanner.cs`). **Ctrl+PageUp/Down** cycles the category (skipping one with
nothing in it), **Shift+PageUp/Down** the subcategory within it (skipping empties),
**Alt+PageUp/Down** steps one thing at a time and wraps at both ends (the only
repeating chord of the three), and **Alt+Home** goes to what it is pointing at — the inspect cell
onto the thing's ROUNDED spoken pair while that mode is up, otherwise the tree cursor onto the
thing's own node. The FIRST scanner press of a game says where the cursor already is instead of
moving it.
**THIRTEEN categories, in the owner's order (2026-08-22)**, and **Alt+Home moves the camera in every
one of them** (owner decision 2026-08-22) — Contested Influence's alone also arms the inspect cursor
(above). The catalogue, the columns each category writes down for itself, the by-identity
column-and-row memory and what each landing does are `docs/test-recipes/scanner.md`, **Taxonomy v3**
and **Alt+Home**.

**THREE CATEGORIES THE PLAYER MAKES, AFTER THE THIRTEEN** (2026-08-23; moved to the BACK of the
cycle 2026-08-24, `ScannerCustomSlots` → `ES2Access/Core/UI/ScannerCustomSlots.cs`). Three FIXED slots, each empty or
holding `{name, selectors, keywords}`; "delete" is clearing a slot, there are no ids and no reorder.
A configured slot is a category the cycle reaches LAST, in slot order, with the player's own name as
its category name; an unconfigured slot, and a configured one that caught nothing this press, are
skipped by exactly the rule that skips a built-in category with nothing in it. They are LAST and not
first because the cursor starts at category zero and the first scanner press of a game says where the
cursor already stands: in front, a player who had configured nothing heard "none found" as the first
thing the scanner ever said to them. **The slot rows are always in the table**,
which is what keeps every built-in category's index — and therefore the cursor's per-category
memory — the same whatever the player does to their slots. Its columns are "all", then one per
SELECTOR in the order they were added, then one per KEYWORD; each selector column is named with
BOTH halves ("Systems: enemy", "Anomalies: Multiple Moons") because two selectors that both say
"all" are two different columns to the player and to the cursor's name memory. A selector is a pair
of STABLE KEYS (`ScannerKeys`: `systems`/`fleets`/`anomalies`… and `all`/`friendly`/`enemy`/
`unoccupied`/`explorable`/`low-power`…, or — for one of the four derived categories' kind columns —
the GAME's own definition name, `PlanetAnomaly27`, `Luxury5`, `CuriosityTypeGuardian`), so it
survives a language change and a galaxy with no such column; a selector this galaxy cannot answer
is SKIPPED for that press and stays stored. A KEYWORD matches a result's name, its kind and its
composed detail through the type-ahead's own tiered, diacritic-insensitive match — which is why a
colonizable world's description is now composed for every world up front rather than for the row
being read (measured 2026-08-23: 4–7 ms a press either way). "all" hears each result ONCE however
many of the player's questions caught it, and says it as the column it came from would in its own
"all" ("Multiple Moons on Dusay II"); a selector's own column says what that built-in column says.
Nothing custom reaches the type-ahead search, which searches the graph's nodes and never the
scanner's results.

**The six quick keys: `,` `.` `/` and Shift+each** (owner-approved 2026-08-23; actions
`galaxy.scanCustom1Next`…`3Prev`, so all six are rebindable rows of the Controls tab). One key per
SLOT, not per category name — the key means the same thing whatever the player renames. A press
walks that slot's "all" list FLAT, nearest first from where the player is reading, SAYS the landing
as a scanner result would ("Rigel, -16, -5, 3 south, 1 of 21") and then performs the category's
ordinary Alt+Home landing, so the tree cursor and the camera go there and the page announces the
arrival after it. **THE SWEEP KEEPS THE LIST IT STARTED WITH** (2026-08-24): the order is taken
nearest-first when the sweep begins and then FROZEN, so press after press walks 1, 2, 3 … n and wraps
at both ends. The sweep ends only when the PLAYER moves, and the walk is therefore anchored on the
rounded pair its own landing is TAKING them to and not on where the press was made from.
Two things the
map reads out at the same coordinates are one place here, so stepping between the planets of one
system does not end a sweep. A press while parked on the entry it is standing on steps ON rather than
re-landing (`ScannerWalk`). Shift walks the same frozen list backwards — from a landing, back the way
it came; from a fresh press, to the FARTHEST result, the nearest-first ordering read backwards,
songs-of-conquest-access's own rule. An EMPTY slot says "No custom category on {key}", naming the key off the LIVE
binding, and is never silent; a configured slot that caught nothing says the ordinary
"{category}: all, none found". The six are keys of the MAP WIDGET like the rest of the scanner's
(`GalaxyScanner.QuickKeysClaimed` → `Active`) and need no physical-modifier trick: the game binds
nothing to those three keys and the mod's type-ahead takes letters and space and never punctuation.
Not repeating — every press is a jump.

**Bare PageUp/PageDown remain the GAME's keyboard zoom**, Ctrl+Home stays the review
buffer's first line and plain Home/End the stop's ends.
**KEYS OF THE MAP WIDGET, like the inspect cursor's (2026-08-17).** Every chord above acts only
while the focused stop IS the map (`GalaxyHudScreen.CursorOnMap`, over `IsMapStop`); on the zoom
slider, the HUD buttons, the view title and every other screen they are unclaimed AND unconsumed —
no speech, no step, nothing. The gate is asked in `GalaxyScanner.HandleKey` as well as in the claim,
because an unclaimed key still reaches `Screen.AnyKey`. Leaving the map SUSPENDS the keys and resets
nothing: the parked scope, the per-category memory and the armed flag are all still there on the way
back, so the next press resumes the sweep rather than re-announcing where it stood. (Corrected: the
chords used to be live wherever the galaxy page was focused, which stepped the list from the HUD
button strip.)
The claim is what makes the bare press work and it is a NEW SHAPE: the three chords are claimed only
while the map stop is focused AND a modifier is PHYSICALLY held (`GalaxyScanner.KeysClaimed` →
`Active`, through `InputAction.ClaimedWhile`). The camera's own matcher reads its binding's key codes and ignores its
modifiers (`GalaxyViewCameraController.IsInputKeyCombinationPressed`), so a plain claim on PageUp
would take the bare press too — and `ModInput.LeaveToGame` cannot help, because the modifiers the
stand-down reads come off the combination the GAME is asking about, which declares none either way.
The physical modifier is the only thing that separates the two presses. `DevProbe.Chord` cannot
prove this half (it holds no key); what it proves is the important half — with no modifier held,
`Claims("PageUp")` reads `claims:false` on the galaxy page.

## Tab, type-ahead and text editing

**Tab and Shift+Tab wrap** (owner decision 2026-08-12): the last stop's Tab lands on the first,
the first stop's Shift+Tab on the last. On a page with exactly ONE stop the key is consumed and
says nothing — coming round to the panel the player is already on is not a move. **A screen where
every key means one thing** answers `Screen.AnyKey` before the review chords and before
navigation: the cutscene's arrows, Tab, Enter and review chords all become the game's own
press-anything skip, because a claimed key is invisible to the game's binding matcher and would
otherwise do nothing at all. Escape is never offered to it and stays the game's.

Backslash and every Enter chord are claimed on every mod screen. **Backslash and Ctrl+Alt+Enter are
SILENT where the control has no such command** — pressed speculatively all over a page, a cue on
each is noise; silent but consumed, never a fall back. Ctrl+Shift+Enter, Ctrl+Enter and Shift+Enter DO fall
back to the control's plain click (above), and are silent-and-consumed only where the control has no
click either. Space while something is carried is the same: consumed on a control that will not take
it, silent, carry kept.
**Space is claimed only where it can act** — the focused control has something to pick up,
something is already being carried, or a live type-ahead search is taking the space as text
(`ModEntry.CarryKeyClaimed` → `GraphNavigator.TakesCarryKey`, through `InputAction.ClaimedWhile`;
owner decision 2026-08-12, reversing the blanket claim of 2026-08-11). Everywhere else it falls
through to the game, whose Space is the strategic lens (`ToggleScanView`) — modelled now by
`ScanViewScreen`, which announces the lens on arrival, again at every layer-descriptor change while
the mode is up (same-name band boundaries included — `docs/galaxy-map.md`), and the view again on the way out,
so handing the key back cannot drop the player into an unannounced mode. Every other binding is
claimed outright.
**THE STAR-SYSTEM PAGE IS THE ONE PLACE SPACE NEVER FALLS THROUGH** (owner ruling 2026-08-26;
`SystemManagementScreen.SwallowsCarryKey`, asked by `ModEntry.CarryKeyClaimed` beside the ordinary
carry claim and again by `ModEntry.SwallowedCarry` before the dispatch swallows the press). There the
game's Space is a SCREEN-LEVEL shortcut rather than a mode of its own — the scan button in
`hud:view-title` names it, "Shortcut: Space or Mouse 3" — and a player pressing Space on a planet
card or a queue line means pick-this-up, so a whole different view arriving instead is not an
outcome that row offered. The key is therefore the mod's on every node of that page: a row with
something to pick up carries exactly as before, every other press is consumed and SILENT (the same
no-cue rule as a carry landing on a control that will not take it), and scan mode stays one Enter
away on the button the game draws for it. Scoped to that page alone — the scan view over it
(layer 11), the galaxy and every modal the page opens keep Space as the game's, measured before and
after. The PLANET-OVERVIEW page deliberately keeps the fall-through (owner ruling 2026-08-27): it
draws the same scan button with the same shortcut, but no node there offers a pickup, so the mod has
no meaning for Space on that page and the game's is left standing — do not extend the swallow there.
The silence of a consumed press on a slot with nowhere to send its unit is also a ruling
(2026-08-27, silent refusal chosen over a spoken phrase), not an oversight. While something is carried, **Escape puts it
down and goes no further** (`claimsBack` reads true only then), and the carry dies silently when the
player leaves the page it started on — a menu opened over that page is still that page.

**Typing a letter searches the focused stop** (no search key: the first printable character starts
one; Up/Down step the matches, Home/End their ends, Escape clears it and goes no further — ahead of
every other Escape the mod claims, a mode of the mod's own included (owner ruling 2026-08-19: the
search is the innermost surface, so one Escape never ends two of them), **Backspace
does exactly what Escape does** — ends the search, "Search cleared", and goes no further, so it never
also runs the page's own second command on the match it landed on; any other action ends it and then
does its own job). Backspace is the way OUT of a search rather than an editor for it: a search is
re-typed in a keystroke, and one gesture is worth more than character editing (owner decision
2026-08-14). So **A–Z are claimed from the game on every mod screen**
(`GraphNavigator.TakesTypedKey` via `ModInput.ClaimsTypedKey`, asked before the press), and a
space typed into a LIVE search is text — the carry key takes it for the search (a live search
is one of the three conditions of Space's claim, above). Screens opt out with `AllowsTypeahead`
(the key-rebind capture rows, and the cutscene where letters belong to the game's skip) or
`CapturesRawInput` (the frames between asking for a key capture / text editor and the
game taking the keyboard). The narrower knob beside them is `Screen.SuspendsTypeahead`, which keeps
the letters CLAIMED and makes them do nothing — as against `AllowsTypeahead`, which hands them back
to the game. It is for a mode that has taken Up/Down for itself, where a search could be started but
its results could never be stepped: the typed keys are drained, an open search is cleared silently on
the way in, and the game still never sees a letter. Today only `GalaxyHudScreen`, while
`GalaxyInspect.Active` (**Inspect-mode keys**). Edit fields are entered explicitly and share ONE editor
(`TextFieldEditor`): Enter on the field hands the keyboard over ("editing"); typing echoes
each character and Backspace speaks the deleted one; caret moves (arrows/Home/End) speak the
character under the caret; Enter commits ("edited") and the SURFACE STAYS — the mod takes
the key from the game's validate dispatch, so committing an edit never performs the
screen's action (saving, renaming are the Save/Confirm buttons; a game that will not take
a value refuses at its own button); Escape — or ANY loss of the keyboard
that is not Return: a click elsewhere, a right click — cancels, restoring the pre-edit text
and saying "Cancelled", before a second Escape closes the surface. The chat box is the one
exemption: its Enter sends, through the game. **A FOCUSED edit field never adjusts its value on
Left/Right — the arrows navigate, and the number is changed by opening the edit and typing it**
(owner ruling 2026-08-27: "left / right when my focus is on it increments it, even though this should
only happen when I'm editing it"). That covers every numeric editable — the negotiation basket's
quantity, the marketplace's tradable-item quantity and its tax rate — none of which is wired to the
game's own plus/minus buttons any more; where the game draws those buttons they are declared as
their own nodes, so the stepper is still one keypress away. A SLIDER is the exception and keeps
arrow-adjust: it has no edit to go inside, and the arrows are the only gesture it has. Role words: "editable",
and "numeric editable" for the boxes a NUMBER is typed into rather than free text — the word says
which kind of thing the field takes, and no longer promises an arrow step
(owner rulings 2026-08-17 and 2026-08-27; the cancel-restore is wholly mod-authored — the engine has no
cancel semantics of its own, and the hand-over waits for the activating key's release).

## The mod's Controls tab

**The Controls category holds one row per mod ACTION**, in the order `ModEntry.BindKeys`
registers them — which groups them by family already: the cursor's keys, then the map's, then
the review buffer's. Each row is the game's own `OptionKeyMappingItem`, so it reads exactly as a
Controls-tab row does, read as a three-column TABLE (the action, its primary key, its secondary
key) with Enter on a key cell capturing that cell and Delete emptying it. Every row's
title and description are the mod's own strings (`action.<action key>.title` /
`.description`) — mandatory, not cosmetic: a localization key the game has no row for is drawn
and spoken RAW (`docs/gui.md`). An action bound to more than two chords keeps the extras, which stay
live and are not offered by any row (today only `galaxy.inspectGrow`, four chords because "+"
is three of them on a common keyboard). **No mod↔mod conflict check** (ruling 10): the rows
carry `AcceptsMultipleKeys`, which also stops the game's Controls tab stealing a chord from one
of them. The mod-versus-game informative warning (ruling 9) is above. **The TAB itself says
nothing of the mod's own**: its title and its tooltip are the game's `%OptionToggleControlsTitle`
and `%OptionToggleControlsDescription` ("Set key bindings"), so the page reads as the game's own
Controls page in every language (owner ruling 2026-08-24) — which page the player is on is the
window's own name, "Mod settings".

**A rebind persists on Apply and reverts on Cancel** — the window's own semantics, with no hook
on either button: the settings file is written when the window HIDES, by which point Apply has
committed or Cancel has restored (`ES2Access/UI/Settings/ModSettings.cs`). The file
(`<plugin dir>\settings.cfg`) holds one `keys.<action key>` line per action the player has
actually MOVED, in the game's own `InputBinding.ToRegistryString` form; moving a key back to its
default takes the line out again, so a later build changing a default reaches everybody who
never touched that key.

The rest of the mod's settings window — how a player reaches it, the two tabs, the Scanner tab's
slot editor — is `docs/test-recipes/mod-settings.md`.

Game-mechanism findings (window gates, pool slots, tooltip internals, fleet and quest
mechanics, the icon numbers) live in the game-facts topic files ([README.md](README.md)) — a new
fact lands there, never here.

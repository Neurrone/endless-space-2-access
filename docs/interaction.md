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
and took the systems, the star lanes and the HUD out of Tab. It is contributed by the galaxy page
alone, which is complete rather than a gap: entering a system swaps the cursor to
`StarSystemCursor` and the game hides the window outright (measured — `docs/fleets.md`).

## The key map

**Ctrl and Alt in this file name the mod's FIRST and SECOND chord modifiers, not two fixed
keys.** On Windows they are Control and Alt; on macOS they are held as Option and Command
(every `Ctrl+X` below is `Option+X` there, every `Alt+X` is `Cmd+X`, letters unchanged),
because Control+arrows belong to the macOS desktop and Control+Option is VoiceOver's own
modifier. The one place the choice is made is `KeyboardBinding` (its doc comment carries the
full reasoning; the conflict scan and the player-facing spelling are `macos/README.md`). The
game's own Control bindings stay on real Control on both systems.

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
**Space** pick up / swap what is being dragged (`OnPickUp`) — **never a put-back**: pressed again on
the control the thing came from it picks it up AGAIN, because a source can hand over different
amounts of the same thing (a population marker carries itself and every marker of the same people
after it) and a cancel there would throw the drag away instead of re-sizing it; the back key is the
only cancel (owner ruling 2026-08-29). The pick-up says what is now held and BOTH ways out
("Dragging Imperials x 3. Enter to drop, Escape to cancel."), with the chords rendered from the live
action table like any hint, and **every draggable control ends its buffer with a derived hint** —
"⟨carry⟩ to drag ⟨thing⟩." while nothing is held, "⟨activate⟩ to drop ⟨thing⟩." on a
target that would take what is (`CarryState.HintLines`, composed by `NodeBuffer` after the
hand-picked hints; no screen wires either) —
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
does either (bug 17). The Mac player has no such toggle: Option+Enter reaches the mod
untouched (owner's hands, 2026-08-31), so the macOS chords carry no equivalent constraint. The gesture moved to Ctrl+Shift+Enter, which the game binds nothing to and
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
silent, as the mouse does there. **Off a table, one screen wires it by hand**: the advanced battle
setup's ship rows, where the game's second click on the ship's chip in the 3D arena is what pins it
to its flotilla, and the chip's SINGLE click does nothing at all (owner ruling 2026-08-29, reversing
the same day's ruling that had put the pin on Enter). The row keeps Enter for the DROP - it is a
target for a ship being carried - so the two gestures no longer collide, and the row's buffer names
the chord (**Usage hints**, above), because nothing about a roster line suggests a double click. The save list's second click ACTS rather than shows — it loads the
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
galaxy's way back from every leap it has taken is about where the player has BEEN, and wiring
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
`FreeMovementSpeed` gate); a star lane with a fleet selected ("deselect the fleet", on Enter, only
where the mod's Enter really deselects); a notification row and a turn-log row ("dismiss"); a
research technology, a constructible and a colony curiosity ("queue it first"); a fleet row and a
ship tile in the fleet lists ("add to the selection" / "select up to here"); a control carrying a
LIVE `GuiButtonHint` ("show missing technology" — declared generically wherever `Cells.Add` or
`CardActions.Emit` wires the jump, plus the troop list's own copy); the military page's fleet row
("show and select fleet"), the empire page's systems row ("open system management screen"), the
load list's row in LOAD mode only ("load") and the advanced battle setup's ship rows ("lock or
unlock this ship in its flotilla", naming the DOUBLE-CLICK chord — the one place in the mod where a
hint names that gesture, because a roster line suggests nothing about it and the game says the same
thing about its own chips on a button the player may never stand on). A table's double-click hint is named by the SCREEN
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
are adjacent rows of the Controls tab too (the table is 81 rows as of 2026-09-02).
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
Right again on it is a consumed leaf; Left shuts it. `OnFollow` leaves (star lanes) are untouched —
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
`StartChatting` off Enter/Tab to Ctrl+Tab — Option+Tab on a Mac, the same chord in the mod's
modifier space (the registry's Ctrl is REAL Control there, which the mod's bare Tab fires under) —
through the game's own options (ONLY while it still has
the shipped default; a customised binding is left alone), and whatever chord the binding sits on is
handed back through the stand-down (`ModInput.LeaveToGame`), translated into the mod's chord space
by `KeyChords.FromCombination` — so re-binding chat in the game's
options keeps working, a Cmd chord included. **Open chat is a PLACE, not a stop**: the key opens the mod's child screen
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

**Nine zoom-in fleet actions seat the cursor where their own name says they will**, and **a targeting
cursor arming ENDS a live-or-suspended inspect mode** (a modal target picker ends it by popping the
page). Which nine, what suspends rather than forgets a seat, and the expedition sequence to expect are
`docs/test-recipes/fleets.md`, **Ordering a fleet around**.

**A camera move made by the POINTER seats the cursor too, through the arrival seat's own rule** — a
left click on a star or a wreck, and the wheel scrolled in past the deepest step (owner ruling
2026-08-29), put the cursor on that system's row while it is reading the map, and re-seat the map
stop's remembered row silently while it is reading anything else (`GalaxyPick` →
`GalaxyHudScreen.ArmPickSeat`; the mechanism and what the right-click undo does are
`docs/galaxy-map.md`). No key of the player's is involved and nothing new is claimed.

**Enter on a NAMED star lane travels when the click would be a structural no-op** (owner ruling
2026-08-20, a deliberate exception to Enter-is-click-parity): `LaneClick` runs
`ConfirmAt` → `Deselect()` → travel, so an armed mode still confirms, a carried fleet is still put
down, and only the case the game's own click answers with nothing borrows Right's travel. A dark
lane stays silent. **Enter on a planet card defers to an armed targeting mode** the same way system
nodes and lanes do (`PlanetClick`: `ConfirmAt(system)` first, else the planet page) — before
2026-08-20 the page opened over the mode and silently discarded it. **The map stop names itself
with the game's targeting instruction while a mode is armed** (`MapContext()`; the label reverts
to "Galactic Map (Ctrl+G)" when the mode ends — the focus chord rides on the map's own name only,
never on the game's instruction). Arming while the cursor is inside the stop adds no extra utterance
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

**A star lane is a LEAF and Right on a named one TRAVELS** (`NodeVtable.OnFollow` →
`KeyGraph.TreeMove.Followed`, consumed silently) — never `ZoomIn`, because travelling is not a click
and must not confirm an armed targeting mode (measured: the mode survives Right and is still ended by
Enter on the same lane); a lane into the dark is a silent leaf. **Backspace pops the trail** while
focus is in `galaxy:systems`, again with no words, and an empty trail is consumed and silent. Where
the trail leads and what it collapses is `docs/test-recipes/galaxy-map.md`, **Travelling the
star lanes**.

**BACKSPACE RETURNS FROM EVERY LEAP, NOT ONLY FROM A LANE** (owner-approved 2026-08-31;
`GalaxyHudScreen.NoteLeap`). A bookmark jump, the home jump and the scanner's go-to throw the player
across the galaxy in one press, and there is no walk back through the tree from where they land — so
the way back is remembered on the SAME trail the lanes use, in the order things happened, and
Backspace undoes the most recent leap whichever kind it is. One trail and not two, because "take me
back" is a question about the last place left and never about which key left it. A lane hop restores
its lane node exactly as before; a jump restores **the exact row the cursor was on, at whatever
depth** — a planet, a lane, a fleet, a dossier — because that is where the player was, and coming
back to the system instead would be a different place from the one they left. **ONE INVALIDATION RULE
FOR THE WHOLE TRAIL** (owner ruling 2026-08-31, overturning an earlier fall-back-to-the-system's-row):
an entry that can no longer be honoured EXACTLY is dropped and the pop carries on to the one before
it, exactly as a lane hop into the fog already was; a trail with none left does nothing. A leap is a
promise to put the player back on the row they were standing on, and landing on that row's system
instead would report success while restoring nothing they meant — the case that settled it is a leap
made from an in-transit fleet's row, where the fleet has since arrived and the tree files it
somewhere else entirely, so the old system is not even where the thing is. What is NOT invalid is a
row inside a branch the player has since SHUT: the landing opens its ancestors on the way, so that
entry stands (`GalaxyHudScreen.LeapStands` tells the two apart by asking whether the deepest ancestor
the tree still declares is open). **A jump made from anywhere but the map stop remembers nothing at all**: Backspace is
claimed only while focus is in `galaxy:systems`, and a trail entry pushed from the notifications
would be a way back that could only ever pull the player's focus off the panel they were reading.
The restore goes through the page's one landing (`GoTo`) like everything else that sends the player
somewhere.

**THE INSPECT CELL KEEPS ITS OWN STACK, AND THE TWO NEVER MIX** (same ruling; `GalaxyInspect`). While
the cell is live Backspace is the CELL's way back — the mode takes the key ahead of the page, so the
tree's trail is for inspect-off alone. What goes on it is leaps only: a bookmark jump, the home jump,
the scanner's go-to (a row target or a square alike) and the two Alt+arrow travel keys, which cross
a lane's length in one press. The plain arrows and the Shift+arrow skips push NOTHING — they are how
the player moves, and a stack recording every step would be an undo history of a sweep rather than a
way back from a jump. Popping teleports the cell back and the cell's own reading is the announcement,
with the zoom untouched like everything else under the cell. The stack lives and dies with the mode
instance — arming clears it, leaving clears it — and an empty one is consumed and silent, exactly as
the empty tree trail is. A jump made while the mode is PARKED still pushes its cell: Backspace is not
reachable from another stop, but the landing brings the player back to the map and it must be there.
**Zoom is an adjustable node** on the existing
Left/Right + Shift chords (no new binding), and it lives on BOTH the scan view and the
`hud:view-title` stop, in a row of its own (the view-name label and its close-button node are gone —
owner-ruled 2026-08-18, Escape closes screens); its
coarse step is a LAYER-BAND jump rather than ≈10 increments — an owner-approved deviation, since
ten of the camera's thirteen steps would be the whole range. **A system's page and a planet's are
rungs 14 and 15**, so the ladder stands in their `hud:view-title` stop too (before the scan button,
same key, no band word there): Left steps out of the page, Right opens the planet, and at 15 a
refused Right re-reads the unchanged value rather than falling silent, because the value is still
readable — the silent-refusal convention only covers a control whose reading goes away.

**A ladder step that changes the page lands on the arriving page's ladder** (13↔14↔15, and the scan
view's own pair), whatever that page remembers and whatever it would otherwise open on: the player
who stepped off a ladder arrives standing on one, and the new rung is read once, by the arriving
slider's own value line after the page has named itself. Only a step made ON a ladder hands the seat
over — every other way into these pages (Enter on a system's "Manage system" or a planet's card, the
mouse, Escape or the close button back out, the page arrows) lands exactly where it always did, and
the rung watcher still announces those, since the player was not on the ladder. A refused step (the
clamp at either end) hands nothing over.

**THE BAND WORD NAMES WHAT THE LEVEL GIVES** (owner ruling 2026-09-01, replacing the engine-jargon
words). Five words over the camera's ladder, at the game's own layer boundaries and no others:
levels 1–2 **Constellations**, 3–4 **Systems and star lanes**, 5–6 **Systems, star lanes and
fleets**, 7–12 **System details**, 13 **Orbital**. Level 1 was SILENT before (its `PaintingLayer`
descriptor was unmapped) and now speaks like the rest; the two layers that offer the same thing
(`SystemsLayer` 7–10, `SystemLayer` 11–12) share one word, so the cadence is still boundary-only.
Same wording from the slider's own value and from the watcher, as before. Scan mode still drops the
band word and says the lens.

**THE LADDER CARRIES A USAGE HINT** (owner ruling 2026-09-01), because the game's own zoom is two
keys HELD and nothing about this page suggests an adjustable answers the arrows. A hint line in the
ladder's review buffer, composed from the LIVE bindings like every other hint, naming BOTH gestures
at once — an adjustable worked from one end is half a control — and saying what the gesture buys on
the page it is standing on. **The chords it names are the COARSE pair, Shift+Left/Right** (owner
ruling 2026-09-01, after playtest, replacing the plain arrows): one rung usually changes nothing the
player can hear, because the band word and the lens name only move at a boundary, so a hint naming
the fine step pointed at a key that mostly does nothing. The two sentences are
`{0} or {1} to change detail level` on the map (`hint.change-detail-level`) and
`{0} or {1} to change lens` under a scan lens. `NodeHint` takes an optional second action for this,
and a pair sentence with only one half renderable contributes nothing.

**A STAR LANE ANNOUNCES ITSELF AS A BUTTON** (owner ruling 2026-09-01), in both views: Enter on a
lane has gone down it since the travel keys landed, so the row's role says so. Nothing else about
the lane moves.

**A LANE IS SAID AS A DIRECTION AND A FAR END, AND NO LANE NUMBER IS EVER SPOKEN** (owner ruling
2026-09-02, both views): a lane row reads "northeast to Leo" — "by wormhole" on the end where it is
one, "to an unexplored system" where the map has not named the far end — because the rows already
sit under the system's **Star lanes** region, which says the words once for all of them. A fleet
under way, hanging under the system it is arriving at, reads "arriving at Rigel from Dusay by star
lane this turn" / "…by wormhole" — and where the map has not named that far end, "arriving at Rigel
from an unexplored system to the ⟨direction⟩ by star lane …", which way still being the compass word
that system's own lane row says for the same line, so an unnamed origin is never two incoming lanes
said identically (owner ruling 2026-09-02); a fleet crossing open space reads "arriving at Rigel from
the west …", the eight-word bearing from that system out to where the fleet is standing. The
clockwise-from-north ordinal survives only
as the internal ordering (`LanesOf`). The scanner's Unexplored rows keep their own sentence, "Star
lane from Dusay heading north", and so does the INSPECT CELL, "Star lane from ⟨west⟩ to ⟨east⟩" —
westmost end first and no compass word at all (owner ruling 2026-09-02, reverting a day-old change):
a cell reads the lanes crossing it, and westmost-first is what makes one lane crossing two
neighbouring cells heard as one lane.

**A FLEET UNDER WAY SAYS WHEN IT GETS HERE, AND WHERE IT GOES NEXT** (owner rulings 2026-09-02;
`FleetRows.AddEnRoute`/`AddFreeMoving`). The turn in the arriving sentence is the turn the fleet's
route reaches THE HOSTING SYSTEM on, counting the turn in progress as one — not the turn the journey
ends on, which is a different number whenever the fleet is passing through. Where the journey does
carry on past this system, a second phrase follows the composition: "en route to Dusay in 2 turns",
"…this turn", or "en route to an unexplored system …" for a destination the map has not named;
where the destination IS the hosting system there is no second phrase, because the arriving sentence
already said it. **"Moving to X" leaves both row kinds** — it named the journey's end while the
sentence above it named the leg — and docked rows and the scanner's fleet results keep it unchanged.

**NEVER MORE THAN A SIGHTED PLAYER SEES**: both the turn count and the "en route" phrase are said
only where the game would DRAW that fleet's path — own fleets, and foreign ones the empire has
earned the sight of (`FleetRoute.RouteShown`, and `FleetRoute.Current` is the gate every caller
passes through). A foreign fleet otherwise says the arriving phrase with no turn clause and nothing
about its destination; a pirate under Sabel reads "arriving at Sabel from an unexplored system to
the southwest by star lane, neutral Amoeba, Pincer, 0 movement points" and no more.

**THE TREE IS BAND-FILTERED, AND SO IS THE SCANNER** (owner ruling 2026-09-01; the table is
`Core/UI/Bands.cs`, read through `UI/ZoomBands.cs`). The rows the map stop offers are the KINDS the
picture is drawing at that distance — camera-position-independent, per kind, never per frustum:
1–2 constellation groups alone (closed, since nothing inside them is drawn); 3–4 adds system rows
with their lane children; 5–6 adds fleets; 7+ adds everything else (planets, deposits and the other
label dossiers, the manage-system button, quest pins, hangars, probe directions, and the probes,
missiles and pins out in open space). A system's spoken fleet counts go with the fleet rows, so the
number it says and the children it opens onto stay one answer. The scanner's category ring skips a
band-hidden category by the same rule it skips an empty one, and a custom slot's selectors and
keywords see only band-declared rows. **At levels 1–2 every category is hidden and a scanner press
answers the existing none-found line** ("Luxury Resources: all, none found" — whichever category the
cursor was parked in), never silence.

**THE GALAXY PAGE WEARS THE SCAN LENS, AND THE MAP THEN GROUPS BY OWNER** (owner ruling 2026-09-01;
`GalaxyHudScreen.IsActive`/`Scanning`, `Screens/Galaxy/ScanRows.cs`, `ScanLensPanels`). Scan view is
the same map under a different light, so the page that models the map keeps the keyboard in-mode and
the inspect cursor, the scanner, the bookmarks, the type-ahead and the map summary are the same code
paths — no second screen, no second tree. `GalaxyHudScreen` is active at the galaxy overview in
EITHER view (the game's two flags are exact complements outside a battle or a cinematic), minus the
frames a battle screen is still fading off; `ScanViewScreen` keeps the two rungs the galaxy page does
not reach, the system-management and planet lenses. **Stops in-mode**: `scan:title` (the lens's own
strip, then the zoom ladder — the game hides the panel the ordinary view-title cluster is read off),
the map stop, `scan:system` at the System lens (the panel it draws over one star — its regions and
its wording are `docs/test-recipes/galaxy-map.md`), `scan:legend` (named **Captions**, the mod's own
word — the panel has no heading and its tick carries only the game's "Caption"), and the turn
controls; the
banners, the pinned quest, the notification
strip and the fleet panel are all things the game stops drawing. The lens's ARRIVAL gate — a lens has
drawn itself — governs that furniture and never the page, because keeping the cursor across the mode
change is the point; entering says the lens once and leaving says the map, the pair a pushed and
popped screen used to say. **The tree**, from the band that names the systems: one heading per
empire, ordered by the centre the watching empire's intelligence knows
(`DepartmentOfIntelligence.GetEmpirePosition`, `Known`-gated, in the constellations' own reading
order; an empire with no known centre sorts after those that have one), then minor factions, pirates,
No owner and **Unexplored** — a "???" system's owner is unknown rather than none — with empty
headings undeclared and no constellations at all. Rows read from the MODEL, so focusing one gives
what hovering the label would (FOCUS IS HOVER, ruled: the lens paints some label content only for the
systems it thinks important). Children: the same named regions the ordinary row is read in, three of
them - Planets (dots), Star lanes, and Status (the scan label's own picture nodes).
Absent throughout: fleets, probes, missiles, pins, quest markers, deposits, docks, hangars, wrecks.
**The headings are not in the stars' keys** — every row the cursor could be standing on keeps the key
it has in the ordinary view, which is what makes the mode change cost the cursor nothing either way.
**And a landing still opens a heading the player has shut** (owner ruling 2026-09-01, after playtest;
`ScanRows.NoteGrouping` → `KeyGraph.GroupingAncestor`): landings read their ancestry out of the
target's key, so the page NAMES this extra level to the engine instead, and the lane hop, the
scanner's go-to, a bookmark jump, a type-ahead result, a restored leap and the reconciliation of a
dead row all open it one level per build, exactly as they open a shut constellation in the ordinary
view. Before it, following a lane into a shut "No owner" did nothing at all — no move, no word.
**Nothing reads the
camera**: expansion is in place at every lens and a landing slides and seats (`EnsureBand` and
`FollowPlace`'s inside-snap both stand down in-mode), because under a lens the rung IS the lens and a
silent zoom would change what the whole screen means. The scanner filters by the same table
(`Bands.Scans`): Systems / Colonizable Planets / Unexplored at Trade, the same plus Contested at
Economy, Systems / Unexplored at the System lens, nothing at Diplomacy — **Curiosities appear at no
scan lens at all** (the scan dot prefab does not wire the curiosity circle).

**THE DIPLOMACY BAND IS A LIST OF EMPIRES** (owner ruling 2026-09-01;
`Screens/Galaxy/ScanDiplomacy.cs`). At the two furthest-out rungs the lens names no star at all, so
the map stop holds every MAJOR the player has met plus their own — always at least one, which is
what makes the reconciliation into this band always land somewhere — ordered by the centre the
WATCHED empire's intelligence knows, in the constellations' own reading order, with an empire it
does not place sorting after those it does. A row says who they are, that centre **as a position and
never as a home** (the game draws the same circle whether the record is their capital or the
highest-influence colony the watcher can see), and how the watched empire stands with them in the
game's own word for the state. **And the star the lens writes their NAME over** (owner ruling
2026-09-01, after playtest; `scan.empire-home`, "Home System ⟨star⟩"): that is a second drawing and
not the centre circle, so the row says it wherever the game paints that line — which is exactly at a
major's home system the player has explored (`ExplorationState >= 2 && IsMajorHomeSystem`, the same
gate the swap toggle lives behind). The player's own home always answers; an unexplored foreign
centre stays a position and nothing more. Reported from the owner's game, where his capital was the
one place on the band with no row of any kind while a tethered colony had one. Under it hangs what
the picture puts there: the SWAP TOGGLE wherever
the game draws one — inside the empire-name line of a label at a major's explored home, and nowhere
else — named with the game's own word for the gesture; and, for the empire the lens is WATCHING, the
colonies it tethers to that centre, gated by the WATCHED empire's knowledge rather than the player's
and named only where the player's own knowledge names them. BATTLE rows are a level of their own,
one per fight the lens has planted a label over, since two empires are fighting there and filing the
row under either would say the fight was one of theirs. **The empire headings from level 3 carry the
same reading**, which is what makes the whole ladder one shape — and, because both wear the one key,
a system under the cursor when the camera crosses into this band seats on its OWNER's row.

**A LANDING ON SOMETHING THE LENS DOES NOT DRAW LEAVES THE LENS FIRST** (owner ruling 2026-09-01;
`GalaxyHudScreen.DrawnByTheLens`). Measured first: the game's own reveal flow run in-mode does
nothing about the mode — it slides the camera to the fleet and leaves the player in a lens that
draws no fleet. So before a landing is performed the target's KIND is asked of the scan ladder. A
system and a world have rows under the map lenses and the landing slides and seats in-mode as
before; a fleet, a probe, a missile, an ally's pin, a quest marker — and a system at the Diplomacy
band — have no row anywhere in the mode, so the landing leaves through the game's own toggle
(`ToggleScanView`) and then lands the ordinary way, forced band included. A bookmarked POINT is
always drawn: it is the player's own annotation rather than a rendering.

**THE IN-MODE TREE ENDS WITH "BOOKMARKS"** (owner ruling 2026-09-01, the word approved the same
day): the player's own POINT bookmarks are a group of their own at the end of the map stop, in
position order, because everything else in that tree is a picture of what the lens is painting and
loose annotations among the empires would read as places the lens had drawn. A bookmarked SYSTEM
has no row there — its annotation rides the system's own row. The heading is not in the rows' keys,
so a bookmark keeps the key it has in the ordinary view and the cursor rides across the mode
change; it seeds open, and an empty group is not declared.

**THE HACKING FAMILY IS FOUR LENS-INDEPENDENT STOPS** (owner ruling 2026-09-01;
`Screens/Galaxy/ScanHacking.cs`): `scan:hacking` (bandwidth, its allocations, speed, operations),
`scan:traitors` (the sleeper count and the repartition toggle), `scan:console` (the three mode
switches and each program menu's rows) and `scan:notifications` (a row per chip), declared between
the map and the legend on both scan pages. All four read DRAWN widgets, so on an install without
the Awakening DLC — where the game hides the transforms outright — they declare nothing and need no
gate of their own. Three labels are excluded as measured placeholders (`%TracingSpeedTitle`,
`%TraceOperationsCountTitle`, the traitors banner's prefab revenue line). A program's Enter is the
game's own click, so it arms the hacking targeting cursor the mod already models.

**A ZOOM THAT SHRINKS THE BAND NEVER TAKES THE CURSOR OUT OF THE TREE** (owner ruling 2026-09-01).
When the level change takes the focused row's whole kind away, the cursor lands on the row that
CONTAINED it — a fleet or a lane's fleet on its system row, a deposit dossier or the manage button on
its system row, a system on its constellation row — and the map stop's remembered position is
repaired the same way, so Tab back after zooming from the slider lands there too. It is the graph
engine's own seat choice, one tier above the nearest-survivor walk and taken only on a build that
declares fewer kinds than the one before (`GraphBuilder.SeatOnContainer`), so an ordinary row dying
still lands on the neighbour beside it. A row with no containing row — a probe drifting in open
space — falls back to the nearest survivor, which is still inside the tree.

**THE ZOOM LEVEL TELLS YOU WHAT AN EXPANSION GIVES** (owner ruling 2026-09-01, the graded model;
`GalaxyHudScreen.FollowPlace` / `NoteJump` / `CollapseZoom`, `ConstellationRows.ZoomInto`). Right on
a system at levels 3–6 opens it IN PLACE — the lanes at 3–4, the lanes and the fleets at 5–6 — and
walking those children moves the camera no closer than the player put it: below the detail band the
map draws no inside for a system, so there is nothing for the camera to come in on and the far bands
stay what they are for, reading the map's geometry. At 7–12 the expansion completes the detail and
so forces the zoom to level 13 exactly, which is the jump it already made; at 13 it is in place.
A CONSTELLATION opens in place from level 3; at 1–2, where the map names no system, the press first
brings the picture to level 3 centred on that stretch of sky and then opens — the zoom line is
announced first and the settled first child after it, the ordinary `BetweenViews` hold. **Closing
hands back the view you were browsing**: the level the expansion jumped from, or — where nothing was
written down, which is what a hot reload leaves behind — spoken level 9, the inspect cursor's own
entry ceiling (`GalaxyInspect.EntryZoomCeiling`), so the mod has one number for "a sane distance to
be put at" rather than two. The memory is per system and per page instance, and a collapse whose
camera is not in on that system moves nothing at all. Backslash's own way out is unchanged and still
goes to the game's default step: a zoom-out by hand is the player choosing to read the same place
from further off, not undoing an expansion.

**A SNAP LANDING FORCES ITS TARGET'S BAND** (owner ruling 2026-09-01; `GalaxyHudScreen.EnsureBand`,
off `Bands.LowestLevel`). Before the cursor is sent anywhere, the camera comes to the nearest-out
level at which the map is drawing that KIND of thing — a planet or anything hanging off one to 13,
a fleet to 5, a system to 3, anything else standing out in the open to 7 — and no closer, so a
landing made from the orbital view is never pulled back out. It is not a camera preference and it
overrides the caller's (`MapCamera.None` included): a band that draws nothing of the kind declares no
row, and a landing there sends the cursor to a node that does not exist and says nothing — measured
at levels 1–2, where a bookmark jump moved the camera and spoke not a word. Beyond the forced band
each landing keeps its own framing: the scanner's go-to on a system still zooms in on it, and a
bookmark's `LandInside` still lands on the system's first child, which at level 3 is a lane and no
zoom. **FOLLOWING A LANE FRAMES NOTHING** (owner ruling 2026-09-02; `MapReach.Local`, set by
`GalaxyHudScreen.Arrive` and read by `MapLandings.Decide` into `MapLanding.Frame`): Right on a
star lane, and Backspace back up it, move the player to a NEIGHBOUR of the row they are standing on,
so the camera does exactly what expanding that system in place would do at this distance and no
more — a slide at 3–6, the ordinary coming-in at 7 and beyond. It framed before, which took a follow
at spoken level 5 down to 13 while expanding the very same system stayed put. The forced band is
untouched (a system still needs level 3), and it is never reached from a follow anyway: at 1–2 the
map draws no lane, so the tree declares no lane row and there is nothing to press Right on. **A bookmarked POINT forces nothing** — a bookmark is the player's own annotation rather than
a rendering, and it has a row at every level; at 1–2, where every constellation group stands shut,
those rows are declared at the TOP level instead, interleaved by position and keyed exactly as they
are everywhere else so the cursor rides across the boundary. A bookmarked SYSTEM has no row at 1–2
(its row is the system's), and its jump is the case the forced band exists for. The type-ahead needs
no rule of its own: a search finds only what the band's tree holds, so its landings are already in
band (measured — "sabel i" answers 0 results at level 4).

**A WORLD READS AS THE DOT THE MAP IS DRAWING OF IT** (owner ruling 2026-09-01;
`PlanetRows.AddPlanetDot`). At levels 7–12 a planet is a coloured circle in the system's nameplate,
and its row says what hovering that circle gives — the name and the colonisation status in the
game's own words — plus the marks the circle carries (curiosities in orbit, a mining probe), with the
circle's own tooltip as the review buffer and no children. Size, type, outputs, anomalies, deposits,
the dossiers and every action belong to the orbital card, which the map draws one band closer, so
they are read at 13 and nowhere else. It is the SAME node either side, so a cursor standing on a
world when the camera pulls back stays on that world and simply hears less.

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

**UNDER A LIVE INSPECT CELL, ONLY THE CELL MOVES** (owner rulings 2026-08-31). A landing made while
the player is driving a square about the map moves the SQUARE and nothing else: not the zoom — that
reverses the 2026-08-22 line which had a place's zoom override the cell's own slide, so the picture
stays at the scale the player chose — and not the tree cursor, which reverses the reseat ruling made
earlier the same day. It is one rule for every gesture — the scanner's Alt+Home, a notification's
show-location, Ctrl+L, travelling a lane, a bookmark jump — because they are all the one landing, and
it is two lines of `MapLandings.Decide` rather than a case per caller.

**ESCAPE RESTORES; ENTER COMMITS** (owner ruling 2026-08-31, the settled shape of leaving the mode).
Because nothing moves the cursor while the cell is up, **Escape always lands on the row the mode was
armed from** — however far the cell was swept, jumped or travelled in between. Coming back is what
Escape means. **Enter is the other half**: it exits on what the cell HOLDS, so a player who wants to
stay where they swept to presses Enter rather than Escape, and stepping inside what they landed on
zooms exactly as any tree walk does. What died with the reseat is worth naming, because all of it was
built for it and none of it is left inert: the deferred seat, its camera-free mark, and the mode's
`Reseat` entry point. The whole double-speak class went with it — there is no landing in flight at
Escape any more.

**A cursor the tree re-seats underneath a live mode says NOTHING** (owner ruling 2026-08-31;
`Screen.SilentUnderMode`, overridden by the galaxy page while the cell is live and on the map). The
player is reading squares, not rows; when the row the cursor was parked on stops being declared, the
tree lands it on a neighbour it did not choose, and reading that out interrupts them to name
somewhere they never went. The re-seat still happens — the cursor must stand somewhere real — and the
news comes when they leave, on the exit's own landing. The line is SWALLOWED rather than held:
suppressing without recording it only defers it, and the first frame after the mode ends then reads
the neighbour out ahead of the exit's landing (measured: two utterances for one exit). With the mode
off, a vanished focused row keeps its voice everywhere.

**Where the armed-from row has DIED while the mode was up** — a bookmark whose slot a dedupe took, a
fleet the tree has re-filed — **Escape lands on the nearest surviving thing that stands somewhere**,
measured from the place that row stood at (`GalaxyHudScreen.RestoreRow` → `NearestPlacedRow`, reusing
`Core/UI/NearestPick.cs`). The place is what the player meant, and the replacement case falls out for
free: a bookmark that took the old slot's tile is at distance zero and wins. **This is Escape's rule
alone.** The Backspace trail stays exact-or-drop, because it always has an earlier entry to fall to;
Escape has nothing behind it, so a near miss beats nothing at all.

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

**AN OPENED STAR SYSTEM IS SEVEN NAMED REGIONS** (owner design 2026-09-02;
`GalaxyHudScreen.AddInside`), in one fixed order at every zoom band and under a scan lens too:
**Status** (`<key>/status` — the label's battle, blockade, blackout, siege, invasion, juggernaut,
academy, sleeper, rebellion and King-of-the-Hill marks, the decaying-system and metaplot-battle-rules
pictures, then the quest pins) · **Actions** (`<key>/actions` — the management door, the label's
buttons, the wrecked Arks, the probe bearings) · **Planets** · **Star lanes** · **Fleets** (parked,
en route, free-moving, then the hangars) · **Resources** (the deposits) · **Details**
(`<key>/details` — the star's own dossier first, then the portal, honor zone, wonder, detection
probe, temple, slumbering ruins, home system, trading company, exploration winners and haunted
planets). Each is a `PushContext` level, so the name is said once on the way in and the "N of M"
counts that region alone; Alt+Up/Down jumps between them and a region the map draws nothing for does
not exist (measured 2026-09-02: the 3–6 bands are Star lanes plus Fleets, band 3 alone Star lanes;
the Trade lens is Planets, Star lanes and Status where a lens icon is drawn, and the System lens,
which draws no planet cards, is Star lanes and that same Status).
Which region a label picture belongs to is decided by the WIDGET it was read off
(`SystemLabelReadout.Region`), never by its words. Dossier nodes keep the keys they had —
`<key>/tooltip/<i>` indexed over the whole collected list, not over the region — so bookmarks and
landings are untouched.

**ONE DOOR PER SYSTEM, NEVER TWO** (owner ruling 2026-09-02). The map draws two ways into a colony's
own page — the button beside the name and the construction slot below it — and the slot's click is
the name-line button's own handler (`StarSystemLabel.OnRequestManagementView`). So where the label
draws the slot, its node (`<key>/queue`, "Building Interplanetary Transport Network, 3 turns",
carrying the constructible's dossier) IS the door and leads the Actions region, and no "Manage
system" is declared beside it; where the slot is not drawn — an outpost, a foreign colony we hold a
traitor in — `<key>/management` is the door exactly as before.

**Which key crosses a multi-region stop** (measured on the empire HUD band, 2026-08-24): Up/Down
step between the stop's REGIONS (rows) and clamp at the last one; Right/Left walk within the row.
Alt+Up/Down jump regions by name where the stop declares them.

**A PANEL SET THE PLAYER READS IS ONE STOP, ONE REGION PER PANEL** (owner design 2026-08-29,
`SystemManagementScreen.BuildSidePanels`). The star-system page's left edge draws four unlabelled
information boxes — colony info, population, representatives, governor, and whatever an outpost or
a ghost system draws instead — and they were four Tab stops, so Tab crossed the same edge four
times to reach the panels below. They are now ONE stop ("System information", the mod's own word:
naming it after any panel would say that panel's name twice, since it is also the first region),
with each panel a region named as before, so Alt+Up/Down steps System → Population →
Representatives → Governor and Up/Down still walks every row in the same order. The SPACEPORT keeps
its own stop: it is a place to WORK — a ring to carry population out of — not a thing to read. The
split is asked of the panel, not of a list, so an outpost's or a ghost's set merges without being
modelled. **One consequence, accepted**: a panel that split ITSELF into regions loses that (the
representatives panel's two captioned blocks), because a region is now one panel; the captions
still name the blocks and are still rows, so only the region chord's stop inside that one panel
went. Regions are flat within a stop — a tier that wants both needs the two-tier model, not this.

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
cell (silent for none or several — **Enter commits, Escape restores**, below), **Escape** leaves
("Exited inspect mode", on the row the mode was armed from), **`+`/`-`** grow and
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
key the player would have had to press first, and it arms nothing (2026-08-22).
**WHAT ENTER LANDS ON, in order** (owner ruling 2026-08-31; the order is DATA, in
`Core/UI/PlacedRows.cs`): the one PLACE in the square — a star system or a special node — else the
one FLEET, else the one MOVER the map draws out between the stars (a probe, an obliterator missile,
an ally's pin), else the one QUEST MARKER, else the one POINT BOOKMARK. Things that move come before
things that annotate, and the player's own note comes last of all. Each tier needs exactly one
candidate and the tiers above it to be empty, so a place beats a fleet standing at it and beats a
bookmark on its tile (measured: a square holding Dusay, a fleet and bookmark 8 exits on Dusay's row);
two of anything at the deciding tier is taken and silent, which is the refusal a key pressed
speculatively mid-sweep should give. **The bookmark, probe, missile and pin tiers all closed the same
gap**: each had a row, a position, a place in the leap trail and a place in the restore, and was
missing from Enter's list alone — so a square that READ OUT "bookmark 1" answered nothing when Enter
was pressed on it. The four inventories are one table now, and a row kind that is not in it fails a
build-time lint rather than working in three places out of four.

**THE CURSOR IS ARMED ON A PLACE, so a row that stands nowhere refuses** (owner ruling 2026-08-31):
Ctrl+I on a CONSTELLATION heading is silent, arms nothing and moves nothing, exactly as Shift+n
answers the same rows. A constellation is a grouping, not somewhere the player is standing — and the
`Constellation` entity HAS a position, the centroid the map writes its name at, which is the trap:
the arming used to walk up from a row that could not answer and take its parent's, so the cell opened
a whole stretch of sky away from the heading the player was on. The walk now accepts a STAR SYSTEM
and nothing else, which is what keeps a planet, a lane, a dossier or a berthed fleet arming at their
star — none of those has a position of its own, because the map draws them all there. The refuse-list
measured on the fixture is exactly the constellation headings; by the same rule any other grouping
row with no place of its own joins it (the unexplored bucket, code-predicted — this fixture declares
none).
**THE DIPLOMACY BAND IS THE ONE PLACE WHERE THE HEADINGS ARE THE PICTURE** (owner ruling
2026-09-01, after playtest; `GalaxyHudScreen.DiplomacyRowPlace`). At the scan ladder's two
furthest-out rungs the lens names no star at all, so those rows are not headings over stars the
player can walk into — they are what is drawn. Every one of them with a place ARMS the survey there:
an empire row at the centre the watching empire's intelligence has for it (an empire it cannot place
has no circle and still stands nowhere), a spoke and a battle row at the star at their end, a
bookmark row as everywhere else. The BOOKMARKS heading has no place and keeps the silent refusal.
Before this the whole band refused, so the survey — the one reading those rungs exist for — was
armable from a point bookmark and nowhere else (measured: Ctrl+I on a spoke row consumed, silent,
`Live` false). It changes where a row IS and nothing about what it can DO: Enter's order, the leap
trail and the restore are still the registry's (`Core/UI/PlacedRows.cs`).
Escape and the size
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
**An EMPTY square is taken and does nothing** — there is no leap of any kind on these chords
(owner ruling 2026-09-01: Alt+Left/Right are for star lanes and fleets only).

**THE CELL HOLDS WHAT THE BAND DRAWS** (owner ruling 2026-09-01; filtered in `GalaxyInspect.Read`).
The cursor reads the same band table the tree filters its rows with, in the same kind vocabulary
(`Core/UI/Bands.cs`, `BandKind`), so a square can never name a thing at a distance the tree has no
row for: fleets from spoken level **5**, the things the picture only draws beside a full nameplate —
probes, obliterator missiles, ally pins, quest pins — from **7** (`ZoomBands.MapDetail`, the tree's
own detail gate), star lanes from **3**. Filtered at the one gathering, so the reading, Enter, the
Shift+arrow skip's comparison of two cells and the Alt+arrow travel keys all look at the same
square — which is also why the travel keys simply have nothing to follow at levels 1–2. Two
deliberate exceptions, both the survey's: SYSTEMS are named at every level although the picture
stops naming them at 1–2 (that deviation is what the survey IS), and the player's own point
BOOKMARKS go quiet at 1–2 because the survey's ruled reading is territory, systems and
constellations alone.

**UNDER A LENS THE CELL TOPS OUT AT 10** (owner ruling 2026-09-01). The map lenses are a galaxy of
worlds and the Diplomacy band is a galaxy of painted TERRITORY — which is the survey's whole subject
— but the System lens (11–13) turns the node labels off and puts one system's panel on the screen
instead, so there are no squares of galaxy left to read and a live cell carried into it ends with
the ordinary "Exited inspect mode", moving no camera. ARMING there needs nothing new: the entry
ceiling the mode has always had is internal rung 8, which under a lens IS the Economy band, so
Ctrl+I from the System lens pulls out to it and arms, and the lens change announces itself.
Crossings between 2 and 3 continue the mode as any within-range crossing does. The cell is band
filtered off the same table in-mode, so a lens that draws no probes, missiles or ally pins names
none — the kinds the picture draws out BETWEEN the stars are their own column of the table
(`BandKind.OpenSpace`) rather than riding on the planet dots, because those two answers part company
under a lens.

**THE MODE IS ZOOM-AWARE** (owner ruling 2026-09-01, corrected; `GalaxyInspect.ShowsTheGalaxy`).
In the ordinary view the cell operates at spoken levels **1–12**. ARMING is unchanged: allowed from anywhere, and a camera
closer than level 9 is pulled OUT to the entry ceiling first (`EntryZoomCeiling`, internal rung 8),
so the mode never opens past the top of its range; a camera already further out is the player's own
and stays. A LIVE cell carried into **level 13** by ANY route — the zoom slider's fine or coarse
step, the game's own held PageUp, the wheel — ends the mode with the existing "Exited inspect mode"
line, and **the exit itself moves no camera** (it is `Leave`, not the Escape path: no recentre, no
re-seat, the tree cursor left exactly where it stands). The question is asked once a frame as "where
is the camera", never per key, so a route nobody thought of is covered by construction; the boundary
itself is read off the band table (`Bands`) as "the planets have become full cards" rather than
written down as a 13. A rung the game has no answer for — a battle, a level it is still flying
between — keeps the mode, on the standing rule that a filter which cannot tell what is drawn must
withhold nothing. **Crossing the other way does NOT exit**: at levels 1–2 the cell continues as the
TERRITORY SURVEY, saying whose territory every square is rather than only saying so on a crossing
(`docs/test-recipes/inspect-and-influence.md`, **The territory survey**). ARMING at 1–2 is allowed by
the zoom rule but still needs a row that stands somewhere, and those bands declare only constellation
headings (which refuse) and point bookmarks (which arm) — an OPEN owner question, on the roadmap.

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

## Bookmark keys

**Ten places the player names by a digit, and the home system beside them** (owner-approved
2026-08-31; `ES2Access/Screens/GalaxyBookmarks.cs`, store `ES2Access/UI/Bookmarks/MapBookmarkStore.cs`):
**Shift+1..9,0 SET bookmark n**, **Ctrl+1..9,0 JUMP to bookmark n**, and **Ctrl+C JUMPS TO THE HOME
SYSTEM** — the same landing, at a place nobody has to set, consuming no slot and never written down
(the game's own `DepartmentOfTheInterior.HomeSystemNode`). **An empire with no home system yet gets
a spoken refusal, "No home system"** (`galaxy.bookmark.no-home`, owner's wording, owner ruling
2026-08-31): the Vaulters begin that way and stay so until they super-colonise
(`FactionTraitManualHomeSystem`), so it is a state a player can really be in, and a key that answers
nothing is indistinguishable from a key that does not work. Top-row digits only; the keypad's are a
different key and nothing asks for them. None of the twenty-one repeats.
**All twenty-one are claimed while the galaxy page is up**
(`GalaxyBookmarks.KeysClaimed` → `GraphNavigator.DeclaresStop(galaxy:systems)`, through
`InputAction.ClaimedWhile`), **the game's scan lens included** (owner ruling 2026-09-02, replacing
the stand-down that shipped with the feature). A bookmark is the player's own annotation rather than
something a lens paints, the in-mode tree draws every one of them — a system's on its own row, a
point's under the "Bookmarks" heading — and the landings underneath are already the mode's own, so
there was nothing left for the keys to hand back. A SYSTEM jump in-mode is `LandInside`, which
opens the owner heading on the way in through `KeyGraph.GroupingAncestor` and speaks once; a POINT
jump is what it is in the ordinary view — the cell where the cell is driving, otherwise the
bookmark's own row, at the System lens as everywhere else. The chords are free: the game's only digit bindings are its
`DebugSwitchToEmpire*` actions, dead behind the `Accessibility<=Internal || EnableModdingTools` gate
(`GuiManager.cs:2131`, measured Public/False), it binds nothing at all to C, and the type-ahead takes
A–Z and space and drops every other character, so Shift+digit is never typing (measured 2026-08-31:
a PHYSICAL bare `Alpha3` on the galaxy page says nothing and moves nothing).

**SET is a key of the MAP WIDGET; JUMP is a key of the PAGE.** A bookmark is made out of where the
tree cursor stands, so off the map stop the set key is silently nothing — not a refusal, because
there is no place there to refuse. Any row the tree files under a system bookmarks THAT SYSTEM (by
`GameEntityGUID`, so the bookmark follows it); anything with a row of its own out on the map — a
probe, a fleet away from any berth, a missile, a pin — bookmarks the POINT it stands at today, a
photograph and not a leash. A constellation, and every other heading, is silently nothing: a row's
own subject cannot stand in for a position here, because a `Constellation` IS an entity with one
(its centroid), and asking the subject bookmarked the middle of a stretch of sky. Jumping works from
any stop of the page, because coming back to a place is what a player reading the notifications
wants and making them Tab to the map first is asking them to be where they are trying to go.
**With the inspect cell driving the map, the set key is made out of the CELL** (owner ruling
2026-08-31; `GalaxyBookmarks.SetFromCell` ← `GalaxyInspect.CellPlace` → `CellSubject`), and the
square answers in one of three ways. **One place in it** bookmarks that place, GUID and all, so a
bookmark made from the cell and one made from the tree are the same bookmark. **No place in it** —
empty sky, a lane through it, a fleet crossing it — keeps the square's own point. **Two or more
places in it REFUSES**, out loud and storing nothing: "Shrink cursor so it contains only one
system" (`galaxy.bookmark.shrink`, owner's words, owner ruling 2026-08-31). Which star the player
meant cannot be known at eleven units across, and quietly keeping the point instead is the worst of
the three — they asked for a star and would be handed a piece of empty sky — so the refusal names
the one thing that gets them what they asked for, and the size keys are already under their hand.
The two that DO store are audible apart: the line names the system or says the pair. PARKED none of
it arises, because the cursor is off the map stop and a set there is already silently nothing.

**ONE PLACE, ONE SLOT** (owner ruling 2026-08-31; `MapBookmarks.SetAlone`). Setting a bookmark
somewhere another slot already holds EMPTIES that other slot: the player meant to move the place to
this digit, not to own it twice, and two digits for one place is a wasted slot out of ten. It is
said, in a sentence of its own — "Bookmark ⟨new⟩ set on ⟨place⟩, replacing bookmark ⟨old⟩"
(`galaxy.bookmark.set-replacing`; the plain line stays for a set that collided with nothing) —
because a slot the player set must never go missing without their hearing it. **What counts as the
same place is asked kind by kind**: two SYSTEM bookmarks are the same place when they are the same
system, by GUID, so two different stars that happen to round into one spoken tile keep both slots;
everything else is judged on the TILE, the rounded pair the player hears, so two points they cannot
tell apart when read out are one place, and so is a point set on a bookmarked system's tile.
Re-setting a slot the player is already standing on is the plain overwrite it always was — a slot
never collides with itself. **The rule applies ON SET only**: a file that already holds two slots for
one place keeps them until a set touches that place, because rewriting a player's file on the
strength of a rule they have not invoked is not the store's business. Both changes go to disk in one
write.

**A jump is a FOCUS landing and never a click** — an armed targeting cursor is waiting for an Enter
somewhere, and a jump that confirmed it would send a fleet to the bookmark instead of taking the
player there (the travel-versus-click split above; measured 2026-08-31 with a `ProbeLaunchingCursor`
armed, which survived the jump). Inspect OFF, a system bookmark lands on the system's FIRST CHILD,
which is what brings the camera all the way in through the page's one camera rule; the branch is
asked for on the press and the landing waits for the build that opens it
(`GalaxyHudScreen.FollowBookmarkLanding`, 12 frames, falling back to the system's own row). A point
bookmark lands on its own synthetic row, and the camera slides onto the point through the same rule.
Inspect ON, the jump is **the page's own landing and nothing of its own** (`GalaxyHudScreen.GoTo`
with a `MapTarget`): the cell moves to the bookmark's rounded spoken pair and NOTHING else does — not
the zoom, not the tree cursor — exactly as the scanner's go-to now behaves, so Escape afterwards
still puts the player back where they armed the mode (**Under a live inspect cell**, above). PARKED
is the one shape that landing cannot express, and the difference is SPEECH: somebody has to bring the
player back to the map, and a stop landing announces itself. So the map stop is focused SILENTLY
(`GraphNavigator.FocusStop(stop, announce: false)`) and the mode's own resume reads the new cell —
one utterance, naming the place jumped to rather than whichever row the map stop was left on. That
return lands on the armed-from row, which is where Escape would have gone anyway, so the two agree.
An empty slot is a spoken refusal ("No bookmark n") and moves nothing.
**Backspace comes back from a jump** — the leap is remembered on the tree's trail, or on the cell's
own stack while the inspect cursor is up (**Backspace: the screen's second command**, above). A jump
made from another panel of the page remembers nothing, and an empty slot's refusal is not a leap.
**A JUMP ANNOUNCES EXACTLY ONCE** (owner ruling 2026-08-31), whichever of the three shapes it takes:
inspect off, the tree landing is the announcement; inspect live, the cell's own reading is; parked,
the cell's reading again, behind a silent return to the map.

**A bookmarked system's row ends with "bookmark n"**, last of everything it says: it is the player's
own note about the place, not a fact about it. A bookmark whose system this build lists no row for
falls back to a synthetic row of its own, "Bookmark n at ⟨pair⟩", filed in the constellation whose
outline holds it (`ConstellationMap`) and read in that group's own order — or, where no named
stretch of sky does, walked into the top level by position like everything else homeless on this map.
**The inspect cell says the same word, from the same composition** (`GalaxyHudScreen.BookmarkWord`,
one method with two overloads and one ModStrings key, so the two surfaces cannot drift): a
bookmarked system standing in the square ends ITS part of the cell's sentence with "bookmark n"
("Dusay, bookmark 5, 1st Defenders Navy, …"), exactly where its tree row ends with it, and a POINT
bookmark inside the square is named by the same containment every other thing in the cell is found
by — so it is right at every cursor size — reading "bookmark n" alone, last of the things standing
in the square and ahead of the lanes crossing it. It says which slot and no more: a bookmark has no
name, and where it is is the pair the cell is about to say anyway. **Both join cell identity**, so
the Shift+arrow skip stops at a bookmarked square (`CellSkip`) — a point bookmark is the one thing a
square can hold that the map draws nothing for, and a skip blind to it would sweep the cursor
straight over the one place the player asked to be able to find again.

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

**The Controls category holds one row per mod ACTION, in SIX TABLES under six headings** (owner
ruling 2026-09-02): Cursor and navigation, Buffers, UI hotkeys, Inspect mode, Scanner, Bookmarks.
The layout is `ES2Access/UI/Input/KeybindLayout.cs` and NOT the order `ModEntry.BindKeys` registers
in - which key exists is the input layer's business and where its row is drawn is the page's, kept
apart so that moving a row cannot move a binding. A heading is a `ModRows.Caption`, which the
options screen turns into the name of a REGION and never into a stop: "3 of 22" counts the table the
player is standing in, Alt+arrow jumps by the six names, and Down still walks the page end to end.
Each row is the game's own `OptionKeyMappingItem`, so it reads exactly as a
Controls-tab row does, read as a three-column TABLE (the action, its primary key, its secondary
key) with Enter on a key cell capturing that cell and Delete emptying it. Every row's
title is the mod's own string (`action.<action key>.title`) - mandatory, not cosmetic: a
localization key the game has no row for is drawn and spoken RAW (`docs/gui.md`). **A DESCRIPTION IS
OPTIONAL** (owner ruling 2026-09-02): most titles say everything the row has to say, and an
`action.<key>.description` the table does not carry leaves the row's `AgeTooltip.Content` empty, so
nothing is drawn and nothing is declared. An action bound to more than two chords keeps the extras, which stay
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

The rest of the mod's settings window — how a player reaches it (a DRAWN entry titled "Mod Settings",
tooltipped "Endless Space 2 accessibility mod settings", on both menus - owner rulings 2026-09-02), the four tabs (General,
Scanner, Controls, Bookmarks), the Scanner tab's slot editor and the Bookmarks tab — is `docs/test-recipes/mod-settings.md`.

Game-mechanism findings (window gates, pool slots, tooltip internals, fleet and quest
mechanics, the icon numbers) live in the game-facts topic files ([README.md](README.md)) — a new
fact lands there, never here.

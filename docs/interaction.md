# ES2 interaction language — layers, keys, claims

The mod's ES2-specific interaction design: the layer budget, the key map, and the claim
rules. Read when BUILDING a screen; the loop itself is `docs/dev-loop.md`, and the generic
doctrine behind these rules is `docs/generic/input.md` / `ui-navigation.md`. A new layer
number or key binding lands here (bindings themselves need owner approval first).

## Layer budget

Static per screen (doctrine: ui-navigation.md "Layers are static"). **A layer number is
allocated by the main agent when a stage is briefed, never claimed inside a stage** —
pipelined stages cannot see each other's claims, and three of them once picked the same
number independently.

`0` main menu, the new-game lobby, and the menu-replacing out-game pages (Credits/DLC/Mods/
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
`23`/`24` target pickers · `25` system-selection modal (over the star-system page that opens it
and under BOTH things it can raise itself: the tutorial page it registers a key for, and the
drop list its policy column opens) · `26` fleet-selection · `27` hero list ·
`28` hero-selection (under the hero-inspection window its own Inspect raises) ·
`29` juggernaut specialization ·
`33` government · `34` laws · `35` ship designer (under the hull drop list 70 and the
lose-changes box 100) ·
`36` election, population **and** recipe-creation — a deliberate three-way share: all sit on
the game's exclusive modal stack behind different openers, so no two can be up together, and
a window on such a stack voids any layer constraint against its stack-mates
(ui-navigation.md; owner rule 2026-08-12: mutually exclusive screens may share a number) ·
`41` negotiation · `42` advanced battle report · `43` minor-faction diplomacy · `44` pirate
diplomacy · `45` hero inspection (under rename 80 and the message box 100) · `46` the academy
pair (shared — the two DLC modals, never up together) · `47` cutscenes · `48` advanced battle
setup · `49` victory-achieved ·
`50` game-menu · `51` end-game journal · `52` options (one number, above the pause menu that
can open it) · `55` load-save · `60` loading · `70` drop-list (above options, its owner) ·
`80` rename box · `85` improvements modal (over the star-system page, under its own
confirmation) · `86` system-politics modal · `90` tutorial-selection modal (over the new game
screen) · `96` contextual prompt (over the scan view that raises it and the modal stack it draws last
on; under the non-blocking box) · `97` non-blocking box · `98` tutorial popup · `99` error box ·
`100` message-box.
Mod-owned CHILD screens (`Screen.PushChild`) have no layer: the manager focuses the deepest
child of the top screen.

**The tutorial popup sits at 98 — above everything except the two boxes that must be
answerable.** The game itself draws most tutorial popups over its own screens, modals and
notifications (`TutorialPopupLayer`, per page — es2-facts), so any lower number buries one of
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
(`FleetPanel` — `docs/helpers.md`), not a screen, because selecting a fleet changes only the cursor and the
map underneath stays live and has to stay walkable. A layer of its own put it OVER the galaxy
and took the systems, the starlanes and the HUD out of Tab. It is contributed by the galaxy page
alone, which is complete rather than a gap: entering a system swaps the cursor to
`StarSystemCursor` and the game hides the window outright (measured — es2-facts).

**ES2 key map, in one place** (defaults in `ModEntry.BindKeys`; the generic table is
`docs/generic/input.md`). On top of arrows/Tab/Enter/Backspace/Escape/Home/End, Alt+arrows and
the Ctrl review chords: **Shift+Left/Right** coarse slider step — and, while the galaxy's inspect
cursor is driving the map, that mode's skip instead, since a coarse step means nothing on a map
(below); **Shift+Up/Down** and **Alt+Left/Right** are the inspect cursor's alone and are inert
everywhere else — **Ctrl+Shift+Enter** the control's
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
**The Alt-click is the one chord whose keys are not its gesture's, and that is deliberate**: Alt+Enter
is Unity's own built-in fullscreen toggle, handled inside the player's D3D11 window code below every
managed layer, so the mod's claim never reaches it and the window resizes on every press. Nothing
suppressible reaches it either — boot flags, a window subclass, DXGI's `MakeWindowAssociation` were
all checked and rejected (bug 17) — so the gesture moved to Ctrl+Shift+Enter, which the game binds
nothing to (its only Ctrl+Shift chords are two debug ones) and which keeps the family: every modified
click is still a modified Enter. Owner ruling 2026-08-19.
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
is wired as of 2026-08-21 (`CuriosityExpeditions` — `docs/helpers.md`), attached generically in
`CardActions.Emit` off the widget's own `CuriosityInteraction`, so the colony card has it and the
galaxy orbital card's fleet-mode twin does not. Dismiss-all is declared as of 2026-08-23, and NOT as
a chord: it is a BUTTON at the end of the notifications stop whose Enter runs the Alt branch's own
call (below), so nothing has to read a physically held modifier.
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
**Escape during a capture is a CANCEL** — the row goes back to what it was bound to and the mod says
"Rebinding cancelled." The game itself would either wipe the slot or, where the row's other slot is
empty, silently keep it (`docs/es2-facts.md`, "Escape during a key capture"); one key, one meaning.
**A chord the mod and the game both answer to is said, not resolved** (ruling 9): committing either
side's row onto the other's chord raises the game's own informative box — "While the mod's ⟨X⟩ is
active, the game's ⟨Y⟩ will not fire" — and the binding still lands, because the mod shadows the
game's keys by design. Mod↔mod overlaps are not checked at all (ruling 10).
Both windows are this same table: the game's Controls tab and the mod's Keybinds category are the
same `OptionKeyMappingItem` rows read by the same screen.

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
status — `docs/test-recipes.md`'s per-screen paragraphs own it; a drop always puts the carried
item at the target's own position ("Moved ⟨name⟩ to position ⟨n⟩").

**A control can END ITS REVIEW BUFFER by naming the chord that works there** (2026-08-21;
`NodeHints`/`ChordNames` — `docs/helpers.md`). A **usage hint** is one short sentence, one per
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

**Six keys name a PLACE and one ends the turn** (owner-approved 2026-08-22; `ModEntry.BindKeys`,
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
cost nothing (es2-facts).
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
system's name in the colony panel (the stop that holds the name — the panel the game binds for a
colony, an outpost and a ghost alike, which is the same condition under which it draws the arrows);
`Cells.EmitLinear` orders them off the rectangles, and MEASURED (2026-08-22) that is the name,
then previous, then next: the arrows do not flank the name at all - the banner is a wide panel at
the left of the page (x 32-250) and the arrows sit at x 256 and x 1204, one either side of the
whole page. The game gives
them no title, so they are mod-named ("Previous system" / "Next system") over the game's own
tooltips, like the planet page's pair.
**The ACADEMY strip's pair is declared the same way** (`academy:previous`/`academy:next`, in the
heroes stop, owner decision 2026-08-22): the game titles them no more than it titles the system
pair and gives them no tooltip at all, so both parts of the name are the mod's, and the arrow the
game switches off at its end of the strip reads unavailable.
**A control may end its NAME with the chord that works on it**, not only its buffer's last line
(`ChordNames.Label`, template `label.with-chord`): the four paging pairs and the end-turn button
carry theirs, because the key is the whole reason the pair was worth declaring twice. It is read on
every landing, which is the cost the owner accepted for these five; hints stay buffer-only.
`ChordNames.KeyName` now asks the GAME for the key's name first (`%KeyCode<Name>`, 120 rows in each
of the ten languages) and falls back to the mod's table and then to the engine's `KeyCode` name.

**Right opens a group AND goes inside it, in ONE press; Left comes out AND shuts it** (owner ruling
2026-08-22, `KeyGraph.TreeRight`/`TreeLeft`): the two presses each direction used to need are one,
so a shut group and an open one answer Right identically — the child is announced with its position
and the header's "expanded" word is never heard, while the "collapsed" word rides the parent's own
announcement on the way out. Left on a header itself is still the plain collapse with the cursor
unmoved. **A group that turns out to be EMPTY is left OPEN** and says "Nothing in here": expanding a
galaxy system brings the camera in, and the auto-recollapse this replaces zoomed straight back out.
Right again on it is a consumed leaf; Left shuts it. `OnFollow` leaves (starlanes) are untouched —
they were already one press.

**Ctrl+Tab is the GAME's chat key, not a mod binding**: at startup `GameChatKey` moves
`StartChatting` off Enter/Tab to Ctrl+Tab through the game's own options (ONLY while it still has
the shipped default; a customised binding is left alone), and whatever chord the binding sits on is
handed back through the stand-down (`ModInput.LeaveToGame`) — so re-binding chat in the game's
options keeps working. **Open chat is a PLACE, not a stop**: the key opens the mod's child screen
over the page (`ChatScreen`, in every session including single player — `docs/helpers.md`) with the
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
the game left with no Escape route: there the mod claims Escape (`ConsumesBack` true only then) and
runs that cursor's own right-click cancel, so Escape cannot raise the pause menu over a map still
waiting for a target (owner-ruled deviation, 2026-08-12).

**Inspect mode names constellation crossings** (2026-08-20): its own utterance AHEAD of the cell
(the "Skipped N squares" precedent, taking the interrupt the cell would have had) — on entry when
the initial tile is inside an explored hull, then change-only: "{0} constellation" crossing in,
"Out of {0} constellation" crossing out to unassigned space. `ConstellationMap` is explored-only
so fog leaks nothing; a CONSTELLATION boundary is not part of cell identity for Shift+arrows (the
skip runs through them); suspend/resume re-reads the cell only, never the crossing.
**Inspect mode names INFLUENCE crossings the same way** (2026-08-21): a second change-only utterance
behind the constellation's and ahead of the cell — "In {0}'s / your influence" for a cell PROVED to
be one empire's throughout, "Edge of …" where the boundary runs through it (several empires collapse
into one list line), "Out of {0}'s influence" naming what was left on the step into unowned space,
and the system row's own contested line alongside any of them. Growing the cursor over a rim is a
crossing too, so the memo is keyed on the SIZE as well as the cell. Unlike the constellation, the
classification IS part of cell identity: Shift+arrow stops at a border (`in:`/`edge:`/`vs:` +
empire index tokens in `CellSignature`). Gate: a cell wholly under the fog says nothing about
influence and contributes no token, and an empire whose colony node the map is not showing is
stripped of its NAME while its field stays in the arithmetic — so an unseen neighbour can cost a
cell its "in" and can never be named by one. **A cell names a starlane only
where the fog draws it** (2026-08-20): the link gate answers "is this lane lit", never "lit
HERE" — `Lit` samples the cell's unit squares through `IVisibilityService.IsExplored`, the
same field the cell's "Unexplored" word uses; the tree's lane rows keep the link-level gate
(they hang under the system, a thing not a place). The scanner has THIRTEEN categories —
constellations were removed 2026-08-20 (owner: not a discrete point) and Contested Influence added
2026-08-21. **A targeting cursor
arming now re-reads the standing control** (`AnnounceNextLanding`, arming path only; seat
actions and the modal-picker pop stay re-read-free). The standalone instruction utterance is
SKIPPED whenever a readout of the map stop will carry it: the cursor is inside the map stop
(with a dismissed-inspect re-read coming), OR a pending focus request targets a node inside it
(`GraphNavigator.PendingStopKey`, resolved through the same ancestor walk landings use — a
request into a still-collapsed branch answers correctly). This also fixed a pre-existing
double with NO inspect involved: probe arming's own seat lands inside the map, whose context
IS the instruction, so the watcher's line + the landing spoke it twice. The standalone line
is the plain else-branch (dev-injected arming with nothing pending); "Target selection ended"
is never swallowed — a landing in flight would not carry it (`!ended` guard). Pump-order
caveat: a request made AFTER the watcher runs in the same frame is invisible to it — the
guard works because deferred landings into collapsed branches outlive their frame (owner
rulings 2026-08-20). **The zoom band word is silent while the
game's scan mode is up** (`ZoomBand` returns null under `Scanning`; the lens titles carry the
naming there). **A focused constellation node HOLDS its culled label shown**
(`ConstellationLabelHold`, re-asserted per frame because culling recomputes on camera-position
change; released on blur/pop/reload through the window's own re-decide) so the game's
"Constellation" tooltip has a widget to fill.

**The galaxy map stop is constellation-grouped** (2026-08-20): top level = one group per
EXPLORED constellation (gate: `Constellation.Exploration[player] > 0`, the label's own check,
stale aggregate included), drifting rows at their own positions, and the merged
"Unexplored constellation" group LAST. Key heads: `galaxy:constellation/<guid>` and
`galaxy:constellation/unexplored`; all system ids live under them (`SystemKey` is the one
composer). Groups default EXPANDED on first sight (`Seed`, once per group per galaxy).
Constellation nodes speak no coordinate; opening one never moves the camera; the
collapse-un-zoom rule now exists at TWO levels (`ZoomOutOf`: collapsing a constellation whose
member holds `FocusedSystem` runs that system's own zoom-out). Adding a tree LEVEL obliges the
type-ahead scope to grow a range for the newly-hideable tier (`HiddenSystem`/`RevealSystem` —
a scope that does not loses the tier from search with nothing in any dump to show it).


**TYPING SEARCHES WHAT THE PAGE WOULD DECLARE, not what it has declared** (owner ruling
2026-08-22; `SearchScope.Extend` + `GraphBuilder.ExpandAll` — `docs/helpers.md`). On every screen,
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
unlock names, and the galaxy's, because its systems and fleets have reveal paths already proven.
A screen with such a scope must supply `SearchScope.IdOf` — the pure "which control is result i",
beside the `Land` that has side effects — or its results are offered twice.
**Stepping to another result closes the branches the PREVIOUS landing opened** (owner ruling
2026-08-23) — only those the SEARCH itself opened, so a branch the player had open before typing is
never touched, and the branch the LAST landing opened stays open when the search ends, because that
is where the player has been left (`GraphNavigator.RevealDeep` / `_searchOpened`). One consequence to
expect: a result only the deep build knew about announces one or two frames after the keystroke
rather than on it.

**The star-system page is named after the system it is showing** (owner ruling 2026-08-22):
`ScreenName` is "Heka, System management" — the DRAWN system name (the rename button's own label,
`ColonyInfoSidePanel.SystemTitleLabel`, which the game writes for an outpost as readily as for a
colony) composed with the game's own word for the page (`%StarSystemManagementScanViewWindowTitle`)
through `screen.star-system-named` = "{0}, {1}". `screen.star-system` ("Star system") stays as the
fallback for a system with no colony panel drawn. Turning the page (Alt+Left/Right, or the game's
own arrows beside the name) is the reason: it never leaves the screen, so without the system in the
name the one fact the turn is FOR went unspoken.

**Turning that page is ONE announcement and a seat in the new system's content** (2026-08-22, fixing
a defect found in batch 3). The view level is re-entered with a new node, and the mod's own gates now
ride that out: `IsActive` asks `LevelThroughTransitions` and latches on the page having planet cards
drawn, the way the planet page does, so the screen no longer leaves and comes back (it did that
TWICE per turn, announcing itself twice). The screen then notices its own system changing
(`SystemManagementScreen.Turned`), says the new name once, waits 30 frames for the game to rebind the
window — seating earlier reads a row belonging to the system just left — and seats the cursor on
`InitialFocusStop` with a 60-frame retry budget for the cards binding. The latch must be gated on the
cards being DRAWN and not merely on the window being shown: a page that becomes active while it can
declare nothing gets its cursor seated on the first shared HUD control instead (measured: an entry
landing on the view-title's scan button, and the walk's next Enter then toggling scan mode).
Known rough edge: while the page is between systems it declares nothing, so the cursor migrates to a
HUD stop for a moment and that migration is announced — one stray line between the screen name and
the landing.

**The minor gauge's four bands are named by the game, twice over** (2026-08-22): "CORDIAL (25)" -
the relation state the band buys and the relation points it starts at, composed through
`minor.band` = "{0} ({1})". The state comes off the band's OWN sentence key
(`%DiplomaticRelationStateMinorCordialDescription` → `…Title`) and the threshold off the segment's
position on the bar, so neither half is hard-coded and a patch that re-cuts the bands moves both.
The sentence stays in the buffer (`TooltipMode.None`) rather than being said after the name.
Beside them, the relation POINTS row is captioned **"Relationship"** (`minor.relationship`, a mod
phrase — owner ruling 2026-08-22, replacing the gloss sentence the shared last resort had been
using as the row's name; the sentence is now an ordinary tooltip). The Academy's relation-state row
keeps its own reading.

**Structural keys are PATHS, and that is load-bearing** (2026-08-20): `KeyGraph.AncestorKeys`
reads a landing's ancestry out of the id's own `/`-separated key, so programmatic landings
(locate, scanner go-to, deferred seats) auto-expand collapsed ancestors one level per build.
Renaming a key's HEAD silently breaks landings into that branch; a non-path key simply gets no
ancestry (as reachable as before). A landing in flight is cancelled by the player's own next move.

**The six zoom-in fleet actions name where they put the cursor and then put it there**
(2026-08-20): Colonize/Super Colonize/Destroy Planet/Expedition/Mining Probe/Reclaim Mothership
append a "moves focus to the first …" phrase and, on activation, expand the acting system and seat
the cursor on the first matching action row (`SeatAfterFleetAction`/`FollowActionSeat`; a
positional row id must hold steady 20 frames before the seat commits — the orbital card's buttons
arrive over several frames). No match = branch open, cursor unmoved, silent. **The seat and the
navigator's pending landing are SUSPENDED, not forgotten** (2026-08-20), while the galaxy page
is away (the discovery cutscene POPS it — a sibling view level) or the view is mid-flight:
suspended frames spend no budget and prove nothing; the landing knows its OWNER screen, so
another surface's arrival, cursor and keys never touch it; the player's own navigation ON the
requesting screen's graph still cancels. The expected expedition sequence is: press → the
discovery video plays (if enabled) and reads its cards → "Galaxy" → the seat lands on the
first curiosity, once. **A targeting cursor
arming ends a live-or-suspended inspect mode** with the mode's own exit line spoken ahead of the
instruction (`GalaxyInspect.Dismiss` from `GlobalHud.AnnounceCursorMode` — all nine cursors — and
from the six seat actions); entering inspect over an armed mode stays allowed, its landing Enter
leaves the found node focused for the mode's confirm, and Escape unwinds innermost-first. A modal
target picker ENDS the mode (page pop), never suspends it.

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
`Screen.OnFocusVisual` override → `Place` → `FollowPlace` — `docs/helpers.md`). Every focus landing on
the map stop resolves to a PLACE — the system a row hangs under, or a drifting thing itself — and to
whether the cursor is ON that row or INSIDE it. Same place, same closeness → nothing moves. A
different place with the cursor ON its row → `PanTo` (the slide, zoom untouched). Further IN on a
place → `SnapTo` (no flight, and it arms the landing's own settle wait). **The camera is never taken
back OUT by the rule**: stepping from a world up to its own star moves nothing, and the ways out stay
the player's (Backslash, closing the branch). Three triggers folded into it in the process — the
system row's own `PanTo`, `OnExpand`'s `ZoomTo` (system nodes no longer override `OnExpand` at all;
the engine keeps the expansion set) and the go-to landing's own `SnapTo`, which now asks the rule and
so leaves nothing for the landed node's focus to add. What that buys, all measured 2026-08-23:
**expanding a system (Right) brings the camera in because the first child's focus does it** — the
zoom and the descent are still one press, and a system with nothing under it says "Nothing in here";
**a manual Backslash zoom-out survives the whole of reading that system's children** (the rule's
record is what the camera was ASKED for, not where it is, so it says "already there"); crossing into
another system's children snaps once, on the crossing; and a go-to always moves, record or no record,
because a landing is a request rather than the cursor wandering. Enter is unchanged, Backslash remains
the way out, and **collapse un-zooms** while `GalaxyViewLevels.FocusedSystem` is still that system — a
camera the player has since moved elsewhere is left alone, so collapsing moves nothing there — and it
also drops the rule's "inside" record, which is what lets re-opening the same system bring the camera
back in. The gate is NOT `FocusedStarSystemNode`: measured, that is where the camera IS and it lags a
flight (`docs/es2-facts.md`). **A starlane is a LEAF and
Right on a named one TRAVELS** (`NodeVtable.OnFollow` → `KeyGraph.TreeMove.Followed`, consumed
silently): the cursor lands on the destination system's ONE node at the root of the systems stop,
that branch opens, and the camera goes there through the page's one landing and so through the camera
rule above — never `ZoomIn`, because travelling is not a click and must not confirm an armed targeting
mode (measured: the mode survives Right and is still ended by Enter on the same lane). The landing's ordinary announcement is the whole
announcement. **Backspace pops the trail** while focus is in `galaxy:systems`: back to the exact lane
node under the origin (the origin re-expanded so that node exists), camera back to the origin, again
no words; a hop whose origin or destination is no longer perceived is skipped, and an empty trail is
consumed and silent. A system opened BY travel is **collapsed on the way out** (another hop or a pop)
and one the player opened is left alone — and neither runs the collapse's own un-zoom, since travel
scripts the camera itself. The trail survives an excursion to another screen (the page keeps its
state on pop) and dies with the game instance. A lane into the dark is a silent leaf under Right.
A fleet on a LANE hangs under the end it is heading FOR, after the parked ones, saying which lane and
which bearing off the same lane list the lane nodes number themselves from (`GalaxyHudScreen.Bound`,
2026-08-16: the map draws where a fleet is going and never where it came from, so a lane fleet in
transit is filed like a free mover); one STOPPED between two stars is heading for neither end and
keeps its row under both; a fleet crossing OPEN SPACE
hangs under its DESTINATION only — the map draws where a fleet is going and never where it came from
(es2-facts) — and one whose destination is unperceived gets a top-level row instead, walked into the
system list by its own pair. **The systems stop is ONE region, not two**: colonies are not split off
from the rest (owner ruling 2026-08-16), so Alt+Up/Down on this stop jumps only between the stars and
what is drifting out between them, and declares nothing at all while there is nothing drifting
(since 2026-08-19 every sighted probe is a top-level drifting row — `galaxy:probe/<guid>` —
never a child of its nearest star). **Zoom is an adjustable node**
on the existing
Left/Right + Shift chords (no new binding), and it lives on BOTH the scan view and the galaxy's
`hud:view-title` stop, in a row of its own (the view-name label and its close-button node are gone —
owner-ruled 2026-08-18, Escape closes screens); its
coarse step is a LAYER-BAND jump rather than ≈10 increments — an owner-approved deviation, since
ten of the camera's thirteen steps would be the whole range.

**A node that owns dossiers beyond its own tooltip is an expandable group with TWO regions**
(owner ruling 2026-08-22, batch 2; `UI/TooltipChildren.cs`, `docs/helpers.md`). Right/Left are the
ordinary group arrows (today's two-press contract). Alt+Up/Down steps between them:
`<key>/actions` FIRST — the node's own buttons and structural children in the order the surface
draws them (absent where it has none) — and `<key>/tooltips` SECOND, labelled with the mod word
**"Tooltips"** (`node.tooltips-region`) as a `PushContext` level, holding one node per dossier keyed
`<key>/tooltip/<i>`. A dossier node is **named by the game's own header line for that dossier**
(`AgeWidgets.TooltipTitle`), its buffer is the drawn tooltip, and **Enter on it is consumed and
silent** — it wires no `OnActivate`, because there is nothing there to do. Positions count within
the region. Live today on: galaxy system nodes (the system's own stat block, then one per deposit
in the ground), system-management planet cards (the planet's dossier, then one per FIDSI pip),
and research-wheel dots (one per DRAWN unlock icon — the strip the wheel reveals under a hovered
dot, which carries the unlocked thing's full page including its cost).

**The galaxy system readout says whose place it is** (owner ruling 2026-08-22): name, coordinates,
`group`, then the OWNER WORD — the controlling empire as the game's own system dossier states it
(`GuiEmpire.GetLeaderName`, so an empire the player has not met reads `%EmpireUnknownTitle`
"Unknown Empire"), the game's `%MarketplaceScreenNoOwnerTitle` "No owner" where the map shows no
colony the player can see, and NOTHING at all for a system of the player's own — then
`%HomeSystemTitle` "Home System" (trailing space trimmed) on any empire's home system whose owner
the player can see, then the rest as before. The POPULATION FIGURE left the spoken readout: it is
a line of the system's own dossier, which is now a node, and it stays in the review buffer.

**Region keys added by the one-per-row rollout (2026-08-18)**, all key-only unless named — labelled
ones carry drawn text or the named ModStrings word: `system:constructibles/filters|list` and
`system:hangar/toolbar|ships` (labels Filters/Available; hangar toolbar = Actions; same pairs under
the `empire:` prefix on the detail tabs); `economy:luxuries/legend`, `economy:strategics/legend`,
`recipe:luxuries/legend`, `recipe:strategics/legend` (since 2026-08-19 the caption alone — all four
grids are 8-column tables and the items live in the sheet's own `reg:0`; the `/items` keys are
retired);
`laws:detail/{law,effects,action}` ("Effects" labelled); `population:detail/affinity`,
`population:thresholds`, one per captioned block, `population:detail/assimilate`,
`population:politics/{intro,traits,reactions}`; `election:local/{title,trends,empire}`;
`hero:ship/{characteristics,modules,figures}`;
`hero:tree-stats/{completion,starting,mastery,relics,box/N}` (2026-08-20, one per drawn box in the
skill page's statistics column — `box/N` is the fallback for a box the mod does not recognise);
`troops:evolution/caption` + `/type/<i>`;
the notification popup's top/bottom control regions (since 2026-08-19 those two regions live on a
stop of their own, `notification:controls` — the title-bar strip then the bottom bar, whatever
buttons this popup added to it included — beside `notification:content`, which is the empire-info
band then the body; focus still starts on the content and Tab wraps between the two. The popup
declared no stop of its own before that split, building into the builder's auto `stop#0`).
ControlIds retired by
the rollout (per-stop
cursor memory for them is gone): the ~25 caption rows converted to labels (each named in the batch
reports in the session ledger), `hud:view-title/name` everywhere, the faction chooser's
`faction-choice:hull` readout and its Previous/Next hull buttons (replaced by
`faction-choice:hull/<i>` pager rows), the tutorial popup's page controls (replaced by
`tutorial:page/<i>`), and (2026-08-19) `tactics:available-count` and `tactics:deck-caption` — the
deck editor's two stops are labelled instead ("Available" mod-worded, the set by its drawn
"Tactics" caption). New walkable ids: `diplomacy:center` (whose ring is centred).

**A block's caption names the block, never a row of its own — unless the caption carries a
tooltip** (owner ruling 2026-08-22, generalised from the minor-diplomacy design; `Captions` —
`docs/helpers.md`). A block's NAME is a spoken level with no review buffer behind it, so a caption
the game hung a sentence on has nowhere else to put its words and stays a row as well; the
announcer drops the level whose label the row below repeats (`GraphAnnouncer.DuplicatesNext`), so
the word is still said once. Which of the two a caption is, is asked of the WIDGET every build,
never written into a screen: the same prefab caption carries a sentence on one window and nothing
on the next. Screens converted: the population overview's people name (`population:affinity`, the
"Imperials" row is now the region's name), and the economy, senate, recipe and negotiation
headings, whose caption rows are now conditional rather than unconditional (`economy:*`,
`senate:*`, `recipe:*`, `negotiation:pressure-title`) — the negotiation pressure band is
additionally named by the game's own drawn title ("Pressure"/"War Exhaustion") with the mod word
`negotiation.pressure` left only as the fallback. Not converted, and known: `SidePanels.Effects`
declares a `PanelFeatureEffects` caption as a row unconditionally, because that collector fills a
flat cell list with no builder to push a level on.

**Minor Civilization diplomacy is named entirely by the game** (2026-08-22). Screen name = the
window's own title plus whose window it is ("Minor Civilization diplomacy, Niris"), with
`screen.minor-diplomacy` left as the fallback; the mod strings `minor.identity`, `minor.relation`
and `minor.gains` are retired and the `minor:gains` stop with them. Four stops:
`minor:identity` — the window title as its first row (that title carries the only sentence about
what the window is for), then the drawn empire name as a `PushContext` level over the regions
`minor:identity/{about,traits,planet-effects,opinion}` ("Traits" and the two panel-feature
captions are the game's words); `minor:relation` — named "Diplomatic Relation" with that
caption's own row, regions `minor:relation/{state,rewards,modifiers}` plus the gauge's
`minor:gauge/tooltips` "Tooltips" region (the four band sentences the prefab hangs along the
gauge, one node each, hidden while at war exactly as the game hides them); `minor:actions` —
named by the game's "Actions" caption, which it does draw, with that caption's own row
`minor:actions-title` (the `diplomacy.actions-band` mod word is the fallback here now);
`minor:treasury` unchanged. The identity panel is declared COLUMN BY
COLUMN, not by drawn row: the lore paragraph is one tall block beside three short ones and the
rectangle banding interleaved them.
Both caption rows are resolved from the LABEL the prefab names uniquely
(`RelationInfoTitle`/`ActionsTitle`) and read off its PARENT group, because the window draws the
word on the label and hangs the sentence on the group, and three different groups in it are called
`TitleGroup` (2026-08-22 live fix: asking for `TitleGroup` answered with the faction banner, which
named the relation panel "Niris" and left both sentences with no surface — es2-facts). **The same
propagation reached the Academy and pirate windows**, whose action bands carry the identical shape:
`academy-diplomacy:actions-title` and `pirate:actions-title` now name those stops by the game's
drawn "Actions" and carry its sentence, with `diplomacy.actions-band` as the fallback. Both are
fixture-blocked (neither window can be opened in `[Beginner] test`), so that pair is prefab-verified
only. The Academy's own `RelationInfo/TitleGroup` ("Status") is NOT converted — its stop is still
named by `academy.relation`, awaiting the owner's ruling with the rest of that screen's wording.

**The population overview's collection track says the number and the state** (2026-08-22): the
"Collection status" caption keeps a row (it carries a tooltip) and that row carries the current
count, and each threshold reads "{n} population, reached"/"not reached"
(`population.threshold-reached`/`-not-reached`) with the bonus's effect lines reviewable rather
than announced. "Collection Effects" is gated on the rows the table is DRAWING and says the game's
own "No Effects" when there are none. The reactions region is named by the wheel's own title
("Reaction to Political Events"), and the six party dossiers on the legend beside it are a
`population:politics/parties` "Tooltips" region after the sectors, which keeps the sectors the
stop's primary rows. The "Collection status" row's sentence is read off `CollectionUnlockGroup`
rather than off the `Title` label inside it — the same split-caption shape as the minor window
(2026-08-22 live fix: the row existed and carried no explanation).

**The first-contact card names its two uncaptioned figures** (2026-08-22): the
`MinorEmpireMetNotificationWindow` card draws "None"/"Unknown Empire" and the relation state beside
bare icons and puts the captions on the icons' tooltips, so those two rows are declared with the
game's own `%MinorFactionCurrentAllyTitle`/`%MinorFactionRelationTitle` as their names and the
drawn words as their values ("Ally, None"). Declared for that one prefab, not as a rule over every
popup's drawn body.
**The galaxy view names its four PANELS** (2026-08-19) with `GraphBuilder.PushContext` levels, said
once on arrival and never repeated while walking inside: Galactic Map (`galaxy.map-panel` — renamed
from "Map" 2026-08-22, owner ruling: the panel Ctrl+G goes to says what it is), Quest
(`hud.quest-panel`), Notifications (`hud.notifications-panel`) and View Controls
(`hud.view-controls-panel`). Quest and Notifications ride the one shared `GlobalHud` contribution,
so those two words are said on every one of the thirteen screens that draw those panels; "View
Controls" is the galaxy's alone (gated on the zoom ladder no other page passes) and is the one name
that overrides a word the game DRAWS — "GALAXY VIEW" on `TopTitlePanel`, owner ruling 2026-08-19,
because the view's name says which page the player is on and the screen has already said that
(es2-facts).
**The Turn log** (2026-08-20): a second notifications stop, `hud:turn-log` (context word
`hud.turn-log-panel`, "Turn log"), rides the shared `GlobalHud` contribution immediately after
`hud:notifications` on all eleven HUD pages. The game's own notifications keep the first stop
— `GlobalHud.Notifications` now filters `ModNotification`s out — and the mod's (sightings,
arrivals, sieges, dispatches; `ModNotifications`) live in the second, grouped under
`PushContext` regions `hud:turn-log/turn/<n>` ("Turn {n}", `hud.turn-log-turn`), newest turn
first, arrival order within a turn. Enter opens the shared popup and Backslash dismisses — the
existing stop's behaviors, NO new bindings — and the stop is absent while the log is empty
(owner ruling 2026-08-20). Rows carry no tooltip section: the icon tooltip is the title again
(es2-facts). And the popup's Minimize hands back to the stop that OWNS the minimized
notification, not a remembered one — the popup's own Previous/Next walks game↔mod inside one
popup, so the way out is asked of the notification being put aside
(`NotificationScreen.HandBackOnMinimize`).
**Each of those two stops ends with a "throw them all away" BUTTON** (owner ruling 2026-08-23; no new
key binding — it is reached with the arrows and pressed with Enter, and pressing says nothing of its
own, exactly as dismissing one row does). `hud:notification/dismiss-all` ("Dismiss all
notifications", keyed on the game's own `BaseTriangleBackground` control) runs
`GuiNotificationService.DismissAllGuiNotifications()` — the very call the game's Alt+right click on
that triangle makes; the handler's other branch, Shift, is `HideAllGuiNotifications()`, which only
closes popups that happen to be open and is not offered. `hud:turn-log/dismiss-all` ("Dismiss all
Turn log entries", a region of its own after the turn regions) discards only the mod's own
notifications, one by one, the way Backslash does on a row. Consequence measured 2026-08-23 and left
as the shipped behaviour: the game's list is ONE list, so the notification button empties the Turn
log too — as the mouse's Alt+right click does. Both stops are absent while their list is empty, so
neither button is ever offered over nothing.
**The shared HUD's empire stop carries a row region per drawn band**, on every page in the game:
`hud:empire/{controls,key-resources,research,strategics}` (labelled Controls / Key Resources /
Research — reusing `galaxy.research` — / Strategic Resources) plus the seven faction bands
`hud:empire/{lifeforce,genes,singularities,golden-age,pirate-mark,honor,relics}` (Essence, Manage
Population, Singularities, Golden Age, Pirate Mark, Keii, Relics — the game's own words except
Singularities and Pirate Mark, which have no standalone title key in the corpus and ship as
`hud.singularities-panel` / `hud.pirate-mark-panel`). No new stop and no new ControlIds: the regions
are `PushContext` levels around each MEASURED row, the word riding on that row's cells, so a line two
panels contribute to gets neither word nor key (`hud:empire/line/<n>`) rather than the leftmost
contributor's.
**The lobby's competitor bands are named** (2026-08-21): the New Game screen's Competitors panel
already jumped band to band with Alt+Up/Down (`newgame:competitor/<i>`, one per drawn slot), and each
band is now a `PushContext` level saying "Player {n}" (`new-game.player`) — said on arrival in the
band and never while walking its four rows. `n` is the slot's PLACE IN THE PANEL counted in drawn
order (the grid is four across, so the second row starts at Player 5), not `LobbySlot.Index`, and the
player's own Empire panel is a stop of its own and is not counted. The game captions every slot "AI",
so nothing it draws tells two of them apart; multiplayer draws the same panel from the same class and
gets the same words. Cost of the level, as ui-navigation warns: positions re-base, so a row now reads
"3 of 4" within its band instead of counting the whole stop.

**Ctrl+I is the galaxy's INSPECT MODE** — a square of galaxy the player moves about the map and hears
the contents of, instead of walking the tree (`GalaxyInspect` — `docs/helpers.md`). Ctrl+I is free in
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
Every other key falls through untouched. **Ctrl+I only ARMS
the mode — it is not a toggle**: pressed again while the cursor is up it is taken and does nothing,
silently, on the same ruling as Enter on an empty cell (the key is pressed speculatively mid-sweep,
and dropping the cursor there would cost the player the cell they were standing on). The three ways
out are Escape, a landing Enter made, and the map going away. **A LIVE TYPE-AHEAD SEARCH TAKES
ESCAPE AHEAD OF THE MODE** (owner ruling 2026-08-19): the first Escape clears the search and leaves
the player IN the mode — the square still drawn, the arrows still the cell's — and the next one
exits. The rule is not the mode's own: `ModEntry.Dispatch` routes Escape past `Screen.AnyKey`
whenever `GraphNavigator.SearchIsActive`, so every mode of that shape obeys it and the innermost
mod-invented surface is always the one Escape ends. The claim never changes hands (`claimsBack`
reads true with either alive, or both). **A MODE OF A WIDGET, NOT OF THE
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

**Shift+arrows go to the next INTERESTING cell** (2026-08-19; `CellSkip` — `docs/helpers.md`): the
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

**The galaxy's SCANNER is on the Page keys with a modifier, and it is NOT a mode** — no arm key,
nothing to exit, Escape never touches it; the chords are live for as long as the tree cursor stands
on the MAP stop, alongside tree navigation and alongside the inspect cursor (`GalaxyScanner` —
`docs/helpers.md`). **Ctrl+PageUp/Down** cycles the category (skipping one with
nothing in it), **Shift+PageUp/Down** the subcategory within it (skipping empties),
**Alt+PageUp/Down** steps one thing at a time and wraps at both ends (the only
repeating chord of the three), and **Alt+Home** goes to what it is pointing at — the inspect cell
onto the thing's ROUNDED spoken pair while that mode is up, otherwise the tree cursor onto the
thing's own node. The FIRST scanner press of a game says where the cursor already is instead of
moving it.
**THIRTEEN categories, in the owner's order (2026-08-22)**: Systems; Colonizable Planets;
Unexplored; Anomalies; Curiosities; Luxury Resources; Strategic Resources; Contested Influence;
Fleets; Probes; Ally pins; Obliterator missiles; Quest markers. Four of them build their
subcategories from WHAT WAS FOUND — one column per anomaly definition, curiosity, or resource,
sorted by the localized name, behind the columns the category writes down for itself — so both the
column a category was left in and the
row the cursor stood on are remembered by IDENTITY (name, key) rather than by index
(`ScannerCursor.Reseat`, `ScannerTable`): a kind appearing ahead of the remembered one must not
move the cursor to it. In an "all" column such a row reads "{kind} on {planet}"; in a kind's own
column it is the planet alone. Three of the four write down only "all"; **Curiosities writes down
three** (owner ruling 2026-08-23) — "all", **Explorable** (the game's own `CanBeSearched` for the
empire) and **Insufficient Expedition Power** (that call's `EmpireExpeditionPowerTooLow` failure,
which is what the card draws a padlock for) — and then the kinds. Colonizable Planets has NO "all": its two columns are unoccupied
(the game's own `IsColonizable`) and occupied (settled by somebody else and settleable by this
empire's technology — the "able" half alone), and its row carries the whole description of the
world (size and type, resources, anomalies, curiosities, max population, the five outputs as
NUMBERS). Unexplored is "all"-only and its things are EDGES, not places: every drawn lane or
wormhole whose far end the player has not perceived, named from the end they can see
("Star lane 3 from Rigel heading west").
**Alt+Home now MOVES THE CAMERA on every category** (owner decision 2026-08-22): the landing is
the page's own locate landing — focus the node, and ask the camera rule for the place — for anything
standing at a node, a planet and a lane included (they land on their own node under their system
and zoom to the system). A thing that stands at a bare point (a fleet under way, a probe, a pin, a
missile) has no node to zoom into and gets the inspect cursor's own `CenterOn` slide instead.
Contested Influence keeps arming the inspect cursor and is unchanged.
**Contested Influence is the one category whose Alt+Home TURNS THE INSPECT CURSOR ON** (owner
decision 2026-08-21, `GalaxyInspect.ArmAt`): its results are squares of the player's own reach a
rival's field now wins ("Near Dusay, −7, −1, …"), and a square has no node, no row and nothing to
select, so leaving the cursor alone could only move the camera and say nothing. Arming announces
exactly what Ctrl+I announces and opens the cell ON the square, so the arrival is heard once —
"Inspect mode, Cursor 1 by 1", the constellation crossing, the influence lines, the pair. With the
cursor already up it is an ordinary jump. No other category force-arms anything. Its one
subcategory is "all": every result is the player's own ground being taken, so an affiliation scope
would have exactly one answer. 

**THREE CATEGORIES THE PLAYER MAKES, AFTER THE THIRTEEN** (2026-08-23; moved to the BACK of the
cycle 2026-08-24, `ScannerCustomSlots` → `docs/helpers.md`). Three FIXED slots, each empty or
holding `{name, selectors, keywords}`; "delete" is clearing a slot, there are no ids and no reorder.
A configured slot is a category the cycle reaches LAST, in slot order, with the player's own name as
its category name; an unconfigured slot, and a configured one that caught nothing this press, are
skipped by exactly the rule that skips a built-in category with nothing in it. **They went in FRONT
first and that was wrong**: the cursor starts at category zero and the first scanner press of a game
says where the cursor already stands rather than moving it, so a player who had configured nothing
heard "none found" as the first thing the scanner ever said to them. **The slot rows are always in the table**,
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
`galaxy.scanCustom1Next`…`3Prev`, so all six are rebindable rows of the Keybinds tab). One key per
SLOT, not per category name — the key means the same thing whatever the player renames. A press
walks that slot's "all" list FLAT, nearest first from where the player is reading, SAYS the landing
as a scanner result would ("Rigel, -16, -5, 3 south, 1 of 21") and then performs the category's
ordinary Alt+Home landing, so the tree cursor and the camera go there and the page announces the
arrival after it. Each landing becomes the place the next press measures from, which turns the key
into a nearest-neighbour hop across the map: the walk RE-ANCHORS whenever the player has moved off
the place it last swept from, and a press while parked on the entry it is standing on steps ON
rather than re-landing (`ScannerWalk`). Shift is the same list reversed, so from a landing it goes
to the FARTHEST result rather than back the way it came — the nearest-first ordering read backwards,
soc-access's own rule. An EMPTY slot says "No custom category on {key}", naming the key off the LIVE
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
the mode is up (same-name band boundaries included — es2-facts), and the view again on the way out,
so handing the key back cannot drop the player into an unannounced mode. Every other binding is
claimed outright. While something is carried, **Escape puts it
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
game taking the keyboard). Edit fields are entered explicitly and share ONE editor
(`TextFieldEditor`): Enter on the field hands the keyboard over ("editing"); typing echoes
each character and Backspace speaks the deleted one; caret moves (arrows/Home/End) speak the
character under the caret; Enter commits ("edited") and the SURFACE STAYS — the mod takes
the key from the game's validate dispatch, so committing an edit never performs the
screen's action (saving, renaming are the Save/Confirm buttons; a game that will not take
a value refuses at its own button); Escape — or ANY loss of the keyboard
that is not Return: a click elsewhere, a right click — cancels, restoring the pre-edit text
and saying "Cancelled", before a second Escape closes the surface. The chat box is the one
exemption: its Enter sends, through the game. Role words: "editable",
and "numeric editable" for the stepper boxes whose Left/Right adjust announces the new value
(owner rulings 2026-08-17; the cancel-restore is wholly mod-authored — the engine has no
cancel semantics of its own, and the hand-over waits for the activating key's release).

**The research wheel is searched by what a technology GIVES, not only by its name** (2026-08-22).
`ResearchScreen.TypeAheadScope` still covers every dot the wheel would draw, wherever it is buried,
but each one now answers with its title and then, after a comma, the same terms the GAME's own
search box looks through (`TechnologyLookupPanel.BindTechnology` — es2-facts): the technology's
localized keywords, and for every unlock the empire already passes the `UnlockAvailability`
prerequisites for, that unlock's title, its keywords and the localized titles of its category and
sub-category. So "Impervious" finds Survival Suits and "Miners Union" finds Galactic Commodities
Exchange, while `TypeAheadSearch`'s before-the-comma rule keeps a title match ahead of an unlock
match. The terms are built once per technology and kept until the turn changes — which is when an
unlock's prerequisites can move — so a ten-letter search composes 107 strings, not 1070.

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

**Ctrl+L GOES TO WHERE A THING HAPPENED** (owner-approved 2026-08-22; `UiActions.GoToLocation`,
`ModEntry.BindKeys`, `GraphNavigator.GoToLocation`/`TakesGoToLocation`): the game's own show-location
button, from the keyboard. Ctrl+L is free in this game — the input manager binds nothing to L at all,
confirmed live with a physical press that moved neither the camera nor a word — and the letter costs
nothing, since A–Z are already claimed by type-ahead. It is **exactly what the mouse does clicking
that button**, and it is offered exactly where the popup DRAWS one: 41 of the 69 prefabs bind a
show-location button their layout never holds (es2-facts), and the key follows the layout, not the
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

**ONE landing on the galaxy page** (owner ruling 2026-08-22; `GalaxyHudScreen.GoTo(MapTarget,
MapCamera)`, `MapLandings.Decide` — `docs/helpers.md`). Five things used to send the player somewhere
on the map and each answered the same three questions for itself; they disagreed, and the scanner's
answer for a PLANET was wrong (it jumped the inspect CELL onto a world, which the cell cannot read).
The table, by what the thing IS:
- **a place** (a star system, a special node): the free cursor STAYS UP where it is up, the cell goes
  to the place's own tile, the camera ZOOMS in either way, and the tree cursor is moved to the
  system's node — silently while the cell is up, to be felt when the mode ends.
- **a thing at a bare point** (a fleet under way, a probe, an ally's pin, a missile, a quest marker
  planted out in the open): the same, except the camera SLIDES onto the point; in the cell the cell's
  own slide is the whole camera move.
- **a world and everything drawn at one** (a planet, an anomaly/curiosity/resource on one, a
  colonizable world, **and a quest marker standing AT a system**): the free cursor **ENDS FIRST**
  ("Exited inspect mode", then the landing), the branch opens, the camera zooms in. A marker at a
  system is a child of that system, not a thing of its own out on the map.
- **a point with nothing on it**: a DEFECT, not a behaviour (owner ruling 2026-08-22, superseding the
  force-inspect proposal): everything the game can point the player at is supposed to have a row, so
  the request is logged where a sweep can find it (`/log?grep=galaxy go-to`), the existing "Shown on
  the map" line is said, and NOTHING moves — no cursor, no cell, no arming. Never "nearest system" as
  a stand-in: a bare position is matched against what the map draws with a tight coincidence
  tolerance (1.5 galaxy units, against a 6.7-unit closest-neighbour), not with a neighbourhood
  radius. Contested Influence's own Alt+Home still ARMS the cursor — a square of sky is a thing by
  design, not a lost one.
Out of the cell **the landing's own announcement is the whole utterance, once, and it is composed
after the map has caught up with the camera**: `Screen.LandingSuspended` now also covers
`GalaxyViewLevels.CameraSettling` plus a twenty-frame tail, and a suspended frame holds even a
control that is already declared (`FocusRequest.Step`). Measured: a scanner jump to Osulo I used to
say "Osulo I, Colonized, 1 of 7" mid-flight and now says "Osulo I, group, Medium Mediterrane.,
Colonized, collapsed, 2 of 8".

**QUEST MARKERS ARE NODES** (owner ruling 2026-08-22; `QuestMarkers` — `docs/helpers.md`). A marker
standing at a system is a child of it, declared LAST — after the planets, the lanes and the fleets —
keyed `<system>/marker/<pin guid>`; one planted out in the open (on a fleet crossing a lane) is a
top-level row of the galaxy's drifting region, `galaxy:marker/<pin guid>`, beside the probes and the
missiles. Both are named by the game's own quest title in the tracked or the ordinary form
(`galaxy.system-quest-marker[-pinned]`, the phrase the system's buffer already used), carry the
step's objective in the review buffer, have **no tooltip** (the game hangs none on a marker) and
**Enter is INERT** — a pin is not clickable on the map either, and there is no journal-opening
gesture to invent. Backslash likewise. ONE enumeration feeds the system's buffer lines, the marker
nodes, the scanner's Quest markers category (which now includes the open-space ones and lands on the
MARKER, not on its system) and the inspect cell — a cell holding a marker reads it after the places
and before the lanes, and Enter on a cell whose only thing is a marker exits and lands on its node.

**The population side panel's rows are real nodes** (owner ruling 2026-08-22, retiring the batch-2
compromise): a population entry ("Imperials, 3") is an expandable group whose "Tooltips" region holds
the political parties nested in its dossier (`Cells.Declare` gives a cell a subtree; `PoliticsDossier`
supplies the parties). They were the row BELOW their population until now, because a side panel emits
a flat list of cells and a cell could not open a subtree.

**THE MOD'S OWN SETTINGS ARE A MENU ENTRY, NOT A HOTKEY** (owner ruling 2026-08-23;
`ES2Access/UI/ModOptions/ModSettingsNode.cs`). One synthetic `GraphNodes.Button` labelled
"Mod settings" (`mod-settings.entry`), declared immediately AFTER the game's own Options entry
on the main menu (`mainmenu:mod-settings`, after the `MainMenuSettings` group closes — Options
is an entry with a flyout, so the node has to be a sibling of the group and not one of its
children) and on the pause menu (`gamemenu:mod-settings`, after the entry whose button is wired
to `OnOptionsCb`). Nothing is drawn for it, so a sighted player never meets it — the shape the
graph already uses for things the game does not draw. **No key binding, and no gating**: both
menus are static and are exactly where the game opens its own Options, and Apply, Cancel and
Escape all pop back to the menu by the game's own hand. Enter shows the mod's cloned options
window with the skin matching the menu (`OutGameSkin = !Gui.IsInGame`). Ctrl+M was weighed and
set aside; it remains verified free should a hotkey ever be wanted.

**The mod's settings window is the game's options window, and reads as one** — same tab bar,
same rows, same button bar, same Escape/Apply/Cancel, so `OptionsScreen` serves it with one
change (`Window()` answers whichever instance is shown). Its screen name is "Mod settings"
(`screen.mod-settings`), not the game's "Options", because the two are the same window class
and a player arriving must not be told they are in the game's settings. Categories are a
data-driven list (`ModOptions.Categories`) drawn in list order.

**The Keybinds category holds one row per mod ACTION**, in the order `ModEntry.BindKeys`
registers them — which groups them by family already: the cursor's keys, then the map's, then
the review buffer's. Each row is the game's own `OptionKeyMappingItem`, so it reads exactly as a
Controls-tab row does ("Move down, button, Down Arrow, <description>, 2 of 50"), Enter captures
the primary and Backspace the secondary until the table redesign lands (ruling 6). Every row's
title and description are the mod's own strings (`action.<action key>.title` /
`.description`) — mandatory, not cosmetic: a localization key the game has no row for is drawn
and spoken RAW (es2-facts). An action bound to more than two chords keeps the extras, which stay
live and are not offered by any row (today only `galaxy.inspectGrow`, four chords because "+"
is three of them on a common keyboard). **No mod↔mod conflict check** (ruling 10): the rows
carry `AcceptsMultipleKeys`, which also stops the game's Controls tab stealing a chord from one
of them. The mod↔game informative warning (ruling 9) is not in yet.

**A rebind persists on Apply and reverts on Cancel** — the window's own semantics, with no hook
on either button: the settings file is written when the window HIDES, by which point Apply has
committed or Cancel has restored (`ES2Access/UI/Settings/ModSettings.cs`). The file
(`<plugin dir>\settings.cfg`) holds one `keys.<action key>` line per action the player has
actually MOVED, in the game's own `InputBinding.ToRegistryString` form; moving a key back to its
default takes the line out again, so a later build changing a default reaches everybody who
never touched that key.

**THE SCANNER TAB AND THE THREE CUSTOM-CATEGORY TABS EXIST ONLY IN A GAME** (2026-08-23; DRAWN
with the game's own widgets since 2026-08-24, `ES2Access/UI/ModOptions/`). In a game the window's
tab bar is **Scanner, Custom category 1, Custom category 2, Custom category 3, Keybinds**; on the
main menu it is Keybinds alone, because the columns a category is written out of are a fact about
the galaxy being played. The window is rebuilt when the player crosses that line
(`ModOptions.Tick`, only ever with the window down).

**The Scanner tab is three drawn buttons**, one per slot, reading "Custom category {n}: {name}"
("empty" when the slot is unset). Enter opens that slot's own tab and puts the cursor on its first
row. (History: this tab held an INVISIBLE tree of mod nodes until 2026-08-24 and drew nothing at
all, which the owner rejected. A per-slot editor in a second cloned WINDOW was measured and is not
possible - es2-facts, "two `GuiModalWindow`s on one stack".)

**A slot's tab is flat**, and every row on it is a row the game draws:
- **"Name"** - a text box holding the category's name. Typing a name into an EMPTY slot is what
  fills it, and the rest of the page appears; until then the tab holds this box alone. Blank is
  refused ("A custom category needs a name"), and a name already taken - by a built-in category's
  live localized label or by another slot, case-insensitively - is refused with "{name} is already
  the name of a category". Both put back what was there, drawn and spoken.
- **"Keyword {n}"** - one box per keyword. Editing one changes that keyword IN PLACE (its position
  is its column order); blanking one takes it out, speaking "{kw} removed".
- **"Add keyword"** - an empty box after them. What is typed there is added and the box blanks
  itself; a word already in the category is refused with "That keyword is already in this custom
  category".
- **"Clear this custom category"** - a drawn button, no confirmation (Cancel is the undo), speaking
  "Custom category {n} cleared" and leaving the page as the name box alone.
- Then **one SECTION per built-in scanner category**, in scanner order. Its caption is drawn as a
  row and spoken as the section's NAME - "{category}, {n} selected" - never as a control of its
  own, so **Ctrl+left/right walks the thirteen sections** and the rows above the first caption are
  a section too. Under each caption is one checkbox per column: the columns that category writes
  down, then - for the four derived ones - the kinds found THIS game (keyed by the definition's own
  name, labelled by its localized title), then any stored selector this galaxy has no column for,
  ticked and read as "{key}, not found this game" so it can be taken off.

Every one of those rows is the game's own prefab over a per-row provider object, so the drawn page
and the spoken page cannot disagree. The text rows are edited by the mod's ordinary text editor
(Enter ends the edit and nothing else, Escape puts back what was there); the commit itself needs a
Harmony prefix, because the game's own text-field row assigns the label OBJECT into the option
(es2-facts).

**Apply and Cancel are the window's own, with nothing re-implemented.** The edits live in a
`ScannerCustomSlots.Copy()`; the Scanner panel carries ONE invisible game option whose value is
"does the copy differ from what is saved", so the window's own machinery lights Apply, raises its
own "%OptionExitWithoutApplyMessage" on Escape or Cancel, and throws the copy away through that
option's setter when it restores. The copy is written through on the window's hide, which is the
same save-on-hide the keybind rows already have. **Speech order**: an edit that changes the SHAPE
of the page (a name that fills a slot, a keyword added or removed, a clear) rebuilds the page from
the pump rather than from inside the engine's own focus change, and the sentence that goes with it
is said there - so a refusal is heard after the control it left unchanged.

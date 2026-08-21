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
(`OnSelectRange`). There is NO reorder chord: moving an item within its list is a drag like any other.
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
instead) — must be WIRED if they are ever declared, never left to the fall-back.
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
**Backspace (`NodeVtable.OnSecondary`) is NOT a right click** — it is the second command on a node
that folded two of the GAME's controls into one, and the only one left is the options screen's
key-rebind row, where Enter captures the binding's first key and Backspace its second. Anything the
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
(they hang under the system, a thing not a place). The scanner has SIX categories —
constellations were removed 2026-08-20 (owner: not a discrete point). **A targeting cursor
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
to "Map" when the mode ends). Arming while the cursor is inside the stop adds no extra utterance
(the announcer diffs on focus change); Tab-away/Tab-back re-speaks the instruction, which is the
point; child node ids and the cursor survive the rename.

**Expanding a galaxy system node (Right) also brings the camera in** through the game's own zoom
(`GalaxyViewLevels.ZoomTo`); Enter is unchanged, Backslash remains the way out, and **collapse
un-zooms** while `GalaxyViewLevels.FocusedSystem` is still that system — a camera the player has
since moved elsewhere is left alone, so collapsing moves nothing there. **A starlane is a LEAF and
Right on a named one TRAVELS** (`NodeVtable.OnFollow` → `KeyGraph.TreeMove.Followed`, consumed
silently): the cursor lands on the destination system's ONE node at the root of the systems stop,
that branch opens, and the camera goes there through `ZoomTo` — never `ZoomIn`, because travelling is
not a click and must not confirm an armed targeting mode (measured: the mode survives Right and is
still ended by Enter on the same lane). The landing's ordinary announcement is the whole
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
**The galaxy view names its four PANELS** (2026-08-19) with `GraphBuilder.PushContext` levels, said
once on arrival and never repeated while walking inside: Map (`galaxy.map-panel`), Quest
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
(a jump to the map stop was tried and vetoed, 2026-08-17). Escape and the size
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
`docs/helpers.md`). **Ctrl+PageUp/Down** cycles the category (systems ↔ fleets, skipping one with
nothing in it), **Shift+PageUp/Down** the subcategory within it (all / friendly / neutral / enemy,
skipping empties), **Alt+PageUp/Down** steps one thing at a time and wraps at both ends (the only
repeating chord of the three), and **Alt+Home** goes to what it is pointing at — the inspect cell
onto the thing's ROUNDED spoken pair while that mode is up, otherwise the tree cursor onto the
thing's own node. The FIRST scanner press of a game says where the cursor already is instead of
moving it. **Bare PageUp/PageDown remain the GAME's keyboard zoom**, Ctrl+Home stays the review
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

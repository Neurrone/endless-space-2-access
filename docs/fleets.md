# ES2 facts — fleets

Fleets, their orders and movement, the pathfinder and interception, the fleet panel, selection
and ship transfer. Index and charter: `README.md`.

## Fleets and movement

- **A docked fleet's position IS its star's.** `FleetPosition`'s node-position setter takes
  `service.Get3DPosition(nodePosition)`, so `Fleet.GalaxyPosition` for anything in orbit equals the
  `StarSystemNode`'s exactly (measured: `GalaxyFleet.transform.position` equals
  `Fleet.GalaxyPosition` for a fleet under way too). Turning a reveal POINT back into a thing must
  therefore prefer the containing place on a tie, and find a parked fleet by its BERTH — the
  `DockingSlotCursorTarget`'s own transform, which is what `EndTurnWindow.SelectIdleFleet` aims the
  camera at. Galaxy scale for the radius: nearest-neighbour system spacing measured at 6.69 minimum,
  10.61 mean over 136 systems.
- **Which fleet of a shared berth the next-idle-fleet button meant is only knowable a few frames
  later.** `EndTurnWindow.SelectIdleFleet` (:1387-1409) asks for the camera FIRST and selects the
  fleet from `SelectFleetWhenViewReady`, a coroutine that waits out the 0.3 s slide. So the selection
  standing when the reveal arrives is the PREVIOUS answer; a landing that reads it immediately picks
  the wrong fleet whenever two fleets are parked at the same system (measured).
- **Which label the map draws a fleet with is the map's answer, not a flag of ours.** A fleet in
  orbit is drawn by the DOCK label of its slot (`DockLabelsWindow`→`DockLabel`, matched through
  `DockingSlot.GalaxyFleets`), one under way by its own (`FleetLabelsWindow`→`FleetLabel`, matched
  through `GalaxyFleet.Fleet`); exactly one is bound at a time, so searching both windows is the
  whole test. **The tooltip the engine DRAWS is the lozenge's** (`GarrisonsLabelButton.Tooltip`,
  i.e. `lozenge.AgeTransform.AgeTooltip`), not the label's `CenterTooltip` — both are filled from
  the same fleet data and both read `Class=FleetGroup`, so declaring the label's leaves the node
  expecting a tooltip over a review buffer that never fills (`DrawnTooltip` checks bind identity).
  Point at, and declare, the widget the pointer lands on. A dock label covers the whole SLOT, so
  two fleets parked together share one "2 Fleets" tooltip - which is what the game shows a hoverer.
- **The two fleet repositories PARTITION the map's fleets, and each label window filters further.**
  A docked fleet is not in `IVisibleGalaxyFleetRepositoryService.GalaxyFleets` at all (measured: two
  fleets parked at Dusay, `GalaxyFleets.Count == 0`), so "on a lane" needs no is-it-docked test - the
  repository the lane reads has already answered it. `FleetLabelsWindow.ShowAllLabels` (:79-81) then
  adds `(int)Fleet.Visibility[Gui.PlayerEmpire as MajorEmpire] >= 2` before drawing, which the
  repository does NOT apply, so anything claiming visual parity has to apply it too.
- **"N Fleets" is `GuiFleetGroup.Title`, and the map's own friend/neutral/enemy split is NOT the
  diplomatic one.** The heading on every lozenge tooltip picks between
  `%PanelFeatureFleetCount{Player,Enemy,Neutral,Allied}{Single,Plural}` off the diplomatic relation to
  the group's owner (`GuiFleetGroup.cs:40-76`), so the phrase for two fleets is "2 Fleets", "2 Enemy
  Fleets" or "2 Allied Fleets" depending on whose they are. But the comparison behind that choice is
  `DiplomaticRelationState.GetDiplomaticRelationStateValue(state)` against ColdWar and Peace — and that
  function answers **-1 for every non-Major state name**, so a cold war, every minor faction, every
  lesser empire and every pirate all read as ENEMY in the count phrase. Mod policy (owner ruling
  2026-08-26, superseding the 2026-08-16 war-only taxonomy): the mod has ONE standing ladder,
  `FleetPresence.SideOf` — the game's own value ladder for majors (at most cold war = enemy, at most
  peace = neutral, above = friendly), pirates enemy unless the bought peace, any war state enemy,
  Brainwashed/Aligned/Integrated/Academy-ally friendly, everything else neutral — deliberately NOT
  reproducing the `-1` fallthrough. The count phrases (`FleetPresence.Standing`), the spoken fleet
  phrase (`FleetPhrase.Owned`) and the scanner's affiliation buckets (`GalaxyScanner.Scope`, which
  files the player's own things with friendly because the filters offer three buckets) all read it. **And the group a DOCK label counts is not the docking slot's fleet list**:
  `DockLabel.FillDockedGarrisons` (:186-212) prepends the system's own hangar garrison while it holds
  ships of the player's and no mothership is attached, so the drawn number can exceed
  `DockingSlot.GalaxyFleets.Count` (measured at Dusay: hangar present, `ShipsCount == 0`, drawn title
  "2 Fleets" over exactly the two fleets).
- **Two fleets close together on screen are drawn as ONE marker and their own labels are hidden.**
  `MergedFleetLabels` takes the labels over (`AddLabel` sets `FleetLabel.IsMerged`, the label goes
  `Visible=false, Alpha=0`) and binds its own `DualGarrisonsLabelButtons` to the whole group, whose
  `FriendlyGarrisonsButton.Tooltip` carries the `FleetGroup`/`GuiFleetGroup` dossier for all of them.
  A mod node that looked only at the fleet's own label therefore found no lozenge, declared no tooltip
  and drew nothing. The way
  back is the marker's own wrapper: `GuiFleetGroup.Garrisons` is a public list, so the fleet is matched
  by GUID against it. What the marker shows is the GROUP's dossier, which is exactly what the mouse
  gets while the map is drawing them as one thing.
- **The map's fleet lozenge carries its ship-kind badges as separate tooltips.**
  `GarrisonsLabelButton.ExplorationShipIcon`/`ColonyShipIcon` ("One of these ships is an exploration
  ship." / "… a colony ship.") are icons INSIDE the lozenge with their own `AgeTooltip`s, beside the
  lozenge's own `GuiFleetGroup` dossier (which `FleetLabel.CenterTooltip` is a second carrier of, so
  the resolver's (class, content, target) dedupe collapses the two): the mod's
  fleet row carries the `GuiFleetGroup` dossier in full and neither badge sentence.
- **A fleet's CURRENT LEG is `FleetPosition.Movement.Start`/`.Goal`, and cancelling a move does not
  clear it.** The pair names the two nodes of the lane the fleet is drawn on right now (not its
  destination - `Path.Destination` is that), so matching them against a `Link`'s two extremity
  `NodePosition`s, either way round, is the whole "which fleets are on this lane" test. Measured after
  `OrderCancelEntityAction`: `Path == null` while `IsInMovement` stays true with start and goal
  intact - a cancel does not call `UnsetMovement` - so a stranded fleet goes on belonging to its lane.
- **A leg is NOT always a lane.** Free movement (the Ctrl+right-click course, and whatever a scripted
  start hands out) sets a start and a goal with **no `Link` between them** —
  `Galaxy.GetLink(start, goal)` answers null — so the fleet is in neither `FleetPresence.FleetsAt`
  (it is not docked) nor `FleetsOn` (there is no lane to be on). It IS drawn, so
  `FleetPresence.Drawing()` (the inspect cursor's and the scanner's source) still has it. Such legs
  belong to AUTOMATED delivery fleets (`Fleet.IsAutomated`, the `AutomatedFleet` tag), which is why
  they fly with star lanes ignored. **Mod policy**: the tree hangs such a fleet under its DESTINATION alone, and
  under no system at all where the destination is unperceived — there it gets a top-level row walked
  into the system list by its own rounded pair (`GalaxyHudScreen.AddAdrift`). The rationale is parity,
  not tidiness: **the map draws where a fleet is GOING and never where it came from.** A selected
  fleet's committed path is drawn ahead of it as dots and numbered turn markers; `Fleet.Path` starts
  at the node being flown towards, so the source is not in it, and the only place the game writes a
  destination as text is owner-gated (`PanelFeatureGarrisonInfoAutomatedFleet` :77-85). A lane fleet
  is filed the same way, under the extremity its leg is flying TO and no other.
  `GalaxyHudScreen.CrossingOpenSpace` is the one test (`Linked(start, goal)` over the start node's own
  `Links`), which is also what keeps this list and `EnRouteOn` from both claiming one fleet — two
  claims under one system is a duplicate control id and throws the page out of `Build`.
- **"On a lane with no destination" does not exist: every fleet on a lane is flying to one of its
  two ends.** A fleet enters a lane's list only through `FleetPresence.Between`, which demands
  `IsInMovement` AND that the leg's start and goal node-positions be the lane's two extremities.
  `IsInMovement` is `Movement.IsValid` — start and goal are valid `NodePosition`s, nothing more
  (`decompiled/Assembly-CSharp/Movement.cs`): there is no "actively moving" sense to it, an
  out-of-movement-points fleet keeps its valid leg, and a cancelled order leaves start and goal
  intact (above). And `IPositioningService.GetGameNode` is a plain array index that answers a node
  for any valid position and throws only for an invalid one
  (`decompiled/Assembly-CSharp/Galaxy.cs` :374). So the goal of any listed fleet always resolves to
  one lane extremity. **Mod consequence**: `GalaxyHudScreen.Bound` files a lane fleet under that end
  alone with no second branch; on the degenerate failures left (no positioning service, a throw) the
  fleet is left UNLISTED rather than hosted by a guess.
- **A leg WITH a link the map does not draw is still not a free mover, and is still tree-absent.**
  `EnRouteOn` walks `LanesOf`, which drops a lane below the drawn intensity and a wormhole an empire
  cannot see, while `FreeMovingAt` skips any leg its two ends have a `Link` for. So a fleet flying an
  undrawn lane falls between them — deliberately: its road is not on the screen, and the scanner and
  the inspect cursor still reach it.
- **`FleetPresence.Selectable` refuses an AUTOMATED fleet**, so `GalaxyHudScreen.SelectFleet` is a
  silent no-op on one — any "go to this fleet" relying on selection alone reads as a dead
  key. The game's own tooltip says so: feature class
  `PanelFeatureGarrisonInfoAutomatedFleet`.
- **The map shows a fleet's SHIP COUNT only from the Visible tier up.**
  `GarrisonsLabelButton.RefreshShipCount` :203-217 adds a fleet's ships into the lozenge number only
  while `fleet.IsAutomated` — an automated delivery fleet's strength is public — or
  `(int)fleet.Visibility[Gui.PlayerEmpire] >= 3`; below that the lozenge is drawn with the fleet
  missing from its own total, and no placeholder is shown. Same test in
  `RefreshMultiGarrisonsChevrons` :219-232. **Mod policy**: `FleetPresence.ShowsShipCount` is that
  predicate verbatim, and it now gates `FleetPhrase.Composition` (the design-name ship groups that
  replaced the "N ships" total, 2026-08-26), which answers null below it — the whole part omitted,
  no placeholder. An empire's own fleets are always at full visibility, so nothing changes for them.
- **Drawing a foreign fleet's PATH is a separate and much narrower permission than selecting it.**
  `GalaxyFleetCursorTarget.ValidateSelection` :17-24 refuses only AUTOMATED fleets — there is no
  owner test, so the mouse selects anybody's fleet — but `GalaxyGarrisonCursor.RenderPath` :525
  returns before drawing anything for a fleet of another empire unless the player has
  `Empire.SeesEnemyPathfinding` AND is at war with its owner (or the owner is a `PirateEmpire` the
  player may attack). **Mod policy**: `FleetRoute.Current` asks the same question, so the
  turn-by-turn itinerary, the "arrives in N turns" part and the route review buffer are all silent
  for a foreign fleet whose path the game would not draw. Before the gate the mod read an AI's whole
  plan out of the model, naming systems the player had never seen (measured: "arrivesIn=9 places=4
  last=Fajis"). What is left for a foreign fleet is what a sighted player still has — the lozenge,
  its position, and the leg it is flying now, which is geometry on the screen.
- **`PathRenderer.RenderPath` :408-545 does NOT clip a path to what the player can see.** There is
  no visibility, exploration or fog test anywhere in it: once the cursor decides to render, every leg
  of `path.PathPositions` is turned into curve and marker data whatever lies under it, and
  `hiddenColor` is the whole path's fade-out colour rather than a per-segment gate. So a route the
  mod reads should be read to its end, with unperceived nodes left unnamed by the existing
  `MapVisibility.Perceived` gate (`FleetRoute.Named`) — there is no "the route continues beyond
  explored space" boundary to communicate, because the drawing has none.
- **The position model has no planet-level fleet state.** Docking slots and hangars key on the
  system's `GameNode` (`DockingSlotCursorTarget.GameNode`), and `FleetPosition.GetOrbit()` returns a
  `GameNode`; nothing anywhere binds a fleet to a `Planet`. So "which fleets are at this planet" is
  not a question the game can answer, and a planet readout that stays silent about fleets is matching
  the model rather than skipping a feature.
- **A fleet's status is `Position.IsInOrbit`, not `IsMoving`.** `GuiTableCellFleetStatus.Refresh`
  (the game's own fleet-list status column) draws "orbiting" + the orbit node's name when in orbit
  and "moving" + `Fleet.Path.Destination`'s node name otherwise, and never asks `IsMoving` — which
  is the `FleetActionMoveTo` tag and goes FALSE the moment a fleet spends its movement half way
  along a lane. Measured: after a posted move, `moving=False orbit=False mp=0` with the path still
  set, which an `IsMoving` test describes as being nowhere.
- **Posting a second move to a moving fleet SUPERSEDES the first** (measured: `EntityActions` stays
  at exactly one `GoToFleetAction`, with a new `Id`), so "move" stays offerable while a fleet is
  under way. Cancel is `new OrderCancelEntityAction(action.Initiator.EmpireIndex, action.Id)`
  posted through `Gui.GetActivePlayerController()`, and it leaves the fleet stranded mid-lane with
  `Path == null`.
- **The game accepts a move order to the node a fleet is already orbiting, and does nothing with
  it.** Measured on a docked fleet: `FindPath` to its own system returns a path of cost 0 and
  `GoToFleetAction.CanBeExecuted` answers true. So "would the game accept this" is not the whole
  offerability test for a destination — the fleet's own orbit has to be excluded by hand, or the
  menu grows an entry that answers Enter with silence.
- **A fleet flying towards an unexplored node cannot be re-routed until it arrives.**
  `GalaxyGarrisonCursor.GetGalaxyPathToPosition` (:469-473) has a `FailureFlags.NextNodeUnknown` for
  exactly this — a fleet between nodes (`Position.NodePosition == Invalid`) whose next node is
  unexplored — and the pathfinder underneath simply returns null. Measured: a fleet stopped mid-lane
  bound for an unexplored system answers `FindPath(its next node → its own home system)` with null,
  and CANCELLING the move does not restore it (`Path == null`, `IsInMovement` still true, next node
  still the unexplored one). Every "where could this fleet go" answer inherits that.
- **A STAR LANE is a move target in its own right, not just a road to one.**
  `GalaxyGarrisonCursor.GetGalaxyPathToTargets` (:329-342) resolves the hovered galaxy node OR a
  `GalaxyLinkCursorTarget`'s `Link`, and `GetGalaxyPathToLink` builds the route in two halves: the
  ordinary path to whichever extremity the fleet comes in by, plus one `AddMovement` transition
  towards the other. Measured through the mod's own menu on a fleet docked at one end: the posted
  order's `Fleet.Path.Destination` is the FAR node — which is how a fleet is pointed down a lane
  into the dark, since that far node need not be explored. A fleet already flying that same lane is
  answered with the path to its own `NextValidNodePosition`, the game's way of saying "you are
  already doing that".
- **Ctrl held during the move gesture asks for `PathfindingFlags.FreeMovementOnly` — and without
  the technology that is the game's own no-op.** `GalaxyGarrisonCursor.GetGalaxyPathToPosition`
  (:453) reads the physical modifier; but `PathfindingManager.GetTransitionCost` (:219) re-admits
  warp/wormhole transitions while the fleet's `FreeMovementSpeed <= epsilon`, so the Ctrl route
  equals the plain route until the tech exists (measured: Primus→Rigel, 3 steps either way).
  The mod's Ctrl+Backslash reaches the same decision through `FleetOrders.RequestedFlags`.
- **A refused move gesture is NOT silent in the game — the reasons go to the failure banner.**
  The cursor collects `FailureInfo`s through the three-argument `CanBeExecuted` (:245) and a ladder
  of relaxed `FindPath` re-runs (:456-506), shown via `IGuiService.SetFailureInfos`. Mod policy
  (2026-08-14): the send key pressed on a NAMED destination speaks each distinct reason once
  (`GalaxyHudScreen.SayRefusals`); the fleet's own orbit stays silent, because nothing was refused
  there.
- **`GalaxyView.CanFleetMovementBeOrdered` is gesture disambiguation, not movement law.** It is
  false exactly while the overview's zoom is forced, and its only readers in the whole game are
  `GalaxyGarrisonCursor.OnCursorDown/OnCursorUp` — because a right click is then reassigned to
  restore-zoom, so it cannot also mean "move". Every real legality rule (borders, citadels, vision,
  points, frozen systems) lives in the pathfinder and `CanBeExecuted`, which the mod's orders run.
  Mod policy (2026-08-14): backslash keeps sending with the zoom forced — expanding a tree system
  forces it — deliberately more capable than the mouse, which has a conflict to resolve and the
  keyboard does not.
- **The map's own turn markers read one LOW for a fleet stopped mid-lane with nothing left.**
  `PathRenderer.DisplayMovement` (:765) marks a turn boundary only when the leg is not already
  under way, so the turn a stranded fleet cannot move in gets no marker and the destination is
  labelled a turn early — measured: the marker said 2, the fleet arrived after 3, and the end-turn
  countdown `3 → 2 → 1` confirmed the corrected walk. `RouteTurns.Walk` emits that turn; the mod's
  numbers deliberately diverge from the drawn labels because the drawn labels are wrong about the
  world.
- **A fleet flies THROUGH systems mid-turn and only ends a turn AT a node when the budget dies
  exactly there.** A route can pass through a system mid-turn and end that turn mid-lane.
  "Where does each turn end" and "which nodes does the fleet reach" are different
  questions with different answers.
- **Neither interception nor route cancellation raises an event, and neither is worded anywhere.**
  `EventFleetGotInterceptedByAnEnemy` is raised only by `GuardEmpireLocalAction` (:605); the common
  citadel interception (`Citadel.cs` :195-222) raises nothing, and no cancelled-route event exists.
  Both endings share `Fleet.OnGoToEnd` → `SetPath(null)`, and the game's only signal is a
  `GuiFleetStatus` icon on the lozenge — so polling `Fleet.Path` is the faithful watch
  (`FleetRouteWatch`), and arrival is told from loss by whether the fleet stands at the remembered
  destination (the same test `GoToFleetAction` :307 makes).
- **Every `GoToFleetAction` ending funnels through `ClientFinalize` → `Fleet.OnGoToEnd`** —
  arrival, cancellation, replacement and interception alike (`EntityAction.ClientExecute`
  :467-492 runs `ClientCancel` → `WaitingForFinalization` → `ClientFinalize` :695-702;
  `GoToFleetAction.ClientFinalize` :258-268 calls `OnGoToEnd` unconditionally). A hook there
  tells arrival by the game's own success test (standing on `Path.Destination`, :307-311),
  never by the absence of an abort. `Fleet.HasBeenIntercepted` is cleared at
  `ClientInitialize` (:271-279) — the start of the NEXT journey — so at a journey's end it
  still reports that journey; and `OnGoToEnd`'s first act is `SetPath(null)`, so a PREFIX is
  the only place the destination is still readable (`FleetArrivals`).
- **Client visibility writes all pass `EntityVisibility.SetLayer`, and nothing calls it
  silent**: no call site in either assembly passes `silent: true` (the only explicit argument
  anywhere is `silent: false`, `GameClient.cs` :5022), and the server's refresh transients
  (`ServerPreRefreshVisibility` :237-247) touch `serverLayers` only — the post-refresh diff
  ships net changes, so client `SetLayer` never sees a spurious downgrade (measured: 384 idle
  frames, 7 visible foreign fleets, zero events).
- **`Fleet.GeneratePathfindingData()` and `GalaxyPath.Data` hand back the fleet's own SHARED
  `PathfindingData` instance.** Mutating it corrupts the fleet's real pathfinding — always copy
  before simulating (`FleetRoute` does).
- **Sending a fleet clears the map's selection**, so re-ordering a moving fleet means re-selecting
  it first — and it is why a route REPLACEMENT never looks like a cancellation to a watcher that
  checks the fleet still exists and where it stands.
- **An order is only POSTED by `PostOrder`; the session executes it later** — a stock read in
  the same call still sees the pre-order value, which is why the game's own probe click tests
  `<= 1` rather than `== 0` to decide the mode is over.
- **`Garrison.ShipsIncludingHero` is an `IEnumerable<Ship>` yield iterator, never a list** — a
  cast to `IList` answers null silently and turned every fleet's probe count into zero for two
  releases. Walk game collections through the interface they declare.
- **`FleetActionButton*.OnClick` indexes `GameNodes[NodePosition.NodeIndex]`, which is -1 for a
  fleet in transit** — the game's orbit gate is all that stops it throwing; never force-enable
  these buttons off-orbit.
- **`FleetActionToggleReclaimMothership.OnToggle` has two branches** — no running action →
  `ZoomInOnNode`; already running → the game's `%ConfirmCancelReclaimDescription` message box,
  no zoom.
- **A data-driven button roster's closed set lives in `Public/Gui/**/GuiElements[*].xml`, not the
  assemblies.** When a `WindowGuiElement`-derived class holds `(Name, TypeName)` pairs
  (`FleetsScreenGuiElement.FleetActionButtons` — all 32 rows, one of them a STAR-SYSTEM
  action driven from the fleet panel: `GroundBattleStarSystemActionDefinition`), the XML on disk is the
  authoritative enumeration; a decompiled-file glob under-counts. Static game data, readable
  without the game running.
- **Names for a fleet panel's wordless buttons and columns are in the game's data, twice over.**
  The action buttons resolve through `Gui.GetTitle(definitionName)` — which hands back the KEY, so
  `AgeText.Clean` localizes it — and the toolbar/management buttons have their own
  `%Fleet{SelectAll,SelectAllShips,Merge,Disband,CreateFromHangar,CreateFromShips,Repair,Retrofit,
  Scrap,Sell,SpecializeJuggernaut}Title`. The fleet-line COLUMNS are named by
  `%FleetListTable{CommandPoints,MovementPoints,Health}Title`, whose values are icon tokens
  (`[commandPoint]`), so `AgeText.Clean` turns them into "Command Point", "Movement", "Health".
- **"Apply movements" is one ORDER and no camera work.** `EndTurnWindow.ApplyMovementsButton`'s click
  (`OnApplyMovementsCb` :1356-1361) posts a single `OrderMoveIdleFleets(playerEmpire.Index)` and
  touches no cursor, no selection and no view level — measured: the mod's landing pipeline never
  fires for it, and nothing is spoken by the press itself. What the player then hears is the
  notification watchers reporting the consequences ("1st Patriots Navy, Patrol arrived at Osulo",
  "Niris colony sighted at Osulo"). The button is switched on by `UpdateApplyMovementsButton`
  (:1006-1016) on TWO conditions — `CanEndTurn()` **and**
  `DepartmentOfTransportation.GetNumberOfMovableFleets() > 0` — which is the state probe for both
  "is there anything to apply" and the refusal case (applying takes the movable count to 0).
- **`EndTurnWindow.SelectIdleFleet` only works for a fleet with a docking slot.** It looks the slot
  up in `IVisibleDockingSlotRepositoryService` to aim the camera (:1387-1409); a fleet under way has
  none, so it falls through to `fleetsScreen.SelectIdleFleet`, which — with the window not shown,
  which is exactly the case, since showing it is what selecting a fleet does — merely stashes the
  fleet in `garrisonToSelectAtNextShowing` (`FleetsScreen.cs` :672-682) and returns. Nothing
  happens, silently. The route that works wherever the fleet is standing is the one `MilitaryScreen`
  (:552-559) and `NamedShipInfoPanel` (:218-224) take: find the `GalaxyFleet` in
  `IVisibleGalaxyFleetRepositoryService`, `ICursorService.Select(galaxyFleet.CursorTarget)`,
  `ChangeCursor(typeof(GalaxyGarrisonCursor), galaxyFleet)`, then
  `Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(fleet)` — in that order, because the
  panel's own visibility is gated on the cursor: the cursor goes `GalaxyCursor` →
  `GalaxyGarrisonCursor`, the panel's stops appear, and the camera focus moves onto the fleet.
  **The last call also CLOSES whatever full screen the player is on** — the request alone puts the
  galaxy page back and no window has to be hidden by hand. So a locate is
  a screen change the mod hears as an ordinary re-entry of the galaxy, and the cursor it re-seats on
  arrival is the only thing that tells the player where the game has taken them.

- **A DOCKED fleet stands at TWO points, and the game's own "show me this fleet" sites disagree about
  which.** The BERTH is `IVisibleDockingSlotRepositoryService.GetDockingSlotWithFleet(fleet)
  .transform.position` — where the ship model is drawn, beside the star; the STAR is
  `fleet.GalaxyPosition`, and `fleet.Position.GetOrbit()` hands the `StarSystemNode` back directly
  (measured a couple of galaxy units apart). The sites split: `MilitaryScreen.OnLineDoubleClick`
  (:511-560) selects the BERTH and then frames the STAR (`RequestGalaxyOverviewViewLevel
  (SelectedFleet)`), `NamedShipInfoPanel` (:184-236) does the same with the garrison's game node, while
  `EndTurnWindow.SelectIdleFleet` (:1387-1409) frames the BERTH and selects it. **Selecting a berth
  moves no camera at all** — measured by suppressing the mod's own landing over the military page's
  second click: the only camera move left was the single damped `CenterOnPoint` on the star.
  So the game itself makes ONE move per site; a second move is the MOD's landing, and the mod
  reconciles the two points its own way.

## The fleet panel, selection and ship transfer

- **Full deselect needs BOTH** `ChangeCursor(typeof(GalaxyCursor), cursor)` AND `Select(null)` on
  `ICursorService` (`CursorManager.cs:233-263`): `ChangeCursor` alone leaves `CursorTarget.Selected`
  stuck. Two selection stores exist (`FleetsScreen.SelectedGarrisons` and
  `IGuiSelectedGarrisonsRepositoryService` — the movement cursor reads the repository), so never
  wire a selection through only one. An order that names its initiator explicitly bypasses both,
  which is why the mod's explicit-initiator send (backslash) posts moves without needing the
  selection stores wired.
- **Selecting a DOCKED fleet selects the docking SLOT, and the selection follows the slot's contents.**
  The idle-fleet route leaves `DockingGalaxyGarrisonCursor` up (a `GalaxyGarrisonCursor` subclass);
  `IGuiSelectedGarrisonsRepositoryService` then reports whatever is parked in that slot, so ordering
  the selected fleet away leaves the OTHER fleet at that system selected — measured: a send emptied
  the selection onto the second fleet, and the next send moved that one. A fleet under way selects to a
  plain `GalaxyGarrisonCursor` and is the only thing selected. Anything that posts one order per
  selected fleet has to re-read the repository at the moment of the press, never remember it.
- **Every targeting-mode cancel at a multi-fleet system hands the panel to the slot's FIRST
  fleet** (measured 2026-08-20, keyboard Escape and mouse right-click byte-identical):
  `ProbeLaunchingCursor.SwitchToGalaxyCursor` (:55-70) selects the DOCKING SLOT, not the origin
  fleet; the arming hid the panel and `FleetsScreen.OnBeginHide` (:925-943) ran
  `UnselectAllGarrisons`, so `FleetsScreen.RefreshGarrisonSelection` (:1116-1129) defaults
  positionally — `Garrisons[0]`, or `[1]` past a Hangar. The actor's spent state is irrelevant
  (control run: cancel with nothing launched swaps identically). This positional default owns
  every "panel opened for the wrong fleet" symptom; Enter on a fleet's own row is correct in
  every measured state.
- **`FleetsScreen.OnBeginHide` removes garrisons with a forward loop and leaves a residue list**
  (measured: `Garrisons=[1296]` still standing after the panel closed).
- **`DockLabel.OnClick` accumulates duplicate subscribers** — `DockLabelsWindow.OnDockLabelClicked`
  re-subscribes on every pooled `ShowLabel` (4 measured), so one dock-label click advances the
  garrison cycle N times; at even cycle parity the mouse's click-cycling never changes the
  selection at all.
- **A list the game selects with modifier-clicks asks the REAL keyboard, which is what makes the mod's
  selection chords free.** `FleetsManagementPanel.OnToggleFleetLine` (:277-299) and
  `ShipsManagementPanel.OnToggleShipItem` (:707-750) both branch on
  `Input.IsControlKeyDown()/IsShiftKeyDown()`, so a replayed click made from a Ctrl+Enter or
  Shift+Enter binding runs the game's MODIFIED branch without the mod supplying anything: one click
  path, three gestures, the mouse and the keyboard sharing one anchor. (Supersedes an earlier reading
  that these are radios with no multiple selection — that was true only while the keyboard had no
  chord to press.) The three branches are not symmetric: a fleet line treats Control and Shift
  IDENTICALLY (one line in or out), so the fleet list has no range gesture at all, while a ship tile's
  Shift is a real run from the panel's private `lastClickedShipGUID` anchor with everything outside the
  run deselected. Plain, on both, is `Select*RadioMode` — the whole selection replaced — which is why
  Enter on an already-selected row leaves it selected.
- **A ship tile's drawn tick is a frame BEHIND the panel's model.** `SelectShip` and
  `SelectShipRadioMode` (`ShipsManagementPanel.cs` :426-485) write only `selectedShipsPerGarrison` and
  set `Dirty`; the toggles are rewritten by `BindGarrisonPanel` (:355-372) on the next refresh. So the
  tick answers "is this ship selected" correctly in every settled frame and WRONGLY in the frame after
  a selection key — anything spoken immediately after an activation has to read the model
  (`GetSelectedShips`, the one public window onto that dictionary), and only the per-frame watch can
  afford the tick. `DeselectShips()` does not even set `Dirty`, so it desynchronises the two until
  something else refreshes the panel. A FLEET line has the same lag and the same answer, and there the
  model is `FleetsScreen.SelectedGarrisons`.
- **Emptying the fleet panel's selection does not stick.** Measured: `UnselectGarrison` on the only
  garrison left the selection at 0 and the game put it straight back (the mod's live membership part
  spoke "not selected" then "selected" in consecutive frames). The panel is drawn for a cursor that
  names a garrison, so "nothing selected" is not a state it will sit in.
- **The fleet panel lists only the garrisons of the CURSOR's own container.** Two fleets flying the
  same lane are two `GalaxyFleet`s, so selecting one draws a one-line fleet list — measured with both
  player fleets mid-lane between Dusay and Rigel: `FleetsScreen.Garrisons.Count == 1`. Every
  cross-fleet gesture (merge, a ship dragged or carried from one fleet to another) therefore needs the
  two garrisons to be in one container: docked in the same slot, or a hangar and the fleet parked at
  it.
- **`DepartmentOfDefense.CanTransferShips` has no co-location test and no is-it-moving test.** Measured
  on two fleets stranded mid-lane: transferring a ship from either into the other answers TRUE, as does
  transferring a ship into the fleet it is already in. What it does check is the destination
  (read-only, disband-only, locked), each ship (reassignable, not in an encounter, not a docking or
  unmodifiable fleet, not mothership-attached, hangar and always-in-a-fleet rules, one-of-role) and,
  for a Fleet destination, command points, privateering, attached motherships. The words for a refusal
  are `Gui.FormatFailureInfo(string.Empty, failureInfo)` — with an empty base description that is
  exactly the `%Failure<Flag>Description` sentence and a colour directive, so `AgeText.Clean` finishes
  it (measured: "No ships are selected").
- **The transfer itself is private and the drag is its only caller.** `FleetsScreen.TransferShips(List
  <Ship>, Hero, GameEntityGUID, NodePosition)` (:1038) builds `OrderTransferShips` and posts it — and,
  when `CheckIfRemovingShipsCausesInvisibilityCancelation` says so, raises a confirmation box first.
  Reaching it by reflection is what keeps that confirmation; rebuilding the order by hand loses it.
  A zero-GUID destination with a valid `NodePosition` is the drag's OTHER ending, dropping onto empty
  space to make a new fleet — the same thing the panel's own Create button does.
- **`FleetsScreen` is the front-most window Escape reaches, and it already deselects.**
  `GuiManager.HandleInput` (:2058-2064) walks the shown windows front to back offering each
  `IInputHandler` the action; `FleetsScreen.HandleInput` (:376-382) answers Exit with
  `ChangeCursor(typeof(GalaxyCursor), Gui.GetCursor())`, which drops the garrison cursor the
  window's own visibility is gated on (`UpdateGameWindowsVisibility` :1543,
  `isInNormalView && !IsAnyScreenVisible && CurrentCursor is GalaxyGarrisonCursor`). So the panel
  needs no `ConsumesBack` — measured `DevProbe.Claims("Escape")` = `claims:false` with a fleet
  selected and the galaxy page focused. Note it does NOT null `CursorTarget.Selected`; that is what
  a mouse user gets too. The same gate is why F4/F7 hide the panel outright and clear its selection.
- **The selected-fleet panel is a GALAXY-OVERVIEW overlay, despite what its gate reads like.**
  `isInNormalView` is true on the star-system and planet view levels too, so the window's own
  condition looks like it spans them — but entering a system swaps the cursor (measured:
  `CurrentCursor` = `StarSystemCursor`, `CurrentGalaxyViewLevel` =
  `GalaxyViewLevel_SystemManagement`, `FleetsScreen.Shown` = false), and the cursor is the other
  half of the gate. So the panel belongs to the galaxy page alone, and the selection is dropped by
  the visit rather than kept under the system page.
- **A full screen drawn over the map takes the panel with it, silently.** Opening any of them
  force-swaps to the plain `GalaxyCursor` and clears the fleet selection
  (`GuiManager.cs:1783-1795`), so the panel closes while the mod's galaxy page is off the stack —
  the close frame its watch would have answered on never happens under that page. Measured
  2026-08-26: cursor on the panel's fleet line, show then hide
  `MilitaryScreen`, and the cursor came back on an unrelated mid-lane fleet's top-level row three
  stops away. The mod answers it by catching the release at the pop and seating on the way back
  (`GalaxyHudScreen._releasedAcross`), which is the same landing letting the fleet go gives.

## The targeting-cancel fleet swap

The game's own mechanism behind "the panel reopened for the wrong fleet", hop by hop:

1. Escape → `GuiManager.HandleInput` Exit branch →
   `((ProbeLaunchingCursor)CurrentCursor).SwitchToGalaxyCursor()` (`GuiManager.cs:2103-2109`).
   Mouse right-click reaches the SAME method (`ProbeLaunchingCursor.cs:129,177-183`) — keyboard and
   mouse are byte-identical here.
2. `SwitchToGalaxyCursor` selects the **docking slot**, not the origin fleet
   (`ProbeLaunchingCursor.cs:55-70`).
3. Arming had hidden the fleet panel, and `FleetsScreen.OnBeginHide` runs `UnselectAllGarrisons()`
   (`FleetsScreen.cs:925-943`), so the slot's selection is empty when the cancel re-selects it.
4. `FleetsScreen.Cursor_SelectionChanged` → `RefreshGarrisonSelection` (`:1364-1382` → `:1116-1129`):
   with nothing selected it defaults **positionally** — `Garrisons[0]`, or `Garrisons[1]` if `[0]`
   is a Hangar.

The swap has nothing to do with idle-fleet preference (`EndTurnWindow.SelectIdleFleet` takes an
explicit fleet and contains no preference; `GetNextIdleFleet` is called only by the game's own
next-idle-fleet button) and nothing to do with the fleet having acted.

`SwitchToGalaxyCursor` exists on `ProbeLaunchingCursor` and on `CoordinationRequestCursor` (an
unrelated implementation) — there is no shared base method to patch, and no other targeting cursor
re-selects anything on cancel (`ObliteratorFireCursor` :69-90 and the shared Exit branch
`GuiManager.cs:2115-2120` just `ChangeCursor(typeof(GalaxyCursor))`), so there is nothing to
generalize.

The prefix must catch `ProbeOriginFleet` BEFORE the switch: selecting the slot swaps the cursor and
the swap's deactivate nulls that property synchronously, so a postfix alone reads null.

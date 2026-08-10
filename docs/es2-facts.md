# ES2 facts — reverse-engineered mechanisms

Game-mechanism findings with no home in the code: how ES2/Amplitude actually behaves, each
measured or cited. The dev loop itself (helpers, layers, keys, server, recipes) is
`dev-loop.md`; game-agnostic doctrine is `docs/generic/`. A new fact lands here — never in
the dev loop — and anything that turns out generic graduates to the generic docs.


- **A collapsed tutorial is a HUD stop, not a tutorial screen.** The game crops the popup
  to its title bar and hides nothing, so `MinimizeToggle.State` is the only signal;
  `TutorialScreen` stands down while it is set and `BuildCollapsedBar` declares the leftover
  bar in `GlobalHud`'s `hud:tutorial` stop, on whichever view level is underneath. The game
  does NOT draw the bar on the planet overview, so that page has no such stop — measured, not
  assumed.
- **The pinned quest panel needs TWO existence answers, and neither is the other.**
  `GuiManager.UpdateGameWindowsVisibility` (:1542) hides `PinnedQuestWindow` outright behind any
  GuiScreen (`isInNormalView && !IsAnyScreenVisible`), while `PinnedQuestWindow.UpdateDisplayedQuest`
  (:60-80) additionally needs an `ActiveQuest`; and `PinnedQuestPanel.OnBeginHide` nulls
  `PinnedQuest` at the START of its fade, which is what makes `window.Shown && panel.PinnedQuest !=
  null` the gate rather than a visibility flag mid-animation. The panel's three click targets are the
  panel itself (`OnClickQuestCb` → journal), `ShowLocationButton` (`OnShowLocationCb`) and
  `UnpinButton` (whose handler lives on the WINDOW, `PinnedQuestWindow.OnUnpinQuestCb`).
- **`AgeWidgets.Operable` answers enablement only — pair it with `Visible` before OFFERING an
  action.** `GuiQuest.SetupMarker` (:419-428) hides the quest's marker button by writing
  `AgeTransform.Visible` and never touches `Enable`, so an `Operable`-only gate put a "Show location"
  entry into the pinned quest's menu for a quest the game draws no marker for (measured: menu read
  "1 of 3" with `hasMarkers=False`). Every menu builder in this repo asks both — `AddOrbitalAction`
  already did.
- **A window's own `HandleInput` override can turn its Cancel button into a Confirm.**
  `GuiModalWindow.OnCancelCb` is nothing but `HandleInput(InputAction.Exit)`, so any window that
  overrides Exit to mean something other than "dismiss" silently changes what its Cancel button
  does — and the game's tooltip on that button goes on saying the old thing. Read the override
  before trusting either the key or the button.
- **`Visible` is not "drawn".** `AgeTransform.RefreshChildrenIList/Array` leaves the surplus
  children of a pooled table (a competitor slot an empire count no longer needs) flagged
  `Visible` with `Alpha == 0`. Ask about alpha too — but only `== 0`, since a read-only setting
  is faded to 0.5 and is still drawn.
- **The click sound is an `AgeAudio` component on the widget's own transform** (posts
  `MouseUpEventID` via the gui audio proxy, `AgeAudio.MouseUp` :191-197, on the engine's
  mouse dispatch — never from the handler). `AgeWidgets.Click` posts the component's down/up
  before dispatching; the generic rule is widgets.md's "a click is more than its handler".
- **A window's `AgeTransform.Enable` lags its `Shown`/`IsReady` by a frame or two** (measured:
  `shown=True ready=False windowEnable=False`, both true two frames later), and the same lag
  exists for a PANEL swapped inside a window that never closes (the faction chooser under the
  custom faction editor). A screen whose arrival needs enablement gates on `Operable` — window
  AND swapped panel — where it has shown the symptom (every control reading "unavailable" once);
  the modal family's plain `Shown && IsReady` gate is otherwise correct as-is. Rationale:
  ui-navigation.md's arrival-gates-on-enablement.
- **A `GuiTable` line is a POOL SLOT, not a row.** `LineNNN` names (and positions) are reassigned
  whenever the table refreshes or re-sorts, so a cursor keyed on either sits on a different thing
  a frame later — measured: picking a trait in the custom-faction editor left the next Enter
  picking whatever the re-sort moved under the cursor. Key a line on `GuiTableLine.Data`; with it
  as `ControlId.Referenced` the cursor even follows an entry from one table into the other.
- **`SendMessage(name, sender)` does not reach a zero-argument handler** — most game callbacks
  take `(GameObject obj = null)`, but not all (`OnPreviousHullCb()`/`OnNextHullCb()`), and
  `DontRequireReceiver` swallows the mismatch. `AgeWidgets.Press`/`Toggle`/`Choose` resolve the
  arity (cached) and pick the overload; the generic rule is widgets.md's arity contract.
- **Every tooltip is an ordered list of panel features.** `GuiTooltipWindow.DoBind` resolves
  the tooltip's `Class` through the description database and instantiates one prefab per
  feature under `PanelFeaturesTable`; a feature's SUB-features are added as further siblings in
  the same table, not nested, so the drawn tooltip is always one flat ordered list. `IsSeparator`
  and `IsSpacing` are on Assembly-CSharp's global `GuiPanelFeature`, not on the firstpass
  `Amplitude.Unity.Gui` base — a REPL probe typed to the latter cannot see them.
- **A tooltip names its bare numbers out of TWO registries, and only one of them is the icon
  table.** A figure the panel draws beside a picture is captioned by that picture, but the
  pictures come in two kinds: an inline `[token]` inside a label's own text, which
  `IconNames`/`IconTable` resolves, and a standalone `AgePrimitiveImage` the feature binds from a
  field of its own (`HealthIcon`, `MovementPointsIcon`, `ActionPointIcon`, `CommandPointsIcon`,
  the `ValueDuplet.Symbol` of each ship-size count). The second kind is not markup and never
  reaches the token table, so its word is not there to be found: it is the STAT's own title in the
  game's element database (`Gui.GetTitle` + the `"%"+name+"Title"` fallback) or a `%…Title` key
  the game already uses for that column — `%ShipStat*Title`, `%ActionPointTitle`,
  `%ShipSize*Title`, `%FleetListTableCommandPointsTitle`. `DevProbe.UnknownIcons()` is silent
  about these by design; the symptom is a spoken line that is only figures, and the fix is a typed
  reader, never a new icon row.
- **`Gui.GetTitle` can hand back a key that has no translation.** `ShipStatCommandPoints`
  declares `%ShipStatCommandsTitle`, which the corpus no longer has; the engine's own naming
  convention (`"%" + name + "Title"`) resolves it. Anything reading a title through the element
  database needs that fallback, and silence rather than a `%key` as the last resort.
- **A typewriter label already holds all its words.** `AgeModifierTypewriter` does not write
  text a character at a time: it sets the whole string once and advances the label's
  `CurrentLine`/`CurrentCharInLine`, which only the RENDERER honours. So `AgeText.Label` on a
  mid-animation label is complete, and an announcer never has to rebuild the panel's phrasing
  from the model to beat the animation.
- **On the technology wheel, `Visible` is a CAMERA answer.** `TechnologyItem2.UpdateVisibility`
  clears it for anything off screen, so enumerate by `VisibleByDefinition` (107 of 385 in the
  fixture) and move the camera before expecting a tooltip; the drawn LINK arcs are the opposite —
  `TechnologyScreen.Refresh` sets `Visible` on exactly the arcs that apply to this empire, so that
  flag IS the game's own link filter (22 of 162 at turn 2).
- **The markers on the wheel's rings are DEEDS, and their state is a colour.**
  `TechnologyStageItem.DeedItem` is drawn only while `guiTechnologyStage.GetDeed(empire)` found a
  started quest (`DeedItem2.Refresh` :131-199 sets `Visible = deed != null`), and it paints itself in
  one of the four technology-state colours, each of which the key panel names —
  `%DeedState{Available,Researched,Disabled,NotAvailable}Title` = Available / Completed / Failed /
  Locked, and `%CategoryDeedTitle` = "Deed". The wrapper the marker built is the private field
  `guiDeed`; its own public predicates (`IsDeedAvailable`, `IsDeedVisible`) are the same tests the
  marker makes. The empire that won a failed deed is found through
  `IQuestManagementService.GetQuestsByInstanceId`, not on the deed itself. **The turn-2 fixture draws
  12 of them** (all `InProgress`): measured `GetDeed(Gui.PlayerEmpire) != null` on 12 of 20 bound
  stages, and Empire Development II's stage is already `Researched`, so that deed is *available* and
  carries its full `DeedDescription` tooltip — the cheapest cross-check that a deed's state word is
  right is that the game switches the tooltip's CLASS on the same predicate.
- **What the game recommends researching is a list, not just a badge.**
  `TechnologyScreen.SuggestedGuiTechnologies` (refilled in `Refresh` :393-398 from
  `DepartmentOfScience.SuggestedTechnologies`) is what `UpdateSuggestionTop` badges the dots from
  per frame; the game's word for one is `%SuggestedItemTitle` ("Suggested").
  `TechnologyItem2.UpdateSuggestionBottom` belongs to the notification windows'
  `SuggestedTechnologiesPanel` alone — nothing on the wheel calls it.
- **Aiming the wheel's viewport** takes a point measured from the middle of the wheel in the
  normalized (782-wide) space the stages place their dots in: `DoZoomIn(aim, 0.3f)` from the
  overview, `DoTranslate(aim * 4, 0.3f)` once `Viewport.GetComponent<GuiValueController>()
  .CurrentValue == 4` (both private; the controller is reachable off `Viewport`). A quadrant's own
  aim already exists as the game's `OnSectorClick` — call it through `ITechnologyQuadrantClient`
  rather than recomputing radius 195.5 at the sector's mid-angle.
- **On the quest strip, `Alpha` and `Visible` answer nothing — the BINDING does.**
  `NarrativeScreen.ShowFilteredQuests` (:248-251) unbinds every `QuestCard` before refilling the
  table, so `card.GuiQuest == null` is the game's own "this slot is a leftover". The pooled-slot
  Alpha==0 rule is actively WRONG here: `UpdateCardsAlpha` (:190-220) rewrites the alpha of every
  child each frame to between 0.25 and 1 — the fade over the cards near either edge of the viewport —
  so a faded card is a real card and no card ever reads 0.
- **`GraphNavigator.Alternate` speaks the node's `StateText`.** A node built by a factory that
  supplies one (radio, checkbox, slider) answers Alt+Enter with the word for the PRIMARY action:
  the quest card's Alt+Enter pinned a quest and said "selected". A node with two activations has to
  own its `StateText` and answer for whichever one just ran — which is a wrapper the journal's card
  carried until the pin became a node of its own. **The real lesson is the one above it**: the card had
  no modified click in the game at all, so the second activation was invented, and the honest fix was
  to declare the control the card DRAWS (`QuestCard.PinToggle`) as a child node and leave Alt+Enter
  unwired. The game names that action, though never on the toggle: `%NotificationQuestBegunPinTitle`
  ("Pin Quest"), the button the quest-begun notification offers for the same thing, with
  `%NarrativeScreenActiveQuestPinDescription` as the toggle's own tooltip.
- **Two controls that name the same backing object are one control to the cursor.**
  `ControlId.Reference` is followed before the structural key, so the research screen's queue row
  and its wheel dot both keyed on `GuiTechnology2` teleported the player into the queue panel the
  moment they queued something; the dot keys on `TechnologyDefinition` instead.
- **Which label the map draws a fleet with is the map's answer, not a flag of ours.** A fleet in
  orbit is drawn by the DOCK label of its slot (`DockLabelsWindow`→`DockLabel`, matched through
  `DockingSlot.GalaxyFleets`), one under way by its own (`FleetLabelsWindow`→`FleetLabel`, matched
  through `GalaxyFleet.Fleet`); exactly one is bound at a time, so searching both windows is the
  whole test. **The tooltip the engine DRAWS is the lozenge's** (`GarrisonsLabelButton.Tooltip`,
  i.e. `lozenge.AgeTransform.AgeTooltip`), not the label's `CenterTooltip` — both are filled from
  the same fleet data and both read `Class=FleetGroup`, so declaring the label's leaves the readout
  saying "has tooltip" over a review buffer that never fills (`DrawnTooltip` checks bind identity).
  Point at, and declare, the widget the pointer lands on. A dock label covers the whole SLOT, so
  two fleets parked together share one "2 Fleets" tooltip - which is what the game shows a hoverer.
- **The two fleet repositories PARTITION the map's fleets, and each label window filters further.**
  A docked fleet is not in `IVisibleGalaxyFleetRepositoryService.GalaxyFleets` at all (measured: two
  fleets parked at Dusay, `GalaxyFleets.Count == 0`), so "on a lane" needs no is-it-docked test - the
  repository the lane reads has already answered it. `FleetLabelsWindow.ShowAllLabels` (:79-81) then
  adds `(int)Fleet.Visibility[Gui.PlayerEmpire as MajorEmpire] >= 2` before drawing, which the
  repository does NOT apply, so anything claiming visual parity has to apply it too.
- **"N Fleets" is `GuiFleetGroup.Title`, and it is RELATION-aware.** The heading on every lozenge
  tooltip picks between `%PanelFeatureFleetCount{Player,Enemy,Neutral,Allied}{Single,Plural}` off the
  diplomatic relation to the group's owner (`GuiFleetGroup.cs:40-76`), so the phrase for two fleets is
  "2 Fleets", "2 Enemy Fleets" or "2 Allied Fleets" depending on whose they are. It is constructed
  from a `List<Garrison>` and reads only `Gui`, so the mod can hand it a group and speak what comes
  back rather than re-deriving relation words. **And the group a DOCK label counts is not the docking
  slot's fleet list**: `DockLabel.FillDockedGarrisons` (:186-212) prepends the system's own hangar
  garrison while it holds ships of the player's and no mothership is attached, so the drawn number can
  exceed `DockingSlot.GalaxyFleets.Count` (measured at Dusay: hangar present, `ShipsCount == 0`, drawn
  title "2 Fleets" over exactly the two fleets).
- **A fleet's CURRENT LEG is `FleetPosition.Movement.Start`/`.Goal`, and cancelling a move does not
  clear it.** The pair names the two nodes of the lane the fleet is drawn on right now (not its
  destination - `Path.Destination` is that), so matching them against a `Link`'s two extremity
  `NodePosition`s, either way round, is the whole "which fleets are on this lane" test. Measured after
  `OrderCancelEntityAction`: `Path == null` while `IsInMovement` stays true with start and goal
  intact - a cancel does not call `UnsetMovement` - so a stranded fleet goes on belonging to its lane.
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
- **A data-driven button roster's closed set lives in `Public/Gui/**/GuiElements[*].xml`, not the
  assemblies.** When a `WindowGuiElement`-derived class holds `(Name, TypeName)` pairs
  (`FleetsScreenGuiElement.FleetActionButtons` — all 30 fleet actions), the XML on disk is the
  authoritative enumeration; a decompiled-file glob under-counts. Static game data, readable
  without the game running.
- **Full deselect needs BOTH** `ChangeCursor(typeof(GalaxyCursor), cursor)` AND `Select(null)` on
  `ICursorService` (`CursorManager.cs:233-263`): `ChangeCursor` alone leaves `CursorTarget.Selected`
  stuck. Two selection stores exist (`FleetsScreen.SelectedGarrisons` and
  `IGuiSelectedGarrisonsRepositoryService` — the movement cursor reads the repository), so never
  wire a selection through only one. An order that names its initiator explicitly bypasses both,
  which is why the fleet menu posts moves without selecting anything.
- **A queue line's own click is the CANCEL, and the game asks its own question when it needs to.**
  `ConstructionLine.MainButton` → `OnCancelCb` (:378-393) sends `OnCancelConstruction` to the panel,
  and `StarSystemQueuePanel.OnCancelConstruction` (:425-442) branches on
  `Construction.IsAlreadyInvested`: uninvested, it posts `OrderCancelConstruction` at once; invested,
  it raises the game's own `MessageBoxWindow` with `%StarSystemCancelConstructionConfirmation` and
  posts only on `MessageBoxResult.Ok`. The box has BOTH buttons — `GuiManager.ShowMessage`
  (:2303-2315) defaults `cancelTitle` to `%MessageBoxCancelTitle` and `MessageBoxWindow` shows the
  Cancel button whenever that is non-empty — so Enter on a queue line is never an unaskable loss.
  Pressing MainButton rather than reaching for the panel's handler also keeps the god-mode branch and
  the mid-drag guard the game puts in front of it. (Live-verified on a 46%-built improvement: the box
  came up, Cancel left the queue untouched.)
- **A dropped queue line lands AT the target's index, and both queues post an absolute index.**
  `StarSystemQueuePanel.OnDragCompleted` (:302-320) posts `OrderMoveConstruction` with the dragged
  line's new `GetSiblingIndex()`, which `OnDragMoved` (:273-300) produced by removing the line from
  its visible-order list and re-inserting it at the index of the row the cursor is over; the research
  wheel's `ResearchStatusSidePanel` (:180-243) computes an insertion SLOT from the cursor's x and
  posts `OrderMoveResearch` with it. Despite the name, `OrderMoveResearch.IndexOffset` is absolute —
  `DepartmentOfScience.MoveResearchProcessor` passes it straight to `ResearchQueue.Move`, and
  `DepartmentOfIndustry.MoveConstructionProcessor` (:474-511) only adds an offset for the
  `Base.Current`/`Base.End` cases the GUI never uses. `ConstructionQueue.Move` (:156-176) is
  `RemoveAt(from); Insert(destination)`, so passing the target row's CURRENT index puts the carried
  item exactly where the target was, in both directions. That is the rule the keyboard carry copies.
- **A buy-out button is hidden, not disabled, when the empire cannot buy out at all.**
  `ConstructionLine.RefreshBuyout` (:272-343) sets `Visible = false` for another empire's system and
  for the `BuyoutTechnologyNotUnlocked` / `BuyoutIncompatibleAffinity` failures, and otherwise leaves
  the button `Visible` with `Enable = false` and the reason written into its own tooltip
  (`Gui.FormatFailureInfos("%ConstructionBuyoutDescription", …)`). So the gate for declaring one is
  VISIBLE and the gate for offering it is `Enable` — not the hint test the planet cards need. At
  turn 3 both currencies read `BuyoutTechnologyNotUnlocked`, so no buy-out is drawn at all.
- **A list the game selects with modifier-clicks asks the REAL keyboard, which is what makes the mod's
  selection chords free.** `FleetsManagementPanel.OnToggleFleetLine` (:277-299) and
  `ShipsManagementPanel.OnToggleShipItem` (:707-750) both branch on
  `Input.IsControlKeyDown()/IsShiftKeyDown()`, so a replayed click made from a Ctrl+Enter or
  Shift+Enter binding runs the game's MODIFIED branch without the mod supplying anything: one click
  path, three gestures, the mouse and the keyboard sharing one anchor. (Superseding the earlier
  reading that these are radios with no multiple selection — that was true only while the keyboard had
  no chord to press.) The three branches are not symmetric: a fleet line treats Control and Shift
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
  panel's own visibility is gated on the cursor. Measured: cursor `GalaxyCursor` →
  `GalaxyGarrisonCursor`, the panel's stops appear, and the camera focus moves from
  `[66.741, 0, -21.212]` to the fleet's own `[59.684, 0, -25.12]`.
- **A STARLANE is a move target in its own right, not just a road to one.**
  `GalaxyGarrisonCursor.GetGalaxyPathToTargets` (:329-342) resolves the hovered galaxy node OR a
  `GalaxyLinkCursorTarget`'s `Link`, and `GetGalaxyPathToLink` builds the route in two halves: the
  ordinary path to whichever extremity the fleet comes in by, plus one `AddMovement` transition
  towards the other. Measured through the mod's own menu on a fleet docked at one end: the posted
  order's `Fleet.Path.Destination` is the FAR node — which is how a fleet is pointed down a lane
  into the dark, since that far node need not be explored. A fleet already flying that same lane is
  answered with the path to its own `NextValidNodePosition`, the game's way of saying "you are
  already doing that".
- **A screen whose own buttons rebuild its window must split arrival from departure.** Merging two
  fleets destroys both and builds a third; the window goes not-ready for a frame or two, and a plain
  `Shown && IsReady` gate stood the screen down mid-order — the transcript read "Galaxy" plus the
  whole HUD, then the panel again. Asking `IsReady` once on the way in and only `Shown` thereafter
  (one instance bool) leaves the Garrison order's transcript as two live "unavailable" parts and
  nothing else. Before/after measured on merge vs garrison in the same run.
- **Names for a fleet panel's wordless buttons and columns are in the game's data, twice over.**
  The action buttons resolve through `Gui.GetTitle(definitionName)` — which hands back the KEY, so
  `AgeText.Clean` localizes it — and the toolbar/management buttons have their own
  `%Fleet{SelectAll,SelectAllShips,Merge,Disband,CreateFromHangar,CreateFromShips,Repair,Retrofit,
  Scrap,Sell,SpecializeJuggernaut}Title`. The fleet-line COLUMNS are named by
  `%FleetListTable{CommandPoints,MovementPoints,Health}Title`, whose values are icon tokens
  (`[commandPoint]`), so `AgeText.Clean` turns them into "Command Point", "Movement", "Health".
- **`GalaxyView` has two `SelectGameNode` overloads and they do different things.** The one taking a
  `GameNode` force-zooms (`SelectNode` → `ZoomInOnNode`); the one taking the map's own `GalaxyNode` —
  which is what a real left click reaches, via `GalaxyStarSystemCursorTarget.GalaxyStarSystem` — asks
  the colonized-star-system repository first and requests the **management view level** for a colony
  of the player's, force-zooming only for everything else (`GalaxyView.cs:110-165`). So "what the left
  click does" is not one answer, and a mod that wants the zoom must call `ZoomInOnNode` (or
  `GalaxyViewLevels.ZoomTo`) rather than the click's own entry point.
- **The map's right-click undo of a zoom is per-VISIT.** `GalaxyViewLevel_GalaxyOverview.ZoomInOnNode`
  sets `hasZoomBeenForced` and `RestoreZoom` needs it; leaving the overview level and coming back
  (into a system's management page and out again) clears it while the CAMERA stays where the zoom put
  it — measured: `zoomStep` still 12, `HasZoomBeenForced` false. So "come back out" is an offer that
  can disappear under the player without the view changing, and a screen that reports it has to ask
  the flag every time rather than remember having zoomed. **And the converse traps too**: a force
  initiated while ALREADY at step 12 saves step 12 as the parameters to restore, so `RestoreZoom`
  with the flag TRUE can be a talking no-op (measured: "Zoomed out" spoken, `zoomStep` unmoved,
  flag still set — the engine's restore does not clear it). The mod's backslash therefore never
  calls `RestoreZoom`: `ZoomToStep(node, DefaultZoomStep)` at the focused system whenever
  `ZoomStep > DefaultZoomStep`, which is deterministic in every state the camera can be in.
- **`StarSystemLabel.RequestManagementViewButton` is the map's only route into a colony's page, it has
  no tooltip, and its `Enable` IS the ownership test.** `RefreshStarSystemNameLine` (:1750) writes
  `Enable = MainColonizedStarSystem != null && MainColonizedStarSystem.Empire == Parent.LookingEmpire`,
  and the widget carries no `AgeTooltip` at all (measured on the drawn label) — so `Visible &&
  Operable` answers "is this a colony of mine" without asking the model, and anything declaring the
  button has to bring its own name.
- **Selecting a DOCKED fleet selects the docking SLOT, and the selection follows the slot's contents.**
  The idle-fleet route leaves `DockingGalaxyGarrisonCursor` up (a `GalaxyGarrisonCursor` subclass);
  `IGuiSelectedGarrisonsRepositoryService` then reports whatever is parked in that slot, so ordering
  the selected fleet away leaves the OTHER fleet at that system selected — measured: a send emptied
  the selection onto the second fleet, and the next send moved that one. A fleet under way selects to a
  plain `GalaxyGarrisonCursor` and is the only thing selected. Anything that posts one order per
  selected fleet has to re-read the repository at the moment of the press, never remember it.
- **The galaxy's world axes are a fixed compass: +world X is EAST, +world Z is NORTH.**
  `GalaxyPosition` is the flattened world position — `X = world.x`, `Y = world.z`
  (`GalaxyPosition.cs:38-42`) — so the bearing from one node to another, clockwise from north, is
  `atan2(Δx, Δz)` on those two fields and nothing else. Measured against the live camera's own
  `WorldToScreenPoint` at the fixture's home view: from Dusay (screen centre, 640,400 of 1280x800)
  Primus is bearing 38.3° and lands at screen +451,+492 (up and right), Qarius 347.8° at -130,+519
  (up), Rigel 253.8° at -737,-184 (left and slightly down).
- **The galaxy camera never rotates, so a compass word is world-fixed.**
  `GalaxyViewCameraController.StartRotating()` is private with zero call sites and the live camera
  reads `euler = (59.5, 0, 0)` — pitch only. Nothing spoken about direction has to track a yaw, and
  a screen that did would be handling a case the game cannot produce.
- **A management-page planet card has no click of its own — the click the game answers is the one on
  the PLANET behind it.** `PlanetLabel_SystemManagement` wires handlers only to its own buttons
  (colonize, terraform, build, reduce anomaly, rename, the status label, the decolonize toggle) and its
  root transform carries no `AgeControl` at all (measured on all three of Dusay's cards:
  `rootAgeControl=none`). What opens a planet's page is `GalaxyPlanetCursorTarget.OnCursorClick`
  (:30-53) — a LEFT click on the planet's own 3D body, refused while `AgeManager.Instance.MouseCovered`
  (i.e. over the card) or while a population marker is hovered, and gated on the current view level
  being `GalaxyViewLevel_SystemManagement` — which asks for `GalaxyViewLevel_PlanetOverview`. The wheel
  does the same through `SystemManagementCameraController.HandleInput(ZoomIn)` (:137-155). So "Enter is
  the card's click" means the planet's page, and it is the same call `GalaxyViewLevels.OpenPlanet`
  makes. The card's `PlanetRenameButton.Enable` IS the game's `CanRename` predicate
  (`RefreshPlanetBasicInfo` :1271 — client ready, not `IsUnique`, a colony of the player's), and the
  button carries an EMPTY tooltip while it refuses, so it needs the mod's own name.
- **Moving population is a drag with a STATIC in the middle, and the static is read every frame.**
  `PopulationEnumerator.DragInfo` (a single static `PopulationDragInfo`) is filled by
  `OnPopulationMarkerDragStarted` (:240-253) — whose own two gates are `owner.CanMovePopulation` and
  `IPopulationsManagementService.CanMovePopulation(affinity)` — and the commit is
  `PlanetLabelsWindow_SystemManagement.IDragDropClient.ApplyDrop` (:18-47), which posts
  `OrderTransferPopulationFromPlanetToPlanet(empire, source.GUID, target.Planet.GetColonizedPlanet
  (player).GUID, affinity, quantity, replacedAffinity)` for a planet card and a PAIR of
  `OrderTransferSpaceportPopulation` for the spaceport panel, then plays the drop sound. Which targets
  the drag offers is `GetPopulationDragDropTargets` (:67-77) — every shown label whose
  `PlanetPopulationEnumerator{Focused,Ghost}.CanAcceptPopulationDrop()` says yes — plus the spaceport
  panel while it is shown. Two traps: **`CanAcceptPopulationDrop` THROWS unless a drag is already in
  progress** (it dereferences `DragInfo.TransitingPopulation`), so it can only be asked with the static
  filled in; and `BuildListOfGuiPopulations` (:132-167) subtracts `DragInfo.Quantity` from the source's
  drawn markers every refresh, so a static left set draws units that have not gone anywhere. The
  refusal underneath is `ColonizedPlanet.CanWelcomeSomeOfPopulation` (:307-317), a bare bool with **no
  `FailureInfo` and so no game sentence** — the only population refusal the mod cannot speak in the
  game's words. A marker's `Rank` is what the drag moves (markers of one affinity are ranked
  count…1 in draw order), and `GuiPopulation.Title` is the game's display name for the affinity
  (measured on Dusay: "Imperials", "Yuusho" for `AffinityTerrans`/`AffinityHisshos`).
- ES2's icon numbers, for re-verification: 382 registered tokens (single writer
  `AgeManager.CreateSpecialCharactersDictionary` → `AgePrimitiveLabel.SpecialCharacters`,
  keys `"[TOKEN]"` upper-cased), 371 named + 11 nameless colour directives; localization
  corpus 25 821 strings, 1 861 with brackets.
- **The game's Space is `ToggleScanView`** (`InputManager.cs:233`, one binding shared with Mouse2) —
  the strategic lens mode that sets `IsInScanView`, drops `IsInNormalView` (hiding the pinned quest
  and most HUD) and repaints the whole map, unmodelled by the mod (roadmap row). This is why the
  mod's drag key over-claims Space on every mod screen (owner decision 2026-08-11) instead of the
  conditional claim it launched with: a screen-reader user reaching for a pickup must never flip the
  map into an unannounced mode. The lens keeps its Mouse2 route; `InputAction.ClaimedWhile` remains
  for a conditional hand-back once the lens is modelled and can announce itself.

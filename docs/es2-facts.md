# ES2 facts — reverse-engineered mechanisms

Game-mechanism findings with no home in the code: how ES2/Amplitude actually behaves, each
measured or cited. The loop itself is `dev-loop.md`; helpers are `helpers.md`; layers and
keys are `interaction.md`; per-screen recipes are `test-recipes.md`; game-agnostic doctrine
is `docs/generic/`. A new fact lands here — never in those — and anything that turns out
generic graduates to the generic docs.


- **A notification popup that draws its own content leaves the shared description UNWRITTEN, and
  the game hides the label rather than clearing it.** `NotificationWindow.Refresh` (:247-251) writes
  `NotificationDescription.Text = GuiNotification.GetDescription()` **only while that label is
  visible**, and a window like `TechnologyUnlockedNotificationWindow` parks the label under a hidden
  `Dummy` and draws cards of its own instead. `NotificationTechnologyUnlocked` overrides only
  `GetTitle()` (:51-55), so both the parked label and `GetDescription()` answer
  `%NotificationTechnologyUnlockedDescription`, which localizes to the template with its hole still
  in it: "Research has been completed: {0}". A second form of the same thing:
  `%NotificationTechnologyStageUnlockedDescription` has NO translation at all, so `Gui.Localize`
  hands back the key (measured). **Mod policy** (`NotificationScreen.Text`): a description whose
  label the player cannot see, or whose text is still a brace-digit template or a raw `%key`, is
  ABSENT — never spoken, never a words node, never buffer content — and such a popup is read off
  what it DRAWS instead. Titles are exempt: every measured popup formats its title properly, and the
  one unfilled title seen (`"Research Complete: {0}"`, mid-browse) was a stale frame, which is why
  the change watcher waits for `IsReady`.
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
  arity (cached) and pick the overload; the generic rule is widgets.md's arity contract. The
  mismatch runs BOTH ways and a C# default argument does not save you: `SendMessage("Cb")` on a
  handler declared `Cb(GameObject obj = null)` logs "Calling function … with no parameters but
  the function requires 1" and does nothing (measured on
  `OutpostInfoSidePanel.OnClickChangeColonyCb`). From `/eval`, invoking such a private handler
  by reflection with an explicit `new object[]{ null }` is the reliable route. A double-click
  handler is the same trap the other way round: `OnLineDoubleClickCb` takes NO argument while
  the engine dispatches with one, so arity resolution is required — and
  `MilitaryScreen.OnLineDoubleClick` then acts on `SelectedFleet`, not on the line it was
  passed, so the row must be selected first for the replay to mean anything.
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
  database needs that fallback, and silence rather than a `%key` as the last resort. The same
  hazard has a second face: `Gui.GetLocalizedTitle` answers for a missing element with a PINK
  DESIGNER PLACEHOLDER or the raw `%Key` rather than failing, so every title read this way is
  tested before it is spoken (the measured list of offenders is in the stage-5c report).
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
- **"Go and look at THIS technology" leaves no state behind.** Every way the game takes the player
  to a dot — Ctrl+click on a hint button (`GuiButtonHint.ActivateHint`, the colonize and buy-out
  buttons), a technology-unlocked notification — calls `TechnologyScreen.FocusTechnology(GuiTechnology2)`
  (:154-167) and then `ShowWindow`. With the page already up it acts at once (`ForceZoomIn` +
  `TechnologyItem2.FocusTechnology`'s pulse) and stores nothing; with the page closed it stashes the
  technology in the private `FocusedTechnology`, which the show coroutine `DefferedDoZoomIn` (:776-790)
  consumes and nulls the moment the window appears. So nothing readable survives the open, and a mod
  that wants to put its cursor where the VIEW was sent has to hear the call itself (a Harmony patch on
  that one overload) rather than poll for a result — `ES2Access.Screens.ResearchLocate`, consumed by
  `ResearchScreen.OnUpdate` and dropped again when the page closes.
- **"Go and look at THIS place on the map" is three calls, and they nest.** `IGuiGameWindowService`
  is where every reveal in the game ends up, and only three of its members move the galaxy view:
  `RequestGalaxyOverviewViewLevel(IGameEntityWithGalaxyPosition)` (`GuiManager.cs` :1170) forwards
  straight to `RequestGalaxyOverviewViewLevel(Vector3)` (:1175) and DROPS the entity, and
  `ShowQuestLocation(Quest, QuestStep)` (:1264-1286) picks a marker and then calls the same `Vector3`
  overload. So a patch on "the call site" gets the poorest signature — hook all three, and note that
  postfixes fire inner-first, so the richer outer capture naturally overwrites the poorer one it
  caused. Measured: 51 player-facing flows (notifications, panel locate buttons, table double clicks,
  the traitor banner, the next-idle-fleet button) reach the map through them.
  `ES2Access.Screens.GalaxyLocate` is that capture; `GalaxyHudScreen.OnUpdate` consumes it.
- **`ShowQuestLocation` CYCLES markers** through a private `lastShownMarkerIndexByQuest`, keyed on
  quest name + step name — press the pin twice and the camera goes to the next marker. Nothing needs
  to read that dictionary, though: the method resolves its marker and then makes the ordinary
  position request with it, so a hook on the position call already has the chosen marker's own
  position. A quest with NO markers makes no request at all and moves nothing.
- **`RequestStarSystemManagementViewLevel` silently degrades to a galaxy centre.** For a system that
  is blacked out (:1224-1228) or that the player neither owns nor has a traitor in (:1244-1247) it
  calls `RequestGalaxyOverviewViewLevel(component.Position)` instead — no page opens, and the only
  feedback a mouse user gets is the camera sliding. Measured from `unlocked` on a non-owned system:
  the reveal capture fires and the mod says "Shown on the map" rather than announcing a page.
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
- **Every `GuiTableLine` in the game carries a `DoubleClickButton`** (`GuiTableLine.Bind` :96-99
  wires it to `OnLineDoubleClickCb` → the table client's `OnLineDoubleClick`) — measured: 14 lines
  live in `unlocked`, none without one. Only eight classes implement the handler
  (`StarSystemsManagementPanel` :434-441 opens that system's management page, `MilitaryScreen` :511
  shows the fleet on the map, `FleetSelectionModalWindow` :181 and `SystemSelectionModalWindow` :184
  pick and close, `LoadSaveModalWindow` :401 loads or overwrites, `JoinGameScreen` :435, and
  `MarketplaceBuyableItemsPanel` :354 and `HeroCompleteListModalWindow` :83 are EMPTY), and
  `FleetsScreen` is not among them. All eight read `GuiTable.SelectedLine`, never the line they were
  handed, so replaying the gesture means selecting the row first.
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
  which is why the mod's explicit-initiator send (backslash) posts moves without needing the selection stores wired.
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
  `[66.741, 0, -21.212]` to the fleet's own `[59.684, 0, -25.12]`. **The last call also CLOSES
  whatever full screen the player is on** — measured from `unlocked` with `MilitaryScreen` shown:
  the request alone put the galaxy page back and no window had to be hidden by hand. So a locate is
  a screen change the mod hears as an ordinary re-entry of the galaxy, and the cursor it re-seats on
  arrival is the only thing that tells the player where the game has taken them.
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
  with the flag TRUE can be a talking no-op (measured: the mod spoke, `zoomStep` unmoved,
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
- **The spaceport side panel needs a system improvement before it exists at all.**
  `SpaceportSidePanel.CanBeShown()` is `Spaceport.IsAvailable()`, and that is
  `MaxPopulation > 0 && !(StarSystem is ExploitedStarSystem) && State == Colony && !IsHiddenSystem`
  (`Spaceport.cs:179-182`) — `MaxPopulation` being the `SpaceportCapacity` simulation property, which
  starts at 0. Measured in `unlocked`: one colonized system (Xiu), `IsAvailable()` false, spaceport
  population 0, the panel bound but never shown. So the panel, its markers and both directions of its
  drag are FIXTURE-BLOCKED in every save this repo has. Its markers are its enumerator's OWN children
  (`SpaceportPopulationEnumerator.PopMarkersContainer` IS that enumerator's transform, unlike a planet
  card's ring), so a walk that wants them intercepts the enumerator itself; and the panel is a child of
  `SidePanelsWindow/Viewport/SidePanelsTable`, so the shared side-panel sweep picks it up the moment the
  game draws it (proved by `SidePanelsWindow.ShowSidePanel` — `SidePanel.Show` itself throws).
  An occupied slot's tooltip is written by `SpaceportSidePanel.Refresh` :169-186; a slot the panel has
  not refreshed yet still carries the prefab's placeholder, the literal words "This is changed by code".
- ES2's icon numbers, for re-verification: 382 registered tokens (single writer
  `AgeManager.CreateSpecialCharactersDictionary` → `AgePrimitiveLabel.SpecialCharacters`,
  keys `"[TOKEN]"` upper-cased), 371 named + 11 nameless colour directives; localization
  corpus 25 821 strings, 1 861 with brackets.
- **A few symbols are painted straight into a panel and so are missing from the element-derived
  picture table.** `TurnSymbol` — the hourglass the construction-completed table draws in front of
  a build's remaining turns — is drawn by an `AgePrimitiveImage` that no `GuiElement` carries a
  token for, so the derivation that built `IconTable.PictureRows` never saw it and
  `DevProbe.UnknownIcons` listed it under `pictures`. It is now a HAND-WRITTEN row
  (`TURNSYMBOL=icon.turn`) and a regeneration must keep it: the picture is the only caption its
  number has, and it is what tells the mod that "3" in that column means turns.
- **A popup's own line handlers can take NO argument.** `ConstructionCompletedNotificationLine`'s
  wired click is `OnSelectSystemCb()` with an empty parameter list, while `AgeControlButton` sends
  `SendMessage(name, senderGameObject)`. Unity will not deliver a one-argument SendMessage to a
  zero-argument method, and with `DontRequireReceiver` it says nothing either — the row simply does
  nothing. `AgeWidgets.Press`/`Send` look the arity up first and are the only safe pressing route;
  `NotificationScreen`'s own private `Send` (used for the shared skeleton buttons, all of which do
  take the sender) does not, so anything new pressed from that screen goes through `AgeWidgets`.
- **The construction-completed popup draws a real table**, and its rectangles pair by themselves:
  the caption band (`CollumnNames`: "System" 264–504, "Completed" 502–822, "Next Construction"
  825–1145) x-aligns with each line's three cell groups (`StarSystemInfo` 264–504,
  `CompletedConstructionInfo` 502–812, `NextConstructionInfo` 825–1145), so columns can be paired by
  x-overlap with no per-window knowledge. The line (`CompletedConstructionLine000`) is a wired
  `AgeControlButton` under `ContentSW`'s scroll view, carries an EMPTY `AgeTooltip` of its own —
  which is why the pre-table reading fused all four labels into one row — and hangs the two
  `Constructible` dossiers off the `Icon` inside each figure cell, never on the cell or the line.
  A system with an empty queue hides `NextConstructionInfo` and shows `NoNextConstructionButton`
  (a group, no button component) in the same column instead.
- **The game's Space is `ToggleScanView`** (`InputManager.cs:233`, one binding shared with Mouse2) —
  the strategic lens mode that sets `IsInScanView`, drops `IsInNormalView` (hiding the pinned quest
  and most HUD) and repaints the whole map, modelled by `ScanViewScreen`. The mod's drag key
  therefore claims Space only where it can act — a pick-up on the focused control, a live carry,
  or a search collecting the space as text (`ModEntry.CarryKeyClaimed` →
  `GraphNavigator.TakesCarryKey`, owner decision 2026-08-12, after the blanket claim of
  2026-08-11). Everywhere else the key reaches the game and `ScanViewScreen` announces the lens,
  which is what made the hand-back safe; the lens keeps its Mouse2 route.
- **Everything an OUTPOST has, and how the game refuses it** (measured turn 4, Rigel; the
  card's fields are `PlanetLabel_SystemManagement` :106-143):
  - The management page draws the constructibles, queue and hangar panels for an outpost
    **bound but hidden** (`StarSystemScreen.cs:565` binds them, `Visible=false`), so a
    side-panel-per-stop model tracks the outpost/colony swap with nothing declared.
  - `OutpostActionsTable` is **pooled and over-reserved** (7 children for 3 drawn actions).
    An action the faction cannot have at all is hidden outright —
    `FailureInfo.ContainsFlag("Discard", …)` in `OutpostActionItem.Bind` — which is what
    hides the Hisshos/TimeLords/Vodyani variants; one merely refused *today* stays
    `Visible` with `AgeTransform.Enable = false`. So "drawn" and "offered" are the two
    separate questions, and the mod's `AddRefusable` shape is the right one.
  - An item **names itself nowhere on the card**: it draws only a cost. The name, category,
    description, duration, effects and cost all live on `GuiOutpostAction`, the wrapper hung
    on the item's own tooltip, whose `Title` is readable at bind time (no drawing needed) and
    whose `TooltipClass` is `"OutpostAction"` (renderer-assembled → indicated + buffered).
  - **A renderer-assembled tooltip's refusal is computable without drawing it**:
    `PanelFeatureFailureInfos.Bind` is literally
    `Gui.FormatFailureInfos(((IFailureInfosProvider)target).FailureInfos)`, and the target is
    filled in at bind time. Asking the wrapper gives the same sentence the panel would draw,
    at once — measured: "There are no enemy Outposts on this star system.", "Cannot afford
    75 Influence". (The drawn tooltip is the free oracle for it.)
  - **The cancel window is the start turn only.** `OutpostActionItem.Bind` sets
    `Enable = … && service.Turn == Action.StartTurn`; the click is
    `OnOutpostActionSwitchCb` (:1566) — `OrderEntityAction` to start (no confirmation),
    `OrderCancelEntityAction(refund: true)` to cancel. Live round trip: dust 253.81 → 103.81
    → 253.81, entity actions 0 → 1 (`OutpostActionBoostGrowthWithDust`) → 0. A running item
    swaps its cost for `DurationLabel` (`EndTurn - turn`), so the same drawn field reads
    "150 Dust" or "10 Turn" depending on state.
  - **Decolonize is a toggle whose handler flips the state again.** `OnDecolonizeToggleCb`
    (:1587) starts with `DecolonizeToggle.State = !DecolonizeToggle.State` — i.e. it undoes
    the flip the click itself made — then either posts `OrderCancelEntityAction` (already
    scheduled: unschedules with NO confirmation) or raises the game's own
    `%PlanetDecolonizeConfirmationDescription` message box. Replaying the click the ordinary
    way (`AgeWidgets.Toggle`: state then handler) therefore lands on the right behaviour;
    calling the handler alone would not. The toggle's drawn On label is the ellipsized
    "Decolonizati." — the game's real words are `%PlanetDecolonizeTitle` / 
    `%PlanetDecolonizingTitle`.
  - **The orbital card's outpost tooltip is for FOREIGN outposts only.**
    `PlanetLabel_SystemOrbital.RefreshOutpostStatus` (:490-525) blanks `OutpostTooltip.Content`
    every refresh and re-fills it with `%OutpostColonizationTooltipDescription` **only** when
    `ColonizedPlanet.Empire != Gui.PlayerEmpire`. On your own outpost the game deliberately
    puts nothing on hover, so there is nothing to buffer — verified live (content empty on
    Rigel I) and by forcing the content, which the buffer then carried.
  - `OutpostInfoSidePanel` has **no header icon of its own**, so the generic "name a panel by
    the first readable image tooltip in it" heuristic reached all the way down to
    `HappinessIcon` and called the whole panel "The overall Approval level of people living in
    this System". Its `ColonyGroup` row is also the one row of that panel with no title label:
    the colony's name is the whole of what is drawn, and the row's meaning is only on the
    group's own tooltip (`%OutpostSideColonyWhichProvidesGrowthDescription`). The corpus has
    no `%OutpostSide*` panel title at all.
  - `OutpostInfoSidePanel.ColonyChangeButton` opens `SystemSelectionModalWindow`
    (`OnClickChangeColonyCb`), now modelled by `SystemSelectionScreen` — see the
    system-selection block below.

- **The COLONY side of migration is three count-only readouts, and the corpus titles none of
  them.** `ColonyPopulationSidePanel.RefreshContent` (:197-240) draws `GrowthLine` as up to three
  groups — `EmigrationGroup` (turns until the next migrant leaves), `ImmigrationGroup`
  (`MigrationSourceSystems.Count`) and `OutpostsGroup`
  (`OutpostMigrationDestinationSystems.Count`) — each a symbol beside a bare number, each shown
  only while its own count is non-zero (`GrowthLine.Visible` is the OR of the three, so an
  undrawn one is genuinely absent rather than blank). The only words are the sentence the game
  writes onto each group's own tooltip, and it EXPLAINS the row rather than titling it:
  `%StarSystemSideEmigrationDescription` / `%StarSystemSideImmigrationDescription` /
  `%StarSystemSideOutpostsDescription` (the last naming the destination systems by
  `ColonyInfoSidePanel.FormatSystemList`). Grepping `Public\Localization\english` for
  `StarSystemSide(Outposts|Emigration|Immigration)*` returns only those descriptions and the
  emigration `Format` — there is no `*Title` for any of the three. **Mod policy**: the row's name
  is therefore the mod's own counted phrase, said off the MODEL
  (`OutpostMigrationDestinationSystems.Count`, not the drawn digits, which are already a number
  turned into text and cannot choose a plural form), with the game's sentence kept as the row's
  detail under the ordinary tooltip rule. Only the outposts group is drawn in either fixture
  (turn 4, Dusay feeding Rigel), so the other two are unnamed because they are fixture-blocked,
  not because bareness was chosen for them.

- **`SystemSelectionModalWindow` is a GENERIC "pick one of your systems" window, and its Exit
  commits nothing.** Six panels open the one window with their own `Purpose` and their own
  three delegates (`OutpostInfoSidePanel`, `SpaceportSidePanel`, `ShipsSpawnPointSidePanel`,
  `AcademyScreen`, `MarketplaceBuyableItemsPanel`, `NamedShipInfoPanel`), so nothing about the
  opener is visible in the window — model it off the drawn title, the drawn headers and the
  drawn rows. What matters for a mod:
  - **Cancel and Escape are safe here, unlike the faction chooser.** The class overrides
    `OnCancelCb` only to raise its own `OnCanceled` event (:164-171) and declares NO
    `HandleInput`, so Exit takes `GuiModalWindow`'s own `HideWindow` (:36-42). The commit lives
    in `OnValidateCb` and `OnLineDoubleClick` alone (:184-201) — both call `SelectDelegate`.
    That is the exact opposite of `FactionChoiceModalWindow`, whose overridden Exit routes to
    its Validate handler; the two windows look identical and behave oppositely, so read the
    override before trusting a Cancel.
  - **The commit is a DOUBLE click, and it is destructive per opener.** For the outpost the
    delegate posts `OrderChangeOutpostGrowthProvider` (`OutpostInfoSidePanel.SelectColony`
    :150-157), which resets the outpost's ship timer even when the colony picked is the one
    already set. A single click on a line only selects it (`OnLineSelection` :179-182 merely
    re-weighs `ValidateButton.Enable`), which is why the mod wires Enter to the line's own
    single click and leaves the double click to the Confirm button.
  - **The window does not pre-select the system currently in force.** `OnBeginShow` →
    `GuiTable.BeginShow()` sets `SelectedLine = null` every time, so Confirm opens disabled
    even where the outpost already has a provider (measured: Rigel's `OutpostMigrationSource`
    was Dusay and the Dusay row still opened "not selected").
  - **A refused system stays in the list, disabled, with the opener's sentence as a `Simple`
    tooltip on the LINE.** `GuiColonizedStarSystemObject.OnBind` (:34-44) writes
    `failDescription` into `line.Tooltip.Content`, nulls its `Target` and forces the class to
    `Simple` — i.e. the reason is readable off the widget, no drawn tooltip needed. On an
    accepted line the tooltip is left empty (`GuiTableLine.Unbind` releases it), so declaring
    it unconditionally costs nothing.
  - **Cell tooltips are of both kinds in one row.** Measured on the `SystemListTable`: the five
    income columns carry class `SimulationProperty` and Approval carries `StarSystemHappiness`
    (renderer-assembled — indicate + buffer), while Policy and Hangar carry no class and a
    `%…Description` key in `Content` (announce). Population and Construction carry no tooltip
    on the cell at all and hang theirs on the icons inside. Two columns — `AssignedHero` and
    `ResourcesIncome` — are drawn, visible and EMPTY for a plain colony.
  - The header row is `TableGuiElement.Columns` in order and each header's caption is
    `%<TableName><ColumnName>Title` with the tooltip `%…Description`
    (`GuiTableHeader.Refresh`), so the five income headers draw as bare icon tokens
    (`[foodColored]`) that only `AgeText.Clean` turns into "Food". Pair a cell with its header
    by `GuiTableCell.ColumnInfo.Name == GuiTableHeader.PropertyName`, never by index —
    `GuiTable.SetSort` re-sorts the LINES and leaves the columns alone, but the pairing by name
    is what survives a table that ever reorders them.
  - Sorting has no feedback of its own beyond the row order and a sort arrow's alpha
    (`SortStatus`), and `GuiTable.CurrentSortPropertyName` is the only readable answer to
    "which column is the list sorted by".

- **A `PlanetLabel`'s show is camera-gated and can be dropped for good.** `PlanetLabel.OnBeginShow`
  (PlanetLabel.cs:423-428) does not show anything: it sets `AgeTransform.Visible = false` and hands
  the reveal to `ShowWhenTransitionFinished` (:443-473), which waits for the planet's screen position
  to stop changing (i.e. for the camera to stop) before measuring where to draw the card. That
  coroutine has two bare `yield break`s — one when the weak `ICameraService` reference is dead or
  null, one when `ICameraService.Camera` is null (:451-459) — and **nothing retries**. So a show
  issued while the camera is being swapped, which is exactly what `PlanetScreen.BindPlanet` (:100-112)
  does on the way into the planet overview, can be lost outright: the label ends bound to its planet
  and idle-hidden, and stays that way. Measured live on the planet page: `card.Shown=false`,
  `Showing=false`, `Hiding=false`, `card.Planet != null`, with `PlanetScreen.Shown=true`,
  `IsReady=true`, `Planet != null`, no modal and not scanning — the game drew the two left-hand side
  panels and no card at all. **The stuck signature is unambiguous**: `GuiPanel.Shown` is
  `(Visible && !Hiding) || Showing`, and the only thing that hides a bound label also unbinds it
  (`PlanetScreen.UnbindPlanet` :114-121), so "bound and idle-hidden" is never a resting state the game
  puts itself in. **The repair is the game's own call**: `GuiPanel.Show()` runs `OnBeginShow` again
  whenever `Shown` is false, and once the camera is back the coroutine completes normally
  (verified live). Two things make a retry delicate — the coroutine leaves `Showing` FALSE for its
  whole wait, so a healthy pending show is indistinguishable from a lost one except by how long it
  lasts, and the wait is unbounded while the camera moves, so a retry must be gated on the view
  standing still (`GalaxyView.CanChangeGalaxyView`, i.e. no `GalaxyViewLevelTransitionCurrent`;
  re-entering the same level with a new subject, which is how the page steps between planets, is a
  transition too). `PlanetOverviewScreen.FinishArriving` is the mod's repair.
  **All four subclasses share the base coroutine** — `PlanetLabel_PlanetOverview`,
  `PlanetLabel_SystemOrbital`, `PlanetLabel_SystemManagement`,
  `PlanetLabel_SystemManagementScanView`; none overrides `ShowWhenTransitionFinished`, and only the
  last two override `OnBeginShow` at all (both call base). So the orbital and management cards can be
  lost the same way; only the planet overview is repaired so far, because only there was it measured.

- **`GuiManager.ShownGuiPanels` leaks (parked, unanalysed).** Measured on the turn-4 fixture while
  the planet page was up: the collection held ~80 `FleetsScreen` entries and 2 `StarSystemScreen`
  entries — panels shown without a matching hide, accumulating over a session. Nothing the mod does
  reads that collection, and no symptom has been traced to it; noted here so the next investigation
  starts from a measurement rather than from a fresh surprise.

## Focus, text fields and the engine's own keyboard

- **The engine delivers keys to the focused control in `LateUpdate`.** `AgeManager.LateUpdate`
  (:919-923) sends `KeyDown` to `FocusedControl` on any `anyKeyDown` frame, and
  `AgeControlTextField.KeyDown` (:76-81) polls `GetKeyDown` itself on top of that. With
  `RenameModalWindow.OnBeginShow` (:74-82) taking focus SYNCHRONOUSLY, one physical Enter could
  open the box, be delivered to the field, validate and hide it — a rename that committed
  nothing. `GameKeyboardHandover` is the answer (helpers.md).
- **A text field stands the WHOLE mod layer down.** `AgeControlTextArea.IsKeyExclusive` true is
  the signal; `AgeControlDropList` and the key-binding field are the only other exclusive
  controls in the game.
- **The engine's Escape for a key-exclusive field only UNFOCUSES it** (`InputManager.cs`
  :1210-1241) — it never closes the surface around it — and while a field is exclusive the
  engine swallows every other game hotkey (the camera excepted). So the mod owns the exit from
  any keyboard it hands over.
- **Hiding an AGE window does NOT unfocus its text field** (measured: field `Visible=True`,
  `activeInHierarchy=True`, window `Visible=False`), which kills the mod's key layer
  permanently — and it happens on the rename box's own hide path.
  `AgeManager.FocusedControl`'s setter runs FocusLoss/FocusGain (:277-301), so clearing it is
  the game's own hand-back.

## Windows, layers and the modal stack

- **`GuiManager.ModalOnTop` is the game's record of the topmost SHOWN modal** (:310, written
  :1750-1765). An exclusive stack WITHDRAWS the window underneath (its `Shown` goes false), and
  `ModalOnTop` is null during a modal's own close — the frame on which a window's own flag and
  this record disagree.
- **The icon-strip screens are engine-exclusive.** `BackgroundRenderer` carries
  `GuiWindowsStackExclusive` — a prefab component, no code assigns it — so no two strip screens
  can be shown at once, which is what lets them share one mod layer.
- **`AgeScreen.SortingOrder` is the engine's own draw ladder** (Label 0 … ModalRenderer 5 …
  OverlayRenderer 6). It puts a NOTIFICATION under a modal, which is why the mod's notification
  layer sits below every modal rather than above them.
- **Every rename in the game is ONE box.** Whatever the opener — a system, a fleet, a ship design, a
  hero — it funnels through `IGuiService.RequestNewName` (`GuiManager.cs:2364`), which shows the one
  `RenameModalWindow` with the caller's callback. So a rename screen is written once and every opener
  inherits it.
- **`ContextualPromptWindow` has NO keyboard dismissal of its own**: it declares no `HandleInput`, and
  `ScanOverlayWindow` — which is up whenever the prompt is — swallows Exit, so the game's only ways
  out are the cross, a right click and a click away. A mod screen over it has to supply the close
  itself.
- **Tutorial pages declare their own draw layer**: `TutorialPopupLayer` is per-page, and 49 of
  233 pages declare one ABOVE modals — so the tutorial screen has to sit near the top of the
  mod's ladder, with only the error and message boxes above it.

## Tables, pools and clicks

- **`GuiTable`'s can-select flag is recorded as `LinesTable.Enable`** (`GuiTable.Bind` :130, its
  only writer), so an ancestor-walking "is it operable" test conflates read-only with refused. A
  refused ROW is the line's own `AgeTransform.Enable` (`GuiTableEntry.OnBind` :22-27).
- **`GuiTableLine.OnLineSelectionCb` clears `ClickedCell` AFTER notifying** — read the cell,
  then the line, in that order, or the cell is already gone.
- **AGE clicks PROPAGATE.** `propagateInteraction` defaults true (`AgeControl.cs:19`) and
  `MouseUp` re-delivers up the chain (:170-192); the engine reaches the hit target by
  `SendMessage` (`AgeManager.cs:890` — where the click audio comes from) and the ancestors by a
  plain C# call, with no audio.
- **AGE `ReserveChildren` tables retire rows by FADING** (alpha 0, `Visible` still true) — the
  third retirement style, beside the surplus-child alpha 0 of "`Visible` is not 'drawn'" and the
  scan view's pool that parks stale children fully visible outside the table's extents. Every
  per-row read gates on painted-ness. A retired row also keeps its old RECT, which is how the
  planet card's climate table (`PlanetGameplayTypeTable` — the one table on that card whose `Load`
  does NOT set `StrictVisibility`) put the previous planet's biodiversity line on top of the
  curiosity line and banded the two into one drawn row: a faded row is a layout hazard as well as a
  phantom line.
- **`GuiRadioGroup` rewires its child toggles and ignores `State`** — the group is the authority,
  not the toggle it holds.
- `SystemSelectionModalWindow` binds `interactiveCells: false`, so its shipped table has no
  interactive cell at all.
- **`StarSystemPlanetCardsPanel` is its own drop client** — the panel, not the page around it, takes
  both population moves: planet→planet, and the spaceport shipment, which the game routes through
  `GuiTableCellSystemPopulation`. So a second host wanting the same drag wires the panel, not the
  screen.

## The icon-strip screens (senate, empire, economy, research)

- Government: **the Validate button's missing-technology hint is the LAST of three refusals**, so a
  save can refuse the change without ever lighting the hint. `GovernmentModalWindow.Refresh`
  :204-214 tries `GovernmentChangeLocked`, then `GovernmentChangeCooldown`, then
  `MustHaveTechnology`, and only the third reaches `FormatButtonHint`. Measured in `unlocked`:
  `Enable` false, `IsHintActive` false, the `GuiButtonHint` component present but with no technology
  in it (`GuiButtonHint.IsActive()` is exactly `GuiTechnology != null`). Under a hint the game's
  `OnValidateCb` :379-395 does hint-jump AND close, both gated on the physically held Control —
  which no injected key can reproduce, so that pair is manual-script only.
- Senate: **`GuiPolitics.Title` contains the party SYMBOL** — `GetLocalizedTitle(Name)` is the
  bare word. An emptied `SenatorCard` keeps its old words, so a card is gated on the model, not
  on its labels. And costs and totals live INSIDE the control they belong to: `LawsWindow`'s
  `InfluenceCostLabel` is a child of `VoteButton`.
- Empire: **`SystemListTable`'s five interactive cells all `PropagateInteraction` with an
  `OnClickCb`** — a two-step gesture where the cell records `ClickedCell` and the propagated
  toggle opens the panel; the resources column carries a handler-less `DummyButton`.
  `EmpireStatusSidePanel.HappinessAndRebellionGroup` is wired to a method that exists nowhere,
  and `EmpirePerformanceTracker` titles can be parked (the game itself draws "?").
  `EmpireBanner` draws exactly ONE buy-out button (Influence) for the UE at turn 1.
- Economy: **`GuiLocatedResource.TargetEffect` always throws** (its ctor never assigns the
  element — go through `Gui.GetGuiElement`). `ResourcesPanel.RefreshResourceItem` decays a
  soft-hidden item's alpha by ×0.3 per refresh and never restores it, so alpha is the only drawn
  test there. A tooltip's CLASS is rebind-fresh while its TARGET can be stale (slots, resources,
  salables) — take a name from the target only when the class says the rich variant is bound.
  `AdCreationModalWindow` is a dead stub (unregistered, its opener never shown);
  `EconomyScreen.ToggleSystems` is null live, so the tab strip is read off the drawn table;
  `%TargetEffectIndustryTitle` contains the game's own icon typo ("Improves Industry [foodColored]"),
  which is why a resource family's column is NOT named from its `TargetEffect` title: the heading is
  drawn as an icon alone (`EconomyPanel.RefreshResourceHeader` :177-185), so it speaks the resource
  the family improves, off the game's own short titles (`%SubCategoryFoodTitle` … ,
  `%CategoryManpowerTitle`, `%HonorTitle`) keyed by target effect, and the sentence stays on the
  heading's tooltip; a compound strategic family (`TargetEffectFoodIndustry`,
  `TargetEffectSystemDevelopmentEffects`) has no short word and keeps the title. And
  `ResourceItem.OnClickCb` is god-mode-only.
- Economy, luxuries: **the luxury grid is a GRID** — the items cycle through 8 target effects with a
  period of 8, so the columns are the FIDSI families and a row read as a flat strip loses which
  family each figure belongs to.
- Empire, the side panels: `EmpireDescriptionSidePanel` hangs its tooltips on the LABELS inside each
  group and on each icon — never on the group itself, so a group-level read finds nothing.
  `EmpireStatusSidePanel.PanelTitle` is the ONLY side-panel heading in the family that carries a
  tooltip of its own.
- Research: **254 of 385 technologies carry an affinity badge** — a majority, so the badge is
  ordinary content rather than an exception.
- Politics: `PoliticalEventsPopulationPanel`'s table binds `canSelect:false`, has per-system
  columns, keeps names only on the tooltip WRAPPERS and values only as cell tooltips, and its
  `%SystemPopulationPoliticsTable*Title` keys are parked.
- Election: **the action outcomes are never drawn** (`ElectionFinalPanel.Refresh` :180-181 hides
  both branches unconditionally), the modal nulls `OverrolledTransform`/`FocusedControl` on every
  step change (:71-77) so a hover highlight must be re-armed, and `%ElectionScreenTitle` is a
  parked key.

## Military, ships and the designer

- **`EnrollButton` is invisible early game** — the button actually drawn there is
  `UpgradeButton`, which opens the ground-troop modal — and `OnClickManPowerCb` is entirely
  god-mode (7 groups, so they are declared transparent).
- **Module tiles are double-click-only** (`UseLeftClick=false`), and the slots wire
  `OnSlotUnequipCb` to BOTH the empty frame and the fitted button. The category filter DIMS
  slots, so enabled ≠ will-take-this-module — `CanModuleBeBound` is the test, and the game's own
  drag re-enables the compatible ones.
- **A drag can be COMMITTED without starting one**: fill
  `DragDropWindow.ShipDesignModuleDraggedItem` and call `ApplyDrop` — never `StartDragDrop`.
- `ShipDesignItem.OnToggleCb` forces `State=true`, so there is no de-select click (null the
  panel's property instead); costs, stats and the module list are hidden while a design is
  invalid (the fresh-Create state); and the designer's resource items are god-mode readouts
  named through `TooltipTitle`.
- **`RepartitionHorizontalGauge.Refresh` HIDES a half whose value is zero**, so a reading of the
  hidden half is unfalsifiable until a fixture gives that half a value — which is how a right-hand
  share measured against the bar's far end instead of its middle (163% with energy at 37%) survived a
  whole audit unseen.
- **"Behemoth" in the game's fiction is `Juggernaut` in the code** — grep both spellings or half
  the family is invisible.
- Fleet actions and columns are named twice over in the game's data: the action buttons resolve
  through `Gui.GetTitle(definitionName)`, the toolbar has its own `%Fleet*Title` keys, and
  `%FleetListTable{CommandPoints,MovementPoints,Health}Title` name the columns.

## Heroes and the academy

- **A hero's `SkillPoints` is Level − 1** (a simulation property); `SpentSkillPoints` is what the
  save serializes.
- **`HeroSkillTreeSkillItem` writes then DESTROYS both halves of its prerequisite feedback**
  (:113/:142 enable, :119/:159 tooltip) — which only bites the Nakalim and Templar trees, since
  no base-game skill declares a `RequiredSkill`.
- The inspection hub's slide is ONE 0.3 s offset interpolation, and the engine re-enables the
  arriving panel only AFTER `ModifiersRunning` ends.
- The game's own `%SkillTreeAvailableSkillPointsTitle` is abbreviated, and every starting skill
  titles as "Starting Skill" (only the dossiers differ).
- Hero-card figure captions are `%HeroCardExperienceTitle` and friends; unspent points, cooldown
  and relics borrow `%HeroInspectionRemainingSkillPointsTitle`,
  `%AssignmentCooldownBaseDurationTitle` and `%HeroRelicTitle`.
- `HeroSelectionModalWindow.Refresh` (:74-77) wipes `SelectedHero` through an inverted
  `Contains` — never cache it.
- **`%SkillTreeStageLevelTitle` + `RequiredLevel` is a per-RING unlock threshold**, drawn as a
  leader-line legend beside the ring — not a skill's name and not a per-branch total. Read as either
  it says the wrong thing about every skill on the ring.

## Battles

- **There is no battle HISTORY**: the encounter records are `SkipSerialization`, and
  `PastEncounter` is a marker COUNT, not a list. Anything the player wants to re-read has to be
  read while the battle's own surfaces are up.
- `GroundTroopUpgrade` leaves its tooltip EMPTY while locked (the reasons had to be reproduced by
  hand), and manpower upgrades have no `GuiElement` names at all.
- The mini battle cards hide `PlayTitle` and their tooltip omits the name —
  `GuiBattlePlaySlot.Title` is the only source.
- Nine `EndBattleStatus` words; the realization labels are subjectless; the WatchBattle opt-outs
  are the game's own; the pre-roll is a raw-input gate; battle-speed keys are
  Plus/Minus/Asterisk/Pause, none of which the mod claims.
- `ShowOtherCards` does not clamp; clicking an already-selected card IS the validation; and the
  ENEMY play cards set YOUR plan.

## Diplomacy and the sweep

- The diplomacy ring draws UNMET majors; `LeaderCard` wires no control at all; the sector has no
  tooltip and a god-mode branch.
- **Closing an unsigned negotiation still posts an order**, and `EvaluationAnnotation` is
  discarded on the way.
- `AcademyModalWindow`'s Bind can WEDGE the window (recovery in test-recipes), and
  `PirateDiplomacy.Refresh` throws outright when there are no pirate systems.
- `Gui.FormatFailureInfos` returns the BASE text when every failure is ignorable — an empty-looking
  refusal that is really "nothing to report".
- The non-blocking box's countdown lives in the MESSAGE, not in a field of its own.

## Galaxy labels, probes and the scan view

- **A starlane is ONE `Link` shared by both end systems**, so per-system nodes built from a link
  must key STRUCTURALLY (measured as a focus teleport on a fog-off build).
- `PlanetCuriosityItem` is Class-backed yet its `Content` holds real words (`FormatFailureInfos`,
  written in `Refresh`), so the refusal reads off `Content` while the name comes from the wrapper
  (there is no Title label).
- Hangar labels are drawn from `IVisibleGalaxyHangarRepositoryService` gated on `ShipsCount > 0`;
  the click is `Select(CursorTarget)` + `ChangeCursor(GalaxyGarrisonCursor)`, and
  `Hangar.LocalizedName` is `"%HangarTitle (⟨node⟩)"`.
- **`StarSystemLabel` prefab-authors a `%…Description` into every contextual icon's tooltip
  `Content`** and rewrites it at refresh, so a drawn-gated reader always has the game's own
  sentence — but some icons' content only fills once they are drawn.
- **Pooled label widgets keep the PREVIOUS system's values** (a hidden `TraitorCountLabel` read a
  stale "1"), so every label read is gated on ancestor-walked visibility.
  `DualGarrisonsLabelButtons.OnClick` selects `garrisons[0]` only — a duplicate affordance,
  deliberately omitted.
- **Probe, obliterator projectile and coordination request carry a bare `GalaxyPosition`** — no
  node, no link; `Fleet` alone stores a leg. `ProbeLabel` draws a countdown only for your OWN
  probe, and `ObliteratorProjectileLabel` writes destination and ETA only for yours.
  `WreckedMothershipLabelWindow` binds `FocusedGameNode` and its items follow the curiosity
  pattern. Constellation exploration is an aggregate recomputed on node-exploration events.
- **The scan view is a MODE, not a view level**: `IsInNormalView` goes false and only
  `EndTurnWindow` survives, while `TopTitlePanel` keeps the lens-naming label even hidden.
  `ScanViewWindowCaptionsPanel` is a pool that does not clean up (surplus children stay fully
  visible with stale words, arranged past the table's extents), so counts come from the lens's
  own `GuiElement` data through `Prerequisite.Check`. `BattleScanViewWindow` has no header (fall
  back to `Shown`), and `StarSystemOrbitalScanViewWindow` is an unregistered stub.
- `ColonyInfoSidePanel.SecurityAndTroopsTooltip` (:60, filled :549-555) hangs on
  `SecurityGroup` — `SecurityValue`'s own transform tooltip is null. `ColonyHeroSidePanel` swaps
  variants by `Visible` flags only (:157-240), the unassigned prefab keeps STALE hero text, and
  the unassign button is spelled `UnssignButton`.
- **A system has TWO star tooltips** — the label's and `PlanetLabelsWindow_SystemOrbital.StarTooltip`
  — swapped by the camera; both class-backed, so only the drawn one has words: resolve at READ
  time, never remember one. At orbital zoom the label group's top edge leaves the screen (y=-1
  measured), so a tooltip anchored to it draws clamped away from it — a camera-dependent pointer
  must be re-committed on camera change (`GalaxyHudScreen.FollowCamera`).
- **The remaining label readouts**: the KOTH score figure exists only in
  `KingOfTheHillScoreLine`'s ROW tooltip content; deposit exploited-state =
  `StarSystemLabelDepositItem.ResourceImage.AgeTransform.Enable`; `DualGarrisonsLabelButtons` ship
  counts sit on each button's `ShipCountLabel`; `AcademyGroup`'s own tooltip is bound to the
  SYSTEM dossier (`StarSystemLabel:1777`) — never read it as the group's own.
- **The game's keyboard zoom is unusable as shipped**: `ZoomIn:PageUp`/`ZoomOut:PageDown`
  defaults, but `KeyboardZoomStepByStep=False` so a TAP moves nothing (held ramp, one notch per
  0.1 s); the galaxy camera answers by POLLING (its `HandleInput` is a stub); the
  system-management and planet-overview controllers answer `InputAction.ZoomIn/Out` only while
  `!AgeManager.IsMouseCovered`. Camera layers per step: 0 Painting, 1 GalaxyMap,
  2-3 InformativeGalaxy, 4-5 Constellation, 6-9 Systems, 10-11 System, 12 SystemOverview
  (13 steps, default 9).
- **The scan system BAND never draws planets**: `StarSystemManagementScanViewWindow` binds only
  while `FocusedStarSystemNode != null` — the planets belong to the management lens one rung in.
  `StarSystemManagementScanViewPopulationSynergyItem` carries NO AgeTooltip anywhere (the icon
  table names its textures); `PlanetStatusGroup` carries none either.
  `%BonusPopulationDefenseTitle` is absent from localization (the ExtendedGuiElement's AltTitle
  exists).
- **A scan BAND writes no words of its own.** `ScanNodeLabel` has no text: the planet dots and the
  trade dial carry everything, and all of it on CLASS-backed tooltips, so an `AgeWidgets.DrawnLines`
  reading of a band returns the system's name and nothing else — the content has to be read control by
  control. `PlanetCircleItem.Content` is the RAW internal name (the spoken name comes from the
  `GuiWrapper`), and `TradeCompanyGroup` is a SIBLING of `ContentTable`, not a child, so a walk of the
  table misses it.
- **The scan band's gate is painted-ness, and only painted-ness.** `MainMetaModifier.TargetAlphas`
  fades a whole band per camera layer, and the `metaModifiers` a label collects in `Awake` never
  animate the POOLED circles (which are created later), so neither the modifier list nor `Visible`
  answers what is drawn — `AgeWidgets.Painted(ContentTable)` is the band gate and the circles' own
  drawn alpha is the per-dot one.
- **The governor's panel on the system-management lens has no words for the two things it is
  ABOUT.** `StarSystemManagementScanViewHeroPanel` is shown only where the system has an
  `AssignedHero` (`StarSystemManagementScanViewWindow.Bind`), and measured on the drawn panel it
  carries NO `AgeTooltip` anywhere — not on `EfficiencyGroup`, `HeroEfficiencyIcon`, the portrait or
  the root. The hero's NAME is drawn nowhere on it (the portrait is the identity) and lives in the
  panel's private `guiHero`; the dial is geometry alone — `RefreshEfficiency` counts the governor's
  colonized-system skills whose modifier paths are currently valid, divides, and writes the ratio to
  `EfficiencySector.MaxAngle` as an angle, so `MaxAngle / 3.6` IS the percentage and re-deriving the
  skill math would be a second copy of the game's counting rules. The two captions it does draw
  (`%SystemManagementScanViewHeroEffectivenessTitle`, `…HeroOutputTitle`) are plain prefab labels,
  not fields of the class, so they are read as drawn lines rather than by name; the output half
  hides `OutputContentGroup` and shows a `%None` label when the governor adds nothing, and the
  hidden group keeps the prefab's placeholder "999 [prestige]" text, so the reading must be
  `PaintedLines`.
- **The planet lens draws a THIRD table nothing else mentions.** `PlanetScanViewWindow` has
  `PlanetRemainsItemsTable` under the right-hand column (rect 1050,260,220,480), filled from
  `Planet.Remains` and drawn per item only where `!remains.Definition.VisibleInSystemOverview`
  (`PlanetRemainsItem.Refresh`) — each a title plus a paragraph. `unlocked` has no remains on any
  planet of Xiu, so the table is drawn EMPTY there and a stats-only reading of the lens looks
  complete.
- `ScanViewDiplomacyLabel` draws exactly ONE line: on your own home system `SwapToggle.Enable` is
  false, so the second variant never appears.
- The rename box's Cancel/Confirm captions are `%MessageBoxCancelTitle`/`%MessageBoxValidateTitle`;
  Cancel runs `GuiModalWindow.OnCancelCb` — the same hide Escape reaches. `PopulationCount.Tooltip`
  (population rows) sits on the entry's SYMBOL child (class `PopulationStarSystem`); the row
  transform carries none.
- **While a TARGETING CURSOR is current, the left click means confirm and nothing else.** There
  are NINE such classes — eight declare `HasUserInstructions` (`ProbeLaunching`,
  `CoordinationRequest`, `TimeBubble`, `ObliteratorFire`, `TakeSystem`, `HackingProgram`,
  `HackingOperation`, `EntityActionCursor`) and `EntityActionCursor`'s two subclasses
  (`PirateMarkCursor`, `HonorActionCursor`) inherit it. All override `OnCursorClick` without
  calling base and return false from `ValidateSelection`, so select and zoom never run under a
  targeting mode — which is what makes Enter-as-confirm the parity answer rather than a
  competing binding. Two aim at the POINTER rather than at a cursor target (`ProbeLaunchingCursor`,
  `CoordinationRequestCursor`), so a confirm for those goes through the order they post. **The
  right button is answered inside each cursor's own `OnCursorClick` and NONE of the nine right
  branches reads a cursor target**: a cancel for seven, one waypoint back or the prompt closed for
  the hacking pair — which is why the mod's Backslash-while-armed needs no node. **Escape is not
  uniform**: six cancel via `GuiManager.cs:2101-2120`, the hacking pair via
  `ScanOverlayWindow.HandleInput:145-181`, and `TakeSystemCursor` has NO Escape route at all —
  its own banner says "Right Click to cancel" and with it up, Exit reaches `GameMenuModalWindow`
  (the mod claims Escape only there and runs that cancel, owner-ruled).
  `HasUserInstructions == true` is exactly that nine-mode set, so it is the banner predicate. The
  instruction window can briefly show the PREVIOUS mode's caption on entry (stale until the next
  refresh).

## Endings, notifications and the journal

- **The elimination popup's groups hold no text**, and it hides Dismiss and Minimize — so its
  sentence has to ride something else (the mod puts it on the screen name).
- **`EndGameSummary` is written at popup-SHOW time**, which is what makes the journal's ending
  entries readable at all. **Its CONSTRUCTOR saves itself**: `new EndGameSummary(game)` writes
  the `.bin` and adds the journal row unless `Game.EndGameSummaryAlreadySaved` or
  `EnableModdingTools` (`EndGameSummary.cs:145-151`). Constructing one and then calling
  `SaveEndGameSummary` registers the SAME instance twice — two rows sharing one row object
  throw `Duplicate control id` and empty the journal.
- **The journal's Details column has no text of its own**: its two buttons carry their only
  words on their own tooltips (`%VictoryScreenScoreScreenButtonDescription`,
  `%JournalModalWindowDeleteEntryDescription`), one line each.
- **A tutorial page's popup layer decides whether a MINIMIZED popup's bar survives a covering
  window**: `UnderScreens`/`FleetsScreen` hide the whole panel; `AboveScreens`/
  `AboveNotifications`/`AboveModalWindows` keep it drawn and clickable (minimizing only crops
  the panel). Counted in `Public\Tutorials\*.xml`: 117/10 vs 41/16/49. Closing a modal a
  tutorial was drawn over makes the popup re-announce its page — the panel is briefly
  un-minimized during the hide.
- **The report family's breakdown toggle is VESTIGIAL**: `ReportPanel` carries no `AgeModifier` at
  all (verified on four report windows), so the toggle animates nothing and the tables it claims to
  collapse stay drawn either way — the caption-less icon the shared caption rule drops costs the
  player nothing.
- **A hacking outcome's countdown is REAL-TIME seconds** — 10/20/30/45 by outcome, not turns — and it
  auto-picks a default when it runs out, so the choice popup is one of the few surfaces where reading
  slowly changes the result. `PickHackingOperation` only raises its prompt where the node offers MORE
  than one operation (data-gated), so a single-operation node never shows the picker at all.
- `AgeModifierTypewriter`'s labels are complete from frame one (see the typewriter fact above);
  AGE also localizes label text itself, so assigning a raw `%key` still DRAWS localized
  (`AgePrimitiveLabel.cs:702-717`) — which means a drawn label is no evidence that the mod's own
  lookup would have resolved.

## Multiplayer, session and the install

- **The game has NO store code besides Steam** — no GOG/Galaxy/Epic assemblies or branches
  anywhere; the single branch is "did `SteamAPI_Init()` succeed", and the failure path is
  hardcoded on (`enableOfflineModeWhenSteamClientIsDown = true`): services register with
  `IsSteamRunning == false`, all DLC unowned, language forced to English, Join Game refused
  with `SteamNotRunning`. Launching `EndlessSpace2.exe` with Steam closed reproduces the
  whole store-less profile — a free test fixture. The mod calls no Steam API anywhere.
- **DLC ownership has exactly one source**: `DownloadableContent.IsSubscribed` →
  `SteamApps.BIsSubscribedApp` (no subclass overrides it). The 367 `*_DLC*` data files
  ship with the base install; only flags gate them. To unhide at runtime, add
  `Subscribed|Installed|Activated` via `AddAccessibility` — NOT `Shared`, which
  `RuntimeState_Lobby:400-430` wipes and re-derives at session creation.
- **What the gates actually read is `IsShared`, and only a NEW game derives it.** The re-derive
  branch runs for a freshly CREATED session (`RuntimeState_Lobby.cs:440-450`); a LOADED game takes
  the save's own `sbs` bitfield instead, and `rdcol` — the flag that would send a load down the
  derive branch — is never written anywhere in the game. So a `Shared` flag set by hand in a loaded
  session STAYS set, which is what makes an unowned expansion's UI sightable at all. Two consumers
  cache the answer at load time (`HackingManager`, `ScanOverlayWindow`), so their branches stay dark
  until the save is loaded again.
- **The `*_DLC*` datatables load whether or not the expansion is owned** — measured: the Juggernaut
  specialization modal reports `GuiSpecializations.Count == 3` on an install owning no expansion. So
  a DLC screen's DATA is measurable here even when its entry point is not.
- **"Steam cloud" saves are not a Steam API**: the toggle writes a registry key that
  redirects the save directory to `<saves>\Cloud` — identical on any store.

- **The out-game pages, measured** (DLC browser, mod manager, disclaimer, credits):
  `DownloadableContentType` 1 is `Personal`, which the game words as "Add-on" — the browser's own
  type column, not an ownership state. `AvailableModItem` leaves a DOWNLOADING row's toggles
  enabled and its handler only logs (a game bug: the click does nothing), so the mod speaks that as
  the refusal it meant. `ModdingSelectedModPanel` swaps its two branches by ALPHA alone — both stay
  `Visible`, so a reader gates on `SelectedGuiMod` instead. `DisclaimerModalWindow.HandleInput`
  returns true for EVERY action and acts on none, so Escape cannot dismiss it and only its own two
  buttons can (Decline quits the game). The credit roll is 598 items and exits itself after
  ≈8.5 minutes.

- **A lobby has chat history the moment it becomes multiplayer**: switching Session Mode posts
  `%LobbyChatRenamed` through the chat service, so the log is never empty in a session that was
  ever MP.
- **Game chat text carries `#RRGGBB#` colour markup** — cleaned like any other game text before
  speaking.
- `EnableFactionIntroductionVideos` is FALSE in this install, so the faction intro cutscenes
  cannot be sighted here at all.
- **`StartChatting`'s default is `Return,Tab`** (`InputManager.cs:269`) — both of the mod's own
  primary keys. Bindings persist to `Settings/Input/…` in the user `Registry.xml`, flushed only on
  a clean quit and re-applied at boot AFTER the input service is published, so a write made during
  start-up can be overwritten: wait for the binding table before writing one. `Option.GetValue` is
  uncached for key mappings, so the options row follows a programmatic change with no refresh of
  its own. The game has exactly **two `ChatPanel`s** — the in-game one over an
  `AgeControlTextFieldChat` (`StandardCancel=false`) and the lobby's over a plain
  `AgeControlTextField` (`StandardCancel=true`) — plus one other chat-TYPE field, the
  coordination-request pin box, which is why a chat reader scopes on `ChatPanel` identity rather
  than on the control's type. `InGameChatWindow` is shown and answers `StartChatting` in single
  player too, so the key is live in every session.
- **A single-player chat line goes out and comes back**: a message sent from the in-game panel is
  stamped `ChatMessageOption.Default` (= `RecipientGlobal|TypeUser|ScopeNetwork`, by
  `ChatDefaultOptionHook`), leaves through `Session.SendLobbyChatMessage` and returns through
  `Session_LobbyChatMessage` → `ReceiveMessage`, arriving on `OnChatMessageReceived` with the
  player's own name filled in from the lobby slot (measured in a Single session: "Neurrone: probe
  alpha"). So single-player chat is a real, readable log rather than a dead box — which is what the
  mod's chat surfaces are gated on (`SessionChat.HasChat`), MP-only having been the earlier policy.
  `ChatManager` keeps the messages until the session is released or `RemoveMessages` is called;
  the PANEL keeps only fifty (`ChatPanel.MaxHistory`, enforced in `AddLine`) and pools those
  widgets, which is the bound the walkable log copies.
- **`InGameChatWindow`'s visibility is `GameReady` and nothing else**:
  `GuiManager.UpdateGameWindowsVisibility` passes a bare `true` for it (:1579-1580) and
  `SetGameWindowVisibility` ANDs in `GameReady`, so "the window is shown" already means "a game is
  running" — no session-mode test needed to keep chat off the menu.
- **The in-game panel is discreet exactly when the player is not typing**, and `SetDiscreet`
  (:127-180) takes the field with it: `ChatTextField.AgeTransform.Enable` and `Label.Visible` both
  go false. So the field's own enabled flag reports which state the panel is in, never whether chat
  can be opened. `SetFocus` (:116-125) is the single way in — the chat key, a tab click
  (`ChatTab.OnTabCb`) and the new-message button (`OnNotificationCb`) all call it, and it focuses
  the field and un-discreets in one call, with `OnValidateObject` nulled for a frame so the keypress
  that opened the box cannot also validate it.

## Card and tooltip drawing mechanisms

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
- **A panel feature can be a whole SECTION rather than a row.** `PanelFeatureModuleEffects` and
  `PanelFeatureHullInfo` build N instances of ONE prefab, and each instance is a complete section —
  heading and all — not a line of a list. A reader that treats the instances as repeated rows
  flattens several sections into one.
- **The galaxy camera's 13 zoom steps, and only the LAST reaches orbital.**
  `CanFocusGalaxyEntity()` is `zoomStep == ZoomStepsCount - 1`; until then
  `FocusedStarSystemNode` stays null and `PlanetLabelsWindow_SystemOrbital` never shows, and
  the camera must also be within `DistanceMinToCatchFocusOnNode` of the node. Step 3 draws a
  system's name only, step 9 its whole label. `SetZoomStep()` alone swaps the drawn layer
  WITHOUT moving the camera. At step 12 the focused system's own label is pushed off the top
  of the screen (y ≈ -230). (Operational use: the camera recipe in `test-recipes.md`.)
- **Stepping between planets re-enters the SAME view level with a NULL blink.**
  `Gui.GuiGameWindowService.CurrentGalaxyViewLevel` goes null for a few frames and the window
  unbinds its planet on every Previous/Next step; `GalaxyViewLevels.LevelThroughTransitions`
  is the view's own non-blinking answer. (Why the planet screen gates on it: its doc comment.)
- **A hint-blocked button stays `Visible` AND `Enable`.** The game turns its click into
  "jump to the missing technology" instead of disabling it, so `Gui.IsHintActive(transform)`
  is the ONLY discriminator between an offerable button and a blocked one — never gate on
  `Enable`. (The hint tooltip's three-part structure: the card/tooltip mechanisms above.)
  `Gui.FormatButtonHint` FORCES `Enable = true` as it writes the hint, and only 6 of the 16
  prefabs using the mechanism happen to be honest about their own flag — so the question is
  asked per site, never inherited from a prefab that looked right.
- **The hint exists so a CLICK can explain itself, and that click is the only thing such a control
  does.** `Gui.FormatButtonHint` switches the control on; the click then asks `Gui.IsHintActive` and
  runs `Gui.ActivateHint`, which reads the Ctrl the player is PHYSICALLY holding and jumps the
  technology screen to the missing technology. `AgeWidgets.Offered` answers false for exactly that
  trick, so the mod's own Ctrl-chord fall back to the plain click would replay a no-op — the jump is
  therefore WIRED, once, in `Cells.Add` (helpers.md). A hint hanging off a CHILD widget rather than
  the declared one (the troop rows' locked type) is named by its own screen. The count on this
  install: **101 `GuiButtonHint` instances, 8 of them hint-active** (2026-08-13). One of the eight is
  the marketplace tab, which is hint-active while its own `Enable` is false — reachable from the
  keyboard through the hint even though the mouse cannot click it.
- **A refusal's own wording is keyed by its flag**: `%Failure<flag>Description` — so the sentence a
  blocked control shows can be looked up from the flag alone, with no tooltip drawn.

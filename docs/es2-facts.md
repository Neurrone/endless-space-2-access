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
- **Four notification windows wire the shared description to a label they left OUT of their
  layout, and two of those still carry the skeleton's key on it.** Measured over every
  `NotificationWindow` instance in the scene (`Resources.FindObjectsOfTypeAll`): 65 of 69 hold
  `NotificationDescription` somewhere under the window's own `AgeTransform`; `DeedCompleted`,
  `QuestCompleted`, `MetaplotBegun` and `MetaplotFinished` do not — `AgeTransform.Parent == null`,
  the GameObject parented to a `Dummy` outside the window, the rect a degenerate `(2,15,45,20)`
  in the corner of the screen. `Refresh`'s visibility gate never fires on them, so
  `DeedCompletedNotificationWindow`'s label still holds the prefab's
  `%NotificationDeedCompletedDescription`, which localizes to **"You have achieved this legendary
  Deed!"** — spoken by the mod on the popup announcing that another empire got there FIRST, because
  the label passes every other test: marked visible, no hidden ancestor (it has no ancestor), and
  a whole sentence rather than a template. `GetDescription()` answers the same key, so the
  notification-side fallback repeats the lie. What the popup actually draws is its
  `QuestDescriptionGroup` (StatusTitle / ObjectiveTitle / Outcome / ObjectiveLore) and nothing
  else — measured against `/gui/age`, there is no success-or-failure sentence anywhere outside it.
  **Mod policy** (`NotificationScreen.DescriptionLabel`/`Held`): a description label the window's
  own tree does not hold is no words label at all, and the question stops there — no fallback to
  `GetDescription()`, because a popup with nowhere to draw a description never showed that
  sentence under any circumstances.
- **41 of the 69 notification prefabs bind a Show Location button their own layout never holds,
  and the game marks it VISIBLE anyway.** `ShowLocationButton` is `[GuiBound]`, so every window
  resolves one; `NotificationWindow.OnBeginShow` (:139-140) then sets its `Visible`/`Enable` from
  `GuiNotification.HasLocation` without asking whether the prefab laid it out. Measured over all
  69 instances: 28 hold it in their bottom button bar (rect ~`(643,575,120,44)`), the other 41
  answer with an orphan — `AgeTransform.Parent == null`, `IsRootTransform` false, rect
  `(0,0,50,50)` — which the engine never draws because rendering walks the tree. Same shape as the
  detached description label above, on a CONTROL: visible, alpha 1, enabled, drawn nowhere.
  **Mod policy** (`NotificationScreen.Painted`): a paint test that walks up the chain must end AT
  the popup's root, not merely run out of parents — an orphan passes every step of the walk and
  then has no root, and the reward for the lenient version was a "Show Location" stop on
  Outpost→Colony and Planet Destroyed that did nothing. `Controls` filters the whole list,
  rails included, through that test.
- **Every notification popup declares its two strips as CONTAINERS, and the popup's own buttons
  join the bottom one.** Measured over all 69 `NotificationWindow` instances in the scene: the
  browsing arrows sit in a `NavigationGroup` and the pop-up-again box in an `AutoPopupGroup`, both
  of them children of `TitleGroup`; `Dismiss`/`Minimize`/`ShowLocation` sit in a `ButtonsGroup`
  (66 of 69 — the other three park a spare `DismissButton` in a `Dummy` beside the description, and
  their `Minimize` is still in `ButtonsGroup`). Of the 98 wired controls the popups add on top of
  that skeleton, **50 are inside `ButtonsGroup`** (`AcceptButton`, `ValidateButton`, `ReplayButton`,
  `AcademyButton`, the quest popup's `PinToggle`, …) and **none is anywhere inside `TitleGroup`**;
  the other 48 sit in content containers of their own (`PoliticalSupportLinesTable`,
  `CompletedTechnologyGroup`, `ChoicesTable`, `ContentGroup`, …). **Mod policy**
  (`NotificationScreen.Sort`): a control is classified by identity plus containment — a base-class
  rail goes to its strip by name whatever the popup did with it, a control the game drew inside
  `TitleGroup` or the button bar goes to that strip, and everything else is body content walked in
  the row it is drawn in. Rectangles cannot answer it: banding against the description swept an
  election survey's four party lines into the top strip (the description is drawn BELOW the chart
  there), and banding against the rails themselves is the same guess with a different ruler.
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
- **Icon-table coverage proof** (moved here from `dev-loop.md`, which is the loop only): run every
  `<LocalizationPair>` value in `<game>\Public\Localization\english\*.xml` through
  `ES2Access.UI.AgeText.Clean`, then `DevProbe.UnknownIcons()` — `tokens` must be empty, and the
  expected token counts are this file's icon numbers.
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
- **The label's management button is stricter than the view level behind it.** `StarSystemLabel`
  :1626-1648 assigns `MainColonizedStarSystem` only while the state is `Colony`, and :1750 enables
  `RequestManagementViewButton` from it — so the button is drawn dead on an OUTPOST of ours, while
  `RequestStarSystemManagementViewLevel` (:1224-1247) opens the page for any system of ours that is
  not `Lost`. A mod reading "can the player do this" off the button's `Enable` under-offers by
  exactly the outpost case. Measured turn 18: Heka `enable=False`, repository `State == Outpost`,
  `IsBlackedOut == false`, and `GalaxyViewLevels.OpenSystem` opens the page — whose outpost half
  (`OutpostInfoSidePanel`, the outpost-action checkboxes) the system screen already reads.
- **The map's star/fleet/mote labels are pooled and re-bound as the camera slides** (2026-08-17):
  a tooltip widget captured for a place goes stale within the 0.3 s camera glide, so anything
  aiming the pointer at a map thing must resolve the widget from the ENTITY per frame —
  `GalaxyHudScreen.MapMark` is the one lookup, shared by the inspect cell's aim (see the code
  comment at its `AddSystem` site for the mechanism).
- **An undiscovered system's label carries `Enable=True` from the prefab.** A sweep of all 87
  `StarSystemLabel`s found ~80 with `RequestManagementViewButton.Enable == True` and the visibility
  chain false. Any offer gated on `Enable` alone would exist for the whole galaxy; the drawn-chain
  test has to come first.
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
- **Every targeting-mode cancel at a multi-fleet system hands the panel to the slot's FIRST
  fleet** (measured 2026-08-20, keyboard Escape and mouse right-click byte-identical):
  `ProbeLaunchingCursor.SwitchToGalaxyCursor` (:55-70) selects the DOCKING SLOT, not the origin
  fleet; the arming hid the panel and `FleetsScreen.OnBeginHide` (:925-943) ran
  `UnselectAllGarrisons`, so `FleetsScreen.RefreshGarrisonSelection` (:1116-1129) defaults
  positionally — `Garrisons[0]`, or `[1]` past a Hangar. The actor's spent state is irrelevant
  (control run: cancel with nothing launched swaps identically). This positional default owns
  every "panel opened for the wrong fleet" symptom; Enter on a fleet's own row is correct in
  every measured state. Known issue + fix options: `docs/fleet-selection-cancel-swap.md`.
- **`FleetsScreen.OnBeginHide` removes garrisons with a forward loop and leaves a residue list**
  (measured: `Garrisons=[1296]` still standing after the panel closed).
- **`DockLabel.OnClick` accumulates duplicate subscribers** — `DockLabelsWindow.OnDockLabelClicked`
  re-subscribes on every pooled `ShowLabel` (4 measured), so one dock-label click advances the
  garrison cycle N times; at even cycle parity the mouse's click-cycling never changes the
  selection at all.
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
  supplies one (radio, checkbox, slider) answers the alternate-click chord with the word for the
  PRIMARY action: the quest card's alternate click pinned a quest and said "selected". A node with two
  activations has to own its `StateText` and answer for whichever one just ran — which is a wrapper the
  journal's card carried until the pin became a node of its own. **The real lesson is the one above
  it**: the card had no modified click in the game at all, so the second activation was invented, and
  the honest fix was to declare the control the card DRAWS (`QuestCard.PinToggle`) as a child node and
  leave the chord unwired. (The chord itself was Alt+Enter until 2026-08-19 and is now
  Ctrl+Shift+Enter — `docs/interaction.md`.) The game names that action, though never on the toggle: `%NotificationQuestBegunPinTitle`
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
- **A leg is NOT always a lane.** Free movement (the Ctrl+right-click course, and whatever a scripted
  start hands out) sets a start and a goal with **no `Link` between them** —
  `Galaxy.GetLink(start, goal)` answers null — so the fleet is in neither `FleetPresence.FleetsAt`
  (it is not docked) nor `FleetsOn` (there is no lane to be on). It IS drawn, so
  `FleetPresence.Drawing()` (the inspect cursor's and the scanner's source) still has it. Measured
  on `[Beginner] test`: two of its six fleets (`1st Conquerors Navy`, `1st Vanquishers Navy`) are on
  legs between Dusay and Heka with no link, and both are AUTOMATED delivery fleets
  (`Fleet.IsAutomated`, the `AutomatedFleet` tag — measured 2026-08-16), which is why they fly with
  starlanes ignored. **Mod policy** (2026-08-16, reversing the both-ends policy shipped earlier the
  same day): the tree hangs such a fleet under its DESTINATION alone, and under no system at all
  where the destination is unperceived — there it gets a top-level row walked into the system list by
  its own rounded pair (`GalaxyHudScreen.AddAdrift`). The rationale is parity, not tidiness: **the
  map draws where a fleet is GOING and never where it came from.** A selected fleet's committed path
  is drawn ahead of it as dots and numbered turn markers; `Fleet.Path` starts at the node being flown
  towards, so the source is not in it, and the only place the game writes a destination as text is
  owner-gated (`PanelFeatureGarrisonInfoAutomatedFleet` :77-85). A lane fleet keeps its two rows,
  because a lane is drawn geometry with both ends on the screen. `GalaxyHudScreen.CrossingOpenSpace`
  is the one test (`Linked(start, goal)` over the start node's own `Links`), which is also what keeps
  this list and `EnRouteOn` from both claiming one fleet — two claims under one system is a duplicate
  control id and throws the page out of `Build`.
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
- **The map shows a fleet's SHIP COUNT only from the Visible tier up.**
  `GarrisonsLabelButton.RefreshShipCount` :203-217 adds a fleet's ships into the lozenge number only
  while `fleet.IsAutomated` — an automated delivery fleet's strength is public — or
  `(int)fleet.Visibility[Gui.PlayerEmpire] >= 3`; below that the lozenge is drawn with the fleet
  missing from its own total, and no placeholder is shown. Same test in
  `RefreshMultiGarrisonsChevrons` :219-232. **Mod policy**: `FleetPresence.ShowsShipCount` is that
  predicate verbatim and `GalaxyHudScreen.FleetText` omits the "N ships" part outright when it is
  false. An empire's own fleets are always at full visibility, so nothing changes for them.
- **A leg WITH a link the map does not draw is still not a free mover, and is still tree-absent.**
  `EnRouteOn` walks `LanesOf`, which drops a lane below the drawn intensity and a wormhole an empire
  cannot see, while `FreeMovingAt` skips any leg its two ends have a `Link` for. So a fleet flying an
  undrawn lane falls between them — deliberately: its road is not on the screen, and the scanner and
  the inspect cursor still reach it. No fixture has produced one.
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
  (`FleetsScreenGuiElement.FleetActionButtons` — all 32 rows, one of them a STAR-SYSTEM
  action driven from the fleet panel: `GroundBattleStarSystemActionDefinition`), the XML on disk is the
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
- **The game writes NO word when a construction is queued** — the click answers with a sound and a
  flying icon and nothing else — and every construction-queue string in the corpus is a REFUSAL
  (`%FailureConstructionAlreadyQueuedDescription`, "You already queued this construction"). The one
  "Queued" the corpus has, `%TechnologyStatusQueuedTitle`, is a TECHNOLOGY's own state word, which
  the research dot changes to under the cursor and a constructible tile has no equivalent of.
  **Mod policy**: the queue phrases are mod-authored (`queue.queued`, `queue.queued-first`,
  `queue.cancelled`), shared by both queues — a deliberate deviation from game-sourced words, made
  because four of the seven queue gestures answered the key with no word at all.
- **The construction queue line draws an ABBREVIATED title.** `ConstructionLine.RefreshTitle` writes
  `GuiConstructible.GetFullTitle(Title, Title.WordWrap)`, so "Interplanetary Transport Network" is
  drawn — and therefore read — as "Interplanetary Transport N." on the queue line while the
  constructible tile beside it draws the full name. Any mod sentence naming a queue line inherits the
  abbreviation, which is the drawn word and so the right one.
- **God mode re-purposes BOTH queue-removal buttons.** `ConstructionLine.OnCancelCb` (:378-392) buys
  the construction out instead of cancelling it, and `TechnologyItem2.OnToggleCb` (:734-745) unlocks
  the technology outright instead of queueing or dequeueing. Any announcement attached to those two
  controls has to ask `GodGalaxyCursor.IsGuiInGodMode()` before saying what the press did.
- **Research has no cancel confirmation and construction has one — the two queues are the same shape
  everywhere else and differ exactly there.** `TechnologyScreen.DequeueTechnology` (:189-202) posts
  `OrderCancelResearch` unconditionally, while `StarSystemQueuePanel.OnCancelConstruction` (:425-442)
  branches on `Construction.IsAlreadyInvested` and raises the game's own message box first (the bullet
  above). So an outcome line for a cancel is honest on the research queue and on an uninvested
  construction, and must be withheld where the game has still to ask its own question.
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
- **The map names a special node one exploration step later than a star system.**
  `GalaxySpecialNodeCursorTarget.VisibleByCurrentEmpire` (:22-27) needs exploration ≥ 3 where
  `GalaxyStarSystemCursorTarget` (:89-94) takes ≥ 2 — and `SpecialNode : StarSystemNode`, so any
  `is StarSystemNode` enumeration (including `Galaxy.StarSystemNodes`) already contains the Academy
  and quest nodes; what needs doing is the threshold, not the enumeration. Mod policy:
  `GalaxyHudScreen.Perceived` gates a `SpecialNode` at 3 (measured evidence pair: a special and an
  ordinary node forced to the same exploration 2 answer False and True).
- **Ctrl held during the move gesture asks for `PathfindingFlags.FreeMovementOnly` — and without
  the technology that is the game's own no-op.** `GalaxyGarrisonCursor.GetGalaxyPathToPosition`
  (:453) reads the physical modifier; but `PathfindingManager.GetTransitionCost` (:219) re-admits
  warp/wormhole transitions while the fleet's `FreeMovementSpeed <= epsilon`, so the Ctrl route
  equals the plain route until the tech exists (measured: Primus→Rigel, 3 steps either way).
  The mod's Ctrl+Backslash reaches the same decision through `FleetOrders.RequestedFlags`.
- **A refused move gesture is NOT silent in the game — the reasons go to the failure banner.**
  The cursor collects `FailureInfo`s through the three-argument `CanBeExecuted` (:245) and a ladder
  of relaxed `FindPath` re-runs (:456-506), shown via `IGuiService.SetFailureInfos`. Mod policy
  (2026-08-14, reversing the earlier documented silence, which rested on the wrong premise that the
  game answered with nothing): the send key pressed on a NAMED destination speaks each distinct
  reason once (`GalaxyHudScreen.SayRefusals`); the fleet's own orbit stays silent, because nothing
  was refused there.
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
  exactly there.** Measured: a route to Rigel passes Dusay during turn 3 and ends that turn
  mid-lane. "Where does each turn end" and "which nodes does the fleet reach" are different
  questions with different answers.
- **Neither interception nor route cancellation raises an event, and neither is worded anywhere.**
  `EventFleetGotInterceptedByAnEnemy` is raised only by `GuardEmpireLocalAction` (:605); the common
  citadel interception (`Citadel.cs` :195-222) raises nothing, and no cancelled-route event exists.
  Both endings share `Fleet.OnGoToEnd` → `SetPath(null)`, and the game's only signal is a
  `GuiFleetStatus` icon on the lozenge — so polling `Fleet.Path` is the faithful watch
  (`FleetRouteWatch`), and arrival is told from loss by whether the fleet stands at the remembered
  destination (the same test `GoToFleetAction` :307 makes).
- **The map's NAME gate is `StarSystemLabel`'s, and it is looser than the mouse's.** The label
  shows at exploration ≥ 2 AND (visibility Known or ≥ 3) (`ShowOrHideIfVisibleByEmpire`
  :1514-1522), draws `GameNode.LocalizedName` at ≥ 2 and the literal `"???"` below it
  (`RefreshEmpireNameLabel` :1894-1921), and the window binds a label to EVERY `GameNode` —
  special nodes included, with no special-node branch on the name. So
  `GalaxySpecialNodeCursorTarget`'s stricter ≥ 3 governs TARGETING, not naming: between
  exploration 2 and 3 the map names a special node the mouse cannot yet click. Measured live:
  every Unrevealed node's label hidden, so a lane preview into the dark never shows the far
  end's name anywhere — the mod's "an unexplored system" placeholder is exact parity.
- **A link's drawn line is built full-length at ignition and only TINTED by exploration.**
  `GalaxyWarplink.Ignite` uses both extremities' real positions unconditionally;
  `GetIntensityFromState` (:362-372) paints Localized/Identified at intensity 0 — invisible —
  and PartiallyRevealed+ at 1. Existence of the geometry was never the question; visibility is
  the intensity. Mod policy: `MapVisibility.Drawn(link)` gates the tree's lanes at
  ≥ PartiallyRevealed.
- **`EntityExploration.SetState` only ever RAISES a state** (:87-100), so a fog test that needs
  a lower state must write the byte in the by-reference `GetCurrentStates()` array and put it
  back (test-recipes).
- **`TradeRouteRenderer` draws in scan view only, own routes only, per-LEG with an undirected
  merge** (three materials: open/blockaded/mixed; the blockade flag ACCUMULATES down a route's
  path, and a route blockaded at either end draws blockaded from its first leg — the picture,
  so the mod copies it). It computes once on entering scan view and never refreshes mid-mode;
  the Economy lens legend captions only two of the three colours. The fixture cannot create a
  trading company (`CreateTradingCompanyPreprocessor` needs the HQ tech AND the improvement
  built — `DepartmentOfCommerce.cs:816-855`).
- **The Academy and quest sites are NOT `SpecialNode`s** — each is an ordinary
  `StarSystemNode` carrying the `WorldAcademy` / `QuestNodeTag` tag (the label has its own
  `AcademyIconGroup`). `SpecialNode` means the eight stellar-phenomenon kinds (Black Hole,
  Asteroid Field ×2 definitions, Collapsing Star, Solar Nebula, Neutron Star, Nebular Clouds,
  Rejuvenation Field): no planets, same zoom/click as a star, and the KIND is named only by
  the dossier's category line (`GuiSpecialNode.TooltipClass = "SpecialNode"`,
  `Gui.GetLocalizedTitle(SpecialNodeDefinition.Name)` — e.g. "Solar Nebula" where an ordinary
  star reads "Star System (White Star)"); the label and `LocalizedName` ("B10 6805") never
  say it.
- **`ContextualIconInvasion.AgeTooltip.Content` is prefab-authored and never cleared.**
  `RefreshInvasionContextualIcon` (:748-749) clears only `Class` and `Target`, so the
  `%StarSystemLabelInvasionDescription` sentence sits in `Content` on every label forever —
  harmless only because readers gate on the icon being drawn. A reader that skips the
  visibility gate reads a phantom invasion.
- **`Gui.GetTitle` has no GuiElement for `InvadedStarSystem`, `CitadelDefense` or
  `GuardedColonizedStarSystem`** — the raw `%…Title` key (or a "(missing GuiElement)" marker)
  comes back, so those states have NO game-authored noun; mod phrases required. And the
  `StarSystem` tooltip class carries `PanelFeatureTimeBubblesContainer` but NO guard or
  citadel feature (`GuiTooltipDescriptions.xml`) — the guard ring is the map's only telling.
- **`Garrison.ShipsIncludingHero` is an `IEnumerable<Ship>` yield iterator, never a list** — a
  cast to `IList` answers null silently and turned every fleet's probe count into zero for two
  releases. Walk game collections through the interface they declare.
- **An order is only POSTED by `PostOrder`; the session executes it later** — a stock read in
  the same call still sees the pre-order value, which is why the game's own probe click tests
  `<= 1` rather than `== 0` to decide the mode is over.
- **The Expedition fleet action arms no mode**: `FleetActionButtonExpedition.OnClick` plays a
  sound and force-zooms via `galaxyView.SelectGameNode` so a mouse can reach the curiosity
  items on the orbital cards; the accessible path is the curiosity button under the zoomed
  system's planets. It is probe-based (`GuiExpeditionFleetAction : GuiProbeBasedFleetAction`)
  and greys out at 0 probes. A first visit to an undiscovered system routes through the
  discovery cinematic, all of it already spoken.
- **A probe launch accepts ANY non-zero direction** — `LaunchProbeFleetActionDefinition.
  CheckContext` (:92-95) refuses only a zero vector (`DirectionIsInvalid`); initiator checks
  are docked-in-orbit + movement cost. Galaxy axes, measured against the camera:
  `GalaxyPosition.X` = east, `.Y` (world z) = north. A launched probe has already moved one
  hop of its `Speed` (6 here) when created — it never sits on the launch star — and probe
  speed vs lane length (16.5-26.6) means a nearest-star anchor MIGRATES mid-flight.
  `VisibleEntityLabel` draws at `WorldToScreenPoint(Entity.GalaxyPosition)` gated on camera
  culling + `Visibility >= 3`, so the drawn position licenses direction-and-distance words.
- **Arming a targeting mode from the fleet-actions stop closes the fleet panel and seats the
  cursor back in the acting fleet's system branch** — the last node if the branch is open,
  the system node if closed. That is reconciliation's doing, not a landing, and it only holds
  when the cursor was in that branch to begin with: from anywhere else the player was left
  standing where they were, with the mode up and no way to it. So the mod no longer relies on
  it — arming seats the cursor on the probe mode's own first bearing itself
  (`GalaxyHudScreen.FollowProbeArming`, 2026-08-19).
- **A `GalaxyLink` game object carries TWO mirrored `GalaxyLinkCursorTarget` halves**
  (start/destination swapped; `GetCursorTarget` picks by where along the line the pointer
  is), and **no targeting cursor consumes a link target** — only the garrison cursor and
  the scan overlay do — so a mode confirmed on a lane refuses silently and writes no hover
  readout. A lane confirm for the pointer-aimed modes therefore aims at the lane's far
  extremity, flipped when the acting fleet stands on it (a zero-length probe heading is the
  game's own refusal).
- **Seven of the nine targeting cursors write a hover readout from `OnCursorEnter`**
  (obliterator: ETA + star-destruction odds + protection warning; take-system, time-bubble,
  the `EntityActionCursor` pair, hacking-program: failure infos). `ProbeLaunching` and
  `CoordinationRequest` declare no enter readout (pointer-aimed), and `HackingOperation`'s
  enter also STORES `hoveredCursorTargets` for its own click, so replaying it would re-aim
  the mouse's next click. `IGuiService.SetFailureInfos` is an EVENT — `GameOverlayTooltipPanel`
  is only a subscriber and can hold stale text with `Visible=false`, so the event, not the
  panel, is the oracle. A VALID target makes four of the modes write nothing at all, and the
  obliterator refuses a non-Behemoth fleet with an EMPTY FailureInfo list.
- **`PanelFeatureProbeFleetActionInfo`'s captions live in the PREFAB** (`%ProbeStockTitle`
  sibling labels), so the "default" reader pairs "Exploration Probes 2/2" correctly — a
  feature class on `default` is only a defect when the DRAWN feature divorces value from
  caption. The game gives Launch Mining Probe the same prefab, so a mining stock is captioned
  "Exploration Probes" — the game's own mislabel, mirrored not corrected.
- **`OrderCreateTimeBubble` does not land from the REPL** (it needs `TimeBubblesStock`, and
  `OrderAddTimeBubbleStock` does not land either); the public route is
  `DepartmentOfTheInterior.CreateTimeBubble(guid, definitionName, node)`.
- **`Fleet.GeneratePathfindingData()` and `GalaxyPath.Data` hand back the fleet's own SHARED
  `PathfindingData` instance.** Mutating it corrupts the fleet's real pathfinding — always copy
  before simulating (`FleetRoute` does).
- **Sending a fleet clears the map's selection**, so re-ordering a moving fleet means re-selecting
  it first — and it is why a route REPLACEMENT never looks like a cancellation to a watcher that
  checks the fleet still exists and where it stands.
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
- **The empire's own origin on that compass is `DepartmentOfTheInterior.HomeSystemNode`**
  (`DepartmentOfTheInterior.cs:655`) — the one place a player already has in their head, which is
  what makes a coordinate pair mean anything. It is a plain settable property with no event, null
  until a home is chosen and replaced wholesale by a new game or a load, so anything caching it
  re-derives on the player empire changing IDENTITY rather than subscribing. Fixture `[Beginner]
  test`: Dusay at raw (68.884, -22.450), and the 13 systems the map names span -43..23 east and
  -42..34 north of it against a whole-galaxy span of -95..92 by -64..66.
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
    whose `TooltipClass` is `"OutpostAction"` (renderer-assembled → buffer-only, Indicate mode).
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
- **A focused text field acts on Return itself, twice over — and the validate is the
  SCREEN's action, not the edit's.** `AgeControlTextField.KeyDown` (firstpass :76-89) fires
  the field's validate callback on `Input.GetKeyDown(Return)`: for `LoadSaveModalWindow`
  that is `OnSaveNameTextFieldValidateCb` (:509-516) → `OnSaveCb()`, which writes the save
  and closes the screen; for `RenameModalWindow` it posts the rename and closes the box.
  Separately, `InputManager.HandleInput` (Assembly-CSharp :1210-1243) swallows `Validate`
  ONLY while a key-exclusive control holds focus — on every frame the field does NOT hold
  it, the window's own Validate handler is live. **Mod policy that follows:** never hand a
  game text field the keyboard while the key that asked for the edit is still down (both
  doors shut by that one rule), and while the mod owns a live edit it takes Return/
  KeypadEnter from the KeyDown dispatch and ends the edit itself, leaving the surface
  standing — the chat box is the single exemption (its Enter sends).

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
- **A page comes back ONE FRAME before its own content does**, and that frame is where a cursor
  goes missing. Measured with a per-frame trace (`DevProbe.Trace`) across a technology-wheel round
  trip from the star system page in `unlocked`: f+0 the wheel's window shows (`IsAnyScreenVisible`)
  and the star system screen stops declaring; f+1..3 NO mod screen is active at all (the handover
  gap, three frames); the wheel runs; on the way back three more empty frames, then **f=N
  `screen.star-system` is active declaring 3 nodes, f=N+1 declaring 78**. The three are the shared
  HUD strip (`Screen.BuildShared`'s collapsed-tutorial bar), because `SystemManagementScreen.Build`
  returns early until its planet cards exist while `IsActive` (view level + no modal + window shown)
  is already true. The tutorial SCREEN never enters the stack at any point in the trip — the
  cursor's landing on "Close tutorial" was the shared strip being the whole render for one frame,
  not layer 98 taking focus. The mod policy that follows: shared contributions are skipped when the
  page declared nothing, so "nothing here yet" stays an empty render.

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
- **A notification popup folds a detail panel away by FADING it, and fades ITSELF in on arrival —
  two alpha animations that a single painted-ness rule cannot both serve.** The six report popups
  (`DamageReportNotificationWindow` and its three subclasses, `DisplacementReport`,
  `PirateMissionReport`, plus `ForceTruceProposed`'s two breakdown groups) collapse their detail
  panel with `ReportPanel.StartAllModifiers(forward: false)`: measured on
  `IonWaveReportNotificationWindow`, `ReportPanel` sits at `Visible true, Alpha 0` with all five of
  its children at alpha 1 and every ancestor at alpha 1, so a `Visible`-only read speaks a whole
  "Damage Report" the screen draws nothing of. The popup's ARRIVAL animates the window's own
  transform 0 → 1 while every child stays at alpha 1 (measured: a `POST /wait` on
  `Shown && Alpha <= 0` fires, one on `Shown && IsReady && !Painted(root)` never does over 154
  frames). So the gate is the ENGINE's own child test —
  `child.Visible && (parent.StrictVisibility || child.Alpha > 0)`, `AgeWidgets.Paints` — applied
  descending from the window root and never to the root itself. `NotificationWindow.IsReady` is
  already past the fade, so the arrival announcement is not at risk either way.
- **`GuiWindow.IsReady` is not "painted".** The screen's settled-popup seam fires two READY frames
  after a popup's words settle, and on those frames a popup can still be drawing not one string:
  a `POST /wait` on `DevProbe.NotificationParity().Contains("\"texts\":0")` fires on a live raise,
  and the parity check a moment later is clean. No settled popup of the sixty-four paints fewer
  than four strings, so "paints nothing" is an early frame rather than a finding, and the auto-check
  defers on it (bounded, with a give-up line).
- **AGE `ReserveChildren` tables retire rows by FADING** (alpha 0, `Visible` still true) — the
  third retirement style, beside the surplus-child alpha 0 of "`Visible` is not 'drawn'" and the
  scan view's pool that parks stale children fully visible outside the table's extents. Every
  per-row read gates on painted-ness. A retired row also keeps its old RECT, which is how the
  planet card's climate table (`PlanetGameplayTypeTable` — the one table on that card whose `Load`
  does NOT set `StrictVisibility`) put the previous planet's biodiversity line on top of the
  curiosity line and banded the two into one drawn row: a faded row is a layout hazard as well as a
  phantom line. A parked item also keeps its old tooltip `Target` wrapper, so a name-by-wrapper
  read resurrects the PREVIOUS binding's name (the galaxy planet card spoke another planet's
  "Dustciduous Trees" deposit); `AgeWidgets.ItemText` now enforces the alpha gate centrally, so
  every table read that names items through it is covered. A pooled table can also be retired
  WHOLESALE with its rows left painted: `PlanetLabel_SystemOrbital.RefreshPlanetCuriosities`
  (:1090-1103) sets `PlanetCuriositiesTable.Visible = remaining.Count > 0` and RETURNS before
  refreshing the children, so a planet whose last curiosity was just expedited keeps a child at
  `Visible true, Alpha 1` inside a hidden table (measured on Ita II the turn its expedition
  landed). The table's own visibility is the first gate and painted-ness the second — together
  they are exactly what the engine's own `AgeTransform.GetVisibleChildrenCount` counts
  (`Visible && (StrictVisibility || Alpha > 0)`, :2549-2561), which is the free oracle for any
  count the mod speaks off such a table.
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
- Senate: **hovering a senator card and hovering a party row are the SAME highlight**
  (`SenatorsPanel.OnMouseEnter` → `SenateScreen.HighlightPolitics` :157-161, which lights both the
  assembly's score/pie slice and the party's senators). The association is the party NAME, and both
  surfaces are named by it, so nothing is lost when the highlight is not drawn.
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
  element — go through `Gui.GetGuiElement`). `ResourcesPanel.RefreshResourceItem` soft-hides an
  item by MULTIPLYING its bound alpha by 0.3 (the two fades below), so alpha is the only drawn
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
- Economy, luxuries: **the grid is a 24-slot 3×8 lattice with TWO fades, and they mean different
  things** (measured 2026-08-19 on all 24 slots, correcting an earlier draft of this line).
  `ResourceItem.Bind` sets alpha 1 for a luxury that `Exists` for this empire and 0 for one that does
  not; `ResourcesPanel.RefreshResourceItem` (:203-215) then multiplies by 0.3 when both stock and net
  are zero (`SoftHide`). So **alpha 0.3 = drawn, and the empire holds none of it** — true of an
  unlocated resource AND of a located-but-empty one alike (Eden Incense and Giga Lattice are both
  known and both at 0.3) — while **alpha 0 = a pool row the game is not drawing at all**, still
  carrying the previous bind's icon and tooltip class (nine of them read
  `Strategic01Small`/`StrategicResourceBanner`). A lattice line the game faded out entirely is not a
  row of eight empty cells, it is a line nobody can see: the mod reads one row per DRAWN line and
  says `nav.cell-empty` ("empty") in the faded cells of a line it does read.
- Economy and the development window: **one property gates BOTH strategic grids** —
  `SimulationProperties.Empire.CanUseStrategicForRecipe` (Material Expertise): `EconomyPanel.Refresh`
  sets `StrategicsGroup.Visible` from it every refresh, and the development window's strategic
  component grid derives from the same predicate. Measured 2026-08-19: unlike the descriptor-driven
  properties that shrug writes off, this one DOES stick under `SetPropertyBaseValue` +
  `Refresh(false)` — set it and the game draws the real grid itself, set it back and it is gone
  (the sighting route is in test-recipes).
- Economy, luxuries: **a luxury the empire has not located is drawn ANONYMOUSLY, on purpose.**
  `GuiResource` (:108-133) substitutes a placeholder in every drawn thing about it: `GetName` →
  `UnknownLuxury`, `GetImage` → the single shared `UnknownLuxurySmall` texture, `GetColor` → the
  `UnknownLuxury` colour; `ResourceItem.SetTooltipProperties` clears the tooltip's class and target
  and writes `Gui.GetDescription(GuiResource.UnknownLuxuryName)` as its whole content. The model
  still knows the slot is `Luxury1`, so a mod reading the model would name a resource the screen
  refuses to show. **Mod policy**: the economy grid speaks that sentence and never the name.
- Empire, the side panels: `EmpireDescriptionSidePanel` hangs its tooltips on the LABELS inside each
  group and on each icon — never on the group itself, so a group-level read finds nothing.
  `EmpireStatusSidePanel.PanelTitle` is the ONLY side-panel heading in the family that carries a
  tooltip of its own.
- Research: **254 of 385 technologies carry an affinity badge** — a majority, so the badge is
  ordinary content rather than an exception.
- Research: **the wheel draws NO turn count on any technology.** `TechnologyItem2` declares
  `TurnsGroup`/`TurnsLabel` and `RefreshTurns` fills them from
  `DepartmentOfScience.GetTechnologyRemainingTurn`, but the prefab wires neither — measured null on
  every one of the 385 items in `unlocked`, drawn and undrawn alike. The only surfaces that ever
  show a technology's remaining turns are `ResearchQueueItem` in the research status side panel
  (`TurnsGroup` visible, alpha 1, text `"6[turnColored]"` for the in-progress technology) and
  `EmpireBanner`'s research line (:417). `GetTechnologyRemainingTurn` answers for ANY technology, so
  a readout that simply asks it invents a number the screen never shows — the mod policy that
  follows: turns are spoken on the queue item, nowhere on the wheel
  (`ResearchText.Progress`). What the dot itself draws is the queue POSITION
  (`PositionInQueueGroup`, visible only for `Queued`/`InProgress` — measured `True`/`"1"` on the
  in-progress dot and `False` on every other) and, in its tooltip, the cost ("Cost: 131 Science").
- Load/save: **the Mods column writes a multi-sentence dossier into `Content`.** The save table's
  `RuntimeModules` column (`Public/Gui/GuiElements[Tables].xml`, `SaveGames`) hangs a
  Content-backed tooltip carrying the verdict sentence, a "Configuration:" heading and a line per
  module — so `GraphNodes.ModeFor`'s premise for announcing Content ("the single sentence the game
  wrote") does not hold for it, and the column overrides the rule to INDICATE. `GuiTableHeader`'s
  drawn caption is translated; `PropertyName` is the column's stable name for a screen that needs
  to single one out.
- Politics: `PoliticalEventsPopulationPanel`'s table binds `canSelect:false`, has per-system
  columns, keeps names only on the tooltip WRAPPERS and values only as cell tooltips, and its
  `%SystemPopulationPoliticsTable*Title` keys are parked.
- Election: **the action outcomes are never drawn** (`ElectionFinalPanel.Refresh` :180-181 hides
  both branches unconditionally), the modal nulls `OverrolledTransform`/`FocusedControl` on every
  step change (:71-77) so a hover highlight must be re-armed, and `%ElectionScreenTitle` is a
  parked key.
- Election, the vote breakdown (`ElectionLocalPanel`, step 1) — everything on it is drawn from
  private state and most of it carries no words at all:
  - **The Political Trends bars are positionally bound and so ARE attributable.**
    `Refresh` :208-209 `ReserveChildren`/`RefreshChildrenIList` over
    `starSystemElectionInformations[currentStarSystemIndex].PoliticsWithLocalScoresAndCumulatedScores`,
    so bar `i` is entry `i` of that list; each entry is `KeyValuePair<PoliticsDefinition,int[]>` with
    `Value[0]` = this system's count and `Value[1]` = the count through this system (the struct at
    :13-34). The private fields are `starSystemElectionInformations` (:86),
    `currentStarSystemIndex` (:74) and `cumulatedRepresentativesCount` (:88); the struct is private
    too, so its fields are looked up off the boxed value. Measured on the user's save: 7 entries,
    bar 6 (`Politics00`, Independent) invisible — `BindPoliticsCumulativeSupportGauge` :306 sets
    `Visible` from `Senate.AvailablePolitics`, so visibility already IS the party filter.
  - **The counting-progress bar has no words anywhere.** The three segments (:239-250) are
    `PreviousRepresentativesGauge`/`SystemRepresentativesGauge`/`RemainingRepresentativesGauge`, and
    all three are children of `CumulatedRepresentativesGauge`, which sits INSIDE the Overall Empire
    box in the trends column (measured rect 996,348,168,4 inside 980,318,200,80) — not beside the
    system carousel it advances with. The two numbers behind it are
    `starSystemElectionInformations[current].CumulatedRepresentativesCount` and
    `cumulatedRepresentativesCount`.
  - **`Show` starts a 1.5 s auto-carousel** (:180, the `MoveCarousel` coroutine :384-400) that keeps
    stepping to the next system until a Prev/Next click sets `moveCarouselAutomatically` false
    (:70,:350-366) — so the panel rewrites itself under a reader. Setting that private flag false is
    exactly the state one arrow click leaves, and the coroutine exits on its own when the index is
    already the last (which is why a one-system save cannot demonstrate it).
  - **The representative strip WRAPS at three items.** `SystemRepresentativeTable` is 72 px wide and
    lays two 36 px items per line (measured: Item000/Item001 at y=740, Item002 at y=760), so a
    geometry-derived row splits one system's parties across two lines of navigation. Row membership
    there has to be declared, not read off the rects (`Cells.EmitRow`).
  - **A representative item's tooltip is class-backed** (`Class` = "Politics", `Target` = a
    `GuiPolitics`), and its `Content` holds the party's element NAME ("Politics01") — an authoring
    leftover, never a caption. The party's clean word is `Gui.GetLocalizedTitle(definition.Name)` or
    `AgeText.Clean(wrapper.Title)`; both answer "Industrialists" (the `GuiPolitics.Title` symbol
    glyph cleans away).
- Election, the result (`ElectionFinalPanel` + `WinnerSenatorCard`, step 2) — a winner card is
  THREE independent things drawn in one box, and reading the box's labels as one phrase
  ("Militarists Established +Industrialists") says three facts as if they were the card's title:
  - **The party and its experience tier are separate labels of `SenatorBaseCard`** —
    `PoliticsNameLabel` (`= GuiPolitics.Title`, :121-124) and `PoliticsExperienceLabel` (the
    tier WORD out of `GuiPolitics.FindExperienceInformation`, :165-176). The card's dossier is
    `PoliticsTooltip` (class-backed, `Target` = the `GuiPolitics`), and `NameTooltip` and
    `PortraitTooltip` are `Copy`s of it (`WinnerSenatorCard.cs:42`, `SenatorBaseCard.cs:154`), so
    a card has the SAME dossier hanging on three widgets — collecting a card's tooltips by
    walking it (`SettingRows.RowSections`) buffers the dossier three times.
  - **`ExperienceTooltip` is content-backed** (`%SenatePoliticsExperienceDescription`, :116-119),
    so the shared short/long rule would ANNOUNCE that definition on every landing on a winner.
    Mod policy: the card names which tooltip speaks — the party dossier is Indicate mode
    (buffer-only, hover-drawn), the experience sentence is declared `TooltipMode.None`,
    because it explains a word rather than the card.
  - **The vote-redirection badges exist only where both halves hold**: `redirectedVotes.Count > 0`
    AND `GuiGovernment.CanRedirectVotes(empire)` (`WinnerSenatorCard.cs:85-92`). They are pooled
    children of `AdditionalPoliticsContainer` (`ReserveChildren`/`RefreshChildrenIList` :88-89),
    each a `PoliticsMiniature` whose `Label.Text` is `"+" + GuiPolitics.SymbolString` — an ICON
    token, which the mod's inline-icon naming renders "+Industrialists" — and whose `Tooltip`
    is content-backed `%ElectionFinalVoteRedirectionDescription`, one sentence naming both
    parties (`PoliticsMiniature.cs:14-21`).
  - **The badges' rectangles are no reading order.** `RefreshPoliticsMiniature` (:116-133) places
    each one at a computed ANGLE around the support gauge (measured: a card at [414,180,200,430]
    with its badge at [613,466,24,24], i.e. outside the card's own column), so the row is
    declared, not banded (`Cells.EmitRow`).
  - **`AdditionalPoliticsGroup` fades in on a modifier** started by a delayed coroutine
    (`PostponeSecondarySupportAnimation` :135-146 → `StartAllModifiers`), so `Visible` is true
    while the group is still at alpha 0 — the badges are gated on `AgeWidgets.Painted`.
  - Fixture-blocked on the owner's own election (two winners, one badge each, both
    "Established", neither with a hero): a card with several badges or none, the senator-hero
    variant (`HeroExperienceGroup`, `SenatorBaseCard.cs:131-150`) and the experience-GAIN gauge
    (`PoliticsExperienceGaugeGain`, drawn only for `data.ExperienceGain > 0.1f`, :93-113).

## Military, ships and the designer

- **`EnrollButton` is invisible early game** — the button actually drawn there is
  `UpgradeButton`, which opens the ground-troop modal — and `OnClickManPowerCb` is entirely
  god-mode (7 groups, so they are declared transparent).
- **Module tiles are double-click-only** (`UseLeftClick=false`), and the slots wire
  `OnSlotUnequipCb` to BOTH the empty frame and the fitted button. The category filter DIMS
  slots, so enabled ≠ will-take-this-module — `CanModuleBeBound` is the test, and the game's own
  drag re-enables the compatible ones.
- **A ship slot draws three separate facts as wordless pictures, and the game has a title for each.**
  `AgeWidgets.TextOf` answers empty on all three markers — the transforms hold only image children
  (`Dot1`, `Dot2`, …), which is why the designer's first attempt to read them was dead code:
  - **Effect multiplier** — `Slot.Definition.EffectMultiplier` → `GuiSlot.Multiplier`, drawn as 2, 3
    or 4 DOTS (`SlotMultiplierx2/x3/x4`, shown at `== 2`, `== 3`, `>= 4` — `ShipDesignEditionSlotItem
    .Bind` :82-84). The game's words: `%PanelFeatureSlotMultiplierTitle` = "{0} Multiplier",
    description "{0} instances of the module are installed in this slot".
  - **Symmetrical pairing** — `Slot.EditingListeners` → `GuiSlot.IsSymetrical`, drawn as the single
    `SlotPairingFlag` circle. `%PanelFeatureSlotSymetricalTitle` = "Symmetrical (x2 cost)" — the
    doubled COST is this flag's, while the dots multiply the module's effect.
  - **Heavy mount** — `Slot.Definition.IsLargeSlot` → `GuiSlot.IsLarge`, with no marker at all: the
    slot is simply drawn 1.3× bigger (`ShipDesignBaseSlotItem.Bind` :21-26; measured 57×57 against
    its neighbours' 44×44). `%PanelFeatureSlotLargeTitle` = "Heavy Mount".

  **A symmetrical pair can never be split by a re-sort, because only one of the pair is ever drawn**:
  every `ListenerSlot` target is `IsEditable="false" IsHidden="true"` (25 listeners ↔ 25 hidden slots,
  exact match, in `HullDefinitions[Balancing].xml` — the only file defining them),
  `ShipDesignBasePanel.RefreshShipSlots` (:222) filters `IsHidden` before creating drawn items, and
  fitting the driver copies the module into the hidden twin silently (`Slot.BindModule` →
  `EditingListeners[i].BindModule(silentSlot: true)`). **Fixture limit**: multiplier and pairing exist
  ONLY in `HullDefinitions[Balancing].xml` (12 `EffectMultiplier`, 25 `ListenerSlot`, and ZERO of
  either across the other 19 hull files), so no faction hull in an ordinary game draws either marker
  and the heavy mount is the only one of the three a normal game reaches. **Mod policy**: the three
  are spoken on EMPTY slots only, at the end of the line — a filled slot's module tooltip already
  ends with a "Slot Information" section drawing the multiplier.
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
  titles as "Starting Skill" (only the dossiers differ). A starting skill's real identity is its
  mastery: `HeroSkillDefinition.SkillLevels[0].MasteryLevels[*].MasteryName`, localized
  `%<name>Title` (`HeroMasteryCommand` → "Command", `HeroMasteryLabour` → "Labor") — the mask is
  deliberate (`GuiHeroSkill.Title` answers the generic word whenever `isStartingSkill`, and the
  underlying defs are hero-unique with no usable localized names).
- **`PanelFeatureEffectsSets.TitleLabel` ("Effects:") belongs to the FEATURE**, not to a separate
  header feature — and a multi-level skill tooltip is N sibling copies of
  `PanelFeatureSkillEffectsSets`, one per level (current, then next), each with its own "Effects:"
  caption. Only the visible siblings read (0 levels → next only; maxed → current only).
- `GuiEffectMapper.UnloadEffects` retires effect lines by setting **Alpha = 0** while `Visible`
  stays true — the pooled-table alpha trap, in a second place (first: retired table rows).
- In this install the player's registry `TooltipDisplayDelay` is genuinely **0.0**, so
  `TooltipDelay(-1)` restoring to 0 is correct, not a leaked override (`RegisteredTooltipDelay()`
  reads `Application.Registry` and agrees).
- **`GalaxyView.SelectGameNode` on a colonized system does NOT leave the galaxy view** — it
  branches to `RequestGalaxyViewLevelChange(typeof(GalaxyViewLevel_SystemManagement), …)`
  (`GalaxyView.cs:155-166`) but measured lands where `ZoomInOnNode` does: zoom step 13, galaxy
  page still focused, orbital cards drawn (2026-08-20).
- **`FleetActionButton*.OnClick` indexes `GameNodes[NodePosition.NodeIndex]`, which is -1 for a
  fleet in transit** — the game's orbit gate is all that stops it throwing; never force-enable
  these buttons off-orbit.
- **The orbital card's button set is not final on the frame the card appears**:
  `PlanetLabel_SystemOrbital` blanks its buttons at bind (:444-450) and the refresh re-enables the
  applicable ones over the next frames — a positional child id can change owner between frames
  (measured 2026-08-20; why `FollowActionSeat` requires 20 steady frames).
- **`Constellation.Exploration[empire]` is a STALE aggregate**: it recomputes only on
  node-exploration events, counts member systems at node state ≥ 4 (visited, not merely seen),
  and at turn 1 all five constellations read 0 — including the one the empire lives in. It is
  the constellation label's own show gate; the mod's grouping mirrors it exactly.
- **`EntityExploration.GetCurrentStates()` is public and by-reference** — no reflection needed
  to force/restore an exploration byte (the states array IS the storage).
- **`ConstellationLabel` is CULLED, not just alpha-faded**: `ConstellationLabelsWindow` hides any
  label whose `CulledIn` is false, and `MarkLabelsCulling` reruns on every camera-POSITION change
  (`SpecificUpdate`) — a one-shot force is undone by any pan, which is why `ConstellationLabelHold`
  re-asserts per frame; `window.Dirty = true` is a complete reflection-free restore (`Refresh`
  calls `MarkLabelsCulling` unconditionally). In `unlocked` no `GalaxyConstellation` is ever
  culled in at ANY camera position. Constellation GUIDs in `unlocked`: Canista 1, Andromeda 72,
  Vela 264, Herkules 516 (home), Fornax 713.
- **The lane gate, second half**: a starlane's line is built end-to-end at link creation and
  tinted uniformly from the link's own state (`GalaxyLink.Refresh` :247-252 passes the SAME
  state for both extremities); what shortens a lane into the dark to a stub is the FOG SHADER —
  `FOWRendererService` publishes the empire's distance field as a global `_DistanceToFOW`
  texture (:347) the map's world materials sample. So `Exploration >= PartiallyRevealed`
  answers "is this lane lit", never "lit HERE" (measured 2026-08-20).
- **The labels/geometry split**: everything the map names out in space EXCEPT lanes is an AGE
  label whose window gates itself — its declaring gate IS the drawn answer. Lanes are world
  geometry occluded by the fog shader, the one class whose place-reading needs a second,
  position-aware gate (`IVisibilityService.IsExplored` per unit square).
- **Alpha is not a gate for the constellation tooltip family**: a held label at play zoom is
  `Shown=True, Alpha=0` and its tooltip still fills and reads — `ConstellationLabel.Refresh`
  writes Content/Target/Class regardless of alpha.
- Live hull-oracle result for `unlocked` (2026-08-20): `regions 5, members 136, outside own
  hull 0, inside another hull 22, classified elsewhere 0` — the interlock is real (22 members
  sit inside a neighbour's hull) and the nearest-member tie-break resolves every one.
- `MetaplotBattleRulesIcon` lives INSIDE `HomeAndTradingTable` (child 10) — it was always read
  by the table walk; a field-by-field audit counts it as an unread field.
- **A drawn empire colour cannot identify a minor faction**: all twelve minor empires share one
  grey (0.627³), and the neutral/unknown fills are white differing only in alpha (0.753/0.251).
  Where colour is not injective, gate on the drawn COUNT and read identity from the writer's own
  data source (`RefreshEmpireColoredBar` :1851-1867 — not Lost, not Ghost, visibility ≥ 1,
  player inserted first).
- On the pooled `StarSystemLabel` set only labels whose ROOT `Visible` is true have run their
  `Refresh*`: 135 of 136 hold prefab-default `Visible=true` on the badge groups, and the
  ancestor walk in `AgeWidgets.Visible` is the only thing keeping them silent.
- `GuiBehaviour.AgeTransform` and `AgeTransform.AgeTooltip` are Awake-cached — NULL on prefabs;
  instantiate before touching either.
- **A planet row is a LEAF until the map binds its orbital card** — a landing inside a collapsed
  system therefore costs a camera flight (expand system → wait for the flight → the planet becomes
  a group → expand planet → land; measured 28 frames vs 3 with the camera already in). Budgets on
  pending landings must be sized in frames-of-flight, not frames-of-rebuild.
- **`FleetActionToggleReclaimMothership.OnToggle` has two branches** — no running action →
  `ZoomInOnNode`; already running → the game's `%ConfirmCancelReclaimDescription` message box,
  no zoom.
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
- **The tactics deck window's two panels are asymmetric about captions.** The SET draws its own
  `MyDeckGroup/PanelTitle` = "Tactics", with the tooltip "Displays all your selected tactics"; the
  AVAILABLE list draws no caption of any kind, only
  `%PlayCardDeckModalWindowAvailablePlayCardsCountTitle` ("4 tactics available"), which is a count
  sentence and not a name. So one of the two stops is named by the game's own drawn word and the
  other's name is necessarily the mod's (`tactics.available-panel` = "Available"). The panel name is
  announced on arrival and never focused, so the caption's own tooltip has nowhere to live — the
  parity cost of not declaring the caption as a row (owner ruling 2026-08-19).
- Nine `EndBattleStatus` words; the realization labels are subjectless; the WatchBattle opt-outs
  are the game's own; the pre-roll is a raw-input gate; battle-speed keys are
  Plus/Minus/Asterisk/Pause, none of which the mod claims.
- `ShowOtherCards` does not clamp; clicking an already-selected card IS the validation; and the
  ENEMY play cards set YOUR plan.
- **A ground-battle outcome's second click is on the item's own transform.** Measured off the
  unbound prefab (`GroundBattleOutcomeSelectionNotificationWindow.OutcomeItemPrefab`, readable with no
  battle running): `GroundBattleOutcomeItem.Toggle` sits on the item's own `AgeTransform`, carries
  `UseDoubleClick` with `OnDoubleClickMethod = OnDoubleClickCb` (select AND validate, :74-79) and
  `OnSwitchMethod = OnToggleCb` (select only). That is exactly the shape the notification screen's
  choice reader already declares and already gives the double-click chord to, so the gesture is
  covered without a battle to run it on. A prefab's `AgeTransform` is null until it is instantiated —
  probe the fields, not the transform.

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
- **Who the player stands with, in one call**: `empire.GetAgency<DepartmentOfForeignAffairs>()
  .GetDiplomaticRelation(other)` → `.State.Name` (a `StaticString`) and `.State.IsWarState`, plus
  `.HasAbility(DiplomaticAbilityDefinition.Names.Alliance)`. The state names are FOUR separate
  ladders on `DiplomaticRelationState.Names` — `Major` (Unknown/War/HotWar/ColdWar/Truce/Peace/
  Alliance/Team), `Minor` (Unknown…Cordial…Integrated, plus its own War), `Pirate`
  (Aggressive/Neutral/Cordial/BestFriend/Peace) and `Academy`. `IsWarState` covers the Major, Minor
  and Academy wars and **never a pirate state**: pirates are hostile by disposition, not by a war
  state, so any "at war with" test that must include them needs the pirate branch written out (the
  scanner treats a `PirateEmpire` as an enemy unless its state is `Pirate.Peace`). The player's
  relation to their OWN empire has a null `State` — ask identity first. Measured on `[Beginner]
  test`: three unmet majors (`Major.Unknown`), nine minors (`Minor.Unknown`/`Cordial`), one
  `LesserEmpire` (`Lesser.Default`) and one `PirateEmpire` (`Pirate.Neutral`).
- **The map's own friend/neutral/enemy split is NOT the diplomatic one.** `GuiFleetGroup.Title`
  compares `DiplomaticRelationState.GetDiplomaticRelationStateValue(state)` against ColdWar and
  Peace — and that function answers **-1 for every non-Major state name**, so a cold war, every
  minor faction, every lesser empire and every pirate all read as ENEMY in the lozenge's count
  phrase. Mod policy (owner's taxonomy, 2026-08-16): the scanner does not borrow it — enemy is at
  war (pirates included), friendly is the player's own plus alliance/team, everything else is
  neutral.
- **Whose colour a system's label paints** (`StarSystemLabel.RebuildColonizedStarSystemsList`):
  among the `IColonizedStarSystemRepositoryService.GetValues(node.NodePosition)` colonies, the ones
  at `Visibility[player] >= 1`, preferring the player's own, and only those whose `State` is
  `Colony`. So an OUTPOST has no owner by that rule — which is why "is this system mine" is asked of
  `DepartmentOfTheInterior.ColonizedStarSystems` instead (the same list the tree's owned region is
  built from), where an outpost counts.

## Galaxy labels, probes and the scan view

- **A `SpecialNode` IS a `StarSystemNode`.** Nebulae, dust clouds and the rest are drawn in
  a star's place, perceived through the same gate, and have rows in the galaxy tree. Any
  code branching on "is this a place" must count them — counting only non-special nodes
  made inspect mode's Enter a silent no-op on the Solar Nebula (B10 6805, the one special
  in `[Beginner] test`). Mod policy: places = systems + specials.
- **`Fleet.IsAutomated` is true for the free-movement fleets in `[Beginner] test`**, so
  `FleetPresence.Selectable` refuses them and `GalaxyHudScreen.SelectFleet` is a silent
  no-op on that fixture — any "go to this fleet" relying on selection alone reads as a dead
  key there. The game's own tooltip says so: feature class
  `PanelFeatureGarrisonInfoAutomatedFleet`.

- **A starlane is ONE `Link` shared by both end systems**, so per-system nodes built from a link
  must key STRUCTURALLY (measured as a focus teleport on a fog-off build).
- **Camera culling is not an information gate.** Every `VisibleEntityLabelsWindow` (probes,
  obliterator projectiles) and the coordination-request window make TWO separate tests per label:
  `RefreshLabelsCulling` keeps only the entities Unity's own `CullingGroup` reports inside the
  world camera (`GalaxyEntityCulling`, registered against `CameraPreRenderHookHandler.WorldCamera`,
  no distance bands set), and `ShowOrHideIfVisibleByEmpire` then applies the real knowledge gate,
  `Visibility[lookingEmpire] >= 3` (Visible). Only the second is about what the player may know.
  **Mod policy:** anything enumerating these things reads the SIMULATION
  (`DepartmentOfDefense.Probes` / `.ObliteratorProjectiles`,
  `ICoordinationRequestRepositoryService`) with the `>= 3` gate, never the drawn-label list — the
  probes were on the drawn list and a whole scanner category disappeared when the camera moved
  (`MapVisibility.Sighted` is that gate; `GalaxyHudScreen.Anchor` is the worked example). The
  label is still attached when one is drawn, because the game assembles a probe's DOSSIER onto the
  label's tooltip at draw time and there is no other source for it — everything else a row says
  (`GuiProbe.Title`, `GuiProbe.RemainingLifetime` + `[turn]`, the owner) comes off the entity.
- **`EmpirePosition.Known` is what reveals a foreign capital, and it is not enough on its own.**
  `DepartmentOfIntelligence.RefreshEmpirePosition` (:479-535) sets `Known = true` for another
  empire once ANY of that empire's colonies is explored (≥ 4) or in sight (≥ 3) with the colony
  itself visible (≥ 1); the position it stores is the HOME system's when the home system is among
  those, and otherwise the empire's highest-influence visible colony. When nothing is visible it
  writes the home position anyway and sets `Known = false` — so the stored position equals the
  home system's for an empire the player knows nothing about, and a gate on the position alone
  would leak every capital in the galaxy. The diplomacy lens draws its home circle off `Known`
  (`GalaxyStarSystem.ContentForDiplomaticScanViewForHomeSystem.Update`) and iterates MAJOR empires
  only. **Mod policy:** a foreign home system is named only when `Known` is true AND the known
  position is the home system's; minor factions are not asked, matching the lens. Measured in
  `[Beginner] test` turn 21: all three AI majors read `Known=False` while their stored positions
  are exactly their (unseen) home systems — Leaper/Baten, St Chaoiver/Jundur, Doria/Lonica.
- **An ally PIN and an obliterator MISSILE are recomposable in full, so neither needs its label.**
  A missile's whole reading is arithmetic on the entity (`ObliteratorProjectileLabel.Refresh`):
  turns = `Ceil(|position − Destination.GalaxyPosition| / Speed)`, or 99 at zero speed; the tooltip
  is `%ObliteratorProjectileLabelDescription(turns, destination)` and the countdown `turns + "[turn]"`
  — **both written for the player's OWN missile only**, which is the game's ruling on what an empire
  may know, and its knowledge gate is the probes' (`Visibility[empire] >= 3`). A pin's message is
  `CoordinationRequest.Message` (the label's field is assigned from it every refresh, so the entity
  is the source and the field is only a possibly-truncated rendering of it); its two sentences are
  `%CoordinationTools⟨RequestType⟩CoordinationRequestTooltip` plus a sender line that branches on
  ownership (`…SenderCoordinationRequestTooltip`, or `…ReceiverCoordinationRequestTooltip` with the
  owner's name + faction); and its DISMISS is two deterministic routes, not a widget click
  (`CoordinationRequestLabel.OnDismissCb`): your own pin posts `OrderRemoveCoordinationRequest`,
  anybody else's is `SetForceHidden(true)` + `UpdateVisiblity(playerEmpire)` — and the label's own
  `Hide()` need not be replayed, because the request raises `VisibilityChanged` and any label hides
  itself off that. A pin's knowledge gate is `CoordinationRequest.IsVisible(empire)` (not
  force-hidden, and shared with the alliance). **Mod policy:** that gate and nothing else — the other
  half of `CanShowRequestLabel`, `ICoordinationRequestRepositoryService.ShowRequestToggle`, is the
  player's global "draw the pins" switch, and whether a reader obeys a display toggle is a design
  question rather than a fact about knowledge (left unobeyed, flagged to the owner).
- **`GalaxyQuestMarker` is a world object, not a culled label**: `UpdateVisibility` (:157-165)
  only asks whether `Marker.Empires` lists the active player's empire, so enumerating quest
  markers from the journal with that one test matches the picture at every zoom. A marker's
  position resolves through whatever it is bound to (`QuestMarker.GalaxyPosition`), which is
  `GalaxyPosition.Zero` when the target has none.
- **A mining probe is surfaced only in the planet's dossier** (`PanelFeatureMiningProbe.Bind`
  :15-58), and its gates are split: the owner's leader name is written for ANY empire's probe
  (`%PanelFeatureMiningProbeDescription` + `GuiEmpire.GetLeaderName`), while the yield and the
  remaining turns are written for the player's OWN probe alone — and a player's own probe with no
  yield hides the whole feature. `GuiPlanet` is the `IMiningProbeBonusProvider`.
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
- **The map draws its own lines through `ILineRendererService`, and two of the six arguments are
  not what they look like.** `CreateLine(pos0, pos1, width, color0, color1, materialType)` +
  `ShowLine` puts a `LineToRender` — a plain record of public fields the manager reads live each
  frame, so moving one is field mutation — into the SAME manager the starlanes use
  (`Services.GetService<ILineRendererService>()` and the galaxy technique's own answer are one
  object, measured). But: **`materialType` is an INDEX into a private `materials[]`** the manager was
  loaded with (`GetMaterialIndex` answers -1 for a foreign material), so it is borrowed off a live
  `GalaxyWarplink.Line` (0 on this build); and **a `Color32` is not a colour** — it is two packed
  16-bit indices into the GPU colour palette (`GalaxyLink.Refresh` and `GalaxyStarSystem`'s
  `defaultWhiteEncodedColor` both build one as `(slot & 0xFF, slot >> 8, slot2 & 0xFF, slot2 >> 8)`),
  so slots come from `Amplitude.Unity.Graphics.Services.GetService<IGPUColorEvolutionService>(5)`
  (`RegisterColorSlot`/`ChangeColorSlot`/`FreeColorSlot`; context 5 is the galaxy's). A line that
  gets either wrong is accepted, reports itself `Visible`, and is simply not on the screen. **The
  `width` argument is ignored by material 0** — 0.1, 2 and 20 all draw the same hairline (measured) —
  and the drawn hue came out a pale cyan whatever colour the slot was registered with, so a mod-drawn
  line is told apart from a starlane by being cyan, not by weight. `ReleaseLine` ×N and
  `FreeColorSlot` on teardown; the manager's own `lineToRenders` count is the check.
- **A short line's invisibility is about the CAMERA, not the line — corrected 2026-08-16.** The
  earlier reading ("a 3-unit line is invisible under every `materialType` 0-13, a 16-unit one draws a
  stub, a 33-unit one draws almost whole") was taken at zoom step 9 and generalised into a rule about
  world length; it is not one. The lane shaders eat off each END in something closer to SCREEN space,
  so the same 3-unit line is invisible far out and draws as a solid bar close in: crop evidence, the
  inspect cursor's 3-unit cell edges drawing as four clean bars at the zoom the cursor's own camera
  sits at. **What this cost:** the overshoot workaround — running every side ten units past its
  corner — was built on the false rule and was never needed at the zoom the mode actually uses; it
  was the sprawling crop-mark frame the owner then rejected. Before working round a length
  threshold, re-measure it at the camera the feature will really be used at. Thickness IS still dead
  (the width argument is ignored by material 0), so a heavier line has to be several parallel ones.
  Materials 0-13 are lane, wormhole, diplomacy, trade-route ×3 and hacking-route ×8.
- **Filled quads and rings are not available on the galaxy view.** `QuadRendererManager` is loaded
  with an EMPTY material list (measured `materials.Count == 0`), every `QuadRenderer` the build
  defines is a distance-field NUMBER (`Amplitude/Galaxy/PathNumber`, the turn markers on a fleet's
  path) and `QuadToRender` needs an `IAtlasElement`, so there is no solid-fill quad to draw with.
  **Rings: still unavailable, but every reason first given for it was wrong** (re-measured
  2026-08-16, crops at each step). `ICircleRendererService` lives at renderer context **0**, read off
  a live orbit ring's own `RendererContextIndex` — asking at 5, where the colour palette lives,
  answers null, and a null service draws nothing and raises nothing, which is what the first
  investigation actually hit. The mask is not the obstacle either: live it is
  `0xFFFFFFFFFFFEDFFF` (only `CurvedLine` and `QuestMarker` cleared), so `PlanetOrbit` — where all
  444 of the game's own live circles sit — and `Line` are both ON. And the manager's shown list IS
  the render source: hiding every `materialType == 0` `CircleToRender` removed the solid planet-orbit
  rings from the screen. Even so, a circle created through `CreateCircle` + `ShowCircle` never
  appeared — not on `Line`, not on `PlanetOrbit`, and not when given a drawn orbit ring's exact
  position, axes, width, material index and encoded colour, at radii from 1 to 9. Whatever the
  remaining difference is, a mod cannot get a ring onto this view; **do not spend a stage on it
  again without a new lead.** (A trap met on the way: writing a live circle's `Radius` proves
  nothing — `CircleRenderer.Draw` re-`Init`s its record from the component every refresh.)
- **THE BORROWED-RENDERER SAGA IS CLOSED: the mod draws its own overlay (2026-08-17).** The three
  bullets above stay true about the game's renderers and stay worth reading before anyone asks one
  of them for a mark — but the inspect cursor no longer uses any of them, and no future map mark
  should start there either. Every borrowed answer failed the same way twice over: the mark is drawn
  IN THE WORLD, so it shrinks with the camera (the case that has to work is a one-unit cell at full
  overview zoom, about one pixel of world), and the mod controls neither thickness (width ignored)
  nor hue (a palette index, not a colour). A `MonoBehaviour` of the mod's own with an `OnGUI` that
  projects the cell's four world corners through `ICameraService.Camera` (`Default Camera`) and
  strokes textured rects round the bounding box has none of those problems: IMGUI composites above
  the whole scene AND above the game's own AGE windows at a low `GUI.depth` (measured — the square
  drew over an open `GuiTooltipWindow`), thickness and a minimum on-screen size are in PIXELS so no
  zoom can thin them away, and the colour asked for is the colour drawn. Cost is four
  `WorldToScreenPoint` calls and eight rects a frame while armed and nothing at all otherwise.
  `ES2Access/UI/InspectMarker.cs` is the worked example. Two engine notes it paid for:
  `WorldToScreenPoint` measures y from the BOTTOM and IMGUI from the top, and the host object must be
  DESTROYED (not disabled) on teardown, because a behaviour surviving a hot reload belongs to an
  assembly the next load cannot reach.
- **`GalaxyViewCameraController.CenterOnPoint(point, damping)` takes a bare point** and SmoothDamps
  to it, auto-clamped to the galaxy (`ClampCameraPosition`) — the way to move the camera to empty
  space, where `GuiManager.RequestGalaxyOverviewViewLevel` needs an entity and trips the mod's own
  `GalaxyLocate` watch. Damping 0.3 (the game's own figure) reads as one smooth slide per keypress.
  `ForceZoomingOnPosition(step, point)` is the same thing with a zoom step, and is how a camera
  reading is restored exactly.
- **Fog of war is a per-POINT question with a per-point answer**: `IVisibilityService.IsExplored
  (empire, GalaxyPosition)` samples the empire's fog-of-war distance field — the very field the fog
  is drawn from — so a region can be sampled square by square (121 lookups into a byte array cost
  nothing). There is no second, "currently visible" field for arbitrary points: the map draws ONE
  fog, so there is no unexplored/remembered distinction to resolve for a point. `GalaxyBounds` on the
  same service is NOT the galaxy's extent — it is that field's rect, scaled 2.5× (`VisibilityController
  .GalaxyBoundsScaleFactor`), so anything wanting "where does the galaxy stop" measures
  `Galaxy.GameNodes` instead (`[Beginner] test`: x `[-164.0, 22.8]`, y `[-41.5, 88.3]` from home).
  **The game has no UI word for the fog**: "the fog of war" occurs exactly once in the whole English
  corpus, in one quest objective's tooltip, and "miasma" occurs nowhere at all — so a mod that says
  it says it in its own words. The mod's word is **"unexplored"** (`galaxy.inspect.fog*`), naming the
  predicate it actually samples rather than the picture drawn over it.
- **`InputManager` binds no letter keys and no I at all.** The full default table is the F-keys
  (F1-F8 screens), arrows, `KeypadEnter` end turn, `Space`/`Mouse2` scan view, `Return`/`Tab` chat,
  `KeypadMinus` sleep-for-this-turn, `Ctrl+F` search, `PageUp`/`PageDown` zoom, the debug chords, and
  the encounter camera's `Minus,KeypadMinus`/`Plus,KeypadPlus` speed keys — so `Ctrl+I` was free.
- **`TopTitlePanel` is the only captioned cluster on the HUD.** It draws the view's own name over
  the zoom/scan controls (`TitleLabel` = "Galaxy View" on the map), while the pinned-quest panel, the
  notification strip and the top-left banner rows draw NO caption at all — measured on
  `/gui/age?window=GameOverlayWindow`: `ControlBanner` holds only `ScreenTogglesTable`,
  `EmpireBanner` only its three value areas plus `CurrentResearchArea`, and `StrategicsBanner` only
  `ResourceItemsTable`. So every word naming those panels and rows is necessarily mod-authored (the
  keys are listed in `interaction.md`), and the one cluster the game DOES caption is the one whose
  mod word deliberately overrides it — "View Controls" over the drawn "GALAXY VIEW", because the
  view's name says which page the player is on and the screen has already said that on arrival
  (owner ruling 2026-08-19).
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
- **A layer band is NOT a lens: nine descriptors map onto six lens titles**
  (`TopTitlePanel.Load`, :116-124 — Painting+GalaxyMap = Diplomacy, InformativeGalaxy+Constellation
  = Trade, Systems = Economy, System+SystemOverview = the system overview, plus SystemManagement
  and PlanetOverview from the view LEVELS). So three descriptor boundaries fall inside one title
  (steps 0→1, 3→4, 11→12), and crossing one still re-runs the per-layer alpha/position tables over
  the lens window, its sections and every label (`GuiLayeredScanViewWindow.cs:64-88`,
  `LabelMetaModifier.cs:233-262`) — sub-panels and label lines appear and disappear.
  `GalaxyLayerController.cs:78-83` early-returns on an unchanged DESCRIPTOR name, so the descriptor
  is the identity of the drawing and the title is only its heading.
  Mod policy (owner ruling 2026-08-17): `ScanViewScreen.AnnounceLens` speaks the lens at every
  descriptor change, same-name boundaries included — a repeated "Trade" is cheaper than a silent
  redraw.
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

- **The Laws Cancelled prefab hangs TWO tooltips per line** — the real `Law` dossier on
  `CancelledLawLine000` itself and a completely empty one (no class, no content, no target)
  on `LawDetails/Icon` — and wraps the whole line in `LawDetails`, a group spanning both
  captioned columns. Empty decoration tooltips on prefab icons likely generalize; the
  never-drawable filter (`AgeWidgets.NeverDraws`) exists because of this window.

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
- **The report family's breakdown toggle is REAL — the earlier "vestigial" reading was an
  ask-the-wrong-component error.** `ReportPanel.GetComponents<AgeModifier>()` really does answer
  zero, which is what the reversed claim rested on; the animation lives in the transform's modifier
  SET (`AgeFirstModifierSet`), which is what `StartAllModifiers` drives. Measured live 2026-08-15:
  collapsed, `ReportPanel` is `Alpha 0` and a crop of its rectangle is empty sky; activating the
  toggle draws "DAMAGE REPORT" and the ships line in the same rectangle. Never conclude "animates
  nothing" from an absent `AgeModifier` component — ask the panel's ALPHA in both states, and pair
  it with a crop.
- **A hacking outcome's countdown is REAL-TIME seconds** — 10/20/30/45 by outcome, not turns — and it
  auto-picks a default when it runs out, so the choice popup is one of the few surfaces where reading
  slowly changes the result. `PickHackingOperation` only raises its prompt where the node offers MORE
  than one operation (data-gated), so a single-operation node never shows the picker at all.
- `AgeModifierTypewriter`'s labels are complete from frame one (see the typewriter fact above);
  AGE also localizes label text itself, so assigning a raw `%key` still DRAWS localized
  (`AgePrimitiveLabel.cs:702-717`) — which means a drawn label is no evidence that the mod's own
  lookup would have resolved.

## Multiplayer, session and the install

- **Launcher stuck in session 0.** A `launcher-x64` orphaned into the *Services* session
  never exits and cannot be killed; the launch guard skips other sessions, but if a launch
  still fails, `tasklist /FI "PID eq <pid>"` tells you which session you are fighting.

- **The STEAM build has NO store code besides Steam** (measured on the Steam build; the GOG
  build differs — see the GOG bullets below) — no GOG/Galaxy/Epic assemblies or branches
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

- **The GOG build renames the galaxy class**: top-level `Galaxy` (Steam) is `GalaxyIngame`
  (GOG), because the GOG install ships `GalaxyCSharp.dll` — the GOG Galaxy SDK, whose
  namespace is `Galaxy.Api` — and the class name would collide. The `Game.Galaxy` PROPERTY
  keeps its name (`public GalaxyIngame Galaxy`), and `GameNodes`/`StarSystemNodes` are
  member-identical on both. Policy this forces: the mod never names the type — a member ref
  compiled against one store's assembly fails at RUNTIME on the other (the IL embeds the
  type name), which no build or test on one machine catches. `UI/GameGalaxy.cs` is the
  single reflected seam; any new galaxy-object access goes through it. The offline
  `StoreDivergenceTests` fails the suite if any other file names a divergent member.
- **The GOG build strips the Steam Workshop fields**: `ModdingScreen` has no
  `SteamWorkshopButton` / `WorkshopLegalAgreementButton`(+`Label`), and
  `ModdingAvailableModsPanel` has no `WorkshopFilterToggle`; every other field on both
  classes is identical. `UI/SteamWorkshop.cs` reflects them (null on GOG), and the modding
  page simply declares only the controls the running build draws.
- **`decompiled/` is a Steam-era snapshot** (generated 29 Jul 2026, before this machine
  switched to GOG) and disagrees with the live assemblies on at least the two points above.
  Regenerating via `decompile.ps1` would replace it with the GOG view and the Steam DLLs
  only exist on the other machine now — so when the snapshot and the live game disagree,
  verify against the installed build with
  `ilspycmd -t <Type> "<game>\EndlessSpace2_Data\Managed\Assembly-CSharp.dll"`.

- **The out-game pages, measured** (DLC browser, mod manager, disclaimer, credits):
  `DownloadableContentType` 1 is `Personal`, which the game words as "Add-on" — the browser's own
  type column, not an ownership state. `AvailableModItem` leaves a DOWNLOADING row's toggles
  enabled and its handler only logs (a game bug: the click does nothing), so the mod speaks that as
  the refusal it meant. `ModdingSelectedModPanel` swaps its two branches by ALPHA alone — both stay
  `Visible`, so a reader gates on `SelectedGuiMod` instead. `DisclaimerModalWindow.HandleInput`
  returns true for EVERY action and acts on none, so Escape cannot dismiss it and only its own two
  buttons can (Decline quits the game). The credit roll is 598 items and exits itself after
  ≈8.5 minutes.

- **A `SettingItem` carries TWO tooltips and only one of them moves with the value.**
  `SettingTitle.AgeTransform.AgeTooltip.Content` — what the setting IS — is written once at `Bind`
  :33, while `CurrentSettingTooltip.Content` is rewritten for every value the setting lands on
  (`SettingSliderItem.CurrentValue` :52-55, `SettingCheckBoxItem.Refresh` :50-53,
  `SettingDropListItem.Refresh` :72-75, each also setting `DirtyTarget`). The options screen's
  `OptionItem` has ONE tooltip, written at `Load` :23 and never rewritten. That difference is the
  whole discriminator for which settings re-read the game's sentence after a change
  (`SettingRows.SayValueTooltip` — helpers.md): the new-game lobby, the advanced settings modal and
  the pause menu's settings panels all do, and the options screen deliberately does not, because
  there the sentence has not changed and a re-read would repeat it on every keypress.

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
- **The in-game panel is discreet whenever nobody is typing in it and no pointer rests on it**, and
  `SetDiscreet` (:127-180) takes the field with it: `ChatTextField.AgeTransform.Enable` and
  `Label.Visible` both go false, `ChatTabs[i].ShowOrHide` hides the tab bar, and going discreet
  clears the engine's focus if the box still had it. So the field's own enabled flag reports which
  state the panel is in, never whether chat can be opened. `discreet` itself is a private bool on
  `InGameChatPanel` and is the one thing every part of that visibility derives from. `SetFocus`
  (:116-125) is the single way in — the chat key, a tab click (`ChatTab.OnTabCb`) and the
  new-message button (`OnNotificationCb`) all call it, and it focuses the field and un-discreets in
  one call, with `OnValidateObject` nulled for a frame so the keypress that opened the box cannot
  also validate it.
- **The POINTER is what keeps the panel open after the box lets go**: `OnTextFieldLoseFocusCb`
  (:310-317) goes discreet and hides every line unless `IsHoveringThePanel` (private, :493-514 —
  the panel's rect, its scrollbar, and the engine's `OverrolledTransform`) says the cursor is on it.
  A keyboard has no equivalent, so a mod that wants the panel walkable has to BE that pointer
  (`ChatHold`) — and dropping the engine's focus fires this handler first, so the hold is
  re-asserted after, never before. **Mod policy 2026-08-14**: chat is a mod-owned child screen held
  open exactly while it is up.
- **Escape out of the chat box is an ACTION dispatch, not a key delivery.** The field is
  `AgeControlTextFieldChat` with `StandardCancel=false`, so `InputManager.HandleInput` :1228-1239
  hands `InputAction.Exit` down the handler chain, and `InGameChatPanel.HandleInput` :108-112
  answers it with `SetDiscreet(true)`. `ChatPanel.OnTextFieldKeyDownCb` :232-246 has an Escape route
  of its own, but the InputManager's `Update` beats `AgeManager`'s LateUpdate KeyDown by a frame and
  has already dropped the focus that route needs — so the panel's own `HandleInput` is the only
  place that press can be intercepted (measured 2026-08-14).
- **A chat tab that answers `CanShowTab` false is never drawn** even while the panel is open:
  `ChatTab.OnBeginShow` gates on the same predicate, so `CanShowTab` and drawn-ness agree for a
  non-discreet panel and the mod needs only one of them.

## Card and tooltip drawing mechanisms

**A failed tooltip request is PARKED for 999 seconds, and only a change of hovered transform
lifts it.** `GuiTooltipController.Update` (Amplitude.Unity.Gui/GuiTooltipController.cs:214-224):
when the hover delay elapses and `ReadTooltipInformation()` says no — which it does when the
tooltip has neither `Content` nor `Target` (:235) — the controller writes
`timeBeforeShowingTooltip = 999f` instead of retrying. Two things, and only these two, re-ask:
`AgeManager.OverrolledTransform` becoming a DIFFERENT transform (:191), or the tooltip's
`Target` being written again, which sets `AgeTooltip.DirtyTarget` and makes :186-190 drop the
remembered transform. This is a mouse-shaped design — a hand moves, so the edge always comes.
`PointerFocus.LateTick` re-asserts the SAME transform every frame, so a keyboard user sitting
still gets neither edge and the tooltip is suppressed for the whole 999 s. Measured live: a
turn-start notification popup put focus on `notification:CompletedTechnologyTitle`, whose
`AgeTooltip` carries neither content nor target; the controller parked and was still counting
down (989 → 975) fifteen seconds later. **Mod policy:** never aim the pointer at a tooltip that
has neither content nor target, and where the mod holds a hover it must re-issue the request
itself when the window has not drawn — `AgeTooltip.DirtyTarget = true` is the engine's own
re-ask signal and needs no reflection, but it resets the countdown, so it must be issued once
per stall and never per frame.

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
- **RepresentativesStarSystemSidePanel draws its captions non-uniformly** (2026-08-18): one block's
  caption is a bare sibling title label, the other's sits INSIDE its group — the mod takes each
  block's caption as the topmost line the block produced, never by tree position.
- **The recipe window and the economy screen draw the same family grid** from the same
  `GuiResources` list; the recipe copy read `ExtendedGuiElement.Title` (the family DESCRIPTION,
  carrying the shipped icon typo "Improves Industry Food") while the economy copy maps
  `TargetEffect` to the game's short `%SubCategory…Title` words. One reader now, internal on
  `EconomyScreen` (2026-08-18).
- **A prefab tooltip's `%…` Content can be a placeholder the game overwrites at bind**
  (`NegotiationModalWindow` swaps in the war/influence pressure title and description at bind) — a
  caption test that localizes the prefab key alone answers "no sentence" for a caption that has one.
- **`QuestJournal.ActiveTutorial` picks the in-progress tutorial quest by popup layer then
  priority, and `TutorialWindow.Update` re-derives it every frame** — a hand-`Bind` of a different
  tutorial is overwritten next frame; a tutorial is raised only by making its quest win that
  comparison.
- **`FactionChoiceModalWindow` keeps its hull set in the private `filteredShipHulls` and its
  position in `currentHull`; `OnNextHullCb`/`OnPreviousHullCb` are the only movers and they wrap**
  — the mod's hull pager steps those callbacks, shortest way round.
- **`GameMenuModalWindow.Title` holds `%GameMenuModalWindowTitle`, localizing to "Game\nMenu"** —
  the only place the pause menu names itself; the mod joins the two drawn lines with a space.

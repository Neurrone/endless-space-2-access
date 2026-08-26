# ES2 facts — planets, colonies and influence

Planet cards on the orbital and management pages, colonies and outposts, the population drag,
the generic system-selection window, and the world facts the scanner is built on — influence,
colonizability and the four kind databases. The map itself is in `galaxy-map.md`; fleets in
`fleets.md`. Index and charter: `README.md`.

## Planet cards, colonies and outposts

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
- **The orbital card's button set is not final on the frame the card appears**:
  `PlanetLabel_SystemOrbital` blanks its buttons at bind (:444-450) and the refresh re-enables the
  applicable ones over the next frames — a positional child id can change owner between frames
  (measured 2026-08-20; why `FollowActionSeat` requires 20 steady frames).
- **A planet row is a LEAF until the map binds its orbital card** — a landing inside a collapsed
  system therefore costs a camera flight (expand system → wait for the flight → the planet becomes
  a group → expand planet → land; measured 28 frames vs 3 with the camera already in). Budgets on
  pending landings must be sized in frames-of-flight, not frames-of-rebuild.
- **The card draws FIDSI two different ways** (`PlanetLabel_SystemOrbital.RefreshFIDSI` :1012-1028):
  a colony gets `FidsiEnumerator` with numbers, an unsettled world gets `FidsiScoreTable` with pips.
  `FidsiProperties` holds SIX entries and `DisplayedProperties` is 5 — the sixth is `Happiness`, not
  an output. Read the numbers only where the enumerator is visible, or the buffer describes a card
  nobody can see. The property NAMES differ per state (`FidsiEnumerator.LoadPlanet`:
  `PlanetOutpost*` for an outpost, `Planet*` for a colony, `PlanetInitial*` for a world nobody has
  settled), and all three sets resolve to the same five titles ("Planet Food production" … "Planet
  Influence production"); whichever strip is not drawn is left bound to whatever it last showed.
- **A system's deposit dossier and a planet's are DIFFERENT panels.** The system label's strip draws
  one icon per KIND of deposit in the whole system, bound to a `GuiResourceDepositGroup` ("Common
  Strategic Deposit … Deposits on this system 1/1 / Resource income +0/+2 Hyperium"); the orbital
  card draws one icon per deposit ON THAT WORLD, bound to a `GuiResourceDeposit` ("Hyperium / Average
  Deposit / description / Deposit effects on planet: / +1 Science per Population"). Covering one has
  never covered the other, and the deposit SIZE exists only on the per-planet one.
- **Which deposits the card lists is the game's own two-way rule** (`RefreshResourceDeposits`): the
  COLONIZED deposits when `Planet.ColonizedPlanet` belongs to the player, the planet's raw
  `ResourceDeposits` otherwise. A membership read that ignores it disagrees with the icons for every
  colony of your own.
- **The card's pooled tables retire items by FADING them** (the general AGE rule is in
  `gui.md`), and a retired item keeps its old binding. Measured on Osulo III, which has
  no deposits at all and still answered `AgeWidgets.Draws` = true on two items titled Hyperium and
  Titanium — the previous planet's. `AgeWidgets.ItemText` already answers null for an alpha-0 item,
  which is why no buffer line ever spoke one, but `Draws` and the `Visible` test inside
  `TooltipChildren.Add` do not: any collector over one of these tables gates on `AgeWidgets.Painted`,
  and takes its MEMBERSHIP from the model rather than from the table. That includes the tables whose
  items are BUTTONS — `PlanetCuriositiesTable` (`RefreshPlanetCuriosities` :1297) and
  `OutpostActionsTable` (:988) — where a retired item is offered as a stop the player cannot see.
  A retired CURIOSITY item is also NAMELESS — the wrapper that carries the thing's name is the item's
  `Tooltip.Target`, and a retired one measured null — so it announced as a bare "button, unavailable"
  (Heka II 2026-08-26: one drawn curiosity, one leftover from another planet).
- **A drawn deposit item says a NUMBER and nothing else; the resource's name is on the wrapper.**
  `ResourceDepositItem.Refresh` (:28-42) fills `AmountLabel` with the figure and leaves `TitleLabel`
  to the prefabs that have one — the system-management card's does not, so the item's only children
  are an icon and a "Value" label. The name lives on the item's `Tooltip.Target`, a
  `GuiResourceDeposit` whose `Title` is what `AgeWidgets.TooltipTitle` answers. A reader that takes
  the drawn TEXT of these items (`AgeWidgets.ItemText`, which prefers drawn text over the wrapper)
  therefore reads "3" and "2" with nothing saying of what — measured on Ita III 2026-08-26, and the
  reason the card's deposit lines are composed per item rather than by the generic table reader.
  The same wrapper is what the item's DOSSIER is assembled from, so one drawn item is worth both a
  captioned line and a "Tooltips" child.
- **On a COLLAPSED card the info tables draw their icons and fade their captions.** Measured on Ita
  III 2026-08-26: `PlanetGameplayType000` paints at alpha 1 while its own `Title` child ("Cold")
  sits at alpha 0, and the same holds for the anomaly and curiosity items. So the ITEM is the level
  the drawn-ness question is asked at - `AgeWidgets.ItemText`'s alpha gate is on the item, and
  `AgeTransform.GetChildren`-style text scrapes ignore the caption's own alpha, which is what makes
  an icon-only row still answer with its word. Reading the caption's alpha instead would silence
  every trait on every collapsed card.
- **`PlanetAnomalyItem` hangs its dossier on its `Icon` child and wires its CLICK on the row.** The
  row's own `AgeTooltip` is null, so pointing at the row draws nothing; `OnActivateMethod=OnHintCb`
  is on the row while `Gui.FormatButtonHint` puts the hint STATE on the separate `HintButton`, and
  the hint is only ever filled in for a colonized planet of the player's
  (`PlanetAnomalyItem.Bind` returns early otherwise).
- **Terraformation and RESTORATION are one field of the planet's**, `TerraformationInProgress`, and
  the game tells them apart by the tags on the terraformation being carried out
  (`InitiateRestorationEmpireActionFleetActionDefinition.CheckConstructibleTags`): restoration is
  tagged `PlanetTerraformationFromDestroyed`, and anything tagged `PlanetTerraformationOnlyViaSystem`
  is neither, which is why the map draws no button for it.
- **GAME DEFECT: `%PlanetRestoreWithJuggernautInProgressDescription` asks for `{2}` and the game
  passes two arguments.** Its own drawn sentence therefore carries an unfilled slot. Mod policy: a
  phrase whose fill leaves a `{`digit standing is treated as absent and never spoken
  (`GalaxyHudScreen.Localize`), and the mod passes the leader name in the third position so the
  sentence completes.
- **The three in-progress juggernaut buttons DO name what they are doing, on the wrapper their own
  tooltip points at** (`PlanetLabel_SystemOrbital` :806-830, :885-900, :960-975 — the
  player-empire branch, the only one drawn enabled). Measured 2026-08-23:
  `InProgressTerraformationButton`'s `Target` is the `IGuiConstructible` of the
  `PlanetTerraformationDefinition` ("Terraform To Arctic"); `InProgressAnomalyReductionButton`'s is
  the `AnomalyReductionDefinition`'s ("Reduced Ice-10"); `InProgressRestorationButton`'s is a
  `GuiEntityAction` over `InitiateRestorePlanetFleetAction` ("Restore planet"). All three are
  `GuiWrapper`s, so `AgeWidgets.TooltipTitle` reads them headlessly with no hover. Their `Content`
  is the same on all three ("Remaining turns: N" + `%PlanetCancelJuggernautActionButtonDescription`)
  which is why the shared sentence names none of them any more.
- **`PlanetTerraformationDefinition` has no database of its own** — `Databases.GetDatabase<…>()`
  answers null for it; the definitions live in the `ConstructibleElement` database (1016 entries)
  and are found by type. `AnomalyReductionDefinition` DOES have one (28 entries). An anomaly
  reduction's `AltTitle` is an unresolved key (`%AnomalyReduction10Title`) while a terraformation's
  is a key the game localizes on purpose (`%PlanetTypeArcticTitle` → "Arctic"), so `Title` is the
  member to read for a name and `AltTitle` only where the game itself localizes it.
- **`PlanetLabel_SystemOrbital.PirateLairGroup` is a control despite its field type.** The field is
  declared `AgeTransform` and the widget carries an `AgeControlButtonRadial` wired to
  `OnClickPirateDiplomacyButtonCb`; the game keeps it DRAWN and merely switches it off for a
  pirate-hating empire, with the refusal appended to its own plain-text tooltip.
- **The unique-planet title the game already has** is `%PlanetScreenUniquePlanetTitle` ("Unique
  Planet"), read off `PlanetLabel_SystemDiscovery.UniqueSubtitle` on the UNSHOWN discovery window —
  the prefab carries the key and the bind never rewrites it.
- **A population ring is a list of SLOTS, and everything a slot means is its colour.**
  `PopulationEnumerator.BuildListOfGuiPopulations` (:132-167) lays one entry per unit out first,
  grouped by affinity in `ColonizedPlanet.PopulationsByAffinity` order, then null entries up to
  `MaxPopulation`; `RetrievePopulationMarker` (:207-220) binds every index at or past `MaxPopulation`
  as `locked: true` **with a null population**, so a colony holding more units than its current
  maximum draws the surplus as locked slots rather than as people. The comfortable maximum is
  `ColonizedPlanet.MaxPopulationUnderOverPopulation` = `floor(MaxPopulation ×
  PopulationPercentForOverPopulation)` (0.7 live), and the arc over the ranks past it is drawn only
  where `PlanetPopulationEnumeratorRadial.RefreshOverpopulation` says so: a colony, a system that is
  NOT an `ExploitedStarSystem`, `State != StarSystemState.Lost`, and `!Empire.CanUseHonor`. Its
  sentence is `%PlanetLabelOverPopulationDescription` when the arc covers exactly one slot and
  `…DescriptionPlural` otherwise — the game's own singular rule, off `MaxPopulation - safeMax` and
  never off how many people are in it.
- **Nothing on the ring the player navigates carries a tooltip.** Every tooltip binding in
  `PopulationMarker.Bind` sits under `IsDetailed`, and the detailed ring
  (`PlanetLabel_SystemManagement.PlanetPopulationEnumeratorFocused`) is swapped in only while a mouse
  is over the card — the SIMPLE ring is what is drawn for a keyboard player, and its markers are bound
  with no class, no content and no target. A marker's own rect is no help either: the radial markers
  all report the CONTAINER's rect (measured: five visible markers, all `543,446,261,261`) and differ
  only by angle. So a slot's dossier is the mod's own carrier, aimed at the ring's rect.
- **An OUTPOST planet still draws a ring — one slot, under the overpopulation arc.** Measured
  2026-08-26 on a personal save: an outpost's planet answers `PopulationCount 0`, `MaxPopulation 1`,
  `MaxPopulationUnderOverPopulation 0`, one visible marker, and `RefreshOverpopulation`'s four
  conditions all hold (the state is `Outpost`, which is not `Lost`), so the card draws the orange arc
  beside that single empty marker. A model-only reading that expects an outpost to have no ring is
  wrong about the picture.
- **A world NOBODY has settled draws a ring too, and `IsAvailable` is not the gate.**
  `PlanetPopulationEnumerator.IsAvailable` (:76-88) does want a `ColonizedPlanet`, but the base class
  spends that answer on `AgeTransform.Enable` alone (`PopulationEnumerator.Refresh` :117-129) — the
  markers are built and shown unconditionally. With no colony, `GetPopulationOwnerData` (:63-74)
  falls back to the PLANET (`Planet.PopulationCount`, `MaxPopulation`, `BaseMaxPopulation`) and hands
  the ring an EMPTY population map, so `BuildListOfGuiPopulations` adds nothing per unit and counts
  `populationCount → populationMaxCount` null entries: one EMPTY marker per point of room, none of
  them locked (no index ever reaches `populationMaxCount`) and no arc over them (
  `RefreshOverpopulation` wants a colony). Measured 2026-08-26 in Ita: Ita II (colonizable, max 6)
  and Ita I (inhospitable, max 3) each paint exactly that many markers, alongside Ita III's colonized
  5. Inferring "no colony, no ring" from `IsAvailable` is wrong about the picture, and it is how much
  room a world has that a colonization is decided on.
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
- **`StarSystemPlanetCardsPanel` is its own drop client** — the panel, not the page around it, takes
  both population moves: planet→planet, and the spaceport shipment, which the game routes through
  `GuiTableCellSystemPopulation`. So a second host wanting the same drag wires the panel, not the
  screen.
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
- `ColonyInfoSidePanel.SecurityAndTroopsTooltip` (:60, filled :549-555) hangs on
  `SecurityGroup` — `SecurityValue`'s own transform tooltip is null. `ColonyHeroSidePanel` swaps
  variants by `Visible` flags only (:157-240), the unassigned prefab keeps STALE hero text, and
  the unassign button is spelled `UnssignButton`.
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
  - `SystemSelectionModalWindow` binds `interactiveCells: false`, so its shipped table has no
    interactive cell at all.

## Influence, colonizability and the scanner's kinds

- **Influence is one circle per COLONY, resolved at nodes, strongest source wins** (2026-08-21).
  Each colonized system carries a radius it grows for itself
  (`ColonizedStarSystem.LastInfluenceValue`, refreshed from the simulation property
  `StarSystem.SystemInfluenceRadius` by `ColonizedStarSystemRepository.UpdateInfluence` :219-226)
  and a strength that falls off inside it, `(1 - (d/R)^InfluenceStrenghtPower) * R` — so a big
  circle beats a small one even well off its centre. Every node is resolved to the ONE strongest
  source standing over it (`TryGetInfluence` :77-129, walking the whole galaxy) and the answer is
  cached on the node as `GameNode.SystemWhichInfluences` (:212) — a plain property, so "whose
  influence is here" costs nothing, while "how far does this place reach" is
  `TryGetInfluenceRadius(NodePosition, …)` (:132-173), a dictionary lookup over the colonies at
  that one node plus, in the 4-argument overload, next turn's estimate off
  `StarSystem.SystemNextInfluenceRadiusEstimation`. An OUTPOST projects radius 0 (its descriptor
  forces `SystemInfluenceRadius` to zero), so the service answers FALSE for it — measured on Heka
  in `[Beginner] test`. A PIRATE base is the other end of the same trap: its colonies answer TRUE
  with a radius of **1E-08** and a next-turn estimate of 0 (measured on all four pirate systems in
  that fixture), so a "> 0" gate lets a reach through that speaks as "0.0". **Mod policy:** a
  figure that speaks as zero is no reach at all and says nothing (`InfluenceText.Radius`), the same
  silence an outpost gets. The read surface is `Services.GetService<IInfluenceService>()`.
  **`ColonizedStarSystem.InfluenceState` is a DIPLOMATIC verdict, not the factual comparison**
  (:2994-3031): it answers None for a foreign influencer the relation grants `NeutralInfluence`,
  so anything asking "is somebody else's influence over this place" must compare the empires
  itself. `InfluenceOwner` looks through an integrated minor faction to the empire that absorbed
  it, while the disk the map draws takes its colour from `SystemWhichInfluences.Empire`
  (`GalaxyStarSystem.UpdateInfluenceRange` :1932) — mod policy: name and compare the empire the
  COLOUR is drawn for.
  **Fog obligation:** every one of those values is global simulation state, identical for every
  player. The game's own disk is hidden on `Node.Visibility.IsInvisible(playerEmpire)` (:1926), so
  the mod gates every influence reading on `MapVisibility.Perceived` — measured: on
  `[Beginner] test` the sim has radii for 17 systems the player has never seen (Baten 6.58,
  Lonica 6.34, …) and the mod says nothing about any of them.
  System CONVERSION by influence is the game's own notification territory
  (`RefreshInfluenceConversion` :175-210 → `EventSystemUnderInfluence`); the mod reports the state,
  not the event.
  **Restoring the fixture after probing influence: `IInfluenceService.UpdateInfluence()` recomputes
  every `LastInfluenceValue` and every node's winner from the simulation**, so it is an exact undo
  for a probe that wrote either — measured 2026-08-21 (graph dump back to byte-identical bar the
  clock).
- **`Planet.IsColonizable(empire)` is two questions, and only one of them is about the planet**
  (`Planet.cs:796-921`, 2026-08-22). `IsEmpireAbleToColonize` picks a colonization constructible
  and checks its prerequisites — and the candidate list is rebuilt from the database by
  `Planet.Type` and nothing else (`RefreshColonizationConstructibles` :1310-1321), while both
  prerequisite checks run against the **empire's** simulation object. So the answer depends only on
  (planet type, empire): it can be memoized per type for a whole sweep exactly, which is what turns
  a per-planet prerequisite check into one per kind of world. `IsEmpireAllowedToColonize` is the
  other half and is genuinely per-system (already colonized, being colonized, another empire's
  colony in the system, own outpost, razed this turn, the neighbour rules) — mod policy: ask ABLE
  first, then ALLOWED only for a world nobody has settled.
  **Mod taxonomy (owner, 2026-08-22):** "unoccupied" = `!IsColonized && IsColonizable` (both
  halves); "occupied" = somebody else's `ColonizedPlanet` (outpost or colony, minor factions
  included) that this empire is ABLE to settle — the allowed half is deliberately not asked,
  because it refuses every planet in a system somebody else holds, which is the whole of that
  scope.
- **The kind a scanner row SAYS is localized; the kind a saved selector NAMES is not — and both
  are already in hand at snapshot time.** The scanner's four derived categories column themselves
  by the game's localized title (`new GuiAnomaly(definition, planet).Title`, `GuiCuriosity`,
  `GuiResource`), and the memo those titles are cached under is built from the game's own INTERNAL
  name — `AnomalyDefinition.Name`, `CuriosityDefinition.DisplayedType`, `ResourceDefinition.Name`
  (through `GuiResource.Name`, which is `GuiWrapper.name`, the definition's own `StaticString`).
  So storing a stable per-kind key costs one field on the result and no extra lookup; the fallback
  of storing the LOCALIZED label was not needed. MEASURED on `[Beginner] test`: the names are
  `PlanetAnomaly17`, `PlanetAnomaly27Alt`, `PlanetAnomalyNaturalWonder2`, `Luxury5`, `Luxury8`,
  `Strategic1`, `Strategic2`, `Strategic4` — content-authored ids, not display text.
- **Which COLUMN a kind is in is a fact about the galaxy, not about the taxonomy**: the derived
  columns are sorted by the localized title, so a saved selector naming a definition is resolved
  by finding a result carrying that internal name and then finding the column carrying that
  result's title. Two definitions that share one localized title share one column, and the second
  one's selector resolves to it — which is the right answer for a player who hears one word.
- **Composing every colonizable world's description up front costs nothing measurable.**
  `ScannerCost.Line()` on that fixture (12 star systems, 30 colonizability checks) reads
  **4–7 ms a press** with the descriptions composed eagerly, against the 30 ms the scanner warns
  at — so the lazy path the scanner had for them was an optimization of something that was never
  expensive, and it cost keywords the ability to see a world's type at all.
- **Definitions the game DRAWS with the same words are common**, and they are different keys: a
  luxury and its `System` twin (`Luxury15`/`SystemLuxury15`, both "Amianthoid") and an anomaly and
  its quality variants (`PlanetAnomaly23`/`PlanetAnomaly23Reduced`, both "Acid Rain"). The scanner's
  own found-columns dedupe by LABEL, and the editor MERGES the twins (owner ruling 2026-08-24): each
  merged column keeps every key its twins were defined under, and at SCAN time a saved selector is
  resolved through the databases to the words it is drawn with and matched against the found column's
  LABEL (`ScannerKindIndex`), which is what makes a category saved under either twin match.
- **The four kind databases, counted 2026-08-24**: `AnomalyDefinition` 108, `CuriosityDefinition`
  162 (12 distinct `DisplayedType`s), `ResourceDefinition` 123 — 24 `Luxury` + 24 `SystemLuxury`,
  6 `Strategic` + 6 `SystemStrategic`, and 63 the scanner never surfaces (46 `Common`, 11
  `LateCollect`, 3 `Gameplay`, 3 `Academy`). Every one of them resolves a localized title, so
  nothing is dropped for want of words. The editor's own column counts after the twin merge,
  measured the same day: Anomalies 82 (from 109), Curiosities 15 (unchanged), Luxury 25 (from 49),
  Strategic 7 (from 13) — 27, 24 and 6 pairs merged. **The two sets of pre-merge figures do not
  reconcile** (108 vs 109 anomalies, 12 vs 15 curiosity types, 48 vs 49 luxuries, 12 vs 13
  strategics) and nobody has since named what the editor's list holds beyond the database rows —
  re-derive both counts from one probe before quoting either.

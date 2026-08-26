# ES2 facts — research

The technology wheel, and the construction and research queues.
Index and charter: `README.md`.

## Research and the technology wheel

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
- **What the game recommends researching is a list, not just a badge.**
  `TechnologyScreen.SuggestedGuiTechnologies` (refilled in `Refresh` :393-398 from
  `DepartmentOfScience.SuggestedTechnologies`) is what `UpdateSuggestionTop` badges the dots from
  per frame; the game's word for one is `%SuggestedItemTitle` ("Suggested").
  `TechnologyItem2.UpdateSuggestionBottom` belongs to the notification windows'
  `SuggestedTechnologiesPanel` alone — nothing on the wheel calls it.
- **The research buy-out is a UNITED EMPIRE affinity ability, and this fixture cannot draw it.**
  The descriptor is `EmpirePointBuyoutUnlocked`, granted only by `AffinityGameplayTerrans`
  (`Public/Simulation/FactionTraits[Affinity].xml:238`); the TUTORIAL United Empire affinity
  `AffinityGameplayTerransTutorial` has it commented out (:296), which is why the save the mod is
  tested on reads `empirePointBuyoutUnlocked=False` and the game itself draws no button. It buys the
  HEAD of the research queue (`EmpireBanner.OnExecuteBuyout` :612-632 over `ResearchQueue.Peek()`),
  pays in Influence (`EmpireEmpirePoint`, cost `GetBuyoutCostWithBonus` :358-399), posts
  `OrderBuyoutTechnology` and is offered by `DepartmentOfTheTreasury.CanBuyoutTechnology` :2272. The
  BUTTON is on the empire banner's research line, not on the wheel — the mod declares it once, as
  `hud:empire/research-buyout` (`GlobalHud.AddResearchBuyout`), and deliberately does not mirror it
  onto the research screen (owner ruling 2026-08-22). Lumeris' buy-out is the DUST one
  (`IsBuyoutUnlocked`), which the banner puts no button on at all.
- **The game's own technology search is a substring filter with no cursor.**
  `TechnologyLookupPanel` (Ctrl+F parks focus in its field — `TechnologyScreen` :232-243) builds one
  keyword list per technology in `BindTechnology` (:41-73): the title, `TechnologyDefinition.
  GetLocalizedKeywords()`, and for every unlock whose `IPrerequisiteProvider` passes
  `PrerequisiteHelper.CheckPrerequisites(..., ConstructionFlags.UnlockAvailability)` that unlock's
  title, its keywords, and the localized titles of its `Category` and `SubCategory`. Matching
  (`DisplayMatches` :148-206) uppercases, splits on `" ,.;:-0123456789"` and AND-s a `Contains` test
  per term; a hit blinks a frame on the matching dots and writes a count. No ranking, no selection,
  no camera, no next/previous — which is why the mod does not delegate to it and copies the CORPUS
  instead. **Mod policy: the wheel is searched by what a technology GIVES, not only by its name**
  (2026-08-22). `ResearchScreen.TypeAheadScope` covers every dot the wheel would draw, wherever it is
  buried, and each one answers with its title and then, after a comma, the same terms this panel
  looks through: the technology's localized keywords, and for every unlock the empire already passes
  the `UnlockAvailability` prerequisites for, that unlock's title, its keywords and the localized
  titles of its category and sub-category. So "Impervious" finds Survival Suits and "Miners Union"
  finds Galactic Commodities Exchange, while `TypeAheadSearch`'s before-the-comma rule keeps a title
  match ahead of an unlock match. The terms are built once per technology and kept until the turn
  changes — which is when an unlock's prerequisites can move — so a ten-letter search composes 107
  strings, not 1070.
- **Aiming the wheel's viewport** takes a point measured from the middle of the wheel in the
  normalized (782-wide) space the stages place their dots in: `DoZoomIn(aim, 0.3f)` from the
  overview, `DoTranslate(aim * 4, 0.3f)` once `Viewport.GetComponent<GuiValueController>()
  .CurrentValue == 4` (both private; the controller is reachable off `Viewport`). A quadrant's own
  aim already exists as the game's `OnSectorClick` — call it through `ITechnologyQuadrantClient`
  rather than recomputing radius 195.5 at the sector's mid-angle.
- **Two controls that name the same backing object are one control to the cursor.**
  `ControlId.Reference` is followed before the structural key, so the research screen's queue row
  and its wheel dot both keyed on `GuiTechnology2` teleported the player into the queue panel the
  moment they queued something; the dot keys on `TechnologyDefinition` instead.
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
- **A technology's unlock icons are readable and pointable without the hover.**
  `TechnologyItem2.TechnologyUnlocksContainer` holds one `TechnologyUnlock<i>` per unlock the empire
  may see, each with the unlocked thing's own class-backed tooltip (`Constructible`, `ShipModule`,
  `EmpireImprovement`…) and its game title on the wrapper — present with the technology screen CLOSED.
  The container sits at alpha 0 until the dot is hovered, so the gate to use is the `Visible` FLAG and
  never the transparency. An unlock the empire's affinity hides is bound to no icon at all
  (`GuiTechnology2.TechnologyUnlocks` counts 3 where 2 are drawn), which is the mod's own "what the
  picture is not showing is not said" line for free. Pointing at an icon draws the FULL dossier —
  description, effects, **cost**, upkeep, political impact — which is the mouse parity the dot's own
  `TechnologyUnlockEmbedded` tooltip cannot give (that class has no cost panel by data design).

## The construction and research queues

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
- **Research has no cancel confirmation and construction has one — the two queues are the same shape
  everywhere else and differ exactly there.** `TechnologyScreen.DequeueTechnology` (:189-202) posts
  `OrderCancelResearch` unconditionally, while `StarSystemQueuePanel.OnCancelConstruction` (:425-442)
  branches on `Construction.IsAlreadyInvested` and raises the game's own message box first (the bullet
  above). So an outcome line for a cancel is honest on the research queue and on an uninvested
  construction, and must be withheld where the game has still to ask its own question.
- **Cancelling research has no progress precondition — the game takes it back at any point.**
  `DepartmentOfScience.CancelResearchPreprocessor` (:563-577) checks only that the entity exists and
  that `ResearchQueue.Contains` it; `CancelResearchProcessor` (:579-599) refunds the instant stocks.
  `TechnologyScreen.OnToggleTechnologyItem` (:203-229) un-toggles `InProgress` as readily as
  `Queued`, `TechnologyItem2.ComputeTechnologyState` (:535-536) keeps the toggle enabled while a
  technology is in progress, and `ResearchQueueItem.OnActivateCb` (:107-111) dequeues
  unconditionally. There is no cancel-confirmation string for research anywhere in the corpus (only
  constructions have one). **Measured live 2026-08-22** on `[Beginner] test` at turn 22, one turn
  into "Survival Suits": both of the mod's routes cancelled it and said so — the queue row
  (*"Cancelled Survival Suits"*, `ResearchQueue.Length` 1 → 0) and the wheel dot (the same line, then
  the dot's own state word flipping to "Available"). `TechnologyItem2.Dragging` read false
  throughout. So the reported "cancel with progress does nothing" does not reproduce; what WAS real
  is that both routes used to return silently when their own gate failed, which is a different
  defect and is fixed (`ResearchScreen.Dequeue` now says the technology's state instead; the wheel
  dot already answered through its `StateText` refusal part — verified: Enter on a "Not available"
  dot says *"Not available"*).
- **God mode re-purposes BOTH queue-removal buttons.** `ConstructionLine.OnCancelCb` (:378-392) buys
  the construction out instead of cancelling it, and `TechnologyItem2.OnToggleCb` (:734-745) unlocks
  the technology outright instead of queueing or dequeueing. Any announcement attached to those two
  controls has to ask `GodGalaxyCursor.IsGuiInGodMode()` before saying what the press did.
- **God-mode handlers are decoration for a normal player.** The list measured in the 2026-08-23/24
  coverage sweep: `EmpireBanner`'s three resource areas, `ResourceItem.OnClickCb`,
  `ColonyInfoSidePanel.OnUpkeepCb`, `ColonyPopulationSidePanel.OnHappinessGroupCb`,
  `TechnologyStageItem.OnUnlockStageCb` and `DeedItem2`'s `GodButton`, all guarded by
  `GodGalaxyCursor.IsGuiInGodMode()`.


# ES2 facts — the icon-strip screens

The senate, empire, economy and politics pages reached from the galaxy HUD icon strip.
Index and charter: `README.md`.

## The icon-strip screens (senate, empire, economy, politics)

- Government: **the Validate button's missing-technology hint is the LAST of three refusals**, so a
  save can refuse the change without ever lighting the hint. `GovernmentModalWindow.Refresh`
  :204-214 tries `GovernmentChangeLocked`, then `GovernmentChangeCooldown`, then
  `MustHaveTechnology`, and only the third reaches `FormatButtonHint` — under the first two the
  `GuiButtonHint` component is present with no technology in it and `IsHintActive` reads false
  (`GuiButtonHint.IsActive()` is exactly `GuiTechnology != null`). Under a hint the game's
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
- Empire, the side panels: `EmpireDescriptionSidePanel` hangs its tooltips on the LABELS inside each
  group and on each icon — never on the group itself, so a group-level read finds nothing.
  `EmpireStatusSidePanel.PanelTitle` is the ONLY side-panel heading in the family that carries a
  tooltip of its own.
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
- **The recipe window and the economy screen draw the same family grid** from the same
  `GuiResources` list; the recipe copy read `ExtendedGuiElement.Title` (the family DESCRIPTION,
  carrying the shipped icon typo "Improves Industry Food") while the economy copy maps
  `TargetEffect` to the game's short `%SubCategory…Title` words. One reader now, internal on
  `EconomyScreen` (2026-08-18).
- Economy, luxuries: **the luxury grid is a GRID** — the items cycle through 8 target effects with a
  period of 8, so the columns are the FIDSI families and a row read as a flat strip loses which
  family each figure belongs to.
- Economy, luxuries: **the grid is a 24-slot 3×8 lattice with TWO fades, and they mean different
  things** (measured 2026-08-19 on all 24 slots). `ResourceItem.Bind` sets alpha 1 for a luxury that
  `Exists` for this empire and 0 for one that does not; `ResourcesPanel.RefreshResourceItem`
  (:203-215) then multiplies by 0.3 when both stock and net are zero (`SoftHide`). So **alpha 0.3 =
  drawn, and the empire holds none of it** — true of an unlocated resource AND of a located-but-empty
  one alike (Eden Incense and Giga Lattice are both known and both at 0.3) — while **alpha 0 = a pool
  row the game is not drawing at all**, still carrying the previous bind's icon and tooltip class
  (nine of them read `Strategic01Small`/`StrategicResourceBanner`). A lattice line the game faded out
  entirely is not a row of eight empty cells, it is a line nobody can see: the mod reads one row per
  DRAWN line and says `nav.cell-empty` ("empty") in the faded cells of a line it does read.
- Economy and the development window: **one property gates BOTH strategic grids** —
  `SimulationProperties.Empire.CanUseStrategicForRecipe` (Material Expertise): `EconomyPanel.Refresh`
  sets `StrategicsGroup.Visible` from it every refresh, and the development window's strategic
  component grid derives from the same predicate. Unlike the descriptor-driven
  properties that shrug writes off, this one DOES stick under `SetPropertyBaseValue` +
  `Refresh(false)` — set it and the game draws the real grid itself, set it back and it is gone.
- Economy, luxuries: **a luxury the empire has not located is drawn ANONYMOUSLY, on purpose.**
  `GuiResource` (:108-133) substitutes a placeholder in every drawn thing about it: `GetName` →
  `UnknownLuxury`, `GetImage` → the single shared `UnknownLuxurySmall` texture, `GetColor` → the
  `UnknownLuxury` colour; `ResourceItem.SetTooltipProperties` clears the tooltip's class and target
  and writes `Gui.GetDescription(GuiResource.UnknownLuxuryName)` as its whole content. The model
  still knows the slot is `Luxury1`, so a mod reading the model would name a resource the screen
  refuses to show. **Mod policy**: the economy grid speaks that sentence and never the name.
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
- **RepresentativesStarSystemSidePanel draws its captions non-uniformly** (2026-08-18): one block's
  caption is a bare sibling title label, the other's sits INSIDE its group — the mod takes each
  block's caption as the topmost line the block produced, never by tree position.
- **A retired pooled line keeps its words and goes to alpha 0** — the population overview's
  Collection Effects block for a people with no reached threshold still holds a "Militarist" line
  at `Visible` true, `Alpha` 0, in a table of height 0 (measured 2026-08-22).
  `PopulationModalWindow.RefreshCollectionEffects` (:340-375) reserves the table to the effect count
  and refreshes it, which is what leaves the surplus behind. (The general rule is in
  `gui.md`.)
- **A collection threshold shows its state by BRIGHTNESS and nothing else.** `ThresholdItem.Bind`
  (:18-77) writes the number into `ThresholdMaxValue`, the bonus's effect lines (or
  `%PanelFeatureNoEffectsTitle` "No Effects") into `CircleTooltip.Content`, and sets
  `Circle.Alpha` to 1 when the ratio reached 1 and 0.3 otherwise. The arithmetic behind the fade is
  `SelectedGuiPopulation.GetCount() >= item.Threshold` — the same test
  `RefreshCollectionEffects` uses to decide which bonuses are in force — and
  `IGuiPopulation.CollectionBonuses[i].Threshold` is the number as an int, so the mod reads the
  fact rather than the drawing of it.
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
    so the shared kind rule ANNOUNCES that definition on landing on a winner — which is the
    blanket ruling (2026-08-28, `gui.md` §Tooltips): the card declares all three tooltips at
    their natural modes, the class-backed dossiers buffer-only, the content-backed experience
    sentence spoken whole. The earlier per-card muting of that sentence went with every other
    caller-chosen mode.
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


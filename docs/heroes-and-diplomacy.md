# ES2 facts — heroes and diplomacy

Heroes and the academy, and the diplomacy screen and its sweep.
Index and charter: `README.md`.

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
  stays true — the pooled-table alpha trap again (`gui.md`).
- Hero-card figure captions are `%HeroCardExperienceTitle` and friends; unspent points, cooldown
  and relics borrow `%HeroInspectionRemainingSkillPointsTitle`,
  `%AssignmentCooldownBaseDurationTitle` and `%HeroRelicTitle`.
- **A hero card keeps most of what it knows on tooltips INSIDE it, not on the card.** Measured on
  the recruitment prefab: eleven `AgeTooltip`s under one card, up to six levels down (the resolver's
  default reach of four finds only the ship). Nine earn a node — three class-backed definition lines
  (`HeroProperty` ×2, `Politics`), four `HeroSkillMastery` lines, two plain sentences
  (`%HeroCardExperienceDescription` over the gauge, `%HeroSkillsMasteryDescription` over the mastery
  heading) — plus `ShipGroup`, whose `ShipDesignHeroRecruitement` dossier is the hero ship's whole
  stat page (role, size, health, movement, offence/defence) and is the only place the game states it.
  The eleventh hangs on a `DummyHeroExperienceLine` the card keeps hidden. Prefab child order matches
  drawn order here (rects ascend down the card).
- **`HeroDetailedCard.HeroTooltip` is null on the recruitment AND inspection prefabs** — the
  card-whole "Hero" dossier is a band some prefabs simply do not draw, so a reader must treat it as
  optional rather than as the card's one tooltip.
- **`AcademyScreen.HeroCardsTable` has no children until the empire owns a hero** (measured on a
  turn-29 save whose first hero was still being offered): the strip is not merely tutorial-gated,
  it is empty, and so is every reading hung on its cards.
- `HeroSelectionModalWindow.Refresh` (:74-77) wipes `SelectedHero` through an inverted
  `Contains` — never cache it.
- **`%SkillTreeStageLevelTitle` + `RequiredLevel` is a per-RING unlock threshold**, drawn as a
  leader-line legend beside the ring — not a skill's name and not a per-branch total. Read as either
  it says the wrong thing about every skill on the ring.


## Diplomacy and the sweep

- The diplomacy ring draws UNMET majors; `LeaderCard` wires no control at all; the sector has no
  tooltip and a god-mode branch.
- **Every card is BOUND with real data whether the empire is met or not — what hides the unmet one is
  the `ContextMenu`'s ALPHA.** The screen fills each sector's detail widgets (`EmpireNameLabel`, the
  pressure group's `Title`/`Label`/`Description`, `DiplomaticStatusPressureLabel`, `AttitudeGroup`,
  the `ClickLabel` footer) from live simulation figures for every empire, and then raises that block
  on hover ONLY for a known, living one: `ShowContextMenu` / `HideContextMenu` gate on
  `IsKnown && !HasBeenEliminated` (decompiled `Assembly-CSharp/DiplomacyScreen.cs`:842-849), and every
  one of those widgets is a descendant of `ContextMenu`. So a reader that takes the labels as they are
  becomes the fog answering with facts the picture never shows. Measured 2026-08-27 on
  `[Beginner] access test`: the two unmet cards read `menuAlpha=0` with the mod's own cursor hovering
  one, and a crop of that card is the silhouette hologram alone — no name text, no diplomatic status,
  no pressure figure, no footer. **Mod policy**: an unmet or eliminated card says its label, its
  refusal sentence and its alert marker and nothing else — `DiplomacyScreen.Drawn(EmpireSector)`
  mirrors the game's own predicate and is asked per frame inside the part functions, so a first
  contact (or an elimination) changes what the card says without waiting for a rebuild.
  `AgeWidgets.Visible` still ignores alpha deliberately: the gate here is the game's own predicate,
  not a transparency test.
- **"Met" is one flag on the diplomatic relation**: `DiplomaticRelation.HasAbility(
  DiplomaticAbilityDefinition.Names.IsKnown)`, which is what `DepartmentOfForeignAffairs`'s own
  `HasMetAnyMajorEmpire` counts (`:194-205`). The mod's diplomacy-band empire list is that flag
  over the MAJOR empires plus the player, minus the eliminated (an eliminated empire holds no
  colony, so the lens has neither centre nor line to draw for it).
- **The diplomacy scan lens draws from ONE empire's records at a time.** `WatchingEmpire` is reset
  to the player every time the scan view opens (`DiplomacyScanViewWindow.OnBeginShow`) and is
  changed only by the swap toggle; every centre, link, relation icon and spoke on that band is then
  composed against THAT empire's intelligence rather than the player's. It persists across lenses
  within one scan session (the window is `Shown` at every rung), so the closer lenses — which draw
  no diplomacy at all — ask about the player instead. **Mod policy**: the diplomacy band's rows are
  composed against the watched empire; the owner headings at the closer lenses against the player.
- **An empire's CENTRE is `DepartmentOfIntelligence.GetEmpirePosition(e)`, `Known`-gated** — the
  home system where the watching empire has discovered it, else that empire's highest-influence
  colony they can see (`RefreshEmpirePosition` :479-535). The game draws the identical circle and
  link in both cases, so **the mod speaks a POSITION and never "home"** (owner ruling 2026-09-01):
  naming the case would hand a keyboard player a fact the picture withholds.
- **The band's curved SPOKES belong to the WATCHED empire and are gated by THAT empire's
  knowledge** (`GalaxyStarSystem.UpdateDiplomaticScanView` :900-983): one curve per colonized
  system of the watched empire where the colony is visible to them (`Visibility >= 1`) and the node
  is `Revealed` to them (`Exploration >= 4`), and never at their own home system, whose curve would
  have no length and is never shown. Verified live and optically 2026-09-01: pointed at Leaper the
  lens drew five curves, four of them at systems the PLAYER has never explored. So it is genuinely
  an intelligence tool for locating a watched empire's holdings, and the mod mirrors it exactly —
  naming a star only where the player's own knowledge names it.
- **The swap toggle is drawn in exactly one place**: inside the empire-name line of a diplomacy
  label standing at a MAJOR's home system, gated `ExplorationState >= 2 && IsMajorHomeSystem`
  (`ScanViewDiplomacyLabel.RefreshEmpireNameLine` :304-312); a battle-only label draws no line at
  all, and the toggle is switched OFF for the player's own empire and for whoever is already being
  watched (:310-311). The underlying mechanism works for any met empire (REPL-verified), and the
  mod still offers the swap only where the game draws it — parity, not caution.
- **The battle line is the encounter repository's**, not the node's fleet list:
  `RefreshBattles` walks `Encounters` for one in progress at this node whose group leaders have
  joined, and tints two emblems from `Groups[0]`/`Groups[1]`. `CollectFightingEmpires` (the docked,
  alive, in-encounter fleets) is what decides the label is drawn at all.
- **Closing an unsigned negotiation still posts an order**, and `EvaluationAnnotation` is
  discarded on the way.
- `AcademyModalWindow`'s Bind can WEDGE the window (recovery in test-recipes), and
  `PirateDiplomacy.Refresh` throws outright when there are no pirate systems.
- `Gui.FormatFailureInfos` returns the BASE text when every failure is ignorable — an empty-looking
  refusal that is really "nothing to report".
- The non-blocking box's countdown lives in the MESSAGE, not in a field of its own.
- `MetaplotBattleRulesIcon` lives INSIDE `HomeAndTradingTable` (child 10) — it was always read
  by the table walk; a field-by-field audit counts it as an unread field.
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
  `LesserEmpire` (`Lesser.Default`) and one `PirateEmpire` (`Pirate.Neutral`). (The map's own
  friend/neutral/enemy split is a different and cruder comparison — `fleets.md`.)
- **The minor-civilization window captions every band itself, and three of the captions carry a
  sentence.** `%MinorFactionDiplomacyModalWindowTitle` "Minor Civilization diplomacy" (+
  `…Description`), `…TraitsTitle` "Traits" (no description), `…RelationTitle` "Diplomatic Relation"
  (+ `…Description`), `…RelationRewardsTitle` "Relation Rewards" (none), `…RelationModifiersTitle`
  "Modifiers" (+ `…Description`), `…ActionsTitle` "Actions" (+ `…Description` — so the mod's own
  "Actions" word was never needed on THIS window, and the old `ModStrings.DiplomacyActionsBand`
  comment saying only the pirate window captions its band was wrong for all three: the Academy has
  `%AcademyDiplomacyModalWindowActionsTitle` and the pirates `%PirateDiplomacyModalWindowActionsTitle`).
  Per-figure titles: `%MinorFactionRelationTitle` "Relation", `%MinorFactionCurrentAllyTitle`
  "Ally", `%MinorFactionMajorTraitTitle` "Personality", `%MinorFactionMinorTraitTitle` "Faction
  trait". The one figure with NO title anywhere is the relation points and their trend, which
  `RefreshRelationInfo` composes into ONE label ("40 (+7/turn)"): `%TrendTitle` "Relation Points per
  Turn" names the trend alone and `%MinorFactionRelationTrendDescription` is a gloss on the line, so
  that row falls back to the gloss as its name (owner-accepted, 2026-08-22).
- **The relation gauge's band tooltips are prefab decoration with no caption and no wrapper, but
  each band's name is derivable twice over.** `MinorFactionDiplomacyModalWindow.
  GaugeTooltipsTransformList` holds four prefab segments named `Tooltip0/25/50/75`, laid along
  `GaugeLine` — measured 2026-08-22 at x = 0 / 66 / 133 / 200 across a 266-wide bar, i.e. 0 / 25 / 50 /
  75 on the same 0-100 scale the relation POINTS are on (33 points reads CORDIAL). `RefreshMinorRelationGauge`
  (:119-142) writes nothing to them at all, and `ToggleGaugeTooltips` (:287-293) hides the lot while
  at war. Each carries a Content-only tooltip whose key is
  `%DiplomaticRelationStateMinor<State>Description`, and the game's own title for the state is that
  key with `Description` swapped for `Title` (`%DiplomaticRelationStateMinorCordialTitle` =
  "CORDIAL"). Nothing in the game names them as bands, so the mod composes each name as "CORDIAL (25)" —
  the state's own Title key plus the segment's measured threshold
  (`test-recipes/modals-and-outgame.md`), the sentence announced after it under the kind
  rule. The Academy window has the identical field and the same gap.
- **The Academy's trend label carries a COMPUTED tooltip, not the gloss.**
  `AcademyDiplomacyModalWindow` :343-368 writes the eight per-turn contributions into
  `RelationTrendLabel`'s own tooltip, so the "outermost first, own last" tooltip walk finds a
  breakdown where the minor window finds a one-line gloss — which is why that row is captioned by
  the game's `%AcademyRelationPointsTitle` "Academy Relation points" rather than by the last-resort
  first-line rule. Its state label is left uncaptioned: the game keeps no title for it and the state
  word says what it is.
- **A one-sided contract is a `Declaration`, and the game deliberately draws NO approval for one.**
  `NegotiationModalWindow.RefreshDealApproval` (:980) only paints the deal-approval band when the
  contract holds terms AND `GetPropositionMethod() != Declaration`; otherwise it takes
  `StopAIEvaluationFeedback(reset: true)`, which hides both gauge halves and fades all five faces.
  Measured 2026-08-27 on a live cold war: two resource gifts from the player alone read
  `method=Declaration` with `EmpireWhichReceivesAIEvaluation.Evaluation == -0.0921` — the evaluation
  EXISTS and the screen shows nothing, and 409 frames of waiting never drew it. Putting a term on the
  other side flipped it to `method=Negotiation` and the bar appeared at once. So "the approval band
  is blank" is the game's own answer for an empty contract and for a one-sided demand or gift, not a
  missing reading; speaking the evaluation there would tell a screen-reader player something no
  sighted player can see.
- **The approval band names nothing.** The five faces (`AIEvaluationSmileys`, prefab-named
  `SmileyVeryAngry` … `SmileyVeryHappy`) are bare images: no label, no tooltip, and no `GuiElement`
  — swept 2026-08-27 across all 10,446 of them and across all ten localizations, where the string
  "smiley" appears nowhere and `%NegotiationModalWindowDealApprovalDescription` is the only
  `DealApproval` key in the game. What the game DOES compute is the bracket: `RefreshDealApproval`
  :985-992 turns the evaluation into an index 0-4 and lights that face by writing `Alpha = 1f`,
  leaving the rest at their own `FadeOnDisableFactor`. Reading the lit face is therefore reading what
  is drawn; naming the five is not available without inventing words.
- **A hero ship's hull tooltip can carry an unrelated glossary sentence, and it is the GAME's text.**
  Measured 2026-08-27: `ShipDesignOverviewPanel.HullTooltip` on the hero inspection page had
  `Content` = "Role: Support / Corporate\nThe term used by the Sower director to describe the way the
  Academy is managed." — a definition of a word, on a hull. The tooltip is attached to `HullLabel`,
  so nothing about the mod's aim is wrong; the game wrote that content. Read as drawn.

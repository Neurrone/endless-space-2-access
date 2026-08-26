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
  "CORDIAL"). Nothing in the game names them as bands, so the mod names each by its own sentence's
  first line. The Academy window has the identical field and the same gap.
- **The Academy's trend label carries a COMPUTED tooltip, not the gloss.**
  `AcademyDiplomacyModalWindow` :343-368 writes the eight per-turn contributions into
  `RelationTrendLabel`'s own tooltip, so the "outermost first, own last" tooltip walk finds a
  breakdown where the minor window finds a one-line gloss — which is why that row is captioned by
  the game's `%AcademyRelationPointsTitle` "Academy Relation points" rather than by the last-resort
  first-line rule. Its state label is left uncaptioned: the game keeps no title for it and the state
  word says what it is.

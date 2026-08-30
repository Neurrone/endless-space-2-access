# ES2 facts — military

The military screen, the ship designer, and battles.
Index and charter: `README.md`.

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
- **The "Projectile-Energy Balance" block draws TWO bars under ONE heading, and both bars take the
  same pair of side words.** `PanelFeatureShipInfo` (:59-61, :168-176) and
  `PanelFeatureMilitaryPowerBalance` (:5-11, :38-40) each own an `OffensiveBalanceGauge` (kinetic +
  missile against laser + beam) and a `DefensiveBalanceGauge` (plating absorption against shield
  absorption); `RepartitionHorizontalGauge.Refresh` writes no text, so the only words in the block
  are `%ShipStatMilitaryPowerBalanceTitle` over the pair. Both bars read out of
  `%ShipDesignProjectileTitle` / `%ShipDesignEnergyTitle`, so a reading that says only the split
  says the SAME LINE TWICE whenever the two bars sit at the same ratio ("Projectile 100%,
  Projectile 100%" on the hero-recruitment ship dossier, measured 2026-08-26).
  The distinguishing words the game does have are two sentences,
  `%OffensiveBalanceGaugeDescription` and `%DefensiveBalanceGaugeDescription` (Locales :7430,
  :7413) — no C# writes them; the PREFAB hangs each on its own gauge as a class-free
  `AgeTooltip.Content` (verified live on the drawn `PanelFeatureShipInfo`, reach 0, target null).
  That is why the ship designer's own copy of the pair needs no caption: each gauge is a stop
  whose short tooltip is announced, so it already reads "Energy 100%, The balance between the
  offensive power of…". Inside a tooltip's line list there is no such stop, so the line takes the
  name of the power it breaks down — which is also how the block is DRAWN: the offensive figure
  sits directly above the top bar and the defensive figure directly below the bottom one.
- **The ship tooltip's OTHER hover sentences stay unread (owner ruling 2026-08-26).**
  `PanelFeatureShipInfo` hangs a class-free sentence on nearly every stat group (health, movement,
  manpower, command points, the two powers, the two gauges, and the shared Projectile/Energy column
  icons — ten in all, measured live) — all static boilerplate, identical on every ship, so folding
  them into the tooltip's line list was measured, proposed and declined. Fighter/Bomber's keys
  (`%ShipStatFighterDescription`/`…Bomber…`) have NO string — the localizer echoes the key, a
  vanilla bug. The ship XP gauge reuses tooltip class `StarSystemHappiness` with no readable words
  and no wrapper: the one hover target on the page that stays mouse-only.
- **"Behemoth" in the game's fiction is `Juggernaut` in the code** — grep both spellings or half
  the family is invisible.
- **Every `GuiTableLine` in the game carries a `DoubleClickButton`** (`GuiTableLine.Bind` :96-99
  wires it to `OnLineDoubleClickCb` → the table client's `OnLineDoubleClick`) — measured: 14 lines
  live in `unlocked`, none without one. Only eight classes implement the handler
  (`StarSystemsManagementPanel` :434-441 opens that system's management page, `MilitaryScreen` :511
  shows the fleet on the map, `FleetSelectionModalWindow` :181 and `SystemSelectionModalWindow` :184
  pick and close, `LoadSaveModalWindow` :401 loads or overwrites, `JoinGameScreen` :435, and
  `MarketplaceBuyableItemsPanel` :354 and `HeroCompleteListModalWindow` :83 are EMPTY), and
  `FleetsScreen` is not among them. All eight read `GuiTable.SelectedLine`, never the line they were
  handed, so replaying the gesture means selecting the row first.


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
- **A watched battle is a REPORT being replayed, and the fight lives only in that stream.** The
  client plays one timestamped instruction at a time through
  `GalaxyEncounter.ParseReportInstruction` (:1184-1205, recursing into sub-instructions) and applies
  it to the model, which by the next frame remembers only the resulting STATE — so who shot whom,
  whether it landed, and what the shields ate are answerable only from the instructions. Three
  semantics measured on a real report and load-bearing for anything reading it: a MISS is
  `CreateSalvo.Miss` with no `Hit` following and no `Attack_Miss` event; a `Hit`'s damage is
  POST-mitigation and the shields' share is in its own sub-instructions as
  `DamageReceivedAbsorbedByShield` deltas, written once per accounting level and therefore read as a
  maximum; and `PhaseReports` can skip a phase index outright (measured 0, 1, 2, 4), so the game's own
  phase numbering has gaps in it. Everything else about the stream, with the counts, is
  `test-recipes/battles.md`, **What the fight says**.
- **The advanced report's morale badge is a GROUP fact drawn once per phase.**
  `AdvancedReportPhaseItem.Refresh` (:39-62) asks
  `EncounterGroup.GetPropertyValue(SimulationProperties.EncounterGroup.MoraleBonus)` of each side and
  stamps a `[happiness]` glyph tinted with that side's empire colour onto EVERY fought phase column —
  the same answer, repeated. So it is a fact about the side, not about the phase, and a reader that
  put it on each phase would be saying one thing N times (mod policy: one line per holding side, in
  that side's heading). The game names it: `%SpaceBattleMoraleBonusTitle` = "Morale bonus",
  `%SpaceBattleMoraleBonusDescription` = "A fleet gets a morale bonus when it has more active
  flotillas than its opponent." — the same pair `BattleStateGroupPanel` hangs on the in-battle badge
  as the `SpaceBattleMoraleBonus` GuiElement. But WHOSE bonus it is, the game says only in the
  glyph's tint, so the mod's line has to state it (`battle.your-morale-bonus` /
  `battle.enemy-morale-bonus`) and the game's title, read out, is a caption a listener cannot answer
  "mine or theirs?" from (owner-reported 2026-08-30). The description stays behind the line.
- **The advanced report binds the PLAYER subclass behind a base-typed field.**
  `AdvancedEncounterReportModalWindow.PlayerBattleGroupReportPanel` is declared
  `BattleGroupReportPanel` (:21), but the instance is `PlayerBattleGroupReportPanel` — the only
  panel with a `RewardsTable` and the three labels under it (experience gained, resources earned,
  salvage rescued). A reader that trusts the declared type loses all three; cast, do not trust the
  field. The enemy's field holds `EnemyBattleGroupReportPanel`, which has no rewards at all.
- **A damage bar's tooltip has a fourth feature nobody sees on an ordinary battle.** The
  `DamageGaugeCell` tooltip class's panel definition is `PanelFeatureHeader`,
  `PanelFeatureDescriptionGameplay`, `PanelFeatureSeparator`, `PanelFeatureAffectedByPlay`, and the
  last renders `%PanelFeatureAffectedByOnePlayDescription` / `…TwoPlaysDescription` off
  `GuiDamageData.AffectingPlayNames` — a list the game computes from the fought plans' modifiers
  against the bar's own properties (:99-152) and which is EMPTY for most bars, so the feature hides
  itself and a reader taking only Title and Description looks complete. Read a tooltip class's
  feature list off `GuiTooltipWindow.TooltipDescription.PanelFeaturesDescriptions` (each entry's
  `Prefab` names the feature) before concluding a tooltip has been fully read; `DevProbe.Tooltip()`
  reports only the features that RENDERED.
- **The advanced report's two roster panels are different shapes.**
  `BattleGroupReportPanel` (the player's) draws a `BattleFlotillasPanel` and one `FlotillaLine` per
  flotilla; `EnemyBattleGroupReportPanel` overrides `Refresh` to bind a single `BattleGarrisonPanel`
  and draws no flotilla line at all. The window nonetheless keeps arena cards for BOTH sides
  (`Player/EnemyFlotillaCard2DContainer`, three each, only the live flotilla's `Visible`), so the
  sentence naming a flotilla's optimal range has a roster row to live on for the player and none for
  the enemy. Unlike the ADVANCED SETUP window, where the enemy has no cards either. The
  fighter/bomber squadron cards are symmetric on the report, though: BOTH sides' arena containers are
  `EncounterPlayFlotillaCardContainer` with three `EncounterFighterBomberCard2D` each, where the
  setup window gives the enemy a single fleet-wide card.
- **A ground-battle outcome's second click is on the item's own transform.** Measured off the
  unbound prefab (`GroundBattleOutcomeSelectionNotificationWindow.OutcomeItemPrefab`, readable with no
  battle running): `GroundBattleOutcomeItem.Toggle` sits on the item's own `AgeTransform`, carries
  `UseDoubleClick` with `OnDoubleClickMethod = OnDoubleClickCb` (select AND validate, :74-79) and
  `OnSwitchMethod = OnToggleCb` (select only). That is exactly the shape the notification screen's
  choice reader already declares and already gives the double-click chord to, so the gesture is
  covered without a battle to run it on. A prefab's `AgeTransform` is null until it is instantiated —
  probe the fields, not the transform.
- **The ground setup popup's two aftermath badges are wired to each other's data.**
  `GroundBattleSetupNotificationWindow.Refresh` (decompiled :165-168) reads
  `ImprovementsDestructionByStrategies` into the value it assigns to `PopulationDeathLabel` and
  `PopulationDestructionByStrategies` into the one for `ConstructedDestroyedLabel`. The mod reads the
  drawn screen and keeps parity with the drawn association (owner call, 2026-08-25). Invisible in
  practice so far: every shipped tactic moves both by the same percentage.
- **A collapsed AGE accordion fades, it does not hide.** The DETAILS block on
  `GroundBattleContenderSetupPanel` keeps its three labels' text current unconditionally
  (`RefreshDetails`) and collapses through a height/alpha modifier: measured `Visible=true, Alpha=0`,
  and the game still draws their tooltips when the pointer lands on them. What the game truly
  withholds it sets `Visible=false` (the enemy side's health/damage multipliers, :177-178). So a
  `Visible` gate reads collapsed-but-live rows and still drops genuinely-hidden ones — and the mod
  deliberately reads the collapsed DETAILS rows without modelling the toggle (owner call,
  2026-08-25).
- **The ground power gauge's two numbers.** The dial is sized from
  `GroundBattle.SpawnReport.OpponentInitManPowers[Left/RightEmpireManpowerIndex]` (decompiled
  `GroundBattleSetupNotificationWindow.cs:152-154`). Both indices are `protected` on
  `GroundBattleNotificationWindow` — read them back by reflection rather than re-deriving the
  four-branch attacker/defender/third-party rule — and `NotificationGroundBattleSetup.GroundBattle`
  is the public route to the battle. The gauge's tooltip-bearing transform is
  `window.BattlePowerGauge.AgeTransform` (it IS the `PowerBalanceGroup`).
- **An impact arrow written INSIDE a number is a state marker, not a noun.** The game writes
  `[negativeImpactWhite]`/`[positiveImpactWhite]` into the assigned-manpower figure to mark a number
  its own rules moved (`GroundBattleContenderSetupPanel.cs:58,64`) and appends the explanation to the
  row's tooltip. The global icon rendering ("negative") is right everywhere else and wrong inside a
  figure — strip the marker contextually in that row's reading (`BattleNotifications.ManpowerReading`),
  never in the icon table.
- **Two prefabs sharing one panel class can caption the same field with different words.** The two
  ground popups share `GroundBattleContenderBasePanel`/`ManpowerLine` and one row prefab, but the
  REPORT prefab captions the manpower row "Remaining" where the setup prefab says "Assigned" — and
  neither panel class rewrites the caption (`GroundBattleContenderBasePanel.cs:78-79` sets only the
  line tooltips; the report panel only the values). The difference exists only in the prefab drawing.
  Mod policy it forced: name such a row from the DRAWN caption with the key as fallback
  (`BattleNotifications.RowTitle`). Reserve is the control case — its drawn label holds the very key
  the mod used, so the drawn read changes nothing there.
- **The ground report's outcome word and its meaning are two fields of one GuiElement**
  (`"EndBattleStatus" + GroundBattleResult`). The SPACE report wires the Description onto the title's
  tooltip (`BattleReportNotificationWindow.cs:262`); the GROUND report does not — the game ships a
  sentence the player can never see without the mod (`BattleNotifications.OutcomeDescription`
  resolves it, third-party branch included).
- **`DamageGauge.EffectiveDamageCells` stacks its blocks bottom-up relative to pool order**
  (`EffectiveDamageCell000` drawn below `…003`): pool index is not reading order for this container —
  emit its rows in drawn (top-down) order.
- **The outcome-selection popup's Confirm button is bound to no field.**
  `GroundBattleOutcomeSelectionNotificationWindow` draws a `ValidateButton` ("Confirm", in
  `ButtonsGroup`) wired by prefab name to `OnValidateCb` — the window class exposes it nowhere, so
  only a by-name lookup (`AgeWidgets.ChildNamed`) finds it. A `Body` variant loses the shared
  `Extras` sweep that used to catch it; the body declares it explicitly.
- **The ground-battle outcome countdown is multiplayer-only.** `GroundBattle.OutcomeTimeLimit` is
  stamped from the lobby setting `BattleOutcomeTimerDuration` (`GameServer.cs:2573`, default 0);
  single player never sets it, so `IsTimeLimited` is false, no gauge draws, and no default is
  auto-picked. The mod declares the countdown as a focus-announced node that exists only while the
  gauge is drawn (owner design 2026-08-25). `HackingOperation` has the same `OutcomeTimeLimit`
  mechanism.
- **The outcome popup's header markup resolves through the icon table**: `"[improvement] N"` →
  "Improvement N", `"N [wonder]"` → "N Wonder", `%None` → "None" — a word plus a number, no
  brackets, verified through the real `AgeText.Clean` off the unbound prefab.
- **A choice card is a title over a paragraph, and is named by its FIRST label.**
  `GroundBattleOutcomeItemNNN` carries its `AgeControlToggle` on the card itself and holds exactly
  two labels — `OutcomeTitle` and `OutcomeDescription`, the latter ONE label of up to six
  newline-separated consequence lines — which is why "all the labels joined" is the wrong name for
  a choice card. Mod policy (owner-approved 2026-08-25, all four choice families): the card
  announces its first label with the control parts; the rest reviews a line at a time
  (`NotificationScreen.Control.Drawn` — never `Control.Details`, which replaces the sections and
  would drop the card's refusal tooltip; the struct's doc comment carries the contract).
- **The battle model holds the ENDING while the battle is loading.** Every phase report is applied to
  the encounter as it arrives (`Encounter.OnPhaseReportReceived:960` → `ParsePhaseReport:678` →
  `ParseReportInstruction`, which calls `SetStatus` on the entity each instruction names), and the
  model is only rewound by `Encounter.RestoreEntitiesSimulation` in `GalaxyEncounter.Ignite`
  (`GalaxyEncounter.cs:1798`), which runs after the coroutine waits for `Encounter.State` to reach
  `Report`/`Finished` and just BEFORE `State = LoadingWaitForPlayer` (:1844). So for the whole
  `GalaxyEncounterState.Loading` window, `Groups[g].Flotillas[f].Status` and every ship's are the
  battle's FINAL state; at the pre-roll gate they are Alive again. Measured 2026-08-29 on the owner's
  Sabel fixture: at the wedge all 8 flotillas read `Alive`, while the phase reports carry exactly two
  flotilla `EntityStatus=Destroyed` instructions (guid 52, the player's empty reinforcement flotilla,
  `Index=0`, at t=26.6; guid 5, the pirates' only real flotilla, `Index=1`, at t=63.0 — the last event
  of the fight). Mod policy it forced: the loss, phase and progress tiers read the model only while
  the stream is being replayed (`SpaceBattleScreen.Playing` — Running/PreparingSkipping/Skipping),
  because before that the model is a spoiler, not a report of what has happened.
- **`BattleScreen.CurrentMode` is a dead property.** It is declared (`BattleScreen.cs:130`) and
  assigned by nothing in the whole assembly, so it reads `None` for the length of every battle. The
  act is `SwitchBattleDisplayMode` (:163-208), which shows exactly one of `BattleIntroductionPanel`,
  `BattleDiskPanel` and `BattleOutcomePanel` per act and hides the others — the panels' `Shown` IS the
  act, and it is what the player is looking at (`SpaceBattleScreen.Acting`).
- **The battle screen's location and opponent labels are written by one act only.**
  `RefreshLocationTitle`/`RefreshOpponentTitle` are called from nowhere but
  `SwitchBattleDisplayMode(Introduction)` (`BattleScreen.cs:166-169`), so before that act — and always
  while the window is not `Shown` — both hold the PREFAB's placeholders ("Battle at Antares", "Versus
  #FF8080#DeltaPattern", measured 2026-08-29). They are not an empty-label case, so an
  empty-means-fall-back guard never fires on them. The loading window is the surface that names the
  battle earlier: `BattleTitle` ("Battle at Sabel") is composed in `OnBeginShow` (:107) and the two
  `BattleGroupInfoPanel`s are bound left = the player's group, right = the enemy's (:135-136), so
  `RightBattleGroupInfoPanel.MainLeaderName` is the opponent ("Pirates").
- **A hacking outcome's countdown is REAL-TIME seconds** — 10/20/30/45 by outcome, not turns — and it
  auto-picks a default when it runs out, so the choice popup is one of the few surfaces where reading
  slowly changes the result. `PickHackingOperation` only raises its prompt where the node offers MORE
  than one operation (data-gated), so a single-operation node never shows the picker at all.


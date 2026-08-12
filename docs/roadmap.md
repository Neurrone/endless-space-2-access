# Roadmap — work remaining

Only work still to be done lives here (owner rule). What a fixture cannot show and how to
test a shipped screen: `docs/test-recipes.md`. Mechanisms: `docs/es2-facts.md`. Layers and
keys: `docs/interaction.md`. The shipped index at the bottom is pointers only — a row may
name a screen and its mod file, nothing else; notes about a shipped screen belong in the
files above.

## To build

- Notification variants awaiting a live sighting (baseline ships; upgrade per popup on
  sighting): election survey; ground-battle outcome selection; SimpleDescription-family
  members with own fields (alliance update, diplomatic relation change, constellation
  event, deed completed); the 8 header-less line-class tables (bailiff + its totals
  footer, law cancelled, population change, trading blockade, treaty cancelled, relics ×2,
  queue-empty); one-of-N semantics for hand-written choice popups (hero recruitment,
  ground-battle/hacking outcome pickers) + the hacking parameters sub-choice and countdown
  gauge; breakdown-toggle tables (damage/displacement/force-truce);
  DiplomaticInteractionNotificationWindow (MoodMessageLabel, NegotiationContributionPanel).
- Galaxy-label gaps: AcademyGroup bottom readout; KOTH score value; deposit
  exploited-state; constellation ownership bonus; pin-message editing.
- Assigned-governor side panel: `Special` case for its three bare readouts (needs a save
  with a governor).
- StarSystemPopulationModalWindow (tutorial-locked in the fixtures) — and verify whether
  the stage-6 population screen already covers the planet page's population-entry click.
- Skill-tree type-ahead: a TypeAheadScope so search reaches skills in collapsed branches.
- Modal-return cursor: closing any modal over the star system page lands on the planets
  stop's start node, not the opening button (pre-existing; improvements/rename too).
- Departing-fade stand-down: the spurious "unavailable" frame when a game confirmation
  opens over a mod screen (general fix).
- SidePanels drops the panel title's own explanatory tooltip (generic fix, three screens).
- SettingRows editors end in silence: no watcher notices the game's field letting go, so a
  committed or cancelled settings edit re-reads nothing (the rename box now does this
  right; hoist its field-released re-read into the shared editor).
- Empire page as a second drop client for population moves (if wanted).
- Event narration (turn events) via `IEventService.EventRaised`.
- Real models for the minimum-pass pages: Mods, Credits/DLC/disclaimer, export, multiplayer
  join (multiplayer deferred until single-player is solid).
- Rebindable mod keys (long-standing, from input.md).

## To decide (owner)

- Space over-claim → conditional hand-back, now the scan lens is modelled and announces
  itself (`InputAction.ClaimedWhile` was kept for exactly this).
- Tab-at-edge silence: edge cue or wrap.
- Cutscene keys: the passive announcer claims keys the page's own press-anything
  affordance needs (Enter-to-skip lost).
- Click parity when the game's own click is a bug: stated exception path or strict parity.
- The galaxy's remaining world-navigation model — lanes as routes and an answer to "what is
  near me" — still wanted, or does the map-as-tree cover it?

## Shipped (pointers only)

| Screen (game) | Mod file (`ES2Access/Screens/` unless noted) |
|---|---|
| Main menu / options / load-save / loading | MainMenuScreen, OptionsScreen, LoadSaveScreen, LoadingScreen |
| New game lobby / advanced / faction / custom faction / tutorial choice | NewGameScreen, AdvancedSettingsScreen, FactionChoiceScreen, CustomFactionScreen, TutorialSelectionScreen |
| Notifications (all variants) / tutorial | NotificationScreen (+NotificationBody, BattleNotifications), TutorialScreen |
| Galaxy map + HUD / fleet panel | GalaxyHudScreen, GlobalHud, FleetPanel |
| Star system / improvements / system-selection / politics / rename | SystemManagementScreen, ImprovementsModalScreen, SystemSelectionScreen, SystemPoliticsScreen, RenameModalScreen |
| Planet / planet constructibles / discovery cutscene | PlanetOverviewScreen, PlanetConstructiblesScreen, SystemDiscoveryScreen |
| Research / quest journal | ResearchScreen, QuestJournalScreen |
| Scan view | ScanViewScreen |
| Senate / government / laws / population / election | SenateScreen, GovernmentScreen, LawsScreen, PopulationScreen, ElectionScreen |
| Empire / economy / recipe | EmpireScreen, EconomyScreen, RecipeCreationScreen |
| Military / fleet-selection / ship designer / troops / tactics deck | MilitaryScreen, FleetSelectionScreen, ShipDesignScreen, TroopManagementScreen, BattleTacticsScreen |
| Academy / hero selection / hero inspection / hero list / academy modals | AcademyScreen, HeroSelectionScreen, HeroInspectionScreen, HeroCompleteListScreen, AcademyModalScreen, AcademyDiplomacyScreen |
| Battles: setup/report popups, cinematics, advanced report/setup/plays | SpaceBattleScreen, GroundBattleViewScreen, AdvancedBattleReportScreen, AdvancedEncounterPlayScreen, BattleNotifications |
| Diplomacy / negotiation / minor / pirate | DiplomacyScreen, NegotiationScreen, MinorFactionDiplomacyScreen, PirateDiplomacyScreen |
| Target pickers / cutscenes / victory trio / journal | TargetSelectionScreen, CutsceneScreen, VictoryScreen, VictoryAchievedScreen, JournalScreen |
| Dialogs: message box / error / non-blocking / game menu / drop list | MessageBoxScreen, ErrorScreen, NonBlockingMessageScreen, GameMenuScreen, DropListScreen |
| Menu floors (minimum pass) | MenuDestinationScreens |

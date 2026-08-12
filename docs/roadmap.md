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
  event, deed completed); the 9 header-less line-class tables (bailiff + its totals
  footer, law cancelled, population change, trading blockade, treaty cancelled, relics ×2,
  queue-empty, lost-roots connectivity); one-of-N semantics for hand-written choice popups
  (hero recruitment, ground-battle/hacking outcome pickers) + the hacking parameters
  sub-choice and countdown gauge; breakdown-toggle tables (damage/displacement/force-truce);
  DiplomaticInteractionNotificationWindow (MoodMessageLabel, NegotiationContributionPanel).
- Galaxy-label gaps: AcademyGroup bottom readout; KOTH score value; deposit
  exploited-state; constellation ownership bonus; pin-message editing.
- Assigned-governor side panel: `Special` case for its three bare readouts (needs a save
  with a governor).
- Verify whether the population screen covers the planet page's population-entry click
  (the window itself, StarSystemPopulationModalWindow, is supported — SystemPoliticsScreen
  binds it; 2026-08-12 census).
- Skill-tree type-ahead: a TypeAheadScope so search reaches skills in collapsed branches.
- Modal-return cursor: closing any modal over the star system page lands on the planets
  stop's start node, not the opening button (pre-existing; improvements/rename too).
- Notification arrival-focus race: a popup's first build can run before its description
  label is visible, landing arrival focus on the first control instead of the words (why
  the elimination sentence had to ride the screen name).
- `screen.empire.Build threw: Cannot begin a stop inside an open row`
  (`EmpireScreen.BuildTabs`) — seen once in an earlier session's ring; empties the whole
  empire page when it fires. Reproduce and fix.
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
- DisclaimerModalWindow has no floor (the census's one uncovered non-DLC window besides
  the saving spinner): retail reachability is only the weak-GPU boot popup — decide
  whether a boot-time floor is worth it.
- ContextualPromptWindow: every show site is hacking — rides the Penumbra wait
  (2026-08-12 census).
- **Expansion surfaces — UNSIGHTABLE here until the DLCs are installed** (2026-08-12 audit,
  `audit-dlc-mechanics.md` at the repo root; none of the four expansions has a depot in
  this install, so every item below is code-verified at best):
  - Behemoth family (Supremacy): the specialization modal's REAL model when sightable
    (floor shipped at layer 29 — cards as radios, resources as readouts; deferred there:
    cards-as-one-row vs several, strategic-resource naming, the GuiButtonHint on a
    blocked card); the rest of the family when sightable.
  - Hacking subsystem (Penumbra): dashboard, processing-power/operations banner, traitors
    banner, program panel, operation route-building — one large stage, NOT to be written
    blind; wait for the DLC.
  - Traitor victim-side actions (Reveal/Kill/Remove in the population side panel) +
    pirate-lair and destroyed-planet orbital labels — small, shares the fixture wait.
  - The report-family breakdown toggle (Damage/Displacement/IonWave/ObliteratorVictim/
    PirateMission reports): a caption-less icon the shared caption rule drops — harmless
    only if the collapsed panel keeps its tables Visible, UNVERIFIED with a real report.
  - Umbral Choir ghost-system page (Penumbra): the Ghost state is a THIRD mode of the
    star system page beside Outpost and Colony — two side panels of bare-icon controls +
    a growth gauge.
- Riftborn time bubble on the galaxy map: a `GalaxyEntity` with a disk renderer — no
  widget, no label window; making it discoverable is real map-tree modelling.
- Faction sightings needing a non-UE base-game save (code landed drawn-gated, unsighted):
  Lumeris — tech buy-out on the HUD research line (+ scrap↔sell swap rows); Cravers —
  planet depletion status + hunting-grounds decay; Vodyani — ark-as-colony walk
  (`ExploitedStarSystem`: does the model need to change at all?), ark docking slots as a
  drag target, ark population (full list: `audit-factions.md` §4 stages C-D).

## To decide (owner)

- Space over-claim → conditional hand-back, now the scan lens is modelled and announces
  itself (`InputAction.ClaimedWhile` was kept for exactly this).
- Tab-at-edge silence: edge cue or wrap.
- Cutscene keys: the passive announcer claims keys the page's own press-anything
  affordance needs (Enter-to-skip lost).
- Click parity when the game's own click is a bug: stated exception path or strict parity.
- The galaxy's remaining world-navigation model — lanes as routes and an answer to "what is
  near me" — still wanted, or does the map-as-tree cover it?
- Cursor target modes have no keyboard confirm (GlobalHud announces the mode and stops):
  one gesture would serve probe launch, take-system, obliterator fire, pirate mark, time
  bubble and the hacking cursors. Needs an owner-approved binding.

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

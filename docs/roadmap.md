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
  sub-choice and the outcome COUNTDOWN gauge (real-time seconds, auto-picking a default when it
  runs out — es2-facts; needs a `Variant` hook plus a live sighting); the breakdown-toggle
  popups' tables (damage/displacement/force-truce — the toggle itself is vestigial, below);
  DiplomaticInteractionNotificationWindow (MoodMessageLabel, NegotiationContributionPanel).
- Galaxy-label gaps: constellation ownership bonus; pin-message editing.
- Scan management-lens remainder (unverifiable at turn 1): the trade-quality dial
  (geometry-only rating, no tooltip), the empire-rank bar graph + global-rank histogram,
  the ghost/traitor lines.
- Targeting-cursor remainder (Escape-on-TakeSystem and armed-Backslash SHIPPED, 30d23f9):
  `HonorActionCursor` fleet/docking targets need a `ConfirmAt` overload for fleet
  cursor-targets; `HackingOperationCursor` route building rides the Penumbra wait; the
  instruction banner can speak the previous mode's caption for a frame (cheap fix: skip
  an instruction that is not the current cursor's).
- Chat: the recipient tabs ship (`ChatCluster`), but MP-fixture verification of the whole
  cluster — tabs, new-message button, alliance sending — is outstanding.
- Assigned-governor side panel: `Special` case for its three bare readouts (needs a save
  with a governor).
- The planet page's population-entry click has no opener node yet (the window itself,
  StarSystemPopulationModalWindow, is covered — SystemPoliticsScreen binds it).
- Skill-tree type-ahead: a TypeAheadScope so search reaches skills in collapsed branches.
- Modal-return cursor: closing any modal over the star system page lands on the planets
  stop's start node, not the opening button (pre-existing; improvements/rename too).
- ReadCell cells on EmpireScreen/MilitaryScreen/SystemSelectionScreen can say
  "unavailable" twice (own part + the shared tail); the split-cell fix exists
  (`Adorn(availability:false)`) but SystemSelectionScreen's combo needs care — a refused
  row's word must survive.
- `screen.victory` announces the raw key `%VictoryScreenPlayingPlayerTitle` — the mod's
  lookup does not resolve it even though the drawn label localizes (the AGE
  draw-time-localization trap, live-caught).
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
- A real model for the resource exporter, the one out-game page still on the shape floor: the
  resource list and the export itself.
- Rebindable mod keys (long-standing, from input.md).
- The contextual prompt's component tables: modelled from the four data-defined shapes, but no
  fixture draws a table with ROWS — re-measure when one can be sighted.
- `StockAndNet` now exists in three copies (GlobalHud, EconomyScreen, JuggernautSpecializationScreen)
  — hoist the visibility-correct one and drop the others.
- **Expansion surfaces — UNSIGHTABLE here until the DLCs are installed** (2026-08-12 audit,
  `audit-dlc-mechanics.md` at the repo root; none of the four expansions has a depot in
  this install, so every item below is code-verified at best — except where the `*_DLC*`
  datatables alone are enough to bind a window, which is how the Behemoth modal got measured):
  - Behemoth family (Supremacy): the specialization modal is a MODEL now, not a floor (layer 29 —
    the datatables load unowned, so the three specializations and the six named resources were
    measurable here). What still waits for the DLC is only what needs a real Behemoth: a TAKEABLE
    card, Confirm, and the toolbar route into the modal; then the rest of the family.
  - Hacking subsystem (Penumbra): dashboard, processing-power/operations banner, traitors
    banner, program panel, operation route-building — one large stage, NOT to be written
    blind; wait for the DLC.
  - Traitor victim-side actions (Reveal/Kill/Remove in the population side panel) +
    pirate-lair and destroyed-planet orbital labels — small, shares the fixture wait.
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

- Rename Confirm's caption: mod key vs the game's own `%MessageBoxValidateTitle` (Cancel
  now uses the game's).
- The report family's breakdown toggle (IonWave and friends) is DECLARED NOWHERE: it is
  vestigial — `ReportPanel` carries no `AgeModifier`, so it animates nothing (es2-facts).
  Shipped that way; overrule if it should be offered anyway.
- The drawn-heading lookup renames two out-game pages: "Multiplayer room" and "Asset export".
  Keep the game's drawn headings or the mod's older names?

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
| Contextual prompt / Behemoth specialization | ContextualPromptScreen, JuggernautSpecializationScreen |
| Out-game pages: disclaimer / credits / DLC browser / mod manager / join game (export still a floor) | DisclaimerScreen, CreditsScreen, DLCScreen, ModdingConfigScreen, MenuDestinationScreens |

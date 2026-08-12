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
- Galaxy-label gaps: constellation ownership bonus; pin-message editing.
- Scan management-lens remainder (unverifiable at turn 1): the trade-quality dial
  (geometry-only rating, no tooltip), the empire-rank bar graph + global-rank histogram,
  the ghost/traitor lines.
- Navigation defect: `ui.end` from an EXPANDED GROUP node on the scan content stop landed
  on another stop's last node (hud:turn's Game menu) — End crossed a stop boundary; from a
  child node it behaves. Reproduce and fix in the shared navigation.
- Targeting-cursor remainder: `TakeSystemCursor` gets Escape-to-cancel (owner-ruled
  2026-08-12, awaiting go-ahead to implement); Backslash while a mode is armed should be
  the mode's right-click (cancel / waypoint removal — follows from the owner's
  any-click-parity correction); `HonorActionCursor` fleet/docking targets need a
  `ConfirmAt` overload for fleet cursor-targets; `HackingOperationCursor` route building
  rides the Penumbra wait; the instruction banner can speak the previous mode's caption
  for a frame (cheap fix: skip an instruction that is not the current cursor's).
- Chat: the alliance tab's SENDING is pointer-only (incoming lines narrate); MP-fixture
  verification outstanding.
- Assigned-governor side panel: `Special` case for its three bare readouts (needs a save
  with a governor).
- Verify whether the population screen covers the planet page's population-entry click
  (the window itself, StarSystemPopulationModalWindow, is supported — SystemPoliticsScreen
  binds it; 2026-08-12 census).
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

- Should collapsing a system un-zoom? (Shipped: no — Backslash is the way out and both
  tiers stay reachable with the branch open; guarded alternative: un-zoom only if the
  camera is still on that system.)
- Should the GALAXY view also get the scan view's Zoom node? (There zoom only changes how
  much is drawn, not the subject.)
- Rename Confirm's caption: mod key vs the game's own `%MessageBoxValidateTitle` (Cancel
  now uses the game's).

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

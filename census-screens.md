# Window census — complete universe, one verdict per class (2026-08-12)

## Method and accounting

**Enumeration.** Transitive subclass closure over every class declaration in
`decompiled/` (all three assemblies), scripted (classes.tsv: 5137 class declarations
parsed). Seeds and what they found:

- `Amplitude.Unity.Gui.GuiWindow` (firstpass) → **191 classes** (via the ES2
  global-namespace `GuiWindow` subclass; includes `GuiScreen`, `GuiModalWindow`,
  `NotificationWindow`, scan-view, and labels families). Note: the brief's
  `GuiScreenWindow` / `GuiPanelWindow` do not exist in ES2 — the real bases are
  `GuiScreen` and `GuiModalWindow`, both inside this closure.
- `AgeScreen` (firstpass) → **1 class** (`GuiGalaxyScreen`); plus the engine bases
  `AgeScreen` and `AgeDispatchScreen` themselves = 3 rows.
- `DebugUIWindow` (a `Behaviour`-based OnGUI debug console family, discovered by the
  *Window.cs filename sweep, outside the AGE GUI) → **62 classes** + the base = 63 rows.

**Registration cross-check (live).** The game's `GuiManager.guiWindowsFromBackToFront`
was read over the dev server: 170 window instances, 169 unique class names
(`LoadSaveModalWindow` has two instances: `OutGameLoadModalWindow` + in-game). Every
live-registered class is inside the closure — **zero classes the closure missed**. The
22 closure classes NOT live-registered are exactly the abstract/pure bases plus the 5
dead-code windows called out below.

**Coverage cross-reference.** (1) `ES2Access/ModEntry.cs` registrations (67 mod
screens); (2) each `ES2Access/Screens/*.cs` window binding; (3) windows read inside
other screens (`GlobalHud` reads `UserInstructionsWindow`/`GameOverlayWindow`/
`PinnedQuestWindow`/`NotificationItemsWindow`/`EndTurnWindow`; `GalaxyHudScreen` reads
the labels windows; `NotificationScreen` discovers the ENTIRE notification family
generically via `GetComponentsInChildren<NotificationWindow>(true)`
(NotificationScreen.cs:3532) plus a per-type variant registry (:2453)).

**Accounting: 257 total = 161 SUPPORTED + 1 WORK NOW + 3 DEFERRED + 68 UNREACHABLE + 24 EXCLUDED.**
(191 GuiWindow closure + 3 AgeScreen family + 63 DebugUI family = 257; sums per table below.)

## SUPPORTED (161)

Mechanism key: **own** = a mod screen binds this window; **NS** = NotificationScreen's
generic discovery + baseline body (variant registry upgrades where noted); **read** =
read inside another mod screen; **floor** = minimum-pass shape floor
(MenuDestinationScreens / WindowShape). Mod files are under `ES2Access/Screens/` unless
noted. "(variant row)" = the roadmap's notification-variants row also tracks a per-popup
upgrade on live sighting — baseline ships today, so these stay SUPPORTED.

| Class | Mechanism | Mod file |
|---|---|---|
| AcademyDiplomacyModalWindow | own | AcademyDiplomacyScreen.cs |
| AcademyDiscoveredNotificationWindow | NS | NotificationScreen.cs |
| AcademyModalWindow | own | AcademyModalScreen.cs |
| AcademyRequestNotificationWindow | NS | NotificationScreen.cs |
| AcademyRoleNotificationWindow | NS (named in variant registry) | NotificationScreen.cs |
| AdvancedEncounterPlayModalWindow | own | AdvancedEncounterPlayScreen.cs |
| AdvancedEncounterReportModalWindow | own | AdvancedBattleReportScreen.cs |
| AdvancedSettingsModalWindow | own | AdvancedSettingsScreen.cs |
| AllianceUpdateNotificationWindow | NS (variant row) | NotificationScreen.cs |
| BailiffReportNotificationWindow | NS (variant row) | NotificationScreen.cs |
| BattleLoadingWindow | read | SpaceBattleScreen.cs |
| BattleReportNotificationWindow | own | BattleNotifications.cs |
| BattleScanViewWindow | own | ScanViewScreen.cs |
| BattleScreen | own | SpaceBattleScreen.cs |
| BattleSetupNotificationWindow | own | BattleNotifications.cs |
| ColonizationCutsceneModalWindow | own | CutsceneScreen.cs |
| ConstellationEventNotificationWindow | NS (variant row) | NotificationScreen.cs |
| ConstellationLabelsWindow | read (map-as-tree constellation lines, GalaxyHudScreen.cs:599,863; residual "constellation ownership bonus" on roadmap) | GalaxyHudScreen.cs |
| ConstructionCompletedNotificationWindow | NS | NotificationScreen.cs |
| ConstructionQueueEmptyNotificationWindow | NS (variant row) | NotificationScreen.cs |
| ContextualAcademyDiplomaticExchangeUpdateNotificationWindow | NS | NotificationScreen.cs |
| ContextualDiplomaticExchangeUpdateNotificationWindow | NS | NotificationScreen.cs |
| ContributionWarningNotificationWindow | NS | NotificationScreen.cs |
| CoordinationRequestLabelsWindow | read | GalaxyHudScreen.cs |
| CreditScreen | floor (real model on roadmap minimum-pass row) | MenuDestinationScreens.cs |
| CuriosityDiscoveredNotificationWindow | NS | NotificationScreen.cs |
| CutsceneModalWindow | own | CutsceneScreen.cs |
| DLCModalWindow | floor (roadmap minimum-pass row) | MenuDestinationScreens.cs |
| DeedCompletedNotificationWindow | NS (variant row) | NotificationScreen.cs |
| DefenseHackingProgramEncounteredNotificationWindow | NS (content Penumbra; sighting DLC-blocked) | NotificationScreen.cs |
| DiplomacyScanViewWindow | own | ScanViewScreen.cs |
| DiplomacyScreen | own | DiplomacyScreen.cs |
| DiplomaticInteractionNotificationWindow | NS (variant row names it: MoodMessageLabel etc.) | NotificationScreen.cs |
| DiplomaticRelationChangeNotificationWindow | NS (variant row) | NotificationScreen.cs |
| DisplacementReportNotificationWindow | NS (breakdown-toggle variant row) | NotificationScreen.cs |
| DockLabelsWindow | read | GalaxyHudScreen.cs, UI/FleetPresence.cs |
| DragDropWindow | read (drag ghost; deterministic drag model) | UI/ShipDesignRows.cs |
| EconomyScanViewWindow | own | ScanViewScreen.cs |
| EconomyScreen | own | EconomyScreen.cs |
| ElectionModalWindow | own | ElectionScreen.cs |
| ElectionSurveyNotificationWindow | NS (variant row: "election survey") | NotificationScreen.cs |
| EmpireEliminatedNotificationWindow | NS | NotificationScreen.cs |
| EmpireIntroductionNotificationWindow | NS | NotificationScreen.cs |
| EmpireScreen | own | EmpireScreen.cs |
| EmpireScreenInformationNotificationWindow | NS | NotificationScreen.cs |
| EndTurnWindow | read | GlobalHud.cs, GalaxyHudScreen.cs |
| ErrorModalWindow | own | ErrorScreen.cs |
| EventOnFleetNotificationWindow | NS | NotificationScreen.cs |
| FactionChoiceModalWindow | own | FactionChoiceScreen.cs, CustomFactionScreen.cs |
| FactionSwitchedNotificationWindow | NS | NotificationScreen.cs |
| FleetLabelsWindow | read | GalaxyHudScreen.cs, UI/FleetPresence.cs |
| FleetSelectionModalWindow | own | FleetSelectionScreen.cs |
| FleetsScreen | own | FleetPanel.cs (+GalaxyHudScreen, GlobalHud) |
| ForceTruceAnsweredNotificationWindow | NS (breakdown-toggle variant row) | NotificationScreen.cs |
| ForceTruceProposedNotificationWindow | NS (breakdown-toggle variant row) | NotificationScreen.cs |
| GameMenuModalWindow | own | GameMenuScreen.cs |
| GameOverlayWindow | read | GlobalHud.cs |
| GovernmentModalWindow | own | GovernmentScreen.cs |
| GroundBattleOutcomeSelectionNotificationWindow | NS (one-of-N variant row) | NotificationScreen.cs |
| GroundBattleReportNotificationWindow | own | BattleNotifications.cs |
| GroundBattleScreen | own | GroundBattleViewScreen.cs |
| GroundBattleSetupNotificationWindow | own | BattleNotifications.cs |
| GroundBattleTargetSelectionModalWindow | own (GroundTargetSelectionScreen) | TargetSelectionScreen.cs |
| GroundTroopManagementModalWindow | own | TroopManagementScreen.cs |
| GuiTooltipWindow | read (tooltip pipeline) | UI/DrawnTooltip.cs, UI/PointerFocus.cs, UI/TooltipFeatures.cs |
| HackingOperationOutcomeSelectionNotificationWindow | NS (one-of-N variant row; Penumbra sighting blocked) | NotificationScreen.cs |
| HangarLabelsWindow | read | GalaxyHudScreen.cs |
| HeroCompleteListModalWindow | own | HeroCompleteListScreen.cs |
| HeroInspectionModalWindow | own | HeroInspectionScreen.cs |
| HeroRecruitmentNotificationWindow | NS (one-of-N variant row) | NotificationScreen.cs |
| HeroSelectionModalWindow | own | HeroSelectionScreen.cs |
| HeroUpdateNotificationWindow | NS | NotificationScreen.cs |
| ImprovementsManagementModalWindow | own | ImprovementsModalScreen.cs |
| InformationNotificationWindow | NS | NotificationScreen.cs |
| IonWaveReportNotificationWindow | NS (report-family toggle; DLC verification on roadmap) | NotificationScreen.cs |
| JoinGameScreen | floor (real model on roadmap: multiplayer join) | MenuDestinationScreens.cs |
| JournalModalWindow | own | JournalScreen.cs |
| JuggernautSpecializationModalWindow | own (floor at layer 29; REAL model DLC-blocked, roadmap Behemoth row) | JuggernautSpecializationScreen.cs |
| LawBaseNotificationWindow | NS (live-registered concrete despite the name) | NotificationScreen.cs |
| LawCancelledNotificationWindow | NS (variant row) | NotificationScreen.cs |
| LawsManagementModalWindow | own | LawsScreen.cs |
| LoadSaveModalWindow | own (covers both instances: in-game + OutGameLoadModalWindow, LoadSaveScreen.cs:1192) | LoadSaveScreen.cs |
| LoadingWindow | own | LoadingScreen.cs |
| LodestoneRewardNotificationWindow | NS | NotificationScreen.cs |
| LostRootsConnectivityNotificationWindow | NS (variant row) | NotificationScreen.cs |
| LuxuryDiscoveredNotificationWindow | NS | NotificationScreen.cs |
| MainMenuScreen | own | MainMenuScreen.cs |
| MessageBoxNonBlockingWindow | own | NonBlockingMessageScreen.cs |
| MessageBoxWindow | own | MessageBoxScreen.cs |
| MetaplotBegunNotificationWindow | NS | NotificationScreen.cs |
| MetaplotFinishedNotificationWindow | NS | NotificationScreen.cs |
| MilitaryScreen | own | MilitaryScreen.cs |
| MinorEmpireMetNotificationWindow | NS | NotificationScreen.cs |
| MinorFactionDiplomacyModalWindow | own | MinorFactionDiplomacyScreen.cs |
| ModdingScreen | floor (roadmap minimum-pass row: Mods) | MenuDestinationScreens.cs |
| NarrativeEventBegunNotificationWindow | NS | NotificationScreen.cs |
| NarrativeEventCompletedNotificationWindow | NS | NotificationScreen.cs |
| NarrativeScreen | own | QuestJournalScreen.cs |
| NegotiationModalWindow | own | NegotiationScreen.cs |
| NewDownloadableContentNotificationWindow | NS | NotificationScreen.cs |
| NewGameScreen | own | NewGameScreen.cs |
| NewUnlockedContentNotificationWindow | NS | NotificationScreen.cs |
| NotificationItemsWindow | read | GlobalHud.cs |
| ObliteratorAttackReportNotificationWindow | NS (report family) | NotificationScreen.cs |
| ObliteratorProjectileLabelsWindow | read | GalaxyHudScreen.cs |
| ObliteratorVictimReportNotificationWindow | NS (report family) | NotificationScreen.cs |
| OptionsModalWindow | own | OptionsScreen.cs |
| OutpostToColonyNotificationWindow | NS | NotificationScreen.cs |
| PinnedQuestWindow | read | GlobalHud.cs |
| PirateDiplomacyModalWindow | own | PirateDiplomacyScreen.cs |
| PirateMissionReportNotificationWindow | NS (report family) | NotificationScreen.cs |
| PlanetDestroyedNotificationWindow | NS | NotificationScreen.cs |
| PlanetLabelsWindow_SystemDiscovery | own | SystemDiscoveryScreen.cs |
| PlanetLabelsWindow_SystemManagement | own | SystemManagementScreen.cs |
| PlanetLabelsWindow_SystemOrbital | read | GalaxyHudScreen.cs, PlanetConstructiblesScreen.cs |
| PlanetScanViewWindow | own | ScanViewScreen.cs |
| PlanetScreen | own | PlanetOverviewScreen.cs |
| PlayCardDeckModalWindow | own | BattleTacticsScreen.cs |
| PlayDeckFreeCostNotificationWindow | NS | NotificationScreen.cs |
| PlayDeckNewSlotNotificationWindow | NS | NotificationScreen.cs |
| PopulationChangeNotificationWindow | NS (variant row) | NotificationScreen.cs |
| PopulationCollectionThresholdReachedNotificationWindow | NS | NotificationScreen.cs |
| PopulationModalWindow | own | PopulationScreen.cs |
| ProbeLabelsWindow | read | GalaxyHudScreen.cs |
| PropagandaStartedNotificationWindow | NS | NotificationScreen.cs |
| QuestBegunNotificationWindow | NS | NotificationScreen.cs |
| QuestCompletedNotificationWindow | NS | NotificationScreen.cs |
| RecipeCreationModalWindow | own | RecipeCreationScreen.cs |
| RelicsCollectionCanceledNotificationWindow | NS (variant row) | NotificationScreen.cs |
| RelicsCollectionCompletedNotificationWindow | NS (variant row) | NotificationScreen.cs |
| RenameModalWindow | own | RenameModalScreen.cs |
| ResourcesExportScreen | floor (roadmap minimum-pass row: export) | MenuDestinationScreens.cs |
| ScanNodeLabelsWindow | own | ScanViewScreen.cs |
| ScanOverlayWindow | own | ScanViewScreen.cs |
| ScanViewInformationNotificationWindow | NS | NotificationScreen.cs |
| SenateScreen | own | SenateScreen.cs |
| ShipDesignModalWindow | own | ShipDesignScreen.cs, UI/ShipDesignRows.cs |
| SidePanelsWindow | read | UI/SidePanels.cs (+PlanetOverview/QuestJournal/Research screens) |
| SpecialNodeEventNotificationWindow | NS | NotificationScreen.cs |
| StarSystemLabelsWindow | read | GalaxyHudScreen.cs |
| StarSystemManagementScanViewWindow | own | ScanViewScreen.cs |
| StarSystemOverviewScanViewWindow | own | ScanViewScreen.cs |
| StarSystemPopulationModalWindow | own (roadmap: tutorial-locked verification outstanding) | SystemPoliticsScreen.cs |
| StarSystemScreen | own | SystemManagementScreen.cs |
| SystemSelectionModalWindow | own | SystemSelectionScreen.cs |
| TargetSelectionModalWindow | own | TargetSelectionScreen.cs |
| TechnologyNeededNotificationWindow | NS | NotificationScreen.cs |
| TechnologyScreen | own | ResearchScreen.cs |
| TechnologyStageUnlockedNotificationWindow | NS | NotificationScreen.cs |
| TechnologyUnlockedNotificationWindow | NS | NotificationScreen.cs |
| TradeScanViewWindow | own | ScanViewScreen.cs |
| TradingBlockadeNotificationWindow | NS (variant row) | NotificationScreen.cs |
| TradingNotificationWindow | NS | NotificationScreen.cs |
| TreatiesCancelledNotificationWindow | NS (variant row) | NotificationScreen.cs |
| TutorialSelectionModalWindow | own | TutorialSelectionScreen.cs |
| TutorialWindow | own | TutorialScreen.cs |
| UserInstructionsWindow | read (cursor-mode announcer, GlobalHud.cs:242-300) | GlobalHud.cs |
| VictoryAchievedModalWindow | own | VictoryAchievedScreen.cs |
| VictoryScreen | own | VictoryScreen.cs |
| WreckedMothershipLabelWindow | read | GalaxyHudScreen.cs |

## WORK NOW (1)

| Class | What a blind player loses | Evidence |
|---|---|---|
| AnimatedLoadingIconWindow | The only on-screen sign that a save is still being written (`GuiManager.cs:1578` shows it exactly while `IsSaving`); a blind player cannot tell a manual save or autosave is still in progress before quitting. No mod coverage, no roadmap row. JUDGMENT CALL: it is a textless spinner — the owner may rule it decorative and demote to excluded, or want a one-line "saving"/"saved" announcement. | decompiled/Assembly-CSharp/GuiManager.cs:1578; window has zero labels/controls |

## DEFERRED (3)

| Class | Justification |
|---|---|
| ContextualPromptWindow | Penumbra hacking prompt (title+description shown by cursor target modes). Every show site is hacking: `ScanGalaxyCursor.cs:62-64` (hacking-lane scan), `HackingOperationCursor`, `HackingProgramCursor`, `HackingOperation*Component`, `ScanOverlayWindow.cs:134-176` (all three uses check hacking prompt names), `AllocationProvidersListComponent` (processing-power overcap). Roadmap: "Hacking subsystem (Penumbra): dashboard, processing-power/operations banner, traitors banner, program panel, operation route-building — one large stage, NOT to be written blind; wait for the DLC." DLC absent (verified in brief). Live check: registered, hidden, placeholder text only. |
| DisclaimerModalWindow | Roadmap: "Real models for the minimum-pass pages: Mods, Credits/DLC/disclaimer, export, multiplayer join". CAUTION: unlike Credits/DLC there is NO floor today — no mod screen binds it. Retail reachability is narrow: shown at main-menu boot only when `SystemInfo.graphicsShaderLevel < 30` (MainMenuScreen.cs:344-371; alpha/beta branches are version-gated off; `Gui.ShowLongGameDisclaimerIfNeeded` at Gui.cs:1743 has no callers). |
| InGameChatWindow | Multiplayer in-game chat. Roadmap: "multiplayer join (multiplayer deferred until single-player is solid)". Shown from GuiManager/CompetitorOrbitalSlot in multiplayer sessions only. |

## UNREACHABLE (68)

**Dead or debug windows in the GuiWindow closure (7):**

| Class | Evidence |
|---|---|
| AdCreationModalWindow | No prefab in this build (absent from the live 170-window registry); its one caller `MarketplaceAdBanner.cs:159-166` falls into the LogError branch; mod EconomyScreen.cs:54 doc corroborates ("a class of constants with no controls in this build"). |
| GroundBattleOutcomeAppliedNotificationWindow | No prefab registered; zero callers; no `GuiNotificationGroundBattleOutcomeApplied` data class exists to bind it. Dead code. |
| GuiModalNotificationWindow | Zero subclasses, zero callers, no prefab registered. Dead base experiment. |
| StarSystemOrbitalScanViewWindow | Zero callers anywhere in decompiled code; no prefab registered. Dead. |
| TestLocKeyDebugWindow | Shown only by the cheat chat hook (`ChatCheatsHook.cs`) via GuiManager:2073; no prefab in this build, so even the cheat path no-ops. |
| ImageInformationWindow | The game's internal hovered-widget inspector; registered live but NO caller ever shows it (only reference in decompiled code is its own file). Debug tooling. |
| DebugUIGlobalButtonWindow | Debug empire-switch button strip; part of the debug UI gated by `DebugUIToggle.cs:10-14` — visible only when `Version.Accessibility <= Internal` or `Preferences.EnableModdingTools`, then Shift+F1. Debug/god-mode tooling, not normal play. |

**DebugUIWindow family (61 concrete classes)** — a `Behaviour`/OnGUI debug console
outside the AGE GUI, all behind the same `DebugUIToggle` internal/modding-tools gate
(evidence above); god-mode operations (reveal galaxy, switch empire, force election
outcomes...). One verdict each, same evidence:

DebugUIWebAPI, DebugUIWindow_AI, DebugUIWindow_Academy, DebugUIWindow_AcademyRequests,
DebugUIWindow_Achievements, DebugUIWindow_Alliances, DebugUIWindow_AutoTurn,
DebugUIWindow_BattleDirector, DebugUIWindow_BeginGamescomTutorial,
DebugUIWindow_ClearPlayerPrefs, DebugUIWindow_Clients, DebugUIWindow_Diplomacy,
DebugUIWindow_DiplomaticTributes, DebugUIWindow_DiscoverCuriosities,
DebugUIWindow_ElectionUserInteraction, DebugUIWindow_EmpireSwitch,
DebugUIWindow_FleetMissions, DebugUIWindow_FrontGuiWindow, DebugUIWindow_G2GAuth,
DebugUIWindow_G2GProfile, DebugUIWindow_GalaxyViewCamera, DebugUIWindow_GameTimers,
DebugUIWindow_Government, DebugUIWindow_JobScheduler, DebugUIWindow_LobbyChat,
DebugUIWindow_LobbyData, DebugUIWindow_LobbyList, DebugUIWindow_LobbySlots,
DebugUIWindow_Menu, DebugUIWindow_MinorFactions, DebugUIWindow_Negotiation,
DebugUIWindow_Performances, DebugUIWindow_PersistentMenu, DebugUIWindow_Players,
DebugUIWindow_Politics, DebugUIWindow_Population, DebugUIWindow_QuestGlobalVariables,
DebugUIWindow_QuestProbabilities, DebugUIWindow_QuestVariables, DebugUIWindow_Quests,
DebugUIWindow_Relics, DebugUIWindow_Research, DebugUIWindow_Resources,
DebugUIWindow_RevealEveryoneForEveryone, DebugUIWindow_RevealGalaxy,
DebugUIWindow_RevertSettings, DebugUIWindow_Senate, DebugUIWindow_Senators,
DebugUIWindow_Simulation, DebugUIWindow_Steamworks, DebugUIWindow_Survey,
DebugUIWindow_SwitchFaction, DebugUIWindow_TimeBubbles, DebugUIWindow_TradingCompanies,
DebugUIWindow_TutorialKeys, DebugUIWindow_Victory, DebugUIWindow_ViewLayers,
DebugUIWindow_Wars, DebugUIWindows_Clock, DebugUIWindows_DumpSim,
DebugUIWindows_TemporaryEffects. (61)

## EXCLUDED (24) — appendix

Abstract bases (8, `abstract` in source, no prefab):
BaseGarrisonsLabelsWindow, BaseLabelsWindow, BaseLabelsWithPoleWindow,
DamageReportNotificationWindow, GuiLayeredScanViewWindow, GuiScanViewWindow,
PlanetLabelsWindow, VisibleEntityLabelsWindow.

Pure base classes, concrete but no own registration (9, confirmed absent from the live
170-window registry):
GuiWindow (both the Amplitude.Unity.Gui and the ES2 global-namespace declaration —
counted as one universe row), GuiScreen, GuiModalWindow, NotificationWindow,
SimpleDescriptionNotificationWindow, QuestNotificationWindow,
GroundBattleNotificationWindow, BattleCommonNotificationWindow,
ForceTruceBaseNotificationWindow.

Decorative chrome, no text and no controls (2):
GameScreenBackgroundWindow (one background image, GameScreenBackgroundWindow.cs:24),
BlackCurtainWindow (fade curtain, zero labels/controls).

AgeScreen family (3):
AgeScreen (engine base component), AgeDispatchScreen (engine screen-container
plumbing, firstpass), GuiGalaxyScreen (render surface only — camera wiring and matrix
dirtying, GuiGalaxyScreen.cs:5-40; no widgets of its own; galaxy content is covered by
GalaxyHudScreen's map-as-tree).

DebugUI abstract bases (2): DebugUIWindow, DebugUIWindow_Draggable.

## Sum check

161 + 1 + 3 + 68 + 24 = 257 = 191 (GuiWindow closure) + 3 (AgeScreen family) + 63
(DebugUI family). Every class named above exactly once; scripts and intermediate data
in this scratchpad (classes.tsv, closure-*.tsv, mod-coverage.tsv, live-names.txt).

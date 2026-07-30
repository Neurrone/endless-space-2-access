# Screen roadmap

Living inventory of ES2's screens and what making each accessible involves. Class names are
from `decompiled/Assembly-CSharp/`. Status: ☐ pending, ▶ in progress, ✅ done. Complexity:
S (read + activate a list), M (multiple panels/state), L (novel interaction model).

## Phase 1 — outgame (the current front)

| Screen | Classes | Cx | Status | Notes |
|---|---|---|---|---|
| Main menu | `MainMenuScreen`, `MainMenuItem`/`SubItem` | S | ✅ | First graph-UI screen: flyouts as expandable nodes, tooltips announced inline, review buffer, visual hover sync. Patterns now in `docs/generic/ui-navigation.md`/`buffers.md`/`tooltips.md` |
| Options | `OptionsModalWindow` | M | ✅ | Full widget set: instant-switch tab bar, checkbox/slider/droplist rows, droplist popup as sub-screen (game's real popup driven, focus handed over so the game consumes Escape), key rebinding via the game's capture flow, buttons discovered by visibility (window has per-skin duplicate button bars). Both skins work — opened from the pause menu it sits at static layer 52 |
| Load / save | `LoadSaveModalWindow` | S | ✅ | Both instances (in-game + `OutGameLoadModalWindow`); the save list is a table (header+value cells, column-preserving Up/Down, announced as a table on entry; cell buffers hold the cell), Enter selects then Load/Save/Delete act; edit-field name entry via deferred focus handoff (no typed-char echo yet). `run-game.ps1 -LoadSave <title>` boots into a save via `POST /loadsave` |
| Loading screen | `LoadingWindow`, `Diagnostics.Progress` | S | ✅ | Passive announcer: status lines on change, quarter milestones, tip once; progress record baselined on push (it outlives the previous load) |
| Notifications (popups) | `NotificationWindow` + ~60 subclasses | M | ✅ | One generic screen: one stop in visual regions (top controls / empire-info dossier while ticked / body text / bottom controls), per-subclass extras by wired-handler/visible/has-caption filter, tooltips per the shared mode rule |
| Tutorial popup | `TutorialWindow`, `TutorialPopupPanel` | S | ✅ | Body text focused first, page dots navigable (no invented position text), minimize hands the keyboard back (collapsed bar becomes a galaxy stop), close confirm via MessageBoxScreen |
| New game lobby | `NewGameScreen`, `FactionChoiceModalWindow`, `AdvancedSettingsModalWindow` | L | ☐ | Faction/empire slots (`CompetitorSlot`), galaxy settings; the gateway to everything |
| Tutorial choice | `TutorialSelectionModalWindow` | S | ☐ | Appears on new game |
| Mods | `ModdingScreen` | M | ☐ | Low priority |
| Credits / DLC / disclaimer | `CreditScreen`, `DLCModalWindow`, `DisclaimerModalWindow` | S | ☐ | Trivial |
| Multiplayer join | `JoinGameScreen` | M | ☐ | Defer until single-player is solid |

Cross-cutting from day one: `ErrorModalWindow` and generic `GuiModalWindow` confirmations
must always speak — a silent error dialog is a soft-lock for a blind player.
`MessageBoxWindow` (all Ok/Cancel/Alternative confirmations, incl. the 15 s video-settings
countdown and binding conflicts) is covered by `MessageBoxScreen`; `ErrorModalWindow` and
`MessageBoxNonBlockingWindow` are still pending.

## Phase 2 — in-game core loop

Rough order: the loop a player touches every turn, before the screens they touch occasionally.

| Screen | Classes | Cx | Notes |
|---|---|---|---|
| Esc menu | `GameMenuModalWindow` | S | ✅ Circular items + panel toggles; save/load failure reasons spoken; each open settings panel is a conditional Tab stop (game settings read-only, timer rows editable) |
| Galaxy HUD + end turn | `EndTurnWindow`, `EmpireBanner`, `NotificationItemsWindow` | M | ✅ Stops in reading order (empire resources as measured rows / systems / fleets / notifications — the latter two only when populated / turn controls), passive turn announcement, camera-focus on system/fleet activation, real tooltips rendered on focus and read back. Game key collisions (chat on Tab/Enter, EndTurn on KeypadEnter, camera pan on arrows) suppressed generally by `GameKeyStandDown` |
| Galaxy map | `GuiGalaxyScreen`, galaxy view | L | The big one: 3D starfield → scanner/cursor model (world-navigation patterns), system/fleet browsing, movement orders |
| Star system | `StarSystemScreen`, `StarSystemPopulationModalWindow`, `ImprovementsManagementModalWindow` | L | Colony management: construction queue, population, improvements |
| Planet | `PlanetScreen`, `PopulationModalWindow` | M | Colonization, terraform |
| Research | `TechnologyScreen` | L | ES2's tech "web" by era/quadrant — needs a navigation model |
| Empire | `EmpireScreen` | M | Overview + approval/economy per system |
| Economy / market | `EconomyScreen`, `ResourcesExportScreen` | M | Marketplace buy/sell, resources |
| Fleets & military | `FleetsScreen`, `MilitaryScreen`, `FleetSelectionModalWindow`, `ShipDesignModalWindow` | L | Fleet composition, retrofit, ship designer |
| Diplomacy | `DiplomacyScreen`, `NegotiationModalWindow`, `MinorFactionDiplomacyModalWindow`, `PirateDiplomacyModalWindow` | L | Negotiation term-building |
| Politics & senate | `SenateScreen`, `ElectionModalWindow`, `GovernmentModalWindow`, `LawsManagementModalWindow` | M | Parties, elections, laws |
| Heroes / academy | `AcademyScreen`, `HeroSelectionModalWindow`, `HeroInspectionModalWindow`, `HeroCompleteListModalWindow`, `AcademyModalWindow`, `AcademyDiplomacyModalWindow` | M | Hero assignment, skill trees; academy quests (Awakening DLC) |
| Quests & journal | `JournalModalWindow`, `NarrativeScreen` | M | Quest choices are gameplay-critical |

## Phase 3 — battles and events

| Screen | Classes | Cx | Notes |
|---|---|---|---|
| Space battle | `BattleScreen`, `PlayCardDeckModalWindow`, `TargetSelectionModalWindow` | L | Card-based tactics + outcome report narration |
| Ground battle | `GroundBattleScreen`, `GroundTroopManagementModalWindow`, `GroundBattleTargetSelectionModalWindow` | M | Troop composition + card play |
| Cutscenes / narrative events | `CutsceneModalWindow`, `ColonizationCutsceneModalWindow` | S | Skip + narrate |
| Victory / defeat | `VictoryScreen`, `VictoryAchievedModalWindow` | S | |
| Misc | `RenameModalWindow`, `AdCreationModalWindow`, `RecipeCreationModalWindow`, `AdvancedEncounter*` | S | As encountered |

## Non-screen work these imply

- Event narration (turn events, battle results) via `IEventService.EventRaised` — feeds every
  phase-2 screen.
- ✅ `/loadsave` dev endpoint + `run-game.ps1 -LoadSave` boot-into-save. Still wanted: a
  mid-game fixture save (fleets, pending notifications, unlocked End Turn) — the turn-1
  beginner save only proves absences.
- Galaxy map world-navigation model — the largest single design effort; draws on the scanner
  patterns in the generic docs.

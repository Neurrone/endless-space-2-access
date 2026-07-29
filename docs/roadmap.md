# Screen roadmap

Living inventory of ES2's screens and what making each accessible involves. Class names are
from `decompiled/Assembly-CSharp/`. Status: ☐ pending, ▶ in progress, ✅ done. Complexity:
S (read + activate a list), M (multiple panels/state), L (novel interaction model).

## Phase 1 — outgame (the current front)

| Screen | Classes | Cx | Status | Notes |
|---|---|---|---|---|
| Main menu | `MainMenuScreen`, `MainMenuItem`/`SubItem` | S | ✅ | First graph-UI screen: flyouts as expandable nodes, tooltips announced inline, review buffer, visual hover sync. Patterns now in `docs/generic/ui-navigation.md`/`buffers.md`/`tooltips.md` |
| Options | `OptionsModalWindow` | M | ☐ | Tabbed; sliders/droplists/toggles — first full widget-set test |
| Load / save | `LoadSaveModalWindow` | S | ☐ | Also reached in-game; needed early for test loops |
| New game lobby | `NewGameScreen`, `FactionChoiceModalWindow`, `AdvancedSettingsModalWindow` | L | ☐ | Faction/empire slots (`CompetitorSlot`), galaxy settings; the gateway to everything |
| Tutorial choice | `TutorialSelectionModalWindow` | S | ☐ | Appears on new game |
| Mods | `ModdingScreen` | M | ☐ | Low priority |
| Credits / DLC / disclaimer | `CreditScreen`, `DLCModalWindow`, `DisclaimerModalWindow` | S | ☐ | Trivial |
| Multiplayer join | `JoinGameScreen` | M | ☐ | Defer until single-player is solid |

Cross-cutting from day one: `ErrorModalWindow` and generic `GuiModalWindow` confirmations
must always speak — a silent error dialog is a soft-lock for a blind player.

## Phase 2 — in-game core loop

Rough order: the loop a player touches every turn, before the screens they touch occasionally.

| Screen | Classes | Cx | Notes |
|---|---|---|---|
| Esc menu | `GameMenuModalWindow` | S | Early — save/load/quit from in-game |
| Galaxy HUD + end turn | `EndTurnWindow`, notification windows | M | Turn status, pending-action validators, notifications feed |
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
- Test-fixture saves + a `/loadsave` dev endpoint (`IGameSerializationService.LoadGame`) once
  phase 2 starts.
- Galaxy map world-navigation model — the largest single design effort; draws on the scanner
  patterns in the generic docs.

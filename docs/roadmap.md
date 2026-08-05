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
| Tutorial popup | `TutorialWindow`, `TutorialPopupPanel` | S | ✅ | Body text focused first, page dots navigable (no invented position text), minimize hands the keyboard back (the collapsed bar becomes a `hud:tutorial` stop on whichever view level is underneath), close confirm via MessageBoxScreen |
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
| Global HUD (empire, notifications, turn) | `EndTurnWindow`, `EmpireBanner`, `NotificationItemsWindow`, `ControlBanner` | M | ✅ `GlobalHud` — the four clusters the game draws over EVERY view level, declared by each of the three pages under them (galaxy, star system, planet overview) in the order they are drawn: `hud:empire` (the banners as measured rows) first, the page's own stops, then `hud:tutorial`, `hud:notifications` (only while something is pending) and `hud:turn`. They lived on the galaxy screen until the star-system and planet pages showed the defect — drawn on screen, absent from Tab. Passive turn announcement travels with them. Game key collisions (chat on Tab/Enter, EndTurn on KeypadEnter, camera pan on arrows) suppressed generally by `GameKeyStandDown` |
| Galaxy map HUD | `GuiGalaxyScreen` | M | ✅ The galaxy's own stops: systems and fleets, only when populated; camera-focus on activation; real tooltips rendered on focus and read back |
| Galaxy map | `GuiGalaxyScreen`, galaxy view, `PlanetLabelsWindow_SystemOrbital` | L | ▶ The map as a tree off the galaxy HUD's systems stop: a system expands into the planets and starlanes its label draws (fog-gated — a lane to an undiscovered system names nothing) and the camera follows the cursor. **Expanding changes no distance**; how close the camera stands is asked for from the system's own menu ("Show system view" force-zooms to the LAST step, which is the only one at which the game gives a system focus and draws its planet cards; "Return to galaxy view" puts the step back where it was, the map's own default if nobody remembers) and that choice decides how much a planet child has to say — a circle with a name, a state and the game's tooltip when the camera is out, the drawn orbital card when it is in. Enter on a system opens that menu, with "Open system" (the game's management view) first for a colony of yours. A card child announces name, size and type, colonize status; its buffer holds ONLY the card's face — the five outputs where the card writes them (a colony; an unsettled world is drawn as pips and the numbers behind them are not read), the anomalies/curiosities/deposits it draws as icons, and the card's own dossier tooltip. Enter opens an action menu: View planet, Open system, the colonize/buy-outpost/minor-faction buttons and the five secondary buttons (terraform, restore, reduce anomaly, mining probe, destroy — named by the game's own fleet-action titles), each gated on the game's predicate plus `Gui.IsHintActive`; and where colonization is hint-blocked, one **informational** entry carrying the game's blocking sentence ("Missing technology Maximized Exploitation"), which does nothing but close. Screens are bound to `GalaxyViewLevel`s (`GalaxyViewLevels`); the management, planet and system-discovery levels all have screens of their own. **Unverified at turn 1**: every colonize/terraform/mining/destroy/restore/anomaly-reduction button (none is offered by the fixture — colonize is tech-blocked into hint mode on both uncolonized planets, and the five secondary ones need a Behemoth in the system), outposts, minor factions and pirate lairs. Next: the world-navigation model (fleets, lanes as routes, "what is near me"), and the orbital layer's other surfaces — `DockLabel` (one at the fixture), `HangarLabel`, `WreckedMothershipLabelWindow` |
| ↳ Planet constructible panel | `PlanetConstructiblePanel` | S | ▶ `PlanetConstructiblesScreen` (layer 20, polled — it is a panel parked inside `PlanetLabelsWindow_SystemOrbital`, not a window): the list that slides out under a planet card when Terraform or Reduce Anomaly is pressed, and where the order is actually placed. One node per `StarSystemConstructibleItem` (full name from the constructible, cost/turns from the drawn tooltip, the game's refusals in the buffer), Enter replays the line's own click, Escape dismisses it through the game's own `OnCloseConstructiblePanel` message and is consumed (nothing in the game closes this panel with a key). **Implemented, unverified live**: both openers need a Behemoth offering the fleet action, which the turn-1 fixture has not got, so the whole run is on the human test script — registration and a false predicate at the galaxy, orbital and management states are all that can be proven offline |
| ↳ System discovery cutscene | `GalaxyViewLevel_SystemDiscovery`, `PlanetLabelsWindow_SystemDiscovery`, `PlanetLabel_SystemDiscovery` | S | ▶ `SystemDiscoveryScreen` (layer 10, a view level like the other three): a passive announcer on the loading-screen pattern, nothing declared to navigate. Arrival says which system is being discovered (the cutscene draws no heading of its own); each planet the reveal brings up is announced queued, as the card's own fields in drawn order — name (from the model, the card ellipsizes it), unique, hostile presence, size and type, status, anomalies, curiosities, deposits, FIDS ratings. The card's labels are safe to read despite the typewriter: it advances a render cursor, never the label's text. No skip key — the game's own click/Escape are untouched. **Implemented, unverified live**: triggering a discovery needs exploration, which the turn-1 fixture forbids, so the whole run is on the human test script |
| Star system | `StarSystemScreen`, `StarSystemPopulationModalWindow`, `ImprovementsManagementModalWindow` | L | ▶ `SystemManagementScreen`: stops in drawn order — planet cards (card detail in the review buffer), one stop per side panel the game draws (so outpost/ghost sets switch for free), constructibles, queue, hangar. Enter queues / Alt+Enter queues at the head / Shift+Up-Down reorders; controls with several actions (planet cards, queue lines) open `ChoiceSubmenuScreen`. Rename box covered by `RenameModalScreen`. The side panels' wordless readouts are hand-modelled rather than scraped (`Special`): population units and council seats take their name from the `GuiWrapper` on their own tooltip, the growth line takes the game's sentence about it, approval is named by the game's own property title (Obedience for an honour empire), and the political-sensitivity graph — which produced no node at all — reads one share per party off the drawn bars. **Unverified at turn 1**: queue reorder, buyout, hangar ships, population transfer, colonize, outpost/ghost panels, rebellion and migration rows. The planet card's menu now leads with "View planet", which opens the planet overview. Next: `StarSystemPopulationModalWindow` (tutorial-locked in the fixture) |
| ↳ Improvements modal | `ImprovementsManagementModalWindow` | S | ✅ `ImprovementsModalScreen` (layer 85): the drawn bands as three stops — heading (title + system upkeep), the grid of improvement tiles in the rows the engine wrapped them onto, the Close/Scrap row. Tiles are the game's own checkboxes (select-then-act: tick, then Scrap, which raises the game's confirmation), names read in full because the tiles ellipsize them. **Unverified at turn 1**: nothing in the fixture is destructible, so ticking a tile, the Scrap button's enabled label and its confirmation, multi-row wrapping, scroll-into-view, the assigned-hero readout and the empty-list state are all code-verified only |
| Planet | `PlanetScreen`, `PopulationModalWindow` | M | ▶ `PlanetOverviewScreen` (layer 10, a view level like the galaxy and system pages): three stops in drawn order — the info panel (name, the game's Previous/Next Planet buttons, the five outputs named by the game's property titles), the population panel (read-only: the count, then one entry per affinity with its dossier on the tooltip), and the right-hand card as one node per drawn row (status/type/size/climate/biodiversity/anomalies/ratings, every tooltip in the row in the review buffer, a unique planet's lore as a focusable text node). Reached from "View planet", the first entry of the system page's planet-card menu, and from the view level however else it is entered. Stepping planets keeps the cursor on the button and announces the new planet passively. **Unverified at turn 1**: curiosities, resource deposits and the depletion row (no planet in the fixture has any), and the population entries' click (the game opens `PopulationModalWindow`, unmodelled — declared read-only per the approved design). Next: `PopulationModalWindow`, colonization, terraform |
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
| Cutscenes / narrative events | `CutsceneModalWindow`, `ColonizationCutsceneModalWindow` | S | Skip + narrate. The system-discovery cutscene is done (phase 2, under Galaxy map) and is the pattern to copy: announce passively from the model behind the animation, add no skip key of our own |
| Victory / defeat | `VictoryScreen`, `VictoryAchievedModalWindow` | S | |
| Misc | `RenameModalWindow`, `AdCreationModalWindow`, `RecipeCreationModalWindow`, `AdvancedEncounter*` | S | `RenameModalWindow` ✅ (`RenameModalScreen`): the game focuses its own text field, so the mod only names the box and reads the field. Rest as encountered |

## Cross-cutting done

- **Drawn tooltips read a feature at a time** (`TooltipFeatures`, `DrawnTooltip`,
  `Core/Speech/TooltipText.cs`). Every tooltip in the game is an ordered list of panel-feature
  prefabs; the reader bands each feature's own subtree instead of the whole window, reads a run
  of identical items as items, skips the game's separator/spacing features by their own flags,
  and gives the ship stat block the game's own stat titles. `DevProbe.Tooltip()` reports which
  reader answered for each feature, so a family nobody has looked at surfaces in a probe rather
  than in speech.

## Non-screen work these imply

- Event narration (turn events, battle results) via `IEventService.EventRaised` — feeds every
  phase-2 screen.
- ✅ `/loadsave` dev endpoint + `run-game.ps1 -LoadSave` boot-into-save. Still wanted: a
  mid-game fixture save (fleets, pending notifications, unlocked End Turn) — the turn-1
  beginner save only proves absences.
- Galaxy map world-navigation model — the largest single design effort; draws on the scanner
  patterns in the generic docs.

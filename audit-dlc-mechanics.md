# DLC / expansion mechanics — GUI accessibility audit (non-faction)

Decompile + game-data + mod-source only. No live game (another stage owns it). Every claim
below carries a `file:line` cite or is marked UNVERIFIED.

Scope note per the brief: **per-faction panels are another stage's job.** Faction-only surfaces
are listed once, in §7, as one-liners.

---

## 1. How the DLC roster was enumerated (not from memory)

The authoritative list is the 24 `DownloadableContent<N>` classes, registered in
`decompiled/Assembly-CSharp/DownloadableContentManager.cs:39-60` (`Register<DownloadableContentN>()`
— note **1 and 15 are NOT registered**, so 22 are live). Each class declares its internal
`StaticString` name, Steam app id and its content restrictions
(`DownloadableContentRestrictionCategory`, enum at `DownloadableContentRestrictionCategory.cs`).

Internal name → public name comes from the game's own data, not from recall:
`Public/Gui/GuiElements[DownloadableContents].xml` maps each name to a `%…Title` key, resolved in
`Public/Localization/english/ES2_Localization_{Vaulters,Updates,Update11..14}_Locales.xml`.

Three further gate mechanisms were swept:

| Gate mechanism | Where | Found |
|---|---|---|
| `IDownloadableContentService.IsShared(DownloadableContentN.ReadOnlyName)` in code | grep over `decompiled/` | 68 call sites across 24 files (see §3-§6) |
| `<DownloadableContentPrerequisite>` in game XML | `Public/**/*.xml` | 4 in `Gui/GuiElements[ScanView].xml` (DLCUC), 4 inverted in `Gui/Miscellaneous/PropertyFilteringRuleDefinitions.xml` (2 DLCUC, 2 DLCTemplars). Consumed generically by `DownloadableContentPrerequisite.Check()` |
| Per-expansion datatable file sets | `Public/**/*_DLC{2,3,4,5}.xml` | the cleanest mechanic inventory per expansion; DLC2=Supremacy, DLC3=Penumbra, DLC4=Awakening, DLC5=Dark Matter |

`DownloadableContentRestrictionCategory.Setting` + `GameSettingDefinition.IsValid`
(`GameSettingDefinition.cs:169-178`, `:453-462`) is how DLC-only *game settings* drop out of the
new-game screens.

### Full roster

| # | Internal name | Public name | Steam app | GUI-bearing mechanics? |
|---|---|---|---|---|
| 1 | CollectorEdition | (not registered) | — | no |
| 2 | EarlyAccessSubscriber | Uthassum Rhamoezz | 527500 | no — 1 hero |
| 3 | DigitalDeluxeUpgrade | Pathfinders | 539430 | no — skins + hero |
| 4 | FounderPack | Cravers Prime | 392110 | no — skin/hulls |
| 5 | StellarPrisoner | Stellar Prisoner | 392110 | no — quest content |
| 6 | TargetLocked | Target Locked | 392110 | no |
| 7 | HalloweenUpdate | Little Grin Man | 392110 | no |
| 8 | Update4 | Galactic Statecraft Update | 392110 | no restrictions; free content |
| 9 | **DLCVaulters** | **Endless Space 2: Vaulters** | 733140 | **YES — pirates as a system** |
| 10 | Update5 | Community Challenge | 763330 | minor — `Setting Spiral6` galaxy shape, `PlanetAnomaly48` |
| 11 | Update6 | Endless Day Update | 392110 | no |
| 12 | Update7 | Guardians and the Galaxy | 392110 | no restrictions (Guardians ship free to all) |
| 13 | MinorDLCQuests | **Untold Tales** | 813410 | no NEW GUI — 4 minor factions + heroes only |
| 14 | MinorDLCMusic | **Lost Symphony** | 813420 | no — music |
| 15 | MinorDLCVynil | (not registered) | — | no |
| 16 | **DLCHisshos** | **Endless Space 2: Supremacy** | 806780 | **YES — Behemoths, obliterator, ion wave** |
| 17 | Update11 | **Renegade Fleets Update** | 392110 | no — 6 ship skins × 10 hulls |
| 18 | **DLCUC** | **Endless Space 2: Penumbra** | 988440 | **YES — hacking, traitors, ghost systems** |
| 19 | Update12 | **Celestial Worlds** | 949081 | **no gates at all** — zero restrictions, zero `IsShared` sites, no XML prerequisite |
| 20 | Update13 | Harmonic Memories | 949080 | no — 1 hero |
| 21 | Update14 | Muck and Makers | 1054280 | no — minor faction Basryxo + hero |
| 22 | **DLCTemplars** | **Endless Space 2: Awakening** | 1128540 | **YES — Academy overhaul, relics, pirate diplomacy** |
| 23 | DLCDarkMatter | Endless Space 2: Dark Matter | 1523730 | no — 6 heroes + 2 model swaps |
| 24 | PugHeroAddOn | Sbejru Mashmir | 1523730 | no — 1 hero |

**Four expansions carry non-faction GUI**: Vaulters (9), Supremacy (16), Penumbra (18),
Awakening (22). Everything else is content that flows through already-modelled surfaces (hero
lists, faction pickers, minor-faction diplomacy, ship-design hull lists, galaxy-shape settings).

Two brief expectations corrected by the data:
- **Celestial Worlds is not a gated mechanic at all** — `DownloadableContent19.cs` has an empty
  restriction list and there is no `DownloadableContent19.ReadOnlyName` anywhere in the code. Its
  planet content ships ungated.
- **"Renegade Fleets" is cosmetic**; the 353 lines in `DownloadableContent17.cs` are all
  `ShipCustomSkin` + `HullGuiElement` entries, resolved automatically by
  `DownloadableContentManager.GetValidShipCustomSkin` (`:321-334`). No picker GUI.
- **Pirate *diplomacy* is Awakening (22), not Vaulters** — `DiplomacyScreen.cs:610` gates
  `PiratesScreenButton` on DLC22, while the pirates *simulation* is DLC9
  (`PiratesManager.cs:998`). Vaulters gives you pirates; Awakening lets you talk to them.

---

## 2. Environment finding that reshapes priority: none of the four expansions is active here

- Steam manifest `steamapps/appmanifest_392110.acf` `InstalledDepots` = 392111/392113/392114/392117
  (base app) + 527503 (app 527500 = EarlyAccessSubscriber). **No depot for 733140, 806780, 988440
  or 1128540.** No other `appmanifest_*.acf` exists for those app ids.
- Corroborated by the repo's own measured note: `ES2Access/Screens/ScanViewScreen.cs:46-47` —
  "the game switches all three off outright for an installation without that content
  (`ScanOverlayWindow.OnGameCreated`), **which is this one**".
- Both fixtures (`docs/dev-loop.md:3-4`, `[Beginner] test` turn 4 and `[Midgame] quests fleets`
  turn 3) are early-turn beginner saves; neither could show a Behemoth, a hacking operation, a
  relic or an Academy request even with the DLC installed.

**Consequence:** every gap below is *code-verifiable only*. `IsShared` returns false for all four
expansions, so the game does not draw any of these surfaces — a live sighting is impossible until
the owner buys and installs the expansion. Confirm in one line before any stage is planned:
`Amplitude.Unity.Framework.Services.GetService<IDownloadableContentService>()` then `IsShared` on
each of `DownloadableContent9/16/18/22.ReadOnlyName`.

Nothing here is *broken* by that — see §8, the DLC-absence safety review.

---

## 3. Vaulters (DLC9 / `DLCVaulters`) — non-faction surfaces

Mechanic: **pirates become a real actor** (lairs, marks, missions, power level, standing).
Portals and the Argosy are Vaulters-faction gameplay → §7.

Gate sites: `PiratesManager.cs:998` (whole subsystem), `PirateGroup.cs:65`,
`PlanetLabel_SystemOrbital.cs:1196`, `GuiNotificationOptionsManager.cs:2113-2128` (4 notifications).

| Surface | Host screen | Gate | Mod coverage | Severity | Fixture |
|---|---|---|---|---|---|
| `PirateGroup` on a system label: pirate power, next-fleet timer | Galaxy map labels | DLC9 + `!IsPirateHater` (`PirateGroup.cs:65`) | **full** — `UI/SystemLabelReadout.cs:316-345` reads both groups | — | save with pirate presence |
| Pirate-mark buyout button on a system label | Galaxy map labels | pirates marked the system | **full** — `SystemLabelReadout.cs:152-153`, mod phrase `galaxy.system-pirate-mark-buyout` | — | marked system |
| `PirateMarkInventoryPanel` in the HUD | Global HUD | pirates active | **full** — `Screens/GlobalHud.cs:680,829-840` | — | mark in inventory |
| `PirateLairGroup` + `PirateIcon` on an orbital planet card | Galaxy orbital cards | DLC9 && lair holder (`PlanetLabel_SystemOrbital.cs:1196`) | **UNVERIFIED / likely none** — `GalaxyHudScreen.OrbitalActions` (:1373-1409) names Colonize/Buy-Outpost/Minor-Faction/5 secondary buttons; `PirateLairGroup` is a bare icon group in neither the actions list nor (checked) `OrbitalDetails` | medium — "there is a pirate lair here" is invisible | save with a pirate lair |
| Pirate diplomacy window | own screen | DLC22 (!) on the diplomacy page button; also a pirate-held system's own button | **full** — `Screens/PirateDiplomacyScreen.cs` (five bands, reinforcement thresholds, next-fleet toggle) | — | needs DLC22 + pirates |
| `PirateMarkCursor` — mouse mode to place a mark | galaxy, cursor mode | pirate action | **partial** — mode *announced* (`GlobalHud.cs:161-185`) but **no keyboard target confirm**; see §6 | high | pirate action available |
| `PirateMissionReportNotificationWindow` — blockade report: 2 cloned-line tables behind a toggle | Notifications | DLC9 (`GuiNotificationOptionsManager.cs:2123`) | **partial** — baseline title/description/drawn body; **no `Variant`** for `RawLeechedResourcesTable` / `PlayerLeechedResourcesTable` (`PirateMissionReportNotificationWindow.cs:27,34`), so the leeched-resource lists read as scattered drawn rows rather than tables | medium | pirate blockade |
| `NotificationSystemMarkedByPirates`, `NotificationPiratePeaceExpired`, `NotificationEmpireFreeSuperColonization` | Notifications | DLC9 | **full (baseline)** — `SimpleDescription`-shaped, no own tables | — | — |
| `CuriosityTypeRemainsPirateLair` (Supremacy data, pirate-flavoured) | orbital curiosity | DLC16 | **full** — curiosity ring covered, `GalaxyHudScreen.cs:1411-1445` | — | — |
| Ground-battle report's `PirateLairTitle` | Ground battle report popup | pirate lair fight | **full** — `Screens/BattleNotifications.cs:339` | — | pirate lair battle |

---

## 4. Supremacy (DLC16 / `DLCHisshos`) — Behemoths. The largest gap.

"Behemoth" is **`Juggernaut`** in code (`Public/Simulation/Battles/HullDefinitions[Behemoths_DLC2].xml`
is the hull file; every class is `*Juggernaut*`). Three specializations, from
`Public/Gui/GuiElements[Juggernauts_DLC2].xml`: **Obliterator, Battleship, Citadel**.

Gate sites: `MilitaryScreen.cs:319,335`, `GuiPlanet.cs:387`, `PlanetLabel.cs:288`,
`PlanetLabel_SystemOrbital.cs:1241`, `StarSystemLabel.cs:2290`, `PanelFeaturePlanetStatus.cs:50`,
`GuiNotificationOptionsManager.cs:2133-2148`, `GuiJuggernautSpecialization.cs:42` (cross-check
against DLC18).

| Surface | Host screen | Gate | Mod coverage | Severity | Fixture |
|---|---|---|---|---|---|
| **`JuggernautSpecializationModalWindow`** — pick a specialization: card table, empire money/manpower/strategic-resource readouts, Validate + a confirmation box | own modal (`GuiModalWindow`) | DLC16 + a Behemoth selected; opened from `ShipsManagementPanel.cs:506,797` | **NONE** — no `ES2Access/Screens/*` models it; grep for `JuggernautSpecializationModalWindow` over `ES2Access/` returns nothing | **critical** — this is the whole mechanic's decision point, and the button that opens it *is* declared (below), so a keyboard player can reach a modal the mod has no screen for. Exclusive-modal stack withdraws the host window → probable keyboard dead-end until Escape | Behemoth in a fleet |
| "Specialize Behemoth" toolbar button | Military screen / fleet panel ship toolbar | DLC16 + selection | **full** — `UI/ShipRows.cs:78-83`, named `%FleetSpecializeJuggernautTitle` | — (but see the row above) | Behemoth selected |
| `MilitaryJuggernautStatusSidePanel` — Behemoth count gauge vs cap | Military screen left column | DLC16 (`MilitaryScreen.cs:319`) | **partial, likely defective** — picked up generically by `SidePanels.Drawn` (`Screens/MilitaryScreen.cs:211-236`), but the panel declares no `PanelTitle` (only `JuggernautCountGauge`, `JuggernautCountValue`), so `SidePanels.Name` falls through to **`panel.GetType().Name`** (`UI/SidePanels.cs:113-123`) — the stop would be announced as the literal string "MilitaryJuggernautStatusSidePanel" unless the prefab hangs a header tooltip (UNVERIFIED). A bare gauge + bare value is also the exact shape the roadmap already flags for the governor panel (`docs/roadmap.md:24`) | high (spoken class name is a visible defect) | Supremacy owned; panel shows with 0 Behemoths |
| `FireObliteratorFleetAction` → **`ObliteratorFireCursor`** (destroy a planet) | fleet actions, then a cursor mode | DLC16 + obliterator Behemoth (`FleetActionButtonFireObliterator.cs:24-25`) | **partial** — button named generically (`Screens/FleetPanel.cs:274-315`, element-DB title); mode *announced*; **no keyboard target confirm** (§6) | **critical** — irreversible action reachable only by mouse click on the map | obliterator Behemoth |
| `FireIonWaveFleetAction` (toggle) | fleet actions | DLC16 | **full** — generic `FleetActionItem` toggle reading | — | Behemoth |
| `FleetActionKamikaze`, `LaunchMiningProbeFleetAction`, `InitiateTerraformPlanetFleetAction`, `InitiateRestorePlanetFleetAction`, `InitiateReduceAnomalyFleetAction` (all `FleetActions_DLC2`) | fleet actions + orbital card secondary buttons | DLC16 | **full** — `GalaxyHudScreen.cs:1391-1396` names all five off the game's own action titles; `FleetPanel` covers the fleet-panel copies | — | Behemoth in system |
| `DestroyPlanetFleetAction` button on an orbital card | Galaxy orbital cards | DLC16 | **full** — `GalaxyHudScreen.cs:1396` (`%DestroyPlanetFleetActionTitle`) | — | — |
| Behemoth "work on this planet" opener list | Planet card → `PlanetConstructiblesScreen` | DLC16 + Behemoth in system | **full** — `Screens/PlanetConstructiblesScreen.cs:9,31` explicitly documents it and its fixture limit | — | Behemoth in system (`docs/test-recipes.md:342,353` already records this) |
| **Destroyed-planet status** — `GuiPlanet.PlanetStatuses.Destroyed` on the planet label, orbital card and system label | Galaxy labels / planet page | DLC16 (`GuiPlanet.cs:387`, `PlanetLabel.cs:288`, `PlanetLabel_SystemOrbital.cs:1241`, `StarSystemLabel.cs:2290`, `PanelFeaturePlanetStatus.cs:50`) | **UNVERIFIED** — five separate call sites; the mod's readouts read drawn text so it *may* fall out for free, but the status is drawn partly as icon state | medium — "this planet no longer exists" | a destroyed planet |
| `ContextualIconJuggernautEffects` on a system label | Galaxy map labels | Behemoth effects on the system | **full** — `UI/SystemLabelReadout.cs:103` | — | Behemoth parked |
| `SimulationDescriptors[Citadel_DLC2]` / `DamageAppliedByCitadel` battle play | Battle tactics / advanced report | DLC16 + citadel Behemoth | **UNVERIFIED, likely full** — battle plays and encounter-play elements are read generically by `BattleTacticsScreen` / `AdvancedEncounterPlayScreen` | low | citadel battle |
| `ObliteratorVictimReportNotificationWindow` (3 tables) | Notifications | DLC16 | **full** — `Variant` at `Screens/NotificationScreen.cs:2551-2562` | — | — |
| `IonWaveReportNotificationWindow` (1 table) | Notifications | DLC16 | **full** — `NotificationScreen.cs:2544-2550` | — | — |
| **`ObliteratorAttackReportNotificationWindow`** — the *attacker's* copy: 4 own labels, no table | Notifications | DLC16 | **partial** — no `Variant`; a `DamageReportNotificationWindow` subclass whose four labels (`DestroyedCommandPointsLabel`, `DamagedCommandPointsLabel`, `DestroyedImprovementsLabel`, `KilledPopulationsLabel`) are bare values | medium | firing the obliterator |
| `NotificationJuggernautLocalActionEnded`, `NotificationOutpostLockedWithHonor`, `NotificationMinorEmpireRazed`, `NotificationRemainsSpawned`, `NotificationInfluencedSystemPopulationConsumed`, `NotificationShipLockedReactivatedShipRole{Battleship,Obliterator}`, `NotificationFleetActionReady{FireIonWave,FireObliterator}` (`Public/Gui/GuiElements[Notifications_DLC2].xml`) | Notifications | DLC16 | **full (baseline)** | — | — |
| `NotificationGroundBattleOutcome{Selection,Applied}` | Notifications | DLC16 | **partial** — `GroundBattleOutcomeSelectionNotificationWindow` has a `Choices` variant (`NotificationScreen.cs:2612-2619`) but no Confirm; `docs/roadmap.md:17` already tracks "one-of-N semantics for … ground-battle/hacking outcome pickers" | medium (already tracked) | ground battle |
| `Setting JuggernautQuestEnabled` in advanced settings | New game / advanced settings | DLC16 (`DownloadableContent16.cs`, `Setting` restriction) | **full** — `AdvancedSettingsScreen` + `SettingRows` read whatever the game draws, and `GameSettingDefinition.IsValid` drops invalid settings | — | — |
| Behemoth hulls in the ship designer | Ship designer | hull `DownloadableContentPrerequisite` / `HullGuiElement` restriction (`HullDefinition.cs:224,312-313`) | **full (assumed)** — the designer's hull list is read from what the game offers | — | Supremacy owned |

---

## 5. Penumbra (DLC18 / `DLCUC`) — hacking. The deepest unmodelled subsystem.

Hacking has **no window of its own**: it lives entirely inside the **scan view** (trade and economy
lenses) plus per-node labels and galaxy-label buttons. 150+ classes; 38 notification types
(`Public/Gui/GuiElements[Notifications_DLC3].xml`).

Gate sites: `ScanOverlayWindow.cs:242-253` (dashboard + both banners hard-hidden when absent),
`ScanNodeLabel.cs:741`, `HackingManager.cs:1931`, `DepartmentOfIntelligence.cs:647`,
`FleetActionButtonAttack.cs:83`, `GuiJuggernautSpecialization.cs:42`,
`GuiNotificationOptionsManager.cs:2153-2218` (15 notifications), plus 4 XML
`DownloadableContentPrerequisite` entries in `Gui/GuiElements[ScanView].xml:98,137,203,242`.

| Surface | Host screen | Gate | Mod coverage | Severity | Fixture |
|---|---|---|---|---|---|
| **`ScanViewWindowHackingDashboard`** — title, mode toggles (`HackingOperation` / `DefensiveProgram` / `OffensiveProgram`), two program menus cloned from `{Defensive,Offensive}ProgramLinePrefab`, selected-program state | Scan view overlay (`ScanOverlayWindow.HackingDashboard`) | DLC18 (`ScanOverlayWindow.cs:242`) | **NONE** — `Screens/ScanViewScreen.cs:46-47` states it outright: "The hacking dashboard and its banners are not modelled" | **critical** — this is where every hacking operation and program starts | Penumbra owned |
| **`ScanViewWindowHackingBanner`** — processing-power stock/allocation cells, overcap warning, hacking speed, live operation lines, trace lines | Scan view overlay | DLC18 | **NONE** (same note) | **critical** — the only place ongoing operations and traces against you are listed | Penumbra owned |
| **`ScanViewWindowTraitorsBanner`** — traitor total, revenue, details toggle, per-empire table | Scan view overlay | DLC18 | **NONE** (same note) | high — traitor economics | Penumbra owned |
| Scan-view legend groups `HackingCaption` / `TraceCaption` (4 items each) | Scan view legend | DLC18 XML prerequisite | **full** — `ScanViewScreen.AddCaptionGroups`/`Declared`/`Allowed` (`:962-1027`) filters caption groups by the game's own prerequisites, so these appear exactly when owned. **This is the one place the mod is already DLC-correct by construction.** | — | Penumbra owned |
| **`ScanNodeLabelHackingProgramPanel`** — the ring of `HackingProgramItem`s around a scan node | Scan view node labels | DLC18 + hover (`IconGroupHoverArea`) | **NONE** — `ScanViewScreen.BuildNodes` (:393-443) reads `TraitorAndTradeLine` + `ContentTable` only | high — per-node program placement | Penumbra owned |
| Hacking-operation cursor modes: **`HackingOperationCursor`** (start point, waypoints, target — `ScanNodeLabel.cs:756-760` shows waypoint/start icons) and **`HackingProgramCursor`** (place a program on a node) | scan view, cursor modes | DLC18 | **partial** — mode announced (`GlobalHud.cs:161-185`); **no keyboard route-building or target confirm** (§6) | **critical** — an operation is a multi-node *route*, so this is the hardest cursor mode in the game | Penumbra owned |
| `StarSystemLabelHackingBeaconButton` — displace a beacon | Galaxy map labels | DLC18 + beacon present | **full** — `UI/SystemLabelReadout.cs:175-200`, mod phrase `galaxy.system-hacking-beacon` ("Displace hacking beacon") | — | beacon on a system |
| **Traitor system actions** — `RevealTraitorsSystemAction`, `KillTraitorsSystemAction`, `RemoveTraitorsSystemAction`, drawn as bare icons by `SystemTraitorsActionItem` inside `TraitorsGroup` | Star system page → `ColonyPopulationSidePanel` (`ColonyPopulationSidePanel.cs:30,106`) | DLC18 + traitors in your system. **Non-faction — this is the VICTIM side, every empire needs it** | **partial, UNVERIFIED** — no `Special` case for `TraitorsGroup` in `Screens/SystemManagementScreen.cs` (grep: no "traitor"); they fall to the generic walk, which declares a clickable widget with empty text as `Cells.Control(...)` named from its tooltip (`UI/SidePanels.cs:243-278`) — plausible but never sighted | high — the only way to clear traitors | save with traitors |
| Traitor count on a system label (`TraitorCountGroup`, `TraitorCountLabel`) | Galaxy map labels | DLC18 | **full** — `UI/SystemLabelReadout.cs:56-61` | — | traitors present |
| Traitor/trade line on a scan node | Scan view | DLC18 | **full** — `ScanViewScreen.cs:425` | — | — |
| `LaunchDetectionProbeFleetAction` (`FleetActions_DLC3`) → posts an order directly (`FleetActionButtonLaunchDetectionProbe.cs:45-46`) | fleet actions | DLC18 | **full** — generic `FleetActionItem` reading | — | — |
| `InvisibilityFleetAction` toggle | fleet actions | DLC18 | **full** — generic toggle reading | — | — |
| `HackingOperationOutcomeSelectionNotificationWindow` — outcomes table + parameters sub-table + Validate + countdown gauge | Notifications | DLC18 | **partial** — `Variant` with both `Choices` tables and `Confirm` (`NotificationScreen.cs:2620-2636`); `docs/roadmap.md:17` still tracks one-of-N semantics, the parameters sub-choice and the countdown gauge | medium (tracked) | hacking outcome |
| `DisplacementReportNotificationWindow` (2 tables behind a toggle) | Notifications | DLC18 | **full** — `NotificationScreen.cs:2533-2543` | — | — |
| **`DefenseHackingProgramEncounteredNotificationWindow`** — carries a `CancelHackButton` and nothing else | Notifications | DLC18 | **partial** — no `Variant`; a bare-icon action button is exactly what the shared caption rule drops (`NotificationScreen.cs:2416-2422` describes the `Gateways` hatch for this) | medium — "cancel the hack" may be undeclared | hitting a defense program |
| The other 30 `Notifications_DLC3` popups (backdoor lifecycle ×6, operation cancel reasons ×4, ghost system/planet lost, hidden-home-system detected ×3, resources stolen from pirates, traitor discovered/removed ×4, …) | Notifications | DLC18 | **full (baseline)** — `SimpleDescription`-shaped | — | — |
| Bailiff auctions `AuctionActionCancelHackingProgram` / `AuctionActionDestroyHackingBeacon` (`Gui/GuiElements[Bailiff_DLC3].xml`) | `BailiffReportNotificationWindow` lines | DLC18 | **full** — the report's cloned-line table has a `Variant` (`NotificationScreen.cs:2453-2459`); its totals footer is already on `docs/roadmap.md:14-15` | — | bailiff turn |
| `Setting HackingOutcomeTimer` + `HackingOutcomeTimerDuration` (4 values) | New game / advanced settings | DLC18 `Setting` restriction | **full** — generic settings reading | — | — |
| `PropertyFilteringRuleDefinitions.xml:186,205` — two empire properties hidden without DLCUC (processing power etc.) | Empire/economy readouts | inverted DLCUC prerequisite | **full by construction** — the game filters; the mod reads what is drawn | — | — |
| **`ScanOverlayWindow.HandleInput`** claims Escape for the dashboard's active mode (`:130-183`) | Scan view input | DLC18 (`hackingEnabled`) | **UNVERIFIED risk** — with Penumbra owned, Escape in the scan view is consumed to close a dashboard mode before it can leave the lens. `ScanViewScreen` deliberately does not consume Escape (`:29-31`), which is correct, but the *ordering* has never been measured with the DLC on | medium | Penumbra owned |

---

## 6. The cross-DLC blocker: cursor target modes have no keyboard confirm

This is one defect that damages three expansions at once, and it is **not on the roadmap** —
only in a code comment.

`Screens/GlobalHud.cs:136-183` (`AnnounceCursorMode`) says it plainly: eight cursors turn a button
press into a *mode* that waits for a mouse click on the map, the mod announces the game's own
instruction and the mode's end, and "**whether the galaxy tree grows a 'confirm the target here'
gesture for these modes is an open design question and deliberately not answered here**".

The cursor classes (`decompiled/Assembly-CSharp/*Cursor.cs`) and who needs them:

| Cursor | Reached from | Expansion |
|---|---|---|
| `ObliteratorFireCursor` | `FleetActionButtonFireObliterator.cs:24-25` | Supremacy |
| `HackingOperationCursor` (multi-node route: start, waypoints, target) | hacking dashboard | Penumbra |
| `HackingProgramCursor` | hacking dashboard program menu | Penumbra |
| `PirateMarkCursor` | pirate action | Vaulters |
| `ProbeLaunchingCursor` | probe actions (base + DLC18 detection probe) | base + Penumbra |
| `HonorActionCursor` | Hissho honor actions | Supremacy (faction) → §7 |
| `TakeSystemCursor`, `TimeBubbleCursor`, `GalaxyGarrisonCursor`, `CoordinationRequestCursor` | base game | base |

Base-game cursors share the gap, so the fix pays for itself outside DLC work — but for Supremacy
and Penumbra it is the difference between "announced" and "operable". `docs/roadmap.md` should
carry this as a **To decide (owner)** item; today it exists only as a comment.

---

## 7. Awakening (DLC22 / `DLCTemplars`) — Academy overhaul + relics

Gate sites: `AcademyGroup.cs:110`, `AcademyInfoSidePanel.cs:119,162`, `DiplomacyScreen.cs:605,610,959`,
`StarSystemLabel.cs:2994`, `Senate.cs:1139,1224`, `DepartmentOfForeignAffairs.cs:3458,3481`,
`GameManager.cs:1106`, `DroppableRelic.cs:39`, `DroppableRelation.cs:39`,
`NotificationMetaplot{Begun,Finished}.cs:76,56`, `GuiNotificationOptionsManager.cs:2223-2268` (10).

**A second gate matters for fixtures**: `GameManager.cs:1106-1112` — even with DLC22, if the lobby's
`AcademyExpansion` setting is `"None"` the Academy home empire is never created. So a save needs
DLC22 **and** `AcademyExpansion != None`.

| Surface | Host screen | Gate | Mod coverage | Severity | Fixture |
|---|---|---|---|---|---|
| `AcademyModalWindow` — 13 `AcademyBasePanel` subclasses (roles, roles priority, roles report, contribution control/info, appeasement, grand admiral, librarian, master of dust, get-a-job, metaplot, vault keeper, no-roles, header) + a named-ship strip | own modal, layer 46 | DLC22 + Academy bound | **full by construction** — `Screens/AcademyModalScreen.cs:14-33` reads one stop per DRAWN panel via `MustShow`/`RefreshPanelsVisibility`, so all 13 are covered without 13 readers. Doc comment already records that it could not be bound in the fixture | — | DLC22 + Academy request |
| `AcademyDiplomacyModalWindow` | own screen | DLC22 (`DiplomacyScreen.cs:959`) | **full** — `Screens/AcademyDiplomacyScreen.cs` | — | DLC22 |
| Academy button on the diplomacy page / on a system label | Diplomacy / galaxy labels | DLC22 + `HasBeenDiscovered` (`DiplomacyScreen.cs:605`, `StarSystemLabel.cs:2994`) | **full** — `Screens/DiplomacyScreen.cs:548` names screen buttons | — | DLC22 |
| `AcademyGroup` on a system label | Galaxy map labels | DLC22 (`AcademyGroup.cs:110`) | **partial — already tracked**: `docs/roadmap.md:20` "AcademyGroup bottom readout" | low (tracked) | DLC22 |
| `AcademyInfoSidePanel` — DLC22 changes the level tooltip and the failure flag on the hero-list button (`:119-121,159-166`) | Academy screen left column | DLC22 | **full** — read generically as a side panel; the text differences are the game's | — | DLC22 |
| `ContextualAcademyDiplomaticExchangeUpdateNotificationWindow` — exclusive choices, tick-shaped Validate, roles table, gateway into the Academy screen | Notifications | DLC22 | **full** — the richest `Variant` in the file (`NotificationScreen.cs:2729-2751`) | — | DLC22 |
| **`AcademyRoleNotificationWindow`** — an `AcademyRolesReportPanel.RoleLineTable` of cloned lines | Notifications | DLC22 | **partial** — no `Variant`, so its role lines read as scattered drawn rows. (The *other* window's copy of the same panel IS handled, `NotificationScreen.cs:2771-2779` — so the fix is one entry reusing `Roles(...)`) | medium | DLC22 role granted |
| `AcademyRequestNotificationWindow`, `AcademyDiscoveredNotificationWindow`, `ContributionWarningNotificationWindow`, `LodestoneRewardNotificationWindow`, `NotificationAcademyDiplomaticMoodMessage`, `NotificationOnAcademyDiplomaticExchange` | Notifications | DLC22 | **full (baseline)** — all `SimpleDescriptionNotificationWindow` with no own tables (verified: 3-13 lines each) | — | — |
| `NotificationMetaplot{Begun,Finished}` — swap their description for a DLC22 variant (`:76,56`) | Notifications | DLC22 | **full (baseline)** | — | — |
| **`CollectRelicsFleetAction`** (`FleetActions_DLC4`) + `FleetActionToggleCollectRelics` (cancels via `OrderCancelEntityAction`, `:108-109`) | fleet actions | DLC22, **non-faction** (`StarSystemAwakenImprovementNonTemplars` exists) | **full** — generic `FleetActionItem` toggle | — | relics spawned |
| `StarSystemCollectRelicImprovement` / `StarSystemAwakenImprovement` (+ Vaulters and non-Templars variants) | System construction queue / improvements modal | DLC22 | **full (assumed)** — constructibles read from the game's own lists | — | DLC22 |
| `PanelFeatureSystemRelics`, `PanelFeatureCollectRelicsEffects`, `PanelFeatureRelicSkill`, `PanelFeatureBeaconDisplacement` | tooltips | DLC22 / DLC18 | **full (assumed)** — `UI/TooltipFeatures.cs` reads panel features generically | low | DLC22 |
| Relic slots on a hero's skill tree (`RelicSkillTreeItem`) | Hero inspection | DLC22 + Templars-ish | **full** — `Screens/HeroInspectionScreen.cs:1217-1370` (`AddRelics`, `RelicName`) | — | DLC22 |
| Relic totals in the HUD (`RelicManagementPanel`: net / research / hero / FIDI / temple relics) | Global HUD | DLC22 | **full** — `Screens/GlobalHud.cs:682,914-956` | — | DLC22 |
| `RelicsCollectionCompleted/CanceledNotificationWindow` | Notifications | DLC22 | **full** — both `Variant`s (`NotificationScreen.cs:2490-2511`); `docs/roadmap.md:15` "relics ×2" refers to their header-less line classes | low (tracked) | — |
| `NotificationRelicsSpawned`, `NotificationOnEmpireRelicSlotLocked` | Notifications | DLC22 | **full (baseline)** | — | — |
| `VaultKeeperPolitic` law + `OrderSetVaultKeeperPolitics` (`Senate.cs:1139,1224` — DLC22 bypasses the law's own prerequisites) | Senate / laws | DLC22 | **UNVERIFIED, likely full** — `LawsScreen`/`SenateScreen` read the game's law lists; the DLC only changes which laws qualify | low | DLC22 |
| `Setting AcademyDifficulty` (4 values) + `AcademyExpansion` (5 values) | New game / advanced settings | DLC22 `Setting` restriction | **full** — generic settings reading | — | — |
| `AcademyDiplomaticRelationStates` (5 + 5 "Other" variants) | Diplomacy readouts | DLC22 | **full (assumed)** — states read as game text | — | DLC22 |

### Faction-only surfaces (other stage) — one line each

- **Vaulters**: the Argosy (`ShipRoleSuperColonizer`) and portals — `CreatePortalFleetAction` requires
  `ShipRoleSuperColonizer` + `EmpireImprovementUnique11SuperColonizer`, or a Behemoth carrying
  `ModuleSupportJuggernautVaulters` (`Public/Simulation/EntityActions[Fleet].xml:504-524`) — a
  Vaulters×Supremacy cross. The mod already reads `ContextualIconPortal` /
  `ContextualIconBlockedFleetPortal` (`UI/SystemLabelReadout.cs:90-91`).
- **Supremacy / Hissho**: Honor (`EmpireActions_DLC2` 4 honor actions, `HonorActionCursor`,
  `FleetActionToggleHonorAction`, `Honor_DLC2` gui elements), `OutpostActionFinishHisshos`.
- **Penumbra / Umbral Choir**: sanctuaries and ghost systems (`GhostifyEmpireAction`,
  `GhostInfoSidePanel`, `GhostPopulationSidePanel`, `ColonyInfoSidePanel.DecolonizeGhostToggle` at
  `:64,538-548,1002-1015`, `GhostLevel1`, `PlanetTypeFake*`), invisible-ship UI.
- **Awakening / Nakalim(Templars)**: `EmpireRelicsSidePanel` on the Empire screen is
  affinity-gated, not DLC-gated (`EmpireRelicsSidePanel.cs:48-51`: `CanBeShown()` requires
  `AffinityGameplayTemplars`) — its four clickable `RelicSlotItem`s (assign relics to empire slots)
  are **not modelled** and belong to the faction stage.

---

## 8. DLC-absence safety review (the mod must not misbehave without a DLC)

Good news first: **DLC ownership never removes a C# type.** All the classes above live in
`Assembly-CSharp`; the DLC gates data, prefab content and `IsShared`. So no mod code path can
`TypeLoadException` for a missing DLC.

Reviewed for absence-safety:

1. `ScanViewScreen` — **correct and exemplary.** `Declared`/`Allowed` (`:997-1027`) run the game's
   own `Prerequisite.Check` over caption groups, so the hacking/trace legend groups appear exactly
   when owned and vanish when not. No hard-coded count.
2. `ScanViewScreen` also never touches `HackingDashboard`/`HackingBanner`/`TraitorsBanner`, which
   `ScanOverlayWindow.cs:249-254` sets `Visible = false` on when absent. Safe.
3. `AcademyModalScreen.IsActive` (`:66-77`) requires `window.Shown && window.IsReady &&
   Panels(window) != null`. Without DLC22 the window is never shown (no `AcademyEmpire` to bind —
   `AcademyModalWindow.cs:111`). Safe as written. **The brief's premise that this incident is
   recorded in `docs/test-recipes.md` is stale: grep for "academy" over `docs/*.md` returns only
   `es2-architecture.md:95`, `es2-facts.md:539` and two `roadmap.md` pointers — no incident note
   exists anywhere in the docs.** Either it was never written down or it was lost; worth a line in
   `docs/test-recipes.md` either way.
4. `MilitaryScreen.BuildSidePanels` iterates `SidePanels.Drawn`, and `MilitaryScreen.cs:319` only
   *shows* the Behemoth panel when owned. Safe — the panel is simply absent.
5. `SystemLabelReadout` reads `label.PirateGroup`, `TraitorCountGroup`,
   `ContextualIconJuggernautEffects`, `ContextualIconPortal` and the hacking-beacon button — all
   guarded by `AgeWidgets.Visible` / null checks. Safe.
6. `NotificationScreen.Variants` is keyed on `Type`; DLC popups simply never arrive. Safe.
7. `ShipRows.Toolbar` declares `panel.SpecializeJuggernautButton` unconditionally, relying on the
   button's own visibility. **UNVERIFIED without the DLC** — if the prefab leaves the field null,
   `ShipRows.Button` must tolerate null (it reads `AgeWidgets.Visible` first, so probably fine).
8. The **live risk is the inverse**: not absence, but *presence*. Nothing in this mod has ever run
   with `IsShared == true` for any expansion (§2). Every "full" above is a code-level judgement.

---

## 9. Prioritized gap list, grouped into plausible stages

Ordered by damage to a blind player per unit of work. Sizes are rough.

**Stage A — the Behemoth specialization modal (small, critical, no DLC needed to *write*)**
`JuggernautSpecializationModalWindow`: card table (`SpecializationCardTable` of
`JuggernautSpecializationActionCard`), empire money/manpower/strategic resources, Validate, and the
confirmation box it raises (`:179-208`). This is the only *completely unmodelled modal reachable
from a control the mod already declares* — today pressing "Specialize Behemoth" almost certainly
strands the keyboard. Shape it on `HeroSelectionScreen`/`AcademyModalScreen`. Add a
`SidePanels.Name` fallback or a mod phrase for `MilitaryJuggernautStatusSidePanel` in the same
stage, so the panel does not announce its class name.

**Stage B — cursor target modes get a keyboard confirm (medium, cross-cutting, base game pays for it)**
Resolve the open question in `GlobalHud.cs:150-153`. Serves `ObliteratorFireCursor`,
`HackingProgramCursor`, `PirateMarkCursor`, `ProbeLaunchingCursor`, `TakeSystemCursor`,
`TimeBubbleCursor` and (partly) `HackingOperationCursor`. **Owner decision required**; put it on
`docs/roadmap.md` §To decide first. Multi-node routes (hacking operations) probably need their own
gesture and can be deferred to Stage D.

**Stage C — the six unregistered notification `Variant`s (small, mechanical)**
One dictionary entry each in `NotificationScreen.Register()`:
`PirateMissionReportNotificationWindow` (2 tables, DLC9),
`ObliteratorAttackReportNotificationWindow` (4 bare labels, DLC16),
`AcademyRoleNotificationWindow` (reuse the existing `Roles(...)` helper, DLC22),
`DefenseHackingProgramEncounteredNotificationWindow` (`CancelHackButton` as a `Gateway`/`Confirm`,
DLC18). Also worth folding in: the `docs/roadmap.md:14-19` items that happen to be DLC ones
(bailiff totals footer, relics ×2 line classes, hacking parameters sub-choice + countdown gauge).
All fixture-blocked for sighting; all cheap and low-risk to write.

**Stage D — hacking (large; needs Penumbra installed to verify at all)**
The whole subsystem in one place, because the pieces only make sense together:
`ScanViewWindowHackingDashboard` (modes + program menus), `ScanViewWindowHackingBanner`
(processing-power allocation, operation and trace lists), `ScanViewWindowTraitorsBanner`,
`ScanNodeLabelHackingProgramPanel`, and a route-building gesture for `HackingOperationCursor`
(start → waypoints → target). Also measure `ScanOverlayWindow.HandleInput`'s Escape claim
(`:130-183`) against `ScanViewScreen`'s deliberate non-consumption. **Do not schedule before the
owner installs Penumbra** — §2 shows the game hard-hides all three surfaces here, so a stage would
be writing blind against prefab fields nobody can see.

**Stage E — traitor system actions (small, but only inside a save with traitors)**
Verify or add a `Special` case for `TraitorsGroup` / `SystemTraitorsActionItem` in
`SystemManagementScreen` (Reveal / Kill / Remove traitors). Non-faction victim-side mechanic:
without it, a player being hacked cannot clear traitors. Pair with reading
`PlanetLabel_SystemOrbital.PirateLairGroup` and the Supremacy destroyed-planet status on the
orbital card — three small label/panel reads that share a fixture requirement.

**Stage F — documentation debt, no code (tiny, do it now)**
- `docs/roadmap.md`: add "cursor target modes have no keyboard confirm" to **To decide**; add the
  Behemoth specialization modal and the hacking subsystem to **To build**.
- `docs/es2-facts.md`: record the DLC gate mechanism (three kinds of gate, §1), the
  `AcademyExpansion == "None"` second gate on the Academy home (`GameManager.cs:1106-1112`), and
  that "Behemoth" is `Juggernaut` in code.
- `docs/test-recipes.md`: record that **no expansion is installed in this environment** (§2), so
  every DLC surface is fixture-blocked by ownership rather than by turn count — and record the
  Academy-bind hazard that the brief believed was already there.

**Not worth a stage:** Untold Tales, Lost Symphony, Harmonic Memories, Muck and Makers, Dark
Matter, Renegade Fleets, Celestial Worlds, Community Challenge, Cravers Prime, Pathfinders and the
hero add-ons introduce no GUI surface of their own — their content arrives through hero lists,
faction/minor-faction pickers, hull lists, anomaly readouts and the galaxy-shape setting, all of
which the mod already reads from the game's own collections.

# Faction-specific accessibility surface — audit

Decompiled + mod-source only (no live game). Cites are `file:line` into
`decompiled/Assembly-CSharp/` (game) and `ES2Access/` (mod). Line numbers are for this
decompile; member names are the durable handle.

## 1. The roster, from game data

`Public/Simulation/Factions[Major*].xml` + `FactionTraits[Affinity*].xml`. Each major
declares exactly one `<Affinity Name="AffinityGameplay*"/>`; the code-side constants are
`FactionTrait.AffinityGameplay*` (`FactionTrait.cs:165-189`).

| Display name | Faction serial | Gameplay affinity | Ships with |
|---|---|---|---|
| Sophons | `FactionSophons` | `AffinityGameplaySophons` | base |
| Cravers | `FactionCravers` | `AffinityGameplayCravers` | base |
| United Empire | `FactionTerrans` | `AffinityGameplayTerrans` (constant is spelled `AffinityGameplayUnitedEmpire`) | base |
| Horatio | `FactionHoratio` | `AffinityGameplayHoratio` | base |
| Riftborn | `FactionTimeLords` | `AffinityGameplayTimeLords` | base |
| Unfallen | `FactionUnfallen` | `AffinityGameplayUnfallen` | base |
| Vodyani | `FactionVampirilis` | `AffinityGameplayVampirilis` | base |
| Lumeris | `FactionVenetians` | `AffinityGameplayVenetians` | base |
| Vaulters | `FactionVaulters` | `AffinityGameplayVaulters` | DLC9 `DLCVaulters` (`DownloadableContent9.cs:7`) |
| Hissho | `FactionMajorHisshos` | `AffinityGameplayMajorHisshos` | DLC16 `DLCHisshos` (`DownloadableContent16.cs:7`) |
| Umbral Choir | `FactionUmbralChoir` | `AffinityGameplayUmbralChoir` | DLC18 `DLCUC` (`DownloadableContent18.cs:7`) |
| Nakalim | `FactionTemplars` | `AffinityGameplayTemplars` | DLC22 `DLCTemplars` (`DownloadableContent22.cs:7`) |

**The owner's DLC list is exactly right** — 8 base majors, 4 DLC majors, no fifth. Also in
the data and worth knowing about:

- `FactionMezari`, `FactionSheredyn` (`Factions[Major].xml:105,136`) are `Hidden="true"`
  reskins that **share** `AffinityGameplayTerrans`; only the ship skin differs
  (`DownloadableContent10.cs:75`). They add no GUI surface.
- `FactionAcademyEmpire` (`Factions[Academy_DLC4].xml`) is an AI-only empire, not playable.
- `FactionRandom`, `FactionTerransTutorial` are pickers/fixtures, not surfaces.

## 2. How the game gates faction GUI — the vocabulary to grep

Affinity-name comparisons are the *minority* of gates. The real gate vocabulary is a set of
capability predicates on `Empire` (`Empire.cs:140-210`), most backed by a simulation
property, which GUI code reads by name. This table is the thing to reuse; grepping
`AffinityGameplay*` alone finds about a fifth of the surface.

| Predicate | Faction it means | `Empire.cs` |
|---|---|---|
| `HasMotherships` | Vodyani (arks) | :142 (via `DepartmentOfDefense`) |
| `HasOmniscience` | Sophons | :144 (`DepartmentOfScience.cs:198` = tag `AffinityGameplaySophons`) |
| `CanRootSystems`, `CanUseRoots()` | Unfallen (vines) | :154; `DepartmentOfTheInterior.cs:7492` = affinity Unfallen |
| `HasSuperColonizer`, `CanSuperColonizeSlumberingRuins`, `CanSeeSlumberingRuins` | Unfallen (golden age / ruins) | :156-160 |
| `HasGhostSystems`, `HasHiddenHomeSystem` | Umbral Choir | :164-166 |
| `HasGeneHunter` | Horatio | :175 |
| `CanUseHonor`, `HasHisshosFestivals` | Hissho | :205, :209 |
| `CanBuyout`, `CanBuyoutWithEmpirePoint`, `CanBuyOutposts`, `CanBuyAllOutposts`, `CanSellShips`, `CanHaveTradingCompanies`, `HasAccessToMarketplace` | Lumeris (a Lumeris cluster; some also reachable by tech) | :178-180; `DepartmentOfTheInterior.cs:354-356` |
| `CanUsePortals` / `CannotUsePortals` | Vaulters | :202-204 |
| `CanManipulateTimeBubble()` | Riftborn | :470; `DepartmentOfTheInterior.cs:7638` = affinity TimeLords |
| affinity `== AffinityGameplayTemplars` | Nakalim (relics) | no predicate — compared directly |
| `CanGrowMultiplePopPerTurn`, `HasPopulationDiversityEffect`, `KillForeignPopulationOnInvade` | assorted (Horatio/Cravers flavour) | :161, :189, :194 |

Cravers are the odd one out: they have **no** capability predicate. Their signature
(planet depletion) is a *planet* state — `Planet.IsPartiallyOrFullyDepleted()` /
`GetDepletionState()` — reachable by anyone with mining probes too.

## 3. Coverage matrix

Coverage codes: **FULL** = the mod reads this surface explicitly or by a generic mechanism
whose contract covers it; **PART** = reachable but something a sighted player sees is
dropped; **NONE** = not read.

Two generic mechanisms carry most of the load and should be understood before reading the
rows: `SidePanels.Drawn`/`Readouts` (`ES2Access/UI/SidePanels.cs:68,187`) reads whatever
side panels the window is drawing, by widget-tree shape, so a faction-only side panel needs
no per-faction code; and `TooltipFeatures.Read` (`ES2Access/UI/TooltipFeatures.cs:66`) has a
default banding reader, so every faction-only `PanelFeature*` is read without a rule.

### 3.1 Base-game majors

| # | Surface (game class) | Host screen | Gate | Mod today | Sev. | Fixture |
|---|---|---|---|---|---|---|
| **United Empire** | | | | | | |
| 1 | — none — the affinity adds no GUI of its own; it is the baseline every other row deviates from | — | — | — | — | — |
| **Sophons** | | | | | | |
| 2 | `PanelFeatureOmniscience` (tech tooltip block: what omniscience refunds on this tech) | Research wheel tech tooltips | `DepartmentOfScience.HasOmniscience` (`PanelFeatureOmniscience.cs:44`) | FULL via `TooltipFeatures` default reader | low | Sophons, any turn with an unresearched tech |
| 3 | `TechnologyItem2.AffinityGroup` / `AffinityIcon` / `AffinityTooltip` — the marker saying "this tech has an unlock specific to YOUR affinity", preferring the player's own gameplay affinity | Research wheel, every tech node | `affinityNames.Count > 0` (`TechnologyItem2.cs:290-316`) | **NONE** — `ResearchScreen` reads `item.Tooltip` and `item.GuiTechnology` only (`ES2Access/Screens/ResearchScreen.cs:302-329`); the affinity icon has its own separate tooltip | med | any faction (content differs per faction) |
| **Cravers** | | | | | | |
| 4 | `PlanetDepletionStatusItem` on the management planet card (depletion state, turns left, points/max, reverting-vs-ongoing) | Star system management page, per planet card | `Planet.IsPartiallyOrFullyDepleted()` (`PlanetLabel_SystemManagement.cs:80,1321-1332`) | **NONE** — the mod's card reads an explicit field list that omits it (`ES2Access/Screens/SystemManagementScreen.cs:512-521`) | **high** | Cravers, a colony some turns in; or any faction with a mining probe landed |
| 5 | `PlanetLabel_PlanetOverview.DepletionLine`/`DepletionLabel`/`DepletionTooltip` | Planet page | same predicate (`PlanetLabel_PlanetOverview.cs:41-45,230-240`) | FULL *if* `DepletionLine` is a child of `ContentTable` — `PlanetOverviewScreen` walks that table generically (`ES2Access/Screens/PlanetOverviewScreen.cs:512`). **Verify live.** | med | as row 4 |
| 6 | `PanelFeaturePlanetFullDepletionEffects` (tooltip: what a fully depleted planet loses) | planet tooltips | tooltip class `PlanetWithFullDepletion` (`PlanetDepletionStatusItem.cs:30-37`) | FULL via `TooltipFeatures` | low | a fully depleted planet |
| 7 | `GovernmentDictatorshipCravers` government descriptor | Government screen | `Senate.cs:148,1632` | FULL — a government item read as drawn | low | Cravers |
| **Horatio** | | | | | | |
| 8 | `GeneManagementShortcutPanel` in the HUD (assimilation countdown / ready icon + shortcut to the population screen) | Galaxy HUD, over every view level | `InspectedEmpire.HasGeneHunter` (`GameOverlayWindow.cs:414-427`) | FULL (`ES2Access/Screens/GlobalHud.cs:715-756`) | — | Horatio |
| 9 | `PopulationCensusPanel.GeneManagementButton` **replacing** `PopulationDetailsButton` | Senate screen, census board | `HasGeneHunter` (`PopulationCensusPanel.cs:62-63`) | FULL — both declared, whichever is drawn (`ES2Access/Screens/SenateScreen.cs:678-684`) | — | Horatio |
| 10 | `PopulationModalWindow.AssimilationGroup` / `AssimilationEffects` / `AssimilateButton` (the splice itself) | Population screen | `HasAssimilatedPopulation \|\| CanAssimilatePopulation` (`PopulationModalWindow.cs:309-336`) — **not** gated on `HasGeneHunter`; Horatio's difference is being able to reach it without conquest | FULL (`ES2Access/Screens/PopulationScreen.cs:231-234`) | — | Horatio, or anyone with a foreign population |
| 11 | `PopulationAffinityFilter` gene-hunter extras (`AssimilatedGroup`, `ReadyForAssimilationIcon`, "Assimilable" highlight) | Population screen, per affinity filter | `empire.HasGeneHunter` (`PopulationAffinityFilter.cs:87-99`) | PART — the filter row is read, but the two drawn markers are icon-only; unverified whether the shape walk names them | low | Horatio with ≥1 foreign population |
| **Riftborn** | | | | | | |
| 12 | `TimeBubbleStockPanel` + `TimeBubbleItem` (bubbles held, plant one, destroy one) | Galaxy HUD | `DepartmentOfTheInterior.CanManipulateTimeBubble()` (`GameOverlayWindow.cs:430-441`) | FULL (`ES2Access/Screens/GlobalHud.cs:763-802`) | — | Riftborn |
| 13 | `TimeBubbleCursor` — planting a bubble is a cursor mode, not a click | Galaxy | cursor pushed by the panel's click | FULL by the generic cursor-mode announcer (`GlobalHud.AnnounceCursorMode`, :161-205) | — | Riftborn, ≥1 bubble in stock |
| 14 | `PanelFeatureTimeBubbleHeader` / `PanelFeatureTimeBubblesContainer` (bubble tooltips) | tooltips | bubble present | FULL via `TooltipFeatures` | low | Riftborn with a planted bubble |
| 15 | `GalaxyTimeBubble` map object + `NotificationTimeBubbleCreated/Expired/Unused` | Galaxy / notifications | bubble planted | PART — notifications go through shared windows (baseline covered); whether the bubble is discoverable **on the map** is untested | med | Riftborn with a planted bubble |
| **Unfallen** | | | | | | |
| 16 | `GoldenAgePanel` in the HUD (golden-age countdown, colonizer lock timer, locate-the-ship) | Galaxy HUD | `agency.HasSuperColonizer` (`GameOverlayWindow.cs:445-457`) | FULL (`ES2Access/Screens/GlobalHud.cs:808-823`) | — | Unfallen |
| 17 | `PlanetLabel_SystemOrbital.RefreshDisabledUnfallenColonizationButton` — Colonize kept `Visible` but `Enable=false` with `%ColonizePlanetRootedButtonDescription` in its tooltip | Orbital planet card | `CanUseRoots() && !alreadyColonized` (`PlanetLabel_SystemOrbital.cs:610,1269-1284`) | FULL — the mod declares `ColonizeButton` and reads its refusal (`ES2Access/Screens/GalaxyHudScreen.cs:1378`) | — | Unfallen, an uncolonized planet |
| 18 | `StarSystemLabel.PacificConversionButton` Unfallen branch (`CannotConvertRoots`) and `AcademyConversionButton` Unfallen branch | Galaxy system label | `MainColonizedStarSystem.Empire.Faction.Affinity == Unfallen` (`StarSystemLabel.cs:930-941,1309-1319`) | FULL (`ES2Access/UI/SystemLabelReadout.cs:155-164`, `AddRefusable`) | — | any faction next to an Unfallen system |
| 19 | `StarSystemLabel.GoldenAgeIconGroup` | Galaxy system label | golden age active (`StarSystemLabel.cs:2262,2423-2437`) | FULL — it is a child of `HomeAndTradingTable`, which the mod walks (`SystemLabelReadout.cs:111` → `AddTable` → `Say`) | — | Unfallen in a golden age |
| 20 | `LostRootsConnectivityNotificationWindow` — a scrolling table of `LostRootsConnectivityNodeLine`, each a click that flies to the system | Notification popup | roots connectivity lost | PART — **not** in the mod's `Variant` registry (`ES2Access/Screens/NotificationScreen.cs:2447+`), so it falls to the generic drawn-body reading. It is a ninth member of the "header-less line-class table" family the roadmap already tracks, and is **missing from that roadmap list** | med | Unfallen losing a rooted system |
| 21 | `PlanetLabelsWindow_SystemOrbital` super-colonize-ruins branch | Orbital view | `CanSuperColonizeSlumberingRuins` | PART — unexamined this pass; `ContextualIconSlumberingRuins` on the system label IS read (`SystemLabelReadout.cs:96`) | low | Unfallen next to slumbering ruins |
| **Vodyani** | | | | | | |
| 22 | `LifeforceStatusPanel` in the HUD (essence stock/cap/net, ark count) | Galaxy HUD | affinity `== Vampirilis` (`GameOverlayWindow.cs:398-409`) | FULL (`ES2Access/Screens/GlobalHud.cs:694-709`) | — | Vodyani |
| 23 | `PlanetLabel_SystemOrbital.VodyaniHintButton` — replaces Colonize; explains you need an ark in orbit, with `NoMothershipInOrbit` appended | Orbital planet card | `HasMotherships` (`PlanetLabel_SystemOrbital.cs:63,613-616,1286-1305`) | FULL (`GalaxyHudScreen.cs:1382`) | — | Vodyani, an uncolonized planet |
| 24 | `WreckedMothershipLabelWindow` / `WreckedMothershipItem` — a whole galaxy label window for a destroyed ark and reclaiming it | Galaxy | a wreck at the focused node | FULL (`ES2Access/Screens/GalaxyHudScreen.cs:1451-1520`) | — | Vodyani after losing an ark |
| 25 | Ark fleet actions: `FleetActionToggleAttachMothership`, `FleetActionToggleReclaimMothership`, `AttachMothershipFleetAction`, `GuiReclaimMothershipFleetAction` | Fleet panel / fleet actions | ark in the fleet | FULL by generic enumeration of `FleetActionItem` from `FleetActionsTable` (`ES2Access/Screens/FleetPanel.cs:232-297`) | — | Vodyani with an ark fleet |
| 26 | `DockLabel` / `DockingSlotCursorTarget` / `FleetDockingController` — docking ships into an ark's slots | Galaxy | ark present | FULL/PART — `DockLabel` is handled (8 refs in the mod) and `FleetPresence` knows `slot.ContainsAttachedMothership` (`ES2Access/UI/FleetPresence.cs:175`); `FleetDockingController` unreferenced. Verify the drag-into-slot gesture live | med | Vodyani ark with a fleet to dock |
| 27 | `ExploitedStarSystem` — the Vodyani stand-in for a colony (`OrderCreateExploitedStarSystem`, `OrderColonizeExploitedStarSystemPlanet`); `StarSystemLabel.cs:1995-1997` takes the population count off the attached ark | Star system page + galaxy label | ark attached | PART — the label's own text is read, so counts arrive; whether the management page is *coherent* for an exploited system (it is neither Outpost nor Colony nor Ghost in `StarSystemScreen.cs:465-476`) is **untested**. Roadmap already flags "outposts/leeching unknown" | **high** | Vodyani with an attached ark |
| 28 | `PlanetLabel_SystemOrbital.HuntingGroundsIcon` — "this planet is decaying", with a different sentence per cause (Vodyani leech / pirates / Unfallen), and **hidden outright for a Vodyani player** | Orbital planet card | `PlanetLabel_SystemOrbital.cs:34,357-380` (`:374` compares the player's affinity to Vampirilis) | **NONE** on the card. The system-level equivalent (`StarSystemLabel.DecayingSystemGroup`, :2340-2373, same per-cause sentences) IS read via `HomeAndTradingTable` | med | any faction with a planet being leeched |
| 29 | `ShipDesignEditionPanel.ArksVisualNoticeLabel` + mothership-design branches (max ark level, essence cost template) | Ship designer | `ShipDesign.IsMothershipDesign` (`ShipDesignEditionPanel.cs:458`) | FULL (`ES2Access/UI/ShipDesignRows.cs:211-213`) | — | Vodyani editing an ark design |
| 30 | `ElectionLocalPanel` writes the **ark's** name into `StarSystemNameLabel` where other factions get a system's (`ElectionLocalPanel.cs:218-222`); `ElectionCarouselMothership` is a 3D mesh, not a widget | Election modal | council owner is a `Mothership` | FULL — the mod reads the label (no per-faction code needed) | — | Vodyani election |
| 31 | `PopulationMothership` — population living on an ark; `StarSystemPopulationModalWindow` mentions it | Population / system-population modal | ark attached | PART — `StarSystemPopulationModalWindow` is already on the roadmap as tutorial-locked | med | Vodyani, ark with population |
| **Lumeris** | | | | | | |
| 32 | `PlanetLabel_SystemOrbital.BuyOutpostButton` — buy an outpost with Dust instead of colonizing | Orbital planet card | `CanBuyOutposts \|\| CanBuyAllOutposts` (`PlanetLabel_SystemOrbital.cs:67,619,644-680`) | FULL (`GalaxyHudScreen.cs:1384`) | — | Lumeris, an uncolonized planet |
| 33 | `EmpireBanner.BuyoutButton` — buy the **current technology** outright from the HUD research line; hidden entirely on `BuyoutIncompatibleAffinity`, else `Enable=false` with the reason in its own tooltip | Galaxy HUD, over every view level | `DepartmentOfTheTreasury.CanBuyoutTechnology` (`EmpireBanner.cs:24,428,470-491`; gate `DepartmentOfTheTreasury.cs:2150-2160`) | **NONE** — `GlobalHud.AddResearch` declares only `ResearchButton` and checks `ResearchGroup`/button visibility (`ES2Access/Screens/GlobalHud.cs:524-564`). The mod already models the *construction* buy-out (`ES2Access/UI/SystemPanels.cs:516-530`, and `docs/empire-screens.md`, "a buy-out button is hidden, not disabled"), so the rule is known and this call site was simply missed | **high** | Lumeris (or any faction after the buyout tech), a technology queued |
| 34 | `ShipsManagementPanel`: `SellButton` **replaces** `ScrapButton` | Military screen, ships board | `ITradingManagementService.CanSellShips` (`ShipsManagementPanel.cs:341-350`) | FULL — both declared (`ES2Access/UI/ShipRows.cs:76-77`) | — | Lumeris with a ship selected |
| 35 | `TradeCompaniesPanel` + `tradeRelatedSidePanels` | Economy screen | `InspectedEmpire.CanHaveTradingCompanies` (`EconomyScreen.cs:129-132,203-210`) | FULL (`ES2Access/Screens/EconomyScreen.cs:402-413` + `SidePanels.Drawn`) | — | Lumeris / anyone with trading companies |
| 36 | `MarketplacePanel` family | Economy screen, Marketplace tab | `HasAccessToMarketplace` (`MarketplacePanel.IsAccessible`) | FULL (`ES2Access/Screens/EconomyScreen.cs:481,909,939,1224`) | — | marketplace unlocked |
| 37 | `StarSystemLabel.cs:3045` — the system buy-out confirmation swaps to `%StarSystemEssenceConversionConfirmation` when the influencer `HasMotherships` | Galaxy system label → message box | `HasMotherships` | FULL — the message box screen reads the game's own sentence | — | Vodyani/Lumeris pair |

### 3.2 DLC majors

| # | Surface (game class) | Host screen | Gate | Mod today | Sev. | Fixture |
|---|---|---|---|---|---|---|
| **Vaulters** (DLC9) | | | | | | |
| 38 | `FleetActionToggleCreatePortal` / `CreatePortalFleetAction` / `UsePortalFleetAction` / `MoveToProbeAction` | Fleet panel | `CanUsePortals` | FULL by generic `FleetActionItem` enumeration (`FleetPanel.cs:232-297`) | — | Vaulters with a fleet |
| 39 | `StarSystemLabel.ContextualIconPortal` / `ContextualIconBlockedFleetPortal` | Galaxy system label | portal present (`StarSystemLabel.cs:62-64`) | FULL (`SystemLabelReadout.cs:90-91`) | — | Vaulters with a portal |
| 40 | `EventFleetUsedPortal` / `QuestExecutionTreeDecorator_FleetUsedPortal` | non-GUI | — | n/a (simulation) | — | — |
| 41 | Vaulters' remaining surface is a fleet action and two icons — **no window or panel is Vaulters-only.** `FailureFlags`/`StarSystemNode`/`HackingOperationCursor` mention Vaulters only for portal pathing | — | — | FULL | — | — |
| **Hissho** (DLC16) | | | | | | |
| 42 | `HonorManagementPanel` + `HonorGaugeSegment` action buttons in the HUD (keii total, one button per unlocked threshold, turns left on a running action) | Galaxy HUD | `InspectedEmpire.CanUseHonor` (`GameOverlayWindow.cs:483-490`) | FULL (`ES2Access/Screens/GlobalHud.cs:861-909`) | — | Hissho |
| 43 | `ElectionModalWindow.HisshosFestivalPanel` (next observance line) | Election modal | `InspectedEmpire.HasHisshosFestivals` (`ElectionModalWindow.cs:29-36,207-215`) | FULL (`ES2Access/Screens/ElectionScreen.cs:279-283`) | — | Hissho election |
| 44 | `StarSystemLabel.HonorActionSystemBonusesIconGroup` / `HonorActionSystemDefenseIconGroup` / `ContextualIconHonorZone` | Galaxy system label | honor action targeting this system (`StarSystemLabel.cs:249-251,2260-2261`, honor branches ~:2461-2478) | FULL — icon groups sit in `HomeAndTradingTable` (walked); the honor zone is read explicitly (`SystemLabelReadout.cs:92,111`) | — | Hissho with a running honor action |
| 45 | Happiness→**obedience** relabelling: `MajorEmpire.cs:31` picks `EmpireObedience`; `GovernmentModalWindow.cs:176-184` and `GovernmentItem.cs:93-101` **hide** the happiness group for a Hissho; `PanelFeatureHappiness`, `GuiTableCellSystemHappiness`, `GuiTableHeader`, `HappinessSidePanelItem`, `EmpireStatusSidePanel.cs:87-89` all swap wording | Government, senate, system pages, tooltips | `CanUseHonor` | FULL — every one of these is a live label or tooltip the mod reads as drawn; nothing structural to add | — | Hissho |
| 46 | Over-colonization warning text swaps (`%OverColonizationWarning*HisshosDescription`) on `FleetActionButtonColonize.cs:45`, `EmpireActionButtonMinorDiplomacy.cs:144`, `PlanetLabel_SystemOrbital.cs:1444`; outpost caption swaps on `PlanetLabel_SystemManagement.cs:35-39,1030-1076` | Orbital/management cards, diplomacy | `CanUseHonor` | FULL — tooltip/label text | — | Hissho past the colony threshold |
| 47 | `PanelFeatureHisshosFestival` + `FestivalIcon` on `ConstructionLine.cs:96-108` and `StarSystemConstructibleItem.cs:81-93` (an extra icon with tooltip class `"HisshosFestival"` on festival constructibles) | Construction queue / constructible list | subcategory match | PART — the constructible row is read; the extra festival icon's own tooltip is likely dropped by the row model. Low stakes (the same fact is in the constructible's main tooltip) | low | Hissho with a festival buildable |
| 48 | `NotificationOutpostLockedWithHonor` | Notification popup | Hissho | PART — shared notification window, so baseline covers it; unsighted | low | Hissho |
| 49 | `OutpostActionsTable` hides the Hisshos/TimeLords/Vodyani outpost-action variants outright via `FailureInfo.ContainsFlag("Discard", …)` | Management planet card, outpost | affinity | FULL and already recorded (`docs/planets.md`, the outpost card's `OutpostActionsTable`) | — | any faction, an outpost |
| **Umbral Choir** (DLC18) | | | | | | |
| 50 | `GhostInfoSidePanel` — 4 controls (`ShipDestinationButton`, `ClearShipDestinationButton`, `PopDestinationButton`, `ClearPopDestinationButton`), each named only by its own tooltip, each carrying a `FormatFailureInfos` refusal | Star system page, ghost system | `StarSystemState.Ghost` (`StarSystemScreen.cs:373,473-476`) | PART — `SidePanels.Drawn` picks the panel up and the shape walk finds wired buttons, but these four are **bare icons with no drawn text**, so their names depend on the tooltip fallback in `Cells.Control`. Needs a live check that each reads as a named, refusable control | med | Umbral Choir with a ghost system |
| 51 | `GhostPopulationSidePanel` (`StarSystemPopulationDetails` + growth gauge for a ghost) | Star system page, ghost system | as above (`StarSystemScreen.cs:361`) | PART — same mechanism; the growth gauge is a bar that draws no number | med | as row 50 |
| 52 | **The Ghost state is a third mode of the star system page**: `StarSystemScreen.cs:465-476` shows `ghostRelatedSidePanels` and hides the constructibles, queue and hangar panels entirely | Star system page | `StarSystemState.Ghost` | PART — the mod tracks the outpost/colony swap (`docs/planets.md`, the outpost card) but has no note about Ghost; the panel model is generic so it probably degrades correctly. **Verify** | med | as row 50 |
| 53 | `StarSystemScreen.SwitchTraitorsModeButton` — toggles the page between *your sleepers' view* of a foreign colony and the owner's; the handler flips `traitorVisionToggled` and rebinds the whole page | Star system page | `StarSystemNode.EmpiresWithTraitors.Contains(playerEmpire) && secondary != null` (`StarSystemScreen.cs:30,453,629,752`) | **NONE** — the mod's system screen reads the constructible/queue/hangar panels, planet labels and side panels, and no window-level button (`ES2Access/Screens/SystemManagementScreen.cs:193-215,936`). `PreviousSystemButton`/`NextSystemButton` are also unread (probably deliberate, given the mod's own system picker) | **high** | Umbral Choir with sleepers in a foreign colony |
| 54 | `PlanetLabel_SystemOrbital.UmbralChoirHintButton` — replaces Colonize | Orbital planet card | `HasGhostSystems` (`PlanetLabel_SystemOrbital.cs:65,617-619,1307-1330`) | FULL (`GalaxyHudScreen.cs:1383`) | — | Umbral Choir, an uncolonized planet |
| 55 | `StarSystemLabel.TraitorCountGroup`/`TraitorCountLabel` (sleepers) and `HauntCirclesTable` (ghost systems) | Galaxy system label | sleepers/ghosts present | FULL (`ES2Access/UI/SystemLabelReadout.cs:52-67,115`) | — | Umbral Choir |
| 56 | `StarSystemLabelHackingBeaconButton` (parked into `BottomButtonsGroup` at runtime) and `LatentHackingBeaconIcon` | Galaxy system label | beacon charging here (`StarSystemLabel.cs:284,2265`) | FULL (`SystemLabelReadout.cs:173-208`, plus the icon via `HomeAndTradingTable`) | — | Umbral Choir with a beacon |
| 57 | `MiningProbe` Umbral branch (`MiningProbe.cs:48`), `PanelFeatureMiningProbe*`, `PlanetLabel_SystemOrbital.MiningProbeButton` | Orbital planet card / tooltips | `Tags.Contains(AffinityGameplayUmbralChoir)` | FULL — the button is declared (`GalaxyHudScreen.cs:1395`), the tooltips via `TooltipFeatures` | — | Umbral Choir with a probe |
| 58 | `HasHiddenHomeSystem` + `EventHiddenHomeSystemDetected` / `NotificationHiddenHomeSystemDetected` | simulation + shared notification | `Empire.cs:166` | PART — shared window, baseline covers it; unsighted | low | Umbral Choir |
| 59 | `SystemTraitorsActionItem` / `TimedSystemTraitorsAction` / `GuiSystemTraitorsAction` / `TraitorsGroup` / `ClearPopulationTraitorStatusSystemAction` / `DestroyTraitorPopulationsSystemAction` — the actions a *victim* takes against sleepers | Star system page (system actions) | traitors present | PART/NONE — unexamined; no mod reference to `TraitorsGroup` or `SystemTraitorsActionItem` | med | any faction with traitors in a system |
| 60 | Custom-faction editor Umbral branches: home-planet droplist swapped (`CustomFactionStartSetupPanel.InitializePlanetUmbralChoir`, :186), population-modifier list swapped (`CustomFactionPanel.cs:328-348`), a hardcoded droplist correction (`CustomFactionPopulationPanel.cs:309-313`) | Custom faction editor | affinity `== UmbralChoir` | FULL — these change only *which options* fill controls the mod already models | — | custom faction on the Umbral affinity |
| **Nakalim** (DLC22) | | | | | | |
| 61 | `RelicManagementPanel` in the HUD (five relic stocks, kept at zero rather than dropped) | Galaxy HUD | affinity `== Templars` (`GameOverlayWindow.cs:495-506`) | FULL (`ES2Access/Screens/GlobalHud.cs:914-956`) | — | Nakalim |
| 62 | `EmpireRelicsSidePanel` + 4 × `RelicSlotItem` — assign/remove relics to the exploration/expansion/diplomacy/political slots | Empire screen, left column | affinity `== Templars` (`EmpireRelicsSidePanel.cs:49-52`) | PART — `SidePanels.Drawn` picks the panel up (`ES2Access/Screens/EmpireScreen.cs:209-216`) and each slot's `button` is wired (`RelicSlotItem.OnClickCb`, :142), **but the slot flips between "assign" and "remove" mode invisibly**: `Update()` sets `AddModeActive` from the stock and the only on-screen sign is which of `AssignRelicsImage`/`RemoveRelicsImage` is drawn (`RelicSlotItem.cs:35,133-135,172-186`). The slot also names itself only with a `GuiSymbol` icon (`FIDSIGroupTitle`). A blind player presses a button that silently means the opposite thing | **high** | Nakalim with ≥1 relic in stock, then ≥1 assigned |
| 63 | `SkillTreeEditionPanel.RelicSkillsGroup` / `RelicSkillItemsTable` / `RelicSkillTreeItem` — a whole second skill tree bought with relics | Hero inspection | `GuiHero.CanSeeBonusSkillTree()` (`SkillTreeEditionPanel.cs:231-234,472-544`) | FULL (`ES2Access/Screens/HeroInspectionScreen.cs:1217-1345`) | — | Nakalim hero |
| 64 | `HeroDetailedCard.RelicsGroup`/`RelicsLabel` | Hero cards everywhere | affinity `== Templars` (`HeroDetailedCard.cs:400-407`) | FULL (`ES2Access/UI/HeroCards.cs:287`) | — | Nakalim hero |
| 65 | `PanelFeatureSystemRelics` (raw relics on a system, in the system tooltip) | System tooltips | raw relics > 0 **and** affinity `== Templars` (`PanelFeatureSystemRelics.cs:73`) | FULL via `TooltipFeatures` | low | Nakalim near a relic system |
| 66 | `StarSystemLabel.ContextualIconTemple`, `AcademyConversionButton` Templars gate (`StarSystemLabel.cs:1230`) | Galaxy system label | temples / Templar influence | FULL (`SystemLabelReadout.cs:95,160-164`) | — | Nakalim |
| 67 | `FleetActionToggleCollectRelics` / `CollectRelicsFleetAction` / `PanelFeatureCollectRelicsEffects` / `GuiStarSystemCollectRelicImprovement` | Fleet panel / tooltips | Templars | FULL by generic fleet-action enumeration + `TooltipFeatures` | — | Nakalim fleet at a relic system |
| 68 | `RelicsCollectionCompletedNotificationWindow` / `RelicsCollectionCanceledNotificationWindow` | Notification popups | relic collection | PART — both **are** registered in the mod's variant table (`ES2Access/Screens/NotificationScreen.cs:2491,2502`) but the roadmap lists "relics ×2" as awaiting a live sighting | low | Nakalim collecting relics |
| 69 | `NotificationOnEmpireRelicSlotLocked`, `NotificationRelicsSpawned`, `EventRelicsReceived` | shared notification windows | Templars in game | PART — baseline; unsighted | low | Nakalim |
| 70 | `AcademyVaultKeeperPanel` (`OrderSetVaultKeeperPolitics`, candidate cards, politics/laws table) | Academy modal | `MustShow()` (`AcademyVaultKeeperPanel.cs:83`) — an Academy-DLC panel, reachable once the Academy is discovered | FULL by shape — `AcademyModalScreen` enumerates `window.panelsTable.Children` and reads each with `SidePanels.Content` (`ES2Access/Screens/AcademyModalScreen.cs:114-143,175-185`); the candidate cards' selectability is unverified | med | Academy discovered, vault keeper vote open |
| 71 | `AcademyDiplomacyGiveResourcesAction.cs:36` — the Academy will accept relics from a Templar empire | Academy diplomacy | affinity `== Templars` | FULL — a drawn action row | — | Nakalim + Academy |

### 3.3 DLC mechanics whose native faction is a major — one line each, per the brief

- **Hacking + sleeper/traitor UI is a DLC18 mechanic, not a faction gate**:
  `ScanOverlayWindow.cs:242` enables `HackingGroup`, `HackingBanner`, `TraitorsBanner` and
  `HackingDashboard` on `dlcService.IsShared(DownloadableContent18.ReadOnlyName)`, and
  `IHackingService.IsHackingAvailable` / `EventMinorAndPirateHackingUnlocked` open it to
  *any* empire. It is Umbral Choir's entire economy, so for that faction the priority is
  total even though the stage is another's. The unmodelled surface (the mod says so itself
  at `ES2Access/Screens/ScanViewScreen.cs:46-47`) is roughly 25 classes:
  `ScanViewWindowHackingDashboard/HackingBanner/TraitorsBanner`,
  `ScanNodeLabelHackingProgramPanel`, `TraitorBannerEmpireItem`, `HackingViewer`,
  `HackingOperation{Line,Cursor,BackdoorItem,OutcomeItem,OutcomeParameterItem}`,
  `HackingProgram{Line,Item,Cursor}`, `PanelFeatureHackingOperation{,Step}`,
  `PanelFeatureHackingProgramCosts`, `PanelFeatureBeaconDisplacement`,
  `DefenseHackingProgramEncounteredNotificationWindow`. The mod already registers
  `HackingOperationOutcomeSelectionNotificationWindow` (`NotificationScreen.cs:2621`).
- **Juggernauts/citadels** (`MilitaryJuggernautStatusSidePanel`,
  `JuggernautSpecializationModalWindow`, `StarSystemLabel.JuggernautCitadelIconGroup`,
  `ContextualIconJuggernautEffects`) are a DLC mechanic with no native faction; the side
  panel arrives through `SidePanels.Drawn` on the military screen and the two icons through
  the label walk, but `JuggernautSpecializationModalWindow` has no mod reference at all.
- **Pirate marks** (`PirateMarkInventoryPanel`, `PirateMarkCursor`,
  `StarSystemLabel.PirateMarkBuyoutButton`) are open to any empire; the HUD panel and the
  label button are already modelled (`GlobalHud.cs:829-854`, `SystemLabelReadout.cs:150`).
- **Marketplace / trading companies** read as Lumeris-flavoured but are tech-gated for
  everyone; already modelled (rows 35-36).

## 4. Prioritized gap list, grouped into plausible stages

Ordered by what a blind player loses. Every item is a surface on a screen the mod already
models, so none needs a new screen — which is why they group into small stages.

**Stage A — four missed controls/readouts on already-modelled screens (highest value per
line changed; no new design)**

1. Row 33 — `EmpireBanner.BuyoutButton` in `GlobalHud.AddResearch`. Declare it beside the
   research line as a refusable control (visible = declared, `Enable` = offered, reason in
   its own tooltip — the exact rule already written down for construction buy-outs in
   `docs/empire-screens.md`, "a buy-out button is hidden, not disabled"). Lumeris cannot buy technology at all today.
2. Row 53 — `StarSystemScreen.SwitchTraitorsModeButton`. A page-level toggle that changes
   what the whole page is about; a sleeper player cannot reach the infiltrated view.
3. Row 4 (+5) — `PlanetDepletionStatusItem` on the management planet card. Cravers' core
   feedback loop and the only report of a mining probe's damage on that card.
4. Row 28 — `HuntingGroundsIcon` on the orbital planet card (per-cause decay sentence).
   Cheap: one more `Say`-style readout beside the existing card details.

**Stage B — Nakalim relic assignment (a semantics bug, not a missing widget)**

5. Row 62 — `RelicSlotItem`'s invisible add/remove mode. The slot must announce which of
   the two things pressing it will do (the game says it only by swapping
   `AssignRelicsImage`/`RemoveRelicsImage`) and must name itself (the drawn title is a
   `GuiSymbol` icon token). Same stage: verify the four slots' refusal sentences
   (`%EmpireRelicsSlotLocked`, `NoRelicsToAssign`) survive the shape walk.

**Stage C — Umbral Choir's ghost-system page (needs the DLC fixture)**

6. Rows 50-52 — the Ghost state of the star system page: the two ghost side panels' four
   bare-icon controls, the growth gauge, and a recorded note that Ghost is a *third* page
   mode beside Outpost and Colony.
7. Row 59 — the anti-sleeper system actions (`SystemTraitorsActionItem`, `TraitorsGroup`),
   which affect every faction that gets infiltrated, not just Umbral Choir.

**Stage D — Vodyani ark-as-colony coherence (needs a Vodyani save with an attached ark)**

8. Row 27 — walk the star system page and the galaxy for an `ExploitedStarSystem` and
   decide what, if anything, the model has to change. This is the roadmap's existing
   "outposts/leeching unknown" reduced to one concrete question.
9. Rows 26, 31 — ark docking slots as a drag target, and ark population.

**Stage E — small, per-notification and per-tooltip upgrades (cheap, batchable)**

10. Row 20 — add `LostRootsConnectivityNotificationWindow` to the roadmap's header-less
    line-class table family (it is a ninth member and currently absent from that list), then
    register a variant when it is sighted.
11. Row 3 — `TechnologyItem2.AffinityGroup`'s own tooltip on research nodes.
12. Rows 11, 15, 47, 70 — the icon-only markers: gene-hunter assimilation markers, the
    time bubble on the map, the festival icon on construction rows, the vault-keeper
    candidate cards.

**Fixture prerequisites (check first — several stages are blocked on them)**

- **Which faction DLCs this install owns is unknown from the decompile.** The gates are
  `IDownloadableContentService.IsShared(DLCVaulters | DLCHisshos | DLCUC | DLCTemplars)`.
  The mod's own `ScanViewScreen` doc comment says the test install lacks DLC18 (Umbral
  Choir) — if that is still true, rows 50-59 are unreachable live and Stage C is
  fixture-blocked. Ask the dev server's `/eval` for the four `IsShared` answers before
  planning any DLC-faction stage.
- Saves needed, one per stage: Lumeris (A1, and it also gives rows 32/34/35), Cravers with
  a few turns of depletion (A3), Nakalim with relics in stock **and** one assigned (B5),
  Umbral Choir with a ghost system and sleepers in a foreign colony (C), Vodyani with an
  attached ark (D). Hissho, Vaulters, Sophons, Horatio, Riftborn and Unfallen need **no new
  fixture work** — every one of their surfaces is either already covered or covered by a
  generic mechanism, and the few PART rows are low severity.

## 5. What is reassuring

Of the 69 real surfaces in the matrix (rows 1, 40 and 41 are notes, not surfaces), 44 are
FULL, 19 PART — mostly "a generic mechanism covers this but nobody has watched it live" —
and **5 are NONE** (rows 3, 4, 28, 33, 53), plus `JuggernautSpecializationModalWindow` from
§3.3. Three design decisions taken for other reasons are what bought that:

- `GlobalHud.AddFactionPanels` (`ES2Access/Screens/GlobalHud.cs:672-956`) models all seven
  faction HUD panels and asks the *game* which are drawn rather than re-deriving affinities
  — so seven factions' core readouts arrived in one stage.
- `SidePanels.Drawn` reads whatever the side-panel window holds, so `EmpireRelicsSidePanel`,
  the two ghost panels and the juggernaut panel needed no per-faction code.
- `TooltipFeatures`' default banding reader means every faction-only `PanelFeature*` is read
  without a rule being written for it.

The failure mode that remains is uniform and worth naming: **the gaps are all at
window-level fields and explicit field lists.** Every NONE row is a widget hanging off a
window class (`EmpireBanner.BuyoutButton`, `StarSystemScreen.SwitchTraitorsModeButton`,
`TechnologyItem2.AffinityGroup`) or a field absent from a hand-written per-card list
(`PlanetDepletionStatusItem`, `HuntingGroundsIcon`) — never something a generic walk was
asked about and got wrong.

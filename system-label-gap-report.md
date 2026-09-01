# Normal-view galaxy gap audit (scan OFF)

Fixture: turn 32, Neurrone, camera home focus, screen 1280x800. All rows are my own
live measurement or a decompiled read; no doc claims.

## 1. StarSystemLabel elements (three labels: Dusay = colony, Heka = unowned w/ deposits, Libra = unowned)

Measured with an effective-alpha walk (`Visible` AND the product of `Alpha` up the parent
chain) at zoom step 9. "no instance" = the prefab field exists, nothing in this fixture sets it.

| Element (source) | Game draws it here | Mod says it (where) | Verdict |
|---|---|---|---|
| System name (`StarSystemNameLabel`) | Dusay / Heka / Libra / Rigel | announcement + buffer | covered |
| Name background tint = owner colour (`StarSystemNameBackground.TintColor`) | Dusay RGBA(.118,.431,.784); Libra neutral grey | announcement "Home System, colonized" / "No owner" | covered |
| System level frame (`StarSystemNameLevelFrame`) | `Visible=false` on every painted label | — | fixture-blocked |
| Population count (`PopulationCountLabel`) | Dusay "5" | buffer "5 population" | covered |
| Traitor count (`TraitorCountGroup/Label`) | no instance (0 traitors; group's parent line is hidden on non-colonies) | — | fixture-blocked |
| Construction image (`ConstructionImage`) | Dusay `StarSystemImprovementApproval1Small` | buffer "Building Infinite Supermarkets" | covered |
| Construction turns (`ConstructionTurnsLabel`) | Dusay "1" | buffer "1 turns" | covered |
| Queued-construction **tooltip** (class-backed, `guiConstructible.TooltipClass`) | promised at rect 730,-74,50,16 | nothing — `Coverage` bucket `uncovered`, `where: QueuedConstructionGroup` | **MISSING** |
| No-construction cross (`NoConstructionCross`) | no instance (queue non-empty) | — | fixture-blocked |
| Deposits main table (`DepositsMainTable`) | Heka: `Luxury06Small` + `Luxury03Small`, both `Enable=true` | buffer "Transvine, exploited" / "Dustciduous Trees, exploited" | covered |
| Deposits secondary table | no instance (count <= main capacity) | (same bind path) | fixture-blocked |
| Home icon (`HomeSystemIconGroup`, empire-tinted) | Dusay crown above the name | announcement "Home System" + buffer "This is a faction's home system" | covered |
| Rest of home-and-trading line (academy, marketplace, trading company, decaying, honor bonuses, honor defense, golden age, juggernaut citadel, metaplot battle rules, latent hacking beacon) | no instance | — | fixture-blocked (code-only) |
| Empire colour bars (`EmpireColoredBarsTable`) | 1 bar per label, and only at steps 2-5 (alpha 0 at 6-12) | owner named in the announcement | covered (single owner); multi-owner stack fixture-blocked |
| Haunt circles (`HauntsLine`) | no instance | — | fixture-blocked |
| Planet circles (`PlanetCirclesTable`) | Dusay 3, Heka 4, Libra 2, Rigel 2 | child rows per planet: name, size/type, status, 5 FIDSI values, curiosities, moons | covered |
| Left contextual icons: battle, portal, blocked-fleet portal, honor zone, wonder, detection probe, temple, slumbering ruins | none set; the GROUP is alpha 1.0 at every zoom step | — | fixture-blocked (code-only) |
| Right contextual icons: invasion, siege, juggernaut effects, blackout | none set | — | fixture-blocked (code-only) |
| Pirate mark + buyout button (`PirateMarkGroup`) | `PirateMarkGroup.Visible=false` on all four | — | fixture-blocked |
| Pacific / academy conversion buttons, `GivenToAcademyGroup`, `PacificConversionGauge` | not drawn | — | fixture-blocked |
| Diplomacy button + minor-relation gauge | alpha 0.0 at every step | — | fixture-blocked |
| Garrison buttons (`DualGarrisonsButtons`) | Dusay's is in-hierarchy at **alpha 0** -> unpainted | — | fixture-blocked (see note below) |
| Rebellion group, king-of-the-hill table, exploration winner | not drawn | — | fixture-blocked |
| Request-management-view button | in hierarchy | "Manage system" child row | covered |

`Coverage(true)` also reports 5 `GarrisonsLabelButton` "uncovered" tooltips. All five sit at
NaN / 10918 / -33238 rects: pooled, unpainted entries. Not a live gap.

## 2. Rest of the normal view

| Surface | Game draws here | Mod says it (where) | Verdict |
|---|---|---|---|
| `ConstellationLabelsWindow` | step 1 only: "Serpens" + "+15% [food]", "Corvus" + "+15% [industry]" | constellation rows say name + "expanded" only | **MISSING (constellation bonus)** |
| `StarSystemLabelsWindow` | see §1 | see §1 | see §1 |
| `FleetLabelsWindow` | 45 visible nodes, ship-count texts "1","1","1" | fleet rows under systems and lanes ("1 Fleet, 2nd Paragons Navy") | covered |
| `DockLabelsWindow` | 28 visible nodes, texts "3","1" | "Hangar (Dusay), 1 ships" child row | covered |
| `ProbeLabelsWindow` | 7 nodes, "1[turn]" | `galaxy:probe/1868` node exists (TooltipParity lists it under `unknown`, not a defect bucket) | covered |
| `HangarLabelsWindow` | 11 nodes, "1" | hangar child row | covered |
| `ObliteratorProjectileLabelsWindow` | 3 nodes, no text = no instance | — | fixture-blocked |
| `CoordinationRequestLabelsWindow` | 3 nodes, no text | — | fixture-blocked |
| `WreckedMothershipLabelWindow` | shown at step 12, 3 nodes, no text | — | fixture-blocked |
| `PlanetLabelsWindow_SystemOrbital` (step 12 only) | 155 nodes: "Dusay I / Huge Lava / Inhospitable", "Dusay II / Large Desert", "Raia / Medium Terran / Colonized", FIDSI 3\|22\|30\|30\|50 | planet child rows carry name, type, status, all five FIDSI values | covered |
| `GameOverlayWindow` | 88 nodes: "Galaxy View", scan title, 303 dust / 0 manpower / 454 influence / 78 titanium / 5 hyperium / 414 buyout, "Plasma Metallurgy 2 turns" | `hud:empire` (key resources, strategics, research, buyout) + `hud:view-title` | covered |
| `NotificationItemsWindow` | 116 nodes, 9 items | `hud:notifications` stop, 10 rows | covered; per-item tooltips are `uncovered` in Coverage |
| `PinnedQuestWindow` | "Chapter 2. Looking to the Future, Excavating the Past - Part 1" + "Hold at least 3 Systems of Level 2 or higher." | `hud:quest` stop, both lines + "Ongoing" | covered; the panel's own tooltip is `uncovered` |
| `SidePanelsWindow` | 3 nodes, no text — nothing open | — | fixture-blocked |
| `EndTurnWindow` | turn 32, End Turn, clock 11:53 AM | `hud:turn` -> `hud:end-turn` "Turn 32", `hud:real-time-clock` | covered |
| Scene: star lanes | drawn (white lines in every crop) | lane child rows "Starlane 1 to Primus, northeast" (+ fleets on the lane) | covered |
| Scene: influence / territory | drawn | buffer "Influence radius: 7.2, no change next turn" | covered |
| Scene: star disc / star type | drawn | tooltip section "Star System (Blue Star)" + "These stars have a strong chance of harboring Hot planets." | covered |
| Scene: wormhole lanes | no instance in this fixture | — | fixture-blocked |
| Tooltip promised with nothing drawn | — | `hud:empire/research` (both Coverage and TooltipParity `promised`) | pre-existing defect, unrelated to labels |

`Ghosts()`: `shippedUnpainted: 0`, one `droppedByGate` (`chat:new-messages`, not visible). Clean.

## 3. Zooming OUT in normal view (steps 12 -> 0)

Effective alpha per element, measured at each step after a settle. Elements not listed are 0
at every step in this fixture.

| Step | Layer | name | planets | pop+queue | deposits | home | colour bars | ctx-icon groups | Fleet/Dock/Probe/Hangar nodes | Constellation nodes |
|---|---|---|---|---|---|---|---|---|---|
| 12 | SystemOverviewLayer | 0 | 0 | 0 | 0 | 0 | 0 | 1.0 | 45/28/7/11 | 1 |
| 10-11 | SystemLayer | 1 | 1 | 1 | 1 | 1 | 0 | 1.0 | 45/28/7/11 | 1 |
| 6-9 | SystemsLayer | 1 | 1 | 1 | 1 | 1 | 0 | 1.0 | 45/28/7/11 | 1 |
| 4-5 | ConstellationLayer | 1 | 0 | 0 | 0 | 0 | 1 | 1.0 | 42/1/1/1 | 1 |
| 2-3 | InformativeGalaxyLayer | 1 | 0 | 0 | 0 | 0 | 1 | 1.0 | 1/1/1/1 | 1 |
| 1 | GalaxyMapLayer | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1/1/1/1 | 13 |
| 0 | PaintingLayer | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 1/1/1/1 | 1 |

Boundaries — what the eye gains/loses, and whether the mod's tree changes:

- **12 <-> 11**: system labels vanish entirely; `PlanetLabelsWindow_SystemOrbital` turns on (155 nodes: planet names, sizes/types, status, FIDSI). Tree: no change; the planet detail is already in the system's child rows at every zoom.
- **10 <-> 9** (SystemLayer -> SystemsLayer): no measurable element change. Tree: no change.
- **6 <-> 5**: planets, population+queue, deposits and the home line go dark; empire colour bars come ON; dock/probe/hangar labels stop drawing; fleet labels 45 -> 42. Tree: no change — this is the one boundary where a sighted player loses real per-system detail and a tree reader loses nothing.
- **4 <-> 3**: fleet labels stop drawing. Tree: no change.
- **2 <-> 1**: system names and colour bars go off; constellation names + their bonuses come on. Tree: no change, and the bonus is never available (§2, MISSING).
- **1 <-> 0**: constellation names go off; pure art view. Tree: no change.

Tree zoom-independence verified: `/gui/graph?buffers=1` at step 1 vs step 9 differs by 18
lines, all inside `hud:view-title` — the zoom value/band ("2 of 15, Galaxy map" vs "10 of 15,
Systems") and the scan button's label, which the GAME changes with the band ("Diplomacy scan"
vs "Economy scan"). The mod already speaks both. No other per-zoom behaviour is indicated.

## Evidence and limits

- Crops (never re-read): `scratchpad/dusay2.png` (home crown, pop 5, queue image + "1",
  name, 3 planet circles), `scratchpad/heka3.png` (2 deposit icons above "Heka", 4 planet
  circles), `scratchpad/libra2.png` (name + 2 planet circles, upper rows clipped off-screen),
  `scratchpad/dusay_step12.png` (planet labels "Dusay II / Large Desert / Inhospitable"),
  `scratchpad/dusay_step1.png` (lanes only, no label).
- Zoom step used for the §1 deep-dive: 9 (SystemsLayer). Steps 6-11 draw an identical element
  set, so 9 is representative; step 12 draws none of it.
- Fixture cannot show, so **code-only, not MISSING**: all 12 contextual icons, pirate
  mark/buyout, pacific/academy conversion, given-to-academy, rebellion, haunt circles,
  traitor count, no-construction cross, secondary deposits, system level frame, garrison
  buttons, academy/marketplace/trading-company/decaying/honor/golden-age/citadel/metaplot/
  hacking-beacon icons, wormhole lanes, multi-owner colour-bar stacks, obliterator projectile /
  coordination request / wrecked mothership labels, side panels.
- `Rigel` was off-screen (x = -160) and `Libra`'s upper rows clip above y = 0, so the third
  label's non-name rows were read from field values, not from a crop.

State left behind: normal view (scan off), camera home focus [68.884, 0, -22.45] at zoom
step 9 (zoomRatio 1.0 vs the 0.947 found — same step), Dusay row re-collapsed and still the
cursor, `AgeManager.FocusedControl = null`, tooltip delay restored (`now == registry == 0`),
game running.

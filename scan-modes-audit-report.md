# Scan view (scan modes) — live read-only audit

Read-only. No source or doc was changed. Every claim below is a measurement taken in this
session unless it is explicitly marked *unverified* or *not reachable in this fixture*.
Existing docs were treated as hypotheses and are not cited as evidence.

## Fixture

`[Beginner] access test`, turn 32 (the game reports `Turn 31` on the game object, `Turn 32` on
the end-turn button). Chosen because it is the highest-turn named save and the only one with a
met major empire and multiple colonised systems; the autosaves are turn 24–28 of what looks like
the same run, and everything else is turn 1–11.

Measured contents:

- Player: **Neurrone** (`MajorEmpire`, index 0). Home system **Dusay** at world `(68.9, 0, -22.4)`.
- 15 empires: 4 majors, 9 minors, 1 lesser, 1 pirate.
- Diplomacy: exactly **one met major** — Leaper (AI), `DiplomaticRelationStateColdWar`. St Chaoiver
  and Doria are `DiplomaticRelationStateUnknown` (unmet).
- Galaxy: **86 `GalaxyStarSystem`** objects, **68 `GalaxyLink`** objects, 13 home systems.
- Camera: 13 zoom steps (`ZoomStepsCount = 13`, indices 0–12); the mod's ladder is 15 rungs
  (13 camera steps + system page + planet page).
- Screen 1280x800.
- No hacking: `ScanOverlayWindow.hackingEnabled = false`.

Crops live in the session scratchpad
`…\f17fa586-a950-4d68-934b-2660b71cbe41\scratchpad\`.

## The step → descriptor table (measured)

Read straight off the prefab field `GalaxyViewCameraController.LayerDescriptorNamesByZoomIndex`
and cross-checked against `ILayerService.LayerDescriptorCurrent` at every step
(`ILayerService` is a **global-namespace** type in `Assembly-CSharp`, not `Amplitude.Unity.View`):

| camera step | ladder rung (mod) | descriptor | lens title drawn |
|---|---|---|---|
| 0 | 1 of 15 | `PaintingLayer` | Diplomacy |
| 1 | 2 of 15 | `GalaxyMapLayer` | Diplomacy |
| 2 | 3 of 15 | `InformativeGalaxyLayer` | Trade |
| 3 | 4 of 15 | `InformativeGalaxyLayer` | Trade |
| 4 | 5 of 15 | `ConstellationLayer` | Trade |
| 5 | 6 of 15 | `ConstellationLayer` | Trade |
| 6 | 7 of 15 | `SystemsLayer` | Economy |
| 7 | 8 of 15 | `SystemsLayer` | Economy |
| 8 | 9 of 15 | `SystemsLayer` | Economy |
| 9 | 10 of 15 | `SystemsLayer` | Economy |
| 10 | 11 of 15 | `SystemLayer` | System |
| 11 | 12 of 15 | `SystemLayer` | System |
| 12 | 13 of 15 | `SystemOverviewLayer` | System |
| — (system page) | 14 of 15 | *(descriptor not read live — unverified)* | System management |
| — (planet page) | 15 of 15 | *(descriptor not read live — unverified)* | Planet |

Seven descriptors are reachable from the galaxy camera; the last two lenses are reached by
climbing off the camera ladder into the system page and the planet page (the mod's `ui.right` on
its zoom slider does this).

**`Shown` is not the lens discriminator.** At *every* galaxy step, `DiplomacyScanViewWindow`,
`TradeScanViewWindow`, `EconomyScanViewWindow` and `StarSystemOverviewScanViewWindow` are all
`Shown == true` with `AgeTransform.Alpha == 1`. What changes per band is the per-widget alpha the
layer tables apply *inside* them. Only `StarSystemManagementScanViewWindow` and
`PlanetScanViewWindow` actually toggle `Shown`. Any future code that asks "which lens is up" must
ask the drawn **header**, as `ScanViewScreen.DrawnHeader()` already does, not `Shown`.

## Per band

### Diplomacy — steps 0 (`PaintingLayer`) and 1 (`GalaxyMapLayer`)

**Optically drawn** (`band0-map.png`, `band1-map.png`):

- The painted galaxy backdrop (step 0) / the galaxy map (step 1). No star discs, no system names.
- **Star lanes are drawn** — the whole white lane network is visible at both steps.
- One large **orange circle** ringing the player's home system Dusay (the home-system diplomatic
  circle prefab), with short orange spokes fanning from Dusay to ~4 neighbouring nodes.
- One **red/salmon curved line** running west from Dusay and a second arcing north-east — the
  rendered link from the watching empire's centre to the other empire's centre.
- One empire **name label**, "Neurrone", with the empire emblem, in blue, drawn at the empire centre.
- A small circled node NW of the label with its own line — the other-empire centre marker.

**Windows/sections declared.** `DiplomacyScanViewWindow` has exactly one section prefab:
`ScanViewWindowHeader`. Caption groups declared on its `ScanViewWindowGuiElement`:
`DiplomaticStatus` "Diplomatic Status" (4 items), `EmpireInfos` "Empire Info" (2),
`BattleInfos` "Battle Info" (2). **Zero `ScanViewCheckboxLineGuiElements`.**

**3D scene content (probed by reflection).** Exactly **one** `GalaxyStarSystem` in the whole
galaxy carries `contentForDiplomaticScanViewForHomeSystem` — Dusay
(`empire = Empire 0`, `homeSystemDiplomaticCircleInstance = DiplomaticScanViewHomeSystem(Clone)`,
`linkBetweenAnotherEmpires` **count 1**, `empireCenterChangeEventRegistered = true`). So this save
draws: one home circle, one empire-to-empire line, one other-empire-centre circle.
`DiplomacyScanViewWindow.WatchingEmpire = Empire 0`, `InspectedEmpire = Empire 0`;
`NodeLabelsContainer` has 86 pooled children of which **1 is visible**.

**What the mod's tree carries.** `scan:content` holds exactly one row:
`Dusay, 0, 0, Imperials Neurrone  [scan:diplomacy/535]`. `scan:legend` carries all 12 legend
rows (3 group titles + 8 items + the Caption toggle) — faithful.

**The gap.** The single label row is faithful to the single drawn label. Absent from the tree:
the home-system circle; the diplomatic line to Leaper and what its colour means; the other-empire
centre; the whole star-lane network; every system on the map (none is labelled at this band, so
none exists in the tree at all); and the label's own `RelationIcon` / `RelationGroup` / `BattleLine`
sub-widgets. `ScanViewDiplomacyLabel.SwapToggle` — the control that changes `WatchingEmpire` to
another empire's point of view — is **untestable in this fixture**: it is only enabled when the
label's system belongs to an empire other than the player's, and no other empire's home system is
visible here.

### Trade — steps 2–3 (`InformativeGalaxyLayer`) and 4–5 (`ConstellationLayer`)

**Optically drawn** (`band2-map.png`, `band4-map.png`):

- Systems drawn as **ring circles**, colour-coded (white, orange, blue, red, purple) — roughly 25
  visible in the crop. No star discs at this range.
- A dense **orange/gold trade-route network** connecting systems — this is the band's actual subject.
- Star lanes are still there under the trade lines.
- Exactly **three** systems carry a name label: Sabel, Dusay, Primus. Each label shows the name,
  a row of small planet circles (filled/hollow), and small icons above.

**Windows/sections declared.** `TradeScanViewWindow`: one section prefab, `ScanViewWindowHeader`;
zero checkbox lines. Caption groups: `HackingCaption` "Hacking" (4 items, **1 prerequisite**),
`TraceCaption` "Tracing" (2 items, **1 prerequisite**), `TradeRoutes` "Trade Network" (4 items).

**What the mod's tree carries.** 3 rows — Sabel, Dusay, Primus — as collapsed groups; expanding one
(`ui.right`) yields its planet circles (`Dusay I`, `Dusay II`, `Raia`), each with the game's own
circle tooltip. The focused system row's buffer carries the trade dossier
("The Trade Efficiency of this System is impacted by these factors: 1 Colonized Neighbors (3 max.),
System Level 1, 5 System Population, Governor Dmitri Lenko"). `scan:legend` shows the 5 Trade
Network rows + the toggle; Hacking and Tracing are absent because their prerequisites are unmet.

**Why only three rows, checked.** `ScanNodeLabelsWindow.LabelsContainer` has 86 children;
**21 are `Visible`** but **only 3 have `ContentTable.Alpha > 0`** — the other 18 sit at
`ctAlpha = 0, ctVis = true`. The crop confirms only 3 names are painted. `DevProbe.GateDiff()`
and `DevProbe.Ghosts()` at this band report no gate drops (`onlyGated: []`,
`shippedUnpainted: 0`). So the mod's label walk is **correct**: it matches exactly what the game
paints. This is not a mod defect and not a settling artefact (re-probed after the fade).

**The gap.** The map itself. ~22 unlabelled system circles whose ring colour encodes their trade
status, the entire orange trade-route network, and the star lanes are unrepresented. A player
reading the tree learns three system names and nothing about the network the band exists to show.

### Economy — steps 6–9 (`SystemsLayer`)

**Optically drawn** (`band7-map.png`):

- Star discs (blue/orange glows) for each drawn system.
- **Star lanes drawn prominently** as white lines between systems.
- System labels on a coloured name bar (ownership colour), each with a row of planet circles;
  Dusay additionally has an icons line and a traitor/trade line.
- A large ellipse around Dusay — the influence/territory border.
- Scattered faint blue chevron glyphs whose meaning I did not identify.

**Windows/sections declared.** `EconomyScanViewWindow`: one section prefab,
`ScanViewWindowHeader`; zero checkbox lines. Its caption groups are **identical to Trade's**:
`HackingCaption`, `TraceCaption`, `TradeRoutes` "Trade Network".

That last fact resolves a suspicion worth recording: at the Economy band the mod's `scan:legend`
reads "Trade Network / Trade Route / Blockaded Trade Routes / Company Headquarters / Company
Subsidiary". That is **not** a stale read — the legend crop (`band7-legend.png`) shows the game
drawing exactly those captions, and the data says Economy declares the same three groups Trade does.

**What the mod's tree carries.** With the camera on Dusay: 7 rows — Rigel, Qarius, Heka, Dusay,
Primus, Libra, Mrk 180 — which is exactly the set of labels the game paints (`ScanNodeLabelsWindow`
dump agrees name for name). Fourteen systems are inside the viewport by
`Camera.WorldToScreenPoint`; the game labels 7 of them.

**The gap.** Seven of fourteen on-screen systems have no row because the game draws them no label.
Star lanes, the territory ellipse, ownership colouring of the name bars, and the label's icon lines
are all absent from the tree.

### System — steps 10–11 (`SystemLayer`) and 12 (`SystemOverviewLayer`)

**Optically drawn** (`band10-map.png`, `band12-map.png`):

- The star as a bright disc, ringed by a heavy black ellipse (the system boundary).
- Coloured **arc gauges** around the system — cyan 77 (Science), yellow 44 (Dust), orange 100
  (Industry) — plus small boxed labels 10 (Influence), 63% (Population), 12 (Food).
- At step 12 a segmented white/grey **population bar** along the top of the ellipse.
- The system name "Dusay" with a `+` info toggle and a trading-score icon above it.
- **Star lanes drawn** — three white lines radiate out of the star at both steps.

**Windows/sections declared.** `StarSystemOverviewScanViewWindow`: one section prefab,
`ScanViewWindowHeader`; zero checkbox lines. Caption groups: `SystemRank` "System's Rank"
(5 items), `Population` "Population" (3 items).

The game's own widgets at this band (`/gui/age`, visible-only): a `FidsiLabelGroup` of six labels
— "10 Influence", "77 Science", "44 Dust", "100 Industry", "12 Food", "63% Population" — and a
`NodeInfoGroup` holding `SystemName` "Dusay", a `SystemInfoToggle`, and a `TradingScoreRating`.
At step 12 four of the six FIDSI labels are positioned off-screen; at step 10 all six are on screen.

**What the mod's tree carries.** One row: `Dusay, group, collapsed  [scan:system]`.
`scan:legend` carries all 11 rows — faithful.

**The gap.** The six FIDSI figures and the trading-score rating are the entire content of this
band and none of them appears as a row (the source has a `scan:system/output/i` id, so they may be
inside the collapsed group; I did not expand it at this band — **unverified**). The
`SystemInfoToggle`, the population bar, the system boundary, the star lanes and every neighbouring
system on screen (Tercana and Libra were both inside the viewport at step 10) are absent.

### System management — ladder rung 14 of 15

**Optically drawn** (`band-sysmgmt.png`): three planet discs rendered large, each with a FIDSI
pie-chart and a population-synergy icon strip; a "Trade Factors / Efficiency" panel across the
top; a "Hero efficiency / Hero output" panel at the bottom with the hero portrait; the captions
panel down the left; blue trade-route curves entering from the right edge.

**Windows/sections declared.** `StarSystemManagementScanViewWindow`: one built section,
`ScanViewWindowHeader`; zero checkbox lines. Caption groups: `Trading` (2), `Planets` (4),
`Hero` (1).

**What the mod's tree carries.** The richest band by far: `scan:content/trade` (Trade Factors,
1 Colonized Neighbors (3 max.), System Level 1, 5 System Population, Governor Dmitri Lenko,
Efficiency), `scan:content/hero` (hero efficiency + output), `scan:content/planets` (Dusay I,
Dusay II, Raia with per-planet FIDSI, hospitability and population synergies), and all 11 legend
rows. Everything drawn as text is carried.

**The gap.** Only the wordless picture: the blue trade curves at the right edge, and the fact
that the planet discs are rendered images.

### Planet — ladder rung 15 of 15

**Optically drawn** (`band-planet.png`): one full-screen rendered planet under a scanning grid,
with three stat panels — Atmosphere, Structure, Global — and the title "Planet" with an info icon.

**Windows/sections declared.** `PlanetScanViewWindow`: one built section, `ScanViewWindowHeader`;
zero checkbox lines; **no caption groups at all** (which is why no `scan:legend` stop exists here).

**What the mod's tree carries.** All thirteen stat rows across `scan:stats/left/0`,
`scan:stats/left/1` and `scan:stats/right/0`. Full text coverage.

**The gap.** The rendered planet itself and the title's info icon.

## Cross-cutting

### The checkbox banner does not exist in this build

Every one of the six lens windows has `SectionPrefabs.Length == 1`, and the single prefab is
`ScanViewWindowHeader`. `ScanViewWindowCheckboxBanner` exists as a type but has **zero instances**
in the scene. `ScanViewWindowGuiElement.ScanViewCheckboxLineGuiElements` is **null/empty on every
window** (Diplomacy, Trade, Economy, StarSystemOverview, StarSystemManagement, Planet, Battle).

There is therefore **no checkbox to enumerate and none to toggle**. Task 4's "toggle one
representative checkbox, crop, restore" was vacuous and was not performed — there is nothing to
toggle. Any design that assumes a per-lens filter banner is designing for a surface this build
does not ship.

### Hacking / traitors / notification surfaces

`ScanOverlayWindow` field-by-field (reflection, at the diplomacy band):

| field | state |
|---|---|
| `CaptionsPanel` (`ScanViewWindowCaptionsPanel`) | visible, alpha 1, rect [10,10,176,424] — the legend the mod models |
| `HackingGroup` (`AgeTransform`) | visible but **height 0** — it is the collapsed container the captions panel lives inside |
| `HackingBanner` | **not visible** |
| `TraitorsBanner` | **not visible** (prefab placeholder text reads "Traitors: 5") |
| `HackingDashboard` | **not visible** (rect [250,10,218,70]; child menus `DefensiveProgramMenuTable`, `OffensiveProgramMenuTable`) |
| `ScanNotificationPanel` (`ScanViewScanNotificationItemsPanel`) | present at [1070,10,200,28], `ContentTable` empty — no scan notifications pending |
| `GameOverlayTooltipPanel` | present; carried the text "No visible path could be found to this node…" |
| `hackingEnabled` | **false** |

So on this install the hacking family is gated off at the source (`hackingEnabled == false`;
`IHackingService` resolves to a `HackingManager`, and the Trade/Economy caption groups
`HackingCaption` and `TraceCaption` each carry one `Prerequisite` that is not met — consistent
with the "Vaulters"/hacking content not being active here). Had they been on, `HackingBanner`
would show the hacking summary, `HackingDashboard` the offensive/defensive program menus,
`TraitorsBanner` a traitor count, and the Trade/Economy legend would gain the "Hacking" (4 items)
and "Tracing" (2 items) groups. **What each would actually contain is unverified** — the fixture
cannot draw them.

`ScanNotificationPanel` would carry `ScanNotificationItem` entries; none occurred in this session,
so its contents are unverified.

### Windows that register, and where

- Always present as scene objects and always `Shown` while the galaxy scan lens is up:
  `DiplomacyScanViewWindow`, `TradeScanViewWindow`, `EconomyScanViewWindow`,
  `StarSystemOverviewScanViewWindow`, `ScanOverlayWindow`.
- Present but `Shown == false` on the galaxy view; shown only inside the system page / planet page:
  `StarSystemManagementScanViewWindow`, `PlanetScanViewWindow`.
- `BattleScanViewWindow`: one instance, never shown here, `SectionPrefabs.Length == 0`; it cannot
  apply to the galaxy lens anyway (`IsInGalaxyScanView` excludes battle).
- **`StarSystemOrbitalScanViewWindow` does not register at all**: the type exists in the assembly,
  `FindObjectsOfType` returns **0 instances**, and `Gui.GetGuiElement("StarSystemOrbitalScanViewWindow")`
  returns **null**. Nothing in this build instantiates it.

### Interaction status quo, measured with the scan lens up

**Inspect mode and the scanner do not arm.** With `screen.scan-view` focused, injected one per
request ~0.45 s apart, every map action came back `unconsumed` with no speech:

`galaxy.inspect` → unconsumed · `galaxy.scanCategoryNext` → unconsumed · `galaxy.scanNext` →
unconsumed · `galaxy.scanGoTo` → unconsumed · `ui.summarizeMap` → unconsumed.

(There is no `galaxy.summarizeMap`; the map summary is `ui.summarizeMap`.) So in scan view the
player loses the inspect cursor, the scanner, and the map summary — the three things that make the
galaxy readable — and gets no feedback that they are gone.

**System rows per band, star-lane rows per band.** System rows exist only where the game paints a
`ScanNodeLabel` (or, at the System band, the single focused system):

| band | mod system rows (camera on Dusay) | star-lane rows |
|---|---|---|
| Diplomacy (0,1) | 1 (an *empire* label, not a system row) | 0 |
| Trade (2–5) | 3 | 0 |
| Economy (6–9) | 7 | 0 |
| System (10–12) | 1 | 0 |
| System management (14) | n/a (one system's page) | 0 |
| Planet (15) | n/a | 0 |

**No band carries a single star-lane row anywhere**, while every galaxy band draws the lane network.

**What the zoom ladder announces in-mode** (walked `ui.right` from rung 1 to rung 15, transcript
from `/speech`):

```
1 of 15 → "Zoom, slider, 1 of 15, 2 of 2"
2 of 15 → "2 of 15" + "Diplomacy scan"
3 of 15 → "3 of 15" + "Trade Scan"
4 of 15 → "4 of 15"
5 of 15 → "5 of 15" + "Trade Scan"
6 of 15 → "6 of 15"
7 of 15 → "7 of 15" + "Economy scan"
8..10 of 15 → "8 of 15", "9 of 15", "10 of 15"
11 of 15 → "11 of 15" + "System scan"
12 of 15 → "12 of 15"
13 of 15 → "13 of 15" + "System scan"
14 of 15 → "System management scan"
15 of 15 → (screen title becomes "Planet scan")
```

The lens name is re-announced at every *descriptor* boundary, including the two boundaries where
the name does not change (rung 5, rung 13). The band word is suppressed in scan mode, as designed.

**The camera-culling problem is real and severe.** At the Economy band with the camera on Dusay
the tree holds 7 system rows. Moving the camera to `(-10, 0, 30)` at the *same* zoom step:

- the `scan:content` stop **disappears entirely** — not "0 rows", the stop itself is gone;
- with the cursor parked on the "Dusay" row before the move, the mod first re-seats it onto a
  surviving row and says so ("Kais, -35, 33, group, collapsed, 1 of 2"), then when that row also
  goes, drops the cursor back to the zoom slider and says "Zoom, slider, 8 of 15, 2 of 2".

So the map content of every scan band is a function of where the camera happens to point, and a
keyboard player's place in it is destroyed by any camera motion. Nothing tells the player that the
map content vanished because of the camera rather than because there is nothing there.

## Fixture limits — what this save could not show, and what I did not verify

1. **Only one met major empire.** The diplomacy band therefore draws exactly one empire label, one
   home circle and one `LinkBetweenAnotherEmpire`. A save with several met majors would draw a fan
   of lines and several empire-centre circles; that multi-empire picture is unverified.
2. **`WatchingEmpire` swap not exercised.** `ScanViewDiplomacyLabel.SwapToggle` only enables on a
   label whose system belongs to a non-player empire. No such label was visible. The swap's
   behaviour, and what the diplomacy band looks like from another empire's point of view, are
   unverified.
3. **No hacking content.** `hackingEnabled == false`; `HackingBanner`, `HackingDashboard`,
   `TraitorsBanner` never drew, and the `Hacking`/`Tracing` caption groups never appeared. Their
   contents are unverified.
4. **No scan notifications occurred.** `ScanNotificationPanel` was present and empty throughout;
   what it draws is unverified.
5. **No battle.** `BattleScanViewWindow` and the diplomacy legend's "Battle Info / Current battles /
   Completed battles" items were never exercised. The `BattleLine` on `ScanViewDiplomacyLabel` was
   never drawn.
6. **Descriptors at rungs 14 and 15 not read live.** I identified those two bands by the drawn
   header text ("System management", "Planet"); I did not read `LayerDescriptorCurrent` there, so
   the names `SystemManagementLayer` / `PlanetOverviewLayer` are unverified for this build.
7. **The System band's collapsed group was not expanded.** At steps 10–12 the mod declares one
   collapsed `scan:system` group; I did not open it, so whether the six FIDSI figures are inside it
   is unverified. The source carries a `scan:system/output/i` id which suggests they are.
8. **Only one camera framing per band.** Every band was measured with the camera centred on Dusay
   (plus one deliberate off-target framing for the culling test). Bands were not sampled over other
   regions of the galaxy, over unexplored space, or over another empire's territory.
9. **Physical keys untested.** Everything above was driven through `POST /input` and `/eval`;
   nothing was pressed as a real OS key event, so any behaviour that branches on a key being
   physically down is untested here.
10. **The blue chevron glyphs** scattered across the Economy-band crop were not identified.
11. **The tutorial popup** was never expanded or touched; it did not appear in the shown-window
    list at any point, so its state is as-found rather than deliberately restored.

## State left behind

Game **left running** as instructed. Scan view exited (`IsInGalaxyScanView == false`), camera
restored to exactly its as-found framing (`focus [68.884, 0, -22.45]`, `zoomStep 9`),
`AgeManager.FocusedControl` nulled, tooltip delay untouched (`was 0.0 / now 0.0 / registry 0.0` —
it was never set by this session), focus back on `screen.galaxy`.

## Addendum — second pass

Read-only. No source or doc was changed. Everything below is a measurement taken in this
second session (same fixture, same running game) unless marked *unverified*. Where it
contradicts the first pass, the correction is stated explicitly.

### Corrections to the first pass

1. **The "orange trade-route network" at the Trade band is the star-lane network recoloured,
   not trade routes.** `LineRendererManager` holds exactly **69** `LineToRender`, all visible:
   **64 `WarpLineMat`** (star lanes) + **4 `WormHoleLineMat`** + **1 `DiplomaticScanViewLine`**.
   **Zero** lines carry `TradeRouteLineMat` / `TradeRouteBlockadedLineMat` /
   `TradeRouteMixedLineMat`. `TradeRouteRenderer.lineToRenders` (the renderer-field oracle,
   `TradeRouteRenderer.cs:62`, filled in `UpdatePlayerEmpireDependantData`, `:204-300`) is
   **empty**, because `DepartmentOfCommerce.TradingCompanies.Count == 0` and there are **0**
   `TradingRoute`s at turn 32. The mod's `DepartmentOfCommerce` route walk found nothing
   because **there is nothing to find** — not because the walk is wrong. The band's gold lines
   are the same 64 lane segments the Economy band draws in white; the colour comes from the
   per-descriptor shader selectors (`_LineLayer`, `_LayerIndex`, table in §3), not from a
   different renderer.
2. **The blue chevron glyphs are galaxy-background art, not data.** They are present at the
   *same* zoom step with scan view **off** (crops `p2-econ7.png` scan-on vs `p2-normal7.png`
   scan-off, both at step 7, same camera). They encode nothing and are not a scan-mode element.
   First-pass open item 10 is closed.
3. **`ScanNodeLabel`s do not draw at the System band.** `ScanNodeLabelsWindow`'s
   `VisibilityFilter` reads alpha **0** at `SystemLayer` and `SystemOverviewLayer` (table in
   §2). The "Dusay" name visible at steps 10–12 comes from
   `StarSystemOverviewScanViewWindow.NodeInfoGroup` / `NodeNameLabel`
   (`StarSystemOverviewScanViewWindow.cs:17,19`), a different widget.
4. **The two missing descriptor names are confirmed** (first-pass open item 6, partially):
   `GalaxyLayerController.LayerDescriptors` enumerates ten — `PaintingLayer, GalaxyMapLayer,
   InformativeGalaxyLayer, ConstellationLayer, SystemsLayer, SystemLayer, SystemOverviewLayer,
   **SystemManagementLayer**, **PlanetOverviewLayer**, FleetOverviewLayer`. Every scan window's
   own alpha table is keyed on those names, and `StarSystemManagementScanViewWindow` is the one
   window with alpha 1 at `SystemManagementLayer`, so the rung-14 descriptor is settled by the
   data even though I again did not read `LayerDescriptorCurrent` while standing on that page.
5. **`ILayerService` exposes only two `Layer` objects** (`WormholeLayer`, `PlanetLayer`), so the
   `Layer`/`LayerDescriptor` machinery is *not* where per-band content is decided. Per-band
   content is decided in exactly two places: the per-widget `LabelMetaModifier` alpha tables
   (§2) and the per-descriptor shader selectors (§3).

**Methodological caveat that governs every alpha table below.**
`LabelMetaModifier.AnimateToLayer` only writes alpha when its private `hasChangingAlpha` flag
is set (`LabelMetaModifier.cs:240`). Measured: on the `PlanetCirleItem*` and `OwnerCircle*`
modifiers `hasChangingAlpha == false`, so **their `TargetAlphas` lists are dead data** — those
widgets sit at alpha 1 at every band (measured live at steps 4 and 7) and their modifiers only
drive position/anchors/arc radius. A prefab alpha table is a visibility oracle **only** where
`hasChangingAlpha` is true (it is true for the window-level `VisibilityFilter`s and for
`ScanNodeLabel.MainMetaModifier` on `ContentTable`, both of which were seen to move live).

---

### §1 Hacking / traitors surfaces, forced on

#### The gate, read fresh

`ScanOverlayWindow.cs:242`:

```csharp
hackingEnabled = dlcService != null && dlcService.IsShared(DownloadableContent18.ReadOnlyName);
```

`DownloadableContent18.cs:7,12` — `ReadOnlyName = "DLCUC"`, `DownloadableContentType.Exclusive`,
`DownloadableContentSharing.SharedByServer`. It is a **DLC ownership/sharing gate, not a game
option and not a tech**. Measured live: `dlcService` is a `DownloadableContentManager` and
`IsShared("DLCUC") == false` on this install.

What `false` costs (all in `ScanOverlayWindow.cs`): `:249-254` hides the three transforms **and
never adds their `LabelMetaModifier`s to `metaModifiers`** (so they are also never animated to a
layer); `:279`, `:295`, `:307` skip their `OnShow`/`OnHide`/`Refresh`; `:348` collapses
`HackingGroup`'s height to 0. Measured before forcing: `metaModifiers.Count == 2` (the window's
own `VisibilityFilter` plus the scan-notification panel).

The `IHackingService` itself resolves fine (`HackingManager`) and the **data is fully loaded**:
`Gui.GuiWrapperProviderService.GuiHackingPrograms` = **25**;
`SimulationProperties.Empire.MaximumHackingOperationsCount` = **1**; regular operations = 0;
`DepartmentOfIntelligence.GetTraitorsCount()` = 0.

#### The caption groups cannot be satisfied, only bypassed

`ScanViewCaptionGroupGuiElement.Prerequisites` is declared
`[XmlElement(Type = typeof(DownloadableContentPrerequisite), ElementName = "DownloadableContentPrerequisite")]`
— a **DLC prerequisite is the only kind the schema admits**, and
`ScanViewWindowCaptionsPanel.Refresh` drops a group whose `Prerequisite.Check` fails. Measured:
the Trade/Economy caption element carries `HackingCaption` (1 prerequisite), `TraceCaption`
(1 prerequisite), `TradeRoutes` (0). I bypassed by nulling `Prerequisites` on the two groups and
calling `CaptionsPanel.Refresh()`, then restored the arrays (verified back at 1 / 1 / 0).

#### What each surface draws when forced (step 7, Economy band)

Forced by: reflecting `hackingEnabled = true`, setting the three transforms `Visible`, appending
their `LabelMetaModifier`s to `metaModifiers` and animating to the current layer, calling each
section's `OnShow(true)`, then `Dirty = true`. Crop: `p2-hackforced.png` (rect 0,0,500,150).

**`ScanViewWindowHackingBanner`** — rect `[10,10,220,76]`, alpha 1.

| widget | state | bound? |
|---|---|---|
| `ProcessingPowerTitle` | "Bandwidth Allocated: **0**/**55**" | real — allocated stock / `ProcessingPower` max |
| `AllocatedProcessingPowerCellTable` | 0 children | real (nothing allocates) |
| `ProcessingPowerOvercapWarning` | hidden | real (`overcappingAllocationProviders.Count == 0`) |
| `HackingSpeedLabel` | "Hacking Speed: **100** `[hackingSpeed]`" | real |
| `HackingOperationsLabel` | "Hacking Operations: **0**/**1**" | real (count / `MaximumHackingOperationsCount`) |
| `HackingOperationLinesTable` | 0 rows | real (no operations exist) |
| `TraceOperationsGroup` | **hidden** | — |
| `TracingSpeedLabel`, `TraceOperationsLabel` | literally `"%TracingSpeedTitle"`, `"%TraceOperationsCountTitle"` | **placeholder** — the raw loca keys, never refreshed |

**`ScanViewWindowTraitorsBanner`** — rect `[10,86,220,45]`, alpha 1.

| widget | state | bound? |
|---|---|---|
| `TotalCountLabel` | "Sleepers: **0**" | real (`DepartmentOfIntelligence.GetTraitorsCount()`) |
| `TotalRevenueLabel` | **hidden**, text "Total siphon: 45`[dust]` 21.2`[Lux01Redsang]` 12`[Lux02Jadonyx]` 2`[Lux03Dustciduous]` 8.4`[Lux04Bluecap]` 12`[Lux05EdenIncense]`" | **pure prefab placeholder** — proves nothing about real siphon output |
| `TraitorsEmpireTable` | 0 rows | real (no empire has sleepers here) |
| `DetailsToggle` / `ToggleBodyAgeTransform` | drawn as "SLEEPER REPARTITION", **disabled**, tooltip "#E73C3C#You have no Sleepers `[traitor]`.#REVERT#" | real failure text |

**`ScanViewWindowHackingDashboard`** — rect `[250,10,218,70]`, alpha 1. `TitleLabel` = "Hacking
Console"; three `AgeControlToggle`s, all visible and off (`ActiveMode == None`);
`HackingOperationModeToggle` **enabled** with tooltip "Click to enter Hacking Mode. This allows
you to draw the path of your next Hacking Op, …"; the two menu toggles enabled with their own
descriptions. The program menus (`DefensiveProgramMenu` / `OffensiveProgramMenu`) stay hidden
until toggled; opened via `OnToggleMode`, they hold **all 25** `GuiHackingProgram`s — none was
discarded by `ProgramDefinition.CanBeLaunched`:

- **Defensive (11)**: Academy Encrypt · Academy Interrogate · Lockdown · Encrypt · Interrogate ·
  Firewall · Track · Minor Encrypt · Minor Interrogate · Pirate Encrypt · Pirate Interrogate
- **Offensive (14)**: Academy Accelerator ×2 · Academy Overload · Accelerator ×2 · Piggyback ·
  Divert · Overload · Minor Accelerator ×2 · Minor Overload · Pirate Accelerator ×2 ·
  Pirate Overload

**The forced caption groups** (crop `p2-captions-forced.png`) — item titles are the localized
strings; each item carries a colour and an **empty `Description`**:

| group (key) | drawn title | items |
|---|---|---|
| `HackingCaption` (`%TradeScanViewHackingCaptionTitle`) | Hacking | Planned route · Progress next Turn · Current progress · Security Breach |
| `TraceCaption` (`%TradeScanViewTraceCaptionTitle`) | Tracing | Own Trace · Enemy Trace |
| `TradeRoutes` (`%TradeScanViewTradeRoutesCaptionTitle`) | Trade Network | Trade Route · Blockaded Trade Routes · Company Headquarters · Company Subsidiary |

#### Does the mod's legend pick the forced groups up? Yes.

With the prerequisites bypassed, `GET /gui/graph` shows `scan:legend` growing from 6 rows to
**14**, with new regions `scan:legend/0` (Hacking + its 4 items) and `scan:legend/1` (Tracing +
its 2 items), all localized. **The legend is fully data-driven and needs no change for a
DLC-owning install.**

**But nothing else does.** With all three surfaces forced visible and drawing real numbers, the
mod's tree still declared only `scan:title`, `scan:zoom`, `scan:content`, `scan:legend`,
`hud:turn`. **No stop and no row exists for the hacking banner, the traitors banner, the hacking
dashboard, or the scan-notification panel** — on a DLC-owning install a player would lose the
bandwidth gauge, the operation counter, the sleeper count and the whole program console.

#### What a forced show CANNOT prove

- Empty tables prove nothing: operation lines, allocated-power cells and traitor-empire rows
  were all 0-row because no such objects exist. Their **row layout, per-row text and tooltips
  are unverified.**
- Two labels are demonstrably **placeholder** (`%TracingSpeedTitle`, `%TraceOperationsCountTitle`)
  and one is demonstrably **stale prefab text** (`TotalRevenueLabel`'s "Total siphon: 45…").
  A forced show cannot distinguish those from bound content without a case that populates them —
  which is exactly why each is called out above.
- Program lines: only the `Label` was read. Costs, cooldowns, per-program tooltips and the
  disabled/failure states are **unverified**.
- Every hacking widget on the *map* stayed invisible even with `hackingEnabled` forced, because
  no operations/beacons/backdoors/traces exist: `ScanNodeLabel.HackingWaypointIcon`,
  `HackingStartingPointIcon`, `HackingOperationBackdoorsTable`, `DefensiveProgramsPanel`,
  `OffensiveProgramsPanel`, `DefenseHackingProgramEncountersAttacker/DefenderGroup`
  (`ScanNodeLabel.cs:80-101`), and `HackingOperationRenderer.previewLineToRenders` = 0. **The
  whole in-map hacking picture is unverified.**
- `ScanNotificationPanel` still had nothing to show; unchanged from the first pass.

#### Restore

`POST /loadsave "[Beginner] access test"` then `wait-game.ps1 ingame` (12 s). Re-verified:
`hackingEnabled == False`; `HackingBanner` / `TraitorsBanner` / `HackingDashboard` all
`Visible == false`; caption prerequisites back at 1 / 1 / 0; camera identical to as-found
(`focus [68.884,0,-22.45] zoomStep 9`); tutorial window absent from the shown-window list; scan
view off.

---

### §2 Exact per-lens data inventory

#### The master gate table

Measured off the live `LabelMetaModifier`s (all rows here have `hasChangingAlpha == true`).
Alpha per descriptor. `Paint` = PaintingLayer (step 0), `GalMap` = GalaxyMapLayer (1),
`Info` = InformativeGalaxyLayer (2–3), `Const` = ConstellationLayer (4–5),
`Sys` = SystemsLayer (6–9), `Sy` = SystemLayer (10–11), `SyOv` = SystemOverviewLayer (12),
`Mgmt` = SystemManagementLayer (rung 14), `Plan` = PlanetOverviewLayer (rung 15).

| surface (the child named `VisibilityFilter`, one level under the window) | Paint | GalMap | Info | Const | Sys | Sy | SyOv | Mgmt | Plan |
|---|---|---|---|---|---|---|---|---|---|
| `DiplomacyScanViewWindow` | **1** | **1** | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| `TradeScanViewWindow` | 0 | 0 | **1** | **1** | 0 | 0 | 0 | 0 | 0 |
| `EconomyScanViewWindow` | 0 | 0 | 0 | 0 | **1** | 0 | 0 | 0 | 0 |
| `StarSystemOverviewScanViewWindow` | 0 | 0 | 0 | 0 | 0 | **1** | **1** | 0 | 0 |
| `StarSystemManagementScanViewWindow` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | **1** | 0 |
| `PlanetScanViewWindow` | *(no meta modifier at all — `Shown`-gated only)* | | | | | | | | |
| `ScanNodeLabelsWindow` | 0 | 0 | **1** | **1** | **1** | 0 | 0 | 0 | 0 |
| `ScanOverlayWindow` | **1** | **1** | **1** | **1** | **1** | **1** | **1** | **1** | 0 |
| ⤷ `HackingGroup/HackingBanner` | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 0 |
| ⤷ `HackingGroup/TraitorsBanner` | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 0 |
| ⤷ `HackingDashboard` | 1 | 1 | 1 | 1 | 1 | 1 | 1 | **0** | 0 |
| ⤷ `ScanViewWindowScanNotifications` | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 0 |

This is the authoritative "which lens shows what" table: the four galaxy lens windows are all
`Shown` at every step (first-pass finding, re-confirmed), but exactly one of them has a non-zero
`VisibilityFilter` alpha at any descriptor. The overlay (legend + hacking family + notifications)
is the only surface that spans every band, and it drops out only at the Planet page.

#### Diplomacy lens — steps 0–1 (`PaintingLayer`, `GalaxyMapLayer`)

Labels: `ScanViewDiplomacyLabel`, one per node, pooled 86, **1 visible** (Dusay). A label exists
at all only where `HasSomethingToShow` (`ScanViewDiplomacyLabel.cs:100-114`) is true — **a major
empire's home system, or a node with a fleet currently in an encounter**. That, not culling, is
why exactly one label draws in this fixture.

| element (field) | what it encodes | source |
|---|---|---|
| `StarPole` (`:10`) | the vertical pole tying the label to its star; length from camera-to-node distance | `RefreshPositionAndSize`, `:174-182` |
| `MainLinesContainer` (`:12`) | the table the two lines below arrange into | `Refresh`, `:246-259` |
| `EmpireNameLine` / `EmpireNameLabel` (`:14,:20`) | the owning empire's **leader name**, drawn only if `ExplorationState >= 2 && StarSystemNode.IsMajorHomeSystem` | `RefreshEmpireNameLine`, `:302-329`; measured text `"#1E6EC8#[terrans] Neurrone#REVERT#"` |
| `RelationGroup` / `RelationIcon` (`:16,:18`) | the **diplomatic relation state** between `Parent.WatchingEmpire` and the label's empire, as icon + tint (`Gui.GetTexture/GetColor(relationState.Name)`); **hidden when they are the same empire** | `:313-323`; measured **hidden** here (watching empire == label empire) |
| `BattleLine` + `BattleOpponentLeft/RightIcon` (`:22,:24,:26`) | the two sides of an ongoing battle in this orbit: empire emblem + colour of each `encounter.Groups[0/1].Leader` empire, for encounters with `Orbit.GUID == node.GUID` and state `InProgress`/`Setup` | `RefreshBattles`, `:331-355`; **never drawn in this fixture** |
| `SwapToggle` (`:28`) | switches the whole lens to another empire's point of view; `State`/`Enable` from `MainColonizedStarSystem.Empire` vs `Parent.WatchingEmpire`; writes `DiplomacyScanViewWindow.WatchingEmpire` | `:310-312`, `OnSwapCb :410-416`; still **untestable** in this fixture |

Scene renderables at this band (all measured live): the painted galaxy backdrop and the scan
grid; the **64 `WarpLineMat` lane segments + 4 `WormHoleLineMat`** (present at step 0 — the
`_LineLayer` selector reads `(0,0,0,0)` at `PaintingLayer` but the crop `p2-band0.png` shows the
lanes drawn, so that zero vector is a shader *style* slot, not an off switch); **1
`DiplomaticScanViewLine`** (`LineRendererManager` material index 2) — the single
empire-centre-to-empire-centre link; the **home-system circle** prefab instance
(`GalaxyStarSystem.DiplomaticScanViewHomeSystemPrefab`, `GalaxyStarSystem.cs:113`) and the
**other-empire-centre circle** prefab (`:42`), both built out of `CircleRenderer`s
(`CircleRendererManager` holds 109 circles overall).

**Not drawn at this band:** every `ScanNodeLabel` — so no system names, no planet circles, no
ownership rings.

#### Trade lens — steps 2–5 (`InformativeGalaxyLayer`, `ConstellationLayer`)

Labels: `ScanNodeLabel`. Measured at step 4: **21 culled in, exactly 3 with `ContentTable`
alpha 1** — Sabel, Dusay, Primus, the player's three colonies. The mechanism is
`ScanNodeLabel.Refresh` (`:553-557`):

```csharp
bool isImportant = IsImportant;
defaultTradeLayersAlpha = ((!isImportant) ? 0f : 1f);
MainMetaModifier.TargetAlphas[2] = defaultTradeLayersAlpha;   // InformativeGalaxyLayer
MainMetaModifier.TargetAlphas[3] = defaultTradeLayersAlpha;   // ConstellationLayer
```

i.e. **the game rewrites the label's own alpha table per label, per refresh, from `IsImportant`**
(`:261-323`: the player owns a colony or a ghost here, or there is a player hacking
program/beacon/operation/backdoor here, or it is the empire's `BestSystem`). Measured `t2`/`t3`
= 1 only on Sabel/Dusay/Primus; 0 on the other 18. `OnMouseEnterCb` (`:1035`) temporarily raises
them to 1 for a hovered label — so mouse hover reveals content the keyboard cannot ask for.

Every element of the label, and what it encodes:

| element | what it encodes | source |
|---|---|---|
| `NameGroup` / `NameLabel` (`:37,:41`) | the node's `LocalizedName`, or **`"???"` when `ExplorationState < 2`** | `RefreshNameGroup`, `:641-651` |
| `NameBackground` (`:39`) | tinted the **owning empire's colour** when the system is (or has) a ghost colony of the player, else the neutral prefab tint | `:673-687` |
| `RelationBar` (`:43`) | the ownership band under the name, tinted by a 6-way `ScanNodeOwnership`: Own / Ally / Enemy / Pirate / Minor / Neutral, derived from `filteredColonizedStarSystems` + `DiplomaticRelationState` thresholds (`<= ColdWar` ⇒ Enemy, `>= Peace` ⇒ Ally) | `Refresh :472-540`, `RefreshNameGroup :652-672` |
| `StarCircle` (`:26`) *xor* `OwnerCircleTable` (`:28`) | **either** the plain star ring (no colonised empire here, or a hacking waypoint/start marker) **or** one `AgePrimitiveArc` per empire colour, each arc sweeping `360°·i/n … 360°·(i+1)/n` — so a contested system draws a pie of empire colours | `Refresh :551-563`, `RefreshOwnerCircle :831-852` |
| `PlanetsLine` / `PlanetCirclesTable` (`:46,:48`) | one `PlanetCircleItem` per planet, in system order; drawn only when `ExplorationState >= 2 && PlanetsVisibility` | `RefreshPlanetsLine :1010-1024`, `BindPlanetItems :997-1007` |
| ⤷ each planet circle's **state** | `"???"` unknown (`ExplorationState < 4`, tooltip `%PlanetStatusUnknownDescription`) · pirate-lair-uncolonizable · **colonized** (tinted the owning empire's colour) for ColonizedByMe/ByEnemy/Outpost/OutpostBy · colonizable · uncolonizable (Hostile/Unavailable) · destroyed | `PlanetCircleItem.Refresh :86-140` |
| ⤷ per-circle overlays | `UniquePlanetFeedback` (`Planet.IsUnique`) · `MiningProbeFeedback` (mining probe present, tinted its empire) · `TerraformationFeedback` (terraformation in progress, tinted its empire) · `AnomalyReductionFeedback` (anomaly reduction in progress) · `GhostFeedback` (a ghost colonised planet visible to the player) · **`CuriosityAnimatedCircle`** (`SearchableCuriosityCount > 0 && ExplorationState >= 4`) | `PlanetCircleItem.cs:140-222` |
| ⤷ per-circle tooltip | class `"PlanetSimple"` targeted at the `GuiPlanet` — the game's own full planet dossier | `PlanetCircleItem.cs:136-139` |
| `TraitorAndTradeLine` / `Table` (`:53,:55`) | drawn only if at least one of the three below is | `Refresh :566-570` |
| ⤷ `TradeInfrastructureSymbol` (`:57`) | `TradingCompanyHeadquarter` **or** `TradingCompanySubsidiary` icon, when the player has one at this node | `RefreshContextualIcons :690-746` |
| ⤷ `TradeCompanyGroup` / `Icon` / `Name` / `CompanyIncomeGroup` (`:61-67`) | the trading company headquartered here: its definition icon, `LocalizedName`, and one duplet per `ResourceIncome` item | `:697-731` |
| ⤷ `TradeRatingGroup` (`:69`, a `StarSystemManagementScanViewItemRating`) | the system's **trading score**: current vs potential against `scoreRatings`, from `GuiColonizedStarSystem.ComputeTradingScore`; visible only for the player's own colonies | `RefreshTradingScore :896-913` |
| ⤷ `TradeEfficiencyTooltip` (`:71`) | the trade-efficiency dossier (the "Colonized Neighbors / System Level / Population / Governor" text the first pass read) | `:910-912` |
| ⤷ `TraitorCountGroup` / `TraitorCountLabel` (`:73,:75`) | `MainColonizedStarSystem.GetTraitorsCount(playerEmpire)`, hidden at 0 — **measured hidden; the "1" in the widget dump is stale prefab text** | `Refresh :559-565` |
| `BlackoutIcon` (`:77`) | system blacked out, plus a tooltip with `GetBlackoutRemainingTurns()` | `RefreshBlackoutIcon :969-995` |
| `BestSystemIcon` (`:80`) | `DepartmentOfTheInterior.BestSystem` of the owning **major** empire; tooltip text differs by whether the hacking DLC is shared | `:733-753` — **measured visible on Dusay** |
| `HackingSpeedBonusIcon` (`:82`) | the `HackingSpeedIncreaseFromHauntDestruction` descriptor parsed into effect text | `RefreshHackingSpeedBonusIcon :915-967` |
| `HackingIconsTable`, `HackingWaypointIcon`, `HackingStartingPointIcon`, `HackingOperationBackdoorsTable`, `Defensive/OffensiveProgramsPanel`, `DefenseHackingProgramEncountersAttacker/DefenderGroup` (`:84-101`) | the whole in-map hacking picture | `RefreshHackingIcons :748-820` — **all invisible here, DLC absent** |

Scene at this band: the same 64 + 4 lane segments, **drawn gold** rather than white (crop
`p2-trade4.png`); the system ring circles (`CircleRendererManager`, 109 circles); the galaxy
backdrop and grid. **No trade-route lines exist** (see corrections).

#### Economy lens — steps 6–9 (`SystemsLayer`)

Same `ScanNodeLabel` inventory as Trade, with two measured differences:

1. **`ContentTable` alpha is 1 for *every* culled-in label** (`TargetAlphas[4]` is a static 1 and
   is never rewritten): at step 7, all 7 in-frame labels read `ct=1`, including the four unowned
   ones (Mrk 180, Libra, Heka, Qarius, Rigel). This is the band where an unowned system's name,
   ownership bar and planet circles become readable.
2. The lanes are drawn **white** rather than gold (`_LineLayer` moves from slot 0 to slot 1 at
   `SystemsLayer`; §3).

Also at this band: the influence/territory circle around the player's system (a
`CircleRenderer`), the star discs (`SunRenderer` / `DiskRendererManager`). The FIDSI arc gauges
are *not* yet drawn (the `StarSystemOverviewScanViewWindow` filter is 0 here).

#### System lens — steps 10–11 (`SystemLayer`), 12 (`SystemOverviewLayer`)

`ScanNodeLabel`s are **gone** (filter alpha 0). Everything drawn comes from
`StarSystemOverviewScanViewWindow`, which binds the star system nearest the screen centre
(`MaxDistanceToScreenCenter = 350f`, `:11`):

| element | what it encodes | source |
|---|---|---|
| `NodeInfoGroup` / `NodeNameLabel` (`:17,:19`) | the bound system's name | `Refresh :269` |
| `SystemInfoToggle` (`:27`) | opens the info panel; visible only when `InfoPanel.CanShowInfos \|\| RemainsPanel.CanShowRemains` | `:275` |
| `GhostSwapButton` / `GhostsTable` (`:21,:23`) | present when two colonised systems share the node (a ghost); one colour bar per empire | `:279-296` |
| `TradeRatingGroup` + `TradeEfficiencyTooltip` (`:32,:34`) | the same current-vs-potential trading score as the map label | `:299-311` |
| `TraitorsGroup` / `TraitorsLabel` / `TraitorsButton` (`:36,:38,:40`) | traitor (sleeper) count on the bound system plus the removal-action button | `:313-336` |
| `InfoPanel.FidsiLabelsPanel` → **6 `ScanViewSystemOverviewFidsiLabel`** | the band's core content. Labels 0–4 read `NetSystemEmpirePoint` (Influence), `NetSystemResearch` (Science), `NetSystemMoney` (Dust), `NetSystemProduction` (Industry), `NetSystemGrowth` (Food); label 5 reads `PopulationCount / MaximumPopulationCount` as a percentage. **The value is encoded twice**: as the label's text (value + colourised resource symbol) and as its *distance from the star* — `radius = fidsiRenderer.GetFIDSIRadius(value)` at angle `π/5·(2i+1) + π/2`, which is the radius of the coloured arc gauge drawn by `FIDSIRendererManager` | `ScanViewSystemOverviewFidsiLabel.cs:11-19, 71-100`; `FIDSIToRender.cs` |
| `InfoPanel.BarGraph` (`ScanViewSystemEmpireRankBarGraph`) | the system's per-empire rank bars, bound to the `SystemRank` caption group and `guiElement.EmpireRankingProperties` | `ScanViewSystemOverviewInfoPanel.cs:29-33` |
| `InfoPanel.GlobalRankHistogram` (`ScanViewSystemGlobalRankHistogram`) | the galaxy-wide rank histogram | same file |
| `InfoPanel.InformationInaccessibleLabel` | shown when the system's data is not visible to the player | same file |
| `RemainsPanel` (`ScanViewSystemOverviewRemainsPanel`) | the "remains" / wreck panel for the bound node | `:15`, `Refresh :268` |
| the population bar | `FIDSIToRender.CurrentPop / CurrentMaxPop / PopTypeCount / PopCounts[]` (per-affinity population counts) fed to `IFIDSIRendererService.SetFIDSIData` | `FIDSIToRender.cs:16-40`; `GalaxyStarSystem.cs:2109` registers the slot |

Measured FIDSI geometry: at step 10 all six labels are on screen (rects spread ±160 px around
the star); **at step 12 four of the six fly off-screen** (measured x = 1713, x = −317, y = 1249
against a 1280×800 screen) because the radius scales with zoom. The System band's own content is
partly unreadable at its own last step.

Scene: the star disc, the system boundary ellipse and the arc gauges (`FIDSIRendererManager`,
`CircleRendererManager`), and the lanes in the `SystemLayer` / `SystemOverviewLayer` styles.

#### System-management lens — rung 14 (`SystemManagementLayer`)

`StarSystemManagementScanViewWindow` (filter alpha 1 only here). Its per-planet cards are
`PlanetLabel_SystemManagementScanView`:

| element | what it encodes | source |
|---|---|---|
| `PlanetTitle` + `PlanetTitleUnderline` (`:32,:34`) | the planet's name | `PlanetLabel_SystemManagementScanView.cs` |
| `FidsiSectorsGroup` (`StarSystemManagementScanViewFidsiSector`) + `FidsiMiniaturesGroup` | the planet's five FIDSI outputs as pie sectors plus miniature glyphs, from `PlanetFidsiProperties` (`:23`) | same |
| `PlanetStatusGroup` / `PlanetStatusIcon` (`:56,:58`) | the planet's colonisation status icon; caption names `Colonized` / `Colonizable` / `Uncolonizable` (`:17-21`) | same |
| `PlanetGhostStatusGroup` / `Icon` (`:60,:62`) | ghost-colony status | same |
| `SynergiesTable` (`StarSystemManagementScanViewPopulationSynergyItem`) (`:64`) | the population-synergy strip | same |
| `StarSystemManagementScanViewHeroPanel` | hero efficiency + hero output | `StarSystemManagementScanViewHeroPanel.cs` |
| `StarSystemManagementScanViewItemRating` | the trade-factors / efficiency rating panel | `StarSystemManagementScanViewItemRating.cs` |

**Anomalies, curiosities and deposits are NOT on these cards** — the card class has no anomaly,
curiosity or deposit field at all. The only curiosity/anomaly indication anywhere in scan mode is
the `PlanetCircleItem`'s `CuriosityAnimatedCircle` and `AnomalyReductionFeedback` on the map
labels at the Trade/Economy bands (§4).

#### Planet lens — rung 15 (`PlanetOverviewLayer`)

`PlanetScanViewWindow` has **no `LabelMetaModifier` at all** — it is gated purely by `Shown`
(`GuiManager.cs:1576`). Its content is the thirteen stat rows the first pass enumerated; nothing
new was measured. Note the overlay's own filter is **0** at `PlanetOverviewLayer`, which is why
there is no legend, no hacking family and no scan-notification panel on this page.

---

### §3 Within-band zoom-step diffs

**The governing fact:** `LabelMetaModifier` is keyed on the descriptor **name only**
(`AnimateToLayer`, `LabelMetaModifier.cs:233-243`). Two steps that share a descriptor therefore
produce **identical widget alphas**. Everything that changes inside such a pair is camera
distance: which nodes survive `IGalaxyEntityWithCulling.Visible`
(`ScanNodeLabelsWindow.MarkLabelsCullingSafe`, `:338-354`), and label/gauge geometry.

Measured painted-label counts, walking every step in scan view with the camera pinned at
`(68.884, 0, −22.45)`:

| step | descriptor | lens title | `ScanNodeLabel`s culled in | what changed vs the previous step |
|---|---|---|---|---|
| 0 | PaintingLayer | Diplomacy | 21 *(window filter 0 — none painted)* | — |
| 1 | GalaxyMapLayer | Diplomacy | 21 *(none painted)* | **descriptor boundary inside one lens title.** No scan-window alpha changes at all (both descriptors give Diplomacy 1, everything else 0). The change is in the scene shader selectors: `_LineLayer` `(0,0,0,0)` → slot 0, `_LayerIndex` 8 → 7, `_AllLayers1` `(0,0,0,0)` → slot 3. Visually the painted galaxy backdrop gives way to the map style |
| 2 | InformativeGalaxyLayer | Trade | 21 | **lens change.** Diplomacy labels off; `ScanNodeLabels` + Trade window on |
| 3 | InformativeGalaxyLayer | Trade | 21 | **nothing.** Same descriptor ⇒ identical alphas; the same 21 nodes culled in; only the camera is closer |
| 4 | ConstellationLayer | Trade | 21 | **descriptor boundary under one title.** No scan-widget alpha change anywhere (Trade window and `ScanNodeLabelsWindow` are 1 at both). What changes is the scene: `_PlanetLayer` and `_OrbitLayer` go from all-zero to slot 0 — **planet and orbit rendering switches on** — and `_LayerIndex` 6 → 5, `_AllLayers1` slot 2 → slot 1. `_LineLayer` and `_GalaxyLayer` are unchanged, which is why the lanes stay gold |
| 5 | ConstellationLayer | Trade | **17** | **nothing but culling** (4 nodes leave the frustum) |
| 6 | SystemsLayer | Economy | 12 | **lens change.** Trade window off, Economy on; `ContentTable` now paints for every culled-in label, not just the player's three; `_LineLayer` slot 0 → 1 (gold lanes → white), `_GalaxyLayer` slot 0 → 1, `_PlanetLayer`/`_OrbitLayer` slot 0 → 1 |
| 7 | SystemsLayer | Economy | **7** | **nothing but culling** (5 nodes leave) |
| 8 | SystemsLayer | Economy | **6** | **nothing but culling** (1 node leaves) |
| 9 | SystemsLayer | Economy | **4** | **nothing but culling** (2 nodes leave) |
| 10 | SystemLayer | System | 2 | **lens change.** All `ScanNodeLabel`s stop painting; `StarSystemOverviewScanViewWindow` starts; `_LineLayer` slot 1 → 2 |
| 11 | SystemLayer | System | 2 | **nothing.** Same descriptor; same two nodes; the six FIDSI labels move outward as the camera closes |
| 12 | SystemOverviewLayer | System | 2 | **descriptor boundary under one title.** No scan-window alpha change (`StarSystemOverviewScanViewWindow` is 1 at both). `_LineLayer` slot 2 → 3, `_OrbitLayer` slot 0 → 1, `_LayerIndex` 3 → 2. Optically the population bar appears and the FIDSI gauges grow — measured, four of the six FIDSI labels leave the screen entirely |

**Answering the question directly, per boundary:**

- **6 → 9 (inside Economy, one descriptor).** Nothing in the lens changes. The *only* difference
  is how many systems are inside the camera frustum: 12 → 7 → 6 → 4. Zooming in inside this band
  **removes** content and adds none. For a keyboard player the four Economy steps are four
  progressively smaller subsets of the same list.
- **3 → 4 (inside Trade, descriptor boundary under one title).** No widget appears or disappears
  and no row changes. The scene turns planet/orbit rendering on (`_PlanetLayer`, `_OrbitLayer`)
  and the shader ramp advances. Nothing a screen reader could report changes.
- **0 → 1 (inside Diplomacy) and 11 → 12 (inside System)** are the same shape: a descriptor
  boundary that changes only scene shading — plus, at 12, the population bar and the FIDSI
  gauges spreading off-screen.

The per-descriptor shader selectors, measured off the eleven live
`LayerDependantShaderValueController`s (column order = the descriptor order above):

| param | Paint | GalMap | Info | Const | Sys | Sy | SyOv | Mgmt | Plan |
|---|---|---|---|---|---|---|---|---|---|
| `_LayerIndex` | 8 | 7 | 6 | 5 | 4 | 3 | 2 | 1 | 0 |
| `_GalaxyLayer` | s0 | s0 | s0 | s0 | s1 | s2 | s2 | s3 | s3 |
| `_LineLayer` | zero | s0 | s0 | s0 | s1 | s2 | s3 | zero | zero |
| `_PlanetLayer` | zero | zero | zero | s0 | s1 | s2 | s2 | s2 | s3 |
| `_OrbitLayer` | zero | zero | zero | s0 | s1 | s2 | s2 | s3 | zero |
| `_AllLayers1` / `_AllLayersFast1` | zero | s3 | s2 | s1 | s0 | zero | zero | zero | zero |
| `_AllLayers0` / `_AllLayersFast0` | zero | zero | zero | zero | zero | s3 | s2 | s1 | s0 |
| `_AllLayers2` / `_AllLayersFast2` | s0 | zero | zero | zero | zero | zero | zero | zero | zero |

(`sN` = the one-hot slot; "zero" = the all-zero vector. `_LineLayer`'s all-zero at
`PaintingLayer` is **not** an off switch — the lanes are visibly drawn at step 0, crop
`p2-band0.png`.)

---

### §4 What scan mode hides from the normal view

**One line of code hides all of it** — `GuiManager.UpdateGameWindowsVisibility`:

```csharp
// GuiManager.cs:1555
bool flag = !IsAnyScreenVisible && !IsInScanView && !IsInBattle && !IsInGroundBattle;
```

and every normal-view map-label window is gated on it (`:1556-1567`). Scan mode alone flips
`flag` false, so those windows are **hidden outright** (`Gui.GuiService.HideWindow`), not merely
faded or culled. Separately `:1538-1542` gate `GameOverlayWindow`, `SidePanelsWindow`,
`NotificationItemsWindow` and `PinnedQuestWindow` on `isInNormalView`, and `:1584` sets
`IGuiNotificationService.CanShowNotifications = !IsInScanView && …`.

Measured, at every one of the 13 zoom steps with scan view on, all of these read
`Shown == false` and every pooled label under them `Visible == false` — versus `Shown == true`
with live instances at the same steps with scan view off.

| element (window · label class) | instances in fixture | normal view, step 7 | any scan band (steps 0–12) | whose doing |
|---|---|---|---|---|
| Fleet lozenges / labels (`FleetLabelsWindow` · `FleetLabel`) | 5 | **5 painted** | **hidden** (window `Shown=false`; all 5 labels `Visible=false`) | scan **MODE** (`GuiManager.cs:1559`) |
| Merged fleet labels (`MergedFleetLabels`) | 0 | no instance | no instance; same window, so would be hidden | code says hidden |
| PROBE motes (`ProbeLabelsWindow` · `ProbeLabel`) | **2** (one in flight, `DurationLabel` "1[turn]"; one "24[turn]") | **1 painted** | **hidden** | scan **MODE** (`:1560`) |
| Obliterator missiles (`ObliteratorProjectileLabelsWindow` · `ObliteratorProjectileLabel`) | **0** | window shown, no labels | **window `Shown=false`** at every band | code says hidden (`:1561`); live drawing **unverified — no instance** |
| Coordination-request / ally pins (`CoordinationRequestLabelsWindow` · `CoordinationRequestLabel`) | **0** | window shown, no labels | **window `Shown=false`** at every band | code says hidden (`:1562`); live drawing **unverified — no instance** |
| Normal-view system labels (`StarSystemLabelsWindow` · `StarSystemLabel`) | 86 pooled | **4 painted** | **hidden** | scan **MODE** (`:1557`) |
| ⤷ everything they carry | contextual icons (battle, portal, blocked-fleet portal, honor zone, wonder, detection probe, temple, slumbering ruins, invasion, siege, juggernaut, blackout), pirate mark + buyout button, pacific/academy conversion buttons, empire colour bars, population count, **traitor count**, **construction queue image + turns**, **deposits (main and secondary tables)**, haunt circles, home-and-trading line (`StarSystemLabel.cs:58-215`) | drawn | **all gone** | scan MODE |
| Orbital cards / docks (`DockLabelsWindow` · `DockLabel`) | 2 | **2 painted** | **hidden** | scan **MODE** (`:1558`) |
| Hangars (`HangarLabelsWindow` · `HangarLabel`) | 1 | **1 painted** | **hidden** | scan **MODE** (`:1563`) |
| Constellation names (`ConstellationLabelsWindow` · `ConstellationLabel`) | 3 | 2 visible / 0 painted at step 7 | **hidden** | scan **MODE** (`:1556`) |
| Wrecked mothership (`WreckedMothershipLabelWindow`) | 2 windows, none shown | not shown | not shown | `:1567`, same `flag` |
| Orbital / management planet cards (`PlanetLabelsWindow_SystemOrbital`, `_SystemManagement`) | not shown here | not shown | `_SystemManagement` is **replaced** by `StarSystemManagementScanViewWindow` at rung 14 | `:1565` vs `:1575` |
| **Quest pins** (`PinnedQuestWindow`) | shown | **shown** | **hidden** (`isInNormalView && !IsAnyScreenVisible`) | scan **MODE** (`:1542`) |
| Notification items (`NotificationItemsWindow`) + notification auto-popup | shown | shown | **hidden**, and `CanShowNotifications` forced false | scan **MODE** (`:1541`, `:1584`) |
| HUD side panels + game overlay (`SidePanelsWindow`, `GameOverlayWindow`) | shown | shown | **hidden** | scan **MODE** (`:1538-1539`) |
| End-turn window (`EndTurnWindow`) | shown | shown | **still shown** — the one HUD survivor | `:1540` |
| **Curiosities** | — | on `StarSystemLabel`'s planet circles | **rendered**, via `ScanNodeLabel`'s `PlanetCircleItem.CuriosityAnimatedCircle`, at the **Trade and Economy bands only** (and at Trade only on `IsImportant` labels) | `PlanetCircleItem.cs:203-219` |
| **Anomaly indication** | — | planet circles | only **`AnomalyReductionFeedback`** — an *in-progress reduction*, not the anomaly itself — on the same circles, same bands | `PlanetCircleItem.cs:175-190` |
| Mining probes / terraformation / ghost colonies / unique planets | — | planet circles | **rendered** on the same circles, same bands | `PlanetCircleItem.cs:140-202` |
| **Deposits / strategic-resource indications** | — | `StarSystemLabel.DepositsMainTable` + `DepositsSecondaryTable` (`:201-207`) | **nowhere.** No scan-view surface carries a deposit widget: not `ScanNodeLabel`, not `StarSystemOverviewScanViewWindow`, not `PlanetLabel_SystemManagementScanView` | scan MODE, with no replacement |

**Is any of this ordinary camera-layer behaviour?** No. Every row above was compared at the
*same* zoom step (7) with scan view off and on, and the difference is the window's `Shown` flag
flipping, driven by `IsInScanView` alone. Camera-layer behaviour is a separate, additional
effect — inside a lens it only decides how many nodes are culled in (§3).

---

### §5 Unverifiable remainder, and why

1. **Every hacking surface's populated state.** Forcing proved layout and four bound scalars; it
   could not populate operation lines, allocated-power cells, traitor-empire rows or the trace
   group, and it demonstrably left two labels showing raw loca keys and one showing prefab
   numbers. The in-map hacking widgets (waypoints, starting points, backdoors, program panels,
   encounter groups) never became visible because no hacking objects exist.
2. **Program-line internals.** Only the `Label` of each of the 25 program rows was read; costs,
   tooltips and disabled states are unverified.
3. **`ScanNotificationPanel`** — still empty; unverified (unchanged from the first pass).
4. **Battle content** — no encounter exists, so `ScanViewDiplomacyLabel.BattleLine`, the
   diplomacy legend's Battle Info items and `BattleScanViewWindow` remain unverified.
5. **`SwapToggle` / multi-empire diplomacy** — still one met major and one home-system label;
   unverified (unchanged).
6. **The rung-14/15 descriptors were again not read from `LayerDescriptorCurrent` while standing
   on those pages.** They are settled by the window alpha tables (§2) but not by a live read.
7. **Contested / minor / pirate ownership rendering.** The 6-way `ScanNodeOwnership` colouring
   and the multi-arc `OwnerCircleTable` were read from code; this fixture contains no contested
   node, so only the Own and Neutral branches were seen drawn.
8. **`"???"` unexplored labels.** No unexplored node was inside a scan band's frame during the
   walk, so the `ExplorationState < 2` name branch was not seen drawn.
9. **Obliterator missiles, coordination-request pins, merged fleet labels** — zero instances.
   Their scan-mode hiding is proved from `GuiManager.cs:1559-1562` plus the window `Shown=false`
   measurement, but **live drawing is unverified**.
10. **Physical keys** — again everything was driven through `/eval`, `/input` and `/loadsave`;
    nothing was pressed as a real OS key event.
11. **One camera framing.** All 13 steps were walked with the camera pinned at Dusay. Bands were
    not sampled over unexplored space or another empire's territory.
12. **The forced-caption bypass mutates global GUI data.** I restored the two `Prerequisites`
    arrays and verified them back at 1 / 1 / 0, but a `POST /loadsave` does **not** reload the
    GUI datatable, so that restore rests on the explicit re-verification rather than on the save
    reload.

---

### §6 State left behind

Game **left running**, as instructed. Verified in the final probe:

- scan view **off** (`IsInGalaxyScanView == false`), focused screen `screen.galaxy`, cursor on
  `galaxy:constellation/446/system/535` (Dusay) — as found;
- camera at `focus [68.884, 0, −22.45]`, `zoomStep 9`, `zoomRatio 0.947` — identical to the
  as-found framing;
- `AgeManager.Instance.FocusedControl = null`;
- `DevProbe.TooltipDelay(-1)` → `was 0.0 / now 0.0 / registry 0.0` — never set by this session;
- `ScanOverlayWindow.hackingEnabled == false`; `HackingBanner` / `TraitorsBanner` /
  `HackingDashboard` all `Visible == false`; `metaModifiers.Count` back to 2;
- `HackingCaption` and `TraceCaption` `Prerequisites` restored (1 each), `TradeRoutes` 0;
- fixture reloaded from `[Beginner] access test` mid-session (turn 32); tutorial window absent
  from the shown-window list; no notifications pending; end-turn button reading "Turn 32".

Crops from this pass live beside the first pass's, in
`…\f17fa586-a950-4d68-934b-2660b71cbe41\scratchpad\`: `p2-band0.png`, `p2-trade4.png`,
`p2-econ7.png`, `p2-normal7.png`, `p2-hackforced.png`, `p2-captions-forced.png`.

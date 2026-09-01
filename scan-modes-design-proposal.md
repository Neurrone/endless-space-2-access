# Scan modes — audit summary and design proposal (v2)

> **STATUS 2026-09-01: SHIPPED** — all six stages merged to main; the behavioural contracts
> now live in `docs/interaction.md` and the test recipes; the player handover is
> `scan-modes-manual-test.md`. Everything below stands as the ruled spec of record.
> **Still awaiting the owner**: (1) the manual-test pass (physical keys) and the
> fixture-blocked table in the manual-test file; (2) arming inspect from an owner-group
> heading at levels 1–2 is refused (consistent with the grouping ruling) — rule if a heading
> should arm at its centroid; (3) the shared-lane route lines speak the lane's painted
> colour — RULED 2026-09-01 (playtest): flipped to PER-ROUTE states, "mixed" removed
> entirely (a mixed lane is an open line plus a blockaded line heard together); (4) the
> ghost dot overlay's live sighting is pinned (no ghost in any fixture).

Companion to `scan-modes-audit-report.md` (two measured passes; docs were treated as stale
throughout — every game fact below was measured this session or read fresh from decompiled code,
and claims neither could reach are flagged). Nothing here is implemented; this awaits approval.
v2 supersedes v1 after the second pass corrected three of its premises.

## What a scan mode is (measured)

Scan view is ONE mode; the "scan modes" are lenses selected by the camera's zoom step. All four
galaxy lens windows are `Shown` at every step — the real gate is each window's `VisibilityFilter`
alpha per layer descriptor (the addendum's master gate table). Net effect:

| lens | camera steps | what gates it on |
|---|---|---|
| Diplomacy | 0–1 | DiplomacyScanViewWindow filter |
| Trade | 2–5 | TradeScanViewWindow + ScanNodeLabelsWindow filters |
| Economy | 6–9 | EconomyScanViewWindow + ScanNodeLabelsWindow filters |
| System | 10–12 | StarSystemOverviewScanViewWindow filter (node labels OFF) |
| System management | system page | StarSystemManagementScanViewWindow filter |
| Planet | planet page | PlanetScanViewWindow `Shown` (no filter; overlay/legend off here) |

**Within a lens's interval, nothing a screen reader could report changes.** Modifiers key on the
descriptor name, so steps sharing a descriptor differ only by camera culling (Economy 6→9 shrinks
the culled-in label set 12→7→6→4 and adds nothing); descriptor boundaries under one title (3→4,
0→1, 11→12) change only scene shading (planet/orbit rendering, lane colour) plus, at 12, the
population bar and the FIDSI gauges spreading (four of six off-screen at 1280×800). This
undercuts the premise of the 2026-08-17 "announce every descriptor change, same-name included"
ruling — see decision 5.

Also settled: **there is no trade-route network in this fixture** — the Trade band's gold lines
are the 64 star-lane segments recoloured by shader (zero `TradeRoute*` line materials;
`TradingCompanies.Count == 0`); the Economy band's blue chevrons are background art; there is no
checkbox banner in this build; `StarSystemOrbitalScanViewWindow` never registers; the hacking
family is a DLC gate (`IsShared("DLCUC")`, `ScanOverlayWindow.cs:242`), force-audited in §1 of
the addendum.

## The defects

1. **The map is missing from the tree.** Every band from Trade inward renders systems and star
   lanes; the mod's scan tree carries only lens-labelled systems (3 at Trade — the game's
   `IsImportant` filter, `ScanNodeLabel.cs:553-557`; 7 at Economy — camera culling) and zero
   star-lane rows anywhere. The label walk itself is faithful; the map under it is unmodelled.
2. **The map-reading modes are dead, silently**: inspect, all scanner chords, and the map
   summary return `unconsumed` in-mode; camera motion empties the scan tree and dumps the cursor
   on the zoom slider.
3. **On a DLC-owning install the hacking family is invisible to the mod.** Forced on, the
   banner (bandwidth, hacking speed, operations), the traitors banner (sleeper count), and the
   dashboard (mode toggle + 25 named programs in two menus) all bind real data; the legend picks
   the extra caption groups up automatically, but no stop or row exists for any of the four
   surfaces.

## The model

**Scan mode is the same map wearing a different lens — so the mod keeps the same map,** filtered
by what the lens renders (owner rulings 2026-09-01), with the lens contributing what it actually
adds: its name, per-system readings, the diplomacy drawings, the legend, and (on DLC installs)
the hacking surfaces.

### Reuse mechanism (unchanged from v1, awaiting decision 1)

`GalaxyHudScreen` stays active while the galaxy scan lens is up (gate becomes "normal view OR
galaxy scan lens", still overview level, no screen/modal/loading) — the tree, inspect cursor,
scanner, bookmarks, type-ahead and map summary are then the same code paths, zero copies. The
lens's galaxy-band builders move from `ScanViewScreen` into a composed component (`UI/ScanLens`)
the galaxy page invokes only in-mode; `ScanViewScreen` shrinks to the system/planet pages, where
it already ships the best-covered bands. Rejected alternative (extracting the ~7k-line map
builder so `ScanViewScreen` hosts a second tree) — same end state, far larger refactor, two
screens claiming one world.

### The parity filters (ruled 2026-09-01)

The game hides every normal-view map-label window in scan mode through one gate
(`GuiManager.cs:1555-1567`, `:1538-1542`, `:1584`). The in-mode tree and the scanner follow the
measured element × band table (addendum §4) — ONE table drives both, so they cannot disagree:

- **Dropped in-mode, every band** (game hides them outright): fleet rows (incl. adrift/en-route),
  probe rows, obliterator missiles, ally pins, quest markers, hangar/dock rows, deposit rows
  (deposits survive on NO scan surface), wreck rows, notification strip, pinned quest.
- **Kept from the Trade band inward**: system rows and their star-lane rows; planet rows
  (the lens draws a circle per planet, with colonisation state, unique/mining/terraformation/
  ghost/curiosity/anomaly-reduction overlays and the full "PlanetSimple" dossier tooltip).
- **Diplomacy band (steps 0–1)**: the game renders NO individual systems (no discs, no labels —
  only the painted galaxy, the lanes, and the empire drawings), so the proposed tree there is
  empire-level rows only (see per-lens below); systems return at step 2. Decision 3.
- **The scanner's category ring** skips lens-hidden categories exactly as it skips empty ones
  (same code path). Proposed availability, derived from the same table:

| scanner category | Diplomacy | Trade | Economy | System |
|---|---|---|---|---|
| Systems | — | ✓ | ✓ | ✓ |
| Colonizable Planets | — | ✓ | ✓ | — |
| Unexplored (lanes) | — | ✓ | ✓ | ✓ |
| Anomalies | — | — | — | — |
| Curiosities | — | — | — | — | *(CORRECTED 2026-09-01: the scan dot prefab does not wire `CuriosityAnimatedCircle` — field null, measured live; the normal-view dot prefab does, circle seen VISIBLE on Heka III — so the category is normal-view-only)* |
| Luxury / Strategic | — | — | — | — |
| Contested Influence | — | — | ✓ *(PROVISIONAL — see rider: contest rendering unmeasured anywhere)* | — |
| Fleets / Probes / Ally pins / Missiles / Quest markers | — | — | — | — |

(Anomalies: only an *in-progress reduction* is ever rendered, never the anomaly — category
hidden. Custom categories filter per their constituent selectors.)

- **Focus = hover (RULED 2026-09-01, every scan lens).** The game paints some label content
  only conditionally (Trade's `IsImportant` filter, `ScanNodeLabel.cs:553-557`) but mouse hover
  raises any label's content (`:1035`) — so focusing a row in-mode speaks what hovering it would
  reveal, on every lens. Without this most Trade-band rows would be anonymous rings.
- **Constellations (RULED 2026-09-01): not in the in-mode tree.** The game hides the whole
  constellation-label window at every lens, so the scan-mode tree is FLAT system rows in reading
  order — no constellation groups.
- **Inspect mode** stays available at every band: it reads squares and territory, and the
  Diplomacy band's painted backdrop IS territory — the inspect cell is precisely its reader.

### Per lens: what the game shows → what the mod should say

Exact element-by-element inventories with sources are in the addendum §2; this is the model.

**Diplomacy (0–1).** Renders: painted galaxy + lanes; per qualifying node (`HasSomethingToShow`:
a major's home system, or a battle in orbit) a label with leader name, relation icon, battle
line, watching-empire swap toggle; the home-system circle; and TWO line families
(`GalaxyStarSystem.cs:900-983`): a straight link from the watching empire's centre to each MET
major's centre — the centre being the watching empire's INTELLIGENCE record of where that
empire is (`DepartmentOfIntelligence.GetEmpirePosition`, the same `Known`-gated table the
scanner's homeworld category already reads) — with a circle at the far end; and curved spokes
from each of the watching empire's own systems back to its own centre (the empire's extent).
Mod (RULED 2026-09-01): the band is a LIST OF EMPIRES the player has met, always n ≥ 1 (self
included). Each row carries what the lens draws for that empire: leader/name, relation state in
the game's own word, its centre in the mod's normal coordinate idiom (pair + offset,
`GalaxyCoordinates`) where the game knows one (`EmpirePosition.Known` — discovery-gated: at
least one of their colonies surveyed or sighted; the centre is their home if discovered, else
their highest-influence known colony, `DepartmentOfIntelligence.cs:486-524`). **The centre is
spoken as a position only, never as "home"** — the game draws the same circle and line in both
cases (`EmpirePosition` stores only a position; `LinkBetweenAnotherEmpire` has one prefab), so
naming the case would give the player information a sighted player does not have (ruled). A
**battle row per drawn battle label** joins the region — named by the system, saying the two
sides — since the lens plants a label at any node with a fight in orbit, not just homes. The
**spokes are modelled** (ruled): one set for whichever empire the lens is watching — after a
swap, the watched empire's systems tethered to its centre. **Verified live and optically
(2026-09-01)**: setting `DiplomacyScanViewWindow.WatchingEmpire` to Leaper from the REPL drew 5
spokes, 4 at systems the player has NEVER explored (Muzis, Deneb, Dorado, Cetus), and the crop
pair (`spokes-leaper.png` / `spokes-self.png`) shows the orange spoke curve drawn over the
player's fog while watching Leaper and gone when watching self — the lens is genuinely an intel
tool for locating a watched empire's colonized systems, and the mod mirrors the drawing exactly
(gates keyed to the WATCHED empire's knowledge, `GalaxyStarSystem.cs:926-973`). The swap
TOGGLE's own UI path stays fixture-blocked (a foreign label draws only at a major's explored
HOME; Leaper is known through the colony Kais, its home unexplored). No system tree at this
band; inspect answers "whose space is this".

**Trade (2–5).** Renders: ring circles for every culled-in system (`StarCircle` or the
per-empire `OwnerCircleTable` pie), gold lanes, and full labels for important systems: name
("???" if unexplored), ownership bar (6-way own/ally/enemy/pirate/minor/neutral), planet circles
with per-circle states/overlays/dossier tooltips, trading-company HQ/subsidiary icon + company
income, trading-score dial + efficiency dossier, traitor count, blackout, best-system, hacking
icons (DLC). When companies exist, `TradeRouteRenderer` draws real route lines (open/blockaded/
mixed) — zero here. Mod: system tree rows carry the label content as scan decoration
(ownership wording from the 6-way state, planet circle children, dial dossier, icons), under the
focus=hover ruling for unimportant systems; the shipped `scan:routes` model-read group stays (it
was right — nothing existed to draw) — its per-band gating re-checked when a fixture has
companies.

**Economy (6–9).** Renders: the same labels for EVERY culled-in system (no importance filter),
white lanes, star discs, the territory circle. Mod: same decoration as Trade; territory via
inspect; contested-influence scanner category live here.

**System (10–12).** Renders: node labels OFF; the centre-screen system's
`StarSystemOverviewScanViewWindow` — name, info toggle, ghost swap, trading score, traitors
group + removal button, six FIDSI labels (value spoken twice by the game: text + gauge radius),
rank bar graph + global histogram (geometry; roadmap), remains panel, population bar
(per-affinity counts). Mod: keep the existing `scan:system` group and extend it with the parts
not yet read (traitors group + button, ghost swap, population bar once its per-affinity data is
read from `FIDSIToRender`); the tree continues to hold all systems (discs + lanes still render).
The game's own step-12 defect (four FIDSI labels off-screen) doesn't affect the reading — the
mod reads the window, not the pixels.

**System management (rung 14).** Already faithful (trade factors, hero panel, planet cards:
FIDSI sectors, status, ghost status, synergies). Confirmed: the cards carry NO anomaly,
curiosity or deposit fields — nothing to add. Stays on `ScanViewScreen`.

**Planet (rung 15).** Already faithful (13 stat rows); overlay/legend correctly absent (filter
0). Stays on `ScanViewScreen`.

**Hacking family (all bands, DLC installs; decision 8).** Proposed: model the three overlay
surfaces as a lens-independent scan stop — banner (bandwidth allocated/max, hacking speed,
operations n/max, operation rows), traitors banner (sleeper count, repartition toggle with the
game's failure tooltip), dashboard (mode toggle + the two program menus as rows) — reading drawn
widgets so they simply never appear on this install; the two provably-placeholder labels
(`%TracingSpeedTitle`, the "Total siphon" prefab text) are excluded from any reading until a
real case shows them bound. Verification is fixture-blocked here (roadmap: needs a DLC-owning
session; in-map hacking icons likewise). The legend needs nothing — measured picking the DLC
groups up by itself.

### Keys

No new bindings. Map keys keep working in-mode; Space keeps its claim rules; Escape stays the
game's exit.

## Decisions

Ruled (2026-09-01): parity filtering of the in-mode tree per object kind; the scanner
responding to lens visibility (one measured element × band table drives both); **focus = hover
on every scan lens**; **same-name lens announcements kept**; **hacking family modelled now**
against drawn widgets, verification deferred to a DLC-owning session.

Also ruled (2026-09-01, second round): **reuse mechanism confirmed**; **diplomacy band = the
list of empires the player has met (n ≥ 1, self included)**; **empire centres spoken in the
mod's normal coordinate idiom**; **constellations NOT in the in-mode tree** (the game hides
them at every lens, so parity flattens the tree there).

Also ruled (2026-09-01, third round): **in-mode planet rows are cut to dot parity** — the dot's
tooltip is small (name + status sentences: colonized-by / unique / too-hostile / the
unknown-planet sentence), so the scan-mode planet row says exactly that plus the dot's drawn
overlays (mining probe, terraformation, ghost, curiosity mark, anomaly reduction), not the
normal view's full planet reading. No separate tooltip section (decision 7 closed).

## Normal-view follow-ups (separate small stage)

Two real gaps from the normal-view audit (`system-label-gap-report.md`):
1. The colony label's queued-construction CLASS-BACKED tooltip is promised and never offered
   (`Coverage` bucket `uncovered`, `QueuedConstructionGroup`).
2. The constellation labels at far zoom draw a constellation BONUS ("Serpens +15% food") the
   mod's constellation rows never carry — add it to the row when the label draws it (at other
   zooms it already reaches the player through the constellation tooltip).

## Open: zoom-band parity for the NORMAL view (owner direction 2026-09-01, needs settling)

The owner proposes the normal-view tree should also match what the zoom band renders — at
steps 0–1 (constellations/art only) the tree would show only constellations and their bonuses.
Unifies with the scan-mode rule: ONE per-band kind table, both modes, tree + scanner.
Measured LOD ladder (normal view): 12 planet labels only; 6–11 full system labels
(planets/pop/queue/deposits/home); 4–5 names + colour bars, docks/probes/hangars off; 2–3
fleets off too; 1 constellation names + bonuses only; 0 pure art.
RULED (2026-09-01): **kind-level parity per band, not frustum parity** — rows exist for every
perceived thing of a kind the band renders, camera-position-independent. **Step 0 keeps the
constellation floor** (deliberate deviation from the pure-art band). **The scanner band-filters
in normal view too.** **Expanding a system row never changes the zoom level** — a system row
visible at a band expands in place to exactly the children that band renders, so its
information stays viewable at that zoom. **Lanes are ALWAYS children of systems, at every
band** (the game draws the lane network at every band) — and the existing right-arrow/Enter
lane-following mechanics keep working; a system whose band renders no other child still
expands to its lanes.

**Swap access fact (measured + code, 2026-09-01):** the game's own UI offers the
watching-empire swap ONLY at a major's home system the player has explored
(`ScanViewDiplomacyLabel.cs:304-312` — the toggle lives inside the empire-name line, gated
`ExplorationState >= 2 && IsMajorHomeSystem`; a battle-only label draws no toggle). The
underlying mechanism works for any met empire (REPL-verified), but the mod offers the swap only
where the game draws it — parity.

**Passive label icons become CHILD NODES (ruled 2026-09-01):** the system label's passive icons
are no longer stuffed into the row's buffer — each drawn icon becomes a child node whose focus
raises its own tooltip (the standing tooltip-bearing-widgets-are-nodes rule). Kinds this
converts (child exists only while its icon draws): the 12 contextual icons (battle, portal,
blocked-fleet portal, honor zone, wonder, detection probe, temple, slumbering ruins, invasion,
siege, juggernaut effects, blackout); the standing-icons strip (home, marketplace, academy,
trading company HQ/subsidiary, decaying/outpost-being-lost, honor bonuses, honor defense,
golden age, juggernaut citadel, metaplot battle rules, latent hacking beacon); per-deposit
icons (main + secondary tables); haunt circles; exploration-winner badges; king-of-the-hill;
rebellion; given-to-academy; the traitor count; the construction-queue group — which fixes the
normal-view MISSING queued-construction tooltip for free, since the child points at the widget
and focus raises its class-backed tooltip. The scan label's own icon set (blackout,
best-system, hacking icons) follows the same shape in-mode. Actionable buttons (diplomacy,
buyout, conversions, garrison, manage-system) were already modelled as nodes and are untouched.

**Proposed normal-view band spec (2026-09-01, awaiting sign-off; spoken zoom levels).** The
game's 12↔13 boundary (system nameplates off, per-planet labels on) is a rendering
REAL-ESTATE swap within one subject, not information gating — so the tree treats spoken
levels 7–13 as ONE detail band, which answers the owner's ergonomics point (deposits and the
rest stay present at level 13) without forced zoom, camera side effects, or tooltip unions,
and keeps expand-never-zooms intact. This supersedes the strict-parity recommendation and
retires the zoom-12-union idea. Parity still governs the true subject changes:

| spoken level (camera step) | tree content |
|---|---|
| 1 (0) | constellations + bonuses only (the ruled floor — game draws pure art) |
| 2 (1) | constellations + bonuses (the game draws them here) |
| 3–4 (2–3) | + system rows (name, owner) with lane children |
| 5–6 (4–5) | + fleets |
| 7–13 (6–12) | full system detail: population, construction queue (child with its tooltip), deposit children, passive-icon children, planet children with full readings, docks/hangars/probes, fleets, lanes |
| 14–15 | the system page / planet page, as today |

The scanner's normal-view category filter reads the same table (fleets from level 5,
planet-derived categories from level 7, systems from level 3). Landings/auto-zoom stays
paused until this table is signed off. Scan-mode band shapes stay as ruled earlier in this
file; the two specs share the one per-band kind table mechanism.

**Expand-zoom resolution (2026-09-01, owner-confirmed direction).** Zoom-on-expand is KEPT in
normal view: `ui.right` into a system zooms to level 13 exactly as today, which draws the
orbital cards and so delivers the action buttons as expand-children — dropping it would force
a shift-tab/ladder/tab round trip to reach actions, a regression the owner vetoed.
Expand-never-zooms is SCOPED TO SCAN MODE, where zoom selects the lens and a silent zoom would
change the meaning of everything on screen (and where no orbital cards exist, so nothing is
lost). The governing principle: an expansion may move the camera only as far as its own
content needs to be fully rendered, and only where zoom does not change meaning. Under the
merged 7–13 band the system row is identical across the band, so the jump changes nothing the
tree was saying.

**The consolidated normal-view spec (sighted rendering, tree, scanner per spoken zoom level),
for sign-off. DEV = deliberate deviation from the sighted rendering:**

| level (step) | sighted rendering (measured) | tree, browsing | scanner |
|---|---|---|---|
| 1 (0) | territory-painted galaxy art + lanes; nothing named | constellation rows: name + bonus, presented as collapsed groups — **DEV (the ruled floor)** | none — edge note |
| 2 (1) | constellation names + bonuses; lanes; art | same rows (parity) | none — edge note |
| 3–4 (2–3) | + system names on owner-coloured bars; lanes | + system rows (name, owner) with lane children | Systems, Unexplored |
| 5–6 (4–5) | + fleet lozenges | + fleet rows | + Fleets |
| 7–12 (6–11) | full system nameplates: name, owner tint, population, queue image+turns, deposit icons, home icon, planet DOTS (hover = name + status only), contextual icons; dock/hangar/probe labels; influence ellipse; fleets; lanes. Within 7–12 nothing changes but culling (measured) | full SYSTEM-level detail (population, queue child, deposit children, icon children, docks, hangars, probes, fleets, lanes) plus **planet children at DOT fidelity** — name + status, what the drawn dot and its hover carry (the same shape as scan mode's dot-parity rows); the full planet reading, dossiers and actions live at 13 | all remaining categories (planet dots render, so planet-derived categories are live) |
| 13 (12) | system nameplates GONE; per-planet orbital cards instead (name, size/type, status, FIDSI, action buttons); wreck labels; docks/hangars/probes/fleets/lanes | the 7–12 rows PLUS planet children (full readings + dossiers + actions) — **DEV upward: system-level parts (deposits, queue, population, icon children) kept although the game no longer draws them here** (RE-RATIFIED as option (a) 2026-09-01 after stage 4 shipped paint-parity instead: children's existence keys to the label's BOUND state, not painted alpha, at 13) | all |
| 14–15 | the system / planet pages | unchanged | as today |

Spec riders:
- **Constellations keep their grouping role at every level** (deliberate deviation — the game
  draws their names only at level 2, but they are the map's own organizing concept, their
  tooltip carries the bonus at all levels, and the owner's brief says the tree idiom works;
  contrast the scan-mode flatten ruling, where the game hides the whole window).
- **Expand behaviour (RULED 2026-09-01, the graded model)**: expansion respects the band
  below the detail region and completes the detail inside it. At levels 3–4 a system expands
  IN PLACE to its lane children only; at 5–6 in place to lanes + fleets; no zoom change at
  either — the low bands are genuinely a geometry-reading mode (who connects to whom, where
  the fleets are), skipping planet detail and tooltips. At levels 7–12 expansion forces zoom
  to 13 so the orbital cards (planets, action buttons) draw, while the 7–12-only information
  (deposits, queue, population, icons) stays in the row for convenience (the flagged DEV);
  **collapsing the row restores the zoom the expansion jumped from**. At 13 expansion is in
  place (already there). A constellation at levels 3+ expands in place; at 1–2 expanding
  zooms to level 3 and opens (the rows present as collapsed groups there so the gesture is
  discoverable). The coherence rule the player can hold: THE ZOOM LEVEL TELLS YOU WHAT AN
  EXPANSION GIVES; only inside the detail region does expanding complete the detail, and
  collapse always hands back the view you were browsing.
- **Scanner edge at levels 1–2**: every category is band-hidden; a press must not be silent —
  the existing "none found" idiom answers (verify the all-empty ring live; give it that line
  if it is currently mute).
- **Provisional bands, flagged (corrected 2026-09-01)**: Contested Influence's RENDERING is
  unmeasured anywhere — the fixture has zero contested tiles, and what was measured at level
  7 is the territory ELLIPSE, a different thing — so its band placement (both modes) and its
  inspect-arming special are pinned until a fixture shows contest. Quest markers / ally pins
  / missiles at 7+ pending a live instance (their windows' zoom alphas unmeasured).
- **Bookmarks are mod-authored annotations, not game renderings** — present at every level.
- The two normal-view fixes ride along: the queued-construction tooltip (free via the
  icon-children conversion) and the constellation bonus on the row.
- **Slider band words (owner-approved direction 2026-09-01)**: the engine-jargon band words
  ("Informative galaxy" etc.; level 1 currently silent — PaintingLayer unmapped) are replaced
  with words naming what the level gives, same boundary-only cadence: 1–2 "Constellations",
  3–4 "Systems and star lanes", 5–6 "Systems, star lanes and fleets" (owner's wording), 7–12
  and 13 RULED: **"System details"** and **"Orbital"**. Scan mode unchanged (band suppressed,
  lens spoken).
- **Collapse-restore fallback (RULED 2026-09-01, revised)**: when no remembered zoom exists
  to restore (a hot reload wiped the memory), collapsing goes to **spoken level 9** (internal
  step 8) — matching the inspect cursor's entry ceiling, so the two "sane default" cameras
  in the mod are one number. (The game's own `ZoomDefaultStep` is step 9 / spoken 10; the
  owner chose 9 for consistency with inspect.)
- **Snap landings force the target's band (RULED 2026-09-01 — resolves the paused landings
  question)**: Alt+Home, locate, quest pins, notifications and bookmarks zoom to the minimum
  band their target needs — a planet, action or 13-only child to 13 (today's planet go-to
  already zooms there, kept); a fleet to ≥5; a system keeps today's framing landing. The
  type-ahead search consequently finds what the current band's tree holds, coherent with the
  model.
- **Reconciliation on band shrink (owner, 2026-09-01): the cursor NEVER leaves the tree.**
  When a zoom change removes the focused row's band — by ANY route (slider, PageUp/Down) —
  the cursor lands on what that thing still is at the new band, else the nearest enclosing
  survivor: a planet focused at 13 STAYS on its planet row, now reading at dot fidelity; a
  13-only child (an action button, a dossier page) lands on its planet row; a fleet below
  level 5 lands on its (or its lane's) system row; a system at levels 1–2 lands on its
  CONSTELLATION row. Falling out of the tree to the HUD or the zoom slider is a defect —
  measured happening in scan mode today; unreachable in normal view today only because the
  tree is still zoom-independent, so the behaviour is proven by the named evidence-pairs
  when band parity lands, each driven through both zoom routes.
- **Inspect mode is zoom-aware (RULED 2026-09-01, normal view; corrected)**: the mode
  operates at levels 3–12. ARMING is allowed from anywhere and adjusts the camera as it
  already does today: Ctrl+I from closer than level 9 — level 13, or focus on a planet/child
  — ⚡ pulls the camera OUT to the entry ceiling, level 9, and arms
  (`GalaxyInspect.EntryZoomCeiling`, existing behaviour, kept per the owner). Arming from
  levels 1–2 is REFUSED via the existing unavailable idiom (RULED — the entry-floor idea was
  declined: those levels show constellations, and inspect's starting coordinates need
  systems, which exist from level 3). Crossing into 1–2 or 13 while the
  cell is LIVE — by any route — exits the mode with the existing "Exited inspect mode"
  message; the exit itself moves no camera. OPEN interplay: scan mode's Diplomacy band sits
  on the same camera steps as levels 1–2 and was ruled to lean on inspect as the territory
  reader, and the scan System band reaches step 12 — whether these zoom rules apply inside
  scan mode needs the owner's word when the scan half resumes.
- **Scan-half rulings, third round (2026-09-01)**: battle rows carry the SYSTEM and its
  coordinate pair in the standard idiom ("Kais, -35, 33, battle: Neurrone, Leaper"), the name
  gated by the player's own knowledge (coordinates-only otherwise, same rule as spokes).
  TRADE ROUTES are WOVEN INTO THE MAP'S OWN GEOMETRY (RULED, superseding the per-route
  children idea): an endpoint system says "Trade route to ⟨other end⟩"; an intermediate
  system says "along trade route from ⟨X⟩ to ⟨Y⟩"; and a STAR LANE the route rides says it
  carries the route, with the drawn traffic state ("carries trade route ⟨X⟩ to ⟨Y⟩, open /
  blockaded / mixed" — the three materials the renderer draws). MULTIPLICITY (RULED): a lane
  carrying several routes names each with its own state; a system that is endpoint or
  waypoint of several routes lists each relationship — one spoken line per route, never a
  merged summary. The route is then traced by
  walking lanes exactly as a sighted player traces the line. The shipped flat `scan:routes`
  group RETIRES into this weave. Knowledge-safe (`RevealNodesOnTradingRoutePath`); all
  fixture-blocked until a save has a trading company. INSPECT in scan mode tops out at level 10 (map lenses only);
  normal view keeps 3–12 with the additions below. RULED (2026-09-01): Ctrl+I on the System
  lens pulls out to the Economy lens and arms — the entry ceiling kept in scan mode for
  symmetry, the lens change audible through the existing announcement. RULED: a snap landing
  whose target kind the current lens does not represent LEAVES SCAN MODE FIRST, then performs
  the ordinary normal-view landing (the game's own in-mode reveal behaviour still gets
  measured during implementation).
- **Inspect at levels 1–2: the territory survey (RULED 2026-09-01, reversing the earlier
  refusal)**: arming at 1–2 is allowed and the mode reads, per cell: whose territory the
  cell is (the ownership painting — the band's one otherwise-unreadable subject), the known
  systems inside it (named only by the player's own knowledge), and constellation crossings.
  REVERSED (owner, 2026-09-01, after stage 3 shipped it): there is NO ownership-change leap
  on any chord — the travel keys (Alt+Left/Right, not shift+arrow as earlier texts said) are
  for star lanes and fleets only, and no other chord was assigned. The survey reads
  ownership per square and announces crossings; leaping is not offered. ALSO RULED: the
  inspect cell has PARITY WITH ZOOM — its contents-reading is band-filtered like the tree
  (at 1–2 the survey speaks ONLY territory, contained known systems and constellation
  crossings; fleets/probes/lanes/bookmarks are silent there, and each kind speaks only from
  its band elsewhere). RULED: the cell stays **1×1 by
  default** at 1–2 — size increases are the player's deliberate act.
- **Scan tree 3+ is GROUPED BY OWNER (RULED 2026-09-01, superseding flat reading order for
  those bands)**: the Trade and Economy lenses paint every system's owner (ownership rings /
  pies and the 6-way owner bar — measured), so the scan tree from level 3 groups systems
  under their empires, symmetric with the Diplomacy band: at 1–2 the empire list stands
  alone; zooming in GROWS each empire's row into a group of its systems, and deeper bands
  add per-system detail beneath — one continuous shape across every lens. The empire group
  rows keep the diplomacy-band reading (leader, relation, centre) so the continuity is
  audible. Buckets beyond met majors: minor factions, pirates, no-owner, and unexplored
  ("???") systems each group under their own heading — the unexplored heading exists because
  a "???" system's owner is UNKNOWN, not none (`ScanNodeLabel.RefreshNameGroup` draws "???"
  below `ExplorationState 2`): filing it under No owner would assert knowledge the label
  withholds; heading wording RULED: "Unexplored" (consistent with existing mod terminology),
  and any empty group is not declared. ORDERING (RULED): empire groups are
  ordered by their KNOWN CENTRE'S position in space, the same reading order the
  constellations use — respecting what the player sees on the map. Completion details
  (natural application, flagged): an empire met but with no known centre sorts after the
  positioned ones (nothing of it is drawn to see); the position-less buckets (no owner,
  unexplored) come last, their systems in reading order within. FLAG: whether the SYSTEM lens (11–13) also paints per-system
  owner for surrounding systems is unverified (its node labels are off; only the territory
  ellipse and centre panel were measured) — the grouping is applied there for shape
  continuity, marked for verification.
- **Custom scanner categories are band-aware (RULED 2026-09-01)**: a custom category's
  selectors each obey their base category's band at the current zoom — a slot mixing
  `systems:neutral` with `fleets:friendly` answers only its systems half at levels 3–4, and
  a keyword matches only rows the band declares.
- **Open-space object bands**: probes MEASURED at levels 7+ (their labels stop drawing below
  step 6, with docks and hangars); obliterator missiles, ally pins and quest markers sit with
  probes at 7+ PROVISIONALLY — no instance exists to measure, pinned for verification.
  Levels 5–6 are fleets only.

## Scan-mode consolidated specification (2026-09-01)

Principle: the zoom ladder selects the LENS, so nothing changes zoom as a side effect —
except the one ruled auto-zoom: Ctrl+I on the System lens pulls out to Economy and arms,
audibly. Bands: 1–2 Diplomacy, 3–6 Trade, 7–10 Economy, 11–13 System, 14–15 the
management/planet pages on the slimmed `ScanViewScreen`.

**Tree** — grouped by owner from level 3 (empires ordered by known centre in constellation
reading order; then minors, pirates, No owner, Unexplored; empty groups undeclared; empire
group rows keep the diplomacy reading so 1–2 is simply the same list before it grows
children): 1–2 the empire list alone + battle rows + spokes (knowledge-gated naming);
3–10 each empire's systems with lens decoration (focus=hover), dot-fidelity planet
children, lane children carrying the trade-route weave; 11–13 systems at name+lanes
fidelity plus the `scan:system` centre panel. Parity-hidden kinds (fleets, probes,
missiles, pins, markers, deposits, docks/hangars, wrecks, constellations) absent
throughout; expansion always in place; camera-independent rows (kind parity).

**Scanner** — lens-filtered from the same table; custom categories lens-aware; go-to slides
and seats, never zooms (lens-filtering guarantees the target is representable).

**Inspect** — 1–2: the territory survey (per-cell ownership, contained known systems,
constellation crossings; 1×1 default; travel keys leap to the next ownership change);
3–10: the ordinary cell; 11–13: arming pulls out to Economy; a live cell crossing into a
band that disallows it exits with the existing message. TRAVEL KEYS (RULED 2026-09-01): in
scan mode shift+arrow keeps today's normal-view semantics everywhere (travel by what the
cell holds, finding unexplored tiles as it does today); the 1–2 ownership-change leap
LAYERS onto the empty-cell case, displacing nothing — the merge order verified against the
current code at implementation.

**Scan-dot overlay correction (2026-09-01, measured live)**: the scan label's dot prefab
does NOT wire `CuriosityAnimatedCircle` (field null on every scan dot; the normal-view
prefab wires it and the circle drew VISIBLE on Heka III) — so Curiosities are
NORMAL-VIEW-ONLY and the scan Curiosities category is dropped from every lens.
Colonizability and unique/colonized/unknown STATES stay: they are the scan dot's own drawn
state and its live-measured tooltip sentences. The other overlays (mining probe,
terraformation, ghost, anomaly reduction) are wiring-UNVERIFIED on the scan prefab —
per-field null-probe pinned for implementation; the element × band table's scan rows for
them are downgraded to provisional.

**Landings** — slide and seat on the band's representative; a target kind the lens hides
exits scan mode first, then lands normally. **Reconciliation** — never leaves the tree;
lens crossings seat on the nearest survivor (a planet's system; a system's empire group at
1–2); mode entry/exit re-seat by the same rule, each a named evidence-pair.

**The hacking family (DLC installs), modelled as lens-independent stops** (they span every
band per the gate table; dashboard absent at Management, all absent at Planet), read off
drawn widgets so they never appear on this install:
- *Hacking banner*: readout rows — bandwidth allocated/max (+ one row per allocation when
  any exist), hacking speed, operations n/max, one row per running operation (row content
  unverified — no operation has ever existed in a fixture); the overcap warning when drawn;
  the trace group only when drawn, and the two provably-placeholder labels
  (`%TracingSpeedTitle`, the prefab "Total siphon" line) excluded until a live case shows
  them bound.
- *Traitors banner*: the sleeper-count readout plus the SLEEPER REPARTITION toggle as a real
  control carrying the game's own failure tooltip when disabled; per-empire rows when the
  table draws any.
- *Hacking console (dashboard)*: its three toggles as controls with their game tooltips; the
  two program menus as groups of program rows (11 defensive / 14 offensive measured by name;
  costs, cooldowns and per-program tooltips unverified). Activating a program flows into the
  game's hacking targeting cursor, which the mod's existing `CursorTargeting` support
  already models.
- *Scan notification panel*: one row per chip when any exist (content unverified).
- The legend needs nothing — measured picking the DLC caption groups up by itself.

**Open**: inspect-exit camera restoration (below); System-lens owner painting (verification
flag); the pinned fixture-blocked set (trade routes, contested influence, hacking content,
multi-empire diplomacy, "???" systems).

**Inspect exit (RULED 2026-09-01): unchanged.** Entry pulls to the ceiling; leaving keeps
whatever the player zoomed to (`GalaxyInspect.cs:87-93`); the tree cursor is untouched by
the mode either way. The dismiss-restores-camera idea was declined.

**Bookmarks in scan mode (RULED 2026-09-01)**: a bookmarked SYSTEM's annotation rides the
system's own row in the owner-grouped tree (the row exists there); POINT bookmarks get a
"Bookmarks" group in the scan tree (position-ordered); the bookmark jump keys keep working,
riding the inspect cell for points as in normal view.

## Late rulings (2026-09-01, during stage 6)

- **Unique and ghost dot overlays are read** where the dot prefab draws them (both wired on
  the scan prefab, measured) — reusing the existing "Unique Planet" and ghost-signal
  wordings; the normal-view dot path checked for the same two.
- **Star-lane rows are BUTTONS** — Enter already follows the lane; the role now says so, so
  the gesture is discoverable. Both modes.
- **The zoom slider carries a usage hint** composed from the live bindings (rebinding-safe,
  the ChordNames idiom): normal view "{key} or {key} to change zoom", scan mode "{key} or
  {key} to change lens" (wording delegated to the main agent by the owner).

## Refactor and implementation sequencing (proposed 2026-09-01)

**Stage 0 — the split (behaviour-preserving, no spec content).** `GalaxyHudScreen.cs`
(11,776 lines) becomes a spine plus row components under `ES2Access/Screens/Galaxy/`,
by code motion only:

| new file | takes (current lines) | ~size |
|---|---|---|
| `GalaxyTree.cs` | BuildSystems' spine — existence lists, partition, ordering, bookmark interleave, group emit (:4596-4846) | 0.4k |
| `ConstellationRows.cs` | AddConstellation, AddUnexplored (:4847-5118) | 0.3k |
| `SystemRows.cs` | AddPlace/AddSystem/AddLocated/AddInside/AddDeposits/AddManagementView/AddLabelButtons (:5119-6467) | 1.4k |
| `OpenSpaceRows.cs` | quest markers, open-space markers, sendables, probes, projectiles, pins (:6467-6830, :10493-11100) | 1.0k |
| `PlanetRows.cs` | AddPlanets + dossiers, signals, anomalies, fidsi, curiosities, wrecks (:6830-8404) | 1.6k |
| `LaneRows.cs` | AddStarlanes, probe directions (:8664-9084) | 0.4k |
| `FleetRows.cs` | fleets, en-route, free-moving, adrift, AddFleet, hangars, garrison helpers (:9084-10493, :11497) | 1.5k |
| `Core/UI/Bands.cs` + `UI/ZoomBands.cs` | NEW: the per-band kind/fidelity table as pure Core data (offline-testable) + the thin adapter reading zoom step / scan lens — the single source for tree, scanner, inspect gating, snap landings, slider words | 0.2k |

`GalaxyHudScreen` keeps gates, keys, stops, landings consumption, HUD (~2k). Static
helpers stay static; the spine owns the per-build caches. Proof: the `walks/` before/after
diff (single-session stash loop), build, offline tests; code motion touches no pointing
call, so the focused-tooltip blind spot is out of scope by construction.

**Stage 1 — normal-view band parity.** Tree browsing filter + scanner band filter (custom
categories included) + slider band words + the 1–2 scanner edge + zoom-under-cursor
reconciliation. Verify: the named evidence-pairs (both zoom routes), scanner
category-per-level walk, `ScannerBandTests` offline against the Core table.

**Stage 2 — motion model.** Graded expansion (in-place at 3–6, jump-to-13 at 7–12,
collapse-restore with the level-9 fallback, constellation expand at 1–2), snap-landing
band rule, dot-fidelity planet rows at 7–12. Verify: expansion/collapse pairs per band,
landing pairs per target kind.

**Stage 3 — inspect zoom rules + territory survey.** 3–12 gating, exits on crossing (both
routes), 1–2 survey (ownership cell reading, ownership-change travel layered on the
empty-cell case, 1×1 default). Verify: exit-message pairs, survey transcript vs
per-square influence oracle.

**Stage 4 — icon children.** Passive label icons → tooltip-bearing child nodes (both
label families), retiring the buffer stuffing; the construction-tooltip fix falls out; the
constellation bonus row. Verify: `TooltipParity`/`Coverage` clean on the galaxy screen,
per-icon focused-tooltip pairs for the kinds the fixture draws.

**Stage 5 — scan mode on the shared components.** 5a: the gate change
(`IsInGalaxyScanView` admitted), `ScanLens` composition, `ScanViewScreen` slimmed to the
pages, owner-grouped scan tree + lens parity filter + scanner lens filter. 5b: diplomacy
band content (empire list, spokes, battles, centre links), hacking family stops,
exit-scan-first landings, scan reconciliation and mode entry/exit pairs. 5c: the
trade-route weave (buildable; live verification fixture-blocked) and the pinned null-probe
sweep of scan-dot overlays. Verify: per-lens tree dumps vs the spec tables, the scan
evidence-pairs, crops for the diplomacy drawings.

**Stage 6 — closure.** Manual-test document, per-charter doc landings (interaction.md
keys/layers, test-recipes updates, roadmap), and the walk baselines refreshed.

Sequencing rules: stages run as sequential Opus subagents (one live game); stage 0 may run
in a worktree; the Core band table + its tests are the one piece pipelineable alongside
stage 0 (no shared files). Hotspot files (`ModEntry.cs`, `ModStrings.cs`,
`english.json`) are owned by exactly one stage at a time — stages 1, 4 and 5b each touch
them and therefore never overlap. Fixture-blocked items (trade routes, contested
influence, hacking content, multi-empire diplomacy, "???" systems, System-lens owner
painting) ship code-complete with pinned verifications listed in the roadmap, not silently
assumed.

## Fixture-blocked / unverified (the coverage statement)

Multi-empire diplomacy (one met major: one label, one link); the `WatchingEmpire` swap; battle
content on any lens; real trade-route lines (no trading company exists — the render, the
blockade colours and the mod group's per-band gating all re-verify then); populated hacking
tables, program-row internals, in-map hacking icons, scan notifications; contested/minor/pirate
ownership arcs (code-read only); the "???" unexplored name branch (none in frame); missiles /
ally pins / merged fleet labels (hidden by `GuiManager.cs:1559-1562` + window state, no live
instance); rung-14/15 descriptors (settled by alpha tables, not read live); physical-key
behaviour in-mode; only one camera framing sampled.

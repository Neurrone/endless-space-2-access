# Stage 1 — normal-view band parity

Branch `scan-modes`, 5 commits, build 0 warnings, `dotnet test` 1183/1183 green, live on
`ES2Access-r6` against `[Beginner] access test`. Game left RUNNING, camera home (`focus
[68.884, 0, -22.45]`, step 9), cursor back on `hud:empire/screen/EmpireScreen`, Sabel re-collapsed,
no field holding focus — as the session found it.

1. `5053c18` **adapter + seat choice.** `ES2Access/UI/ZoomBands.cs` (spoken level + scanning flag →
   the Core table; a level it cannot read hides nothing). `Bands.SameShape` tells a step within a band
   from one across it. `GraphBuilder.SeatOnContainer` → `GraphRender` → a third tier in
   `KeyGraph.Reconcile` AND `RepairStopMemory`: on a build declaring fewer kinds, a dead cursor seats
   on `DeepestDeclaredAncestor` (the key-path ancestry the engine already uses to reach into a
   collapsed group) rather than the nearest-survivor walk; ordinary row deaths keep today's answer,
   pinned by a test. **This touched `Core/UI/Graph/{GraphBuilder,GraphTypes,KeyGraph}.cs`, mirrored
   into `docs/generic/src/graph-ui/`; `sync-generic-src.ps1` was run to keep the tests green — the
   stage's one docs/generic touch, mechanical, no authored prose.**
2. `581c4f6` **tree browsing filter**, normal view, per kind, camera-independent. Constellation groups
   stand CLOSED at 1–2 (`Right` answers the existing "Nothing in here"); a search build still opens
   everything. Row content is otherwise today's but for one thing: a system's spoken fleet counts
   (`FleetPresence.At` / `UnderWayNearby`) go silent below 5, so the number and the children the
   branch opens onto stay one answer.
3. `259aeaf` **scanner band filter** — hidden categories are never gathered and are cleared before the
   custom slots are composed, so custom selectors and keywords inherit the filter with no rule of
   their own. No new ModStrings key was needed for the 1–2 edge.
4. `c56e83d` **slider band words** (ruled wording); `PaintingLayer` mapped, so level 1 speaks for the
   first time; `SystemsLayer`+`SystemLayer` share "System details".
5. `f283f7e` **doc landings** — `interaction.md`, `test-recipes/galaxy-map.md`, `roadmap.md`.
## Tree shape per spoken level (`/gui/graph`, key shapes counted, Sabel expanded)

| level | top level | Sabel's children |
|---|---|---|
| 1–2 | 2 constellation groups, closed; no systems, bookmarks or probe | — (no system row) |
| 3–4 | + 21 systems, + 4 point bookmarks | 3 lanes |
| 5–6 | same | 3 lanes, 2 fleets |
| 7–10 | + 1 probe (open space) | 3 lanes, 2 fleets, 4 planets, 4 dossiers, manage-system |
| 11–13 | same | as 7–10 minus manage-system — the GAME stops drawing that label button (pre-existing existence gate, not the band filter) |

Matches the spec table. No adrift fleets, missiles, pins, quest markers, hangars or wrecks exist in
this fixture: those kinds ship code-complete and unsighted.
## Scanner ring (per press, `galaxy.scanCategoryNext`, cursor on the map stop)

- **L1 edge, verbatim:** every scanner chord — category, subcategory, next, go-to — answers
  `"Luxury Resources: all, none found"` (whichever category the cursor was parked in) and moves
  nothing. The existing idiom, not silence.
- **L3:** `explorer: all` → `Systems: all` → `Unexplored: all`, repeating. **L5:** + `Fleets: all`.
  **L7 = L13:** Systems, Colonizable Planets, Unexplored, Anomalies, Curiosities, Luxury, Strategic,
  Fleets, Probes, explorer (Contested/Pins/Projectiles/Markers are EMPTY here, not band-hidden).
  Custom slot inherits it: `explorer … 1 of 20` at L3, `1 of 54` at L7.
## Reconciliation (cursor before → after, with the spoken line)

- (a) `…/system/476/fleet/1839` → zoom 4 → `…/system/476`: *"Sabel, -35, -5, group, Home System,
  colonized, expanded, 8 of 18"* (fleet counts gone with the rows). (b) `…/system/476` → zoom 2 →
  `galaxy:constellation/446`: *"Serpens, group, collapsed, 2 of 2"*. (c) `…/system/476/tooltip/1`
  (Titanium deposit dossier) → zoom 6 → `…/system/476`.
- **Slider route** (`ui.coarseDecrease` on the View Controls zoom node, map memory on the fleet row,
  13→4): Tab back to the map lands on `…/system/476`. Top-level probe → level 6: nearest-survivor
  fallback, `…/system/572` (Byrtus) — still in the tree; a drifting probe has no containing system in
  its key, so the container tier cannot apply.
- **MANUAL-TEST (physical keys):** `POST /key` was NOT refused (desktop unlocked) but bare
  PageUp/PageDown moved no rung. Cause measured and not mine — `GalaxyViewCameraController.CheckInputs`
  gates all keyboard zoom on `Gui.GuiNotificationService.CurrentGuiNotification == null`, and
  `NotificationPopulationGained` stood all session (`DevProbe.Claims("PageDown")`: the mod does not
  claim it). That route's re-seat stays UNPROVEN — manual pass with no notification up.
## Band words (ladder 1→13, `/speech`) and walk regression

`Zoom level 1 of 15, Constellations` · 2 Constellations · 3–4 Systems and star lanes · 5–6 Systems,
star lanes and fleets · 7–12 System details · 13 Orbital. Level 1 was silent before.
Only `walks/01-galaxy` changes, through the zoom slider's spoken value in every dump holding View
Controls; its tree legs run at level 13 after the first expansion — inside the detail band — so their
rows are unchanged, and no other family holds the zoom ladder or the map tree.
## For stage 2

- Two gaps this stage OPENS, both closed by the ruled snap-landing rule: at 1–2 a bookmark jump is
  consumed, slides the camera and says **nothing** (measured); and a point bookmark inside a
  constellation is unreachable by browsing while those groups are closed (keys and type-ahead reach it).
- Expansion state is still the player's — the band only forces the presentation — so "expand at 1–2
  zooms to 3 and opens" hooks `ConstellationRows.Open`. The band read is
  `_showsSystems/_showsFleets/_showsDetail`, once per build in `GalaxyTree.ReadBand`, with
  `_showsDetail` derived from the Planets column rather than a hard-coded 7; `SeatOnContainer` is set
  only on non-search builds and only when the shape really changed.

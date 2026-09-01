# Scan modes stage 0 — the galaxy split and the band table

Branch `scan-modes`. `8e4f64f` the code motion, `5fa07a7` the inert Core band table.

## What moved where

`GalaxyHudScreen` is now a **partial class** across `ES2Access/Screens/Galaxy/`, namespace
unchanged — partial rather than seven new types because it is the only split that is
behaviour-preserving *by construction*.

| new file | takes | lines |
|---|---|---|
| `Galaxy/GalaxyTree.cs` | per-build caches (`_systems`/`_located`/`_colonies`/`_adrift`), bookmark points + `AddBookmarkPoint`, `BuildSystems` + `Partition`, the reading-order comparators, system-label fetches | 716 |
| `Galaxy/ConstellationRows.cs` | `AddConstellation`, `AddUnexplored`, `Seed`, `ZoomOutOf`, constellation labels | 238 |
| `Galaxy/SystemRows.cs` | `AddPlace`/`AddLocated`/`AddSystem`/`AddDeposits`/`AddInside`/`AddManagementView`/`AddLabelButtons`, owner & outpost wording, time bubbles, guard lines | 1365 |
| `Galaxy/OpenSpaceRows.cs` | quest markers, sendables, `Drifting`/`Sight`/`Anchor`, `AddProbes`/`AddProjectiles`/`AddPins`, `Countdown`, `Follow` | 1281 |
| `Galaxy/PlanetRows.cs` | `AddPlanets` and the orbital-card family — dossiers, signals, anomalies, FIDSI, curiosities, wrecks, card actions | 1757 |
| `Galaxy/LaneRows.cs` | `LaneId`/`LanesOf`, `AddStarlanes`, `AddProbeDirections`, lane commands | 425 |
| `Galaxy/FleetRows.cs` | `AddFleets`/`AddEnRoute`/`AddFreeMoving`/`AddAdrift`/`AddFleet`/`AddHangars`, lozenge & garrison helpers | 1126 |
| `GalaxyHudScreen.cs` (kept) | gates, keys, stops, `Build`, camera/seat/landing followers, travel & trail, bookmark API, fleet selection, shared tiny helpers (`AddLine`, `AddTooltip`, `AddWidgetLines`, `Visible`, `Point`/`PointAt`, `Owner`, `Perceived`) | 4922 |

Not the ~2k the brief guessed: the follower/travel/landing machinery (:610–4235) is ~3.6k of it
and is "landings consumption", so it stayed by the brief's own rule.

## Surface changes forced by the motion

**None** — no private→internal, no signature change, no static/instance change. Proof: sorting
every non-blank line of the old file against the eight new ones leaves exactly one unmatched line
(`public sealed class` → `public sealed partial class`) plus 154 lines of scaffolding (7 × 22);
`git diff` on the screen file is 1 insertion / 6855 deletions, that line being the insertion.

One test-side change *was* forced. `VisibilityTestLintTests` scans a file for bare
`Visible(`/`Painted(` calls only when **that file** declares the private shim, so the split
silently dropped ~18 sites out of the lint. It is now partial-aware. With that in, the regenerated
allowlists differ from `HEAD` only by paths, per-file count splits of the same totals, and one
pre-existing stale `CutsceneScreen.cs` entry the regeneration pruned.

## The band table (inert)

`Core/UI/Bands.cs`, pure BCL, no runtime consumer. `BandFidelity` (None/Name/Dot/Full),
`BandKind`, `ScanLens`, and `Shows(level, scanning, kind)`,
`Scans(level, scanning, categoryKey)` (keys reused from `ScannerKeys`), `LensAt(level)`. Levels
are the **spoken** 1–15, clamped. 23 tests in `ES2Access.Tests/UI/BandsTests.cs`.

## Verification

- before: 141 dumps at `ES2Access-r2`; after: 141 at `ES2Access-r3`; identical `skipped.txt`.
- `diffwalks.sh`: **50 differing lines**, all one class — a collapsed `hud:tutorial` stop present
  before and gone after, on senate / government / laws / system-politics / the senate-cell tooltip.
  No galaxy screen among them.
- **Classified by re-measurement.** Stashed the split, rebuilt and reloaded the pre-split build
  (`r4`), re-ran `03-empire.sh` and `02-system.sh`: the tutorial stop is absent there too — the
  first walk consumed those one-shot popups. Diffing that pre-split re-run against the split walk:
  **0 differing lines** across all thirteen re-captured dumps, the five affected included. Same
  fixture, two builds, nothing differs. Stash popped, rebuilt, reloaded (`r5`).
- `dotnet test` 1177 passed / 0 failed (was 1154); `dotnet build` 0 warnings.
- **Blind spot, by construction.** Unfocused Class-backed tooltips cancel in the walk diff. Out of
  scope here: no `Point(`/`PointAt(`/`PointsAt` site was edited at all — the sorted-line proof
  shows the only edited line in 11,776 was the class declaration.

## What stage 1 must know

1. **`UI/ZoomBands.cs` was NOT written** (the brief allowed skipping it). Pieces:
   `GalaxyViewLevels.ZoomRung` is 0-based (0–12 galaxy steps, 13 = system management, 14 = planet)
   and `GalaxyViewLevels.Scanning` answers `IsInScanView`. **Measured while restoring the camera:**
   `SetZoom(9, …)` announced *"Zoom level 10 of 15"* — spoken level is `ZoomRung + 1`, which is the
   convention `Bands` is written in.
2. **Two interpretive calls in the table, for the owner to confirm.** Normal-view 14–15 encoded as
   identical to 13 (the design says the tree is "unchanged" there); scan 14–15 (system management,
   planet page) encoded as offering no map kinds (they "stay on `ScanViewScreen`"). Neither measured.
3. `Contested` at the Economy lens is PROVISIONAL in the code comment, as the design has it.
4. `ModEntry.cs`, `Core/Speech/ModStrings.cs` and `locale/english.json` were not touched.
5. Game left running at `r5`, camera home (step 9, 68.884/-22.45), focus released, tutorial as
   found; tooltip delay left at the 0 it already held at stage start.

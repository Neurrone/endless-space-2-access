# Stage 5c — the trade-route weave, and the pinned sweeps

Branch `scan-modes`, 3 commits, build 0 warnings, `dotnet test` **1190/1190** green, verified live on
`ES2Access-r29` against `[Beginner] access test`. Game RUNNING, **scan OFF**, camera at the save's own
`focus [68.884,0,-22.45]` step 9, cursor `hud:empire/screen/EmpireScreen`, constellations expanded /
systems collapsed, `TradingCompanies.Count == 0` (synthetic company removed, then `POST /loadsave`).
`docs/generic/` untouched; `sync-generic-src.ps1` not run — nothing touched is mirrored.

1. `6d768fb` **the weave**. `Core/UI/TradeWeave.cs` (new, engine-free) takes each route as
   `TradeRouteRenderer` :225-247 does — pairwise legs under the undirected key :234-236, the blockade
   flag accumulating :233, the end-blockade flag :228 — and answers what the routes are to a PLACE
   (`At`) and what rides a LANE (`On`, each route carrying the material the renderer would paint that
   lane, :272-283). `Screens/Galaxy/ScanTradeRoutes.cs` (new) reads the department once per build;
   `ScanRows.AddScanSystem` gets ends and waypoints, `LaneRows.AddStarlanes` the rides. **Gated on the
   mode and nothing else, because the drawing is** (`ViewService_ScanViewSwitched` :184-190 into
   `:204-300`, no zoom/lens/camera term); the gather runs from `BuildSystems` in BOTH modes, so leaving
   empties it. `TradeLanes`, its tests and `scan:routes` are deleted (seven now-unspeakable keys gone).
2. `44616c6` **the scan dot stops claiming marks it cannot draw** (`PlanetRows.AddPlanetDot`).
3. `8b998eb` **doc landings**: `docs/galaxy-map.md` (three game facts), `test-recipes/galaxy-map.md`
   (synthesis recipe + two fixture-blocked entries), `docs/roadmap.md` (pins and flags).

## New phrases — six, all mod-authored, in the ruling's quoted shapes

`scan.trade-route-to` "Trade route to {0}" · `scan.trade-route-to-blockaded` "Trade route to {0},
blockaded" · `scan.trade-route-through` "along trade route from {0} to {1}" · `scan.trade-lane-open` /
`-blockaded` / `-mixed` "carries trade route {0} to {1}, open|blockaded|mixed". The end's state word is
the brief's "+ its state": a route is painted blockaded or it is not, so two forms. Path nodes are
named by the mod's ordinary rule — `RevealNodesOnTradingRoutePath` :1943-1959 raises them to `Known`,
which is not the threshold the label's "???" keys on.

## The synthesis WORKED — the live weave, transcribed

Built from the REPL: the game's own pathfinder for the path (Dusay#76 → Rigel#72 → Sabel#67), a
`TradingRoute` and a `TradingCompany` by hand, and the department's private `tradingCompanies` list by
reflection. No order posted, no reveal called, **zero `Warning` lines** throughout; removed by clearing
the list, then `POST /loadsave`. Recipe landed in `test-recipes/galaxy-map.md`.

- `Dusay, 0, 0, group, Home System, Trade route to Sabel` · `Sabel, -35, -5, …, Trade route to Dusay` ·
  `Rigel, -16, -5, group, No owner, along trade route from Dusay to Sabel`.
- Rigel's lanes: `Starlane 1 to Dusay, east, carries trade route Dusay to Sabel, open` ·
  `Starlane 2 to Heka, east` (silent — no route) · `Starlane 3 to Sabel, west, …, open`.
- **Multiplicity + mixed**: a second route on the same path with `IsBeingSoftBlockadedOnHQ` doubles
  every line (`… Trade route to Sabel, Trade route to Sabel, blockaded`) and both shared lanes read
  `carries trade route Dusay to Sabel, mixed` twice.
- **The gate**: with the company still injected, toggling the lens OFF removes every route line. And
  at zero routes the Trade-lens `/gui/graph` before the weave and after the reload differ only by the
  reload's cursor seat and the wall clock.

## Sweep 1 — the scan dot's overlay fields (all 253 circles the lens was drawing, step 4)

| wired 253/253 | null 253/253 |
|---|---|
| `CircleImage`, `UniquePlanetFeedback`, `GhostFeedback` | `CuriosityAnimatedCircle`, `MiningProbeFeedback`, `TerraformationFeedback`, `AnomalyReductionFeedback` |

Correction made: the in-mode dot row no longer says curiosities or mining probes (nothing ever claimed
terraformation or anomaly reduction). Evidence pair: under the lens `Dusay I, Inhospitable`; the same
planet on the ordinary map at level 9 `Dusay I, Inhospitable, 2 curiosities`. The mining-probe half is
inert here (no probe in the fixture), so only its code path is proved. **Unique and ghost are drawn and
read by NEITHER dot row** — left alone and pinned; nothing existing could be reused, so nothing was
invented.

## Sweep 2 — the System lens paints no owner (the 5a flag, answered)

Camera on Dusay: step 8 → 6 labels painted, 4 `StarCircle`s, 2 `OwnerCircleTable`s; step 11, same
camera → **0 of 86** on every one; crops agree (Economy: nameplate, dots, owner ring, influence
ellipse; System: only the centre panel's gauges). The owner grouping at 11–13 is therefore a
deliberate deviation for shape continuity. **Flagged, not redesigned.**

## Sweep 3 — the "???" Unexplored group: no candidate exists

Census of all 86 systems: **65 Unrevealed, 17 Revealed, 4 Owned, none at Localized / Identified /
PartiallyRevealed** — `_located` is empty galaxy-wide, so no framing can sight one. PINNED.

## Judgement calls

- **A lane's traffic word is the LANE's colour, said on every route's line** — the brief's "the state
  the renderer would paint". Two routes over one blockaded hop both say "mixed". The other reading of
  the ruling ("each with its own state" = per-route open/blockaded) makes "mixed" unreachable and
  disagrees with the single line the player sees. Reversible in one method; flagged for the owner.
- **A waypoint line carries no traffic word** (the spec quotes none; the colour is on the lanes), and
  **the lines are announcement parts on rows that already exist**, in row order (after owner and home,
  before the bookmark note) — not children, not buffer lines.
- **A route doubling back over its own hop is named once on that lane** while both legs still colour
  it; and `TradeLanes` was deleted rather than left dead, 5a having retired the group it served.

## For stage 6

- Hotspots free again (`ModEntry.cs` untouched; `ModStrings`/`english.json` +6 keys, −7).
  `Core/UI/TradeWeave.cs` is the whole model, `ScanTradeRoutes.cs` the only screen-side code, and
  `GatherTradeRoutes` must stay on BOTH modes' build path. Tests 1188 → 1190.
- **Unexplained, worth one run**: the log ring holds ~100 warnings from BEFORE this build —
  `NotSupportedException: Collection is read-only` at `NodeAnnouncement[]::Add` in `AddSpokes` ←
  `AddEmpireRow` ← `BuildEmpireList` (5b's diplomacy band). Not reproducible on r29; the retry also
  found `DiplomacyScanViewWindow.WatchingEmpire = Gui.Game.Empires[1]` REVERTS to the player before the
  next build, so 5b's swap recipe needs re-checking before that path can be driven again.
- Untested by construction: the drawn lines themselves, physical keys in-mode, the unique/ghost marks.

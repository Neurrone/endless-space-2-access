# Stage 6 — closure

Branch `scan-modes`, 8 commits, build 0 warnings, `dotnet test` **1191/1191**, verified live on
`ES2Access-r30`…`r33` against `[Beginner] access test`. Game QUIT at the end (below). `docs/generic/`
untouched except `sync-generic-src.ps1`, run for two mirrored Core files (`graph-ui/GraphBuilder.cs`,
`graph-ui/GraphSheet.cs`) — mechanical, no authored prose.

## The defect: a vtable that advertised Add and refused it

517 warnings, seq 3403–3919, every one `galaxy: reading the systems threw: NotSupportedException:
Collection is read-only` at `NodeAnnouncement[]::ICollection.Add` under `AddSpokes` ← `AddEmpireRow`
← `BuildEmpireList` ← `BuildScanTree` ← `BuildSystems` — so each aborted the WHOLE map-tree read.
**Cause.** `NodeVtable.Announcements` is an `IList`, every vtable factory but two hands back a list,
and extending a returned vtable with one more part is the ordinary idiom — but `GraphBuilder.Label`
and `GraphSheet`'s text cell returned an ARRAY under it, which advertises `Add` and throws from it at
run time and nowhere else. `ScanDiplomacy.cs` has one commit whose `AddSpokes` builds a `List`, and
the burst ENDS at 3919 (before 5c's r29), so the throwing call was fixed inside 5b before it
committed. **Fixed at the hazard** (`e3c3463`): both factories return a mutable list with a test
naming the rule; the 13 `GraphBuilder.Label` callers were audited, none mutating today.
**Proof, fixed build (r30), clean log.** Watching the player: Neurrone a group over the disabled
`Swap position` toggle and spokes `Ita, 5, 34` / `Primus, 17, 21` / `Sabel, -35, -5`, Leaper a
`COLD WAR` leaf. Watching Leaper: Leaper first with FIVE spokes (`Unexplored system` ×4 over the
player's fog, `Kais, -35, 33` named), centre `-83, 24`; Neurrone loses its centre and takes
`COLD WAR`. A ten-press injected walk spoke every row, **zero warnings**; none since 3919.
**The swap recipe, re-derived** (5c read it as reverting "between frames"): it does not decay —
measured holding across frames, `SetZoomHere` lens changes and mod rebuilds. The one thing that
resets it is `DiplomacyScanViewWindow.OnBeginShow` (`:163-167`) — **leaving and re-entering scan
mode** — and `POST /loadsave`. Corrected in `test-recipes/galaxy-map.md`.

**The owner's three additions**, landed before the baselines and the sweep:
- `5a00d66` **the dot's two marks**, both views, in the game's own words (one `GhostWord` shared with
  the full reading) under the game's own gate — `PlanetCircleItem.Refresh` puts both inside its
  Revealed branch = `Surveyed`. Reading the overlay's ALPHA was tried first and reverted: a culled
  label paints no circles, so the mark came and went with the camera. No mode branch (both prefabs
  wire both fields, 253/253 circles). Live `Raia, Colonized, Unique Planet` at level 9 and under the
  Economy lens with Dusay's label culled; ghost fixture-blocked.
- `218f75d` **a star lane is a button**: `Starlane 1 to Primus, northeast, button, 5 of 8`, Enter
  still lands at Primus. Role word only.
- `5c4ff77` **the ladder's usage hint**, in the review buffer where the mod's hints live:
  `Left Arrow or Right Arrow to change zoom` / `… to change lens`. `NodeHint` gained an optional
  SECOND action (an adjustable worked from one end is half a control); a pair sentence with one half
  unrenderable contributes nothing. **Two new keys**: `hint.change-zoom`, `hint.change-lens`.

**Regression sweep** (r33; `/log` clean of new warnings and errors throughout):
- **Ladder 1→13, normal view**: the five ruled band words in order, level 1 included. **Tree**: L1 2
  constellations + 4 bookmarks; L3 +21 systems; L5 +2 fleets; L7 +7 planets +1 probe; L13 the same
  minus manage-system. **Scanner**: L1 the none-found edge on every chord; L3
  Systems/Unexplored/explorer; L5 +Fleets; L7 all ten. Matches the spec table throughout.
- **Scan mode 1/4/8/12**: lens names right; scanner Diplomacy none-found, Trade and Economy
  Systems → Colonizable Planets → Unexplored → explorer, System without Colonizable. `scanGoTo`
  slides and never zooms (`zoomStep` 3→3, 7→7, 11→11).
- **Reconciliation**: Kais focused at level 4 in-mode → level 1 → `galaxy:owner/1`, its owner's row,
  said; normal view, a system at level 1 → its constellation row reading `+15% Industry`.
  **Expansion pair**: expand at 8 → level 13 Orbital; collapse → level 8 System details.
- **Inspect**: armed from a system row at the System lens → out to the Economy lens (step 8, "Zoom
  level 9 of 15"), `Live=True`; crossing back in → `Live=False`. **Noted, not a regression**: arming
  is REFUSED from an owner HEADING, consistent with the standing grouping-row ruling and the open
  owner question already on the roadmap.
- **`TooltipParity()`**: normal view byte-identical to stage 4's baseline (one `promised`
  `hud:empire/research`, one `unknown` `galaxy:probe/1868`, all else empty); scan mode `clean:true`.
  **`Coverage()`**: no new bucket kind; with a system expanded `QueuedConstructionGroup` and
  `HomeSystemIconGroup` leave `uncovered` as stage 4 measured, and collapsed they return (the
  documented blind spot). Scan-mode `uncovered` is 8 owners — the first scan baseline recorded.

**Walk baselines, and the docs sweep.** Full nine-family walk on r33 — **111 dumps + 24 tooltip
captures in 507 s**, 5 fixture-shape skips, left at `<scratchpad>/walk-r33/`. **A baseline cannot be
carried to another session** — `GraphSheet` row keys are `GetHashCode()`, surviving a reload and not
a restart — so nothing under `walks/` is committed and the next refactor stashes its own "before".
Nothing stale under "Informative galaxy", `TradeLanes`, `scan:node` or the `zoom.band.*` keys; stale
and fixed (`e7c597a`): six `census-screens.md` rows pointing scan windows at `ScanViewScreen.cs`,
three `NONE` verdicts in `audit-dlc-mechanics.md`, and that file's own "not modelled" summary. **Roadmap consolidated**: the plan's 90-line
shipped narrative became one Shipped pointer row plus four sub-bullets of what is owed (MANUAL,
PINNED, FLAGGED, OPEN) pointing at the manual test; this defect and the WatchingEmpire instability
left it entirely, and the unique mark left the pinned list, being shipped. **Two findings the log knew and no doc did** (`4ca03f1`): 5c's synthetic-trading-company recipe left
the GAME's own `DepartmentOfCommerce.UpdateTradingRoutes` throwing **1314 NREs** that outlived its
restore and are invisible to every mod-side probe (the recipe now says to grep and reload); and the
walk SIGHTED `PopulationChangeNotificationWindow`, whose parity check finds a figure with no caption.

**Coverage — everything shipped and unverified.** Physical keys, the whole plan (`POST /input`
presses nothing): the re-seat pair, every mod chord, the wheel as an inspect exit, real arrows on the
ladder — `scan-modes-manual-test.md`. Nothing has been heard through a screen reader.
Fixture-blocked: the drawn trade-route lines and
their three materials; a ghost world (the dot's Sanctuary sentence is code-only); battle rows; the
swap toggle's UI; every hacking table's content, program costs and in-map icons; ordering beyond
n = 2; contested-influence rendering and its band; "???" systems; adrift fleets below level 5; quest
markers, ally pins, missiles. Unruled and left as shipped: the System lens's owner grouping (it
paints no owner there) and a multi-route lane saying the LANE's colour.
`POST /quit` answered 200 and the dev port closed, but the process did not exit: polled every 5 s for
**440 s**, `Responding=False` throughout — a shutdown hang, past the 120 s the loop calls one.
Force-killed; no orphan left in any session. Nothing was saved, so the fixture is untouched.

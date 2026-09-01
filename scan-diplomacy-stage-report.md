# Stage 5b — diplomacy band, hacking family, exit-scan-first landings, inspect gating

Branch `scan-modes`, 7 commits, build 0 warnings, `dotnet test` 1188/1188 green, verified live on
`ES2Access-r21`…`r28` against `[Beginner] access test`. Game RUNNING, **scan OFF**, inspect off,
camera at the save's own `focus [68.884,0,-22.45]` step 9 (restored by `POST /loadsave`, so the
fixture is exact), `TooltipDelay` at the 0 the session found. `docs/generic/` untouched;
`sync-generic-src.ps1` NOT run — no mirrored file changed.

1. `af2a991` **the diplomacy band**, `Screens/Galaxy/ScanDiplomacy.cs` (new). Levels 1–2: every met
   MAJOR (`DiplomaticRelation.HasAbility(IsKnown)`) plus the player, minus the eliminated, ordered by
   the WATCHED empire's known centre in reading order (unplaced last). Row = leader · centre pair
   (position only, never "home") · relation vs the watched empire; children = the swap toggle where
   the game draws one and the watched empire's spokes; battle rows top-level. Owner headings from
   level 3 gain the same reading, asked of the PLAYER. `PlacedRow.Drawing("battle")`.
2. `737a2ce` **the hacking family**, `Screens/Galaxy/ScanHacking.cs` (new; `ScanLensPanels` became
   partial): four stops worn by BOTH scan pages — `scan:hacking`, `scan:traitors`, `scan:console`,
   `scan:notifications` — read off drawn widgets, so they declare nothing without the DLC.
3. `687676d` **exit-scan-first landings**: `GoTo`/`LandInside` ask `DrawnByTheLens(target)` and leave
   through `GuiManager.ToggleScanView()` first; `EnsureBand(target, leaving)` then forces the
   normal-view band. New `BandKind.OpenSpace` column, read by the tree's open-space gate and the cell.
4. `544e4d8` **inspect's in-mode range**: `ShowsTheGalaxy` gets a scan branch — the cell lives while
   the lens shows planets (3–10) or empires (1–2) and ends at the System lens.
5. `9ca8b03` **the "Bookmarks" group** (owner word arrived mid-stage): `galaxy.bookmark.group`,
   `galaxy:bookmarks` last in the map stop, POINT bookmarks only, seeded open, rows keeping
   `galaxy:bookmark/N`, `PlacedRow.Grouping("bookmarks")` — 5a's last named hole.
6. `76d811a` + `0995579` **doc landings**: `interaction.md`, `heroes-and-diplomacy.md`,
   `test-recipes/galaxy-map.md`, `.../inspect-and-influence.md`, `.../scanner.md`, `roadmap.md`.

## Evidence

- **Level 1**: `Cravers Leaper (AI), -35, 33, COLD WAR` · `Imperials Neurrone, 0, 0, group` →
  `Swap position, checkbox, not checked, unavailable` · `Ita, 5, 34` · `Primus, 17, 21` ·
  `Sabel, -35, -5` · `Bookmarks, group, expanded, 3 of 3` → four point bookmarks in position order.
- **Watched-empire swap** (`WatchingEmpire = Empires[1]`, restored): Leaper grows **five** spokes —
  `Unexplored system` ×4 at (-55,50)/(-49,45)/(-59,34)/(-65,26) plus `Kais, -35, 33` — the audit's
  optically-confirmed five, four over the player's own fog; Leaper's centre moves to its own home
  (-83, 24); **Neurrone loses its centre** (Leaper's intelligence does not place it) and takes the
  `COLD WAR` word. Every part follows the watched empire.
- **Reconciliation (5a's hole)**: Kais focused at level 4 → `SetZoomHere(0)` → cursor `galaxy:owner/1`,
  `"Cravers Leaper (AI), -35, 33, COLD WAR, 1 of 6"`. From a player-owned system it lands on that
  system's spoke row. Never the zoom slider. Headings at level 4 carry the reading.
- **Hacking forced on** (recipe landed in `test-recipes/galaxy-map.md`): `Bandwidth Allocated: 0/55` ·
  `Hacking Speed: 100` · `Hacking Operations: 0/1` · `Sleepers: 0` · `Sleeper Repartition, checkbox,
  checked, unavailable, You have no Sleepers .` · the three console switches with the game's
  descriptions; Enter on the defensive switch opened the **eleven** defensive programs (the audit's
  exact list) through the mod's own activation. Focused-tooltip pair on the repartition toggle:
  `shown:true`, one row, raw `#E73C3C#You have no Sleepers [traitor].` = the announced text.
  **Restore** by `POST /loadsave` → `hackingEnabled=False`, `metaModifiers=2`, all three transforms
  invisible, caption prerequisites untouched (never bypassed).
- **Landings — the game measured first**: with `GalaxyLocate.Suppressed`, the game's own reveal on a
  fleet in-mode slides the camera and **stays** in the lens (`afterScan=True`, level 8). Then: fleet
  at Economy → `"Galaxy"` then the fleet's row, `scanning=False level=13`; system at Economy → **no**
  `"Galaxy"`, `scanning=True`, `zoomStep 7` both sides, camera slid, cursor on Sabel under its owner
  heading; system at the Diplomacy band → `"Galaxy"` then the ordinary landing at 13.
- **Inspect**: survey at 1 in-mode — `"Corvus constellation"` · `"Unexplored, -66, -26"`, swept east
  `"Influence contested by Epistis"` / `"Edge of…"` / `"In…"` / `"Out of Epistis's influence"`, with
  no lane, bookmark, fleet or probe. Ordinary cell at 8: names its star lane. 2↔3 live: mode
  continues. Slider 10→11: `"System scan"` · **`"Exited inspect mode"`**, camera untouched. Arming at
  12: cell reads, then `"Economy scan"` · `"Zoom level 9 of 15"`, `Live=True`. **Open-space A/B**: the
  SAME square (-79, 4) at level 8 says `Probe, -79, 4` in the ordinary view, bare `-79, 4` under the
  lens. Zero `Warning` lines in `/log` across the final sweep.

## New phrases — for owner sign-off

**One**, the one already approved: `galaxy.bookmark.group` = **"Bookmarks"**.

Everything else reuses an existing word — named so a reuse can be vetoed: the battle row says
`scan.battle` ("Battle between {0}") rather than the spec's literal "battle: ⟨sides⟩"; the swap
toggle is the GAME's `%DiplomacyScreenSwapModeTitle` ("Swap position", whose Description is the very
tooltip the game hangs on that toggle); the console's switches are `%HackingDashboard<Mode>Title`;
an unnamed star is `GalaxySystemUnexplored`; empires are `GuiEmpire.GetLeaderName`; the relation is
the game's own `DiplomaticRelationState` title.

## Judgement calls

- **The swap toggle is a CHILD of the empire row, not the row itself** (the retired `BuildDiplomacy`
  made the row the checkbox). The row must keep the level-3 heading's shape or "one continuous shape"
  breaks the moment a toggle draws; and it is the standing widget-is-a-node rule. Reversible.
- **Announcement order is the map's**: label parts come before the role word, so a row reads
  "⟨leader⟩, ⟨centre⟩, group, ⟨relation⟩" — the shipped `Byrtus, -25, -42, group, No owner` shape —
  not the spec's leader-relation-centre.
- **A spoke row carries no owner word**: it would come from what the PLAYER sees of the colony, which
  at a system the lens reveals over fog would contradict the heading it hangs under.
- **An empire with neither toggle nor spokes is a LEAF**, not an empty group. Same key either way.
- **Relation/centre are the WATCHED empire's at 1–2 and the PLAYER's at 3+**: `WatchingEmpire`
  persists across lenses, and the closer lenses draw no diplomacy.
- **Spoke and battle rows refuse the cell, Enter, the leap trail and the restore**
  (`PlacedRow.Drawing`) — the inert default, since 1–2 arming is the standing open owner question.
- **Eliminated majors are excluded** (no colony → the lens draws neither centre nor line).
  `BandKind.OpenSpace` was added rather than special-casing the cell; normal-view values are
  identical to what `MapDetail` gave, so nothing there changed.

## Fixture-blocked

Battle rows (no save has a fight in orbit under the lens — code-walked against
`RefreshBattles`/`CollectFightingEmpires`); the swap toggle's UI for a FOREIGN empire (Leaper is
known through the colony Kais, its home unexplored, so only the player's own disabled toggle drew);
all hacking per-row content (allocation cells, operation lines, traitor-empire rows, program costs
and tooltips) and the whole in-map hacking picture; scan-notification chips; empire ordering beyond
n = 2; the `[traitor]` glyph in the repartition failure sentence, which reads as nothing
(`UnknownIcons` reports no token, so it is stripped rather than missed).

## What 5c must know

`ScanRows.AddScanSystem` emits the lane children (`AddStarlanes`) — the trade-route weave hangs off
there and off `LaneRows`; the flat `scan:routes` group is still gone. `Bands` now has SEVEN kinds (a
new column needs `Kinds`, both ladders and `Row(...)`). `ScanLensPanels` is `partial`. `ScanCircle`
is the one place the scan prefab's per-planet widget is resolved, so the pinned null-probe sweep
starts there. Hotspots are free: `ModEntry.cs` untouched, `ModStrings.cs`/`english.json` gained
exactly one key.

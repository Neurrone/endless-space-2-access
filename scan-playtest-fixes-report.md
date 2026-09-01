# Scan-mode playtest fixes

Branch `scan-modes`, 7 commits, build 0 warnings, `dotnet test` **1193/1193**, verified live on `ES2Access-r2`…`r8`
**against the owner's own campaign** (turn 28, one major met, two colonies). Game left RUNNING,
`speechAvailable:true` after every reload, zero `Warning`/`Error` since the final one. `docs/generic/` untouched
except `sync-generic-src.ps1`, run for two mirrored Core files (`graph-ui/KeyGraph.cs`, `hot-reload/ModEntry.cs`) —
mechanical, no prose. **Restored exactly as found** (probed first, checked last): camera `focus [0.884, 0, -48.45]`
step 10, cursor `galaxy:constellation/1/bookmark/2`, scan OFF, inspect off, the scan-view information tick OFF (a
persistent registry value — turned on to measure, turned back), every branch opened during testing re-shut,
`TooltipDelay` untouched (found 0, left 0). No `/loadsave`, no order, no turn, no bookmark change.

## Per fix: root cause, evidence

**1. (item 10) The survey arms at the Diplomacy band** — `89bb8c8`. **The briefed prime suspect was wrong**: 5b's
`544e4d8` range gates only a LIVE cell (`Update`: `if (_live && !ShowsTheGalaxy())`), never arming. The second
suspect is the whole cause — no row at 1–2 answers "where do you stand": the empire headings are
`PlacedRow.Grouping` and the battle rows `Drawing` (both refuse), and `spoke` is a segment the table does not name,
so the walk up reaches the refusing heading. Fixed as a band-scoped place answer (`DiplomacyRowPlace`), asked only
while the lens draws empires, leaving Enter / the leap trail / the restore as the registry declares them.
**Evidence**: spoke row → `Inspect mode, Cursor 1 by 1` · `Serpens constellation` · `In your influence` · `Heka,
-1, -9`, `Live=True`; empire row → the same at `Dusay, 0, 0`; `galaxy:bookmarks` → `consumed`, `speech: []`,
`Live=False`.

**2. (item 8) A landing opens a shut owner heading** — `1d9e956`. Landings read ancestry out of the target's KEY
and the scan headings are deliberately not in their stars' keys, so the target row is undeclared,
`DeepestDeclaredAncestor` finds nothing, and the landing does nothing, silently. Fixed at the ancestry, not per
caller: `KeyGraph.GroupingAncestor`, a page-supplied hook asked about the key and each of its path ancestors and
answered outermost. **Evidence** at Economy with `galaxy:owner/none` shut by hand: `ui.right` on `Starlane 1 to
Primus` → `"No owner, group, expanded, 3 of 4, Primus, 17, 21, …"`; `galaxy.scanGoTo` onto Rigel → the same shape,
`zoomStep` 7 both sides. Both did nothing at all before.

**3. (item 9) The empire row names its home** — `4a2654d`. The gate is the drawing's own: the label whose
empire-name LINE is painted for that empire, which the game paints exactly at `ExplorationState >= 2 &&
IsMajorHomeSystem`. **Evidence**: `Imperials Neurrone, 0, 0, Home System Dusay, group, expanded, 1 of 2` — the
capital that had no row of any kind before.

**4. (items 5+6) The System lens is a Tab stop** — `696c187`. Map stop lean (the `scan:system` group gone from its
head); a stop after it holding `scan:system/name`, the tick, and three regions. **Evidence** at Dusay: Outputs `12
Influence / 49 Science / 57 Dust / 89 Industry / 38 Food / 75% Population`, present with the tick OFF (they are
their own panel — measured). With it on, **System's Rank** (the game's caption): the drawn `Overall system rank
[2/4]`, then `FIDSI, rank 1 of 2` · `Defense, rank 2 of 2` · `Population, rank 1 of 2` · `No. of representatives,
rank 1 of 2`, cross-checked against the bars' own `1st/2nd/1st/1st`; then the sheet, `Turn 28 / rank 2 of 4 / 4`
back to turn 1, every cell cross-checked against an `/eval` walk of `TakeSnapshot(turn)` (t21 `known=3 rank=0` →
`rank 1 of 3`), crossing right saying the game's caption `No. of systems in my Empire, 4`. Tick: `checked` +
`System information shown`, `not checked` + `System information hidden`, registry back to `False`. **Re-centre**:
cursor on Heka's row, camera driven to Dusay (panel `Dusay`) → Tab in → camera and panel both at Heka, rung
unchanged.

**5. (item 1) The hint names the coarse chords** — `7886e35`. Both modes: `Shift+Left Arrow or Shift+Right Arrow to change detail level` / `… to change lens`.

**6. (owner ruling, mid-stage) Per-route lane states** — `1290506`. `TradeWeave.Traffic.Mixed`, the template and
its key are removed rather than left unreachable; each ride carries its own route's state and the merge survives
only for finding which routes share a lane. Offline only — no save has ever had a trading company, so the PINNED
live verification inherits the new wording. `8904402` is the doc landing (`interaction.md`,
`test-recipes/galaxy-map.md`, `roadmap.md`).

## New or changed phrases — for sign-off

| key | English | note |
|---|---|---|
| `scan.empire-home` | `Home System {0}` | new; reuse did not compose (`%HomeSystemTitle` is a bare word) |
| `scan.system-outputs` | `Outputs` | new; region name — the lens captions those six labels with nothing |
| `scan.system-rank` | `rank {0} of {1}` | new; per-property lines and history cells |
| `scan.system-rank-region` | `System rank` | new, and never heard here — fall-back if the caption group is re-cut |
| `scan.system-info-shown` / `-hidden` | `System information shown` / `hidden` | new pair, fleet-panel idiom |
| `hint.change-detail-level` | `{0} or {1} to change detail level` | replaces `hint.change-zoom`, key renamed with it |
| `scan.trade-lane-mixed` | — | **REMOVED** |
| `scan.system-info` | `System information` | unchanged, now also the STOP's name (as briefed) — so the stop and the tick inside it share the words |

## Judgement calls, flagged

- **The Tab-into re-centre** (briefed as allowed to slide): the game's own pan, never a rung change, and only
  where the cursor's star and the camera really disagree — in the ordinary case the map's camera rule has already
  put it there, so it earns its keep only when something else moved it.
- **The rank table's columns** are `[turn, rank, known systems]` as briefed, the rank cell a whole sentence so it
  needs no caption and the third under the game's own; the `of M` therefore repeats the third column's figure — say
  the word and it becomes a bare number under a new caption.
- **`Remains`** takes the remains' own drawn TITLE as its region name — the panel is an icon, a title and a
  description with no caption widget, so there is no game word "Remains" to take. **The per-property denominator**
  counts this system too, where the game's axis counts the others alone ("rank 1 of 1" beats "of 0"). **A route
  doubling back** over its own hop is one line, blockade winning. **The placed-row registry gains one band-scoped
  exception**, documented in both files: the diplomacy band overrides where such a row STANDS, never what it does.

## What his save could not show

One major met and never at war: **no battle row**, **no foreign swap toggle**, **no foreign home** (`Home System`
is proved for his own empire only), **no ordering beyond n = 2**. **No remains** on either colony (`CanShowRemains`
false), so that region is code-only; **no trading company**, so the per-route weave change is offline-proved; **no
ghost, no "???" system, no DLC hacking content**. And **no physical key was pressed** — everything above is `POST
/input`, so Ctrl+I, Shift+arrows on the ladder and Tab into the new stop stay manual-test items.

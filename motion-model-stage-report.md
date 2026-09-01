# Stage 2 — the motion model

Branch `scan-modes`, 4 commits, build 0 warnings, `dotnet test` 1187/1187 green, live on
`ES2Access-r9` against `[Beginner] access test`. Game left RUNNING, camera home (`focus [68.861, 0,
-22.453]`, step 9), cursor on `hud:empire/screen/EmpireScreen`, Sabel collapsed, both constellations
expanded, no fleet selected, no field focused, 8 empire notifications (the count the session began
with), `CurrentGuiNotification` NULL, test bookmark 5 cleared, `docs/generic/` untouched.

1. `f927483` **graded expansion + collapse-restore.** `Bands.LowestLevel` (Core, tested) →
   `ZoomBands.LowestLevel`; `GalaxyViewLevels.SetZoomHere`. `FollowPlace` downgrades "inside" to
   "at" while the band draws no detail (so 3–6 open in place) and records the level a real jump left
   (`NoteJump`, per system, an INSTANCE field so `ModEntry.Stop` wipes it); `CollapseZoom` hands that
   back, else `GalaxyInspect.EntryZoomCeiling` (step 8 = spoken 9), now `internal` so the mod's two
   "sane default camera" numbers are one. `ConstellationRows.ZoomInto` on a new `OnExpand` zooms 1–2
   to level 3 on the constellation's centroid and opens, with the `BetweenViews` hold.
2. `3b413b8` **snap landings + both bookmark gaps.** `EnsureBand`/`BandNeeded` in `GoTo` before the
   cursor is sent anywhere — overriding the caller's `MapCamera`, since a missing row is not a camera
   preference — and in `LandInside`. Point bookmarks force nothing and, at 1–2, are declared at the
   TOP level (`BookmarkPoint.Under` beside `Sky`, so the KEY is unchanged across the boundary).
3. `f1ec8e6` **dot fidelity** — `PlanetRows.AddPlanetDot` at `BandFidelity.Dot`.
4. `1093970` **doc landings** — `interaction.md` (three contracts), `test-recipes/galaxy-map.md`
   (pairs + the system-bookmark fixture note), `roadmap.md`, `notifications.md`.

## Pairs (`DevProbe.Camera()` before → after, `/speech`)

| pair | measured |
|---|---|
| expand a system at 3, then collapse | step 2 → 2, `focus [34.16,0,-27.24]` unchanged both ways; 3 lane children; "Starlane 1 to Rigel, east, 1 of 3"; no zoom line either way |
| expand at 5, and at 13 | at 5: step 4 → 4 unchanged, 3 lanes + 2 fleets, and stepping onto the fleet row ALSO moved nothing (it snapped to 13 before). At 13: step 12 → 12 (label offset 34.16→33.76 only), straight to "Manage system…" |
| expand at 8, then collapse | step 7 → 12, "Zoom level 13 of 15, Orbital" · "Manage system, button, 1 of 10"; collapse 12 → 7, "Sabel … collapsed, 8 of 18" · "Zoom level 8 of 15, System details" |
| collapse, memory wiped by `POST /reload` | step 12 → 8; "Zoom level 9 of 15, System details" — the fallback, live |
| constellation at 1, and at 7 | at 1: step 0 → 2 on Serpens' centroid, "Zoom level 3 of 15, Systems and star lanes" · "Ita, 5, 34, group, outpost, collapsed, 1 of 18". At 7: step 6 → 6, no zoom line, only the descend's own slide onto Ita |
| scanner go-to, planet from 8 / system from 4 | planet: step 7 → 12, cursor on `…/system/585/planet/1`, full reading — landing level 13 ✓. System: step 3 → 12, today's framing landing, unchanged |
| fleet, `ui.nextIdleFleet` from 3 | step 2 → 4, "Zoom level 5 of 15, Systems, star lanes and fleets", **no further**; lands on the fleet row (impossible before — no row at 3) |
| bookmark jump at level 1 | POINT: zoom unchanged, camera slid, **"Bookmark 3 at -66, -26, 6 of 6" spoken** (gap (a) closed). SYSTEM: step 0 → 2, branch opened, lands on the system's first lane, whole path announced |
| type-ahead onto a planet from 4 | **0 results** — a search sees only what the band's tree holds (the ruled behaviour), so type-ahead needs no band rule of its own; from 8 the same search finds it (5 results) and the ordinary camera rule completes the detail to 13 |
| tree at level 1 | 2 constellation groups + all 4 point bookmarks at the TOP level, interleaved by position, keys unchanged (gap (b) closed) |
| dot vs full | `…/system/476/planet/0` at 13: "Sabel I, group, Medium Mediterranean, Inhospitable, collapsed, 2 of 10" + 5 FIDSI lines + Dust Ruins + Titanium + dossier children. At 12, SAME id, cursor survived: "Sabel I, Inhospitable, 2 of 10" — a leaf, buffer = name + status + the circle's own tooltip "This planet is too hostile to be colonized by your empire" |

Reconciliation 13 → 4 on a fleet row still lands on Sabel's system row. Reload-restore: after
`POST /reload` (r9) a fresh expand at 8 → 13 and collapse → 8, clean — but **a reload wipes the
EXPANSION set too**, so the fallback pair needs the branch re-opened (in place, at 13) first.

## Judgement calls (flagged, not buried)

- **Dot content.** Brief: "name + colonisation status"; spec file: "what the drawn dot and its hover
  carry (the same shape as scan mode's dot-parity rows)" — and that ruling names the dot's drawn
  overlays. I kept the two the mod already models (curiosity count, mining-probe line) and dropped
  size/type, the detail buffer and the dossier children. One place to reverse.
- **"Collapse at 13 with no jump memory" reads as the level-9 fallback, not "no motion"** — the
  brief's two clauses collide, only this makes the required reload evidence possible, and it is
  today's behaviour with the number moved from step 9 to 8. At 3–6 there is genuinely no motion.
- **A bookmark jump to a SYSTEM at 1–2 lands at level 3, not 13**: the forced band is the target's
  MINIMUM, and past it `LandInside` keeps its own promise ("exactly where walking in with Right
  would have") — a lane, no zoom. The scanner's go-to on a system still zooms to 13, as it always
  did, and **Backslash's zoom-out is unchanged** (the game's default step): only collapse was ruled.
- **A bookmarked SYSTEM still has no ROW at 1–2** (its annotation rides the system's row): its jump
  works, browsing to it does not. No ruling covers a top-level row for one, so it is on the roadmap
  as an owner question rather than invented here. **`NoteJump` records any inside-snap that raised
  the level, landings included**, so collapsing a system a go-to dived into also hands the view back.

## Manual test

**Physical PageUp/PageDown zoom is FIXED, half-proven.** The blocker was never the mod:
`CurrentGuiNotification` stood on a `NotificationPopulationGained` with no popup drawn and no entry
in any empire's list, and `CheckInputs` refuses all keyboard zoom while it is non-null.
`DismissGuiNotification` does not clear it (that call is about the list); invoking the manager's own
private setter with null does, raising the same Refresh a real close does. Cleared, a real
`POST /key PageDown` moved the rung 9 → 7 and spoke both band words. **The reconciliation pair
through that route is NOT proven** — the RDP desktop locked mid-session (`GetForegroundWindow()` = 0,
`/key` 409, three re-focus attempts), so that seat was driven through the injected zoom. One manual
pass: cursor on a fleet row inside an expanded system, hold PageDown past level 5, expect the system
row read out. Every other key here was an ACTION (`/input`), so the chord-level claims for
`ui.right`/`ui.left` stay untested; graded expansion at 3–6 was verified on one system (Sabel); and
no adrift fleet, missile, ally pin or quest marker exists in this fixture, so those kinds' band
minimum (7) ships unsighted.

## For stage 3 (inspect zoom rules)

`GalaxyInspect.EntryZoomCeiling` is now `internal` and SHARED with `CollapseZoom` — re-cutting it
moves both cameras, which is the point. `EnsureBand` runs only when a landing moves the TREE cursor,
so never under a live cell (`plan.FocusNode` is false there): "only the cell moves" is untouched.
`ZoomBands.LowestLevel(kind, fidelity)` is the one way to ask "what level does X need" — use it for
the 3–12 gating. And at 7–12 ANY step INSIDE a system now takes the camera to 13, so a rule assuming
the cursor can sit on a planet child at level 8 will not hold.

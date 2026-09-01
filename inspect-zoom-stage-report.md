# Stage 3 — inspect zoom rules + the level 1–2 territory survey

Branch `scan-modes`, 4 commits, build 0 warnings, `dotnet test` 1187/1187 green, verified live on
`ES2Access-r10`/`r11` against `[Beginner] access test` (left on `r12`, `staleBuild:false`). Game
RUNNING, camera home (`focus [68.861, 0, -22.453]`, step 9), cursor on `hud:empire/screen/
EmpireScreen`, cell dismissed, Corvus expanded / systems collapsed — as found. `docs/generic/` untouched.

1. `ac76747` **zoom contract + exit.** `GalaxyInspect.ShowsTheGalaxy()` reads the boundary off the
   band table (`Bands`) as "the planets have become full cards", never as a hard 13; `Update()` takes
   the cell down through `Leave()` — the no-camera, no-re-seat teardown — the frame the camera crosses
   it. Asked as "where is the camera", so every route is covered by construction. A rung with no
   answer (battle, a level in flight) keeps the mode. `Surveying()` lands with it as the far-end pair.
2. `299f84e` **the survey.** `Influence()` skips only its change-suppression while `Surveying()`, so
   at 1–2 whose the square is is said on EVERY square; everywhere else the crossing idiom is
   byte-identical. Nothing else in the reading moved; the cursor keeps its 1×1 default.
3. `a6e4761` **the border leap.** `LeapToOwnershipChange` under `FollowWest`/`FollowEast`, fired only
   where the cell offers NO candidate; `ReadingAt` borrows the mode's own cell influence the way the
   skip's signature does. No new ModStrings key, no `ModEntry` key-claim change, no new static state.
4. `017c65f` **doc landings** — `interaction.md` (zoom contract + leap), `test-recipes/
   inspect-and-influence.md` (the pairs, the oracle, the reflection recipe), `roadmap.md`.

## Evidence (`/speech`, `DevProbe.Camera()`)

| pair | measured |
|---|---|
| arm at 13 | step 12 → **8**, armed, "Inspect mode, Cursor 1 by 1" … "Zoom level 9 of 15, System details" — the ceiling still holds after stage 2 |
| arm at 5 | step 4 → 4, armed, no zoom line |
| arm at 1 | step 0 → 0, armed; "Corvus constellation" · "bookmark 1, Unexplored, -68, 18" |
| 12 → 13, slider FINE step | "13 of 15, Orbital" · "Exited inspect mode"; focus `[34.352,0,10.188]` unchanged both sides |
| 12 → 13, slider COARSE step | same two lines, focus unchanged |
| 12 → 13, **physical `POST /key PageUp`** | "Exited inspect mode" · "Zoom level 13 of 15, Orbital" (this route says them the other way round — the game's camera update lands before the zoom watch) |
| 3 → 2 → 3 | at 3: `-7,0` "Edge of your influence", `-6,0` "In your influence", `-5,0` silent. At 2: **every** square "In your influence". Back at 3: silent again |
| survey vs oracle, level 1, y=0, x −12→+12 | oracle (`IInfluenceService.TryGetInfluence` at each square centre — the game's own point resolution, the field the disk is painted from): Neurrone −7…+7, nobody outside. Cell: "Edge of" at ±7, "In" −6…+6, silence beyond. Exact agreement. "Out of Serpens constellation" announced where crossed |
| leap east, empty cell in own territory | (4,0) → "Edge of your influence" · "7, 0" |
| leap west from there | (7,0) → "In your influence" · "6, 0" |
| one lane (regression) | (-1,0) → east travels to Dusay (0,0), west to Rigel (-16,-5) — unchanged |
| ambiguous cell (regression) | Dusay (0,0), three lanes: both keys silent, no leap |
| no change to map edge | (10,0) east: silent, `Centre()` still `10,0` — today's answer, unchanged |
| reload | armed, `POST /reload` (r10→r11): `Live` false, re-arming works |

## Judgement calls (flagged, not buried)

- **The travel keys are Alt+Left/Right, not shift+arrow.** Brief and spec both say "shift+arrow";
  shift+arrow is the SKIP (`CellSkip`) and Alt+Left/Right is travel-by-cell-contents. I put the leap
  on the TRAVEL pair, which is what both texts describe in words. It therefore has no north/south
  arm — there are only two travel keys. One place to reverse if the owner meant the skip pair.
- **"Empty cell" = no candidate, not "no contents".** A cell with TWO lanes, or fleets bound for
  different places, keeps today's silent AMBIGUITY refusal and does not leap: that refusal is
  deliberate, and layering over it would change a ruled meaning. So does a one-lane cell whose far
  end is dark (the no-leak refusal).
- **The leap's "different" is the crossing announcement's own `InfluenceReading.Equals`** (holder,
  cover, contesters), not a bare owner comparison — so the leap can never land where the arrival
  then declines to call a crossing, and "edge of X" is a landing (the boundary is the thing).
- **The survey is ADDITIVE** (the brief's "add only what is missing"): at 1–2 the cell still names
  fleets, probes, lanes and bookmarks, which the band does not draw. The spec's survey sentence
  enumerates only territory + known systems + constellation crossings. If that enumeration was meant
  as exhaustive, this is the one place to cut.
- **Unowned space says nothing about ownership** — no phrase exists for "nobody's territory" and none
  was invented (open question, roadmap). "Out of X's influence" on the step out is unchanged.
- **Committed one hunk that was not mine**: `docs/roadmap.md` already carried an uncommitted owner
  paragraph (the bookmarked-system ruling + two ratifications) when this stage started; it rode into
  `017c65f` because it shares the file. Called out in that commit message too.

## Open / manual test

- **Arming at 1–2 has almost nowhere to stand.** Those bands declare only constellation headings
  (which REFUSE arming, ruling 2026-08-31) and point bookmarks (which arm). On this fixture the
  survey is reached from a bookmark row or by zooming out with a live cell. Owner question on the
  roadmap: should a constellation heading arm at its centroid there?
- **Manual pass:** every key was an `/input` action except the one `POST /key PageUp`, which worked
  (desktop unlocked, `CurrentGuiNotification` null). Untested physically: the Alt+Left/Right chords'
  own claim while the cell is live, and the wheel as an exit route. Fixture-blocked: multi-empire and
  contested survey wordings (only two circles are perceived).

## For stage 4 / 5

`ShowsTheGalaxy()` and `Surveying()` are the two band predicates the mode owns; both read
`ZoomBands` and neither knows about scan mode, so `Bands.Scan`'s rows decide their answer the moment
`Scanning` is true — the scan ruling ("inspect tops out at level 10; Ctrl+I on the System lens pulls
out to Economy") needs the SCAN ladder's Planets/Systems columns to say so, or an explicit lens
branch. Nothing else in the mode is zoom-aware. `LeapToOwnershipChange` costs one `CellNow()` per
candidate square (0.01 ms in empty space, ~1.2 ms inside a bubble) and only on a keypress.

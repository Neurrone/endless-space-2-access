# Inspect mode and influence

The map's cell cursor and everything influence-shaped: the node line, the cell reading, the
ground sweep and the watch that speaks at a turn boundary.

## Influence on `[Beginner] test`

**Influence on `[Beginner] test`: two radii, no contest, no foreign influence** (measured
2026-08-21, turn 21). Perceived systems that project at all: Dusay 6.563 → 6.634 and Osulo 4.584 →
4.633 (Niris, a minor faction) — both round to the same one-decimal figure, so **the growing and
shrinking wordings are fixture-blocked here** and only "no change" appears live (the three
branches are unit-tested in `InfluenceTextTests`). Heka is an outpost and the service answers
false for it: no line, which is the negative test. Every node's influencer is its own colony, so
**the "Under … influence" suffix and the contested line never appear on this fixture unaided**.
The three probes that stand in, each an exact-undo mutation from `/eval`:
- **A foreign or your own influencer over a place.** `node.SetSystemWhichInfluences(colony)` on a
  perceived node (Electra is empty and safe — a node with a colony fires
  `ColonizedStarSystem.RefreshInfluenceState` and rewrites a descriptor), read, restore.
- **Somebody reaching without winning.** `colony.LastInfluenceValue = 50f` makes that colony's
  circle cover the whole constellation; read the contested line on any node it now reaches.
- **Undo for both:** `Services.GetService<IInfluenceService>().UpdateInfluence()` — the game's own
  pass recomputes every radius and every node's winner from the simulation (ES2 facts). Verified
  by diffing `/gui/graph?buffers=1` before and after: identical bar the HUD clock.
Empire indexes here: Neurrone (the player) 0, Niris 4 — the contested list is sorted by index, so
the player sorts first in a mixed list.

## Inspect-cell influence

**Inspect-cell influence on `[Beginner] test`** (measured 2026-08-21, turn 21).
`IInfluenceService.InfluenceStrenghtPower` is 4 and the galaxy has 86 `GameNodes`. Only TWO circles
are perceived — Dusay (yours) R 6.56 at pair 0,0, and Osulo (Niris) R 4.58 at pair −30.85,−31.81 —
and they are 44.3 apart, so **every multi-empire wording is fixture-blocked unaided**. Sixteen other
colonies project radii the player has never seen; they are why the perception filter exists, and
`Sabel` (Amoeba, R 4.58 at −34.72,−4.79) is the nearest of them.
The two walks that need no mutation, both spoken live:
- **Your own bubble.** `JumpTo(0, 0)` → "In your influence" (Dusay's own node is in the cell and the
  node sample agrees), Right ×6 silent, x=7 "Edge of your influence", x=8 "Out of your influence"
  (that cell is also wholly fogged). Growing the cursor at x=6 from 1 to 3 crosses the rim and says
  "Edge of your influence" — the size is part of the memo key.
- **A foreign bubble.** `JumpTo(-29, -32)` → "In Niris's influence", −28/−27 silent, −26 "Edge of
  Niris's influence", −25 "Out of Niris's influence".
- **Skip.** From 1,0 Shift+Right answers "Skipped 5 squares" and lands on 7,0 reading the edge.
The blocked wordings, and the one bounded mutation that produces all of them at once —
`osulo.LastInfluenceValue = 41.8f` (found by walking `GameNodes` and matching
`TryGetInfluenceRadius`'s system name), which puts Niris's rim 2.5 from Dusay and the strength
crossover at 4:
| cell (diagonal toward Osulo) | reads |
|---|---|
| 0,0 and −1,−1 | "In your influence" |
| −2,−2 | "In your influence" + "Influence contested by Niris" |
| −3,−3 | "Edge of Neurrone's and Niris's influence" (the LIST form) |
| −4,−4, −5,−5 | "In Niris's influence" + "Influence contested by your empire" |
| −6,−6 outwards | "In Niris's influence" |
**Undo is the same `IInfluenceService.UpdateInfluence()`** the node recipe uses; verified by
re-running the classification sweep and the radius dump and getting the pre-mutation output back.
The same inflation is how the FOG GATE is proved: with it up, `-40,-44` is unexplored and inside
Niris's circle, `SystemInfluence.OverCell` (ungated) answers "In Niris's influence", and the mode
walking onto it says "Unexplored" and then the coordinates, and nothing about influence at all.

## The influence ground sweep and its two readers

**The influence GROUND sweep and its two readers on `[Beginner] test`** (measured 2026-08-21, turn
21). `InfluenceGround.Sweep(Gui.PlayerEmpire, out queries)` is public, so the whole classification is
one `/eval` away and needs no keypress. Pristine: **169 squares, 114 queries, 8 ms, 113 provably
yours, 0 taken** — so the scanner's **Contested Influence category is EMPTY on this fixture** and its
"none found" wording is what a press in it answers. With the same `osulo.LastInfluenceValue = 41.8f`
inflation the inspect recipe above uses (set it and read; do NOT call `UpdateInfluence` after
setting, which recomputes the value straight back from the simulation and is the UNDO): **41 taken,
92 still provably yours, 6735 queries, 76 ms** — the worst case, a rival circle swallowing the whole
of Dusay's reach.
- **The approaching border, which must stay silent.** `osulo.LastInfluenceValue = 38.0f` is the one
  radius found where exactly ONE previously-certified square loses its certificate and NO
  previously-certified square has a taker (7 squares inside Dusay's reach ARE enemy-won at 38, but
  none of them was ever certified yours). Sweeping ladder against the pristine certified set: 37 and
  37.5 → nothing changes; **38 → thinned 1, lost 0**; 38.5/39 → lost 3; 40 → lost 9; 41.8 → lost 20.
- **Driving the turn-end watch without ending a turn.** The mutation cannot survive a real turn — the
  game's own influence pass recomputes `LastInfluenceValue` before the watch sweeps — so the diff is
  driven by raising the watch's own boundary flag by reflection:
  `typeof(ES2Access.UI.ModNotifications).Assembly.GetType("ES2Access.UI.InfluenceGroundWatch")
  .GetField("_turnBegan", NonPublic|Static).SetValue(null, true)`, then read `/speech`. The same
  field walk reaches `_empire` (set it null to pretend another galaxy loaded and prove the
  re-baseline). Measured sequence: flag on the pristine field → silent, `groundWatched` 113 (the
  baseline); inflate to 41.8 + flag → **"Dusay's influence lost ground to Niris"** once, one line for
  41 squares, `groundWatched` 92; undo + flag → silent, back to 113; inflate to 38 + flag → silent,
  113 → 112.
- **The real hook.** `Gui.GuiService.GetWindow<EndTurnWindow>(false).EndTurnService.Target
  .TryToEndTurn()` ends the turn — the FIRST call is eaten by the idle-systems/empty-queue validator
  (it speaks "Construction Queue empty", "Idle Systems") and the second gets through; the window's
  `EndTurnService.Target.Turn` reads STALE afterwards, so confirm on `/speech` ("Turn 22") instead.
  Crossing the boundary moved `groundQueries` 1201 → 1082 and `groundWatched` 112 → 113, which is the
  proof `GameClientState_Turn_Begin` reaches the watch. Ending a turn MUTATES the save's world —
  `POST /loadsave "[Beginner] test"` afterwards, then re-minimize the tutorial.
- **The probe** is `DevProbe.Notifications()`: `groundSubscribed`, `groundWatched`, `groundTiles`,
  `groundQueries`, `groundMilliseconds`.

## What a crossing says

**Inspect mode names CONSTELLATION crossings** (2026-08-20): its own utterance AHEAD of the cell (the
"Skipped N squares" precedent, taking the interrupt the cell would have had) — on entry when the
initial tile is inside an explored hull, then change-only: "{0} constellation" crossing in, "Out of
{0} constellation" crossing out to unassigned space. `ConstellationMap` is explored-only so fog leaks
nothing; a CONSTELLATION boundary is not part of cell identity for Shift+arrows (the skip runs
through them); suspend/resume re-reads the cell only, never the crossing.

**It names INFLUENCE crossings the same way** (2026-08-21): a second change-only utterance behind the
constellation's and ahead of the cell — "In {0}'s / your influence" for a cell PROVED to be one
empire's throughout, "Edge of …" where the boundary runs through it (several empires collapse into
one list line), "Out of {0}'s influence" naming what was left on the step into unowned space, and the
system row's own contested line alongside any of them. Growing the cursor over a rim is a crossing
too, so the memo is keyed on the SIZE as well as the cell. Unlike the constellation, the
classification IS part of cell identity: Shift+arrow stops at a border (`in:`/`edge:`/`vs:` + empire
index tokens in `CellSignature`). Gate: a cell wholly under the fog says nothing about influence and
contributes no token, and an empire whose colony node the map is not showing is stripped of its NAME
while its field stays in the arithmetic — so an unseen neighbour can cost a cell its "in" and can
never be named by one. The measured wordings are **Inspect-cell influence** above.

**A cell names a starlane only where the fog DRAWS it** (2026-08-20): the link gate answers "is this
lane lit", never "lit HERE" — `Lit` samples the cell's unit squares through
`IVisibilityService.IsExplored`, the same field the cell's "Unexplored" word uses; the tree's lane
rows keep the link-level gate (they hang under the system, a thing not a place).

**A targeting cursor arming re-reads the standing control** (`AnnounceNextLanding`, arming path only;
seat actions and the modal-picker pop stay re-read-free). The standalone instruction utterance is
SKIPPED whenever a readout of the map stop will carry it: the cursor is inside the map stop (with a
dismissed-inspect re-read coming), OR a pending focus request targets a node inside it
(`GraphNavigator.PendingStopKey`, resolved through the same ancestor walk landings use — a request
into a still-collapsed branch answers correctly). This also fixed a pre-existing double with NO
inspect involved: probe arming's own seat lands inside the map, whose context IS the instruction, so
the watcher's line + the landing spoke it twice. The standalone line is the plain else-branch
(dev-injected arming with nothing pending); "Target selection ended" is never swallowed — a landing
in flight would not carry it (`!ended` guard). Pump-order caveat: a request made AFTER the watcher
runs in the same frame is invisible to it — the guard works because deferred landings into collapsed
branches outlive their frame (owner rulings 2026-08-20).

## The zoom contract and the territory survey

Rules: `docs/interaction.md`, **Inspect-mode keys**. Driving the cell to a chosen square without
twenty arrow presses: the mode's `JumpTo` is public, so reflect `_driving` out of `GalaxyInspect`
and invoke it — `typeof(ES2Access.Screens.GalaxyInspect).GetField("_driving", NonPublic|Static)`,
then `GetMethod("JumpTo").Invoke(m, new object[] { x, y })` with `?settle=900`. Zoom is set with
`ES2Access.UI.GalaxyViewLevels.SetZoomHere(step)` (step = spoken level − 1), which lands
immediately — the rung is readable in the same `/eval`.

**On `[Beginner] access test`, home (the cell's own 0,0) is Dusay**, and the player's influence
sources are `r=7.15` at (0,0), `r=4.50` at (16,20) and an outpost at (-34,-4) projecting nothing
(outposts force `SystemInfluenceRadius` to zero). So the y=0 line through home is the boundary walk:
the game's own oracle, `IInfluenceService.TryGetInfluence` at each square centre, answers Neurrone
from x=-7 to x=+7 and nobody outside — and the cell says "Edge of your influence" at ±7 (the 1×1
square straddles the rim), "In your influence" from -6 to +6, and nothing at ±8 and beyond. That is
the oracle pair for the survey; it is the game's point resolution, the same field the disk is
painted from, not the mod's own classification.

Measured pairs (2026-09-01, `ES2Access-r10`):

| pair | measured |
|---|---|
| arm at 13 | camera step 12 → 8, mode armed, "Inspect mode, Cursor 1 by 1" … "Zoom level 9 of 15, System details" — the entry ceiling still holds after the stage-2 zoom machinery |
| arm at 5 | step 4 → 4, armed, no zoom line |
| arm at 1 | step 0 → 0, armed; first reading "Inspect mode, Cursor 1 by 1" · "Corvus constellation" · "bookmark 1, Unexplored, -68, 18" |
| live at 12 → slider fine step in | "13 of 15, Orbital" then "Exited inspect mode"; camera focus `[34.352, 0, 10.188]` unchanged either side — only the zoom the player asked for moved |
| live at 12 → slider COARSE step in | same two lines, focus unchanged |
| live at 12 → physical `POST /key PageUp` | "Exited inspect mode" then "Zoom level 13 of 15, Orbital" (the game's own camera update lands before the mod's zoom watch, so this route says them the other way round — both are heard) |
| live at 3 → zoom to 2 → back to 3 | at 3: `-8,0` silent, `-7,0` "Edge of your influence", `-6,0` "In your influence", `-5,0` silent. At 2: every square says "In your influence". Back at 3: silent again |
| survey walk at level 1, -12 → +12 on y=0 | matches the oracle square for square (above); "Out of Serpens constellation" announced where crossed; unowned squares say nothing about ownership |

**Arming at 1–2 needs a placed row and those bands have almost none**: the tree there is
constellation headings (which refuse arming, the 2026-08-31 ruling) plus the point bookmarks stage 2
moved to the top level — so on this fixture the survey is armed from a bookmark row, or entered by
zooming out with the cell already live. OPEN owner question (roadmap).

**Border leap pairs** (`galaxy.inspectFollowEast` / `…FollowWest`, level 1):

| pair | measured |
|---|---|
| empty cell inside own territory, east | from (4,0) → "Edge of your influence" · "7, 0" — the first square the oracle's rim falls in |
| and back west | from (7,0) → "In your influence" · "6, 0" |
| one lane in the cell (regression) | (-1,0) holds `Star lane from Rigel to Dusay`: east travels to Dusay (0,0), west travels to Rigel (-16,-5) — today's semantics, untouched |
| ambiguous cell (regression) | Dusay (0,0) holds three lanes: both keys silent, no leap |
| no change to the map edge | from (10,0) east: silent, `Centre()` still `10,0` — the existing "nothing to travel to" answer, unchanged |

## Inspect mode

**Inspect mode** (`galaxy.inspect` / `galaxy.inspectGrow` / `galaxy.inspectShrink` + the ordinary
`ui.*` actions, all through `POST /input`). Entry lands on the focused stop's OWN pair (2026-08-16:
it used to land on the parent system's, because a fleet/probe/missile/pin row is keyed structurally
and the walk only read `ControlId.Reference`, which only a system's node carries —
`GalaxyHudScreen.cs:1657`). Measured: focus `galaxy:system/491/fleet/1304` (the Patriots, `-37, -31`,
under Osulo at `-31, -32`) and `galaxy.inspect` answers `1st Patriots Navy, Star lane from
Heracles to Osulo, -37, -31` with `DevProbe.Camera().focus` at `(31.884, 0, -53.45)` = origin + (-37, -31). A
row with no thing of its own (a planet, a lane) still answers with its system, and entering from
the empire stop (no place under the cursor) gives home. So entering from the empire stop and from
`galaxy:system/535` (Dusay) both give "0, 0" on
`[Beginner] test` — pick a non-home system to tell the two apart. The measured expectations there:
entry says `Inspect mode, Cursor 1 by 1` (default since 2026-08-19; the size persists per
session, so a re-entry repeats whatever was last set) then `Dusay, Star lane from Rigel to Dusay, Star lane
from Qarius to Dusay, Star lane from Dusay to Primus, 0, 0`. **The cell says its CONTENTS first and
its coordinate LAST** (owner ruling 2026-08-26): systems, special nodes,
fleets, probes, obliterator missiles, ally pins, then the lanes crossing it, then the fog, then the
pair — an EMPTY cell is the bare pair and nothing else, and no reading ever opens with a comma. The
three
open-space kinds in the tree's own declaration order (`AddProbes`/`AddProjectiles`/`AddPins`), off
the page's own `DrawnProbes`/`DrawnProjectiles`/`DrawnPins` lists, so the cell and the tree cannot
disagree about what the map is drawing. `ui.right` twice then `ui.up` twice reads
`3, 0` / `6, 0` / `6, 3` / `Star lane from Dusay to Primus, 6, 6` — that last cell is the
lane-crossing check against known geometry (Dusay 0,0 to Primus 16.5,20.9 enters the cell centred
6,6 and misses the one at 6,3). The camera is the exact
oracle for the pan: `DevProbe.Camera().focus` must equal `GalaxyCoordinates.Origin() + (x, 0, y)` —
`(74.884, 0, -16.45)` at cell (6,6) with home at `(68.884, -22.45)`. Edge refusal: the galaxy's node
bounds are x `[-164.0, 22.8]`, y `[-41.5, 88.3]`, so at 11x11 from (6,50) one `ui.right` reaches
(17,50) and the next answers `Map edge` (measured again eastward from (0,9) at 11x11 — (11,9),
(22,9), then `Map edge`).
Fog: (6,50) at 11x11 reads `Unexplored, 6, 50` (whole), (6,6)
`34 squares unexplored` ahead of its pair — grow/shrink and the count tracks. **The mode's state probe is
`DevProbe.Claims("Escape,Minus")`**: both claim true only while it is live AND the tree cursor is on
the map stop, which is how "Enter on an
empty cell did nothing" is proved to be a refusal rather than an exit (focus unchanged in
`DevProbe.Screen()`, claims still true).
**Suspension off the map (2026-08-17).** `ui.prev` from the map lands on `hud:view-title/name`
(`Galaxy View, 1 of 2`) and `ui.down` from there on `hud:view-title/zoom` — that pair IS the
route to the zoom slider, not one Shift+Tab. With the mode armed and the slider focused,
`DevProbe.Claims("Escape,Minus")` reads `claims:false` on both and `claimsBack:false`, `ui.left`
/`ui.right` answer `5 of 15`/`4 of 15` like any slider, and the marker's reflected world rect is
unchanged — that rect is the "the cell did not move" oracle, since a suspended cell says nothing.
`ui.next` back to the map answers with the map node's own announcement and then, ~12 frames later,
the retained cell (`Ita, 5, 34, group, collapsed, 1 of 13` then `3, 0`); the next
`ui.right` reads `6, 0`. **Arming is a key of the map too (2026-08-17, owner's veto of the jump):**
`galaxy.inspect` pressed from a stop that is NOT the map is not claimed and does nothing — no focus
move, no arming, no speech. Its evidence is a state probe, since a key that does nothing looks in
`/speech` exactly like a key that never arrived: from `hud:view-title/zoom`, `DevProbe.Screen()`
still reads `hud:view-title/zoom` afterwards, `DevProbe.Claims("Escape,Minus")` still reads
`claims:false`/`claimsBack:false`, and `/speech` gains nothing. The same probe is the ONLY way to
check that `galaxy.inspect` re-injected while the mode is live does nothing: claims still true plus
an unchanged cell reading on the next `ui.*` is the evidence. `galaxy.inspect` does not toggle out; the exits are `ui.back`, a landing
Enter made, and leaving the map.
**TYPING IS INERT WHILE THE MODE IS LIVE, so the map's inspect mode can no longer stack a search on
top of itself (2026-08-26).** The old two-press Escape (search first, mode second) is UNREACHABLE
here: `GalaxyHudScreen.SuspendsTypeahead` is true while `GalaxyInspect.Active`, so the letters are
still claimed from the game and simply start nothing. What to measure: with the mode live,
`POST /type "res"` answers
`{"typed":"res","taken":false,"searching":false,"search":"","results":0,"speech":[]}` and `/speech`
gains nothing; `DevProbe.Claims("R,Escape,Space")` reads `R claims:true`, `Escape claims:true`,
`Space claims:false` (Space is still only claimed with a live search buffer); `ui.down`/`ui.up` still
move the cell. The PHYSICAL half is proven here — `POST /key "R E S"` (hold 120, gap 120) answered
`sent:["R","E","S"]` with `speech:[]` and `SearchIsActive` false, and `ui.right` afterwards still read
the next cell, so the mode was live throughout. **Search THEN inspect**: `POST /type "dus"` from the
map stop (`taken:true`, 18 results, focus `galaxy:system/535`) followed by `galaxy.inspect` announces
the ordinary entry — `Inspect mode, Cursor 1 by 1` / `Serpens constellation` / `In your influence` /
`Dusay, …, 0, 0` — with **no "Search cleared" line**, and `SearchIsActive=false`, `SearchText=""`,
`SearchResultCount=0`: the entry drops the search SILENTLY. After `ui.back` ("Exited inspect mode"
plus the map node's line) `POST /type` starts a fresh search from where the cursor was. The mode's own
survival still needs a state probe rather than a speech line: reflect
`ES2Access.Screens.GalaxyInspect`'s static `Live`/`Active`
(both internal — go through `typeof(ES2Access.Dev.DevProbe).Assembly.GetType(…)`) and
`ES2Access.UI.InspectMarker`'s private static `_drawer`, which reads non-null while armed and
**null** the moment the mode ends; the cheap live half is one `ui.right`, which must answer with
the next cell pair (`5, 34` → `6, 34`). The two-press Escape ORDER is still live on the two surfaces
that never suspended typing — the TARGETING cursor's
(`ChangeCursor(typeof(TakeSystemCursor), new AcademyDiplomacyGiveSystemAction())` → search →
`ui.back` "Search cleared", cursor still `TakeSystemCursor` → `ui.back` "Target selection ended")
and the CARRY's (`ModEntry.Carry.PickUp(...)` → search → "Search cleared", `IsCarrying` still true
→ "Cancelled drag"); both of those come free from `SearchAction` running ahead of the switch in
`GraphNavigator.Dispatch` and neither needed a change. Enter on a one-place cell lands on `galaxy:system/<guid>`
with the tree's own announcement; Enter on a fleet-only cell (shrink to 1x1 and walk to `-1, -6`,
`1st Conquerors Navy`) answers `Fleet panel open for …` — but only from a CLEAN cursor; reset one
that is not with `cursors.ChangeCursor(typeof(GalaxyCursor), Gui.GetCursor())`. Auto-exit is driven with
`Gui.GuiGameWindowService.RequestStarSystemManagementViewLevel(guid)` — every keyboard route out of
the map is claimed by the mode itself, so an engine call is the only way to stage it — and the exit
line must land AFTER the whole arrival burst (`Star system`, `Zoom level 14 of 15`, `Planets, Raia…`,
then `Exited inspect mode`). **Leaving takes the CAMERA back too**: the exit that returns
focus re-centres on the position the mode was ENTERED at (`_entryAt`, taken on the way in — the
cursor is re-seated in the same breath, so asking the navigator again would read the old node). A
landing that Enter made does NOT re-centre, or it would fly the camera off the thing just landed on.
Measured: enter at the Patriots row (camera `(31.884, 0, -53.45)`), `ui.up` eight times to
`(31.884, 0, -29.49)`, `ui.back` → camera back at `(31.6, 0, -53.3)` and focus back on
`galaxy:system/491/fleet/1304`.
**THE DRAWN SQUARE IS A MOD OVERLAY, NOT A BORROWED RENDERER.** The mark is
`ES2Access/UI/InspectMarker.cs`: a `GameObject` named `ES2Access inspect marker` whose `OnGUI`
projects the cell each frame. **Its evidence is OPTICAL and nothing else** — `crop-shot.ps1` on the
screen centre, because the mode's own camera centres each cell there (1280x800 → `-Rect
500,280,280,240` frames it at any size). State probes only support a crop, never replace it: host
count is
`Resources.FindObjectsOfTypeAll(typeof(GameObject))` filtered by that name (1 while armed, **0**
after `ui.back` and after a `/reload` taken with the mode armed — the leak test), and the world rect
is reflection on the component's `_lowX`/`_highX`/`_lowY`/`_highY`, which is how "the cell did not
move" is proved while the player is off the map. Crop hygiene since the cell-owned aim
(2026-08-17): the drawn tooltip is the CELL's, not the focused node's, and an `/eval`
`PointerFocus.Release()` lasts one frame because the pump re-aims — a clean marker crop is taken
by stepping to an EMPTY cell instead.
**The cell owns the game's drawn tooltip while the mode drives the map (2026-08-17)**: the aim rule
is system > fleets > probes/missiles/pins, and NOTHING on an empty cell (`win=hidden`, bare-sky
crop). Oracle pair: `DevProbe.TooltipPipe()` plus a `crop-shot.ps1` of `GuiTooltipWindow`'s rect.
`ModEntry.Navigator.ClearVisual()` from `/eval` stages a camera-driven focus-visual re-commit
without waiting for one — the cell's aim must survive it. Measured cells in `[Beginner] test`:
(-1,-6) at 11×11 = Heka + 2 fleets (system wins), (-12,-6) at 11×11 = Rigel + 1st Protectors Navy,
(-56,-28) at 11×11 = probe only, (0,22) = Qarius, (6,0)/(0,3)/(-56,-17) = empty. Leaving the mode
releases the aim and re-commits the standing control's focus visual (focus never moved, so nothing
else would ask).
**The mode's review buffer is the CELL's, and its oracle is `/input buffer.first` +
`buffer.lineDown`, never `/gui/graph?buffers=1`** — the graph dump renders the NODE's declared
buffer lines and cannot see an override. The buffer's ORDER is the sentence's, coordinate last.
Measured: at (0,0) `Dusay` / the three lanes / `0, 0`; at (0,-3) `1st Vanquishers Navy` / `0, -3`;
a fleet on a lane at (3,31) `2nd Saviors Navy, Small Logistics` / `Star lane from Qarius to Ita` /
`3, 31`; off the map `Galaxy View`; after Enter, the landed row's own lines. **Enter on a fleet cell lands on the fleet's own tree row** through
`GalaxyHudScreen.NodeFor`, opening the branch: from Dusay, `ui.down` to (0,-3) then Enter gives
focus `galaxy:system/522/fleet/1642` and Heka `expanded` — restore the fixture by focusing
`galaxy:system/522` and `ui.left` (collapsed at rest). **Enter on the cell the mode was ARMED on
is the regression test for the silent landing**: expect "Exited inspect mode" and the stop
announcing itself again. Every exit (Escape included) speaks the exit line THEN the landing.
Entry enforces the zoom ceiling: a camera closer than spoken "Zoom level 9" is pulled out to it
(the watcher announces), one already further out stays, silently. **Fixture pairs**: `B10 6805`
at (-5,-26) is a `SpecialNode` (Solar Nebula) — the only special in `[Beginner] test`, so it is
the fixture for "Enter on a special". Perceived systems: Heracles (-43,-30), Osulo (-31,-32),
Electra (-17,-21), Rigel (-16,-5), Qarius (-5,23), Ita (5,34), Heka (-1,-9), Dusay (0,0),
Primus (17,21), Leo (23,33), Byrtus (-25,-42), Libra (-11,11). Byrtus is the south-edge fixture:
1×1 at (-25,-41), Down must land "Byrtus, -25, -42" and a second Down "Map edge". Camera zoom for the size/zoom matrix is set directly:
`cam.ForceZoomingOnPosition(step, cam.TargetPositionCurrent)`, step 0 = full overview, 12 = closest
(`ZoomStepsCount` 13).
Fixture-blocked in the cell reading, for the same reasons the tree's own nodes are: an obliterator
missile (none drawn) and an ally coordination pin (no alliance) — both share the cell's enumeration,
visibility and wording with the tree, so the tree's route is the only place either can be sighted.
**Skip and travel (2026-08-19)**: `ui.coarseDecrease`/`ui.coarseIncrease` are the WEST/EAST skip
while the mode drives the map, `galaxy.inspectSkipNorth`/`…South` the other two, and
`galaxy.inspectFollowWest`/`…FollowEast` the travel keys. Measured on `[Beginner] test` at 1×1 from
Ita (5,34): north gives `5, 35`, then `Skipped 2 squares` + `Unexplored, 5, 38` (the fog bucket
changing is a stop), then `Skipped 49 squares` + `Unexplored, 5, 88` (the run to the north edge — the
landing is not counted, hence 49 and not 50), then `Map edge`; southward is the mirror
(`Skipped 50 squares` + `5, 37`). At 5×5 from (5,34) north gives `21 squares unexplored, 5, 39`, the
crop pair for "the square landed where the speech says". The travel keys' fixture is the SIX fleets,
all the player's own and all in transit — `1st Patriots Navy` Heracles→Osulo, `1st Defenders Navy`
Primus→Dusay, `1st Victors Navy` Dusay→Primus (those two share the cell `12, 15`, heading OPPOSITE
ways, which is the ambiguity fixture), `1st Protectors Navy` Dusay→Rigel (the discriminating case:
its destination is the lane's WESTmost end, so Alt+Right landing on Rigel and not on Dusay is what
proves the fleet beats the lane), `1st Conquerors Navy` and `1st Vanquishers Navy` both Dusay→Heka
across open space (the agreeing-fleets fixture: 7×7 at (0,-3) holds both). Route to any of them:
`galaxy.scanCategoryNext` to Fleets, `galaxy.scanNext` until the name is right, `galaxy.scanGoTo`.
Measured landings: the one-lane cell (-4,23) gives Qarius westward / Ita eastward; the 2- and 3-lane
cells (Qarius, Dusay at 7×7) are silent both ways; the half-dark lane at (-19,-5)
(`Star lane from Rigel going west`) travels west to Rigel and is silent eastward; the free mover at
(-1,-6) is silent westward (no lane) and lands on Heka eastward. A refusal's evidence is
`DevProbe.Camera()` unchanged as well as the silence, since a jump is an action and silence alone
cannot tell "refused" from "acted". **Fixture-blocked**: a FOREIGN fleet in transit (no contact on
this save) and a fleet bound for a system the player has not perceived — both would exercise the
no-leak rule, and neither can be staged here.
**Visual evidence: crop the screen centre, at every zoom.** The cell's centre is
`GalaxyViewCameraController.TargetPositionCurrent`, which the mode keeps at the screen centre, so a
crop is aimed by halving the window rather than by projecting corners. The window is **1280×800**
(`UnityEngine.Screen.width/height`, and the `/screenshot` frame agrees), centre **(640, 400)**; a
`-Rect 500,280,280,240` frames any cursor size. Measured 2026-08-17 with the mod-drawn overlay: the
square is pale cyan with a dark backing band and is unmistakable at **1×1 at zoom step 0** (the case
every borrowed renderer failed — it draws at its ~26 px floor, centred on the cell), at **1×1 zoomed
in**, at **11×11 at step 0** and at **3×3 at the default step 5**; at 11×11 CLOSE IN the box is still
wider than the viewport, because the mode's camera does not zoom out with the cursor. The overlay is
screen-space, so **any zoom is a fair test**. The focused node's own tooltip
is drawn over the middle of the map: it cannot HIDE the square (IMGUI draws over it, which is
the non-occlusion evidence) but it makes an ugly crop, and
`/eval ES2Access.UI.PointerFocus.Release()` after the last `/input` clears it, the next focus change
putting it back.

## Bookmarks in the cell

The rules are `docs/interaction.md`, **Bookmark keys**; the tree half and the standing fixture
bookmarks are `galaxy-map.md`, **Bookmarks**.

- **A point bookmark in the square** reads as one item of the sentence, last of the things standing
  in it: `galaxy.bookmarkGoTo1` (inspect off) then `galaxy.inspect` opens the cell on the point and
  answers **"bookmark 1, Unexplored, -68, 18"** — before, the same cell read "Unexplored, -68, 18".
  Its own line in the review buffer, in the same place.
- **Backspace is the CELL's way back while the mode is live** (`ui.secondary`), on a stack of its own
  that never touches the tree's trail. Pushed by leaps only — a bookmark jump, `galaxy.bookmarkHome`,
  `galaxy.scanGoTo`, and `galaxy.inspectFollowWest`/`East`; the plain and Shift arrows push nothing.
  Measured: Alt+Left from `-4, 23` (the one-lane cell) travelled to Qarius at `-5, 23`, and
  `ui.secondary` answered **"Star lane from Qarius to Ita, -4, 23"** with the cell back and
  `zoomStep` unchanged. Three silences to check while you are there — an empty stack (`speech: []`,
  cell unmoved), a travel key that REFUSED (a cell with two lanes: it pushes nothing, so the next
  Backspace is still silent), and a re-armed mode (push one, Escape, Ctrl+I again, Backspace: silent,
  because the stack dies with the mode instance). A PARKED jump pushes too: park, jump, and the
  landing brings focus back to the map with the entry waiting.
- **Every landing made under the live cell moves the CELL and nothing else** (owner rulings
  2026-08-31): the cell glides to the thing's square, `zoomStep` is the same before and after, and
  the tree cursor does not move at all — so the mode still ends on the row it was armed from. That
  covers the scanner's Alt+Home, a bookmark jump, Ctrl+L and a notification's show-location alike,
  because they are one landing (`MapLandings.Decide` with `inspectLive`). Two checks catch a
  regression: `zoomStep` either side, and the cursor probed before arming and again after Escape.
  The camera frame-trace is still the finer one — **one** decelerating run of samples ending at the
  square's centre, with nothing after it.
- **Enter's tiers, and the three kinds that became enterable 2026-08-31.** A probe, an obliterator
  missile and an ally's pin now end the mode on their own rows, at a tier between the fleet and the
  quest marker. **Fixture-blocked here**: `ScannedProbes().Count`, `SightedProjectiles.Count` and
  `SightedPins.Count` all read 0 on the turn-26 save, so the three are unit-tested and manual-listed
  rather than measured. To check one when a fixture has it: sweep the cell onto a square holding only
  that thing and press `ui.activate` — the mode should say "Exited inspect mode" and then that row.
  The tier order itself is `Core/UI/PlacedRows.cs` and is pinned by `PlacedRowsTests`.
- **A row kind that stands somewhere must be DECLARED.** `PlacedRowLintTests` extracts the key
  segments the galaxy tree builds and fails on any that is neither in the registry nor on
  `placed-rows.allow` (the segments an ancestor carries — planet, lane, launch, action, wreck,
  hangar, and the stop key itself). A new row kind therefore red-bars the build until somebody
  answers its four questions: arms, enterable and at which tier, leap-recordable, restore-candidate.
- **A vanished focused row is SILENT while the cell is live, and only then.** Stage it with two
  bookmark slots on one tile: arm on slot A's row, jump the cell away, then set slot B onto slot A's
  tile from the cell — the dedupe kills slot A's row under the cursor. Expect no neighbour
  announcement at all until Escape, which then says one landing (the nearest surviving place, which
  is slot B at distance zero). The control is the same kill with the mode OFF: the neighbour still
  announces, measured as "Libra, -11, 11, group, No owner, collapsed, 5 of 15".
- **Ctrl+I refuses a row that stands nowhere.** On a CONSTELLATION heading it is silent, arms nothing
  and moves nothing (`speech: []`, `live=False`, cursor unmoved) — the check is worth keeping because
  the failure mode was not a crash but a cell opening a stretch of sky away, at the constellation's
  centroid. On a system row, a planet, a lane or a bookmark row it still arms at that row's own
  place. To enumerate the refuse-list on any fixture, walk `render.Order` for the map stop and ask
  `GalaxyHudScreen.RowPlace` up each row's ancestry: the rows where no ancestor answers are the ones
  Ctrl+I refuses.
- **A PARKED bookmark jump reads no cell out loud, by design** (a jump announces once — the row it
  lands on). So it is the wrong route for measuring a cell's wording: the reading is still in the
  review buffer (`buffer.first` → "bookmark 1", then "Unexplored", "-68, 18"), and out loud it comes
  back with one arrow key or an ordinary Tab-away/Tab-back, which still speaks.
- **A bookmarked system in the square** ends ITS part of the sentence with the word, not the cell's:
  **"Dusay, bookmark 5, 1st Defenders Navy, Patrol, Star lane from Rigel to Dusay, …, 0, 0"**, and
  `buffer.first` reads the place line as **"Dusay, bookmark 5"**. Re-reading a cell after setting on
  it needs a step away and back (`ui.right` then `ui.left`) — the sentence is spoken on arrival.
- **Setting from the cell, three answers.** `galaxy.bookmarkSet<n>` with the cursor live on a square
  holding ONE system says "Bookmark n set on ⟨system⟩" and writes `slot<n> = <guid>,<x>,<y>`; on a
  square holding NO system it says "Bookmark n set on ⟨pair⟩" and writes `slot<n> = 0,<x>,<y>`,
  where `<x>,<y>` is `GalaxyCoordinates.Origin()` plus the cell's own pair — an arithmetic check the
  file can be read against (home `68.884, -22.45`, cell `0, 4` → `0,68.8843002,-18.4499054`). A
  square holding TWO OR MORE refuses: **"Shrink cursor so it contains only one system"**, nothing
  stored. A lane crossing the square does not make it a place: `-74, -31` at 9×9 names a starlane
  and still sets a POINT.
- **Staging a two-system square.** Cell centres are relative to where the cursor stands
  (`InspectGrid.Step` adds the size), not a fixed grid, so ANY integer pair is a reachable centre —
  drive one with `mode.JumpTo(x, y)` off the mode's private `_driving` field. On this fixture,
  **(-65, -31) at 11×11 holds Lors and Idrus** ("Lors, -66, -26, bookmark 3, Idrus, -64, -34, …,
  -65, -31") and **one `galaxy.inspectShrink` to 9×9 leaves only Idrus** — the refusal and the
  ordinary set on one square, two key presses apart. The same 11×11 cell is also the check that
  containment holds at a big cursor: a bookmarked system's suffix and a point bookmark's word are
  both found from the edge of the square, not only at its centre.
- **The skip stops at one.** Two adjacent bare squares in the same influence bubble differ only by
  the bookmark, which is the clean test: from `0, 6` southward, `galaxy.inspectSkipSouth` answers
  "Skipped 1 square" and then "bookmark 2, 0, 4". Pick the pair inside ONE bubble — an influence
  crossing is part of cell identity too and would stop the walk for its own reasons.

## Fixture-blocked

- The growing and shrinking influence wordings, the "Under … influence" suffix and the
  contested line — every one needs a mutation (**Influence on `[Beginner] test`**).
- Every multi-empire cell wording; only two circles are perceived (**Inspect-cell influence**).
- The scanner's Contested Influence category: 0 contested squares pristine
  (**The influence ground sweep**).
- In the cell reading: an obliterator missile and an ally coordination pin; a FOREIGN fleet in
  transit and a fleet bound for an unperceived system for the travel keys (**Inspect mode**).

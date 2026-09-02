# The galaxy scanner

Driving the scanner by action key, what each tier says, the categories and their oracles, and
Alt+Home — the one go-to that every category shares.

## Driving it

**The galaxy SCANNER** (read-only; `GalaxyScanner`). Drive it by action key —
`galaxy.scanCategoryNext|Prev`, `galaxy.scanSubcategoryNext|Prev`, `galaxy.scanNext|Prev`,
`galaxy.scanGoTo` — and read `/speech`. The FIRST press after a `/reload` says where the cursor
already is and moves nothing (the screen instance is new, so every reload re-arms it), and that
re-armed cursor starts at `Systems: all` again — a column parked in before the reload is gone.
**They are keys of the MAP stop.** With the cursor anywhere else on the galaxy page — the HUD
stop is where a fresh session sits — every scanner key answers `unconsumed` and nothing is
spoken, which reads exactly like the scanner being broken. Seat the cursor on the tree first
(`Navigator.FocusNode(ControlId.Structural("galaxy:constellation/<c>/system/<s>"))`), then press.

**The chords work under a SCAN LENS too** (2026-09-01), because the galaxy page keeps the map stop
in-mode — and the ring is filtered by the same band table the tree reads, so what the walk finds
and what the tree holds cannot disagree. Measured on `[Beginner] access test`, cycling
`galaxy.scanCategoryNext` with the cursor on the map stop:

| lens (camera step) | ring |
|---|---|
| Diplomacy (0–1) | nothing listed; every chord answers the none-found line for whichever category the cursor is parked in |
| Trade (2–5) | Systems, Colonizable Planets, Unexplored, then the configured custom slots |
| Economy (6–9) | the same (Contested Influence is band-allowed here and EMPTY on this fixture, so it is skipped as empty rather than as hidden) |
| System (10–12) | Systems, Unexplored, then the slots — Colonizable Planets drops out |

A custom slot inherits the filter with no rule of its own: the same slot measured `1 of 28` at
Trade and Economy and `1 of 20` at the System lens. **Curiosities are listed at no scan lens at
all** — the scan dot prefab does not wire the curiosity circle. And **`galaxy.scanGoTo` in-mode
SLIDES**: `DevProbe.Camera()` before and after shows the focus moving and `zoomStep` unchanged,
because under a lens the rung selects the lens and a landing must not change it — **so long as the
lens draws that KIND of thing**. A go-to whose target the lens hides (any fleet or probe, and a
system at the Diplomacy band) now LEAVES the lens first and then lands the ordinary way, forced band
included; the transcript's tell is one `"Galaxy"` line before the landing (2026-09-01,
`docs/interaction.md`, "A landing on something the lens does not draw"). The Diplomacy row above is
unchanged by 5b's empire list: the ring is still empty there, and the map stop simply has rows now.

## What each tier says

No count appears anywhere in a scope line — the instance line's "N of M" carries the size. **No press that lands on something is silent about the landing**: the
only difference between the tiers is how much of the scope is named in front of the instance line.
The ARMING press moved nothing but is still parked on something, so it says the whole scope AND that
thing, whichever tier's key armed it — measured on `[Beginner] test` from home,
`galaxy.scanNext` and `galaxy.scanCategoryNext` alike answer
`Systems: all, Dusay, 0, 0, here, 1 of 13`, and the NEXT `galaxy.scanNext` answers
`Heka, -1, -9, 9 south, 1 west, 2 of 13`, which is what proves the arming press held at index 1.
A SUBCATEGORY step (Shift) says the subcategory then the landing —
`friendly, Dusay, 0, 0, here, 1 of 2`. A CATEGORY step (Ctrl) says the whole scope then the landing —
`Systems: friendly, Dusay, 0, 0, here, 1 of 2`. An instance step says the instance line alone. A
scope standing empty under a parked cursor keeps its own sentence, `⟨scope⟩, none found`.
An instance line is `name[, extras], pair, offset components, N of M`.
A single-subcategory category's Shift press comes round to `all` and reads its
landing too, by the same code path; fixture-blocked, since all three such categories are empty.

## The oracle for the whole reading

The oracle for the whole reading is a table computed independently in `/eval`: walk
`galaxy.GameNodes`, skip anything `MapVisibility.Perceived` refuses (special nodes are IN since
taxonomy v2, so they are no longer skipped), subtract
`DepartmentOfTheInterior.HomeSystemNode.GalaxyPosition`, and print each axis rounded away from zero
— the spoken pair IS those two numbers and the spoken direction is their DIFFERENCE from the
reference point's own rounded pair, north/south component first, zero components dropped, both zero
collapsing to `here`. Measured on `[Beginner] test` from home: `Systems: all, Dusay,
0, 0, here, 1 of 13`, then Heka `-1, -9, 9 south, 1 west`, Libra `-11, 11, 11 north, 11 west`,
Rigel `-16, -5`, Qarius `-5, 23`, Primus `17, 21`, Electra `-17, -21`, Ita `5, 34`, Leo `23, 33`,
Osulo `-31, -32`, Byrtus `-25, -42`, Heracles `-43, -30`; `Fleets: all, 1st Vanquishers Navy, 0, -3,
3 south, 1 of 6`. **The distance SORT is unchanged** — the order is still nearest-first by true
distance; only the wording of the direction changed.
**The fixture's own scanner shape** (turn 21, taxonomy v2): **13** perceived places — 12 star
systems plus the `SpecialNode` B10 6805, which is now IN the category — of which **Dusay is the
colony and Heka the OUTPOST**; Rigel is neither, so friendly is `{Dusay, Heka}` (2) and neutral is
the other ten. The special node is in "all" and "special" ONLY and is deliberately not counted as
neutral, so neutral stayed at 10 while "all" went 12 → 13. Enemy is EMPTY, which is what proves the
Shift cycle skips. Fleets are six, all the player's, so neutral and enemy fleets are empty too.
Nothing here can produce the "⟨scope⟩, none found" line: it needs a scope to empty UNDER a parked
cursor, and cycling skips empties by design — fixture-blocked, covered by `ScannerCursorTests`.

> **Taxonomy dating (2026-08-26):** the affiliation buckets now follow the game's own ladder
> through `FleetPresence.SideOf` — a cold-war MAJOR's systems, fleets and probes file under
> ENEMY, not neutral (measured: Kais joined the enemy systems the moment the ruling landed).
> The transcripts and counts below were measured under the older war-only taxonomy on
> `[Beginner] test`, where no major was met, so its figures happen to be unaffected — but
> re-measure before using them as a baseline on any save with a met major.

## The subcategories of systems

**The seven SUBCATEGORIES of systems** cycle all → friendly → neutral → enemy →
homeworld → **minor factions** → special, empties skipped. Measured transcript, one Shift press each
(the press now reads its own landing): `friendly, Dusay, 0, 0, here, 1 of 2`;
`neutral, Libra, -11, 11, 11 north, 11 west, 1 of 10`; `homeworld, Dusay, 0, 0, here, 1 of 1`;
`minor factions, Osulo, -31, -32, 32 south, 31 west, 1 of 1`;
`special, B10 6805, -5, -26, 26 south, 5 west, 1 of 1`; then `all, Dusay, 0, 0, here, 1 of 13`.
**MINOR FACTIONS OVERLAPS NEUTRAL BY DESIGN** — it is an ownership filter laid over the affiliation
trio, not a fourth member of it, so Osulo is found by both and `neutral` stays at 10. The oracle is
an independent `/eval` walk: for every perceived node, any `ColonizedStarSystem` at its
`NodePosition` with `Visibility[me] >= 1` whose `Empire is MinorEmpire`. Measured here:
`perceived=13 specials=1 minorSystems=1` — **Osulo (Niris, Colony)** and nothing else. The galaxy
holds nine minor-faction homes but only Osulo is perceived at turn 21; an earlier note in this repo
claiming ~9 of them were visible was wrong. Coming back to systems from
fleets with the memory on special says the whole scope,
`Systems: special, B10 6805, -5, -26, 26 south, 5 west, 1 of 1`, and `galaxy.scanGoTo` from there
lands on the tree row `B10 6805, -5, -26, group, Solar Nebula, collapsed, 10 of 13` —
which is the check that the scanner and the tree agree about what is on the map.
**HOMEWORLD is fixture-blocked past the player's own.** The oracle is the `EmpirePosition` table:
`me.GetAgency<DepartmentOfIntelligence>().GetEmpirePosition(other).Known` for each `MajorEmpire`.
Measured here: Neurrone `Known=True` (own, Dusay), Leaper/Baten, St Chaoiver/Jundur and
Doria/Lonica all `Known=False` — and each stored position IS that empire's unseen home, which is
why the mod checks `Known` as well as the position (ES2 facts). Minor factions have home systems
too (Niris/Osulo, Amoeba/Sabel, Epistis/Dyl, Yuusho/Olvaldi… nine of them) and are deliberately
NOT in this scope.

## Taxonomy v3 — the thirteen categories

Ctrl+PageDown walks **Systems, Colonizable Planets, Unexplored, Anomalies, Curiosities, Luxury
Resources, Strategic Resources, Contested Influence, Fleets, Probes, Ally pins, Obliterator
missiles, Quest markers** — that ORDER is the one to check a press against; where a paragraph
elsewhere calls fleets "the second" or probes "the third" it is describing the wording, not the
position. Everything below was heard on `[Beginner] test` (turn 21, reference = the map stop's own
place); the measured rows and counts are quoted inline. Drive it with the same action keys; count
the Ctrl presses from Systems. Contested Influence is SKIPPED on this fixture (169 ground tiles
swept, 0 contested), so the Ctrl ring runs Systems, Colonizable, Unexplored, Anomalies, Curiosities,
Luxury, Strategic, Fleets, Probes and back. (Constellations were removed 2026-08-20 — owner: not a
discrete point — and Contested Influence added 2026-08-21.)

**Four of them build their subcategories from WHAT WAS FOUND** — Anomalies, Curiosities, Luxury
Resources and Strategic Resources get one column per anomaly definition, curiosity or resource,
sorted by the LOCALIZED name and sitting BEHIND the columns the category writes down for itself.
Three of the four write down only "all"; Curiosities writes down three (below). Colonizable Planets
writes down two and has no "all" at all.

Per category, what to check:

- **Colonizable Planets** (1 Ctrl press). Two subcategories and NO "all": `unoccupied` first.
  MEASURED on `[Beginner] test`: `Colonizable Planets: unoccupied, Libra II, Tiny Boreal, Binary
  Moons, Ruins, max population 6, Food 6, Dust 3, Science 5, -11, 11, here, 1 of 7`, and the
  `occupied` half is Osulo I, which adds its deposits (`Hyperium`, `Titanium`) after the size and
  type. So: exactly ONE comma between the planet's name and its size, size BEFORE type, resource
  names without the deposit suffix ("Titanium", not "Titanium-70"), and **the five outputs carry
  the mod's own SHORT names** — `Food`, `Industry`, `Dust`, `Science`, `Influence`
  (`GalaxyScanner.PotentialNames` → `ModStrings.Icon*`, spoken through
  `ModStrings.GalaxyScannerOutput`, the template `"{0} {1}"`). **An output the world does not make
  is absent, not zero**: `ScannerOutputs.Says` drops anything whose floor is 0, which is why Libra
  II has no Industry and no Influence line. A sparse world drops the absent parts entirely (Rigel I
  has no resources, anomalies or curiosities and says none). Oracle for membership: an `/eval` walk
  of the perceived systems asking `!p.IsColonized && p.IsColonizable(me)` per planet - measured 7,
  equal to the `unoccupied` count; the `occupied` half (a foreign or minor colony this empire's tech
  could settle) is Osulo I alone, and the same walk's `IsColonized && other empire && able` count
  is 1.

- **Unexplored** (2 presses). "all"-only, and its things are EDGES rather than places: every drawn
  lane or wormhole whose far end the player has not perceived, named from the end they CAN see.
  `Unexplored: all, Star lane, ⟨system⟩ ⟨direction⟩ to an unexplored system,
  ⟨the system's pair⟩, ⟨offset⟩, 1 of ⟨m⟩` (wording ruling 2026-09-02; no lane number is spoken any
  more, here or anywhere). Check the DIRECTION against the tree: focus the same system, walk its
  lane rows, and the compass word on the matching row must be the same one (both come from
  `LanesOf` and the same eight-word set).
  Oracle for the count: for every perceived node, its drawn links whose far end is not perceived,
  summed. **Each lane must appear once** — a duplicate would mean both ends were perceived, which
  contradicts the gate. A wormhole reads "Wormhole, ⟨system⟩ ⟨direction⟩ to …"; fixture-blocked
  unless the empire has wormhole technology.

- **Anomalies / Curiosities / Luxury Resources / Strategic Resources** (3-6 presses). In `all` the
  row is `⟨kind⟩ on ⟨planet⟩`; Shift steps into one KIND at a time, alphabetically, and there the
  scope line is the kind's own name and the row is the planet alone ("Acid Rain, Primus I, 17, 21,
  42 north, 34 east, 1 of 1"). MEASURED on `[Beginner] test`: anomalies 12 in "all" over 10 kinds
  (Acid Rain 1, Binary Moons 2, Hollow Planet 1, Huygens Rings 1, Mineral Rich 1, Multiple Moons 2,
  Polar Tempests 1, Single Moon 1, Strong Magnetic Field 1, The Platform of Ys 1); curiosities 16
  over 5 (Atmospheric 1, Life Form 6, Ruins 4, Signal 2, Subterranean 3); luxuries 10 over 2
  (Dustciduous Trees 6, Transvine 4); strategics 5 over 3 (Antimatter 1, Hyperium 2, Titanium 2) -
  every per-kind total sums to its "all", and every list is alphabetical in the localized names.
  The kinds are the game's own words. One row per (KIND, planet), owner's wording: two anomalies of
  the SAME kind on one world are one row, two of different kinds are two.
  Curiosities appear on systems that are NOT surveyed (the gate is `Curiosity.CanBeSeen`), and the
  planet is then named with the game's "unknown" word - FIXTURE-BLOCKED here, since all 13
  perceived systems are surveyed.
  **Curiosities cycle differently from the other three**: `Shift+PageDown` steps "all" (16) →
  **Explorable** (6) → **Insufficient Expedition Power** (10) → then one column per kind
  (Atmospheric 1, Life Form 6, Ruins 4, Signal 2, …). The two named columns are
  `Curiosity.CanBeSearched(empire, null, failures)` and the `EmpireExpeditionPowerTooLow` failure it
  records (`ScannerKeys.cs:74`); on `[Beginner] test` at turn 21 they partition the whole category,
  so a curiosity in NEITHER (one already being expedited, or quest-locked) is fixture-blocked. The
  kind columns count fewer than "all" because a kind is counted once per PLANET.

- **Contested Influence** (7 presses; EMPTY on this fixture). Its one subcategory is "all" — every
  result is the player's own ground being taken, so an affiliation scope would have exactly one
  answer — and its rows are squares ("Near Dusay, −7, −1, …") rather than places. It is the ONE
  category whose Alt+Home turns the inspect cursor ON (owner decision 2026-08-21,
  `GalaxyInspect.ArmAt`): a square has no node, no row and nothing to select, so leaving the cursor
  alone could only move the camera and say nothing. Arming announces exactly what Ctrl+I announces
  and opens the cell ON the square, so the arrival is heard once — "Inspect mode, Cursor 1 by 1",
  the constellation crossing, the influence lines, the pair. With the cursor already up it is an
  ordinary jump. No other category force-arms anything.

- **The memory is by NAME, for the column AND the row.** Both the column a category was left in and
  the row the cursor stood on are remembered by IDENTITY (name, key) rather than by index
  (`ScannerCursor.Reseat`, `ScannerTable`): a kind appearing ahead of the remembered one must not
  move the cursor to it. Park on a kind, cycle away to another category and back: the same
  KIND must come back, not the same column index. The offline proof is `ScannerKindsTests`; the
  live proof is doing it on a fixture where a kind sorts in the middle - DONE 2026-08-22: parked on
  "Mineral Rich" (5th of 10 anomaly kinds), cycled three categories on and three back, and the
  scope line came back "Anomalies: Mineral Rich" (Luxury likewise came back on "Transvine").

- **Alt+Home works in every category** — the one recipe is **Alt+Home — going to what the scanner
  found**, below.

- **The cost.** `POST /eval` `ES2Access.UI.ScannerCost.Line()` after a press answers
  `scanner snapshot ⟨ms⟩ ms, ⟨n⟩ colonizability checks, press ⟨n⟩`. Take it (a) on the first press
  of a session and (b) while holding Alt+PageDown. Anything at or over 30 ms also logs itself, so
  `GET /log?grep=scanner snapshot` is the second reading. MEASURED 2026-08-22: **32 ms on the
  session's first press** (the one line in the log) and **5-8 ms** on every press after, including a
  25-press burst; **30 colonizability checks**, every press. The shape to check is the SUM: one
  check per planet TYPE seen (19 here) plus one per unsettled world of a settleable type (11) -
  against 33 declared planets. A count that tracks the PLANET count means the memo is not working.

## Probes

**PROBES are the third category** (2026-08-16), cycled to by Ctrl after fleets. They are the
TRAVELLING probes only — the same `_drifting` list the tree's probe rows and the inspect cell read,
so all three agree. Detection probes (no mote of their own; they surface on system labels) and
mining probes (planet-anchored) are deliberately absent. The instance line reuses the tree row's
words for what a probe is called and whose it is, plus the owner-gated countdown, and leaves the
"N turns out from ⟨star⟩" bearing to the tree row: `Probes: all, Probe, Neurrone, 4 Turn,
-55, -30, 30 south, 55 west, 1 of 1`. `galaxy.scanGoTo` lands on the probe's own top-level row, MEASURED as
`galaxy:probe/1621` (the three open-space kinds sit at the top of the stop, so no branch is opened
on the way in); there is no select-the-thing fallback, because the game lets nobody
click a probe.
**The probe list is NO LONGER camera-dependent** (fixed 2026-08-16): `_drifting` is built from
`DepartmentOfDefense.Probes` across every empire under `MapVisibility.Sighted` (the game's own
`Visibility >= 3`), and the drawn label is attached only when there is one. The oracle for that is
NOT the zoom step — the culling group is a frustum test and the fixture's one probe survived steps
5, 12 and 14 — but hiding the motes by hand: `l.CulledIn = false; l.Hide()` over every `ProbeLabel`
in `ProbeLabelsWindow.LabelsContainer` gives `visible=0`, and the scanner still answers
`Probes: all, Probe, Neurrone, 4 Turn, -55, -30, …, 1 of 1`, identical to the drawn reading (which
also proves the label-free name and countdown are word-for-word the label's). The TREE row under
the same hand-cull reads `Probe, -55, -30, Neurrone, west of Heracles, 2 turns out, 4 Turn, 3 of 3`
— identical to the drawn reading (the label-culled row's buffer simply lacks the dossier, which is
assembled onto the label and there is no label). Restore with
`l.CulledIn = true; l.ShowOrHideIfVisibleByEmpire(Gui.PlayerEmpire)` — the window's own refresh only
runs when the camera POSITION moves, so it does not put them back by itself.
Foreign-probe subcategories are fixture-blocked (the sighted probe is the player's own; the save
holds two probes, one of them not sighted); the affiliation test is the same
`Scope(owner, empire, foreign)` the fleets use.
**The fixture's one sighted probe, in full** (`[Beginner] test` turn 21): empire 0's, GUID **1621**,
`GalaxyPosition(13.59, -52.30)`, nearest declared system **Heracles/488** at ~12 units; empire 3's is
not sighted. So the save covers "a probe with a star to measure from" and nothing else — a probe with
no star near it, and any foreign probe, are both unsighted here. **Ctrl to "Probes: all" then
`galaxy.scanGoTo` (Alt+Home) is the cheapest route to it**, cheaper than opening the system branch
the probe's bearing sentence names.

## Quest markers, ally pins and obliterator missiles

**The three "what is there" categories** — quest markers, ally pins, obliterator missiles — sit
after probes in the Ctrl ring, each with a single subcategory. A Shift press on one comes round to
`all` and says it again. All three are EMPTY in every fixture, and the empty-skip proves itself:
Ctrl from probes lands back on `Systems`, and Ctrl backwards from systems lands on `Probes`
(measured). The oracles for "is it really empty":
quest markers — walk `QuestJournal.Read(QuestState.InProgress)` (bind as `System.Collections.IList`)
and `quest.GetMarkers(quest.GetCurrentStep())`; `[Beginner] test` turn 21 has **32** quests in
progress and **zero** markers on any of them, so nothing is drawn. **Only markers standing AT A
SYSTEM are listed (2026-08-17, owner's ruling)**: a free-floating marker — one planted on a fleet in
mid-lane — is dropped in `GalaxyHudScreen.ScannedMarkers` because the go-to would have nowhere to
land, so the enumeration oracle above must be filtered by `MarkerSystem(marker) != null` before it is
compared with the category's count, and the go-to on a listed marker lands on
`galaxy:system/<guid>` (`NodeFor` → `SystemId`). Fixture-blocked live: the enumeration answers
`markers=0` on this save either way. Pins and missiles —
`DepartmentOfDefense.ObliteratorProjectiles` and the coordination-request repository are empty, and
pins need a game with allies in it.
**Pins and missiles are label-free too** (2026-08-16): both are enumerated from the simulation under
the game's own knowledge gates and every word of their rows is recomposed from the entity, so
neither moves with the camera (ES2 facts). Nothing about them is read off a drawn widget any more —
including the pin's DISMISS, which is now the game's own two orders rather than a button press. All
fixture-blocked; the evidence is the recomposition checked line by line against
`ObliteratorProjectileLabel.Refresh` and `CoordinationRequestLabel.SetTooltips`/`OnDismissCb`, plus
the probes' own hand-cull proof that the pattern works.

**Quest markers are FIXTURE-BLOCKED in both saves** - `[Beginner] test` has 32 quests in progress and
`[Midgame] quests fleets` 40, and every one reports `GetMarkers(step).Count == 0`. Register synthetic
ones to see the whole family (they die with the next `POST /loadsave`):
`QuestMarker m = new QuestMarker(); m.GUID = new GameEntityGUID(987654321UL);
m.QuestInstanceID = quest.QuestInstanceID; m.StepName = step.Name; m.BoundTargetGUID = <target>.GUID;
m.EmpireIndexes = new int[] { Gui.PlayerEmpire.Index }; m.MarkerType = new Amplitude.StaticString("Default");
m.Load(); Services.GetService<IQuestManagementService>().Register(m);` — bind one to a perceived
`StarSystemNode` for the at-a-system case and one to a **Ship** for the open-space case (a Ship is not
one of the five kinds `QuestMarkers` maps to a node, and its `GalaxyPosition` resolves to the galaxy
origin, which reads as a pair well off home). MEASURED with the pinned quest on
`[Midgame] quests fleets`: the system's buffer gains
`Tracked quest here: Prologue: TO THE STARS!`; the marker's node is
`galaxy:constellation/446/system/535/marker/987654321`, last child ("10 of 10"), buffer =
the step's objective; the open-space one is `galaxy:marker/987654322` at "-69, 22" in the drifting
region; the scanner's Quest markers category lists both and its go-to lands on the MARKER (the
at-a-system one zooms, the open-space one slides); the inspect cell reads
`0, 0, Dusay, Tracked quest here: …, Star lane …`; Enter on a cell holding only the marker says
"Exited inspect mode" and lands on its node; Enter and Backslash ON the node are silent and move
nothing. The quest LOCATE (`Gui.GuiGameWindowService.ShowQuestLocation(quest, step)`) says
"⟨quest⟩, objective shown on the map" and lands on a marker node - `ShowQuestLocation` cycles
markers, so two runs land on different ones.

## The reference point moves with the cursor

**The reference point is a LADDER, most specific first**: the free inspection cursor's cell if
that mode is up, else the focused world stop's own place, else home. `galaxy.scanGoTo` feeds
back into it — a jump becomes the new place to look around from.

**Proving the reference point moves.** With the tree cursor on the HUD the scanner measures from
home; focus a system (`galaxy:system/505`) and the same list re-sorts round it. Since 2026-08-16 a
row with a thing of its OWN measures from that thing rather than from its parent system: standing on
`galaxy:system/488/probe/1621` (the probe at `-55, -30`), `Systems: friendly` answers
`Heka, -1, -9, 21 north, 54 east` and `Systems: all` then reads Osulo `2 south, 24 east`, Byrtus
`12 south, 30 east`, Electra `9 north, 38 east` — each the difference of the two spoken pairs.

## Alt+Home — going to what the scanner found

One key, one recipe: `galaxy.scanGoTo` (Alt+Home). Route to any instance first —
`galaxy.scanCategoryNext/Prev` to the category, `galaxy.scanNext` to the instance, and note that
**landing on the node the cursor already stands on is silent**, so step off it first (after a
type-ahead for the same thing that is exactly where the cursor is). Read the landing from `/speech`,
the camera from `DevProbe.Camera()` before and after, and the cursor from the `>` line of
`/gui/graph`.

**Three landings, by what was found.** In inspect mode the cell moves to the ROUNDED pair and
reads out ("0, -3, 1st Vanquishers Navy") with `DevProbe.Camera()` focus at
`origin + (x, y)` exactly. Outside it, a system lands the tree cursor on `galaxy:system/<guid>` with
its ordinary announcement, and a lane fleet opens its host branch and lands on
`galaxy:system/535/fleet/1622`. A FREE-MOVEMENT fleet lands the same way, on its DESTINATION's row:
`galaxy.scanGoTo` on `1st Conquerors Navy` opens **Heka's** branch and lands on
`galaxy:system/522/fleet/1570`, heard as "1st Conquerors Navy, -1, -6, free moving to Heka,
1 ships, Moving to Heka, 0 movement points, Arrives in 2 turns, 8 of 9" (no role word —
it is an automated fleet). There is only one row to pick — the source branch no longer holds one.
**Fixture-blocked**: the `SelectFleet` fallback (camera + fleet panel + the scanner's line spoken
again) is reachable only by a fleet PARKED at a system the map does not name or flying a lane the
map does not draw — a free mover always has a row, top-level if its destination is unperceived
(`AddAdrift`). No fixture produces either.

**It lands AND zooms, on every category — OUT OF THE INSPECT CELL** (owner decision 2026-08-22;
under a live cell nothing zooms, 2026-08-31, below). For anything standing at a
NODE — a planet and a lane included — the landing is the page's own locate landing: focus the node,
and ask the camera rule for the place (so they land on their own node under their system and zoom to
the system). A thing that stands at a bare POINT (a fleet under way, a probe, a pin, a missile) has
no node to zoom into and gets the inspect cursor's own `CenterOn` slide instead. MEASURED on `[Beginner] test`: a SYSTEM lands on
`galaxy:constellation/446/system/491` with the camera zoomed (`zoomStep` 9 → 12, focus on Osulo); a
PLANET on `…/system/505/planet/0` with both ancestors opened and the camera zoomed; a LANE on
`…/system/505/lane/650` (9 → 12); a mid-lane FLEET keeps its own node (`…/system/522/fleet/1570`)
while the camera slides to it; a PROBE lands on `galaxy:probe/1621` with the camera SLID (zoomStep
unchanged, focus (13.59, -52.30)). Contested Influence must still ARM the cursor — fixture-blocked,
the category is empty here.
**With the inspect cursor UP, NOTHING ZOOMS** (owner ruling 2026-08-31, reversing the "zooms anyway"
line measured here on 2026-08-22 — `docs/interaction.md`, **The galaxy map's keys and landings**): a
SYSTEM keeps the mode, moves the cell to its tile and leaves the picture at the scale the player
chose, exactly as a FLEET at a bare point always did, and the `>` line shows the tree cursor seated
silently on the system's own row. Measured 2026-08-31 on the turn-26 fixture: `galaxy.scanGoTo` onto
**Heracles** answered "Serpens constellation" / "Heracles, Star lane from Heracles to Osulo, -43,
-30", camera focus `[25.884, 0, -52.45]` = the square's centre, **`zoomStep` 8 before and after**,
cursor `galaxy:constellation/446/system/488`. Then `ui.back` landed on Heracles's own row with the
camera framed on it and still at 8, and one `ui.right` INTO the system answered "Zoom level 13 of 15,
System Overview" — the ordinary camera machinery, untouched. A PLANET still **says "Exited inspect
mode" first**, `GalaxyInspect.Live` reads false, and the landing is the ordinary zooming one
(measured: Rigel I) — the one landing that still zooms with the cursor up is the one that takes it
down.
**The settled-row proof.** Zoom out first (`GalaxyViewLevels.SetZoom(5, Vector3.zero)`), move the
cursor off the target (`ui.home`), then run the go-to and read `/speech`: the landing must be the
CLOSE-camera reading. Osulo I settled is
`Osulo I, group, Medium Mediterrane., Colonized, collapsed, 2 of 8` — the closer view adds rows and
the card's own words, so a landing composed before the camera arrived would say
`Osulo I, Colonized, 1 of 7` instead. To time it: fire the input in the background, then
`POST /wait` on `!GalaxyViewLevels.CameraSettling && GalaxyViewLevels.ZoomStep >= 12` (12-14 frames,
~0.9 s from the far camera) and dump `/gui/graph` immediately and again 300 ms later - the card's
words are there at once, its buttons ("group") 300 ms later.

**Physically, end to end** (`POST /key?hold=250&gap=150`, desktop unlocked): `Ctrl+PageDown` to a
category, `Alt+PageDown` twice to step off the thing the cursor is already on, then `Alt+Home`. With
the instant camera the landing announced 394 ms after the key release ("Libra, -11, 11, group, No
owner, collapsed, 5 of 13").

## Custom categories

**Configuring a custom scanner category from `/eval`.** There is no editor
yet, so the three slots are written through the runtime API and read back on the next press —
nothing needs a reload, and a reload proves the file:

```
ES2Access.Core.UI.ScannerCustomCategory one = new ES2Access.Core.UI.ScannerCustomCategory("Watch list");
one.AddSelector(new ES2Access.Core.UI.ScannerSelector("systems", "neutral"));
one.AddSelector(new ES2Access.Core.UI.ScannerSelector("fleets", "friendly"));
one.AddKeyword("Dusay");
ES2Access.UI.Settings.ScannerCustomSettings.Set(0, one);   // slot index 0 = "custom category 1"
```

Wrap it in the `((System.Func<string>)(() => { … }))()` IIFE and return
`ModSettings.File.Get("scanner.custom.1")` to see the encoded line
(`Watch list|systems:neutral,fleets:friendly|Dusay`). The selector vocabulary is `ScannerKeys`;
a KIND selector takes the game's own definition name, which `[Beginner] test` supplies plenty of —
`anomalies:PlanetAnomaly27` (Multiple Moons), `strategic:Strategic2` (Hyperium),
`curiosities:explorable`. Clear with `ScannerCustomSettings.Clear(0..2)`, which removes the keys
from `settings.cfg` outright — do it at the end of a session, since a slot left configured adds a
category at the END of every later cycle.

MEASURED on that fixture (turn 21): with NO slot configured the first (arming) `galaxy.scanCategoryNext`
lands on **"Systems: all, Rigel, -16, -5, 3 south, 1 of 13"**; with slot 1 configured the cycle reaches
**"Watch list: all, …"** LAST, after Probes, and wraps from there to Systems. `galaxy.scanSubcategoryNext` inside it steps **Systems: neutral** (10) → **Fleets: friendly** (6) → **Dusay** (5) → **all** (21), the three
partitioning "all" exactly. A slot configured with a selector this galaxy cannot answer
(`luxury:NoSuchResource`) is SKIPPED by the category cycle in both directions and answers its own
quick key with "{name}: all, none found". An unconfigured slot's quick key says
"No custom category on ," / "on Shift+/" — the key named off the live binding.
`ES2Access.UI.ScannerCost.Line()` reads **4–7 ms a press** whether or not a slot is configured,
which is the measurement behind composing every colonizable world's description up front.

## Fixture-blocked

- The `⟨scope⟩, none found` line: it needs a scope to empty UNDER a parked cursor
  (**Driving it**, **The oracle**); covered by `ScannerCursorTests`.
- A single-subcategory category's Shift press (**What each tier says**) — all three such
  categories are empty here.
- HOMEWORLD past the player's own (**The subcategories of systems**).
- A wormhole row in Unexplored; a curiosity on an unsurveyed system; Contested Influence's
  cursor arming (**Taxonomy v3**).
- Foreign probes, and a probe with no star near it (**Probes**).
- Quest markers, ally pins and obliterator missiles in every save — the synthetic-marker
  recipe is the only sighting (**Quest markers, ally pins and obliterator missiles**).
- `galaxy.scanGoTo`'s `SelectFleet` fallback (**Alt+Home**).

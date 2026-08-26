# The galaxy map's fleets — tree rows, orders, targeting and the fleet panel

Everything fleet-shaped on the galaxy page: how fleets appear in the tree, what no fixture
perceives of foreign ones, ordering and re-routing, the targeting cursors, the route-loss
watcher, the selection chords and drag, and the selected-fleet panel. The rest of the page —
tree shape, fog and labels, camera, locates, dossiers, keys — is in `galaxy-map.md`; the
game-side facts are in `docs/fleets.md`.

## Fleets in the tree: en route, free-moving, automated

**Destination-only, for lane fleets and free movers alike.** A fleet in transit is declared under
the endpoint it is flying TO and nowhere else — a lane fleet saying which of THAT system's lanes it
is on, a free mover reading "free moving to ⟨system⟩". The independent oracle is the leg itself:
`IPositioningService.GetGameNode(fleet.Position.Movement.Goal)`. A fleet on a lane that is NOT under
way keeps a row under each end, which is what it always had — the rule is about transit. The LANE
node's own "what is flying this lane" phrase is not reconciled with it: a lane is a leaf, so its
count is a statement about the lane rather than about children it does not have.

On `[Beginner] test` the four lane fleets read Patriots Heracles→Osulo, Defenders Primus→Dusay,
Victors Dusay→Primus, Protectors Dusay→Rigel, each with exactly ONE row —
`galaxy:system/491/fleet/1304`, `.../535/fleet/1447`, `.../543/fleet/1593`, `.../505/fleet/1622` —
so `POST /type defe|victo|protec|patrio` each answer `results:1`. The two free movers both fly Dusay
→ Heka and each gets one row under Heka: "1st Conquerors Navy, -1, -6, free moving to Heka, 1 ships,
Moving to Heka, 0 movement points, Arrives in 2 turns, 8 of 9", the Vanquishers the same at `9 of 9`;
`vanq`/`conq` each answer `results:1`, landing on the Heka row.
**The counts have DRIFTED between measurements** — one pass recorded Dusay "1 fleet under way
nearby" with Primus 1, Rigel 1, Osulo 1, Heracles none, Heka 2; a later one recorded Dusay "3 fleets
under way nearby" (its three lane fleets) and Heka "2". Both are kept because neither has been
re-run against the current turn: **re-measure the per-system counts on the next live session** before
treating either as the baseline.

No fixture here has a fleet IN ORBIT (all six read `Position.IsInOrbit == false`), so the
parked-fleet rows (`AddFleets`) cannot be exercised live in this save; and no fixture produces a free
mover with an unperceived destination, so `galaxy.fleet-free-moving-to-unexplored` and the top-level
`AddAdrift` row it belongs to have never been heard. The same goes for a lane fleet whose destination
the map has not named.
**The fixture's two free movers are AUTOMATED delivery fleets** (`Fleet.IsAutomated` true; the other
four are false), which matters twice: the game counts an automated fleet's
ships whatever the visibility (`GarrisonsLabelButton` :210), so they are the wrong fleets for
testing the ship-count gate — use `1st Protectors Navy` — and `GalaxyFleetCursorTarget`
refuses both selection (:17-24) and highlight (:26-33) for one. **The mod says so**: `FleetNode`
declares an unselectable fleet `ControlTypes.Text` with no `OnActivate`, so the row carries no role
word and Enter is a no-op. The regression check is one line
of a branch dump — the Heka rows must read "…, -1, -6, free moving to Heka, …" with no "button",
while Dusay's three lane fleets (not automated) must still read "…, 12, 15, button, on starlane 1,
northeast, …". A build where all five say "button" has lost `FleetPresence.Selectable`; one where
all five drop it has the predicate inverted.

## Foreign fleets: what no fixture perceives

**`[Beginner] test` perceives NO foreign fleet at all.** Measured 2026-08-16 by walking
`Gui.Game.Empires` and each empire's `DepartmentOfDefense.Fleets` (as `System.Collections.IList`),
printing `(int)f.Visibility[me]`: Neurrone 6 fleets all at 3, and every one of the other 25 fleets
(Leaper 8, St Chaoiver 2, Doria 4, nine minor empires 1 each, LesserEmpire 4, PirateEmpire 0) at 0.
So **every foreign-fleet behaviour on the galaxy map is fixture-blocked here** — the ship-count
gate, foreign selection, and the foreign-route gate alike. `me.SeesEnemyPathfinding` is also false.
Two probes stand in for the missing fixture, and both are real evidence rather than reasoning:
- **Ship count below Visible.** Write the private `EntityVisibility.layers` array by reflection
  (`typeof(EntityVisibility).GetField("layers", Instance|NonPublic)`, index by `empire.Index`) to
  drop a NON-automated fleet to `Layer.Marked`, dump, then write it back to `Visible`. Measured on
  `1st Protectors Navy` under Dusay: "… on starlane 2, west, **1 ships**, Moving to an unexplored
  system, 6 movement points, …" became "… on starlane 2, west, Moving to an unexplored system,
  6 movement points, …" — the part omitted, no placeholder, its two Visible neighbours unchanged.
  The write survives a graph rebuild; the game's own visibility pass puts it back at the next turn.
- **A foreign fleet's route.** `FleetRoute.Committed(f)` and `.CommittedLines(f)` answer null for
  any of Leaper's fleets (they need no visibility to be reachable from `/eval`), while
  `FleetRoute.Of(f, f.Path)` — the same walk with the gate stepped around — still answers
  "arrivesIn=9 places=4 last=Fajis". That pair is the failing half: without the gate the mod read
  an AI's whole plan out of the model, through systems the player has never seen.

## Ordering a fleet around

**Ordering a fleet around** (state-changing — only against a save you can reload, and only after
every read-only check is done). It is two halves: **Enter** on the fleet's own node selects it, then
**`/input ui.contextual`** (backslash) on the DESTINATION — a system node, or a starlane child of one
(expand the system with `ui.right`) — sends it. What it answers, in the mod's own words
(`ModStrings.GalaxySendFleet`/`GalaxySendFleets`, `GalaxyHudScreen.SendAll`): with ONE fleet going,
*"Send fleet ⟨name⟩ here"*; with several, *"Send the ⟨n⟩ selected fleets here"* — so a multi-select
send is its own branch and must be tested as one. Where nothing could get there the REFUSALS are
spoken instead, in the game's own sentences. The one silent case is the one with nothing to explain:
every selected fleet is already parked at the destination, so no route was refused.
Post it through the mod's own key rather than from `/eval`, then probe the game:
`fleet.Position.IsInOrbit`, `fleet.Path.Destination` through `IPositioningService.GetGameNode`, and
`empire.GetAgency<DepartmentOfLabour>().EntityActions` (index it - never `foreach`) for the
`GoToFleetAction` whose `Initiator.GUID` is the fleet — the count stays at ONE and the `Id` changes,
which is how a supersede is told from a stack. The under-way `FleetLabel` only exists once a
move is in flight, so that is the moment to measure the pointer path (`AgeManager.Instance
.OverrolledTransform` + the tooltip window's rect). Restore with `POST /loadsave` and re-check
camera, fleets and quest. Note tutorial progress does NOT live in the save: selecting a fleet
advances the tutorial popup, and only re-minimizing puts the fixture back.
**What the turn-3 fixture cannot show is a RE-ROUTE.** Its one known system's three lanes all run
into the dark, so a fleet that leaves is instantly un-re-routable (`NextNodeUnknown`) — measured:
`PathToLink` and `PathTo` answer null for every destination except the lane it is already on. Every
lane also costs 8.3–13.3 against 5–6 movement points, so an ordered fleet always ends the turn
stranded mid-lane rather than discovering anything.

**The six zoom-in fleet actions name where they put the cursor and then put it there**
(2026-08-20): Colonize, Super Colonize, Destroy Planet, Expedition, Mining Probe and Reclaim
Mothership each append a "moves focus to the first …" phrase to their own name and, on activation,
expand the acting system and seat the cursor on the first matching action row
(`SeatAfterFleetAction`/`FollowActionSeat`; a positional row id must hold steady 20 frames before the
seat commits — the orbital card's buttons arrive over several frames, `docs/planets.md`). No match
= branch open, cursor unmoved, silent. **The seat and the navigator's pending landing are SUSPENDED,
not forgotten** (2026-08-20) while the galaxy page is away (the discovery cutscene POPS it — a
sibling view level) or the view is mid-flight: suspended frames spend no budget and prove nothing;
the landing knows its OWNER screen, so another surface's arrival, cursor and keys never touch it; the
player's own navigation ON the requesting screen's graph still cancels. The expected expedition
sequence is: press → the discovery video plays (if enabled) and reads its cards → "Galaxy" → the seat
lands on the first curiosity, once. **A targeting cursor arming ends a live-or-suspended inspect
mode** with the mode's own exit line spoken ahead of the instruction (`GalaxyInspect.Dismiss` from
`GlobalHud.AnnounceCursorMode` — all nine cursors — and from the six seat actions); entering inspect
over an armed mode stays allowed, its landing Enter leaves the found node focused for the mode's
confirm, and Escape unwinds innermost-first. A modal target picker ENDS the mode (page pop), never
suspends it.

**What neither fixture can show on the galaxy map.** A send to a SYSTEM (turn 3 knows one, and its
three lanes all run into the dark); minor factions and pirate lairs; a FOREIGN outpost — the only
state that fills `%OutpostColonizationTooltipDescription` — and `OutpostCancelIcon` (an outpost
being lost or decolonized); and every fleet-label variant beyond the plain one-ship own fleet:
merged labels, guarding, multi-ship/automated/privateer fleets, and any fleet of another empire
(only one empire is visible at turn 3).

**Forcing a send REFUSAL to hear its reason.** No fixture state refuses a move, so flip the game's
own cheapest gate for the duration of one handler call and restore it in the SAME `/eval`:
`GameNode.IsLocked = true` on the destination answers `SystemIsBeingFrozen` (pathfinder-side), and
the private `StarSystemNode.empirePlanningEncounterIndex = <empire index>` on the fleet's own orbit
answers `EncounterInPreparation` (action-side — this one proves the three-argument
`CanBeExecuted`). Invoke the node's real `OnContextual` delegate by reflection, read `/speech`,
restore, and re-load the pristine save if a positive control posted a real order.

## Targeting modes

**Free-aiming a probe by compass** (arm through the fleet-actions stop — that list walks with
Up/Down, `ui.right` answers `unconsumed`; **arming seats the cursor on the group's FIRST bearing,
north**, with the fleet's system branch and the "Launch towards" group both opened for it — fixed
2026-08-19, it used to leave the cursor wherever it stood): `ui.down` from north to another
bearing, `ui.activate`; oracle
`DepartmentOfDefense.Probes[i].Direction` against the unit vector (X=east, world-z=north).
Primus's lanes run NE/SW/NW, so N/E/SE/S/W are lane-free bearings. Anchor-migration and
at-star cases: set `Probe.GalaxyPosition` from `/eval`, restore by `/loadsave`.
**The mode needs a fleet IN ORBIT, so `[Beginner] test` cannot arm it** — all six of its fleets are
mid-lane at turn 21 (`Position.IsInOrbit` false on every one, measured 2026-08-19), and the
direction group is declared only at the node the acting fleet orbits. The fixture is
`[Midgame] quests fleets`: `1st Patriots Navy` orbits Dusay (node 535) carrying 2 probes, and its
"Launch Probes" action is enabled. Selecting a fleet there raises the 6-page `Tutorial_Fleets`
popup — minimize it before injecting anything else. **Cancelling a probe launch gives the panel
back to the fleet that armed it** (fixed 2026-08-26 — mechanism, oracles and what was proven in
`docs/fleets.md` § "The targeting-cancel fleet swap"). Proving the swap fix needs TWO of the
player's fleets at one docking slot; the GUID oracle is `FleetsScreen.SelectedGarrisons`, never
the spoken panel name alone, and the patch can be A/B'd from `/eval` via
`ES2Access.Screens.ProbeCancelSelection.Remove()`/`.Install()`.
Re-read the actions stop AFTER an Enter on a fleet's map row, never before.

**Confirming a targeting mode on a LANE** (the probe-down-the-dark-lane repro): arm the
cursor from `/eval` with `Fleets[0]`, focus the lane node (`galaxy:system/543/lane/662` on
Primus), `POST /input ui.activate`. The direction oracle is
`DepartmentOfDefense.Probes[i].Direction` against the far node's bearing from the fleet.
Do NOT oracle `GameOverlayTooltipPanel.Label.Text` after a reflected `OnCursorEnter` — it
keeps whatever an earlier cursor broadcast (the panel is a subscriber; ES2 facts); the
buffer dump is the oracle.

**The fixture's one text-producing targeting mode** is
`ChangeCursor(typeof(TakeSystemCursor), new AcademyDiplomacyGiveSystemAction())` (disarm:
`ChangeCursor(typeof(GalaxyCursor))`) — aim at an owned colony and the armed-mode buffer
line reads "Must be a Academy Owned System". `TimeBubbleCursor` arms with any string and
the obliterator arms on any fleet, but both answer EMPTY on every fixture system (a valid
time-bubble target draws no panel; the obliterator refuses a non-Behemoth with an empty
info list).

**Arming the two pointer-aimed targeting modes from the REPL** (both reversible; the pin
posts a real order, so reload after): `ICursorService.ChangeCursor(typeof(
ProbeLaunchingCursor), fleet)` and `ChangeCursor(typeof(CoordinationRequestCursor),
CoordinationRequest.CoordinationRequestType.Attack)` — the latter arms with NO ally, and
`CursorTargeting.ConfirmAt(node)` drives both. Fixture notes: the Patriots fleet carries
exactly 2 probes; the Expedition button greys out at 0 probes.

**Reaching a targeting mode without its prerequisites.** `ICursorService.ChangeCursor(typeof
(TimeBubbleCursor), "TimeBubbleSlowingTime")`, or `(typeof(ProbeLaunchingCursor), fleet)`, or
`(typeof(TakeSystemCursor), new AcademyDiplomacyGiveSystemAction())` (public parameterless ctor;
its `OnComplete` runs only on a successful left click) — the same call the game's own buttons
make, so the mode comes up with the banner and the confirm live even where the empire could
never open it. What a confirm is verified by is the **mode ENDING** — cursor back to
`GalaxyCursor`, banner gone — never the order's effect. CAUTION: in the current "unlocked" save
`CanPlaceTimeBubble(Xiu)` answers TRUE, so Enter on a system in TimeBubble mode WOULD post the
order — the safe refused-target pair is `TakeSystemCursor` on one of your own colonies
(`TakeSystemNotAcademyOwned`). The hacking pair is NOT enterable here (a real program name
bounces the cursor back same-frame). Proving "the node's own command yielded" needs the camera
parked one step past `DefaultZoomStep` first, or the ordinary Backslash is a silent no-op and
absence proves nothing. `POST /loadsave "unlocked"` afterwards.

## The route-loss watcher

**Reaching the route-loss watcher's endings.** No fixture produces a real interception or
invalidation, and both are reversible only by `POST /loadsave`: order a fleet out, then from
`/eval` either `fleet.SetPath(null)` (expect "The route of ⟨fleet⟩ to ⟨dest⟩ was cancelled") or
`HasBeenIntercepted = true` first (expect "⟨fleet⟩ was intercepted at ⟨system⟩", and no
cancellation line). The negative pairs matter as much: a normal arrival and a route REPLACEMENT
(re-select the fleet — sending cleared the selection — and Backslash a new destination) must both
stay silent, checked with a `/speech?since=N` window.

## Selection chords and the drag

**Testing the selection chords and the drag.** `/input` cannot hold a modifier, so
`ui.selectToggle`/`ui.selectRange` reach the row's own click with NO physical Ctrl or Shift and the
game runs its plain (radio) branch: the injection proves the wiring, the announcement and the
fall-backs, never the modified semantics — for those, hold the key for real (the physical-modifier
recipe in `docs/dev-loop.md`). What IS
provable live: flip the panel's model from `/eval` and watch the row's live membership part
(`ShipsManagementPanel.DeselectShips()` plus `Dirty = true` makes a tile read "not selected" under a
standing cursor), then press the chord and read the state the row speaks back. The drag needs no
modifier and so is fully injectable: `DevProbe.Claims("Space")` reads true exactly where a
pick-up, a carry or a live search is, so it IS the claim-side proof of a drag source (measured:
false on a one-item construction queue line, true once the line reads "draggable");
`ModEntry.Carry.IsCarrying`/`.Held.Name`/`.Held.Kind` is the
state probe, a compatible row's readout grows "drop target" while something is held, `ui.carry`
answers "Dragging …" on a source and SILENCE everywhere else — including on a drop target that is not
also a source — with the drag kept, `ui.carry` back on the source it came from and `ui.back` both
answer "Cancelled drag" (`claimsBack` reads true only until it does), and **`ui.activate` is the
drop**: on a control that takes the cargo it announces the drop and the control's own click does NOT
run, on any other control the click runs and the drag survives it (inject Enter on a harmless toggle
to prove that half). Silence is proved with a `/speech?since=N` window, not with the `/input` reply.

## The selected-fleet panel

**Working the selected-fleet panel.** It is a contributor, so there is no `screen=` key for it and
no screen change to wait for: its three stops simply join the galaxy page's, between the systems
stop and `hud:quest`, and `/speech` says "Fleet panel open for …". Open it the way the player does —
**Enter on a fleet node in the tree**, under the system it is parked at or the lane it is flying —
and check where the cursor actually is before every injected key
(`DevProbe.Screen()`): a blind `ui.next`/`ui.activate` run once landed on the HUD's "Close tutorial"
and raised its confirmation (cancel it with `ui.down` then `ui.activate`; Confirm is irreversible for
the fixture). Close the panel with `Gui.GuiService.GetWindow<FleetsScreen>().HandleInput
(InputAction.Exit)` — the same route the key takes, since Escape itself cannot be injected. The
turn-3 fixture's permitted destructive pair, LAST in a run: "Select all" then Merge (probe
`Gui.PlayerEmpire.GetAgency<DepartmentOfDefense>().Fleets.Count`, 2 → 1), then Garrison (1 → 0, and
the management row swaps Merge for the hangar-only Create). Neither is reversible without
`POST /loadsave`. **The hero band has no fixture** (no hero at turn 3) but can be MEASURED:
`w.FleetHeroPanel.Show()` draws it against a null hero and `.Hide()` puts it back — that is how the
assign/unassign button's naming was found to come from `AssignIcon.Visible` rather than from which of
the two shared-tooltip transforms is up. **Also unverified in the fixture**: 26 of the 31 fleet
actions and every TOGGLE action, Retrofit/Repair/Scrap/Sell/Specialize enabled, the other-empire
banners, a list long enough to scroll, the range-outcome sentence with two or more ships, and the
DROP itself — the cursor draws exactly one fleet line and each fleet owns exactly one ship, so every
reachable transfer would destroy a fleet.

## Fixture-blocked

- Every foreign-fleet behaviour: the ship-count gate, foreign selection and the
  foreign-route gate (**Foreign fleets**); the two probes there stand in.
- A fleet IN ORBIT, and a free mover with an unperceived destination (**Fleets in the tree**).
- A send to a SYSTEM on the turn-3 fixture, a RE-ROUTE, minor factions and pirate lairs, a
  FOREIGN outpost, and every fleet-label variant beyond the plain one-ship own fleet
  (**Ordering a fleet around**).
- The probe compass mode on `[Beginner] test` — it needs a fleet in orbit, so the fixture is
  `[Midgame] quests fleets` (**Targeting modes**).
- 26 of the 31 fleet actions, every TOGGLE action, the other-empire banners, a scrolling
  list, the two-ship range sentence and the DROP (**The selected-fleet panel**).

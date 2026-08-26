# The galaxy map — tree, camera, labels and dossiers

The galaxy page's own surfaces. Everything fleet-shaped — tree rows, orders, targeting modes,
the route-loss watcher, the selection chords and the fleet panel — is in
`test-recipes/fleets.md`. Inspect mode and influence are in
`test-recipes/inspect-and-influence.md`; the scanner is in `test-recipes/scanner.md`.

## Screen model: the tree's shape and the page's four panels

**The galaxy map stop is constellation-grouped** (2026-08-20): top level = one group per
EXPLORED constellation (gate: `Constellation.Exploration[player] > 0`, the label's own check,
stale aggregate included), drifting rows at their own positions, and the merged
"Unexplored constellation" group LAST. Key heads: `galaxy:constellation/<guid>` and
`galaxy:constellation/unexplored`; all system ids live under them (`SystemKey` is the one
composer). Groups default EXPANDED on first sight (`Seed`, once per group per galaxy).
Constellation nodes speak no coordinate; opening one never moves the camera; the
collapse-un-zoom rule exists at TWO levels (`ZoomOutOf`: collapsing a constellation whose
member holds `FocusedSystem` runs that system's own zoom-out). The system ordering below is
the order WITHIN a group.

**The galaxy view names its four PANELS** (2026-08-19) with `GraphBuilder.PushContext` levels, said
once on arrival and never repeated while walking inside: Galactic Map (`galaxy.map-panel` — renamed
from "Map" 2026-08-22, owner ruling: the panel Ctrl+G goes to says what it is), Quest
(`hud.quest-panel`), Notifications (`hud.notifications-panel`) and View Controls
(`hud.view-controls-panel`). Quest and Notifications ride the one shared `GlobalHud` contribution,
so those two words are said on every one of the thirteen screens that draw those panels; "View
Controls" is the galaxy's alone (gated on the zoom ladder no other page passes) and is the one name
that overrides a word the game DRAWS — "GALAXY VIEW" on `TopTitlePanel`, owner ruling 2026-08-19,
because the view's name says which page the player is on and the screen has already said that
(ES2 facts).

**The shared HUD's empire stop carries a row region per drawn band**, on every page in the game:
`hud:empire/{controls,key-resources,research,strategics}` (labelled Controls / Key Resources /
Research — reusing `galaxy.research` — / Strategic Resources) plus the seven faction bands
`hud:empire/{lifeforce,genes,singularities,golden-age,pirate-mark,honor,relics}` (Essence, Manage
Population, Singularities, Golden Age, Pirate Mark, Keii, Relics — the game's own words except
Singularities and Pirate Mark, which have no standalone title key in the corpus and ship as
`hud.singularities-panel` / `hud.pirate-mark-panel`). No new stop and no new ControlIds: the regions
are `PushContext` levels around each MEASURED row, the word riding on that row's cells, so a line two
panels contribute to gets neither word nor key (`hud:empire/line/<n>`) rather than the leftmost
contributor's.

**The Turn log** (2026-08-20): a second notifications stop, `hud:turn-log` (context word
`hud.turn-log-panel`, "Turn log"), rides the shared `GlobalHud` contribution immediately after
`hud:notifications` on all eleven HUD pages. The game's own notifications keep the first stop
— `GlobalHud.Notifications` filters `ModNotification`s out — and the mod's (sightings,
arrivals, sieges, dispatches; `ModNotifications`) live in the second, grouped under
`PushContext` regions `hud:turn-log/turn/<n>` ("Turn {n}", `hud.turn-log-turn`), newest turn
first, arrival order within a turn. Enter opens the shared popup and Backslash dismisses — the
existing stop's behaviors, NO new bindings — and the stop is absent while the log is empty
(owner ruling 2026-08-20). Rows carry no tooltip section: the icon tooltip is the title again
(ES2 facts). The popup's Minimize hands back to the stop that OWNS the minimized
notification, not a remembered one — the popup's own Previous/Next walks game↔mod inside one
popup, so the way out is asked of the notification being put aside
(`NotificationScreen.HandBackOnMinimize`). Working the two lists is
`docs/test-recipes/notifications.md`.

**QUEST MARKERS ARE NODES** (owner ruling 2026-08-22; `ES2Access/UI/QuestMarkers.cs`). A marker
standing at a system is a child of it, declared LAST — after the planets, the lanes and the fleets —
keyed `<system>/marker/<pin guid>`; one planted out in the open (on a fleet crossing a lane) is a
top-level row of the galaxy's drifting region, `galaxy:marker/<pin guid>`, beside the probes and the
missiles. Both are named by the game's own quest title in the tracked or the ordinary form
(`galaxy.system-quest-marker[-pinned]`, the phrase the system's buffer already used), carry the
step's objective in the review buffer, have **no tooltip** (the game hangs none on a marker) and
**Enter is INERT** — a pin is not clickable on the map either, and there is no journal-opening
gesture to invent. Backslash likewise. ONE enumeration feeds the system's buffer lines, the marker
nodes, the scanner's Quest markers category (which includes the open-space ones and lands on the
MARKER, not on its system) and the inspect cell — a cell holding a marker reads it after the places
and before the lanes, and Enter on a cell whose only thing is a marker exits and lands on its node.

## The tree's system order

**The galaxy tree's system order** (merged into ONE list 2026-08-16, superseding the two-region
order shipped the same day). Every perceived system is one list sorted on the SPOKEN pair — rounded
northing descending, rounded easting ascending inside a row a unit high; ties on the rounded pair
fall back to the raw values, which no fixture reaches. There is no colonies-first region any more,
and with nothing drifting in open space the stop declares **no regions at all** (one region would
swallow Alt+Up/Down). `[Beginner] test` reads, ordinals included: Ita `1 of 13`, Leo, Qarius,
Primus, Libra, **Dusay `6 of 13`**, Rigel, Heka `8 of 13`, Electra, B10 6805, Heracles, Osulo,
Byrtus `13 of 13`. The independent oracle is an `/eval` walk of `Galaxy.StarSystemNodes` (as a
non-generic `IEnumerator` — the property is a yield iterator) filtered by `MapVisibility.Perceived`,
sorted through `MapCoordinates.ReadingOrder` and printing `MapCoordinates.Round(position − origin)`;
it agreed name for name and ordinal for ordinal. Home is not pinned first: Dusay sits 6th because
its northing 0 falls between Libra's 11 and Rigel's −5. Nothing else re-orders: the scanner keeps
its distance sort (baseline below unchanged) and a branch's children keep theirs.

A system row's "N population" part comes off the map's own drawn LABEL and is absent while the
camera is not drawing that label — so a system dumped right after a camera move can read
"colonized, 3 fleets under way nearby" with no population, and the same row reads
"colonized, 5 population, 3 fleets under way nearby" from the home camera. Not a regression;
put the camera back (`DevProbe.Camera()` focus `[68.884, 0, -22.45]`, zoom slider `10 of 15`)
before comparing a before/after dump.

## Travelling the starlanes

**The model.** A starlane is a LEAF and Right on a NAMED one travels (`NodeVtable.OnFollow` →
`KeyGraph.TreeMove.Followed`, consumed silently): the cursor lands on the destination system's ONE
node at the root of the systems stop, that branch opens, and the camera goes there through the page's
one landing and so through the camera rule below — never `ZoomIn`, because travelling is not a click
and must not confirm an armed targeting mode. The landing's ordinary announcement is the whole
announcement. **Backspace pops the trail** while focus is in `galaxy:systems`: back to the exact lane
node under the origin (the origin re-expanded so that node exists), camera back to the origin, again
no words; a hop whose origin or destination is no longer perceived is SKIPPED, and an empty trail is
consumed and silent. A system opened BY travel is collapsed on the way out (another hop or a pop) and
one the player opened is left alone — and neither runs the collapse's own un-zoom, since travel
scripts the camera itself. The trail survives an excursion to another screen (the page keeps its
state on pop) and dies with the game instance. A lane into the dark is a silent leaf under Right.

Read-only. `[Beginner] test` perceives twelve star systems plus one special node, so chains longer
than two hops are available; the worked example below runs Dusay `535` ↔ Primus `543` ↔ Rigel `505`
(the three system ids are current), and its lane keys and camera figures were measured when only
those three were perceived — **re-measure them on the next live session**.
A lane is a LEAF: no expansion word in `/gui/graph`, and
`ui.right` on `galaxy:system/535/lane/658` (unexplored) is consumed and silent. On a named one
(`galaxy:system/535/lane/661`) `ui.right` speaks the destination's ordinary landing
("Primus, group, 1 Fleet, expanded, …") and `DevProbe.Camera()` moves focus `52.6,-27.5 step 9` →
`85.1,-1.8 step 12`. `ui.secondary` pops: the exact lane node again, camera back at the origin, and a
system opened BY travel reads `collapsed` afterwards while one the player opened stays `expanded`. A
third pop on an emptied trail answers `consumed (navigator)` with an empty `speech` array — which is
the whole assertion, since nothing else on this stop wires `OnSecondary`. The trail survives closing
a screen over the page (`Gui.GuiService.GetWindow<TechnologyScreen>(false).HandleInput(InputAction
.Exit)` is the cheap way back) and is gone after `POST /loadsave`.
**Proving travel is not a click.** Arm a mode with no order behind it —
`Services.GetService<Amplitude.Unity.View.ICursorService>().ChangeCursor(typeof(
CoordinationRequestCursor), Gui.GetCursor())` — and read it back with an IIFE returning
`Amplitude.Unity.View.Cursor c = Gui.GetCursor()`'s type name plus `HasUserInstructions` (bare
`Cursor` binds to `UnityEngine.Cursor` in the REPL). `ui.right` on a named lane leaves it
`CoordinationRequestCursor instructions=True`; `ui.activate` on the same lane ends it — "Target
selection ended", `GalaxyCursor instructions=False` — which is the mode-ends evidence, no order
posted either way. Note the tutorial arms a probe cursor of its own around turn 6, so read the
cursor before assuming the mode you set is the one that is up.

## The map widget's own keys and claims

**The claim, without pressing a key.** `DevProbe.Claims("PageUp,PageDown")` must read `claims:false`
on `screen.galaxy` (no modifier is held during an HTTP request), which is the over-suppression
check — the game's keyboard zoom is untouched. `DevProbe.Chord("Ctrl+PageUp")` reads
`suppressed:false` for the same reason and proves nothing; isolate the conjuncts instead —
`ES2Access.Screens.GalaxyScanner.Active` (the map-stop half) and
`ES2Access.UI.Input.KeyboardBinding.AnyModifierHeld` — and walk `ModEntry.Input.Actions` (as
`System.Collections.IList`) printing `BindingsDisplay`, `ClaimedWhen != null` and `ClaimedWhen()`.
The one physical check left — bare PageUp/PageDown still zoom, the chords do not — goes on the
manual script.

**The chords are keys of the MAP WIDGET, not of the galaxy page** (fixed 2026-08-17; they used to
fire from any stop of the page). Off the galaxy AND on the page's other stops — `hud:empire`,
`hud:view-title` (which is where the **Zoom slider** lives, `ui.down` once from the stop's first
control), `hud:quest`, `hud:tutorial`, `hud:notifications`, `hud:turn` — every `galaxy.scan*`
injection answers `unconsumed` with an empty `speech`. Injection alone is not the oracle (an
`unconsumed` key that had already stepped the cursor would look the same), so pair it with a state
probe: reflect `GalaxyHudScreen._scanner` → `GalaxyScanner._cursor` and print
`Category/Subcategory/Index` plus the private `_armed`. That probe is also the RETENTION oracle —
park the cursor on the map (`Fleets: friendly`, index 2), Tab away, inject, Tab back, and the four
numbers must be unchanged and the next `galaxy.scanNext` must step (`… 3 of 6`) rather than re-arm.

## Fog, labels and map marks

**Lowering a fog state reversibly.** `EntityExploration.SetState` refuses to lower, so write
the byte directly: `node.Exploration.GetCurrentStates()[empire.Index]` is by-reference — set
it, dump the graph or invoke the predicate, put the original back in the same `/eval`.
(Proved the lane gate both directions: a PartiallyRevealed link dropped to Identified left
the tree; restored, it returned with its original numbering.)

**The 136-label SystemLabelReadout census** is the byte-identical regression oracle for any
galaxy-label change: one `/eval` calling `Lines/Population/Sleepers` over every pooled label,
diffed before/after (on `unlocked` turn 1: 135 labels answer 0 lines, Xiu answers 2).
**Force-showing a label badge** (exploration winner and kin): instantiate the game's own prefab
into the drawn label, set the tooltip the way `Refresh` does, read, then `DestroyImmediate` +
`RebuildInternalChildrenList(false)` + re-derive the private `metaModifiers` array (it is
indexed with no null guard).
**Force-showing a constellation label** (its tooltip never fills naturally in `unlocked` —
labels are culled, ES2 facts): `label.CulledIn = true` + `ShowOrHideIfVisibleByEmpire(
window.LookingEmpire)` + `Dirty = true`; restore with `CulledIn = false` + `Hide(true)`.
Note a force-shown label's tooltip rows all report rect (0,0,0,0) — no longer a blocker: the
`"constellation"` typed reader answers off the feature's own fields whatever the rects say, and a
FOCUSED constellation node holds its label shown by itself (`ConstellationLabelHold`, re-asserted per
frame because culling recomputes on camera-POSITION change, and released on blur/pop/reload through
the window's own re-decide) so the game's "Constellation" tooltip has a widget to fill; the recipe
is only needed for labels no node focuses. The hull-oracle one-liner:
`/eval ES2Access.UI.ConstellationMap.AuditWholeGalaxy().ToString()`.

**`unlocked` fleet/orbit realities** (2026-08-20; the live fixture has since ADVANCED to turn 3
— Gistrad and Hir now perceived, both fleets still mid-lane; re-probe before trusting counts):
at turn 1 NO player fleet is in orbit (1100 and 1108 both mid-lane, `NodeIndex=-1`), Xiu
(GUID 548) is the only explored system, no wreck exists anywhere in the galaxy, and fleet
1108's action panel draws 8 buttons, all "must be orbiting" — of the six seat actions only
Start Expedition is drawn at all. Xiu's orbital rows: `planet/0|1|3/action/0` = Colonize,
`planet/1/action/1` = Signal (curiosity).
**Reproducing the discovery cutscene reversibly**: write
`StarSystemNode.DiscoveryStatuses[empireIndex] = false` on an explored system, then
`GalaxyView.SelectGameNode(node)` — the game sets the byte back itself at the cutscene's end,
nothing to restore. In `unlocked`, Kamos (GameNodes index 75) has three curiosity-bearing
planets; Gistrad (79) is undiscovered outright. **Pressing a real fleet-action button of the
six seat actions is SAFE** — their `OnClick` posts no order, so
`FleetActionItem.SetEnable(true, null)` + `Vtable.OnActivate()` fires the true closure without
spending a probe.
**Finding a fogged lane cell**: one `/eval` over `GameNodes()` for links where `Drawn(link)`
but exactly one end is `Perceived`, then sample `IVisibilityService.IsExplored` along the
segment for the boundary. **Driving the inspect cell from `/eval`**: reflect
`GalaxyInspect._driving` + `JumpTo(x,y)` — collapses dozens of arrow injections to one call.
**Verifying the fleet-action seat without an orbiting fleet**: in one `/eval`,
`view.ZoomInOnNode(node)` (or `SelectGameNode`) + `page.SeatAfterFleetAction(node, kind)` via
`ES2Access.ModEntry.Screens.Current as GalaxyHudScreen`.
**Firing a real mod node whose game control is disabled**: `FleetActionItem.SetEnable(true, null)`
then `ModEntry.Navigator.CurrentNode.Vtable.OnActivate()` in one `/eval` (the panel's next refresh
restores the flag) — proves the real closure instead of re-implementing it.
**RAISING to `MapVisibility.Perceived` needs TWO writes** (2026-08-20): `Exploration ≥
Identified` alone is not enough — also reflect `EntityVisibility`'s private `layers[]`
(`GetField("layers", Instance|NonPublic)`) to `Layer.Known`; restore both from saved bytes.
Verified reversible (a revealed far end returned to "to an unexplored system"). Fixture
fact: `unlocked` at turn 1 has NO named starlane — Xiu's four lanes are all dark and lane
944's far end is a SpecialNode (never travelable) — so lane-travel work needs this reveal
or a later save.

**Trade routes without a trading company** (the fixture cannot make one — preprocessor needs
the HQ tech and the built improvement): inject a fake company/routes into
`DepartmentOfCommerce` reversibly, then oracle against the renderer — invoke the private
`TradeRouteRenderer.UpdatePlayerEmpireDependantData` and diff `lineToRenders` (material +
endpoint pairs) against the mod's rows. Empty-state check: baseline the scan content stop
before injecting and after removing.

**Forcing a special node perceived**: exploration byte via `GetCurrentStates()` +
`SetLayer(…, Visible, silent)`; restore the LAYER by reflection on the private `layers`
array. Its subtree is legitimately empty ("Nothing in here") while its links stay unrevealed
— the same answer an ordinary star with unrevealed links gives.

**Forcing the system node's map marks** (all `/eval`-reversible; pristine save is the restore):
ground battle — add `StarSystemLabel.GroundBattleInProgressTag` to the colony's
`SimulationObject.Tags` (the NAMED-attacker branch is unreachable: `GetGroundBattleOnNode`
matches `DefenderNode`, which only resolves from a real serialized battle); time bubble —
`DepartmentOfTheInterior.CreateTimeBubble(newGuid, <definition name from the
TimeBubbleDefinition database>, node)`; quest markers — `IQuestManagementService.Register`
/`Unregister` a `QuestMarker`; guard — the public `GuardingEmpireIndex`/
`GuardingDisplayedEmpireIndex` setters; citadel — `colony.BindCitadel(new Citadel(guid,
Citadel1))`, undone by `UnbindCitadel(true)`. Lowering an `EntityVisibility` LAYER (unlike
exploration) needs reflection on the private `layers` array.

**The system-label batch.** Most of it is fixture-blocked; the escape hatch is the force-content
trick in two variants — write the game's own `%…Description` into a label's tooltip `Content`, or
assign the WRAPPER the label reads its name off — then focus the node, read
`/gui/graph?buffers=1`, and let the next refresh blank it. Every read must be gated on
ancestor-walked visibility, because the hidden pooled widgets hold the previous system's values.
The one-frame variant for whole readouts: force the widgets, call
`SystemLabelReadout.Lines(label)`, restore — ALL inside one `/eval`, so the game's own refresh
cannot intervene; the absence-diff must come back RESTORED == BASE. **Raising the bar over a modal
on demand**: flip the bound tutorial's `TutorialDefinition.Layer` (via `TutorialPopupPanel`'s
private `tutorial` field) to `AboveModalWindows`, then
`TutorialWindow.UpdateVisibilityAccordingToOtherWindows(Gui.GuiGameWindowService, true)` — the
game re-evaluates only on a change; restore the layer afterwards. Entering a system binds
"A MATTER OF INFLUENCE", leaving unbinds it. The unlocked save's four Xiu lanes are all
unexplored — no lane TRAVEL is testable there; `[Beginner] test` is the fixture for that.

**Probes and faction panels (the other galaxy labels).** The unlocked save draws NONE of this
surface. A probe row is exercisable with `probeLabel.Show()` then `Hide(true)` — self-healing. The
faction panels need `Bind` + `Show`, then `Hide` + `Unbind` **and** `InspectedEmpire` restored
through its private setter: `Unbind` leaves the game's own `Refreshed` handler live, which NREs on
the next refresh otherwise.

**A merged fleet lozenge has no fixture**: `[Beginner] test` never draws `MergedFleetLabels`
(count 1, `vis=False alpha=0` at every zoom probed 2026-08-24) — two stacked fleets are needed to
sight the pooled-lozenge aim re-commit live.

## "Go and look at this"

**The game's own "show me this fleet", and what the galaxy page does when it lands.** The repro for
"the map snapped back to where I was" needs the locate to happen while the galaxy page is DOWN:
focus a system node (type-ahead via `POST /type` with the system's name — the map ids are
`galaxy:constellation/<c>/system/<id>` under the constellation grouping, so a hand-built
`FocusNode` id is fragile; the focus pans the camera there), `Gui.GuiService.ShowWindow<MilitaryScreen>()`, then run the locate from
ES2 facts (`ICursorService.Select(galaxyFleet.CursorTarget)` → `ChangeCursor(typeof
(GalaxyGarrisonCursor), galaxyFleet)` → `RequestGalaxyOverviewViewLevel(fleet)`). That last call
closes the screen by itself, so the galaxy page comes back in the same `/eval` and its speech is in
that response. Two things to read: `DevProbe.Camera()` must still be on the FLEET's position (a
snap-back shows as the system's own position returning), and `DevProbe.Screen()` must report the
fleet's node — in `unlocked` both visible fleets are mid-lane, so the landing proves the lane
branch was opened too. The false-positive half of the same test: select a fleet with the galaxy up,
move the cursor elsewhere (`hud:end-turn`), then show and hide `MilitaryScreen` — the cursor must
stay where the player left it, because the game made no reveal request (the landing is driven by
`GalaxyLocate`'s capture now, not by diffing the selection across the page's absence).

**Every other "go and look at this" the galaxy answers** (`GalaxyLocate`, all from `/eval`, all with
the galaxy already up unless said otherwise, each read as `speech` on the reply):
a NAMED thing — `Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(colonizedStarSystem.Node)`
→ the cursor lands on that system's node, one announcement, nothing of the mod's own added;
a POINT at a declared place — the same call with `(Vector3)node.GalaxyPosition` → the same landing;
a POINT out in the open — `new UnityEngine.Vector3(60f, 0f, 60f)` → "Shown on the map" then the
view-title node; the F2 DOWNGRADE — `RequestStarSystemManagementViewLevel(nonOwnedNode.GUID)` → the
same pair, and no claim that a page opened; the NEXT IDLE FLEET, which needs `[Midgame] quests
fleets` (two idle fleets parked in one berth) — invoke the private `EndTurnWindow.OnNextIdleFleetCb`
by reflection and the cursor must land on the fleet the game selects, ALTERNATING between the two on
repeated presses (landing on the same one twice is the berth tie-break reading the previous
selection — ES2 facts). A QUEST PIN needs a quest with markers, which neither fixture has: compose
the game's own nesting in one `/eval` statement instead — `RequestGalaxyOverviewViewLevel(pos)` then
`ShowQuestLocation(quest, quest.GetCurrentStep())` on the marker-less pinned quest — which leaves
exactly the state `ShowQuestLocation` leaves when it does have one, and must say
"⟨quest title⟩, objective shown on the map" before the landing. The same call ALONE on a marker-less
quest must be silent.

**A landing's announcement waits for the camera.** Out of the inspect cell the landing's own
announcement is the whole utterance, once, and it is composed after the map has caught up:
`Screen.LandingSuspended` covers `GalaxyViewLevels.CameraSettling` plus a twenty-frame tail, and a
suspended frame holds even a control that is already declared (`FocusRequest.Step`). Measured: a
scanner jump to Osulo I used to say "Osulo I, Colonized, 1 of 7" mid-flight and now says "Osulo I,
group, Medium Mediterrane., Colonized, collapsed, 2 of 8". The landing rules themselves (which
cursor moves, zoom or slide) are `MapLandings.Decide`'s doc comment in
`ES2Access/Core/UI/MapLanding.cs`.

## Moving the camera, and the camera rule

**The rule itself** (owner ruling 2026-08-23; `GalaxyHudScreen`'s `Screen.OnFocusVisual` override →
`Place` → `FollowPlace` — `ES2Access/Screens/GalaxyHudScreen.cs`). Every focus landing on the map
stop resolves to a PLACE — the system a row hangs under, or a drifting thing itself — and to whether
the cursor is ON that row or INSIDE it. Same place, same closeness → nothing moves. A different place
with the cursor ON its row → `PanTo` (the slide, zoom untouched). Further IN on a place → `SnapTo`
(no flight, and it arms the landing's own settle wait). **The camera is never taken back OUT by the
rule**: stepping from a world up to its own star moves nothing, and the ways out stay the player's
(Backslash, closing the branch). Three triggers were folded into it: the system row's own `PanTo`,
`OnExpand`'s `ZoomTo` (system nodes no longer override `OnExpand` at all; the engine keeps the
expansion set) and the go-to landing's own `SnapTo`, which now asks the rule and so leaves nothing
for the landed node's focus to add. Expanding a system with Right brings the camera in because the
FIRST CHILD's focus does it, so the zoom and the descent are still one press. The rule's record is
what the camera was ASKED for, not where it is — which is why a zoom-out by hand answers "already
there" for the rest of that system's children, and why a go-to moves anyway, a landing being a
request rather than the cursor wandering. **Collapse un-zooms** while `GalaxyViewLevels.FocusedSystem`
is still that system (a camera the player has since moved elsewhere is left alone) and drops the
rule's "inside" record, which is what lets re-opening the same system bring the camera back in. Why
the gate is NOT `FocusedStarSystemNode`, and why an arrival is not finished until the map is DRAWING
the new system's planets (`GalaxyHudScreen.ShowFocusedSystem`), are both in `docs/galaxy-map.md`.

**Moving the galaxy camera.** `GalaxyViewLevels.PanTo/ZoomTo/ZoomToStep/OpenSystem` in the mod (`ZoomToStep(node, 9)` is how a
test puts the fixture's camera back home in one call); from
`/eval`, `((GalaxyViewCameraController)Services.GetService<ICameraService>().CameraController)
.ForceZoomingOnPosition(step, pos)` (fully qualify). There are 13 steps: step 3 draws a system's
name only, step 9 its whole label (name + planet circles), and **only step 12, the last, reaches
the ORBITAL view** — `CanFocusGalaxyEntity()` is `zoomStep == ZoomStepsCount - 1`, and until it is
true `Gui.GuiGameWindowService.FocusedStarSystemNode` stays null and
`PlanetLabelsWindow_SystemOrbital` (one `PlanetLabel_SystemOrbital` card per planet) is never
shown; the camera must also be within `DistanceMinToCatchFocusOnNode` of the node, so zoom AT it.
Step 9 vs step 12 is the evidence-crop pair for the two things a planet child can read. Only ONE system label is
visible at either step (86 exist, all keeping their node and tooltip), so the tree's label lookup
is unaffected — but at step 12 the focused system's own label is pushed off the top of the screen
(y ≈ -230), which is why the system node's pointer goes to
`PlanetLabelsWindow_SystemOrbital.StarTooltip` instead. Never `SetZoomStep()` alone: it swaps the
drawn layer without moving the camera. `DevProbe.Camera()` before and after; the fixture's home is
focus `[68.884, 0, -22.45]`, zoomStep 9.

**Watching the camera rule from `/input`.** One `/eval` after each injected action, printing the
camera's own target and the cursor together, is the whole instrument:
`((GalaxyViewCameraController)Services.GetService<ICameraService>().CameraController)
.cameraTargetTransform.position` (fully qualify `Amplitude.Unity.Framework.Services` /
`Amplitude.Unity.View.ICameraService`) beside `GalaxyViewLevels.ZoomStep`,
`GalaxyViewLevels.CameraSettling` and `ModEntry.Navigator.CurrentNode.Id`. `settling=True` on the
line after a step is a PAN in flight; `settling=False` with the step changed to 12 is the SNAP.
`DevProbe.Camera()` answers the same focus/eye/step without the cursor.

MEASURED 2026-08-23 on `[Beginner] test`, all through `POST /input`:

- **Right on a closed system**: step 9 → 12, focus onto the system, `settling` false on the very
  next probe (one snap, no flight), cursor on the system's first child.
- **Down/Up inside one system** (management, planets, lanes, a fleet, a `Tooltips` dossier card):
  nine consecutive steps, camera bit-identical at `(68.48,-22.85)` step 12 throughout.
- **Backslash on the system row** (`ui.contextual`): step 12 → 9, focus unmoved — and then walking
  the same children keeps step 9. That is the "a zoom by hand survives" rule.
- **Left** (collapse): step 12 → 9, focus unmoved; **Right again** snaps back in. (Re-opening only
  works because the collapse drops the rule's "inside" record — a build where Right after a collapse
  leaves the camera out has lost `LeftPlace`.)
- **Crossing systems with Down**: a step onto another system's ROW pans (`settling=True`, step
  unchanged at 9 or at 12); the next step INTO its children snaps.
- **`POST /type "dustcid"` + `ui.down` × 3**: one snap per step, four systems (Leo, Qarius, Primus,
  Heka), `settling` false at each — and 9.4 still holds, `ui.back` leaving only the last one open.
- **A go-to** (`ui.goToLocation` on a turn-log row, the scanner's `galaxy.scanGoTo`): camera in at
  step 12, `settling` false, and the announcement is the SETTLED row
  ("Libra, -11, 11, group, No owner, expanded, 5 of 13, Libra II, Tiny Boreal, Colonizable,
  1 curiosity, 2 of 2"). It moves even when the record says the camera is already there, which is
  the case to test by pressing Backslash first.
- **A game-led locate** (`Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(node)` from
  `/eval`): the cursor lands, the camera arrives once, and ten probes over five seconds show no
  second jump.
- **A HUD stop** (`ui.focusTurnLog`): the camera does not move at all — the rule is scoped to the
  map stop.

**Measuring a landing's camera cost.** The sharp instrument is a plain boolean `POST /wait`
predicate on the game's own gate, which reports `frames` and `elapsedMs`:
`ES2Access.UI.GalaxyViewLevels.FocusedSystem != null && Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemOrbital>(false).Shown`
answers "is the orbital-card surface up" (measured 1 frame / 0 ms after `SnapTo`, 8 frames / 598 ms
after `ZoomTo`), and `!ES2Access.UI.GalaxyViewLevels.CameraSettling` answers "has the flight ended"
(894 ms / 11 frames for `ZoomTo`). The SNAP settles in 0 ms and leaves the camera identical to the
flight's — step 12, `ZoomForced` still true, `RestoreZoom` still valid — so the fast-forward is a
timing change and nothing else. `DevProbe.RowTrace` is the blunter one and reads the navigator's
last built render, so it under-reports a row that changes without a rebuild.

## Map coordinates

**Map coordinates.** The route in is `ui.next` to `galaxy:systems` and reading the dump: home
(Dusay) must read "0, 0" and every other pair must match
`ES2Access.UI.GalaxyCoordinates.Text(n.GalaxyPosition)` computed over `Galaxy.GameNodes` from
`/eval` (`StarSystemNodes` is a yield iterator — `as IList` on it answers null and the eval
NREs; index `GameNodes` instead). The free oracle is the ORBIT invariant: walk every empire's
`DepartmentOfDefense.Fleets`, and for each fleet with a `FleetOrders.Orbit` the fleet's pair
must equal that node's — 8 orbiting fleets, 0 mismatches on `[Beginner] test`, and none of
them the player's, so this is a model check rather than a spoken one. What the fixture DOES
reach spoken: a special node (`B10 6805, -5, -26, Solar Nebula`), a drifting probe (a TOP-LEVEL
row in the open-space region since 2026-08-19 — no branch to expand; its sentence names the star
it is out from), and fleets under way (`1st Defenders Navy, 12, 15` on Dusay's lane to
Primus). Fixture-blocked: an orbiting fleet of the player's own, an obliterator projectile,
and an ally coordination pin. The negative half is one dump of `screen.star-system`: the
rename button reads `Dusay, button, …` with no pair.

## The scan view

**The scan view.** Entry is Enter on the lens toggle — which sits in the view-title stop's ROW,
so it is `ui.right` from the name node and Up/Down never reach it (Down goes name → Zoom), and
it is not declared at all at a rung with no lens over it (rung 13 draws none; step out to ~7
first). Leaving is Enter on `scan:title/lens`. The PLAYER route through the lenses is the
`Zoom` node beside it — Left/Right steps the 15-rung ladder, Shift+Left/Right jumps a lens band,
Right at rung 13 enters the system, Enter on a planet card reaches the planet lens, Left steps
back out. Verify with those injected actions; `cam.ForceZoomingOnPosition(step, position)` is for
RESTORE only (`SetZoomStep` alone leaves the labels culled). **The zoom IS present in scan mode
(re-measured 2026-08-17 against a "the slider is missing in scan mode" report, NOT reproduced):**
`scan:zoom` is the second node of the title ROW, reached by `ui.down` OR `ui.right` from
`scan:title/lens`, announcing `Zoom, slider, N of 15, 2 of 2`, and `ui.left`/`ui.right` step it
(measured across rungs 0→12 both ways 2026-08-17, with the lens name spoken on every DESCRIPTOR
crossing — including the three where the name does not change, so a full sweep says "Diplomacy
scan" at 0↔1, "Trade Scan" at 1↔2 and again at 3↔4, "Economy scan" at 5↔6, "System scan" at 9↔10
and again at 11↔12, and nothing but the slider value inside a band). **The zoom BAND word is silent
while the game's scan mode is up** (`ZoomBand` returns null under `Scanning`; the lens titles carry
the naming there). Two states DO leave
it valueless or absent, both shared with the plain HUD and both by design: `ZoomLadder.Text` returns
null while the ladder waits for a level the game has not moved to (the announcement keeps the name
and loses `N of 15` — seen on the refused `ui.right` at rung 13), and `ZoomLadder.Build` declares
nothing at all where `GalaxyViewLevels.ZoomRung < 0` (a battle lens or the system-discovery view). The zoom table: 0-1 Diplomacy /
2-5 Trade / 6-9 Economy / 10-12 System, plus the system and planet layers — six titles over nine
descriptors, so the band boundaries are one rung finer than the titles (ES2 facts). **All four lens windows
report `Shown` at once**, so the drawn `ScanViewWindowHeader` is the only reliable lens signal and
`CaptionsPanel.ScanViewGuiElement` goes stale. Restore `ShowScanViewCaptions` and
`ShowScanViewSystemInfos` afterwards. Fixture notes: one perceived system; synergies on two of
Xiu's four planets; the rank graphs and remains panel never draw at turn 1.

**The governor panel on the system-management lens** (rung 14 + scan) needs no save with a
governor and no simulation change. Its `Bind` refuses without an `AssignedHero`, so bind it by
hand: copy the window's private `guiInspectedSystem` into the panel's `guiColonizedStarSystem`,
put `new GuiHero(((MajorEmpire)Gui.PlayerEmpire).GetAgency<DepartmentOfEducation>()
.ActiveHeroes[0])` into its `guiHero`, invoke its private `RefreshEfficiency` and
`RefreshFidsiBonus`, then `ArrangeChildren()` + `Show()`. The unassigned academy hero has NO
colonized-system skills, so the dial comes out at angle 0 — set `EfficiencySector.MaxAngle` by
hand (216f reads back as 60%) to see a real proportion. Restore with `MaxAngle = 0`,
`Hide(instant: true)`, `Unbind()`, and re-dump: the `scan:hero` node must be gone.

**The planet lens's remains table** (rung 15 + scan) is empty on every planet of Xiu. Sight it
with `PlanetRemainsItemsTable.ReserveChildren(1, w.PlanetRemainsItemPrefab, "PlanetRemainsItem")`,
write `Title.Text`/`Description.Text` on the child's `PlanetRemainsItem` and set the child
`Visible = true` + `ArrangeChildren()`. Restore by blanking both labels and hiding the child; the
pooled child stays in the pool and the window's next `OnBeginShow` re-reserves the table from
`Planet.Remains`, so nothing survives a planet change.

**Leaving scan mode from `/eval` is not obvious.** `IGuiGameWindowService` has no `RequestScanView`,
and pressing `GameOverlayWindow.TopTitlePanel.ScanButton` through `AgeWidgets.Press` did nothing.
`POST /loadsave` of the fixture is the reliable way out (and then re-minimize the tutorial and
`POST /reload` before any sheet-keyed comparison).

## The fog-off smoke pattern

**Galaxy: the fog-off smoke pattern.** Forcing a world-state predicate TRUE in code, rebuilding,
walking read-only and reverting is the sanctioned alternative to mutating a save — it is what proved
the shared-`Link` teleport. In the unlocked save the map draws 1 perceived system, 0 hangars and a
Signal curiosity that refuses, so nothing else on it can be sighted there.

## Type-ahead on the map

**Searching what a collapsed branch would declare.** On the galaxy, `POST /type "raia"` with Dusay
CLOSED lands on `galaxy:constellation/446/system/535/planet/2` and announces
"Raia, Medium Terran, Colonized, 4 of 8" — the system opens on the way. The cost of the search's
one fully-open build is `ES2Access.UI.GraphNavigator.SearchBuildMs` / `SearchBuildNodes` (32-78 ms /
131 nodes on the galaxy at turn 21). To see the whole enumeration a search is looking through,
build it by hand from `/eval`: `new GraphBuilder(new HashSet<ControlId>())` with `ExpandAll = true`,
`screen.Build(b)`, `screen.BuildShared(b)`, then walk `render.Order` printing
`SearchScope.TextFor(node)`. **"antimatter" IS findable** — a
system's deposit dossiers come from `node.Planets[*].ResourceDeposits`, not from the icons the label
happens to be drawing, so every deposit system carries one card per kind at every camera. Measured
across the whole slider on `[Beginner] test`: `hyperium` 2, `titanium` 2, `transvine` 2,
`dustcid` 4, `antimatter` 1 — eleven cards over the six deposit systems (Osulo, Qarius, Ita, Heka,
Primus, Leo), identical with the camera out over the galaxy and with it in on Dusay's orbital view.

**Type-ahead stepping closes what it opened.** On the galaxy with nothing
expanded, `POST /type "dustcid"` (4 results in 4 systems) then `ui.down` three times: exactly ONE
system is expanded at each step — the one the cursor is in — and `ui.back` ("Search cleared") leaves
the LAST one open. A branch the player expanded before typing is never closed: expand Dusay by hand
first and it is still open after the search has walked past it.

## Dossiers and tooltips

**Walking a "Tooltips" region.** Three surfaces carry one on
`[Beginner] test`, and each is reached the same way: expand the node with Right twice, step to the
second region with **Alt+Down** (`ui.regionNext`), and read the dossier nodes there.
- **Galaxy system.** Route: on `screen.galaxy`, Tab to `galaxy:systems`, `POST /type "osulo"`,
  `ui.back`, Right twice (the camera comes in). The actions region holds Diplomacy, three planets,
  three lanes and a fleet; the Tooltips region holds `Osulo` (the system dossier — its header reads
  "Osulo - Niris" off the LABEL and just "Osulo" off the orbital window the camera swaps to) then
  `Hyperium`, `Titanium`, `Transvine`. Dusay has the system dossier and no deposits.
- **System-management planet card.** Route: open Dusay's management page
  (`Gui.GuiGameWindowService.RequestStarSystemManagementViewLevel(node.GUID)`), End to `Dusay I`,
  Right twice.
  The actions region is Colonize + two curiosities; the Tooltips region is `Dusay I` then the five
  pips (`Planet Food production` … `Planet Influence production`). A colonized planet (Raia) swaps
  the strip: the same five dossiers arrive off the `FidsiEnumerator` duplets instead.
- **Research dot.** Route: technology screen, expand a quadrant, a stage, then a dot. Only dots the
  wheel binds unlock icons for become groups — Adamantian Alloys gives four (Advanced Strike
  Fighter, Adamantian Repairbots, Adamantian Enhancer, Squadron Shifter), Planetary Landscaping two.
  Each node's buffer is the unlocked thing's FULL page including `Cost:`.
Evidence per node: `DevProbe.Tooltip()` (`shown:true` plus the dossier's own class), the focused
`GET /gui/graph?buffers=1`, and a `crop-shot.ps1` of
`Gui.GuiService.GetWindow<GuiTooltipWindow>(false).AgeTransform.GetGlobalPosition()`.

**The frame-order probe for a NESTED dossier** (one drawn inside another tooltip). Focus the row,
let its tooltip draw, then walk the tooltip WINDOW for tooltips of its own
(`GetWindow<GuiTooltipWindow>(false).GetComponentsInChildren<AgeTooltip>(true)`) — the inner ones
have real targets. Then point at one and re-probe: `shown:false` is the answer, and it is the answer
by construction (ES2 facts). Do this before designing any nested-dossier reading.

**Fixture-blocked here.** The hero detailed card's four-symbol row (`HeroInspectionScreen`
`AddRow` → `AddDossierRow`) needs the Academy, which is tutorial-gated on `[Beginner] test`; the
construction line's festival badge needs a Hissho festival constructible; the honor gauge's own
dossier needs a Hissho empire. All three are declaration-side only and gated on finding ≥1 (≥2 for
the hero row) named class-backed dossier, so they are inert everywhere they were not measured.

**What a tooltip's panel features are, and which have typed readers**, is the
`DevProbe.TooltipParity()` row in `docs/dev-loop.md` §1. Regenerate the class-to-feature map from
`Public/Gui/GuiTooltipDescriptions*.xml` (strip XML comments first — several features are commented
out and a naive grep counts them).

**Reading one drawn tooltip feature by feature, when `DevProbe.Tooltip()` refuses.** The same
reading is available directly:
`Gui.GuiService.GetWindow<GuiTooltipWindow>(false)` → `ES2Access.UI.DrawnTooltip.Features(w.AgeTooltip)`
cast to `System.Collections.IList`, then unbox each entry as `ES2Access.UI.TooltipFeatures.Reading`
and print `Feature`, `Reader` and `Lines`. Which features the FALLBACK has answered for is
`ES2Access.UI.TooltipFeatures.DefaultRead` — walk it with a non-generic `IEnumerator`, the REPL will
not take `foreach` over it.

**The play-deck tooltip** (`PlayDeck` class, the only one in the fixture): on the research wheel,
Tab to `research:tree`, `POST /type "lethal"` → the second result is
`research:technology/TechnologyDefinitionMilitary19/tooltip/1` ("Lethal Squadrons"), a Tooltips
child of a COLLAPSED dot — reachable only since batch 8's search change. Its buffer holds one block
per tactic: the tactic's name, then "Flotilla 1 Short Range / Flotilla 2 Short Range / Flotilla 3
Long Range", then the effect paragraph.

**Deposit dossiers across the zoom.** The evidence pair for a mod-owned
tooltip carrier: `POST /type "antim"` from the map stop, then
`Gui.GuiService.GetWindow<GuiTooltipWindow>(false).AgeTooltip.AgeTransform.gameObject.name` says
which widget is drawing it — `LuxuryItem_1` (the label's own icon) at the systems view level,
`Dossier deposit/543/StrategicDeposit4` (the carrier) once the camera is in on the system and the
deposit LINE has faded to alpha 0. `/gui/graph?buffers=1` of the focused card is byte-identical in
both states (8 lines, the refusal "Missing technology Extreme Atmospherics" included).

**Where the mod's own tooltip carrier draws.** `POST /type "antim"` from the map stop with the camera
in on Primus, then `Gui.GuiService.GetWindow<GuiTooltipWindow>(false)` for
`AgeTransform.GetGlobalPosition()`, `AgeTooltip.AgeTransform.gameObject.name` and
`AgeTooltip.AnchorMode`. Since 2026-08-23 that reads
`(0, 420, 240, 380)` / `Dossier deposit/543/StrategicDeposit4` / `TOP_LEFT` on a 1280x800 screen —
the panel's bottom edge ON the screen's bottom edge, left edge at x=0. `crop-shot.ps1 -Rect
0,420,240,380` is the evidence crop. The AGE screen's own size is
`GetWindow<GuiTooltipWindow>(false).AgeTransform.Screen.Root.Width/Height`, which on this machine
equals `Screen.width/height` 1:1.

**The galaxy's per-planet dossiers** (`[Beginner] test`, batch 11). Route with player gestures only:
`ui.focusMap`, then the scanner — `galaxy.scanCategoryNext` to **Colonizable Planets**,
`galaxy.scanSubcategoryNext` to **occupied** (the fixture's one entry is Osulo I),
`galaxy.scanGoTo`. That lands on `galaxy:constellation/446/system/491/planet/0` with the branch
opened and the camera on Osulo. `ui.right` opens the world: **2 dossier nodes** — Hyperium then
Titanium. The five FIDSI figures are NOT among them since 2026-08-24 (owner ruling: their pages say
what FIDSI is, on every world, and the management card declares them) — the walk that measured SEVEN
here counted those five. Osulo II has one deposit and shows ONE node; Osulo III has neither deposit
nor anomaly, so it declares no Tooltips region at all and is a leaf again unless the card draws it an
action; neither shows the stale pooled items. Expect the SAME two at every zoom: `ES2Access.UI.GalaxyViewLevels.SetZoom(9, at)` from
`/eval` keeps the branch open and the nodes are then carrier-drawn (the panel appears at the screen's
BOTTOM-LEFT instead of over the icon; the words are identical, verified by crop).
**Landing at zoom 12 does not bind the orbital cards.** `galaxy.scanGoTo` moves the camera while the
view LEVEL stays `GalaxyViewLevel_GalaxyOverview`, so `PlanetLabelsWindow_SystemOrbital` keeps the
PREVIOUS system's cards (measured: focused system Osulo, cards Rigel I / Kyros). `ui.activate` on the
system row (`ZoomIn`) is what rebinds them — check with
`GetWindow<PlanetLabelsWindow_SystemOrbital>(false).GetComponentsInChildren<PlanetLabel_SystemOrbital>(true)`
and read each card's `Planet.LocalizedName` before trusting a "the card is drawn" measurement.
**Expanding several siblings without collapsing any**: `ui.left` on a group collapses it, so walk
UPWARD — `ui.right` (expands and enters the first child), `ui.up` twice (child → this row → previous
row), `ui.right` again. Down-then-right walks past the stop and drags the camera with it.

**Multi-row tables** need a real fixture with several saves/rows — do not mutate the game's
data structures to fake one. **Reading a tooltip the game only writes for somebody else**: a
content-backed tooltip the fixture leaves empty (the orbital card's `OutpostTooltip`, written
only for a FOREIGN outpost) is still provable — set `.Content` from `/eval` to what the game
would write, focus the node, read `/gui/graph?buffers=1`; the card's next refresh blanks it
again, so nothing is left behind.

## Keys: tree arrows, place keys and page turns

Fixture `[Beginner] test` unless an item says otherwise. The keys inject as ACTIONS (`POST /input`
with `ui.pageNext`, `ui.focusMap`, …). **The CHORD half cannot be answered from the dev server at
all**: `POST /key` refuses with 409 unless the game has the foreground (a locked desktop is enough
to lose it), and `DevProbe.Chord("Ctrl+G")` does NOT answer it either — its `Claimed` walks the
combination's KEY CODES and asks `ModInput.ClaimsKey` per key, and type-ahead claims every letter on
every mod screen, so Ctrl+G reads `suppressed:true` on the research screen exactly as on the galaxy
page. "The chord reaches the mod and not the game" is a MANUAL-TEST line, not an injectable one.

- **Tree arrows, one press each way.** MEASURED: on the galaxy stop, `ui.right` on collapsed **Dusay**
  answers "Open system, button, 1 of 8" — the system's first child, with its position, no
  "expanded" word — and the camera goes in (zoomStep 9 → 12, "Zoom level 13 of 15, System
  Overview"). `ui.left` from that child answers "Dusay, 0, 0, group, Home System, colonized, 1 fleet
  under way nearby, collapsed, 6 of 13" and one zoom-out (12 → 9). Left on a HEADER is still the
  plain collapse: from Dusay it answered "Serpens, group, collapsed, 1 of 2". The research wheel is
  the same one press each way ("Military I, group, collapsed, 1 of 6" in, "Military, group,
  collapsed, …, 1 of 4" out, no camera). Starlane travel is unchanged: Right on "Starlane 3 to
  Qarius" travels and says only the landing.
  **No fixture system has an EMPTY expandable group** — even the SPECIAL node (B10 6805) has two
  lane children — so "Nothing in here", the group staying open and the second Right being a
  consumed leaf are still unit-test-only.
- **The six place keys.** MEASURED on the galaxy page, each landing identical to the one Tab reaches:
  `ui.focusEmpire` → "Controls, Empire Summary, button, unavailable, …, 1 of 8"; `ui.focusNotifications`
  → "Notifications, Laws Cancelled, button"; `ui.focusTurn` → "End Turn (Ctrl+Alt+E), button, Turn 21,
  1 of 6"; `ui.focusMap` → "Galactic Map, …" on the remembered position. Where the stop is absent the
  key is TOTAL silence with the cursor unmoved, proved twice by `DevProbe.Screen()` either side:
  `ui.focusTurnLog` on the galaxy (the fixture's Tab ring is Controls → View Controls → Galactic Map
  → Quest → Tutorial → Notifications → End Turn, with no turn-log stop) and `ui.focusMap` on the
  research screen.
- **Ctrl+Alt+E.** MEASURED: `ui.endTurn` on the galaxy page with `CanEndTurnByShortcut` true says
  nothing of its own and replays the game's shortcut — so the FIRST press answered the game's own
  idle-system prompt ("Construction Queue empty", "Idle Systems, 1 of 2") and the second ended the
  turn (`Gui.Game.Turn` 20 → 21, `hud:end-turn` "Turn 21" → "Turn 22"). The refusal branch (the
  node's own readout) has no fixture: nothing on this save refuses the key.
  **Encounter check (on a battle screen):** `E` is `EncounterCameraElevationUp` and the encounter
  cameras poll their bindings PRIVATELY — those matchers are not covered by `GameKeyStandDown`
  (:49-52). Fixture-blocked: no battle exists on `[Beginner] test`, and it needs `POST /key` anyway.
- **Alt+Left/Right on each of the four pages.** MEASURED: the STAR-SYSTEM page turns (Dusay → Heka →
  Dusay, the game's own cycle wraps with two colonies); the PLANET page turns and wraps ("Dusay II,
  Inhospitable" / "Dusay I, Inhospitable" / "Raia, Colonized"), one clean utterance per press; the
  NOTIFICATION popup with a single notification draws both arrows switched off and answers both keys
  with silence. The ACADEMY is fixture-blocked (see below). On the galaxy map stop with the inspect
  cursor up the same chords still travel the cell.
  Turning the star-system page is ONE announcement and a seat in the new system's content since
  2026-08-22 — the double announcement and the `hud:view-title/scan` seat it used to inherit are
  fixed; what to expect now is `docs/test-recipes/systems-and-planets.md`, **Opening and leaving
  the star system page**.
- **The declared pairs and their chord labels.** MEASURED off `/gui/graph`:
  - star system, in the colony panel's stop: `system:colony/banner` (1 of 14), then
    "Previous system (Alt+Left Arrow), button, Navigates to the previous System in your empire"
    (`system:previous`, 2 of 14), then "Next system (Alt+Right Arrow), …to the next System…"
    (`system:next`, 3 of 14). The reading order is the NAME, then previous, then next — the arrows
    do not flank the name: the banner is a wide panel at x 32-250 and the arrows sit at x 256 and
    x 1204, one either side of the whole page. `Cells.EmitLinear` is faithful to that.
  - planet page: "Previous planet (Alt+Left Arrow)" / "Next planet (Alt+Right Arrow)", the game's
    own tooltips, 2 and 3 of 8.
  - notification popup: "Next notification (Alt+Right Arrow)" FIRST (1 of 5) then "Previous
    notification (Alt+Left Arrow)" (2 of 5) — the game draws them that way round.
  - end turn: "End Turn (Ctrl+Alt+E), button, Turn N".
  - academy: "Previous hero (Alt+Left Arrow)" / "Next hero (Alt+Right Arrow)", 1 and 2 of 2.
  `ChordNames.Of(ES2Access.ModEntry.Input, "ui.pagePrev", 0)` is the chord on its own, and the key
  names come out of the GAME's table (`%KeyCodeLeftArrow` = "Left Arrow").
- **"Galactic Map".** MEASURED: the galaxy map stop's context word is "Galactic Map" on every landing.
- **The Academy's strip arrows ARE declared** (`academy:previous`/`academy:next`, owner decision
  2026-08-22). Structure verified on a FORCED show (`Gui.GuiService.ShowWindow(GetWindow<AcademyScreen>
  (false))` — `ControlBanner.ToggleScreen("AcademyScreen")` does nothing in this save): both nodes
  appear in `academy:heroes` reading "Previous hero (Alt+Left Arrow), button, unavailable, 1 of 2" and
  "Next hero (Alt+Right Arrow), button, unavailable, 2 of 2". Behaviour is FIXTURE-BLOCKED: the save
  has no heroes, so the game keeps both arrows switched off and the page keys have nothing to move.
- **The election winner rows** are fixture-blocked: `GET /gui/graph?screen=screen.election` answers
  "screen inactive" on turn 21.
- **A stored walk that opens a group with Right changed meaning** with the single-press arrows: a `ui.right` that used to
  expand a group and stay now steps INTO it, so the old walk's "expand the card, then Enter" reaches
  the card's first child instead of the card. Re-record any stored route that opens something with
  Right before diffing it against a pre-batch baseline.

## Usage hints on the map

- **Map target + starlane (`[Beginner] test`).** Both fleets in Heka's branch are mid-free-move and
  declare `ControlTypes.Text` — Enter on them selects NOTHING, so the mod's own select route cannot
  be driven there. The route that works from `/eval` is the game's three calls IN ONE statement
  (splitting them leaves the cursor swapped and the panel unshown): find the `GalaxyFleet` in
  `IVisibleGalaxyFleetRepositoryService.GalaxyFleets`, then `ICursorService.Select(gf.CursorTarget)`,
  `ChangeCursor(typeof(GalaxyGarrisonCursor), gf)`, `Gui.GuiGameWindowService.
  RequestGalaxyOverviewViewLevel(gf.Fleet)` — speech says "Fleet panel open for …" and
  `FleetOrders.Selected().Count` reads 1. Close with `GetWindow<FleetsScreen>().HandleInput(
  InputAction.Exit)`. `FleetsScreen.AddGarrison`+`SelectGarrison` alone does NOT hold: the window's
  own refresh unselects everything while it is not shown, so the selection is gone a second later.
  With the selection up, every system node and every lane node ends its buffer with "Backslash to
  move the fleet here" + "Ctrl+Backslash to use off-lane free movement", and a LANE adds "Enter to
  deselect the fleet".
- **The fleet panel's own stops** carry "Ctrl+Enter to add to the selection" + "Shift+Enter to
  select up to here" on `fleets:line/<guid>` and `fleets:ships/ship/<id>`.

## Fixture-blocked

- A merged fleet lozenge (**Fog, labels and map marks**).
- An orbiting fleet of the player's own, an obliterator projectile and an ally coordination
  pin (**Map coordinates**).
- The hero detailed card's four-symbol row, the construction line's festival badge and the
  honor gauge's dossier (**Dossiers and tooltips**).
- An EMPTY expandable group, and the encounter-camera key check (**Keys**).
- The off-lane free-movement hint's NEGATIVE: all six of the player's fleets carry
  `FreeMovementSpeed` 0.8, and the property is descriptor-driven — a `SetPropertyBaseValue` write
  sticks in the base and the computed value stays put (**Usage hints on the map**).
- The empty-space half of the deselect hint: the mod declares no empty-space control, and
  `Deselect()` is reached only from `LaneClick` (**Usage hints on the map**).

Fleet-family recipes and their fixture-blocked items are in `fleets.md`.

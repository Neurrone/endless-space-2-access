# Star systems, planets and the management page

Opening a system, walking its cards and queue, the orbital and planet views, and the side
panels that hang off them.

## Opening and leaving the star system page

**Opening the star system page.** `GalaxyViewLevels.OpenSystem(Gui.PlayerEmpire.GetAgency
<DepartmentOfTheInterior>().ColonizedStarSystems[0].Node)` from `/eval` (Dusay, GUID 535 in the
fixture; `GameEntityGUID` is NOT in `Amplitude.Unity.Game`, so go through the node). The page
arrives in pieces — the side panels a frame or two before the planet cards — so a screen that
declared the half that existed seated the cursor on the wrong stop for good. Here the late
half (the cards) is the page's FIRST stop, so waiting for it is still right — but per the
tightened rule (making-screens-accessible.md §3), the gate protects the cursor's seat, not
the page: a page whose early half is usable declares it, and the planet page's lost-card
repair (`Nudge`) is the other half of that story.

**Screen model: the page is named after the system it is showing** (owner ruling 2026-08-22).
`ScreenName` is "Heka, System management" — the DRAWN system name (the rename button's own label,
`ColonyInfoSidePanel.SystemTitleLabel`, which the game writes for an outpost as readily as for a
colony) composed with the game's own word for the page (`%StarSystemManagementScanViewWindowTitle`)
through `screen.star-system-named` = "{0}, {1}". `screen.star-system` ("Star system") stays as the
fallback for a system with no colony panel drawn. Turning the page (Alt+Left/Right, or the game's
own arrows beside the name) is the reason: it never leaves the screen, so without the system in the
name the one fact the turn is FOR went unspoken.

**WHERE THE CURSOR LANDS, IN THREE CASES** (owner design 2026-08-29). A first entry — nothing
remembered, which is also every entry after a `/reload`, since that wipes the navigator's per-screen
state — lands on the **System information** stop, the page's own first stop: `DevProbe.Screen()`
answers `system:colony/banner` and the announcement is "System information, System, System level N,
button, … 1 of 15". Any LATER entry from the galaxy, and every **page turn**, put the cursor back
where the player was instead, so `InitialFocusStop` is only the last fallback. Everything but a
planet card comes back by key alone (`system:queue/header` means the same row of whatever system is
up, so the navigator's own reconcile finds it); a planet card is asked for BY POSITION — the same
card counting from the left, the same row of it — because its key carries the planet's GUID. The
fallbacks, in order: the planets stop's first row, then System information.

**Turning that page is ONE announcement and one landing.** The view level is re-entered with a new
node and the mod's gates ride that out (`IsActive` asks `LevelThroughTransitions`), so the screen
does not leave and come back. The screen notices its own system changing
(`SystemManagementScreen.Turned`), says the new name once, waits 30 frames for the game to rebind the
window, and then seats (`Restore`) with a 60-frame budget for the cards binding. Three things keep
that to one landing, and all three are load-bearing: the page declares NOTHING until it is whole
(`Whole` — cards drawn AND side panels drawn AND no turn in flight), the seat's own card is opened on
the build that declares it (`OpenCardBeingSeated`), and `BetweenViews` holds every announcement until
the seat lands or gives up. The old rough edge — a stray HUD line between the screen name and the
landing — is gone as of 2026-08-29.

**The star-system page's name and its page turn.** Entering (the `OpenSystem` route above, or Enter
on the galaxy's `…/management` node) announces "Dusay, System management" once and then one landing
line. `POST /input ui.pageNext` announces "Ita, System management" once and, about a second later,
exactly one more line. Check with `/speech?since=N` (**two** lines per turn — the screen name and the
landing; a third is the regression) and `DevProbe.Screen()`. Measured pairs, 2026-08-29: on card 0
slot 2 of Ita, `ui.pagePrev` lands on `system:planet/541/population/2` ("Slot 2 of 8") — the same
card, the same rank; on the rename button of Ita's card 0 (`/name/action/0`), a turn to Heka, whose
card 0 draws no rename button, falls back to `system:planet/533`, the planets stop's first row; on
slot 8 of an 8-slot world, a turn to a system whose card 0 has fewer slots falls back the same way.
The regression to watch for is a landing on `hud:view-title/scan`: it means a render was declared
while the page was half torn down, which is how the cursor used to be lost (see **Why the page
declares nothing while it is in pieces**, `planets.md`).

**Escape out of a view level** cannot be tested through `/input`: with no screen of ours
focused the injector's action is dropped before the game sees it. What the key reaches is
`StarSystemScreen.HandleInput(InputAction.Exit)` — call that to prove the destination, and
leave the key routing itself for the human test script.

**Leaving the planet page.** `ui.back` does NOT leave it under injection (two presses, still
`screen.planet`); `Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(node)` is the way back to
the galaxy, and `RequestStarSystemManagementViewLevel(node.GUID)` — the NODE's GUID — is the way to a
system page.

## The management page's permitted round trip

**The one permitted state round-trip on the management page**
is Enter on a cheap constructible to queue it and Enter on its queue line to cancel it — check `dust`
and the queue's names/order before and after (`ConstructionQueue.PendingConstructions`, indexed
never `foreach`ed). Queue two or three and the line becomes a drag source as well, which is how the
reorder is exercised inside the same round trip; the research queue is the same shape
(`DepartmentOfScience.ResearchQueue`, queued from the wheel's `research:suggested` stop in two key
presses). Both were run against a LIVE owner session and restored exactly.
**The colony panel's two banner clicks leave the page and come straight back.** `system:colony/banner`
is the game's own banner button and opens the empire summary at its systems tab;
`system:colony/level`, the badge in the banner's corner, opens the economy screen at its economy tab.
Both are silent on activation — the arriving screen announces itself — so the oracle is the window's
own `CurrentTabName` beside `DevProbe.Screen()` (measured 2026-08-29: `SystemsList` with the cursor
on "Systems Management, table, Dusay", `Economy` with the cursor on "Trading Companies"). Neither
screen answers `POST /input ui.back` — Escape there is the GAME's — so close them from `/eval` with
`Gui.GuiService.HideWindow(...)` when the desktop cannot supply a real key press.
**That round trip has a SPOKEN oracle since 2026-08-19**: Enter on "Interplanetary Transport
Network" answers *"Queued Interplanetary Transport Network"* while `ConstructionQueue.Length` grows,
and Enter on its queue line answers *"Cancelled …"* with the ABBREVIATED title the line draws
("Interplanetary Transport N." — ES2 facts). ITER ("Cannot afford the resource cost") is the
fixture's ready-made REFUSAL control on the same panel. **The confirmation branch has no fixture
here**: all seven of Dusay's constructibles report `NeedsConfirmation = false` (nothing is invested),
so the game's own message box — and the suppressed outcome line that goes with it — is code-verified
only. The home planet is
`IsUnique` so planet rename is unreachable; `StarSystemPopulationModalWindow`'s opener is
tutorial-locked; and at turn 3 no buy-out button is drawn at all
(`BuyoutTechnologyNotUnlocked` — ES2 facts), so the queue line's buy-out children have no fixture.
**The rest of the management page's blocked list**: a FOREIGN empire's outpost card (no save has
one; its population rows are sighted by lending — **Sighting ANOTHER empire's colony card**); an outpost
action running PAST its start turn (the disabled cancel); the regress/stagnant/complete captions and
the Hisshos wording; the `Discard`-hidden faction actions (Hisshos/TimeLords/Vodyani); buy-outpost;
and, last checked turn 1, hangar ships, colonize, the ghost panels, and the rebellion and migration
rows. `EmigrationGroup`/`ImmigrationGroup` — the growth line's two count-only siblings of the
outposts readout — are drawn by neither save, so nothing has been invented for them.

**The constructible filters are a safe round trip.** They are one select-one group the panel
re-derives from `SelectedConstructibleFilterName` on every refresh, so Enter on another filter and
Enter back on "All" leaves the fixture as found — nothing about the system or its queue moves. The
grid under them changes with the pick, which is the cheap proof the pick landed.

**The management-view node's negative control.** "Unowned systems gain nothing" is only proved by a
system whose label button is VISIBLE and inoperable — an invisible button passes for the wrong
reason. Sweep the labels with
`GetWindow<StarSystemLabelsWindow>(false).GetComponentsInChildren<StarSystemLabel>(true)` and print
`StarSystemNode.LocalizedName`, the `RequestManagementViewButton` visibility chain, `.Enable`, and
the gate under test; only the camera's own system and its neighbours read `vis=True`. Expanding a
system ZOOMS to it, so two systems expanded at once is not an A/B: the one the camera left stops
drawing its label and loses its `Manage system` child.

## Orbital and planet cards

**Curiosities on the orbital cards are nearly fixture-blocked.** A card draws them only for a
system the empire has SURVEYED, and the model scan (`new GuiPlanet(planet).GetRemainingCuriosities
(Gui.PlayerEmpire).Count` over `Gui.Game.Galaxy.GameNodes`) finds exactly one reachable card in each
save: the owner's `[User] bug session` (turn 10) has **Ita III** with one ("Ruins", item rect
≈ `735,634,24,24` inside the card at `633,520,128,140`), and `[Beginner] test` has **Primus V** with
one. No explored system in either save has a planet with TWO, so the plural line has no fixture -
resolve it with `ModStrings.Plural(...GalaxyPlanetCuriosityOne, ...GalaxyPlanetCuriosities, 2)` and
say so. Crop the card with the cursor on a DIFFERENT planet: focusing it points the pointer at the
card and the dossier tooltip covers the ring. The painted gate is proved by parking the item the way
the engine does - set the item's `Alpha = 0f` from `/eval` (it sticks; the card does not refresh
every frame), re-crop, re-read, then set it back to `1f`.

**Walking a card's population slots.** The card's ring is a row per SLOT in up to three regions
("Population", "Overpopulation", "Locked" — the game's own words, so grep the transcript for the
localized ones, not for mod keys). Expand the card with `/input ui.right` and walk down: the region
name is heard on band entry only, and each row reads "Slot N of M" plus the affinity, or "Empty slot
N of M". A FILLED slot in the overpopulation band is a GROUP whose "Tooltips" region holds one child,
the arc's sentence; every other slot is a leaf. **The slot dossiers are the mod's own carriers**
(nothing on the drawn ring carries a tooltip — ES2 facts), so their evidence pair is
`DevProbe.TooltipDelay(0)` → focus the row → `DevProbe.Tooltip()` for the words and
`Gui.GuiService.GetWindow<GuiTooltipWindow>(false).AgeTransform.GetGlobalPosition()` +
`crop-shot.ps1` for where the window drew: it must land on the ring's own rect, ABOVE it, not at the
screen's bottom-left corner. The crop is also the only oracle for the ARC — the same crop shows the
orange sector, and it must cover exactly the ranks the "Overpopulation" region holds.
The **Locked** band needs a colony holding MORE units than its current maximum, which no save here
produces; it is covered by `ES2Access.Tests/UI/PopulationSlotsTests.cs` instead.
The **pick-up** on a filled slot needs somewhere on the page to put the unit down, which since
2026-08-29 is EITHER a second colony of the player's in the system OR a drawn spaceport panel (below,
**Moving population between planets**) — a one-colony system with a port carries, and only a
one-colony system with no port leaves every slot row read-only. The page's own Space claim is what a
probe there reads: `Claims("Space")` is true on every node of this page and `ui.carry` answers
`consumed` in SILENCE, so a read-only slot row is told from a pick-up row by the silence and by the
row's own readout, never by the claim.
**How much a slot picks up is its RANK**, and the walk is the only way to see it: within one
affinity's contiguous run the first row carries the whole run and the last carries one, so a run of
four reads "Space to drag Imperials x 4." / "x 3." / "x 2." / "x 1." down the rows, and the
pick-up says the same figure ("Dragging Imperials x 4. Enter to drop, Escape to cancel."). Grep the
buffers for `to drag` after expanding the card: the counts must descend inside each run and restart
at the next affinity. **A population drag states its count even at ONE** ("Dragging Imperials x 1",
owner ruling 2026-08-29) so the last row of a run reads like its neighbours rather than like a
different kind of answer — a bare "Dragging Imperials" there is now a defect. That rule is the
DRAG's and population's alone: an ordinary readout still drops the singular, and a module, a ship, a
queue line or a tactic card must never read "x 1".
An UNCOLONIZED card's ring is the same walk with a shorter answer: every row reads a plain "Empty
slot" and takes its number from the region ("Empty slot, 3 of 6"), because there is only the one
"Population" band and the rank and the position are the same figure — no dossiers and no pick-up
either. Any star-system page with a
world nobody has settled shows it, and a page holding both kinds walks the two shapes in one pass.
The count oracle is one `/eval` walk of
`PlanetPopulationEnumeratorSimple.PopMarkersContainer.Children` counting `Visible && Alpha > 0`
(the pool holds a couple of extra retired children on each card), and it must equal both the row
count and `Planet.MaxPopulation`; the picture oracle is a `crop-shot.ps1` of that container's rect,
where the grey arc segments must number the rows.

**A planet card's deposit lines and their dossiers.** A drawn deposit reads
"&lt;resource&gt;: &lt;amount&gt;" in the card's buffer and gains a child in the card's "Tooltips"
region named for the same resource — the name is on the tooltip's wrapper and NOT on the item (ES2
facts), so a bare number in the buffer is the regression to grep for. The painted gate is checked in
both directions on one page: a world WITH deposits draws its items at alpha 1, and a world with none
keeps the previous planet's items parked at alpha 0 still holding that resource, which must
contribute neither a line nor a dossier. The dossier is Class-backed (`ResourceDeposit`), so its
words exist only once the node is FOCUSED: walk to it, then `DevProbe.Tooltip()` plus
`/gui/graph?buffers=1` for the pair.

**Stepping between planets on the planet overview** re-enters the SAME view level with a new
planet: `Gui.GuiGameWindowService.CurrentGalaxyViewLevel` (what `GalaxyViewLevels.Level` and
`At<T>()` read) goes NULL for a few frames while it happens, and the window unbinds its planet.
A screen gated on either pops and re-pushes on every step. `GalaxyViewLevels.LevelThroughTransitions`
is the view's own answer and does not blink; gate on that and declare nothing while the window
is empty (an empty `Build` leaves the cursor untouched — `KeyGraph.Rerender` returns false).

**Reproducing a show the game lost** (how a repair for a dropped deferred reveal is tested without
re-walking the route that races). Hide the panel the way the game's own unbind does and DON'T unbind
it: `Gui.GuiService.GetWindow<PlanetScreen>(false).PlanetLabel.Hide(true)` leaves the exact stuck
signature — planet still bound, `Shown`/`Showing`/`Hiding` all false, window still shown and ready.
The repair window is short (~20 frames), so fire the hide and the `/gui/graph` dump as two curls in
ONE bash command to catch the missing-stop state; then `POST /wait` on
`…PlanetLabel.Shown` and dump again for the rejoined stop. `/log?grep=` on the repair's own log
line is what proves it fired ONCE rather than every frame. Costs the owner one visible blink of the
card. The racy entry ITSELF — walking galaxy → orbital → Enter until the race lands — stays on the
manual script, because reproducing it costs the owner's camera.

**Auditing the planet card needs a planet with FEWER lines than the one before it.** In
`unlocked`, Xiu's planets are the pair that catches the pooled-row trap: open Xiu I (two climate
lines) and then Xiu II (ONE, plus a curiosity) with
`GalaxyViewLevels.OpenPlanet(…StarSystemNode.Planets[i])`. The card's climate table then holds a
faded leftover sitting on the curiosity's rect, so the audit is `/gui/graph` against a
`crop-shot.ps1` of the card (rect ≈ `960,290,300,240`) rather than against a `/gui/age` dump —
the dump prunes nothing here but shows no alpha, and only the crop says which lines are on the
screen. Raia (planet 2) is the unique one, and the only planet that draws the lore paragraph and
the "Unique Planet" subtitle. Deposits, anomalies and depletion have no planet anywhere in Xiu.
In `[Beginner] test`, Raia (Next planet from `Planets[0]`) draws THREE population kinds — the
fixture for the populated population panel; Dusay I draws only the summary.

**A tech-blocked Colonize, for testing the missing-technology jump**: `unlocked` has three on the
star system page (`PlanetLabel_SystemManagement.ColonizeButton` on Xiu I, Xiu II and Xiu IV all
answer `Gui.IsHintActive` true; the hint's technology is the wheel's own `GuiTechnology`, e.g.
"Maximized Exploitation" on Xiu I). Expand the planet card and the node is the card's own
`.../action/0`. **The jump cannot be proved by injection**: `GuiButtonHint.ActivateHint` (:18-34)
tests `Input.GetKey(LeftControl)` and no injected action holds a key, so `Gui.ActivateHint(t)`
answers `False` from `/eval` too. What IS provable headlessly is the WIRING — reach the focused
node through the loaded-from-bytes assembly (`ModEntry.Navigator.CurrentNode.Vtable.OnSelectToggle`)
and check it is non-null; the keystroke half belongs on the manual script.

**What the beginner fixture cannot show on the orbital cards**: neither uncolonized planet's
Colonize button is offered — both are tech-blocked, and the game leaves a blocked button
`Visible` AND `Enable` while turning its click into "jump to the missing technology", so
`Gui.IsHintActive(button.AgeTransform)` is the only thing that tells them apart: gate on it, never
on `Enable`. Buy-outpost, minor faction, pirate lair and all five `SecondaryButtonsTable`
buttons are undrawn (measured: `Visible=false`, `Enable=true`, on every card — `Enable` says
nothing here); the whole table is hidden, because every `Refresh*Status` returns before showing
its button when no fleet in the system offers the action, which means no Behemoth in the beginner save. The
one anomaly in the fixture is Multiple Moons on Dusay II. Those five buttons carry CLASS
tooltips and so have no short name on the card — but the game DOES name each of them on the
fleet action it carries out: `%InitiateTerraformPlanetFleetActionTitle`,
`%InitiateRestorePlanetFleetActionTitle`, `%InitiateReduceAnomalyFleetActionTitle`,
`%LaunchMiningProbeFleetActionTitle`, `%DestroyPlanetFleetActionTitle`. Grep the corpus for
`FleetActionTitle` before reaching for `ModStrings`.

**Forcing the orbital card's fixture-blocked widgets** (structure only, never content). On a card
whose `Planet` is bound, set `InProgressTerraformationButton.Visible/Enable = true` plus a plain
`AgeTooltip.Content`, and the same for `InProgressRestorationButton`,
`InProgressAnomalyReductionButton`, `PirateLairGroup` (plus its `AgeTooltip.Content`),
`OutpostCancelIcon.Visible` and `HauntIcon.AgeTransform.Visible/AgeTooltip.Content`. The mod then
declares "Click to cancel the action" and "A Pirate Lair is orbiting this Planet" as action children
and the two icon sentences as buffer lines. **Restore by hand**: the card's own refresh puts the
Visible flags back but NOT a Content you overwrote — write `PirateLairGroup.AgeTooltip.Content` back
to `%PlanetPirateLairDescription` and `HauntIcon`'s to empty, then set `card.Dirty = true`.

**Drawing the three in-progress juggernaut buttons on a save with no juggernaut.** A forced show
alone is not enough any more, because their names come off the wrapper their tooltip points at.
Find the card (`GetWindow<PlanetLabelsWindow_SystemOrbital>(false)` →
`GetComponentsInChildren<PlanetLabel_SystemOrbital>(true)`, matched on `card.Planet.LocalizedName`),
LEND each button a real wrapper — `InstantiateIGuiConstructible` over a `PlanetTerraformationDefinition`
found in the `ConstructibleElement` database, over `Databases.GetDatabase<AnomalyReductionDefinition>()
.GetValues()[0]`, and `new GuiEntityAction(<InitiateRestorationEmpireActionFleetActionDefinition>,
CategoryFleetAction)` — writing `Class`, `Target` and a `Content` of
`"%PanelFeatureRemainingTurnsTitle" + " N\n" + "%PlanetCancelJuggernautActionButtonDescription"`, then
set `button.Visible = true; button.Enable = true`. **Their parent `SecondaryButtonsTable` is hidden
too** and the walk gate is the ancestor chain, so `button.AgeTransform.Parent.Visible = true` is the
step that makes them nodes at all (measured: without it only `InProgressRestorationButton`, which
hangs elsewhere, appeared). Restore with `AgeTooltip.ReleaseData()` on all three, `Visible = false`
on each and on the table. Verified 2026-08-23: "Terraform To Arctic" / "Restore planet" /
"Reduced Ice-10", each with the shared cancel sentence and "Remaining turns: N" in its own buffer.

**MINING PROBES on a planet row** (2026-08-16, not part of the scanner): the galaxy map's orbital
cards and the empire screen's planet cards both say
`ES2Access.UI.MiningProbes.Line(planet)`. Fixture-blocked — `DepartmentOfTheTreasury.miningProbes`
is empty for every empire, and `Line` returns null for all three Dusay planets, which is the proof
that nothing changed in the fixture's rows. The shared-path evidence for the positive branch is the
game's own text, read from `/eval`: `Gui.Localize("%PanelFeatureMiningProbeDescription",
GetLeaderName(...))` gives `#1E6EC8#[terrans] Neurrone#REVERT#'s Mining Probe is currently mining
the Resource deposits of this planet`, which `AgeText.Clean` speaks as
`Imperials Neurrone's Mining Probe is currently mining the Resource deposits of this planet`; the
owner-gated half formats as `+3.7` per symbol and `12 Turn` for the countdown.

**The planet card's anomaly rows are where "a card's tooltip is rarely on the card" was measured**
(the rule is in `dev-loop.md` §2). Pointing at an anomaly ROW draws nothing — the tooltip hangs off
a child inside it — while the node still declares the tooltip and its buffer stays empty, which
reads exactly like a mis-declared node. Aim `PointerFocus` at `tooltip.AgeTransform` instead, and
prove it with `DevProbe.Tooltip()`: neither `/gui/graph?buffers=1` nor a screenshot distinguishes
the two cases. The same rows bite on the reading side — a group walk asking `widget.AgeTooltip`
gets silence, because the words are on the component's own Tooltip field.

## Moving population between planets

**The population side panel's rows are real nodes** (owner ruling 2026-08-22, retiring the batch-2
compromise): a population entry ("Imperials, 3") is an expandable group whose "Tooltips" region holds
the political parties nested in its dossier (`Cells.Declare` gives a cell a subtree; `PoliticsDossier`
supplies the parties). They were the row BELOW their population until then, because a side panel emits
a flat list of cells and a cell could not open a subtree.

**Moving population about** (management page). The drag is offered where the GAME's own drag has a
target: a second colony of the player's in the system, OR a drawn spaceport panel
(`PopulationMoves.OnSystemPage`, mirroring `PlanetLabelsWindow_SystemManagement.StartDrag`). A
one-colony system with no port declares every slot row read-only and there is no pick-up — `ui.carry`
on it is silent, and since 2026-08-26 it is `consumed` rather than `unconsumed` with
`Claims("Space")` true, because the PAGE claims Space on every node of itself
(`docs/interaction.md`).

**Who advertises, mid-drag, is the game's own answer** (`PopulationMoves.Accepts` fills `DragInfo`
with `DragInProgress` FALSE, asks `CanAcceptPopulationDrop`, restores). The evidence pair is one
`/gui/graph?buffers=1` taken while something is held: every slot row of the SOURCE planet carries
neither "drop target" nor a drop hint (the game excludes `SourcePopulationOwner`), while another
colony's slots carry both ("drop target" in the readout, "Enter to drop ⟨thing⟩." last in the
buffer). Take the same dump with nothing held and the filled rows carry "draggable" and the pick-up
hint instead — the two words are never both on one row. The card header carries none of it in any
state.

**THE DROP TARGETS ARE THE SLOTS, NEVER THE CARD** (owner ruling 2026-08-29). An EMPTY non-locked
slot is the plain add, an OCCUPIED slot is the game's SWAP (the row's own affinity becomes
`ReplacedPopulationAffinity`), a LOCKED slot takes nothing, and the card's group header carries no
`DropKind` at all. The mouse does accept a drop anywhere on the card's rectangle; the keyboard does
not, because a header that also swallowed drops made two rows out of one gesture. So the mid-carry
dump must show the card header WITHOUT "drop target" and without a drop hint while its slots have
both, and Enter on the header must still do the card's own click (it opens the planet overview page —
which also ends the carry, since the player has left the page). A full planet then offers only its
swaps and a planet with room offers its free places, which reaches every outcome the mouse reaches.
Verify a swap by per-affinity count probes on both ends, never by speech alone:
`Spaceport.GetPopulationCountByAffinity(...)` and `ColonizedPlanet.GetPopulationByAffinity(...).Count`
before and after. A same-affinity swap is a legitimate net-zero — dropping Imperials onto a filled
Imperials slot bounces one out and puts N back — so a probe that does not move is the RIGHT answer
there, not a failed drop. A planet-to-planet swap needs a system with two colonies of the player's;
where no fixture has one, that half is blocked and the port's swap (the bounce branch) is reachable.

**What a successful drop SAYS is what really moved, not what was carried** — the spaceport clamps
instead of refusing, so a carry of four onto a port with three free slots answers "Moved Imperials x
3 to Spaceport" and the port probe reads 3. That mismatch is the check: carry a run longer than the
free room and the spoken figure must equal the probed delta.

Also testable with no second colony: push a drag by hand —
`ES2Access.ModEntry.Carry.PickUp(new ES2Access.Core.UI.CarryItem(pop, "Imperials", "population",
3), ES2Access.ModEntry.Navigator.Screen)` — and watch `/input ui.activate` on a card that refuses
answer in the mod's fallback words with the drag kept, `ui.carry` anywhere that is not a source
answer silently with the drag kept, and `ui.back` answer "Cancelled drag". **`ui.carry` on the
control the thing came from is NOT a cancel** (2026-08-29): it re-picks and re-announces, so a
put-back expectation in an old script now fails as a live drag.
**Sighting ANOTHER empire's colony card**, which no save puts in a player system (an enemy outpost
on a free world of one). Lend a card the foreign colony, all inside ONE `/eval` so no frame draws it
and the card never `Refresh`es with somebody else's colony bound: take the card from
`GetWindow<PlanetLabelsWindow_SystemManagement>(false)` →
`GetComponentsInChildren<PlanetLabel_SystemManagement>(true)` matched on `Planet.GUID`, save
`card.ColonizedPlanet`, set it to an AI empire's real `ColonizedPlanet`
(`empire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems[i].PlanetsColonized[j]`),
`card.PlanetPopulationEnumeratorSimple.Bind(card.Planet, foreign, card.Client)` + `RefreshNow()`,
read, and restore all three in a `finally` plus `card.Dirty = true`. Read it with
`ES2Access.Dev.GraphDump.Dump(false, true)` — the public static behind `/gui/graph`, so the whole
lend-read-restore is one statement. The card must be EXPANDED first (`/input ui.right` on it; a
`POST /reload` clears expansion state). Measured 2026-08-27 on Dusay I lent Leaper's Husk (4/9,
safe 6): nine rows, "Cravers" in slots 1-4, a Population band of six and an Overpopulation band of
three, `drawnMarkers=9`, `OverPopulationSector.Visible=True`. Class-backed dossiers still read
EMPTY in the dump (they need a drawn tooltip window), so prove them off the CARRIERS instead —
sweep `FindObjectsOfType<AgeTooltip>()` for `ES2Access.UI.ScratchTooltips.Owns(t) && t.Class ==
"Population"` and print `Content`, `Target`, `((GuiPopulation)Target).PopulationEmpire`. The
pick-up half stays fixture-blocked here for the same reason the drop is (one colony per system, so
`canCarry` is false on every card); what IS distinguishing live is `DropKind` on the card node —
`population` on the player's, null on the foreign one, read off
`ES2Access.ModEntry.Navigator.InspectRender().Order[i].Vtable`.

**Sighting the spaceport side panel**, which no save draws (ES2 facts: `IsAvailable()` needs
`MaxPopulation > 0`). Show it with `Gui.GuiService.GetWindow<SidePanelsWindow>(false).ShowSidePanel(p)`
— `SidePanel.Show` itself throws with a message telling you so — and the side-panel sweep declares it
at once. To make its population ROWS exist, lend it real data:
`p.SpaceportPopulationEnumerator.Bind(colonizedPlanet, p.gameObject)` + `RefreshNow()` draws that
planet's markers in the spaceport's slots, and `Bind(p.Spaceport, p.gameObject)` + `RefreshNow()` puts
it back. That proves the rows, their words and the pick-up — **never Enter on a
planet card while the binding is lent**, because the drop would move real population. Do not press the
destination button either: it opens `SystemSelectionModalWindow`.

**Walking the spaceport panel** (where the save DRAWS one — `Spaceport.IsAvailable()`). The stop is
called **"Spaceport"** (the mod's word carrying the game's title; without it the stop was named by the
header icon's whole sentence), and its reading order is the panel's: the title line, then a row per
drawn MARKER in slot order, then the destination line and its button. The markers are banded like a
planet ring — filled and empty under the game's "Population" title, locked under its locked-slot title
— and each row is pointed at its OWN marker, so its buffer is the sentence the panel wrote there
("This slot is empty and can be used to transfer a population unit to another star system" /
"This population is scheduled to leave the star system" / the locked one, which names the capacity the
next system level buys). Only those three sentences are accepted; a marker the panel has not refreshed
still holds the prefab's "This is changed by code", which reads as no tooltip at all.
A filled row picks up at its rank and takes a swap; an empty row takes a plain add; a locked row is a
readout with no drop. **Every one of those gates asks the CLAMP rather than a hand-written room
test** (`PopulationMoves.IntoPort`), which is what stops a row advertising a move that would carry
nobody: an empty row needs a free slot, and a swap row works on a FULL port only while the source
planet has room to take the bounced unit — a full port plus a full source planet moves nothing and
must therefore say nothing. The mirror of that on the planet side: a port-sourced drop onto a FULL
planet is refused by the mod even though the game's own `CanWelcomeSomeOfPopulation` accepts it,
because the spaceport's client never performs the swap that acceptance is predicated on. **There is no port-to-port move**: with a port-sourced unit held, no port row
says "drop target" and Enter on one answers the mod's refusal with the drag kept and the counts
unchanged (prove it with a count probe, not with silence). **A port-to-planet drop onto an OCCUPIED
slot IGNORES the swap** — the spaceport's own client posts one order and never reads
`ReplacedPopulationAffinity` — so the target affinity's count must be unchanged on both ends while the
carried people arrive (measured 2026-08-29). A LOCKED port slot needs a port holding more than its
maximum, which is not reachable by filling a port: the enumerator counts empties up to the maximum
exactly, so a level-2 port of capacity three draws three markers and no locked one.
**`PlanetPopulationEnumerator.CanAcceptPopulationDrop()` THROWS when no drag is in progress**
(`DragInfo.TransitingPopulation` is null), so it can only be called with `PopulationEnumerator.DragInfo`
filled in — and it is a static, read every frame by the enumerator's own refresh, so clear it in a
`finally` or a marker the player is still looking at reads as already gone.

**Sighting a card's SANCTUARY band** (no save has Penumbra; worked 2026-08-29 on `[Beginner] test`,
Dusay). Take a card that HAS a colony, set `PlanetLabel`'s auto-property backing field
`<GhostColonizedPlanet>k__BackingField` (reflection, `BindingFlags.Instance | NonPublic`) to that
card's own `ColonizedPlanet` — a lend of real data, and one the player's own empire owns, so the
band's own branch draws everything — then
`GhostPopulationEnumeratorFocused.Bind(card.Planet, colony, card.Client)`, `.Show(true)`, and
`card.RefreshNow()`. Expand the card and read the stop. The band declares "Your Sanctuary" (buffer:
the captioned population fraction, then the five outputs, then the panel's own sentence), a
`…/ghost/population/<band>` region of slot rows that carry and take drops through the SAME machinery
as the world's ring, and the traitor button. Restore in one statement: `GhostPopulationEnumeratorFocused.Hide(true)`,
`GhostFidsiGroup.Hide(true)`, `Bind(planet, null, client)`, put the backing field back to
`card.Planet.GhostColonizedPlanet` (the real value, normally null), `RefreshNow()` — then confirm
`GhostGroup.Visible` false on every card and grep the graph for `ghost` (must be 0 hits).
**The ring is hover-gated**, so a probe that never puts the pointer in the band sees no slot rows at
all; the mod's own focus does put it there (the band's title is what it aims at), which is why the
rows appear once the cursor is on "Your Sanctuary" and not before. What the lend CANNOT prove is
content — a real ghost's figures, its rival variant, and any refusal the traitor button would give.

**Sighting the SANCTUARY LINKS panel** (`ShipsSpawnPointSidePanel`, same DLC block — its gate wants
`Empire.HasGhostSystems`, but the prefab is base game). One `/eval`: take the drawn
`ColonyInfoSidePanel`, lend its `ContainerPanel` to the spawn panel if that is null, `Bind` it the
colony panel's own `ColonizedStarSystem`, `InternalShow()` (never `Show` — `SidePanel.Show` throws),
`RefreshNow()`. Four rows read; to see the two CLEAR buttons as well, set their
`AgeTransform.Visible = true` after the refresh and dump before the panel goes dirty again — six
rows. Restore with `Visible = false` on both, `InternalHide()`, `Unbind()`, then diff the whole graph
against a dump taken before the lend (only the focus marker and the clock should differ).

**The bottom panels' expand button is NOT in the tree** (owner ruling 2026-08-29) — see
`docs/planets.md`. Nothing to walk; what a run should check is that `DevProbe.Coverage()` counts it
`inert` rather than listing it under `actionsUncovered`. Pressing the game's own button from `/eval`
writes the PERSISTED `IGuiOptionsService.ExpandSystemPanels`, so a probe that presses it must press
it again and confirm the option and the three `GuiFrameExpander` heights came back (177 collapsed,
292 expanded on a 1920×1200 window).

**The page OPENS those panels on arrival**, for whoever is watching the screen (owner request
2026-08-29, `SystemManagementScreen.ExpandBottomPanels`): entering the page toggles the game's own
expanders and sets the option, once, on the frame the page arrives. Silent, and it declares nothing.
The test is visual and needs a leave-and-return, because it runs on ENTRY only: collapse with the
game's own button from `/eval`, leave with
`Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(node.GalaxyPosition)`, come back with
`GalaxyViewLevels.OpenSystem(node)`, then check `ExpandSystemPanels` true and all three frame
heights 292, and `crop-shot.ps1 -Rect 385,880,900,315` for the sighted half (collapsed shows two
rows of constructibles, expanded four). Collapsing while the page is UP must stick — the entry
latch has already fired — which is the second half of the check.

**The four information side panels are ONE stop with four regions** (owner design 2026-08-29;
`docs/interaction.md`). A colony page has 11 Tab stops, not 14. Walking them: Tab to
`system:side` lands on the colony banner announcing "System information, System, System level 2, …";
`ui.regionNext` three times steps Population → Representatives → Governor, each announcing its own
name and landing on its first row, and a fourth is consumed silently at the last region;
`ui.regionPrev` walks back the same way. Up/Down still walks every row of all four panels in drawn
order — the seam is invisible to the arrows, and a dump diff against a pre-merge capture shows the
node ids and row text unchanged, with only the stop/region lines moved and the new stop name added
to the first row. The SPACEPORT is still its own stop after them, and its carry is unaffected
(carry from a port slot with a planet card expanded: all eight planet-ring slots read "drop
target").

**The Tab order, top to bottom** (owner design 2026-08-29 — what the system IS comes before what is
in it). Walking it with `FocusStop("hud:empire")` then eleven `ui.next`, the spoken order on a
colony page is: Hud (the empire banners, whose first row is Controls) → System management scan →
**System information** → **Spaceport** → **Planets** → Constructibles → Construction queue → Hangar →
Quest → Notifications → End Turn, and the twelfth wraps back to the empire banners. The stop keys in declaration order are
`hud:empire`, `hud:view-title`, `system:side`, `system:side/SpaceportSidePanel`, `system:planets`,
`system:constructibles`, `system:queue`, `system:hangar`, `hud:quest`, `hud:notifications`,
`hud:turn` (`system:page`, the traitors-mode toggle, sits between `hud:view-title` and `system:side`
on the rare page that draws it). The move that put the panels first was a pure reordering: a sorted
diff of `/gui/graph?buffers=1` before and after holds no node id and no buffer line either side, only
the cursor marker and the clock. Entry still does NOT land on the planets — see **WHERE THE CURSOR
LANDS, IN THREE CASES** above.

## Working an outpost

**Working an outpost** (`[Beginner] test`, Rigel). Open it with
`GalaxyViewLevels.OpenSystem(...ColonizedStarSystems[1].Node)`; entering pops "INTO A FOREIGN LAND"
and leaving pops "Dangerous Visions" — re-minimize both (`docs/test-recipes/fixtures.md`).
The permitted round trip, LAST in a run: Enter on **Merchants and
Money** starts it and Enter again the SAME turn cancels it with a refund — probe
`DepartmentOfLabour.EntityActions` (index it, never `foreach`) and
`Gui.PlayerEmpire.GetPropertyValue(SimulationProperties.Empire.BankAccount)` before and after
(measured 253.81 → 103.81 → 253.81). **Decolonize**: Enter raises the game's own confirmation, which
must SPEAK; answer Cancel and check
`...ColonizedStarSystems[1].IsScheduledForDecolonization` — never Confirm. `POST /loadsave`
afterwards regardless.

## The assigned-governor side panel

**The assigned-governor side panel** is measurable without a save that has a governor, and the
CHEAPER of the two routes is the one that gives real words. No fixture has a governor — in
`unlocked` the empire holds one system (Xiu) with `AssignedHero` null and one unassigned hero in the
academy (`DepartmentOfEducation.ActiveHeroes[0]`, Dmitri Lenko). Write that hero into the panel's
private `privateAssignedHero` by reflection and set `Dirty = true`: `Refresh` (:157-240) then binds
the whole assigned variant — portrait dossier, affinity and class dossiers, experience gauge — from
a real `GuiHero`, and nothing in the simulation is touched (the system's own `AssignedHero` stays
null). Put it back with the same field plus `Dirty`, or `POST /loadsave`. The older route — flipping
the `Visible` flags `Refresh` writes — proves the variant's SHAPE only: the unassigned prefab holds
STALE hero text and every class-backed tooltip has a null target, so nothing draws.
`HeroInformationGroup` holds four children (name, affinity icon, gauge, class icon) and the two
ICONS never appear in a `/gui/age` dump — that route prunes a subtree with no text and no *readable*
tooltip, and theirs are class-backed with empty content, so their existence is an `/eval` walk of
`.Children` or nothing.

## Usage hints on the star-system page

- **Curiosities are ALL refused in `[Beginner] test`** — the empire's Expedition Power is 2 and
  every curiosity on the map needs 3. **This is not the same gate the scanner's `Explorable (6)`
  column counts**: that column asks `Curiosity.CanBeSearched(empire, null, failures)` with NO fleet,
  and the `Insufficient Expedition Power (10)` column is the `EmpireExpeditionPowerTooLow` failure it
  records (`docs/test-recipes/scanner.md`). Which of the two the card's own Enter consults has not
  been re-measured — name the gate before quoting either count against the other.
  To run the queue-then-cancel round trip, grant the game's own
  descriptor: `Databases.GetDatabase<Amplitude.Unity.Simulation.SimulationDescriptor>().GetValue(
  (StaticString)"EmpireImprovementCuriosityLevel2")` → `Gui.PlayerEmpire.AddDescriptor(d, true)`,
  then `Refresh(true)` in a SECOND `/eval` (the value reads 2 in the same statement and 3 on the
  next). The pooled `PlanetCuriosityItem`s do not re-`Refresh` on their own — even across closing
  and reopening the star-system page — so invoke their private `Refresh()` by reflection over
  `FindObjectsOfType<PlanetCuriosityItem>()`; that also pops the "Studying Curiosities" tutorial,
  which needs the usual minimize. Undo with `RemoveDescriptor(d)` + `Refresh(true)` + the same
  reflected `Refresh()` sweep, and check `enable=False` came back.
  Measured round trip on Dusay (system node GUID 535, `RequestStarSystemManagementViewLevel`): Enter
  on `system:constructible/StarSystemImprovementIndustry2` ("Queued Interplanetary Transport
  Network") → `ui.alternate` on the curiosity `system:planet/536/action/1` → the queue reads
  `0=CuriosityExpeditionSignal 1=StarSystemImprovementIndustry2`, i.e. the head. The alternate is
  SILENT (no "Queued … as first item" — the curiosity's own Enter is silent too). Cancel both with
  Enter on their `system:queue/<guid>` rows.
- **A live `GuiButtonHint` host in `[Beginner] test`**: Dusay I / Dusay II on the star-system page —
  expand `system:planet/536` and its Colonize action reads "unavailable, Missing technology
  Maximized Exploitation" and ends with "Ctrl+Enter to show missing technology". The HUD's
  tutorial-disabled Empire Summary and Hero Management buttons are NOT hint hosts (they say
  "disabled during this part of the Tutorial" and carry no hint line), which is the negative worth
  keeping.

## Fixture-blocked

**What the beginner fixture cannot show on the planet overview** (last checked turn 1): curiosities,
resource deposits and the depletion row (no planet in the fixture has any), and the population
entries' click — the game opens `PopulationModalWindow` there, and the entries are declared
read-only per the approved design.

**The planet constructible panel has no fixture either.** Corrected 2026-08-29: it has THREE
hosts, not one, and three openers, not two — the card's Build Infrastructure button opens it as
well, on the system-management page, on the orbital page and on the EMPIRE page's own cards panel
(`StarSystemPlanetCardsPanel.OnClickBuildInfrastructure` :278-288). None of them is reachable in a
fixture, but for different reasons: the ORBITAL window's Terraform and Reduce Anomaly buttons
(`PlanetLabelsWindow_SystemOrbital.OnTerraformPlanet` :255-265, `OnReduceAnomaly` :285-295) are
never drawn without a Behemoth in the system, while Build Infrastructure is drawn everywhere and
always DISABLED, because no save has an available planet improvement. Forcing the button and
reading the (empty) panel on both the management and the empire routes is in
`empire-screens.md`, "The specialization list from a planet card". What IS testable offline:
`screen.planet-constructibles` registers (`/gui/graph?screen=…` answers "not active"), and its
predicate reads false at the galaxy overview, at the orbital zoom step with the cards drawn, and
on the management page. Opening it from `/eval` is not worth it: `ShowConstructiblePanel` is
private and indexes `fleetByActionDefinitionDictionary`, which in the beginner save holds no fleet.

**The system-discovery cutscene has no fixture at all.** It only runs on a system's FIRST
visit (`GalaxyViewLevel_SystemDiscovery.CanBeActivated`: explored, visible, planets-visible,
not already discovered), so reaching it means exploring — which the fixture forbids. What IS
testable offline: the screen registers, and its predicate reads false at the galaxy,
management and planet view levels (walk the three and call `IsActive()` on the registered
instance). `Application.Preferences.ForceSystemDiscoverySequence` is the game's own re-run
switch, for a human running the manual script on a throwaway save.

**Reading a discovery card's CONTENT without the cutscene** (worked 2026-08-27; the same card
the colonization scene draws): the panel hangs off a window that exists unshown, so one `/eval`
does the whole round trip — `Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemDiscovery>(false)`,
take `.PlanetLabel`, save `.Planet` and the `Visible` flag of every `AgeTransform` from the
panel's own up through its parents, then set `.Planet` to any planet, call `RefreshNow()`
(public, ungated — it fills every label and pools every table), force that ancestor chain
`Visible = true`, call the reader, and restore both in a `finally`. Forcing the chain is what
makes the read faithful: `AgeWidgets.Visible` walks ancestors, so against the unshown window
every table reads hidden and the card answers with its name alone. Nothing survives the probe —
the restore happens inside the one statement, so no frame ever draws the panel, and the game's
own `OnBeginShow` re-`Refresh`es from scratch anyway. Pick the planet by walking
`ES2Access.UI.GameGalaxy.GameNodes()` for a `StarSystemNode` whose `Planets` hold deposits and
anomalies — whatever the loaded galaxy happens to have; the card is read off the PLANET, so no
save's own layout is part of the route.

- A FOREIGN empire's outpost card (population rows sighted by lending — **Moving population
  between planets**; the rest of the card unsighted), an outpost action past its start turn, the
  regress/stagnant/complete captions, the Hisshos wording, the `Discard`-hidden faction
  actions, buy-outpost, hangar ships, colonize, the ghost panels, the rebellion and migration
  rows, and `EmigrationGroup`/`ImmigrationGroup` (**The management page's permitted round
  trip**).
- The confirmation branch of a queued constructible: nothing on Dusay reports
  `NeedsConfirmation`.
- Mining probes on a planet row: `DepartmentOfTheTreasury.miningProbes` is empty for every
  empire (**Orbital and planet cards**).
- The population DROP: both fixtures have one colony in the system (**Moving population**).
- A governor: no save has one (**The assigned-governor side panel**).
- A card's Sanctuary band and the Sanctuary links panel: both need a player empire that HAS ghost
  systems, i.e. the Umbral Choir, which is Penumbra content and a faction choice made at new-game
  time — no save in this repo is one, so neither fixture can draw them whatever the session owns.
  Their STRUCTURE is sighted by lending (**Moving population between planets**); their content — a
  real ghost's figures and tooltips, the rival variant, the traitor button's two refusals, and
  either destination picker's round trip — is untested.

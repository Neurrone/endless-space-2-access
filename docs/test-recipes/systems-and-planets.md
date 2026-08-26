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

**Turning that page is ONE announcement and a seat in the new system's content.** The view level is
re-entered with a new node, and the mod's gates ride that out: `IsActive` asks
`LevelThroughTransitions` and latches on the page having planet cards drawn, the way the planet page
does, so the screen does not leave and come back. The screen then notices its own system changing
(`SystemManagementScreen.Turned`), says the new name once, waits 30 frames for the game to rebind the
window — seating earlier reads a row belonging to the system just left — and seats the cursor on
`InitialFocusStop` with a 60-frame retry budget for the cards binding. The latch must be gated on the
cards being DRAWN and not merely on the window being shown: a page that becomes active while it can
declare nothing gets its cursor seated on the first shared HUD control instead. Known rough edge:
while the page is between systems it declares nothing, so the cursor migrates to a HUD stop for a
moment and that migration is announced — one stray line between the screen name and the landing.

**The star-system page's name and its page turn.** Entering (the `OpenSystem` route above) announces
"Dusay, System management" once and seats on `system:planet/…`. `POST /input ui.pageNext` then
announces "Heka, System management" once and, about a second later, the new system's first planet
row. Expect ONE stray line between the two — the cursor migrating to a HUD stop while the page
declares nothing between systems. Check with `/speech?since=N` (exactly one screen-name line per
turn) and `DevProbe.Screen()` (`node` under `system:`). The regression to watch for is the ENTRY
landing on `hud:view-title/scan` instead: it means the screen went active before its planet cards
were drawn, and the walk's next Enter then toggles scan mode and poisons every later dump.

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
**The rest of the management page's blocked list**: a FOREIGN empire's outpost card; an outpost
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
drawing its label and loses its `Open system` child.

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

**Moving population between planets** (management page). The drag is offered only where the system
has a SECOND colony of the player's (`ColonizedStarSystem.PlanetsColonized.Count > 1`) — with one, the
population rows are declared read-only and there is no pick-up (measured live: with one colony
`Claims("Space")` reads false on the population rows and `ui.carry` answers `unconsumed`),
which is what both fixtures show
(Dusay: `planetsColonized=1`, `GetSpaceportSidePanel()` not shown). What IS testable with one colony:
push a drag by hand — `ES2Access.ModEntry.Carry.PickUp(new ES2Access.Core.UI.CarryItem(pop, "Imperials",
"population"), ES2Access.ModEntry.Navigator.Screen)` — and watch the card's readout grow "drop target",
`/input ui.activate` on the card refuse in the mod's fallback words with the drag kept, `ui.carry`
anywhere that is not a source answer silently with the drag kept, and `ui.back` answer
"Cancelled drag".
**Sighting the spaceport side panel**, which no save draws (ES2 facts: `IsAvailable()` needs
`MaxPopulation > 0`). Show it with `Gui.GuiService.GetWindow<SidePanelsWindow>(false).ShowSidePanel(p)`
— `SidePanel.Show` itself throws with a message telling you so — and the side-panel sweep declares it
at once ("Spaceport", the destination line and its button; the empty panel adds no rows, which is the
empty-state proof). To make its population ROWS exist, lend it real data:
`p.SpaceportPopulationEnumerator.Bind(colonizedPlanet, p.gameObject)` + `RefreshNow()` draws that
planet's markers in the spaceport's slots, and `Bind(p.Spaceport, p.gameObject)` + `RefreshNow()` puts
it back. That proves the rows, their words and the pick-up ("Dragging Imperials") — **never Enter on a
planet card while the binding is lent**, because the drop would move real population. Do not press the
destination button either: it opens `SystemSelectionModalWindow`.
**`PlanetPopulationEnumerator.CanAcceptPopulationDrop()` THROWS when no drag is in progress**
(`DragInfo.TransitingPopulation` is null), so it can only be called with `PopulationEnumerator.DragInfo`
filled in — and it is a static, read every frame by the enumerator's own refresh, so clear it in a
`finally` or a marker the player is still looking at reads as already gone.

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

**The planet constructible panel has no fixture either.** `PlanetConstructiblePanel` is opened
only by the card's Terraform and Reduce Anomaly buttons
(`PlanetLabelsWindow_SystemOrbital.OnTerraformPlanet` :255-265, `OnReduceAnomaly` :285-295), and
neither button is ever drawn without a Behemoth in the system. What IS testable offline:
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

- A FOREIGN empire's outpost card, an outpost action past its start turn, the
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

# ES2 per-screen test recipes

How to work each screen family against the live game without damaging the owner's fixture:
openers, safe round trips, reversibility probes, and what each fixture cannot show. Loaded
per-need — grep for the screen you are touching; the screen-agnostic verification patterns
(evidence crops, tooltip audits, silence rules, etiquette) stay in `docs/dev-loop.md` §2.
A new per-screen recipe or fixture limit lands HERE; `docs/roadmap.md` holds only work remaining
plus a pointer index of shipped screens.

**Raising a notification on demand** (the fixture has none pending):
`Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.Event.IEventService>().Notify(new EventEmpireIntroduction(Gui.PlayerEmpire))`
— dismiss afterwards (`Gui.GuiNotificationService.DismissGuiNotification(...)`); minimizing
leaves it in the icon strip, which is a fixture change. **For a notification whose event has
gameplay listeners** (anything on a quest), do NOT go through the event bus: build the
notification and show it directly — `var n = new NotificationQuestBegun(); n.Bind(new
EventQuestBegun(Gui.PlayerEmpire, quest)); Gui.GuiNotificationService.ShowGuiNotification(n);` —
then dismiss with the window's own binding
(`Gui.GuiService.GetWindow<QuestBegunNotificationWindow>().GuiNotification`). `IsAnyNotificationVisible`
is on `Gui.GuiGameWindowService`, not on the notification service. Raising the quest popup also pops
the "Tracking Quests" tutorial page, so re-minimize afterwards. **No save shows an UNLOCKED End
Turn** either, so the turn cluster's operable state stays code-verified.

**Working a popup that draws its own content** (the research family: "Research Complete",
"Technology Stage unlocked", "Construction Complete" — reachable by pressing Next/Previous
notification on a turn where research finished). Browsing between them is SAFE and reversible;
`DismissButton`/`Done` is not, and neither are the CARD buttons — `CompletedTechnologyTitle` and
`NextTechnologyTitle` dismiss the popup and open the technology screen (`OnTechnologyCompletedCb`,
`OnTechnologyNextCb`). Which popup type is up: iterate
`((GuiManager)Gui.GuiService).gameObject.GetComponentsInChildren<NotificationWindow>(true)` for
`Shown` and read `GetType().Name` (index the array — a `foreach` over it poisons the REPL session);
`/gui/age?window=<ThatTypeName>` is then the layout. The body rows are checked against that dump,
not against the code: the tree lists `TechnologyStageUnlockedNotificationWindow`'s unlocks table
BEFORE its title groups, and only the rects put them in reading order. Each unlock's tooltip is
Class-backed (`Constructible`), so its buffer is empty until the row is focused — audit it with the
tooltip pattern in `dev-loop.md` §2. **The drawn body is one region per CARD** where the popup drew
cards — the research popup answers `ui.regionNext` with top strip → "Just Completed, Xenobiology" →
"Next Research, Plasma Metallurgy" → "Minimize", while plain Down still walks all seven rows across
the boundary — and stays a single `notification:body` region where it drew one thing (the stage
popup, which draws no captioned control in the body). Checking a region change means
dumping `/gui/graph` for the region HEADERS and walking `ui.regionNext`/`Prev`: the row list alone
looks identical either way.

**"Construction Complete" is a TABLE, not rows** (region `notification:table:reg:0`, keys
`notification:table:row<hash>c<column>`). The regression shapes for its two neighbours are
"Research Complete" = two card regions / 7 items and "Technology Stage unlocked" = one
`notification:body` region / 5 rows — a change to the sheet detection that moves either of those
has broken it (the research popup's lore scroll view is the near miss it must keep rejecting). The
table's own shape on `[Beginner] test`: one row, "Dusay, button, Drone Networks, Cerebral Reality,
3 turns remaining"; Right crosses "Completed" then "Next Construction" — the column names are
spoken as the crossed edge and the drawn caption row is NOT a row of its own — and both figure
cells indicate a Class-backed `Constructible` dossier and carry it in the buffer. **Never press
Enter on a row while testing**: it is the game's own click (`OnSelectSystemCb`), which opens that
system's management view and puts the notification away — two fixture changes at once. Prove it
from `ConstructionCompletedNotificationWindow` :75-79 and leave the press to the manual test.
**The seams around the table are the BUILDER's, not the screen's** (`StitchModeBoundaries`, unit-tested
in `GraphBuilderTests`): every cell of the table's top row reaches the strip above and every cell of its
bottom row reaches the strip below, and coming back lands on the row's primary — live-verified
2026-08-11, all six crossings spoken. A `NotificationScreen` that grows hand-written `Connect` calls
around its sheet again is a regression, not a fix; the engine rule is the place to change.
**Multi-row is fixture-blocked**: the fixture finishes one construction on the turn it is saved
at, so only one line is ever drawn, and the ragged path (a system with nothing queued draws
`NoNextConstructionButton` in the third column instead) needs a save with several colonies
finishing at once — no fixture in the repo has one. The remaining-turns label on this line is a
bare integer or the `[infinite]` token ("Unlimited" once cleaned), never `-`
(`ConstructionCompletedNotificationLine.RefreshNextConstruction` :140-148 is the writer, not its
`FormatNumberOfTurns`).

**The leading-prose rule cannot be seen on any live popup.** All three of the research family take
the no-visible-words branch — Construction Complete's description is real but its label is parked
under a hidden container, Technology Stage's is a localization key the files never answered, Research
Complete's is an unfilled template — so a popup that both SAYS and DRAWS something is unreachable
here. Test it as exact non-regression instead: snapshot `/gui/graph?edges=1&buffers=1` for all three
along a fixed browse route (Previous, Previous, then left + Next + Next back to Construction
Complete), change, reload, walk the identical route and `diff`. In one session the ids are stable
objects, so the three files come out byte-identical and need no hash normalising.

**The queue-empty states of the research popup have no fixture** (`[Beginner] test`, turn 4, has a
research queue): `EmptyNextTechnologyGroup` (queue empty, nothing suggested) and
`SuggestedTechnologiesPanel` (queue empty with suggestions — toggles with captions, which would
arrive as body controls) are both drawn only when `DepartmentOfScience.ResearchQueue.Length == 0`
(`TechnologyUnlockedNotificationWindow.Refresh` :131-159). Do not fake it by emptying the queue —
that is a fixture change; reach it by playing a save whose queue has run dry.

**No quest in either fixture is in CHOICE state**, so a popup's choice cards have never been drawn:
the checkbox side of the radio/checkbox rule (the quest popup's own Pin toggle) is live-verified and
the `GuiRadioGroup` side is code-verified only.

**World position → screen pixel.**
`((GalaxyViewCameraController)Amplitude.Unity.Framework.Services.GetService
<Amplitude.Unity.View.ICameraService>().CameraController).Camera.WorldToScreenPoint((Vector3)node.GalaxyPosition)`
— the galaxy camera hangs off the controller's `Camera` property; `Camera.main` is null in this
game and the controller's own GameObject carries no `Camera` component, so both of those routes
answer nothing. Screen y is Unity's (bottom-origin); `crop-shot.ps1` takes top-origin pixels.
That is how a spoken direction is checked against the picture (es2-facts, world axes).

**A panel of wordless readouts.** `SystemManagementScreen`'s generic scrape reads a side panel
off the shape of its widget tree, which cannot name a bare number beside a symbol. `Special()` is
the escape hatch: match the widget by its game COMPONENT (`PopulationCount`,
`SystemRepresentativeItem`) or against a field of the owning `SidePanel` (`HapinessGroup`,
`GrowthGaugeItem`, `OutpostsGroup`, `PoliticalSensitivityBreakdown`) and return a hand-built
cell — one whose only words are a COUNT says it in a counted phrase off the model
(`ModStrings.Plural`), never by re-reading the drawn digits. `Transparent()`
is its partner, for a group the game made clickable that is really a band of readouts (the
approval box answers a click only in god mode). Names come from the game: `AgeWidgets.TooltipTitle`
for anything with a `GuiWrapper` on its tooltip, `Gui.GetLocalizedTitle(property)` for a measure,
the tooltip's first line for a control that explains itself on hover — but only where that
line NAMES the thing; a data-bearing sentence that merely explains ("This system is diverting
part of its growth to Rigel…") is a description, not a title, and a control with no naming
line anywhere gets a mod phrase. Keys must include
`widget.name` — a per-panel suffix alone collides across a repeated row and throws
`Duplicate control id`, which silently empties the WHOLE screen.

**The tutorial picker** is raised by `NewGameScreen.OnBeginShow` and only while
`TutorialManager.IsPlayingForTheFirstTime()` (registry `GameSettings/HasAlreadyPlayedOnce`, which
only `GameClientState_Introduction` ever sets — cancelling leaves it, so the box comes back). Back
to the MAIN MENU is two Escapes, i.e. `window.HandleInput(InputAction.Exit)` on the modal and then
on `NewGameScreen`. Never press Confirm or double-Enter a card in a test: both start a game.

**Working the new game lobby.** Everything is lobby-local and reversible (restore what you
change; `w.Session.GetLobbyData<string>("competitorcount")` etc. is the before/after probe).
**Never press Start** (`OnClickStartCb` launches). **Every way out of `FactionChoiceModalWindow`
COMMITS the highlighted faction** — Escape, Select, and the button labelled "Cancel", because
`GuiModalWindow.OnCancelCb` is `HandleInput(InputAction.Exit)` and this window routes Exit to
`OnValidateCb` (measured: picking Sophons and pressing Cancel left the lobby on Sophons). Opening
it is safe if you put the selection back first; `Gui.GetPlayerLobbySlot(ng.Session).FactionName`
is the before/after probe (fixture: `FactionTerrans`). Selecting a card does NOT commit.
`AdvancedSettingsModalWindow` is a safe open + `HandleInput(InputAction.Exit)` (its Back button is
the same `OnCancelCb`); the lobby stands down while either is up. The advanced window builds a
table per CATEGORY once and shows only `CurrentCategory`'s — read whichever is drawn, never the
container's first child.

**What the outgame fixtures cannot show.** Lobby: the multiplayer-only states (chat, Join/Kick/Ready,
the DLC strip) have no fixture at all, and renaming the player needs Steam. Advanced settings: no
column overflows at 1280×800, so scroll-into-view is inherited but unexercised. Custom faction
editor: nothing was ever persisted — SAVING a faction (`OnValidateCb` :686-700) and editing or
deleting an existing one are code-verified only. Load/save: the window declares no content unless
the dialog is up (`/gui/graph?screen=screen.load-save` answers "not active" from a running game),
so the whole family — including the type-ahead that searches SAVES rather than cells — is live-checked
only from the manual script.

**Working the technology wheel.** Open/close it from `/eval` with
`Gui.GuiService.GetWindow<GameOverlayWindow>().ControlBanner.ToggleScreen("TechnologyScreen")`
(F4 does the same); the first open in a session raises the "Tech Savvy" tutorial popup. The
permitted round trip is queue-then-cancel — probe with
`Gui.PlayerEmpire.GetAgency<DepartmentOfScience>().ResearchQueue.Length` and
`.PendingConstructions[i].ConstructibleElement.Name` before and after — but queueing fires
`EventTutorial_TechnologySelected`, so do it LAST and restore with `POST /loadsave`.
**Blocked in the beginner fixture (last checked turn 2)**: dependency links (only the Juggernaut
chain has them and the fixture draws none), Disabled technologies and their failure reasons,
buyout, a queue long enough to scroll, and a deed that has been WON or LOST — all 12 drawn deeds
are in progress, so "Locked" and "Available" read live (the latter with its whole `DeedDescription`
in the buffer) while Completed, Failed and "won by ⟨empire⟩" are unit-tested offline only.

**Round-tripping the pinned quest** (how both halves of the `hud:quest` passive announcement get
proved in one run): stash the quest first — `Quest __pinned = Gui.PlayerEmpire.GetAgency
<DepartmentOfInternalAffairs>().QuestJournal.ActiveQuest;` — then unpin through the mod's own node
(`/input ui.down` onto "Unpin quest", then `ui.activate`) and read `/speech` for "No quest is pinned"
plus a `/gui/graph` with no `hud:quest` stop; put it back with `…QuestJournal.ActiveQuest = __pinned;`,
which is the same assignment the journal's own pin toggle makes (`NarrativeScreen.cs:443`) and
answers with "Pinned quest: …". Opening the journal from the panel node is safe and reversible:
`ControlBanner.ToggleScreen("NarrativeScreen")` closes it again, and the stop comes back with the
cursor still on it. **Unverified in either fixture**: "Show location" (the turn-3 quest has no
marker, so the game hides the button), the numeric "(x/y)" progress branch, and a quest waiting on
an objective choice (which draws no progress word at all).

**Working the quest journal.** Open/close from `/eval` with
`Gui.GuiService.GetWindow<GameOverlayWindow>().ControlBanner.ToggleScreen("NarrativeScreen")`
(F7 and Enter on the pinned-quest panel node do the same; the mod screen is polled, so all
three land identically). Switching the filter is reversible and is the game's own radio group —
`ES2Access.UI.AgeWidgets.Toggle(w.QuestSelectionTogglesTable.Children[i].AgeControl as
AgeControlToggle)`, with `w.QuestFilteringRadioGroup.CurrentSelection` the before/after probe
(fixture: 0 = Current). **The turn-3 fixture draws exactly ONE card under every filter** — the
journal holds 40 in-progress and 13 completed quests and all but one are `QuestDefinition.Hidden`
or narrative events (`NarrativeScreen.cs:279`), so multi-card list navigation and the strip's paging
follow have no fixture at all. The Failed filter draws none, which IS the testable empty case: the
`quests:list` and `quests:detail` stops both disappear. **The pin is a child node of the card**, not a
gesture on it: `ui.right` opens the card, `ui.down` lands on "Pin Quest", Enter toggles, and
`QuestJournal.ActiveQuest` is the probe; unpinning speaks "not checked" from the toggle and "No quest
is pinned" from the HUD's watcher, even with the journal covering the panel. Alt+Enter on a card is
now silent by design (the game has no modified click there). **Also unverified at turn 3**: the
Show-location marker (the quest has none, so the game hides the button), the minor-faction button,
the podium a cooperative quest gets instead of a reward table, and the "Pending objective choice…"
placeholder.

**Ordering a fleet around** (state-changing — only against a save you can reload, and only after
every read-only check is done). It is two halves: **Enter** on the fleet's own node selects it, then
**`/input ui.contextual`** (backslash) on the DESTINATION — a system node, or a starlane child of one
(expand the system with `ui.right`) — sends it, answering "Send fleet ⟨name⟩ here" or "Nothing to do
here". Post it through the mod's own key rather than from `/eval`, then probe the game:
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

**What neither fixture can show on the galaxy map.** A send to a SYSTEM (turn 3 knows one, and its
three lanes all run into the dark); minor factions and pirate lairs; a FOREIGN outpost — the only
state that fills `%OutpostColonizationTooltipDescription` — and `OutpostCancelIcon` (an outpost
being lost or decolonized); and every fleet-label variant beyond the plain one-ship own fleet:
merged labels, guarding, multi-ship/automated/privateer fleets, and any fleet of another empire
(only one empire is visible at turn 3).

**Testing the selection chords and the drag.** `/input` cannot hold a modifier, so
`ui.selectToggle`/`ui.selectRange` reach the row's own click with NO physical Ctrl or Shift and the
game runs its plain (radio) branch: the injection proves the wiring, the announcement and the
fall-backs, never the modified semantics — those are code-trace plus the manual script. What IS
provable live: flip the panel's model from `/eval` and watch the row's live membership part
(`ShipsManagementPanel.DeselectShips()` plus `Dirty = true` makes a tile read "not selected" under a
standing cursor), then press the chord and read the state the row speaks back. The drag needs no
modifier and so is fully injectable: `DevProbe.Claims("Space")` reads true on EVERY mod screen
(the over-claim — es2-facts' scan-view fact) so it proves nothing about a pickup;
`ModEntry.Carry.IsCarrying`/`.Held.Name`/`.Held.Kind` is the
state probe, a compatible row's readout grows "drop target" while something is held, `ui.carry`
answers "Dragging …" on a source and SILENCE everywhere else — including on a drop target that is not
also a source — with the drag kept, `ui.carry` back on the source it came from and `ui.back` both
answer "Cancelled drag" (`claimsBack` reads true only until it does), and **`ui.activate` is the
drop**: on a control that takes the cargo it announces the drop and the control's own click does NOT
run, on any other control the click runs and the drag survives it (inject Enter on a harmless toggle
to prove that half). Silence is proved with a `/speech?since=N` window, not with the `/input` reply.

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

**Moving population between planets** (management page). The drag is offered only where the system
has a SECOND colony of the player's (`ColonizedStarSystem.PlanetsColonized.Count > 1`) — with one, the
population rows are declared read-only and there is no pick-up (measured live under the
launch-era conditional Space claim; under today's over-claim only the second half still
discriminates — `ui.carry` answers `unconsumed` while `Claims("Space")` reads true on every
mod screen), which is what both fixtures show
(Dusay: `planetsColonized=1`, `GetSpaceportSidePanel()` not shown). What IS testable with one colony:
push a drag by hand — `ES2Access.ModEntry.Carry.PickUp(new ES2Access.Core.UI.CarryItem(pop, "Imperials",
"population"), ES2Access.ModEntry.Navigator.Screen)` — and watch the card's readout grow "drop target",
`/input ui.activate` on the card refuse in the mod's fallback words with the drag kept, `ui.carry`
anywhere that is not a source answer silently with the drag kept, and `ui.back` answer
"Cancelled drag".
**`PlanetPopulationEnumerator.CanAcceptPopulationDrop()` THROWS when no drag is in progress**
(`DragInfo.TransitingPopulation` is null), so it can only be called with `PopulationEnumerator.DragInfo`
filled in — and it is a static, read every frame by the enumerator's own refresh, so clear it in a
`finally` or a marker the player is still looking at reads as already gone.

**Working an outpost** (`[Beginner] test`, Rigel). Open it with
`GalaxyViewLevels.OpenSystem(...ColonizedStarSystems[1].Node)`; entering pops "INTO A FOREIGN LAND"
and leaving pops "Dangerous Visions" — re-minimize both
(`Gui.GuiService.GetWindow<TutorialWindow>().GetComponentInChildren<TutorialPopupPanel>(true).MinimizeToggle`
through `AgeWidgets.Toggle`). The permitted round trip, LAST in a run: Enter on **Merchants and
Money** starts it and Enter again the SAME turn cancels it with a refund — probe
`DepartmentOfLabour.EntityActions` (index it, never `foreach`) and
`Gui.PlayerEmpire.GetPropertyValue(SimulationProperties.Empire.BankAccount)` before and after
(measured 253.81 → 103.81 → 253.81). **Decolonize**: Enter raises the game's own confirmation, which
must SPEAK; answer Cancel and check
`...ColonizedStarSystems[1].IsScheduledForDecolonization` — never Confirm. `POST /loadsave`
afterwards regardless.

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

**Entering a system re-opens the tutorial.** The first time the camera reaches a view level,
the game pops that level's tutorial page — so an Enter-on-a-colony test leaves the popup
un-minimized. Put it back (`TutorialPopupPanel.MinimizeToggle`, then send its `OnSwitchMethod`)
before calling the run done.

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

**What the beginner fixture cannot show on the planet overview** (last checked turn 1): curiosities,
resource deposits and the depletion row (no planet in the fixture has any), and the population
entries' click — the game opens `PopulationModalWindow` there, and the entries are declared
read-only per the approved design.

**Opening the star system page.** `GalaxyViewLevels.OpenSystem(Gui.PlayerEmpire.GetAgency
<DepartmentOfTheInterior>().ColonizedStarSystems[0].Node)` from `/eval` (Dusay, GUID 535 in the
fixture; `GameEntityGUID` is NOT in `Amplitude.Unity.Game`, so go through the node). The page
arrives in pieces — the side panels a frame or two before the planet cards — so a screen that
declared the half that existed seated the cursor on the wrong stop for good. Here the late
half (the cards) is the page's FIRST stop, so waiting for it is still right — but per the
tightened rule (making-screens-accessible.md §3), the gate protects the cursor's seat, not
the page: a page whose early half is usable declares it, and the planet page's lost-card
repair (`Nudge`) is the other half of that story.

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

**The planet constructible panel has no fixture either.** `PlanetConstructiblePanel` is opened
only by the card's Terraform and Reduce Anomaly buttons
(`PlanetLabelsWindow_SystemOrbital.OnTerraformPlanet` :255-265, `OnReduceAnomaly` :285-295), and
neither button is ever drawn without a Behemoth in the system. What IS testable offline:
`screen.planet-constructibles` registers (`/gui/graph?screen=…` answers "not active"), and its
predicate reads false at the galaxy overview, at the orbital zoom step with the cards drawn, and
on the management page. Opening it from `/eval` is not worth it: `ShowConstructiblePanel` is
private and indexes `fleetByActionDefinitionDictionary`, which in the beginner save holds no fleet.

**Per-screen blocked-in-fixture inventories live here**, one paragraph per screen; the roadmap holds
only work remaining.
What a test SESSION needs here: **the one permitted state round-trip on the management page**
is Enter on a cheap constructible to queue it and Enter on its queue line to cancel it — check `dust`
and the queue's names/order before and after (`ConstructionQueue.PendingConstructions`, indexed
never `foreach`ed). Queue two or three and the line becomes a drag source as well, which is how the
reorder is exercised inside the same round trip; the research queue is the same shape
(`DepartmentOfScience.ResearchQueue`, queued from the wheel's `research:suggested` stop in two key
presses). Both were run against a LIVE owner session and restored exactly. The home planet is
`IsUnique` so planet rename is unreachable; `StarSystemPopulationModalWindow`'s opener is
tutorial-locked; and at turn 3 no buy-out button is drawn at all
(`BuyoutTechnologyNotUnlocked` — es2-facts), so the queue line's buy-out children have no fixture.
**The rest of the management page's blocked list**: a FOREIGN empire's outpost card; an outpost
action running PAST its start turn (the disabled cancel); the regress/stagnant/complete captions and
the Hisshos wording; the `Discard`-hidden faction actions (Hisshos/TimeLords/Vodyani); buy-outpost;
and, last checked turn 1, hangar ships, colonize, the ghost panels, and the rebellion and migration
rows. `EmigrationGroup`/`ImmigrationGroup` — the growth line's two count-only siblings of the
outposts readout — are drawn by neither save, so nothing has been invented for them.

**The improvements modal has nothing destructible in the beginner fixture** (last checked turn 1):
ticking a tile, the Scrap button's enabled label and its confirmation, multi-row wrapping,
scroll-into-view, the assigned-hero readout and the empty-list state are all code-verified only.

**The system-discovery cutscene has no fixture at all.** It only runs on a system's FIRST
visit (`GalaxyViewLevel_SystemDiscovery.CanBeActivated`: explored, visible, planets-visible,
not already discovered), so reaching it means exploring — which the fixture forbids. What IS
testable offline: the screen registers, and its predicate reads false at the galaxy,
management and planet view levels (walk the three and call `IsActive()` on the registered
instance). `Application.Preferences.ForceSystemDiscoverySequence` is the game's own re-run
switch, for a human running the manual script on a throwaway save.

**Escape out of a view level** cannot be tested through `/input`: with no screen of ours
focused the injector's action is dropped before the game sees it. What the key reaches is
`StarSystemScreen.HandleInput(InputAction.Exit)` — call that to prove the destination, and
leave the key routing itself for the human test script.

**Opening the system-selection modal** (`SystemSelectionModalWindow`, the outpost side panel's
"change colony" picker). Its Confirm does nothing without the DELEGATES its opener installs, so
open it through the opener's own private handler by reflection:
`typeof(OutpostInfoSidePanel).GetMethod("OnClickChangeColonyCb", System.Reflection.BindingFlags
.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(UnityEngine.Object
.FindObjectOfType<OutpostInfoSidePanel>(), new object[]{ null })` — `SendMessage` with no
argument logs an arity error and does nothing (es2-facts). Escape/Cancel is safe (commits
nothing); **never press Confirm or replay a line's double click** — for the outpost purpose
that posts `OrderChangeOutpostGrowthProvider` and resets the ship timer. Selecting a row is
harmless (enables Confirm, posts nothing). **Fixture-blocked** (turn-4 `[Beginner] test` has exactly
ONE colony, so the table draws one row): multi-row navigation, up/down between rows, a sort visibly
reordering, a REFUSED row's sentence, the scroll view, and an operable policy drop list — the fixture
draws it `interactable=false`, so the combo-box branch and the `DropListScreen` it opens are
code-verified only.

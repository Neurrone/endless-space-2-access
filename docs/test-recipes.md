# ES2 per-screen test recipes

How to work each screen family against the live game without damaging the owner's fixture:
openers, safe round trips, reversibility probes, and what each fixture cannot show. Loaded
per-need — grep for the screen you are touching; the screen-agnostic verification patterns
(evidence crops, tooltip audits, silence rules, etiquette) stay in `docs/dev-loop.md` §2.
A new per-screen recipe or fixture limit lands HERE; `docs/roadmap.md` holds only work remaining
plus a pointer index of shipped screens.

**A third fixture exists**: the owner's **"unlocked" save** — every screen unlocked, the
TECHNOLOGIES not (turn 1; the gate table is in the stage-8 report). Recipes below that say "this
save" without naming one mean that save, and it is why so many screens read structurally right
and content-poor.

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
fall-backs, never the modified semantics — for those, hold the key for real (next paragraph). What IS
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

**Holding a PHYSICAL modifier while a key is pressed** (the only way to test a modified click's game
branch — Ctrl+click to locate, Alt+click to queue at the head). From a PowerShell script: bring the
game up with `SwitchToThisWindow` plus `AttachThreadInput` + `SetFocus`, then drive the keys with
`keybd_event`. `SetForegroundWindow` ALONE fails silently — the window comes up but Unity still reads
the key as released, so the chord runs unmodified and looks like a wiring bug. Re-focus before every
run, not once per session. And when the surface under test is a game screen shown UNDER a modal, it
never reaches the mod's own stack: probe `Gui.GuiService.GetWindow<T>().Shown`, not
`DevProbe.Stack()`, or a screen that is working reads as absent.

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
population rows are declared read-only and there is no pick-up (measured live: with one colony
`Claims("Space")` reads false on the population rows and `ui.carry` answers `unconsumed`),
which is what both fixtures show
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
**Without an outpost at all** the same window is still reachable through a different opener: build
the `ShipsSpawnPointSidePanel` validator delegate with `CreateDelegate` and show the window with it.
Same warning, doubled — **never press Confirm** on that purpose either: it NPEs.

**The rename box.** It walks heading / field / Cancel / Confirm. Its openers are the star-system
name line (the Colony panel's name node opens it directly), the planet card's rename button
(unreachable on a unique home planet) and the fleet panel; close it with
`window.HandleInput(InputAction.Exit)`, which is the route the key takes. A `ColonyInfoSidePanel`
found via `GetComponentInChildren` on `StarSystemScreen` has a NULL `RenameButton` (wrong
instance) — drive the opener through the mod's node. The one-Escape contract —
the first Escape steps OUT of editing, the second closes the box — is proved in halves, because
Escape itself cannot be injected: simulate the FIRST one with
`AgeManager.Instance.FocusedControl = null` and prove the mod's consumption with
`DevProbe.Claims("Escape")`. The same-frame commit bug is not provable through `/input ui.activate`
(no physical key exists), but the latch half is: `POST /wait` on
`ModEntry.Input.ConsumedKeys.Count > 0`. Reaching a mod internal from `/eval` at all needs the
loaded-from-bytes assembly by name — scan `AppDomain.CurrentDomain.GetAssemblies()` for the
`modAssemblyName` that `/status` reports, then `GetType` off it; the plain type name does not
resolve.

**The assigned-governor side panel** is measurable without a save that has a governor: flip the
`Visible` flags `ColonyHeroSidePanel.Refresh` (:157-240) writes, read, and put them back. The
unassigned prefab holds STALE hero text, so what the flip proves is the variant's SHAPE, never its
words.

**The system-politics modal.** Open it from the star-system page's own node. "Show all events" is
persistent WINDOW state — restore it — while the party pick is not. The table binds
`canSelect:false`, so nothing in it commits.

**Hero selection and the hero list.** The pickers are reachable from the academy family. **Never
press Confirm and never press the card's own Content button**; selecting a card commits nothing,
but `Refresh` wipes `SelectedHero` (es2-facts), so a cached selection is meaningless. Note the
**modal-return cursor**: closing any modal over the star system page lands on the planets stop's
start node rather than on the button that opened it — pre-existing, and true of improvements and
rename too.

**The election modal** is a 12-step wizard walked entirely read-only: every step's Next/Previous is
non-committing, and the outcomes are never drawn (es2-facts).
`GovernmentAction_ForceElections` is the game's own way to raise a real one and is UNVERIFIED — do
not spend a fixture on it without the owner's say-so.

**The scan view.** Entry is Enter on the lens toggle; the PLAYER route through the lenses is the
`Zoom` node beside it — Left/Right steps the 15-rung ladder, Shift+Left/Right jumps a lens band,
Right at rung 13 enters the system, Enter on a planet card reaches the planet lens, Left steps
back out. Verify with those injected actions; `cam.ForceZoomingOnPosition(step, position)` is for
RESTORE only (`SetZoomStep` alone leaves the labels culled). The zoom table: 0-1 Diplomacy /
2-5 Trade / 6-9 Economy / 10-12 System, plus the system and planet layers. **All four lens windows
report `Shown` at once**, so the drawn `ScanViewWindowHeader` is the only reliable lens signal and
`CaptionsPanel.ScanViewGuiElement` goes stale. Restore `ShowScanViewCaptions` and
`ShowScanViewSystemInfos` afterwards. Fixture notes: one perceived system; synergies on two of
Xiu's four planets; the rank graphs and remains panel never draw at turn 1.

**Galaxy: the fog-off smoke pattern.** Forcing a world-state predicate TRUE in code, rebuilding,
walking read-only and reverting is the sanctioned alternative to mutating a save — it is what proved
the shared-`Link` teleport. In the unlocked save the map draws 1 perceived system, 0 hangars and a
Signal curiosity that refuses, so nothing else on it can be sighted there.

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
unexplored — no lane-destination child is testable there.

**Probes and faction panels (the other galaxy labels).** The unlocked save draws NONE of this
surface. A probe row is exercisable with `probeLabel.Show()` then `Hide(true)` — self-healing. The
faction panels need `Bind` + `Show`, then `Hide` + `Unbind` **and** `InspectedEmpire` restored
through its private setter: `Unbind` leaves the game's own `Refreshed` handler live, which NREs on
the next refresh otherwise.

**The senate family** (senate, government, laws, population). Open it from `/eval` with
`ControlBanner.OnControlBannerToggle`; reach the modals through the mod's own nodes. **NEVER press
Validate, Pass, Abolish, a boost, or Assimilate.** The selection resets on every show, so nothing
carries between visits. Expect a ~1 s `unavailable` on the page under a just-closed modal — that is
the game's fade, not a defect; re-read. **Save-blocked**: the gene hunter, assimilation, relics, a
real election, an enabled Abolish, a drawn history graph, an empty senator slot, and the outpost
panel.

**The empire page.** The interactive cells are columns 1/2/4/11/13. Nothing closes an opened band
except leaving the page. The tab switch and the panel instances are both probeable from `/eval`;
`SidePanels`' `PanelTitle` branch first got exercised here.

**The economy page and the recipe modal.** Which rows draw at all is the stage-8 gate table (this
save is screen-unlocked, not tech-unlocked) — which also means the **Marketplace tab is refused**
(missing Galactic Commodities Exchange), so the buy table has NO fixture in `unlocked` and the
resources grids are the only economy tables that can be walked. The recipe modal is reachable with zero slots via
`new GuiRecipeSlot(0,false)` + `ShowWindow`. **NEVER press Confirm** — it is enabled even with an
empty recipe and posts `OrderCreateRecipe` — and note Reset does NOT clear `RecipeModified`.

**Military and fleet-selection.** **Never press Retrofit**: it is immediate, with no confirmation.
A force-shown fleet-selection window must never have a row SELECTED — `ProcessSelection` NREs on a
null `CheckValidity`. Create raises the Architects tutorial page in this save, so minimize it
afterwards. Restore the camera when done.

**The ship designer.** Open it by reflection on the private `Cb`s, and take the panel instance with
`GetComponentInChildren` on the WINDOW — the hero window hosts a second one, and grabbing the wrong
instance reads a page nobody is on. **Never press Create or Apply.** Only civilian hulls exist in
this save. Restore `SelectedGuiShipDesign` and the toggles: `ShowDetailedStatsToggle` persists
across opens, the category filter does not — and the two hosts hold INDEPENDENT toggles, so the
designer's state says nothing about the hero window's. The detailed toggle gates exactly the three
`Detailed*` panels and nothing else; `Accuracy`/`Evasion` are hidden in the PREFAB (no fixture can
show them) and `SpecialStatsTable` only fills for a mining probe. Reopening from `/eval` needs a
`GuiShipDesign`: take one off the `ShipDesignItem` children rather than constructing it. **Edit
raises the Architects tutorial**, whose page node swallows navigation — minimize it before walking
anything.

**Hero inspection.** Bind, open, switch pages and close from `/eval`. An unrecruited `GuiHero` is
the read-only fixture. For a skill point, set `Level = 2` and `Refresh`, then restore by reloading
the save. Page switches raise tutorial popups — minimize them.

**Troops and the tactics deck** are both non-committing until Confirm, which makes them safe to walk
whole. A refusal is provable from BOTH sides by injecting one: force the game's own refusal state,
read the spoken reason, and put it back.

**The battle fixture** is a 14-step script (in the session report) because a battle cannot be
created from `/eval` — it needs two hostile fleets meeting. Everything before the meeting is
read-only; from the setup popup onward the run is destructive, so it goes LAST and ends with
`POST /loadsave`.

**Diplomacy, the academy pair and the sweep** are largely forced-show work: bind what the window
needs, set `Visible=true`, read, then `Unbind` and hide, and re-diff the graph dump to prove nothing
was left behind. A forced show proves STRUCTURE, not content. **Never press** any diplomacy action,
any negotiation button (closing an unsigned negotiation still posts an order — es2-facts), or
anything on the pirate page while there are no pirate systems (its `Refresh` throws). **The
`AcademyModalWindow` Bind wedge**: a half-bind survives the probe and leaves the window unusable —
recover with `Unbind` plus a re-issued `POST /loadsave`, and never force-show a DLC modal without
its data.

**Forcing a DLC side panel without the DLC.** The prefab INSTANCES exist regardless: bind the panel,
set `Visible=true`, read the graph, then `Unbind` + hide and re-diff. The same holds for every
`NotificationWindow` instance — all of them exist whether or not the DLC that raises them is
installed, so notification variants are readable structurally even when they are unsightable.

**The three bind-and-show openers the DLC stage used** (the datatables load whether or not the
expansion is owned — es2-facts, so these give real CONTENT, not just structure):
the Juggernaut specialization modal binds off a fleet ship reached through
`DepartmentOfDefense`; `ContextualPromptWindow` binds with a `ContextualPromptGuiElement` —
**never press its "Yes"**, which commits the hacking operation behind it; and
`StarSystemPopulationModalWindow` binds `...ColonizedStarSystems[0]`, which raises the BREAD AND
CIRCUSES tutorial — minimize it afterwards.
**Correction to `audit-dlc-mechanics.md` §5**: it calls
`DefenseHackingProgramEncounteredNotificationWindow` a partial gap needing a `Variant`. It is not —
its `CancelHackButton` carries the words "Cancel Hacking Op", so the shared caption rule finds it and
no per-window wiring is needed.

**Walking the out-game family from inside a session.** Leave the session first: show
`BlackCurtainWindow`, then `GameClient.Disconnect(ClientLeft)` — the menu comes up with the pages
reachable. Per page, `Gui.GuiService.ShowWindow<T>()` and `HandleInput(InputAction.Exit)` to close,
EXCEPT the disclaimer, which swallows every action (es2-facts) — close it through its own Accept
node. **Never press**: Decline on the disclaimer (quits the game), Confirm on the mod manager
(reloads the runtime), or any store/web button (leaves the game). The DLC browser REMEMBERS its
selected tab across opens — put the tab back when done.
**The asset exporter** (`ShowWindow<ResourcesExportScreen>()`): never press either export button
(they write files) or Open folder; progress is drivable by setting the panel's private
`lastMessage` + the private `ExportInProgress` setter, restoring both; a row's click goes through
reflection on `OnResourceExportPropertyItemClick(int)` — `SendMessage` from `/eval` drops the
argument; the page reloads its manifests every visit, so wait for them. Known game bugs:
`ResourceExportPropertyItem.Refresh` NREs on some assets (page stays "No asset selected"), and
re-entering resets the filter TICKS without firing their callbacks, so the ticks can contradict
the drawn list until one is toggled.

**The elimination popup and the journal.** `OrderEliminateEmpire` writes a REAL `EndGameSummary`,
which is what makes the journal's ending entries readable; delete the entry afterwards through the
journal's own cell rather than by editing the summary. The popup's groups hold no text and it hides
Dismiss and Minimize (es2-facts), so its sentence rides the screen name.
**A journal row without ending a game**: `new EndGameSummary(Gui.Game)` self-saves the FIRST time
only (it sets `Game.EndGameSummaryAlreadySaved`); after that, construct one and call
`SaveEndGameSummary(it)` — exactly one wrapper, never both for one instance (two rows, one object
→ `Duplicate control id`). Open the journal in-game with
`Gui.GuiService.ShowWindow<JournalModalWindow>()`, close with `HideWindow` — **never Escape**,
which hides the journal and shows the MAIN MENU. Enter on the score-screen cell opens
`VictoryScreen` (`fromJournal:true`); come back with `HideWindow<VictoryScreen>()` +
`ShowWindow<JournalModalWindow>()`. Delete through the cell, answer Confirm, then `POST /loadsave`.
**Raising a tutorial popup or an error on demand**: opening the technology screen or the politics
modal binds a tutorial ("A MATTER OF INFLUENCE" / "BREAD AND CIRCUSES") and closing that window
unbinds it; `((GuiManager)Gui.GuiService).ShowError(flags, message, stack,
UnityEngine.LogType.Error)` raises the error box — dismiss with its Continue button, never Exit
Game. In `unlocked` the star system page itself binds one
(`Gui.GuiGameWindowService.RequestStarSystemManagementViewLevel(...Node.GUID)`), which arrives
EXPANDED and takes the keyboard; collapse and expand it without walking to it by replaying its own
arrow — `MinimizeToggle.State = true/false` then `SendMessage(OnSwitchMethod)`.

**The collapsed-tutorial-under-a-modal window** (the one state where a minimised popup can speak
over the page underneath): expand the popup, open a modal over it — an `AboveModalWindows` tutorial
stays `Shown`, and the mod's tutorial screen stands down for the modal while its linger stays armed —
then minimise it while the modal is up, then close the modal. Watch `/speech` across the close: the
tutorial's title and page must not be in it. Every step is an `/eval` (the improvements modal's
opener is in `dev-loop.md` §2), so the whole repro is four requests.

**A solo multiplayer session** — the only fixture for the MP-only states, correcting the older claim
that they have none at all. Switch the lobby's Session Mode to Protected, which makes it a
multiplayer session with one player. The safe start/stop is `LocalPlayerReady` true then false, never
Start. Send a chat line with `ReplaceInputText` plus the reflected `OnTextFieldValidateCb`. Leave the
lobby before any `POST /loadsave`: from a lobby that route answers not-ready forever.

**The chat key and the chat box.** Neither half needs a keypress. The remap is proved with
`DevProbe.Chord("Ctrl+Tab")` → `suppressed:false` while `Chord("Tab")` stays suppressed; the handler
chain with `InputManager.HandleInput(InputAction.StartChatting)` to open the box and
`HandleInput(InputAction.Exit)` to close it. The options row re-reads live, so the binding shown
there follows the programmatic move with no reopen.

**Notification regression capture.** Any change to the notification family is checked by walking a
fixed browse route over all three research-family popups and diffing `/gui/graph?edges=1&buffers=1`
per popup, per the exact-non-regression pattern above.

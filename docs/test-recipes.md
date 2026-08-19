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
(`Gui.GuiService.GetWindow<QuestBegunNotificationWindow>().GuiNotification`). A
notification shown this way never joins the empire's list, so `POST /loadsave` clears it
without trace — the cheapest restore. Luxury example: `new NotificationLuxuryDiscovered()`
bound to `new EventLuxuryDiscovered(Gui.PlayerEmpire as Empire, new
Amplitude.StaticString("Luxury4"))`; Luxury1–6 = RedSang, Jadonyx, Dustciduous Trees,
Bluecap Mold, Eden Incense, Transvine. `IsAnyNotificationVisible`
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
cards — since the 2026-08-19 stop split the walk is per stop: on `notification:content`,
`ui.regionNext` steps "Just Completed, Xenobiology" → "Next Research, Plasma Metallurgy", and the
strips are walked on `notification:controls` — and the body stays a single region where it drew
one thing (the stage
popup, which draws no captioned control in the body). Checking a region change means
dumping `/gui/graph` for the region HEADERS and walking `ui.regionNext`/`Prev`: the row list alone
looks identical either way.

**"Construction Complete" is a TABLE, not rows** (region `notification:table:reg:0`, keys
`notification:table:row<hash>c<column>`). The regression shapes for its two neighbours are
"Research Complete" = two card regions / 7 items and "Technology Stage unlocked" = one
`notification:body` region / 5 rows — a change to the sheet detection that moves either of those
has broken it (the research popup's lore scroll view is the near miss it must keep rejecting). The
table's own shape on `[Beginner] test`: one row, "Dusay, button, Drone Networks, Cerebral Reality,
3 turns remaining"; Right crosses "Completed" then "Next Construction" and Left back onto the row
crosses "System" ("System, Dusay, button, …", 2026-08-15) — the column names are
spoken as the crossed edge, EVERY column's including the first, and the drawn caption row is NOT a
row of its own — and both figure
cells indicate a Class-backed `Constructible` dossier and carry it in the buffer. **Never press
Enter on a row while testing**: it is the game's own click (`OnSelectSystemCb`), which opens that
system's management view and puts the notification away — two fixture changes at once. Prove it
from `ConstructionCompletedNotificationWindow` :75-79 and leave the press to the manual test.
**The strips are a different STOP from the body since the 2026-08-19 split**
(`notification:content` = empire-info band + body, `notification:controls` = top strip + bottom
bar), so the table no longer reaches the strips by Up/Down at all — a sheet row's vertical edges
stop at the table's boundary (re-measured live 2026-08-19 on Laws Cancelled: the row's only edges
are `left`/`right` within the table) and Tab is the crossing. A `NotificationScreen` that grows
hand-written `Connect` calls around its sheet is still a regression; the stop split is the design.
**Multi-row is no longer fixture-blocked** — the popup is STACKABLE, so a two-row one is built by
binding the same notification twice before showing it, and nothing joins the empire's list:
`var n = new NotificationConstructionsCompleted(); n.Bind(new EventConstructionCompleted(PEMP,
(ColonizedStarSystem)css[0], CONSTR)); n.Bind(new EventConstructionCompleted(PEMP,
(ColonizedStarSystem)css[1], CONSTR)); Gui.GuiNotificationService.ShowGuiNotification(n);` — with
`CONSTR` any `ConstructibleElement` without the `HideNotification` tag (`StarSystemImprovementColonyBase`
is one that has a real title), and `ColonizedStarSystems` bound as `System.Collections.IList` because
its `ReadOnlyCollection<ColonizedStarSystem>` is REPL poison. Sighted 2026-08-15 on the owner's live
save: rows "Dusay, button, Colony Base, Xeno-Industrial Infras., 2 turns remaining" and "Rigel,
button, Colony Base, Empty Construction queue", 1 of 2 / 2 of 2, `clean:true`. Dismiss with
`Gui.GuiNotificationService.DismissGuiNotification(Gui.GuiService.GetWindow<ConstructionCompletedNotificationWindow>(false).GuiNotification)`;
the empire's list reads 0 before and after. The old block stands only for a SAVE that draws several
lines by itself — no fixture in the repo has one. The RAGGED path is no longer blocked: sighted
2026-08-15 on the owner's live save (turn 18), the third column reads "Empty Construction queue"
where the system has nothing queued, and the row is "Rigel, button, Drone Networks, Empty
Construction queue" — the `NoNextConstructionButton` branch, walked as an ordinary cell. The
remaining-turns label on this line is a
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
the `GuiRadioGroup` side is code-verified only. **Sighted once on a player's own save**
(`QuestBegunNotificationWindow`, a three-way choice, 2026-08-15): `notification:body/0/LoreGroup` =
the lore paragraph as ONE row (node `notification:body/0/QuestLoreSW`, and the start of the walk),
`notification:body/1/StatusContentGroup` = "Choose an objective", the three
`QuestChoiceItem00N` radios banded into one row, then the objective line, "Reward" and the reward
label; Minimize / Pin Quest / Confirm and the usual three top controls are all on the
`notification:controls` stop (2026-08-19 split), walked with Down.
Two body regions, not three — a build that puts `StatusTitleGroup` and `QuestChoicePanel` back in
regions of their own has lost the lore, because it is the lore that makes
`QuestDescriptionContent` the cards' container.

**A popup's lore is only readable while its label overflows nothing.** The game writes quest,
deed, technology and metaplot lore as ONE label inside a scroll view, laid out at the text's full
height inside a viewport a fraction of it — 429 px of paragraph inside 182 px of window on the
quest popup. The label keeps its whole rectangle, which runs down across the bottom button strip,
so the body reader's "between the strips" test dropped it (fixed by `AgeWidgets.Clipped`,
2026-08-15). It is TEXT-LENGTH dependent and so invisible to a structural check: the same prefab
reads correctly with short lore (the deed popup's `ObjectiveLore`, the research popup's
`TechnologyLoreSW`) and silently loses it once the writer wrote a long one. 22 of the 69
notification prefabs hold a scroll view with labels in it; the single-label "…Lore…" ones are
`QuestBegun`, `QuestCompleted`, `NarrativeEventBegun`, `DeedCompleted` (`OutcomeLoreGroup`),
`SpecialNodeEvent`, `TechnologyUnlocked`, `MetaplotBegun`/`Finished`, `NewUnlockedContent` and
`NewDownloadableContent` — check the lore of any of them against a save whose text is long.

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
only from the manual script. It CAN be raised in-game from `/eval` the way the pause menu does
(`var w = Gui.GuiService.GetWindow("LoadSaveModalWindow") as LoadSaveModalWindow; w.LoadSaveMode =
LoadSaveModalWindow.LoadSaveType.Save;` — or `LoadFromGame` — `Gui.GuiService.ShowWindow(w);`), and it
closes with `Gui.GuiService.HideWindow(w)` or with `w.HandleInput(InputAction.Exit)` (which DOES
exist and answers `true`) — but Exit takes the game's own route back and RAISES THE PAUSE MENU, so
that variant is two closes: the modal, then `GetWindow<GameMenuModalWindow>().HandleInput(
InputAction.Exit)`.
The type-ahead lands on a save by name (`POST /type "fleet rework"`) and gives ONE result per save
whichever column focus is in. **Never Enter on a row, never press Load, Save or Delete, and NEVER
`ui.doubleClick` a row** — the second click is CARRIED here (owner ruling 2026-08-14): it selects the
row and fires the load (or, in save mode, the overwrite), with only the game's own confirmation box
between the chord and a loaded game. The saves are a
`TableSheet` since 2026-08-14: the sort band is a row above the rows, Up/Down speak "x of ⟨saves⟩",
and a column's caption is the crossed edge — the NAME column's too since 2026-08-15 (`TableSheet.Columns`
now leads with `HeaderFor(cells[0], 0)`), so stepping Left back onto a save says that column's caption
before the save's name, or stays silent if the game drew that heading as a bare icon. Which of the two
this table does has not been sighted live; it is a manual-test step. The Mods column is the one column that overrides the
shared `ModeFor` rule (`LoadSaveScreen.CellTooltipReading`, keyed on the header's `PropertyName`
`RuntimeModules`, never on the translated caption): its Content is a whole module dossier, so the
dossier is read from the buffer rather than announced. Measured on the Autosave row (transcript
predates the "has tooltip" removal): `loadsave:…c7` says *"Valid"*, buffer *"Mods, Valid" / "The
mod configuration of this game is valid and the same as yours." / "Configuration:" / "- Endless
Space 2"*.

**Working the technology wheel.** Open/close it from `/eval` with
`Gui.GuiService.GetWindow<GameOverlayWindow>().ControlBanner.ToggleScreen("TechnologyScreen")`
(F4 does the same); the first open in a session raises the "Tech Savvy" tutorial popup. The
permitted round trip is queue-then-cancel — probe with
`Gui.PlayerEmpire.GetAgency<DepartmentOfScience>().ResearchQueue.Length` and
`.PendingConstructions[i].ConstructibleElement.Name` before and after — but queueing fires
`EventTutorial_TechnologySelected`, so do it LAST and restore with `POST /loadsave`.
**Driving the game's own "go and look at this technology"** (the Ctrl+click hint jump, which no
injection can reproduce because the game reads the physically held Control): call what the game
calls — `var w = Gui.GuiService.GetWindow<TechnologyScreen>(); w.FocusTechnology(
Gui.GuiWrapperProviderService.GetGuiTechnology2("TechnologyDefinition<Quadrant><n>"));
Gui.GuiService.ShowWindow(w);` — from the galaxy AND with the wheel already open on some other dot.
The mod's cursor must land on that technology (`ResearchLocate`); a plain reopen afterwards must
still restore the remembered position. In **`unlocked`** the tech screen binds "A MATTER OF
INFLUENCE" on EVERY open (not just the first) and the popup arrives expanded and takes the keyboard,
so each of these steps ends with the minimize replay before the landing can be read — the locate is
HELD, not lost, while the popup is up. Closing the window unbinds the tutorial again.
**The transition round trip** (the route that lost the cursor to the tutorial bar): from the star
system page, park the cursor on a real node, `w.FocusTechnology(…); Gui.GuiService.ShowWindow(w);`,
then close with `ControlBanner.ToggleScreen("TechnologyScreen")`. It must come back on the node it
left. Watch the frames with a `POST /wait` on `ES2Access.Dev.DevProbe.Trace("tag")` and
`GET /log?since=0&grep=trace` — the frame to look at is the one where `screen.star-system` is active
with a node count in single digits.
**Where the wheel's first open lands** (re-check after any `IsActive` change): `POST /reload` for a
fresh navigator state, then open — measured landing is `research:tree`, "Technology tree, Military,
group, … 1 of 4". A 115-frame `POST /wait` across an open found NO frame where the window is
`Shown && IsReady` while its transform is disabled, so this screen has no readiness race to gate on.
**Link arcs in `unlocked` (turn ~15)**: 22 of 162 drawn — cost-reduction pairs (Xenobiology →
Machine Bacteria / Eukaryotic Sap, and Eukaryotic Sap on to Wave Function Control / Graviton
Research, which is the one dot that reads an arc from BOTH ends) and four exclusions (Tensor
Algorithms ↔ Advanced Game Theory, Mineral Manipulation ↔ Optimized Logistics, Advanced Fusion Power
↔ Hyperium Magnetics, Orichalcix Alignment ↔ Programmable Quadrinix). **Dependency arcs
("Unlocks"/"Unlocked by") are drawn in neither fixture** — offline-tested only.
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
**Proving the quest half of the galaxy locate without a marker.** `unlocked` has six current quests
and all six report `GetMarkers(step).Count == 0`, and `GuiManager.ShowQuestLocation` makes NO
position request when there are none — so nothing lands. Make the pair of calls the game would make,
in ONE `/eval` statement so they share a frame: `RequestGalaxyOverviewViewLevel(<a system's
position>)` then `ShowQuestLocation(quest, quest.GetCurrentStep())`. `GalaxyLocate.RememberQuest`
attaches the quest to the still-fresh position request exactly as a real marker would, and the
landing speaks *"⟨quest⟩, objective shown on the map"* before the place's own readout. Enumerate the
quests with `Services.GetService<IQuestRepositoryService>().GetCurrentQuests(Gui.PlayerEmpire.Index)`
(cast each item to `Quest`; `Quest` has `State` and `GetCurrentStep()`, not `QuestState`/
`QuestStepInProgress`).

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

**Travelling the starlanes** (read-only; the three systems this recipe was written against are Dusay
`535`, Rigel `505` and Primus `543` — a TWO-hop chain, with only Dusay↔Primus and Dusay↔Rigel named
lanes. **The fixture has since advanced**: it now perceives twelve systems plus one special node, so
"exactly three" no longer holds and a longer chain may be available. The three system ids are still
right — a 2026-08-16 `/gui/graph` shows all three — but the lane keys and camera figures below were
not re-measured). A lane is a LEAF: no expansion word in `/gui/graph`, and
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
**En-route fleets and the count phrase** (rewritten 2026-08-16: destination-only now covers LANE
fleets too, not just free movers). A fleet in transit on a starlane is declared under the endpoint it
is flying TO and nowhere else, saying which of THAT system's lanes it is on. The independent oracle
is the leg itself: `IPositioningService.GetGameNode(fleet.Position.Movement.Goal)`. On
`[Beginner] test` the four lane fleets read Patriots Heracles→Osulo, Defenders Primus→Dusay, Victors
Dusay→Primus, Protectors Dusay→Rigel, and each has exactly ONE row —
`galaxy:system/491/fleet/1304`, `.../535/fleet/1447`, `.../543/fleet/1593`, `.../505/fleet/1622` —
so `POST /type defe|victo|protec|patrio` each answer `results:1` (they answered `results:2` while
both ends hosted). The counts follow the rows: Dusay says "1 fleet under way nearby" (it said 3),
Primus 1, Rigel 1, Osulo 1, Heracles none, Heka 2 (its two free movers). A fleet on a lane that is
NOT under way keeps a row under each end, which is what it always had — the rule is about transit.
The LANE node's own "what is flying this lane" phrase is unchanged and is not reconciled: a lane is
a leaf, so its count is a statement about the lane rather than about children it does not have.
Fixture-blocked: a lane fleet whose destination the map has not named, which takes the top-level
adrift row and says `on a star lane to an unexplored system`.

**The galaxy SCANNER** (read-only; `GalaxyScanner`). Drive it by action key —
`galaxy.scanCategoryNext|Prev`, `galaxy.scanSubcategoryNext|Prev`, `galaxy.scanNext|Prev`,
`galaxy.scanGoTo` — and read `/speech`. The FIRST press after a `/reload` says where the cursor
already is and moves nothing (the screen instance is new, so every reload re-arms it).

**What each tier SAYS** (2026-08-16 wording; no count anywhere in a scope line — the instance line's
"N of M" carries the size). **No press that lands on something is silent about the landing**: the
only difference between the tiers is how much of the scope is named in front of the instance line.
The ARMING press moved nothing but is still parked on something, so it says the whole scope AND that
thing, whichever tier's key armed it — measured 2026-08-17 on `[Beginner] test` from home,
`galaxy.scanNext` and `galaxy.scanCategoryNext` alike answer
`Systems: all, Dusay, 0, 0, here, 1 of 13`, and the NEXT `galaxy.scanNext` answers
`Heka, -1, -9, 9 south, 1 west, 2 of 13`, which is what proves the arming press held at index 1.
(Superseded: it said `Systems: all` and stopped, which left the player told which list they were in
and not what was in it.) A SUBCATEGORY step (Shift) says the subcategory then the landing —
`friendly, Dusay, 0, 0, here, 1 of 2`. A CATEGORY step (Ctrl) says the whole scope then the landing —
`Systems: friendly, Dusay, 0, 0, here, 1 of 2`. An instance step says the instance line alone. A
scope standing empty under a parked cursor keeps its own sentence, `⟨scope⟩, none found`.
An instance line is `name[, extras], pair, offset components, N of M`.
(Superseded: the subcategory tier said the bare label with no instance line until the owner ruled
that against it — a bare label answered "you are in an empty place" and "there are things here" with
the same sentence.) A single-subcategory category's Shift press comes round to `all` and reads its
landing too, by the same code path; fixture-blocked, since all three such categories are empty.

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

**The seven SUBCATEGORIES of systems** (2026-08-16) cycle all → friendly → neutral → enemy →
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
why the mod checks `Known` as well as the position (es2-facts). Minor factions have home systems
too (Niris/Osulo, Amoeba/Sabel, Epistis/Dyl, Yuusho/Olvaldi… nine of them) and are deliberately
NOT in this scope.

**PROBES are the third category** (2026-08-16), cycled to by Ctrl after fleets. They are the
TRAVELLING probes only — the same `_drifting` list the tree's probe rows and the inspect cell read,
so all three agree. Detection probes (no mote of their own; they surface on system labels) and
mining probes (planet-anchored) are deliberately absent. The instance line reuses the tree row's
words for what a probe is called and whose it is, plus the owner-gated countdown, and leaves the
"N turns out from ⟨star⟩" bearing to the tree row: `Probes: all, Probe, Neurrone, 4 Turn,
-55, -30, 30 south, 55 west, 1 of 1`. `galaxy.scanGoTo` opens the probe's star and lands on
`galaxy:system/488/probe/1621`; there is no select-the-thing fallback, because the game lets nobody
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
neither moves with the camera (es2-facts). Nothing about them is read off a drawn widget any more —
including the pin's DISMISS, which is now the game's own two orders rather than a button press. All
fixture-blocked; the evidence is the recomposition checked line by line against
`ObliteratorProjectileLabel.Refresh` and `CoordinationRequestLabel.SetTooltips`/`OnDismissCb`, plus
the probes' own hand-cull proof that the pattern works.

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
**Proving the reference point moves.** With the tree cursor on the HUD the scanner measures from
home; focus a system (`galaxy:system/505`) and the same list re-sorts round it. Since 2026-08-16 a
row with a thing of its OWN measures from that thing rather than from its parent system: standing on
`galaxy:system/488/probe/1621` (the probe at `-55, -30`), `Systems: friendly` answers
`Heka, -1, -9, 21 north, 54 east` and `Systems: all` then reads Osulo `2 south, 24 east`, Byrtus
`12 south, 30 east`, Electra `9 north, 38 east` — each the difference of the two spoken pairs.
**`galaxy.scanGoTo` has three landings.** In inspect mode the cell moves to the ROUNDED pair and
reads out ("0, -3, 1st Vanquishers Navy") with `DevProbe.Camera()` focus at
`origin + (x, y)` exactly. Outside it, a system lands the tree cursor on `galaxy:system/<guid>` with
its ordinary announcement, and a lane fleet opens its host branch and lands on
`galaxy:system/535/fleet/1622`. A FREE-MOVEMENT fleet lands the same way, on its DESTINATION's row
(2026-08-16): `galaxy.scanGoTo` on `1st Conquerors Navy` opens **Heka's** branch and lands on
`galaxy:system/522/fleet/1570`, heard as "1st Conquerors Navy, -1, -6, free moving to Heka,
1 ships, Moving to Heka, 0 movement points, Arrives in 2 turns, 8 of 9" (no role word —
it is an automated fleet, below). There is only
one row to pick now — the source branch no longer holds one. Move the tree cursor somewhere else
before pressing it: landing on the node the cursor is already on is silent, and after a type-ahead
for the same fleet that is exactly where it stands. **Fixture-blocked**: the `SelectFleet` fallback
(camera + fleet panel + the scanner's line spoken again) is now reachable only by a fleet PARKED at
a system the map does not name or flying a lane the map does not draw — a free mover always has a
row, top-level if its destination is unperceived (`AddAdrift`). No fixture produces either.

**Free-moving fleets in the galaxy tree** (`[Beginner] test`, destination-only since 2026-08-16;
the both-ends design shipped earlier that day was reversed — see es2-facts). The two free movers
both fly Dusay → Heka, and each gets ONE row, under Heka: "1st Conquerors Navy, -1, -6,
free moving to Heka, 1 ships, Moving to Heka, 0 movement points, Arrives in 2 turns,
8 of 9" and the Vanquishers the same at `9 of 9`. **No role word on those two** — they are automated
fleets and the map refuses them to the mouse, so the mod declares them `ControlTypes.Text` (below).
Dusay's branch holds its three LANE fleets and
nothing else, and its count says "3 fleets under way nearby" (it said 5 while the source rows
existed); Heka says "2 fleets under way nearby". Type-ahead answers `results:1` for `vanq`/`conq`
now, landing on the Heka row (it was `results:2`).
No fixture here has a fleet IN ORBIT (all six read `Position.IsInOrbit == false`), so the parked-fleet
rows (`AddFleets`) cannot be exercised live in this save; and no fixture produces a free mover with an
unperceived destination, so `galaxy.fleet-free-moving-to-unexplored` and the top-level `AddAdrift`
row it belongs to have never been heard.
**The fixture's two free movers are AUTOMATED delivery fleets** (`Fleet.IsAutomated` true — probed
2026-08-16; the other four are false), which matters twice: the game counts an automated fleet's
ships whatever the visibility (`GarrisonsLabelButton` :210), so they are the wrong fleets for
testing the ship-count gate — use `1st Protectors Navy` — and `GalaxyFleetCursorTarget`
refuses both selection (:17-24) and highlight (:26-33) for one. **Since the owner ruling of
2026-08-16 the mod says so**: `FleetNode` declares an unselectable fleet `ControlTypes.Text` with no
`OnActivate`, so the row carries no role word and Enter is a no-op. The regression check is one line
of a branch dump — the Heka rows must read "…, -1, -6, free moving to Heka, …" with no "button",
while Dusay's three lane fleets (not automated) must still read "…, 12, 15, button, on starlane 1,
northeast, …". A build where all five say "button" has lost `FleetPresence.Selectable`; one where
all five drop it has the predicate inverted.
**Minimizing the tutorial popup, the step every galaxy session starts with.** `POST /input` does not
work on `screen.tutorial` — `ui.down`/`ui.right` answer `unconsumed` and `ui.end`/`ui.next` answer
`consumed` while moving no cursor and speaking nothing (measured twice, 2026-08-16). Invoke the
game's own handler instead: `TutorialPopupPanel.OnMinimizeCb`, private, no arguments, by reflection
from `/eval`. Take the panel from `FindObjectsOfType<TutorialPopupPanel>()` and pick the one that is
`IsBound && Shown` — `FindObjectOfType` (singular) handed back an unbound instance on one launch and
`OnMinimizeCb` threw an NRE inside itself (its `tutorial` field is null there). Guard on
`MinimizeToggle != null && !MinimizeToggle.State` so a re-run is idempotent.
**A MULTI-page tutorial popup is fixture-blocked in `[Beginner] test`** (its only in-progress
tutorial has one page); `[Midgame] quests fleets` has the 6-page `Tutorial_Fleets` in progress —
selecting a fleet in the galaxy tree raises it. Page counts per tutorial:
`Public/Gui/GuiElements[Tutorials].xml`; the in-progress set is
`DepartmentOfInternalAffairs.QuestJournal[QuestState.InProgress]` filtered to
`TutorialDefinition`.

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

**Forcing a send REFUSAL to hear its reason.** No fixture state refuses a move, so flip the game's
own cheapest gate for the duration of one handler call and restore it in the SAME `/eval`:
`GameNode.IsLocked = true` on the destination answers `SystemIsBeingFrozen` (pathfinder-side), and
the private `StarSystemNode.empirePlanningEncounterIndex = <empire index>` on the fleet's own orbit
answers `EncounterInPreparation` (action-side — this one proves the three-argument
`CanBeExecuted`). Invoke the node's real `OnContextual` delegate by reflection, read `/speech`,
restore, and re-load the pristine save if a positive control posted a real order.

**Lowering a fog state reversibly.** `EntityExploration.SetState` refuses to lower, so write
the byte directly: `node.Exploration.GetCurrentStates()[empire.Index]` is by-reference — set
it, dump the graph or invoke the predicate, put the original back in the same `/eval`.
(Proved the lane gate both directions: a PartiallyRevealed link dropped to Identified left
the tree; restored, it returned with its original numbering.)

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

**Free-aiming a probe by compass** (arm through the fleet-actions stop — that list walks
with Left/Right, not Up/Down; landing is the "Launch towards" group, last in the system
branch): End/Right/Down to a bearing, `ui.activate`; oracle
`DepartmentOfDefense.Probes[i].Direction` against the unit vector (X=east, world-z=north).
Primus's lanes run NE/SW/NW, so N/E/SE/S/W are lane-free bearings. Anchor-migration and
at-star cases: set `Probe.GalaxyPosition` from `/eval`, restore by `/loadsave`.

**Confirming a targeting mode on a LANE** (the probe-down-the-dark-lane repro): arm the
cursor from `/eval` with `Fleets[0]`, focus the lane node (`galaxy:system/543/lane/662` on
Primus), `POST /input ui.activate`. The direction oracle is
`DepartmentOfDefense.Probes[i].Direction` against the far node's bearing from the fleet.
Do NOT oracle `GameOverlayTooltipPanel.Label.Text` after a reflected `OnCursorEnter` — it
keeps whatever an earlier cursor broadcast (the panel is a subscriber; es2-facts); the
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

**Reaching the route-loss watcher's endings.** No fixture produces a real interception or
invalidation, and both are reversible only by `POST /loadsave`: order a fleet out, then from
`/eval` either `fleet.SetPath(null)` (expect "The route of ⟨fleet⟩ to ⟨dest⟩ was cancelled") or
`HasBeenIntercepted = true` first (expect "⟨fleet⟩ was intercepted at ⟨system⟩", and no
cancellation line). The negative pairs matter as much: a normal arrival and a route REPLACEMENT
(re-select the fleet — sending cleared the selection — and Backslash a new destination) must both
stay silent, checked with a `/speech?since=N` window.

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

**The game's own "show me this fleet", and what the galaxy page does when it lands.** The repro for
"the map snapped back to where I was" needs the locate to happen while the galaxy page is DOWN:
focus a system node (`Navigator.FocusNode(ControlId.Structural("galaxy:system/<GUID>"))`, which
pans the camera there), `Gui.GuiService.ShowWindow<MilitaryScreen>()`, then run the locate from
es2-facts (`ICursorService.Select(galaxyFleet.CursorTarget)` → `ChangeCursor(typeof
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
selection — es2-facts). A QUEST PIN needs a quest with markers, which neither fixture has: compose
the game's own nesting in one `/eval` statement instead — `RequestGalaxyOverviewViewLevel(pos)` then
`ShowQuestLocation(quest, quest.GetCurrentStep())` on the marker-less pinned quest — which leaves
exactly the state `ShowQuestLocation` leaves when it does have one, and must say
"⟨quest title⟩, objective shown on the map" before the landing. The same call ALONE on a marker-less
quest must be silent.

**A table's double click** (`TableSheet.ShowOnMap`, every table): from `unlocked`,
`Gui.GuiService.ShowWindow<EmpireScreen>()` lands the cursor on the Systems Management table's row;
`POST /input ui.doubleClick` must open that system's management page (heard as the tutorial popup
plus "Zoom level 14 of 15", confirmed by `/gui/graph` reading `Star system`). Note every
`GuiTableLine` in the game carries a `DoubleClickButton`, so the absent-means-silent guard is not
reachable from a fixture — the silent tables are the ones whose client handler is empty (es2-facts).

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
**Sighting the spaceport side panel**, which no save draws (es2-facts: `IsAvailable()` needs
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

**Opening the star system page.** `GalaxyViewLevels.OpenSystem(Gui.PlayerEmpire.GetAgency
<DepartmentOfTheInterior>().ColonizedStarSystems[0].Node)` from `/eval` (Dusay, GUID 535 in the
fixture; `GameEntityGUID` is NOT in `Amplitude.Unity.Game`, so go through the node). The page
arrives in pieces — the side panels a frame or two before the planet cards — so a screen that
declared the half that existed seated the cursor on the wrong stop for good. Here the late
half (the cards) is the page's FIRST stop, so waiting for it is still right — but per the
tightened rule (making-screens-accessible.md §3), the gate protects the cursor's seat, not
the page: a page whose early half is usable declares it, and the planet page's lost-card
repair (`Nudge`) is the other half of that story.

**The management-view node's negative control.** "Unowned systems gain nothing" is only proved by a
system whose label button is VISIBLE and inoperable — an invisible button passes for the wrong
reason. Sweep the labels with
`GetWindow<StarSystemLabelsWindow>(false).GetComponentsInChildren<StarSystemLabel>(true)` and print
`StarSystemNode.LocalizedName`, the `RequestManagementViewButton` visibility chain, `.Enable`, and
the gate under test; only the camera's own system and its neighbours read `vis=True`. Expanding a
system ZOOMS to it, so two systems expanded at once is not an A/B: the one the camera left stops
drawing its label and loses its `Open system` child.

**The constructible filters are a safe round trip.** They are one select-one group the panel
re-derives from `SelectedConstructibleFilterName` on every refresh, so Enter on another filter and
Enter back on "All" leaves the fixture as found — nothing about the system or its queue moves. The
grid under them changes with the pick, which is the cheap proof the pick landed.

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

**Multi-row tables** need a real fixture with several saves/rows — do not mutate the game's
data structures to fake one. **Reading a tooltip the game only writes for somebody else**: a
content-backed tooltip the fixture leaves empty (the orbital card's `OutpostTooltip`, written
only for a FOREIGN outpost) is still provable — set `.Content` from `/eval` to what the game
would write, focus the node, read `/gui/graph?buffers=1`; the card's next refresh blanks it
again, so nothing is left behind.

**Opening game modals from /eval** (to measure one without walking there): set what its opener
sets, then show it — for the improvements list, `var w =
Gui.GuiService.GetWindow<ImprovementsManagementModalWindow>(); w.ColonizedStarSystem =
...ColonizedStarSystems[0]; Gui.GuiService.ShowWindow(w);`. Close the way Escape does:
`w.HandleInput(InputAction.Exit)`. A modal whose opener installs DELEGATES has to be opened
through the opener's own handler — reflection for a private `Cb(GameObject obj = null)`, since
`SendMessage` with no argument logs an arity error and does nothing (es2-facts); the worked
route for `SystemSelectionModalWindow`, with its never-press warnings, is below. The two
text-box surfaces, for a keyboard-focus probe: `GetWindow<LoadSaveModalWindow>()`, set
`LoadSaveMode = LoadSaveType.Save`, `ShowWindow`; and `GetWindow<RenameModalWindow>()`, set
`OriginalName`, `ShowWindow` — at rest on BOTH, `AgeManager.Instance.FocusedControl` is null and
`DevProbe.Claims("Return")` is `claims:true`, the "game is not holding the keyboard" check the
edit-field defects turn on. The chat page without a chat key: `ChatHold.OpenOnTheBox(panel)` by
reflection on the loaded assembly opens the panel AND pushes the chat screen; stepping out of
typing (`FocusedControl = null`) closes both — the game's own validate on an empty box shuts the
panel, leaving nothing behind.

**The rename box.** It walks heading / field / Cancel / Confirm. Its openers are the star-system
name line (the Colony panel's name node opens it directly), the planet card's rename button
(unreachable on a unique home planet) and the fleet panel; close it with
`window.HandleInput(InputAction.Exit)`, which is the route the key takes. A `ColonyInfoSidePanel`
found via `GetComponentInChildren` on `StarSystemScreen` has a NULL `RenameButton` (wrong
instance) — drive the opener through the mod's node. **The whole edit-field round trip is
`/key`-driven end to end** (since `POST /key`, 2026-08-17): `/key Return` → "editing", surface
stays; `/key?text=1 "zz"` → per-character echo; `/key Return` → "edited", surface STILL up, text
kept (the commit no longer performs the screen's action — saving and renaming are the Save and
Confirm buttons now); `/key Escape` → pre-edit text restored + "Cancelled"; a second `/key
Escape` closes the box without acting. `/key` arrow names are `UpArrow`/`DownArrow`/`LeftArrow`/
`RightArrow` (`Down` alone is rejected with the full vocabulary). The levers
(`DevProbe.EndEdit(bool)`, `DevProbe.ArmCommit()`) remain as the FALLBACK for a locked desktop,
where `/key` answers 409. `AgeManager.Instance.FocusedControl = null` from `/eval` is also the
cancel path (restore + "Cancelled"); prove Escape consumption with `DevProbe.Claims("Escape")`,
and the latch half with `POST /wait` on `ModEntry.Input.ConsumedKeys.Count > 0`. Reaching a mod
internal from `/eval` needs the loaded-from-bytes assembly by name — scan
`AppDomain.CurrentDomain.GetAssemblies()` for the `modAssemblyName` that `/status` reports, then
`GetType` off it; the plain type name does not resolve. Fixture blocks for the numeric
editables: `[Beginner] test` refuses the Marketplace tab (no Galactic Commodities Exchange), and
the negotiation quantity needs a diplomacy contact no current fixture has. The heading's TEXT
depends on the opening route: the `/eval` `OriginalName`+ShowWindow route draws it empty, the
player gesture draws "Enter a new name for your System …" — do not chase the empty heading as a
defect off the `/eval` route.

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

**The system-politics modal.** Open it from the star-system page's own node. "Show all events" is
persistent WINDOW state — restore it — while the party pick is not. The table binds
`canSelect:false`, so nothing in it commits. In `[Beginner] test` the representatives panel's
"Shows in detail…" button is unavailable, so the modal has no player-gesture route there —
`Bind(ColonizedStarSystems[0])` + ShowWindow stays the only in, and it raises BREAD AND CIRCUSES
(minimize after).

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
`election:local/support` declares `election:local/{title,trends,empire}` regions and pushes the
`PoliticsSupportGroup` caption over the party bars; the caption child's name is a guess guarded by
`string.IsNullOrEmpty` — on the first real election, check whether the bars arrive under a word or
bare, and walk the wizard's flattened bands (all code-only so far).

**The vote breakdown (step 1) can only be tested on a real election turn**, i.e. on the owner's own
save, and it is then the ONLY test surface — so treat it as live and non-disposable: **never press
Next Step, Skip, or Escape** (Escape raises the skip confirmation), and never `POST /loadsave` or
`/quit` out of it. Safe: `/reload`, arrow/Tab injection, `/gui/graph?buffers=1`, and the Prev/Next
system buttons (which are switched off on a one-system empire). Sighted 2026-08-16 on a
one-system empire (Dusay, 4 representatives). What the dump should show: one row reading
`Dusay / Industrialists 1 / Scientists 2 / Militarists 1` (the parties keep their own nodes and
their own class-backed dossiers — focus each and re-dump with `buffers=1`, since an unfocused
class tooltip reads empty), a Political Trends row per available party as "party, N of total", the
Overall Empire line, and "N of M representatives counted". Read the trends pairing back against
`/eval` on the panel's private `starSystemElectionInformations` (es2-facts) — the bars themselves
carry no text to check against.
Fixture-blocked on a one-system empire, and only there: the carousel auto-advance and the mod's
stop for it (the coroutine exits immediately when the index is already the last, so the flag write
is provable by reflection but its effect is not), the row re-reading on Next System, the progress
line moving off "M of M", and any Political Trends entry whose cumulated count differs from the
first system's.

**The result step (step 2)** has the same live-only constraint and the same never-press list, with
one addition: on the LAST step Escape presses Finish, and the law cards' `DoubleClickButton` must
not be activated either. Safe: `/reload`, arrow/Tab injection, `/gui/graph?buffers=1`,
`DevProbe.Tooltip()` with a delay set (restore `TooltipDelay(-1)`). Measured 2026-08-16 on the
owner's save (two winners, one redirection badge each). What the dump should show: one ROW per
winner — `Militarists, Established` then `+Industrialists, The votes for the
" Industrialists" political party have been redirected to the " Militarists" political party.` —
never the glued `Militarists Established +Industrialists`. The winner's place is stamped as a ROW
position, so "1 of 2" is heard arriving at a card and on stepping to the other winner, and NOT on
walking out to that winner's badges; the winner rows share a row key, so Down from a badge lands on
the next winner's badge. The card's focused buffer holds the party dossier AND the experience
sentence ("Reflects the experience gained by this political party…"); the badge announces its own
sentence and buffers it. Check the badge's tooltip really draws with `DevProbe.Tooltip()` while it
is focused (`shown:true`, one `PanelFeatureSimple` feature) — the card's own tooltip hangs off a
child, so a pointer aimed at the card draws nothing.
Fixture-blocked on that save: a winner with several badges or with none beside one that has them,
the senator-hero card variant (`HeroExperienceGroup`), the experience-GAIN gauge, an experience
tier other than "Established", and the election-action outcomes (never drawn — es2-facts).

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

**Inspect mode** (`galaxy.inspect` / `galaxy.inspectGrow` / `galaxy.inspectShrink` + the ordinary
`ui.*` actions, all through `POST /input`). Entry lands on the focused stop's OWN pair (2026-08-16:
it used to land on the parent system's, because a fleet/probe/missile/pin row is keyed structurally
and the walk only read `ControlId.Reference`, which only a system's node carries —
`GalaxyHudScreen.cs:1657`). Measured: focus `galaxy:system/491/fleet/1304` (the Patriots, `-37, -31`,
under Osulo at `-31, -32`) and `galaxy.inspect` answers `-37, -31, 1st Patriots Navy, Star lane from
Heracles to Osulo` with `DevProbe.Camera().focus` at `(31.884, 0, -53.45)` = origin + (-37, -31). A
row with no thing of its own (a planet, a lane) still answers with its system, and entering from
the empire stop (no place under the cursor) gives home. So entering from the empire stop and from
`galaxy:system/535` (Dusay) both give "0, 0" on
`[Beginner] test` — pick a non-home system to tell the two apart. The measured expectations there:
entry says `Inspect mode, Cursor 1 by 1` (default since 2026-08-19; the size persists per
session, so a re-entry repeats whatever was last set) then `0, 0, Dusay, Star lane from Rigel to Dusay, Star lane
from Qarius to Dusay, Star lane from Dusay to Primus`; the cell lists systems, special nodes,
fleets, probes, obliterator missiles, ally pins, then the lanes crossing it and the fog — the three
open-space kinds in the tree's own declaration order (`AddProbes`/`AddProjectiles`/`AddPins`), off
the page's own `DrawnProbes`/`DrawnProjectiles`/`DrawnPins` lists, so the cell and the tree cannot
disagree about what the map is drawing. `ui.right` twice then `ui.up` twice reads
`3, 0` / `6, 0` / `6, 3` / `6, 6 Star lane from Dusay to Primus` — that last cell is the
lane-crossing check against known geometry (Dusay 0,0 to Primus 16.5,20.9 enters the cell centred
6,6 and misses the one at 6,3). Empty cells say the pair and nothing else. The camera is the exact
oracle for the pan: `DevProbe.Camera().focus` must equal `GalaxyCoordinates.Origin() + (x, 0, y)` —
`(74.884, 0, -16.45)` at cell (6,6) with home at `(68.884, -22.45)`. Edge refusal: the galaxy's node
bounds are x `[-164.0, 22.8]`, y `[-41.5, 88.3]`, so at 11x11 from (6,50) one `ui.right` reaches
(17,50) and the next answers `Map edge` (it said `Edge of the galaxy` before 2026-08-16; measured
again eastward from (0,9) at 11x11 — (11,9), (22,9), then `Map edge`).
Fog: (6,50) at 11x11 is `Unexplored` whole, (6,6)
is `34 squares unexplored` — grow/shrink and the count tracks. **The mode's state probe is
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
Enter made, and leaving the map. Enter on a one-place cell lands on `galaxy:system/<guid>`
with the tree's own announcement; Enter on a fleet-only cell (shrink to 1x1 and walk to `-1, -6`,
`1st Conquerors Navy`) answers `Fleet panel open for …` — but only from a CLEAN cursor: with a
`GalaxyGarrisonCursor` already up holding nothing, the selection did not take (seen once, unexplained;
`cursors.ChangeCursor(typeof(GalaxyCursor), Gui.GetCursor())` resets it). Auto-exit is driven with
`Gui.GuiGameWindowService.RequestStarSystemManagementViewLevel(guid)` — every keyboard route out of
the map is claimed by the mode itself, so an engine call is the only way to stage it — and the exit
line must land AFTER the whole arrival burst (`Star system`, `Zoom level 14 of 15`, `Planets, Raia…`,
then `Exited inspect mode`). **Leaving takes the CAMERA back too** (2026-08-16): the exit that returns
focus re-centres on the position the mode was ENTERED at (`_entryAt`, taken on the way in — the
cursor is re-seated in the same breath, so asking the navigator again would read the old node). A
landing that Enter made does NOT re-centre, or it would fly the camera off the thing just landed on.
Measured: enter at the Patriots row (camera `(31.884, 0, -53.45)`), `ui.up` eight times to
`(31.884, 0, -29.49)`, `ui.back` → camera back at `(31.6, 0, -53.3)` and focus back on
`galaxy:system/491/fleet/1304`. (The companion measurement — entering from the empire stop, walking
to cell (22,75) and coming back to home — is retired with the off-map jump it depended on.)
**THE DRAWN SQUARE IS A MOD OVERLAY, NOT A BORROWED RENDERER (2026-08-17)** — every `_outline`
line-renderer probe in this recipe is retired with the mechanism. The mark is
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
buffer lines and cannot see an override. Measured: at (0,0) `0, 0` / `Dusay` / the three lanes;
at (0,-3) `0, -3` / `1st Vanquishers Navy`; off the map `Galaxy View`; after Enter, the landed
row's own lines. **Enter on a fleet cell lands on the fleet's own tree row** through
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
1×1 at (-25,-41), Down must land "-25, -42, Byrtus" and a second Down "Map edge". Camera zoom for the size/zoom matrix is set directly:
`cam.ForceZoomingOnPosition(step, cam.TargetPositionCurrent)`, step 0 = full overview, 12 = closest
(`ZoomStepsCount` 13).
Fixture-blocked in the cell reading, for the same reasons the tree's own nodes are: an obliterator
missile (none drawn) and an ally coordination pin (no alliance) — both share the cell's enumeration,
visibility and wording with the tree, so the tree's route is the only place either can be sighted.
**Visual evidence: crop the screen centre, at every zoom.** The cell's centre is
`GalaxyViewCameraController.TargetPositionCurrent`, which the mode keeps at the screen centre, so a
crop is aimed by halving the window rather than by projecting corners. The window is **1280×800**
(`UnityEngine.Screen.width/height`, and the `/screenshot` frame agrees), centre **(640, 400)**; a
`-Rect 500,280,280,240` frames any cursor size. Measured 2026-08-17 with the mod-drawn overlay: the
square is pale cyan with a dark backing band and is unmistakable at **1×1 at zoom step 0** (the case
every borrowed renderer failed — it draws at its ~26 px floor, centred on the cell), at **1×1 zoomed
in**, at **11×11 at step 0** and at **3×3 at the default step 5**; at 11×11 CLOSE IN the box is still
wider than the viewport, because the mode's camera does not zoom out with the cursor. The old advice
in this paragraph — never pull the camera back, because short lines vanish — died with the line
renderer: the overlay is screen-space and any zoom is now a fair test. The focused node's own tooltip
is drawn over the middle of the map: it can no longer HIDE the square (IMGUI draws over it, which is
the non-occlusion evidence) but it makes an ugly crop, and
`/eval ES2Access.UI.PointerFocus.Release()` after the last `/input` clears it, the next focus change
putting it back.

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
and again at 11↔12, and nothing but the slider value inside a band). Two states DO leave
it valueless or absent, both shared with the plain HUD and both by design: `ZoomLadder.Text` returns
null while the ladder waits for a level the game has not moved to (the announcement keeps the name
and loses `N of 15` — seen on the refused `ui.right` at rung 13), and `ZoomLadder.Build` declares
nothing at all where `GalaxyViewLevels.ZoomRung < 0` (a battle lens or the system-discovery view). The zoom table: 0-1 Diplomacy /
2-5 Trade / 6-9 Economy / 10-12 System, plus the system and planet layers — six titles over nine
descriptors, so the band boundaries are one rung finer than the titles (es2-facts). **All four lens windows
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
unexplored — no lane TRAVEL is testable there; `[Beginner] test` is the fixture for that.

**Probes and faction panels (the other galaxy labels).** The unlocked save draws NONE of this
surface. A probe row is exercisable with `probeLabel.Show()` then `Hide(true)` — self-healing. The
faction panels need `Bind` + `Show`, then `Hide` + `Unbind` **and** `InspectedEmpire` restored
through its private setter: `Unbind` leaves the game's own `Refreshed` handler live, which NREs on
the next refresh otherwise.

**The senate family** (senate, government, laws, population). Open it from `/eval` with
`ControlBanner.OnControlBannerToggle`; reach the modals through the mod's own nodes. The government
modal also opens directly — `Gui.GuiService.ShowWindow<SenateScreen>()` then
`Gui.GuiService.ShowWindow<GovernmentModalWindow>()`, closed with `HideWindow` — which is how its
Validate button's hint state is probed (`Gui.IsHintActive(w.ValidateButton)`; es2-facts says why it
reads false here). **NEVER press
Validate, Pass, Abolish, a boost, or Assimilate.** The selection resets on every show, so nothing
carries between visits. Expect a ~1 s `unavailable` on the page under a just-closed modal — that is
the game's fade, not a defect; re-read. **Save-blocked**: the gene hunter, assimilation, relics, a
real election, an enabled Abolish, a drawn history graph, an empty senator slot, and the outpost
panel.
Since the one-per-row rollout (2026-08-18) every band in the senate family is one node per row — a
`left ->`/`right ->` edge under a `senate:`/`government:`/`laws:`/`population:` stop is a
regression. Ten caption ids became level labels (lists in the session's batch-4 report); the words
arrive prefixed onto each block's first node. Regions to expect: `laws:detail/{law,effects,action}`;
`population:detail/affinity`, `population:thresholds`, one per captioned block,
`population:detail/assimilate`; `population:politics/{intro,traits,reactions}`.

**The empire page.** The interactive cells are columns 1/2/4/11/13. Nothing closes an opened band
except leaving the page. The tab switch and the panel instances are both probeable from `/eval`;
`SidePanels`' `PanelTitle` branch first got exercised here. `ui.end` does not move inside a
`GraphSheet` row (it answers consumed and speaks nothing) — walk columns with `ui.right`.

**The economy page and the recipe modal.** Which rows draw at all is the stage-8 gate table (this
save is screen-unlocked, not tech-unlocked) — which also means the **Marketplace tab is refused**
(missing Galactic Commodities Exchange), so the buy table has NO fixture in `unlocked` and the
resources grids are the only economy tables that can be walked. The recipe modal is reachable with zero slots via
`new GuiRecipeSlot(0,false)` + `ShowWindow`. **NEVER press Confirm** — it is enabled even with an
empty recipe and posts `OrderCreateRecipe` — and note Reset does NOT clear `RecipeModified`.
Both luxury grids are a legend region plus an items region in ONE stop since 2026-08-18; each item
trails its FIDSI family word, and Giga Lattice sits under Influence (item index 12). The recipe
modal's legend reads the economy screen's short titles ("Industry"), not the family descriptions.
The modal's project stop draws ONE slot in `unlocked` (multi-slot strip unmeasured); the strategics
grid is drawn by no save.

**Military and fleet-selection.** **Never press Retrofit**: it is immediate, with no confirmation.
A ship tile's SECOND click (Ctrl+Alt+Enter) opens that ship's design read-only and is a safe round
trip: `Gui.GuiService.ShowWindow<MilitaryScreen>()`, pick a fleet row (Enter) to draw the ship list,
Tab twice, `ui.doubleClick` → "Ship Design: Settler", close with
`Gui.GuiService.GetWindow<ShipDesignModalWindow>().HandleInput(InputAction.Exit)`. The first open
raises the Architects tutorial — minimize it. The star-system HANGAR is empty in `unlocked` ("No ships
in the hangar"), so the second host for that gesture is the selected-fleet panel, reached with the
`GalaxyFleet` select route in es2-facts.
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
the save. Page switches raise tutorial popups — minimize them. Cheaper than `/eval`: the whole
family — overview → ship design page → skill tree page — walks from the Academy's own Inspect
button with `/input` in `unlocked`; each page switch raises a tutorial to minimize.

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
selected tab across opens — put the tab back when done. The whole family also walks from the main
menu with `/input`: Content opens the DLC browser, Mods opens the mod manager (its Back node
closes it), and the Mods flyout's "Game asset export" child is the exporter's player route — no
`ShowWindow` needed for any of the three.
**The asset exporter** (`ShowWindow<ResourcesExportScreen>()`): never press either export button
(they write files) or Open folder; progress is drivable by setting the panel's private
`lastMessage` + the private `ExportInProgress` setter, restoring both; a row's click goes through
reflection on `OnResourceExportPropertyItemClick(int)` — `SendMessage` from `/eval` drops the
argument; the page reloads its manifests every visit, so wait for them. Known game bugs:
`ResourceExportPropertyItem.Refresh` NREs on some assets (page stays "No asset selected"), and
re-entering resets the filter TICKS without firing their callbacks, so the ticks can contradict
the drawn list until one is toggled. Selecting an asset with `ui.activate` on a row is safe and
DOES draw the `resources-export:export` band even though `Refresh` NREs and the Selected panel
first reads "No asset selected" — the panel fills a moment later.

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
Game — and `((GuiManager)Gui.GuiService).ShowMessageNonBlocking(message,
MessageBoxType.INFORMATIVE, null)` raises the non-blocking box (dismiss via Cancel; enum members
are UPPERCASE). Both work from the main menu too.
`Gui.GuiGameWindowService.RequestStarSystemManagementViewLevel(new GameEntityGUID((ulong)<id>))`
opens a system's page from `/eval` — the `ulong` cast is required or the constructor is ambiguous;
`ToggleScanView()` is the scan view's in and out. In `unlocked` the star system page itself binds one
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

**The whole chat page, in an ORDINARY single-player fixture** — no solo-MP lobby needed, because
the in-game panel is live in single player (es2-facts). Seed the log through the game's own send:
`p.ChatTextField.ReplaceInputText("…")` plus the reflected
`InGameChatPanel.OnTextFieldValidateCb` (a `null` argument), where `p` is
`Gui.GuiService.GetWindow<InGameChatWindow>(false).InGameChatPanel`; each call posts, echoes back
through `OnChatMessageReceived`, and speaks. A loop of 55 in one `/eval` proves the fifty-row bound.
**Chat is a CHILD SCREEN now (2026-08-14), not a stop on the page** — no `ui.prev` reaches it, and
the whole round trip is drivable without a keystroke: `p.SetFocus()` is Ctrl+Tab (expect
`Screens.Current.Key == "screen.chat"`, `/gui/graph` declaring nothing while the box holds the
keyboard, and one line of speech); `p.HandleInput(InputAction.Exit)` is the first Escape and must
answer `true` with the cursor landing on `chat:message-box`, the panel still non-discreet (crop it —
`p.AgeTransform.GetGlobalPosition()` gives the rect) and `DevProbe.Claims("Escape")` reading
`claims:true`; `/input ui.back` is the second and must return the cursor to the exact pre-chat node.
`/input ui.next` on the chat page is consumed and SILENT (one stop) — that is the containment check.
Re-entry is `ui.activate` on the box node, then `AgeManager.Instance.FocusedControl ==
p.ChatTextField` for the hand-over and `DevProbe.Claims("Escape")` → `layerLive:false, claims:false`
for the stand-down; the reflected `OnTextFieldValidateCb` on an EMPTY box is the game closing chat
underneath the page, which must pop it and land the cursor back. Restore the fixture with
`Services.GetService<IChatControllerService>().RemoveMessages()` plus the reflected protected
`ChatPanel.ClearLines`, then one `/reload` so the chat review buffer reseeds from the now-empty
history. The alliance tab and the new-message button stay unreachable in single player by the game's
own hand (no alliance; a Global line on the Global tab never raises the button) — the solo-MP lobby
above is still the only fixture for those.

**Notification regression capture.** Any change to the notification family is checked by walking a
fixed browse route over all three research-family popups and diffing `/gui/graph?edges=1&buffers=1`
per popup, per the exact-non-regression pattern above.

**The parity probe is the family's own check** — `DevProbe.NotificationParity()` on whatever popup
is up: painted-but-unspoken text, spoken-but-undrawn words, misfiled or out-of-order nodes, tooltips
promised or lost, each an array that is empty when the popup is clean (`clean:true`). It runs by
ITSELF on every popup shown while the dev server is on (two ready frames after the popup settles)
and `Log.Warn`s one line per invariant broken — `/log?grep=parity` is the whole readout. The
settle delay is load-bearing: on the first ready frame the quest popup counted one body item more
than it does two frames later, which moved every row's position ("3 of 5" for a row that settles at
"2 of 5"). Seeds that prove it still bites, each restored by its inverse (verified 2026-08-15 on
`QuestBegunNotificationWindow`): **honesty** — `Alpha = 0` on a painted label the mod reads
(`StatusTitle`), since the screen's own `Collect`/`Draws` ask `Visible` and never alpha;
**completeness** — `Visible = true` on `ObjectiveLoreLabel`, a second paragraph in the lore scroll
view that the lore row does not read (a live gap: a quest that shows two lore labels loses one);
**placement** — move `PinToggle` into `TitleGroup` (private `parent` field plus both `Children`
lists, restored to index 3), which `Sort` sweeps into the top strip; **tooltip parity** — add an
`AgeTooltip` with `Content` to a card's `DoubleClickControlButton` (**and set `privateTooltip` by
reflection** — es2-gui-framework: the runtime getter reads the cached field, so a plain
`AddComponent` is invisible to the engine as well as to the mod). The one direction NOT seedable is
a declared-tooltip expectation with nothing to draw: the expectation and the check are the same
`AgeWidgets.Draws` predicate, so game state cannot separate them.

**The Laws Cancelled popup reads as a SHEET** (post-fix shape, 2026-08-17): region
`notification:table:reg:0`, row key `notification:table:row<hash>c0`, cell `…c1`, captions
"Laws"/"Political Ideologies" spoken as the crossings, and the law's dossier on the ROW (the
class-backed `Law` tooltip draws on focus and fills the buffer). Fixture notes: no route exists
to re-summon a laws-cancelled popup once dismissed, and a raised popup cannot displace an open
one (`ShowGuiNotification` does not bring it to front) — regression checks on this family wait
for a naturally pending one. The research/construction table popups (the other sheet-reading
family) still owe a walk from before the wrapper-descent change.

**Sweeping the whole family** (the apparatus lives in `%TEMP%\parity`: `tmpl.txt` + `run.sh` for
bind-and-show, `tmplfs.txt` + `runfs.sh` for lent-data force-shows, `reduce.sh` to collapse the
JSONs outside context; one `/eval` handle bank per session, re-issued after every `/reload`). Three
traps, each measured 2026-08-15:
- **The template probes the popup that is still up and shows the next**, so the LAST line of a run
  is never probed — repeat a harmless window as the final row.
- **A first show under-counts painted text.** `TechnologyUnlocked` read 15 painted strings 1.3 s
  after its first show and 19 on a later one: its `AnimateOnEndShowTransforms` bring the unlock
  cards in one at a time, and the probe flagged four honesty rows for captions that were simply not
  drawn yet. Wait ~2.6 s, and re-probe any window that flags before believing it.
- **"System" is a substring.** The completeness check accounts a painted string that appears
  anywhere inside a spoken line, so a caption like "System" is accounted or not depending on
  whether a Class-backed tooltip happened to be drawn into a buffer when the probe ran. A finding
  that comes and goes between runs of the same popup is this, not flakiness in the reader.
- `RelicsCollectionCanceled` cannot be raised with `ReasonOfCancelation.CanceledByPlayer` — its
  `Bind` rejects exactly that reason (`NotificationRelicsCollectionCanceled.Bind`); use
  `FleetDestroyed`.

**Settled state, 2026-08-15** (two consecutive full sweeps, 63 window types, byte-identical
reductions): every type reads `clean:true`. The last two that did not — `ConstructionCompleted` and
`PopulationChange`, each reporting its sheet's FIRST-column caption ("System") as painted-but-unsaid
— were closed the same day by the owner's consistency ruling: `GraphSheet` now labels the crossing
into column 0 with that column's header like every other crossing, and every sheet adapter passes
its primary caption (`NotificationScreen.BuildSheet` no longer drops `Headers[0]`). Re-verified live:
both popups walk Right/Right/Left/Left as "Completed …" / "Next Construction …" / "Completed …" /
"System, Dusay, …" (and "Population (Affinity) …" / "Population Change …" / … / "System, Dusay, …"),
both `clean:true` with no completeness rows — the crossing is credited by the probe's own
`AddCrossings`, so no exemption was ever needed. Fixture-blocked and therefore never swept: `PirateMissionReport`
(below) and the five battle-stack types (`BattleSetup`, `BattleReport`, `GroundBattleSetup`,
`GroundBattleReport`, `HackingOperationOutcomeSelection`), whose `Bind` throws without a live
encounter or hacking operation and which no force-show has yet been able to show.

**Reading the description path without a popup up.** The three answers that decide a popup's words —
`DescriptionLabel`, `Description`, `Words` — are private statics on `NotificationScreen` taking the
window, so `/eval` reflection reads them for ANY notification window whether or not it is the one
showing (`typeof(ES2Access.Screens.NotificationScreen).GetMethod(name, NonPublic|Static, null, new
[]{typeof(NotificationWindow)}, null).Invoke(null, new object[]{window})`). That is how the
real-words case is regression-tested when no live popup has real words: bind a
`LuxuryDiscoveredNotificationWindow` and read it directly. Beware that
`Gui.GuiNotificationService.ShowGuiNotification` on top of an already-open popup does NOT bring the
new one to the front — the mod keeps reading the popup that was there — so a raised notification is
readable by reflection but not by `/gui/graph`; dismiss it with
`DismissGuiNotification(window.GuiNotification)` and the empire's own list is untouched (verified:
count unchanged before and after).

**The election survey is the only popup that both SAYS and DRAWS** (`ElectionSurveyNotificationWindow`,
raised on the game's own election turns — no save in the repo has one pending, so it is a
walk-past-it fixture). Its shape, live-verified 2026-08-15: `notification:top` = Next / Previous /
Pop up automatically; `notification:body` = the words node first, then the four
`PoliticalSupportLine00N` buttons in drawn order, three of them (Industrialists, Militarists,
Ecologists) banded into ONE row walked with Left/Right and the fourth (Scientists) on the row below,
each reading "N%, Party, button" with a Class-backed `Politics` dossier in its buffer
once focused; `notification:bottom` = Minimize / Done. **One body region, not four** — the party
lines are rows inside `PoliticalSupportLinesTable`, and a build that gives each of them a
`notification:body/N/PoliticalSupportLine00N` region has re-broken the card rule
(`NotificationScreen.Cards`: a card HOLDS what it heads). The game also declares this table in the
`Tables` variant list, so a build where the body comes back as `notification:table:` rows has
re-broken `ReadTableSheet`'s guard.

**The deed and quest-completed popups are the detached-description family** (es2-facts): their
shape is four body rows (StatusTitle, ObjectiveTitle, "Outcome", ObjectiveLore) with **no words
node**, `notification:top` holding Next/Previous/Pop-up-automatically and `notification:bottom`
holding Minimize/Done. A words node reappearing on one of them, or the whole strip collapsing into
`notification:bottom`, is the detached-label regression.

**The collapsed-report family** — `IonWaveReport`, `ObliteratorAttackReport`,
`ObliteratorVictimReport`, `DisplacementReport`, `PirateMissionReport`, `ForceTruceProposed`. Each
draws a "+" (`ReportToggle` / `MissionReportToggle` / `Winner`+`LooserBreakdownToggle`) over a
detail panel it keeps FADED (es2-facts), and each toggle is declared through the `Expanders` variant
field and named from its own tooltip's opening sentence. Live-verified 2026-08-15 on a forced
IonWave: collapsed = `notification:words`, the icon label, then
`notification:expander/ReportToggle` reading "Click to display the details of the report, checkbox,
not checked, 2 of 2" and NO detail rows; `ui.activate` says "checked" and the body
grows `notification:body/2/Title` "Damage Report" and `notification:body/3/Title` "You lost 0 Ships
to your enemy's Ion Wave"; parity `clean:true` in BOTH states, and `crop-shot.ps1 -Rect
704,216,442,200` shows empty sky collapsed and the drawn panel expanded. A build where the detail
rows read while the "+" says "not checked" has lost the painted-ness gate; one where the expander
node is missing has lost the variant entry. `ForceTruceProposed` used to keep ONE placement finding
(`notification:body/0/Title` drawn above the words it is read after); the words-lead-the-body design
is now encoded in the probe (`NotificationAudit.CheckPlacement` drops the words node from the
down-the-page order), so all six read `clean:true` and any placement row on them is a real
regression.

**Fixtures for that family.** All six are raised by binding the notification by hand
(`b5.tsv`/`b8.tsv` shapes; `EventIonWaveReport(PEMP, AIEMP, new IonWaveReport(SSN, PEMP.Index))` is
the cheapest). `PirateMissionReport` is FIXTURE-BLOCKED: its `Bind` dereferences an
`AttackSystemPirateDiplomaticAction` that only a live pirate mission produces, and force-SHOWING the
window instead throws in `OnBeginShow` and leaves a half-shown popup drawing nothing — hide it at
once. `NarrativeEventBegun` needs a narrative quest (`QuestJournal.Read(QuestState.InProgress)`
index 26, `AcademyQuest01`, on the parity-audit save) and its choice cards come through the
`Choices` variant field; the fixture's card draws NO words at all (crop-verified: an icon on an
empty panel), so its node is legitimately nameless and carries only its dossier tooltip — and that
tooltip is `SimpleDescription`, so the parity probe reports it unaccounted until the node is
FOCUSED. Raising it also pops the "Unforgettable Events" tutorial, which retires itself when the
popup hides.

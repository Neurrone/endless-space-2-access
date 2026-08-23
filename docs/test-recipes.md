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

**`[Beginner] test` has TWO notifications waiting, so next/previous browsing is NOT
fixture-blocked** (measured 2026-08-19): `NotificationLawCancelled` ("Laws Cancelled" — one table
row, "Improvement Completion Incentivisation Scheme / Industrialists") and
`NotificationTechnologyNeeded` ("Research queue Empty" — since 2026-08-19 its
`SuggestedTechnologiesPanel` reads as the research-complete popup's does: the instruction row then
one named card per branch, and a bottom bar carrying its own "Technology Screen" button. It read as
13 loose body rows until that variant was registered). Open one without touching the event bus:
`Gui.GuiNotificationService.ShowGuiNotification((GuiNotification)((System.Collections.IList)Gui.GuiNotificationService.GetPlayerEmpireGuiNotifications())[0])`,
restore with `Gui.GuiNotificationService.HideAllGuiNotifications()`.
**Putting a DISMISSED notification back**, without `POST /loadsave` and without the event bus (this
is how one dismissed by accident was restored in a single eval): `GetPlayerEmpireGuiNotifications()`
returns the manager's LIVE list, so `RecordEventForEmpire` can be replayed by hand —
`new NotificationLawCancelled()` → `RegisterComponents()` → `Bind(new EventLawCancelled(empire,
lawDef))` → `Load()` → `AlreadyRead = true` → `list.Insert(0, n)` → the private
`OnPlayerEmpireNotificationsCollectionChanged(CollectionChangeAction.Add, n)` by reflection so the
HUD strip refreshes. Verified: the strip read "Notifications, Laws Cancelled, button, 1 of 2" again.
The event bus is exactly the route to avoid here — `EventLawCancelled` is an `EventOnPolitics`,
which `QuestManager` reads as a quest trigger (`QueryPoliticsNameFromGameEvent` :2934).

**Raising a notification on demand** (for a popup family neither fixture holds pending):
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

**Raising a MOD notification** (populating the Turn log) is bus-safe — mod event types have no
game listeners: `ES2Access.UI.ModNotifications.Raise(new ES2Access.UI.EventModFleetArrived(player,
fleet, fleet.GetGameNode()))`, enumerating fleets by binding `DepartmentOfDefense.Fleets` as a
non-generic `IList`. For a GAME event with listeners, keep to the `RecordEventForEmpire` replay
above. End-of-turn news is stamped with the turn that ENDED, not the one you wake in — expect the
"Turn {n}" region one lower than the HUD reads after the boundary. Related fixture tools:
**make a whole empire's fleets genuinely visible** with `player.VisionSharingBits |= other.Bits`
then `IVisibilityService.ForceRefresh(-1L, true)` — sharing propagates only on a layer CHANGE, so
without the forced refresh nothing happens; **`IEndTurnService.TryToEndTurn()` answers false the
first time** (validators speak their warnings) — call it twice; **after `POST /loadsave`, re-run
the REPL setup** — a `var` bank silently keeps the DEAD game's objects (cost one false "the mod is
broken"); and a **save round trip without touching the fixture** is
`IGameSerializationService.SaveGame(...)` to a scratch title, reload, delete the file.

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
The PANEL itself is no longer unsighted, though: `TechnologyNeededNotificationWindow` draws the same
`SuggestedTechnologiesPanel` whenever the queue is empty, and `[Beginner] test` has one pending — so
the cards' wording, ordering and buffers are testable there, and only the research-COMPLETE popup's
two queue-empty branches still need a played-dry save.

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
From the main menu one `ui.activate` on New Game opens the lobby directly — the flyout's Quick start
and Beginner children both LAUNCH, so never activate either.
`Session.SetLobbyData(Amplitude.Unity.Session.LobbyData.CompetitorCount, n)` drives the count through
the game's own path and the panel rebuilds within a settle or two; the default 8 competitors draw
SEVEN AI slots in a 4×2 grid (y=162: x=424/556/688/820, y=220: x=424/556/688), which is the order the
"Player 1…7" band labels are numbered in. Shrinking the count while the last band's last row is
focused is the reconciliation test: focus relocates to the new last band's same column and announces
once ("Player 6, Color, combo box, Purple, 4 of 4" at 8→7). Put the count back and re-read the probe.
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
**The settings walk, and how to leave it as found** (the route the value-tooltip re-read was proved
on): main menu → `ui.activate` on New Game — its flyout's two children, Quick start and Beginner,
both LAUNCH, so never activate either — then Tab×4 lands on Gameplay. Every change is lobby data and
is restored by moving it back (`ng.Session.GetLobbyData<string>("gamespeed")` is the before/after
probe). The advanced settings modal is the Gameplay stop's last item and only calls `ShowWindow`
(`NewGameScreen` :550-552), so opening it is safe; leaving either surface is
`HandleInput(InputAction.Exit)` on the window. The pause menu's own settings panels are behind the
game-menu checkboxes "Show Game Settings" / "Modify Timer Settings", which are OFF by default — put
them back.

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
whichever column focus is in — landing in the column focus was in, since a save row matched by its
NAME. The sort headings are searchable too since 2026-08-21 (`SearchesAsItself` on the band):
`POST /type "session m"` lands on `loadsave:header/SessionMode/3` and says "Session Mode, button,
Sort by session mode", with no step sideways off it. **Never Enter on a row, never press Load, Save or Delete, and NEVER
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
`EventTutorial_TechnologySelected`, so do it LAST (cancelling restores the QUEUE, not that event's
own effect on the tutorial — `POST /loadsave` is the only restore for it).
**The queue round trip's route is FIVE injections, and the first Enter does not queue** (measured on
`[Beginner] test` turn 21, whose research queue is empty): `ui.prev` to the `research:suggested`
stop, `ui.down` onto a cheap technology, then Enter — which **JUMPS to that dot** rather than
queueing, `research:suggested`'s activate being `Jump` — and it is the SECOND Enter, on the dot
itself, that queues and says *"Queued …"* on top of the dot's own state word flipping to "In
progress". "Survival Suits" and "Ubiquitous Surveillance" are the fixture's two available Military I
dots (195 Science each); "Military II" expands to four "Not available" dots, the ready-made research
REFUSAL controls. Restore by cancelling what was added — `ResearchQueue.Length` back to 0, measured
2026-08-19, no `POST /loadsave` needed.
**Cancelling a technology that has PROGRESS** (measured 2026-08-22, and it works on both routes):
queue a cheap dot, `ui.endTurn` TWICE (this save has an idle system, so the first press only raises
the game's own prompt), then reopen the wheel and press Enter either on the wheel dot or on
`research:queue/<TechnologyDefinition…>` in the `research:status` stop — reached by Tab to that
stop, `ui.home`, `ui.down` twice, NOT by `ui.end` (which lands in the `research:region/key` band).
Both answer *"Cancelled ⟨title⟩"* with `ResearchQueue.Length` dropping to 0, and the dot's own
state word then flips to "Available". `TechnologyItem2.Dragging` reads false throughout. Cancelling
REFUNDS the science, so the dot reads its full cost again. Ending turns raises notification popups
that steal focus — hide them with a `FindObjectsOfType<NotificationWindow>()` loop over `Shown`
before reading the wheel, or the next injection goes to the popup. Restore with `POST /loadsave`
(the turns cannot be given back).
**Proving the queue row's REFUSAL branch** (the never-be-silent guarantee — the state is not
reachable in play): queue a dot, focus its queue row, then race a background
`ResearchQueue.Remove(PendingConstructions[0])` `/eval` against a `POST /input ui.activate` ~0.3 s
later. The row is still drawn while the queue no longer holds the technology, so Enter answers with
the technology's state (*"Available"*) instead of the "Cancelled" it can no longer honestly say.
**Searching the wheel by what a technology UNLOCKS** (2026-08-22): Tab onto the `research:tree`
stop first — `POST /type` searches the FOCUSED stop, and from `research:status` the same letters
find nothing. Then `POST /type "surv"` → 2 results, Survival Suits first (title match);
`"Impervious"` → 1 result, Survival Suits (its unlock "Impervious Bunkers"); `"Miners Union"` →
Galactic Commodities Exchange; `"Zelevas Incarnate"` → Emperor's Shadow. `ui.down`/`ui.up` step,
`ui.back` says "Search cleared". The per-keystroke cost is
`ES2Access.Screens.ResearchScreen.SearchTextBuilds`: it must reach 107 (one per drawn dot on this
fixture) and then STOP rising, however many letters follow — it climbs by another 107 only after a
turn change or a `POST /loadsave`, which is the cache expiring. To read a technology's whole search
string, invoke the private static `ResearchScreen.BuildSearchText(GuiTechnology2)` by reflection
over `TechnologyQuadrantsContainer` → `TechnologyQuadrantItem.TechnologyStagesContainer` →
`BaseTechnologyStageItem.TechnologyItems` (the stage's dots are NOT its `Children`).
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
is pinned" from the HUD's watcher, even with the journal covering the panel. The alternate click
(Ctrl+Shift+Enter since 2026-08-19; it was Alt+Enter) on a card is
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

### Scanner taxonomy v3 (2026-08-22) — the six new categories, VERIFIED LIVE 2026-08-22

The order Ctrl+PageDown now walks is **Systems, Colonizable Planets, Unexplored, Anomalies,
Curiosities, Luxury Resources, Strategic Resources, Contested Influence, Fleets, Probes, Ally pins,
Obliterator missiles, Quest markers** — so the paragraphs below that call fleets "the second" and
probes "the third" describe the WORDING, not the position any more. Everything below was written offline and then HEARD on `[Beginner] test` (turn 21, reference = the
map stop's own place); the measured rows and counts are quoted inline. Drive it with the same
action keys; count the Ctrl presses from Systems. Contested Influence is SKIPPED on this fixture
(169 ground tiles swept, 0 contested), so the Ctrl ring runs Systems, Colonizable, Unexplored,
Anomalies, Curiosities, Luxury, Strategic, Fleets, Probes and back.

Per category, what to check:

- **Colonizable Planets** (1 Ctrl press). Two subcategories and NO "all": `unoccupied` first. MEASURED
  on `[Beginner] test`: `Colonizable Planets: unoccupied, Rigel I, Small Forest, max population 7,
  Planet Food production 8, Planet Industry production 4, Planet Dust production 4, Planet Science
  production 3, Planet Influence production 0, -16, -5, 3 south, 1 of 7` and
  `occupied, Osulo I, Medium Mediterranean, Hyperium, Titanium, max population 7, Planet Food
  production 6, ..., 1 of 1`. So: exactly ONE comma between the planet's name and its size, size
  BEFORE type, resource names without the deposit suffix ("Titanium", not "Titanium-70"), and the
  five outputs carry the GAME's own titles - which are "Planet Food production" and kin, not the
  bare "Food"/"Influence" this recipe first guessed; `PlanetInitialPrestige` reads "Planet
  Influence production". A sparse world drops the absent parts entirely (Rigel I has no resources,
  anomalies or curiosities and says none). Oracle for membership: an `/eval` walk of the perceived
  systems asking `!p.IsColonized && p.IsColonizable(me)` per planet - measured 7, equal to the
  `unoccupied` count; the `occupied` half (a foreign or minor colony this empire's tech could
  settle) is Osulo I alone, and the same walk's `IsColonized && other empire && able` count is 1.
- **Unexplored** (2 presses). `Unexplored: all, Star lane ⟨n⟩ from ⟨system⟩ heading ⟨direction⟩,
  ⟨the system's pair⟩, ⟨offset⟩, 1 of ⟨m⟩`. Check the lane NUMBER against the tree: focus the same
  system, walk its lane rows, and the numbering must be identical (both come from `LanesOf`).
  Oracle for the count: for every perceived node, its drawn links whose far end is not perceived,
  summed. **Each lane must appear once** — a duplicate would mean both ends were perceived, which
  contradicts the gate. A wormhole reads "Wormhole ⟨n⟩ from …"; fixture-blocked unless the empire
  has wormhole technology.
- **Anomalies / Curiosities / Luxury Resources / Strategic Resources** (3-6 presses). In `all` the
  row is `⟨kind⟩ on ⟨planet⟩`; Shift steps into one KIND at a time, alphabetically, and there the
  scope line is the kind's own name and the row is the planet alone ("Acid Rain, Primus I, 17, 21,
  42 north, 34 east, 1 of 1"). MEASURED on `[Beginner] test`: anomalies 12 in "all" over 10 kinds
  (Acid Rain 1, Binary Moons 2, Hollow Planet 1, Huygens Rings 1, Mineral Rich 1, Multiple Moons 2,
  Polar Tempests 1, Single Moon 1, Strong Magnetic Field 1, The Platform of Ys 1); curiosities 16
  over 5 (Atmospheric 1, Life Form 6, Ruins 4, Signal 2, Subterranean 3); luxuries 10 over 2
  (Dustciduous Trees 6, Transvine 4); strategics 5 over 3 (Antimatter 1, Hyperium 2, Titanium 2) -
  every per-kind total sums to its "all", and every list is alphabetical in the localized names.
  The kinds are the game's own words. One row per (KIND, planet), owner's wording 2026-08-22: two
  anomalies of the SAME kind on one world are one row, two of different kinds are two.
  Curiosities appear on systems that are NOT surveyed (the gate is `Curiosity.CanBeSeen`), and the
  planet is then named with the game's "unknown" word - FIXTURE-BLOCKED here, since all 13
  perceived systems are surveyed.
- **The memory is by NAME.** Park on a kind, cycle away to another category and back: the same
  KIND must come back, not the same column index. The offline proof is `ScannerKindsTests`; the
  live proof is doing it on a fixture where a kind sorts in the middle - DONE 2026-08-22: parked on
  "Mineral Rich" (5th of 10 anomaly kinds), cycled three categories on and three back, and the
  scope line came back "Anomalies: Mineral Rich" (Luxury likewise came back on "Transvine").
- **Alt+Home lands AND zooms, on every category.** MEASURED 2026-08-22: a planet result lands on
  `galaxy:constellation/446/system/491/planet/0` with both ancestors opened and the camera moved
  onto the system; a lane result on `…/system/505/lane/650` (camera zoomStep 9 → 12); a mid-lane
  FLEET keeps its own node (`…/system/522/fleet/1570`) while the camera slides to it, and the probe
  lands on `galaxy:probe/1621` with the camera slid to (13.59, -52.30). With the inspect cursor UP
  the cell jumps and nothing else moves (tree cursor unchanged, camera follows the cell).
  Contested Influence must still ARM the cursor - fixture-blocked, the category is empty here.
  **The landing UTTERANCE is composed before the camera arrives** (`FocusNode` then `Camera`, the
  same order the page's own `Arrive` uses): a planet landing said "Osulo I, Colonized, 1 of 7" while
  the settled row reads "Osulo I, group, Medium Mediterrane., Colonized, collapsed, 2 of 8", because
  the closer view adds rows and the card's own words. Re-reading the node gives the full line. This
  is the locate path's own behaviour, not the scanner's.
- **The cost.** `POST /eval` `ES2Access.UI.ScannerCost.Line()` after a press answers
  `scanner snapshot ⟨ms⟩ ms, ⟨n⟩ colonizability checks, press ⟨n⟩`. Take it (a) on the first press
  of a session and (b) while holding Alt+PageDown. Anything at or over 30 ms also logs itself, so
  `GET /log?grep=scanner snapshot` is the second reading. MEASURED 2026-08-22: **32 ms on the
  session's first press** (the one line in the log) and **5-8 ms** on every press after, including a
  25-press burst; **30 colonizability checks**, every press. The shape to check is the SUM: one
  check per planet TYPE seen (19 here) plus one per unsettled world of a settleable type (11) -
  against 33 declared planets. A count that tracks the PLANET count means the memo is not working.

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
  pass recomputes every radius and every node's winner from the simulation (es2-facts). Verified
  by diffing `/gui/graph?buffers=1` before and after: identical bar the HUD clock.
Empire indexes here: Neurrone (the player) 0, Niris 4 — the contested list is sorted by index, so
the player sorts first in a mixed list.

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
walking onto it says the coordinates and "Unexplored" and nothing about influence at all.

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
**The 136-label SystemLabelReadout census** is the byte-identical regression oracle for any
galaxy-label change: one `/eval` calling `Lines/Population/Sleepers` over every pooled label,
diffed before/after (on `unlocked` turn 1: 135 labels answer 0 lines, Xiu answers 2).
**Force-showing a label badge** (exploration winner and kin): instantiate the game's own prefab
into the drawn label, set the tooltip the way `Refresh` does, read, then `DestroyImmediate` +
`RebuildInternalChildrenList(false)` + re-derive the private `metaModifiers` array (it is
indexed with no null guard).
**Force-showing a constellation label** (its tooltip never fills naturally in `unlocked` —
labels are culled, es2-facts): `label.CulledIn = true` + `ShowOrHideIfVisibleByEmpire(
window.LookingEmpire)` + `Dirty = true`; restore with `CulledIn = false` + `Hide(true)`.
Note a force-shown label's tooltip rows all report rect (0,0,0,0) — no longer a blocker: the
`"constellation"` typed reader answers off the feature's own fields whatever the rects say, and a
FOCUSED constellation node holds its label shown by itself (`ConstellationLabelHold`); the recipe
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
popup — minimize it before injecting anything else. **Cancelling ANY targeting mode at a
multi-fleet system re-selects the FIRST fleet at the slot, not the actor** (the game's own
`ProbeLaunchingCursor.SwitchToGalaxyCursor` selects the docking slot and
`FleetsScreen.RefreshGarrisonSelection` defaults positionally — measured 2026-08-20, keyboard and
mouse identical), so after a cancel the panel and `fleets:actions` belong to that first fleet.
Enter on a fleet's own map row is CORRECT in every measured state (a 2026-08-19 report that it
"selected the other fleet" was this cancel-swap still standing) — just re-read the actions stop
AFTER the Enter, never before.

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
focus a system node (type-ahead via `POST /type` with the system's name — the map ids are
`galaxy:constellation/<c>/system/<id>` under the constellation grouping, so a hand-built
`FocusNode` id is fragile; the focus pans the camera there), `Gui.GuiService.ShowWindow<MilitaryScreen>()`, then run the locate from
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
presses). Both were run against a LIVE owner session and restored exactly.
**That round trip has a SPOKEN oracle since 2026-08-19**: Enter on "Interplanetary Transport
Network" answers *"Queued Interplanetary Transport Network"* while `ConstructionQueue.Length` grows,
and Enter on its queue line answers *"Cancelled …"* with the ABBREVIATED title the line draws
("Interplanetary Transport N." — es2-facts). ITER ("Cannot afford the resource cost") is the
fixture's ready-made REFUSAL control on the same panel. **The confirmation branch has no fixture
here**: all seven of Dusay's constructibles report `NeedsConfirmation = false` (nothing is invested),
so the game's own message box — and the suppressed outcome line that goes with it — is code-verified
only. The home planet is
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
name line (the System panel's name node opens it directly - the box was called "Colony" until 2026-08-22), the planet card's rename button
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
never the glued `Militarists Established +Industrialists`. Since 2026-08-22 the card and each badge
are single-item ROWS sharing the winners' row key (owner ruling): DOWN walks card, badge, next
card, next badge, RIGHT from the card falls through silently, and the winner's place is still
stamped as a row position, so "1 of 2" is heard arriving at a card and on stepping to the other
winner and NOT on walking out to that winner's badges. The card's focused buffer holds the party dossier AND the experience
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
Enter made, and leaving the map.
**Escape with a type-ahead search live takes TWO presses (2026-08-19).** From the map stop:
`galaxy.inspect`, `POST /type "qa"` (→ `Qarius, -5, 23, group, collapsed, 3 of 14`, focus
`galaxy:system/508`, still a map node so the mode stays Active), then `ui.back` → **"Search
cleared"** with the mode intact, and a second `ui.back` → **"Exited inspect mode"** plus the map
node's own line. The mode's survival needs a state probe, since "Search cleared" alone cannot
tell a live mode from a dead one: reflect `ES2Access.Screens.GalaxyInspect`'s static `Live`/`Active`
(both internal — go through `typeof(ES2Access.Dev.DevProbe).Assembly.GetType(…)`) and
`ES2Access.UI.InspectMarker`'s private static `_drawer`, which reads non-null while armed and
**null** the moment the mode ends; the cheap live half is one `ui.right`, which must answer with
the next cell pair (`5, 34` → `6, 34`). `DevProbe.Claims("Escape")` is `claims:true` with the
search live, with only the mode live, and with both — and `false` once both are down, so nothing
leaks to the game and no claim sticks. The same two-press order is the TARGETING cursor's
(`ChangeCursor(typeof(TakeSystemCursor), new AcademyDiplomacyGiveSystemAction())` → search →
`ui.back` "Search cleared", cursor still `TakeSystemCursor` → `ui.back` "Target selection ended")
and the CARRY's (`ModEntry.Carry.PickUp(...)` → search → "Search cleared", `IsCarrying` still true
→ "Cancelled drag"); both of those come free from `SearchAction` running ahead of the switch in
`GraphNavigator.Dispatch` and neither needed a change. Enter on a one-place cell lands on `galaxy:system/<guid>`
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
**Skip and travel (2026-08-19)**: `ui.coarseDecrease`/`ui.coarseIncrease` are the WEST/EAST skip
while the mode drives the map, `galaxy.inspectSkipNorth`/`…South` the other two, and
`galaxy.inspectFollowWest`/`…FollowEast` the travel keys. Measured on `[Beginner] test` at 1×1 from
Ita (5,34): north gives `5, 35`, then `Skipped 2 squares` + `5, 38, Unexplored` (the fog bucket
changing is a stop), then `Skipped 49 squares` + `5, 88, Unexplored` (the run to the north edge — the
landing is not counted, hence 49 and not 50), then `Map edge`; southward is the mirror
(`Skipped 50 squares` + `5, 37`). At 5×5 from (5,34) north gives `5, 39, 21 squares unexplored`, the
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
`w.GuiRecipeSlot = new GuiRecipeSlot(0, false); Gui.GuiService.ShowWindow(w)`; close with
`HideWindow(w)`, and a RE-SHOW is the restore — `OnBeginShow` clears `currentRecipeIngredients`
and `RecipeModified` (measured 2026-08-19). **NEVER press Confirm** — it is enabled even with an
empty recipe and posts `OrderCreateRecipe` — and note Reset does NOT clear `RecipeModified`.
All four family grids are tables sharing one `ResourceGrid` reading since 2026-08-19 (luxuries 8
columns, strategics 6; "empty" holes, blank lines undeclared). The column captions read the economy
screen's short titles ("Industry"), not the family descriptions. **Since 2026-08-21 the families are
also a header ROW** — `<stop>/family/<i>`, one node per drawn icon, above the first line and inside
the sheet's `reg:0`; Tab still lands on the first data row and Up from a cell reaches its own
column's heading. Each heading carries the family sentence ("This family of resource improves Food
…") and no cell does any more, so a before/after of that sentence's count in
`/gui/graph?buffers=1` is the check: 132 buffer lines → 14, one per family, on each page.
Type-ahead here matches CELLS by their own words (the rows have no name): `POST /type "transv"`
from any column lands on `economy:luxuries/r0c5` and says "Approval, Transvine, 22, +2 per turn,
1 of 2" — a landing is never stepped sideways off, and "trad" lands on the family heading.
**Whether the strategics grids draw is `CanUseStrategicForRecipe` (above) plus a
`EconomyPanel.RefreshNow()`** — setting the property alone leaves `StrategicsGroup.Visible` false
until the panel refreshes.
**A 0×0 game window makes every grid degenerate to ONE column.** `AgeTransform.GetGlobalPosition()`
answers all-zero rects when `UnityEngine.Screen.width` is 0 (a locked or disconnected session), so
`ResourceGrid.ColumnOf` maps every item to column 0 and the luxury lattice reads as 12 rows of one
resource plus seven "empty"s instead of 3×8. Probe `UnityEngine.Screen.width` before believing a
layout measurement; the navigation model (8 headers, 8 columns, the column-paired seam) is still
fully exercised in that state, only the cell-to-column assignment is wrong. The modal's project stop draws ONE slot in `unlocked` (multi-slot strip
unmeasured). The two STRATEGIC grids draw only for an empire with Material Expertise — no save
here has it, but `SimulationProperties.Empire.CanUseStrategicForRecipe` DOES stick under
`SetPropertyBaseValue` + `Refresh(false)`: set it, the game draws the real grid itself, set it
back (verified round trip 2026-08-19).

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
**The slots are grouped by module TYPE** (`SlotOrder` — helpers.md), so the drawn order survives
only inside one type. On the Patrol design (a Small Zolya-class hull) the "Module slots" panel walks
*"Module slots, empty, button, Defense Module, 1 of 6" / "String Gravitics Engine, button,
draggable, 2 of 6" / "Improved Probes, button, draggable, 3 of 6" / "empty, button, Weapon Module,
4 of 6" / "Basic High-I Slugs, button, draggable, 5 of 6" / "Drop here to remove, 6 of 6"* — the
engine slot is a defence-AND-support slot, which is why it reads with the defence ones. Fitting or
removing a module must never move a slot: the key is the slot's, not the module's.
**Two of the three slot markers need another RULESET, not just a bigger hull.** Multiplier and
pairing exist only in `HullDefinitions[Balancing].xml` (es2-facts), so no faction hull in this save
draws either, and a heavy mount needs a medium or large hull (its medium-hull instance is behind
`TechnologyImproveHull3`). Sight them by LENDING hull data — designer opened in Create mode with
`Bind(null)`, hull taken from `Gui.GuiWrapperProviderService.GuiHulls`, left through the
confirm-lose-changes path (measured 2026-08-19, the design list 9 before and 9 after):
`HullMedium01Balancing` reads *"empty, button, Weapon Module, Times 2 Multiplier, Symmetrical (x2
cost), 6 of 9"*, `HullLarge01Balancing` reads Times 4, and `HullLarge01Terrans` — a real rendered
faction hull — reads *"empty, button, Weapon Module, Special Module, Heavy Mount, 6 of 11"* with the
slot measuring 57×57 against its neighbours' 44×44. A FILLED slot says none of the three. A
perceptual pass on the dots and the pairing circle needs a game started on the Balancing ruleset.

**Hero inspection.** Bind, open, switch pages and close from `/eval`. An unrecruited `GuiHero` is
the read-only fixture. For a skill point, set `Level = 2` and `Refresh`, then restore by reloading
the save. Page switches raise tutorial popups — minimize them. Cheaper than `/eval`: the whole
family — overview → ship design page → skill tree page — walks from the Academy's own Inspect
button with `/input` in `unlocked`; each page switch raises a tutorial to minimize.
`HandleInput(InputAction.Exit)` BACK-STEPS hub modes (skill page → overview → closed), one call
per level — verify closure by the `/gui/graph` header, never by absence of an error; reopen with
`Bind(ActiveHeroes[0])` + `SwitchHubMode(mode, true)` + `ShowWindow<HeroInspectionModalWindow>()`.
**A two-level skill tooltip without a skill point** (reversible, no Apply): reflect the
definition into `SkillTreeEditionPanel`'s private `leveledUpSkillDefinitions`, call
`heroPreview.UnlockSkill(def, GetTotalSkillLevel(def), true)`, set `Dirty = true`, then LEAVE the
node and RETURN before probing (a still-focused node's tooltip answers pre-mutation content);
reset with `window.gameObject.SendMessage("OnResetSkillsCb", DontRequireReceiver)` and assert
`IsSkillsModified == false` and points back to 0. `unlocked` has no relic-skill hero, no
two-mastery starting skill, and no natural skill point.

**Troops and the tactics deck** are both non-committing until Confirm, which makes them safe to walk
whole. A refusal is provable from BOTH sides by injecting one: force the game's own refusal state,
read the spoken reason, and put it back.
**The deck opens straight from `/eval`** — `Gui.GuiService.ShowWindow<PlayCardDeckModalWindow>()`,
no military screen needed — and closes with `HandleInput(InputAction.Exit)`. `[Beginner] test` turn
21 draws four available tactics (Team Spirit, Barrage Fire, Power to Shields, Revive and Rebuild),
three of them already in the set, plus two locked slots. **Opening it advances the game's own
tutorial page to "Military"** — harmless while the popup is minimized, but it is a fixture change to
know about.

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
**The mod manager's library is EMPTY in this install** (no local mod, no Workshop subscription), so
the page draws only its "No mods" placeholder and no mod row can be walked — everything about a mod
row is code-verified only. Its top band is one drawn line of 32 px (`FolderFiltersTable`, rect
`44,117,724,32`) holding the two folder toggles in drawn order Workshop then Local;
`DisableCustomConfigButton` joins that same band but is invisible unless the runtime was started
with a custom mod configuration.
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

## The screens batch (2026-08-22) — verifying the captions

**Opening the minor-civilization window from `/eval`.** Its `Bind` takes the minor empire AND the
game node its home system sits on, so both have to be found before it will show: walk the game's
empires for a `MinorEmpire`, take its home `StarSystem`'s `GameNode`, then
`var w = Gui.GuiService.GetWindow<MinorFactionDiplomacyModalWindow>(); w.Bind(minor, node);
Gui.GuiService.ShowWindow(w);`. Close it the way Escape does —
`w.HandleInput(InputAction.Exit)`, Assembly-CSharp's `InputAction`. In a normal session the route
is the galaxy map: a minor's home system label draws a diplomacy button (`StarSystemLabel`'s
`DiplomacyButton`, class tooltip `MinorFaction`), and its Enter opens this window.

Expected after this batch (Tab order, `/gui/graph`):

- Arrival says **"Minor Civilization diplomacy, Niris"** as the screen name — not "Niris" alone,
  and not the mod's "Minor faction diplomacy".
- `minor:identity` — first row is the window title, its buffer holding
  `%MinorFactionDiplomacyModalWindowDescription`. Everything below sits under the level
  **"Niris"**, in four regions (Alt+Up/Down): the lore paragraph; **"Traits"** with
  "Personality, ⟨trait⟩" and "Faction trait, ⟨trait⟩" — each buffer carrying the icon's sentence
  AND the trait's own class-backed dossier, which must DRAW on focus; **"Effects on planets"** with
  its lines; **"Political output"** with its party line. The lore paragraph must NOT land between
  "Political output" and the party (the old geometric interleave).
- `minor:relation` — named **"Diplomatic Relation"**, first row that caption itself with
  `…RelationDescription` in its buffer. Then "Relation, CORDIAL"; the points row named by the trend
  gloss with "40 (+7/turn)" as its value; "Ally, None". Then a **"Tooltips"** region of four nodes,
  one per gauge band — and with the faction at WAR that region must be absent, because the game
  hides those transforms.
- Then **"Relation Rewards"** with ONE ROW PER RESOURCE ("+6.5 Dust", "+4.1 Science", …) instead of
  one four-line label, or the game's own "no rewards" sentence; then **"Modifiers"** with its
  caption row, the influence line where the system is under the player's influence, and one row per
  temporary effect.
- `minor:actions` — named **"Actions"** by the game's own caption, with that caption's own row
  (`…ActionsDescription`) ahead of the action cards.
- Fixture note: the four gauge bands and the influence-modifier line need a minor that is neither at
  war nor unknown; "Modifiers" is empty on a freshly met one.

**Population overview** (senate → census, or a star system's population row):

- `population:heading` still reads "Population Overview" and keeps its tooltip.
- `population:detail` — the first region is now named **"Imperials"** (the people's own name) with
  the lore paragraph as its only row; there is no "Imperials" ROW any more.
- The collection track: first node **"Collection status, 3"** (the caption plus the count, the
  caption's `%CollectionUnlockGroupDescription` in its buffer), then one node per threshold reading
  **"10 population, not reached"** / **"50 population, reached"**, with that bonus's effect lines
  reviewable but not spoken. Cross-check the state against the drawing: `.\crop-shot.ps1` the track,
  and a bright circle must be a "reached" node.
- **"Collection Effects"** must read the game's **"No Effects"** where the block is empty, and must
  NOT read the ghost row ("Militarist" at alpha 0 — the defect this gate fixes). Prove it with an
  `/eval` walk printing `Alpha` beside `Visible` for `CollectionEffectsTable.Children`, and a crop
  of the same rect.
- `population:politics` — the reactions region is now named **"Reaction to Political Events"** (said
  once on arrival, again on the Alt+Down into the ring), the six sector rows are unchanged, and a
  **"Tooltips"** region follows them with one node per party dossier off the legend
  (`PoliticsLabelsTable`). Each must DRAW its dossier on focus (`DevProbe.Tooltip()`).

**First contact with a minor** (the popup, or `MinorEmpireMetNotificationWindow`'s card driven on
the unshown window — `MinorFactionCard.Refresh(playerEmpire, minorEmpire, Gui.ImageSize.Mood)`):
the card's two figure rows must read **"Ally, None"** (or "Ally, Unknown Empire") and
**"Relation, UNKNOWN (?, +0/turn)"**, with the icons' sentences still in their buffers. Every other
popup's drawn body must be unchanged — walk a battle report and a construction report and diff.

**The caption sweep.** On the economy, senate, recipe-creation and negotiation windows, a box whose
heading carries NO tooltip must no longer have a heading ROW (the box is still named on the way
in); a box whose heading DOES carry one keeps it. The negotiation pressure band is named by the
game's drawn title ("Pressure", or "War Exhaustion" with a war on) rather than by the mod's word.
Diff `/gui/graph?buffers=1` for those four screens against a pre-merge capture: only heading rows
may disappear.

## Usage hints — reaching each context

A hint reads in `/gui/graph?buffers=1` without focusing anything (`NodeBuffer` feeds both the live
buffer and the dump), so every check below is one dump plus whatever it takes to get the context on
screen. `ES2Access.UI.Input.ChordNames.Of(ES2Access.ModEntry.Input, "<action>", <index>)` from
`/eval` is the chord half on its own, and the same call on a hand-built `KeyboardBinding` is the
rebind proof without touching the shipped bindings.

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
  deselect the fleet". **The off-lane negative is fixture-blocked**: all six of the player's fleets
  carry `FreeMovementSpeed` 0.8, and the property is descriptor-driven (a `SetPropertyBaseValue`
  write sticks in the base and the computed value stays put). The empty-space half of the deselect
  hint has no node to sit on — the mod declares no empty-space control, and `Deselect()` is reached
  only from `LaneClick`.
- **Curiosities are ALL refused in `[Beginner] test`** — the empire's Expedition Power is 2 and
  every curiosity on the map needs 3. To run the queue-then-cancel round trip, grant the game's own
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
- **A turn-log row on demand**: `ES2Access.UI.ModNotifications.Raise(new
  ES2Access.UI.EventModFleetArrived(Gui.PlayerEmpire, fleet, ES2Access.UI.FleetOrders.Orbit(fleet)))`
  gives one `hud:turn-log/<turn>/0` row ("… arrived", then "Backslash to dismiss"); `ui.contextual`
  on it dismisses and the whole stop goes, which restores the fixture and proves the wiring in the
  same press.
- **The rest, all from `/eval` openers already documented above**: `ShowWindow(GetWindow<
  TechnologyScreen>())` then expand a quadrant and a stage for "Ctrl+Shift+Enter to queue it first"
  on a technology (the `research:suggested` stop has no such hint — those nodes only jump);
  `ShowWindow(GetWindow<MilitaryScreen>())` for "Ctrl+Alt+Enter to show and select fleet" (exactly
  one per row, on column 0); `ShowWindow(GetWindow<EmpireScreen>())` for "Ctrl+Alt+Enter to open
  system management screen" (both openings pop a tutorial page — minimize first);
  `LoadSaveModalWindow` with `LoadSaveMode = LoadFromGame` for "Ctrl+Alt+Enter to load" on all
  eleven rows, and flipping the same window to `Save` makes every one of them vanish. The fleet
  panel's own stops carry "Ctrl+Enter to add to the selection" + "Shift+Enter to select up to here"
  on `fleets:line/<guid>` and `fleets:ships/ship/<id>`.

**The hull drop list, from `/eval` in five requests** (the cheapest route to a class-backed drop-list
entry — the ship-hull list is one of the seven target-table lists). The designer opens in CREATION
mode with no ship at all: `var w = Gui.GuiService.GetWindow<ShipDesignModalWindow>(false);
w.Bind(null); Gui.GuiService.ShowWindow(w);` — the graph then reads
`Ship Design: creating` with a `shipdesign/info/hull` combo on "None". Reach it with `/input ui.next`
(the heading stop holds only the title), `POST /type` `hull`, `/input ui.back` to clear the search,
then `/input ui.activate`: `screen.drop-list | Hull` opens with `None / Karga-class / Zolya-class`.
`/input ui.down` onto Karga-class and `DevProbe.Tooltip()` answers class `ShipHull` with
`PanelFeatureHeader` (Karga-class, Hull (Ship Design)), `PanelFeatureHullInfo` (Role Colonizer, Size
Small, Command Points 1) and `PanelFeatureCosts` (Cost: 50 Industry). `/input ui.back` closes the
list; the designer itself must be closed through the MODAL STACK (below) — hiding it leaves it
`ModalOnTop` with `Shown` false, and every modal opened afterwards then reads as buried.

**Draining the modal stack from `/eval`** (the reset every multi-screen sweep needs). `GuiManager`'s
`ModalOnTop` can name a window whose `Shown` is false — hiding a modal does not pop it, and a screen
gated on "am I the top modal" (the minor-diplomacy screen is) then never activates, showing as
`screen: none` with the window plainly up. The drain that works: while `ModalOnTop != null`, re-`Show`
it if `!Shown` and then `HandleInput(InputAction.Exit)`; six passes is more than any real stack needs.
Follow it with `HideWindow` on the full screens (senate, economy, empire, military, technology) and
`Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(Gui.PlayerEmpire.GetAgency<
DepartmentOfTheInterior>().ColonizedStarSystems[0].Node)` to land back on the galaxy.

**Re-opening the minor-civilization window** on a save that has met one: find the `MinorEmpire` in
`(Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.Game.IGameService>().Game as
global::Game).Empires` (indexed, never `foreach`ed — the REPL's generic poison), then `w.Bind(minor,
minor.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems[0].Node);
Gui.GuiService.ShowWindow(w)`. In the owner's turn-22 save Niris is `Empires[4]`, at Osulo.

**Opening the EMPIRE screen raises a tutorial page in a tutorial save** ("Snapshot Of An Empire"),
which takes the mod's focus (`screen: screen.tutorial`) and has to be re-minimized afterwards. The
HUD button for it reads "This functionality is disabled during this part of the Tutorial" in that
save, so the empire screen is fixture-blocked there for anything but a forced `ShowWindow`.

**Leaving the planet page.** `ui.back` does NOT leave it under injection (two presses, still
`screen.planet`); `Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(node)` is the way back to
the galaxy, and `RequestStarSystemManagementViewLevel(node.GUID)` — the NODE's GUID — is the way to a
system page.

**Walking a "Tooltips" region (batch 2, 2026-08-22).** Three surfaces carry one on
`[Beginner] test`, and each is reached the same way: expand the node with Right twice, step to the
second region with **Alt+Down** (`ui.regionNext`), and read the dossier nodes there.
- **Galaxy system.** Route: on `screen.galaxy`, Tab to `galaxy:systems`, `POST /type "osulo"`,
  `ui.back`, Right twice (the camera comes in). The actions region holds Diplomacy, three planets,
  three lanes and a fleet; the Tooltips region holds `Osulo` (the system dossier — its header reads
  "Osulo - Niris" off the LABEL and just "Osulo" off the orbital window the camera swaps to) then
  `Hyperium`, `Titanium`, `Transvine`. Dusay has the system dossier and no deposits.
- **System-management planet card.** Route: `steps/to-system.cs`, End to `Dusay I`, Right twice.
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
by construction (es2-facts). Do this before designing any nested-dossier reading.

**Fixture-blocked in batch 2.** The hero detailed card's four-symbol row (`HeroInspectionScreen`
`AddRow` → `AddDossierRow`) needs the Academy, which is tutorial-gated on `[Beginner] test`; the
construction line's festival badge needs a Hissho festival constructible; the honor gauge's own
dossier needs a Hissho empire. All three are declaration-side only and gated on finding ≥1 (≥2 for
the hero row) named class-backed dossier, so they are inert everywhere they were not measured.

## The input batch (2026-08-22) — verifying the keys, VERIFIED LIVE 2026-08-22

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
  **A pre-existing wart the page keys inherit** (it is identical when the game's own arrow BUTTON is
  pressed, so it is not the key's): turning the star-system page announces the screen TWICE ("Star
  system", the landing, again) and seats the cursor on `hud:view-title/scan` instead of the new
  system's own content. The planet page and the notification popup do neither.
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
- **`walk2.sh`'s route changed meaning** with the single-press arrows: a `ui.right` that used to
  expand a group and stay now steps INTO it, so the old walk's "expand the card, then Enter" reaches
  the card's first child instead of the card. Re-record any stored route that opens something with
  Right before diffing it against a pre-batch baseline.

## Going to a place on the map (batch 7, 2026-08-22)

**The scanner's Alt+Home, per kind, out of the inspect cursor.** Ctrl+G to the map, then
`galaxy.scanCategoryNext/Prev` to the category, `galaxy.scanNext` to the instance, `galaxy.scanGoTo`.
MEASURED on `[Beginner] test`: a SYSTEM lands on `galaxy:constellation/446/system/491` with the
camera zoomed (`zoomStep` 9 → 12, focus on Osulo); a PLANET on `…/system/505/planet/0` with the
camera zoomed; a PROBE on `galaxy:probe/1621` with the camera SLID (zoomStep unchanged, focus
(13.59, -52.30)). Read the landing from `/speech`, the camera from `DevProbe.Camera()` before and
after, and the cursor from the `>` line of `/gui/graph`.
**With the inspect cursor UP** (`galaxy.inspect` first): a SYSTEM keeps the mode, moves the cell to
its tile, ZOOMS anyway, and the `>` line shows the tree cursor on the system node
(measured: Byrtus, cell "-25, -42, Byrtus", zoomStep 9 → 12, cursor
`galaxy:constellation/446/system/572`); a FLEET keeps the mode and only slides (cell "-37, -31, 1st
Patriots Navy", cursor `…/system/491/fleet/1304`); a PLANET **says "Exited inspect mode" first**,
`GalaxyInspect.Live` reads false, and the landing is the ordinary one (measured: Rigel I).
**The settled-row proof.** Zoom out first (`GalaxyViewLevels.SetZoom(5, Vector3.zero)`), move the
cursor off the target (`ui.home`), then run the go-to and read `/speech`: the landing must be the
CLOSE-camera reading. Osulo I settled is
`Osulo I, group, Medium Mediterrane., Colonized, collapsed, 2 of 8`; the pre-2026-08-22 defect said
`Osulo I, Colonized, 1 of 7`. To time it: fire the input in the background, then
`POST /wait` on `!GalaxyViewLevels.CameraSettling && GalaxyViewLevels.ZoomStep >= 12` (12-14 frames,
~0.9 s from the far camera) and dump `/gui/graph` immediately and again 300 ms later - the card's
words are there at once, its buttons ("group") 300 ms later.

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

**Ctrl+L, the go-to-location key.** `POST /key` needs a LONGER HOLD than the default for a chord to be
seen: `POST /key?hold=250&gap=150` with body `Ctrl+L` works, the bare `POST /key` body `Ctrl+L`
silently does nothing (measured twice, 2026-08-22 - the mod polls once a frame and the default press
is over before a frame ends). Test surfaces:
- **an open popup**: raise one with `RecordEventForEmpire` (below), read
  `notification:show-location`'s name - it must be `Show Location (Ctrl+L)` - and press the chord from
  anywhere on the popup; the popup goes aside and the map lands.
- **a strip row**: `Gui.GuiNotificationService.HideAllGuiNotifications()` puts the notification on the
  strip, `ui.focusNotifications` lands on it, and the row's buffer must read
  `Ctrl+L goes to location` then `Backslash to dismiss`. The chord must NOT open the popup
  (`/gui/graph` still says `screen.galaxy`).
- **a turn-log row**: raise a mod notification with
  `ES2Access.UI.ModNotifications.Raise(new ES2Access.UI.EventModFleetArrived(Gui.PlayerEmpire, fleet, node))`;
  `ui.focusTurnLog`, same hint order, same behaviour.
- **where absent**: on the end-turn button `ES2Access.ModEntry.Navigator.TakesGoToLocation()` is
  false, the physical chord is silent, and `POST /input ui.goToLocation` answers `unconsumed`.
**A notification shown with `ShowGuiNotification` is NOT in the empire's list** and so never reaches
the strip; to get a row, call the manager's private
`RecordEventForEmpire(gameEvent, empire)` by reflection instead
(`new EventQuestBegun(Gui.PlayerEmpire, quest)` on the pinned quest raises the quest-begun popup and
registers it).

## Batch-8 recipes (2026-08-22)

**The tooltip panel-feature audit table lives in `docs/es2-facts.md`** ("The tooltip PANEL-FEATURE
audit"): the corpus counts, the class-to-feature map for the commonest tooltips, which features have
typed readers, and the measured `StarSystem` reading. Regenerate the map from
`Public/Gui/GuiTooltipDescriptions*.xml` (strip XML comments first — several features are commented
out and a naive grep counts them).

**Reading one drawn tooltip feature by feature, when `DevProbe.Tooltip()` refuses.** It answered
`{"error":"No token to close. Path ''."}` on the research wheel's play-deck child (2026-08-22,
cause not chased). The same reading is available directly:
`Gui.GuiService.GetWindow<GuiTooltipWindow>(false)` → `ES2Access.UI.DrawnTooltip.Features(w.AgeTooltip)`
cast to `System.Collections.IList`, then unbox each entry as `ES2Access.UI.TooltipFeatures.Reading`
and print `Feature`, `Reader` and `Lines`. That is what produced the `StarSystem` table in
`es2-facts.md`. Which features the FALLBACK has answered for is
`ES2Access.UI.TooltipFeatures.DefaultRead` — walk it with a non-generic `IEnumerator`, the REPL will
not take `foreach` over it.

**The play-deck tooltip** (`PlayDeck` class, the only one in the fixture): on the research wheel,
Tab to `research:tree`, `POST /type "lethal"` → the second result is
`research:technology/TechnologyDefinitionMilitary19/tooltip/1` ("Lethal Squadrons"), a Tooltips
child of a COLLAPSED dot — reachable only since batch 8's search change. Its buffer holds one block
per tactic: the tactic's name, then "Flotilla 1 Short Range / Flotilla 2 Short Range / Flotilla 3
Long Range", then the effect paragraph.

**Searching what a collapsed branch would declare.** On the galaxy, `POST /type "raia"` with Dusay
CLOSED lands on `galaxy:constellation/446/system/535/planet/2` and announces
"Raia, Medium Terran, Colonized, 4 of 8" — the system opens on the way. The cost of the search's
one fully-open build is `ES2Access.UI.GraphNavigator.SearchBuildMs` / `SearchBuildNodes` (32-78 ms /
131 nodes on the galaxy at turn 21). To see the whole enumeration a search is looking through,
build it by hand from `/eval`: `new GraphBuilder(new HashSet<ControlId>())` with `ExpandAll = true`,
`screen.Build(b)`, `screen.BuildShared(b)`, then walk `render.Order` printing
`SearchScope.TextFor(node)`. **Corrected 2026-08-23 (batch 9):** "antimatter" IS findable now — a
system's deposit dossiers come from `node.Planets[*].ResourceDeposits`, not from the icons the label
happens to be drawing, so every deposit system carries one card per kind at every camera. Measured
across the whole slider on `[Beginner] test`: `hyperium` 2, `titanium` 2, `transvine` 2,
`dustcid` 4, `antimatter` 1 — eleven cards over the six deposit systems (Osulo, Qarius, Ita, Heka,
Primus, Leo), identical with the camera out over the galaxy and with it in on Dusay's orbital view.

**The star-system page's name and its page turn.** Entering (`to-system.cs`) announces
"Dusay, System management" once and seats on `system:planet/…`. `POST /input ui.pageNext` then
announces "Heka, System management" once and, about a second later, the new system's first planet
row. Expect ONE stray line between the two — the cursor migrating to a HUD stop while the page
declares nothing between systems. Check with `/speech?since=N` (exactly one screen-name line per
turn) and `DevProbe.Screen()` (`node` under `system:`). The regression to watch for is the ENTRY
landing on `hud:view-title/scan` instead: it means the screen went active before its planet cards
were drawn, and the walk's next Enter then toggles scan mode and poisons every later dump.

**Leaving scan mode from `/eval` is not obvious.** `IGuiGameWindowService` has no `RequestScanView`,
and pressing `GameOverlayWindow.TopTitlePanel.ScanButton` through `AgeWidgets.Press` did nothing.
`POST /loadsave` of the fixture is the reliable way out (and then re-minimize the tutorial and
`POST /reload` before any sheet-keyed comparison).

**Measuring a landing's camera cost.** The sharp instrument is a plain boolean `POST /wait`
predicate on the game's own gate, which reports `frames` and `elapsedMs`:
`ES2Access.UI.GalaxyViewLevels.FocusedSystem != null && Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemOrbital>(false).Shown`
answers "is the orbital-card surface up" (measured 1 frame / 0 ms after `SnapTo`, 8 frames / 598 ms
after `ZoomTo`), and `!ES2Access.UI.GalaxyViewLevels.CameraSettling` answers "has the flight ended"
(894 ms / 11 frames for `ZoomTo`). `DevProbe.RowTrace` is the blunter one and reads the navigator's
last built render, so it under-reports a row that changes without a rebuild.

**Alt+Home end to end** (`POST /key?hold=250&gap=150`, desktop unlocked): `Ctrl+PageDown` to a
category, `Alt+PageDown` twice to step off the thing the cursor is already on — landing on the
node you are standing on is silent — then `Alt+Home`. Measured 2026-08-22 with the instant camera:
the landing announced 394 ms after the key release ("Libra, -11, 11, group, No owner, collapsed,
5 of 13").

**The colonizable-planet scanner row** (`Ctrl+PageDown` to Colonizable Planets, `Alt+PageDown` to
step): "Libra II, Tiny Boreal, Binary Moons, Ruins, max population 6, Food 6, Dust 3, Science 5,
-11, 11, here, 1 of 7" — short resource names, and a resource the world does not make is simply
absent (Libra II has no Industry or Influence line).

**The scanner's Curiosities columns (batch 9, 2026-08-23).** `Ctrl+PageDown` five times from the map
stop reaches **Curiosities**; `Shift+PageDown` then steps "all" (16) → **Explorable** (6) →
**Insufficient Expedition Power** (10) → one column per kind (Atmospheric 1, Life Form 6, Ruins 4,
Signal 2, …). The two named columns are `Curiosity.CanBeSearched(empire, null, failures)` and the
`EmpireExpeditionPowerTooLow` failure it records; on `[Beginner] test` at turn 21 they partition the
whole category, so a curiosity in NEITHER (one already being expedited, or quest-locked) is
fixture-blocked. The kind columns count fewer than "all" because a kind is counted once per PLANET.

**Configuring a custom scanner category from `/eval` (stage 3, 2026-08-23).** There is no editor
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
**"Watch list: all, …"** LAST, after Probes, and wraps from there to Systems (2026-08-24 — before that
move the custom category led the cycle and an unconfigured slot 1 answered the arming press with
"none found"). `galaxy.scanSubcategoryNext` inside it steps **Systems: neutral** (10) → **Fleets: friendly** (6) → **Dusay** (5) → **all** (21), the three
partitioning "all" exactly. A slot configured with a selector this galaxy cannot answer
(`luxury:NoSuchResource`) is SKIPPED by the category cycle in both directions and answers its own
quick key with "{name}: all, none found". An unconfigured slot's quick key says
"No custom category on ," / "on Shift+/" — the key named off the live binding.
`ES2Access.UI.ScannerCost.Line()` reads **4–7 ms a press** whether or not a slot is configured,
which is the measurement behind composing every colonizable world's description up front.

**Deposit dossiers across the zoom (batch 9, 2026-08-23).** The evidence pair for a mod-owned
tooltip carrier: `POST /type "antim"` from the map stop, then
`Gui.GuiService.GetWindow<GuiTooltipWindow>(false).AgeTooltip.AgeTransform.gameObject.name` says
which widget is drawing it — `LuxuryItem_1` (the label's own icon) at the systems view level,
`Dossier deposit/543/StrategicDeposit4` (the carrier) once the camera is in on the system and the
deposit LINE has faded to alpha 0. `/gui/graph?buffers=1` of the focused card is byte-identical in
both states (8 lines, the refusal "Missing technology Extreme Atmospherics" included).

**Type-ahead stepping closes what it opened (batch 9, 2026-08-23).** On the galaxy with nothing
expanded, `POST /type "dustcid"` (4 results in 4 systems) then `ui.down` three times: exactly ONE
system is expanded at each step — the one the cursor is in — and `ui.back` ("Search cleared") leaves
the LAST one open. A branch the player expanded before typing is never closed: expand Dusay by hand
first and it is still open after the search has walked past it.

## Batch-10 recipes (2026-08-23) — the camera rule

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
opened and the camera on Osulo. `ui.right` opens the world: **7 dossier nodes** — five outputs then
Hyperium then Titanium (Osulo II has one deposit, Osulo III none, and neither shows the stale pooled
items). Expect the SAME seven at every zoom: `ES2Access.UI.GalaxyViewLevels.SetZoom(9, at)` from
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

**Forcing the orbital card's fixture-blocked widgets** (structure only, never content). On a card
whose `Planet` is bound, set `InProgressTerraformationButton.Visible/Enable = true` plus a plain
`AgeTooltip.Content`, and the same for `InProgressRestorationButton`,
`InProgressAnomalyReductionButton`, `PirateLairGroup` (plus its `AgeTooltip.Content`),
`OutpostCancelIcon.Visible` and `HauntIcon.AgeTransform.Visible/AgeTooltip.Content`. The mod then
declares "Click to cancel the action" and "A Pirate Lair is orbiting this Planet" as action children
and the two icon sentences as buffer lines. **Restore by hand**: the card's own refresh puts the
Visible flags back but NOT a Content you overwrote — write `PirateLairGroup.AgeTooltip.Content` back
to `%PlanetPirateLairDescription` and `HauntIcon`'s to empty, then set `card.Dirty = true`.

**The senate's census badges and the forced-law badge** (batch 11). Each `senate:census/arc/N` is now
an expandable group whose "Tooltips" region holds four nodes — the party the population leans
towards, what that gives the empire, the law it unlocks, and what the boost badge means. The three
dots are 8x8 pictures under `LabelsContainer/SubInfosTable/CollectionTable`; the fourth is
`PopulationBoostLabel`. `senate:law-slot/0` opens onto its forced badge's sentence. **Coverage reads
these as `unread` while the branch is COLLAPSED** (the probe's declaration side is the render as it
stands), so expand before believing a count.

**Closing a full screen when `/key` is refused.** The senate, the population modal and the star
system page do not claim Escape (`DevProbe.Claims("Escape")` → `claims:false`), so `POST /input
ui.back` is a no-op and only a real key would close them. With the game not in the foreground
(`POST /key` answers "the game does not have the foreground"), use the documented
`Gui.GuiService.HideWindow(...)` route — never `HandleInput(InputAction.Exit)`, which wedged the
screen stack in the previous stage. The game menu closes through its own **Resume Game** node.

## Batch-12 recipes (2026-08-23) — the two dismiss-all buttons and the juggernaut buttons

**Testing "Dismiss all notifications" WITHOUT losing the owner's pending news.** The button calls the
game's own `DismissAllGuiNotifications()`, which unloads and unbinds every notification, so the
`RecordEventForEmpire` replay above is the only way back and it needs the notification's own event.
Cheaper and exact: **stash the real ones out of the live list first**, since
`GetPlayerEmpireGuiNotifications()` IS the manager's list —
`var STASHED = (GuiNotification)((System.Collections.IList)Gui.GuiNotificationService.GetPlayerEmpireGuiNotifications())[0];`
then `list.Remove(STASHED)` plus the private
`OnPlayerEmpireNotificationsCollectionChanged(CollectionChangeAction.Remove, STASHED)` by reflection
so the strip refreshes (`var NOTIFY = …GetMethod("OnPlayerEmpireNotificationsCollectionChanged", …)`;
top-level `var`s persist across `/eval` requests, so the handle survives the `/input` presses).
Raise a DISPOSABLE game notification in its place
(`Notify(new EventEmpireIntroduction(pe))` — it auto-pops its popup, so
`Gui.GuiNotificationService.HideAllGuiNotifications()` right after to get back to the HUD), press the
button, then put the stashed one back with `list.Insert(0, STASHED)` + the same reflected call with
`CollectionChangeAction.Add`. Verified 2026-08-23: the strip read "Laws Cancelled" again with
`AlreadyRead` still true. Nothing is ever unloaded, so no rebuild is needed.

**Testing "Dismiss all Turn log entries"** needs no stashing: raise three MOD notifications (the
`ModNotifications.Raise` recipe above), but with DIFFERENT subjects — `ModNotification.Subject()`
dedupes repeats, so the same event three times is one row. `EventModFleetArrived(pe, f0, …)`,
`EventModFleetStopped(pe, f1, …)`, `EventModFleetArrived(pe, f1, …)` gives three. Then
`ui.focusTurnLog` → `ui.end` lands on the button, `ui.activate` presses it. The press itself says
nothing; what is heard is the cursor's reconciliation onto the nearest survivor.

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

**The notification strip's pooled items outlive the notifications.** After raising and dismissing
several, `Coverage()` reports `NotificationItem001..003` in the `hidden` bucket — retired pool
children keeping the previous binding's tooltip. Not a gap; take the count from
`GetPlayerEmpireGuiNotifications()`, never from the table's children.

## The mod's settings window (stage 2a, 2026-08-23)

**Getting there is the player's own route, and only that route counts.** In game: open the pause
menu (`Gui.GuiService.ShowWindow(Gui.GuiService.GetWindow<GameMenuModalWindow>())` from `/eval`
is the same thing Escape does), then `/input ui.down` five times from Save Game and
`/input ui.activate` — "Mod settings, button, 6 of 9", then "Mod settings" + "Keybinds, tab,
selected, …". On the MAIN MENU the entry sits after Options as 8 of 9; the whole main-menu route
is untested live from an in-game fixture, but the placement is checkable without leaving the game
with `GET /gui/graph?screen=screen.main-menu`, which reads the live window (not prefab content)
even while the screen is inactive — and it is the check that caught the node being declared in
the wrong branch, since the main menu's Options entry is a GROUP with a flyout while the pause
menu's is a flat button.

**Reading it.** `GET /gui/graph` on the mod window gives three stops exactly as the game's options
window does: `options:tabs` (one tab, "Keybinds"), `options:rows` (50 key-mapping rows), and
`options:buttons` (Cancel, Apply — Apply "unavailable, No modification detected." until something
changes, which is also the proof that the option getter's instance is stable). Row ids are
`options:0TabPanel/<index><action key>KeyMapping`.

**Rebinding without a keyboard.** The physical capture is the game's own and cannot be driven from
`/eval`; write the value instead:
`item.Option.Value = new Amplitude.Unity.Input.InputBinding("<action>",
Amplitude.Unity.Input.KeyCombination.FromString("Ctrl+K", "+"), KeyCombination.None);
w.OnOptionChanged(item.Option);` where `w` is `ES2Access.UI.ModOptions.ModOptions.Window()` and
`item` is the `OptionKeyMappingItem` on the row whose transform name matches. Then all four
follow in the same frame: the drawn field (`item.PrimaryKeyBindingField.Label.Text`),
`ChordNames.Of(ModEntry.Input, "<action>", 0)`, `NodeHints.Chord("<action>", 0)` — the delegate
every usage hint renders through — and `ApplyButton.AgeTransform.Enable`.

**Apply, Cancel and the file.** Apply and Cancel are pressed through the mod's own activate path
(`ui.next` twice to the button stop, `ui.home`/`ui.end`, then `ui.activate`). Apply hides the
window and lands the cursor back on the "Mod settings" entry it was opened from; Cancel with
changes raises the game's own "Are you sure you want to quit without saving?" box (a mod screen —
`ui.end` then `ui.activate` for Confirm) and restores every row. The file is
`<game>\BepInEx\plugins\ES2Access\settings.cfg`: after Apply it holds
`keys.<action> = <action>:Ctrl+K,` for exactly the actions that moved, and moving one back to its
default takes its line out again (the file goes to 0 bytes when nothing is moved).

**Reload-restore.** `POST /reload` destroys the clone (teardown by name) and rebuilds it on the
next frame; a shown window closes with it and the pause menu is focused again cleanly. The rebind
survives, because it is read from the file at `ModEntry.Start`:
`ChordNames.Of(ModEntry.Input, "ui.goToLocation", 0)` still answers the new chord and
`ModBindings.Moved("ui.goToLocation")` is true.

**What the harness cannot reach here.** The physical capture (Enter on a row → prompt → chord →
release → the row speaks what stuck), Escape on the mod window (`POST /key` needs the game
foreground; the registration that makes it work is checkable instead — the clone sits at the FRONT
of `GuiManager.guiWindowsFromBackToFront`, ahead of `GameMenuModalWindow`, and is an
`IInputHandler`), and the whole main-menu route.

### Key bindings, the table (stage 2b, 2026-08-23)

**Reading it.** Both windows read the same way. `/input ui.next` into `options:rows` lands on the
first row's name cell — "Controls, table, Confirm, Enter, ⟨description⟩, 1 of 41" — and then
`ui.right` / `ui.left` cross the columns ("Primary key, Enter, button" / "Secondary key, empty,
button" / back onto "Action, Confirm, …"), `ui.down` stays in the column and names the row it landed
in ("Cancel, empty, button, 2 of 41"). Cell ids are `options:⟨panel⟩/keys/row⟨hash⟩c⟨0|1|2⟩`.

**Driving a clear.** `/input ui.clear` on a key cell — the cell announces its new "empty" as a live
part, Apply lights, and the game's own value follows
(`(Amplitude.Unity.Framework.Services.GetService<IInputOptionsService>()).InputBindingsValidate
.ToString()` → `Validate: , `). Cancel on the window puts it back. The claim is checkable with
`DevProbe.Claims("Delete")`: `claims:true` on a key cell, false on the name cell and off the screen.

**Driving a COMMIT (which `Option.Value` alone does not do).** Writing the option's value skips the
game's commit method, so it raises no conflict box and no overlap warning. Drive the real thing:
set `item.PrimaryKeyBindingField.KeyCombination = KeyCombination.FromString("Ctrl+H", "+")` and then
invoke the private `OnLoseFocusCb` with `item.PrimaryKeyBindingField.gameObject`. That is the path a
finished capture takes, conflict check included.

**The overlap warning, both ways.** Game side: on the Controls tab, commit `InputBindingsQuickSave`
onto `Ctrl+H` (the mod's `ui.focusEmpire`) — the box reads "While the mod's Go to the empire banners
is active, the game's Quick Save will not fire" and the binding still lands (`QuickSave: Ctrl + H`).
Mod side: on the mod's Keybinds tab, commit `ui.goToLocation` onto `F1` — "While the mod's Show on
the map is active, the game's Empire Screen will not fire", and `ui.goToLocation: F1` sticks. One
Confirm button, no Cancel. Cancel on the window restores both sides.

**Simulating a capture without a keyboard.** `/input ui.activate` on a key cell speaks the prompt and
DOES hand over: an injected action holds no key, so the two clear frames pass at once and
`AgeManager.Instance.FocusedControl` becomes that `AgeControlKeyBindingField`. End it with
`AgeManager.Instance.FocusedControl = null` from `/eval`. What that cannot reach is the Escape half —
the cancel branch asks `Input.GetKey(KeyCode.Escape)`, and `POST /key` refuses while the game is not
foregrounded.

**Walking the graph and reaching the windows.** Pause menu → Options is 5 of 9, Mod settings 6 of 9;
from the tabs stop `ui.next` reaches the rows and `ui.next` again the buttons (`ui.home` is Cancel).
Cancel with changes raises the game's own confirmation (`ui.end` then `ui.activate` confirms) and
lands back on the pause menu. `/input ui.back` does NOT close a game-owned window: Escape is left to
the game and an injected action presses no key — use the window's own Cancel button instead.

### The Scanner tab and the custom-category tabs (stage 5, 2026-08-24)

**Getting there** is the stage 2a route plus one move: pause menu -> `ui.down` x5 ->
`ui.activate` ("Mod settings", then "Scanner, tab, selected, ..., 1 of 5" - Scanner is the tab the
window opens on), then `ui.next` into `options:rows`, which holds the three drawn slot buttons.
`ui.activate` on one opens that slot's tab and lands on its Name box. From `/eval`,
`ModOptions.OpenCategory("CustomCategory1")` switches tabs directly - but it does NOT move the
cursor, and a cursor whose row has gone re-seats onto a TAB, where landing switches the page
again; follow it with `ModEntry.Navigator.FocusStop("options:rows")`.

Node ids are `options:0TabPanel/slot{0..2}Button` on the Scanner tab and, on a slot's tab,
`options:{n}TabPanel/nameField`, `keyword{i}Field`, `newKeywordField`, `clearButton`,
`section{categoryKey}` (a caption - drawn, never a node) and `select{categoryKey}:{columnKey}`.
Regions are `options:{n}TabPanel/head` and `.../section{categoryKey}`, so `ui.regionNext` walks
the thirteen sections; the head region is what makes the name and keyword boxes a place Ctrl+arrow
can leave.

**Driving a text row.** `POST /type` cannot reach a game-owned field and `POST /key` needs the game
foregrounded, so: `ui.activate` on the row, wait a frame for the hand-over
(`TextFieldEditor.Editing` goes true and `AgeManager.Instance.FocusedControl` is the `TextField`),
then from `/eval`

```
AgeControlTextField f = AgeManager.Instance.FocusedControl as AgeControlTextField;
f.Label.Text = "Watch list";
typeof(ES2Access.Screens.TextFieldEditor)
  .GetField("CommitTheNextRelease", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
  .SetValue(null, true);
AgeManager.Instance.FocusedControl = null;
```

`CommitTheNextRelease` is internal, so it needs reflection; without it the focus drop is a CANCEL
and the pre-edit text goes back. MEASURED speech for a name: `"Watch list", "edited",
"Custom category 1, Name, editable, Watch list"` and then the rebuilt page's landing. A refusal
follows the landing - `"Systems is already the name of a category"`, `"A custom category needs a
name"`, `"That keyword is already in this custom category"` - and the box goes back to what it
held. An `ui.activate` on a row while a text edit is still PENDING starts an edit on that row
instead of pressing it (the screen captures raw input while a hand-over is waiting); recover with
`TextFieldEditor.Abandon(); AgeManager.Instance.FocusedControl = null;`.

**What to check.** Apply lights only once something differs
(`ModOptions.Window().ApplyButton.AgeTransform.Enable`); Cancel with changes raises the game's own
"Are you sure you want to quit without saving?" (`screen.message-box` - `ui.end` then
`ui.activate` confirms) and leaves `ScannerCustomSettings.Slot(0)` and the file untouched; Apply
hides the window and writes `scanner.custom.1 = Watch list|systems:neutral|Dusay`, which survives
`POST /reload`. Clearing then applying takes the key out of `settings.cfg` altogether. The Scanner
tab's button follows: "Custom category 1: Watch list" once named, "Custom category 2: empty" while
a slot stands empty.

**The stale-selector row** needs a selector the galaxy cannot answer. Write one behind the editor
(`ScannerCustomSettings.Slots.Slot(0).AddSelector(new ScannerSelector("luxury","NoSuchResource"));
ScannerCustomSettings.Save();`) and reopen: the Luxury section offers "NoSuchResource, not found
this game, checkbox, checked"; unticking it takes the selector out (the row stays until the page is
next built, which is what lets the player change their mind before Apply).

**On `[Beginner] test` at turn 21** the tab offers Systems 7 columns, Colonizable 2, Unexplored 1,
Anomalies 11 (10 kinds), Curiosities 8 (5 kinds), Luxury 3 (2), Strategic 4 (3), Contested 1,
Fleets 4, Probes 4, and 1 each for pins, missiles and quest markers. Note the anomaly keys are the
game's own and are not what a guess would produce: Multiple Moons is `PlanetAnomaly27Alt`.

**The minimised tutorial must NOT be declared over the settings window.** With the military
tutorial minimised, `/gui/graph` on `screen.options` must hold no `hud:tutorial` stop, and
`(Gui.GuiService as GuiManager).IsAnyModalVisible` must read true with `ModalOnTop` naming
`ES2AccessModOptionsWindow`. Hide the window and the bar is back on the galaxy - that pair is the
regression test for the clone's modal registration (es2-facts, stage 5).

**Leave the fixture with all three slots cleared** (the Clear button then Apply, or
`ScannerCustomSettings.Clear(0..2)`) - `settings.cfg` goes back to 0 bytes.

# Notification popups, the strip and the turn log

Raising, reading, restoring and auditing the notification family — the popups, the HUD
strip, the mod's own turn log, and the battle popups that arrive as notifications.

## The pending notifications in `[Beginner] test`

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
**The same live list is how a real notification is STASHED and put back** — the cheap, exact way to
test anything that dismisses the owner's pending news:
`var STASHED = (GuiNotification)((System.Collections.IList)Gui.GuiNotificationService.GetPlayerEmpireGuiNotifications())[0];`
then `list.Remove(STASHED)` plus the private
`OnPlayerEmpireNotificationsCollectionChanged(CollectionChangeAction.Remove, STASHED)` by reflection
so the strip refreshes (`var NOTIFY = …GetMethod("OnPlayerEmpireNotificationsCollectionChanged", …)`;
top-level `var`s persist across `/eval` requests, so the handle survives the `/input` presses in
between). Put it back with `list.Insert(0, STASHED)` and the same reflected call with
`CollectionChangeAction.Add`. Nothing is ever unloaded, so no rebuild is needed, and `AlreadyRead`
survives the round trip.
**A notification RAISED with `ShowGuiNotification` never joins the empire's list**, so it never
reaches the strip and `POST /loadsave` clears it without trace. To get a STRIP row, call the
manager's private `RecordEventForEmpire(gameEvent, empire)` by reflection instead — e.g.
`new EventQuestBegun(Gui.PlayerEmpire, quest)` on the pinned quest, which raises the quest-begun
popup and registers it.

## Raising a notification on demand

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

## Raising a MOD notification (the turn log)

**Raising a MOD notification** (populating the Turn log) is bus-safe — mod event types have no
game listeners: `ES2Access.UI.ModNotifications.Raise(new ES2Access.UI.EventModFleetArrived(player,
fleet, fleet.GetGameNode()))`, enumerating fleets by binding `DepartmentOfDefense.Fleets` as a
non-generic `IList`. For a GAME event with listeners, keep to the `RecordEventForEmpire` replay
above. End-of-turn news is stamped with the turn that ENDED, not the one you wake in — expect the
"Turn {n}" region one lower than the HUD reads after the boundary. The fixture tools that undo a
probe are in `docs/test-recipes/fixtures.md`.

## Working a popup that draws its own content

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

## "Construction Complete" is a table

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

## Exact non-regression, and the leading-prose rule

**The leading-prose rule cannot be seen on any live popup.** All three of the research family take
the no-visible-words branch — Construction Complete's description is real but its label is parked
under a hidden container, Technology Stage's is a localization key the files never answered, Research
Complete's is an unfilled template — so a popup that both SAYS and DRAWS something is unreachable
here. Test it as exact non-regression instead: snapshot `/gui/graph?edges=1&buffers=1` for all three
along a fixed browse route (Previous, Previous, then left + Next + Next back to Construction
Complete), change, reload, walk the identical route and `diff`. In one session the ids are stable
objects, so the three files come out byte-identical and need no hash normalising.

## The research popup's queue-empty states

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

## Quest and choice popups

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

## A popup's lore

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

## The parity probe

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
reflection** — `docs/gui.md`: the runtime getter reads the cached field, so a plain
`AddComponent` is invisible to the engine as well as to the mod). The one direction NOT seedable is
a declared-tooltip expectation with nothing to draw: the expectation and the check are the same
`AgeWidgets.Draws` predicate, so game state cannot separate them.

## The Laws Cancelled popup

**The Laws Cancelled popup reads as a SHEET** (post-fix shape, 2026-08-17): region
`notification:table:reg:0`, row key `notification:table:row<hash>c0`, cell `…c1`, captions
"Laws"/"Political Ideologies" spoken as the crossings, and the law's dossier on the ROW (the
class-backed `Law` tooltip draws on focus and fills the buffer). Fixture notes: no route exists
to re-summon a laws-cancelled popup once dismissed, and a raised popup cannot displace an open
one (`ShowGuiNotification` does not bring it to front) — regression checks on this family wait
for a naturally pending one. The research/construction table popups (the other sheet-reading
family) still owe a walk from before the wrapper-descent change.

## Sweeping the whole family

The apparatus — a bind-and-show template, a lent-data force-show template and a reducer that
collapses the JSONs outside context — is built per session in a scratch directory and is not
preserved in the repo; one `/eval` handle bank per session, re-issued after every `/reload`.
Three traps:
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

## Reading the description path without a popup up

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

## The election survey popup

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

## The deed and quest-completed popups

**The deed and quest-completed popups are the detached-description family** (ES2 facts): their
shape is four body rows (StatusTitle, ObjectiveTitle, "Outcome", ObjectiveLore) with **no words
node**, `notification:top` holding Next/Previous/Pop-up-automatically and `notification:bottom`
holding Minimize/Done. A words node reappearing on one of them, or the whole strip collapsing into
`notification:bottom`, is the detached-label regression.

## The collapsed-report family

**The collapsed-report family** — `IonWaveReport`, `ObliteratorAttackReport`,
`ObliteratorVictimReport`, `DisplacementReport`, `PirateMissionReport`, `ForceTruceProposed`. Each
draws a "+" (`ReportToggle` / `MissionReportToggle` / `Winner`+`LooserBreakdownToggle`) over a
detail panel it keeps FADED (ES2 facts), and each toggle is declared through the `Expanders` variant
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
(`EventIonWaveReport(PEMP, AIEMP, new IonWaveReport(SSN, PEMP.Index))` is the cheapest; the
per-family bind tables were measured live and are not preserved in the repo). `PirateMissionReport` is FIXTURE-BLOCKED: its `Bind` dereferences an
`AttackSystemPirateDiplomaticAction` that only a live pirate mission produces, and force-SHOWING the
window instead throws in `OnBeginShow` and leaves a half-shown popup drawing nothing — hide it at
once. `NarrativeEventBegun` needs a narrative quest (`QuestJournal.Read(QuestState.InProgress)`
index 26, `AcademyQuest01`, on the parity-audit save) and its choice cards come through the
`Choices` variant field; the fixture's card draws NO words at all (crop-verified: an icon on an
empty panel), so its node is legitimately nameless and carries only its dossier tooltip — and that
tooltip is `SimpleDescription`, so the parity probe reports it unaccounted until the node is
FOCUSED. Raising it also pops the "Unforgettable Events" tutorial, which retires itself when the
popup hides.

## Ctrl+L, the go-to-location key

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
A row on the strip needs `RecordEventForEmpire` — see **The pending notifications in
`[Beginner] test`**.

## The two dismiss-all buttons

**Screen model: each of the two stops ends with a "throw them all away" BUTTON** (owner ruling
2026-08-23; no new key binding — it is reached with the arrows and pressed with Enter, and pressing
says nothing of its own, exactly as dismissing one row does). **Each clears ONLY its own list**
(owner ruling 2026-08-24). `hud:notification/dismiss-all` ("Dismiss all notifications", keyed on the
game's own `BaseTriangleBackground` control) dismisses the notifications the GAME raised one by one,
the way Backslash does on a row, skipping every notification the mod owns. It does NOT call
`GuiNotificationService.DismissAllGuiNotifications()` — the call the game's Alt+right click on that
triangle makes — because the game's list is ONE list and that call takes the Turn log with it
(`docs/notifications.md`). `hud:turn-log/dismiss-all` ("Dismiss all Turn log entries", a region of
its own after the turn regions) discards only the mod's own notifications, the same way. Which list a
notification is in is one test in one place (`GlobalHud.Mine`, split by `Core/UI/OwnedNotifications`),
so the two buttons cannot disagree. Both stops are absent while their list is empty, so neither
button is ever offered over nothing.

**Testing "Dismiss all notifications" WITHOUT losing the owner's pending news.** The button dismisses
each of the GAME's notifications through `DismissGuiNotification` (it skips the mod's own), which
unloads and unbinds each one it touches, so the `RecordEventForEmpire` replay is the only way back
and it needs the notification's own event. Stash the real ones out of the live list first (**The
pending notifications in `[Beginner] test`**), raise a DISPOSABLE game notification in their place
(`Notify(new EventEmpireIntroduction(pe))` — it auto-pops its popup, so
`Gui.GuiNotificationService.HideAllGuiNotifications()` right after to get back to the HUD), press the
button, then put the stashed one back. Verified: the strip read "Laws Cancelled" again with
`AlreadyRead` still true.

**Testing "Dismiss all Turn log entries"** needs no stashing: raise three MOD notifications (the
`ModNotifications.Raise` recipe above), but with DIFFERENT subjects — `ModNotification.Subject()`
dedupes repeats, so the same event three times is one row. `EventModFleetArrived(pe, f0, …)`,
`EventModFleetStopped(pe, f1, …)`, `EventModFleetArrived(pe, f1, …)` gives three. Then
`ui.focusTurnLog` → `ui.end` lands on the button, `ui.activate` presses it. The press itself says
nothing; what is heard is the cursor's reconciliation onto the nearest survivor.

**Proving the two dismiss-alls do not reach into each other** (owner ruling 2026-08-24). Stand BOTH
lists up at once — the stashed-and-replaced game notification above plus three mod ones — then press
one button and read the OTHER stop before pressing the second: `GET /gui/graph` must still show
`hud:notification/*` after "Dismiss all Turn log entries", and the Turn log's `hud:turn-log/turn/*`
regions must still be there after "Dismiss all notifications". A stop that vanished is the defect;
the graph, not the speech, is the oracle, because pressing either button says nothing of its own.

**The notification strip's pooled items outlive the notifications.** After raising and dismissing
several, `Coverage()` reports `NotificationItem001..003` in the `hidden` bucket — retired pool
children keeping the previous binding's tooltip. Not a gap; take the count from
`GetPlayerEmpireGuiNotifications()`, never from the table's children.

## Battles

**The battle fixture** is a 14-step script (measured live; not preserved in the repo) because a battle cannot be
created from `/eval` — it needs two hostile fleets meeting. Everything before the meeting is
read-only; from the setup popup onward the run is destructive, so it goes LAST and ends with
`POST /loadsave`.

**Ground-battle SETUP popup** (verified live 2026-08-25, player attacking). Stop order: title,
balance ("Manpower L against R"), [yours] role / Assigned / Reserve / troop rows / three details
rows (health, damage, bombing), [theirs] same minus the two multipliers (the game hides them;
enemy special line "May inflict pre-battle damage" has no tooltip), [aftermath] tactic cards /
population badge / improvements badge, [controls] Watch / Fight — 9 positions on the attacker
fixture. Probe handles: `w.BattlePowerGauge.AgeTransform` IS the `PowerBalanceGroup`;
`w.Left/RightContenderPanel as GroundBattleContenderSetupPanel` reaches the role and details
labels. The badge rows are named from their PARENT groups' tooltips (`Cells.AddStat`, null title);
the details rows read at `Visible=true, Alpha=0` while the DETAILS accordion is collapsed — the
crop is the oracle for what is DRAWN (evidence rects: badges `965,546,180,48`, collapsed DETAILS
`423,500,460,140`). Selecting a tactic card changes the pending order — restore card 0 if toggled.
Each card's class-backed "GroundBattleStrategy" tooltip carries the full per-tactic numbers
(bombing, multipliers, deployment-limit change) in the focused buffer. The notification audits
prove nothing here (bodies invisible to them — roadmap); use `DevProbe.Tooltip()` directly.

**Ground-battle REPORT popup** (verified live 2026-08-25, attacker, continuing siege). Stop order:
title (outcome word; buffer adds the game's description sentence), balance ("Manpower L against R",
final manpowers), [yours] strategy (buffer holds the tactic dossier) / Remaining / Reserve / troop
rows / "Damage Dealt" + one row per drawn damage block, [theirs] the same shape, [aftermath]
population / improvements / will-continue, [controls] Retreat / Continue — 7 numbered positions.
Probe handles: `w.Left/RightPlayTooltip` (class `GroundBattleStrategy`) hang on
`PlayCardLeft`/`PlayCardRight`, NOT on `PlayTitle` — aim at the tooltip's own transform;
`w.Left/RightContenderPanel as GroundBattleContenderReportPanel` reaches `DamageIcon`/`DamageGauge`.
Evidence rects: damage gauges `858,321,60,270` and `1192,321,60,270`, manpower rows
`393,340,1324,110`. **Never activate** Retreat/Continue/Replay/Minimize/Dismiss — all five dismiss
the popup and Retreat also posts `GroundBattle.OrderStandBy()`.

**Ground-battle OUTCOME-SELECTION popup** (modelled 2026-08-25, NEVER sighted live — needs a
decisive victory). Tier-zero inventory off the unshown window (instantiated, `Shown=False`,
`GuiNotification=null`; fields all non-null): `SystemNameLabel`/`SystemLevelLabel`/
`SystemPopulationCountTable`/`SystemPopulationNoneLabel` (`%None`)/`SystemImprovementsLabel`
(`"[improvement] N"`)/`SystemWondersLabel` (`"N [wonder]"`)/`OutcomesTable`/`TimerGauge`. Prefab
texts are placeholders the bind rewrites — content claims wait for the live popup.
`SystemPopulationCountPrefab` (= `CapturedPopulationCount`) carries `PopulationCount` + one button
(`OnClickCb`); `OutcomeItemPrefab`'s toggle has `UseDoubleClick=True` (`OnDoubleClickCb` = pick AND
validate). The `ValidateButton` ("Confirm") is bound to no window field — by-name lookup only
(ES2 facts). The countdown is multiplayer-only (ES2 facts); expect NO timer row in single player.
The notification parity probe reads `nodes:5` on any body-owning popup — it cannot referee this
family; probe directly. LIVE (sighted 2026-08-25): the popup is destructible by a single
keypress — one Enter on a card posts `OrderSelectGroundBattleOutcome`, the double-click chord
validates and COMMITS — so the only safe verification is `ui.down`/`ui.up` plus
`GET /gui/graph?buffers=1`, with "the previously selected card still reads `selected`" as the
after-dump guard. It arrives alongside the ground-battle REPORT notification (Alt+Left/Right
move between them), and the window can change under you if a person is at the keyboard —
re-dump before interpreting.

## The turn log's usage hint

`ES2Access.UI.ModNotifications.Raise(new ES2Access.UI.EventModFleetArrived(Gui.PlayerEmpire, fleet,
ES2Access.UI.FleetOrders.Orbit(fleet)))` gives one `hud:turn-log/<turn>/0` row ("… arrived", then
"Backslash to dismiss"); `ui.contextual` on it dismisses and the whole stop goes, which restores the
fixture and proves the wiring in the same press.

## Fixture-blocked

- **Never swept**: `PirateMissionReport` and the five battle-stack types (`BattleSetup`,
  `BattleReport`, `GroundBattleSetup`, `GroundBattleReport`, `HackingOperationOutcomeSelection`),
  whose `Bind` throws without a live encounter or hacking operation and which no force-show has yet
  been able to show. Every other type reads `clean:true`.

- The queue-empty branches of the research-complete popup (**The research popup's
  queue-empty states**).
- A quest in CHOICE state, and therefore the `GuiRadioGroup` side of the radio/checkbox rule
  (**Quest and choice popups**).
- A popup that both SAYS and DRAWS: all three of the research family take the no-visible-words
  branch (**Exact non-regression**); the election survey is the one that does both, and no save
  has one pending.
- A multi-row Laws Cancelled popup, and any re-summoning of one once dismissed
  (**The Laws Cancelled popup**).
- `PirateMissionReport`, and the ground-battle OUTCOME-SELECTION popup's live content
  (**The collapsed-report family**, **Battles**).

# Modals, the out-game family and the main menu

Opening a modal without walking to it, the windows that must never be confirmed, and
everything reachable outside a running session.

## Opening game modals from `/eval`

**Opening game modals from /eval** (to measure one without walking there): set what its opener
sets, then show it — for the improvements list, `var w =
Gui.GuiService.GetWindow<ImprovementsManagementModalWindow>(); w.ColonizedStarSystem =
...ColonizedStarSystems[0]; Gui.GuiService.ShowWindow(w);`. Close the way Escape does:
`w.HandleInput(InputAction.Exit)`. A modal whose opener installs DELEGATES has to be opened
through the opener's own handler — reflection for a private `Cb(GameObject obj = null)`, since
`SendMessage` with no argument logs an arity error and does nothing (ES2 facts); the worked
route for `SystemSelectionModalWindow`, with its never-press warnings, is below. The two
text-box surfaces, for a keyboard-focus probe: `GetWindow<LoadSaveModalWindow>()`, set
`LoadSaveMode = LoadSaveType.Save`, `ShowWindow`; and `GetWindow<RenameModalWindow>()`, set
`OriginalName`, `ShowWindow` — at rest on BOTH, `AgeManager.Instance.FocusedControl` is null and
`DevProbe.Claims("Return")` is `claims:true`, the "game is not holding the keyboard" check the
edit-field defects turn on. The chat page without a chat key: `ChatHold.OpenOnTheBox(panel)` by
reflection on the loaded assembly opens the panel AND pushes the chat screen; stepping out of
typing (`FocusedControl = null`) closes both — the game's own validate on an empty box shuts the
panel, leaving nothing behind.

## The rename box

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

## The system-selection modal

**Opening the system-selection modal** (`SystemSelectionModalWindow`, the outpost side panel's
"change colony" picker). Its Confirm does nothing without the DELEGATES its opener installs, so
open it through the opener's own private handler by reflection:
`typeof(OutpostInfoSidePanel).GetMethod("OnClickChangeColonyCb", System.Reflection.BindingFlags
.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(UnityEngine.Object
.FindObjectOfType<OutpostInfoSidePanel>(), new object[]{ null })` — `SendMessage` with no
argument logs an arity error and does nothing (ES2 facts). Escape/Cancel is safe (commits
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

## The improvements modal

**The improvements modal has nothing destructible in the beginner fixture** (last checked turn 1):
ticking a tile, the Scrap button's enabled label and its confirmation, multi-row wrapping,
scroll-into-view, the assigned-hero readout and the empty-list state are all code-verified only.

## The system-politics modal

**The system-politics modal.** Open it from the star-system page's own node. "Show all events" is
persistent WINDOW state — restore it — while the party pick is not. The table binds
`canSelect:false`, so nothing in it commits. In `[Beginner] test` the representatives panel's
"Shows in detail…" button is unavailable, so the modal has no player-gesture route there —
`Bind(ColonizedStarSystems[0])` + ShowWindow stays the only in, and it raises BREAD AND CIRCUSES
(minimize after).

## Hero selection and the modal-return cursor

**Hero selection and the hero list.** The pickers are reachable from the academy family. **Never
press Confirm and never press the card's own Content button**; selecting a card commits nothing,
but `Refresh` wipes `SelectedHero` (ES2 facts), so a cached selection is meaningless. Note the
**modal-return cursor**: closing any modal over the star system page lands on the planets stop's
start node rather than on the button that opened it — pre-existing, and true of improvements and
rename too.

## The election wizard

**The election modal** is a THREE-step wizard (Support / Votes Breakdown / Results — measured on a
real interactive election 2026-08-24, all three steps Coverage-clean): every step's Next/Previous
is non-committing, and the outcomes are never drawn (ES2 facts).
A real interactive election can be forced on a disposable save:
`Gui.PlayerEmpire.GetAgency<DepartmentOfDomesticAffairs>().Senate.ForceAnticipatedElectionsAsap =
true`, then end the turn — the wizard raises itself (verified 2026-08-24; single graphical player
gets Interactive mode automatically).
`election:local/support` declares `election:local/{title,trends,empire}` regions and pushes the
`PoliticsSupportGroup` caption over the party bars; measured on that election: the bars arrive
under "Political Trends", not bare.

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
`/eval` on the panel's private `starSystemElectionInformations` (ES2 facts) — the bars themselves
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
tier other than "Established", and the election-action outcomes (never drawn — ES2 facts).

## The minor-civilization window

**Opening the minor-civilization window from `/eval`.** Its `Bind` takes the minor empire AND the
game node its home system sits on, so both have to be found before it will show: walk the game's
empires for a `MinorEmpire`, take its home `StarSystem`'s `GameNode`, then
`var w = Gui.GuiService.GetWindow<MinorFactionDiplomacyModalWindow>(); w.Bind(minor, node);
Gui.GuiService.ShowWindow(w);`. Close it the way Escape does —
`w.HandleInput(InputAction.Exit)`, Assembly-CSharp's `InputAction`. In a normal session the route
is the galaxy map: a minor's home system label draws a diplomacy button (`StarSystemLabel`'s
`DiplomacyButton`, class tooltip `MinorFaction`), and its Enter opens this window.
On a save that has already MET one, find the `MinorEmpire` in
`(Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.Game.IGameService>().Game as
global::Game).Empires` (indexed, never `foreach`ed — the REPL's generic poison), then
`w.Bind(minor, minor.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems[0].Node);
Gui.GuiService.ShowWindow(w)`. In the owner's turn-22 save Niris is `Empires[4]`, at Osulo.

**Screen model: the window is named entirely by the game** (2026-08-22). Screen name = the
window's own title plus whose window it is ("Minor Civilization diplomacy, Niris"), with
`screen.minor-diplomacy` left as the fallback; the mod strings `minor.identity`, `minor.relation`
and `minor.gains` are retired and the `minor:gains` stop with them. Four stops:
`minor:identity` — the window title as its first row (that title carries the only sentence about
what the window is for), then the drawn empire name as a `PushContext` level over the regions
`minor:identity/{about,traits,planet-effects,opinion}` ("Traits" and the two panel-feature
captions are the game's words); `minor:relation` — named "Diplomatic Relation" with that
caption's own row, regions `minor:relation/{state,rewards,modifiers}` plus the gauge's
`minor:gauge/tooltips` "Tooltips" region (the four band sentences the prefab hangs along the
gauge, one node each, hidden while at war exactly as the game hides them); `minor:actions` —
named by the game's "Actions" caption, which it does draw, with that caption's own row
`minor:actions-title` (the `diplomacy.actions-band` mod word is the fallback here now);
`minor:treasury` unchanged. The identity panel is declared COLUMN BY COLUMN, not by drawn row: the
lore paragraph is one tall block beside three short ones and the rectangle banding interleaved them.
Both caption rows are resolved from the LABEL the prefab names uniquely
(`RelationInfoTitle`/`ActionsTitle`) and read off its PARENT group, because the window draws the
word on the label and hangs the sentence on the group, and three different groups in it are called
`TitleGroup` (2026-08-22 live fix: asking for `TitleGroup` answered with the faction banner, which
named the relation panel "Niris" and left both sentences with no surface — ES2 facts).
**The same propagation reached the Academy and pirate windows**, whose action bands carry the
identical shape: `academy-diplomacy:actions-title` and `pirate:actions-title` name those stops by the
game's drawn "Actions" and carry its sentence, with `diplomacy.actions-band` as the fallback. Neither
window opens in `[Beginner] test`; the ACADEMY half is still prefab-verified only, while the pirate
half was measured live on 2026-08-30 (a save where a Pirate Lair has been contacted — see "The pirate
window" below) and reads as designed. The Academy's own `RelationInfo/TitleGroup` ("Status") is NOT converted — its stop is still
named by `academy.relation`, awaiting the owner's ruling with the rest of that screen's wording.

**The gauge's four bands are named by the game, twice over** (2026-08-22): "CORDIAL (25)" — the
relation state the band buys and the relation points it starts at, composed through `minor.band` =
"{0} ({1})". The state comes off the band's OWN sentence key
(`%DiplomaticRelationStateMinorCordialDescription` → `…Title`) and the threshold off the segment's
position on the bar, so neither half is hard-coded and a patch that re-cuts the bands moves both.
The sentence is announced after the name (the 2026-08-28 kind ruling removed the muting that
once held it to the buffer; it stays reviewable there too).
Beside them, the relation POINTS row is captioned **"Relationship"** (`minor.relationship`, a mod
phrase — owner ruling 2026-08-22, replacing the gloss sentence the shared last resort had been
using as the row's name; the sentence is now an ordinary tooltip). The Academy's relation-state row
keeps its own reading.

**The first-contact card names its two uncaptioned figures** (2026-08-22): the
`MinorEmpireMetNotificationWindow` card draws "None"/"Unknown Empire" and the relation state beside
bare icons and puts the captions on the icons' tooltips, so those two rows are declared with the
game's own `%MinorFactionCurrentAllyTitle`/`%MinorFactionRelationTitle` as their names and the
drawn words as their values ("Ally, None"). Declared for that one prefab, not as a rule over every
popup's drawn body.

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

**First contact with a minor** (the popup, or `MinorEmpireMetNotificationWindow`'s card driven on
the unshown window — `MinorFactionCard.Refresh(playerEmpire, minorEmpire, Gui.ImageSize.Mood)`):
the card's two figure rows must read **"Ally, None"** (or "Ally, Unknown Empire") and
**"Relation, UNKNOWN (?, +0/turn)"**, with the icons' sentences still in their buffers. Every other
popup's drawn body must be unchanged — walk a battle report and a construction report and diff.

## The pirate window

**Walking the additional-firepower track** (`PirateDiplomacyModalWindow`). The window needs a save
where the player has contacted a Pirate Lair, which `[Beginner] test` is not; the routes in are the
diplomacy page's pirate button and a pirate-held system's diplomacy button. Expected (`/gui/graph`,
measured 2026-08-30):

- `pirate:power` opens on `pirate:window-title` — the drawn "Pirate Diplomacy" carrying the only
  sentence about what the whole window is for — and the "Pirate power" band comes after it. Same
  shape as `minor:identity`'s first row.
- `pirate:next-fleet` ends in the track: `pirate:reinforcements-title` reading **"Additional
  Firepower:"** with its own sentence (the game hangs that one on the LABEL, not on the group), then
  one `pirate:threshold/N` per circle reading **"Threshold 1, 21 percent"** or **"Threshold 1,
  reached"**. The `ReinforcementsThresholdsKeyCircle` beside them repeats the caption's sentence and
  is left as decoration.
- The percentage is of THAT mark's own stretch of the track, never of the whole: the thresholds are
  cumulative costs, so mark N runs from the sum below it to its own total
  (`RefreshReinforcementsThresholdItem`). Cross-check against the drawing — `.\crop-shot.ps1` the
  `ReinforcementsGroup` rect: the orange fill before circle 1 must be about the fraction the first
  mark speaks, and a BRIGHT circle must be a "reached" node.
- The circle's effect lines ("Bonus effects applied to the Fleet:" / "+1 Movement Points on Ships")
  are buffer-only. Their tooltip hangs on the Circle CHILD, so a node aimed at the item AROUND it
  makes `DevProbe.Tooltip()` answer `shown:false` with an empty buffer — that was the defect fixed on
  2026-08-30, and it is what the walk re-checks: focus a threshold, `TooltipDelay(0)`, step off and
  back on, and both `Tooltip()` and `/gui/graph?buffers=1` must carry the lines.
- The states the fixture's stock cannot reach (a mark REACHED, a mark clamped below its own stretch)
  are exercised on the composer instead of the game: reflect the private static
  `ES2Access.Screens.PirateDiplomacyScreen.Mark` from `/eval` and invoke it with
  `(widget, index, min, max, stock)` triples.

## DLC panels and windows

**Forcing a DLC side panel without the DLC.** The prefab INSTANCES exist regardless: bind the panel,
set `Visible=true`, read the graph, then `Unbind` + hide and re-diff. The same holds for every
`NotificationWindow` instance — all of them exist whether or not the DLC that raises them is
installed, so notification variants are readable structurally even when they are unsightable.

**The three bind-and-show openers the DLC stage used** (the datatables load whether or not the
expansion is owned — ES2 facts, so these give real CONTENT, not just structure):
the Juggernaut specialization modal binds off a fleet ship reached through
`DepartmentOfDefense`; `ContextualPromptWindow` binds with a `ContextualPromptGuiElement` —
**never press its "Yes"**, which commits the hacking operation behind it; and
`StarSystemPopulationModalWindow` binds `...ColonizedStarSystems[0]`, which raises the BREAD AND
CIRCUSES tutorial — minimize it afterwards.

## The tutorial picker and the new game lobby

**The tutorial picker** is raised by `NewGameScreen.OnBeginShow` and only while
`TutorialManager.IsPlayingForTheFirstTime()` (registry `GameSettings/HasAlreadyPlayedOnce`, which
only `GameClientState_Introduction` ever sets — cancelling leaves it, so the box comes back). Back
to the MAIN MENU is two Escapes, i.e. `window.HandleInput(InputAction.Exit)` on the modal and then
on `NewGameScreen`. Never press Confirm or double-Enter a card in a test: both start a game.

**Screen model: the lobby's competitor bands are named** (2026-08-21). The New Game screen's
Competitors panel already jumped band to band with Alt+Up/Down (`newgame:competitor/<i>`, one per
drawn slot), and each band is a `PushContext` level saying "Player {n}" (`new-game.player`) — said on
arrival in the band and never while walking its four rows. `n` is the slot's PLACE IN THE PANEL
counted in drawn order (the grid is four across, so the second row starts at Player 5), not
`LobbySlot.Index`, and the player's own Empire panel is a stop of its own and is not counted. The
game captions every slot "AI", so nothing it draws tells two of them apart; multiplayer draws the
same panel from the same class and gets the same words. Cost of the level, as ui-navigation warns:
positions re-base, so a row reads "3 of 4" within its band instead of counting the whole stop.

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
the dialog is up (`/gui/graph?screen=screen.load-save` answers "not active" from a running game).
The PLAYER's own route reaches it from a running game and is the one to use — physical `POST /key
Escape` raises the pause menu (`/input ui.back` is a mod action and never gets there), then
`ui.activate` on "Save Game" (1 of 9) or `ui.down` once and activate for "Load Game" (2 of 9);
Cancel in the modal's command stop closes it and puts the pause menu back. Only the type-ahead
that searches SAVES rather than cells is still live-checked from the manual script. The window CAN
also be raised in-game from `/eval` the way the pause menu does
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

**The window's stops (2026-09-02): three in the save skin, two in the load skin.**
`loadsave:content` (the title caption row, the cloud toggle, the sort band and the saves),
`loadsave:name-field` (the save-name box alone — only the save skin draws it), `loadsave:commands`.
Tab cycles content → field → commands → content and Shift+Tab back; the commands walk with
Up/Down, not Left/Right. **Arrival lands on the FIRST SAVE in both skins**, never on the field:
save mode says *"Save Game" / "Save Game, table, [Beginner] access test, not selected, 1 of 11"*,
load mode *"Load Game" / "Load Game, table, …, 1 of 16"*. Which side of the table the cloud toggle
and the field sit on is measured, not listed (cloud band −1, field band 1 at 1280×800), so the
field's stop is declared after the table because that is where it is drawn.

**An empty saves table** happens for a fresh profile (no saves at all) on the main menu's Load Game
and on the in-game Save window. Prove it without touching a save file: set every child of
`w.GuiTable.LinesTable` to `Visible = false` from `/eval`, dump, then set them back. The content
stop then holds the title row, the cloud toggle and the sort band and nothing else, and the start
node is the title row — the player hears *"Save Game" / "Save Game, Save the current game, 1 of 2"*
and reaches the headings with Down. (The dump's traversal begins at the render's start node, which
is what makes the first line printed the proof.)

## The out-game family

**Walking the out-game family from inside a session.** Leave the session first: show
`BlackCurtainWindow`, then get the client as
`Gui.GetActivePlayerController().GameInterface as GameClient` and call
`Disconnect(GameDisconnectionReason.ClientLeft)` — `GameClient` is not a `UnityEngine.Object`, so
a `FindObjectsOfType` route does not compile. The menu comes up with the pages
reachable. Per page, `Gui.GuiService.ShowWindow<T>()` and `HandleInput(InputAction.Exit)` to close,
EXCEPT the disclaimer, which swallows every action (ES2 facts) — close it through its own Accept
node. **Never press**: Decline on the disclaimer (quits the game), Confirm on the mod manager
(reloads the runtime), or any store/web button (leaves the game). The DLC browser REMEMBERS its
selected tab across opens — put the tab back when done. The whole family also walks from the main
menu with `/input`: Content opens the DLC browser, Mods opens the mod manager (its Back node
closes it), and the Mods flyout's "Game asset export" child is the exporter's player route — no
`ShowWindow` needed for any of the three. The exporter can also be forced with
`Gui.GuiService.ShowWindow<ResourcesExportScreen>()`, but it must be LEFT with
`ShowWindow<MainMenuScreen>()`, never `HideWindow` — hiding it strands a screenless state where
`POST /loadsave` answers `[not ready]` forever.

**The journal's filter menu** (2026-08-24): `ShowWindow<JournalModalWindow>()` works from the main
menu too; five of its nine drawn headers carry a funnel (`journal:filter/<Property>/<-n>`), Enter
opens `screen.table-filter` (15 empire checkboxes on the Empire column), Escape closes it via the
game's own `ToggleFilter` path. The custom faction editor's trait tables never draw a funnel — the
prefab wires no `FilterToggle` (ES2 facts) — so the journal is the only live surface.

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
Dismiss and Minimize (ES2 facts), so its sentence rides the screen name.
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
`ToggleScanView()` is the scan view's in and out. In `unlocked` the star system page itself binds one,
which arrives EXPANDED and takes the keyboard — collapse it with the replay in
`docs/test-recipes/fixtures.md`.

## A tutorial popup over a modal

**The collapsed-tutorial-under-a-modal window** (the one state where a minimised popup can speak
over the page underneath): expand the popup, open a modal over it — an `AboveModalWindows` tutorial
stays `Shown`, and the mod's tutorial screen stands down for the modal while its linger stays armed —
then minimise it while the modal is up, then close the modal. Watch `/speech` across the close: the
tutorial's title and page must not be in it. Every step is an `/eval` (the improvements modal's
opener is under **Opening game modals from `/eval`** above), so the whole repro is four requests.

## Multiplayer and chat

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
the in-game panel is live in single player (ES2 facts). Seed the log through the game's own send:
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

## The negotiation table

The treaty modal, opened from the diplomacy ring by clicking a MET empire (or from a
relation-change popup's link). Nothing opens it in `[Beginner] test`: it needs a met major, which
that save has none of, so every reading below was taken on the owner's live game and the fixture
block is real.

**The hazard that shapes every visit: closing an unsigned negotiation POSTS AN ORDER.**
`UnBindActiveContract` posts `OrderChangeDiplomaticContractState(Inactive)`, so this is one of the
few windows a stage may not open and close freely to look at. On the owner's own game, do not open
it at all unless he has it open; when he does, drive it with `/input` and leave the contract exactly
as found. Escape stays the GAME'S here for the same reason — the mod does not consume Back on this
screen, unlike every other modal of the family (ES2 facts, gui.md).

**What to verify, and where each reading comes from.** The stops are the window's own bands:
`negotiation:title`, `my-empire`, `relationship`, `their-empire` (the dossier is a REGION nested in
it, opened by the Empire Information tick box, not a stop of its own), `pressure`, `my-terms`,
`their-terms`, `contract` (whose LAST row is the deal approval, not a stop of its own) and
`actions`. Each shelf is a header row (Name/Type/Cost) over a `GraphSheet`, with the six category
filters as ONE horizontal row entered at whichever is in force. Enter on a term row sends the
window's own `OnSelectTerm` and the row says selected/not selected; the basket row that appears
carries the quantity stepper (Enter opens the edit — the arrows do NOT adjust it, `interaction.md`).

**The gotcha that looks like a bug and is not:** the deal approval reads the empty word for an empty
contract AND for a one-sided gift or demand, because the game refuses to draw an approval for a
`Declaration`-type contract even though it has computed one (ES2 facts,
`heroes-and-diplomacy.md`). Put a term on BOTH sides to make the band appear before calling it a
defect. The pressure band's two threshold markers carry no words of their own — their place along
the bar is read off `GaugeThresholdItem`'s `PercentLeft`, which is the game's own number.

**The shelves are pooled and fade-retire**, so a shelf of five terms can sit in a table of nine bound
children; the count must match the DRAWN rows, and "N of 9 with four unreachable" is the symptom of
a walk that asked `Visible` instead of drawn-ness (gui.md).

## The load/save modal's usage hint

`LoadSaveModalWindow` with `LoadSaveMode = LoadFromGame` gives "Ctrl+Alt+Enter to load" on all
eleven rows, and flipping the same window to `Save` makes every one of them vanish.

## The cutscene modal

**Playing any cutscene video on demand** — the whole family (faction intro, colonization, outro,
metaplot) is one call, no game state needed, and it works from the MAIN MENU: `Gui.GuiService
.GetWindow<CutsceneModalWindow>(false).ShowWindow(System.IO.Path.Combine(UnityEngine
.Application.streamingAssetsPath, "Movies/Colonization/Arctic.mp4"), null)`, and for an outro add
`, true, null, true, "LostBack"` to pick the ending's own subtitle and description track. Movie
names are the game's affinity codenames (`docs/notifications.md` §Cutscene videos).
`Gui.GuiService.HideWindow(…)` cuts it short.
For the colonization window instead, `GetWindow<ColonizationCutsceneModalWindow>(false)` and
`Bind(planet)` before showing — any `StarSystemNode.Planets[0]` off `Gui.Game.Galaxy.GameNodes`
will do; the planet only feeds the card, not the video.

**Proving a description track's TIMING**, not just its content: note `/speech`'s `next`, fire the
video, then poll `/speech?since=N` once a second against wall clock. Cues arrive at their authored
offsets plus a constant ~1.2 s of video load, so the DELTAS between them are the oracle — Arctic's
1.0/4.5/8.5/11.0 landed at 1.2/5.2/9.2/11.9. The variant half needs an A/B on a pair whose timings
actually differ: `Horatio_Outro` is the sharpest (LostBack speaks at 8.2 s, LostNotBack is silent
until 26.0 s), while the three `Terrans_Outro_*` pairs differ by 0.2 s and prove nothing.
`ES2Access.UI.CutsceneDescriptions.Movie/Variant/CueCount/Playing` is the direct probe.

## Fixture-blocked

- The numeric editables: the Marketplace tab and a negotiation quantity have no fixture
  (**The rename box**).
- Multi-row navigation, a visible re-sort, a REFUSED row's sentence, the scroll view and an
  operable policy drop list (**The system-selection modal**).
- Ticking a tile, the Scrap button's enabled label and confirmation, multi-row wrapping,
  scroll-into-view, the assigned-hero readout and the empty list (**The improvements modal**).
- The carousel auto-advance, the row re-read on Next System, a progress line off "M of M", a
  winner with several badges or none, the senator-hero card, the experience-GAIN gauge, a tier
  other than "Established", and the election-action outcomes (**The election wizard**).
- The four gauge bands and the influence-modifier line need a minor neither at war nor unknown
  (**The minor-civilization window**).
- The multiplayer-only lobby states, renaming the player, saving/editing/deleting a custom
  faction, and column overflow at 1280x800 (**The tutorial picker and the new game lobby**).
- The mod manager's library is empty in this install; the alliance tab and the new-message
  button stay unreachable in single player (**The out-game family**, **Multiplayer and
  chat**).

# Battle popups and the advanced setup

Raising, reading and auditing the battle family — the space-battle setup and report popups,
the battle plan chooser, the ADVANCED battle setup window, and the ground-battle popups.
The notification family these popups arrive through is `docs/test-recipes/notifications.md`.

## The battle fixture

**The battle fixture** is a 14-step script (measured live; not preserved in the repo) because a battle cannot be
created from `/eval` — it needs two hostile fleets meeting. Everything before the meeting is
read-only; from the setup popup onward the run is destructive, so it goes LAST and ends with
`POST /loadsave`.

## The space-battle setup popup

**Space-battle SETUP popup** (verified live 2026-08-29, player vs pirates, one fleet a side). Stop
order in `notification:content`: title, arena, [yours] fleet header / one row per flotilla,
[theirs] fleet header group / ships, [aftermath] balance, [plan] the Battle Plan combo row and then
Advanced / Watch / Retreat / Fight in the SAME band (owner, 2026-08-29: Alt+Down from the balance
lands on the plan and stops there — the buttons after it are the next rows down, not a section) —
8 numbered positions with every group shut. The BALANCE line is
directional and names the two sides ("Balance of power: 1st Conquerors Navy has 103% more military
power than 8th Greedy Pirates"); the names come from the first non-reinforcement garrison of each
`EncounterGroup.Setup.ContenderSetups` entry, which is the same string the roster header draws.
A flotilla the battle left EMPTY is a plain row ("Flotilla 1, Empty" — the game's own `EmptyLabel`),
not a group: nothing hangs under it, and the lines and their badges carry no game tooltip anywhere
(measured on `FlotillaLine`, `FlotillaIndexGroup`, `Circle`, `FlotillaIndexLabel`, `EmptyLabel`).

## The battle plan chooser

**The PLANS are a DROPLIST, not a band.** Pressing the game's own Previous/Next arrow does not show
a plan, it CHOOSES it, so the popup carries ONE closed row ("Battle Plan, combo box, ⟨plan⟩,
⟨effect line⟩") and Enter opens `screen.battle-plan` — a mod-owned child screen, no layer, one
`GraphNodes.Choice` row per `group.AvailablePlays` entry, landing on the plan in force. **Arrival on
a chooser row turns the card** (shortest way round, guarded on the row being the focused one), Enter
ACCEPTS and closes, Escape puts back the plan that was in force when the list opened, and the
chooser refuses to open at all while the game has the arrows switched off (the closed row then reads
`unavailable` and swallows Enter — force it with
`w.Previous/NextPlayButton.AgeTransform.Enable = false`, and put it back). Readback for every step:
`w.LeftBattleGroupSetupPanel.EncounterGroup.Setup.PlayDefinition` against `AvailablePlays`. Card
region for the evidence crop: `950,562,210,282` (the title, the three range diagrams and ONE effect
line change together). The card's effects table is POOLED — `Item001` sits at `Visible=true,
Alpha=0` holding the previous plan's line, so the row reads it with `AgeWidgets.PaintedText` and the
crop is the oracle. At most ONE chooser row is expanded at a time, the one the cursor is inside: the
nested children all read the single drawn card, so a second open row would read this row's card
under that row's name.

Nested "Tooltips" children, four on the closed row and four on each chooser row: the family badge
(named with the game's own family title, "Aggressive", and keeping its own different sentence) and
one per flotilla range diagram. A range child says its line ONCE — "Flotilla 1: Short Range", never
"Flotilla 1: Short Range, Short Range": the name carries the sentence, so `TooltipChildren.AddPlain`
takes it back out of the sections (`Unrepeat`).
Ship rows carry one child each, the role badge named with the game's role title ("Attacker",
"Exploration") plus the whole different sentence behind it. The arena row carries one, the
Separator's static "Effects applied to all the ships in the Theater" — `ArenaGroupTooltip` itself is
class `Simple` with EMPTY content whenever the theater applies no effects.
`TooltipParity()` is clean here except `Previous/NextPlayButton`, which stay `uncovered`
(owner-ruled subsumed by the chooser). `NotificationParity()` keeps four accepted residues:
`battle-setup/balance` `unlocatable` (it is computed, and the game draws only an arc); the same two
arrow buttons under `tooltips`; `placement` for the two hero portraits and the four bottom-bar
controls; and `honesty` "says what nothing draws" for every row whose name the mod composes out of
a key the game draws no words for — "Flotilla N" (`%FlotillaNameTitle` over a line that draws the
bare number) and the "(Ctrl+L)" on Show Location. The unfocused-plan-row `honesty` entries the
paged band produced are GONE with the band. **Expand Flotilla N, the enemy garrison, every ship
row, the arena row and the Battle Plan row before believing either audit** — a collapsed branch
reads as `unread`/`decoration`.
**Never activate** Fight, Retreat, the Watch toggle or the auto-popup box.

## ADVANCED battle setup

**ADVANCED battle setup** (`screen.advanced-battle-setup`, `AdvancedEncounterPlayModalWindow`) — the
window the setup popup's Advanced button opens. Fixture used throughout (owner's, 2026-08-29):
system Sabel, "1st Conquerors Navy" at 2 CP with Patrol3 + Endeavor in flotilla 2, flotillas 1 and 3
LOCKED below their CP thresholds, enemy Pirates. Stops: `advanced-play:heading`,
`:tactics` (region `:tactics-region`), `:yours-stop` (`:yours`), `:theirs-stop` (`:theirs`),
`:stats` (`:figures`), `:controls`.

**Five stops, each naming itself on the way in** (owner ruling 2026-08-29, superseding the
two-sides-two-stops ruling of the same day, which had left the hand of plans buried at the head of
your fleet): `ui.next` walks heading → TACTICS → your fleet → enemy → STATS → controls → the
tutorial bar → back to heading. The three plan radios are alone in `:tactics` — picking a plan is
what this window is for, so it is one Tab from anywhere and never the length of a roster away — and
it is also `InitialFocusStop`, where the cursor seats on arrival (measured: lands on the SELECTED
radio, "Power to Shields … selected, 3 of 3"). Four context words, one per band:
"Tactics", "Your fleets", "Enemy fleets", "Stats" — the last two of those four are new with this
ruling. Measured whole cycle 2026-08-29, r46 (six `ui.next` from the tactics stop): "Your fleets,
DefaultPlayerName (United Empire)" → "Enemy fleets, Pirates" → "Stats, Range, …, 4 of 4" → "Your
fleets, checkbox, checked, …" → "Tutorial, MADE TO ORDER" → "Advanced Battle Setup, Gives you more
options…" → "Tactics, Power to Shields, radio button, …, 3 of 3".

The HEADING stop is three rows: `advanced-play/title` (the window's own title, carrying "Gives you
more options for preparing your battle plan"), `/location` and `/arena`. The rest of the walk: in
`:tactics`, `ui.down` ×2 crosses the other two plan cards and nothing else — the stop holds only the
three; `ui.right` on a plan card opens its "Tooltips" region (family badge, then one range entry per
flotilla, "1 of 4".."4 of 4"). In `:yours-stop`, `ui.down` from the leader line crosses the hero, the
fleet header and then the flotilla rows. `ui.next` ×3 from `:yours-stop` reaches `:controls`, where
`ui.home` + `ui.down` is the Enemy fleets switch — the only cheap way to make the enemy roster (a
`BattleGarrisonPanel`, not flotilla lines) declare anything. Toggle it back.

**The heading's two labels each have an icon beside them, and the game explains the pair on
whichever it felt like.** `ArenaGroup` holds two boxes, each a `Separator` icon plus a label:
`SystemInfo` (`687,84,48,48` + `ArenaLocationValue`) and `ArenaInfo` (`960,84,48,48` +
`ArenaNameLabel`). The LOCATION label carries the drawable `StarSystem` dossier and its icon carries
a plain sentence, so the row points at its own and the icon becomes a nested entry ("Tooltips, Star
system where the battle is taking place", drawn at `696,153,342,43`); the ARENA label's own tooltip
is `Simple/content=0/notarget`, which the engine can never draw, so that row points at the ICON
instead and speaks its sentence ("Effects applied to all the ships in the Theater",
`969,153,342,43`). Neither is wired by name: `Note`'s `Beside` asks `AgeWidgets.Draws` which of the
siblings the game would really draw (measured 2026-08-29, r42; before this the arena row's request
parked at `timer=998` and drew nothing).

**A plan card SAYS its effects.** The window draws all three cards at once, each permanently on its
own plan, so `BattleNotifications.PlanEffects(card)` — the card-only core, no carousel guard — is a
Value part on every radio: "Team Spirit, radio button, +100% Effect of Morale Bonuses on Fleet,
selected, 1 of 3". The "n of 3" is now the ANNOUNCER's own stamp: with the plans lifted into a stop
of their own, the `:tactics` context is the only one on this window pushed with positions LEFT ON,
and the hand-declared `AnnouncementKinds.Position` part the radios used to carry (needed while they
shared the position-suppressed `:yours` level with the leader line and the flotilla rows) is gone.
Verified identical on r46: "…, selected, collapsed, 3 of 3". `:yours` and the two roster levels stay
suppressed — those rows are not one numbered set — while `:figures` keeps positions on, because its
pager rows are a list. Evidence crop for the three cards: `633,186,654,282`.

**A flotilla is drawn TWICE**, and the second drawing is where the words are. The roster line holds
only the number and `EmptyLabel`; the arena card holds the unlock sentence
(`EncounterPlayFlotillaCard3D.CommandPointsLabel` — "Unlocked at 5 CP and 2 Ships" for a locked one,
"Minimum 1 CP" for the open one; all three are chain-visible and alpha 1 on this fixture, so all
three are spoken) and the range hover (`EncounterPlayFlotillaCard2D`'s own `AgeTooltip`, "This
flotilla is optimal at Short range and has 58% compatibility with the ships"). The host hands both
in through `BattleRosters.FlotillaExtras`; the popups pass nothing and read unchanged. Reach the
cards typed — `window.EncounterPlayScreen3D.PlayerEncounterPlayContainers` → the one
`EncounterPlayFlotillaCardContainer` → `FlotillaCard2DContainer.Children` → each child's
`EncounterPlayFlotillaCard2D` — never by widget name. **Match line to card by the NUMBER the line
DRAWS** (`FlotillaIndexLabel`, 1-based) against `EncounterPlayFlotillaCard2D.Index` (0-based), never
by child order: measured 1↔0, 2↔1, 3↔2, and `Setup.FlotillaSetups[i].IsFlotillaValid` agrees
(False/True/False), but two collections built by different code agreeing today is not a contract.
The enemy side has no cards to match — its 3D container is an `EncounterPlayFleetCardContainer` and
its setup panel holds a garrison panel with zero `BattleFlotillasPanel` (measured), so its roster is
passed no extras.

**The arena's labels are NOT where their rects say.** `CommandPointsLabel.GetGlobalPosition()`
answers the layout rect inside `Container3D` (`96,303,343,30`); the perspective camera draws the
text skewed and far lower, and the roster panel is drawn OVER the left of it. A crop taken from the
rect lands on the roster panel and shows nothing — crop `400,600,560,340` instead to see
"…cked at 5 CP and 2 Ship[s]". The 2D card's tooltip, by contrast, is an ordinary tooltip window and
`DevProbe.Tooltip()` gives its live rect (`356,652,342,43` with the cursor on Flotilla 1).

**The sorting band's sentence is on the group ABOVE the buttons.** `SortingButtonsGroup`
(`355,1116,249,66`) carries no tooltip at all; its PARENT `SortingGroup` (`282,1116,322,66`) carries
"Choose a preset to automatically place your ships into your flotillas", which no mouse can rest on
without resting on a button. It is declared as a reviewed (buffer-only) section on the FIRST sorting
button, not as a second aimed tooltip. The whole band reads `unavailable` while
`Setup.ValidFlotillasCount <= 1` — which is this fixture, so the sorting buttons' ACTIONS are
fixture-blocked here.

**The STATS band is a four-row PAGER, not four switches.** `advanced-play:stat/0..3` in
`:figures`, under the "Stats" context, one row per page, and standing on a row turns the window's
box to it (the same idiom as the tutorial's pages and `FactionChoiceScreen.BuildHulls`); the four
`StatsToggles` are not declared at all. Entering the stop lands on the page already showing
(`LandStopOn`), so `ui.next` into it turns nothing; a position the player left there still outranks
it. The context carries positions (the rows ARE a list, and the stamp is what replaced the ticked
switch), so the counts are unchanged at "1 of 4".."4 of 4" — the fighters line would be a fifth
sibling where the window draws one, and this fixture draws none.

**A pager row's buffer is one line per figure.** The page's name and each sentence it draws are
separate announcement PARTS, so the readout is the one composed sentence it always was and the
buffer is steppable: "Military power" / "Balance of power: … 310% more military power …" / the
diagram's tooltip (measured 2026-08-29, r42 — before this the name ran into the balance sentence on
one line). The trajectory page's parts are one per curve SLOT in `TrajectoryContainerLeft`, resolved
at read time, so an undrawn slot contributes to neither surface. **Row 1 aims at the band of
switches**: `StatsTogglesGroup` (`820,1116,279,66`) carries "Choose which set of stats you want to
see", which is what that row already said and now also draws (`TooltipPipe` →
`over=StatsTogglesGroup`, tooltip at `829,1151,342,43`); rows 2-4 aim at their first diagram as
before. Verify the page follows the
cursor by probing `w.StatsPanels[i].Visible` after each `/input` — measured 2026-08-29:
`ui.next`→`stat/1` (page unchanged), `ui.down`→2, `ui.down`→3, `ui.home`→0, `ui.end`→3, and
`POST /type "traj"` lands on `stat/0` and turns the box with it. The focus guard is the same shape
as `BattlePlanScreen.Show`: a `GET /gui/graph` taken with the cursor at `advanced-play/plan/0`
resolves all four rows' labels and leaves the page where it was.

Index↔panel mapping, measured (the mod asks the panels by the gauges the window keeps in them, never
by index): 0 `TrajectoryContainerLeft` `514,201,893,870`, 1 `PowerBalanceGroup` `690,366,540,540`,
2 `DamageGroup` `630,306,660,660`, 3 `RangeGroup` `675,351,570,570`. The MILITARY page's row carries
`BalanceText`, which is computed from the encounter groups and not from the gauge — measured
2026-08-29: the row still reads "Balance of power: … 310% more military power …" with the
TRAJECTORIES page showing, so this user of `BalanceText` no longer needs a radio flipped to be
sighted.

**The rings say their figures only by how far round they are drawn.** `BattlePowerGauge.Refresh`
writes `MaxAngle = share * 360 - 1` into each `AgePrimitiveSector`, so the painted share is
`(MaxAngle + 1) / 360` and the mod reads exactly that (`BattleArcs`) rather than recomputing the
window's arithmetic. Measured on this fixture: energy 270.44/87.56 → 75/25, projectile 359/-1 →
100/0 (the right arc is `Visible=false` at `MaxAngle=-1`, read as the 0% it is), short 191.56/166.44
→ 53/47, medium 151.73/206.27 → 42/58, long 234.86/123.14 → 66/34; each pair rounds to 100 from its
own arc, so neither half is derived from the other. Independent oracle for the range trio:
`GuiBattleHelpers.GetAverageRangeEfficiency(w.Player|EnemyEncounterGroup.Setup.GetShipsData(),
"Short"|"Medium"|"Long")` — 0.575/0.5, 0.7/0.95, 0.95/0.5, i.e. 53/47, 42/58, 66/34, an exact match.
Call it inline inside the eval, never into a local: the returned `IEnumerable<IEncounter…>` is a
constructed generic over a game type and poisons the REPL session. The ring THICKNESS
(`MaxRadius - MinRadius`) is the window's cross-ring comparison and is what "Energy is the bigger
threat" / "Medium range matters most" report; a tie says nothing.

**The stats rings are painted in the 3D arena, not in the panel rects.** A crop taken from a panel
rect above lands high and left of the rings. The arena region that frames them is `740,600,540,420`;
the aimed tooltip is up within ~120 ms of the landing and covers the middle of the range trio, which
is why the eyeball check reads the rings' colours (blue = player, grey = enemy) rather than
measuring them.

**Trajectories: all three curves are DRAWN, locked flotillas included.**
`TrajectoryContainerLeft.Children` holds `TrajectoryCurve0..2`, each `Visible=true, Alpha=1`; a
locked flotilla's curve is `Enable=false` at `Opacity=0.25` (`EncounterPlayTrajectoryCurve.Bind`
fades rather than hides), so all three are spoken on this fixture where only flotilla 2 is valid.
The container's own `Visible` is the PAGE's state, so the curves are asked one step
(`AgeWidgets.DrawnChild`), never through `DrawnChildren`'s container gate. The range clause is the
CARD indicator's own composition — `Gui.Localize("%AdvancedPlayFlotillaOptimalRangeTitle",
Gui.GetLocalizedTitle(rangeName))` = "Short Range"; `GetLocalizedTitle` alone answers the bare
"Short".

`TooltipParity()` on this screen: all four defect buckets (`promised`, `misaimed`, `unraised`,
`unaimed`) empty over 33 nodes with the window as `root` (measured 2026-08-29, r42). `uncovered`
holds exactly six, ALL owner-accepted omissions: the two arena chips' bare-number tooltips ("1450",
"1510") and the four `StatsToggles` descriptions ("Displays stats about …", "Toggle trajectory
visuals on/off") — the pager is the switches' whole job and the owner ruled those four out.
`MediumRangeGauge` and `LongRangeGauge` sit in `decoration` although their words ARE in the rows'
buffers: a reviewed (buffer-only) section carries no tooltip identity for the audit's `Covering`
test, which is the same answer the sorting band's sentence gets. A COLLAPSED group's dossiers read
the same way — the location icon's sentence, each plan card's family badge and range indicators, and
the ship rows' `RoleIcon`s (in `unread`) are all declared as nested entries and only DECLARED while
their owner is expanded, so a run taken over a collapsed tree reports them. Expand before believing
one. Expansion state does NOT survive `POST /reload`: the flotilla
groups and the plan cards all come back collapsed, because `GraphState.Expanded` is rebuilt empty
and every group in the mod is collapsed by default. Re-expand before believing a dump.

**Arranging the fleet: the lock and the carry** (verified live 2026-08-29, r42). A ship row under a
flotilla line carries the game's pin on the DOUBLE-CLICK chord, is a carry SOURCE, and is a drop
target for its own flotilla; a flotilla line is the same drop target. Both arrive through
`BattleRosters.FlotillaExtras.Ship` / `.Row`, which only this window
hands in — the enemy side of this same window is passed `null` and its rows read "Prowler, Health,
2000/2000" with no state word and nothing to pick up (measured with the Enemy fleets switch on).
`FlotillaExtras.Ship` is handed the `FlotillaLine` as well as the row, which is how a ship row finds
the card its own flotilla is drawn as.

*Row → chip identity, measured.* `BattleShipItem.ShipSetup` is a public property the prefab NEVER
fills (`Bind` sets `GuiBattleShip` and nothing else) — it reads null on every row of every battle
surface, so do not use it. The identity is `item.GuiBattleShip.ShipData as EncounterShipSetup`,
which is REFERENCE-equal to the entry in `Setup.FlotillaSetups[i].ShipSetups[j]` (both fixture rows
resolved to `[1][0]` and `[1][1]`); the chip is then the `EncounterPlayShipItemInteractive` in some
card's `AllShips` whose `ShipSetup` is that object, and the drop container is `chip.Card.Container as
EncounterPlayFlotillaCardContainerInteractive` — the same route the chip's own drag-completed
callback takes.

*The lock is on `ui.doubleClick` (Ctrl+Alt+Enter), not on Enter* (owner ruling 2026-08-29, reversing
the same day's activation binding): the chord runs `ShipSetup.LockedInFlotilla = !…` — the game's
whole double-click — and speaks the new state, "locked in flotilla" / "not locked". The row keeps
the state words and has NO role word (it is not a checkbox any more), and its buffer ends
"Ctrl+Alt+Enter to lock or unlock this ship in its flotilla" (`hint.lock-ship`, rendered from the
live action table) before the carry's own "Space to drag ⟨ship⟩.". Probe the result in the sim, not
by ear: `UnityEngine.Object.FindObjectsOfType(typeof(EncounterPlayShipItemInteractive))` answers an
ARRAY (safe in the REPL), and each chip's `ShipSetup.LockedInFlotilla` plus `GetHashCode()` tells
which one flipped — measured `0:True→0:False` on Endeavor with Patrol 3 untouched.
`DevProbe.Chord("Ctrl+Alt+Return")` reads `suppressed:true` on a ship row.
**The PHYSICAL chord is unproven from a script**: `POST /key` refuses (409) unless the game holds
the foreground, and the injected `POST /input ui.doubleClick` presses no key. Nothing on this path
branches on key state (`KeyGraph.DoubleClick` just calls the vtable), but the physical press is an
owner manual-test line. Read the RESULT off the
chip's tint, not off pixels — the setter raises `LockedInFlotillaChanged`, the chip's handler calls
`Refresh`, and locked paints `Icon.TintColor`/`Glow.TintColor` white while unlocked leaves them the
empire colour. Measured pair on Patrol3: locked → `RGBA(1,1,1,1)` + `PreferredFlotillaIndex 1`;
unlocked → `RGBA(0.118,0.431,0.784,1)` + `-1`, with Endeavor staying blue throughout.
**A crop cannot see the chips.** They are `ShipItem`s in `Container3D`, so the same perspective
offset the arena's labels have (above) applies: `GetGlobalPosition()` answers `397,610,24,24` and
`397,636,24,24` and a crop of either lands on the roster panel drawn over that spot. The chips are
24 age-pixels projected into a camera view; the tint fields ARE the drawing, and a frame diff across
the toggle is useless because the window animates continuously (measured: a 1920×1200 diff at
threshold 60 lights up the timer band and the arena trails and never isolates a 24-pixel chip).

*The carry.* `ui.carry` on a ship row → "Dragging Patrol 3. Enter to drop, Escape to cancel.", and
`DevProbe.Claims` reads Space `claims:true` on a ship row and `claimsBack:true` once something is
held. `ui.activate` on a flotilla line commits through the GAME's
`EncounterPlayFlotillaCardContainerInteractive.OnDropShipItem`, which hit-tests the chip's
`Position.center` against each card's `CardRect`: the mod centres the chip's own rect on the target
card's, calls the container, and restores the saved rect on false, exactly as `OnDragCompletedCb`
does. Measured refusals on this fixture, both leaving `FlotillaSetups` counts and the chip's rect
`(367,409,24,24)` untouched and the carry alive: a LOCKED flotilla answers with the game's own
unlock sentence ("Unlocked at 5 CP and 2 Ships" — read off `Card3D.CommandPointsLabel`, and only
where `IsFlotillaValid` is false, since the open flotilla's label says "Minimum 1 CP" and is not a
reason for anything), and a SAME-FLOTILLA drop answers "Patrol 3 cannot go there" —
`CanAddShip` → `ShipSetups.Contains` → false, and the juggernaut branch does not apply to a small
hull. `ui.back` answers "Cancelled drag" and goes no further.
**No flotilla row says "drop target" on this fixture, and that is correct**: `DropAccepts` is the
game's own `CanAddShipItem`, which refuses the two locked flotillas and the one the ship is already
in, so all three refuse and none advertises. Both the "draggable" word and the "Space to drag …"
hint vanish from the ship rows while something is held, as the carry idiom requires.

**A SHIP row takes a drop too, into the flotilla that ship is in** (owner ruling 2026-08-29): the
game's own drop is a hit test against whichever flotilla card contains the point, and a ship is
drawn on its flotilla's card, so the ship row is wired with the same card its flotilla line has.
The collision this replaced is the thing to re-check after any change here: carry Patrol 3, press
Enter on Endeavor — measured "Patrol 3 cannot go there" (the drop was attempted and the game
refused it, same flotilla) with Endeavor's `LockedInFlotilla` UNCHANGED across the press, and Enter
on an idle ship row answers with silence and changes nothing. The accepted case on a ship row is
fixture-blocked for the same reason the flotilla one is (below): every flotilla here is either
locked or the one the ship is already in.

**The carry makes the game's own two noises** (owner-approved 2026-08-29). A mouse dragging a chip
hears `Gui.PlaySound(951096559)` as the drag starts (`EncounterPlayShipItemInteractive`
`OnDragStartedCb` :85) and `Gui.PlaySound(4116586482)` at every ending (`OnDragCompletedCb` :97,
:101, both branches), so `CarrySounds` posts the same two around the keyboard carry — pick-up,
successful drop, refused drop, cancel — for the battle-ship cargo kind ONLY; the lock stays silent,
as the game is. Nothing over the wire can HEAR them: `DevProbe.Sounds()` answers `posted` and `last`
instead, and the measured sequence on this fixture was `0/0` → pick-up `1/951096559` → refused drop
`2/4116586482` → `ui.back` `3/4116586482`. **Owner manual test: that these are audible and are the
same two cues a mouse drag makes.**

*`Coverage()` delta.* The two `…/FlotillaCard3DContainerLeft/ShipItem/LockButton` entries under
`actionsUncovered` are STILL reported and are accounted for rather than fixed: the audit matches a
node to the widget it stands on, and the node carrying that double click stands on the roster line
for the same ship. One per ship in the player's flotillas, always.

**FIXTURE-BLOCKED — needs a 5+ CP fleet (owner manual test).** With two valid flotillas: (a) carry a
ship off flotilla A, walk to flotilla B, confirm B's row says "drop target" and its buffer ends
"Enter to drop ⟨ship⟩.", press Enter and expect "Moved ⟨ship⟩ to Flotilla ⟨n⟩" — then check the
ROSTER LINES redrew, i.e. the ship's row now sits under B, since only `RefreshFlotillaCards2D` is
proven to follow a drop and `BattleGroupSetupPanel`'s own refresh cadence is unverified — and the
same press on a SHIP ROW inside B, which is wired to B's card as well; (b) the
juggernaut swap — a juggernaut carried onto a FULL flotilla holding a same-size, same-CP ship: the
drop succeeds and the two ships trade places, but no row advertises it, because `CanAddShipItem`
says no and the mod does not restate the container's dispatch to predict the swap. Press Enter
anyway and expect the move; (c) the three sorting presets with a ship LOCKED, confirming the locked
one stays where it was put.

**Never activate** Fight or Retreat here either.

## ADVANCED battle report

**ADVANCED battle report** (`screen.battle-report-advanced`, `AdvancedEncounterReportModalWindow`) —
the window the report popup's Advanced button opens (`battle-report/advanced`; the Back button
returns to that popup, and `Back()` answers false so Escape stays the game's). Layer 42. Fixture
used throughout: the owner's own session, 2026-08-30 — a fought battle vs Pirates, "Decisive
Victory", ONE flotilla a side ("Flotilla 2"), the enemy wiped, 2 of the 3 phase columns fought.
Reached by activating `battle-report/advanced` on the notification popup; **never**
`w.HandleInput(InputAction.Exit)` from `/eval`, which wedges the stack.

**Five stops**: `battle-advanced:heading`, `:tactics`, `:phases`, `:damage`, `:controls`, then the
shared `hud:tutorial` and round to the heading. Regions inside them are `battle-advanced:yours` /
`:theirs`. Measured whole cycle (r3, 2026-08-30): heading (8 positions) → tactics (the two plan
records, one per side) → phases (2 positions on this fixture) + the roster region while a fleet
toggle is out → damage → controls (4) → tutorial (3) → heading.

**The heading is eight rows**, and three of them are things the window draws as pictures and writes
down nowhere:

- `battle-advanced/outcome` — "Decisive Victory, You destroyed all the enemy ships".
- `battle-advanced/balance` — `BattleNotifications.Balance` (internal since this stage; one
  question, one home) over `w.BattlePowerGauge.AgeTransform` (= `PowerBalanceGroup`,
  `885,174,150,150`, tooltip `%NotificationBattleSetupPowerBalanceDescription` → "This diagram
  shows the balance of power between the two opposing fleets."), `w.Player/EnemyEncounterGroup`,
  `setup:false`. Measured powers 1998.4 vs 0, so this fixture exercises the **`BalanceAllKey`
  branch**: "Balance of power: 1st Conquerors Navy has all the military power, 8th Greedy Pirates
  has none". Evidence crop `600,100,720,300` — the ring is solid blue with no grey arc. It is the
  one `unlocatable` node in `Ghosts()` (synthetic, as on the setup popup).
- `battle-advanced/your-morale` / `their-morale` — ONE line per holding side, in that side's region.
  The line is the mod's own STATEMENT, not the game's caption: `battle.your-morale-bonus` = "Your
  fleet had the morale bonus" / `battle.enemy-morale-bonus` = "Their fleet had the morale bonus",
  with `%SpaceBattleMoraleBonusDescription` ("A fleet gets a morale bonus when it has more active
  flotillas than its opponent.") behind it as before. The game's own title
  `%SpaceBattleMoraleBonusTitle` ("Morale bonus") is deliberately NOT the line — read out it is a
  caption followed by a definition, and the owner could not tell whose fleet had one (2026-08-30);
  whose it is, is drawn as the icon's empire COLOUR, which speech has not got, and the region a row
  sits in is not spoken. Measured landing: "Your fleet had the morale bonus, A fleet gets a morale
  bonus when it has more active flotillas than its opponent., 5 of 8". The gate is
  `group.GetPropertyValue(SimulationProperties.EncounterGroup.MoraleBonus) > 0`
  (`AdvancedReportPhaseItem.Refresh` :39-62 asks the GROUP, then stamps the same colourised
  `[happiness]` on EVERY fought phase — a group-level fact repeated, not a per-phase reading).
  Measured: player 1, enemy 0, so one line and one icon colour — the ENEMY phrase is therefore
  fixture-blocked here and only its resolution is proven
  (`ModStrings.Get("battle.enemy-morale-bonus")` → "Their fleet had the morale bonus"). The row
  points at the FIRST drawn
  `MoraleBonusLabel`; evidence pair `740,940,420,170` shows the two blue thumbs over "Phase I" and
  "Phase II", none over "Phase III", with the tooltip drawn at `800,1056,342,43`.
- `battle-advanced/their-flotilla/N` — the enemy's arena cards, see the flotilla block below.

**The tactics stop is the two plans the battle was fought under.** The window instantiates one
`BattlePlayCard` per side into `PlayerPlayCardContainer`/`EnemyPlayCardContainer`
(`AdvancedEncounterReportModalWindow` :95-116, bound in `OnBeginShow` :182-201), so each card is
permanently on its own plan and `BattleNotifications.PlanEffects(card)` (the card-only core) is a
Value part exactly as on the ADVANCED setup window. Each row is wrapped in the same
"Your fleets"/"Enemy fleets" context the report POPUP puts round its own plan rows, because the
title is the same on both sides: `%NotificationBattleReportSelectedPlayTitle` → **"Selected Plan"**.
The stop itself is named "Tactics" from `battle.tactics` — the SAME key the ADVANCED SETUP window
names its own hand of plans with, so the two windows say the same word for the same thing
(2026-08-30). Measured landings: `ui.next` into the stop says "**Tactics**, Your fleets, Selected
Plan, Power to Shields, +100% Shield absorption on Ships, +10% Shield capacity on Ships, collapsed",
and `ui.down` to the other side says "Enemy fleets, Selected Plan, Hard Target, +25% Long range
defense bonus on Ships, collapsed" — the "Tactics" level is already entered by then and is not
repeated. Crop `600,100,720,300` shows both cards printing those same effect lines.
`ui.right` opens the card's "Tooltips" region — `BattleNotifications.PlanDossiers(card, null)`,
nothing to turn: "Tooltips, Defensive, Focuses on reducing damage taken. Every Fighter Squadron
remains with the friendly ships to protect against enemy bombers., 1 of 4", then "Flotilla 1: Medium
Range, 2 of 4" / "Flotilla 2: Long Range, 3 of 4" / "Flotilla 3: Long Range, 4 of 4". Three range
indicators are drawn per card whatever the battle fielded, `Enable=True` only on the live flotilla.
`ui.left` collapses; **`ui.back` does not** — measured silent on a dossier entry.
The card's own tooltip is CLASS-backed (`BattlePlayCard`), so it reads EMPTY in an unfocused
`/gui/graph?buffers=1` and only appears once the row is focused: "Increases long range defenses;
used to keep distance in battle" / "The enemy chose this in 100% of battles against you" (the
player's says "You chose this in 100% of battles overall, and 100% against the opposing empire").

**The phases are a flat list, not a grid** (2026-08-30, replacing the `GraphSheet`). Each sentence
the game writes already names its flotilla and its phase, so the reading is flotilla-major runs of
whole sentences and a flotilla name leads a run ONLY where more than one fought. Measured:
"The Flotillas 2 were at Long range during phase 1, Damage repartition: 1893 vs 0, 1 of 2" /
"… Medium range during phase 2, Damage repartition: 180 vs 0, 2 of 2" — **one utterance, two buffer
lines**, because the game's own `\n` here is punctuation, not wrapping (`Refresh` :62 writes the
statement and the tally as two lines, and each cloaking addendum is appended as a `\n\n` paragraph
:64-78). Every glued reading on this screen was split the same way on 2026-08-30 (`Prose`): the
phase stats, the two Totals, and the damage bars — "Damage caused by your Beam weapons: 968,
Including 215 critical" is now two buffer lines, not one line with a newline inside it.
`AdvancedReportPhaseItemContainer` holds three
`AdvancedReportPhaseItem`s; the unfought one is `Visible=True, Enable=False` with
`PhaseReport=null` and an invisible `FlotillaStatItemContainer`, so it contributes no line.
**The stat items are POOLED**: every phase holds three, and the ones for flotillas that never
fielded are `Visible=False` with `VisualFlotillaIndex=0` and an EMPTY tooltip — so both the
fought-flotilla count and the run anchor must test `AgeWidgets.Visible`, or a one-flotilla battle
reads as three. A crop of a stat item's rect (`717,511,146,72`) shows nothing: the phase panel is
painted in the perspective arena and the roster panel is drawn over the left of it, the same trap
the setup window's arena labels have. The sentences are tooltip text — probe them, don't crop them.

**A flotilla is drawn TWICE here too, and the two sides resolve differently** (measured
2026-08-30). `PlayerFlotillaCard2DContainer` and `EnemyFlotillaCard2DContainer` are fields on the
window (no need for the setup screen's walk through `EncounterPlayScreen3D`); each holds three
`EncounterPlayFlotillaCard2D`, of which only the live flotilla's is `Visible`. But only the PLAYER's
roster panel draws flotilla lines — `w.PlayerBattleGroupReportPanel` holds one
`BattleFlotillasPanel` and, with the toggle out, three `FlotillaLine`s numbered 1/2/3;
`EnemyBattleGroupReportPanel` holds ZERO (it is an `EnemyBattleGroupReportPanel`, a garrison). So:
- the player's cards go in through `BattleRosters.FlotillaExtras.Tooltip`, matched to the line by
  the NUMBER the line DRAWS (`FlotillaIndexLabel`, 1-based) against `Card2D.Index` (0-based), never
  by child order. Measured with the Your-fleets toggle on: "Flotilla 2, group, collapsed, This
  flotilla is optimal at Long range and has 95% compatibility with the ships", while Flotilla 1 and
  3 stay "Flotilla 1, Empty" — their cards are `Visible=False` and hand in nothing.
- the enemy's card has no line to hang on and is read in the THEIRS heading region instead:
  "Flotilla 2, This flotilla is optimal at Medium range and has 95% compatibility with the ships,
  8 of 8", tooltip drawn at `1569,716,342,43` (evidence crop taken there).
This asymmetry — the player's sentence only while the roster is out, the enemy's always — is
measured, not designed; flag it if the reading should be symmetric.

**A damage bar can say which TACTIC moved it, and that sentence is a tooltip FEATURE.** `GuiDamageData`
is an `IAffectingPlaysProvider` and computes, from the fought plans' modifiers (:99-152), which plays
touched the properties behind that bar; the `DamageGaugeCell` tooltip class's own panel definition
lists four features and the fourth is `PanelFeatureAffectedByPlay` (read off the live
`GuiTooltipWindow.TooltipDescription.PanelFeaturesDescriptions`, 2026-08-30:
`PanelFeatureHeader`, `PanelFeatureDescriptionGameplay`, `PanelFeatureSeparator`,
`PanelFeatureAffectedByPlay`). The feature hides itself on an empty list, so it never showed here —
but the mod's `Title`/`Description`-only reading WOULD have dropped it on any battle where a plan
modified damage. It is appended to the bar's buffer section in the game's own words:
`%PanelFeatureAffectedByOnePlayDescription` = 'The Tactic "{0}" affected this value' and
`%PanelFeatureAffectedByTwoPlaysDescription` = 'The Tactics "{0}" and "{1}" affected this value',
chosen by the same rule the feature uses (one name, else the first two). **No mod phrase.**
*Correction to the "every cell is empty" reading*: measured on this fixture, the two ABSORBED cells
carry one play each on both gauges — player `DamageAppliedAbsorbedByHullPlating` / `…ByShield` →
"Hard Target", enemy `…ByShield` → "Power to Shields" — they are simply `Visible=false` because
nothing was absorbed. **Fixture-blocked**: needs a battle with absorbed damage, or a plan that
modifies an offensive damage property.

**The missed-shot band's PROPORTION is spoken; its totals are not.** The game writes the count on
the band's tooltip ("Missed Shots: 2" / the enemy gauge's "Evaded Shots: 9") and draws the share as
the band's height, `MainContentTable.Height * (1 - hitRatio)` with no figure anywhere
(`DamageGauge.RefreshMissedDamage` :229-243), so the row adds `battle.shots-missed` =
"{0}% of shots missed" and no total — the same call `BalanceText` makes. `hitRatio` is
`totalHitSent / totalShotSent` over the group's flotillas PLUS its citadels (`Refresh` :80-86,
:106-108); the gauge keeps all three in private fields, so the mod re-derives them from
`DamageGauge.GetFlotillasPropertyValue(group, SimulationProperties.Flotilla.TotalShotSent |
TotalHitSent)` plus the citadel loop. The node is `Nodes.Drawn` on `MissedDamageGroup`, which the
game hides whenever the Show Missed Shots switch is off or nothing missed, so the gate is what
governs the phrase. Measured 2026-08-30 (reflection read-back vs the drawn band): player
`totalShotSent 17 / totalHitSent 15 / hitRatio 0.8824`, band `101/861 = 0.1173` → "Missed Shots: 2,
12% of shots missed"; enemy `21 / 12 / 0.5714`, band `61/142 = 0.4296` → "Evaded Shots: 9, 43% of
shots missed". The band-height share and `1 - hitRatio` agree to the layout's own rounding, and
`shots - hits` equals the count the game wrote (2 and 9). **Negative control, measured the same
day**: with Show Missed Shots unticked, BOTH missed rows leave the damage stop entirely — count and
percentage together — because the game hides `MissedDamageGroup` and the gate drops the node.

**The rewards ride on the player's ROSTER, behind the Your-fleets switch.** The window's field is
typed `BattleGroupReportPanel` (`AdvancedEncounterReportModalWindow` :21) but the bound instance is
the `PlayerBattleGroupReportPanel` subclass (verified live 2026-08-30), which owns `RewardsTable` +
`ResourcesEarnedTitleLabel` / `SalvageRescuedTitleLabel` / `TotalExperienceTitleLabel`. The screen
reads them with the SAME helper the simple report popup uses — `BattleNotifications.Rewards`, now
internal and prefixed — so the two windows say the same thing. Measured with Your fleets on:
"**Experience gained: 5, The total experience gained by your Ships and Hero. Ships gain bonus health
every level.**" at `battle-advanced/yours/experience`, last row of the yours region. The resources
and salvage labels exist but are `Visible=False` this battle (they still hold their raw prefab keys,
`%NotificationBattleReport…Title`) and appear in `Ghosts().droppedByGate` — **fixture-blocked**.
The enemy panel is the `EnemyBattleGroupReportPanel` subclass and has no rewards table at all, which
is why only the player side is asked.

**Fighter/bomber squadron counts** (`battle-advanced/your-squadrons` / `their-squadrons`, in each
side's HEADING region). `EncounterFighterBomberCard2D` counts operational and destroyed fighters and
bombers in report mode (:76-92, the `useSetup=false` branch), and those numbers are the only place
in the report a squadron is counted — the rosters list ships. **The card is NOT caption-less**: each
of its four chips is an Icon + a Label under a group carrying the game's own content-backed tooltip
(measured 2026-08-30) —
`%EncounterFighterBomberFighterDescription` "Number of operational Fighter units.",
`…DeadFighterDescription` "Number of destroyed Fighter units.",
`…BomberDescription` "Number of operational Bomber units.",
`…DeadBomberDescription` "Number of destroyed Bomber units." — so the row is named by the game's
sentence (label fallback) with the drawn number as its value, and **no mod phrase was added**.
A chip whose count is zero is hidden and a card with all four hidden hides itself (`RefreshValues`).
*Container correction*: on the REPORT, **both** sides are `EncounterPlayFlotillaCardContainer` with
three cards each (`FighterBomber2DCardLeftGroup` / `…RightGroup`), not a fleet container on the
enemy side as on the setup window — so the flotilla-number lead applies to both. Aim at the chip's
GROUP, never the label: the tooltip is on the group. **Fixture-blocked**: all six cards read
`Visible=false` on this pirate battle (no carriers), so only the absence is verified — the screen
declares nothing here and `Ghosts()` stays clean.

**Toggling a roster from a script**: `ui.next` to `:controls`, `ui.home`, `ui.activate` (the mod's
own checkbox; answers "checked"/"not checked"). Toggle it back — the panel slides OVER the phase
grid, so the phases stop's own content changes with it.

`TooltipParity()` on this screen: `clean:true`, 50 nodes, all eleven buckets empty — but `root` is
`null`, so that is the DECLARATION half only, not a painted-side pass. `Ghosts()`:
`shippedUnpainted:0`; `droppedByGate:24`, all accounted for (the two hero portraits the window is
not drawing, the zero-height damage cells for weapon types that never fired, the two hidden reward
labels, and the missed-shot rows whenever that switch is off). `Coverage()` is run
under a modal, so 16 of its 17 roots are the HUD behind it — subtract by path first. What is left
inside `AdvancedEncounterReportModalWindow` is ten `unread` entries, all the COLLAPSED-branch blind
spot: the family badge and three range indicators on each plan card, plus the two `RoleIcon`s under
the collapsed flotilla group. Expand before believing them.

**Never activate** anything but the three checkboxes here: Back closes the window (recoverable via
the popup's `battle-report/advanced`), and the popup behind it owns Rewatch and Replay.

## The space battle itself (`screen.battle`)

The cinematic is a narrated stream, so almost everything it says is unverifiable without a battle
actually running. The one part that IS reachable from a script is the PRE-ROLL: the game loads the
battle and then soft-locks waiting for a raw keypress, and it will sit there indefinitely. **A game
parked at the pre-roll is a free fixture** — `POST /reload` re-enters the screen with the loading
window already drawn, which is the same case a player meets when they arrive mid-load.

**Wedge probe handles** (all measured 2026-08-29, r48, owner's Sabel-vs-pirates battle):
`w = GetWindow<BattleLoadingWindow>(false)` — `w.Shown`, `w.Caption.Text` ("Press space or click to
launch the battle"), `w.BattleTitle.Text` ("Battle at Sabel"),
`w.Right/LeftBattleGroupInfoPanel.MainLeaderName.Text` ("Pirates" / "DefaultPlayerName (United
Empire)" — right is always the enemy), `w.GalaxyEncounter.State`
(`LoadingWaitForPlayer`), `.Encounter.CurrentPhaseIndex` (-1 here). The BattleScreen alongside it
reads `Shown=False`, `GalaxyEncounter=null` and PLACEHOLDER titles — never read those two labels off
an unshown window (`military.md`).

**Reading the battle report without running the battle.** `Encounter.PhaseReports` is public and by
the pre-roll holds the whole fight. Walk it with an `ArrayList` as a stack (`Instructions` and
`SubInstructions` are `List<GameType>` — index them through `System.Collections.IList`, never
`foreach`) and pick out `EncounterReportInstruction_EntityStatus`: that is how the premature-loss bug
was pinned to two specific flotillas without ever launching the fight.

**Pre-roll stop** `battle:pre-roll`, two read-only rows, no controls: `battle:title` ("Battle at
Sabel, Pirates") and `battle:launch` ("Press space or click to launch the battle"). Both are the
game's own drawn words; the screen declares nothing else while the loading window is up. Evidence
crops: title `18,24,1884,60`, caption `510,1116,900,54`, enemy name `1449,114,453,48`.
Measured after a reload onto the wedge: the ring is exactly "Battle at Sabel, Pirates" (screen name),
"Press space or click to launch the battle" (the caption watch), "Battle at Sabel, Pirates, 1 of 2"
(landing on row 1) — and then 3469 frames / 60 s of silence with no repeat and no loss lines. The
screen name and row 1 are the same words by design; the arrival therefore says them twice.

### Re-watching a battle from a script

**The report popup's Rewatch button replays the whole cinematic and changes no game state**, which
makes a fought battle a re-runnable fixture for everything the cinematic says. Route in from the
galaxy: `/input ui.focusNotifications` → `ui.down` to the "A Battle has ended against …" row →
`ui.activate` → `ui.end` (the Rewatch button is the last position, "5 of 5") → `ui.activate`.
`ui.down` from the report's first row descends INTO the fleet groups, so `ui.end` is the only cheap
way to the controls band. **Never activate `ReplayButton`** — the window has both, and that one
re-fights the battle.

The rewatch then parks at the **pre-roll gate**, which reads a raw keypress
(`GalaxyEncounter.Update` :2089-2101 asks `Input.GetKeyDown(Space)` directly). `POST /key` is the
honest way past it and refuses (409) unless the game holds the foreground — on a locked desktop
`SetForegroundWindow` reports handle 0 and it never will. The lever is the game's OWN test hook in
the same condition: `POST /eval GalaxyEncounter.DoNotWaitForPlayerInput = true` runs the identical
branch a Space press runs (it is what `TestAutomationCatalog` :193 uses). **Set it back to false
afterwards** — it is a public static and survives the battle. Setting it before the loading window
has written its labels skips the pre-roll rows, and the screen name falls back to the mod's own
"Space battle"; wait for the caption if those are what is under test.

The whole run is ~75 s of stream. Poll `/speech?since=N&wait=…` in a loop and allow for ~20 s of
silence between phases — a quiet-poll exit set shorter than that stops mid-battle.

### What the fight says (verified live 2026-08-30, r50, Sabel vs Pirates)

Measured whole-run transcript, in order: the pre-roll pair, then **"Battle at Sabel, Versus
Pirates"** (introduction) → **"Balance of power: 1st Conquerors Navy has 310% more military power
than 8th Greedy Pirates"** (main act) → **"Phase I"** → the exchange of fire → **"Phase II"** →
… → **"Battle 25 percent fought"** → **"Enemy Prowler is lost"** → **"Enemy Flotilla 2 is
destroyed"** → **"Phase V"** → **"Decisive Victory"**.

**The exchange of fire is read off the replay STREAM, not the model** (`ES2Access/UI/BattleStream.cs`
— one Harmony postfix on `GalaxyEncounter.ParseReportInstruction`, the recursion every instruction
and sub-instruction passes through in play order). Shots are gathered per attacker→target pair over
a 5 s window and spoken loudest-first, at most three lines a window (`SpaceBattleScreen`
`VolleySeconds` / `VolleyLines`). Measured cadence on this fixture: 1-3 lines every 5-7 s, e.g.
"Patrol hit Prowler: 108 energy damage, missed" / "Prowler hit Endeavor 3 times: 86 energy damage" /
"Prowler missed Endeavor 2 times" / "Endeavor hit Prowler: 188 projectile damage".

**Instruction inventory of this report** (1904 instructions, four phases): 38 `CreateSalvo`,
27 `Hit`, 39 `PrepareAttack`, 41 `Destruction`/`EntityStatus`, 101 `Event`, 1578 `UpdateProperty`,
3 `Spawn`, 2 `FlotillaSpawn`. Walk it offline from the report surface without replaying anything —
`NotificationBattleReport.Encounter.PhaseReports`, an `ArrayList` as the stack (`Instructions` and
`SubInstructions` are `List<GameType>`; index through `System.Collections.IList`, never `foreach`).

**A MISS is `CreateSalvo.Miss` and nothing else.** No `Attack_Miss` event is emitted and no `Hit`
follows (`BattleSimulationSalvo.HitTarget` :189-202), so hits and misses need no correlation: count
misses off the salvos, damage off the hits. Measured 38 salvos / 27 hits / `TotalLongShot` 37 vs
`TotalLongHit` 27 — consistent.

**Shield absorption, measured.** A shielded hit does NOT suppress the `Hit`, and no
`Attack_HitShield` event reaches the client stream at all. `Hit.TheoreticalDamages` is
post-mitigation (`SimulationProperties.Salvo.EffectiveDamage`), and what the shields ate is in the
hit's own SUB-instructions as `DamageReceivedAbsorbedByShield` `UpdateProperty` deltas on the
target. The same delta is written once per accounting level — measured twice per hit for
`DamageReceived…`, three times for `DamageApplied…` — so it is read as a **maximum, never a sum**. A
shot stopped dead is therefore a `Hit` whose damage is zero with an absorbed delta above it, which
is what "fully absorbed by shields" is keyed on. **On this fixture every absorbed delta is 0.0**, so
neither absorption line has ever been heard (roadmap).

**Damage type comes from the weapon module's own flags**, `WeaponTypeEnergy` /
`WeaponTypePhysical` (the pair `AdvancedEncounterPlayModalWindow.GetModulesPowerValuesByFleet`
:415-450 sums). Measured: `ModuleWeaponLaser1` and `ModuleWeaponBeam2` energy, `ModuleWeaponMissile1`
physical. There is **no game-localized bare type noun** to borrow: the `DamageAppliedBy…` GuiElements
localize only with a Player/Enemy suffix and only as whole gauge captions
("Damage caused by your Laser weapons: 123"), so the mod's own per-type templates carry the words.

**Ship names come from `GuiShip.GetTitle(ship, design)`** — the ship's user name if it has one, else
the design's localized name ("Patrol", "Endeavor", "Prowler"). The stream names PARTS (a shot comes
from a weapon module and lands on a section), so both ends are walked up the `Parent` chain to the
owning `EncounterShip`. **A citadel has no name anywhere in the game's strings** (`%CitadelTitle` and
its variants all localize to themselves), so citadel fire is DROPPED rather than given an invented
attacker — fixture-blocked here regardless, since this report holds no citadel salvo.

**The phase numbers skip, and that is the report's own doing.** Measured `PhaseReports` phase
indices on this battle: **0, 1, 2, 4** — there is no phase-3 record at all, so the narration says
"Phase I, Phase II, Phase III, Phase V" and is faithfully reading `Encounter.CurrentPhaseIndex`
through the game's own `%AdvancedReportModalWindowPhaseTitle`. Not a mod defect; do not "fix" it.

**Phase lines are held until the fight proper is on screen.** The encounter is already in phase one
while the introduction is playing, so an ungated watch says "Phase I" before the battle has said
where it is (measured before the gate: Phase I at 0 s, introduction at 1 s, balance at 6 s). The
gate is the display mode reaching Main or later; measured after: introduction 5 s, balance 10 s,
Phase I 10 s.

**An empty flotilla gets no destruction line.** Measured on this report, after the fight: the
player's group holds a flotilla with `Index=0`, `Ships.Length==0` and `Status=Destroyed` — the
game's own empty reinforcement slot — while the enemy's `Index=1` flotilla holds the one real ship.
The transcript carries "Enemy Flotilla 2 is destroyed" and NOTHING about the player's, which is the
evidence pair for the suppression: the model says destroyed, the narration is silent. Restore the
line (drop the `Ships.Length == 0` test) to see the false "Your Flotilla 1 is destroyed" come back.

**Accepted residue: one bare "unavailable" as the battle screen tears down.** Measured at the end of
every run. The cursor sits on `battle:camera/0` for the whole fight; its availability flips as the
screen closes, and the announcer speaks the changed part alone. The same mechanism is why the
controls stop reads "1 of 5" early and "1 of 4" later — the Skip button is drawn only while the game
will take a skip. Pre-existing, cosmetic, and outside the narration tiers.

### MANUAL TEST — the battle running (owner)

Fixture: the Sabel battle's report popup, Rewatch (above). Everything
below rides one uninterrupted run; the mod-authored lines are quoted from `english.json` with the
fixture's own numbers where known.

1. **Entering the battle** (loading window appears). Expect, in order: the screen-name line **"Battle
   at Sabel, Pirates"**, then the caption as the game writes it — **"Loading…"**, then **"Press space
   or click to launch the battle"** when the load finishes. Each caption is said ONCE however long it
   stays up. *Fails as*: "Battle at Antares, Versus DeltaPattern" (placeholder read), a caption said
   twice, or the launch caption never said at all.
2. **Visual check at the pre-roll**: the screen draws "BATTLE AT SABEL" across the top and "Press
   space or click to launch the battle" along the bottom — the same two lines just spoken, with the
   pirate skull at the top right and "Pirates" on the right-hand panel.
3. **Tab / arrows at the pre-roll**, before pressing anything: two rows, "Battle at Sabel, Pirates,
   1 of 2" and "Press space or click to launch the battle, 2 of 2", each re-readable in the buffer.
   Neither does anything when activated. *This is also the check that Tab is no longer dead here.*
4. **Press Space.** The pre-roll rows go with the loading window.
5. **Introduction act**: the game's own two lines off the battle screen, shaped
   "Battle at ⟨system⟩, Versus ⟨opponent⟩" — and the same pair is what the screen now answers with if
   re-announced later in the fight. *Fails as*: the mod's own "Space battle", or the Antares
   placeholder. **This act, and 6-9 below, are driven off the panels the screen shows, because
   `BattleScreen.CurrentMode` is never assigned (`military.md`) — before r48 none of them ever
   spoke, so treat any silence here as a regression report, not as a quiet battle.**
6. **Main act**: the balance line, "Balance of power: 1st Conquerors Navy has 310% more military
   power than 8th Greedy Pirates".
7. **Phases**: "Phase I", then "Phase II", … as the fight moves through them — the phase number and
   nothing else. *Fails as*: the balance sentence repeated on every one of them, or a phase said
   before the introduction. A number SKIPPED ("Phase III" then "Phase V") is correct on this
   fixture — the report itself has no phase-3 record.
7b. **The exchange of fire**, from the moment the shooting starts: a line every 5 s or so naming both
   ships, how many shots landed, and how much damage of which kind — "Patrol hit Prowler: 108 energy
   damage, missed", "Prowler hit Endeavor 3 times: 86 energy damage", "Endeavor hit Prowler: 188
   projectile damage", "Prowler missed Endeavor 2 times". *Fails as*: silence during a visible
   exchange, a GUID or a design name where a ship should be, more than three lines in one breath, or
   one line per shot.
8. **Losses**, as they happen and never before the fight starts: "Enemy ⟨ship⟩ is lost" / "N enemy
   ships lost", "Your ⟨ship⟩ is lost" / "N of your ships lost", and per flotilla "Enemy Flotilla 2 is
   destroyed" / "Your Flotilla 1 is destroyed". On this fixture the player has 310% more power, so
   expect the enemy's losses and few or none of your own. **The bug this replaces: those two flotilla
   lines used to arrive during LOADING, before Space was pressed — they were the report's ending
   being read early. Any destruction line heard before the fight is visibly under way is that bug
   back.**
9. **Progress**: "Battle 25 percent fought", then 50, 75 — upward only, at most once each.
10. **Outcome act**: the game's own outcome word (`OutcomeValue`, one of the nine `EndBattleStatus`
    words), spoken once.
11. **Watch it again** (the game's own re-watch): the whole sequence repeats from the top — the clock
    jumping backwards re-arms every watermark, so the losses and progress marks are news again.

## Ground battles

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

## Fixture-blocked

- A cross-flotilla move, the juggernaut SWAP and the three sorting presets on the ADVANCED
  window: the fixture has one valid flotilla, so every drop it can reach is a refusal and the
  sorting band reads `unavailable` (**ADVANCED battle setup**).
- The ground-battle OUTCOME-SELECTION popup's live content — it needs a decisive victory
  (**Ground battles**).
- On the ADVANCED battle REPORT, everything a MULTI-flotilla battle would show: the flotilla-name
  line that leads each run in the phase list (measured `fought == 1` on this fixture, so no name is
  emitted and the branch is unexercised), the enemy-side morale line "Their fleet had the morale
  bonus" (`MoraleBonus` reads 0 there — only the phrase's resolution is proven), and more than one
  enemy heading flotilla line. The
  percentage branch of the balance sentence is unexercised too — the enemy's military power is 0,
  so only `BalanceAllKey` has been heard here (**ADVANCED battle report**).
- On the ADVANCED battle REPORT, three more that this battle cannot draw: the **resources** and
  **salvage** reward lines (both labels `Visible=False`, only Experience is drawn); every
  **fighter/bomber squadron count** (all six cards `Visible=false` — needs a battle with carriers);
  and the **"The Tactic … affected this value"** sentence on a damage bar (the only cells whose
  `AffectingPlayNames` is non-empty are the two absorbed-damage cells, and nothing was absorbed —
  needs absorbed damage, or a plan modifying an offensive damage property)
  (**ADVANCED battle report**).
- Six of the cinematic's narration lines, because this report contains nothing that feeds them
  (**What the fight says**): reinforcements arriving mid-fight (`_Spawn` after time zero — all three
  spawns here are at 0.0), a ship repairing (a positive `Health` delta on a section — every Health
  delta here is negative or zero, and the one `Healing` event carries a null initiator and a
  per-phase recompute), a battle effect and a medal (no `_BattleEffect` and no `_Medal` instruction
  at all, and both are additionally gated on the game having a written title), both shield-absorption
  clauses (every absorbed delta is 0.0), and citadel fire (no citadel salvo, and no name to give one).
- The five battle-stack notification types are never swept by the family sweep, whose
  `Bind` throws without a live encounter (`notifications.md`, **Fixture-blocked**).

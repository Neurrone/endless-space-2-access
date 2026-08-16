# ES2 interaction language — layers, keys, claims

The mod's ES2-specific interaction design: the layer budget, the key map, and the claim
rules. Read when BUILDING a screen; the loop itself is `docs/dev-loop.md`, and the generic
doctrine behind these rules is `docs/generic/input.md` / `ui-navigation.md`. A new layer
number or key binding lands here (bindings themselves need owner approval first).

## Layer budget

Static per screen (doctrine: ui-navigation.md "Layers are static"). **A layer number is
allocated by the main agent when a stage is briefed, never claimed inside a stage** —
pipelined stages cannot see each other's claims, and three of them once picked the same
number independently.

`0` main menu, the new-game lobby, and the menu-replacing out-game pages (Credits/DLC/Mods/
Export/Join — registered after `MainMenuScreen`, and equal layers tie-break by registration
order; never up together, since showing one hides the others) ·
`5` advanced settings · `6` faction chooser (both over the lobby, their only opener) ·
`7` custom faction editor (over the chooser, whose own window hosts its panel; the three are
never up together, and all sit well under the drop list a setting can open and the message
box a Cancel or a Delete confirms in) ·
`10` the view levels — galaxy, star-system, planet-overview, system-discovery, plus the two
battle view levels (the space cinematic and the ground battle) — never up together ·
`11` scan view (over the view levels, under everything they raise; one number for every lens,
which are never up together) ·
`15` **every icon-strip screen, shared deliberately** — research, quest journal, senate,
empire, economy, military, academy, diplomacy — because the engine enforces the exclusivity
itself (`BackgroundRenderer` carries `GuiWindowsStackExclusive`, measured), so no two of them
can be shown at once ·
`18` notification (the engine's own ladder: above the screens, below every modal) ·
`20` planet-constructibles (the panel a planet card slides out under itself) ·
`21` troop management · `22` battle-tactics deck (both measured below the tutorial popup via
`AgeScreen.SortingOrder` — ModalRenderer 5 < OverlayRenderer 6) ·
`23`/`24` target pickers · `25` system-selection modal (over the star-system page that opens it
and under BOTH things it can raise itself: the tutorial page it registers a key for, and the
drop list its policy column opens) · `26` fleet-selection · `27` hero list ·
`28` hero-selection (under the hero-inspection window its own Inspect raises) ·
`29` juggernaut specialization ·
`33` government · `34` laws · `35` ship designer (under the hull drop list 70 and the
lose-changes box 100) ·
`36` election, population **and** recipe-creation — a deliberate three-way share: all sit on
the game's exclusive modal stack behind different openers, so no two can be up together, and
a window on such a stack voids any layer constraint against its stack-mates
(ui-navigation.md; owner rule 2026-08-12: mutually exclusive screens may share a number) ·
`41` negotiation · `42` advanced battle report · `43` minor-faction diplomacy · `44` pirate
diplomacy · `45` hero inspection (under rename 80 and the message box 100) · `46` the academy
pair (shared — the two DLC modals, never up together) · `47` cutscenes · `48` advanced battle
setup · `49` victory-achieved ·
`50` game-menu · `51` end-game journal · `52` options (one number, above the pause menu that
can open it) · `55` load-save · `60` loading · `70` drop-list (above options, its owner) ·
`80` rename box · `85` improvements modal (over the star-system page, under its own
confirmation) · `86` system-politics modal · `90` tutorial-selection modal (over the new game
screen) · `96` contextual prompt (over the scan view that raises it and the modal stack it draws last
on; under the non-blocking box) · `97` non-blocking box · `98` tutorial popup · `99` error box ·
`100` message-box.
Mod-owned CHILD screens (`Screen.PushChild`) have no layer: the manager focuses the deepest
child of the top screen.

**The tutorial popup sits at 98 — above everything except the two boxes that must be
answerable.** The game itself draws most tutorial popups over its own screens, modals and
notifications (`TutorialPopupLayer`, per page — es2-facts), so any lower number buries one of
them; what keeps 98 livable is that a collapsed popup stands down and that the mod follows the
panel's own visibility, so a popup the game has hidden holds nothing. The error box (99) and
the message box (100) go ABOVE it, because an error or a confirmation the tutorial buries is
unanswerable. A collapsed popup's bar is declared on the focused page EXACTLY while the game is
drawing it (`TutorialPopupPanel` shown + minimized — the game itself hides the panel for
UnderScreens pages under a cover, so "declared" and "drawn" are one condition), except on the
answer-only surfaces (`Screen.AnswersOnly`: the error box, the message box, the non-blocking box,
the drop list, the loading screen); the eleven HUD-edge pages keep their own placement
(`GraphBuilder.DeclaredStop` suppresses the shared append). Owner's third and final ruling on
this bar, 2026-08-12.

**The selected-fleet panel has NO layer** — it is a contributor to the galaxy page
(`FleetPanel` — `docs/helpers.md`), not a screen, because selecting a fleet changes only the cursor and the
map underneath stays live and has to stay walkable. A layer of its own put it OVER the galaxy
and took the systems, the starlanes and the HUD out of Tab. It is contributed by the galaxy page
alone, which is complete rather than a gap: entering a system swaps the cursor to
`StarSystemCursor` and the game hides the window outright (measured — es2-facts).

**ES2 key map, in one place** (defaults in `ModEntry.BindKeys`; the generic table is
`docs/generic/input.md`). On top of arrows/Tab/Enter/Backspace/Escape/Home/End, Alt+arrows and
the Ctrl review chords: **Shift+Left/Right** coarse slider step, **Alt+Enter** the control's other
activation (queue at the head), **Backslash** the control's right-click command
(`NodeVtable.OnContextual`), **Ctrl+Backslash** the game's Ctrl+right-click — the SAME `Contextual`
action bound as a second chord, never a wired variant, because the game runs one handler for both
clicks and reads the physical modifier inside it (on the map: a free-movement-only route,
`FleetOrders.RequestedFlags`), **Ctrl+Alt+Enter** the control's DOUBLE click (`OnDoubleClick`),
**Space** pick up / swap / put back what is being dragged (`OnPickUp`),
**Enter** drop it where it will be taken (`DropKind` + `OnDrop`), **Ctrl+Enter** one item into or out
of the game's own selection (`OnSelectToggle`), **Shift+Enter** extend that selection to here
(`OnSelectRange`). There is NO reorder chord: moving an item within its list is a drag like any other.
**Each of those keys means the game's own gesture and nothing else** — Backslash is the right click,
Alt+Enter the Alt-click, Ctrl+Enter the Ctrl-click, Ctrl+Alt+Enter the second click. The three
modified LEFT clicks (Alt+Enter, Ctrl+Enter, Shift+Enter) **fall back to the control's plain click**
where the screen wires no handler of their own (`KeyGraph.ModifiedClick`): the player is physically
holding the modifier, so replaying the click is what lets the GAME's handler branch on it —
Ctrl+click to locate a technology, Alt+click to queue at the head — with no per-screen wiring, and a
handler that ignores modifiers just does its ordinary thing, exactly as a modified mouse click would.
A wired slot stays an OVERRIDE, for the controls where the game runs a genuinely different handler.
Backslash and Ctrl+Alt+Enter keep absent-means-silent: a right click or a second click that does not
exist has nothing to replay. On a `GuiTable` row, the second click is wired by `TableSheet` for EVERY
table and every cell of a row (its `DoubleClickButton`, the row selected first because the game's
handlers all read `GuiTable.SelectedLine`), so the empire page's systems table opens that system's
management page, the military page shows the fleet on the map, and the two selection modals pick and
close — no screen declares any of it. The tables whose client does nothing with the gesture stay
silent, as the mouse does there. The save list's second click ACTS rather than shows — it loads the
row (behind the same confirmation the Load button raises in game) or saves over it (behind
`%LoadSaveConfirmOverwriteDescription`) — and it is carried like every other table's: the game's own
confirmation is the guard on chord and mouse alike (owner ruling 2026-08-14). A selectable table row
speaks its selection state with NO role word — the row's name plus "selected"/"not selected" is the
whole affordance, and a radio word on every row of an announced table is noise (same ruling).
Where the game has left a control switched on only so a click can
explain itself, Ctrl+Enter is that explanation: the jump to the missing technology (`Cells.Add` →
`AgeWidgets.Locate`), wired once for every such control; those controls still announce themselves
unavailable, and Enter on them does nothing, as the mouse's plain click does. (Owner rulings
2026-08-13.) The double-click chord is free because no handler in the game combines Ctrl and Alt with
a click and its own binding matcher is exact-modifier (`InputManager.InputsMatch`); a mod screen
replaying a double click checks that the game's handler does not read the modifiers the player is
still holding.
**Backspace (`NodeVtable.OnSecondary`) is NOT a right click** — it is the second command on a node
that folded two of the GAME's controls into one, and the only one left is the options screen's
key-rebind row, where Enter captures the binding's first key and Backspace its second. Anything the
game itself puts on a right click goes on Backslash, never here: the HUD's notification dismiss was
on Backspace until 2026-08-13 and moved, because the game dismisses on right click
(`NotificationItemsWindow.HandleInput` :90-101).
The key is offered to the SCREEN first (`Screen.Secondary`, taking the focused node — mirroring
`Screen.Contextual`), for a second command that belongs to a PANEL rather than to a control: the
galaxy's way back down the starlanes it has travelled is about where the player has BEEN, and wiring
it per node would mean wiring it onto every node that panel will ever declare. A screen answers only
inside the stop it means it in and leaves every other panel's Backspace exactly as it was. And a LIVE
type-ahead search takes the key ahead of both (below).
The Enter chords pass the PHYSICAL modifier through to the game's handler, which
is how the game's own selection rules apply rather than a copy of them. Which screens have the
chords and which cargo kinds the drag carries (ships, population, both queues) is coverage
status — `docs/test-recipes.md`'s per-screen paragraphs own it; a drop always puts the carried
item at the target's own position ("Moved ⟨name⟩ to position ⟨n⟩").

**Ctrl+Tab is the GAME's chat key, not a mod binding**: at startup `GameChatKey` moves
`StartChatting` off Enter/Tab to Ctrl+Tab through the game's own options (ONLY while it still has
the shipped default; a customised binding is left alone), and whatever chord the binding sits on is
handed back through the stand-down (`ModInput.LeaveToGame`) — so re-binding chat in the game's
options keeps working. **Open chat is a PLACE, not a stop**: the key opens the mod's child screen
over the page (`ChatScreen`, in every session including single player — `docs/helpers.md`) with the
cursor ON the box's node, NOT typing in it — entering the box is Enter on that node, the edit-field
idiom everywhere else (owner ruling 2026-08-14; the game's own answer to the key, keyboard straight
into the box, is intercepted in the panel's input handler — a mouse click or the new-message button
still types at once, since a pointer asked for the box itself). While the panel is up Tab cycles
inside chat and the page underneath is unreachable, exactly as the panel behaves for a mouse; Up from
the box walks the log newest-first. **While typing, the first Escape steps OUT of the box** into that
page — keyboard back, panel still drawn, cursor on the box's own node — and **Escape from the page
closes**, handing the player back to the control they left (the covered page keeps its own cursor). The panel is drawn
only while somebody is typing in it or a pointer rests on it, so the mod holds it open for as long as
its page is up (`ChatHold`) — which is the one thing that lets chat follow the drawing like every
other surface; declared on the window's existence instead, its controls sat in every page's Tab ring
with nothing on screen (owner-reported, 2026-08-14). The one part left on the pages is the
new-message button, because that is the one part the game draws while chat is closed. Escape on the
chat page is an ordinary `ConsumesBack` claim — a mod-owned child screen the game cannot close — but
the first Escape, the step out, is taken from INSIDE the game's own dispatch (`ChatEscape` prefixes
`InGameChatPanel.HandleInput`), because while the box holds the keyboard the whole mod layer is stood
down and the key never reaches it.

**Enter is click parity everywhere.** Every node's Enter is the click the game itself puts on that
control, including the destructive ones — a research queue item dequeues, a construction queue line
cancels (instantly while nothing is invested, behind the GAME's own confirmation box once something
is). There are no mod-invented action menus left; a control's extra buttons are child nodes opened
with right. The one thing that displaces a node's click is a live drag landing on a control that
takes the cargo, which is what makes Enter the drop key — and the other is a targeting cursor: while
the game is waiting for a target, Enter on a map node is that confirm and the node's own click waits,
exactly as the mouse's does — and the fleet nodes select nothing, because a click cannot. Backslash
follows the same rule: while a mode is armed it is the map's own right click for that mode and
nothing else (a cancel for seven of the nine cursors, one waypoint back while a hacking operation is
plotted, the prompt closed for the program picker) — the SCREEN takes the key before the focused
node (`Screen.Contextual`, a new hook: the screen is offered the contextual key first, for a mode
the game imposed on the whole page), so it works anywhere and sending fleets / undoing a zoom wait
for as long as the mode is up. Escape stays the game's except under `TakeSystemCursor`, the one mode
the game left with no Escape route: there the mod claims Escape (`ConsumesBack` true only then) and
runs that cursor's own right-click cancel, so Escape cannot raise the pause menu over a map still
waiting for a target (owner-ruled deviation, 2026-08-12).

**Expanding a galaxy system node (Right) also brings the camera in** through the game's own zoom
(`GalaxyViewLevels.ZoomTo`); Enter is unchanged, Backslash remains the way out, and **collapse
un-zooms** while `GalaxyViewLevels.FocusedSystem` is still that system — a camera the player has
since moved elsewhere is left alone, so collapsing moves nothing there. **A starlane is a LEAF and
Right on a named one TRAVELS** (`NodeVtable.OnFollow` → `KeyGraph.TreeMove.Followed`, consumed
silently): the cursor lands on the destination system's ONE node at the root of the systems stop,
that branch opens, and the camera goes there through `ZoomTo` — never `ZoomIn`, because travelling is
not a click and must not confirm an armed targeting mode (measured: the mode survives Right and is
still ended by Enter on the same lane). The landing's ordinary announcement is the whole
announcement. **Backspace pops the trail** while focus is in `galaxy:systems`: back to the exact lane
node under the origin (the origin re-expanded so that node exists), camera back to the origin, again
no words; a hop whose origin or destination is no longer perceived is skipped, and an empty trail is
consumed and silent. A system opened BY travel is **collapsed on the way out** (another hop or a pop)
and one the player opened is left alone — and neither runs the collapse's own un-zoom, since travel
scripts the camera itself. The trail survives an excursion to another screen (the page keeps its
state on pop) and dies with the game instance. A lane into the dark is a silent leaf under Right.
Fleets on a LANE hang under BOTH end systems, after the parked ones, each saying which lane and which
bearing off the same lane list the lane nodes number themselves from; a fleet crossing OPEN SPACE
hangs under its DESTINATION only — the map draws where a fleet is going and never where it came from
(es2-facts) — and one whose destination is unperceived gets a top-level row instead, walked into the
system list by its own pair. **The systems stop is ONE region, not two**: colonies are not split off
from the rest (owner ruling 2026-08-16), so Alt+Up/Down on this stop jumps only between the stars and
what is drifting out between them, and declares nothing at all while there is nothing drifting. **Zoom is an adjustable node**
on the existing
Left/Right + Shift chords (no new binding), and it lives on BOTH the scan view and the galaxy's
`hud:view-title` stop, in a row of its own beside the name of what the player is looking at; its
coarse step is a LAYER-BAND jump rather than ≈10 increments — an owner-approved deviation, since
ten of the camera's thirteen steps would be the whole range.

**Ctrl+I is the galaxy's INSPECT MODE** — a square of galaxy the player moves about the map and hears
the contents of, instead of walking the tree (`GalaxyInspect` — `docs/helpers.md`). Ctrl+I is free in
this game (`InputManager` binds nothing at all to I) and is bound outright, so plain I is suppressed
from the game wherever a mod screen is focused, which costs nothing. While the mode is LIVE it takes
its keys at MODE level, ahead of the review chords and of navigation (`Screen.AnyKey` — the same hook
the cutscene uses, and the same displacement the map already lives with under an armed targeting
cursor): **arrows** move the cell by exactly its own size, **Enter** lands on the one thing in the
cell (silent for none or several), **Escape** leaves ("Exited inspect mode"), **`+`/`-`** grow and
shrink it through 1/3/5/7/9/11 units. Every other key falls through untouched. **Ctrl+I only ARMS
the mode — it is not a toggle**: pressed again while the cursor is up it is taken and does nothing,
silently, on the same ruling as Enter on an empty cell (the key is pressed speculatively mid-sweep,
and dropping the cursor there would cost the player the cell they were standing on). The three ways
out are Escape, a landing Enter made, and the map going away. **A MODE OF A WIDGET, NOT OF THE
SCREEN (2026-08-17).** Every sentence above holds while the tree cursor is on the MAP stop
(`GalaxyHudScreen.IsMapStop`); off it the mode is SUSPENDED, not ended — Tab and Shift+Tab walk the
screen's other stops as usual and the stop landed on gets every key exactly as if no cursor were
armed. This is not a nicety: the mod's own zoom slider is two stops above the map and lives on the
arrows, so a mode claiming arrows screen-wide made it unusable. The cell, its size and its drawn
square are all kept while suspended, and coming back to the map reads the cell out again (after a
short wait, so the arriving stop's own announcement is not cut off). ARMING obeys the same rule: Ctrl+I
pressed from another stop is NOT claimed and does nothing at all — no focus move, no arming, no speech
(a jump to the map stop was tried and vetoed, 2026-08-17). Escape and the size
keys are claimed from the game ONLY while the mode is live AND on the map stop
(`GalaxyInspect.KeysClaimed` → `Active` through
`InputAction.ClaimedWhile`, the Space precedent), which is what leaves the game its own KeypadMinus
(`SleepForThisTurn`) and the battle screens their `EncounterSpeedUp`/`EncounterSlowDown` everywhere
else. `+` is THREE chords — bare Equals, Shift+Equals and KeypadPlus (plus `KeyCode.Plus`) — because
the matcher is exact-modifier and "+" is Shift and the equals key on most layouts. The mode cannot
outlive the page: anything that takes the player off the map ends it, and the line saying so is
spoken from the pump once the arriving page's announcement burst has gone quiet.

**The galaxy's SCANNER is on the Page keys with a modifier, and it is NOT a mode** — no arm key,
nothing to exit, Escape never touches it; the chords are live for as long as the map is the focused
page, alongside tree navigation and alongside the inspect cursor (`GalaxyScanner` —
`docs/helpers.md`). **Ctrl+PageUp/Down** cycles the category (systems ↔ fleets, skipping one with
nothing in it), **Shift+PageUp/Down** the subcategory within it (all / friendly / neutral / enemy,
skipping empties), **Alt+PageUp/Down** steps one thing at a time and wraps at both ends (the only
repeating chord of the three), and **Alt+Home** goes to what it is pointing at — the inspect cell
onto the thing's ROUNDED spoken pair while that mode is up, otherwise the tree cursor onto the
thing's own node. The FIRST scanner press of a game says where the cursor already is instead of
moving it. **Bare PageUp/PageDown remain the GAME's keyboard zoom**, Ctrl+Home stays the review
buffer's first line and plain Home/End the stop's ends.
The claim is what makes that work and it is a NEW SHAPE: the three chords are claimed only while the
galaxy page is focused AND a modifier is PHYSICALLY held (`GalaxyScanner.KeysClaimed` through
`InputAction.ClaimedWhile`). The camera's own matcher reads its binding's key codes and ignores its
modifiers (`GalaxyViewCameraController.IsInputKeyCombinationPressed`), so a plain claim on PageUp
would take the bare press too — and `ModInput.LeaveToGame` cannot help, because the modifiers the
stand-down reads come off the combination the GAME is asking about, which declares none either way.
The physical modifier is the only thing that separates the two presses. `DevProbe.Chord` cannot
prove this half (it holds no key); what it proves is the important half — with no modifier held,
`Claims("PageUp")` reads `claims:false` on the galaxy page.

**Tab and Shift+Tab wrap** (owner decision 2026-08-12): the last stop's Tab lands on the first,
the first stop's Shift+Tab on the last. On a page with exactly ONE stop the key is consumed and
says nothing — coming round to the panel the player is already on is not a move. **A screen where
every key means one thing** answers `Screen.AnyKey` before the review chords and before
navigation: the cutscene's arrows, Tab, Enter and review chords all become the game's own
press-anything skip, because a claimed key is invisible to the game's binding matcher and would
otherwise do nothing at all. Escape is never offered to it and stays the game's.

Backslash and every Enter chord are claimed on every mod screen. **Backslash and Ctrl+Alt+Enter are
SILENT where the control has no such command** — pressed speculatively all over a page, a cue on
each is noise; silent but consumed, never a fall back. Alt+Enter, Ctrl+Enter and Shift+Enter DO fall
back to the control's plain click (above), and are silent-and-consumed only where the control has no
click either. Space while something is carried is the same: consumed on a control that will not take
it, silent, carry kept.
**Space is claimed only where it can act** — the focused control has something to pick up,
something is already being carried, or a live type-ahead search is taking the space as text
(`ModEntry.CarryKeyClaimed` → `GraphNavigator.TakesCarryKey`, through `InputAction.ClaimedWhile`;
owner decision 2026-08-12, reversing the blanket claim of 2026-08-11). Everywhere else it falls
through to the game, whose Space is the strategic lens (`ToggleScanView`) — modelled now by
`ScanViewScreen`, which announces the lens on arrival and the view again on the way out, so
handing the key back cannot drop the player into an unannounced mode. Every other binding is
claimed outright. While something is carried, **Escape puts it
down and goes no further** (`claimsBack` reads true only then), and the carry dies silently when the
player leaves the page it started on — a menu opened over that page is still that page.

**Typing a letter searches the focused stop** (no search key: the first printable character starts
one; Up/Down step the matches, Home/End their ends, Escape clears it and goes no further, **Backspace
does exactly what Escape does** — ends the search, "Search cleared", and goes no further, so it never
also runs the page's own second command on the match it landed on; any other action ends it and then
does its own job). Backspace is the way OUT of a search rather than an editor for it: a search is
re-typed in a keystroke, and one gesture is worth more than character editing (owner decision
2026-08-14). So **A–Z are claimed from the game on every mod screen**
(`GraphNavigator.TakesTypedKey` via `ModInput.ClaimsTypedKey`, asked before the press), and a
space typed into a LIVE search is text — the carry key takes it for the search (a live search
is one of the three conditions of Space's claim, above). Screens opt out with `AllowsTypeahead`
(the key-rebind capture rows, and the cutscene where letters belong to the game's skip) or
`CapturesRawInput` (the frames between asking for a key capture / text editor and the
game taking the keyboard). Edit fields are entered explicitly: Enter on the field hands the
keyboard over, and Escape steps back OUT of editing before a second Escape closes the surface —
both halves the engine's own gestures (the rename box is the worked example).

**Escape is the game's, except over a surface the mod invented.** A screen answers
`ConsumesBack` (asked BEFORE the press), and `ModInput` latches EVERY consumed key until the
player lets go — the rationale for both is `docs/generic/input.md` (the back-key rules and
the liveness self-race law). `ConsumesBack` is NOT a copy of `Back()`: `DropListScreen`
handles Escape and still needs the engine to see it. Probe live with
`ES2Access.Dev.DevProbe.Claims("Escape")` — `claims` true only where a mod-owned surface is
focused, the latch shown when the surface has already gone. That probe, not
`/input ui.back`, is what proves the key does not fall through. It cannot tell a MODIFIED binding
from its plain one (it is asked per `KeyCode`), so a removed chord is proved by `POST /input` with
the action key instead: an unregistered action 400s and lists the ones that exist.

Game-mechanism findings (window gates, pool slots, tooltip internals, fleet and quest
mechanics, the icon numbers) live in [es2-facts.md](es2-facts.md) — a new fact lands there,
never here.

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
(`NodeVtable.OnContextual`), **Ctrl+Alt+Enter** the control's DOUBLE click (`OnDoubleClick`),
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
silent, as the mouse does there. Where the game has left a control switched on only so a click can
explain itself, Ctrl+Enter is that explanation: the jump to the missing technology (`Cells.Add` →
`AgeWidgets.Locate`), wired once for every such control; those controls still announce themselves
unavailable, and Enter on them does nothing, as the mouse's plain click does. (Owner rulings
2026-08-13.) The double-click chord is free because no handler in the game combines Ctrl and Alt with
a click and its own binding matcher is exact-modifier (`InputManager.InputsMatch`); a mod screen
replaying a double click checks that the game's handler does not read the modifiers the player is
still holding.
The Enter chords pass the PHYSICAL modifier through to the game's handler, which
is how the game's own selection rules apply rather than a copy of them. Which screens have the
chords and which cargo kinds the drag carries (ships, population, both queues) is coverage
status — `docs/test-recipes.md`'s per-screen paragraphs own it; a drop always puts the carried
item at the target's own position ("Moved ⟨name⟩ to position ⟨n⟩").

**Ctrl+Tab is the GAME's chat key, not a mod binding**: at startup `GameChatKey` moves
`StartChatting` off Enter/Tab to Ctrl+Tab through the game's own options (ONLY while it still has
the shipped default; a customised binding is left alone), and whatever chord the binding sits on is
handed back through the stand-down (`ModInput.LeaveToGame`) — so re-binding chat in the game's
options keeps working.

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
since moved elsewhere is left alone, so collapsing moves nothing there. A **lane destination OPENS
like the system it names**, minus its own lanes: its children are keyed STRUCTURALLY under the lane
(the reference tier dropped, not the level refused), which is how the one-object-one-node rule is
kept while the tier below stays reachable. **Zoom is an adjustable node** on the existing
Left/Right + Shift chords (no new binding), and it lives on BOTH the scan view and the galaxy's
`hud:view-title` stop, in a row of its own beside the name of what the player is looking at; its
coarse step is a LAYER-BAND jump rather than ≈10 increments — an owner-approved deviation, since
ten of the camera's thirteen steps would be the whole range.

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
one; Up/Down step the matches, Home/End their ends, Escape clears it and goes no further, any other
action ends it and then does its own job). So **A–Z are claimed from the game on every mod screen**
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

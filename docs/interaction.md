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
screen) · `96` free · `97` non-blocking box · `98` tutorial popup · `99` error box ·
`100` message-box.
Mod-owned CHILD screens (`Screen.PushChild`) have no layer: the manager focuses the deepest
child of the top screen.

**The tutorial popup sits at 98 — above everything except the two boxes that must be
answerable.** The game itself draws most tutorial popups over its own screens, modals and
notifications (`TutorialPopupLayer`, per page — es2-facts), so any lower number buries one of
them; what keeps 98 livable is that a collapsed popup stands down and that the mod follows the
panel's own visibility, so a popup the game has hidden holds nothing. The error box (99) and
the message box (100) go ABOVE it, because an error or a confirmation the tutorial buries is
unanswerable. A collapsed popup's bar is declared on whatever page took the keyboard back —
every page, not only the ones that read it in their own place (`Screen.BuildShared`).

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
Alt+Enter the Alt-click, Ctrl+Enter the Ctrl-click, Ctrl+Alt+Enter the second click — and a screen
whose control lacks that gesture leaves the key silent rather than lending it to another one. The
double-click chord is free because no handler in the game combines Ctrl and Alt with a click and its
own binding matcher is exact-modifier (`InputManager.InputsMatch`); a mod screen replaying a double
click checks that the game's handler does not read the modifiers the player is still holding.
The Enter chords pass the PHYSICAL modifier through to the game's handler, which
is how the game's own selection rules apply rather than a copy of them. Which screens have the
chords and which cargo kinds the drag carries (ships, population, both queues) is coverage
status — `docs/test-recipes.md`'s per-screen paragraphs own it; a drop always puts the carried
item at the target's own position ("Moved ⟨name⟩ to position ⟨n⟩").

**Enter is click parity everywhere.** Every node's Enter is the click the game itself puts on that
control, including the destructive ones — a research queue item dequeues, a construction queue line
cancels (instantly while nothing is invested, behind the GAME's own confirmation box once something
is). There are no mod-invented action menus left; a control's extra buttons are child nodes opened
with right. The one thing that displaces a node's click is a live drag landing on a control that
takes the cargo, which is what makes Enter the drop key.

**Tab and Shift+Tab wrap** (owner decision 2026-08-12): the last stop's Tab lands on the first,
the first stop's Shift+Tab on the last. On a page with exactly ONE stop the key is consumed and
says nothing — coming round to the panel the player is already on is not a move. **A screen where
every key means one thing** answers `Screen.AnyKey` before the review chords and before
navigation: the cutscene's arrows, Tab, Enter and review chords all become the game's own
press-anything skip, because a claimed key is invisible to the game's binding matcher and would
otherwise do nothing at all. Escape is never offered to it and stays the game's.

Backslash and every Enter chord are claimed on every mod screen and are **SILENT where
the control has no such command** — they are pressed speculatively all over a page, and a cue on
every one of them is noise. Silent but still consumed, and never a fall back to plain activation.
Space while something is carried is the same: consumed on a control that will not take it, silent,
carry kept.
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

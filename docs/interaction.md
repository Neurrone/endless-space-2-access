# ES2 interaction language — layers, keys, claims

The mod's ES2-specific interaction design: the layer budget, the key map, and the claim
rules. Read when BUILDING a screen; the loop itself is `docs/dev-loop.md`, and the generic
doctrine behind these rules is `docs/generic/input.md` / `ui-navigation.md`. A new layer
number or key binding lands here (bindings themselves need owner approval first).

## Layer budget

Static per screen (doctrine: ui-navigation.md "Layers are static"):
`0` main-menu and the new-game lobby (never up together — showing one hides the other) ·
`5` advanced settings · `6` faction chooser (both over the lobby, their only opener) ·
`7` custom faction editor (over the chooser, whose own window hosts its panel; the three are
never up together, and all sit well under the drop list a setting can open and the message
box a Cancel or a Delete confirms in) ·
`10` galaxy, star-system, planet-overview and system-discovery (the four
view levels, never up together) · `20` planet-constructibles (the panel a planet card slides
out under itself) · `25` system-selection modal (over the star-system page that opens it and
under BOTH things it can raise itself: the tutorial page it registers a key for, and the
drop list its policy column opens) ·
`15` research (the technology wheel — a GuiScreen overlay drawn over whichever view level is
underneath, so above them and below the planet panel) · `16` quest journal (the other GuiScreen
overlay; the same strip of screen icons opens both, so the two are never up together) ·
`18` notification (the engine's own ladder: above the screens, below every modal) ·
`50` game-menu · `52` options (one number, above the pause menu that can
open it) · `55` load-save · `60` loading · `70` drop-list (above options, its owner) ·
`80` rename box · `85` improvements modal (over the star-system page, under its own
confirmation) · `90` tutorial-selection modal (over the new game screen) ·
`99` tutorial popup (above EVERYTHING but the message box: the game itself draws most tutorial
popups over its own screens, modals and notifications, so any lower number buries one of them —
what keeps 99 livable is that a collapsed popup stands down, and that the mod follows the panel's
own visibility, so a popup the game has hidden holds nothing) · `100` message-box.
Mod-owned CHILD screens (`Screen.PushChild`) have no layer: the manager focuses the deepest
child of the top screen.

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

Backslash, every Enter chord and **Space** are claimed on every mod screen and are **SILENT where
the control has no such command** — they are pressed speculatively all over a page, and a cue on
every one of them is noise. Silent but still consumed, and never a fall back to plain activation.
Space while something is carried is the same: consumed on a control that will not take it, silent,
carry kept. (Why Space is claimed even where nothing is draggable: the scan-view fact in
`es2-facts.md`.)
**Space is claimed wherever a mod screen is focused** — an over-claim by design (owner
decision 2026-08-11; the scan-view fact in `es2-facts.md`): the game's Space is the strategic
lens, and a player reaching for a pickup must never flip the map into an unannounced mode.
`InputAction.ClaimedWhile` remains the mechanism (`ModEntry.CarryKeyClaimed` — any focused mod
screen) so the claim can become conditional again once the lens is modelled and announces
itself; every other binding is claimed outright. While something is carried, **Escape puts it
down and goes no further** (`claimsBack` reads true only then), and the carry dies silently when the
player leaves the page it started on — a menu opened over that page is still that page.

**Typing a letter searches the focused stop** (no search key: the first printable character starts
one; Up/Down step the matches, Home/End their ends, Escape clears it and goes no further, any other
action ends it and then does its own job). So **A–Z are claimed from the game on every mod screen**
(`GraphNavigator.TakesTypedKey` via `ModInput.ClaimsTypedKey`, asked before the press), and a
space typed into a LIVE search is text — the carry key stands aside for it (Space's claim
itself is unconditional, above). Screens opt out with `AllowsTypeahead` (the rename box) or
`CapturesRawInput` (the frames between asking for a key capture / text editor and the game taking
the keyboard).

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

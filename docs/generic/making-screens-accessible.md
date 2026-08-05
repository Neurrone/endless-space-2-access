# Making a screen accessible — the process

The per-screen workflow, from an unmodeled game page to a shipped, verified screen. The
architecture it builds on is [ui-navigation.md](ui-navigation.md); this doc is the order of
operations and the doctrine that decides the model. It exists because the expensive failure
mode is not writing bad code — it is shipping a *plausible* model of a screen instead of a
*measured* one, and reworking every screen built on the same guess.

The concrete tools each step names — which routes, which helpers, which recipes — live in
the game repo's own **living dev map** (ES2 Access keeps it as `docs/dev-loop.md`; see the
playbook's note on maintaining one). Consult it first; this doc is the process, the map is
the toolbox.

## 0. The doctrine: the widget tree mirrors the game's visuals

The model of a screen is read off the pixels, not off the code. Measure rects and take a
screenshot before modeling; lay out stops and rows as drawn, in the page's reading order.
Concretely:

- A dialog with controls above and below its text is rows, with the body text itself a
  **focusable node** — usually where focus lands on arrival.
- A visual table is a table: cells announce the drawn column heading and value, Up/Down
  preserves the column (see the table pattern in [ui-navigation.md](ui-navigation.md)).
- A panel's bottom control row is its own Tab stop, walked Left/Right; side panels that open
  next to content are conditional stops or regions that exist only while open.
- Regions ([input.md](input.md)'s Alt+arrows) map to the screen's visual bands, top to
  bottom. Never declare a lone region — the jump key would swallow silently.
- Each element's review buffer holds **its own** content (its tooltip, its cell), not the
  container's shared text — the container is a walk away.

And the words are the game's words. Where the game shows something for a state — failure
tooltips, captions, placeholders — surface that text, never a mod paraphrase. Preserve it
exactly: no mod separators or punctuation inserted (multi-line game text joins with a space,
not a list comma — "disabled., Once" is a defect). Conversely, invent nothing the game does
not show: no placeholder nodes for empty states, no spoken position text where the game shows
dots — a stop with nothing in it does not exist that frame. The full text rules are in
[localization.md](localization.md). A recipe for finding those words when the game has a
state *enum*: grep the localization corpus for a key pattern built from the enum member
names — games with a status enum almost always ship a parallel string table, and it covers
the states your fixture cannot reach.

The same doctrine has a **fog-of-war corollary** in any game with partial information: the
world model answers questions the renderer refuses to — adjacency APIs return
never-discovered entities' names, and name lookups resolve for anything. Find the
renderer's own visibility predicate and route every name and fact through it; never read
the model directly for anything the player could not see. The *filter* is what needs the
test, not the model.

## 1. Research and measure

- Screenshot the screen and dump the drawn rects (the dev server's GUI dumps; see
  [dev-server.md](dev-server.md)). The rects decide rows, bands, and reading order — not the
  widget hierarchy, which routinely disagrees with what is drawn — and not collection order
  either: a container may lay children out right-to-left or in pool order, so sort by
  measured position, never trust the list.
- **Find the predicate that creates the surface.** Rects tell you what a window shows, not
  what makes it exist at all — and in a 3D-world game half the UI is gated on camera/view
  state. A window measured correctly while its existence-gate goes unread is how a whole
  feature gets missed. There is often one central method listing every window's gate; find
  it first.
- **A measured constant can be a plateau, not a boundary.** A threshold observed once ("this
  zoom step draws the full label") can be true and still wrong as a limit. When a numeric
  threshold matters, find the game code that *compares* against it and read the limit from
  there; never freeze a number measured from one observation.
- Find the screen's classes in the decompiled code: where its text lives, what its buttons
  are wired to, which service state drives it ([reverse-engineering.md](reverse-engineering.md)).
- Check the game's colliding key bindings for this screen ([input.md](input.md) — "The game
  hears your keys too"). A collision that can move the game's focus is a blocker.
- Note what the screen shows for its empty/disabled/error states — those words are the model.

## 2. Propose the model, get it approved

One compact proposal per screen, before any implementation: the measured layout (screenshot +
key rects), the stops/rows/regions mirroring it, each control's role, where focus lands, and
any non-obvious behavior (what Enter does on a row; what is deliberately not modeled). The
project owner approves the design; new key bindings need their own approval
([input.md](input.md)). When a *new kind* of surface opens (the first popup, the first table,
the first in-game HUD), the first implemented screen goes to manual screen-reader review
before sibling screens are batched — the calibration is cheap on one screen and unaffordable
on five.

## 3. Implement

Imitate the adapter exemplars, don't invent: the screen shape from an existing screen of the
same kind, widgets per [widgets.md](widgets.md), tooltips per [tooltips.md](tooltips.md),
buffers per [buffers.md](buffers.md), text through the shared pipeline
([localization.md](localization.md), icons per [icons-and-symbols.md](icons-and-symbols.md)).
Activation goes through the game's own deterministic handlers; state the game manages stays
the game's (select-then-act where the game distinguishes selection from action). Everything
reload-safe ([hot-reload.md](hot-reload.md)) and per-frame cheap — `Build` runs every tick
([performance.md](performance.md)). Two implementation rules that recur:

- **A page assembled from several independent windows**: the cursor seats on whichever half
  arrived first and, once placed, never moves — declare nothing until the late half is
  drawn.
- **Reading a panel you haven't modelled in detail** (read-only side panels,
  out-of-fixture state variants): descend only into children that are themselves
  containers; a group whose children are all primitives is ONE line. This models whole
  panels cheaply without per-widget work.

## 4. Verify with evidence, not claims

- Walk the whole screen in one request (the accessible-tree dump,
  [dev-server.md](dev-server.md)) and read it against the screenshot.
- Any claim that spoken output matches drawn output carries the **evidence pair**: a
  screenshot cropped to the claimed region (with its rect) beside the spoken/buffer lines.
  Cropping is also what keeps image costs sane — never read full frames.
- Exercise keys at the production dispatch point (input injection), not by calling the
  navigator directly — a screen that answers the navigator but not the injector is a screen
  whose keys don't reach it.
- Then reason through what the harness structurally cannot reproduce — real key-down/key-up
  sequences, focus handoffs, perceptual timing — item by item, on paper
  ([dev-server.md](dev-server.md) "What this loop cannot verify").

**The unreachable-screen tier.** When a screen cannot be reached in the fixture at all (a
cutscene needing game progress, a panel behind an unbuildable unit), verification has a
named fallback shape — do all of it, not some: prove the screen registers (the by-key graph
dump route); prove its predicate is FALSE at every reachable neighbouring state; walk the
opener's event/code chain with file:line cites recorded in the screen's own doc comment;
unit-test whatever logic was extracted into the engine-free core; and hand the entire
perceptual run to the human as a named blocked item. Never ship a plausible-but-unmeasured
model silently — the blocked list is the honesty mechanism.

## 5. Hand over the manual test

The human test script lists the exact steps, exactly what should be heard at each step, and —
for anything visual — what a sighted observer should see. Its content is the residue of step
4: every physically-untestable item becomes a scripted check. Perceptual behavior (repeat
cadence, interrupt feel, whether focus visibly follows) is only ever confirmed this way.

## 6. Keep the docs alive

A finished screen updates the project's living docs in the same change (screen inventory,
any new helper or recipe) — and anything learned that is game-agnostic comes back to these
generic docs. A screen is not done while its lessons exist only in the diff.

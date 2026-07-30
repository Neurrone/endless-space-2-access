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
[localization.md](localization.md).

## 1. Research and measure

- Screenshot the screen and dump the drawn rects (the dev server's GUI dumps; see
  [dev-server.md](dev-server.md)). The rects decide rows, bands, and reading order — not the
  widget hierarchy, which routinely disagrees with what is drawn.
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
([performance.md](performance.md)).

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

## 5. Hand over the manual test

The human test script lists the exact steps, exactly what should be heard at each step, and —
for anything visual — what a sighted observer should see. Its content is the residue of step
4: every physically-untestable item becomes a scripted check. Perceptual behavior (repeat
cadence, interrupt feel, whether focus visibly follows) is only ever confirmed this way.

## 6. Keep the docs alive

A finished screen updates the project's living docs in the same change (screen inventory,
any new helper or recipe) — and anything learned that is game-agnostic comes back to these
generic docs. A screen is not done while its lessons exist only in the diff.

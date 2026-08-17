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
  **focusable node** — usually where focus lands on arrival. The dialog's drawn heading is
  a node too — a three-part contract, jointly load-bearing: the heading is a focusable
  node first in reading order; the screen's spoken name carries the same words (arrival
  announces it); and the start node is set explicitly on the first focusable node below
  the heading (often the body text itself). The third part is not optional: the screen name
  is spoken outside the graph, so the announcer's dedupe cannot save a screen whose focus
  lands on the heading node — arrival would speak the title twice. A window's own heading is declared once, in the
  first stop — never repeated per band. And a caption the game draws over
  **several** controls is itself a navigable node; a caption tied to exactly one control
  folds into that control's readout instead.
- A visual table is a table: a row identifies itself by name, a cell speaks its value with
  the column heading spoken as the crossed edge, Up/Down preserves the column (see the table
  pattern in [ui-navigation.md](ui-navigation.md)).
- A panel's bottom control row is its own Tab stop, walked Left/Right; side panels that open
  next to content are conditional stops or regions that exist only while open.
- Two navigation tiers, one division of labor: the screen's visual panels are **Tab
  stops**; the sections *within* one panel are **regions**, jumped with the region chord
  ([input.md](input.md)'s Alt+Up/Down) — the jump never crosses a panel, Tab does that.
  What counts as a section is the game's answer, not the mod's: content the game draws
  as captioned groups is one region per group, never flattened into one — a row must be
  heard in the group it belongs to. Never declare a lone region — the jump key would
  swallow silently — and never a derived boundary the player cannot see: a derived
  section needs a drawn caption to justify it.
- Each element's review buffer holds **its own** content (its tooltip, its cell), not the
  container's shared text — the container is a walk away.

And the words are the game's words. Where the game shows something for a state — failure
tooltips, captions, placeholders — surface that text, never a mod paraphrase. Only words
the game finished writing count: text still holding an unfilled template slot ("… {0}") or
a key the localizer hands back unchanged ("%SomeKey") is parked, not shown — treat it as
absent, never speak it. The same trap has a prefab form: a widget HIDDEN today may already
carry its text from the prefab, so a tooltip-fed readout gated on anything but the game's own
drawn flag ships a false statement about every healthy object. Ask drawn-ness of the CHAIN
with the engine's own child test — a container retires without touching what it holds — and
start the walk below the screen's root, because the root itself may animate. Preserve it
exactly: no mod separators or punctuation inserted (multi-line game text joins with a space,
not a list comma — worked examples in [localization.md](localization.md)). Conversely, invent nothing the game does
not show: no placeholder nodes for empty states, no spoken position text where the game shows
dots — a stop with nothing in it does not exist that frame. "Invent nothing" has a companion:
**say everything the game always draws.** The discriminator is *always drawn* vs *revealed on
hover* — permanently drawn text (a card's description paragraph) is spoken in full as part of
the control's readout; hover-revealed text is announced or buffered by the tooltip rule
([tooltips.md](tooltips.md)). Applying the tooltip rule to always-drawn text silences words
that are on the player's screen. And what a screen says and what it draws are not
alternatives: a body composer written as "if there is a description, else the drawn
content" silently drops one side — and no dump reveals it, because the result still reads
fluently. A composer composes, it never chooses. The full text rules are in
[localization.md](localization.md). A recipe for finding those words when the game has a
state *enum*: grep the localization corpus for a key pattern built from the enum member
names — games with a status enum almost always ship a parallel string table, and it covers
the states your fixture cannot reach.

"Invent nothing" has one licensed exception: **gathering a scattered signal**. A game
sometimes encodes a fact as decoration spread across a spatial layout — a "suggested" badge
on one of hundreds of nodes, a highlight on scattered tiles — which a sighted player takes
in at a glance and a blind player cannot recover by walking. A mod-arranged surface (a list
stop of exactly those items) is then the only accessible rendering of something the game
*does* show. The discriminator: is the fact drawn somewhere the player can walk to, or only
encoded in position and decoration across the layout? The license has three conditions:
every word in the aggregate is the game's own, the surface is approved by the project owner,
and it is recorded as a deliberate deviation in the screen's doc comment.

The same doctrine has a **fog-of-war corollary** in any game with partial information: the
world model answers questions the renderer refuses to — adjacency APIs return
never-discovered entities' names, and name lookups resolve for anything. Find the
renderer's own visibility predicate and route every name and fact through it; never read
the model directly for anything the player could not see. The *filter* is what needs the
test, not the model. And the predicate is per FACT, not per entity: partial-information
games commonly gate facets of one thing separately (as one example, ES2 draws a fleet at
one detection tier, its ship count a tier higher, and its path only on a diplomatic
ability), so passing an entity's existence gate discharges nothing about its other facts —
ask "who draws THIS number" once per fact you speak. And the filter governs names and facts, not OFFERS: a game can hide a
thing's identity while still letting the mouse act on its position, and withdrawing an
affordance the mouse has is a separate, louder decision than withholding a name.

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
- **Take a state test from the code that DRAWS the state.** The model's best-named
  property can be wrong (a fleet's `IsMoving`, public and named exactly right, reads false
  the moment it spends its movement mid-route); the status column, icon switch or colour
  swap the game renders the state from is the test the player's screen agrees with — and
  it doubles as §4's free oracle.
- Find the screen's classes in the decompiled code: where its text lives, what its buttons
  are wired to, which service state drives it ([reverse-engineering.md](reverse-engineering.md)).
  A data-driven screen can keep its **shape** and its **values** in separate registries —
  one table saying which rows exist and their widget kind, another what each row can say.
  Both are closed sets answering different questions; find both before modeling.
- **A reused window is modelled off what is drawn, not what it was opened for.** A window
  serving several modes may build every mode's content and hide all but one; "which mode
  was I opened for" is opener-set state that can go stale, "which content is drawn" cannot.
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

Approval is not a second measurement: when implementation-time measurement contradicts the
approved design — drawn order, a surface the design missed — ship the measured shape (a gap
takes the sibling exemplar's treatment) and report the deviation with its evidence, rather
than shipping the approved-but-contradicted shape or stalling to re-ask.

## 3. Implement

Imitate the adapter exemplars, don't invent: the screen shape from an existing screen of the
same kind, widgets per [widgets.md](widgets.md), tooltips per [tooltips.md](tooltips.md),
buffers per [buffers.md](buffers.md), text through the shared pipeline
([localization.md](localization.md), icons per [icons-and-symbols.md](icons-and-symbols.md)).
**Rows are shared, layout is not**: when a second screen shares the same row prefabs as an
existing one, hoist the per-widget row builder out of the first screen instead of copying
its shape — the second screen then inherits widget kinds its own fixture never draws.
Activation goes through the game's own deterministic handlers; state the game manages stays
the game's (select-then-act where the game distinguishes selection from action). Everything
reload-safe ([hot-reload.md](hot-reload.md)) and per-frame cheap — `Build` runs every tick
([performance.md](performance.md)). Two implementation rules that recur:

- **A page assembled from several independent windows**: the cursor seats on whichever half
  arrived first and, once placed, never moves. That gate protects the SEATING, so hold back
  only while the cursor is what is at stake: a page whose early half is already usable
  declares it in drawn order and lets the late half join a later rebuild. A page-wide gate on
  one piece turns a half the game drops — a show it defers and then loses — into permanent
  silence.
- **Reading a panel you haven't modelled in detail** (read-only side panels,
  out-of-fixture state variants): descend only into children that are themselves
  containers; a group whose children are all primitives is ONE line. This models whole
  panels cheaply without per-widget work — and completely only where the panel's tooltips
  are content-backed ([tooltips.md](tooltips.md)).

## 4. Verify with evidence, not claims

- Walk the whole screen in one request (the accessible-tree dump,
  [dev-server.md](dev-server.md)) and read it against the screenshot — and grep the dump
  for nodes whose line opens with a bare role: a control whose name resolved empty
  announces itself role-first, and nothing else in the loop flags it.
- Any claim that spoken output matches drawn output carries the **evidence pair**: a
  screenshot cropped to the claimed region (with its rect) beside the spoken/buffer lines.
  Cropping is also what keeps image costs sane — never read full frames. And the pair's
  spoken half is judged **as a listener hears it**, not as a match: a number without its
  caption in the same line, or a tooltip feature answered by the fallback reader, FAILS the
  check even though it matches the pixels perfectly — "1500/1500" beside an unnamed icon
  satisfies spoken-equals-drawn and tells the player nothing. Matching is necessary;
  comprehensible is the bar.
- **When N prefabs share one reader, per-screen evidence pairs do not scale** — ship a
  mechanical parity check that re-derives both sides (the drawn tree, the declared graph)
  and runs itself on whatever the player meets; a shared reader's premises are per-prefab
  data and only measurement can validate them.
- **A fix whose whole effect is an ABSENCE has no pair until you build one.** Silence on a
  clean run is not evidence, and these repro windows are narrow by nature. Revert the one
  guard, rebuild, reload, re-run the same probes: the failing half costs a couple of minutes
  and is the only thing that shows the fix does anything at all.
- **Verify with player-available gestures only.** Reaching the state under test by an engine
  call — opening the window from the REPL, arming a mode by setting its flag — proves the
  READING and never the reachability, and a screen whose only route in is a method no key
  reaches passes every evidence pair while being unusable (it shipped that way twice). The
  route in belongs in the evidence too: injected actions that correspond to real keys,
  pressed from where the player actually stands.
- **Exercise a gesture from states the test did not create.** A verification that always
  arrives through its own setup path only proves that path's precondition — a zoom-restore
  verified by zoom-in-then-restore never met a camera that was already zoomed by other
  means, and shipped broken for exactly that player. Enumerate the states the game can be
  in when the key is pressed, not the states the test script produces.
- Exercise keys at the production dispatch point (input injection), not by calling the
  navigator directly — a screen that answers the navigator but not the injector is a screen
  whose keys don't reach it. The same trap exists one level down: calling the game's
  handler from the REPL to "confirm the wiring" proves the handler, not the mod's route to
  it — any step that verifies an *activation* must go through the mod's own activate path.
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

The same rigor scales down from screens to features: **a fixture-blocked claim is a
measurement, not an inference.** "The fixture cannot show X" in a report or blocked list
needs a probe over the game's own predicate — one eval — never a chain of
read-from-the-code plausibilities. A wrong entry in a blocked list is worse than a gap: it
retires a feature from testing on a guess (measured instance: "no stage deeds at turn 2"
was derived from preconditions, every link plausible, and 12 of 20 stages had one). And
when the game *changes a rendering decision* on the same predicate your speech depends on —
swapping a tooltip's class, an icon, a color — that switch is a **free oracle**: it
confirms your state mapping independently, with no second probe — and the engine often
already computes the number you are about to recompute; find its predicate and match it.

## 5. Hand over the manual test

The human test script lists the exact steps, exactly what should be heard at each step, and —
for anything visual — what a sighted observer should see. Its content is the residue of step
4: every physically-untestable item becomes a scripted check. Perceptual behavior (repeat
cadence, interrupt feel, whether focus visibly follows) is only ever confirmed this way.

## 6. Keep the docs alive

A finished screen updates the project's living docs in the same change (screen inventory,
any new helper or recipe) — and anything learned that is game-agnostic comes back to these
generic docs. A screen is not done while its lessons exist only in the diff.

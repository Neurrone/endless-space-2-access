# UI navigation — the graph engine

The keyboard-navigable accessible UI layer: how a screen becomes something a blind player
walks with arrows and hears. Design lineage: Factorio Access's key-graph → Tanglebeep →
wotr-access's graph engine, ported here (BCL-pure, unit-tested — 100+ offline tests came with
the port) and proven on ES2's main menu. The engine is game-agnostic and copied as-is; a thin
per-game adapter binds it to the game's widgets, input, and speech.

## The architecture: immediate mode

Every frame, the active screen declares its **entire UI as a graph** by reading live game
state — no retained tree, no widget diffing, no cache to invalidate. The declared
`GraphRender` is thrown away on every rebuild; the only thing that survives is `GraphState`:
the focus cursor, per-tab-stop remembered positions, and the set of expanded groups. This
buys the property that makes announcements reliable: **a focus change is announced exactly
once, by one code path, regardless of what caused it** — a keypress, a rebuild, or the game
yanking a widget out from under the cursor. The navigator diffs last-spoken focus against
current focus once per frame after everything settles. The cost side of this bargain —
rebuilds proportional to the open screen, never the world — is
[performance.md](performance.md)'s "bound immediate-mode rebuilds".

Cursor survival across rebuilds is tiered (`KeyGraph.Reconcile`): follow the **backing
object** if it moved (identity ride-along via `ControlId.Reference`); else the same
**structural key**; else the **nearest survivor** walking the previous traversal order; else
the start node. Give any node whose row/entity can move or vanish a `ControlId.Referenced`
identity and reconciliation is free. The sharper case is not "the row moved" but **the
widget stayed and its meaning changed**: a table that pools and re-sorts its line widgets
reassigns them on every re-sort, so a cursor keyed on the widget acts on a different item
a frame later — heard as a correct-sounding announcement attached to the wrong action,
which no dump reveals. Key such lines on the game's *data* object, never the widget.

## Core engine (copy verbatim)

- **`ControlId`** — two-tier identity: value-equatable structural key + optional object
  reference. **The structural key is the uniqueness key** — the reference only rides along
  for reconciliation. Derive a repeated node's key from its index in its parent, never from
  a widget name: pooled/recycled widgets share names transiently, and a duplicate key
  throws out of `Build`. The symptom of that throw is three layers from the cause — the
  whole screen silently declares nothing — so the first diagnostic for an unexpectedly
  empty screen is the log, not the model.
- **`GraphTypes`** — `GraphNode` (4-way `Transitions`, `Parent` chain, stop/region keys,
  expandability, auto position) and `NodeVtable`: **behaviors as data**. Announcement parts
  (`NodeAnnouncement`, each a `Func<string>` resolved at speak time — read live, never cache),
  `OnActivate`/`OnSecondary`/`OnAdjust` (adjust preempts Left/Right navigation),
  `StateText` (spoken interrupting right after activate/adjust), `DetailLines` (feeds the
  review buffer — see [buffers.md](buffers.md)), focus-visual hooks. `ControlType` is a
  **registry value, not a class** — it owns the localized role word and the speak order of
  part kinds. wotr-access migrated off a class-hierarchy proxy system to this; don't
  reintroduce classes.
- **`GraphBuilder`** — the per-render DSL. Menu mode (`StartRow`/`AddItem`) auto-wires rows
  and gives rows sharing a `rowKey` column-preserving vertical navigation; raw mode
  (`AddNode`/`Connect`) wires arbitrary topologies; `PushContext` adds non-focusable labeled
  levels (spoken once on entry via the path diff) — its id derives from its label, so
  sibling contexts sharing a drawn caption (seven slots all captioned "AI") silently
  collapse in the path diff: disambiguate the label or key when the game repeats captions; `BeginStop` partitions a screen into
  Tab-cyclable stops with remembered positions; regions subdivide stops — **two tiers, one
  division of labor**: Tab moves between panels (stops), the region jump (WotR: Ctrl+arrows;
  ES2 Access: Alt+arrows) moves between the sections *of the current panel*, landing on the
  section's first node. Regions are scoped to their stop by construction (`MoveRegion`
  filters on the stop key) — a screen that models its visual panels as stops has no region
  jumps between them, and that is the intended model, not a gap (WotR's inventory: five
  panels as stops; regions used inside a panel — quest groups within the journal list,
  action-bar segments); expandable groups
  give tree semantics to Left/Right. Two expansion rules: `IsExpanded` is the cost gate —
  declaring children only when expanded is the whole of how a tree stays inside
  [performance.md](performance.md)'s bounded-rebuild rule — and the `OnExpand`/`OnCollapse`
  vtable hooks are **replacements** for the engine's own expansion bookkeeping, not add-on
  callbacks (they exist for adapters driving a retained game-side container that owns its
  expand state); a hook that only wants a side effect must flip the builder's expansion set
  itself, or the tree refuses to stay open. Expansion also changes **no world state**: in a
  game whose view distance decides what is drawn, "go in" (tree) and "get closer" (camera)
  are different verbs, and binding a camera move to the expand key makes whichever
  information tier the other zoom level shows unreachable — when different zoom levels show
  different information, both sets must stay accessible, with the camera on its own
  explicit control (wherever fits the screen's design).
- **`GraphAnnouncer`** — composes speech by **diffing the old and new focus paths**: the
  common ancestor prefix stays silent, newly entered levels read outermost-first, duplicate
  labels dedupe. Injected delegates (`PartFilter`, `PositionText`, `ExpandedStateText`) keep
  it speech- and language-agnostic — the adapter wires them to the localization layer.
  Position stamping: "n of m" among the siblings arrows actually reach, suppressed for a
  lone sibling — **except under an expandable group**, where even an only child announces
  "1 of 1": having just descended, the player needs to hear the level's size (found by ear
  on ES2's one-item flyouts).
- **`KeyGraph`** — the operations (`Move`, `MoveStop`, `MoveRegion`, `TreeRight`/`TreeLeft`,
  `MoveToEdge`, `Activate`…). It never speaks; every operation returns a result for the
  caller to announce.
- **`GraphSheet`** — screen-reader tables over raw mode: one stop, regions as sections,
  column-preserving rows, edge labels naming the crossed column, ragged rows falling back to
  the primary cell.
- **`TypeAheadSearch`** — tiered fuzzy matching (ported to WotR from OniAccess), engine-side.

## The adapter (per game; imitate, don't copy)

- **Navigator** (`GraphNavigator`): binds input actions to KeyGraph operations; the single
  `EnsureFocus` site announces the focus diff, fills the review buffer, applies focus
  visuals, and baselines the live-part watch. Navigation moves interrupt speech (held-key
  repeat reads where you land); screen-entry announcements queue — the interrupt-policy
  tiers in [speech.md](speech.md).
- **Screens** (`Screen`/`ScreenManager`): poll-and-diff. Each screen declares `Key`, `Layer`,
  an `IsActive()` predicate re-evaluated every tick against live game state (window
  visibility + readiness gates + "is a sub-window covering me"), and `Build(GraphBuilder)`.
  The manager sorts active screens by layer, diffs against its stack, keeps one `GraphState`
  per live screen (a covered screen keeps its cursor), and speaks `ScreenName` on focus.
- **Input**: the whole subject — the mod's chord/repeat/stand-down layer, the default key
  table, the game's colliding bindings and the suppression doctrine that resolves them —
  lives in [input.md](input.md).
- **Node factories** (`GraphNodes`): per-widget-type constructors binding the game's widgets
  into vtables — label funcs through the game's text pipeline (localize, strip markup),
  activation through the game's own deterministic click path, tooltip surfacing per
  [tooltips.md](tooltips.md), detail lines per [buffers.md](buffers.md).
- **Focus visuals** (`PointerFocus`): make the game *look* pointed-at where focus is, so
  sighted bystanders can follow. Use the engine's own entry points, never OS cursor
  warping — and find the hover entry point **per control class**: one widget kind may
  expose an explicit hover-simulation call while another is only reachable through its
  own mouse-enter/leave path. Verify the chosen target actually changes pixels — a widget
  parked inside a card that *looks* like the hover target can be wired to nothing, and
  only a screenshot pair proves the highlight moved. Send the same show/hide-submenu
  messages the game itself sends; the native tooltip appears by feeding the engine's
  hover-authority field — written at **end of frame**, after the engine's own LateUpdate
  recompute would have clobbered it (a `WaitForEndOfFrame` coroutine; no patching needed).
  Track wanted-vs-showing and apply only differences so stepping within a flyout never
  flickers it. Check tooltip **anchoring**: cursor-anchored ("free") tooltip modes render at
  the OS mouse position — during a keyboard session that is a random screen corner, which
  reads as "the tooltip doesn't appear" — so while focus rests on an element, re-anchor its
  tooltip to that element (setting the anchor explicitly if a null anchor falls back to some
  default marker) and restore the original values on blur. Anchor to the transform that hugs
  the **visible text** (the label), not the hit area — hit areas get stretched to cover
  flyouts and hover zones, and an anchor under a stretched hit box floats far from the text
  (ES2: the featured menu entry's button was twice the height of its label). Real-mouse policy: keyboard focus
  wins on every focus change, but the physical mouse is not fought frame-by-frame — document
  the tradeoff. Restore everything on teardown. Verify focus visuals with **measured rects
  and screenshots**, never existence checks — both anchoring lessons above were found only
  after "the tooltip appeared" passed automated verification while rendering somewhere
  absurd.
- **Scroll-into-view** (`ScrollIntoView.cs`): whenever focus lands on a control inside a
  scrollable view, scroll minimally until it is fully visible. Hook the **single
  focus-commit site** (beside the focus-visual application), resolve the container from the
  node's backing object, and every screen — present and future — inherits it with nothing
  declared. Scroll through the engine's **own scroll entry point** (ES2: replaying the
  mouse-wheel handler) rather than writing offsets directly, so clamping, scrollbar state
  and scroll notifications stay identical to a hand on the mouse. Only scroll when the
  control is actually out of view, and never re-run per frame — a self-correcting loop would
  fight a sighted helper's wheel, the same tradeoff as the real-mouse policy above. Verify
  with measured rects: the row past the edge must land *at* the edge (a unit mismatch
  between rect space and scroll space shows up as consistent under/overshoot).
- **Discover controls, don't trust named references**: a reused window can carry duplicate
  per-skin control sets (ES2's options window has two complete button bars) with the API's
  named fields pointing at one skin — possibly the hidden one. And **no visibility flag can
  be trusted on its own**: containers hide whole ancestor chains while the child still
  reports visible, and pooled list containers do the reverse — surplus recycled children
  stay "visible" with zero alpha, so a flag-trusting sweep declares rows the player cannot
  see (example: ES2's competitor-slot pool kept an eighth slot alive this way after the
  count came back down). Effective drawn-ness is the ancestor walk *plus* whatever render
  test the engine itself honours (alpha, clipping). Declare what is actually drawn — walk
  ancestors for visibility, test drawn-ness, filter decorative click-shields (no activation
  wiring), order by measured position so speech order matches the screen.

## Patterns proven since the port

- **Passive announcements** (things that change while no control is focused — loading
  progress, a page the game advances, the turn number): a screen's per-frame update diffs a
  tuple of live source values and speaks QUEUED, never interrupting. Baseline the diff when
  the screen arrives (arrival already speaks via `ScreenName`; the two must not both fire) —
  but only for a source that *outlives* the screen, like loading progress. For a source the
  game itself clears on leave, baseline to **nothing** instead: an arrival baseline can
  swallow the first event depending on frame timing, and the way to tell the two kinds
  apart is to find where the game nulls the field. Commit the watermark **only on the path
  that actually speaks**: a source can bind a frame before its UI draws, and consuming the
  diff on an early-return frame loses the announcement permanently (a composer that returns
  null for "nothing to say yet" is the natural signal). Reset it when the screen leaves,
  and keep all of it instance state so it is reload-safe by construction. For a continuous 0..1 value, quantize into steps (quarters), announce upward
  crossings only, only the highest when one frame crosses several, and re-arm when the value
  drops so a restarted phase reports afresh. Worked sample: `src/graph-ui/LoadingScreen.cs`.
- **Arriving and standing down are different questions.** Arrive only when the widget has
  finished animating in (its labels may still hold the previous item's words) — and
  arrival also gates on **enablement**, not just visibility: an engine that disables the
  page under a modal can re-enable it a frame *after* the modal reports itself gone, so a
  screen arriving on "modal gone" alone reads every control "unavailable" exactly once —
  invisibly, because live parts only speak on change. A window can also be closed by the
  player's own *delegated* key — an exit key the mod let through that commits and hides in
  one frame; polling shown-state covers that for free, watching only the mod's own key
  handling does not. But never
  stand down while merely *covered* — everything that hides your panel draws above your
  layer, and a screen that blinks out mid-transition hands the player to whatever is
  underneath for a frame (heard as a spurious announcement of the screen below). For
  staying active *through* a transition, prefer a second, **non-blinking authority** over
  any timer: games often answer "which page is up" from more than one place, and one blips
  null on a same-page re-entry while another does not — picking the right source beats a
  bounded linger, and a linger can be actively wrong (it keeps a screen alive while a
  same-layer sibling wakes). For **leaving**, gate on the game's *unbind* — the data the
  window was opened for going null — not on visibility: a window can stop reporting shown
  at *begin*-hide while the page beneath re-enables only at *end*-hide, and a visibility
  gate strands the player on a page that is not yet interactive. Where the game unbinds for
  a single frame during a rebind, a small bounded linger is the last resort. Through all of
  it, an empty `Build` is the safety valve: declaring nothing is legal — the render is
  skipped and the cursor survives — which is what makes staying active through a
  transition safe.
- **Initial focus and Tab clamping**: Tab does not wrap, so whichever stop the cursor starts
  on must be the first stop, or Tab reads as broken. An explicit start node wins over the
  "land on the selected alternative" rule unless the start node is itself one of the
  alternatives (declares a selected-kind part).
- **Layers are static.** A screen's layer must never change while it is up: other screens
  (popups, confirmations) are placed *relative* to it, and a layer that slides underneath
  them cannot be reliably placed under either value. Number with gaps; when a window can be
  opened from pages at different layers, give it one number above the highest opener — and
  **below anything its own controls can raise** (a modal whose combo boxes open the shared
  drop-list popup must sit under the popup's layer). Getting that bound wrong is silent:
  the popup renders, but the wrong screen keeps focus.
- **A roster grid linearises.** A grid of cards (factions, loadouts, portraits) reads as
  one row per card in drawn order — left-to-right, top-to-bottom — not as a 2D table: the
  cells are peers of one kind, so column-preserving vertical moves buy nothing and the
  grid's wrap points are a rendering accident. A card's permanently-drawn description
  follows the always-shown-text rule ([widgets.md](widgets.md)); the card's substance
  lives in its buffer ([buffers.md](buffers.md)'s card example).
- **Tables read as tables**: one graph row per data row with a shared row key (Up/Down keeps
  the column), one node per cell announcing the drawn column heading then the drawn value,
  entering the table announces its role once. Never drop an empty cell — the shared-column
  invariant dies — speak its heading with an "empty" word. A cell's review buffer holds that
  cell's own content (heading, value, the cell's own tooltip), not the whole row: the row is
  a walk away. `GraphSheet` (above) is the raw-mode engine for this; the drawn-header pairing
  is the adapter's job, by the game's own column names, never by index.
- **Minimized is not gone**: when the game collapses a panel to a title bar rather than
  hiding it, hand the keyboard to the surface beneath (the collapsed screen stands down) and
  declare the leftover bar's controls where they are drawn — as a stop on the screen below
  (and note the screen below *changes*: see the persistent-overlay pattern next) — because
  the game's restore affordance is mouse-only.
- **A persistent overlay is a contributor, not a screen.** A HUD cluster drawn over several
  pages (end-turn controls, resource banners, a collapsed tutorial bar) must not be declared
  by whichever page first met it — when the player moves to another page under the same
  overlay, those controls silently vanish from the tab order. Extract one contributor
  object owning the cluster's stops and their passive announcers; each page calls it in its
  own drawn order; stop keys are named after the cluster (not after any one page), and
  per-screen cursor memory keeps working unchanged because the stops live in each page's
  own `GraphState`.
- **A control the game wires to a screen you haven't modelled yet** is a named tradeoff,
  decided explicitly and reported: declare it read-only (the player loses an affordance but
  hears no dead end), or declare the action and accept that it opens a silent screen until
  that screen is modelled (the affordance kept, the dead end temporary). Either is
  defensible; choosing silently is not.

## Child screens and action menus

A control with more than one action cannot spend Right on "show me the actions" — the
arrows are world and tree navigation. The answer is a **modal vertical-list menu** opened
from the control, holding its actions; destructive or drag-only defaults go in the menu,
never on the activation key. The mechanism (wotr-access's choice submenu, ported to ES2
Access):

- **Child screens**: a screen can push one child (`PushChild`/`RemoveChild`, a single
  linear chain — no branching); the manager's current screen is the deepest active child.
  Per-screen state isolation is what returns focus to the opening control for free: the
  covered parent's cursor is never touched, and a popped child's state is dropped.
- **One `Open(title, options, current, onSelect)` entry point.** Options are a snapshot
  built at open time from live predicates; invalid actions are simply not added — no
  disabled entries to skip past. The empty case ("nothing to do here") is answered once,
  inside the shared helper, never per caller. Enter invokes then closes, in that order;
  `current = -1` lands on the first option; entries carry a "menu item" role word.
- **This is not the native-popup wrapper.** Wrapping a game's own drop-list popup needs
  game-focus handoff and a deferred close to dodge the engine's Escape race; a pure
  mod-owned child screen needs none of that. Keep the two mechanisms separate.
- **Menus are for controls with N actions.** When the game's own model is select-then-act
  (toggle items, then one button acts on the selection), keep the game's model — do not
  force a menu onto it.
- Escape must close the menu and go **no further**: a mod-owned surface denies the game the
  key — see the back-key ownership rules in [input.md](input.md).

## The confirmation-dialog screen

Games funnel confirmations through one shared message-box window (quit?, discard changes?,
countdown boxes). Make it a single high-layer screen registered once — every flow that dead-ends
in a confirmation then speaks for free, and a silent confirmation is a soft-lock for a blind
player. The shape (`src/graph-ui/MessageBoxScreen.cs`):

- **Top layer**, above every ordinary screen; ordinary screens must yield while a modal is
  visible so the hand-off is clean and their cursor survives underneath.
- The question is a **focusable text node** where focus lands on arrival — re-readable in
  place by refocusing, walkable in the review buffer — with the answers as a row below it, in
  **drawn order** (which button is left on screen is which button reads first). `ScreenName`
  carries only the dialog's static heading, so arrival speaks heading then question exactly
  once. Declare the buttons from **live visibility** each rebuild, never from the API's
  nominal shape — dialog windows get reused with leftover state from the previous dialog.
- **Text the game rewrites every frame** (countdown timers) must never feed node identity,
  live announcement parts, or per-frame speech: the text node's label resolves live (a
  refocus or buffer read gives the current second) but nothing re-announces on its own.
- Let Escape fall through to the game's own cancel path — this is a *game-owned* surface;
  a mod-owned child screen does the opposite ([input.md](input.md)'s back-key rules). Poll
  the window's "shown and fully ready" state for **arrival** rather than subscribing to
  visibility events, which fire before the captions are written — and remember that gate is
  arrival-only; departure gates on the unbind (see "Arriving and standing down").

## Adapting to a new game

1. Verify the game has no usable focus-navigation system of its own first (ES2's AGE: none —
   focus is mouse-only; gamepad keycodes filtered out of the rebind UI). If one exists,
   consider piggybacking before building.
2. Wire the text pipeline: final localized display string + the game's own markup cleaner.
3. Wire activation: the deterministic handler a real click reaches (not synthesized input).
4. Wire screen predicates: visibility + the game's "fully shown and interactive" gate.
5. Add role words and state words to the localization table.
6. Ship the first screen, verify announcements via the dev server, then hand the human the
   perceptual test script (repeat cadence, interrupt feel, announcement shape) — built from
   the list of what the harness cannot reproduce (see "What this loop cannot verify" in
   [dev-server.md](dev-server.md)).

## Source files

Engine (game-agnostic): [`src/graph-ui/`](src/graph-ui/) — `ControlId.cs`, `GraphTypes.cs`,
`KeyGraph.cs`, `GraphBuilder.cs`, `GraphAnnouncer.cs`, `TooltipParts.cs`, `GraphSheet.cs`,
`TypeAheadSearch.cs`, `TextUtil.cs`. Adapter exemplars (ES2-specific, models to imitate):
`GraphNavigator.cs`, `ControlTypes.cs`, `GraphNodes.cs`, `AgeText.cs`, `PointerFocus.cs`,
`ScrollIntoView.cs`, `AgeLayout.cs` (reading drawn layout: row banding by mutual-centre
containment — adjacent panels overlap by pixels, so span overlap misgroups — reading order,
and the alignment tiebreak for co-located caption/value rects), `Screen.cs`,
`ScreenManager.cs`, `MainMenuScreen.cs`, `DropListScreen.cs`, `MessageBoxScreen.cs`,
`ChoiceSubmenuScreen.cs` (the action menu), `LoadingScreen.cs`. The input layer: [input.md](input.md). Value-widget patterns
(checkboxes, sliders, combo boxes, tabs, popups, key capture): [widgets.md](widgets.md).
The per-screen process (measure → model → approve → implement → verify → hand over):
[making-screens-accessible.md](making-screens-accessible.md).

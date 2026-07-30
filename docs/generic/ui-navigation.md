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
identity and reconciliation is free.

## Core engine (copy verbatim)

- **`ControlId`** — two-tier identity: value-equatable structural key + optional object
  reference.
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
  levels (spoken once on entry via the path diff); `BeginStop` partitions a screen into
  Tab-cyclable stops with remembered positions; regions subdivide stops; expandable groups
  give tree semantics to Left/Right.
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
  sighted bystanders can follow. Use the engine's own entry points, never OS cursor warping:
  ES2 has `SimulateHover(bool)` on buttons and the same show/hide-submenu messages the game
  itself sends; the native tooltip appears by feeding the engine's hover-authority field
  (`OverrolledTransform`) — written at **end of frame**, after the engine's own LateUpdate
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
  named fields pointing at one skin — possibly the hidden one. And a widget's own visibility
  flag is not **effective** visibility: containers hide whole ancestor chains while the
  child still reports visible. Declare what is actually drawn — walk ancestors for
  visibility, filter decorative click-shields (no activation wiring), order by measured
  position so speech order matches the screen.

## Patterns proven since the port

- **Passive announcements** (things that change while no control is focused — loading
  progress, a page the game advances, the turn number): a screen's per-frame update diffs a
  tuple of live source values and speaks QUEUED, never interrupting. Baseline the diff when
  the screen arrives (arrival already speaks via `ScreenName`; the two must not both fire),
  reset it when the screen leaves, and keep all of it instance state so it is reload-safe by
  construction. For a continuous 0..1 value, quantize into steps (quarters), announce upward
  crossings only, only the highest when one frame crosses several, and re-arm when the value
  drops so a restarted phase reports afresh. Worked sample: `src/graph-ui/LoadingScreen.cs`.
- **Arriving and standing down are different questions.** Arrive only when the widget has
  finished animating in (its labels may still hold the previous item's words); but never
  stand down while merely *covered* — everything that hides your panel draws above your
  layer, and a screen that blinks out mid-transition hands the player to whatever is
  underneath for a frame (heard as a spurious announcement of the screen below). Games also
  *unbind* data rather than hide windows during transitions, so "is my data attached" is not
  the stay-active gate either — a small bounded linger covers a rebind that takes a frame.
- **Initial focus and Tab clamping**: Tab does not wrap, so whichever stop the cursor starts
  on must be the first stop, or Tab reads as broken. An explicit start node wins over the
  "land on the selected alternative" rule unless the start node is itself one of the
  alternatives (declares a selected-kind part).
- **Layers are static.** A screen's layer must never change while it is up: other screens
  (popups, confirmations) are placed *relative* to it, and a layer that slides underneath
  them cannot be reliably placed under either value. Number with gaps; when a window can be
  opened from pages at different layers, give it one number above the highest opener.
- **Tables read as tables**: one graph row per data row with a shared row key (Up/Down keeps
  the column), one node per cell announcing the drawn column heading then the drawn value,
  entering the table announces its role once. Never drop an empty cell — the shared-column
  invariant dies — speak its heading with an "empty" word. A cell's review buffer holds that
  cell's own content (heading, value, the cell's own tooltip), not the whole row: the row is
  a walk away. `GraphSheet` (above) is the raw-mode engine for this; the drawn-header pairing
  is the adapter's job, by the game's own column names, never by index.
- **Minimized is not gone**: when the game collapses a panel to a title bar rather than
  hiding it, hand the keyboard to the surface beneath (the collapsed screen stands down) and
  declare the leftover bar's controls where they are drawn — usually as a stop on the screen
  below — because the game's restore affordance is mouse-only.

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
- Let Escape fall through to the game's own cancel path; poll the window's
  "shown and fully ready" state rather than subscribing to visibility events, which fire
  before the captions are written.

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
`LoadingScreen.cs`. The input layer: [input.md](input.md). Value-widget patterns
(checkboxes, sliders, combo boxes, tabs, popups, key capture): [widgets.md](widgets.md).
The per-screen process (measure → model → approve → implement → verify → hand over):
[making-screens-accessible.md](making-screens-accessible.md).

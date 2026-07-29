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
current focus once per frame after everything settles.

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
  repeat reads where you land); screen-entry announcements queue.
- **Screens** (`Screen`/`ScreenManager`): poll-and-diff. Each screen declares `Key`, `Layer`,
  an `IsActive()` predicate re-evaluated every tick against live game state (window
  visibility + readiness gates + "is a sub-window covering me"), and `Build(GraphBuilder)`.
  The manager sorts active screens by layer, diffs against its stack, keeps one `GraphState`
  per live screen (a covered screen keeps its cursor), and speaks `ScreenName` on focus.
- **Input** (`ModInput` + bindings): exact-modifier chord matching (Ctrl+A must not also
  fire bare A — and releasing a modifier mid-hold must not convert the chord); key repeat
  implemented mod-side from the **OS typematic settings** (`SystemParametersInfo`, with
  fallbacks) so held-arrow cadence feels native; navigation actions repeat, buffer-review
  actions are one-shot. The whole layer stands down while the game's own text input owns the
  keyboard — find the game's authoritative "typing now" signal (ES2:
  `FocusedControl.IsKeyExclusive`, the same check the game's shortcut dispatcher uses).
  Polling `UnityEngine.Input` from your own `Update` is never blocked by the game — every
  game layer only reads the same static input state (verify once per game). Two consequences
  discovered on ES2's options screen, both general:
  - **The exclusivity signal conflates "the player is typing" with "some widget owns the
    keyboard."** When the *mod itself* parks the game's focus on a widget (to make the game
    swallow a key — see below), the stand-down check silences the mod too. Add a narrow
    ownership exemption — an injected predicate ("this focused control is mine"), never a
    type test — and do NOT exempt genuine capture/typing widgets: during a key-rebind
    capture the layer must stand down fully so arrows and Escape are bindable.
  - **One physical key, two listeners.** The mod cannot consume a key the game polls, so
    "the mod handled Escape" never stops the game also handling it. The deterministic fix is
    to make the *game* swallow the key through its own authority: give the game's focus
    system the widget the key concerns (a focused key-exclusive widget makes the game's
    dispatcher consume Escape itself — its own mouse flows rely on this). And never depend on
    same-frame ordering between the mod's handler and the game's: releasing focus in the
    same frame the game would have consumed the key re-opens the leak intermittently, so
    defer mod-side state changes that would un-arm the game's consume path by a frame.
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

## The confirmation-dialog screen

Games funnel confirmations through one shared message-box window (quit?, discard changes?,
countdown boxes). Make it a single high-layer screen registered once — every flow that dead-ends
in a confirmation then speaks for free, and a silent confirmation is a soft-lock for a blind
player. The shape (`src/graph-ui/MessageBoxScreen.cs`):

- **Top layer**, above every ordinary screen; ordinary screens must yield while a modal is
  visible so the hand-off is clean and their cursor survives underneath.
- Speak the composed question **once, on arrival** (via `ScreenName`), buttons as a row,
  message lines in the review buffer. Declare the buttons from **live visibility** each
  rebuild, never from the API's nominal shape — dialog windows get reused with leftover
  state from the previous dialog.
- **Text the game rewrites every frame** (countdown timers) must never feed node identity or
  per-frame announcements: a label-derived context id would re-announce the sentence every
  frame. Read it once at the screen boundary; the review buffer keeps it re-readable, frozen
  at the moment focus landed. No timer re-announcing.
- Let Escape fall through to the game's own cancel path; poll the window's
  "shown and fully ready" state rather than subscribing to visibility events, which fire
  before the captions are written.

## Default key bindings

Proven across wotr-access/SoC and adopted for ES2 (make rebindable eventually):

| Keys | Action |
|---|---|
| Arrows | Move (repeating); Left/Right adjust sliders, expand/collapse tree groups |
| Shift+Left / Shift+Right | Coarse adjust, ~10 increments (repeating) — see [widgets.md](widgets.md) |
| Tab / Shift+Tab | Cycle tab-stops, landing on the stop's remembered position |
| Enter | Activate (primary); on a key-binding row, start capturing the primary binding |
| Backspace | Secondary action; on a key-binding row, start capturing the secondary binding |
| Escape | Back / close |
| Home / End | First / last |
| Alt+Up / Alt+Down | Region jumps inside tables (repeating) |
| Ctrl+Up/Down, Ctrl+Left/Right, Ctrl+Home/End | Review buffer — see [buffers.md](buffers.md) |

Every new binding is approved by the project owner before it ships — a binding is UX surface
a screen reader user must memorize, and there are no "obvious defaults".

There is deliberately no tooltip key — see [tooltips.md](tooltips.md).

## Adapting to a new game

1. Verify the game has no usable focus-navigation system of its own first (ES2's AGE: none —
   focus is mouse-only; gamepad keycodes filtered out of the rebind UI). If one exists,
   consider piggybacking before building.
2. Wire the text pipeline: final localized display string + the game's own markup cleaner.
3. Wire activation: the deterministic handler a real click reaches (not synthesized input).
4. Wire screen predicates: visibility + the game's "fully shown and interactive" gate.
5. Add role words and state words to the localization table.
6. Ship the first screen, verify announcements via the dev server, then hand the human the
   perceptual test script (repeat cadence, interrupt feel, announcement shape).

## Source files

Engine (game-agnostic): [`src/graph-ui/`](src/graph-ui/) — `ControlId.cs`, `GraphTypes.cs`,
`KeyGraph.cs`, `GraphBuilder.cs`, `GraphAnnouncer.cs`, `TooltipParts.cs`, `GraphSheet.cs`,
`TypeAheadSearch.cs`, `TextUtil.cs`. Adapter exemplars (ES2-specific, models to imitate):
`GraphNavigator.cs`, `ControlTypes.cs`, `GraphNodes.cs`, `AgeText.cs`, `PointerFocus.cs`,
`ScrollIntoView.cs`, `Screen.cs`, `ScreenManager.cs`, `MainMenuScreen.cs`,
`DropListScreen.cs`, `MessageBoxScreen.cs`, and the input layer (`InputBinding.cs`,
`KeyboardBinding.cs`, `OsKeyboard.cs`, `InputAction.cs`, `UiActions.cs`, `ModInput.cs`).
Value-widget patterns (checkboxes, sliders, combo boxes, tabs, popups, key capture):
[widgets.md](widgets.md).

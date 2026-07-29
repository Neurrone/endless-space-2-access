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
  game layer only reads the same static input state (verify once per game).
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

## Default key bindings

Proven across wotr-access/SoC and adopted for ES2 (make rebindable eventually):

| Keys | Action |
|---|---|
| Arrows | Move (repeating); Left/Right adjust sliders, expand/collapse tree groups |
| Tab / Shift+Tab | Cycle tab-stops, landing on the stop's remembered position |
| Enter | Activate (primary) |
| Backspace | Secondary action |
| Escape | Back / close |
| Home / End | First / last |
| Alt+Up / Alt+Down | Region jumps inside tables (repeating) |
| Ctrl+Up/Down, Ctrl+Left/Right, Ctrl+Home/End | Review buffer — see [buffers.md](buffers.md) |

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
`Screen.cs`, `ScreenManager.cs`, `MainMenuScreen.cs`, and the input layer
(`InputBinding.cs`, `KeyboardBinding.cs`, `OsKeyboard.cs`, `InputAction.cs`, `UiActions.cs`,
`ModInput.cs`).

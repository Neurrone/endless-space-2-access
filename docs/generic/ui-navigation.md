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
identity and reconciliation is free — with one converse rule: **one object, one node**.
Reference identity is followed *before* the structural key, so two nodes sharing a backing
object are one control to the cursor, and focus teleports between the surfaces that share it
(ES2: a research-queue row and its tech-wheel node shared the technology wrapper; queueing a
technology threw focus into the queue panel). Where two surfaces show the same entity, at
most one carries the object reference — the other keys structurally. The sharper case is not
"the row moved" but **the
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
  `StateText` (spoken interrupting right after activate/adjust), `Sections` (ordered
  content blocks, each a live lines-func with a surfacing mode, from which the engine
  derives BOTH the tooltip announcement part and the review buffer — one declaration, two
  surfaces, so they cannot diverge; see [buffers.md](buffers.md) and
  [tooltips.md](tooltips.md)), focus-visual hooks. `ControlType` is a
  **registry value, not a class** — it owns the localized role word and the speak order of
  part kinds. wotr-access migrated off a class-hierarchy proxy system to this; don't
  reintroduce classes.
- **`GraphBuilder`** — the per-render DSL, in four parts:
  - **Two modes.** Menu mode (`StartRow`/`AddItem`) auto-wires rows and gives rows sharing
    a `rowKey` column-preserving vertical navigation; raw mode (`AddNode`/`Connect`) wires
    arbitrary topologies. The two may share a stop: the builder stitches each menu↔raw
    seam itself, and a seam is a ROW, not a node — every cell of the raw side's edge row
    reaches across. An adapter hand-wiring a seam it thinks is missing is the regression,
    not the fix.
  - **`PushContext`** adds non-focusable labeled levels (spoken once on entry via the path
    diff). Its id derives from its label, so sibling contexts sharing a drawn caption
    (ES2's lobby: seven competitor slots all captioned "AI") silently collapse in the path
    diff — disambiguate the label or key when the game repeats captions.
  - **Stops and regions — two tiers, one division of labor.** `BeginStop` partitions a
    screen into Tab-cyclable stops with remembered positions; regions subdivide a stop.
    Tab moves between panels (stops); the region jump (WotR: Ctrl+arrows; ES2 Access:
    Alt+Up/Down) moves between the sections *of the current panel*, landing on the
    section's first node. Regions are scoped to their stop by construction (`MoveRegion`
    filters on the stop key) — a screen that models its visual panels as stops has no
    region jumps between them, and that is the intended model, not a gap (WotR's
    inventory: five panels as stops; regions used inside a panel — quest groups within
    the journal list, action-bar segments).
  - **Expandable groups** give tree semantics to Left/Right, under two rules: `IsExpanded`
    is the cost gate — declaring children only when expanded is the whole of how a tree
    stays inside [performance.md](performance.md)'s bounded-rebuild rule — and the
    `OnExpand`/`OnCollapse` vtable hooks are **replacements** for the engine's own
    expansion bookkeeping, not add-on callbacks (they exist for adapters driving a
    retained game-side container that owns its expand state); a hook that only wants a
    side effect must flip the builder's expansion set itself, or the tree refuses to stay
    open.
- **Expansion changes no world state.** In a game whose view distance decides what is
  drawn, "go in" (tree) and "get closer" (camera) are different verbs, and binding a
  camera move to the expand key makes whichever information tier the other zoom level
  shows unreachable — when different zoom levels show different information, both sets
  must stay accessible, with the camera on its own explicit control (wherever fits the
  screen's design).
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
  the primary cell. It stamps each cell's column number on the node (`NodeVtable.Column`), so
  row-shaped subsystems — type-ahead's one-result-per-row filter, anything else that must
  tell a primary cell from its columns — ask the node instead of rediscovering table shape.
  Its node ids are private: a sheet names its first row (`FirstRow`, for the start node) and
  can be told to continue below a node the screen declared (`Follows`) — an adapter
  reconstructing a cell key is the defect those two exist to prevent.
- **`TypeAheadSearch`** — tiered fuzzy matching (ported to WotR from OniAccess), engine-side; the behavior contract is the "Type-ahead search" section below.

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
  [tooltips.md](tooltips.md), content sections per [buffers.md](buffers.md). **A factory
  whose signature omits a cross-cutting concern loses it screen by screen** — three
  unrelated screens shipped rows with buffers and no tooltip indication because two
  factories simply didn't take a tooltip parameter; nobody bypassed a rule deliberately.
  The strong fix is making the concern *derived* from one declaration so the omission is
  unrepresentable (what `Sections` did for tooltips); where a concern must stay a
  parameter, every factory takes it uniformly even when a control kind rarely uses it,
  and the review question is "which factories don't?", never "which screens forgot?".
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
- **Camera-follows-focus** is the same problem in a world or zoomable view, and gets the
  same shape of answer: when focus lands on something the camera is not looking at, move the
  view through the game's **own camera entry points** (its zoom/pan/locate calls), never by
  writing view offsets. The mechanism that keeps it from fighting anyone: **one coalesced
  request slot** — focus changes and tree expansion *write* the slot (last writer wins) and
  the per-frame update *applies* it only when no view animation is running. Never a move per
  keystroke, never interrupting a running animation, and a request for something already in
  view is a no-op. Verify with viewport rect probes: walking within an in-view group must
  leave the view bit-identical.
- **Discover controls, don't trust named references**: a reused window can carry duplicate
  per-skin control sets (ES2's options window has two complete button bars) with the API's
  named fields pointing at one skin — possibly the hidden one. And **no visibility flag can
  be trusted on its own**: containers hide whole ancestor chains while the child still
  reports visible, and pooled list containers do the reverse — surplus recycled children
  stay "visible" with zero alpha, so a flag-trusting sweep declares rows the player cannot
  see (example: ES2's competitor-slot pool kept an eighth slot alive this way after the
  count came back down). Zero alpha is only one retirement style: pools also park surplus
  children fully visible with stale content arranged outside the parent's extents, and retire
  rows by fading. So take counts and enumeration from the data the game BOUND, never from the
  widget pool, and gate every per-widget read on effective drawn-ness. Effective drawn-ness is
  the ancestor walk *plus* whatever render
  test the engine itself honours (alpha, clipping). And in a pannable or zoomable surface
  there is a third answer that is neither: **a cull** — `Visible` recomputed per frame from
  the screen rect, meaning "where the camera looks", not "what exists" (ES2's tech wheel
  culls every off-screen node; 11 of 107 read visible at any pan position). Enumerate such a
  surface by the model's own would-be-drawn flag, never the drawn one — and remember a
  renderer-composed tooltip cannot exist until the camera has brought its widget on screen,
  so reading one is a camera move plus a hover, in that order. Declare what is actually drawn — walk
  ancestors for visibility, test drawn-ness, filter decorative click-shields (no activation
  wiring), order by measured position so speech order matches the screen.

## Patterns proven since the port

- **Passive announcements** (things that change while no control is focused — loading
  progress, a page the game advances, the turn number): a per-frame watcher — the screen's own
  update where the source belongs to one page, a pump-scoped watcher where it does not —
  diffs a tuple of live source values and speaks QUEUED, never interrupting.
  A **textless** indicator (a spinner, a throbber, a colour swap) never shows up as a missing
  announcement in any text audit, so a census has to hunt zero-text windows and ask what
  boolean drives each one; that state belongs to a pump-scoped watcher, because the surface
  it lives on is often not a screen the player can be on at all. Baseline the diff when
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
  handling does not. **And name the handover gap**: between the opener standing down and
  the new screen's arrival gate passing there is an interval — frames, not instants (~4
  frames measured on ES2's improvements modal) — where NO screen is focused and the mod is
  deaf. Keys the mod consumed are protected across it by the input layer's release latch
  ([input.md](input.md)); keys it did not consume reach the game, so design modal opens
  knowing the interval exists rather than discovering it as a bug. But never
  stand down while merely *covered* — everything that hides your panel draws above your
  layer, and a screen that blinks out mid-transition hands the player to whatever is
  underneath for a frame (heard as a spurious announcement of the screen below). That
  reasoning holds only while your layer is *below* everything that can cover you: a screen
  placed at the top of the stack has nothing above it to blame, so it gates on its own
  surface's visibility instead. **Covered is not withdrawn**: an exclusive modal stack HIDES
  the window underneath (shown goes false), and there the screen below must stand down — ask
  the engine's own topmost-modal record which case you are in, never your window's flag; the
  two disagree exactly on the closing frame. A window on such a stack also voids any layer
  constraint against its stack-mates, which can never be up together. For
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
  transition safe. It is also the answer for a DEPARTING screen: the game disables a fading
  page's controls before the unbind, so live parts fire "unavailable" during the fade unless
  the screen declares nothing once it stops being shown. And an arrival the game LOSES may be finished for it: where part of a
  page's reveal is deferred to a coroutine or animation that can abort silently, re-issue
  the game's own show call — only after a settle long enough that a merely-slow arrival is
  never pushed, only while the precondition the game's own attempt needed is already
  satisfied, and then not again for a pause, because the re-issue is deferred too and the
  stalled state reads the same for a while after it. `src/graph-ui/Nudge.cs` is that
  discipline as an engine-free counter. **A mode with no window at all** (a strategic lens, a
  targeting cursor) has nothing to bind: its predicate flips frames before anything draws, so
  arrive on the mode's first drawn-and-operable surface and gate one-way on the mode itself
  thereafter — and a mode the game can drop the player into needs at least a watcher
  announcing entry and exit, or the player is in an unannounced world. While such a mode is
  armed, EVERY pointer gesture takes the mode's meaning — the primary click is the confirm,
  and if the game gives the mode a right-click (cancel, waypoint removal) that gesture's key
  follows it too; the mod's ordinary node actions and selection gestures must yield, or the
  keyboard does what the mouse could not. Two mechanics follow: the intercept belongs on the
  SCREEN, asked before the focused node's own handler — wiring it per node silently misses
  every node that has no such handler — and the mode's gesture is DELEGATED to the game's own
  per-mode handler rather than re-implemented, because the same button means cancel, undo one
  waypoint, or close a prompt depending on the mode, and one call site covers modes no
  fixture can even reach.
- **Tab wraps; initial focus still matters.** Tab cycles round both ways — a player who cannot
  see the panels reads a dead key as broken — and a page with one stop consumes the key
  silently. Wrapping does not excuse a bad landing: an ordering where the cursor starts
  mid-page still reads as arbitrary, so the start stop should be the first one. An explicit
  start node wins over the "land on the selected alternative" rule unless the start node is
  itself one of the alternatives (declares a selected-kind part).
- **Layers are static.** A screen's layer must never change while it is up: other screens
  (popups, confirmations) are placed *relative* to it, and a layer that slides underneath
  them cannot be reliably placed under either value. Number with gaps; when a window can be
  opened from pages at different layers, give it one number above the highest opener — and
  **below anything its own controls can raise** (a modal whose combo boxes open the shared
  drop-list popup must sit under the popup's layer). Getting that bound wrong is silent:
  the popup renders, but the wrong screen keeps focus. Before numbering a surface at all,
  look for the game's **own per-instance draw-order declaration** (a sorting-order field, a
  renderer bucket): where the engine already ranks its windows, the mod's numbers are copying
  a table that exists, and guessing contradicts it. And a screen placed above EVERYTHING is
  only safe where the player can dismiss or collapse it and the mod stands down on that —
  otherwise it buries every popup the game draws over it. A shared overlay strip (a
  collapsed-help bar, a persistent HUD fixture) is declared by the pages whose LAYOUT owns
  it — measured by where the game places the control, not by where its pixels remain
  visible: an overlay drawn over a modal is not thereby part of the modal's tab order.
- **A roster grid linearises.** A grid of cards (factions, loadouts, portraits) reads as
  one row per card in drawn order — left-to-right, top-to-bottom — not as a 2D table: the
  cells are peers of one kind, so column-preserving vertical moves buy nothing and the
  grid's wrap points are a rendering accident. A card's permanently-drawn description
  follows the always-shown-text rule ([making-screens-accessible.md](making-screens-accessible.md)
  §0); the card's substance lives in its buffer ([buffers.md](buffers.md)'s card example).
  A **sparse grid** is not a table either: when the game keeps the full lattice and hides most
  cells, column-preserving moves pair wrong across the holes — linearise the drawn cells and
  let the drawn headers become a walkable legend.
- **Tables read as tables**: one graph row per data row with a shared row key (Up/Down keeps
  the column), one node per cell announcing the drawn value alone — the column heading is
  spoken as the EDGE the player crosses to reach the cell, never repeated by the cell itself —
  and entering the table announces its role once. A cell is role-less text: a control type is
  two things, a reading order and a role word, and a metadata cell wants only the order —
  except a cell the game draws a real CONTROL into: that cell keeps the control's role word
  and click, and its availability is asked of the control, not of the row — and a control
  the screen reads itself must not ALSO be given the container's state part: a node's parts
  are additive, so a shared tail that repeats "unavailable" says it twice; the container
  adds only what the node did not answer. No
  position phrases inside a table — neither rows nor cells say "N of M"; the row identifies
  itself by name. Never drop an empty cell — the shared-column invariant dies — speak an
  "empty" word in it. A cell's review buffer holds that cell's own content (heading, value,
  the cell's own tooltip), not the whole row: the row is a walk away. `GraphSheet` (above) is
  the raw-mode engine for all of this — headings as edge labels, no auto positions, and the
  column stamp that type-ahead's one-result-per-row filter reads; a table built OUTSIDE it
  must stamp each cell's `Column` by hand, or searching matches every cell of every row. The
  drawn-header pairing is the adapter's job, by the game's own column names, never by index.
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
- **When one screen swaps whole PAGES in place**, announce the new page and blur the cursor so
  seating re-runs: reconciliation's nearest-survivor tier would otherwise keep a node of the
  old page alive and read its business over the new one.
- **A control the game wires to a screen you haven't modelled yet** is a named tradeoff,
  decided explicitly and reported: declare it read-only (the player loses an affordance but
  hears no dead end), or declare the action and accept that it opens a silent screen until
  that screen is modelled (the affordance kept, the dead end temporary). Either is
  defensible; choosing silently is not. Find such controls by grepping the declared controls'
  handlers for the engine's open-window calls — a gateway nobody noticed is a dead end the
  player cannot diagnose. And the "silent screen" arm has a cheap floor — a **minimum pass**
  where the page names itself on arrival, reads its drawn controls, and Escape is verified —
  so even a deferred screen is never an entry into silence.

## Type-ahead search

Typing finds controls. There is deliberately **no key that starts a search** — the first
printable character does (the input-layer cost of that choice — claiming the letter keys
from the game — is [input.md](input.md)'s typed-text rule). The behavior contract, re-derived
once from WotR source and now written down so the next game doesn't have to:

- **Scope = the focused Tab stop's declared nodes.** A tabular row contributes ONE result
  (non-primary columns are filtered out via the column number stamped on the node), and a
  landing returns to the column the search began on — searching never yanks the player out
  of the column they were scanning.
- **While a search is live**: Up/Down step the matches (with key repeat), Home/End jump to
  the first and last, Escape clears the search **and goes no further**, and any other
  action (Tab, Enter, an arrow when no results are up) ends the search and then does its
  ordinary job in the same press.
- **Each keystroke lands and re-reads**, so holding a letter reads the matches as they
  narrow. A keystroke that matches nothing drops the character and speaks a localized
  "no match for {text}" — never silence.
- **Staleness**: focus moving by any other means (a rebuild, the game, another key)
  invalidates the results silently. Never act on a result list computed for a cursor
  position that no longer holds; track where the last result landed and compare.
- **Screens opt out with two flags**: one for "raw keys are (or are about to be) handed
  elsewhere" — text editors, key-capture, including the deferred-handover frames — and one
  for "no type-ahead on this screen". Every raw-key reader consults the first flag; see
  [input.md](input.md).
- **A screen may supply its own search scope and landing action** — `(count, textOf(i),
  land(i))` — for items not declared in the graph (a collapsed tree's leaves). The landing
  callback does the screen's own work (expand the ancestors) and *returns the id to focus*,
  so the navigator still owns the announce-and-track step and the architecture's core
  invariant holds: a focus change is announced exactly once, by one code path.

## Gesture parity and child screens

The activation model is **mouse parity**: every gesture is one the game defines, and the
mod invents none. (ES2 Access shipped an action-menu system first and then deleted every
menu; the doctrine below is what replaced it, and it costs less than it looks — the game
already has an answer for each case.)

- **The activation key is the game's left click** on the focused thing. Where the click is
  destructive, the guard is the game's OWN confirmation flow (funnelled through the
  message-box screen) — never a mod menu in front of the click. A click the game answers
  with silence stays silent on the keyboard too.
- **A control's several actions are its DRAWN buttons, modeled as child nodes** — declared
  while visible, refusing (reason in the tooltip part) while disabled, absent while the
  game hides them. Two rules follow: a container with no drawn actions is a LEAF, never an
  expandable dead end; and a refused action is a declared-refusing node, not a missing one.
- **The alternate-activation chord is the game's modifier-click variant** where one exists
  — replay the click and let the game's handler read the physically held modifier — and
  nothing where the game has none.
- **A right-click command key** mirrors the game's right-click at the focused thing, given
  the current selection (move orders, zoom restore); its availability is computed on the
  press, not per frame.
- **Moving things — reorder, transfer — is the game's DRAG, modeled as a keyboard drag**
  (`src/graph-ui/Carry.cs`): one key holds (pick up / swap on a new source / put back on
  its own source), the activation key drops on a compatible target (overriding that
  target's click only while dragging; drop on the held item's own row is a cancel, matching
  the game's drag), Escape cancels — a mod-owned MODE takes the back key like a mod-owned
  surface does ([input.md](input.md)). Validate and commit through the game's drag path
  including its confirmations, and **read the game's drag handler for the landing rule** —
  which index `OnDragCompleted` posts and what the collection's `Move` does with it is the
  one thing an implementer guesses wrong. Where the mouse's gesture is "release over
  NOTHING" (drag out of the container to remove), there is no widget to drop on: declare an
  always-visible mod-authored drop-target node at the end of the container, labelled as a
  complete instruction, reading as a plain line while nothing is carried — discoverable
  before it is ever needed. **Say what the drag can do here, from the vtable, in the
  announcer**: a source appends "draggable" while nothing is held, a target appends "drop
  target" while something it takes is held, and a node that is both says only the drop word
  mid-drag. Derive both where the tooltip indication is derived, never per screen. The source
  word asks the pick-up command itself (which is why that command must be a pure QUERY), so
  it is never said on an empty slot; and a target family where some members refuse needs an
  acceptance predicate consulted by the INDICATION only — the drop still goes through the
  game's own check, whose refusal carries the game's reason for a player who presses anyway.
- **Child screens remain** (`PushChild`/`RemoveChild`, a single linear chain): the
  native-popup wrapper (game-focus handoff, deferred close to dodge the engine's Escape
  race) and the confirmation screen still need them. Per-screen state isolation returns
  focus to the opener for free. What no longer exists is the action-menu use of them.

## The confirmation-dialog screen

Games funnel confirmations through one shared message-box window (quit?, discard changes?,
countdown boxes). Make it a single high-layer screen registered once — every flow that dead-ends
in a confirmation then speaks for free, and a silent confirmation is a soft-lock for a blind
player. The shape (`src/graph-ui/MessageBoxScreen.cs`):

- **Top layer**, above every ordinary screen; ordinary screens must yield while a modal is
  visible so the hand-off is clean and their cursor survives underneath.
- The dialog follows the three-part heading contract
  ([making-screens-accessible.md](making-screens-accessible.md) §0): the drawn heading is a
  focusable node first in reading order, `ScreenName` carries the same words, and the start
  node is set **explicitly** on the question — the question is a **focusable text node**
  where focus lands on arrival, re-readable in place by refocusing, walkable in the review
  buffer — with the answers as a row below it, in **drawn order** (which button is left on
  screen is which button reads first). Declare the buttons from **live visibility** each
  rebuild, never from the API's nominal shape — dialog windows get reused with leftover
  state from the previous dialog.
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
3. Wire activation: the deterministic handler a real click reaches (not synthesized
   input) — through the game's ONE pressing helper. A screen growing a private copy of
   that wiring is a defect waiting: the copy misses what the helper has learned (handler
   arity, click replay) and fails silently.
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
`Carry.cs` (the keyboard drag), `LoadingScreen.cs`. The input layer: [input.md](input.md). Value-widget patterns
(checkboxes, sliders, combo boxes, tabs, popups, key capture): [widgets.md](widgets.md).
The per-screen process (measure → model → approve → implement → verify → hand over):
[making-screens-accessible.md](making-screens-accessible.md).

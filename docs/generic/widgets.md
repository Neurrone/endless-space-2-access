# Value widgets — checkboxes, sliders, combo boxes, tabs, key capture

How controls that _hold a value_ become operable, on top of the graph engine in
[ui-navigation.md](ui-navigation.md). A control that carries several _actions_ instead of a
value exposes them as its drawn buttons — the gesture-parity pattern in
[ui-navigation.md](ui-navigation.md).

## The vocabulary

One node factory per widget kind (see `src/graph-ui/GraphNodes.cs`), all built from the same
parts, all text `Func<string>` resolved at speak time:

- **Checkbox** — activate toggles; a **live** Value part speaks checked/unchecked, so a
  game-driven flip under the cursor announces itself.
- **Slider** — `OnAdjust` wired; the caller supplies the value already formatted the way the
  game _displays_ it (replicate the game's own number formatting — percentages, decimals —
  rather than inventing one).
- **Combo box** — closed, it speaks label + current value; activate opens the entry list
  (see below). Read the current value from the widget's _rendered_ label when one exists —
  it is already localized — not from the backing data table.
- **Tab** — carries a Selected part; the screen wires selection into focus (instant-switch
  tab bars) or activation, per design.
- **Radio button** — N in-place alternatives where exactly one is in force and picking is
  not doing (select-then-confirm dialogs, mode pickers). Only the chosen one speaks
  "selected" — it inherits the tab's silence-when-unselected rule — and its Selected part
  makes focus entering the group land on the current choice. Never model these as
  checkboxes: that promises an untick the game does not have. The tell in code is a refresh
  that re-derives EVERY sibling's state from one selected-name field: the widget's own
  `.State` is not authoritative there, which is also what makes flip-then-notify wrong for it
  — replaying the engine's click on the member already chosen unticks it until the panel's
  next refresh writes it back, a blink to a mouse and a spoken state change to a live Selected
  part. Picking SETS the game's selection; the flip is a transient.
- **Choice** — a popup entry: label + selected + position, deliberately **no role word**, so
  a 20-entry list doesn't say "list item" 20 times.
- **Edit field** — the game's own text editor, announced with an edit-field role word and its
  current text as the value. Activating it hands the engine's keyboard focus to the field (a
  spoken prompt says typing has begun — silence is indistinguishable from a broken key) and
  the mod stands down while the game edits; the game's own commit/cancel keys end the edit
  and the field re-reads its new text when focus returns. **The handoff must not happen on
  the activating key's frame** — the engine would deliver that same Enter to the field — see
  the late-frame rule in [input.md](input.md). The pending handover is also a flag every
  raw-key reader consults (input.md's typed-text rule) — typing meant for the field must
  never feed a type-ahead search, including on the deferral frames. Typed-character echo is
  the screen reader's own, not the mod's.
- **Step indicators** (page dots, carousel marks) — the game draws position as a row of
  marks, not text: declare each mark as a read-only page indicator carrying the Selected
  part (never invent a spoken "N of M" the game doesn't show), and if the game's marks are
  clickable, activating one jumps to that page.
- **A camera whose zoom changes the SUBJECT is a widget.** Where the zoom steps swap which
  data layer or which tier of entity the view draws (a strategic lens, an overlay mode), the
  camera holds a value and belongs in the graph as an adjustable — `OnAdjust` stepping the
  tiers, the landed tier spoken — because the wheel can be the only gesture the game bound
  to it, and then every tier but the current one is unreachable by keyboard. A camera that
  only changes how close the same subject is drawn stays a viewport (the camera-follows-focus
  and expansion-zoom rules in [ui-navigation.md](ui-navigation.md)).
- **Always-drawn text is always spoken** — a permanently drawn paragraph is part of the
  control's readout, never tooltip-ruled; the full rule and its discriminator live in
  [making-screens-accessible.md](making-screens-accessible.md) §0. The converse gate is
  drawn-ness, never emptiness: **a hidden label keeps whatever was last written into it**
  (and a never-shown one keeps the prefab's words), so a readout gated on "its text is
  non-empty" happily speaks the previous prompt's title or a net figure from before the
  panel closed — gate on the game's own drawn flag and let the words be whatever they are.
- **A leader-line callout names a relationship, not the widget it points at.** A caption the
  game draws with a line or arrow into a cluster ("Used Skill Points 4") is a statement about
  the cluster, so adopting it as the pointed-at widget's name gives every node in that
  cluster the same meaningless phrase; name those nodes from what they each are, and let the
  callout read as its own line where it is drawn.

Two announcement parts do the heavy lifting:

- **`StateText`** — the value re-spoken _interrupting_ immediately after activate/adjust.
  This is the synchronous feedback path that makes held-key adjustment feel right: each
  repeat speaks the new value ("99%", "89%") with no focus re-read. At a range end the value
  simply repeats — hearing the same number _is_ the boundary cue (same clamp-not-wrap rule
  as the review buffers). On a *refused* activation it must produce nothing (the silence
  rule below), and the enforcement is two-layered: the factory returns null while the
  control is disabled, **and** the navigator treats "spoke nothing" as no reason to
  re-baseline its live-value watch — an unconditional re-baseline there silently swallows
  the next genuine change.
- **`SelectedPart`** — a non-empty Selected part also marks the node as its stop's **landing
  node**: focus entering the stop lands there instead of on the first node. A tab bar lands
  on the active tab; a popup lands on the current value. Produce it on any "one of these is
  current" group.

## Replaying activations

Activation goes through the game's own deterministic handlers — and a real click is more
than the handler, so replaying takes four rules, each shipped as a bug first:

- **Toggles flip-then-notify**: replay a toggle the way its own click path runs — set the
  widget's state first, then invoke its wired switch handler, which reads the state it now
  finds. Calling the handler alone acts on the stale state — except where the handler ignores
  the state it is called with and a later refresh re-derives it (a radio group, above): there
  the pick sets rather than flips.
- **A handler the mod cannot replay**: when the game's own handler derives its value from
  the pointer (a slider whose click path reads the drag cursor), replay the handler's
  *tail* against an explicit index instead — and read the current index from the same
  source the game's own refresh reads, never from the widget.
- **The dispatch has an arity contract.** An engine's message-style dispatch can silently
  drop a call whose argument count the handler doesn't match (Unity's `SendMessage` with
  one argument is never delivered to a zero-argument method, and the mismatch is
  swallowed). Resolve the handler's parameter count before dispatching, and **verify a
  replayed activation by its effect, never by the absence of an error**.
- **A click is more than its handler.** The engine's dispatch does things around the
  handler that the handler knows nothing about — audio components on the widget (ES2: the
  click sound lives in an `AgeAudio` component the handler never touches), in other
  engines haptics or particle feedback. Replay everything the dispatch does, not just the
  handler, or keyboard users get a silently different interaction — and the tell is that
  nothing errors.

## Budget screens

**Never re-compute what the game already displays.** Point pools, costs and counts tempt
an adapter into deriving them from the model — and then keeping up with prerequisites,
level rules and DLC. Declare the game's own drawn totals as live readouts and let its own
click path enforce what may be picked.

## Disabled is a spectrum

- An unavailable control stays focusable and announces unavailable; activating it does
  nothing — **in silence**. Never re-speak the control's state after a swallowed
  activation: a checkbox answering Enter with "not checked" is indistinguishable from a
  successful uncheck. The player already heard "unavailable" on focus.
- Enablement is rarely one flag: control-level enable, transform-level enable and a
  disabled ancestor are separate answers, and no single one is what the player sees. Ask an
  effective-enablement helper that walks the chain, not the widget.
- A refusing control may not be disabled at all: games repurpose blocked buttons as
  "why not?" links — ES2's blocked Colonize stays clickable and jumps to the blocking
  technology instead. An action offered to the player (in a menu or on activation) must be
  gated on the game's own action predicate, never on visible-and-enabled.
- **State that answers later is its own case.** When activation posts an order or command
  the game applies frames later, the immediate re-read is stale by definition. The truthful
  feedback is the **live value part** announcing the result when it lands; the tempting
  alternative — speaking an optimistic prediction of the new state on the keypress — turns
  a rejected or reordered command into a confident lie. Say nothing extra on the press; let
  the live part say what actually happened. A page whose WHOLE content arrives after it opens
  owes an arrival announcement of its own, and that watcher arms per VISIT, not per observed
  transition — the answer can land before the page's first frame.

## Text that animates in

A label revealed by a typewriter or fade usually already holds every word: engines commonly
set the full string once and animate a draw cursor that only the renderer honours. Before
rebuilding a panel's phrasing from the model to "beat the animation", compare the label's
stored text with what the animation component was set up with — the words may all be there
from frame one.

## Charts

A drawn graph — bars as clipped rectangles, gauges as fill ratios — often carries no text
at all. Read the encoded values off the drawn geometry (fill percentages, clip heights),
announce the non-trivial series in one line, put every series in the review buffer, and
take the series names from the model's own ordered list: the bars themselves name nothing.
**Verify every encoded series in a state where it is non-zero.** A series drawn at zero
reads as zero whatever the arithmetic behind it is, so a fixture that only ever shows zeroes
cannot falsify a scale, origin or clip-height error — the gauge that read 163% in play had
passed exactly that check.

## Adjust granularity

Fine = one increment on Left/Right; coarse = ~10 increments on **Shift+Left/Right**, both
repeating, both clamped. Exact-modifier chord matching is what makes the chord safe (Shift+Left
must not also fire Left — the same mechanism that keeps Ctrl+Up off the plain-arrow path).
The coarse step falls back to a fraction of the range when the widget declares no increment.

## Popups as sub-screens

A widget's transient popup (a combo box's entry list) is its own **screen**, driven by
mod-side state: the parent screen records "this widget is open", the popup screen's
`IsActive` reads that state _and_ the live widget (window still up, popup still open — the
game closing it underneath must pop the screen cleanly). The parent screen stays in the
stack, covered, keeping its cursor for the return.

- Entries are Choice nodes; the current one carries Selected, so the popup opens on it.
- Open the game's **real popup** for visuals when it has one, via the widget's own open path;
  move the game's own highlight as focus moves; on cancel, restore the real selection's
  highlight. A sighted observer should not be able to tell the session from a mouse session.
- Escape closes the popup only — never the window under it. Getting that guarantee is an
  input-ownership problem, not a screen problem: see [input.md](input.md) (the Escape
  carve-out and the game-consumes-via-focus mechanism).
- Entries can be individually disabled: keep them focusable, announce unavailable, swallow
  activation — same rule as everywhere else.

## Key-rebind capture

Use the game's own capture widget — hand it the keyboard and let it scan; the mod only
manages the handover and the announcements. The flow that reads well: activate → speak a
prompt ("Press the new key combination."), silence while listening, the combo builds under
the player's fingers if the game live-updates its label, then the settled binding re-reads
through the row's live value part. Focus never moves, so nothing else speaks.

**The handover trap** (this WILL bite, in any engine): the keypress that _activated_ capture
is still held when the game's scanner starts, so the scanner sees your Enter — and if the
game commits on key-release, the activation key's own release ends the capture before the
player touches anything. Defer the handover: speak the prompt immediately, then poll until
**no key at all is held** before giving the game's widget focus. Unity footnote: the
"nothing held" frame is the _release_ frame, on which key-up events still fire — wait one
frame more (ES2 uses two consecutive clear frames).

While a capture is live the mod's input layer must be **fully stood down** — arrows and
Escape must be capturable as bindings, so the mod-ownership exemption
([input.md](input.md)) must not cover capture widgets. Cancel any pending handover if focus moves or the screen deactivates, and
on teardown restore the widget's displayed state _before_ releasing focus, so a hot reload
mid-capture binds nothing.

Conflict dialogs ("this key is already bound to X") arrive through the game's shared
confirmation window — if the dialog screen exists, the flow needs nothing extra.

## Source exemplars

`src/graph-ui/GraphNodes.cs` (the factories), `DropListScreen.cs` (popup-as-sub-screen,
focus handover, cancel-restore), `MessageBoxScreen.cs` (the confirmation-dialog screen —
see ui-navigation.md). Models to imitate, not copy: they name ES2 types.

# Value widgets — checkboxes, sliders, combo boxes, tabs, key capture

How controls that _hold a value_ become operable, on top of the graph engine in
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
- **Choice** — a popup entry: label + selected + position, deliberately **no role word**, so
  a 20-entry list doesn't say "list item" 20 times.
- **Edit field** — the game's own text editor, announced with an edit-field role word and its
  current text as the value. Activating it hands the engine's keyboard focus to the field (a
  spoken prompt says typing has begun — silence is indistinguishable from a broken key) and
  the mod stands down while the game edits; the game's own commit/cancel keys end the edit
  and the field re-reads its new text when focus returns. **The handoff must not happen on
  the activating key's frame** — the engine would deliver that same Enter to the field — see
  the late-frame rule in [input.md](input.md). Typed-character echo is the screen reader's
  own, not the mod's.
- **Step indicators** (page dots, carousel marks) — the game draws position as a row of
  marks, not text: declare each mark as a read-only page indicator carrying the Selected
  part (never invent a spoken "N of M" the game doesn't show), and if the game's marks are
  clickable, activating one jumps to that page.
- **Toggles flip-then-notify**: replay a toggle the way its own click path runs — set the
  widget's state first, then invoke its wired switch handler, which reads the state it now
  finds. Calling the handler alone acts on the stale state.

Two announcement parts do the heavy lifting:

- **`StateText`** — the value re-spoken _interrupting_ immediately after activate/adjust.
  This is the synchronous feedback path that makes held-key adjustment feel right: each
  repeat speaks the new value ("99%", "89%") with no focus re-read. At a range end the value
  simply repeats — hearing the same number _is_ the boundary cue (same clamp-not-wrap rule
  as the review buffers).
- **`SelectedPart`** — a non-empty Selected part also marks the node as its stop's **landing
  node**: focus entering the stop lands there instead of on the first node. A tab bar lands
  on the active tab; a popup lands on the current value. Produce it on any "one of these is
  current" group.

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

# Review buffers

Per-element and per-stream text the player reviews line by line, at their own pace, with
dedicated keys — instead of having verbose content forced through a one-shot announcement or
a modal reader. Origin: songs-of-conquest-access invented this shape (its commit history
records the pivot: "tooltips are no longer read automatically in favour of using the UI
buffer"); the guiding principle from its audit notes: *plan review buffers early so speech
output does not become a one-time unreadable dump*. Adopted for ES2 with identical semantics.

## The model

Several **named buffers**, not one global one and not one per widget:

- The **UI buffer** (always visible): holds the currently focused element's reviewable
  content. Repopulated on every real focus change, cursor reset to line 0.
- **Event-log buffers** (visible only while their owning screen is up; `FollowLatest`):
  rolling histories — SoC keeps map-notification and combat-event logs. These are the
  *second sink* of the same event stream that drives live narration: the announcer speaks an
  event once, the recorder also appends it for later review. Cleared at lifecycle boundaries
  (new combat, back to main menu).

Data structure (`ReviewBuffer`): trimmed non-empty `Lines`, a clamped `CurrentLineIndex`,
`ReplaceLines` (clear + refill, cursor → 0), `AppendLine` (cursor jumps to the end only when
`FollowLatest`), and `MoveFirst/Last/Previous/Next` returning
`Moved | BeginningOfBuffer | EndOfBuffer`. The manager registers buffers in order, computes
the visible set from the active screens (the UI buffer is always forced visible), cycles
only visible buffers, and snaps a `FollowLatest` buffer to its newest line when selected.

## Keys and announcement rules

All Ctrl-modified so they never collide with plain-arrow navigation, and all **one-shot**
(no auto-repeat — reading is deliberate):

| Keys | Action | Speaks |
|---|---|---|
| Ctrl+Up / Ctrl+Down | Previous / next line | The raw line text only — no "line n of m", no label prefix |
| Ctrl+Home / Ctrl+End | First / last line | Same |
| Ctrl+Left / Ctrl+Right | Previous / next visible buffer (wraps) | "{buffer label}. {current line}" — the only place a buffer's name is spoken |

- Ends **clamp, never wrap**; hearing the same line repeat *is* the boundary cue (no error
  tone, no boundary message).
- An empty buffer speaks a fixed "Buffer empty" phrase, not silence.
- Buffer speech is **queued, not interrupting** — navigation announcements interrupt, review
  must not stomp them.

## Population (the part that takes judgment)

The UI buffer is a **curated list of short lines**, not a text dump — the judgment lives in
what a section's lines should hold (tooltip content per [tooltips.md](tooltips.md), a
card's drawn face), not in assembling the buffer, which the engine does (mechanics below).
Long native text is split into lines at semantic boundaries — newlines first; if the game
composes a tooltip's content through a drawing interface, capture one line per drawing
call rather than one blob (SoC's fake details-drawer pattern).

The buffer holds the focused element's **own** content, never its container's: each dialog
control carries its own tooltip, not the dialog's shared body text (the body is a focusable
node with its own buffer); a table cell carries its heading, value, and the cell's own
tooltip, not the whole row (the row is a walk away). And the buffer mirrors what the game
shows for the element *in its current state*: a minimized notification the game draws as an
icon buffers its title, not the expanded description — the full text belongs to the opened
popup, where the game shows it.

**The buffer is the widget's face.** Populate it from what the drawn widget shows, never
from the model behind it: the same model value can be drawn as a number (readable) or as
pips/a rating, and reading the model then describes a control that does not exist on
screen. The face deliberately extends to the widget's own indicated tooltip: an indicated
tooltip must be readable from the buffer. That invariant holds **by construction** — the
indication and the buffer both derive from the same declared section
(`NodeVtable.Sections`, [ui-navigation.md](ui-navigation.md)), never from two separately
wired channels. Simulation state the widget doesn't draw stays out.

**The "card" worked example**: a control whose readout is just name + state and whose
entire substance lives in the buffer — type, traits, anomalies, outputs, refusal reasons.
Cards (unit portraits, planet cards, item tiles) are the cleanest instance of "the buffer
is where the rest of the control lives". A grid of such cards linearises to one row per
card in drawn order — the roster-grid pattern in [ui-navigation.md](ui-navigation.md).

Cursor rules, all load-bearing:

- Repopulate **only on a real focus change or when the element's spoken readout changed** —
  an immediate-mode UI rebuilds every frame, and a mere rebuild must NOT reset the review
  cursor (the player may be three lines deep).
- Any repopulation snaps the selected buffer back to the UI buffer — after moving focus,
  Ctrl+Down always reviews the thing you're on.
- Content resolves live at populate time, so game-appended text (disabled reasons) is
  current.

In the graph engine this hangs off `NodeVtable.Sections` — ordered content blocks (each a
live lines-func with a surfacing mode: a tooltip announced or indicated per the mode rule,
or buffer-only for an aggregate face) — and the engine composes the buffer itself: an auto
HEAD from the node's own readout (label + state words, so a node with **no** sections
already buffers correctly — its label lines, for free), then the sections in declared
order, with the first-line-duplicates-label dedup. The navigator's single focus-commit
site does the fill — one hook, no per-screen buffer code, and no screen or factory
constructs buffer content by hand.
Announcement parts of the tooltip kind are excluded from the auto head: the sections
already carry that content, and the "has tooltip" indicator is meta, not content.

## Reload safety

Buffers and their key handlers tear down with the mod; nothing about a buffer may outlive a
hot reload (SoC nulls and detaches in `OnDestroy`; ES2 resets in `ModEntry.Stop`).

## Source files

Engine (game-agnostic): [`src/buffers/`](src/buffers/) — `ReviewBuffer.cs`,
`ReviewBufferManager.cs` (+ tests in the mod repo). Adapter exemplars: `BufferController.cs`
(keys → manager → speech policy), `BufferActions.cs`; population lives in the navigator —
see [`src/graph-ui/GraphNavigator.cs`](src/graph-ui/GraphNavigator.cs).

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

The UI buffer is a **curated list of short lines**, not a text dump: the element's label,
its state words (disabled/expanded — as spoken), then its detail lines (tooltip content —
see [tooltips.md](tooltips.md)), with the first detail line dropped when it exactly
duplicates the label (native tooltips often repeat the control's name). Long native text is
split into lines at semantic boundaries — newlines first; if the game composes details
through a drawing interface, capture one line per drawing call rather than one blob (SoC's
fake details-drawer pattern).

Cursor rules, all load-bearing:

- Repopulate **only on a real focus change or when the element's spoken readout changed** —
  an immediate-mode UI rebuilds every frame, and a mere rebuild must NOT reset the review
  cursor (the player may be three lines deep).
- Any repopulation snaps the selected buffer back to the UI buffer — after moving focus,
  Ctrl+Down always reviews the thing you're on.
- Content resolves live at populate time, so game-appended text (disabled reasons) is
  current.

In the graph engine this hangs off `NodeVtable.DetailLines` (a `Func<IList<string>>`), and
the navigator's single focus-commit site does the fill — one hook, no per-screen buffer code.
Announcement parts of the tooltip kind are excluded from the buffer's state-word section:
details already carry that content, and the "has tooltip" indicator is meta, not content.

## Reload safety

Buffers and their key handlers tear down with the mod; nothing about a buffer may outlive a
hot reload (SoC nulls and detaches in `OnDestroy`; ES2 resets in `ModEntry.Stop`).

## Source files

Engine (game-agnostic): [`src/buffers/`](src/buffers/) — `ReviewBuffer.cs`,
`ReviewBufferManager.cs` (+ tests in the mod repo). Adapter exemplars: `BufferController.cs`
(keys → manager → speech policy), `BufferActions.cs`; population lives in the navigator —
see [`src/graph-ui/GraphNavigator.cs`](src/graph-ui/GraphNavigator.cs).

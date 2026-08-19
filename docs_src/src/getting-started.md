# Getting Started

## Screens 

Every screen is divided into stops. A stop is one panel of the game: a list, a table, a strip of buttons, the galaxy map, a card's detail pane.

- `Tab`: next stop
- `Shift+Tab`: previous stop
- `Up` / `Down` / `Left` / `Right`: navigates items in a stop, `Left` and `Right` also collapses and expands grouped content or adjusts sliders
- `Shift+Left` / `Shift+Right`: a coarse step on an adjustable item
- `Home` / `End`: first and last item of the stop
- `Alt+Up` / `Alt+Down`: previous and next region of a stop

## Acting on things

- `Enter` or `Numpad Enter`: left click, this is the primary confirmation gesture
- `Ctrl+Shift+Enter`: Alt-click — on queues, this is "queue this at the head". (`Alt+Enter` itself belongs to Unity, the game's engine: it switches between fullscreen and windowed, and nothing the mod does can take it back)
- `Ctrl+Enter`: Ctrl-click — adds one item to or removes it from the game's own selection, locate missing technology
- `Shift+Enter`: Shift-click — extends the selection to here
- `Ctrl+Alt+Enter`: double click — the game's second click, which some controls (a ship tile, a table row, a designer module) need
- `Backslash`: right click, which in this game is a command rather than a menu
- `Ctrl+Backslash`: Ctrl+right-click
- `Escape`: back
- `Backspace`: move to previous location on the galaxy map

## Drag and drop

The game moves construction queue lines, populations, ships, modules and battle cards by dragging. On the keyboard that is two keys:

1. Focus what you want to move and press `Space` to pick it up. It says what it is carrying.
2. Move to where it should go — anywhere, including another stop or another panel.
3. Press `Enter` to drop it. Where the target refuses, you hear the game's own reason and keep carrying.
4. `Space` again on the original place puts it back; `Space` on another carriable item swaps.
5. `Escape` puts it down and does nothing else.

Dragging a line within its own list is how you reorder a queue — there is no separate reorder key. A drop always lands the item at the target's own position: "Moved Settler to position 2".

Where nothing can be picked up and nothing is being carried, Space belongs to the game, which is what keeps its own Space (the scan mode) working on the map.

## Editing text

A text box is announced as "editable", or "numeric editable" for the stepper boxes whose Left and Right adjust a value.

Editing is explicit, so that arrow keys keep working as navigation until you ask for the keyboard:

1. `Enter` on the field hands the keyboard to it: "editing".
2. Typing echoes each character. `Backspace` speaks the character it deleted. Arrows, `Home` and `End` speak the character the caret lands on.
3. `Enter` commits: "edited". The screen stays where it is — committing an edit never presses the screen's Save, Rename or Confirm button, which is a separate control.
4. `Escape` cancels: the text you started with is restored and you hear "Cancelled". A second Escape then closes the screen.

The one exception is the chat box, whose Enter sends the message, because that is the game's own behavior.

## Type-ahead search

There is no search key. Type a letter and the focused stop starts searching:

- keep typing to narrow it
- `Up` / `Down`: previous and next match
- `Home` / `End`: first and last match
- `Escape` or `Backspace`: ends the search — "Search cleared" — and does nothing else, so neither key also acts on the row you landed on
- any other command ends the search and then does its own job

A search is re-typed in a keystroke, which is why Backspace ends it rather than editing it. Some screens hand the letters to the game instead: the key-capture rows in Options, and cinematics where any key skips.

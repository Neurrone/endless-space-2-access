# Getting Started

## Screens are made of stops

Every screen is divided into stops. A stop is one panel of the game: a list, a table, a strip of buttons, the galaxy map, a card's detail pane.

- `Tab`: next stop
- `Shift+Tab`: previous stop

Both wrap, so Tab from the last stop lands on the first. On a screen with only one stop the key does nothing, because coming back to where you already are is not a move.

A stop only exists while the game is drawing something at it. Where a screen has no fleets and no notifications, Tab finds nothing there — there are no empty placeholders to walk past.

## Inside a stop

What a stop holds is a tree. Some rows are plain items, some are groups you can open.

- `Up` / `Down`: previous and next item
- `Right`: open a group, or increase the value of an adjustable item
- `Left`: close a group, or decrease the value
- `Shift+Left` / `Shift+Right`: a coarse step on an adjustable item
- `Home` / `End`: first and last item of the stop
- `Alt+Up` / `Alt+Down`: previous and next region, where a stop is divided into sections

Each item announces its name, its state, its role, and where it sits in the list ("3 of 12"). Tables announce the row you have arrived in, and the column heading you crossed as you step sideways, so a cell never has to repeat its own caption.

## Acting on things

`Enter` is the game's left click, on whatever the cursor is standing on. That includes the destructive ones: Enter on a queued technology dequeues it, Enter on a construction line cancels it — behind the game's own confirmation where the game asks for one.

The rest of the family are the game's other mouse gestures, and each means only that:

- `Enter` or `Numpad Enter`: left click
- `Alt+Enter`: Alt-click — on the queues, this is "queue this at the head"
- `Ctrl+Enter`: Ctrl-click — adds one item to or removes it from the game's own selection, and on a control the game has left switched on only to explain itself, this is the jump to the missing technology
- `Shift+Enter`: Shift-click — extends the selection to here
- `Ctrl+Alt+Enter`: double click — the game's second click, which some controls (a ship tile, a table row, a designer module) need
- `Backslash`: right click, which in this game is a command rather than a menu
- `Ctrl+Backslash`: the game's Ctrl+right-click
- `Escape`: back, or close, wherever the game closes something

The three modified left clicks fall back to the plain click where a screen wires nothing of their own, because you are physically holding the modifier and the game's own handler is what decides what that means. Backslash and the double click stay silent where the control has no such command: pressed speculatively, a cue on every one of them would be noise.

`Backspace` is a second command, not a right click. It exists for the few places where one control of the mod stands for two of the game's — the key-rebinding rows in Options, where Enter captures a binding's first key and Backspace its second — and for a command that belongs to a whole panel rather than to one row, like stepping back down the starlanes you have travelled on the galaxy map.

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

## When something happens by itself

The mod speaks events you did not cause: the new turn, a fleet's route being cancelled or intercepted, a save being written, the camera being moved for you ("Shown on the map"), the zoom changing. These are queued rather than interrupting, except where you have just pressed a key, in which case what you asked for comes first.

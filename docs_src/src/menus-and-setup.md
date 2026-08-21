# Menus and Setup

## The Main Menu

The main menu and the pages reached from it — the first-launch disclaimer, the credits, the DLC browser, the mod manager, the asset exporter, and Join Game — are all navigable. `Escape` backs out of each one, except the disclaimer, which only its Accept button closes.

The DLC browser has its tabs, items and buttons, and remembers the tab you were on. The mod manager's Confirm reloads the game's runtime, so expect the game to restart itself.

## Options

The options screen has its tabs, the rows of the current tab, and the buttons at the bottom. Every kind of setting is a control: combo boxes open a drop list, sliders take `Left` and `Right` with `Shift` for a coarse step, text fields are edited explicitly, and checkboxes toggle with `Enter`.

The key-rebinding rows are the one place `Backspace` has a meaning of its own: `Enter` on a row captures the binding's first key and `Backspace` captures its second. While the game is waiting for a key capture, letters go to the game rather than starting a search.

## Starting a Game

The new game lobby has the setup rows — galaxy shape, size, difficulty, the competitors, the victory conditions — plus its action buttons. Each competitor is a named band of rows: entering one says `Player 1`, `Player 2`, and so on, counted down the panel (your own empire's panel is separate and uncounted), and `Alt+Up` / `Alt+Down` jump straight between players. Inside a band sit that player's name, faction, difficulty and colour — and in multiplayer, the join, kick, lock and ready controls the game draws for the slot, between the faction and the colour.

The **faction chooser** opens over the lobby, with the faction cards, the custom-faction entry, the hulls, the traits, the faction description, and buttons.

The **custom faction editor** opens over the chooser: your faction's details, its setup, its population, the available traits, the selected traits, and buttons. The point budget is read as you spend it.

When the game offers a tutorial choice, that window has its choices and buttons.

## Loading and Saving

The load/save screen has the save list and the command buttons. The list is a table: press `Up` from the first row to reach the column headings, where `Enter` sorts. `Ctrl+Alt+Enter` (double click) on a row loads the save or saves over it, behind the same confirmation the Load and Save buttons raise.

The game shows no text while saving, so the mod announces when a save starts and finishes.

The loading screen announces what the game is doing and its rough progress, in quarters. A battle's loading screen also reads the caption telling you the game is waiting for a keypress.

## In-Game Menus

`Escape` on a map view opens the game menu, which has its menu items, the game settings panel and the timer settings panel.

The end-game journal lists your finished games, one row each. `Enter` on a row opens its score screen. `Escape` there returns to the main menu, which is the game's own behavior.

## Chat

Press `Ctrl+Tab` to open chat. This is the game's own chat key, which the mod rebinds at startup so that `Enter` and `Tab` stay navigation keys. If you rebind chat in the game's options, your binding opens it instead.

The chat panel opens over the current page, and the page underneath is unreachable while it is up. `Tab` cycles inside the panel. Focus lands on the message box without typing in it.

- The recipient tabs (Global, Alliance) choose who the next message goes to.
- The message log holds the newest fifty messages, newest first. Older messages are in the Chat [review buffer](buffers.md).
- `Enter` on the message box starts typing: `Chat. Type a message…`
- `Enter` in the box sends the message.
- The first `Escape` leaves the box with focus on its row; the second closes chat and returns you to the control you left.

Incoming messages are spoken. In multiplayer, joins, leaves, kicks, host changes, renames and the launch countdown are spoken too, because the game posts them as chat messages.

Multiplayer itself is untested with this mod. The chat panel works in single player; the Alliance tab and the new-message button only appear in a session with other players.

# Menus and Setup

## The main menu

The main menu and the pages that replace it — the disclaimer you accept on first launch, the credits, the DLC browser, the mod manager, the asset exporter, and Join Game — are all navigable. Escape backs out of each one, except the disclaimer, which only its own Accept button closes because the game swallows every other key there.

The DLC browser walks its tabs, its items and its buttons, and remembers the tab you were on. The mod manager's Confirm reloads the game's runtime, so expect the game to restart itself.

## Options

The options screen walks its tabs, the rows of the tab you are on, and the buttons at the bottom. Every kind of setting the game has is a control: combo boxes open a drop list as a screen of its own, sliders take `Left` and `Right` with `Shift` for a coarse step, text fields are edited explicitly, and tick boxes toggle with `Enter`.

The **key-rebinding rows** are the one place `Backspace` means something of its own: `Enter` on a row captures the binding's first key and `Backspace` captures its second. While the game is waiting for a key capture, letters go to the game rather than starting a search.

## Starting a game

The new game lobby walks the setup rows — galaxy shape, size, difficulty, the competitors, the victory conditions — plus its own action buttons. Each competitor is a row of its own.

The **faction chooser** opens over the lobby and walks the faction cards, the custom-faction entry, the hulls, the traits, the faction description, and its buttons.

The **custom faction editor** opens over the chooser: your faction's details, its setup, its population, the traits available, the traits you have selected, and the buttons — with the point budget read as you spend it.

Where the game offers a tutorial choice, that modal walks its choices and its buttons.

## Loading and saving

The load/save screen walks the save list and the command buttons. The list is a table: `Up` from the first row reaches the column headings, where `Enter` sorts. A row's double click (`Ctrl+Alt+Enter`) acts rather than shows — it loads the save, or saves over it — behind the same confirmation the game raises for the Load and Save buttons themselves.

A save in flight is the one thing the game reports with no words at all, so the mod announces it starting and finishing.

The loading screen has nothing to operate — a load happens to you — so it exists only to say what the game is doing and roughly how far along it is, in quarters. A battle's loading screen also reads out the caption that tells you the game is waiting for a keypress.

## In-game menus

Escape on a view level opens the game menu, which walks its menu, the game settings panel and the timer settings panel.

The end-game journal lists the games you have finished, one row each, with the score screen behind a row's Enter. Escape there returns you to the main menu rather than to the journal, which is the game's own behavior.

## Chat

`Ctrl+Tab` opens chat. It is the game's own chat key, which the mod moves off Enter and Tab at startup so those keys stay navigation; if you rebind chat in the game's options, your binding is what opens it.

Chat is a **place**, not a stop: the panel opens over whatever page you were on, Tab cycles inside it, and the page underneath is unreachable while it is up — exactly what open chat does for a mouse. The cursor lands **on** the message box rather than typing in it.

- the recipient tabs (Global, Alliance) decide who the next message goes to
- the message log is a group of the newest fifty messages, walked newest first; the review buffer's Chat buffer keeps the rest
- `Enter` on the box hands the keyboard over: "Chat. Type a message…"
- `Enter` in the box sends, because that is the game's own behavior for this one field
- the first `Escape` steps out of the box, leaving the panel drawn with the cursor on the box's own row; the second `Escape` closes chat and hands you back to the control you left

Messages arriving are spoken, and in a multiplayer session so are the joins, leaves, kicks, host changes, renames and the launch countdown, because the game posts those as chat.

Multiplayer itself is untested with this mod. The chat surfaces work in single player, where the panel is live; the alliance tab and the new-message button only appear in a session that has other players in it.

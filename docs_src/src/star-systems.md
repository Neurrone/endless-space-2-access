# Star Systems

Opening a system takes you out of the galaxy view and onto that system's own page. Two routes reach it: `Right` on the zoom control past the map's closest step, which opens the page for one of your own colonies and merely zooms in on anything else; and a double click (`Ctrl+Alt+Enter`) on the system's row in the empire screen's table. `Escape` leaves, and the zoom's `Left` steps back out as well.

## The system management page

Tab walks five panels:

- **Page**: what the system as a whole is — its name and the rename button, its owner, its approval and politics, the improvements it has, the buttons the game draws across the top. Readouts with no control behind them still carry their tooltips, so the numbers behind a figure are in the review buffer.
- **Planets**: one card per planet, with its type, size, outputs, anomalies and the actions available on it — colonize, terraform, and whatever the game offers this turn. A card's buttons are child nodes: `Right` on the card opens them.
- **Constructibles**: what you can build here, with the filter group above the list. The filters are one choose-one set, so `Enter` on a filter switches to it.
- **Queue**: the construction queue. `Enter` on a line cancels it — instantly while nothing is invested, and behind the game's own confirmation once something is. `Alt+Enter` on a constructible queues it at the head. Buy-out buttons are child nodes of the line.
- **Hangar**: the ships parked here, with the toolbar the game draws over them. "No ships in the hangar" where it is empty.

Closing any modal opened over this page puts the cursor back on the planets panel.

### Reordering the queue

Queue order is a drag, not a separate key:

1. `Space` on the line you want to move.
2. `Up` or `Down` to the line whose place it should take.
3. `Enter` to drop: "Moved Settler to position 2".

### Moving population

Where the system allows it, a planet's population is dragged the same way: `Space` on the population you are moving, walk to the destination planet, `Enter` to drop. A refusal speaks the game's own reason and you keep carrying.

## Improvements

The improvements modal opens from the management page and walks a summary, the list of improvements, and its action buttons. Each row carries its full description in the review buffer.

## System politics

The system politics modal opens from the management page too: a heading, the parties with their support, and the events that have moved them. "Show all events" expands the list.

## The planet page

Going one step further in — `Right` on the zoom from a system, or `Enter` on a planet card — opens one planet's own page, with three panels: the planet's information, its population, and the card itself with the actions on it. Stepping between planets re-enters the same page with a new planet, and the mod re-reads it.

A planet's own constructibles panel slides out from under its card and is a screen of its own while it is open.

## Renaming

The rename box walks its heading, the text field, Cancel and Confirm. `Enter` on the field starts editing, `Enter` again commits the text — and then Confirm is what actually renames the system, because committing an edit never presses a screen's button for you.

## Discovering a system

The first time you reach a system, the game plays its discovery cinematic. It is read out as it goes, and any key skips it, exactly as the game's own "press anything" does.

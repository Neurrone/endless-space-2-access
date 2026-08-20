# Galaxy View

The galaxy map is the main screen of the game. 

## The HUD

The panels the game draws over every view are separate Tab stops, the same on the galaxy, in a star system and on a planet:

- **Empire banners**: your empire's numbers, laid out in rows — the screen icons, the running totals, the research line, the stockpiles. `Up` and `Down` move between rows, `Left` and `Right` along a row. The totals are read-only; the breakdown behind each number is in its tooltip, readable in the review buffer. The screen icons open their screens with `Enter`.
- **View title**: the name of what you are looking at, the zoom slider, and the scan toggle.
- **Pinned quest**: the quest you are tracking and its current step.
- **Tutorial bar**: when you collapse a tutorial popup, this bar holds its title, a close button, and a button to reopen it.
- **Notifications**: the icon strip. `Enter` opens a notification; `\` dismisses it.
- **Turn controls**: End Turn and the controls the game draws with it. A new turn is announced automatically.

## The Systems Tree

The map is exposed as a list grouped by constellation. The top level holds one entry per constellation you have explored, anything drifting in open space at its own position, and an **Unexplored constellation** group at the end, holding every discovered place whose constellation the game has not named to you yet. A constellation becomes explored — and gains its own named group — once one of its systems has been fully explored. Everything is in map reading order: north to south, then west to east; the groups sort by their centre.

A constellation entry reads the game's name for it and no coordinate. Its details are the game's own constellation information: who controls it, who first fully discovered it, your progress toward control ("You control 1/3 Star Systems needed to own this constellation"), and the constellation's control bonus. `Right` opens a constellation without moving the camera; `Left` collapses it, and if the camera was flown into one of its systems, it comes back out.

Inside a constellation sit the star systems and special objects such as nebulae and dust clouds.

Each entry speaks its name followed by its coordinates, a pair of whole numbers measured from your home system. Home is `0, 0`; `-11, 11` is eleven units south and eleven units east of it.

After the coordinates, a system reads its status: colonized or outpost, population, current construction, ground battles, citadels, how many fleets are under way nearby, and so on. Special objects read their type, such as `Solar Nebula`. Full details are in the [review buffer](buffers.md).

- `Right`: expand a system. This also zooms the camera in, because the game only draws a system's contents up close. An expanded system lists its planets, then the starlanes leaving it, then the fleets present.
- `Left`: collapse the system and zoom back out. If you have moved the camera elsewhere since expanding, the camera stays where it is.
- `Enter`: left-click the focused item.
- `\`: right-click the focused item.

While an action is waiting for a target — launching a probe, firing an obliterator — the map panel names itself with the game's instruction instead of "Map", so tabbing out and back re-reads what the game is waiting for. `Enter` on a system, a starlane or a planet confirms the target there; the game refuses an illegal target silently and the mode stays up. `\` cancels the mode.

### Starlane Travel

Press `Right` on a starlane whose far end you have explored to travel down it: focus moves to the destination system's entry, that system expands, and the camera follows. `Enter` on the lane travels too, when nothing else wants the key: with a targeting mode armed it confirms the target at the lane, and with a fleet selected it puts the fleet down, as the game's own click does.

Press `Backspace` to go back. Each hop is remembered; going back puts focus on the starlane you travelled from and returns the camera. Systems that were only expanded because you travelled into them are collapsed on the way back; systems you expanded yourself are left alone.

### Fleets

A fleet parked at a system is listed under that system. A fleet in transit is listed under its destination system, with the starlane it is on and its direction. A fleet whose destination you cannot see gets a top-level entry of its own.

### Regions and Search

Use `Alt+Up` and `Alt+Down` to jump between the systems and anything drifting in open space. These keys do nothing while nothing is drifting.

Type to search the whole map, including fleets inside systems you have not expanded.

## Zoom

The game's own keyboard zoom (`PageUp` and `PageDown`, held down) stops working inside a star system, so the mod also exposes the zoom as a slider next to the view title: press `Tab` to the view title, then `Down` to reach it.

- `Right`: zoom in one level
- `Left`: zoom out one level
- `Shift+Right` / `Shift+Left`: jump a whole layer band — a run of zoom levels that draw the same layer of the map

The slider announces its position and the layer band the camera is on, for example `Zoom, slider, 10 of 15, Systems`. The 15 levels run from the whole galaxy through the map camera's thirteen steps, then into a star system's own page, and finally onto a single planet's page — the same range the mouse wheel covers. The bands, from furthest out: Galaxy map, Informative galaxy, Constellation, Systems, System and System Overview; the very furthest step and the two page levels add no band word.

In the normal galaxy view, the zoom level controls how much detail the game draws. Zoom changes are announced wherever you are.

## The Game's Scan Mode

Press `Space` on the map, or `Enter` on the scan button next to the view title, to toggle the game's scan mode. This is the game's own strategic overlay: the camera stays where it is and every label on the map is replaced with a different set.

In scan mode, zoom does two jobs. It still controls how much the game draws, and it also selects the **lens** — what kind of information the labels show. Four lenses span the galaxy zoom range, from widest to closest: diplomacy, trade, economy and system overview. Two more appear as you zoom further in: the system management lens inside a system, and the planet lens on a planet.

A single zoom step can change what the numbers on screen mean, so the mod announces the lens when you enter scan mode and again at every layer boundary you cross.

Scan mode has three panels: the title (with the lens toggle and the zoom), the content the current lens draws, and the legend — the game's own explanation of the colours and symbols the lens is using. If your empire has trade routes, a Trade routes group appears at the end of the content panel, one entry per starlane, giving the number of routes and how many are blockaded.

Press `Escape` or `Space` to leave. The mod announces the view you return to.

## Inspect Mode

Press `Ctrl+I` on the map to enter inspect mode: a square cursor you move around the galaxy to hear what is in each area.

- `Arrows`: move the cursor by exactly its own size, so no area is skipped or heard twice
- `Shift+Arrows`: move to the next cell in that direction that differs from where you are — in what it contains or in how much of it is unexplored. Cells identical to the one you left are skipped, and the mod says how many (`Skipped 12 squares`) before reading the landing. If nothing differs all the way out, you land on the last cell before the map edge
- `Alt+Left` / `Alt+Right`: travel along what the cursor holds. On a cell whose only feature is a single starlane, jump to the system at its end: `Alt+Left` goes to the first system in the lane's announcement (the western end of a fully explored lane, or the one known end of a lane running into unexplored space), `Alt+Right` to the second. On a cell with fleets in transit, `Alt+Right` jumps to their destination instead, when the map shows one and every moving fleet in the cell is going to the same place — a single fleet's destination wins over the lane it is flying. The keys are silent when the answer would be ambiguous or the target is unexplored, and they never exit the mode
- `+` / `-`: grow or shrink the cursor through 1, 3, 5, 7, 9 and 11 units
- `Enter`: move tree focus to the object in the cursor. Does nothing if the cursor holds nothing, or more than one thing
- `Escape`: exit, announced as `Exited inspect mode`. The camera returns to where you entered the mode

Each cell speaks its coordinates, then its contents: systems and special objects, fleets, probes, obliterator missiles, ally pins, any starlanes crossing the cell, and how much of the cell is unexplored (`Unexplored`, or `34 squares unexplored`). An empty cell speaks only its coordinates. Moving past the edge of the galaxy announces `Map edge`.

While inspect mode is active, the review buffer holds the current cell's contents, and the mod draws a visible square around the area being inspected.

Type-ahead search also works while the cursor is up: typing letters searches the map's entries as usual and moves tree focus to the match. The inspect cursor stays where it is. While a search is open, the first `Escape` clears only the search (`Search cleared`) and leaves you in inspect mode; the next `Escape` exits the mode.

## The Scanner

The scanner is a directory of everything on the map, sorted nearest first from wherever you are reading. It is not a mode: there is nothing to enter or exit, and its keys work whenever focus is on the map, alongside normal navigation and inspect mode.

- `Ctrl+PageUp` / `Ctrl+PageDown`: previous / next category
- `Shift+PageUp` / `Shift+PageDown`: previous / next subcategory
- `Alt+PageUp` / `Alt+PageDown`: previous / next result, wrapping at both ends
- `Alt+Home`: jump to the current result

The categories are systems, fleets, probes, quest markers, ally pins and obliterator missiles. The systems category has subcategories: all, friendly, neutral, enemy, homeworld, minor factions and special. Empty categories and subcategories are skipped, so a press always lands on something.

Changing category announces the full scope and the result you land on, for example `Systems: friendly, Dusay, 0, 0, here, 1 of 2`. Changing subcategory announces the subcategory and the result. Stepping through results announces the result alone: name, coordinates, distance and direction, and position in the list, for example `Heka, -1, -9, 9 south, 1 west, 2 of 13`. The very first scanner press of a game announces where you already are without moving.

`Alt+Home` moves tree focus to the result's own entry, expanding whatever it is inside. While inspect mode is active, it moves the inspect cursor to the result's coordinates instead.

Results are re-sorted by distance on every press, measured from where you are currently reading — the inspect cursor if that mode is active, otherwise the focused entry. Leaving the map suspends the scanner without resetting it. Plain `PageUp` and `PageDown` remain the game's own zoom keys.

### Scanner or Scan Mode?

The names are similar but the features are different:

- The **scanner** belongs to the mod. It changes nothing on screen, has no state to enter or leave, and answers questions like "where is the nearest enemy fleet" while you carry on reading the map.
- **Scan mode** belongs to the game. It repaints the whole map, and the zoom level decides what kind of information the labels show.

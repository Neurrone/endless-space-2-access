# Galaxy View

The galaxy is where a turn starts and ends, and it is the busiest screen in the mod. Tab walks its panels: the empire banners, the name of the view you are in with the zoom beside it, the pinned quest, a collapsed tutorial bar, the notification strip, the turn controls, and the map itself. While a fleet is selected, that fleet's own panels join the ring.

## The systems tree

The map stop is one list of every place the map has named for you — star systems and the phenomena that sit between them — in map reading order: from the north down, and west to east across each row.

Every row says its name and then its coordinates, as a pair of whole numbers measured from your home system. Home is `0, 0`; `-11, 11` is eleven units south and eleven east of it. One spoken unit is one galaxy unit, so the pairs can be compared with each other and with the length of a starlane.

After the pair, a system says what is going on there in the game's own terms — colonized or an outpost, its population, a ground battle, a citadel, how many fleets are under way nearby, and for a phenomenon what kind it is ("Solar Nebula", "Collapsing Star"). The full dossier is in the review buffer.

`Right` opens a system, and brings the camera in at the same time, because the map only draws a system's contents once it is close: from far off a planet is a circle with a name, and from as close as the game goes it is a card with its outputs and its anomalies. Inside a system you get its planets, then the starlanes leaving it, then the fleets that are there.

`Left` closes it again and takes the camera back out — unless you have since moved the camera somewhere else yourself, in which case nothing is moved.

`Enter` is the map's left click on a system, and `Backslash` its right click.

### Travelling the starlanes

A starlane is a leaf, not a branch. `Right` on a lane whose far end the map has named **travels**: the cursor lands on that system's own row at the top of the stop, that system opens, and the camera goes there. It answers "what is down this line" by going, rather than by making you walk back through a list of a hundred systems.

`Backspace` is the way back. Each hop is remembered, and the key puts the cursor on the very lane it was pressed on, with the camera back where it started. A system that was only opened because you travelled to it is closed again on the way out; one you opened yourself is left as you left it. An empty trail is silent.

Travelling is not a click: it posts no order and it will not confirm a targeting mode you have armed. A lane running into unexplored space is a silent leaf.

### Fleets in the tree

A fleet parked at a system hangs under that system. A fleet **under way** hangs under the system it is flying **to**, and nowhere else, because the map draws where a fleet is going rather than where it came from. A lane fleet also says which of that system's lanes it is on and in which direction. A fleet whose destination the map has not named gets a row of its own at the top level.

### Regions and search

The systems stop is one region: `Alt+Up` and `Alt+Down` jump between the stars and whatever is drifting in the open space between them, and do nothing at all while nothing is drifting.

Typing searches the whole map, including fleets buried inside systems you have not opened.

## Zoom

The game's own keyboard zoom is two keys held down, and it does nothing at all once the game is inside a system, which leaves a lot of the game out of reach. So the mod offers the zoom as a control of its own, in the same row as the name of the view you are looking at — `Tab` to the view title and `Down` or `Right` reaches it.

It is an ordinary adjustable: `Right` moves in, `Left` moves out, and it announces "Zoom, slider, 10 of 15". The ladder runs from the whole galaxy through the thirteen steps of the map camera, then into a star system's own page, then onto one planet's — the same journey the mouse wheel makes.

`Shift+Left` and `Shift+Right` jump a whole band rather than one step, where a band is a stretch of the ladder that draws the same layer of the map. On a thirteen-step ladder a fixed ten-step jump would be the entire range, so the coarse step follows the map's own layers instead.

In the normal view, the zoom decides how much detail the game draws. Wherever you are standing, a zoom change is announced.

## The game's own scan mode

`Space` on the map — or `Enter` on the scan button beside the name of the view — turns on the game's strategic lens. It is a mode rather than a place: the camera stays where it is and the game repaints every label on the map with a different set.

In scan mode the zoom does two jobs. It still decides how much the game draws, and it also **chooses the lens**. Four lenses sit across the galaxy ladder, from the widest view inwards: diplomacy, then trade, then economy, then the system overview. Two more are reached by going further in — a system's management lens as you enter a system, and the planet lens on one planet.

Because a zoom step can silently change what the numbers mean, the mod announces the lens on arrival and again at every band boundary you cross, so you are never reading trade figures believing them to be diplomacy.

The stops in scan mode are the title (with the lens toggle and the zoom), the content the live lens draws, and a **legend** stop, which is the game's own key to the colours and symbols that lens is using. Where the empire has trade routes, a "Trade routes" group is the last thing in the content stop, one row per lane, saying how many routes cross it and how many of them are blockaded.

`Escape`, `Space`, or `Enter` on the lens toggle, leaves — and the mod announces the view you are back in.

## Inspect mode

`Ctrl+I` on the map arms a spatial cursor: a square of galaxy that you move around with the arrow keys and hear the contents of. It is the answer to "what is over there", which the tree cannot ask.

- `Arrows`: move the cell by exactly its own size, so the cells tile — nothing is skipped and nothing is heard twice
- `+` and `-`: grow and shrink it through 1, 3, 5, 7, 9 and 11 units
- `Enter`: land the tree cursor on the one thing in the cell (silent where there is nothing, or more than one thing)
- `Escape`: leave — "Exited inspect mode" — and the camera goes back to where the mode was armed

Each cell speaks its coordinate pair and then what the map draws there: systems and phenomena, fleets, probes, obliterator missiles, ally pins, the starlanes that cross the cell even when neither of their ends is inside it, and how much of the cell is unexplored ("Unexplored", or "34 squares unexplored"). An empty cell says its pair and nothing more. Pushing past the galaxy's edge answers "Map edge".

Arming it only works while the cursor is on the map, and pressing `Ctrl+I` again while it is up does nothing — it is not a toggle, so a speculative press mid-sweep cannot cost you the cell you were standing on. Tabbing away **suspends** the mode rather than ending it: the other stops behave exactly as if no cursor were armed (which matters, because the zoom slider lives on the arrow keys), the cell and its size are kept, and coming back to the map reads the cell out again.

While the mode is up, the review buffer holds the cell's reading rather than the control you left, and the mod draws a square on screen around the cell for anyone watching.

## The scanner

The scanner is the mod's own directory of what is on the map: a distance-sorted list of things, nearest first, measured from wherever you are reading. It is **not a mode** — there is nothing to arm, nothing to exit, and Escape never touches it. Its keys are live whenever the cursor is on the map stop, alongside tree navigation and alongside inspect mode.

- `Ctrl+PageUp` / `Ctrl+PageDown`: previous and next category
- `Shift+PageUp` / `Shift+PageDown`: previous and next subcategory within it
- `Alt+PageUp` / `Alt+PageDown`: one match at a time, wrapping at both ends
- `Alt+Home`: go to what the scanner is pointing at

The categories are systems, fleets, probes, quest markers, ally pins and obliterator missiles. Systems carry subcategories: all, friendly, neutral, enemy, homeworld, minor factions and special. A category or subcategory with nothing in it is skipped, so a press always lands on something.

A category step names the whole scope and then what you landed on — "Systems: friendly, Dusay, 0, 0, here, 1 of 2". A subcategory step names the subcategory and the landing. A step through the list says the landing alone: name, the coordinate pair, how far away and in which direction, and "3 of 13". Zero distance collapses to "here". The very first scanner press of a game says where you already are and moves nothing.

`Alt+Home` moves the tree cursor onto that thing's own row, opening whatever branch it is buried in — or, while inspect mode is up, moves the inspect cell onto its coordinates.

Leaving the map suspends the keys and resets nothing, so the next press resumes the sweep where you left it. Plain `PageUp` and `PageDown` stay the game's own keyboard zoom.

### Scanner or scan mode?

They sound alike and do entirely different things.

- The **scanner** is the mod's. It changes nothing on screen, has no state to enter or leave, and answers "where is the nearest enemy fleet" while you carry on reading the tree.
- **Scan mode** is the game's. It repaints the whole map, and the zoom decides which lens — that is, what the labels mean — you are reading.

## The HUD

The clusters the game draws over every view level are stops of their own, and they are the same on the galaxy, in a star system and on a planet:

- **Empire banners**: what the empire is worth, laid out in the rows they are drawn in — the screen icons, the running totals, the research line, the stockpiles. Up and Down move between rows, Left and Right along one. The totals are readable but not clickable; the stat block behind each number is in its tooltip, which means in the review buffer. The screen icons do open their screens.
- **View title**: the name of what you are looking at, the zoom, and the scan toggle.
- **Pinned quest**: the quest you are tracking, and its current step.
- **Tutorial bar**: a tutorial popup you have collapsed leaves a bar here — its title, its close button, and the arrow that brings it back.
- **Notifications**: the icon strip. `Enter` opens a notification, `Backslash` dismisses it, which is what the game puts on a right click.
- **Turn controls**: End Turn and what the game draws with it. A new turn is announced without anyone standing on the button, because the turn changing happens to you rather than being done by you.

Notification popups are ordinary screens: they walk their top strip, their body and their bottom buttons, tables inside them read as tables, and the "+" that some reports draw over a hidden detail panel is a checkbox you tick to grow the body.

# Galaxy View

## Coordinates

Coordinates are spoken with your home system as the origin. For major factions that don't have an origin home system, the bottom left of the map is used as the reference point until a home system is established.

Coordinates are in the form `(x, y)` where x is east and `y` is north.

## The Systems Tree

The map is exposed as a hierarchical tree.

- Constellations are collections of star systems
- Star systems contain actions, planets, fleets, resource deposits, star lanes and wormholes
- Planets contain curiosities and their possible actions

Use `Right` or `Left` to expand or collapse the current item.

Use `Alt+Up` and `Alt+Down` to move between regions to efficiently find something.

Systems, planets and other entities have tooltips. There are also nested tooltips as well, which are exposed as children that can be read individually.

Press `Enter` or `Right` to follow a star lane or wormhole. Use `Backspace` to move to the previous location after traversing a wormhole or star lane.

The map can be zoomed in and out to show different levels of detail. The mod manages this for you to ensure that when expanding a system, its contents have their full tooltips rendered.

To get a birds eye view of the map, zoom out to the "Systems" or "Systems and fleets" level. At that zoom level, expanding a system does not change the zoom level so the system's contents only contain star lanes, wormholes and fleets.

### Fleets

A fleet parked at a system is listed under that system. A fleet in transit is listed under its destination system, with the starlane it is on and its direction. A fleet with an unknown destination gets a top-level entry of its own.

## Scan Mode

Press `Space` on the map, or `Enter` on the scan button next to the view title, to toggle the game's scan mode. This is the game's own strategic overlay. This should not be confused with the mod's scanner tool.

In scan mode, zoom does two jobs. It still controls how much the game draws, and it also selects the lens - what kind of information the labels show. Four lenses span the galaxy zoom range, from widest to closest: diplomacy, trade, economy and system overview. Two more appear as you zoom further in: the system management lens inside a system, and the planet lens on a planet.

Press `Escape` or `Space` to leave.

## Inspect Mode

Use inspect mode to understand geometry. This is critical as systems can require off-lane travel (i.e, travel through open space). Inspect mode is also required to understand spheres of influence around systems.

Press `Ctrl+I` on the map to enter inspect mode. In this mode, the cursor announces the contents of a 1 by 1 square of the map. Use `+` and `-` to grow and shrink the size of the cursor. Use the arrow keys to navigate and `Shift` with the arrow keys to move to the next interesting tile in that direction.

When the cursor is on a star lane, `Alt+Left` and `Alt+Right` travels to either end.

When the cursor is on a fleet, `Alt+Right` moves to its destination if known.

`Backspace` returns to the previous location before a jump.

To exit inspect mode:

- Press `Enter` on a location that contains an entity. If a location can be resolved unambiguously, you are returned to the systems tree view focused on that entity
- Press `Escape` to return to the systems tree view at your previous location

## The Scanner

The scanner finds things of interest that you have already explored. This works in the systems tree and inspect mode. It also respects the current zoom level.

- `Ctrl+PageUp` / `Ctrl+PageDown`: previous / next category
- `Shift+PageUp` / `Shift+PageDown`: previous / next subcategory
- `Alt+PageUp` / `Alt+PageDown`: previous / next result
- `Alt+Home`: jump to the current result
- `Backspace`: return the cursor to the previous location before the jump

## Bookmarks

Bookmarks let you save locations of interest and return to them later. There are ten bookmark slots, using the number keys `1` through `0`. This works with both the systems tree and inspect mode.

- `Shift+number`: saves the current position to that bookmark slot, overwriting any existing bookmark. There is no separate delete command
- `Ctrl+number`: jump to that bookmark slot. Note that this does not require the map to be focused
- `Backspace`: return the cursor to the previous location before the jump

## Jump To Capital System

Use `Ctrl+C` to jump to your capital system. This acts like any other bookmark without taking up a slot.

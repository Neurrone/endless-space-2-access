# Command Reference

This page summarizes the mod's keys in one place. Keys the game itself owns are marked as such.

## Global

| Key                     | Action                                                     |
| ----------------------- | ---------------------------------------------------------- |
| `Tab`                   | Next panel (wraps)                                         |
| `Shift+Tab`             | Previous panel (wraps)                                     |
| `Enter`, `Numpad Enter` | Activate — the game's left click                           |
| `Escape`                | Back or close; drops a carried item first if one is held   |
| `Ctrl+Tab`              | Open chat (the game's own chat key, moved here at startup) |

## Trees and Panels

| Key                          | Action                                                                      |
| ---------------------------- | --------------------------------------------------------------------------- |
| `Up` / `Down`                | Previous / next item                                                        |
| `Right`                      | Expand a group, or increase an adjustable value                             |
| `Left`                       | Collapse a group, or decrease an adjustable value                           |
| `Shift+Right` / `Shift+Left` | Coarse increase / decrease                                                  |
| `Home` / `End`               | First / last item of the panel                                              |
| `Alt+Up` / `Alt+Down`        | Previous / next region within the panel                                     |
| `Ctrl+Shift+Enter`           | The game's Alt-click (queue at the head) — `Alt+Enter` itself is Unity's fullscreen switch |
| `Ctrl+Enter`                 | The game's Ctrl-click; on a blocked control, jump to the missing technology |
| `Shift+Enter`                | The game's Shift-click (extend the selection)                               |
| `Ctrl+Alt+Enter`             | The game's double click                                                     |
| `\`                          | The game's right click                                                      |
| `Ctrl+\`                     | The game's Ctrl+right-click                                                 |
| `Backspace`                  | A control's or panel's second command                                       |
| `Space`                      | Pick up, swap, or put back a draggable item                                 |
| `Enter` (while carrying)     | Drop the carried item                                                       |

## Editing and Search

| Key                       | Action                                                  |
| ------------------------- | ------------------------------------------------------- |
| Any letter or digit       | Start or extend a type-ahead search on the focused panel |
| `Up` / `Down`             | Previous / next search match                            |
| `Home` / `End`            | First / last search match                               |
| `Escape`, `Backspace`     | End the search (`Search cleared`)                       |
| `Enter` on a text field   | Start editing (`editing`)                               |
| `Enter` while editing     | Commit (`edited`); the screen stays open                |
| `Escape` while editing    | Cancel and restore the original text (`Cancelled`)      |
| `Backspace` while editing | Delete a character, spoken as it is deleted             |

## Review Buffers

| Key          | Action          |
| ------------ | --------------- |
| `Ctrl+Up`    | Previous line   |
| `Ctrl+Down`  | Next line       |
| `Ctrl+Left`  | Previous buffer |
| `Ctrl+Right` | Next buffer     |
| `Ctrl+Home`  | First line      |
| `Ctrl+End`   | Last line       |

## Galaxy Map

| Key                          | Action                                                   |
| ---------------------------- | -------------------------------------------------------- |
| `Right` on a system          | Expand it and zoom the camera in                         |
| `Left` on a system           | Collapse it and zoom back out                            |
| `Right` on a named starlane  | Travel to the system at the far end                      |
| `Backspace`                  | Go back along the starlanes you travelled                |
| `Enter` on a system          | The map's left click                                     |
| `\` on a system              | The map's right click                                    |
| `Left` / `Right` on the zoom | Out / in one of the 15 zoom levels                       |
| `Shift+Left` / `Shift+Right` | Out / in a whole layer band                              |
| `Space`                      | Toggle the game's scan mode (the game's own key)         |
| `PageUp` / `PageDown`        | The game's keyboard zoom, held down (the game's own key) |

## Scanner

Available while focus is on the map.

| Key              | Action                     |
| ---------------- | -------------------------- |
| `Ctrl+PageUp`    | Previous category          |
| `Ctrl+PageDown`  | Next category              |
| `Shift+PageUp`   | Previous subcategory       |
| `Shift+PageDown` | Next subcategory           |
| `Alt+PageUp`     | Previous result            |
| `Alt+PageDown`   | Next result                |
| `Alt+Home`       | Jump to the current result |

## Inspect Mode

| Key                            | Action                                                 |
| ------------------------------ | ------------------------------------------------------ |
| `Ctrl+I`                       | Enter inspect mode (only on the map; not a toggle)     |
| `Arrows`                       | Move the cursor by its own size                        |
| `+` (or `Shift+=`, keypad `+`) | Grow the cursor — 1, 3, 5, 7, 9, 11 units              |
| `-` (or keypad `-`)            | Shrink the cursor                                      |
| `Enter`                        | Move tree focus to the object in the cursor            |
| `Escape`                       | Exit and re-centre the camera                          |

## Fleet Orders

| Key                                | Action                                             |
| ---------------------------------- | -------------------------------------------------- |
| `Enter` on a fleet                 | Select it and open its panel                       |
| `Ctrl+Enter` on a fleet            | Add it to or remove it from the selection          |
| `Shift+Enter` on a fleet           | Extend the selection to it                         |
| `\` on a destination               | Send the selected fleets there                     |
| `Ctrl+\` on a destination          | Send them by free movement instead of starlanes    |
| `\` on the lane a fleet is flying  | Stop at the next system                            |
| `Enter` while a mode is armed      | Confirm the target                                 |
| `\` while a mode is armed          | The mode's right click — cancel                    |
| `Space` then `Enter`               | Carry a ship to another fleet                      |

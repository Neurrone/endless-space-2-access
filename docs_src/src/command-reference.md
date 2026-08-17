# Command Reference

Every key the mod binds, in one place. Keys the game itself owns are marked as such.

## Global

| Key                    | Action                                                    |
| ---------------------- | --------------------------------------------------------- |
| `Tab`                  | Next stop (wraps)                                         |
| `Shift+Tab`            | Previous stop (wraps)                                     |
| `Enter`, `Numpad Enter` | Activate — the game's left click                          |
| `Escape`               | Back or close; puts down a carried item where one is held  |
| `Ctrl+Tab`             | Open chat (the game's own chat key, moved here at startup) |

## Trees and Stops

| Key                              | Action                                                      |
| -------------------------------- | ----------------------------------------------------------- |
| `Up` / `Down`                    | Previous / next item                                        |
| `Right`                          | Open a group, or increase an adjustable value                |
| `Left`                           | Close a group, or decrease an adjustable value               |
| `Shift+Right` / `Shift+Left`     | Coarse increase / decrease                                   |
| `Home` / `End`                   | First / last item of the stop                                |
| `Alt+Up` / `Alt+Down`            | Previous / next region within the stop                       |
| `Alt+Enter`                      | The game's Alt-click (queue at the head)                     |
| `Ctrl+Enter`                     | The game's Ctrl-click; on a blocked control, jump to the missing technology |
| `Shift+Enter`                    | The game's Shift-click (extend the selection)                 |
| `Ctrl+Alt+Enter`                 | The game's double click                                       |
| `Backslash`                      | The game's right click                                        |
| `Ctrl+Backslash`                 | The game's Ctrl+right-click                                   |
| `Backspace`                      | A control's or panel's second command                         |
| `Space`                          | Pick up, swap, or put back what is being carried              |
| `Enter` (while carrying)         | Drop what is being carried                                    |

## Editing and Search

| Key                     | Action                                                        |
| ----------------------- | ------------------------------------------------------------- |
| Any letter or digit     | Start or extend a type-ahead search on the focused stop        |
| `Up` / `Down`           | Previous / next search match                                   |
| `Home` / `End`          | First / last search match                                      |
| `Escape`, `Backspace`   | End the search ("Search cleared") and do nothing else           |
| `Enter` on a text field | Hand the keyboard to the field ("editing")                      |
| `Enter` while editing   | Commit ("edited"); the screen stays open                        |
| `Escape` while editing  | Cancel, restoring the text you started with ("Cancelled")       |
| `Backspace` while editing | Delete a character, spoken as it goes                         |

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

| Key                          | Action                                                        |
| ---------------------------- | ------------------------------------------------------------- |
| `Right` on a system          | Open it and bring the camera in                                |
| `Left` on a system           | Close it and take the camera back out                          |
| `Right` on a named starlane  | Travel to the system at the far end                            |
| `Backspace`                  | Step back down the starlanes you have travelled                 |
| `Enter` on a system          | The map's left click                                            |
| `Backslash` on a system      | The map's right click                                           |
| `Left` / `Right` on the zoom | Out / in one rung of the 15-rung ladder                         |
| `Shift+Left` / `Shift+Right` | Out / in a whole layer band                                     |
| `Space`                      | The game's scan mode (the game's own key)                       |
| `PageUp` / `PageDown`        | The game's keyboard zoom, held down (the game's own key)         |

## Scanner

Live while the cursor is on the map.

| Key               | Action                        |
| ----------------- | ----------------------------- |
| `Ctrl+PageUp`     | Previous category             |
| `Ctrl+PageDown`   | Next category                 |
| `Shift+PageUp`    | Previous subcategory          |
| `Shift+PageDown`  | Next subcategory              |
| `Alt+PageUp`      | Previous match                |
| `Alt+PageDown`    | Next match                    |
| `Alt+Home`        | Go to the current match       |

## Inspect Mode

| Key                          | Action                                              |
| ---------------------------- | --------------------------------------------------- |
| `Ctrl+I`                     | Arm the cell cursor (only on the map; not a toggle)  |
| `Arrows`                     | Move the cell by its own size                        |
| `+` (or `Shift+=`, keypad `+`) | Grow the cell — 1, 3, 5, 7, 9, 11 units            |
| `-` (or keypad `-`)          | Shrink the cell                                      |
| `Enter`                      | Land on the one thing in the cell                    |
| `Escape`                     | Leave, and re-centre the camera                       |

## Fleet Orders

| Key                            | Action                                                      |
| ------------------------------ | ----------------------------------------------------------- |
| `Enter` on a fleet             | Select it and open its panel                                 |
| `Ctrl+Enter` on a fleet        | Add it to or remove it from the selection                     |
| `Shift+Enter` on a fleet       | Extend the selection to it                                    |
| `Backslash` on a destination   | Send the selected fleets there                                 |
| `Ctrl+Backslash` on a destination | Send them by free movement instead of the starlanes          |
| `Backslash` on the lane a fleet is flying | Stop at the next system                              |
| `Enter` while a mode is armed  | Confirm the target                                             |
| `Backslash` while a mode is armed | The mode's own right click — cancel, or one waypoint back    |
| `Space` then `Enter`           | Carry a ship to another fleet                                  |

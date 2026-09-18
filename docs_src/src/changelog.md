# Changelog

## Unreleased

- The end-of-game scores and empire chronicles screens are now accessible
- Improve performance of screens with tables to fix sluggishness when navigating on the empire systems tab
- Tables now support `Ctrl+Alt` with the arrow keys to move to the first or last column of the current row, or to the first or last row in the column. This is mostly for the systems table as there are 20 keypresses required to move from the first to the last column
- The bookmarks tab in mod settings now supports importing bookmarks from the clipboard
- Added a help tab to mod settings with links to the mod homepage and Discord

## V0.2.1

- Fix column positions being lost when moving down from column headers if the table has a column with multiple icons
- Standardize how planets are read in the galactic map, empire and system management screens. This should fix various inconsistencies where a piece of information was read out in one screen but not another even when visible
- When starting a drag in the empire or system management screens, fixed the drag not being cancelled when no possible drop targets exist after a subscreen transition
- Planets in the empire and system management screens now summarize population information in their buffers
- Political support history information is now presented as a table in the senet screen
- Documented `Ctrl+M` hotkey in the galactic map. This reads the dimensions of the galaxy and describes where your home system is relative to galactic centre
- Expose the players popup in end turn controls
- The names of major factions are now consistently used where the game draws faction icons. For example, "Kappa (AI) (Riftborn)" instead of "Kappa (AI)"
- Notifications now animate instantly when opened so the mod can read them immediately. This also fixes initial focus when expanding a notification landing on the action buttons instead of the notification's content
- Added support for the forced truce, metaplot conclusion, new unlocked content, metaplot finished and displacement report notifications

## V0.2.0

The game should be largely playable now including space and ground combat. This is now beta quality and ready for wider playtesting.

- Added accessibility for numerous screens
- UI: removed unneeded uses of horizontal rows for a less confusing experience
- Added hotkeys to move to various parts of the UI, the mod tells you what they are
- `Ctrl+M` when focus is in the galactic map summarize the map's dimensions
- `Alt+Left` and `Alt+Right` moves to the previous or next system on the system management screen, or notification when a notification is open
- Provide better instructions when executing fleet actions
- When selecting a direction to send an exploration probe, information about unexplored tiles is now provided
- The panels on the system management screen are a single tab stop for easier navigation, use `Alt+Up` or `Alt+Down` to move through them
- Fixed numerous bugs where the camera and the mod's focus weren't in sync in the galaxy view
- A turn log raises notifications for events the game does not natively notify for such as enemy fleets moving in sight or systems discovered. This tells you what a sighted player would already see
- Added `Ctrl+L` shortcut to move to the location of a notification
- Non-interrupting notifications now speak their titles as they occur
- A mod settings screen can be accessed from the main or pause menus
- Usage hints automatically speak when the focused element has additional available actions, enalbed by default
- Add scanner for finding various categories of items with support for custom categories
- Add support for rebinding mod commands
- Tooltips that draw additional tooltips on hover are now exposed as nested tooltips
- All tooltips speak automatically, the automatic announcement of long tooltips can be disabled in mod settings
- `Ctrl+I` in the galactic map enables inspect mode to explore the geometry of the map. Use `Alt+Left` or `Alt+Right` to follow a fleet or a star lane to its source / destination. Use `+` or `-` to change the cursor size, and shift with the arrow keys to move to the next interesting tile. `Enter` exits inspect mode and returns to the tree view with focus on where your cursor was when inspecting. `Escape` exits inspect mode and restores focus to your previous position.
- The system's influence radius is indicated in its buffer and influence is read in inspect mode
- `Ctrl+C` jumps to your capital system
- Up to 10 bookmarks can be set by `Shift+1` through `0`. Use `Ctrl+1` through `0` to jump to a bookmark. This works with inspect mode. Jumping to bookmarks does not require focus to be on the map.
- All 69 cutscene videos are described as they play. Descriptions are spoken in the gaps between the video's own dialogue.

## V0.1.1

- Fixed failures to run on Gog because the game renames its `Galaxy` class to avoid conflicts with the Gog Galaxy DLL

## V0.1.0

Initial public release

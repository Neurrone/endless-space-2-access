# Changelog

## V0.2.0

The game should be largely playable now including space and ground combat. This is now beta quality and ready for wider playtesting.

- Added accessibility for numerous screens
- UI: removed unneeded uses of horizontal rows for a less confusing experience
- Added hotkeys to move to various parts of the UI, the mod tells you what they are
- `Ctrl+M` when focus is in the galactic map summarize the map's dimensions
- `Alt+Left` and `Alt+Right` moves to the previous or next system on the system management screen, or notification when a notification is open
- Improved instructions when executing fleet actions
- When selecting a direction to send an exploration probe, information about unexplored tiles is now provided
- The panels on the system management screen are a single tab stop for easier navigation, use `Alt+Up` or `Alt+Down` to move through them
- Fixed numerous bugs where the camera and the mod's focus weren't in sync in the galaxy view
- A turn log raises notifications for events the game does not natively notify for such as enemy fleets moving in sight or systems discovered. This tells you what a sighted player would already see
- Added `Ctrl+L` shortcut to move to the location of a notification
- Non-interrupting notifications now speak their titles as they occur
- Added usage hints to various controls at the bottom of the UI buffer
- Add scanner for finding various categories of items with support for custom categories
- Added a "Mod Settings" screen, accessed from the main or pause menu to configure custom scanner categories and remap commands
- Tooltips that draw additional tooltips on hover are now exposed as nested tooltips
- `Ctrl+I` in the galactic map enables inspect mode to explore the geometry of the map. Use `Alt+Left` or `Alt+Right` to follow a fleet or a star lane to its source / destination. Use `+` or `-` to change the cursor size, and shift with the arrow keys to move to the next interesting tile. `Enter` exits inspect mode and returns to the tree view with focus on where your cursor was when inspecting. `Escape` exits inspect mode and restores focus to your previous position.
- The system's influence radius is indicated in its buffer and influence is read in inspect mode
- `Ctrl+C` jumps to your capital system
- Up to 10 bookmarks can be set by `Shift+1` through `0`. Use `Ctrl+1` through `0` to jump to a bookmark. This works with inspect mode. Jumping to bookmarks does not require focus to be on the map.

## V0.1.1

- Fixed failures to run on Gog because the game renames its `Galaxy` class to avoid conflicts with the Gog Galaxy DLL

## V0.1.0

Initial public release

# Changelog

## Unreleased

- Ship design: add a way to remove modules, fixed confusing wording for installed modules, add labels to various panels
- Remove the use of unneeded columns in the following screens to reduce confusion:
    - Ship design
    - Military
    - Confirmation dialogs
    - End turn controls
    - Fleet panel
    - Star system (side panels, improvements, politics, rename, and system selection dialogs)
    - Empire screen (tabs and side panels)
    - Economy screen and the recipe creation dialog
- The constructibles and hangar panels (star system and empire screens) are now labelled, with the constructible filters in a "Filters" section, the hangar's buttons in an "Actions" section, and every constructible or ship read one per row in an "Available" section
- The luxury and strategic resource grids (economy screen and recipe dialog) read as a legend of resource families followed by one resource per row, each saying its family; the recipe dialog's family names are fixed ("Industry" instead of "Improves Industry Food")
- Tutorial pop-ups are now a list of pages: moving up and down turns the game's own pages and reads them, so the previous/next page buttons are gone. The minimized bar is a labelled "Tutorial" panel reading its title, then Minimize, then Close
- The screen title's close button is no longer in the tab order — press Escape to close a screen instead. On the galaxy map, the view title now labels the panel holding the zoom and scan controls

## V0.1.1

- Fixed failures to run on Gog because the game renames its `Galaxy` class to avoid conflicts with the Gog Galaxy DLL

## V0.1.0

Initial public release

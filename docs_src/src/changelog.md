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
    - Senate screen and the government, laws, population, and election dialogs
    - Planet overview
    - Troop management, battle tactics, and fleet selection dialogs
    - Academy screen, hero inspection, hero selection, and the academy dialogs
    - Research screen, quest journal, and the diplomacy screens (including negotiation, minor faction, and pirate dialogs)
    - Notification pop-ups, battle pop-ups and battle screens
    - All dialogs (error, non-blocking messages, victory, journal, chat tabs, and more)
    - The whole out-game family (options, load/save, new game lobby, faction chooser, custom faction editor, tutorial picker, DLC browser, mod manager, and asset exporter)
- The diplomacy screen now says whose ring of empires is shown (the centre empire is a readable line at the top of the list)
- Notification pop-ups read their content first; the browse controls (previous/next) and the window controls come after it
- The faction chooser's ship hull viewer is a list: moving through it shows and reads each hull, replacing the previous/next buttons
- The options screen's settings say which category tab they belong to; the DLC browser's list says which tab it is showing
- The pause menu names itself from the game's own drawn title
- Drawn captions across the senate family now label their sections instead of occupying rows, and the laws/population dialogs gained section jumps (Alt+Up/Down)
- The ship designer's module filters share one row again inside their "Filters" section, matching the constructibles pattern
- The view title is no longer in the tab order anywhere: the galaxy map keeps the zoom and scan controls, other views keep just their scan button, and Escape remains the way to close a screen
- The constructibles and hangar panels (star system and empire screens) are now labelled, with the constructible filters in a "Filters" section, the hangar's buttons in an "Actions" section, and every constructible or ship read one per row in an "Available" section
- The luxury and strategic resource grids (economy screen and recipe dialog) read as a legend of resource families followed by one resource per row, each saying its family; the recipe dialog's family names are fixed ("Industry" instead of "Improves Industry Food")
- Tutorial pop-ups are now a list of pages: moving up and down turns the game's own pages and reads them, so the previous/next page buttons are gone. The minimized bar is a labelled "Tutorial" panel reading its title, then Minimize, then Close
- The screen title's close button is no longer in the tab order — press Escape to close a screen instead. On the galaxy map, the view title now labels the panel holding the zoom and scan controls
- Ship design: the ship's module slots are now read grouped by the type of module they take (defence, then support, then weapon), instead of wherever the hull happens to draw them. A slot that takes several types is read with the first of them, and what is fitted in a slot never changes where it is read
- Ship design: the statistics panel's module health figure is now called "Health Bonus" instead of announcing only its explanation and a bare number
- Research screen: the branches and stages of the technology tree now say only their names ("Military", "Military I") — the researched-over-available counts in front of every group are gone
- Star system and empire screens: queuing a construction now says what went into the queue ("Queued Interplanetary Transport Network"), instead of answering the key with silence
- Ship design: an empty slot now reads the three markers the game only draws as pictures — "Times 2 Multiplier" for the dots that multiply a fitted module, "Symmetrical (x2 cost)" for a slot mirrored on the far side of the ship, and "Heavy Mount" for the slot the game only draws bigger. A filled slot is unchanged: its module's own tooltip already says all three

## V0.1.1

- Fixed failures to run on Gog because the game renames its `Galaxy` class to avoid conflicts with the Gog Galaxy DLL

## V0.1.0

Initial public release

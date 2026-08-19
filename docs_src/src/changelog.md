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
- The strategic resource grid (economy screen) and both resource grids in the recipe dialog read as a legend of resource families followed by one resource per row, each saying its family; the recipe dialog's family names are fixed ("Industry" instead of "Improves Industry Food")
- Tutorial pop-ups are now a list of pages: moving up and down turns the game's own pages and reads them, so the previous/next page buttons are gone. The minimized bar is a labelled "Tutorial" panel reading its title, then Minimize, then Close
- The screen title's close button is no longer in the tab order — press Escape to close a screen instead
- Ship design: the ship's module slots are now read grouped by the type of module they take (defence, then support, then weapon), instead of wherever the hull happens to draw them. A slot that takes several types is read with the first of them, and what is fitted in a slot never changes where it is read
- Ship design: the statistics panel's module health figure is now called "Health Bonus" instead of announcing only its explanation and a bare number
- Research screen: the branches and stages of the technology tree now say only their names ("Military", "Military I") — the researched-over-available counts in front of every group are gone
- Star system and empire screens: queuing a construction now says what went into the queue ("Queued Interplanetary Transport Network"), instead of answering the key with silence. Queuing one at the front says so ("Queued Infinite Supermarkets as first item"), and taking a line out of the queue says "Cancelled Infinite Supermarkets"
- Research screen: the same words now answer the research queue — "Queued Survival Suits" for a technology you queue, "Queued Survival Suits as first item" for one you queue at the front, and "Cancelled Survival Suits" for one you take out, whether from its dot on the wheel or from the queue itself
- Galaxy map: a probe in flight is now listed at the top level of the map, in the same section as obliterator missiles and ally pins, instead of being hidden inside whichever system it happened to be flying nearest to. Its row still says which star it is out from, which way and how many turns
- Galaxy map: tabbing between the panels now says which one you have landed in — "Map", "Quest", "Notifications" and "View Controls" (the zoom and scan controls). The quest and notification panels say their name on every screen that draws them
- The top-left corner of every screen now says which of its four rows you are on: "Controls" for the strip of buttons that open the game's screens, "Key Resources" for dust, manpower and influence, "Research", and "Strategic Resources" for the stockpiles
- Galaxy map: inspect mode (Ctrl+I) now opens on a single square instead of a three by three one — press plus to widen it. Pressing plus or minus when the cursor is already at its largest or smallest now says nothing at all, instead of repeating the size you are already on
- Notification pop-ups: the controls are now a Tab stop of their own, separate from what the notification says. Tab moves between reading the notification and working it — browsing to the next or previous one, popping this kind up automatically, minimizing and Done — so a long report is read to its end without the controls in the way, and they are one key away from anywhere in it
- Battle tactics: the two lists now say which one you have tabbed into — "Available" for the tactics you can pick up, and the game's own "Tactics" for the set below. The "4 tactics available" count row and the repeated "Tactics" caption row are gone
- Review buffers: Ctrl+Left and Ctrl+Right now step over any buffer with nothing in it, so a single-player game no longer cycles through an empty chat log to get back to where it started
- Economy screen: the luxury resources are now an eight column table, one column per resource family. The family names are the table's column headings and nothing you walk through: moving sideways says which family you have crossed into, tabbing into the table says which family you have landed in, and what a family does for a system's development is in the review buffer of every resource in its column. Moving up and down keeps the column, and a family with no resource on that line says "empty" so the columns stay lined up. Typing still finds a resource by name from any column
- The top-left corner of every screen now names the faction rows too, for the empires that have them: "Essence", "Manage Population", "Singularities", "Golden Age", "Pirate Mark", "Keii" and "Relics"
- Ship design: an empty slot now reads the three markers the game only draws as pictures — "Times 2 Multiplier" for the dots that multiply a fitted module, "Symmetrical (x2 cost)" for a slot mirrored on the far side of the ship, and "Heavy Mount" for the slot the game only draws bigger. A filled slot is unchanged: its module's own tooltip already says all three
- Changing a game setting now says the game's own sentence about the value you have moved to, not just its name — "Slow, A slow-paced game takes about 450 turns maximum" instead of "Slow". This covers the new game lobby, its advanced settings, and the settings panels in the pause menu; the options screen is unchanged, because its sliders and tick boxes keep one description whatever they are set to
- Mod manager: the mods library now reads as a "Filters" section holding the folder switches on one row, followed by an "Available" section with the library's contents one per row, matching the constructibles pattern
- The top-left corner of every screen: Alt+Up and Alt+Down now jump between its rows — Controls, Key Resources, Research, Strategic Resources, and the faction rows below them — instead of only naming them
- Economy screen: tabbing into the luxury resources now lands on the first line of the table, so you reach the figures straight away
- A control's description now reaches the review buffer one frame sooner after you arrow onto it. If it still feels slow, raise the frame rate limit in the game's video options — the wait is a fixed number of frames, so a 20 frame per second cap makes it five times longer than the game's default does

## V0.1.1

- Fixed failures to run on Gog because the game renames its `Galaxy` class to avoid conflicts with the Gog Galaxy DLL

## V0.1.0

Initial public release

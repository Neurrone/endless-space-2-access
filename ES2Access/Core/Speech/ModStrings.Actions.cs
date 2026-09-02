using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// What each of the mod's own keyboard actions is CALLED, and - where there is more to say than
    /// the name already says - what it does: the row's name, and the sentence its tooltip explains
    /// it with.
    ///
    /// The keys are composed from the action's own name (<c>ui.down</c> becomes
    /// <c>action.ui.down.title</c>), not declared one constant at a time, because the action names
    /// are the input layer's and this table only has to answer for them. An action nobody has
    /// written words for reads out as its own name rather than as silence, which is a defect a
    /// player can report rather than one nobody can hear.
    ///
    /// A TITLE IS COMPULSORY AND A DESCRIPTION IS NOT (owner ruling 2026-09-02). Most rows say
    /// everything they have to say in their name, and a tooltip that only repeats the name is a
    /// second reading of the same words on every row the player steps onto. So an action with no
    /// <c>.description</c> row here has no tooltip at all: <c>ModBindings.Description</c> answers
    /// empty, the row's <c>AgeTooltip</c> is left empty, and the options screen declares none.
    ///
    /// They live apart from the rest of <see cref="ModStrings"/> for the reason the icon names do:
    /// one family, read together, easier to translate as a block.
    /// </summary>
    public static partial class ModStrings
    {
        /// <summary>The name a rebinding row gives the action called <paramref name="actionKey"/>.
        /// </summary>
        public static string ActionTitleKey(string actionKey)
        {
            return "action." + actionKey + ".title";
        }

        /// <summary>The sentence a rebinding row explains that action with - which most actions do
        /// not have.</summary>
        public static string ActionDescriptionKey(string actionKey)
        {
            return "action." + actionKey + ".description";
        }

        /// <summary>Every key this table ships - so a test can check the translation template
        /// carries them all, which the shipped-constant sweep cannot do for a composed key.
        /// </summary>
        public static IEnumerable<string> ActionStringKeys()
        {
            return ActionDefaults.Keys;
        }

        private static readonly Dictionary<string, string> ActionDefaults = new Dictionary<
            string,
            string
        >
        {
            // Moving the cursor.
            { "action.ui.up.title", "Move up" },
            { "action.ui.up.description", "Move the cursor to the control above." },
            { "action.ui.down.title", "Move down" },
            { "action.ui.down.description", "Move the cursor to the control below." },
            { "action.ui.left.title", "Move left" },
            {
                "action.ui.left.description",
                "Move the cursor left, move to enclosing group, or decrement a slider"
            },
            { "action.ui.right.title", "Move right" },
            {
                "action.ui.right.description",
                "Move the cursor right, expand and move to the first child of the group, or increment a slider"
            },
            { "action.ui.next.title", "Move to next panel" },
            { "action.ui.next.description", "Move to the next panel of this screen." },
            { "action.ui.prev.title", "Move to previous panel" },
            { "action.ui.prev.description", "Move to the previous panel of this screen." },
            { "action.ui.home.title", "Move to first control" },
            { "action.ui.home.description", "Move to the first control of this panel." },
            { "action.ui.end.title", "Move to last control" },
            { "action.ui.end.description", "Move to the last control of this panel." },
            { "action.ui.regionPrev.title", "Move to previous section" },
            { "action.ui.regionPrev.description", "Move to the previous section of this panel." },
            { "action.ui.regionNext.title", "Move to next section" },
            { "action.ui.regionNext.description", "Move to the next section of this panel." },

            // Working a control. Each of the four modified clicks is NAMED for the gesture it
            // performs, so the row says which mouse gesture the player is giving the game and the
            // sentence beside it says nothing else.
            { "action.ui.activate.title", "Activate" },
            {
                "action.ui.activate.description",
                "Do what a left click does here, or complete a drag and drop."
            },
            { "action.ui.back.title", "Back" },
            {
                "action.ui.back.description",
                "Exits the current context such as an active drag, inspect mode or the current screen"
            },
            { "action.ui.alternate.title", "Alt left click" },
            { "action.ui.alternate.description", "Performs an Alt left-click" },
            { "action.ui.contextual.title", "Right click" },
            {
                "action.ui.contextual.description",
                "Perform the game's right click here. With Control held, the game reads the Control itself."
            },
            { "action.ui.doubleClick.title", "Double click" },
            { "action.ui.doubleClick.description", "Perform the game's double click here." },
            { "action.ui.coarseDecrease.title", "Step down" },
            {
                "action.ui.coarseDecrease.description",
                "Moves a slider down by a tenth of its range, steps the zoom control down one level, or, while inspecting the map, moves to the next interesting tile west"
            },
            { "action.ui.coarseIncrease.title", "Step up" },
            {
                "action.ui.coarseIncrease.description",
                "Moves a slider up by a tenth of its range, steps the zoom control up one level, or, while inspecting the map, moves to the next interesting tile east"
            },
            { "action.ui.carry.title", "Start drag" },
            { "action.ui.carry.description", "Start dragging" },
            { "action.ui.selectToggle.title", "Control left click" },
            { "action.ui.selectToggle.description", "Performs a Control left-click" },
            { "action.ui.selectRange.title", "Shift left click" },
            { "action.ui.selectRange.description", "Performs a Shift left-click" },

            // Turning the page, and going straight somewhere.
            { "action.ui.pagePrev.title", "Move to previous page" },
            {
                "action.ui.pagePrev.description",
                "Activates the previous page control on screens that have one"
            },
            { "action.ui.pageNext.title", "Move to next page" },
            {
                "action.ui.pageNext.description",
                "Activates the next page control on screens that have one"
            },
            { "action.ui.focusEmpire.title", "Focus HUD" },
            { "action.ui.focusNotifications.title", "Focus notifications" },
            { "action.ui.focusTurn.title", "Focus turn controls" },
            { "action.ui.focusTurnLog.title", "Focus turn log" },
            { "action.ui.focusMap.title", "Focus galactic map" },
            { "action.ui.summarizeMap.title", "Summarize galactic map" },
            {
                "action.ui.summarizeMap.description",
                "Read galaxy shape and size and distance from galactic centre"
            },
            { "action.ui.endTurn.title", "Activate end turn button" },
            { "action.ui.nextIdleFleet.title", "Next idle fleet" },
            { "action.ui.applyMovements.title", "Apply fleet movements" },
            {
                "action.ui.applyMovements.description",
                "Activates apply fleet movements button"
            },
            { "action.ui.goToLocation.title", "Move to location" },
            {
                "action.ui.goToLocation.description",
                "Jump to location associated with the focused item such as a notification"
            },
            { "action.ui.secondary.title", "Return to previous position" },
            {
                "action.ui.secondary.description",
                "Return to previous position after following a star lane, scanner or bookmark jump. Also works in inspect mode"
            },
            { "action.ui.clear.title", "Clear key binding" },
            { "action.ui.clear.description", "Clears a key bind" },

            // The map's inspect cursor.
            { "action.galaxy.inspect.title", "Enter inspect mode" },
            {
                "action.galaxy.inspect.description",
                "Use the arrow keys to inspect the map. Works with bookmarks and the scanner to jump to map locations"
            },
            { "action.galaxy.inspectGrow.title", "Expand cursor" },
            { "action.galaxy.inspectShrink.title", "Shrink cursor" },
            { "action.galaxy.inspectSkipNorth.title", "Move to next interesting tile north" },
            { "action.galaxy.inspectSkipSouth.title", "Move to next interesting tile south" },
            { "action.galaxy.inspectFollowWest.title", "Follow star lane to source" },
            {
                "action.galaxy.inspectFollowEast.title",
                "Follow fleet or star lane to destination"
            },

            // The map's scanner.
            { "action.galaxy.scanCategoryNext.title", "Move to next category" },
            { "action.galaxy.scanCategoryPrev.title", "Move to previous category" },
            { "action.galaxy.scanSubcategoryNext.title", "Move to next subcategory" },
            { "action.galaxy.scanSubcategoryPrev.title", "Move to previous subcategory" },
            { "action.galaxy.scanNext.title", "Move to next scanner result" },
            { "action.galaxy.scanPrev.title", "Move to previous scanner result" },
            { "action.galaxy.scanGoTo.title", "Go to the scanner result" },

            // The three categories the player makes for themselves, each on one key and its shifted
            // self. The titles are numbered rather than named, because the row has to say which key
            // this is whatever the player has called the category it answers for - and an empty slot
            // has no name at all.
            { "action.galaxy.scanCustom1Next.title", "Move to next result in custom category 1" },
            {
                "action.galaxy.scanCustom1Prev.title",
                "Move to previous result in custom category 1"
            },
            { "action.galaxy.scanCustom2Next.title", "Move to next result in custom category 2" },
            {
                "action.galaxy.scanCustom2Prev.title",
                "Move to previous result in custom category 2"
            },
            { "action.galaxy.scanCustom3Next.title", "Move to next result in custom category 3" },
            {
                "action.galaxy.scanCustom3Prev.title",
                "Move to previous result in custom category 3"
            },

            // MAP BOOKMARKS: ten places the player names by a digit - a key to make each one
            // and a key to go back to it - and the home system on a key of its own.
            { "action.galaxy.bookmarkSet1.title", "Set bookmark 1" },
            { "action.galaxy.bookmarkSet2.title", "Set bookmark 2" },
            { "action.galaxy.bookmarkSet3.title", "Set bookmark 3" },
            { "action.galaxy.bookmarkSet4.title", "Set bookmark 4" },
            { "action.galaxy.bookmarkSet5.title", "Set bookmark 5" },
            { "action.galaxy.bookmarkSet6.title", "Set bookmark 6" },
            { "action.galaxy.bookmarkSet7.title", "Set bookmark 7" },
            { "action.galaxy.bookmarkSet8.title", "Set bookmark 8" },
            { "action.galaxy.bookmarkSet9.title", "Set bookmark 9" },
            { "action.galaxy.bookmarkSet0.title", "Set bookmark 0" },
            { "action.galaxy.bookmarkGoTo1.title", "Jump to bookmark 1" },
            { "action.galaxy.bookmarkGoTo2.title", "Jump to bookmark 2" },
            { "action.galaxy.bookmarkGoTo3.title", "Jump to bookmark 3" },
            { "action.galaxy.bookmarkGoTo4.title", "Jump to bookmark 4" },
            { "action.galaxy.bookmarkGoTo5.title", "Jump to bookmark 5" },
            { "action.galaxy.bookmarkGoTo6.title", "Jump to bookmark 6" },
            { "action.galaxy.bookmarkGoTo7.title", "Jump to bookmark 7" },
            { "action.galaxy.bookmarkGoTo8.title", "Jump to bookmark 8" },
            { "action.galaxy.bookmarkGoTo9.title", "Jump to bookmark 9" },
            { "action.galaxy.bookmarkGoTo0.title", "Jump to bookmark 0" },
            { "action.galaxy.bookmarkHome.title", "Jump to home system" },

            // Reading the description of what the cursor is on.
            { "action.buffer.lineUp.title", "Read previous buffer line" },
            { "action.buffer.lineDown.title", "Read next buffer line" },
            { "action.buffer.prev.title", "Switch to previous buffer" },
            { "action.buffer.next.title", "Switch to next buffer" },
            { "action.buffer.first.title", "Read first line in buffer" },
            { "action.buffer.last.title", "Read last line in buffer" },
        };
    }
}

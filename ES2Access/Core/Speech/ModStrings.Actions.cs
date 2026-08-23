using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// What each of the mod's own keyboard actions is CALLED, and what it does - the two lines a
    /// rebinding row shows: the action's name, and the sentence its tooltip explains it with.
    ///
    /// The keys are composed from the action's own name (<c>ui.down</c> becomes
    /// <c>action.ui.down.title</c>), not declared one constant at a time, because the action names
    /// are the input layer's and this table only has to answer for them. An action nobody has
    /// written words for reads out as its own name rather than as silence, which is a defect a
    /// player can report rather than one nobody can hear.
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

        /// <summary>The sentence a rebinding row explains that action with.</summary>
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
                "Move the cursor left, close the group it is in, or turn a value down."
            },
            { "action.ui.right.title", "Move right" },
            {
                "action.ui.right.description",
                "Move the cursor right, open the group it is on, or turn a value up."
            },
            { "action.ui.next.title", "Next panel" },
            { "action.ui.next.description", "Go to the next panel of this screen." },
            { "action.ui.prev.title", "Previous panel" },
            { "action.ui.prev.description", "Go to the previous panel of this screen." },
            { "action.ui.home.title", "First control" },
            { "action.ui.home.description", "Go to the first control of this panel." },
            { "action.ui.end.title", "Last control" },
            { "action.ui.end.description", "Go to the last control of this panel." },
            { "action.ui.regionPrev.title", "Previous section" },
            {
                "action.ui.regionPrev.description",
                "Jump to the previous section of this panel."
            },
            { "action.ui.regionNext.title", "Next section" },
            { "action.ui.regionNext.description", "Jump to the next section of this panel." },

            // Working a control.
            { "action.ui.activate.title", "Activate" },
            {
                "action.ui.activate.description",
                "Do what a click on this control does, and put down whatever is being carried."
            },
            { "action.ui.secondary.title", "Second command" },
            {
                "action.ui.secondary.description",
                "The screen's own second command here, where it has one."
            },
            { "action.ui.back.title", "Back" },
            {
                "action.ui.back.description",
                "Close what is open, clear a search, or put down what is being carried."
            },
            { "action.ui.alternate.title", "Alternate command" },
            {
                "action.ui.alternate.description",
                "The game's own modified click on this control, such as queueing at the head of a queue."
            },
            { "action.ui.contextual.title", "Right click command" },
            {
                "action.ui.contextual.description",
                "The command the game puts on a right click here."
            },
            { "action.ui.doubleClick.title", "Second click command" },
            {
                "action.ui.doubleClick.description",
                "The command the game puts on a double click here."
            },
            { "action.ui.coarseDecrease.title", "Turn down by a lot" },
            {
                "action.ui.coarseDecrease.description",
                "Move a value down by about a tenth of its range."
            },
            { "action.ui.coarseIncrease.title", "Turn up by a lot" },
            {
                "action.ui.coarseIncrease.description",
                "Move a value up by about a tenth of its range."
            },
            { "action.ui.carry.title", "Pick up or put back" },
            {
                "action.ui.carry.description",
                "Pick up what the cursor is on, swap it for another, or put it back where it came from."
            },
            { "action.ui.selectToggle.title", "Add to or take out of the selection" },
            {
                "action.ui.selectToggle.description",
                "Take this one in or out of the selection, leaving the rest of it alone."
            },
            { "action.ui.selectRange.title", "Select up to here" },
            {
                "action.ui.selectRange.description",
                "Take everything from the last one picked up to this one."
            },

            // Turning the page, and going straight somewhere.
            { "action.ui.pagePrev.title", "Previous page" },
            {
                "action.ui.pagePrev.description",
                "Turn the whole screen back - the previous system, planet, notification or hero."
            },
            { "action.ui.pageNext.title", "Next page" },
            {
                "action.ui.pageNext.description",
                "Turn the whole screen on - the next system, planet, notification or hero."
            },
            { "action.ui.focusEmpire.title", "Go to the empire banners" },
            {
                "action.ui.focusEmpire.description",
                "Go straight to the empire's resource banners, on the screens that draw them."
            },
            { "action.ui.focusNotifications.title", "Go to the notifications" },
            {
                "action.ui.focusNotifications.description",
                "Go straight to the notification strip, on the screens that draw it."
            },
            { "action.ui.focusTurn.title", "Go to the turn controls" },
            {
                "action.ui.focusTurn.description",
                "Go straight to the turn controls, on the screens that draw them."
            },
            { "action.ui.focusTurnLog.title", "Go to the turn log" },
            {
                "action.ui.focusTurnLog.description",
                "Go straight to the log of what has happened this turn."
            },
            { "action.ui.focusMap.title", "Go to the map" },
            { "action.ui.focusMap.description", "Go straight to the galaxy map." },
            { "action.ui.endTurn.title", "End the turn" },
            {
                "action.ui.endTurn.description",
                "End the turn from anywhere the turn controls are drawn."
            },
            { "action.ui.goToLocation.title", "Show on the map" },
            {
                "action.ui.goToLocation.description",
                "Go to where the thing under the cursor happened."
            },

            // The map's inspect cursor.
            { "action.galaxy.inspect.title", "Inspect the map" },
            {
                "action.galaxy.inspect.description",
                "Turn on the square of galaxy the arrows sweep, and turn it off again."
            },
            { "action.galaxy.inspectGrow.title", "Inspect a bigger square" },
            {
                "action.galaxy.inspectGrow.description",
                "Make the inspected square of galaxy one step larger."
            },
            { "action.galaxy.inspectShrink.title", "Inspect a smaller square" },
            {
                "action.galaxy.inspectShrink.description",
                "Make the inspected square of galaxy one step smaller."
            },
            { "action.galaxy.inspectSkipNorth.title", "Skip north" },
            {
                "action.galaxy.inspectSkipNorth.description",
                "Move the inspected square north until what it holds changes."
            },
            { "action.galaxy.inspectSkipSouth.title", "Skip south" },
            {
                "action.galaxy.inspectSkipSouth.description",
                "Move the inspected square south until what it holds changes."
            },
            { "action.galaxy.inspectFollowWest.title", "Follow the starlane west" },
            {
                "action.galaxy.inspectFollowWest.description",
                "Travel to the western end of the starlane in the inspected square."
            },
            { "action.galaxy.inspectFollowEast.title", "Follow the fleets east" },
            {
                "action.galaxy.inspectFollowEast.description",
                "Travel to where the fleets in the inspected square are going."
            },

            // The map's scanner.
            { "action.galaxy.scanCategoryNext.title", "Scan for the next kind of thing" },
            {
                "action.galaxy.scanCategoryNext.description",
                "Scan for the next kind of thing near where you are reading."
            },
            { "action.galaxy.scanCategoryPrev.title", "Scan for the previous kind of thing" },
            {
                "action.galaxy.scanCategoryPrev.description",
                "Scan for the previous kind of thing near where you are reading."
            },
            { "action.galaxy.scanSubcategoryNext.title", "Next way of looking" },
            {
                "action.galaxy.scanSubcategoryNext.description",
                "Narrow the scan to the next group within this kind of thing."
            },
            { "action.galaxy.scanSubcategoryPrev.title", "Previous way of looking" },
            {
                "action.galaxy.scanSubcategoryPrev.description",
                "Narrow the scan to the previous group within this kind of thing."
            },
            { "action.galaxy.scanNext.title", "Next thing found" },
            {
                "action.galaxy.scanNext.description",
                "Read out the next thing the scan found, nearest first."
            },
            { "action.galaxy.scanPrev.title", "Previous thing found" },
            {
                "action.galaxy.scanPrev.description",
                "Read out the previous thing the scan found, nearest first."
            },
            { "action.galaxy.scanGoTo.title", "Go to what the scan found" },
            {
                "action.galaxy.scanGoTo.description",
                "Go to whatever the scan is pointing at."
            },

            // Reading the description of what the cursor is on.
            { "action.buffer.lineUp.title", "Previous line of the description" },
            {
                "action.buffer.lineUp.description",
                "Read the line above in the description of what the cursor is on."
            },
            { "action.buffer.lineDown.title", "Next line of the description" },
            {
                "action.buffer.lineDown.description",
                "Read the line below in the description of what the cursor is on."
            },
            { "action.buffer.prev.title", "Previous description" },
            {
                "action.buffer.prev.description",
                "Switch to the previous body of text - the screen's own, or the chat log."
            },
            { "action.buffer.next.title", "Next description" },
            {
                "action.buffer.next.description",
                "Switch to the next body of text - the screen's own, or the chat log."
            },
            { "action.buffer.first.title", "First line of the description" },
            {
                "action.buffer.first.description",
                "Go to the first line of the description."
            },
            { "action.buffer.last.title", "Last line of the description" },
            { "action.buffer.last.description", "Go to the last line of the description." },
        };
    }
}

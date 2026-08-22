namespace ES2Access.UI.Input
{
    /// <summary>The action names the graph navigator understands. Named for what the player is doing,
    /// not for the key that happens to be bound to it.</summary>
    public static class UiActions
    {
        public const string Up = "ui.up";
        public const string Down = "ui.down";
        public const string Left = "ui.left";
        public const string Right = "ui.right";
        public const string Next = "ui.next";
        public const string Prev = "ui.prev";
        public const string Activate = "ui.activate";
        public const string Secondary = "ui.secondary";
        public const string Back = "ui.back";
        public const string Home = "ui.home";
        public const string End = "ui.end";
        public const string RegionPrev = "ui.regionPrev";
        public const string RegionNext = "ui.regionNext";

        // The coarse half of a slider: the same move left and right make, in the step size the
        // control calls large.
        public const string CoarseIncrease = "ui.coarseIncrease";
        public const string CoarseDecrease = "ui.coarseDecrease";

        /// <summary>The other thing Enter could have meant on this control - the game's own
        /// modified click, where it has one.</summary>
        public const string Alternate = "ui.alternate";

        /// <summary>The command the game puts on a right click here.</summary>
        public const string Contextual = "ui.contextual";

        /// <summary>The command the game puts on a DOUBLE click here - its own second click, which
        /// on this game's tables and cards is a command in its own right.</summary>
        public const string DoubleClick = "ui.doubleClick";

        /// <summary>Pick the focused thing up, swap it for another, or put it back where it came
        /// from. Where it is put DOWN is named with <see cref="Activate"/>.</summary>
        public const string Carry = "ui.carry";

        // The game's own two ways of changing a selection without replacing it: one item in or out,
        // and everything from the last one to this one.
        public const string SelectToggle = "ui.selectToggle";
        public const string SelectRange = "ui.selectRange";

        /// <summary>Turn the whole page back or on - the previous/next system, planet, notification or
        /// hero - wherever the cursor is standing on a screen that draws such a pair
        /// (<see cref="ES2Access.Screens.Screen.PagePrev"/>). Inert everywhere else.</summary>
        public const string PagePrev = "ui.pagePrev";
        public const string PageNext = "ui.pageNext";

        // Go straight to a named panel, on the pages that draw it. Each is live exactly while the
        // focused screen declares its stop, and does nothing at all anywhere else.
        public const string FocusEmpire = "ui.focusEmpire";
        public const string FocusNotifications = "ui.focusNotifications";
        public const string FocusTurn = "ui.focusTurn";
        public const string FocusTurnLog = "ui.focusTurnLog";
        public const string FocusMap = "ui.focusMap";

        /// <summary>End the turn from anywhere the game is drawing the end-turn button, without walking
        /// to it. The game's own end-turn shortcut key is not reachable for a mod user - the mod claims
        /// the keypad Enter it sits on for Activate.</summary>
        public const string EndTurn = "ui.endTurn";
    }
}

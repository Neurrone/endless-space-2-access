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
    }
}

namespace ES2Access.UI.Input
{
    /// <summary>
    /// The actions that belong to the GALAXY MAP rather than to a control on it.
    ///
    /// They are separate from <see cref="UiActions"/> because the navigator knows nothing about them:
    /// they are offered to the focused SCREEN (<c>Screen.AnyKey</c>), which is how a mode the map
    /// imposes on the whole page takes a key ahead of whatever the cursor is standing on.
    /// </summary>
    public static class MapActions
    {
        /// <summary>Turn the map's inspect cursor on and off - a square of galaxy the player sweeps
        /// with the arrows, hearing what is inside it.</summary>
        public const string Inspect = "galaxy.inspect";

        // The cursor's size, on the two keys that mean bigger and smaller everywhere. Claimed from the
        // game only while the cursor is up, so the game keeps its own minus key the rest of the time.
        public const string InspectGrow = "galaxy.inspectGrow";
        public const string InspectShrink = "galaxy.inspectShrink";

        // GO TO THE NEXT INTERESTING CELL, north and south. The east-west pair of the same gesture is
        // not here: Shift and the side arrows are already the coarse step (<see cref="UiActions"/>),
        // which means nothing on a map, so the inspect cursor takes those two actions instead of
        // double-binding the chord.
        public const string InspectSkipNorth = "galaxy.inspectSkipNorth";
        public const string InspectSkipSouth = "galaxy.inspectSkipSouth";

        // TRAVEL BY WHAT THE CELL HOLDS: west along the one lane here, or east to where the fleets
        // here are going (<see cref="ES2Access.Screens.GalaxyInspect"/>). Keys of the inspect cursor
        // and of nothing else - off the map, and with no cursor up, they do nothing at all.
        public const string InspectFollowWest = "galaxy.inspectFollowWest";
        public const string InspectFollowEast = "galaxy.inspectFollowEast";

        // The SCANNER - "what is near me, of this kind" (<see cref="ES2Access.Screens.GalaxyScanner"/>).
        // Three tiers, each on the same pair of keys with a different modifier: the kind of thing, the
        // way of looking at that kind, and one thing at a time. They are not a mode - nothing arms them
        // and nothing exits them - but they ARE keys of the map widget, like the inspect cursor's: they
        // go through the same hook and are inert on every other stop of the galaxy page and on every
        // other page, while the position they were left in survives the trip away.
        public const string ScanCategoryNext = "galaxy.scanCategoryNext";
        public const string ScanCategoryPrev = "galaxy.scanCategoryPrev";
        public const string ScanSubcategoryNext = "galaxy.scanSubcategoryNext";
        public const string ScanSubcategoryPrev = "galaxy.scanSubcategoryPrev";
        public const string ScanNext = "galaxy.scanNext";
        public const string ScanPrev = "galaxy.scanPrev";

        /// <summary>Go to whatever the scanner is pointing at - the inspect cursor onto its square
        /// while that mode is up, the tree cursor onto its node otherwise.</summary>
        public const string ScanGoTo = "galaxy.scanGoTo";
    }
}

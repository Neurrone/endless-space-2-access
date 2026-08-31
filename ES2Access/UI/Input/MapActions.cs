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

        // THE THREE CATEGORIES THE PLAYER MADE, one pair of keys each (owner ruling 2026-08-23). They
        // are not a fourth tier of the scanner: each key walks its slot's whole list flat, nearest
        // first, and GOES to what it lands on - one press, one hop across the map. The number is the
        // SLOT, so the key means the same thing whatever the player has since called the category.
        public const string ScanCustom1Next = "galaxy.scanCustom1Next";
        public const string ScanCustom1Prev = "galaxy.scanCustom1Prev";
        public const string ScanCustom2Next = "galaxy.scanCustom2Next";
        public const string ScanCustom2Prev = "galaxy.scanCustom2Prev";
        public const string ScanCustom3Next = "galaxy.scanCustom3Next";
        public const string ScanCustom3Prev = "galaxy.scanCustom3Prev";

        /// <summary>The action that walks a slot forward, and the one that walks it back - indexed by
        /// slot, so the scanner can answer a press by number and name the key that made it.</summary>
        public static readonly string[] ScanCustomNext = new string[]
        {
            ScanCustom1Next,
            ScanCustom2Next,
            ScanCustom3Next,
        };

        public static readonly string[] ScanCustomPrev = new string[]
        {
            ScanCustom1Prev,
            ScanCustom2Prev,
            ScanCustom3Prev,
        };

        // THE TEN MAP BOOKMARKS, a set key and a jump key each
        // (<see cref="ES2Access.Screens.GalaxyBookmarks"/>). One action per slot rather than one
        // parameterised action, so every one of them is a row of the Controls tab the player can move
        // - which is the whole reason the scanner's quick keys are ten rows too. The names carry the
        // DIGIT and not an index, because the digit is what the player presses and what they hear.
        public const string BookmarkSet1 = "galaxy.bookmarkSet1";
        public const string BookmarkSet2 = "galaxy.bookmarkSet2";
        public const string BookmarkSet3 = "galaxy.bookmarkSet3";
        public const string BookmarkSet4 = "galaxy.bookmarkSet4";
        public const string BookmarkSet5 = "galaxy.bookmarkSet5";
        public const string BookmarkSet6 = "galaxy.bookmarkSet6";
        public const string BookmarkSet7 = "galaxy.bookmarkSet7";
        public const string BookmarkSet8 = "galaxy.bookmarkSet8";
        public const string BookmarkSet9 = "galaxy.bookmarkSet9";
        public const string BookmarkSet0 = "galaxy.bookmarkSet0";

        public const string BookmarkGoTo1 = "galaxy.bookmarkGoTo1";
        public const string BookmarkGoTo2 = "galaxy.bookmarkGoTo2";
        public const string BookmarkGoTo3 = "galaxy.bookmarkGoTo3";
        public const string BookmarkGoTo4 = "galaxy.bookmarkGoTo4";
        public const string BookmarkGoTo5 = "galaxy.bookmarkGoTo5";
        public const string BookmarkGoTo6 = "galaxy.bookmarkGoTo6";
        public const string BookmarkGoTo7 = "galaxy.bookmarkGoTo7";
        public const string BookmarkGoTo8 = "galaxy.bookmarkGoTo8";
        public const string BookmarkGoTo9 = "galaxy.bookmarkGoTo9";
        public const string BookmarkGoTo0 = "galaxy.bookmarkGoTo0";

        /// <summary>Go to the empire's home system - the same landing a bookmark jump makes, at a
        /// place the player never has to set and that consumes no slot.</summary>
        public const string BookmarkHome = "galaxy.bookmarkHome";

        /// <summary>The set and jump actions in SLOT order - the order of
        /// <c>ES2Access.Core.Bookmarks.MapBookmarks.Digits</c>, 1 to 9 and then 0, so index i and
        /// digit i are the same slot in both tables.</summary>
        public static readonly string[] BookmarkSet = new string[]
        {
            BookmarkSet1,
            BookmarkSet2,
            BookmarkSet3,
            BookmarkSet4,
            BookmarkSet5,
            BookmarkSet6,
            BookmarkSet7,
            BookmarkSet8,
            BookmarkSet9,
            BookmarkSet0,
        };

        public static readonly string[] BookmarkGoTo = new string[]
        {
            BookmarkGoTo1,
            BookmarkGoTo2,
            BookmarkGoTo3,
            BookmarkGoTo4,
            BookmarkGoTo5,
            BookmarkGoTo6,
            BookmarkGoTo7,
            BookmarkGoTo8,
            BookmarkGoTo9,
            BookmarkGoTo0,
        };
    }
}

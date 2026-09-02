using ES2Access.Core.Speech;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// HOW THE CONTROLS TAB IS LAID OUT: six tables, each under a heading of its own.
    ///
    /// Eighty-one rows in one table is a page whose only landmark is "row 47 of 81". Split into
    /// six, every row counts inside the table it belongs to ("3 of 22"), the section jump walks the
    /// page by six names, and stepping down across a boundary announces the block being entered
    /// (owner ruling 2026-09-02).
    ///
    /// This is a PRESENTATION table, not the registration order: the order the mod registers its
    /// actions in decides which keys exist and in what order the input layer learns them, and moving
    /// a row on this page must not disturb that. So the layout is written out here, by name, and
    /// <see cref="ES2Access.UI.ModOptions.KeybindRows"/> builds the panel from it.
    ///
    /// EVERY REGISTERED ACTION APPEARS EXACTLY ONCE. A key added to the mod and forgotten here would
    /// be a binding the player cannot see, let alone change; the unit test over this table is what
    /// makes that a build failure rather than a missing row.
    /// </summary>
    public static class KeybindLayout
    {
        /// <summary>One heading and the rows drawn under it.</summary>
        public sealed class Block
        {
            public Block(string titleKey, string[] actions)
            {
                TitleKey = titleKey;
                Actions = actions;
            }

            /// <summary>The <see cref="ModStrings"/> key the heading is drawn and spoken from.
            /// </summary>
            public readonly string TitleKey;

            /// <summary>The action keys of this table's rows, top to bottom.</summary>
            public readonly string[] Actions;
        }

        /// <summary>The six tables, in the order they are drawn.</summary>
        public static readonly Block[] Blocks = new[]
        {
            new Block(
                ModStrings.ModSettingsKeysCursor,
                new[]
                {
                    UiActions.Up,
                    UiActions.Down,
                    UiActions.Left,
                    UiActions.Right,
                    UiActions.Next,
                    UiActions.Prev,
                    UiActions.Activate,
                    UiActions.Alternate,
                    UiActions.Contextual,
                    UiActions.DoubleClick,
                    UiActions.Carry,
                    UiActions.SelectToggle,
                    UiActions.SelectRange,
                    UiActions.Back,
                    UiActions.Home,
                    UiActions.End,
                    UiActions.RegionPrev,
                    UiActions.RegionNext,
                    UiActions.CoarseIncrease,
                    UiActions.CoarseDecrease,
                    UiActions.PagePrev,
                    UiActions.PageNext,
                }
            ),
            new Block(
                ModStrings.ModSettingsKeysBuffers,
                new[]
                {
                    BufferActions.LineUp,
                    BufferActions.LineDown,
                    BufferActions.Prev,
                    BufferActions.Next,
                    BufferActions.First,
                    BufferActions.Last,
                }
            ),
            new Block(
                ModStrings.ModSettingsKeysHotkeys,
                new[]
                {
                    UiActions.FocusEmpire,
                    UiActions.FocusNotifications,
                    UiActions.FocusTurn,
                    UiActions.FocusTurnLog,
                    UiActions.FocusMap,
                    UiActions.SummarizeMap,
                    UiActions.EndTurn,
                    UiActions.NextIdleFleet,
                    UiActions.ApplyMovements,
                    UiActions.GoToLocation,
                    UiActions.Secondary,
                    UiActions.Clear,
                }
            ),
            new Block(
                ModStrings.ModSettingsKeysInspect,
                new[]
                {
                    MapActions.Inspect,
                    MapActions.InspectGrow,
                    MapActions.InspectShrink,
                    MapActions.InspectSkipNorth,
                    MapActions.InspectSkipSouth,
                    MapActions.InspectFollowWest,
                    MapActions.InspectFollowEast,
                }
            ),
            new Block(
                ModStrings.ModSettingsKeysScanner,
                new[]
                {
                    MapActions.ScanCategoryNext,
                    MapActions.ScanCategoryPrev,
                    MapActions.ScanSubcategoryNext,
                    MapActions.ScanSubcategoryPrev,
                    MapActions.ScanNext,
                    MapActions.ScanPrev,
                    MapActions.ScanGoTo,
                    MapActions.ScanCustom1Next,
                    MapActions.ScanCustom1Prev,
                    MapActions.ScanCustom2Next,
                    MapActions.ScanCustom2Prev,
                    MapActions.ScanCustom3Next,
                    MapActions.ScanCustom3Prev,
                }
            ),
            new Block(
                ModStrings.ModSettingsKeysBookmarks,
                new[]
                {
                    MapActions.BookmarkHome,
                    MapActions.BookmarkSet1,
                    MapActions.BookmarkSet2,
                    MapActions.BookmarkSet3,
                    MapActions.BookmarkSet4,
                    MapActions.BookmarkSet5,
                    MapActions.BookmarkSet6,
                    MapActions.BookmarkSet7,
                    MapActions.BookmarkSet8,
                    MapActions.BookmarkSet9,
                    MapActions.BookmarkSet0,
                    MapActions.BookmarkGoTo1,
                    MapActions.BookmarkGoTo2,
                    MapActions.BookmarkGoTo3,
                    MapActions.BookmarkGoTo4,
                    MapActions.BookmarkGoTo5,
                    MapActions.BookmarkGoTo6,
                    MapActions.BookmarkGoTo7,
                    MapActions.BookmarkGoTo8,
                    MapActions.BookmarkGoTo9,
                    MapActions.BookmarkGoTo0,
                }
            ),
        };
    }
}

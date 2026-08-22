using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The half of the scanner's stepping rules that only exists because half its categories are
    /// derived from what is out there: a column per kind of anomaly, of curiosity, of resource,
    /// sorted by the name the player hears.
    ///
    /// Every failure here is inaudible in exactly the way the rest of the cursor's are, and one worse
    /// besides: a remembered INDEX resolving to a different kind does not sound like a bug at all, it
    /// sounds like a galaxy with different things in it. So the shapes that move underneath the cursor
    /// - a kind appearing ahead of the remembered one, a kind disappearing, a list re-sorting because
    /// the player walked somewhere else - are all exercised here.
    /// </summary>
    public class ScannerKindsTests
    {
        // One category whose columns are kinds: "all" and then one per anomaly the galaxy is showing,
        // in the order a player would hear them.
        private static ScannerTable Kinds(string[] labels, params int[] counts)
        {
            return new ScannerTable(new int[][] { counts }, new string[][] { labels });
        }

        private static readonly string[] TwoKinds = new string[]
        {
            "all",
            "Garden of Eden",
            "Zero-G Gym",
        };

        [Fact]
        public void AKindAppearingAheadOfTheRememberedOneDoesNotMoveTheCursorToIt()
        {
            ScannerCursor cursor = new ScannerCursor();
            cursor.Arm();
            cursor.Hold(Kinds(TwoKinds, 3, 2, 1));

            // Down to the last kind: "Zero-G Gym", at column two.
            cursor.CycleSubcategory(1, Kinds(TwoKinds, 3, 2, 1));
            cursor.CycleSubcategory(1, Kinds(TwoKinds, 3, 2, 1));
            Assert.Equal(2, cursor.Subcategory);

            // A new kind is found, and the list is alphabetical, so every column after it has moved.
            ScannerTable grown = Kinds(
                new string[] { "all", "Amber Fields", "Garden of Eden", "Zero-G Gym" },
                5,
                2,
                2,
                1
            );
            Assert.Equal(ScannerAnswer.Scope, cursor.Hold(grown));
            Assert.Equal(3, cursor.Subcategory);
            Assert.Equal("Zero-G Gym", grown.Label(0, cursor.Subcategory));
        }

        [Fact]
        public void AKindThatIsGoneLeavesTheCursorInTheFirstColumnThatHoldsSomething()
        {
            ScannerCursor cursor = new ScannerCursor();
            cursor.Arm();
            cursor.Hold(Kinds(TwoKinds, 3, 2, 1));
            cursor.CycleSubcategory(1, Kinds(TwoKinds, 3, 2, 1));
            Assert.Equal(1, cursor.Subcategory);

            // The last Garden of Eden was searched: its column does not exist any more, and an index
            // into a column that is gone points at somebody else's things.
            ScannerTable shrunk = Kinds(new string[] { "all", "Zero-G Gym" }, 1, 1);
            Assert.Equal(ScannerAnswer.Scope, cursor.Hold(shrunk));
            Assert.Equal(0, cursor.Subcategory);
            Assert.Equal("all", shrunk.Label(0, cursor.Subcategory));
        }

        [Fact]
        public void CyclingBackIntoACategoryOpensTheKindItWasLeftInWhereverItNowStands()
        {
            string[] systems = new string[] { "all", "friendly" };
            ScannerCursor cursor = new ScannerCursor();
            cursor.Arm();
            ScannerTable both = new ScannerTable(
                new int[][] { new int[] { 4, 1 }, new int[] { 3, 2, 1 } },
                new string[][] { systems, TwoKinds }
            );
            cursor.Hold(both);

            cursor.CycleCategory(1, both);
            cursor.CycleSubcategory(1, both);
            cursor.CycleSubcategory(1, both);
            Assert.Equal(1, cursor.Category);
            Assert.Equal("Zero-G Gym", both.Label(1, cursor.Subcategory));

            cursor.CycleCategory(-1, both);
            Assert.Equal(0, cursor.Category);

            // ...and back, with the kinds re-lettered underneath.
            ScannerTable moved = new ScannerTable(
                new int[][]
                {
                    new int[] { 4, 1 },
                    new int[] { 5, 2, 2, 1 },
                },
                new string[][]
                {
                    systems,
                    new string[] { "all", "Amber Fields", "Garden of Eden", "Zero-G Gym" },
                }
            );
            cursor.CycleCategory(1, moved);
            Assert.Equal(1, cursor.Category);
            Assert.Equal("Zero-G Gym", moved.Label(1, cursor.Subcategory));
        }

        [Fact]
        public void ACategoryWithNoNamesKeepsSayingItsScopeIsEmptyRatherThanMovingOn()
        {
            // The rule the naming must not have broken: a scope that EXISTS and has emptied is where
            // the cursor stays, and the empty line is what it says. Only a column that has gone is a
            // reason to move.
            ScannerCursor cursor = new ScannerCursor();
            cursor.Arm();
            int[][] counts = new int[][] { new int[] { 6, 4, 2 } };
            cursor.Hold(counts);
            cursor.CycleSubcategory(1, counts);
            cursor.CycleSubcategory(1, counts);
            Assert.Equal(2, cursor.Subcategory);

            Assert.Equal(ScannerAnswer.Empty, cursor.Hold(new int[][] { new int[] { 4, 4, 0 } }));
            Assert.Equal(2, cursor.Subcategory);
        }

        [Fact]
        public void ARowThatNarrowsBringsTheCursorBackInside()
        {
            ScannerCursor cursor = new ScannerCursor();
            cursor.Arm();
            int[][] wide = new int[][] { new int[] { 6, 4, 2, 1 } };
            cursor.Hold(wide);
            cursor.CycleSubcategory(1, wide);
            cursor.CycleSubcategory(1, wide);
            cursor.CycleSubcategory(1, wide);
            Assert.Equal(3, cursor.Subcategory);

            Assert.Equal(ScannerAnswer.Scope, cursor.Hold(new int[][] { new int[] { 2, 2 } }));
            Assert.Equal(0, cursor.Subcategory);
            Assert.Equal(0, cursor.Index);
        }

        // ---- re-seating on the thing itself ----

        [Fact]
        public void TheCursorStaysOnTheThingItWasToldAboutWhenTheListReSorts()
        {
            ScannerCursor cursor = new ScannerCursor();
            cursor.Arm();
            int[][] counts = new int[][] { new int[] { 3 } };
            cursor.Hold(counts);

            string[] first = new string[] { "a", "b", "c" };
            cursor.Landed(first);
            cursor.Step(1, counts);
            cursor.Step(1, counts);
            cursor.Landed(first);
            Assert.Equal(2, cursor.Index);
            Assert.Equal("c", cursor.ResultKey);

            // The player walked somewhere else, so the same three things come back in another order.
            string[] resorted = new string[] { "c", "a", "b" };
            cursor.Reseat(counts, resorted);
            Assert.Equal(0, cursor.Index);

            cursor.Step(1, counts);
            cursor.Landed(resorted);
            Assert.Equal("a", cursor.ResultKey);
        }

        [Fact]
        public void AThingThatIsGoneLeavesTheCursorOnTheNearestRatherThanNowhere()
        {
            ScannerCursor cursor = new ScannerCursor();
            cursor.Arm();
            int[][] counts = new int[][] { new int[] { 3 } };
            cursor.Hold(counts);
            cursor.Step(1, counts);
            cursor.Step(1, counts);
            cursor.Landed(new string[] { "a", "b", "c" });
            Assert.Equal("c", cursor.ResultKey);

            int[][] fewer = new int[][] { new int[] { 2 } };
            cursor.Reseat(fewer, new string[] { "a", "b" });
            Assert.Equal(0, cursor.Index);
        }

        [Fact]
        public void ReSeatingIsAskedBeforeAnythingHasBeenLandedOnAndDoesNothing()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = new int[][] { new int[] { 3 } };
            cursor.Reseat(counts, new string[] { "a", "b", "c" });
            Assert.Equal(0, cursor.Index);
            Assert.Null(cursor.ResultKey);
        }

        [Fact]
        public void AnEmptyScopeLeavesNothingToReSeatOn()
        {
            ScannerCursor cursor = new ScannerCursor();
            cursor.Arm();
            cursor.Hold(new int[][] { new int[] { 1 } });
            cursor.Landed(new string[] { "a" });
            Assert.Equal("a", cursor.ResultKey);

            cursor.Landed(new string[0]);
            Assert.Null(cursor.ResultKey);
        }

        [Fact]
        public void ForgettingDropsTheThingAsWellAsThePlace()
        {
            ScannerCursor cursor = new ScannerCursor();
            cursor.Arm();
            cursor.Hold(Kinds(TwoKinds, 3, 2, 1));
            cursor.CycleSubcategory(1, Kinds(TwoKinds, 3, 2, 1));
            cursor.Landed(new string[] { "a", "b" });

            cursor.Forget();
            Assert.Equal(0, cursor.Category);
            Assert.Equal(0, cursor.Subcategory);
            Assert.Equal(0, cursor.Index);
            Assert.Null(cursor.ResultKey);
        }

        // ---- the table itself ----

        [Fact]
        public void ATableWithNoNamesAnswersNothingAboutThem()
        {
            ScannerTable table = new int[][] { new int[] { 2, 1 } };
            Assert.Equal(2, table.Width(0));
            Assert.Null(table.Label(0, 0));
            Assert.Equal(-1, table.Find(0, "all"));
            Assert.True(table.Holds(0));
        }

        [Fact]
        public void ATableAnswersOutOfRangeQuestionsWithoutThrowing()
        {
            ScannerTable table = Kinds(TwoKinds, 3, 2, 1);
            Assert.Equal(0, table.Count(0, 9));
            Assert.Equal(0, table.Count(4, 0));
            Assert.Null(table.Label(0, 9));
            Assert.Equal(0, table.Width(-1));
            Assert.Equal(0, table.FirstHolding(0));
            Assert.Equal(-1, table.Find(0, null));
        }

        [Fact]
        public void TheFirstColumnThatHoldsSomethingSkipsTheEmptyOnesInFront()
        {
            ScannerTable table = Kinds(TwoKinds, 0, 0, 4);
            Assert.Equal(2, table.FirstHolding(0));
            Assert.True(table.Holds(0));
            Assert.False(Kinds(TwoKinds, 0, 0, 0).Holds(0));
        }
    }
}

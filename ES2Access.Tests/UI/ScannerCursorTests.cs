using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The scanner's stepping rules. Every failure here is inaudible: a cycle that lands on an empty
    /// scope, a step that stops dead at the end of a list, an index left pointing past the end of a
    /// list that shrank - each one sounds exactly like a scanner that simply found nothing, and none
    /// of them can be told apart in a transcript.
    /// </summary>
    public class ScannerCursorTests
    {
        // The taxonomy the galaxy scanner actually uses: two categories, four scopes each, with
        // "all" first. Written out here so the rules are exercised against a real shape.
        private static int[][] Counts(
            int systemsAll,
            int systemsFriendly,
            int systemsNeutral,
            int systemsEnemy,
            int fleetsAll,
            int fleetsFriendly,
            int fleetsNeutral,
            int fleetsEnemy
        )
        {
            return new int[][]
            {
                new int[] { systemsAll, systemsFriendly, systemsNeutral, systemsEnemy },
                new int[] { fleetsAll, fleetsFriendly, fleetsNeutral, fleetsEnemy },
            };
        }

        // The fixture galaxy: twelve systems (two of them the player's, ten neutral, no enemy), six
        // fleets (all the player's).
        private static int[][] Fixture()
        {
            return Counts(12, 2, 10, 0, 6, 6, 0, 0);
        }

        [Fact]
        public void TheFirstPressSaysWhereItIsAndMovesNothing()
        {
            ScannerCursor cursor = new ScannerCursor();
            Assert.True(cursor.Arm());

            Assert.Equal(ScannerAnswer.Scope, cursor.Hold(Fixture(), ScannerAnswer.Scope));
            Assert.Equal(0, cursor.Category);
            Assert.Equal(0, cursor.Subcategory);
            Assert.Equal(0, cursor.Index);

            // ...and only the first.
            Assert.False(cursor.Arm());
        }

        [Fact]
        public void TheArmingPressSaysTheSentenceOfTheTierThatWasPressed()
        {
            ScannerCursor cursor = new ScannerCursor();
            cursor.Arm();
            Assert.Equal(ScannerAnswer.Instance, cursor.Hold(Fixture(), ScannerAnswer.Instance));
        }

        [Fact]
        public void AnArmingPressIntoAnEmptyScopeSaysSoInsteadOfItsOwnSentence()
        {
            ScannerCursor cursor = new ScannerCursor();
            cursor.Arm();
            Assert.Equal(
                ScannerAnswer.Empty,
                cursor.Hold(Counts(0, 0, 0, 0, 0, 0, 0, 0), ScannerAnswer.Instance)
            );
        }

        [Fact]
        public void SteppingWrapsRoundBothEndsOfTheList()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = Fixture();
            for (int i = 1; i < 12; i++)
            {
                Assert.Equal(ScannerAnswer.Instance, cursor.Step(1, counts));
                Assert.Equal(i, cursor.Index);
            }

            // Off the far end and back to the nearest.
            cursor.Step(1, counts);
            Assert.Equal(0, cursor.Index);

            // And the other way round, from the nearest to the far end.
            cursor.Step(-1, counts);
            Assert.Equal(11, cursor.Index);
        }

        [Fact]
        public void SteppingAnEmptyScopeSaysSoAndStaysAtTheStart()
        {
            ScannerCursor cursor = new ScannerCursor();
            // Park on "enemy systems", which the fixture has none of.
            cursor.CycleSubcategory(-1, Counts(12, 2, 10, 1, 6, 6, 0, 0));
            Assert.Equal(3, cursor.Subcategory);

            // ...and now the last one has gone.
            Assert.Equal(ScannerAnswer.Empty, cursor.Step(1, Fixture()));
            Assert.Equal(3, cursor.Subcategory);
            Assert.Equal(0, cursor.Index);
        }

        [Fact]
        public void CyclingSubcategoriesSkipsTheOnesWithNothingInThem()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = Fixture();
            // Into the fleets, whose only non-empty scopes are "all" and "friendly".
            cursor.CycleCategory(1, counts);
            Assert.Equal(1, cursor.Category);
            Assert.Equal(0, cursor.Subcategory);

            Assert.Equal(ScannerAnswer.Scope, cursor.CycleSubcategory(1, counts));
            Assert.Equal(1, cursor.Subcategory);

            // Neutral and enemy hold nothing, so the next press comes round to "all" rather than
            // landing on either of them.
            Assert.Equal(ScannerAnswer.Scope, cursor.CycleSubcategory(1, counts));
            Assert.Equal(0, cursor.Subcategory);

            // Backwards skips them too.
            Assert.Equal(ScannerAnswer.Scope, cursor.CycleSubcategory(-1, counts));
            Assert.Equal(1, cursor.Subcategory);
        }

        [Fact]
        public void ACategoryWithOneNonEmptyScopeComesRoundToItself()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = Counts(3, 0, 0, 0, 0, 0, 0, 0);
            Assert.Equal(ScannerAnswer.Scope, cursor.CycleSubcategory(1, counts));
            Assert.Equal(0, cursor.Subcategory);
            Assert.Equal(0, cursor.Index);
        }

        [Fact]
        public void CyclingCategoriesSkipsAnEmptyOneAndWraps()
        {
            ScannerCursor cursor = new ScannerCursor();
            // No fleets drawn anywhere: the category next door is skipped, and there being nowhere
            // else to go leaves the cursor where it was.
            int[][] noFleets = Counts(12, 2, 10, 0, 0, 0, 0, 0);
            Assert.Equal(ScannerAnswer.Scope, cursor.CycleCategory(1, noFleets));
            Assert.Equal(0, cursor.Category);

            // With fleets about, the same press crosses over - and comes back.
            int[][] counts = Fixture();
            cursor.CycleCategory(1, counts);
            Assert.Equal(1, cursor.Category);
            cursor.CycleCategory(1, counts);
            Assert.Equal(0, cursor.Category);
        }

        [Fact]
        public void ANewCategoryOpensAtItsFirstScopeThatHoldsSomething()
        {
            ScannerCursor cursor = new ScannerCursor();
            // A category whose "all" is somehow empty while a later scope is not: the landing must
            // still be somewhere the player can hear something.
            int[][] counts = Counts(12, 2, 10, 0, 0, 0, 4, 0);
            cursor.CycleCategory(1, counts);
            Assert.Equal(1, cursor.Category);
            Assert.Equal(2, cursor.Subcategory);
        }

        [Fact]
        public void LandingInAScopeGoesBackToTheNearestThing()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = Fixture();
            cursor.Step(1, counts);
            cursor.Step(1, counts);
            cursor.Step(1, counts);
            Assert.Equal(3, cursor.Index);

            // A re-scope deliberately keeps no hold of what was selected - the list it indexed is
            // gone.
            cursor.CycleSubcategory(1, counts);
            Assert.Equal(0, cursor.Index);

            cursor.Step(1, counts);
            cursor.CycleCategory(1, counts);
            Assert.Equal(0, cursor.Index);
        }

        [Fact]
        public void AListThatShrankUnderTheCursorPutsItBackOnTheNearest()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = Fixture();
            for (int i = 0; i < 9; i++)
            {
                cursor.Step(1, counts);
            }

            Assert.Equal(9, cursor.Index);

            // Eight of the twelve have gone out of sight since the last press.
            Assert.Equal(ScannerAnswer.Instance, cursor.Step(1, Counts(4, 2, 2, 0, 6, 6, 0, 0)));
            Assert.Equal(1, cursor.Index);
        }

        [Fact]
        public void EverythingEmptyLeavesTheCursorWhereItIsAndSaysSo()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] nothing = Counts(0, 0, 0, 0, 0, 0, 0, 0);
            Assert.Equal(ScannerAnswer.Empty, cursor.CycleCategory(1, nothing));
            Assert.Equal(0, cursor.Category);
            Assert.Equal(ScannerAnswer.Empty, cursor.CycleSubcategory(1, nothing));
            Assert.Equal(0, cursor.Subcategory);
            Assert.Equal(ScannerAnswer.Empty, cursor.Step(1, nothing));
        }

        [Fact]
        public void ForgettingPutsItBackAtTheBeginningAndUnarmsIt()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = Fixture();
            cursor.Arm();
            cursor.CycleCategory(1, counts);
            cursor.Step(1, counts);

            cursor.Forget();
            Assert.Equal(0, cursor.Category);
            Assert.Equal(0, cursor.Subcategory);
            Assert.Equal(0, cursor.Index);
            Assert.True(cursor.Arm());
        }

        [Fact]
        public void TheScopeCountIsTheOneTheCursorIsIn()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = Fixture();
            Assert.Equal(12, cursor.Count(counts));
            cursor.CycleSubcategory(1, counts);
            Assert.Equal(2, cursor.Count(counts));
            cursor.CycleCategory(1, counts);
            Assert.Equal(6, cursor.Count(counts));
        }

        // Each category remembers its OWN subcategory. Inaudible when it is wrong in the worst way: a
        // player who narrowed systems to their own, looked at the fleets and came back would hear a
        // scope name that is right for the scope they are in and wrong for the one they asked for.

        // The three-category taxonomy the scanner grew: systems, fleets, probes.
        private static int[][] Three(
            int systemsAll,
            int systemsFriendly,
            int fleetsAll,
            int fleetsFriendly,
            int probesAll,
            int probesFriendly
        )
        {
            return new int[][]
            {
                new int[] { systemsAll, systemsFriendly, systemsAll - systemsFriendly, 0 },
                new int[] { fleetsAll, fleetsFriendly, fleetsAll - fleetsFriendly, 0 },
                new int[] { probesAll, probesFriendly, probesAll - probesFriendly, 0 },
            };
        }

        [Fact]
        public void ACategoryComesBackToTheSubcategoryItWasLeftIn()
        {
            // The owner's worked example: systems at friendly, out to fleets, and back.
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = Fixture();
            cursor.Arm();
            cursor.CycleSubcategory(1, counts);
            Assert.Equal(0, cursor.Category);
            Assert.Equal(1, cursor.Subcategory);

            // Fleets, never visited, opens at "all".
            cursor.CycleCategory(1, counts);
            Assert.Equal(1, cursor.Category);
            Assert.Equal(0, cursor.Subcategory);

            cursor.CycleCategory(-1, counts);
            Assert.Equal(0, cursor.Category);
            Assert.Equal(1, cursor.Subcategory);
        }

        [Fact]
        public void EachOfThreeCategoriesKeepsItsOwnPlace()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = Three(12, 2, 6, 6, 3, 3);
            cursor.Arm();

            // Systems to friendly, fleets left at all, probes to friendly.
            cursor.CycleSubcategory(1, counts);
            Assert.Equal(1, cursor.Subcategory);
            cursor.CycleCategory(1, counts);
            Assert.Equal(1, cursor.Category);
            Assert.Equal(0, cursor.Subcategory);
            cursor.CycleCategory(1, counts);
            Assert.Equal(2, cursor.Category);
            cursor.CycleSubcategory(1, counts);
            Assert.Equal(1, cursor.Subcategory);

            // Round the cycle again: each is where it was left.
            cursor.CycleCategory(1, counts);
            Assert.Equal(0, cursor.Category);
            Assert.Equal(1, cursor.Subcategory);
            cursor.CycleCategory(1, counts);
            Assert.Equal(1, cursor.Category);
            Assert.Equal(0, cursor.Subcategory);
            cursor.CycleCategory(1, counts);
            Assert.Equal(2, cursor.Category);
            Assert.Equal(1, cursor.Subcategory);
        }

        [Fact]
        public void ARememberedSubcategoryThatHasEmptiedFallsBackToTheFirstThatHolds()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = Fixture();
            cursor.Arm();
            cursor.CycleSubcategory(1, counts);
            Assert.Equal(1, cursor.Subcategory);
            cursor.CycleCategory(1, counts);

            // The player's own systems have gone; the memory must not park the cursor on nothing.
            int[][] emptied = Counts(10, 0, 10, 0, 6, 6, 0, 0);
            cursor.CycleCategory(-1, emptied);
            Assert.Equal(0, cursor.Category);
            Assert.Equal(0, cursor.Subcategory);
        }

        [Fact]
        public void ForgettingForgetsTheMemoryToo()
        {
            ScannerCursor cursor = new ScannerCursor();
            int[][] counts = Fixture();
            cursor.Arm();
            cursor.CycleSubcategory(1, counts);
            cursor.Forget();

            cursor.Arm();
            cursor.CycleCategory(1, counts);
            cursor.CycleCategory(-1, counts);
            Assert.Equal(0, cursor.Category);
            Assert.Equal(0, cursor.Subcategory);
        }
    }
}

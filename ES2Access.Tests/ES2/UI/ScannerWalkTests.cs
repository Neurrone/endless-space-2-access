using ES2Access.ES2.UI;
using Xunit;

namespace ES2Access.Tests.ES2.UI
{
    /// <summary>
    /// The one-key walk of a custom category. Every failure here sounds like a galaxy with less in
    /// it: a walk that re-lands on the thing the player is standing on reads as a key that has
    /// stopped working, one that stops at the end of the list reads as a list that ends there, and
    /// one that steps from an entry that has since vanished lands somewhere the player was never
    /// told about.
    /// </summary>
    public class ScannerWalkTests
    {
        private static readonly string[] Three = new string[] { "a", "b", "c" };

        [Fact]
        public void AnEmptyListHasNowhereToLand()
        {
            Assert.Equal(-1, ScannerWalk.Land(1, new string[0], null, false, false));
            Assert.Equal(-1, ScannerWalk.Land(1, null, null, true, false));
        }

        [Fact]
        public void ANewSweepStartsAtTheNearestThing()
        {
            Assert.Equal(0, ScannerWalk.Land(1, Three, null, false, false));
            Assert.Equal(0, ScannerWalk.Land(-1, Three, "c", false, false));
        }

        [Fact]
        public void APressWhileParkedOnTheNearestThingStepsOn()
        {
            Assert.Equal(1, ScannerWalk.Land(1, Three, "a", false, true));
            Assert.Equal(2, ScannerWalk.Land(-1, Three, "a", false, true));
        }

        [Fact]
        public void APressParkedOnTheOnlyThingThereIsStaysOnIt()
        {
            string[] one = new string[] { "a" };
            Assert.Equal(0, ScannerWalk.Land(1, one, "a", false, true));
        }

        [Fact]
        public void ASweepStepsOnAndWrapsAtBothEnds()
        {
            Assert.Equal(2, ScannerWalk.Land(1, Three, "b", true, false));
            Assert.Equal(0, ScannerWalk.Land(1, Three, "c", true, false));
            Assert.Equal(1, ScannerWalk.Land(-1, Three, "c", true, false));
            Assert.Equal(2, ScannerWalk.Land(-1, Three, "a", true, false));
        }

        [Fact]
        public void AnEntryThatVanishedLandsOnTheEndThePressWasHeadingFor()
        {
            Assert.Equal(0, ScannerWalk.Land(1, Three, "gone", true, false));
            Assert.Equal(2, ScannerWalk.Land(-1, Three, "gone", true, false));
        }

        [Fact]
        public void TheSweepContinuesOnlyFromTheSameCategoryAndTheSamePlace()
        {
            ScannerWalk walk = new ScannerWalk();
            Assert.False(walk.Sweeping(0, 3.0, 4.0));

            walk.Anchor(0, 3.0, 4.0, Three);
            Assert.True(walk.Sweeping(0, 3.0, 4.0));
            Assert.False(walk.Sweeping(1, 3.0, 4.0));
            Assert.False(walk.Sweeping(0, 3.5, 4.0));
            Assert.False(walk.Sweeping(0, 3.0, -4.0));
        }

        [Fact]
        public void ForgettingEndsTheSweep()
        {
            ScannerWalk walk = new ScannerWalk();
            walk.Anchor(2, 1.0, 1.0, Three);
            walk.Forget();
            Assert.False(walk.Sweeping(2, 1.0, 1.0));
            Assert.Null(walk.Sweep);
        }

        [Fact]
        public void OutsideASweepTheOrderIsTheNearestFirstListItself()
        {
            Assert.Equal(new int[] { 0, 1, 2 }, ScannerWalk.Ordering(Three, null));
            Assert.Equal(new int[] { 0, 1, 2 }, ScannerWalk.Ordering(Three, new string[0]));
            Assert.Empty(ScannerWalk.Ordering(new string[0], Three));
        }

        [Fact]
        public void ASweepKeepsItsOrderWhileTheNearestFirstListReshuffles()
        {
            // The same three things, sorted from somewhere the walk's own landing moved to.
            string[] resorted = new string[] { "c", "a", "b" };
            Assert.Equal(new int[] { 1, 2, 0 }, ScannerWalk.Ordering(resorted, Three));
        }

        [Fact]
        public void ASweepDropsWhatWentAndTakesWhatArrivedAtTheEnd()
        {
            string[] now = new string[] { "d", "c", "a" };
            int[] order = ScannerWalk.Ordering(now, Three);
            Assert.Equal(new int[] { 2, 1, 0 }, order);
            Assert.Equal(now.Length, order.Length);
        }

        /// <summary>
        /// The defect of 2026-08-24: each landing moves the player, the list re-sorts from the new
        /// place, and a walk that steps in THAT list circles a handful of near entries instead of
        /// reaching the far side. Frozen, the same presses reach the end and wrap.
        /// </summary>
        [Fact]
        public void APressAfterALandingThatMovedTheReferenceStepsOnDownTheSameList()
        {
            string[] list = new string[] { "a", "b", "c", "d" };
            ScannerWalk walk = new ScannerWalk();

            // Press one: no sweep yet, the player is parked on the nearest thing, so it steps on.
            int at = ScannerWalk.Land(1, list, "a", walk.Sweeping(0, 0.0, 0.0), true);
            Assert.Equal(1, at);
            walk.Anchor(0, 1.0, 0.0, list);

            // Press two: the landing put the player at 1,0 and that is where they still are.
            string[] resorted = new string[] { "b", "a", "c", "d" };
            bool sweeping = walk.Sweeping(0, 1.0, 0.0);
            Assert.True(sweeping);
            int[] order = ScannerWalk.Ordering(resorted, walk.Sweep);
            string[] walked = Walked(resorted, order);
            Assert.Equal(list, walked);
            Assert.Equal(2, ScannerWalk.Land(1, walked, "b", sweeping, false));
        }

        [Fact]
        public void APlayerMoveStartsANewSweepAtTheNearestThing()
        {
            ScannerWalk walk = new ScannerWalk();
            walk.Anchor(0, 1.0, 0.0, Three);
            Assert.False(walk.Sweeping(0, 7.0, 7.0));
            Assert.Equal(0, ScannerWalk.Land(1, Three, "b", false, false));
        }

        private static string[] Walked(string[] keys, int[] order)
        {
            string[] taken = new string[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                taken[i] = keys[order[i]];
            }

            return taken;
        }
    }
}

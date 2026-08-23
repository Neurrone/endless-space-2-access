using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
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

            walk.Anchor(0, 3.0, 4.0);
            Assert.True(walk.Sweeping(0, 3.0, 4.0));
            Assert.False(walk.Sweeping(1, 3.0, 4.0));
            Assert.False(walk.Sweeping(0, 3.5, 4.0));
            Assert.False(walk.Sweeping(0, 3.0, -4.0));
        }

        [Fact]
        public void ForgettingEndsTheSweep()
        {
            ScannerWalk walk = new ScannerWalk();
            walk.Anchor(2, 1.0, 1.0);
            walk.Forget();
            Assert.False(walk.Sweeping(2, 1.0, 1.0));
        }
    }
}

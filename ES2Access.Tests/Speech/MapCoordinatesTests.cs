using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The pair of numbers a place on the map is said in. Two things can go wrong and neither ever
    /// sounds wrong: rounding each end before subtracting (which puts up to a whole unit of error into
    /// the difference the player actually steers by), and a midpoint that rounds towards zero on one
    /// side of home and away from it on the other (which makes the galaxy lopsided about its origin).
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class MapCoordinatesTests
    {
        public MapCoordinatesTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void HomeIsTheOrigin()
        {
            Assert.Equal("0, 0", MapCoordinates.Text(68.884, -22.45, 68.884, -22.45));
        }

        [Fact]
        public void EastIsPositiveAndNorthIsPositive()
        {
            Assert.Equal("10, 4", MapCoordinates.Text(10.0, 4.0, 0.0, 0.0));
            Assert.Equal("-10, -4", MapCoordinates.Text(-10.0, -4.0, 0.0, 0.0));
        }

        [Fact]
        public void TheOffsetIsMeasuredFromHomeBeforeItIsRounded()
        {
            // Both ends sit on a half unit: rounding first would give (5 - -5) = 10 rather than 9.
            Assert.Equal("9, 0", MapCoordinates.Text(4.5, 0.0, -4.5, 0.0));
        }

        [Theory]
        [InlineData(0.49, 0)]
        [InlineData(0.5, 1)]
        [InlineData(1.5, 2)]
        [InlineData(-0.49, 0)]
        [InlineData(-0.5, -1)]
        [InlineData(-1.5, -2)]
        public void MidpointsGoAwayFromZero(double offset, int spoken)
        {
            Assert.Equal(spoken, MapCoordinates.Round(offset));
        }

        [Fact]
        public void TheSeparatorComesFromTheStringTable()
        {
            ModStrings.Install(
                new System.Collections.Generic.Dictionary<string, string>
                {
                    { ModStrings.GalaxyCoordinatePair, "{1} north, {0} east" },
                }
            );
            Assert.Equal("33 north, -75 east", MapCoordinates.Text(-75.0, 33.0, 0.0, 0.0));
        }
    }
}

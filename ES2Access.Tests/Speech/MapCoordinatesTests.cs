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
        public void ReadingOrderRunsNorthToSouth()
        {
            Assert.True(MapCoordinates.ReadingOrder(0.0, 34.0, 0.0, 33.0) < 0);
            Assert.True(MapCoordinates.ReadingOrder(0.0, -42.0, 0.0, 0.0) > 0);
        }

        [Fact]
        public void ReadingOrderRunsWestToEastAlongARow()
        {
            Assert.True(MapCoordinates.ReadingOrder(-11.0, 11.0, 5.0, 11.0) < 0);
            Assert.True(MapCoordinates.ReadingOrder(23.0, 33.0, 5.0, 33.0) > 0);
        }

        [Fact]
        public void ARowIsAWholeSpokenUnitHigh()
        {
            // Both round to 11 north, so they are one row and the easting decides - however far apart
            // the raw northings are inside that unit.
            Assert.True(MapCoordinates.ReadingOrder(-11.0, 10.6, 5.0, 11.4) < 0);
        }

        [Fact]
        public void APairThatRoundsTheSameFallsBackToTheRawPosition()
        {
            Assert.True(MapCoordinates.ReadingOrder(3.0, 11.4, 3.0, 11.2) < 0);
            Assert.True(MapCoordinates.ReadingOrder(3.4, 11.0, 3.2, 11.0) > 0);
            Assert.Equal(0, MapCoordinates.ReadingOrder(3.0, 11.0, 3.0, 11.0));
        }

        [Fact]
        public void TheFixtureGalaxyReadsInOneList()
        {
            // The thirteen perceived systems of `[Beginner] test`, offered in the galaxy's own node
            // order, must come back in the order the map is read - colonies not held at the front.
            var places = new System.Collections.Generic.List<double[]>
            {
                new[] { 0.0, 0.0 },
                new[] { -1.0, -9.0 },
                new[] { 5.0, 34.0 },
                new[] { 23.0, 33.0 },
                new[] { -5.0, 23.0 },
                new[] { 17.0, 21.0 },
                new[] { -11.0, 11.0 },
                new[] { -16.0, -5.0 },
                new[] { -17.0, -21.0 },
                new[] { -5.0, -26.0 },
                new[] { -43.0, -30.0 },
                new[] { -31.0, -32.0 },
                new[] { -25.0, -42.0 },
            };
            places.Sort((a, b) => MapCoordinates.ReadingOrder(a[0], a[1], b[0], b[1]));
            Assert.Equal(
                "5,34 23,33 -5,23 17,21 -11,11 0,0 -16,-5 -1,-9 -17,-21 -5,-26 -43,-30 -31,-32 "
                    + "-25,-42",
                string.Join(
                    " ",
                    places.ConvertAll(p => p[0] + "," + p[1]).ToArray()
                )
            );
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

using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The compass words a starlane is named with. The whole of what can go wrong here is the arc
    /// boundaries: the words are arcs CENTRED on the compass points, so a lane leaving four degrees
    /// west of straight up is "north" and not "northwest", and getting the slicing wrong points a
    /// player at the wrong line on the map without ever sounding wrong.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class CompassDirectionsTests
    {
        public CompassDirectionsTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void APlaceDueNorthIsNorthAndTheBearingIsZero()
        {
            Assert.Equal(0.0, CompassDirections.Bearing(0.0, 1.0), 6);
            Assert.Equal("north", CompassDirections.Direction(0.0, 1.0));
        }

        [Theory]
        [InlineData(0.0, "north")]
        [InlineData(45.0, "northeast")]
        [InlineData(90.0, "east")]
        [InlineData(135.0, "southeast")]
        [InlineData(180.0, "south")]
        [InlineData(225.0, "southwest")]
        [InlineData(270.0, "west")]
        [InlineData(315.0, "northwest")]
        public void EachCompassPointIsTheMiddleOfItsOwnArc(double bearing, string word)
        {
            Assert.Equal(word, ModStrings.Get(CompassDirections.KeyForBearing(bearing)));
        }

        [Theory]
        [InlineData(22.4, "north")]
        [InlineData(22.5, "northeast")]
        [InlineData(67.4, "northeast")]
        [InlineData(67.5, "east")]
        [InlineData(337.4, "northwest")]
        [InlineData(337.5, "north")]
        [InlineData(359.9, "north")]
        public void AnArcEndsHalfWayToTheNextCompassPoint(double bearing, string word)
        {
            Assert.Equal(word, ModStrings.Get(CompassDirections.KeyForBearing(bearing)));
        }

        [Fact]
        public void EastIsPositiveAndTheBearingRunsClockwise()
        {
            Assert.Equal(90.0, CompassDirections.Bearing(1.0, 0.0), 6);
            Assert.Equal(180.0, CompassDirections.Bearing(0.0, -1.0), 6);
            Assert.Equal(270.0, CompassDirections.Bearing(-1.0, 0.0), 6);
            Assert.Equal(45.0, CompassDirections.Bearing(1.0, 1.0), 6);
        }

        [Fact]
        public void SomewhereExactlyWhereYouAreStandingReadsNorthRatherThanThrowing()
        {
            Assert.Equal(0.0, CompassDirections.Bearing(0.0, 0.0), 6);
            Assert.Equal("north", CompassDirections.Direction(0.0, 0.0));
        }

        [Fact]
        public void TheThreeLanesOutOfTheFixturesHomeSystemReadTheWayTheyAreDrawn()
        {
            Assert.Equal("northeast", ModStrings.Get(CompassDirections.KeyForBearing(38.3)));
            Assert.Equal("west", ModStrings.Get(CompassDirections.KeyForBearing(253.9)));
            Assert.Equal("north", ModStrings.Get(CompassDirections.KeyForBearing(347.8)));
        }
    }
}

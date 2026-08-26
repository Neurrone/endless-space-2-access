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

        // The sixteen-word compass, for a surface that AIMS. Same rule, half the arc: the failure to
        // catch is a word claiming an arc that belongs to its neighbour, which points a probe eleven
        // degrees off and sounds exactly right.

        [Theory]
        [InlineData(0.0, "north")]
        [InlineData(22.5, "north-northeast")]
        [InlineData(45.0, "northeast")]
        [InlineData(67.5, "east-northeast")]
        [InlineData(90.0, "east")]
        [InlineData(112.5, "east-southeast")]
        [InlineData(135.0, "southeast")]
        [InlineData(157.5, "south-southeast")]
        [InlineData(180.0, "south")]
        [InlineData(202.5, "south-southwest")]
        [InlineData(225.0, "southwest")]
        [InlineData(247.5, "west-southwest")]
        [InlineData(270.0, "west")]
        [InlineData(292.5, "west-northwest")]
        [InlineData(315.0, "northwest")]
        [InlineData(337.5, "north-northwest")]
        public void EachOfTheSixteenPointsIsTheMiddleOfItsOwnArc(double bearing, string word)
        {
            Assert.Equal(word, ModStrings.Get(CompassDirections.KeyForBearing16(bearing)));
        }

        [Theory]
        [InlineData(11.24, "north")]
        [InlineData(11.25, "north-northeast")]
        [InlineData(33.74, "north-northeast")]
        [InlineData(33.75, "northeast")]
        [InlineData(258.74, "west-southwest")]
        [InlineData(258.75, "west")]
        [InlineData(348.74, "north-northwest")]
        [InlineData(348.75, "north")]
        [InlineData(359.9, "north")]
        public void ASixteenthArcEndsHalfWayToTheNextPoint(double bearing, string word)
        {
            Assert.Equal(word, ModStrings.Get(CompassDirections.KeyForBearing16(bearing)));
        }

        [Fact]
        public void TheSixteenWordCompassLeavesTheEightWordOneAlone()
        {
            // Lanes and the scanner keep the eight words on purpose; the half-winds exist only where
            // something is being aimed. A bearing that has a half-wind of its own still reads as the
            // whole wind it is nearest to when it is being DESCRIBED.
            Assert.Equal("northeast", ModStrings.Get(CompassDirections.KeyForBearing(22.5)));
            Assert.Equal("north-northeast", ModStrings.Get(CompassDirections.KeyForBearing16(22.5)));
            Assert.Equal("north-northeast", CompassDirections.Direction16(1.0, 2.4142135));
        }

        // The OTHER way of saying which way something lies: the two components, which is what the
        // scanner says. The failure that matters here is silent - a component said in the wrong order,
        // or a zero component said as "0 east", still sounds like an answer.

        [Fact]
        public void SomethingDueSouthSaysOneComponent()
        {
            Assert.Equal("23 south", CompassDirections.Offsets(0, -23));
            Assert.Equal("9 north", CompassDirections.Offsets(0, 9));
        }

        [Fact]
        public void SomethingDueEastOrWestSaysOneComponentWithNoLeadingComma()
        {
            Assert.Equal("4 east", CompassDirections.Offsets(4, 0));
            Assert.Equal("17 west", CompassDirections.Offsets(-17, 0));
        }

        [Fact]
        public void SomethingOffAxisSaysEastWestFirstThenNorthSouth()
        {
            // The order the spoken PAIR itself is in (MapCoordinates.Text), so a listener holds one
            // order for where things are and for how far apart they are (owner ruling 2026-08-26).
            Assert.Equal("1 west, 23 south", CompassDirections.Offsets(-1, -23));
            Assert.Equal("5 east, 16 north", CompassDirections.Offsets(5, 16));
            Assert.Equal("71 west, 23 north", CompassDirections.Offsets(-71, 23));
        }

        [Fact]
        public void SomethingOnTheSamePairHasNoComponentsAtAll()
        {
            // The caller says whatever "here" means to it; this answers with nothing rather than with
            // a pair of zeroes.
            Assert.Null(CompassDirections.Offsets(0, 0));
        }
    }
}

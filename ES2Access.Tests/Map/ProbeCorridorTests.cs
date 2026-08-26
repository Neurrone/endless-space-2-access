using System;
using System.Text;
using ES2Access.Core.Map;
using Xunit;

namespace ES2Access.Tests.Map
{
    /// <summary>
    /// What a probe would fly through down one bearing. Everything here is a way the marching can be
    /// wrong without sounding wrong: a run of fog split in two because a flank was not looked at, two
    /// runs merged because the explored unit between them was skipped, a range that stops a unit short
    /// of the rim, or a rim rounded twice into two different numbers.
    ///
    /// The span lists in the named cases were measured in the live galaxy; they are the strings the
    /// player actually heard, held here so the geometry can never quietly stop producing them.
    /// </summary>
    public class ProbeCorridorTests
    {
        /// <summary>A galaxy that is a wide square, so a heading out of the middle has room to run.
        /// </summary>
        private static ConvexHull Galaxy()
        {
            return ConvexHull.Build(
                new[]
                {
                    new MapPoint(-100, -100),
                    new MapPoint(100, -100),
                    new MapPoint(100, 100),
                    new MapPoint(-100, 100),
                }
            );
        }

        /// <summary>Fog in the given half-open stretches of the heading, the same everywhere across
        /// the corridor - the case where the three samples always agree.</summary>
        private static MapExplored Fog(params int[] bounds)
        {
            return delegate(double east, double north)
            {
                int step = (int)Math.Round(north);
                for (int i = 0; i + 1 < bounds.Length; i += 2)
                {
                    if (step >= bounds[i] && step < bounds[i + 1])
                    {
                        return false;
                    }
                }

                return true;
            };
        }

        private static string Spans(ProbeCorridorReading reading)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < reading.Spans.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }

                text.Append(reading.Spans[i]);
            }

            return text.ToString();
        }

        private static ProbeCorridorReading North(double edge, MapExplored explored)
        {
            return ProbeCorridor.Read(new MapPoint(0, 0), 0.0, edge, 2.0, explored);
        }

        [Fact]
        public void TheSouthwestHeadingOfTheFixtureReadsAsSixStretches()
        {
            ProbeCorridorReading reading = North(
                58,
                Fog(12, 15, 17, 18, 19, 26, 31, 35, 38, 44, 46, 58)
            );

            Assert.Equal("12-15, 17-18, 19-26, 31-35, 38-44, 46-58", Spans(reading));
            Assert.Equal(58, reading.Edge);
            Assert.True(reading.ReachesEdge);
        }

        [Fact]
        public void AStretchEndingOneUnitBeforeTheNextBeginsStaysTwoStretches()
        {
            // 17-18 and 19-26 touch at a single explored unit. Losing that unit merges them and the
            // player is told to launch into a wall.
            Assert.Equal("17-18, 19-26", Spans(North(30, Fog(17, 18, 19, 26))));
        }

        [Fact]
        public void OneUnitOfFogIsAStretchOfItsOwn()
        {
            Assert.Equal("17-18", Spans(North(30, Fog(17, 18))));
        }

        [Fact]
        public void FogAllTheWayToTheRimEndsOnTheRimsOwnNumber()
        {
            ProbeCorridorReading reading = North(51, Fog(7, 51));

            Assert.Equal("7-51", Spans(reading));
            Assert.True(reading.ReachesEdge);
        }

        [Fact]
        public void FogStoppingShortOfTheRimDoesNotReachIt()
        {
            ProbeCorridorReading reading = North(51, Fog(7, 50));

            Assert.Equal("7-50", Spans(reading));
            Assert.False(reading.ReachesEdge);
        }

        [Fact]
        public void AGalaxyExploredThatWayHasNoStretchesAtAll()
        {
            ProbeCorridorReading reading = North(40, Fog());

            Assert.Empty(reading.Spans);
            Assert.False(reading.ReachesEdge);
            Assert.Equal(40, reading.Edge);
        }

        [Fact]
        public void FogStartingAtTheSystemItselfStartsAtZero()
        {
            Assert.Equal("0-6", Spans(North(40, Fog(0, 6))));
        }

        [Fact]
        public void FogOnAFlankCountsEvenWhereTheLineItselfIsExplored()
        {
            // The live north heading: a pocket of explored map at 28-30 sits on the line and the fog
            // to one side of it runs straight past. A probe launched into that pocket reveals nothing
            // new, so the pocket does not break the stretch.
            MapExplored explored = delegate(double east, double north)
            {
                int step = (int)Math.Round(north);
                if (step < 7 || step >= 51)
                {
                    return true;
                }

                return step >= 28 && step <= 30 && east > -1;
            };

            ProbeCorridorReading reading = North(51, explored);

            Assert.Equal("7-51", Spans(reading));
        }

        [Fact]
        public void AWiderCorridorIsSampledFurtherOut()
        {
            // Fog confined to a narrow band beside the line: a corridor that does not reach it says
            // the heading is clear, and one that does says it is not.
            MapExplored explored = delegate(double east, double north)
            {
                int step = (int)Math.Round(north);
                return !(step >= 10 && step < 14 && east > 2.5 && east < 3.5);
            };

            Assert.Empty(ProbeCorridor.Read(new MapPoint(0, 0), 0, 40, 2.0, explored).Spans);
            Assert.Equal(
                "10-14",
                Spans(ProbeCorridor.Read(new MapPoint(0, 0), 0, 40, 3.0, explored))
            );
        }

        [Fact]
        public void TheRimIsRoundedOnceAndEverythingIsMeasuredAgainstThatNumber()
        {
            Assert.Equal(58, North(57.6, Fog()).Edge);
            Assert.Equal(57, North(57.4, Fog()).Edge);
            Assert.Equal(58, North(57.5, Fog()).Edge);
        }

        [Fact]
        public void ASystemOnTheRimHasHeadingsWithNothingDownThem()
        {
            ProbeCorridorReading nothing = North(0.4, Fog(0, 10));

            Assert.Equal(0, nothing.Edge);
            Assert.Empty(nothing.Spans);
            Assert.False(nothing.ReachesEdge);

            ProbeCorridorReading barely = North(1.2, Fog(0, 10));
            Assert.Equal(1, barely.Edge);
            Assert.Equal("0-1", Spans(barely));
            Assert.True(barely.ReachesEdge);
        }

        [Fact]
        public void ADiagonalHeadingWalksBothAxesAndStopsAtTheGalaxysRim()
        {
            // Northeast out of the middle of a 200-wide square: the rim is a corner's distance away,
            // and the fog is measured along the diagonal rather than along either axis.
            MapExplored explored = delegate(double east, double north)
            {
                double along = Math.Sqrt(east * east + north * north);
                return along < 9.5 || along > 20.5;
            };

            ProbeCorridorReading reading = ProbeCorridor.Read(
                Galaxy(),
                new MapPoint(0, 0),
                45.0,
                0.0,
                explored
            );

            Assert.Equal(141, reading.Edge);
            Assert.Equal("10-21", Spans(reading));
        }

        [Fact]
        public void TheGalaxysOwnOutlineSuppliesTheRim()
        {
            ProbeCorridorReading reading = ProbeCorridor.Read(
                Galaxy(),
                new MapPoint(0, 60),
                0.0,
                2.0,
                Fog()
            );

            Assert.Equal(40, reading.Edge);
        }
    }
}

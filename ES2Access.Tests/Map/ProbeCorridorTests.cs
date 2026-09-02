using System;
using System.Collections.Generic;
using System.Text;
using ES2Access.Core.Map;
using Xunit;
using static ES2Access.Tests.Map.ProbeFixture;

namespace ES2Access.Tests.Map
{
    /// <summary>
    /// What a probe would fly through down one bearing. Everything here is a way the marching can be
    /// wrong without sounding wrong: a run of fog split in two because a flank was not looked at, two
    /// runs merged because the explored unit between them was skipped, a range that stops a unit short
    /// of the rim, or a rim rounded twice into two different numbers.
    ///
    /// And the way it WAS wrong: fog beside the flight line reported as fog on it, which turned a
    /// heading a probe would find nothing down into the most promising one on the compass.
    ///
    /// The span lists in the named cases were measured in the live galaxy; they are the strings the
    /// player actually heard, held here so the geometry can never quietly stop producing them.
    /// </summary>
    public class ProbeCorridorTests
    {
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

        private static string Text(IList<UnexploredSpan> spans)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < spans.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }

                text.Append(spans[i]);
            }

            return text.ToString();
        }

        private static string Spans(ProbeCorridorReading reading)
        {
            return Text(reading.Spans);
        }

        private static ProbeCorridorReading North(double edge, MapExplored explored)
        {
            return North(edge, 2.0, explored);
        }

        private static ProbeCorridorReading North(
            double edge,
            double halfWidth,
            MapExplored explored
        )
        {
            return ProbeCorridor.Read(Galaxy(), Home, Home, 0.0, edge, halfWidth, explored);
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
            Assert.Empty(reading.Clockwise);
            Assert.Empty(reading.CounterClockwise);
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
            Assert.Empty(reading.Clockwise);
            Assert.Empty(reading.CounterClockwise);
            Assert.False(reading.ReachesEdge);
            Assert.Equal(40, reading.Edge);
        }

        [Fact]
        public void FogStartingAtTheSystemItselfStartsAtZero()
        {
            Assert.Equal("0-6", Spans(North(40, Fog(0, 6))));
        }

        [Fact]
        public void AFogEdgeRunningParallelToTheHeadingLeavesTheFlightLineClear()
        {
            // The case that provoked the redesign: the live southeast bearing had a fog edge running
            // alongside it, just inside the corridor. Read as one corridor it said the whole heading
            // was unexplored from the system outwards; the line itself was known the entire way.
            // The fog starts at the outermost tile a half-width of 3.5 reaches, three units out.
            MapExplored explored = delegate(double east, double north)
            {
                return east > -3.0 || north < 2;
            };

            ProbeCorridorReading reading = North(40, 3.5, explored);

            Assert.Empty(reading.Spans);
            Assert.Empty(reading.Clockwise);
            Assert.Equal("2-40", Text(reading.CounterClockwise));
        }

        [Fact]
        public void FogOnBothFlanksOfAKnownLineIsReadOnBothSides()
        {
            // A sliver of explored map the width of the line with fog either side of it.
            MapExplored explored = delegate(double east, double north)
            {
                return !(north >= 5 && north < 9 && Math.Abs(east) > 1);
            };

            ProbeCorridorReading reading = North(40, explored);

            Assert.Empty(reading.Spans);
            Assert.Equal("5-9", Text(reading.Clockwise));
            Assert.Equal("5-9", Text(reading.CounterClockwise));
        }

        [Fact]
        public void NothingIsSaidAlongsideAStretchWhereTheLineItselfIsDark()
        {
            // Fog on one flank the whole way and fog across everything from 5 to 10. Over 5-10 the
            // player is being sent into the dark anyway, so the flank there is not a separate fact -
            // and the alongside stretches break around it rather than running through it.
            MapExplored explored = delegate(double east, double north)
            {
                if (north >= 5 && north < 10)
                {
                    return false;
                }

                return east < 1;
            };

            ProbeCorridorReading reading = North(20, explored);

            Assert.Equal("5-10", Spans(reading));
            Assert.Equal("0-5, 10-20", Text(reading.Clockwise));
            Assert.Empty(reading.CounterClockwise);
        }

        [Fact]
        public void TheClockwiseSideOfDueNorthIsTheEastOne()
        {
            // Which side a stretch is on is half of what the sentence says, and the two sides are one
            // sign apart in the geometry - so the sign is pinned against a heading whose sides have
            // names everybody agrees on.
            MapExplored explored = delegate(double east, double north)
            {
                return east < 1;
            };

            ProbeCorridorReading reading = North(20, explored);

            Assert.Equal("0-20", Text(reading.Clockwise));
            Assert.Empty(reading.CounterClockwise);
        }

        [Fact]
        public void AWiderCorridorReachesFurtherAlongside()
        {
            // Fog confined to a narrow band beside the line: a corridor that does not reach it says
            // the heading is clear either way, and one that does reports it as alongside - never as
            // fog on the line, which is clear in both.
            MapExplored explored = delegate(double east, double north)
            {
                int step = (int)Math.Round(north);
                return !(step >= 10 && step < 14 && east > 1.5 && east < 3.5);
            };

            ProbeCorridorReading narrow = North(40, 1.0, explored);
            Assert.Empty(narrow.Spans);
            Assert.Empty(narrow.Clockwise);

            ProbeCorridorReading wide = North(40, 3.0, explored);
            Assert.Empty(wide.Spans);
            Assert.Equal("10-14", Text(wide.Clockwise));
        }

        [Fact]
        public void AFlankIsSampledAtTheCorridorsEdgeAndNeverPastIt()
        {
            // A half-width of 3.5 out of a lattice point reaches the tile three units out and not the
            // one four units out. Fog at four is map the probe would fly straight past - the share
            // leading the same sentence does not count it, so the alongside clause may not name it
            // either (the old sampling did: it took the flank at exactly 3.5 and rounded that to 4).
            MapExplored beyond = delegate(double east, double north)
            {
                return !(Math.Round(east) == 4 && north >= 5 && north < 9);
            };

            Assert.Empty(North(40, 3.5, beyond).Clockwise);

            // Widen the corridor until it does reach that tile and the same fog is heard - the tile
            // is skipped for being outside the vision circle, not for being unreadable.
            Assert.Equal("5-9", Text(North(40, 4.0, beyond).Clockwise));

            // And the outermost tile the circle DOES reach is sampled, so fog at the corridor's own
            // edge is never lost by pulling the sample in.
            MapExplored edge = delegate(double east, double north)
            {
                return !(Math.Round(east) == 3 && north >= 5 && north < 9);
            };

            Assert.Equal("5-9", Text(North(40, 3.5, edge).Clockwise));
        }

        [Fact]
        public void AFlankStopsAtTheEdgeOfTheMapAsWellAsTheEdgeOfTheCorridor()
        {
            // A galaxy whose frame ends two units east of the heading, with a corridor three and a
            // half units wide: the probe's circle reaches the tile three units out, the map does not,
            // and the share does not count it - so neither may the alongside clause. The sample steps
            // in to the outermost tile that is on the map AND in the circle, which is the one at two.
            ConvexHull narrow = ConvexHull.Build(
                new[]
                {
                    new MapPoint(-100, -100),
                    new MapPoint(2, -100),
                    new MapPoint(2, 100),
                    new MapPoint(-100, 100),
                }
            );

            MapExplored offTheMap = delegate(double east, double north)
            {
                return !(Math.Round(east) == 3 && north >= 5 && north < 9);
            };

            Assert.Empty(
                ProbeCorridor.Read(narrow, Home, Home, 0.0, 40, 3.5, offTheMap).Clockwise
            );

            // The same fog on a frame that holds it is heard, so the tile is skipped for being off
            // the map, not for being out of reach of the search.
            Assert.Equal(
                "5-9",
                Text(ProbeCorridor.Read(Galaxy(), Home, Home, 0.0, 40, 3.5, offTheMap).Clockwise)
            );

            // And the outermost tile that is on the map decides instead - it is not skipped along
            // with the tile beyond it.
            MapExplored atTheEdge = delegate(double east, double north)
            {
                return !(Math.Round(east) == 2 && north >= 5 && north < 9);
            };

            Assert.Equal(
                "5-9",
                Text(ProbeCorridor.Read(narrow, Home, Home, 0.0, 40, 3.5, atTheEdge).Clockwise)
            );
        }

        [Fact]
        public void WithinTheProbesReachAFullyExploredShareLeavesNothingAlongsideToSay()
        {
            // The two halves of a bearing's sentence, over the stretch they share. The empire here has
            // explored exactly the probe's own footprint and nothing else, so the share reads 100
            // percent - and no alongside stretch may then begin inside the reach. Past the reach they
            // may, and do: the stretches deliberately run on to the rim (docs/galaxy-map.md).
            Assert.NotEmpty(ShareAndStretchesAgree(Galaxy(), 22.5).Clockwise);

            // And the same where the frame cuts along the corridor, taking the east flank off the map
            // from the system outwards. Nothing off the map is explored here either, so any tile the
            // clause samples that the share would not count comes back dark and breaks the fact.
            ShareAndStretchesAgree(
                ConvexHull.Build(
                    new[]
                    {
                        new MapPoint(-100, -100),
                        new MapPoint(2, -100),
                        new MapPoint(2, 100),
                        new MapPoint(-100, 100),
                    }
                ),
                0.0
            );
        }

        private static ProbeCorridorReading ShareAndStretchesAgree(
            ConvexHull frame,
            double bearing
        )
        {
            const double Reach = 30;
            const double HalfWidth = 3.5;

            double radians = bearing * Math.PI / 180.0;
            MapPoint origin = new MapPoint(0.25, -0.5);
            MapPoint tip = new MapPoint(
                origin.X + Math.Sin(radians) * Reach,
                origin.Y + Math.Cos(radians) * Reach
            );
            MapExplored explored = delegate(double east, double north)
            {
                MapPoint tile = new MapPoint(east, north);
                return tile.SquaredDistanceToSegment(origin, tip) <= HalfWidth * HalfWidth + 1e-9
                    && frame.Contains(tile);
            };

            ProbeFootprint footprint = ProbeFootprint.Read(
                frame,
                origin,
                origin,
                bearing,
                Reach,
                HalfWidth,
                explored
            );

            Assert.Equal(100, footprint.PercentExplored);
            Assert.True(footprint.Tiles > 100, "the footprint is a real corridor, not an empty one");

            ProbeCorridorReading reading = ProbeCorridor.Read(
                frame,
                origin,
                origin,
                bearing,
                HalfWidth,
                explored
            );

            NoneBefore(reading.Clockwise, (int)Reach);
            NoneBefore(reading.CounterClockwise, (int)Reach);
            return reading;
        }

        private static void NoneBefore(IList<UnexploredSpan> spans, int reach)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                Assert.True(
                    spans[i].From >= reach,
                    "alongside stretch " + spans[i] + " begins inside the probe's reach"
                );
            }
        }

        [Fact]
        public void EveryFogSampleLandsOnTheAnchorsOwnLattice()
        {
            // The promise that makes the numbers checkable: a bearing's stretches are claims about the
            // same tiles the inspect cursor counts fog in, so no sample may fall between them.
            MapPoint anchor = new MapPoint(0.25, -0.5);
            MapExplored explored = delegate(double east, double north)
            {
                Lattice(east - anchor.X, "east");
                Lattice(north - anchor.Y, "north");
                return true;
            };

            ProbeCorridorReading reading = ProbeCorridor.Read(
                Galaxy(),
                new MapPoint(0.25, -0.5),
                anchor,
                22.5,
                40,
                3.5,
                explored
            );

            Assert.Empty(reading.Spans);
        }

        [Fact]
        public void AnAnchorConsistentGalaxyReadsTheSameFromAnyAnchor()
        {
            // Only the fractional part of the anchor matters, and only because it decides which tiles
            // exist - so a galaxy whose fog is laid out on the anchor's own tiles reads identically
            // wherever the anchor is put.
            MapPoint[] anchors = new[]
            {
                new MapPoint(0, 0),
                new MapPoint(0.37, -0.62),
                new MapPoint(-2, 5),
            };

            for (int i = 0; i < anchors.Length; i++)
            {
                MapPoint anchor = anchors[i];
                MapPoint at = anchor;
                MapExplored explored = delegate(double east, double north)
                {
                    double step = Math.Round(north - at.Y);
                    return !(step >= 12 && step < 15);
                };

                ProbeCorridorReading reading = ProbeCorridor.Read(
                    Galaxy(),
                    anchor,
                    anchor,
                    0.0,
                    40,
                    2.0,
                    explored
                );

                Assert.Equal("12-15", Spans(reading));
            }
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
                Home,
                Home,
                45.0,
                0.0,
                explored
            );

            Assert.Equal(141, reading.Edge);
            Assert.Equal("10-21", Spans(reading));
        }

        [Fact]
        public void TheHeadingIsKeptSoThatItsSidesCanBeNamed()
        {
            Assert.Equal(
                225.0,
                ProbeCorridor.Read(Galaxy(), Home, Home, 225.0, 2.0, Fog()).Bearing
            );
        }

        [Fact]
        public void TheGalaxysOwnOutlineSuppliesTheRim()
        {
            ProbeCorridorReading reading = ProbeCorridor.Read(
                Galaxy(),
                new MapPoint(0, 60),
                Home,
                0.0,
                2.0,
                Fog()
            );

            Assert.Equal(40, reading.Edge);
        }
    }
}

using System;
using System.Collections.Generic;
using ES2Access.Core.Map;
using ES2Access.ES2.Map;
using Xunit;
using static ES2Access.Tests.ES2.Map.ProbeFixture;

namespace ES2Access.Tests.ES2.Map
{
    /// <summary>
    /// How much of what a probe would reveal is already known - the one number the player compares
    /// sixteen bearings on. Every galaxy here is small enough to count by eye, because the ways this
    /// can be wrong are all silent: a diagonal counting the same tile twice reads as a lower share
    /// than the map holds, a footprint run to the map's rim instead of to the probe's reach reads as
    /// darker than the launch would find, and either one is a perfectly ordinary-sounding percentage.
    /// </summary>
    public class ProbeFootprintTests
    {
        private static readonly MapExplored Everywhere = delegate(double east, double north)
        {
            return true;
        };

        private static ProbeFootprint North(double reach, double halfWidth, MapExplored explored)
        {
            return ProbeFootprint.Read(Galaxy(), Home, Home, 0.0, reach, halfWidth, explored);
        }

        [Fact]
        public void TheFlightLineItselfIsTheSystemAndEveryUnitOutToTheProbesReach()
        {
            // Five units of flight is six tiles: the one the probe leaves from and the five it crosses.
            ProbeFootprint footprint = North(5, 0, Everywhere);

            Assert.Equal(6, footprint.Tiles);
            Assert.Equal(6, footprint.Explored);
            Assert.Equal(100, footprint.PercentExplored);
        }

        [Fact]
        public void TheShareIsOfTheFootprintAndNotOfTheFlightLine()
        {
            MapExplored explored = delegate(double east, double north)
            {
                return north < 3;
            };

            ProbeFootprint footprint = North(5, 0, explored);

            Assert.Equal(6, footprint.Tiles);
            Assert.Equal(3, footprint.Explored);
            Assert.Equal(50, footprint.PercentExplored);
        }

        [Fact]
        public void TheVisionRadiusWidensTheFootprintAndRoundsOffBothEnds()
        {
            // Two units of flight with a one-unit circle around it: the line's three tiles, one tile
            // beyond each end, and three either side. Eleven, and none of the corners.
            ProbeFootprint footprint = North(2, 1, Everywhere);

            Assert.Equal(11, footprint.Tiles);
        }

        [Fact]
        public void ATileIsCountedOnceHoweverTheBearingCrossesIt()
        {
            // A diagonal marched a unit at a time lands twice on the same tile and then overshoots
            // past the reach - four tiles where the geometry holds three. Counting the lattice itself
            // is what makes the number reproducible from the map.
            ProbeFootprint footprint = ProbeFootprint.Read(
                Galaxy(),
                Home,
                Home,
                45.0,
                4,
                0,
                Everywhere
            );

            Assert.Equal(3, footprint.Tiles);
        }

        [Fact]
        public void EveryTileIsAskedAboutExactlyOnce()
        {
            List<string> asked = new List<string>();
            MapExplored explored = delegate(double east, double north)
            {
                asked.Add(east + "," + north);
                return true;
            };

            ProbeFootprint footprint = ProbeFootprint.Read(
                Galaxy(),
                Home,
                Home,
                45.0,
                12,
                2.5,
                explored
            );

            Assert.Equal(footprint.Tiles, asked.Count);
            Assert.Equal(asked.Count, new List<string>(Distinct(asked)).Count);
        }

        private static IEnumerable<string> Distinct(IList<string> items)
        {
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            for (int i = 0; i < items.Count; i++)
            {
                if (!seen.ContainsKey(items[i]))
                {
                    seen[items[i]] = true;
                    yield return items[i];
                }
            }
        }

        [Fact]
        public void TheProbesReachEndsTheFootprintLongBeforeTheMapDoes()
        {
            Assert.Equal(4, North(3, 0, Everywhere).Tiles);
            Assert.Equal(11, North(10, 0, Everywhere).Tiles);
        }

        [Fact]
        public void AReachRunningPastTheRimIsCutOffAtTheRim()
        {
            // The ranges the same bearing is spoken with run to the map's edge; the share is of what
            // the probe would actually light up, and the map ending is one of the two things that
            // stops it.
            ProbeFootprint footprint = ProbeFootprint.Read(
                Galaxy(2),
                Home,
                Home,
                0.0,
                10,
                0,
                Everywhere
            );

            Assert.Equal(3, footprint.Tiles);
        }

        [Fact]
        public void NoTileOutsideTheMapIsCountedEvenBesideTheLine()
        {
            // A corridor wider than the galaxy: the frame clips the flanks as well as the flight.
            ProbeFootprint footprint = ProbeFootprint.Read(
                Galaxy(1),
                Home,
                Home,
                0.0,
                1,
                5,
                Everywhere
            );

            Assert.Equal(9, footprint.Tiles);
        }

        [Fact]
        public void TheFogIsAskedOnTheAnchorsOwnLattice()
        {
            // The promise the ranges make too: the share is over tiles the player can steer the
            // inspect cursor onto, so no sample may fall between them.
            MapPoint anchor = new MapPoint(0.25, -0.5);
            MapExplored explored = delegate(double east, double north)
            {
                Lattice(east - anchor.X, "east");
                Lattice(north - anchor.Y, "north");
                return true;
            };

            ProbeFootprint footprint = ProbeFootprint.Read(
                Galaxy(),
                anchor,
                anchor,
                22.5,
                10,
                2.5,
                explored
            );

            Assert.True(footprint.Tiles > 0);
        }

        [Fact]
        public void AnchorsAWholeNumberApartDescribeTheSameTiles()
        {
            MapPoint[] anchors = new[]
            {
                new MapPoint(0, 0),
                new MapPoint(-2, 5),
                new MapPoint(37, -62),
            };

            for (int i = 0; i < anchors.Length; i++)
            {
                Assert.Equal(
                    11,
                    ProbeFootprint
                        .Read(Galaxy(), Home, anchors[i], 0.0, 2, 1, Everywhere)
                        .Tiles
                );
            }
        }

        [Fact]
        public void TheShareIsWholePercentWithHalvesRoundedUp()
        {
            Assert.Equal(13, new ProbeFootprint(1, 8).PercentExplored);
            Assert.Equal(33, new ProbeFootprint(1, 3).PercentExplored);
            Assert.Equal(67, new ProbeFootprint(2, 3).PercentExplored);
            Assert.Equal(0, new ProbeFootprint(0, 40).PercentExplored);
            Assert.Equal(100, new ProbeFootprint(40, 40).PercentExplored);
        }

        [Fact]
        public void AFootprintWithNoTilesInItHasNothingLeftToFind()
        {
            // A system on the very rim aimed off the map: the bearing's own clause says "fully
            // explored to the map edge at 0" in the same breath, and a share of zero would contradict
            // it.
            Assert.Equal(100, new ProbeFootprint(0, 0).PercentExplored);
        }
    }
}

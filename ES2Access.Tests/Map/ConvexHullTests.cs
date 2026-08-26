using ES2Access.Core.Map;
using Xunit;

namespace ES2Access.Tests.Map
{
    /// <summary>
    /// The shape of a constellation, and the two questions asked of it per keystroke: does it hold
    /// this place, and if not, how far out is it.
    ///
    /// Each case below is a way a galaxy breaks a naive outline: a constellation strung along one
    /// line, a constellation of a single star, two stars sharing a position exactly, and the
    /// float-wobble band where a star sits on its own constellation's rim.
    /// </summary>
    public class ConvexHullTests
    {
        private static ConvexHull Hull(params double[] coordinates)
        {
            MapPoint[] places = new MapPoint[coordinates.Length / 2];
            for (int i = 0; i < places.Length; i++)
            {
                places[i] = new MapPoint(coordinates[2 * i], coordinates[2 * i + 1]);
            }

            return ConvexHull.Build(places);
        }

        private static bool HasCorner(ConvexHull hull, double x, double y)
        {
            for (int i = 0; i < hull.Count; i++)
            {
                if (hull.Vertex(i).X == x && hull.Vertex(i).Y == y)
                {
                    return true;
                }
            }

            return false;
        }

        [Fact]
        public void ASquareKeepsItsFourCornersAndDropsWhatSitsInside()
        {
            ConvexHull hull = Hull(0, 0, 10, 0, 10, 10, 0, 10, 5, 5);

            Assert.Equal(4, hull.Count);
            Assert.True(HasCorner(hull, 0, 0));
            Assert.True(HasCorner(hull, 10, 0));
            Assert.True(HasCorner(hull, 10, 10));
            Assert.True(HasCorner(hull, 0, 10));
        }

        [Fact]
        public void ATriangleKeepsItsThreeCorners()
        {
            ConvexHull hull = Hull(0, 0, 6, 0, 3, 4);

            Assert.Equal(3, hull.Count);
            Assert.True(HasCorner(hull, 0, 0));
            Assert.True(HasCorner(hull, 6, 0));
            Assert.True(HasCorner(hull, 3, 4));
        }

        [Fact]
        public void APlaceInTheMiddleOfAStraightEdgeIsNoCorner()
        {
            ConvexHull hull = Hull(0, 0, 5, 0, 10, 0, 10, 10, 0, 10);

            Assert.Equal(4, hull.Count);
            Assert.False(HasCorner(hull, 5, 0));
        }

        [Fact]
        public void PlacesAlongOneLineCollapseToTheirEndpoints()
        {
            ConvexHull hull = Hull(0, 0, 1, 1, 2, 2, 3, 3);

            Assert.Equal(2, hull.Count);
            Assert.True(HasCorner(hull, 0, 0));
            Assert.True(HasCorner(hull, 3, 3));
        }

        [Fact]
        public void ASinglePlaceIsItsOwnOutline()
        {
            ConvexHull hull = Hull(2, 2);

            Assert.Equal(1, hull.Count);
            Assert.True(hull.Contains(new MapPoint(2, 2)));
            Assert.False(hull.Contains(new MapPoint(2, 2.5)));
        }

        [Fact]
        public void TwoPlacesAreASegment()
        {
            ConvexHull hull = Hull(0, 0, 10, 0);

            Assert.Equal(2, hull.Count);
            Assert.True(HasCorner(hull, 0, 0));
            Assert.True(HasCorner(hull, 10, 0));
        }

        [Fact]
        public void TheSamePlaceTwiceIsOnePlace()
        {
            ConvexHull hull = Hull(4, 4, 4, 4);

            Assert.Equal(1, hull.Count);
        }

        [Fact]
        public void AnOutlineDoesNotDependOnTheOrderThePlacesArriveIn()
        {
            ConvexHull one = Hull(0, 0, 10, 0, 10, 10, 0, 10, 5, 5);
            ConvexHull other = Hull(5, 5, 0, 10, 10, 10, 0, 0, 10, 0);

            Assert.Equal(one.Count, other.Count);
            for (int i = 0; i < one.Count; i++)
            {
                Assert.Equal(one.Vertex(i).X, other.Vertex(i).X);
                Assert.Equal(one.Vertex(i).Y, other.Vertex(i).Y);
            }
        }

        [Fact]
        public void AnEmptyScatterHoldsNothingAndIsInfinitelyFarFromEverywhere()
        {
            ConvexHull hull = ConvexHull.Build(new MapPoint[0]);

            Assert.Equal(0, hull.Count);
            Assert.False(hull.Contains(new MapPoint(0, 0)));
            Assert.Equal(double.PositiveInfinity, hull.DistanceTo(new MapPoint(0, 0)));
        }

        [Fact]
        public void InsideIsInside()
        {
            Assert.True(Hull(0, 0, 10, 0, 10, 10, 0, 10).Contains(new MapPoint(5, 5)));
        }

        [Fact]
        public void OutsideIsOutside()
        {
            Assert.False(Hull(0, 0, 10, 0, 10, 10, 0, 10).Contains(new MapPoint(11, 5)));
        }

        [Fact]
        public void ACornerCountsAsInside()
        {
            Assert.True(Hull(0, 0, 10, 0, 10, 10, 0, 10).Contains(new MapPoint(10, 10)));
        }

        [Fact]
        public void APlaceOnAnEdgeCountsAsInside()
        {
            Assert.True(Hull(0, 0, 10, 0, 10, 10, 0, 10).Contains(new MapPoint(10, 5)));
        }

        [Fact]
        public void MissingTheEdgeByLessThanTheEpsilonStillCounts()
        {
            Assert.True(Hull(0, 0, 10, 0, 10, 10, 0, 10).Contains(new MapPoint(10 + 1e-9, 5)));
        }

        [Fact]
        public void MissingACornerByLessThanTheEpsilonAlsoCounts()
        {
            Assert.True(Hull(0, 0, 10, 0, 10, 10, 0, 10).Contains(new MapPoint(10 + 1e-9, 10 + 1e-9)));
        }

        [Fact]
        public void FurtherOutThanTheEpsilonDoesNotCount()
        {
            ConvexHull hull = Hull(0, 0, 10, 0, 10, 10, 0, 10);

            Assert.False(hull.Contains(new MapPoint(10.001, 5)));
            Assert.True(hull.Contains(new MapPoint(10.001, 5), 0.01));
        }

        [Fact]
        public void ASegmentHoldsOnlyItsOwnLine()
        {
            ConvexHull hull = Hull(0, 0, 10, 0);

            Assert.True(hull.Contains(new MapPoint(5, 0)));
            Assert.False(hull.Contains(new MapPoint(5, 1)));
        }

        [Fact]
        public void InsideIsNoDistanceAtAll()
        {
            Assert.Equal(0.0, Hull(0, 0, 10, 0, 10, 10, 0, 10).DistanceTo(new MapPoint(5, 5)));
        }

        [Fact]
        public void OutsideMeasuresThePerpendicularDropToTheNearestEdge()
        {
            Assert.Equal(3.0, Hull(0, 0, 10, 0, 10, 10, 0, 10).DistanceTo(new MapPoint(13, 5)), 9);
        }

        [Fact]
        public void PastACornerItMeasuresToTheCorner()
        {
            Assert.Equal(5.0, Hull(0, 0, 10, 0, 10, 10, 0, 10).DistanceTo(new MapPoint(13, 14)), 9);
        }

        [Fact]
        public void ASinglePlaceIsMeasuredStraightToIt()
        {
            Assert.Equal(3.0, Hull(2, 2).DistanceTo(new MapPoint(2, 5)), 9);
        }

        [Fact]
        public void ASegmentIsMeasuredToTheSegment()
        {
            ConvexHull hull = Hull(0, 0, 10, 0);

            Assert.Equal(3.0, hull.DistanceTo(new MapPoint(5, 3)), 9);
            Assert.Equal(4.0, hull.DistanceTo(new MapPoint(-4, 0)), 9);
        }

        // How far a heading out of a place inside the outline runs before it leaves - the length of
        // the corridor an order aimed that way has. What can go wrong is silent: a corridor measured
        // to the nearest EDGE rather than to where the line actually leaves, or a line through a
        // corner picking up two crossings and answering the near one.

        [Fact]
        public void AHeadingOutOfTheMiddleRunsToTheSideItPointsAt()
        {
            ConvexHull hull = Hull(0, 0, 10, 0, 10, 10, 0, 10);

            Assert.Equal(5.0, hull.ExitDistance(new MapPoint(5, 5), 1, 0), 9);
            Assert.Equal(5.0, hull.ExitDistance(new MapPoint(5, 5), 0, 1), 9);
            Assert.Equal(5.0, hull.ExitDistance(new MapPoint(5, 5), -1, 0), 9);
            Assert.Equal(2.0, hull.ExitDistance(new MapPoint(5, 8), 0, 1), 9);
        }

        [Fact]
        public void ADiagonalRunsToTheCornerItPointsAt()
        {
            ConvexHull hull = Hull(0, 0, 10, 0, 10, 10, 0, 10);

            Assert.Equal(System.Math.Sqrt(50), hull.ExitDistance(new MapPoint(5, 5), 1, 1), 9);
            Assert.Equal(System.Math.Sqrt(200), hull.ExitDistance(new MapPoint(0, 0), 1, 1), 9);
        }

        [Fact]
        public void TheDirectionNeedNotBeAUnitLong()
        {
            ConvexHull hull = Hull(0, 0, 10, 0, 10, 10, 0, 10);

            Assert.Equal(5.0, hull.ExitDistance(new MapPoint(5, 5), 37, 0), 9);
            Assert.Equal(0.0, hull.ExitDistance(new MapPoint(5, 5), 0, 0), 9);
        }

        [Fact]
        public void APlaceOnTheRimHasTheWholeWidthOneWayAndNothingTheOther()
        {
            ConvexHull hull = Hull(0, 0, 10, 0, 10, 10, 0, 10);

            Assert.Equal(10.0, hull.ExitDistance(new MapPoint(0, 5), 1, 0), 9);
            Assert.Equal(0.0, hull.ExitDistance(new MapPoint(0, 5), -1, 0), 9);
            Assert.Equal(5.0, hull.ExitDistance(new MapPoint(0, 5), 0, 1), 9);
        }

        [Fact]
        public void APlaceAHairOutsideStillMeasuresToWhereTheLineLeaves()
        {
            // Coordinates arrive through a camera and a float, so a star on its own galaxy's rim can
            // land just outside it; the corridor must not collapse to nothing there.
            ConvexHull hull = Hull(0, 0, 10, 0, 10, 10, 0, 10);

            Assert.Equal(10.0000001, hull.ExitDistance(new MapPoint(-1e-7, 5), 1, 0), 6);
        }

        [Fact]
        public void AnOutlineWithNoInsideHasNoCorridor()
        {
            Assert.Equal(0.0, Hull(0, 0, 10, 0).ExitDistance(new MapPoint(5, 0), 1, 0), 9);
            Assert.Equal(0.0, Hull(2, 2).ExitDistance(new MapPoint(2, 2), 1, 0), 9);
        }

        // How far across the outline is, and where its middle is.

        [Fact]
        public void TheOutlineIsAsWideAndAsTallAsTheBoxAroundIt()
        {
            ConvexHull hull = Hull(0, 0, 10, 0, 10, 4, 0, 4);

            Assert.Equal(10.0, hull.Width, 9);
            Assert.Equal(4.0, hull.Height, 9);
        }

        [Fact]
        public void TheMiddleIsTheBalancePointOfTheAreaAndNotOfTheBox()
        {
            // A lopsided outline is where the three candidate middles part company: this one's box is
            // centred at y 5.5 and its corners average to y 6.2, while the area balances at 5.254 -
            // the corners' average being dragged upwards by the one extra corner along the top.
            ConvexHull hull = Hull(0, 0, 10, 0, 10, 10, 5, 11, 0, 10);

            Assert.Equal(5.0, hull.Centroid.X, 9);
            Assert.Equal(5.253968, hull.Centroid.Y, 6);

            ConvexHull square = Hull(0, 0, 10, 0, 10, 10, 0, 10);
            Assert.Equal(5.0, square.Centroid.X, 9);
            Assert.Equal(5.0, square.Centroid.Y, 9);
        }

        [Fact]
        public void AnOutlineWithNoAreaAveragesItsPlacesInstead()
        {
            Assert.Equal(5.0, Hull(0, 0, 10, 0).Centroid.X, 9);
            Assert.Equal(7.0, Hull(2, 7).Centroid.Y, 9);
        }
    }
}

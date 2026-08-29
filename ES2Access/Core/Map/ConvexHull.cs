using System;

namespace ES2Access.Core.Map
{
    /// <summary>
    /// The smallest convex outline enclosing a scatter of places — the shape of a constellation, as
    /// far as anyone asking "am I inside it?" is concerned.
    ///
    /// Built once per galaxy, then asked two questions per keystroke: is this place inside, and if
    /// not, how far outside. The two answers share one boundary — a place ON the outline is IN it,
    /// and so is one that misses by no more than an epsilon, because coordinates that arrived
    /// through a camera and a float are never exactly on a line and a star must not fall out of its
    /// own constellation for want of a millionth of a unit. The epsilon is a DISTANCE, applied to
    /// the outline itself rather than to each edge's line, so "inside" and "distance is zero" answer
    /// identically everywhere, corners included.
    ///
    /// Degenerate scatters are outlines too: one place is its own outline, and two places — or any
    /// number of places along one line — are a segment. An empty scatter encloses nothing and is
    /// infinitely far from everywhere, so it can never win a comparison.
    ///
    /// Nothing here allocates once the outline is built.
    /// </summary>
    public sealed class ConvexHull
    {
        /// <summary>How far outside the outline still counts as on it, in the caller's own units.
        /// </summary>
        public const double OnEdge = 1e-6;

        private static readonly Comparison<MapPoint> WestThenSouth = CompareWestThenSouth;

        private readonly MapPoint[] _vertices;
        private readonly MapPoint _centroid;
        private readonly double _width;
        private readonly double _height;

        private ConvexHull(MapPoint[] vertices)
        {
            _vertices = vertices;
            _centroid = MeasureCentroid(vertices, out _width, out _height);
        }

        /// <summary>How far the outline reaches east to west - the width of the box that just holds
        /// it, in the caller's own units. An empty outline is zero across.</summary>
        public double Width
        {
            get { return _width; }
        }

        /// <summary>How far the outline reaches south to north.</summary>
        public double Height
        {
            get { return _height; }
        }

        /// <summary>
        /// The middle of the outline - the balance point of the enclosed AREA, not the middle of the
        /// box around it and not the average of the corners.
        ///
        /// Which of the three is "the middle" only matters for a lopsided galaxy, and there the area
        /// centroid is the one that answers the question a player asks: where is the bulk of this
        /// thing. The box's middle ignores the shape entirely, and averaging corners drags the answer
        /// towards whichever side the outline happens to have most corners on.
        ///
        /// An outline with no area - a single place, a run along one line - has its corners averaged
        /// instead, because a balance point of nothing has no meaning; an empty one is the origin.
        /// </summary>
        public MapPoint Centroid
        {
            get { return _centroid; }
        }

        /// <summary>Corners of the outline, counter-clockwise, with no corner repeated and no place
        /// left sitting in the middle of a straight run.</summary>
        public int Count
        {
            get { return _vertices.Length; }
        }

        public MapPoint Vertex(int index)
        {
            return _vertices[index];
        }

        public static ConvexHull Build(MapPoint[] places)
        {
            return Build(places, places.Length);
        }

        /// <summary>Wraps the first <paramref name="count"/> places in <paramref name="places"/>.
        /// The array is not kept or reordered: the work happens on a copy, so a caller may reuse its
        /// buffer.</summary>
        public static ConvexHull Build(MapPoint[] places, int count)
        {
            MapPoint[] sorted = new MapPoint[count];
            Array.Copy(places, sorted, count);
            Array.Sort(sorted, WestThenSouth);

            int distinct = 0;
            for (int i = 0; i < count; i++)
            {
                if (distinct > 0 && sorted[i].X == sorted[distinct - 1].X && sorted[i].Y == sorted[distinct - 1].Y)
                {
                    continue;
                }

                sorted[distinct++] = sorted[i];
            }

            if (distinct < 3)
            {
                MapPoint[] few = new MapPoint[distinct];
                Array.Copy(sorted, few, distinct);
                return new ConvexHull(few);
            }

            // Andrew's monotone chain: the lower boundary west to east, then the upper one back
            // again, each keeping only the turns that bend left. Popping on a cross product of zero
            // drops places that merely sit on a straight run, which is also what collapses a wholly
            // collinear scatter to its two endpoints.
            MapPoint[] chain = new MapPoint[2 * distinct];
            int built = 0;
            for (int i = 0; i < distinct; i++)
            {
                while (built >= 2 && Cross(chain[built - 2], chain[built - 1], sorted[i]) <= 0)
                {
                    built--;
                }

                chain[built++] = sorted[i];
            }

            for (int i = distinct - 2, lowerEnd = built + 1; i >= 0; i--)
            {
                while (built >= lowerEnd && Cross(chain[built - 2], chain[built - 1], sorted[i]) <= 0)
                {
                    built--;
                }

                chain[built++] = sorted[i];
            }

            // The walk closes on the place it started from; that repeat is not a corner.
            MapPoint[] vertices = new MapPoint[built - 1];
            Array.Copy(chain, vertices, built - 1);
            return new ConvexHull(vertices);
        }

        /// <summary>Whether the outline holds this place, counting its own boundary and an
        /// <see cref="OnEdge"/> band just outside it.</summary>
        public bool Contains(MapPoint place)
        {
            return Contains(place, OnEdge);
        }

        public bool Contains(MapPoint place, double epsilon)
        {
            return NearestSquared(place, epsilon) == 0;
        }

        /// <summary>How far this place is from the outline: zero anywhere it holds, otherwise the
        /// shortest distance to its boundary.</summary>
        public double DistanceTo(MapPoint place)
        {
            return DistanceTo(place, OnEdge);
        }

        public double DistanceTo(MapPoint place, double epsilon)
        {
            double squared = NearestSquared(place, epsilon);
            return squared == 0 || double.IsInfinity(squared) ? squared : Math.Sqrt(squared);
        }

        private double NearestSquared(MapPoint place, double epsilon)
        {
            if (_vertices.Length == 0)
            {
                return double.PositiveInfinity;
            }

            double band = epsilon * epsilon;
            if (_vertices.Length == 1)
            {
                double toPlace = _vertices[0].SquaredDistanceTo(place);
                return toPlace <= band ? 0 : toPlace;
            }

            if (_vertices.Length == 2)
            {
                double toSegment = place.SquaredDistanceToSegment(_vertices[0], _vertices[1]);
                return toSegment <= band ? 0 : toSegment;
            }

            if (Encloses(place))
            {
                return 0;
            }

            double best = double.PositiveInfinity;
            for (int i = 0; i < _vertices.Length; i++)
            {
                int next = i + 1 == _vertices.Length ? 0 : i + 1;
                double edge = place.SquaredDistanceToSegment(_vertices[i], _vertices[next]);
                if (edge < best)
                {
                    best = edge;
                }
            }

            return best <= band ? 0 : best;
        }

        /// <summary>The corners run counter-clockwise, so a place the outline holds is left of every
        /// edge — and a place exactly on one is left of it by nothing at all.</summary>
        private bool Encloses(MapPoint place)
        {
            for (int i = 0; i < _vertices.Length; i++)
            {
                int next = i + 1 == _vertices.Length ? 0 : i + 1;
                if (Cross(_vertices[i], _vertices[next], place) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// How far a straight line leaving <paramref name="origin"/> travels before it leaves the
        /// outline - the length of the corridor an order aimed that way has to work with.
        ///
        /// The direction is given as its two components, east and north, and need not be a unit
        /// length: the answer is a distance in the same units the corners are in. A line that leaves
        /// the outline immediately answers zero, which is what a place ON the rim aimed outwards is,
        /// and an outline with no interior - a single place, a run along one line - has no corridor
        /// at all and answers zero everywhere.
        ///
        /// Clipped against every edge at once rather than intersected edge by edge, so a line through
        /// a CORNER is answered by the same arithmetic as any other and cannot pick up two crossings
        /// or none. An origin the outline does not hold is answered with the FAR side, where the line
        /// finally leaves; callers put the origin inside.
        /// </summary>
        public double ExitDistance(MapPoint origin, double east, double north)
        {
            if (_vertices.Length < 3)
            {
                return 0;
            }

            double length = Math.Sqrt(east * east + north * north);
            if (length <= 0)
            {
                return 0;
            }

            double stepX = east / length;
            double stepY = north / length;
            double enters = 0;
            double leaves = double.PositiveInfinity;

            for (int i = 0; i < _vertices.Length; i++)
            {
                int next = i + 1 == _vertices.Length ? 0 : i + 1;
                double edgeX = _vertices[next].X - _vertices[i].X;
                double edgeY = _vertices[next].Y - _vertices[i].Y;

                // The corners run counter-clockwise, so inside is left of the edge: this is how far
                // left of it the origin is, and how fast the line is closing on it.
                double leftOf = edgeX * (origin.Y - _vertices[i].Y) - edgeY * (origin.X - _vertices[i].X);
                double rate = edgeX * stepY - edgeY * stepX;
                if (rate == 0)
                {
                    if (leftOf < 0)
                    {
                        return 0;
                    }

                    continue;
                }

                double crossing = -leftOf / rate;
                if (rate > 0)
                {
                    if (crossing > enters)
                    {
                        enters = crossing;
                    }
                }
                else if (crossing < leaves)
                {
                    leaves = crossing;
                }
            }

            return leaves < enters || leaves < 0 || double.IsInfinity(leaves) ? 0 : leaves;
        }

        private static MapPoint MeasureCentroid(MapPoint[] vertices, out double width, out double height)
        {
            width = 0;
            height = 0;
            if (vertices.Length == 0)
            {
                return new MapPoint(0, 0);
            }

            double minX = vertices[0].X;
            double maxX = minX;
            double minY = vertices[0].Y;
            double maxY = minY;
            double sumX = 0;
            double sumY = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                MapPoint corner = vertices[i];
                if (corner.X < minX) minX = corner.X;
                if (corner.X > maxX) maxX = corner.X;
                if (corner.Y < minY) minY = corner.Y;
                if (corner.Y > maxY) maxY = corner.Y;
                sumX += corner.X;
                sumY += corner.Y;
            }

            width = maxX - minX;
            height = maxY - minY;

            if (vertices.Length >= 3)
            {
                double twiceArea = 0;
                double weightedX = 0;
                double weightedY = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    MapPoint here = vertices[i];
                    MapPoint next = vertices[i + 1 == vertices.Length ? 0 : i + 1];
                    double cross = here.X * next.Y - next.X * here.Y;
                    twiceArea += cross;
                    weightedX += (here.X + next.X) * cross;
                    weightedY += (here.Y + next.Y) * cross;
                }

                if (twiceArea != 0)
                {
                    return new MapPoint(weightedX / (3 * twiceArea), weightedY / (3 * twiceArea));
                }
            }

            return new MapPoint(sumX / vertices.Length, sumY / vertices.Length);
        }

        private static double Cross(MapPoint origin, MapPoint first, MapPoint second)
        {
            return (first.X - origin.X) * (second.Y - origin.Y) - (first.Y - origin.Y) * (second.X - origin.X);
        }

        private static int CompareWestThenSouth(MapPoint a, MapPoint b)
        {
            if (a.X < b.X)
            {
                return -1;
            }

            if (a.X > b.X)
            {
                return 1;
            }

            if (a.Y < b.Y)
            {
                return -1;
            }

            return a.Y > b.Y ? 1 : 0;
        }
    }
}

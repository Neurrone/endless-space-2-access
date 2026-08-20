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

        private ConvexHull(MapPoint[] vertices)
        {
            _vertices = vertices;
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
                double toSegment = SquaredDistanceToSegment(place, _vertices[0], _vertices[1]);
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
                double edge = SquaredDistanceToSegment(place, _vertices[i], _vertices[next]);
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

        private static double SquaredDistanceToSegment(MapPoint place, MapPoint from, MapPoint to)
        {
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double length = dx * dx + dy * dy;
            double along = 0;
            if (length > 0)
            {
                along = ((place.X - from.X) * dx + (place.Y - from.Y) * dy) / length;
                if (along < 0)
                {
                    along = 0;
                }
                else if (along > 1)
                {
                    along = 1;
                }
            }

            double offX = from.X + along * dx - place.X;
            double offY = from.Y + along * dy - place.Y;
            return offX * offX + offY * offY;
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

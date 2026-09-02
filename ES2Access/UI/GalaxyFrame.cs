using System.Collections.Generic;
using ES2Access.Core.Map;

namespace ES2Access.UI
{
    /// <summary>
    /// Where the galaxy stops, in the two shapes it stops in - the one place in the mod that answers
    /// "how big is this map", so that no two features can answer it differently.
    ///
    /// Both are measured over EVERY node the galaxy holds (<see cref="GameGalaxy.GameNodes"/>): stars,
    /// and the specials that drift between them. A special node is a place on the map with a name, a
    /// coordinate pair and a row in the tree, so the map plainly goes as far as the furthest one - a
    /// frame drawn round the stars alone would leave places the player can steer to sitting outside
    /// the map.
    ///
    /// <see cref="Edges"/> is the box: how far the map goes east, west, north and south, and therefore
    /// how far anything may GO. The inspect cursor roams it and a probe's bearing is measured to it,
    /// which is the point of their sharing it - the same rim in the sentence "fully explored to the map
    /// edge at 40" and in the refusal the cursor makes when it reaches that rim.
    ///
    /// <see cref="Shape"/> is the outline the nodes themselves make - what a sighted player sees when
    /// they zoom all the way out and the spiral or the disc takes form. It is what the map's own
    /// overview is said from: its width and height are the box's, but its MIDDLE is the balance point
    /// of the shape, which is where the bulk of the galaxy lies. The middle of the box can easily be
    /// empty sky.
    ///
    /// Cached per game and keyed on the game itself: no node moves for the length of one, and a new
    /// game - or a save loaded over this one - is a new object here and re-measures on the next ask.
    /// </summary>
    public static class GalaxyFrame
    {
        /// <summary>The answer while there is no galaxy to measure: an outline with no corners, which
        /// holds nothing and has no corridor through it, so a caller that forgets to check gets zero
        /// rather than an exception.</summary>
        private static readonly ConvexHull Nothing = ConvexHull.Build(new MapPoint[0]);

        private static ConvexHull _edges;
        private static ConvexHull _shape;
        private static double _west;
        private static double _east;
        private static double _south;
        private static double _north;
        private static bool _measured;
        private static object _of;

        /// <summary>
        /// How far the map goes each way, in the game's OWN coordinates - the caller that wants them
        /// measured from home subtracts <see cref="GalaxyCoordinates.Origin"/>, which is one
        /// subtraction and cannot disagree with this box by so much as a rounding.
        ///
        /// False, with all four zero, while there is no galaxy yet.
        /// </summary>
        public static bool Extent(out double west, out double east, out double south, out double north)
        {
            Ensure();
            west = _west;
            east = _east;
            south = _south;
            north = _north;
            return _measured;
        }

        /// <summary>
        /// The four edges of the map as a four-cornered outline, so that "how far does a straight line
        /// down this bearing get before it leaves the map" is
        /// <see cref="ConvexHull.ExitDistance"/> and nothing else - a rectangle is a convex outline,
        /// and the exit arithmetic is the same arithmetic already proven on the galaxy's own shape.
        ///
        /// Never null; an outline with no corners while there is no galaxy.
        /// </summary>
        public static ConvexHull Edges()
        {
            Ensure();
            return _edges == null ? Nothing : _edges;
        }

        /// <summary>The outline the galaxy's nodes actually make, for the reading that describes the
        /// map rather than bounding a move. Never null.</summary>
        public static ConvexHull Shape()
        {
            Ensure();
            return _shape == null ? Nothing : _shape;
        }

        /// <summary>Let go of the galaxy this was measured from - mod teardown. The next ask measures
        /// afresh, which is what an unmeasured cache does anyway.</summary>
        public static void Forget()
        {
            _of = null;
            _measured = false;
            _edges = null;
            _shape = null;
            _west = 0.0;
            _east = 0.0;
            _south = 0.0;
            _north = 0.0;
        }

        private static void Ensure()
        {
            object game = Gui.Game;
            if (_measured && ReferenceEquals(game, _of))
            {
                return;
            }

            _of = game;
            _measured = false;
            _edges = null;
            _shape = null;
            _west = 0.0;
            _east = 0.0;
            _south = 0.0;
            _north = 0.0;

            GameNode[] nodes = GameGalaxy.GameNodes();
            if (nodes == null || nodes.Length == 0)
            {
                return;
            }

            List<MapPoint> places = new List<MapPoint>(nodes.Length);
            bool first = true;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null)
                {
                    continue;
                }

                double east = nodes[i].GalaxyPosition.X;
                double north = nodes[i].GalaxyPosition.Y;
                places.Add(new MapPoint(east, north));
                if (first)
                {
                    _west = _east = east;
                    _south = _north = north;
                    first = false;
                    continue;
                }

                if (east < _west)
                {
                    _west = east;
                }
                else if (east > _east)
                {
                    _east = east;
                }

                if (north < _south)
                {
                    _south = north;
                }
                else if (north > _north)
                {
                    _north = north;
                }
            }

            if (first)
            {
                return;
            }

            _shape = ConvexHull.Build(places.ToArray());
            _edges = ConvexHull.Build(
                new MapPoint[]
                {
                    new MapPoint(_west, _south),
                    new MapPoint(_east, _south),
                    new MapPoint(_east, _north),
                    new MapPoint(_west, _north)
                }
            );
            _measured = true;
        }
    }
}

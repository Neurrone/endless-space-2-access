using System;
using ES2Access.Core.Map;

namespace ES2Access.ES2.Map
{
    /// <summary>
    /// How much of what a probe launched down one bearing would REVEAL is already known.
    ///
    /// The ranges a corridor reading gives are the whole truth and are also six numbers long; a player
    /// comparing sixteen bearings needs one number first, to decide which of the sixteen are worth
    /// listening to at all. That number is this: of every tile the probe would light up - the ones it
    /// flies over and the ones its vision circle sweeps past on the way - how many the empire already
    /// has. Ten percent means a launch this way would show ninety tiles out of a hundred for the first
    /// time.
    ///
    /// The set is the probe's own flight and no more: a straight run from the system out to
    /// <c>reach</c> units (what the probe covers before it expires), thickened by its vision radius to
    /// either side and rounded off at both ends, then clipped to the frame the mod calls the edge of
    /// the map (<c>docs/galaxy-map.md</c>). It is therefore a SHORTER stretch of the bearing than the
    /// ranges are read over, which run to the map's rim however far past the probe's reach that is -
    /// deliberately, since the ranges answer "what is out that way" and this answers "what would this
    /// launch buy me".
    ///
    /// Tiles are the one-unit lattice the anchor sets, the same one the inspect cursor walks and
    /// <see cref="ProbeCorridor"/> samples the fog at, and each is counted ONCE however the bearing
    /// runs across it - the lattice is enumerated directly rather than marched along, so a diagonal
    /// cannot count a tile twice or skip one between steps.
    ///
    /// Engine-free like the rest of this folder: the galaxy is reached only through the
    /// <see cref="MapExplored"/> the caller hands in.
    /// </summary>
    public struct ProbeFootprint
    {
        /// <summary>How far outside the vision circle still counts as inside it, so that a tile the
        /// corridor samples as a FLANK - taken at the very edge of the vision radius - is never
        /// dropped here for a rounding error's worth of distance.</summary>
        private const double OnEdge = 1e-9;

        /// <summary>Whether a tile that far (squared) from the flight is one the probe would light
        /// up. THE membership test: <see cref="ProbeCorridor"/> asks it of its flank samples with the
        /// perpendicular distance to the flight line, so a tile the corridor is willing to speak
        /// about and a tile this counts are the same tile, down to the last epsilon.</summary>
        internal static bool InVision(double squaredDistance, double halfWidth)
        {
            return squaredDistance <= halfWidth * halfWidth + OnEdge;
        }

        public ProbeFootprint(int explored, int tiles)
        {
            Explored = explored;
            Tiles = tiles;
        }

        /// <summary>Tiles of the footprint the empire has already explored.</summary>
        public readonly int Explored;

        /// <summary>Tiles in the footprint altogether.</summary>
        public readonly int Tiles;

        /// <summary>The share already explored, whole percent, halves rounded up - the same plain
        /// rounding every other spoken percentage in the mod uses, with no band near either end
        /// treated specially. A footprint with no tiles in it at all - a system on the rim aimed off
        /// the map - has nothing left to find and reads as fully explored, which is what the bearing's
        /// own ranges say in the same breath.</summary>
        public int PercentExplored
        {
            get
            {
                if (Tiles <= 0)
                {
                    return 100;
                }

                int percent = (int)Math.Round(
                    100.0 * Explored / Tiles,
                    MidpointRounding.AwayFromZero
                );
                return percent < 0 ? 0 : (percent > 100 ? 100 : percent);
            }
        }

        /// <summary>
        /// The footprint of a launch down <paramref name="bearing"/> out of
        /// <paramref name="origin"/>: the tiles within <paramref name="halfWidth"/> of the flight,
        /// which runs <paramref name="reach"/> units or as far as the map's outline lets it, whichever
        /// is shorter.
        ///
        /// <paramref name="anchor"/> is the lattice the tiles sit on - only its fractional part
        /// matters - and is the same anchor <see cref="ProbeCorridor.Read"/> is given, so the two
        /// readings of one bearing are talking about the identical tiles.
        /// </summary>
        public static ProbeFootprint Read(
            ConvexHull galaxy,
            MapPoint origin,
            MapPoint anchor,
            double bearing,
            double reach,
            double halfWidth,
            MapExplored explored
        )
        {
            double radians = bearing * Math.PI / 180.0;
            double stepEast = Math.Sin(radians);
            double stepNorth = Math.Cos(radians);

            double flight = galaxy.ExitDistance(origin, stepEast, stepNorth);
            if (reach < flight)
            {
                flight = reach;
            }

            if (flight < 0)
            {
                flight = 0;
            }

            if (halfWidth < 0)
            {
                halfWidth = 0;
            }

            MapPoint tip = new MapPoint(
                origin.X + stepEast * flight,
                origin.Y + stepNorth * flight
            );

            int fromEast = First(Math.Min(origin.X, tip.X) - halfWidth - anchor.X);
            int toEast = Last(Math.Max(origin.X, tip.X) + halfWidth - anchor.X);
            int fromNorth = First(Math.Min(origin.Y, tip.Y) - halfWidth - anchor.Y);
            int toNorth = Last(Math.Max(origin.Y, tip.Y) + halfWidth - anchor.Y);

            int tiles = 0;
            int known = 0;
            for (int east = fromEast; east <= toEast; east++)
            {
                for (int north = fromNorth; north <= toNorth; north++)
                {
                    MapPoint tile = new MapPoint(anchor.X + east, anchor.Y + north);
                    if (
                        !InVision(tile.SquaredDistanceToSegment(origin, tip), halfWidth)
                        || !galaxy.Contains(tile)
                    )
                    {
                        continue;
                    }

                    tiles++;
                    if (explored(tile.X, tile.Y))
                    {
                        known++;
                    }
                }
            }

            return new ProbeFootprint(known, tiles);
        }

        /// <summary>The first lattice offset at or beyond an edge of the box, and the last at or
        /// before it - both given a hair's slack, because a box edge landing exactly on a lattice
        /// point arrives here as a float that may sit either side of it and the tile there is in the
        /// footprint or not by geometry, never by that.</summary>
        private static int First(double offset)
        {
            return (int)Math.Ceiling(offset - OnEdge);
        }

        private static int Last(double offset)
        {
            return (int)Math.Floor(offset + OnEdge);
        }
    }
}

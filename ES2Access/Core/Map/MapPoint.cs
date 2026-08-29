namespace ES2Access.Core.Map
{
    /// <summary>
    /// One place on the galaxy plane, as two plain numbers. The geometry in this folder never sees
    /// the game's own vector types: a caller hands over east and north (or whatever axes the map
    /// measures in) and gets answers in the same units, which is what keeps all of it testable with
    /// no engine present.
    /// </summary>
    public struct MapPoint
    {
        public readonly double X;
        public readonly double Y;

        public MapPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Squared distance to another place — squared, because ranking never needs the
        /// root and the root is the only expensive part.</summary>
        public double SquaredDistanceTo(MapPoint other)
        {
            double dx = other.X - X;
            double dy = other.Y - Y;
            return dx * dx + dy * dy;
        }

        /// <summary>Squared distance to the nearest point of a straight run between two places —
        /// the run itself, not the infinite line through it, so a place off either end is measured
        /// to that end. A run of no length is one place, and answers the distance to it.</summary>
        public double SquaredDistanceToSegment(MapPoint from, MapPoint to)
        {
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double length = dx * dx + dy * dy;
            double along = 0;
            if (length > 0)
            {
                along = ((X - from.X) * dx + (Y - from.Y) * dy) / length;
                if (along < 0)
                {
                    along = 0;
                }
                else if (along > 1)
                {
                    along = 1;
                }
            }

            double offX = from.X + along * dx - X;
            double offY = from.Y + along * dy - Y;
            return offX * offX + offY * offY;
        }
    }
}

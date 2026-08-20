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
    }
}

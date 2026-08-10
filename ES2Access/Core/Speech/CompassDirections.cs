namespace ES2Access.Core.Speech
{
    /// <summary>
    /// Which way one place on the map lies from another, said as one of the eight compass words.
    ///
    /// The map draws a starlane as a line and a player who can see it reads its direction off the
    /// screen; spoken, that direction has to become a word. The eight words are arcs CENTRED on the
    /// compass points - north runs from 337.5 degrees round through 22.5, so a lane that leaves
    /// almost straight up is "north" whichever side of straight up it leaves on - which is how
    /// people describe a heading out loud, and is not the same as slicing the circle at the compass
    /// points.
    ///
    /// Engine-free, so the arc boundaries are testable without the game. The caller supplies the two
    /// components of the offset already in the map's own terms: how far east, and how far north.
    /// </summary>
    public static class CompassDirections
    {
        /// <summary>The bearing of an offset, in degrees clockwise from north, in [0, 360).</summary>
        public static double Bearing(double east, double north)
        {
            double degrees = System.Math.Atan2(east, north) * 180.0 / System.Math.PI;
            if (degrees < 0.0)
            {
                degrees += 360.0;
            }

            return degrees >= 360.0 ? 0.0 : degrees;
        }

        /// <summary>The <see cref="ModStrings"/> key for the compass word an offset falls in.</summary>
        public static string DirectionKey(double east, double north)
        {
            return KeyForBearing(Bearing(east, north));
        }

        /// <summary>The compass word an offset points in, in the player's language.</summary>
        public static string Direction(double east, double north)
        {
            return ModStrings.Get(DirectionKey(east, north));
        }

        /// <summary>The compass word a bearing falls in - the arc test on its own, so the boundaries
        /// can be checked at the degree rather than through an offset that has to be trusted to land
        /// on one.</summary>
        public static string KeyForBearing(double bearing)
        {
            int arc = (int)System.Math.Floor((bearing + 22.5) / 45.0);
            return Words[((arc % 8) + 8) % 8];
        }

        private static readonly string[] Words = new string[]
        {
            ModStrings.DirectionNorth,
            ModStrings.DirectionNorthEast,
            ModStrings.DirectionEast,
            ModStrings.DirectionSouthEast,
            ModStrings.DirectionSouth,
            ModStrings.DirectionSouthWest,
            ModStrings.DirectionWest,
            ModStrings.DirectionNorthWest,
        };
    }
}

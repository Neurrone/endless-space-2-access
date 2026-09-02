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

        /// <summary>
        /// Which way one place lies from another said as its two COMPONENTS - "23 south", "1 west, 23
        /// south" - rather than as a distance and a compass word.
        ///
        /// A single word and a length ("23 units southwest") tells a player how far to walk but not
        /// where the thing is: to put it on the map they still have to resolve the diagonal into the
        /// two numbers the map's own pairs are said in. The components ARE those two numbers, so a
        /// thing heard at "1 west, 23 south" of a place whose pair the player already knows can be
        /// placed by adding, and the arithmetic always comes out - the caller hands in the difference
        /// of the two ROUNDED pairs, the very pairs both places were spoken with.
        ///
        /// EAST/WEST first, because that is the order the pair itself is spoken in
        /// (<see cref="MapCoordinates.Text"/>) and the two are heard in the same breath: an offset
        /// whose halves came the other way round would have the listener holding one order for
        /// positions and another for the distances between them. A zero component is left out rather
        /// than said - "0 east" is a word about nothing. Both zero is not this function's answer - the
        /// caller says whatever "here" means to it.
        /// </summary>
        public static string Offsets(int east, int north)
        {
            return Offsets(east, north, false);
        }

        /// <summary>
        /// The same offset, said SHORT where the player has asked for it - "23s", "1w, 23s".
        ///
        /// The same four components, the same comma between them and the same zero left out; only
        /// the per-axis template changes, so a language decides for itself how short its own answer
        /// is and where the abbreviation goes. Asked for by a player stepping through scanner
        /// results, who hears this on every one of them.
        /// </summary>
        public static string Offsets(int east, int north, bool shortened)
        {
            MessageBuilder message = new MessageBuilder();
            Component(message, east, true, shortened);
            Component(message, north, false, shortened);
            return message.Build();
        }

        /// <summary>One component, left out entirely when it is zero - "0 east" is a word about
        /// nothing - and joined to whatever is already there with the list's own comma.</summary>
        private static void Component(
            MessageBuilder message,
            int units,
            bool sideways,
            bool shortened
        )
        {
            if (units == 0)
            {
                return;
            }

            string text = ModStrings.Format(
                sideways
                    ? (
                        units > 0
                            ? (shortened ? ModStrings.OffsetEastShort : ModStrings.OffsetEast)
                            : (shortened ? ModStrings.OffsetWestShort : ModStrings.OffsetWest)
                    )
                    : (
                        units > 0
                            ? (shortened ? ModStrings.OffsetNorthShort : ModStrings.OffsetNorth)
                            : (shortened ? ModStrings.OffsetSouthShort : ModStrings.OffsetSouth)
                    ),
                System.Math.Abs(units)
            );
            if (message.IsEmpty)
            {
                message.Fragment(text);
            }
            else
            {
                message.ListItemForcedComma(text);
            }
        }

        /// <summary>The compass word a bearing falls in - the arc test on its own, so the boundaries
        /// can be checked at the degree rather than through an offset that has to be trusted to land
        /// on one.</summary>
        public static string KeyForBearing(double bearing)
        {
            int arc = (int)System.Math.Floor((bearing + 22.5) / 45.0);
            return Words[((arc % 8) + 8) % 8];
        }

        /// <summary>
        /// The same bearing said with SIXTEEN words instead of eight - the arcs half as wide, each
        /// still centred on its own word, so "north" now runs 348.75 through 11.25 and everything
        /// between it and northeast is "north-northeast".
        ///
        /// It is a second answer rather than a better one. A direction that DESCRIBES where something
        /// already is - a lane, a system found by the scanner - is easier to hear in eight words, and
        /// the extra precision buys the player nothing they can act on. A direction that AIMS is the
        /// opposite case: an order given down a bearing of the player's choosing covers only as much
        /// of the map as there are words to say, and eight words leave the gaps between them
        /// unreachable. So the eight-word arcs stay exactly as they were and this exists beside them.
        /// </summary>
        public static string KeyForBearing16(double bearing)
        {
            int arc = (int)System.Math.Floor((bearing + 11.25) / 22.5);
            return Words16[((arc % 16) + 16) % 16];
        }

        /// <summary>The sixteen-word compass word an offset points in, in the player's language.
        /// </summary>
        public static string Direction16(double east, double north)
        {
            return ModStrings.Get(KeyForBearing16(Bearing(east, north)));
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

        private static readonly string[] Words16 = new string[]
        {
            ModStrings.DirectionNorth,
            ModStrings.DirectionNorthNorthEast,
            ModStrings.DirectionNorthEast,
            ModStrings.DirectionEastNorthEast,
            ModStrings.DirectionEast,
            ModStrings.DirectionEastSouthEast,
            ModStrings.DirectionSouthEast,
            ModStrings.DirectionSouthSouthEast,
            ModStrings.DirectionSouth,
            ModStrings.DirectionSouthSouthWest,
            ModStrings.DirectionSouthWest,
            ModStrings.DirectionWestSouthWest,
            ModStrings.DirectionWest,
            ModStrings.DirectionWestNorthWest,
            ModStrings.DirectionNorthWest,
            ModStrings.DirectionNorthNorthWest,
        };
    }
}

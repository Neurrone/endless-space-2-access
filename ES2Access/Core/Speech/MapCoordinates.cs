namespace ES2Access.Core.Speech
{
    /// <summary>
    /// Where a place on the map IS, as the pair of whole numbers said straight after its name.
    ///
    /// A sighted player reads position off the map itself - this star is over on the left, that one is
    /// up and away to the right - and a compass word (<see cref="CompassDirections"/>) only answers
    /// that question one hop at a time, between two places the player already has in mind. A pair of
    /// numbers answers it for the whole galaxy at once: two places heard minutes apart can still be
    /// held against each other, and a route can be planned without walking every lane between.
    ///
    /// The pair is measured from the empire's HOME system, so home is "0, 0" and every other number
    /// is a distance and a direction from the one place the player already knows. It is NOT scaled:
    /// one spoken unit is one galaxy unit, the same unit the game's own lane lengths and movement
    /// figures are in, so a lane about ten units long is about ten units of difference in the pairs at
    /// its ends.
    ///
    /// Subtract FIRST, then round. Rounding each place to a whole unit before subtracting would let
    /// two half-unit roundings pile up into a whole unit of error in the difference - the number the
    /// player actually uses - and the error would land on the places nearest home, where the pairs are
    /// smallest and a unit matters most. Midpoints go away from zero, so a place exactly half a unit
    /// east and half a unit west of home are 1 and -1 rather than both being 0.
    ///
    /// Engine-free: the caller hands in the two points already in the map's own terms (how far east,
    /// how far north), which is what makes the rounding rule testable without the game.
    /// </summary>
    public static class MapCoordinates
    {
        /// <summary>The spoken pair for a place at (<paramref name="east"/>, <paramref name="north"/>)
        /// with home at (<paramref name="originEast"/>, <paramref name="originNorth"/>).</summary>
        public static string Text(double east, double north, double originEast, double originNorth)
        {
            return ModStrings.Format(
                ModStrings.GalaxyCoordinatePair,
                Round(east - originEast),
                Round(north - originNorth)
            );
        }

        /// <summary>
        /// The order two places on the map are READ in: north to south by rows a unit high, and west to
        /// east along a row.
        ///
        /// Compared on the SPOKEN pair rather than on the raw offsets, because the pair is the only
        /// thing the player can hear: somebody holding two rows' numbers against each other must never
        /// meet "3, 12" before "-4, 12". So the rounding here is <see cref="Round"/>'s, the same
        /// rounding the row itself says, and two places whose northing rounds the same are one row
        /// however far apart their raw positions are. Identical rounded pairs are settled on the raw
        /// values so that the order is at least fixed.
        ///
        /// Offsets from home, in the map's own units - the same numbers <see cref="Text"/> is handed.
        /// </summary>
        public static int ReadingOrder(
            double leftEast,
            double leftNorth,
            double rightEast,
            double rightNorth
        )
        {
            int row = Round(rightNorth).CompareTo(Round(leftNorth));
            if (row != 0)
            {
                return row;
            }

            int along = Round(leftEast).CompareTo(Round(rightEast));
            if (along != 0)
            {
                return along;
            }

            int north = rightNorth.CompareTo(leftNorth);
            return north != 0 ? north : leftEast.CompareTo(rightEast);
        }

        /// <summary>One axis' offset as the whole number it is spoken as: nearest, midpoints away from
        /// zero. Written out rather than taken from the framework so the rule is the one the tests
        /// state, on every runtime this mod is loaded into.</summary>
        public static int Round(double offset)
        {
            return offset < 0.0
                ? (int)System.Math.Ceiling(offset - 0.5)
                : (int)System.Math.Floor(offset + 0.5);
        }
    }
}

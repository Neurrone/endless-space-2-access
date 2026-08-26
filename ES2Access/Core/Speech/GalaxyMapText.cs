using ES2Access.Core.Map;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// The galaxy as a whole, in one sentence: what it was generated as, how far across it is, and
    /// where its middle lies from home.
    ///
    /// A sighted player learns all of this in the second the map first draws - the shape of the
    /// scatter, how much of it there is, whether home sits in the middle or out on a rim. None of it
    /// is written anywhere, so a listener never learns it at all, and every distance the mod says
    /// afterwards ("41 north, 12 west") is a number without a scale to hold it against. This is the
    /// scale.
    ///
    /// The shape and the size come from the game's own settings and are dropped in as the game's own
    /// words. The size across is the box that just holds the systems, measured off their outline; the
    /// middle is that outline's balance point (<see cref="ConvexHull.Centroid"/>), said as an offset
    /// from home the way every other offset in the mod is said - the north/south component first
    /// (<see cref="CompassDirections.Offsets"/>).
    /// </summary>
    public static class GalaxyMapText
    {
        /// <summary>The sentence for a galaxy whose systems make this outline, with home at
        /// <paramref name="home"/>.</summary>
        public static string Summary(
            string shape,
            string size,
            ConvexHull galaxy,
            MapPoint home,
            string homeName
        )
        {
            return Summary(
                shape,
                size,
                MapCoordinates.Round(galaxy.Width),
                MapCoordinates.Round(galaxy.Height),
                CompassDirections.Offsets(
                    MapCoordinates.Round(galaxy.Centroid.X - home.X),
                    MapCoordinates.Round(galaxy.Centroid.Y - home.Y)
                ),
                homeName
            );
        }

        /// <summary>The same from parts already measured, so the sentence itself can be checked
        /// without a galaxy. <paramref name="offsets"/> is a whole spoken offset
        /// (<see cref="CompassDirections.Offsets"/>), not a pair of numbers, because how far the
        /// middle is from home is one phrase in the sentence and its two halves are not separately
        /// placeable.</summary>
        public static string Summary(
            string shape,
            string size,
            int width,
            int height,
            string offsets,
            string homeName
        )
        {
            return ModStrings.Format(
                ModStrings.GalaxyMapSummary,
                shape,
                size,
                width,
                height,
                offsets,
                homeName
            );
        }
    }
}

using ES2Access.Core.Map;
using ES2Access.Core.Speech;

namespace ES2Access.ES2.Speech
{
    /// <summary>
    /// The galaxy as a whole, in one sentence: what it was generated as, how far across it is, and
    /// where its middle lies from home.
    ///
    /// A sighted player learns all of this in the second the map first draws - the shape of the
    /// scatter, how much of it there is, whether home sits in the middle or out on a rim. None of it
    /// is written anywhere, so a listener never learns it at all, and every distance the mod says
    /// afterwards ("12 west, 41 north") is a number without a scale to hold it against. This is the
    /// scale.
    ///
    /// The shape and the size come from the game's own settings and are dropped in as the game's own
    /// words, in the game's own casing. The size across is the box that just holds the systems,
    /// measured off their outline; the middle is that outline's balance point
    /// (<see cref="ConvexHull.Centroid"/>), said as an offset from home the way every other offset in
    /// the mod is said - the east/west component first
    /// (<see cref="CompassDirections.Offsets"/>).
    ///
    /// Where there is no offset to say, the middle is said as the place it is AT instead, and the
    /// sentence is still spoken: <see cref="Summary(string, string, ConvexHull, MapPoint, string)"/>
    /// names home when the middle rounds onto it, and
    /// <see cref="SummaryAtPair(string, string, ConvexHull, string)"/> gives the coordinate pair when
    /// there is no home to measure from at all. Each is its own template, because "at a place" and
    /// "at a pair of numbers" are not one phrase in every language.
    /// </summary>
    public static class GalaxyMapText
    {
        /// <summary>
        /// The sentence for a galaxy whose systems make this outline, with home at
        /// <paramref name="home"/>.
        ///
        /// A home system standing on the galactic centre - to within the whole unit the offset is
        /// spoken in - leaves no offset to say, and that case ends on home's own name instead: the
        /// middle is AT the home system rather than some way off it.
        /// </summary>
        public static string Summary(
            string shape,
            string size,
            ConvexHull galaxy,
            MapPoint home,
            string homeName
        )
        {
            string offsets = CompassDirections.Offsets(
                MapCoordinates.Round(galaxy.Centroid.X - home.X),
                MapCoordinates.Round(galaxy.Centroid.Y - home.Y)
            );
            if (string.IsNullOrEmpty(offsets))
            {
                return ModStrings.Format(
                    ModStrings.GalaxyMapSummaryAtHome,
                    shape,
                    size,
                    MapCoordinates.Round(galaxy.Width),
                    MapCoordinates.Round(galaxy.Height),
                    homeName
                );
            }

            return Summary(
                shape,
                size,
                MapCoordinates.Round(galaxy.Width),
                MapCoordinates.Round(galaxy.Height),
                offsets,
                homeName
            );
        }

        /// <summary>The same from parts already measured, so the sentence itself can be checked
        /// without a galaxy. <paramref name="offsets"/> is a whole spoken offset
        /// (<see cref="CompassDirections.Offsets"/>), not a pair of numbers, because how far
        /// the middle is from home is one phrase in the sentence and its two halves are not
        /// separately placeable.</summary>
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

        /// <summary>The sentence for a galaxy nobody lives in yet - an empire with no home system, so
        /// there is nothing for the middle to be an offset FROM. <paramref name="pair"/> is the
        /// coordinate pair the map is already speaking every place in while that is true
        /// (<c>GalaxyCoordinates</c> measures it from the game's own origin), taken as an
        /// already-spoken string so this sentence can be checked with no game present.</summary>
        public static string SummaryAtPair(
            string shape,
            string size,
            ConvexHull galaxy,
            string pair
        )
        {
            return ModStrings.Format(
                ModStrings.GalaxyMapSummaryAtPair,
                shape,
                size,
                MapCoordinates.Round(galaxy.Width),
                MapCoordinates.Round(galaxy.Height),
                pair
            );
        }
    }
}

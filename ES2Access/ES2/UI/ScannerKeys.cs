namespace ES2Access.ES2.UI
{
    /// <summary>
    /// THE NAMES A SAVED SCANNER SELECTOR IS WRITTEN IN - one short, stable string per category and
    /// per written-down subcategory, in the order the scanner holds them.
    ///
    /// A custom category is a list of selectors the player configured months ago and the mod has to
    /// find again on every press, in whatever language the game is being played in and in a galaxy
    /// that may have no such column at all. So a selector cannot be an index (the columns of the four
    /// derived categories move as the galaxy changes), and it cannot be a LABEL (the labels are
    /// localized and a language change would empty every custom category at once). It is these keys,
    /// which are the mod's own and never spoken.
    ///
    /// A subcategory that is not written down here is a KIND - one of the four derived categories'
    /// columns - and its key is the game's own internal name for the definition
    /// (<c>AnomalyDefinition.Name</c>, <c>CuriosityDefinition.DisplayedType</c>,
    /// <c>ResourceDefinition.Name</c>), which is equally stable and equally unspoken. Nothing here
    /// lists them: which ones exist is a fact about the galaxy, not about the taxonomy.
    ///
    /// Engine-free because the CODEC is (<see cref="ScannerCustomCodec"/>): the settings file is
    /// written and read off the engine, and a key that changed spelling between two builds would
    /// silently empty a player's categories with nothing to hear.
    /// </summary>
    public static class ScannerKeys
    {
        public const string Systems = "systems";
        public const string Colonizable = "colonizable";
        public const string Unexplored = "unexplored";
        public const string Anomalies = "anomalies";
        public const string Curiosities = "curiosities";
        public const string Luxury = "luxury";
        public const string Strategic = "strategic";
        public const string Contested = "contested";
        public const string Fleets = "fleets";
        public const string Probes = "probes";
        public const string Pins = "pins";
        public const string Projectiles = "projectiles";
        public const string Markers = "markers";

        /// <summary>The one column every category but the settleable worlds has, and the one a custom
        /// category always opens on.</summary>
        public const string All = "all";

        /// <summary>The scanner's thirteen categories, in the order it holds them - so the index of a
        /// key here is the index of the category there, once the custom slots in front of them are
        /// taken off.</summary>
        public static readonly string[] Categories = new string[]
        {
            Systems,
            Colonizable,
            Unexplored,
            Anomalies,
            Curiosities,
            Luxury,
            Strategic,
            Contested,
            Fleets,
            Probes,
            Pins,
            Projectiles,
            Markers,
        };

        /// <summary>The subcategories each category WRITES DOWN, in the order it writes them - the
        /// same shape as the scanner's own label table, so a key's position here is the column's
        /// index there. The four derived categories list only what they write down; the rest of
        /// their row is kinds.</summary>
        public static readonly string[][] Subcategories = new string[][]
        {
            new string[] { All, "friendly", "neutral", "enemy", "homeworld", "minor", "special" },
            new string[] { "unoccupied", "occupied" },
            new string[] { All },
            new string[] { All },
            new string[] { All, "explorable", "low-power" },
            new string[] { All },
            new string[] { All },
            new string[] { All },
            new string[] { All, "friendly", "neutral", "enemy" },
            new string[] { All, "friendly", "neutral", "enemy" },
            new string[] { All },
            new string[] { All },
            new string[] { All },
        };

        /// <summary>Which category a key names, or -1 for a key no build of the mod has ever had.
        /// </summary>
        public static int Category(string key)
        {
            for (int i = 0; key != null && i < Categories.Length; i++)
            {
                if (Categories[i] == key)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Which of a category's written-down columns a key names, or -1 where it names
        /// none - which is what a KIND key answers, and what a column this galaxy has no such thing
        /// in answers too.</summary>
        public static int Subcategory(int category, string key)
        {
            if (key == null || category < 0 || category >= Subcategories.Length)
            {
                return -1;
            }

            string[] row = Subcategories[category];
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] == key)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

namespace ES2Access.ES2.UI
{
    /// <summary>
    /// What a scanner press is decided from: how many things each subcategory of each category
    /// holds, and - where the category knows them - what those subcategories are CALLED.
    ///
    /// The counts alone were enough while every category's subcategory list was written into the
    /// source. They are not enough once a category's subcategories are derived from what is out
    /// there - one per kind of anomaly the player can see, sorted by name - because then the list
    /// changes shape between one press and the next: a kind appearing inserts a column, a kind
    /// disappearing removes one, and the INDEX a category was last left at can come back meaning a
    /// different scope. So a category may name its columns, and the cursor remembers the NAME.
    ///
    /// A table with no names behaves exactly as the counts did on their own, which is why the
    /// conversion from the raw table is implicit: the categories whose subcategories are a fact
    /// about the category rather than about the galaxy lose nothing and say nothing extra.
    ///
    /// Engine-free for the same reason the rest of the scanner's rules are: a column resolved to
    /// the wrong scope sounds exactly like a galaxy with different things in it.
    /// </summary>
    public sealed class ScannerTable
    {
        public ScannerTable(int[][] counts, string[][] labels)
        {
            _counts = counts;
            _labels = labels;
        }

        /// <summary>A bare counts table, unnamed - what a taxonomy fixed in the source hands over.
        /// </summary>
        public static implicit operator ScannerTable(int[][] counts)
        {
            return new ScannerTable(counts, null);
        }

        public int Categories
        {
            get { return _counts == null ? 0 : _counts.Length; }
        }

        /// <summary>How many subcategories a category has - how many columns its row holds.</summary>
        public int Width(int category)
        {
            return category < 0 || category >= Categories ? 0 : _counts[category].Length;
        }

        public int Count(int category, int subcategory)
        {
            return subcategory < 0 || subcategory >= Width(category)
                ? 0
                : _counts[category][subcategory];
        }

        /// <summary>What a subcategory is called, or null where the category does not name its
        /// columns - which is the only thing that tells the cursor whether a remembered name means
        /// anything.</summary>
        public string Label(int category, int subcategory)
        {
            if (_labels == null || category < 0 || category >= _labels.Length)
            {
                return null;
            }

            string[] row = _labels[category];
            return row == null || subcategory < 0 || subcategory >= row.Length
                ? null
                : row[subcategory];
        }

        /// <summary>Which column of a category carries this name, or -1 where none does any more -
        /// the question a cursor asks about the scope it was last left in.</summary>
        public int Find(int category, string label)
        {
            if (label == null)
            {
                return -1;
            }

            int width = Width(category);
            for (int i = 0; i < width; i++)
            {
                if (label == Label(category, i))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Whether a category holds anything at all - what a cycle asks before it decides
        /// to skip one.</summary>
        public bool Holds(int category)
        {
            int width = Width(category);
            for (int i = 0; i < width; i++)
            {
                if (_counts[category][i] > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The first subcategory of a category that holds something - where a category is
        /// opened when the scope it remembers is gone or was never set.</summary>
        public int FirstHolding(int category)
        {
            int width = Width(category);
            for (int i = 0; i < width; i++)
            {
                if (_counts[category][i] > 0)
                {
                    return i;
                }
            }

            return 0;
        }

        private readonly int[][] _counts;
        private readonly string[][] _labels;
    }
}

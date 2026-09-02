using System.Collections.Generic;

namespace ES2Access.ES2.UI
{
    /// <summary>One kind of thing the game's own databases define - the internal name a selector is
    /// saved as, and the localized words the player hears. Handed to
    /// <see cref="ScannerTaxonomyCategory.AddKinds"/> by whoever read the database.</summary>
    public sealed class ScannerKind
    {
        public ScannerKind(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public readonly string Key;
        public readonly string Label;
    }


    /// <summary>
    /// WHICH COLUMN OF A SCAN A SAVED KIND SELECTOR MEANS, when the definition it names and the one
    /// the galaxy holds are two names for the same word.
    ///
    /// The scanner columns what it found by the LABEL - one "Acid Rain" column holds
    /// <c>PlanetAnomaly23</c> and <c>PlanetAnomaly23Reduced</c> alike - while a selector saved by the
    /// editor names ONE definition. Matching the found things by key therefore missed every column
    /// whose twin was the one out there. Matching by the label the key is DRAWN with cannot: the key
    /// is resolved against the game's databases, which know both twins whether or not this galaxy
    /// holds either.
    ///
    /// Engine-free so the twin rule is testable without a galaxy - the databases arrive as the same
    /// <see cref="ScannerKind"/> list the editor's taxonomy is built from.
    /// </summary>
    public sealed class ScannerKindIndex
    {
        public ScannerKindIndex(IList<ScannerKind> kinds)
        {
            for (int i = 0; kinds != null && i < kinds.Count; i++)
            {
                ScannerKind kind = kinds[i];
                if (
                    kind == null
                    || string.IsNullOrEmpty(kind.Key)
                    || string.IsNullOrEmpty(kind.Label)
                    || _labels.ContainsKey(kind.Key)
                )
                {
                    continue;
                }

                _labels.Add(kind.Key, kind.Label);
            }
        }

        /// <summary>The words a definition is drawn with, or null for a key no database of this build
        /// defines.</summary>
        public string Label(string key)
        {
            string label;
            return key != null && _labels.TryGetValue(key, out label) ? label : null;
        }

        /// <summary>Which of a scanned category's columns a saved selector names, or -1 where nothing
        /// of that kind was found this game.</summary>
        public int Column(string key, IList<string> columns)
        {
            string label = Label(key);
            for (int i = 0; label != null && columns != null && i < columns.Count; i++)
            {
                if (columns[i] == label)
                {
                    return i;
                }
            }

            return -1;
        }

        private readonly Dictionary<string, string> _labels = new Dictionary<string, string>();
    }

    /// <summary>One column a custom category can be pointed at: the stable key it is SAVED as, and
    /// the words the player hears. A column the galaxy cannot answer this game keeps its key and has
    /// no words, which is what <see cref="Missing"/> is for.</summary>
    public sealed class ScannerTaxonomyColumn
    {
        public ScannerTaxonomyColumn(string key, string label, bool missing)
            : this(key, label, missing, null) { }

        /// <summary>
        /// A column that answers for SEVERAL of the game's internal names - the twins the game draws
        /// with one word (a luxury and its System twin, an anomaly and its Reduced form). The scanner
        /// columns what it finds by the LABEL, so those definitions were always one column out there;
        /// this is the editor and the saved selector agreeing with it.
        /// </summary>
        public ScannerTaxonomyColumn(string key, string label, bool missing, IList<string> keys)
        {
            Key = key ?? string.Empty;
            Label = label;
            Missing = missing;
            _keys = keys == null || keys.Count == 0 ? new List<string> { Key } : new List<string>(keys);
        }

        /// <summary>Every internal name this one column answers for, the canonical <see cref="Key"/>
        /// among them. A selector saved under any of them means this column - which is what keeps a
        /// category written before the twins were merged working.</summary>
        public IList<string> Keys
        {
            get { return _keys; }
        }

        /// <summary>Whether a saved selector naming <paramref name="key"/> means this column.
        /// </summary>
        public bool Answers(string key)
        {
            for (int i = 0; i < _keys.Count; i++)
            {
                if (_keys[i] == key)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly List<string> _keys;

        /// <summary>What a selector writes down (<see cref="ScannerKeys"/>, or a definition's own
        /// name for one of the four derived categories' kinds).</summary>
        public readonly string Key;

        /// <summary>What this column is called in the language the game is being played in, or null
        /// for one this galaxy has nothing of.</summary>
        public readonly string Label;

        /// <summary>Whether this column is only here because the player's category still points at
        /// it: a luxury nobody has found this game, a kind that was on the last galaxy. It is offered
        /// so the selector can be taken OFF - dropping it silently would leave the player unable to
        /// remove something they can hear the scanner skipping.</summary>
        public readonly bool Missing;
    }

    /// <summary>One built-in scanner category as the editor offers it: what it is called, and every
    /// column a selector could name.</summary>
    public sealed class ScannerTaxonomyCategory
    {
        internal ScannerTaxonomyCategory(string key, string label)
        {
            Key = key;
            Label = label;
        }

        /// <summary>The category's stable key - <see cref="ScannerKeys.Categories"/>.</summary>
        public readonly string Key;

        /// <summary>What the scanner calls it, in the player's language.</summary>
        public readonly string Label;

        public IList<ScannerTaxonomyColumn> Columns
        {
            get { return _columns; }
        }

        /// <summary>Add a column the galaxy answers for. Order is the offer order: the columns the
        /// category writes down first, then the kinds found this game.</summary>
        public void Add(string key, string label)
        {
            _columns.Add(new ScannerTaxonomyColumn(key, label, false));
        }

        /// <summary>
        /// Add every KIND the game defines for this category, sorted by the words the player hears -
        /// ONE COLUMN PER WORD.
        ///
        /// Definitions the game draws with the same word are one column, not several (owner ruling
        /// 2026-08-24): the game pairs an anomaly with its Reduced form and a planet deposit with its
        /// system-wide twin, and the SCANNER has always keyed its found columns by the label, so only
        /// one of a pair could ever have matched anything. The column keeps every key its twins were
        /// defined under, so a category saved under either one still means this column.
        ///
        /// The list comes from the game's DATABASES, not from what a galaxy happens to hold (owner
        /// ruling 2026-08-24): a category the player is writing has to be able to ask for a luxury
        /// nobody has surveyed yet, and a snapshot of the current map could only offer what has
        /// already been found. Kinds sharing one key are one column - the databases define several
        /// curiosities per displayed type - and a kind the localizer has no words for is dropped,
        /// because a column nobody can read is a column nobody can choose.
        ///
        /// The order is the caller's <paramref name="order"/> rather than a fixed comparison: the
        /// labels are localized, so sorting them is a question about the language being played in.
        /// </summary>
        public void AddKinds(IList<ScannerKind> kinds, IComparer<string> order)
        {
            List<string> canonical = new List<string>();
            Dictionary<string, string> keys = new Dictionary<string, string>();
            Dictionary<string, List<string>> twins = new Dictionary<string, List<string>>();
            Dictionary<string, string> byLabel = new Dictionary<string, string>();
            for (int i = 0; kinds != null && i < kinds.Count; i++)
            {
                ScannerKind kind = kinds[i];
                if (
                    kind == null
                    || string.IsNullOrEmpty(kind.Key)
                    || string.IsNullOrEmpty(kind.Label)
                    || keys.ContainsKey(kind.Key)
                )
                {
                    continue;
                }

                keys.Add(kind.Key, kind.Label);
                string first;
                if (byLabel.TryGetValue(kind.Label, out first))
                {
                    twins[first].Add(kind.Key);
                    continue;
                }

                byLabel.Add(kind.Label, kind.Key);
                twins.Add(kind.Key, new List<string> { kind.Key });
                canonical.Add(kind.Key);
            }

            canonical.Sort(new ByLabel(keys, order));
            for (int i = 0; i < canonical.Count; i++)
            {
                string key = canonical[i];
                List<string> row = twins[key];
                // The keys of one column in a stable order, whatever order the database was read in:
                // the first is the canonical one a new tick is saved under.
                row.Sort(string.CompareOrdinal);
                _columns.Add(new ScannerTaxonomyColumn(row[0], keys[key], false, row));
            }
        }

        /// <summary>Two kinds compared by the words they are drawn with, ties broken by their keys so
        /// two columns the game names the same still come out in a stable order.</summary>
        private sealed class ByLabel : IComparer<string>
        {
            public ByLabel(Dictionary<string, string> labels, IComparer<string> order)
            {
                _labels = labels;
                _order = order;
            }

            public int Compare(string left, string right)
            {
                int byLabel =
                    _order == null
                        ? string.CompareOrdinal(_labels[left], _labels[right])
                        : _order.Compare(_labels[left], _labels[right]);
                return byLabel != 0 ? byLabel : string.CompareOrdinal(left, right);
            }

            private readonly Dictionary<string, string> _labels;
            private readonly IComparer<string> _order;
        }

        internal bool Holds(string key)
        {
            return Answering(key) != null;
        }

        /// <summary>The column a saved selector names, or null where this category offers none -
        /// which is what makes it a stale selector. A twin's key answers with the column its label
        /// shares.</summary>
        public ScannerTaxonomyColumn Answering(string key)
        {
            for (int i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].Answers(key))
                {
                    return _columns[i];
                }
            }

            return null;
        }

        private readonly List<ScannerTaxonomyColumn> _columns =
            new List<ScannerTaxonomyColumn>();
    }

    /// <summary>
    /// EVERY COLUMN A CUSTOM CATEGORY COULD BE POINTED AT, as the editor has to offer them: the
    /// thirteen built-in categories, each with the subcategories it writes down plus - for the four
    /// whose columns are kinds - the kinds THIS galaxy holds.
    ///
    /// It is a snapshot, taken when the settings window opens, because half of it is a fact about the
    /// galaxy rather than about the taxonomy: which anomalies have been surveyed, which luxuries
    /// anybody has found. Rebuilding it per frame would be a walk of every planet sixty times a
    /// second for an answer that cannot change while a modal window is up.
    ///
    /// <see cref="Offer"/> is the one rule that is not a straight read of the snapshot: a selector the
    /// player has SAVED whose column this galaxy has none of is offered anyway, ticked and marked
    /// <see cref="ScannerTaxonomyColumn.Missing"/>. Without it a stale selector would be invisible
    /// and un-removable - the scanner skips it every press (<see cref="ScannerCustomPlan"/>) and the
    /// editor would show the player nothing to untick.
    ///
    /// Engine-free, so that rule is testable without a galaxy.
    /// </summary>
    public sealed class ScannerTaxonomy
    {
        public IList<ScannerTaxonomyCategory> Categories
        {
            get { return _categories; }
        }

        public ScannerTaxonomyCategory Add(string key, string label)
        {
            ScannerTaxonomyCategory category = new ScannerTaxonomyCategory(key, label);
            _categories.Add(category);
            return category;
        }

        /// <summary>The words the thirteen built-in categories go by - what a new name is checked
        /// against (<see cref="ScannerCustomSlots.NameTaken"/>).</summary>
        public IList<string> Labels()
        {
            List<string> labels = new List<string>();
            for (int i = 0; i < _categories.Count; i++)
            {
                labels.Add(_categories[i].Label);
            }

            return labels;
        }

        /// <summary>
        /// The columns to offer for one category, given what the player's category already points at:
        /// this galaxy's columns in their own order, then every SAVED selector for this category that
        /// none of them answers, marked missing.
        /// </summary>
        public IList<ScannerTaxonomyColumn> Offer(
            string categoryKey,
            IList<ScannerSelector> selectors
        )
        {
            List<ScannerTaxonomyColumn> offer = new List<ScannerTaxonomyColumn>();
            ScannerTaxonomyCategory category = Category(categoryKey);
            if (category == null)
            {
                return offer;
            }

            offer.AddRange(category.Columns);
            for (int i = 0; selectors != null && i < selectors.Count; i++)
            {
                ScannerSelector selector = selectors[i];
                if (selector.Category != categoryKey || category.Holds(selector.Subcategory))
                {
                    continue;
                }

                offer.Add(new ScannerTaxonomyColumn(selector.Subcategory, null, true));
            }

            return offer;
        }

        public ScannerTaxonomyCategory Category(string key)
        {
            for (int i = 0; key != null && i < _categories.Count; i++)
            {
                if (_categories[i].Key == key)
                {
                    return _categories[i];
                }
            }

            return null;
        }

        private readonly List<ScannerTaxonomyCategory> _categories =
            new List<ScannerTaxonomyCategory>();
    }
}

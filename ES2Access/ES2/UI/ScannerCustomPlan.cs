using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;

namespace ES2Access.ES2.UI
{
    /// <summary>What the live scanner can tell a plan about its own columns. Implemented by the
    /// scanner, faked by the tests: the plan's rules are about ORDER and ABSENCE, neither of which
    /// needs a galaxy.</summary>
    public interface IScannerColumns
    {
        /// <summary>Which live column a selector names, or false where this galaxy has none - a
        /// luxury nobody has found, a category a later build removed. The selector is kept either
        /// way; only this scan skips it.</summary>
        bool Find(ScannerSelector selector, out int category, out int subcategory);

        /// <summary>What a live column is called, in the language the game is being played in - both
        /// halves, so two selectors that both say "all" are still two different columns to a player
        /// and to the cursor's memory.</summary>
        string Label(int category, int subcategory);
    }

    /// <summary>One column of a custom category: its "all", one of its selectors, or one of its
    /// keywords.</summary>
    public sealed class ScannerCustomColumn
    {
        public ScannerCustomColumn(string label, int category, int subcategory, string keyword)
        {
            Label = label;
            Category = category;
            Subcategory = subcategory;
            Keyword = keyword;
        }

        /// <summary>What the scope line calls it, and what the cursor remembers it by.</summary>
        public readonly string Label;

        /// <summary>The live column this one draws from, or -1 where it draws from all of them.
        /// </summary>
        public readonly int Category;
        public readonly int Subcategory;

        /// <summary>The word this column looks for, or null where it is not a keyword column.
        /// </summary>
        public readonly string Keyword;

        /// <summary>Whether this is the category's own "all" - everything it caught, however it was
        /// caught.</summary>
        public bool Everything
        {
            get { return Category < 0 && Keyword == null; }
        }
    }

    /// <summary>
    /// A CUSTOM CATEGORY AS THE SCANNER WILL BUILD IT THIS PRESS: what its columns are, in order,
    /// with the ones this galaxy cannot answer left out.
    ///
    /// The order is the whole point and it is the player's own: "all" first, because it is where a
    /// category is always opened and the one column that can never be empty while the category holds
    /// anything; then the selectors in the order they were added; then the keywords in the order they
    /// were added. Nothing sorts anything - a custom category is a list the player wrote, and reading
    /// it back in a different order would make it somebody else's list.
    ///
    /// A selector this galaxy has no column for is SKIPPED, not dropped: the plan is rebuilt every
    /// press, so the column comes back by itself the day the galaxy has one.
    ///
    /// Engine-free so that both of those are testable off the game, neither having any audible
    /// failure: a column silently missing sounds like a category the player configured differently.
    /// </summary>
    public sealed class ScannerCustomPlan
    {
        /// <summary>The plan for one slot, or null where the slot is empty.</summary>
        public static ScannerCustomPlan Of(ScannerCustomCategory category, IScannerColumns columns)
        {
            if (category == null || columns == null)
            {
                return null;
            }

            ScannerCustomPlan plan = new ScannerCustomPlan(category.Name);
            plan._columns.Add(
                new ScannerCustomColumn(
                    ModStrings.Get(ModStrings.GalaxyScannerCustomAll),
                    -1,
                    -1,
                    null
                )
            );

            IList<ScannerSelector> selectors = category.Selectors;
            for (int i = 0; i < selectors.Count; i++)
            {
                int at;
                int sub;
                if (!columns.Find(selectors[i], out at, out sub))
                {
                    continue;
                }

                plan._columns.Add(
                    new ScannerCustomColumn(columns.Label(at, sub), at, sub, null)
                );
            }

            IList<string> keywords = category.Keywords;
            for (int i = 0; i < keywords.Count; i++)
            {
                plan._columns.Add(new ScannerCustomColumn(keywords[i], -1, -1, keywords[i]));
            }

            return plan;
        }

        private ScannerCustomPlan(string name)
        {
            Name = name;
        }

        /// <summary>What the category cycle calls it - the player's own name.</summary>
        public readonly string Name;

        public IList<ScannerCustomColumn> Columns
        {
            get { return _columns; }
        }

        /// <summary>The column names, as the scanner's label table holds them.</summary>
        public string[] Labels()
        {
            string[] labels = new string[_columns.Count];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = _columns[i].Label;
            }

            return labels;
        }

        /// <summary>
        /// WHETHER A KEYWORD CATCHES A RESULT: the same tiered, diacritic-insensitive match the
        /// type-ahead search uses, asked of everything the row says about the thing before it starts
        /// saying where it is - its name, the kind of thing it is, and the detail already composed
        /// for it (owner ruling 2026-08-23).
        ///
        /// Not the column names: "friendly" is a question the player asked with a selector, and a
        /// keyword matching it would make every friendly thing a member of a category the player
        /// meant to be about words.
        /// </summary>
        public static bool Catches(string keyword, string name, string kind, string extra)
        {
            string wanted = ScannerCustomCategory.Clean(keyword);
            if (wanted == null)
            {
                return false;
            }

            wanted = wanted.ToLowerInvariant();
            return Caught(name, wanted) || Caught(kind, wanted) || Caught(extra, wanted);
        }

        private static bool Caught(string text, string lowerKeyword)
        {
            int position;
            return !string.IsNullOrEmpty(text)
                && TypeAheadSearch.MatchTier(text.ToLowerInvariant(), lowerKeyword, out position)
                    >= 0;
        }

        private readonly List<ScannerCustomColumn> _columns = new List<ScannerCustomColumn>();
    }
}

using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.ES2.UI;
using ES2Access.UI.Settings;

namespace ES2Access.Screens
{
    /// <summary>The player's own categories: what each configured slot asks for this press, and the
    /// columns the galaxy answers it with.</summary>
    internal sealed partial class GalaxyScanner
    {
        /// <summary>A category with no columns at all - an unconfigured slot, which is a row of the
        /// table holding nothing and is therefore skipped by every cycle exactly as a built-in
        /// category with nothing in it is.</summary>
        private static readonly string[] NoColumns = new string[0];

        // ---- the player's own categories ----

        /// <summary>
        /// WHAT THE THREE SLOTS ASK FOR, this press: one plan per configured slot, its columns
        /// resolved against the galaxy as it now stands (<see cref="ScannerCustomPlan"/>). A slot
        /// standing empty plans nothing, which is what tells its quick keys to say so.
        /// </summary>
        private static ScannerCustomPlan[] Plans(List<Found>[] world, string[][] labels)
        {
            ScannerCustomPlan[] plans = new ScannerCustomPlan[SlotCount];
            ScannerCustomSlots slots = ScannerCustomSettings.Slots;
            if (!slots.Any)
            {
                return plans;
            }

            Columns columns = new Columns(world, labels);
            for (int slot = 0; slot < SlotCount; slot++)
            {
                plans[slot] = ScannerCustomPlan.Of(slots.Slot(slot), columns);
            }

            return plans;
        }

        /// <summary>
        /// EVERYTHING ONE SLOT CAUGHT, one list per column of the category the player wrote.
        ///
        /// A selector's column holds whatever its built-in column holds, shared as a struct copy - the
        /// same facts, so a row of the custom category and the row it came from can never say
        /// different things about one planet. A keyword's column holds everything in the whole
        /// scanner whose own words match it. And "all" holds each result ONCE however many of the
        /// player's questions caught it, which is the only column where the same thing could have
        /// arrived twice.
        /// </summary>
        private static List<Found>[] CustomColumns(
            ScannerCustomPlan plan,
            List<Found>[] world,
            string[][] labels,
            double east,
            double north
        )
        {
            IList<ScannerCustomColumn> plans = plan.Columns;
            List<Found>[] columns = new List<Found>[plans.Count];
            List<Found> all = new List<Found>();
            HashSet<string> seen = new HashSet<string>();
            columns[0] = all;
            for (int c = 1; c < plans.Count; c++)
            {
                ScannerCustomColumn column = plans[c];
                List<Found> caught = new List<Found>();
                if (column.Keyword != null)
                {
                    Keyword(caught, column.Keyword, world);
                }
                else
                {
                    Selected(caught, column.Category, column.Subcategory, world, labels);
                }

                Sort(caught, east, north);
                columns[c] = caught;
                for (int i = 0; i < caught.Count; i++)
                {
                    Found found = caught[i];
                    if (!seen.Add(found.Key))
                    {
                        continue;
                    }

                    // In "all" the row says what it would say in the column it came from's own
                    // "all" - the kind and then the world, where the source category's columns are
                    // kinds - because "all" has said nothing about it either.
                    found.Prefix = Kinds(found.From) && found.Kind != null;
                    all.Add(found);
                }
            }

            Sort(all, east, north);
            return columns;
        }

        /// <summary>One built-in column, as a custom category holds it.</summary>
        private static void Selected(
            List<Found> caught,
            int category,
            int subcategory,
            List<Found>[] world,
            string[][] labels
        )
        {
            List<Found> source = world[category];
            bool prefix = Kinds(category) && subcategory < ScopeKeys[category].Length;
            for (int i = 0; i < source.Count; i++)
            {
                if (!Holds(source[i], category, subcategory, labels[category]))
                {
                    continue;
                }

                Found found = source[i];
                found.From = category;
                found.Prefix = prefix && found.Kind != null;
                caught.Add(found);
            }
        }

        /// <summary>Everything the scanner can see whose own words match one keyword - its name, the
        /// kind of thing it is, and the detail already composed for it (owner ruling 2026-08-23).
        /// Every built-in category is asked, and no custom one: a custom category holding another's
        /// results would be the same things twice under two names.</summary>
        private static void Keyword(List<Found> caught, string keyword, List<Found>[] world)
        {
            for (int at = 0; at < BuiltInCount; at++)
            {
                List<Found> source = world[at];
                bool kinds = Kinds(at);
                for (int i = 0; i < source.Count; i++)
                {
                    if (
                        !ScannerCustomPlan.Catches(
                            keyword,
                            source[i].Name,
                            source[i].Kind,
                            source[i].Extra
                        )
                    )
                    {
                        continue;
                    }

                    Found found = source[i];
                    found.From = at;
                    found.Prefix = kinds && found.Kind != null;
                    caught.Add(found);
                }
            }
        }

        /// <summary>What a plan asks the live galaxy about its own columns: which one a saved
        /// selector names, and what it is called.</summary>
        private sealed class Columns : IScannerColumns
        {
            public Columns(List<Found>[] world, string[][] labels)
            {
                _world = world;
                _labels = labels;
            }

            public bool Find(ScannerSelector selector, out int category, out int subcategory)
            {
                category = -1;
                subcategory = -1;
                int built = ScannerKeys.Category(selector.Category);
                if (built < 0)
                {
                    return false;
                }

                category = built;
                subcategory = ScannerKeys.Subcategory(built, selector.Subcategory);
                if (subcategory >= 0)
                {
                    return true;
                }

                // Not one of the columns the category writes down, so it is a KIND - and which column
                // a kind is in is a fact about this galaxy. The definition's own name is resolved to
                // the WORDS the game draws it with (the databases know them whether or not this
                // galaxy holds any), and the column is the one carrying those words - which is what
                // the table is keyed by, and what makes a selector saved under either of two twins
                // find the one column they share.
                if (!Kinds(category))
                {
                    return false;
                }

                int column = KindIndex(category).Column(selector.Subcategory, _labels[category]);
                if (column >= 0)
                {
                    subcategory = column;
                    return true;
                }

                // A key no database of this build defines - another mod's, or a definition the game
                // dropped. Nothing can resolve its words, so the only thing left is what was found.
                List<Found> found = _world[category];
                for (int i = 0; i < found.Count; i++)
                {
                    if (found[i].KindKey != selector.Subcategory)
                    {
                        continue;
                    }

                    string[] row = _labels[category];
                    for (int c = 0; c < row.Length; c++)
                    {
                        if (row[c] == found[i].Kind)
                        {
                            subcategory = c;
                            return true;
                        }
                    }
                }

                return false;
            }

            public string Label(int category, int subcategory)
            {
                // BOTH halves: two selectors that both say "all" are two different columns, and a
                // player who hears "all" twice in one category has no way to tell them apart - nor
                // has the cursor, which remembers a column by its name.
                return ModStrings.Format(
                    ModStrings.GalaxyScannerScope,
                    ModStrings.Get(CategoryKeys[category]),
                    _labels[category][subcategory]
                );
            }

            private readonly List<Found>[] _world;
            private readonly string[][] _labels;
        }
    }
}

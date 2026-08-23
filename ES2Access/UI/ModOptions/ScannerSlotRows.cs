using System;
using System.Collections.Generic;
using Amplitude.Unity.Options;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// ONE SLOT'S OWN TAB - the page where a custom scanner category is written.
    ///
    /// Flat, and every row is one of the game's own: a text box for the name, one per keyword, an
    /// empty one to add another, a drawn button that empties the slot, and then the taxonomy - one
    /// CAPTION per built-in scanner category with a checkbox under it per column. The game's Controls
    /// tab is the precedent for a page this long (forty-one rows in one flat list).
    ///
    /// THE CAPTIONS ARE NOT ROWS TO STAND ON. Each is drawn as a row so the page looks sectioned, and
    /// spoken as the NAME of the section under it - the mod's rule for a drawn caption over a block
    /// (<see cref="ES2Access.Screens.OptionsScreen"/> turns them into regions, so Ctrl+arrow jumps
    /// between the thirteen). Deliberately not one undifferentiated checklist: the full taxonomy is
    /// over a hundred columns on a mature galaxy, and the count in each caption is what lets a player
    /// pass a section by ear.
    ///
    /// AN EMPTY SLOT SHOWS ONLY ITS NAME BOX. A category is a name plus what it asks for, and there
    /// is nothing to tick columns onto until the slot holds one; typing a name is what fills it, and
    /// the page is built again with everything else on it.
    /// </summary>
    public static class ScannerSlotRows
    {
        /// <summary>Fill the tab of <paramref name="slot"/>. Called when the window builds the panel,
        /// and again whenever an edit changes what the page holds.</summary>
        public static void Fill(OptionsTabPanel panel, int slot)
        {
            if (panel == null || panel.OptionsTable == null)
            {
                Log.Warn("mod options: the tab for custom category " + (slot + 1) + " is not built");
                return;
            }

            _panels[slot] = panel;
            _captions[slot] = new Dictionary<string, OptionItem>();
            try
            {
                ModRows.Begin(panel);
                List<Option> options = new List<Option>();
                int at = slot;

                Add(
                    options,
                    ModRows.Text(
                        panel,
                        "nameField",
                        ModStrings.Get(ModStrings.ScannerEditName),
                        () => ScannerEditor.NameOf(at),
                        text => ScannerEditor.SetName(at, text)
                    )
                );

                if (ScannerEditor.Working.Slot(slot) != null)
                {
                    Keywords(panel, options, slot);
                    Add(
                        options,
                        ModRows.Button(
                            panel,
                            panel.Parent,
                            "clearButton",
                            ModStrings.Get(ModStrings.ScannerEditClear),
                            () => ScannerEditor.Clear(at)
                        )
                    );
                    Columns(panel, options, slot);
                }

                ModRows.Publish(panel, options);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: building the tab for custom category " + (slot + 1) + " threw: " + e);
            }
        }

        /// <summary>Build the page again, from the pump, after an edit changed what it holds.
        /// </summary>
        public static void Refill(int slot)
        {
            OptionsTabPanel panel = slot >= 0 && slot < _panels.Length ? _panels[slot] : null;
            if (panel == null || panel.OptionsTable == null)
            {
                return;
            }

            try
            {
                ModRows.Clear(panel);
                Fill(panel, slot);
                panel.RefreshNow();
            }
            catch (Exception e)
            {
                Log.Warn("mod options: rebuilding custom category " + (slot + 1) + " threw: " + e);
            }
        }

        /// <summary>Say a section's caption again, after a tick under it changed how many of its
        /// columns the category draws from.</summary>
        public static void Recount(int slot)
        {
            Dictionary<string, OptionItem> captions =
                slot >= 0 && slot < _captions.Length ? _captions[slot] : null;
            if (captions == null)
            {
                return;
            }

            IList<ScannerTaxonomyCategory> categories = ScannerEditor.Taxonomy.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                OptionItem item;
                if (captions.TryGetValue(categories[i].Key, out item))
                {
                    ModRows.Recaption(item, Caption(slot, categories[i]));
                }
            }
        }

        public static void Forget()
        {
            for (int i = 0; i < _panels.Length; i++)
            {
                _panels[i] = null;
                _captions[i] = null;
            }
        }

        // ---- the blocks ----

        /// <summary>One box per keyword, and one empty box after them. Blanking a box takes that
        /// keyword out; typing in the last one adds another.</summary>
        private static void Keywords(OptionsTabPanel panel, List<Option> options, int slot)
        {
            IList<string> keywords = ScannerEditor.Keywords(slot);
            for (int i = 0; i < keywords.Count; i++)
            {
                int at = slot;
                int index = i;
                Add(
                    options,
                    ModRows.Text(
                        panel,
                        "keyword" + i + "Field",
                        ModStrings.Format(ModStrings.ScannerEditKeyword, i + 1),
                        () => ScannerEditor.Keyword(at, index),
                        text => ScannerEditor.SetKeyword(at, index, text)
                    )
                );
            }

            int slotted = slot;
            Add(
                options,
                ModRows.Text(
                    panel,
                    "newKeywordField",
                    ModStrings.Get(ModStrings.ScannerEditAddKeyword),
                    Nothing,
                    text => ScannerEditor.AddKeyword(slotted, text)
                )
            );
        }

        /// <summary>The taxonomy: a caption per built-in category, then a checkbox per column of it.
        /// A column this galaxy has nothing of has no words of its own, so a stored selector pointing
        /// at one is named by the key it was saved as and said as the stale thing it is - which is
        /// what lets the player take it off.</summary>
        private static void Columns(OptionsTabPanel panel, List<Option> options, int slot)
        {
            ScannerTaxonomy taxonomy = ScannerEditor.Taxonomy;
            ScannerCustomCategory category = ScannerEditor.Working.Slot(slot);
            IList<ScannerTaxonomyCategory> categories = taxonomy.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                ScannerTaxonomyCategory section = categories[i];
                Option caption = ModRows.Caption(
                    panel,
                    "section" + section.Key,
                    Caption(slot, section)
                );
                Add(options, caption);
                _captions[slot][section.Key] = Row(panel, "section" + section.Key);

                IList<ScannerTaxonomyColumn> columns = taxonomy.Offer(
                    section.Key,
                    category == null ? null : category.Selectors
                );
                for (int c = 0; c < columns.Count; c++)
                {
                    int at = slot;
                    ScannerTaxonomyColumn column = columns[c];
                    ScannerSelector selector = new ScannerSelector(section.Key, column.Key);
                    string label = column.Missing
                        ? ModStrings.Format(ModStrings.ScannerEditMissing, column.Key)
                        : column.Label;
                    Add(
                        options,
                        ModRows.Toggle(
                            panel,
                            "select" + section.Key + ":" + column.Key,
                            label,
                            () => ScannerEditor.Holds(at, selector),
                            ticked => ScannerEditor.Select(at, selector, ticked)
                        )
                    );
                }
            }
        }

        private static string Caption(int slot, ScannerTaxonomyCategory section)
        {
            return ModStrings.Format(
                ModStrings.ScannerEditSelected,
                section.Label,
                ScannerEditor.Chosen(slot, section.Key)
            );
        }

        private static void Add(List<Option> options, Option option)
        {
            if (option != null)
            {
                options.Add(option);
            }
        }

        private static string Nothing()
        {
            return string.Empty;
        }

        private static OptionItem Row(OptionsTabPanel panel, string name)
        {
            for (int i = 0; i < panel.OptionsTable.Children.Count; i++)
            {
                AgeTransform child = panel.OptionsTable.Children[i];
                if (child != null && child.name == name)
                {
                    return child.GetComponent<OptionItem>();
                }
            }

            return null;
        }

        private static readonly OptionsTabPanel[] _panels = new OptionsTabPanel[
            ScannerCustomSlots.Count
        ];

        private static readonly Dictionary<string, OptionItem>[] _captions = new Dictionary<
            string,
            OptionItem
        >[ScannerCustomSlots.Count];
    }
}

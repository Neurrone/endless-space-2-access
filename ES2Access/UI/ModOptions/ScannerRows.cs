using System;
using System.Collections.Generic;
using Amplitude.Unity.Options;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// THE SCANNER TAB - the whole editor for the player's own three scanner categories, on one page.
    ///
    /// Each slot is ONE DRAWN CONTROL that opens and shuts in place: "Custom category 1: Watch list",
    /// "Custom category 2: empty". Under it, hidden until it is opened, sit that slot's own rows - a
    /// text box for the name, one per keyword, an empty one to add another, a button that empties the
    /// slot, and then the taxonomy: one CAPTION per built-in scanner category with a checkbox under it
    /// per column. Every one of them is a row of the same <c>OptionsTable</c>, which is what makes the
    /// page look to a sighted player exactly like what a screen reader walks (owner ruling
    /// 2026-08-24, replacing the three top-level tabs this used to be).
    ///
    /// OPENING A SLOT IS SHOW-AND-ARRANGE, NEVER A REBUILD. The rows all exist from the moment the
    /// page is built; expanding sets their <c>Visible</c> and re-arranges the table. That matters
    /// twice: the mod's tree keys expand a group and step into it IN ONE PRESS, so the children have
    /// to be there the same frame the state flips - a rebuild deferred to the pump would report an
    /// empty group - and nothing the player is standing on is destroyed underneath them.
    ///
    /// THE CAPTIONS ARE NOT ROWS TO STAND ON. Each is drawn as a row so the page looks sectioned, and
    /// spoken as the NAME of the section under it (<see cref="ES2Access.Screens.OptionsScreen"/> turns
    /// them into regions, so Ctrl+arrow jumps between the thirteen). Deliberately not one
    /// undifferentiated checklist: the full taxonomy is over a hundred columns, and the count in each
    /// caption is what lets a player pass a section by ear.
    ///
    /// AN EMPTY SLOT HOLDS ONLY ITS NAME BOX. A category is a name plus what it asks for, and there is
    /// nothing to tick columns onto until the slot holds one; typing a name is what fills it, and the
    /// page is built again with everything else on it.
    ///
    /// The INVISIBLE ROW at the top is the panel's one declared option (<see cref="IModScannerService"/>)
    /// and is not a setting: its value answers "does what the player has edited differ from what is
    /// saved", which is the question the window asks to decide whether Apply lights, whether Escape
    /// asks about unapplied changes, and what Cancel puts back.
    /// </summary>
    public static class ScannerRows
    {
        /// <summary>Fill the Scanner tab. Called when the window builds the panel, and again whenever
        /// an edit changes what the page holds.</summary>
        public static void Fill(OptionsTabPanel panel)
        {
            if (panel == null || panel.OptionsTable == null)
            {
                Log.Warn("mod options: the Scanner panel is not built");
                return;
            }

            _panel = panel;
            _headers.Clear();
            for (int slot = 0; slot < ScannerCustomSlots.Count; slot++)
            {
                _members[slot] = new List<AgeTransform>();
                _captions[slot] = new Dictionary<string, OptionItem>();
            }

            try
            {
                List<Option> options = new List<Option>();
                Option[] declared = panel.Options;
                if (declared == null || declared.Length == 0)
                {
                    Log.Warn("mod options: the Scanner panel loaded no option to track edits with");
                }
                else
                {
                    ScannerEditor.Marker(declared[0]);
                    options.Add(declared[0]);
                    for (int i = 0; i < panel.OptionsTable.Children.Count; i++)
                    {
                        panel.OptionsTable.Children[i].Visible = false;
                    }
                }

                ModRows.Begin(panel);
                for (int slot = 0; slot < ScannerCustomSlots.Count; slot++)
                {
                    int at = slot;
                    Add(
                        options,
                        ModRows.Group(
                            panel,
                            panel.Parent,
                            Name(slot, "Header"),
                            Caption(slot),
                            () => Expanded(at),
                            open => Expand(at, open)
                        )
                    );
                    _headers.Add(Row(panel, Name(slot, "Header")));
                    Slot(panel, options, slot);
                }

                ModRows.Publish(panel, options);
                Arrange();
            }
            catch (Exception e)
            {
                Log.Warn("mod options: building the Scanner tab threw: " + e);
            }
        }

        /// <summary>Build the page again, from the pump, after an edit changed what it HOLDS - a
        /// keyword added, a slot named or emptied. Opening and shutting a slot never comes here.
        /// </summary>
        public static void Refill()
        {
            OptionsTabPanel panel = _panel;
            if (panel == null || panel.OptionsTable == null)
            {
                return;
            }

            try
            {
                ModRows.Clear(panel);
                Fill(panel);
                panel.RefreshNow();
            }
            catch (Exception e)
            {
                Log.Warn("mod options: rebuilding the Scanner tab threw: " + e);
            }
        }

        /// <summary>Say the slot headers again, after a slot was named or emptied.</summary>
        public static void Relabel()
        {
            for (int slot = 0; slot < _headers.Count; slot++)
            {
                OptionItem item = _headers[slot];
                if (item != null && item.TitleLabel != null)
                {
                    item.TitleLabel.Text = Caption(slot);
                }
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

        /// <summary>Whether a slot is open. Read by the group node, which is what the state word in
        /// its readout comes from.</summary>
        public static bool Expanded(int slot)
        {
            return slot >= 0 && slot < _open.Length && _open[slot];
        }

        /// <summary>Open or shut one slot: its rows are shown or hidden and the table is arranged
        /// again, in the same call, so the tree keys find the children the same frame.</summary>
        public static void Expand(int slot, bool open)
        {
            if (slot < 0 || slot >= _open.Length || _open[slot] == open)
            {
                return;
            }

            _open[slot] = open;
            Arrange();
        }

        /// <summary>Shut every slot - what a window opening starts from.</summary>
        public static void CollapseAll()
        {
            for (int i = 0; i < _open.Length; i++)
            {
                _open[i] = false;
            }
        }

        public static void Forget()
        {
            _panel = null;
            _headers.Clear();
            for (int i = 0; i < _members.Length; i++)
            {
                _members[i] = null;
                _captions[i] = null;
            }

            CollapseAll();
        }

        // ---- the blocks ----

        /// <summary>One slot's own rows, all of them built and then shown or hidden together.
        /// </summary>
        private static void Slot(OptionsTabPanel panel, List<Option> options, int slot)
        {
            int at = slot;
            Member(
                panel,
                slot,
                options,
                ModRows.Text(
                    panel,
                    Name(slot, "Name"),
                    ModStrings.Get(ModStrings.ScannerEditName),
                    () => ScannerEditor.NameOf(at),
                    text => ScannerEditor.SetName(at, text)
                ),
                Name(slot, "Name")
            );

            if (ScannerEditor.Working.Slot(slot) == null)
            {
                return;
            }

            Keywords(panel, options, slot);
            Member(
                panel,
                slot,
                options,
                ModRows.Button(
                    panel,
                    panel.Parent,
                    Name(slot, "Clear"),
                    ModStrings.Get(ModStrings.ScannerEditClear),
                    () => ScannerEditor.Clear(at)
                ),
                Name(slot, "Clear")
            );
            Columns(panel, options, slot);
        }

        /// <summary>One box per keyword, and one empty box after them. Blanking a box takes that
        /// keyword out; typing in the last one adds another.</summary>
        private static void Keywords(OptionsTabPanel panel, List<Option> options, int slot)
        {
            IList<string> keywords = ScannerEditor.Keywords(slot);
            for (int i = 0; i < keywords.Count; i++)
            {
                int at = slot;
                int index = i;
                string name = Name(slot, "Keyword" + i);
                Member(
                    panel,
                    slot,
                    options,
                    ModRows.Text(
                        panel,
                        name,
                        ModStrings.Format(ModStrings.ScannerEditKeyword, i + 1),
                        () => ScannerEditor.Keyword(at, index),
                        text => ScannerEditor.SetKeyword(at, index, text)
                    ),
                    name
                );
            }

            int slotted = slot;
            string added = Name(slot, "NewKeyword");
            Member(
                panel,
                slot,
                options,
                ModRows.Text(
                    panel,
                    added,
                    ModStrings.Get(ModStrings.ScannerEditAddKeyword),
                    Nothing,
                    text => ScannerEditor.AddKeyword(slotted, text)
                ),
                added
            );
        }

        /// <summary>The taxonomy: a caption per built-in category, then a checkbox per column of it.
        /// A column the game defines no words for has no label of its own, so a stored selector
        /// pointing at one is named by the key it was saved as and said as the stale thing it is -
        /// which is what lets the player take it off.</summary>
        private static void Columns(OptionsTabPanel panel, List<Option> options, int slot)
        {
            ScannerTaxonomy taxonomy = ScannerEditor.Taxonomy;
            ScannerCustomCategory category = ScannerEditor.Working.Slot(slot);
            IList<ScannerTaxonomyCategory> categories = taxonomy.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                ScannerTaxonomyCategory section = categories[i];
                string caption = Name(slot, "Section" + section.Key);
                Member(
                    panel,
                    slot,
                    options,
                    ModRows.Caption(panel, caption, Caption(slot, section)),
                    caption
                );
                _captions[slot][section.Key] = Row(panel, caption);

                IList<ScannerTaxonomyColumn> columns = taxonomy.Offer(
                    section.Key,
                    category == null ? null : category.Selectors
                );
                for (int c = 0; c < columns.Count; c++)
                {
                    int at = slot;
                    ScannerTaxonomyColumn column = columns[c];
                    string key = section.Key;
                    string label = column.Missing
                        ? ModStrings.Format(ModStrings.ScannerEditMissing, column.Key)
                        : column.Label;
                    string name = Name(slot, "Select" + section.Key + ":" + column.Key);
                    Member(
                        panel,
                        slot,
                        options,
                        ModRows.Toggle(
                            panel,
                            name,
                            label,
                            () => ScannerEditor.Holds(at, key, column),
                            ticked => ScannerEditor.Select(at, key, column, ticked)
                        ),
                        name
                    );
                }
            }
        }

        // ---- the machinery ----

        /// <summary>Take a row into the page and remember which slot it belongs to, so opening and
        /// shutting that slot can show and hide it.</summary>
        private static void Member(
            OptionsTabPanel panel,
            int slot,
            List<Option> options,
            Option option,
            string name
        )
        {
            Add(options, option);
            OptionItem item = Row(panel, name);
            if (item != null && item.AgeTransform != null)
            {
                _members[slot].Add(item.AgeTransform);
            }
        }

        /// <summary>Show what is open, hide what is not, and lay the table out again.</summary>
        private static void Arrange()
        {
            OptionsTabPanel panel = _panel;
            if (panel == null || panel.OptionsTable == null)
            {
                return;
            }

            try
            {
                for (int slot = 0; slot < _members.Length; slot++)
                {
                    List<AgeTransform> rows = _members[slot];
                    for (int i = 0; rows != null && i < rows.Count; i++)
                    {
                        if (rows[i] != null)
                        {
                            rows[i].Visible = _open[slot];
                        }
                    }
                }

                panel.OptionsTable.ArrangeChildren();
            }
            catch (Exception e)
            {
                Log.Warn("mod options: arranging the Scanner tab threw: " + e);
            }
        }

        private static string Name(int slot, string part)
        {
            return "slot" + slot + part;
        }

        private static string Caption(int slot)
        {
            return ModStrings.Format(
                ModStrings.ScannerEditSlotButton,
                slot + 1,
                ScannerEditor.SpokenName(slot)
            );
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

        private static OptionsTabPanel _panel;

        private static readonly List<OptionItem> _headers = new List<OptionItem>();

        /// <summary>Which slots are open. Collapsed by default, and every time the window opens.
        /// </summary>
        private static readonly bool[] _open = new bool[ScannerCustomSlots.Count];

        private static readonly List<AgeTransform>[] _members = new List<AgeTransform>[
            ScannerCustomSlots.Count
        ];

        private static readonly Dictionary<string, OptionItem>[] _captions = new Dictionary<
            string,
            OptionItem
        >[ScannerCustomSlots.Count];
    }
}

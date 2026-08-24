using System;
using System.Collections.Generic;
using Amplitude.Unity.Options;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;
using ES2Access.Screens;
using ES2Access.UI.Settings;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// THE EDITOR FOR THE PLAYER'S OWN THREE SCANNER CATEGORIES - what the Scanner tab of the mod's
    /// settings window is working on.
    ///
    /// This holds the MODEL and the rules; the rows that draw it are <see cref="ScannerRows"/>, one
    /// page with a collapsible block per slot. It was a tree of mod nodes over an empty tab until
    /// 2026-08-24 and the owner rejected that outright: the window is the game's, so its pages are
    /// drawn with the game's own widgets and a sighted player sees what a screen reader hears.
    ///
    /// EDITS ARE HELD UNTIL APPLY (owner ruling 2, 2026-08-23). Everything works on a
    /// <see cref="ScannerCustomSlots.Copy"/>; <see cref="Commit"/> hands it to
    /// <see cref="ScannerCustomSettings.Replace"/> when the window hides, and Cancel never gets that
    /// far because the window's own restore has already thrown the copy away
    /// (<see cref="Discard"/>). The window is told an edit happened through the Scanner panel's one
    /// invisible row, which is what lights Apply and what makes Escape ask the game's own
    /// "%OptionExitWithoutApplyMessage" - none of that is re-implemented.
    ///
    /// A COMMITTED EDIT THAT CHANGES THE SHAPE OF A PAGE ASKS FOR A REBUILD RATHER THAN DOING ONE.
    /// A text row commits when the field loses the keyboard, and the mod is inside the engine's own
    /// focus change when that happens - destroying the field there would be pulling the floor out
    /// from under the call. So the setter records what wants rebuilding and <see cref="Tick"/>, from
    /// the pump, does it. The refusals are said there too, for the reason they always were: a
    /// screen's arrival interrupts anything queued ahead of it.
    /// </summary>
    internal static class ScannerEditor
    {
        /// <summary>The game's key for the Scanner tab - an identifier, never a spoken word (the
        /// words are <see cref="ModStrings.ModSettingsScanner"/>).</summary>
        public const string CategoryName = "Scanner";

        // ---- what the player is editing ----

        /// <summary>The three slots as the player is currently leaving them. A copy, so Cancel is the
        /// copy being dropped and nothing else.</summary>
        public static ScannerCustomSlots Working
        {
            get { return _working ?? (_working = ScannerCustomSettings.Slots.Copy()); }
        }

        /// <summary>Every column a selector could name, this galaxy - taken once when the window
        /// opens, because half of it is a walk of every perceived planet.</summary>
        public static ScannerTaxonomy Taxonomy
        {
            get { return _taxonomy ?? (_taxonomy = GalaxyScanner.Taxonomy()); }
        }

        /// <summary>Whether anything the player has done differs from what is saved - what the
        /// invisible row's option answers, and therefore what lights Apply and what makes Escape ask
        /// the question.</summary>
        public static bool Edited
        {
            get { return !Working.Same(ScannerCustomSettings.Slots); }
        }

        /// <summary>The window put the option's backup back: Cancel, or the player answering the
        /// game's own "you have not applied" box. Everything since the window opened goes.</summary>
        public static void Discard()
        {
            _working = null;
        }

        /// <summary>Start again from what is saved - called as the window begins to show, BEFORE the
        /// game takes its backup of every option, so the backup is taken over a clean copy.</summary>
        public static void Begin()
        {
            _working = null;
            _taxonomy = null;
            _say = null;
            _refill = false;
            ScannerRows.CollapseAll();
        }

        /// <summary>What the player settled on, written through. Called when the window hides, by
        /// which point Cancel has already dropped the copy - so a copy that still differs is one the
        /// player applied.</summary>
        public static void Commit()
        {
            if (_working == null || _working.Same(ScannerCustomSettings.Slots))
            {
                return;
            }

            ScannerCustomSettings.Replace(_working);
            _working = null;
        }

        /// <summary>Mod teardown: hold no galaxy and no edit across a reload.</summary>
        public static void Forget()
        {
            _working = null;
            _taxonomy = null;
            _marker = null;
            _say = null;
            _refill = false;
        }

        /// <summary>The invisible row's option, handed over once the Scanner panel has built it. It
        /// is the only thing the window's Apply/Cancel machinery can be told about an edit through.
        /// </summary>
        public static void Marker(Option option)
        {
            _marker = option;
        }

        // ---- what the rows read and write ----

        /// <summary>What the slot is called - the player's own name, or nothing at all where the slot
        /// stands empty (which is what the name box shows as an empty box to type into).</summary>
        public static string NameOf(int slot)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            return category == null ? string.Empty : category.Name;
        }

        /// <summary>What the slot is called, said as a slot: the player's own name, or the word for a
        /// slot nobody has filled - so an empty slot is heard as a slot rather than as a gap.
        /// </summary>
        public static string SpokenName(int slot)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            return category == null ? ModStrings.Get(ModStrings.ScannerEditEmpty) : category.Name;
        }

        /// <summary>
        /// Name the slot, which is also how an empty one is FILLED: a category is a name plus what it
        /// asks for, and there is nothing to tick columns onto until the slot holds one.
        /// </summary>
        public static void SetName(int slot, string typed)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            string wanted = ScannerCustomCategory.Clean(typed);
            if (wanted == null)
            {
                // Blanking the name of a category that exists is not a way to delete it - the Clear
                // button is - and a nameless category is one the cycle would read out as silence.
                if (category != null)
                {
                    _say = ModStrings.Get(ModStrings.ScannerEditNameBlank);
                    Rebuild();
                }

                return;
            }

            if (Working.NameTaken(wanted, slot, Taxonomy.Labels()))
            {
                _say = ModStrings.Format(ModStrings.ScannerEditNameTaken, wanted);
                Rebuild();
                return;
            }

            if (category == null)
            {
                Working.Set(slot, new ScannerCustomCategory(wanted));
                Changed();
                // The page was a name box and nothing else; it is a whole category now.
                Rebuild();
                return;
            }

            if (category.Rename(wanted))
            {
                Changed();
                ScannerRows.Relabel();
            }
        }

        public static IList<string> Keywords(int slot)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            return category == null ? NoKeywords : category.Keywords;
        }

        public static string Keyword(int slot, int index)
        {
            IList<string> keywords = Keywords(slot);
            return index >= 0 && index < keywords.Count ? keywords[index] : string.Empty;
        }

        /// <summary>Change one keyword, or - blanked - take it out.</summary>
        public static void SetKeyword(int slot, int index, string typed)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            IList<string> keywords = Keywords(slot);
            if (category == null || index < 0 || index >= keywords.Count)
            {
                return;
            }

            string was = keywords[index];
            string wanted = ScannerCustomCategory.Clean(typed);
            if (wanted == null)
            {
                if (category.RemoveKeyword(was))
                {
                    _say = ModStrings.Format(ModStrings.ScannerEditRemoved, was);
                    Changed();
                }

                Rebuild();
                return;
            }

            if (wanted == was)
            {
                return;
            }

            if (!category.ReplaceKeyword(index, wanted))
            {
                _say = ModStrings.Get(ModStrings.ScannerEditKeywordTaken);
                Rebuild();
                return;
            }

            Changed();
        }

        /// <summary>Add a keyword from the empty box at the end of the list.</summary>
        public static void AddKeyword(int slot, string typed)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            string wanted = ScannerCustomCategory.Clean(typed);
            if (category == null || wanted == null)
            {
                return;
            }

            if (!category.AddKeyword(wanted))
            {
                _say = ModStrings.Get(ModStrings.ScannerEditKeywordTaken);
                Rebuild();
                return;
            }

            Changed();
            Rebuild();
        }

        /// <summary>Whether this slot draws from a column - asked of every key the column answers
        /// for, so a category saved before two twins became one column still reads as ticked.
        /// </summary>
        public static bool Holds(int slot, string categoryKey, ScannerTaxonomyColumn column)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            IList<string> keys = column.Keys;
            for (int i = 0; category != null && i < category.Selectors.Count; i++)
            {
                ScannerSelector selector = category.Selectors[i];
                if (selector.Category != categoryKey)
                {
                    continue;
                }

                for (int k = 0; k < keys.Count; k++)
                {
                    if (selector.Subcategory == keys[k])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Tick or untick a column. A tick saves the column's canonical key; an UNTICK takes
        /// every key it answers for off, which is what lets a player remove a selector an older build
        /// wrote under the other twin.</summary>
        public static void Select(
            int slot,
            string categoryKey,
            ScannerTaxonomyColumn column,
            bool wanted
        )
        {
            ScannerCustomCategory category = Working.Slot(slot);
            if (category == null || Holds(slot, categoryKey, column) == wanted)
            {
                return;
            }

            if (wanted)
            {
                category.AddSelector(new ScannerSelector(categoryKey, column.Key));
            }
            else
            {
                IList<string> keys = column.Keys;
                for (int k = 0; k < keys.Count; k++)
                {
                    category.RemoveSelector(new ScannerSelector(categoryKey, keys[k]));
                }
            }

            Changed();
            ScannerRows.Recount(slot);
        }

        /// <summary>How many of a built-in category's columns this slot draws from - the count that
        /// makes a hundred-column taxonomy walkable, since a section with nothing ticked can be
        /// passed over by ear. Counted in COLUMNS, so a category an older build wrote under both of
        /// two twins says the one checkbox the player can see.</summary>
        public static int Chosen(int slot, string categoryKey)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            ScannerTaxonomyCategory section = Taxonomy.Category(categoryKey);
            List<string> counted = new List<string>();
            for (int i = 0; category != null && i < category.Selectors.Count; i++)
            {
                ScannerSelector selector = category.Selectors[i];
                if (selector.Category != categoryKey)
                {
                    continue;
                }

                ScannerTaxonomyColumn column =
                    section == null ? null : section.Answering(selector.Subcategory);
                string key = column == null ? selector.Subcategory : column.Key;
                if (!counted.Contains(key))
                {
                    counted.Add(key);
                }
            }

            return counted.Count;
        }

        /// <summary>Emptying the slot. No confirmation: Cancel on the window is the undo, and one is
        /// enough - a question in front of every clear would be a question the player answers a
        /// hundred times to use the feature once.</summary>
        public static void Clear(int slot)
        {
            if (Working.Slot(slot) == null || !Working.Clear(slot))
            {
                return;
            }

            _say = ModStrings.Format(ModStrings.ScannerEditCleared, slot + 1);
            Changed();
            ScannerRows.Relabel();
            Rebuild();
        }

        // ---- the pump ----

        /// <summary>
        /// Rebuild whatever a committed edit changed the shape of, and say what came of it.
        ///
        /// The rebuild is here rather than in the setter because a text row commits from inside the
        /// engine's own focus change: the field the player was typing in is mid-<c>FocusLoss</c>, and
        /// destroying it there destroys the object the engine is still walking. The speech is here
        /// for its own reason - a refusal follows a page changing under the player, and a screen's
        /// arrival interrupts anything queued ahead of it.
        /// </summary>
        public static void Tick()
        {
            if (_refill)
            {
                _refill = false;
                ScannerRows.Refill();
                Reread();
            }

            string say = _say;
            _say = null;
            Voice.Say(say, false);
        }

        /// <summary>Ask for the page to be built again on the next tick, because what it holds has
        /// changed rather than just what it says.</summary>
        private static void Rebuild()
        {
            _refill = true;
        }

        /// <summary>Tell the window something changed, through the invisible row's option: that is
        /// what recomputes "has anything changed", lights Apply and arms the question Escape asks.
        /// </summary>
        private static void Changed()
        {
            ModOptionsWindow window = ModOptions.Window();
            if (window == null || _marker == null)
            {
                return;
            }

            try
            {
                window.OnOptionChanged(_marker);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: telling the window about a scanner edit threw: " + e);
            }
        }

        /// <summary>Read the control the cursor is on again. The rows were rebuilt under it, so the
        /// ordinary "say it when the cursor moves" rule would leave the player standing on a row
        /// nobody has read to them.</summary>
        private static void Reread()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator != null)
            {
                navigator.AnnounceNextLanding();
            }
        }

        private static ScannerCustomSlots _working;
        private static ScannerTaxonomy _taxonomy;
        private static Option _marker;

        /// <summary>What the next tick says, once the rows have been built again.</summary>
        private static string _say;

        /// <summary>Whether the page has to be built again on the next tick.</summary>
        private static bool _refill;

        private static readonly string[] NoKeywords = new string[0];
    }
}

using System;
using System.Collections.Generic;
using Amplitude.Unity.Options;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.Screens;
using ES2Access.UI.Settings;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// THE EDITOR FOR THE PLAYER'S OWN THREE SCANNER CATEGORIES - the Scanner tab of the mod's
    /// settings window, and the only part of that window the game draws nothing for.
    ///
    /// WHY A TREE OF MOD NODES rather than the game's own option rows: none of the five row kinds can
    /// express a list somebody edits. There is no repeatable row, no add and no remove, and the one
    /// text row the engine ships is broken outright (<c>OptionTextFieldItem</c> commits the LABEL
    /// object into the option's value and the cast is swallowed as a logged error). So the Scanner
    /// panel holds one row, invisible, whose only job is to make the window's own Apply/Cancel
    /// machinery see the edits (<see cref="ModScannerService"/>), and everything the player walks is
    /// declared here as graph nodes. A sighted player therefore sees an empty tab - accepted for this
    /// release, and the reason the tab exists at all is that the whole surface is keyboard-only.
    ///
    /// THE SHAPE IS soc-access's, adapted: one expandable group per SLOT, and inside it a button that
    /// opens the naming box, one expandable group per built-in scanner category holding a checkbox
    /// per column, a Keywords group, and a button that empties the slot. Deliberately not one flat
    /// checklist - the full taxonomy is over a hundred columns on a mature galaxy, and a single run
    /// of checkboxes is not something anybody walks twice.
    ///
    /// AN EMPTY SLOT SHOWS ONLY ITS NAME BUTTON. A category is a NAME plus what it asks for, and
    /// there is nothing to tick columns onto until the slot holds one; naming it is what fills the
    /// slot, and the box opens pre-filled with "Custom N" so accepting the offer is one keystroke.
    ///
    /// EDITS ARE HELD UNTIL APPLY (owner ruling 2, 2026-08-23). Everything here works on a
    /// <see cref="ScannerCustomSlots.Copy"/>; <see cref="Commit"/> hands it to
    /// <see cref="ScannerCustomSettings.Replace"/> when the window hides, and Cancel never gets that
    /// far because the window's own restore has already thrown the copy away
    /// (<see cref="Discard"/>). The window is told an edit happened through the invisible row's
    /// option, which is what lights Apply and what makes Escape ask the game's own
    /// "%OptionExitWithoutApplyMessage" - none of that is re-implemented.
    ///
    /// WHAT COMES BACK FROM THE NAMING BOX ARRIVES OUTSIDE THE PUMP - it is the game's own click
    /// handler calling the mod - so the callback only records what was typed, and the pump does the
    /// rest: the edit itself lands in the same frame the box was confirmed in, and <see cref="Tick"/>
    /// says what came of it after the screens, because a refusal follows a window closing and a
    /// screen's arrival interrupts anything queued ahead of it.
    /// </summary>
    internal static class ScannerEditor
    {
        /// <summary>The game's key for the tab - an identifier, never a spoken word (the words are
        /// <see cref="ModStrings.ModSettingsScanner"/>).</summary>
        public const string CategoryName = "Scanner";

        /// <summary>How long a name or a keyword may be. The box enforces it as the player types, so
        /// this is the only place the limit has to exist.</summary>
        private const int MaxChars = 40;

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
        }

        /// <summary>The invisible row's option, handed over once the panel has built it. It is the
        /// only thing the window's Apply/Cancel machinery can be told about an edit through.</summary>
        public static void Marker(Option option)
        {
            _marker = option;
        }

        // ---- the tree ----

        /// <summary>Whether this tab is the editor's - asked by the mod's options screen, of the
        /// game's own category NAME rather than of the drawn label, which is localized.</summary>
        public static bool Owns(string categoryName)
        {
            return categoryName == CategoryName && ModOptions.IsOurs(OptionsScreen.Window());
        }

        public static void Build(GraphBuilder builder)
        {
            for (int i = 0; i < ScannerCustomSlots.Count; i++)
            {
                int slot = i;
                ControlId id = ControlId.Structural(Key(slot));
                builder.BeginGroup(id, GraphNodes.Group(() => SlotLabel(slot)));
                if (builder.IsExpanded(id))
                {
                    AddName(builder, slot);
                    if (Working.Slot(slot) != null)
                    {
                        AddCategories(builder, slot);
                        AddKeywords(builder, slot);
                        AddClear(builder, slot);
                    }
                }

                builder.EndGroup();
            }
        }

        private static string Key(int slot)
        {
            return "scanner:slot/" + slot;
        }

        private static string SlotLabel(int slot)
        {
            return ModStrings.Format(ModStrings.ScannerEditSlot, slot + 1, NameOf(slot));
        }

        /// <summary>What the slot is called - the player's own name, or the word for a slot nobody has
        /// filled. Both are spoken in the same place, so an empty slot is heard as a slot rather than
        /// as a gap in the list.</summary>
        private static string NameOf(int slot)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            return category == null ? ModStrings.Get(ModStrings.ScannerEditEmpty) : category.Name;
        }

        private static void AddName(GraphBuilder builder, int slot)
        {
            int at = slot;
            builder.AddItem(
                ControlId.Structural(Key(slot) + "/name"),
                GraphNodes.Button(
                    () => ModStrings.Format(ModStrings.ScannerEditName, NameOf(at)),
                    () => AskName(at)
                )
            );
        }

        /// <summary>One expandable group per built-in scanner category, each saying how many of its
        /// columns this custom category draws from - the count is what makes a hundred-column taxonomy
        /// walkable, since a category with nothing ticked can be passed over by ear.</summary>
        private static void AddCategories(GraphBuilder builder, int slot)
        {
            IList<ScannerTaxonomyCategory> categories = Taxonomy.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                ScannerTaxonomyCategory category = categories[i];
                int at = slot;
                string key = category.Key;
                string label = category.Label;
                ControlId id = ControlId.Structural(Key(slot) + "/cat/" + key);
                builder.BeginGroup(
                    id,
                    GraphNodes.Group(
                        () =>
                            ModStrings.Format(
                                ModStrings.ScannerEditSelected,
                                label,
                                Chosen(at, key)
                            )
                    )
                );
                if (builder.IsExpanded(id))
                {
                    AddColumns(builder, slot, key);
                }

                builder.EndGroup();
            }
        }

        private static void AddColumns(GraphBuilder builder, int slot, string categoryKey)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            IList<ScannerTaxonomyColumn> columns = Taxonomy.Offer(
                categoryKey,
                category == null ? null : category.Selectors
            );
            for (int i = 0; i < columns.Count; i++)
            {
                int at = slot;
                ScannerTaxonomyColumn column = columns[i];
                ScannerSelector selector = new ScannerSelector(categoryKey, column.Key);
                // A column this galaxy has nothing of has no words of its own: it is named by the key
                // it was saved as, said as the stale thing it is, so the player can take it off.
                string label = column.Missing
                    ? ModStrings.Format(ModStrings.ScannerEditMissing, column.Key)
                    : column.Label;
                builder.AddItem(
                    ControlId.Structural(Key(slot) + "/cat/" + categoryKey + "/" + column.Key),
                    GraphNodes.Checkbox(
                        () => label,
                        () => Holds(at, selector),
                        () => Toggle(at, selector)
                    )
                );
            }
        }

        private static void AddKeywords(GraphBuilder builder, int slot)
        {
            ControlId id = ControlId.Structural(Key(slot) + "/keywords");
            builder.BeginGroup(
                id,
                GraphNodes.Group(() => ModStrings.Get(ModStrings.ScannerEditKeywords))
            );
            if (builder.IsExpanded(id))
            {
                int at = slot;
                builder.AddItem(
                    ControlId.Structural(Key(slot) + "/keywords/add"),
                    GraphNodes.Button(
                        () => ModStrings.Get(ModStrings.ScannerEditAddKeyword),
                        () => AskKeyword(at)
                    )
                );

                ScannerCustomCategory category = Working.Slot(slot);
                IList<string> keywords = category.Keywords;
                for (int i = 0; i < keywords.Count; i++)
                {
                    // Keyed by position in the list, the way any repeated node is: a word removed
                    // moves every word below it up, and the cursor lands on whatever now sits here.
                    string word = keywords[i];
                    builder.AddItem(
                        ControlId.Structural(Key(slot) + "/keywords/" + i),
                        GraphNodes.Button(
                            () => ModStrings.Format(ModStrings.ScannerEditRemoveKeyword, word),
                            () => RemoveKeyword(at, word)
                        )
                    );
                }
            }

            builder.EndGroup();
        }

        /// <summary>Emptying the slot. No confirmation: Cancel on the window is the undo, and one is
        /// enough - a question in front of every clear would be a question the player answers a
        /// hundred times to use the feature once.</summary>
        private static void AddClear(GraphBuilder builder, int slot)
        {
            int at = slot;
            builder.AddItem(
                ControlId.Structural(Key(slot) + "/clear"),
                GraphNodes.Button(
                    () => ModStrings.Get(ModStrings.ScannerEditClear),
                    () => Clear(at)
                )
            );
        }

        // ---- what the controls do ----

        private static int Chosen(int slot, string categoryKey)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            int count = 0;
            for (int i = 0; category != null && i < category.Selectors.Count; i++)
            {
                if (category.Selectors[i].Category == categoryKey)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool Holds(int slot, ScannerSelector selector)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            for (int i = 0; category != null && i < category.Selectors.Count; i++)
            {
                if (category.Selectors[i].Same(selector))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Toggle(int slot, ScannerSelector selector)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            if (category == null)
            {
                return;
            }

            if (Holds(slot, selector))
            {
                category.RemoveSelector(selector);
            }
            else
            {
                category.AddSelector(selector);
            }

            Changed();
        }

        /// <summary>Take a word out. The button goes with it, so the sentence interrupts - said from
        /// the keypress, before the rebuild that drops the node - and the place the cursor lands next
        /// is announced after it, queued, by the navigator's own arrival.</summary>
        private static void RemoveKeyword(int slot, string keyword)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            if (category == null || !category.RemoveKeyword(keyword))
            {
                return;
            }

            Voice.Say(ModStrings.Format(ModStrings.ScannerEditRemoved, keyword), true);
            Changed();
        }

        private static void Clear(int slot)
        {
            if (Working.Slot(slot) == null || !Working.Clear(slot))
            {
                return;
            }

            Voice.Say(ModStrings.Format(ModStrings.ScannerEditCleared, slot + 1), true);
            Changed();
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

        // ---- the naming box ----

        /// <summary>
        /// Open the game's own rename box, pre-filled.
        ///
        /// It is the box the game opens for a system or a fleet, and it is already a screen of the
        /// mod's (<see cref="RenameModalScreen"/>) - the heading, the field, Cancel and Confirm all
        /// read without anything written here. It is IN-GAME ONLY, which is the same condition the
        /// Scanner tab itself is under.
        /// </summary>
        private static void AskName(int slot)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            Ask(
                ModStrings.Format(ModStrings.ScannerEditNamePrompt, slot + 1),
                category == null ? ScannerCustomCategory.DefaultName(slot) : category.Name,
                new RenameModalWindow.RenameInputValidated(
                    delegate(string typed)
                    {
                        Rename(slot, typed);
                    }
                )
            );
        }

        private static void AskKeyword(int slot)
        {
            ScannerCustomCategory category = Working.Slot(slot);
            if (category == null)
            {
                return;
            }

            Ask(
                ModStrings.Format(ModStrings.ScannerEditKeywordPrompt, category.Name),
                string.Empty,
                new RenameModalWindow.RenameInputValidated(
                    delegate(string typed)
                    {
                        AddKeyword(slot, typed);
                    }
                )
            );
        }

        private static void Ask(
            string message,
            string original,
            RenameModalWindow.RenameInputValidated validated
        )
        {
            try
            {
                if (!Gui.GuiServiceAvailable)
                {
                    return;
                }

                Unfreeze();
                Gui.GuiService.RequestNewName(
                    message,
                    original,
                    MaxChars,
                    new RenameModalWindow.RenameInputChanged(Accept),
                    validated
                );
            }
            catch (Exception e)
            {
                Log.Warn("mod options: opening the naming box threw: " + e);
            }
        }

        /// <summary>
        /// LET THE NAMING BOX RUN AT ALL.
        ///
        /// The box is the game's, and it lives on the exclusive modal stack (<c>ModalRenderer</c>).
        /// The options window - the game's own as much as this clone of it - lives on
        /// <c>OverlayRenderer</c>, which is the LAST age screen, and every <c>GuiModalWindow</c> with
        /// <c>HideGuiBehind</c> hides and disables every screen BEHIND its own as it finishes showing
        /// (<c>GuiModalWindow.OnEndShow</c>). So while the settings window is up the whole modal
        /// screen is switched off: its root is not visible, <c>AgeTransform.UpdateHierarchy</c>
        /// returns at once, and the box's own fade-in never advances a frame - it sits at alpha 0,
        /// never becomes <c>IsReady</c>, and the mod's rename screen never arrives (measured
        /// 2026-08-23).
        ///
        /// Putting the screens behind back for as long as the box is up is the whole fix, and it is
        /// the game's own call in both directions. What it cannot fix is DRAWING: the overlay screen
        /// sorts above the modal one, so the box comes up behind the settings window's own opaque
        /// background and a sighted player does not see it. That is the same trade the Scanner tab
        /// already makes.
        /// </summary>
        private static void Unfreeze()
        {
            ModOptionsWindow window = ModOptions.Window();
            if (window == null)
            {
                return;
            }

            try
            {
                Gui.GuiService.ShowAllAgeScreensBehind(window.AgeTransform.Screen);
                Gui.GuiService.EnableAllAgeScreensBehind(window.AgeTransform.Screen);
                _unfroze = true;
            }
            catch (Exception e)
            {
                Log.Warn("mod options: waking the modal screen for the naming box threw: " + e);
            }
        }

        /// <summary>Put the screens behind back the way the window left them, once the box has
        /// finished going away. Nothing to do where the window itself has gone: hiding it is where
        /// the game shows them again anyway.</summary>
        private static void Refreeze()
        {
            if (!_unfroze)
            {
                return;
            }

            try
            {
                ModOptionsWindow window = ModOptions.Window();
                if (window == null || !window.Shown)
                {
                    _unfroze = false;
                    return;
                }

                RenameModalWindow box = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<RenameModalWindow>(false)
                    : null;
                if (box != null && (box.Shown || box.Visible))
                {
                    return;
                }

                Gui.GuiService.HideAllAgeScreensBehind(window.AgeTransform.Screen);
                Gui.GuiService.DisableAllAgeScreensBehind(window.AgeTransform.Screen);
                _unfroze = false;
            }
            catch (Exception e)
            {
                Log.Warn("mod options: putting the screens behind back threw: " + e);
                _unfroze = false;
            }
        }

        /// <summary>What the box asks about the text as it is typed. The window asserts this is
        /// non-null and this build of the game never calls it - the method that would is only reached
        /// from a profanity filter the shipped build compiled out - so the refusals live where the
        /// name is actually accepted, in <see cref="Tick"/>.</summary>
        private static bool Accept(string previous, string typed, ref string failure)
        {
            return true;
        }

        /// <summary>
        /// Say what came of the box, and put the screens behind back once it has gone.
        ///
        /// AFTER the screens, because a refusal follows a window closing: the settings window
        /// announces itself again as the box goes and that announcement interrupts, so a sentence
        /// queued ahead of it would be thrown away. Queued, so it lands behind the window's own
        /// arrival and behind the control the cursor is on rather than cutting either off.
        /// </summary>
        public static void Tick()
        {
            string say = _say;
            _say = null;
            Voice.Say(say, false);
            Refreeze();
        }

        /// <summary>
        /// The name that came back from the box, applied AT ONCE - not deferred to the pump.
        ///
        /// The mod's activation of Confirm runs the game's own click handler synchronously, so this
        /// runs in the same frame's key handling and the settings window's return - which happens
        /// later in that frame - reads the new name. Deferring it by a frame was measured: the slot
        /// announced itself as it WAS and then again as it is, two readouts of one edit, the first of
        /// them saying "empty" about a slot the player had just named.
        ///
        /// Only the SPEECH is deferred (<see cref="_say"/>), and only because a screen arrival
        /// interrupts. The cursor never moved while the box was up, so the landing has to be asked
        /// for as well, or a rename that took would be silent.
        /// </summary>
        private static void Rename(int slot, string typed)
        {
            string wanted = ScannerCustomCategory.Clean(typed);
            if (wanted == null)
            {
                return;
            }

            if (Working.NameTaken(wanted, slot, Taxonomy.Labels()))
            {
                _say = ModStrings.Format(ModStrings.ScannerEditNameTaken, wanted);
                Reread();
                return;
            }

            ScannerCustomCategory category = Working.Slot(slot);
            if (category == null)
            {
                Working.Set(slot, new ScannerCustomCategory(wanted));
            }
            else if (!category.Rename(wanted))
            {
                Reread();
                return;
            }

            Changed();
            Reread();
        }

        private static void AddKeyword(int slot, string typed)
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
                Reread();
                return;
            }

            Changed();
            Reread();
        }

        /// <summary>Read the control the cursor is on again. Focus never moved while the box was up,
        /// so the ordinary "say it when the cursor moves" rule would leave a successful rename
        /// silent.</summary>
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
        /// <summary>What the next tick says, once the window has finished announcing itself again.
        /// </summary>
        private static string _say;

        /// <summary>Whether the screens behind the settings window are currently put back for the
        /// naming box (<see cref="Unfreeze"/>).</summary>
        private static bool _unfroze;
    }
}

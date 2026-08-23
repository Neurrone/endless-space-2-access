using System.Collections;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// The mod's own options window: the GAME's options modal, cloned, with the mod's categories in
    /// it instead of the game's six.
    ///
    /// It is a subclass rather than a second use of the game's window because the whole difference
    /// is <see cref="Load"/>, which in the original hard-codes Video/Audio/Gameplay/Gui/Controls/
    /// Notifications. Everything below that - the tab bar, the panels, Apply/Cancel/Escape, the
    /// backup-and-restore of every value, the skin swap between the main menu and a running game -
    /// is the game's, unchanged, and so is every row: a key-mapping row here is the same prefab the
    /// Controls tab uses, which is why the mod's options SCREEN reads this window with no changes.
    ///
    /// <see cref="Load"/> never calls <c>base.Load()</c>: that is the method that builds the six.
    /// It re-expresses what the two ancestors do (yield a frame, then bind the window's
    /// <c>[GuiBound]</c> properties) and then does the same work the original does, over the mod's
    /// own category list. Everything it touches on the base class is private, so it goes through
    /// <see cref="ModOptions"/>'s reflection - the five members that make this window possible are
    /// listed there.
    ///
    /// The other override is <see cref="OnBeginHide"/>, which is where the mod's settings file is
    /// written. By the time the window hides, Apply has committed the new values or Cancel has put
    /// the backups back, so "save on hide" IS "save on apply" and needs no hook into either button.
    /// </summary>
    public sealed class ModOptionsWindow : OptionsModalWindow
    {
        /// <summary>The GameObject's name, which is also how a leftover from a crashed teardown is
        /// found: after a hot reload the old load's TYPES no longer match, and a name does.
        /// </summary>
        public const string WindowName = "ES2AccessModOptionsWindow";

        /// <summary>Start the coroutine the game would have started for one of its own windows.
        /// Nothing does it for a clone - <c>GuiManager.LoadGuiWindows</c> ran long before this
        /// existed.</summary>
        internal void BeginLoad()
        {
            StartCoroutine(Load());
        }

        protected override IEnumerator Load()
        {
            // What GuiWindow.Load and GuiModalWindow.Load do, said again rather than called: the
            // one thing between them and here is OptionsModalWindow.Load, which builds the game's
            // own six categories.
            yield return null;
            GuiBoundAttribute.BindAllProperties(this);

            IList<ModCategory> categories = ModOptions.Categories;
            string[] names = new string[categories.Count];
            for (int i = 0; i < categories.Count; i++)
            {
                names[i] = categories[i].Name;
            }

            ModOptions.SetPrivate(this, "tabPanels", new Dictionary<string, OptionsTabPanel>());
            ModOptions.SetPrivate(this, "tabToggles", new Dictionary<string, OptionsTabToggle>());
            ModOptions.SetPrivate(this, "categoryNames", names);
            ModOptions.SetPrivateProperty(
                this,
                "CurrentApplicationSettingsCategory",
                names.Length == 0 ? null : names[0]
            );

            for (int i = 0; i < categories.Count; i++)
            {
                ModCategory category = categories[i];
                ModOptions.AddCategory(this, i, category, categories.Count);
                Relabel(category);
                if (category.Fill != null)
                {
                    category.Fill(Panel(category.Name));
                }

                // A frame between categories, exactly as the original yields between its own: the
                // panels instantiate a prefab each and the rows instantiate one apiece.
                yield return null;
            }

            RadioGroup.Load();
            RadioGroup.CurrentSelection = 0;
            OptionsTabPanel first = names.Length == 0 ? null : Panel(names[0]);
            if (first != null)
            {
                first.Show();
            }
        }

        /// <summary>
        /// Write the mod's own words over the tab the game has just built.
        ///
        /// Not optional: the game names a tab with <c>%OptionToggle&lt;Category&gt;Title</c>, and a
        /// localization key nothing has a row for comes back UNCHANGED from the localizer - so an
        /// unrelabelled tab draws and speaks "%OptionToggleKeybindsTitle".
        /// </summary>
        private void Relabel(ModCategory category)
        {
            try
            {
                OptionsTabToggle toggle = Toggle(category.Name);
                if (toggle == null)
                {
                    return;
                }

                if (toggle.TitleLabel != null)
                {
                    toggle.TitleLabel.Text = category.Title();
                }

                AgeTooltip tooltip =
                    toggle.Toggle == null || toggle.Toggle.AgeTransform == null
                        ? null
                        : toggle.Toggle.AgeTransform.AgeTooltip;
                if (tooltip != null)
                {
                    tooltip.Content = category.Description();
                }
            }
            catch (System.Exception e)
            {
                Log.Warn("mod options: naming the " + category.Name + " tab threw: " + e);
            }
        }

        /// <summary>
        /// Start the scanner editor from what is saved, BEFORE the window takes its backup of every
        /// option.
        ///
        /// The order is the whole point: the base call is where <c>BackupApplicationSettings</c>
        /// runs, and the Scanner tab's one option answers "does what the player has edited differ
        /// from what is saved". A copy left over from the last time the window was open would be
        /// backed up as already-changed, which would light Apply on a window nobody has touched.
        /// </summary>
        protected override void OnBeginShow(bool instant)
        {
            // Before the base call, which is where the game takes its backup of every option: the
            // slot pages are rebuilt from what is SAVED, so no row is backed up already-changed.
            ScannerEditor.Begin();
            for (int slot = 0; slot < ScannerCustomSlots.Count; slot++)
            {
                ScannerSlotRows.Refill(slot);
            }

            ScannerRows.Relabel();
            base.OnBeginShow(instant);
        }

        /// <summary>Save what the player settled on. Apply has already written the new values into
        /// the mod's stores and Cancel has already put the old ones back, so whatever is in them now
        /// is what the player asked to keep.</summary>
        protected override void OnBeginHide(bool instant)
        {
            base.OnBeginHide(instant);
            ModOptions.Persist();
        }

        private OptionsTabPanel Panel(string category)
        {
            return ModOptions.PanelOf(this, category);
        }

        private OptionsTabToggle Toggle(string category)
        {
            return ModOptions.ToggleOf(this, category);
        }
    }
}

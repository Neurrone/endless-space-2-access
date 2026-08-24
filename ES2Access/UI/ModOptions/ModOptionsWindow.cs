using System.Collections;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using Amplitude.Unity.Options;
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

            TakeOverTheResetButtons();
        }

        /// <summary>
        /// POINT THE WINDOW'S "RESET TO DEFAULTS" AT THE MOD'S OWN KEYS.
        ///
        /// The clone came with the button - twice, once per skin - and with the game's wiring on it,
        /// which resets the GAME's bindings (<c>OptionsModalWindow.OnResetConfirmation</c> :353-361
        /// calls <c>IInputOptionsService.ResetToDefaultBindings</c>). On this window that would be a
        /// button that silently rewrote a page it is not even showing. Every AGE button dispatches by
        /// SendMessage to a named method on a GameObject, so re-aiming it is two fields, and the
        /// game's own window is untouched.
        ///
        /// The button's VISIBILITY is the game's own rule and is left alone: it shows for the
        /// category called "Controls" and no other (:119-120, :223-224), which is exactly what the
        /// mod's key-binding tab is called.
        /// </summary>
        private void TakeOverTheResetButtons()
        {
            Aim(ResetButton);
            Aim(ResetInGameButton);
        }

        private void Aim(AgeControlButton button)
        {
            if (button == null)
            {
                return;
            }

            try
            {
                button.OnActivateObject = gameObject;
                button.OnActivateMethod = "OnModResetCb";
            }
            catch (System.Exception e)
            {
                Log.Warn("mod options: aiming the reset button threw: " + e);
            }
        }

        /// <summary>Ask the question the game asks before its own reset, in the game's own words.
        /// Public because the button reaches it by SendMessage; the argument is the GameObject every
        /// AGE button sends and nothing here needs it.</summary>
        public void OnModResetCb(UnityEngine.GameObject sender)
        {
            if (ModOptions.KeybindsCategory != Category())
            {
                return;
            }

            try
            {
                Gui.GuiService.ShowMessage(
                    ResetConfirmation,
                    MessageBoxType.IMPORTANT,
                    OnModResetConfirmation
                );
            }
            catch (System.Exception e)
            {
                Log.Warn("mod options: asking about a reset threw: " + e);
            }
        }

        /// <summary>
        /// PUT EVERY MOD KEY BACK ON THE KEYS IT SHIPPED ON.
        ///
        /// Through each row's own option, which is the same path a rebind takes: the value lands in
        /// the binding store and on the live input layer at once, the window is told a setting
        /// changed - so Apply lights and Cancel puts the old keys back - and the row redraws both its
        /// fields. Applying then writes the file, where every action now matching its default drops
        /// its line (<c>ModBindings.Persist</c>). That mirrors the game's own reset, which also acts
        /// at once and marks the window dirty.
        /// </summary>
        private void OnModResetConfirmation(object sender, MessageBoxResultEventArgs e)
        {
            if (e == null || e.Result != MessageBoxResult.Ok)
            {
                return;
            }

            try
            {
                OptionsTabPanel panel = Panel(ModOptions.KeybindsCategory);
                if (panel == null || panel.OptionsTable == null)
                {
                    return;
                }

                Option last = null;
                for (int i = 0; i < panel.OptionsTable.Children.Count; i++)
                {
                    OptionKeyMappingItem row =
                        panel.OptionsTable.Children[i].GetComponent<OptionKeyMappingItem>();
                    Amplitude.Unity.Input.InputBinding now =
                        row == null || row.Option == null
                            ? null
                            : row.Option.Value as Amplitude.Unity.Input.InputBinding;
                    Amplitude.Unity.Input.InputBinding shipped =
                        now == null
                            ? null
                            : ES2Access.UI.Input.ModBindings.Default(now.InputAction.ToString());
                    if (shipped == null)
                    {
                        continue;
                    }

                    row.Option.Value = shipped;
                    row.Refresh();
                    last = row.Option;
                }

                if (last != null)
                {
                    OnOptionChanged(last);
                }

                Dirty = true;
            }
            catch (System.Exception thrown)
            {
                Log.Warn("mod options: resetting the mod's keys threw: " + thrown);
            }
        }

        /// <summary>The game's own question before a key-binding reset.</summary>
        private const string ResetConfirmation = "%OptionBindingResetConfirmation";

        /// <summary>Which of the two tabs is showing, read from the private the game keeps it in.
        /// </summary>
        private string Category()
        {
            OptionsTabToggle toggle = Toggle(ModOptions.KeybindsCategory);
            return toggle != null && toggle.Toggle != null && toggle.Toggle.State
                ? ModOptions.KeybindsCategory
                : null;
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
            ScannerRows.Refill();
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

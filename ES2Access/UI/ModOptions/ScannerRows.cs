using System;
using Amplitude.Unity.Options;
using ES2Access.Core.Util;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// The Scanner tab's rows - or rather its ONE row, which is hidden the moment the game has built
    /// it.
    ///
    /// The panel loads normally: <see cref="IModScannerService"/> declares one toggle, so the game's
    /// own <c>OptionsTabPanel.Load</c> instantiates one checkbox from its own prefab and the panel is
    /// a panel like any other - backed up on show, checked for changes, restored on Cancel. What it
    /// must not be is DRAWN or WALKED: the row is not a setting the player has any business toggling
    /// (see <see cref="IModScannerService"/>), and everything the tab actually offers is declared as
    /// graph nodes by <see cref="ScannerEditor"/>.
    ///
    /// Hiding it is enough for both. The mod's options screen never reads this panel's rows at all -
    /// it hands the whole rows region to the editor - and every scan the window makes walks
    /// <c>OptionsTable.Children</c>, which does not care whether a child is visible.
    ///
    /// It is also why the row's raw <c>%OptionScannerCustomCategoriesTitle</c> label is left alone
    /// where the Keybinds rows have theirs rewritten: nothing draws it and nothing speaks it.
    /// </summary>
    public static class ScannerRows
    {
        public static void Fill(OptionsTabPanel panel)
        {
            if (panel == null || panel.OptionsTable == null)
            {
                Log.Warn("mod options: the Scanner panel is not built");
                return;
            }

            try
            {
                Option[] options = panel.Options;
                if (options == null || options.Length == 0)
                {
                    Log.Warn("mod options: the Scanner panel loaded no option to track edits with");
                    return;
                }

                ScannerEditor.Marker(options[0]);
                for (int i = 0; i < panel.OptionsTable.Children.Count; i++)
                {
                    panel.OptionsTable.Children[i].Visible = false;
                }
            }
            catch (Exception e)
            {
                Log.Warn("mod options: hiding the Scanner panel's row threw: " + e);
            }
        }
    }
}

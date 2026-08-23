using System;
using System.Collections.Generic;
using Amplitude.Unity.Options;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;
using ES2Access.Screens;
using ES2Access.UI;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// THE SCANNER TAB: one drawn button per slot, and the invisible row the window's Apply/Cancel
    /// hangs off.
    ///
    /// The button says which slot it is and what is in it - "Custom category 1: Watch list", "Custom
    /// category 2: empty" - and opens that slot's own tab. It is the game's own button, cloned off
    /// the window's Cancel (<see cref="ModRows.Button"/>), because the tab has to be DRAWN: this is
    /// the game's window, and a page a sighted player finds blank is not a page (owner ruling
    /// 2026-08-24, replacing the invisible tree this tab held before).
    ///
    /// The INVISIBLE ROW is the panel's one declared option (<see cref="IModScannerService"/>) and is
    /// not a setting: its value answers "does what the player has edited differ from what is saved",
    /// which is the question the window asks to decide whether Apply lights, whether Escape asks
    /// about unapplied changes, and what Cancel puts back. Hidden because there is nothing there for
    /// anybody to toggle.
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

            _buttons.Clear();
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
                    Option option = ModRows.Button(
                        panel,
                        panel.Parent,
                        "slot" + slot + "Button",
                        Caption(slot),
                        () => Open(at)
                    );
                    if (option != null)
                    {
                        options.Add(option);
                    }

                    _buttons.Add(Row(panel, "slot" + slot + "Button"));
                }

                ModRows.Publish(panel, options);
            }
            catch (Exception e)
            {
                Log.Warn("mod options: building the Scanner tab threw: " + e);
            }
        }

        /// <summary>Say the buttons again, after a slot was named or emptied on its own tab.
        /// </summary>
        public static void Relabel()
        {
            for (int slot = 0; slot < _buttons.Count; slot++)
            {
                OptionItem item = _buttons[slot];
                if (item != null && item.TitleLabel != null)
                {
                    item.TitleLabel.Text = Caption(slot);
                }
            }
        }

        public static void Forget()
        {
            _buttons.Clear();
        }

        private static string Caption(int slot)
        {
            return ModStrings.Format(
                ModStrings.ScannerEditSlotButton,
                slot + 1,
                ScannerEditor.SpokenName(slot)
            );
        }

        /// <summary>
        /// Open the slot's own tab, and put the cursor on it.
        ///
        /// The second half is not a nicety. Switching the page takes THIS button off the screen, and
        /// a cursor whose node has gone is re-seated by the navigator - onto a TAB, where focusing is
        /// switching (the tab vtable's own OnFocusVisual), so the page the button just opened was
        /// closed again by the landing. Asking for the rows leaves the cursor where the player was
        /// going.
        /// </summary>
        private static void Open(int slot)
        {
            ModOptions.OpenCategory(ScannerEditor.SlotCategory(slot));
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator != null)
            {
                navigator.FocusStop(OptionsScreen.RowStop);
            }
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

        private static readonly List<OptionItem> _buttons = new List<OptionItem>();
    }
}

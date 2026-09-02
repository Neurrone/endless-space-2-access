using System;
using System.Collections.Generic;
using Amplitude.Unity.Options;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.UI.Input;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// The Keybinds category's rows - SIX TABLES, each under a heading of its own, built out of the
    /// game's own prefab and read by the mod's options screen with nothing written for it.
    ///
    /// The game builds a panel's rows by reflecting over ONE provider's properties, which cannot
    /// express a list that grows. So the panel is loaded empty and filled here instead, through the
    /// two public doors the game leaves open: the panel's own <c>OptionKeyMappingPrefab</c>, and
    /// <c>OptionItem.Load(option, window, panel)</c>. The only closed door is the panel's
    /// <c>Options</c> array, whose setter is private - and that array is what the game's own scans
    /// read, so it is written by reflection at the end (<c>ModRows.Publish</c>).
    ///
    /// Row order is <see cref="KeybindLayout"/>'s, not the order the mod registers its actions in:
    /// which key exists is the input layer's business and where its row is drawn is the page's, and
    /// they are kept apart so that moving a row cannot move a binding. Each heading is a
    /// <c>ModRows.Caption</c>, which the options screen turns into the name of a REGION - so
    /// "3 of 22" counts the table the player is in and Alt+arrow walks the page by its six names.
    ///
    /// The table's priority comparer is dropped before the first row goes in, so nothing can
    /// re-sort rows that all carry the same priority into an order nobody chose.
    /// </summary>
    public static class KeybindRows
    {
        public static void Fill(OptionsTabPanel panel)
        {
            if (panel == null || panel.OptionsTable == null || panel.OptionKeyMappingPrefab == null)
            {
                Log.Warn("mod options: the Keybinds panel is not built, no rows added");
                return;
            }

            // Before anything is added, not after: the comparer runs from AgeTransform.Init inside
            // InstantiateChild, so a row it cannot sort throws on the way in.
            ModRows.Begin(panel);
            List<Option> options = new List<Option>();
            int index = 0;
            KeybindLayout.Block[] blocks = KeybindLayout.Blocks;
            for (int b = 0; b < blocks.Length; b++)
            {
                KeybindLayout.Block block = blocks[b];
                Option heading = ModRows.Caption(
                    panel,
                    index + "keysCaption",
                    ModStrings.Get(block.TitleKey)
                );
                index++;
                if (heading != null)
                {
                    options.Add(heading);
                }

                for (int i = 0; i < block.Actions.Length; i++)
                {
                    Option option = Add(panel, index, block.Actions[i]);
                    index++;
                    if (option != null)
                    {
                        options.Add(option);
                    }
                }
            }

            ModRows.Publish(panel, options);
        }

        private static Option Add(OptionsTabPanel panel, int index, string actionKey)
        {
            try
            {
                ModBindingOption provider = new ModBindingOption(actionKey);
                Option[] minted = Option.GetOptions(
                    provider,
                    typeof(IModBindingProvider),
                    true,
                    true,
                    true
                );
                if (minted.Length == 0)
                {
                    Log.Warn("mod options: no binding option minted for " + actionKey);
                    return null;
                }

                Option option = minted[0];
                AgeTransform row = panel.OptionsTable.InstantiateChild(
                    panel.OptionKeyMappingPrefab,
                    index + actionKey + "KeyMapping"
                );
                row.Init();
                OptionKeyMappingItem item = row.GetComponent<OptionKeyMappingItem>();
                if (item == null)
                {
                    Log.Warn("mod options: the key mapping prefab has no OptionKeyMappingItem");
                    return null;
                }

                item.Load(option, panel.Parent, panel);
                // After Load, never before: Load writes the game's own %Option<Name>Title into both,
                // and an unregistered key comes back from the localizer unchanged - so leaving them
                // would draw and speak the raw key.
                if (item.TitleLabel != null)
                {
                    item.TitleLabel.Text = ModBindings.Title(actionKey);
                }

                if (item.Tooltip != null)
                {
                    item.Tooltip.Content = ModBindings.Description(actionKey);
                }

                return option;
            }
            catch (Exception e)
            {
                Log.Warn("mod options: building the row for " + actionKey + " threw: " + e);
                return null;
            }
        }
    }
}

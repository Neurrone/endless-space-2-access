using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude.Unity.Options;
using ES2Access.Core.Util;
using ES2Access.UI.Input;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// The Keybinds category's rows - one key-mapping row per action the mod registers, built out of
    /// the game's own prefab and read by the mod's options screen with nothing written for it.
    ///
    /// The game builds a panel's rows by reflecting over ONE provider's properties, which cannot
    /// express a list that grows. So the panel is loaded empty and filled here instead, through the
    /// two public doors the game leaves open: the panel's own <c>OptionKeyMappingPrefab</c>, and
    /// <c>OptionItem.Load(option, window, panel)</c>. The only closed door is the panel's
    /// <c>Options</c> array, whose setter is private - and that array is what the game's own scans
    /// read, so it is written by reflection at the end.
    ///
    /// Row order is the order the mod REGISTERS its actions in, which already groups them by family
    /// (the cursor's keys, then the map's, then the review buffer's). The table's priority
    /// comparer is dropped afterwards, so nothing can re-sort rows that all carry the same priority
    /// into an order nobody chose.
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

            List<Option> options = new List<Option>();
            IList<string> actionKeys = ModBindings.ActionKeys;
            for (int i = 0; i < actionKeys.Count; i++)
            {
                Option option = Add(panel, i, actionKeys[i]);
                if (option != null)
                {
                    options.Add(option);
                }
            }

            SetOptions(panel, options.ToArray());
            // The panel's own comparer sorts by option priority, and every row here has the same
            // one - so with it in place the table lands in whatever order the instantiation left,
            // which measured as the REVERSE of the order the rows went in. Cleared, the table's
            // fallback comparer is sibling index, and sorting by that is the order they were added.
            panel.OptionsTable.ChildrenComparer = null;
            panel.OptionsTable.Sort();
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

        private static void SetOptions(OptionsTabPanel panel, Option[] options)
        {
            try
            {
                PropertyInfo property = typeof(OptionsTabPanel).GetProperty(
                    "Options",
                    BindingFlags.Instance | BindingFlags.Public
                );
                MethodInfo setter = property == null ? null : property.GetSetMethod(true);
                if (setter == null)
                {
                    Log.Warn("mod options: OptionsTabPanel.Options has no setter");
                    return;
                }

                setter.Invoke(panel, new object[] { options });
            }
            catch (Exception e)
            {
                Log.Warn("mod options: setting the panel's options threw: " + e);
            }
        }
    }
}

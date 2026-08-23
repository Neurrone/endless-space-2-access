using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// THE WAY INTO THE MOD'S SETTINGS - one entry the mod declares on the main menu and on the
    /// pause menu, immediately after the game's own Options entry.
    ///
    /// It is a node with no widget behind it: nothing is drawn for it and a sighted player never
    /// meets it, which is the shape the graph already uses for things the game does not draw (the
    /// map's system cards, the mod's own notification row). That is deliberate - the pause menu's
    /// entries are a circle of prefab items with staggered animation delays, and a real drawn entry
    /// there would be a fight with the layout for no gain to anybody who cannot see it.
    ///
    /// No key binding. Both menus are static, both are exactly where the game opens its own
    /// Options, and the window's Apply, Cancel and Escape all pop back to the menu by the game's own
    /// hand - so the entry needs no gating, no turn check and no transient-modal check
    /// (owner ruling 2026-08-23).
    /// </summary>
    public static class ModSettingsNode
    {
        /// <summary>Declare the entry under <paramref name="key"/>, which is the screen's own
        /// structural id for it.</summary>
        public static void Add(GraphBuilder builder, string key)
        {
            builder.AddItem(
                ControlId.Structural(key),
                GraphNodes.Button(
                    () => ModStrings.Get(ModStrings.ModSettingsEntry),
                    ModOptions.Open,
                    ModOptions.CanOpen
                )
            );
        }
    }
}

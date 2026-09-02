using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// THE WAY INTO THE MOD'S SETTINGS - one entry DRAWN on the main menu and on the pause menu,
    /// immediately after the game's own Options, and called "Accessibility mod settings".
    ///
    /// It used to be a node with no widget behind it, on the grounds that the pause menu's ring of
    /// prefab items would be a fight for no gain to anybody who cannot see it. Owner ruling
    /// 2026-09-02 reverses that: a sighted player sharing the machine - or helping - has to be able
    /// to find these settings too, so the entry is a real one, in the ring, clickable with the
    /// mouse, and read by each screen the same way it reads the game's own entries.
    ///
    /// Nothing here re-implements a menu. Each menu is given ONE MORE OF ITS OWN ENTRIES and then
    /// left to draw, arrange, animate and dispatch it:
    ///
    /// - THE PAUSE MENU's entries are a fixed serialized array of prefab instances
    ///   (<c>GameMenuModalWindow.GameMenuItems</c>) drawn by an AGE table whose arrangement is
    ///   CIRCLE_EVENLY_SPACED (measured 2026-09-02): the ring is the TABLE spreading its children,
    ///   not a set of hand-placed rectangles, and where an entry sits round it is decided by where
    ///   it sits in the child list. So the Options item is cloned, put in the array and in the
    ///   child list straight after Options, and the table is asked to arrange itself - which spreads
    ///   seven entries exactly as it spread six. What Load does ONCE and therefore has to be said
    ///   again is the rest: each label on the side away from the centre, and the fade-in delays
    ///   spread over the entries there now.
    ///
    /// - THE MAIN MENU's entries are DATA (<c>MainMenuScreenGuiElement.Entries</c>) and the screen
    ///   builds one item per entry on every refresh. So the mod appends one entry and the game
    ///   builds, binds, places, animates and dispatches the item for it. The click the game sends
    ///   is <c>"OnClick" + entry.Name</c> to the screen's own GameObject, which is exactly what the
    ///   mod's own main-menu screen sends when the keyboard activates an entry - so a component
    ///   answering that name on the screen makes the mouse and the keyboard converge with nothing
    ///   re-aimed (<see cref="ModSettingsClick"/>).
    ///
    /// The entry has NO TOOLTIP: the game's own entries carry a sentence about what they do, and
    /// there is no owner-approved wording for this one, so the cloned/looked-up content is cleared
    /// rather than left saying what the Options entry says.
    ///
    /// Teardown puts both menus back exactly as they were - the array, the entry list, the ring the
    /// table draws for six - and destroys what was added, BY NAME, because after a hot reload the
    /// old load's types never equal this one's.
    /// </summary>
    public static class ModSettingsMenuEntry
    {
        /// <summary>The GameObject the mod adds to the pause menu's ring. Distinctive, because
        /// teardown finds it by name.</summary>
        public const string PauseMenuItemName = "ES2AccessModSettingsMenuItem";

        /// <summary>The main menu entry's own name - the identifier the game builds the item from,
        /// names the GameObject after, and sends the click under.</summary>
        public const string MainMenuEntryName = "ES2AccessModSettings";

        /// <summary>What each screen keys its node under. Unchanged from when the entry was
        /// synthetic, so a doc, a recipe or a remembered cursor still finds it.</summary>
        public const string PauseMenuNodeKey = "gamemenu:mod-settings";
        public const string MainMenuNodeKey = "mainmenu:mod-settings";

        /// <summary>The method the pause menu's button is aimed at, on the mod's own options window.
        /// </summary>
        public const string ActivateMethod = "OnAccessibilitySettingsCb";

        /// <summary>The words on the entry, both menus.</summary>
        public static string Title()
        {
            return ModStrings.Get(ModStrings.ModSettingsEntry);
        }

        /// <summary>The sentence beside them, both menus (owner ruling 2026-09-02): the entry is
        /// named as briefly as the menu can draw on one line, so the tooltip is where it says which
        /// mod these settings belong to. Every neighbouring entry on both menus carries one.
        /// </summary>
        public static string Description()
        {
            return ModStrings.Get(ModStrings.ModSettingsEntryDescription);
        }

        /// <summary>Put the entry on whichever menu exists, and keep it right. Called once a frame
        /// from the pump; the whole cost on a frame where nothing has changed is two null checks and
        /// an array scan.</summary>
        public static void Tick()
        {
            if (_stopped)
            {
                return;
            }

            try
            {
                TickPauseMenu();
            }
            catch (Exception e)
            {
                Log.Warn("mod settings entry: the pause menu threw: " + e);
            }

            try
            {
                TickMainMenu();
            }
            catch (Exception e)
            {
                Log.Warn("mod settings entry: the main menu threw: " + e);
            }
        }

        /// <summary>Give both menus back exactly what they had.</summary>
        public static void Shutdown()
        {
            _stopped = true;
            try
            {
                RemoveFromPauseMenu();
            }
            catch (Exception e)
            {
                Log.Warn("mod settings entry: unpicking the pause menu threw: " + e);
            }

            try
            {
                RemoveFromMainMenu();
            }
            catch (Exception e)
            {
                Log.Warn("mod settings entry: unpicking the main menu threw: " + e);
            }

            _stopped = false;
        }

        // ---- the pause menu ----

        private static void TickPauseMenu()
        {
            GameMenuModalWindow window = PauseMenu();
            if (window == null || window.GameMenuItems == null || !ModOptions.CanOpen())
            {
                return;
            }

            if (Index(window.GameMenuItems, PauseMenuItemName) >= 0)
            {
                return;
            }

            int options = IndexOfOptions(window.GameMenuItems);
            if (options < 0 || window.ButtonsCircularTable == null)
            {
                return;
            }

            GameMenuItem cloned = ClonePauseMenuItem(window, window.GameMenuItems[options]);
            if (cloned == null)
            {
                return;
            }

            GameMenuItem[] grown = new GameMenuItem[window.GameMenuItems.Length + 1];
            for (int i = 0, at = 0; i < window.GameMenuItems.Length; i++)
            {
                grown[at++] = window.GameMenuItems[i];
                if (i == options)
                {
                    grown[at++] = cloned;
                }
            }

            window.GameMenuItems = grown;
            _pauseMenu = window;
            Order(window.ButtonsCircularTable, cloned.AgeTransform, options + 1);
            Arrange(window, grown);
            Log.Info(
                "mod settings entry: added to the pause menu after Options ("
                    + options
                    + " of "
                    + grown.Length
                    + "): "
                    + Names(grown)
            );
        }

        /// <summary>The Options item's own clone: the same prefab instance the game placed, so the
        /// circle, the blur, the icon frame and the button all come across. What changes is the
        /// words, the tooltip and where the button sends its click.</summary>
        private static GameMenuItem ClonePauseMenuItem(
            GameMenuModalWindow window,
            GameMenuItem options
        )
        {
            if (options == null || options.AgeTransform == null)
            {
                return null;
            }

            AgeTransform made = window.ButtonsCircularTable.InstantiateChild(
                options.transform,
                PauseMenuItemName
            );
            if (made == null)
            {
                return null;
            }

            made.Init();
            GameMenuItem item = made.GetComponent<GameMenuItem>();
            if (item == null)
            {
                Log.Warn("mod settings entry: the cloned pause-menu item has no GameMenuItem");
                UnityEngine.Object.DestroyImmediate(made.gameObject);
                return null;
            }

            string title = Title();
            if (item.LabelRight != null)
            {
                item.LabelRight.Text = title;
            }

            if (item.LabelLeft != null)
            {
                item.LabelLeft.Text = title;
            }

            AgeTransform button = item.ButtonAgeTransform;
            if (button != null)
            {
                if (button.AgeTooltip != null)
                {
                    // The clone brought the Options entry's own sentence with it; this entry's own
                    // goes in its place, so the row explains itself the way its neighbours do.
                    button.AgeTooltip.Content = Description();
                }

                AgeControlButton control = button.GetComponent<AgeControlButton>();
                ModOptionsWindow settings = ModOptions.Window();
                if (control != null && settings != null)
                {
                    // Those two fields ARE the mouse: an AGE button dispatches by SendMessage to a
                    // named method on a named GameObject, and a button with nothing in them cannot
                    // be pressed at all.
                    control.OnActivateObject = settings.gameObject;
                    control.OnActivateMethod = ActivateMethod;
                }
            }

            made.Enable = true;
            made.Visible = true;
            return item;
        }

        /// <summary>
        /// PUT THE NEW ENTRY WHERE IT BELONGS IN THE RING - which is a matter of CHILD ORDER, not
        /// of coordinates.
        ///
        /// The ring's parent is an AGE table arranging its children
        /// <c>CIRCLE_EVENLY_SPACED</c> (measured 2026-09-02): it walks its children in order and
        /// gives each the next polar angle, so where an entry sits round the circle is decided by
        /// where it sits in that list, and any X or Y the mod writes is overwritten the next time
        /// the table arranges. A child instantiated into it lands at the END, which would put the
        /// mod's entry after Resume; this moves it, in the Unity hierarchy and in the AGE list
        /// alike, so a later rebuild of either finds the same order.
        /// </summary>
        private static void Order(AgeTransform table, AgeTransform child, int at)
        {
            if (table == null || child == null)
            {
                return;
            }

            try
            {
                child.transform.SetSiblingIndex(at);
                List<AgeTransform> children = table.Children;
                if (children != null && children.Remove(child) && at <= children.Count)
                {
                    children.Insert(at, child);
                }
            }
            catch (Exception e)
            {
                Log.Warn("mod settings entry: placing the entry in the ring threw: " + e);
            }
        }

        /// <summary>
        /// Let the ring lay itself out again, and put each label back on the side away from the
        /// centre.
        ///
        /// Both are the window's own rules: the table spreads whatever children it has evenly round
        /// the circle, and <c>GameMenuModalWindow.Load</c> :76-86 decides a label's side by the
        /// entry's X against the table's centre and staggers the fade-in delays by index. Load runs
        /// ONCE, when the window loads, so an entry added afterwards needs both said again - for
        /// every entry, because seven of them stand in different places than six did.
        /// </summary>
        private static void Arrange(GameMenuModalWindow window, GameMenuItem[] items)
        {
            AgeTransform table = window.ButtonsCircularTable;
            if (table == null)
            {
                return;
            }

            // The circle counts and places only the children it can SEE - Visible, and Alpha above
            // zero unless StrictVisibility says Visible is enough (AgeTransform
            // .ApplyCircleEvenlySpacedArrangement) - and the entries fade in and out one after
            // another with the window, so at any moment between the window's animations some of them
            // are at alpha 0. Arranged then, the ring was laid out for the entries that happened to be
            // faded in and the rest kept their old slots (owner-reported 2026-09-02: the new entry
            // drawn on top of Resume). Visible-only counting for the one call puts every entry in.
            bool strict = table.StrictVisibility;
            table.StrictVisibility = true;
            try
            {
                table.ArrangeChildren();
            }
            finally
            {
                table.StrictVisibility = strict;
            }

            for (int i = 0; i < items.Length; i++)
            {
                AgeTransform a = items[i] == null ? null : items[i].AgeTransform;
                if (a == null)
                {
                    continue;
                }

                if (a.X > table.CenterX)
                {
                    items[i].ShowRightLabel();
                }
                else
                {
                    items[i].ShowLeftLabel();
                }
            }

            Stagger(window, items);
        }

        /// <summary>Re-spread the show animation's per-item delays the way the window's own
        /// <c>Load</c> spreads them, so the entries still fade in one after another round the ring
        /// in the time the ring itself takes. Run over the entries that are left, it is also what
        /// puts the original delays back: the game's own numbers come out of the same arithmetic.
        /// </summary>
        private static void Stagger(GameMenuModalWindow window, GameMenuItem[] items)
        {
            AgeModifierItem circle = FirstModifier(window.Circle);
            AgeModifierItem modal = FirstModifier(window.AgeTransform);
            if (circle == null || modal == null || items.Length == 0)
            {
                return;
            }

            float total = circle.Duration;
            float spread = total / items.Length;
            float delay = modal.Duration * 0.5f + spread * 0.5f;
            for (int i = 0; i < items.Length; i++)
            {
                AgeModifierItem item =
                    items[i] == null ? null : FirstModifier(items[i].AgeTransform);
                if (item != null)
                {
                    item.StartDelay = delay;
                    item.ReverseStartDelay = total - delay;
                }

                delay += spread;
            }
        }

        private static AgeModifierItem FirstModifier(AgeTransform transform)
        {
            AgeModifierSet set = transform == null ? null : transform.AgeFirstModifierSet;
            return set == null || set.ModifierItems == null || set.ModifierItems.Length == 0
                ? null
                : set.ModifierItems[0];
        }


        private static void RemoveFromPauseMenu()
        {
            GameMenuModalWindow window = _pauseMenu ?? PauseMenu();
            _pauseMenu = null;
            if (window == null || window.GameMenuItems == null)
            {
                return;
            }

            List<GameMenuItem> kept = new List<GameMenuItem>();
            GameObject standing = null;
            for (int i = 0; i < window.GameMenuItems.Length; i++)
            {
                GameMenuItem item = window.GameMenuItems[i];
                if (item != null && item.name == PauseMenuItemName)
                {
                    standing = item.gameObject;
                    continue;
                }

                kept.Add(item);
            }

            if (standing == null)
            {
                return;
            }

            window.GameMenuItems = kept.ToArray();
            // Destroyed first: the table spreads whatever children it still has, so the ring the
            // game drew for six comes back from the game's own arrangement rather than from
            // remembered coordinates.
            UnityEngine.Object.DestroyImmediate(standing);
            Arrange(window, window.GameMenuItems);
        }


        /// <summary>The ring in order, for the one line the injection logs - which is what says
        /// where the mod's entry landed if a prefab change ever moves the game's own.</summary>
        private static string Names(GameMenuItem[] items)
        {
            System.Text.StringBuilder names = new System.Text.StringBuilder();
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0)
                {
                    names.Append(", ");
                }

                names.Append(items[i] == null ? "?" : items[i].name);
            }

            return names.ToString();
        }

        private static int Index(GameMenuItem[] items, string name)
        {
            for (int i = 0; items != null && i < items.Length; i++)
            {
                if (items[i] != null && items[i].name == name)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Which entry opens the game's options - found by what it DOES, so a renamed
        /// prefab cannot move the mod's entry away from it.</summary>
        private static int IndexOfOptions(GameMenuItem[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                AgeTransform button = items[i] == null ? null : items[i].ButtonAgeTransform;
                AgeControlButton control =
                    button == null ? null : button.GetComponent<AgeControlButton>();
                if (control != null && control.OnActivateMethod == "OnOptionsCb")
                {
                    return i;
                }
            }

            return -1;
        }

        private static GameMenuModalWindow PauseMenu()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GameMenuModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the main menu ----

        private static void TickMainMenu()
        {
            MainMenuScreen screen = MainMenu();
            if (screen == null)
            {
                return;
            }

            MainMenuScreenGuiElement element = Element(screen);
            if (element == null || element.Entries == null)
            {
                return;
            }

            if (Index(element.Entries, MainMenuEntryName) < 0)
            {
                int options = Index(element.Entries, SettingsEntryName);
                if (options < 0)
                {
                    return;
                }

                MainMenuScreenGuiElement.MainMenuEntry mine =
                    new MainMenuScreenGuiElement.MainMenuEntry();
                mine.Name = MainMenuEntryName;
                mine.Enabled = true;
                mine.Colored = false;
                mine.SubEntries = null;

                _mainMenuEntries = element.Entries;
                MainMenuScreenGuiElement.MainMenuEntry[] grown =
                    new MainMenuScreenGuiElement.MainMenuEntry[element.Entries.Length + 1];
                for (int i = 0, at = 0; i < element.Entries.Length; i++)
                {
                    grown[at++] = element.Entries[i];
                    if (i == options)
                    {
                        grown[at++] = mine;
                    }
                }

                element.Entries = grown;
                _mainMenuElement = element;
                Log.Info("mod settings entry: added to the main menu after Options");
            }

            if (screen.gameObject.GetComponent<ModSettingsClick>() == null)
            {
                screen.gameObject.AddComponent<ModSettingsClick>();
            }

            Relabel(screen);
        }

        /// <summary>
        /// Write the mod's words over the item the game has just bound.
        ///
        /// Not optional: <c>MainMenuItem.Bind</c> takes the title and the tooltip from the game's own
        /// localization under the entry's NAME, and a key nothing has a row for comes back unchanged
        /// - so an unrelabelled entry draws and speaks "ES2AccessModSettings". The icon is the game's
        /// own fallback for an entry it has no picture for, which is what every unknown entry gets.
        /// </summary>
        private static void Relabel(MainMenuScreen screen)
        {
            AgeTransform container = screen.MainMenuItemsContainer;
            // Only while the menu is up: the screen's window exists in a running game too, and
            // walking its items for a component every frame would be a cost paid for nothing.
            if (container == null || container.Children == null || !screen.Shown)
            {
                return;
            }

            string title = Title();
            for (int i = 0; i < container.Children.Count; i++)
            {
                MainMenuItem item = container.Children[i].GetComponent<MainMenuItem>();
                if (
                    item == null
                    || item.MainMenuEntry == null
                    || item.MainMenuEntry.Name != MainMenuEntryName
                )
                {
                    continue;
                }

                // Written every frame rather than compared first: the label's own setter already
                // does nothing when the words have not changed, and the tooltip's content is a
                // plain field.
                if (item.TitleLabel != null)
                {
                    item.TitleLabel.Text = title;
                }

                if (item.Tooltip != null)
                {
                    item.Tooltip.Content = Description();
                }
            }
        }

        private static void RemoveFromMainMenu()
        {
            MainMenuScreen screen = MainMenu();
            if (screen != null)
            {
                ModSettingsClick click = screen.gameObject.GetComponent<ModSettingsClick>();
                if (click != null)
                {
                    UnityEngine.Object.DestroyImmediate(click);
                }
            }

            MainMenuScreenGuiElement element = _mainMenuElement ?? Element(screen);
            _mainMenuElement = null;
            if (element != null && _mainMenuEntries != null)
            {
                element.Entries = _mainMenuEntries;
            }

            _mainMenuEntries = null;
        }

        private static int Index(
            MainMenuScreenGuiElement.MainMenuEntry[] entries,
            string name
        )
        {
            for (int i = 0; entries != null && i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].Name == name)
                {
                    return i;
                }
            }

            return -1;
        }

        private static MainMenuScreen MainMenu()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<MainMenuScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The screen's own gui element - the DATA its entries come from. Private on the
        /// screen and null until it has loaded.</summary>
        private static MainMenuScreenGuiElement Element(MainMenuScreen screen)
        {
            if (screen == null)
            {
                return null;
            }

            try
            {
                FieldInfo field = typeof(MainMenuScreen).GetField(
                    "mainMenuScreenGuiElement",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                return field == null
                    ? null
                    : field.GetValue(screen) as MainMenuScreenGuiElement;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's own name for the main menu's Options entry.</summary>
        private const string SettingsEntryName = "MainMenuSettings";

        private static bool _stopped;
        private static GameMenuModalWindow _pauseMenu;
        private static MainMenuScreenGuiElement _mainMenuElement;
        private static MainMenuScreenGuiElement.MainMenuEntry[] _mainMenuEntries;
    }

    /// <summary>
    /// THE CLICK HALF OF THE MAIN MENU'S ENTRY, on the screen the game sends it to.
    ///
    /// Every main-menu entry, top level and sub-entry alike, is activated by one message named after
    /// the entry and sent to the screen's own GameObject (<c>MainMenuItem.OnClickCb</c>) - and that
    /// is also what the mod's own main-menu screen sends when the keyboard activates one. So a
    /// component answering that name is the whole wiring: the mouse and the keyboard arrive at the
    /// same method, and no button had to be re-aimed.
    ///
    /// It sits on the SCREEN rather than on the item because the screen rebuilds its items whenever
    /// it refreshes, and a receiver that went with them would have to be put back every time.
    /// </summary>
    public sealed class ModSettingsClick : MonoBehaviour
    {
        /// <summary>Public, and named to match the entry, because the game reaches it by
        /// SendMessage. It takes no argument: neither sender passes one.</summary>
        public void OnClickES2AccessModSettings()
        {
            try
            {
                ModOptions.Open();
            }
            catch (Exception e)
            {
                // Runs inside the engine's own dispatch: never throw into it.
                Log.Warn("mod settings entry: opening from the main menu threw: " + e);
            }
        }
    }
}

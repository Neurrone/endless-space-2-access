using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

// The game has its own MainMenuScreen; this file adapts it, so the two names have to coexist.
using GameMainMenu = MainMenuScreen;

namespace ES2Access.Screens
{
    /// <summary>
    /// The game's main menu, made navigable.
    ///
    /// Eight entries, three of which fly out into sub-entries on hover. Hover is the problem: the
    /// sub-entries exist in the hierarchy from the moment the menu binds, but the game only fades
    /// them in while the mouse is over their parent, so there is no keyboard route to them at all.
    /// Here they become the children of an expandable group, reached with Right and left again with
    /// Left, which is the shape a screen reader user already knows from every tree they use.
    ///
    /// Everything is read from the live window on each build - labels included - because the menu
    /// rebinds itself whenever the game's state changes underneath it (a save appearing turns "No
    /// saved game was found" into the save's name) and a cached label would keep announcing the old
    /// world.
    ///
    /// Entries the player cannot use stay in the list and announce that they are unavailable, rather
    /// than vanishing: "Join Game is there but Steam is not running" is information, and a menu that
    /// silently changes length between sessions is not navigable.
    ///
    /// Every entry's tooltip is spoken on focus. They are a sentence or two saying what the entry
    /// does - and, on an entry that is refusing, why - which is exactly what a player arriving at
    /// this menu needs to hear without having to ask for it. The review buffer still holds them for
    /// re-reading.
    /// </summary>
    public sealed class MainMenuScreen : Screen
    {
        public override string Key
        {
            get { return "screen.main-menu"; }
        }

        public override string ScreenName
        {
            get { return ModStrings.Get(ModStrings.ScreenMainMenu); }
        }

        /// <summary>The menu is the page everything else is reached from and returned to, so leaving
        /// it for the credits and coming back puts the player where they were rather than at the top
        /// of the list again.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>
        /// The menu is ours while it is fully shown and nothing is on top of it. Both covering cases
        /// are covered by the game's own state: a window that replaces the menu (Credits, the DLC
        /// browser) hides it, which drops IsReady, and a modal that floats over it is reported by the
        /// gui manager. IsReady additionally waits out the show animation, so nothing is announced
        /// while the menu is still fading in.
        /// </summary>
        public override bool IsActive()
        {
            GameMainMenu window = Window();
            if (window == null || !window.IsReady)
            {
                return false;
            }

            GuiManager gui = GuiService();
            return gui != null && !gui.IsAnyModalVisible;
        }

        public override void Build(GraphBuilder builder)
        {
            GameMainMenu window = Window();
            if (window == null || window.MainMenuItemsContainer == null)
            {
                return;
            }

            foreach (MainMenuItem item in Items(window))
            {
                string name = EntryName(item);
                if (name == null)
                {
                    continue;
                }

                MainMenuItem entry = item;
                ControlId id = ControlId.Referenced(item, "mainmenu:" + name);
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeText.Label(entry.TitleLabel),
                    () => Click(name),
                    () => Enabled(entry.AgeTransform),
                    entry.Tooltip,
                    GraphNodes.ModeFor(entry.Tooltip)
                );
                vtable.OnFocusVisual = () =>
                    PointerFocus.MoveTo(
                        entry.Button,
                        entry.Tooltip,
                        entry.TitleLabel.AgeTransform,
                        entry,
                        Flyout
                    );
                vtable.OnBlurVisual = ReleasePointer;

                List<MainMenuSubItem> subItems = SubItems(item);
                if (subItems.Count == 0)
                {
                    builder.AddItem(id, vtable);
                    continue;
                }

                // The entry is still a button - activating New Game starts a new game - and it also
                // opens onto its sub-entries, so it is declared as both.
                builder.BeginGroup(id, vtable);
                for (int i = 0; i < subItems.Count; i++)
                {
                    MainMenuSubItem sub = subItems[i];
                    string subName = SubEntryName(sub);
                    if (subName == null)
                    {
                        continue;
                    }

                    NodeVtable subVtable = GraphNodes.Button(
                        () => AgeText.Label(sub.TitleLabel),
                        () => Click(subName),
                        () => Enabled(sub.AgeTransform),
                        sub.Tooltip,
                        GraphNodes.ModeFor(sub.Tooltip)
                    );
                    // The flyout being open is the parent's business, so a step between sub-entries
                    // leaves it standing rather than closing and reopening it.
                    subVtable.OnFocusVisual = () =>
                        PointerFocus.MoveTo(
                            sub.Button,
                            sub.Tooltip,
                            sub.TitleLabel.AgeTransform,
                            entry,
                            Flyout
                        );
                    subVtable.OnBlurVisual = ReleasePointer;

                    builder.AddItem(
                        ControlId.Referenced(sub, "mainmenu:" + name + "/" + subName),
                        subVtable
                    );
                }

                builder.EndGroup();
            }
        }

        /// <summary>
        /// Open and close an entry's flyout the way hovering it does. The game sends itself these two
        /// messages from its own mouse-enter handler, and sends the show one to every entry when the
        /// player turns menu animation off - so this is the entry's own door, not a reimplementation
        /// of it. The hover handler itself is deliberately not called: it starts a coroutine that
        /// watches the real cursor and would close the flyout again on the next frame.
        ///
        /// Held in a field rather than made per entry: the menu rebuilds every frame.
        /// </summary>
        private static readonly Action<object, bool> Flyout = (owner, open) =>
        {
            MainMenuItem item = owner as MainMenuItem;
            if (item == null)
            {
                return;
            }

            item.SendMessage(
                open ? "ShowOrReshowSubMenu" : "HideOrRehideSubMenu",
                SendMessageOptions.DontRequireReceiver
            );
        };

        private static readonly Action ReleasePointer = PointerFocus.Release;

        /// <summary>
        /// Activate an entry exactly the way the mouse does: the game wires every menu button, top
        /// level and sub-entry alike, to a message named after the entry, sent to the screen. Naming
        /// the handler rather than driving the button means no hover animation has to be faked and no
        /// click can land on whatever the mouse happens to be over.
        /// </summary>
        private static void Click(string entryName)
        {
            GameMainMenu window = Window();
            if (window == null || !window.IsReady)
            {
                return;
            }

            try
            {
                window.gameObject.SendMessage(
                    "OnClick" + entryName,
                    SendMessageOptions.DontRequireReceiver
                );
            }
            catch (Exception e)
            {
                Log.Warn("main menu: activating " + entryName + " threw: " + e);
            }
        }

        // Alpha is 0 on a closed flyout and on the whole menu while it animates in, so the visible-only
        // filter would hide entries that are perfectly real; visibility is checked explicitly instead.
        private static List<MainMenuItem> Items(GameMainMenu window)
        {
            List<MainMenuItem> items = new List<MainMenuItem>();
            try
            {
                foreach (MainMenuItem item in window.MainMenuItemsContainer.GetChildren<MainMenuItem>(false))
                {
                    if (item != null && Visible(item.AgeTransform))
                    {
                        items.Add(item);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("main menu: reading the entries threw: " + e);
            }

            return items;
        }

        private static List<MainMenuSubItem> SubItems(MainMenuItem item)
        {
            List<MainMenuSubItem> subItems = new List<MainMenuSubItem>();
            try
            {
                if (item.SubItemsContainer == null)
                {
                    return subItems;
                }

                foreach (
                    MainMenuSubItem sub in item.SubItemsContainer.GetChildren<MainMenuSubItem>(false)
                )
                {
                    // The game hides sub-entries that do not apply this session (the tutorial prompt
                    // once you have played, a mod configuration that is already loaded).
                    if (sub != null && Visible(sub.AgeTransform))
                    {
                        subItems.Add(sub);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("main menu: reading the sub-entries threw: " + e);
            }

            return subItems;
        }

        private static string EntryName(MainMenuItem item)
        {
            try
            {
                return item.MainMenuEntry == null ? null : item.MainMenuEntry.Name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string SubEntryName(MainMenuSubItem sub)
        {
            try
            {
                return sub.MainMenuSubEntry == null ? null : sub.MainMenuSubEntry.Name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Visible(AgeTransform transform)
        {
            try
            {
                return transform != null && transform.Visible;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Enabled(AgeTransform transform)
        {
            try
            {
                return transform != null && transform.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static GameMainMenu Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GameMainMenu>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GuiManager GuiService()
        {
            try
            {
                return Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

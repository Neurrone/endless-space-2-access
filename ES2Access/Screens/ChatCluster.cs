using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The chat panel the game draws in the corner of every in-game page (<c>InGameChatPanel</c>) - not the
    /// typing, which is the game's own field and already announced (<see cref="ES2Access.UI.ChatField"/>),
    /// and not the messages, which are narrated and kept in a review buffer
    /// (<see cref="ES2Access.UI.SessionChat"/>), but the one part of it a keyboard could not reach at all:
    /// WHO THE NEXT MESSAGE GOES TO.
    ///
    /// The panel has two tabs, Global and Alliance, and they are not a filter - the tab that is showing
    /// decides the RECIPIENT of what is typed next (<c>ChatPanel.SetMessageOptionFromTab</c> stamps the
    /// message with <c>RecipientGlobal</c> or <c>RecipientAlliance</c>), and the only way to change it was to
    /// click one. Ctrl+Tab opens the field, the field is key-exclusive, and the mod stands down for it - so a
    /// blind player could type to the room and never to their alliance.
    ///
    /// THE DRAWING CANNOT BE THE GATE HERE, which is the one exception in the mod's own rule. The panel
    /// hides both tabs whenever it is discreet (<c>SetDiscreet</c>), and it is discreet exactly when the
    /// player is NOT typing - so the tabs are drawn only in the state where the keyboard belongs to the game
    /// and the mod cannot navigate. Following the drawing would mean the tabs stay unreachable forever. So a
    /// tab is declared on the game's OWN answer to whether it exists at all (<c>ChatTab.CanShowTab</c>: the
    /// alliance tab exists while the empire is in an alliance), and switching it does what a click does -
    /// the panel's <c>currentTab</c> survives the box closing and reopening, so choosing the recipient
    /// BEFORE pressing Ctrl+Tab is the whole gesture.
    ///
    /// Declared only in a multiplayer session, which is the same question the chat review buffer asks and
    /// for the same reason: in single player the game gives chat nothing to do, the alliance tab can never
    /// exist, and one remaining tab is not a choice. A stop that offered it anyway would be a stop the
    /// player walks past on every page to learn what a glance would have told them.
    ///
    /// Shared by every page rather than owned by one (<see cref="Screen.BuildShared"/>), for the reason the
    /// collapsed tutorial bar is: the game draws it over whatever the player is looking at, so it belongs to
    /// whatever page that is, and the stop key is shared so each page remembers its own cursor there.
    /// </summary>
    public static class ChatCluster
    {
        public static readonly object Stop = "chat:panel";

        /// <summary>The tabs and the new-message button, where the game is offering chat at all.</summary>
        public static void Build(GraphBuilder builder)
        {
            InGameChatPanel panel = Panel();
            if (panel == null)
            {
                return;
            }

            builder.BeginStop(Stop);
            builder.PushContext(ModStrings.Get(ModStrings.ChatPanel));
            builder.StartRow();
            Tabs(builder, panel);
            Notification(builder, panel);
            builder.EndRow();
            builder.PopContext();
        }

        /// <summary>One node per tab the game would show, in the order the bar draws them.</summary>
        private static void Tabs(GraphBuilder builder, InGameChatPanel panel)
        {
            try
            {
                ChatTab[] tabs = panel.ChatTabs;
                for (int i = 0; tabs != null && i < tabs.Length; i++)
                {
                    ChatTab tab = tabs[i];
                    if (tab == null || !tab.CanShowTab || tab.TabToggle == null)
                    {
                        continue;
                    }

                    ChatTab it = tab;
                    NodeVtable vtable = GraphNodes.Tab(
                        () => AgeText.Label(it.TabTitle),
                        () => it.TabToggle.State
                    );
                    // The mark the bar paints on a tab holding something the player has not seen. It is a
                    // bare picture, so these words are the mod's; nothing else on screen says it.
                    vtable.Announcements.Add(GraphNodes.ValuePart(() => Unread(it)));
                    vtable.OnActivate = () => AgeWidgets.Toggle(it.TabToggle);
                    // Keyed by position in the bar: the two tabs are separate objects, but a structural
                    // key is what identity is compared on, so a shared one would collide and empty the
                    // whole page.
                    builder.AddItem(ControlId.Referenced(tab, "chat:tab/" + i), vtable);
                }
            }
            catch (Exception e)
            {
                Log.Warn("chat: reading the chat tabs threw: " + e);
            }
        }

        /// <summary>The button the panel raises for a few seconds when a message arrives while chat is
        /// closed. Pressing it opens the box, which is what it does for a mouse (<c>OnNotificationCb</c>).
        /// Declared only while it is drawn: unlike the tabs, this one is on the screen exactly when the
        /// player can reach it.</summary>
        private static void Notification(GraphBuilder builder, InGameChatPanel panel)
        {
            try
            {
                AgeControlButton button = panel.NotificationButton;
                AgeTransform widget = AgeWidgets.Transform(button);
                if (!AgeWidgets.Visible(widget))
                {
                    return;
                }

                AgeControlButton it = button;
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                NodeVtable vtable = GraphNodes.Button(
                    CardActions.NameFromTooltip(tooltip),
                    () => AgeWidgets.Press(it),
                    () => AgeWidgets.Operable(widget),
                    tooltip,
                    TooltipMode.None
                );
                AgeWidgets.Point(vtable, it, tooltip, widget);
                builder.AddItem(ControlId.Referenced(button, "chat:new-messages"), vtable);
            }
            catch (Exception e)
            {
                Log.Warn("chat: reading the new-message button threw: " + e);
            }
        }

        private static string Unread(ChatTab tab)
        {
            try
            {
                return tab.NotificationImage != null && tab.NotificationImage.Visible
                    ? ModStrings.Get(ModStrings.ChatUnread)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The panel while the game is really drawing it, in a session that has chat.</summary>
        private static InGameChatPanel Panel()
        {
            try
            {
                if (!SessionChat.InMultiplayer())
                {
                    return null;
                }

                InGameChatWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<InGameChatWindow>(false)
                    : null;
                if (window == null || !window.Shown)
                {
                    return null;
                }

                InGameChatPanel panel = window.InGameChatPanel;
                return panel != null && AgeWidgets.Visible(panel.AgeTransform) ? panel : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

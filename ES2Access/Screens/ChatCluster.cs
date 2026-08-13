using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The whole chat panel the game draws in the corner of every in-game page
    /// (<c>InGameChatPanel</c>) as something a keyboard can work: who the next message goes to, what
    /// has been said, and the box it is typed into. The typing itself stays the game's - the field is
    /// key-exclusive, the mod stands down for it, and the hand-over is announced by
    /// <see cref="ES2Access.UI.ChatField"/>; the arriving lines stay
    /// <see cref="ES2Access.UI.SessionChat"/>'s to narrate.
    ///
    /// The panel has two tabs, Global and Alliance, and they are not a filter - the tab that is showing
    /// decides the RECIPIENT of what is typed next (<c>ChatPanel.SetMessageOptionFromTab</c> stamps the
    /// message with <c>RecipientGlobal</c> or <c>RecipientAlliance</c>), and the only way to change it was to
    /// click one. Ctrl+Tab opens the field, the field is key-exclusive, and the mod stands down for it - so a
    /// blind player could type to the room and never to their alliance.
    ///
    /// THE DRAWING CANNOT BE THE GATE HERE, which is the one exception in the mod's own rule. The panel
    /// hides its tabs, its lines and its field whenever it is discreet (<c>SetDiscreet</c> :127-180
    /// clears the field's <c>Enable</c> and its label's visibility along with them), and it is discreet
    /// exactly when the player is NOT typing - so all of it is drawn only in the state where the
    /// keyboard belongs to the game and the mod cannot navigate. Following the drawing would mean the
    /// panel stays unreachable forever. So each part is declared on the game's OWN answer to whether it
    /// exists: a tab on <c>ChatTab.CanShowTab</c> (the alliance tab exists while the empire is in an
    /// alliance), a message on the service's history, the box on the panel being there at all.
    /// Switching a tab does what a click does - the panel's <c>currentTab</c> survives the box closing
    /// and reopening, so choosing the recipient BEFORE opening it is the whole gesture.
    ///
    /// Declared wherever the GAME opens chat, single player included: the panel answers the chat key
    /// and posts messages in one (<see cref="ES2Access.UI.SessionChat.HasChat"/> records what was
    /// measured), so a solo player has a chat log to read whether or not anyone else can hear it. The
    /// alliance tab simply reports itself absent there, which is the game's own answer and not a rule
    /// of the mod's.
    ///
    /// Shared by every page rather than owned by one (<see cref="Screen.BuildShared"/>), for the reason the
    /// collapsed tutorial bar is: the game draws it over whatever the player is looking at, so it belongs to
    /// whatever page that is, and the stop key is shared so each page remembers its own cursor there.
    /// </summary>
    public static class ChatCluster
    {
        public static readonly object Stop = "chat:panel";

        /// <summary>How many of the newest messages are walkable - the panel's own limit
        /// (<c>ChatPanel.MaxHistory</c>, enforced in <c>AddLine</c>).</summary>
        private const int MaxLines = 50;

        /// <summary>The panel's controls, in the order it draws them down the corner: the tab bar, the
        /// lines that have been said, the box they are typed into.</summary>
        public static void Build(GraphBuilder builder)
        {
            InGameChatPanel panel = SessionChat.Panel();
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
            Messages(builder);
            Field(builder, panel);
            builder.PopContext();
        }

        /// <summary>
        /// What has been said, one node per message, oldest first - the log the panel draws and a
        /// keyboard could not read. The lines fade off the screen and the box scrolls, and neither
        /// gesture is a key, so before this the only way back to a message was to have heard it go by.
        ///
        /// Read from the service's own history rather than off the drawn lines, for the reason
        /// <see cref="SessionChat.History"/> gives: the panel pools and rebinds fifty widgets
        /// (<c>InGameChatPanel.AddLine</c> :231-282), so a widget walk reads recycled blanks.
        ///
        /// Bounded to the last fifty, which is not a number of the mod's: it is exactly what the panel
        /// itself keeps (<c>AddLine</c> drops the oldest past fifty), so the walk offers what the game
        /// would still be showing and nothing it has already thrown away. Everything older stays in the
        /// chat review buffer, which holds the session entire.
        ///
        /// A GROUP rather than fifty rows in the open, because this stop is now on every page of the
        /// game and every page rebuilds it every frame: collapsed, the log costs one node, and it costs
        /// fifty only while somebody is reading it (the cost gate in the graph engine's own rules).
        /// </summary>
        private static void Messages(GraphBuilder builder)
        {
            ReadOnlyCollection<ChatMessage> messages = SessionChat.Messages();
            int count = messages == null ? 0 : messages.Count;
            if (count == 0)
            {
                // Nothing said yet: the panel draws no lines either, and an expandable group with
                // nothing under it is a dead end rather than a heading.
                return;
            }

            ControlId id = ControlId.Structural("chat:messages");
            builder.BeginGroup(id, GraphNodes.Group(() => ModStrings.Get(ModStrings.ChatMessages)));
            try
            {
                if (!builder.IsExpanded(id))
                {
                    return;
                }

                for (int i = count > MaxLines ? count - MaxLines : 0; i < count; i++)
                {
                    ChatMessage message = messages[i];
                    if (message == null)
                    {
                        continue;
                    }

                    ChatMessage it = message;
                    NodeVtable vtable = new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => SessionChat.Line(it)),
                        },
                        // Nothing on screen to point at: the lines are hidden in every frame the mod
                        // can navigate in, and a stale hover elsewhere would be a lie.
                        OnFocusVisual = AgeWidgets.ReleasePointer,
                    };
                    // Keyed on its place in the history, which never changes, with the message riding
                    // along so the cursor keeps the line it is on even if the collection is rebuilt.
                    builder.AddItem(ControlId.Referenced(message, "chat:message/" + i), vtable);
                }
            }
            catch (Exception e)
            {
                Log.Warn("chat: reading the messages threw: " + e);
            }
            finally
            {
                // Whatever happened above, the group has to be closed or the page declares nothing.
                builder.EndGroup();
            }
        }

        /// <summary>
        /// The box the next message is typed in. Enter on it is the game's own <c>SetFocus</c> - what a
        /// click on the panel, on a tab, or on the new-message button all reach
        /// (<c>InGameChatPanel.OnNotificationCb</c>, <c>ChatTab.OnTabCb</c>) - and it is the same thing
        /// the chat key does, so the keyboard has a way into chat that does not depend on a chord the
        /// player may have rebound.
        ///
        /// Nothing is deferred and nothing is announced here, unlike every other of the mod's text
        /// boxes. The game hands the field the keyboard inside that one call with no gap state to sit
        /// in, the Enter that asked for it is not delivered to the field (the mod's own consumed-key
        /// latch, <see cref="ES2Access.UI.Input.GameKeyboardHandover"/>, suppresses exactly the
        /// transition frame), and what has just happened is announced by
        /// <see cref="ES2Access.UI.ChatField"/> in the words the chat key already uses - so arriving by
        /// key and arriving by node sound the same.
        ///
        /// It is never refusing, though the widget is disabled whenever the player is not typing: that
        /// flag says which state the panel is in, not whether chat can be opened.
        /// </summary>
        private static void Field(GraphBuilder builder, InGameChatPanel panel)
        {
            try
            {
                AgeControlTextField field = panel.ChatTextField;
                if (field == null)
                {
                    return;
                }

                AgeControlTextField box = field;
                InGameChatPanel it = panel;
                NodeVtable vtable = GraphNodes.EditField(
                    () => ModStrings.Get(ModStrings.ChatMessageBox),
                    () => TextFieldEditor.Typing(box) ? null : SettingRows.FieldText(box),
                    () => it.SetFocus()
                );
                vtable.OnFocusVisual = AgeWidgets.ReleasePointer;
                builder.AddItem(ControlId.Referenced(field, "chat:message-box"), vtable);
            }
            catch (Exception e)
            {
                Log.Warn("chat: reading the chat box threw: " + e);
            }
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
    }
}

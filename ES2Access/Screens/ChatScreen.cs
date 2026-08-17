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
    /// The chat panel the game draws in the corner of every in-game page (<c>InGameChatPanel</c>) as a
    /// page of its own: who the next message goes to, what has been said, and the box it is typed into.
    /// The typing itself stays the game's - the field is key-exclusive, the mod stands down for it, and
    /// the hand-over is announced by <see cref="ES2Access.UI.ChatField"/>; the arriving lines stay
    /// <see cref="ES2Access.UI.SessionChat"/>'s to narrate.
    ///
    /// A MOD-OWNED CHILD SCREEN (<see cref="Screen.PushChild"/>), not a stop on every page. Open chat is
    /// a place the player is IN: while the panel is up, Tab cycles within it and the page underneath is
    /// unreachable, exactly as the panel behaves for a mouse, which cannot click through it either. The
    /// covered parent keeps its own cursor, so closing chat puts the player back on the control they
    /// left with nothing remembered here (owner ruling, 2026-08-14). Before that ruling this was a stop
    /// contributed to every page, which put its controls in every page's Tab ring even while the game
    /// drew none of it.
    ///
    /// THE DRAWING IS THE GATE, like everywhere else in the mod. The panel hides its tabs, its lines and
    /// its field whenever it is discreet (<c>SetDiscreet</c> :127-180 clears the field's <c>Enable</c>
    /// and its label's visibility along with them), and it is discreet whenever nobody is typing in it
    /// and no pointer rests on it. What makes the drawing usable as a gate is
    /// <see cref="ES2Access.UI.ChatHold"/>, which holds the panel open for as long as this screen is up
    /// - what a pointer resting on the panel does for a mouse. So the way in is the game's own chat key,
    /// after which one Escape steps out of the box into this page and a second closes it.
    ///
    /// The panel has two tabs, Global and Alliance, and they are not a filter - the tab that is showing
    /// decides the RECIPIENT of what is typed next (<c>ChatPanel.SetMessageOptionFromTab</c> stamps the
    /// message with <c>RecipientGlobal</c> or <c>RecipientAlliance</c>), and the only way to change it
    /// was to click one. Within an open panel each part is declared on the game's OWN answer to whether
    /// it exists: a tab on <c>ChatTab.CanShowTab</c> (the alliance tab exists while the empire is in an
    /// alliance, and a tab that answers no is never shown - <c>ChatTab.OnBeginShow</c> asks the same
    /// question), a message on the service's history, the box on the panel being there at all.
    /// Switching a tab does what a click does - the panel's <c>currentTab</c> survives the box closing
    /// and reopening, so choosing the recipient BEFORE opening it is the whole gesture.
    ///
    /// Wherever the GAME opens chat, single player included: the panel answers the chat key and posts
    /// messages in one (<see cref="ES2Access.UI.SessionChat.HasChat"/> records what was measured), so a
    /// solo player has a chat log to read whether or not anyone else can hear it. The alliance tab
    /// simply reports itself absent there, which is the game's own answer and not a rule of the mod's.
    /// The LOBBY's chat panel is a different class with no discreet state of its own
    /// (<c>NewGameChatPanel</c>, read by <see cref="NewGameScreen"/>'s own chat row) and nothing here
    /// touches it.
    ///
    /// One thing stays on the pages: the new-message button (<see cref="BuildNewMessages"/>), which is
    /// the only part of the panel the game draws while chat is CLOSED.
    /// </summary>
    public sealed class ChatScreen : Screen
    {
        public static readonly object Stop = "chat:panel";

        /// <summary>The new-message button's stop, on the page rather than here - it is drawn exactly
        /// when this screen is not up.</summary>
        public static readonly object AlertStop = "chat:alert";

        /// <summary>The box's own key, so the step out of typing can land the cursor on it
        /// (<see cref="ES2Access.UI.ChatHold"/>) without rebuilding the id.</summary>
        public const string FieldKey = "chat:message-box";

        /// <summary>How many of the newest messages are walkable - the panel's own limit
        /// (<c>ChatPanel.MaxHistory</c>, enforced in <c>AddLine</c>).</summary>
        private const int MaxLines = 50;

        public override string Key
        {
            get { return "screen.chat"; }
        }

        /// <summary>Never asked: a child screen is PUSHED by whatever opened it and popped by whatever
        /// closes it, not polled by the manager. Answered honestly all the same, which is what the hold
        /// is - the panel is open because the mod is keeping it open.</summary>
        public override bool IsActive()
        {
            return ChatHold.Holding;
        }

        /// <summary>Nothing shared is added here. The page underneath keeps the collapsed tutorial bar
        /// and the new-message button; this screen is the chat panel and, deliberately, nothing else -
        /// which is the whole point of containing the player in it.</summary>
        public override bool AnswersOnly
        {
            get { return true; }
        }

        /// <summary>Silent on arrival, because arriving here is the game handing the keyboard to the
        /// box and <see cref="ES2Access.UI.ChatField"/> has already said so in the words the chat key
        /// uses. The context below names the panel again for anyone who steps out into it.</summary>
        public override string ScreenName
        {
            get { return null; }
        }

        /// <summary>A surface the mod put up and the game cannot close: Escape is ours, and must not
        /// also reach the pause menu behind the panel.</summary>
        public override bool ConsumesBack
        {
            get { return true; }
        }

        public override bool Back()
        {
            return ChatHold.Close();
        }

        /// <summary>Whatever took this screen away - the page under it closing, the mod unloading -
        /// the panel is let go with it, or the game keeps drawing a panel nobody can reach.</summary>
        public override void OnPop()
        {
            _editor.Cancel();
            ChatHold.ChildClosed();
        }

        /// <summary>The deferred hand-over of the keyboard to the box, and everything the edit itself
        /// says - shared with every other text box in the game (<see cref="TextFieldEditor"/>).
        /// </summary>
        private readonly TextFieldEditor _editor = new TextFieldEditor();

        public override void OnUpdate()
        {
            _editor.Update();
        }

        /// <summary>False while the box has been asked for and the keyboard has not changed hands yet:
        /// what the player types next belongs in the message, not in a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        /// <summary>The panel's controls, in the order it draws them down the corner: the tab bar, the
        /// lines that have been said, the box they are typed into.</summary>
        public override void Build(GraphBuilder builder)
        {
            InGameChatPanel panel = SessionChat.Panel();
            if (panel == null || !ChatHold.IsOpen(panel) || ChatHold.Typing(panel))
            {
                // Nothing to declare, and nothing said about it. Either the game has closed the panel
                // under us - the hold's own tick is about to take this screen away - or it holds the
                // keyboard for the box, where every key is the player's typing and there is nothing
                // here to walk. An empty render leaves the cursor unseated and silent, which is what
                // makes opening chat sound like one thing rather than a page arriving over it.
                return;
            }

            builder.BeginStop(Stop);
            builder.PushContext(ModStrings.Get(ModStrings.ChatPanel));
            builder.StartRow();
            Tabs(builder, panel);
            builder.EndRow();
            Messages(builder);
            Field(builder, panel);
            builder.PopContext();
        }

        /// <summary>
        /// The button the panel raises on the PAGE for a few seconds when a message arrives while chat
        /// is closed (<c>InGameChatPanel.AddLine</c> :275-281). Pressing it opens the box, which is what
        /// it does for a mouse (<c>OnNotificationCb</c>) - and opening the box is what brings this
        /// screen up.
        ///
        /// It stays on the pages because that is where the game draws it: it is visible exactly while
        /// the panel is discreet, which is exactly while this screen is not up. Declared on its own
        /// visibility, so the page carries it for the seconds it is there and not a frame longer.
        /// </summary>
        public static void BuildNewMessages(GraphBuilder builder)
        {
            try
            {
                InGameChatPanel panel = SessionChat.Panel();
                if (panel == null)
                {
                    return;
                }

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
                builder.BeginStop(AlertStop);
                builder.StartRow();
                builder.AddItem(ControlId.Referenced(button, "chat:new-messages"), vtable);
                builder.EndRow();
            }
            catch (Exception e)
            {
                Log.Warn("chat: reading the new-message button threw: " + e);
            }
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
        /// FLAT rows, not an expandable group, and the newest sits directly above the box: Up from the
        /// box reads the last thing said, Up again the one before it, exactly as the eye walks the
        /// column bottom-up (owner ruling 2026-08-14, reversing the collapsed group - a fold between
        /// the box and the last message was a step nobody asked for, and the rows only exist while the
        /// chat page itself does, so their cost is bounded by the visit).
        /// </summary>
        private static void Messages(GraphBuilder builder)
        {
            ReadOnlyCollection<ChatMessage> messages = SessionChat.Messages();
            int count = messages == null ? 0 : messages.Count;
            if (count == 0)
            {
                // Nothing said yet: the panel draws no lines either.
                return;
            }

            try
            {
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
                        // The drawn lines are a fading, scrolling column the mod does not track a
                        // widget for, so there is nothing here to point at.
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
        }

        /// <summary>
        /// The box the next message is typed in. Enter on it is the game's own <c>SetFocus</c> - what a
        /// click on the panel, on a tab, or on the new-message button all reach
        /// (<c>InGameChatPanel.OnNotificationCb</c>, <c>ChatTab.OnTabCb</c>) - and it is the same thing
        /// the chat key does, so the keyboard has a way back into typing that does not depend on a
        /// chord the player may have rebound.
        ///
        /// It goes through the shared editor like every other text box (<see cref="TextFieldEditor"/>),
        /// with three things of its own. Enter stays the GAME's: everywhere else the mod takes the
        /// commit key so that ending an edit does not also fire the screen's validate
        /// (<see cref="TextEditOptions.OwnCommit"/>), but here the validate is how a message is SENT
        /// and taking it would leave the box unable to do the one thing it is for. The hand-over is the
        /// panel's <c>SetFocus</c> rather than a bare
        /// change of the engine's focus, because that call also brings the panel out of its discreet
        /// state. And a COMMIT says nothing: Enter here SENDS, and the line coming back through the
        /// chat service is what announces it (<see cref="ES2Access.UI.SessionChat"/>) - "edited" on top
        /// of that would be the same event said twice. Escape is still a cancel, and a cancel still
        /// puts back what was in the box when the player entered it, which for a half-typed message
        /// means losing the draft (owner ruling).
        ///
        /// It is never refusing, though the widget is disabled whenever the player is not typing: that
        /// flag says which state the panel is in, not whether chat can be opened.
        /// </summary>
        private void Field(GraphBuilder builder, InGameChatPanel panel)
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
                ControlId row = ControlId.Referenced(field, FieldKey);
                TextFieldEditor editor = _editor;
                TextEditOptions how = new TextEditOptions
                {
                    HandOver = () => it.SetFocus(),
                    AnnounceCommit = false,
                    OwnCommit = false,
                };
                NodeVtable vtable = GraphNodes.EditField(
                    () => ModStrings.Get(ModStrings.ChatMessageBox),
                    () => TextFieldEditor.Typing(box) ? null : SettingRows.FieldText(box),
                    () => editor.Request(box, null, null, row, how)
                );
                vtable.OnFocusVisual = AgeWidgets.ReleasePointer;
                ControlId id = row;
                builder.AddItem(id, vtable);
                // The page opens ON the box - focus lands on it, not inside it (owner ruling
                // 2026-08-14): the box is what the player came for, the newest message is one Up
                // away, and typing is Enter.
                builder.SetStart(id);
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

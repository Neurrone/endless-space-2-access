using System;
using System.Reflection;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.Screens;

namespace ES2Access.UI
{
    /// <summary>
    /// The mod's hand on the in-game chat panel: what opens <see cref="Screens.ChatScreen"/> over the
    /// page, what keeps the panel drawn while a keyboard walks it, and what lets go.
    ///
    /// The panel has two states and only one of them is on screen. DISCREET is the resting one: tabs,
    /// lines, scrollbar and the box are hidden or switched off (<c>InGameChatPanel.SetDiscreet</c>
    /// :127-180), and the panel leaves it only while its field holds the engine's keyboard - which is
    /// exactly when the mod stands down. A mouse has a third state a keyboard has not: the pointer
    /// resting on the panel keeps it open after the field lets go (<c>OnTextFieldLoseFocusCb</c>
    /// :310-317 closes the panel only when the pointer is somewhere else). THIS CLASS IS THAT POINTER.
    ///
    /// Which is what lets the chat page follow the drawing like every other surface in the mod. It did
    /// not before: chat was declared as a stop on every page whenever the window existed, so its
    /// controls sat in every page's Tab ring while the game drew nothing at all, and the first Escape
    /// out of the box shut the panel before any of them could be reached (owner-reported, 2026-08-14).
    ///
    /// Three gestures. THE CHAT KEY opens the chat page with the cursor ON the box's node - not
    /// inside it; typing is Enter on the box, the edit-field idiom everywhere else in the mod
    /// (<see cref="OpenOnTheBox"/>, reached from the panel's own input handler so the game's
    /// hand-the-keyboard-over answer never runs for the key; a MOUSE opening the box - a click, the
    /// new-message button - still types at once, and <see cref="Tick"/> notices that edge and pushes
    /// the page under it, which is what contains them in chat for as long as it is open). The
    /// first ESCAPE while typing steps OUT rather than closing (<see cref="StepOutOfTyping"/>): the
    /// keyboard comes back, the panel is held open, and the cursor lands on the box's own node with the
    /// tabs and the log one arrow away. The second ESCAPE, from a node of that page, closes
    /// (<see cref="Close"/>) - and the covered page underneath still holds the cursor the player left
    /// on it, so nothing here has to remember where they came from.
    ///
    /// Whatever else takes the page away - the game closing the panel with its own chat key or an empty
    /// validate, the page underneath going, the mod unloading - comes back through
    /// <see cref="ChildClosed"/>, so the panel is let go exactly once however it ends.
    ///
    /// Static, like the panel it holds, and <see cref="Stop"/> lets go: a reload that left the hold on
    /// would leave the game drawing a panel nobody is reading.
    /// </summary>
    internal static class ChatHold
    {
        /// <summary>The panel the mod is keeping open, or null when it is holding nothing.</summary>
        private static InGameChatPanel _held;

        /// <summary>The chat page, made once and re-pushed: the manager drops its cursor when it pops
        /// (<c>KeepStateOnPop</c> is false), so every opening starts at the top by itself.</summary>
        private static ChatScreen _child;

        private static FieldInfo _discreet;
        private static MethodInfo _hovering;
        private static bool _reportedFailure;

        /// <summary>Whether the mod is holding the panel open - which is also what makes the chat page
        /// active (<see cref="Screens.ChatScreen.IsActive"/>).</summary>
        public static bool Holding
        {
            get { return _held != null; }
        }

        /// <summary>
        /// Whether the panel is drawing itself: tabs, lines and box on screen rather than discreet.
        /// The gate <see cref="Screens.ChatScreen"/> declares on.
        ///
        /// Read from the game's own <c>discreet</c> field rather than inferred from a widget, because
        /// that field is the one thing every part of the panel's visibility is derived from. The
        /// fallback is the box's own enabled flag, which <c>SetDiscreet</c> writes in lockstep - a
        /// panel that cannot be asked would otherwise leave the chat page permanently empty.
        /// </summary>
        public static bool IsOpen(InGameChatPanel panel)
        {
            if (panel == null)
            {
                return false;
            }

            try
            {
                if (_discreet == null)
                {
                    _discreet = typeof(InGameChatPanel).GetField(
                        "discreet",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    );
                }

                if (_discreet != null)
                {
                    return !(bool)_discreet.GetValue(panel);
                }

                return panel.ChatTextField != null && panel.ChatTextField.AgeTransform.Enable;
            }
            catch (Exception e)
            {
                Warn("chat: asking whether the panel is open threw: " + e);
                return false;
            }
        }

        /// <summary>Whether the game is holding the keyboard for this panel's box - the state in which
        /// every key belongs to the player's typing and the chat page declares nothing.</summary>
        public static bool Typing(InGameChatPanel panel)
        {
            AgeManager age = AgeManager.Instance;
            return panel != null
                && age != null
                && ReferenceEquals(age.FocusedControl, panel.ChatTextField);
        }

        /// <summary>
        /// Called from the pump, before the screens rebuild. Two edges to catch, and the game causes
        /// both: the box taking the keyboard (chat is open - put the player in it) and the panel going
        /// discreet under a page that is still up (the game's own chat key toggling it shut, or a
        /// validate on an empty box - take the page away).
        ///
        /// Everything else is somebody's <see cref="ChildClosed"/>.
        /// </summary>
        public static void Tick()
        {
            try
            {
                InGameChatPanel panel = SessionChat.Panel();
                if (_held == null)
                {
                    if (Typing(panel))
                    {
                        Open(panel);
                    }

                    return;
                }

                if (!IsOpen(_held) || !ReferenceEquals(panel, _held))
                {
                    // The panel is gone or shut. Its page goes with it, and ChildClosed - which the
                    // pop calls - finds nothing left to let go of.
                    ChatScreen child = _child;
                    _held = null;
                    if (child != null)
                    {
                        child.CloseSelf();
                    }
                }
            }
            catch (Exception e)
            {
                Warn("chat: minding the chat panel threw: " + e);
                _held = null;
            }
        }

        /// <summary>
        /// The chat key: open the panel and its page with the cursor ON the box - not inside it.
        ///
        /// The game's own answer to the key is <c>SetFocus</c>, which hands the box the keyboard in
        /// the same press - and the box is key-exclusive, so that one chord took every key away at
        /// once. Typing is entered the way every other text box in the mod is entered: Enter on the
        /// box's node (owner ruling 2026-08-14). False when there is no page to open over, which
        /// tells the caller to let the game act instead.
        /// </summary>
        public static bool OpenOnTheBox(InGameChatPanel panel)
        {
            if (panel == null)
            {
                return false;
            }

            try
            {
                Open(panel);
                // The landing on the box is the page's own start node (ChatScreen.Field declares
                // it), so a fresh open needs no cursor move from here.
                return _held != null;
            }
            catch (Exception e)
            {
                Warn("chat: opening on the box threw: " + e);
                return false;
            }
        }

        /// <summary>
        /// The player pressed Escape while the game's chat box had the keyboard. Take the keyboard
        /// back, keep the panel open, and put the cursor on the box's own node.
        ///
        /// The order matters and is the game's, not ours: dropping the engine's focus fires the panel's
        /// own lose-focus handler, which closes the panel because no pointer is over it. So the hold is
        /// re-asserted AFTER, and re-opening is the same call a returning mouse would cause.
        /// </summary>
        public static void StepOutOfTyping(InGameChatPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            try
            {
                AgeManager age = AgeManager.Instance;
                if (age != null)
                {
                    age.FocusedControl = null;
                }

                Open(panel);
                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null)
                {
                    navigator.FocusNode(ControlId.Structural(ChatScreen.FieldKey));
                }
            }
            catch (Exception e)
            {
                Warn("chat: stepping out of the chat box threw: " + e);
            }
        }

        /// <summary>
        /// Escape on the chat page: close the panel and take the page away, which hands the player back
        /// to the cursor the covered page still holds. False when the mod is holding nothing, which is
        /// what leaves the key to the game everywhere else.
        ///
        /// The panel is closed outright rather than left to the pointer test <see cref="ChildClosed"/>
        /// uses: this is the player asking for it to go, and a mouse resting on the panel is not an
        /// argument against a key that was pressed.
        /// </summary>
        public static bool Close()
        {
            InGameChatPanel panel = _held;
            if (panel == null)
            {
                return false;
            }

            _held = null;
            try
            {
                panel.SetDiscreet(true);
            }
            catch (Exception e)
            {
                Warn("chat: closing the panel threw: " + e);
            }

            ChatScreen child = _child;
            if (child != null)
            {
                child.CloseSelf();
            }

            return true;
        }

        /// <summary>The chat page has been popped, by whatever route. Let the panel go the way the game
        /// does when its own field loses the keyboard: it closes unless the pointer is resting on it,
        /// which is the game's own rule and not a copy of one. Does nothing when the caller has already
        /// let go, so every route through here lets go exactly once.</summary>
        public static void ChildClosed()
        {
            InGameChatPanel panel = _held;
            _held = null;
            if (panel == null)
            {
                return;
            }

            try
            {
                if (!Hovering(panel))
                {
                    panel.SetDiscreet(true);
                }
            }
            catch (Exception e)
            {
                Warn("chat: letting the panel go threw: " + e);
            }
        }

        /// <summary>Let the panel and its page go, whoever is left holding them - the mod is unloading,
        /// and a panel held open by an assembly nobody can reach would stay open for the rest of the
        /// session.</summary>
        public static void Stop()
        {
            ChatScreen child = _child;
            _child = null;
            if (child != null)
            {
                child.CloseSelf();
            }

            ChildClosed();
            _reportedFailure = false;
        }

        // Hold the panel open and put the chat page over whatever the player is on. Idempotent: the
        // chat key, the step out of typing and a re-entry all arrive here, and only the first of them
        // has anything to do.
        private static void Open(InGameChatPanel panel)
        {
            _held = panel;
            panel.SetDiscreet(false);
            if (_child != null && _child.ParentScreen != null)
            {
                return;
            }

            ScreenManager screens = ModEntry.Screens;
            Screen current = screens == null ? null : screens.Current;
            if (current == null)
            {
                // No page of ours to open over - the mod has nothing focused, and the game's chat
                // behaves as it always did until it has.
                _held = null;
                return;
            }

            if (_child == null)
            {
                _child = new ChatScreen();
            }

            current.PushChild(_child);
        }

        // The game's own "is the pointer on the panel", which is the whole of its rule for whether a
        // field losing the keyboard closes the panel. Private to it, so it is asked by reflection
        // rather than reimplemented - the test counts the scrollbar and the engine's rollover as well
        // as the panel's rect.
        private static bool Hovering(InGameChatPanel panel)
        {
            try
            {
                if (_hovering == null)
                {
                    _hovering = typeof(InGameChatPanel).GetMethod(
                        "IsHoveringThePanel",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    );
                }

                return _hovering != null && (bool)_hovering.Invoke(panel, null);
            }
            catch (Exception e)
            {
                Warn("chat: asking whether the pointer is on the panel threw: " + e);
                return false;
            }
        }

        private static void Warn(string message)
        {
            if (_reportedFailure)
            {
                return;
            }

            _reportedFailure = true;
            Log.Warn(message);
        }
    }
}

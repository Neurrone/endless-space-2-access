using System;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The one thing the game's chat boxes do not say: that they have taken the keyboard, and that they
    /// have given it back.
    ///
    /// Ctrl+Tab (<see cref="Input.GameChatKey"/>) raises the game's <c>StartChatting</c>, and whichever
    /// chat panel is up answers it by putting the engine's focus on its own text field - the in-game
    /// panel through <c>InGameChatPanel.SetFocus</c> :116-125, the lobby's inside its own
    /// <c>HandleInput</c> :25-33. Those fields are key-exclusive, so the whole mod layer stands down for
    /// them - correctly: the player is typing, and every letter, arrow and Backspace belongs to the
    /// field. But the game draws the change and says nothing, so without this the key is silent both
    /// ways: the player cannot tell whether chat opened, and cannot tell whether Escape got them out.
    ///
    /// Only those two edges are announced. What happens BETWEEN them is already covered: the screen
    /// reader echoes the typing, a sent line is narrated by <see cref="SessionChat"/> (the message comes
    /// back through the chat service, one's own included), and the whole log stays re-readable in the
    /// chat review buffer once the field lets go.
    ///
    /// Leaving is announced by re-reading the control the cursor is on rather than with a line of its
    /// own: the player pressed Escape to get back somewhere, and where they are is the useful answer.
    /// Escape is the game's own gesture either way, and this only reports it - the in-game field
    /// declares <c>StandardCancel = false</c> so the engine hands Exit down to the panel, which drops
    /// the focus itself (`InGameChatPanel.HandleInput` :108-112, with
    /// `ChatPanel.OnTextFieldKeyDownCb` :232-246 as the second route), while the lobby's field leaves
    /// <c>StandardCancel</c> on and the engine clears the focus for it
    /// (`InputManager.HandleInput` :1216-1227, measured).
    ///
    /// A CHAT PANEL'S OWN FIELD, not any key-exclusive control: the game has exactly two chat panels
    /// and one other <c>AgeControlTextFieldChat</c> - the alliance coordination request's pin field
    /// (measured) - and every other editor in the game belongs to a screen of the mod's that speaks for
    /// itself. The test is therefore the panel, asked once per change of focus rather than once a frame.
    /// </summary>
    internal sealed class ChatField
    {
        private AgeControl _decidedAbout;
        private bool _isChatField;
        private bool _typing;

        /// <summary>Called from the per-frame pump, after the screens have ticked so that re-reading the
        /// focused control reads the graph as it now is.</summary>
        public void Tick()
        {
            try
            {
                bool typing = TypingInChat();
                if (typing == _typing)
                {
                    return;
                }

                _typing = typing;
                if (typing)
                {
                    Voice.Say(ModStrings.Get(ModStrings.ChatTyping), true);
                    return;
                }

                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null)
                {
                    navigator.AnnounceCurrent();
                }
            }
            catch (Exception e)
            {
                Log.Warn("chat: watching the chat field threw: " + e);
                _decidedAbout = null;
                _isChatField = false;
            }
        }

        /// <summary>Forget what was decided. Nothing here is installed anywhere, so a reload has nothing
        /// to give back - but a stale "the player is typing" would make the next load's first Escape
        /// re-read a control nobody asked about.</summary>
        public void Stop()
        {
            _decidedAbout = null;
            _isChatField = false;
            _typing = false;
        }

        // The verdict is cached against the control it was made about: the focus changes rarely and the
        // question is asked every frame.
        private bool TypingInChat()
        {
            AgeManager age = AgeManager.Instance;
            AgeControl focused = age == null ? null : age.FocusedControl;
            if (ReferenceEquals(focused, _decidedAbout))
            {
                return _isChatField;
            }

            _decidedAbout = focused;
            _isChatField = IsAChatPanelsField(focused);
            return _isChatField;
        }

        private static bool IsAChatPanelsField(AgeControl focused)
        {
            if (focused == null || focused.transform.parent == null)
            {
                return false;
            }

            ChatPanel panel = focused.transform.parent.GetComponent<ChatPanel>();
            return panel != null && ReferenceEquals(panel.ChatTextField, focused);
        }
    }
}

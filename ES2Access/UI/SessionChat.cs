using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Buffers;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Everything said in a session, spoken - and in a multiplayer one that is the whole of the
    /// session's life, because the game posts every one of those events as a chat message.
    ///
    /// Somebody joining, leaving, being disconnected or kicked (<c>Session.OnLobbyChatUpdate</c>
    /// :591-649), the host changing (<c>OnLobbyOwnerChange</c> :651-666), a player renaming themselves
    /// (<c>RuntimeState_Lobby.TryChangeName</c> :827-846), each tick of the launch countdown
    /// (<c>SessionState_OpenedAndCounting.Countdown</c> :84-108) and the network clock synchronizing
    /// (<c>SessionState_Synchronizing</c> :22,28) are all <c>IChatClientService.PostMessage</c> calls
    /// with <c>ChatMessageOption.TypeSystem</c>, already localized and with the player's name
    /// substituted in. So ONE subscription to <c>OnChatMessageReceived</c> narrates lobby membership,
    /// the countdown, the clock and what the other players type, and no other part of the session has
    /// to be watched at all.
    ///
    /// The event fires from the game's own code, so the handler only ENQUEUES; <see cref="Tick"/>,
    /// which the per-frame pump calls, is what speaks. Lines are queued rather than interrupting: a
    /// message arriving is never more urgent than what the player just asked for.
    ///
    /// The same lines go into a review buffer of their own, which is the second sink review buffers
    /// are for - the announcement passes once, the log stays readable. The buffer is reachable
    /// wherever the GAME offers chat (Ctrl+Left/Right cycles visible buffers), which is
    /// <see cref="HasChat"/>: every multiplayer session, lobby included, and every running game
    /// whatever its session mode - the in-game panel answers the chat key and posts messages in single
    /// player too, and a message the player can send is one they must be able to re-read. Only the
    /// single-player LOBBY is left out, and by the game's own hand: it disables its chat box outright
    /// (<c>NewGameChatPanel.SessionService_SessionChange</c> :43-65). A hot reload loses the buffer's
    /// contents, so attaching seeds it from the service's own <c>ReadOnlyMessages</c> history rather
    /// than starting the player's log at the reload.
    ///
    /// Instance state throughout, and the subscription is given back in <see cref="Stop"/>, so a
    /// reload leaves nothing of this behind.
    /// </summary>
    internal sealed class SessionChat
    {
        /// <summary>The buffer's key, and its place in the cycle: registered after the UI buffer, so
        /// Ctrl+Right from a control's description lands here.</summary>
        public const string BufferKey = "chat";

        /// <summary>What the game has handed us since the last frame. Cleared every tick, so a message
        /// that arrives while the mod is being torn down is simply never spoken.</summary>
        private readonly List<ChatMessage> _pending = new List<ChatMessage>();

        private readonly ReviewBuffer _buffer;

        /// <summary>The service we are subscribed to. It outlives a session but not the game process,
        /// and comes back as a different instance if the manager is ever released - so it is compared,
        /// not assumed.</summary>
        private IChatClientService _service;

        private bool _live;

        public SessionChat(BufferController buffers)
        {
            _buffer = buffers == null
                ? null
                : buffers.Buffers.Register(
                    BufferKey,
                    () => ModStrings.Get(ModStrings.BufferChat),
                    true
                );
        }

        /// <summary>Speak whatever arrived since the last frame, and keep the log reachable exactly
        /// while there is a session to read one from.</summary>
        public void Tick()
        {
            IChatClientService service = Service();
            if (service != _service)
            {
                Attach(service);
            }

            bool live = HasChat();
            if (live != _live)
            {
                _live = live;
                if (_buffer != null)
                {
                    _buffer.Visible = live;
                    // A lobby OPENS in single player and is switched to multiplayer from inside it
                    // (measured: the switch renames the player, which is itself a system message), so
                    // by the time the log becomes reachable the session may already have said things.
                    // Seeding makes the log and the lobby's own chat row agree.
                    Seed();
                }
            }

            if (!live)
            {
                // Nowhere for the player to see or answer this: the menu, or the single-player lobby
                // whose chat box the game has switched off.
                _pending.Clear();
                return;
            }

            for (int i = 0; i < _pending.Count; i++)
            {
                string line = Line(_pending[i]);
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (_buffer != null)
                {
                    _buffer.AppendLine(line);
                }

                Voice.Say(line, false);
            }

            _pending.Clear();
        }

        /// <summary>Give the subscription back and forget the log - the mod is going away, and a
        /// handler left on the game's event would call into an assembly nobody can reach.</summary>
        public void Stop()
        {
            Attach(null);
            _live = false;
            if (_buffer != null)
            {
                _buffer.Visible = false;
                _buffer.Clear();
            }
        }

        /// <summary>Every line of this session so far, in the words the narration used - what the
        /// lobby's chat row offers for review (<see cref="Screens.NewGameScreen"/>). Read from the
        /// service's own history rather than off the drawn lines: the panel pools and reuses the fifty
        /// widgets it keeps (<c>ChatPanel.AddLine</c> :114-151), so a widget walk finds recycled
        /// blanks, and both surfaces have to say the same words.</summary>
        public static IList<string> History()
        {
            List<string> lines = new List<string>();
            try
            {
                ReadOnlyCollection<ChatMessage> messages = Messages();
                if (messages == null)
                {
                    return lines;
                }

                for (int i = 0; i < messages.Count; i++)
                {
                    string line = Line(messages[i]);
                    if (!string.IsNullOrEmpty(line))
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("chat: reading the history threw: " + e);
            }

            return lines;
        }

        /// <summary>The session's messages themselves, oldest first, or null where there is no chat
        /// service - the game's own collection, which is what both the drawn panel and this narration
        /// read. For a surface that needs the messages rather than the sentences
        /// (<see cref="Screens.ChatCluster"/> walks them one node each); the words a message is said in
        /// are <see cref="Line"/>'s, so every surface says the same thing.</summary>
        internal static ReadOnlyCollection<ChatMessage> Messages()
        {
            try
            {
                IChatClientService service = Service();
                return service == null ? null : service.ReadOnlyMessages;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The line said last, which is what a chat row reads out before its history is
        /// walked.</summary>
        public static string Latest()
        {
            IList<string> lines = History();
            return lines.Count == 0 ? null : lines[lines.Count - 1];
        }

        private void Attach(IChatClientService service)
        {
            if (_service != null)
            {
                try
                {
                    _service.OnChatMessageReceived -= Received;
                }
                catch (Exception e)
                {
                    Log.Warn("chat: unsubscribing threw: " + e);
                }
            }

            _service = service;
            _pending.Clear();
            if (_service == null)
            {
                if (_buffer != null)
                {
                    _buffer.Clear();
                }

                return;
            }

            try
            {
                _service.OnChatMessageReceived += Received;
            }
            catch (Exception e)
            {
                Log.Warn("chat: subscribing threw: " + e);
                _service = null;
                return;
            }

            Seed();
        }

        /// <summary>Refill the log from what the session has already said - the lobby a hot reload landed
        /// in the middle of, or one that had been talking before it became multiplayer. Recorded, never
        /// spoken: history is not news, and a reload must not read the last ten minutes back.</summary>
        private void Seed()
        {
            if (_buffer == null)
            {
                return;
            }

            _buffer.Clear();
            if (!_live)
            {
                return;
            }

            foreach (string line in History())
            {
                _buffer.AppendLine(line);
            }
        }

        /// <summary>The watcher itself. It runs inside the game's own event dispatch, alongside the
        /// chat panel's handler, so it records and returns: throwing here would rob the panel of the
        /// message, and speaking here would put speech outside the pump.</summary>
        private void Received(object sender, ChatMessageReceivedEventArgs e)
        {
            try
            {
                if (e != null && e.Message != null)
                {
                    _pending.Add(e.Message);
                }
            }
            catch (Exception exception)
            {
                Log.Warn("chat: recording a message threw: " + exception);
            }
        }

        /// <summary>
        /// One message as a sentence. A system message is its own text - the game writes those as
        /// complete lines ("* Alice has joined the lobby.", "Game will start in 5 seconds.") - and a
        /// player's message is said with their name in front of it, the way the panel draws it
        /// (<c>ChatLine.Refresh</c> :89-130 writes "[name] text").
        ///
        /// The scope the panel expresses as a COLOUR is said in words instead: a whisper and a message
        /// to one's alliance are not the same thing as talking to the room, and a screen-reader user
        /// has no colour to tell them apart.
        /// </summary>
        internal static string Line(ChatMessage message)
        {
            try
            {
                string text = AgeText.Clean(message.Text);
                if (string.IsNullOrEmpty(text))
                {
                    return null;
                }

                if ((message.Option & ChatMessageOption.TypeSystem) == ChatMessageOption.TypeSystem)
                {
                    return text;
                }

                string name = AgeText.Clean(message.PlayerName);
                if (string.IsNullOrEmpty(name))
                {
                    return text;
                }

                if (
                    (message.Option & ChatMessageOption.RecipientWhisper)
                    == ChatMessageOption.RecipientWhisper
                )
                {
                    return ModStrings.Format(ModStrings.ChatWhispered, name, text);
                }

                if (
                    (message.Option & ChatMessageOption.RecipientAlliance)
                    == ChatMessageOption.RecipientAlliance
                )
                {
                    return ModStrings.Format(ModStrings.ChatToAlliance, name, text);
                }

                return ModStrings.Format(ModStrings.ChatSaid, name, text);
            }
            catch (Exception e)
            {
                Log.Warn("chat: reading a message threw: " + e);
                return null;
            }
        }

        private static IChatClientService Service()
        {
            try
            {
                return Services.GetService<IChatClientService>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether the game is offering chat at all - the one question the review buffer and the chat
        /// panel's own controls (<see cref="Screens.ChatCluster"/>) both ask, so the two can never
        /// disagree about whether this session has chat.
        ///
        /// Two surfaces, so two answers ORed: any multiplayer session has the lobby's chat row, and any
        /// running game has the in-game panel - which the game shows whenever a game is ready, single
        /// player included (<c>GuiManager.UpdateGameWindowsVisibility</c> :1579-1580 passes a bare true
        /// into <c>SetGameWindowVisibility</c>, whose only condition is <c>GameReady</c>), answers the
        /// chat key in, and posts messages from (measured: a line sent in a single-player game comes
        /// back through <c>OnChatMessageReceived</c> with the player's own name on it).
        ///
        /// So the drawn panel is the gate rather than the session mode. What is left out is the single
        /// player LOBBY, where the game itself switches the box off.
        /// </summary>
        internal static bool HasChat()
        {
            return InMultiplayer() || Panel() != null;
        }

        /// <summary>The in-game chat panel while the game is really drawing it, or null. Asked by the
        /// gate above and by the cluster that declares the panel's controls.</summary>
        internal static InGameChatPanel Panel()
        {
            try
            {
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

        /// <summary>Whether there is a session other players can be in. The chat panel asks the same
        /// question the same way (<c>ChatPanel.SessionService_SessionChange</c> :248-262).</summary>
        internal static bool InMultiplayer()
        {
            try
            {
                Amplitude.Unity.Session.ISessionService service =
                    Services.GetService<Amplitude.Unity.Session.ISessionService>();
                return service != null
                    && service.Session != null
                    && service.Session.SessionMode != SessionMode.Single;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

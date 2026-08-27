using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Buffers;
using ES2Access.UI.Input;

namespace ES2Access.UI
{
    /// <summary>
    /// The player's end of the review buffers: the keys that walk them, and what each move sounds
    /// like.
    ///
    /// Three rules, all of them about not getting in the way of navigation:
    ///
    /// Review speech QUEUES. A navigation move interrupts, because the player asked for something
    /// newer than whatever is still being read; stepping through the description of the control they
    /// are already on is the opposite - they are reading, and cutting the reading off to read the next
    /// line of the same text would make a paragraph unreadable.
    ///
    /// A line step speaks the line and NOTHING ELSE - no label, no position, no "line 3 of 7". The
    /// buffer is text the player is reading, not a control they are inspecting.
    ///
    /// An edge CLAMPS SILENTLY: running off the end re-speaks the last line rather than announcing a
    /// boundary. Hearing the same line twice is itself the signal, and it costs no words.
    /// </summary>
    public sealed class BufferController
    {
        /// <summary>The always-present buffer: the focused control's full description, refilled by
        /// <see cref="GraphNavigator"/> whenever focus lands somewhere new.</summary>
        public const string UiKey = "ui";

        private readonly ReviewBufferManager _buffers = new ReviewBufferManager();

        public BufferController()
        {
            _buffers.Register(UiKey, () => ModStrings.Get(ModStrings.BufferUi));
        }

        /// <summary>The buffers themselves. Public so a screen can register its own log and POST /eval
        /// can read the lines back.</summary>
        public ReviewBufferManager Buffers
        {
            get { return _buffers; }
        }

        /// <summary>Refill the UI buffer with the focused control's description and put the player back
        /// in it - a new control is the newest thing to read, whatever log they were in.</summary>
        public void ReplaceUiLines(IEnumerable<string> lines)
        {
            _buffers.ReplaceLines(UiKey, lines);
            _buffers.SetCurrent(UiKey);
        }

        /// <summary>Run a buffer action by name; false means it was not one of ours, so the navigator
        /// gets its turn. The dev server calls this too, which is how buffer review is tested without
        /// a keyboard.</summary>
        public bool Dispatch(string actionKey)
        {
            switch (actionKey)
            {
                case BufferActions.LineUp:
                    _buffers.MovePreviousLine();
                    return SpeakLine();
                case BufferActions.LineDown:
                    _buffers.MoveNextLine();
                    return SpeakLine();
                case BufferActions.First:
                    _buffers.MoveFirstLine();
                    return SpeakLine();
                case BufferActions.Last:
                    _buffers.MoveLastLine();
                    return SpeakLine();
                case BufferActions.Prev:
                    _buffers.MoveBuffer(-1);
                    return SpeakBuffer();
                case BufferActions.Next:
                    _buffers.MoveBuffer(1);
                    return SpeakBuffer();
                default:
                    return false;
            }
        }

        // The move result is deliberately ignored: at either edge the cursor stayed put and the line
        // under it is re-spoken.
        private bool SpeakLine()
        {
            ReviewBuffer buffer = _buffers.Current;
            string line = buffer == null ? null : buffer.CurrentLine;
            Voice.Say(line ?? ModStrings.Get(ModStrings.BufferEmpty), false);
            return true;
        }

        // Switching names the buffer before reading it, so the player knows which stream they are in.
        private bool SpeakBuffer()
        {
            ReviewBuffer buffer = _buffers.Current;
            if (buffer == null)
            {
                return true;
            }

            string line = buffer.CurrentLine ?? ModStrings.Get(ModStrings.BufferEmpty);
            Voice.Say(ModStrings.Format(ModStrings.BufferLine, buffer.LabelText, line), false);
            return true;
        }
    }
}

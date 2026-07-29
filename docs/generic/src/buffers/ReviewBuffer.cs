using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI.Buffers
{
    /// <summary>What a line move ran into. A caller that clamps silently can ignore it; one that wants
    /// an edge tone or a spoken boundary has it.</summary>
    public enum ReviewBufferMove
    {
        Moved,
        BeginningOfBuffer,
        EndOfBuffer,
    }

    /// <summary>
    /// A reviewable stream of text with a cursor on it - the screen-reader idiom for "let me read that
    /// again, at my own pace". The focused control's full description, a log of notifications, a combat
    /// feed: each is a buffer, and the player walks its lines without the game having to hold still.
    ///
    /// Lines are stored trimmed, and blank ones are dropped on the way in, so a buffer never contains a
    /// line whose review would be silence.
    ///
    /// <see cref="FollowLatest"/> is the difference between the two kinds of buffer. A log follows: new
    /// lines arrive under the cursor, and the player reading the newest one keeps reading the newest
    /// one. A control's description does not: the cursor sits where the player put it, and only an
    /// explicit <see cref="ReplaceLines"/> moves it.
    ///
    /// Deliberately BCL-only, with no notion of speech: the buffer holds text and a position, and the
    /// adapter decides what that sounds like.
    /// </summary>
    public sealed class ReviewBuffer
    {
        private readonly List<string> _lines = new List<string>();
        private int _index;

        public ReviewBuffer(string key, Func<string> label, bool followLatest)
        {
            Key = key;
            Label = label;
            FollowLatest = followLatest;
        }

        /// <summary>The stable name the buffer is registered and addressed by.</summary>
        public string Key { get; private set; }

        /// <summary>The buffer's spoken name, resolved live so it follows the player's language.</summary>
        public Func<string> Label { get; private set; }

        /// <summary>Whether the player can currently reach this buffer; see
        /// <see cref="ReviewBufferManager.SetVisible"/>.</summary>
        public bool Visible { get; set; }

        /// <summary>An appended line moves the cursor onto itself - the log behaviour.</summary>
        public bool FollowLatest { get; private set; }

        /// <summary>The buffer's spoken name, falling back to its key so an unlabelled buffer is
        /// identifiable rather than nameless.</summary>
        public string LabelText
        {
            get
            {
                string text = Label == null ? null : Label();
                return string.IsNullOrEmpty(text) ? Key : text;
            }
        }

        public IList<string> Lines
        {
            get { return _lines; }
        }

        public int Count
        {
            get { return _lines.Count; }
        }

        /// <summary>Where the cursor is, clamped into the lines that exist.</summary>
        public int CurrentLineIndex
        {
            get
            {
                Clamp();
                return _index;
            }
        }

        /// <summary>The line under the cursor, or null when the buffer is empty.</summary>
        public string CurrentLine
        {
            get
            {
                Clamp();
                return _lines.Count == 0 ? null : _lines[_index];
            }
        }

        /// <summary>Refill the buffer and put the cursor back at the top - the control under the
        /// player's focus changed, so reviewing it starts from its name again.</summary>
        public void ReplaceLines(IEnumerable<string> lines)
        {
            _lines.Clear();
            _index = 0;
            if (lines == null)
            {
                return;
            }

            foreach (string line in lines)
            {
                Add(line);
            }
        }

        /// <summary>Add a line to the end. The cursor follows it only on a
        /// <see cref="FollowLatest"/> buffer.</summary>
        public void AppendLine(string line)
        {
            if (!Add(line))
            {
                return;
            }

            if (FollowLatest)
            {
                _index = _lines.Count - 1;
            }
        }

        public void Clear()
        {
            _lines.Clear();
            _index = 0;
        }

        public ReviewBufferMove MoveFirst()
        {
            _index = 0;
            return _lines.Count == 0 ? ReviewBufferMove.BeginningOfBuffer : ReviewBufferMove.Moved;
        }

        public ReviewBufferMove MoveLast()
        {
            if (_lines.Count == 0)
            {
                _index = 0;
                return ReviewBufferMove.EndOfBuffer;
            }

            _index = _lines.Count - 1;
            return ReviewBufferMove.Moved;
        }

        public ReviewBufferMove MovePrevious()
        {
            Clamp();
            if (_lines.Count == 0 || _index <= 0)
            {
                _index = 0;
                return ReviewBufferMove.BeginningOfBuffer;
            }

            _index--;
            return ReviewBufferMove.Moved;
        }

        public ReviewBufferMove MoveNext()
        {
            Clamp();
            if (_lines.Count == 0)
            {
                _index = 0;
                return ReviewBufferMove.EndOfBuffer;
            }

            if (_index >= _lines.Count - 1)
            {
                _index = _lines.Count - 1;
                return ReviewBufferMove.EndOfBuffer;
            }

            _index++;
            return ReviewBufferMove.Moved;
        }

        private bool Add(string line)
        {
            if (line == null)
            {
                return false;
            }

            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            _lines.Add(trimmed);
            return true;
        }

        private void Clamp()
        {
            if (_index < 0 || _lines.Count == 0)
            {
                _index = 0;
            }
            else if (_index >= _lines.Count)
            {
                _index = _lines.Count - 1;
            }
        }
    }
}

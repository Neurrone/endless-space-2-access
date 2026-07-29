using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI.Buffers
{
    /// <summary>
    /// The set of review buffers and which one the player is reading.
    ///
    /// Buffers are registered once, in the order the player cycles them; the first one registered is
    /// the UI buffer - the focused control's description - and is always reachable, because there has
    /// to be something to read even on a screen that has no log of its own. The rest come and go with
    /// context (<see cref="SetVisible"/>): the combat feed is worth cycling to during a battle and is
    /// noise everywhere else.
    ///
    /// Cycling only ever visits visible buffers and wraps, so a screen with one buffer answers the
    /// switch key by naming the buffer the player is already in rather than by doing nothing.
    ///
    /// Deliberately BCL-only; the adapter turns the results into speech.
    /// </summary>
    public sealed class ReviewBufferManager
    {
        private readonly List<ReviewBuffer> _order = new List<ReviewBuffer>();
        private readonly Dictionary<string, ReviewBuffer> _buffers =
            new Dictionary<string, ReviewBuffer>();

        private string _currentKey;

        /// <summary>Register a buffer. Registration order is cycle order, and the first buffer
        /// registered is the one that is always visible.</summary>
        public ReviewBuffer Register(string key, Func<string> label, bool followLatest = false)
        {
            ReviewBuffer existing;
            if (key == null || _buffers.TryGetValue(key, out existing))
            {
                return null;
            }

            ReviewBuffer buffer = new ReviewBuffer(key, label, followLatest);
            buffer.Visible = _order.Count == 0;
            _order.Add(buffer);
            _buffers.Add(key, buffer);
            if (_currentKey == null)
            {
                _currentKey = key;
            }

            return buffer;
        }

        public IList<ReviewBuffer> Buffers
        {
            get { return _order; }
        }

        /// <summary>The buffer the player is reading. Falls back to the first visible one if the
        /// current buffer has since been hidden.</summary>
        public ReviewBuffer Current
        {
            get
            {
                ReviewBuffer buffer;
                if (
                    _currentKey == null
                    || !_buffers.TryGetValue(_currentKey, out buffer)
                    || !buffer.Visible
                )
                {
                    buffer = FirstVisible();
                    _currentKey = buffer == null ? null : buffer.Key;
                }

                return buffer;
            }
        }

        public string CurrentKey
        {
            get
            {
                ReviewBuffer buffer = Current;
                return buffer == null ? null : buffer.Key;
            }
        }

        public ReviewBuffer Get(string key)
        {
            ReviewBuffer buffer;
            return key != null && _buffers.TryGetValue(key, out buffer) ? buffer : null;
        }

        /// <summary>Declare which buffers the player can currently reach. The first-registered buffer
        /// is visible whatever is passed; a current buffer that just went out of scope hands over to
        /// the first visible one.</summary>
        public void SetVisible(IEnumerable<string> keys)
        {
            HashSet<string> visible = new HashSet<string>();
            if (_order.Count > 0)
            {
                visible.Add(_order[0].Key);
            }

            if (keys != null)
            {
                foreach (string key in keys)
                {
                    if (key != null)
                    {
                        visible.Add(key);
                    }
                }
            }

            for (int i = 0; i < _order.Count; i++)
            {
                _order[i].Visible = visible.Contains(_order[i].Key);
            }
        }

        public void ReplaceLines(string key, IEnumerable<string> lines)
        {
            ReviewBuffer buffer = Get(key);
            if (buffer != null)
            {
                buffer.ReplaceLines(lines);
            }
        }

        public void AppendLine(string key, string line)
        {
            ReviewBuffer buffer = Get(key);
            if (buffer != null)
            {
                buffer.AppendLine(line);
            }
        }

        public void Clear(string key)
        {
            ReviewBuffer buffer = Get(key);
            if (buffer != null)
            {
                buffer.Clear();
            }
        }

        /// <summary>Put the player in a buffer without cycling to it (new content arriving in the UI
        /// buffer, say). A following buffer snaps to its newest line, since that is what the player
        /// asked to be shown.</summary>
        public ReviewBuffer SetCurrent(string key)
        {
            ReviewBuffer buffer = Get(key);
            if (buffer == null || !buffer.Visible)
            {
                return null;
            }

            if (buffer.FollowLatest)
            {
                buffer.MoveLast();
            }

            _currentKey = key;
            return buffer;
        }

        /// <summary>Step to the next (+1) or previous (-1) VISIBLE buffer, wrapping. With one visible
        /// buffer this lands back on it, which is the honest answer: that is where the player is.</summary>
        public ReviewBuffer MoveBuffer(int step)
        {
            if (_order.Count == 0)
            {
                return null;
            }

            int start = IndexOf(CurrentKey);
            if (start < 0)
            {
                start = 0;
            }

            int index = start;
            do
            {
                index += step < 0 ? -1 : 1;
                if (index < 0)
                {
                    index = _order.Count - 1;
                }
                else if (index >= _order.Count)
                {
                    index = 0;
                }

                ReviewBuffer buffer = _order[index];
                if (buffer.Visible)
                {
                    return SetCurrent(buffer.Key);
                }
            } while (index != start);

            return Current;
        }

        public ReviewBufferMove MoveFirstLine()
        {
            ReviewBuffer buffer = Current;
            return buffer == null ? ReviewBufferMove.BeginningOfBuffer : buffer.MoveFirst();
        }

        public ReviewBufferMove MoveLastLine()
        {
            ReviewBuffer buffer = Current;
            return buffer == null ? ReviewBufferMove.EndOfBuffer : buffer.MoveLast();
        }

        public ReviewBufferMove MovePreviousLine()
        {
            ReviewBuffer buffer = Current;
            return buffer == null ? ReviewBufferMove.BeginningOfBuffer : buffer.MovePrevious();
        }

        public ReviewBufferMove MoveNextLine()
        {
            ReviewBuffer buffer = Current;
            return buffer == null ? ReviewBufferMove.EndOfBuffer : buffer.MoveNext();
        }

        private ReviewBuffer FirstVisible()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                if (_order[i].Visible)
                {
                    return _order[i];
                }
            }

            return null;
        }

        private int IndexOf(string key)
        {
            for (int i = 0; i < _order.Count; i++)
            {
                if (_order[i].Key == key)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

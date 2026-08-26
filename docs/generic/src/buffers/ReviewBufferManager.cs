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
    /// Cycling only ever visits buffers that are visible AND have lines in them, and wraps, so a
    /// screen with one buffer worth reading answers the switch key by naming the buffer the player is
    /// already in rather than by doing nothing (<see cref="MoveBuffer"/>).
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

        /// <summary>
        /// Step to the next (+1) or previous (-1) buffer the player can read, wrapping.
        ///
        /// A buffer has to be visible AND have something in it. Visibility is about whether the buffer
        /// makes sense here; emptiness is about whether stopping on it would tell the player anything,
        /// and "Chat. Buffer empty" on the way round a single-player game is a stop that costs a
        /// keypress and says nothing (owner ruling 2026-08-19). Skipping it lands the cycle back on the
        /// buffer the player is in, which is the same answer a lone buffer has always given.
        ///
        /// The last resort is the buffer they are already in, so a player whose buffers are ALL empty
        /// is never left with nowhere to stand: they hear where they are and that it is empty, which is
        /// the truth.
        /// </summary>
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
                if (buffer.Visible && buffer.Count > 0)
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

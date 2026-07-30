using System.Collections.Generic;
using System.Threading;

namespace ES2Access.Dev
{
    /// <summary>
    /// Thread-safe ring buffer of everything the mod has spoken, so a developer or agent who
    /// cannot hear the screen reader can read it back over HTTP. Every entry carries a stable,
    /// monotonically increasing sequence number; callers poll with the highest sequence they
    /// have already seen and get only what is newer.
    ///
    /// Written from the Unity main thread (the speech pump) and read from HTTP handler threads. A
    /// reader can also BLOCK for the next line (<see cref="WaitForNewer"/>): the write side pulses
    /// the same lock it appends under, so a caller waiting on "what does this say next" is released
    /// on the frame it is said rather than on its next poll.
    /// </summary>
    internal sealed class SpeechLog
    {
        internal struct Entry
        {
            public long Seq;
            public string Text;
        }

        private const int Capacity = 1000;

        private readonly object _lock = new object();
        private readonly List<string> _texts = new List<string>();

        // Sequence number of _texts[0]. Sequences start at 1 so "since=0" means "everything".
        private long _firstSeq = 1;

        // Set when the mod is going away, so a waiter blocked for the next line is released rather
        // than left holding an HTTP thread against an object nobody will write to again.
        private bool _closed;

        public void Add(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            lock (_lock)
            {
                _texts.Add(text);
                if (_texts.Count > Capacity)
                {
                    _texts.RemoveAt(0);
                    _firstSeq++;
                }

                Monitor.PulseAll(_lock);
            }
        }

        /// <summary>The sequence of the newest line held - the cursor a caller passes as "since" to
        /// ask only about what happens after now.</summary>
        public long Cursor
        {
            get
            {
                lock (_lock)
                {
                    return _firstSeq + _texts.Count - 1;
                }
            }
        }

        /// <summary>
        /// Block up to <paramref name="timeoutMilliseconds"/> for a line newer than
        /// <paramref name="since"/>, returning true as soon as there is one (or at once if there
        /// already was). HTTP threads only - the main thread must never wait on the pump that feeds
        /// this.
        /// </summary>
        public bool WaitForNewer(long since, int timeoutMilliseconds)
        {
            lock (_lock)
            {
                if (Newer(since) || _closed)
                {
                    return Newer(since);
                }

                // One wait, not a loop: a spurious wake would only cost the caller the rest of its
                // budget, and Monitor.Wait here is only ever pulsed by an actual append or by close.
                Monitor.Wait(_lock, timeoutMilliseconds);
                return Newer(since);
            }
        }

        /// <summary>Release anyone waiting for the next line - the mod is unloading.</summary>
        public void Close()
        {
            lock (_lock)
            {
                _closed = true;
                Monitor.PulseAll(_lock);
            }
        }

        // Caller holds the lock.
        private bool Newer(long since)
        {
            return _firstSeq + _texts.Count - 1 > since;
        }

        /// <summary>
        /// Entries whose sequence is greater than <paramref name="since"/>, oldest first, empty
        /// when nothing is newer. <paramref name="next"/> is the cursor to pass next time; it
        /// advances even when entries were dropped by the ring buffer, so a slow poller resumes
        /// at the oldest line still held rather than replaying from the start.
        /// </summary>
        public List<Entry> Since(long since, out long next)
        {
            lock (_lock)
            {
                long end = _firstSeq + _texts.Count;
                long seq = since + 1;
                if (seq < _firstSeq)
                {
                    seq = _firstSeq;
                }

                List<Entry> entries = new List<Entry>();
                for (; seq < end; seq++)
                {
                    entries.Add(new Entry { Seq = seq, Text = _texts[(int)(seq - _firstSeq)] });
                }

                next = end - 1;
                if (next < since)
                {
                    next = since;
                }

                return entries;
            }
        }
    }
}

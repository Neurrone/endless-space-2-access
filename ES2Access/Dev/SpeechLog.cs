using System.Collections.Generic;

namespace ES2Access.Dev
{
    /// <summary>
    /// Thread-safe ring buffer of everything the mod has spoken, so a developer or agent who
    /// cannot hear the screen reader can read it back over HTTP. Every entry carries a stable,
    /// monotonically increasing sequence number; callers poll with the highest sequence they
    /// have already seen and get only what is newer.
    ///
    /// Written from the Unity main thread (the speech pump) and read from HTTP handler threads.
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
            }
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

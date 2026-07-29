using System;
using System.Collections.Generic;

namespace ES2Access.Loader.Dev
{
    /// <summary>
    /// Thread-safe ring of text lines with stable, monotonically increasing sequence numbers,
    /// behind the loader's cursor-polled feeds (GET /log, and the spoken lines POST /eval reports
    /// back). Callers poll with the highest sequence they have already seen and get only what is
    /// newer, so nothing is missed between polls and nothing is replayed.
    ///
    /// Written from whichever thread produced the line - the Unity main thread for speech, any
    /// thread at all for BepInEx log events - and read from HTTP handler threads.
    /// </summary>
    internal sealed class SeqLog
    {
        internal struct Entry
        {
            public long Seq;
            public string Text;
        }

        private readonly object _lock = new object();
        private readonly List<string> _texts = new List<string>();
        private readonly int _capacity;

        // Sequence number of _texts[0]. Sequences start at 1 so "since=0" means "everything".
        private long _firstSeq = 1;

        public SeqLog(int capacity)
        {
            _capacity = capacity;
        }

        /// <summary>The newest sequence number held, or 0 when nothing has been written. Taking
        /// this before an action and passing it as <c>since</c> afterwards reports exactly what
        /// that action produced.</summary>
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

        public void Add(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            lock (_lock)
            {
                _texts.Add(text);
                if (_texts.Count > _capacity)
                {
                    _texts.RemoveAt(0);
                    _firstSeq++;
                }
            }
        }

        /// <summary>
        /// Entries whose sequence is greater than <paramref name="since"/>, oldest first, empty
        /// when nothing is newer. <paramref name="next"/> is the cursor to pass next time; it
        /// advances even when entries were dropped by the ring buffer, so a slow poller resumes at
        /// the oldest line still held rather than replaying from the start.
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

        /// <summary>Drop entries whose text does not contain <paramref name="needle"/>, ignoring
        /// case. Filtering after <see cref="Since"/> rather than inside it keeps the cursor
        /// counting every line, so a filtered poll still advances past what it hid.</summary>
        public static List<Entry> Matching(List<Entry> entries, string needle)
        {
            if (string.IsNullOrEmpty(needle))
            {
                return entries;
            }

            List<Entry> matched = new List<Entry>();
            foreach (Entry entry in entries)
            {
                if (entry.Text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matched.Add(entry);
                }
            }

            return matched;
        }
    }
}

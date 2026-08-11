using System.Collections.Generic;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// Things the game does in a FLURRY, reported as one line: the ships a salvo destroys, the troops
    /// a round kills.
    ///
    /// A cinematic is not a sequence of steps the player takes, it is a stream the game plays, and the
    /// two rules that make such a stream listenable are both easy to get wrong and neither shows up in
    /// a dump.
    ///
    /// The first is the WATERMARK, and it is PER RUN rather than per thing. What is announced is a
    /// change since the last utterance - a ship that has gone from alive to destroyed - and the game
    /// replays the very same stream against a reset model when the player asks to watch the battle
    /// again. A watermark that survived that replay would leave the whole second showing silent, so a
    /// run that starts over calls <see cref="Reset"/> and everything is news again.
    ///
    /// The second is the BURST. Six ships die inside a second, one per frame, and a watcher that spoke
    /// each as it noticed it queues six utterances the player then has to listen through - by which
    /// time the battle has moved on. So a noticed thing OPENS a window, everything noticed inside that
    /// window joins it, and the whole burst is offered once at the end as the one thing it was: "four
    /// of your ships lost". The window is short enough that the report still lands while it is true.
    ///
    /// Engine-free, so both rules are tested off-game; the caller passes the clock in, which is also
    /// what lets a test step time by hand.
    /// </summary>
    public sealed class BurstWatch
    {
        private readonly Dictionary<string, bool> _told = new Dictionary<string, bool>();
        private readonly List<string> _pending = new List<string>();
        private readonly float _window;
        private float _opened;
        private bool _open;

        /// <summary><paramref name="window"/> is how long a burst gathers before it is offered, in
        /// seconds.</summary>
        public BurstWatch(float window)
        {
            _window = window;
        }

        /// <summary>Nothing has been announced and nothing is gathering - a fresh run of the same
        /// stream, where every one of these things is about to happen again.</summary>
        public void Reset()
        {
            _told.Clear();
            _pending.Clear();
            _open = false;
        }

        /// <summary>
        /// Something has happened that the player has not been told about.
        ///
        /// <paramref name="id"/> identifies the thing for the length of the run - the same ship
        /// noticed on the next frame is the same event, not a second one. <paramref name="name"/> is
        /// what to call it if it turns out to be the only thing in its burst.
        /// </summary>
        public void Note(string id, string name, float now)
        {
            if (string.IsNullOrEmpty(id) || _told.ContainsKey(id))
            {
                return;
            }

            _told[id] = true;
            _pending.Add(name);
            if (!_open)
            {
                _open = true;
                _opened = now;
            }
        }

        /// <summary>Whether a burst is gathering - so a caller can hold off on the quieter things it
        /// would otherwise say over the top of it.</summary>
        public bool Gathering
        {
            get { return _open; }
        }

        /// <summary>
        /// The burst, once it has had its window to fill up: what was noticed, oldest first. Null
        /// while nothing is gathering or the window is still open, so the caller says nothing.
        ///
        /// Taking it closes the burst: the next thing noticed opens a fresh one.
        /// </summary>
        public IList<string> Due(float now)
        {
            if (!_open || now - _opened < _window)
            {
                return null;
            }

            _open = false;
            if (_pending.Count == 0)
            {
                return null;
            }

            List<string> burst = new List<string>(_pending);
            _pending.Clear();
            return burst;
        }
    }
}

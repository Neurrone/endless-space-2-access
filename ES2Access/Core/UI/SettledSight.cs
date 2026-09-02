using System.Collections.Generic;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// Whether something was on the map long enough to have been SEEN.
    ///
    /// A partial-information game recomputes visibility every time anything moves, and the client is
    /// handed the result in batches: a thing crossing the edge of the player's sight can rise into
    /// it and fall out of it again inside a second, or inside a single frame, with nothing ever
    /// drawn on the screen for either step. A watcher that spoke each write would tell the player
    /// about ships a sighted player never saw, and would then tell them the ship was lost - two
    /// pieces of news about an event that did not happen.
    ///
    /// So a crossing is a CANDIDATE, not news. It commits only if nothing crosses back for the whole
    /// settle window; a reverse crossing inside the window cancels it silently and leaves the thing
    /// exactly where it stood before - a flash into sight is no sighting, and a flicker out of it is
    /// no loss. Only a committed crossing is news, and both directions are reported the same way, so
    /// a caller can never announce a loss for something it never announced a sighting of.
    ///
    /// The window is deliberately longer than the batching cadence rather than tuned to it: the
    /// question is what the PLAYER could have seen, not what the wire carried.
    ///
    /// Keys are the caller's - a game entity's id - and the clock is passed in, so all of it is
    /// engine-free and tested off-game.
    /// </summary>
    public sealed class SettledSight
    {
        /// <summary>One crossing that held: <see cref="InSight"/> true is a sighting, false a loss.
        /// </summary>
        public struct Change
        {
            public ulong Key;

            public bool InSight;
        }

        private sealed class Watched
        {
            /// <summary>Which side of the boundary this key has SETTLED on - what the caller has
            /// already been told, and what a candidate is measured against.</summary>
            public bool InSight;

            /// <summary>A crossing waiting out its window. Its direction is always the opposite of
            /// <see cref="InSight"/>: a step that changes nothing is never a candidate.</summary>
            public bool Pending;

            public float Since;
        }

        private readonly Dictionary<ulong, Watched> _watched = new Dictionary<ulong, Watched>();
        private readonly float _window;

        /// <summary><paramref name="window"/> is how long a crossing must hold before it is news, in
        /// seconds.</summary>
        public SettledSight(float window)
        {
            _window = window;
        }

        /// <summary>Nothing has been seen and nothing is waiting - a different galaxy, which is what
        /// a new game, a loaded save or a reload each is.</summary>
        public void Reset()
        {
            _watched.Clear();
        }

        /// <summary>How many things are settled in sight - what a probe reads to see that a caller's
        /// own table and this one still agree.</summary>
        public int InSightCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<ulong, Watched> pair in _watched)
                {
                    if (pair.Value.InSight)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>How many crossings are waiting out their window right now.</summary>
        public int PendingCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<ulong, Watched> pair in _watched)
                {
                    if (pair.Value.Pending)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Whether this key is settled in sight - the caller's test for "I have already told
        /// the player about this one".</summary>
        public bool InSightNow(ulong key)
        {
            Watched watched;
            return _watched.TryGetValue(key, out watched) && watched.InSight;
        }

        /// <summary>Whether a crossing for this key is waiting out its window - what a caller asks
        /// before adopting something silently, so an adoption never overtakes news that is one
        /// moment away from being told.</summary>
        public bool Settling(ulong key)
        {
            Watched watched;
            return _watched.TryGetValue(key, out watched) && watched.Pending;
        }

        /// <summary>Start this key off IN SIGHT with nothing to say about it - the baseline every
        /// caller takes when it arrives in a galaxy already half-explored. A thing held this way is
        /// never announced as a sighting, and its going out of sight later is a real loss.</summary>
        public void Hold(ulong key)
        {
            _watched[key] = new Watched { InSight = true };
        }

        /// <summary>Forget this key entirely: a thing that no longer exists crosses nothing, and a
        /// candidate standing for it must not commit.</summary>
        public void Forget(ulong key)
        {
            _watched.Remove(key);
        }

        /// <summary>
        /// One observed crossing, at <paramref name="now"/>.
        ///
        /// A step onto the side this key is already settled on is not a crossing and is ignored - the
        /// caller may hand over every write it sees. A repeat of a candidate's own direction does not
        /// restart its clock: the window is measured from the FIRST crossing, which is the moment the
        /// thing appeared or vanished as far as the player is concerned. A crossing the other way
        /// cancels the candidate outright, and nothing at all is reported for either step.
        /// </summary>
        public void Note(ulong key, bool inSight, float now)
        {
            Watched watched;
            if (!_watched.TryGetValue(key, out watched))
            {
                watched = new Watched();
                _watched[key] = watched;
            }

            if (watched.Pending)
            {
                if (inSight != watched.InSight)
                {
                    return;
                }

                // Back where it started inside its own window: never drawn long enough to be seen,
                // so there is nothing to say and nothing to take back.
                watched.Pending = false;
                return;
            }

            if (inSight == watched.InSight)
            {
                return;
            }

            watched.Pending = true;
            watched.Since = now;
        }

        /// <summary>
        /// The crossings that have held for the whole window, and the moment they become the caller's
        /// news: each is settled here before it is returned, so a caller that drops one on the floor
        /// is never offered it again. Null while nothing is due.
        /// </summary>
        public IList<Change> Due(float now)
        {
            List<Change> due = null;
            foreach (KeyValuePair<ulong, Watched> pair in _watched)
            {
                Watched watched = pair.Value;
                if (!watched.Pending || now - watched.Since < _window)
                {
                    continue;
                }

                watched.Pending = false;
                watched.InSight = !watched.InSight;
                if (due == null)
                {
                    due = new List<Change>(2);
                }

                due.Add(new Change { Key = pair.Key, InSight = watched.InSight });
            }

            return due;
        }
    }
}

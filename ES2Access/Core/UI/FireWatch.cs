using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI
{
    /// <summary>Which of the two kinds of gun a shot came out of - the same split the battle's own
    /// power arcs are drawn from. <see cref="Unknown"/> is the honest answer for a weapon that claims
    /// both or neither, and it costs the line its type word rather than inventing one.</summary>
    public enum DamageKind
    {
        Unknown,
        Energy,
        Projectile,
    }

    /// <summary>
    /// The EXCHANGE OF FIRE, gathered into something a person can listen to.
    ///
    /// A battle's report is one instruction per weapon shot - prepare, launch, hit - and a watcher
    /// that spoke each of them would say several hundred lines over a fight that lasts a minute. What
    /// a sighted player actually takes off the screen in that minute is coarser and slower: who is
    /// shooting at whom, whether it is landing, and how hard. So shots are gathered for a window and
    /// then reported per ATTACKER-TARGET PAIR - one line per pair per window, however many shots went
    /// into it.
    ///
    /// The window is much longer than <see cref="BurstWatch"/>'s, and deliberately: a death is urgent
    /// and a volley is not. Losses stay on the short window so they still land while the wreckage is
    /// on screen; fire is a running commentary and reads better in fewer, fuller sentences.
    ///
    /// A pair's tallies are kept apart by DAMAGE KIND rather than summed, because the two kinds are
    /// what the game's own arcs distinguish and "40 energy damage" says something "40 damage" does
    /// not. Misses are counted, never named - a miss has nothing to say but that it happened. Shield
    /// absorption is carried alongside the damage that got THROUGH, because those are two different
    /// numbers and the game's own damage gauge reports them as two.
    ///
    /// Groups come back LOUDEST FIRST so a caller that can only afford so many lines drops the
    /// quietest exchanges rather than the last-arriving ones.
    ///
    /// Engine-free, so the windowing and the grouping are tested off-game; the caller passes the
    /// clock in.
    /// </summary>
    public sealed class FireWatch
    {
        /// <summary>Everything one attacker did to one target inside one window.</summary>
        public sealed class Volley
        {
            public string Attacker;

            public string Target;

            /// <summary>How many shots landed. Zero with <see cref="Misses"/> above zero is a pair
            /// that fired and hit nothing.</summary>
            public int Hits;

            public int Misses;

            /// <summary>Damage that got THROUGH, per kind - post-mitigation, which is what the
            /// report's own hit instruction carries.</summary>
            public float Energy;

            public float Projectile;

            /// <summary>Damage from a weapon that answered neither kind, kept apart so the line can
            /// drop the type word instead of guessing one.</summary>
            public float Untyped;

            /// <summary>What the target's shields ate on top of what got through.</summary>
            public float Absorbed;

            public float Damage
            {
                get { return Energy + Projectile + Untyped; }
            }
        }

        /// <summary>What joins the two names into one dictionary key, so that "AB" firing at "C" and
        /// "A" firing at "BC" stay two different exchanges rather than one.</summary>
        private const string Separator = "|";

        private readonly Dictionary<string, Volley> _byPair = new Dictionary<string, Volley>();
        private readonly List<Volley> _order = new List<Volley>();
        private readonly float _window;
        private float _opened;
        private bool _open;

        /// <summary><paramref name="window"/> is how long fire gathers before it is offered, in
        /// seconds.</summary>
        public FireWatch(float window)
        {
            _window = window;
        }

        /// <summary>Nothing is gathering and nothing has been said - a fresh run of the same stream,
        /// where the whole fight is about to happen again.</summary>
        public void Reset()
        {
            _byPair.Clear();
            _order.Clear();
            _open = false;
        }

        /// <summary>Whether fire is gathering, so a caller can hold quieter lines back.</summary>
        public bool Gathering
        {
            get { return _open; }
        }

        /// <summary>
        /// One shot, from <paramref name="attacker"/> at <paramref name="target"/>.
        ///
        /// A shot with no attacker or no target is dropped: the line is built around the two names
        /// and there is nothing to say without them.
        /// </summary>
        public void Note(
            string attacker,
            string target,
            bool hit,
            float damage,
            float absorbed,
            DamageKind kind,
            float now
        )
        {
            if (string.IsNullOrEmpty(attacker) || string.IsNullOrEmpty(target))
            {
                return;
            }

            string pair = attacker + Separator + target;
            Volley volley;
            if (!_byPair.TryGetValue(pair, out volley))
            {
                volley = new Volley { Attacker = attacker, Target = target };
                _byPair[pair] = volley;
                _order.Add(volley);
            }

            if (!_open)
            {
                _open = true;
                _opened = now;
            }

            if (!hit)
            {
                volley.Misses++;
                return;
            }

            volley.Hits++;
            if (absorbed > 0f)
            {
                volley.Absorbed += absorbed;
            }

            if (damage <= 0f)
            {
                return;
            }

            switch (kind)
            {
                case DamageKind.Energy:
                    volley.Energy += damage;
                    break;
                case DamageKind.Projectile:
                    volley.Projectile += damage;
                    break;
                default:
                    volley.Untyped += damage;
                    break;
            }
        }

        /// <summary>
        /// The window's exchanges once it has had its time to fill up, loudest first. Null while
        /// nothing is gathering or the window is still open, so the caller says nothing.
        ///
        /// Taking them closes the window: the next shot opens a fresh one.
        /// </summary>
        public IList<Volley> Due(float now)
        {
            if (!_open || now - _opened < _window)
            {
                return null;
            }

            _open = false;
            if (_order.Count == 0)
            {
                return null;
            }

            List<Volley> due = new List<Volley>(_order);
            _byPair.Clear();
            _order.Clear();
            due.Sort(Loudest);
            return due;
        }

        private static int Loudest(Volley a, Volley b)
        {
            int byDamage = b.Damage.CompareTo(a.Damage);
            if (byDamage != 0)
            {
                return byDamage;
            }

            return (b.Hits + b.Misses).CompareTo(a.Hits + a.Misses);
        }
    }
}

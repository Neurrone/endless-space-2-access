using System.Collections.Generic;
using ES2Access.Core.Util;

namespace ES2Access.ES2.UI
{
    /// <summary>
    /// THE ONE-KEY WALK of a custom category: a flat list of everything the category caught, taken
    /// nearest first from where the player is reading, one press one step.
    ///
    /// It is a different gesture from the scanner's three paging tiers and needs its own rule for
    /// exactly one reason: a single key has no scope axis. The paging keys let the player choose a
    /// column and then step it; the quick key IS the column ("all") and the step at once, so what it
    /// walks is the whole category flattened, and everything it needs to remember between presses is
    /// here.
    ///
    /// A SWEEP KEEPS THE LIST IT STARTED WITH. The order is taken nearest-first from where the
    /// player was when the sweep began, and then it is FROZEN: press after press walks 1, 2, 3 … n
    /// and wraps. It has to be frozen, because every landing moves the player and re-sorting from
    /// the new place turns the key into a nearest-neighbour hop that circles a handful of entries
    /// and can never reach the far side of the map (reported 2026-08-24: a 46-entry category that
    /// never got past 6). What the player asked for is a list, and a list is walked in one order.
    ///
    /// THE SWEEP RE-ANCHORS WHEN THE PLAYER MOVES - and only then. The place it compares against is
    /// where the walk's OWN last landing is taking the player, never where they were before it: the
    /// landing is the walk moving them, and reading that as "the player moved" is what restarted the
    /// sweep on every press. A press from where the last landing left off continues; a press from
    /// anywhere else - an arrow, Home, a jump - starts a new sweep at the nearest thing. The place is
    /// the rounded pair the player is TOLD, so two things the map reads out at the same coordinates
    /// are one place here, exactly as they are when the walk asks whether the player is parked on
    /// what it last landed on.
    ///
    /// AND A PRESS WHILE PARKED ON THE CURRENT ENTRY STEPS ON. Landing on a thing makes that thing
    /// the nearest thing, so a restarted sweep would otherwise land on it again and the key would
    /// look stuck. The press asked to move: it moves. That is what turns the first press after a
    /// player move into a step rather than a way of re-reading where they stand.
    ///
    /// Engine-free, because every failure here is inaudible: a walk that re-lands, one that stops at
    /// the end of the list, and one that steps from an entry that has since vanished all sound like
    /// a galaxy with less in it than the player thought.
    /// </summary>
    public sealed class ScannerWalk
    {
        /// <summary>Whether this press continues the sweep the last one was in: the same category,
        /// from where that press left the player. Asked before the press is acted on, and it records
        /// nothing - the press may yet find nothing to land on.</summary>
        public bool Sweeping(int category, double east, double north)
        {
            return _anchored && _category == category && _east == east && _north == north;
        }

        /// <summary>The order the sweep is walking, or null where there is no sweep to continue.
        /// </summary>
        public IList<string> Sweep
        {
            get { return _order; }
        }

        /// <summary>Where this press is taking the player and in what order it was walking, so the
        /// next one can tell whether the player has moved since and carry on down the same list. The
        /// position is the LANDING's, not the place the press was made from, which is the whole of
        /// what makes the sweep continue.</summary>
        public void Anchor(int category, double east, double north, IList<string> order)
        {
            _anchored = true;
            _category = category;
            _east = east;
            _north = north;
            _order = Copy(order);
        }

        /// <summary>Forget the sweep - the mod being torn down, or another galaxy.</summary>
        public void Forget()
        {
            _anchored = false;
            _category = -1;
            _east = 0.0;
            _north = 0.0;
            _order = null;
        }

        /// <summary>
        /// THE ORDER THIS PRESS WALKS, as positions in the nearest-first list it was handed.
        ///
        /// A sweep walks the order it started in, so its entries come first and in that order.
        /// Anything that has GONE since is simply missing from the answer, and anything that has
        /// ARRIVED since joins at the end in nearest-first order - the sweep keeps its shape and the
        /// player can still reach everything the category holds. Outside a sweep, and for a sweep
        /// whose order is not known, the answer is the nearest-first list unchanged.
        ///
        /// The answer is always a permutation of the whole list, so a count taken from it is the
        /// count the player is told.
        /// </summary>
        public static int[] Ordering(IList<string> nearest, IList<string> frozen)
        {
            int count = nearest == null ? 0 : nearest.Count;
            int[] order = new int[count];
            if (count == 0)
            {
                return order;
            }

            if (frozen == null || frozen.Count == 0)
            {
                for (int i = 0; i < count; i++)
                {
                    order[i] = i;
                }

                return order;
            }

            bool[] taken = new bool[count];
            int at = 0;
            for (int i = 0; i < frozen.Count; i++)
            {
                int found = IndexOf(nearest, frozen[i]);
                if (found >= 0 && !taken[found])
                {
                    taken[found] = true;
                    order[at++] = found;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (!taken[i])
                {
                    order[at++] = i;
                }
            }

            return order;
        }

        /// <summary>
        /// Where the press lands, as an index into the list as it now stands, or -1 where the list is
        /// empty.
        /// </summary>
        /// <param name="delta">+1 for the next, -1 for the previous.</param>
        /// <param name="keys">The list this press walks, by identity, in walking order.</param>
        /// <param name="standing">What the walk was last standing on, or null.</param>
        /// <param name="sweeping">What <see cref="Sweeping"/> answered for this press.</param>
        /// <param name="parked">Whether the nearest entry is the one the walk is standing on AND the
        /// player is standing on it - which is what a landing leaves behind.</param>
        public static int Land(
            int delta,
            IList<string> keys,
            string standing,
            bool sweeping,
            bool parked
        )
        {
            int count = keys == null ? 0 : keys.Count;
            if (count == 0)
            {
                return -1;
            }

            if (!sweeping)
            {
                // A new sweep starts at the nearest thing - unless that is the thing the player is
                // already standing on, in which case the press steps past it.
                return parked && count > 1 ? Cycle.Wrap(delta, count) : 0;
            }

            int at = IndexOf(keys, standing);
            if (at < 0)
            {
                // The entry the sweep was on has gone. Landing on the end the press was heading for
                // is not a wrap: the player never walked off anything.
                return delta < 0 ? count - 1 : 0;
            }

            return Cycle.Wrap(at + delta, count);
        }

        private static string[] Copy(IList<string> keys)
        {
            if (keys == null)
            {
                return null;
            }

            string[] copy = new string[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                copy[i] = keys[i];
            }

            return copy;
        }

        private static int IndexOf(IList<string> keys, string key)
        {
            for (int i = 0; key != null && i < keys.Count; i++)
            {
                if (keys[i] == key)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool _anchored;
        private int _category = -1;
        private double _east;
        private double _north;
        private string[] _order;
    }
}

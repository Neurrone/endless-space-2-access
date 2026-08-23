using System.Collections.Generic;

namespace ES2Access.Core.UI
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
    /// THE SWEEP RE-ANCHORS WHEN THE PLAYER MOVES. The list is sorted from where the player is
    /// reading, and each landing takes them somewhere - so the next press is measured from the new
    /// place, and continuing an old ordering from a new place would walk the map in an order that
    /// meant nothing. A press from an unmoved place continues the sweep; a press from anywhere else
    /// starts a new one at the nearest thing.
    ///
    /// AND A PRESS WHILE PARKED ON THE CURRENT ENTRY STEPS ON. Landing on a thing makes that thing
    /// the nearest thing, so a restarted sweep would otherwise land on it again and the key would
    /// look stuck. The press asked to move: it moves. That is what turns the key into a
    /// nearest-neighbour hop across the map rather than a way of re-reading one result.
    ///
    /// Engine-free, because every failure here is inaudible: a walk that re-lands, one that stops at
    /// the end of the list, and one that steps from an entry that has since vanished all sound like
    /// a galaxy with less in it than the player thought.
    /// </summary>
    public sealed class ScannerWalk
    {
        /// <summary>Whether this press continues the sweep the last one was in: the same category,
        /// from the same place. Asked before the press is acted on, and it records nothing - the
        /// press may yet find nothing to land on.</summary>
        public bool Sweeping(int category, double east, double north)
        {
            return _anchored && _category == category && _east == east && _north == north;
        }

        /// <summary>Where this press ended up looking from, so the next one can tell whether the
        /// player has moved since.</summary>
        public void Anchor(int category, double east, double north)
        {
            _anchored = true;
            _category = category;
            _east = east;
            _north = north;
        }

        /// <summary>Forget the sweep - the mod being torn down, or another galaxy.</summary>
        public void Forget()
        {
            _anchored = false;
            _category = -1;
            _east = 0.0;
            _north = 0.0;
        }

        /// <summary>
        /// Where the press lands, as an index into the list as it now stands, or -1 where the list is
        /// empty.
        /// </summary>
        /// <param name="delta">+1 for the next, -1 for the previous.</param>
        /// <param name="keys">The category's whole list, nearest first, by identity.</param>
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
                return parked && count > 1 ? Wrap(delta, count) : 0;
            }

            int at = IndexOf(keys, standing);
            if (at < 0)
            {
                // The entry the sweep was on has gone. Landing on the end the press was heading for
                // is not a wrap: the player never walked off anything.
                return delta < 0 ? count - 1 : 0;
            }

            return Wrap(at + delta, count);
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

        private static int Wrap(int value, int length)
        {
            return length <= 0 ? 0 : ((value % length) + length) % length;
        }

        private bool _anchored;
        private int _category = -1;
        private double _east;
        private double _north;
    }
}

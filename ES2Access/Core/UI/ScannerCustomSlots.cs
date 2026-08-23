using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// THE THREE SLOTS a player's own scanner categories live in - fixed, numbered, and each either
    /// empty or holding one category (owner ruling 2026-08-23).
    ///
    /// Fixed slots rather than a list is the whole design: the six quick keys address a SLOT, so
    /// "custom category 2" means the same thing to the player on every press of the same key,
    /// whatever they have done to the other two. There are therefore no ids, no ordering, and no
    /// delete - clearing a slot is the delete, and the slot is still there afterwards.
    ///
    /// Engine-free, and holding nothing but the three categories: where they are STORED is the
    /// settings file's business (<see cref="ScannerCustomCodec"/> writes one string per slot) and
    /// what they CATCH is the scanner's.
    /// </summary>
    public sealed class ScannerCustomSlots
    {
        /// <summary>How many there are. Fixed, and the same number as the pairs of quick keys.
        /// </summary>
        public const int Count = 3;

        /// <summary>What is in a slot, or null where it is empty. An index outside the three answers
        /// null rather than throwing: the callers are a key handler and a settings row.</summary>
        public ScannerCustomCategory Slot(int slot)
        {
            return slot < 0 || slot >= Count ? null : _slots[slot];
        }

        /// <summary>Put a category in a slot, or null to clear it. A nameless category is refused -
        /// the name is what the category cycle says, and a slot holding a nameless one would be a
        /// category the player cannot hear.</summary>
        public bool Set(int slot, ScannerCustomCategory category)
        {
            if (slot < 0 || slot >= Count)
            {
                return false;
            }

            if (category != null && category.Name == null)
            {
                return false;
            }

            _slots[slot] = category;
            return true;
        }

        public bool Clear(int slot)
        {
            return Set(slot, null);
        }

        /// <summary>Whether any slot holds anything - what tells the scanner whether to do any of
        /// this work at all.</summary>
        public bool Any
        {
            get
            {
                for (int i = 0; i < Count; i++)
                {
                    if (_slots[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// WHETHER A NAME IS ALREADY TAKEN - by one of the scanner's built-in categories or by
        /// another slot.
        ///
        /// Trimmed and case-insensitive, because the conflict a player hits is a spoken one: two
        /// categories the cycle reads out with the same words are indistinguishable however they are
        /// spelt. The built-in labels are handed IN rather than looked up, because they are localized
        /// and live - the caller resolves them in the language the game is being played in.
        ///
        /// A pure function on purpose: the editor asks it before it commits anything, and the answer
        /// must not depend on what has been half-typed anywhere.
        /// </summary>
        public bool NameTaken(string name, int slot, IList<string> builtInLabels)
        {
            string wanted = ScannerCustomCategory.Clean(name);
            if (wanted == null)
            {
                return false;
            }

            for (int i = 0; builtInLabels != null && i < builtInLabels.Count; i++)
            {
                if (Matches(builtInLabels[i], wanted))
                {
                    return true;
                }
            }

            for (int i = 0; i < Count; i++)
            {
                if (i != slot && _slots[i] != null && Matches(_slots[i].Name, wanted))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// WHETHER TWO SETS OF SLOTS SAY THE SAME THING - what the editor's "has anything changed"
        /// is, and therefore what lights Apply and what makes Escape ask.
        ///
        /// Ordinal and order-sensitive throughout, because everything here is: a selector's order is
        /// its column order, a keyword's order is its column order, and a name the player retyped in
        /// a different case is a different name to hear.
        /// </summary>
        public bool Same(ScannerCustomSlots other)
        {
            if (other == null)
            {
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                if (!Same(_slots[i], other._slots[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Same(ScannerCustomCategory left, ScannerCustomCategory right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            if (left.Name != right.Name || left.Selectors.Count != right.Selectors.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Selectors.Count; i++)
            {
                if (!left.Selectors[i].Same(right.Selectors[i]))
                {
                    return false;
                }
            }

            if (left.Keywords.Count != right.Keywords.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Keywords.Count; i++)
            {
                if (left.Keywords[i] != right.Keywords[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>An independent copy of all three - what an editor holds its edits in until Apply.
        /// </summary>
        public ScannerCustomSlots Copy()
        {
            ScannerCustomSlots copy = new ScannerCustomSlots();
            for (int i = 0; i < Count; i++)
            {
                copy._slots[i] = _slots[i] == null ? null : _slots[i].Copy();
            }

            return copy;
        }

        private static bool Matches(string label, string wanted)
        {
            string clean = ScannerCustomCategory.Clean(label);
            return clean != null && string.Equals(clean, wanted, StringComparison.OrdinalIgnoreCase);
        }

        private readonly ScannerCustomCategory[] _slots = new ScannerCustomCategory[Count];
    }
}

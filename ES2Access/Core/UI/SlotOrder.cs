using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI
{
    /// <summary>Which way a weapon slot's guns can be brought to bear: at both sides, at one side, at
    /// the nose, at the tail, or nowhere nameable - the last being every slot the game gave no firing
    /// cone at all, and every cone shape that is none of these.</summary>
    public enum SlotFacing
    {
        None,
        Broadside,
        FrontTurret,
        RightBroadside,
        LeftBroadside,
        RearTurret,
    }

    /// <summary>
    /// Putting a ship's module slots in an order the player can predict: alphabetically by the TYPE of
    /// module each slot takes.
    ///
    /// A hull draws its slots wherever its own 3D model puts the guns, so the order they are read in is
    /// an accident of the model - weapon, support, weapon - and a walk down the ship never gathers the
    /// slots a player is looking for. The type is the fact the ship is being walked FOR, so the type is
    /// what the list is grouped by.
    ///
    /// The key is the SLOT's, never the module fitted in it: a module called "A-something" sitting in a
    /// weapons slot is still read with the weapons, and taking it out or putting another one in cannot
    /// move the row the cursor is standing on. A slot that accepts several types is filed under the
    /// alphabetically first of them, with the rest as tie-breaks, so a defence-and-support slot always
    /// sits with the defence slots whatever is in it. A slot with no restriction at all takes anything,
    /// so it belongs to no type and is read last. Ties keep the order they arrived in, which leaves the
    /// drawn order intact inside one type.
    ///
    /// The words compared are the ones the player HEARS - the game's own localized titles for its
    /// module categories - so the alphabet is the player's, not an internal enum's.
    ///
    /// Inside one type the slots that shoot the same way are read together: the broadsides, then the
    /// front turrets, then the rear turrets, then whatever the game gave no firing cone (owner ruling,
    /// 2026-09-10). That is a fact about the SLOT like its type is, and it is compared as a rank rather
    /// than as the words it is spoken with, so translating the phrases cannot reshuffle the ship.
    /// </summary>
    public static class SlotOrder
    {
        /// <summary>Put one slot's type names in the order they are compared in: alphabetically, with
        /// anything the game left unnamed after them.</summary>
        public static void Alphabetical(string[] names)
        {
            if (names == null)
            {
                return;
            }

            for (int i = 1; i < names.Length; i++)
            {
                string name = names[i];
                int at = i - 1;
                while (at >= 0 && CompareName(names[at], name) > 0)
                {
                    names[at + 1] = names[at];
                    at--;
                }

                names[at + 1] = name;
            }
        }

        /// <summary>Which of two slots is read first, from their type names - each set already
        /// <see cref="Alphabetical"/>. A slot with no type at all is read after every slot that has
        /// one; otherwise the first type that differs decides, and a slot that accepts a strict prefix
        /// of another's types is read first.</summary>
        public static int Compare(string[] left, string[] right)
        {
            int mine = Count(left);
            int theirs = Count(right);
            if (mine == 0 || theirs == 0)
            {
                return mine == theirs ? 0 : (mine == 0 ? 1 : -1);
            }

            int shared = mine < theirs ? mine : theirs;
            for (int i = 0; i < shared; i++)
            {
                int order = CompareName(left[i], right[i]);
                if (order != 0)
                {
                    return order;
                }
            }

            return mine - theirs;
        }

        /// <summary>Which of two slots is read first when both take the same types: the one whose guns
        /// cover the wider arc of the ship - broadside, then front turret, then rear turret, then a
        /// slot with no cone at all.</summary>
        public static int Compare(
            string[] left,
            SlotFacing leftFacing,
            string[] right,
            SlotFacing rightFacing
        )
        {
            int order = Compare(left, right);
            return order != 0 ? order : Rank(leftFacing) - Rank(rightFacing);
        }

        /// <summary>Order <paramref name="items"/> by their parallel <paramref name="keys"/>, keeping
        /// the order they came in wherever two keys are equal - an insertion sort, which is stable and
        /// is walking a list of a hull's slots.</summary>
        public static void Arrange<T>(IList<T> items, IList<string[]> keys)
        {
            Arrange(items, keys, null);
        }

        /// <summary>The same, with the way each slot shoots as the tie-break inside one type.</summary>
        public static void Arrange<T>(
            IList<T> items,
            IList<string[]> keys,
            IList<SlotFacing> facings
        )
        {
            if (
                items == null
                || keys == null
                || items.Count != keys.Count
                || (facings != null && facings.Count != items.Count)
            )
            {
                return;
            }

            for (int i = 1; i < items.Count; i++)
            {
                T item = items[i];
                string[] key = keys[i];
                SlotFacing facing = At(facings, i);
                int at = i - 1;
                while (at >= 0 && Compare(keys[at], At(facings, at), key, facing) > 0)
                {
                    items[at + 1] = items[at];
                    keys[at + 1] = keys[at];
                    if (facings != null)
                    {
                        facings[at + 1] = facings[at];
                    }

                    at--;
                }

                items[at + 1] = item;
                keys[at + 1] = key;
                if (facings != null)
                {
                    facings[at + 1] = facing;
                }
            }
        }

        private static SlotFacing At(IList<SlotFacing> facings, int index)
        {
            return facings == null ? SlotFacing.None : facings[index];
        }

        /// <summary>Where a facing sits in the reading order: the broadsides first - both sides, then
        /// the right one, then the left one - then the front turrets, then the rear turrets, and last
        /// the slots with no cone the mod has a word for. Not the enum's own values: a slot nobody
        /// asked about the facing of is <see cref="SlotFacing.None"/>, and that answer is read last
        /// rather than first.</summary>
        private static int Rank(SlotFacing facing)
        {
            switch (facing)
            {
                case SlotFacing.Broadside:
                    return 0;
                case SlotFacing.RightBroadside:
                    return 1;
                case SlotFacing.LeftBroadside:
                    return 2;
                case SlotFacing.FrontTurret:
                    return 3;
                case SlotFacing.RearTurret:
                    return 4;
                default:
                    return 5;
            }
        }

        /// <summary>How many of a slot's types the game gave a word to - the rest sort after them and
        /// are not part of the key.</summary>
        private static int Count(string[] names)
        {
            int count = 0;
            for (int i = 0; names != null && i < names.Length; i++)
            {
                if (!string.IsNullOrEmpty(names[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CompareName(string left, string right)
        {
            bool mine = string.IsNullOrEmpty(left);
            bool theirs = string.IsNullOrEmpty(right);
            if (mine || theirs)
            {
                return mine == theirs ? 0 : (mine ? 1 : -1);
            }

            int order = string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
            return order != 0 ? order : string.CompareOrdinal(left, right);
        }
    }
}

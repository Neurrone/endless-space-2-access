using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// One list, two owners.
    ///
    /// The player walks the game's notification strip and the mod's own Turn log as two separate
    /// lists, but the engine keeps every notification in ONE collection and the only thing telling
    /// the two apart is who raised each entry. So each "throw them all away" button has to be told
    /// which side of that split it may touch: a dismiss-all that walks the whole collection empties
    /// the other list as well (owner ruling 2026-08-24 - neither button ever clears the other's).
    ///
    /// The split is handed in as a CONVERTER rather than as a predicate so that one test answers
    /// both questions: the mod's side comes back already typed, and the game's side is exactly what
    /// that same test did not claim. Nothing can be in both lists and nothing can fall between them,
    /// which is the property the two buttons rest on and the one the tests state.
    ///
    /// Null entries belong to neither side: the engine's own walks over that collection check for
    /// them, so a hole in it is a thing that happens rather than a thing to trust away.
    /// </summary>
    public static class OwnedNotifications
    {
        /// <summary>The entries <paramref name="mine"/> claims, in the list's own order.</summary>
        public static List<TMine> Mine<TItem, TMine>(IList<TItem> all, Converter<TItem, TMine> mine)
            where TItem : class
            where TMine : class
        {
            List<TMine> found = new List<TMine>();
            if (all == null || mine == null)
            {
                return found;
            }

            for (int i = 0; i < all.Count; i++)
            {
                TMine claimed = all[i] == null ? null : mine(all[i]);
                if (claimed != null)
                {
                    found.Add(claimed);
                }
            }

            return found;
        }

        /// <summary>The entries <paramref name="mine"/> does not claim, in the list's own order -
        /// what a dismiss-all over the OTHER owner's list is allowed to touch.</summary>
        public static List<TItem> Theirs<TItem, TMine>(IList<TItem> all, Converter<TItem, TMine> mine)
            where TItem : class
            where TMine : class
        {
            List<TItem> found = new List<TItem>();
            if (all == null || mine == null)
            {
                return found;
            }

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && mine(all[i]) == null)
                {
                    found.Add(all[i]);
                }
            }

            return found;
        }
    }
}

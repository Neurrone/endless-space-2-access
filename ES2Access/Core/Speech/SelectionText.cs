using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// What a list the player picks SEVERAL things out of says about its own membership.
    ///
    /// Two questions, and they are not the same one. A row asks "am I in the selection" - and unlike a
    /// radio group, where saying nothing is what lets focus land on the choice already made, here the
    /// absence has to be audible: a player walking a ship list needs to hear which ships are picked and
    /// which are not. A RANGE asks something the rows cannot answer at all, because every row between
    /// the anchor and here has just changed: what the selection is NOW. That is one sentence about the
    /// whole list, and it is composed here rather than in a screen so the rule - and its edges, a range
    /// that came down to one row or to none - is testable off the game.
    /// </summary>
    public static class SelectionText
    {
        /// <summary>Whether this row is in the selection, in whichever of the two words the player's
        /// language uses.</summary>
        public static string Membership(bool selected)
        {
            return ModStrings.Get(selected ? ModStrings.NavSelected : ModStrings.NavNotSelected);
        }

        /// <summary>
        /// What a range selection came to: how many are picked out and the two names at its ends.
        ///
        /// Null when there is no range to report - nothing selected, or a single row, where the row's
        /// own <see cref="Membership"/> is the truthful and shorter answer. The caller falls back to it,
        /// which is also what keeps "1 ships selected" out of the language - and why the pair's own
        /// singular (<see cref="ModStrings.FleetsShipRange"/>) is never spoken, existing only so a
        /// three-form language is asked for the paucal a range of two, three or four needs.
        /// </summary>
        public static string Range(IList<string> names)
        {
            if (names == null || names.Count < 2)
            {
                return null;
            }

            string first = names[0];
            string last = names[names.Count - 1];
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(last))
            {
                return null;
            }

            return ModStrings.Format(
                ModStrings.PluralKey(
                    ModStrings.FleetsShipRange,
                    ModStrings.FleetsShipsRange,
                    names.Count
                ),
                names.Count,
                first,
                last
            );
        }
    }
}

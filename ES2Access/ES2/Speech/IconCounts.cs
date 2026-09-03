using System.Globalization;
using System.Text;
using ES2Access.Core.Speech;

namespace ES2Access.ES2.Speech
{
    /// <summary>
    /// The one icon the game writes a NUMBER against rather than a noun, and what that number and
    /// icon say together.
    ///
    /// The turn icon is drawn hard against its count - the cost panel writes
    /// "{0} ({1}[turnColored])" and the remaining-turns panel "{0} [turnColored]" - so naming the
    /// picture the way every other icon is named reads it as "(8 Turn)" and "1 Turn". A count and
    /// its noun are a counted phrase, and a counted phrase is one whole sentence per number form in
    /// every language the mod speaks, so the pair goes through <see cref="ModStrings.Plural"/> and
    /// a translator owns the word order: English "8 turns", Polish "8 tury", Russian "ходов: 8".
    ///
    /// This is the ONLY icon with a count rule (owner ruling 2026-09-03). Every other icon stands
    /// for a noun that reads correctly beside a bare number - "60 Industry" - and inflecting them
    /// all would ask every translator for a hundred counted pairs to fix nothing.
    ///
    /// Engine-free: this decides, and <c>ES2Access.UI.AgeText</c> splices.
    /// </summary>
    public static class IconCounts
    {
        // int.Parse cannot overflow on nine digits, and no turn count the game draws is longer.
        private const int MaxDigits = 9;

        /// <summary>
        /// What <paramref name="token"/> and the number in front of it say together, or null when
        /// no count rule applies and the icon is to be named the ordinary way.
        ///
        /// <paramref name="before"/> is the reading built so far, whose tail is where the number
        /// is; <paramref name="consumed"/> is how many of its trailing characters the phrase
        /// replaces - the digits and whatever blanks separated them from the icon, because the
        /// phrase carries its own spacing between the two.
        ///
        /// A rule applies only to a whole INTEGER standing on its own: the digit run has to be
        /// preceded by nothing at all or by something that is neither a letter nor a digit nor a
        /// sign nor a decimal point, so "1.5[turnColored]", "-3[turnColored]" and "v2[turnColored]"
        /// all keep the plain reading rather than being told a count they do not carry. Blanks
        /// between the number and the icon are allowed and a newline is not, because a number on
        /// the line above is a different sentence; colour markup between them is left alone too,
        /// which costs the counted reading there and never spoils it.
        /// </summary>
        public static string Phrase(StringBuilder before, string token, out int consumed)
        {
            consumed = 0;
            string key;
            if (!IconTable.TryKeyForToken(token, out key) || key != ModStrings.IconTurn)
            {
                return null;
            }

            int end = before.Length;
            while (end > 0 && (before[end - 1] == ' ' || before[end - 1] == '\t'))
            {
                end--;
            }

            int start = end;
            while (start > 0 && before[start - 1] >= '0' && before[start - 1] <= '9')
            {
                start--;
            }

            if (start == end || end - start > MaxDigits || !StandsAlone(before, start))
            {
                return null;
            }

            int count = int.Parse(before.ToString(start, end - start), CultureInfo.InvariantCulture);
            consumed = before.Length - start;
            return ModStrings.Plural(ModStrings.IconTurnCount, ModStrings.IconTurnsCount, count);
        }

        // Whether the digit run beginning at start is a number in its own right rather than the
        // tail of something longer.
        private static bool StandsAlone(StringBuilder before, int start)
        {
            if (start == 0)
            {
                return true;
            }

            char c = before[start - 1];
            return !char.IsLetterOrDigit(c) && c != '.' && c != ',' && c != '+' && c != '-';
        }
    }
}

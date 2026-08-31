using System.Text;

namespace ES2Access.Core.Util
{
    /// <summary>
    /// Turning a piece of the GAME's text into a piece of a FILE's name.
    ///
    /// The mod names a per-campaign file after the empire's faction, so that a folder of them can be
    /// read by a person rather than by a machine. That text is not the mod's: a stock faction's
    /// internal name is a tidy identifier, but a custom faction's is whatever the player typed into
    /// the editor - spaces, punctuation, emoji, a slash, a name in a script this machine's file
    /// system may or may not like, or nothing at all.
    ///
    /// So the rule is a whitelist and not a blacklist: LETTERS AND DIGITS SURVIVE, everything else is
    /// dropped. That is stricter than the file system needs and deliberately so - a blacklist has to
    /// know about every reserved character of every platform, about trailing dots and spaces, about
    /// leading dashes, and it is wrong the day one of them changes. Letters and digits are safe
    /// everywhere and always, in any script.
    ///
    /// Answers the empty string where nothing survives, which is the caller's cue to fall back to a
    /// name that needs no text at all.
    /// </summary>
    public static class FileNameText
    {
        /// <summary>
        /// <paramref name="text"/> as a file-name part of at most <paramref name="maxLength"/>
        /// characters - the letters and digits of it, in order, cut to length.
        ///
        /// The cut never splits a surrogate pair: half a pair is not a character, and some file
        /// systems refuse it outright. A cap of zero or less answers the empty string, which is the
        /// caller saying it wants no text at all.
        /// </summary>
        public static string Safe(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || maxLength <= 0)
            {
                return string.Empty;
            }

            StringBuilder kept = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length && kept.Length < maxLength; i++)
            {
                char c = text[i];
                // Asked of the STRING and not of the char, because a letter outside the basic plane
                // is written as two chars and neither of them is a letter on its own.
                bool pair =
                    char.IsHighSurrogate(c)
                    && i + 1 < text.Length
                    && char.IsLowSurrogate(text[i + 1]);
                if (!char.IsLetterOrDigit(text, i))
                {
                    if (pair)
                    {
                        i++;
                    }

                    continue;
                }

                if (!pair)
                {
                    kept.Append(c);
                    continue;
                }

                // A pair counts as the two chars it is written with, and is taken whole or not at
                // all: half of one is not a character, and some file systems refuse it outright.
                if (kept.Length + 2 > maxLength)
                {
                    break;
                }

                kept.Append(c).Append(text[i + 1]);
                i++;
            }

            return kept.ToString();
        }
    }
}

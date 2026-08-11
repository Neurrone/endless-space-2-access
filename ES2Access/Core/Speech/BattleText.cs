using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// What the mod says about a battle in its own words - which is only ever what the game writes
    /// down nowhere: how many ships were just lost, how far through the fight is.
    ///
    /// Everything a battle SAYS on screen comes from the game (the outcome word, the phase title, each
    /// ship's status sentence) and is spoken as the game wrote it. What is left here is the counting,
    /// and counting needs a form per number, which is why these are keys rather than glued fragments.
    ///
    /// The narration is written against phrases the build may not carry yet, which is why every one of
    /// them is asked for OPTIONALLY (<see cref="OptionalText.Phrase"/>): a narrator that read
    /// "battle.your-ships-lost" at the player mid-cinematic would be worse than one that said nothing.
    /// </summary>
    public static class BattleText
    {
        /// <summary>The battle narration's name for <see cref="OptionalText.Phrase"/> - the phrase for
        /// <paramref name="key"/>, or null where this build has no such phrase.</summary>
        public static string Optional(string key, params object[] args)
        {
            return OptionalText.Phrase(key, args);
        }

        /// <summary>A counted phrase in the form its number calls for (see
        /// <see cref="ModStrings.Plural"/>), or null where the build has no such phrase.</summary>
        public static string Counted(string oneKey, string manyKey, int count)
        {
            return Optional(count == 1 ? oneKey : manyKey, count);
        }

        /// <summary>
        /// One line for a burst of things lost at once.
        ///
        /// A single loss is NAMED - the game has a sentence for what happened to that ship, and it is
        /// the interesting thing about it. Several at once are COUNTED instead: a salvo takes four ships
        /// in a second, and reading four names over the top of the next salvo buries the one fact that
        /// matters, which is that four went. So <paramref name="oneKey"/> takes the name and
        /// <paramref name="manyKey"/> takes the count.
        /// </summary>
        public static string Losses(IList<string> names, string oneKey, string manyKey)
        {
            if (names == null || names.Count == 0)
            {
                return null;
            }

            if (names.Count == 1)
            {
                return string.IsNullOrEmpty(names[0]) ? null : Optional(oneKey, names[0]);
            }

            return Optional(manyKey, names.Count);
        }
    }
}

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
    /// <see cref="Optional"/> is the other half and the reason this file exists at all: a screen may be
    /// written against a phrase the build does not carry yet. <see cref="ModStrings.Get"/> answers an
    /// unknown key with the key itself - deliberately, so a typo is audible - and a narrator that spoke
    /// "battle.your-ships-lost" at the player would be worse than one that said nothing. So a phrase
    /// asked for here is OPTIONAL: present, it speaks; absent, that line of narration is simply silent
    /// and everything else on the screen still works.
    /// </summary>
    public static class BattleText
    {
        /// <summary>The phrase for <paramref name="key"/>, or null where this build has no such
        /// phrase. Never the key itself.</summary>
        public static string Optional(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            string template = ModStrings.Get(key);
            if (string.IsNullOrEmpty(template) || template == key)
            {
                return null;
            }

            return args == null || args.Length == 0 ? template : ModStrings.Format(key, args);
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

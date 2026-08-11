namespace ES2Access.Core.Speech
{
    /// <summary>
    /// A mod-authored phrase a screen may have been written against before the build carries it.
    ///
    /// <see cref="ModStrings.Get"/> answers an unknown key with the KEY - deliberately, so a typo is
    /// audible while a screen is being built. That is the right answer for a phrase the screen cannot
    /// do without, and the wrong one for a phrase it can: a screen that read
    /// "screen.error" at the player would be worse than one that said nothing and let the game's own
    /// words stand.
    ///
    /// So a phrase asked for HERE is optional: present, it speaks; absent, that line is silent and
    /// everything else on the screen still works. It is how a screen stage lands ahead of the
    /// translation it needs, and how a translation that has not caught up degrades.
    /// </summary>
    public static class OptionalText
    {
        /// <summary>The phrase for <paramref name="key"/>, or null where this build has no such
        /// phrase. Never the key itself.</summary>
        public static string Phrase(string key, params object[] args)
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
    }
}

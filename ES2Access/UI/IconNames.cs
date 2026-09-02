using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.ES2.Speech;

namespace ES2Access.UI
{
    /// <summary>
    /// What an icon is called, for the two places an icon reaches a reader: an inline
    /// <c>[token]</c> in a sentence (<see cref="AgeText.Clean"/>, before the engine's own
    /// <c>AgeUtils.CleanLine</c> turns the bracket into a picture glyph) and a picture the game
    /// draws on its own (<see cref="DrawnTooltip"/>, where a stat strip's column headings are
    /// pictures and the numbers beside them are unreadable without them).
    ///
    /// Both are one lookup in <see cref="IconTable"/> followed by one <see cref="ModStrings"/>
    /// read. Nothing is inferred from the icon's id: an id is an asset name, and a name derived
    /// from it is a guess that reads as one - "9 turnColored", "System Development Upgrades" for
    /// the Dust coin, "92% (blue-gray Infantry 44)". The table says what each icon is called or
    /// says that it is called nothing, and there is no third answer.
    ///
    /// The two kinds fail differently, because their universes are different sizes. The engine
    /// registers a CLOSED set of 382 tokens from its own files, so a token the table has never
    /// heard of is a patch or a mod adding one, worth exactly one warning and then silence -
    /// <see cref="UnknownTokens"/> is the audit list. Pictures are an OPEN set - every bitmap in
    /// the game can be drawn into a panel - so a picture the table does not name is the ordinary
    /// case of decoration, and answering null for it is the filter that keeps backgrounds, rules
    /// and portraits out of the reading rather than a failure. Those are collected in
    /// <see cref="UnknownPictures"/> for auditing, and never logged.
    ///
    /// Main-thread only (one shared warn-once set per load).
    /// </summary>
    public static class IconNames
    {
        /// <summary>How many unnamed pictures to remember. It is an audit sample, not a census -
        /// a screen full of art must not grow this without bound.</summary>
        private const int MaxRemembered = 200;

        private static readonly List<string> Tokens = new List<string>();
        private static readonly List<string> Pictures = new List<string>();

        /// <summary>The name for the icon <paramref name="token"/> stands for (brackets already
        /// stripped), or null when the table says it has no name - and, once, a warning when the
        /// table has never heard of it.</summary>
        public static string NameFor(string token)
        {
            string key;
            if (!IconTable.TryKeyForToken(token, out key))
            {
                if (Remember(Tokens, token))
                {
                    Log.Warn("icons: the icon table has no entry for the token '" + token + "'");
                }

                return null;
            }

            return key.Length == 0 ? null : ModStrings.Get(key);
        }

        /// <summary>
        /// The name for a picture the game draws on its own, identified by the name of the
        /// TEXTURE being drawn - <c>AgePrimitiveImage.Texture.name</c>, which is the file the
        /// artist authored ("FIDSIFood").
        ///
        /// Null is the answer for every picture that is not standing in for a word, which is what
        /// makes this usable as a filter and not only as a lookup: a panel is full of backgrounds,
        /// rules and portraits, and a tooltip that read them all out would announce a line the
        /// panel never wrote.
        /// </summary>
        public static string NameForAsset(string asset)
        {
            string key;
            if (!IconTable.TryKeyForPicture(asset, out key))
            {
                Remember(Pictures, asset);
                return null;
            }

            return key.Length == 0 ? null : ModStrings.Get(key);
        }

        /// <summary>The inline tokens seen this load that the table does not cover. Empty is the
        /// expected result; anything in it is a name a player heard nothing for.</summary>
        public static IList<string> UnknownTokens
        {
            get { return Tokens; }
        }

        /// <summary>The textures seen this load that the table does not name. Mostly decoration
        /// - read it when a tooltip is missing a word, to see which picture carried it.</summary>
        public static IList<string> UnknownPictures
        {
            get { return Pictures; }
        }

        private static bool Remember(List<string> seen, string id)
        {
            if (string.IsNullOrEmpty(id) || seen.Contains(id))
            {
                return false;
            }

            if (seen.Count >= MaxRemembered)
            {
                return false;
            }

            seen.Add(id);
            return true;
        }
    }
}

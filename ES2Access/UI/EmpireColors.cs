using System;
using System.Collections.Generic;
using System.Text;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// What an empire colour is called.
    ///
    /// The game draws its colour lists as bare swatches - twelve squares with no words anywhere on
    /// them, in the list or in a tooltip - so a keyboard player was offered "2 of 12" and nothing else.
    /// But the game does name them, in its own palette data: <c>Public/Mapping/Palettes.xml</c> gives
    /// every colour a Name ("MarineBlue", "Wine", "Apple"), which is what the swatch IS, just never
    /// written on screen. So the name comes from the colour itself - match the drawn swatch against the
    /// palette the player has chosen in the options and read back what that palette calls it.
    ///
    /// Matching by COLOUR rather than by list position is deliberate: it does not assume a list is the
    /// palette in palette order, so a list that omits or reorders colours still names them right.
    ///
    /// Those names are data identifiers, not localized text - the corpus has no entry for any of them -
    /// so each becomes a <see cref="ModStrings"/> key whose English default is the game's own word,
    /// spaced out for reading. A palette a mod adds, or a colour a patch renames, falls through to the
    /// game's word split at its capitals rather than to silence.
    /// </summary>
    public static class EmpireColors
    {
        /// <summary>How far apart two colours can be and still be the same one. The palette is authored
        /// in whole bytes and arrives as floats, so this is rounding, not tolerance.</summary>
        private const float SamePlace = 1.5f / 255f;

        /// <summary>What the palette the player is using calls <paramref name="color"/>, or null when
        /// nothing in it matches - a colour the game drew from somewhere else entirely.</summary>
        public static string Name(Color color)
        {
            try
            {
                List<Entry> palette = Palette();
                for (int i = 0; i < palette.Count; i++)
                {
                    if (Same(palette[i].Color, color))
                    {
                        return Spoken(palette[i].Name);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("colors: naming an empire colour threw: " + e);
            }

            return null;
        }

        /// <summary>Forget the palette - the mod is going away, or the player has changed it.</summary>
        public static void Reset()
        {
            _entries = null;
            _from = null;
        }

        private struct Entry
        {
            public string Name;
            public Color Color;
        }

        // The palette is read once and held: it is a database lookup plus a walk of seventeen colours,
        // and it only changes when the player picks another palette in the options - which the game
        // itself asks for a restart to apply.
        private static List<Entry> _entries;
        private static string _from;

        private static List<Entry> Palette()
        {
            IGuiOptionsService options =
                Amplitude.Unity.Framework.Services.GetService<IGuiOptionsService>();
            string chosen = options == null ? null : options.EmpireColorPalette;
            if (_entries != null && _from == chosen)
            {
                return _entries;
            }

            _from = chosen;
            _entries = new List<Entry>();
            if (string.IsNullOrEmpty(chosen))
            {
                return _entries;
            }

            Amplitude.Unity.Framework.IDatabase<global::Palette> palettes =
                Amplitude.Unity.Framework.Databases.GetDatabase<global::Palette>(true);
            global::Palette palette = palettes == null ? null : palettes.GetValue(chosen);
            Amplitude.Unity.Xml.XmlColorReference[] colors =
                palette == null ? null : palette.Colors;
            for (int i = 0; colors != null && i < colors.Length; i++)
            {
                if (colors[i] == null || colors[i].Name == null)
                {
                    continue;
                }

                _entries.Add(
                    new Entry { Name = colors[i].Name.ToString(), Color = colors[i].ToColor() }
                );
            }

            return _entries;
        }

        private static bool Same(Color first, Color second)
        {
            return Mathf.Abs(first.r - second.r) <= SamePlace
                && Mathf.Abs(first.g - second.g) <= SamePlace
                && Mathf.Abs(first.b - second.b) <= SamePlace;
        }

        /// <summary>The palette's word for a colour, as a translator left it - or, for one nobody has
        /// translated, the game's own identifier read the way it is written: at its capitals.</summary>
        private static string Spoken(string name)
        {
            string key = ModStrings.ColorPrefix + Hyphenated(name);
            string translated;
            return ModStrings.TryGetDefault(key, out translated) ? ModStrings.Get(key) : Split(name);
        }

        private static string Hyphenated(string name)
        {
            StringBuilder text = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    text.Append('-');
                }

                text.Append(char.ToLowerInvariant(name[i]));
            }

            return text.ToString();
        }

        private static string Split(string name)
        {
            StringBuilder text = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    text.Append(' ');
                }

                text.Append(name[i]);
            }

            return text.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace ES2Access.UI
{
    /// <summary>
    /// Turns a raw AGE string into something worth reading aloud, in the one place both the screen
    /// adapters and the dev dump go through.
    ///
    /// Four steps, in order: prefer a label's already-resolved <c>TranslatedText</c>; resolve a
    /// leftover <c>%key</c> through the game's localizer (tooltips and deferred content routinely
    /// still hold keys); run the engine's own <see cref="AgeUtils.CleanLine"/>, which expands the
    /// <c>[Icon]</c> tokens and strips the <c>#RRGGBBAA#</c> colour markup the labels carry; then
    /// drop the glyphs those tokens expanded to, which live in the font's private use area and mean
    /// nothing to a screen reader.
    ///
    /// Newlines survive — the game separates a disabled button's reason from its description with a
    /// blank line, and a screen reader reads that as the pause it is. Every step is guarded: a
    /// getter that throws costs the text, not the frame.
    ///
    /// Main-thread only (it reads live scene objects and shares one scratch buffer).
    /// </summary>
    public static class AgeText
    {
        // The font's private use area, where the [Icon] tokens expand to picture glyphs.
        private const char PrivateUseFirst = '';
        private const char PrivateUseLast = '';

        private static readonly StringBuilder CleanBuffer = new StringBuilder(512);

        /// <summary>The spoken form of a raw AGE string, or null when nothing is left to say.</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            string text = raw;
            try
            {
                if (Gui.IsLocalizationKey(text))
                {
                    text = Gui.Localize(text);
                }
            }
            catch (Exception) { }

            try
            {
                StringBuilder buffer = CleanBuffer;
                AgeUtils.CleanLine(text, ref buffer);
                text = buffer.ToString();
            }
            catch (Exception) { }

            return StripUnspeakable(text);
        }

        /// <summary>A label's spoken text. <c>TranslatedText</c> is the post-localization,
        /// post-markup string the label actually renders; <c>Text</c> is what was assigned, which
        /// for a data-driven caption is still a <c>%key</c>.</summary>
        public static string Label(AgePrimitiveLabel label)
        {
            if (label == null)
            {
                return null;
            }

            string text = null;
            try
            {
                text = label.TranslatedText;
                if (string.IsNullOrEmpty(text))
                {
                    text = label.Text;
                }
            }
            catch (Exception) { }

            return Clean(text);
        }

        /// <summary>A widget's tooltip text. Populated at bind time, so it reads without ever
        /// showing the tooltip window; for a disabled control the game has already appended the
        /// reason it is disabled.</summary>
        public static string Tooltip(AgeTooltip tooltip)
        {
            if (tooltip == null)
            {
                return null;
            }

            try
            {
                return Clean(tooltip.Content);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A cleaned block of game text as separate spoken lines - blank ones dropped - for a
        /// review buffer to walk. The game writes a tooltip as a name, a description and, when it is
        /// refusing, its reason, separated by exactly these newlines.</summary>
        public static IList<string> Lines(string text)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return lines;
            }

            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    lines.Add(trimmed);
                }
            }

            return lines;
        }

        /// <summary>The tooltip attached to <paramref name="transform"/>, if it has one.</summary>
        public static string Tooltip(AgeTransform transform)
        {
            if (transform == null)
            {
                return null;
            }

            try
            {
                return Tooltip(transform.AgeTooltip);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Control characters (newlines excepted) and the private-use glyphs the icon tokens expand
        // to: rendered, they are pictures; spoken, they are noise.
        private static string StripUnspeakable(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            StringBuilder kept = new StringBuilder(text.Length);
            foreach (char character in text)
            {
                bool speakable =
                    character == '\n'
                    || (
                        character >= ' '
                        && (character < PrivateUseFirst || character > PrivateUseLast)
                    );
                if (speakable)
                {
                    kept.Append(character);
                }
            }

            string result = kept.ToString().Trim();
            return result.Length == 0 ? null : result;
        }
    }
}

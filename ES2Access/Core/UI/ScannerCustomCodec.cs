using System.Collections.Generic;
using System.Text;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// ONE SLOT AS ONE LINE of the settings file, and back.
    ///
    /// The shape is <c>name|selectors|keywords</c>, selectors being <c>category:subcategory</c> and
    /// both lists comma-separated. Every separator is backslash-escaped inside the player's own text,
    /// so a category called "Enemy, mostly" survives the round trip; a backslash escapes itself.
    ///
    /// Two rules make the format survive the mod changing:
    ///
    /// <para>MISSING TRAILING FIELDS DECODE. A line written by a build that only knew names reads as
    /// a category with no selectors and no keywords, rather than as corruption - so a field added
    /// later costs nobody their categories.</para>
    ///
    /// <para>CORRUPTION IS AN EMPTY SLOT, never an exception and never a half-built category. The
    /// worst thing this could do is throw on the scanner's key path, and the second worst is hand
    /// back a category holding a selector nobody wrote. A record with no name at all is corruption
    /// by that rule; a single malformed selector is not - it is dropped and the rest is kept, which
    /// is the same forgiveness a selector this galaxy has no column for gets.</para>
    ///
    /// Engine-free: the file is read and written off the engine, and a codec that lost a player's
    /// categories would do it silently.
    /// </summary>
    public static class ScannerCustomCodec
    {
        private const char Field = '|';
        private const char Item = ',';
        private const char Pair = ':';
        private const char Escape = '\\';

        /// <summary>One slot as the settings file holds it. Null for an empty slot, which the caller
        /// stores as a removed key rather than as an empty line.</summary>
        public static string Encode(ScannerCustomCategory category)
        {
            if (category == null || category.Name == null)
            {
                return null;
            }

            StringBuilder text = new StringBuilder();
            Write(text, category.Name);
            text.Append(Field);
            IList<ScannerSelector> selectors = category.Selectors;
            for (int i = 0; i < selectors.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(Item);
                }

                Write(text, selectors[i].Category);
                text.Append(Pair);
                Write(text, selectors[i].Subcategory);
            }

            text.Append(Field);
            IList<string> keywords = category.Keywords;
            for (int i = 0; i < keywords.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(Item);
                }

                Write(text, keywords[i]);
            }

            return text.ToString();
        }

        /// <summary>What one line of the file says, or null where it says nothing usable.</summary>
        public static ScannerCustomCategory Decode(string text)
        {
            if (text == null)
            {
                return null;
            }

            IList<string> fields = Split(text, Field);
            ScannerCustomCategory category = ScannerCustomCategory.Named(
                fields.Count > 0 ? Unescape(fields[0]) : null
            );
            if (category == null)
            {
                return null;
            }

            if (fields.Count > 1)
            {
                IList<string> selectors = Split(fields[1], Item);
                for (int i = 0; i < selectors.Count; i++)
                {
                    IList<string> pair = Split(selectors[i], Pair);
                    if (pair.Count == 2)
                    {
                        category.AddSelector(
                            new ScannerSelector(Unescape(pair[0]), Unescape(pair[1]))
                        );
                    }
                }
            }

            if (fields.Count > 2)
            {
                IList<string> keywords = Split(fields[2], Item);
                for (int i = 0; i < keywords.Count; i++)
                {
                    category.AddKeyword(Unescape(keywords[i]));
                }
            }

            return category;
        }

        private static void Write(StringBuilder text, string value)
        {
            for (int i = 0; value != null && i < value.Length; i++)
            {
                char c = value[i];
                if (c == Field || c == Item || c == Pair || c == Escape)
                {
                    text.Append(Escape);
                }

                text.Append(c);
            }
        }

        /// <summary>
        /// Split on a separator the text may also contain, escaped - KEEPING the escapes, because the
        /// structure is NESTED and the inner separators have not been split on yet. Taking them off
        /// here is the bug this comment exists for: one unescaping pass over an escaped comma hands
        /// the comma back to the keyword split, which then reads one keyword as two.
        ///
        /// An empty input is no parts at all rather than one empty part, so a category with no
        /// keywords does not decode as one blank keyword.
        /// </summary>
        private static IList<string> Split(string text, char separator)
        {
            List<string> parts = new List<string>();
            if (text == null || text.Length == 0)
            {
                return parts;
            }

            StringBuilder part = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == Escape && i + 1 < text.Length)
                {
                    part.Append(c).Append(text[++i]);
                    continue;
                }

                if (c == separator)
                {
                    parts.Add(part.ToString());
                    part.Length = 0;
                    continue;
                }

                part.Append(c);
            }

            parts.Add(part.ToString());
            return parts;
        }

        /// <summary>A leaf value as the player wrote it - the escapes taken off, which happens once
        /// and only where nothing further will be split.</summary>
        private static string Unescape(string text)
        {
            if (text == null || text.IndexOf(Escape) < 0)
            {
                return text;
            }

            StringBuilder plain = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == Escape && i + 1 < text.Length)
                {
                    c = text[++i];
                }

                plain.Append(c);
            }

            return plain.ToString();
        }
    }
}

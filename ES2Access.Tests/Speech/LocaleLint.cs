using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ES2Access.Core.Speech;

namespace ES2Access.Tests.Speech
{
    /// <summary>The alphabet a language writes its own words in, for the one check that can tell a
    /// translation apart from a copy of the English without reading it.</summary>
    public enum NativeScript
    {
        /// <summary>A Latin-script language, where nothing can be concluded from the letters.</summary>
        None,
        Cyrillic,
        Hangul,
        Han,
    }

    /// <summary>One line of a cutscene's audio description, as the file carries it.</summary>
    public sealed class CueRow
    {
        public double At;
        public double End;
        public string Text;
    }

    /// <summary>
    /// The checks a shipped translation has to pass, as pure functions over strings and
    /// dictionaries: bytes in, offender list out, nothing touched on disk.
    ///
    /// Split out from the tests that apply them for two reasons. The lints themselves need
    /// synthetic BAD input to prove they catch anything, and today the mod ships English only - a
    /// suite that only ran them over the shipped files would be a suite of checks nobody had ever
    /// seen fail. And a lint that answers a LIST rather than throwing lets the file-driven tests
    /// report every offender in one failure, which is what a translator handing a file back needs;
    /// one assertion per entry would stop at the first.
    ///
    /// Every function returns an empty list for "clean". Messages name the key, because the file
    /// they came from is known to the caller and repeating it on 400 lines helps nobody.
    ///
    /// Every character this file cares about is written as a code point rather than as itself, on
    /// purpose: a lint for mangled encodings that was itself carried by non-ASCII source bytes
    /// would go wrong exactly when the thing it looks for happened to the repository.
    /// </summary>
    public static class LocaleLint
    {
        private const int ByteOrderMark = 0xFEFF;
        private const int Replacement = 0xFFFD;
        private const int C1First = 0x0080;
        private const int C1Last = 0x009F;

        private static readonly Regex Placeholder = new Regex(@"\{(\d+)(?:[,:][^}]*)?\}");

        /// <summary>The suffix that makes a locale key the paucal form of the key before it.</summary>
        public const string FewSuffix = PluralRules.FewSuffix;

        /// <summary>
        /// What is wrong with the file's BYTES, before anything parses them.
        ///
        /// Every finding here is a mishandled encoding rather than a mistranslation, and they earn
        /// their own check because each survives a JSON parse and reaches the player as garbage: a
        /// byte order mark, bytes that are not UTF-8 at all, the replacement character a lossy
        /// conversion leaves behind, the C1 controls a Windows-1252 round trip produces, and the
        /// bigrams of a double encoding.
        /// </summary>
        public static IList<string> EncodingProblems(byte[] bytes)
        {
            List<string> problems = new List<string>();
            if (bytes == null)
            {
                problems.Add("the file could not be read");
                return problems;
            }

            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException e)
            {
                problems.Add("is not valid UTF-8: " + e.Message);
                return problems;
            }

            if (text.Length > 0 && text[0] == ByteOrderMark)
            {
                problems.Add("starts with a UTF-8 byte order mark");
            }

            SortedSet<string> replacements = new SortedSet<string>();
            SortedSet<string> controls = new SortedSet<string>();
            SortedSet<string> mangled = new SortedSet<string>();
            for (int i = 0; i < text.Length; i++)
            {
                int c = text[i];
                if (c == Replacement)
                {
                    replacements.Add(Context(text, i));
                }
                else if (c >= C1First && c <= C1Last)
                {
                    controls.Add(Hex(c) + " near " + Context(text, i));
                }

                if (IsDoubleEncodedAt(text, i))
                {
                    mangled.Add(
                        Hex(text[i]) + " " + Hex(text[i + 1]) + " near " + Context(text, i)
                    );
                }
            }

            foreach (string where in replacements)
            {
                problems.Add("carries a replacement character (U+FFFD) near " + where);
            }

            foreach (string where in controls)
            {
                problems.Add("carries a C1 control " + where);
            }

            foreach (string where in mangled)
            {
                problems.Add("looks doubly encoded: " + where);
            }

            return problems;
        }

        /// <summary>
        /// Keys the mod does not speak. A paucal key is legitimate only where the language has a
        /// paucal form at all, so a stray <c>.few</c> in a French file is reported rather than
        /// quietly ignored - it is a phrase a translator wrote that will never be heard.
        /// </summary>
        public static IList<string> UnknownKeys(
            IEnumerable<string> keys,
            ICollection<string> defaultKeys,
            bool allowsPaucal
        )
        {
            List<string> problems = new List<string>();
            foreach (string key in keys)
            {
                if (defaultKeys.Contains(key))
                {
                    continue;
                }

                if (IsPaucal(key) && defaultKeys.Contains(BaseKey(key)))
                {
                    if (!allowsPaucal)
                    {
                        problems.Add(
                            "'" + key + "': a paucal form, which this language has no rule for"
                        );
                    }

                    continue;
                }

                problems.Add("unknown key '" + key + "'");
            }

            return problems;
        }

        /// <summary>Keys the mod speaks that the file does not answer for.</summary>
        public static IList<string> MissingKeys(
            IEnumerable<string> required,
            ICollection<string> present
        )
        {
            List<string> problems = new List<string>();
            foreach (string key in required)
            {
                if (!present.Contains(key))
                {
                    problems.Add("missing key '" + key + "'");
                }
            }

            return problems;
        }

        /// <summary>
        /// Plural pairs whose paucal form is absent. Without it a three-form language falls back to
        /// the MANY sentence for 2, 3 and 4 (<see cref="ModStrings.Plural"/>), which is
        /// grammatically wrong rather than silent - a file the mod can still ship, and a defect the
        /// translator has to be told about.
        /// </summary>
        public static IList<string> MissingPaucals(
            IEnumerable<string> manyKeys,
            ICollection<string> present
        )
        {
            List<string> problems = new List<string>();
            foreach (string manyKey in manyKeys)
            {
                if (!present.Contains(manyKey + FewSuffix))
                {
                    problems.Add("missing paucal form '" + manyKey + FewSuffix + "'");
                }
            }

            return problems;
        }

        /// <summary>
        /// Entries whose <c>{n}</c> placeholders differ from the English template's. A paucal form
        /// is compared against its pair's English sentence, which is what it was written from.
        /// </summary>
        public static IList<string> PlaceholderMismatches(
            IDictionary<string, string> table,
            IDictionary<string, string> english
        )
        {
            List<string> problems = new List<string>();
            foreach (KeyValuePair<string, string> entry in table)
            {
                string source;
                if (!english.TryGetValue(BaseKey(entry.Key), out source))
                {
                    continue;
                }

                string expected = Join(Placeholders(source));
                string actual = Join(Placeholders(entry.Value));
                if (expected != actual)
                {
                    problems.Add(
                        "'"
                            + entry.Key
                            + "': English takes "
                            + expected
                            + ", translation takes "
                            + actual
                    );
                }
            }

            return problems;
        }

        /// <summary>
        /// Entries that were never translated, spotted without knowing the language: a value
        /// identical to its English source. Short entries are exempt, because a keyboard key name,
        /// a unit or a proper noun is legitimately the same in both, and the threshold is on the
        /// ENGLISH side so a language that renders three English words as one is not accused.
        /// </summary>
        public static IList<string> UntranslatedEntries(
            IDictionary<string, string> table,
            IDictionary<string, string> english
        )
        {
            List<string> problems = new List<string>();
            foreach (KeyValuePair<string, string> entry in table)
            {
                string source;
                if (!english.TryGetValue(BaseKey(entry.Key), out source))
                {
                    continue;
                }

                if (
                    Words(source) >= 3
                    && string.Equals(entry.Value, source, StringComparison.Ordinal)
                )
                {
                    problems.Add("'" + entry.Key + "' is still the English text: " + source);
                }
            }

            return problems;
        }

        /// <summary>
        /// Entries in a non-Latin language carrying no letter of that language's own script.
        ///
        /// Two rules, because neither alone is honest. Per entry: anything whose English runs to
        /// three words or more is prose, and prose with no native letter in it was not translated.
        /// Across the file: at least four fifths of the entries with any real word in them must be
        /// in the native script, which catches a file translated in patches. The remainder is
        /// deliberate slack - "Ctrl", "F1", a faction name and a bare number are all correct
        /// answers in Russian, Korean and Chinese alike.
        /// </summary>
        public static IList<string> ScriptProblems(
            IDictionary<string, string> table,
            IDictionary<string, string> english,
            NativeScript script
        )
        {
            List<string> problems = new List<string>();
            if (script == NativeScript.None)
            {
                return problems;
            }

            int considered = 0;
            int native = 0;
            foreach (KeyValuePair<string, string> entry in table)
            {
                string source;
                if (!english.TryGetValue(BaseKey(entry.Key), out source))
                {
                    continue;
                }

                bool isNative = HasScript(entry.Value, script);
                if (Words(source) >= 3 && !isNative)
                {
                    problems.Add("'" + entry.Key + "' has no " + script + " in it: " + entry.Value);
                }

                if (Letters(source) >= 2)
                {
                    considered++;
                    if (isNative)
                    {
                        native++;
                    }
                }
            }

            if (considered > 0 && native * 100 < considered * 80)
            {
                problems.Add(
                    "only "
                        + native
                        + " of "
                        + considered
                        + " worded entries are in "
                        + script
                        + "; at least 80 percent must be"
                );
            }

            return problems;
        }

        /// <summary>
        /// What the snapshot beside a translation says about its freshness: it must record exactly
        /// the keys the translation answers, and every English text it records must still be the
        /// English text today. A key whose English was rewritten since is STALE - the translation is
        /// still a sentence, so nothing at runtime can notice, which is the whole reason the
        /// snapshot exists.
        /// </summary>
        public static IList<string> SnapshotProblems(
            IDictionary<string, string> snapshot,
            IDictionary<string, string> translation,
            IDictionary<string, string> english
        )
        {
            List<string> problems = new List<string>();
            foreach (KeyValuePair<string, string> entry in translation)
            {
                if (!snapshot.ContainsKey(entry.Key))
                {
                    problems.Add(
                        "'" + entry.Key + "' has no record of the English it was translated from"
                    );
                }
            }

            foreach (KeyValuePair<string, string> entry in snapshot)
            {
                if (!translation.ContainsKey(entry.Key))
                {
                    problems.Add("'" + entry.Key + "' is recorded but not translated");
                    continue;
                }

                string current;
                if (!english.TryGetValue(BaseKey(entry.Key), out current))
                {
                    continue;
                }

                if (!string.Equals(entry.Value, current, StringComparison.Ordinal))
                {
                    problems.Add(
                        "'"
                            + entry.Key
                            + "' is stale: translated from \""
                            + entry.Value
                            + "\", English now reads \""
                            + current
                            + "\""
                    );
                }
            }

            return problems;
        }

        /// <summary>
        /// A description table's shape against the English one: the same videos, the same number of
        /// cues in each, and each cue at the same place in the video. The timings belong to the
        /// footage rather than to the language, so a translation that moved one has either dropped a
        /// cue or invented one.
        /// </summary>
        public static IList<string> DescriptionShapeProblems(
            IDictionary<string, IList<CueRow>> table,
            IDictionary<string, IList<CueRow>> english
        )
        {
            List<string> problems = new List<string>();
            foreach (KeyValuePair<string, IList<CueRow>> entry in english)
            {
                if (!table.ContainsKey(entry.Key))
                {
                    problems.Add("'" + entry.Key + "' is described in English and not here");
                }
            }

            foreach (KeyValuePair<string, IList<CueRow>> entry in table)
            {
                IList<CueRow> source;
                if (!english.TryGetValue(entry.Key, out source))
                {
                    problems.Add("'" + entry.Key + "' is not a video English describes");
                    continue;
                }

                if (entry.Value.Count != source.Count)
                {
                    problems.Add(
                        "'"
                            + entry.Key
                            + "' has "
                            + entry.Value.Count
                            + " cues, English has "
                            + source.Count
                    );
                    continue;
                }

                for (int i = 0; i < entry.Value.Count; i++)
                {
                    // Equals rather than ==, so a cue with no timing at all compares equal to the
                    // English one that also has none, and is left to CueProblems to complain about.
                    if (
                        !entry.Value[i].At.Equals(source[i].At)
                        || !entry.Value[i].End.Equals(source[i].End)
                    )
                    {
                        problems.Add(
                            "'"
                                + entry.Key
                                + "' cue "
                                + i
                                + " runs "
                                + Number(entry.Value[i].At)
                                + "-"
                                + Number(entry.Value[i].End)
                                + ", English runs "
                                + Number(source[i].At)
                                + "-"
                                + Number(source[i].End)
                        );
                    }
                }
            }

            return problems;
        }

        /// <summary>
        /// What is wrong with the cues themselves, in any language including English: an empty line
        /// says nothing, and cues are handed out in file order by <see cref="DescriptionTrack"/>, so
        /// one that starts before the cue above it is spoken at the wrong moment and swallows the
        /// one it jumped.
        /// </summary>
        public static IList<string> CueProblems(IDictionary<string, IList<CueRow>> table)
        {
            List<string> problems = new List<string>();
            foreach (KeyValuePair<string, IList<CueRow>> entry in table)
            {
                double previous = double.NegativeInfinity;
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    CueRow cue = entry.Value[i];
                    if (cue.Text == null || cue.Text.Trim().Length == 0)
                    {
                        problems.Add("'" + entry.Key + "' cue " + i + " has no text");
                    }

                    if (cue.At < previous)
                    {
                        problems.Add(
                            "'"
                                + entry.Key
                                + "' cue "
                                + i
                                + " starts at "
                                + Number(cue.At)
                                + ", after a cue at "
                                + Number(previous)
                        );
                    }

                    previous = cue.At;
                }
            }

            return problems;
        }

        /// <summary>
        /// A description table as a flat key-to-text map ("Arctic[0]"), so the checks written for the
        /// string table - script, untranslated, staleness - apply to cue prose unchanged. Only
        /// meaningful once <see cref="DescriptionShapeProblems"/> is clean, since it is the shape
        /// that makes the two tables' keys line up.
        /// </summary>
        public static IDictionary<string, string> Flatten(IDictionary<string, IList<CueRow>> table)
        {
            Dictionary<string, string> flat = new Dictionary<string, string>();
            foreach (KeyValuePair<string, IList<CueRow>> entry in table)
            {
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    flat[entry.Key + "[" + i + "]"] = entry.Value[i].Text ?? string.Empty;
                }
            }

            return flat;
        }

        /// <summary>The English text a description snapshot records, flattened the same way, so
        /// <see cref="SnapshotProblems"/> reads it too.</summary>
        public static IDictionary<string, string> Flatten(
            IDictionary<string, IList<string>> snapshot
        )
        {
            Dictionary<string, string> flat = new Dictionary<string, string>();
            foreach (KeyValuePair<string, IList<string>> entry in snapshot)
            {
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    flat[entry.Key + "[" + i + "]"] = entry.Value[i] ?? string.Empty;
                }
            }

            return flat;
        }

        /// <summary>The key a paucal form belongs to, or the key itself.</summary>
        public static string BaseKey(string key)
        {
            return IsPaucal(key) ? key.Substring(0, key.Length - FewSuffix.Length) : key;
        }

        public static bool IsPaucal(string key)
        {
            return key != null
                && key.Length > FewSuffix.Length
                && key.EndsWith(FewSuffix, StringComparison.Ordinal);
        }

        /// <summary>Whether <paramref name="text"/> contains a letter of <paramref name="script"/>.</summary>
        public static bool HasScript(string text, NativeScript script)
        {
            for (int i = 0; text != null && i < text.Length; i++)
            {
                int c = text[i];
                switch (script)
                {
                    case NativeScript.Cyrillic:
                        // Cyrillic (U+0400) through the end of the supplement block (U+052F).
                        if (c >= 0x0400 && c <= 0x052F)
                        {
                            return true;
                        }

                        break;

                    case NativeScript.Hangul:
                        // Syllables, then conjoining jamo, then the compatibility jamo block.
                        if (
                            (c >= 0xAC00 && c <= 0xD7A3)
                            || (c >= 0x1100 && c <= 0x11FF)
                            || (c >= 0x3130 && c <= 0x318F)
                        )
                        {
                            return true;
                        }

                        break;

                    case NativeScript.Han:
                        // CJK Unified Ideographs, then extension A. Both Chinese locales draw on the
                        // same blocks; this cannot tell simplified from traditional and does not try.
                        if ((c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF))
                        {
                            return true;
                        }

                        break;
                }
            }

            return false;
        }

        /// <summary>How many of a phrase's whitespace-separated tokens carry a letter. A number or a
        /// bare placeholder is not a word a translator has to render.</summary>
        public static int Words(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int words = 0;
            foreach (
                string token in text.Split(
                    new[] { ' ', '\t', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                if (Letters(token) > 0)
                {
                    words++;
                }
            }

            return words;
        }

        public static int Letters(string text)
        {
            int letters = 0;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                if (char.IsLetter(text[i]))
                {
                    letters++;
                }
            }

            return letters;
        }

        // A double encoding leaves the original lead byte as its own Latin-1 character - the four
        // that start Latin and Cyrillic text - followed by the continuation byte's, always in
        // U+0080..U+00BF. Written as a scan rather than a regex so this file needs no non-ASCII
        // character of its own.
        private static bool IsDoubleEncodedAt(string text, int i)
        {
            if (i + 1 >= text.Length)
            {
                return false;
            }

            int lead = text[i];
            int next = text[i + 1];
            return (lead == 0x00C3 || lead == 0x00C2 || lead == 0x00D0 || lead == 0x00D1)
                && next >= 0x0080
                && next <= 0x00BF;
        }

        private static SortedSet<string> Placeholders(string template)
        {
            SortedSet<string> indexes = new SortedSet<string>();
            foreach (Match match in Placeholder.Matches(template ?? string.Empty))
            {
                indexes.Add(match.Groups[1].Value);
            }

            return indexes;
        }

        private static string Join(SortedSet<string> indexes)
        {
            if (indexes.Count == 0)
            {
                return "no placeholders";
            }

            StringBuilder text = new StringBuilder();
            foreach (string index in indexes)
            {
                if (text.Length > 0)
                {
                    text.Append(", ");
                }

                text.Append("{").Append(index).Append("}");
            }

            return text.ToString();
        }

        // Enough of the line around an offending byte to find it by eye, with the offender's own
        // neighbours intact - a message that only gave the index would send a translator counting
        // characters.
        private static string Context(string text, int index)
        {
            int start = Math.Max(0, index - 20);
            int end = Math.Min(text.Length, index + 20);
            return "\"" + text.Substring(start, end - start).Replace("\n", " ") + "\"";
        }

        private static string Hex(int c)
        {
            return "U+" + c.ToString("X4", CultureInfo.InvariantCulture);
        }

        private static string Number(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

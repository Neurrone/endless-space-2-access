using System;
using System.Collections.Generic;
using System.Text;

namespace ES2Access.Core.Settings
{
    /// <summary>
    /// The mod's settings as a file of <c>key = value</c> lines, parsed and written back without
    /// losing anything it did not understand.
    ///
    /// It keeps the file's LINES, not a dictionary: a comment, a blank line, and a key this build
    /// has never heard of all survive a load/save round trip in the place the player (or an older
    /// build of the mod) put them. That is what makes it safe to write the file on every window
    /// close, and what stops a downgrade from silently deleting the newer build's settings.
    ///
    /// Values are stored verbatim where they are ordinary text and QUOTED where they are not - a
    /// value with leading or trailing spaces, an embedded newline, a quote or a backslash - with
    /// C-style escapes (<c>\\ \" \n \r \t</c>) inside the quotes. Reading accepts either form.
    ///
    /// Where a key appears more than once the LAST line wins, for reading and for writing alike, so
    /// a hand-edited file reads the way a player would expect and a save cannot end up with two
    /// answers.
    ///
    /// A file may also carry the mod's own HEADER COMMENT on its first line
    /// (<see cref="SetHeaderComment"/>) - a sentence for whoever opens the file, rewritten on every
    /// write. It lives here rather than in the callers because this class owns the file's lines: a
    /// caller that inserted its own first line would be writing this format from outside it.
    ///
    /// Deliberately BCL-only: it is the half of the settings store that can be unit-tested off the
    /// engine. The file itself is somebody else's job (<c>ES2Access.UI.Settings.ModSettings</c>).
    /// </summary>
    public sealed class SettingsFile
    {
        private readonly List<string> _lines = new List<string>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(
            StringComparer.Ordinal
        );

        /// <summary>An empty file - what a first run starts from.</summary>
        public SettingsFile() { }

        /// <summary>Read a file that already exists. Anything that is not a <c>key = value</c> line
        /// is kept as it was and ignored.</summary>
        public static SettingsFile Parse(IEnumerable<string> lines)
        {
            SettingsFile file = new SettingsFile();
            if (lines == null)
            {
                return file;
            }

            foreach (string line in lines)
            {
                string text = line ?? string.Empty;
                file._lines.Add(text);

                string key = KeyOf(text);
                if (key != null)
                {
                    file._index[key] = file._lines.Count - 1;
                }
            }

            return file;
        }

        /// <summary>The whole file, ready to be written out one line each.</summary>
        public IList<string> ToLines()
        {
            return new List<string>(_lines);
        }

        /// <summary>Whether the file says anything about <paramref name="key"/>.</summary>
        public bool Has(string key)
        {
            return key != null && _index.ContainsKey(key);
        }

        /// <summary>What the file says <paramref name="key"/> is, or null where it says nothing.
        /// An empty value is a value: it reads as the empty string, not as absent.</summary>
        public string Get(string key)
        {
            int at;
            if (key == null || !_index.TryGetValue(key, out at))
            {
                return null;
            }

            return ValueOf(_lines[at]);
        }

        /// <summary>Write a value, in place where the key is already there and at the end where it
        /// is not. A null value removes the key, which is how a setting goes back to its default.
        /// </summary>
        public void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key) || KeyOf(key + " = x") != key)
            {
                throw new ArgumentException("not a usable settings key: '" + key + "'", "key");
            }

            if (value == null)
            {
                Remove(key);
                return;
            }

            string line = key + " = " + Encode(value);
            int at;
            if (_index.TryGetValue(key, out at))
            {
                _lines[at] = line;
                return;
            }

            _lines.Add(line);
            _index[key] = _lines.Count - 1;
        }

        /// <summary>Forget what the file said about <paramref name="key"/> - every line of it, so a
        /// hand-edited duplicate cannot come back on the next read.</summary>
        public void Remove(string key)
        {
            if (key == null || !_index.ContainsKey(key))
            {
                return;
            }

            List<string> kept = new List<string>();
            for (int i = 0; i < _lines.Count; i++)
            {
                if (KeyOf(_lines[i]) != key)
                {
                    kept.Add(_lines[i]);
                }
            }

            _lines.Clear();
            _lines.AddRange(kept);
            Reindex();
        }

        /// <summary>
        /// The mark that says a first-line comment is the MOD's header and not the player's own.
        ///
        /// A bare <c>#</c> could not tell them apart, and a header refreshed on every write would
        /// eventually eat a note somebody had written at the top of their own file. <c>#!</c> is the
        /// one shape nobody types by accident, it is still a comment to every reader of this format
        /// (<see cref="KeyOf"/> ignores it exactly as it ignores <c>#</c>), and it needs no word in
        /// it - so the header's TEXT stays a translated sentence with no file syntax in it.
        /// </summary>
        private const string HeaderMark = "#!";

        /// <summary>
        /// The mod's own header line, without its mark - null where the file has none.
        ///
        /// Only the FIRST line is ever the header: a file's header is where a person looks first, and
        /// a mark found further down is somebody else's line, left alone.
        /// </summary>
        public string HeaderComment
        {
            get
            {
                if (_lines.Count == 0 || !IsHeader(_lines[0]))
                {
                    return null;
                }

                return _lines[0].Substring(HeaderMark.Length).Trim();
            }
        }

        /// <summary>
        /// Write the mod's header comment at the top of the file - replacing the one that is there,
        /// or putting the first one in.
        ///
        /// It is REFRESHED rather than appended, so a file written a hundred times has one header and
        /// not a hundred. A first line that is a comment of the PLAYER's - anything not carrying
        /// <see cref="HeaderMark"/> - is not touched: the header goes in above it and their note stays
        /// where they put it.
        ///
        /// A null or blank text takes the mod's header out again and leaves everything else alone.
        /// Line breaks in the text would end the comment and turn the rest into rubbish the parser
        /// would keep forever, so they are folded to spaces here rather than trusted to the caller.
        /// </summary>
        public void SetHeaderComment(string text)
        {
            string line = OneLine(text);
            bool has = _lines.Count > 0 && IsHeader(_lines[0]);
            if (line == null)
            {
                if (has)
                {
                    _lines.RemoveAt(0);
                    Reindex();
                }

                return;
            }

            if (has)
            {
                _lines[0] = HeaderMark + " " + line;
                return;
            }

            _lines.Insert(0, HeaderMark + " " + line);
            Reindex();
        }

        private static bool IsHeader(string line)
        {
            return line != null && line.StartsWith(HeaderMark, StringComparison.Ordinal);
        }

        /// <summary>The header's text as one line, or null for nothing to say.</summary>
        private static string OneLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            StringBuilder flat = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                flat.Append(c == '\n' || c == '\r' ? ' ' : c);
            }

            string line = flat.ToString().Trim();
            return line.Length == 0 ? null : line;
        }

        /// <summary>The keys the file holds, in the order it holds them.</summary>
        public IList<string> Keys
        {
            get
            {
                List<string> keys = new List<string>();
                for (int i = 0; i < _lines.Count; i++)
                {
                    string key = KeyOf(_lines[i]);
                    if (key != null && !keys.Contains(key))
                    {
                        keys.Add(key);
                    }
                }

                return keys;
            }
        }

        private void Reindex()
        {
            _index.Clear();
            for (int i = 0; i < _lines.Count; i++)
            {
                string key = KeyOf(_lines[i]);
                if (key != null)
                {
                    _index[key] = i;
                }
            }
        }

        /// <summary>The key a line declares, or null where the line is a comment, blank, or has no
        /// separator at all. A key may not be empty and may not start with the comment mark, so a
        /// commented-out setting stays commented out.</summary>
        private static string KeyOf(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            int separator = line.IndexOf('=');
            if (separator < 0)
            {
                return null;
            }

            string key = line.Substring(0, separator).Trim();
            if (key.Length == 0 || key[0] == '#' || key[0] == ';')
            {
                return null;
            }

            return key;
        }

        private static string ValueOf(string line)
        {
            int separator = line.IndexOf('=');
            string raw = line.Substring(separator + 1).Trim();
            return raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"'
                ? Decode(raw.Substring(1, raw.Length - 2))
                : raw;
        }

        /// <summary>A value as it goes into the file: bare where bare would read back unchanged,
        /// quoted and escaped where it would not.</summary>
        private static string Encode(string value)
        {
            bool plain = value.Length == 0 || (value == value.Trim() && value[0] != '"');
            if (plain)
            {
                for (int i = 0; i < value.Length && plain; i++)
                {
                    char c = value[i];
                    plain = c != '\\' && c != '"' && c != '\n' && c != '\r' && c != '\t';
                }
            }

            if (plain)
            {
                return value;
            }

            StringBuilder quoted = new StringBuilder(value.Length + 2);
            quoted.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        quoted.Append("\\\\");
                        break;
                    case '"':
                        quoted.Append("\\\"");
                        break;
                    case '\n':
                        quoted.Append("\\n");
                        break;
                    case '\r':
                        quoted.Append("\\r");
                        break;
                    case '\t':
                        quoted.Append("\\t");
                        break;
                    default:
                        quoted.Append(c);
                        break;
                }
            }

            return quoted.Append('"').ToString();
        }

        private static string Decode(string value)
        {
            StringBuilder plain = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c != '\\' || i + 1 >= value.Length)
                {
                    plain.Append(c);
                    continue;
                }

                char next = value[++i];
                switch (next)
                {
                    case 'n':
                        plain.Append('\n');
                        break;
                    case 'r':
                        plain.Append('\r');
                        break;
                    case 't':
                        plain.Append('\t');
                        break;
                    default:
                        // Anything else after a backslash is that character - which is what makes
                        // \\ and \" work without a table of their own.
                        plain.Append(next);
                        break;
                }
            }

            return plain.ToString();
        }
    }
}

using System.Collections.Generic;
using System.Text;
using ES2Access.Core.Util;

namespace ES2Access.Core.Speech
{
    /// <summary>One thing a tooltip drew, once its words have been read out of the widget.</summary>
    public struct TooltipPart
    {
        public string Text;

        /// <summary>Set when the text is the name of a PICTURE rather than words the panel wrote. It
        /// is the same text either way, but it earns its place in the line differently - see
        /// <see cref="TooltipText.AddRow"/>.</summary>
        public bool Icon;

        /// <summary>Set for a part that is a whole fact of its own and only LANDED in this row. A
        /// panel that draws a caption on one line and its value on the next leaves the value sharing
        /// a row with something unrelated, and a typed reader that has put the caption back on to it
        /// says so here rather than letting the row glue two facts into one sentence.</summary>
        public bool OwnLine;

        public TooltipPart(string text, bool icon)
            : this(text, icon, false) { }

        public TooltipPart(string text, bool icon, bool ownLine)
        {
            Text = text;
            Icon = icon;
            OwnLine = ownLine;
        }
    }

    /// <summary>
    /// Turning what a tooltip drew into the lines a review buffer walks.
    ///
    /// Measuring where the words are is the engine's job (rectangles, panels, visibility); deciding
    /// what a row of them SAYS is not, and it is the half worth testing off-engine - which picture
    /// completes a sentence and which one is decoration, when a caption and its value are one fact
    /// rather than two, and where a paragraph the window wrapped keeps its own breaks.
    /// </summary>
    public static class TooltipText
    {
        /// <summary>
        /// One drawn row appended to <paramref name="lines"/>, or nothing when the row said nothing.
        ///
        /// The join is a plain space, on purpose: a caption and its value are prose the panel laid
        /// out across two widgets instead of one, not a list of separate facts, so
        /// <see cref="ModStrings.ListSeparator"/> would read a false pause into the middle of a
        /// sentence. A part whose OWN text still holds an embedded newline - a paragraph the window
        /// wrapped at its own width - keeps that break, which is what lets "one label, several
        /// physical lines" and "several labels, one shared line" share this one path.
        ///
        /// A part marked <see cref="TooltipPart.OwnLine"/> breaks the row where it sits, because it is
        /// not part of the sentence the rest of the row is: a hero's level is drawn under its own
        /// caption and lands in the row belonging to their class, and "Counselor Level 1" is a rank
        /// nobody has.
        /// </summary>
        public static void AddRow(IList<string> lines, IList<TooltipPart> row)
        {
            int start = 0;
            List<TooltipPart> run = new List<TooltipPart>();
            for (int i = 0; i < row.Count; i++)
            {
                if (!row[i].OwnLine)
                {
                    continue;
                }

                AddSegment(lines, Segment(row, start, i, run));
                AddSegment(lines, Segment(row, i, i + 1, run));
                start = i + 1;
            }

            AddSegment(lines, Segment(row, start, row.Count, run));
        }

        private static List<TooltipPart> Segment(
            IList<TooltipPart> row,
            int from,
            int to,
            List<TooltipPart> run
        )
        {
            run.Clear();
            for (int i = from; i < to; i++)
            {
                run.Add(row[i]);
            }

            return run;
        }

        /// <summary>One spoken line's worth of parts - a whole drawn row, or the part of one that
        /// belongs together.</summary>
        private static void AddSegment(IList<string> lines, IList<TooltipPart> row)
        {
            string words = Words(row);
            if (words.Length == 0)
            {
                return;
            }

            StringBuilder combined = new StringBuilder();
            for (int i = 0; i < row.Count; i++)
            {
                TooltipPart cell = row[i];
                if (string.IsNullOrEmpty(cell.Text) || (cell.Icon && Says(words, cell.Text)))
                {
                    continue;
                }

                if (combined.Length > 0)
                {
                    combined.Append(' ');
                }

                combined.Append(cell.Text);
            }

            AddLines(lines, combined.ToString());
        }

        /// <summary>A caption and the value drawn beside it as the one fact they are. Null when there
        /// is no value: a stat the panel left blank is not a stat with an empty answer.</summary>
        public static string Captioned(string caption, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return string.IsNullOrEmpty(caption) ? value.Trim() : caption.Trim() + " " + value.Trim();
        }

        /// <summary>The repeated items of one strip as the single line they are drawn as - three range
        /// bands across a row are one fact with three parts, not three facts.</summary>
        public static string Items(IList<string> items)
        {
            return SpokenList.Items(items);
        }

        /// <summary>The parts of one item joined the way the item is drawn: a picture, a caption and a
        /// number laid out in a little block are one phrase.</summary>
        public static string Phrase(IList<string> parts)
        {
            MessageBuilder message = new MessageBuilder();
            for (int i = 0; parts != null && i < parts.Count; i++)
            {
                message.Fragment(parts[i]);
            }

            return message.Build();
        }

        /// <summary>Split on the breaks a wrapped paragraph carries, dropping the blanks.</summary>
        public static void AddLines(IList<string> lines, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    lines.Add(trimmed);
                }
            }
        }

        /// <summary>
        /// The row's actual words, and the reason a row with none of them is dropped whole.
        ///
        /// A picture completes a sentence or it illustrates one. The five icons of a stat strip are
        /// read because the numbers beside them are unreadable without them; the star portrait at the
        /// top of a system's tooltip is on its own line with nothing to complete, and announcing "Blue
        /// Star" for it puts a line into the reading that the panel never wrote - just above the line
        /// where the panel does say, in words, "Star System (Blue Star)".
        ///
        /// Sharing the line with words is therefore the whole test, and it needs no threshold on how
        /// big a picture has to be before it stops being an icon.
        /// </summary>
        private static string Words(IList<TooltipPart> row)
        {
            StringBuilder said = new StringBuilder();
            for (int i = 0; i < row.Count; i++)
            {
                if (!row[i].Icon && !string.IsNullOrEmpty(row[i].Text))
                {
                    said.Append(row[i].Text).Append(' ');
                }
            }

            return said.ToString();
        }

        /// <summary>Whether the row's own words already say what an icon on it is called - the header
        /// symbol beside the heading "Star System (Blue Star)" is named "System", and reading both
        /// gives "System Star System (Blue Star)".</summary>
        private static bool Says(string words, string name)
        {
            string key = TextUtil.LettersAndDigits(name);
            return key.Length > 0 && TextUtil.LettersAndDigits(words).IndexOf(key) >= 0;
        }
    }
}

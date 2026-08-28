using System;
using System.Collections.Generic;
using ES2Access.Core.Util;

namespace ES2Access.Core.UI.Graph
{
    /// <summary>
    /// Whether a tooltip's words REACHED the focus readout, and whether words that were never meant to
    /// be in it are.
    ///
    /// The rule this measures is <see cref="TooltipParts"/>' own, read back off the result: a tooltip
    /// the game carries WORDS for is announced whole when focus lands, and a tooltip the game
    /// ASSEMBLES on hover reaches the player through the review buffer alone. The projection makes that
    /// true by construction; this asks whether it came out true, which is a different question the
    /// moment anything composes a readout by hand.
    ///
    /// Two asymmetries decide the shape of both answers:
    ///
    /// - A line the readout ALREADY SAYS is dropped from the tooltip part on purpose (a control named
    ///   after the first line of its own tooltip would otherwise say it twice). So "did the tooltip
    ///   reach the readout" must be asked of the WHOLE readout, never of the tooltip part alone: a
    ///   deduped line is still there, in the label's voice, and asking the part would report every
    ///   such control as a defect.
    /// - The other direction has no such alibi, but it does have a coincidence problem: a caption is a
    ///   short word, and short words turn up in readouts by accident. So a leak is only claimed for a
    ///   SUBSTANTIAL line (<see cref="Substantial"/>), and never for one the readout says as a part of
    ///   its own - a control whose drawn name happens to be the tooltip's first line is naming itself,
    ///   not leaking.
    ///
    /// Both comparisons are on letters and digits alone (<see cref="TextUtil.LettersAndDigits"/>), the
    /// same reduction the audits match painted text with: markup, punctuation and case are differences
    /// that must not hide the words.
    /// </summary>
    public static class TooltipClassRule
    {
        /// <summary>How many letters and digits a line needs before its presence in a readout is
        /// evidence of anything. A resource caption ("Food", "12/20") turns up inside unrelated
        /// sentences all day; a sentence out of a hover panel does not.</summary>
        public const int Substantial = 12;

        /// <summary>
        /// The first line of <paramref name="lines"/> the readout does not say, or null when the readout
        /// accounts for all of them.
        ///
        /// Asked of a tooltip whose words the game CARRIES, about a node that announces no tooltip part
        /// at all: every line of it should be somewhere in the readout, either in the tooltip part or -
        /// for the line the label was read off - in the label. A line with no letters or digits in it
        /// (a rule, a spacer) is not owed a reading.
        /// </summary>
        public static string Unspoken(IList<string> lines, string readout)
        {
            string said = TextUtil.LettersAndDigits(readout);
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                string want = TextUtil.LettersAndDigits(lines[i]);
                if (want.Length == 0)
                {
                    continue;
                }

                if (said.IndexOf(want, StringComparison.Ordinal) < 0)
                {
                    return lines[i];
                }
            }

            return null;
        }

        /// <summary>
        /// The first line of <paramref name="lines"/> the readout is saying anyway, or null.
        ///
        /// Asked of a tooltip the game ASSEMBLES on hover - words that belong in the review buffer and
        /// nowhere else. <paramref name="alsoSpoken"/> is the readout's own parts other than the
        /// tooltip's: a line equal to one of them is that part's own words (an icon whose drawn name is
        /// the panel's first line), which is a control naming itself and not a class-backed tooltip
        /// getting into the readout.
        /// </summary>
        public static string Leaked(IList<string> lines, string readout, IList<string> alsoSpoken)
        {
            string said = TextUtil.LettersAndDigits(readout);
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                string line = lines[i];
                string want = TextUtil.LettersAndDigits(line);
                if (want.Length < Substantial)
                {
                    continue;
                }

                if (said.IndexOf(want, StringComparison.Ordinal) >= 0 && !Own(alsoSpoken, line))
                {
                    return line;
                }
            }

            return null;
        }

        private static bool Own(IList<string> alsoSpoken, string line)
        {
            for (int i = 0; alsoSpoken != null && i < alsoSpoken.Count; i++)
            {
                if (TextUtil.IsBlank(alsoSpoken[i]))
                {
                    continue;
                }

                if (
                    string.Equals(
                        alsoSpoken[i].Trim(),
                        line == null ? null : line.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }
    }
}

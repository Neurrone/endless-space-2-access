using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// The sentence a game gives for refusing something, pulled out of the tooltip it wrote it into.
    ///
    /// Games rarely say "no" on their own line. A button the game has disabled - or, worse, left
    /// clickable and turned into a signpost - carries a tooltip assembled from parts: what the button
    /// would do, why it will not do it, and often an instruction aimed squarely at a mouse ("hold
    /// Control and click to find the technology"). Only the middle part is the refusal, and it is the
    /// only part worth putting in front of a player who asked for the action and did not get it: the
    /// first part repeats the name of the thing they just chose, and the last is advice they cannot
    /// take.
    ///
    /// The shape is the same wherever this pattern appears, so the trimming lives here rather than in
    /// one screen: drop the leading description when the game had something to append to it, drop any
    /// line that is the pointing-device instruction verbatim, and read what is left as one phrase.
    /// </summary>
    public static class RefusalText
    {
        /// <summary>
        /// <paramref name="lines"/> is the tooltip as drawn, in order.
        /// <paramref name="mouseInstruction"/> is the game's own localized "click here to go there"
        /// sentence, or null where the game has none. Null comes back when nothing is left, which
        /// means the tooltip was only ever a description and the caller has no refusal to report.
        /// </summary>
        public static string Compose(IList<string> lines, string mouseInstruction)
        {
            if (lines == null || lines.Count == 0)
            {
                return null;
            }

            // A single line was never joined to anything, so it IS the whole of what the game said.
            int first = lines.Count > 1 ? 1 : 0;
            MessageBuilder message = new MessageBuilder();
            for (int i = first; i < lines.Count; i++)
            {
                string line = lines[i] == null ? null : lines[i].Trim();
                if (string.IsNullOrEmpty(line) || Same(line, mouseInstruction))
                {
                    continue;
                }

                message.Fragment(line);
            }

            string text = message.Build();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static bool Same(string line, string instruction)
        {
            return !string.IsNullOrEmpty(instruction)
                && string.Equals(line, instruction.Trim());
        }
    }
}

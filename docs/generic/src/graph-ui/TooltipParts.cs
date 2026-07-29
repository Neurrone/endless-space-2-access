using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.Core.UI.Graph
{
    /// <summary>
    /// How a control's tooltip reaches the player when focus lands on it. The choice belongs to the
    /// screen that declares the control, not to a length heuristic: only the author knows whether a
    /// tooltip is the one sentence that explains the control (say it) or a stat block the player will
    /// want to walk at their own pace (mention that it is there).
    ///
    /// Either way the tooltip's full text still feeds the review buffer, so nothing is ever only
    /// available by hearing it go past.
    /// </summary>
    public enum TooltipMode
    {
        /// <summary>The tooltip does not enter the focus readout at all.</summary>
        None,

        /// <summary>Its text is spoken as part of the readout.</summary>
        Announce,

        /// <summary>A short "has tooltip" is spoken in place of the text.</summary>
        Indicate,
    }

    /// <summary>Builds the tooltip announcement part a <see cref="TooltipMode"/> asks for. Kept beside
    /// the graph types rather than in a game adapter so the wording and the empty-tooltip rule are the
    /// same on every screen and testable without the game.</summary>
    public static class TooltipParts
    {
        /// <summary>The tooltip part for <paramref name="mode"/>, reading <paramref name="lines"/> at
        /// speak time - a control that is refusing appends its reason to its own tooltip, and the
        /// reason it gives has to be the one it would give now. Null when the mode is
        /// <see cref="TooltipMode.None"/> or there is no tooltip to read; a tooltip that resolves
        /// empty stays silent in both modes, so "has tooltip" never promises an empty buffer.</summary>
        public static NodeAnnouncement Part(TooltipMode mode, Func<IList<string>> lines)
        {
            if (mode == TooltipMode.None || lines == null)
            {
                return null;
            }

            bool announce = mode == TooltipMode.Announce;
            return new NodeAnnouncement(
                () => announce ? Spoken(lines()) : Indication(lines()),
                kind: AnnouncementKinds.Tooltip
            );
        }

        // A tooltip is written as lines - a name, a description, a reason - and read out as one
        // sentence, so the lines join the way any other list of parts does.
        private static string Spoken(IList<string> lines)
        {
            if (lines == null)
            {
                return null;
            }

            MessageBuilder message = new MessageBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (!TextUtil.IsBlank(lines[i]))
                {
                    message.ListItem(lines[i].Trim());
                }
            }

            return message.Build();
        }

        private static string Indication(IList<string> lines)
        {
            if (lines != null)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (!TextUtil.IsBlank(lines[i]))
                    {
                        return ModStrings.Get(ModStrings.NavHasTooltip);
                    }
                }
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.Core.UI.Graph
{
    /// <summary>
    /// How a control's tooltip reaches the player when focus lands on it.
    ///
    /// The choice is decided from the tooltip itself - <see cref="ES2Access.UI.GraphNodes.ModeFor"/>,
    /// which every screen goes through - rather than picked per control: whether a tooltip is the one
    /// sentence that explains the control (say it outright) or a stat block assembled at draw time
    /// that the player will want to walk at their own pace (mention that it is there) is something the
    /// tooltip itself already answers, by whether it names a CLASS or carries plain Content. A screen
    /// only chooses directly in the rare case it has no <c>AgeTooltip</c> to hand <c>ModeFor</c> at
    /// all - a control this mod invented rather than one the game drew.
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

        /// <summary>
        /// A short "has tooltip" is spoken in place of the text - unconditionally, because the
        /// control HAVING one is what this mode means and is already known by the time it is chosen.
        ///
        /// Asking whether the lines resolve to anything first would be asking the wrong question at
        /// the wrong moment. The mode is only ever reached for a tooltip that names a CLASS, whose
        /// words do not exist until the tooltip window draws them - a third of a second after focus
        /// arrives, which is well after the readout that would mention it has been composed. So the
        /// check answered "empty" every time and the indication was never spoken at all, on exactly
        /// the controls that most needed it.
        /// </summary>
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
        /// <see cref="TooltipMode.None"/> or there is no tooltip at all.</summary>
        public static NodeAnnouncement Part(TooltipMode mode, Func<IList<string>> lines)
        {
            if (mode == TooltipMode.None || lines == null)
            {
                return null;
            }

            bool announce = mode == TooltipMode.Announce;
            return new NodeAnnouncement(
                () => announce ? Spoken(lines()) : ModStrings.Get(ModStrings.NavHasTooltip),
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
    }
}

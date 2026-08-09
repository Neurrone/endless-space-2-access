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

    /// <summary>
    /// The PROJECTION from a control's declared <see cref="NodeSection"/>s onto the focus readout.
    ///
    /// The buffer's half of the same declaration is the navigator's (<c>GraphNavigator.BufferLines</c>);
    /// this half is one announcement part, composed from the sections' modes alone:
    ///
    /// - the LAST <see cref="TooltipMode.Announce"/> section is the one spoken outright. A row can carry
    ///   more than one tooltip (the heading explains the measure, the value describes itself) and it is
    ///   the value's - the last one drawn - that the player asked for by landing there.
    /// - any <see cref="TooltipMode.Indicate"/> section adds "has tooltip", UNCONDITIONALLY. Its words do
    ///   not exist until the game draws them, so a check for content would answer "empty" every time.
    /// - <see cref="TooltipMode.None"/> sections say nothing here at all: they are the control's drawn
    ///   face, already reviewable, and reading them on every pass is what buffers exist to avoid.
    ///
    /// Kept beside the graph types rather than in a game adapter so the wording and these rules are the
    /// same on every screen and testable without the game.
    /// </summary>
    public static class TooltipParts
    {
        /// <summary>The tooltip part <paramref name="sections"/> project to, resolved at speak time - a
        /// control that is refusing appends its reason to its own tooltip, and the reason it gives has
        /// to be the one it would give now. Null when nothing in the list wants to be heard.</summary>
        public static NodeAnnouncement Part(IList<NodeSection> sections)
        {
            if (sections == null)
            {
                return null;
            }

            // The modes are structural - they come from the tooltip's own class, decided when the node
            // was declared - so which section speaks is settled here, once, rather than per readout.
            Func<IList<string>> spoken = null;
            bool indicate = false;
            for (int i = 0; i < sections.Count; i++)
            {
                NodeSection section = sections[i];
                if (section == null || section.Lines == null)
                {
                    continue;
                }

                if (section.Mode == TooltipMode.Indicate)
                {
                    indicate = true;
                }
                else if (section.Mode == TooltipMode.Announce)
                {
                    spoken = section.Lines;
                }
            }

            if (spoken == null && !indicate)
            {
                return null;
            }

            Func<IList<string>> lines = spoken;
            bool hasLong = indicate;
            return new NodeAnnouncement(() => Compose(lines, hasLong), kind: AnnouncementKinds.Tooltip);
        }

        /// <summary>The part a single tooltip projects to - for a control this mod invented, which has
        /// no <c>AgeTooltip</c> to read a mode off.</summary>
        public static NodeAnnouncement Part(TooltipMode mode, Func<IList<string>> lines)
        {
            return lines == null || mode == TooltipMode.None
                ? null
                : Part(new[] { new NodeSection(lines, mode) });
        }

        // What the player hears: the short tooltip's own words, and - when the row also carries a long
        // one - that there is more waiting in the buffer. Both, when both are there: dropping the words
        // because something else on the row is reviewable would lose the sentence the game's author
        // wrote for exactly this moment.
        //
        // A tooltip's lines are the game's own prose: they join with a space, never the list
        // separator (localization.md - "Permanently deletes the selected custom faction, This
        // faction cannot be edited" was a comma splice the game never wrote; any pause belongs
        // to the game's own punctuation). The has-tooltip indicator is mod text and stays its
        // own list item.
        private static string Compose(Func<IList<string>> lines, bool indicate)
        {
            MessageBuilder message = new MessageBuilder();
            IList<string> spoken = lines != null ? lines() : null;
            if (spoken != null)
            {
                bool first = true;
                for (int i = 0; i < spoken.Count; i++)
                {
                    if (!TextUtil.IsBlank(spoken[i]))
                    {
                        if (first)
                        {
                            message.ListItem(spoken[i].Trim());
                            first = false;
                        }
                        else
                        {
                            message.Fragment(spoken[i].Trim());
                        }
                    }
                }
            }

            if (indicate)
            {
                message.ListItem(ModStrings.Get(ModStrings.NavHasTooltip));
            }

            return message.Build();
        }

    }
}

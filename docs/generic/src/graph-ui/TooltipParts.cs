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
    /// that the player will want to walk at their own pace (leave it to the review buffer) is
    /// something the tooltip itself already answers, by whether it names a CLASS or carries plain
    /// Content. A screen only chooses directly in the rare case it has no <c>AgeTooltip</c> to hand
    /// <c>ModeFor</c> at all - a control this mod invented rather than one the game drew.
    ///
    /// Either way the tooltip's full text still feeds the review buffer, so nothing is ever only
    /// available by hearing it go past. Nothing in the readout says the buffer has something in it:
    /// the convention is that the player checks the buffer, on every control, so a per-control claim
    /// about it only ever spent the player's time saying what is nearly always true.
    /// </summary>
    public enum TooltipMode
    {
        /// <summary>The tooltip does not enter the focus readout at all.</summary>
        None,

        /// <summary>Its text is spoken as part of the readout.</summary>
        Announce,

        /// <summary>
        /// Its text reaches the player through the review buffer alone: the readout says nothing
        /// about it, not even that it is there.
        ///
        /// Still a mode of its own rather than <see cref="None"/>, which it now matches in the
        /// readout. <see cref="None"/> is content the control DRAWS - already on the screen. This is
        /// a tooltip the GAME would draw on hover, so it is the thing the pointer is aimed at when
        /// focus arrives and the thing the tooltip-parity audit holds a screen to; the two need to
        /// tell the sections apart. Its <see cref="NodeSection.Indicates"/> is the engine's own
        /// would-it-draw test, kept for them - never a test of the section's LINES, whose words do
        /// not exist until the tooltip window draws them.
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
    /// - <see cref="TooltipMode.Indicate"/> and <see cref="TooltipMode.None"/> sections say nothing
    ///   here at all. Both are reviewable and neither is announced: reading them on every pass is what
    ///   buffers exist to avoid, and announcing that they EXIST was a claim the player heard on most
    ///   controls of most screens, which is the same as hearing it on none.
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
            for (int i = 0; i < sections.Count; i++)
            {
                NodeSection section = sections[i];
                if (section == null || section.Lines == null)
                {
                    continue;
                }

                if (section.Mode == TooltipMode.Announce)
                {
                    spoken = section.Lines;
                }
            }

            if (spoken == null)
            {
                return null;
            }

            Func<IList<string>> lines = spoken;
            return new NodeAnnouncement(() => Compose(lines), kind: AnnouncementKinds.Tooltip);
        }

        /// <summary>The part a single tooltip projects to - for a control this mod invented, which has
        /// no <c>AgeTooltip</c> to read a mode off.</summary>
        public static NodeAnnouncement Part(TooltipMode mode, Func<IList<string>> lines)
        {
            return lines == null || mode == TooltipMode.None
                ? null
                : Part(new[] { new NodeSection(lines, mode) });
        }

        // What the player hears: the short tooltip's own words, and nothing about the long one - a
        // long tooltip is read from the review buffer, which the player checks whether or not anything
        // said there was something in it.
        //
        // A tooltip's lines are the game's own prose: they join with a space, never the list
        // separator (localization.md - "Permanently deletes the selected custom faction, This
        // faction cannot be edited" was a comma splice the game never wrote; any pause belongs
        // to the game's own punctuation).
        private static string Compose(Func<IList<string>> lines)
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

            return message.Build();
        }

    }
}

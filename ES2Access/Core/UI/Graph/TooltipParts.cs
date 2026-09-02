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
    /// Content. There is no way for a screen to say otherwise: the only sections that carry a mode
    /// off a tooltip are built at that one door, and a screen that wants a section of its own asks for
    /// <see cref="NodeSection.Buffer"/> or <see cref="NodeSection.Composed"/> - which are about words
    /// the mod composed, where there is no tooltip to have a kind.
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
    /// - a control points at ONE tooltip, so at most ONE tooltip's lines are spoken: the LAST
    ///   <see cref="TooltipMode.Announce"/> section that came off a tooltip. A row can carry more than
    ///   one (the heading explains the measure, the value describes itself) and it is the value's - the
    ///   last one drawn - that the player asked for by landing there.
    /// - words the mod COMPOSED (<see cref="NodeSection.Composed"/>) are the control's own rather than
    ///   any tooltip's, so they are not in that competition: every one of them is spoken, in declared
    ///   order alongside the tooltip's. A report row that went and got the sentence behind its outcome
    ///   word AND carries a tooltip must not lose one to the other.
    /// - <see cref="TooltipMode.Indicate"/> and <see cref="TooltipMode.None"/> sections say nothing
    ///   here at all. Both are reviewable and neither is announced: reading them on every pass is what
    ///   buffers exist to avoid, and announcing that they EXIST was a claim the player heard on most
    ///   controls of most screens, which is the same as hearing it on none.
    /// - a line the READOUT ALREADY SPEAKS is dropped from it. Half this game's icons are named by the
    ///   first line of their own tooltip, and a control whose label was read off the tooltip used to
    ///   have to choose between saying that line twice ("Empire Summary, button, Empire Summary Click
    ///   to consult...") and throwing the whole tooltip away. Neither is the answer: the line the label
    ///   already said comes out, the rest is announced, and no call site has to know.
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
            return Part(sections, null);
        }

        /// <summary>
        /// The same, minus whatever <paramref name="alreadySpoken"/> - the rest of the control's own
        /// readout - is going to say anyway.
        ///
        /// Resolved at speak time on BOTH sides, because both sides are: a label read off the
        /// tooltip's own first line changes when the tooltip does, and a dedupe settled when the node
        /// was declared would go on dropping last turn's sentence. Compared the way the review
        /// buffer's head compare works (trimmed, case-insensitive, whole line), so the two surfaces
        /// agree about what counts as the same words.
        /// </summary>
        public static NodeAnnouncement Part(
            IList<NodeSection> sections,
            IList<NodeAnnouncement> alreadySpoken
        )
        {
            if (sections == null)
            {
                return null;
            }

            // The modes are structural - they come from the tooltip's own class, decided when the node
            // was declared - so which sections speak is settled here, once, rather than per readout.
            // Two passes, because "the last tooltip is the one this control points at" can only be
            // answered after every section has been seen.
            int pointedAt = -1;
            for (int i = 0; i < sections.Count; i++)
            {
                if (Speaks(sections[i]) && sections[i].FromTooltip)
                {
                    pointedAt = i;
                }
            }

            List<Func<IList<string>>> spoken = null;
            for (int i = 0; i < sections.Count; i++)
            {
                if (!Speaks(sections[i]) || (sections[i].FromTooltip && i != pointedAt))
                {
                    continue;
                }

                if (spoken == null)
                {
                    spoken = new List<Func<IList<string>>>(2);
                }

                spoken.Add(sections[i].Lines);
            }

            if (spoken == null)
            {
                return null;
            }

            List<Func<IList<string>>> lines = spoken;
            NodeAnnouncement[] said = Snapshot(alreadySpoken);
            return new NodeAnnouncement(() => Compose(lines, said), kind: AnnouncementKinds.Tooltip);
        }

        private static bool Speaks(NodeSection section)
        {
            return section != null
                && section.Lines != null
                && section.Mode == TooltipMode.Announce;
        }

        // The parts as they stood when the node was composed. A copy, because the caller goes on
        // filling and sorting its own list afterwards and the dedupe is about what the readout SAYS,
        // which is settled by then.
        private static NodeAnnouncement[] Snapshot(IList<NodeAnnouncement> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return null;
            }

            NodeAnnouncement[] copy = new NodeAnnouncement[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                copy[i] = parts[i];
            }

            return copy;
        }

        // Whether the readout is going to say this line anyway, in the words it would say it in.
        private static bool AlreadySaid(NodeAnnouncement[] said, string line)
        {
            for (int i = 0; said != null && i < said.Length; i++)
            {
                NodeAnnouncement part = said[i];
                if (part == null || part.Text == null)
                {
                    continue;
                }

                string text;
                try
                {
                    text = part.Text();
                }
                catch (Exception)
                {
                    continue;
                }

                if (SpokenText.SameLine(text, line))
                {
                    return true;
                }
            }

            return false;
        }

        // What the player hears: the short tooltip's own words, and nothing about the long one - a
        // long tooltip is read from the review buffer, which the player checks whether or not anything
        // said there was something in it.
        //
        // A tooltip's lines are the game's own prose: they join with a space, never the list
        // separator (localization.md - "Permanently deletes the selected custom faction, This
        // faction cannot be edited" was a comma splice the game never wrote; any pause belongs
        // to the game's own punctuation).
        //
        // Two BLOCKS - a sentence the row composed and the tooltip it also carries - are two separate
        // things to say, so they are separated the way the readout separates everything else; the lines
        // WITHIN one block stay the prose they were. And a line an earlier block already put in the
        // readout is not repeated by a later one: a row that composed its sentence out of the same
        // words its tooltip carries would otherwise say it twice.
        private static string Compose(List<Func<IList<string>>> sections, NodeAnnouncement[] said)
        {
            MessageBuilder message = new MessageBuilder();
            List<string> earlier = new List<string>();
            for (int s = 0; sections != null && s < sections.Count; s++)
            {
                IList<string> spoken = Resolve(sections[s]);
                int start = earlier.Count;
                for (int i = 0; spoken != null && i < spoken.Count; i++)
                {
                    if (TextUtil.IsBlank(spoken[i]))
                    {
                        continue;
                    }

                    string line = spoken[i].Trim();
                    if (AlreadySaid(said, line) || SpokenText.Mentions(earlier, start, line))
                    {
                        continue;
                    }

                    if (earlier.Count == start)
                    {
                        message.ListItem(line);
                    }
                    else
                    {
                        message.Fragment(line);
                    }

                    earlier.Add(line);
                }
            }

            return message.Build();
        }

        private static IList<string> Resolve(Func<IList<string>> lines)
        {
            return lines == null ? null : lines();
        }

    }
}

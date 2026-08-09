using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI.Graph
{
    /// <summary>
    /// The BUFFER half of a control's declared content - the other half of
    /// <see cref="TooltipParts"/>, projected from the same <see cref="NodeVtable.Sections"/>.
    ///
    /// Two parts, in this order:
    ///
    /// - an automatic HEAD, read off the control's own readout: its name, then the state words the
    ///   readout appends ("unavailable", "checked", "expanded"). The role word and the auto-stamped
    ///   position are left out - they describe the control, and the buffer is for what the control has
    ///   to say - and so is the tooltip part, whether it announces the text or only says there is one,
    ///   because the tooltip's own lines follow below. The head is why a control that declares NO
    ///   sections still reviews correctly: a lore paragraph declared as nothing but a label is
    ///   reviewable as that paragraph, for free.
    /// - the sections, in declared order, which is drawn order: a row's heading tooltip before its
    ///   value's dossier, a card's drawn output rows before the panel behind it.
    ///
    /// Nothing here asks what MODE a section is in. Every section is reviewable - that is what makes
    /// "indicate and review" and "announce and review" the same promise to the player, and it is why
    /// the two surfaces are derived from one declaration rather than wired twice.
    /// </summary>
    public static class NodeBuffer
    {
        public static List<string> Lines(GraphNode node)
        {
            List<string> lines = new List<string>();
            if (node == null || node.Vtable == null)
            {
                return lines;
            }

            string label = GraphAnnouncer.FirstPartText(node);
            Add(lines, label);

            List<NodeAnnouncement> parts = GraphAnnouncer.EffectiveAnnouncements(node);
            for (int i = 0; i < parts.Count; i++)
            {
                NodeAnnouncement part = parts[i];
                if (
                    part == null
                    || part.Kind == AnnouncementKinds.Label
                    || part.Kind == AnnouncementKinds.Role
                    || part.Kind == AnnouncementKinds.Tooltip
                )
                {
                    continue;
                }

                Add(lines, Resolve(part.Text));
            }

            if (
                node.Expandable
                && !node.Vtable.SpeaksOwnExpansion
                && GraphAnnouncer.ExpandedStateText != null
            )
            {
                Add(lines, GraphAnnouncer.ExpandedStateText(node.Expanded));
            }

            IList<NodeSection> sections = node.Vtable.Sections;
            bool first = true;
            for (int s = 0; sections != null && s < sections.Count; s++)
            {
                IList<string> details = Resolve(sections[s]);
                for (int i = 0; i < details.Count; i++)
                {
                    // A tooltip whose first line is just the control's name again: the buffer already
                    // opened with it. Only the FIRST line of the whole list is tested, and only an
                    // exact repeat is dropped, so a heading that adds anything still reads.
                    bool duplicate = first && IsSameText(label, details[i]);
                    first = false;
                    if (!duplicate)
                    {
                        Add(lines, details[i]);
                    }
                }
            }

            return lines;
        }

        private static readonly List<string> None = new List<string>();

        private static IList<string> Resolve(NodeSection section)
        {
            if (section == null || section.Lines == null)
            {
                return None;
            }

            try
            {
                return section.Lines() ?? None;
            }
            catch (Exception)
            {
                return None;
            }
        }

        private static string Resolve(Func<string> text)
        {
            try
            {
                return text == null ? null : text();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsSameText(string left, string right)
        {
            return !string.IsNullOrEmpty(left)
                && !string.IsNullOrEmpty(right)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                lines.Add(line);
            }
        }
    }
}

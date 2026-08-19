using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// The sheet of prose the game draws BESIDE a window to say who an empire is: its faction, what that
    /// faction is about, what it is good at, who else it has met, and - for a computer-run rival - what it
    /// is like to deal with. One prefab (<c>NegotiationEmpireInfoPanel</c>) serves every surface that
    /// offers it, so the reading lives here rather than on the first screen that met it: the introduction
    /// popup and the diplomatic-offer popup open it from a tick box, and the negotiation table opens the
    /// same panel from its own.
    ///
    /// It is read off what is DRAWN rather than out of the panel's fields, because that is what the panel
    /// is: a page of headings and paragraphs, a different set of them per empire, every one of them a line
    /// the game has already written and laid out. One drawn line is one row, which is also how it scrolls -
    /// the sheet is taller than its viewport and the cursor brings itself into view. A hidden branch is
    /// skipped rather than read: the panel keeps a block per kind of empire and hides the ones this one has
    /// nothing to say for.
    ///
    /// A faction trait explains itself in a tooltip the game assembles as it draws it - a Class tooltip,
    /// per <see cref="GraphNodes.ModeFor"/> - so a trait's row indicates having one rather than reading it
    /// outright, and carries the drawn tooltip as review-buffer content regardless.
    ///
    /// Callers put it in a REGION of the page it hangs off (never a Tab stop of its own: it is still part
    /// of the one place the window is) and stop declaring it the moment the tick box that opened it is
    /// unticked, so the cursor is never left standing in a panel that has gone.
    /// </summary>
    public static class EmpireDossier
    {
        /// <summary>How deep a widget tree is walked before giving up - a guard against a cycle, not a
        /// real limit on any panel the game draws.</summary>
        private const int MaxDepth = 64;

        /// <summary>One line the panel draws: the label's own transform - which is the rectangle the rows
        /// are worked out from, and what has to be scrolled into view - and the widget the game hung the
        /// explaining tooltip on, which for a table row is the row rather than its label.</summary>
        public struct DrawnLine
        {
            public AgeTransform Widget;
            public AgeTransform Owner;
            public AgeTooltip Tooltip;
            public string Text;
        }

        public static readonly Func<DrawnLine, AgeTransform> LineWidget = line => line.Widget;

        /// <summary>What one drawn line says. A line the game wrote as prose keeps the prose - its own
        /// wrapping is where the words ran out, not punctuation - while two labels drawn side by side (an
        /// empire and how it gets on with you) are two facts, and read as two.</summary>
        public static string RowText(List<DrawnLine> row)
        {
            MessageBuilder message = new MessageBuilder();
            for (int i = 0; i < row.Count; i++)
            {
                message.ListItem();
                foreach (string line in AgeText.Lines(row[i].Text))
                {
                    message.Fragment(line);
                }
            }

            return message.Build();
        }

        /// <summary>Everything a widget subtree is showing, in the order it is laid out.</summary>
        public static void Read(
            AgeTransform widget,
            List<DrawnLine> lines,
            AgeTooltip inherited,
            int depth
        )
        {
            if (depth > MaxDepth)
            {
                return;
            }

            AgeTooltip tooltip = widget.AgeTooltip ?? inherited;
            AgeTransform owner = widget.AgeTooltip != null ? widget : null;
            string text = AgeText.Label(widget.GetComponent<AgePrimitiveLabel>());
            if (!string.IsNullOrEmpty(text))
            {
                lines.Add(
                    new DrawnLine
                    {
                        Widget = widget,
                        Owner = owner ?? widget,
                        Tooltip = tooltip,
                        Text = text,
                    }
                );
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && child.Visible)
                {
                    Read(child, lines, tooltip, depth + 1);
                }
            }
        }

        /// <summary>The dossier panel a window carries, wherever it keeps it - the same panel serves the
        /// introduction popup, a diplomatic offer and the negotiation table.</summary>
        public static NegotiationEmpireInfoPanel Panel(UnityEngine.GameObject host)
        {
            try
            {
                return host == null
                    ? null
                    : host.GetComponentInChildren<NegotiationEmpireInfoPanel>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool Open(NegotiationEmpireInfoPanel panel)
        {
            try
            {
                return panel != null
                    && panel.Shown
                    && AgeWidgets.Visible(panel.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The transform of an OPEN dossier, or null - what a caller reading the window's own
        /// body tests against so the sheet is not read a second time in the body's own rows.</summary>
        public static AgeTransform Drawn(NegotiationEmpireInfoPanel panel)
        {
            return Open(panel) ? panel.AgeTransform : null;
        }

        /// <summary>
        /// The open dossier's rows, as a REGION of the page it hangs off.
        ///
        /// Every row is something the game wrote down for the player to read, not a control they work, so
        /// there is no role word and no state - just the words, the tooltip where the game hung one, and
        /// the pointer aimed at it so a Class tooltip is actually drawn.
        ///
        /// A drawn row is a row of NODES, not one node. Where the game draws two explaining things side
        /// by side - two faction traits on one line - each carries its own tooltip, and a row that kept
        /// only the first one's would leave the second's description with nowhere to be read from. So a
        /// row is split where the tooltip changes: consecutive lines under the SAME tooltip (which is
        /// how a label under the widget the tooltip hangs on arrives here, and how a run of plain prose
        /// arrives with none at all) are one node, and the next tooltip starts the next node. The nodes
        /// of one row stay peers of that row, so left and right walk the line and up and down cross it.
        ///
        /// The region is begun only once there is a row to put in it: an empty <c>SetRegion</c> would
        /// re-tag whatever the caller declares next.
        /// </summary>
        public static void Build(
            GraphBuilder builder,
            NegotiationEmpireInfoPanel panel,
            string keyPrefix,
            object regionKey
        )
        {
            if (!Open(panel))
            {
                return;
            }

            List<DrawnLine> lines = new List<DrawnLine>();
            Read(panel.AgeTransform, lines, null, 0);
            if (lines.Count == 0)
            {
                return;
            }

            builder.SetRegion(regionKey);
            int index = 0;
            foreach (List<DrawnLine> row in AgeLayout.Rows(lines, LineWidget))
            {
                builder.StartRow();
                int part = 0;
                for (int at = 0; at < row.Count; part++)
                {
                    int end = at + 1;
                    while (end < row.Count && ReferenceEquals(row[end].Tooltip, row[at].Tooltip))
                    {
                        end++;
                    }

                    List<DrawnLine> it = row.GetRange(at, end - at);
                    at = end;
                    AgeTooltip tooltip = it[0].Tooltip;
                    AgeTransform under = it[0].Owner;
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => RowText(it)),
                        },
                        Sections = GraphNodes.Sections(null, tooltip),
                        OnFocusVisual = () => PointerFocus.MoveTo(null, tooltip, under),
                        OnBlurVisual = AgeWidgets.ReleasePointer,
                    };
                    builder.AddItem(
                        ControlId.Referenced(
                            it[0].Widget,
                            keyPrefix + index + "." + part + "/" + it[0].Widget.name
                        ),
                        vtable
                    );
                }

                builder.EndRow();
                index++;
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

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
    /// A relation row is the one place the sheet says something the game draws no words for at all:
    /// how the empire being read stands with a third empire is an icon and nothing else, so the row
    /// reads the other empire's name and then that state in the game's own word for it.
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

        /// <summary>The panel's own list of who it drew a relation row for and how they stand - one
        /// entry per row, in the order the rows were filled.</summary>
        private static readonly FieldInfo Relations = GameHandlers.Field(
            typeof(NegotiationEmpireInfoPanel),
            "empireAndRelationStateDatas"
        );

        /// <summary>The state off one of those entries; the type they are is private to the panel, so
        /// the property is taken off the first entry there is rather than off a name.</summary>
        private static PropertyInfo _state;

        /// <summary>One line the panel draws: the label's own transform - which is the rectangle the rows
        /// are worked out from, and what has to be scrolled into view - and the tooltip that explains it,
        /// which for a table row is the row's rather than the label's own.</summary>
        public struct DrawnLine
        {
            public AgeTransform Widget;
            public AgeTooltip Tooltip;
            public string Text;

            /// <summary>What the game says about this line with a picture instead of words - a
            /// relation row's state - read after the line's own text. Empty for every line the game
            /// wrote down itself.</summary>
            public string State;
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

                if (!string.IsNullOrEmpty(row[i].State))
                {
                    message.ListItem(row[i].State);
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
            string text = AgeText.Label(widget.GetComponent<AgePrimitiveLabel>());
            if (!string.IsNullOrEmpty(text))
            {
                lines.Add(
                    new DrawnLine
                    {
                        Widget = widget,
                        Tooltip = tooltip,
                        Text = text,
                    }
                );
            }

            int mark = lines.Count;
            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                // Flow control: a hidden band's children each read visible of their own, so a walk that
                // descended into one would collect lines the player cannot see.
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child != null)
                {
                    Read(child, lines, tooltip, depth + 1);
                }
            }

            RelationState(widget, lines, mark);
        }

        /// <summary>
        /// The relation state of a row that draws it as an ICON, put on the empire's name so the row
        /// says who they are and then how they stand - the one thing on this sheet that has to be
        /// worked out rather than read off what is drawn.
        ///
        /// It is carried by the name's own line rather than declared as a line of its own because the
        /// state has no rectangle to be placed by: it would share the name label's, and a row's lines
        /// are ordered by where they were drawn.
        ///
        /// Which state belongs to which row is the row's place under the table: the panel fills a list
        /// and hands the rows their entries from it in order.
        /// </summary>
        private static void RelationState(AgeTransform row, List<DrawnLine> lines, int from)
        {
            try
            {
                DiplomaticRelationStateLine drawn = row.GetComponent<DiplomaticRelationStateLine>();
                if (drawn == null || drawn.EmpireNameLabel == null || Relations == null)
                {
                    return;
                }

                NegotiationEmpireInfoPanel panel =
                    // walk: audit M1, to move behind FrameSweep
                    row.GetComponentInParent<NegotiationEmpireInfoPanel>();
                if (panel == null || panel.RelationsTable == null)
                {
                    return;
                }

                IList datas = Relations.GetValue(panel) as IList;
                int at = panel.RelationsTable.Children.IndexOf(row);
                object data = datas == null || at < 0 || at >= datas.Count ? null : datas[at];
                if (data == null)
                {
                    return;
                }

                if (_state == null)
                {
                    _state = data.GetType().GetProperty("DiplomaticRelationState");
                }

                DiplomaticRelationState state =
                    _state == null ? null : _state.GetValue(data, null) as DiplomaticRelationState;
                string word =
                    state == null ? null : AgeText.Clean(Gui.GetLocalizedTitle(state.Name));
                if (string.IsNullOrEmpty(word))
                {
                    return;
                }

                AgeTransform label = drawn.EmpireNameLabel.AgeTransform;
                for (int i = from; i < lines.Count; i++)
                {
                    if (ReferenceEquals(lines[i].Widget, label))
                    {
                        DrawnLine stamped = lines[i];
                        stamped.State = word;
                        lines[i] = stamped;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("dossier: reading a relation row's state threw: " + e);
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
                    // walk: audit M1, to move behind FrameSweep
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
                    // Flow control: the whole dossier below is read by descending this panel.
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
                    AgeTransform under = it[0].Widget;
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
                    builder.AddItem(Nodes.Drawn(
                        ControlId.For(
                            it[0].Widget,
                            keyPrefix + index + "." + part + "/" + it[0].Widget.name
                        ),
                        vtable,
                        it[0].Widget
                    ));
                }

                builder.EndRow();
                index++;
            }
        }
    }
}

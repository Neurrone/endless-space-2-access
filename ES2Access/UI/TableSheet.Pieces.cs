using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    public sealed partial class TableSheet
    {
        // ---- a cell read as several pieces ----

        /// <summary>
        /// One hover surface drawn inside a cell, and the widgets it hangs on.
        ///
        /// A cell that draws several things the game explains separately - a status circle per
        /// planet, a resource icon and figure per deposit, a badge per expansion, the count, the next
        /// people, the growth arrow and the turns of a population cell - is several columns to a
        /// keyboard, one per surface (owner ruling 2026-09-03): each says its own words, carries its
        /// own tooltip, and Left/Right walk them under the one caption. Two widgets that share a
        /// surface - the population icon and the count beside it, one sentence on both - are one
        /// piece, read together.
        /// </summary>
        private sealed class Piece
        {
            /// <summary>The surface, or null for the REMAINDER: words the cell draws under no surface
            /// at all, which keep the cell's own tooltip where it has one.</summary>
            public AgeTooltip Tooltip;

            /// <summary>The widgets the surface hangs on, in the order found; the remainder's is the
            /// cell itself.</summary>
            public readonly List<AgeTransform> Widgets = new List<AgeTransform>(2);

            /// <summary>For the remainder: the other pieces' widgets, whose subtrees are theirs to
            /// read and not this one's.</summary>
            public HashSet<AgeTransform> Skip;
        }

        /// <summary>
        /// The pieces a cell is read as, in drawn order, or null for a cell that is one column: a cell
        /// the screen supplies the value of, or one drawing fewer than two surfaces of its own. The
        /// row's own dossier hung on the cell (<see cref="Explains"/>) is not a piece - the row already
        /// says it - and neither is a surface the game could never draw.
        ///
        /// Words the cell draws under no surface - the status label beside the planet circles - come
        /// first, as a piece of their own, so nothing the cell says is lost to the split.
        /// </summary>
        private List<Piece> Pieces(AgeTransform cell, GuiTableHeader header, AgeTooltip rowTip)
        {
            try
            {
                if (cell == null || Supplied(header, cell))
                {
                    return null;
                }

                List<AgeTooltip> found = Hovers(cell);
                List<Piece> pieces = null;
                for (int i = 0; i < found.Count; i++)
                {
                    AgeTooltip tooltip = found[i];
                    if (
                        tooltip == null
                        || AgeWidgets.NeverDraws(tooltip)
                        || AgeWidgets.SameTooltip(tooltip, rowTip)
                    )
                    {
                        continue;
                    }

                    AgeTransform widget = tooltip.AgeTransform;
                    if (widget == null || ReferenceEquals(widget, cell))
                    {
                        continue;
                    }

                    Piece piece = pieces == null ? null : Sharing(pieces, tooltip);
                    if (piece == null)
                    {
                        piece = new Piece { Tooltip = tooltip };
                        if (pieces == null)
                        {
                            pieces = new List<Piece>(found.Count);
                        }

                        pieces.Add(piece);
                    }

                    piece.Widgets.Add(widget);
                }

                if (pieces == null || pieces.Count < 2)
                {
                    return null;
                }

                // The hover walk keeps ONE tooltip per surface, so the other widgets the game hung the
                // same surface on - the count beside the population icon - are claimed here, or they
                // would read as words under no surface.
                Claim(cell, pieces, 0);

                HashSet<AgeTransform> taken = new HashSet<AgeTransform>();
                for (int i = 0; i < pieces.Count; i++)
                {
                    for (int w = 0; w < pieces[i].Widgets.Count; w++)
                    {
                        taken.Add(pieces[i].Widgets[w]);
                    }
                }

                _echo = Caption(header);
                string rest;
                try
                {
                    rest = Drawn(cell, null, DeepCellDepth, taken);
                }
                finally
                {
                    _echo = null;
                }

                if (!string.IsNullOrEmpty(rest))
                {
                    AgeTooltip own = CellTooltip(header, cell);
                    Piece remainder = new Piece { Tooltip = own, Skip = taken };
                    remainder.Widgets.Add(cell);
                    pieces.Insert(0, remainder);
                }

                OrderPieces(pieces);
                return pieces;
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a cell's pieces threw: " + e);
                return null;
            }
        }

        /// <summary>Every painted widget under <paramref name="widget"/> whose tooltip is one of the
        /// pieces' surfaces joins that piece, once.</summary>
        private static void Claim(AgeTransform widget, List<Piece> pieces, int depth)
        {
            // Flow control: the walk stops where the renderer stops, as the hover walk did.
            if (widget == null || depth > DeepCellDepth || !widget.Visible || widget.Alpha <= 0f)
            {
                return;
            }

            if (depth > 0)
            {
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                Piece piece = tooltip == null ? null : Sharing(pieces, tooltip);
                if (piece != null && !piece.Widgets.Contains(widget))
                {
                    piece.Widgets.Add(widget);
                }
            }

            IList<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                Claim(children[i], pieces, depth + 1);
            }
        }

        private static Piece Sharing(List<Piece> pieces, AgeTooltip tooltip)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (AgeWidgets.SameTooltip(pieces[i].Tooltip, tooltip))
                {
                    return pieces[i];
                }
            }

            return null;
        }

        /// <summary>Pieces in the order the player sees them - down the lines and left to right along
        /// each, the same rule the words of one cell are read in (<see cref="Ordered"/>) - by where
        /// each piece's first widget is drawn.</summary>
        private void OrderPieces(List<Piece> pieces)
        {
            _drawn.Clear();
            for (int i = 0; i < pieces.Count; i++)
            {
                UnityEngine.Rect at = pieces[i].Widgets[0].GetGlobalPosition();
                _drawn.Add(
                    new DrawnPart(
                        i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        at.x,
                        at.y,
                        at.y + at.height
                    )
                );
            }

            Ordered(_drawn);
            List<Piece> sorted = new List<Piece>(pieces.Count);
            for (int i = 0; i < _drawn.Count; i++)
            {
                sorted.Add(
                    pieces[
                        int.Parse(_drawn[i].Text, System.Globalization.CultureInfo.InvariantCulture)
                    ]
                );
            }

            _drawn.Clear();
            pieces.Clear();
            pieces.AddRange(sorted);
        }

        /// <summary>What one piece says: the words drawn on its widgets, by the cell rules - and for a
        /// piece drawing no word at all, the first line of its own explanation, or what the game calls
        /// the thing it explains, so a bare arrow or badge still has a name.</summary>
        private string PieceText(Piece piece, GuiTableHeader header)
        {
            _echo = Caption(header);
            try
            {
                MessageBuilder said = new MessageBuilder();
                for (int i = 0; i < piece.Widgets.Count; i++)
                {
                    said.ListItem(Drawn(piece.Widgets[i], null, DeepCellDepth, piece.Skip));
                }

                string drawn = said.Build();
                if (!string.IsNullOrEmpty(drawn))
                {
                    return drawn;
                }

                AgeTooltip tooltip = piece.Tooltip;
                if (AgeWidgets.Readable(tooltip) != null)
                {
                    // Label fallback: a piece drawing no word is named by the first line of its own
                    // explanation - the rung the naming ladder reaches for a wordless badge - and the
                    // words stay its own section, declared through the door, so nothing is announced here.
                    IList<string> lines = AgeText.Lines(AgeText.Tooltip(tooltip));
                    if (lines.Count > 0)
                    {
                        return lines[0];
                    }
                }

                string title = tooltip == null ? null : AgeWidgets.TooltipTitle(tooltip);
                return string.IsNullOrEmpty(title) ? ModStrings.Get(ModStrings.NavCellEmpty) : title;
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a piece threw: " + e);
                return ModStrings.Get(ModStrings.NavCellEmpty);
            }
            finally
            {
                _echo = null;
            }
        }

        /// <summary>
        /// One piece of a cell as a column of its own. The screen's answer for the CELL - a button
        /// that opens a panel, the population column's drop - is asked once and applies to every piece
        /// of it (<see cref="ReadCell"/>): the piece is the same control as far as the game is
        /// concerned, drawn as one part of it. What is the piece's own is what it says, what explains
        /// it, and where the pointer goes.
        /// </summary>
        private NodeVtable PieceVtable(
            GuiTable table,
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header,
            Piece piece
        )
        {
            GuiTable owner = table;
            GuiTableLine row = line;
            AgeTransform it = cell;
            GuiTableHeader heading = header;
            Piece part = piece;
            Func<bool> enabled = Operable(table, line);
            NodeVtable answer = Answer(row, cell, header, enabled);
            bool saysRefusal = answer != null && ReferenceEquals(answer, _answeredRefusal);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = answer != null ? answer.ControlType : ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.ValuePart(() => PieceText(part, heading)),
                },
                Sections = GraphNodes.Sections(null, piece.Tooltip),
                BufferHead = () =>
                    new MessageBuilder()
                        .ListItem(Caption(heading))
                        .ListItem(PieceText(part, heading))
                        .Build(),
            };
            if (answer != null)
            {
                vtable.OnActivate = answer.OnActivate;
                vtable.OnAlternate = answer.OnAlternate;
                vtable.OnDoubleClick = answer.OnDoubleClick;
                vtable.OnPickUp = answer.OnPickUp;
                vtable.DropKind = answer.DropKind;
                vtable.OnDrop = answer.OnDrop;
                vtable.DropAccepts = answer.DropAccepts;
                vtable.StateText = answer.StateText;
                // The cell's own availability, which the screen's answer carries as a part the
                // pieces cannot share: asked again here of the same two things.
                vtable.Announcements.Add(
                    GraphNodes.DisabledPart(() => enabled() && AgeWidgets.Enabled(it))
                );
            }
            else if (Choosable(table))
            {
                vtable.OnActivate = () =>
                {
                    if (enabled())
                    {
                        AgeWidgets.Toggle(row.SelectionToggle);
                    }
                };
                vtable.StateText = () =>
                    Selected(owner, row) ? ModStrings.Get(ModStrings.NavSelected) : null;
            }

            Adorn(table, line, vtable, answer == null || !saysRefusal);
            AgeWidgets.PointAt(vtable, it);
            if (piece.Tooltip != null)
            {
                AgeWidgets.PointAt(vtable, it, piece.Tooltip);
            }

            return vtable;
        }

        /// <summary>The screen's answer for a cell, asked once per cell per build and shared by its
        /// pieces (<see cref="_answers"/>) - the hook may do work, and it marks the sheet's own
        /// refusal flag (<see cref="SaysRowRefusal"/>), which is remembered beside it.</summary>
        private NodeVtable Answer(
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header,
            Func<bool> enabled
        )
        {
            if (ReadCell == null)
            {
                return null;
            }

            NodeVtable answer;
            if (_answers.TryGetValue(cell, out answer))
            {
                return answer;
            }

            _saysRowRefusal = null;
            try
            {
                answer = ReadCell(line, cell, header, enabled);
            }
            catch (Exception e)
            {
                Log.Warn("table: the screen's reading of a cell threw: " + e);
                answer = null;
            }

            if (answer != null && ReferenceEquals(answer, _saysRowRefusal))
            {
                _answeredRefusal = answer;
            }

            _saysRowRefusal = null;
            _answers[cell] = answer;
            return answer;
        }

        /// <summary>Per-build memo of the screen's answer per cell (<see cref="Answer"/>), cleared at
        /// the top of <see cref="Rows"/> for the reason every other cell memo is: a pooled cell widget
        /// outlives the row bound to it.</summary>
        private readonly Dictionary<AgeTransform, NodeVtable> _answers =
            new Dictionary<AgeTransform, NodeVtable>();

        /// <summary>The last answer the screen marked as already saying the row's refusal.</summary>
        private NodeVtable _answeredRefusal;
    }
}

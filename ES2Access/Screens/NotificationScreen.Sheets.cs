using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;
using Line = ES2Access.UI.EmpireDossier.DrawnLine;

namespace ES2Access.Screens
{
    /// <summary>The two tables a popup can show: the one it drew its content as, and the one it
    /// stamped out of a prefab.</summary>
    public sealed partial class NotificationScreen
    {
        // ---- the table a popup drew its content as ----

        /// <summary>The table a popup drew: the captions across the top, left to right, and one row
        /// per line the game drew under them.</summary>
        private sealed class Sheet
        {
            public List<Line> Headers;
            public List<SheetRow> Rows;

            /// <summary>What the popup wrote UNDER the lines - the inspector's report ends with what all
            /// of it came to. Not a line of the table: it stands outside the rows, and reads as the
            /// full-width row it is drawn as.</summary>
            public List<List<Line>> Footer;
        }

        /// <summary>One line of that table: the thing the game wired the click to, and its pieces
        /// already paired with the caption each was drawn under - a slot per caption, empty where the
        /// line drew nothing in that column.</summary>
        private struct SheetRow
        {
            public AgeTransform Widget;
            public AgeTransform[] Cells;
        }

        /// <summary>
        /// Whether the popup's content is a TABLE, and what its columns and lines are.
        ///
        /// Nothing here knows which popup it is looking at. A table is what the game draws when it
        /// has a list of things and a fact or two about each: a SCROLLING list whose lines are things
        /// a click does something with, under a band of CAPTIONS written across the top of it. Both
        /// halves have to be there, and two further conditions keep a popup that merely has a
        /// scrolling paragraph in it - the research report's lore - from being read as a grid: nothing
        /// the popup drew in its content may sit outside the list except those captions, and every
        /// line's pieces must fall one to a caption, left to right. A popup that fails any of it is
        /// read the ordinary way, as the rows it drew.
        ///
        /// The popup's own words are not among what it drew: they lead the body as a row of their own
        /// (<paramref name="words"/>), so a popup that says a sentence over a table still has a table
        /// rather than losing it to a caption band the sentence would have broken up.
        /// </summary>
        private static Sheet ReadSheet(
            NotificationWindow window,
            List<Control> controls,
            List<Control> inside,
            AgeTransform words
        )
        {
            try
            {
                AgeTransform root = Root(window);
                if (root == null)
                {
                    return null;
                }

                List<Line> drawn = new List<Line>();
                Read(root, drawn, null, 0);

                List<AgeTransform> title = TitleBar(window, controls);
                List<AgeTransform> buttons = ButtonBar(controls);
                List<Line> body = new List<Line>();
                foreach (Line line in drawn)
                {
                    if (
                        InBody(line.Widget, title, buttons)
                        // The same folded-panel test DrawnRows applies: a line the popup keeps at
                        // alpha 0 still holds its last words, and a stale one admitted here would
                        // be spoken as a column name on every row of the sheet.
                        && Painted(line.Owner, root)
                        && !PartOf(line.Widget, controls)
                        && !IsWords(line, words)
                    )
                    {
                        body.Add(line);
                    }
                }

                foreach (AgeControlScrollView view in ScrollViews.Under(root))
                {
                    AgeTransform widget = view == null ? null : view.AgeTransform;
                    // Flow control: a scroll view the popup is not drawing holds none of this notification's body.
                    if (
                        widget == null
                        || !AgeWidgets.Visible(widget)
                        || !InBody(widget, title, buttons)
                    )
                    {
                        continue;
                    }

                    Sheet sheet = SheetIn(widget, body, inside);
                    if (sheet != null)
                    {
                        return sheet;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for a table threw: " + e);
            }

            return null;
        }

        /// <summary>The table this scrolling list is the body of, or null where it is not one.
        /// </summary>
        private static Sheet SheetIn(AgeTransform view, List<Line> body, List<Control> inside)
        {
            // A control the popup captioned and drew OUTSIDE the list is content this reading would
            // drop, so the popup is not a table - it is a page with a list on it.
            foreach (Control control in inside)
            {
                if (!AgeWidgets.Under(control.Widget, view))
                {
                    return null;
                }
            }

            List<Line> headers = new List<Line>();
            List<Line> within = new List<Line>();
            foreach (Line line in body)
            {
                (AgeWidgets.Under(line.Widget, view) ? within : headers).Add(line);
            }

            // Column names are written on one line across the top of the list, and they are the only
            // words outside it; one of them alone is a heading rather than a set of columns.
            if (
                headers.Count < 2
                || within.Count == 0
                || AgeLayout.Rows(headers, LineWidget).Count != 1
                || AgeLayout.Band(headers[0].Widget, view) >= 0
            )
            {
                return null;
            }

            headers.Sort(AcrossTheRow);

            List<AgeTransform> lines = RowWidgets(view);
            if (lines.Count == 0)
            {
                return null;
            }

            // Words inside the list that belong to no line would be dropped by a reading that walks
            // lines - a footer, a heading the game left in there.
            foreach (Line line in within)
            {
                if (!InAny(line.Widget, lines))
                {
                    return null;
                }
            }

            List<SheetRow> rows = new List<SheetRow>();
            foreach (AgeTransform line in lines)
            {
                AgeTransform[] cells = Columns(line, headers);
                if (cells == null)
                {
                    return null;
                }

                rows.Add(new SheetRow { Widget = line, Cells = cells });
            }

            return new Sheet { Headers = headers, Rows = rows };
        }

        /// <summary>The lines of a scrolling list: the things in it the game wired a click to and
        /// wrote something on. What is in there for the look of it - the frame, the scrollbar - is
        /// wired to nothing and says nothing, and is not a line.</summary>
        private static List<AgeTransform> RowWidgets(AgeTransform view)
        {
            List<AgeTransform> lines = new List<AgeTransform>();
            Collect(view, lines, 0);
            lines.Sort(DownTheTable);
            return lines;
        }

        private static void Collect(AgeTransform widget, List<AgeTransform> lines, int depth)
        {
            // Flow control: this is the scraper's recursion stop as well as a line's own test - a
            // hidden branch's children each keep Visible of their own and would be read out of it.
            if (widget == null || depth > MaxAncestors || !widget.Visible)
            {
                return;
            }

            if (depth > 0 && Wired(widget) && Draws(widget, 0))
            {
                lines.Add(widget);
                return;
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child != null)
                {
                    Collect(child, lines, depth + 1);
                }
            }
        }

        private static bool Wired(AgeTransform widget)
        {
            AgeControlButton button = AgeWidgets.Button(widget);
            return button != null && !string.IsNullOrEmpty(button.OnActivateMethod);
        }

        /// <summary>Whether the game wrote anything the player can see inside this.</summary>
        private static bool Draws(AgeTransform widget, int depth)
        {
            // Flow control: the same recursion stop, for the boolean 'does this subtree draw
            // anything' the row shape is decided from.
            if (widget == null || depth > MaxCellDepth || !widget.Visible)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(AgeText.Label(widget.GetComponent<AgePrimitiveLabel>())))
            {
                return true;
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child != null && Draws(child, depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A line's pieces, one per column: which caption each was drawn under, answered by
        /// the rectangles the game laid them out at. Null where the line does not read as a row of
        /// that table - two pieces landing in one column, or running back across the page, is the
        /// answer that the captions are not columns over these lines at all.
        ///
        /// Where the line writes everything it says inside ONE piece that lies across several captions,
        /// that piece is the line's own wrapper rather than a column - a prefab draws a frame, a block
        /// holding all the words, and a frame again - and the columns are what is drawn inside it. So
        /// the pieces of THAT are what the captions are asked about instead. One level down, and no
        /// further: a wrapper inside a wrapper is a shape nothing measured has, and the rows are the
        /// safe reading for anything this does not recognise.</summary>
        private static AgeTransform[] Columns(AgeTransform line, List<Line> headers)
        {
            return Columns(line, headers, 0);
        }

        private static AgeTransform[] Columns(AgeTransform line, List<Line> headers, int depth)
        {
            AgeTransform[] cells = new AgeTransform[headers.Count];
            int filled = 0;
            int last = -1;
            AgeTransform only = null;
            List<AgeTransform> children = line.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                // Shape: which children count as a row's column pieces, not whether the row exists.
                if (child == null || !child.Visible || !Draws(child, 0))
                {
                    continue;
                }

                int column = ColumnOf(child, headers);
                if (column < 0 || column <= last)
                {
                    return null;
                }

                cells[column] = child;
                last = column;
                filled++;
                only = child;
            }

            if (depth == 0 && filled == 1 && Spans(only, headers) > 1)
            {
                return Columns(only, headers, depth + 1);
            }

            return filled > 1 && cells[0] != null ? cells : null;
        }

        /// <summary>How many captions a piece was drawn across - one for a column, more for the block a
        /// prefab wrapped a whole line in.</summary>
        private static int Spans(AgeTransform cell, List<Line> headers)
        {
            Rect it = cell.GetGlobalPosition();
            int across = 0;
            for (int i = 0; i < headers.Count; i++)
            {
                Rect header = headers[i].Widget.GetGlobalPosition();
                if (Mathf.Min(it.xMax, header.xMax) - Mathf.Max(it.xMin, header.xMin) > 0f)
                {
                    across++;
                }
            }

            return across;
        }

        /// <summary>Which caption a piece of a line was drawn under: the one it shares most of its
        /// width with, and none where it shares width with no caption at all.</summary>
        private static int ColumnOf(AgeTransform cell, List<Line> headers)
        {
            Rect it = cell.GetGlobalPosition();
            int best = -1;
            float most = 0f;
            for (int i = 0; i < headers.Count; i++)
            {
                Rect header = headers[i].Widget.GetGlobalPosition();
                float shared = Mathf.Min(it.xMax, header.xMax) - Mathf.Max(it.xMin, header.xMin);
                if (shared > most)
                {
                    most = shared;
                    best = i;
                }
            }

            return best;
        }

        private static bool InAny(AgeTransform widget, List<AgeTransform> ancestors)
        {
            for (int i = 0; i < ancestors.Count; i++)
            {
                if (AgeWidgets.Under(widget, ancestors[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The table, as a table: a line per row, the line's first piece the row itself and the rest
        /// its columns.
        ///
        /// The row is a BUTTON, because that is what the game made the line - clicking it is how a
        /// player opens the system a construction finished in - and Enter is that click. It reads the
        /// whole line, so walking down the table hears each system with what it finished and what it
        /// starts next; the columns beside it are there to walk across when one of those is the thing
        /// being compared, and each says the caption it is under as the edge crossed to reach it
        /// rather than repeating it in every row - the row column too, so coming back left says
        /// "System, Dusay" the same way going right said "Completed, Drone Networks".
        ///
        /// The words the popup opened with, where it had any, are the row above the first one - the
        /// table continues below them - and the strips around the whole thing are joined to it by the
        /// builder, which knows a seam is a ROW rather than a node.
        /// </summary>
        private static void BuildSheet(
            GraphBuilder builder,
            NotificationWindow window,
            Sheet sheet,
            ControlId lead
        )
        {
            // Every caption the popup drew, the row column's included: the sheet says each of them as
            // the edge that crosses into its column, so dropping one here would be a drawn word the
            // player can never hear.
            string[] columns = new string[sheet.Headers.Count];
            for (int i = 0; i < sheet.Headers.Count; i++)
            {
                columns[i] = sheet.Headers[i].Text;
            }

            GraphSheet table = new GraphSheet(builder, SheetKey);
            table.Region(Title(window), columns);
            table.Follows(lead);
            foreach (SheetRow row in sheet.Rows)
            {
                List<KeyValuePair<int, NodeVtable>> cells =
                    new List<KeyValuePair<int, NodeVtable>>();
                for (int c = 1; c < row.Cells.Length; c++)
                {
                    if (row.Cells[c] != null)
                    {
                        cells.Add(new KeyValuePair<int, NodeVtable>(c, CellNode(row, c)));
                    }
                }

                // The line the popup drew is both halves: what the row is keyed and scrolled by, and
                // what its cells exist by - these tables are the game's own scrolling lists, and a
                // line it retires keeps its words.
                table.RowAt(RowNode(row), row.Widget, cells, row.Widget);
            }

            for (int i = 0; sheet.Footer != null && i < sheet.Footer.Count; i++)
            {
                List<Line> band = sheet.Footer[i];
                table.Line(
                    new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => RowText(band)),
                        },
                        OnFocusVisual = AgeWidgets.ReleasePointer,
                    }
                );
            }

            table.Finish();
            if (lead == null)
            {
                // What the popup drew is all it has, so focus lands on its first line.
                builder.SetStart(table.FirstRow);
            }
        }

        /// <summary>The row itself: what the line says, all of it, and the game's own click where the
        /// game put one there. A table whose lines do nothing - what the inspector sold, which laws
        /// lapsed - has rows the player reads rather than works, and says no role word for a button that
        /// is not there.</summary>
        private static NodeVtable RowNode(SheetRow row)
        {
            AgeTransform widget = row.Widget;
            AgeTransform[] cells = row.Cells;
            NodeVtable vtable = Wired(widget)
                ? GraphNodes.Button(
                    () => CellText(cells[0]),
                    () => AgeWidgets.Press(widget),
                    () => AgeWidgets.Operable(widget),
                    AgeWidgets.Raw(widget)
                )
                : new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => CellText(cells[0])),
                    },
                    Sections = GraphNodes.Sections(null, AgeWidgets.Raw(widget)),
                };

            for (int c = 1; c < cells.Length; c++)
            {
                AgeTransform cell = cells[c];
                if (cell != null)
                {
                    // Not watched: a notification is a report of something that has already happened,
                    // and nothing in it changes under a standing cursor.
                    vtable.Announcements.Add(GraphNodes.ValuePart(() => CellText(cell), false));
                }
            }

            AgeControlButton click = AgeWidgets.Button(widget);
            if (click != null)
            {
                AgeWidgets.Point(vtable, click);
            }
            else
            {
                AgeWidgets.PointAt(vtable, widget);
            }

            return vtable;
        }

        /// <summary>One column of a row: what the game drew in it and the tooltips it hung there - a
        /// constructible's dossier, assembled as it is drawn, so indicated rather than read out and
        /// carried in the buffer. It does not say its own caption: the sheet says that as the edge the
        /// player crossed to get here.</summary>
        private static NodeVtable CellNode(SheetRow row, int column)
        {
            AgeTransform cell = row.Cells[column];
            AgeTransform name = row.Cells[0];
            List<AgeTooltip> tooltips = Tooltips(cell);
            // ONE tooltip: the last drawn in the column, which is the one the cell points at below. The
            // others used to be sections of their own and could never fill - the cell raises what it
            // points at and nothing else - and a sheet cell is not a group, so there is nowhere here to
            // hand them to. Dropped rather than left as a promise; the shape to watch is a column whose
            // extra explanation is content-backed, which no popup in the fixture draws.
            AgeTooltip shown = Last(tooltips);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.ValuePart(() => CellText(cell), false),
                },
                Sections = GraphNodes.Sections(GraphNodes.TooltipSection(shown)),
                SearchText = () => CellText(name),
            };

            // The tooltip hangs off the picture inside the cell rather than the cell, and pointing at
            // anything else draws nothing.
            AgeTooltip tooltip = shown;
            AgeTransform hover = tooltip == null ? null : AgeWidgets.TooltipOwner(tooltip);
            vtable.OnFocusVisual =
                hover == null
                    ? AgeWidgets.ReleasePointer
                    : () => PointerFocus.MoveTo(hover, tooltip);
            vtable.OnBlurVisual = AgeWidgets.ReleasePointer;
            vtable.PointsAt = () => hover == null ? null : tooltip;
            return vtable;
        }

        /// <summary>The tooltips the game hung inside one column, in the order it drew them.</summary>
        private static List<AgeTooltip> Tooltips(AgeTransform cell)
        {
            List<AgeTooltip> tooltips = new List<AgeTooltip>();
            AgeWidgets.EffectiveTooltips(
                cell,
                tooltips,
                TooltipReach.Own | TooltipReach.Descendants,
                MaxCellDepth
            );
            return tooltips;
        }

        private static AgeTooltip Last(List<AgeTooltip> tooltips)
        {
            return tooltips.Count == 0 ? null : tooltips[tooltips.Count - 1];
        }

        /// <summary>
        /// What one column of a row says: what the game wrote in it, read across the way it is drawn.
        ///
        /// A bare number is the one thing that cannot be read as it stands, because what it counts is
        /// drawn beside it as a picture rather than written: the construction table puts an hourglass
        /// in front of the turns a build has left, and "3" on its own is a number the player has to
        /// guess the units of. Where the column draws that hourglass, its number is said as the turns
        /// it stands for - and where the game put a word there instead of a number ("[infinite]"), the
        /// word is what it says, because the game has already answered.
        /// </summary>
        private static string CellText(AgeTransform cell)
        {
            if (cell == null)
            {
                return null;
            }

            List<AgePrimitiveLabel> labels = new List<AgePrimitiveLabel>();
            bool turns = false;
            ReadCell(cell, labels, ref turns, 0);
            labels.Sort(AcrossTheControl);

            MessageBuilder message = new MessageBuilder();
            foreach (AgePrimitiveLabel label in labels)
            {
                string text = AgeText.Label(label);
                message.ListItem(
                    turns && IsCount(text)
                        ? ModStrings.Format(ModStrings.GalaxyTurnsRemaining, text)
                        : text
                );
            }

            return message.Build();
        }

        private static void ReadCell(
            AgeTransform widget,
            List<AgePrimitiveLabel> labels,
            ref bool turns,
            int depth
        )
        {
            // Content: which labels are collected into a cell's text.
            if (widget == null || depth > MaxCellDepth || !widget.Visible)
            {
                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (label != null && !string.IsNullOrEmpty(AgeText.Label(label)))
            {
                labels.Add(label);
            }

            turns = turns || IsTurnIcon(widget);

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                ReadCell(children[i], labels, ref turns, depth + 1);
            }
        }

        /// <summary>Whether the picture drawn here is the game's own turn symbol - which is what tells
        /// a number beside it apart from every other number a table can hold.</summary>
        private static bool IsTurnIcon(AgeTransform widget)
        {
            try
            {
                AgePrimitiveImage image = widget.GetComponent<AgePrimitiveImage>();
                Texture texture = image == null ? null : image.Texture;
                string key;
                return texture != null
                    && IconTable.TryKeyForPicture(texture.name, out key)
                    && key == ModStrings.IconTurn;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsCount(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsDigit(text[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static readonly Comparison<Line> AcrossTheRow = delegate(Line a, Line b)
        {
            return AgeLayout.ReadingOrder(a.Widget, b.Widget);
        };

        private static readonly Comparison<AgeTransform> DownTheTable = delegate(
            AgeTransform a,
            AgeTransform b
        )
        {
            return AgeLayout.TopThenLeft(a, b);
        };

        // ---- the table a popup stamped out of a prefab ----

        /// <summary>
        /// The table a popup filled by CLONING a line: the inspector's report, the laws that lapsed, the
        /// systems that lost population.
        ///
        /// These are the same thing to the player as the scrolling table <see cref="ReadSheet"/> finds -
        /// a list of things and a fact or two about each - but the game builds them the other way round:
        /// no scroll view, and instead a container the popup names in its own code and refills every
        /// refresh from one prefab. That container is what <see cref="TableWidgets"/> declares, per popup,
        /// and its visible children are the lines.
        ///
        /// Whether the columns are NAMED is the prefab's decision rather than the code's: the popup
        /// writes its captions into labels the prefab lays out above the container, and nothing in the
        /// class says whether it has any. So this asks the screen: a single band of two or more words
        /// drawn clear above the container, with nothing else of the popup's own drawn out there, and
        /// every line's pieces falling one to a caption. Where that band exists the table reads as a
        /// table, columns spoken as the edge crossed to reach them; where it does not, the popup keeps
        /// the rows it always had, each line joined into the one row it looks like.
        ///
        /// What the popup wrote UNDER the container is the table's footer - the inspector's report ends
        /// with what all of it came to - and reads as the full-width row it is drawn as, after the lines.
        /// </summary>
        private static Sheet ReadTableSheet(
            NotificationWindow window,
            List<Control> controls,
            List<Control> inside,
            AgeTransform words
        )
        {
            try
            {
                List<AgeTransform> tables = TableWidgets(window, controls);

                // One container, or the captions above one of them would be read as captions over all
                // of them. A popup that drew two tables keeps its rows.
                if (tables == null || tables.Count != 1)
                {
                    return null;
                }

                AgeTransform table = tables[0];

                // A control the popup captioned and drew OUTSIDE the table is content a table reading
                // would drop, exactly as for a scrolling one.
                foreach (Control control in inside)
                {
                    if (!AgeWidgets.Under(control.Widget, table))
                    {
                        return null;
                    }
                }

                // And a line the popup wired a click to is a control in its own right, walked in the band
                // it was drawn in. Where that band is a STRIP - a line the game drew inside the title or
                // the button bar - the line is already declared there, and reading it as a table too
                // would declare it twice. The rows stay.
                foreach (Control control in controls)
                {
                    if (AgeWidgets.Under(control.Widget, table) && !Has(inside, control.Widget))
                    {
                        return null;
                    }
                }

                List<AgeTransform> lines = TableRows(table);
                if (lines.Count == 0)
                {
                    return null;
                }

                List<Line> headers = null;
                List<List<Line>> footer = null;
                foreach (
                    List<Line> band in AgeLayout.Rows(Outside(window, controls, words, table), LineWidget)
                )
                {
                    int where = AgeLayout.Band(band[0].Widget, table);
                    if (where < 0)
                    {
                        if (headers != null)
                        {
                            // Two bands above: one of them is a heading rather than a set of columns, and
                            // nothing here can tell which. The rows are the safe reading.
                            return null;
                        }

                        headers = band;
                    }
                    else if (where > 0)
                    {
                        if (footer == null)
                        {
                            footer = new List<List<Line>>();
                        }

                        footer.Add(band);
                    }
                    else
                    {
                        // Words drawn level with the lines but outside them would be dropped.
                        return null;
                    }
                }

                if (headers == null || headers.Count < 2)
                {
                    return null;
                }

                headers.Sort(AcrossTheRow);

                List<SheetRow> rows = new List<SheetRow>();
                foreach (AgeTransform line in lines)
                {
                    AgeTransform[] cells = Columns(line, headers);
                    if (cells == null)
                    {
                        return null;
                    }

                    rows.Add(new SheetRow { Widget = line, Cells = cells });
                }

                return new Sheet { Headers = headers, Rows = rows, Footer = footer };
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for a prefab table threw: " + e);
                return null;
            }
        }

        /// <summary>The text the popup drew in its content area OUTSIDE its table - the captions over it,
        /// the totals under it.</summary>
        private static List<Line> Outside(
            NotificationWindow window,
            List<Control> controls,
            AgeTransform words,
            AgeTransform table
        )
        {
            List<Line> outside = new List<Line>();
            AgeTransform root = Root(window);
            if (root == null)
            {
                return outside;
            }

            List<Line> drawn = new List<Line>();
            Read(root, drawn, null, 0);

            List<AgeTransform> title = TitleBar(window, controls);
            List<AgeTransform> buttons = ButtonBar(controls);
            AgeTransform dossier = Dossier(window);
            foreach (Line line in drawn)
            {
                if (
                    InBody(line.Widget, title, buttons)
                    && !PartOf(line.Widget, controls)
                    && !IsWords(line, words)
                    && !AgeWidgets.Under(line.Widget, table)
                    && !AgeWidgets.Under(line.Widget, dossier)
                )
                {
                    outside.Add(line);
                }
            }

            return outside;
        }

        /// <summary>The lines of a prefab table: the children of the container the popup wrote something
        /// in. A line the game refilled with nothing - a clone it keeps around for the next turn and has
        /// hidden - is not a line.</summary>
        private static List<AgeTransform> TableRows(AgeTransform table)
        {
            List<AgeTransform> rows = new List<AgeTransform>();
            List<AgeTransform> children = table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child != null && AgeWidgets.Visible(child) && Draws(child, 0))
                {
                    rows.Add(child);
                }
            }

            rows.Sort(DownTheTable);
            return rows;
        }

        /// <summary>Every line of every prefab table this popup drew, for the reading that keeps rows: a
        /// line is one row whichever table it came from, so a popup with two of them (the improvements
        /// and the populations an obliterator destroyed) still reads a line at a time.</summary>
        private static List<AgeTransform> TableLines(NotificationWindow window)
        {
            List<AgeTransform> tables = TableWidgets(window, null);
            if (tables == null)
            {
                return null;
            }

            List<AgeTransform> lines = new List<AgeTransform>();
            for (int i = 0; i < tables.Count; i++)
            {
                lines.AddRange(TableRows(tables[i]));
            }

            return lines.Count == 0 ? null : lines;
        }

        /// <summary>The containers this popup fills with cloned lines, as the popup's own code names
        /// them, and only while the player can see them - a report panel a breakdown toggle has folded
        /// away draws nothing.</summary>
        private static List<AgeTransform> TableWidgets(
            NotificationWindow window,
            List<Control> controls
        )
        {
            Variant variant = VariantOf(window);
            if (variant == null || variant.Tables == null)
            {
                return null;
            }

            List<AgeTransform> tables = new List<AgeTransform>();
            List<AgeTransform> title = controls == null ? null : TitleBar(window, controls);
            List<AgeTransform> buttons = controls == null ? null : ButtonBar(controls);
            AgeTransform root = Root(window);
            foreach (AgeTransform table in variant.Tables(window))
            {
                // Flow control: a table the popup is not drawing must not be WALKED - its rows are the last notification's.
                if (table == null || !Painted(table, root))
                {
                    continue;
                }

                if (controls != null && !InBody(table, title, buttons))
                {
                    continue;
                }

                tables.Add(table);
            }

            return tables;
        }

    }
}

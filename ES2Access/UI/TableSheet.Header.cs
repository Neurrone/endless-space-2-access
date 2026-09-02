using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    public sealed partial class TableSheet
    {
        // ---- the sort headers ----

        /// <summary>One node per column heading, in the row the game drew them in - declared in the
        /// table's OWN stop, immediately above its rows, so the band is where it is drawn rather than a
        /// Tab stop of its own: Up from the first row reaches the headings and Down comes back. Either
        /// half of the sheet may be declared without the other, and in either order: both read the
        /// headings first.
        ///
        /// The band carries no position: "1 of 8" there counts the table's COLUMNS, which is not a place
        /// in a list. Where the player is in the table is said by the rows (<see cref="TableRow"/>).
        /// </summary>
        public void Headers(GraphBuilder builder, GuiTable table)
        {
            Read(table);
            if (_headers.Count == 0)
            {
                return;
            }

            GuiTable owner = table;
            int[] columns = BandColumns(table);
            builder.StartRow(positions: false);
            for (int i = 0; i < _headers.Count; i++)
            {
                GuiTableHeader header = _headers[i];
                AgeTransform widget = header.AgeTransform;
                string property = PropertyOf(header);
                NodeVtable vtable = GraphNodes.Button(
                    () => Caption(header),
                    () => AgeWidgets.Press(widget),
                    () => AgeWidgets.Operable(widget),
                    header.Tooltip
                );
                vtable.Announcements.Insert(
                    1,
                    GraphNodes.SelectedPart(() => SortedBy(owner, property))
                );
                // Which column this heading stands over, so that Up out of a row lands on the heading
                // of the column the player was in rather than on the first one
                // (<c>GraphBuilder.StitchModeBoundaries</c> pairs the seam by this number).
                vtable.Column = columns[i];
                // And searched by its own words: a heading is not a cell of the row below it, so the
                // one-result-per-row filter would drop every heading past the first from type-ahead.
                vtable.SearchesAsItself = true;
                AgeWidgets.PointAt(vtable, widget);
                builder.AddItem(Nodes.Drawn(
                    ControlId.For(header, _key + "header/" + property + "/" + i),
                    vtable,
                    header
                ));
                Filter(builder, owner, header, i, columns[i]);
            }

            builder.EndRow();
        }

        /// <summary>
        /// The funnel a column draws beside its caption, where the game is drawing one.
        ///
        /// Its own node, beside the sort node and never merged into it: they are two different things
        /// to do to one column - sort by it, or hide values of it - and the game draws them as two
        /// widgets (owner ruling: one row means a row of NODES). The game draws the funnel only for a
        /// column its table declared values to filter by (<c>GuiTableHeader.Refresh</c> :49-52), so
        /// most columns have no second node at all.
        ///
        /// A checkbox, because that is what the game made it: ticking it opens the column's menu of
        /// values and unticking it closes it again, both through the toggle's own handler. Named by the
        /// column's caption, and by nothing else - the prefab hangs no words on the funnel and the game
        /// has no name for the thing it does, so the alternative would be a word this mod invented.
        /// </summary>
        private void Filter(
            GraphBuilder builder,
            GuiTable table,
            GuiTableHeader header,
            int index,
            int column
        )
        {
            // Whether the game is drawing the funnel is the gate's question, asked of the same widget
            // the node below stands on.
            AgeControlToggle funnel = header.FilterToggle;
            if (funnel == null)
            {
                return;
            }

            GuiTableHeader it = header;
            GuiTable owner = table;
            AgeTooltip tooltip = AgeWidgets.Raw(funnel.AgeTransform);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => Caption(it),
                () => funnel.State,
                () => Open(owner, it),
                () => AgeWidgets.Operable(funnel.AgeTransform),
                tooltip
            );
            // A number no CELL has, and distinct per column. The band's seam with the rows below it is
            // paired column by column, and that pairing is dropped wholesale the moment two nodes of
            // the band claim one column (<c>GraphBuilder.ByColumn</c>) - so a funnel stamped with its
            // own column's number would have cost every column its heading on the way up out of a row.
            vtable.Column = -(column + 1);
            vtable.SearchesAsItself = true;
            AgeWidgets.Point(vtable, funnel);
            builder.AddItem(Nodes.Drawn(
                ControlId.For(funnel, _key + "filter/" + PropertyOf(header) + "/" + index),
                vtable,
                funnel
            ));
        }

        /// <summary>Tick or untick the funnel the way a click on it does, and tell the menu screen
        /// which column it is about to be showing - the game's menu is one panel per table and says
        /// nothing about which header opened it.</summary>
        private static void Open(GuiTable table, GuiTableHeader header)
        {
            ES2Access.Screens.TableFilterScreen.Opened(table, header);
            AgeWidgets.Toggle(header.FilterToggle);
        }

        /// <summary>
        /// The logical column each heading stands over, one per entry of <see cref="_headers"/>.
        ///
        /// Its own index, for every table but one: the game draws one heading per column, in the order
        /// a row lays its cells out, and <see cref="Rows"/> numbers those cells 0..N in that same order.
        /// A cell the screen reads as SEVERAL columns (<see cref="SplitCell"/>) is the exception - it
        /// pushes every column after it along - so a table with such a cell is measured against a real
        /// row instead of assumed. Only that table pays for the walk.
        /// </summary>
        private int[] BandColumns(GuiTable table)
        {
            int[] map = new int[_headers.Count];
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = i;
            }

            if (SplitCell == null)
            {
                return map;
            }

            List<GuiTableLine> lines = Lines(table);
            for (int l = 0; l < lines.Count; l++)
            {
                List<AgeTransform> cells = CellsOf(lines[l]);
                if (cells.Count < 2)
                {
                    continue;
                }

                Assign(map, HeaderFor(cells[0], 0), 0);
                int column = 0;
                for (int i = 1; i < cells.Count; i++)
                {
                    GuiTableHeader header = HeaderFor(cells[i], i);
                    IList<NodeVtable> parts = Split(lines[l], cells[i], header, AlwaysOn);
                    Assign(map, header, ++column);
                    if (parts != null)
                    {
                        column += parts.Count - 1;
                    }
                }

                return map;
            }

            return map;
        }

        private void Assign(int[] map, GuiTableHeader header, int column)
        {
            for (int i = 0; i < _headers.Count; i++)
            {
                if (ReferenceEquals(_headers[i], header))
                {
                    map[i] = column;
                    return;
                }
            }
        }

        /// <summary>The headings the table is drawing, which is what pairs a caption to a column.
        /// Called by both <see cref="Headers"/> and <see cref="Rows"/>; public for a screen that reads a
        /// heading for something else.</summary>
        public void Read(GuiTable table)
        {
            _headers.Clear();
            try
            {
                AgeTransform headers = table == null ? null : table.HeadersTable;
                List<AgeTransform> children = headers == null ? null : headers.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    GuiTableHeader header =
                        child == null ? null : child.GetComponent<GuiTableHeader>();
                    // Flow control and pairing input: the count decides whether a heading band is
                    // opened at all, and this list's ORDER is what pairs each caption to a column
                    // number (BandColumns) - a heading the table is not drawing would shift every
                    // column after it.
                    if (header != null && AgeWidgets.Visible(child))
                    {
                        _headers.Add(header);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("table: reading the column headings threw: " + e);
            }
        }

        private static bool SortedBy(GuiTable table, string property)
        {
            try
            {
                return !string.IsNullOrEmpty(property)
                    && table.CurrentSortPropertyName == property;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

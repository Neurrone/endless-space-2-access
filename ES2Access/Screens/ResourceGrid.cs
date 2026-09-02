using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// One of the game's resource lattices, read as the table it is drawn as: a row of family icons
    /// across the top, then the resources of each family laid out down the icon's column.
    ///
    /// The game draws this same lattice in four places - the economy page's luxuries and strategics,
    /// and the same two halves of the system-development window - so the layout reading lives here and
    /// each screen supplies only what one of its own cells is (<see cref="Reader"/>). What is generic:
    ///
    /// - <b>The icons ARE the columns' headings</b> - measured: each sits centred over one column of
    ///   items. Which column a cell is in is therefore measured off the HEADINGS
    ///   (<see cref="ColumnOf"/>), never off a count of the drawn cells, because the lattice is SPARSE:
    ///   the game keeps every resource in the table and fades the ones the empire has nothing of, so
    ///   counting only the drawn ones would shift every column after the first hole.
    /// - <b>The families are a real HEADER ROW as well as the columns' captions</b> (owner ruling
    ///   2026-08-21, reversing his own 2026-08-19 "column headers and nothing else"). A family is still
    ///   what the player crosses an edge into and what a landing in a column is announced by
    ///   (<see cref="NodeVtable.ColumnHeader"/>, which <see cref="GraphSheet.NamedRows"/> being false
    ///   turns on) - and it is ALSO a node, one per drawn icon, in a row above the first line of the
    ///   lattice (<see cref="Headings"/>). Up from a cell reaches its own column's heading and Down comes
    ///   back, which is the shape every sort-header band in this mod has (<c>TableSheet.Headers</c>)
    ///   minus the press: these headings sort nothing, so nothing is wired to Enter and no state word is
    ///   said. What the heading node is FOR is the family's own sentence about what the family does
    ///   ("This family of resource improves Food when used in System Development"): it lives on the
    ///   heading's own tooltip, so it is declared where the game hung it and reachable from the one node
    ///   that IS the family, rather than repeated into the review buffer of every cell of the column.
    ///   One node per icon and never one node for the band: a node carries one tooltip, so a merged band
    ///   would drop seven of the eight sentences while still sounding complete.
    /// - <b>A faded cell is still a cell</b>, saying the word for empty under the caption its edge
    ///   already said: dropping it would take the column out from under a player walking down it, which
    ///   is the one thing a table is for. A line the game faded WHOLE is not a row of empties - it is
    ///   not a line the eye sees, so it is not a row (<see cref="Drawn"/>).
    /// - <b>The rows have no NAME</b> - column 0 is another resource, not a heading - so the sheet is
    ///   told so and no vertical crossing announces a row, and each cell searches by its own words.
    ///
    /// The cells themselves stay the host screen's: the economy page's are read-only holdings, the
    /// development window's are pickable components with the game's own click on them, and the table
    /// machinery never touches a vtable it was handed beyond the stamps <see cref="GraphSheet"/> puts
    /// on every cell.
    ///
    /// One instance per grid, reused across builds rather than allocated per frame: Build runs every
    /// tick.
    /// </summary>
    internal sealed class ResourceGrid
    {
        /// <summary>What one drawn cell of the lattice is, in the host screen's terms - null for a
        /// widget the game is not drawing, which is what makes the place a hole.</summary>
        internal delegate NodeVtable Reader(AgeTransform widget);

        /// <summary>Where each column is drawn across the screen, taken off the headings - which is
        /// what tells a cell which family it belongs to whatever the game faded out of the line above
        /// it.</summary>
        private readonly List<float> _centres = new List<float>();

        /// <summary>The family icon the game drew over each column, or null where it drew none - the
        /// widget the heading node is read off and the pointer is aimed at. A heading the game did not
        /// draw leaves a null rather than shifting the ones after it: the index IS the column.</summary>
        private readonly List<AgeTransform> _headings = new List<AgeTransform>();

        /// <summary>The heading tooltip of each family, by column - the sentence saying what that
        /// family of resource does. Kept beside <see cref="_headings"/> because the tooltip is what the
        /// heading node is declared with and what the pointer must be aimed through.</summary>
        private readonly List<AgeTooltip> _tips = new List<AgeTooltip>();

        // The drawn cells, and which column and which line of the lattice each was drawn in - three
        // parallel lists, because a hole in the lattice is an index the cell list does not have.
        private readonly List<NodeVtable> _cells = new List<NodeVtable>();
        private readonly List<int> _columns = new List<int>();
        private readonly List<int> _lines = new List<int>();

        private readonly List<KeyValuePair<int, NodeVtable>> _row =
            new List<KeyValuePair<int, NodeVtable>>();

        /// <summary>Start a read of this grid and measure its columns off the heading band, answering
        /// how many the game drew. Everything a previous frame measured is dropped here, so a throw
        /// further down leaves an empty grid rather than last frame's cells.</summary>
        internal int Columns(AgeTransform headers)
        {
            _centres.Clear();
            _headings.Clear();
            _tips.Clear();
            _cells.Clear();
            _columns.Clear();
            _lines.Clear();
            IList<AgeTransform> children = headers == null ? null : headers.Children;
            // Banding input: the lattice's heading row and every column under it are laid out from
            // these rectangles, before a cell exists for the gate to drop.
            bool band = children != null && AgeWidgets.Visible(headers);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child == null)
                {
                    break;
                }

                UnityEngine.Rect at = child.GetGlobalPosition();
                bool drawn = band && SettingRows.Drawn(child);
                _centres.Add(at.x + at.width * 0.5f);
                _headings.Add(drawn ? child : null);
                _tips.Add(drawn ? AgeWidgets.Raw(child) : null);
            }

            return _centres.Count;
        }

        /// <summary>Read the lattice's cells, in the order the game laid them out.</summary>
        internal void Read(AgeTransform items, Reader read)
        {
            IList<AgeTransform> children = items == null ? null : items.Children;
            int line = 0;
            int previous = -1;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                int column = ColumnOf(children[i]);
                // A line of the lattice ends where the next cell steps back to a column at or before
                // the one just read - the game lays them out left to right, and the fading that empties
                // a cell never moves the ones beside it.
                if (column <= previous)
                {
                    line++;
                }

                previous = column;
                NodeVtable cell = read(children[i]);
                if (cell != null)
                {
                    _cells.Add(cell);
                    _columns.Add(column);
                    _lines.Add(line);
                }
            }
        }

        /// <summary>
        /// The grid as the table it is drawn as: the row of family headings, then a row per drawn line
        /// of the lattice, <paramref name="columns"/> wide.
        ///
        /// Tab into the box lands on a ROW of the table and not on the headings above it, the same
        /// landing every other table in the mod has - and the landing says which column it is in, which
        /// is what a table with no row names owes a player who arrives in it. The headings are reached
        /// the way the eye reaches them, with Up from the first line.
        ///
        /// A grid whose heading band the game drew nothing in has no columns to be a table of, and
        /// falls back to the drawn cells one per row.
        /// </summary>
        internal void Emit(GraphBuilder builder, string[] columns, object stop, string title)
        {
            int width = columns == null ? 0 : columns.Length;
            GraphSheet sheet = new GraphSheet(builder, stop + "/");
            sheet.NamedRows = false;
            sheet.Region(title, width == 0 ? null : columns);
            Headings(builder, columns, stop);
            if (width == 0)
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    sheet.Line(_cells[i]);
                }
            }
            else
            {
                int lines = 0;
                for (int i = 0; i < _lines.Count; i++)
                {
                    lines = Math.Max(lines, _lines[i] + 1);
                }

                // The drawn cells laid out into the lattice ONCE, so the emit below indexes a place
                // rather than searching for it. Both questions - is this line drawn at all, and what
                // is in this place - used to walk every cell the game drew, for every place in a grid
                // that is lines by columns: the luxuries grid alone paid that several hundred times a
                // frame for a table whose whole content is one pass.
                NodeVtable[] lattice = new NodeVtable[lines * width];
                bool[] drawn = new bool[lines];
                for (int i = 0; i < _cells.Count; i++)
                {
                    int at = _lines[i];
                    if (at < 0 || at >= lines)
                    {
                        continue;
                    }

                    // A line the game faded whole - the beginner save's third row of luxuries - is not
                    // a row of eight empties, it is not a row. One cell drawn anywhere on it is what
                    // makes it one, whatever column that cell is in.
                    drawn[at] = true;
                    int side = _columns[i];
                    if (side >= 0 && side < width)
                    {
                        lattice[(at * width) + side] = _cells[i];
                    }
                }

                for (int line = 0; line < lines; line++)
                {
                    if (!drawn[line])
                    {
                        continue;
                    }

                    NodeVtable primary = null;
                    _row.Clear();
                    for (int column = 0; column < width; column++)
                    {
                        NodeVtable cell = lattice[(line * width) + column] ?? EmptyCell();
                        if (column == 0)
                        {
                            primary = cell;
                        }
                        else
                        {
                            _row.Add(new KeyValuePair<int, NodeVtable>(column, cell));
                        }
                    }

                    sheet.RowAt(primary, null, _row);
                }
            }

            sheet.Finish();
            builder.LandStopOn(sheet.FirstRow);
            builder.SetRegion(null);
        }

        /// <summary>
        /// The row of family headings, one node per icon the game drew - declared in the table's OWN
        /// stop immediately above its first line, so the band is where it is drawn rather than a Tab
        /// stop of its own.
        ///
        /// Each node says its family's name and carries the heading's own tooltip, which is the
        /// family's sentence and lives here and nowhere else. Each is stamped with the column it stands
        /// over (<see cref="NodeVtable.Column"/>), which is what
        /// <c>GraphBuilder.StitchModeBoundaries</c> pairs the seam by: Up out of a cell reaches the
        /// heading of the column the player was in rather than the first one.
        ///
        /// Nothing is wired to Enter: these headings sort nothing and the game gives their icons no
        /// click, so the row is a row of readouts and a press on one answers with nothing - which is
        /// what a click there does. The band carries no position either: "1 of 8" would count the
        /// table's COLUMNS, which is not a place in a list.
        ///
        /// Searched by their own words (<see cref="NodeVtable.SearchesAsItself"/>): every node past
        /// column 0 is otherwise dropped from type-ahead as a duplicate of a row name, and here there
        /// are no row names and a family IS a thing to type at.
        ///
        /// A column the game drew no icon over, or one whose icon has neither a name nor a sentence, is
        /// no heading: nothing is declared for it, and a cell of that column crosses the seam to the
        /// first heading instead. That is the same fallback a column with no caption already gets from
        /// the sheet, which labels its crossings with the same array.
        /// </summary>
        private void Headings(GraphBuilder builder, string[] columns, object stop)
        {
            int width = columns == null ? 0 : columns.Length;
            bool open = false;
            for (int i = 0; i < width && i < _headings.Count; i++)
            {
                AgeTransform widget = _headings[i];
                string name = columns[i];
                AgeTooltip tip = Tip(i);
                if (widget == null || (string.IsNullOrEmpty(name) && tip == null))
                {
                    continue;
                }

                string said = name;
                NodeVtable vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => said),
                    },
                    Sections = GraphNodes.Sections(null, tip),
                    Column = i,
                    SearchesAsItself = true,
                };
                AgeWidgets.PointAt(vtable, widget, tip);
                if (!open)
                {
                    builder.StartRow(positions: false);
                    open = true;
                }

                ScrollIntoView.Anchor(vtable, widget);
                // Keyed by the column, because the band is a fixed set of slots - but the icon the
                // heading was read off is drawn, and a grid whose band the game switches off column by
                // column is exactly what the gate's chain walk is for.
                builder.AddItem(
                    Nodes.Drawn(ControlId.Structural(stop + "/family/" + i), vtable, widget)
                );
            }

            if (open)
            {
                builder.EndRow();
            }
        }

        /// <summary>Which column a cell is drawn in: the heading it sits under.</summary>
        private int ColumnOf(AgeTransform widget)
        {
            if (widget == null)
            {
                return -1;
            }

            UnityEngine.Rect at = widget.GetGlobalPosition();
            float centre = at.x + at.width * 0.5f;
            int best = -1;
            float nearest = 0f;
            for (int i = 0; i < _centres.Count; i++)
            {
                float distance = Math.Abs(_centres[i] - centre);
                if (best < 0 || distance < nearest)
                {
                    best = i;
                    nearest = distance;
                }
            }

            return best;
        }

        private AgeTooltip Tip(int column)
        {
            return column >= 0 && column < _tips.Count ? _tips[column] : null;
        }


        /// <summary>A place in the lattice the game is drawing nothing in, in the words every empty
        /// cell in this mod uses. Which family the hole is in is said by the column it was crossed into
        /// (<see cref="NodeVtable.ColumnHeader"/>), and what that family DOES is a step up on the
        /// heading itself.</summary>
        private static NodeVtable EmptyCell()
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.ValuePart(() => ModStrings.Get(ModStrings.NavCellEmpty)),
                },
            };
        }
    }
}

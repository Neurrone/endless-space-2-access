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
    /// - <b>The families are column headers and nothing else</b> (owner ruling 2026-08-19, replacing a
    ///   walkable band of icons in front of the grid). A family is what the player crosses an edge into
    ///   and what a landing in a column is announced by (<see cref="NodeVtable.ColumnHeader"/>, which
    ///   <see cref="GraphSheet.NamedRows"/> being false turns on). The one thing the band was holding
    ///   that a caption cannot say - each family's sentence about what it does - moves into the review
    ///   buffer of every cell of that family's column (<see cref="FamilySection"/>), which is the only
    ///   surface it is reachable from once the band is gone.
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
        /// widget the game is not drawing, which is what makes the place a hole.
        /// <paramref name="familyTip"/> is the heading tooltip of the column the cell landed in, which
        /// the cell is expected to carry as a review section (<see cref="FamilySection"/>).</summary>
        internal delegate NodeVtable Reader(AgeTransform widget, AgeTooltip familyTip);

        /// <summary>Where each column is drawn across the screen, taken off the headings - which is
        /// what tells a cell which family it belongs to whatever the game faded out of the line above
        /// it.</summary>
        private readonly List<float> _centres = new List<float>();

        /// <summary>The heading tooltip of each family, by column - the sentence saying what that
        /// family of resource does, which the table reading has no heading row to keep it on. A heading
        /// the game did not draw leaves a null rather than shifting the ones after it: the index IS the
        /// column.</summary>
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
            _tips.Clear();
            _cells.Clear();
            _columns.Clear();
            _lines.Clear();
            IList<AgeTransform> children = headers == null ? null : headers.Children;
            bool band = children != null && AgeWidgets.Visible(headers);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child == null)
                {
                    break;
                }

                UnityEngine.Rect at = child.GetGlobalPosition();
                _centres.Add(at.x + at.width * 0.5f);
                _tips.Add(band && SettingRows.Drawn(child) ? AgeWidgets.Raw(child) : null);
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
                NodeVtable cell = read(children[i], Tip(column));
                if (cell != null)
                {
                    _cells.Add(cell);
                    _columns.Add(column);
                    _lines.Add(line);
                }
            }
        }

        /// <summary>
        /// The grid as the table it is drawn as: a row per drawn line of the lattice,
        /// <paramref name="columns"/> wide, the families its columns and nothing else.
        ///
        /// Tab into the box lands on a ROW of the table, the same landing every other table in the mod
        /// has - and the landing says which column it is in, which is what a table with no row names
        /// owes a player who arrives in it.
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

                for (int line = 0; line < lines; line++)
                {
                    if (!Drawn(line))
                    {
                        continue;
                    }

                    NodeVtable primary = null;
                    _row.Clear();
                    for (int column = 0; column < width; column++)
                    {
                        NodeVtable cell = CellAt(line, column) ?? EmptyCell(Tip(column));
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
        /// What the family heading says about its column, as a section every cell UNDER that heading
        /// carries - reviewable, never spoken.
        ///
        /// The sentence ("This family of resource improves Food when used in System Development") is
        /// the one thing the heading band was holding that the column caption does not say, and with
        /// the band gone this is where it lives: the player reviews it from whichever cell of the
        /// column they are standing on (owner ruling 2026-08-19). Not an
        /// <see cref="TooltipMode.Indicate"/> section - it is not this control's own hover tooltip, so
        /// the pointer is never aimed at it and the tooltip-parity audit does not hold the cell to it.
        /// </summary>
        internal static NodeSection FamilySection(AgeTooltip tip)
        {
            Func<IList<string>> lines = GraphNodes.TooltipDetails(tip);
            return lines == null ? null : new NodeSection(lines, TooltipMode.None);
        }

        /// <summary>One more section after the ones a control declared, dropping a null on either
        /// side.</summary>
        internal static IList<NodeSection> Append(IList<NodeSection> sections, NodeSection extra)
        {
            if (extra == null)
            {
                return sections;
            }

            List<NodeSection> all = new List<NodeSection>(
                sections == null ? 1 : sections.Count + 1
            );
            for (int i = 0; sections != null && i < sections.Count; i++)
            {
                all.Add(sections[i]);
            }

            all.Add(extra);
            return all;
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

        /// <summary>Whether the eye sees this line of the lattice at all. A line the game faded whole -
        /// the beginner save's third row of luxuries - is not a row of eight empties, it is not a row.
        /// </summary>
        private bool Drawn(int line)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                if (_lines[i] == line)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The cell the game drew at one place in the lattice, or null where it faded it out.
        /// </summary>
        private NodeVtable CellAt(int line, int column)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                if (_lines[i] == line && _columns[i] == column)
                {
                    return _cells[i];
                }
            }

            return null;
        }

        /// <summary>A place in the lattice the game is drawing nothing in, in the words every empty
        /// cell in this mod uses - carrying its column's family sentence like every other cell of that
        /// column, because which family the hole is in is the one thing there is to know about it.
        /// </summary>
        private static NodeVtable EmptyCell(AgeTooltip familyTip)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.ValuePart(() => ModStrings.Get(ModStrings.NavCellEmpty)),
                },
                Sections = GraphNodes.Sections(FamilySection(familyTip)),
            };
        }
    }
}

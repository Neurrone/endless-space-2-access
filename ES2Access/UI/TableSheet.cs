using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// One of the game's <c>GuiTable</c>s as a screen-reader table.
    ///
    /// <c>GuiTable</c> is a single piece of machinery the game points at a dozen different column sets
    /// (<c>Public/Gui/GuiElements[Tables].xml</c>): the system-selection modal and the Empire screen's
    /// systems tab bind the SAME <c>SystemListTable</c>; the Military screen's fleets, the
    /// fleet-selection modal and the hero list bind <c>FleetListTable</c> and
    /// <c>HeroCompleteListTable</c>; the marketplace binds four more. Every one of them has the same
    /// parts - a headers band whose captions come from <c>%&lt;TableName&gt;&lt;ColumnName&gt;Title</c>
    /// and whose click sorts (<c>GuiTableHeader.Refresh</c> :23-52, <c>OnClickCb</c> :55-62), a pool of
    /// lines each carrying one cell per column, a single selected line, and rows the opener has DISABLED
    /// with its reason on the line's tooltip. So the reading is written once, here, and a screen says
    /// only what its own table means.
    ///
    /// What a table reads as (all of it inherited, none of it per screen):
    ///
    /// - <b>The headers band</b> is a row of buttons; Enter is the header's own sort click, and the
    ///   column the table is currently sorted by reads "selected", watched live - which is also the only
    ///   feedback a sort press has, since the game answers it by reordering rows and nothing else. A
    ///   column the table forbids sorting on is drawn disabled (<c>DisableSorting</c>) and reads so.
    /// - <b>The rows</b> are a <see cref="GraphSheet"/>: up and down walk the rows down the NAME column,
    ///   left and right walk one row's figures, and a column's own caption is the spoken edge the player
    ///   crosses to reach it rather than a word every cell repeats. The name cell says the name, whether
    ///   the row is the one selected, and carries the whole row in its review buffer - a row of thirteen
    ///   figures in front of "not selected" is thirteen figures the player sits through to hear the one
    ///   word they are stepping the list for.
    /// - <b>Selecting</b> is the game's own select-then-act and is copied rather than shortened: Enter on
    ///   a row is the row's own click, which selects it and enables whatever the screen's Confirm is.
    ///   The game also commits on a DOUBLE click and that gesture is deliberately not wired here - a
    ///   single Enter that both picked and committed would make every pass over the list a decision. A
    ///   table bound <c>canSelect: false</c> (<c>GuiTable.Bind</c> :130, which is the flag
    ///   <c>LinesTable.Enable</c> records) has rows that are not choices at all: they read as plain
    ///   lines, not radios, and Enter does nothing, because that is what a click does.
    /// - <b>A refused row</b> - one the opener will not accept - is drawn with its line transform
    ///   switched off and the game's sentence for why on the line's tooltip
    ///   (<c>GuiTable.Refresh(objects, disabledObjects)</c> :246-278 and the
    ///   <c>GuiTableEntry.OnBind</c> that writes <c>line.AgeTransform.Enable</c>, :22-27). It is declared
    ///   REFUSING and says that sentence: the same treatment every blocked control in the mod gets.
    ///   A read-only table is NOT a table of refusals - the <c>canSelect</c> flag lives on
    ///   <c>LinesTable</c>, so refusal is asked of the LINE's own flag plus the chain above the table,
    ///   never of the chain through <c>LinesTable</c>.
    /// - <b>Empty cells</b> are still declared. Every column is read even where it is drawing nothing -
    ///   a cell that dropped out of a row would shift the columns under the player and take
    ///   up-and-down's column with them - and an empty one reads the word for empty under the caption its
    ///   edge already said.
    ///
    /// Five hooks are left to the screen, because only the screen knows the answers:
    /// <see cref="ReadCell"/> replaces a whole cell where the game drew a CONTROL in it rather than a
    /// figure (the systems table's automation drop list); <see cref="ReadValue"/> replaces only what a
    /// cell SAYS, for a column that draws no words at all (the politics table's support icons, whose value
    /// is on their own tooltip); <see cref="ActivateCell"/> replaces
    /// just what Enter does on a cell, which is how an interactive-cells table
    /// (<c>GuiTable.Bind(interactiveCells: true)</c>) offers the cell's own button - and note that such a
    /// button is the FIRST half of a two-step gesture, so what a screen hands back is
    /// <see cref="AgeWidgets.PressPropagating"/>, never a bare press; <see cref="Decorate"/> adds the
    /// gestures the game gives a row beyond its click; <see cref="NameColumn"/> names a column whose
    /// heading draws no caption; and <see cref="RowDetails"/> adds what a row draws inside its name cell
    /// beyond the name. Six, then - all six OPTIONAL, and nothing here presses anything on a screen's
    /// behalf.
    ///
    /// One instance per table per screen, kept across builds: its two scratch lists are reused, and
    /// <c>Build</c> runs every frame.
    /// </summary>
    public sealed class TableSheet
    {
        /// <summary>The object a row STANDS FOR - the system, the fleet - which is what identifies the
        /// row. The wrapper a table binds is rebuilt on every refresh and the line widget is pooled and
        /// re-sorted, so neither can key a row: a cursor keyed on the widget acts on a different item a
        /// frame later. It doubles as the test for whether a pooled line is a real row.</summary>
        public delegate object RowObject(GuiTableLine line);

        /// <summary>What the row is called when its name column draws nothing.</summary>
        public delegate string RowLabel(GuiTableLine line);

        /// <summary>A cell the screen reads itself, for a column the game draws a control in. Null for
        /// every other column, which is the shared value cell.</summary>
        public delegate NodeVtable CellReader(
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header,
            Func<bool> enabled
        );

        /// <summary>What Enter does on a cell, for a table whose cells carry their own buttons. Null
        /// means the row's own click, which is what Enter does on the name.</summary>
        public delegate Action CellActivation(GuiTableLine line, AgeTransform cell);

        /// <summary>What a column is CALLED, for a table whose heading draws no usable caption of its
        /// own. Null for the ordinary case, which is the caption the game wrote into the heading.
        /// </summary>
        public delegate string ColumnCaption(GuiTableHeader header);

        /// <summary>What a cell SAYS, for a column whose value is not in the widgets it draws. Null for
        /// the ordinary cell, whose value is what it draws.</summary>
        public delegate string CellValue(GuiTableHeader header, AgeTransform cell);

        /// <summary>What a row says BEYOND its columns - lines the game draws inside the row's own name
        /// cell under the name. Null for a table whose rows are only their columns.</summary>
        public delegate IList<string> RowExtras(GuiTableLine line);

        /// <summary>
        /// How deep inside a cell to look for what it is drawing.
        ///
        /// Two, measured against the systems table: every word and every icon a cell shows sits at one
        /// or two levels down (the status cell's label under its group, the planet circles under theirs).
        /// The third level exists and is not part of the cell - it is the automation drop list's closed
        /// POPUP, whose seven entries would otherwise be read out as though the cell were showing all of
        /// them at once. A table that draws deeper cells raises it and says what it measured.
        /// </summary>
        public int MaxCellDepth = 2;

        /// <summary>See <see cref="RowLabel"/>. Unset is legal for a table whose name column always
        /// draws the name.</summary>
        public RowLabel RowName;

        /// <summary>See <see cref="CellReader"/>.</summary>
        public CellReader ReadCell;

        /// <summary>See <see cref="CellActivation"/>.</summary>
        public CellActivation ActivateCell;

        /// <summary>The gestures the game gives a row beyond its click - the Military screen's fleet row
        /// answers the table's own double click on a separate key. Handed the row's finished vtable.
        /// </summary>
        public Action<GuiTableLine, NodeVtable> Decorate;

        /// <summary>
        /// See <see cref="ColumnCaption"/>. Unset is the ordinary case.
        ///
        /// It exists because a column's caption is not always IN the heading: the politics table draws a
        /// population's portrait and nothing else, and leaves the raw <c>%…Title</c> key in the label for
        /// a string the game never wrote - parked text, which is not a caption to speak. Whether a
        /// heading's picture has a name and where, is the screen's knowledge, so the screen answers it.
        /// </summary>
        public ColumnCaption NameColumn;

        /// <summary>
        /// See <see cref="CellValue"/>. Unset is the ordinary case.
        ///
        /// It exists for the politics table, whose cells draw an ICON and no figure: how strongly one
        /// party's events move another's score is written only on the cell's own tooltip ("Weak support").
        /// That cannot be a general rule - the systems table hangs a COLUMN EXPLANATION on cells it draws
        /// empty, and reading that as a value would answer "Hero" with a sentence about heroes - so which
        /// it is stays the screen's answer.
        ///
        /// Where it answers, the cell's own tooltip is NOT declared as a second surface: its words are
        /// already the value, and announcing them twice is the buffer and the readout saying one thing
        /// each. Its answer is used everywhere a cell's value is read - the cell, the cell's buffer and
        /// the whole-row buffer - so no surface can disagree with another.
        /// </summary>
        public CellValue ReadValue;

        /// <summary>See <see cref="RowExtras"/>. Read BEFORE the column facts, which is the order they
        /// are drawn in - the extras sit inside the leftmost cell.</summary>
        public RowExtras RowDetails;

        private readonly string _key;
        private readonly RowObject _rowRef;
        private readonly List<GuiTableHeader> _headers = new List<GuiTableHeader>();
        private readonly List<AgeTransform> _cells = new List<AgeTransform>();

        /// <param name="keyPrefix">Prefixes every id this sheet declares; one per table per screen.
        /// </param>
        /// <param name="rowRef">See <see cref="RowObject"/>. Required, because a table with index keys
        /// silently rebadges the next row as "the same control" the moment one is removed.</param>
        public TableSheet(string keyPrefix, RowObject rowRef)
        {
            _key = keyPrefix;
            _rowRef = rowRef;
        }

        // ---- the sort headers ----

        /// <summary>One node per column heading, in the row the game drew them in. Either half of the
        /// sheet may be declared without the other, and in either order: both read the headings first.
        /// </summary>
        public void Headers(GraphBuilder builder, GuiTable table)
        {
            Read(table);
            if (_headers.Count == 0)
            {
                return;
            }

            GuiTable owner = table;
            builder.StartRow();
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
                AgeWidgets.PointAt(vtable, widget);
                builder.AddItem(
                    ControlId.Referenced(header, _key + "header/" + property + "/" + i),
                    vtable
                );
            }

            builder.EndRow();
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

        // ---- the rows ----

        /// <summary>
        /// Every row the table is showing, a row each, a cell at a time. Returns the sheet, whose
        /// <c>FirstRow</c> is the id a screen whose content IS the table sends focus to.
        ///
        /// <paramref name="title"/> is what the region is called - the window's own heading, the panel's
        /// caption - spoken once with the table's role as the player enters it.
        /// </summary>
        public GraphSheet Rows(GraphBuilder builder, GuiTable table, string title)
        {
            // Read again rather than relying on Headers having run: a screen that declares the band
            // elsewhere, or not at all, would otherwise get a table whose columns are nameless, and the
            // only symptom would be silent edges.
            Read(table);
            List<GuiTableLine> lines = Lines(table);
            GraphSheet sheet = new GraphSheet(builder, _key);
            sheet.Region(title, Columns(lines));
            for (int l = 0; l < lines.Count; l++)
            {
                GuiTableLine line = lines[l];
                ReadCells(line);
                if (_cells.Count == 0)
                {
                    continue;
                }

                List<KeyValuePair<int, NodeVtable>> cells =
                    new List<KeyValuePair<int, NodeVtable>>();
                for (int i = 1; i < _cells.Count; i++)
                {
                    AgeTransform cell = _cells[i];
                    cells.Add(
                        new KeyValuePair<int, NodeVtable>(
                            i,
                            CellVtable(table, line, cell, HeaderFor(cell, i))
                        )
                    );
                }

                sheet.RowAt(PrimaryVtable(table, line, _cells[0]), _rowRef(line), cells);
            }

            sheet.Finish();
            return sheet;
        }

        /// <summary>The captions the sheet speaks when the player crosses into a column, read off a
        /// real row: which heading is over which column is the pairing <see cref="HeaderFor"/> makes,
        /// and it survives a re-sort. Every row of one table has the same columns, so the first one
        /// that has any answers for all of them.</summary>
        private string[] Columns(List<GuiTableLine> lines)
        {
            for (int l = 0; l < lines.Count; l++)
            {
                List<AgeTransform> cells = CellsOf(lines[l]);
                if (cells.Count < 2)
                {
                    continue;
                }

                string[] columns = new string[cells.Count - 1];
                for (int i = 1; i < cells.Count; i++)
                {
                    columns[i - 1] = Caption(HeaderFor(cells[i], i));
                }

                return columns;
            }

            return null;
        }

        /// <summary>
        /// The row itself, which is the column the game draws the row's NAME in: the name, and - where
        /// the table lets the player choose - whether this row is the one taken. The figures are the
        /// cells beside it.
        ///
        /// Both membership words are spoken, not just "selected": this is a list the player is choosing
        /// from, and a row that says nothing when it is out of the selection leaves them counting
        /// silences. The whole row is still one keypress away in the review buffer, where it is read at
        /// read time rather than watched: working it out means walking every cell and nothing about them
        /// changes under a standing cursor.
        /// </summary>
        private NodeVtable PrimaryVtable(GuiTable table, GuiTableLine line, AgeTransform cell)
        {
            GuiTable owner = table;
            GuiTableLine row = line;
            AgeTransform widget = line.AgeTransform;
            AgeTransform name = cell;
            Func<bool> enabled = Operable(table, line);
            NodeVtable vtable;
            if (Selectable(table))
            {
                vtable = GraphNodes.SelectionItem(
                    () => RowText(row, name),
                    () => Selected(owner, row),
                    null,
                    () => AgeWidgets.Toggle(row.SelectionToggle),
                    enabled,
                    line.Tooltip,
                    null,
                    () => RowFacts(row)
                );
                AgeWidgets.Point(vtable, row.SelectionToggle, line.Tooltip, widget);
            }
            else
            {
                // A table the game will not let the player pick from: the rows are readouts, and a
                // radio word on them would offer a choice that is not there.
                vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => RowText(row, name)),
                        GraphNodes.DisabledPart(enabled),
                    },
                    Sections = GraphNodes.Sections(() => RowFacts(row), line.Tooltip),
                };
                AgeWidgets.PointAt(vtable, widget);
            }

            GraphNodes.AddRefusal(vtable, line.Tooltip, enabled);

            if (Decorate != null)
            {
                Decorate(row, vtable);
            }

            return vtable;
        }

        /// <summary>One column of a row: what it is showing, with the game's own tooltip for the column
        /// behind it. It does NOT say its heading - the sheet says that as the edge the player crossed
        /// to get here, so a cell reached sideways is named once and a cell reached from the row above is
        /// named by the row it belongs to. Enter here is the row's click, the same as on the name - a
        /// player who has arrowed across to compare a figure should not have to arrow back to pick the
        /// row they just compared - unless the screen says the cell has a click of its own.</summary>
        private NodeVtable CellVtable(
            GuiTable table,
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header
        )
        {
            GuiTable owner = table;
            GuiTableLine row = line;
            AgeTransform it = cell;
            GuiTableHeader heading = header;
            Func<bool> selected = () => Selected(owner, row);
            Func<bool> enabled = Operable(table, line);
            NodeVtable vtable = ReadCell == null ? null : ReadCell(row, it, heading, enabled);
            if (vtable == null)
            {
                vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.ValuePart(() => Text(heading, it)),
                    },
                    Sections = GraphNodes.Sections(
                        () => CellFacts(heading, it),
                        Supplied(heading, it) ? null : TooltipOf(it)
                    ),
                };
                Action own = ActivateCell == null ? null : ActivateCell(row, it);
                if (own != null)
                {
                    Action act = own;
                    vtable.OnActivate = () =>
                    {
                        if (enabled())
                        {
                            act();
                        }
                    };
                }
                else if (Selectable(table))
                {
                    vtable.OnActivate = () =>
                    {
                        if (enabled())
                        {
                            AgeWidgets.Toggle(row.SelectionToggle);
                        }
                    };
                }

                if (Selectable(table))
                {
                    vtable.StateText = () =>
                        selected() ? ModStrings.Get(ModStrings.NavSelected) : null;
                }
            }

            if (Selectable(table))
            {
                vtable.Announcements.Add(GraphNodes.SelectedPart(selected));
            }

            vtable.Announcements.Add(GraphNodes.DisabledPart(enabled));
            vtable.SearchText = () => RowText(row, null);
            AgeWidgets.PointAt(vtable, it);
            return vtable;
        }

        /// <summary>The row for the review buffer: one line per column, empties included - a buffer is
        /// walked column by column and a missing line is a column the player would count wrong.
        /// </summary>
        private IList<string> RowFacts(GuiTableLine line)
        {
            List<AgeTransform> cells = CellsOf(line);
            List<string> lines = new List<string>();
            Extras(line, lines);
            for (int i = 1; i < cells.Count; i++)
            {
                GuiTableHeader header = HeaderFor(cells[i], i);
                string fact = new MessageBuilder()
                    .ListItem(Caption(header))
                    .ListItem(Text(header, cells[i]))
                    .Build();
                if (!string.IsNullOrEmpty(fact))
                {
                    lines.Add(fact);
                }
            }

            return lines;
        }

        private void Extras(GuiTableLine line, List<string> into)
        {
            if (RowDetails == null)
            {
                return;
            }

            try
            {
                IList<string> extra = RowDetails(line);
                for (int i = 0; extra != null && i < extra.Count; i++)
                {
                    if (!string.IsNullOrEmpty(extra[i]))
                    {
                        into.Add(extra[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a row's own lines threw: " + e);
            }
        }

        /// <summary>This cell for the review buffer: its own heading and value, then the words hanging
        /// off whatever it draws inside itself - which planets a status circle stands for, what the
        /// number beside a growth arrow counts. The cell's own tooltip is declared separately, so it is
        /// not repeated here. Public so a screen's <see cref="ReadCell"/> gives its own cell the same
        /// buffer every other column has.</summary>
        public IList<string> CellFacts(GuiTableHeader header, AgeTransform cell)
        {
            List<string> lines = new List<string>();
            try
            {
                string drawn = Value(header, cell);
                string fact = new MessageBuilder()
                    .ListItem(Caption(header))
                    .ListItem(drawn ?? ModStrings.Get(ModStrings.NavCellEmpty))
                    .Build();
                if (!string.IsNullOrEmpty(fact))
                {
                    lines.Add(fact);
                }

                List<string> inside = new List<string>();
                List<AgeTransform> children = cell == null ? null : cell.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    CollectTooltips(children[i], inside, 1);
                }

                for (int i = 0; i < inside.Count; i++)
                {
                    // A column drawn as a picture reads its picture's words as its value; the same
                    // sentence again as a detail is the buffer saying one thing twice.
                    if (inside[i] != drawn)
                    {
                        lines.Add(inside[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("table: reviewing a column threw: " + e);
            }

            return lines;
        }

        // ---- reading a cell ----

        /// <summary>What a cell is showing, with the word for showing nothing - the systems table's
        /// Hero and Resources columns are drawn empty for most systems and still have to read.</summary>
        public string CellText(AgeTransform cell)
        {
            return DrawnText(cell) ?? ModStrings.Get(ModStrings.NavCellEmpty);
        }

        /// <summary>The one answer to "what does this cell say": the screen's own reading where it has
        /// one (<see cref="ReadValue"/>), else what the cell draws. Every surface asks it, so the cell,
        /// its buffer line and the row's summary cannot disagree.</summary>
        private string Value(GuiTableHeader header, AgeTransform cell)
        {
            if (ReadValue != null)
            {
                try
                {
                    string said = ReadValue(header, cell);
                    if (said != null)
                    {
                        return said;
                    }
                }
                catch (Exception e)
                {
                    Log.Warn("table: reading a column's own value threw: " + e);
                }
            }

            return DrawnText(cell);
        }

        private string Text(GuiTableHeader header, AgeTransform cell)
        {
            return Value(header, cell) ?? ModStrings.Get(ModStrings.NavCellEmpty);
        }

        /// <summary>Whether the screen answered for this cell, which is also what says its own tooltip is
        /// not a second thing to read.</summary>
        private bool Supplied(GuiTableHeader header, AgeTransform cell)
        {
            if (ReadValue == null)
            {
                return false;
            }

            try
            {
                return ReadValue(header, cell) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Everything the player can see in a cell, or null when it is showing nothing: its
        /// words, and - for a column drawn as a picture, like the automation icon - the first thing its
        /// pictures say for themselves.</summary>
        public string DrawnText(AgeTransform cell)
        {
            MessageBuilder labels = new MessageBuilder();
            List<string> tooltips = new List<string>();
            try
            {
                CollectDrawn(cell, labels, tooltips, 0);
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a column threw: " + e);
            }

            string drawn = labels.Build();
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            return tooltips.Count > 0 ? tooltips[0] : SortKeyText(cell);
        }

        /// <summary>
        /// What a cell the game draws as a PORTRAIT is showing, out of the value the game sorts the
        /// column by.
        ///
        /// The assigned-hero column - the systems table's, the fleets table's, the fleet-selection
        /// window's - is a picture and nothing else: no label, and a "Hero" tooltip the tooltip window
        /// assembles, so there is nothing on the widget to read and the cell said the empty word whether a
        /// hero was assigned or not. Which hero, or none, is the only thing the column exists to say.
        ///
        /// The game has already worked the answer out: it writes the assigned hero's own localized name
        /// into the cell's <c>Comparable</c> so the header can sort on it, and the empty string when the
        /// slot is free (<c>GuiTableCellAssignedHero.Refresh</c> :20-63). So that is what is read - the
        /// game's words, kept in step with the portrait by the same Refresh that paints it. An empty
        /// answer stays null, and the shared empty word covers it.
        /// </summary>
        private static string SortKeyText(AgeTransform cell)
        {
            try
            {
                GuiTableCellAssignedHero portrait =
                    cell == null ? null : cell.GetComponent<GuiTableCellAssignedHero>();
                string name =
                    portrait == null ? null : AgeText.Clean(portrait.Comparable as string);
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void CollectDrawn(
            AgeTransform widget,
            MessageBuilder labels,
            List<string> tooltips,
            int depth
        )
        {
            if (widget == null || depth > MaxCellDepth || !widget.Visible)
            {
                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (label != null)
            {
                labels.ListItem(AgeText.Label(label));
            }

            if (depth > 0)
            {
                AddTooltip(widget.AgeTooltip, tooltips);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                CollectDrawn(children[i], labels, tooltips, depth + 1);
            }
        }

        /// <summary>The words hanging off the things drawn INSIDE a cell, for the buffer. The cell's
        /// own tooltip is not among them: it is declared as the control's tooltip and reaches both
        /// surfaces from there.</summary>
        private void CollectTooltips(AgeTransform widget, List<string> into, int depth)
        {
            if (widget == null || depth > MaxCellDepth || !widget.Visible)
            {
                return;
            }

            AddTooltip(widget.AgeTooltip, into);
            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                CollectTooltips(children[i], into, depth + 1);
            }
        }

        /// <summary>A tooltip's words, but only where the words are actually in it: the ones these
        /// tables hang on their number columns name a simulation property and are assembled by the
        /// tooltip window at draw time, so there is nothing in them to read off the widget.</summary>
        private static void AddTooltip(AgeTooltip tooltip, List<string> into)
        {
            if (AgeWidgets.Readable(tooltip) == null)
            {
                return;
            }

            IList<string> lines = AgeText.Lines(AgeText.Tooltip(tooltip));
            for (int i = 0; i < lines.Count; i++)
            {
                if (!into.Contains(lines[i]))
                {
                    into.Add(lines[i]);
                }
            }
        }

        // ---- reading the table ----

        /// <summary>The rows the table is showing. A table POOLS its lines rather than destroying them,
        /// and the surplus is left transparent rather than hidden - so a row is only real when the game
        /// has something bound to it.</summary>
        public List<GuiTableLine> Lines(GuiTable table)
        {
            List<GuiTableLine> lines = new List<GuiTableLine>();
            try
            {
                AgeTransform host = table.LinesTable;
                List<AgeTransform> children = host == null ? null : host.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    GuiTableLine line =
                        child == null ? null : child.GetComponent<GuiTableLine>();
                    if (
                        line != null
                        && child.Visible
                        && (host.StrictVisibility || child.Alpha > 0f)
                        && _rowRef(line) != null
                    )
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("table: reading the row list threw: " + e);
            }

            return lines;
        }

        /// <summary>
        /// Whether a click on a row would pick it out - the <c>canSelect</c> the table was bound with,
        /// which the game records as <c>LinesTable</c>'s own Enable flag (<c>GuiTable.Bind</c> :130, the
        /// only writer of it).
        ///
        /// Asked of the flag rather than of the chain above it on purpose: a window that switches the
        /// whole table off while an order is in flight is a REFUSAL, and a table bound read-only is not.
        /// </summary>
        public static bool Selectable(GuiTable table)
        {
            try
            {
                return table != null && AgeWidgets.Enabled(table.LinesTable);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether the game would act on this row: the line's own flag - which is where a
        /// refused row is marked - and everything ABOVE <c>LinesTable</c>, whose own flag is the
        /// <c>canSelect</c> the table was bound with and says something else entirely.</summary>
        private static Func<bool> Operable(GuiTable table, GuiTableLine line)
        {
            GuiTable owner = table;
            GuiTableLine row = line;
            return () => AgeWidgets.Enabled(Widget(row)) && AgeWidgets.Operable(Above(owner));
        }

        private static AgeTransform Widget(GuiTableLine line)
        {
            try
            {
                return line == null ? null : line.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Above(GuiTable table)
        {
            try
            {
                AgeTransform lines = table == null ? null : table.LinesTable;
                return lines == null ? null : lines.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ReadCells(GuiTableLine line)
        {
            _cells.Clear();
            Collect(line, _cells);
        }

        /// <summary>A row's columns as their own list, for the two readouts that walk the whole row at
        /// READ time - the summary a player hears and the lines they can review. The build's own copy is
        /// reused across builds and would be a different row's by then.</summary>
        private static List<AgeTransform> CellsOf(GuiTableLine line)
        {
            List<AgeTransform> cells = new List<AgeTransform>();
            Collect(line, cells);
            return cells;
        }

        private static void Collect(GuiTableLine line, List<AgeTransform> into)
        {
            try
            {
                List<AgeTransform> children =
                    line == null || line.CellsTable == null ? null : line.CellsTable.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    if (children[i] != null)
                    {
                        into.Add(children[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a row's columns threw: " + e);
            }
        }

        /// <summary>The heading over a cell: paired by the column's own name, so the pairing survives a
        /// re-sort, and by position for a cell that does not say which column it is.</summary>
        public GuiTableHeader HeaderFor(AgeTransform cell, int index)
        {
            string column = ColumnName(cell);
            if (column != null)
            {
                for (int i = 0; i < _headers.Count; i++)
                {
                    if (PropertyOf(_headers[i]) == column)
                    {
                        return _headers[i];
                    }
                }
            }

            return index < _headers.Count ? _headers[index] : null;
        }

        /// <summary>
        /// What a column is called: the caption the game wrote into the heading from
        /// <c>%&lt;TableName&gt;&lt;ColumnName&gt;Title</c>.
        ///
        /// Read through <see cref="AgeText.Label"/> rather than off the raw string because several of
        /// these captions ARE bare icon tokens - four of the fleet table's eight columns are drawn as a
        /// picture and nothing else - and that path is where a token becomes the icon's name
        /// (<see cref="IconNames"/>). A token the icon table does not name leaves the caption empty and
        /// lands in <c>IconNames.UnknownTokens</c>, which is the audit list; nothing is guessed from the
        /// asset name here.
        /// </summary>
        /// <summary>What this table calls a column: the screen's own answer where it has one
        /// (<see cref="NameColumn"/>), else the caption the game wrote. Public because a screen's
        /// <see cref="ReadCell"/> names its own cell's column with the same words the sheet's edges use.
        /// </summary>
        public string Caption(GuiTableHeader header)
        {
            if (NameColumn == null)
            {
                return HeaderName(header);
            }

            try
            {
                return NameColumn(header);
            }
            catch (Exception)
            {
                return HeaderName(header);
            }
        }

        public static string HeaderName(GuiTableHeader header)
        {
            try
            {
                return header == null ? null : AgeText.Label(header.Label);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string PropertyOf(GuiTableHeader header)
        {
            try
            {
                return header == null ? null : header.PropertyName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ColumnName(AgeTransform cell)
        {
            try
            {
                GuiTableCell component =
                    cell == null ? null : cell.GetComponent<GuiTableCell>();
                return component != null && component.ColumnInfo != null
                    ? component.ColumnInfo.Name
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A cell's tooltip: the one the cell component names, falling back to whatever the
        /// transform carries.</summary>
        public static AgeTooltip TooltipOf(AgeTransform cell)
        {
            try
            {
                GuiTableCell component =
                    cell == null ? null : cell.GetComponent<GuiTableCell>();
                if (component != null && component.Tooltip != null)
                {
                    return component.Tooltip;
                }
            }
            catch (Exception) { }

            return AgeWidgets.Raw(cell);
        }

        /// <summary>What the row is called: the name the game has written in its first column, and the
        /// screen's own answer when the cell is not the one being asked or is drawing nothing.</summary>
        private string RowText(GuiTableLine line, AgeTransform cell)
        {
            string drawn = cell == null ? null : DrawnText(cell);
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            try
            {
                return RowName == null ? null : RowName(line);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Selected(GuiTable table, GuiTableLine line)
        {
            try
            {
                return ReferenceEquals(table.SelectedLine, line);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

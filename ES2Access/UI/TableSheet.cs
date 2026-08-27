using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI.Input;

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
    /// - <b>The headers band</b> is a row of buttons sitting in the table's own Tab stop, one row above
    ///   the first line - reached with Up from row 1, the way it is reached with the eye, and reached in
    ///   the COLUMN the player was standing in rather than at the first one; Enter is the header's own sort click, and the
    ///   column the table is currently sorted by reads "selected", watched live - which is also the only
    ///   feedback a sort press has, since the game answers it by reordering rows and nothing else. A
    ///   column the table forbids sorting on is drawn disabled (<c>DisableSorting</c>) and reads so.
    /// - <b>The rows</b> are a <see cref="GraphSheet"/>: up and down walk the rows down the NAME column,
    ///   left and right walk one row's figures, and a column's own caption is the spoken edge the player
    ///   crosses to reach it rather than a word every cell repeats - the NAME column's caption included,
    ///   so the step back onto the row's name names that column too. The name cell says the name, whether
    ///   the row is the one selected, and carries the whole row in its review buffer - a row of thirteen
    ///   figures in front of "not selected" is thirteen figures the player sits through to hear the one
    ///   word they are stepping the list for.
    /// - <b>Selecting</b> is the game's own select-then-act and is copied rather than shortened: Enter on
    ///   a row is the row's own click, which selects it and enables whatever the screen's Confirm is.
    ///   The game also commits on a DOUBLE click and that gesture is deliberately not wired here - a
    ///   single Enter that both picked and committed would make every pass over the list a decision. A
    ///   table bound <c>canSelect: false</c> (<c>GuiTable.Bind</c> :130, which is the flag
    ///   <c>LinesTable.Enable</c> records) has rows that are not choices at all: they read as plain
    ///   lines, not radios, and Enter does nothing, because that is what a click does. A window that
    ///   binds the flag ON and then wires nothing to the pick says so itself
    ///   (<see cref="RowsAreLines"/>) - the flag cannot see that.
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
    /// Eight hooks are left to the screen, because only the screen knows the answers:
    /// <see cref="ReadCell"/> replaces a whole cell where the game drew a CONTROL in it rather than a
    /// figure (the systems table's automation drop list); <see cref="SplitCell"/> reads ONE of the
    /// game's cells as several columns, each with its own Enter (the journal's Details buttons);
    /// <see cref="CellTooltipReading"/> overrides how loudly a column's tooltip reads where the shared
    /// <see cref="GraphNodes.ModeFor"/> rule gets it wrong (the save list's Mods dossier - key it on
    /// <see cref="PropertyOf"/>, the game's own column name, never the translated caption);
    /// <see cref="ReadValue"/> replaces only what a
    /// cell SAYS, for a column that draws no words at all (the politics table's support icons, whose value
    /// is on their own tooltip); <see cref="ActivateCell"/> replaces
    /// just what Enter does on a cell, which is how an interactive-cells table
    /// (<c>GuiTable.Bind(interactiveCells: true)</c>) offers the cell's own button - and note that such a
    /// button is the FIRST half of a two-step gesture, so what a screen hands back is
    /// <see cref="AgeWidgets.PressPropagating"/>, never a bare press; <see cref="Decorate"/> adds a
    /// gesture the game gives a row on some OTHER control than the table's own (the marketplace's
    /// quantity steppers) - the table's double click is not one of those, it belongs to every table and
    /// is wired here (<see cref="ShowOnMap"/>); <see cref="NameColumn"/> names a column whose
    /// heading draws no caption; and <see cref="RowDetails"/> adds what a row draws inside its name cell
    /// beyond the name. All eight OPTIONAL, and nothing here presses anything on a screen's
    /// behalf.
    ///
    /// One instance per table per screen, kept across builds: its two scratch lists are reused, and
    /// <c>Build</c> runs every frame. <c>SystemSelectionScreen</c> was the first adopter.
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
        /// every other column, which is the shared value cell. The cell it answers with still gets the
        /// row's own refusal said over the top of it, unless the screen says its cell already says it
        /// (<see cref="SaysRowRefusal"/>).</summary>
        public delegate NodeVtable CellReader(
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header,
            Func<bool> enabled
        );

        /// <summary>
        /// A cell the screen reads as SEVERAL columns, for a column the game drew more than one control
        /// in - the journal's Details cell, which holds the button that opens a finished game's score
        /// screen and the button that deletes the entry. Each answer becomes a column of its own, walked
        /// with left and right like any other, so every control keeps a plain Enter of its own.
        ///
        /// Null - and an empty or one-item answer - leaves the cell to <see cref="ReadCell"/> and the
        /// shared value reading. Every row must answer with the same COUNT for a column: the sheet takes
        /// its column captions from the first row that has cells, and each part is captioned with the
        /// heading of the cell it came out of, so a row that splits a column differently from its
        /// neighbours would walk crooked.
        /// </summary>
        public delegate IList<NodeVtable> CellControls(
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

        /// <summary>How a column's own tooltip should reach the player, where the shared rule reads it
        /// wrong. Null - for the column or for the table - leaves <see cref="GraphNodes.ModeFor"/> to
        /// answer.</summary>
        public delegate TooltipMode? CellTooltip(GuiTableHeader header, AgeTransform cell);

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

        /// <summary>How far <see cref="DeepText"/> looks - deep enough for a cell holding a whole panel
        /// of its own, which is as far as any of these tables nest.</summary>
        private const int DeepCellDepth = 6;

        /// <summary>See <see cref="RowLabel"/>. Unset is legal for a table whose name column always
        /// draws the name.</summary>
        public RowLabel RowName;

        /// <summary>
        /// The rows are lines to READ rather than choices to make - the one thing the table's own
        /// <c>canSelect</c> flag cannot see.
        ///
        /// The flag says whether a click would pick a row out, and for every table but one that is the
        /// same question as whether picking MEANS anything. The journal is the exception: it is bound
        /// selectable, so the game highlights a clicked line, while the window wired nothing to the pick
        /// at all - <c>GuiTableLine</c> sends "OnLineSelection" to a client that does not implement it
        /// (<c>GuiTableLine.cs:204-214</c>) and the page has no Confirm for a pick to enable - so the
        /// row's own actions are the buttons inside its cells and the highlight leads nowhere. Rows that
        /// say "not selected" there offer a choice the window does not have.
        ///
        /// Unset - the ordinary case - leaves the table's flag to answer.
        /// </summary>
        public bool RowsAreLines;

        /// <summary>See <see cref="CellReader"/>.</summary>
        public CellReader ReadCell;

        /// <summary>See <see cref="CellControls"/>. Asked first: a cell it answers for is not read by
        /// <see cref="ReadCell"/> as well.</summary>
        public CellControls SplitCell;

        /// <summary>See <see cref="CellActivation"/>.</summary>
        public CellActivation ActivateCell;

        /// <summary>A gesture the game gives a row on some control of its own beyond the table's - the
        /// marketplace's quantity steppers. Handed the row's finished vtable. The table's own double
        /// click is NOT this: it is every table's, and <see cref="ShowOnMap"/> wires it.</summary>
        public Action<GuiTableLine, NodeVtable> Decorate;

        /// <summary>The <see cref="ModStrings"/> key of the USAGE HINT a row ends its buffer with,
        /// saying what this table's second click does ("{0} to show and select fleet"). Unset - the
        /// ordinary case - is a table whose client answers the gesture with nothing, or one where the
        /// game's own tooltip already says it, and then nothing is said.</summary>
        public string DoubleClickHint;

        /// <summary>
        /// See <see cref="ColumnCaption"/>. Unset is the ordinary case.
        ///
        /// It exists because a column's caption is not always IN the heading: the politics table draws a
        /// population's portrait and nothing else, and leaves the raw <c>%â€¦Title</c> key in the label for
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

        /// <summary>
        /// See <see cref="CellTooltip"/>. Unset is the ordinary case.
        ///
        /// It exists because <see cref="GraphNodes.ModeFor"/>'s rule reads a tooltip's SHAPE, and its
        /// premise for announcing a Content-backed one is that Content is "the single sentence the game
        /// wrote to explain the control". A column can break that premise: the save list's Mods column
        /// writes a whole multi-sentence dossier into Content - every module the save wants, with its
        /// version and whether it is installed - which is a review read rather than something to have
        /// spoken whole every time focus passes over the cell. The rule cannot see that, because the
        /// dossier is only composed when the row binds, so the column says so here.
        /// </summary>
        public CellTooltip CellTooltipReading;

        private readonly string _key;
        private readonly RowObject _rowRef;

        /// <summary>The cell the screen last stamped with <see cref="SaysRowRefusal"/>, read and cleared
        /// by the one <see cref="ReadCell"/> call that could have stamped it.</summary>
        private NodeVtable _saysRowRefusal;

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
                builder.AddItem(
                    ControlId.Referenced(header, _key + "header/" + property + "/" + i),
                    vtable
                );
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
            AgeControlToggle funnel = header.FilterToggle;
            if (funnel == null || !AgeWidgets.Visible(funnel.AgeTransform))
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
            builder.AddItem(
                ControlId.Referenced(funnel, _key + "filter/" + PropertyOf(header) + "/" + index),
                vtable
            );
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
                int column = 0;
                for (int i = 1; i < _cells.Count; i++)
                {
                    AgeTransform cell = _cells[i];
                    GuiTableHeader header = HeaderFor(cell, i);
                    IList<NodeVtable> parts = Split(line, cell, header, Operable(table, line));
                    if (parts == null)
                    {
                        cells.Add(
                            new KeyValuePair<int, NodeVtable>(
                                ++column,
                                CellVtable(table, line, cell, header)
                            )
                        );
                        continue;
                    }

                    for (int p = 0; p < parts.Count; p++)
                    {
                        // The part aimed the pointer at the control it declared, so the cell-wide aim is
                        // not applied over the top of it, and it answered for that control's own
                        // availability, so the row's is not said as well.
                        Adorn(table, line, parts[p], false);
                        cells.Add(new KeyValuePair<int, NodeVtable>(++column, parts[p]));
                    }
                }

                // The line widget is what the row is DRAWN as, and a table whose rows are keyed by the
                // game's model (a save, a trait) has nothing else with a rectangle: without it the
                // viewport had nothing to follow and End left the focused row clipped off the bottom.
                sheet.RowAt(
                    PrimaryVtable(table, line, _cells[0]),
                    _rowRef(line),
                    cells,
                    Widget(line)
                );
            }

            sheet.Finish();

            // Tab into the table lands on a ROW - the selected one where there is one - and not on the
            // heading band declared above it, whose sorted column reads "selected" too.
            builder.LandStopOn(sheet.FirstRow);
            return sheet;
        }

        /// <summary>The captions the sheet speaks when the player crosses into a column, read off a
        /// real row: which heading is over which column is the pairing <see cref="HeaderFor"/> makes,
        /// and it survives a re-sort. Every row of one table has the same columns, so the first one
        /// that has any answers for all of them. A cell read as several columns
        /// (<see cref="SplitCell"/>) gives each of them the heading it came out of - they ARE that one
        /// column of the game's table, and there is no other caption for them to have.
        ///
        /// The NAME column leads the list, because the sheet labels the crossing back onto a row's name
        /// with it exactly as it labels every other crossing; it is never split, being the cell the row
        /// itself is made of.</summary>
        private string[] Columns(List<GuiTableLine> lines)
        {
            for (int l = 0; l < lines.Count; l++)
            {
                List<AgeTransform> cells = CellsOf(lines[l]);
                if (cells.Count < 2)
                {
                    continue;
                }

                List<string> columns = new List<string>(cells.Count);
                columns.Add(Caption(HeaderFor(cells[0], 0)));
                for (int i = 1; i < cells.Count; i++)
                {
                    GuiTableHeader header = HeaderFor(cells[i], i);
                    string caption = Caption(header);
                    IList<NodeVtable> parts = Split(lines[l], cells[i], header, AlwaysOn);
                    for (int p = 0; p < (parts == null ? 1 : parts.Count); p++)
                    {
                        columns.Add(caption);
                    }
                }

                return columns.ToArray();
            }

            return null;
        }

        private static readonly Func<bool> AlwaysOn = delegate
        {
            return true;
        };

        /// <summary>The several controls the screen reads a cell as, or null where it reads the cell as
        /// one thing - which is every cell of every other table.</summary>
        private IList<NodeVtable> Split(
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header,
            Func<bool> enabled
        )
        {
            if (SplitCell == null)
            {
                return null;
            }

            try
            {
                IList<NodeVtable> parts = SplitCell(line, cell, header, enabled);
                return parts == null || parts.Count == 0 ? null : parts;
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a cell's own controls threw: " + e);
                return null;
            }
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
            if (Choosable(table))
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
                // A table row is not read as a radio button, though its selection IS one: the row's
                // name and its spoken "selected"/"not selected" carry the whole affordance, and a role
                // word on every row of a table the player was just told is a table is noise (owner
                // ruling 2026-08-14). Text is the role-less type with the table reading order.
                vtable.ControlType = ControlTypes.Text;
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
            ShowOnMap(row, vtable);

            // The USAGE HINT for that second click - on the ROW and not on every cell of it, though
            // the gesture works from all of them: what the click DOES here is a fact about the row,
            // and repeating it down eight columns is eight times the sentence for one affordance.
            // What it does is the table client's business and differs table by table, so the wording
            // is the screen's to name (<see cref="DoubleClickHint"/>); the tables whose client
            // answers the gesture with nothing name nothing and stay silent.
            if (row.DoubleClickButton != null)
            {
                NodeHints.Add(vtable, DoubleClickHint, UiActions.DoubleClick);
            }

            if (Decorate != null)
            {
                Decorate(row, vtable);
            }

            return vtable;
        }

        /// <summary>
        /// The table's own DOUBLE click, which every table in this game wires the same way and only some
        /// of them answer.
        ///
        /// A line carries the button the second click lands on (<c>GuiTableLine.DoubleClickButton</c>,
        /// bound to <c>OnLineDoubleClickCb</c> at <c>GuiTableLine.cs</c> :96-99), and the game forwards
        /// it to whatever the table's client does with it: the empire's systems table opens that
        /// system's management page, the military screen shows that fleet on the map, the two selection
        /// modals pick the row and close. So the alternate-activation chord belongs to every table at
        /// once rather than to whichever screen happened to notice - a per-screen wiring is how six of
        /// the eight lost the gesture.
        ///
        /// Every one of those handlers reads the table's SELECTED line rather than the line it was
        /// handed, so the row is picked first - which is exactly what the mouse's first click did before
        /// its second one arrived. A row already picked is not picked again: the game's own selection
        /// handler slides panels about and plays a sound, and neither belongs to a request to be shown
        /// something. A table whose client does nothing with the gesture stays silent, as the mouse's
        /// double click does there. The save list's second click is the one that ACTS rather than
        /// shows - it loads or overwrites, behind the game's own confirmation box - and it is carried
        /// like the rest: the chord is the mouse's gesture, and the confirmation is the game's own
        /// guard on both of them (owner ruling 2026-08-14, reversing the earlier opt-out).
        /// </summary>
        private void ShowOnMap(GuiTableLine line, NodeVtable vtable)
        {
            GuiTableLine row = line;
            if (row.DoubleClickButton == null || vtable.OnDoubleClick != null)
            {
                return;
            }


            vtable.OnDoubleClick = () =>
            {
                try
                {
                    if (!AgeWidgets.Enabled(row.AgeTransform))
                    {
                        return;
                    }

                    if (row.SelectionToggle != null && !row.SelectionToggle.State)
                    {
                        AgeWidgets.Toggle(row.SelectionToggle);
                    }

                    AgeWidgets.DoubleClick(row.DoubleClickButton);
                }
                catch (Exception e)
                {
                    Log.Warn("table: replaying a row's double click threw: " + e);
                }
            };
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
            _saysRowRefusal = null;
            NodeVtable vtable = ReadCell == null ? null : ReadCell(row, it, heading, enabled);
            bool saysRefusal = vtable != null && ReferenceEquals(vtable, _saysRowRefusal);
            _saysRowRefusal = null;
            AgeTooltip aim = null;
            if (vtable == null)
            {
                AgeTooltip cellTip = Supplied(heading, it) ? null : TooltipOf(it);
                List<AgeTooltip> inner = Inside(it, cellTip);
                // Where the cell carries nothing of its own, the pointer goes to the last dossier
                // drawn inside it - otherwise its section is a promise the player can never collect,
                // since a renderer-assembled tooltip has no words until the game draws it.
                aim = cellTip != null || inner == null ? null : inner[inner.Count - 1];
                vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.ValuePart(() => Text(heading, it)),
                    },
                    Sections = WithInner(
                        GraphNodes.Sections(
                            () => CellFacts(heading, it),
                            cellTip,
                            Reading(heading, it)
                        ),
                        inner
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
                else if (Choosable(table))
                {
                    vtable.OnActivate = () =>
                    {
                        if (enabled())
                        {
                            AgeWidgets.Toggle(row.SelectionToggle);
                        }
                    };
                }

                if (Choosable(table))
                {
                    vtable.StateText = () =>
                        selected() ? ModStrings.Get(ModStrings.NavSelected) : null;
                }
            }

            // Declared for the screen's own cells too: a cell reads its caption from the edge the
            // player crossed, so its buffer is the one surface that has to carry the pair itself.
            vtable.BufferHead = () => CellHead(heading, it);
            Adorn(table, line, vtable, !saysRefusal);
            AgeWidgets.PointAt(vtable, it);
            if (aim != null)
            {
                AgeWidgets.PointAt(vtable, it, aim);
            }

            return vtable;
        }

        /// <summary>The cell's declared sections with the dossiers hanging INSIDE it added after them,
        /// each read by the mode the shared rule answers for it - which for a renderer-assembled one is
        /// always "indicate", so nothing new is spoken and the buffer gains what the game draws.
        /// </summary>
        private static IList<NodeSection> WithInner(
            IList<NodeSection> sections,
            List<AgeTooltip> inner
        )
        {
            if (inner == null || inner.Count == 0)
            {
                return sections;
            }

            List<NodeSection> all = new List<NodeSection>(
                (sections == null ? 0 : sections.Count) + inner.Count
            );
            for (int i = 0; sections != null && i < sections.Count; i++)
            {
                all.Add(sections[i]);
            }

            for (int i = 0; i < inner.Count; i++)
            {
                NodeSection section = GraphNodes.TooltipSection(inner[i]);
                if (section != null)
                {
                    all.Add(section);
                }
            }

            return all.Count == 0 ? null : all;
        }

        /// <summary>What this column says about how loudly its tooltip should read - see
        /// <see cref="CellTooltipReading"/>.</summary>
        private TooltipMode? Reading(GuiTableHeader header, AgeTransform cell)
        {
            if (CellTooltipReading == null)
            {
                return null;
            }

            try
            {
                return CellTooltipReading(header, cell);
            }
            catch (Exception e)
            {
                Log.Warn("table: asking how a column's tooltip should read threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The screen's answer for one cell of its own whose availability ALREADY includes the row's -
        /// wrap the vtable in it and return the answer (<c>return sheet.SaysRowRefusal(vtable);</c>).
        ///
        /// A cell the screen reads itself says its own control's availability, and the ROW's refusal is
        /// said over the top of it by the sheet, because the two are different questions: the Empire
        /// screen's construction column is switched off on an outpost whose row is perfectly available.
        /// Where the screen composed the cell's answer FROM the row's - the Empire screen's action
        /// buttons and the Military screen's hero button both refuse whenever their row does - the cell
        /// already says the word, and the shared one would make a refused row say "unavailable" twice.
        ///
        /// Per CELL rather than per screen, and matched by IDENTITY, because one <see cref="ReadCell"/>
        /// answers different shapes for different columns: the Empire screen's policy column is a combo
        /// box whose own availability is the DROP LIST's and not the row's, so it is not stamped and
        /// keeps the shared word. Anything else - a vtable stamped and not returned, a stamp left over
        /// from an earlier cell - keeps it too, which is the safe half of the choice.
        /// </summary>
        public NodeVtable SaysRowRefusal(NodeVtable vtable)
        {
            _saysRowRefusal = vtable;
            return vtable;
        }

        /// <summary>
        /// What every cell of a row says beyond its own words, however the cell was read: whether the row
        /// it belongs to is the one taken, whether the row is refused, and the row's name as what a typed
        /// letter searches - so one row is one search result whichever column the player is standing in.
        ///
        /// The pointer is deliberately not here: a cell the screen read itself has already aimed it at the
        /// control it declared. <paramref name="availability"/> is off for a cell read as SEVERAL controls
        /// (<see cref="SplitCell"/>), each of which says whether its own button is refused - a closer
        /// answer than the row's, and a second one would say "unavailable" twice - and off for a cell the
        /// screen answered for the row itself (<see cref="SaysRowRefusal"/>), for the same reason.
        /// </summary>
        private void Adorn(
            GuiTable table,
            GuiTableLine line,
            NodeVtable vtable,
            bool availability
        )
        {
            GuiTable owner = table;
            GuiTableLine row = line;
            if (Choosable(table))
            {
                vtable.Announcements.Add(GraphNodes.SelectedPart(() => Selected(owner, row)));
            }

            if (availability)
            {
                vtable.Announcements.Add(GraphNodes.DisabledPart(Operable(table, line)));
            }

            // The row's second click, from any of its columns - for the same reason Enter here is the
            // row's click: a player who arrowed across to compare a figure should not have to arrow
            // back to act on the row they just compared.
            ShowOnMap(row, vtable);
            vtable.SearchText = () => RowText(row, null);
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

        /// <summary>
        /// What a cell's buffer OPENS with: the column's caption and the cell's value, which is what
        /// the player heard on arriving - the caption as the crossed edge, then the value.
        ///
        /// It is the buffer's declared head (<see cref="NodeVtable.BufferHead"/>) AND the first of the
        /// cell's own facts, deliberately the same string from the same place: the head dedupe then
        /// drops the second, so the pair is said once however the cell was read. Splitting the two
        /// apart is what made a cell open "Valid" and then "Mods, Valid".
        /// </summary>
        private string CellHead(GuiTableHeader header, AgeTransform cell)
        {
            return new MessageBuilder()
                .ListItem(Caption(header))
                .ListItem(Value(header, cell) ?? ModStrings.Get(ModStrings.NavCellEmpty))
                .Build();
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
                string fact = CellHead(header, cell);
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
                CollectDrawn(cell, labels, tooltips, 0, MaxCellDepth, false);
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

            if (tooltips.Count > 0)
            {
                return tooltips[0];
            }

            return SortKeyText(cell) ?? DeepText(cell);
        }

        /// <summary>
        /// What a cell whose figure sits DEEPER than <see cref="MaxCellDepth"/> is showing - asked only
        /// of a cell the shallow reading found nothing in at all, so a cell that already reads keeps the
        /// reading it had.
        ///
        /// The systems table's Resources column is such a cell: the game draws a whole
        /// <c>ResourcesPanel</c> inside it and the panel keeps its own pooled item table, so the figure
        /// the player sees is four levels down (cell / ResourcesBanner / ResourceItemsTable /
        /// ResourceIncomeItemList / Net) and the column said the empty word beside a drawn "2".
        ///
        /// The shallow cap cannot simply be raised: the third level of the automation column is that
        /// drop list's CLOSED popup, whose entries would then be read as though the cell were showing
        /// all of them at once. The popup is parked at ALPHA ZERO with <c>Visible</c> still true, so
        /// this pass is painted-only, which leaves it out however deep it looks - and leaves out a
        /// pooled item the panel retired the same way.
        /// </summary>
        private string DeepText(AgeTransform cell)
        {
            MessageBuilder labels = new MessageBuilder();
            try
            {
                CollectDrawn(cell, labels, null, 0, DeepCellDepth, true);
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a column deeper threw: " + e);
            }

            string drawn = labels.Build();
            return string.IsNullOrEmpty(drawn) ? null : drawn;
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
            int depth,
            int limit,
            bool paintedOnly
        )
        {
            if (widget == null || depth > limit || !widget.Visible)
            {
                return;
            }

            if (paintedOnly && widget.Alpha <= 0f)
            {
                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (label != null)
            {
                labels.ListItem(AgeText.Label(label));
            }

            if (depth > 0 && tooltips != null)
            {
                AddTooltip(widget.AgeTooltip, tooltips);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                CollectDrawn(children[i], labels, tooltips, depth + 1, limit, paintedOnly);
            }
        }

        /// <summary>The words hanging off the things drawn INSIDE a cell, for the buffer. The cell's
        /// own tooltip is not among them: it is declared as the control's tooltip and reaches both
        /// surfaces from there. Only the tooltips whose words are ON the widget: the class-backed ones
        /// have no words until they are drawn, and reach the buffer as SECTIONS instead
        /// (<see cref="Inside"/>).</summary>
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

        /// <summary>
        /// The RENDERER-ASSEMBLED tooltips hanging on the things drawn inside a cell.
        ///
        /// A cell's own tooltip has always been declared, and the words hanging on its pieces have
        /// always been read (<see cref="CollectTooltips"/>) - but only where those words are on the
        /// widget. A class-backed one inside a cell has no words until it is drawn, so reading it as
        /// text answered "nothing" and the dossier the game hangs on a status circle or a growth arrow
        /// was dropped without trace. Declared as its own section instead, which is the surface that
        /// can wait for the drawing.
        ///
        /// The cell's own is excluded here (the caller declares it), and so is anything equal to it -
        /// a table that names its tooltip through <c>GuiTableCell.Tooltip</c> may be naming one that
        /// hangs on a piece INSIDE the cell.
        /// </summary>
        private List<AgeTooltip> Inside(AgeTransform cell, AgeTooltip own)
        {
            List<AgeTooltip> found = new List<AgeTooltip>();
            AgeWidgets.EffectiveTooltips(cell, found, TooltipReach.Descendants, MaxCellDepth);
            List<AgeTooltip> kept = null;
            for (int i = 0; i < found.Count; i++)
            {
                if (
                    AgeWidgets.Readable(found[i]) != null
                    || AgeWidgets.SameTooltip(found[i], own)
                )
                {
                    continue;
                }

                if (kept == null)
                {
                    kept = new List<AgeTooltip>(found.Count - i);
                }

                kept.Add(found[i]);
            }

            return kept;
        }

        // ---- reading the table ----

        /// <summary>The rows the table is showing. A table POOLS its lines rather than destroying them,
        /// and the surplus is left transparent rather than hidden - so a row is only real when the game
        /// has something bound to it AND the game is drawing it. <c>StrictVisibility</c> buys a faded
        /// row no exemption here: that flag only tells the ARRANGER to keep counting the row's slot
        /// (<c>GetVisibleChildrenCount</c>), while the renderer skips an alpha 0 child either way
        /// (<c>firstpass/AgeTransform.cs:1955</c>) - and the exemption once let a strict table's
        /// retired rows be read aloud.</summary>
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
                        && child.Alpha > 0f
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

        /// <summary>Whether picking a row is a CHOICE the player is being offered: the table's flag, and
        /// the screen's own answer where the flag is on while nothing is wired to the pick
        /// (<see cref="RowsAreLines"/>).</summary>
        private bool Choosable(GuiTable table)
        {
            return !RowsAreLines && Selectable(table);
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

        /// <summary>The name the game gives the column in its own table definition
        /// (<c>Public/Gui/GuiElements[Tables].xml</c>) - what the game sorts by, and the only stable
        /// way for a screen to name one of its columns: the drawn caption is translated.</summary>
        public static string PropertyOf(GuiTableHeader header)
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

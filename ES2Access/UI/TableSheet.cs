using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.Screens;

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
    /// Seven hooks are left to the screen, because only the screen knows the answers:
    /// <see cref="ReadCell"/> replaces a whole cell where the game drew a CONTROL in it rather than a
    /// figure (the systems table's automation drop list); <see cref="SplitCell"/> reads ONE of the
    /// game's cells as several columns, each with its own Enter (the journal's Details buttons);
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
    public sealed partial class TableSheet
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

        private readonly string _key;
        private readonly RowObject _rowRef;

        /// <summary>The cell the screen last stamped with <see cref="SaysRowRefusal"/>, read and cleared
        /// by the one <see cref="ReadCell"/> call that could have stamped it.</summary>
        private NodeVtable _saysRowRefusal;

        private readonly List<GuiTableHeader> _headers = new List<GuiTableHeader>();
        private readonly List<AgeTransform> _cells = new List<AgeTransform>();

        /// <summary>
        /// Per-BUILD memos, both cleared at the top of <see cref="Rows"/> and keyed on the cell widget:
        /// the screen's own reading of a cell (<see cref="ReadValue"/>) and the tooltip subtree hanging
        /// inside it (<see cref="Inside"/>). Every surface a cell has asks for both - the value part,
        /// the buffer head, the cell's facts, the whole row's facts - and each asked again from scratch,
        /// so a wide table paid for the same hook call and the same subtree walk several times over
        /// while the answer could not have changed.
        ///
        /// One build is the memo's whole life BECAUSE a table pools its lines: a cell widget outlives
        /// the row bound to it, so anything keyed on the widget across builds would answer for the row
        /// that has gone. Clearing at the top of the build is what makes the widget a safe key.
        ///
        /// They are read after the build too - a value part and a buffer head resolve when the
        /// announcer asks, which is later in the same frame - and that is the point: the memo makes
        /// everything one node says about a cell agree, at the freshness of the tree the node came
        /// from, which is the freshness the node already had.
        /// </summary>
        private readonly Dictionary<AgeTransform, KeyValuePair<GuiTableHeader, string>> _supplied =
            new Dictionary<AgeTransform, KeyValuePair<GuiTableHeader, string>>();

        private readonly Dictionary<AgeTransform, List<AgeTooltip>> _hovers =
            new Dictionary<AgeTransform, List<AgeTooltip>>();

        /// <param name="keyPrefix">Prefixes every id this sheet declares; one per table per screen.
        /// </param>
        /// <param name="rowRef">See <see cref="RowObject"/>. Required, because a table with index keys
        /// silently rebadges the next row as "the same control" the moment one is removed.</param>
        public TableSheet(string keyPrefix, RowObject rowRef)
        {
            _key = keyPrefix;
            _rowRef = rowRef;
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

        // ---- the two cells a table draws a CONTROL in ----

        /// <summary>
        /// A cell the game drew a BUTTON in rather than a figure - the Empire page's status,
        /// population, construction and hangar columns, its assigned-hero column and the Military
        /// page's, which are what open the panels under the table.
        ///
        /// Null for every other column, which is the shared value cell. Enter here is the cell's own
        /// click carried on to the row's toggle, which is the two-step the mouse makes: the cell button
        /// records which cell was hit and the toggle's handler reads it and opens the matching panel.
        ///
        /// The cell is still a cell - it says the figure it is drawing and not its heading, which the
        /// sheet speaks as the edge - and it is read here rather than by the ordinary value path only
        /// because a REFUSAL lives on the cell: the construction column of an outpost or a ghost is
        /// switched off with the game's own sentence about why, while the row it sits in is perfectly
        /// available. The sentence is read off the first tooltip-bearing CHILD
        /// (<see cref="RefusalTooltip"/>) rather than off the cell, because that is where the game
        /// writes it; the Military page's copy of this asked the cell alone and refused silently.
        ///
        /// Its own "unavailable" covers the row's, since the row's own answer is one of the three this
        /// cell asks - so the sheet leaves the shared one off (<see cref="SaysRowRefusal"/>).
        /// </summary>
        public NodeVtable ButtonCell(AgeTransform cell, GuiTableHeader header, Func<bool> enabled)
        {
            AgeControlButton button = CellButton(cell);
            if (button == null)
            {
                return null;
            }

            AgeTransform it = cell;
            AgeControlButton press = button;
            GuiTableHeader heading = header;
            Func<bool> rowEnabled = enabled;
            Func<bool> operable = () =>
                rowEnabled() && AgeWidgets.Operable(press.AgeTransform) && AgeWidgets.Enabled(it);
            AgeTooltip tooltip = TooltipOf(cell);
            AgeTooltip reason = RefusalTooltip(cell) ?? tooltip;
            NodeVtable vtable = new NodeVtable
            {
                // Named as the button it is, unlike the figures beside it: the game draws a click
                // target in these columns and the whole point of the column is what pressing it
                // opens, so the role word is the only thing that says the cell can be pressed at all.
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.ValuePart(() => CellText(it)),
                    GraphNodes.DisabledPart(operable),
                },
                Sections = GraphNodes.Sections(() => CellFacts(heading, it), tooltip),
                OnActivate = () =>
                {
                    if (operable())
                    {
                        AgeWidgets.PressPropagating(press);
                    }
                },
            };
            GraphNodes.AddRefusal(vtable, reason, operable);
            return SaysRowRefusal(vtable);
        }

        /// <summary>The button a cell carries, where it has one the game is drawing. A column can carry
        /// a dummy with no handler at all - the Empire page's resources column does - which is not one
        /// of these: pressing it does what a click on any plain cell does, and that is the sheet's own
        /// job.</summary>
        private static AgeControlButton CellButton(AgeTransform cell)
        {
            try
            {
                if (cell == null)
                {
                    return null;
                }

                AgeControlButton button = cell.GetComponentInChildren<AgeControlButton>(true);
                // Different widget: the search reaches HIDDEN children on purpose (a cell holds the
                // controls of every shape its column can take), so which of them the game is drawing
                // is the answer to "is this cell a button at all" - and it is asked of the button,
                // never of the cell the gate will ask about.
                return button != null
                    && !string.IsNullOrEmpty(button.OnActivateMethod)
                    && button.AgeTransform.Visible
                    ? button
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where a refused cell's reason is written. The construction column puts its sentence
        /// on the label it draws INSTEAD of the construction ("this system is an outpost"), not on the
        /// cell, so the first tooltip with words in it under the cell is the one that answers.</summary>
        private static AgeTooltip RefusalTooltip(AgeTransform cell)
        {
            try
            {
                IList<AgeTransform> children = cell == null ? null : cell.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    // Different widget: which of the cell's alternative children the game is drawing
                    // decides whose sentence the refusal is. The children the column is not using this
                    // row keep the last row's words.
                    if (child == null || !child.Visible)
                    {
                        continue;
                    }

                    AgeTooltip tooltip = AgeWidgets.Raw(child);
                    if (tooltip != null && AgeWidgets.Readable(tooltip) != null)
                    {
                        return tooltip;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>
        /// The automation-policy cell, where the game draws a DROP LIST rather than a readout - the
        /// same cell the Empire page's systems table and the system-selection modal both draw, and the
        /// same treatment: where the game leaves it operable the cell is a combo box and Enter opens
        /// the list, and a policy the game has switched off is a readout of what the system is doing
        /// instead.
        ///
        /// Null for every other column. Like every other cell it does not say its own heading - the
        /// crossed edge does - but the list it opens is still TITLED with it, because that window is
        /// somewhere the player has been taken. Its own availability is the DROP LIST's rather than the
        /// row's, so it is not stamped with <see cref="SaysRowRefusal"/> and keeps the shared word.
        /// </summary>
        public NodeVtable PolicyCell(AgeTransform cell, GuiTableHeader header, Func<bool> enabled)
        {
            AgeControlDropList list = DropList(cell);
            if (list == null || !AgeWidgets.Operable(list.AgeTransform) || !enabled())
            {
                return null;
            }

            AgeControlDropList it = list;
            AgeTransform widget = cell;
            GuiTableHeader heading = header;
            return GraphNodes.ComboBox(
                null,
                () => CellText(widget),
                () => SettingRows.OpenList(it, HeaderName(heading)),
                () => AgeWidgets.Operable(it.AgeTransform),
                TooltipOf(widget),
                () => CellFacts(heading, widget)
            );
        }

        private static AgeControlDropList DropList(AgeTransform cell)
        {
            try
            {
                return cell == null ? null : cell.GetComponentInChildren<AgeControlDropList>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- what a line stands for ----

        /// <summary>
        /// The model object a line stands for, off the wrapper the table binds - the answer every
        /// screen with a table needs and each of the seven wrote for itself.
        ///
        /// A <c>GuiTableLine</c> carries the GAME's wrapper in <c>Data</c>, and a wrapper is rebuilt on
        /// every refresh, so it is the model underneath it that identifies the row across refreshes
        /// (which is what <see cref="RowObject"/> is for). The sheet deliberately hands the line to its
        /// host without unwrapping it - only the host knows which wrapper its table binds - so this is
        /// the unwrapping written once and named per screen at the point of use.
        /// </summary>
        public static RowObject Model<TWrapper>(Func<TWrapper, object> model)
            where TWrapper : class
        {
            return line =>
            {
                try
                {
                    TWrapper wrapper = line == null ? null : line.Data as TWrapper;
                    return wrapper == null ? null : model(wrapper);
                }
                catch (Exception)
                {
                    return null;
                }
            };
        }

        /// <summary>What a row is called when its name column draws nothing - the model's own name off
        /// the same wrapper, cleaned of the game's markup exactly as a drawn label would be.</summary>
        public static RowLabel Name<TWrapper>(Func<TWrapper, string> name)
            where TWrapper : class
        {
            return line =>
            {
                try
                {
                    TWrapper wrapper = line == null ? null : line.Data as TWrapper;
                    return wrapper == null ? null : AgeText.Clean(name(wrapper));
                }
                catch (Exception)
                {
                    return null;
                }
            };
        }
    }
}

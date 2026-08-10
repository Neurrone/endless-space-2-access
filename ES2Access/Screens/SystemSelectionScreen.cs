using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The window the game opens whenever it needs the player to pick one of their own systems - which
    /// colony feeds an outpost, where a ship is spawned, where the Academy sends a hero. It is a
    /// GENERIC window: six different panels open it with their own <c>Purpose</c>, their own filter and
    /// their own reason for refusing a system, and none of that is written down here. What is modelled
    /// is what the window DRAWS - its title, its sort headers, its table of systems and its two
    /// buttons - so every caller of it speaks without a screen of its own.
    ///
    /// Three stops, in drawn order. The sort headers across the top, where Enter is the header's own
    /// sort click and the column the table is currently sorted by reads "selected" - which is also the
    /// feedback for having just pressed one, since the part is watched live. Then the table, emitted as
    /// a <see cref="GraphSheet"/>: up and down walk the systems down the name column, left and right
    /// walk one system's figures, and the column's own caption is the spoken EDGE the player crosses to
    /// reach it ("Status, Colony 1") rather than a word every cell repeats. Then Cancel and Confirm.
    ///
    /// So a row says its name, that it is a choice, and whether it is the one taken - and nothing else.
    /// Thirteen figures in front of "not selected" is thirteen figures the player has to sit through to
    /// hear the one word they are stepping the list for; the figures are a walk sideways away, and all
    /// of them at once are in the review buffer.
    ///
    /// The table is the game's own SELECT-THEN-ACT model and it is copied rather than shortened: Enter
    /// on a row is the row's own click, which selects it and enables Confirm, and Confirm is a button
    /// like any other. The game also commits on a DOUBLE click, and that gesture is deliberately not
    /// wired to anything - a single Enter that both picked and committed would make every pass over
    /// the list a decision.
    ///
    /// A system the opener will not accept is drawn disabled with the game's own sentence for why on
    /// the line's tooltip (<c>SystemSelectionModalWindow.GuiColonizedStarSystemObject.OnBind</c>), so
    /// the row is declared REFUSING and says that sentence: the same treatment every blocked control
    /// in the mod gets.
    ///
    /// Escape is the game's. The window is a plain <c>GuiModalWindow</c> with no <c>HandleInput</c> of
    /// its own, so Exit hides it - and unlike the faction chooser, whose Exit is routed to its Validate
    /// handler, hiding this one commits nothing at all.
    /// </summary>
    public sealed class SystemSelectionScreen : Screen
    {
        private static readonly object HeadersStop = "syssel:headers";
        private static readonly object LinesStop = "syssel:lines";
        private static readonly object ActionsStop = "syssel:actions";

        /// <summary>
        /// How deep inside a cell to look for what it is drawing.
        ///
        /// Two, measured against this table: every word and every icon a cell shows sits at one or two
        /// levels down (the status cell's label under its group, the planet circles under theirs). The
        /// third level exists and is not part of the cell - it is the policy drop list's closed POPUP,
        /// whose seven entries would otherwise be read out as though the cell were showing all of them
        /// at once.
        /// </summary>
        private const int MaxCellDepth = 2;

        private readonly List<GuiTableHeader> _headers = new List<GuiTableHeader>();
        private readonly List<AgeTransform> _cells = new List<AgeTransform>();

        public override string Key
        {
            get { return "screen.system-selection"; }
        }

        /// <summary>
        /// Over the star system page and the panel a planet card can slide out under it, and BELOW two
        /// things this window itself can raise: the tutorial page it registers a key for
        /// (<c>AddTutorialKeyIFN</c>) and the drop list its policy column opens.
        /// </summary>
        public override int Layer
        {
            get { return 25; }
        }

        /// <summary>What the window has written across its top - "Select a System". The window does not
        /// expose the label, so it is found where it is drawn.</summary>
        public override string ScreenName
        {
            get { return Title(Window()); }
        }

        /// <summary>The headers, because they are drawn first and Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return HeadersStop; }
        }

        public override bool IsActive()
        {
            SystemSelectionModalWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's own: Exit hides the window and commits nothing.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            SystemSelectionModalWindow window = Window();
            GuiTable table = Table(window);
            if (table == null)
            {
                return;
            }

            ReadHeaders(table);

            builder.BeginStop(HeadersStop);
            BuildHeaders(builder, table);

            builder.BeginStop(LinesStop);
            BuildLines(builder, table, Title(window));

            builder.BeginStop(ActionsStop);
            BuildActions(builder, window);
        }

        // ---- the sort headers ----

        /// <summary>One node per column heading. Enter sorts by it - the header's own click - and the
        /// heading the table is sorted by says so, watched live, which is how the player hears that a
        /// press took: the game gives sorting no other feedback than the order of the rows.</summary>
        private void BuildHeaders(GraphBuilder builder, GuiTable table)
        {
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
                    () => HeaderText(header),
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
                    ControlId.Referenced(header, "syssel:header/" + property + "/" + i),
                    vtable
                );
            }

            builder.EndRow();
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

        // ---- the systems ----

        /// <summary>
        /// Every system the table is showing, a row each, a cell at a time.
        ///
        /// Every column is declared even when it is drawing nothing - this table keeps a Hero and a
        /// Resources column that most empires leave empty - because a cell that dropped out of a row
        /// would shift the columns under the player and take up-and-down's column with them. An empty
        /// one reads the word for empty under the caption its edge already said.
        /// </summary>
        private void BuildLines(GraphBuilder builder, GuiTable table, string title)
        {
            List<GuiTableLine> lines = Lines(table);
            GraphSheet sheet = new GraphSheet(builder, "syssel:");
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

                sheet.RowAt(PrimaryVtable(table, line, _cells[0]), SystemOf(line), cells);
            }

            sheet.Finish();
        }

        /// <summary>The captions the sheet speaks when the player crosses into a column, read off a
        /// real row: which heading is over which column is the pairing <see cref="HeaderFor"/> makes,
        /// and it survives a re-sort. Every row of this table has the same columns, so the first one
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
                    columns[i - 1] = HeaderText(HeaderFor(cells[i], i));
                }

                return columns;
            }

            return null;
        }

        /// <summary>
        /// The row itself, which is the column the game draws the system's NAME in: the name, and
        /// whether the row is the one selected. The figures are the cells beside it.
        ///
        /// Both membership words are spoken, not just "selected": this is a list the player is choosing
        /// from, and a row that says nothing when it is out of the selection leaves them counting
        /// silences. The whole row is still one keypress away in the review buffer, where it is read at
        /// read time rather than watched: working it out means walking thirteen cells and nothing about
        /// them changes under a standing cursor.
        /// </summary>
        private NodeVtable PrimaryVtable(GuiTable table, GuiTableLine line, AgeTransform cell)
        {
            GuiTable owner = table;
            GuiTableLine row = line;
            AgeTransform widget = line.AgeTransform;
            AgeTransform name = cell;
            NodeVtable vtable = GraphNodes.SelectionItem(
                () => SystemName(row, name),
                () => Selected(owner, row),
                null,
                () => AgeWidgets.Toggle(row.SelectionToggle),
                () => AgeWidgets.Operable(widget),
                line.Tooltip,
                null,
                () => RowFacts(row)
            );
            AgeWidgets.Point(vtable, row.SelectionToggle, line.Tooltip, widget);
            return vtable;
        }

        /// <summary>One column of a row: what it is showing, with the game's own tooltip for the
        /// column behind it. It does NOT say its heading - the sheet says that as the edge the player
        /// crossed to get here, so a cell reached sideways is named once and a cell reached from the
        /// row above is named by the row it belongs to. Enter here is the row's click, the same as on
        /// the name - a player who has arrowed across to compare Approval should not have to arrow
        /// back to pick the system they just compared.</summary>
        private NodeVtable CellVtable(
            GuiTable table,
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header
        )
        {
            GuiTable owner = table;
            GuiTableLine row = line;
            AgeTransform widget = line.AgeTransform;
            AgeTransform it = cell;
            GuiTableHeader heading = header;
            Func<bool> selected = () => Selected(owner, row);
            Func<bool> enabled = () => AgeWidgets.Operable(widget);
            NodeVtable vtable = Policy(it, heading, enabled);
            if (vtable == null)
            {
                vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.ValuePart(() => CellText(it)),
                    },
                    Sections = GraphNodes.Sections(() => CellFacts(heading, it), TooltipOf(it)),
                };
                vtable.OnActivate = () =>
                {
                    if (enabled())
                    {
                        AgeWidgets.Toggle(row.SelectionToggle);
                    }
                };
                vtable.StateText = () =>
                    selected() ? ModStrings.Get(ModStrings.NavSelected) : null;
            }

            vtable.Announcements.Add(GraphNodes.SelectedPart(selected));
            vtable.Announcements.Add(GraphNodes.DisabledPart(enabled));
            vtable.SearchText = () => SystemName(row, null);
            AgeWidgets.PointAt(vtable, it);
            return vtable;
        }

        /// <summary>
        /// The automation policy column, where the game draws a DROP LIST rather than a readout - and
        /// so, where the game leaves it operable, the cell is a combo box and Enter opens the list
        /// instead of selecting the row.
        ///
        /// Null for every other column, and for a policy the game has switched off: an empire without
        /// system automation gets the list drawn disabled, and a disabled list is a readout of what the
        /// system is doing instead.
        ///
        /// Like every other cell it does not say its own heading - the crossed edge does - but the list
        /// it opens is still TITLED with it, because that window is somewhere the player has been taken.
        /// </summary>
        private NodeVtable Policy(
            AgeTransform cell,
            GuiTableHeader header,
            Func<bool> enabled
        )
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
                () => SettingRows.OpenList(it, HeaderText(heading)),
                () => AgeWidgets.Operable(it.AgeTransform),
                TooltipOf(widget),
                null,
                () => CellFacts(heading, widget)
            );
        }

        /// <summary>The row for the review buffer: one line per column, empties included - a buffer is
        /// walked column by column and a missing line is a column the player would count wrong.
        /// </summary>
        private IList<string> RowFacts(GuiTableLine line)
        {
            List<AgeTransform> cells = CellsOf(line);
            List<string> lines = new List<string>();
            for (int i = 1; i < cells.Count; i++)
            {
                string fact = new MessageBuilder()
                    .ListItem(HeaderText(HeaderFor(cells[i], i)))
                    .ListItem(CellText(cells[i]))
                    .Build();
                if (!string.IsNullOrEmpty(fact))
                {
                    lines.Add(fact);
                }
            }

            return lines;
        }

        /// <summary>This cell for the review buffer: its own heading and value, then the words hanging
        /// off whatever it draws inside itself - which planets a status circle stands for, what the
        /// number beside a growth arrow counts. The cell's own tooltip is declared separately, so it is
        /// not repeated here.</summary>
        private static IList<string> CellFacts(GuiTableHeader header, AgeTransform cell)
        {
            List<string> lines = new List<string>();
            try
            {
                string drawn = DrawnText(cell);
                string fact = new MessageBuilder()
                    .ListItem(HeaderText(header))
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
                Log.Warn("system selection: reviewing a column threw: " + e);
            }

            return lines;
        }

        // ---- reading a cell ----

        /// <summary>What a cell is showing, with the word for showing nothing - the Hero and Resources
        /// columns are drawn empty for most systems and still have to read.</summary>
        private static string CellText(AgeTransform cell)
        {
            return DrawnText(cell) ?? ModStrings.Get(ModStrings.NavCellEmpty);
        }

        /// <summary>Everything the player can see in a cell, or null when it is showing nothing: its
        /// words, and - for a column drawn as a picture, like the policy icon - the first thing its
        /// pictures say for themselves.</summary>
        private static string DrawnText(AgeTransform cell)
        {
            MessageBuilder labels = new MessageBuilder();
            List<string> tooltips = new List<string>();
            try
            {
                CollectDrawn(cell, labels, tooltips, 0);
            }
            catch (Exception e)
            {
                Log.Warn("system selection: reading a column threw: " + e);
            }

            string drawn = labels.Build();
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            return tooltips.Count > 0 ? tooltips[0] : null;
        }

        private static void CollectDrawn(
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
        private static void CollectTooltips(AgeTransform widget, List<string> into, int depth)
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

        /// <summary>A tooltip's words, but only where the words are actually in it: the ones this table
        /// hangs on its number columns name a simulation property and are assembled by the tooltip
        /// window at draw time, so there is nothing in them to read off the widget.</summary>
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

        // ---- the bottom band ----

        /// <summary>Cancel and Confirm, taken from the band they share rather than named: the window
        /// exposes Confirm and leaves Cancel as its sibling, and reading the band keeps them in the
        /// order they are drawn in. Confirm is disabled until a system is picked, which is what makes
        /// it read unavailable with the game's own sentence for what it would do.</summary>
        private void BuildActions(GraphBuilder builder, SystemSelectionModalWindow window)
        {
            AgeTransform validate = ValidateTransform(window);
            AgeTransform band = validate == null ? null : validate.Parent;
            List<AgeTransform> buttons = new List<AgeTransform>();
            try
            {
                List<AgeTransform> children = band == null ? null : band.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (
                        child != null
                        && AgeWidgets.Visible(child)
                        && AgeWidgets.Button(child) != null
                    )
                    {
                        buttons.Add(child);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("system selection: reading the button band threw: " + e);
            }

            if (buttons.Count == 0)
            {
                return;
            }

            builder.StartRow();
            for (int i = 0; i < buttons.Count; i++)
            {
                AgeTransform button = buttons[i];
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeWidgets.TextOf(button),
                    () => AgeWidgets.Press(button),
                    () => AgeWidgets.Operable(button),
                    AgeWidgets.Raw(button)
                );
                AgeWidgets.Point(vtable, AgeWidgets.Button(button));
                builder.AddItem(
                    ControlId.Referenced(button, "syssel:button/" + NameOf(button)),
                    vtable
                );
            }

            builder.EndRow();
        }

        // ---- reading the window ----

        /// <summary>The window's own title, found where it is drawn: the class exposes its table and
        /// its Confirm button and nothing else.</summary>
        private static string Title(SystemSelectionModalWindow window)
        {
            try
            {
                if (window == null)
                {
                    return null;
                }

                AgePrimitiveLabel[] labels =
                    window.GetComponentsInChildren<AgePrimitiveLabel>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] != null && labels[i].name == "WindowTitle")
                    {
                        return AgeText.Label(labels[i]);
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        private void ReadHeaders(GuiTable table)
        {
            _headers.Clear();
            try
            {
                AgeTransform headers = table.HeadersTable;
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
                Log.Warn("system selection: reading the column headings threw: " + e);
            }
        }

        /// <summary>The rows the table is showing. A table POOLS its lines rather than destroying them,
        /// and the surplus is left transparent rather than hidden - so a row is only real when the game
        /// has a system bound to it.</summary>
        private static List<GuiTableLine> Lines(GuiTable table)
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
                        && SystemOf(line) != null
                    )
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("system selection: reading the system list threw: " + e);
            }

            return lines;
        }

        private void ReadCells(GuiTableLine line)
        {
            _cells.Clear();
            Collect(line, _cells);
        }

        /// <summary>A row's columns as their own list, for the two readouts that walk the whole row at
        /// READ time - the summary a player hears and the lines they can review. Build's own copy is
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
                Log.Warn("system selection: reading a system's columns threw: " + e);
            }
        }

        /// <summary>The heading over a cell: paired by the column's own name, so the pairing survives a
        /// re-sort, and by position for a cell that does not say which column it is.</summary>
        private GuiTableHeader HeaderFor(AgeTransform cell, int index)
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

        private static string HeaderText(GuiTableHeader header)
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

        private static AgeTooltip TooltipOf(AgeTransform cell)
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

        /// <summary>The system a row stands for. The wrapper the table binds is rebuilt on every
        /// refresh, so it is the system underneath it that identifies the row.</summary>
        private static ColonizedStarSystem SystemOf(GuiTableLine line)
        {
            try
            {
                GuiColonizedStarSystem wrapper =
                    line == null ? null : line.Data as GuiColonizedStarSystem;
                return wrapper == null ? null : wrapper.ColonizedStarSystem;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the row is called: the name the game has written in its first column, and the
        /// system's own name when the cell is not the one being asked.</summary>
        private static string SystemName(GuiTableLine line, AgeTransform cell)
        {
            string drawn = cell == null ? null : DrawnText(cell);
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            try
            {
                GuiColonizedStarSystem wrapper =
                    line == null ? null : line.Data as GuiColonizedStarSystem;
                return wrapper == null ? null : AgeText.Clean(wrapper.LocalizedName);
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

        private static string NameOf(AgeTransform widget)
        {
            try
            {
                return widget.name;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static AgeTransform ValidateTransform(SystemSelectionModalWindow window)
        {
            try
            {
                return window == null || window.ValidateButton == null
                    ? null
                    : window.ValidateButton.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GuiTable Table(SystemSelectionModalWindow window)
        {
            try
            {
                return window == null ? null : window.GuiTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static SystemSelectionModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<SystemSelectionModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

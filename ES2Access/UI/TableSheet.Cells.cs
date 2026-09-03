using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI.Input;

namespace ES2Access.UI
{
    public sealed partial class TableSheet
    {
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
            _supplied.Clear();
            _hovers.Clear();
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
                // What the ROW is explained by, asked once and handed to every cell of it: a cell
                // whose own tooltip is that same surface has nothing of its own to say.
                AgeTooltip rowTip = Explains(line, _cells[0]);
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
                                CellVtable(table, line, cell, header, rowTip)
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
            AgeTooltip explains = Explains(line, cell);
            NodeVtable vtable;
            if (Choosable(table))
            {
                vtable = GraphNodes.SelectionItem(
                    () => RowText(row, name),
                    () => Selected(owner, row),
                    null,
                    () => AgeWidgets.Toggle(row.SelectionToggle),
                    enabled,
                    explains,
                    () => RowFacts(row)
                );
                // A table row is not read as a radio button, though its selection IS one: the row's
                // name and its spoken "selected"/"not selected" carry the whole affordance, and a role
                // word on every row of a table the player was just told is a table is noise (owner
                // ruling 2026-08-14). Text is the role-less type with the table reading order.
                vtable.ControlType = ControlTypes.Text;
                AgeWidgets.Point(vtable, row.SelectionToggle, explains, widget);
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
                    Sections = GraphNodes.Sections(() => RowFacts(row), explains),
                };
                AgeWidgets.PointAt(vtable, widget);
            }

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
        /// The dossier the ROW is explained by.
        ///
        /// A table line normally carries it - <c>GuiTableLine.Tooltip</c>, which is what a mouse over
        /// any part of the row draws - and that is what every table here used. The marketplace's buy
        /// table does not: the game leaves the line's own field null and hangs the resource's dossier on
        /// each drawn CELL instead (measured live 2026-08-30), so the row's primary node offered nothing
        /// while the price cell beside it offered the whole dossier - the same resource, explained from
        /// a number and not from its name. The primary cell's own tooltip is the fallback, and it is the
        /// one a mouse over the row's NAME draws.
        ///
        /// Additive by construction: it answers only where the line's own tooltip was null or had
        /// nothing to draw, which is exactly where the row used to promise nothing at all. A table whose
        /// lines DO carry a drawable tooltip is untouched.
        /// </summary>
        private static AgeTooltip Explains(GuiTableLine line, AgeTransform cell)
        {
            AgeTooltip own = null;
            try
            {
                own = line == null ? null : line.Tooltip;
            }
            catch (Exception) { }

            if (AgeWidgets.Draws(own))
            {
                return own;
            }

            AgeTooltip drawn = AgeWidgets.Raw(cell);
            return AgeWidgets.Draws(drawn) ? drawn : own;
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
            GuiTableHeader header,
            AgeTooltip rowTip
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
                // A cell declares a tooltip only where the game gave that COLUMN something of its
                // own. Some tables hang one hover surface across the whole row - the marketplace's
                // buy lines carry the same Resource dossier, same class and same target, on the name,
                // the stock and the price alike (measured 2026-08-30) - and there the row already
                // says it, so a cell repeating it is the same sentence three times over. Identity,
                // never text: the election table's per-cell "Weak support" is a different surface on
                // every column and stays declared (owner ruling 2026-08-30).
                if (AgeWidgets.SameTooltip(cellTip, rowTip))
                {
                    cellTip = null;
                }

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
                    // ONE tooltip: the cell's own, or - where the column supplies that - the dossier
                    // the pointer is sent to. The rest of what <see cref="Inside"/> found is dropped,
                    // and dropping it takes nothing away: those are renderer-assembled by
                    // construction (Inside keeps only the class-backed ones), so their words exist
                    // solely while the game is drawing THEM, which it only ever does for the one the
                    // node points at. Measured 2026-08-28 on the load/save window's Content column,
                    // where four DLCItem dossiers were declared, three could never fill, and the
                    // cell's buffer read "Content, empty" either way. A sheet cell is not a group, so
                    // "a second hover surface is a child entry" has nowhere to put one here - which
                    // is the one place in the mod where that ruling costs a surface rather than
                    // moving it, and it costs nothing while Inside stays class-backed only.
                    Sections = GraphNodes.Sections(
                        () => CellFacts(heading, it),
                        cellTip ?? aim
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
    }
}

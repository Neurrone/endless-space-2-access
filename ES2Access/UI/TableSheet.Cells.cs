using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.Localization;
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
            // The per-cell memos are per FRAME, snapshot or no snapshot: they stand behind what a cell
            // SAYS, which is read when the cell is read and must be this frame's answer.
            _supplied.Clear();
            _hovers.Clear();
            _answers.Clear();
            _answeredRefusal = null;
            List<GuiTableLine> lines = Lines(table);
            GraphSheet sheet = new GraphSheet(builder, _key);
            if (!Rebound(table, title, lines))
            {
                // Same rows, same bindings: hand back what the last build minted. Only the MINTING is
                // skipped - every declaration is re-gated, and every word is still read when read.
                sheet.Region(title, _kept.Columns);
                sheet.Replay(_kept.Block);
                sheet.Finish();
                builder.LandStopOn(sheet.FirstRow);
                return sheet;
            }

            Kept keep = new Kept(table, title, _headers, lines, _rowRef);
            sheet.Records(keep.Block);
            keep.Columns = Columns(lines);
            sheet.Region(title, keep.Columns);
            for (int l = 0; l < lines.Count; l++)
            {
                GuiTableLine line = lines[l];
                ReadCells(line);
                if (_cells.Count == 0)
                {
                    continue;
                }

                List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>();
                // What the ROW is explained by, asked once and handed to every cell of it: a cell
                // whose own tooltip is that same surface has nothing of its own to say.
                AgeTooltip rowTip = Explains(line, _cells[0]);
                // Every cell keeps its LOGICAL column - the game's - whatever it is read as: a cell
                // read as several controls or several pieces puts them all under that one column, so
                // the caption is the column's for each of them and a row that draws more pieces than
                // its neighbour is still the same columns to Up and Down (GraphSheet.SheetCell).
                for (int i = 1; i < _cells.Count; i++)
                {
                    AgeTransform cell = _cells[i];
                    GuiTableHeader header = HeaderFor(cell, i);
                    IList<NodeVtable> parts = Split(line, cell, header, Operable(table, line));
                    if (parts != null)
                    {
                        for (int p = 0; p < parts.Count; p++)
                        {
                            // The part aimed the pointer at the control it declared, so the cell-wide
                            // aim is not applied over the top of it, and it answered for that control's
                            // own availability, so the row's is not said as well.
                            Adorn(table, line, parts[p], false);
                            cells.Add(new GraphSheet.SheetCell(i, p, parts[p]));
                        }

                        continue;
                    }

                    List<Piece> pieces = Pieces(cell, header, rowTip);
                    if (pieces == null)
                    {
                        cells.Add(
                            new GraphSheet.SheetCell(i, 0, CellVtable(table, line, cell, header, rowTip))
                        );
                        continue;
                    }

                    for (int p = 0; p < pieces.Count; p++)
                    {
                        cells.Add(
                            new GraphSheet.SheetCell(
                                i,
                                p,
                                PieceVtable(table, line, cell, header, pieces[p])
                            )
                        );
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
            // Only a build that got all the way here is kept: one that threw part way through declared
            // part of a table, and replaying that would hide the failure behind a shorter table.
            _kept = keep;

            // Tab into the table lands on a ROW - the selected one where there is one - and not on the
            // heading band declared above it, whose sorted column reads "selected" too.
            builder.LandStopOn(sheet.FirstRow);
            return sheet;
        }

        /// <summary>
        /// What one build of the rows declared, kept until the game rebinds the table.
        ///
        /// A row's STRUCTURE - how many cells, which column each is, what each of them is a control of
        /// and which widget it stands on - is settled when the game binds the line
        /// (<c>GuiTable.Refresh</c> makes a fresh wrapper per row and hands it to <c>GuiTableLine.Bind</c>,
        /// which is what repaints the cells). Between two binds the mod was reading the same widgets
        /// and minting the same keys, closures and edges sixty times a second, for a table whose shape
        /// had not moved.
        /// </summary>
        private sealed class Kept
        {
            public readonly GraphSheet.Block Block = new GraphSheet.Block();
            public string[] Columns;

            public readonly GuiTable Table;
            public readonly string Title;
            public readonly string Language;
            public readonly GuiTableHeader[] Headers;
            public readonly GuiTableLine[] Lines;
            public readonly object[] Bound;
            public readonly object[] Refs;

            /// <summary>What the rows were DRAWING when they were read. See <see cref="PaintOf"/>.
            /// </summary>
            public readonly long Paint;

            public Kept(
                GuiTable table,
                string title,
                List<GuiTableHeader> headers,
                List<GuiTableLine> lines,
                RowObject rowRef
            )
            {
                Table = table;
                Title = title;
                Language = ModLocale.Language;
                Headers = headers.ToArray();
                Lines = lines.ToArray();
                Bound = new object[lines.Count];
                Refs = new object[lines.Count];
                for (int i = 0; i < lines.Count; i++)
                {
                    Bound[i] = lines[i].Data;
                    Refs[i] = rowRef(lines[i]);
                }

                Paint = PaintOf(lines);
            }
        }

        private Kept _kept;

        /// <summary>
        /// Whether this frame's rows are a different table from the one <see cref="_kept"/> holds - in
        /// which case they are read off the widgets again.
        ///
        /// Everything the declarations were minted from is asked: the table itself, the region's title
        /// and the language its captions were written in, the heading widgets (which is what pairs a
        /// caption to a column), the line widgets in the order they are drawn (a re-sort moves them),
        /// each line's own object, and the thing each row STANDS FOR. A rebind replaces a line's Data
        /// with a fresh wrapper, so it is the game's own signal that the row was repainted.
        ///
        /// And then what the rows are DRAWING (<see cref="PaintOf"/>), because the game also repaints a
        /// line WITHOUT rebinding it, and that changes the answer.
        /// </summary>
        private bool Rebound(GuiTable table, string title, List<GuiTableLine> lines)
        {
            Kept kept = _kept;
            try
            {
                if (
                    kept == null
                    || !ReferenceEquals(kept.Table, table)
                    || !string.Equals(kept.Title, title, StringComparison.Ordinal)
                    || !string.Equals(kept.Language, ModLocale.Language, StringComparison.Ordinal)
                    || kept.Headers.Length != _headers.Count
                    || kept.Lines.Length != lines.Count
                )
                {
                    return true;
                }

                for (int i = 0; i < kept.Headers.Length; i++)
                {
                    if (!ReferenceEquals(kept.Headers[i], _headers[i]))
                    {
                        return true;
                    }
                }

                for (int i = 0; i < kept.Lines.Length; i++)
                {
                    GuiTableLine line = lines[i];
                    if (
                        !ReferenceEquals(kept.Lines[i], line)
                        || !ReferenceEquals(kept.Bound[i], line.Data)
                        || !ReferenceEquals(kept.Refs[i], _rowRef(line))
                    )
                    {
                        return true;
                    }
                }

                return kept.Paint != PaintOf(lines);
            }
            catch (Exception e)
            {
                Log.Warn("table: comparing the kept rows threw: " + e);
                return true;
            }
        }

        /// <summary>
        /// A fingerprint of what the table's rows are DRAWING, folded over every widget inside every
        /// cell of every row: whether it is switched on, whether it is painted at all
        /// (<c>Alpha &gt; 0</c>, which is what <see cref="AgeWidgets.Painted"/> reads), and how many
        /// children it has. Three flag reads per widget, no lookup of any kind and no allocation -
        /// the fold is arithmetic (<see cref="PaintFold"/>).
        ///
        /// It is walked EVERY frame, which is the price of noticing the other way a row changes.
        /// A rebind - a fresh wrapper into <c>GuiTableLine.Bind</c> - is caught by identity
        /// (<see cref="Rebound"/>), but the game also calls <c>GuiTableLine.Refresh()</c> on its own
        /// (<c>StarSystemsManagementPanel.OnAssignmentOrderProcessed</c>, after a hero is assigned),
        /// which repaints a row's cells while keeping its <c>Data</c>. That repaint flips Visible on
        /// the fixed widgets inside the cells and, in four cell kinds, reserves pooled children - and
        /// which widgets are painted inside a cell is exactly what decides how many PIECES the cell is
        /// read as, and which surface each of them points at. A cell's words are read when the cell is
        /// read and need none of this; its shape is minted at build time and does.
        ///
        /// The root is the row's cell table rather than its cells, so a cell appearing or leaving is a
        /// change too; the depth budget is one level for the cells themselves plus the
        /// <see cref="DeepCellDepth"/> the piece walk reaches inside each of them, so the signature
        /// covers every widget that walk could find.
        /// </summary>
        private static long PaintOf(List<GuiTableLine> lines)
        {
            long hash = PaintFold.Seed;
            for (int i = 0; i < lines.Count; i++)
            {
                GuiTableLine line = lines[i];
                hash = Painting(line == null ? null : line.CellsTable, hash, 0);
            }

            return hash;
        }

        private static long Painting(AgeTransform widget, long hash, int depth)
        {
            if (widget == null)
            {
                return PaintFold.Fold(hash, -1);
            }

            List<AgeTransform> children = widget.Children;
            int count = children == null ? 0 : children.Count;
            // Not an existence test: nothing is declared or dropped here. The two flags are the
            // CONTENT of the fingerprint - they are what a repaint moves, and what the piece walk
            // will read the next time the rows are minted - so they are folded in whatever they say,
            // and the walk never branches on them.
            hash = PaintFold.Fold(
                hash,
                (widget.Visible ? 1 : 0) | (widget.Alpha > 0f ? 2 : 0) | (count << 2)
            );
            if (depth >= DeepCellDepth + 1)
            {
                return hash;
            }

            for (int i = 0; i < count; i++)
            {
                hash = Painting(children[i], hash, depth + 1);
            }

            return hash;
        }

        /// <summary>The captions the sheet speaks when the player crosses into a column, read off a
        /// real row: which heading is over which column is the pairing <see cref="HeaderFor"/> makes,
        /// and it survives a re-sort. Every row of one table has the same columns, so the first one
        /// that has any answers for all of them. One caption per LOGICAL column: a cell read as several
        /// controls or pieces keeps them all under the caption of the column it came out of - they ARE
        /// that one column of the game's table, and there is no other caption for them to have.
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
                for (int i = 0; i < cells.Count; i++)
                {
                    columns.Add(Caption(HeaderFor(cells[i], i)));
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
        /// silences. Its review buffer is the name cell's own, like every other column's: the
        /// column's caption and value, the words drawn inside the cell, and any lines the screen adds
        /// for the row - never the other columns.
        /// </summary>
        private NodeVtable PrimaryVtable(GuiTable table, GuiTableLine line, AgeTransform cell)
        {
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
                    Picked(table, line),
                    null,
                    () => AgeWidgets.Toggle(row.SelectionToggle),
                    enabled,
                    explains,
                    () => NameFacts(row, name)
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
                    Sections = GraphNodes.Sections(() => NameFacts(row, name), explains),
                };
                // Aimed at the tooltip the node READS, not at the line: where the row's dossier hangs on
                // the name cell rather than the line, pointing at the line drew nothing while the
                // readout spoke the cell's words (measured on the journal 2026-09-17). The choosable
                // branch above already aims this way.
                AgeWidgets.PointAt(vtable, widget, explains);
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
            if (line.DoubleClickButton == null || vtable.OnDoubleClick != null)
            {
                return;
            }

            vtable.OnDoubleClick = DoubleClicks(line);
        }

        /// <summary>The row's second click, as the one handler every cell of it shares (see
        /// <see cref="Picked"/>): what the gesture does is a fact about the ROW, so there is one of it
        /// however many columns offer it.</summary>
        private Action DoubleClicks(GuiTableLine line)
        {
            if (ReferenceEquals(line, _clicksLine) && _clicks != null)
            {
                return _clicks;
            }

            GuiTableLine row = line;
            _clicksLine = line;
            _clicks = () =>
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
            return _clicks;
        }

        /// <summary>
        /// The tooltip the CELL itself explains its value with, or null where it has none.
        ///
        /// A prefab may hang an EMPTY tooltip on the cell - no class, no words and nothing to assemble
        /// words from (<see cref="AgeWidgets.NeverDraws"/>), which is decoration the game will never
        /// draw. Taken at its word it beat the real dossiers drawn inside the cell: the cell declared a
        /// surface with nothing in it and the pointer was sent nowhere, so the systems table's resources
        /// column offered a bare number and no explanation of it. A tooltip the game could never draw is
        /// no tooltip at all.
        ///
        /// Where the screen supplies the cell's value (<see cref="ReadValue"/>) the cell's own tooltip is
        /// already the value and is not a second thing to read.
        /// </summary>
        private AgeTooltip CellTooltip(GuiTableHeader header, AgeTransform cell)
        {
            if (Supplied(header, cell))
            {
                return null;
            }

            AgeTooltip own = TooltipOf(cell);
            return AgeWidgets.NeverDraws(own) ? null : own;
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
            GuiTableLine row = line;
            AgeTransform it = cell;
            GuiTableHeader heading = header;
            Func<bool> enabled = Operable(table, line);
            _saysRowRefusal = null;
            NodeVtable vtable = ReadCell == null ? null : ReadCell(row, it, heading, enabled);
            bool saysRefusal = vtable != null && ReferenceEquals(vtable, _saysRowRefusal);
            _saysRowRefusal = null;
            // A cell the screen answered for is still pointed where its explanation is: a button
            // cell hanging an empty placeholder on itself and its dossier on the picture inside would
            // otherwise aim the pointer at the placeholder and draw nothing.
            AgeTooltip aim = vtable == null || CellTooltip(heading, it) != null ? null : LastInside(it);
            if (vtable == null)
            {
                AgeTooltip cellTip = CellTooltip(heading, it);
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
                    vtable.OnActivate = Picks(table, line);
                }

                if (Choosable(table))
                {
                    vtable.StateText = SelectedText(table, line);
                }
            }

            // Declared for the screen's own cells too: a cell reads its caption from the edge the
            // player crossed, so its buffer is the one surface that has to carry the pair itself.
            vtable.BufferHead = () => CellHead(heading, it);
            Adorn(table, line, vtable, !saysRefusal);
            // Aimed ONCE: the dossier aim rewrites every field the bare aim writes, so making both
            // was a closure and three delegates per cell per frame, thrown away by the next line.
            if (aim != null)
            {
                AgeWidgets.PointAt(vtable, it, aim);
            }
            else
            {
                AgeWidgets.PointAt(vtable, it);
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
            GuiTableLine row = line;
            if (Choosable(table))
            {
                vtable.Announcements.Add(GraphNodes.SelectedPart(Picked(table, line)));
            }

            if (availability)
            {
                vtable.Announcements.Add(GraphNodes.DisabledPart(Operable(table, line)));
            }

            // The row's second click, from any of its columns - for the same reason Enter here is the
            // row's click: a player who arrowed across to compare a figure should not have to arrow
            // back to act on the row they just compared.
            ShowOnMap(row, vtable);
            vtable.SearchText = Searches(line);
        }

        /// <summary>The name cell for the review buffer: the lines the screen adds for the row
        /// (<see cref="RowDetails"/>), then the cell's own facts exactly as every other column's
        /// (<see cref="CellFacts"/>).</summary>
        private IList<string> NameFacts(GuiTableLine line, AgeTransform cell)
        {
            List<string> lines = new List<string>();
            Extras(line, lines);
            IList<string> own = CellFacts(HeaderFor(cell, 0), cell);
            for (int i = 0; i < own.Count; i++)
            {
                lines.Add(own[i]);
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

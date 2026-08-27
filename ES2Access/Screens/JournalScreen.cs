using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// Every game this installation has finished, one line each - the journal the main menu opens, and the
    /// page a score screen opened from it goes back to (<c>JournalModalWindow</c>).
    ///
    /// One table and one box. The table is the game's own <c>EndGameSummaryTable</c> column set, read by
    /// the shared table reading (<see cref="TableSheet"/>). The box filters out games that were played
    /// with mods, and it is the game's own box: ticking it writes the preference to the registry and
    /// refreshes the list.
    ///
    /// Where a row is a LINE rather than a choice, and where its actions actually are, is the thing this
    /// page had to be told (measured): the window binds the table selectable, so a clicked row
    /// highlights, and then implements neither <c>OnLineSelection</c> nor <c>OnLineDoubleClick</c> - the
    /// highlight leads nowhere. What a row can DO is in its last column, where the game draws two buttons
    /// per row (<see cref="DetailsCells"/>): the score screen of that finished game, and delete. So the
    /// rows read as lines (<see cref="TableSheet.RowsAreLines"/>) and that one cell is read as two
    /// columns, which is where the row's actions are.
    ///
    /// The empty case is the one worth naming: a fresh installation has finished no games, and the window
    /// swaps the table for a line saying so. That line is read where the table would have been, because
    /// "there is nothing here" is the answer to the question the player came with.
    ///
    /// Escape is the game's and it is not a plain close: <c>HandleInput</c> hides the journal and shows the
    /// MAIN MENU, which is where the player came from.
    /// </summary>
    public sealed class JournalScreen : Screen
    {
        private static readonly object LinesStop = "journal:lines";
        private static readonly object ActionsStop = "journal:actions";

        /// <summary>The mod's own name for the page, for the frames before the window's heading is
        /// written. Optional: a build without the phrase says nothing rather than reading the
        /// key.</summary>
        private const string ScreenNameKey = "screen.journal";

        private readonly List<Cell> _cells = new List<Cell>();

        private readonly TableSheet _table;

        public JournalScreen()
        {
            _table = new TableSheet("journal:", SummaryOf);
            _table.RowName = SummaryName;
            _table.SplitCell = DetailsCells;
            _table.RowsAreLines = true;
        }

        public override string Key
        {
            get { return "screen.journal"; }
        }

        /// <summary>Above the game menu, so that a journal opened over a paused game is the page the
        /// player is on, and above the main menu it is usually opened from.</summary>
        public override int Layer
        {
            get { return 51; }
        }

        public override string ScreenName
        {
            get
            {
                string title = WindowShape.Title(Window());
                return string.IsNullOrEmpty(title) ? OptionalText.Phrase(ScreenNameKey) : title;
            }
        }

        /// <summary>The table, because it is drawn first and Tab does not wrap - its headings are the
        /// row above its first line, not a stop of their own.</summary>
        public override object InitialFocusStop
        {
            get { return LinesStop; }
        }

        public override bool IsActive()
        {
            try
            {
                JournalModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: Exit puts the main menu back.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            JournalModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            GuiTable table = Table(window);
            // Both bools decide whether a STOP is opened at all and which of two readings fills it -
            // flow, not one node's existence.
            bool rows = table != null && AgeWidgets.Visible(Lines(table));
            // The column headings are drawn over an EMPTY journal exactly as they are over a full one -
            // they still sort, and each still explains what its column holds - so the band follows what
            // the game draws rather than whether there is anything under it.
            bool headings = table != null && AgeWidgets.Visible(Headers(table));

            _cells.Clear();
            if (!rows)
            {
                // Nothing has been finished yet, or the list is still loading: the game's own line saying
                // so, where the table would have been.
                Cells.AddReadout(_cells, Transform(window.NoDataAvailableLabel), "journal:empty");
                Cells.AddReadout(_cells, window.LoadingFeedback, "journal:loading");
            }

            if (rows || headings || _cells.Count > 0)
            {
                builder.BeginStop(LinesStop);
                AddTitle(builder, window);
                if (headings)
                {
                    _table.Headers(builder, table);
                }

                if (rows)
                {
                    _table.Rows(builder, table, WindowShape.Title(window));
                }
                else
                {
                    Cells.EmitLinear(builder, _cells);
                }
            }

            _cells.Clear();
            WindowShape.Controls(_cells, window, "journal", Headers(table), Lines(table));
            if (_cells.Count > 0)
            {
                builder.BeginStop(ActionsStop);
                Cells.EmitLinear(builder, _cells);
            }
        }

        /// <summary>The window's heading as a row, and only where the game hung a sentence on it: the
        /// words are already this screen's spoken name, but a spoken name has no review buffer behind
        /// it, so an explanation has nowhere else to live (<see cref="Captions.Row"/>).</summary>
        private static void AddTitle(GraphBuilder builder, JournalModalWindow window)
        {
            AgeTransform title = WindowShape.TitleWidget(window);
            Captions.Row(builder, title, "journal:title", Parent(title));
        }

        private static AgeTransform Parent(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the Details column ----

        /// <summary>
        /// The last column, where the game draws the row's two BUTTONS rather than a figure: open the
        /// score screen of that finished game, and delete the entry. It is read as TWO columns, one per
        /// button, in the order they are drawn (<see cref="TableSheet.SplitCell"/>).
        ///
        /// This is the only door there is to a past game's score screen, and until it was declared the
        /// journal was a list a keyboard player could read and do nothing with: the row's own click goes
        /// nowhere (<see cref="TableSheet.RowsAreLines"/>) and the cell read as an ordinary figure.
        ///
        /// Two columns rather than one cell with two keys, because a button the player has to guess a
        /// second key for is a button they never find: each is walked into with right like every other
        /// column, and each keeps a plain Enter, which is the click the game itself puts on it. The
        /// delete is not made safer by hiding it - it is behind the game's OWN confirmation box
        /// (<c>GuiTableCellScoreScreenButton.OnDeleteEntryCb</c> :39-45), which is what asks the
        /// question.
        ///
        /// The game wrote no caption on either button, so each is named by the sentence its own tooltip
        /// says - the whole of it, since both are one line
        /// (<c>%VictoryScreenScoreScreenButtonDescription</c>,
        /// <c>%JournalModalWindowDeleteEntryDescription</c>) - and each cell's review buffer holds its
        /// own button's line and not the other's.
        ///
        /// The buttons are found by the handler the game wired to them rather than by their names in the
        /// prefab: what a button DOES is the thing being declared here.
        /// </summary>
        private IList<NodeVtable> DetailsCells(
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header,
            Func<bool> enabled
        )
        {
            AgeControlButton open = Wired(cell, "OnScoreScreenCb");
            AgeControlButton remove = Wired(cell, "OnDeleteEntryCb");
            if (open == null && remove == null)
            {
                return null;
            }

            List<NodeVtable> controls = new List<NodeVtable>(2);
            Add(controls, open, cell, enabled);
            Add(controls, remove, cell, enabled);
            return controls;
        }

        /// <summary>One of the cell's buttons as a column of its own: named by its own tooltip, pressed
        /// by Enter, and refusing in the game's own words.</summary>
        private void Add(
            List<NodeVtable> controls,
            AgeControlButton button,
            AgeTransform cell,
            Func<bool> enabled
        )
        {
            if (button == null)
            {
                return;
            }

            AgeTransform it = cell;
            AgeControlButton press = button;
            AgeTooltip tooltip = AgeWidgets.Raw(button.AgeTransform);
            Func<bool> rowEnabled = enabled;
            Func<bool> operable = () =>
                rowEnabled() && AgeWidgets.Operable(press.AgeTransform) && AgeWidgets.Enabled(it);
            NodeVtable vtable = new NodeVtable
            {
                // Named as a button, unlike the figures beside it: this column is a thing to press, and
                // the role word is the only thing that says so.
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.ValuePart(() => CellName(tooltip, press.AgeTransform)),
                    GraphNodes.DisabledPart(operable),
                },

                // Nothing behind it: the button's one line of tooltip is already the name, its column is
                // said by the edge the player crossed to get here, and there is nothing else drawn in it.
                // A cell-wide fact line here would read the OTHER button's sentence as well.
                OnActivate = () =>
                {
                    if (operable())
                    {
                        AgeWidgets.PressPropagating(press);
                    }
                },
            };

            GraphNodes.AddRefusal(vtable, tooltip, operable);
            AgeWidgets.Point(vtable, press);
            controls.Add(vtable);
        }

        /// <summary>What one of the buttons is called: the sentence it explains itself with, else
        /// whatever it is drawing - the game writes no caption on either.</summary>
        private string CellName(AgeTooltip tooltip, AgeTransform button)
        {
            string described = CardActions.FirstLine(tooltip);
            return string.IsNullOrEmpty(described) ? _table.CellText(button) : described;
        }

        /// <summary>The button inside a cell that the game wired to one named handler - which is what the
        /// button DOES, and the only thing about it that is not a prefab naming accident.</summary>
        private static AgeControlButton Wired(AgeTransform cell, string handler)
        {
            try
            {
                AgeControlButton[] buttons =
                    cell == null ? null : cell.GetComponentsInChildren<AgeControlButton>(true);
                for (int i = 0; buttons != null && i < buttons.Length; i++)
                {
                    AgeControlButton button = buttons[i];
                    if (
                        button.OnActivateMethod == handler
                        // Candidate choice, not existence: several buttons share a handler and the drawn
                        // one is the live one. The gate can only drop a node, never pick.
                        && AgeWidgets.Visible(button.AgeTransform)
                    )
                    {
                        return button;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        // ---- reading the window ----

        /// <summary>The game a row stands for. The wrapper the table binds is rebuilt on every refresh,
        /// so it is the summary underneath it that identifies the row.</summary>
        private static object SummaryOf(GuiTableLine line)
        {
            try
            {
                GuiEndGameSummary wrapper = line == null ? null : line.Data as GuiEndGameSummary;
                return wrapper == null ? null : wrapper.EndGameSummary;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the row is called when the name column draws nothing - the game's own title for
        /// the finished game.</summary>
        private static string SummaryName(GuiTableLine line)
        {
            try
            {
                GuiEndGameSummary wrapper = line == null ? null : line.Data as GuiEndGameSummary;
                return wrapper == null ? null : AgeText.Clean(wrapper.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GuiTable Table(JournalModalWindow window)
        {
            try
            {
                return window == null ? null : window.EndGameSummaryGuiTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The band of column captions and the band of rows, so the shared reading of the table
        /// and the shared reading of the window's controls do not both declare them.</summary>
        private static AgeTransform Headers(GuiTable table)
        {
            try
            {
                return table == null ? null : table.HeadersTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Lines(GuiTable table)
        {
            try
            {
                return table == null ? null : table.LinesTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Transform(AgePrimitiveLabel label)
        {
            try
            {
                return label == null ? null : label.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static JournalModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<JournalModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

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
    /// the shared table reading (<see cref="TableSheet"/>) - which also decides whether its rows are
    /// selectable, off the table's own flag, so a row here offers a choice exactly if the window gave it
    /// one. The box filters out games that were played with mods, and it is the game's own box: ticking it
    /// writes the preference to the registry and refreshes the list.
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
        private static readonly object HeadersStop = "journal:headers";
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

        /// <summary>The headers, because they are drawn first and Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return HeadersStop; }
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
            if (table != null && AgeWidgets.Visible(Lines(table)))
            {
                builder.BeginStop(HeadersStop);
                _table.Headers(builder, table);

                builder.BeginStop(LinesStop);
                _table.Rows(builder, table, WindowShape.Title(window));
            }
            else
            {
                // Nothing has been finished yet, or the list is still loading: the game's own line saying
                // so, where the table would have been.
                _cells.Clear();
                Cells.AddReadout(_cells, Transform(window.NoDataAvailableLabel), "journal:empty");
                Cells.AddReadout(_cells, window.LoadingFeedback, "journal:loading");
                if (_cells.Count > 0)
                {
                    builder.BeginStop(LinesStop);
                    Cells.Emit(builder, _cells);
                }
            }

            _cells.Clear();
            WindowShape.Controls(_cells, window, "journal", Headers(table), Lines(table));
            if (_cells.Count > 0)
            {
                builder.BeginStop(ActionsStop);
                Cells.Emit(builder, _cells);
            }
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

using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.Localization;
using ES2Access.UI;

using GameVictoryScreen = VictoryScreen;

namespace ES2Access.Screens
{
    /// <summary>The score graph read as the table it is a picture of, and the per-figure series the
    /// cells are read out of.</summary>
    public sealed partial class VictoryScreen
    {
        /// <summary>The empires the graph plots a curve for, in the order the selector lists them -
        /// the player first, then the rest in game order (<c>BindEmpires</c> :85-104).</summary>
        private readonly List<OfflineEmpire> _empires = new List<OfflineEmpire>();

        /// <summary>The toggle each of those empires is drawn as, in the same order - what a row is
        /// NAMED by, so the table says the words the selector above it says.</summary>
        private readonly List<AgeTransform> _empireWidgets = new List<AgeTransform>();

        /// <summary>One row of turn readings per empire, in <see cref="_empires"/> order. Each cell is
        /// the words for one plotted value, composed when the cell is read.</summary>
        private Func<string>[][] _series;

        private object _seriesSummary;

        private string _seriesFigure;

        private int _seriesEmpires;

        private string[] _headers;

        private int _headerTurns;

        private string _headerLanguage;

        /// <summary>
        /// The score graph, as the table it is a picture of: a row per empire the selector lists, a
        /// column per turn, and the value the game plotted in each cell.
        ///
        /// Nothing on the graph is text a walk could find. It is one coloured polyline per empire over
        /// a pair of numbered axes, with no name anywhere on it - which curve belongs to whom is its
        /// colour, and what a point is worth is where it sits between two axis marks. The numbers here
        /// are the same floats the renderer plots, read the same way it reads them
        /// (<c>VictoryScreenGraph.BuildValues</c> :95-121): the end-game snapshot, then the snapshot
        /// for turn i, then the one for that empire, then the figure. The columns are DISPLAYED turn
        /// numbers - a snapshot is taken with the turn the game is on
        /// (<c>GameInterface.TakeSnapshot</c> :112) and the interface writes that number plus one
        /// (<c>docs/notifications.md</c>) - so snapshot 0 is Turn 1 and the last column is the turn the
        /// game ended on. Each value goes through the formatter this page uses for every other number
        /// it draws (<c>FloatExtensions.ToString</c>, the game's plain whole number, which is what the
        /// score and the trivia figures are written with); the abbreviating formatter is deliberately
        /// not used, because an accumulated figure over a long game would read "1.2k" where the
        /// question is what it was worth.
        ///
        /// Turns ASCEND left to right (owner ruling), unlike the marketplace's price history, which
        /// opens on the newest price.
        ///
        /// Two things are deliberately not here. The axis scale labels: they exist to place a line on a
        /// picture, and the cells state the values themselves. And the game's highlight-the-selected-
        /// curve, which is not a second thing to model - the empire selector above IS that highlight,
        /// and picking a row of this table would be a second way to spend the same state.
        /// </summary>
        private void BuildHistory(
            GraphBuilder builder,
            GameVictoryScreen window,
            VictoryScreenScoresPanel panel
        )
        {
            VictoryScreenGraphSection graphs = panel == null ? null : panel.Graphs;
            if (graphs == null)
            {
                return;
            }

            AgeTransform empty = AgeWidgets.Transform(graphs.NotEnoughDataLabel);
            // Flow control, on a wired prefab field that is always there: this is the BRANCH the
            // section chooses between its two forms (<c>Bind</c> :47-49 shows one and hides the other
            // on the turn count), and the game's own words for a game too short to plot are then the
            // whole of the stop.
            if (empty != null && AgeWidgets.Visible(empty))
            {
                _cells.Clear();
                Cells.AddReadout(_cells, empty, "victory:history/no-data");
                Cells.EmitLinear(builder, _cells);
                return;
            }

            EndGameSummary summary = Summary(window);
            VictoryScreenFigureToggle figure = Figure(graphs);
            if (summary == null || figure == null || figure.FigureName == null)
            {
                return;
            }

            Listed(panel);
            int turns = summary.TurnNb;
            if (_empires.Count == 0 || turns <= 0)
            {
                return;
            }

            Series(summary, figure.FigureName.ToString(), turns);
            string[] headers = Headers(turns);

            GraphSheet sheet = new GraphSheet(builder, "victory:history/");
            // The region is named by the figure the game is plotting, in the game's own words: what
            // this table holds is that one figure and nothing else.
            sheet.Region(AgeWidgets.TextOf(AgeWidgets.Transform(figure.Label)), headers);
            for (int r = 0; r < _empires.Count; r++)
            {
                OfflineEmpire empire = _empires[r];
                AgeTransform drawn = _empireWidgets[r];
                NodeVtable primary = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeWidgets.TextOf(drawn)),
                    },
                };
                sheet.Row(primary, empire, null, _series[r]);
            }

            sheet.Finish();
            // Tab into the graph lands on an empire rather than on the first turn of one.
            builder.LandStopOn(sheet.FirstRow);
        }

        /// <summary>The empires in the order the SELECTOR lists them, with the toggle each is drawn as.
        /// Taken off the toggles the game bound rather than off the summary's own array: the player is
        /// first in the selector and somewhere in the middle of the array, and the table has to read in
        /// the order the page above it is walked. A frame where the toggles are not bound yet therefore
        /// has no rows, which is the right answer - there is no selector to read them in the order
        /// of.</summary>
        private void Listed(VictoryScreenScoresPanel panel)
        {
            _empires.Clear();
            _empireWidgets.Clear();
            GuiRadioGroup group = panel.EmpireRadioGroup;
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(
                group == null ? null : group.TogglesTable
            );
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                VictoryScreenEmpireToggle toggle =
                    child == null ? null : child.GetComponent<VictoryScreenEmpireToggle>();
                if (toggle != null && toggle.Empire != null)
                {
                    _empires.Add(toggle.Empire);
                    _empireWidgets.Add(child);
                }
            }
        }

        /// <summary>
        /// The readings for every empire over every turn, rebuilt only when the game changes what is
        /// plotted.
        ///
        /// A build must do no snapshot lookups: there are one per empire per turn of the whole game,
        /// and this runs every frame the page is up. The three things that decide the answer are all
        /// the game's own - which summary is bound, which figure the selector is on, and how many
        /// empires it lists - so the memo is keyed on those and on nothing about what this screen has
        /// done.
        /// </summary>
        private void Series(EndGameSummary summary, string figure, int turns)
        {
            if (
                _series != null
                && ReferenceEquals(summary, _seriesSummary)
                && figure == _seriesFigure
                && _seriesEmpires == _empires.Count
            )
            {
                return;
            }

            Func<string>[][] built = new Func<string>[_empires.Count][];
            for (int r = 0; r < _empires.Count; r++)
            {
                Func<string>[] row = new Func<string>[turns];
                for (int t = 0; t < turns; t++)
                {
                    // A turn with no reading is still a CELL - dropping it would put a later turn's
                    // value under the wrong column - and it says the word every other empty cell in
                    // the mod says.
                    row[t] = Nothing;
                }

                built[r] = row;
            }

            try
            {
                Snapshot snapshot = summary.EndGameSnapshot;
                for (int t = 0; snapshot != null && t < turns; t++)
                {
                    Snapshot turn;
                    if (
                        !snapshot.TryGetSnapshot(
                            string.Format(GameStatisticsManager.TurnSnapshotId, t),
                            out turn
                        )
                    )
                    {
                        continue;
                    }

                    for (int r = 0; r < _empires.Count; r++)
                    {
                        Snapshot empire;
                        float value;
                        if (
                            turn.TryGetSnapshot(_empires[r].Name.ToString(), out empire)
                            && empire.TryRead(figure, out value)
                        )
                        {
                            float plotted = value;
                            built[r][t] = () =>
                                Amplitude.Extensions.FloatExtensions.ToString(
                                    plotted,
                                    0,
                                    false,
                                    false
                                );
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("victory: reading the score history threw: " + e);
            }

            _series = built;
            _seriesSummary = summary;
            _seriesFigure = figure;
            _seriesEmpires = _empires.Count;
        }

        /// <summary>
        /// The column captions, worded when the table's shape moves rather than once a frame.
        ///
        /// The turn count is the whole of the table's shape, so it and the language decide the row of
        /// them. The first caption is the game's own word for the empire column of an end-game summary
        /// table (<c>%EndGameSummaryTablePlayerEmpireTitle</c>, the caption the journal's table draws);
        /// the word around the turn NUMBER is the mod's, which is what the language stamp is for
        /// (<see cref="ModLocale.Language"/>).
        /// </summary>
        private string[] Headers(int turns)
        {
            string language = ModLocale.Language;
            if (_headers != null && turns == _headerTurns && language == _headerLanguage)
            {
                return _headers;
            }

            string[] headers = new string[turns + 1];
            headers[0] = EmpireColumn();
            for (int c = 0; c < turns; c++)
            {
                // The DISPLAYED turn, under this table's own word for a turn column: a bare number
                // crossed into says nothing about what kind of number it is.
                headers[c + 1] = ModStrings.Format(ModStrings.VictoryHistoryTurn, c + 1);
            }

            _headers = headers;
            _headerTurns = turns;
            _headerLanguage = language;
            return headers;
        }

        /// <summary>The name the game itself puts over the empire column of an end-game summary table.
        /// Null where the localizer hands the key back unresolved, which leaves that crossing
        /// label-free rather than reading the key aloud.</summary>
        private static string EmpireColumn()
        {
            try
            {
                return Written(AgeText.Clean(Gui.Localize("%EndGameSummaryTablePlayerEmpireTitle")));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The figure the graph is plotting: the toggle the game has switched on. Asked of the
        /// toggles rather than of the group's <c>CurrentSelection</c>, because that index is into an
        /// array the group captured at load time and the figure list is re-filtered per empire.
        /// </summary>
        private static VictoryScreenFigureToggle Figure(VictoryScreenGraphSection graphs)
        {
            try
            {
                IList<AgeTransform> children = AgeWidgets.DrawnChildren(graphs.FigureTogglesTable);
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = AgeWidgets.DrawnChild(children, i);
                    AgeControlToggle toggle =
                        child == null ? null : AgeWidgets.Control(child) as AgeControlToggle;
                    if (toggle != null && toggle.State)
                    {
                        return child.GetComponent<VictoryScreenFigureToggle>();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("victory: finding the plotted figure threw: " + e);
            }

            return null;
        }

        /// <summary>The summary the window was bound with. The window keeps it private and hands it on
        /// to the panels, which keep it protected, so there is nowhere public to ask - one field read
        /// per build, through a handle resolved once.</summary>
        private static readonly FieldInfo SummaryField = typeof(GameVictoryScreen).GetField(
            "endGameSummary",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        private static EndGameSummary Summary(GameVictoryScreen window)
        {
            try
            {
                return SummaryField == null
                    ? null
                    : SummaryField.GetValue(window) as EndGameSummary;
            }
            catch (Exception e)
            {
                Log.Warn("victory: reading the end-game summary threw: " + e);
                return null;
            }
        }

        private static readonly Func<string> Nothing = () => null;
    }
}

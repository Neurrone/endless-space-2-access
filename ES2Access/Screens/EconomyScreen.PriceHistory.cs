using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The price-history sheet: the rows the graph draws a curve for, the state that keeps
    /// one curve revealed while the cursor rests on its row, and the restore that puts the rest back.
    /// </summary>
    public sealed partial class EconomyScreen
    {
        /// <summary>The resources the price-history table declared a row for, in the order it declared
        /// them - what turns the focused ROW back into the thing whose curve the game draws.</summary>
        private readonly List<GuiBuyable> _history = new List<GuiBuyable>();

        /// <summary>One token per tradable, so a history row keys and reconciles by the RESOURCE without
        /// carrying the resource itself: the buy table's own rows already carry that object, and two
        /// nodes sharing one reference are one control to the cursor.</summary>
        private readonly Dictionary<ulong, object> _historyKeys = new Dictionary<ulong, object>();

        /// <summary>Whether this screen is currently holding the price graph's curves dimmed. Only ever
        /// true while the cursor is on a history row, and the restore is the game's own call.</summary>
        private bool _dimmed;

        /// <summary>Which history row the buy table was last scrolled FOR - the row's own token, so a
        /// rebuilt wrapper is still the same row. It is what keeps the scroll to the moment focus
        /// ARRIVES: a scroll re-issued every frame would fight a hand on the wheel.</summary>
        private object _revealed;

        // ---- the price graph ----

        /// <summary>
        /// The price graph, as the table it is a picture of: a row per resource the buy table is showing,
        /// a column per turn, and the value the game plotted in each cell.
        ///
        /// Nothing on this panel is text a walk could find. The game draws a set of coloured curves with
        /// numbered axes and no name anywhere on it - which resource a line belongs to is its colour, and
        /// what a point is worth is where it sits between two axis labels. The series here are the same
        /// ones the renderer plots (<c>TradableHistoryCurvesPanel.Refresh</c> :95-152): the buy table's
        /// own lines, over the turn window the marketplace's <c>TradableHistorySpanTurnCount</c> property
        /// sets, trimmed at the front to the earliest turn any of them has a reading for. The columns are
        /// DISPLAYED turn numbers, because the game's own X axis draws snapshot turn plus one, and each
        /// value goes through the game's own amount formatter rounded to a whole number, the way its axis
        /// labels are.
        ///
        /// Two things are deliberately not here. The Y axis's scale labels: they exist to place a line on
        /// a picture, and the cells state the values themselves. And the game's
        /// highlight-the-selected-curve filter, which is a sighted-only narrowing of the same data
        /// (owner-approved) - what the mod does instead is <see cref="Curves"/>, which points the same
        /// highlight at whichever row the cursor is on.
        /// </summary>
        private void BuildHistory(GraphBuilder builder, MarketplaceBuyableItemsPanel panel)
        {
            _history.Clear();
            TradableHistoryCurvesPanel curves = panel.TradableHistoryCurvesPanel;
            // Flow control: whether the graph is read at all. Its rows are SYNTHETIC - they are built
            // from the game's snapshot lists, not from widgets - so no gate stands behind them, and this
            // is the whole of their existence test.
            if (curves == null || !AgeWidgets.Visible(curves.AgeTransform))
            {
                return;
            }

            AgeTransform empty = curves.NoDataAvailableGroup;
            // Flow control, on a wired prefab field that is always there: this is the BRANCH the panel
            // chooses between its two forms (<c>Refresh</c> :127-137 shows one and hides the other), and
            // the game's own words for an empty window are then the whole of the stop.
            if (empty != null && AgeWidgets.Visible(empty))
            {
                builder.BeginStop(HistoryStop);
                builder.PushContext(ModStrings.Get(ModStrings.EconomyPriceHistory));
                _cells.Clear();
                Cells.AddReadout(_cells, empty, "economy:history/no-data");
                Cells.EmitLinear(builder, _cells);
                builder.PopContext();
                return;
            }

            List<GuiTableLine> lines = _buyTable.Lines(panel.BuyableItemsGuiTable);
            for (int i = 0; i < lines.Count; i++)
            {
                ITradableItem item = lines[i] as ITradableItem;
                GuiBuyable buyable = item == null ? null : item.GuiTradable as GuiBuyable;
                if (buyable != null && buyable.History != null && buyable.History.Count > 0)
                {
                    _history.Add(buyable);
                }
            }

            int floor = WindowStart();
            int first = int.MaxValue;
            int last = -1;
            for (int r = 0; r < _history.Count; r++)
            {
                List<TradableSnapshot> history = _history[r].History;
                for (int s = 0; s < history.Count; s++)
                {
                    int turn = history[s].Turn;
                    if (turn < floor)
                    {
                        continue;
                    }

                    if (turn < first)
                    {
                        first = turn;
                    }

                    if (turn > last)
                    {
                        last = turn;
                    }
                }
            }

            // Flow control: no reading inside the window is a graph with nothing on it, and the game
            // draws its own words for that (above) rather than an empty table.
            if (last < first)
            {
                _history.Clear();
                return;
            }

            // NEWEST first (owner ruling 2026-08-30): what a resource is worth NOW is the question this
            // table is opened with, and the answer is then one press right of the row's name rather than
            // the length of the window away. So column 1 is the latest turn and the walk goes back.
            int columns = last - first + 1;
            string[] headers = new string[columns + 1];
            for (int c = 0; c < columns; c++)
            {
                // The DISPLAYED turn, under the same word the turn log names a turn by: a bare number
                // crossed into says nothing about what kind of number it is.
                headers[c + 1] = ModStrings.Format(ModStrings.HudTurnLogTurn, last - c + 1);
            }

            builder.BeginStop(HistoryStop);
            GraphSheet sheet = new GraphSheet(builder, "economy:history/");
            sheet.Region(ModStrings.Get(ModStrings.EconomyPriceHistory), headers);
            AddHistoryCaption(builder, sheet, curves);

            Func<string>[] cells = new Func<string>[columns];
            for (int r = 0; r < _history.Count; r++)
            {
                GuiBuyable buyable = _history[r];
                for (int c = 0; c < columns; c++)
                {
                    // A turn this resource has no reading for is still a CELL - dropping it would put a
                    // neighbour's price under the wrong turn on the way across - and it says the word
                    // every other empty cell in the mod says.
                    cells[c] = Nothing;
                }

                List<TradableSnapshot> history = buyable.History;
                for (int s = 0; s < history.Count; s++)
                {
                    int column = last - history[s].Turn;
                    if (column >= 0 && column < columns)
                    {
                        string drawn = Gui.FormatAmount(
                            UnityEngine.Mathf.RoundToInt(history[s].Value),
                            true,
                            false,
                            false
                        );
                        cells[column] = () => drawn;
                    }
                }

                string title = AgeText.Clean(buyable.Title);
                NodeVtable primary = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => title),
                    },
                };
                sheet.Row(primary, HistoryKey(buyable), null, cells);
            }

            sheet.Finish();
            // Tab into the graph lands on a resource rather than on the sentence about the graph, the
            // same rule every other table on this page lands by.
            builder.LandStopOn(sheet.FirstRow);
        }

        /// <summary>The one sentence the game writes about the graph, which it hangs on the panel itself
        /// rather than on any caption - there is no drawn heading here to carry it, so the row says the
        /// mod's own name for the block and the sentence goes in its buffer.</summary>
        private static void AddHistoryCaption(
            GraphBuilder builder,
            GraphSheet sheet,
            TradableHistoryCurvesPanel curves
        )
        {
            AgeTooltip about = AgeWidgets.Raw(curves.AgeTransform);
            if (about == null || !AgeWidgets.Draws(about))
            {
                return;
            }

            ControlId lead = ControlId.For(curves.AgeTransform, "economy:history/about");
            NodeVtable saying = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ModStrings.Get(ModStrings.EconomyPriceHistory)),
                },
            };
            saying.Sections = GraphNodes.SectionsFor(saying, about);
            builder.AddNode(Nodes.Drawn(lead, saying, curves.AgeTransform));
            sheet.Follows(lead);
        }

        private static readonly Func<string> Nothing = () => null;

        /// <summary>The oldest turn the price graph plots: the game's own window, read off the same
        /// marketplace property the curves panel binds itself with
        /// (<c>TradableHistoryCurvesPanel.Bind</c> :61-68, <c>Refresh</c> :93-94).</summary>
        private static int WindowStart()
        {
            try
            {
                int turn = Gui.Game.Turn;
                ITradingManagementService trading = Trading();
                if (trading == null)
                {
                    return 0;
                }

                int span = UnityEngine.Mathf.RoundToInt(
                    trading.SimulationObject.GetPropertyValue(
                        SimulationProperties.Marketplace.TradableHistorySpanTurnCount
                    )
                );
                return UnityEngine.Mathf.Clamp(turn - span, 0, turn);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>What a price-history row is keyed and reconciled by. Not the tradable itself, which
        /// the buy table's own rows already carry: reference identity is followed before the structural
        /// key, so two nodes sharing one object are one control to the cursor and focus would teleport
        /// between the graph and the table. A token per tradable keeps the row identified across a
        /// re-sort without being the same thing as the row below it.</summary>
        private object HistoryKey(GuiBuyable buyable)
        {
            ulong uid = buyable.Tradable.UID;
            object token;
            if (!_historyKeys.TryGetValue(uid, out token))
            {
                token = new object();
                _historyKeys[uid] = token;
            }

            return token;
        }

        /// <summary>
        /// The visual courtesy that goes with the price-history table: while the cursor is on one of its
        /// rows, the game's own graph draws that resource's curve bright and dims the others, so a
        /// sighted observer can see what is being read (owner-approved).
        ///
        /// The lever is the curve widget's own Enable flag, which is exactly what the game writes for its
        /// own highlight (<c>TradableHistoryCurvesPanel.RefreshTradableHistoryCurve</c> :177,193).
        /// Selecting the buy table's LINE would look the same and is deliberately not used: that
        /// selection is real trade state - it feeds the quantity and the totals - and walking a table
        /// must not spend it.
        ///
        /// The curve widgets carry no back-reference to their resource; they are bound positionally to
        /// the panel's own list, which is built from the POOLED order of the table's line components
        /// rather than the drawn order (<c>Refresh</c> :100-109), so the index is recomputed with the
        /// game's own enumeration. Reasserted every frame because the panel rebinds itself whenever the
        /// table scrolls, and handed back by calling the panel's own refresh, so what returns is the
        /// game's rule rather than the mod's guess at it.
        /// </summary>
        private void Curves()
        {
            try
            {
                MarketplaceBuyableItemsPanel buy = BuyPanel();
                TradableHistoryCurvesPanel curves =
                    buy == null ? null : buy.TradableHistoryCurvesPanel;
                if (curves == null)
                {
                    return;
                }

                GuiBuyable wanted = FocusedHistoryRow();
                Reveal(buy, wanted);
                int index = wanted == null ? -1 : CurveIndex(buy, wanted);
                IList<AgeTransform> children =
                    curves.TradableHistoryCurvesContainer == null
                        ? null
                        : curves.TradableHistoryCurvesContainer.Children;
                if (index < 0 || children == null)
                {
                    RestoreCurves();
                    return;
                }

                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] != null)
                    {
                        children[i].Enable = i == index;
                    }
                }

                _dimmed = true;
            }
            catch (Exception e)
            {
                Log.Warn("economy: highlighting a price curve threw: " + e);
            }
        }

        /// <summary>
        /// Bring the buy table's own line for the focused history row into its scrolling window.
        ///
        /// Not a courtesy: it is what makes the graph HAVE that resource's curve at all. The curves
        /// panel rebuilds its series from the lines the table is currently showing
        /// (<c>TradableHistoryCurvesPanel.Refresh</c> :100-109) and marks itself dirty whenever the
        /// table's virtual area moves more than two pixels (<c>SpecificUpdate</c> :85-93), so a row
        /// scrolled out of that window has no curve to point the highlight at - and on a section long
        /// enough to scroll, the rows the player is reading in the table below are exactly the ones that
        /// go out of it.
        ///
        /// Through the shared reveal, which asks the scroll view's own question - does this widget sit
        /// inside the viewport - and does nothing at all when it does. The engine offers no scroll-TO
        /// call to prefer over it: <c>AgeControlScrollView</c> is public only in its four Reset jumps
        /// and <c>MouseWheel</c>, and its clamping, its scrollbar placement and its OnScroll message all
        /// live behind the private <c>ConstraintAndPlace</c> - so replaying the wheel IS the engine's
        /// own entry point, and writing the virtual area directly would skip the notification the curves
        /// panel is watching for.
        ///
        /// Once per ARRIVAL, never per frame and never on the way out: a scroll the player made stays
        /// where they put it.
        /// </summary>
        private void Reveal(MarketplaceBuyableItemsPanel panel, GuiBuyable wanted)
        {
            object key = wanted == null ? null : HistoryKey(wanted);
            if (ReferenceEquals(key, _revealed))
            {
                return;
            }

            _revealed = key;
            if (wanted == null)
            {
                return;
            }

            AgeTransform line = LineOf(panel, wanted);
            if (line != null)
            {
                ScrollIntoView.Reveal(line);
            }
        }

        /// <summary>The widget the buy table draws this resource's row as, found the way the curves
        /// panel finds its own series - the game's own enumeration of the table's line components, which
        /// is why a row it has stopped enumerating is a row this answers nothing for.</summary>
        private static AgeTransform LineOf(MarketplaceBuyableItemsPanel panel, GuiBuyable wanted)
        {
            try
            {
                GuiTable table = panel == null ? null : panel.BuyableItemsGuiTable;
                if (table == null || wanted.Tradable == null)
                {
                    return null;
                }

                GuiTableLineBuyable[] found = BuyLines.Under(table);
                for (int i = 0; i < found.Length; i++)
                {
                    GuiBuyable buyable =
                        found[i] == null ? null : found[i].GuiTradable as GuiBuyable;
                    if (
                        buyable != null
                        && buyable.Tradable != null
                        && buyable.Tradable.UID == wanted.Tradable.UID
                    )
                    {
                        return found[i].AgeTransform;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: finding a resource's table row threw: " + e);
            }

            return null;
        }

        /// <summary>Give the graph back to the game, exactly as it had it.</summary>
        private void RestoreCurves()
        {
            if (!_dimmed)
            {
                return;
            }

            _dimmed = false;
            try
            {
                MarketplaceBuyableItemsPanel buy = BuyPanel();
                if (buy != null && buy.TradableHistoryCurvesPanel != null)
                {
                    buy.TradableHistoryCurvesPanel.OnSelectedItemChanged();
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: restoring the price curves threw: " + e);
            }
        }

        private static MarketplaceBuyableItemsPanel BuyPanel()
        {
            global::EconomyScreen window = Window();
            MarketplacePanel market = window == null ? null : window.MarketplacePanel;
            return market == null ? null : market.BuyableItemsPanel;
        }

        /// <summary>The resource the cursor is standing on a price-history row of, or nothing where it is
        /// standing anywhere else. Which row it is on is what says which resource: the sheet stamps the
        /// row on every one of its cells, so the answer is the same from any column.</summary>
        private GuiBuyable FocusedHistoryRow()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null || navigator.Screen != this)
            {
                return null;
            }

            GraphNode node = navigator.CurrentNode;
            if (node == null || !HistoryStop.Equals(node.StopKey) || node.Vtable == null)
            {
                return null;
            }

            TableRow row = node.Vtable.Row;
            int index = row == null ? 0 : row.Index;
            return index >= 1 && index <= _history.Count ? _history[index - 1] : null;
        }

        /// <summary>Which of the graph's curves is this resource's.</summary>
        private static int CurveIndex(MarketplaceBuyableItemsPanel panel, GuiBuyable wanted)
        {
            GuiTable table = panel.BuyableItemsGuiTable;
            if (table == null || wanted.Tradable == null)
            {
                return -1;
            }

            GuiTableLineBuyable[] found = BuyLines.Under(table);
            int index = 0;
            for (int i = 0; i < found.Length; i++)
            {
                GuiBuyable buyable = found[i] == null ? null : found[i].GuiTradable as GuiBuyable;
                if (buyable == null)
                {
                    continue;
                }

                if (buyable.Tradable != null && buyable.Tradable.UID == wanted.Tradable.UID)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }
    }
}

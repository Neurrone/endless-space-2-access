using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.Localization;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The support-history panel: the curves the senate draws down the left edge, as the
    /// table they are a picture of, and the snapshot of the game's own arithmetic behind them.
    /// </summary>
    public sealed partial class SenateScreen
    {
        /// <summary>One party's curve as a row: who it is, where its curve sits among the panel's
        /// children, and its share of the senate at each turn in the window.</summary>
        private sealed class SupportSeries
        {
            /// <summary>The wrapper the row is named from and whose dossier the icon at the end of
            /// the curve carries.</summary>
            public GuiPolitics Party;

            /// <summary>What the row is keyed and reconciled by - a token per party rather than the
            /// wrapper, which the senators' cards and the assembly's rows already stand for.</summary>
            public object Token;

            /// <summary>Which child of the container draws this party's curve. The panel reserves one
            /// per party it found support for and refreshes them in that same order
            /// (<c>SupportHistorySidePanel.Refresh</c> :130-131), so the position IS the binding.
            /// </summary>
            public int Slot;

            /// <summary>The shares as the cells say them, NEWEST first, built when the window moves
            /// rather than once a frame.</summary>
            public Func<string>[] Cells;
        }

        /// <summary>
        /// The whole table, worked out once per window rather than once a frame.
        ///
        /// What the window IS, and therefore what a fresh reading depends on: the senate it was read
        /// from, how many records it holds and the turn of the last one. A record is added at the end
        /// of every turn (<c>Senate.GameClientState_Turn_End_RefreshEmpirePoliticsSupport</c> :1402-1407)
        /// and the oldest fall off the front, so those three change together exactly when the numbers
        /// do. The language is in the key because the column captions are the mod's own words.
        /// </summary>
        private sealed class SupportHistory
        {
            public object Senate;

            public int Records;

            public int LastTurn;

            public string Language;

            public string[] Headers;

            public List<SupportSeries> Series;
        }

        private SupportHistory _support;

        /// <summary>One token per party, so a row keys and reconciles by the PARTY without carrying
        /// the party's wrapper: the senator cards and the assembly's own rows stand for the same
        /// parties, and two nodes sharing one reference are one control to the cursor. Held outside
        /// the snapshot so a turn passing does not re-mint the tokens and lose the cursor.</summary>
        private readonly Dictionary<string, object> _supportKeys = new Dictionary<string, object>();

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<SupportSeries> _supportRows = new List<SupportSeries>();
        private readonly List<SupportHistoryCurve> _supportCurves = new List<SupportHistoryCurve>();

        /// <summary>
        /// The support history, as the table it is a picture of: a row per party the panel draws a
        /// curve for, a column per turn in the window, and that party's share of the senate in each
        /// cell.
        ///
        /// Nothing on this panel is text a walk could find, which is why the generic side-panel
        /// reader yields it no cells at all and the stop simply did not exist. The game draws one
        /// coloured polyline per party across a 279x180 box with no axis, no caption and no number
        /// anywhere on it; which line belongs to whom is its colour and the small party icon parked
        /// at its right-hand end, and what a point is worth is how high it sits in the box. So the
        /// series are recomputed from the same records the renderer plots, by the same arithmetic:
        /// the parties in the order the panel first met them (neutrals left out, which is the
        /// panel's own filter, <c>Refresh</c> :87-101), and each record's values divided by their
        /// own sum where that sum is positive (:102-128), a party the record does not mention
        /// counting zero. Measured against the drawn polylines on 2026-09-14: identical to two
        /// decimal places in all 120 cells.
        ///
        /// The columns are the records' OWN turn numbers, not turn plus one. The marketplace's
        /// price graph adds one because the game's own X axis there draws it that way; this panel
        /// draws no axis to agree with, and a record is written at the end of the turn it is stamped
        /// with, so the turn it names is the turn whose senate it describes. Newest first, the same
        /// order the price graph reads in: what the parties hold NOW is the question the panel is
        /// looked at with, and the answer is then one press right of the row's name rather than the
        /// length of the window away.
        ///
        /// Two things are deliberately not here. The colours, which exist to tell curves apart in a
        /// picture and are what the row's name says in words. And a row for a party with no support
        /// anywhere in the window: the game unbinds that curve and draws nothing for it
        /// (<c>SupportHistoryCurve.Bind</c> :18-26, :60-63), so the row exists exactly while the
        /// game draws the party's icon.
        /// </summary>
        private bool BuildSupportHistory(GraphBuilder builder, SidePanel side)
        {
            try
            {
                SupportHistorySidePanel panel = side as SupportHistorySidePanel;
                // Flow control: which of the panel's two forms the game chose. Refresh (:73-139)
                // shows the container and hides the sentence, or the reverse - and under the
                // sentence there is nothing here to read and the shared reader takes the panel.
                if (
                    panel == null
                    || panel.SupportHistoryContainer == null
                    || !AgeWidgets.Visible(panel.SupportHistoryContainer)
                )
                {
                    return false;
                }

                SupportHistory history = Snapshot(panel);
                if (history == null)
                {
                    return false;
                }

                IList<AgeTransform> children = panel.SupportHistoryContainer.Children;
                _supportRows.Clear();
                _supportCurves.Clear();
                for (int i = 0; i < history.Series.Count; i++)
                {
                    SupportSeries series = history.Series[i];
                    SupportHistoryCurve curve = Curve(children, series.Slot);
                    if (curve != null)
                    {
                        _supportRows.Add(series);
                        _supportCurves.Add(curve);
                    }
                }

                if (_supportRows.Count == 0)
                {
                    return false;
                }

                GraphSheet sheet = new GraphSheet(builder, "senate:support/");
                sheet.Region(ModStrings.Get(ModStrings.SenateSupportPanel), history.Headers);
                for (int i = 0; i < _supportRows.Count; i++)
                {
                    SupportSeries series = _supportRows[i];
                    SupportHistoryCurve curve = _supportCurves[i];
                    GuiPolitics party = series.Party;
                    NodeVtable primary = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => AgeText.Clean(party.Title)),
                        },
                        // The party's dossier, on the game's own widget: the icon at the end of the
                        // curve is what a mouse hovers for it, and the tooltip is class-backed, so
                        // the renderer assembles the words and the review buffer carries them.
                        Sections = GraphNodes.Sections(null, curve.PoliticsTooltip),
                    };
                    AgeWidgets.PointAt(primary, curve.PoliticsGroup);
                    // The icon is the row's drawn evidence as well as its dossier: it is the one
                    // widget the panel hides when it unbinds a curve, so a row the game stopped
                    // drawing is a row the gate can take away.
                    sheet.Row(primary, series.Token, curve.PoliticsGroup, series.Cells);
                }

                sheet.Finish();
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("senate: reading the support history threw: " + e);
                return false;
            }
        }

        /// <summary>The curve the panel is drawing in one slot, or nothing where it is drawing none.
        /// A curve with no support anywhere in the window is unbound rather than removed, which
        /// leaves the child in place with its icon and its line switched off.</summary>
        private static SupportHistoryCurve Curve(IList<AgeTransform> children, int slot)
        {
            if (children == null || slot < 0 || slot >= children.Count)
            {
                return null;
            }

            AgeTransform child = children[slot];
            SupportHistoryCurve curve =
                child == null ? null : child.GetComponent<SupportHistoryCurve>();
            // Spoken count, and flow control for the row: the sheet counts its rows as it emits them
            // and stamps "n of m" on every cell, so a row left to the gate would be taken away with
            // the count already told. Asked of the icon rather than of the child, because a curve
            // the panel unbound keeps its child and hides only the line and the icon
            // (<c>SupportHistoryCurve.Unbind</c>).
            if (
                curve == null
                || curve.PoliticsGroup == null
                || curve.PoliticsTooltip == null
                || !AgeWidgets.Visible(curve.PoliticsGroup)
            )
            {
                return null;
            }

            return curve.PoliticsTooltip.Target as GuiPolitics == null ? null : curve;
        }

        /// <summary>The table as the records make it, rebuilt only when the records change.</summary>
        private SupportHistory Snapshot(SupportHistorySidePanel panel)
        {
            Senate senate = panel.Senate;
            if (senate == null || senate.PoliticalSupportHistory == null)
            {
                return null;
            }

            ReadOnlyCollection<PoliticalSupportHistoryRecord> records =
                senate.PoliticalSupportHistory.Records;
            // The panel's own threshold for having a graph at all (Refresh :77), asked again here
            // because the snapshot divides by the window and a window of one is no history.
            if (records == null || records.Count < 2)
            {
                return null;
            }

            int last = records[records.Count - 1].Turn;
            string language = ModLocale.Language;
            SupportHistory held = _support;
            if (
                held != null
                && ReferenceEquals(held.Senate, senate)
                && held.Records == records.Count
                && held.LastTurn == last
                && held.Language == language
            )
            {
                return held;
            }

            List<GuiPolitics> parties = new List<GuiPolitics>();
            List<StaticString> keys = new List<StaticString>();
            for (int r = 0; r < records.Count; r++)
            {
                foreach (KeyValuePair<StaticString, float> entry in records[r])
                {
                    if (keys.Contains(entry.Key))
                    {
                        continue;
                    }

                    GuiPolitics party = Gui.GuiWrapperProviderService.GetGuiPolitics(entry.Key);
                    if (
                        party == null
                        || party.PoliticsDefinition == null
                        || party.PoliticsDefinition.IsNeutral
                    )
                    {
                        continue;
                    }

                    keys.Add(entry.Key);
                    parties.Add(party);
                }
            }

            float[][] shares = new float[parties.Count][];
            for (int p = 0; p < parties.Count; p++)
            {
                shares[p] = new float[records.Count];
            }

            for (int r = 0; r < records.Count; r++)
            {
                float total = 0f;
                foreach (KeyValuePair<StaticString, float> entry in records[r])
                {
                    int p = keys.IndexOf(entry.Key);
                    if (p < 0)
                    {
                        continue;
                    }

                    shares[p][r] = entry.Value;
                    total += entry.Value;
                }

                // The game's own normalization, including its guard: a turn nobody supported anybody
                // in is left as the zeroes it is rather than divided by nothing.
                if (total > 0f)
                {
                    for (int p = 0; p < parties.Count; p++)
                    {
                        shares[p][r] /= total;
                    }
                }
            }

            SupportHistory history = new SupportHistory
            {
                Senate = senate,
                Records = records.Count,
                LastTurn = last,
                Language = language,
                Headers = Headers(records),
                Series = new List<SupportSeries>(parties.Count),
            };

            for (int p = 0; p < parties.Count; p++)
            {
                // The same test the game binds a curve by: a party with nothing anywhere in the
                // window has no line drawn for it, so it has no row either.
                bool any = false;
                for (int r = 0; r < records.Count && !any; r++)
                {
                    any = shares[p][r] > 0f;
                }

                if (!any)
                {
                    continue;
                }

                Func<string>[] cells = new Func<string>[records.Count];
                for (int c = 0; c < records.Count; c++)
                {
                    // The reading is taken here, and the WORDS for it are the cell's own, composed
                    // when the cell is read - as the game writes every other share of a whole.
                    float share = shares[p][records.Count - 1 - c];
                    cells[c] = () =>
                        Amplitude.Extensions.FloatExtensions.ToString(share, 0, true);
                }

                history.Series.Add(
                    new SupportSeries
                    {
                        Party = parties[p],
                        Token = Token(parties[p]),
                        Slot = p,
                        Cells = cells,
                    }
                );
            }

            _support = history;
            return history.Series.Count > 0 ? history : null;
        }

        /// <summary>The turn columns, newest first, under this table's own word for a turn column: a
        /// bare number crossed into says nothing about what kind of number it is. The primary column
        /// carries no caption, because the game draws none over the curves.</summary>
        private static string[] Headers(ReadOnlyCollection<PoliticalSupportHistoryRecord> records)
        {
            string[] headers = new string[records.Count + 1];
            for (int c = 0; c < records.Count; c++)
            {
                headers[c + 1] = ModStrings.Format(
                    ModStrings.SenateSupportHistoryTurn,
                    records[records.Count - 1 - c].Turn
                );
            }

            return headers;
        }

        /// <summary>What a support-history row is keyed and reconciled by.</summary>
        private object Token(GuiPolitics party)
        {
            string name = party.PoliticsDefinition.Name.ToString();
            object token;
            if (!_supportKeys.TryGetValue(name, out token))
            {
                token = new object();
                _supportKeys[name] = token;
            }

            return token;
        }
    }
}

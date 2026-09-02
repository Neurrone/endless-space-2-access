using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// What the fighting COST, as the report popups table it: the casualties and the ruined
    /// improvements a ground invasion left behind, each under the caption the game wrote its total
    /// into, and the plain report tables the space popups draw.
    ///
    /// Part of <see cref="BattleNotifications"/>; the popups themselves are declared there.
    /// </summary>
    internal static partial class BattleNotifications
    {
        /// <summary>
        /// One of the two things the invasion cost the system, as the game drew it: a caption with the
        /// total written into it, and a table of what made that total up.
        ///
        /// The caption NAMES the table rather than standing in it - the shared rule for a caption over
        /// a block (<see cref="Captions"/>), which the two of these used to break by being rows the
        /// player had to step past.
        ///
        /// A table the game filled with NOTHING is not an exception to that (owner ruling 2026-08-28):
        /// the block a player steps into has the same shape whichever way the invasion went, so the
        /// caption is the region's name there too and the count the caption drew into itself becomes
        /// the region's one row (<see cref="Total"/>). A caption the game is not drawing names nothing
        /// and gets no row - which is the pirate-lair case, where the whole report panel is switched
        /// off (<c>GroundBattleReportNotificationWindow.Refresh</c> :200).
        ///
        /// Emptiness is asked AFTER the ghosts are taken out (<see cref="Cells.Drawn"/>), not of the
        /// collected list: a pooled row the game retired at alpha 0 is collected and then dropped at
        /// emit, and a table holding only those would otherwise open a region with nothing in it.
        /// </summary>
        private static void Wreckage(
            GraphBuilder builder,
            AgePrimitiveLabel caption,
            string key,
            List<Cell> rows,
            bool counted
        )
        {
            AgeTransform widget = caption == null ? null : caption.AgeTransform;
            List<Cell> shown = new List<Cell>(rows.Count);
            Cells.Drawn(rows, shown);
            // A named block is a REGION of its own, not just a spoken caption: the region keys jump
            // caption to caption here the way they jump between every other drawn-caption block
            // (owner, 2026-08-28). The outer band is handed back when the block ends.
            object outer = builder.Region;
            bool naming = !string.IsNullOrEmpty(Captions.Text(widget));
            if (naming)
            {
                builder.SetRegion(key);
            }

            bool named = Captions.Push(builder, widget, key);
            if (shown.Count > 0)
            {
                Cells.EmitLinear(builder, shown);
            }
            else if (named && counted)
            {
                Total(builder, widget, key);
            }

            Captions.Pop(builder, named);
            if (naming)
            {
                builder.SetRegion(outer);
            }
        }

        /// <summary>
        /// The one row of a block the game filled with nothing - "Improvements Destroyed: 0" naming
        /// the block and this row standing in it, so the region keeps one shape however many rows the
        /// battle produced.
        ///
        /// The row answers the caption's question - what was destroyed - with "None": the caption is
        /// spoken as this first row's opener and already carries the figure, so a row repeating the
        /// figure read the 0 twice (owner, 2026-08-28). Whether there is a figure behind the caption at
        /// all is the caller's question (<see cref="Counted"/>), because a block the game filled is
        /// rows of its own.
        /// </summary>
        private static void Total(GraphBuilder builder, AgeTransform caption, string key)
        {
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ModStrings.Get(ModStrings.None)),
                },
            };
            builder.AddItem(Nodes.Drawn(ControlId.For(caption, key + "/count"), vtable, caption));
        }

        /// <summary>
        /// Whether the game has a COUNT behind these two captions at all - which is what decides
        /// whether an empty block gets its one row.
        ///
        /// Asked of the report, which is where the counts come from: the window writes both captions
        /// out of it on every refresh, each a localized template taking the count as its only argument
        /// (<c>RefreshReportPanel</c> :304/:316). It used to be asked of the DRAWN caption instead, by
        /// scanning it for its last run of digits - which is a guess about how a translator wrote the
        /// sentence, and answers no for any language that spells a number out.
        /// </summary>
        private static bool Counted(GroundBattle battle)
        {
            try
            {
                GroundBattleBattleReport report = battle == null ? null : battle.BattleReport;
                return report != null && report.IsValid;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Who the invasion killed, a cell per species, in the order the popup laid them
        /// out.</summary>
        private static List<Cell> Casualties(AgeTransform table, string prefix)
        {
            List<Cell> cells = new List<Cell>();
            // Flow control: the rows under a table the popup switched off are each still marked
            // visible, and reading every one of them to find out costs a component scrape apiece.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return cells;
            }

            try
            {
                List<AgeTransform> children = table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    Casualty(cells, children[i], prefix + "/" + i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the destroyed populations threw: " + e);
            }

            return cells;
        }

        /// <summary>
        /// One species the fighting killed: the game's own name for them and how many of them died.
        ///
        /// The cell writes the name NOWHERE - it draws the species' symbol over a bare figure, and the
        /// game keeps the name on the wrapper behind the symbol's tooltip
        /// (<c>PopulationCount.Bind</c>, which hangs the population definition there) - so the row read
        /// as a lone "1" for as long as it was read off the drawn text. The definition's own title is
        /// what every other reading of this cell uses (<see cref="People"/>).
        ///
        /// Two tooltips, one row, and they reach the player two different ways. The FIGURE's is plain
        /// text about what the count counts: it is the last one drawn, so it is the one a hover raises,
        /// so the row keeps it and announces it whole. The SYMBOL's is the species dossier the renderer
        /// assembles, and a dossier the pointer never visits has no words at all - declared on the row
        /// it was a reviewed section that could never fill (measured 2026-08-28, empty on every read).
        /// So it becomes a child entry of the row instead (<see cref="TooltipChildren.Split"/>), named
        /// off the wrapper's own title and aimed at the symbol, and stepping onto it makes the game
        /// draw the species card the row was only ever promising.
        /// </summary>
        private static void Casualty(List<Cell> cells, AgeTransform widget, string key)
        {
            PopulationCount unit =
                widget == null ? null : widget.GetComponent<PopulationCount>();
            if (unit == null)
            {
                return;
            }

            PopulationCount it = unit;
            List<AgeTooltip> tooltips = new List<AgeTooltip>(2);
            AgeWidgets.Tooltips(widget, tooltips);
            TooltipChildren.Carried carried = TooltipChildren.Split(tooltips);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(it.Tooltip)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.Count), false),
                },
            };
            // Read rather than worked: the report's cells carry the game's own click handler but the
            // popup leaves the button switched off (measured 2026-08-28: interactable false), so there
            // is nothing here to do and the row is what it looks like - a readout.
            vtable.Sections = GraphNodes.SectionsFor(vtable, carried.Own);
            Cell cell = Cells.Add(cells, widget, ControlId.For(unit, key), vtable);
            cell.Dossiers = carried.Children;
            cell.Key = carried.Children == null ? null : key;
        }

        /// <summary>What the invasion levelled, a row per improvement.</summary>
        private static List<Cell> Wrecked(AgeTransform table, string prefix)
        {
            List<Cell> cells = new List<Cell>();
            // Flow control: same as the population table above - a table the popup switched off is not
            // walked row by row, and each row costs a component scrape to find out it is not there.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return cells;
            }

            try
            {
                List<AgeTransform> children = table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    Ruin(cells, children[i], prefix + "/" + i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the destroyed improvements threw: " + e);
            }

            return cells;
        }

        /// <summary>
        /// One improvement the fighting destroyed, named from the game's own wrapper for it.
        ///
        /// This row DOES draw its name, so the name is not the defect the population row's was - but
        /// the row's own tooltip holds the constructible the game built the row out of
        /// (<c>DestroyedImprovementItem.Bind</c>), and the title on that is the same words without the
        /// wrapping and hyphenation a narrow row does to them. The drawn text is the fallback for a
        /// build where that wrapper is not a readable one.
        ///
        /// UNVERIFIED against a live row: the fixture this was written on destroyed no improvements
        /// (the table had no children at all), so only the population half below it was measured.
        /// </summary>
        private static void Ruin(List<Cell> cells, AgeTransform widget, string key)
        {
            DestroyedImprovementItem item =
                widget == null ? null : widget.GetComponent<DestroyedImprovementItem>();
            if (item == null)
            {
                return;
            }

            AgeTransform at = widget;
            DestroyedImprovementItem it = item;
            List<AgeTooltip> tooltips = new List<AgeTooltip>(2);
            AgeWidgets.Tooltips(widget, tooltips);
            if (string.IsNullOrEmpty(RuinName(at, it)))
            {
                return;
            }

            // Split for the same reason the population row is (<see cref="Casualty"/>): a dossier this
            // row is not pointing at can never draw, so it becomes a child entry rather than a promise.
            // A row carrying only its own tooltip - which is every row this prefab has been seen to
            // build - keeps it and comes out of here unchanged.
            TooltipChildren.Carried carried = TooltipChildren.Split(tooltips);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => RuinName(at, it)),
                },
            };
            vtable.Sections = GraphNodes.SectionsFor(vtable, carried.Own);
            Cell cell = Cells.Add(cells, widget, ControlId.For(item, key), vtable);
            cell.Dossiers = carried.Children;
            cell.Key = carried.Children == null ? null : key;
        }

        private static string RuinName(AgeTransform widget, DestroyedImprovementItem item)
        {
            string named = AgeWidgets.TooltipTitle(AgeWidgets.Raw(widget));
            return string.IsNullOrEmpty(named) ? AgeText.Label(item.ImprovementTitle) : named;
        }

        /// <summary>Every line of a table the popup fills by cloning one - what it destroyed, what it
        /// looted - as the row each of them looks like.</summary>
        private static void Lines(GraphBuilder builder, AgeTransform table, string prefix)
        {
            // Flow control: the rows under a table the popup switched off are each still marked
            // visible, and reading every one of them to find out costs a text walk apiece.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            try
            {
                List<AgeTransform> children = table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform line = children[i];
                    if (line == null)
                    {
                        continue;
                    }

                    string text = AgeWidgets.TextOf(line);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    AgeTransform it = line;
                    AgeTooltip tooltip = AgeWidgets.Raw(line);
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => AgeWidgets.TextOf(it)),
                        },
                        Sections = GraphNodes.Sections(null, tooltip),
                    };
                    AgeWidgets.PointAt(vtable, line);
                    builder.AddItem(Nodes.Drawn(ControlId.For(line, prefix + "/" + i), vtable, line));
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading a report table threw: " + e);
            }
        }
    }
}

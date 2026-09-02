using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The phases, as the flat run of sentences they are, and the rosters read out
    /// underneath them.</summary>
    public sealed partial class AdvancedBattleReportScreen
    {
        /// <summary>
        /// The phases, as the flat run of sentences they are - flotilla-major, so that reading down is
        /// one flotilla through the battle.
        ///
        /// The game lays this out the other way round, a panel per phase each holding one item per
        /// flotilla, because that is how it draws it; the items are matched into flotillas by the
        /// position the game gave each inside its phase, which is the same flotilla order in every phase
        /// (<c>AdvancedReportPhaseItem.FilterFlotillas</c> walks one list). A phase the battle never
        /// reached draws no item, so that flotilla simply has one line fewer.
        ///
        /// A flotilla is NAMED at the head of its run only where more than one fought: the game writes
        /// the flotilla into every one of these sentences, so with a single flotilla the name would be a
        /// line saying what the next line says anyway.
        /// </summary>
        private static void Phases(
            GraphBuilder builder,
            AdvancedEncounterReportModalWindow window
        )
        {
            AgeTransform container = window.AdvancedReportPhaseItemContainer;
            // Flow control: everything below is a component scrape and a COUNT of flotillas, both of
            // which run before any node exists for the gate to see.
            if (container == null || !AgeWidgets.Visible(container))
            {
                return;
            }

            AdvancedReportPhaseItem[] items = container.GetComponentsInChildren<AdvancedReportPhaseItem>(
                true
            );
            // The flotillas, in the order every phase drew them.
            List<AdvancedReportPhaseFlotillaStatItem[]> phases =
                new List<AdvancedReportPhaseFlotillaStatItem[]>();
            int rows = 0;
            for (int i = 0; i < items.Length; i++)
            {
                AdvancedReportPhaseItem phase = items[i];
                // Which phases contribute a line at all, counted before any is declared.
                if (phase == null || !AgeWidgets.Visible(phase.AgeTransform))
                {
                    continue;
                }

                AdvancedReportPhaseFlotillaStatItem[] stats = Stats(phase);
                phases.Add(stats);
                if (stats.Length > rows)
                {
                    rows = stats.Length;
                }
            }

            // How many flotillas fought - the count that decides whether the runs are named.
            int fought = 0;
            for (int row = 0; row < rows; row++)
            {
                if (FirstStat(phases, row) != null)
                {
                    fought++;
                }
            }

            for (int row = 0; row < rows; row++)
            {
                AdvancedReportPhaseFlotillaStatItem first = FirstStat(phases, row);
                if (first == null)
                {
                    continue;
                }

                if (fought > 1)
                {
                    builder.AddItem(Nodes.Synthetic(
                        ControlId.Structural("battle-advanced/flotilla/" + row),
                        Flotilla(first, row)
                    ));
                }

                for (int column = 0; column < phases.Count; column++)
                {
                    AdvancedReportPhaseFlotillaStatItem stat =
                        row < phases[column].Length ? phases[column][row] : null;
                    // A phase this flotilla was not in: the game draws no item for it, and there is
                    // no sentence to read.
                    if (stat == null || !AgeWidgets.Visible(stat.AgeTransform))
                    {
                        continue;
                    }

                    builder.AddItem(Nodes.Drawn(
                        ControlId.For(stat, "battle-advanced/phase/" + row + "/" + column),
                        Stat(stat),
                        stat
                    ));
                }
            }
        }

        private static AdvancedReportPhaseFlotillaStatItem[] Stats(AdvancedReportPhaseItem phase)
        {
            try
            {
                AgeTransform container = phase.FlotillaStatItemContainer;
                // Flow control: a phase panel the battle never reached is not scraped for its items.
                return container == null || !AgeWidgets.Visible(container)
                    ? new AdvancedReportPhaseFlotillaStatItem[0]
                    : container.GetComponentsInChildren<AdvancedReportPhaseFlotillaStatItem>(true);
            }
            catch (Exception)
            {
                return new AdvancedReportPhaseFlotillaStatItem[0];
            }
        }

        /// <summary>The first phase this flotilla was DRAWN in - what says the flotilla fought at all.
        /// The container pools its items, so a flotilla the battle never fielded still has an item in
        /// every phase: hidden, unbound, and with an empty sentence on it.</summary>
        private static AdvancedReportPhaseFlotillaStatItem FirstStat(
            List<AdvancedReportPhaseFlotillaStatItem[]> cells,
            int row
        )
        {
            for (int i = 0; i < cells.Count; i++)
            {
                AdvancedReportPhaseFlotillaStatItem stat =
                    row < cells[i].Length ? cells[i][row] : null;
                // Spoken count: what this answers is how many flotillas FOUGHT, which decides whether
                // the runs are named at all - a count taken before any node exists for the gate to see.
                if (stat != null && AgeWidgets.Visible(stat.AgeTransform))
                {
                    return stat;
                }
            }

            return null;
        }

        /// <summary>Which flotilla a run of phase lines belongs to, in the game's own numbering - the
        /// panel draws the number on the arena cards beside it rather than on the lines.</summary>
        private static NodeVtable Flotilla(AdvancedReportPhaseFlotillaStatItem stat, int row)
        {
            int number = stat == null ? row + 1 : stat.VisualFlotillaIndex;
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () => BattleRosters.FlotillaName(number)
                    ),
                },
                OnFocusVisual = AgeWidgets.ReleasePointer,
            };
        }

        /// <summary>What the game wrote for this flotilla in this phase, kept whole and kept in the
        /// LINES it was written in, with the pointer parked on the item so the box it came from is on
        /// screen too.</summary>
        private static NodeVtable Stat(AdvancedReportPhaseFlotillaStatItem stat)
        {
            AgeTransform widget = stat.AgeTransform;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>(),
                Sections = null,
            };
            // LABEL FALLBACK: the stat item draws a range icon and two arcs and no words at all, so the
            // sentence the game wrote for it is the only thing that could name this row - the same rung
            // the ordinary naming ladder would have reached. Its lines are the row's own parts, so the
            // door has nothing left to announce twice.
            Prose(vtable, () => AgeText.Lines(AgeText.Tooltip(tooltip)), false, null);
            AgeWidgets.PointAt(vtable, widget);
            return vtable;
        }

        /// <summary>
        /// A block of the game's own prose as a control's readout: one announcement PART per line the
        /// game DREW, so the landing stays one utterance and the review buffer steps line by line.
        ///
        /// The breaks in these are the game's own punctuation, not a box running out of width. The
        /// phase sentence is written as a statement and then its damage tally
        /// (<c>AdvancedReportPhaseFlotillaStatItem.Refresh</c> :62), with each cloaking addendum
        /// appended as a paragraph of its own (:64-78); a damage bar's title is a caption and then its
        /// critical-hit clause. Joined into one part they became one unsteppable buffer line with a
        /// newline inside it, which is neither what the box draws nor something the buffer can walk.
        ///
        /// The COUNT is read as the node is built - this screen rebuilds every frame, so it follows the
        /// text - while each part re-reads its own line as it is spoken, like every other part here.
        /// <paramref name="fallback"/> is for a block the game has not written yet, where the control
        /// still has a name of its own to fall back on.
        /// </summary>
        private static void Prose(
            NodeVtable vtable,
            Func<IList<string>> lines,
            bool named,
            Func<string> fallback
        )
        {
            int count = Read(lines).Count;
            if (count == 0)
            {
                vtable.Announcements.Add(
                    named || fallback != null
                        ? GraphNodes.LabelPart(fallback ?? (() => null))
                        : GraphNodes.ValuePart(fallback ?? (() => null), false)
                );
                return;
            }

            for (int i = 0; i < count; i++)
            {
                int at = i;
                Func<string> line = () => Line(lines, at);
                vtable.Announcements.Add(
                    named && at == 0
                        ? GraphNodes.LabelPart(line)
                        : GraphNodes.ValuePart(line, false)
                );
            }
        }

        private static IList<string> Read(Func<IList<string>> lines)
        {
            try
            {
                IList<string> read = lines == null ? null : lines();
                return read ?? new List<string>();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        /// <summary>One line of a block as it stands NOW - a line the game has since dropped answers
        /// nothing rather than the wrong line.</summary>
        private static string Line(Func<IList<string>> lines, int index)
        {
            IList<string> read = Read(lines);
            return index < read.Count ? read[index] : null;
        }

        /// <summary>The rosters the fleet toggles slide over the phase panel, while one is up. Which
        /// side's is showing is the game's decision and is read off what is drawn. Only the player's
        /// panel draws flotilla lines, so only it is handed the arena cards
        /// (<see cref="FlotillaCards"/>) - the enemy's is a garrison, and its card is read in the
        /// heading instead (<see cref="Flotillas"/>).</summary>
        private static void Rosters(
            GraphBuilder builder,
            AdvancedEncounterReportModalWindow window
        )
        {
            Roster(
                builder,
                YoursRegion,
                ModStrings.BattleYourFleets,
                AgeWidgets.Transform(window.PlayerBattleGroupReportPanel),
                "battle-advanced/yours",
                FlotillaCards(window),
                Rewarded(window)
            );
            Roster(
                builder,
                TheirsRegion,
                ModStrings.BattleEnemyFleets,
                AgeWidgets.Transform(window.EnemyBattleGroupReportPanel),
                "battle-advanced/theirs",
                null,
                null
            );
            builder.SetRegion(null);
        }

        /// <summary>
        /// The player's roster panel as the kind that carries what the battle PAID.
        ///
        /// The window's field is typed as the base panel
        /// (<c>AdvancedEncounterReportModalWindow</c> :21), but the instance it binds is the player
        /// subclass, which is the only one with a rewards table - the experience gained, the resources
        /// earned and the salvage rescued, three labels the base panel has nowhere for (verified live
        /// 2026-08-30). The enemy's panel is the enemy subclass and has none of them, which is why only
        /// this side is asked.
        /// </summary>
        private static PlayerBattleGroupReportPanel Rewarded(
            AdvancedEncounterReportModalWindow window
        )
        {
            try
            {
                return window.PlayerBattleGroupReportPanel as PlayerBattleGroupReportPanel;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Roster(
            GraphBuilder builder,
            object region,
            string nameKey,
            AgeTransform panel,
            string prefix,
            BattleRosters.FlotillaExtras extras,
            PlayerBattleGroupReportPanel rewards
        )
        {
            // Flow control: the roster under a panel the report is not drawing is a walk of its own,
            // and a region and a context would be opened around nothing.
            if (panel == null || !AgeWidgets.Visible(panel))
            {
                return;
            }

            builder.SetRegion(region);
            bool named = BattleRows.Context(builder, nameKey);

            BattleRosters.Roster(builder, panel, prefix, extras);
            BattleNotifications.Rewards(builder, rewards, prefix);
            BattleRows.Close(builder, named);
        }

    }
}

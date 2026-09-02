using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The two damage panels, a side each: one row per bar the gauge is showing, and the
    /// total underneath.</summary>
    public sealed partial class AdvancedBattleReportScreen
    {
        /// <summary>
        /// The two damage panels, a side each: one row per bar the gauge is showing, and the total
        /// underneath.
        ///
        /// A bar the game is not showing - a weapon type that never fired, missed shots while the toggle
        /// is off - is a cell of zero height the game hides, and there is nothing to say about it.
        /// </summary>
        private static void Damage(GraphBuilder builder, AdvancedEncounterReportModalWindow window)
        {
            Gauge(
                builder,
                YoursRegion,
                ModStrings.BattleYourDamage,
                window.PlayerDamageGauge,
                window.PlayerEncounterGroup,
                window.PlayerTotalDamageLabel,
                window.PlayerTotalDamageTooltip,
                "battle-advanced/your-damage"
            );
            Gauge(
                builder,
                TheirsRegion,
                ModStrings.BattleEnemyDamage,
                window.EnemyDamageGauge,
                window.EnemyEncounterGroup,
                window.EnemyTotalDamageLabel,
                window.EnemyTotalDamageTooltip,
                "battle-advanced/their-damage"
            );
            builder.SetRegion(null);
        }

        private static void Gauge(
            GraphBuilder builder,
            object region,
            string nameKey,
            DamageGauge gauge,
            EncounterGroup group,
            AgePrimitiveLabel total,
            AgeTooltip totalTooltip,
            string prefix
        )
        {
            // Flow control: same - the four readings below each walk something, and a region and a
            // context would be opened around nothing.
            if (gauge == null || !AgeWidgets.Visible(gauge.AgeTransform))
            {
                return;
            }

            builder.SetRegion(region);
            bool named = BattleRows.Context(builder, nameKey);

            Bars(builder, gauge.EffectiveDamageCells, prefix + "/effective");
            Bars(builder, gauge.AbsorbedDamageCells, prefix + "/absorbed");
            Missed(builder, gauge.MissedDamageGroup, group, prefix + "/missed");
            Total(builder, total, totalTooltip, prefix + "/total");
            BattleRows.Close(builder, named);
        }

        private static void Bars(GraphBuilder builder, AgeTransform table, string prefix)
        {
            // Flow control: the cells are found by a component scrape, not worth running for a gauge
            // the report is not drawing.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            DamageGaugeCell[] cells = table.GetComponentsInChildren<DamageGaugeCell>(true);
            for (int i = 0; i < cells.Length; i++)
            {
                DamageGaugeCell cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                DamageGaugeCell it = cell;
                builder.AddItem(Nodes.Drawn(
                    ControlId.For(cell, prefix + "/" + i),
                    Bar(
                        cell.AgeTransform,
                        () => AgeText.Lines(Title(it.DamageData)),
                        () => Description(it.DamageData)
                    ),
                    cell
                ));
            }
        }

        /// <summary>
        /// The shots that missed, which the game hangs on the band itself rather than on a cell of its
        /// own - the same wrapper underneath, so the same sentence - plus the one thing the BAND says
        /// that the sentence does not.
        ///
        /// The game writes the count ("Missed Shots: 2") and draws the PROPORTION: the band's height is
        /// the whole gauge's times <c>1 - hitRatio</c> (<c>DamageGauge.RefreshMissedDamage</c>
        /// :229-243), so a sighted player reads "about a fifth of the shooting missed" off a picture
        /// with no number on it, and a listener told "2" cannot recover it. So the row says the share,
        /// as a percentage, and the totals stay where the game put them, which is nowhere - the same
        /// call the balance of power makes (<see cref="BattleNotifications.BalanceText"/>).
        ///
        /// It is said only while the band is DRAWN, which is what the Show Missed Shots switch governs:
        /// the game hides the group when the switch is off or nothing missed, and the gate drops this
        /// node with it.
        /// </summary>
        private static void Missed(
            GraphBuilder builder,
            AgeTransform group,
            EncounterGroup side,
            string key
        )
        {
            if (group == null)
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(group);
            EncounterGroup it = side;
            NodeVtable vtable = Bar(
                group,
                () => AgeText.Lines(Title(Data(tooltip))),
                () => Description(Data(tooltip))
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => MissedShare(it), false));
            builder.AddItem(Nodes.Drawn(ControlId.For(group, key), vtable, group));
        }

        /// <summary>
        /// What share of this side's shooting missed, in the phrase the mod has for it.
        ///
        /// Computed from the same two properties the gauge sizes its band from and summed the same way
        /// (<c>DamageGauge.Refresh</c> :80-86, :106-108): every flotilla of the side, plus its
        /// citadels, which the game counts in and a flotilla walk would leave out. The gauge keeps the
        /// answer in a private field, so this re-derives it rather than reading it - and re-derives it
        /// off the game's own inputs rather than off the band's pixel height, which is scaled by a
        /// second ratio the band shares with the rest of the gauge.
        /// </summary>
        private static string MissedShare(EncounterGroup group)
        {
            try
            {
                if (group == null)
                {
                    return null;
                }

                float shots = DamageGauge.GetFlotillasPropertyValue(
                    group,
                    SimulationProperties.Flotilla.TotalShotSent
                );
                float hits = DamageGauge.GetFlotillasPropertyValue(
                    group,
                    SimulationProperties.Flotilla.TotalHitSent
                );
                for (int i = 0; group.Citadels != null && i < group.Citadels.Count; i++)
                {
                    EncounterCitadel citadel = group.Citadels[i];
                    if (citadel == null)
                    {
                        continue;
                    }

                    shots += citadel.GetPropertyValue(SimulationProperties.Flotilla.TotalShotSent);
                    hits += citadel.GetPropertyValue(SimulationProperties.Flotilla.TotalHitSent);
                }

                if (shots <= 0f)
                {
                    return null;
                }

                return OptionalText.Phrase(
                    ModStrings.BattleShotsMissed,
                    UnityEngine.Mathf.RoundToInt(
                        UnityEngine.Mathf.Clamp01(1f - hits / shots) * 100f
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("battle report: reading the missed-shot share threw: " + e);
                return null;
            }
        }

        private static void Total(
            GraphBuilder builder,
            AgePrimitiveLabel total,
            AgeTooltip tooltip,
            string key
        )
        {
            AgeTransform widget = total == null ? null : total.AgeTransform;
            if (widget == null)
            {
                return;
            }

            AgePrimitiveLabel it = total;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>(),
                Sections = null,
            };
            // LABEL FALLBACK: the label draws a bare figure and an icon ("2074 [damageApplied]"), and
            // the game's name for what it counts is written only in the tooltip - so the tooltip's
            // sentence names the row, with the drawn figure as the fallback where it is not written.
            Prose(
                vtable,
                () => AgeText.Lines(AgeText.Tooltip(tooltip)),
                true,
                () => AgeText.Label(it)
            );
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(total, key), vtable, total));
        }

        /// <summary>One bar of a damage gauge: the game's own caption for it - which is a caption and,
        /// where the bar has one, a clause of its own on a second line, so it is read a part per line
        /// (<see cref="Prose"/>) - and the tactical advice it hangs beside it kept for the review buffer,
        /// since the advice is the same paragraph every battle and a player comparing eight bars does
        /// not want to hear it eight times.</summary>
        private static NodeVtable Bar(
            AgeTransform widget,
            Func<IList<string>> title,
            Func<IList<string>> advice
        )
        {
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>(),
                Sections = GraphNodes.Sections(advice, null),
            };
            Prose(vtable, title, true, null);
            AgeWidgets.PointAt(vtable, widget);
            return vtable;
        }

        private static GuiDamageData Data(AgeTooltip tooltip)
        {
            try
            {
                return tooltip == null ? null : tooltip.Target as GuiDamageData;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Title(GuiDamageData data)
        {
            try
            {
                return data == null ? null : AgeText.Clean(data.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Everything the bar's own box says under its caption: the game's tactical advice, and - where
        /// one of the two plans moved this number - the game's sentence saying which.
        ///
        /// The second one is a whole feature of the tooltip the mod would otherwise drop.
        /// <c>GuiDamageData</c> is an <c>IAffectingPlaysProvider</c> and computes the plays that touched
        /// its own properties (:99-152); the <c>DamageGaugeCell</c> tooltip class lists
        /// <c>PanelFeatureAffectedByPlay</c> among its four features (measured off the live panel
        /// definition, 2026-08-30), which renders that list as a sentence and hides itself when the list
        /// is empty. Reading only Title and Description therefore lost it on every battle where a plan
        /// modified damage. The words are the game's own two keys, and which of them applies is the
        /// game's own rule (one play named, or the first two of several).
        /// </summary>
        private static IList<string> Description(GuiDamageData data)
        {
            try
            {
                if (data == null)
                {
                    return null;
                }

                IList<string> lines = AgeText.Lines(AgeText.Clean(data.Description));
                string plays = Plays(data);
                if (!string.IsNullOrEmpty(plays))
                {
                    lines.Add(plays);
                }

                return lines;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Plays(GuiDamageData data)
        {
            List<string> names = data.AffectingPlayNames;
            if (names == null || names.Count == 0)
            {
                return null;
            }

            return AgeText.Clean(
                names.Count == 1
                    ? Gui.Localize(OnePlayKey, Gui.GetLocalizedTitle(names[0]))
                    : Gui.Localize(
                        TwoPlaysKey,
                        Gui.GetLocalizedTitle(names[0]),
                        Gui.GetLocalizedTitle(names[1])
                    )
            );
        }

        /// <summary>The game's own two sentences for "a tactic moved this number", and the same choice
        /// between them the feature makes.</summary>
        private const string OnePlayKey = "%PanelFeatureAffectedByOnePlayDescription";
        private const string TwoPlaysKey = "%PanelFeatureAffectedByTwoPlaysDescription";

    }
}

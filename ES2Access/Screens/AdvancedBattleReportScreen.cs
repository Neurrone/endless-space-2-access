using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The advanced battle report - how the fight actually went, phase by phase and weapon by weapon.
    ///
    /// The report popup says who won; this window says WHY, and everything it says is drawn as a picture.
    /// The phase panel is a grid of range icons and paired arcs, the damage panels are stacked coloured
    /// bars, and the numbers behind all of it live only in the sentences the game writes onto their
    /// tooltips. So this screen is those sentences, arranged the way the pictures are.
    ///
    /// The phase panel is a TABLE and reads as one (<see cref="GraphSheet"/>): a row per flotilla, a
    /// column per phase, and in each cell the whole sentence the game wrote for that flotilla in that
    /// phase - "The Flotillas 2 were at Long range during phase 1, Damage repartition: 340 vs 120" - plus
    /// whichever of the three cloaking addenda applies. The sentence is kept WHOLE rather than split into
    /// the numbers inside it: the game already wrote the reading, and a cell that said "Long, 340, 120"
    /// would be the mod paraphrasing a sentence it did not need to. It costs some redundancy against the
    /// column header the player crossed to get there, which is the cheaper of the two mistakes. A phase
    /// the battle never reached is a cell the game disables and empties, and it reads as the blank it is.
    ///
    /// The damage panels are the same idea a second time: a row per bar the gauge is showing, each the
    /// game's own sentence for that bar ("Damage caused by your Beam weapons: 340"), with the tactical
    /// advice the game hangs beside it kept for the review buffer rather than spoken - it is the same
    /// paragraph every battle. Absorbed damage, the missed shots the toggle folds in and the totals at the
    /// foot are all bars of the same kind and all read the same way.
    ///
    /// The two fleet toggles do not open anything of the mod's: the game slides a roster panel over the
    /// grid, so what this screen declares follows what is DRAWN - grid while the grid is up, rosters while
    /// a roster is - and the toggles themselves are the only thing that has to be declared for the
    /// keyboard.
    ///
    /// Escape is the game's, and it is not a plain close: the window's own <c>HandleInput</c> puts the
    /// report popup back up, which is where the player came from.
    /// </summary>
    public sealed class AdvancedBattleReportScreen : Screen
    {
        private static readonly object HeadingStop = "battle-advanced:heading";
        private static readonly object PhasesStop = "battle-advanced:phases";
        private static readonly object DamageStop = "battle-advanced:damage";
        private static readonly object ControlsStop = "battle-advanced:controls";

        private static readonly object YoursRegion = "battle-advanced:yours";
        private static readonly object TheirsRegion = "battle-advanced:theirs";

        /// <summary>The game's own titles for the things it draws as pictures.</summary>
        private const string CommandPointsTitleKey = "%ShipStatCommandPointsTitle";
        private const string MissedDamageTitleKey =
            "%AdvancedReportModalWindowShowMissedDamageTitle";
        private const string FlotillaNameKey = "%FlotillaNameTitle";

        /// <summary>The mod's own, for the two things the game names nowhere: which side a damage panel
        /// belongs to, and the two switches that swap the grid for a roster.</summary>
        private const string YourDamageKey = "battle.your-damage";
        private const string EnemyDamageKey = "battle.enemy-damage";
        private const string YourFleetsKey = "battle.your-fleets";
        private const string EnemyFleetsKey = "battle.enemy-fleets";
        private const string ShowYourFleetsKey = "battle.show-your-fleets";
        private const string ShowEnemyFleetsKey = "battle.show-enemy-fleets";
        private const string ScreenNameKey = "screen.battle-report-advanced";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.battle-report-advanced"; }
        }

        /// <summary>Over the notification popup it is opened from and returns to, and under the
        /// confirmation box that can be raised over anything.</summary>
        public override int Layer
        {
            get { return 42; }
        }

        /// <summary>The game's own word for how the battle went, which is what the window writes across
        /// its top.</summary>
        public override string ScreenName
        {
            get
            {
                try
                {
                    AdvancedEncounterReportModalWindow window = Window();
                    string title = window == null ? null : AgeText.Label(window.BattleTitle);
                    return string.IsNullOrEmpty(title)
                        ? BattleText.Optional(ScreenNameKey)
                        : title;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public override bool IsActive()
        {
            try
            {
                AdvancedEncounterReportModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: the window answers Exit by putting the battle report back up, which is
        /// somewhere to go rather than nowhere.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            AdvancedEncounterReportModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                builder.BeginStop(HeadingStop);
                Heading(builder, window);

                builder.BeginStop(PhasesStop);
                Phases(builder, window);
                Rosters(builder, window);

                builder.BeginStop(DamageStop);
                Damage(builder, window);

                builder.BeginStop(ControlsStop);
                Controls(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("battle report: reading the advanced report threw: " + e);
            }
        }

        /// <summary>Who fought, how it ended, and what each side spent: the command-point line is the
        /// game's own "before, then after" sentence, which is the only place the losses in fleet capacity
        /// are written down.</summary>
        private static void Heading(
            GraphBuilder builder,
            AdvancedEncounterReportModalWindow window
        )
        {
            Note(builder, window.BattleTitle, "battle-advanced/outcome");

            builder.SetRegion(YoursRegion);
            Leader(builder, window.PlayerBattleGroupInfoPanel, "battle-advanced/yours");
            Value(builder, window.PlayerCPLabel, CommandPointsTitleKey, "battle-advanced/your-cp");

            builder.SetRegion(TheirsRegion);
            Leader(builder, window.EnemyBattleGroupInfoPanel, "battle-advanced/theirs");
            Value(builder, window.EnemyCPLabel, CommandPointsTitleKey, "battle-advanced/their-cp");
            builder.SetRegion(null);
        }

        /// <summary>
        /// The phase grid: a row per flotilla, a column per phase.
        ///
        /// The game lays this out the other way round - a panel per phase, each holding one item per
        /// flotilla - because that is how it draws it. Read as a table it has to be flotilla-major, so
        /// that walking down compares the same flotilla through the battle and walking across compares
        /// the phases; the columns are matched by the position the game gave each item inside its phase,
        /// which is the same flotilla order in every phase (<c>AdvancedReportPhaseItem.FilterFlotillas</c>
        /// walks one list).
        /// </summary>
        private static void Phases(
            GraphBuilder builder,
            AdvancedEncounterReportModalWindow window
        )
        {
            AgeTransform container = window.AdvancedReportPhaseItemContainer;
            if (container == null || !AgeWidgets.Visible(container))
            {
                return;
            }

            List<AdvancedReportPhaseItem> phases = new List<AdvancedReportPhaseItem>();
            List<string> columns = new List<string>();
            List<AgeTransform> widgets = new List<AgeTransform>();
            AdvancedReportPhaseItem[] items = container.GetComponentsInChildren<AdvancedReportPhaseItem>(
                true
            );
            for (int i = 0; i < items.Length; i++)
            {
                AdvancedReportPhaseItem phase = items[i];
                if (phase == null || !AgeWidgets.Visible(phase.AgeTransform))
                {
                    continue;
                }

                phases.Add(phase);
                widgets.Add(phase.AgeTransform);
                columns.Add(AgeText.Label(phase.PhaseNameLabel));
            }

            if (phases.Count == 0)
            {
                return;
            }

            // The flotillas, in the order the first phase drew them - every phase draws the same set.
            List<AdvancedReportPhaseFlotillaStatItem[]> cells =
                new List<AdvancedReportPhaseFlotillaStatItem[]>();
            int rows = 0;
            for (int i = 0; i < phases.Count; i++)
            {
                AdvancedReportPhaseFlotillaStatItem[] stats = Stats(phases[i]);
                cells.Add(stats);
                if (stats.Length > rows)
                {
                    rows = stats.Length;
                }
            }

            if (rows == 0)
            {
                return;
            }

            // The flotilla column has no caption to speak: the game draws no heading over this grid at
            // all - the phase names come off the phase panels themselves - so column 0 leads the header
            // list as a null and its crossing stays label-free. Nothing is invented for it.
            columns.Insert(0, null);

            GraphSheet sheet = new GraphSheet(builder, "battle-advanced:phases:");
            sheet.Region(null, columns.ToArray());
            for (int row = 0; row < rows; row++)
            {
                object rowRef = "battle-advanced:flotilla:" + row;
                int index = row;
                List<KeyValuePair<int, NodeVtable>> line =
                    new List<KeyValuePair<int, NodeVtable>>();
                for (int column = 0; column < cells.Count; column++)
                {
                    AdvancedReportPhaseFlotillaStatItem stat =
                        index < cells[column].Length ? cells[column][index] : null;
                    if (stat == null || !AgeWidgets.Visible(stat.AgeTransform))
                    {
                        // A phase the battle never reached: the game disables the panel and draws no
                        // items in it, and the cell reads as the blank it is.
                        continue;
                    }

                    line.Add(new KeyValuePair<int, NodeVtable>(column + 1, Stat(stat)));
                }

                sheet.RowAt(Flotilla(FirstStat(cells, index), index), rowRef, line);
            }

            sheet.Finish();
        }

        private static AdvancedReportPhaseFlotillaStatItem[] Stats(AdvancedReportPhaseItem phase)
        {
            try
            {
                AgeTransform container = phase.FlotillaStatItemContainer;
                return container == null || !AgeWidgets.Visible(container)
                    ? new AdvancedReportPhaseFlotillaStatItem[0]
                    : container.GetComponentsInChildren<AdvancedReportPhaseFlotillaStatItem>(true);
            }
            catch (Exception)
            {
                return new AdvancedReportPhaseFlotillaStatItem[0];
            }
        }

        private static AdvancedReportPhaseFlotillaStatItem FirstStat(
            List<AdvancedReportPhaseFlotillaStatItem[]> cells,
            int row
        )
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (row < cells[i].Length && cells[i][row] != null)
                {
                    return cells[i][row];
                }
            }

            return null;
        }

        /// <summary>Which flotilla a row is, in the game's own numbering - the grid draws the number on
        /// the fleet cards beside it rather than on the row.</summary>
        private static NodeVtable Flotilla(AdvancedReportPhaseFlotillaStatItem stat, int row)
        {
            int number = stat == null ? row + 1 : stat.VisualFlotillaIndex;
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () => AgeText.Clean(Gui.Localize(FlotillaNameKey, number.ToString()))
                    ),
                },
                OnFocusVisual = AgeWidgets.ReleasePointer,
            };
        }

        /// <summary>One cell of the grid: the sentence the game wrote for this flotilla in this phase,
        /// whole, with the pointer parked on the item so the tooltip it came from is on screen too.
        /// </summary>
        private static NodeVtable Stat(AdvancedReportPhaseFlotillaStatItem stat)
        {
            AgeTransform widget = stat.AgeTransform;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.ValuePart(() => Sentence(tooltip), false),
                },
                Sections = GraphNodes.Sections(null, null),
            };
            AgeWidgets.PointAt(vtable, widget);
            return vtable;
        }

        /// <summary>A tooltip the game wrote as prose, as one spoken line: its own line breaks are where
        /// the box ran out of width and where it started a new paragraph, not punctuation.</summary>
        private static string Sentence(AgeTooltip tooltip)
        {
            MessageBuilder message = new MessageBuilder();
            foreach (string line in AgeText.Lines(AgeText.Tooltip(tooltip)))
            {
                message.Fragment(line);
            }

            return message.Build();
        }

        /// <summary>The rosters the fleet toggles slide over the grid, while one is up. Which side's is
        /// showing is the game's decision and is read off what is drawn.</summary>
        private static void Rosters(
            GraphBuilder builder,
            AdvancedEncounterReportModalWindow window
        )
        {
            Roster(
                builder,
                YoursRegion,
                YourFleetsKey,
                Widget(window.PlayerBattleGroupReportPanel),
                "battle-advanced/yours"
            );
            Roster(
                builder,
                TheirsRegion,
                EnemyFleetsKey,
                Widget(window.EnemyBattleGroupReportPanel),
                "battle-advanced/theirs"
            );
            builder.SetRegion(null);
        }

        private static void Roster(
            GraphBuilder builder,
            object region,
            string nameKey,
            AgeTransform panel,
            string prefix
        )
        {
            if (panel == null || !AgeWidgets.Visible(panel))
            {
                return;
            }

            builder.SetRegion(region);
            string name = BattleText.Optional(nameKey);
            bool named = !string.IsNullOrEmpty(name);
            if (named)
            {
                builder.PushContext(name, null, false);
            }

            BattleRosters.Roster(builder, panel, prefix);
            if (named)
            {
                builder.PopContext();
            }
        }

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
                YourDamageKey,
                window.PlayerDamageGauge,
                window.PlayerTotalDamageLabel,
                window.PlayerTotalDamageTooltip,
                "battle-advanced/your-damage"
            );
            Gauge(
                builder,
                TheirsRegion,
                EnemyDamageKey,
                window.EnemyDamageGauge,
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
            AgePrimitiveLabel total,
            AgeTooltip totalTooltip,
            string prefix
        )
        {
            if (gauge == null || !AgeWidgets.Visible(gauge.AgeTransform))
            {
                return;
            }

            builder.SetRegion(region);
            string name = BattleText.Optional(nameKey);
            bool named = !string.IsNullOrEmpty(name);
            if (named)
            {
                builder.PushContext(name, null, false);
            }

            Bars(builder, gauge.EffectiveDamageCells, prefix + "/effective");
            Bars(builder, gauge.AbsorbedDamageCells, prefix + "/absorbed");
            Missed(builder, gauge.MissedDamageGroup, prefix + "/missed");
            Total(builder, total, totalTooltip, prefix + "/total");
            if (named)
            {
                builder.PopContext();
            }
        }

        private static void Bars(GraphBuilder builder, AgeTransform table, string prefix)
        {
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            DamageGaugeCell[] cells = table.GetComponentsInChildren<DamageGaugeCell>(true);
            for (int i = 0; i < cells.Length; i++)
            {
                DamageGaugeCell cell = cells[i];
                if (cell == null || !AgeWidgets.Visible(cell.AgeTransform))
                {
                    continue;
                }

                DamageGaugeCell it = cell;
                builder.AddItem(
                    ControlId.Referenced(cell, prefix + "/" + i),
                    Bar(
                        cell.AgeTransform,
                        () => Title(it.DamageData),
                        () => Description(it.DamageData)
                    )
                );
            }
        }

        /// <summary>The shots that missed, which the game hangs on the band itself rather than on a cell
        /// of its own - the same wrapper underneath, so the same sentence.</summary>
        private static void Missed(GraphBuilder builder, AgeTransform group, string key)
        {
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(group);
            builder.AddItem(
                ControlId.Referenced(group, key),
                Bar(group, () => Title(Data(tooltip)), () => Description(Data(tooltip)))
            );
        }

        private static void Total(
            GraphBuilder builder,
            AgePrimitiveLabel total,
            AgeTooltip tooltip,
            string key
        )
        {
            AgeTransform widget = total == null ? null : total.AgeTransform;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgePrimitiveLabel it = total;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => Sentence(tooltip) ?? AgeText.Label(it)),
                },
                Sections = GraphNodes.Sections(null, null),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(ControlId.Referenced(total, key), vtable);
        }

        /// <summary>One bar of a damage gauge: the game's own sentence for it, and the tactical advice it
        /// hangs beside it kept for the review buffer - the advice is the same paragraph every battle, and
        /// a player comparing eight bars does not want to hear it eight times.</summary>
        private static NodeVtable Bar(
            AgeTransform widget,
            Func<string> title,
            Func<IList<string>> advice
        )
        {
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(title) },
                Sections = GraphNodes.Sections(advice, null),
            };
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

        private static IList<string> Description(GuiDamageData data)
        {
            try
            {
                return data == null ? null : AgeText.Lines(AgeText.Clean(data.Description));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The three switches and the way out. The switches are the game's own boxes; Back has
        /// no field of its own on the window, so it is found where the window drew it.</summary>
        private void Controls(GraphBuilder builder, AdvancedEncounterReportModalWindow window)
        {
            _cells.Clear();
            Checkbox(_cells, window.ShowPlayerFleetsToggle, ShowYourFleetsKey, null, "battle-advanced:show-yours");
            Checkbox(_cells, window.ShowEnemyFleetsToggle, ShowEnemyFleetsKey, null, "battle-advanced:show-theirs");
            Checkbox(
                _cells,
                window.ShowMissedDamageToggle,
                null,
                MissedDamageTitleKey,
                "battle-advanced:show-missed"
            );
            Cells.AddControl(_cells, Back(window), "battle-advanced:back");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The way out, which the window keeps in no field of its own: it is found by the
        /// HANDLER it is wired to rather than by a name in the prefab, because the handler is in the
        /// window's own code and a prefab name is a guess.</summary>
        private static AgeTransform Back(AdvancedEncounterReportModalWindow window)
        {
            try
            {
                AgeControlButton[] buttons = window.AgeTransform.GetComponentsInChildren<AgeControlButton>(
                    true
                );
                for (int i = 0; i < buttons.Length; i++)
                {
                    AgeControlButton button = buttons[i];
                    if (
                        button != null
                        && button.OnActivateMethod == BackHandler
                        && AgeWidgets.Visible(button.AgeTransform)
                    )
                    {
                        return button.AgeTransform;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle report: looking for the way out threw: " + e);
            }

            return null;
        }

        private const string BackHandler = "OnBackCb";

        private static void Checkbox(
            List<Cell> cells,
            AgeControlToggle toggle,
            string modKey,
            string gameKey,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (toggle == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => Name(widget, modKey, gameKey),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Offered(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, widget);
            Cells.Add(cells, widget, ControlId.Referenced(toggle, key), vtable);
        }

        /// <summary>What a control is called: the words the game drew on it, else the game's own title
        /// for it, else the mod's - in that order, because the mod's word is the last resort and only
        /// exists for the two switches the game names nowhere at all.</summary>
        private static string Name(AgeTransform widget, string modKey, string gameKey)
        {
            string drawn = AgeWidgets.TextOf(widget);
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            string game = string.IsNullOrEmpty(gameKey) ? null : AgeText.Clean(gameKey);
            return string.IsNullOrEmpty(game) ? BattleText.Optional(modKey) : game;
        }

        private static void Note(GraphBuilder builder, AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (
                widget == null
                || !AgeWidgets.Visible(widget)
                || string.IsNullOrEmpty(AgeText.Label(label))
            )
            {
                return;
            }

            AgePrimitiveLabel it = label;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(ControlId.Referenced(label, key), vtable);
        }

        /// <summary>A figure the game wrote as its own sentence ("18 &gt;&gt; 11 CP"), under the game's
        /// name for what it counts.</summary>
        private static void Value(
            GraphBuilder builder,
            AgePrimitiveLabel label,
            string titleKey,
            string key
        )
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (
                widget == null
                || !AgeWidgets.Visible(widget)
                || string.IsNullOrEmpty(AgeText.Label(label))
            )
            {
                return;
            }

            AgePrimitiveLabel it = label;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Clean(titleKey)),
                    GraphNodes.ValuePart(() => AgeText.Label(it), false),
                },
                Sections = GraphNodes.Sections(null, AgeWidgets.Raw(widget)),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(ControlId.Referenced(label, key), vtable);
        }

        /// <summary>Who is leading this side, and the hero commanding it where there is one.</summary>
        private static void Leader(
            GraphBuilder builder,
            BattleGroupInfoPanel panel,
            string prefix
        )
        {
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            Note(builder, panel.MainLeaderName, prefix + "/leader");
            AgePrimitiveImage portrait = panel.MainHeroPortrait;
            AgeTransform widget = portrait == null ? null : portrait.AgeTransform;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(tooltip)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(ControlId.Referenced(portrait, prefix + "/hero"), vtable);
        }

        private static AgeTransform Widget(GuiPanel panel)
        {
            try
            {
                return panel == null ? null : panel.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AdvancedEncounterReportModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<AdvancedEncounterReportModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

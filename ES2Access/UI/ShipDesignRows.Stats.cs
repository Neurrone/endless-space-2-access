using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    public static partial class ShipDesignRows
    {
        // ---- the ship's statistics ----

        /// <summary>
        /// What the design comes out as: the four running totals, the two balance gauges, the range
        /// accuracies, and - behind the game's own switch - the per-weapon and per-defence breakdowns.
        ///
        /// Read from the shape of what is drawn, because that is what it is: thirty-odd numbers, each
        /// with the sentence explaining it on its own group, and not one of them a control. The two
        /// exceptions are declared for it: the switch itself is a checkbox, and a gauge carries its
        /// value in the width of a bar.
        ///
        /// The shape alone is not enough to NAME them, though: the game draws a caption beside some of
        /// these numbers (the range accuracies, the two military powers' icons, the mining rates) and
        /// none at all beside the rest, so every figure whose caption exists only in the game's string
        /// table is declared by name FIRST and its group is then skipped by the walk.
        ///
        /// FOUR REGIONS, and the shape walk's own cells are split between two of them by the band the
        /// game drew them in - anything under <c>RangeEfficienciesTable</c> is a per-range figure, the
        /// rest is the combat block. In order: the switch on its own (a region of one, so Alt+Up/Down
        /// reaches it and leaves it rather than dead-ending there); the four RUNNING TOTALS, which are
        /// what a design is judged by and are emitted in the owner's order rather than the panel's -
        /// health, manpower, movement, command points, where the panel draws offence and defence in
        /// between; the whole combat block in drawn order; and the per-range figures under the game's own
        /// word for them, "Accuracy" (<c>%AccuracyTitle</c>) - the accuracies always, and the damage
        /// figures beside them once the switch is on.
        ///
        /// The band's "Ship Statistics" caption is the stop's name and no longer a row, and the walk is
        /// told to leave it (and the switch, declared here) alone through the same skip list the named
        /// figures use.
        /// </summary>
        private static void BuildStats(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            List<Cell> cells
        )
        {
            bool labelled = false;
            try
            {
                AgeTransform table = panel.StatisticsTable;
                // Flow control: a stop and a caption context would be opened around nothing, and the
                // whole statistics band would be walked.
                if (table == null || !AgeWidgets.Visible(table))
                {
                    return;
                }

                string keys = prefix + "/stat/";
                AgeTransform caption = FirstLabel(table);
                AgeTransform ranges = panel.RangeEfficienciesTable;

                _namedStats.Clear();
                if (caption != null)
                {
                    _namedStats.Add(caption);
                }

                AgeTransform switched = AgeWidgets.Transform(panel.ShowDetailedStatsToggle);
                if (switched != null)
                {
                    _namedStats.Add(switched);
                }

                _switchCells.Clear();
                _runningCells.Clear();
                _combatCells.Clear();
                _rangeCells.Clear();
                AddDetailedSwitch(_switchCells, panel, keys);
                AddRunningStats(_runningCells, panel, keys, _namedStats);
                AddCombatStats(_combatCells, panel, keys, _namedStats);
                AddRangeStats(_rangeCells, panel, keys, _namedStats);

                cells.Clear();
                SidePanels.Content(cells, table, keys, Stats, null);
                for (int i = 0; i < cells.Count; i++)
                {
                    (AgeWidgets.Under(cells[i].Widget, ranges) ? _rangeCells : _combatCells).Add(cells[i]);
                }

                _namedStats.Clear();
                if (
                    _switchCells.Count == 0
                    && _runningCells.Count == 0
                    && _combatCells.Count == 0
                    && _rangeCells.Count == 0
                )
                {
                    return;
                }

                builder.BeginStop(StatsStop(prefix));
                labelled = Caption(builder, caption);

                builder.SetRegion(keys + "switch");
                EmitOrdered(builder, _switchCells);
                builder.SetRegion(keys + "running");
                EmitOrdered(builder, _runningCells);
                builder.SetRegion(keys + "combat");
                EmitLinear(builder, _combatCells);
                builder.SetRegion(keys + "range");
                EmitRanges(builder, _rangeCells);
            }
            catch (Exception e)
            {
                Log.Warn("ship design: reading the statistics threw: " + e);
            }
            finally
            {
                _namedStats.Clear();
                _switchCells.Clear();
                _runningCells.Clear();
                _combatCells.Clear();
                _rangeCells.Clear();
                if (labelled)
                {
                    builder.PopContext();
                }
            }
        }

        /// <summary>The per-range figures under the game's own word for what they are. Not a caption the
        /// panel draws - it draws none over this band at all - but the title the game keeps for the
        /// statistic itself, which is the same string the hidden accuracy figure is named from.</summary>
        private static void EmitRanges(GraphBuilder builder, List<Cell> cells)
        {
            string title = AgeText.Title("%AccuracyTitle");
            bool named = title != null && cells.Count > 0;
            if (named)
            {
                builder.PushContext(title);
            }

            try
            {
                EmitLinear(builder, cells);
            }
            finally
            {
                if (named)
                {
                    builder.PopContext();
                }
            }
        }

        // The statistics band's four regions, gathered before anything is declared because two of them
        // are filled by the shape walk and have to be split by the band each cell was drawn in. Reused
        // rather than allocated per frame, main-thread only, like the skip list below.
        private static readonly List<Cell> _switchCells = new List<Cell>(1);
        private static readonly List<Cell> _runningCells = new List<Cell>(4);
        private static readonly List<Cell> _combatCells = new List<Cell>(24);
        private static readonly List<Cell> _rangeCells = new List<Cell>(8);

        /// <summary>What the shape walk is to leave alone: the groups whose figures were already declared
        /// with the caption the game keeps in its string table (so the walk does not read the number on
        /// its own), plus the two widgets this band declares itself - the caption, which is the stop's
        /// name, and the detailed-stats switch, which is a region of one. Refilled per band, main-thread
        /// only.</summary>
        private static readonly List<AgeTransform> _namedStats = new List<AgeTransform>();

        /// <summary>The four running totals a design is judged by, in the order the owner asked to hear
        /// them rather than the order the panel draws them in: the panel puts the two military powers
        /// between manpower and movement, and those belong with the combat block below. Named from the
        /// game's own titles, the same six <see cref="AddSimpleStats"/> gives every other host of the
        /// prefab - two of which are declared here instead, in <see cref="AddCombatStats"/>.</summary>
        private static void AddRunningStats(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string keyPrefix,
            List<AgeTransform> declared
        )
        {
            AddStat(cells, panel.HealthLabel, "%ShipStatHealthTitle", keyPrefix + "health", declared);
            AddStat(
                cells,
                panel.ManPowerLabel,
                "%ShipStatManpowerTitle",
                keyPrefix + "manpower",
                declared
            );
            AddStat(
                cells,
                panel.MovementPointsLabel,
                "%ShipStatMovementTitle",
                keyPrefix + "movement",
                declared
            );
            AddStat(
                cells,
                panel.CommandPointsLabel,
                "%ShipStatCommandPointsTitle",
                keyPrefix + "command-points",
                declared
            );
        }

        /// <summary>
        /// The two military powers the base panel draws, plus the figures this panel adds to them, each
        /// named from the game's own title for it - the whole combat block, in the order the panel lays
        /// it out.
        ///
        /// Measured on the live prefab, with the hidden rows included: NOT ONE of these groups holds a
        /// caption label (<c>DetailedOffensiveStatsPanel</c>, <c>DetailedDefensiveStatsPanel</c> and
        /// <c>DetailedRangeDPSGroup</c> contain a single value label each and nothing else), while the
        /// range ACCURACIES above them do draw their own <c>…RangeTitle</c> and are left to the shape
        /// walk. So every row here is a bare number today - the panel writes them all with
        /// <c>FloatExtensions.ToString</c> (<c>ShipDesignEditionPanel.RefreshOffensiveAndDefensiveStats</c>
        /// :1146-1188, <c>RefreshRangeEfficiency</c> :1082-1087) and explains each with a
        /// <c>%…Description</c> SENTENCE, which is a gloss and not a name.
        ///
        /// Six of the titles are not <c>%ShipStat…</c> keys because the game does not keep one under
        /// that name: the four weapon powers are titled as module CATEGORIES
        /// (<c>%CategoryWeaponKineticTitle</c> = "Kinetic", the same registry entry the module strip's
        /// toggles are named from) and the two squadron counts as module SUB-categories
        /// (<c>%SubCategoryModuleSquadronFighterTitle</c> = "Fighter", on the GuiElements that also
        /// carry the <c>[fighter]</c>/<c>[bomber]</c> symbols -
        /// <c>Public\Gui\GuiElements[SubCategories].xml:425-441</c>); the two absorptions have a
        /// percentage-specific title (<c>%…AbsorptionPercentTitle</c>, "Hull Plating absorption",
        /// rather than the sentence-length <c>%…AbsorptionTitle</c>), and the shield capacity is
        /// titled after the property it reads (<c>%ShieldTitle</c> = "Shield power", from
        /// <c>SimulationProperties.Ship.Shield</c>).
        ///
        /// One row has no title under its own name - the plating health bonus, which the panel writes
        /// from <c>GuiShipDesign.HealthBonus</c> and explains with <c>%PlatingHealthBonusDescription</c>,
        /// "The cumulative health bonus given by modules." Unnamed it announced that sentence and then a
        /// bare number, which says what the figure means and never what it is. It is named from
        /// <c>%PanelFeatureModuleEffectsDefenseHealthTitle</c> = "Health Bonus", which is the game's own
        /// caption for the effect this row is the sum OF: a defence module's <c>SectionHealthBonus</c> is
        /// listed under exactly that title in the module tooltip every one of these modules carries
        /// (<c>Public\Gui\Miscellaneous\PanelFeatureModuleEffectsDefinition.xml</c> :98,155). The title
        /// of the property it reads (<c>SimulationProperties.Ship.ShipHealth</c> -> <c>%ShipHealthTitle</c>,
        /// via the <c>ShipHealth</c> GuiElement) is the bare word "Health", which is already the caption
        /// of the ship's own health two rows up and would put the same word on two different numbers.
        /// </summary>
        private static void AddCombatStats(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string keyPrefix,
            List<AgeTransform> declared
        )
        {
            AddStat(
                cells,
                panel.OffensivePowerLabel,
                "%ShipStatOffensiveMilitaryPowerTitle",
                keyPrefix + "offence",
                declared
            );
            AddStat(
                cells,
                panel.DefensivePowerLabel,
                "%ShipStatDefensiveMilitaryPowerTitle",
                keyPrefix + "defence",
                declared
            );
            AddStat(
                cells,
                panel.KineticPowerLabel,
                "%CategoryWeaponKineticTitle",
                keyPrefix + "kinetic",
                declared
            );
            AddStat(
                cells,
                panel.MissilePowerLabel,
                "%CategoryWeaponMissileTitle",
                keyPrefix + "missile",
                declared
            );
            AddStat(
                cells,
                panel.LaserPowerLabel,
                "%CategoryWeaponLaserTitle",
                keyPrefix + "laser",
                declared
            );
            AddStat(
                cells,
                panel.BeamPowerLabel,
                "%CategoryWeaponBeamTitle",
                keyPrefix + "beam",
                declared
            );
            AddStat(
                cells,
                panel.FighterCountLabel,
                "%SubCategoryModuleSquadronFighterTitle",
                keyPrefix + "fighters",
                declared
            );
            AddStat(
                cells,
                panel.BomberCountLabel,
                "%SubCategoryModuleSquadronBomberTitle",
                keyPrefix + "bombers",
                declared
            );
            AddStat(
                cells,
                panel.AccuracyLevelLabel,
                "%AccuracyTitle",
                keyPrefix + "accuracy",
                declared
            );
            AddStat(cells, panel.EvasionLevelLabel, "%EvasionTitle", keyPrefix + "evasion", declared);
            AddStat(
                cells,
                panel.PlatingHealthBonusLabel,
                "%PanelFeatureModuleEffectsDefenseHealthTitle",
                keyPrefix + "plating-health",
                declared
            );
            AddStat(
                cells,
                panel.PlatingAbsorptionLabel,
                "%HullPlatingAbsorptionPercentTitle",
                keyPrefix + "plating-absorption",
                declared
            );
            AddStat(
                cells,
                panel.ShieldCapacityLabel,
                "%ShieldTitle",
                keyPrefix + "shield-capacity",
                declared
            );
            AddStat(
                cells,
                panel.ShieldAbsorptionLabel,
                "%ShieldAbsorptionPercentTitle",
                keyPrefix + "shield-absorption",
                declared
            );
        }

        /// <summary>
        /// The damage the design does at each range, which the game draws under the accuracies of the
        /// same three ranges and only while its detailed switch is on.
        ///
        /// The three figures are three labels of ONE group, so the Nth of them is paired with the Nth
        /// range title - the same title the accuracy column above it draws for itself. Reading the group
        /// instead is what produced three identical "(0)"s with nothing saying which range each belonged
        /// to; passing each label as its own row is what keeps the three off one id and one rect.
        /// </summary>
        private static void AddRangeStats(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string keyPrefix,
            List<AgeTransform> declared
        )
        {
            AddStat(
                cells,
                panel.LongRangeDPSLabel,
                "%ShipStatRangeLongTitle",
                keyPrefix + "dps-long",
                declared,
                true
            );
            AddStat(
                cells,
                panel.MediumRangeDPSLabel,
                "%ShipStatRangeMediumTitle",
                keyPrefix + "dps-medium",
                declared,
                true
            );
            AddStat(
                cells,
                panel.ShortRangeDPSLabel,
                "%ShipStatRangeShortTitle",
                keyPrefix + "dps-short",
                declared,
                true
            );
        }

        private static bool Stats(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            // The skip list covers both what was declared by name and what the band declares itself -
            // its caption and its switch - so a widget on it is simply not walked into.
            if (_namedStats.Contains(widget))
            {
                return true;
            }

            RepartitionHorizontalGauge gauge = widget.GetComponent<RepartitionHorizontalGauge>();
            if (gauge != null)
            {
                BalanceGauges.Add(cells, gauge, keyPrefix + widget.name);
                return true;
            }

            return false;
        }

        /// <summary>The game's own switch for the breakdowns, which is a tick box and has to read as
        /// one: the walk would have made it a line of text saying "Detailed stats".</summary>
        private static void AddDetailedSwitch(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string keyPrefix
        )
        {
            AgeControlToggle toggle = panel.ShowDetailedStatsToggle;
            AgeTransform widget = AgeWidgets.Transform(toggle);
            // Kept for the same count as Stat above: this cell is one of the four lists the
            // statistics stop is opened from.
            if (toggle == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeWidgets.TextOf(widget),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, it);
            Cells.Add(cells, widget, ControlId.For(toggle, keyPrefix + "detailed"), vtable);
        }
    }
}

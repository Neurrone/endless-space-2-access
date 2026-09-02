using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// One side of a GROUND battle, as the setup and report popups draw it: who is leading it, which
    /// side of the invasion it is on, the tactic's own multipliers, the manpower and the troops
    /// committed, what did the damage, and what the game's outcome word means.
    ///
    /// Part of <see cref="BattleNotifications"/>; the popups themselves are declared there.
    /// </summary>
    internal static partial class BattleNotifications
    {
        /// <summary>
        /// One side of a ground battle being SET UP: who is leading it, which side of the invasion it
        /// is on, how much manpower it has committed, what it committed, and what the chosen tactic
        /// does to all of it.
        ///
        /// The role and the tactic's details are the setup panel's alone - the report draws neither -
        /// so they are read here rather than in <see cref="Contender"/>, which both popups share.
        /// </summary>
        private static void Troops(
            GraphBuilder builder,
            object region,
            string nameKey,
            BattleGroupInfoPanel info,
            GroundBattleContenderBasePanel panel,
            string prefix
        )
        {
            builder.SetRegion(region);
            bool named = BattleRows.Context(builder, nameKey);
            try
            {
                GroundBattleContenderSetupPanel setup =
                    panel as GroundBattleContenderSetupPanel;
                BattleRows.Leader(builder, info, prefix);
                Role(builder, setup, prefix);
                Contender(builder, panel, prefix);
                Details(builder, setup, prefix);
            }
            finally
            {
                BattleRows.Close(builder, named);
            }
        }

        /// <summary>Which side of the invasion this is. The panel draws it as a bare symbol between
        /// the two rosters and says which side it means in the sentence the symbol explains itself
        /// with, so the row is that symbol's name and that sentence - both the game's own.</summary>
        private static void Role(
            GraphBuilder builder,
            GroundBattleContenderSetupPanel panel,
            string prefix
        )
        {
            if (panel != null)
            {
                BattleRows.Note(builder, panel.AttackerDefenserLabel, prefix + "/role");
            }
        }

        /// <summary>
        /// What the chosen tactic does to this side: the health and damage multipliers and whatever
        /// the tactic does before the fighting starts, each a line the game has already captioned.
        ///
        /// Always offered, whether or not the player has the DETAILS block open. The block is
        /// collapsed by FADING it - the labels stay visible and keep their text current
        /// (<c>RefreshDetails</c> rewrites them on every refresh) and only their alpha goes to zero -
        /// so there is no state here worth modelling and nothing to open before reading. What the game
        /// genuinely does not offer it hides: the enemy's two multipliers are switched off outright,
        /// and are then not declared.
        /// </summary>
        private static void Details(
            GraphBuilder builder,
            GroundBattleContenderSetupPanel panel,
            string prefix
        )
        {
            if (panel == null)
            {
                return;
            }

            BattleRows.Note(builder, panel.DetailsHealthMultiplierLabel, prefix + "/health-multiplier");
            BattleRows.Note(builder, panel.DetailsDamageMultiplierLabel, prefix + "/damage-multiplier");
            BattleRows.Note(builder, panel.DetailsSpecialLabel, prefix + "/special");
        }

        /// <summary>What a ground contender has: the manpower it committed, the reserve behind it, and a
        /// row per kind of troop - or the game's own line saying it has none.</summary>
        private static void Contender(
            GraphBuilder builder,
            GroundBattleContenderBasePanel panel,
            string prefix
        )
        {
            // Flow control: a side the popup is not drawing must not be WALKED - the troop table below
            // is a component scrape per frame, and every row it finds would be declared and then
            // dropped one at a time.
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            try
            {
                AgePrimitiveLabel manpower = panel.ManpowerValueLabel;
                BattleRows.Value(
                    builder,
                    panel.ManpowerLine,
                    manpower,
                    GroundTroopsTitleKey,
                    prefix + "/manpower",
                    () => ManpowerReading(manpower)
                );
                BattleRows.Value(
                    builder,
                    panel.ReinforcementLine,
                    panel.ReinforcementValueLabel,
                    GroundReinforcementsTitleKey,
                    prefix + "/reinforcements"
                );
                BattleRows.Note(builder, panel.NoTroopsLabel, prefix + "/no-troops");

                AgeTransform table = panel.TroopCellsTable;
                TroopCell[] cells = table == null
                    ? new TroopCell[0]
                    : table.GetComponentsInChildren<TroopCell>(true);
                for (int i = 0; i < cells.Length; i++)
                {
                    Troop(builder, cells[i], prefix + "/troop/" + i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading a ground contender threw: " + e);
            }
        }

        /// <summary>One kind of troop: what it is (the game's own name for it, which the cell draws
        /// nowhere - it draws a picture and a number) and how many of them are left.</summary>
        private static void Troop(GraphBuilder builder, TroopCell cell, string key)
        {
            AgeTransform widget = cell == null ? null : cell.AgeTransform;
            if (widget == null)
            {
                return;
            }

            TroopCell it = cell;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => TroopName(it)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.TroopCount), false),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(cell, key), vtable, cell));
        }

        private static string TroopName(TroopCell cell)
        {
            try
            {
                GuiTroop troop = cell.GuiTroop;
                return troop == null ? null : AgeText.Clean(troop.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What this side's fighting came to: the total the report captions its damage gauge with, and
        /// then one row per thing that did the damage.
        ///
        /// The gauge is a stack of coloured blocks in a column beside the roster, one block per source,
        /// sized by how much of the total that source accounts for - so the whole of it is unreadable
        /// without the game's own explanations, which is where every figure here comes from. A source
        /// the fighting never used draws no block at all (the game gives it zero height and switches it
        /// off), and gets no row.
        ///
        /// The report panel is the only one with a gauge: the setup popup has nothing to report yet, so
        /// a setup panel answers nothing here.
        /// </summary>
        private static void Damage(
            GraphBuilder builder,
            GroundBattleContenderBasePanel panel,
            string prefix
        )
        {
            GroundBattleContenderReportPanel report =
                panel as GroundBattleContenderReportPanel;
            // Flow control: the gauge below is walked block by block, and a report the popup is not
            // drawing has nothing worth walking.
            if (report == null || !AgeWidgets.Visible(report.AgeTransform))
            {
                return;
            }

            try
            {
                // The blocks are stacked bottom-up in the pool's own order, so they are emitted the way
                // they were DRAWN rather than the way they were reserved: on this fixture bombardment
                // is the top block and infantry the one under it, and pool order has them the other way
                // round.
                List<Cell> cells = new List<Cell>();
                AgeTransform caption =
                    report.DamageIcon == null ? null : report.DamageIcon.AgeTransform;
                if (caption != null && !string.IsNullOrEmpty(AgeWidgets.TextOf(caption)))
                {
                    cells.Add(
                        Cells.Readout(caption, AgeWidgets.Raw(caption), prefix + "/damage")
                    );
                }

                DamageGauge gauge = report.DamageGauge;
                AgeTransform blocks = gauge == null ? null : gauge.EffectiveDamageCells;
                List<AgeTransform> children = blocks == null ? null : blocks.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    Source(cells, children[i], prefix + "/damage/" + i);
                }

                Cells.EmitLinear(builder, cells);
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading a ground damage gauge threw: " + e);
            }
        }

        /// <summary>One thing that did some of the damage - infantry, bombardment - as the sentence the
        /// game explains that block of the gauge with. The block draws no words of its own at all, so
        /// its name is the one the game keeps on the wrapper behind the explanation.</summary>
        private static void Source(List<Cell> cells, AgeTransform widget, string key)
        {
            if (widget == null)
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            if (tooltip == null || AgeWidgets.NeverDraws(tooltip))
            {
                return;
            }

            AgeTooltip it = tooltip;
            if (string.IsNullOrEmpty(AgeWidgets.TooltipTitle(it)))
            {
                return;
            }

            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.For(widget, key), vtable);
        }

        /// <summary>
        /// What the game says its own outcome word MEANS - "You inflicted significantly more damage
        /// than the enemy" behind "Major Victory".
        ///
        /// The space report hands the player that sentence by writing it onto the title's tooltip; the
        /// ground report writes only the word and leaves the sentence in the same GuiElement it took
        /// the word from, so the row goes and gets it.
        ///
        /// RE-DERIVES a private method, and there is no way not to: which of the nine outcomes the
        /// window drew is <c>GroundBattleReportNotificationWindow.ComputeGroundBattleResult</c>
        /// (:419-426), which is private, writes nothing down, and leaves only the localized WORD on the
        /// title - and the word is what the sentence would have to be looked up by. So its four
        /// branches are re-derived here off the same three game facts it reads (third-party role,
        /// attacker empire against the window's left side, attacker or defender result), and the
        /// element key is spelled the way the window spells it (:194). What keeps the two honest is
        /// that a re-derivation which went wrong would answer a DIFFERENT one of the nine elements, and
        /// its title is the word the player can hear the row say two lines earlier.
        /// </summary>
        private static IList<string> OutcomeDescription(
            GroundBattleReportNotificationWindow window,
            GroundBattle battle
        )
        {
            try
            {
                GroundBattleBattleReport report = battle == null ? null : battle.BattleReport;
                if (report == null || !report.IsValid || LeftEmpire == null)
                {
                    return null;
                }

                NotificationGroundBattleReport notification =
                    window.GuiNotification as NotificationGroundBattleReport;
                if (notification == null)
                {
                    return null;
                }

                GroundBattleOpponent role;
                bool third = battle.IsEmpireThirdParty(notification.Empire, out role);
                bool attacking = third
                    ? role == GroundBattleOpponent.Attacker
                    : battle.AttackerEmpire == (Empire)LeftEmpire.GetValue(window, null);
                GroundBattleResult result = attacking
                    ? battle.GetAttackerResult()
                    : battle.GetDefenderResult();

                Amplitude.Unity.Gui.GuiElement element = Gui.GetGuiElement(
                    "EndBattleStatus" + result
                );
                string raw = element == null ? null : element.Description;
                if (string.IsNullOrEmpty(raw) || !Gui.IsLocalizationKey(raw))
                {
                    return null;
                }

                string said = AgeText.Title(raw);
                return said == null ? null : AgeText.Lines(said);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

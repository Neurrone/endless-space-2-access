using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The four popups a battle is fought through, as bodies for the notification screen (the Variant
    /// registry in <see cref="NotificationScreen"/>).
    ///
    /// They are popups rather than screens, and that is the whole shape of combat in this game: watching
    /// the cinematic is OPTIONAL and off by default in the mod's terms - the setup popup carries a Watch
    /// box, and with it unticked the battle resolves the instant it starts and the player goes straight
    /// from the setup popup to the report popup. So these four popups are the only part of a battle every
    /// player meets, and they have to carry everything: who is fighting, with what, under which plan,
    /// and what became of each ship.
    ///
    /// None of that is text drawn in a band. A fleet's strength is a coloured arc, a ship's health is a
    /// bar, and the balance of power is two arcs with no number anywhere on them - which is why these
    /// popups are read from their own panels and wrappers rather than by the shared drawn-body reading.
    /// What the mod does NOT do is put its own words on any of it: the outcome word, each ship's fate,
    /// every button's name and every refusal come from the game's own strings.
    ///
    /// Both popups are walked SIDE BY SIDE rather than in strict drawn order: your leader, your plan,
    /// your fleets and what you earned are one region, theirs another. That is a deliberate deviation
    /// from the drawn layout (the two plan cards are drawn together, between the rosters): the player is
    /// comparing two sides, and Alt+Down crossing from "yours" to "theirs" in one step is what makes
    /// that comparison possible without counting rows.
    ///
    /// The countdown is declared but never live: a setup popup can be timed, and a gauge that announced
    /// itself under a standing cursor would talk over everything else the player is trying to read. It is
    /// there to be asked.
    /// </summary>
    internal static class BattleNotifications
    {
        // The bands the body is walked in, top to bottom, with Alt+Up/Down jumping between them.
        private static readonly object HeadingRegion = "battle:heading";
        private static readonly object YoursRegion = "battle:yours";
        private static readonly object TheirsRegion = "battle:theirs";
        private static readonly object AftermathRegion = "battle:aftermath";
        private static readonly object ControlsRegion = "battle:controls";

        /// <summary>Whether the balance of power is declared as a node of its own.
        ///
        /// It is a number the game draws NOWHERE - two arcs, one per side, sized by military power - so
        /// a sighted player reads the ratio and a listener gets nothing at all. Declaring it is
        /// therefore new information rather than a re-reading; the cost is that it is the one line here
        /// with no counterpart on screen. OWNER CALL: set false to leave it out.</summary>
        private const bool DeclareBalance = true;

        // The game's own words. Nothing here is a paraphrase: every one of these is a title the game
        // ships for exactly this control, and the icon-only ones are the reason a name is needed at all.
        private const string ArenaTitleKey = "%NotificationBattleSetupBattleArenaTitle";
        private const string PlanTitleKey = "%NotificationBattleSetupSelectedPlayTitle";
        private const string StartTitleKey = "%NotificationBattleSetupStartButtonTitle";
        private const string RetreatTitleKey = "%NotificationBattleSetupRetreatButtonTitle";
        private const string AdvancedPlayTitleKey = "%NotificationBattleSetupAdvancedPlayButtonTitle";
        private const string WatchToggleTitleKey = "%NotificationBattleSetupWatchToggleTitle";
        private const string ReorderTitleKey = "%NotificationBattleSetupReinforcementReorderingTitle";
        private const string ReportPlanTitleKey = "%NotificationBattleReportSelectedPlayTitle";
        private const string AdvancedReportTitleKey =
            "%NotificationBattleReportAdvancedReportButtonTitle";
        private const string WatchTitleKey = "%NotificationBattleReportWatchButtonTitle";
        private const string RewatchTitleKey = "%NotificationBattleReportRewatchButtonTitle";
        private const string ReplayTitleKey = "%NotificationBattleReportReplayButtonTitle";
        private const string GroundStrategyTitleKey = "%NotificationGroundBattleSetupSelectPlayTitle";
        private const string GroundTroopsTitleKey = "%NotificationGroundBattleSetupDispatchedTroopsTitle";
        private const string GroundReinforcementsTitleKey =
            "%NotificationGroundBattleSetupReinforcementsTitle";
        private const string GroundStandByTitleKey = "%NotificationGroundBattleReportStandByTitle";
        private const string GroundContinueTitleKey = "%NotificationGroundBattleReportContinueTitle";
        private const string GroundReplayTitleKey =
            "%NotificationGroundBattleReportReplayButtonTitle";

        // The mod's own phrases - only where the game writes nothing at all. See BattleText.Optional:
        // a build without these keys is silent about them rather than reading them aloud.
        private const string YourFleetsKey = "battle.your-fleets";
        private const string EnemyFleetsKey = "battle.enemy-fleets";
        private const string YourTroopsKey = "battle.your-troops";
        private const string EnemyTroopsKey = "battle.enemy-troops";
        private const string BalanceKey = "battle.balance";
        private const string TimeLeftKey = "battle.time-left";

        /// <summary>
        /// The setup popup: what is about to happen, and the three things the player decides about it -
        /// which battle plan, whether to watch, and whether to run.
        ///
        /// Everything is disabled the moment the player commits (the game switches the whole popup off
        /// while it waits for the other side), so each control reads its own availability rather than
        /// being dropped: "Fight, unavailable" is the popup telling the player it has their answer.
        /// </summary>
        public static void Setup(NotificationBody body)
        {
            BattleSetupNotificationWindow window = body.Window as BattleSetupNotificationWindow;
            if (window == null)
            {
                return;
            }

            GraphBuilder builder = body.Builder;
            NotificationBattleSetup notification =
                window.GuiNotification as NotificationBattleSetup;

            builder.SetRegion(HeadingRegion);
            Note(builder, window.BattleTitle, "battle-setup/title");
            Arena(builder, window.ArenaGroup, window.ArenaNameLabel, window.ArenaGroupTooltip);

            Side(
                builder,
                YoursRegion,
                YourFleetsKey,
                window.LeftBattleGroupInfoPanel,
                Widget(window.LeftBattleGroupSetupPanel),
                "battle-setup/yours"
            );
            Side(
                builder,
                TheirsRegion,
                EnemyFleetsKey,
                window.RightBattleGroupInfoPanel,
                Widget(window.RightBattleGroupSetupPanel),
                "battle-setup/theirs"
            );

            builder.SetRegion(AftermathRegion);
            Balance(
                builder,
                window.PowerBalanceGroup,
                Group(window.LeftBattleGroupSetupPanel),
                Group(window.RightBattleGroupSetupPanel),
                true,
                "battle-setup/balance"
            );
            Plan(builder, window);

            builder.SetRegion(ControlsRegion);
            List<Cell> controls = new List<Cell>();
            Checkbox(
                controls,
                window.WatchBattleToggle,
                WatchToggleTitleKey,
                "battle-setup/watch"
            );
            Command(controls, window.MoveReinforcementUp, ReorderTitleKey, "battle-setup/reinforce-up");
            Command(
                controls,
                window.MoveReinforcementDown,
                ReorderTitleKey,
                "battle-setup/reinforce-down"
            );
            Command(controls, window.AdvancedPlayButton, AdvancedPlayTitleKey, "battle-setup/advanced");
            Command(controls, window.StartBattleButton, StartTitleKey, "battle-setup/start");
            Command(controls, window.RetreatButton, RetreatTitleKey, "battle-setup/retreat");
            Countdown(
                controls,
                window.TimerGauge,
                notification == null ? (Func<float>)null : notification.GetTimeLeftRatio,
                "battle-setup/timer"
            );
            Cells.Emit(builder, controls);
        }

        /// <summary>
        /// The report popup: the game's own word for how it went, then the same two sides again with
        /// what became of every ship, and what the battle paid.
        /// </summary>
        public static void Report(NotificationBody body)
        {
            BattleReportNotificationWindow window = body.Window as BattleReportNotificationWindow;
            if (window == null)
            {
                return;
            }

            GraphBuilder builder = body.Builder;
            NotificationBattleReport notification =
                window.GuiNotification as NotificationBattleReport;

            // The outcome is one of nine words the game keeps a description for ("Decisive Victory",
            // and what that means for the two empires) - so the heading reads the word and carries the
            // description.
            builder.SetRegion(HeadingRegion);
            Note(builder, window.BattleTitle, "battle-report/outcome");
            Note(builder, window.OpponentHasRetreatedLabel, "battle-report/retreated");
            Arena(builder, null, window.ArenaNameLabel, window.ArenaGroupTooltip);

            builder.SetRegion(YoursRegion);
            bool yours = Context(builder, YourFleetsKey);
            Leader(builder, window.LeftBattleGroupInfoPanel, "battle-report/yours");
            Card(builder, window.PlayerBattlePlayCard, ReportPlanTitleKey, "battle-report/your-plan");
            BattleRosters.Roster(
                builder,
                Widget(window.LeftBattleGroupReportPanel),
                "battle-report/yours"
            );
            Rewards(builder, window.LeftBattleGroupReportPanel);
            Close(builder, yours);

            builder.SetRegion(TheirsRegion);
            bool theirs = Context(builder, EnemyFleetsKey);
            Leader(builder, window.RightBattleGroupInfoPanel, "battle-report/theirs");
            Card(builder, window.EnemyBattlePlayCard, ReportPlanTitleKey, "battle-report/their-plan");
            BattleRosters.Roster(
                builder,
                Widget(window.RightBattleGroupReportPanel),
                "battle-report/theirs"
            );
            Close(builder, theirs);

            builder.SetRegion(AftermathRegion);
            Balance(
                builder,
                null,
                Group(window.LeftBattleGroupReportPanel),
                Group(window.RightBattleGroupReportPanel),
                false,
                "battle-report/balance"
            );
            Note(builder, window.WarExhaustLabel, "battle-report/war-exhaust");

            builder.SetRegion(ControlsRegion);
            List<Cell> controls = new List<Cell>();
            Command(
                controls,
                window.AdvancedReportButton,
                AdvancedReportTitleKey,
                "battle-report/advanced"
            );
            Command(controls, window.WatchButton, WatchName(window), "battle-report/watch");
            Command(controls, window.ReplayButton, ReplayTitleKey, "battle-report/replay");
            Countdown(
                controls,
                Widget(window.ReplayTimerSector),
                notification == null ? (Func<float>)null : notification.GetReplayTimeLeftRatio,
                "battle-report/replay-timer"
            );
            Cells.Emit(builder, controls);
        }

        /// <summary>
        /// The ground battle's setup popup: the troops each side has, the strategy the player picks from
        /// a row of cards, and what the invasion will cost the system either way.
        /// </summary>
        public static void GroundSetup(NotificationBody body)
        {
            GroundBattleSetupNotificationWindow window =
                body.Window as GroundBattleSetupNotificationWindow;
            if (window == null)
            {
                return;
            }

            GraphBuilder builder = body.Builder;
            NotificationGroundBattleSetup notification =
                window.GuiNotification as NotificationGroundBattleSetup;

            builder.SetRegion(HeadingRegion);
            Note(builder, window.BattleTitle, "ground-setup/title");

            Troops(
                builder,
                YoursRegion,
                YourTroopsKey,
                window.LeftBattleGroupInfoPanel,
                window.LeftContenderPanel,
                "ground-setup/yours"
            );
            Troops(
                builder,
                TheirsRegion,
                EnemyTroopsKey,
                window.RightBattleGroupInfoPanel,
                window.RightContenderPanel,
                "ground-setup/theirs"
            );

            builder.SetRegion(AftermathRegion);
            Strategies(builder, window.GroundBattlePlayCardAccordionSlider, "ground-setup/strategy");
            Note(builder, window.PopulationDeathLabel, "ground-setup/population-death");
            Note(builder, window.ConstructionDestroyedLabel, "ground-setup/construction-destroyed");

            builder.SetRegion(ControlsRegion);
            List<Cell> controls = new List<Cell>();
            Checkbox(controls, window.WatchBattleToggle, WatchToggleTitleKey, "ground-setup/watch");
            Command(controls, window.StartBattleButton, StartTitleKey, "ground-setup/start");
            Countdown(
                controls,
                window.TimerGauge,
                notification == null ? (Func<float>)null : notification.GetTimeLeftRatio,
                "ground-setup/timer"
            );
            Cells.Emit(builder, controls);
        }

        /// <summary>
        /// The ground battle's report: how it went, what each side had left, what the fighting destroyed
        /// in the system, and - for a siege that is not over - whether to hold position or press on.
        /// </summary>
        public static void GroundReport(NotificationBody body)
        {
            GroundBattleReportNotificationWindow window =
                body.Window as GroundBattleReportNotificationWindow;
            if (window == null)
            {
                return;
            }

            GraphBuilder builder = body.Builder;

            builder.SetRegion(HeadingRegion);
            Note(builder, window.BattleTitle, "ground-report/outcome");
            Note(builder, window.BattleSubTitle, "ground-report/subtitle");

            builder.SetRegion(YoursRegion);
            bool yours = Context(builder, YourTroopsKey);
            Leader(builder, window.LeftBattleGroupInfoPanel, "ground-report/yours");
            Note(builder, window.LeftPlayTitle, "ground-report/your-strategy");
            Contender(builder, window.LeftContenderPanel, "ground-report/yours");
            Close(builder, yours);

            builder.SetRegion(TheirsRegion);
            bool theirs = Context(builder, EnemyTroopsKey);
            Leader(builder, window.RightBattleGroupInfoPanel, "ground-report/theirs");
            Note(builder, window.RightPlayTitle, "ground-report/their-strategy");
            Contender(builder, window.RightContenderPanel, "ground-report/theirs");
            Close(builder, theirs);

            // What the fighting did to the system, in the tables the popup fills line by line, each under
            // the caption it drew for it.
            builder.SetRegion(AftermathRegion);
            Note(builder, window.PopulationDeathTitle, "ground-report/population-title");
            Lines(builder, window.DestroyedPopulationsTable, "ground-report/population");
            Note(builder, window.ImprovementsDestroyedTitle, "ground-report/improvements-title");
            Lines(builder, window.DestroyedImprovementsTable, "ground-report/improvements");
            Note(builder, window.PirateLairTitle, "ground-report/pirate-lair");
            Lines(builder, window.LootSeizedTable, "ground-report/loot");
            Note(builder, window.HonorGainLabel, "ground-report/honor");
            Note(builder, window.WarExhaustLabel, "ground-report/war-exhaust");
            Readout(builder, window.WillContinueLabel, "ground-report/will-continue");

            builder.SetRegion(ControlsRegion);
            List<Cell> controls = new List<Cell>();
            Command(controls, window.StandByButton, GroundStandByTitleKey, "ground-report/stand-by");
            Command(controls, window.ContinueButton, GroundContinueTitleKey, "ground-report/continue");
            Command(controls, window.ReplayButton, GroundReplayTitleKey, "ground-report/replay");
            Cells.Emit(builder, controls);
        }

        // ---- the pieces both popups are made of ----

        /// <summary>One side of a space battle: who is leading it and every fleet they brought.</summary>
        private static void Side(
            GraphBuilder builder,
            object region,
            string nameKey,
            BattleGroupInfoPanel info,
            AgeTransform roster,
            string prefix
        )
        {
            builder.SetRegion(region);
            bool named = Context(builder, nameKey);
            try
            {
                Leader(builder, info, prefix);
                BattleRosters.Roster(builder, roster, prefix);
            }
            finally
            {
                Close(builder, named);
            }
        }

        /// <summary>One side of a ground battle: who is leading it, how much manpower it has committed
        /// and what it committed.</summary>
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
            bool named = Context(builder, nameKey);
            try
            {
                Leader(builder, info, prefix);
                Contender(builder, panel, prefix);
            }
            finally
            {
                Close(builder, named);
            }
        }

        /// <summary>What a ground contender has: the manpower it committed, the reserve behind it, and a
        /// row per kind of troop - or the game's own line saying it has none.</summary>
        private static void Contender(
            GraphBuilder builder,
            GroundBattleContenderBasePanel panel,
            string prefix
        )
        {
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            try
            {
                Value(
                    builder,
                    panel.ManpowerLine,
                    panel.ManpowerValueLabel,
                    GroundTroopsTitleKey,
                    prefix + "/manpower"
                );
                Value(
                    builder,
                    panel.ReinforcementLine,
                    panel.ReinforcementValueLabel,
                    GroundReinforcementsTitleKey,
                    prefix + "/reinforcements"
                );
                Note(builder, panel.NoTroopsLabel, prefix + "/no-troops");

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
            if (widget == null || !AgeWidgets.Visible(widget))
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
            builder.AddItem(ControlId.Referenced(cell, key), vtable);
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

        /// <summary>Who is fighting for this side, in the game's own "leader of faction" form, and the
        /// hero commanding it where there is one - the portrait carries the hero's whole dossier, so the
        /// row indicates having one and the buffer holds it.</summary>
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

            try
            {
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
            catch (Exception e)
            {
                Log.Warn("battle: reading a side's leader threw: " + e);
            }
        }

        /// <summary>Where the battle is being fought and what that does to it - the game writes the
        /// effects as a list of names and explains them on the group's own tooltip.</summary>
        private static void Arena(
            GraphBuilder builder,
            AgeTransform group,
            AgePrimitiveLabel label,
            AgeTooltip tooltip
        )
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Clean(ArenaTitleKey)),
                    GraphNodes.ValuePart(() => AgeText.Label(it), false),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, group ?? widget);
            builder.AddItem(ControlId.Referenced(label, "battle/arena"), vtable);
        }

        /// <summary>
        /// The battle plan, as the carousel the game made it: the card says which plan is in force and
        /// left and right step to the neighbouring one, which is exactly what the two arrows beside it do.
        ///
        /// Not a list of plans to pick from - the game never draws one - so there is nothing here to
        /// select; stepping IS choosing, and the card's own tooltip says how often that plan has been
        /// picked against this opponent.
        /// </summary>
        private static void Plan(GraphBuilder builder, BattleSetupNotificationWindow window)
        {
            AgeTransform group = window.PlayGroup;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            BattlePlayCard card = Card(window.SelectedPlayCardContainer);
            AgeControlButton previous = window.PreviousPlayButton;
            AgeControlButton next = window.NextPlayButton;
            AgeTooltip tooltip = card == null ? null : card.Tooltip;
            NodeVtable vtable = GraphNodes.Slider(
                () => AgeText.Clean(PlanTitleKey),
                () => card == null ? null : AgeText.Label(card.PlayTitle),
                (sign, large) => AgeWidgets.Press(sign < 0 ? previous : next),
                () => AgeWidgets.Operable(group),
                tooltip
            );
            AgeWidgets.PointAt(vtable, group);
            builder.AddItem(ControlId.Structural("battle-setup/plan"), vtable);
        }

        /// <summary>One side's plan on the report, where it is a card the game drew rather than a
        /// carousel.</summary>
        private static void Card(
            GraphBuilder builder,
            BattlePlayCard card,
            string titleKey,
            string key
        )
        {
            AgeTransform widget = card == null ? null : card.AgeTransform;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            BattlePlayCard it = card;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Clean(titleKey)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlayTitle), false),
                },
                Sections = GraphNodes.Sections(null, card.Tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(ControlId.Referenced(card, key), vtable);
        }

        private static BattlePlayCard Card(AgeTransform container)
        {
            try
            {
                return container == null
                    ? null
                    : container.GetComponentInChildren<BattlePlayCard>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The strategies a ground battle offers, as the one-of-N the game made them: a card
        /// each, exactly one in force, and one the empire has not unlocked refusing with the game's own
        /// reason for it.</summary>
        private static void Strategies(GraphBuilder builder, AccordionSlider slider, string prefix)
        {
            AgeTransform table = slider == null ? null : slider.CardTable;
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            try
            {
                GroundBattlePlayCard[] cards = table.GetComponentsInChildren<GroundBattlePlayCard>(
                    true
                );
                for (int i = 0; i < cards.Length; i++)
                {
                    GroundBattlePlayCard card = cards[i];
                    AgeTransform widget = card == null ? null : card.AgeTransform;
                    if (widget == null || !AgeWidgets.Visible(widget) || card.Toggle == null)
                    {
                        continue;
                    }

                    GroundBattlePlayCard it = card;
                    AgeTooltip tooltip = card.Tooltip;
                    NodeVtable vtable = GraphNodes.Radio(
                        () => StrategyName(it),
                        () => it.Toggle != null && it.Toggle.State,
                        () => AgeWidgets.Toggle(it.Toggle),
                        () => AgeWidgets.Operable(widget),
                        null,
                        tooltip
                    );
                    NodeAnnouncement refusal = GraphNodes.RefusalPart(
                        tooltip,
                        () => AgeWidgets.Operable(widget)
                    );
                    if (refusal != null)
                    {
                        vtable.Announcements.Add(refusal);
                    }

                    AgeWidgets.Point(vtable, it.Toggle, tooltip, widget);
                    builder.AddItem(
                        ControlId.Referenced(card, prefix + "/" + i),
                        vtable
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the ground strategies threw: " + e);
            }
        }

        /// <summary>What a strategy card is called: the words on it, which the card WRAPS to fit its box
        /// (and hyphenates when a word will not fit), so the game's own title for the strategy is read
        /// instead where there is one.</summary>
        private static string StrategyName(GroundBattlePlayCard card)
        {
            try
            {
                string title = card.Data == null ? null : AgeText.Clean(card.Data.Title);
                return string.IsNullOrEmpty(title)
                    ? AgeText.Label(card.TitleLabel)
                    : title;
            }
            catch (Exception)
            {
                return AgeText.Label(card.TitleLabel);
            }
        }

        /// <summary>
        /// The balance of power between the two sides, as the two numbers the arcs are sized from.
        ///
        /// The game draws no number here at all, and the ratio is the single most-read thing on the
        /// popup for a sighted player - it is what tells them whether to fight or run. Setup and report
        /// ask the same question of different halves of the model (what is committed vs what survived),
        /// which is the <paramref name="setup"/> flag.
        /// </summary>
        private static void Balance(
            GraphBuilder builder,
            AgeTransform group,
            EncounterGroup left,
            EncounterGroup right,
            bool setup,
            string key
        )
        {
            if (!DeclareBalance || left == null || right == null)
            {
                return;
            }

            if (BattleText.Optional(BalanceKey, 0, 0) == null)
            {
                return;
            }

            EncounterGroup ours = left;
            EncounterGroup theirs = right;
            bool useSetup = setup;
            AgeTooltip tooltip = AgeWidgets.Raw(group);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => BalanceText(ours, theirs, useSetup)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            if (group != null)
            {
                AgeWidgets.PointAt(vtable, group);
            }
            else
            {
                vtable.OnFocusVisual = AgeWidgets.ReleasePointer;
            }

            builder.AddItem(ControlId.Structural(key), vtable);
        }

        /// <summary>The two military-power figures the game's own helper computes for the arcs.</summary>
        internal static string BalanceText(EncounterGroup left, EncounterGroup right, bool setup)
        {
            try
            {
                float ours = GuiBattleHelpers.GetMilitaryPower(left, setup, true);
                float theirs = GuiBattleHelpers.GetMilitaryPower(right, setup, true);
                return BattleText.Optional(
                    BalanceKey,
                    Mathf.RoundToInt(ours),
                    Mathf.RoundToInt(theirs)
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the player earned: the resources, the salvage and the experience, each with the
        /// itemised breakdown the game hangs on it.</summary>
        private static void Rewards(GraphBuilder builder, PlayerBattleGroupReportPanel panel)
        {
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            try
            {
                Note(builder, panel.ResourcesEarnedTitleLabel, "battle-report/resources");
                Note(builder, panel.SalvageRescuedTitleLabel, "battle-report/salvage");
                Note(builder, panel.TotalExperienceTitleLabel, "battle-report/experience");
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the rewards threw: " + e);
            }
        }

        /// <summary>Which of the two words the game has for the watch button is on it - "Watch" for a
        /// battle nobody has seen, "Rewatch" for one the player has already sat through.</summary>
        private static string WatchName(BattleReportNotificationWindow window)
        {
            try
            {
                string drawn = AgeText.Label(window.WatchButtonLabel);
                return string.IsNullOrEmpty(drawn) ? AgeText.Clean(WatchTitleKey) : drawn;
            }
            catch (Exception)
            {
                return AgeText.Clean(RewatchTitleKey);
            }
        }

        /// <summary>Every line of a table the popup fills by cloning one - what it destroyed, what it
        /// looted - as the row each of them looks like.</summary>
        private static void Lines(GraphBuilder builder, AgeTransform table, string prefix)
        {
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
                    if (line == null || !AgeWidgets.Visible(line))
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
                    builder.AddItem(ControlId.Referenced(line, prefix + "/" + i), vtable);
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading a report table threw: " + e);
            }
        }

        // ---- the shapes a single widget takes ----

        /// <summary>A line the game wrote and is showing: read as it stands, with whatever it explains
        /// itself with carried along.</summary>
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

        /// <summary>The same for a band the game fills with words rather than a single label - the line
        /// saying a siege will go on.</summary>
        private static void Readout(GraphBuilder builder, AgeTransform widget, string key)
        {
            if (
                widget == null
                || !AgeWidgets.Visible(widget)
                || string.IsNullOrEmpty(AgeWidgets.TextOf(widget))
            )
            {
                return;
            }

            Cell cell = Cells.Readout(widget, AgeWidgets.Raw(widget), key);
            builder.AddItem(cell.Id, cell.Vtable);
        }

        /// <summary>A number the game drew beside a picture, under the game's own name for what the
        /// picture means.</summary>
        private static void Value(
            GraphBuilder builder,
            AgeTransform line,
            AgePrimitiveLabel value,
            string titleKey,
            string key
        )
        {
            AgeTransform widget = line ?? (value == null ? null : value.AgeTransform);
            if (widget == null || !AgeWidgets.Visible(widget) || value == null)
            {
                return;
            }

            AgePrimitiveLabel it = value;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Clean(titleKey)),
                    GraphNodes.ValuePart(() => AgeText.Label(it), false),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(ControlId.Referenced(value, key), vtable);
        }

        /// <summary>A button the popup drew as an icon, under the game's own title for it. Its
        /// availability is the game's own test rather than the enable flag, and a refusal reads with the
        /// game's own reason.</summary>
        private static void Command(
            List<Cell> cells,
            AgeTransform widget,
            string titleKey,
            string key
        )
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Offered(it);
            NodeVtable vtable = GraphNodes.Button(
                () => Title(titleKey, it),
                () => AgeWidgets.Press(it),
                enabled,
                tooltip
            );
            NodeAnnouncement refusal = GraphNodes.RefusalPart(tooltip, enabled);
            if (refusal != null)
            {
                vtable.Announcements.Add(refusal);
            }

            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        /// <summary>A box the player ticks, under the game's own title. The watch box is the one control
        /// here that decides what the player MEETS - with it off the battle is over before it starts -
        /// so it says its state like any other box rather than being left to the tooltip.</summary>
        private static void Checkbox(
            List<Cell> cells,
            AgeControlToggle toggle,
            string titleKey,
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
                () => Title(titleKey, widget),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Offered(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, widget);
            Cells.Add(cells, widget, ControlId.Referenced(toggle, key), vtable);
        }

        /// <summary>
        /// How long is left, for a popup the game is timing.
        ///
        /// Never watched: the countdown would otherwise talk over every other thing on the popup, and a
        /// player who wants to know how long they have can ask. A gauge with no number on it is all the
        /// game draws, so the figure comes from the notification's own clock.
        /// </summary>
        private static void Countdown(
            List<Cell> cells,
            AgeTransform gauge,
            Func<float> ratio,
            string key
        )
        {
            if (gauge == null || !AgeWidgets.Visible(gauge) || ratio == null)
            {
                return;
            }

            if (BattleText.Optional(TimeLeftKey, 0) == null)
            {
                return;
            }

            Func<float> left = ratio;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => TimeLeft(left)),
                },
                OnFocusVisual = AgeWidgets.ReleasePointer,
            };
            Cells.Add(cells, gauge, ControlId.Structural(key), vtable);
        }

        private static string TimeLeft(Func<float> ratio)
        {
            try
            {
                return BattleText.Optional(
                    TimeLeftKey,
                    Mathf.Clamp(Mathf.RoundToInt(ratio() * 100f), 0, 100)
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a control is called: the words the game drew on it where it drew any, else the
        /// game's own title for it.</summary>
        private static string Title(string titleKey, AgeTransform widget)
        {
            string drawn = AgeWidgets.TextOf(widget);
            return string.IsNullOrEmpty(drawn) ? AgeText.Clean(titleKey) : drawn;
        }

        /// <summary>Which side of the battle the rows that follow belong to - announced once as focus
        /// enters, so a roster is audibly yours or theirs without every row saying so. A build with no
        /// such phrase opens no level at all, which is why every caller closes with
        /// <c>PopContextIfOpen</c>.</summary>
        private static bool Context(GraphBuilder builder, string nameKey)
        {
            string name = BattleText.Optional(nameKey);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            builder.PushContext(name, null, false);
            return true;
        }

        /// <summary>Close the level <see cref="Context"/> opened, where it opened one.</summary>
        private static void Close(GraphBuilder builder, bool opened)
        {
            if (opened)
            {
                builder.PopContext();
            }
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

        private static AgeTransform Widget(AgePrimitiveSector sector)
        {
            try
            {
                return sector == null ? null : sector.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static EncounterGroup Group(BattleGroupSetupPanel panel)
        {
            try
            {
                return panel == null ? null : panel.EncounterGroup;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static EncounterGroup Group(BattleGroupReportPanel panel)
        {
            try
            {
                return panel == null ? null : panel.EncounterGroup;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

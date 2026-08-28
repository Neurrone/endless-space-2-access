using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The five popups a battle is fought through, as bodies for the notification screen (the Variant
    /// registry in <see cref="NotificationScreen"/>) - the four the fighting itself is watched from,
    /// and the one that asks what to do with a system the invasion took.
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
    /// The countdown is declared but never live: a setup popup and an outcome choice can both be timed
    /// (multiplayer only), and a gauge that announced itself under a standing cursor would talk over
    /// everything else the player is trying to read. It is there to be asked.
    /// </summary>
    internal static class BattleNotifications
    {
        // The bands the body is walked in, top to bottom, with Alt+Up/Down jumping between them.
        private static readonly object HeadingRegion = "battle:heading";
        private static readonly object YoursRegion = "battle:yours";
        private static readonly object TheirsRegion = "battle:theirs";
        private static readonly object AftermathRegion = "battle:aftermath";
        private static readonly object OutcomesRegion = "battle:outcomes";
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

        // The shared skeleton's own word for the button that puts a choice into effect, and the name the
        // outcome popup's prefab draws that button under (see Validate).
        private const string ValidateTitleKey = "%NotificationValidateTitle";
        private const string ValidateButtonName = "ValidateButton";

        // The mod's own phrases - only where the game writes nothing at all. See BattleText.Optional:
        // a build without these keys is silent about them rather than reading them aloud.
        private const string YourFleetsKey = "battle.your-fleets";
        private const string EnemyFleetsKey = "battle.enemy-fleets";
        private const string YourTroopsKey = "battle.your-troops";
        private const string EnemyTroopsKey = "battle.enemy-troops";
        private const string BalanceKey = "battle.balance";
        private const string GroundBalanceKey = "battle.ground-balance";
        private const string TimeLeftKey = "battle.time-left";

        // The impact arrows the game writes INTO a figure to mark one its own rules have moved. They
        // are named like any other icon everywhere else in the mod; inside a number they are not a
        // word at all (see ManpowerReading).
        private static readonly string[] ImpactMarkers =
        {
            "[negativeImpactWhite]",
            "[positiveImpactWhite]",
        };

        // The two arcs of the ground gauge are sized from these, and the window keeps the two indices
        // to itself. Which of the pair is this player's side is a four-branch answer (attacker,
        // defender, and a third party watching either) the game has already written down, so it is
        // read back rather than worked out again here - a second copy of that rule would drift.
        private static readonly PropertyInfo LeftManpowerIndex = ManpowerIndex(
            "LeftEmpireManpowerIndex"
        );
        private static readonly PropertyInfo RightManpowerIndex = ManpowerIndex(
            "RightEmpireManpowerIndex"
        );

        // Which empire the popup is calling "the left one" - the same four-branch answer, and what the
        // report's own outcome test compares the attacker against.
        private static readonly PropertyInfo LeftEmpire = ManpowerIndex("LeftEmpire");

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
            Cells.EmitLinear(builder, controls);
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
            Cells.EmitLinear(builder, controls);
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
            GroundBalance(
                builder,
                window,
                notification == null ? null : notification.GroundBattle,
                true,
                "ground-setup/balance"
            );

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

            // The two percentages the tactic will cost the system. The game writes no caption beside
            // either of them - the picture on the group is the caption - so each is named by the
            // sentence the group explains itself with, which is where the game put those words.
            List<Cell> costs = new List<Cell>();
            Cells.AddStat(costs, window.PopulationDeathLabel, null, "ground-setup/population-death");
            Cells.AddStat(
                costs,
                window.ConstructionDestroyedLabel,
                null,
                "ground-setup/construction-destroyed"
            );
            Cells.EmitLinear(builder, costs);

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
            Cells.EmitLinear(builder, controls);
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
            NotificationGroundBattleReport notification =
                window.GuiNotification as NotificationGroundBattleReport;
            GroundBattle battle = notification == null ? null : notification.GroundBattle;

            builder.SetRegion(HeadingRegion);
            GroundBattleReportNotificationWindow at = window;
            GroundBattle it = battle;
            Note(
                builder,
                window.BattleTitle,
                "ground-report/outcome",
                null,
                () => OutcomeDescription(at, it),
                // Said outright, as the space report says it: the outcome word alone ("Major Victory")
                // is a verdict with no size to it, and the sentence behind it is what the popup exists
                // to tell the player. The space report gets there by having the game write the sentence
                // onto the title.s tooltip; here the row went and got it, so here the row says it.
                true
            );
            Note(builder, window.BattleSubTitle, "ground-report/subtitle");
            GroundBalance(builder, window, battle, false, "ground-report/balance");

            builder.SetRegion(YoursRegion);
            bool yours = Context(builder, YourTroopsKey);
            Leader(builder, window.LeftBattleGroupInfoPanel, "ground-report/yours");
            Note(
                builder,
                window.LeftPlayTitle,
                "ground-report/your-strategy",
                window.LeftPlayTooltip
            );
            Contender(builder, window.LeftContenderPanel, "ground-report/yours");
            Damage(builder, window.LeftContenderPanel, "ground-report/yours");
            Close(builder, yours);

            builder.SetRegion(TheirsRegion);
            bool theirs = Context(builder, EnemyTroopsKey);
            Leader(builder, window.RightBattleGroupInfoPanel, "ground-report/theirs");
            Note(
                builder,
                window.RightPlayTitle,
                "ground-report/their-strategy",
                window.RightPlayTooltip
            );
            Contender(builder, window.RightContenderPanel, "ground-report/theirs");
            Damage(builder, window.RightContenderPanel, "ground-report/theirs");
            Close(builder, theirs);

            // What the fighting did to the system, in the tables the popup fills line by line, each
            // under the caption it drew for it - and the caption is the table's NAME rather than a stop
            // of its own, which is the shared rule for every caption over a block
            // (<see cref="Captions"/>). Both blocks are that shape whether the game filled them or
            // not: an empty one is the caption naming a block whose one row is the count (Wreckage).
            builder.SetRegion(AftermathRegion);
            Wreckage(
                builder,
                window.PopulationDeathTitle,
                "ground-report/population-title",
                Casualties(window.DestroyedPopulationsTable, "ground-report/population")
            );
            Wreckage(
                builder,
                window.ImprovementsDestroyedTitle,
                "ground-report/improvements-title",
                Wrecked(window.DestroyedImprovementsTable, "ground-report/improvements")
            );
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
            Cells.EmitLinear(builder, controls);
        }

        /// <summary>
        /// What to do with a system the invasion has just taken: what was captured, and the fate the
        /// player picks for it.
        ///
        /// The popup is drawn in two halves and read in two regions. The header is the system itself -
        /// its name, how developed it is, who lives there, what is still standing - and the game draws
        /// every part of it as a picture or a bare figure: the level is a number in a badge, each
        /// species is an icon with a count beside it and its name only on the tooltip behind it, and
        /// the improvements and wonders are an icon written into the figure itself. The outcomes are the
        /// decision, and they are read by the shared one-of-N (<see cref="NotificationScreen.BuildChoices"/>)
        /// exactly as every other hand-wired choice is: the card's own words, the game's reason for
        /// refusing it, and the second click that validates it.
        ///
        /// The countdown exists only in multiplayer, where the lobby can put a clock on this decision
        /// (<c>NotificationGroundBattleOutcomeSelection.IsTimeLimited</c>); a single-player popup draws
        /// no gauge and declares no node. As everywhere else here it is asked rather than announced.
        /// </summary>
        public static void GroundOutcome(NotificationBody body)
        {
            GroundBattleOutcomeSelectionNotificationWindow window =
                body.Window as GroundBattleOutcomeSelectionNotificationWindow;
            if (window == null)
            {
                return;
            }

            GraphBuilder builder = body.Builder;
            NotificationGroundBattleOutcomeSelection notification =
                window.GuiNotification as NotificationGroundBattleOutcomeSelection;

            builder.SetRegion(HeadingRegion);
            Note(builder, window.SystemNameLabel, "ground-outcome/system");
            Level(builder, window.SystemLevelLabel, "ground-outcome/level");
            Populations(builder, window.SystemPopulationCountTable, "ground-outcome/population");
            // Exactly one of the two is drawn: the popup switches the count table off for a system with
            // nobody left in it and puts its own line up instead (Refresh :166-187).
            Note(builder, window.SystemPopulationNoneLabel, "ground-outcome/no-population");
            Note(builder, window.SystemImprovementsLabel, "ground-outcome/improvements");
            Note(builder, window.SystemWondersLabel, "ground-outcome/wonders");

            builder.SetRegion(OutcomesRegion);
            NotificationScreen.BuildChoices(builder, window);

            builder.SetRegion(ControlsRegion);
            List<Cell> controls = new List<Cell>();
            Command(controls, Validate(window), ValidateTitleKey, "ground-outcome/validate");
            Countdown(
                controls,
                window.TimerGauge,
                notification == null ? (Func<float>)null : notification.GetTimeLeftRatio,
                "ground-outcome/timer"
            );
            Cells.EmitLinear(builder, controls);
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
            bool named = Context(builder, nameKey);
            try
            {
                GroundBattleContenderSetupPanel setup =
                    panel as GroundBattleContenderSetupPanel;
                Leader(builder, info, prefix);
                Role(builder, setup, prefix);
                Contender(builder, panel, prefix);
                Details(builder, setup, prefix);
            }
            finally
            {
                Close(builder, named);
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
                Note(builder, panel.AttackerDefenserLabel, prefix + "/role");
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

            Note(builder, panel.DetailsHealthMultiplierLabel, prefix + "/health-multiplier");
            Note(builder, panel.DetailsDamageMultiplierLabel, prefix + "/damage-multiplier");
            Note(builder, panel.DetailsSpecialLabel, prefix + "/special");
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
                Value(
                    builder,
                    panel.ManpowerLine,
                    manpower,
                    GroundTroopsTitleKey,
                    prefix + "/manpower",
                    () => ManpowerReading(manpower)
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
        /// the word from, so the row goes and gets it. Which of the nine words is drawn is the window's
        /// own four-branch answer, read back rather than re-derived, exactly as the manpower indices
        /// are.
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

                string said = AgeText.Clean(raw);
                return string.IsNullOrEmpty(said) || Gui.IsLocalizationKey(said)
                    ? null
                    : AgeText.Lines(said);
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
            if (panel == null)
            {
                return;
            }

            try
            {
                Note(builder, panel.MainLeaderName, prefix + "/leader");
                AgePrimitiveImage portrait = panel.MainHeroPortrait;
                AgeTransform widget = portrait == null ? null : portrait.AgeTransform;
                if (widget == null)
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
                builder.AddItem(Nodes.Drawn(ControlId.For(portrait, prefix + "/hero"), vtable, portrait));
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
            if (widget == null)
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
            builder.AddItem(Nodes.Drawn(ControlId.For(label, "battle/arena"), vtable, label));
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
            // Synthetic guard: the carousel is one node standing for three widgets, so it declares no
            // evidence and the gate has nothing to ask - this is the whole of its existence test.
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
            // Synthetic: one node standing for a card and two arrows, so there is no single widget
            // that draws it; the group above is only what it is anchored to.
            builder.AddItem(Nodes.Synthetic(ControlId.Structural("battle-setup/plan"), vtable));
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
            if (widget == null)
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
            builder.AddItem(Nodes.Drawn(ControlId.For(card, key), vtable, card));
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
            // Flow control: the cards are found by a component scrape, which is not worth running for
            // a table the accordion has closed.
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
                    if (widget == null || card.Toggle == null)
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
                    builder.AddItem(Nodes.Drawn(
                        ControlId.For(card, prefix + "/" + i),
                        vtable,
                        card
                    ));
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

            // Synthetic: the balance is computed from the battle, which the popup draws only as an
            // arc with no figure on it.
            builder.AddItem(Nodes.Synthetic(ControlId.Structural(key), vtable));
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

        /// <summary>
        /// The balance of power on the ground, as the two manpower figures the arcs are sized from.
        ///
        /// The same silence as the space gauge, over a different quantity: the disk between the two
        /// rosters draws one arc per side and no number anywhere, and it is the first thing a sighted
        /// player reads off this popup. The figures are the game's own, taken from the very expression
        /// that sizes the arcs, so the line and the picture can never disagree.
        /// </summary>
        private static void GroundBalance(
            GraphBuilder builder,
            GroundBattleNotificationWindow window,
            GroundBattle battle,
            bool setup,
            string key
        )
        {
            if (!DeclareBalance || window == null || battle == null)
            {
                return;
            }

            if (BattleText.Optional(GroundBalanceKey, 0, 0) == null)
            {
                return;
            }

            AgeTransform group =
                window.BattlePowerGauge == null ? null : window.BattlePowerGauge.AgeTransform;
            // Synthetic guard: the line is composed from the battle rather than read off the gauge, so
            // it declares no evidence and the gate has nothing to ask.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            GroundBattleNotificationWindow at = window;
            GroundBattle it = battle;
            bool useSetup = setup;
            if (GroundBalanceText(at, it, useSetup) == null)
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(group);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => GroundBalanceText(at, it, useSetup)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, group);
            // Synthetic: the two manpower figures come out of the battle, not off the gauge - the
            // gauge is what the line is anchored to.
            builder.AddItem(Nodes.Synthetic(ControlId.Structural(key), vtable));
        }

        /// <summary>
        /// The two manpower figures the ground gauge is drawn from, in the player's own order - theirs
        /// on the right whichever side of the invasion they are on.
        ///
        /// The report reads the FINAL manpowers even where the gauge does not: a defender who
        /// surrendered leaves the game drawing a symbolic full arc against an empty one rather than the
        /// two figures, and the numbers are what the line is for.
        /// </summary>
        internal static string GroundBalanceText(
            GroundBattleNotificationWindow window,
            GroundBattle battle,
            bool setup
        )
        {
            float[] powers = setup ? CommittedManpower(battle) : RemainingManpower(battle);
            if (powers == null)
            {
                return null;
            }

            int ours = Manpower(window, powers, LeftManpowerIndex);
            int theirs = Manpower(window, powers, RightManpowerIndex);
            return ours < 0 || theirs < 0
                ? null
                : BattleText.Optional(GroundBalanceKey, ours, theirs);
        }

        /// <summary>What each side committed to the invasion - the setup gauge's own figures.</summary>
        private static float[] CommittedManpower(GroundBattle battle)
        {
            try
            {
                return battle.SpawnReport == null
                    ? null
                    : battle.SpawnReport.OpponentInitManPowers;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What each side has left - the report gauge's own figures, and only once the game
        /// says the report it computes them from is finished.</summary>
        private static float[] RemainingManpower(GroundBattle battle)
        {
            try
            {
                GroundBattleBattleReport report = battle.BattleReport;
                return report == null || !report.IsValid
                    ? null
                    : report.OpponentFinalManPowers;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One side's manpower, rounded the way the gauge rounds it, or -1 where the window
        /// will not say which side that is.</summary>
        private static int Manpower(
            GroundBattleNotificationWindow window,
            float[] powers,
            PropertyInfo index
        )
        {
            if (index == null)
            {
                return -1;
            }

            try
            {
                int side = (int)index.GetValue(window, null);
                return side < 0 || side >= powers.Length
                    ? -1
                    : Mathf.RoundToInt(Mathf.Round(powers[side]));
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static PropertyInfo ManpowerIndex(string name)
        {
            try
            {
                return typeof(GroundBattleNotificationWindow).GetProperty(
                    name,
                    BindingFlags.NonPublic | BindingFlags.Instance
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
            if (panel == null)
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
            List<Cell> rows
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
            else if (named)
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
        /// figure read the 0 twice (owner, 2026-08-28). A caption with no figure in it at all gets no
        /// row, because a block the game filled is rows of its own.
        /// </summary>
        private static void Total(GraphBuilder builder, AgeTransform caption, string key)
        {
            if (string.IsNullOrEmpty(Figure(Captions.Text(caption))))
            {
                return;
            }

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

        /// <summary>The number a caption drew inside its own sentence - the LAST run of digits in it,
        /// which is where this game's count templates put it (both of these captions are a localized
        /// string taking the count as its only argument, <c>RefreshReportPanel</c> :304/:316). Null for
        /// a caption that drew no figure.</summary>
        private static string Figure(string caption)
        {
            if (string.IsNullOrEmpty(caption))
            {
                return null;
            }

            int end = -1;
            for (int i = caption.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(caption[i]))
                {
                    if (end < 0)
                    {
                        end = i;
                    }

                    continue;
                }

                if (end >= 0)
                {
                    return caption.Substring(i + 1, end - i);
                }
            }

            return end < 0 ? null : caption.Substring(0, end + 1);
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
            List<AgeTooltip> own = new List<AgeTooltip>(2);
            List<TooltipChildren.Dossier> dossiers = TooltipChildren.Split(tooltips, own);
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
            vtable.Sections = GraphNodes.SectionsFor(vtable, own);
            Cell cell = Cells.Add(cells, widget, ControlId.For(unit, key), vtable);
            cell.Dossiers = dossiers;
            cell.Key = dossiers == null ? null : key;
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
            List<AgeTooltip> own = new List<AgeTooltip>(2);
            List<TooltipChildren.Dossier> dossiers = TooltipChildren.Split(tooltips, own);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => RuinName(at, it)),
                },
            };
            vtable.Sections = GraphNodes.SectionsFor(vtable, own);
            Cell cell = Cells.Add(cells, widget, ControlId.For(item, key), vtable);
            cell.Dossiers = dossiers;
            cell.Key = dossiers == null ? null : key;
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

        // ---- the shapes a single widget takes ----

        /// <summary>
        /// A line the game wrote and is showing: read as it stands, with whatever it explains itself
        /// with carried along.
        ///
        /// <paramref name="explains"/> is for a line whose dossier the WINDOW holds rather than the
        /// label - the ground report keeps each side's strategy tooltip in a field of its own, hung on
        /// the card around the words - so the row carries it and the pointer is aimed at the widget it
        /// is really on. <paramref name="details"/> is the game.s own further words about the line that
        /// the popup itself never draws anywhere, and <paramref name="sayDetails"/> is whether the row
        /// hands them over as it is read or leaves them in the review buffer. Saying them is for words
        /// that are the POINT of the row - the sentence behind an outcome word, which is what the
        /// player wanted when they landed on it - and the default is the buffer, because a row whose
        /// further words are a second reading of something already on screen would say the screen back.
        /// It is a fact about the row, not about a tooltip: the tooltip that comes after answers for
        /// its own loudness by its own kind.
        /// </summary>
        private static void Note(
            GraphBuilder builder,
            AgePrimitiveLabel label,
            string key,
            AgeTooltip explains = null,
            Func<IList<string>> details = null,
            bool sayDetails = false
        )
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || string.IsNullOrEmpty(AgeText.Label(label)))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            AgeTooltip tooltip = explains ?? AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it)),
                },
                Sections = sayDetails
                    ? GraphNodes.SpokenSections(details, tooltip)
                    : GraphNodes.Sections(details, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget, tooltip);
            builder.AddItem(Nodes.Drawn(ControlId.For(label, key), vtable, label));
        }

        /// <summary>
        /// The button that puts the chosen outcome into effect.
        ///
        /// The window binds no field to it: the prefab draws it in the popup's own button bar, names it
        /// there, and the game wires the click by that name alone (<c>OnValidateCb</c>). So the name on
        /// screen is the only thing there is to ask for it by - and it has to be asked for, because a
        /// popup that writes its own body owns every control it added and nothing else would declare it
        /// (measured 2026-08-25: the prefab draws it as "Confirm" beside Minimize and Show Location).
        /// The card's own second click validates too; this is the button a player who has already picked
        /// their card presses.
        /// </summary>
        private static AgeTransform Validate(GroundBattleOutcomeSelectionNotificationWindow window)
        {
            try
            {
                return AgeWidgets.ChildNamed(window.AgeTransform, ValidateButtonName, 4);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// How developed a system is, which the game draws as a bare number in a badge.
        ///
        /// Nothing beside it says what the number counts - the badge's shape is the caption on screen -
        /// so the row is named the way the system page names the identical figure
        /// (<see cref="ModStrings.SystemLevel"/>, "System level 3"), which is the mod's own phrase for
        /// a figure the game captions nowhere.
        /// </summary>
        private static void Level(GraphBuilder builder, AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || string.IsNullOrEmpty(AgeText.Label(label)))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () => ModStrings.Format(ModStrings.SystemLevel, AgeText.Label(it))
                    ),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget, tooltip);
            builder.AddItem(Nodes.Drawn(ControlId.For(label, key), vtable, label));
        }

        /// <summary>Who lives in the captured system - one cell per species the popup filled the table
        /// with, in the order it laid them out.</summary>
        private static void Populations(GraphBuilder builder, AgeTransform table, string prefix)
        {
            // Flow control: same as the report tables - a table the popup is not drawing is not walked
            // cell by cell.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            try
            {
                List<Cell> cells = new List<Cell>();
                List<AgeTransform> children = table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    People(cells, table, children[i], prefix + "/" + i);
                }

                Cells.EmitLinear(builder, cells);
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the captured populations threw: " + e);
            }
        }

        /// <summary>One kind of person captured with the system: their symbol and how many of them there
        /// are, and their NAME nowhere on the cell at all - the game keeps it on the wrapper behind the
        /// cell's tooltip, which is where every other reading of this control gets it
        /// (<c>SystemManagementScreen.PopulationCell</c>). Clicking it opens the empire's population
        /// window, which is the cell's own handler (<c>PopulationCount.OnClickCb</c>).
        ///
        /// The table is POOLED - the popup reserves a cell per species and keeps the rest around with
        /// their last species still written on them - so a cell is a cell only while the table is
        /// drawing it. That is the gate's own question now: the cell stands on this widget, and a
        /// retired one is taken out before the cells are banded.</summary>
        private static void People(
            List<Cell> cells,
            AgeTransform table,
            AgeTransform widget,
            string key
        )
        {
            if (widget == null)
            {
                return;
            }

            PopulationCount unit = widget.GetComponent<PopulationCount>();
            if (unit == null)
            {
                return;
            }

            AgeTooltip tooltip = unit.Tooltip;
            AgePrimitiveLabel count = unit.Count;
            AgeTransform at = widget;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TooltipTitle(tooltip),
                () => AgeWidgets.Press(at),
                () => AgeWidgets.Operable(at),
                tooltip
            );
            vtable.Announcements.Insert(1, GraphNodes.ValuePart(() => AgeText.Label(count)));
            AgeWidgets.PointAt(vtable, widget, tooltip);
            Cells.Add(cells, widget, ControlId.For(unit, key), vtable);
        }

        /// <summary>The same for a band the game fills with words rather than a single label - the line
        /// saying a siege will go on.</summary>
        private static void Readout(GraphBuilder builder, AgeTransform widget, string key)
        {
            if (widget == null || string.IsNullOrEmpty(AgeWidgets.TextOf(widget)))
            {
                return;
            }

            Cell cell = Cells.Readout(widget, AgeWidgets.Raw(widget), key);
            builder.AddItem(Nodes.Drawn(cell.Id, cell.Vtable, cell.Widget));
        }

        /// <summary>A number the game drew beside a picture, under the game's own name for what the
        /// picture means. <paramref name="reading"/> is for a figure whose drawn string says something
        /// other than what it looks like - the assigned-manpower one; everything else reads the label
        /// as it stands.</summary>
        private static void Value(
            GraphBuilder builder,
            AgeTransform line,
            AgePrimitiveLabel value,
            string titleKey,
            string key,
            Func<string> reading = null
        )
        {
            AgeTransform widget = line ?? (value == null ? null : value.AgeTransform);
            // Different widget: where a ROW was passed the node stands on the label inside it, and this
            // asks about the row instead. Also the widget the title is read off (RowTitle).
            if (widget == null || !AgeWidgets.Visible(widget) || value == null)
            {
                return;
            }

            AgePrimitiveLabel it = value;
            AgeTransform row = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<string> said = reading ?? (() => AgeText.Label(it));
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => RowTitle(row, it, titleKey)),
                    GraphNodes.ValuePart(said, false),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(value, key), vtable, value));
        }

        /// <summary>
        /// What the game DREW over one of these figures, and only failing that the title it ships for
        /// the row.
        ///
        /// The two ground popups share one row prefab and one pair of panel classes, and the report's
        /// prefab captions the same line with a different word: the setup says "Assigned" and the
        /// report says "Remaining", because after the fighting the figure is what is LEFT rather than
        /// what was committed. Neither panel class rewrites that caption, so the only place the
        /// difference exists is the drawing - and a row named from the shared title key told the player
        /// the wrong thing on the report for as long as the key was the only source.
        /// </summary>
        private static string RowTitle(AgeTransform line, AgePrimitiveLabel value, string titleKey)
        {
            string drawn = null;
            try
            {
                List<AgeTransform> children = line == null ? null : line.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    AgePrimitiveLabel label =
                        child == null ? null : child.GetComponent<AgePrimitiveLabel>();
                    if (label == null || ReferenceEquals(label, value))
                    {
                        continue;
                    }

                    drawn = AgeText.Label(label);
                    break;
                }
            }
            catch (Exception) { }

            return string.IsNullOrEmpty(drawn) ? AgeText.Clean(titleKey) : drawn;
        }

        /// <summary>
        /// What the assigned-manpower figure says.
        ///
        /// The game marks a deployment limit its own tactic has moved by writing an impact ARROW into
        /// the number itself - "375[negativeImpactWhite]" - and the mod names that arrow "negative"
        /// like any other icon, so the figure came out as "160/375 negative Manpower": a number that
        /// sounds negative and is not. The arrow is dropped from THIS figure only, because the words
        /// it stands for are already on the row: the game appends its own sentence about the tactic
        /// raising or lowering the limit to the line's tooltip, which the row carries.
        /// </summary>
        private static string ManpowerReading(AgePrimitiveLabel label)
        {
            string raw = null;
            try
            {
                raw = label == null ? null : label.Text;
            }
            catch (Exception) { }

            bool marked = false;
            for (int i = 0; raw != null && i < ImpactMarkers.Length; i++)
            {
                if (raw.IndexOf(ImpactMarkers[i], StringComparison.Ordinal) >= 0)
                {
                    raw = raw.Replace(ImpactMarkers[i], string.Empty);
                    marked = true;
                }
            }

            // Nothing to drop: read the figure exactly as every other one is read, rather than
            // through a second path that could answer differently.
            return marked ? AgeText.Clean(raw) : AgeText.Label(label);
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
            if (widget == null)
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
            Cells.Add(cells, widget, ControlId.For(widget, key), vtable);
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
            if (toggle == null)
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
            Cells.Add(cells, widget, ControlId.For(toggle, key), vtable);
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
            if (gauge == null || ratio == null)
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

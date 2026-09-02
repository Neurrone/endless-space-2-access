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
        private static readonly object PlanRegion = "battle:plan";
        private static readonly object OutcomesRegion = "battle:outcomes";
        private static readonly object ControlsRegion = "battle:controls";

        // The game's own words. Nothing here is a paraphrase: every one of these is a title the game
        // ships for exactly this control, and the icon-only ones are the reason a name is needed at all.
        private const string ArenaTitleKey = "%NotificationBattleSetupBattleArenaTitle";
        private const string AdvancedPlayTitleKey = "%NotificationBattleSetupAdvancedPlayButtonTitle";
        private const string ReorderTitleKey = "%NotificationBattleSetupReinforcementReorderingTitle";
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

        // The name the outcome popup's prefab draws its validate button under (see Validate); the title
        // the button is READ under is shared (BattleRows.ValidateTitleKey).
        private const string ValidateButtonName = "ValidateButton";

        // The impact arrows the game writes INTO a figure to mark one its own rules have moved. They
        // are named like any other icon everywhere else in the mod; inside a number they are not a
        // word at all (see ManpowerReading).
        private static readonly string[] ImpactMarkers =
        {
            "[negativeImpactWhite]",
            "[positiveImpactWhite]",
        };

        // Which empire the popup is calling "the left one" - the same four-branch answer, and what the
        // report's own outcome test compares the attacker against.
        private static readonly PropertyInfo LeftEmpire = GameHandlers.Property(
            typeof(GroundBattleNotificationWindow),
            "LeftEmpire"
        );

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
            BattleRows.Note(builder, window.BattleTitle, "battle-setup/title");
            Arena(builder, window.ArenaGroup, window.ArenaNameLabel, window.ArenaGroupTooltip);

            Side(
                builder,
                YoursRegion,
                ModStrings.BattleYourFleets,
                window.LeftBattleGroupInfoPanel,
                AgeWidgets.Transform(window.LeftBattleGroupSetupPanel),
                "battle-setup/yours"
            );
            Side(
                builder,
                TheirsRegion,
                ModStrings.BattleEnemyFleets,
                window.RightBattleGroupInfoPanel,
                AgeWidgets.Transform(window.RightBattleGroupSetupPanel),
                "battle-setup/theirs"
            );

            builder.SetRegion(AftermathRegion);
            BattleBalance.Balance(
                builder,
                window.PowerBalanceGroup,
                Group(window.LeftBattleGroupSetupPanel),
                Group(window.RightBattleGroupSetupPanel),
                true,
                "battle-setup/balance"
            );

            // One band from the plan to the end (owner, 2026-08-29): Alt+Down from the balance lands
            // ON the plan rather than skipping to the controls, and the four buttons after it are not
            // a section of their own - they are simply the next rows down.
            builder.SetRegion(PlanRegion);
            Plan(builder, window);

            List<Cell> controls = new List<Cell>();
            Checkbox(
                controls,
                window.WatchBattleToggle,
                BattleRows.WatchToggleTitleKey,
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
            Command(controls, window.StartBattleButton, BattleRows.StartTitleKey, "battle-setup/start");
            Command(controls, window.RetreatButton, BattleRows.RetreatTitleKey, "battle-setup/retreat");
            BattleRows.Countdown(
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
            BattleRows.Note(builder, window.BattleTitle, "battle-report/outcome");
            BattleRows.Note(builder, window.OpponentHasRetreatedLabel, "battle-report/retreated");
            Arena(builder, null, window.ArenaNameLabel, window.ArenaGroupTooltip);

            builder.SetRegion(YoursRegion);
            bool yours = BattleRows.Context(builder, ModStrings.BattleYourFleets);
            BattleRows.Leader(builder, window.LeftBattleGroupInfoPanel, "battle-report/yours");
            Card(
                builder,
                window.PlayerBattlePlayCard,
                BattleRows.ReportPlanTitleKey,
                "battle-report/your-plan"
            );
            BattleRosters.Roster(
                builder,
                AgeWidgets.Transform(window.LeftBattleGroupReportPanel),
                "battle-report/yours"
            );
            Rewards(builder, window.LeftBattleGroupReportPanel, "battle-report");
            BattleRows.Close(builder, yours);

            builder.SetRegion(TheirsRegion);
            bool theirs = BattleRows.Context(builder, ModStrings.BattleEnemyFleets);
            BattleRows.Leader(builder, window.RightBattleGroupInfoPanel, "battle-report/theirs");
            Card(
                builder,
                window.EnemyBattlePlayCard,
                BattleRows.ReportPlanTitleKey,
                "battle-report/their-plan"
            );
            BattleRosters.Roster(
                builder,
                AgeWidgets.Transform(window.RightBattleGroupReportPanel),
                "battle-report/theirs"
            );
            BattleRows.Close(builder, theirs);

            builder.SetRegion(AftermathRegion);
            BattleBalance.Balance(
                builder,
                null,
                Group(window.LeftBattleGroupReportPanel),
                Group(window.RightBattleGroupReportPanel),
                false,
                "battle-report/balance"
            );
            BattleRows.Note(builder, window.WarExhaustLabel, "battle-report/war-exhaust");

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
            BattleRows.Countdown(
                controls,
                AgeWidgets.Transform(window.ReplayTimerSector),
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
            BattleRows.Note(builder, window.BattleTitle, "ground-setup/title");
            BattleBalance.GroundBalance(
                builder,
                window,
                notification == null ? null : notification.GroundBattle,
                true,
                "ground-setup/balance"
            );

            Troops(
                builder,
                YoursRegion,
                ModStrings.BattleYourTroops,
                window.LeftBattleGroupInfoPanel,
                window.LeftContenderPanel,
                "ground-setup/yours"
            );
            Troops(
                builder,
                TheirsRegion,
                ModStrings.BattleEnemyTroops,
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
            Checkbox(
                controls,
                window.WatchBattleToggle,
                BattleRows.WatchToggleTitleKey,
                "ground-setup/watch"
            );
            Command(controls, window.StartBattleButton, BattleRows.StartTitleKey, "ground-setup/start");
            BattleRows.Countdown(
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
            BattleRows.Note(
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
            BattleRows.Note(builder, window.BattleSubTitle, "ground-report/subtitle");
            BattleBalance.GroundBalance(builder, window, battle, false, "ground-report/balance");

            builder.SetRegion(YoursRegion);
            bool yours = BattleRows.Context(builder, ModStrings.BattleYourTroops);
            BattleRows.Leader(builder, window.LeftBattleGroupInfoPanel, "ground-report/yours");
            BattleRows.Note(
                builder,
                window.LeftPlayTitle,
                "ground-report/your-strategy",
                window.LeftPlayTooltip
            );
            Contender(builder, window.LeftContenderPanel, "ground-report/yours");
            Damage(builder, window.LeftContenderPanel, "ground-report/yours");
            BattleRows.Close(builder, yours);

            builder.SetRegion(TheirsRegion);
            bool theirs = BattleRows.Context(builder, ModStrings.BattleEnemyTroops);
            BattleRows.Leader(builder, window.RightBattleGroupInfoPanel, "ground-report/theirs");
            BattleRows.Note(
                builder,
                window.RightPlayTitle,
                "ground-report/their-strategy",
                window.RightPlayTooltip
            );
            Contender(builder, window.RightContenderPanel, "ground-report/theirs");
            Damage(builder, window.RightContenderPanel, "ground-report/theirs");
            BattleRows.Close(builder, theirs);

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
                Casualties(window.DestroyedPopulationsTable, "ground-report/population"),
                Counted(battle)
            );
            Wreckage(
                builder,
                window.ImprovementsDestroyedTitle,
                "ground-report/improvements-title",
                Wrecked(window.DestroyedImprovementsTable, "ground-report/improvements"),
                Counted(battle)
            );
            BattleRows.Note(builder, window.PirateLairTitle, "ground-report/pirate-lair");
            Lines(builder, window.LootSeizedTable, "ground-report/loot");
            BattleRows.Note(builder, window.HonorGainLabel, "ground-report/honor");
            BattleRows.Note(builder, window.WarExhaustLabel, "ground-report/war-exhaust");
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
            BattleRows.Note(builder, window.SystemNameLabel, "ground-outcome/system");
            Level(builder, window.SystemLevelLabel, "ground-outcome/level");
            Populations(builder, window.SystemPopulationCountTable, "ground-outcome/population");
            // Exactly one of the two is drawn: the popup switches the count table off for a system with
            // nobody left in it and puts its own line up instead (Refresh :166-187).
            BattleRows.Note(builder, window.SystemPopulationNoneLabel, "ground-outcome/no-population");
            BattleRows.Note(builder, window.SystemImprovementsLabel, "ground-outcome/improvements");
            BattleRows.Note(builder, window.SystemWondersLabel, "ground-outcome/wonders");

            builder.SetRegion(OutcomesRegion);
            NotificationScreen.BuildChoices(builder, window);

            builder.SetRegion(ControlsRegion);
            List<Cell> controls = new List<Cell>();
            Command(controls, Validate(window), BattleRows.ValidateTitleKey, "ground-outcome/validate");
            BattleRows.Countdown(
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
            bool named = BattleRows.Context(builder, nameKey);
            try
            {
                BattleRows.Leader(builder, info, prefix);
                BattleRosters.Roster(builder, roster, prefix);
            }
            finally
            {
                BattleRows.Close(builder, named);
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

        /// <summary>
        /// Where the battle is being fought and what that does to it - the game writes the effects as
        /// a list of names and explains them on the group's own tooltip.
        ///
        /// That tooltip is EMPTY while the theater applies no effects, which is most battles, and the
        /// one sentence saying what this row is about at all ("Effects applied to all the ships in the
        /// Theater") hangs on the little separator beside the name instead. A second hover surface is a
        /// child entry, so the separator becomes one and the row keeps pointing at the tooltip a hover
        /// on the name would raise.
        /// </summary>
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

            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(1);
            TooltipChildren.AddPlain(dossiers, Separator(group));
            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(ControlId.For(label, ArenaKey), vtable, label),
                ArenaKey,
                dossiers
            );
        }

        private const string ArenaKey = "battle/arena";
        private const string SeparatorName = "Separator";

        /// <summary>The separator the arena group draws between its icon and its name - the one thing
        /// on that row carrying the sentence about what a theater's effects are. Found by the name the
        /// prefab gives it, because it is decoration with no component of its own to ask for.</summary>
        private static AgeTransform Separator(AgeTransform group)
        {
            try
            {
                List<AgeTransform> children = group == null ? null : group.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (child != null && child.name == SeparatorName)
                    {
                        return child;
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The battle plan, as the CLOSED combo box the chooser opens from.
        ///
        /// What the game draws is a page turner: one card, an arrow either side of it, and nothing at
        /// all saying how many plans there are or which of them is up. The row says which one is up -
        /// its name and the effects printed on it - and Enter opens the list of the rest
        /// (<see cref="BattlePlanScreen"/>), which is the droplist idiom every other one-of-N setting
        /// in this mod uses.
        ///
        /// It is ONE row and not a band of them (owner ruling 2026-08-29, replacing the band shipped
        /// the day before). Pressing an arrow does not merely SHOW a plan, it chooses it, so a band of
        /// rows made walking Up through the popup re-choose every plan the cursor crossed. Behind a
        /// chooser the browsing is something the player asked for and can back out of.
        ///
        /// The card's own tooltip - how often this plan has been picked against this opponent - is the
        /// row's, and the three range diagrams and the family badge drawn on the card are hover
        /// surfaces of their own, so each is a child entry. All of them read the DRAWN card, which on
        /// this row is always the plan in force: there is no other plan for this row to misdescribe.
        /// </summary>
        private static void Plan(GraphBuilder builder, BattleSetupNotificationWindow window)
        {
            AgeTransform group = window.PlayGroup;
            // Flow control: the row below stands on the one card the group draws, and a group the
            // window is not drawing has no card to read.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            BattlePlayCard card = BattlePlans.PlanCard(window);
            AgeTransform carrier = card == null ? null : card.AgeTransform;
            AgeTooltip tooltip = card == null ? null : card.Tooltip;

            BattleSetupNotificationWindow it = window;
            BattlePlayCard drawn = card;
            NodeVtable vtable = GraphNodes.ComboBox(
                () => AgeText.Clean(BattleRows.SetupPlanTitleKey),
                () => BattlePlans.PlayName(it, BattlePlans.CurrentPlay(it), drawn),
                () => BattlePlanScreen.Open(it),
                () => BattlePlans.Steppable(it),
                tooltip
            );
            // Activating this one opens a list rather than changing the plan: the list that opens says
            // where it starts.
            vtable.StateText = null;
            vtable.Announcements.Add(
                GraphNodes.ValuePart(
                    () => BattlePlans.PlanEffects(it, BattlePlans.CurrentPlay(it), drawn),
                    false
                )
            );

            TooltipChildren.Declare(
                builder,
                BattlePlans.Drawn(ControlId.Structural(BattlePlans.PlanKey), vtable, carrier),
                BattlePlans.PlanKey,
                BattlePlans.PlanDossiers(card, null)
            );
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

        /// <summary>What the player earned: the resources, the salvage and the experience, each with the
        /// itemised breakdown the game hangs on it.</summary>
        /// <summary>
        /// What the battle PAID the player - the three labels the report panel writes under the roster.
        ///
        /// Internal and prefixed because the ADVANCED report window binds the same
        /// <c>PlayerBattleGroupReportPanel</c> behind its own fleet switch
        /// (<see cref="AdvancedBattleReportScreen"/>), and the two windows read the same three labels
        /// the same way: one question, one home.
        /// </summary>
        internal static void Rewards(
            GraphBuilder builder,
            PlayerBattleGroupReportPanel panel,
            string prefix
        )
        {
            if (panel == null)
            {
                return;
            }

            try
            {
                BattleRows.Note(builder, panel.ResourcesEarnedTitleLabel, prefix + "/resources");
                BattleRows.Note(builder, panel.SalvageRescuedTitleLabel, prefix + "/salvage");
                BattleRows.Note(builder, panel.TotalExperienceTitleLabel, prefix + "/experience");
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

        // ---- the shapes a single widget takes ----

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

        /// <summary>What a control is called: the words the game drew on it where it drew any, else the
        /// game's own title for it.</summary>
        private static string Title(string titleKey, AgeTransform widget)
        {
            string drawn = AgeWidgets.TextOf(widget);
            return string.IsNullOrEmpty(drawn) ? AgeText.Clean(titleKey) : drawn;
        }

        /// <summary>A battle row (<see cref="BattleRows.Command"/>) under these popups' own naming
        /// rule.</summary>
        private static void Command(
            List<Cell> cells,
            AgeTransform widget,
            string titleKey,
            string key
        )
        {
            AgeTransform it = widget;
            BattleRows.Command(cells, widget, () => Title(titleKey, it), key);
        }

        /// <summary>A battle box (<see cref="BattleRows.Checkbox"/>) under these popups' own naming
        /// rule.</summary>
        private static void Checkbox(
            List<Cell> cells,
            AgeControlToggle toggle,
            string titleKey,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            BattleRows.Checkbox(cells, toggle, () => Title(titleKey, widget), key);
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

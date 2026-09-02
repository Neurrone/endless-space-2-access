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
    internal static partial class BattleNotifications
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
                // onto the title's tooltip; here the row went and got it, so here the row says it.
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

        /// <summary>
        /// What the battle PAID the player - the resources, the salvage and the experience, the three
        /// labels the report panel writes under the roster, each with the itemised breakdown the game
        /// hangs on it.
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

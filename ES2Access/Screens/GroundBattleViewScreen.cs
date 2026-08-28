using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The ground battle - the invasion animation, narrated. The game's own name for it is "Ground Battle
    /// View", and like the space cinematic it is a stream rather than a page: the whole battle is decided
    /// before the animation starts (the report is handed to the screen along with the battle), and what
    /// plays out is that report being acted.
    ///
    /// So everything here is said by the per-frame watch, in the same tiers as the space battle: the ACTS
    /// off the groups the screen shows and hides (the introduction naming the system and both sides with
    /// the strategy each picked, the conclusion carrying the game's own outcome title and the subtitle it
    /// keeps for what happens next), the PHASES off the viewer's own phase, the ROUNDS off its round
    /// counter, and the TROOPS as bursts - a round kills several units at once and the player needs "three
    /// of your infantry lost", not three lines.
    ///
    /// The counts come from the viewer's own answer for how many units of a kind are still alive, which is
    /// what the cells on screen are drawn from; the cells themselves draw a health bar and a percentage,
    /// so there is no number on screen to read. The first poll BASELINES rather than announcing, because
    /// the report exists in full before the animation starts and a watcher that read it as news would
    /// announce the whole battle at once.
    ///
    /// The round counter is the one thing the viewer keeps to itself, so it is read by reflection and the
    /// round tier simply goes quiet on a build that has renamed it.
    ///
    /// Keys are the game's throughout: Escape leaves the viewer, and slower, faster and reset are its own
    /// bindings on Minus, Plus and Asterisk - none of which the mod claims. The controls it draws are
    /// declared anyway, because they are drawn, and because the speed multiplier is written on the reset
    /// button and nowhere else.
    /// </summary>
    public sealed class GroundBattleViewScreen : Screen
    {
        private const float BurstSeconds = 0.75f;

        private const string SkipTitleKey = "%GroundBattleScreenSkipTitle";

        private const string ScreenNameKey = "screen.ground-battle";
        private const string RoundKey = "ground-battle.round";
        private const string BombardmentKey = "ground-battle.bombardment";
        private const string AssaultKey = "ground-battle.assault";
        private const string YourTroopLostKey = "ground-battle.your-troop-lost";
        private const string YourTroopsLostKey = "ground-battle.your-troops-lost";
        private const string EnemyTroopLostKey = "ground-battle.enemy-troop-lost";
        private const string EnemyTroopsLostKey = "ground-battle.enemy-troops-lost";

        private static readonly object ControlsStop = "ground-battle:controls";

        private readonly StepWatch _act = new StepWatch();
        private readonly StepWatch _phase = new StepWatch();
        private readonly StepWatch _round = new StepWatch();
        private readonly BurstWatch _yourTroops = new BurstWatch(BurstSeconds);
        private readonly BurstWatch _enemyTroops = new BurstWatch(BurstSeconds);
        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>Which battle is being watched, so the same one replayed is news again.</summary>
        private GroundBattleViewer _run;

        /// <summary>Whether the troop counts have been read once without announcing them - the report is
        /// complete before the animation starts, so the first read is history.</summary>
        private bool _baselined;

        public override string Key
        {
            get { return "screen.ground-battle"; }
        }

        /// <summary>One of the game's view levels over the map, like the space battle and the galaxy - no
        /// two of them are ever up together.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        /// <summary>Where the invasion is, in the game's own line for it.</summary>
        public override string ScreenName
        {
            get
            {
                try
                {
                    global::GroundBattleScreen window = Window();
                    string where = window == null
                        ? null
                        : AgeText.Label(window.IntroductionLocationLabel);
                    return string.IsNullOrEmpty(where)
                        ? BattleText.Optional(ScreenNameKey)
                        : where;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>Ours while the game says it is in a ground battle - one flag covering the whole mode,
        /// so nothing blinks through to the map underneath.</summary>
        public override bool IsActive()
        {
            try
            {
                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                return gui != null && gui.IsInGroundBattle;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: the screen is an input handler and answers Escape by leaving the
        /// viewer.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void OnPush()
        {
            Rearm();
        }

        public override void OnPop()
        {
            Rearm();
            _run = null;
        }

        public override void OnUpdate()
        {
            try
            {
                Narrate();
            }
            catch (Exception e)
            {
                Log.Warn("ground battle: narrating the invasion threw: " + e);
            }
        }

        private void Rearm()
        {
            _act.Forget();
            _phase.Forget();
            _round.Forget();
            _yourTroops.Reset();
            _enemyTroops.Reset();
            _baselined = false;
        }

        private void Narrate()
        {
            global::GroundBattleScreen window = Window();
            if (window == null)
            {
                return;
            }

            GroundBattleViewer viewer = Viewer();
            if (!ReferenceEquals(viewer, _run))
            {
                Rearm();
                _run = viewer;
            }

            Act(window);
            Phase(viewer);
            Round(viewer);
            Troops(window, viewer);
        }

        /// <summary>Which act the screen is showing: the introduction (where, who, and the strategy each
        /// side picked) or the conclusion (the game's own outcome title, and the subtitle saying whether
        /// the siege goes on). Both are groups the screen shows and hides.</summary>
        private void Act(global::GroundBattleScreen window)
        {
            if (Shown(window.ConclusionGroup))
            {
                Say(
                    2,
                    new MessageBuilder()
                        .ListItem(AgeText.Label(window.ConclusionEndStatusTitle))
                        .ListItem(Visible(window.ConclusionEndStatusSubTitle))
                        .Build()
                );
                return;
            }

            if (Shown(window.IntroductionGroup))
            {
                Say(
                    1,
                    new MessageBuilder()
                        .ListItem(AgeText.Label(window.IntroductionLocationLabel))
                        .ListItem(AgeText.Label(window.IntroductionLeftEmpireNameLabel))
                        .ListItem(AgeText.Label(window.IntroductionLeftStrategyNameLabel))
                        .ListItem(AgeText.Label(window.IntroductionRightEmpireNameLabel))
                        .ListItem(AgeText.Label(window.IntroductionRightStrategyNameLabel))
                        .Build()
                );
            }
        }

        private void Say(int act, string said)
        {
            if (!_act.IsNew(act) || string.IsNullOrEmpty(said))
            {
                return;
            }

            _act.Told(act);
            Voice.Say(said, false);
        }

        /// <summary>The two phases worth naming: the bombardment from orbit, and the assault on the
        /// ground. The others are the animation setting itself up and mean nothing to a listener.</summary>
        private void Phase(GroundBattleViewer viewer)
        {
            if (viewer == null)
            {
                return;
            }

            GroundBattleViewer.PhaseType phase;
            try
            {
                phase = viewer.CurrentPhase;
            }
            catch (Exception)
            {
                return;
            }

            int step = (int)phase;
            if (!_phase.IsNew(step))
            {
                return;
            }

            string said = null;
            if (phase == GroundBattleViewer.PhaseType.BombardmentPhase)
            {
                said = BattleText.Optional(BombardmentKey);
            }
            else if (phase == GroundBattleViewer.PhaseType.AttackPhase)
            {
                said = BattleText.Optional(AssaultKey);
            }

            _phase.Told(step);
            Voice.Say(said, false);
        }

        /// <summary>Which round of the assault is being fought. The viewer keeps the counter private, so a
        /// build that has renamed the field simply says nothing about rounds.</summary>
        private void Round(GroundBattleViewer viewer)
        {
            if (viewer == null || RoundIndex == null)
            {
                return;
            }

            int round;
            try
            {
                object value = RoundIndex.GetValue(viewer);
                round = value is int ? (int)value : -1;
            }
            catch (Exception)
            {
                return;
            }

            if (round < 0 || !_round.IsNew(round))
            {
                return;
            }

            string said = BattleText.Optional(RoundKey, round + 1);
            if (string.IsNullOrEmpty(said))
            {
                return;
            }

            _round.Told(round);
            Voice.Say(said, false);
        }

        /// <summary>
        /// The units that have died, a side at a time and a burst at a time.
        ///
        /// The cells the game draws are bound to a kind of troop and a side, and the viewer answers how
        /// many of that kind are still standing; how many there were is on the cell itself. So a death is
        /// a drop in that answer, and each drop is noted under an id of its own ("this side's third
        /// infantry") so the same drop is never counted twice and a replay counts it again.
        /// </summary>
        private void Troops(global::GroundBattleScreen window, GroundBattleViewer viewer)
        {
            if (viewer == null)
            {
                return;
            }

            bool baseline = !_baselined;
            _baselined = true;
            Count(window.LeftCard, viewer, _yourTroops, baseline);
            Count(window.RightCard, viewer, _enemyTroops, baseline);

            float now = Time.realtimeSinceStartup;
            Voice.Say(
                BattleText.Losses(_yourTroops.Due(now), YourTroopLostKey, YourTroopsLostKey),
                false
            );
            Voice.Say(
                BattleText.Losses(_enemyTroops.Due(now), EnemyTroopLostKey, EnemyTroopsLostKey),
                false
            );
        }

        private static void Count(
            GroundBattleViewerCard card,
            GroundBattleViewer viewer,
            BurstWatch watch,
            bool baseline
        )
        {
            AgeTransform table = card == null ? null : card.TroopsTable;
            if (table == null)
            {
                return;
            }

            try
            {
                float now = Time.realtimeSinceStartup;
                TroopCell[] cells = table.GetComponentsInChildren<TroopCell>(true);
                for (int i = 0; i < cells.Length; i++)
                {
                    TroopCell cell = cells[i];
                    GuiTroop troop = cell == null ? null : cell.GuiTroop;
                    if (troop == null || cell.MaxTroopCount <= 0)
                    {
                        continue;
                    }

                    GroundBattleOpponent side = troop.IsAttacker
                        ? GroundBattleOpponent.Attacker
                        : GroundBattleOpponent.Defender;
                    int alive = viewer.GetAliveReportUnitCount(side, troop.TroopType);
                    string name = AgeText.Clean(troop.Title);
                    for (int unit = alive + 1; unit <= cell.MaxTroopCount; unit++)
                    {
                        watch.Note(side + "/" + troop.TroopType + "/" + unit, name, now);
                    }
                }

                if (baseline)
                {
                    // Read once and said nothing: the report is complete before the animation starts, so
                    // whatever it already holds is history rather than news.
                    watch.Due(float.MaxValue);
                }
            }
            catch (Exception e)
            {
                Log.Warn("ground battle: counting the troops threw: " + e);
            }
        }

        /// <summary>What the screen lets a watcher do: skip the animation, and change how fast it plays.
        /// The speed controls already have keys of the game's own, and are declared because the multiplier
        /// they show is written nowhere else.</summary>
        public override void Build(GraphBuilder builder)
        {
            global::GroundBattleScreen window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                _cells.Clear();
                Command(window.SkipButton, SkipTitleKey, "ground-battle:skip");
                Command(window.SlowerButtonAgeTransform, null, "ground-battle:slower");
                Speed(window.ResetButtonLabel, "ground-battle:reset");
                Command(window.FasterButtonAgeTransform, null, "ground-battle:faster");
                if (_cells.Count > 0)
                {
                    builder.BeginStop(ControlsStop);
                    Cells.EmitLinear(builder, _cells);
                }
            }
            catch (Exception e)
            {
                Log.Warn("ground battle: reading the controls threw: " + e);
            }
        }

        /// <summary>A button the screen drew as an icon: the game's own title where it has one, else the
        /// sentence it explains itself with, which is what a sighted player reads on hover.</summary>
        private void Command(AgeTransform widget, string titleKey, string key)
        {
            if (widget == null)
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            string named = string.IsNullOrEmpty(titleKey) ? null : AgeText.Clean(titleKey);
            NodeVtable vtable = GraphNodes.Button(
                () => named ?? CardActions.FirstLine(tooltip),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Offered(it),
                tooltip
            );
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(_cells, widget, ControlId.For(widget, key), vtable);
        }

        /// <summary>The reset button, which is also the only place the speed multiplier is written: the
        /// button's own words are the value, so it is named by what it explains itself with.</summary>
        private void Speed(AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            AgeControl control = AgeWidgets.ParentControl(widget);
            AgeTransform button = control == null ? widget : control.AgeTransform;
            // Different widget: the node stands on the BUTTON around the label, and this asks about the
            // label inside it - which the gate's walk up the ancestry never reaches.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            AgeTransform pressed = button;
            AgeTooltip tooltip = AgeWidgets.Raw(button);
            NodeVtable vtable = GraphNodes.Button(
                () => CardActions.FirstLine(tooltip),
                () => AgeWidgets.Press(pressed),
                () => AgeWidgets.Offered(pressed),
                tooltip
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => AgeText.Label(it), false));
            AgeWidgets.PointAt(vtable, button);
            Cells.Add(_cells, button, ControlId.For(label, key), vtable);
        }

        /// <summary>What a label SAYS while it is drawn - a string, not a node.</summary>
        private static string Visible(AgePrimitiveLabel label)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            return AgeWidgets.Visible(widget) ? AgeText.Label(label) : null;
        }

        /// <summary>Whether a phase's whole band is being drawn - flow control for the readings under
        /// it, each of which walks a panel of its own.</summary>
        private static bool Shown(AgeTransform group)
        {
            return group != null && AgeWidgets.Visible(group);
        }

        private static GroundBattleViewer Viewer()
        {
            try
            {
                return GroundBattleViewer.Instance;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static global::GroundBattleScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<global::GroundBattleScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The viewer's own round counter, which it keeps private - the only thing about a ground
        /// battle this screen cannot ask for.</summary>
        private static readonly FieldInfo RoundIndex = Field("currentRoundIndex");

        private static FieldInfo Field(string name)
        {
            try
            {
                return typeof(GroundBattleViewer).GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
            }
            catch (Exception e)
            {
                Log.Warn("ground battle: looking up " + name + " threw: " + e);
                return null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The advanced battle setup - the window the setup popup's Advanced button opens, where a fight is
    /// planned rather than merely accepted (<c>AdvancedEncounterPlayModalWindow</c>).
    ///
    /// The setup popup offers one decision (which battle plan) as a carousel. This window is the same
    /// decision laid out as a HAND: every plan the empire has as a card of its own, the three plans this
    /// opponent has used against you recently, both sides' fleets in the flotillas they will fight in, and
    /// the sorting that decides which ship goes in which flotilla. It is where a player who wants to win a
    /// battle rather than watch one spends their time, and it is drawn almost entirely as pictures.
    ///
    /// It is walked SIDE BY SIDE like the battle popups: your leader, your plans and your flotillas are one
    /// region, theirs another, the figures a third, the controls a fourth. That is a deviation from the
    /// drawn layout - the two decks are drawn opposite each other and the sorting sits between them - and
    /// the same one the popups make, for the same reason: the player is comparing two sides.
    ///
    /// Your plans are RADIOS: each card's Enter is the card's own click, which is what tells the window
    /// which plan is in force (<c>OnClickPlayCb</c>, which ignores a click on the plan already chosen, so
    /// nothing here can be double-committed). There is no confirm - the plan takes effect as it is picked,
    /// and Start Battle is what commits the whole setup.
    ///
    /// Their three cards are READOUTS rather than radios, which is a deliberate departure from click
    /// parity: the game wires every play card the same way, so a click on one of THEIR cards runs
    /// <c>OnClickPlayCb</c> too - and that handler sets the PLAYER's plan to whichever card was clicked
    /// (<c>PlayerEncounterGroup.SetSetupPlay</c>). Reproducing that would let a player's Enter silently
    /// replace their battle plan with the enemy's while reading what the enemy tends to do. The cards are
    /// what they look like - a record of the opponent's habits - so they read as such.
    /// OWNER-RATIFIED (2026-08-12): a click that is a game bug is not given a key. Do not restore
    /// parity here; if the game ever fixes the handler to ignore enemy-card clicks, these can become
    /// plain refused controls instead.
    ///
    /// The sorting buttons and the stats switches are declared with the words the game explains them with,
    /// because it draws them as icons and gives them no titles at all (its localization has a description
    /// per button and no title). The rosters follow the two fleet switches: the game slides a roster panel
    /// out, so what is declared is what is DRAWN.
    ///
    /// What is NOT modelled, and is deferred: the damage and range figures behind the stats switches. Those
    /// gauges are coloured arcs with no number anywhere on them, and unlike the military-power pair - which
    /// the game's own helper computes and this screen reads (<see cref="BattleNotifications.BalanceText"/>)
    /// - the damage and range numbers are worked out inside the window from every module of every ship. The
    /// mod would have to reimplement that arithmetic, and a wrong number here is worse than no number: it
    /// is the figure a player decides whether to fight on.
    ///
    /// Escape is the game's, and it is not a plain close: <c>HandleInput</c> puts the battle-setup
    /// notification back up, which is where the player came from and where the fight is actually started.
    /// </summary>
    public sealed class AdvancedEncounterPlayScreen : Screen
    {
        private static readonly object HeadingStop = "advanced-play:heading";
        private static readonly object PlansStop = "advanced-play:plans";
        private static readonly object StatsStop = "advanced-play:stats";
        private static readonly object ControlsStop = "advanced-play:controls";

        private static readonly object YoursRegion = "advanced-play:yours";
        private static readonly object TheirsRegion = "advanced-play:theirs";
        private static readonly object FiguresRegion = "advanced-play:figures";

        /// <summary>The game's own titles for the things it draws as pictures.</summary>
        private const string PlanTitleKey = "%NotificationBattleSetupSelectedPlayTitle";
        private const string StartTitleKey = "%NotificationBattleSetupStartButtonTitle";
        private const string RetreatTitleKey = "%NotificationBattleSetupRetreatButtonTitle";
        private const string WatchToggleTitleKey = "%NotificationBattleSetupWatchToggleTitle";

        /// <summary>The mod's own, for the four things the game names nowhere. All four are asked for
        /// optionally: a build without the phrase leaves that line out rather than reading a key.</summary>
        private const string YourFleetsKey = "battle.your-fleets";
        private const string EnemyFleetsKey = "battle.enemy-fleets";
        private const string ShowYourFleetsKey = "battle.show-your-fleets";
        private const string ShowEnemyFleetsKey = "battle.show-enemy-fleets";
        private const string TimeLeftKey = "battle.time-left";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.advanced-battle-setup"; }
        }

        /// <summary>Over the battle-setup notification it is opened from and returns to, and under the
        /// message box.</summary>
        public override int Layer
        {
            get { return 48; }
        }

        /// <summary>What the window has written across its top.</summary>
        public override string ScreenName
        {
            get
            {
                try
                {
                    AdvancedEncounterPlayModalWindow window = Window();
                    return window == null ? null : AgeText.Label(window.WindowTitleLabel);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>The plans, because picking one is what the window is for.</summary>
        public override object InitialFocusStop
        {
            get { return PlansStop; }
        }

        public override bool IsActive()
        {
            try
            {
                AdvancedEncounterPlayModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: Exit puts the setup popup back, which is somewhere to go rather than
        /// nowhere.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            AdvancedEncounterPlayModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                builder.BeginStop(HeadingStop);
                Heading(builder, window);

                builder.BeginStop(PlansStop);
                Yours(builder, window);
                Theirs(builder, window);
                builder.SetRegion(null);

                builder.BeginStop(StatsStop);
                Figures(builder, window);

                builder.BeginStop(ControlsStop);
                Controls(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: reading the window threw: " + e);
            }
        }

        /// <summary>Where the battle will be fought and what that does to it: the system, the arena, and
        /// the citadel line the game only draws for an orbit one is guarding.</summary>
        private void Heading(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            Note(builder, window.LocationLabel, "advanced-play/location");
            Note(builder, window.ArenaNameLabel, "advanced-play/arena");
            Note(builder, window.ProtectedByCitadelLabel, "advanced-play/citadel");
        }

        /// <summary>Your side: who is leading it, every plan you could pick, and - while the switch has it
        /// out - the flotillas the ships are arranged into.</summary>
        private void Yours(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            builder.SetRegion(YoursRegion);
            bool named = Context(builder, YourFleetsKey);
            try
            {
                Leader(builder, window.PlayerBattleGroupInfoPanel, "advanced-play/yours");
                Plans(builder, window.PlayerPlaySelectionTable, "advanced-play/plan");
                Roster(builder, window.PlayerBattleGroupSetupPanel, "advanced-play/yours");
            }
            finally
            {
                Close(builder, named);
            }
        }

        /// <summary>Their side: who is leading it, what they have played against you recently, and - while
        /// the switch has it out - what they have brought.</summary>
        private void Theirs(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            builder.SetRegion(TheirsRegion);
            bool named = Context(builder, EnemyFleetsKey);
            try
            {
                Leader(builder, window.EnemyBattleGroupInfoPanel, "advanced-play/theirs");
                if (AgeWidgets.Visible(window.EnemyDeckGroup))
                {
                    History(builder, window.EnemyPlaySelectionTable, "advanced-play/their-plan");
                }

                Roster(builder, window.EnemyBattleGroupSetupPanel, "advanced-play/theirs");
            }
            finally
            {
                Close(builder, named);
            }
        }

        /// <summary>
        /// Every plan the empire has, as the one-of-N the window made them.
        ///
        /// Keyed on the card's POSITION rather than on the card object: the table pools its cards and
        /// re-binds them by index for each battle, so a cursor keyed on the widget would be standing on a
        /// different plan the next time a fight starts.
        /// </summary>
        private void Plans(GraphBuilder builder, AgeTransform table, string prefix)
        {
            _cells.Clear();
            IList<AgeTransform> children = Children(table);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                BattlePlayCard card = Card(widget);
                if (card == null || !AgeWidgets.Visible(widget) || card.Toggle == null)
                {
                    continue;
                }

                BattlePlayCard it = card;
                AgeTransform at = widget;
                AgeTooltip tooltip = card.Tooltip;
                NodeVtable vtable = GraphNodes.Radio(
                    () => PlanName(it),
                    () => it.Toggle != null && it.Toggle.State,
                    () => AgeWidgets.Toggle(it.Toggle),
                    () => AgeWidgets.Operable(at),
                    null,
                    tooltip
                );
                GraphNodes.AddRefusal(vtable, tooltip, () => AgeWidgets.Operable(at));
                AgeWidgets.Point(vtable, it.Toggle, tooltip, at);
                Cells.Add(_cells, widget, ControlId.Structural(prefix + "/" + i), vtable);
            }

            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>What the opponent has played against you recently - a record, not a choice (see the
        /// class comment). Each says which plan it was and carries the game's own card tooltip, which is
        /// where the effects and the times-used count are written.</summary>
        private void History(GraphBuilder builder, AgeTransform table, string prefix)
        {
            _cells.Clear();
            IList<AgeTransform> children = Children(table);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                BattlePlayCard card = Card(widget);
                if (card == null || !AgeWidgets.Visible(widget))
                {
                    continue;
                }

                BattlePlayCard it = card;
                NodeVtable vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeText.Clean(PlanTitleKey)),
                        GraphNodes.ValuePart(() => PlanName(it), false),
                    },
                    Sections = GraphNodes.Sections(null, card.Tooltip),
                };
                AgeWidgets.PointAt(vtable, widget);
                Cells.Add(_cells, widget, ControlId.Structural(prefix + "/" + i), vtable);
            }

            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>What a plan is called: the game's own title for it, else the words the card drew (which
        /// it wraps to fit its box).</summary>
        private static string PlanName(BattlePlayCard card)
        {
            try
            {
                return AgeText.Label(card.PlayTitle);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One side's fleets, in the flotillas they will fight in, while the switch has the panel
        /// out. The shared roster reading answers the whole of it.</summary>
        private static void Roster(
            GraphBuilder builder,
            BattleGroupSetupPanel panel,
            string prefix
        )
        {
            AgeTransform widget = Widget(panel);
            if (AgeWidgets.Visible(widget))
            {
                BattleRosters.Roster(builder, widget, prefix);
            }
        }

        /// <summary>
        /// The figures: which stat page is showing, whatever that page wrote, and the balance of power.
        ///
        /// The switches are a one-of-N the window keeps exclusive itself (<c>OnChangeDisplayedStatsCb</c>
        /// unticks the others), and the game names them only in the sentences it explains them with.
        /// </summary>
        private void Figures(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            builder.SetRegion(FiguresRegion);
            _cells.Clear();
            AgeControlToggle[] toggles = window.StatsToggles;
            for (int i = 0; toggles != null && i < toggles.Length; i++)
            {
                Choice(toggles[i], "advanced-play:stat/" + i);
            }

            Cells.EmitLinear(builder, _cells);

            _cells.Clear();
            AgeTransform[] panels = window.StatsPanels;
            for (int i = 0; panels != null && i < panels.Length; i++)
            {
                if (AgeWidgets.Visible(panels[i]))
                {
                    WindowShape.Readouts(_cells, panels[i], "advanced-play:stat-panel/" + i);
                }
            }

            Cells.EmitLinear(builder, _cells);

            Balance(builder, window);
            Note(builder, window.FightersStanceRatioLabel, "advanced-play/fighters");
            builder.SetRegion(null);
        }

        /// <summary>The two military-power figures the arcs are sized from, which the game writes nowhere
        /// at all. The same reading the battle popups use, off the game's own helper.</summary>
        private static void Balance(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            AgeTransform gauge = Widget(window.BattlePowerGauge);
            if (!AgeWidgets.Visible(gauge))
            {
                return;
            }

            AdvancedEncounterPlayModalWindow it = window;
            if (BalanceText(it) == null)
            {
                return;
            }

            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => BalanceText(it)),
                },
                Sections = GraphNodes.Sections(null, AgeWidgets.Raw(gauge)),
            };
            AgeWidgets.PointAt(vtable, gauge);
            builder.AddItem(ControlId.Structural("advanced-play/balance"), vtable);
        }

        private static string BalanceText(AdvancedEncounterPlayModalWindow window)
        {
            try
            {
                return BattleNotifications.BalanceText(
                    window.PlayerEncounterGroup,
                    window.EnemyEncounterGroup,
                    true
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Everything the player works: the two roster switches, the sorting, whether to watch, and the
        /// three ways this window can end.
        ///
        /// The sorting buttons and the way out are read off what the window drew
        /// (<see cref="Cells.AddControl"/> names a wordless button by the sentence its tooltip opens with),
        /// which is the only name the game has for them. The sorting band as a whole is switched off while
        /// there is only one flotilla to sort (<c>SortingButtonsGroup.Enable</c>), and each button reads
        /// that as being unavailable.
        /// </summary>
        private void Controls(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            _cells.Clear();
            Checkbox(window.ShowYourFleetsToggle, ShowYourFleetsKey, null, "advanced-play:show-yours");
            Checkbox(
                window.ShowEnemyFleetsToggle,
                ShowEnemyFleetsKey,
                null,
                "advanced-play:show-theirs"
            );
            Sorting(window);
            Checkbox(window.WatchBattleToggle, null, WatchToggleTitleKey, "advanced-play:watch");
            Command(window.StartBattleButton, StartTitleKey, "advanced-play:start");
            Command(window.RetreatButton, RetreatTitleKey, "advanced-play:retreat");
            Countdown(window, "advanced-play:timer");
            Cells.AddControl(_cells, ByHandler(window, "OnBackCb"), "advanced-play:back");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The three ways the window can distribute ships between flotillas. Each is a wordless
        /// icon the game explains in a sentence, which is the name it gets.</summary>
        private void Sorting(AdvancedEncounterPlayModalWindow window)
        {
            Cells.AddControl(_cells, window.BestRangeSortingButton, "advanced-play:sort-best-range");
            Cells.AddControl(_cells, window.OptimalSortingButton, "advanced-play:sort-optimal");
            Cells.AddControl(_cells, window.BalancedSortingButton, "advanced-play:sort-balanced");
            // The one that puts the player's own arrangement back has no field on the window, so it is
            // found the same way the way out is.
            Cells.AddControl(
                _cells,
                ByHandler(window, "OnResetSortingCb"),
                "advanced-play:sort-reset"
            );
        }

        /// <summary>One of the stat pages, as the one-of-N the window keeps exclusive itself. Named by the
        /// sentence its own tooltip opens with: the game draws these as icons and its localization has a
        /// description per switch and no title for any of them.</summary>
        private void Choice(AgeControlToggle toggle, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (toggle == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Radio(
                Name(widget, null, null, tooltip),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Offered(widget),
                null,
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, widget);
            Cells.Add(_cells, widget, ControlId.Referenced(toggle, key), vtable);
        }

        /// <summary>A box the player ticks, named by the mod where the game names it nowhere and by the
        /// game everywhere else.</summary>
        private void Checkbox(
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
                Name(widget, modKey, gameKey, null),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Offered(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, widget);
            Cells.Add(_cells, widget, ControlId.Referenced(toggle, key), vtable);
        }

        /// <summary>A button the window drew as an icon, under the game's own title for it, refusing with
        /// the game's own reason - the retreat button carries the failure infos for a fleet that cannot
        /// run.</summary>
        private void Command(AgeTransform widget, string titleKey, string key)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Offered(it);
            NodeVtable vtable = GraphNodes.Button(
                Name(it, null, titleKey, tooltip),
                () => AgeWidgets.Press(it),
                enabled,
                tooltip
            );
            GraphNodes.AddRefusal(vtable, tooltip, enabled);
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(_cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        /// <summary>How long is left, for a battle the game is timing. Never watched - a countdown that
        /// announced itself under a standing cursor would talk over the plan the player is choosing - so it
        /// is there to be asked.</summary>
        private void Countdown(AdvancedEncounterPlayModalWindow window, string key)
        {
            AgeTransform gauge = window.TimerGauge;
            NotificationBattleSetup notification = window.NotificationBattleSetup;
            if (
                !AgeWidgets.Visible(gauge)
                || notification == null
                || OptionalText.Phrase(TimeLeftKey, 0) == null
            )
            {
                return;
            }

            NotificationBattleSetup it = notification;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => TimeLeft(it)),
                },
                OnFocusVisual = AgeWidgets.ReleasePointer,
            };
            Cells.Add(_cells, gauge, ControlId.Structural(key), vtable);
        }

        private static string TimeLeft(NotificationBattleSetup notification)
        {
            try
            {
                return OptionalText.Phrase(
                    TimeLeftKey,
                    UnityEngine.Mathf.Clamp(
                        UnityEngine.Mathf.RoundToInt(notification.GetTimeLeftRatio() * 100f),
                        0,
                        100
                    )
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A button the window keeps in no field of its own - the way out, and the reset beside
        /// the sorting: found by the HANDLER it is wired to rather than by a name in the prefab, because the
        /// handler is in the window's own code and a prefab name is a guess.</summary>
        private static AgeTransform ByHandler(
            AdvancedEncounterPlayModalWindow window,
            string method
        )
        {
            try
            {
                AgeControlButton[] buttons =
                    window.AgeTransform.GetComponentsInChildren<AgeControlButton>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    AgeControlButton button = buttons[i];
                    if (
                        button != null
                        && button.OnActivateMethod == method
                        && AgeWidgets.Visible(button.AgeTransform)
                    )
                    {
                        return button.AgeTransform;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: looking for " + method + " threw: " + e);
            }

            return null;
        }

        /// <summary>Who is leading this side, and the hero commanding it where there is one - the portrait
        /// carries the hero's whole dossier, so the row indicates having one and the buffer holds it.
        /// </summary>
        private static void Leader(GraphBuilder builder, BattleGroupInfoPanel panel, string prefix)
        {
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            Note(builder, panel.MainLeaderName, prefix + "/leader");
            AgePrimitiveImage portrait = panel.MainHeroPortrait;
            AgeTransform widget = portrait == null ? null : portrait.AgeTransform;
            if (!AgeWidgets.Visible(widget))
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

        /// <summary>A line the game wrote and is showing: read as it stands, with whatever it explains
        /// itself with carried along.</summary>
        private static void Note(GraphBuilder builder, AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (!AgeWidgets.Visible(widget) || string.IsNullOrEmpty(AgeText.Label(label)))
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

        /// <summary>What a control is called: the words the game drew on it, else the game's own title for
        /// it, else the sentence its tooltip opens with, else the mod's own word - in that order, because
        /// every step of it is the game's voice and the last one is a last resort.</summary>
        private static Func<string> Name(
            AgeTransform widget,
            string modKey,
            string gameKey,
            AgeTooltip tooltip
        )
        {
            AgeTransform at = widget;
            AgeTooltip tip = tooltip;
            return () =>
            {
                string drawn = AgeWidgets.TextOf(at);
                if (!string.IsNullOrEmpty(drawn))
                {
                    return drawn;
                }

                string game = string.IsNullOrEmpty(gameKey) ? null : AgeText.Clean(gameKey);
                if (!string.IsNullOrEmpty(game))
                {
                    return game;
                }

                string hinted = CardActions.FirstLine(tip);
                return string.IsNullOrEmpty(hinted) ? OptionalText.Phrase(modKey) : hinted;
            };
        }

        /// <summary>Which side the rows that follow belong to - announced once as focus enters, so a
        /// roster is audibly yours or theirs without every row saying so. A build with no such phrase
        /// opens no level at all, which is why every caller closes with <see cref="Close"/>.</summary>
        private static bool Context(GraphBuilder builder, string nameKey)
        {
            string name = OptionalText.Phrase(nameKey);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            builder.PushContext(name, null, false);
            return true;
        }

        private static void Close(GraphBuilder builder, bool opened)
        {
            if (opened)
            {
                builder.PopContext();
            }
        }

        private static BattlePlayCard Card(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<BattlePlayCard>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IList<AgeTransform> Children(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Children;
            }
            catch (Exception)
            {
                return null;
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

        private static AgeTransform Widget(BattlePowerGauge gauge)
        {
            try
            {
                return gauge == null ? null : gauge.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AdvancedEncounterPlayModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<AdvancedEncounterPlayModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

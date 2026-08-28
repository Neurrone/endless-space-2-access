using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The space battle itself - the cinematic, narrated.
    ///
    /// This is a stream rather than a page: the whole battle is computed on the server and sent as a
    /// report of timestamped instructions, and what the player is watching is that report being replayed
    /// against the model. So the model MOVES while nothing is focused, and everything this screen says
    /// comes from its per-frame watch rather than from a cursor - the same shape as the loading screen and
    /// the system-discovery cutscene.
    ///
    /// Four tiers, loudest first.
    ///
    /// The PRE-ROLL GATE is the one that matters most, and it is not narration at all - it is a soft-lock.
    /// When the battle has finished loading the game stops and waits for a keypress it reads RAW, outside
    /// its own input system, and the only thing on screen saying so is a caption on the loading window.
    /// A player who cannot see that caption sits in front of a frozen game with no idea that space or a
    /// click will start it. So the caption is read the moment it appears, once.
    ///
    /// The ACTS come from the display mode the stream itself queues: the introduction names where this is
    /// and who it is against, the main act opens with the balance of power, and the outcome is the game's
    /// own word for how it went. The PHASES come from the encounter's own phase index, each with the
    /// balance again, because how the balance MOVED across a phase is the thing a sighted player is
    /// reading off the two arcs.
    ///
    /// LOSSES are the noisy tier and are aggregated (<see cref="BurstWatch"/>): a salvo takes several
    /// ships inside a second, and the player needs "four of your ships lost" rather than four names
    /// arriving over the top of the next salvo. The watermark is PER RUN, not per ship: asking to watch
    /// the battle again resets the model and plays the same stream over, which this notices as the clock
    /// jumping backwards and treats as news.
    ///
    /// PROGRESS is quantised to quarters and only ever announced upward, so a long battle says something
    /// occasionally without narrating a percentage.
    ///
    /// Nothing here is a control the player works except the three the game draws: skip to the action, the
    /// three camera modes, and the battle lens. No key of the mod's is bound here, and the game keeps
    /// every key it has - Escape stops watching, and pause, faster, slower and reset are its own bindings
    /// on Pause, Plus, Minus and Asterisk, none of which the mod claims.
    /// </summary>
    public sealed class SpaceBattleScreen : Screen
    {
        /// <summary>How long a burst of losses gathers before it is reported as one line. Long enough
        /// that a salvo is one utterance, short enough that the report still lands while the wreckage is
        /// on screen.</summary>
        private const float BurstSeconds = 0.75f;

        /// <summary>How many quarters the battle's progress is reported in.</summary>
        private const int Milestones = 4;

        /// <summary>A jump backwards in the battle clock this large means the stream has been rewound -
        /// the player asked to watch it again - rather than a frame arriving out of order. The game sets
        /// the clock to -0.5 to start a re-watch.</summary>
        private const double Rewound = 0.25;

        private const string PhaseTitleKey = "%AdvancedReportModalWindowPhaseTitle";
        private const string FlotillaNameKey = "%FlotillaNameTitle";
        private const string SkipTitleKey = "%BattleScreenSkipPhaseTitle";
        private const string ScanTitleKey = "%BattleScreenScanTitle";
        private static readonly string[] CameraTitleKeys = new string[]
        {
            "%BattleScreenAutoCameraTitle",
            "%BattleScreenFreeCameraTitle",
            "%BattleScreenOverviewCameraTitle",
        };

        private const string ScreenNameKey = "screen.battle";
        private const string ProgressKey = "battle.progress";
        private const string YourShipLostKey = "battle.your-ship-lost";
        private const string YourShipsLostKey = "battle.your-ships-lost";
        private const string EnemyShipLostKey = "battle.enemy-ship-lost";
        private const string EnemyShipsLostKey = "battle.enemy-ships-lost";
        private const string YourFlotillaLostKey = "battle.your-flotilla-lost";
        private const string YourFlotillasLostKey = "battle.your-flotillas-lost";
        private const string EnemyFlotillaLostKey = "battle.enemy-flotilla-lost";
        private const string EnemyFlotillasLostKey = "battle.enemy-flotillas-lost";

        private static readonly object ControlsStop = "battle:controls";

        private readonly StepWatch _act = new StepWatch();
        private readonly StepWatch _phase = new StepWatch();
        private readonly BurstWatch _yourShips = new BurstWatch(BurstSeconds);
        private readonly BurstWatch _enemyShips = new BurstWatch(BurstSeconds);
        private readonly BurstWatch _yourFlotillas = new BurstWatch(BurstSeconds);
        private readonly BurstWatch _enemyFlotillas = new BurstWatch(BurstSeconds);
        private readonly List<Cell> _cells = new List<Cell>();

        private GalaxyEncounter _run;
        private double _clock;
        private int _milestone;
        private bool _launchSaid;
        private string _caption;

        public override string Key
        {
            get { return "screen.battle"; }
        }

        /// <summary>The same layer as the galaxy and the system pages: a battle is one of the game's view
        /// levels over the one map, and no two of them are ever up together. The battle lens sits just
        /// above it, and every popup well above that.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        /// <summary>Where this is and who it is against, in the game's own two lines - which it only
        /// writes while the introduction is on screen, so the mod's own word covers the rest of the
        /// fight.</summary>
        public override string ScreenName
        {
            get
            {
                try
                {
                    global::BattleScreen window = Window();
                    string where = window == null ? null : AgeText.Label(window.LocationTitle);
                    string who = window == null ? null : AgeText.Label(window.OpponentTitle);
                    string named = new MessageBuilder().ListItem(where).ListItem(who).Build();
                    return string.IsNullOrEmpty(named)
                        ? BattleText.Optional(ScreenNameKey)
                        : named;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Ours from the moment the game says it is in a battle until it says it is not.
        ///
        /// The game's own answer covers the loading window, the cinematic and the fade out of it as one
        /// mode (<c>GuiManager.IsInBattle</c>), which is exactly the span this screen has something to
        /// say about - and because it is one flag rather than a window's visibility, there is no blink
        /// between the loading window going and the battle screen arriving for the galaxy underneath to
        /// announce itself into.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                return gui != null && gui.IsInBattle;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: the battle screen is an input handler and answers Escape by stopping
        /// watching, which resolves the rest of the fight instantly.</summary>
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
                Log.Warn("battle: narrating the cinematic threw: " + e);
            }
        }

        /// <summary>Everything this screen has said is forgotten - a fresh battle, or the same one
        /// watched again.</summary>
        private void Rearm()
        {
            _act.Forget();
            _phase.Forget();
            _yourShips.Reset();
            _enemyShips.Reset();
            _yourFlotillas.Reset();
            _enemyFlotillas.Reset();
            _clock = 0.0;
            _milestone = 0;
            _launchSaid = false;
            _caption = null;
        }

        private void Narrate()
        {
            global::BattleScreen window = Window();
            BattleLoadingWindow loading = Loading();
            GalaxyEncounter encounter = Encounter(window, loading);
            if (encounter == null)
            {
                return;
            }

            // A fresh battle, or the same one rewound to be watched again: everything is news.
            double clock = Clock(encounter);
            if (!ReferenceEquals(encounter, _run) || clock < _clock - Rewound)
            {
                Rearm();
                _run = encounter;
            }

            _clock = clock;

            Launch(encounter, loading);
            Act(window, encounter);
            Phase(encounter);
            Losses(window, encounter);
            Flotillas(encounter);
            Progress(encounter);
        }

        /// <summary>
        /// The gate. While the game is waiting for a key it reads raw, the caption saying so is read
        /// once; every other caption the loading window writes is read as it changes, which is the
        /// loading screen's rule and for the same reason - a wait nobody narrates is a wait that looks
        /// like a hang.
        /// </summary>
        private void Launch(GalaxyEncounter encounter, BattleLoadingWindow loading)
        {
            string caption = loading == null ? null : AgeText.Label(loading.Caption);
            if (string.IsNullOrEmpty(caption))
            {
                return;
            }

            bool waiting =
                State(encounter) == GalaxyEncounter.GalaxyEncounterState.LoadingWaitForPlayer;
            if (waiting)
            {
                if (!_launchSaid)
                {
                    _launchSaid = true;
                    _caption = caption;
                    Voice.Say(caption, false);
                }

                return;
            }

            if (caption != _caption)
            {
                _caption = caption;
                Voice.Say(caption, false);
            }
        }

        /// <summary>Which act of the cinematic is running, and what that act is ABOUT: where and against
        /// whom, then the balance the fight opens on, then the game's own word for how it ended. The
        /// watermark moves only when something was actually said, so an act whose labels the game has not
        /// written yet is announced on the frame it writes them.</summary>
        private void Act(global::BattleScreen window, GalaxyEncounter encounter)
        {
            if (window == null)
            {
                return;
            }

            global::BattleScreen.BattleDisplayMode mode;
            try
            {
                mode = window.CurrentMode;
            }
            catch (Exception)
            {
                return;
            }

            int act = (int)mode;
            if (!_act.IsNew(act))
            {
                return;
            }

            string said = null;
            switch (mode)
            {
                case global::BattleScreen.BattleDisplayMode.Introduction:
                    said = new MessageBuilder()
                        .ListItem(AgeText.Label(window.LocationTitle))
                        .ListItem(AgeText.Label(window.OpponentTitle))
                        .Build();
                    break;
                case global::BattleScreen.BattleDisplayMode.Main:
                    said = Balance(encounter);
                    break;
                case global::BattleScreen.BattleDisplayMode.Outcome:
                    said = AgeText.Label(window.OutcomeValue);
                    break;
            }

            if (string.IsNullOrEmpty(said))
            {
                // Either an act with nothing to say (the ends of each one) or one whose words are not
                // written yet. The first is settled by recording it; the second must be retried, and the
                // only one that can be told apart is the empty-by-design case - so a mode with no text is
                // recorded and a mode that should have text is not.
                if (mode != global::BattleScreen.BattleDisplayMode.Introduction
                    && mode != global::BattleScreen.BattleDisplayMode.Outcome)
                {
                    _act.Told(act);
                }

                return;
            }

            _act.Told(act);
            Voice.Say(said, false);
        }

        /// <summary>Which phase the battle has reached, in the game's own numbering, with the balance as
        /// it stands - the pair a sighted player reads off the phase strip and the arcs.</summary>
        private void Phase(GalaxyEncounter encounter)
        {
            Encounter battle = Battle(encounter);
            if (battle == null)
            {
                return;
            }

            int index;
            try
            {
                index = battle.CurrentPhaseIndex;
            }
            catch (Exception)
            {
                return;
            }

            if (!_phase.IsNew(index))
            {
                return;
            }

            string phase = AgeText.Clean(
                Gui.Localize(PhaseTitleKey, AgeUtils.ToRoman(index + 1))
            );
            if (string.IsNullOrEmpty(phase))
            {
                return;
            }

            _phase.Told(index);
            Voice.Say(new MessageBuilder().ListItem(phase).ListItem(Balance(encounter)).Build(), false);
        }

        /// <summary>
        /// The ships that have gone, a side at a time and a burst at a time.
        ///
        /// Read off the same items the battle's own state panels are built from, which is where the game
        /// keeps its per-ship view of the fight: the wrapper on each item answers destroyed and kamikazed
        /// the way the panel asks it. The LEFT panel is always the player's side - the battle screen binds
        /// it to the player's own group - which is what makes "yours" and "theirs" answerable at all.
        /// </summary>
        private void Losses(global::BattleScreen window, GalaxyEncounter encounter)
        {
            if (window != null)
            {
                Lost(window.BattleStateGroupPanelLeft, _yourShips);
                Lost(window.BattleStateGroupPanelRight, _enemyShips);
            }

            float now = Time.realtimeSinceStartup;
            Voice.Say(
                BattleText.Losses(_yourShips.Due(now), YourShipLostKey, YourShipsLostKey),
                false
            );
            Voice.Say(
                BattleText.Losses(_enemyShips.Due(now), EnemyShipLostKey, EnemyShipsLostKey),
                false
            );
        }

        private static void Lost(BattleStateGroupPanel panel, BurstWatch watch)
        {
            AgeTransform root = panel == null ? null : panel.AgeTransform;
            if (root == null)
            {
                return;
            }

            try
            {
                float now = Time.realtimeSinceStartup;
                BattleStateShipItem[] items = root.GetComponentsInChildren<BattleStateShipItem>(true);
                for (int i = 0; i < items.Length; i++)
                {
                    GuiBattleShip ship = items[i] == null ? null : items[i].GuiBattleShip;
                    if (ship == null || !(ship.IsDestroyed || ship.HasKamikazed))
                    {
                        continue;
                    }

                    watch.Note(ship.GUID.ToString(), AgeText.Clean(ship.Name), now);
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the ships that have gone threw: " + e);
            }
        }

        /// <summary>A whole flotilla wiped out - which the game marks on the model rather than on any
        /// label, and which is the shape of a battle turning.</summary>
        private void Flotillas(GalaxyEncounter encounter)
        {
            Encounter battle = Battle(encounter);
            if (battle == null)
            {
                return;
            }

            try
            {
                float now = Time.realtimeSinceStartup;
                EncounterGroup mine = battle.GetGroupByEmpireIndex(Gui.PlayerEmpire.Index);
                for (int g = 0; g < battle.Groups.Length; g++)
                {
                    EncounterGroup group = battle.Groups[g];
                    if (group == null || group.Flotillas == null)
                    {
                        continue;
                    }

                    BurstWatch watch = ReferenceEquals(group, mine)
                        ? _yourFlotillas
                        : _enemyFlotillas;
                    for (int f = 0; f < group.Flotillas.Length; f++)
                    {
                        EncounterFlotilla flotilla = group.Flotillas[f];
                        if (
                            flotilla == null
                            || flotilla.Status != EncounterEntityStatus.Destroyed
                        )
                        {
                            continue;
                        }

                        watch.Note(
                            g + "/" + f,
                            AgeText.Clean(
                                Gui.Localize(FlotillaNameKey, (flotilla.Index + 1).ToString())
                            ),
                            now
                        );
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the flotillas that have gone threw: " + e);
            }

            float when = Time.realtimeSinceStartup;
            Voice.Say(
                BattleText.Losses(
                    _yourFlotillas.Due(when),
                    YourFlotillaLostKey,
                    YourFlotillasLostKey
                ),
                false
            );
            Voice.Say(
                BattleText.Losses(
                    _enemyFlotillas.Due(when),
                    EnemyFlotillaLostKey,
                    EnemyFlotillasLostKey
                ),
                false
            );
        }

        /// <summary>How far through the fight is, at the quarter marks and only ever upward - a battle
        /// that is rewound re-arms the marks with everything else.</summary>
        private void Progress(GalaxyEncounter encounter)
        {
            if (State(encounter) != GalaxyEncounter.GalaxyEncounterState.Running)
            {
                return;
            }

            int milestone;
            try
            {
                milestone = Mathf.Clamp(
                    (int)(encounter.CurrentBattleTimeRatio * Milestones),
                    0,
                    Milestones
                );
            }
            catch (Exception)
            {
                return;
            }

            if (milestone <= _milestone)
            {
                return;
            }

            _milestone = milestone;
            Voice.Say(
                BattleText.Optional(ProgressKey, milestone * 100 / Milestones),
                false
            );
        }

        /// <summary>Which side the arcs say is stronger and by how much, in the two fleets' own names -
        /// the same sentence the popups read.</summary>
        private static string Balance(GalaxyEncounter encounter)
        {
            Encounter battle = Battle(encounter);
            if (battle == null || battle.Groups == null || battle.Groups.Length < 2)
            {
                return null;
            }

            try
            {
                EncounterGroup mine = battle.GetGroupByEmpireIndex(Gui.PlayerEmpire.Index);
                EncounterGroup theirs = ReferenceEquals(battle.Groups[0], mine)
                    ? battle.Groups[1]
                    : battle.Groups[0];
                return BattleNotifications.BalanceText(mine, theirs, false);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The three things the game lets a watcher do, and nothing else.
        ///
        /// The per-ship grid is deliberately not declared: those tiles are a camera control - clicking one
        /// points the free camera at that ship - and everything they say about the ship is already
        /// narrated. OWNER OPTION: they could be declared as a roster to walk.
        /// </summary>
        public override void Build(GraphBuilder builder)
        {
            global::BattleScreen window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                _cells.Clear();
                Skip(window);
                Cameras(window);
                Scan(window);
                if (_cells.Count > 0)
                {
                    builder.BeginStop(ControlsStop);
                    Cells.EmitLinear(builder, _cells);
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the battle controls threw: " + e);
            }
        }

        /// <summary>Skip to the action - drawn only while the game will take it, and saying which of its
        /// two states it is in (a skip already asked for and not yet honoured is the game's own "pending"
        /// icon, and there is no key bound to any of this).</summary>
        private void Skip(global::BattleScreen window)
        {
            AgeControlToggle skip = window.SkipButton;
            AgeTransform widget = AgeWidgets.Transform(skip);
            if (skip == null)
            {
                return;
            }

            AgeControlToggle it = skip;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Offered(widget);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Clean(SkipTitleKey),
                () => AgeWidgets.Toggle(it),
                enabled,
                tooltip
            );
            NodeAnnouncement refusal = GraphNodes.RefusalPart(tooltip, enabled);
            if (refusal != null)
            {
                vtable.Announcements.Add(refusal);
            }

            AgeWidgets.Point(vtable, it, tooltip, widget);
            Cells.Add(_cells, widget, ControlId.For(skip, "battle:skip"), vtable);
        }

        /// <summary>The three camera modes, which are a radio group in the game and read as one. They
        /// change nothing about what is said - the narration is the same wherever the camera is - and are
        /// declared because they are drawn and because a player may want the overview.</summary>
        private void Cameras(global::BattleScreen window)
        {
            GuiRadioGroup group = window.CameraRadioGroup;
            AgeTransform table = group == null ? null : group.TogglesTable;
            // Flow control: the cameras are found by a component scrape, not worth running for a bar
            // the battle view is not drawing.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            AgeControlToggle[] toggles = table.GetComponentsInChildren<AgeControlToggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                AgeControlToggle toggle = toggles[i];
                AgeTransform widget = AgeWidgets.Transform(toggle);
                if (toggle == null)
                {
                    continue;
                }

                AgeControlToggle it = toggle;
                int index = i;
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                NodeVtable vtable = GraphNodes.Radio(
                    () => CameraName(widget, index),
                    () => it.State,
                    () => AgeWidgets.Toggle(it),
                    () => AgeWidgets.Offered(widget),
                    null,
                    tooltip
                );
                AgeWidgets.Point(vtable, it, tooltip, widget);
                Cells.Add(
                    _cells,
                    widget,
                    ControlId.For(toggle, "battle:camera/" + i),
                    vtable
                );
            }
        }

        /// <summary>What a camera toggle is called: the game's own title for the mode at that position -
        /// the toggles are bare icons and the group's order is the camera-mode enum's own.</summary>
        private static string CameraName(AgeTransform widget, int index)
        {
            string drawn = AgeWidgets.TextOf(widget);
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            return index >= 0 && index < CameraTitleKeys.Length
                ? AgeText.Clean(CameraTitleKeys[index])
                : null;
        }

        /// <summary>The battle lens: the game's own X-ray of the fight, which the mod reads as a screen of
        /// its own once it is up (<see cref="ScanViewScreen"/>).</summary>
        private void Scan(global::BattleScreen window)
        {
            AgeControlToggle scan = window.ScanToggle;
            AgeTransform widget = AgeWidgets.Transform(scan);
            if (scan == null)
            {
                return;
            }

            AgeControlToggle it = scan;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeText.Clean(ScanTitleKey),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Offered(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, widget);
            Cells.Add(_cells, widget, ControlId.For(scan, "battle:scan"), vtable);
        }

        private static GalaxyEncounter.GalaxyEncounterState State(GalaxyEncounter encounter)
        {
            try
            {
                return encounter == null
                    ? GalaxyEncounter.GalaxyEncounterState.Inactive
                    : encounter.State;
            }
            catch (Exception)
            {
                return GalaxyEncounter.GalaxyEncounterState.Inactive;
            }
        }

        private static double Clock(GalaxyEncounter encounter)
        {
            try
            {
                return encounter.CurrentTime;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        private static Encounter Battle(GalaxyEncounter encounter)
        {
            try
            {
                return encounter == null ? null : encounter.Encounter;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The battle the game is playing: the cinematic's own, else the one the loading window
        /// is still preparing - which is where the pre-roll gate lives.</summary>
        private static GalaxyEncounter Encounter(
            global::BattleScreen window,
            BattleLoadingWindow loading
        )
        {
            try
            {
                GalaxyEncounter playing = window == null ? null : window.GalaxyEncounter;
                return playing ?? (loading == null ? null : loading.GalaxyEncounter);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static global::BattleScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<global::BattleScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static BattleLoadingWindow Loading()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<BattleLoadingWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

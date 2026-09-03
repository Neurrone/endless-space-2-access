using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.ES2.Speech;
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
    /// Five tiers, loudest first.
    ///
    /// The PRE-ROLL GATE is the one that matters most, and it is not narration at all - it is a soft-lock.
    /// When the battle has finished loading the game stops and waits for a keypress it reads RAW, outside
    /// its own input system, and the only thing on screen saying so is a caption on the loading window.
    /// A player who cannot see that caption sits in front of a frozen game with no idea that space or a
    /// click will start it. So the caption is read the moment it appears, once - watched by its own TEXT
    /// and not by the game's state, because the state flips to waiting a frame before the window rewrites
    /// the label (<c>BattleLoadingWindow.Refresh</c> :213-216 runs off the state change), and a gate armed
    /// on the state spends itself on the caption that is still there and never says the one that arrives.
    ///
    /// The loading window is also the only battle surface with anything to STAND on, so while it is up the
    /// screen declares two read-only lines - which battle this is, and the prompt that is holding it - and
    /// a player who tabs at the pre-roll finds them instead of an empty screen. They are the same words the
    /// window draws, and they go when it goes.
    ///
    /// The ACTS come from the display mode the stream itself queues: the introduction names where this is
    /// and who it is against, the main act opens with the balance of power, and the outcome is the game's
    /// own word for how it went. The PHASES come from the encounter's own phase index and say only which
    /// phase it is - a place-marker between stretches of narrated fighting, not a place to repeat the
    /// balance sentence.
    ///
    /// The FIGHT ITSELF is the tier the model cannot answer, and it comes from the report stream the
    /// client is replaying (<see cref="BattleStream"/>): a shot fired, a shot that missed, damage that
    /// got through, damage the shields ate, something arriving, something repaired, a medal. Those are
    /// EVENTS - by the next frame the model has forgotten each one - and there are hundreds of them, one
    /// per weapon shot. So they are gathered per attacker-target pair over a window
    /// (<see cref="FireWatch"/>) and reported as one sentence per pair: "Prowler hit Endeavor 3 times:
    /// 86 energy damage, missed twice". A player following the fight by ear gets a running commentary at
    /// roughly the pace a sighted player takes the arena in, instead of either silence or a stream of
    /// individual shots nobody can listen to.
    ///
    /// LOSSES, PHASES and PROGRESS are read off the encounter model, and that model is only telling the
    /// truth about "so far" while the stream is PLAYING. During loading it holds the battle's FINAL state:
    /// the game applies every phase report to it as the reports arrive
    /// (<c>Encounter.OnPhaseReportReceived</c> :960 -> <c>ParsePhaseReport</c> :678) and only rewinds it
    /// with <c>Encounter.RestoreEntitiesSimulation</c> (<c>GalaxyEncounter</c> :1798) just before the
    /// pre-roll gate. A watch that reads it before then reads the ending - which is how the mod once
    /// announced who would lose while the player was still waiting to press space.
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

        /// <summary>How long the exchange of fire gathers before it is reported as one round of
        /// summaries. Much longer than a loss's burst on purpose: a death is urgent and a volley is
        /// not, and a fight narrated every second is a fight nobody can follow. Long enough that a
        /// pair trading shots is one sentence, short enough that the summary still describes what is
        /// on screen.</summary>
        private const float VolleySeconds = 5.0f;

        /// <summary>How many exchanges one window may report. They come back loudest first, so a
        /// crowded window keeps the hardest-hit pairs and drops the skirmishes - a cap the fixture's
        /// three ships never reach, and a big fleet action would.</summary>
        private const int VolleyLines = 3;

        /// <summary>How many quarters the battle's progress is reported in.</summary>
        private const int Milestones = 4;

        /// <summary>A jump backwards in the battle clock this large means the stream has been rewound -
        /// the player asked to watch it again - rather than a frame arriving out of order. The game sets
        /// the clock to -0.5 to start a re-watch.</summary>
        private const double Rewound = 0.25;

        private const string PhaseTitleKey = "%AdvancedReportModalWindowPhaseTitle";
        private const string SkipTitleKey = "%BattleScreenSkipPhaseTitle";
        private const string ScanTitleKey = "%BattleScreenScanTitle";
        private static readonly string[] CameraTitleKeys = new string[]
        {
            "%BattleScreenAutoCameraTitle",
            "%BattleScreenFreeCameraTitle",
            "%BattleScreenOverviewCameraTitle",
        };

        private static readonly object ControlsStop = "battle:controls";
        private static readonly object PreRollStop = "battle:pre-roll";

        private readonly StepWatch _act = new StepWatch();
        private readonly StepWatch _phase = new StepWatch();
        private readonly BurstWatch _yourShips = new BurstWatch(BurstSeconds);
        private readonly BurstWatch _enemyShips = new BurstWatch(BurstSeconds);

        /// <summary>What has already been read off each side's state panel - see <see cref="Roster"/>.
        /// Dropped by <see cref="Rearm"/> along with everything else this screen remembers.</summary>
        private readonly Roster _yourRoster = new Roster();

        private readonly Roster _enemyRoster = new Roster();
        private readonly BurstWatch _yourFlotillas = new BurstWatch(BurstSeconds);
        private readonly BurstWatch _enemyFlotillas = new BurstWatch(BurstSeconds);
        private readonly FireWatch _fire = new FireWatch(VolleySeconds);
        private readonly Dictionary<string, float> _mended = new Dictionary<string, float>();
        private readonly List<Cell> _cells = new List<Cell>();

        private GalaxyEncounter _run;
        private double _clock;
        private int _milestone;
        private bool _titled;
        private bool _opened;
        private string _caption;

        public override string Key
        {
            get { return ModStrings.ScreenBattle; }
        }

        /// <summary>The same layer as the galaxy and the system pages: a battle is one of the game's view
        /// levels over the one map, and no two of them are ever up together. The battle lens sits just
        /// above it, and every popup well above that.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        /// <summary>
        /// Where this is and who it is against, in the game's own two lines - taken from whichever
        /// surface is actually drawing them.
        ///
        /// The loading window writes both when it opens (<c>BattleLoadingWindow.OnBeginShow</c> :107 and
        /// the panel bind at :135-136, left the player's side and right the opponent's), so while it is up
        /// it is the answer. The battle screen's own pair is written by nothing but
        /// <c>BattleScreen.SwitchBattleDisplayMode(Introduction)</c> (:166-169), which is why they are read
        /// only once that act has run: before it - and always while the screen is not shown at all - those
        /// labels hold the PREFAB's placeholders, and a screen that announces itself as "Battle at Antares,
        /// Versus DeltaPattern" is telling the player about a battle that does not exist. With neither
        /// surface talking, the mod's own word covers the rest of the fight.
        /// </summary>
        public override string ScreenName
        {
            get
            {
                try
                {
                    BattleLoadingWindow loading = Showing();
                    string named = loading == null ? null : Named(loading);
                    if (!string.IsNullOrEmpty(named))
                    {
                        return named;
                    }

                    global::BattleScreen window = Window();
                    if (_titled && window != null && window.Shown)
                    {
                        named = new MessageBuilder()
                            .ListItem(AgeText.Label(window.LocationTitle))
                            .ListItem(AgeText.Label(window.OpponentTitle))
                            .Build();
                        if (!string.IsNullOrEmpty(named))
                        {
                            return named;
                        }
                    }

                    return OptionalText.Phrase(ModStrings.ScreenBattle);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>The battle the loading window is drawing, in its own two labels: the title it composes
        /// from the system the fight is over, and the opponent's name off the panel it binds to the enemy
        /// group.</summary>
        private static string Named(BattleLoadingWindow loading)
        {
            BattleGroupInfoPanel enemy = loading.RightBattleGroupInfoPanel;
            return new MessageBuilder()
                .ListItem(AgeText.Label(loading.BattleTitle))
                .ListItem(enemy == null ? null : AgeText.Label(enemy.MainLeaderName))
                .Build();
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
            _yourRoster.Forget();
            _enemyRoster.Forget();
            _yourFlotillas.Reset();
            _enemyFlotillas.Reset();
            _fire.Reset();
            BattleStream.Forget();
            _clock = 0.0;
            _milestone = 0;
            _titled = false;
            _opened = false;
            _caption = null;
        }

        private void Narrate()
        {
            global::BattleScreen window = Window();
            BattleLoadingWindow loading = Showing();
            GalaxyEncounter encounter = Encounter(window, loading);

            if (encounter != null)
            {
                // A fresh battle, or the same one rewound to be watched again: everything is news.
                double clock = Clock(encounter);
                if (!ReferenceEquals(encounter, _run) || clock < _clock - Rewound)
                {
                    Rearm();
                    _run = encounter;
                }

                _clock = clock;
            }

            // After the re-arm and outside the encounter gate: a run that has just been declared fresh
            // has forgotten the last caption, so a watch that ran first would say this one twice - and
            // the caption is the one thing here the player needs whether or not the model resolves.
            Launch(loading);

            if (encounter == null)
            {
                return;
            }

            Act(window, encounter);
            if (Playing(encounter))
            {
                // One clock for the whole tick: the burst windows all measure from the same instant,
                // and four reads of the frame clock inside one narration could put a loss and the
                // flotilla it was in on either side of a window boundary.
                float now = Time.realtimeSinceStartup;
                Phase(encounter);
                Losses(window, now);
                Flotillas(encounter, now);
                Fighting(now);
            }
            else
            {
                // Nothing the stream queued belongs to a fight that is not being played - and the
                // pre-roll is the case that matters, because the report is already in memory then and
                // a queue left to fill would narrate the ending before the player pressed space.
                Discard();
            }

            Progress(encounter);
        }

        /// <summary>Whether the stream is being REPLAYED - the only span in which the model's per-ship and
        /// per-flotilla state means "what has happened so far". Skipping counts: the player asked for the
        /// rest of the fight at speed, and it is still the fight arriving.</summary>
        private static bool Playing(GalaxyEncounter encounter)
        {
            switch (State(encounter))
            {
                case GalaxyEncounter.GalaxyEncounterState.Running:
                case GalaxyEncounter.GalaxyEncounterState.PreparingSkipping:
                case GalaxyEncounter.GalaxyEncounterState.Skipping:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The gate. Every caption the loading window writes is read as it changes - which is the loading
        /// screen's rule and for the same reason, that a wait nobody narrates is a wait that looks like a
        /// hang - and the one saying the game is holding for a keypress is simply the last of them.
        ///
        /// Watched by the caption's own TEXT, so it is said exactly once however long the window sits on
        /// it, and so a caption already on screen when this screen is pushed or the mod reloads is said
        /// rather than missed (<see cref="Rearm"/> forgets the last one). Keying it on the game's WAITING
        /// state instead is what used to lose it: the state turns over one frame before
        /// <c>BattleLoadingWindow.Refresh</c> (:213-216) rewrites the label, so a one-shot armed on the
        /// state fires on the caption that is still there - "Loading…" - and the branch that noticed
        /// captions changing was never reached again.
        /// </summary>
        private void Launch(BattleLoadingWindow loading)
        {
            string caption = loading == null ? null : AgeText.Label(loading.Caption);
            if (string.IsNullOrEmpty(caption) || caption == _caption)
            {
                return;
            }

            _caption = caption;
            Voice.Say(caption, false);
        }

        /// <summary>Which act of the cinematic is running, and what that act is ABOUT: where and against
        /// whom, then the balance the fight opens on, then the game's own word for how it ended. The
        /// watermark moves only when something was actually said, so an act whose labels the game has not
        /// written yet is announced on the frame it writes them.</summary>
        private void Act(global::BattleScreen window, GalaxyEncounter encounter)
        {
            if (window == null || !window.Shown)
            {
                return;
            }

            global::BattleScreen.BattleDisplayMode mode = Acting(window);
            if (mode == global::BattleScreen.BattleDisplayMode.None)
            {
                return;
            }

            // The fight proper is on screen from the main act onwards, which is what releases the
            // phase lines: the encounter is already in phase one while the introduction is still
            // playing, so a phase watch with nothing holding it announces "Phase I" before the
            // battle has said where it is or who it is against (measured: Phase I at 0 s, the
            // introduction at 1 s, the balance at 6 s). Held here, the opening reads in the order the
            // screen presents it - where, against whom, the balance, then the phase.
            if (mode != global::BattleScreen.BattleDisplayMode.Introduction)
            {
                _opened = true;
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
                    // The act that WRITES the two labels, so from here on they are this battle's own.
                    _titled = true;
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

        /// <summary>
        /// Which act is on screen, read off the PANELS the battle screen shows rather than off the mode it
        /// was asked to switch to.
        ///
        /// <c>BattleScreen.CurrentMode</c> looks like the answer and is not: it is a public property the
        /// game declares (<c>BattleScreen</c> :130) and never once assigns - nothing in the whole assembly
        /// writes it - so it reads <c>None</c> for the length of every battle, and a watch on it fires once
        /// on nothing and then stays quiet for good. <c>SwitchBattleDisplayMode</c> (:163-208) shows
        /// exactly one of the three panels per act and hides the others, so the panels ARE the act, and
        /// they are what the player is looking at besides. Read latest-first: the acts only ever run
        /// forwards, and the panel going out is still shown for the length of its fade.
        /// </summary>
        private static global::BattleScreen.BattleDisplayMode Acting(global::BattleScreen window)
        {
            try
            {
                if (window.BattleOutcomePanel != null && window.BattleOutcomePanel.Shown)
                {
                    return global::BattleScreen.BattleDisplayMode.Outcome;
                }

                if (window.BattleDiskPanel != null && window.BattleDiskPanel.Shown)
                {
                    return global::BattleScreen.BattleDisplayMode.Main;
                }

                if (window.BattleIntroductionPanel != null && window.BattleIntroductionPanel.Shown)
                {
                    return global::BattleScreen.BattleDisplayMode.Introduction;
                }
            }
            catch (Exception) { }

            return global::BattleScreen.BattleDisplayMode.None;
        }

        /// <summary>Which phase the battle has reached, in the game's own numbering, and nothing else -
        /// held back until the fight proper is on screen, so the opening is not announced out of order.
        /// The balance used to ride along with it and no longer does (owner ruling): a phase line is a
        /// place-marker in a fight that is already being narrated shot by shot, and repeating the whole
        /// balance sentence at every one of them buried the fighting it was punctuating. The balance is
        /// still said once, where the main act opens on it.</summary>
        private void Phase(GalaxyEncounter encounter)
        {
            Encounter battle = Battle(encounter);
            if (battle == null || !_opened)
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
            Voice.Say(phase, false);
        }

        /// <summary>
        /// The ships that have gone, a side at a time and a burst at a time.
        ///
        /// Read off the same items the battle's own state panels are built from, which is where the game
        /// keeps its per-ship view of the fight: the wrapper on each item answers destroyed and kamikazed
        /// the way the panel asks it. The LEFT panel is always the player's side - the battle screen binds
        /// it to the player's own group - which is what makes "yours" and "theirs" answerable at all.
        /// </summary>
        private void Losses(global::BattleScreen window, float now)
        {
            if (window != null)
            {
                Lost(window.BattleStateGroupPanelLeft, _yourShips, _yourRoster, now);
                Lost(window.BattleStateGroupPanelRight, _enemyShips, _enemyRoster, now);
            }

            Voice.Say(
                BattleText.Losses(
                    _yourShips.Due(now),
                    ModStrings.BattleYourShipLost,
                    ModStrings.BattleYourShipsLost
                ),
                false
            );
            Voice.Say(
                BattleText.Losses(
                    _enemyShips.Due(now),
                    ModStrings.BattleEnemyShipLost,
                    ModStrings.BattleEnemyShipsLost
                ),
                false
            );
        }

        private static void Lost(
            BattleStateGroupPanel panel,
            BurstWatch watch,
            Roster seen,
            float now
        )
        {
            AgeTransform root = panel == null ? null : panel.AgeTransform;
            if (root == null)
            {
                return;
            }

            try
            {
                BattleStateShipItem[] items = seen.Items(root);
                for (int i = 0; i < items.Length; i++)
                {
                    GuiBattleShip ship = items[i] == null ? null : items[i].GuiBattleShip;
                    if (
                        ship == null
                        || !(ship.IsDestroyed || ship.HasKamikazed)
                        // Already turned into words once. Composing its id again is a fresh GUID
                        // string every frame of the animation for a Note that drops it, and the pile
                        // of them grows with every ship that goes.
                        || !seen.News(ship)
                    )
                    {
                        continue;
                    }

                    // Title, not Name: the wrapper's Name is the ship's GUID as a string (the base
                    // GuiWrapper is constructed with it, GuiBattleShip :386-387), and reading it out
                    // gave the player an 18-digit number where a ship should have been. Title is what
                    // the rosters draw - the name the ship was given, else its design's.
                    watch.Note(ship.GUID.ToString(), AgeText.Clean(ship.Title), now);
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the ships that have gone threw: " + e);
            }
        }

        /// <summary>
        /// What has already been read off one side's battle-state panel, for the length of ONE run.
        ///
        /// Two things, and both are about a narration that runs at frame rate: the ship items the
        /// panel is built from are made when the battle binds and stand for the whole run, so the
        /// component walk is made once and revalidated by the items still being alive rather than
        /// repeated every frame; and a ship whose loss has already been announced is remembered by its
        /// own wrapper, so its GUID is turned into a string once and not once per frame thereafter.
        ///
        /// Held for the run and dropped by <see cref="Rearm"/>, which is what a fresh battle - or the
        /// same one watched again - is. Nothing here is a watermark: what has been SAID is the
        /// <see cref="BurstWatch"/>'s own memory, and this only saves asking it.
        /// </summary>
        private sealed class Roster
        {
            private AgeTransform _from;

            private BattleStateShipItem[] _items;

            private readonly Dictionary<GuiBattleShip, bool> _told =
                new Dictionary<GuiBattleShip, bool>();

            public void Forget()
            {
                _from = null;
                _items = null;
                _told.Clear();
            }

            public BattleStateShipItem[] Items(AgeTransform root)
            {
                if (ReferenceEquals(_from, root) && _items != null && Alive(_items))
                {
                    return _items;
                }

                _from = root;
                _items = root.GetComponentsInChildren<BattleStateShipItem>(true);
                return _items;
            }

            /// <summary>Whether this ship's loss still has to be composed - true once, and false on
            /// every frame after it.</summary>
            public bool News(GuiBattleShip ship)
            {
                if (_told.ContainsKey(ship))
                {
                    return false;
                }

                _told[ship] = true;
                return true;
            }

            private static bool Alive(BattleStateShipItem[] items)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>A whole flotilla wiped out - which the game marks on the model rather than on any
        /// label, and which is the shape of a battle turning.</summary>
        private void Flotillas(GalaxyEncounter encounter, float now)
        {
            Encounter battle = Battle(encounter);
            if (battle == null)
            {
                return;
            }

            try
            {
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
                            || flotilla.Ships == null
                            || flotilla.Ships.Length == 0
                        )
                        {
                            // A flotilla that never held a ship is marked destroyed like any other -
                            // measured on the player's own empty reinforcement flotilla, which the
                            // game's report ends with Status Destroyed and zero ships. There is
                            // nothing there to have been destroyed, and announcing a loss the player
                            // did not take is worse than saying nothing. A flotilla that HELD ships
                            // and lost them keeps its roster array, so this only ever drops the
                            // empty ones.
                            continue;
                        }

                        watch.Note(
                            g + "/" + f,
                            BattleRosters.FlotillaName(flotilla.Index + 1),
                            now
                        );
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the flotillas that have gone threw: " + e);
            }

            Voice.Say(
                BattleText.Losses(
                    _yourFlotillas.Due(now),
                    ModStrings.BattleYourFlotillaLost,
                    ModStrings.BattleYourFlotillasLost
                ),
                false
            );
            Voice.Say(
                BattleText.Losses(
                    _enemyFlotillas.Due(now),
                    ModStrings.BattleEnemyFlotillaLost,
                    ModStrings.BattleEnemyFlotillasLost
                ),
                false
            );
        }

        /// <summary>
        /// The fight itself: who is shooting at whom, and what it is doing.
        ///
        /// Everything here comes off the replay stream (<see cref="BattleStream"/>) rather than off
        /// the model, because these are EVENTS - a shot, an arrival, a medal - and the model only ever
        /// answers what is true now. The shots go through a window (<see cref="FireWatch"/>) and come
        /// out as one line per attacker-target pair; the rest are rare enough to say as they happen.
        ///
        /// Losses are deliberately NOT folded in here. They keep their own short window
        /// (<see cref="BurstSeconds"/>) because a ship going is the one thing in a battle a player
        /// needs told immediately, and holding it for the length of a volley summary would land it
        /// after the fight had moved on.
        /// </summary>
        private void Fighting(float now)
        {
            try
            {

                List<BattleStream.Shot> shots = BattleStream.TakeShots();
                if (shots != null)
                {
                    for (int i = 0; i < shots.Count; i++)
                    {
                        BattleStream.Shot shot = shots[i];
                        _fire.Note(
                            shot.Attacker,
                            shot.Target,
                            shot.Hit,
                            shot.Damage,
                            shot.Absorbed,
                            shot.Kind,
                            now
                        );
                    }
                }

                IList<FireWatch.Volley> volleys = _fire.Due(now);
                if (volleys != null)
                {
                    int said = volleys.Count < VolleyLines ? volleys.Count : VolleyLines;
                    for (int i = 0; i < said; i++)
                    {
                        Voice.Say(BattleText.Volley(volleys[i]), false);
                    }
                }

                Arrivals();
                Repairs();
                Effects();
                Medals();
            }
            catch (Exception e)
            {
                Log.Warn("battle: narrating the exchange of fire threw: " + e);
            }
        }

        /// <summary>Everything the stream queued while the fight was not being played, thrown away
        /// unspoken.</summary>
        private void Discard()
        {
            BattleStream.TakeShots();
            BattleStream.TakeArrivals();
            BattleStream.TakeMends();
            BattleStream.TakeEffects();
            BattleStream.TakeAwards();
        }

        /// <summary>Something joining a fight already under way - which changes what the player should
        /// expect from it, and which the game announces with an arrival animation and nothing
        /// else.</summary>
        private static void Arrivals()
        {
            List<BattleStream.Arrival> arrivals = BattleStream.TakeArrivals();
            if (arrivals == null)
            {
                return;
            }

            for (int i = 0; i < arrivals.Count; i++)
            {
                Voice.Say(
                    OptionalText.Phrase(
                        arrivals[i].Mine
                            ? ModStrings.BattleReinforcements
                            : ModStrings.BattleEnemyReinforcements,
                        arrivals[i].Name
                    ),
                    false
                );
            }
        }

        /// <summary>Hull going back on, per ship - the several sections one repair touches are added
        /// up so the player hears one figure for the ship rather than one per section.</summary>
        private void Repairs()
        {
            List<BattleStream.Mend> mends = BattleStream.TakeMends();
            if (mends == null)
            {
                return;
            }

            _mended.Clear();
            for (int i = 0; i < mends.Count; i++)
            {
                float running;
                _mended.TryGetValue(mends[i].Ship, out running);
                _mended[mends[i].Ship] = running + mends[i].Amount;
            }

            foreach (KeyValuePair<string, float> mend in _mended)
            {
                int amount = (int)Mathf.Round(mend.Value);
                if (amount > 0)
                {
                    Voice.Say(
                        OptionalText.Phrase(ModStrings.BattleRepaired, mend.Key, amount),
                        false
                    );
                }
            }
        }

        /// <summary>A battle effect landing on a ship, in the game's own title for it.</summary>
        private static void Effects()
        {
            List<BattleStream.Effect> effects = BattleStream.TakeEffects();
            if (effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                Voice.Say(
                    OptionalText.Phrase(
                        ModStrings.BattleEffectApplied,
                        effects[i].Initiator,
                        effects[i].Name,
                        effects[i].Target
                    ),
                    false
                );
            }
        }

        /// <summary>A medal earned mid-fight, which the game marks with a badge on a card the player
        /// may never look at.</summary>
        private static void Medals()
        {
            List<BattleStream.Award> awards = BattleStream.TakeAwards();
            if (awards == null)
            {
                return;
            }

            for (int i = 0; i < awards.Count; i++)
            {
                Voice.Say(
                    OptionalText.Phrase(
                        ModStrings.BattleMedalEarned,
                        awards[i].Ship,
                        awards[i].Medal
                    ),
                    false
                );
            }
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
                OptionalText.Phrase(ModStrings.BattleProgress, milestone * 100 / Milestones),
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
                return BattleBalance.BalanceText(mine, theirs, false);
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
            BattleLoadingWindow loading = Showing();
            if (loading != null)
            {
                PreRoll(builder, loading);
                return;
            }

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

        /// <summary>
        /// What there is to stand on while the battle is loading: which battle this is, and the prompt
        /// holding it at the pre-roll gate.
        ///
        /// Both are read-only lines carrying the game's own words - nothing here is a control, and the
        /// keypress the second one describes is one the game reads raw, so the row DESCRIBES it and does
        /// not offer to press it. They exist for the same reason the caption is narrated: a player who
        /// arrives at a frozen game and reaches for Tab should find the two facts on screen rather than
        /// nothing at all, and should be able to go back over them at their own pace.
        /// </summary>
        private void PreRoll(GraphBuilder builder, BattleLoadingWindow loading)
        {
            try
            {
                _cells.Clear();
                BattleLoadingWindow it = loading;
                Line(
                    loading.BattleTitle == null ? null : loading.BattleTitle.AgeTransform,
                    "battle:title",
                    () => Named(it)
                );
                Line(
                    loading.Caption == null ? null : loading.Caption.AgeTransform,
                    "battle:launch",
                    () => AgeText.Label(it.Caption)
                );
                if (_cells.Count > 0)
                {
                    builder.BeginStop(PreRollStop);
                    Cells.EmitLinear(builder, _cells);
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the loading window threw: " + e);
            }
        }

        /// <summary>One read-only line, declared only where the game has written something for it to say -
        /// a label the window has left empty is not a node with no name.</summary>
        private void Line(AgeTransform widget, string key, Func<string> words)
        {
            if (widget == null || string.IsNullOrEmpty(words()))
            {
                return;
            }

            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(words) },
            };
            Cells.Add(_cells, widget, ControlId.For(widget, key), vtable);
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
            return GameWindows.Of<global::BattleScreen>();
        }

        /// <summary>The loading window while it is the battle surface the game is DRAWING - which is what
        /// makes its labels this battle's own rather than whatever the last one left in them.</summary>
        private static BattleLoadingWindow Showing()
        {
            return GameWindows.Shown<BattleLoadingWindow>();
        }
    }
}

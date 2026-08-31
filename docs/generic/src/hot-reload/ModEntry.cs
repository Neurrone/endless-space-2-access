using System;
using System.Collections;
using System.IO;
using ES2Access.Core.Native;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.Dev;
using ES2Access.Loader;
using ES2Access.Localization;
using ES2Access.Screens;
using ES2Access.UI;
using ES2Access.UI.Bookmarks;
using ES2Access.UI.Input;
using ES2Access.UI.ModOptions;
using ES2Access.UI.Settings;
using UnityEngine;

namespace ES2Access
{
    /// <summary>
    /// The whole mod, from the loader's point of view. ES2Access.Loader reflection-invokes
    /// <see cref="Start"/> when the game comes up and again after every hot reload, and
    /// <see cref="Stop"/> before it lets this assembly go.
    ///
    /// Both halves have to be complete: everything Start acquires - the speech backend, the
    /// speech tap, dev routes, the per-frame pump, the loaded translations - Stop gives back, so
    /// a reload starts from the same state a fresh game does. Anything left behind would still be
    /// running from an assembly nobody can reach any more.
    /// </summary>
    public static class ModEntry
    {
        /// <summary>The mod's version, as the build stamped it into this assembly - the one source is
        /// <c>&lt;Version&gt;</c> in ES2Access.csproj, so a release bump cannot leave the spoken startup
        /// line, the dev server and the DLL disagreeing. Read from metadata rather than from a file, which
        /// is what makes it work under the loader's load-from-bytes path (there is no path on disk to
        /// ask). Falls back on the numeric assembly version, which the build always writes, rather than
        /// on a word no translator was given.</summary>
        public static readonly string ModVersion = ReadVersion();

        private static string ReadVersion()
        {
            try
            {
                System.Reflection.Assembly assembly = typeof(ModEntry).Assembly;
                System.Reflection.AssemblyInformationalVersionAttribute stamped =
                    (System.Reflection.AssemblyInformationalVersionAttribute)
                        Attribute.GetCustomAttribute(
                            assembly,
                            typeof(System.Reflection.AssemblyInformationalVersionAttribute)
                        );
                return stamped == null || string.IsNullOrEmpty(stamped.InformationalVersion)
                    ? assembly.GetName().Version.ToString()
                    : stamped.InformationalVersion;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>Set to 1 to run without a screen reader: nothing is voiced, but everything the
        /// mod would have said is still readable from the dev server.</summary>
        public const string NoSpeechEnv = "ES2ACCESS_NO_SPEECH";

        /// <summary>How long to wait for the game to report its language before announcing the
        /// mod anyway, so a game that never registers the service is not silent at startup.</summary>
        private const float LanguageWaitSeconds = 5f;

        /// <summary>Public so that code compiled by POST /eval can drive speech directly: the
        /// REPL compiles against this assembly from outside it and cannot see internals.</summary>
        public static PrismSpeech Speech;

        /// <summary>The keyboard cursor over the focused screen. Public for the same reason as
        /// <see cref="Speech"/>: POST /eval drives navigation through it, since HTTP cannot press a
        /// key. <c>ModEntry.Navigator.Dispatch("ui.down")</c> is exactly what the Down arrow does.
        /// </summary>
        public static GraphNavigator Navigator;

        /// <summary>The reviewable text - what the focused control has to say, walked line by line.
        /// Public for POST /eval, which drives it the way Ctrl+Down does:
        /// <c>ModEntry.Buffers.Dispatch("buffer.lineDown")</c>.</summary>
        public static BufferController Buffers;

        /// <summary>Which screen the player is on, and the registry of the ones we know how to
        /// navigate. Public for POST /eval.</summary>
        public static ScreenManager Screens;

        /// <summary>What the player is carrying, if anything - what a screen's pick-up and drop
        /// declarations read (see <see cref="CarryState"/>). The navigator owns it, because the key
        /// that fills it is dispatched there and a carry lives and dies with the page it started on;
        /// null only while the mod is not up.</summary>
        public static CarryState Carry
        {
            get
            {
                GraphNavigator navigator = Navigator;
                return navigator == null ? null : navigator.Carry;
            }
        }

        /// <summary>The mod's keys. Public for POST /eval.</summary>
        public static ModInput Input;

        private static ModHost _host;
        private static ModRoutes _routes;

        /// <summary>The multiplayer session's narration and its log - every session event this game has
        /// is a chat message, so one subscription covers all of them. It lives here rather than on a
        /// screen because it has to be listening in the lobby and in the game alike.</summary>
        private static SessionChat _chat;

        /// <summary>The game's own in-game chat box, announced as the keyboard goes into it and comes
        /// back. Like the chat log it belongs to no screen: the box is opened from wherever the player
        /// is standing.</summary>
        private static ChatField _chatField;

        /// <summary>The wordless spinner the game shows while it writes a save, said out loud. Like the
        /// chat, it lives here rather than on a screen: a save is started from the game menu, from the
        /// save page, from a quick-save key and by the game itself at the turn's end, and the player has
        /// to hear about it wherever they are standing.</summary>
        private static SaveProgress _saving;

        /// <summary>How close the game is looking, said whenever it changes. Like the chat and the save
        /// spinner it belongs to no screen: the zoom crosses pages - the galaxy, the scan overlay, a
        /// system's page, a planet's - and a watcher owned by one of them would fall silent on the very
        /// step that changed the page.</summary>
        private static ZoomWatch _zoom;

        /// <summary>A fleet's journey being called off - intercepted, or cancelled under it - said out
        /// loud. Here rather than on the galaxy page because it happens to fleets wherever the player
        /// is standing: a route dies while its owner is reading the research wheel just as readily as
        /// while they are watching the map, and a watcher owned by the map would miss exactly that.
        /// </summary>
        private static FleetRouteWatch _fleetRoutes;

        private static bool _announcedStartup;
        private static float _startTime;

        public static void Start(ModHost host)
        {
            _host = host;
            _startTime = Time.realtimeSinceStartup;
            _announcedStartup = false;

            Log.Install(host.LogInfo, host.LogWarning, host.LogError);
            ModLocale.PluginDirectory = host.PluginDirectory;
            // Before anything reads a setting: the keys the player has moved are applied to the
            // actions as they are registered, further down.
            ModSettings.Load(host.PluginDirectory);
            // Nothing is read here: which campaign is being played is not known until there is a game,
            // and the store answers that for itself from the pump (<see cref="MapBookmarkStore"/>).
            MapBookmarkStore.Start(host.PluginDirectory);

            Speech = new PrismSpeech();
            if (Environment.GetEnvironmentVariable(NoSpeechEnv) == "1")
            {
                Log.Info("Speech disabled (" + NoSpeechEnv + "=1)");
            }
            else
            {
                string prismPath = Path.Combine(host.GameRootPath, "prism.dll");
                if (NativeLoader.LoadPrism(prismPath))
                {
                    Speech.Initialize();
                }
            }

            Buffers = new BufferController();
            // Registered right after the UI buffer, so the chat log is the buffer Ctrl+Right reaches
            // from a control's description while a multiplayer session is up.
            _chat = new SessionChat(Buffers);
            _chatField = new ChatField();
            _saving = new SaveProgress();
            _zoom = new ZoomWatch();
            // Baselined off the live game, so a reload in the middle of a journey says nothing about a
            // route it was not there to see given.
            _fleetRoutes = new FleetRouteWatch();
            _fleetRoutes.Baseline();
            Navigator = new GraphNavigator(Buffers);
            InstallAnnouncerWording();
            // A tooltip the game has to draw before its words exist arrives after focus does, so the
            // focused control's description is read again when it lands.
            PointerFocus.DrawnTooltipChanged = Navigator.InvalidateBuffer;
            Screens = new ScreenManager(Navigator);
            // Spelled out in full: the game has its own MainMenuScreen in the global namespace.
            Screens.Register(new global::ES2Access.Screens.MainMenuScreen());
            Screens.Register(new OptionsScreen());
            Screens.Register(new DropListScreen());
            Screens.Register(new MessageBoxScreen());
            Screens.Register(new LoadingScreen());
            Screens.Register(new NotificationScreen());
            Screens.Register(new TutorialScreen());
            Screens.Register(new GameMenuScreen());
            Screens.Register(new LoadSaveScreen());
            Screens.Register(new GalaxyHudScreen());
            Screens.Register(new SystemManagementScreen());
            Screens.Register(new PlanetOverviewScreen());
            Screens.Register(new SystemDiscoveryScreen());
            Screens.Register(new ScanViewScreen());
            Screens.Register(new ImprovementsModalScreen());
            Screens.Register(new SystemSelectionScreen());
            Screens.Register(new SystemPoliticsScreen());
            Screens.Register(new HeroSelectionScreen());
            Screens.Register(new ElectionScreen());
            Screens.Register(new RenameModalScreen());
            Screens.Register(new TutorialSelectionScreen());
            Screens.Register(new global::ES2Access.Screens.NewGameScreen());
            Screens.Register(new AdvancedSettingsScreen());
            Screens.Register(new FactionChoiceScreen());
            Screens.Register(new CustomFactionScreen());
            Screens.Register(new PlanetConstructiblesScreen());
            Screens.Register(new ResearchScreen());
            Screens.Register(new QuestJournalScreen());
            Screens.Register(new global::ES2Access.Screens.EmpireScreen());
            Screens.Register(new global::ES2Access.Screens.SenateScreen());
            Screens.Register(new global::ES2Access.Screens.EconomyScreen());
            Screens.Register(new global::ES2Access.Screens.MilitaryScreen());
            Screens.Register(new TroopManagementScreen());
            Screens.Register(new JuggernautSpecializationScreen());
            Screens.Register(new ContextualPromptScreen());
            Screens.Register(new BattleTacticsScreen());
            Screens.Register(new FleetSelectionScreen());
            Screens.Register(new global::ES2Access.Screens.AcademyScreen());
            Screens.Register(new HeroCompleteListScreen());
            Screens.Register(new ShipDesignScreen());
            Screens.Register(new HeroInspectionScreen());
            Screens.Register(new RecipeCreationScreen());
            Screens.Register(new GovernmentScreen());
            Screens.Register(new LawsScreen());
            Screens.Register(new PopulationScreen());
            Screens.Register(new SpaceBattleScreen());
            Screens.Register(new GroundBattleViewScreen());
            Screens.Register(new AdvancedBattleReportScreen());
            Screens.Register(new global::ES2Access.Screens.DiplomacyScreen());
            Screens.Register(new NegotiationScreen());
            Screens.Register(new MinorFactionDiplomacyScreen());
            Screens.Register(new PirateDiplomacyScreen());
            Screens.Register(new AcademyDiplomacyScreen());
            Screens.Register(new AcademyModalScreen());
            Screens.Register(new ErrorScreen());
            Screens.Register(new NonBlockingMessageScreen());
            Screens.Register(new TargetSelectionScreen());
            Screens.Register(new GroundTargetSelectionScreen());
            Screens.Register(new CutsceneScreen());
            Screens.Register(new VictoryAchievedScreen());
            Screens.Register(new global::ES2Access.Screens.VictoryScreen());
            Screens.Register(new JournalScreen());
            Screens.Register(new TableFilterScreen());
            Screens.Register(new AdvancedEncounterPlayScreen());
            Screens.Register(new DisclaimerScreen());
            Screens.Register(new DLCScreen());
            Screens.Register(new CreditsScreen());
            Screens.Register(new ModdingConfigScreen());
            Screens.Register(new ResourcesExportModScreen());
            Screens.Register(new JoinGameListScreen());
            Input = new ModInput();
            Input.Dispatch = Dispatch;
            // The one widget the mod puts the game's own keyboard focus on. The input layer would
            // otherwise read that focus as "the player is typing" and stand down inside a list it is
            // itself driving.
            Input.DrivenByMod = DropListScreen.OwnsFocus;
            Input.HasFocusedScreen = ScreenFocused;
            // Escape is the game's everywhere except over a surface the mod invented, which the game
            // cannot close because it does not know it is there.
            Input.ClaimsBackKey = BackKeyClaimed;
            // Letters are the type-ahead search's while a screen of ours is taking them; the game
            // has hotkeys on most of the alphabet and would otherwise act on every one of them.
            Input.ClaimsTypedKey = TypedKeyClaimed;
            Navigator.TypedCharacters = TypedText.Frame;
            Navigator.KeyboardIsOurs = Input.LayerIsLive;
            BindKeys(Input);
            // Straight after the compiled-in bindings and before anything can press one: the
            // defaults are learnt (so the settings file only ever holds what the player moved) and
            // whatever they moved is put back on.
            ModBindings.Install(Input);
            // Bindings first: the game's scans ask the layer which keys it has, so there must be
            // something to answer with before they can be told to stand down.
            GameKeyStandDown.Install();
            // And the same for the control the game has focused, which is handed the frame's keys after
            // the mod has already acted on them.
            GameKeyboardHandover.Install();
            // And the setter every way out of a text box passes through, which is the only place a
            // commit can be told from a cancel and the last moment a cancel can put the text back.
            GameTextFocus.Install();
            ES2Access.UI.ModOptions.OptionTextFieldCommit.Install();
            // Every key-mapping commit, in either options window: a chord the mod and the game both
            // answer to is said out loud, both ways round, and nothing is taken from anybody.
            BindingOverlaps.Install();
            // The one key that still reaches the game while it holds the keyboard for the chat box:
            // Escape, which the panel answers by shutting itself instead of letting go.
            ChatEscape.Install();
            // The game's own "the thing you are missing is over here", which moves the view and would
            // otherwise leave the cursor behind - once for the technology wheel, once for the map,
            // which between them are where every one of those buttons lands.
            ResearchLocate.Install();
            GalaxyLocate.Install();
            // And the arrivals nobody asked for that the three calls above never see: a save being
            // loaded, and coming back out of a system's page.
            GalaxyOverviewEntry.Install();
            // And the map's own doors, which no "show me this" ever passes through either: a click or
            // the wheel taking the camera in on a star, which moved the picture out from under the
            // page's record of it (see GalaxyPick).
            GalaxyPick.Install();
            // The mod's own notifications ride in the game's list; the strip is left looking exactly
            // as it did, which is what this patch is for (see NotificationStrip). The mappings
            // themselves are re-asserted from the pump, because the manager holding them is built
            // per game.
            NotificationStrip.Install();
            // The two things the game notices and puts on no bus at all: a fleet of the player's
            // reaching where it was sent, and somebody else's fleet going out of sight or standing
            // somewhere else this turn. Both feed the same notification pipeline.
            FleetArrivals.Install();
            ForeignFleetWatch.Install();
            // A watched space battle is a stream of instructions being played back, and the exchange
            // of fire lives only in that stream - the model has forgotten each shot by the next
            // frame. The battle screen drains what this queues (see BattleStream).
            BattleStream.Install();
            // And the game's own probe cancel, which hands the panel back to whichever fleet is
            // parked first at the slot rather than to the one that armed the mode.
            ProbeCancelSelection.Install();

            _routes = new ModRoutes(host);
            _routes.Register();
            // Every notification popup checks itself against what it draws while a developer is
            // watching; off, and costing nothing, in a player's game.
            NotificationAudit.Arm();
            host.SetUpdateHandler(Update);
            host.StartCoroutine(PointFrameEnd());
        }

        /// <summary>
        /// The engine recomputes what the pointer is over in its own LateUpdate and the tooltip
        /// controller reads that in the next Update, so the only moment our answer survives to be
        /// seen is after everything else in the frame has had its say.
        /// </summary>
        private static IEnumerator PointFrameEnd()
        {
            WaitForEndOfFrame frameEnd = new WaitForEndOfFrame();
            while (true)
            {
                yield return frameEnd;
                PointerFocus.LateTick();
            }
        }

        /// <summary>
        /// Where a key the player pressed goes. The whole layer stands down when no screen of ours
        /// is focused - the game's own keys are the only ones that make sense then - and review
        /// keys are offered before navigation ones, since they are chords over the same arrows.
        /// </summary>
        // Spelled out in full: the game has its own InputAction in the global namespace.
        private static bool Dispatch(ES2Access.UI.Input.InputAction action)
        {
            if (!ScreenFocused())
            {
                return false;
            }

            // A screen where every key means one thing answers first, before the review chords and
            // before navigation: on a playing cutscene ANY key is the game's own skip, and a mod that
            // spent the press on a review buffer would have eaten it - see Screen.AnyKey.
            //
            // ESCAPE WHILE A SEARCH IS LIVE IS THE ONE KEY THAT GOES ROUND THIS HOOK (owner ruling
            // 2026-08-19). A type-ahead search is the innermost surface the mod invented, and the key
            // that ends one goes no further (GraphNavigator.SearchAction). A screen answering AnyKey
            // with a MODE of its own - the galaxy's inspect cursor - would otherwise end the mode and
            // take the live search down with it: two surfaces for one press, and no way to keep the
            // mode while dropping the search. So the search clears first and the NEXT Escape leaves
            // the mode. The rule is stated here rather than in each mode because every mode has the
            // same shape; the cutscene is unaffected, since a screen that opts out of type-ahead can
            // never have a live search.
            ES2Access.Screens.Screen focused = Navigator.Screen;
            bool searchOwnsBack = action.Key == UiActions.Back && Navigator.SearchIsActive;
            if (!searchOwnsBack && focused != null && focused.AnyKey(action.Key))
            {
                return true;
            }

            return HudKey(action.Key)
                || Buffers.Dispatch(action.Key)
                || Navigator.Dispatch(action.Key)
                || SwallowedCarry(action.Key);
        }

        /// <summary>
        /// The keys that answer for the PAGE rather than for the control the cursor is on: go to the
        /// empire banners, the notifications, the turn log, the turn controls, the galaxy map; end the
        /// turn; and hear what the galaxy as a whole is.
        ///
        /// Each one re-asks the question its claim asked (is that panel on this page at all), because a
        /// claim is answered before the press and the mod is never allowed to act on a stale yes
        /// (<c>docs/interaction.md</c>). Where the answer is no the key is inert: no speech, no move,
        /// nothing consumed.
        /// </summary>
        private static bool HudKey(string actionKey)
        {
            switch (actionKey)
            {
                case UiActions.FocusEmpire:
                    return FocusStop(GlobalHud.EmpireStop);
                case UiActions.FocusNotifications:
                    return FocusStop(GlobalHud.NotificationStop);
                case UiActions.FocusTurn:
                    return FocusStop(GlobalHud.TurnStop);
                case UiActions.FocusTurnLog:
                    return FocusStop(GlobalHud.TurnLogStop);
                case UiActions.FocusMap:
                    return FocusStop(GalaxyHudScreen.SystemStop);
                case UiActions.SummarizeMap:
                    return SummarizeMap();
                case UiActions.EndTurn:
                    return GlobalHud.EndTurnByKey();
                case UiActions.NextIdleFleet:
                    return GlobalHud.NextIdleFleetByKey();
                case UiActions.ApplyMovements:
                    return GlobalHud.ApplyMovementsByKey();
                default:
                    return false;
            }
        }

        private static bool FocusStop(object stopKey)
        {
            GraphNavigator navigator = Navigator;
            return navigator != null && navigator.FocusStop(stopKey);
        }

        /// <summary>Whether the focused page is drawing a panel, asked from the game's own key scans -
        /// see <see cref="GraphNavigator.DeclaresStop"/>. One predicate per key, because a claim is a
        /// parameterless question.</summary>
        private static bool StopDeclared(object stopKey)
        {
            GraphNavigator navigator = Navigator;
            return navigator != null && navigator.DeclaresStop(stopKey);
        }

        private static bool EmpireStopDeclared()
        {
            return StopDeclared(GlobalHud.EmpireStop);
        }

        private static bool NotificationStopDeclared()
        {
            return StopDeclared(GlobalHud.NotificationStop);
        }

        private static bool TurnStopDeclared()
        {
            return StopDeclared(GlobalHud.TurnStop);
        }

        private static bool TurnLogStopDeclared()
        {
            return StopDeclared(GlobalHud.TurnLogStop);
        }

        private static bool MapStopDeclared()
        {
            return StopDeclared(GalaxyHudScreen.SystemStop);
        }

        /// <summary>
        /// Say what the galaxy IS (<see cref="ES2Access.UI.GalaxyOverview"/>) - the one thing a
        /// sighted player reads off the map in a second and a listener is never told at all.
        ///
        /// Only from the galaxy map, which is the same question the claim asked and is asked again
        /// here: a claim is answered before the press and the act never runs on a stale yes. Off the
        /// map the key is inert and left to the game. With nothing honest to say - a game with no home
        /// system yet - the press is still the map's, and silent.
        /// </summary>
        private static bool SummarizeMap()
        {
            if (!MapStopDeclared())
            {
                return false;
            }

            Voice.Say(GalaxyOverview.Sentence(), true);
            return true;
        }

        /// <summary>Whether anything on the focused page answers the go-to-location key - the claim
        /// half of Control+L (<see cref="GraphNavigator.TakesGoToLocation"/>).</summary>
        private static bool GoToLocationOffered()
        {
            GraphNavigator navigator = Navigator;
            return navigator != null && navigator.TakesGoToLocation();
        }

        /// <summary>Whether a screen of ours has the keyboard cursor - the question both the
        /// dispatch and the game's stand-down turn on.</summary>
        private static bool ScreenFocused()
        {
            GraphNavigator navigator = Navigator;
            return navigator != null && navigator.Screen != null;
        }

        /// <summary>Whether the screen the player is on is going to answer Escape itself - asked by
        /// the game's own key scan, several times a frame, so it stays two field reads.</summary>
        private static bool BackKeyClaimed()
        {
            GraphNavigator navigator = Navigator;
            if (navigator != null && (navigator.SearchIsActive || navigator.Carry.IsCarrying))
            {
                // A search is a surface the mod invented too: Escape puts the keyboard back, and
                // must not also close the screen the player was searching. So is carrying something:
                // the key puts it down, and the page the player was carrying it across stays open.
                return true;
            }

            ScreenManager screens = Screens;
            ES2Access.Screens.Screen current = screens == null ? null : screens.Current;
            return current != null && current.ConsumesBack;
        }

        /// <summary>Whether the carry key is the mod's - asked by the game's own key scan, several
        /// times a frame. Claimed only where it can DO something: a control with something to pick up,
        /// something already being carried, or a live search the space is a character of
        /// (<see cref="GraphNavigator.TakesCarryKey"/>). Everywhere else Space is the game's, which is
        /// ToggleScanView (`InputManager.cs:233`, shared with Mouse2) - the strategic lens, now a
        /// screen of the mod's own (<see cref="ES2Access.Screens.ScanViewScreen"/>) that announces
        /// itself on arrival, so handing the key back no longer drops the player into an unannounced
        /// mode (owner decision 2026-08-12, reversing the blanket claim of 2026-08-11).
        ///
        /// The star-system page is the exception, and a PAGE-level one: there the game's Space is a
        /// screen-level shortcut rather than a mode of its own, and a player pressing it on a row
        /// expects to pick something up or to get nothing
        /// (<see cref="ES2Access.Screens.SystemManagementScreen.SwallowsCarryKey"/>).</summary>
        private static bool CarryKeyClaimed()
        {
            GraphNavigator navigator = Navigator;
            return navigator != null
                && (navigator.TakesCarryKey() || SystemManagementScreen.SwallowsCarryKey());
        }

        /// <summary>The other half of that page-level claim: the carry key on a row with nothing to pick
        /// up is CONSUMED there rather than handed back, and silently - see
        /// <see cref="ES2Access.Screens.SystemManagementScreen.SwallowsCarryKey"/>. Returning true is
        /// also what latches the key until it is released, which is what stops the game acting on a
        /// press the mod has already answered.</summary>
        private static bool SwallowedCarry(string actionKey)
        {
            return actionKey == UiActions.Carry && SystemManagementScreen.SwallowsCarryKey();
        }

        /// <summary>The claim half of Delete: only where the focused control is one that empties
        /// (<see cref="GraphNavigator.TakesClearKey"/>), which today is a key-binding cell.</summary>
        private static bool ClearOffered()
        {
            GraphNavigator navigator = Navigator;
            return navigator != null && navigator.TakesClearKey();
        }

        /// <summary>Whether the focused screen is taking this key as typed text rather than leaving
        /// it to the game - asked by the game's own key scan, several times a frame.</summary>
        private static bool TypedKeyClaimed(KeyCode key)
        {
            GraphNavigator navigator = Navigator;
            return navigator != null && navigator.TakesTypedKey(key);
        }

        /// <summary>
        /// The default keys. The keys that move something - arrows, Tab, and the coarse slider
        /// steps - repeat while held, at the player's own OS repeat rate; the rest fire once,
        /// because nobody wants to activate a button eight times for leaning on Enter.
        ///
        /// The review-buffer chords deliberately do NOT repeat: a line at a time, on purpose, is how
        /// you read something you are trying to understand.
        /// </summary>
        private static void BindKeys(ModInput input)
        {
            input.Register(UiActions.Up).Bind(KeyCode.UpArrow).Repeating();
            input.Register(UiActions.Down).Bind(KeyCode.DownArrow).Repeating();
            input.Register(UiActions.Left).Bind(KeyCode.LeftArrow).Repeating();
            input.Register(UiActions.Right).Bind(KeyCode.RightArrow).Repeating();
            input.Register(UiActions.Next).Bind(KeyCode.Tab).Repeating();
            input.Register(UiActions.Prev).Bind(KeyCode.Tab, shift: true).Repeating();
            input.Register(UiActions.Activate).Bind(KeyCode.Return).Bind(KeyCode.KeypadEnter);
            // The other thing Enter could have meant, on the controls that have one - the game's own
            // ALT-click (queue at the head of a queue, and every other modified click it reads Alt
            // for). It is NOT on Alt and Enter, which is what the gesture it stands for would want:
            // Alt+Enter is Unity's own built-in fullscreen toggle, handled inside the player's D3D11
            // window code below every managed layer, so the mod's claim never reaches it and the
            // window resizes on every press. Nothing suppressible reaches it (boot flags, a window
            // subclass, DXGI's MakeWindowAssociation - all rejected, bug 17), so the chord moved to
            // Control+Shift+Enter, which the game binds nothing to and which keeps the family: every
            // modified click is still a modified Enter (owner ruling 2026-08-19).
            //
            // The cost of the move, and the reason the screens that HAVE an alt-click wire
            // OnAlternate rather than leaning on the fall-back: the plain-click fall back replays the
            // click with whatever the player is physically holding, and what they are holding is now
            // Control and Shift, not Alt. A game handler reading Input.IsAltKeyDown() inside its own
            // click no longer sees it.
            input.Register(UiActions.Alternate)
                .Bind(KeyCode.Return, ctrl: true, shift: true)
                .Bind(KeyCode.KeypadEnter, ctrl: true, shift: true);
            input.Register(UiActions.Secondary).Bind(KeyCode.Backspace);
            // The right click, which in this game is a command in its own right rather than a menu.
            // Claimed on every screen of ours, because it always answers - with the control's command
            // where there is one, and with a cue where there is not.
            //
            // Control and Backslash is the game's own CONTROL and right click, and it is the SAME
            // action deliberately: the game runs one handler for both and reads the modifier inside it
            // (`GalaxyGarrisonCursor.GetGalaxyPathToPosition` :453 asks `Input.IsControlKeyDown()` while
            // plotting the move, and answers with a free-movement course). So the chord is bound rather
            // than wired - the physical Control is still held when the handler runs, which is how the
            // GAME's rule applies rather than a copy of it, exactly as the modified Enter chords do.
            // Everywhere the modifier means nothing to the game, the chord is the plain right click.
            input.Register(UiActions.Contextual)
                .Bind(KeyCode.Backslash)
                .Bind(KeyCode.Backslash, ctrl: true);
            // The game's own DOUBLE click, on the one Enter chord the game itself never uses: no
            // handler in the game combines Control and Alt with a click, and its own binding matcher
            // is exact-modifier (`InputManager.InputsMatch`), so Control+Alt+Enter cannot trip a
            // binding on plain Return either. Claimed on every screen of ours like the rest, and
            // silent on a control that has no second click.
            input.Register(UiActions.DoubleClick)
                .Bind(KeyCode.Return, ctrl: true, alt: true)
                .Bind(KeyCode.KeypadEnter, ctrl: true, alt: true);
            // Pick something up, swap it, or put it back where it came from. Where it is put DOWN is
            // named with Enter, on the control that will take it. The one key here the game keeps a
            // share of: it is only taken where the cursor is standing on something that can be picked
            // up, or while something is already being carried - see CarryKeyClaimed.
            input.Register(UiActions.Carry).Bind(KeyCode.Space).ClaimedWhile(CarryKeyClaimed);
            // The game's own two modified clicks on a list: Control adds one item to the selection or
            // takes it out, Shift takes everything from the last one to this one. The physical
            // modifier stays held while the control's own handler runs, which is how the game's own
            // selection rules - not a copy of them - decide what happens.
            input.Register(UiActions.SelectToggle)
                .Bind(KeyCode.Return, ctrl: true)
                .Bind(KeyCode.KeypadEnter, ctrl: true);
            input.Register(UiActions.SelectRange)
                .Bind(KeyCode.Return, shift: true)
                .Bind(KeyCode.KeypadEnter, shift: true);
            input.Register(UiActions.Back).Bind(KeyCode.Escape);
            input.Register(UiActions.Home).Bind(KeyCode.Home);
            input.Register(UiActions.End).Bind(KeyCode.End);
            input.Register(UiActions.RegionPrev).Bind(KeyCode.UpArrow, alt: true).Repeating();
            input.Register(UiActions.RegionNext).Bind(KeyCode.DownArrow, alt: true).Repeating();
            // The coarse step is the same arrow with Shift on it, which is where a player already
            // expects "the bigger version of this move" to live. Exact-modifier matching is what
            // makes it safe: the plain arrow binding declares Shift off, so it stays silent while
            // Shift is held and the chord never moves the cursor as well as the value.
            input.Register(UiActions.CoarseIncrease)
                .Bind(KeyCode.RightArrow, shift: true)
                .Repeating();
            input.Register(UiActions.CoarseDecrease)
                .Bind(KeyCode.LeftArrow, shift: true)
                .Repeating();

            // Turning the PAGE - the previous or next system, planet, notification, hero - wherever the
            // cursor is standing on a screen that draws such a pair (Screen.PagePrev/PageNext). Alt and
            // the side arrows, beside Alt and the vertical ones which are the region jump; not
            // repeating, because each press re-binds a whole page and leaning on the key would race
            // through six systems.
            //
            // No conditional claim: the arrows are already the mod's on every screen of ours.
            //
            // These share their chords with the galaxy inspect cursor's travel keys, and BOTH actions
            // fire on the press - ModInput.Tick delivers every action whose chord matches, with no
            // first-wins rule anywhere in it. They coexist because no screen answers both: only the
            // galaxy map answers the inspect pair, and it draws no page pair.
            input.Register(UiActions.PagePrev).Bind(KeyCode.LeftArrow, alt: true);
            input.Register(UiActions.PageNext).Bind(KeyCode.RightArrow, alt: true);

            // Straight to a named panel, from anywhere on the page that draws it. Each is taken from the
            // game only while the focused screen DECLARES that panel's stop, which is the same question
            // as "would this key do anything" - and the handler asks it again, because a claim is only
            // about what the game may see (ModEntry.Dispatch -> GraphNavigator.FocusStop, which answers
            // false and leaves the press alone where the stop is absent).
            //
            // The chords are free in this game: Control+H is a debug high-definition toggle that only
            // exists in an internal build, Control+E is bound to a debug action with no handler at all,
            // and Control+N, Control+G, Control+T and Control+Alt+E are bound to nothing
            // (`InputManager.cs`). The LETTERS cost the player nothing either: A-Z are already claimed
            // by the mod's type-ahead wherever one of its screens is focused.
            input
                .Register(UiActions.FocusEmpire)
                .Bind(KeyCode.H, ctrl: true)
                .ClaimedWhile(EmpireStopDeclared);
            input
                .Register(UiActions.FocusNotifications)
                .Bind(KeyCode.N, ctrl: true)
                .ClaimedWhile(NotificationStopDeclared);
            input
                .Register(UiActions.FocusTurn)
                .Bind(KeyCode.E, ctrl: true)
                .ClaimedWhile(TurnStopDeclared);
            input
                .Register(UiActions.FocusTurnLog)
                .Bind(KeyCode.T, ctrl: true)
                .ClaimedWhile(TurnLogStopDeclared);
            input
                .Register(UiActions.FocusMap)
                .Bind(KeyCode.G, ctrl: true)
                .ClaimedWhile(MapStopDeclared);
            // Hear what the galaxy IS - the shape and size it was generated at, how far across it
            // really is, and where its middle lies from home. Beside the go-to-the-map key because it
            // is the same family: one chord, answered by the map, from anywhere on the map's page.
            // Control+M is free in this game (`docs/interaction.md`), and the LETTER costs nothing
            // either - A-Z are already the mod's type-ahead wherever one of its screens is focused.
            input
                .Register(UiActions.SummarizeMap)
                .Bind(KeyCode.M, ctrl: true)
                .ClaimedWhile(MapStopDeclared);
            // End the turn without walking to the button. The game's own end-turn key is the keypad
            // Enter, which the mod claims for Activate, so a mod user has no shortcut for the one thing
            // every turn ends with. Live wherever the turn controls are drawn; when the GAME refuses
            // (`GlobalHud.CanEndTurn`) the key speaks the button's own refusal rather than nothing, since
            // a global key that is silent both when it works and when it does not is unreadable.
            //
            // Every matching action fires (above), so this chord's handler is the only thing that
            // decides what Control+Alt+E does.
            input
                .Register(UiActions.EndTurn)
                .Bind(KeyCode.E, ctrl: true, alt: true)
                .ClaimedWhile(TurnStopDeclared);
            // Go to the next fleet with nothing to do - the other thing the turn corner is pressed for
            // every turn, and the one the game gives no key of its own: its ~70 bindings hold no idle
            // fleet action at all and `EndTurnWindow.NextIdleFleetButton` is a plain button. Control+Alt+F
            // is free: the game's closed set binds bare F to nothing, Control+F to Search, and its
            // matcher is exact-modifier, so a chord carrying Alt as well never reaches Search.
            //
            // Same shape as ending the turn beside it: live wherever the turn controls are drawn, and
            // when the button is refusing, the key speaks that button's own reading rather than nothing.
            input
                .Register(UiActions.NextIdleFleet)
                .Bind(KeyCode.F, ctrl: true, alt: true)
                .ClaimedWhile(TurnStopDeclared);
            // Move everything that was told to move - the third of the turn corner's every-turn buttons,
            // and the third the game gives no key for: its closed set of 70 `InputAction` names holds no
            // apply-movements action at all (the only one carrying the word is `ForceFreeMovement`, which
            // is Control+Mouse1), and `EndTurnWindow.ApplyMovementsButton` is a plain button.
            // Control+Alt+A is free: bare A is bound, but only as the battle camera's own secondary
            // (`EncounterCameraLeft:LeftArrow,A`), and the matcher is exact-modifier, so a chord carrying
            // Control and Alt never reaches it.
            //
            // Same shape as the two beside it, and the act is the button's own click - the node here
            // wires no activate override, because posting the order is all a press does.
            input
                .Register(UiActions.ApplyMovements)
                .Bind(KeyCode.A, ctrl: true, alt: true)
                .ClaimedWhile(TurnStopDeclared);

            // GO TO WHERE THIS HAPPENED: the game's own show-location button, from the keyboard
            // (docs/interaction.md). Control+L is free in this game - the input manager binds nothing
            // to L at all, confirmed live with a physical press that moved neither the camera nor a
            // word - and the letter costs the player nothing either, since A-Z are already claimed by
            // the mod's type-ahead wherever one of its screens is focused.
            //
            // Claimed only while the focused screen or the focused control offers one, and the handler
            // asks the same question again: a claim is answered before the press and the act is never
            // allowed to run on a stale yes. Where the answer is no the key is inert - no speech, no
            // move.
            input
                .Register(UiActions.GoToLocation)
                .Bind(KeyCode.L, ctrl: true)
                .ClaimedWhile(GoToLocationOffered);

            // Empty the control the cursor is on. Delete is bound to nothing at all in the game's own
            // 64 input options (measured 2026-08-23) and only its debug movers and its text areas read
            // the key directly - a text area owns the keyboard while it does, which stands the whole
            // layer down anyway. Claimed all the same only where a control really offers a clear, so
            // a key the game may one day bind stays the game's everywhere else.
            input.Register(UiActions.Clear).Bind(KeyCode.Delete).ClaimedWhile(ClearOffered);

            // The galaxy map's own mode: a square of galaxy swept with the arrows instead of the tree
            // (<see cref="ES2Access.Screens.GalaxyInspect"/>). Control and I is free in this game - the
            // input manager binds nothing at all to I - so it is bound outright; the two size keys are
            // taken from the game only while the cursor is actually up, which is what leaves the game
            // its own keypad minus (Sleep for this turn) everywhere else.
            input.Register(MapActions.Inspect).Bind(KeyCode.I, ctrl: true);
            // The plus key is three chords, not one: on most layouts "+" is Shift and the equals key,
            // and the mod's binding matcher is exact-modifier - so the bare key, the shifted one and
            // the keypad's own plus all have to be declared or the player who really presses "+" gets
            // nothing.
            input
                .Register(MapActions.InspectGrow)
                .Bind(KeyCode.Equals)
                .Bind(KeyCode.Equals, shift: true)
                .Bind(KeyCode.Plus)
                .Bind(KeyCode.KeypadPlus)
                .ClaimedWhile(ES2Access.Screens.GalaxyInspect.KeysClaimed);
            input
                .Register(MapActions.InspectShrink)
                .Bind(KeyCode.Minus)
                .Bind(KeyCode.KeypadMinus)
                .ClaimedWhile(ES2Access.Screens.GalaxyInspect.KeysClaimed);
            // Go to the next INTERESTING cell - a whole stretch of unchanging map crossed in one
            // press. Shift and an arrow, which is where "the bigger version of this move" already
            // lives: east and west are the coarse-step actions above, which the cursor takes for
            // itself while it is driving the map (a coarse step means nothing there), so only the
            // two vertical chords need declaring. Repeating, like the coarse step they join.
            //
            // No conditional claim: the arrows are claimed from the game outright for ordinary
            // navigation and a claim is per key code, so these chords are already the mod's wherever
            // one of its screens is focused. What keeps them harmless everywhere else is that
            // nothing but the inspect cursor answers them.
            input
                .Register(MapActions.InspectSkipNorth)
                .Bind(KeyCode.UpArrow, shift: true)
                .Repeating();
            input
                .Register(MapActions.InspectSkipSouth)
                .Bind(KeyCode.DownArrow, shift: true)
                .Repeating();
            // Travel by what the cell holds: west along the one lane in it, or east to where the
            // fleets in it are going. Alt and the side arrows - beside Alt and the vertical ones,
            // which are the region jump - and not repeating, because each press is a jump across the
            // map rather than a step.
            input.Register(MapActions.InspectFollowWest).Bind(KeyCode.LeftArrow, alt: true);
            input.Register(MapActions.InspectFollowEast).Bind(KeyCode.RightArrow, alt: true);

            // The map's SCANNER (<see cref="ES2Access.Screens.GalaxyScanner"/>): three tiers of "what
            // is near me" on one pair of keys, each tier a different modifier - the kind of thing, the
            // way of looking at that kind, and one thing at a time. Stepping repeats, because sweeping
            // a list of forty systems is what the key is for; changing scope does not, because
            // there are only ever a handful of scopes and leaning on the key would race past them.
            //
            // PageUp and PageDown are the GAME's keyboard zoom, and the game must keep them: the
            // claim is conditional on a modifier being physically held, which is the only thing that
            // tells the chord from the bare press for a camera that reads its binding's key codes and
            // ignores its modifiers - see GalaxyScanner.KeysClaimed. The claim is conditional on the
            // tree cursor standing on the MAP WIDGET too: these are keys of that widget, exactly like
            // the inspect cursor's, and on the zoom slider or the HUD buttons they are inert.
            input
                .Register(MapActions.ScanCategoryNext)
                .Bind(KeyCode.PageDown, ctrl: true)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.KeysClaimed);
            input
                .Register(MapActions.ScanCategoryPrev)
                .Bind(KeyCode.PageUp, ctrl: true)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.KeysClaimed);
            input
                .Register(MapActions.ScanSubcategoryNext)
                .Bind(KeyCode.PageDown, shift: true)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.KeysClaimed);
            input
                .Register(MapActions.ScanSubcategoryPrev)
                .Bind(KeyCode.PageUp, shift: true)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.KeysClaimed);
            input
                .Register(MapActions.ScanNext)
                .Bind(KeyCode.PageDown, alt: true)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.KeysClaimed)
                .Repeating();
            input
                .Register(MapActions.ScanPrev)
                .Bind(KeyCode.PageUp, alt: true)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.KeysClaimed)
                .Repeating();
            // Go to what the scanner is pointing at. Alt and Home, beside the two Alt keys that
            // stepped the list - and Control+Home stays the review buffer's first line, plain Home the
            // first control of the stop.
            input
                .Register(MapActions.ScanGoTo)
                .Bind(KeyCode.Home, alt: true)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.KeysClaimed);

            // THE THREE CATEGORIES THE PLAYER MADE, one key each and the same key shifted for the
            // way back (owner-approved 2026-08-23). Comma, period and slash: three keys in a row
            // under the right hand, bound to nothing at all in the game's own defaults and taken by
            // nothing in the mod - the type-ahead reads letters and space and never punctuation - so
            // the claim needs no physical-modifier trick the way the Page keys did. Not repeating:
            // every press is a jump across the map, and leaning on the key would race past the
            // landings it is making.
            input
                .Register(MapActions.ScanCustom1Next)
                .Bind(KeyCode.Comma)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.QuickKeysClaimed);
            input
                .Register(MapActions.ScanCustom1Prev)
                .Bind(KeyCode.Comma, shift: true)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.QuickKeysClaimed);
            input
                .Register(MapActions.ScanCustom2Next)
                .Bind(KeyCode.Period)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.QuickKeysClaimed);
            input
                .Register(MapActions.ScanCustom2Prev)
                .Bind(KeyCode.Period, shift: true)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.QuickKeysClaimed);
            input
                .Register(MapActions.ScanCustom3Next)
                .Bind(KeyCode.Slash)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.QuickKeysClaimed);
            input
                .Register(MapActions.ScanCustom3Prev)
                .Bind(KeyCode.Slash, shift: true)
                .ClaimedWhile(ES2Access.Screens.GalaxyScanner.QuickKeysClaimed);

            // THE TEN MAP BOOKMARKS, and the home system beside them (owner-approved 2026-08-31).
            // Shift and a digit makes one, Control and the same digit goes back to it, in the digit
            // order the player counts them in - 1 to 9 and then 0, which is the tenth slot and not a
            // zeroth. The top-row digits alone: the keypad's are a different key and nothing here
            // asks for them.
            //
            // The chords are free. The game's only digit bindings are its DebugSwitchToEmpire
            // actions, which are dead behind an accessibility gate no shipped build opens
            // (measured 2026-08-31), and it binds nothing at all to C. The mod's type-ahead takes
            // letters and space and drops every other character, so Shift and a digit is never
            // typing. Claimed only while the galaxy page is up and the game's scan lens is off
            // (<see cref="ES2Access.Screens.GalaxyBookmarks.KeysClaimed"/>) - under the lens the
            // digits go back to the game whole.
            //
            // Not repeating: every press is a jump, or a place being written down.
            for (int slot = 0; slot < MapActions.BookmarkSet.Length; slot++)
            {
                input
                    .Register(MapActions.BookmarkSet[slot])
                    .Bind(BookmarkDigits[slot], shift: true)
                    .ClaimedWhile(ES2Access.Screens.GalaxyBookmarks.KeysClaimed);
            }

            for (int slot = 0; slot < MapActions.BookmarkGoTo.Length; slot++)
            {
                input
                    .Register(MapActions.BookmarkGoTo[slot])
                    .Bind(BookmarkDigits[slot], ctrl: true)
                    .ClaimedWhile(ES2Access.Screens.GalaxyBookmarks.KeysClaimed);
            }

            input
                .Register(MapActions.BookmarkHome)
                .Bind(KeyCode.C, ctrl: true)
                .ClaimedWhile(ES2Access.Screens.GalaxyBookmarks.KeysClaimed);

            input.Register(BufferActions.LineUp).Bind(KeyCode.UpArrow, ctrl: true);
            input.Register(BufferActions.LineDown).Bind(KeyCode.DownArrow, ctrl: true);
            input.Register(BufferActions.Prev).Bind(KeyCode.LeftArrow, ctrl: true);
            input.Register(BufferActions.Next).Bind(KeyCode.RightArrow, ctrl: true);
            input.Register(BufferActions.First).Bind(KeyCode.Home, ctrl: true);
            input.Register(BufferActions.Last).Bind(KeyCode.End, ctrl: true);

            // The chord the game's chat key sits on is handed back to the game rather than declared
            // here, because it follows a binding the player can change - see GameChatKey.
        }

        /// <summary>The ten digit keys, in SLOT order - 1 to 9 and then 0, the order
        /// <c>MapBookmarks.Digits</c> and <see cref="MapActions.BookmarkSet"/> are both in.</summary>
        private static readonly KeyCode[] BookmarkDigits = new KeyCode[]
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
            KeyCode.Alpha5,
            KeyCode.Alpha6,
            KeyCode.Alpha7,
            KeyCode.Alpha8,
            KeyCode.Alpha9,
            KeyCode.Alpha0,
        };

        /// <summary>
        /// Teach the announcer and the table emitter the mod's own wording, and hand it the live drag so
        /// that a control that can be picked up or dropped onto says so. These are static because every
        /// control's readout passes through them; Stop drops them again. Called AFTER the navigator
        /// exists, because the drag is the navigator's.
        ///
        /// PartFilter is deliberately left unset, which means every announcement part speaks. It is
        /// the hook for per-control-type announcement settings, and there is no settings UI yet.
        /// </summary>
        private static void InstallAnnouncerWording()
        {
            GraphAnnouncer.Carry = Carry;
            GraphAnnouncer.PositionText = (index, count) =>
                new MessageBuilder().PushFraction(index, count).Build();
            GraphAnnouncer.ExpandedStateText = expanded =>
                ModStrings.Get(expanded ? ModStrings.NavExpanded : ModStrings.NavCollapsed);

            // How a USAGE HINT spells the gesture it names. Core cannot see the input manager, so the
            // renderer is injected here and reads the LIVE action table - a re-bound gesture re-words
            // every hint that names it, with nothing to keep in step.
            NodeHints.Chord = (actionKey, bindingIndex) =>
                ChordNames.Of(Input, actionKey, bindingIndex);

            // The carry's own three gestures, named to Core so its pick-up announcement and its two
            // derived hints spell whatever chords those actions are bound to now.
            CarryState.PickUpAction = UiActions.Carry;
            CarryState.DropAction = UiActions.Activate;
            CarryState.CancelAction = UiActions.Back;

            // And the game's own two drag noises, so the keyboard's carry sounds like the mouse's
            // drag. Which cargo has a sound at all is CarrySounds' answer, not the carry's.
            Carry.Started = CarrySounds.Started;
            Carry.Ended = CarrySounds.Ended;

            GraphSheet.BlankText = () => ModStrings.Get(ModStrings.NavCellEmpty);
            GraphSheet.TableRoleText = () => ModStrings.Get(ModStrings.NavTable);
            GraphSheet.TextCellType = ControlTypes.Text;
        }

        /// <summary>
        /// Hand everything back that <see cref="Start"/> acquired - and hand ALL of it back even when
        /// one hand-back throws. Every step below runs inside <see cref="Step"/>, so a step that fails
        /// is logged by name and the sequence carries on; the steps are independent, and skipping the
        /// tail of the teardown leaks state that outlives this assembly with nothing left able to free
        /// it. The speech context is the step that made this the rule: it is deliberately LAST (a
        /// screen still speaking while it shuts down would be talking through a freed backend), and a
        /// throw anywhere above it used to skip it, leaking the process-wide Prism context - every
        /// later load then failed with "prism_init returned null context" for the rest of the session.
        /// The order below is otherwise unchanged, because it is the reverse of the acquisitions.
        /// </summary>
        public static void Stop()
        {
            Step("host", () =>
            {
                if (_host != null)
                {
                    _host.SetUpdateHandler(null);
                    _host.StopAllCoroutines();
                    _host.UnregisterAllModRoutes();
                    _host = null;
                }
            });

            Step("dev routes", () =>
            {
                if (_routes != null)
                {
                    _routes.Unregister();
                    _routes = null;
                }
            });

            // The screen's own static, so it outlives this assembly unless it is handed back.
            Step("notification audit", NotificationAudit.Disarm);

            Step("screens", () =>
            {
                if (Screens != null)
                {
                    Screens.Shutdown();
                    Screens = null;
                }
            });

            // Before the buffers go: the log hands its subscription back to the game's chat service,
            // which outlives the mod and would otherwise keep calling into this assembly.
            Step("chat log", () =>
            {
                if (_chat != null)
                {
                    _chat.Stop();
                    _chat = null;
                }
            });

            Step("chat field", () =>
            {
                if (_chatField != null)
                {
                    _chatField.Stop();
                    _chatField = null;
                }
            });

            // And the same for the saving watcher: the window service outlives the mod, so the
            // subscription goes back before this assembly does.
            Step("saving watcher", () =>
            {
                if (_saving != null)
                {
                    _saving.Stop();
                    _saving = null;
                }
            });

            // Nothing subscribed and nothing held: the rung watcher only reads, so letting go of it is
            // the whole of its teardown. The route watcher and the route memo are the same - both read
            // the game and remember nothing the game owns.
            Step("zoom and fleet routes", () =>
            {
                _zoom = null;
                if (_fleetRoutes != null)
                {
                    _fleetRoutes.Forget();
                    _fleetRoutes = null;
                }

                FleetRoute.Reset();
            });

            // Same shape: the map's coordinate origin is a cached read, and letting go of it only
            // drops the empire it was taken from.
            Step("galaxy coordinates", GalaxyCoordinates.Forget);
            // And the constellation outlines derived from it, which hold the galaxy's own nodes.
            Step("constellation map", ConstellationMap.Forget);
            // A constellation NAME the mod was holding drawn is a change to what the game is showing,
            // so it is put back rather than merely let go of - a reload that left one drawn would leave
            // it drawn for the rest of the session with nothing left to take it down.
            Step("constellation label hold", ConstellationLabelHold.Release);
            // And the map's inspect cursor, whose lines the page gave back when it was popped just
            // above: what is left is the flag the input layer's own claim reads.
            Step("galaxy inspect", GalaxyInspect.Reset);

            // Whatever the mod made the game look like, the game looks like itself again. The screens
            // shut down first, so a drop list left open has already been closed by its own OnPop and
            // this only drops the record of it. A key capture has no such hook - the game holds the
            // keyboard, not us - so it is ended here, binding nothing.
            Step("drop list", DropListScreen.Reset);
            Step("table filter", TableFilterScreen.Reset);
            Step("key capture", OptionsScreen.ReleaseCapture);
            // The mod's own settings window is a GameObject in one of the game's window stacks and
            // an entry in two of its registries: it comes out by NAME, because after the swap this
            // load's types match nothing the old load put there.
            Step("mod options window", ModOptions.Shutdown);
            Step("pointer focus", PointerFocus.Shutdown);
            // After the pointer has let go: the carriers are scene objects this assembly created, and
            // destroying one the pointer is still aimed at would leave the engine holding a dead
            // transform.
            Step("scratch tooltips", ScratchTooltips.Shutdown);
            Step("game windows", GameWindows.Shutdown);
            Step("key stand-down patch", GameKeyStandDown.Remove);
            Step("keyboard handover patch", GameKeyboardHandover.Remove);
            Step("text focus patch", GameTextFocus.Remove);
            Step(
                "settings text field patch",
                ES2Access.UI.ModOptions.OptionTextFieldCommit.Remove
            );
            Step("binding overlap patch", BindingOverlaps.Remove);
            // And the edit that patch was watching, so the next load's first keystroke has nothing
            // left over to speak about.
            Step("text field editor", TextFieldEditor.Stop);
            Step("chat escape patch", ChatEscape.Remove);
            // And the chat panel is let go, or the game keeps drawing one nobody is reading.
            Step("chat hold", ChatHold.Stop);
            Step("research locate patch", ResearchLocate.Remove);
            Step("galaxy locate patch", GalaxyLocate.Remove);
            Step("galaxy overview entry patch", GalaxyOverviewEntry.Remove);
            Step("galaxy pick patch", GalaxyPick.Remove);
            // Before the patch comes off: the mappings are removed from the game's own dictionary and
            // every mod notification still standing is dismissed, so nothing from this assembly is
            // left in a list the game will keep asking for titles.
            Step("mod notifications", ModNotifications.Stop);
            Step("notification strip", NotificationStrip.Remove);
            // And the two detection points that feed it, each giving back its patch, its
            // subscription and what it was remembering about the galaxy.
            Step("fleet arrivals", FleetArrivals.Remove);
            Step("foreign fleet watch", ForeignFleetWatch.Remove);
            Step("battle replay stream", BattleStream.Remove);
            Step("probe cancel selection", ProbeCancelSelection.Remove);
            Step("influence ground watch", InfluenceGroundWatch.Remove);

            Step("input", () =>
            {
                if (Input != null)
                {
                    Input.Dispatch = null;
                    Input.DrivenByMod = null;
                    Input.HasFocusedScreen = null;
                    Input.ClaimsBackKey = null;
                    Input.ClaimsTypedKey = null;
                    // A dev request waiting for an injected action to run is waiting for a frame that
                    // will never come now; it is told so rather than left to time out.
                    Input.CancelInjections();
                    Input = null;
                }

                Navigator = null;
                Buffers = null;
            });

            // Process-wide state the announcer and the sheet reader hold on our behalf: left
            // installed, they would keep calling into an assembly nobody can reach any more.
            Step("announcer wording", GraphAnnouncer.Reset);
            Step("sheet wording", GraphSheet.Reset);
            Step("hint wording", NodeHints.Reset);

            // After the input layer has gone, because the binding store reads it: what the player
            // chose is already on disk (the window wrote it when it hid), so this only lets go.
            Step("bindings", ModBindings.Reset);
            Step("scanner categories", ScannerCustomSettings.Reset);
            // Every bookmark a saved campaign has is already on disk (the set wrote it), so this only
            // lets go of the game the store was watching.
            Step("bookmarks", MapBookmarkStore.Reset);
            Step("settings", ModSettings.Reset);

            Step("locale", ModLocale.Reset);
            Step("mod strings", ModStrings.Reset);

            Step("speech", () =>
            {
                if (Speech != null)
                {
                    Speech.Shutdown();
                    Speech = null;
                }
            });
        }

        /// <summary>
        /// One teardown step, isolated: a step that throws is logged by name and the rest of
        /// <see cref="Stop"/> still runs. Nothing in here may throw - even the log call is guarded,
        /// because a logger that fails while the mod is coming down must not be the thing that strands
        /// every step after it. Allocating a delegate per step is a reload-time cost, not a per-frame
        /// one.
        /// </summary>
        private static void Step(string what, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                try
                {
                    Log.Error("teardown step '" + what + "' threw, carrying on: " + e);
                }
                catch
                {
                    // Nothing left that can report this, and the remaining steps still matter more.
                }
            }
        }

        /// <summary>
        /// A screen reader user does not necessarily have the game window in front: a review window,
        /// the screen reader's own settings, or a whole other application can hold focus while the game
        /// is meant to keep going. Unity stops simulating an unfocused player unless this is set, which
        /// would freeze the game mid-turn under someone who is still listening to it.
        ///
        /// The loader sets it once at startup; this re-asserts it because the setting is Unity's and
        /// nothing tells us when the engine has reason to reset it (a resolution or fullscreen change
        /// from the video options is the plausible one). Nothing in the game's own code ever writes it,
        /// so there is no setting here to fight - and the write only happens if something turned it
        /// off, which makes an actual write the interesting event rather than a per-frame cost.
        /// </summary>
        private static void KeepSimulatingUnfocused()
        {
            if (!Application.runInBackground)
            {
                Application.runInBackground = true;
                Log.Info("something switched off background simulation; switched it back on");
            }
        }

        // Harmony hooks and watchers only set state; all speech happens from this pump,
        // once per frame, so ordering and interruption stay deterministic.
        private static void Update()
        {
            KeepSimulatingUnfocused();
            ModLocale.Tick();

            // Before the screens build: which campaign's bookmarks are in hand decides what the map's
            // rows say, and the answer changes on the frame a save is loaded or a new game begins.
            MapBookmarkStore.Tick();

            // Before the keys are polled, and idempotent: the game's chat key has to be off the mod's
            // keys for the layer's suppression to leave a way into chat at all.
            GameChatKey.Tick();

            // One Unity null check in the ordinary case: the mod's settings window is built once the
            // game has finished loading its own, and built again if a runtime change ever takes it
            // away with the rest of them.
            ModOptions.Tick();

            // Before the keys are polled: a window the game hid while its text field held the engine's
            // keyboard would otherwise leave the whole layer standing down for a field nobody can see.
            GameKeyboardHandover.Tick();

            // Keys first, screens second: a keypress and the announcement it causes then land in
            // the same frame, instead of the player hearing the result of the previous one.
            Input.Tick();

            // Right after the keys and before the screens rebuild: the hold is decided on where the
            // cursor now IS, and letting go has to reach the panel before the frame's Build asks it
            // what it is drawing.
            ChatHold.Tick();

            // Typed characters are not one of the mod's actions - they are text, and the focused
            // screen's type-ahead search decides for itself whether it is listening for them.
            Navigator.TypeAheadTick();

            // BEFORE the screens, because all this does is notice that the game has drawn a tooltip and
            // mark the focused control's buffer stale: raised after the screens, the refill it asks for
            // happens on the NEXT frame, and that frame is a quarter of the wait between arrowing onto
            // a control and hearing what it says. Pointing the engine at the focus is a different job
            // and stays at the bottom of the frame (<c>PointerFocus.Tick</c>).
            PointerFocus.WatchDrawn();

            Screens.Tick();

            // After the screens, because the screens are where an edit is ASKED for: the hand-over
            // lands in Screens.Tick and this reads the box it landed in on the next frame. Everything
            // a live edit says - the character typed, the one under the caret, "edited", "Cancelled" -
            // is spoken from here, off state the focus patch and the box itself have already settled.
            TextFieldEditor.Tick();

            // After the screens, and for the same reason the inspect cursor's line is: what the
            // naming box handed back is applied here, and both of its refusals follow that box
            // closing - a screen's arrival interrupts, and a sentence queued before it is thrown
            // away.
            ES2Access.UI.ModOptions.ScannerEditor.Tick();

            // Right after the screens, and only ever with something to say: the map's inspect cursor
            // ends when the map stops being the page the player is on, and the line saying so has to
            // land AFTER whatever took the page over has announced itself - a screen's arrival
            // interrupts, and anything queued before it is thrown away.
            GalaxyInspect.Tick();

            // After the screens, so that reading the cursor's control back on the way out of the chat
            // box reads the graph as the screens have just left it.
            _chatField.Tick();

            // After the screens: a screen's arrival announcement interrupts, and a chat line queued
            // before it would be thrown away. Chat lines queue behind whatever the player asked for.
            _chat.Tick();

            // Alongside the chat, and for the same reason: a save being written is news the game gives
            // no words to, and it queues behind whatever the player asked for.
            _saving.Tick();

            // After the screens, so that a zoom step which also changes the page reads as the page and
            // then the distance rather than the other way round.
            _zoom.Tick();

            // Alongside the save spinner and for the same reason: a route dying is news the game gives
            // no words to, and it queues behind whatever the player asked for.
            _fleetRoutes.Tick();

            // Just before the notifications are spoken, because these two are what RAISES some of
            // them: a fleet of the player's arriving, and somebody else's fleet lost from sight or
            // moved since the turn began. Raising from the pump rather than from their hooks is what
            // lets the line land in the same frame the news does.
            FleetArrivals.Tick();
            ForeignFleetWatch.Tick();
            // And the third: ground one of the player's own systems held at the last turn boundary
            // and does not hold at this one. It subscribes to the turn itself, so there is nothing to
            // install - only this tick and the teardown.
            InfluenceGroundWatch.Tick();

            // The same lane again, for the eight things the game puts on its event bus and then never
            // mentions - a system revealed, a fleet sighted, a siege, an Obliterator - and the five
            // the mod raises itself. This also re-asserts the event mappings, which a new game or a
            // loaded save wipes.
            ModNotifications.Tick();

            // After the screens have settled: the game's own hover, flyout and tooltip follow the
            // focus they just decided on.
            PointerFocus.Tick();

            // Announce in the player's language if the game has told us what it is; give up
            // waiting after a few seconds rather than lose the announcement.
            if (
                !_announcedStartup
                && (
                    ModLocale.LanguageResolved
                    || Time.realtimeSinceStartup - _startTime >= LanguageWaitSeconds
                )
            )
            {
                _announcedStartup = true;
                Speech.Speak(
                    new MessageBuilder().Fragment(
                        ModStrings.Format(ModStrings.StartupReady, ModVersion)
                    ),
                    false
                );
            }
        }
    }
}

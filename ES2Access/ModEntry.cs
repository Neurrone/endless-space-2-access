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
using ES2Access.UI.Input;
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
        public const string ModVersion = "0.1.0";

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

        /// <summary>The mod's keys. Public for POST /eval.</summary>
        public static ModInput Input;

        private static ModHost _host;
        private static ModRoutes _routes;
        private static bool _announcedStartup;
        private static float _startTime;

        public static void Start(ModHost host)
        {
            _host = host;
            _startTime = Time.realtimeSinceStartup;
            _announcedStartup = false;

            Log.Install(host.LogInfo, host.LogWarning, host.LogError);
            ModLocale.PluginDirectory = host.PluginDirectory;

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

            InstallAnnouncerWording();
            Buffers = new BufferController();
            Navigator = new GraphNavigator(Buffers);
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
            Screens.Register(new ImprovementsModalScreen());
            Screens.Register(new RenameModalScreen());
            Screens.Register(new TutorialSelectionScreen());
            Screens.Register(new global::ES2Access.Screens.NewGameScreen());
            Screens.Register(new AdvancedSettingsScreen());
            Screens.Register(new FactionChoiceScreen());
            Screens.Register(new CustomFactionScreen());
            Screens.Register(new PlanetConstructiblesScreen());
            Screens.Register(new ResearchScreen());
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
            // Bindings first: the game's scans ask the layer which keys it has, so there must be
            // something to answer with before they can be told to stand down.
            GameKeyStandDown.Install();

            _routes = new ModRoutes(host);
            _routes.Register();
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

            return Buffers.Dispatch(action.Key) || Navigator.Dispatch(action.Key);
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
            if (navigator != null && navigator.SearchIsActive)
            {
                // A search is a surface the mod invented too: Escape puts the keyboard back, and
                // must not also close the screen the player was searching.
                return true;
            }

            ScreenManager screens = Screens;
            ES2Access.Screens.Screen current = screens == null ? null : screens.Current;
            return current != null && current.ConsumesBack;
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
            // The other thing Enter could have meant, on the controls that have one - Alt and Enter,
            // because Alt and a click is what the game itself puts it on.
            input.Register(UiActions.Alternate)
                .Bind(KeyCode.Return, alt: true)
                .Bind(KeyCode.KeypadEnter, alt: true);
            input.Register(UiActions.Secondary).Bind(KeyCode.Backspace);
            // Move the ITEM rather than the cursor: Shift and the arrow that says which way. Exact
            // modifier matching keeps the plain arrows navigation, the way Shift and a sideways arrow
            // is already the big version of a slider move.
            input.Register(UiActions.MoveItemUp).Bind(KeyCode.UpArrow, shift: true).Repeating();
            input.Register(UiActions.MoveItemDown).Bind(KeyCode.DownArrow, shift: true).Repeating();
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

            input.Register(BufferActions.LineUp).Bind(KeyCode.UpArrow, ctrl: true);
            input.Register(BufferActions.LineDown).Bind(KeyCode.DownArrow, ctrl: true);
            input.Register(BufferActions.Prev).Bind(KeyCode.LeftArrow, ctrl: true);
            input.Register(BufferActions.Next).Bind(KeyCode.RightArrow, ctrl: true);
            input.Register(BufferActions.First).Bind(KeyCode.Home, ctrl: true);
            input.Register(BufferActions.Last).Bind(KeyCode.End, ctrl: true);
        }

        /// <summary>
        /// Teach the announcer the mod's own wording. These are static because every control's
        /// readout passes through them; Stop drops them again.
        ///
        /// PartFilter is deliberately left unset, which means every announcement part speaks. It is
        /// the hook for per-control-type announcement settings, and there is no settings UI yet.
        /// </summary>
        private static void InstallAnnouncerWording()
        {
            GraphAnnouncer.PositionText = (index, count) =>
                new MessageBuilder().PushFraction(index, count).Build();
            GraphAnnouncer.ExpandedStateText = expanded =>
                ModStrings.Get(expanded ? ModStrings.NavExpanded : ModStrings.NavCollapsed);
        }

        public static void Stop()
        {
            if (_host != null)
            {
                _host.SetUpdateHandler(null);
                _host.StopAllCoroutines();
                _host.UnregisterAllModRoutes();
                _host = null;
            }

            if (_routes != null)
            {
                _routes.Unregister();
                _routes = null;
            }

            if (Screens != null)
            {
                Screens.Shutdown();
                Screens = null;
            }

            // Whatever the mod made the game look like, the game looks like itself again. The screens
            // shut down first, so a drop list left open has already been closed by its own OnPop and
            // this only drops the record of it. A key capture has no such hook - the game holds the
            // keyboard, not us - so it is ended here, binding nothing.
            DropListScreen.Reset();
            OptionsScreen.ReleaseCapture();
            PointerFocus.Shutdown();
            GameKeyStandDown.Remove();

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

            // Process-wide state the announcer and the sheet reader hold on our behalf: left
            // installed, they would keep calling into an assembly nobody can reach any more.
            GraphAnnouncer.Reset();
            GraphSheet.Reset();

            ModLocale.Reset();
            ModStrings.Reset();

            if (Speech != null)
            {
                Speech.Shutdown();
                Speech = null;
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

            // Keys first, screens second: a keypress and the announcement it causes then land in
            // the same frame, instead of the player hearing the result of the previous one.
            Input.Tick();

            // Typed characters are not one of the mod's actions - they are text, and the focused
            // screen's type-ahead search decides for itself whether it is listening for them.
            Navigator.TypeAheadTick();

            Screens.Tick();

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
                    new MessageBuilder().Fragment(ModStrings.Get(ModStrings.StartupReady)),
                    false
                );
            }
        }
    }
}

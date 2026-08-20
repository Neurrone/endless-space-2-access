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
            // Bindings first: the game's scans ask the layer which keys it has, so there must be
            // something to answer with before they can be told to stand down.
            GameKeyStandDown.Install();
            // And the same for the control the game has focused, which is handed the frame's keys after
            // the mod has already acted on them.
            GameKeyboardHandover.Install();
            // And the setter every way out of a text box passes through, which is the only place a
            // commit can be told from a cancel and the last moment a cancel can put the text back.
            GameTextFocus.Install();
            // The one key that still reaches the game while it holds the keyboard for the chat box:
            // Escape, which the panel answers by shutting itself instead of letting go.
            ChatEscape.Install();
            // The game's own "the thing you are missing is over here", which moves the view and would
            // otherwise leave the cursor behind - once for the technology wheel, once for the map,
            // which between them are where every one of those buttons lands.
            ResearchLocate.Install();
            GalaxyLocate.Install();
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
        /// mode (owner decision 2026-08-12, reversing the blanket claim of 2026-08-11).</summary>
        private static bool CarryKeyClaimed()
        {
            GraphNavigator navigator = Navigator;
            return navigator != null && navigator.TakesCarryKey();
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

            input.Register(BufferActions.LineUp).Bind(KeyCode.UpArrow, ctrl: true);
            input.Register(BufferActions.LineDown).Bind(KeyCode.DownArrow, ctrl: true);
            input.Register(BufferActions.Prev).Bind(KeyCode.LeftArrow, ctrl: true);
            input.Register(BufferActions.Next).Bind(KeyCode.RightArrow, ctrl: true);
            input.Register(BufferActions.First).Bind(KeyCode.Home, ctrl: true);
            input.Register(BufferActions.Last).Bind(KeyCode.End, ctrl: true);

            // The chord the game's chat key sits on is handed back to the game rather than declared
            // here, because it follows a binding the player can change - see GameChatKey.
        }

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

            GraphSheet.BlankText = () => ModStrings.Get(ModStrings.NavCellEmpty);
            GraphSheet.TableRoleText = () => ModStrings.Get(ModStrings.NavTable);
            GraphSheet.TextCellType = ControlTypes.Text;
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

            // The screen's own static, so it outlives this assembly unless it is handed back.
            NotificationAudit.Disarm();

            if (Screens != null)
            {
                Screens.Shutdown();
                Screens = null;
            }

            // Before the buffers go: the log hands its subscription back to the game's chat service,
            // which outlives the mod and would otherwise keep calling into this assembly.
            if (_chat != null)
            {
                _chat.Stop();
                _chat = null;
            }

            if (_chatField != null)
            {
                _chatField.Stop();
                _chatField = null;
            }

            // And the same for the saving watcher: the window service outlives the mod, so the
            // subscription goes back before this assembly does.
            if (_saving != null)
            {
                _saving.Stop();
                _saving = null;
            }

            // Nothing subscribed and nothing held: the rung watcher only reads, so letting go of it is
            // the whole of its teardown. The route watcher and the route memo are the same - both read
            // the game and remember nothing the game owns.
            _zoom = null;
            if (_fleetRoutes != null)
            {
                _fleetRoutes.Forget();
                _fleetRoutes = null;
            }

            FleetRoute.Reset();
            // Same shape: the map's coordinate origin is a cached read, and letting go of it only
            // drops the empire it was taken from.
            GalaxyCoordinates.Forget();
            // And the constellation outlines derived from it, which hold the galaxy's own nodes.
            ConstellationMap.Forget();
            // A constellation NAME the mod was holding drawn is a change to what the game is showing,
            // so it is put back rather than merely let go of - a reload that left one drawn would leave
            // it drawn for the rest of the session with nothing left to take it down.
            ConstellationLabelHold.Release();
            // And the map's inspect cursor, whose lines the page gave back when it was popped just
            // above: what is left is the flag the input layer's own claim reads.
            GalaxyInspect.Reset();

            // Whatever the mod made the game look like, the game looks like itself again. The screens
            // shut down first, so a drop list left open has already been closed by its own OnPop and
            // this only drops the record of it. A key capture has no such hook - the game holds the
            // keyboard, not us - so it is ended here, binding nothing.
            DropListScreen.Reset();
            OptionsScreen.ReleaseCapture();
            PointerFocus.Shutdown();
            GameKeyStandDown.Remove();
            GameKeyboardHandover.Remove();
            GameTextFocus.Remove();
            // And the edit that patch was watching, so the next load's first keystroke has nothing
            // left over to speak about.
            TextFieldEditor.Stop();
            ChatEscape.Remove();
            // And the chat panel is let go, or the game keeps drawing one nobody is reading.
            ChatHold.Stop();
            ResearchLocate.Remove();
            GalaxyLocate.Remove();
            // Before the patch comes off: the mappings are removed from the game's own dictionary and
            // every mod notification still standing is dismissed, so nothing from this assembly is
            // left in a list the game will keep asking for titles.
            ModNotifications.Stop();
            NotificationStrip.Remove();
            // And the two detection points that feed it, each giving back its patch, its
            // subscription and what it was remembering about the galaxy.
            FleetArrivals.Remove();
            ForeignFleetWatch.Remove();

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

            // Before the keys are polled, and idempotent: the game's chat key has to be off the mod's
            // keys for the layer's suppression to leave a way into chat at all.
            GameChatKey.Tick();

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

            // The same lane again, for the eight things the game puts on its event bus and then never
            // mentions - a system revealed, a fleet sighted, a siege, an Obliterator - and the four
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

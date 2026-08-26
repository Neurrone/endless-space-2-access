using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Amplitude.Unity.Framework;
using Amplitude.Unity.Session;
using Amplitude.Unity.View;
using ES2Access.Core.UI.Graph;
using ES2Access.Loader.Dev;
using ES2Access.Screens;
using ES2Access.UI;
using ES2Access.UI.Input;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace ES2Access.Dev
{
    /// <summary>
    /// The questions a test asks over and over, each as one compile-checked call.
    ///
    /// POST /eval can reach all of this already - that is the point of the REPL - but reaching it
    /// there means writing the traversal by hand every time: a service lookup, two casts, a null
    /// check, and a string concatenation, in a language with no `using` directives, one statement per
    /// request, and a type importer that poisons an identifier for the rest of the session if a
    /// constructed generic over a game type is ever named. Every one of those questions has exactly
    /// one right answer, and every hand-written traversal is a chance to get it subtly wrong and
    /// believe the result. So they live here instead, in a file the compiler checks against the game's
    /// real API, and /eval bodies become <c>ES2Access.Dev.DevProbe.State()</c>.
    ///
    /// Everything returns JSON, and everything that can fail fails as <c>{"error": ...}</c> rather
    /// than by throwing: a probe called from a wait-loop must always answer.
    ///
    /// Main-thread only - all of it reads live game state.
    /// </summary>
    public static class DevProbe
    {
        private const int MaxSavesListed = 60;
        private const int MaxWindowsListed = 80;

        /// <summary>Passed to <see cref="TooltipDelay"/> to put the game's own value back.</summary>
        public const double RestoreTooltipDelay = -1;

        /// <summary>What the mod thinks the player is on, and where its cursor is sitting.</summary>
        public static string Screen()
        {
            return Guarded(json =>
            {
                GraphNavigator navigator = ModEntry.Navigator;
                Screens.Screen screen = navigator == null ? null : navigator.Screen;
                json.WritePropertyName("screen");
                json.WriteValue(screen == null ? null : screen.Key);
                json.WritePropertyName("screenName");
                json.WriteValue(screen == null ? null : screen.ScreenName);
                GraphNode node = navigator == null ? null : navigator.CurrentNode;
                ControlId focused = navigator == null ? null : navigator.FocusedKey;
                json.WritePropertyName("node");
                json.WriteValue(focused == null ? null : Convert.ToString(focused.StructuralKey));
                json.WritePropertyName("announcement");
                json.WriteValue(node == null ? null : GraphAnnouncer.ComposeFull(node));
            });
        }

        /// <summary>Every screen of ours the game is currently showing, bottom layer first - the top of
        /// the list is the one the keys are going to.</summary>
        public static string Stack()
        {
            return Guarded(json =>
            {
                ScreenManager screens = ModEntry.Screens;
                json.WritePropertyName("stack");
                json.WriteStartArray();
                if (screens != null)
                {
                    foreach (Screens.Screen screen in screens.Stack)
                    {
                        json.WriteStartObject();
                        json.WritePropertyName("key");
                        json.WriteValue(screen.Key);
                        json.WritePropertyName("layer");
                        json.WriteValue(screen.Layer);
                        json.WriteEndObject();
                    }
                }

                json.WriteEndArray();
            });
        }

        /// <summary>
        /// One line in the log for every frame, naming the whole of what decides where the keyboard
        /// is: the polled stack, the screen the keys are going to, the cursor, how many nodes that
        /// screen declared this frame, and the state of the tutorial popup and the three window
        /// classes the game weighs it against.
        ///
        /// Always false, so <c>POST /wait</c> on it never finishes early and records the whole
        /// passage: a transition is frames long, and polling from outside samples between them and
        /// misses exactly the frame that moved the cursor. Read the result with
        /// <c>GET /log?grep=trace</c>.
        /// </summary>
        public static bool Trace(string tag)
        {
            try
            {
                System.Text.StringBuilder line = new System.Text.StringBuilder();
                line.Append("trace ").Append(tag).Append(" f=").Append(Time.frameCount);

                ScreenManager screens = ModEntry.Screens;
                line.Append(" stack=");
                if (screens == null)
                {
                    line.Append('?');
                }
                else
                {
                    foreach (Screens.Screen screen in screens.Stack)
                    {
                        line.Append(screen.Key).Append(':').Append(screen.Layer).Append(' ');
                    }
                }

                GraphNavigator navigator = ModEntry.Navigator;
                Screens.Screen current = navigator == null ? null : navigator.Screen;
                ControlId focused = navigator == null ? null : navigator.FocusedKey;
                line.Append(" cur=").Append(current == null ? "-" : current.Key);
                line.Append(" node=")
                    .Append(
                        focused == null ? "-" : Convert.ToString(focused.StructuralKey)
                    );
                line.Append(" nodes=")
                    .Append(navigator == null ? -1 : navigator.RenderedNodeCount);
                line.Append(" ").Append(TutorialState());
                Core.Util.Log.Info(line.ToString());
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("trace threw: " + e.Message);
            }

            return false;
        }

        /// <summary>
        /// A per-frame recording of what the FOCUSED control would say - the row trace, driven from a
        /// <c>POST /wait</c> predicate exactly as <see cref="Trace"/> is.
        ///
        /// The question it answers is how long after an arrival the page is still CHANGING what it
        /// says. A landing announces once, so anything the row gains after that frame is lost, and the
        /// wait a landing spends is only defensible against a measurement of it: run this across the
        /// arrival and count the frames from the camera stopping to the last line that differs.
        /// Always false, so the wait runs to its timeout.
        /// </summary>
        public static bool RowTrace(string tag)
        {
            try
            {
                GraphNavigator navigator = ModEntry.Navigator;
                GraphNode node = navigator == null ? null : navigator.CurrentNode;
                Core.Util.Log.Info(
                    "rowtrace "
                        + tag
                        + " f="
                        + Time.frameCount
                        + " settling="
                        + GalaxyViewLevels.CameraSettling
                        + " | "
                        + (node == null ? "-" : GraphAnnouncer.ComposeFull(node))
                );
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("rowtrace threw: " + e.Message);
            }

            return false;
        }

        /// <summary>The popup as the tutorial screen's own gates see it, plus the three window classes
        /// the game hides it for.</summary>
        private static string TutorialState()
        {
            TutorialWindow window = Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<TutorialWindow>(false)
                : null;
            TutorialPopupPanel panel = window == null ? null : window.TutorialPopupPanel;
            GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            return "tut="
                + (window != null && window.Shown ? "win " : "nowin ")
                + (panel == null ? "nopanel" : (panel.IsBound ? "bound " : "unbound "))
                + (panel != null && panel.Shown ? "shown " : "hidden ")
                + (
                    panel != null && panel.MinimizeToggle != null && panel.MinimizeToggle.State
                        ? "min"
                        : "open"
                )
                + (gui == null ? "" : " gui=")
                + (gui == null ? "" : (gui.IsAnyScreenVisible ? "S" : "-"))
                + (gui == null ? "" : (gui.IsAnyModalVisible ? "M" : "-"))
                + (gui == null ? "" : (gui.IsAnyNotificationVisible ? "N" : "-"));
        }

        /// <summary>
        /// One word for what the game is doing, for a wait-script that needs to know when to start.
        /// Read off the same gates the screens themselves use, so it can never disagree with them:
        ///
        /// <list type="bullet">
        /// <item><c>booting</c> - the GUI service is not up (the same check <see cref="AgeDump"/> and
        /// every screen's window lookup makes first).</item>
        /// <item><c>loading</c> - <c>GuiManager.IsInLoadingWindow</c>, which is what
        /// <c>GalaxyHudScreen.IsActive</c> excludes and <c>LoadingScreen</c> waits for.</item>
        /// <item><c>dialog</c> - a <c>MessageBoxWindow</c> is shown and ready:
        /// <c>MessageBoxScreen.IsActive</c> exactly. It outranks the rest because a message box is
        /// what a script that thought it was in-game would silently hang behind.</item>
        /// <item><c>ingame</c> - a session with a live game client, which is the one condition that
        /// distinguishes "a game is running" from "a menu is up" whatever window is on top of it.</item>
        /// <item><c>menu</c> - everything else: no session, so the main menu, the new-game lobby, or a
        /// modal over either.</item>
        /// </list>
        /// </summary>
        public static string State()
        {
            try
            {
                return Word(Resolve());
            }
            catch (Exception e)
            {
                return Err(e.Message);
            }
        }

        private static string Resolve()
        {
            if (!Gui.GuiServiceAvailable)
            {
                return "booting";
            }

            GuiManager gui = Gui.GuiService as GuiManager;
            if (gui == null)
            {
                return "booting";
            }

            // BEFORE asking anything else. `IsInLoadingWindow` is three by-TYPE lookups with the
            // engine's error reporting ON, so polling /state during boot - which is exactly what a
            // launcher does - wrote one "Could not find GuiWindow of type 'BattleLoadingWindow'"
            // error per poll into the game's diagnostics, and the game forwards every Error to
            // Amplitude's telemetry (measured 2026-08-23, ~207 per session). Once the registry is
            // filled all three windows exist and the property is silent.
            if (!gui.GuiWindowsLoaded)
            {
                return "booting";
            }

            if (gui.IsInLoadingWindow)
            {
                return "loading";
            }

            MessageBoxWindow box = gui.GetWindow<MessageBoxWindow>(false);
            if (box != null && box.Shown && box.IsReady)
            {
                return "dialog";
            }

            return Client() != null ? "ingame" : "menu";
        }

        private static string Word(string state)
        {
            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("state");
                json.WriteValue(state);
                json.WriteEndObject();
            });
        }

        private static global::GameClient Client()
        {
            ISessionService sessions = Services.GetService<ISessionService>();
            global::Session session = sessions == null ? null : sessions.Session as global::Session;
            global::GameClient client =
                session == null ? null : session.GameClient as global::GameClient;
            return client != null && !client.Disconnecting ? client : null;
        }

        /// <summary>The saves <c>POST /loadsave</c> can be given by title, newest first.</summary>
        public static string Saves()
        {
            return Guarded(json =>
            {
                json.WritePropertyName("saves");
                json.WriteStartArray();
                IGameSerializationService saves = Services.GetService<IGameSerializationService>();
                if (saves != null)
                {
                    List<GameSaveDescriptor> all = new List<GameSaveDescriptor>(
                        saves.GetAllGameSaveDescriptors(false)
                    );
                    all.Sort((left, right) => right.DateTime.CompareTo(left.DateTime));
                    int listed = 0;
                    foreach (GameSaveDescriptor save in all)
                    {
                        if (listed++ >= MaxSavesListed)
                        {
                            break;
                        }

                        json.WriteStartObject();
                        json.WritePropertyName("title");
                        json.WriteValue(save.Title);
                        json.WritePropertyName("turn");
                        json.WriteValue(save.TurnPlusOne);
                        json.WritePropertyName("date");
                        json.WriteValue(save.DateTime.ToString("o", CultureInfo.InvariantCulture));
                        json.WriteEndObject();
                    }
                }

                json.WriteEndArray();
            });
        }

        /// <summary>
        /// Where the galaxy camera is looking. The focus point is what a test compares: activating a
        /// system or fleet centres the camera on it, and the only proof that happened is that this
        /// moved. Answers <c>bound: false</c> out of game, when the controller exists but is not
        /// driving anything and its positions are stale.
        /// </summary>
        public static string Camera()
        {
            return Guarded(json =>
            {
                ICameraService cameras = Services.GetService<ICameraService>();
                GalaxyViewCameraController galaxy =
                    cameras == null ? null : cameras.CameraController as GalaxyViewCameraController;
                json.WritePropertyName("controller");
                json.WriteValue(
                    cameras == null || cameras.CameraController == null
                        ? null
                        : cameras.CameraController.GetType().Name
                );
                if (galaxy == null)
                {
                    return;
                }

                json.WritePropertyName("bound");
                json.WriteValue(galaxy.IsCameraBinded);
                WriteVector(json, "focus", galaxy.TargetPositionCurrent);
                WriteVector(json, "eye", galaxy.CameraPositionCurrent);
                json.WritePropertyName("zoomStep");
                json.WriteValue(galaxy.ZoomStepCurrent);
                json.WritePropertyName("zoomRatio");
                json.WriteValue(Round(galaxy.ZoomRatio));
            });
        }

        /// <summary>
        /// The mod's own notifications, from both ends: the reflected entries in the game's
        /// event-to-notification dictionary (with the ASSEMBLY each maps to, which is what tells a
        /// live entry from one an unfinished teardown left behind), who is patching the strip's
        /// refresh, and every mod notification currently standing in the player's list.
        ///
        /// The three answers together are the reload check: after a hot reload every mapping must
        /// name the CURRENT assembly, the strip must have exactly one patch owner, and the list must
        /// hold nothing from the load that just ended.
        /// </summary>
        public static string Notifications()
        {
            return Guarded(json =>
            {
                json.WritePropertyName("assembly");
                json.WriteValue(typeof(ModNotifications).Assembly.GetName().Name);
                json.WritePropertyName("installed");
                json.WriteValue(ModNotifications.Installed);
                json.WritePropertyName("stripPatch");
                json.WriteValue(NotificationStrip.Installed);
                json.WritePropertyName("stripOwners");
                json.WriteStartArray();
                foreach (string owner in NotificationStrip.Owners())
                {
                    json.WriteValue(owner);
                }

                json.WriteEndArray();

                // The two detection points that raise the mod's OWN events. Each is a patch plus, for
                // the foreign-fleet watch, a subscription to a service that outlives this assembly
                // and a table of what the galaxy looked like - all three of which a teardown has to
                // give back.
                json.WritePropertyName("arrivalPatch");
                json.WriteValue(FleetArrivals.Installed);
                json.WritePropertyName("arrivalOwners");
                json.WriteStartArray();
                foreach (string owner in FleetArrivals.Owners())
                {
                    json.WriteValue(owner);
                }

                json.WriteEndArray();

                json.WritePropertyName("visibilityPatch");
                json.WriteValue(ForeignFleetWatch.Installed);
                json.WritePropertyName("visibilityOwners");
                json.WriteStartArray();
                foreach (string owner in ForeignFleetWatch.Owners())
                {
                    json.WriteValue(owner);
                }

                json.WriteEndArray();
                json.WritePropertyName("turnSubscribed");
                json.WriteValue(ForeignFleetWatch.Subscribed);
                json.WritePropertyName("foreignFleetsWatched");
                json.WriteValue(ForeignFleetWatch.Watching);

                // The third detection point: the turn-end influence sweep. Its subscription and its
                // table are what a teardown has to have let go, and its last pass's cost is the one
                // figure no transcript shows.
                json.WritePropertyName("groundSubscribed");
                json.WriteValue(InfluenceGroundWatch.Subscribed);
                json.WritePropertyName("groundWatched");
                json.WriteValue(InfluenceGroundWatch.Watching);
                json.WritePropertyName("groundTiles");
                json.WriteValue(InfluenceGroundWatch.Tiles);
                json.WritePropertyName("groundQueries");
                json.WriteValue(InfluenceGroundWatch.Queries);
                json.WritePropertyName("groundMilliseconds");
                json.WriteValue(InfluenceGroundWatch.Milliseconds);

                json.WritePropertyName("mapped");
                json.WriteStartArray();
                foreach (KeyValuePair<string, string> entry in ModNotifications.Mapped())
                {
                    json.WriteStartObject();
                    json.WritePropertyName(entry.Key);
                    json.WriteValue(entry.Value);
                    json.WriteEndObject();
                }

                json.WriteEndArray();

                json.WritePropertyName("pending");
                json.WriteStartArray();
                IGuiNotificationService service = Gui.GuiNotificationService;
                List<GuiNotification> standing =
                    service == null ? null : service.GetPlayerEmpireGuiNotifications();
                for (int i = 0; standing != null && i < standing.Count; i++)
                {
                    ModNotification mine = standing[i] as ModNotification;
                    if (mine == null)
                    {
                        continue;
                    }

                    json.WriteStartObject();
                    json.WritePropertyName("title");
                    json.WriteValue(mine.GetTitle());
                    json.WritePropertyName("turnsLeft");
                    json.WriteValue(mine.TurnsBeforeAutoDismiss);
                    json.WriteEndObject();
                }

                json.WriteEndArray();
            });
        }

        private static void WriteVector(JsonTextWriter json, string name, Vector3 value)
        {
            json.WritePropertyName(name);
            json.WriteStartArray();
            json.WriteValue(Round(value.x));
            json.WriteValue(Round(value.y));
            json.WriteValue(Round(value.z));
            json.WriteEndArray();
        }

        private static double Round(float value)
        {
            return Math.Round(value, 3);
        }

        /// <summary>The windows the game says are up, with the "shown, animated in, interactive" flag
        /// every screen's IsActive turns on - the fastest way to see why a screen is not activating.
        /// </summary>
        public static string Windows()
        {
            return Guarded(json =>
            {
                json.WritePropertyName("windows");
                json.WriteStartArray();
                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                if (gui != null && gui.ShownGuiPanels != null)
                {
                    int listed = 0;
                    foreach (Amplitude.Unity.Gui.GuiPanel panel in gui.ShownGuiPanels)
                    {
                        // The game's own GuiWindow, not the engine's: IsReady - "shown, animated in,
                        // interactive", the gate every screen's IsActive turns on - is the subclass's.
                        global::GuiWindow window = panel as global::GuiWindow;
                        if (window == null)
                        {
                            continue;
                        }

                        if (listed++ >= MaxWindowsListed)
                        {
                            break;
                        }

                        json.WriteStartObject();
                        json.WritePropertyName("name");
                        json.WriteValue(window.Name ?? window.gameObject.name);
                        json.WritePropertyName("type");
                        json.WriteValue(window.GetType().Name);
                        json.WritePropertyName("isReady");
                        json.WriteValue(window.IsReady);
                        json.WriteEndObject();
                    }
                }

                json.WriteEndArray();
            });
        }

        /// <summary>
        /// Whether the game's key scans are still standing down for the mod's keys. A stripped patch
        /// costs the mod nothing visible - navigation keeps working - while every key it acts on ALSO
        /// fires the game's binding on it, so Tab opens the chat box and Enter ends the turn. That is a
        /// wrong-looking test result with no error anywhere, which is why the count is worth watching
        /// on every /status.
        ///
        /// The owner id is per load (see <see cref="GameKeyStandDown"/>), so a changed id after a
        /// reload is proof the new load's patches are the ones installed.
        /// </summary>
        public static string Patches()
        {
            return Guarded(json =>
            {
                json.WritePropertyName("patches");
                WritePatches(json);
            });
        }

        // Shared with /status, which must stay cheap: the target lookup is reflection over the six
        // signatures - three key scans, AgeControlTextField.KeyDown, InGameChatPanel.HandleInput and
        // AgeManager.set_FocusedControl - and is done once.
        internal static void WritePatches(JsonTextWriter json)
        {
            json.WriteStartArray();
            MethodInfo[] targets;
            string failure = Targets(out targets);
            if (failure != null)
            {
                json.WriteStartObject();
                json.WritePropertyName("error");
                json.WriteValue(failure);
                json.WriteEndObject();
                json.WriteEndArray();
                return;
            }

            foreach (MethodInfo target in targets)
            {
                json.WriteStartObject();
                json.WritePropertyName("method");
                json.WriteValue(target.DeclaringType.Name + "." + target.Name);
                int count = 0;
                List<string> owners = new List<string>();
                try
                {
                    HarmonyLib.Patches info = Harmony.GetPatchInfo(target);
                    if (info != null && info.Prefixes != null)
                    {
                        foreach (Patch prefix in info.Prefixes)
                        {
                            count++;
                            owners.Add(prefix.owner);
                        }
                    }
                }
                catch (Exception e)
                {
                    owners.Add("<err: " + e.Message + ">");
                }

                json.WritePropertyName("prefixCount");
                json.WriteValue(count);
                json.WritePropertyName("owners");
                json.WriteStartArray();
                foreach (string owner in owners)
                {
                    json.WriteValue(owner);
                }

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        private static MethodInfo[] _targets;
        private static string _targetFailure;

        private static string Targets(out MethodInfo[] targets)
        {
            if (_targets == null && _targetFailure == null)
            {
                try
                {
                    MethodInfo[] scans = GameKeyStandDown.KeyScans();
                    MethodInfo[] dispatches = GameKeyboardHandover.KeyDispatches();
                    MethodInfo[] chat = ChatEscape.Handlers();
                    MethodInfo[] focus = GameTextFocus.FocusSetters();
                    MethodInfo[] all = new MethodInfo[
                        scans.Length + dispatches.Length + chat.Length + focus.Length
                    ];
                    scans.CopyTo(all, 0);
                    dispatches.CopyTo(all, scans.Length);
                    chat.CopyTo(all, scans.Length + dispatches.Length);
                    focus.CopyTo(all, scans.Length + dispatches.Length + chat.Length);
                    _targets = all;
                }
                catch (Exception e)
                {
                    _targetFailure = "the game's key scans could not be found: " + e.Message;
                }
            }

            targets = _targets;
            return _targetFailure;
        }

        /// <summary>
        /// What the input layer is claiming from the game right now - the other end of the tripwire
        /// <see cref="Patches"/> watches. The patches say the game is ASKING; this says what it is
        /// being told, which is the only way to see a key leaking through a screen handover.
        ///
        /// <c>latched</c> is the consumed-key latch: a key the mod acted on stays the mod's until the
        /// player lets go of it, because the game's scan runs after our frame and the screen that
        /// consumed the key may be gone by then (see <see cref="ModInput.ClaimsKey"/>). An entry with
        /// <c>held: false</c> is one the next <c>Tick</c> will drop - normal for an injected action,
        /// which pressed no key; an entry that stays with <c>held: false</c> across calls is a stuck
        /// claim, and the game is deaf to that key until it clears.
        ///
        /// <c>layerLive</c> is <see cref="ModInput.LayerIsLive"/>'s verdict, split into the two halves
        /// that can answer no - no screen of ours (<c>screen: null</c>) or the game holding the
        /// keyboard for a text field. A key that reads claimed while <c>layerLive</c> is false is
        /// claimed by the latch alone, which is exactly the handover window the bug class lives in.
        /// </summary>
        public static string Claims()
        {
            return Claims(null);
        }

        /// <summary>
        /// <see cref="Claims()"/> plus <see cref="ModInput.ClaimsKey"/>'s answer - side-effect-free, the
        /// same call the game's key scans make - for each comma-separated <c>KeyCode</c> name in
        /// <paramref name="keys"/> (<c>"Escape,Return,Tab"</c>).
        /// </summary>
        public static string Claims(string keys)
        {
            return Guarded(json =>
            {
                ModInput input = ModEntry.Input;
                if (input == null)
                {
                    throw new InvalidOperationException("the input layer is not up");
                }

                GraphNavigator navigator = ModEntry.Navigator;
                Screens.Screen screen = navigator == null ? null : navigator.Screen;
                json.WritePropertyName("screen");
                json.WriteValue(screen == null ? null : screen.Key);
                json.WritePropertyName("screenFocused");
                json.WriteValue(input.ScreenIsFocused());
                json.WritePropertyName("keyboardElsewhere");
                json.WriteValue(input.KeyboardIsElsewhere());
                json.WritePropertyName("layerLive");
                json.WriteValue(input.LayerIsLive());
                json.WritePropertyName("backClaimed");
                json.WriteValue(input.BackClaimed);
                json.WritePropertyName("claimsBack");
                json.WriteValue(input.ClaimsBack());

                json.WritePropertyName("latched");
                json.WriteStartArray();
                IList<KeyCode> latched = input.ConsumedKeys;
                for (int i = 0; i < latched.Count; i++)
                {
                    json.WriteStartObject();
                    json.WritePropertyName("key");
                    json.WriteValue(latched[i].ToString());
                    json.WritePropertyName("held");
                    json.WriteValue(UnityEngine.Input.GetKey(latched[i]));
                    json.WriteEndObject();
                }

                json.WriteEndArray();

                // The chords that stay the game's whatever the key set says - ask about one with
                // Chord(), which is the only probe that can tell them apart.
                json.WritePropertyName("leftToGame");
                json.WriteStartArray();
                IList<ES2Access.UI.Input.KeyboardBinding> chords = input.ChordsLeftToGame;
                for (int i = 0; i < chords.Count; i++)
                {
                    json.WriteValue(chords[i].DisplayName);
                }

                json.WriteEndArray();

                if (string.IsNullOrEmpty(keys))
                {
                    return;
                }

                json.WritePropertyName("asked");
                json.WriteStartArray();
                foreach (string name in keys.Split(','))
                {
                    string wanted = name.Trim();
                    if (wanted.Length == 0)
                    {
                        continue;
                    }

                    json.WriteStartObject();
                    json.WritePropertyName("key");
                    json.WriteValue(wanted);
                    try
                    {
                        KeyCode key = (KeyCode)Enum.Parse(typeof(KeyCode), wanted, true);
                        json.WritePropertyName("claims");
                        json.WriteValue(input.ClaimsKey(key));
                        json.WritePropertyName("held");
                        json.WriteValue(UnityEngine.Input.GetKey(key));
                    }
                    catch (Exception)
                    {
                        json.WritePropertyName("error");
                        json.WriteValue("no KeyCode is named '" + wanted + "'");
                    }

                    json.WriteEndObject();
                }

                json.WriteEndArray();
            });
        }

        /// <summary>
        /// What the game's own key scans are told about one CHORD - <c>"Ctrl+Tab"</c>, <c>"Tab"</c>,
        /// <c>"Shift+Tab"</c>, parsed by the game's own <c>KeyCombination.FromString</c>.
        ///
        /// <see cref="Claims(string)"/> cannot answer this: it asks per <c>KeyCode</c>, exactly as
        /// <see cref="ModInput.ClaimsKey"/> is asked, so it says "claimed" for Tab and has no way to
        /// speak about Ctrl+Tab. This calls the prefix's own decision
        /// (<see cref="GameKeyStandDown.Claimed"/>) on a combination built here, which is what proves a
        /// game binding is reachable without holding three keys down while an HTTP request arrives.
        ///
        /// <c>suppressed: true</c> means the game is told the chord is not pressed, so whatever it has
        /// bound to it never runs. A chord handed back (<see cref="ModInput.LeaveToGame"/>) reads false
        /// while its bare key reads true.
        /// </summary>
        public static string Chord(string chord)
        {
            return Guarded(json =>
            {
                if (string.IsNullOrEmpty(chord))
                {
                    throw new InvalidOperationException("no chord was asked about");
                }

                Amplitude.Unity.Input.KeyCombination combination =
                    Amplitude.Unity.Input.KeyCombination.FromString(chord, "+");
                json.WritePropertyName("chord");
                json.WriteValue(chord);
                json.WritePropertyName("parsed");
                json.WriteValue(combination.ToString("+"));
                json.WritePropertyName("keys");
                json.WriteStartArray();
                for (int i = 0; i < combination.KeyCodes.Count; i++)
                {
                    json.WriteStartObject();
                    json.WritePropertyName("key");
                    json.WriteValue(combination.KeyCodes[i].ToString());
                    json.WritePropertyName("held");
                    json.WriteValue(UnityEngine.Input.GetKey(combination.KeyCodes[i]));
                    json.WriteEndObject();
                }

                json.WriteEndArray();
                json.WritePropertyName("suppressed");
                json.WriteValue(ES2Access.UI.Input.GameKeyStandDown.Claimed(combination));
            });
        }

        /// <summary>
        /// One log line per frame naming the whole of the engine's hover-to-tooltip pipeline: what the
        /// mod is pointing the engine at, what the tooltip controller remembers pointing at, its
        /// countdown to showing, the tooltip's own content/class/target, and whether the tooltip window
        /// is up. Always false, so <c>POST /wait</c> on it records the whole passage.
        ///
        /// The countdown is the interesting number. <c>GuiTooltipController.Update</c> parks it at
        /// <c>999</c> when the delay elapses over a tooltip with neither content nor target, and only a
        /// CHANGE of hovered transform - or the tooltip's target being written again - ever resets it.
        /// </summary>
        public static bool TooltipTrace(string tag)
        {
            try
            {
                Core.Util.Log.Info("ttrace " + tag + " f=" + Time.frameCount + " " + TooltipPipeline());
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("ttrace threw: " + e.Message);
            }

            return false;
        }

        /// <summary>The same reading as <see cref="TooltipTrace"/>, answered once to a poll.</summary>
        public static string TooltipPipe()
        {
            try
            {
                return TooltipPipeline();
            }
            catch (Exception e)
            {
                return Err(e.Message);
            }
        }

        private static string TooltipPipeline()
        {
            System.Text.StringBuilder line = new System.Text.StringBuilder();
            AgeManager age = AgeManager.Instance;
            AgeTransform over = age == null ? null : age.OverrolledTransform;
            line.Append("over=").Append(Named(over));

            Amplitude.Unity.Gui.GuiTooltipController controller = TooltipController();
            if (controller == null)
            {
                line.Append(" controller=none");
            }
            else
            {
                AgeTransform remembered = controller.OverrolledAgeTransform;
                line.Append(" ctrl=").Append(Named(remembered));
                FieldInfo timer = AccessTools.Field(
                    typeof(Amplitude.Unity.Gui.GuiTooltipController),
                    "timeBeforeShowingTooltip"
                );
                line.Append(" timer=")
                    .Append(
                        timer == null
                            ? "?"
                            : Round((float)timer.GetValue(controller)).ToString(
                                CultureInfo.InvariantCulture
                            )
                    );
                line.Append(" cur=")
                    .Append(
                        controller.CurrentTooltipWindow == null
                            ? "-"
                            : controller.CurrentTooltipWindow.name
                    );
                AgeTooltip tip = controller.OverrolledAgeTooltip;
                line.Append(" tip=").Append(Tipped(tip));
            }

            AgeTooltip aimed = over == null ? null : over.AgeTooltip;
            line.Append(" aimed=").Append(Tipped(aimed));
            line.Append(" want=").Append(Tipped(ES2Access.UI.PointerFocus.Wanted));

            GuiTooltipWindow window = Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<GuiTooltipWindow>(false)
                : null;
            line.Append(" win=")
                .Append(window == null ? "none" : (window.Shown ? "shown" : "hidden"))
                .Append(
                    window == null || window.AgeTooltip == null
                        ? ""
                        : "/" + window.AgeTooltip.Class
                );

            GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            line.Append(" delay=")
                .Append(
                    gui == null
                        ? "?"
                        : Round(gui.TooltipDisplayDelay).ToString(CultureInfo.InvariantCulture)
                );
            line.Append(" notif=")
                .Append(
                    Gui.GuiNotificationService == null
                        ? "?"
                        : (Gui.GuiNotificationService.CanShowNotifications ? "can" : "no")
                );

            GraphNavigator navigator = ModEntry.Navigator;
            ControlId focused = navigator == null ? null : navigator.FocusedKey;
            line.Append(" node=")
                .Append(focused == null ? "-" : Convert.ToString(focused.StructuralKey));
            return line.ToString();
        }

        private static string Named(AgeTransform widget)
        {
            if (ReferenceEquals(widget, null))
            {
                return "null";
            }

            if (widget == null)
            {
                return "destroyed";
            }

            return widget.name;
        }

        private static string Tipped(AgeTooltip tooltip)
        {
            if (ReferenceEquals(tooltip, null))
            {
                return "-";
            }

            if (tooltip == null)
            {
                return "destroyed";
            }

            return "["
                + (string.IsNullOrEmpty(tooltip.Class) ? "noclass" : tooltip.Class)
                + " content="
                + (string.IsNullOrEmpty(tooltip.Content) ? "0" : tooltip.Content.Length.ToString())
                + (tooltip.Target == null ? " notarget" : " target")
                + (tooltip.DirtyTarget ? " dirty" : "")
                + "]";
        }

        private static Amplitude.Unity.Gui.GuiTooltipController TooltipController()
        {
            GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            if (gui == null)
            {
                return null;
            }

            PropertyInfo property = AccessTools.Property(
                typeof(Amplitude.Unity.Gui.GuiManager),
                "GuiTooltipController"
            );
            return property == null
                ? null
                : property.GetValue(gui, null) as Amplitude.Unity.Gui.GuiTooltipController;
        }

        /// <summary>
        /// Set the game's tooltip hover delay - normally 0.3 s - and return what it was.
        /// <see cref="RestoreTooltipDelay"/> (-1) puts the game's own value back.
        ///
        /// It is here because that third of a second is the difference between a tooltip test that
        /// works and one that needs a settle window nobody can size reliably: focus a control, ask for
        /// its tooltip, and the words only exist once the game has drawn them (see
        /// <see cref="DrawnTooltip"/>). At zero the next frame has them.
        ///
        /// Where it lives: the game's <c>GuiManager</c> (the Assembly-CSharp subclass) holds it in
        /// <c>tooltipDisplayDelay</c>, exposed as <c>TooltipDisplayDelay</c> and read fresh by
        /// <c>GuiTooltipController.Update</c> on every new hover - so a write takes effect on the next
        /// hover with no reinitialization. The PUBLIC SETTER IS NOT USED: it writes the player's
        /// Registry.xml, and a dev tool must not edit the player's settings. The private field is
        /// written directly instead.
        ///
        /// Which also gives the restore path for free, and one that survives a hot reload dropping
        /// this class's statics: the registry still holds the real value precisely because nothing here
        /// ever writes it. The first change caches what it displaced; a later load with no cache falls
        /// back to the registry.
        /// </summary>
        /// <summary>
        /// End the text edit that is running right now, as either of its two endings, and report what
        /// the box held on the way out.
        ///
        /// A COMMIT is a physical Return and a cancel is a physical Escape, and neither can be
        /// injected - the mod's own <c>/input</c> queue carries actions, not keystrokes, and the
        /// engine reads both of these keys straight off Unity. So the only way to drive the two
        /// endings from a test is to answer the question the focus setter asks
        /// (<c>TextFieldEditor.CommitTheNextRelease</c>) and then let go of the keyboard for real:
        /// everything downstream - the restore, the words, the refusal path - runs exactly as it does
        /// for the player.
        /// </summary>
        public static string EndEdit(bool commit)
        {
            return Guarded(json =>
            {
                json.WritePropertyName("wasEditing");
                json.WriteValue(ES2Access.Screens.TextFieldEditor.Editing);
                ES2Access.Screens.TextFieldEditor.CommitTheNextRelease = commit;
                AgeManager age = AgeManager.Instance;
                if (age != null)
                {
                    age.FocusedControl = null;
                }

                ES2Access.Screens.TextFieldEditor.CommitTheNextRelease = false;
                json.WritePropertyName("commit");
                json.WriteValue(commit);
            });
        }

        /// <summary>The same lever, ARMED and left armed, for the endings the mod does not cause: the
        /// game's own validate callback releases the keyboard itself (and hides the surface around it),
        /// so a successful commit is driven by arming this and then invoking that callback - which is
        /// the real sequence, with only the physical Return replaced.</summary>
        public static string ArmCommit()
        {
            return Guarded(json =>
            {
                ES2Access.Screens.TextFieldEditor.CommitTheNextRelease = true;
                json.WritePropertyName("armed");
                json.WriteValue(true);
            });
        }

        public static string TooltipDelay(double seconds)
        {
            return Guarded(json =>
            {
                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                if (gui == null)
                {
                    throw new InvalidOperationException("the gui service is not up yet");
                }

                FieldInfo field = TooltipDelayField();
                float was = (float)field.GetValue(gui);
                float wanted;
                if (seconds == RestoreTooltipDelay)
                {
                    wanted = _tooltipDelayWas ?? RegisteredTooltipDelay();
                    _tooltipDelayWas = null;
                }
                else
                {
                    wanted = (float)seconds;
                    if (_tooltipDelayWas == null)
                    {
                        _tooltipDelayWas = was;
                    }
                }

                field.SetValue(gui, wanted);
                json.WritePropertyName("was");
                json.WriteValue(Round(was));
                json.WritePropertyName("now");
                json.WriteValue(Round(wanted));
                json.WritePropertyName("registry");
                json.WriteValue(Round(RegisteredTooltipDelay()));
            });
        }

        private static float? _tooltipDelayWas;

        private static FieldInfo TooltipDelayField()
        {
            FieldInfo field = AccessTools.Field(typeof(GuiManager), "tooltipDisplayDelay");
            if (field == null)
            {
                throw new MissingFieldException("GuiManager", "tooltipDisplayDelay");
            }

            return field;
        }

        // The player's own setting, untouched: it is only ever read here.
        private static float RegisteredTooltipDelay()
        {
            return Amplitude.Unity.Framework.Application.Registry.GetValue(
                GuiManager.Registers.TooltipDisplayDelay,
                0.3f
            );
        }

        /// <summary>
        /// The icons this load met that <see cref="IconNames"/> could not name.
        ///
        /// <c>tokens</c> is the tripwire and must stay EMPTY: the engine's registered token set is
        /// closed and the icon table covers all of it, so an entry here is a patch, a DLC or a mod
        /// that added one - and a word a player heard nothing for. <c>pictures</c> is an audit
        /// sample rather than a defect list: every bitmap in the game can be drawn into a panel and
        /// most of them are decoration, so this is where to look when a tooltip is missing a word
        /// and the picture that carried it needs identifying.
        /// </summary>
        public static string UnknownIcons()
        {
            return Guarded(json =>
            {
                WriteStrings(json, "tokens", IconNames.UnknownTokens);
                WriteStrings(json, "pictures", IconNames.UnknownPictures);
            });
        }

        private static void WriteStrings(JsonTextWriter json, string name, IList<string> values)
        {
            json.WritePropertyName(name);
            json.WriteStartArray();
            for (int i = 0; i < values.Count; i++)
            {
                json.WriteValue(values[i]);
            }

            json.WriteEndArray();
        }

        /// <summary>How deep under the tooltip's panel <see cref="Tooltip"/> looks.</summary>
        private const int MaxTooltipDepth = 8;

        /// <summary>
        /// The tooltip the game is drawing right now, as MEASURED - every label and every icon under
        /// its panel, each with the rectangle it occupies, in the order the panel laid them out, plus
        /// the panel FEATURES those widgets belong to and what each one was read as.
        ///
        /// It is the paired proof a tooltip claim needs. <c>/gui/graph</c> says what the mod would
        /// SPEAK for a control; a screenshot says what the player SEES; neither says why the two
        /// differ. This does: two labels the mod read in the wrong order differ here by a rectangle,
        /// and an icon the mod turned into a bare number is an entry here with an asset name and no
        /// text beside it. Both questions came up on the same tooltip and neither is answerable from
        /// the spoken form alone.
        ///
        /// The <c>features</c> array is the coverage answer. A tooltip is assembled from an ordered
        /// list of feature prefabs and the mod reads each one on its own; a feature nobody has
        /// written a reader for still reads, through the fallback, so the only place a gap can show
        /// is here - the feature's class name beside <c>"default"</c>, with the lines it produced to
        /// judge them by. Coverage belongs in a probe, never in the player's ears.
        ///
        /// An icon reports the name of the TEXTURE it is drawing, which is what the artist called the
        /// picture ("Food", "FIDS_Industry") and therefore what the column means - the only evidence
        /// available for a value the game draws with no word next to it anywhere.
        ///
        /// Requires the tooltip to be up: focus the control first, and
        /// <see cref="TooltipDelay"/><c>(0)</c> so it is up on the next frame.
        /// </summary>
        public static string Tooltip()
        {
            return Guarded(json =>
            {
                GuiTooltipWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GuiTooltipWindow>(false)
                    : null;
                json.WritePropertyName("shown");
                json.WriteValue(window != null && window.Shown);
                if (window == null || !window.Shown || window.PanelFeaturesTable == null)
                {
                    return;
                }

                json.WritePropertyName("class");
                json.WriteValue(
                    window.AgeTooltip == null ? null : window.AgeTooltip.Class
                );

                json.WritePropertyName("features");
                json.WriteStartArray();
                IList<TooltipFeatures.Reading> features = DrawnTooltip.Features(window.AgeTooltip);
                for (int i = 0; i < features.Count; i++)
                {
                    json.WriteStartObject();
                    json.WritePropertyName("feature");
                    json.WriteValue(features[i].Feature);
                    json.WritePropertyName("reader");
                    json.WriteValue(features[i].Reader);
                    json.WritePropertyName("lines");
                    json.WriteStartArray();
                    for (int line = 0; line < features[i].Lines.Count; line++)
                    {
                        json.WriteValue(features[i].Lines[line]);
                    }

                    json.WriteEndArray();
                    json.WriteEndObject();
                }

                json.WriteEndArray();

                // Every feature class the fallback reader has answered for since this assembly loaded,
                // so a feature nobody has judged surfaces in tooling rather than in a player's ears
                // (<see cref="TooltipFeatures.DefaultRead"/>). Cumulative, not this tooltip's.
                json.WritePropertyName("defaultRead");
                json.WriteStartArray();
                List<string> defaulted = new List<string>(TooltipFeatures.DefaultRead);
                defaulted.Sort(StringComparer.Ordinal);
                for (int i = 0; i < defaulted.Count; i++)
                {
                    json.WriteValue(defaulted[i]);
                }

                json.WriteEndArray();

                List<AgeTransform> found = new List<AgeTransform>();
                Gather(window.PanelFeaturesTable, found, 0);

                // Banded across the WHOLE window, which the reader deliberately no longer does: the
                // rows here are the measurement, and a feature whose reading disagrees with them is
                // the interesting case rather than a contradiction.
                json.WritePropertyName("rows");
                json.WriteStartArray();
                foreach (List<AgeTransform> row in AgeLayout.Rows(found, Itself))
                {
                    json.WriteStartArray();
                    foreach (AgeTransform widget in row)
                    {
                        WritePart(json, widget);
                    }

                    json.WriteEndArray();
                }

                json.WriteEndArray();
            });
        }

        /// <summary>
        /// Whether the popup on screen READS the way it DRAWS - the notification family's own
        /// self-check, run against whichever one is up (see <see cref="NotificationAudit"/>).
        ///
        /// Sixty-nine window types share one reader and no fixture reaches most of them, so the
        /// question "does this prefab break a premise of the shared reading" has to be answerable
        /// from the popup itself. Four arrays, each empty when the popup is clean: text that is
        /// painted and unspoken, words spoken that nothing draws, nodes filed in the wrong band or
        /// walked out of the drawn order, and tooltips promised or lost. The same check runs by
        /// itself on every popup the player is shown while the dev server is up, and complains to
        /// the log.
        /// </summary>
        public static string NotificationParity()
        {
            return NotificationAudit.Json();
        }

        /// <summary>
        /// Whether the FOCUSED screen's tooltips read the way the game draws them - the same
        /// comparison, asked of any screen rather than only of a popup (see
        /// <see cref="TooltipAudit"/>).
        ///
        /// Seven arrays, four of them findings: a node promising a dossier with nothing that draws
        /// (<c>promised</c>), a node pointing at a tooltip that draws nothing (<c>misaimed</c>), a
        /// tooltip the game would draw on a control that no node covers (<c>uncovered</c>) and one
        /// whose words are in nothing the covering node carries (<c>unread</c>). The other three are
        /// weaker claims kept apart on purpose: <c>decoration</c> is the same coverage question on a
        /// widget the player cannot work, <c>hidden</c> is what only the pass with the transparency
        /// gate off can see, and <c>undescribed</c> is a defect in the GAME's own data.
        ///
        /// The painted half needs the screen to say where it is drawn
        /// (<c>Screen.RootTransform</c>); a screen that does not answers the declaration-side
        /// questions only, and says so with a null <c>root</c>.
        /// </summary>
        public static string TooltipParity()
        {
            return TooltipAudit.Json();
        }

        /// <summary>
        /// What the FOCUSED screen has never declared - hover words AND actions - against everything
        /// the engine is drawing (see <see cref="CoverageAudit"/>).
        ///
        /// The widening over <see cref="TooltipParity"/> that makes it worth a second call: the
        /// painted side comes from the ENGINE's own list of drawn windows and panels whenever the
        /// screen names no <c>Screen.RootTransform</c>, so the coverage question is finally asked of
        /// the galaxy map - where a hand audit found six undeclared dossiers and four undeclared
        /// buttons on one card while the parity check said <c>clean</c>. And a second half nothing
        /// else asks: every PAINTED control the player could work that no node stands on
        /// (<c>actionsUncovered</c>, "no node stands here") or that a node stands on and cannot press
        /// ("the node here declares no action").
        ///
        /// <paramref name="wholeTree"/> forces the live-tree walk even on a screen that names its own
        /// window - what a modal drawn over a live page needs. Counts first, then the lists, each
        /// capped with a <c>more</c> entry. It walks the whole GUI: run it on demand, never in a loop.
        /// </summary>
        public static string Coverage(bool wholeTree = false)
        {
            return CoverageAudit.Json(wholeTree);
        }

        private static readonly Func<AgeTransform, AgeTransform> Itself = widget => widget;

        /// <summary>Every widget under the panel that draws something a reader would have to account
        /// for - words or a picture - skipping the branches the window is not showing, which it keeps
        /// around holding the last tooltip's text (see <see cref="DrawnTooltip"/>).</summary>
        private static void Gather(AgeTransform widget, List<AgeTransform> found, int depth)
        {
            if (depth > MaxTooltipDepth)
            {
                return;
            }

            if (
                widget.GetComponent<AgePrimitiveLabel>() != null
                || widget.GetComponent<AgePrimitiveImage>() != null
            )
            {
                found.Add(widget);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && child.Visible && (widget.StrictVisibility || child.Alpha > 0f))
                {
                    Gather(child, found, depth + 1);
                }
            }
        }

        private static void WritePart(JsonTextWriter json, AgeTransform widget)
        {
            json.WriteStartObject();
            try
            {
                Rect rect = widget.GetGlobalPosition();
                json.WritePropertyName("rect");
                json.WriteStartArray();
                json.WriteValue(Round(rect.xMin));
                json.WriteValue(Round(rect.yMin));
                json.WriteValue(Round(rect.width));
                json.WriteValue(Round(rect.height));
                json.WriteEndArray();

                AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
                if (label != null)
                {
                    json.WritePropertyName("raw");
                    json.WriteValue(label.Text);
                    json.WritePropertyName("spoken");
                    json.WriteValue(AgeText.Label(label));
                }

                AgePrimitiveImage image = widget.GetComponent<AgePrimitiveImage>();
                if (image != null)
                {
                    json.WritePropertyName("asset");
                    json.WriteValue(image.Texture == null ? null : image.Texture.name);
                }

                json.WritePropertyName("name");
                json.WriteValue(widget.name);
            }
            catch (Exception e)
            {
                json.WritePropertyName("error");
                json.WriteValue(e.Message);
            }

            json.WriteEndObject();
        }

        private static string Guarded(Action<JsonTextWriter> body)
        {
            try
            {
                return DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    body(json);
                    json.WriteEndObject();
                });
            }
            catch (Exception e)
            {
                return Err(e.Message);
            }
        }

        private static string Err(string message)
        {
            return DevJson.Error(message);
        }
    }
}

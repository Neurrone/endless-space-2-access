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
    /// <summary>The probes that report what the game and the mod are: the screen, the stack, the
    /// session, the saves, the camera, the notifications and the patches.</summary>
    public static partial class DevProbe
    {
        private const int MaxSavesListed = 60;

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

                // And the replay stream's own, which is the one a battle stage checks: two owners on
                // it after a reload is a narration saying every shot twice.
                json.WritePropertyName("battleStreamPatch");
                json.WriteValue(BattleStream.Installed);
                json.WritePropertyName("battleStreamOwners");
                json.WriteStartArray();
                foreach (string owner in BattleStream.Owners())
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
                // The settle window's two figures: how many foreign fleets the player has been TOLD
                // are in sight, and how many crossings are still waiting to see whether they held.
                json.WritePropertyName("foreignFleetsInSight");
                json.WriteValue(ForeignFleetWatch.InSight);
                json.WritePropertyName("foreignFleetsSettling");
                json.WriteValue(ForeignFleetWatch.Pending);
                json.WritePropertyName("foreignFleetSweepPending");
                json.WriteValue(ForeignFleetWatch.SweepPending);

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

    }
}

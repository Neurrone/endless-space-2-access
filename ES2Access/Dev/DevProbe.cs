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

        // Shared with /status, which must stay cheap: the target lookup is reflection over the four
        // signatures - three key scans and the focused-control dispatch - and is done once.
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
                    MethodInfo[] all = new MethodInfo[scans.Length + dispatches.Length];
                    scans.CopyTo(all, 0);
                    dispatches.CopyTo(all, scans.Length);
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

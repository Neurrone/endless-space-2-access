using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Amplitude.Unity.Session;
using ES2Access.Core.Speech;
using ES2Access.Loader;
using ES2Access.Loader.Dev;
using ES2Access.UI.Input;
using UnityEngine;

namespace ES2Access.Dev
{
    /// <summary>
    /// The mod's half of the dev server: what it said, and what state it is in. Registered with
    /// the loader on Start and taken back down on Stop, so an unloaded or broken mod answers 404
    /// here instead of reporting state that no longer exists.
    ///
    ///   GET  /status            mod version, speech backend, last spoken line, scene size, and
    ///                           whether the game's key scans are still standing down (see DevProbe)
    ///   GET  /speech?since=N&amp;wait=MS
    ///                           lines spoken after sequence N, plus the next cursor; with wait, hold
    ///                           the connection open until there is one
    ///   GET  /gui/age?window=&amp;depth=&amp;visibleOnly=&amp;fields=
    ///                           the live AGE hierarchy read as accessible meaning (see AgeDump);
    ///                           fields= answers flat text with only those fields per widget
    ///   GET  /gui/graph?edges=1&amp;buffers=1&amp;screen=KEY
    ///                           the focused screen's whole accessible tree, or with screen=, what
    ///                           another registered screen would offer (see GraphDump)
    ///   POST /input             body = an action key; run it as a keypress would (see ModInput)
    ///   POST /type              body = characters; type them at the focused screen (the type-ahead
    ///                           search), and report what it made of them
    ///   POST /loadsave          body = a save title, or empty for the most recent save
    ///
    /// /speech reads the thread-safe buffer straight from the HTTP thread; /status, /gui/age,
    /// /gui/graph and /loadsave touch the scene, so they go through the main-thread queue and answer
    /// 503 if the game is wedged. /input and /speech?wait block the HTTP thread and never the main
    /// one: the game has to keep running frames for either of them to be answered at all.
    /// </summary>
    internal sealed class ModRoutes
    {
        // Long enough for a frame that is doing real work (a screen rebuild, a window animating in),
        // short enough that a wedged game answers rather than hanging the caller.
        private const int InjectionTimeoutMilliseconds = 5000;

        private const int SettleMilliseconds = 400;
        private const int MaxSpeechWaitMilliseconds = 3000;
        private const int SpeechPollMilliseconds = 25;

        /// <summary>The ceiling on /speech?wait, so a caller cannot pin an HTTP thread indefinitely.
        /// </summary>
        private const int MaxWaitMilliseconds = 30000;

        private readonly ModHost _host;
        private readonly SpeechLog _speech = new SpeechLog();

        public ModRoutes(ModHost host)
        {
            _host = host;
        }

        public void Register()
        {
            PrismSpeech.Observer = Spoken;
            // Each route names the query parameters it understands; the loader answers 400 for
            // anything else before the handler runs, so a mistyped parameter is never ignored.
            _host.RegisterRoute("GET", "/status", Status);
            _host.RegisterRoute("GET", "/speech", Speech, "since", "wait");
            _host.RegisterRoute("GET", "/gui/age", Age, "window", "depth", "visibleOnly", "fields");
            _host.RegisterRoute("GET", "/gui/graph", Graph, "edges", "buffers", "screen");
            _host.RegisterRoute("POST", "/input", Input);
            _host.RegisterRoute("POST", "/type", Type);
            _host.RegisterRoute("POST", "/loadsave", LoadSave);
        }

        // The mod's own buffer is what /speech serves; the loader's outlives a hot reload, which
        // is what lets POST /eval report the speech an evaluated call provoked.
        private void Spoken(string text)
        {
            _speech.Add(text);
            _host.NotifySpoken(text);
        }

        /// <summary>The routes themselves are dropped by the loader; this releases the tap on the
        /// speech chokepoint, which is the mod's own static state, and lets go of anyone waiting on
        /// this buffer for a line that is never coming now.</summary>
        public void Unregister()
        {
            PrismSpeech.Observer = null;
            _speech.Close();
        }

        private DevResponse Status(DevRequest request)
        {
            return DevResponse.Json(
                (string)
                    _host.MainThread.Run(() =>
                    {
                        PrismSpeech speech = ModEntry.Speech;
                        int gameObjectCount = UnityEngine
                            .Object.FindObjectsOfType<GameObject>()
                            .Length;
                        return DevJson.Write(json =>
                        {
                            json.WriteStartObject();
                            json.WritePropertyName("version");
                            json.WriteValue(ModEntry.ModVersion);
                            // Which LOAD is answering. /loader/status reports the same name from the
                            // outside; read here it is the mod's own assembly saying so, which is what
                            // catches a reload that half-happened.
                            json.WritePropertyName("modAssemblyName");
                            json.WriteValue(typeof(ModEntry).Assembly.GetName().Name);
                            json.WritePropertyName("speechAvailable");
                            json.WriteValue(speech.Available);
                            json.WritePropertyName("backendName");
                            json.WriteValue(speech.BackendName);
                            json.WritePropertyName("lastSpoken");
                            json.WriteValue(speech.LastSpoken);
                            json.WritePropertyName("gameObjectCount");
                            json.WriteValue(gameObjectCount);
                            // The tripwire: three prefixes, one per key scan. A zero here means the
                            // game is seeing the mod's keys as well as the mod is, which looks like a
                            // test result rather than like a failure.
                            json.WritePropertyName("keyStandDown");
                            DevProbe.WritePatches(json);
                            json.WriteEndObject();
                        });
                    })
            );
        }

        /// <summary>The live AGE hierarchy, as JSON - or, with <c>fields=</c>, as one plain-text line
        /// per widget carrying only what was asked for.</summary>
        private DevResponse Age(DevRequest request)
        {
            string window = request.QueryValue("window");
            // QueryInt falls back silently, so visibleOnly=false would have read as TRUE and the
            // caller would never know: a declared parameter has to be rejected like an undeclared
            // one when its value makes no sense.
            string depthText = request.QueryValue("depth");
            int depth = AgeDump.DefaultDepth;
            if (depthText != null && (!int.TryParse(depthText, out depth) || depth < 0))
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error("depth= expects a whole number of levels, not '" + depthText + "'")
                );
            }

            bool visibleOnly;
            string visibleText = request.QueryValue("visibleOnly");
            if (!ParseFlag(visibleText, true, out visibleOnly))
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error(
                        "visibleOnly= expects 1/0 or true/false, not '" + visibleText + "'"
                    )
                );
            }

            string projection = request.QueryValue("fields");
            if (projection == null)
            {
                return DevResponse.Json(
                    (string)_host.MainThread.Run(() => AgeDump.Dump(window, depth, visibleOnly))
                );
            }

            List<string> fields = AgeDump.ParseFields(projection);
            if (fields.Count == 0)
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error("fields= names no field; /gui/age can project: " + AgeDump.KnownFields())
                );
            }

            string unknown = AgeDump.UnknownField(fields);
            if (unknown != null)
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error(
                        "unknown field '" + unknown + "' in fields=; /gui/age can project: "
                            + AgeDump.KnownFields()
                    )
                );
            }

            return Plain(
                (string)_host.MainThread.Run(() => AgeDump.Lines(window, depth, visibleOnly, fields))
            );
        }

        /// <summary>A query flag written either way callers write one: 1/0 or true/false. False for
        /// a value that is neither, so the route can say so rather than quietly using its default.
        /// </summary>
        private static bool ParseFlag(string text, bool fallback, out bool value)
        {
            value = fallback;
            if (text == null)
            {
                return true;
            }

            if (text == "1" || string.Compare(text, "true", StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = true;
                return true;
            }

            if (
                text == "0"
                || string.Compare(text, "false", StringComparison.OrdinalIgnoreCase) == 0
            )
            {
                value = false;
                return true;
            }

            return false;
        }

        /// <summary>The focused screen's accessible tree in one answer - or, with <c>screen=</c>, what
        /// another registered screen would offer without going there. Text, not JSON: every line of it
        /// is a sentence meant to be read.</summary>
        private DevResponse Graph(DevRequest request)
        {
            bool edges = request.QueryInt("edges", 0) != 0;
            bool buffers = request.QueryInt("buffers", 0) != 0;
            string wanted = request.QueryValue("screen");
            if (string.IsNullOrEmpty(wanted))
            {
                return Plain((string)_host.MainThread.Run(() => GraphDump.Dump(edges, buffers)));
            }

            // The screen registry belongs to the main thread, so resolving the key and dumping what
            // it names happen in the same visit rather than across a gap a reload could fall into.
            return (DevResponse)
                _host.MainThread.Run(() =>
                {
                    // Spelled out in full: UnityEngine has a Screen of its own.
                    ES2Access.Screens.ScreenManager screens = ModEntry.Screens;
                    ES2Access.Screens.Screen screen =
                        screens == null ? null : screens.Find(wanted);
                    if (screen == null)
                    {
                        return DevResponse.Json(
                            400,
                            DevJson.Error(
                                "no screen keyed '" + wanted + "'; the registered screens are: "
                                    + GraphDump.KnownScreens(screens)
                            )
                        );
                    }

                    return Plain(GraphDump.DumpScreen(screen, edges, buffers));
                });
        }

        private static DevResponse Plain(string text)
        {
            return new DevResponse
            {
                ContentType = "text/plain; charset=utf-8",
                Body = Encoding.UTF8.GetBytes(text),
            };
        }

        /// <summary>
        /// Run one of the mod's actions as though its key had been pressed, and report what became of
        /// it - who consumed it, or why nobody could - together with what it made the mod say.
        ///
        /// Unlike <c>ModEntry.Navigator.Dispatch("ui.down")</c> through /eval, this goes through the
        /// PRODUCTION path: the same queue drain point in the frame, the same Dispatch delegate, the
        /// same stand-down when a game text field holds the keyboard. So a screen that answers over
        /// /eval and not over /input is a screen whose keys do not reach it, which is the bug /eval
        /// cannot see.
        ///
        /// Which consumer took it is read off the action's own name rather than reported by the
        /// dispatcher: the buffer and navigation action sets are disjoint by prefix
        /// (<c>buffer.</c>/<c>ui.</c>) and each dispatcher answers only for its own, so the name is as
        /// good as an observation and costs the production path nothing.
        /// </summary>
        private DevResponse Input(DevRequest request)
        {
            string key = (request.Body ?? string.Empty).Trim();
            long spokenBefore = _speech.Cursor;

            object queued = _host.MainThread.Run(() => Queue(key));
            string unknown = queued as string;
            if (unknown != null)
            {
                return DevResponse.Json(404, unknown);
            }

            ModInput.Injection injection = (ModInput.Injection)queued;
            if (!injection.Done.WaitOne(InjectionTimeoutMilliseconds, false))
            {
                return DevResponse.Json(
                    503,
                    DevJson.Error(
                        "the game did not run '"
                            + key
                            + "' within "
                            + InjectionTimeoutMilliseconds
                            + " ms"
                    )
                );
            }

            List<SpeechLog.Entry> spoken = Settled(spokenBefore);
            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("ok");
                    json.WriteValue(injection.Error == null);
                    json.WritePropertyName("action");
                    json.WriteValue(key);
                    json.WritePropertyName("outcome");
                    json.WriteValue(Outcome(injection));
                    if (injection.StoodDown)
                    {
                        json.WritePropertyName("standingDown");
                        json.WriteValue("game owns keyboard");
                    }

                    json.WritePropertyName("error");
                    json.WriteValue(injection.Error);
                    json.WritePropertyName("speech");
                    json.WriteStartArray();
                    foreach (SpeechLog.Entry entry in spoken)
                    {
                        json.WriteValue(entry.Text);
                    }

                    json.WriteEndArray();
                    json.WriteEndObject();
                })
            );
        }

        /// <summary>
        /// Type characters at the focused screen - the type-ahead search - and report what the
        /// search made of them, together with what it said.
        ///
        /// It cannot go through /input: that queue carries ACTIONS, and typing is text. So this
        /// hands the characters to the navigator's own typed-character source and runs the same tick
        /// the frame would have run, gates included - a screen that opted out, or a game text field
        /// holding the keyboard, answers here exactly as it would to a real keyboard.
        /// </summary>
        private DevResponse Type(DevRequest request)
        {
            string text = request.Body ?? string.Empty;
            if (text.Length == 0)
            {
                return DevResponse.Json(400, DevJson.Error("the body is the characters to type"));
            }

            long spokenBefore = _speech.Cursor;
            TypedReport report = (TypedReport)_host.MainThread.Run(() => Typed(text));
            if (report == null)
            {
                return DevResponse.Json(503, DevJson.Error("the navigator is not up"));
            }

            List<SpeechLog.Entry> spoken = Settled(spokenBefore);
            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("typed");
                    json.WriteValue(text);
                    // False with a screen up means the search took none of it: the screen opted
                    // out, a game text field has the keyboard, or nothing there was searchable.
                    json.WritePropertyName("taken");
                    json.WriteValue(report.Taken);
                    json.WritePropertyName("searching");
                    json.WriteValue(report.Searching);
                    json.WritePropertyName("search");
                    json.WriteValue(report.Search);
                    json.WritePropertyName("results");
                    json.WriteValue(report.Results);
                    json.WritePropertyName("focus");
                    json.WriteValue(report.Focus);
                    json.WritePropertyName("speech");
                    json.WriteStartArray();
                    foreach (SpeechLog.Entry entry in spoken)
                    {
                        json.WriteValue(entry.Text);
                    }

                    json.WriteEndArray();
                    json.WriteEndObject();
                })
            );
        }

        /// <summary>What a frame of typing did, read off the navigator on the main thread and
        /// written out on the HTTP one.</summary>
        private sealed class TypedReport
        {
            public bool Taken;
            public bool Searching;
            public string Search;
            public int Results;
            public string Focus;
        }

        // Main thread: typing is navigation, and navigation is the game's own state.
        private static TypedReport Typed(string text)
        {
            ES2Access.UI.GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null)
            {
                return null;
            }

            navigator.TypeText(text);
            bool taken = navigator.TypeAheadTick();
            return new TypedReport
            {
                Taken = taken,
                Searching = navigator.SearchIsActive,
                Search = navigator.SearchText,
                Results = navigator.SearchResultCount,
                Focus = navigator.FocusedKey == null ? null : navigator.FocusedKey.ToString(),
            };
        }

        private static string Outcome(ModInput.Injection injection)
        {
            if (injection.StoodDown)
            {
                return "standing down: game owns keyboard";
            }

            if (!injection.Consumed)
            {
                return "unconsumed";
            }

            return injection.ActionKey.StartsWith("buffer.")
                ? "consumed (buffers)"
                : "consumed (navigator)";
        }

        // Main thread: the action list and the queue both belong to the live input layer. Returns the
        // injection, or the error JSON for a name that is not one of ours.
        private static object Queue(string key)
        {
            ModInput input = ModEntry.Input;
            if (input == null)
            {
                return DevJson.Error("the mod's input layer is not up");
            }

            // Spelled out in full: the game has its own InputAction in the global namespace.
            ES2Access.UI.Input.InputAction action = input.Find(key);
            if (action != null)
            {
                return input.Inject(action);
            }

            StringBuilder known = new StringBuilder();
            foreach (ES2Access.UI.Input.InputAction registered in input.Actions)
            {
                if (known.Length > 0)
                {
                    known.Append(", ");
                }

                known.Append(registered.Key);
            }

            return DevJson.Error(
                "no action named '" + key + "'; the registered actions are: " + known
            );
        }

        // The speech an action provoked usually lands a frame or two later, so the answer waits for
        // quiet the way POST /eval does - from this thread, never the main one, since the game has to
        // keep running frames for anything to be said at all.
        private List<SpeechLog.Entry> Settled(long since)
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch quiet = Stopwatch.StartNew();
            long cursor = since;

            while (
                quiet.ElapsedMilliseconds < SettleMilliseconds
                && total.ElapsedMilliseconds < MaxSpeechWaitMilliseconds
            )
            {
                Thread.Sleep(SpeechPollMilliseconds);
                long now = _speech.Cursor;
                if (now != cursor)
                {
                    cursor = now;
                    quiet.Reset();
                    quiet.Start();
                }
            }

            long next;
            return _speech.Since(since, out next);
        }

        /// <summary>
        /// Boot straight into a saved game, so one command goes from a cold launch to in-game. The
        /// game's own +load_game argument is no use for that: its lookup runs before the save
        /// system exists on a retail boot and falls through to the main menu without a word.
        ///
        /// A caller that arrives too early gets 503 and an "[not ready]" message rather than a
        /// failure, because "too early" is the normal case - the dev server answers while the game
        /// is still building its main menu - and the answer is to ask again in a second.
        /// </summary>
        private DevResponse LoadSave(DevRequest request)
        {
            string title = (request.Body ?? string.Empty).Trim();
            return (DevResponse)_host.MainThread.Run(() => Load(title));
        }

        // Main thread: everything here is game state.
        private static object Load(string title)
        {
            IGameSerializationService saves =
                Amplitude.Unity.Framework.Services.GetService<IGameSerializationService>();
            if (saves == null)
            {
                return NotReady("the save system is not up yet");
            }

            GameSaveDescriptor save = Resolve(saves, title);
            if (save == null)
            {
                return DevResponse.Json(
                    404,
                    DevJson.Error(
                        title.Length == 0
                            ? "there are no saves to load"
                            : "no save titled '" + title + "'"
                    )
                );
            }

            global::MainMenuScreen menu = Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<global::MainMenuScreen>(false)
                : null;
            if (menu != null && menu.Shown && menu.IsReady)
            {
                saves.LoadGame(save);
                return Loaded(save, "from the main menu");
            }

            global::GameClient client = Client();
            if (client != null && !client.Disconnecting)
            {
                // The route the game's own in-game load takes (LoadSaveModalWindow.DeferredLoad):
                // the session disconnects with the save to reopen on, so the running game is torn
                // down first instead of a second one being loaded on top of it.
                client.Disconnect(GameDisconnectionReason.ClientLoadSave, 0, save);
                return Loaded(save, "from the running session");
            }

            return NotReady("neither the main menu nor a running session can start a load yet");
        }

        /// <summary>The save with this title, matched case-insensitively; the most recently written
        /// save when no title was asked for.</summary>
        private static GameSaveDescriptor Resolve(IGameSerializationService saves, string title)
        {
            GameSaveDescriptor newest = null;
            foreach (GameSaveDescriptor save in saves.GetAllGameSaveDescriptors(false))
            {
                if (title.Length == 0)
                {
                    if (newest == null || save.DateTime > newest.DateTime)
                    {
                        newest = save;
                    }
                }
                else if (string.Compare(save.Title, title, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return save;
                }
            }

            return newest;
        }

        private static global::GameClient Client()
        {
            ISessionService sessions =
                Amplitude.Unity.Framework.Services.GetService<ISessionService>();
            global::Session session =
                sessions == null ? null : sessions.Session as global::Session;
            return session == null ? null : session.GameClient as global::GameClient;
        }

        private static DevResponse Loaded(GameSaveDescriptor save, string how)
        {
            return DevResponse.Json(
                Text("loaded '" + save.Title + "' (turn " + save.TurnPlusOne + ") " + how)
            );
        }

        private static DevResponse NotReady(string why)
        {
            return DevResponse.Json(503, DevJson.Error("[not ready] " + why + "; retry"));
        }

        private static string Text(string result)
        {
            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("result");
                json.WriteValue(result);
                json.WriteEndObject();
            });
        }

        /// <summary>
        /// What the mod has said since sequence N. With <c>wait=MS</c> the answer is held open until
        /// there is something newer, up to that many milliseconds - so a caller can ask "what does it
        /// say next" and be answered on the frame it is said, instead of polling and having to guess a
        /// sleep long enough not to miss it and short enough not to waste the test's time.
        ///
        /// The wait blocks this HTTP thread only. The main thread is what produces speech, so a route
        /// that waited there would be waiting for itself.
        /// </summary>
        private DevResponse Speech(DevRequest request)
        {
            long since = request.QueryLong("since", 0);
            int wait = request.QueryInt("wait", 0);
            if (wait > 0)
            {
                _speech.WaitForNewer(since, wait > MaxWaitMilliseconds ? MaxWaitMilliseconds : wait);
            }

            long next;
            List<SpeechLog.Entry> entries = _speech.Since(since, out next);

            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("entries");
                    json.WriteStartArray();
                    foreach (SpeechLog.Entry entry in entries)
                    {
                        json.WriteStartObject();
                        json.WritePropertyName("seq");
                        json.WriteValue(entry.Seq);
                        json.WritePropertyName("text");
                        json.WriteValue(entry.Text);
                        json.WriteEndObject();
                    }

                    json.WriteEndArray();
                    json.WritePropertyName("next");
                    json.WriteValue(next);
                    json.WriteEndObject();
                })
            );
        }
    }
}

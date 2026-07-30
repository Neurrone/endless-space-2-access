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
    ///   GET  /gui/age?window=&amp;depth=&amp;visibleOnly=
    ///                           the live AGE hierarchy read as accessible meaning (see AgeDump)
    ///   GET  /gui/graph?edges=1&amp;buffers=1
    ///                           the focused screen's whole accessible tree (see GraphDump)
    ///   POST /input             body = an action key; run it as a keypress would (see ModInput)
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
            _host.RegisterRoute("GET", "/status", Status);
            _host.RegisterRoute("GET", "/speech", Speech);
            _host.RegisterRoute("GET", "/gui/age", Age);
            _host.RegisterRoute("GET", "/gui/graph", Graph);
            _host.RegisterRoute("POST", "/input", Input);
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

        private DevResponse Age(DevRequest request)
        {
            string window = request.QueryValue("window");
            int depth = request.QueryInt("depth", AgeDump.DefaultDepth);
            bool visibleOnly = request.QueryInt("visibleOnly", 1) != 0;

            return DevResponse.Json(
                (string)_host.MainThread.Run(() => AgeDump.Dump(window, depth, visibleOnly))
            );
        }

        /// <summary>The focused screen's accessible tree in one answer. Text, not JSON: every line of
        /// it is a sentence meant to be read.</summary>
        private DevResponse Graph(DevRequest request)
        {
            bool edges = request.QueryInt("edges", 0) != 0;
            bool buffers = request.QueryInt("buffers", 0) != 0;
            string dump = (string)
                _host.MainThread.Run(() => GraphDump.Dump(edges, buffers));
            return new DevResponse
            {
                ContentType = "text/plain; charset=utf-8",
                Body = Encoding.UTF8.GetBytes(dump),
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

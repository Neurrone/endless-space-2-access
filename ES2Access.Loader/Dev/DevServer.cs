using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace ES2Access.Loader.Dev
{
    /// <summary>
    /// In-process dev driver, on unless ES2ACCESS_NO_DEV=1. It exists so a developer or an AI
    /// agent who can neither see the screen nor hear the screen reader can still observe and
    /// steer the game: dump the live UI hierarchy, grab the frame the game is rendering, run C#
    /// against the running process, swap in a freshly built mod, and shut the game down. Bound to
    /// 127.0.0.1 only, so it is reachable from this machine alone.
    ///
    /// The loader owns these routes, so they keep answering when the mod is broken or unloaded:
    ///
    ///   GET  /gui/game?path=&amp;depth=    live Unity hierarchy as JSON (see GuiDump)
    ///   GET  /screenshot                the rendered frame as image/png
    ///   GET  /log?since=&amp;grep=         everything BepInEx logged, cursor-polled
    ///   GET  /loader/status             loader version, whether the mod is up, reload history
    ///   POST /reload                    rebuild-and-swap the mod assembly on the next frame
    ///   POST /eval?settle=&amp;speech=     run the C# in the body, and report what it made the mod say
    ///   POST /wait?timeout=            block until the boolean expression in the body holds
    ///   POST /quit                      exit the game
    ///
    /// Everything else comes from the route registry the mod fills in through
    /// <see cref="ModHost"/> (/status, /speech). Those answer 404 while the mod is down, which is
    /// the honest answer: there is nothing to report.
    ///
    /// Requests arrive on the HTTP thread; anything that touches Unity is queued onto the main
    /// thread and waited for (503 when the game does not get to it). Not shipped to players.
    /// </summary>
    internal sealed class DevServer
    {
        public const string DisableEnv = "ES2ACCESS_NO_DEV";
        public const string PortEnv = "ES2ACCESS_DEV_PORT";

        private const int DefaultPort = 8771;
        private const int ScreenshotTimeoutMilliseconds = 5000;

        private const int DefaultWaitMilliseconds = 5000;
        private const int MaxWaitMilliseconds = 60000;

        // A wait ends itself on the frame its deadline passes; this is only the backstop for a
        // game that has stopped producing frames at all, so the HTTP thread is never stuck.
        private const int WaitBackstopMilliseconds = 2000;

        private const int DefaultSettleMilliseconds = 700;
        private const int MaxSettleMilliseconds = 3000;
        private const int MaxSpeechWaitMilliseconds = 3000;
        private const int SpeechPollMilliseconds = 25;

        private const int SpokenCapacity = 200;
        private const int LogCapacity = 2000;

        // Long enough for the response to reach the client before the process goes away.
        private const float QuitDelaySeconds = 0.25f;

        private readonly LoaderPlugin _plugin;
        private readonly MainThreadQueue _mainThread = new MainThreadQueue();
        private readonly PredicateWaits _waits = new PredicateWaits();
        private readonly SeqLog _spoken = new SeqLog(SpokenCapacity);
        private readonly SeqLog _log = new SeqLog(LogCapacity);
        private readonly object _routeLock = new object();
        private readonly Dictionary<string, DevRouteHandler> _modRoutes =
            new Dictionary<string, DevRouteHandler>();

        private DevHttpServer _http;
        private BepInExLogTap _logTap;
        private CSharpEvaluator _evaluator;

        public DevServer(LoaderPlugin plugin)
        {
            _plugin = plugin;
        }

        /// <summary>The mod lifecycle the /loader/status and /reload routes drive. Set once, by
        /// the plugin, before <see cref="Start"/>.</summary>
        public ModLoader Mods;

        public MainThreadQueue MainThread
        {
            get { return _mainThread; }
        }

        /// <summary>Bring up the HTTP front end. The queue and the registry work either way, so
        /// ES2ACCESS_NO_DEV=1 only takes away the remote control, not the mod.</summary>
        public void Start()
        {
            if (Environment.GetEnvironmentVariable(DisableEnv) == "1")
            {
                LoaderLog.Info("Dev server disabled (" + DisableEnv + "=1)");
                return;
            }

            // An unattended test run drives the game from another process, so the window never
            // has focus; without this Unity would stop simulating and every wait would time out.
            Application.runInBackground = true;

            _logTap = new BepInExLogTap(_log);
            BepInEx.Logging.Logger.Listeners.Add(_logTap);

            int port = DefaultPort;
            string configuredPort = Environment.GetEnvironmentVariable(PortEnv);
            if (!string.IsNullOrEmpty(configuredPort))
            {
                int.TryParse(configuredPort, out port);
            }

            try
            {
                _http = new DevHttpServer(port, Handle);
                _http.Start();
                LoaderLog.Info("Dev server listening on " + _http.Address);
            }
            catch (Exception e)
            {
                LoaderLog.Error("Dev server failed to start: " + e);
                _http = null;
            }
        }

        /// <summary>Run the work HTTP requests queued for the main thread, then ask every
        /// outstanding /wait whether it is done. Call once per frame.</summary>
        public void Tick()
        {
            _mainThread.Drain();
            _waits.Tick();
        }

        public void Stop()
        {
            if (_http != null)
            {
                _http.Stop();
                _http = null;
            }

            if (_logTap != null)
            {
                BepInEx.Logging.Logger.Listeners.Remove(_logTap);
                _logTap.Dispose();
                _logTap = null;
            }
        }

        /// <summary>Record a line the mod spoke, so POST /eval can report what it provoked. Kept
        /// here rather than in the mod because it has to outlive a hot reload.</summary>
        public void NotifySpoken(string text)
        {
            _spoken.Add(text);
        }

        public void RegisterModRoute(string method, string path, DevRouteHandler handler)
        {
            lock (_routeLock)
            {
                _modRoutes[Key(method, path)] = handler;
            }
        }

        public void UnregisterModRoutes()
        {
            lock (_routeLock)
            {
                _modRoutes.Clear();
            }
        }

        /// <summary>Let a REPL session that outlives a hot reload see the new mod assembly's
        /// types. No-op until someone has actually used /eval.</summary>
        public void ReferenceModAssembly(Assembly assembly)
        {
            if (_evaluator != null)
            {
                _evaluator.Reference(assembly);
            }
        }

        // Runs on an HTTP pool thread, one per request and possibly several at once.
        private DevResponse Handle(DevRequest request)
        {
            try
            {
                if (request.Method == "GET" && request.Path == "/gui/game")
                {
                    return Gui(request);
                }

                if (request.Method == "GET" && request.Path == "/screenshot")
                {
                    return Screenshot();
                }

                if (request.Method == "GET" && request.Path == "/log")
                {
                    return Log(request);
                }

                if (request.Method == "GET" && request.Path == "/loader/status")
                {
                    return LoaderStatus();
                }

                if (request.Method == "POST" && request.Path == "/reload")
                {
                    return Reload();
                }

                if (request.Method == "POST" && request.Path == "/eval")
                {
                    return Eval(request);
                }

                if (request.Method == "POST" && request.Path == "/wait")
                {
                    return Wait(request);
                }

                if (request.Method == "POST" && request.Path == "/quit")
                {
                    return Quit();
                }

                DevRouteHandler handler;
                lock (_routeLock)
                {
                    _modRoutes.TryGetValue(Key(request.Method, request.Path), out handler);
                }

                if (handler != null)
                {
                    return handler(request);
                }

                return DevResponse.Json(
                    404,
                    DevJson.Error("no route for " + request.Method + " " + request.Path)
                );
            }
            catch (MainThreadTimeoutException e)
            {
                return DevResponse.Json(503, DevJson.Error(e.Message));
            }
            catch (Exception e)
            {
                return DevResponse.Json(500, DevJson.Error(e.Message));
            }
        }

        private DevResponse LoaderStatus()
        {
            DateTime? loaded = Mods.ModFileWrittenUtc;
            DateTime? onDisk = Mods.ModFileOnDiskWrittenUtc;
            bool stale = loaded.HasValue && onDisk.HasValue && onDisk.Value > loaded.Value;

            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("loaderVersion");
                    json.WriteValue(LoaderPlugin.PluginVersion);
                    json.WritePropertyName("modLoaded");
                    json.WriteValue(Mods.ModLoaded);
                    json.WritePropertyName("reloadCount");
                    json.WriteValue(Mods.ReloadCount);
                    json.WritePropertyName("failedReloadCount");
                    json.WriteValue(Mods.FailedReloadCount);
                    json.WritePropertyName("lastReloadError");
                    json.WriteValue(Mods.LastReloadError);
                    json.WritePropertyName("modFileWrittenUtc");
                    json.WriteValue(Iso(loaded));
                    json.WritePropertyName("modFileOnDiskWrittenUtc");
                    json.WriteValue(Iso(onDisk));
                    json.WritePropertyName("staleBuild");
                    json.WriteValue(stale);
                    json.WriteEndObject();
                })
            );
        }

        private static string Iso(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("o", CultureInfo.InvariantCulture) : null;
        }

        // Answers before the swap so the client is not holding a socket open across a reload that
        // may itself throw; the outcome shows up in /loader/status and the game log.
        private DevResponse Reload()
        {
            _mainThread.Post(() => Mods.Reload());
            return DevResponse.Json(DevJson.Ok());
        }

        /// <summary>
        /// Run C# against the game and report both what it returned and what it made the mod say.
        /// Most of what evaluated code is worth doing here is provoking an announcement, and the
        /// speech it provokes usually lands a frame or two later, so by default the answer is held
        /// until speech has gone quiet for a settle window. ?speech=0 drops that wait, and the
        /// speech field with it, when the caller only wants the return value.
        /// </summary>
        private DevResponse Eval(DevRequest request)
        {
            if (string.IsNullOrEmpty(request.Body))
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error("POST /eval expects C# source as the request body")
                );
            }

            bool wantSpeech = request.QueryInt("speech", 1) != 0;
            int settle = Clamp(
                request.QueryInt("settle", DefaultSettleMilliseconds),
                0,
                MaxSettleMilliseconds
            );
            long spokenBefore = _spoken.Cursor;

            CSharpEvaluator.Result result = (CSharpEvaluator.Result)
                _mainThread.Run(() => Evaluate(request.Body));

            // Settling polls the ring from this thread, never the main one: the game has to keep
            // running frames for speech to arrive at all.
            List<SeqLog.Entry> spoken = wantSpeech ? Settled(spokenBefore, settle) : null;

            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("ok");
                    json.WriteValue(result.Ok);
                    json.WritePropertyName("result");
                    json.WriteValue(result.Value);
                    json.WritePropertyName("error");
                    json.WriteValue(result.Error);
                    if (spoken != null)
                    {
                        json.WritePropertyName("speech");
                        json.WriteStartArray();
                        foreach (SeqLog.Entry entry in spoken)
                        {
                            json.WriteValue(entry.Text);
                        }

                        json.WriteEndArray();
                    }

                    json.WriteEndObject();
                })
            );
        }

        // Main thread: the point of the REPL is reaching game state, which is only legal here.
        private CSharpEvaluator.Result Evaluate(string source)
        {
            try
            {
                return Evaluator().Evaluate(source);
            }
            catch (Exception e)
            {
                return CSharpEvaluator.Result.Failed(e.ToString());
            }
        }

        // HTTP thread. Returns once nothing has been spoken for the settle window, or once the
        // overall budget is gone, whichever comes first.
        private List<SeqLog.Entry> Settled(long since, int settleMilliseconds)
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch quiet = Stopwatch.StartNew();
            long cursor = since;

            while (
                quiet.ElapsedMilliseconds < settleMilliseconds
                && total.ElapsedMilliseconds < MaxSpeechWaitMilliseconds
            )
            {
                Thread.Sleep(SpeechPollMilliseconds);

                long now = _spoken.Cursor;
                if (now != cursor)
                {
                    cursor = now;
                    quiet.Reset();
                    quiet.Start();
                }
            }

            long next;
            return _spoken.Since(since, out next);
        }

        /// <summary>
        /// Block until the boolean expression in the body is true, checking it on every frame
        /// rather than on every poll. A condition that holds for a single frame - a transition
        /// announced and then replaced, a panel that opens and closes - is invisible to a caller
        /// sampling from outside the process, and this is how it is caught.
        /// </summary>
        private DevResponse Wait(DevRequest request)
        {
            if (string.IsNullOrEmpty(request.Body))
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error("POST /wait expects a C# boolean expression as the request body")
                );
            }

            int timeout = Clamp(
                request.QueryInt("timeout", DefaultWaitMilliseconds),
                0,
                MaxWaitMilliseconds
            );

            object watched = _mainThread.Run(() => Watch(request.Body, timeout));

            string compileError = watched as string;
            if (compileError != null)
            {
                return DevResponse.Json(WaitJson(false, 0, 0, compileError));
            }

            PredicateWait wait = (PredicateWait)watched;
            if (!wait.Done.WaitOne(timeout + WaitBackstopMilliseconds, false))
            {
                _waits.Remove(wait);
                wait.Abandon("the game stopped producing frames while the wait was pending");
            }

            return DevResponse.Json(
                WaitJson(wait.Satisfied, wait.Frames, wait.ElapsedMilliseconds, wait.Error)
            );
        }

        // Main thread: compiling shares the REPL session, so the expression can name whatever
        // earlier /eval requests declared. Returns the wait, or the compile error as a string.
        private object Watch(string expression, int timeoutMilliseconds)
        {
            CompiledPredicate predicate;
            try
            {
                predicate = Evaluator().CompilePredicate(expression);
            }
            catch (Exception e)
            {
                return e.ToString();
            }

            if (predicate.Error != null)
            {
                return predicate.Error;
            }

            PredicateWait wait = new PredicateWait(predicate, timeoutMilliseconds);
            _waits.Add(wait);
            return wait;
        }

        private static string WaitJson(
            bool satisfied,
            int frames,
            int elapsedMilliseconds,
            string error
        )
        {
            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("ok");
                json.WriteValue(error == null);
                json.WritePropertyName("satisfied");
                json.WriteValue(satisfied);
                json.WritePropertyName("frames");
                json.WriteValue(frames);
                json.WritePropertyName("elapsedMs");
                json.WriteValue(elapsedMilliseconds);
                json.WritePropertyName("error");
                json.WriteValue(error);
                json.WriteEndObject();
            });
        }

        private DevResponse Log(DevRequest request)
        {
            long next;
            List<SeqLog.Entry> entries = SeqLog.Matching(
                _log.Since(request.QueryLong("since", 0), out next),
                request.QueryValue("grep")
            );

            return DevResponse.Json(
                DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    json.WritePropertyName("entries");
                    json.WriteStartArray();
                    foreach (SeqLog.Entry entry in entries)
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

        private CSharpEvaluator Evaluator()
        {
            if (_evaluator == null)
            {
                _evaluator = new CSharpEvaluator();
                if (Mods.ModAssembly != null)
                {
                    _evaluator.Reference(Mods.ModAssembly);
                }
            }

            return _evaluator;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private DevResponse Gui(DevRequest request)
        {
            string path = request.QueryValue("path");
            int depth = request.QueryInt("depth", GuiDump.DefaultDepth);
            return DevResponse.Json((string)_mainThread.Run(() => GuiDump.Dump(path, depth)));
        }

        private DevResponse Screenshot()
        {
            FrameCapture capture = new FrameCapture();
            _mainThread.Run(() =>
            {
                _plugin.StartCoroutine(capture.Run());
                return null;
            });

            if (!capture.Done.WaitOne(ScreenshotTimeoutMilliseconds, false))
            {
                return DevResponse.Json(
                    503,
                    DevJson.Error(
                        "the game did not render a frame within "
                            + ScreenshotTimeoutMilliseconds
                            + " ms"
                    )
                );
            }

            if (capture.Failure != null)
            {
                return DevResponse.Json(500, DevJson.Error(capture.Failure));
            }

            return DevResponse.Png(capture.Png);
        }

        private DevResponse Quit()
        {
            _mainThread.Post(() => _plugin.StartCoroutine(QuitAfterAnswering()));
            return DevResponse.Json(DevJson.Ok());
        }

        private static IEnumerator QuitAfterAnswering()
        {
            yield return new WaitForSeconds(QuitDelaySeconds);
            Application.Quit();
        }

        private static string Key(string method, string path)
        {
            return method + " " + path;
        }

        // Reads the framebuffer, which is only legal once the frame has finished rendering, so it
        // has to run as a coroutine; the requesting HTTP thread waits on Done for the PNG.
        private sealed class FrameCapture
        {
            public readonly ManualResetEvent Done = new ManualResetEvent(false);
            public byte[] Png;
            public string Failure;

            public IEnumerator Run()
            {
                yield return new WaitForEndOfFrame();

                Texture2D frame = null;
                try
                {
                    frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                    frame.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                    frame.Apply();
                    Png = frame.EncodeToPNG();
                }
                catch (Exception e)
                {
                    Failure = e.Message;
                }
                finally
                {
                    if (frame != null)
                    {
                        UnityEngine.Object.Destroy(frame);
                    }

                    Done.Set();
                }
            }
        }
    }
}

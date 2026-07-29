using System;
using System.Collections;
using System.Collections.Generic;
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
    ///   GET  /loader/status             loader version, whether the mod is up, reload history
    ///   POST /reload                    rebuild-and-swap the mod assembly on the next frame
    ///   POST /eval                      compile and run the C# in the body against the game
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

        // Long enough for the response to reach the client before the process goes away.
        private const float QuitDelaySeconds = 0.25f;

        private readonly LoaderPlugin _plugin;
        private readonly MainThreadQueue _mainThread = new MainThreadQueue();
        private readonly object _routeLock = new object();
        private readonly Dictionary<string, DevRouteHandler> _modRoutes =
            new Dictionary<string, DevRouteHandler>();

        private DevHttpServer _http;
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

        /// <summary>Run the work HTTP requests queued for the main thread. Call once per frame.</summary>
        public void Tick()
        {
            _mainThread.Drain();
        }

        public void Stop()
        {
            if (_http != null)
            {
                _http.Stop();
                _http = null;
            }
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

        // Runs on the HTTP thread.
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
                    json.WritePropertyName("lastReloadError");
                    json.WriteValue(Mods.LastReloadError);
                    json.WriteEndObject();
                })
            );
        }

        // Answers before the swap so the client is not holding a socket open across a reload that
        // may itself throw; the outcome shows up in /loader/status and the game log.
        private DevResponse Reload()
        {
            _mainThread.Post(() => Mods.Reload());
            return DevResponse.Json(DevJson.Ok());
        }

        private DevResponse Eval(DevRequest request)
        {
            if (string.IsNullOrEmpty(request.Body))
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error("POST /eval expects C# source as the request body")
                );
            }

            return DevResponse.Json((string)_mainThread.Run(() => Compile(request.Body)));
        }

        // Main thread: the point of the REPL is reaching game state, which is only legal here.
        private string Compile(string source)
        {
            CSharpEvaluator.Result result;
            try
            {
                if (_evaluator == null)
                {
                    _evaluator = new CSharpEvaluator();
                    if (Mods.ModAssembly != null)
                    {
                        _evaluator.Reference(Mods.ModAssembly);
                    }
                }

                result = _evaluator.Evaluate(source);
            }
            catch (Exception e)
            {
                result = CSharpEvaluator.Result.Failed(e.ToString());
            }

            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("ok");
                json.WriteValue(result.Ok);
                json.WritePropertyName("result");
                json.WriteValue(result.Value);
                json.WritePropertyName("error");
                json.WriteValue(result.Error);
                json.WriteEndObject();
            });
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

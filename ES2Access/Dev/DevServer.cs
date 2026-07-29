using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.Dev
{
    /// <summary>
    /// In-process dev driver, on unless ES2ACCESS_NO_DEV=1. It exists so a developer or an AI
    /// agent who can neither see the screen nor hear the screen reader can still observe the
    /// game: read back what the mod spoke, dump the live UI hierarchy, grab the frame the game
    /// is rendering, and shut the game down. Bound to 127.0.0.1 only, so it is reachable from
    /// this machine alone.
    ///
    ///   GET  /status                    mod version, speech backend, last spoken line, scene size
    ///   GET  /speech?since=N            lines spoken after sequence N, plus the next cursor
    ///   GET  /gui/game?path=&amp;depth=    live Unity hierarchy as JSON (see GuiDump)
    ///   GET  /screenshot                the rendered frame as image/png
    ///   POST /quit                      exit the game
    ///
    /// Requests arrive on the HTTP thread; anything that touches Unity is queued onto the main
    /// thread and waited for (503 when the game does not get to it). /speech reads the
    /// thread-safe buffer directly. Not shipped to players.
    /// </summary>
    public sealed class DevServer
    {
        public const string DisableEnv = "ES2ACCESS_NO_DEV";
        public const string PortEnv = "ES2ACCESS_DEV_PORT";

        private const int DefaultPort = 8771;
        private const int ScreenshotTimeoutMilliseconds = 5000;

        // Long enough for the response to reach the client before the process goes away.
        private const float QuitDelaySeconds = 0.25f;

        private readonly MonoBehaviour _host;
        private readonly SpeechLog _speech = new SpeechLog();
        private readonly MainThreadQueue _mainThread = new MainThreadQueue();
        private DevHttpServer _http;

        /// <param name="host">The plugin behaviour that runs coroutines for frame-timed work.</param>
        public DevServer(MonoBehaviour host)
        {
            _host = host;
        }

        public void Start()
        {
            if (Environment.GetEnvironmentVariable(DisableEnv) == "1")
            {
                Log.Info("Dev server disabled (" + DisableEnv + "=1)");
                return;
            }

            int port = DefaultPort;
            string configuredPort = Environment.GetEnvironmentVariable(PortEnv);
            if (!string.IsNullOrEmpty(configuredPort))
            {
                int.TryParse(configuredPort, out port);
            }

            PrismSpeech.Observer = _speech.Add;

            try
            {
                _http = new DevHttpServer(port, Handle);
                _http.Start();
                Log.Info("Dev server listening on " + _http.Address);
            }
            catch (Exception e)
            {
                Log.Error("Dev server failed to start: " + e);
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
            PrismSpeech.Observer = null;
            if (_http != null)
            {
                _http.Stop();
                _http = null;
            }
        }

        // Runs on the HTTP thread.
        private DevResponse Handle(DevRequest request)
        {
            try
            {
                if (request.Method == "GET" && request.Path == "/status")
                {
                    return Status();
                }

                if (request.Method == "GET" && request.Path == "/speech")
                {
                    return Speech(request);
                }

                if (request.Method == "GET" && request.Path == "/gui/game")
                {
                    return Gui(request);
                }

                if (request.Method == "GET" && request.Path == "/screenshot")
                {
                    return Screenshot();
                }

                if (request.Method == "POST" && request.Path == "/quit")
                {
                    return Quit();
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

        private DevResponse Status()
        {
            return DevResponse.Json(
                (string)
                    _mainThread.Run(() =>
                    {
                        PrismSpeech speech = Plugin.Speech;
                        int gameObjectCount = UnityEngine
                            .Object.FindObjectsOfType<GameObject>()
                            .Length;
                        return DevJson.Write(json =>
                        {
                            json.WriteStartObject();
                            json.WritePropertyName("version");
                            json.WriteValue(Plugin.PluginVersion);
                            json.WritePropertyName("speechAvailable");
                            json.WriteValue(speech.Available);
                            json.WritePropertyName("backendName");
                            json.WriteValue(speech.BackendName);
                            json.WritePropertyName("lastSpoken");
                            json.WriteValue(speech.LastSpoken);
                            json.WritePropertyName("gameObjectCount");
                            json.WriteValue(gameObjectCount);
                            json.WriteEndObject();
                        });
                    })
            );
        }

        private DevResponse Speech(DevRequest request)
        {
            long next;
            List<SpeechLog.Entry> entries = _speech.Since(request.QueryLong("since", 0), out next);

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
                _host.StartCoroutine(capture.Run());
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
            _mainThread.Post(() => _host.StartCoroutine(QuitAfterAnswering()));
            return DevResponse.Json(DevJson.Ok());
        }

        private static IEnumerator QuitAfterAnswering()
        {
            yield return new WaitForSeconds(QuitDelaySeconds);
            Application.Quit();
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

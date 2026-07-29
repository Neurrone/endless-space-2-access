using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Loader;
using ES2Access.Loader.Dev;
using UnityEngine;

namespace ES2Access.Dev
{
    /// <summary>
    /// The mod's half of the dev server: what it said, and what state it is in. Registered with
    /// the loader on Start and taken back down on Stop, so an unloaded or broken mod answers 404
    /// here instead of reporting state that no longer exists.
    ///
    ///   GET /status            mod version, speech backend, last spoken line, scene size
    ///   GET /speech?since=N    lines spoken after sequence N, plus the next cursor
    ///   GET /gui/age?window=&amp;depth=&amp;visibleOnly=
    ///                          the live AGE hierarchy read as accessible meaning (see AgeDump)
    ///
    /// /speech reads the thread-safe buffer straight from the HTTP thread; /status and /gui/age
    /// touch the scene, so they go through the main-thread queue and answer 503 if the game is
    /// wedged.
    /// </summary>
    internal sealed class ModRoutes
    {
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
        }

        // The mod's own buffer is what /speech serves; the loader's outlives a hot reload, which
        // is what lets POST /eval report the speech an evaluated call provoked.
        private void Spoken(string text)
        {
            _speech.Add(text);
            _host.NotifySpoken(text);
        }

        /// <summary>The routes themselves are dropped by the loader; this releases the tap on the
        /// speech chokepoint, which is the mod's own static state.</summary>
        public void Unregister()
        {
            PrismSpeech.Observer = null;
        }

        private DevResponse Status(DevRequest request)
        {
            return DevResponse.Json(
                (string)
                    _host.MainThread.Run(() =>
                    {
                        PrismSpeech speech = ModEntry.Speech;
                        int gameObjectCount = Object.FindObjectsOfType<GameObject>().Length;
                        return DevJson.Write(json =>
                        {
                            json.WriteStartObject();
                            json.WritePropertyName("version");
                            json.WriteValue(ModEntry.ModVersion);
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

        private DevResponse Age(DevRequest request)
        {
            string window = request.QueryValue("window");
            int depth = request.QueryInt("depth", AgeDump.DefaultDepth);
            bool visibleOnly = request.QueryInt("visibleOnly", 1) != 0;

            return DevResponse.Json(
                (string)_host.MainThread.Run(() => AgeDump.Dump(window, depth, visibleOnly))
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
    }
}

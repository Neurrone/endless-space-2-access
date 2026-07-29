using System;
using System.IO;
using ES2Access.Core.Native;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.Dev;
using ES2Access.Loader;
using ES2Access.Localization;
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
        public const string ModVersion = "0.1.0";

        /// <summary>Set to 1 to run without a screen reader: nothing is voiced, but everything the
        /// mod would have said is still readable from the dev server.</summary>
        public const string NoSpeechEnv = "ES2ACCESS_NO_SPEECH";

        /// <summary>How long to wait for the game to report its language before announcing the
        /// mod anyway, so a game that never registers the service is not silent at startup.</summary>
        private const float LanguageWaitSeconds = 5f;

        /// <summary>Public so that code compiled by POST /eval can drive speech directly: the
        /// REPL compiles against this assembly from outside it and cannot see internals.</summary>
        public static PrismSpeech Speech;

        private static ModHost _host;
        private static ModRoutes _routes;
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

            _routes = new ModRoutes(host);
            _routes.Register();
            host.SetUpdateHandler(Update);
        }

        public static void Stop()
        {
            if (_host != null)
            {
                _host.SetUpdateHandler(null);
                _host.UnregisterAllModRoutes();
                _host = null;
            }

            if (_routes != null)
            {
                _routes.Unregister();
                _routes = null;
            }

            ModLocale.Reset();
            ModStrings.Reset();

            if (Speech != null)
            {
                Speech.Shutdown();
                Speech = null;
            }
        }

        // Harmony hooks and watchers only set state; all speech happens from this pump,
        // once per frame, so ordering and interruption stay deterministic.
        private static void Update()
        {
            ModLocale.Tick();

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
                    new MessageBuilder().Fragment(ModStrings.Get(ModStrings.StartupReady)),
                    false
                );
            }
        }
    }
}

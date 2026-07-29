using System;
using System.IO;
using BepInEx;
using ES2Access.Core.Native;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.Dev;
using ES2Access.Localization;
using UnityEngine;

namespace ES2Access
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "endless.space2.access";
        public const string PluginName = "Endless Space 2 Access";
        public const string PluginVersion = "0.1.0";

        /// <summary>Set to 1 to run without a screen reader: nothing is voiced, but everything the
        /// mod would have said is still readable from the dev server.</summary>
        public const string NoSpeechEnv = "ES2ACCESS_NO_SPEECH";

        /// <summary>How long to wait for the game to report its language before announcing the
        /// mod anyway, so a game that never registers the service is not silent at startup.</summary>
        private const float LanguageWaitSeconds = 5f;

        internal static PrismSpeech Speech;

        private DevServer _dev;
        private bool _announcedStartup;
        private float _awakeTime;

        private void Awake()
        {
            _awakeTime = Time.realtimeSinceStartup;
            Log.Install(Logger.LogInfo, Logger.LogWarning, Logger.LogError);
            Speech = new PrismSpeech();
            if (Environment.GetEnvironmentVariable(NoSpeechEnv) == "1")
            {
                Log.Info("Speech disabled (" + NoSpeechEnv + "=1)");
            }
            else
            {
                string prismPath = Path.Combine(Paths.GameRootPath, "prism.dll");
                if (NativeLoader.LoadPrism(prismPath))
                {
                    Speech.Initialize();
                }
            }

            _dev = new DevServer(this);
            _dev.Start();
        }

        // Harmony hooks and watchers only set state; all speech happens from this pump,
        // once per frame, so ordering and interruption stay deterministic.
        private void Update()
        {
            ModLocale.Tick();

            // Announce in the player's language if the game has told us what it is; give up
            // waiting after a few seconds rather than lose the announcement.
            if (
                !_announcedStartup
                && (
                    ModLocale.LanguageResolved
                    || Time.realtimeSinceStartup - _awakeTime >= LanguageWaitSeconds
                )
            )
            {
                _announcedStartup = true;
                Speech.Speak(
                    new MessageBuilder().Fragment(ModStrings.Get(ModStrings.StartupReady)),
                    false
                );
            }

            _dev.Tick();
        }

        private void OnDestroy()
        {
            _dev.Stop();
            _dev = null;
            ModLocale.Reset();
            ModStrings.Reset();
            Speech?.Shutdown();
            Speech = null;
        }
    }
}

using System;
using System.IO;
using BepInEx;
using ES2Access.Core.Native;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.Dev;

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

        internal static PrismSpeech Speech;

        private DevServer _dev;
        private bool _announcedStartup;

        private void Awake()
        {
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
            if (!_announcedStartup)
            {
                _announcedStartup = true;
                Speech.Speak(new MessageBuilder().Fragment(PluginName).Fragment("ready"), false);
            }

            _dev.Tick();
        }

        private void OnDestroy()
        {
            _dev.Stop();
            _dev = null;
            Speech?.Shutdown();
            Speech = null;
        }
    }
}

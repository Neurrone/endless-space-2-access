using System.IO;
using BepInEx;
using ES2Access.Core.Native;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "endless.space2.access";
        public const string PluginName = "Endless Space 2 Access";
        public const string PluginVersion = "0.1.0";

        internal static PrismSpeech Speech;

        private bool _announcedStartup;

        private void Awake()
        {
            Log.Install(Logger.LogInfo, Logger.LogWarning, Logger.LogError);
            Speech = new PrismSpeech();
            string prismPath = Path.Combine(Paths.GameRootPath, "prism.dll");
            if (NativeLoader.LoadPrism(prismPath))
            {
                Speech.Initialize();
            }
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
        }

        private void OnDestroy()
        {
            Speech?.Shutdown();
            Speech = null;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using ES2Access.Loader.Dev;
using UnityEngine;

namespace ES2Access.Loader
{
    /// <summary>
    /// The BepInEx plugin, and deliberately the only part of the mod that BepInEx knows about.
    /// It is small and stable so that it never needs reloading: it stands up the dev server,
    /// loads ES2Access.dll from bytes, and from then on just ticks. Everything a screen reader
    /// user actually gets lives in that reloadable assembly, which POST /reload swaps out
    /// without restarting the game.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class LoaderPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "endless.space2.access";
        public const string PluginName = "Endless Space 2 Access";
        public const string PluginVersion = "0.1.0";

        private readonly List<Coroutine> _modCoroutines = new List<Coroutine>();

        private DevServer _dev;
        private ModLoader _mods;
        private Action _modUpdate;

        private void Awake()
        {
            LoaderLog.Install(Logger.LogInfo, Logger.LogWarning, Logger.LogError);

            string pluginDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location
            );
            var devServer = Config.Bind(
                "Dev",
                "devServer",
                false,
                "Enable the loopback developer HTTP server on http://127.0.0.1:8771 (REPL, hot "
                    + "reload, remote control). Development only; leave off to play."
            );

            _dev = new DevServer(this);
            _mods = new ModLoader(this, _dev, pluginDirectory);
            _dev.Mods = _mods;
            _dev.Start(devServer.Value);
            _mods.Load();
        }

        private void Update()
        {
            _dev.Tick();

            Action update = _modUpdate;
            if (update == null)
            {
                return;
            }

            try
            {
                update();
            }
            catch (Exception e)
            {
                // Once per frame forever is not a useful way to report this, and a mod that
                // throws every frame would bury everything else in the log.
                _modUpdate = null;
                LoaderLog.Error(
                    "Mod update handler threw and was switched off; POST /reload to restore it: "
                        + e
                );
            }
        }

        private void OnDestroy()
        {
            _mods.Unload();
            _dev.Stop();
        }

        internal void SetModUpdateHandler(Action update)
        {
            _modUpdate = update;
        }

        internal Coroutine StartModCoroutine(IEnumerator routine)
        {
            Coroutine coroutine = StartCoroutine(routine);
            _modCoroutines.Add(coroutine);
            return coroutine;
        }

        internal void StopModCoroutines()
        {
            foreach (Coroutine coroutine in _modCoroutines)
            {
                StopCoroutine(coroutine);
            }

            _modCoroutines.Clear();
        }
    }
}

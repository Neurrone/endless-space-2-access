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
        private int _maxFrameRate;
        private int _forcedWidth;
        private int _forcedHeight;

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

            var maxFrameRate = Config.Bind(
                "Performance",
                "maxFrameRate",
                0,
                "Cap the frame rate to this many frames per second, to cut the CPU the game "
                    + "burns when it has no GPU acceleration to lean on. 0 disables the cap and "
                    + "leaves the game's own frame rate and vertical sync settings untouched."
            );
            _maxFrameRate = maxFrameRate.Value;

            var resolution = Config.Bind(
                "Performance",
                "resolution",
                "",
                "Force this render resolution as WIDTHxHEIGHT (for example 1280x800), "
                    + "regardless of what the display driver advertises, to cut the CPU a "
                    + "software renderer burns per pixel. Empty leaves the game's own "
                    + "resolution handling untouched."
            );
            ParseForcedResolution(resolution.Value);

            ApplyDisplayOverrides();

            _dev = new DevServer(this);
            _mods = new ModLoader(this, _dev, pluginDirectory);
            _dev.Mods = _mods;
            _dev.Start(devServer.Value);
            _mods.Load();
        }

        private void Update()
        {
            ApplyDisplayOverrides();
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

        private void ParseForcedResolution(string setting)
        {
            string value = setting == null ? "" : setting.Trim();
            if (value.Length == 0)
            {
                return;
            }

            string[] parts = value.Split('x', 'X');
            int width;
            int height;
            if (
                parts.Length != 2
                || !int.TryParse(parts[0].Trim(), out width)
                || !int.TryParse(parts[1].Trim(), out height)
                || width <= 0
                || height <= 0
            )
            {
                LoaderLog.Warn(
                    "Performance/resolution is not a WIDTHxHEIGHT size and was ignored: "
                        + value
                );
                return;
            }

            _forcedWidth = width;
            _forcedHeight = height;
        }

        private void ApplyDisplayOverrides()
        {
            ApplyFrameRateCap();
            ApplyForcedResolution();
        }

        private void ApplyForcedResolution()
        {
            if (_forcedWidth <= 0)
            {
                return;
            }

            // Same re-assert story as the frame cap, and for the same reason: VideoManager
            // pushes the player's stored resolution back through Screen.SetResolution when the
            // video options service binds, and in windowed-fullscreen it re-targets the
            // desktop mode. SetResolution lands a frame or more later, so the comparison
            // retries until it takes and then stays quiet.
            if (Screen.width == _forcedWidth && Screen.height == _forcedHeight)
            {
                return;
            }

            Screen.SetResolution(_forcedWidth, _forcedHeight, Screen.fullScreen);
        }

        private void ApplyFrameRateCap()
        {
            if (_maxFrameRate <= 0)
            {
                return;
            }

            // The game overwrites both of these long after this plugin wakes: Bootloader.Awake
            // turns vertical sync back on, and VideoManager applies the player's saved target
            // frame rate when the video options service binds. So the cap has to be re-asserted,
            // not set once. Application.targetFrameRate is ignored while vertical sync is on.
            if (QualitySettings.vSyncCount != 0)
            {
                QualitySettings.vSyncCount = 0;
            }

            if (Application.targetFrameRate != _maxFrameRate)
            {
                Application.targetFrameRate = _maxFrameRate;
            }
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

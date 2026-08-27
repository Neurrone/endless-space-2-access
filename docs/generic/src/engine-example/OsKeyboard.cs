using System;
using System.Runtime.InteropServices;
using ES2Access.Core.Util;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// The player's own key-repeat settings, read from Windows so that holding an arrow in the mod
    /// feels exactly like holding an arrow anywhere else on their machine. A screen reader user has
    /// usually tuned these hard, and a mod that invents its own repeat rate feels wrong in a way
    /// that is difficult to articulate and impossible to configure.
    ///
    /// Read once and cached (the values change rarely; <see cref="Refresh"/> re-reads them). Any
    /// failure - a non-Windows runtime, a locked-down user32 - falls back to the defaults rather
    /// than disabling repeat.
    /// </summary>
    public static class OsKeyboard
    {
        /// <summary>Seconds a key must be held before it starts repeating.</summary>
        public const float DefaultInitialDelay = 0.4f;

        /// <summary>Seconds between repeats once repeating has started.</summary>
        public const float DefaultRepeatInterval = 0.06f;

        private const uint SpiGetKeyboardDelay = 0x0016; // pvParam receives 0..3
        private const uint SpiGetKeyboardSpeed = 0x000A; // pvParam receives 0..31

        [DllImport("user32.dll", SetLastError = false)]
        private static extern bool SystemParametersInfo(
            uint uiAction,
            uint uiParam,
            ref int pvParam,
            uint fWinIni
        );

        private static bool _loaded;
        private static float _initialDelay = DefaultInitialDelay;
        private static float _repeatInterval = DefaultRepeatInterval;

        public static float InitialDelay
        {
            get
            {
                EnsureLoaded();
                return _initialDelay;
            }
        }

        public static float RepeatInterval
        {
            get
            {
                EnsureLoaded();
                return _repeatInterval;
            }
        }

        /// <summary>Re-read the settings (the player changed them mid-session).</summary>
        public static void Refresh()
        {
            _loaded = false;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            try
            {
                int delay = 0;
                int speed = 0;
                if (
                    SystemParametersInfo(SpiGetKeyboardDelay, 0, ref delay, 0)
                    && SystemParametersInfo(SpiGetKeyboardSpeed, 0, ref speed, 0)
                )
                {
                    // Windows reports the delay as a 0..3 step over roughly 250..1000 ms.
                    delay = delay < 0 ? 0 : (delay > 3 ? 3 : delay);
                    _initialDelay = (delay + 1) * 0.25f;

                    // ... and the speed as a 0..31 step over roughly 2.5..30 repeats per second.
                    speed = speed < 0 ? 0 : (speed > 31 ? 31 : speed);
                    float repeatsPerSecond = 2.5f + (speed / 31f) * (30f - 2.5f);
                    _repeatInterval = 1f / repeatsPerSecond;
                    return;
                }

                Log.Warn("input: could not read the OS key repeat settings; using defaults");
            }
            catch (Exception e)
            {
                Log.Warn("input: OS key repeat settings unavailable (" + e.Message + ")");
            }

            _initialDelay = DefaultInitialDelay;
            _repeatInterval = DefaultRepeatInterval;
        }
    }
}

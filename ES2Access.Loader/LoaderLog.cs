using System;

namespace ES2Access.Loader
{
    /// <summary>
    /// Logging seam for the loader, wired to the BepInEx logger in
    /// <see cref="LoaderPlugin"/>. Deliberately separate from the mod's
    /// <c>ES2Access.Core.Util.Log</c>: the loader has to keep reporting when the mod assembly
    /// failed to load, or was unloaded, and it must not depend on anything the mod owns.
    /// </summary>
    internal static class LoaderLog
    {
        private static Action<string> _info;
        private static Action<string> _warn;
        private static Action<string> _error;

        public static void Install(Action<string> info, Action<string> warn, Action<string> error)
        {
            _info = info;
            _warn = warn;
            _error = error;
        }

        public static void Info(string message)
        {
            if (_info != null)
            {
                _info(message);
            }
        }

        /// <summary>
        /// One step of the shutdown sequence, written to BOTH logs under a greppable prefix.
        /// The BepInEx disk logger has been seen to lose its last lines when the process dies
        /// mid-quit, while Unity's own player log (EndlessSpace2_Data/output_log.txt) kept
        /// everything up to the point the game stopped - so a quit trace that only reached one
        /// of them would be missing exactly where a hang happened.
        /// </summary>
        public static void QuitTrace(string message)
        {
            string line = "quit trace: " + message;
            try
            {
                Info(line);
            }
            catch
            {
                // A logger that fails must not be the thing that ends the trace.
            }

            try
            {
                UnityEngine.Debug.Log(line);
            }
            catch
            {
                // Late in shutdown Unity may no longer accept a log line; the BepInEx half stands.
            }
        }

        public static void Warn(string message)
        {
            if (_warn != null)
            {
                _warn(message);
            }
        }

        public static void Error(string message)
        {
            if (_error != null)
            {
                _error(message);
            }
        }
    }
}

using System;

namespace ES2Access.Core.Util
{
    /// <summary>
    /// Logging seam. Core code logs through here; the plugin installs sinks that route
    /// to the BepInEx logger. Keeping the sinks injectable lets Core stay free of any
    /// BepInEx/Unity reference and lets tests capture output.
    ///
    /// The mod runs on P/Invoke and Harmony patches that fail invisibly, so failures
    /// must be logged, never swallowed.
    /// </summary>
    public static class Log
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

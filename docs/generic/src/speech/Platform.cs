using System;
using System.IO;

namespace ES2Access.Core.Util
{
    /// <summary>
    /// Which operating system the mod is running on, asked once. Core has no UnityEngine, so this
    /// reads the runtime: the game's Mono reports every Unix-like system as one of three platform
    /// ids depending on its age, and the macOS system folder tells the Mac apart from the rest.
    /// </summary>
    public static class Platform
    {
        private static bool _resolved;
        private static bool _isMacOS;

        /// <summary>True on macOS, false on Windows (and on anything else).</summary>
        public static bool IsMacOS
        {
            get
            {
                if (!_resolved)
                {
                    int id = (int)Environment.OSVersion.Platform;
                    bool unixLike = id == 4 || id == 6 || id == 128;
                    _isMacOS = unixLike && Directory.Exists("/System/Library/CoreServices");
                    _resolved = true;
                }

                return _isMacOS;
            }
        }
    }
}

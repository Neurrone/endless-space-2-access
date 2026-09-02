using ES2Access.Core.Settings;

namespace ES2Access.UI.Settings
{
    /// <summary>
    /// WHETHER THE SCANNER SAYS HOW FAR AWAY A RESULT IS IN SHORT FORM - "23s" rather than
    /// "23 south" - on disk and in a field the scanner can read on every keystroke.
    ///
    /// OFF unless the settings file says otherwise (owner ruling 2026-09-02): the long form is what
    /// a player meets first, and the short one is a speed-up somebody asks for once they know the
    /// map. An absent key is therefore false, and a file that has never held the setting says
    /// nothing about it.
    ///
    /// Cached rather than re-read: the scanner asks per result and the settings file is a
    /// dictionary lookup plus a parse. The cache is a mod-assembly static, so a hot reload drops it
    /// and the next ask reads the file again.
    /// </summary>
    public static class ScannerDirectionSettings
    {
        private const string SettingKey = "scanner.short-directions";

        private static bool _read;
        private static bool _short;

        /// <summary>Whether the scanner's per-result direction is said short.</summary>
        public static bool Shortened
        {
            get
            {
                if (!_read)
                {
                    _read = true;
                    _short = ModSettings.File.Get(SettingKey) == "true";
                }

                return _short;
            }
            set
            {
                _read = true;
                _short = value;
                ModSettings.File.Set(SettingKey, value ? "true" : null);
            }
        }

        /// <summary>Drop the cache, so the next ask reads the file again - mod teardown, and a test
        /// that has written the file behind this.</summary>
        public static void Reset()
        {
            _read = false;
            _short = false;
        }
    }
}

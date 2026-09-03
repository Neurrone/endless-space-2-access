using ES2Access.Core.Settings;

namespace ES2Access.UI.Settings
{
    /// <summary>
    /// WHETHER THE TOOLTIPS THE GAME ASSEMBLES ON HOVER - an improvement's stat block, a hero's
    /// dossier, a technology, a ship design - are read out when focus lands on them, on disk and in
    /// a field every declared tooltip can ask on every frame.
    ///
    /// ON unless the settings file says otherwise (owner ruling 2026-09-03). Hearing what the game
    /// would have shown a mouse is what a player arriving at this mod expects, and it is the setting
    /// they can turn off once they know the screens. So the key is written only to say NO: an absent
    /// key - a settings file that has never held it - is on, and turning it off is what puts a value
    /// on disk.
    ///
    /// Cached rather than re-read: every tooltip section a screen declares asks this as the node is
    /// built, which is once per control per frame, and the settings file is a dictionary lookup plus
    /// a parse. The cache is a mod-assembly static, so a hot reload drops it and the next ask reads
    /// the file again.
    /// </summary>
    public static class LongTooltipSettings
    {
        private const string SettingKey = "tooltips.announce-long";

        private static bool _read;
        private static bool _announce;

        /// <summary>Whether a tooltip the game assembles on hover is spoken once it has drawn.
        /// </summary>
        public static bool Announced
        {
            get
            {
                if (!_read)
                {
                    _read = true;
                    _announce = ModSettings.File.Get(SettingKey) != "false";
                }

                return _announce;
            }
            set
            {
                _read = true;
                _announce = value;
                ModSettings.File.Set(SettingKey, value ? null : "false");
            }
        }

        /// <summary>Drop the cache, so the next ask reads the file again - mod teardown, and a test
        /// that has written the file behind this.</summary>
        public static void Reset()
        {
            _read = false;
            _announce = true;
        }
    }
}

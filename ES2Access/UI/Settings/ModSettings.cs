using ES2Access.Core.Settings;
using ES2Access.Core.Util;

namespace ES2Access.UI.Settings
{
    /// <summary>
    /// Where the mod's settings live. The FORMAT is <see cref="SettingsFile"/>'s and the disk is
    /// <see cref="SettingsFileOnDisk"/>'s; the path policy is this class, and is all of it.
    ///
    /// The file sits beside the plugin (<c>ES2Access\settings.cfg</c>), which is the directory the
    /// loader handed the mod and the same one the translations are read from. It cannot be derived
    /// from this assembly's <c>Location</c>: the loader loads the mod from BYTES so that
    /// <c>dotnet build</c> can overwrite the DLL while the game runs, and a bytes-loaded assembly
    /// has no location at all.
    ///
    /// Read once at start. WRITTEN when the mod's options window hides, which is the moment the
    /// player's Apply or Cancel has already settled every value - after Cancel they are the backups
    /// again, after Apply the new ones - so saving there gives Apply-to-persist semantics with
    /// nothing hooked into the game's own private buttons.
    ///
    /// A failure either way is logged and swallowed by <see cref="SettingsFileOnDisk"/>: the mod
    /// must still come up on a read-only install or a corrupt file, with the defaults it compiled
    /// in.
    /// </summary>
    public static class ModSettings
    {
        private const string FileName = "settings.cfg";

        private static string _directory;
        private static SettingsFile _file;

        /// <summary>What the file said, or an empty file where there is none yet. Never null once
        /// <see cref="Load"/> has run.</summary>
        public static SettingsFile File
        {
            get { return _file ?? (_file = new SettingsFile()); }
        }

        /// <summary>Where the settings file is, or null while the mod has not been told.</summary>
        public static string Path
        {
            get
            {
                return string.IsNullOrEmpty(_directory)
                    ? null
                    : System.IO.Path.Combine(_directory, FileName);
            }
        }

        /// <summary>Read the file. <paramref name="pluginDirectory"/> is the loader's own
        /// <c>ModHost.PluginDirectory</c>.</summary>
        public static void Load(string pluginDirectory)
        {
            _directory = pluginDirectory;
            _file = new SettingsFile();
            string path = Path;
            if (path == null)
            {
                Log.Warn("settings: no plugin directory, running on compiled-in defaults");
                return;
            }

            _file = SettingsFileOnDisk.Read(path, "settings");
        }

        /// <summary>Write the file back, keeping every line it held that this build does not use.
        /// </summary>
        public static void Save()
        {
            string path = Path;
            if (path == null || _file == null)
            {
                return;
            }

            SettingsFileOnDisk.Write(path, _file, "settings");
        }

        /// <summary>Let go of everything, so a reload starts from the file again.</summary>
        public static void Reset()
        {
            _directory = null;
            _file = null;
        }
    }
}

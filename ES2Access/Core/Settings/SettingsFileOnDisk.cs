using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ES2Access.Core.Util;

namespace ES2Access.Core.Settings
{
    /// <summary>
    /// A <see cref="SettingsFile"/> on disk - the half that reads and writes one, where
    /// <see cref="SettingsFile"/> is the half that knows the format.
    ///
    /// Everything the mod keeps in the flat <c>key = value</c> form comes through here - the
    /// player's settings beside the plugin, one campaign's map bookmarks - so a missing, corrupt or
    /// read-only file behaves the same way whoever asked for it. A missing file reads as an empty
    /// one, an unreadable file is logged and reads as an empty one, and a write that cannot land is
    /// logged and answered false: the mod must still come up, on its compiled-in defaults, from a
    /// read-only install.
    ///
    /// It knows nothing about WHERE any of those files live - the path is the caller's policy - and
    /// each call carries the word that names the file in the log ("settings", "bookmarks"), so a
    /// warning says which of them failed.
    /// </summary>
    public static class SettingsFileOnDisk
    {
        /// <summary>What the file at <paramref name="path"/> says, or an empty file where there is
        /// none yet or it could not be read. Never null.</summary>
        public static SettingsFile Read(string path, string what)
        {
            try
            {
                if (File.Exists(path))
                {
                    return SettingsFile.Parse(File.ReadAllLines(path, Encoding.UTF8));
                }
            }
            catch (Exception e)
            {
                Log.Warn(what + ": reading " + path + " threw, using defaults: " + e);
            }

            return new SettingsFile();
        }

        /// <summary>Write the file out, creating the directory it belongs in. Answers whether it
        /// landed, so a caller that must know its write reached the disk can ask.</summary>
        public static bool Write(string path, SettingsFile file, string what)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllLines(path, ToArray(file), Encoding.UTF8);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn(what + ": writing " + path + " threw: " + e);
                return false;
            }
        }

        private static string[] ToArray(SettingsFile file)
        {
            IList<string> lines = file.ToLines();
            string[] array = new string[lines.Count];
            lines.CopyTo(array, 0);
            return array;
        }
    }
}

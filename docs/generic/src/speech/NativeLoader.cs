using System;
using System.IO;
using System.Runtime.InteropServices;
using ES2Access.Core.Util;

namespace ES2Access.Core.Native
{
    /// <summary>
    /// Preloads native DLLs by absolute path before any P/Invoke runs.
    ///
    /// BepInEx loads the managed plugin from <c>BepInEx\plugins</c>, which is NOT on the
    /// OS native-DLL search path, so a bare <c>[DllImport("prism")]</c> would fail to find
    /// prism.dll. Calling <c>LoadLibrary</c> with the full path puts the module in the
    /// process under its base name; the later by-name P/Invoke then binds to the
    /// already-loaded module. prism.dll is self-contained (its screen-reader clients,
    /// including the NVDA controller, are statically linked), so nothing else needs
    /// preloading.
    /// </summary>
    public static class NativeLoader
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        private static bool _prismLoaded;

        /// <summary>
        /// Preload the vendored Prism runtime from <paramref name="fullPath"/>. Returns true
        /// once prism.dll is in the process; repeat calls after a success are no-ops.
        /// </summary>
        public static bool LoadPrism(string fullPath)
        {
            if (_prismLoaded)
            {
                return true;
            }

            if (!File.Exists(fullPath))
            {
                Log.Error("native: missing " + fullPath);
                return false;
            }

            if (LoadLibraryW(fullPath) == IntPtr.Zero)
            {
                Log.Error(
                    "native: LoadLibrary failed ("
                        + Marshal.GetLastWin32Error()
                        + ") for "
                        + fullPath
                );
                return false;
            }

            _prismLoaded = true;
            Log.Info("native: loaded " + Path.GetFileName(fullPath));
            return true;
        }
    }
}

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
    /// the Prism library. On Windows, <c>LoadLibrary</c> with the full path puts the module
    /// in the process under its base name and the by-name P/Invoke binds to it. On macOS
    /// the same job is <c>dlopen</c> with <c>RTLD_GLOBAL</c>; Mono's own probe for the
    /// import (it tries <c>libprism.dylib</c> among other spellings) then finds the
    /// already-loaded image, and the game folder also sits on <c>DYLD_LIBRARY_PATH</c>
    /// (macos/run-modded.sh) as the second way to the same file. The library is
    /// self-contained on both systems (its screen-reader clients, including the NVDA
    /// controller, are statically linked), so nothing else needs preloading.
    /// </summary>
    public static class NativeLoader
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlopen")]
        private static extern IntPtr MacOpen(string path, int mode);

        [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlerror")]
        private static extern IntPtr MacError();

        private const int RtldNow = 2;
        private const int RtldGlobal = 8;

        private static bool _prismLoaded;

        /// <summary>True once <see cref="LoadPrism"/> has put the Prism library in the
        /// process, so a caller can tell whether the by-name P/Invokes are safe.</summary>
        public static bool PrismLoaded
        {
            get { return _prismLoaded; }
        }

        /// <summary>
        /// Preload the Prism runtime from <paramref name="fullPath"/> (prism.dll on Windows,
        /// libprism.dylib on macOS). Returns true once the library is in the process; repeat
        /// calls after a success are no-ops.
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

            if (Platform.IsMacOS)
            {
                if (MacOpen(fullPath, RtldNow | RtldGlobal) == IntPtr.Zero)
                {
                    Log.Error(
                        "native: dlopen failed ("
                            + (Marshal.PtrToStringAnsi(MacError()) ?? "no dlerror")
                            + ") for "
                            + fullPath
                    );
                    return false;
                }
            }
            else if (LoadLibraryW(fullPath) == IntPtr.Zero)
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

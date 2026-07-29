using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// Raw P/Invoke surface for prism.dll (Prism v0.16.6, https://github.com/ethindp/prism),
    /// pinned against the vendored include/prism.h at vendor/prism/prism.h.
    ///
    /// ABI facts:
    ///   - Calling convention is <c>__cdecl</c> on Windows (PRISM_CALL).
    ///   - All strings crossing the boundary are UTF-8 (the API has a dedicated
    ///     PRISM_ERROR_INVALID_UTF8). The net35 profile this mod targets has neither
    ///     <c>UnmanagedType.LPUTF8Str</c> nor <c>Marshal.PtrToStringUTF8</c>, so UTF-8 is
    ///     marshaled by hand: inbound strings are passed as NUL-terminated <c>byte[]</c>
    ///     (see <see cref="ToUtf8"/>), and returned <c>const char*</c> are decoded with
    ///     <see cref="FromUtf8"/>.
    ///   - C <c>bool</c> is one byte: every bool here is <c>UnmanagedType.I1</c>.
    ///   - C <c>size_t</c> is pointer-width: bound as <see cref="UIntPtr"/>.
    ///   - PrismContext* / PrismBackend* are opaque: bound as <see cref="IntPtr"/>.
    ///
    /// Only the entry points <see cref="PrismSpeech"/> uses are bound; the header holds the
    /// rest (voice/rate/pitch control, speak_to_memory, braille) if later work needs them.
    /// </summary>
    internal static class PrismNative
    {
        // The native module's base name. NativeLoader preloads the full path first, so this
        // by-name reference resolves to the already-loaded module.
        private const string Dll = "prism";

        /// <summary>PrismConfig: a single version byte (see PRISM_CONFIG_VERSION).</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct PrismConfig
        {
            public byte Version;
        }

        /// <summary>Current config schema version expected by prism_init.</summary>
        public const byte ConfigVersion = 2;

        public enum PrismError
        {
            Ok = 0,
            NotInitialized,
            InvalidParam,
            NotImplemented,
            NoVoices,
            VoiceNotFound,
            SpeakFailure,
            MemoryFailure,
            RangeOutOfBounds,
            Internal,
            NotSpeaking,
            NotPaused,
            AlreadyPaused,
            InvalidUtf8,
            InvalidOperation,
            AlreadyInitialized,
            BackendNotAvailable,
            Unknown,
            InvalidAudioFormat,
            InternalBackendLimitExceeded,
            BackendEnteredUndefinedState,
            Count,
        }

        /// <summary>Bits of the uint64 returned by prism_backend_get_features.</summary>
        [Flags]
        public enum PrismBackendFeature : ulong
        {
            IsSupportedAtRuntime = 1UL << 0,
            SupportsSpeak = 1UL << 2,
            SupportsSpeakToMemory = 1UL << 3,
            SupportsBraille = 1UL << 4,
            SupportsOutput = 1UL << 5,
            SupportsIsSpeaking = 1UL << 6,
            SupportsStop = 1UL << 7,
        }

        // --- Lifecycle ---

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_init(ref PrismConfig cfg);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void prism_shutdown(IntPtr ctx);

        // --- Registry ---

        // Allocating constructor: the returned backend is owned by the caller and must be
        // released with prism_backend_free (unlike prism_registry_acquire_best, which is
        // non-owning). Picks the highest-priority backend usable at runtime.
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_registry_create_best(IntPtr ctx);

        // --- Backend ---

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void prism_backend_free(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_backend_name(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong prism_backend_get_features(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_initialize(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_speak(
            IntPtr backend,
            byte[] utf8Text,
            [MarshalAs(UnmanagedType.I1)] bool interrupt
        );

        // "output" = the screen-reader path (speech plus braille as the backend supports),
        // mirroring Tolk's Output. Preferred for screen-reader backends; speak is TTS-only.
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_output(
            IntPtr backend,
            byte[] utf8Text,
            [MarshalAs(UnmanagedType.I1)] bool interrupt
        );

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_stop(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_error_string(PrismError error);

        // --- UTF-8 marshaling helpers (hand-rolled; see class remarks) ---

        /// <summary>Encode a managed string as a NUL-terminated UTF-8 byte buffer.</summary>
        public static byte[] ToUtf8(string s)
        {
            if (s == null)
            {
                s = string.Empty;
            }

            int len = Encoding.UTF8.GetByteCount(s);
            byte[] buf = new byte[len + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, buf, 0);
            buf[len] = 0;
            return buf;
        }

        /// <summary>Decode a NUL-terminated UTF-8 <c>const char*</c> the library owns.</summary>
        public static string FromUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0)
            {
                len++;
            }

            if (len == 0)
            {
                return string.Empty;
            }

            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        /// <summary>Human-readable text for a PrismError, via the library's own table.</summary>
        public static string ErrorString(PrismError error)
        {
            return FromUtf8(prism_error_string(error));
        }
    }
}

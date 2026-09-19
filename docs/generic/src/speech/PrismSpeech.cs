using System;
using System.IO;
using ES2Access.Core.Native;
using ES2Access.Core.Speech.Mac;
using ES2Access.Core.Util;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// Managed lifetime wrapper over the Prism context plus a single owned backend. One
    /// instance speaks for the whole mod. All native handles stay inside here; callers deal
    /// only in <see cref="MessageBuilder"/>s.
    ///
    /// prism.dll must already be loaded into the process before <see cref="Initialize"/> runs
    /// (the plugin preloads it by full path via NativeLoader), otherwise the first P/Invoke
    /// throws DllNotFoundException.
    ///
    /// On macOS the mod speaks the system voice itself (<see cref="MacSystemVoice"/>) by
    /// default, because VoiceOver's announcement API cannot queue and AVSpeech's own queue
    /// leaves a gap between every line; Prism is the fallback there, taken only when the system
    /// voice cannot be stood up at all. The Macaw screen reader is the exception: it queues with
    /// NVDA's priorities, so it is offered as a backend of its own, reached through the Prism
    /// plugin Macaw links at <see cref="MacawPluginPath"/>. The chokepoint and its callers do
    /// not change; only what is behind <see cref="Initialize"/> does.
    /// </summary>
    public sealed class PrismSpeech
    {
        /// <summary>
        /// Optional tap invoked with every non-empty string sent to speech, before the
        /// <see cref="Available"/> gate, so a dev server can read back spoken text even with no
        /// screen reader running. Null in normal play. Lives here because Speak is the single
        /// speech chokepoint.
        /// </summary>
        public static Action<string> Observer;

        private IntPtr _ctx;
        private IntPtr _backend;
        private bool _useOutput;
        private bool _macawLive;
        private MacSystemVoice _mac;

        /// <summary>True once a backend was created and initialized successfully.</summary>
        public bool Available { get; private set; }

        /// <summary>The most recent non-empty text sent to speech, for a repeat-last hotkey.</summary>
        public string LastSpoken { get; private set; }

        /// <summary>Name of the chosen backend (e.g. "NVDA", "SAPI"), once available.</summary>
        public string BackendName { get; private set; }

        /// <summary>The macOS backend setting's three values: the streamed system voice, Prism
        /// as it ships (VoiceOver when it is running), and the Macaw screen reader through its
        /// Prism plugin.</summary>
        public const string MacBackendSystemVoice = "system";
        public const string MacBackendPrism = "prism";
        public const string MacBackendMacaw = "macaw";

        /// <summary>The backend name Macaw's Prism plugin registers under.</summary>
        public const string MacawBackendName = "Macaw";

        /// <summary>Which backend <see cref="Initialize"/> stands up on macOS. Anything but
        /// <see cref="MacBackendPrism"/> or <see cref="MacBackendMacaw"/> means the system
        /// voice, with the best Prism backend (Macaw when it runs) kept as the cannot-start
        /// fallback; the other two mean that backend, with the system voice as ITS cannot-start
        /// fallback, because the preference persists across launches and must never persist
        /// silence. Set before Initialize; meaningless off the Mac.</summary>
        public string MacBackendPreference = MacBackendSystemVoice;

        /// <summary>Which of the three macOS values is actually speaking: the system voice, Macaw
        /// through its plugin, or another Prism backend - null while speech is unavailable and
        /// off the Mac. What a settings row compares its request against.</summary>
        public string MacLiveBackend
        {
            get
            {
                if (!Available || !Platform.IsMacOS)
                {
                    return null;
                }

                if (_mac != null)
                {
                    return MacBackendSystemVoice;
                }

                return _macawLive ? MacBackendMacaw : MacBackendPrism;
            }
        }

        /// <summary>Where a running Macaw links the Prism plugin that speaks through it: a fixed
        /// path under the user's home, refreshed by Macaw at every launch; absent when Macaw is
        /// not installed, which Prism's add_library reports as a clean failure. Read from HOME
        /// first (what Macaw's own clients do), the profile folder when the launcher stripped it.</summary>
        public static string MacawPluginPath
        {
            get
            {
                string home = Environment.GetEnvironmentVariable("HOME");
                if (string.IsNullOrEmpty(home))
                {
                    home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                }

                return Path.Combine(
                    home,
                    "Library/Application Support/Macaw/prism/libMacawPrismPlugin.dylib"
                );
            }
        }

        /// <summary>The macOS system voice while it is the live backend (its voices and the
        /// voice, rate and volume levers), null anywhere else - Windows, Prism chosen, or
        /// speech unavailable.</summary>
        public MacSystemVoice Mac
        {
            get { return _mac; }
        }

        /// <summary>Tear the live backend down and stand up whatever the current preference
        /// names - how a backend change applies without a restart. Anything queued is lost.</summary>
        public void Reinitialize()
        {
            Shutdown();
            Initialize();
        }

        /// <summary>
        /// Stand up the Prism context and acquire the best available backend. On any failure it
        /// logs the cause and leaves the instance unavailable rather than throwing, so a missing
        /// screen reader degrades to silence instead of crashing the game.
        /// </summary>
        public void Initialize()
        {
            if (Available)
            {
                return;
            }

            if (Platform.IsMacOS)
            {
                bool preferMacaw = MacBackendPreference == MacBackendMacaw;
                bool preferPrism = preferMacaw || MacBackendPreference == MacBackendPrism;
                if (!preferPrism)
                {
                    if (TryStartSystemVoice())
                    {
                        return;
                    }

                    Log.Info("speech: system voice unavailable; falling back to Prism");
                }

                // Macaw asked for: its plugin joins the registry and nothing else will do. Prism
                // asked for: the registry as Prism ships it, so the row that says VoiceOver
                // means VoiceOver even while Macaw runs. The system voice's fallback: the best
                // reader there is, Macaw included.
                if (TryStartPrism(!preferPrism || preferMacaw, preferMacaw))
                {
                    return;
                }

                if (preferPrism)
                {
                    // The mirror of the fallback above. The preference PERSISTS in the settings
                    // file, so a Prism that cannot start must never mean silence on every launch
                    // from here on - the player could not hear the row to switch back.
                    Log.Info(
                        (preferMacaw ? "speech: Macaw unavailable" : "speech: Prism unavailable")
                            + "; falling back to the system voice"
                    );
                    TryStartSystemVoice();
                }

                return;
            }

            TryStartPrism(false, false);
        }

        private bool TryStartSystemVoice()
        {
            MacSystemVoice mac = new MacSystemVoice();
            if (!mac.Start())
            {
                return false;
            }

            _mac = mac;
            BackendName = "System Voice";
            Available = true;
            Log.Info("macOS system voice ready: " + mac.Description);
            return true;
        }

        /// <summary>Stand up Prism. <paramref name="withMacawPlugin"/> adds Macaw's plugin to
        /// the registry the context is bound to, so Macaw outranks VoiceOver whenever it runs;
        /// <paramref name="requireMacaw"/> refuses anything else, for the setting that names
        /// Macaw. Off the Mac both are false and the library's global registry serves.</summary>
        private bool TryStartPrism(bool withMacawPlugin, bool requireMacaw)
        {
            if (Platform.IsMacOS && !NativeLoader.PrismLoaded)
            {
                // Reached with Prism or Macaw chosen, or as the system voice's fallback. Prism's
                // own macOS backends cannot queue the way the stream does (VoiceOver's
                // announcement API replaces pending speech), but a cut-off queue beats silence.
                Log.Error("speech: the Prism library is not loaded; speech is unavailable");
                return false;
            }

            PrismNative.PrismConfig cfg = new PrismNative.PrismConfig
            {
                Version = PrismNative.ConfigVersion,
            };
            IntPtr registry = withMacawPlugin ? BuildRegistryWithMacaw() : IntPtr.Zero;
            cfg.Registry = registry;
            _ctx = PrismNative.prism_init(ref cfg);
            if (registry != IntPtr.Zero)
            {
                // The context holds its own reference (Prism's contract, and measured: the
                // backends stay listed after this); ours is done once init has taken it or
                // failed.
                PrismNative.prism_registry_release(registry);
            }

            if (_ctx == IntPtr.Zero)
            {
                Log.Error("Prism: prism_init returned null context");
                return false;
            }

            // create_best hands back an OWNED backend (freed in Shutdown, unlike the
            // acquire_best borrow). It picks the highest-priority backend usable at runtime:
            // a running screen reader, else SAPI.
            _backend = PrismNative.prism_registry_create_best(_ctx);
            if (_backend == IntPtr.Zero)
            {
                Log.Error(
                    "Prism: no speech backend available (prism_registry_create_best returned null)"
                );
                PrismNative.prism_shutdown(_ctx);
                _ctx = IntPtr.Zero;
                return false;
            }

            PrismNative.PrismError err = PrismNative.prism_backend_initialize(_backend);
            if (
                err != PrismNative.PrismError.Ok
                && err != PrismNative.PrismError.AlreadyInitialized
            )
            {
                Log.Error("Prism: backend initialize failed: " + PrismNative.ErrorString(err));
                PrismNative.prism_backend_free(_backend);
                PrismNative.prism_shutdown(_ctx);
                _backend = IntPtr.Zero;
                _ctx = IntPtr.Zero;
                return false;
            }

            ulong features = PrismNative.prism_backend_get_features(_backend);
            _useOutput = (features & (ulong)PrismNative.PrismBackendFeature.SupportsOutput) != 0;
            BackendName =
                PrismNative.FromUtf8(PrismNative.prism_backend_name(_backend)) ?? "unknown";
            _macawLive = BackendName == MacawBackendName;
            if (requireMacaw && !_macawLive)
            {
                // The plugin is in the registry but its backend was passed over, which its
                // is_supported answers only when no Macaw is accepting connections.
                Log.Info(
                    "speech: Macaw is not running (Prism offered " + BackendName + " instead)"
                );
                PrismNative.prism_backend_free(_backend);
                PrismNative.prism_shutdown(_ctx);
                _backend = IntPtr.Zero;
                _ctx = IntPtr.Zero;
                BackendName = null;
                _macawLive = false;
                return false;
            }

            Available = true;
            Log.Info(
                "Prism speech ready, backend: "
                    + BackendName
                    + (_useOutput ? " (output)" : " (speak)")
            );
            return true;
        }

        /// <summary>A registry of Prism's compiled-in backends plus whatever Macaw's plugin
        /// supplies; the compiled-in set alone when Macaw is not installed (add_library fails
        /// cleanly on a missing file), and null when the builder itself cannot be had, which
        /// hands Prism its global registry instead.</summary>
        private static IntPtr BuildRegistryWithMacaw()
        {
            IntPtr builder = PrismNative.prism_registry_builder_new();
            if (builder == IntPtr.Zero)
            {
                Log.Warn("Prism: no registry builder; Macaw's plugin cannot be offered");
                return IntPtr.Zero;
            }

            string path = MacawPluginPath;
            UIntPtr count;
            PrismNative.PrismError err = PrismNative.prism_registry_builder_add_library(
                builder,
                PrismNative.ToUtf8(path),
                -1,
                out count
            );
            if (err == PrismNative.PrismError.Ok)
            {
                Log.Info("Prism: Macaw's plugin loaded from " + path);
            }
            else
            {
                Log.Info(
                    "Prism: Macaw's plugin not loaded ("
                        + PrismNative.ErrorString(err)
                        + ") from "
                        + path
                );
            }

            IntPtr registry = PrismNative.prism_registry_freeze(builder);
            // Freezing spends the builder; it is still ours to free.
            PrismNative.prism_registry_builder_free(builder);
            if (registry == IntPtr.Zero)
            {
                Log.Warn("Prism: the registry could not be frozen; using the global one");
            }

            return registry;
        }

        /// <summary>
        /// Speak <paramref name="message"/> through the screen-reader output path (speech plus
        /// braille where the backend supports it), falling back to plain TTS on backends without
        /// output. This is the mod's single speech entry point, and it deliberately takes a
        /// <see cref="MessageBuilder"/>, not a raw string: every spoken message is composed
        /// through the builder's separation discipline, so this one chokepoint can uniformly
        /// post-process all speech without each call site building its own string. A null or
        /// empty builder is a no-op. <paramref name="interrupt"/> cuts off current speech.
        /// </summary>
        public void Speak(MessageBuilder message, bool interrupt = true)
        {
            SpeakText(message == null ? null : message.Build(), interrupt);
        }

        /// <summary>
        /// Re-speak the most recent non-empty line (the repeat-last hotkey). No-op until
        /// something has been spoken. Re-emits the already-built text rather than rebuilding, so
        /// it is the one path that legitimately speaks without a fresh builder.
        /// </summary>
        public void RepeatLast(bool interrupt = true)
        {
            SpeakText(LastSpoken, interrupt);
        }

        private void SpeakText(string text, bool interrupt)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            LastSpoken = text;

            Action<string> observer = Observer;
            if (observer != null)
            {
                observer(text);
            }

            if (!Available)
            {
                return;
            }

            if (_mac != null)
            {
                _mac.Speak(text, interrupt);
                return;
            }

            if (interrupt && _macawLive)
            {
                // Macaw's "now" cuts the utterance in flight and lets the rest of its queue
                // resume afterwards. The mod's interrupt means what it means on the system voice
                // and on NVDA: everything pending goes too - so the queue is emptied first.
                PrismNative.prism_backend_stop(_backend);
            }

            byte[] utf8 = PrismNative.ToUtf8(text);
            PrismNative.PrismError err = _useOutput
                ? PrismNative.prism_backend_output(_backend, utf8, interrupt)
                : PrismNative.prism_backend_speak(_backend, utf8, interrupt);
            if (err != PrismNative.PrismError.Ok)
            {
                Log.Warn("Prism: speech failed: " + PrismNative.ErrorString(err));
            }
        }

        /// <summary>Silence any in-progress speech.</summary>
        public void Silence()
        {
            if (!Available)
            {
                return;
            }

            if (_mac != null)
            {
                _mac.Silence();
                return;
            }

            PrismNative.prism_backend_stop(_backend);
        }

        /// <summary>Once per frame, from the pump: a backend that paces speech itself (the macOS
        /// system voice) does its work here. Nothing to do for Prism.</summary>
        public void Update()
        {
            if (_mac != null)
            {
                _mac.Update();
            }
        }

        /// <summary>
        /// Release the owned backend and the context. Safe to call twice, and safe if
        /// <see cref="Initialize"/> never ran or failed.
        /// </summary>
        public void Shutdown()
        {
            Trace("speech shutdown entered");
            if (_mac != null)
            {
                _mac.Stop();
                _mac = null;
            }

            if (_backend != IntPtr.Zero)
            {
                // Stop before free: freeing alone does not guarantee in-flight speech halts.
                Trace("prism_backend_stop");
                PrismNative.prism_backend_stop(_backend);
                Trace("prism_backend_free");
                PrismNative.prism_backend_free(_backend);
                _backend = IntPtr.Zero;
                Trace("backend released");
            }

            if (_ctx != IntPtr.Zero)
            {
                Trace("prism_shutdown");
                PrismNative.prism_shutdown(_ctx);
                _ctx = IntPtr.Zero;
                Trace("context released");
            }

            Available = false;
            _macawLive = false;
            // A dead instance must not keep the last backend's name: Reinitialize reports
            // whatever the NEXT Initialize stands up, and /status reads this between the two.
            BackendName = null;
            Trace("speech shutdown done");
        }

        /// <summary>
        /// Where the shutdown trace goes. Core compiles against the BCL alone, so it cannot reach
        /// Unity's player log itself: the mod installs its own dual logger here at start and takes
        /// it back at stop. Each P/Invoke below can block on a screen reader that is not answering,
        /// and the line before it is the only thing that says which one.
        /// </summary>
        public static Action<string> Tracer;

        private static void Trace(string message)
        {
            Action<string> tracer = Tracer;
            if (tracer == null)
            {
                return;
            }

            try
            {
                tracer(message);
            }
            catch
            {
                // A trace must never be what stops the backend being freed.
            }
        }
    }
}

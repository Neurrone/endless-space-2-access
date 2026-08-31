using System;
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
    /// On macOS the mod speaks the system voice itself (<see cref="MacSystemVoice"/>), because
    /// VoiceOver's announcement API cannot queue and AVSpeech's own queue leaves a gap between
    /// every line; Prism is the fallback there, taken only when the system voice cannot be
    /// stood up at all. The chokepoint and its callers do not change; only what is behind
    /// <see cref="Initialize"/> does.
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
        private MacSystemVoice _mac;

        /// <summary>True once a backend was created and initialized successfully.</summary>
        public bool Available { get; private set; }

        /// <summary>The most recent non-empty text sent to speech, for a repeat-last hotkey.</summary>
        public string LastSpoken { get; private set; }

        /// <summary>Name of the chosen backend (e.g. "NVDA", "SAPI"), once available.</summary>
        public string BackendName { get; private set; }

        /// <summary>The macOS backend setting's two values: the streamed system voice, and Prism
        /// (which is VoiceOver when it is running).</summary>
        public const string MacBackendSystemVoice = "system";
        public const string MacBackendPrism = "prism";

        /// <summary>Which backend <see cref="Initialize"/> stands up on macOS. Anything but
        /// <see cref="MacBackendPrism"/> means the system voice, with Prism kept as the
        /// cannot-start fallback; <see cref="MacBackendPrism"/> means Prism, with the system
        /// voice as ITS cannot-start fallback, because the preference persists across launches
        /// and must never persist silence. Set before Initialize; meaningless off the Mac.</summary>
        public string MacBackendPreference = MacBackendSystemVoice;

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
                bool preferPrism = MacBackendPreference == MacBackendPrism;
                if (!preferPrism)
                {
                    if (TryStartSystemVoice())
                    {
                        return;
                    }

                    Log.Info("speech: system voice unavailable; falling back to Prism");
                }

                if (TryStartPrism())
                {
                    return;
                }

                if (preferPrism)
                {
                    // The mirror of the fallback above. The preference PERSISTS in the settings
                    // file, so a Prism that cannot start must never mean silence on every launch
                    // from here on - the player could not hear the row to switch back.
                    Log.Info("speech: Prism unavailable; falling back to the system voice");
                    TryStartSystemVoice();
                }

                return;
            }

            TryStartPrism();
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

        private bool TryStartPrism()
        {
            if (Platform.IsMacOS && !NativeLoader.PrismLoaded)
            {
                // Reached with Prism chosen, or as the system voice's fallback. Prism's macOS
                // backends cannot queue the way the stream does (VoiceOver's announcement API
                // replaces pending speech), but a cut-off queue beats silence.
                Log.Error("speech: the Prism library is not loaded; speech is unavailable");
                return false;
            }

            PrismNative.PrismConfig cfg = new PrismNative.PrismConfig
            {
                Version = PrismNative.ConfigVersion,
            };
            _ctx = PrismNative.prism_init(ref cfg);
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
            Available = true;
            Log.Info(
                "Prism speech ready, backend: "
                    + BackendName
                    + (_useOutput ? " (output)" : " (speak)")
            );
            return true;
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
            if (_mac != null)
            {
                _mac.Stop();
                _mac = null;
            }

            if (_backend != IntPtr.Zero)
            {
                // Stop before free: freeing alone does not guarantee in-flight speech halts.
                PrismNative.prism_backend_stop(_backend);
                PrismNative.prism_backend_free(_backend);
                _backend = IntPtr.Zero;
            }

            if (_ctx != IntPtr.Zero)
            {
                PrismNative.prism_shutdown(_ctx);
                _ctx = IntPtr.Zero;
            }

            Available = false;
            // A dead instance must not keep the last backend's name: Reinitialize reports
            // whatever the NEXT Initialize stands up, and /status reads this between the two.
            BackendName = null;
        }
    }
}

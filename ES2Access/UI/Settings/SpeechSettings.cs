using System.Globalization;
using ES2Access.Core.Speech;
using ES2Access.Core.Speech.Mac;
using ES2Access.Core.Util;

namespace ES2Access.UI.Settings
{
    /// <summary>
    /// The speech half of the settings file, macOS only: which backend the mod speaks through,
    /// and the system voice's voice, rate and volume. The KEYS and their meanings live here; the
    /// rows that edit them are <see cref="ES2Access.UI.ModOptions.SpeechRows"/>.
    ///
    /// An absent voice or rate key means FOLLOW SPOKEN CONTENT - the OS settings stay the mod's
    /// defaults, and a player who never opens the Speech tab keeps them, changes included. A
    /// stored value wins until the tab's reset button removes it. Volume is the mod's own
    /// (Spoken Content has no per-app volume) and defaults to full.
    ///
    /// <see cref="Configure"/> runs before <c>PrismSpeech.Initialize</c> (the backend choice
    /// decides what Initialize stands up) and <see cref="Apply"/> after it (the levers only exist
    /// on a running system voice). On Windows both are no-ops and the keys are never written.
    /// </summary>
    public static class SpeechSettings
    {
        public const string BackendKey = "speech.backend";
        public const string VoiceKey = "speech.voice";
        public const string RateKey = "speech.rate"; // 0-100, AVSpeech's [0,1] scale times 100
        public const string VolumeKey = "speech.volume"; // 0-100

        public const int VolumeDefault = 100;

        /// <summary>The stored backend: <see cref="PrismSpeech.MacBackendPrism"/> when the file
        /// says so, else the system voice.</summary>
        public static string Backend()
        {
            return ModSettings.File.Get(BackendKey) == PrismSpeech.MacBackendPrism
                ? PrismSpeech.MacBackendPrism
                : PrismSpeech.MacBackendSystemVoice;
        }

        /// <summary>The stored voice key (<see cref="VoiceSelection"/>'s language-and-name form),
        /// or the default key meaning the Spoken Content voice.</summary>
        public static string Voice()
        {
            return ModSettings.File.Get(VoiceKey) ?? VoiceSelection.DefaultKey;
        }

        /// <summary>The stored rate percent, or -1 while the rate follows Spoken Content.</summary>
        public static int RatePercent()
        {
            return Percent(RateKey, -1);
        }

        /// <summary>The stored volume percent; full volume until set.</summary>
        public static int VolumePercent()
        {
            return Percent(VolumeKey, VolumeDefault);
        }

        /// <summary>Before Initialize: hand the backend choice to <paramref name="speech"/>.</summary>
        public static void Configure(PrismSpeech speech)
        {
            if (Platform.IsMacOS)
            {
                speech.MacBackendPreference = Backend();
            }
        }

        /// <summary>After Initialize: put the stored voice, rate and volume onto the running
        /// system voice. Nothing to do when Prism answered or speech is unavailable. The three
        /// per-lever halves below are also what the Speech tab's rows call after writing a key,
        /// so a row's live effect and this whole-file apply cannot drift apart.</summary>
        public static void Apply(PrismSpeech speech)
        {
            ApplyVoice(speech);
            ApplyRate(speech);
            ApplyVolume(speech);
        }

        /// <summary>The stored voice onto the running system voice; nothing without one.</summary>
        public static void ApplyVoice(PrismSpeech speech)
        {
            MacSystemVoice mac = speech == null ? null : speech.Mac;
            if (mac != null)
            {
                mac.SetVoiceKey(Voice());
            }
        }

        /// <summary>The stored rate onto the running system voice; an absent key means the voice
        /// keeps following Spoken Content, so nothing is pushed.</summary>
        public static void ApplyRate(PrismSpeech speech)
        {
            MacSystemVoice mac = speech == null ? null : speech.Mac;
            int rate = RatePercent();
            if (mac != null && rate >= 0)
            {
                mac.SetRate01(rate / 100f);
            }
        }

        /// <summary>The stored volume onto the running system voice.</summary>
        public static void ApplyVolume(PrismSpeech speech)
        {
            MacSystemVoice mac = speech == null ? null : speech.Mac;
            if (mac != null)
            {
                mac.SetVolume01(VolumePercent() / 100f);
            }
        }

        private static int Percent(string key, int fallback)
        {
            string text = ModSettings.File.Get(key);
            int value;
            if (
                text == null
                || !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            )
            {
                return fallback;
            }

            return value < 0 ? 0 : (value > 100 ? 100 : value);
        }
    }
}

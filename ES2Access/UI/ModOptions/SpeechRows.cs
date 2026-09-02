using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using Amplitude.Unity.Options;
using ES2Access.Core.Speech;
using ES2Access.Core.Speech.Mac;
using ES2Access.Core.Util;
using ES2Access.Localization;
using ES2Access.UI.Settings;

namespace ES2Access.UI.ModOptions
{
    /// <summary>The Speech tab's service: it declares no options - every row is minted per row
    /// over its own provider (<see cref="ModRows"/>) - and exists because a panel refuses to load
    /// without a registered service.</summary>
    public interface IModSpeechService : IService { }

    public sealed class ModSpeechService : IModSpeechService { }

    /// <summary>
    /// THE SPEECH TAB, macOS only (<see cref="ModOptions.Categories"/> adds it nowhere else):
    /// which backend the mod speaks through, and the system voice's voice, rate and volume.
    ///
    /// Every row edits the settings file (<see cref="SpeechSettings"/>' keys) and the live
    /// backend in the same write, so a change is heard as it is made; Cancel writes the old
    /// values back through the same writers - the reset button's work included, like any other
    /// change made in the window - and the window's hide persists the file. The voice
    /// picker offers the Spoken Content voice first - the default, followed when the OS setting
    /// changes - then the installed voices FOR THE GAME'S LANGUAGE (every voice when none speaks
    /// it, a dead-end guard), and keeps a picked voice from another language listed rather than
    /// silently dropping it. The reset button hands voice and rate back to Spoken Content by
    /// removing their keys and standing the backend up again, which is also what re-reads the OS
    /// values.
    ///
    /// The voice, rate and volume rows are the SYSTEM VOICE's, and a caption above them says so:
    /// with Prism as the live backend they still read and write the stored settings - heard when
    /// the system voice next stands up - but move no live lever (VoiceOver's own voice and rate
    /// belong to VoiceOver). Choosing the Prism backend is validated as it applies: if Prism
    /// cannot start, the stored preference is put back and a message box says so, because a
    /// persisted backend that cannot speak would boot every later session silent
    /// (<see cref="PrismSpeech.Initialize"/> guards those launches the same way).
    /// </summary>
    public static class SpeechRows
    {
        private static OptionDropListItem _backendItem;
        private static OptionDropListItem _voiceItem;
        private static OptionSliderItem _rateItem;

        public static void Fill(OptionsTabPanel panel)
        {
            if (panel == null || panel.OptionsTable == null)
            {
                Log.Warn("mod options: the Speech panel is not built, no rows added");
                return;
            }

            ModRows.Begin(panel);
            List<Option> options = new List<Option>();
            Option backend = ModRows.Choice(
                panel,
                "SpeechBackend",
                ModStrings.Get(ModStrings.SpeechBackend),
                new[] { PrismSpeech.MacBackendSystemVoice, PrismSpeech.MacBackendPrism },
                new[]
                {
                    ModStrings.Get(ModStrings.SpeechBackendSystemVoice),
                    ModStrings.Get(ModStrings.SpeechBackendPrism),
                },
                SpeechSettings.Backend,
                SetBackend,
                out _backendItem
            );
            Publishable(options, backend);

            Publishable(
                options,
                ModRows.Caption(
                    panel,
                    "SpeechSystemVoiceSection",
                    ModStrings.Get(ModStrings.SpeechSystemVoiceSection)
                )
            );

            List<string> voiceValues;
            List<string> voiceLabels;
            VoiceChoices(out voiceValues, out voiceLabels);
            Option voice = ModRows.Choice(
                panel,
                "SpeechVoice",
                ModStrings.Get(ModStrings.SpeechVoice),
                voiceValues,
                voiceLabels,
                SpeechSettings.Voice,
                SetVoice,
                out _voiceItem
            );
            Publishable(options, voice);

            Option rate = ModRows.Slider(
                panel,
                "SpeechRate",
                ModStrings.Get(ModStrings.SpeechRate),
                0f,
                100f,
                5f,
                ReadRate,
                WriteRate,
                out _rateItem
            );
            Publishable(options, rate);

            OptionSliderItem volumeItem;
            Option volume = ModRows.Slider(
                panel,
                "SpeechVolume",
                ModStrings.Get(ModStrings.SpeechVolume),
                0f,
                100f,
                5f,
                ReadVolume,
                WriteVolume,
                out volumeItem
            );
            Publishable(options, volume);

            Option reset = ModRows.Button(
                panel,
                panel.Parent,
                "SpeechFollowSpokenContent",
                ModStrings.Get(ModStrings.SpeechFollowSpokenContent),
                ResetToSpokenContent
            );
            Publishable(options, reset);

            ModRows.Publish(panel, options);
        }

        /// <summary>Mod teardown: hold no row across a reload.</summary>
        public static void Forget()
        {
            _backendItem = null;
            _voiceItem = null;
            _rateItem = null;
        }

        // ---- the choices ----

        /// <summary>The Spoken Content voice first, then the installed voices for the game's
        /// language, then a picked voice from outside it so the selection is never a row that is
        /// not offered.</summary>
        private static void VoiceChoices(out List<string> values, out List<string> labels)
        {
            values = new List<string>();
            labels = new List<string>();
            List<VoiceInfo> installed = MacSystemVoice.InstalledVoices();
            values.Add(VoiceSelection.DefaultKey);
            labels.Add(DefaultVoiceLabel(installed));

            List<VoiceInfo> offered = VoiceSelection.ForLanguage(
                installed,
                VoiceSelection.GamePrimaryLanguage(ModLocale.Language)
            );
            for (int i = 0; i < offered.Count; i++)
            {
                values.Add(VoiceSelection.MakeKey(offered[i]));
                labels.Add(VoiceLabel(offered[i]));
            }

            string stored = SpeechSettings.Voice();
            if (stored != VoiceSelection.DefaultKey && !values.Contains(stored))
            {
                VoiceInfo voice = VoiceSelection.FindByKey(installed, stored);
                if (voice != null)
                {
                    values.Add(stored);
                    labels.Add(VoiceLabel(voice));
                }
            }
        }

        private static string DefaultVoiceLabel(List<VoiceInfo> installed)
        {
            MacSystemVoice mac = Mac();
            string identifier = mac != null
                ? mac.SystemVoiceIdentifier
                : MacSpokenContent.DefaultVoiceIdentifier();
            for (int i = 0; identifier != null && i < installed.Count; i++)
            {
                if (installed[i].Identifier == identifier)
                {
                    return ModStrings.Format(
                        ModStrings.SpeechVoiceDefault,
                        VoiceLabel(installed[i])
                    );
                }
            }

            return ModStrings.Get(ModStrings.SpeechVoiceDefaultUnknown);
        }

        private static string VoiceLabel(VoiceInfo voice)
        {
            return string.IsNullOrEmpty(voice.Language)
                ? voice.Name
                : ModStrings.Format(ModStrings.SpeechVoiceLabel, voice.Name, voice.Language);
        }

        // ---- reads and writes: the file and the live backend in the same motion ----

        private static MacSystemVoice Mac()
        {
            return ModEntry.Speech == null ? null : ModEntry.Speech.Mac;
        }

        private static void SetBackend(string value)
        {
            if (value == SpeechSettings.Backend())
            {
                return;
            }

            ModSettings.File.Set(SpeechSettings.BackendKey, value);
            PrismSpeech speech = ModEntry.Speech;
            if (speech == null)
            {
                return;
            }

            speech.MacBackendPreference = value;
            speech.Reinitialize();
            SpeechSettings.Apply(speech);

            // Asking for Prism is a request that can fail - the dylib missing or quarantined -
            // and Initialize then falls back to the system voice. The stored preference is put
            // back so a Prism that cannot start is never persisted (a later launch would begin
            // from a backend that cannot speak), the row snaps back to what is actually running,
            // and a message box the mod reads says why nothing changed.
            if (
                value == PrismSpeech.MacBackendPrism
                && (speech.Mac != null || !speech.Available)
            )
            {
                ModSettings.File.Set(
                    SpeechSettings.BackendKey,
                    PrismSpeech.MacBackendSystemVoice
                );
                speech.MacBackendPreference = PrismSpeech.MacBackendSystemVoice;
                if (_backendItem != null)
                {
                    _backendItem.Refresh();
                }

                Gui.GuiService.ShowMessage(
                    ModStrings.Get(ModStrings.SpeechBackendPrismFailed),
                    MessageBoxType.INFORMATIVE,
                    Dismissed,
                    "%MessageBoxConfirmationTitle",
                    "%MessageBoxValidateTitle",
                    string.Empty
                );
            }
        }

        private static void Dismissed(object sender, MessageBoxResultEventArgs e) { }

        private static void SetVoice(string key)
        {
            if (key == SpeechSettings.Voice())
            {
                return;
            }

            if (key == VoiceSelection.DefaultKey)
            {
                ModSettings.File.Remove(SpeechSettings.VoiceKey);
            }
            else
            {
                ModSettings.File.Set(SpeechSettings.VoiceKey, key);
            }

            SpeechSettings.ApplyVoice(ModEntry.Speech);
        }

        private static float ReadRate()
        {
            int stored = SpeechSettings.RatePercent();
            if (stored >= 0)
            {
                return stored;
            }

            MacSystemVoice mac = Mac();
            if (mac != null)
            {
                return (float)Math.Round(mac.Rate01 * 100f);
            }

            // Prism is the live backend: the row still shows what the system voice would speak
            // at - the Spoken Content rate - not an invented number.
            float rate = MacSpokenContent.DefaultVoiceRate(
                MacSpokenContent.DefaultVoiceIdentifier()
            );
            return rate >= 0f ? (float)Math.Round(rate * 100f) : 50f;
        }

        private static void WriteRate(float value)
        {
            int percent = (int)Math.Round(value);
            if (percent == (int)Math.Round(ReadRate()))
            {
                // What the row already shows: either nothing changed, or the window's own
                // restore and commit passes are writing the displayed value back. In
                // follow-Spoken-Content mode the displayed value is the OS rate, and storing it
                // would silently end the following - the stored key wins from then on - so the
                // guard compares against the DISPLAYED value, not the stored key.
                return;
            }

            ModSettings.File.Set(
                SpeechSettings.RateKey,
                percent.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            SpeechSettings.ApplyRate(ModEntry.Speech);
        }

        private static float ReadVolume()
        {
            return SpeechSettings.VolumePercent();
        }

        private static void WriteVolume(float value)
        {
            int percent = (int)Math.Round(value);
            if (percent == SpeechSettings.VolumePercent())
            {
                return;
            }

            ModSettings.File.Set(
                SpeechSettings.VolumeKey,
                percent.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            SpeechSettings.ApplyVolume(ModEntry.Speech);
        }

        /// <summary>Voice and rate back to Spoken Content: their keys go, the backend stands up
        /// again (which re-reads the OS values, a change made there included), and the two rows
        /// redraw what they now hold. Cancel undoes this like any other change: the window's
        /// restore writes the opening values back through the rows' own writers.</summary>
        private static void ResetToSpokenContent()
        {
            ModSettings.File.Remove(SpeechSettings.VoiceKey);
            ModSettings.File.Remove(SpeechSettings.RateKey);
            PrismSpeech speech = ModEntry.Speech;
            if (speech != null && speech.Mac != null)
            {
                speech.Reinitialize();
                SpeechSettings.Apply(speech);
            }

            if (_voiceItem != null)
            {
                _voiceItem.Refresh();
            }

            if (_rateItem != null)
            {
                _rateItem.Refresh();
            }
        }

        private static void Publishable(List<Option> options, Option option)
        {
            if (option != null)
            {
                options.Add(option);
            }
        }
    }
}

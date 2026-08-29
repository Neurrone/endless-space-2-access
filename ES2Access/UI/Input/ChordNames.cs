using System.Collections.Generic;
using System.Text;
using ES2Access.Core.Speech;
using UnityEngine;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// A binding written out the way the player would SAY it - "Ctrl+Shift+Enter" - in the player's
    /// own language.
    ///
    /// The other half of <see cref="InputBinding.DisplayName"/>, which is English-only and exists for
    /// logs and the dev server. This one is spoken, so every part of it is a translated word: the
    /// modifiers and the joiner come out of <see cref="ModStrings"/> (a language that spells a chord
    /// "Strg und Eingabe" changes three strings, not this file), and the KEY is named by the GAME's own
    /// key-name table where it has a row for it - see <see cref="KeyName"/>.
    ///
    /// A key nothing names falls back to the engine's <c>KeyCode</c> name rather than to silence: the
    /// mod's table only has to grow when a new gesture is bound to a key the game never named, and a
    /// rebind the mod has never seen still reads out.
    ///
    /// It is addressed by (action key, binding index) rather than by a chord, because that is what a
    /// hint declares: the sentence names the ACTION, and re-binding the action re-words the sentence.
    /// The index is load-bearing - the map's off-lane move is the second binding of the SAME action
    /// as the ordinary move (<c>docs/interaction.md</c>).
    /// </summary>
    public static class ChordNames
    {
        /// <summary>The chord at <paramref name="bindingIndex"/> of the action called
        /// <paramref name="actionKey"/>, or null where the action, the binding, or the input manager
        /// itself is not there.</summary>
        public static string Of(ModInput input, string actionKey, int bindingIndex)
        {
            InputAction action = input == null ? null : input.Find(actionKey);
            IList<InputBinding> bindings = action == null ? null : action.Bindings;
            if (bindings == null || bindingIndex < 0 || bindingIndex >= bindings.Count)
            {
                return null;
            }

            InputBinding binding = bindings[bindingIndex];
            KeyboardBinding chord = binding as KeyboardBinding;
            if (chord != null)
            {
                return Of(chord);
            }

            // An EMPTY slot - a binding the player cleared - names nothing, exactly as an action
            // that was never bound does.
            return binding == null ? null : binding.DisplayName;
        }

        /// <summary>A control's name with the chord that works on it after it - "Next system
        /// (Alt+Right)" - for the few controls whose gesture is worth hearing on every landing rather
        /// than at the end of the review buffer, where the usage hints live.
        ///
        /// The connective is a template with both parts in it, never a bracket glued on here: a
        /// language that writes the chord first has somewhere to put it. An action nothing is bound to
        /// leaves the name exactly as it was.</summary>
        public static string Label(string label, string actionKey, int bindingIndex = 0)
        {
            if (string.IsNullOrEmpty(label))
            {
                return label;
            }

            string chord = Of(ModEntry.Input, actionKey, bindingIndex);
            return string.IsNullOrEmpty(chord)
                ? label
                : ModStrings.Format(ModStrings.LabelWithChord, label, chord);
        }

        /// <summary>The same for a binding already in hand.</summary>
        public static string Of(KeyboardBinding chord)
        {
            if (chord == null)
            {
                return null;
            }

            string joiner = ModStrings.Get(ModStrings.KeyChordJoiner);
            StringBuilder name = new StringBuilder();
            // The modifiers' words are declared beside the keys they name (KeyboardBinding):
            // Option and Cmd on a Mac, Ctrl and Alt everywhere else.
            if (chord.Ctrl)
            {
                Append(name, ModStrings.Get(KeyboardBinding.FirstModifierWord), joiner);
            }

            if (chord.Shift)
            {
                Append(name, ModStrings.Get(ModStrings.KeyShift), joiner);
            }

            if (chord.Alt)
            {
                Append(name, ModStrings.Get(KeyboardBinding.SecondModifierWord), joiner);
            }

            Append(name, KeyName(chord.Key), joiner);
            return name.ToString();
        }

        // The mod's own words for the keys, for a key the GAME has no word for. Everything else answers
        // with the engine's name for the key, which is a readable word for all but the punctuation keys.
        private static readonly Dictionary<KeyCode, string> Named = new Dictionary<KeyCode, string>
        {
            { KeyCode.Return, ModStrings.KeyEnter },
            { KeyCode.KeypadEnter, ModStrings.KeyEnter },
            { KeyCode.Backslash, ModStrings.KeyBackslash },
        };

        /// <summary>The game's OWN word for the key comes first: it ships a table of 120 key names in
        /// every one of its ten languages (<c>%KeyCode&lt;Name&gt;</c>, the same lookup its options
        /// screen writes a binding out with - <c>KeyCombination.LocalizeKeyCode</c>), so a player hears
        /// the arrows and the modifiers named the way the game they are playing names them, in
        /// languages nobody has translated this mod into. The mod's own table is the fallback for the
        /// keys the game's table has no row for, and the engine's <c>KeyCode</c> name the fallback for
        /// the rest.</summary>
        private static string KeyName(KeyCode key)
        {
            string game = GameName(key);
            if (!string.IsNullOrEmpty(game))
            {
                return game;
            }

            string modKey;
            return Named.TryGetValue(key, out modKey) && ModStrings.Has(modKey)
                ? ModStrings.Get(modKey)
                : key.ToString();
        }

        // A miss answers with the key itself, which is how the game tells one apart (its own localizer
        // returns the key it was given). Nothing is spoken from a raw %key.
        private static string GameName(KeyCode key)
        {
            try
            {
                string localizationKey = "%KeyCode" + key;
                string text = Gui.Localize(localizationKey);
                return string.IsNullOrEmpty(text) || text == localizationKey
                    ? null
                    : AgeText.Clean(text);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private static void Append(StringBuilder name, string part, string joiner)
        {
            if (name.Length > 0)
            {
                name.Append(joiner);
            }

            name.Append(part);
        }
    }
}

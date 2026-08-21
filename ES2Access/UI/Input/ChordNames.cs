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
    /// logs and the dev server. This one is spoken, so every part of it comes out of
    /// <see cref="ModStrings"/>: the three modifier words, the key names the mod's own gestures use,
    /// and the joiner between them (a language that spells a chord "Strg und Eingabe" changes three
    /// strings, not this file).
    ///
    /// A key the table does not name falls back to the engine's <c>KeyCode</c> name rather than to
    /// silence: the table only has to grow when a new gesture is bound to a new key, and a rebind the
    /// mod has never seen still reads out.
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

            KeyboardBinding chord = bindings[bindingIndex] as KeyboardBinding;
            return chord == null ? bindings[bindingIndex].DisplayName : Of(chord);
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
            if (chord.Ctrl)
            {
                Append(name, ModStrings.Get(ModStrings.KeyCtrl), joiner);
            }

            if (chord.Shift)
            {
                Append(name, ModStrings.Get(ModStrings.KeyShift), joiner);
            }

            if (chord.Alt)
            {
                Append(name, ModStrings.Get(ModStrings.KeyAlt), joiner);
            }

            Append(name, KeyName(chord.Key), joiner);
            return name.ToString();
        }

        // What the mod's own gestures are bound to. Everything else answers with the engine's name
        // for the key, which is a readable word for all but the punctuation keys.
        private static readonly Dictionary<KeyCode, string> Named = new Dictionary<KeyCode, string>
        {
            { KeyCode.Return, ModStrings.KeyEnter },
            { KeyCode.KeypadEnter, ModStrings.KeyEnter },
            { KeyCode.Backslash, ModStrings.KeyBackslash },
        };

        private static string KeyName(KeyCode key)
        {
            string modKey;
            return Named.TryGetValue(key, out modKey) && ModStrings.Has(modKey)
                ? ModStrings.Get(modKey)
                : key.ToString();
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

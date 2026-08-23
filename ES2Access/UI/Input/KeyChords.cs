using System.Collections.Generic;
using Amplitude.Unity.Input;
using UnityEngine;
using GameInput = Amplitude.Unity.Input.Input;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// The mod's chord and the game's chord, each written as the other.
    ///
    /// They are the same idea in two shapes. The mod's <see cref="KeyboardBinding"/> is one key plus
    /// three exact modifier flags, because that is what an exact-modifier match needs. The game's
    /// <c>KeyCombination</c> is a LIST of key codes plus a modifier mask, and its own capture widget
    /// fills the list first (up to two keys) and then sorts the modifiers out of it
    /// (<c>KeyCombination(List&lt;KeyCode&gt;)</c>) - so a captured chord arrives here as one key
    /// with a mask, and a chord the player bound to a bare modifier arrives as a one-entry list with
    /// no mask at all.
    ///
    /// That last case is the only awkward one, and it is handled rather than refused: a lone
    /// modifier key becomes the mod's key AND its own flag, so pressing it really does fire the
    /// action (the mod matches modifiers by what is physically held, so Control alone must declare
    /// Ctrl). Writing it back drops the flag again, which keeps the round trip stable.
    ///
    /// The game's capture takes at most TWO keys (<c>AgeControlKeyBindingField.
    /// MaximumNumberOfKeysByCombination</c>), so a three-key chord - the mod ships a few, such as
    /// Control+Shift+Enter - reads out correctly and cannot be re-captured as itself. Anything past
    /// the first non-modifier key is dropped on the way in, which is what the game's own binding
    /// dispatch does with it too.
    /// </summary>
    public static class KeyChords
    {
        /// <summary>The game's shape of one of the mod's chords. Null answers with the game's own
        /// empty combination, never with null: the key-mapping row reads it every refresh.</summary>
        public static KeyCombination ToCombination(KeyboardBinding chord)
        {
            KeyCombination combination = new KeyCombination();
            if (chord == null)
            {
                return combination;
            }

            GameInput.KeyModifier own = ModifierOf(chord.Key);
            if (chord.Ctrl && own != GameInput.KeyModifier.Ctrl)
            {
                combination.Modifiers |= GameInput.KeyModifier.Ctrl;
            }

            if (chord.Shift && own != GameInput.KeyModifier.Shift)
            {
                combination.Modifiers |= GameInput.KeyModifier.Shift;
            }

            if (chord.Alt && own != GameInput.KeyModifier.Alt)
            {
                combination.Modifiers |= GameInput.KeyModifier.Alt;
            }

            combination.KeyCodes.Add(chord.Key);
            return combination;
        }

        /// <summary>The mod's shape of one of the game's chords, or null where the chord is empty -
        /// which is what an unbound slot is.</summary>
        public static KeyboardBinding FromCombination(KeyCombination combination)
        {
            if (combination == null || combination.KeyCodes == null)
            {
                return null;
            }

            List<KeyCode> codes = combination.KeyCodes;
            bool ctrl = (combination.Modifiers & GameInput.KeyModifier.Ctrl) != 0;
            bool shift = (combination.Modifiers & GameInput.KeyModifier.Shift) != 0;
            bool alt = (combination.Modifiers & GameInput.KeyModifier.Alt) != 0;

            for (int i = 0; i < codes.Count; i++)
            {
                if (ModifierOf(codes[i]) == GameInput.KeyModifier.None)
                {
                    return new KeyboardBinding(codes[i], ctrl, shift, alt);
                }
            }

            // Nothing but modifier keys: the player bound the action to a modifier on its own.
            for (int i = 0; i < codes.Count; i++)
            {
                GameInput.KeyModifier own = ModifierOf(codes[i]);
                if (own == GameInput.KeyModifier.None)
                {
                    continue;
                }

                return new KeyboardBinding(
                    codes[i],
                    ctrl || own == GameInput.KeyModifier.Ctrl,
                    shift || own == GameInput.KeyModifier.Shift,
                    alt || own == GameInput.KeyModifier.Alt
                );
            }

            return null;
        }

        /// <summary>Which modifier a key code IS, or none where it is an ordinary key.</summary>
        private static GameInput.KeyModifier ModifierOf(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return GameInput.KeyModifier.Ctrl;
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return GameInput.KeyModifier.Shift;
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return GameInput.KeyModifier.Alt;
                default:
                    return GameInput.KeyModifier.None;
            }
        }
    }
}

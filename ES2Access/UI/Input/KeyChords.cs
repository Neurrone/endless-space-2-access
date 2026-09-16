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
    ///
    /// On macOS the mod's two chord modifiers are Option and Command (<see cref="KeyboardBinding"/>),
    /// and the game's mask has no Command bit. So there the mod's first modifier is written as the
    /// game's Alt, and the second travels as the Command key code in the combination's key list,
    /// which is what the game's capture widget produces for a Command chord anyway (it sorts only
    /// Control, Shift and Alt out of the list). A Control mask captured on a Mac names a modifier
    /// no mod chord can hold, and is dropped.
    /// </summary>
    public static class KeyChords
    {
        /// <summary>The mod's modifiers, by position: the first is Control (Windows) or Option
        /// (macOS), the second Alt (Windows) or Command (macOS) - declared once, in
        /// <see cref="KeyboardBinding"/>'s key codes, from which everything here derives.</summary>
        private enum ChordModifier
        {
            None,
            First,
            Shift,
            Second,
        }

        /// <summary>The game's mask bit for a physical modifier key, or None for one its mask
        /// cannot spell - there is no Command bit, which is why the second modifier travels as a
        /// key code in the list on a Mac.</summary>
        private static GameInput.KeyModifier MaskOf(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return GameInput.KeyModifier.Ctrl;
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return GameInput.KeyModifier.Alt;
                default:
                    return GameInput.KeyModifier.None;
            }
        }

        private static readonly GameInput.KeyModifier FirstMask = MaskOf(
            KeyboardBinding.FirstModifierLeft
        );

        private static readonly GameInput.KeyModifier SecondMask = MaskOf(
            KeyboardBinding.SecondModifierLeft
        );

        /// <summary>The game's shape of one of the mod's chords. Null answers with the game's own
        /// empty combination, never with null: the key-mapping row reads it every refresh.</summary>
        public static KeyCombination ToCombination(KeyboardBinding chord)
        {
            KeyCombination combination = new KeyCombination();
            if (chord == null)
            {
                return combination;
            }

            ChordModifier own = ModifierOf(chord.Key);
            if (chord.Ctrl && own != ChordModifier.First)
            {
                combination.Modifiers |= FirstMask;
            }

            if (chord.Shift && own != ChordModifier.Shift)
            {
                combination.Modifiers |= GameInput.KeyModifier.Shift;
            }

            if (chord.Alt && own != ChordModifier.Second)
            {
                if (SecondMask != GameInput.KeyModifier.None)
                {
                    combination.Modifiers |= SecondMask;
                }
                else
                {
                    combination.KeyCodes.Add(KeyboardBinding.SecondModifierLeft);
                }
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
            bool ctrl = (combination.Modifiers & FirstMask) != 0;
            bool shift = (combination.Modifiers & GameInput.KeyModifier.Shift) != 0;
            bool alt = SecondMask != GameInput.KeyModifier.None
                && (combination.Modifiers & SecondMask) != 0;

            // Where the second modifier has no mask bit (a Mac's Command), it is a key in the
            // list, alongside the key it modifies.
            if (SecondMask == GameInput.KeyModifier.None && codes.Count > 1)
            {
                for (int i = 0; i < codes.Count; i++)
                {
                    if (ModifierOf(codes[i]) == ChordModifier.Second)
                    {
                        alt = true;
                    }
                }
            }

            for (int i = 0; i < codes.Count; i++)
            {
                if (ModifierOf(codes[i]) == ChordModifier.None)
                {
                    return new KeyboardBinding(codes[i], ctrl, shift, alt);
                }
            }

            // Nothing but modifier keys: the player bound the action to a modifier on its own.
            for (int i = 0; i < codes.Count; i++)
            {
                ChordModifier own = ModifierOf(codes[i]);
                if (own == ChordModifier.None)
                {
                    continue;
                }

                return new KeyboardBinding(
                    codes[i],
                    ctrl || own == ChordModifier.First,
                    shift || own == ChordModifier.Shift,
                    alt || own == ChordModifier.Second
                );
            }

            return null;
        }

        /// <summary>Which of the mod's modifiers a key code IS, or none where it is an ordinary key
        /// - which on a Mac includes the Control keys, since no chord of the mod holds them there.
        /// Answered from <see cref="KeyboardBinding"/>'s declared modifier keys.</summary>
        private static ChordModifier ModifierOf(KeyCode key)
        {
            if (key == KeyboardBinding.FirstModifierLeft || key == KeyboardBinding.FirstModifierRight)
            {
                return ChordModifier.First;
            }

            if (key == KeyCode.LeftShift || key == KeyCode.RightShift)
            {
                return ChordModifier.Shift;
            }

            if (key == KeyboardBinding.SecondModifierLeft || key == KeyboardBinding.SecondModifierRight)
            {
                return ChordModifier.Second;
            }

            return ChordModifier.None;
        }
    }
}

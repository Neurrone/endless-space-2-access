using System.Text;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// A key plus an exact set of modifiers, polled straight from <see cref="UnityEngine.Input"/>.
    ///
    /// The modifiers must match EXACTLY, not merely be present: a bare-A binding declares
    /// <c>Ctrl = false</c>, so it stays silent while Ctrl is held, and Ctrl+A therefore fires only
    /// the Ctrl+A binding. Without that, every chord would also trigger its unmodified twin - Alt+Up
    /// (jump region) would move the cursor up as well.
    ///
    /// Left and right variants of each modifier are one logical flag; there is no Windows-key
    /// modifier, which no binding in this mod wants.
    ///
    /// <see cref="Ctrl"/> and <see cref="Alt"/> name the mod's FIRST and SECOND chord modifiers, not
    /// two particular keys: Control and Alt on Windows, Option and Command on macOS, where
    /// Control+arrows belong to the desktop (Spaces, Mission Control) and Control+Option is
    /// VoiceOver's own modifier. A binding declared <c>ctrl: true</c> is therefore Option+key on a
    /// Mac, and every chord keeps its letter on both. The declaration block below is the one
    /// place the choice is made - the physical keys each modifier stands for and both spellings
    /// of its name - and the polling here, <see cref="KeyChords"/> and <see cref="ChordNames"/>
    /// all read it.
    /// </summary>
    public sealed class KeyboardBinding : InputBinding
    {
        public readonly KeyCode Key;
        public readonly bool Ctrl;
        public readonly bool Shift;
        public readonly bool Alt;

        public KeyboardBinding(KeyCode key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        /// <summary>Whether the game runs on macOS, where the chord modifiers are Option and Command.</summary>
        internal static readonly bool Mac = Platform.IsMacOS;

        // THE ONE DECLARATION of what the two chord modifiers ARE on this platform. Everything
        // else derives from these four key codes and two word pairs: the polling below, the
        // game-form translation (KeyChords reads the key codes), and the names - English for
        // DisplayName's log form, ModStrings keys for the spoken form (ChordNames).
        internal static readonly KeyCode FirstModifierLeft = Mac
            ? KeyCode.LeftAlt
            : KeyCode.LeftControl;
        internal static readonly KeyCode FirstModifierRight = Mac
            ? KeyCode.RightAlt
            : KeyCode.RightControl;
        internal static readonly KeyCode SecondModifierLeft = Mac
            ? KeyCode.LeftCommand
            : KeyCode.LeftAlt;
        internal static readonly KeyCode SecondModifierRight = Mac
            ? KeyCode.RightCommand
            : KeyCode.RightAlt;
        internal static readonly string FirstModifierEnglish = Mac ? "Option" : "Ctrl";
        internal static readonly string SecondModifierEnglish = Mac ? "Cmd" : "Alt";
        internal static readonly string FirstModifierWord = Mac
            ? ModStrings.KeyOption
            : ModStrings.KeyCtrl;
        internal static readonly string SecondModifierWord = Mac
            ? ModStrings.KeyCmd
            : ModStrings.KeyAlt;

        public override string DisplayName
        {
            get
            {
                StringBuilder name = new StringBuilder();
                if (Ctrl)
                {
                    name.Append(FirstModifierEnglish).Append('+');
                }

                if (Shift)
                {
                    name.Append("Shift+");
                }

                if (Alt)
                {
                    name.Append(SecondModifierEnglish).Append('+');
                }

                return name.Append(Key).ToString();
            }
        }

        public override bool JustPressed()
        {
            return ModifiersMatch() && UnityEngine.Input.GetKeyDown(Key);
        }

        public override bool Held()
        {
            return ModifiersMatch() && UnityEngine.Input.GetKey(Key);
        }

        /// <summary>
        /// Whether the player is physically holding any of the three modifiers.
        ///
        /// For a conditional claim on a key the GAME also polls WITHOUT looking at modifiers - the
        /// galaxy camera's own zoom matcher reads its combination's key codes and ignores its
        /// modifiers entirely (<c>GalaxyViewCameraController.IsInputKeyCombinationPressed</c>), so a
        /// chord on that key hits it as squarely as the bare key does. A claim can only be told apart
        /// from the bare press by the physical modifier, because the combination the game is asking
        /// about carries the BINDING's modifiers and not the player's
        /// (<see cref="GameKeyStandDown.Claimed"/>).
        /// </summary>
        public static bool AnyModifierHeld
        {
            get { return CtrlHeld || ShiftHeld || AltHeld; }
        }

        private bool ModifiersMatch()
        {
            return Ctrl == CtrlHeld && Shift == ShiftHeld && Alt == AltHeld;
        }

        private static bool CtrlHeld
        {
            get
            {
                return UnityEngine.Input.GetKey(FirstModifierLeft)
                    || UnityEngine.Input.GetKey(FirstModifierRight);
            }
        }

        private static bool ShiftHeld
        {
            get
            {
                return UnityEngine.Input.GetKey(KeyCode.LeftShift)
                    || UnityEngine.Input.GetKey(KeyCode.RightShift);
            }
        }

        private static bool AltHeld
        {
            get
            {
                return UnityEngine.Input.GetKey(SecondModifierLeft)
                    || UnityEngine.Input.GetKey(SecondModifierRight);
            }
        }
    }
}

using System.Text;
using UnityEngine;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// A key plus an exact set of modifiers, polled straight from <see cref="UnityEngine.Input"/>.
    ///
    /// The modifiers must match EXACTLY, not merely be present: a bare-A binding declares
    /// <c>Ctrl = false</c>, so it stays silent while Ctrl is held, and Ctrl+A therefore fires only
    /// the Ctrl+A binding. Without that, every chord would also trigger its unmodified twin - Ctrl+Up
    /// (jump region) would move the cursor up as well.
    ///
    /// Left and right variants of each modifier are one logical flag; there is no Windows-key
    /// modifier, which no binding in this mod wants.
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

        public override string DisplayName
        {
            get
            {
                StringBuilder name = new StringBuilder();
                if (Ctrl)
                {
                    name.Append("Ctrl+");
                }

                if (Shift)
                {
                    name.Append("Shift+");
                }

                if (Alt)
                {
                    name.Append("Alt+");
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
                return UnityEngine.Input.GetKey(KeyCode.LeftControl)
                    || UnityEngine.Input.GetKey(KeyCode.RightControl);
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
                return UnityEngine.Input.GetKey(KeyCode.LeftAlt)
                    || UnityEngine.Input.GetKey(KeyCode.RightAlt);
            }
        }
    }
}

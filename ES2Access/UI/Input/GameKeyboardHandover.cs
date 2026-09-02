using System;
using System.Reflection;
using ES2Access.Core.Util;
using HarmonyLib;
using UnityEngine;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// The other half of <see cref="GameKeyStandDown"/>: the game's GUI framework hands the control it
    /// has focused every key that went down, and it does it in LateUpdate - AFTER the mod's own tick
    /// has already acted on that key.
    ///
    /// Which is one frame too late to be harmless. The mod's Enter presses a rename button; the box
    /// opens and gives its text field the engine's focus synchronously, inside that same press; and
    /// then AgeManager delivers the still-down Return to the field, whose answer to Return is to
    /// validate and close. The box opened and shut inside one frame and posted a rename to the
    /// unchanged name - and none of it is visible in the mod's own code, because everything the mod did
    /// was correct.
    ///
    /// So a key the mod has already spent this press is not delivered to the focused control either.
    /// <see cref="ModInput.ActedOnAKeyGoingDown"/> is the same consumed-key latch the game's key scans
    /// ask about, and it cannot eat real typing: the whole input layer stands down for a key-exclusive
    /// control, so from the frame the field owns the keyboard the mod consumes nothing and latches
    /// nothing, and the latch it is holding drops the moment the player lets the key go. What is
    /// suppressed is exactly the transition frame.
    ///
    /// The guard in <see cref="Tick"/> is the mirror failure: the game hands a control the keyboard and
    /// then hides the window around it without taking the keyboard back. The control is still the
    /// engine's focused control and still key-exclusive, so the mod stands down for a field nobody can
    /// see or type into, and every key in the mod goes dead with no way back.
    ///
    /// The OTHER key this dispatch decides is the commit. Enter in a text field runs the window's
    /// validate callback, and a validate is the SCREEN's action rather than the edit's - it writes the
    /// save and closes the save screen, it posts the rename and closes the box - so a player who only
    /// meant to stop typing has performed the screen's primary button (owner-reported). While the mod
    /// owns a live edit the key is therefore taken here and the edit is ended the mod's way
    /// (<see cref="Screens.TextFieldEditor.CommitInsteadOfTheGamesValidate"/>), which leaves the
    /// surface standing. Chat is exempt: its Enter SENDS, and taking it would leave chat unusable.
    ///
    /// Escape is not decided here and cannot be: it never reaches this method at all, because the
    /// InputManager clears the focus from Update before the engine's LateUpdate dispatch runs. That
    /// question belongs to the focus setter - see <see cref="GameTextFocus"/>.
    /// </summary>
    internal static class GameKeyboardHandover
    {
        private static readonly ModPatch Patches = new ModPatch(
            "keyboardhandover",
            "the game's focused-control key dispatch"
        );

        public static void Install()
        {
            Patches.Install(
                patch =>
                {
                    foreach (MethodInfo dispatch in KeyDispatches())
                    {
                        patch.Prefix(
                            dispatch,
                            typeof(GameKeyboardHandover),
                            "SkipWhenTheModAlreadyUsedTheKey"
                        );
                    }
                }
            );
        }

        public static void Remove()
        {
            Patches.Remove();
        }

        /// <summary>
        /// Where the frame's keys reach the focused control. AgeManager sends KeyDown to whatever it
        /// has focused, and the one focused control that ACTS on a key the mod also binds is the text
        /// field: Return validates (and its base clears the focus), Delete and the arrows edit. Every
        /// other AgeControl ignores KeyDown, so this one method is the whole exposure - and patching the
        /// field rather than AgeManager's own update leaves the mouse, the drags and the wheel alone.
        ///
        /// Internal rather than private so the dev server can report whether it is still patched: a
        /// count of zero here looks like a passing test and is a rename box that flashes.
        /// </summary>
        internal static MethodInfo[] KeyDispatches()
        {
            MethodInfo keyDown = AccessTools.Method(
                typeof(AgeControlTextField),
                "KeyDown",
                Type.EmptyTypes
            );
            if (keyDown == null)
            {
                throw new MissingMethodException(
                    typeof(AgeControlTextField).FullName,
                    "KeyDown"
                );
            }

            return new[] { keyDown };
        }

        private static bool SkipWhenTheModAlreadyUsedTheKey(AgeControlTextField __instance)
        {
            try
            {
                ModInput input = ModEntry.Input;
                if (input != null && input.ActedOnAKeyGoingDown())
                {
                    return false;
                }

                // THE COMMIT KEY. Asked after the latch above, so the Return that OPENED this box -
                // which the mod spent on the activation - can never be read as the Return that ends
                // the edit. What the game would do with it is its VALIDATE, which is the SCREEN's
                // action (the save-name box writes a save and closes the whole screen); the edit ends
                // here instead and the surface is left standing.
                //
                // The whole of the engine's own condition, not just its two key codes
                // (<c>AgeControlTextField.KeyDown</c>,
                // <c>decompiled/Assembly-CSharp-firstpass/AgeControlTextField.cs:78</c>): a field the
                // prefab wired no validate callback onto does NOT validate on Return - the engine
                // falls through to its key-down callback or to the base handler - so intercepting the
                // key there was the mod standing in front of a door the game does not have.
                if (Validates(__instance))
                {
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                // Runs inside the engine's own dispatch: let the key through rather than throw into
                // it, and say so once instead of once per press.
                Patches.Report(
                    "deciding whether the mod had already used a key threw, leaving it to the game",
                    e
                );
                return true;
            }
        }

        /// <summary>Whether this press is the one the engine would VALIDATE on, and whether the mod's
        /// own commit should stand in for it. The engine's condition verbatim
        /// (<c>decompiled/Assembly-CSharp-firstpass/AgeControlTextField.cs:78</c>) - the two Return
        /// keys, a validate callback switched on, an object to send it to, and a method name to send -
        /// and then the mod's own question about this field.</summary>
        private static bool Validates(AgeControlTextField field)
        {
            return (
                    UnityEngine.Input.GetKeyDown(KeyCode.Return)
                    || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)
                )
                && field.UseValidateCallback
                && field.OnValidateObject != null
                && !string.IsNullOrEmpty(field.OnValidateMethod)
                && Screens.TextFieldEditor.CommitInsteadOfTheGamesValidate(field);
        }

        /// <summary>
        /// Take the keyboard back from a control the player can no longer see. Called from the pump
        /// before the input layer polls, so the layer is awake again on the same frame the window went.
        ///
        /// Only ever for a control the game is holding EXCLUSIVELY - that is the flag the mod stands
        /// down for - and only when the chain above it says it is not on screen, which is the same
        /// effective-visibility test every readout is built from. A visible field the player is really
        /// typing into is untouched.
        /// </summary>
        public static void Tick()
        {
            try
            {
                AgeManager age = AgeManager.Instance;
                AgeControl focused = age == null ? null : age.FocusedControl;
                if (focused == null || !focused.IsKeyExclusive)
                {
                    return;
                }

                // Input-focus recovery, not node existence: whether to take the keyboard back from a
                // control the game is no longer drawing.
                if (AgeWidgets.Visible(focused.AgeTransform))
                {
                    return;
                }

                // The mod is taking the keyboard back, not the player: an edit that was live on this
                // control ends without a word and without putting anything back, because nothing the
                // player did ended it.
                Screens.TextFieldEditor.Abandon();
                age.FocusedControl = null;
                Log.Info(
                    "the keyboard was left with a control that is no longer drawn ("
                        + focused.name
                        + "); took it back"
                );
            }
            catch (Exception e)
            {
                Patches.Report("checking who holds the keyboard threw", e);
            }
        }
    }
}

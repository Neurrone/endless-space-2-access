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
    /// Telling the ways OUT of an edit apart is not done here and cannot be: Escape never reaches this
    /// method at all, because the InputManager clears the focus from Update before the engine's
    /// LateUpdate dispatch runs. That question belongs to the focus setter - see
    /// <see cref="GameTextFocus"/>.
    /// </summary>
    internal static class GameKeyboardHandover
    {
        private static Harmony _harmony;
        private static bool _reportedFailure;

        public static void Install()
        {
            Remove();

            // A unique id per load, for the reason GameKeyStandDown documents: a fixed id lets the
            // unpatch of the assembly a reload replaced strip this load's patches.
            Harmony harmony = new Harmony(
                "endless.space2.access.keyboardhandover." + Guid.NewGuid().ToString("N")
            );

            try
            {
                HarmonyMethod prefix = new HarmonyMethod(
                    typeof(GameKeyboardHandover).GetMethod(
                        "SkipWhenTheModAlreadyUsedTheKey",
                        BindingFlags.Static | BindingFlags.NonPublic
                    )
                );

                foreach (MethodInfo dispatch in KeyDispatches())
                {
                    harmony.Patch(dispatch, prefix);
                }

                _harmony = harmony;
            }
            catch (Exception e)
            {
                // Unpatched, the mod's Enter goes on committing the box it opened. Worth saying loudly
                // and not worth refusing to start over.
                Log.Error("the game's focused-control key dispatch could not be patched: " + e);
                try
                {
                    harmony.UnpatchSelf();
                }
                catch (Exception undo)
                {
                    Log.Warn("and the partial patch could not be undone: " + undo.Message);
                }
            }
        }

        public static void Remove()
        {
            Harmony harmony = _harmony;
            _harmony = null;
            _reportedFailure = false;
            if (harmony == null)
            {
                return;
            }

            try
            {
                harmony.UnpatchSelf();
            }
            catch (Exception e)
            {
                Log.Error(
                    "the game's focused-control key dispatch could not be unpatched: " + e
                );
            }
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

                return true;
            }
            catch (Exception e)
            {
                // Runs inside the engine's own dispatch: let the key through rather than throw into
                // it, and say so once instead of once per press.
                if (!_reportedFailure)
                {
                    _reportedFailure = true;
                    Log.Warn(
                        "deciding whether the mod had already used a key threw, leaving it to the "
                            + "game: " + e
                    );
                }

                return true;
            }
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
                if (!_reportedFailure)
                {
                    _reportedFailure = true;
                    Log.Warn("checking who holds the keyboard threw: " + e);
                }
            }
        }
    }
}

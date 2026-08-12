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
    /// The third thing this seam is good for is telling the two ways a field lets go of the keyboard
    /// apart. The engine unfocuses a field on Return (its validate) and on Escape
    /// (<c>InputManager.HandleInput</c> :1212-1227, for a control whose <c>StandardCancel</c> is set),
    /// and a watcher downstream sees the same thing either way - a field that was holding the keyboard
    /// and is not any more. Whether the box around it should be finished or fixed depends on which key
    /// did it, so <see cref="TookTheValidateKey"/> reports the Return the engine is about to turn into
    /// a validate, which is the one moment where the two are distinguishable.
    /// </summary>
    internal static class GameKeyboardHandover
    {
        private static Harmony _harmony;
        private static bool _reportedFailure;

        // The field the engine is delivering a validate key to, and the frame it was delivered on. One
        // field, not a set: only the focused control is ever sent KeyDown.
        //
        // Frame-stamped rather than held until someone asks, because the asker is a screen that may
        // have gone by then - a Return that COMMITTED closes the box, and nothing is left to consume
        // the record of it. A stale one would then answer for the next Return the box ever sees.
        private static AgeControl _handedTheValidateKey;
        private static int _handedTheValidateKeyOnFrame;

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
            _handedTheValidateKey = null;
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

                if (
                    __instance != null
                    && __instance.UseValidateCallback
                    && __instance.OnValidateObject != null
                    && (
                        UnityEngine.Input.GetKeyDown(KeyCode.Return)
                        || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)
                    )
                )
                {
                    // Stamped before the engine acts, because acting is what clears the focus this
                    // records the reason for.
                    _handedTheValidateKey = __instance;
                    _handedTheValidateKeyOnFrame = Time.frameCount;
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
        /// Whether <paramref name="field"/> was just handed the key the engine turns into a validate -
        /// which is to say: the focus it has since lost was lost to Return, not to Escape.
        ///
        /// "Just" is a frame's grace: the engine delivers the key from LateUpdate and the pump asks on
        /// the next frame's Update, so one frame is the ordinary answer and two is the margin. A Return
        /// the mod already spent never reaches here at all - the prefix above turns back first - so this
        /// cannot be tripped by the press that OPENED the box.
        /// </summary>
        public static bool TookTheValidateKey(AgeControl field)
        {
            if (field == null || !ReferenceEquals(_handedTheValidateKey, field))
            {
                return false;
            }

            _handedTheValidateKey = null;
            return Time.frameCount - _handedTheValidateKeyOnFrame <= 2;
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

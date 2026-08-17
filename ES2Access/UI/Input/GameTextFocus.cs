using System;
using System.Reflection;
using ES2Access.Core.Util;
using ES2Access.Screens;
using HarmonyLib;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// The one place every way out of a text box passes through.
    ///
    /// A field can lose the engine's keyboard four ways and only one of them reaches the field itself:
    /// Return goes through <c>AgeControlTextField.KeyDown</c> (which is where
    /// <see cref="GameKeyboardHandover"/> sits), but ESCAPE never does - <c>InputManager.HandleInput</c>
    /// :1210-1243 clears the focus from Update, before the engine's LateUpdate dispatch runs at all -
    /// and neither does a right click or a click on something else. What they share is the setter:
    /// <c>AgeManager.FocusedControl</c> (:277-301), a plain non-virtual property that every one of them
    /// assigns.
    ///
    /// So the prefix here is the mod's only chance to tell a commit from a cancel, and - just as
    /// importantly - the last moment before the game's own <c>FocusLoss</c> handlers run. That ordering
    /// is what makes a cancel possible at all: the marketplace clamps and COMMITS whatever it finds in
    /// its quantity box from its lose-focus handler, so the pre-edit text has to be back in the box
    /// before this call returns, not a frame later. <see cref="TextFieldEditor.FocusLeaving"/> does
    /// both and speaks nothing; the words come from the pump on the next frame.
    ///
    /// It answers only about an edit the MOD started. Every other focus change in the game - the mod
    /// taking the keyboard back off a window that is no longer drawn, a panel focusing its own widget,
    /// the box the game itself opened on a rename - passes through untouched.
    /// </summary>
    internal static class GameTextFocus
    {
        private static Harmony _harmony;
        private static bool _reportedFailure;

        public static void Install()
        {
            Remove();

            // A unique id per load, for the reason GameKeyStandDown documents: a fixed id lets the
            // unpatch of the assembly a reload replaced strip this load's patches.
            Harmony harmony = new Harmony(
                "endless.space2.access.textfocus." + Guid.NewGuid().ToString("N")
            );

            try
            {
                HarmonyMethod prefix = new HarmonyMethod(
                    typeof(GameTextFocus).GetMethod(
                        "NoticeAnEditEnding",
                        BindingFlags.Static | BindingFlags.NonPublic
                    )
                );

                foreach (MethodInfo setter in FocusSetters())
                {
                    harmony.Patch(setter, prefix);
                }

                _harmony = harmony;
            }
            catch (Exception e)
            {
                // Unpatched, an edit ends silently and a cancel keeps whatever was typed - worth
                // saying loudly and not worth refusing to start over.
                Log.Error("the game's keyboard-focus setter could not be patched: " + e);
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
                Log.Error("the game's keyboard-focus setter could not be unpatched: " + e);
            }
        }

        /// <summary>Internal rather than private so the dev server can report whether it is still
        /// patched: a count of zero here looks like a passing test and is every Escape out of a text
        /// box keeping what the player asked to throw away.</summary>
        internal static MethodInfo[] FocusSetters()
        {
            MethodInfo setter = AccessTools.PropertySetter(
                typeof(AgeManager),
                "FocusedControl"
            );
            if (setter == null)
            {
                throw new MissingMethodException(typeof(AgeManager).FullName, "set_FocusedControl");
            }

            return new[] { setter };
        }

        private static void NoticeAnEditEnding(AgeManager __instance, AgeControl value)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                TextFieldEditor.FocusLeaving(__instance.FocusedControl, value);
            }
            catch (Exception e)
            {
                // Runs inside the engine's own focus handling: never throw into it, and say so once
                // instead of once per focus change.
                if (!_reportedFailure)
                {
                    _reportedFailure = true;
                    Log.Warn("deciding how an edit ended threw: " + e);
                }
            }
        }
    }
}

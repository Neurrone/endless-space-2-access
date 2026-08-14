using System;
using System.Reflection;
using ES2Access.Core.Util;
using HarmonyLib;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// Turns the first Escape out of the game's chat box into a step OUT of the box instead of the
    /// game's close-the-whole-panel.
    ///
    /// The chat field is key-exclusive, so while the player is typing the mod's whole layer stands down
    /// and the only key that can act is one the GAME routes. Escape is one: the field declares
    /// <c>StandardCancel = false</c>, so <c>InputManager.HandleInput</c> :1228-1239 hands Exit down its
    /// handler chain rather than just dropping the focus, and <c>InGameChatPanel.HandleInput</c>
    /// :108-112 answers it with <c>SetDiscreet(true)</c> - tabs, log and box off the screen in one
    /// press. Everything <see cref="Screens.ChatScreen"/> declares then exists for no frame the player
    /// could reach it in. So the panel's answer is replaced by <see cref="ChatHold.StepOutOfTyping"/>:
    /// keyboard back, panel held open, cursor on the box's node, and a second Escape from there closes
    /// (owner-reported bug, 2026-08-14).
    ///
    /// The panel's own handler is the interception point rather than
    /// <see cref="GameKeyboardHandover"/>'s seam on <c>AgeControlTextField.KeyDown</c>, and it has to
    /// be: the field's <c>OnTextFieldKeyDownCb</c> (<c>ChatPanel.cs</c> :232-246) has an Escape route
    /// of its own but the engine only delivers KeyDown in LateUpdate, by which time the InputManager's
    /// Update-time dispatch has already closed the panel and dropped the focus that route needed.
    /// Whichever key the player has bound to Exit is covered, because the action is what is matched
    /// here, not the key.
    ///
    /// Only while a screen of the mod's is focused. With none, there is no cursor to step out ONTO, and
    /// Escape has to keep meaning what it means to a player who is not using the mod.
    /// </summary>
    internal static class ChatEscape
    {
        private static Harmony _harmony;
        private static bool _reportedFailure;

        public static void Install()
        {
            Remove();

            // A unique id per load, for the reason GameKeyStandDown documents: a fixed id lets the
            // unpatch of the assembly a reload replaced strip this load's patches.
            Harmony harmony = new Harmony(
                "endless.space2.access.chatescape." + Guid.NewGuid().ToString("N")
            );

            try
            {
                HarmonyMethod prefix = new HarmonyMethod(
                    typeof(ChatEscape).GetMethod(
                        "StepOutInsteadOfClosing",
                        BindingFlags.Static | BindingFlags.NonPublic
                    )
                );

                foreach (MethodInfo handler in Handlers())
                {
                    harmony.Patch(handler, prefix);
                }

                _harmony = harmony;
            }
            catch (Exception e)
            {
                // Unpatched, Escape closes chat as the game always did - the chat page is then only
                // up for as long as the player keeps typing, which is worth saying loudly and not
                // worth refusing to start over.
                Log.Error("the chat panel's input handler could not be patched: " + e);
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
                Log.Error("the chat panel's input handler could not be unpatched: " + e);
            }
        }

        /// <summary>Where the game answers an input action for the in-game chat panel. Internal rather
        /// than private so the dev server can report whether it is still patched: a count of zero here
        /// looks like a passing test and is a chat panel that shuts on the first Escape.</summary>
        internal static MethodInfo[] Handlers()
        {
            MethodInfo handleInput = AccessTools.Method(
                typeof(InGameChatPanel),
                "HandleInput",
                new[] { typeof(Amplitude.StaticString) }
            );
            if (handleInput == null)
            {
                throw new MissingMethodException(typeof(InGameChatPanel).FullName, "HandleInput");
            }

            return new[] { handleInput };
        }

        private static bool StepOutInsteadOfClosing(
            InGameChatPanel __instance,
            Amplitude.StaticString inputAction,
            ref bool __result
        )
        {
            try
            {
                if (inputAction != global::InputAction.Exit || __instance == null)
                {
                    return true;
                }

                AgeManager age = AgeManager.Instance;
                if (age == null || !ReferenceEquals(age.FocusedControl, __instance.ChatTextField))
                {
                    // Not the player typing: this is the game closing a panel a mouse left open, and
                    // it is the game's own business.
                    return true;
                }

                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator == null || navigator.Screen == null)
                {
                    return true;
                }

                ChatHold.StepOutOfTyping(__instance);
                __result = true;
                return false;
            }
            catch (Exception e)
            {
                // Runs inside the game's own input dispatch: let the panel answer rather than throw
                // into it, and say so once instead of once per press.
                if (!_reportedFailure)
                {
                    _reportedFailure = true;
                    Log.Warn(
                        "deciding what Escape means in the chat box threw, leaving it to the game: "
                            + e
                    );
                }

                return true;
            }
        }
    }
}

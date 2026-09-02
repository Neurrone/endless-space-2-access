using System;
using System.Reflection;
using HarmonyLib;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// The mod's two interceptions in the chat panel's own input dispatch: the CHAT KEY on a closed
    /// panel opens the chat page with the cursor ON the box rather than handing the box the keyboard
    /// in the same press (owner ruling 2026-08-14 - typing is Enter on the box's node), and the first
    /// Escape out of the box becomes a step OUT instead of the game's close-the-whole-panel.
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
        private static readonly ModPatch Patches = new ModPatch(
            "chatescape",
            "the chat panel's input handler"
        );

        public static void Install()
        {
            Patches.Install(
                patch =>
                {
                    foreach (MethodInfo handler in Handlers())
                    {
                        patch.Prefix(handler, typeof(ChatEscape), "StepOutInsteadOfClosing");
                    }
                }
            );
        }

        public static void Remove()
        {
            Patches.Remove();
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
                if (__instance == null)
                {
                    return true;
                }

                if (inputAction == global::InputAction.StartChatting)
                {
                    // The chat key, on a closed panel: open the page with the cursor ON the box
                    // rather than letting the game hand the box the keyboard in the same press
                    // (owner ruling 2026-08-14 - typing is Enter on the box, like every other text
                    // box here). An open panel's chat key stays the game's, as does the whole key
                    // when the mod has no page to open over.
                    if (ChatHold.IsOpen(__instance))
                    {
                        return true;
                    }

                    GraphNavigator nav = ModEntry.Navigator;
                    if (nav == null || nav.Screen == null || !ChatHold.OpenOnTheBox(__instance))
                    {
                        return true;
                    }

                    __result = true;
                    return false;
                }

                if (inputAction != global::InputAction.Exit)
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
                Patches.Report(
                    "deciding what Escape means in the chat box threw, leaving it to the game",
                    e
                );
                return true;
            }
        }
    }
}

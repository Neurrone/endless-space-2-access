using System;
using System.Reflection;
using ES2Access.Core.Util;
using HarmonyLib;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// Makes the game stand down from the keys the mod is using.
    ///
    /// The mod polls UnityEngine.Input itself (see <see cref="ModInput"/>), and so does the game -
    /// in parallel, from its own scans, with no notion of a key having been handled already. Every
    /// key the mod acts on therefore also does whatever the game has bound to it: KeypadEnter ends the
    /// turn under a cursor that was only trying to press a button; Enter on a message box answers it
    /// twice, once through the game's Validate and once through ours; an arrow moves the cursor and
    /// pans the galaxy camera with it. Tab was the worst of them - it raised StartChatting and the chat
    /// panel took the engine's keyboard focus, leaving the mod deaf mid-sentence - until the chat key
    /// was moved off it (<see cref="GameChatKey"/>).
    ///
    /// So while the mod's layer is live, the game's scans are told that a key the mod claims is not
    /// pressed - <see cref="ModInput.ClaimsKey"/> decides, Escape excepted, because screens delegate
    /// Escape to the game on purpose. When the layer is not live (no screen of ours, or the player
    /// is typing into a game text field) nothing is claimed and the game sees every key as it
    /// always did.
    ///
    /// Two scans, because the game has two independent ways of reading the keyboard:
    ///
    ///   Amplitude.Unity.Input.InputManager.InputsMatch(KeyCombination) - firstpass, private,
    ///     called from that class's own Update for every binding while anyKeyDown. It is the single
    ///     gate every discrete hotkey passes: Validate, Exit, EndTurn, StartChatting, the F-key
    ///     screens, and the generic Up/Down that NotificationWindow.HandleInput reads as
    ///     next/previous notification. Blocking a match there means the action is never raised, so
    ///     no window ever hears about it.
    ///
    ///   GalaxyViewCameraController.IsInputKeyCombinationPressed / IsInputKeyCombinationDown -
    ///     Assembly-CSharp, private, its own copy of the matcher, polling GetKey every frame from
    ///     CheckInputs. Held-key camera panning never goes through the InputManager at all (the
    ///     controller's own HandleInput is a stub), and it ignores modifiers, so Shift+Left and
    ///     Ctrl+Left pan the map as readily as Left does. The zoom bindings run through the same
    ///     two helpers on PageUp/PageDown, and the mod's scanner chords sit on those same two keys -
    ///     so the scanner's claim is conditional on a modifier being PHYSICALLY held
    ///     (<c>GalaxyScanner.KeysClaimed</c>), which is what leaves the bare press zooming. Handing
    ///     the bare chord back with <see cref="ModInput.LeaveToGame"/> would NOT have worked here:
    ///     the modifiers below are read off the combination the game is asking about, and this
    ///     camera's bindings declare none whether or not the player is holding one.
    ///
    /// Both take a KeyCombination and answer a bool, so one prefix serves all three: claim any key
    /// in the combination and the answer becomes "not pressed".
    ///
    /// The encounter cameras keep their own copies of the same helpers. They are left alone until
    /// the mod has a screen on those views - with no screen of ours focused the layer is not live
    /// there and nothing would be claimed anyway.
    ///
    /// Standing the game down from a key can take away the only route to one of its surfaces, which is
    /// why the chat key is MOVED rather than only suppressed (<see cref="GameChatKey"/>) and why a
    /// chord can be handed back while its bare key is kept (<see cref="ModInput.LeaveToGame"/>).
    /// </summary>
    internal static class GameKeyStandDown
    {
        private static Harmony _harmony;
        private static bool _reportedFailure;

        public static void Install()
        {
            Remove();

            // A unique id per load. Harmony identifies a patch's owner by id alone, so a stale
            // UnpatchSelf from the assembly a reload replaced would otherwise strip the patches
            // this load has just applied.
            Harmony harmony = new Harmony(
                "endless.space2.access.gamekeys." + Guid.NewGuid().ToString("N")
            );

            try
            {
                HarmonyMethod prefix = new HarmonyMethod(
                    typeof(GameKeyStandDown).GetMethod(
                        "NotPressedWhenModClaimsIt",
                        BindingFlags.Static | BindingFlags.NonPublic
                    )
                );

                foreach (MethodInfo scan in KeyScans())
                {
                    harmony.Patch(scan, prefix);
                }

                _harmony = harmony;
            }
            catch (Exception e)
            {
                // A failed patch leaves the mod's keys doing double duty, which is worth a loud
                // complaint but not worth refusing to start over.
                Log.Error("the game's key scans could not be patched: " + e);
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
                Log.Error("the game's key scans could not be unpatched: " + e);
            }
        }

        /// <summary>The game's three key-matching routines, resolved by signature so a missing one
        /// fails here with a name rather than silently not being patched. Internal rather than private
        /// so the dev server can report whether they are still patched.</summary>
        internal static MethodInfo[] KeyScans()
        {
            Type[] combination = new[] { typeof(Amplitude.Unity.Input.KeyCombination) };
            return new[]
            {
                Scan(typeof(Amplitude.Unity.Input.InputManager), "InputsMatch", combination),
                Scan(
                    typeof(GalaxyViewCameraController),
                    "IsInputKeyCombinationPressed",
                    combination
                ),
                Scan(typeof(GalaxyViewCameraController), "IsInputKeyCombinationDown", combination),
            };
        }

        private static MethodInfo Scan(Type owner, string name, Type[] parameters)
        {
            MethodInfo scan = AccessTools.Method(owner, name, parameters);
            if (scan == null)
            {
                throw new MissingMethodException(owner.FullName, name);
            }

            return scan;
        }

        private static bool NotPressedWhenModClaimsIt(
            Amplitude.Unity.Input.KeyCombination keyCombination,
            ref bool __result
        )
        {
            if (!Claimed(keyCombination))
            {
                return true;
            }

            __result = false;
            return false;
        }

        /// <summary>
        /// Whether the game is to be told this combination is not pressed. Internal rather than private
        /// so the dev server can ask it without a keyboard (<c>DevProbe.Chord</c>) - the alternative is
        /// holding a chord down while an HTTP request arrives.
        ///
        /// The modifiers are read from the combination the game is asking about, which is the only
        /// place they exist: <see cref="ModInput.ClaimsKey"/> is asked per key. That is what lets a
        /// chord be handed back while its bare key stays the mod's - Ctrl+Tab reaches the game's chat
        /// panel while Tab still moves the cursor (<see cref="ModInput.LeaveToGame"/>).
        /// </summary>
        internal static bool Claimed(Amplitude.Unity.Input.KeyCombination keyCombination)
        {
            try
            {
                ModInput input = ModEntry.Input;
                if (input == null || keyCombination == null || keyCombination.KeyCodes == null)
                {
                    return false;
                }

                Amplitude.Unity.Input.Input.KeyModifier modifiers = keyCombination.Modifiers;
                bool ctrl =
                    (modifiers & Amplitude.Unity.Input.Input.KeyModifier.Ctrl)
                    != Amplitude.Unity.Input.Input.KeyModifier.None;
                bool shift =
                    (modifiers & Amplitude.Unity.Input.Input.KeyModifier.Shift)
                    != Amplitude.Unity.Input.Input.KeyModifier.None;
                bool alt =
                    (modifiers & Amplitude.Unity.Input.Input.KeyModifier.Alt)
                    != Amplitude.Unity.Input.Input.KeyModifier.None;

                for (int i = 0; i < keyCombination.KeyCodes.Count; i++)
                {
                    UnityEngine.KeyCode key = keyCombination.KeyCodes[i];
                    if (input.LeavesToGame(key, ctrl, shift, alt))
                    {
                        continue;
                    }

                    if (input.ClaimsKey(key))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                // Runs inside the game's own scan, several times a frame: claim nothing rather than
                // throw into it, and say so once instead of once per key per frame.
                if (!_reportedFailure)
                {
                    _reportedFailure = true;
                    Log.Warn(
                        "deciding whether the mod claims a key threw, leaving it to the game: " + e
                    );
                }

                return false;
            }
        }
    }
}

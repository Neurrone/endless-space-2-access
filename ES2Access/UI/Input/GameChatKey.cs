using System;
using Amplitude.Unity.Framework;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// Moves the game's own chat key off the keys the mod needs, so that multiplayer chat is
    /// reachable at all.
    ///
    /// ES2 binds <c>StartChatting</c> to Return AND Tab (`InputManager.cs:262-278`), which are the
    /// mod's activate and next-stop keys. <see cref="GameKeyStandDown"/> takes both away from the
    /// game - it has to, or every Tab would hand the engine's keyboard to the chat field and silence
    /// the whole layer - and that leaves the chat panel with no route in at all: the only thing that
    /// opens it is that action (`InGameChatPanel.HandleInput` :88-114 -> `SetFocus` :116-125). A key
    /// the mod claims is not just a collision; it can be the ONLY way to a game surface, and then the
    /// mod owes a replacement.
    ///
    /// The replacement is the game's own binding, moved: Ctrl+Tab, which nothing in the game binds
    /// (all 65 default bindings measured live - the modified ones are Ctrl+Mouse1, Ctrl+F5, Ctrl+F8,
    /// Ctrl+F, Ctrl+E, Ctrl+H, Ctrl+Shift+R, Ctrl+Shift+F3, Ctrl+Alt+V) and which no mod binding
    /// matches either (the mod's Tab bindings are exact-modifier - `KeyboardBinding` - so plain Tab
    /// and Shift+Tab stay the mod's while Ctrl+Tab is nobody's). Going through the game's own option
    /// property rather than a private list is what makes the change visible: Options > Key mappings
    /// reads the same property back (`Option.GetValue` is not cached for a key mapping - its
    /// attribute never sets Latent), so the row shows "Ctrl + Tab" and the player can re-bind it like
    /// any other key.
    ///
    /// ONLY IF THE PLAYER HAS NOT CHOSEN THEIR OWN. The move happens when every combination bound to
    /// the action is one of the keys the mod uses unmodified (Return, KeypadEnter, Tab - which is the
    /// shipped default), and never otherwise: a player who moved chat somewhere else has already
    /// solved the collision themselves, and their choice is not ours to overwrite.
    ///
    /// The write PERSISTS. The game's setter saves the binding to the player's registry
    /// (`InputManager.SaveToRegistry` -> `Settings/Input/InputBindingsStartChatting`, flushed to
    /// Registry.xml when the game quits), so chat stays on Ctrl+Tab on a later launch WITHOUT the mod
    /// too - and Enter/Tab no longer open it there. That is the cost of using the game's own binding
    /// system instead of shadowing it, and it is recoverable from the game's own options screen.
    ///
    /// Whatever chord chat ends up on, the key layer is told to let it through
    /// (<see cref="HandBack"/>): the stand-down is asked per key and would otherwise swallow Ctrl+Tab
    /// along with Tab, and would swallow the player's own choice too if they moved chat onto a modified
    /// letter. So the hand-over follows the binding rather than naming the chord this class writes.
    ///
    /// Asked from the pump rather than once at startup, for two reasons: the input service does not
    /// exist yet when the mod starts on a cold boot, and the binding table is filled a moment AFTER
    /// the service is published (`InputManager.BindServices_IInputOptionsService`), which would let a
    /// write land and then be overwritten by the registry pass - so the check waits for a registered
    /// binding to exist. It keeps looking every few seconds afterwards, which costs one comparison a
    /// frame and covers the player pressing "reset to defaults" in the options screen: that puts
    /// Enter/Tab back, and with the mod live that means chat is unreachable again until it is moved.
    /// </summary>
    internal static class GameChatKey
    {
        /// <summary>Where chat goes. The game's own registry form, so the value round-trips through
        /// <c>InputBinding.ToRegistryString</c> unchanged and can be compared as a string.</summary>
        public const string Chord = "StartChatting:Ctrl+Tab,";

        /// <summary>How often the binding is re-checked once it has been settled once. Long enough to
        /// cost nothing, short enough that a reset in the options screen is repaired before the player
        /// has walked back into the game.</summary>
        private const float RecheckSeconds = 3f;

        /// <summary>How often to look while the game is still starting up and has no bindings to read.
        /// Short, because the check has to have happened before the player can press anything.</summary>
        private const float WaitSeconds = 0.5f;

        private static float _nextCheck;

        /// <summary>The binding the hand-over below was built from, so the work is done when it changes
        /// and not every three seconds.</summary>
        private static string _following;

        public static void Tick()
        {
            float now = Time.unscaledTime;
            if (now < _nextCheck)
            {
                return;
            }

            try
            {
                IInputOptionsService options = Services.GetService<IInputOptionsService>();
                // Spelled out in full: the mod has an InputAction of its own in this namespace.
                if (
                    options == null
                    || options.GetInputBinding(global::InputAction.StartChatting) == null
                )
                {
                    // Either the input manager has not bound its services yet or its bindings are not
                    // filled in; both are answered by asking again in a moment rather than writing a
                    // value the registry pass is about to replace.
                    _nextCheck = now + WaitSeconds;
                    return;
                }

                _nextCheck = now + RecheckSeconds;

                Amplitude.Unity.Input.InputBinding current = options.InputBindingsStartChatting;
                string was = current == null ? null : current.ToRegistryString();
                if (was == null)
                {
                    return;
                }

                if (was != Chord && OnTheModsKeys(current))
                {
                    options.InputBindingsStartChatting =
                        new Amplitude.Unity.Input.InputBinding(Chord);
                    current = options.InputBindingsStartChatting;
                    Log.Info(
                        "the game's chat key was "
                            + was
                            + ", which the mod uses to navigate; moved it to Ctrl+Tab. The game's own "
                            + "Options / Key mappings shows it there and can change it, and the change "
                            + "is saved with the game's settings"
                    );
                    was = Chord;
                }

                if (was == _following)
                {
                    return;
                }

                _following = was;
                HandBack(current, was);
            }
            catch (Exception e)
            {
                // A game that cannot be asked about its bindings is a game whose chat is unreachable,
                // which is worth saying once - not once every three seconds.
                _nextCheck = float.MaxValue;
                Log.Warn("the game's chat key could not be read or moved: " + e);
            }
        }

        /// <summary>
        /// Tell the key layer to let whatever chord chat now sits on through to the game
        /// (<see cref="ModInput.LeaveToGame"/>). This follows the BINDING rather than naming Ctrl+Tab,
        /// because the player may move chat somewhere else from the options screen - and a chord the mod
        /// swallows is a chat box that cannot be opened, whoever chose the key.
        ///
        /// Two kinds of chord are not handed over, and both are logged rather than silently dropped: a
        /// key with no modifier (that is one of the mod's own - a bare letter is its type-ahead search,
        /// Tab is its next stop) and a chord the mod itself binds. Chat is then unreachable until the
        /// player picks a different key, which is a thing to be told.
        /// </summary>
        private static void HandBack(Amplitude.Unity.Input.InputBinding binding, string registryForm)
        {
            ModInput input = ModEntry.Input;
            if (input == null)
            {
                _following = null;
                return;
            }

            input.TakeBackEveryChord();
            int handed =
                HandBack(input, binding.PrimaryKeyCombination)
                + HandBack(input, binding.SecondaryKeyCombination);
            if (handed == 0)
            {
                Log.Warn(
                    "the game's chat key is "
                        + registryForm
                        + ", which the mod needs for itself; chat cannot be opened until it is bound to "
                        + "a chord of its own in the game's Options / Key mappings"
                );
                return;
            }

            if (registryForm != Chord)
            {
                Log.Info(
                    "the game's chat key is "
                        + registryForm
                        + ", which the player chose; left alone and let through to the game"
                );
            }
        }

        private static int HandBack(ModInput input, Amplitude.Unity.Input.KeyCombination combination)
        {
            // A combination of several real keys has no counterpart in the mod's own bindings, so there
            // is nothing to compare it against and nothing it could collide with.
            if (Count(combination) != 1)
            {
                return 0;
            }

            Amplitude.Unity.Input.Input.KeyModifier modifiers = combination.Modifiers;
            if (modifiers == Amplitude.Unity.Input.Input.KeyModifier.None)
            {
                return 0;
            }

            bool ctrl =
                (modifiers & Amplitude.Unity.Input.Input.KeyModifier.Ctrl)
                != Amplitude.Unity.Input.Input.KeyModifier.None;
            bool shift =
                (modifiers & Amplitude.Unity.Input.Input.KeyModifier.Shift)
                != Amplitude.Unity.Input.Input.KeyModifier.None;
            bool alt =
                (modifiers & Amplitude.Unity.Input.Input.KeyModifier.Alt)
                != Amplitude.Unity.Input.Input.KeyModifier.None;
            KeyCode key = combination.KeyCodes[0];
            if (input.BindsChord(key, ctrl, shift, alt))
            {
                return 0;
            }

            input.LeaveToGame(key, ctrl, shift, alt);
            return 1;
        }

        /// <summary>Whether every key bound to the action is one of the keys the mod takes for itself
        /// unmodified - which is what makes the surface unreachable, and what the game ships with
        /// (Return primary, Tab secondary). An action bound to nothing counts as the player's own
        /// choice, not as a default to fix.</summary>
        private static bool OnTheModsKeys(Amplitude.Unity.Input.InputBinding binding)
        {
            int keys = Count(binding.PrimaryKeyCombination) + Count(binding.SecondaryKeyCombination);
            return keys > 0
                && IsAModKey(binding.PrimaryKeyCombination)
                && IsAModKey(binding.SecondaryKeyCombination);
        }

        private static int Count(Amplitude.Unity.Input.KeyCombination combination)
        {
            return combination == null || combination.KeyCodes == null
                ? 0
                : combination.KeyCodes.Count;
        }

        // An empty combination passes: the game's default leaves the secondary slot of most actions
        // empty, and nothing unbound can collide with anything.
        private static bool IsAModKey(Amplitude.Unity.Input.KeyCombination combination)
        {
            if (Count(combination) == 0)
            {
                return true;
            }

            if (combination.Modifiers != Amplitude.Unity.Input.Input.KeyModifier.None)
            {
                return false;
            }

            for (int i = 0; i < combination.KeyCodes.Count; i++)
            {
                KeyCode key = combination.KeyCodes[i];
                if (key != KeyCode.Return && key != KeyCode.KeypadEnter && key != KeyCode.Tab)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

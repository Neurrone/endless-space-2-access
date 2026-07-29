using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// Owns the mod's actions and polls them once per frame from the pump. Polling
    /// <see cref="UnityEngine.Input"/> directly - rather than registering as one of the game's own
    /// input handlers, which would limit us to the actions the game already names - keeps the mod's
    /// keys working on screens the game does not route input to, at the price of having to stand
    /// down politely; see <see cref="GameOwnsKeyboard"/>.
    ///
    /// An instance, not a static: a hot reload drops this object and its handlers with the rest of
    /// the mod, leaving nothing armed behind it.
    ///
    /// Not implemented yet, and deliberately: rebinding persistence (bindings are compiled in), and
    /// the category/shadowing system that lets a modal screen claim a chord away from a screen
    /// beneath it. Both belong here when a second screen layer needs them; until then every action
    /// is offered to the focused screen and nothing shadows anything.
    /// </summary>
    public sealed class ModInput
    {
        private readonly List<InputAction> _actions = new List<InputAction>();

        /// <summary>Offered every triggered action; returning true consumes it. Null means nothing is
        /// listening, so every action falls through to its own <see cref="InputAction.Performed"/>.
        /// </summary>
        public Func<InputAction, bool> Dispatch;

        public IList<InputAction> Actions
        {
            get { return _actions; }
        }

        public InputAction Register(string key)
        {
            InputAction action = new InputAction(key);
            _actions.Add(action);
            return action;
        }

        public InputAction Find(string key)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].Key == key)
                {
                    return _actions[i];
                }
            }

            return null;
        }

        /// <summary>
        /// True while a game text field holds the keyboard. The whole layer stands down then: the
        /// player is typing a save name or a multiplayer message, and every letter, arrow and
        /// Backspace belongs to that field.
        /// </summary>
        public static bool GameOwnsKeyboard()
        {
            try
            {
                AgeManager age = AgeManager.Instance;
                AgeControl focused = age == null ? null : age.FocusedControl;
                return focused != null && focused.IsKeyExclusive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Poll every action and dispatch what fired. Call once per frame, before the
        /// screens tick, so a keypress and the announcement it causes land in the same frame.</summary>
        public void Tick()
        {
            if (GameOwnsKeyboard())
            {
                Disarm();
                return;
            }

            float now = Time.unscaledTime;
            float initialDelay = OsKeyboard.InitialDelay;
            float repeatInterval = OsKeyboard.RepeatInterval;

            for (int i = 0; i < _actions.Count; i++)
            {
                InputAction action = _actions[i];
                bool held = action.Held;
                bool fire = false;

                if (action.JustPressed)
                {
                    fire = true;
                    action.NextRepeatTime = now + initialDelay;
                }
                else if (
                    action.Repeats
                    && held
                    && action.NextRepeatTime > 0f
                    && now >= action.NextRepeatTime
                )
                {
                    // Held past the delay. The next due time is computed from now rather than
                    // advanced by an interval, so a long frame catches up by at most one step
                    // instead of firing a burst. The armed check (NextRepeatTime > 0) means only a
                    // key that was actually pressed during this hold repeats - releasing Shift while
                    // holding Tab makes the bare-Tab binding start matching, and that must not count
                    // as a press.
                    fire = true;
                    action.NextRepeatTime = now + repeatInterval;
                }

                if (!held)
                {
                    action.NextRepeatTime = 0f;
                }

                if (!fire)
                {
                    continue;
                }

                Func<InputAction, bool> dispatch = Dispatch;
                if (dispatch == null || !dispatch(action))
                {
                    action.InvokePerformed();
                }
            }
        }

        // Nothing is armed while the layer is standing down, so the key the player was holding when
        // a text field took focus does not repeat the moment it hands the keyboard back.
        private void Disarm()
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                _actions[i].NextRepeatTime = 0f;
            }
        }
    }
}

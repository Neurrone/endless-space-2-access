using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ES2Access.UI.Input
{
    /// <summary>
    /// A named thing the player can do ("ui.down"), plus the bindings that trigger it. Code asks for
    /// the action by name and never mentions a key, so a rebind is a change in one place.
    ///
    /// An action fires once on press; marking it <see cref="Repeating"/> additionally makes it fire
    /// at the OS typematic rate while held, which is what makes holding Down walk a list.
    /// </summary>
    public sealed class InputAction
    {
        private readonly List<InputBinding> _bindings = new List<InputBinding>();

        public InputAction(string key)
        {
            Key = key;
        }

        /// <summary>The stable name code refers to the action by.</summary>
        public string Key { get; private set; }

        /// <summary>Fires when the action was triggered and nothing consumed it - the global-hotkey
        /// fallback for actions a focused screen did not want.</summary>
        public event Action Performed;

        /// <summary>Auto-repeats while held.</summary>
        public bool Repeats { get; private set; }

        /// <summary>When the next auto-repeat is due, or 0 while the action is not armed. Owned by
        /// <see cref="InputManager"/>.</summary>
        internal float NextRepeatTime;

        public IList<InputBinding> Bindings
        {
            get { return _bindings; }
        }

        public InputAction Bind(InputBinding binding)
        {
            if (binding != null)
            {
                _bindings.Add(binding);
            }

            return this;
        }

        public InputAction Bind(KeyCode key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            return Bind(new KeyboardBinding(key, ctrl, shift, alt));
        }

        /// <summary>Mark the action as auto-repeating while held.</summary>
        public InputAction Repeating()
        {
            Repeats = true;
            return this;
        }

        public bool JustPressed
        {
            get
            {
                for (int i = 0; i < _bindings.Count; i++)
                {
                    if (_bindings[i].JustPressed())
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool Held
        {
            get
            {
                for (int i = 0; i < _bindings.Count; i++)
                {
                    if (_bindings[i].Held())
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>The bindings as the player would read them ("Enter, KeypadEnter").</summary>
        public string BindingsDisplay
        {
            get
            {
                StringBuilder display = new StringBuilder();
                for (int i = 0; i < _bindings.Count; i++)
                {
                    if (display.Length > 0)
                    {
                        display.Append(", ");
                    }

                    display.Append(_bindings[i].DisplayName);
                }

                return display.ToString();
            }
        }

        internal void InvokePerformed()
        {
            Action performed = Performed;
            if (performed != null)
            {
                performed();
            }
        }
    }
}

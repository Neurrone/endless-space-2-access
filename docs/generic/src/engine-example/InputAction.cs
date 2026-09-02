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
        /// <see cref="ModInput"/>.</summary>
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

        /// <summary>Told whenever this action's bindings change, so whoever owns the action can drop
        /// what it derived from them - the input manager's set of claimed key codes. Set by
        /// <see cref="ModInput.Register"/>; null for an action nobody registered.</summary>
        internal Action BindingsChanged;

        /// <summary>
        /// Put a different chord on one of the action's SLOTS - what a rebind does.
        ///
        /// Slots are positions, not entries: slot 0 is the primary and slot 1 the secondary, the two
        /// the options window's key-mapping row draws, and a null <paramref name="binding"/> empties
        /// that slot rather than shifting the other one up. An action bound to more chords than that
        /// keeps them: they sit past slot 1, still fire, and no row offers them
        /// (<c>docs/interaction.md</c>).
        /// </summary>
        internal void SetSlot(int slot, InputBinding binding)
        {
            if (slot < 0)
            {
                return;
            }

            while (_bindings.Count <= slot)
            {
                _bindings.Add(null);
            }

            _bindings[slot] = binding;
            // A null in the middle of the list is a slot nobody filled; the tail is trimmed so that
            // Bindings never ends in one and every reader can stop asking about nulls.
            while (_bindings.Count > 0 && _bindings[_bindings.Count - 1] == null)
            {
                _bindings.RemoveAt(_bindings.Count - 1);
            }

            Action changed = BindingsChanged;
            if (changed != null)
            {
                changed();
            }
        }

        /// <summary>The chord in one slot, or null where that slot is empty.</summary>
        internal InputBinding Slot(int slot)
        {
            return slot >= 0 && slot < _bindings.Count ? _bindings[slot] : null;
        }

        /// <summary>Mark the action as auto-repeating while held.</summary>
        public InputAction Repeating()
        {
            Repeats = true;
            return this;
        }

        /// <summary>
        /// When the action's keys are taken from the game, for an action whose key the game has a
        /// use for too. Null - the usual case - means the key is the mod's for as long as one of its
        /// screens is focused.
        ///
        /// Space is the case this exists for: it is the game's own key everywhere except on a
        /// control that has something to pick up or while something is being carried. Claiming it
        /// unconditionally would cost the player the game's use of it on every screen the mod knows.
        /// </summary>
        public Func<bool> ClaimedWhen { get; private set; }

        /// <summary>Take this action's keys from the game only while <paramref name="when"/> says so;
        /// see <see cref="ClaimedWhen"/>. Asked from the game's own key scans, several times a frame.
        /// </summary>
        public InputAction ClaimedWhile(Func<bool> when)
        {
            ClaimedWhen = when;
            return this;
        }

        /// <summary>Whether this action's keys are the mod's at this moment.</summary>
        internal bool ClaimsItsKeys()
        {
            Func<bool> when = ClaimedWhen;
            if (when == null)
            {
                return true;
            }

            try
            {
                return when();
            }
            catch (Exception)
            {
                // Runs inside the game's own scan: leave the key to the game rather than throw into
                // it, which is also the safe answer - the worst it costs is one press.
                return false;
            }
        }

        /// <summary>Whether one of this action's bindings is on <paramref name="key"/>.</summary>
        internal bool BoundTo(KeyCode key)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                KeyboardBinding keyboard = _bindings[i] as KeyboardBinding;
                if (keyboard != null && keyboard.Key == key)
                {
                    return true;
                }
            }

            return false;
        }

        public bool JustPressed
        {
            get
            {
                for (int i = 0; i < _bindings.Count; i++)
                {
                    if (_bindings[i] != null && _bindings[i].JustPressed())
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
                    if (_bindings[i] != null && _bindings[i].Held())
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
                    // An empty SLOT is skipped rather than named: a row with no primary key still
                    // reads out whatever its secondary is on.
                    if (_bindings[i] == null)
                    {
                        continue;
                    }

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

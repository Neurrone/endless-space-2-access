using System;
using System.Collections.Generic;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// Decides which screen the player is on, every frame, by asking rather than by being told.
    ///
    /// Each tick re-evaluates <see cref="Screen.IsActive"/> across every registered screen, sorts the
    /// survivors by layer, and diffs that list against the stack from last frame. Nothing subscribes
    /// to the game's window events, so there is no way for the mod to end up believing in a screen the
    /// game has closed - a whole class of "the mod is stuck on the previous page" bugs that a
    /// subscription model has to defend against and this one cannot have.
    ///
    /// The cost is one cheap predicate per screen per frame, which is why IsActive must stay cheap.
    ///
    /// A screen that throws is treated as inactive and logged, so one broken page cannot take the
    /// navigation layer down with it.
    /// </summary>
    public sealed class ScreenManager
    {
        private readonly List<Screen> _registered = new List<Screen>();
        private readonly GraphNavigator _navigator;
        private List<Screen> _stack = new List<Screen>();
        private Screen _focused;

        public ScreenManager(GraphNavigator navigator)
        {
            _navigator = navigator;
        }

        /// <summary>The screen the player is on, or null when none of ours is showing.</summary>
        public Screen Current
        {
            get { return _stack.Count > 0 ? _stack[_stack.Count - 1] : null; }
        }

        public IList<Screen> Stack
        {
            get { return _stack; }
        }

        public void Register(Screen screen)
        {
            if (screen != null && !_registered.Contains(screen))
            {
                _registered.Add(screen);
            }
        }

        public void Tick()
        {
            ApplyDiff(Resolve());
            SyncFocus();

            Screen current = Current;
            if (current != null)
            {
                Safe(current.OnUpdate, current, "OnUpdate");
            }

            // OnUpdate may have changed what is showing; re-syncing is free when nothing moved.
            SyncFocus();
            _navigator.EnsureFocus();
        }

        /// <summary>Drop every screen as though the game had closed them all - the mod is going away.
        /// </summary>
        public void Shutdown()
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                Pop(_stack[i]);
            }

            _stack = new List<Screen>();
            _focused = null;
            _navigator.Attach(null);
            _registered.Clear();
        }

        // Active screens, bottom layer first. Insertion-sorted rather than List.Sort, which is not
        // stable: two screens on the same layer must stay in registration order.
        private List<Screen> Resolve()
        {
            List<Screen> active = new List<Screen>();
            for (int i = 0; i < _registered.Count; i++)
            {
                Screen screen = _registered[i];
                if (!IsActive(screen))
                {
                    continue;
                }

                int at = active.Count;
                while (at > 0 && active[at - 1].Layer > screen.Layer)
                {
                    at--;
                }

                active.Insert(at, screen);
            }

            return active;
        }

        private void ApplyDiff(List<Screen> desired)
        {
            // Closures first, from the top down, then openings from the bottom up, so a screen that
            // replaced another hears about it in the order the player experienced it.
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (!desired.Contains(_stack[i]))
                {
                    Pop(_stack[i]);
                }
            }

            for (int i = 0; i < desired.Count; i++)
            {
                if (!_stack.Contains(desired[i]))
                {
                    Safe(desired[i].OnPush, desired[i], "OnPush");
                }
            }

            _stack = desired;
        }

        private void Pop(Screen screen)
        {
            Safe(screen.OnPop, screen, "OnPop");
            if (!screen.KeepStateOnPop)
            {
                _navigator.ScreenClosed(screen);
            }
        }

        // The one place focus changes hands, so a screen opening, closing or being covered all
        // announce identically.
        private void SyncFocus()
        {
            Screen current = Current;
            if (ReferenceEquals(current, _focused))
            {
                return;
            }

            if (_focused != null)
            {
                Safe(_focused.OnUnfocus, _focused, "OnUnfocus");
            }

            _focused = current;
            if (current != null)
            {
                Safe(current.OnFocus, current, "OnFocus");

                // Queued, not interrupting: the focused control's readout follows it.
                Voice.Say(current.ScreenName, false);
            }

            _navigator.Attach(current);
        }

        private static bool IsActive(Screen screen)
        {
            try
            {
                return screen.IsActive();
            }
            catch (Exception e)
            {
                Log.Warn("screens: " + screen.Key + ".IsActive threw: " + e);
                return false;
            }
        }

        private static void Safe(Action action, Screen screen, string what)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Warn("screens: " + screen.Key + "." + what + " threw: " + e);
            }
        }
    }
}

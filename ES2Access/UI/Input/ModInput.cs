using System;
using System.Collections.Generic;
using System.Threading;
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
        /// <summary>
        /// The one key the mod uses and usually lets the game have. Screens delegate Escape back to
        /// the game on purpose - closing a drop list, opening and closing the pause menu and
        /// cancelling a message box are all the game's own Escape routes.
        ///
        /// The exception is a surface the mod itself put on the screen - an action menu - which the
        /// game has never heard of and so cannot close. There Escape must not ALSO reach the game, or
        /// one keystroke closes the menu and opens the pause screen behind it; see
        /// <see cref="ClaimsBackKey"/>.
        /// </summary>
        private const KeyCode DelegatedKey = KeyCode.Escape;

        private readonly List<InputAction> _actions = new List<InputAction>();
        private readonly Queue<Injection> _injected = new Queue<Injection>();
        private HashSet<KeyCode> _claimedKeys;

        /// <summary>Offered every triggered action; returning true consumes it. Null means nothing is
        /// listening, so every action falls through to its own <see cref="InputAction.Performed"/>.
        /// </summary>
        public Func<InputAction, bool> Dispatch;

        /// <summary>
        /// Asked about the control the game is currently giving the keyboard to, when there is one.
        /// Returning true means "that control is ours" and the layer keeps its keys.
        ///
        /// It exists because <see cref="GameOwnsKeyboard"/> reads the game's exclusivity flag, and
        /// that flag means two different things. On a text field it means what it says: the player is
        /// typing. On a widget the mod is driving from the keyboard it means only that the game would
        /// have wanted the keys had a mouse opened it - a drop list's popup reads no keys of its own,
        /// and standing down for it would leave the player in a list they could not move in.
        ///
        /// Deliberately a predicate rather than a type test in here: which widgets the mod is driving
        /// is the screens' business and changes as screens are added, and the input layer has no way
        /// to know. Null means the game's flag is taken at face value.
        /// </summary>
        public Func<AgeControl, bool> DrivenByMod;

        /// <summary>
        /// Asked whether the mod has a screen for what the player is looking at. The layer's keys
        /// only mean anything then - see ModEntry.Dispatch, which turns every action down when no
        /// screen of ours is focused - and it is also what decides whether the game has to keep its
        /// hands off them; see <see cref="ClaimsKey"/>.
        ///
        /// A predicate rather than a reference to the screen stack, for the same reason
        /// <see cref="DrivenByMod"/> is one: the input layer knows about keys, not screens.
        /// </summary>
        public Func<bool> HasFocusedScreen;

        /// <summary>
        /// Asked whether the focused screen is about to take Escape for itself, so the game can be
        /// told to stand down from it for that one press.
        ///
        /// It has to be a question asked BEFORE the key is delivered, because the game's own scan can
        /// run either side of the mod's frame and by the time the key has been handled the screen that
        /// claimed it has closed. Null means Escape is always the game's.
        /// </summary>
        public Func<bool> ClaimsBackKey;

        /// <summary>
        /// Asked whether a key is one the focused surface is taking as TYPED TEXT - the type-ahead
        /// search - rather than leaving to the game, which has letter hotkeys of its own.
        ///
        /// Asked BEFORE the press, like <see cref="ClaimsBackKey"/> and for the same reason: the
        /// game's scan can run either side of the mod's frame. It needs no release latch, though,
        /// because what it answers turns on the screen being focused and taking text - and typing a
        /// letter changes neither. Null means every key is offered to the game as usual.
        /// </summary>
        public Func<KeyCode, bool> ClaimsTypedKey;

        // Set when a back action was actually consumed, cleared when the key comes up. It covers the
        // other half of the race: the game's scan running AFTER the mod's frame, by which point the
        // predicate above is answering for a menu that is already gone.
        private bool _backClaimed;

        /// <summary>
        /// The keys the mod has already acted on this press, held until the player lets go of them.
        ///
        /// <see cref="ClaimsKey"/> otherwise answers for the layer as it is at the moment the game
        /// asks, and the game asks at its own point in the frame - after ours. That is a race an
        /// action can lose against ITSELF: Enter on a button that opens a modal is handled here, the
        /// screens tick in the same frame, the page stands down for the modal and the modal's own
        /// screen has not finished arriving, so by the time the game's scan runs there is no focused
        /// screen and the very key we just consumed reads as unclaimed. The game then does what it
        /// has bound to it - Enter and Tab are both StartChatting - and the chat panel takes the
        /// engine's keyboard, which silences the whole layer until the player presses Enter again.
        ///
        /// So a key stops being the mod's when it comes UP, not when the layer next looks dead. A
        /// key that was never down - an injected action - is therefore let go on the next tick,
        /// which costs nothing: the game's own matchers ask about a key only while it is pressed.
        /// </summary>
        private readonly List<KeyCode> _consumed = new List<KeyCode>();

        public IList<InputAction> Actions
        {
            get { return _actions; }
        }

        /// <summary>
        /// The latch above, as the dev probe reads it (<c>DevProbe.Claims</c>). Both halves of the
        /// Escape/consumed-key machinery only ever misbehave BETWEEN the mod's frame and the game's
        /// scan, so the only way to tell a leak from a stuck claim is to look at the latch itself -
        /// which used to mean reflecting into these fields from the REPL.
        /// </summary>
        public IList<KeyCode> ConsumedKeys
        {
            get { return _consumed.AsReadOnly(); }
        }

        /// <summary>Whether Escape is still claimed from the press that consumed it.</summary>
        public bool BackClaimed
        {
            get { return _backClaimed; }
        }

        /// <summary>Whether a screen of ours is focused at all - the first half of
        /// <see cref="LayerIsLive"/>, split out so a probe can say WHICH half answered no.</summary>
        public bool ScreenIsFocused()
        {
            Func<bool> focused = HasFocusedScreen;
            return focused != null && focused();
        }

        public InputAction Register(string key)
        {
            InputAction action = new InputAction(key);
            _actions.Add(action);
            _claimedKeys = null;
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
        /// One action asked for over the dev server rather than pressed, and what became of it. The
        /// requester blocks on <see cref="Done"/> because the answer - who consumed it, or why
        /// nobody could - is only known once the frame's <see cref="ModInput.Tick"/> has run it.
        /// </summary>
        public sealed class Injection
        {
            public Injection(string actionKey)
            {
                ActionKey = actionKey;
            }

            public readonly string ActionKey;

            /// <summary>Set once the action has been run (or refused, or abandoned).</summary>
            public readonly ManualResetEvent Done = new ManualResetEvent(false);

            /// <summary>Whether a listener took the action - <see cref="Dispatch"/> returned true.
            /// </summary>
            public bool Consumed;

            /// <summary>Set when the action was not offered to anyone because a game text field held
            /// the keyboard, which is the same answer a real keypress would have got.</summary>
            public bool StoodDown;

            /// <summary>What went wrong, when something did.</summary>
            public string Error;

            internal void Finish()
            {
                Done.Set();
            }
        }

        /// <summary>
        /// Ask for <paramref name="action"/> to run on the next <see cref="Tick"/> exactly as a
        /// matched key binding would - same dispatch, same stand-down, same frame position - so a
        /// test can drive the production input path without a keyboard. Repeat semantics are
        /// deliberately not simulated: an injection is one press.
        ///
        /// Main thread only, like <see cref="Tick"/> itself; the dev route marshals onto it.
        /// </summary>
        public Injection Inject(InputAction action)
        {
            Injection injection = new Injection(action.Key);
            _injected.Enqueue(injection);
            return injection;
        }

        /// <summary>Release every injection still waiting - the mod is unloading, and the HTTP
        /// threads blocked on them would otherwise wait out their whole budget against a queue that
        /// will never be drained.</summary>
        public void CancelInjections()
        {
            while (_injected.Count > 0)
            {
                Injection injection = _injected.Dequeue();
                injection.Error = "the mod unloaded before the action ran";
                injection.Finish();
            }
        }

        /// <summary>
        /// True while a game text field holds the keyboard. The whole layer stands down then: the
        /// player is typing a save name or a multiplayer message, and every letter, arrow and
        /// Backspace belongs to that field.
        /// </summary>
        public static bool GameOwnsKeyboard()
        {
            return ExclusiveControl() != null;
        }

        /// <summary>The control the game says is taking the keyboard to itself, or null.</summary>
        private static AgeControl ExclusiveControl()
        {
            try
            {
                AgeManager age = AgeManager.Instance;
                AgeControl focused = age == null ? null : age.FocusedControl;
                return focused != null && focused.IsKeyExclusive ? focused : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The same question as <see cref="GameOwnsKeyboard"/>, with the mod's own widgets
        /// excepted: a control the mod put the game's focus on is one the mod is working, not one the
        /// player is typing into. The other half of <see cref="LayerIsLive"/>.</summary>
        public bool KeyboardIsElsewhere()
        {
            AgeControl focused = ExclusiveControl();
            if (focused == null)
            {
                return false;
            }

            Func<AgeControl, bool> ours = DrivenByMod;
            try
            {
                return ours == null || !ours(focused);
            }
            catch (Exception)
            {
                // A hook that cannot answer means standing down, which is the safe answer: the worst
                // it costs is the mod's keys, and the alternative is stealing the player's typing.
                return true;
            }
        }

        /// <summary>
        /// True while <paramref name="key"/> belongs to the mod and to nothing else. The game polls
        /// UnityEngine.Input in parallel with us, so without this a key we act on ALSO fires the
        /// game's own binding on it - Tab opens the chat box and takes the keyboard with it, Enter
        /// answers a message box a second time, an arrow pans the galaxy camera under the cursor.
        /// The game's key scans ask this and stand down when it says yes.
        ///
        /// Only while the layer is live: a screen of ours is focused and the game is not holding the
        /// keyboard for something the player is typing into. Otherwise the game's keys are the only
        /// ones there are and it must see everything, unchanged.
        ///
        /// Called from the game's per-frame scans, so it stays a set lookup and two flag reads.
        /// </summary>
        public bool ClaimsKey(KeyCode key)
        {
            if (_consumed.Count > 0 && _consumed.Contains(key))
            {
                return true;
            }

            if (!LayerIsLive())
            {
                return false;
            }

            if (key == DelegatedKey)
            {
                return _backClaimed || ClaimsBack();
            }

            return ClaimedKeys().Contains(key) || ClaimsTyped(key);
        }

        /// <summary>Whether the focused surface is taking this key as typed text - the other
        /// before-the-press question, asked after the bindings so a bound key stays bound.</summary>
        public bool ClaimsTyped(KeyCode key)
        {
            Func<KeyCode, bool> claims = ClaimsTypedKey;
            try
            {
                return claims != null && claims(key);
            }
            catch (Exception)
            {
                // Runs inside the game's own scan: leave the key to the game rather than throw into
                // it, which is also the safe answer - the worst it costs is one letter.
                return false;
            }
        }

        /// <summary>What the focused screen answers to "are you about to take Escape" - asked before
        /// the press, and worth reading on its own: it is the half of the Escape story the
        /// <see cref="BackClaimed"/> latch cannot explain.</summary>
        public bool ClaimsBack()
        {
            Func<bool> claims = ClaimsBackKey;
            try
            {
                return claims != null && claims();
            }
            catch (Exception)
            {
                // Runs inside the game's own scan: leave the key to the game rather than throw into
                // it, which is also the safe answer - a stuck claim would make Escape do nothing.
                return false;
            }
        }

        /// <summary>Whether the mod's keys mean anything right now: a screen of ours is focused and
        /// the game is not holding the keyboard for something the player is typing into. What
        /// <see cref="ClaimsKey"/> asks before anything but the latch.</summary>
        public bool LayerIsLive()
        {
            return ScreenIsFocused() && !KeyboardIsElsewhere();
        }

        // Built once and dropped whenever an action is added, which is the only way the bindings
        // change today; a rebinding UI would have to drop it too.
        private HashSet<KeyCode> ClaimedKeys()
        {
            if (_claimedKeys != null)
            {
                return _claimedKeys;
            }

            HashSet<KeyCode> keys = new HashSet<KeyCode>();
            for (int i = 0; i < _actions.Count; i++)
            {
                IList<InputBinding> bindings = _actions[i].Bindings;
                for (int j = 0; j < bindings.Count; j++)
                {
                    KeyboardBinding keyboard = bindings[j] as KeyboardBinding;
                    if (keyboard != null)
                    {
                        keys.Add(keyboard.Key);
                    }
                }
            }

            _claimedKeys = keys;
            return keys;
        }

        /// <summary>Poll every action and dispatch what fired. Call once per frame, before the
        /// screens tick, so a keypress and the announcement it causes land in the same frame.</summary>
        public void Tick()
        {
            // A claim on Escape lasts exactly as long as the press that earned it: the game's scan
            // may still be looking at this frame's keyboard, but the next press is a new question.
            if (_backClaimed && !UnityEngine.Input.GetKey(DelegatedKey))
            {
                _backClaimed = false;
            }

            ReleaseKeysNoLongerHeld();

            if (KeyboardIsElsewhere())
            {
                Disarm();
                RunInjected(true);
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

                Deliver(action);
            }

            RunInjected(false);
        }

        // Where a triggered action goes: the listening layer first, and the action's own handler when
        // nothing wanted it. True when a listener consumed it.
        private bool Deliver(InputAction action)
        {
            Func<InputAction, bool> dispatch = Dispatch;
            if (dispatch != null && dispatch(action))
            {
                if (action.Key == UiActions.Back)
                {
                    _backClaimed = true;
                }

                KeepKeysUntilReleased(action);
                return true;
            }

            action.InvokePerformed();
            return false;
        }

        // The keys that could have made this action fire stay ours for the rest of the press - see
        // _consumed. The action may well have just emptied the screen stack, and the game has not
        // asked about the key yet.
        private void KeepKeysUntilReleased(InputAction action)
        {
            IList<InputBinding> bindings = action.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                KeyboardBinding keyboard = bindings[i] as KeyboardBinding;
                if (keyboard == null || _consumed.Contains(keyboard.Key))
                {
                    continue;
                }

                _consumed.Add(keyboard.Key);
            }
        }

        private void ReleaseKeysNoLongerHeld()
        {
            for (int i = _consumed.Count - 1; i >= 0; i--)
            {
                if (!UnityEngine.Input.GetKey(_consumed[i]))
                {
                    _consumed.RemoveAt(i);
                }
            }
        }

        // The injected actions queued since the last frame, run through exactly the path a pressed
        // key takes. Standing down is reported rather than worked around: an injection that arrives
        // while a text field holds the keyboard must be as invisible as the keypress would have been.
        private void RunInjected(bool standingDown)
        {
            while (_injected.Count > 0)
            {
                Injection injection = _injected.Dequeue();
                try
                {
                    InputAction action = Find(injection.ActionKey);
                    if (standingDown)
                    {
                        injection.StoodDown = true;
                    }
                    else if (action == null)
                    {
                        // Validated before queueing, so this only happens if the bindings changed in
                        // between - worth saying rather than silently dropping.
                        injection.Error = "no action named '" + injection.ActionKey + "' any more";
                    }
                    else
                    {
                        injection.Consumed = Deliver(action);
                    }
                }
                catch (Exception e)
                {
                    injection.Error = e.ToString();
                }

                injection.Finish();
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

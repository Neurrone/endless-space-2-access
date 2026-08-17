using System;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>What a screen wants of an edit beyond the ordinary. Every field in the game takes the
    /// defaults; these are for the two that do not.</summary>
    public sealed class TextEditOptions
    {
        /// <summary>How the keyboard is handed over, where the game has a route of its own that does
        /// more than focus the widget (the chat panel opens itself out of its discreet state in the
        /// same call). Null means focus the field, which is what a click does everywhere else.
        /// </summary>
        public Action HandOver;

        /// <summary>
        /// Whether the game REFUSED what was typed, asked once after a commit: null for taken, and
        /// any string - the game's own reason, or empty where it gives none - for refused. A refusal
        /// puts the keyboard back into the field rather than closing it, because cancelling on the
        /// player's behalf would throw away what they wrote.
        ///
        /// Empty and null are different answers on purpose. A game can refuse without saying why (the
        /// rename box only writes its reason for some refusals), and "no words" must still mean "you
        /// are back in the box", never "it worked".
        /// </summary>
        public Func<string> Refusal;

        /// <summary>Whether a commit says so. The chat box is the exception: its Enter SENDS, and the
        /// line arriving back through the chat service is the announcement (<see cref="UI.SessionChat"/>) -
        /// "edited" on top of it would be saying the same event twice.</summary>
        public bool AnnounceCommit = true;
    }

    /// <summary>
    /// Every text box in the game, entered and left the same way.
    ///
    /// The game gives a text box a caret and nothing else: no words on the way in, none per character,
    /// none on the way out, and no notion of cancelling at all - its Escape simply drops the engine's
    /// focus and leaves whatever was typed in the box (<c>InputManager.HandleInput</c> :1210-1243).
    /// So all of it is the mod's: "editing" on the way in, the character typed or deleted as it
    /// happens, the character under the caret as it moves, and "edited" or "Cancelled" on the way out
    /// with the pre-edit text put back for a cancel.
    ///
    /// THREE SEAMS, and each is the only place its question can be answered:
    ///
    /// <b>The deferred hand-over</b> (<see cref="Update"/>, per screen). The engine delivers key events
    /// to the focused control in its own LateUpdate, after the mod's frame, and a text field's answer
    /// to Return is to hand the focus straight back - which for these fields is also what commits.
    /// Handing over during the frame Enter was pressed therefore gives the field that very Enter.
    ///
    /// <b>The focus setter</b> (<see cref="FocusLeaving"/>). Escape never reaches the field:
    /// <c>InputManager</c> clears the focus from Update, before the engine's KeyDown dispatch runs at
    /// all. A right click and a click elsewhere do not reach it either. <c>AgeManager.FocusedControl</c>
    /// is the one choke point every one of them passes through, and it is also the last moment BEFORE
    /// the game's own lose-focus handlers run - which is where the pre-edit text has to go back, so
    /// that a panel which commits what it finds in the box on the way out (the marketplace clamps and
    /// commits its quantity there) commits the value the player started with.
    ///
    /// <b>The per-frame reading</b> (<see cref="Tick"/>, from the pump). What was typed is worked out
    /// by comparing the box with itself a frame ago (<see cref="TextEditEcho"/>) rather than hooked:
    /// one reading catches the character, the Backspace, the caret step AND the engine's own held-key
    /// repeat, which runs from a coroutine no key dispatch passes through. It speaks from the pump,
    /// which is where all speech belongs.
    ///
    /// ONE edit at a time, because the engine has one focused control at a time - so the request is
    /// per screen (a reload takes it with the screen, and two screens never fight over one) and the
    /// live edit is static.
    /// </summary>
    public sealed class TextFieldEditor
    {
        private AgeControlTextField _field;
        private ControlId _row;
        private object _owner;
        private MethodInfo _gainFocus;
        private TextEditOptions _options;

        /// <summary>Ask for the game's editor, and say so - entering an editor is not a thing a player
        /// can be left to infer from silence.</summary>
        public void Request(
            AgeControlTextField field,
            object owner,
            MethodInfo gainFocus,
            ControlId row
        )
        {
            Request(field, owner, gainFocus, row, null);
        }

        public void Request(
            AgeControlTextField field,
            object owner,
            MethodInfo gainFocus,
            ControlId row,
            TextEditOptions options
        )
        {
            if (_field != null || field == null)
            {
                return;
            }

            _field = field;
            _owner = owner;
            _gainFocus = gainFocus;
            _row = row;
            _options = options;
            Voice.Say(ModStrings.Get(ModStrings.EditStarted), true);
        }

        /// <summary>Whether an editor has been asked for and the keyboard has not changed hands yet.
        /// The screen that owns this editor answers <c>CapturesRawInput</c> with it: during the wait
        /// the mod's keys are still live, and what the player types next is meant for the field.
        /// </summary>
        public bool Pending
        {
            get { return _field != null; }
        }

        /// <summary>Called from the owning screen's per-frame update.</summary>
        public void Update()
        {
            AgeControlTextField field = _field;
            if (field == null)
            {
                return;
            }

            // Moving off the row during the wait is the player changing their mind, and the request has
            // to go with them - otherwise the keyboard would be handed to a field they have left.
            if (!OnRow(_row))
            {
                Cancel();
                return;
            }

            // Wait for a frame on which nothing new went down. Spelled out: the game has its own
            // Input in the global namespace.
            //
            // KEPT rather than removed, deliberately. GameKeyboardHandover now suppresses the engine's
            // whole KeyDown dispatch on a frame the mod already spent a key, which should make this
            // wait redundant - but "should" is the wrong word for the bug it prevents (an editor that
            // opens and commits inside one frame), and it cannot be measured from a test: an injected
            // activation presses no physical key, so `anyKeyDown` is already false and the wait is a
            // no-op for every automated run. Removing it needs a physical Enter on a real field, which
            // is a manual test, not a probe.
            if (UnityEngine.Input.anyKeyDown)
            {
                return;
            }

            object owner = _owner;
            MethodInfo gainFocus = _gainFocus;
            TextEditOptions options = _options;
            ControlId row = _row;
            Cancel();
            try
            {
                AgeManager age = AgeManager.Instance;
                if (age == null || !AgeWidgets.Operable(AgeWidgets.Transform(field)))
                {
                    return;
                }

                // Read BEFORE the hand-over: a panel can empty its own box in the gain-focus handler
                // (the marketplace does), and the text a cancel puts back is the one that was there
                // when the player asked to edit, not the one the game left behind.
                string before = RawText(field);

                if (options != null && options.HandOver != null)
                {
                    options.HandOver();
                }
                else
                {
                    age.FocusedControl = field;
                }

                OptionsScreen.Call(gainFocus, owner, OptionsScreen.NoSender);
                if (Typing(field))
                {
                    Begin(field, before, row, options);
                }
            }
            catch (Exception e)
            {
                Log.Warn("settings: opening a text editor threw: " + e);
            }
        }

        public void Cancel()
        {
            _field = null;
            _owner = null;
            _gainFocus = null;
            _row = null;
            _options = null;
        }

        /// <summary>Whether the game currently has the keyboard on this field - asked of the engine's
        /// own focus, so an edit the game ended is over here the same instant.</summary>
        public static bool Typing(AgeControlTextField field)
        {
            try
            {
                AgeManager age = AgeManager.Instance;
                return field != null
                    && age != null
                    && ReferenceEquals(age.FocusedControl, field);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool OnRow(ControlId id)
        {
            try
            {
                GraphNavigator navigator = ModEntry.Navigator;
                GraphNode node = navigator == null ? null : navigator.CurrentNode;
                return id != null && node != null && id.Equals(node.Id);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- the live edit ----

        /// <summary>How an edit ended, decided at the focus setter and acted on from the pump - because
        /// the setter runs inside the engine's own dispatch, where nothing may speak.</summary>
        private enum Ending
        {
            None,
            Committed,
            Cancelled,

            /// <summary>The mod took the keyboard back itself. Nothing happened that the player did,
            /// so nothing is said and nothing is put back.</summary>
            Abandoned,
        }

        private static AgeControlTextField _editing;
        private static string _snapshot;
        private static ControlId _editingRow;
        private static TextEditOptions _editingOptions;
        private static string _lastText;
        private static int _lastCaret;
        private static Ending _ending;
        private static FieldInfo _caret;
        private static bool _reportedFailure;

        /// <summary>Whether a text box the mod put the player into is holding the keyboard right now.
        /// </summary>
        public static bool Editing
        {
            get { return _editing != null; }
        }

        /// <summary>
        /// The same question asked by anything ELSE that watches the game's keyboard focus - the chat
        /// panels have their own watcher for the boxes a mouse or the chat key opens - and it has to
        /// stay true for the frame the edit ENDS on as well, or that watcher announces the way out a
        /// second time on top of this one's "Cancelled".
        /// </summary>
        public static bool Owned
        {
            get { return _editing != null || _finishedOnFrame == UnityEngine.Time.frameCount; }
        }

        private static int _finishedOnFrame = -1;

        /// <summary>
        /// Answer the next release as a COMMIT whatever the keyboard says, and forget it again
        /// immediately. The dev server's lever (<c>DevProbe.EndEdit</c>), and nothing else sets it:
        /// a commit is a physical Return, and no injection can press one - so without this the whole
        /// "edited" half of an edit could only ever be tested by hand.
        /// </summary>
        internal static bool CommitTheNextRelease;

        private static void Begin(
            AgeControlTextField field,
            string snapshot,
            ControlId row,
            TextEditOptions options
        )
        {
            _editing = field;
            _snapshot = snapshot;
            _editingRow = row;
            _editingOptions = options;
            _ending = Ending.None;
            Baseline();
        }

        /// <summary>Take the box as it stands now to be the thing the next frame is compared against -
        /// so that whatever the game wrote into it while taking the keyboard is not read out as
        /// typing.</summary>
        private static void Baseline()
        {
            _lastText = RawText(_editing);
            _lastCaret = CaretOf(_editing);
        }

        /// <summary>
        /// The engine is about to move its keyboard focus. Called from the setter's own prefix - the
        /// one place a commit, an Escape, a right click and a click on something else all pass through.
        ///
        /// Decides what ended the edit and, for a cancel, puts the pre-edit text back HERE rather than
        /// a frame later: the game's lose-focus handlers run immediately after this and one of them
        /// commits whatever it finds in the box.
        /// </summary>
        internal static void FocusLeaving(AgeControl leaving, AgeControl arriving)
        {
            if (_editing == null || !ReferenceEquals(leaving, _editing) || ReferenceEquals(leaving, arriving))
            {
                return;
            }

            // Return is the only key that can commit one of these boxes; everything else that takes
            // the keyboard away - Escape, a right click, a click elsewhere - abandons the edit.
            bool committed =
                CommitTheNextRelease
                || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)
                || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadEnter);
            CommitTheNextRelease = false;

            if (!committed)
            {
                Restore();
            }

            _ending = committed ? Ending.Committed : Ending.Cancelled;
        }

        /// <summary>Let go of an edit without a word: the mod itself is taking the keyboard back - a
        /// window that stopped being drawn, a screen closing, the mod unloading - and none of that is
        /// something the player did to the text.</summary>
        public static void Abandon()
        {
            if (_editing == null)
            {
                return;
            }

            _ending = Ending.Abandoned;
            Clear();
            _finishedOnFrame = UnityEngine.Time.frameCount;
        }

        /// <summary>Called every frame from the pump, before the screens rebuild.</summary>
        public static void Tick()
        {
            try
            {
                if (_ending != Ending.None)
                {
                    Finish();
                    return;
                }

                if (_editing == null)
                {
                    return;
                }

                if (!Typing(_editing))
                {
                    // The focus went without the setter's prefix saying so - it is not installed, or
                    // the control was destroyed. Nothing is known about why, so nothing is said.
                    Clear();
                    return;
                }

                Echo();
            }
            catch (Exception e)
            {
                Warn("edit: reading a text box being typed in threw: " + e);
                Clear();
            }
        }

        private static void Echo()
        {
            string text = RawText(_editing);
            int caret = CaretOf(_editing);
            EditEcho echo = TextEditEcho.Since(_lastText, _lastCaret, text, caret);
            _lastText = text;
            _lastCaret = caret;
            if (echo.Kind != EditEchoKind.None)
            {
                Voice.Say(echo.Text, true);
            }
        }

        private static void Finish()
        {
            Ending ending = _ending;
            AgeControlTextField field = _editing;
            ControlId row = _editingRow;
            TextEditOptions options = _editingOptions;
            string snapshot = _snapshot;
            Clear();
            _finishedOnFrame = UnityEngine.Time.frameCount;

            if (ending == Ending.Abandoned)
            {
                return;
            }

            if (ending == Ending.Committed)
            {
                string refusal = Refusal(options);
                if (refusal != null)
                {
                    // The game would not take it. Handing the keyboard back rather than closing is
                    // what keeps what the player typed; the words are the game's where it has any,
                    // and "editing" where it has none - a refusal the player cannot hear is a box
                    // they think they have left.
                    PutTheKeyboardBack(field, snapshot, row, options);
                    Voice.Say(
                        refusal.Length > 0 ? refusal : ModStrings.Get(ModStrings.EditStarted),
                        true
                    );
                    return;
                }

                // QUEUED, not interrupting. A commit can take the whole surface away with it (the
                // rename box closes and the page under it announces its own arrival), and a word that
                // interrupts that landing costs the player the landing and tells them less than it
                // takes away. Nothing else is ever speaking when the surface stays.
                if (options == null || options.AnnounceCommit)
                {
                    Voice.Say(ModStrings.Get(ModStrings.EditCommitted), false);
                }

                // And the field again, with what is in it now - but only where the cursor is still
                // standing on it. A commit that closed the surface has already handed the player
                // somewhere else, and that landing is its own announcement. QUEUED behind "edited"
                // rather than interrupting it, or the word the player is waiting for is the one they
                // never hear.
                if (OnRow(row))
                {
                    GraphNavigator navigator = ModEntry.Navigator;
                    if (navigator != null)
                    {
                        navigator.AnnounceCurrent(false);
                    }
                }

                return;
            }

            // A cancel says one word and stops there: what is in the box is what was in it when the
            // player entered, and they have already heard it. Queued for the same reason as the
            // commit - the row's own live value part speaks the restored text as the field lets go,
            // and the chat page announces the node it steps out onto, and this belongs after either.
            Voice.Say(ModStrings.Get(ModStrings.EditCancelled), false);
        }

        private static string Refusal(TextEditOptions options)
        {
            try
            {
                return options == null || options.Refusal == null ? null : options.Refusal();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void PutTheKeyboardBack(
            AgeControlTextField field,
            string snapshot,
            ControlId row,
            TextEditOptions options
        )
        {
            try
            {
                AgeManager age = AgeManager.Instance;
                if (age == null || field == null || age.FocusedControl != null)
                {
                    return;
                }

                age.FocusedControl = field;
                if (Typing(field))
                {
                    Begin(field, snapshot, row, options);
                }
            }
            catch (Exception e)
            {
                Warn("edit: handing the keyboard back after a refusal threw: " + e);
            }
        }

        /// <summary>Put back what was in the box before the player started. The game's own replace is
        /// used rather than a bare write to the label, so the caret, the width and the drawn text are
        /// all recomputed the way they are for any other programmatic change.</summary>
        private static void Restore()
        {
            try
            {
                AgeControlTextField field = _editing;
                if (field != null && RawText(field) != (_snapshot ?? string.Empty))
                {
                    field.AdvancedReplaceInputText(_snapshot ?? string.Empty);
                }
            }
            catch (Exception e)
            {
                Warn("edit: putting back what was in a text box threw: " + e);
            }
        }

        private static void Clear()
        {
            _editing = null;
            _snapshot = null;
            _editingRow = null;
            _editingOptions = null;
            _lastText = null;
            _lastCaret = 0;
            _ending = Ending.None;
        }

        /// <summary>Forget everything: the mod is unloading, and a live edit belonging to an assembly
        /// nobody can reach would speak on the next load's first keystroke.</summary>
        public static void Stop()
        {
            Clear();
            _reportedFailure = false;
        }

        /// <summary>The box's text exactly as the engine holds it - not the cleaned reading, because a
        /// cancel puts this string back and a diff compares it against itself.</summary>
        private static string RawText(AgeControlTextField field)
        {
            try
            {
                return field == null || field.Label == null
                    ? string.Empty
                    : (field.Label.Text ?? string.Empty);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>Where the caret is, in characters from the start. Protected on the engine's own
        /// text area and read by reflection: it is the only thing that says where the arrows, Home and
        /// End have just put it, and the engine parks it past the end until it has computed one.
        /// </summary>
        private static int CaretOf(AgeControlTextField field)
        {
            try
            {
                if (_caret == null)
                {
                    _caret = typeof(AgeControlTextArea).GetField(
                        "cursorPositionInText",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    );
                }

                return _caret == null || field == null
                    ? int.MaxValue
                    : (int)_caret.GetValue(field);
            }
            catch (Exception)
            {
                // Past the end, which every reading clamps to end-of-text: an unreadable caret then
                // looks like a caret that never moves, rather than one pinned to the first character.
                return int.MaxValue;
            }
        }

        private static void Warn(string message)
        {
            if (_reportedFailure)
            {
                return;
            }

            _reportedFailure = true;
            Log.Warn(message);
        }
    }
}

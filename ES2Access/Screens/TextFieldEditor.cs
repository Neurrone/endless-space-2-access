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
        /// Whether the mod ENDS THE EDIT ITSELF when the player presses Enter, instead of letting the
        /// key reach the field and fire whatever the game has wired to it.
        ///
        /// The game's own answer to Enter in a text field is to run the window's validate callback
        /// (<c>AgeControlTextField.KeyDown</c> :76-89) - which for the save-name box WRITES A SAVE AND
        /// CLOSES THE WHOLE SCREEN and for the rename box posts the rename and closes the box. That
        /// makes the commit key of the EDIT also the primary button of the SCREEN, so a player who
        /// only wanted to stop typing has performed the screen's action and lost the surface they were
        /// standing on (owner-reported). The owner's ruling: the commit Enter ends the EDIT and
        /// nothing else - the screen's own Save or Confirm is an ordinary control, activated
        /// deliberately. So the key is taken from the game at the engine's own dispatch
        /// (<see cref="UI.Input.GameKeyboardHandover"/>) and the edit is ended here instead.
        ///
        /// The chat box is the exception: its Enter is not a validate that closes something, it is how
        /// a message is SENT, and taking it would leave chat unusable.
        ///
        /// With no validate ever asked, there is nothing left to REFUSE a commit either: a box that
        /// will not take what was typed says so when its own Confirm button is pressed, which is where
        /// the game writes the reason (the rename box's empty name).
        /// </summary>
        public bool OwnCommit = true;

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
    /// FOUR SEAMS, and each is the only place its question can be answered:
    ///
    /// <b>The deferred hand-over</b> (<see cref="Update"/>, per screen). The engine delivers key events
    /// to the focused control in its own LateUpdate, after the mod's frame, and a text field's answer
    /// to Return is to hand the focus straight back - which for these fields is also what commits.
    /// Handing over during the frame Enter was pressed therefore gives the field that very Enter. So
    /// the wait is for the RELEASE, not for the next frame: a press lasts as long as the finger, and a
    /// field that has the keyboard while Return is still down is one engine dispatch from committing
    /// the keystroke that only asked to start editing.
    ///
    /// <b>The commit key</b> (<see cref="CommitInsteadOfTheGamesValidate"/>, from the engine's key
    /// dispatch). Enter in one of these boxes is the game's VALIDATE, and a validate is the screen's
    /// action rather than the edit's - it saves the game and closes the save screen, it posts the
    /// rename and closes the box. So the key is taken from the game there and the edit is ended here,
    /// leaving the surface standing and its own Save or Confirm button to be pressed on purpose.
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
    ///
    /// Typed on <c>AgeControlTextArea</c>, which is the engine's own base for every box a player types
    /// in (<c>AgeControlTextField : AgeControlTextArea</c>): the one-line boxes and the multi-line ones
    /// - a faction's lore paragraph - are the same control to everything here, and the caret this reads
    /// by reflection is declared on the base in the first place. The one difference belongs to the
    /// GAME: a one-line box answers Return by validating, which is what the wait for the key's release
    /// exists for, and a multi-line box answers it with a line break or by simply letting the keyboard
    /// go. Neither needs anything different said or done here.
    /// </summary>
    public sealed class TextFieldEditor
    {
        private AgeControlTextArea _field;
        private ControlId _row;
        private object _owner;
        private MethodInfo _gainFocus;
        private TextEditOptions _options;

        /// <summary>Ask for the game's editor, and say so - entering an editor is not a thing a player
        /// can be left to infer from silence.</summary>
        public void Request(
            AgeControlTextArea field,
            object owner,
            MethodInfo gainFocus,
            ControlId row
        )
        {
            Request(field, owner, gainFocus, row, null);
        }

        public void Request(
            AgeControlTextArea field,
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
            _row = row ?? CurrentRow();
            _options = options;
            Voice.Say(ModStrings.Get(ModStrings.EditStarted), true);
        }

        /// <summary>
        /// The node the request came from, for a caller that cannot name its own id.
        ///
        /// <see cref="Update"/> abandons a pending request the moment the cursor is not on
        /// <see cref="_row"/> any more, so the id handed in has to be the id of the NODE the player
        /// activated - not merely of the thing it stands for. A caller that builds its own cells knows
        /// both and they are the same string; a caller whose cells are built by
        /// <see cref="Core.UI.GraphSheet"/> knows neither, because a sheet keys its cells itself and
        /// says so (its <c>FirstRow</c> is the one id it hands back). Passing a hand-made key there
        /// matched nothing, so every such edit was cancelled on the frame after it was asked for: the
        /// player heard "editing" and then typed into a field the game had never been given (measured
        /// on the negotiation basket's quantity box; owner-reported as "typing into the field doesn't
        /// work at all").
        ///
        /// The request is raised from the node's own activation, so the focused node IS the row. Read
        /// here rather than at the call site so that the answer and the test that consumes it come from
        /// one place.
        /// </summary>
        private static ControlId CurrentRow()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            return navigator == null ? null : navigator.FocusedKey;
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
            AgeControlTextArea field = _field;
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

            // WAIT FOR THE KEY THAT ASKED TO BE LET GO.
            //
            // A press is several frames long, and the field is the game's the moment it has the
            // keyboard: the engine delivers KeyDown to the focused control from its own LateUpdate,
            // the field's answer to Return is to VALIDATE (AgeControlTextField.KeyDown - which for the
            // rename box posts the rename and for the save-name box writes the save and closes the
            // screen), and the window's own Validate handler is a second door onto the same thing. So
            // handing the keyboard over while the activating Return is still down puts the field one
            // engine dispatch away from committing the press that only asked to start editing - which
            // is what shipped (owner-reported: the first Enter on both boxes committed and closed
            // them). Waiting for the release closes every one of those doors at once, because none of
            // them can be reached by a key that is no longer down.
            //
            // The one-frame anyKeyDown wait is kept alongside it: a key going down on the hand-over
            // frame that the mod did NOT spend is somebody else's, and the field should not be given
            // that one either.
            //
            // Spelled out: the game has its own Input in the global namespace.
            if (UnityEngine.Input.anyKeyDown || StillHoldingTheKeyThatAsked())
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

        /// <summary>Whether the press that asked for this edit is still under the player's finger.
        /// False for every injected activation - nothing was physically pressed - which is what makes
        /// the wait invisible to <c>POST /input</c> and provable only with <c>POST /key</c>.</summary>
        private static bool StillHoldingTheKeyThatAsked()
        {
            try
            {
                ES2Access.UI.Input.ModInput input = ModEntry.Input;
                return input != null && input.StillHoldingASpentKey();
            }
            catch (Exception)
            {
                // Never wedge an edit shut on a question that could not be answered.
                return false;
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
        public static bool Typing(AgeControlTextArea field)
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
            GraphNavigator navigator = ModEntry.Navigator;
            return navigator != null && navigator.CursorIsOn(id);
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

        private static AgeControlTextArea _editing;
        private static string _snapshot;
        private static ControlId _editingRow;
        private static TextEditOptions _editingOptions;
        private static string _lastText;
        private static int _lastCaret;
        private static Ending _ending;

        /// <summary>The frame the live edit began on - what tells the Return that OPENED the box from
        /// the Return that commits it (<see cref="FocusLeaving"/>).</summary>
        private static int _beganOnFrame;

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
        /// immediately. Two things set it: the mod's own commit
        /// (<see cref="CommitInsteadOfTheGamesValidate"/>), which has already decided that this is one
        /// and does not need the keyboard asked a second time, and the dev server's lever
        /// (<c>DevProbe.EndEdit</c>) - a commit is otherwise a physical Return, and no injected ACTION
        /// can press one.
        /// </summary>
        internal static bool CommitTheNextRelease;

        private static void Begin(
            AgeControlTextArea field,
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
            _beganOnFrame = UnityEngine.Time.frameCount;
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
            //
            // And it has to be a Return pressed AFTER the edit began. The press that OPENS a box is
            // still going while the box opens, so reading the keyboard on the opening frame would
            // manufacture a commit out of the very keystroke that asked to start typing.
            bool committed =
                CommitTheNextRelease
                || (
                    UnityEngine.Time.frameCount > _beganOnFrame
                    && (
                        UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Return)
                        || UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.KeypadEnter)
                    )
                );
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
            ControlId row = _editingRow;
            TextEditOptions options = _editingOptions;
            Clear();
            _finishedOnFrame = UnityEngine.Time.frameCount;

            if (ending == Ending.Abandoned)
            {
                return;
            }

            if (ending == Ending.Committed)
            {
                // QUEUED, not interrupting. The mod's own commit leaves the surface standing, so
                // nothing else is speaking - and where the game's Enter is still the game's (chat
                // sends with it) the answer coming back is what the player is waiting for, which a
                // word of the mod's would cut off.
                if (options == null || options.AnnounceCommit)
                {
                    Voice.Say(ModStrings.Get(ModStrings.EditCommitted), false);
                }

                // And the field again, with what is in it now - the text the commit kept - but only
                // where the cursor is still standing on it. QUEUED behind "edited" rather than
                // interrupting it, or the word the player is waiting for is the one they never hear.
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

        /// <summary>
        /// THE COMMIT KEY, TAKEN FROM THE GAME. Called from the engine's own key dispatch to the
        /// focused control (<see cref="UI.Input.GameKeyboardHandover"/>) on a frame Return or
        /// KeypadEnter went down; true when the mod took the key, which means the game's
        /// <c>KeyDown</c> - its validate callback AND the base class's unfocus - must not run.
        ///
        /// Ending the edit is done exactly as every other way out of one is: the engine's focus is
        /// dropped, and the setter's own prefix (<see cref="FocusLeaving"/>) is what decides this was
        /// a commit and marks it. Nothing is spoken here - this runs inside the engine's dispatch,
        /// where nothing may speak - and nothing about the SCREEN is touched: the surface stays
        /// standing, with the text the player typed still in the box.
        ///
        /// The press that OPENED the box cannot reach this: a Return the mod itself has just spent is
        /// refused one step earlier, by the consumed-key latch the handover asks about first.
        /// </summary>
        internal static bool CommitInsteadOfTheGamesValidate(AgeControlTextArea field)
        {
            if (_editing == null || !ReferenceEquals(_editing, field))
            {
                return false;
            }

            if (_editingOptions != null && !_editingOptions.OwnCommit)
            {
                return false;
            }

            try
            {
                AgeManager age = AgeManager.Instance;
                if (age == null)
                {
                    return false;
                }

                CommitTheNextRelease = true;
                age.FocusedControl = null;
                return true;
            }
            catch (Exception e)
            {
                CommitTheNextRelease = false;
                Warn("edit: ending an edit on the commit key threw: " + e);
                return false;
            }
        }

        /// <summary>Put back what was in the box before the player started. The game's own replace is
        /// used rather than a bare write to the label, so the caret, the width and the drawn text are
        /// all recomputed the way they are for any other programmatic change - and where the engine
        /// wrote that replace only on its ONE-LINE box (<c>AgeControlTextField</c>), the multi-line box
        /// gets the same two things done to it by hand: the text back, and the caret at the end of
        /// it.</summary>
        private static void Restore()
        {
            try
            {
                AgeControlTextArea field = _editing;
                string text = _snapshot ?? string.Empty;
                if (field == null || RawText(field) == text)
                {
                    return;
                }

                AgeControlTextField line = field as AgeControlTextField;
                if (line != null)
                {
                    line.AdvancedReplaceInputText(text);
                    return;
                }

                field.Label.Text = text;
                SetCaret(field, text.Length);
                field.Label.ComputeText();
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
        private static string RawText(AgeControlTextArea field)
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
        private static int CaretOf(AgeControlTextArea field)
        {
            try
            {
                if (_caret == null)
                {
                    _caret = GameHandlers.Field(
                        typeof(AgeControlTextArea),
                        "cursorPositionInText"
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

        /// <summary>Move the caret, through the same protected field it is read from.</summary>
        private static void SetCaret(AgeControlTextArea field, int position)
        {
            try
            {
                CaretOf(field);
                if (_caret != null && field != null)
                {
                    _caret.SetValue(field, position);
                }
            }
            catch (Exception)
            {
                // A caret that could not be moved is a drawn cursor one frame out of date, which the
                // next keystroke recomputes; the TEXT is what a cancel had to put back.
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

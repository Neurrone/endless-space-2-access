using System;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>Rebinding a key: the state machine that hands the keyboard to the game field, waits
    /// for the capture to end, and says what stuck.</summary>
    public sealed partial class OptionsScreen
    {
        // ---- rebinding a key ----

        /// <summary>The row whose binding is being captured, remembered so that the mod going away
        /// mid-capture can hand the keyboard back and so that the ending can be read out
        /// (<see cref="WatchForTheEndOfACapture"/>). Whether the field is STILL listening is asked of
        /// the game, never of this field, so it can never be out of step with what is on screen.
        /// </summary>
        private static OptionKeyMappingItem _capturing;

        /// <summary>The row that has asked for a capture and is waiting for the player's hand to come
        /// off the keyboard, which of its two bindings was asked for, and the CELL the ask came from -
        /// the cursor has to still be on that one when the keyboard changes hands.</summary>
        private static OptionKeyMappingItem _pending;
        private static bool _pendingSecondary;
        private static ControlId _pendingCell;

        /// <summary>Consecutive frames with nothing held down since the capture was asked for.
        /// </summary>
        private static int _pendingClearFrames;

        /// <summary>
        /// How many of those it takes before the keyboard changes hands.
        ///
        /// Two, not one. The frame a key comes up is a frame on which nothing is held AND
        /// <c>GetKeyUp</c> reports it - and a key coming up is precisely what the binding field treats
        /// as the end of a capture. Handing over on the first clear frame would therefore hand the
        /// field the release of the very key that asked for the capture, which is the bug this whole
        /// wait exists to avoid. One more frame and that release has been and gone.
        /// </summary>
        private const int ClearFramesBeforeCapture = 2;

        /// <summary>
        /// Ask to listen for a new combination. The prompt is spoken now; the keyboard changes hands
        /// once the player has let go of it - see <see cref="HandOverWhenReleased"/>.
        ///
        /// Everything after the hand-over belongs to the game. The field blanks itself, scans every
        /// key each frame while it holds the focus, builds the combination from up to two keys, and
        /// ends the capture on the first key RELEASE - which hands the focus back, which is what makes
        /// the field apply what was pressed, raise the "that key is already used for X" question if it
        /// has to, and write the result back into both fields.
        ///
        /// The mod's input layer stands down on its own for the duration, because the field declares
        /// itself keyboard-exclusive - and it must, or the arrow keys could never be bound to
        /// anything. This is exactly why the drop list's exemption is written as "is this the one
        /// control we handed focus to" rather than "is the focused control ours".
        ///
        /// Escape is the one key that cannot be bound here, whatever the field is told: the game
        /// takes the keyboard away from a key-exclusive control the moment an Escape-bound action
        /// fires (<c>InputManager.HandleInput</c> :1210-1226, in Update), and the field's own scan
        /// runs later in the frame. So Escape ends the capture instead of landing in it, and the
        /// field commits the nothing it was holding - a CLEAR, which is what the game's own Controls
        /// tab does too (<see cref="WatchForTheEndOfACapture"/>).
        /// </summary>
        private static void StartCapture(OptionKeyMappingItem item, bool secondary)
        {
            try
            {
                // One at a time. A second Enter while the first is still waiting to be handed over is
                // the same request again, and re-arming it would only re-say the prompt.
                if (_pending != null || !AgeWidgets.Operable(AgeWidgets.Transform(item)))
                {
                    return;
                }

                AgeControlKeyBindingField field = secondary
                    ? item.SecondaryKeyBindingField
                    : item.PrimaryKeyBindingField;
                if (field == null || AgeManager.Instance == null)
                {
                    return;
                }

                _pending = item;
                _pendingSecondary = secondary;
                _pendingCell = ModEntry.Navigator == null ? null : ModEntry.Navigator.FocusedKey;
                _pendingClearFrames = 0;

                // Said at once, and interrupting: the row has just been read and what matters now is
                // that the keyboard is about to change hands.
                Voice.Say(
                    ModStrings.Get(
                        secondary
                            ? ModStrings.NavPressSecondaryKey
                            : ModStrings.NavPressPrimaryKey
                    ),
                    true
                );
            }
            catch (Exception e)
            {
                Log.Warn("options: starting a key capture threw: " + e);
            }
        }

        /// <summary>
        /// Give the field the keyboard, but not until the player has let go of everything.
        ///
        /// This is the whole reason the hand-over is deferred at all. The field ends its capture on
        /// the FIRST key release it sees, and the key that asked for the capture - Enter, or Backspace
        /// - is still down at that moment and will come up a few frames later. Handing over
        /// immediately therefore ended the capture with whatever the player had not yet had time to
        /// press, which read as the capture dying the instant it started. Waiting for a clear keyboard
        /// costs nobody anything: the player has to let go before pressing the new combination anyway.
        ///
        /// Nothing is stood down during the wait - the field does not have the focus yet - so the
        /// player can simply arrow away instead, and the request goes with them.
        /// </summary>
        private void HandOverWhenReleased()
        {
            OptionKeyMappingItem item = _pending;
            if (item == null)
            {
                return;
            }

            // Whether the cursor is still on the very CELL that asked to capture. Moving off it is
            // the player changing their mind, and the request has to go with them or the next thing
            // they press would be bound to a key they have left - the cell next door included, since
            // the two keys of one row are two different bindings.
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null || !navigator.CursorIsOn(_pendingCell))
            {
                CancelPending();
                return;
            }

            // Spelled out: the game has its own Input in the global namespace.
            if (UnityEngine.Input.anyKey)
            {
                _pendingClearFrames = 0;
                return;
            }

            if (++_pendingClearFrames < ClearFramesBeforeCapture)
            {
                return;
            }

            try
            {
                AgeControlKeyBindingField field = _pendingSecondary
                    ? item.SecondaryKeyBindingField
                    : item.PrimaryKeyBindingField;
                AgeManager age = AgeManager.Instance;
                _pending = null;
                if (field == null || age == null)
                {
                    return;
                }

                _capturing = item;
                _capturingSecondary = _pendingSecondary;
                age.FocusedControl = field;
            }
            catch (Exception e)
            {
                _pending = null;
                Log.Warn("options: handing the keyboard to a binding field threw: " + e);
            }
        }

        private static void CancelPending()
        {
            _pending = null;
            _pendingCell = null;
            _pendingClearFrames = 0;
        }

        /// <summary>Which of the capturing row's two fields took the keyboard - what
        /// <see cref="WatchForTheEndOfACapture"/> re-reads when the capture ends.</summary>
        private static bool _capturingSecondary;

        /// <summary>
        /// A CAPTURE HAS ENDED - SAY WHAT THE CELL HOLDS NOW, WHATEVER ENDED IT.
        ///
        /// Every ending is the game's: a key released, a click elsewhere, or an Escape. Escape never
        /// reaches the field - <c>InputManager.HandleInput</c> (:1210-1226) runs in Update and nulls
        /// the focused control as soon as an Escape-bound action fires while a key-exclusive control
        /// holds the keyboard, while the field's own key scan runs in AgeManager's LateUpdate - so the
        /// field loses focus holding the nothing it blanked itself to and commits that empty
        /// combination (<c>OptionKeyMappingItem.OnLoseFocusCb</c> :80-98). Escape therefore CLEARS the
        /// cell, exactly as it does on the game's own Controls tab; Escape is simply not a bindable
        /// key, here or there (owner ruling 2026-08-24). The mod adds nothing to that - a clear
        /// through the game's own value path lights Apply and is undone by Cancel like any other
        /// change.
        ///
        /// So this watcher has one job: the ending is the moment the cell is worth reading, and it
        /// reads the same whichever key ended it.
        /// </summary>
        private void WatchForTheEndOfACapture()
        {
            OptionKeyMappingItem item = _capturing;
            if (item == null || CapturingField(item) != null)
            {
                return;
            }

            bool secondary = _capturingSecondary;
            _capturing = null;
            SayWhatStuck(item, secondary);
        }

        /// <summary>
        /// SAY WHAT THE CELL HOLDS NOW - every capture ends with this line.
        ///
        /// A capture that lands on the chord the row is ALREADY on commits nothing: the game's own
        /// lose-focus handler compares the captured combination against both of the row's slots and
        /// skips the whole commit when it matches either (<c>OptionKeyMappingItem.OnLoseFocusCb</c>
        /// :80-98). Nothing then changes, so nothing the mod watches changes either, and the capture
        /// ended in silence - which the owner read as the capture being broken (reported 2026-08-24). An
        /// Escape ending is the same story from the other side: it CLEARS the cell, and the player
        /// hears "not bound" rather than nothing. Re-reading the cell unconditionally means a player
        /// cannot tell one ending from another by listening for silence: every one says the cell.
        ///
        /// Except while a question is up. A commit that collided raises a message box - the game's
        /// own "that key is already used for X", or the mod's overlap warning - and that box is a
        /// screen of ours arriving in the same breath. The chord is read out when the box is answered
        /// instead (<see cref="ES2Access.UI.ModOptions.BindingOverlaps"/>), by which point it is
        /// settled.
        /// </summary>
        private static void SayWhatStuck(OptionKeyMappingItem item, bool secondary)
        {
            if (!Asking())
            {
                ReadCell(item, secondary);
            }
        }

        /// <summary>Whether the game is asking the player something in its message box - which is the
        /// one thing that has the floor when a capture ends.</summary>
        internal static bool Asking()
        {
            try
            {
                MessageBoxWindow box = GameWindows.Of<MessageBoxWindow>();
                return box != null && box.Shown;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Read one of a row's two cells out loud - what a settled capture ends with, and
        /// what answering a collision question ends with. QUEUED, never interrupting: the field has
        /// been saying the combination as it was pressed, and cutting that off mid-word to say the
        /// same thing again would be heard as a stutter rather than as a confirmation.</summary>
        internal static void ReadCell(OptionKeyMappingItem item, bool secondary)
        {
            try
            {
                AgeControlKeyBindingField field = secondary
                    ? item.SecondaryKeyBindingField
                    : item.PrimaryKeyBindingField;
                Voice.Say(CellText(item, field), false);
            }
            catch (Exception e)
            {
                Log.Warn("options: reading a settled binding threw: " + e);
            }
        }

        /// <summary>Which of a row's two fields is listening for keys right now, or null. Read from
        /// the game's own focus rather than from anything the mod remembers, so a capture the game
        /// ended - a key released, an Escape that took the focus away, a click elsewhere - is over
        /// here the same instant. Reading out what the cell ended up holding is the separate question
        /// (<see cref="WatchForTheEndOfACapture"/>).</summary>
        private static AgeControlKeyBindingField CapturingField(OptionKeyMappingItem item)
        {
            try
            {
                AgeManager age = AgeManager.Instance;
                AgeControl focused = age == null ? null : age.FocusedControl;
                if (focused == null)
                {
                    return null;
                }

                if (ReferenceEquals(focused, item.PrimaryKeyBindingField))
                {
                    return item.PrimaryKeyBindingField;
                }

                return ReferenceEquals(focused, item.SecondaryKeyBindingField)
                    ? item.SecondaryKeyBindingField
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Stop listening, and bind nothing - the mod is going away and the player never finished.
        ///
        /// The order matters: the fields are put back to what the option actually holds FIRST, so
        /// that handing the keyboard back cannot apply a half-pressed combination. Letting go is what
        /// makes the game apply, and it only applies what it finds different from the current
        /// binding.
        /// </summary>
        internal static void ReleaseCapture()
        {
            CancelPending();
            OptionKeyMappingItem item = _capturing;
            _capturing = null;
            if (item == null)
            {
                return;
            }

            try
            {
                AgeManager age = AgeManager.Instance;
                if (age == null || CapturingField(item) == null)
                {
                    return;
                }

                item.Refresh();
                age.FocusedControl = null;
            }
            catch (Exception e)
            {
                Log.Warn("options: abandoning a key capture threw: " + e);
            }
        }
    }
}

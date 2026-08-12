using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;
using ES2Access.UI.Input;

namespace ES2Access.Screens
{
    /// <summary>
    /// The box the game opens to type a new name into - for a system, a planet, a fleet, a hero.
    ///
    /// Everything on it is already the game's: the box hands its own text field the engine's keyboard
    /// focus the moment it opens, which is exactly the handover a mod-driven edit field has to be
    /// careful to defer, and it commits on Enter and closes. The mod's input layer stands down for a
    /// key-exclusive control, so from the frame this box appears the player is simply typing into the
    /// game, as they would be with a mouse.
    ///
    /// What is missing without a screen here is only the words: a box that opens silently is a box a
    /// blind player has fallen into. So this screen says what the box is asking, what is in the field,
    /// and that typing has begun - and then gets out of the way.
    /// </summary>
    public sealed class RenameModalScreen : Screen
    {
        public override string Key
        {
            get { return "screen.rename"; }
        }

        /// <summary>Above every page it can be opened from, and below the confirmation box, which is
        /// the one thing that can appear over it.</summary>
        public override int Layer
        {
            get { return 80; }
        }

        /// <summary>The game's own question - "Rename Dusay" - and, when it has not written one yet,
        /// the mod's word for what this box is.</summary>
        public override string ScreenName
        {
            get
            {
                RenameModalWindow window = Window();
                string title = window == null ? null : AgeText.Label(window.Title);
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenRename)
                    : title;
            }
        }

        public override bool IsActive()
        {
            RenameModalWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape is the engine's: the field it focused is key-exclusive, so the game's own
        /// input manager answers the key without the page behind it hearing a thing. What that answer
        /// leaves half-done is finished in <see cref="OnUpdate"/>.</summary>
        public override bool Back()
        {
            return false;
        }

        // Whether the field held the engine's keyboard on the previous frame, so the frame it lets go
        // can be told from all the frames after it.
        private bool _fieldHadKeyboard;

        /// <summary>
        /// The box exists to be typed into, so while it is up the field holds the engine's keyboard.
        /// The moment the field lets go, the box is over - and it is this screen that has to say so,
        /// because the engine's own answer to Escape stops halfway.
        ///
        /// What the engine does with Escape on a key-exclusive control is unfocus it and eat the key
        /// (<c>InputManager.HandleInput</c> :1212-1227). The window around the field is left standing:
        /// a box that still looks like a text prompt, no longer takes text, and cannot be typed into
        /// again, because nothing on it can hand a game field the keyboard back. That is the trap the
        /// player fell into - not a dead key layer, but a live one on a surface with nothing left to do.
        /// So the cancel is finished here, on the frame the keyboard comes back, and one Escape closes
        /// the box.
        ///
        /// Return unfocuses the field too, and means the opposite. Usually the box is already going
        /// (<c>RenameModalWindow.OnValidateCb</c> hides it), and a box on its way out is left alone.
        /// When it is NOT going, the game refused the name - and cancelling on the player's behalf
        /// would throw away what they typed, so the keyboard goes back into the field with the game's
        /// own reason for the refusal. <c>GameKeyboardHandover.TookTheValidateKey</c> is what tells the
        /// two keys apart; without it, "Escape on a name the game would refuse" and "Enter on one" look
        /// identical and one of them has to be wrong.
        ///
        /// Watched rather than hooked because there are several ways out of the field - Escape, Enter,
        /// clicking off it - and all of them mean the same thing to this box.
        /// </summary>
        public override void OnUpdate()
        {
            bool holding = FieldHasKeyboard();
            if (_fieldHadKeyboard && !holding)
            {
                FinishWhatLetGoOfTheKeyboard();
            }

            _fieldHadKeyboard = FieldHasKeyboard();
        }

        /// <summary>
        /// Only when the keyboard went NOWHERE: a control that took it from the field - a message box
        /// raised over this one, the game's own chat - is holding it for a reason of its own, and the
        /// box the player was typing into is still theirs to come back to.
        /// </summary>
        private void FinishWhatLetGoOfTheKeyboard()
        {
            try
            {
                RenameModalWindow window = Window();
                if (window == null || !window.Shown)
                {
                    return;
                }

                AgeManager age = AgeManager.Instance;
                if (age == null || age.FocusedControl != null)
                {
                    return;
                }

                AgeControlTextField field = window.TextField;
                if (GameKeyboardHandover.TookTheValidateKey(field))
                {
                    age.FocusedControl = field;
                    string refusal = Refusal(window);
                    if (!string.IsNullOrEmpty(refusal))
                    {
                        Voice.Say(refusal, true);
                    }

                    return;
                }

                // The same route the engine's own second Escape takes: GuiModalWindow.HandleInput
                // hides the window, which is what puts the page behind it back with the cursor on the
                // control that opened the box.
                window.HandleInput(InputAction.Exit);
            }
            catch (Exception)
            {
                // Nothing here is worth a throw into the pump: the worst a failure costs is the box
                // staying up for the player to press Escape again.
            }
        }

        /// <summary>The game's own words for why it will not take this name - written onto the accept
        /// button's tooltip by <c>RenameModalWindow.CheckButtons</c>, already localized.</summary>
        private static string Refusal(RenameModalWindow window)
        {
            AgeControlButton validate = window.ValidateButton;
            AgeTooltip tooltip = AgeWidgets.Raw(AgeWidgets.Transform(validate));
            return tooltip == null ? null : AgeText.Clean(tooltip.Content);
        }

        /// <summary>Focus lands on the field with the box, so the first frame is already "holding" and
        /// the arrival is not heard as a hand-back.</summary>
        public override void OnPush()
        {
            _fieldHadKeyboard = FieldHasKeyboard();
        }

        private static bool FieldHasKeyboard()
        {
            try
            {
                RenameModalWindow window = Window();
                AgeControlTextField field = window == null ? null : window.TextField;
                AgeManager age = AgeManager.Instance;
                return field != null && age != null && ReferenceEquals(age.FocusedControl, field);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The box exists to be typed into: every letter is the name being written, and
        /// none of them is a search over the two controls here.</summary>
        public override bool AllowsTypeahead
        {
            get { return false; }
        }

        /// <summary>
        /// The field, and - only while the keys could actually reach them - the heading above it and the
        /// button that accepts it.
        ///
        /// While the field holds the engine's keyboard the arrows are the caret's and Tab is eaten by
        /// the game, so nothing on this box is reachable but the field itself. Declaring three stops
        /// there is a promise the keys cannot keep: the field announced itself as "2 of 3" and the
        /// player pressed the arrows looking for the other two. So the box declares what it can be
        /// walked over, which while the player is typing is one stop and no position at all.
        /// </summary>
        public override void Build(GraphBuilder builder)
        {
            RenameModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            bool walkable = !FieldHasKeyboard();

            // The box's own heading, where it is drawn: a node to come back to as well as the name
            // arriving announces. Focus starts on the field below it, so it is not said twice.
            if (walkable)
            {
                SettingRows.AddReadout(
                    builder,
                    SettingRows.TransformOf(window.Title),
                    "rename:heading"
                );
            }

            RenameModalWindow it = window;
            AgeControlTextField field = window.TextField;
            if (field != null)
            {
                NodeVtable vtable = new NodeVtable
                {
                    ControlType = ControlTypes.EditField,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => ModStrings.Get(ModStrings.RenameTypePrompt)),
                        GraphNodes.ValuePart(() => AgeText.Label(it.TextField.Label)),
                    },
                };
                ControlId id = ControlId.Referenced(field, "rename:field");
                builder.AddItem(id, vtable);
                builder.SetStart(id);
            }

            AgeControlButton validate = window.ValidateButton;
            if (walkable && validate != null && AgeWidgets.Visible(AgeWidgets.Transform(validate)))
            {
                AgeControlButton press = validate;
                AgeTooltip tooltip = AgeWidgets.Raw(AgeWidgets.Transform(validate));
                NodeVtable vtable = GraphNodes.Button(
                    () => ModStrings.Get(ModStrings.RenameConfirm),
                    () => AgeWidgets.Press(press),
                    () => press.Enable,
                    tooltip
                );
                AgeWidgets.Point(vtable, press);
                builder.AddItem(ControlId.Referenced(validate, "rename:confirm"), vtable);
            }
        }

        private static RenameModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<RenameModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

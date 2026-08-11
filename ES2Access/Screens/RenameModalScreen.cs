using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

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
        /// input manager answers the key and takes the box down without the page behind it hearing a
        /// thing. It takes TWO presses - see <see cref="OnUpdate"/>.</summary>
        public override bool Back()
        {
            return false;
        }

        // Whether the field held the engine's keyboard on the previous frame, so the frame it lets go
        // can be told from all the frames after it.
        private bool _fieldHadKeyboard;

        /// <summary>
        /// Escape here takes two presses and the first one changes nothing anybody can see: the game's
        /// input manager clears the engine's focus and consumes the key, so the field stops taking
        /// letters while the box stays up. Silence at that moment reads as the box having gone - the
        /// player types the next name into the page behind it - so the hand-back is said.
        ///
        /// Watched rather than hooked because there are several ways out of the field (Escape, Enter,
        /// clicking off it) and only one thing worth saying: the keyboard came back.
        /// </summary>
        public override void OnUpdate()
        {
            bool holding = FieldHasKeyboard();
            if (_fieldHadKeyboard && !holding)
            {
                Voice.Say(ModStrings.Get(ModStrings.RenameKeyboardReturned), true);
            }

            _fieldHadKeyboard = holding;
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
        /// The field, and the button that accepts it. The field is where focus lands and it reads the
        /// name that is in it - which is the name being changed, because the game fills it in - with
        /// the prompt that says the keyboard is already in the box.
        /// </summary>
        public override void Build(GraphBuilder builder)
        {
            RenameModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            // The box's own heading, where it is drawn: a node to come back to as well as the name
            // arriving announces. Focus starts on the field below it, so it is not said twice.
            SettingRows.AddReadout(
                builder,
                SettingRows.TransformOf(window.Title),
                "rename:heading"
            );

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
            if (validate != null && AgeWidgets.Visible(AgeWidgets.Transform(validate)))
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

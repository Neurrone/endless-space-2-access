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
    /// The box hands its own text field the engine's keyboard the moment it opens
    /// (<c>RenameModalWindow.OnBeginShow</c>), which drops the player into a caret with no chance to
    /// hear what the box is asking, what is already in the field or that there is a Confirm button at
    /// all. So the keyboard is taken back on the way in and the box is walked like any other page:
    /// the heading, the field, then the two buttons along the bottom - Cancel and Confirm, in the order
    /// they are drawn. Entering the field is then the player's own decision - Enter
    /// on it, the same activation every other edit field in the mod takes (<see cref="SettingRows"/>) -
    /// and Escape out of it is a step back onto the field rather than out of the box.
    ///
    /// Everything about the edit itself - the words on the way in, the typing, the two ways out and the
    /// text a cancel puts back - is <see cref="TextFieldEditor"/>'s, shared with every other text box in
    /// the game, and so is the commit: Enter ends the EDIT and leaves the box standing on its field
    /// (<see cref="TextEditOptions.OwnCommit"/>). Renaming is what the Confirm button does, and a name
    /// the game will not take is refused there, with the game's own words on that button.
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

        /// <summary>
        /// Escape is the engine's, and it means a different thing at each of the box's two depths -
        /// which is exactly what the engine already does with it. While the field holds the keyboard,
        /// <c>InputManager.HandleInput</c> (:1212-1227) unfocuses the field and eats the key: the edit
        /// ends and the box stays. With nothing focused the same key reaches the modal window itself,
        /// whose <c>HandleInput(Exit)</c> hides it: the box is cancelled. Neither needs the mod, so the
        /// mod claims nothing; what the first of them leaves unsaid is said in <see cref="OnUpdate"/>.
        /// </summary>
        public override bool Back()
        {
            return false;
        }

        /// <summary>The deferred hand-over of the keyboard to the game's field, for the reason the
        /// editor documents: giving it the keyboard on the frame Enter went down gives it that very
        /// Enter, which is its validate.</summary>
        private readonly TextFieldEditor _editor = new TextFieldEditor();

        /// <summary>False while the field has been asked for and the keyboard has not changed hands
        /// yet: what the player types next belongs in the field, not in a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        // How long the box has been the mod's, in frames. Only the first few are counted, and only to
        // bound the take-back below.
        private int _framesOpen;

        public override void OnUpdate()
        {
            TakeBackTheOpeningFocus();
            _editor.Update();
        }

        /// <summary>
        /// Take the keyboard back off the field the box focused for itself.
        ///
        /// Entering an edit field is a decision, not something a player should arrive already inside:
        /// a box that opens straight into a caret cannot be read, and its Confirm button might as well
        /// not exist. The game focuses the field in <c>OnBeginShow</c>, which runs before this screen
        /// is pushed - the push waits on <c>IsReady</c> - but that ordering is the engine's business
        /// rather than a promise, so the take-back is attempted over the box's first frames instead of
        /// exactly once. It stops there: after that a field holding the keyboard is a field somebody
        /// asked for, whether with Enter here or with a mouse.
        ///
        /// The Return that OPENED the box is safe meanwhile: <c>GameKeyboardHandover</c> keeps the key
        /// the mod already spent from reaching the field at all.
        /// </summary>
        private void TakeBackTheOpeningFocus()
        {
            if (_framesOpen > OpeningFrames)
            {
                return;
            }

            _framesOpen++;
            if (_editor.Pending || !FieldHasKeyboard())
            {
                return;
            }

            try
            {
                // The mod taking back what the box focused for itself, not the player leaving an edit:
                // nothing is put back and nothing is said.
                TextFieldEditor.Abandon();
                AgeManager.Instance.FocusedControl = null;
            }
            catch (Exception)
            {
                // A box left in the game's own hands: the player types into it as they always could,
                // which is worse than this screen intends and better than a throw into the pump.
            }
        }

        private const int OpeningFrames = 3;

        public override void OnPush()
        {
            _editor.Cancel();
            _framesOpen = 0;
            TakeBackTheOpeningFocus();
        }

        public override void OnPop()
        {
            _editor.Cancel();
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

        /// <summary>
        /// The field, and - only while the keys could actually reach them - the heading above it and the
        /// two buttons below it, the one that accepts the name and the one that abandons it.
        ///
        /// While the field holds the engine's keyboard the arrows are the caret's and Tab is eaten by
        /// the game, so nothing on this box is reachable but the field itself. Declaring the other stops
        /// there is a promise the keys cannot keep: the field announced itself as "2 of 4" and the
        /// player pressed the arrows looking for the other three. So the box declares what it can be
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

            AgeControlTextField field = window.TextField;
            if (field != null)
            {
                // Declared by the same reader as every other text box the game draws, so that the box
                // this mod invented least reads exactly like the ones it did not: an edit field, the
                // name that is in it, and the keyboard handed over only when it is activated.
                ControlId id = ControlId.For(field, "rename:field");
                Cell cell = SettingRows.TextFieldCell(
                    field,
                    null,
                    AgeWidgets.Raw(AgeWidgets.Transform(field)),
                    null,
                    null,
                    id,
                    _editor
                );
                if (cell != null)
                {
                    builder.AddItem(Nodes.Drawn(cell.Id, cell.Vtable, cell.Widget));
                    builder.SetStart(cell.Id);
                }
            }

            if (!walkable)
            {
                return;
            }

            // The two buttons along the bottom edge, one per row in the order the box draws them:
            // Cancel at the left and Confirm at the right (measured: x 340 and x 856, both at y 424, of
            // a 600-wide box). They are a window's bottom bar - two answers of one kind - and such a
            // band is walked with one key, because a sideways move buys nothing a step down does not
            // and the line they landed on is the layout's business. Cancel is the button the window
            // keeps no field for, so it is found by the name the prefab gives it.
            _buttons.Clear();
            AddButton(
                AgeWidgets.Button(AgeWidgets.ChildNamed(window.AgeTransform, "CancelButton", 3)),
                "rename:cancel"
            );
            AddButton(window.ValidateButton, "rename:confirm");
            Cells.EmitLinear(builder, _buttons);
        }

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<Cell> _buttons = new List<Cell>();

        /// <summary>
        /// One of the two buttons along the bottom of the box, pressed the way a click presses it - which
        /// for Cancel is the window's own <c>OnCancelCb</c>, the same hide that Escape reaches
        /// (<c>GuiModalWindow.OnCancelCb</c> :102-105), so leaving by it throws the typed name away
        /// exactly as the game would.
        ///
        /// Named by the game's own caption, for both of them: the prefab writes each as a localization
        /// key the game resolves (<c>%MessageBoxCancelTitle</c>, <c>%MessageBoxValidateTitle</c>), so
        /// neither button is named in English by this mod and both are named in the same voice.
        /// </summary>
        private void AddButton(AgeControlButton button, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (button == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlButton press = button;
            AgeTransform at = widget;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TextOf(at),
                () => AgeWidgets.Press(press),
                () => press.Enable,
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.Point(vtable, press);
            Cells.Add(_buttons, widget, ControlId.For(button, key), vtable);
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

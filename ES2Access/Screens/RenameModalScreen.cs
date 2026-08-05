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
        /// thing.</summary>
        public override bool Back()
        {
            return false;
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
                builder.AddItem(ControlId.Referenced(field, "rename:field"), vtable);
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
                    tooltip,
                    GraphNodes.ModeFor(tooltip)
                );
                vtable.DetailLines = AgeWidgets.TooltipLines(tooltip);
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

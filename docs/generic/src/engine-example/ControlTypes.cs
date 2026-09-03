using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// The mod's registry of control types. A type is a value, not a class: a node factory points at
    /// one, and the type supplies the localized role word every control of that kind speaks plus the
    /// order its announcement parts read in.
    ///
    /// One order serves every type here - label, then role, then value, selection and enabled state,
    /// then what the tooltip has to say, and the list position last - because that is the order a
    /// screen reader user expects to hear a control in, whatever the control is. What a control says
    /// about the KEYBOARD rather than about itself is after even that, and is not in the order at all:
    /// the drag words and the usage hints (<see cref="AnnouncementKinds.Hint"/>) are kinds no type
    /// orders, which is what keeps them in the trailing bucket behind the position.
    /// </summary>
    public static class ControlTypes
    {
        private static readonly string[] StandardOrder =
        {
            AnnouncementKinds.Label,
            AnnouncementKinds.Role,
            AnnouncementKinds.Value,
            AnnouncementKinds.Selected,
            AnnouncementKinds.Enabled,
            AnnouncementKinds.Tooltip,
            AnnouncementKinds.Position,
        };

        /// <summary>Anything the player activates to make something happen.</summary>
        public static readonly ControlType Button = new ControlType
        {
            Key = "button",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.ControlButton),
        };

        /// <summary>A container the player opens and closes. It has no role word of its own beyond
        /// "group": the announcer already appends its expanded or collapsed state.</summary>
        public static readonly ControlType Group = new ControlType
        {
            Key = "group",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.ControlGroup),
        };

        /// <summary>One page of a screen, reached from a bar of its peers. What matters about a tab
        /// is whether it is the page currently showing, which it says as its selection state.</summary>
        public static readonly ControlType Tab = new ControlType
        {
            Key = "tab",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.ControlTab),
        };

        /// <summary>A setting the player turns on and off, reading its state every time it is
        /// touched.</summary>
        public static readonly ControlType Checkbox = new ControlType
        {
            Key = "checkbox",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.ControlCheckbox),
        };

        /// <summary>One of a set the game lets the player choose exactly one of, in place on the page
        /// rather than in a list something opened. It is not a checkbox: there is no untick, and a
        /// player told "checkbox, not checked" would go looking for one.</summary>
        public static readonly ControlType RadioButton = new ControlType
        {
            Key = "radio-button",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.ControlRadioButton),
        };

        /// <summary>A value along a range. Left and right move it rather than moving the cursor, so
        /// its role word is also the warning that the arrows mean something else here.</summary>
        public static readonly ControlType Slider = new ControlType
        {
            Key = "slider",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.ControlSlider),
        };

        /// <summary>A setting chosen from a list the control opens.</summary>
        public static readonly ControlType ComboBox = new ControlType
        {
            Key = "combo-box",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.ControlComboBox),
        };

        /// <summary>One line of a menu the player has opened to choose an action from. It says so on
        /// every entry, unlike the entries of a value list: a menu is somewhere the player has been
        /// taken, and the role word is what tells them the keyboard has moved somewhere new. (WotR's
        /// port of this pattern reuses the value-list entry and says nothing; hearing "menu item" is
        /// the screen-reader convention and was chosen over matching it.)</summary>
        public static readonly ControlType MenuItem = new ControlType
        {
            Key = "menu-item",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.ControlMenuItem),
        };

        /// <summary>A cell of a table: something the player reads but cannot work, and the one type
        /// with NO role word. A table is already announced as a table on the way in, and a row of
        /// figures that said "text" fourteen times would say nothing else. What the type is for is the
        /// reading ORDER - value, then the row's selection and refusal states, then what the column's
        /// tooltip has to say.</summary>
        public static readonly ControlType Text = new ControlType
        {
            Key = "text",
            Order = StandardOrder,
        };

        /// <summary>Free text the player types into, worked through the game's own editor rather than
        /// through this mod - activating it is what hands the game's keyboard focus to the field.
        /// </summary>
        public static readonly ControlType EditField = new ControlType
        {
            Key = "edit-field",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.ControlEditField),
        };

        /// <summary>A box that holds a number and carries the game's own stepper beside it: Left and
        /// Right change the value rather than walk a caret, and Enter still opens the same editor.
        /// Its own role word, because the arrows meaning two different things on two boxes that
        /// otherwise sound identical is exactly the thing a player has to be told before they
        /// press.</summary>
        public static readonly ControlType NumericEditField = new ControlType
        {
            Key = "numeric-edit-field",
            Order = StandardOrder,
            Common = () => RoleWord(ModStrings.ControlNumericEditField),
        };

        private static IList<NodeAnnouncement> RoleWord(string stringKey)
        {
            return new[]
            {
                new NodeAnnouncement(
                    () => ModStrings.Get(stringKey),
                    kind: AnnouncementKinds.Role
                ),
            };
        }
    }
}

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
    /// screen reader user expects to hear a control in, whatever the control is.
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

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

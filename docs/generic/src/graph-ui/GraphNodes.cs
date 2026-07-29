using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// Factories for the control descriptions screens hand to the graph builder. A screen says what
    /// a control is and how to work it; everything about how that reads aloud lives here, so two
    /// screens with a button announce it identically.
    ///
    /// Every piece of text is a delegate, resolved at speak time, never a captured string: a graph is
    /// rebuilt from live game state on every operation, and a control that cached its label would go
    /// on announcing the state the game was in when the screen was first built.
    /// </summary>
    public static class GraphNodes
    {
        /// <summary>The control's name - always the first part, so the path diff can tell when a
        /// container's label merely repeats the control inside it.</summary>
        public static NodeAnnouncement LabelPart(Func<string> label)
        {
            return new NodeAnnouncement(label, kind: AnnouncementKinds.Label);
        }

        /// <summary>Speaks only while the control is unavailable, and watched live so a control that
        /// becomes available under the cursor says so. The game's own reason for the refusal is not
        /// repeated here.</summary>
        public static NodeAnnouncement DisabledPart(Func<bool> enabled)
        {
            return new NodeAnnouncement(
                () =>
                    enabled == null || enabled()
                        ? null
                        : ModStrings.Get(ModStrings.NavDisabled),
                live: true,
                kind: AnnouncementKinds.Enabled
            );
        }

        /// <summary>The control's review-buffer content, read from its tooltip: the game has already
        /// put the description there, and for a control that is refusing, the reason it is refusing.
        /// Resolved at review time, so the reason a button gives is the one it would give now.</summary>
        public static Func<IList<string>> TooltipDetails(AgeTooltip tooltip)
        {
            if (tooltip == null)
            {
                return null;
            }

            return () => AgeText.Lines(AgeText.Tooltip(tooltip));
        }

        /// <summary>The same, for a control that carries its tooltip on its transform rather than
        /// naming it in a field.</summary>
        public static Func<IList<string>> TooltipDetails(AgeTransform transform)
        {
            return transform == null ? null : TooltipDetails(transform.AgeTooltip);
        }

        /// <summary>What, if anything, the tooltip contributes to the focus readout - the screen's
        /// call, per control. Null when the control has no tooltip or the screen wants it read only
        /// from the buffer.</summary>
        public static NodeAnnouncement TooltipPart(TooltipMode mode, AgeTooltip tooltip)
        {
            return TooltipParts.Part(mode, TooltipDetails(tooltip));
        }

        /// <summary>A control the player activates. An unavailable one stays focusable and readable -
        /// knowing that Join Game exists but is out of reach is the point - and simply swallows the
        /// activation.</summary>
        public static NodeVtable Button(
            Func<string> label,
            Action activate,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            TooltipMode tooltipMode = TooltipMode.None
        )
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = Parts(label, enabled, tooltip, tooltipMode),
                DetailLines = TooltipDetails(tooltip),
                OnActivate = () =>
                {
                    if (enabled != null && !enabled())
                    {
                        return;
                    }

                    if (activate != null)
                    {
                        activate();
                    }
                },
            };
        }

        /// <summary>A container the player expands and collapses. Declare it with the builder's
        /// BeginGroup, which stamps the expanded state and parents the children onto it.</summary>
        public static NodeVtable Group(
            Func<string> label,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            TooltipMode tooltipMode = TooltipMode.None
        )
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Group,
                Announcements = Parts(label, enabled, tooltip, tooltipMode),
                DetailLines = TooltipDetails(tooltip),
            };
        }

        // The readout every control here is built from: what it is called, whether it is refusing,
        // and as much of its tooltip as the screen asked to be spoken.
        private static List<NodeAnnouncement> Parts(
            Func<string> label,
            Func<bool> enabled,
            AgeTooltip tooltip,
            TooltipMode tooltipMode
        )
        {
            List<NodeAnnouncement> parts = new List<NodeAnnouncement>
            {
                LabelPart(label),
                DisabledPart(enabled),
            };

            NodeAnnouncement tooltipPart = TooltipPart(tooltipMode, tooltip);
            if (tooltipPart != null)
            {
                parts.Add(tooltipPart);
            }

            return parts;
        }
    }
}

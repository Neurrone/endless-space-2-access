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

        /// <summary>Speaks only while the control is the chosen one among its peers - the showing tab,
        /// the entry a list is currently set to. Saying nothing when unselected is load-bearing: it is
        /// what lets focus entering a stop land on the choice already in force rather than at the top
        /// of the list.</summary>
        public static NodeAnnouncement SelectedPart(Func<bool> selected)
        {
            return new NodeAnnouncement(
                () =>
                    selected != null && selected()
                        ? ModStrings.Get(ModStrings.NavSelected)
                        : null,
                live: true,
                kind: AnnouncementKinds.Selected
            );
        }

        /// <summary>What the control currently holds. Watched live, so a value the game changes on
        /// its own - a setting another control has just constrained, a volume the game clamped -
        /// speaks under the cursor without the whole control being re-read.</summary>
        public static NodeAnnouncement ValuePart(Func<string> value)
        {
            return new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value);
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
                OnActivate = Guarded(activate, enabled),
            };
        }

        /// <summary>A setting the player turns on and off. Its state is both announced live - so a
        /// box the game ticks on the player's behalf says so - and spoken immediately after a
        /// toggle, which is what makes holding the key down readable.</summary>
        public static NodeVtable Checkbox(
            Func<string> label,
            Func<bool> state,
            Action toggle,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            TooltipMode tooltipMode = TooltipMode.None
        )
        {
            Func<string> stateText = () =>
                ModStrings.Get(
                    state != null && state() ? ModStrings.NavChecked : ModStrings.NavUnchecked
                );

            List<NodeAnnouncement> parts = Parts(label, enabled, tooltip, tooltipMode);
            parts.Add(ValuePart(stateText));
            return new NodeVtable
            {
                ControlType = ControlTypes.Checkbox,
                Announcements = parts,
                DetailLines = TooltipDetails(tooltip),
                StateText = stateText,
                OnActivate = Guarded(toggle, enabled),
            };
        }

        /// <summary>A value the player moves along a range with left and right, and by a coarse step
        /// with the same arrows held with Shift. <paramref name="valueText"/> is already in the form the player
        /// should hear it - percent, decibels, a count - because only the screen knows what the
        /// number means.</summary>
        public static NodeVtable Slider(
            Func<string> label,
            Func<string> valueText,
            Action<int, bool> adjust,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            TooltipMode tooltipMode = TooltipMode.None
        )
        {
            List<NodeAnnouncement> parts = Parts(label, enabled, tooltip, tooltipMode);
            parts.Add(ValuePart(valueText));
            return new NodeVtable
            {
                ControlType = ControlTypes.Slider,
                Announcements = parts,
                DetailLines = TooltipDetails(tooltip),
                StateText = valueText,
                // Declared even when the slider is refusing, so left and right stay the slider's
                // keys rather than quietly turning back into navigation on a control that looks
                // exactly like the one beside it.
                OnAdjust = (sign, large) =>
                {
                    if (enabled != null && !enabled())
                    {
                        return;
                    }

                    if (adjust != null)
                    {
                        adjust(sign, large);
                    }
                },
            };
        }

        /// <summary>A setting chosen from a list the control opens. Activating it is the screen's
        /// business - what the list is and how it is navigated belongs to whoever declared it.</summary>
        public static NodeVtable ComboBox(
            Func<string> label,
            Func<string> valueText,
            Action open,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            TooltipMode tooltipMode = TooltipMode.None
        )
        {
            List<NodeAnnouncement> parts = Parts(label, enabled, tooltip, tooltipMode);
            parts.Add(ValuePart(valueText));
            return new NodeVtable
            {
                ControlType = ControlTypes.ComboBox,
                Announcements = parts,
                DetailLines = TooltipDetails(tooltip),
                StateText = valueText,
                OnActivate = Guarded(open, enabled),
            };
        }

        /// <summary>One page of a screen. Only the showing tab says it is selected, and saying
        /// nothing is how the rest stay quiet - which is also what lets focus entering the tab bar
        /// land on the page the player is actually looking at rather than on the first tab.
        ///
        /// How a tab is switched to is the screen's business: set <c>OnActivate</c> on the returned
        /// vtable if the game needs a click, leave it unset for a bar that changes page on focus.</summary>
        public static NodeVtable Tab(
            Func<string> label,
            Func<bool> selected,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            TooltipMode tooltipMode = TooltipMode.None
        )
        {
            List<NodeAnnouncement> parts = Parts(label, enabled, tooltip, tooltipMode);
            parts.Add(SelectedPart(selected));
            return new NodeVtable
            {
                ControlType = ControlTypes.Tab,
                Announcements = parts,
                DetailLines = TooltipDetails(tooltip),
            };
        }

        /// <summary>One entry of a list the player has opened to pick from. It carries no role word:
        /// the control that opened the list has just been read as the combo box it is, and repeating
        /// "list item" on every entry of a twenty-line resolution list only slows the reading down.
        /// The entry the list is currently set to says so, which is also how focus lands on it.</summary>
        public static NodeVtable Choice(
            Func<string> label,
            Func<bool> selected,
            Action choose,
            Func<bool> enabled = null,
            Func<IList<string>> details = null
        )
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    LabelPart(label),
                    SelectedPart(selected),
                    DisabledPart(enabled),
                },
                DetailLines = details,
                OnActivate = Guarded(choose, enabled),
            };
        }

        // The swallow every unavailable control shares: it stays focusable and readable, and the
        // action goes nowhere.
        private static Action Guarded(Action action, Func<bool> enabled)
        {
            return () =>
            {
                if (enabled != null && !enabled())
                {
                    return;
                }

                if (action != null)
                {
                    action();
                }
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

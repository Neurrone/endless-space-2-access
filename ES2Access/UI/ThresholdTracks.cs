using System.Collections.Generic;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// The circles strung along a filling track - the shape the game draws wherever a stock climbs
    /// towards a series of marks and the player has to know which mark this is, how near it is, and
    /// what reaching it buys: the population overview's collection track, the pirate window's
    /// additional-firepower track.
    ///
    /// Every one of them is the <c>ThresholdItem</c> prefab, and reading one is the same three facts
    /// each time. The explanation hangs on the Circle CHILD, so a node aimed at the item around it
    /// draws no tooltip at all (measured on the pirate track, 2026-08-30: aimed at the item, the
    /// tooltip request never resolved). That explanation is the item's own <c>CircleTooltip</c>
    /// field, which for a CLASS-backed tooltip is not reachable from the widget by any other route.
    /// And what it holds is a list of effects, which belongs in the review buffer rather than in the
    /// announcement.
    ///
    /// What a mark SAYS is the caller's, because only the screen knows what the track counts and what
    /// arithmetic decides "reached" - and the drawn circle cannot be asked either way: the game shows
    /// the answer by fading the circle's alpha, and each screen re-writes the number on it to mean
    /// something of its own (the population track draws the threshold, the pirate window overwrites it
    /// with the mark's ordinal).
    /// </summary>
    public static class ThresholdTracks
    {
        /// <summary>
        /// One mark on such a track: the words the caller composed for it, with the circle's own
        /// explanation left in the buffer and the pointer aimed where that explanation will draw.
        ///
        /// Nothing is added for a mark the game is not drawing, nor for a circle carrying no
        /// explanation at all - a bare number with no sentence behind it is a step past nothing. The
        /// cell is laid on the CIRCLE, where the game draws the mark, while the id is keyed to the
        /// ITEM, which is what the table pools and re-binds.
        /// </summary>
        public static void Add(List<Cell> cells, AgeTransform widget, string words, string key)
        {
            // Banding input: Cells.Add takes the mark without asking the gate, and the circles are
            // worked into a row by where they are drawn along the track.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            ThresholdItem item = widget.GetComponent<ThresholdItem>();
            AgeTransform circle = AgeWidgets.ChildNamed(widget, "Circle", 2) ?? widget;
            AgeTooltip tooltip =
                item != null && item.CircleTooltip != null
                    ? item.CircleTooltip
                    : AgeWidgets.Raw(circle);
            if (tooltip == null)
            {
                return;
            }

            string said = words;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => said),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, circle);
            Cells.Add(cells, circle, ControlId.For(widget, key), vtable);
        }
    }
}

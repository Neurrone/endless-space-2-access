using System.Collections.Generic;

namespace ES2Access.Core.UI.Graph
{
    /// <summary>
    /// Whether a node that wrote a tooltip's words down ever gave the player a way to see them.
    ///
    /// A tooltip is TWO promises. Declaring a section says the words are REVIEWABLE; aiming the node
    /// (<see cref="NodeVtable.PointsAt"/>) says WHICH dossier it shows; and moving the pointer on focus
    /// (<see cref="NodeVtable.OnFocusVisual"/>) is what makes the game DRAW its own tooltip, because a
    /// game draws tooltips on hover and nothing else. <see cref="TooltipKindRule"/> is the same kind of
    /// reading-back for the words; this one is for the pointer.
    ///
    /// The shape it names is the one that hid a live defect behind a clean audit: a node that declares
    /// tooltip-derived sections and does NEITHER. It falls through every other check - the aim buckets
    /// have no aim to judge, the raising bucket asks about a node that HAS one - and reads as a node
    /// with no tooltip at all, while the buffer promises words the game will never draw (measured on
    /// the load/save window's Steam-Cloud box, 2026-08-28, where the whole screen answered
    /// <c>clean:true</c>).
    ///
    /// It is a rule rather than an inline test so it can be proved off the engine, and it takes the two
    /// promises as plain booleans so it never has to know what a widget is.
    /// </summary>
    public static class TooltipAimRule
    {
        /// <summary>
        /// True when these sections carry a TOOLTIP's words and nothing about the node would ever raise
        /// it.
        ///
        /// <paramref name="aims"/> is "the node names which tooltip it shows"; <paramref name="raises"/>
        /// is "focusing it moves the pointer". Either one is enough to keep the node out of here:
        /// aiming without raising is its own finding, and raising without aiming still draws the
        /// tooltip for the player.
        ///
        /// Sections the MOD composed, and content the control draws, are not tooltips and carry no such
        /// promise - a node whose only sections are those is complete without a pointer anywhere.
        /// </summary>
        public static bool Unraisable(IList<NodeSection> sections, bool aims, bool raises)
        {
            if (aims || raises)
            {
                return false;
            }

            for (int i = 0; sections != null && i < sections.Count; i++)
            {
                if (sections[i] != null && sections[i].FromTooltip)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

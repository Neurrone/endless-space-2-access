using System;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// The game's <c>PopulationCount</c> entry - one kind of person living somewhere, drawn as a symbol
    /// with a number beside it - wherever a panel lists them.
    ///
    /// Distinct from <see cref="PopulationRings"/>, which is the ring of SLOTS on a planet card: this is
    /// the breakdown a side panel writes, on the planet overview page and on the star system page's
    /// outpost and colony panels, and both of those read it the same way.
    /// </summary>
    public static class PopulationRows
    {
        /// <summary>
        /// One kind of person living here.
        ///
        /// The entry draws their symbol and how many of them there are and never writes what they are
        /// called; the game keeps that name on the wrapper hung on the tooltip - which is on the SYMBOL
        /// inside the entry and not on the entry itself, so a consumer aims the pointer at the tooltip
        /// rather than at the row (measured: the row carries no tooltip of its own, and pointing at it
        /// left the row's review buffer with the dossier nowhere).
        ///
        /// A BUTTON, not a readout: the entry's own click opens the empire's population window
        /// (<c>PopulationCount.OnClickCb</c>), which is the only route into what these people are beyond
        /// the sentence on their symbol - and it is the same window the senate's census button opens
        /// (<see cref="Screens.PopulationScreen"/>).
        ///
        /// <paramref name="tooltip"/> is passed rather than read off the entry because a host may filter
        /// out a prefab stub before handing it over.
        /// </summary>
        public static NodeVtable Count(AgeTransform widget, PopulationCount unit, AgeTooltip tooltip)
        {
            AgeTransform at = widget;
            AgePrimitiveLabel count = unit.Count;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TooltipTitle(tooltip),
                () => AgeWidgets.Press(at),
                () => AgeWidgets.Operable(at),
                tooltip
            );
            vtable.Announcements.Insert(1, GraphNodes.ValuePart(() => AgeText.Label(count)));
            return vtable;
        }
    }
}

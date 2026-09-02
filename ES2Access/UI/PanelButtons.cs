using System;
using System.Collections.Generic;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// The buttons a PREFAB wires along a panel or a card, named by the mod rather than by anything
    /// drawn on them.
    ///
    /// A toolbar of icons is the one kind of control the shape walk cannot read: the game draws a
    /// symbol and writes no caption, and the only name it has is the localization key the prefab's own
    /// panel class uses for its tooltip. So the host names each one and this builds the row - one
    /// place, because the ship toolbar and the hero card were building it two ways and had drifted on
    /// which button is declared at all.
    /// </summary>
    public static class PanelButtons
    {
        /// <summary>
        /// One wired button as a row, or nothing where the prefab draws none.
        ///
        /// A button the game has DISABLED is still declared: it is drawn, so the player meets it where
        /// a sighted player does, and what it answers is "unavailable" plus the game's own sentence for
        /// why (<see cref="GraphNodes.AddRefusal"/>). Existence belongs to the node gate, which already
        /// asks whether the game is drawing the button; nothing here asks it again (owner ruling,
        /// replacing an operable gate that dropped a refused row and its reason with it).
        ///
        /// <paramref name="propagating"/> is for a button INSIDE something that is itself a control - a
        /// hero card's own toggle - where a click does the button's work AND the container's, and a
        /// keyboard press that skipped the second half would leave the page half-acted
        /// (<see cref="AgeWidgets.PressPropagating"/>).
        /// </summary>
        public static Cell Add(
            List<Cell> cells,
            AgeControlButton button,
            string titleKey,
            string keyPrefix,
            string key,
            bool propagating
        )
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (widget == null)
            {
                return null;
            }

            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Operable(AgeWidgets.Transform(it));
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Title(titleKey),
                propagating
                    ? (Action)(() => AgeWidgets.PressPropagating(it))
                    : (Action)(() => AgeWidgets.Press(it)),
                enabled,
                tooltip
            );
            GraphNodes.AddRefusal(vtable, tooltip, enabled);
            AgeWidgets.Point(vtable, it);
            return Cells.Add(cells, widget, ControlId.For(button, keyPrefix + "/" + key), vtable);
        }
    }
}

using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>One control a screen has decided to declare, held with the widget it was read off so
    /// the whole set can be ordered the way the game drew it rather than the way it was collected.
    /// </summary>
    public sealed class Cell
    {
        public AgeTransform Widget;
        public ControlId Id;
        public NodeVtable Vtable;
    }

    /// <summary>
    /// Declaring a panel's controls in the rows the game draws them in.
    ///
    /// Collection order is whatever the reading code happened to walk; the player's order is the one on
    /// screen. Gathering cells and emitting them through <see cref="AgeLayout.Rows"/> means a strip the
    /// engine wrapped onto a second line is walked as two lines with nothing being told, and a screen
    /// that lists its buttons in a different order from the prefab still reads left to right.
    /// </summary>
    public static class Cells
    {
        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

        public static void Add(
            List<Cell> cells,
            AgeTransform widget,
            ControlId id,
            NodeVtable vtable
        )
        {
            cells.Add(new Cell { Widget = widget, Id = id, Vtable = vtable });
        }

        public static void Emit(GraphBuilder builder, List<Cell> cells)
        {
            foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
            {
                builder.StartRow();
                foreach (Cell cell in row)
                {
                    builder.AddItem(cell.Id, cell.Vtable);
                }

                builder.EndRow();
            }
        }

        /// <summary>A control the game drew, activated the way a mouse activates it - the shape every
        /// page's plain buttons take, so a screen that has nothing to say about a button beyond "the
        /// game drew it here" says exactly that.</summary>
        public static Cell Control(
            AgeTransform widget,
            AgeControlButton button,
            AgeTooltip tooltip,
            string text,
            string key
        )
        {
            AgeControlButton it = button;
            AgeTransform at = widget;
            // A control the game draws as a bare icon has no caption of its own; the sentence it
            // explains itself with on hover is what a sighted player reads, so it is the name here too
            // - and then the tooltip must not be announced as well, or the control says the same
            // sentence twice. The buffer still holds all of it.
            bool named = !string.IsNullOrEmpty(text);
            string caption = named ? text : CardActions.FirstLine(tooltip);
            NodeVtable vtable = GraphNodes.Button(
                () => caption,
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(at),
                tooltip,
                named ? GraphNodes.ModeFor(tooltip) : TooltipMode.None
            );
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, key),
                Vtable = vtable,
            };
        }

        /// <summary>The drawn control at <paramref name="widget"/>, if there is one to declare - a
        /// widget the game is not drawing, or one with no button on it, contributes nothing.</summary>
        public static void AddControl(List<Cell> cells, AgeTransform widget, string key)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlButton button = AgeWidgets.Button(widget);
            if (button != null)
            {
                cells.Add(
                    Control(widget, button, AgeWidgets.Raw(widget), AgeWidgets.TextOf(widget), key)
                );
            }
        }

        /// <summary>The drawn readout at <paramref name="widget"/>, if it says anything - a band the
        /// game has emptied contributes nothing rather than an unnamed node.</summary>
        public static void AddReadout(List<Cell> cells, AgeTransform widget, string key)
        {
            if (
                widget != null
                && AgeWidgets.Visible(widget)
                && !string.IsNullOrEmpty(AgeWidgets.TextOf(widget))
            )
            {
                cells.Add(Readout(widget, AgeWidgets.Raw(widget), key));
            }
        }

        /// <summary>A line the player reads rather than works: whatever words the game drew in it, and
        /// its own tooltip.</summary>
        public static Cell Readout(AgeTransform widget, AgeTooltip tooltip, string key)
        {
            AgeTransform at = widget;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(at)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, key),
                Vtable = vtable,
            };
        }
    }
}

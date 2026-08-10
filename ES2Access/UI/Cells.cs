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
    }
}

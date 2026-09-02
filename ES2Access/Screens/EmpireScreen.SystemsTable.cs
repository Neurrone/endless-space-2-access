using System;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The systems tab: the table of the empire systems and the cells a row answers with.
    /// </summary>
    public sealed partial class EmpireScreen
    {
        // ---- the systems tab ----

        /// <summary>The table of systems: the sort headers, the rows, and then whichever panel the last
        /// cell click slid out under them.</summary>
        private void BuildSystems(GraphBuilder builder, global::EmpireScreen window)
        {
            StarSystemsManagementPanel panel = window.StarSystemsManagementPanel;
            GuiTable table = panel == null ? null : panel.GuiTable;
            // Flow control: the shared table reading walks every line and cell of it.
            if (table == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(SystemsStop);
            _table.Headers(builder, table);
            _table.Rows(builder, table, PanelTitle(panel));

            BuildDetails(builder, panel);
        }

        /// <summary>
        /// One column of a row where the game drew a BUTTON in the cell rather than a figure - status,
        /// population, construction, hangar and the assigned hero, which are what open the panels under
        /// the table.
        ///
        /// Null for every other column, which is the shared value cell. Enter here is the cell's own
        /// click carried on to the row's toggle, which is the two-step the mouse makes: the cell button
        /// records which cell was hit and the toggle's handler reads it and opens the matching panel.
        ///
        /// The cell is still a cell - it says the figure it is drawing and not its heading, which the
        /// sheet speaks as the edge - and it is read as a control rather than as a value only because a
        /// REFUSAL lives on the cell: the construction column of an outpost or a ghost is switched off
        /// with the game's own sentence about why, while the row it sits in is perfectly available.
        /// Both shapes are the sheet's (<see cref="TableSheet.PolicyCell"/>,
        /// <see cref="TableSheet.ButtonCell"/>); what is this page's own is the DROP the population
        /// column takes.
        /// </summary>
        private NodeVtable ActionCell(
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header,
            Func<bool> enabled
        )
        {
            NodeVtable policy = _table.PolicyCell(cell, header, enabled);
            if (policy != null)
            {
                return policy;
            }

            NodeVtable vtable = _table.ButtonCell(cell, header, enabled);
            if (vtable == null)
            {
                return null;
            }

            // The population column of another system is where a carried unit is SENT: the game's own
            // drag drops it here and the source system's spaceport ships it over.
            GuiTableCellSystemPopulation population = PopulationCell(cell);
            if (population != null)
            {
                GuiTableCellSystemPopulation at = population;
                vtable.DropKind = PopulationMoves.Kind;
                vtable.DropAccepts = held => Shippable(at, held) != null;
                vtable.OnDrop = held => Ship(at, held);
            }

            return vtable;
        }

        private static GuiTableCellSystemPopulation PopulationCell(AgeTransform cell)
        {
            try
            {
                return cell == null ? null : cell.GetComponent<GuiTableCellSystemPopulation>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

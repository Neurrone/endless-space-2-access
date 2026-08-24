using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The menu a column's funnel opens: which values of that column the table is allowed to show.
    ///
    /// A screen of its own, on the drop list's precedent, because that is what it is to the player - a
    /// smaller thing on top of a bigger one, with its own name (the game writes "Filter by &lt;column&gt;"
    /// across it), its own contents, and its own way out. It is NOT a drop list, though: the game's
    /// menu is a plain panel of independent tick boxes, so nothing is picked and nothing is dismissed
    /// by choosing - every box can be on or off, the table re-filters as each one flips, and the menu
    /// stays up until the funnel is unticked.
    ///
    /// Opening and closing are one gesture, the funnel's own: <c>GuiTableHeader.OnToggleFilterCb</c>
    /// reads the toggle it was called with and asks <c>GuiTable.ToggleFilter</c> to bind the menu or
    /// unbind it. So the header's checkbox node IS the open and the close, Escape here is that same
    /// checkbox flipped back, and neither the panel nor the table is touched by the mod directly.
    ///
    /// Which menu is open is mod state rather than a question asked of the game: one menu instance
    /// serves every column of a table, and only the header that opened it knows which column's values
    /// it is showing.
    /// </summary>
    public sealed class TableFilterScreen : Screen
    {
        /// <summary>The table whose menu is open and the header that opened it - everything this
        /// screen needs, and the only thing the page underneath knows.</summary>
        private sealed class Request
        {
            public GuiTable Table;
            public GuiTableHeader Header;
        }

        private static Request _open;

        /// <summary>Open the filter menu of <paramref name="header"/>, which is the funnel being
        /// ticked. The game does the opening: the caller has already flipped the toggle, and this only
        /// records which column the menu that appears belongs to.</summary>
        public static void Opened(GuiTable table, GuiTableHeader header)
        {
            _open = table == null || header == null
                ? null
                : new Request { Table = table, Header = header };
        }

        /// <summary>Forget any open menu - the mod is going away.</summary>
        public static void Reset()
        {
            _open = null;
        }

        public override string Key
        {
            get { return "screen.table-filter"; }
        }

        /// <summary>Above both pages that own a filterable table - the end-game journal and the custom
        /// faction editor - and below everything either of them can raise over itself (a drop list at
        /// 70, a confirmation at 100).</summary>
        public override int Layer
        {
            get { return 53; }
        }

        /// <summary>An open filter menu is a question about one column: the only things it offers are
        /// that column's values and leaving it.</summary>
        public override bool AnswersOnly
        {
            get { return true; }
        }

        /// <summary>The heading the menu writes for itself, which names the column it is filtering.
        /// </summary>
        public override string ScreenName
        {
            get
            {
                GuiTableFilterMenu menu = Menu();
                return menu == null ? null : AgeText.Label(menu.TitleLabel);
            }
        }

        /// <summary>Ours while the game is drawing the menu the funnel opened.</summary>
        public override bool IsActive()
        {
            GuiTableFilterMenu menu = Menu();
            return menu != null && menu.Shown && AgeWidgets.Visible(menu.AgeTransform);
        }

        /// <summary>Escape unticks the funnel, which is the same gesture that opened the menu and the
        /// only route the game has for closing it.</summary>
        public override bool Back()
        {
            Close();
            return true;
        }

        public override bool ConsumesBack
        {
            get { return true; }
        }

        public override void Build(GraphBuilder builder)
        {
            GuiTableFilterMenu menu = Menu();
            AgeTransform table = menu == null ? null : menu.FilterItemTable;
            IList<AgeTransform> children = table == null ? null : table.Children;
            string key = "table-filter:" + (menu == null ? "" : menu.GetInstanceID().ToString()) + "/";
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                GuiTableFilterItem item =
                    child == null ? null : child.GetComponent<GuiTableFilterItem>();
                if (item == null || item.Toggle == null || !AgeWidgets.Visible(child))
                {
                    continue;
                }

                GuiTableFilterItem it = item;
                NodeVtable vtable = GraphNodes.Checkbox(
                    () => Name(it),
                    () => it.Toggle.State,
                    () => AgeWidgets.Toggle(it.Toggle),
                    () => AgeWidgets.Operable(child)
                );
                AgeWidgets.Point(vtable, it.Toggle);
                builder.AddItem(ControlId.Referenced(item, key + i), vtable);
            }
        }

        /// <summary>What the game calls this value. The row's own label, which the menu writes from the
        /// filter's name through the game's own localization - and the icon's name where the prefab
        /// drew a symbol instead of a word, since a filter is sometimes a colour or a shape.</summary>
        private static string Name(GuiTableFilterItem item)
        {
            try
            {
                string label = AgeText.Label(item.Label);
                return string.IsNullOrEmpty(label)
                    ? AgeWidgets.PaintedText(item.AgeTransform)
                    : label;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Untick the funnel the way a click on it does, which is what asks the game to unbind
        /// the menu. Harmless when the game has already closed it.</summary>
        private static void Close()
        {
            Request request = _open;
            _open = null;
            try
            {
                AgeControlToggle funnel =
                    request == null || request.Header == null ? null : request.Header.FilterToggle;
                if (funnel != null && funnel.State)
                {
                    AgeWidgets.Toggle(funnel);
                }
            }
            catch (Exception e)
            {
                Log.Warn("table filter: closing the menu threw: " + e);
            }
        }

        /// <summary>The menu the open funnel's table keeps - one per table, bound to whichever column
        /// was ticked last.</summary>
        private static GuiTableFilterMenu Menu()
        {
            try
            {
                Request request = _open;
                return request == null || request.Table == null ? null : request.Table.FilterMenu;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

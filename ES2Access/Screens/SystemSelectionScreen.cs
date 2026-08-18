using System;
using System.Collections.Generic;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The window the game opens whenever it needs the player to pick one of their own systems - which
    /// colony feeds an outpost, where a ship is spawned, where the Academy sends a hero. It is a
    /// GENERIC window: six different panels open it with their own <c>Purpose</c>, their own filter and
    /// their own reason for refusing a system, and none of that is written down here. What is modelled
    /// is what the window DRAWS - its title, its sort headers, its table of systems and its two
    /// buttons - so every caller of it speaks without a screen of its own.
    ///
    /// Three stops, in drawn order: the sort headers across the top, the table of systems, then Cancel
    /// and Confirm. The first two are <see cref="TableSheet"/> - the reading of a <c>GuiTable</c> is
    /// shared with every other screen the game points that machinery at, including the Empire screen's
    /// systems tab, which binds this window's OWN column set. Everything the sheet does, and why
    /// (headings as crossed edges, the whole row in the name cell's buffer, select-then-act, a refused
    /// row declared refusing with the game's sentence), is written down there.
    ///
    /// What is this window's alone is the automation policy column, where the game draws a drop list
    /// rather than a figure (<see cref="Policy"/>), and its bottom band. The line tooltip the sheet
    /// reads a refusal from is written by
    /// <c>SystemSelectionModalWindow.GuiColonizedStarSystemObject.OnBind</c> (:34-44) as plain content,
    /// so it is announced rather than merely indicated.
    ///
    /// Escape is the game's. The window is a plain <c>GuiModalWindow</c> with no <c>HandleInput</c> of
    /// its own, so Exit hides it - and unlike the faction chooser, whose Exit is routed to its Validate
    /// handler, hiding this one commits nothing at all.
    /// </summary>
    public sealed class SystemSelectionScreen : Screen
    {
        private static readonly object LinesStop = "syssel:lines";
        private static readonly object ActionsStop = "syssel:actions";

        private readonly TableSheet _table;

        public SystemSelectionScreen()
        {
            _table = new TableSheet("syssel:", SystemOf);
            _table.RowName = SystemName;
            _table.ReadCell = Policy;
        }

        public override string Key
        {
            get { return "screen.system-selection"; }
        }

        /// <summary>
        /// Over the star system page and the panel a planet card can slide out under it, and BELOW two
        /// things this window itself can raise: the tutorial page it registers a key for
        /// (<c>AddTutorialKeyIFN</c>) and the drop list its policy column opens.
        /// </summary>
        public override int Layer
        {
            get { return 25; }
        }

        /// <summary>What the window has written across its top - "Select a System". The window does not
        /// expose the label, so it is found where it is drawn.</summary>
        public override string ScreenName
        {
            get { return Title(Window()); }
        }

        /// <summary>The table, because it is drawn first and Tab does not wrap - its headings are the
        /// row above its first line, not a stop of their own.</summary>
        public override object InitialFocusStop
        {
            get { return LinesStop; }
        }

        public override bool IsActive()
        {
            SystemSelectionModalWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's own: Exit hides the window and commits nothing.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            SystemSelectionModalWindow window = Window();
            GuiTable table = Table(window);
            if (table == null)
            {
                return;
            }

            builder.BeginStop(LinesStop);
            _table.Headers(builder, table);
            _table.Rows(builder, table, Title(window));

            builder.BeginStop(ActionsStop);
            BuildActions(builder, window);
        }

        /// <summary>
        /// The automation policy column, where the game draws a DROP LIST rather than a readout - and
        /// so, where the game leaves it operable, the cell is a combo box and Enter opens the list
        /// instead of selecting the row.
        ///
        /// Null for every other column, and for a policy the game has switched off: an empire without
        /// system automation gets the list drawn disabled, and a disabled list is a readout of what the
        /// system is doing instead.
        ///
        /// Like every other cell it does not say its own heading - the crossed edge does - but the list
        /// it opens is still TITLED with it, because that window is somewhere the player has been taken.
        /// </summary>
        private NodeVtable Policy(
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header,
            Func<bool> enabled
        )
        {
            AgeControlDropList list = DropList(cell);
            if (list == null || !AgeWidgets.Operable(list.AgeTransform) || !enabled())
            {
                return null;
            }

            AgeControlDropList it = list;
            AgeTransform widget = cell;
            GuiTableHeader heading = header;
            return GraphNodes.ComboBox(
                null,
                () => _table.CellText(widget),
                () => SettingRows.OpenList(it, TableSheet.HeaderName(heading)),
                () => AgeWidgets.Operable(it.AgeTransform),
                TableSheet.TooltipOf(widget),
                null,
                () => _table.CellFacts(heading, widget)
            );
        }

        // ---- the bottom band ----

        /// <summary>Cancel and Confirm, taken from the band they share rather than named: the window
        /// exposes Confirm and leaves Cancel as its sibling, and reading the band keeps them in the
        /// order they are drawn in - one per row, the way a window's bottom bar is walked. Confirm is
        /// disabled until a system is picked, which is what makes it read unavailable with the game's
        /// own sentence for what it would do.</summary>
        private void BuildActions(GraphBuilder builder, SystemSelectionModalWindow window)
        {
            AgeTransform validate = ValidateTransform(window);
            AgeTransform band = validate == null ? null : validate.Parent;
            List<AgeTransform> buttons = new List<AgeTransform>();
            try
            {
                List<AgeTransform> children = band == null ? null : band.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (
                        child != null
                        && AgeWidgets.Visible(child)
                        && AgeWidgets.Button(child) != null
                    )
                    {
                        buttons.Add(child);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("system selection: reading the button band threw: " + e);
            }

            if (buttons.Count == 0)
            {
                return;
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                AgeTransform button = buttons[i];
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeWidgets.TextOf(button),
                    () => AgeWidgets.Press(button),
                    () => AgeWidgets.Operable(button),
                    AgeWidgets.Raw(button)
                );
                AgeWidgets.Point(vtable, AgeWidgets.Button(button));
                builder.AddItem(
                    ControlId.Referenced(button, "syssel:button/" + NameOf(button)),
                    vtable
                );
            }
        }

        // ---- reading the window ----

        /// <summary>The window's own title, found where it is drawn: the class exposes its table and
        /// its Confirm button and nothing else.</summary>
        private static string Title(SystemSelectionModalWindow window)
        {
            try
            {
                if (window == null)
                {
                    return null;
                }

                AgePrimitiveLabel[] labels =
                    window.GetComponentsInChildren<AgePrimitiveLabel>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] != null && labels[i].name == "WindowTitle")
                    {
                        return AgeText.Label(labels[i]);
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        private static AgeControlDropList DropList(AgeTransform cell)
        {
            try
            {
                return cell == null ? null : cell.GetComponentInChildren<AgeControlDropList>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The system a row stands for. The wrapper the table binds is rebuilt on every
        /// refresh, so it is the system underneath it that identifies the row.</summary>
        private static ColonizedStarSystem SystemOf(GuiTableLine line)
        {
            try
            {
                GuiColonizedStarSystem wrapper =
                    line == null ? null : line.Data as GuiColonizedStarSystem;
                return wrapper == null ? null : wrapper.ColonizedStarSystem;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the row is called when the name column draws nothing - the system's own
        /// name.</summary>
        private static string SystemName(GuiTableLine line)
        {
            try
            {
                GuiColonizedStarSystem wrapper =
                    line == null ? null : line.Data as GuiColonizedStarSystem;
                return wrapper == null ? null : AgeText.Clean(wrapper.LocalizedName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string NameOf(AgeTransform widget)
        {
            try
            {
                return widget.name;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static AgeTransform ValidateTransform(SystemSelectionModalWindow window)
        {
            try
            {
                return window == null || window.ValidateButton == null
                    ? null
                    : window.ValidateButton.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GuiTable Table(SystemSelectionModalWindow window)
        {
            try
            {
                return window == null ? null : window.GuiTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static SystemSelectionModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<SystemSelectionModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

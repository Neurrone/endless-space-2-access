using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// Why a system's parties think what they think: the modal the representatives side panel's political
    /// overview button opens over the star system page (<c>RepresentativesStarSystemSidePanel
    /// .OnOpenPopulationPanelCb</c> :209-217, which binds the system and shows the window).
    ///
    /// The window is a party PICKER over a table. Across the top sit the empire's parties, of which
    /// exactly one is in force - the game draws them as a radio group and the pick rebinds everything
    /// below it (<c>StarSystemPopulationModalWindow.OnClickPolitics</c> :217-229), so they are radios and
    /// Enter is the toggle's own switch. Under them is a table of the ONGOING EVENTS that are moving the
    /// chosen party's score, one row per party whose events matter, one column per population living in
    /// the system.
    ///
    /// Three things about that table are not like the mod's other ones, and all three are answered
    /// through <see cref="TableSheet"/>'s own hooks rather than by a second table reader:
    ///
    /// - It is bound <c>canSelect: false</c> (<c>PoliticalEventsPopulationPanel.BuildPopulationGuiTable</c>
    ///   :173), so its rows are readouts and Enter does nothing on them - which is what a click does.
    /// - Its population columns draw a PORTRAIT and no caption. The label the game left in the heading is
    ///   the raw key of a string it never wrote (<c>%SystemPopulationPoliticsTableAffinityTerransTitle</c>),
    ///   so the column's name is taken off the heading's own tooltip wrapper instead
    ///   (<see cref="ColumnName"/>).
    /// - Its cells draw an ICON and no figure: how strongly one party's events move another's score is on
    ///   the cell's own tooltip ("Weak support"), which makes that sentence the cell's VALUE rather than
    ///   something to indicate (<see cref="SupportText"/>).
    ///
    /// Only the parties the game is DRAWING are declared. The other three toggles keep the last support
    /// figure that was written into them - a stale "100%" - and are hidden by alpha rather than by
    /// visibility (<c>RefreshPoliticalImpactToggle</c> :183-202), so drawn-ness here is alpha as well as
    /// the enable chain.
    ///
    /// The window's own heading is declared where it is drawn and focus lands on it, so there is no screen
    /// name: saying the title as a screen name and then again as the control focus lands on says it twice
    /// (the <see cref="ImprovementsModalScreen"/> precedent).
    ///
    /// Escape is the game's: the window is a <c>GuiModalWindow</c> whose <c>HandleInput(Exit)</c> hides
    /// it, and hiding it commits nothing - the party pick is a view of the system, not an order.
    /// </summary>
    public sealed class SystemPoliticsScreen : Screen
    {
        private static readonly object HeadingStop = "politics:heading";
        private static readonly object PartiesStop = "politics:parties";
        private static readonly object EventsStop = "politics:events";
        private static readonly object CloseStop = "politics:close";

        private readonly TableSheet _table;
        private readonly List<Cell> _cells = new List<Cell>();

        public SystemPoliticsScreen()
        {
            _table = new TableSheet("politics:", RowOf);
            _table.NameColumn = ColumnName;
            _table.ReadValue = SupportText;
            _table.RowDetails = EventLines;
        }

        public override string Key
        {
            get { return "screen.system-politics"; }
        }

        /// <summary>Over the star system page and everything that page can have up, and under the
        /// message box. Nothing on this window opens a drop list or a confirmation of its own.</summary>
        public override int Layer
        {
            get { return 86; }
        }

        /// <summary>The heading, because it is drawn first and Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return HeadingStop; }
        }

        /// <summary>Set once the window has finished arriving and cleared when the game unbinds the
        /// system it was opened for, which is the last thing <c>OnEndHide</c> does (:133-139). Instance
        /// state, so a hot reload starts it over rather than inheriting a stale answer.</summary>
        private bool _arrived;

        public override bool IsActive()
        {
            StarSystemPopulationModalWindow window = Window();
            try
            {
                if (window == null || window.ColonizedStarSystem == null)
                {
                    _arrived = false;
                    return false;
                }

                if (!_arrived)
                {
                    _arrived = window.Shown && window.IsReady;
                }

                return _arrived;
            }
            catch (Exception)
            {
                _arrived = false;
                return false;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            StarSystemPopulationModalWindow window = Window();
            if (window == null || !window.Shown)
            {
                // On the way out. The screen stays ACTIVE until the game unbinds the system - leaving at
                // begin-hide would hand the keyboard to a page that is not interactive yet - but it
                // declares nothing while the window fades, because the game switches these controls off
                // as it goes and a live part on the focused one would announce the fade as a refusal.
                // An empty render keeps the cursor.
                return;
            }

            builder.BeginStop(HeadingStop);
            BuildHeading(builder, window);

            builder.BeginStop(PartiesStop);
            BuildParties(builder, window);

            builder.BeginStop(EventsStop);
            BuildEvents(builder, window);

            builder.BeginStop(CloseStop);
            BuildClose(builder, window);
        }

        // ---- the heading ----

        /// <summary>The window's title, with the sentence the game wrote about the whole window on its
        /// tooltip. The window does not expose the label, so it is found where it is drawn.</summary>
        private void BuildHeading(GraphBuilder builder, StarSystemPopulationModalWindow window)
        {
            AgeTransform title = Named(Root(window), "TitleLabel");
            if (title == null || !AgeWidgets.Visible(title))
            {
                return;
            }

            _cells.Clear();
            AddReadout(_cells, title, "politics:title");
            Cells.Emit(builder, _cells);
        }

        // ---- the parties ----

        /// <summary>
        /// One radio per party the game is drawing, one per row in the order it drew them - a strip of
        /// choices of one kind, where a sideways step buys nothing a step down does not: the party's name, whether
        /// it is the one being explained, and the support figure written under the name.
        ///
        /// Keyed STRUCTURALLY by the toggle's place in the group, deliberately: the party wrapper a
        /// toggle is bound to is the same object the table's rows stand for, and reference identity is
        /// followed before the structural key - two nodes sharing one object are one control to the
        /// cursor, which would teleport focus between the radio and its row. The rows are the ones that
        /// move, so they keep the reference and the toggles - a fixed pool the game refreshes by index -
        /// key by position.
        /// </summary>
        private void BuildParties(GraphBuilder builder, StarSystemPopulationModalWindow window)
        {
            _cells.Clear();
            try
            {
                AgeTransform table =
                    window.PoliticsSelectionGroup == null
                        ? null
                        : window.PoliticsSelectionGroup.TogglesTable;
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AddParty(_cells, children[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("politics: reading the parties threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
        }

        private static void AddParty(List<Cell> cells, AgeTransform widget, int index)
        {
            PoliticalImpactToggle party = Party(widget);
            if (party == null || !Drawn(widget) || !AgeWidgets.Operable(widget))
            {
                return;
            }

            PoliticalImpactToggle it = party;
            AgeTransform host = widget;
            NodeVtable vtable = GraphNodes.Radio(
                () => PartyName(it),
                () => it.Toggle != null && it.Toggle.State,
                () => AgeWidgets.Toggle(it.Toggle),
                () => AgeWidgets.Operable(host),
                null,
                it.Tooltip
            );
            // The figure the toggle draws under the name: how sensitive this system is to the party.
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => AgeText.Label(it.PoliticsSensitivityScore))
            );
            AgeWidgets.Point(vtable, it.Toggle, it.Tooltip, host);
            Cells.Add(cells, widget, ControlId.Structural("politics:party/" + index), vtable);
        }

        /// <summary>What the toggle calls the party: the name it draws, falling back to the wrapper the
        /// game hangs on its tooltip - the tooltip's own content is the party's internal name
        /// ("Politics01") and is never spoken.</summary>
        private static string PartyName(PoliticalImpactToggle party)
        {
            string drawn = AgeText.Label(party.PoliticsName);
            return string.IsNullOrEmpty(drawn) ? AgeWidgets.TooltipTitle(party.Tooltip) : drawn;
        }

        // ---- the events table ----

        /// <summary>
        /// The panel under the parties, in the order it is drawn: the filter the game puts above the
        /// table, then the sort headings, then the table itself. The three are one stop because they are
        /// one panel; the vertical seams between the button rows and the sheet are the builder's, which
        /// knows a seam is a ROW rather than a node.
        ///
        /// With no party picked the game replaces the whole table with one sentence, and that sentence is
        /// what is declared - there is no table to read and nothing invented in its place.
        /// </summary>
        private void BuildEvents(GraphBuilder builder, StarSystemPopulationModalWindow window)
        {
            PoliticalEventsPopulationPanel panel = window.PoliticalEventsPanel;
            if (panel == null)
            {
                return;
            }

            _cells.Clear();
            AddCheckbox(_cells, panel.ShowAllEventsToggle);
            AgeTransform empty =
                panel.NoPoliticsSelectedLabel == null
                    ? null
                    : panel.NoPoliticsSelectedLabel.AgeTransform;
            if (empty != null && AgeWidgets.Visible(empty))
            {
                AddReadout(_cells, empty, "politics:no-party");
            }

            Cells.Emit(builder, _cells);

            GuiTable table = panel.PopulationGuiTable;
            if (table == null || !AgeWidgets.Visible(table.AgeTransform))
            {
                return;
            }

            _table.Headers(builder, table);
            _table.Rows(builder, table, PanelTitle(panel));
        }

        private static void AddCheckbox(List<Cell> cells, AgeControlToggle toggle)
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTransform host = widget;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeWidgets.TextOf(host),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(host),
                AgeWidgets.Raw(host)
            );
            AgeWidgets.Point(vtable, it);
            Cells.Add(cells, widget, ControlId.Referenced(toggle, "politics:show-all"), vtable);
        }

        /// <summary>What the panel calls its table, which is what the player hears as they enter it.
        /// </summary>
        private static string PanelTitle(PoliticalEventsPopulationPanel panel)
        {
            AgeTransform title = Named(panel.AgeTransform, "Title");
            return title == null ? null : AgeWidgets.TextOf(title);
        }

        /// <summary>The party a row stands for. The wrapper the table binds is built afresh on every
        /// refresh (<c>RefreshPoliticsInfoByPopulation</c> :121-143), so it is the party underneath it
        /// that identifies the row.</summary>
        private static object RowOf(GuiTableLine line)
        {
            try
            {
                GuiPoliticsInfoByPopulation wrapper =
                    line == null ? null : line.Data as GuiPoliticsInfoByPopulation;
                return wrapper == null ? null : wrapper.TargetGuiPolitics;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a population column is called: the heading draws a portrait and leaves the raw
        /// key of a string the game never wrote in its label, so the name comes off the wrapper the game
        /// hangs on the heading's tooltip ("Imperials", "Yuusho").</summary>
        private static string ColumnName(GuiTableHeader header)
        {
            string drawn = TableSheet.HeaderName(header);
            if (!Unwritten(drawn))
            {
                return drawn;
            }

            try
            {
                return AgeWidgets.TooltipTitle(header.Tooltip);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the game left a caption it never wrote - a bare localization key, which is
        /// parked text and not something to speak.</summary>
        private static bool Unwritten(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            try
            {
                return Gui.IsLocalizationKey(text);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// What a support cell says: the game draws an icon and no words, and the words are on the cell's
        /// own tooltip ("Weak support", "Moderate support"). That sentence is how strongly this row's
        /// party moves the chosen party's score among this column's population - the cell's VALUE, not
        /// something to indicate.
        ///
        /// Null for a column that draws its own words, which reads as any other cell.
        /// </summary>
        private string SupportText(GuiTableHeader header, AgeTransform cell)
        {
            if (_table.DrawnText(cell) != null)
            {
                return null;
            }

            Func<IList<string>> words = AgeWidgets.TooltipLines(TableSheet.TooltipOf(cell));
            return words == null ? null : Phrase(words());
        }

        private static string Phrase(IList<string> lines)
        {
            MessageBuilder message = new MessageBuilder();
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                message.ListItem(lines[i]);
            }

            return message.Build();
        }

        /// <summary>The events themselves: the lines the game draws inside a row's name cell under the
        /// party's name, which is what the row is actually about ("New Population in the empire
        /// (30 Turn)"). They are the row's own content rather than a column of it, so they read in its
        /// review buffer before the figures.</summary>
        private static IList<string> EventLines(GuiTableLine line)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTransform table = Named(Widget(line), "ModifiersTable");
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (child == null || !child.Visible)
                    {
                        continue;
                    }

                    string text = AgeText.Label(child.GetComponent<AgePrimitiveLabel>());
                    if (!string.IsNullOrEmpty(text) && !lines.Contains(text))
                    {
                        lines.Add(text);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("politics: reading a row's events threw: " + e);
            }

            return lines;
        }

        // ---- the bottom band ----

        /// <summary>The band along the bottom, read rather than named: the window exposes no button at
        /// all, and reading the band keeps whatever it draws in the order it is drawn in.</summary>
        private void BuildClose(GraphBuilder builder, StarSystemPopulationModalWindow window)
        {
            _cells.Clear();
            try
            {
                AgeTransform band = Named(Root(window), "BottomGroup");
                IList<AgeTransform> children = band == null ? null : band.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AddButton(_cells, children[i], "politics:button/");
                }
            }
            catch (Exception e)
            {
                Log.Warn("politics: reading the bottom band threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
        }

        // ---- shared ----

        private static void AddButton(List<Cell> cells, AgeTransform widget, string keyPrefix)
        {
            AgeControlButton button = widget == null ? null : AgeWidgets.Button(widget);
            if (button == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TextOf(it),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(it),
                AgeWidgets.Raw(it)
            );
            AgeWidgets.Point(vtable, button);
            Cells.Add(cells, widget, ControlId.Referenced(widget, keyPrefix + Name(widget)), vtable);
        }

        private static void AddReadout(List<Cell> cells, AgeTransform widget, string key)
        {
            AgeTransform it = widget;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(it)),
                },
                Sections = GraphNodes.Sections(null, AgeWidgets.Raw(it)),
            };
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        /// <summary>Drawn as the game draws these: a party the window is not showing is left ENABLED
        /// nowhere but transparent everywhere, so alpha is half the question.</summary>
        private static bool Drawn(AgeTransform widget)
        {
            try
            {
                return AgeWidgets.Visible(widget) && widget.Alpha > 0f;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static PoliticalImpactToggle Party(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<PoliticalImpactToggle>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Widget(GuiTableLine line)
        {
            try
            {
                return line == null ? null : line.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Root(StarSystemPopulationModalWindow window)
        {
            try
            {
                return window == null ? null : window.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A widget the window keeps no field for, found by the name the prefab gives it.
        /// </summary>
        private static AgeTransform Named(AgeTransform root, string name)
        {
            try
            {
                if (root == null)
                {
                    return null;
                }

                AgeTransform[] found = root.GetComponentsInChildren<AgeTransform>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null && found[i].name == name)
                    {
                        return found[i];
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        private static string Name(AgeTransform widget)
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

        private static StarSystemPopulationModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<StarSystemPopulationModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

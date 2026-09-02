using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The empire summary: the page F1 opens, with the empire's own description down the left edge and
    /// two tabs in the middle - every system the empire holds as a table, and how the empire is doing
    /// against each way of winning.
    ///
    /// The tab bar is the game's own instant switch: pressing a tab hides one panel and shows the other
    /// in the same frame, so what is declared is whatever is DRAWN rather than which tab the window
    /// remembers - other screens set that field on their way here (a notification, a colony panel), and
    /// a page modelled off the remembered value would describe the tab the player is not looking at.
    ///
    /// The systems table is the same <c>SystemListTable</c> the system-selection window binds, read by
    /// the shared table reader (<see cref="TableSheet"/>): rows down the name column, figures across,
    /// column captions spoken as the edge the player crosses. What is this page's own is that five of
    /// the fourteen columns are BUTTONS - status, population, construction, hangar and the assigned hero
    /// - and each opens a different panel under the table. That two-step is the game's own and is
    /// replayed as one gesture: the cell's own button records which cell was clicked and the click then
    /// propagates to the row's toggle, which is what actually opens the panel
    /// (<see cref="AgeWidgets.PressPropagating"/>; <c>PropagateInteraction</c> measured true on all five
    /// of this table's cell buttons). A cell the game has switched off - the construction column of an
    /// outpost or a ghost - is declared REFUSING with the sentence the game wrote into it, which is why
    /// those five columns are read here rather than by the sheet's ordinary value cell: the refusal
    /// lives on the CELL while the sheet's own availability question is about the ROW.
    ///
    /// The panel a cell opens is a second instance of the prefabs the star system page draws along its
    /// own bottom edge, so the planet cards, the constructibles, the queue and the hangar are read by
    /// the shared readers rather than modelled twice (<see cref="SystemPanels"/>).
    ///
    /// Population MOVES here, and this page offers one the star system page does not. The panel holding
    /// the cards is its own drop client (<c>StarSystemPlanetCardsPanel.UpdateDrag</c> :167-205) and takes
    /// a dropped unit two ways: onto another CARD, which is the move between two planets of this system,
    /// and onto the population cell of ANOTHER SYSTEM's row in the table, which points this system's
    /// spaceport at that system and ships the unit there. Both are offered as the mod's carry - Space to
    /// pick a unit up off a card, Enter to put it down - so the population rows are sources and both
    /// kinds of target say so.
    ///
    /// Nothing closes a detail panel: the game itself has no gesture for it - clicking the same cell
    /// again leaves it open (measured) - and it goes when another cell is clicked or the page is left.
    ///
    /// The victory tab is a wheel with no controls in it at all: six sectors drawn clockwise from the
    /// top, each with the empire's rank in that race and three performance figures around its rim. It
    /// reads as one row per sector with the figures as its children, in the order the wheel draws them.
    /// The sentence the game puts on a sector - what the condition asks for and how far along the
    /// empire is - is plain text the game wrote, so it is announced whole like every other plain
    /// tooltip in the mod. This page states no exception: a tooltip's kind is the whole of the rule.
    ///
    /// Escape and F1 stay the game's: the page is the game's own and its own close paths work. It is one
    /// of the icon strip's screens, which the engine draws in an exclusive window stack - opening any
    /// other one hides this instantly - which is why they all share a layer.
    /// </summary>
    public sealed class EmpireScreen : Screen
    {
        private static readonly object TabsStop = "empire:tabs";
        private static readonly object SystemsStop = "empire:systems";
        private static readonly object PlanetsStop = "empire:detail/planets";
        private static readonly object ConstructiblesStop = "empire:detail/constructibles";
        private static readonly object QueueStop = "empire:detail/queue";
        private static readonly object HangarStop = "empire:detail/hangar";
        private static readonly object VictoryStop = "empire:victory";

        /// <summary>The prefix the shared readers key this page's ids under.</summary>
        private const string Keys = "empire:";

        /// <summary>The clusters the game draws over every page. They are drawn over this one too.
        /// </summary>
        private readonly GlobalHud _hud = new GlobalHud();

        private readonly TableSheet _table;

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<SidePanel> _panels = new List<SidePanel>();
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<PlanetCard> _cards = new List<PlanetCard>();

        /// <summary>Which panel a table cell has slid out under the table, and the system it was opened
        /// for, as of the last frame this page was asked (see <see cref="WatchDetails"/>). Instance
        /// state, so it is reload-safe.</summary>
        private Detail _detail;
        private string _detailSystem;

        public EmpireScreen()
        {
            _table = new TableSheet(Keys, SystemOf);
            _table.RowName = SystemName;
            _table.ReadCell = ActionCell;
            // The systems table's own second click opens that system's management page.
            _table.DoubleClickHint = ModStrings.HintOpenSystemManagement;
        }

        public override string Key
        {
            get { return "screen.empire"; }
        }

        /// <summary>Above the view levels it is drawn over, beside the senate and the technology wheel:
        /// the strip of icons in the corner opens all of them and the engine's window stack lets only
        /// one be up at a time.</summary>
        public override int Layer
        {
            get { return 15; }
        }

        public override string ScreenName
        {
            get
            {
                string title = ScreenTitle();
                return string.IsNullOrEmpty(title) ? ModStrings.Get(ModStrings.ScreenEmpire) : title;
            }
        }

        /// <summary>Whichever tab is drawn, because that is what the player opened the page for. The
        /// panels down the left edge and the tab bar itself are a Shift+Tab away.</summary>
        public override object InitialFocusStop
        {
            get { return VictoryDrawn(Window()) ? VictoryStop : SystemsStop; }
        }

        /// <summary>A page the player closes and comes straight back to, with the cursor where they
        /// left it.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>Escape is the game's: it closes the screen, which is what the page's own exit does
        /// too.</summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        /// <summary>Arrival gates on the page being WORKABLE, not just on no modal being up: the renderer
        /// switches the whole background stack off while a modal is showing and back on a frame or more
        /// AFTER the modal reports itself gone, so coming back on "no modal" alone lands the cursor on a
        /// page whose every control is still switched off and reads one passing "unavailable".</summary>
        public override bool IsActive()
        {
            try
            {
                global::EmpireScreen window = Window();
                if (window == null || !window.Shown || !window.IsReady)
                {
                    return false;
                }

                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                return gui != null
                    && !gui.IsAnyModalVisible
                    && !gui.IsInLoadingWindow
                    && AgeWidgets.Operable(window.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void OnPush()
        {
            _hud.Baseline();
            // Start the watch from what is on the screen now, so coming back to a page that still has
            // a panel out never announces an opening nobody just made.
            _detail = DrawnDetail(out _detailSystem);
        }

        public override void OnPop()
        {
            _hud.Forget();
            _detail = Detail.None;
            _detailSystem = null;
        }

        public override void OnUpdate()
        {
            _hud.Update();
            WatchDetails();
        }

        public override void Build(GraphBuilder builder)
        {
            global::EmpireScreen window = Window();
            if (window == null)
            {
                return;
            }

            // Down and across the screen: the empire's banners along the top with this page's own title
            // beside them, the panels down the left edge, the tab bar and whichever tab it is showing,
            // then the right-hand edge and the turn controls.
            _hud.Top(builder);
            BuildSidePanels(builder);
            BuildTabs(builder, window);
            if (VictoryDrawn(window))
            {
                BuildVictory(builder, window);
            }
            else
            {
                BuildSystems(builder, window);
            }

            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.TurnLog(builder);
            _hud.Turn(builder);
        }

        // ---- the panels down the left edge ----

        /// <summary>A stop per panel the game is drawing down the left edge, top to bottom, read off
        /// what is in them - one line per thing the panel says, whatever the box wrapped onto the same
        /// row.</summary>
        private void BuildSidePanels(GraphBuilder builder)
        {
            try
            {
                SidePanels.Drawn(_panels);
                for (int i = 0; i < _panels.Count; i++)
                {
                    SidePanel panel = _panels[i];
                    builder.BeginStop("empire:side/" + panel.GetType().Name);
                    builder.PushContext(PanelName(panel));
                    _cells.Clear();
                    SidePanels.Readouts(
                        _cells,
                        panel,
                        "empire:side/" + i + "/",
                        SpecialCell,
                        Transparent
                    );
                    Cells.EmitLinear(builder, _cells);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading the side panels threw: " + e);
            }
        }

        /// <summary>What a side panel is called. The approval box and the relic box write a heading
        /// across their own top, and the shared reader takes that; the box holding the empire's
        /// portrait and description writes none at all, so it gets a word of the mod's.</summary>
        private static string PanelName(SidePanel panel)
        {
            if (panel is EmpireDescriptionSidePanel)
            {
                return ModStrings.Get(ModStrings.EmpireDescriptionPanel);
            }

            return SidePanels.Name(panel);
        }

        /// <summary>
        /// The two things in these panels the shape of the widget tree cannot name.
        ///
        /// The first is how many systems the empire holds against how many it can hold before the rest
        /// start to suffer. The panel draws it as a bare "1/7" over a bar, and the sentence saying what
        /// the two numbers ARE is on the group around them - which the walk would otherwise descend past,
        /// leaving a fraction with nothing to say for itself. The game writes one of three sentences there
        /// depending on the empire (<c>EmpireStatusSidePanel.Refresh</c> :87-98), so the words are
        /// whatever it wrote this frame.
        ///
        /// The second is an empire relic slot (<see cref="RelicSlot"/>).
        /// </summary>
        private static bool SpecialCell(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            // The type test first, so that every other panel's walk costs nothing: only the relic box
            // holds slots, and it only exists for one faction.
            if (panel is EmpireRelicsSidePanel && RelicSlot(cells, widget, keyPrefix))
            {
                return true;
            }

            EmpireStatusSidePanel status = panel as EmpireStatusSidePanel;
            if (status == null || !ReferenceEquals(widget, status.OvercolonizationGroup))
            {
                return false;
            }

            cells.Add(
                Cells.Readout(widget, AgeWidgets.Raw(widget), keyPrefix + "overcolonization")
            );
            return true;
        }

        /// <summary>
        /// One of the four empire relic slots the relic box draws (<c>RelicSlotItem</c>, Nakalim only -
        /// <c>EmpireRelicsSidePanel.CanBeShown</c> requires the Templars affinity).
        ///
        /// It is here rather than left to the shape walk because pressing it does one of TWO OPPOSITE
        /// things and the game says which only by swapping one picture for another. <c>Update</c> flips
        /// the slot into remove mode the moment the slot holds a relic and back into assign mode when it
        /// empties (<c>RelicSlotItem</c> :172-188), and the drawn sign is which of
        /// <c>AssignRelicsImage</c>/<c>RemoveRelicsImage</c> is visible - so that is what is READ here,
        /// rather than the private field behind it. A slot declared by shape alone would be a button whose
        /// name never changes while its meaning does.
        ///
        /// The slot has no name of its own either: it titles itself with an icon token
        /// (<c>FIDSIGroupTitle.Text = def.GuiSymbol</c>, "[explorer]" and friends), so the name is the
        /// words the game opens the slot's own explanation with - "Exploration Relic" - and the icon's
        /// name only where that sentence is missing.
        ///
        /// Everything the game refuses with is on the slot's OWN tooltip and is carried here in full,
        /// which is what keeps the locked-slot sentence (<c>%EmpireRelicsSlotLocked</c>) and the
        /// nothing-to-assign failure reachable: both are appended to that tooltip and to nothing else.
        ///
        /// Measured by binding the four slots to the game's own <c>EmpireRelicSlotDefinition</c> rows and
        /// forcing the box visible, then restoring: "Exploration Relic:, button, Assign relics,
        /// unavailable, Effects: ... This slot is locked. You must build a Cathedral to the Lost ...", and
        /// with the remove picture drawn instead, "Exploration Relic:, button, Remove relics, 2, ...".
        /// What is NOT measured is a real Nakalim empire: the definitions load for everyone (their datatable
        /// carries no DLC prerequisite) but the box itself only shows for that affinity, so the assign and
        /// remove PRESSES have never been made.
        /// </summary>
        private static bool RelicSlot(List<Cell> cells, AgeTransform widget, string keyPrefix)
        {
            RelicSlotItem slot = Slot(widget);
            if (slot == null)
            {
                return false;
            }

            AgeTransform button = AgeWidgets.Transform(slot.button);
            if (button == null || !AgeWidgets.Visible(button))
            {
                // Nothing to press and nothing to say: the walk is still stopped, because descending
                // into the slot would scatter it into an icon, a picture and a number.
                //
                // The central gate does NOT cover this and cannot: the cell below is declared on
                // `widget`, the slot, and that is the widget the gate asks about. What is tested here
                // is the BUTTON inside it, a different widget with a state of its own - the box draws
                // an empty slot with no button at all. This is the only place the slot's existence is
                // decided: the walk stops either way, so nothing further down declares it, and the
                // node this gate refuses would otherwise be a button whose press target is not drawn.
                return true;
            }

            RelicSlotItem it = slot;
            AgeTransform at = button;
            AgeTooltip tooltip = slot.assignRelicsTooltip ?? AgeWidgets.Raw(button);
            Func<bool> offered = () => AgeWidgets.Offered(at);
            NodeVtable vtable = GraphNodes.Button(
                () => RelicSlotName(it),
                () => AgeWidgets.Press(at),
                offered,
                tooltip
            );
            // BOTH tooltips the slot carries, through the nesting sink. The name comes off the FIDSI
            // group.s sentence, so that sentence has to be reachable - it used to be read for the name
            // and then declared nowhere, and everything after its first line was unreachable. The
            // slot.s OWN is the one the pointer goes to, so it is last and it is the one the slot
            // announces; the group.s becomes a child entry of its own, because a second tooltip a node
            // is not pointing at is a second hover target and one row means a row of NODES.
            List<AgeTooltip> found = new List<AgeTooltip>(2);
            if (
                it.FIDSIGroupTooltip != null
                && !AgeWidgets.SameTooltip(it.FIDSIGroupTooltip, tooltip)
            )
            {
                found.Add(it.FIDSIGroupTooltip);
            }

            found.Add(tooltip);
            TooltipChildren.Carried carried = TooltipChildren.Split(found);
            vtable.Sections = GraphNodes.SectionsFor(vtable, carried.Own);
            vtable.Announcements.Add(GraphNodes.ValuePart(() => RelicSlotAction(it)));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => RelicsAssigned(it)));
            GraphNodes.AddRefusal(vtable, tooltip, offered);
            // Kept over the door's aim, which it re-states with the slot's BUTTON added so the slot
            // lights up under the cursor.
            AgeWidgets.Point(vtable, slot.button, tooltip, button);
            // The name and the position, because there are FOUR of these in one box and a key that named
            // only the panel is the same for all four - which throws Duplicate control id and empties the
            // WHOLE page. (Measured: that is how this was found. The clones happen to carry per-slot names
            // in this prefab, and the index is what makes the key safe if a later one does not.)
            string key =
                keyPrefix + "relic-slot/" + widget.name + "/" + AgeWidgets.IndexInParent(widget);
            Cell cell = Cells.Add(cells, widget, ControlId.For(slot, key), vtable);
            cell.Dossiers = carried.Children;
            cell.Key = carried.Children == null ? null : key;
            return true;
        }

        /// <summary>Which relic slot this is, in the game's own words: the sentence it explains itself
        /// with opens with the slot's name ("Exploration Relic:"), and the drawn title is an icon.
        /// </summary>
        private static string RelicSlotName(RelicSlotItem slot)
        {
            try
            {
                string described = CardActions.FirstLine(slot.FIDSIGroupTooltip);
                if (!string.IsNullOrEmpty(described))
                {
                    return described;
                }

                AgePrimitiveLabel title = slot.FIDSIGroupTitle;
                return title == null ? null : AgeWidgets.TextOf(title.AgeTransform);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What pressing this slot would do, read off the picture the game is drawing on it.
        /// </summary>
        private static string RelicSlotAction(RelicSlotItem slot)
        {
            try
            {
                return ModStrings.Get(
                    // Content: which of two words the action is called by.
                    AgeWidgets.Visible(slot.RemoveRelicsImage)
                        ? ModStrings.EmpireRelicSlotRemove
                        : ModStrings.EmpireRelicSlotAssign
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How many relics are in the slot, while the game is drawing that group at all - it
        /// hides it for an empty slot rather than writing a zero.</summary>
        private static string RelicsAssigned(RelicSlotItem slot)
        {
            try
            {
                return AgeWidgets.DrawnLabel(slot.AssignRelicsGroup, slot.AssignedRelicsLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static RelicSlotItem Slot(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<RelicSlotItem>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether a group the game made clickable is really a band of readouts.
        ///
        /// The approval box is: the prefab wires its click to <c>OnHappinessGroupCb</c> and no such
        /// method exists anywhere, so the message goes nowhere and the mouse gets silence - and a click
        /// the game answers with silence stays silent here. Treated as the band it is, its rows are read
        /// separately and the rich approval tooltip inside it (which hangs on the group holding the
        /// figure, not on the box) is found where the game put it.
        /// </summary>
        private static bool Transparent(AgeTransform widget, SidePanel panel)
        {
            EmpireStatusSidePanel status = panel as EmpireStatusSidePanel;
            return status != null && ReferenceEquals(widget, status.HappinessAndRebellionGroup);
        }

        // ---- the tab bar ----

        /// <summary>The two tabs, in the order they are drawn. Enter is the toggle's own click, which
        /// swaps the panel underneath in the same frame - so nothing is spoken for the press beyond the
        /// tab saying it is now the selected one; the page rebuilds around it.</summary>
        private void BuildTabs(GraphBuilder builder, global::EmpireScreen window)
        {
            builder.BeginStop(TabsStop);
            _cells.Clear();
            AddTab(_cells, window.ToggleSystems, 0);
            AddTab(_cells, window.ToggleVictory, 1);
            Cells.EmitLinear(builder, _cells);
        }

        private static void AddTab(List<Cell> cells, AgeControlToggle toggle, int index)
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (widget == null)
            {
                return;
            }

            AgeControlToggle it = toggle;
            NodeVtable vtable = GraphNodes.Tab(
                () => AgeWidgets.TextOf(widget),
                () => it.State,
                () => AgeWidgets.Operable(widget),
                AgeWidgets.Raw(widget)
            );
            vtable.OnActivate = () => AgeWidgets.Toggle(it);
            AgeWidgets.Point(vtable, it);
            Cells.Add(cells, widget, ControlId.For(widget, "empire:tab/" + index), vtable);
        }

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
        /// sheet speaks as the edge - and it is read here rather than by the sheet only because a
        /// REFUSAL lives on the cell: the construction column of an outpost or a ghost is switched off
        /// with the game's own sentence about why, while the row it sits in is perfectly available.
        /// </summary>
        private NodeVtable ActionCell(
            GuiTableLine line,
            AgeTransform cell,
            GuiTableHeader header,
            Func<bool> enabled
        )
        {
            NodeVtable policy = Policy(cell, header, enabled);
            if (policy != null)
            {
                return policy;
            }

            AgeControlButton button = ActionButton(cell);
            if (button == null)
            {
                return null;
            }

            AgeTransform it = cell;
            AgeControlButton press = button;
            GuiTableHeader heading = header;
            Func<bool> rowEnabled = enabled;
            Func<bool> operable = () =>
                rowEnabled() && AgeWidgets.Operable(press.AgeTransform) && AgeWidgets.Enabled(it);
            AgeTooltip tooltip = TableSheet.TooltipOf(cell);
            AgeTooltip reason = RefusalTooltip(cell) ?? tooltip;
            GuiTableCellSystemPopulation population = PopulationCell(cell);
            NodeVtable vtable = new NodeVtable
            {
                // Named as the button it is, unlike the figures beside it: the game draws a click target
                // in these five columns and the whole point of the column is what pressing it opens, so
                // the role word is the only thing that says the cell can be pressed at all.
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.ValuePart(() => _table.CellText(it)),
                    GraphNodes.DisabledPart(operable),
                },
                Sections = GraphNodes.Sections(() => _table.CellFacts(heading, it), tooltip),
                OnActivate = () =>
                {
                    if (operable())
                    {
                        AgeWidgets.PressPropagating(press);
                    }
                },
            };
            GraphNodes.AddRefusal(vtable, reason, operable);
            // The population column of another system is where a carried unit is SENT: the game's own
            // drag drops it here and the source system's spaceport ships it over.
            if (population != null)
            {
                GuiTableCellSystemPopulation at = population;
                vtable.DropKind = PopulationMoves.Kind;
                vtable.DropAccepts = held => Shippable(at, held) != null;
                vtable.OnDrop = held => Ship(at, held);
            }

            // Its own "unavailable" already covers the row's, since the row's own answer is one of the
            // three this cell asks - so the sheet leaves the shared one off (TableSheet.SaysRowRefusal).
            return _table.SaysRowRefusal(vtable);
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

        /// <summary>
        /// The automation policy column, where the game draws a DROP LIST rather than a readout - the
        /// same cell the system-selection window's table draws, and the same treatment: where the game
        /// leaves it operable the cell is a combo box and Enter opens the list, and a policy the game has
        /// switched off is a readout of what the system is doing instead.
        ///
        /// The list it opens is TITLED with the column, because that window is somewhere the player has
        /// been taken; the cell itself still does not say its own heading, which the crossed edge does.
        /// </summary>
        private NodeVtable Policy(AgeTransform cell, GuiTableHeader header, Func<bool> enabled)
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
                () => _table.CellFacts(heading, widget)
            );
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

        /// <summary>The button a cell of this table carries, where it has one the game is drawing. The
        /// resources column carries a dummy with no handler at all, which is not one of these: pressing
        /// it does what a click on any plain cell does, and that is the sheet's own job.</summary>
        private static AgeControlButton ActionButton(AgeTransform cell)
        {
            try
            {
                if (cell == null)
                {
                    return null;
                }

                AgeControlButton button = cell.GetComponentInChildren<AgeControlButton>(true);
                return button != null
                    && !string.IsNullOrEmpty(button.OnActivateMethod)
                    && button.AgeTransform.Visible
                    ? button
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where a refused cell's reason is written. The construction column puts its sentence
        /// on the label it draws INSTEAD of the construction ("this system is an outpost"), not on the
        /// cell, so the first tooltip with words in it under the cell is the one that answers.</summary>
        private static AgeTooltip RefusalTooltip(AgeTransform cell)
        {
            try
            {
                IList<AgeTransform> children = cell == null ? null : cell.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (child == null || !child.Visible)
                    {
                        continue;
                    }

                    AgeTooltip tooltip = AgeWidgets.Raw(child);
                    if (tooltip != null && AgeWidgets.Readable(tooltip) != null)
                    {
                        return tooltip;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        // ---- the panels a cell slides out ----

        /// <summary>What the table has under it - the four things a cell can slide out, as the player
        /// meets them. The construction cell shows the game's constructibles and queue panels side by
        /// side, which is one thing to press a cell for and so one member here. The status and
        /// population cells slide out the SAME cards panel in two drawn modes
        /// (<c>StarSystemsManagementPanel.OnLineSelection</c> :311-318 picks by clicked cell,
        /// <c>PlanetCard.DisplayMode</c>): actions, with the colonize/specialization/terraform buttons
        /// on each card, and population, with the rings and no buttons at all - so they are two members
        /// here, because a player told only "planets panel" cannot know which of two different panels
        /// arrived, or that pressing the OTHER cell is what brings out the buttons.</summary>
        private enum Detail
        {
            None,
            PlanetActions,
            PlanetPopulation,
            Construction,
            Hangar,
        }

        /// <summary>
        /// A panel arriving under the table, or going away, is announced - the same reason the fleet
        /// panel announces itself: the page does not change, so a whole Tab stop's worth of content
        /// appears and disappears under the player with nothing to hear but Tab taking longer to come
        /// round. Queued rather than interrupting: the player pressed the cell, and cutting off the
        /// cell's own readout would take away the answer they asked for.
        ///
        /// A SWAP - another cell, or the same cell on another row - says only the new opening. The
        /// panels change over in one frame (<c>StarSystemsManagementPanel.OnLineSelection</c> :285-311
        /// hides all of them and shows the one the clicked cell stands for), so there is no closed state
        /// in between to report, and "closed, open" would say twice over what one sentence already says.
        /// </summary>
        private void WatchDetails()
        {
            try
            {
                string system;
                Detail now = DrawnDetail(out system);
                if (now == _detail && system == _detailSystem)
                {
                    return;
                }

                Detail was = _detail;
                _detail = now;
                _detailSystem = system;
                Voice.Say(
                    now == Detail.None
                        ? ModStrings.Get(ClosedPhrase(was))
                        : ModStrings.Format(OpenedPhrase(now), system),
                    false
                );
            }
            catch (Exception e)
            {
                Log.Warn("empire: watching the panel under the table threw: " + e);
            }
        }

        /// <summary>
        /// Which panel the game has under the table, and the system it is showing.
        ///
        /// Asked of <c>Shown</c> rather than of the drawn flag the graph build uses: a panel on its way
        /// out stays Visible for the length of its fade while its replacement is already up, so a swap
        /// read off Visible would announce the panel the player just left before announcing the one they
        /// asked for. <c>Shown</c> goes false the frame Hide is called (<c>GuiPanel.OnBeginHide</c> sets
        /// Hiding), which is the frame the swap happens on.
        ///
        /// The system is the table's own selected row, which is where the game itself reads it from when
        /// it binds any of these panels (<c>ShowStarSystemPlanetCardsPanelWithActions</c> and its three
        /// siblings, :342-378) - and the hangar panel, unlike the other two, keeps no system of its own
        /// to ask.
        /// </summary>
        private Detail DrawnDetail(out string system)
        {
            system = null;
            global::EmpireScreen window = Window();
            StarSystemsManagementPanel panel =
                window == null ? null : window.StarSystemsManagementPanel;
            if (panel == null || !panel.Shown)
            {
                return Detail.None;
            }

            Detail detail = Detail.None;
            StarSystemPlanetCardsPanel cards = panel.StarSystemPlanetCardsPanel;
            StarSystemConstructiblePanel constructibles =
                Child<StarSystemConstructiblePanel>(panel.ConstructiblePanelContainer);
            StarSystemQueuePanel queue = Child<StarSystemQueuePanel>(panel.QueuePanelContainer);
            StarSystemHangarPanel hangar = Child<StarSystemHangarPanel>(panel.HangarPanelContainer);
            // Which of the three is up - the game shows exactly one at a time. The cards panel is
            // asked its MODE as well: the same panel is two different things to the player.
            if (cards != null && cards.Shown)
            {
                detail = cards.Mode == PlanetCard.DisplayMode.Actions
                    ? Detail.PlanetActions
                    : Detail.PlanetPopulation;
            }
            else if (
                (constructibles != null && constructibles.Shown)
                || (queue != null && queue.Shown)
            )
            {
                detail = Detail.Construction;
            }
            else if (hangar != null && hangar.Shown)
            {
                detail = Detail.Hangar;
            }

            if (detail != Detail.None)
            {
                system = SystemName(panel.GuiTable == null ? null : panel.GuiTable.SelectedLine);
            }

            return detail;
        }

        private static string OpenedPhrase(Detail detail)
        {
            switch (detail)
            {
                case Detail.Construction:
                    return ModStrings.EmpireConstructionPanelOpened;
                case Detail.Hangar:
                    return ModStrings.EmpireHangarPanelOpened;
                case Detail.PlanetActions:
                    return ModStrings.EmpirePlanetActionsPanelOpened;
                default:
                    return ModStrings.EmpirePopulationPanelOpened;
            }
        }

        private static string ClosedPhrase(Detail detail)
        {
            switch (detail)
            {
                case Detail.Construction:
                    return ModStrings.EmpireConstructionPanelClosed;
                case Detail.Hangar:
                    return ModStrings.EmpireHangarPanelClosed;
                case Detail.PlanetActions:
                    return ModStrings.EmpirePlanetActionsPanelClosed;
                default:
                    return ModStrings.EmpirePopulationPanelClosed;
            }
        }

        /// <summary>Whichever of the four panels the last cell click opened, in the order they are
        /// drawn. Only one kind is ever up: the game hides all of them before showing the one the
        /// clicked cell stands for, and the construction cell shows two side by side.</summary>
        private void BuildDetails(GraphBuilder builder, StarSystemsManagementPanel panel)
        {
            try
            {
                StarSystemPlanetCardsPanel cards = panel.StarSystemPlanetCardsPanel;
                // Flow control: each of these four readings descends a panel of its own.
                if (cards != null && AgeWidgets.Visible(cards.AgeTransform))
                {
                    builder.BeginStop(PlanetsStop);
                    builder.PushContext(ModStrings.Get(ModStrings.SystemPlanetsPanel));
                    BuildCards(builder, cards);
                    builder.PopContext();
                }

                StarSystemConstructiblePanel constructibles =
                    Child<StarSystemConstructiblePanel>(panel.ConstructiblePanelContainer);
                // Flow control: a stop and a context would be opened around nothing, and the shared
                // reading walks the whole panel.
                if (constructibles != null && AgeWidgets.Visible(constructibles.AgeTransform))
                {
                    builder.BeginStop(ConstructiblesStop);
                    builder.PushContext(ModStrings.Get(ModStrings.SystemConstructiblesPanel));
                    SystemPanels.Constructibles(builder, constructibles, Keys);
                    builder.PopContext();
                }

                StarSystemQueuePanel queue = Child<StarSystemQueuePanel>(panel.QueuePanelContainer);
                // Flow control: same, for the construction queue.
                if (queue != null && AgeWidgets.Visible(queue.AgeTransform))
                {
                    builder.BeginStop(QueueStop);
                    builder.PushContext(ModStrings.Get(ModStrings.SystemQueuePanel));
                    SystemPanels.Queue(builder, queue, Keys);
                    builder.PopContext();
                }

                StarSystemHangarPanel hangar = Child<StarSystemHangarPanel>(panel.HangarPanelContainer);
                // Flow control: same, for the hangar.
                if (hangar != null && AgeWidgets.Visible(hangar.AgeTransform))
                {
                    builder.BeginStop(HangarStop);
                    builder.PushContext(ModStrings.Get(ModStrings.SystemHangarPanel));
                    SystemPanels.Hangar(builder, hangar, Keys);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading the panel under the table threw: " + e);
            }
        }

        private static T Child<T>(AgeTransform container)
            where T : UnityEngine.Component
        {
            try
            {
                return container == null ? null : container.GetComponentInChildren<T>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The planets of the selected system, left to right - which is NOT the order the panel holds
        /// them in: it lays its cards out from the right, so the system's first planet is the rightmost
        /// card. Measured rather than assumed.
        ///
        /// The card is a readout with its own buttons as child nodes, the same shape the star system
        /// page's cards have, and its population ring is a row per SLOT that a unit can be carried off
        /// or dropped onto - the same gesture and the same shared arithmetic the star system page's
        /// ring uses, through this panel's own drop client.
        /// </summary>
        private void BuildCards(GraphBuilder builder, StarSystemPlanetCardsPanel panel)
        {
            _cards.Clear();
            try
            {
                PlanetCard[] all = panel.GetComponentsInChildren<PlanetCard>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    // The kept cards are SORTED by rectangle below and read in that order, so a card
                    // the panel is not drawing must not be in the list - its stale rectangle would
                    // reorder the ones that are.
                    if (all[i] != null && AgeWidgets.Visible(all[i].AgeTransform) && all[i].Planet != null)
                    {
                        _cards.Add(all[i]);
                    }
                }

                _cards.Sort(LeftToRight);
                for (int i = 0; i < _cards.Count; i++)
                {
                    AddCard(builder, _cards[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading the planet cards threw: " + e);
            }
        }

        private static readonly Comparison<PlanetCard> LeftToRight = (left, right) =>
            left.AgeTransform.GetGlobalPosition().x.CompareTo(
                right.AgeTransform.GetGlobalPosition().x
            );

        private void AddCard(GraphBuilder builder, PlanetCard card)
        {
            PlanetCard it = card;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.PlanetNameLabel)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlanetTypeLabel)),
                    // The same sentence the map's planet rows say, for the same reason: the game keeps
                    // a mining probe in the planet's dossier, where only a hover finds it.
                    GraphNodes.ValuePart(() => MiningProbes.Line(it.Planet), false),
                },
                Sections = GraphNodes.Sections(
                    () => CardDetails(it),
                    AgeWidgets.Raw(it.AgeTransform)
                ),
            };
            // The card itself takes NO drop: the drop lives on its SLOTS, for the reason the star
            // system page's cards do (owner ruling 2026-08-29) - a header that also swallowed drops
            // made two rows out of one gesture.
            AgeWidgets.PointAt(vtable, card.AgeTransform);

            string key = "empire:planet/" + card.Planet.GUID;
            ControlId id = ControlId.For(card.Planet, key);
            List<CardActions.CardAction> buttons = CardButtons(card);
            List<Population> units = new List<Population>(4);
            List<PopulationSlots.Slot> slots = CardSlots(card, units);
            if (buttons.Count == 0 && slots.Count == 0)
            {
                // Synthetic: the card stands for the PLANET, and the walk that found the planet is
                // what vouches for it.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            // Synthetic for the same reason as the leaf above: the card stands for the planet.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                AddPopulations(builder, key, card, units, slots, CanCarry(card));
                CardActions.Emit(builder, key, buttons);
            }

            builder.EndGroup();
        }

        /// <summary>Which of the card's own buttons the game is drawing. Only colonizing has a word of
        /// the mod's - it is the same wordless button the star system page draws and the same word is
        /// used for it; the other three name themselves in the sentence they explain themselves with.
        /// </summary>
        private static List<CardActions.CardAction> CardButtons(PlanetCard card)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(2);
            try
            {
                CardActions.AddNamedByMod(found, card.ColonizeButton, ModStrings.SystemColonize);
                // Refusable, not merely named: the game keeps these three drawn while switched off,
                // with the reason appended to the naming tooltip, so a blocked one is declared
                // refusing rather than vanishing.
                CardActions.AddRefusableNamedByTooltip(found, card.BuildInfrastructureButton);
                CardActions.AddRefusableNamedByTooltip(found, card.ReduceAnomalyButton);
                CardActions.AddRefusableNamedByTooltip(found, card.TerraformButton);
                AddCuriosities(found, card);
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading a planet card's buttons threw: " + e);
            }

            return found;
        }

        /// <summary>The curiosities the card is drawing, each a wordless icon kept CLICKABLE while
        /// refused with the reason in its own tooltip, named off the wrapper the game hangs there.
        /// </summary>
        private static void AddCuriosities(List<CardActions.CardAction> found, PlanetCard card)
        {
            IList<AgeTransform> items = AgeWidgets.DrawnChildren(card.CuriosityItemsTable);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                // The collected actions are NUMBERED by their place in the list CardActions.Emit
                // builds, and the number is each node's structural key - so an item the card is not
                // drawing must never enter it.
                if (
                    item != null
                    && AgeWidgets.Visible(item)
                    && item.GetComponent<PlanetCuriosityItem>() != null
                )
                {
                    CardActions.AddRefusable(found, item, CardActions.TitleOf(item));
                }
            }
        }

        /// <summary>Everything else the card draws, in the order it draws it: what kind of world it is,
        /// what has been found on it, and its five outputs.</summary>
        private static IList<string> CardDetails(PlanetCard card)
        {
            List<string> lines = new List<string>();
            try
            {
                AddWidgetLines(lines, card.PlanetTypeGroup);
                AddWidgetLines(lines, card.PlanetGameplayTypesTable);
                AddWidgetLines(lines, card.ResourceDepositItemsTable);
                AddWidgetLines(lines, card.AnomalyItemsTable);
                AddFidsi(lines, card);
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading a planet card's details threw: " + e);
            }

            return lines;
        }

        /// <summary>The planet's five outputs, named by the game's own property titles, in the two
        /// shapes the card draws them in. A COLONY's are written as numbers and read as numbers, off
        /// the same simulation object the card reads them from. A world nobody has settled gets no
        /// numbers at all: the card hides that row and draws a table of rating pips instead
        /// (<c>PlanetCard.Bind</c> :231-242, <c>RefreshScoreLine</c> :395-402), which the map's card
        /// and the management page's do too, so the lines of both shapes are composed for all three
        /// in <see cref="PlanetOutputs"/>. Which shape is drawn is the game's own bind-time test -
        /// settled, or a colonization the player has already ordered - so it is the test here rather
        /// than the card's own <c>ColonizedPlanet</c>: a world with a colonization pending is drawn
        /// with the numbers of the colony it is about to be, and that field is still null for it.
        /// </summary>
        private static void AddFidsi(List<string> lines, PlanetCard card)
        {
            FidsiEnumerator fidsi = card.FidsiEnumerator;
            if (fidsi == null || fidsi.FidsiProperties == null || card.Planet == null)
            {
                return;
            }

            if (card.Planet.ColonizedPlanet == null && card.PlayerGhostColonizedPlanet == null)
            {
                IList<string> ratings = PlanetOutputs.Ratings(
                    card.Planet,
                    fidsi,
                    card.FidsiParametersGuiElement
                );
                for (int i = 0; i < ratings.Count; i++)
                {
                    lines.Add(ratings[i]);
                }

                return;
            }

            ColonizedPlanet colony = card.ColonizedPlanet;
            Amplitude.Unity.Simulation.SimulationObject simulation =
                colony != null ? colony.SimulationObject : card.Planet.SimulationObject;
            if (simulation == null)
            {
                return;
            }

            IList<string> numbers = PlanetOutputs.Numbers(simulation, fidsi);
            for (int i = 0; i < numbers.Count; i++)
            {
                lines.Add(numbers[i]);
            }
        }

        /// <summary>The SLOTS of the ring the card draws in its population mode - contents from the
        /// colony, existence from the drawing, off the arithmetic both pages share
        /// (<see cref="PopulationMoves.Slots"/>).</summary>
        private static List<PopulationSlots.Slot> CardSlots(PlanetCard card, List<Population> units)
        {
            try
            {
                return PopulationMoves.Slots(
                    card.Planet,
                    card.ColonizedPlanet,
                    DrawnMarkers(card),
                    units
                );
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading a planet card's population slots threw: " + e);
                return new List<PopulationSlots.Slot>();
            }
        }

        /// <summary>How many markers the ring the card is DRAWING is showing. The ring retires a marker
        /// by HIDING it rather than by fading it
        /// (<c>PopulationEnumerator.HideAllPopulationMarkers</c>), which the shared child test covers as
        /// well: it asks the visibility flag first.</summary>
        private static int DrawnMarkers(PlanetCard card)
        {
            PlanetPopulationEnumerator enumerator = card.PlanetCardPopulationEnumerator;
            return enumerator == null
                ? 0
                : AgeWidgets.DrawnCount(
                    enumerator.PopMarkersContainer ?? enumerator.AgeTransform
                );
        }

        /// <summary>
        /// A row per SLOT of the ring the card draws, in the three bands it draws them in - the same
        /// model the star system page's cards use, off the same shared arithmetic
        /// (<see cref="PopulationMoves.Slots"/>).
        ///
        /// It was a row per AFFINITY until 2026-08-29, which said who lived on the world and nothing
        /// about how much room there was - the question the ring is on the card to answer - and gave a
        /// player no way to hear that the first marker of a run carries five people and the last one.
        ///
        /// <paramref name="canCarry"/> is where the game would let a drag start off this card AND this
        /// page has somewhere to put the unit down. One press carries what the game's own drag would
        /// carry from that marker, and what is carried is captured then, because the row is rebuilt
        /// every frame.
        /// </summary>
        private static void AddPopulations(
            GraphBuilder builder,
            string keyPrefix,
            PlanetCard card,
            List<Population> units,
            List<PopulationSlots.Slot> slots,
            bool canCarry
        )
        {
            if (slots.Count == 0)
            {
                return;
            }

            ColonizedPlanet colony = Settled(card);
            object outer = builder.Region;
            int total = slots.Count;
            bool inBand = false;
            PopulationSlots.Band band = PopulationSlots.Band.Population;
            try
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    PopulationSlots.Slot slot = slots[i];
                    if (!inBand || band != slot.Kind)
                    {
                        if (inBand)
                        {
                            builder.PopContext();
                        }

                        band = slot.Kind;
                        inBand = true;
                        builder.SetRegion(keyPrefix + "/population/" + band);
                        builder.PushContext(PopulationMoves.BandName(band));
                    }

                    AddPopulationSlot(builder, keyPrefix, card, colony, units, slot, total, canCarry);
                }
            }
            finally
            {
                if (inBand)
                {
                    builder.PopContext();
                }

                builder.SetRegion(outer);
            }
        }

        /// <summary>One slot of the card's ring: where it is, who is in it, and - on a filled slot of
        /// the player's own colony - the carry and the SWAP the game puts on that marker.</summary>
        private static void AddPopulationSlot(
            GraphBuilder builder,
            string keyPrefix,
            PlanetCard card,
            ColonizedPlanet colony,
            List<Population> units,
            PopulationSlots.Slot slot,
            int total,
            bool canCarry
        )
        {
            Population unit = slot.Unit >= 0 && slot.Unit < units.Count ? units[slot.Unit] : null;
            bool empty = unit == null && slot.Kind != PopulationSlots.Band.Locked;
            bool vacant = colony == null && empty;
            int rank = slot.Rank;
            int outOf = total;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () =>
                            vacant
                                ? ModStrings.Get(ModStrings.SystemPopulationSlotVacant)
                                : ModStrings.Format(
                                    empty
                                        ? ModStrings.SystemPopulationSlotEmpty
                                        : ModStrings.SystemPopulationSlot,
                                    rank,
                                    outOf
                                )
                    ),
                    GraphNodes.ValuePart(() => unit == null ? null : PopulationName(unit)),
                },
            };

            // Every non-locked slot of the player's own ring takes a drop, and who is standing in it
            // decides which kind: an occupied slot is the game's SWAP, an empty one the plain add the
            // card itself takes. Same rule and same reason as the star system page's ring - the free
            // place is where a player carrying somebody aims.
            if (colony != null && slot.Kind != PopulationSlots.Band.Locked)
            {
                if (canCarry && unit != null)
                {
                    ColonizedPlanet source = colony;
                    Population held = unit;
                    int carried = PopulationMoves.Carried(units, slot.Unit);
                    vtable.OnPickUp = () => PopulationMoves.Pick(source, held, carried);
                }

                PlanetCard it = card;
                StaticString replaced = unit == null ? StaticString.Empty : unit.Affinity;
                vtable.DropKind = PopulationMoves.Kind;
                vtable.DropAccepts = cargo => Accepts(it, cargo);
                vtable.OnDrop = cargo => DropOnCard(it, cargo, replaced);
            }

            // Synthetic: a slot is read out of the colony's own model - the ring draws a marker per
            // unit and nothing per empty slot - so the enumeration is the honesty here.
            builder.AddItem(
                Nodes.Synthetic(
                    ControlId.Structural(keyPrefix + "/population/" + slot.Rank),
                    vtable
                )
            );
        }

        // ---- moving a population unit ----

        /// <summary>The game's own word for an affinity.</summary>
        private static string PopulationName(Population population)
        {
            try
            {
                return AgeText.Clean(Gui.GetLocalizedTitle(population.Affinity));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The colony this card is for, or null - the card of an unsettled world, or of somebody
        /// else's colony, is neither a source nor a target.</summary>
        private static ColonizedPlanet Settled(PlanetCard card)
        {
            try
            {
                ColonizedPlanet colony = card == null ? null : card.ColonizedPlanet;
                return colony != null && colony.Empire == Gui.PlayerEmpire ? colony : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether a unit can be picked up off this card.
        ///
        /// Two questions, both the game's. Would the drag START here: the markers are interactive only
        /// while the system may move population at all (<c>PlanetPopulationEnumerator.Bind</c> :46) and the
        /// drag itself is refused unless the colony is the player's and may move population
        /// (<c>PopulationEnumerator.OnPopulationMarkerDragStarted</c> :240-252). And is there anywhere on
        /// THIS page to put it: another colony of the same system - the cards beside this one - or another
        /// colonized system in the table, which the source system's spaceport ships it to
        /// (<c>StarSystemPlanetCardsPanel.UpdateDrag</c> :167-205 does both).
        /// </summary>
        private static bool CanCarry(PlanetCard card)
        {
            try
            {
                ColonizedPlanet colony = Settled(card);
                PlanetPopulationEnumerator markers =
                    card == null ? null : card.PlanetCardPopulationEnumerator;
                AgeTransform container = markers == null ? null : markers.PopMarkersContainer;
                if (
                    colony == null
                    || !colony.CanMovePopulation
                    || !AgeWidgets.Operable(container)
                )
                {
                    return false;
                }

                return PopulationMoves.OnEmpirePage(colony.ColonizedStarSystem);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether this card would take the carried unit - the game's own
        /// <c>PlanetPopulationEnumerator.CanAcceptPopulationDrop</c>, asked through the render-safe fill
        /// (<see cref="PopulationMoves.Accepts"/>) rather than re-derived here.
        ///
        /// It used to be a copy of that check's three clauses with the quantity hardcoded to one, which
        /// is exactly the shape that goes wrong when the carry grows a quantity: the copy would still be
        /// asking whether ONE unit fits while the player held three. The game's own answer is asked
        /// instead, and it is what keeps the card the unit is being carried OFF from advertising itself.
        /// </summary>
        private static bool Accepts(PlanetCard card, CarryItem held)
        {
            Population population = held == null ? null : held.Cargo as Population;
            return population != null
                && Settled(card) != null
                // This page's drop moves people from one PLANET to another (the panel's own
                // transfer), so a unit with no planet behind it - one waiting in a spaceport - is
                // refused by the drop and must therefore be refused by the gate as well. Not
                // reachable today, since a carry ends when the player leaves the page it started on,
                // but the two halves have to answer the same question whether or not anything asks.
                && PopulationMoves.PlanetOf(population) != null
                && PopulationMoves.Accepts(
                    card.PlanetCardPopulationEnumerator,
                    population,
                    held.Quantity
                );
        }

        /// <summary>
        /// Put a carried unit on this card, the way the game's own drag does it: its
        /// <c>PopulationEnumerator.DragInfo</c> is filled in exactly as
        /// <c>OnPopulationMarkerDragStarted</c> fills it, the target card's own
        /// <c>CanAcceptPopulationDrop</c> decides, and the panel's own
        /// <c>TransferDraggedPopulationToPlanet</c> posts the order - which is what keeps the exact
        /// <c>OrderTransferPopulationFromPlanetToPlanet</c> the game builds
        /// (<c>StarSystemPlanetCardsPanel</c> :245-260, private, so it is reached by reflection).
        ///
        /// The drag info is cleared whatever happens: it is a static the game's own refresh reads every
        /// frame to draw a unit as already gone, and a stale one would empty a marker the player is
        /// still looking at.
        /// </summary>
        private static DropResult DropOnCard(
            PlanetCard card,
            CarryItem item,
            StaticString replaced
        )
        {
            Population population = item == null ? null : item.Cargo as Population;
            ColonizedPlanet destination = Settled(card);
            ColonizedPlanet source = Owner(population);
            PlanetPopulationEnumerator markers =
                card == null ? null : card.PlanetCardPopulationEnumerator;
            StarSystemPlanetCardsPanel panel = Cards(card);
            if (
                population == null
                || destination == null
                || source == null
                || markers == null
                || panel == null
            )
            {
                return DropResult.Refused(null);
            }

            try
            {
                try
                {
                    PopulationMoves.Fill(
                        source,
                        population,
                        item.Quantity,
                        replaced,
                        true
                    );
                    if (!markers.CanAcceptPopulationDrop())
                    {
                        return DropResult.Refused(null);
                    }

                    if (!Transfer(panel, destination))
                    {
                        return DropResult.Refused(null);
                    }
                }
                finally
                {
                    PopulationMoves.Clear();
                }

                // Planet to planet the whole carry moves: the game swaps the surplus back rather than
                // dropping it (DepartmentOfTheInterior.TransferPopulationFromPlanetToPlanet).
                return DropResult.Done(
                    ModStrings.Format(
                        ModStrings.SystemPopulationMoved,
                        PopulationMoves.Name(population, item.Quantity),
                        AgeText.Clean(destination.LocalizedName)
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("empire: moving a population unit threw: " + e);
                return DropResult.Refused(null);
            }
        }

        /// <summary>Hands the carried unit to the panel's own transfer, and says whether the game had
        /// one to hand it to: a patch that renames the method is a refusal here rather than an
        /// exception, which is the one failure policy <see cref="GameHandlers"/> gives every screen
        /// that reaches into the game this way.</summary>
        private static bool Transfer(
            StarSystemPlanetCardsPanel panel,
            ColonizedPlanet destination
        )
        {
            MethodInfo method = GameHandlers.Method(
                typeof(StarSystemPlanetCardsPanel),
                "TransferDraggedPopulationToPlanet"
            );
            if (method == null)
            {
                return false;
            }

            method.Invoke(panel, new object[] { destination });
            return true;
        }

        private static StarSystemPlanetCardsPanel Cards(PlanetCard card)
        {
            try
            {
                return card == null
                    ? null
                    : card.GetComponentInParent<StarSystemPlanetCardsPanel>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Which colony of the empire the carried unit lives on. Found rather than remembered:
        /// what is carried is the game's own <c>Population</c>, and a unit picked up on the star system
        /// page and dropped here has to be found from scratch.</summary>
        private static ColonizedPlanet Owner(Population population)
        {
            return PopulationMoves.PlanetOf(population);
        }

        /// <summary>Where a carried unit would be SENT by dropping it on this system's population cell,
        /// or null where the game would not take it: its own drop asks for a colonized system of the
        /// player's other than the one the unit is on, and a spaceport at that one able to ship
        /// (<c>StarSystemPlanetCardsPanel.UpdateDrag</c> :183-199).</summary>
        private static ColonizedStarSystem Shippable(
            GuiTableCellSystemPopulation cell,
            CarryItem item
        )
        {
            try
            {
                Population population = item == null ? null : item.Cargo as Population;
                GuiColonizedStarSystem wrapper = cell == null ? null : cell.GuiColonizedStarSystem;
                ColonizedStarSystem destination =
                    wrapper == null ? null : wrapper.ColonizedStarSystem;
                ColonizedPlanet source = Owner(population);
                ColonizedStarSystem from = source == null ? null : source.ColonizedStarSystem;
                if (
                    destination == null
                    || from == null
                    || destination.GUID == from.GUID
                    || destination.State != StarSystemState.Colony
                )
                {
                    return null;
                }

                Spaceport port = from.Spaceport;
                if (port == null || !port.IsAvailable())
                {
                    return null;
                }

                // The people board the SOURCE system's spaceport first and a ship carries them on
                // from there, so a FULL port ships nobody however much room the destination has -
                // the order is posted, the port clamps it to nothing
                // (<c>Spaceport.TransferPopulation</c> :191) and the game's own mouse drop is a
                // silent no-op. The room has to be asked HERE and not only at the drop, because this
                // is also what decides whether the row says "drop target": a row that advertises and
                // then refuses is worse than a row that stays quiet (owner-reported 2026-08-29).
                return PopulationMoves.IntoPort(port, null, item.Quantity) > 0 ? destination : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Send the carried unit to this system, which is what the game's own drop on this cell does: the
        /// source system's spaceport is pointed at the destination and then ordered to ship
        /// (<c>StarSystemPlanetCardsPanel.UpdateDrag</c> :183-199).
        ///
        /// The game sets the destination BEFORE asking whether a ship can leave, so a refusal there
        /// leaves the spaceport pointed somewhere new. The three reasons that have nothing to do with the
        /// destination (frozen, besieged, blockaded) are therefore asked FIRST and refuse in the game's
        /// own words, changing nothing; the destination is only set once the answer is yes.
        /// </summary>
        private static DropResult Ship(GuiTableCellSystemPopulation cell, CarryItem item)
        {
            ColonizedStarSystem destination = Shippable(cell, item);
            Population population = item == null ? null : item.Cargo as Population;
            ColonizedPlanet source = Owner(population);
            Spaceport port =
                source == null || source.ColonizedStarSystem == null
                    ? null
                    : source.ColonizedStarSystem.Spaceport;
            if (destination == null || port == null)
            {
                return DropResult.Refused(null);
            }

            try
            {
                StaticString failure;
                if (!port.CanCreateAutomatedShip(out failure) && !IsNoDestination(failure))
                {
                    return DropResult.Refused(FailureText(failure));
                }

                port.SetDestination(destination);
                if (!port.CanCreateAutomatedShip(out failure))
                {
                    return DropResult.Refused(FailureText(failure));
                }

                // The order puts the carried people into the source system's own spaceport, which
                // CLAMPS against its free room and never refuses (Spaceport.TransferPopulation :191) -
                // so what is said is what will really board.
                int moved = PopulationMoves.IntoPort(port, null, item.Quantity);
                if (moved <= 0)
                {
                    return DropResult.Refused(null);
                }

                PlayerController controller = Gui.GetActivePlayerController();
                controller.PostOrder(
                    new OrderTransferSpaceportPopulation(
                        controller.Empire.Index,
                        population.Affinity,
                        item.Quantity,
                        source.GUID
                    )
                );
                return DropResult.Done(
                    ModStrings.Format(
                        ModStrings.EmpirePopulationSent,
                        PopulationMoves.Name(population, moved),
                        AgeText.Clean(destination.LocalizedName)
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("empire: sending a population unit threw: " + e);
                return DropResult.Refused(null);
            }
        }

        private static bool IsNoDestination(StaticString failure)
        {
            return !StaticString.IsNullOrEmpty(failure)
                && failure == FailureFlags.SpaceportNoDestination;
        }

        /// <summary>The game's own sentence for one of its failure flags, in the shape the game itself
        /// builds it (<c>AvailableModItem.Refresh</c> :77) - and nothing at all for a flag the string
        /// files never answered, which is parked text rather than a reason.</summary>
        private static string FailureText(StaticString failure)
        {
            try
            {
                if (StaticString.IsNullOrEmpty(failure))
                {
                    return null;
                }

                return AgeText.Title("%Failure" + failure.ToString() + "Description");
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the victory tab ----

        /// <summary>
        /// The victory wheel: one row per way of winning, in the order the wheel draws them - clockwise
        /// from the top, which is the order the panel builds its sectors in (measured).
        ///
        /// A row says what the race is and where the empire stands in it. A condition this game was set
        /// up without draws no rank at all and says so in its own sentence, which is on the tooltip
        /// where the game put it. The three figures around a sector's rim are its children.
        /// </summary>
        private void BuildVictory(GraphBuilder builder, global::EmpireScreen window)
        {
            VictoryAndPerformancePanel panel = window.VictoryAndPerformancePanel;
            AgeTransform container = panel == null ? null : panel.VictorySectorsContainer;
            if (container == null)
            {
                return;
            }

            builder.BeginStop(VictoryStop);
            string title = PanelTitle(panel);
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            try
            {
                IList<AgeTransform> sectors = container.Children;
                for (int i = 0; sectors != null && i < sectors.Count; i++)
                {
                    AddSector(builder, sectors[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading the victory wheel threw: " + e);
            }

            if (named)
            {
                builder.PopContext();
            }
        }

        private void AddSector(GraphBuilder builder, AgeTransform widget, int index)
        {
            VictoryConditionSector sector =
                widget == null ? null : widget.GetComponent<VictoryConditionSector>();
            if (sector == null)
            {
                return;
            }

            VictoryConditionSector it = sector;
            AgeTooltip tooltip = AgeWidgets.Raw(AgeWidgets.Transform(sector.VictoryObjectives));
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.VictoryObjectives)),
                    GraphNodes.ValuePart(() => Rank(it)),
                },
                // The game writes a paragraph and the progress line into one plain tooltip, and it is
                // announced whole like every other plain one: the tooltip.s own kind decides, and this
                // page states no exception to it.
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, AgeWidgets.Transform(sector.VictoryObjectives) ?? widget);

            string key = "empire:victory/" + index;
            ControlId id = ControlId.For(widget, key);
            IList<AgeTransform> hexes = Hexes(sector);
            if (hexes == null || hexes.Count == 0)
            {
                builder.AddItem(Nodes.Drawn(id, vtable, widget));
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            builder.BeginGroup(Nodes.Drawn(id, vtable, widget));
            if (builder.IsExpanded(id))
            {
                for (int i = 0; i < hexes.Count; i++)
                {
                    AddHex(builder, sector, hexes[i], key, i);
                }
            }

            builder.EndGroup();
        }

        /// <summary>Where the empire stands in this race, in the game's own word for the place. A
        /// condition the game was not set up with draws no rank ring at all.</summary>
        private static string Rank(VictoryConditionSector sector)
        {
            try
            {
                return sector.VictoryRankGroup == null || !sector.VictoryRankGroup.Visible
                    ? null
                    : AgeText.Label(sector.VictoryRankValue);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IList<AgeTransform> Hexes(VictoryConditionSector sector)
        {
            try
            {
                AgeTransform container = sector.EmpirePerformanceContainer;
                // Shape, not existence: an empty answer makes the sector a plain readout instead of an
                // expandable group, so a hidden rim must not read as a group with nothing in it.
                return container == null || !AgeWidgets.Visible(container)
                    ? null
                    : container.Children;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One figure off a sector's rim. The hex draws a picture and a number and keeps its
        /// name in the game's own element registry, which is where the caption comes from; a tracker the
        /// corpus has no title for is left unnamed rather than read out as a key, and its own sentence -
        /// which the game does write - still says what the number counts.</summary>
        private void AddHex(
            GraphBuilder builder,
            VictoryConditionSector sector,
            AgeTransform widget,
            string keyPrefix,
            int index
        )
        {
            EmpirePerformanceHex hex =
                widget == null ? null : widget.GetComponent<EmpirePerformanceHex>();
            if (hex == null)
            {
                return;
            }

            EmpirePerformanceHex it = hex;
            string name = HexName(sector, index);
            NodeVtable vtable = GraphNodes.Readout(
                () => name,
                () => AgeText.Label(it.ValueLabel),
                null,
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(
                ControlId.For(widget, keyPrefix + "/hex/" + index),
                vtable,
                widget
            ));
        }

        /// <summary>The tracker's own title. The hex holds no reference to what it is drawing once it is
        /// bound, so the sector's own definition is asked for the list it built its hexes from - the
        /// same list, in the same order, that the game handed the container.</summary>
        private static string HexName(VictoryConditionSector sector, int index)
        {
            try
            {
                VictoryConditionDefinition definition =
                    SectorDefinition == null
                        ? null
                        : SectorDefinition.GetValue(sector) as VictoryConditionDefinition;
                EmpirePerformanceTracker[] trackers =
                    definition == null ? null : definition.VisibleEmpirePerformanceTrackers;
                if (trackers == null || index >= trackers.Length || trackers[index] == null)
                {
                    return null;
                }

                Amplitude.Unity.Gui.ExtendedGuiElement element =
                    Gui.GetExtendedGuiElement(trackers[index].Name);
                // A title the corpus never wrote comes back as its own key: parked text, which is not
                // a name to speak.
                return element == null ? null : AgeText.Title(element.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly FieldInfo SectorDefinition = GameHandlers.Field(
            typeof(VictoryConditionSector),
            "victoryConditionDefinition"
        );

        // ---- reading the window ----

        /// <summary>Whether the wheel is the tab being drawn. Asked of what is on screen rather than of
        /// the window's remembered tab, which other screens set on their way here.</summary>
        private static bool VictoryDrawn(global::EmpireScreen window)
        {
            try
            {
                return window != null
                    && window.VictoryAndPerformancePanel != null
                    // Flow control: whether the Victory tab's whole subtree is walked at all.
                    && AgeWidgets.Visible(window.VictoryAndPerformancePanel.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The heading a panel writes across its own top. Neither panel exposes the label, so
        /// it is found where it is drawn.</summary>
        private static string PanelTitle(GuiPanel panel)
        {
            try
            {
                AgeTransform title =
                    panel == null ? null : AgeWidgets.ChildNamed(panel.AgeTransform, "Title", 3);
                return title == null ? null : AgeWidgets.TextOf(title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AddWidgetLines(List<string> lines, AgeTransform widget)
        {
            // Content: which drawn lines are gathered into a reading.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            IList<string> drawn = AgeWidgets.DrawnLines(widget);
            for (int i = 0; drawn != null && i < drawn.Count; i++)
            {
                if (!string.IsNullOrEmpty(drawn[i]) && !lines.Contains(drawn[i]))
                {
                    lines.Add(drawn[i]);
                }
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

        /// <summary>What the row is called when the name column draws nothing - the system's own name.
        /// </summary>
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

        private static string ScreenTitle()
        {
            try
            {
                return AgeText.Clean(Gui.GetLocalizedTitle("EmpireScreen"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static global::EmpireScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<global::EmpireScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where this screen is drawn, for the tooltip audit (see
        /// <see cref="ES2Access.Screens.Screen.RootTransform"/>).</summary>
        public override AgeTransform RootTransform
        {
            get { return RootOf(Window()); }
        }
    }
}

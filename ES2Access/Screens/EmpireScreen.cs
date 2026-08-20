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
    /// empire is - is INDICATED rather than announced, this page's one deliberate departure from the
    /// short-tooltip rule: it is a paragraph, and the six rows are walked as a list.
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

        public EmpireScreen()
        {
            _table = new TableSheet(Keys, SystemOf);
            _table.RowName = SystemName;
            _table.ReadCell = ActionCell;
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
        }

        public override void OnPop()
        {
            _hud.Forget();
        }

        public override void OnUpdate()
        {
            _hud.Update();
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
            vtable.Announcements.Add(GraphNodes.ValuePart(() => RelicSlotAction(it)));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => RelicsAssigned(it)));
            GraphNodes.AddRefusal(vtable, tooltip, offered);
            AgeWidgets.Point(vtable, slot.button, tooltip, button);
            // The name and the position, because there are FOUR of these in one box and a key that named
            // only the panel is the same for all four - which throws Duplicate control id and empties the
            // WHOLE page. (Measured: that is how this was found. The clones happen to carry per-slot names
            // in this prefab, and the index is what makes the key safe if a later one does not.)
            Cells.Add(
                cells,
                widget,
                ControlId.Referenced(
                    slot,
                    keyPrefix
                        + "relic-slot/"
                        + widget.name
                        + "/"
                        + AgeWidgets.IndexInParent(widget)
                ),
                vtable
            );
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
                return AgeWidgets.Visible(slot.AssignRelicsGroup)
                    ? AgeText.Label(slot.AssignedRelicsLabel)
                    : null;
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
            if (widget == null || !AgeWidgets.Visible(widget))
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
            Cells.Add(cells, widget, ControlId.Referenced(widget, "empire:tab/" + index), vtable);
        }

        // ---- the systems tab ----

        /// <summary>The table of systems: the sort headers, the rows, and then whichever panel the last
        /// cell click slid out under them.</summary>
        private void BuildSystems(GraphBuilder builder, global::EmpireScreen window)
        {
            StarSystemsManagementPanel panel = window.StarSystemsManagementPanel;
            GuiTable table = panel == null ? null : panel.GuiTable;
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
                vtable.DropKind = PopulationKind;
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
                null,
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

        /// <summary>Whichever of the four panels the last cell click opened, in the order they are
        /// drawn. Only one kind is ever up: the game hides all of them before showing the one the
        /// clicked cell stands for, and the construction cell shows two side by side.</summary>
        private void BuildDetails(GraphBuilder builder, StarSystemsManagementPanel panel)
        {
            try
            {
                StarSystemPlanetCardsPanel cards = panel.StarSystemPlanetCardsPanel;
                if (cards != null && AgeWidgets.Visible(cards.AgeTransform))
                {
                    builder.BeginStop(PlanetsStop);
                    builder.PushContext(ModStrings.Get(ModStrings.SystemPlanetsPanel));
                    BuildCards(builder, cards);
                    builder.PopContext();
                }

                StarSystemConstructiblePanel constructibles =
                    Child<StarSystemConstructiblePanel>(panel.ConstructiblePanelContainer);
                if (constructibles != null && AgeWidgets.Visible(constructibles.AgeTransform))
                {
                    builder.BeginStop(ConstructiblesStop);
                    builder.PushContext(ModStrings.Get(ModStrings.SystemConstructiblesPanel));
                    SystemPanels.Constructibles(builder, constructibles, Keys);
                    builder.PopContext();
                }

                StarSystemQueuePanel queue = Child<StarSystemQueuePanel>(panel.QueuePanelContainer);
                if (queue != null && AgeWidgets.Visible(queue.AgeTransform))
                {
                    builder.BeginStop(QueueStop);
                    builder.PushContext(ModStrings.Get(ModStrings.SystemQueuePanel));
                    SystemPanels.Queue(builder, queue, Keys);
                    builder.PopContext();
                }

                StarSystemHangarPanel hangar = Child<StarSystemHangarPanel>(panel.HangarPanelContainer);
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
        /// page's cards have, and its population markers are a row per affinity that a unit can be
        /// carried off - the same gesture, through this panel's own drop client.
        /// </summary>
        private void BuildCards(GraphBuilder builder, StarSystemPlanetCardsPanel panel)
        {
            _cards.Clear();
            try
            {
                PlanetCard[] all = panel.GetComponentsInChildren<PlanetCard>(true);
                for (int i = 0; i < all.Length; i++)
                {
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
            // A colony of the player's is where a carried population unit can be put down - the same set
            // of cards the game's own drag offers as targets.
            if (Settled(card) != null)
            {
                vtable.DropKind = PopulationKind;
                vtable.DropAccepts = held => Accepts(it, held);
                vtable.OnDrop = held => DropOnCard(it, held);
            }

            AgeWidgets.PointAt(vtable, card.AgeTransform);

            string key = "empire:planet/" + card.Planet.GUID;
            ControlId id = ControlId.Referenced(card.Planet, key);
            List<CardActions.CardAction> buttons = CardButtons(card);
            List<Population> populations = Populations(card);
            if (buttons.Count == 0 && populations.Count == 0)
            {
                builder.AddItem(id, vtable);
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            builder.BeginGroup(id, vtable);
            if (builder.IsExpanded(id))
            {
                AddPopulations(builder, key, card, populations, CanCarry(card));
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
                CardActions.AddNamedByTooltip(found, card.BuildInfrastructureButton);
                CardActions.AddNamedByTooltip(found, card.ReduceAnomalyButton);
                CardActions.AddNamedByTooltip(found, card.TerraformButton);
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
            AgeTransform table = card.CuriosityItemsTable;
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
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

        /// <summary>The planet's five outputs, named by the game's own property titles and read off the
        /// same simulation object the card reads - the colony's where there is one, the planet's own
        /// potential where there is not.</summary>
        private static void AddFidsi(List<string> lines, PlanetCard card)
        {
            FidsiEnumerator fidsi = card.FidsiEnumerator;
            if (fidsi == null || fidsi.FidsiProperties == null || card.Planet == null)
            {
                return;
            }

            ColonizedPlanet colony = card.ColonizedPlanet;
            Amplitude.Unity.Simulation.SimulationObject simulation =
                colony != null ? colony.SimulationObject : card.Planet.SimulationObject;
            if (simulation == null)
            {
                return;
            }

            int count = Math.Min(fidsi.DisplayedProperties, fidsi.FidsiProperties.Count);
            for (int i = 0; i < count; i++)
            {
                GuiSimulationProperty property = fidsi.FidsiProperties[i];
                if (property == null)
                {
                    continue;
                }

                lines.Add(
                    new MessageBuilder()
                        .ListItem(AgeText.Clean(Gui.GetLocalizedTitle(property.Name)))
                        .ListItem(Amount(simulation.GetPropertyValue(property.Name)))
                        .Build()
                );
            }
        }

        /// <summary>Who lives on the planet, off the ring of markers the card draws in its population
        /// mode.</summary>
        private static List<Population> Populations(PlanetCard card)
        {
            List<Population> found = new List<Population>(2);
            try
            {
                PlanetPopulationEnumerator enumerator = card.PlanetCardPopulationEnumerator;
                if (enumerator == null || !AgeWidgets.Visible(enumerator.AgeTransform))
                {
                    return found;
                }

                AgeTransform container = enumerator.PopMarkersContainer ?? enumerator.AgeTransform;
                IList<AgeTransform> markers = container.Children;
                for (int i = 0; markers != null && i < markers.Count; i++)
                {
                    AgeTransform marker = markers[i];
                    if (marker == null || !marker.Visible)
                    {
                        continue;
                    }

                    PopulationMarker it = marker.GetComponent<PopulationMarker>();
                    Population population =
                        it == null || it.GuiPopulation == null ? null : it.GuiPopulation.Population;
                    if (population != null && !found.Contains(population))
                    {
                        found.Add(population);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("empire: reading a planet card's populations threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// A row per population on the card: the game's own name for the affinity and how many of them
        /// live here, which is what the ring of markers draws.
        ///
        /// <paramref name="canCarry"/> is where the game would let a drag start off this card AND this
        /// page has somewhere to put the unit down. One press carries ONE unit - the smallest move the
        /// game's own drag makes - and the name is captured then, because the row is rebuilt every frame.
        /// </summary>
        private static void AddPopulations(
            GraphBuilder builder,
            string keyPrefix,
            PlanetCard card,
            List<Population> populations,
            bool canCarry
        )
        {
            ColonizedPlanet colony = Settled(card);
            for (int i = 0; i < populations.Count; i++)
            {
                Population population = populations[i];
                NodeVtable vtable = GraphNodes.Readout(
                    () => PopulationName(population),
                    () => new MessageBuilder().PushQuantity(population.Count).Build(),
                    null,
                    null
                );
                if (canCarry && colony != null)
                {
                    ColonizedPlanet source = colony;
                    Population held = population;
                    vtable.OnPickUp = () => Pick(source, held);
                }

                builder.AddItem(
                    ControlId.Referenced(population, keyPrefix + "/population/" + i),
                    vtable
                );
            }
        }

        // ---- moving a population unit ----

        /// <summary>What the carried thing IS, shared with the star system page so a unit picked up
        /// there can be put down here.</summary>
        private const string PopulationKind = SystemManagementScreen.PopulationKind;

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
                    || container == null
                    || !container.Enable
                )
                {
                    return false;
                }

                return Elsewhere(colony.ColonizedStarSystem);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether this page offers anywhere to put a unit taken off <paramref name="from"/>: a
        /// second colony in the same system, or a spaceport with another colony to send it to.</summary>
        private static bool Elsewhere(ColonizedStarSystem from)
        {
            if (from == null)
            {
                return false;
            }

            if (from.PlanetsColonized.Count > 1)
            {
                return true;
            }

            Spaceport port = from.Spaceport;
            if (port == null || !port.IsAvailable())
            {
                return false;
            }

            DepartmentOfTheInterior interior =
                Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>();
            for (int i = 0; interior != null && i < interior.ColonizedStarSystems.Count; i++)
            {
                ColonizedStarSystem other = interior.ColonizedStarSystems[i];
                if (
                    other != null
                    && other.GUID != from.GUID
                    && other.State == StarSystemState.Colony
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>One unit of this population, picked up. Null where the game would not let the drag
        /// start: its own two tests are the colony's and the affinity's.</summary>
        private static CarryItem Pick(ColonizedPlanet source, Population population)
        {
            try
            {
                IPopulationsManagementService populations =
                    Services.GetService<IPopulationsManagementService>();
                if (
                    population.Count <= 0
                    || !source.CanMovePopulation
                    || populations == null
                    || !populations.CanMovePopulation(population.Affinity)
                )
                {
                    return null;
                }

                return new CarryItem(population, PopulationName(population), PopulationKind);
            }
            catch (Exception e)
            {
                Log.Warn("empire: picking a population unit up threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Whether this card would take the carried unit - the same three questions the game's own
        /// <c>PlanetPopulationEnumerator.CanAcceptPopulationDrop</c> asks, asked WITHOUT touching the
        /// static drag info it reads (that call throws with no drag in progress, and filling the static in
        /// to ask a speculative question would empty a marker the player is still looking at). So the
        /// planet a unit came OFF does not offer itself as a target.
        /// </summary>
        private static bool Accepts(PlanetCard card, CarryItem held)
        {
            try
            {
                ColonizedPlanet destination = Settled(card);
                Population population = held == null ? null : held.Cargo as Population;
                ColonizedPlanet source = Owner(population);
                if (
                    destination == null
                    || population == null
                    || source == null
                    || ReferenceEquals(destination, source)
                )
                {
                    return false;
                }

                return destination.CanWelcomeSomeOfPopulation(
                    new TransitingPopulation(population.Affinity, 1)
                );
            }
            catch (Exception)
            {
                return false;
            }
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
        private static DropResult DropOnCard(PlanetCard card, CarryItem item)
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
                PopulationEnumerator.PopulationDragInfo drag = PopulationEnumerator.DragInfo;
                drag.DragInProgress = true;
                drag.SourcePopulationOwner = source;
                drag.GuiPopulation = Wrap(source, population);
                drag.Quantity = 1;
                drag.TransitingPopulation = new TransitingPopulation(population.Affinity, 1);
                drag.ReplacedPopulationAffinity = StaticString.Empty;
                try
                {
                    if (!markers.CanAcceptPopulationDrop())
                    {
                        return DropResult.Refused(null);
                    }

                    Transfer(panel, destination);
                }
                finally
                {
                    drag.DragInProgress = false;
                    drag.SourcePopulationOwner = null;
                    drag.GuiPopulation = null;
                    drag.Quantity = 0;
                    drag.TransitingPopulation = null;
                    drag.ReplacedPopulationAffinity = StaticString.Empty;
                }

                return DropResult.Done(
                    ModStrings.Format(
                        ModStrings.SystemPopulationMoved,
                        item.Name,
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

        private static void Transfer(
            StarSystemPlanetCardsPanel panel,
            ColonizedPlanet destination
        )
        {
            MethodInfo method = typeof(StarSystemPlanetCardsPanel).GetMethod(
                "TransferDraggedPopulationToPlanet",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            if (method == null)
            {
                throw new MissingMethodException("TransferDraggedPopulationToPlanet");
            }

            method.Invoke(panel, new object[] { destination });
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

        /// <summary>The <c>GuiPopulation</c> the game's own drag carries, which is what the transfer reads
        /// the affinity out of.</summary>
        private static GuiPopulation Wrap(ColonizedPlanet source, Population population)
        {
            DepartmentOfTheInterior interior = source.Empire.GetAgency<DepartmentOfTheInterior>();
            PopulationEmpire empire =
                interior == null
                    ? null
                    : interior.GetPopulationByAffinity(population.Affinity) as PopulationEmpire;
            return new GuiPopulation(population, empire, source.Empire);
        }

        /// <summary>Which colony of the empire the carried unit lives on. Found rather than remembered:
        /// what is carried is the game's own <c>Population</c>, and a unit picked up on the star system
        /// page and dropped here has to be found from scratch.</summary>
        private static ColonizedPlanet Owner(Population population)
        {
            try
            {
                DepartmentOfTheInterior interior =
                    population == null
                        ? null
                        : Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>();
                for (int i = 0; interior != null && i < interior.ColonizedStarSystems.Count; i++)
                {
                    ColonizedStarSystem system = interior.ColonizedStarSystems[i];
                    for (int p = 0; system != null && p < system.PlanetsColonized.Count; p++)
                    {
                        ColonizedPlanet planet = system.PlanetsColonized[p];
                        Population held;
                        if (
                            planet != null
                            && planet.PopulationsByAffinity.TryGetValue(
                                population.Affinity,
                                out held
                            )
                            && ReferenceEquals(held, population)
                        )
                        {
                            return planet;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("empire: finding a carried unit's planet threw: " + e);
            }

            return null;
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
                return port != null && port.IsAvailable() ? destination : null;
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

                PlayerController controller = Gui.GetActivePlayerController();
                controller.PostOrder(
                    new OrderTransferSpaceportPopulation(
                        controller.Empire.Index,
                        population.Affinity,
                        1,
                        source.GUID
                    )
                );
                return DropResult.Done(
                    ModStrings.Format(
                        ModStrings.EmpirePopulationSent,
                        item.Name,
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

                string said = AgeText.Clean(
                    Gui.Localize("%Failure" + failure.ToString() + "Description")
                );
                return string.IsNullOrEmpty(said) || said[0] == '%' ? null : said;
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
            if (sector == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            VictoryConditionSector it = sector;
            AgeTooltip tooltip = AgeWidgets.Raw(Widget(sector.VictoryObjectives));
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.VictoryObjectives)),
                    GraphNodes.ValuePart(() => Rank(it)),
                },
                // The game writes a paragraph and the progress line into one plain tooltip. Indicated
                // rather than announced - this page's one departure from the short-tooltip rule - so
                // that walking the six rows is a list rather than six paragraphs.
                Sections = GraphNodes.Sections(null, tooltip, TooltipMode.Indicate),
            };
            AgeWidgets.PointAt(vtable, Widget(sector.VictoryObjectives) ?? widget);

            string key = "empire:victory/" + index;
            ControlId id = ControlId.Referenced(widget, key);
            IList<AgeTransform> hexes = Hexes(sector);
            if (hexes == null || hexes.Count == 0)
            {
                builder.AddItem(id, vtable);
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            builder.BeginGroup(id, vtable);
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
            if (hex == null || !AgeWidgets.Visible(widget))
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
            builder.AddItem(
                ControlId.Referenced(widget, keyPrefix + "/hex/" + index),
                vtable
            );
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
                string title =
                    element == null ? null : AgeText.Clean(Gui.Localize(element.Title));
                // A title the corpus never wrote comes back as its own key: parked text, which is not
                // a name to speak.
                return string.IsNullOrEmpty(title) || title[0] == '%' ? null : title;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly FieldInfo SectorDefinition = SectorDefinitionField();

        private static FieldInfo SectorDefinitionField()
        {
            try
            {
                return typeof(VictoryConditionSector).GetField(
                    "victoryConditionDefinition",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- reading the window ----

        /// <summary>Whether the wheel is the tab being drawn. Asked of what is on screen rather than of
        /// the window's remembered tab, which other screens set on their way here.</summary>
        private static bool VictoryDrawn(global::EmpireScreen window)
        {
            try
            {
                return window != null
                    && window.VictoryAndPerformancePanel != null
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

        private static AgeTransform Widget(AgePrimitiveLabel label)
        {
            try
            {
                return label == null ? null : label.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Amount(float value)
        {
            try
            {
                return Gui.FormatAmount(value, true, Gui.Rounding.Floor, false, 0);
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
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// the shared readers rather than modelled twice (<see cref="SystemPanels"/>). The one thing the
    /// player cannot do here that they can there is MOVE a population unit between planets: the drag
    /// this page's cards accept runs through a different drop client, so the markers are readouts and
    /// the move stays on the star system page.
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
        private static readonly object HeadersStop = "empire:headers";
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
                return gui != null && !gui.IsAnyModalVisible && !gui.IsInLoadingWindow;
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
            _hud.Turn(builder);
        }

        // ---- the panels down the left edge ----

        /// <summary>A stop per panel the game is drawing down the left edge, top to bottom, read off
        /// what is in them.</summary>
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
                    Cells.Emit(builder, _cells);
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
        /// The one readout in these panels the shape of the widget tree cannot name: how many systems
        /// the empire holds against how many it can hold before the rest start to suffer.
        ///
        /// The panel draws it as a bare "1/7" over a bar, and the sentence saying what the two numbers
        /// ARE is on the group around them - which the walk would otherwise descend past, leaving a
        /// fraction with nothing to say for itself. The game writes one of three sentences there
        /// depending on the empire (<c>EmpireStatusSidePanel.Refresh</c> :87-98), so the words are
        /// whatever it wrote this frame.
        /// </summary>
        private static bool SpecialCell(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
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
            Cells.Emit(builder, _cells);
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

            builder.BeginStop(HeadersStop);
            builder.PushContext(ModStrings.Get(ModStrings.EmpireHeadingsBand));
            _table.Headers(builder, table);
            builder.PopContext();

            builder.BeginStop(SystemsStop);
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
            NodeAnnouncement refusal = GraphNodes.RefusalPart(reason, operable);
            if (refusal != null)
            {
                vtable.Announcements.Add(refusal);
            }

            return vtable;
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
        /// page's cards have. Its population markers are readouts here: the drag that moves a unit
        /// between planets runs through the labels window on that page, so the move stays there.
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
                },
                Sections = GraphNodes.Sections(
                    () => CardDetails(it),
                    AgeWidgets.Raw(it.AgeTransform)
                ),
            };
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
                AddPopulations(builder, key, populations);
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
        /// mode. Readouts: the game's own drag between cards runs through a drop client this page does
        /// not have.</summary>
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

        private static void AddPopulations(
            GraphBuilder builder,
            string keyPrefix,
            List<Population> populations
        )
        {
            for (int i = 0; i < populations.Count; i++)
            {
                Population population = populations[i];
                NodeVtable vtable = GraphNodes.Readout(
                    () => AgeText.Clean(Gui.GetLocalizedTitle(population.Affinity)),
                    () => new MessageBuilder().PushQuantity(population.Count).Build(),
                    null,
                    null
                );
                builder.AddItem(
                    ControlId.Referenced(population, keyPrefix + "/population/" + i),
                    vtable
                );
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

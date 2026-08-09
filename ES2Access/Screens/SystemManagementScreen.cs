using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// One star system, opened up: the page the game takes the player to when they enter a colony of
    /// theirs, and where a 4X game is actually played.
    ///
    /// It is not a window either. The page is the game's StarSystemScreen with the planet labels drawn
    /// over the middle of it and the side panels drawn down the left, and none of the three knows about
    /// the others - so being ours is "is the camera at the system management view level", the same
    /// question the galaxy page asks about itself, rather than "is a window up".
    ///
    /// Tab walks the page in the order it is drawn: the planets across the middle, then the panels down
    /// the left edge, then the three panels along the bottom. Which side panels there are is the game's
    /// answer, not ours: it swaps whole sets of them by what the system IS - a colony, an outpost, the
    /// ghost of one - so this screen declares a stop for each panel it finds drawn and gets the
    /// switching for free.
    ///
    /// RIGHT IS NOT AN ACTION KEY here. The galaxy page taught the player that right opens a thing up,
    /// and this page is reached by pressing it; a control that also used right for "and here are the
    /// things you can do to me" would make the player know which of the two each control is. So a
    /// control with more than one action answers ENTER with a menu of them
    /// (<see cref="ChoiceSubmenuScreen"/>), and the menu is also where a control whose own click is
    /// destructive - cancelling a construction - or impossible without a mouse - dragging a population
    /// unit between planets - puts its actions, so that Enter is never a key you regret pressing.
    ///
    /// A planet card holds far more than a control's readout can carry - its type, its traits, its
    /// anomalies, its five outputs, and the game's own sentence about why it cannot be colonized yet -
    /// so the readout is its name and what state it is in, and all the rest is in the review buffer,
    /// which is what the review buffer is for.
    /// </summary>
    public sealed class SystemManagementScreen : Screen
    {
        private static readonly object PlanetStop = "system:planets";
        private static readonly object ConstructiblesStop = "system:constructibles";
        private static readonly object QueueStop = "system:queue";
        private static readonly object HangarStop = "system:hangar";

        /// <summary>The clusters the game draws over every view level. They are drawn over this page
        /// too, and until they were declared here they were on the screen and out of reach.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

        /// <summary>Reused across builds rather than allocated per frame: Build runs every tick.
        /// </summary>
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<PlanetLabel_SystemManagement> _planets =
            new List<PlanetLabel_SystemManagement>();
        private readonly List<SidePanel> _panels = new List<SidePanel>();

        public override string Key
        {
            get { return "screen.star-system"; }
        }

        /// <summary>The same layer as the galaxy: it is the other half of the same map, and the two are
        /// never up together.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        public override string ScreenName
        {
            get { return ModStrings.Get(ModStrings.ScreenStarSystem); }
        }

        /// <summary>The planets, because they are what the player came here to look at and they are
        /// the first thing Tab must reach - Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return PlanetStop; }
        }

        /// <summary>The page a modal is opened FROM, so closing the improvements list or the rename box
        /// puts the cursor back on the control that opened it rather than at the top of the page.
        /// </summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>Ours while the camera is in a system and nothing has replaced the page. The scan
        /// overlay is the game's own X-ray of this same view level and shows a different set of things,
        /// so it is not this screen.</summary>
        public override bool IsActive()
        {
            try
            {
                if (!GalaxyViewLevels.At<GalaxyViewLevel_SystemManagement>())
                {
                    return false;
                }

                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                if (gui == null || gui.IsAnyModalVisible || gui.IsInLoadingWindow)
                {
                    return false;
                }

                if (GalaxyViewLevels.Scanning)
                {
                    return false;
                }

                StarSystemScreen window = Window();
                return window != null && window.Shown;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape is the game's: from here it takes the camera back out to the galaxy, which
        /// is the same route the page's own close button takes.</summary>
        public override bool Back()
        {
            return false;
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
            StarSystemScreen window = Window();
            if (window == null)
            {
                return;
            }

            // The page arrives in pieces: the game's window and the side panels are up a frame or two
            // before the planet cards are drawn over them. Declaring the half that exists would seat
            // the cursor on a side panel and leave it there, because a cursor that has been placed is
            // never moved again - so nothing is declared until the cards are there, which is what
            // "nothing here yet" is for. Every system has planets, so this always resolves.
            Labels(_planets);
            if (_planets.Count == 0)
            {
                return;
            }

            // Down the screen: the empire's banners in the top-left corner, then the page itself,
            // then the right-hand edge - a collapsed tutorial's bar and the notification icons under
            // it - and the turn controls in the bottom corner. Same order as every other view level,
            // because the game draws them in the same places whichever one is up.
            _hud.Empire(builder);

            builder.BeginStop(PlanetStop);
            builder.PushContext(ModStrings.Get(ModStrings.SystemPlanetsPanel));
            BuildPlanets(builder, window);
            builder.PopContext();

            BuildSidePanels(builder);

            BuildBottomPanel(
                builder,
                ConstructiblesStop,
                ModStrings.SystemConstructiblesPanel,
                () => BuildConstructibles(builder, window)
            );
            BuildBottomPanel(
                builder,
                QueueStop,
                ModStrings.SystemQueuePanel,
                () => BuildQueue(builder, window)
            );
            BuildBottomPanel(
                builder,
                HangarStop,
                ModStrings.SystemHangarPanel,
                () => BuildHangar(builder, window)
            );

            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.Turn(builder);
        }

        private static void BuildBottomPanel(
            GraphBuilder builder,
            object stop,
            string nameKey,
            Action build
        )
        {
            builder.BeginStop(stop);
            builder.PushContext(ModStrings.Get(nameKey));
            build();
            builder.PopContext();
        }

        // ---- the planets ----

        /// <summary>
        /// The planet cards across the middle, in the order they are drawn - which is left to right,
        /// and is NOT the order the system holds its planets in: the table lays the cards out from the
        /// right, so the model's first planet is the rightmost card. Measured rather than assumed,
        /// because a reading order taken from the model would have been backwards.
        /// </summary>
        private void BuildPlanets(GraphBuilder builder, StarSystemScreen window)
        {
            try
            {
                for (int i = 0; i < _planets.Count; i++)
                {
                    AddPlanet(builder, _planets[i], window);
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the planet cards threw: " + e);
            }
        }

        private void AddPlanet(
            GraphBuilder builder,
            PlanetLabel_SystemManagement label,
            StarSystemScreen window
        )
        {
            Planet planet = label.Planet;
            if (planet == null)
            {
                return;
            }

            PlanetLabel_SystemManagement it = label;
            // The card's own status button carries the game's sentence about the state - "too hostile
            // to be colonized", and which technology would change that. It is read through the review
            // buffer rather than announced on focus, so nothing is taken from it here beyond making
            // sure the pointer lands on it and draws it.
            AgeTransform status = StatusWidget(label);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.PlanetTitle)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlanetStatus)),
                },
                Sections = GraphNodes.Sections(() => PlanetDetails(it), null),
                OnActivate = () => OpenPlanetMenu(it, window),
            };
            AgeWidgets.PointAt(vtable, status ?? label.AgeTransform);
            builder.AddItem(
                ControlId.Referenced(planet, "system:planet/" + planet.GUID),
                vtable
            );
        }

        /// <summary>
        /// Everything the card shows that the readout cannot carry: why the game is refusing to
        /// colonize it, what kind of world it is, what living there is like, what has been found on it,
        /// and its five outputs. In the order the card draws them, top to bottom.
        /// </summary>
        private static IList<string> PlanetDetails(PlanetLabel_SystemManagement label)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTransform status = StatusWidget(label);
                Add(lines, AgeWidgets.TooltipLines(AgeWidgets.Raw(status)));
                AddWidgetLines(lines, label.PlanetTypeGroup);
                AddWidgetLines(lines, label.PlanetSizeGroup);
                AddWidgetLines(lines, label.PlanetGameplayTypeTable);
                AddWidgetLines(lines, label.PlanetAnomaliesTable);
                AddWidgetLines(lines, label.PlanetCuriositiesTable);
                AddWidgetLines(lines, label.ResourceDepositsGroup);
                AddWidgetLines(lines, label.ImprovementStatus);
                AddFidsi(lines, label);
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a planet's details threw: " + e);
            }

            return lines;
        }

        /// <summary>The planet's five outputs, named by the game's own property titles. Read off the
        /// same simulation object the card reads: the colony's when there is one, the planet's own
        /// potential when there is not - which is exactly the two things the card draws, as values in
        /// one case and as rows of pips in the other.</summary>
        private static void AddFidsi(List<string> lines, PlanetLabel_SystemManagement label)
        {
            FidsiEnumerator fidsi = label.FidsiEnumerator;
            if (fidsi == null || fidsi.FidsiProperties == null)
            {
                return;
            }

            ColonizedPlanet colony = label.ColonizedPlanet;
            Amplitude.Unity.Simulation.SimulationObject simulation =
                colony != null ? colony.SimulationObject : label.Planet.SimulationObject;
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

        /// <summary>
        /// What can be done to this planet, as a menu built from what the game is offering right now.
        ///
        /// Colonize and rename are the card's own two buttons. Moving a population unit has no button
        /// at all - the game only offers it as a drag from one card to another - so the menu is the
        /// only place a keyboard can reach it, one entry per unit and destination.
        /// </summary>
        private void OpenPlanetMenu(PlanetLabel_SystemManagement label, StarSystemScreen window)
        {
            List<string> labels = new List<string>();
            List<Action> actions = new List<Action>();
            try
            {
                // First, because it is what a click on the card itself does: the game's own default
                // for a planet is to open its page, and everything else here is a button drawn on top
                // of it.
                Planet planet = label.Planet;
                if (planet != null)
                {
                    Planet it = planet;
                    labels.Add(ModStrings.Get(ModStrings.SystemViewPlanet));
                    actions.Add(() => GalaxyViewLevels.OpenPlanet(it));
                }

                // The game leaves the colonize button clickable when it is refusing: clicking it then
                // points at the technology that would unlock it instead of colonizing anything. That
                // is a HINT, not the action this entry is named after, so a button in that state is
                // not offered - the reason is already in the card's own readout.
                AgeControlButton colonize = label.ColonizeButton;
                if (
                    colonize != null
                    && AgeWidgets.Visible(AgeWidgets.Transform(colonize))
                    && colonize.Enable
                    && !Gui.IsHintActive(colonize.AgeTransform)
                )
                {
                    AgeControlButton it = colonize;
                    labels.Add(ModStrings.Get(ModStrings.SystemColonize));
                    actions.Add(() => AgeWidgets.Press(it));
                }

                AgeTransform rename = label.PlanetRenameButton;
                if (rename != null && AgeWidgets.Visible(rename) && rename.Enable)
                {
                    AgeTransform it = rename;
                    labels.Add(ModStrings.Get(ModStrings.SystemRenamePlanet));
                    actions.Add(() => AgeWidgets.Press(it));
                }

                AddPopulationMoves(label, window, labels, actions);
            }
            catch (Exception e)
            {
                Log.Warn("system: working out a planet's actions threw: " + e);
            }

            List<Action> chosen = actions;
            ChoiceSubmenuScreen.Open(
                AgeText.Label(label.PlanetTitle),
                labels,
                -1,
                index =>
                {
                    if (index >= 0 && index < chosen.Count)
                    {
                        chosen[index]();
                    }
                }
            );
        }

        /// <summary>
        /// One entry per population unit on this planet and per other planet of ours in the system it
        /// could go to. The game moves these by dragging a marker from one card to another; the order
        /// posted here is the one that drag posts, a unit at a time - the smallest move the drag can
        /// make, and the only quantity a menu can offer without inventing a number picker the game
        /// does not have.
        /// </summary>
        private static void AddPopulationMoves(
            PlanetLabel_SystemManagement label,
            StarSystemScreen window,
            List<string> labels,
            List<Action> actions
        )
        {
            ColonizedPlanet source = label.ColonizedPlanet;
            ColonizedStarSystem system = window.ColonizedStarSystem;
            if (source == null || system == null || source.Empire != Gui.PlayerEmpire)
            {
                return;
            }

            for (int p = 0; p < system.PlanetsColonized.Count; p++)
            {
                ColonizedPlanet destination = system.PlanetsColonized[p];
                if (destination == null || ReferenceEquals(destination, source))
                {
                    continue;
                }

                foreach (
                    KeyValuePair<StaticString, Population> entry in source.PopulationsByAffinity
                )
                {
                    if (entry.Value == null || entry.Value.Count <= 0)
                    {
                        continue;
                    }

                    ColonizedPlanet from = source;
                    ColonizedPlanet to = destination;
                    StaticString affinity = entry.Key;
                    labels.Add(
                        ModStrings.Format(
                            ModStrings.SystemMovePopulation,
                            AgeText.Clean(Gui.GetLocalizedTitle(affinity)),
                            AgeText.Clean(to.LocalizedName)
                        )
                    );
                    actions.Add(() => MovePopulation(from, to, affinity));
                }
            }
        }

        private static void MovePopulation(
            ColonizedPlanet source,
            ColonizedPlanet destination,
            StaticString affinity
        )
        {
            try
            {
                PlayerController player = Gui.GetActivePlayerController();
                player.PostOrder(
                    new OrderTransferPopulationFromPlanetToPlanet(
                        player.Empire.Index,
                        source.GUID,
                        destination.GUID,
                        affinity,
                        1,
                        StaticString.Empty
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("system: moving a population unit threw: " + e);
            }
        }

        private static AgeTransform StatusWidget(PlanetLabel_SystemManagement label)
        {
            AgePrimitiveLabel status = label.PlanetStatus;
            return status == null ? null : status.AgeTransform;
        }

        /// <summary>The planet cards the page is drawing, left to right. Ordered by where they are on
        /// screen rather than by the order the window pools them in, which is the model's order and
        /// runs the other way.</summary>
        private void Labels(List<PlanetLabel_SystemManagement> into)
        {
            into.Clear();
            PlanetLabelsWindow_SystemManagement window = Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false)
                : null;
            if (window == null)
            {
                return;
            }

            PlanetLabel_SystemManagement[] labels =
                window.GetComponentsInChildren<PlanetLabel_SystemManagement>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && AgeWidgets.Visible(labels[i].AgeTransform))
                {
                    into.Add(labels[i]);
                }
            }

            into.Sort(ByDrawnX);
        }

        private static readonly Comparison<PlanetLabel_SystemManagement> ByDrawnX = (left, right) =>
        {
            float a = left.AgeTransform.GetGlobalPosition().x;
            float b = right.AgeTransform.GetGlobalPosition().x;
            return a.CompareTo(b);
        };

        // ---- the side panels ----

        /// <summary>
        /// A stop per panel the game is drawing down the left edge, top to bottom. Which ones those are
        /// is the game's answer to what the system is: a colony gets its colony, population and
        /// representative panels, an outpost and a ghost get their own sets. Declaring what is drawn
        /// rather than what a colony has is what makes the other two work without being modelled.
        /// </summary>
        private void BuildSidePanels(GraphBuilder builder)
        {
            try
            {
                SidePanelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<SidePanelsWindow>(false)
                    : null;
                if (window == null)
                {
                    return;
                }

                _panels.Clear();
                SidePanel[] panels = window.GetComponentsInChildren<SidePanel>(true);
                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i] != null && AgeWidgets.Visible(panels[i].AgeTransform))
                    {
                        _panels.Add(panels[i]);
                    }
                }

                _panels.Sort(ByDrawnY);
                for (int i = 0; i < _panels.Count; i++)
                {
                    SidePanel panel = _panels[i];
                    builder.BeginStop("system:side/" + panel.GetType().Name);
                    builder.PushContext(PanelName(panel));
                    ColonyInfoSidePanel colony = panel as ColonyInfoSidePanel;
                    if (colony != null)
                    {
                        BuildColonyInfo(builder, colony);
                    }
                    else
                    {
                        BuildReadouts(builder, panel, "system:side/" + i + "/");
                    }

                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the side panels threw: " + e);
            }
        }

        private static readonly Comparison<SidePanel> ByDrawnY = (left, right) =>
        {
            float a = left.AgeTransform.GetGlobalPosition().y;
            float b = right.AgeTransform.GetGlobalPosition().y;
            return a.CompareTo(b);
        };

        /// <summary>What a side panel is called. The game writes no title on them - it marks each with
        /// an icon in its corner and explains it in that icon's tooltip - so the three a colony has are
        /// named here, and anything else takes the game's own sentence about itself.</summary>
        private static string PanelName(SidePanel panel)
        {
            if (panel is ColonyInfoSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemColonyPanel);
            }

            if (panel is ColonyPopulationSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemPopulationPanel);
            }

            if (panel is RepresentativesStarSystemSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemRepresentativesPanel);
            }

            string described = FirstLine(HeaderTooltip(panel));
            return string.IsNullOrEmpty(described) ? panel.GetType().Name : described;
        }

        private static AgeTooltip HeaderTooltip(SidePanel panel)
        {
            try
            {
                AgePrimitiveImage[] images = panel.GetComponentsInChildren<AgePrimitiveImage>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    AgeTooltip tooltip = AgeWidgets.Raw(images[i].AgeTransform);
                    if (tooltip != null && AgeWidgets.Readable(tooltip) != null)
                    {
                        return tooltip;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>
        /// The colony panel, hand-modelled because it is the one side panel that is mostly controls:
        /// the system's name is a rename button, the upkeep line opens the improvements list, and the
        /// automation policy is a list to choose from.
        /// </summary>
        private void BuildColonyInfo(GraphBuilder builder, ColonyInfoSidePanel panel)
        {
            _cells.Clear();

            AddReadout(
                _cells,
                panel.SystemBanner,
                "system:colony/banner",
                () =>
                    ModStrings.Format(
                        ModStrings.SystemLevel,
                        AgeText.Label(panel.LevelLabel)
                    )
            );

            AgeControlButton rename = panel.RenameButton;
            if (rename != null && AgeWidgets.Visible(AgeWidgets.Transform(rename)))
            {
                AgeControlButton it = rename;
                AgeTooltip tooltip = AgeWidgets.Raw(AgeWidgets.Transform(rename));
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeText.Label(panel.SystemTitleLabel),
                    () => AgeWidgets.Press(it),
                    () => AgeWidgets.Operable(AgeWidgets.Transform(it)),
                    tooltip
                );
                AgeWidgets.Point(vtable, it);
                Add(_cells, AgeWidgets.Transform(rename), ControlId.Referenced(rename, "system:colony/rename"), vtable);
            }

            AddReadout(
                _cells,
                panel.SecurityValue == null ? null : panel.SecurityValue.AgeTransform,
                "system:colony/security",
                () => ModStrings.Get(ModStrings.SystemSecurity),
                () => AgeText.Label(panel.SecurityValue)
            );
            AddReadout(
                _cells,
                panel.UpkeepLabel == null ? null : panel.UpkeepLabel.AgeTransform,
                "system:colony/upkeep",
                () => AgeText.Label(panel.UpkeepLabel)
            );

            AgeTransform improvements = ImprovementsButton(panel);
            if (improvements != null && AgeWidgets.Visible(improvements))
            {
                AgeTransform it = improvements;
                AgeTooltip tooltip = AgeWidgets.Raw(improvements);
                NodeVtable vtable = GraphNodes.Button(
                    () => ModStrings.Get(ModStrings.SystemImprovements),
                    () => AgeWidgets.Press(it),
                    () => AgeWidgets.Operable(it),
                    tooltip
                );
                AgeWidgets.PointAt(vtable, it);
                Add(_cells, it, ControlId.Referenced(it, "system:colony/improvements"), vtable);
            }

            AddFidsiCells(_cells, panel);
            AddPolicy(_cells, panel);
            Emit(builder, _cells);
        }

        /// <summary>The system's five outputs, one readout each, named by the game's own titles for the
        /// properties behind them - the same pairing the panel draws as an icon and a number.</summary>
        private static void AddFidsiCells(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            FidsiEnumerator fidsi = panel.FidsiEnumerator;
            AgeTransform group = fidsi == null ? null : fidsi.FidsiGroup;
            if (group == null || fidsi.FidsiProperties == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            int count = Math.Min(fidsi.DisplayedProperties, fidsi.FidsiProperties.Count);
            for (int i = 0; i < count; i++)
            {
                AgeTransform item = ChildAt(group, i);
                GuiSimulationProperty property = fidsi.FidsiProperties[i];
                if (item == null || property == null)
                {
                    continue;
                }

                AgeTransform widget = item;
                GuiSimulationProperty it = property;
                AddReadout(
                    cells,
                    widget,
                    "system:colony/fidsi/" + i,
                    () => AgeText.Clean(Gui.GetLocalizedTitle(it.Name)),
                    () => AgeWidgets.TextOf(widget)
                );
            }
        }

        /// <summary>The automation policy: a list the control opens, which is a screen of its own - the
        /// same one every drop list in the game gets.</summary>
        private static void AddPolicy(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeControlDropList list = panel.PolicyDroplist;
            AgeTransform group = panel.PolicyGroup;
            if (list == null || group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeControlDropList it = list;
            ColonyInfoSidePanel owner = panel;
            AgeTransform widget = AgeWidgets.Transform(list);
            string title = LabelIn(group);
            NodeVtable vtable = GraphNodes.ComboBox(
                () => title,
                () => DropListScreen.EntryText(it, it.SelectedItem),
                () =>
                    DropListScreen.Open(
                        it,
                        title,
                        index =>
                        {
                            it.SelectedItem = index;
                            Send(it.OnSelectionObject, it.OnSelectionMethod, owner);
                        }
                    ),
                () => AgeWidgets.Operable(widget)
            );
            // Activating this opens a list rather than changing the setting, so there is no new state
            // to report: the list that opens says where it starts.
            vtable.StateText = null;
            AgeWidgets.PointAt(vtable, widget);
            Add(cells, widget, ControlId.Referenced(list, "system:colony/policy"), vtable);
        }

        private static void Send(GameObject target, string method, Component fallback)
        {
            if (target == null && fallback != null)
            {
                target = fallback.gameObject;
            }

            if (target != null && !string.IsNullOrEmpty(method))
            {
                target.SendMessage(method, target, SendMessageOptions.DontRequireReceiver);
            }
        }

        private static AgeTransform ImprovementsButton(ColonyInfoSidePanel panel)
        {
            AgeTransform group = panel.SystemUpkeepGroup;
            if (group == null)
            {
                return null;
            }

            AgeControlButton[] buttons = group.GetComponentsInChildren<AgeControlButton>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].OnActivateMethod == "OnImprovementsCb")
                {
                    return buttons[i].AgeTransform;
                }
            }

            return null;
        }

        // ---- the bottom panels ----

        /// <summary>
        /// What this system can be told to build: the filters that decide which of them are shown, then
        /// the items themselves in the order the grid lays them out.
        ///
        /// Enter puts one at the end of the queue and Alt and Enter at the front, which are the game's
        /// own click and its own Alt-click. A confirmation the game wants for a particular thing -
        /// scrapping the colony, most of them - is asked exactly as the game asks it, through the
        /// message box that is already a screen of ours.
        /// </summary>
        private void BuildConstructibles(GraphBuilder builder, StarSystemScreen window)
        {
            try
            {
                StarSystemConstructiblePanel panel =
                    window.GetComponentInChildren<StarSystemConstructiblePanel>(true);
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    return;
                }

                _cells.Clear();
                AgeTransform filters = panel.ConstructibleFiltersTable;
                if (filters != null && AgeWidgets.Visible(filters))
                {
                    ConstructibleFilter[] all = filters.GetComponentsInChildren<ConstructibleFilter>(true);
                    for (int i = 0; i < all.Length; i++)
                    {
                        AddFilter(_cells, all[i]);
                    }
                }

                Emit(builder, _cells);

                _cells.Clear();
                AgeTransform table = panel.ConstructibleTable;
                if (table != null)
                {
                    StarSystemConstructibleItem[] items =
                        table.GetComponentsInChildren<StarSystemConstructibleItem>(true);
                    for (int i = 0; i < items.Length; i++)
                    {
                        AddConstructible(_cells, items[i], window);
                    }
                }

                Emit(builder, _cells);
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the constructibles threw: " + e);
            }
        }

        private static void AddFilter(List<Cell> cells, ConstructibleFilter filter)
        {
            if (filter == null || !AgeWidgets.Visible(filter.AgeTransform))
            {
                return;
            }

            AgeControlToggle toggle = filter.Toggle;
            if (toggle == null)
            {
                return;
            }

            ConstructibleFilter it = filter;
            AgeTooltip tooltip = filter.Tooltip;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => FirstLine(tooltip),
                () => it.Toggle.State,
                () => AgeWidgets.Toggle(it.Toggle),
                () => AgeWidgets.Operable(it.AgeTransform),
                tooltip,
                TooltipMode.None
            );
            AgeWidgets.PointAt(vtable, filter.AgeTransform);
            Add(
                cells,
                filter.AgeTransform,
                ControlId.Referenced(filter, "system:filter/" + filter.name),
                vtable
            );
        }

        private static void AddConstructible(
            List<Cell> cells,
            StarSystemConstructibleItem item,
            StarSystemScreen window
        )
        {
            if (item == null || !AgeWidgets.Visible(item.AgeTransform))
            {
                return;
            }

            IGuiConstructible constructible = item.GuiConstructible;
            if (constructible == null)
            {
                return;
            }

            StarSystemConstructibleItem it = item;
            AgeTooltip tooltip = AgeWidgets.Raw(item.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ConstructibleName(it)),
                    GraphNodes.ValuePart(() => ConstructibleCost(it, window)),
                    GraphNodes.DisabledPart(() => AgeWidgets.Operable(it.AgeTransform)),
                },
                Sections = GraphNodes.Sections(() => ConstructibleDetails(it), null),
                OnActivate = () => Queue(it, window, false),
                OnAlternate = () => Queue(it, window, true),
            };
            AgeWidgets.PointAt(vtable, item.AgeTransform);
            Add(
                cells,
                item.AgeTransform,
                ControlId.Referenced(item, "system:constructible/" + constructible.Name),
                vtable
            );
        }

        /// <summary>The item's full name. The grid clips its caption to fit the tile - "Cerebral ." -
        /// so the name is taken from what the tile is FOR rather than from what the tile says.
        /// </summary>
        private static string ConstructibleName(StarSystemConstructibleItem item)
        {
            try
            {
                return AgeText.Clean(Gui.Localize(item.GuiConstructible.Title));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ConstructibleCost(
            StarSystemConstructibleItem item,
            StarSystemScreen window
        )
        {
            try
            {
                float cost = item.GuiConstructible.GetIndustryCost(window.ColonizedStarSystem);
                return cost <= 0f ? null : ModStrings.Format(ModStrings.SystemIndustryCost, Amount(cost));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the tile's tooltip says - its cost, what it does, and for a refusing one the
        /// game's own reasons, which the game collects on the item as it works out whether to offer it.
        /// </summary>
        private static IList<string> ConstructibleDetails(StarSystemConstructibleItem item)
        {
            List<string> lines = new List<string>();
            try
            {
                Add(lines, AgeWidgets.TooltipLines(AgeWidgets.Raw(item.AgeTransform)));
                AddFailures(lines, item.FailureInfosProvider);
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a constructible's details threw: " + e);
            }

            return lines;
        }

        private static void AddFailures(List<string> lines, IFailureInfosProvider provider)
        {
            if (provider == null || provider.FailureInfos == null)
            {
                return;
            }

            for (int i = 0; i < provider.FailureInfos.Count; i++)
            {
                string text = AgeText.Clean(
                    Gui.FormatFailure(string.Empty, provider.FailureInfos[i].Flag.ToString())
                );
                if (!string.IsNullOrEmpty(text) && !lines.Contains(text))
                {
                    lines.Add(text);
                }
            }
        }

        /// <summary>
        /// Put a thing in the queue, the way the panel's own click does - including the confirmation
        /// the game insists on for the few constructions it will not let you queue by accident, asked
        /// with the game's own words through the game's own message box.
        ///
        /// <paramref name="atHead"/> is the game's Alt-click: the same order, followed by a move to the
        /// front once the game has accepted it and there is something to move.
        /// </summary>
        private static void Queue(
            StarSystemConstructibleItem item,
            StarSystemScreen window,
            bool atHead
        )
        {
            try
            {
                if (!AgeWidgets.Operable(item.AgeTransform))
                {
                    return;
                }

                ColonizedStarSystem system = window.ColonizedStarSystem;
                IConstructible constructible = item.GuiConstructible.Constructible;
                if (system == null || constructible == null)
                {
                    return;
                }

                if (constructible.NeedsConfirmation)
                {
                    StarSystemConstructibleItem confirmed = item;
                    Gui.GuiService.ShowMessage(
                        GuiConstructibleElement.GetConfirmationMessage(
                            constructible,
                            Gui.GetActivePlayerController().Empire as Empire,
                            system.GUID
                        ),
                        MessageBoxType.IMPORTANT,
                        (sender, result) =>
                        {
                            if (result.Result == MessageBoxResult.Ok)
                            {
                                Post(confirmed, window, atHead);
                            }
                        }
                    );
                    return;
                }

                Post(item, window, atHead);
            }
            catch (Exception e)
            {
                Log.Warn("system: queueing a construction threw: " + e);
            }
        }

        private static void Post(
            StarSystemConstructibleItem item,
            StarSystemScreen window,
            bool atHead
        )
        {
            try
            {
                ColonizedStarSystem system = window.ColonizedStarSystem;
                PlayerController player = Gui.GetActivePlayerController();
                OrderQueueConstruction order = new OrderQueueConstruction(
                    player.Empire.Index,
                    system.GUID,
                    item.GuiConstructible.Constructible
                );
                if (atHead)
                {
                    Ticket ignored;
                    player.PostOrder(
                        order,
                        out ignored,
                        (sender, args) => MoveToHead(args, system)
                    );
                }
                else
                {
                    player.PostOrder(order);
                }

                // The flying icon the panel draws when a click queues something: the page looks the
                // same to someone watching whether the queue was filled by hand or by keyboard.
                window.AcknowledgeConstruction(item.AgeTransform, item.Icon.Image);
            }
            catch (Exception e)
            {
                Log.Warn("system: posting a construction order threw: " + e);
            }
        }

        private static void MoveToHead(TicketRaisedEventArgs args, ColonizedStarSystem system)
        {
            try
            {
                if (args.Result != PostOrderResponse.Processed)
                {
                    return;
                }

                OrderQueueConstruction queued = args.Order as OrderQueueConstruction;
                PlayerController player = Gui.GetActivePlayerController();
                player.PostOrder(
                    new OrderMoveConstruction(
                        player.Empire.Index,
                        system.GUID,
                        queued.ConstructionGameEntityGUID,
                        0
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("system: moving a construction to the head threw: " + e);
            }
        }

        /// <summary>
        /// What the system is building, in order. A line's own click CANCELS it, which is not a thing
        /// Enter should ever do by itself, so Enter opens the line's menu instead - cancel, and buy the
        /// rest of it with whichever currencies the game is willing to take today.
        ///
        /// Shift and an arrow moves the line itself, which is the keyboard's version of dragging it up
        /// or down the queue.
        /// </summary>
        private void BuildQueue(GraphBuilder builder, StarSystemScreen window)
        {
            try
            {
                StarSystemQueuePanel panel = window.GetComponentInChildren<StarSystemQueuePanel>(true);
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    return;
                }

                AgeTransform table = panel.ConstructionLinesTable;
                if (table == null)
                {
                    return;
                }

                ConstructionLine[] lines = table.GetComponentsInChildren<ConstructionLine>(true);
                for (int i = 0; i < lines.Length; i++)
                {
                    AddQueueLine(builder, lines[i], panel, window);
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the construction queue threw: " + e);
            }
        }

        private static void AddQueueLine(
            GraphBuilder builder,
            ConstructionLine line,
            StarSystemQueuePanel panel,
            StarSystemScreen window
        )
        {
            if (line == null || !AgeWidgets.Visible(line.AgeTransform) || line.Construction == null)
            {
                return;
            }

            ConstructionLine it = line;
            AgeTooltip tooltip = AgeWidgets.Raw(line.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.Title)),
                    GraphNodes.ValuePart(() => QueueLineState(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
                OnActivate = () => OpenQueueMenu(it, panel),
                OnReorder = direction => MoveLine(it, direction, window),
            };
            AgeWidgets.PointAt(vtable, line.AgeTransform);
            builder.AddItem(
                ControlId.Referenced(line.Construction, "system:queue/" + line.Construction.GUID),
                vtable
            );
        }

        /// <summary>Where the line is in the queue, how far along it is, and how long is left - the
        /// three things the line draws beside its name.</summary>
        private static string QueueLineState(ConstructionLine line)
        {
            MessageBuilder message = new MessageBuilder();
            try
            {
                message.ListItem(
                    ModStrings.Format(ModStrings.SystemQueuePosition, AgeText.Label(line.Rank))
                );
                if (line.Progress != null && line.Progress.Visible)
                {
                    message.ListItem(
                        ModStrings.Format(
                            ModStrings.SystemProgress,
                            Mathf.RoundToInt(line.Progress.PercentRight)
                        )
                    );
                }

                if (line.RemainingTurnLabel != null && line.RemainingTurnLabel.Visible)
                {
                    message.ListItem(
                        ModStrings.Format(
                            ModStrings.GalaxyTurnsRemaining,
                            AgeText.Label(line.RemainingTurnLabel)
                        )
                    );
                }
            }
            catch (Exception) { }

            return message.Build();
        }

        private static void OpenQueueMenu(ConstructionLine line, StarSystemQueuePanel panel)
        {
            List<string> labels = new List<string>();
            List<Action> actions = new List<Action>();
            List<Func<System.Collections.Generic.IList<string>>> details =
                new List<Func<System.Collections.Generic.IList<string>>>();
            try
            {
                labels.Add(ModStrings.Get(ModStrings.SystemCancelConstruction));
                details.Add(null);
                ConstructionLine it = line;
                StarSystemQueuePanel owner = panel;
                actions.Add(() => Cancel(it, owner));

                BuyoutButton[] buyouts = line.BuyoutButtons;
                for (int i = 0; buyouts != null && i < buyouts.Length; i++)
                {
                    BuyoutButton buyout = buyouts[i];
                    if (
                        buyout == null
                        || !AgeWidgets.Visible(buyout.AgeTransform)
                        || !AgeWidgets.Operable(buyout.AgeTransform)
                    )
                    {
                        continue;
                    }

                    BuyoutButton press = buyout;
                    labels.Add(
                        ModStrings.Format(
                            ModStrings.SystemBuyOut,
                            AgeText.Clean(Gui.GetLocalizedTitle("Empire" + buyout.Resource))
                        )
                    );
                    details.Add(AgeWidgets.TooltipLines(buyout.Tooltip));
                    actions.Add(() => AgeWidgets.Press(press.AgeTransform));
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: working out a queue line's actions threw: " + e);
            }

            List<Action> chosen = actions;
            ChoiceSubmenuScreen.Open(
                AgeText.Label(line.Title),
                labels,
                -1,
                index =>
                {
                    if (index >= 0 && index < chosen.Count)
                    {
                        chosen[index]();
                    }
                },
                details
            );
        }

        /// <summary>Cancel through the panel's own handler, which is what knows to ask before throwing
        /// away industry that has already gone into the thing.</summary>
        private static void Cancel(ConstructionLine line, StarSystemQueuePanel panel)
        {
            try
            {
                if (CancelConstruction != null)
                {
                    CancelConstruction.Invoke(panel, new object[] { line });
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: cancelling a construction threw: " + e);
            }
        }

        /// <summary>Move a line one place up or down its queue - the same order the game posts when a
        /// line is dropped somewhere new.</summary>
        private static void MoveLine(ConstructionLine line, int direction, StarSystemScreen window)
        {
            try
            {
                ColonizedStarSystem system = window.ColonizedStarSystem;
                DepartmentOfIndustry industry =
                    system == null ? null : system.Empire.GetAgency<DepartmentOfIndustry>();
                ConstructionQueue queue =
                    industry == null ? null : industry.GetConstructionQueue(system);
                if (queue == null)
                {
                    return;
                }

                int at = queue.IndexOf(line.Construction);
                int to = at + direction;
                if (at < 0 || to < 0 || to >= queue.PendingConstructions.Count)
                {
                    return;
                }

                PlayerController player = Gui.GetActivePlayerController();
                player.PostOrder(
                    new OrderMoveConstruction(
                        player.Empire.Index,
                        system.GUID,
                        line.Construction.GUID,
                        to
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("system: moving a construction threw: " + e);
            }
        }

        private static readonly MethodInfo CancelConstruction = Handler(
            typeof(StarSystemQueuePanel),
            "OnCancelConstruction"
        );

        private static MethodInfo Handler(Type type, string name)
        {
            try
            {
                return type.GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
            }
            catch (Exception e)
            {
                Log.Warn("system: looking up " + name + " threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The ships parked in the system: the row of things that can be done to a selection, then the
        /// ships themselves. Enter picks a ship up and puts it down again rather than doing anything to
        /// it, because that is the game's own model here - you choose ships and then press a button.
        /// </summary>
        private void BuildHangar(GraphBuilder builder, StarSystemScreen window)
        {
            try
            {
                StarSystemHangarPanel panel = window.GetComponentInChildren<StarSystemHangarPanel>(true);
                if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
                {
                    return;
                }

                _cells.Clear();
                AddToolbar(_cells, panel.SelectAllButton, ModStrings.SystemSelectAllShips, "select-all");
                AddToolbar(_cells, panel.CreateButton, ModStrings.SystemCreateFleet, "create-fleet");
                AddToolbar(_cells, panel.RepairButton, ModStrings.SystemRepairShips, "repair");
                AddToolbar(_cells, panel.RetrofitButton, ModStrings.SystemRetrofitShips, "retrofit");
                AddToolbar(_cells, panel.ScrapButton, ModStrings.SystemScrapShips, "scrap");
                AddToolbar(_cells, panel.SellButton, ModStrings.SystemSellShips, "sell");
                Emit(builder, _cells);

                _cells.Clear();
                AgeTransform table = panel.GarrisonPanelsTable;
                if (table != null)
                {
                    ShipItem[] ships = table.GetComponentsInChildren<ShipItem>(true);
                    for (int i = 0; i < ships.Length; i++)
                    {
                        AddShip(_cells, ships[i]);
                    }
                }

                Emit(builder, _cells);
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the hangar threw: " + e);
            }
        }

        private static void AddToolbar(
            List<Cell> cells,
            AgeControlButton button,
            string nameKey,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (button == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(nameKey),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(AgeWidgets.Transform(it)),
                tooltip
            );
            AgeWidgets.Point(vtable, it);
            Add(cells, widget, ControlId.Referenced(button, "system:hangar/" + key), vtable);
        }

        private static void AddShip(List<Cell> cells, ShipItem ship)
        {
            if (ship == null || !AgeWidgets.Visible(ship.AgeTransform) || ship.GuiShip == null)
            {
                return;
            }

            ShipItem it = ship;
            AgeTooltip tooltip = AgeWidgets.Raw(ship.AgeTransform);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeText.Label(it.Title),
                () => it.SelectionToggle != null && it.SelectionToggle.State,
                () => AgeWidgets.Toggle(it.SelectionToggle),
                () => AgeWidgets.Operable(it.AgeTransform),
                tooltip,
                TooltipMode.None
            );
            AgeWidgets.PointAt(vtable, ship.AgeTransform);
            Add(
                cells,
                ship.AgeTransform,
                ControlId.Referenced(ship, "system:ship/" + ship.GetInstanceID()),
                vtable
            );
        }

        // ---- reading a panel nobody has modelled ----

        /// <summary>
        /// A panel read as it is drawn: every group in it that says something becomes a line, in the
        /// rows the panel lays them out in. The population and representative panels are all readouts
        /// and no decisions, and the panels an outpost or a ghost gets instead are the same shape, so
        /// they are all read this way rather than each having its own list of fields to keep in step
        /// with the game.
        ///
        /// Where to stop descending is the whole problem, and the answer is in the shape of the tree
        /// rather than in the text: a group whose children are all PRIMITIVES - a number, an icon, a
        /// word - is one thing the game has drawn out of several pieces ("3" beside a population icon
        /// beside "Imperials"), and reads as one line. A group that contains other GROUPS is a
        /// container, and each of those is a line of its own. Taking the outermost group that had any
        /// text at all instead collapsed a whole side panel into a single sentence.
        ///
        /// Some of what these panels draw has no words on it at all - a number beside a symbol, a bar
        /// chart - and the shape of the tree cannot name those. Each is answered by
        /// <see cref="Special"/>, which hands back a whole control read from the game's own model, and
        /// the walk continues around it.
        /// </summary>
        private void BuildReadouts(GraphBuilder builder, SidePanel panel, string keyPrefix)
        {
            _cells.Clear();
            Collect(_cells, panel.ContentGroup, keyPrefix, 0, panel);
            Emit(builder, _cells);
        }

        private const int MaxScrapeDepth = 6;

        private static void Collect(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            int depth,
            SidePanel panel
        )
        {
            if (widget == null || depth > MaxScrapeDepth || !AgeWidgets.Visible(widget))
            {
                return;
            }

            try
            {
                Cell special = Special(widget, keyPrefix, panel);
                if (special != null)
                {
                    cells.Add(special);
                    return;
                }

                AgeControlButton button = AgeWidgets.Button(widget);
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                string text = AgeWidgets.TextOf(widget);
                bool activatable =
                    button != null
                    && !string.IsNullOrEmpty(button.OnActivateMethod)
                    && !Transparent(widget, panel);
                if (!activatable && depth < MaxScrapeDepth && HasGroupChild(widget))
                {
                    IList<AgeTransform> children = widget.Children;
                    for (int i = 0; children != null && i < children.Count; i++)
                    {
                        Collect(cells, children[i], keyPrefix, depth + 1, panel);
                    }

                    return;
                }

                if (string.IsNullOrEmpty(text) && !activatable)
                {
                    return;
                }

                string key = keyPrefix + widget.name + "/" + depth;
                cells.Add(
                    activatable
                        ? Control(widget, button, tooltip, text, key)
                        : Readout(widget, tooltip, key)
                );
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a panel threw: " + e);
            }
        }

        /// <summary>Whether anything inside this widget is itself a container - which is what makes the
        /// widget a band of separate lines rather than one line drawn out of pieces.</summary>
        private static bool HasGroupChild(AgeTransform widget)
        {
            IList<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child == null || !AgeWidgets.Visible(child))
                {
                    continue;
                }

                IList<AgeTransform> grandchildren = child.Children;
                for (int j = 0; grandchildren != null && j < grandchildren.Count; j++)
                {
                    if (grandchildren[j] != null && AgeWidgets.Visible(grandchildren[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ---- the readouts the tree's shape cannot name ----

        /// <summary>
        /// A control the panels draw as symbols and numbers, read from the game's own model instead of
        /// from the words on it - because there are none. Each of these was a line of bare digits
        /// before: "2", "1", "3", "50% Content", "+Imperials 9 Turn", and one graph that produced no
        /// line at all.
        ///
        /// Null for everything else, which is the ordinary walk.
        /// </summary>
        private static Cell Special(AgeTransform widget, string keyPrefix, SidePanel panel)
        {
            PopulationCount population = widget.GetComponent<PopulationCount>();
            if (population != null)
            {
                return PopulationCell(widget, population, keyPrefix);
            }

            SystemRepresentativeItem representative = widget.GetComponent<SystemRepresentativeItem>();
            if (representative != null)
            {
                return RepresentativeCell(widget, representative, keyPrefix);
            }

            ColonyPopulationSidePanel population2 = panel as ColonyPopulationSidePanel;
            if (population2 != null)
            {
                HappinessSidePanelItem approval = population2.HapinessGroup;
                if (approval != null && ReferenceEquals(widget, approval.AgeTransform))
                {
                    return ApprovalCell(widget, approval, population2, keyPrefix);
                }

                GrowthItem growth = population2.GrowthGaugeItem;
                if (
                    growth != null
                    && growth.NextPopulationLabel != null
                    && ReferenceEquals(widget, growth.NextPopulationLabel.AgeTransform.Parent)
                )
                {
                    return GrowthCell(widget, growth, keyPrefix);
                }
            }

            RepresentativesStarSystemSidePanel representatives =
                panel as RepresentativesStarSystemSidePanel;
            if (
                representatives != null
                && ReferenceEquals(widget, representatives.PoliticalSensitivityBreakdown)
            )
            {
                return SensitivityCell(widget, representatives, keyPrefix);
            }

            return null;
        }

        /// <summary>Whether a group the game made clickable is really a band of readouts. The approval
        /// box answers a click only in the developers' god mode, and treating it as one control is what
        /// glued its icon, its percentage and its status word into a single "50% Content" line.
        /// </summary>
        private static bool Transparent(AgeTransform widget, SidePanel panel)
        {
            ColonyPopulationSidePanel population = panel as ColonyPopulationSidePanel;
            return population != null
                && population.HapinessGroup != null
                && ReferenceEquals(widget, population.HapinessGroup.AgeTransform.Parent);
        }

        /// <summary>One kind of person living here. The entry draws their symbol and how many of them
        /// there are and never writes what they are called; the game keeps that name on the wrapper
        /// hung on the entry's own tooltip.</summary>
        private static Cell PopulationCell(
            AgeTransform widget,
            PopulationCount unit,
            string keyPrefix
        )
        {
            AgeTooltip tooltip = unit.Tooltip;
            AgePrimitiveLabel count = unit.Count;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(tooltip)),
                    GraphNodes.ValuePart(() => AgeText.Label(count)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, keyPrefix + widget.name + "/population"),
                Vtable = vtable,
            };
        }

        /// <summary>A party's seats on this system's council. Drawn as the party's emblem and a count,
        /// with the party itself on the tooltip - the tooltip's own words are the internal name of the
        /// party ("Politics01"), so the wrapper is the only place its title can come from.</summary>
        private static Cell RepresentativeCell(
            AgeTransform widget,
            SystemRepresentativeItem item,
            string keyPrefix
        )
        {
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            AgePrimitiveLabel count = item.ProbabilityLabel;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(tooltip)),
                    GraphNodes.ValuePart(() => AgeText.Label(count)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, keyPrefix + widget.name + "/representative"),
                Vtable = vtable,
            };
        }

        /// <summary>How the people here feel about being governed: the game's own name for the measure -
        /// which is a different word for an empire that rules by honour - then the percentage and the
        /// status word the panel draws.</summary>
        private static Cell ApprovalCell(
            AgeTransform widget,
            HappinessSidePanelItem approval,
            ColonyPopulationSidePanel panel,
            string keyPrefix
        )
        {
            HappinessSidePanelItem it = approval;
            ColonyPopulationSidePanel owner = panel;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ApprovalName(owner)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.HappinessValueLabel)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.HappinessStatusLabel)),
                },
                Sections = GraphNodes.Sections(() => ApprovalDetails(it, tooltip), null),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, keyPrefix + widget.name + "/approval"),
                Vtable = vtable,
            };
        }

        private static string ApprovalName(ColonyPopulationSidePanel panel)
        {
            try
            {
                IHappinessProvider system =
                    panel == null ? null : panel.ColonizedStarSystem as IHappinessProvider;
                StaticString property =
                    system != null && system.CanUseHonor
                        ? SimulationProperties.Empire.Obedience
                        : SimulationProperties.Empire.Happiness;
                return AgeText.Clean(Gui.GetLocalizedTitle(property));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IList<string> ApprovalDetails(
            HappinessSidePanelItem approval,
            AgeTooltip tooltip
        )
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTransform icon = approval.HappinessIcon == null
                    ? null
                    : approval.HappinessIcon.AgeTransform;
                Add(lines, AgeWidgets.TooltipLines(AgeWidgets.Raw(icon)));
                Add(lines, AgeWidgets.TooltipLines(tooltip));
            }
            catch (Exception) { }

            return lines;
        }

        /// <summary>Who is being born here next and when. The panel draws the kind as a symbol with a
        /// plus in front of it and the wait as a bare number of turns; the sentence the game explains
        /// the symbol with is the only thing on the panel that says what either of them means, so it is
        /// what this is called.</summary>
        private static Cell GrowthCell(AgeTransform widget, GrowthItem growth, string keyPrefix)
        {
            GrowthItem it = growth;
            AgeTooltip kind = AgeWidgets.Raw(growth.NextPopulationLabel.AgeTransform);
            AgeTooltip when = growth.TurnsBeforeNextPop == null
                ? null
                : AgeWidgets.Raw(growth.TurnsBeforeNextPop.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () => FirstLine(kind) ?? AgeText.Label(it.NextPopulationLabel)
                    ),
                    GraphNodes.ValuePart(() => Drawn(it.TurnsBeforeNextPop)),
                    GraphNodes.ValuePart(() => Drawn(it.NextPopulationDestinationLabel)),
                },
                Sections = GraphNodes.Sections(() => GrowthDetails(kind, when), null),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, keyPrefix + widget.name + "/growth"),
                Vtable = vtable,
            };
        }

        private static IList<string> GrowthDetails(AgeTooltip kind, AgeTooltip when)
        {
            List<string> lines = new List<string>();
            Add(lines, AgeWidgets.TooltipLines(kind));
            Add(lines, AgeWidgets.TooltipLines(when));
            return lines;
        }

        private static string Drawn(AgePrimitiveLabel label)
        {
            try
            {
                return label != null && AgeWidgets.Visible(label.AgeTransform)
                    ? AgeText.Label(label)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The political sensitivity graph: one bar per party, as tall a fraction of the plot as that
        /// share of the people here leans towards it. The bars carry no text whatever - the graph is
        /// drawn from clipped rectangles - so the parties come from the game's own list of them, in the
        /// order it lays the bars out, and each share is how far up its own bar is left unclipped.
        ///
        /// The bars a party has no support in are drawn faded, so only the ones with any are spoken;
        /// all of them are in the review buffer.
        /// </summary>
        private static Cell SensitivityCell(
            AgeTransform widget,
            RepresentativesStarSystemSidePanel panel,
            string keyPrefix
        )
        {
            RepresentativesStarSystemSidePanel it = panel;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => FirstLine(tooltip)),
                    GraphNodes.ValuePart(() => SensitivityText(it, true)),
                },
                Sections = GraphNodes.Sections(() => SensitivityDetails(it, tooltip), null),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, keyPrefix + widget.name + "/sensitivity"),
                Vtable = vtable,
            };
        }

        private static string SensitivityText(
            RepresentativesStarSystemSidePanel panel,
            bool supportedOnly
        )
        {
            MessageBuilder message = new MessageBuilder();
            List<string> bars = new List<string>();
            Sensitivity(panel, supportedOnly, bars);
            for (int i = 0; i < bars.Count; i++)
            {
                message.ListItem(bars[i]);
            }

            return message.Build();
        }

        private static IList<string> SensitivityDetails(
            RepresentativesStarSystemSidePanel panel,
            AgeTooltip tooltip
        )
        {
            List<string> lines = new List<string>();
            Add(lines, AgeWidgets.TooltipLines(tooltip));
            Sensitivity(panel, false, lines);
            return lines;
        }

        private static void Sensitivity(
            RepresentativesStarSystemSidePanel panel,
            bool supportedOnly,
            List<string> into
        )
        {
            try
            {
                AgeTransform container = panel.PoliticsGaugesContainer;
                IList<AgeTransform> bars = container == null ? null : container.Children;
                if (bars == null)
                {
                    return;
                }

                IList<GuiPolitics> parties = Parties();
                for (int i = 0; i < bars.Count && i < parties.Count; i++)
                {
                    PoliticsSensitivityGauge gauge =
                        bars[i] == null ? null : bars[i].GetComponent<PoliticsSensitivityGauge>();
                    if (gauge == null || gauge.Clipper == null)
                    {
                        continue;
                    }

                    float share = (100f - gauge.Clipper.PercentTop) * 0.01f;
                    if (supportedOnly && share <= 0f)
                    {
                        continue;
                    }

                    into.Add(
                        new MessageBuilder()
                            .Fragment(AgeText.Clean(parties[i].Title))
                            .Fragment(Amplitude.Extensions.FloatExtensions.ToString(share, 0, true))
                            .Build()
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the political sensitivity graph threw: " + e);
            }
        }

        private static readonly List<GuiPolitics> _parties = new List<GuiPolitics>();

        /// <summary>The parties the graph has a bar for, in the graph's own order: the game's list of
        /// them with the independents left out, which is the same filter the panel applies when it
        /// makes the bars.</summary>
        private static IList<GuiPolitics> Parties()
        {
            _parties.Clear();
            try
            {
                System.Collections.IList all = Gui.GuiWrapperProviderService.GuiPolitics;
                for (int i = 0; i < all.Count; i++)
                {
                    GuiPolitics party = all[i] as GuiPolitics;
                    if (party != null && !party.PoliticsDefinition.IsNeutral)
                    {
                        _parties.Add(party);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: listing the political parties threw: " + e);
            }

            return _parties;
        }

        private static Cell Control(
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
            string caption = named ? text : FirstLine(tooltip);
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

        private static Cell Readout(AgeTransform widget, AgeTooltip tooltip, string key)
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

        // ---- shared ----

        /// <summary>A control on its way into the graph, still carrying the widget it was read from: the
        /// rows are worked out from a whole panel at once, which cannot be done while declaring it row
        /// by row.</summary>
        private sealed class Cell
        {
            public AgeTransform Widget;
            public ControlId Id;
            public NodeVtable Vtable;
        }

        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

        /// <summary>Declare a panel's controls in the rows they are drawn in, so up and down move
        /// between lines and left and right along one - measured, so a strip the engine wraps onto a
        /// second line is walked as two lines without anything being told.</summary>
        private static void Emit(GraphBuilder builder, List<Cell> cells)
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

        private static void Add(List<Cell> cells, AgeTransform widget, ControlId id, NodeVtable vtable)
        {
            cells.Add(new Cell { Widget = widget, Id = id, Vtable = vtable });
        }

        private static void AddReadout(
            List<Cell> cells,
            AgeTransform widget,
            string key,
            Func<string> label,
            Func<string> value = null
        )
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(label) },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            if (value != null)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(value));
            }

            AgeWidgets.PointAt(vtable, widget);
            Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        private static void AddWidgetLines(List<string> lines, AgeTransform widget)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            IList<AgeTransform> children = widget.Children;
            if (children == null || children.Count == 0)
            {
                AddLine(lines, AgeWidgets.TextOf(widget));
                return;
            }

            // A table of things - the traits, the anomalies - reads one line per thing, which is how it
            // is drawn and how it is reviewed.
            for (int i = 0; i < children.Count; i++)
            {
                if (AgeWidgets.Visible(children[i]))
                {
                    AddLine(lines, AgeWidgets.TextOf(children[i]));
                }
            }
        }

        private static void AddLine(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line) && !lines.Contains(line))
            {
                lines.Add(line);
            }
        }

        private static void Add(List<string> lines, Func<IList<string>> source)
        {
            if (source == null)
            {
                return;
            }

            try
            {
                IList<string> from = source();
                for (int i = 0; from != null && i < from.Count; i++)
                {
                    AddLine(lines, from[i]);
                }
            }
            catch (Exception) { }
        }

        /// <summary>The first thing a tooltip says - what a control with no caption of its own is
        /// called, in the game's words.</summary>
        private static string FirstLine(AgeTooltip tooltip)
        {
            try
            {
                if (AgeWidgets.Readable(tooltip) == null)
                {
                    return null;
                }

                IList<string> lines = AgeText.Lines(AgeText.Tooltip(tooltip));
                return lines != null && lines.Count > 0 ? lines[0] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The caption written beside a control - a drop list's own name, which the game draws
        /// as a label next to it rather than on it.</summary>
        private static string LabelIn(AgeTransform group)
        {
            try
            {
                AgePrimitiveLabel[] labels = group.GetComponentsInChildren<AgePrimitiveLabel>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    string text = AgeText.Label(labels[i]);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        private static AgeTransform ChildAt(AgeTransform table, int index)
        {
            try
            {
                IList<AgeTransform> children = table.Children;
                return children != null && index < children.Count ? children[index] : null;
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

        private static StarSystemScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<StarSystemScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

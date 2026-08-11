using System;
using System.Collections.Generic;
using Amplitude.Unity.Gui;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// One planet, looked at on its own: the page the game takes the player to when they click a planet
    /// on a system's management page.
    ///
    /// Like the galaxy and the system page it is not a window but a VIEW LEVEL - the camera flies onto
    /// the planet and three unrelated panels are drawn over it - so being ours is "is the game at the
    /// planet overview level", and the three view levels are never up together.
    ///
    /// It is a page you READ. The game offers nothing to do to a planet from here: colonizing and
    /// renaming live on the system page's planet cards, and the only two controls drawn are the game's
    /// own Previous and Next Planet buttons, which walk the system's planets one at a time. They are
    /// declared as the ordinary buttons they are drawn as - there is no cycling gesture, because a key
    /// that only works on one page is a key nobody finds.
    ///
    /// The layout is the layout: the two side panels down the left edge (what the planet is called,
    /// what it produces, who lives on it), then the card down the right (its status, what kind of world
    /// it is, what has been found on it, how good it is at each of the four outputs, and - for a unique
    /// planet - the game's paragraph about it). Everything is declared from what is DRAWN, so a planet
    /// with no anomalies has no anomaly row and one that is not unique has no lore.
    ///
    /// The card's rows are read as they are drawn - a heading and its value in one phrase, "Status
    /// Colonized" - with every tooltip drawn anywhere in the row going to the review buffer, because a
    /// row like Type carries two: the game's explanation of what "type" means and its paragraph about
    /// Terran worlds.
    ///
    /// Cycling planets is the one thing that happens to the player rather than being done to a control
    /// they are standing on: they press Enter on Next Planet and every other thing on the page changes.
    /// So the new planet is announced passively, the same way the galaxy announces a new turn.
    /// </summary>
    public sealed class PlanetOverviewScreen : Screen
    {
        private static readonly object InfoStop = "planet:info";
        private static readonly object PopulationStop = "planet:population";
        private static readonly object CardStop = "planet:card";

        /// <summary>The clusters the game draws over every view level. They are drawn over this page
        /// too, and until they were declared here they were on the screen and out of reach.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

        /// <summary>Shared by the population units so up and down across them keep the column.</summary>
        private static readonly object PopulationRowKey = "planet:population-row";

        /// <summary>How far into a row to look for the tooltips that belong to it.</summary>
        private const int TooltipDepth = 4;

        /// <summary>Reused across builds rather than allocated per frame: Build runs every tick.
        /// </summary>
        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>Set once the page has finished arriving, cleared when the game unbinds the planet.
        /// Instance state, so a hot reload starts it over rather than inheriting a stale answer.
        /// </summary>
        private bool _arrived;

        /// <summary>
        /// How long the card is given to draw itself before the page finishes arriving on its behalf,
        /// and how long the game is left alone afterwards. Frames, because the poll that asks is
        /// per-frame: about a third of a second and about a second at the rate the game runs at.
        ///
        /// The settle is what keeps the ordinary case untouched. The game's own show completes a frame
        /// or two after the camera stops, and the whole time it is waiting the card looks exactly like
        /// a card that will never come.
        /// </summary>
        private const int CardSettleFrames = 20;

        private const int CardPauseFrames = 60;

        /// <summary>
        /// The card's own show, done for it when the game loses it.
        ///
        /// <c>PlanetScreen.BindPlanet</c> asks the card to show itself, and the card defers the reveal
        /// to a coroutine that waits for the camera to stop moving before it measures where the planet
        /// is on screen. That coroutine gives up outright when there is no camera to measure against
        /// (PlanetLabel.cs:443 - two bare <c>yield break</c>s), which is the state the game is in for
        /// part of every flight onto a planet, and NOTHING retries: the card ends up bound to the
        /// planet and permanently hidden, so the page draws its left-hand panels and no card at all.
        /// Asking the card to show itself again once the camera is back is the whole repair - it is
        /// the game's own call, and it completes normally.
        /// </summary>
        private readonly Nudge _finishArriving = new Nudge(CardSettleFrames, CardPauseFrames);

        /// <summary>The planet the last announcement was about, so cycling says the new one once.
        /// </summary>
        private Planet _announced;

        public override string Key
        {
            get { return "screen.planet"; }
        }

        /// <summary>The same layer as the galaxy and the system page: the three are the game's view
        /// levels over the one map, and no two of them are ever up together.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        public override string ScreenName
        {
            get { return ModStrings.Get(ModStrings.ScreenPlanet); }
        }

        /// <summary>The info panel, which is drawn first and holds the planet's name - and Tab does not
        /// wrap, so the cursor has to start at the first stop.</summary>
        public override object InitialFocusStop
        {
            get { return InfoStop; }
        }

        /// <summary>
        /// Ours while the camera is on a planet and nothing has replaced the page.
        ///
        /// Arriving and leaving are different questions. We arrive when the WINDOW says it is shown and
        /// ready with a planet bound - the page, not any one piece of it.
        ///
        /// The card is deliberately not part of this. It is drawn a frame or two after the side panels
        /// and the game can lose it altogether (see <see cref="_finishArriving"/>), and a page that
        /// waited for it said NOTHING when it never came - the left-hand panels were on the screen,
        /// the player heard silence, and there was no way out but the mouse. What the card being late
        /// really costs is the cursor: it must not seat on the card and it must not seat on the HUD, so
        /// the page declares its stops in drawn order and the card's simply joins on a later rebuild
        /// when the card turns up. Either way the cursor starts on the info panel.
        ///
        /// We leave when the VIEW LEVEL stops being a planet - not when the window stops holding one,
        /// and not on the GUI's answer to which level is up. Stepping to the next planet is this same
        /// level re-entered with a different planet, and the way the game does that is to unbind the
        /// window, drop the GUI's idea of the current level for a few frames, and bind it all again.
        /// A screen that left on either of those would pop and come back for every press of Next: the
        /// player would lose the button they were standing on and hear the page announce itself afresh
        /// each time. Staying up and declaring nothing while the window is empty costs nothing - a
        /// screen with nothing on it leaves the cursor exactly where it was - and is what makes
        /// stepping through a system's planets a matter of pressing one key repeatedly.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                GalaxyViewLevel_PlanetOverview level =
                    GalaxyViewLevels.LevelThroughTransitions as GalaxyViewLevel_PlanetOverview;
                if (level == null || level.Planet == null || GalaxyViewLevels.Scanning)
                {
                    _arrived = false;
                    return false;
                }

                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                if (gui == null || gui.IsAnyModalVisible || gui.IsInLoadingWindow)
                {
                    _arrived = false;
                    return false;
                }

                if (!_arrived)
                {
                    PlanetScreen window = Window();
                    _arrived =
                        window != null
                        && window.Planet != null
                        && window.Shown
                        && window.IsReady;
                }

                return _arrived;
            }
            catch (Exception)
            {
                _arrived = false;
                return false;
            }
        }

        /// <summary>Escape is the game's: the window is an input handler of its own and answers the key
        /// by taking the camera back out to the system the planet is in.</summary>
        public override bool Back()
        {
            return false;
        }

        /// <summary>Arrival baselines the watch on the planet that is showing, so opening the page never
        /// announces a planet twice - the screen name and the focused control already say where you
        /// are.</summary>
        public override void OnPush()
        {
            PlanetScreen window = Window();
            _announced = window == null ? null : window.Planet;
            _finishArriving.Forget();
            _hud.Baseline();
        }

        public override void OnPop()
        {
            _announced = null;
            _finishArriving.Forget();
            _hud.Forget();
        }

        /// <summary>Which planet the page is about changes under the player when they press Previous or
        /// Next, and the cursor is on the button rather than on anything that changed - so the new
        /// planet says its name and its status itself, queued behind whatever is speaking.</summary>
        public override void OnUpdate()
        {
            _hud.Update();
            try
            {
                // Not until the window has finished arriving on the new planet: it binds the planet
                // first and redraws the card afterwards, so a frame in between would announce the new
                // planet's name beside the old one's status. Nothing is remembered as announced until
                // it has actually been said, so the wait costs no announcement.
                PlanetScreen window = Window();
                if (window == null || !window.Shown || !window.IsReady)
                {
                    return;
                }

                FinishArriving(window);

                Planet planet = window.Planet;
                PlanetLabel_PlanetOverview card = Card(window);
                if (planet == null || card == null || ReferenceEquals(planet, _announced))
                {
                    return;
                }

                _announced = planet;
                Voice.Say(
                    new MessageBuilder()
                        .ListItem(AgeText.Clean(planet.LocalizedName))
                        .ListItem(AgeText.Label(card.PlanetStatus))
                        .Build(),
                    false
                );
            }
            catch (Exception e)
            {
                Log.Warn("planet: watching which planet is showing threw: " + e);
            }
        }

        /// <summary>
        /// See that the page finishes arriving: the card the game bound to this planet and then never
        /// drew is asked to show itself again, once the camera it measures itself against is back.
        ///
        /// The stuck state is exact and cannot be anything else. The card is bound to the same planet
        /// the window is, and it is neither shown nor on its way in or out - and the game never leaves
        /// a bound card resting hidden, because the only thing that hides one also unbinds it
        /// (<c>PlanetScreen.UnbindPlanet</c>). What it does look like is a card whose show is still
        /// waiting on the camera, which is why this waits (<see cref="CardSettleFrames"/>) before
        /// deciding, only asks while the view is standing still, and then stands back
        /// (<see cref="CardPauseFrames"/>): the reveal it asks for is deferred as well, so for a while
        /// afterwards the card still reads exactly as it did before.
        /// </summary>
        private void FinishArriving(PlanetScreen window)
        {
            PlanetLabel_PlanetOverview card = Label(window);
            if (card == null)
            {
                _finishArriving.Due(false, false);
                return;
            }

            bool lost;
            try
            {
                lost =
                    card.Planet != null
                    && ReferenceEquals(card.Planet, window.Planet)
                    && !card.Shown
                    && !card.Showing
                    && !card.Hiding;
            }
            catch (Exception)
            {
                return;
            }

            bool ready = GalaxyViewLevels.CameraDrawing && !GalaxyViewLevels.ChangingLevel;
            if (!_finishArriving.Due(lost, ready))
            {
                return;
            }

            Log.Info("planet: the card never drew itself - asking the game to show it again");
            try
            {
                card.Show();
            }
            catch (Exception e)
            {
                Log.Warn("planet: showing the card again threw: " + e);
            }
        }

        public override void Build(GraphBuilder builder)
        {
            PlanetScreen window = Window();
            if (window == null || window.Planet == null)
            {
                return;
            }

            PlanetLabel_PlanetOverview card = Card(window);
            PlanetInfoSidePanel info = Panel<PlanetInfoSidePanel>();
            PlanetPopulationSidePanel population = Panel<PlanetPopulationSidePanel>();
            if (info == null)
            {
                return;
            }

            // Down the screen: the empire's banners in the top-left corner and the name of the view in
            // the centre, then the page itself,
            // then the right-hand edge - a collapsed tutorial's bar and the notification icons under
            // it - and the turn controls in the bottom corner. Same order as every other view level,
            // because the game draws them in the same places whichever one is up.
            _hud.Top(builder);

            builder.BeginStop(InfoStop);
            BuildInfo(builder, info, window.Planet);

            builder.BeginStop(PopulationStop);
            BuildPopulation(builder, population);

            // Only while it is actually on the screen. A card the game has lost is not a stop that
            // reads empty, it is a stop that does not exist yet - and it joins the page on whichever
            // rebuild follows the card turning up.
            if (card != null)
            {
                builder.BeginStop(CardStop);
                BuildCard(builder, card);
            }

            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.Turn(builder);
        }

        // ---- the info panel ----

        /// <summary>The planet's name with the game's own two navigation buttons beside it, then the
        /// five outputs under them, in the two bands they are drawn in.</summary>
        private void BuildInfo(GraphBuilder builder, PlanetInfoSidePanel panel, Planet planet)
        {
            _cells.Clear();
            AddReadout(_cells, Transform(panel.PlanetTitleLabel), "planet:name");
            AddButton(_cells, panel.PreviousPlanetButton, "planet:previous");
            AddButton(_cells, panel.NextPlanetButton, "planet:next");
            AddFidsi(_cells, panel, planet);
            Emit(builder, _cells, null);
        }

        /// <summary>
        /// The five numbers along the panel, named by the game's own property titles: what the panel
        /// draws beside each one is an icon, so the words are not on the screen anywhere and the
        /// property is where they live.
        ///
        /// The value is read off the same simulation object the panel reads - the colony's when the
        /// planet has one, the planet's own potential when it does not - rather than off the label,
        /// which is a number on its way to the real one.
        /// </summary>
        private static void AddFidsi(List<Cell> cells, PlanetInfoSidePanel panel, Planet planet)
        {
            FidsiEnumerator fidsi = panel.FidsiEnumerator;
            if (fidsi == null || fidsi.FidsiProperties == null)
            {
                return;
            }

            ColonizedPlanet colony = planet.GetColonizedPlanet(Gui.PlayerEmpire);
            Amplitude.Unity.Simulation.SimulationObject simulation =
                colony != null ? colony.SimulationObject : planet.SimulationObject;
            IList<AgeTransform> items = Children(fidsi.AgeTransform);
            if (simulation == null || items == null)
            {
                return;
            }

            int count = Math.Min(fidsi.DisplayedProperties, fidsi.FidsiProperties.Count);
            for (int i = 0; i < count && i < items.Count; i++)
            {
                GuiSimulationProperty property = fidsi.FidsiProperties[i];
                AgeTransform item = items[i];
                if (property == null || item == null || !AgeWidgets.Visible(item))
                {
                    continue;
                }

                Amplitude.StaticString name = property.Name;
                Amplitude.Unity.Simulation.SimulationObject it = simulation;
                AgeTooltip tooltip = Meaningful(AgeWidgets.Raw(item));
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeText.Clean(Gui.GetLocalizedTitle(name))),
                        GraphNodes.ValuePart(() => Amount(it.GetPropertyValue(name))),
                    },
                    Sections = GraphNodes.Sections(null, tooltip),
                };

                PointAtTooltip(vtable, tooltip, item);
                Add(cells, item, ControlId.Referenced(item, "planet:fidsi/" + name), vtable);
            }
        }

        // ---- the population panel ----

        /// <summary>
        /// Who lives here: the panel's own heading with the count beside it, then one entry per kind of
        /// population, each carrying its dossier on its tooltip.
        ///
        /// Nothing here is activatable. The panel is a breakdown, and the whole of what the page offers
        /// a player is reading it.
        /// </summary>
        private void BuildPopulation(GraphBuilder builder, PlanetPopulationSidePanel panel)
        {
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            _cells.Clear();
            AgeTransform count = Transform(panel.PopulationCountLabel);
            AddReadout(_cells, count == null ? null : count.Parent, "planet:population-summary");
            Emit(builder, _cells, null);

            _cells.Clear();
            IList<AgeTransform> units = Children(panel.PopulationsContainer);
            for (int i = 0; units != null && i < units.Count; i++)
            {
                AddPopulation(_cells, units[i], i);
            }

            Emit(builder, _cells, PopulationRowKey);
        }

        private static void AddPopulation(List<Cell> cells, AgeTransform widget, int index)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            PopulationCount unit = Component<PopulationCount>(widget);
            if (unit == null)
            {
                return;
            }

            AgeTooltip tooltip = Meaningful(unit.Tooltip);
            AgePrimitiveLabel count = unit.Count;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => TooltipTitle(tooltip)),
                    GraphNodes.ValuePart(() => AgeText.Label(count)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };

            PointAtTooltip(vtable, tooltip, widget);
            Add(
                cells,
                widget,
                ControlId.Referenced(widget, "planet:population/" + index),
                vtable
            );
        }

        private static string TooltipTitle(AgeTooltip tooltip)
        {
            return AgeWidgets.TooltipTitle(tooltip);
        }

        // ---- the card ----

        /// <summary>
        /// The card down the right-hand side, row by row in the order it is drawn: the planet's name,
        /// the paragraph a unique planet gets, then everything in the content table - the "Unique
        /// Planet" subtitle, status, type, size, the climate and biodiversity lines, whatever deposits,
        /// anomalies and curiosities have been found, how depleted it is, and its four output ratings.
        ///
        /// Which of those exist is the game's answer, read from what is drawn, and the rows are ordered
        /// by where they ended up on screen rather than by the order the table holds them in - a hidden
        /// row keeps the position it had when it was last shown, so the model's order is not the
        /// screen's.
        /// </summary>
        private void BuildCard(GraphBuilder builder, PlanetLabel_PlanetOverview card)
        {
            _cells.Clear();
            AddReadout(_cells, Transform(card.PlanetTitle), "planet:title");
            AddReadout(_cells, Transform(card.LoreLabel), "planet:lore");

            IList<AgeTransform> rows = Children(card.ContentTable);
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                AddCardRows(_cells, rows[i]);
            }

            Emit(builder, _cells, null);
        }

        /// <summary>
        /// One row of the content table, or - for the tables inside it - one node per thing they hold.
        ///
        /// Told apart by what the widget draws rather than by its name: a row whose own words are on it
        /// or on its children is a line, and one whose children are groups with the words inside THEM
        /// is a table of things. That is the difference between "Status Colonized" and a list of
        /// anomalies, and it holds for the tables this fixture cannot show.
        /// </summary>
        private static void AddCardRows(List<Cell> cells, AgeTransform row)
        {
            if (row == null || !AgeWidgets.Visible(row))
            {
                return;
            }

            if (Component<AgePrimitiveLabel>(row) != null || HasLabelChild(row))
            {
                AddRow(cells, row, Name(row));
                return;
            }

            IList<AgeTransform> items = Children(row);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] != null && AgeWidgets.Visible(items[i]))
                {
                    // Named under the table they came out of: two tables pool their rows from
                    // different prefabs and nothing stops both calling them "Item000", and two nodes
                    // sharing a structural key take the whole screen down.
                    AddRow(cells, items[i], Name(row) + "/" + Name(items[i]));
                }
            }
        }

        private static bool HasLabelChild(AgeTransform widget)
        {
            IList<AgeTransform> children = Children(widget);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (children[i] != null && Component<AgePrimitiveLabel>(children[i]) != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A row of the card as one control: everything it draws in one phrase, the tooltip
        /// that explains its value announced or indicated by the usual rule, and every tooltip drawn
        /// anywhere in the row in the review buffer - the Type row alone carries two.</summary>
        private static void AddRow(List<Cell> cells, AgeTransform row, string key)
        {
            string text = AgeWidgets.TextOf(row);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            AgeTransform it = row;
            AgeTooltip tooltip = PrimaryTooltip(row);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(it)),
                },
                Sections = RowSections(it, tooltip),
            };

                        PointAtTooltip(vtable, tooltip, row);
            Add(cells, row, ControlId.Referenced(row, "planet:row/" + key), vtable);
        }

        /// <summary>The tooltip that explains what the row SAYS: the row's own where it has one - the
        /// trait dossier a climate line carries - and otherwise the last one drawn along it, which is
        /// the value's rather than the heading's.</summary>
        private static AgeTooltip PrimaryTooltip(AgeTransform row)
        {
            AgeTooltip own = Meaningful(AgeWidgets.Raw(row));
            if (own != null)
            {
                return own;
            }

            IList<AgeTransform> children = Children(row);
            for (int i = children == null ? -1 : children.Count - 1; i >= 0; i--)
            {
                if (children[i] == null || !AgeWidgets.Visible(children[i]))
                {
                    continue;
                }

                AgeTooltip tooltip = Meaningful(AgeWidgets.Raw(children[i]));
                if (tooltip != null)
                {
                    return tooltip;
                }
            }

            return null;
        }

        /// <summary>Every tooltip drawn in a row as declared sections, in drawn order - and only the
        /// one that explains what the row SAYS speaks, because what follows it on a card is a badge,
        /// not a value.</summary>
        private static IList<NodeSection> RowSections(AgeTransform row, AgeTooltip said)
        {
            List<AgeTooltip> found = new List<AgeTooltip>();
            CollectTooltips(row, found, TooltipDepth);
            List<NodeSection> sections = null;
            for (int i = 0; i < found.Count; i++)
            {
                NodeSection section = GraphNodes.TooltipSection(
                    found[i],
                    found[i] == said ? null : (TooltipMode?)TooltipMode.None
                );
                if (section == null)
                {
                    continue;
                }

                if (sections == null)
                {
                    sections = new List<NodeSection>(found.Count);
                }

                sections.Add(section);
            }

            return sections;
        }

        private static void CollectTooltips(AgeTransform widget, List<AgeTooltip> found, int depth)
        {
            if (widget == null || depth < 0)
            {
                return;
            }

            try
            {
                if (!widget.Visible)
                {
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }

            AgeTooltip tooltip = Meaningful(AgeWidgets.Raw(widget));
            if (tooltip != null)
            {
                found.Add(tooltip);
            }

            IList<AgeTransform> children = Children(widget);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                CollectTooltips(children[i], found, depth - 1);
            }
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

        /// <summary>Declare a panel's controls in the bands they are drawn in - measured, so a row the
        /// game inserts between two others is walked where it appears.</summary>
        private static void Emit(GraphBuilder builder, List<Cell> cells, object rowKey)
        {
            foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
            {
                builder.StartRow(rowKey);
                foreach (Cell cell in row)
                {
                    builder.AddItem(cell.Id, cell.Vtable);
                }

                builder.EndRow();
            }
        }

        private static void Add(
            List<Cell> cells,
            AgeTransform widget,
            ControlId id,
            NodeVtable vtable
        )
        {
            cells.Add(new Cell { Widget = widget, Id = id, Vtable = vtable });
        }

        private static void AddReadout(List<Cell> cells, AgeTransform widget, string key)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = Meaningful(AgeWidgets.Raw(widget));
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(it)),
                },
                Sections = RowSections(it, tooltip),
            };

                        PointAtTooltip(vtable, tooltip, widget);
            Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        private static void AddButton(List<Cell> cells, AgeControlButton button, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = Meaningful(AgeWidgets.Raw(widget));
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TextOf(it),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(it),
                tooltip
            );
            AgeWidgets.Point(vtable, button);
            Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        /// <summary>
        /// Make the game draw the tooltip this control was declared with, which means pointing at the
        /// widget the tooltip is actually ON.
        ///
        /// The card's rows do not carry their own: an anomaly's dossier hangs off the title inside the
        /// row, not off the row, and pointing at the row showed nothing at all while the readout went
        /// on saying the control had a tooltip. A control with no tooltip anywhere still gets pointed
        /// at, so that anything hoverable under it lights up.
        /// </summary>
        private static void PointAtTooltip(
            NodeVtable vtable,
            AgeTooltip tooltip,
            AgeTransform fallback
        )
        {
            AgeTransform at = fallback;
            try
            {
                if (tooltip != null && tooltip.AgeTransform != null)
                {
                    at = tooltip.AgeTransform;
                }
            }
            catch (Exception) { }

            AgeWidgets.PointAt(vtable, at);
        }
        /// <summary>A tooltip only when there is something behind it. AGE hangs a tooltip component on
        /// widgets that never get one filled in, and an empty one would be picked as a row's
        /// explanation and then say nothing.</summary>
        private static AgeTooltip Meaningful(AgeTooltip tooltip)
        {
            try
            {
                return tooltip != null
                    && (!string.IsNullOrEmpty(tooltip.Class) || !string.IsNullOrEmpty(tooltip.Content))
                    ? tooltip
                    : null;
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

        private static AgeTransform Transform(AgePrimitiveLabel label)
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

        private static IList<AgeTransform> Children(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Children;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static T Component<T>(AgeTransform widget)
            where T : UnityEngine.Component
        {
            try
            {
                return widget == null ? null : widget.GetComponent<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The card whether or not it is on the screen - what the window HAS, as against what
        /// <see cref="Card"/> answers, which is what the player can see.</summary>
        private static PlanetLabel_PlanetOverview Label(PlanetScreen window)
        {
            try
            {
                return window == null ? null : window.PlanetLabel;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static PlanetLabel_PlanetOverview Card(PlanetScreen window)
        {
            try
            {
                PlanetLabel_PlanetOverview card = window == null ? null : window.PlanetLabel;
                return card != null && AgeWidgets.Visible(card.InfoGroup) ? card : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One of the panels the planet screen adds to the shared side-panel window. Found by
        /// type rather than held, because the window that owns them is not ours and rebuilds them.
        /// </summary>
        private static TPanel Panel<TPanel>()
            where TPanel : SidePanel
        {
            try
            {
                SidePanelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<SidePanelsWindow>(false)
                    : null;
                TPanel panel = window == null ? null : window.GetComponentInChildren<TPanel>(true);
                return panel != null && AgeWidgets.Visible(panel.AgeTransform) ? panel : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static PlanetScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<PlanetScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

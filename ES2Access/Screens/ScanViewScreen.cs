using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The scan view - the game's own X-ray of whatever the player is looking at.
    ///
    /// It is a MODE rather than a place: the camera stays where it was and the game swaps every label
    /// on the map for a different set. Which set is not a choice the player makes. The map's zoom step
    /// picks a layer descriptor, the descriptor picks the lens, and the lens decides what the labels
    /// mean - so zooming, which everywhere else only changes how much is drawn, here changes the
    /// SUBJECT. That is why the lens announces itself on arrival and again every time the descriptor
    /// changes: a sighted player reads the lens's name across the top of the screen, and a player who
    /// zoomed one step would otherwise be reading trade figures believing them to be diplomacy.
    ///
    /// Which is why the mode carries a zoom of its own (<see cref="BuildZoom"/>). Every other page can
    /// leave the camera to the mouse; here the camera is what decides WHICH of the six lenses the player
    /// is reading, so a page with no zoom is a page with five lenses and a system's planets missing.
    ///
    /// Before this screen the mod was silent here. Every other page gates on the game's "normal view",
    /// which the scan view turns off, and the game hides the window each of those pages is built from -
    /// so entering the lens took the keyboard away entirely and left only the End Turn button. That is
    /// also why nothing of the galaxy page can be reused: the system labels, the fleet lozenges and the
    /// orbital cards are all hidden, and what the lens draws instead is its own set of labels.
    ///
    /// There is no key to get in here and none to get out. Getting in is the scan button the game draws
    /// beside the view's name (GlobalHud.ViewTitle), and Escape and right-click are the game's own way
    /// out, which this screen deliberately does not consume.
    ///
    /// The lenses are not dispatched on. Each of them draws its own window, and the game hides every
    /// window but the live one - so every lens is offered every frame and the DRAWN one is the only one
    /// that contributes anything. A lens this mod has never seen still gets its labels read, and a lens
    /// mapped to the wrong zoom step cannot make the screen describe something that is not on screen.
    ///
    /// What the lens NAME is called comes from the game rather than from a copy of its table: the panel
    /// across the top of every other page keeps a label naming the lens for the current zoom layer, and
    /// the game goes on writing it while the panel itself is hidden.
    ///
    /// The battle lens cannot be reached from a save with no battle in it, so its labels - which are
    /// ships, flotillas and damage numbers the renderer creates and destroys as the fight goes - are
    /// read by the same drawn-labels reading as any lens without a model of its own
    /// (<c>BattleScanViewWindow</c>, verified by class only; a manual test is handed over for it). The
    /// hacking dashboard and its banners are not modelled: the game switches all three off outright for
    /// an installation without that content (<c>ScanOverlayWindow.OnGameCreated</c>), which is this one.
    /// </summary>
    public sealed class ScanViewScreen : Screen
    {
        private static readonly object TitleStop = "scan:title";
        private static readonly object ContentStop = "scan:content";
        private static readonly object LegendStop = "scan:legend";

        private static readonly object TradeRegion = "scan:content/trade";
        private static readonly object PlanetsRegion = "scan:content/planets";

        /// <summary>The clusters the game keeps drawing over the lens - which is only the turn controls;
        /// it hides the banners, the pinned quest and the notification strip.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

        /// <summary>The lens the player has already been told about, and the layer descriptor it was
        /// read from. The descriptor outlives the screen - the game keeps it up to date whether or not
        /// the lens is up - so arriving baselines against what is showing rather than against nothing,
        /// and the arrival announcement is not said twice.</summary>
        private string _lens;
        private string _descriptor;

        /// <summary>Whether the lens has finished showing itself since the mode was entered - the
        /// arrival gate, held until the mode ends.</summary>
        private bool _arrived;

        /// <summary>How long the zoom's value waits for a view level the game has been asked for - about
        /// half a second, which is longer than the game takes to begin a transition and short enough that
        /// a refused request is not left mute.</summary>
        private const int ZoomSettleFrames = 30;

        /// <summary>The rung the last zoom press was made from, and what is left of its wait.</summary>
        private int _zoomFrom = -1;
        private int _zoomWait;

        /// <summary>The title strip each lens window draws for itself. The windows live for the whole
        /// session and instantiate their sections once, so these are found once per showing rather than
        /// per frame; instance state, so a hot reload takes them with the screen.</summary>
        private ScanViewWindowHeader[] _headers;

        /// <summary>The six labels the system lens rings a star with. Same reasoning: the panel creates
        /// them once and reuses them for whichever system the camera is nearest.</summary>
        private ScanViewSystemOverviewFidsiLabel[] _fidsi;

        /// <summary>Scratch for reading how many captions the live lens declares, and the context its
        /// prerequisites are asked in. Reused rather than allocated, because the legend is read on every
        /// rebuild.</summary>
        private readonly List<ScanViewCaptionGroupGuiElement> _legend =
            new List<ScanViewCaptionGroupGuiElement>();

        private readonly Amplitude.Unity.Framework.PrerequisiteContext _prerequisites =
            new Amplitude.Unity.Framework.PrerequisiteContext();

        public override string Key
        {
            get { return "screen.scan-view"; }
        }

        /// <summary>Just above the view levels it overlays and well below everything that can be raised
        /// over it. It is not one of them: the game keeps the view level underneath and merely stops
        /// calling it normal, so this screen is drawn over the galaxy, a system or a planet alike.
        /// </summary>
        public override int Layer
        {
            get { return 11; }
        }

        /// <summary>The game's own name for the lens that is up. Said on arrival, which is the whole
        /// point: the lens is what the mode MEANS.</summary>
        public override string ScreenName
        {
            get { return LensName(); }
        }

        /// <summary>
        /// Ours while the game is in the scan view and nothing has replaced it. Which view level is
        /// underneath makes no difference - the galaxy, a system's page and a planet's page all hand
        /// over to their own lens - so unlike every other page this one does not ask.
        ///
        /// Arriving waits for the lens to have drawn ITSELF. The game turns "normal view" off a good
        /// number of frames before it shows the lens's own windows, and for those frames the only thing
        /// on the screen is the turn controls - so a screen that arrived on the mode alone seated the
        /// cursor on the End Turn button and left it there, because a cursor is placed once. The wait is
        /// an ARRIVAL gate and nothing more: once the lens is up the screen stays ours until the mode
        /// ends, so the frames where the game is fading the lens back out do not hand the player to the
        /// page underneath and back again.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                GuiManager gui = GuiState();
                bool scanning =
                    gui != null
                    && gui.IsInScanView
                    && !gui.IsAnyScreenVisible
                    && !gui.IsAnyModalVisible
                    && !gui.IsInLoadingWindow;
                if (!scanning)
                {
                    _arrived = false;
                    return false;
                }

                _arrived = _arrived || Drawn();
                return _arrived;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether a lens has anything of its own on the screen yet. Every lens but the battle's
        /// draws a title strip; the battle's has no section at all, so it answers for itself.
        ///
        /// Drawn is not enough: the game switches the lens's controls on a frame AFTER it shows them, so
        /// a screen arriving the moment the strip appears reads it "unavailable" once - once, and then
        /// never again, because a live part only re-speaks on change and by then the player has heard
        /// it.</summary>
        private bool Drawn()
        {
            ScanViewWindowHeader header = DrawnHeader();
            if (header != null)
            {
                return AgeWidgets.Operable(header.AgeTransform);
            }

            BattleScanViewWindow battle = Window<BattleScanViewWindow>();
            return battle != null && battle.Shown;
        }

        /// <summary>Escape is the game's: it is what leaves the lens, and the mod inventing a way out of
        /// a mode the game already knows how to leave would be a second answer to one question.
        /// </summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        public override bool Back()
        {
            return false;
        }

        public override object InitialFocusStop
        {
            get { return TitleStop; }
        }

        public override void OnPush()
        {
            _hud.Baseline();
            _headers = null;
            _fidsi = null;
            _descriptor = Descriptor();
            _lens = LensName();
        }

        public override void OnPop()
        {
            _zoomWait = 0;
            _zoomFrom = -1;
            _hud.Forget();
            _headers = null;
            _fidsi = null;
            _descriptor = null;
            _lens = null;
        }

        public override void OnUpdate()
        {
            _hud.Update();
            AnnounceLens();
            WatchZoom();
        }

        /// <summary>The lens has changed under the player - they zoomed, or they walked into a system -
        /// and everything on the screen now means something else. Queued, never interrupting: it is
        /// something that happened rather than an answer to a key.</summary>
        private void AnnounceLens()
        {
            try
            {
                string descriptor = Descriptor();
                if (descriptor == _descriptor)
                {
                    return;
                }

                _descriptor = descriptor;
                string lens = LensName();
                if (string.IsNullOrEmpty(lens) || lens == _lens)
                {
                    return;
                }

                _lens = lens;
                Voice.Say(lens, false);
            }
            catch (Exception e)
            {
                Log.Warn("scan: watching the lens threw: " + e);
            }
        }

        /// <summary>Down the screen: the lens's own title across the top, then what it draws over the
        /// map, then the legend down the left edge, then the turn controls in the corner.</summary>
        public override void Build(GraphBuilder builder)
        {
            builder.BeginStop(TitleStop);
            BuildTitle(builder);
            BuildZoom(builder);

            builder.BeginStop(ContentStop);
            BuildDiplomacy(builder);
            BuildNodes(builder);
            BuildSystemOverview(builder);
            BuildSystemManagement(builder);
            BuildPlanet(builder);
            BuildUnmodelled(builder);

            builder.BeginStop(LegendStop);
            BuildLegend(builder);

            _hud.Turn(builder);
        }

        // ---- the title ----

        /// <summary>The strip the live lens draws where every other page draws the view's name, and it
        /// is a button for the same reason that one is: pressing it leaves.</summary>
        private void BuildTitle(GraphBuilder builder)
        {
            ScanViewWindowHeader header = DrawnHeader();
            if (header == null)
            {
                return;
            }

            ScanViewWindowHeader it = header;
            AgeControlButton button = header.Button;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Label(it.TitleLabel),
                () => AgeWidgets.Press(button),
                () => AgeWidgets.Operable(it.AgeTransform),
                AgeWidgets.Raw(it.AgeTransform)
            );
            AgeWidgets.Point(vtable, button, AgeWidgets.Raw(it.AgeTransform), it.AgeTransform);
            builder.AddItem(ControlId.Referenced(header, "scan:title/lens"), vtable);
        }

        // ---- the zoom ----

        /// <summary>
        /// How close the game is looking, as something the player can move.
        ///
        /// This lens is the one page where the zoom is not a matter of how much is drawn: the map's zoom
        /// step picks the layer, the layer picks the lens, and so zooming changes the SUBJECT. The game's
        /// own answer for a keyboard is two keys HELD down (PageUp and PageDown, polled while pressed -
        /// a tap moves nothing) and nothing at all once the game is inside a system, which left the whole
        /// zoom-dependent surface - every lens but the one the camera happened to be on, and the planets
        /// of a system - out of reach. So the mode carries the zoom as an adjustable of its own, on the
        /// arrows the mod already spends on a value, and the ladder runs all the way from the whole
        /// galaxy to one planet (<see cref="GalaxyViewLevels.StepZoom"/>).
        ///
        /// The value is the rung and nothing else: what a rung MEANS is the lens's name, which the screen
        /// announces whenever it changes, and repeating it here would say it twice. While the game is
        /// flying between two view levels there is no rung to report - the answer is a step behind - so
        /// the value says nothing and the lens announcement carries the news.
        /// </summary>
        private void BuildZoom(GraphBuilder builder)
        {
            if (GalaxyViewLevels.ZoomRung < 0)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Slider(
                () => ModStrings.Get(ModStrings.ScanZoom),
                ZoomText,
                Zoom
            );
            builder.AddItem(ControlId.Structural("scan:zoom"), vtable);
        }

        /// <summary>One rung, and then the wait for a rung the game has not moved to yet. A press that
        /// asks for a VIEW LEVEL is deferred - the game starts flying a frame or two later - so the rung
        /// read straight afterwards is still the one the player has just left, and saying it answers
        /// "nothing happened" to a press that did something.</summary>
        private void Zoom(int sign, bool coarse)
        {
            int before = GalaxyViewLevels.ZoomRung;
            if (!GalaxyViewLevels.StepZoom(sign, coarse) || GalaxyViewLevels.ZoomRung != before)
            {
                return;
            }

            _zoomFrom = before;
            _zoomWait = ZoomSettleFrames;
        }

        private string ZoomText()
        {
            int rung = GalaxyViewLevels.ZoomRung;
            int rungs = GalaxyViewLevels.ZoomRungs;
            if (
                rung < 0
                || rungs <= 0
                || GalaxyViewLevels.ChangingLevel
                || (_zoomWait > 0 && rung == _zoomFrom)
            )
            {
                return null;
            }

            return new MessageBuilder().PushFraction(rung + 1, rungs).Build();
        }

        /// <summary>Counts the wait above down, and ends it the moment the rung moves - so the value
        /// speaks itself as soon as it is true, and a request the game refused goes quiet again instead of
        /// staying silent for good.</summary>
        private void WatchZoom()
        {
            if (_zoomWait <= 0)
            {
                return;
            }

            _zoomWait--;
            if (GalaxyViewLevels.ZoomRung != _zoomFrom)
            {
                _zoomWait = 0;
            }
        }

        // ---- the diplomacy lens ----

        /// <summary>
        /// One row per label the diplomacy lens is drawing - which is not one per system: the lens draws
        /// a label only where there is something to say, a major empire's home system or a battle
        /// (<c>ScanViewDiplomacyLabel.HasSomethingToShow</c>).
        ///
        /// The label writes the OWNER's name on itself and not the system's, because on the map the
        /// system is the place the label is standing on. A keyboard player has no such place, so the row
        /// is named by the system and says the label's words as its value - a deliberate deviation from
        /// what is drawn, and the only one here.
        /// </summary>
        private void BuildDiplomacy(GraphBuilder builder)
        {
            DiplomacyScanViewWindow window = Window<DiplomacyScanViewWindow>();
            if (window == null || !window.Shown || window.NodeLabelsContainer == null)
            {
                return;
            }

            try
            {
                IList<AgeTransform> children = window.NodeLabelsContainer.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    ScanViewDiplomacyLabel label =
                        children[i] == null
                            ? null
                            : children[i].GetComponent<ScanViewDiplomacyLabel>();
                    if (
                        label == null
                        || label.GameNode == null
                        || !AgeWidgets.Visible(label.AgeTransform)
                    )
                    {
                        continue;
                    }

                    AddDiplomacyRow(builder, label);
                }
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the diplomacy lens threw: " + e);
            }
        }

        private static void AddDiplomacyRow(GraphBuilder builder, ScanViewDiplomacyLabel label)
        {
            ScanViewDiplomacyLabel it = label;
            GameNode node = label.GameNode;
            ControlId id = ControlId.Referenced(node, "scan:diplomacy/" + node.GUID);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => node.LocalizedName),
                    GraphNodes.ValuePart(() => AgeWidgets.TextOf(it.MainLinesContainer)),
                },
                Sections = GraphNodes.Sections(
                    NodeSection.Buffer(() => AgeWidgets.DrawnLines(it.MainLinesContainer))
                ),
            };
            AgeWidgets.PointAt(vtable, label.AgeTransform);

            AgeControlToggle swap = label.SwapToggle;
            AgeTransform line = AgeWidgets.Transform(swap);
            if (line == null || !AgeWidgets.Visible(line))
            {
                builder.AddItem(id, vtable);
                return;
            }

            // The toggle IS the empire's name line, so it is named by the words the game wrote on it.
            // Switching it points the whole lens at that empire's diplomacy instead of the player's; the
            // game switches it off, with no words at all, wherever there is nobody else to watch.
            AgeControlToggle toggle = swap;
            NodeVtable child = GraphNodes.Checkbox(
                () => AgeText.Label(it.EmpireNameLabel),
                () => toggle.State,
                () => AgeWidgets.Toggle(toggle),
                () => AgeWidgets.Operable(line),
                AgeWidgets.Raw(line)
            );
            AgeWidgets.PointAt(child, line);

            builder.BeginGroup(id, vtable);
            builder.AddItem(
                ControlId.Referenced(swap, "scan:diplomacy/" + node.GUID + "/watch"),
                child
            );
            builder.EndGroup();
        }

        // ---- the trade and economy lenses ----

        /// <summary>
        /// One row per label the trade and economy lenses draw, which is one per node the camera is
        /// looking at and the empire can see (<c>ScanNodeLabelsWindow.RefreshLabelsVisibilityAndPosition</c>
        /// - culling first, then the empire's own visibility). Both lenses draw the same labels; what
        /// separates them is which lines inside each label the layer fades up, so there is nothing here
        /// to tell them apart and nothing that should.
        ///
        /// The label's own name goes in the readout and its figures - the trading company, the traitor
        /// count, the ratings - are the value. Everything else it draws is an icon with a tooltip, which
        /// is what the buffer is for.
        /// </summary>
        private void BuildNodes(GraphBuilder builder)
        {
            ScanNodeLabelsWindow window = Window<ScanNodeLabelsWindow>();
            if (window == null || !window.Shown || window.LabelsContainer == null)
            {
                return;
            }

            try
            {
                IList<AgeTransform> children = window.LabelsContainer.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    ScanNodeLabel label =
                        children[i] == null ? null : children[i].GetComponent<ScanNodeLabel>();
                    if (
                        label == null
                        || label.GameNode == null
                        || !AgeWidgets.Visible(label.AgeTransform)
                    )
                    {
                        continue;
                    }

                    ScanNodeLabel it = label;
                    GameNode node = label.GameNode;
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => NodeName(it)),
                            GraphNodes.ValuePart(
                                () => AgeWidgets.TextOf(it.TraitorAndTradeLine)
                            ),
                        },
                        Sections = GraphNodes.Sections(
                            NodeSection.Buffer(() => AgeWidgets.DrawnLines(it.ContentTable))
                        ),
                    };
                    AgeWidgets.PointAt(vtable, label.AgeTransform);
                    builder.AddItem(
                        ControlId.Referenced(node, "scan:node/" + node.GUID),
                        vtable
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the node labels threw: " + e);
            }
        }

        /// <summary>What the label calls the place, falling back on the model's name for a node whose
        /// name the lens is drawing as a bare circle.</summary>
        private static string NodeName(ScanNodeLabel label)
        {
            try
            {
                string drawn = AgeText.Label(label.NameLabel);
                return string.IsNullOrEmpty(drawn) ? label.GameNode.LocalizedName : drawn;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the system lens ----

        /// <summary>
        /// The system lens inspects ONE system - whichever is nearest the middle of the screen
        /// (<c>StarSystemOverviewScanViewWindow.MaxDistanceToScreenCenter</c>) - so it is one node, with
        /// the six figures it rings the star with as its children and the tick that opens its longer
        /// panel beside them.
        ///
        /// The figures need no help: the game writes each one as a number and the output's own icon, and
        /// the icon has a name.
        ///
        /// It is a lens about one system and NOT about its planets - those belong to the system
        /// management lens a rung further in, which is why the zoom carries on past the closest step
        /// (<see cref="BuildZoom"/>).
        ///
        /// What the tick reveals is read here as well, because the panel it shows is a SIBLING of the
        /// name rather than a child of it: the system's name again, the remains standing on one of its
        /// planets, and the line the game writes instead of all of it for somebody else's colony. The two
        /// rank graphs it also holds are geometry with no words at all and are not modelled (roadmap);
        /// in a save where neither they nor any remains are drawn, the tick changes nothing that can be
        /// heard, which is the truth about that save rather than a gap in the reading.
        /// </summary>
        private void BuildSystemOverview(GraphBuilder builder)
        {
            StarSystemOverviewScanViewWindow window = Window<StarSystemOverviewScanViewWindow>();
            if (window == null || !window.Shown || !AgeWidgets.Visible(window.NodeInfoGroup))
            {
                return;
            }

            try
            {
                StarSystemOverviewScanViewWindow it = window;
                ControlId id = ControlId.Structural("scan:system");
                NodeVtable vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Group,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeText.Label(it.NodeNameLabel)),
                    },
                    Sections = GraphNodes.Sections(NodeSection.Buffer(() => SystemLines(it))),
                };
                AgeWidgets.PointAt(vtable, window.NodeInfoGroup);

                builder.BeginGroup(id, vtable);
                ScanViewSystemOverviewFidsiLabel[] labels = Fidsi(window);
                for (int i = 0; i < labels.Length; i++)
                {
                    ScanViewSystemOverviewFidsiLabel label = labels[i];
                    if (label == null || !AgeWidgets.Visible(label.AgeTransform))
                    {
                        continue;
                    }

                    builder.AddLabel(
                        ControlId.Referenced(label, "scan:system/output/" + i),
                        () => AgeText.Label(label.ValueLabel)
                    );
                }

                AddSystemInfoToggle(builder, window);
                builder.EndGroup();
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the system lens threw: " + e);
            }
        }

        /// <summary>Everything the lens has to say about the system it is inspecting: the strip the name
        /// and the tick sit in, the line of figures above it, and the panel the tick shows - each read
        /// only while it is drawn, so what the buffer holds is what is on the screen.</summary>
        private static IList<string> SystemLines(StarSystemOverviewScanViewWindow window)
        {
            List<string> lines = new List<string>();
            AddDrawn(lines, window.NodeInfoGroup);
            AddDrawn(lines, window.TopLineTable);
            AddDrawn(lines, window.RemainsPanel.Shown ? window.RemainsPanel.AgeTransform : null);
            AddDrawn(
                lines,
                window.InfoPanel.Shown ? window.InfoPanel.InformationInaccessibleLabel.AgeTransform : null
            );
            return lines;
        }

        /// <summary>A panel's drawn words appended to <paramref name="lines"/>, and nothing at all where
        /// the panel is not on the screen.</summary>
        private static void AddDrawn(List<string> lines, AgeTransform widget)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            IList<string> drawn = AgeWidgets.DrawnLines(widget);
            for (int i = 0; drawn != null && i < drawn.Count; i++)
            {
                AddLine(lines, drawn[i]);
            }
        }

        /// <summary>The tick beside the system's name. The game draws it as a bare box, so it is named
        /// here; what it reveals is a second panel off to one side, whose words the node's own buffer
        /// reads once it is showing (<see cref="SystemLines"/>).</summary>
        private static void AddSystemInfoToggle(
            GraphBuilder builder,
            StarSystemOverviewScanViewWindow window
        )
        {
            AgeControlToggle toggle = window.SystemInfoToggle;
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => ModStrings.Get(ModStrings.ScanSystemInfo),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(widget),
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(ControlId.Referenced(toggle, "scan:system/info"), vtable);
        }

        // ---- the system management lens ----

        /// <summary>
        /// The lens over a system's own page: what makes the system worth trading with, and then one
        /// label per planet in it.
        ///
        /// Two captioned groups, so two regions. The planets' figures are the exception to reading what
        /// is drawn: the lens writes each output as a bare number beside its icon, and a spoken "16" is
        /// nothing, so the numbers are read from the planet with the output's own title in front of
        /// them - the five properties the label itself uses
        /// (<c>PlanetLabel_SystemManagementScanView.PlanetFidsiProperties</c>).
        ///
        /// A card SAYS all of itself. It is not a container the player opens - the game draws it as one
        /// thing, four items on a card the size of a stamp - so its state and its synergies are parts of
        /// its readout rather than something to go and find, and the buffer is the same content a line at
        /// a time (<see cref="CardLines"/>). Nothing on the card is left to a tooltip, because it has
        /// none: the status mark carries no <c>AgeTooltip</c> at all, on the prefab or on the data.
        /// </summary>
        private void BuildSystemManagement(GraphBuilder builder)
        {
            StarSystemManagementScanViewWindow window = Window<StarSystemManagementScanViewWindow>();
            if (window == null || !window.Shown)
            {
                return;
            }

            try
            {
                IList<string> trade = AgeWidgets.Visible(window.TradingGroup)
                    ? AgeWidgets.DrawnLines(window.TradingGroup)
                    : null;
                IList<AgeTransform> children =
                    window.PlanetLabelsGroup == null ? null : window.PlanetLabelsGroup.Children;
                // Two halves means two regions; only one of them showing means none, or the jump key
                // would swallow the press and move nothing.
                bool regions = trade != null && trade.Count > 0 && Planets(children) > 0;

                builder.SetRegion(regions ? TradeRegion : null);
                AddDrawnLines(builder, trade, "scan:trade");

                builder.SetRegion(regions ? PlanetsRegion : null);
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    PlanetLabel_SystemManagementScanView label =
                        children[i] == null
                            ? null
                            : children[i].GetComponent<PlanetLabel_SystemManagementScanView>();
                    if (
                        label == null
                        || label.Planet == null
                        || !AgeWidgets.Visible(label.AgeTransform)
                    )
                    {
                        continue;
                    }

                    PlanetLabel_SystemManagementScanView it = label;
                    Planet planet = label.Planet;
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => AgeText.Label(it.PlanetTitle)),
                            GraphNodes.ValuePart(() => Outputs(planet)),
                            GraphNodes.ValuePart(() => Status(planet), false),
                            GraphNodes.ValuePart(() => SynergyText(it), false),
                        },
                        // No sections: the card has no tooltip anywhere on it and nothing it holds is
                        // hidden from the readout, so the buffer the readout itself makes - a line per
                        // part - already is the card a line at a time.
                        // The click a planet's own body takes from a system's page, which is the game's
                        // only route from here to one planet: the lens follows the level, so this is
                        // also the way from the system's planets to the planet's own data sheet.
                        OnActivate = () => GalaxyViewLevels.OpenPlanet(planet),
                    };
                    AgeWidgets.PointAt(vtable, label.AgeTransform);
                    builder.AddItem(
                        ControlId.Referenced(planet, "scan:planet/" + planet.GUID),
                        vtable
                    );
                }

                builder.SetRegion(null);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the system management lens threw: " + e);
            }
        }

        /// <summary>
        /// The strip of icon pairs the lens lines up under a planet's ring: one per way a population of
        /// the empire's suits that planet, drawn as the population's face beside the output it gains and
        /// captioned once above them all.
        ///
        /// Both pictures are named from the icon table - the same reading the mod gives an icon anywhere
        /// else - because the item carries no words and no tooltip of ANY kind (measured on the drawn
        /// cards: no <c>AgeTooltip</c> on the item, on either image, or on the table), so there is nothing
        /// else on the widget to read. The game's own caption ("Population synergies") heads the list.
        ///
        /// Null where the planet has none. The caption label stays drawn over an EMPTY table, so reading
        /// the strip's words would announce a heading with nothing under it on every planet in the system.
        /// </summary>
        private static string SynergyText(PlanetLabel_SystemManagementScanView label)
        {
            try
            {
                AgeTransform table = label.SynergiesTable;
                if (table == null || !AgeWidgets.Visible(table))
                {
                    return null;
                }

                MessageBuilder message = new MessageBuilder();
                IList<AgeTransform> children = table.Children;
                int said = 0;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    StarSystemManagementScanViewPopulationSynergyItem item =
                        children[i] == null
                            ? null
                            : children[i].GetComponent<
                                StarSystemManagementScanViewPopulationSynergyItem
                            >();
                    if (item == null || !AgeWidgets.Visible(item.AgeTransform))
                    {
                        continue;
                    }

                    string pair = SynergyPair(item);
                    if (string.IsNullOrEmpty(pair))
                    {
                        continue;
                    }

                    if (said == 0)
                    {
                        message.Fragment(Caption(label));
                    }

                    message.ListItemForcedComma(pair);
                    said++;
                }

                return said == 0 ? null : message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One synergy: which population and what it gets, in the words the icon table gives the
        /// two pictures. Null when neither picture has a name, which is the icon table saying this is
        /// decoration.</summary>
        private static string SynergyPair(StarSystemManagementScanViewPopulationSynergyItem item)
        {
            string population = Picture(item.AffinityIcon);
            string output = Picture(item.FidsiIcon);
            if (string.IsNullOrEmpty(population))
            {
                return output;
            }

            return string.IsNullOrEmpty(output)
                ? population
                : ModStrings.Format(ModStrings.ScanSynergy, population, output);
        }

        private static string Picture(AgePrimitiveImage image)
        {
            Texture texture = image == null ? null : image.Image;
            return texture == null ? null : IconNames.NameForAsset(texture.name);
        }

        /// <summary>The caption the game writes over the synergies strip, read off the strip's own group -
        /// the caption belongs to the group rather than to the table, so the table alone would lose it.
        /// </summary>
        private static string Caption(PlanetLabel_SystemManagementScanView label)
        {
            AgeTransform table = label.SynergiesTable;
            AgeTransform group = table == null ? null : table.Parent;
            return group == null ? null : AgeWidgets.TextOf(group);
        }

        /// <summary>
        /// What the lens is painting the planet as, in the game's own word for that state.
        ///
        /// The card draws it as one icon and nothing else - the legend's colonized or hospitable mark, or
        /// the picture of whichever improvement stands on a colony - and the icon table names none of
        /// them. So the state comes from the model through the wrapper the game asks the same question of
        /// (<c>GuiPlanet.PlanetStatus</c>, the same expression the map's own planet circles are read by),
        /// which answers with more than the three the icons distinguish: whose colony, whose outpost,
        /// destroyed, hostile, or free to settle.
        /// </summary>
        private static string Status(Planet planet)
        {
            try
            {
                GuiPlanet.PlanetStatuses status = new GuiPlanet(planet).PlanetStatus;
                return AgeText.Clean(Gui.Localize("%PlanetStatus" + status + "Title"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AddLine(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                lines.Add(line);
            }
        }

        /// <summary>How many planet labels the lens is drawing.</summary>
        private static int Planets(IList<AgeTransform> children)
        {
            int count = 0;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                PlanetLabel_SystemManagementScanView label =
                    children[i] == null
                        ? null
                        : children[i].GetComponent<PlanetLabel_SystemManagementScanView>();
                if (label != null && label.Planet != null && AgeWidgets.Visible(label.AgeTransform))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>What a planet is putting out, as the lens rings it: each of the five raw outputs the
        /// label draws, named, and the ones it is producing none of left out - the lens hides those
        /// sectors rather than drawing a zero.</summary>
        private static string Outputs(Planet planet)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                StaticString[] properties =
                    PlanetLabel_SystemManagementScanView.PlanetFidsiProperties;
                for (int i = 0; i < properties.Length; i++)
                {
                    float value = planet.GetPropertyValue(properties[i]);
                    if (value == 0f)
                    {
                        continue;
                    }

                    message.ListItem(
                        ModStrings.Format(
                            ModStrings.ScanOutput,
                            GlobalHud.Amount(value, false, 0),
                            Gui.GetLocalizedTitle(properties[i])
                        )
                    );
                }

                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the planet lens ----

        /// <summary>
        /// The lens over a planet's own page is a data sheet and nothing else: two columns of captioned
        /// categories, each a list of "name, value, unit" lines the game composes from the planet's type
        /// and its tags. One region per category, one node per line.
        ///
        /// A line whose value is a list - what an atmosphere is made of - draws the parts in one label
        /// and the percentages in another, side by side, so the two are read together part by part.
        /// </summary>
        private void BuildPlanet(GraphBuilder builder)
        {
            PlanetScanViewWindow window = Window<PlanetScanViewWindow>();
            if (window == null || !window.Shown)
            {
                return;
            }

            try
            {
                AgeTransform left = window.PlanetStatsCategoryItemsTableLeft;
                AgeTransform right = window.PlanetStatsCategoryItemsTableRight;
                bool regions = Categories(left) + Categories(right) > 1;
                AddCategories(builder, left, "left", regions);
                AddCategories(builder, right, "right", regions);
                builder.SetRegion(null);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the planet lens threw: " + e);
            }
        }

        /// <summary>How many captioned categories a column is drawing.</summary>
        private static int Categories(AgeTransform table)
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            int count = 0;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                PlanetStatsCategoryItem category =
                    children[i] == null
                        ? null
                        : children[i].GetComponent<PlanetStatsCategoryItem>();
                if (category != null && AgeWidgets.Visible(category.AgeTransform))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AddCategories(
            GraphBuilder builder,
            AgeTransform table,
            string side,
            bool regions
        )
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                PlanetStatsCategoryItem category =
                    children[i] == null
                        ? null
                        : children[i].GetComponent<PlanetStatsCategoryItem>();
                if (category == null || !AgeWidgets.Visible(category.AgeTransform))
                {
                    continue;
                }

                string key = "scan:stats/" + side + "/" + i;
                builder.SetRegion(regions ? key : null);
                PlanetStatsCategoryItem it = category;
                builder.AddLabel(
                    ControlId.Structural(key),
                    () => AgeText.Label(it.Title)
                );
                AddStatLines(builder, category.StatLinesTable, key);
            }
        }

        private static void AddStatLines(GraphBuilder builder, AgeTransform table, string key)
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                PlanetStatLine line =
                    children[i] == null ? null : children[i].GetComponent<PlanetStatLine>();
                if (line == null || !AgeWidgets.Visible(line.AgeTransform))
                {
                    continue;
                }

                PlanetStatLine it = line;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeText.Label(it.Title)),
                        GraphNodes.ValuePart(() => StatValue(it)),
                    },
                };
                AgeWidgets.PointAt(vtable, line.AgeTransform);
                builder.AddItem(ControlId.Structural(key + "/" + i), vtable);
            }
        }

        /// <summary>A statistic's value and its unit, and for a breakdown the parts paired with their
        /// shares: the game draws the names in one column and the numbers in another, one line each, so
        /// the Nth name belongs to the Nth number.</summary>
        private static string StatValue(PlanetStatLine line)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                IList<string> values = AgeText.Lines(AgeText.Label(line.Value));
                IList<string> names = Drawn(line.DetailTitle)
                    ? AgeText.Lines(AgeText.Label(line.DetailTitle))
                    : null;
                for (int i = 0; values != null && i < values.Count; i++)
                {
                    string name = names != null && i < names.Count ? names[i] : null;
                    message.ListItem(
                        name == null
                            ? values[i]
                            : ModStrings.Format(ModStrings.ScanOutput, values[i], name)
                    );
                }

                if (Drawn(line.Unit))
                {
                    message.ListItem(AgeText.Label(line.Unit));
                }

                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Drawn(AgePrimitiveLabel label)
        {
            return label != null
                && AgeWidgets.Visible(label.AgeTransform)
                && !string.IsNullOrEmpty(AgeText.Label(label));
        }

        // ---- a lens with no model of its own ----

        /// <summary>Whatever a lens this mod has not modelled is drawing, read off its labels. Today
        /// that is the battle lens alone, which no save without a battle in it can reach; it is declared
        /// so that a player who does reach it hears the fight rather than silence.</summary>
        private void BuildUnmodelled(GraphBuilder builder)
        {
            BattleScanViewWindow window = Window<BattleScanViewWindow>();
            if (window == null || !window.Shown)
            {
                return;
            }

            AddDrawnLines(
                builder,
                AgeWidgets.Visible(window.AgeTransform)
                    ? AgeWidgets.DrawnLines(window.AgeTransform)
                    : null,
                "scan:battle"
            );
        }

        /// <summary>A read-only panel as one node per line of words it draws.</summary>
        private static void AddDrawnLines(GraphBuilder builder, IList<string> lines, string key)
        {
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                string line = lines[i];
                builder.AddLabel(ControlId.Structural(key + "/" + i), () => line);
            }
        }

        // ---- the legend ----

        /// <summary>
        /// What the lens's colours and symbols mean, as the panel down the left edge lists them: a tick
        /// that folds the whole thing away, then a group per heading.
        ///
        /// The groups are declared only while the tick is on. The game leaves them in the tree when it
        /// folds the panel and squeezes them to nothing instead of hiding them, so asking whether they
        /// are visible would declare a list the player cannot see - the tick is the game's own answer to
        /// whether the legend is showing.
        ///
        /// One panel serves every lens: the live lens points it at its own captions
        /// (<c>GuiLayeredScanViewWindow</c>), which is also why nothing here says which lens it belongs
        /// to. It is not shown at all over a lens with no legend.
        /// </summary>
        private void BuildLegend(GraphBuilder builder)
        {
            ScanOverlayWindow window = Window<ScanOverlayWindow>();
            ScanViewWindowCaptionsPanel panel = window == null ? null : window.CaptionsPanel;
            if (
                window == null
                || !window.Shown
                || panel == null
                || !AgeWidgets.Visible(panel.AgeTransform)
            )
            {
                return;
            }

            try
            {
                AgeControlToggle toggle = panel.CaptionsToggle;
                AgeTransform widget = AgeWidgets.Transform(toggle);
                if (widget == null || !AgeWidgets.Visible(widget))
                {
                    return;
                }

                AgeControlToggle it = toggle;
                NodeVtable vtable = GraphNodes.Checkbox(
                    () => LegendName(panel),
                    () => it.State,
                    () => AgeWidgets.Toggle(it),
                    () => AgeWidgets.Operable(widget),
                    AgeWidgets.Raw(widget)
                );
                AgeWidgets.PointAt(vtable, widget);
                builder.AddItem(ControlId.Referenced(toggle, "scan:legend/show"), vtable);

                if (!toggle.State)
                {
                    return;
                }

                AddCaptionGroups(builder, panel);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the legend threw: " + e);
            }
        }

        /// <summary>What the game writes on the tick, which is the word it uses for the legend.
        /// </summary>
        private static string LegendName(ScanViewWindowCaptionsPanel panel)
        {
            string drawn = AgeWidgets.TextOf(panel.ToggleBodyAgeTransform);
            return string.IsNullOrEmpty(drawn) ? ModStrings.Get(ModStrings.ScanLegend) : drawn;
        }

        /// <summary>
        /// The headings and their lines - as many of each as this lens HAS, which is not as many as the
        /// panel is holding.
        ///
        /// The panel is a pool. It reserves a widget per caption the lens declares and rebinds them, and
        /// it neither hides nor moves the ones a smaller lens does not need - so after a lens with three
        /// headings, a lens with one still has three in the tree, the last two carrying the previous
        /// lens's words, arranged past the bottom of the table where nothing draws them. Asking whether
        /// they are visible answers yes and declares a legend belonging to a lens the player left.
        ///
        /// So the COUNT comes from the lens's own data, filtered the way the panel filters it - the
        /// caption groups whose prerequisites hold (<c>ScanViewWindowCaptionsPanel.Refresh</c>, which is
        /// how content the player does not own drops out) - and the widgets are read in that order,
        /// because that is the order they were bound in.
        /// </summary>
        private void AddCaptionGroups(GraphBuilder builder, ScanViewWindowCaptionsPanel panel)
        {
            List<ScanViewCaptionGroupGuiElement> declared = Declared(panel);
            IList<AgeTransform> children =
                panel.CaptionsTable == null ? null : panel.CaptionsTable.Children;
            // One region is no region: the jump key would swallow the press and move nothing. Decided
            // before anything is tagged, because a node keeps the region it was added under.
            bool regions = declared.Count > 1;
            int groups = 0;
            for (int i = 0; children != null && i < children.Count && groups < declared.Count; i++)
            {
                ScanViewCaptionGroup group =
                    children[i] == null ? null : children[i].GetComponent<ScanViewCaptionGroup>();
                if (group == null || !AgeWidgets.Visible(group.AgeTransform))
                {
                    continue;
                }

                ScanViewCaptionItemGuiElement[] items =
                    declared[groups].ScanViewCaptionItemGuiElements;
                string key = "scan:legend/" + i;
                // A heading the game draws over several lines is somewhere to be as well as a word: it
                // is the region the lines under it belong to, and a place the jump key can land.
                builder.SetRegion(regions ? key : null);
                ScanViewCaptionGroup it = group;
                builder.AddLabel(ControlId.Structural(key), () => AgeText.Label(it.Title));
                AddCaptionItems(builder, group.ItemsTable, key, items == null ? 0 : items.Length);
                groups++;
            }

            builder.SetRegion(null);
        }

        /// <summary>The caption groups this lens declares and the player's content allows, in the order
        /// the panel binds them.</summary>
        private List<ScanViewCaptionGroupGuiElement> Declared(ScanViewWindowCaptionsPanel panel)
        {
            _legend.Clear();
            ScanViewWindowGuiElement element = panel.ScanViewGuiElement;
            ScanViewCaptionGroupGuiElement[] groups =
                element == null ? null : element.ScanViewCaptionGroupGuiElements;
            for (int i = 0; groups != null && i < groups.Length; i++)
            {
                if (Allowed(groups[i]))
                {
                    _legend.Add(groups[i]);
                }
            }

            return _legend;
        }

        private bool Allowed(ScanViewCaptionGroupGuiElement group)
        {
            Amplitude.Unity.Framework.Prerequisite[] prerequisites =
                group == null ? null : group.Prerequisites;
            for (int i = 0; prerequisites != null && i < prerequisites.Length; i++)
            {
                if (!prerequisites[i].Check(_prerequisites))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddCaptionItems(
            GraphBuilder builder,
            AgeTransform table,
            string key,
            int declared
        )
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            int items = 0;
            for (int i = 0; children != null && i < children.Count && items < declared; i++)
            {
                ScanViewCaptionItem item =
                    children[i] == null ? null : children[i].GetComponent<ScanViewCaptionItem>();
                if (item == null || !AgeWidgets.Visible(item.AgeTransform))
                {
                    continue;
                }

                ScanViewCaptionItem it = item;
                builder.AddLabel(
                    ControlId.Structural(key + "/" + i),
                    () => AgeText.Label(it.Title)
                );
                items++;
            }
        }

        // ---- which lens ----

        /// <summary>
        /// The game's own name for the lens that is up.
        ///
        /// Read off the label the top-centre panel keeps for it. That panel is hidden in the scan view,
        /// but the game goes on writing the label from the layer service whichever mode it is in
        /// (<c>TopTitlePanel.LayerService_LayerDescriptorChanged</c>), which is what makes it the game's
        /// answer rather than a copy of the game's table. The lens's own title strip is the fallback: it
        /// is a different string - what the lens's window is called rather than what the mode is - which
        /// is exactly why arriving can say one and the title node the other without repeating itself.
        /// </summary>
        private string LensName()
        {
            try
            {
                ScanViewWindowHeader header = DrawnHeader();
                if (header == null)
                {
                    // The battle lens draws no title strip of its own, and the label the panel keeps is
                    // about the map's zoom layer rather than about the fight - so there is no name here
                    // and the mod says what the mode is instead of a lens that is not the one showing.
                    return ModStrings.Get(ModStrings.ScreenScanView);
                }

                GameOverlayWindow overlay = Window<GameOverlayWindow>();
                TopTitlePanel panel = overlay == null ? null : overlay.TopTitlePanel;
                string name = panel == null ? null : AgeText.Label(panel.ScanLabel);
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }

                name = AgeText.Label(header.TitleLabel);
                return string.IsNullOrEmpty(name)
                    ? ModStrings.Get(ModStrings.ScreenScanView)
                    : name;
            }
            catch (Exception)
            {
                return ModStrings.Get(ModStrings.ScreenScanView);
            }
        }

        /// <summary>Which layer of the map the camera is on, which is what decides the lens. Compared as
        /// text because that is what changing it means: the same descriptor re-applied is not a change.
        /// </summary>
        private static string Descriptor()
        {
            try
            {
                ILayerService service = Amplitude.Unity.Framework.Services.GetService<ILayerService>();
                StaticString current = service == null ? null : service.LayerDescriptorCurrent;
                return current == null ? null : current.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The title strip of the lens that is drawn. Every lens has one and the game hides all
        /// but the live one, so this is also the answer to "which lens is up" wherever one is needed.
        /// </summary>
        private ScanViewWindowHeader DrawnHeader()
        {
            ScanViewWindowHeader[] headers = Headers();
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i] != null && AgeWidgets.Visible(headers[i].AgeTransform))
                {
                    return headers[i];
                }
            }

            return null;
        }

        private ScanViewWindowHeader[] Headers()
        {
            if (_headers != null)
            {
                return _headers;
            }

            List<ScanViewWindowHeader> found = new List<ScanViewWindowHeader>();
            AddHeader(found, Window<DiplomacyScanViewWindow>());
            AddHeader(found, Window<TradeScanViewWindow>());
            AddHeader(found, Window<EconomyScanViewWindow>());
            AddHeader(found, Window<StarSystemOverviewScanViewWindow>());
            AddHeader(found, Window<StarSystemManagementScanViewWindow>());
            AddHeader(found, Window<PlanetScanViewWindow>());
            AddHeader(found, Window<BattleScanViewWindow>());
            _headers = found.ToArray();
            return _headers;
        }

        private static void AddHeader(List<ScanViewWindowHeader> found, Component window)
        {
            try
            {
                ScanViewWindowHeader header =
                    window == null
                        ? null
                        : window.GetComponentInChildren<ScanViewWindowHeader>(true);
                if (header != null)
                {
                    found.Add(header);
                }
            }
            catch (Exception e)
            {
                Log.Warn("scan: finding a lens title threw: " + e);
            }
        }

        private ScanViewSystemOverviewFidsiLabel[] Fidsi(StarSystemOverviewScanViewWindow window)
        {
            if (_fidsi == null)
            {
                try
                {
                    _fidsi = window.GetComponentsInChildren<ScanViewSystemOverviewFidsiLabel>(true);
                }
                catch (Exception e)
                {
                    Log.Warn("scan: finding the system outputs threw: " + e);
                    _fidsi = new ScanViewSystemOverviewFidsiLabel[0];
                }
            }

            return _fidsi;
        }

        private static TWindow Window<TWindow>()
            where TWindow : Amplitude.Unity.Gui.GuiWindow
        {
            try
            {
                return Gui.GuiServiceAvailable ? Gui.GuiService.GetWindow<TWindow>(false) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GuiManager GuiState()
        {
            try
            {
                return Gui.GuiGameWindowService as GuiManager;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

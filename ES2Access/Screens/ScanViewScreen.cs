using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
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
    /// picks a layer descriptor (<c>GalaxyViewCameraController.LayerDescriptorNamesByZoomIndex</c>), the
    /// descriptor picks the lens, and the lens decides what the labels mean - so zooming here does TWO
    /// jobs: it still changes how much is drawn, as it does everywhere, and it also SELECTS the lens.
    ///
    /// Nine descriptors map onto six lens names (<c>TopTitlePanel.Load</c>), so three of the boundaries
    /// fall inside a single name - and crossing one is not a quiet event: the game re-runs its per-layer
    /// alpha and position tables over the lens window, its sections and every label
    /// (<c>GuiLayeredScanViewWindow</c>, <c>LabelMetaModifier</c>), which makes whole sub-panels and
    /// label lines appear and disappear. So the lens announces itself on arrival and again at every
    /// DESCRIPTOR change, same-name band boundaries included: a sighted player reads the lens's name
    /// across the top of the screen and watches the picture change under it, and a player who zoomed one
    /// step would otherwise be reading trade figures believing them to be diplomacy - or reading a band
    /// that has quietly dropped half of what it was drawing.
    ///
    /// Which is why the mode carries a zoom of its own (<see cref="ZoomLadder"/>) - the same control the
    /// map offers, and needed harder here: the galaxy's thirteen camera steps carry FOUR of the lenses
    /// (diplomacy 0-1, trade 2-5, economy 6-9, the system overview 10-12), and the other two are not on
    /// that ladder at all - the system-management and planet lenses come from ENTERING the system's and
    /// the planet's own view levels (<c>GalaxyViewLevel.ApplyLayer</c>), which is what the ladder's last
    /// two rungs do. A page with no zoom is a page with whichever single lens the camera happened to be
    /// on, and a system's planets missing.
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
    /// A battle's own Scan button enters this same game mode (<c>BattleScreen</c> calls
    /// <c>ToggleScanView</c>), but there it means something else entirely: an overlay of per-ship stats
    /// on the fight the player is already watching, not a lens over the map. So this screen stands down
    /// for the duration of a battle (<see cref="IsActive"/>) and the battle screen keeps the player and
    /// the narration; the toggle is one checkbox on that screen. The hacking dashboard and its banners
    /// are not modelled: the game switches all three off outright for a session without that content
    /// (<c>ScanOverlayWindow.OnGameCreated</c>).
    ///
    /// <b>Why every drawn test in this file stays.</b> A lens row stands for a GAME ENTITY - a node, a
    /// planet, a lane, a hero - and not for the label the renderer happens to be drawing it with, so
    /// almost every declaration here is <see cref="UI.Nodes.Synthetic"/> and the central gate has
    /// nothing to ask of it. The honesty about whether an entity is on the screen therefore lives in
    /// these walks: each asks the label it read the entity off whether the renderer is drawing it
    /// (<c>Painted</c> for the layers the lens FADES, <c>Visible</c> for the ones it switches), and a
    /// walk that stopped asking would announce whatever the label held for the last camera position.
    /// The rest of the tests here choose which words a row says, which is content and never existence.
    /// </summary>
    public sealed class ScanViewScreen : Screen
    {
        private static readonly object TitleStop = "scan:title";
        private static readonly object ContentStop = "scan:content";
        private static readonly object LegendStop = "scan:legend";

        private static readonly object TradeRegion = "scan:content/trade";
        private static readonly object PlanetsRegion = "scan:content/planets";
        private static readonly object HeroRegion = "scan:content/hero";
        private static readonly object RemainsRegion = "scan:content/remains";

        /// <summary>The clusters the game keeps drawing over the lens - which is only the turn controls;
        /// it hides the banners, the pinned quest and the notification strip.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

        /// <summary>The layer descriptor the player has already been told about - the DESCRIPTOR and not
        /// the lens's name, because three of the descriptor boundaries fall inside one name and the
        /// drawing changes at every one of them, so a name is no baseline. The descriptor outlives the
        /// screen - the game keeps it up to date whether or not the lens is up - so arriving baselines
        /// against what is showing rather than against nothing, and the arrival announcement is not said
        /// twice.</summary>
        private string _descriptor;

        /// <summary>Whether the lens has finished showing itself since the mode was entered - the
        /// arrival gate, held until the mode ends.</summary>
        private bool _arrived;

        /// <summary>How close the game is looking, which on this page is also WHICH LENS is being read -
        /// the same control the map itself offers (<see cref="ZoomLadder"/>).</summary>
        private readonly ZoomLadder _zoom = new ZoomLadder();

        /// <summary>The title strip each lens window draws for itself. The windows live for the whole
        /// session and instantiate their sections once, so these are found once per showing rather than
        /// per frame; instance state, so a hot reload takes them with the screen.</summary>
        private ScanViewWindowHeader[] _headers;

        /// <summary>The six labels the system lens rings a star with. Same reasoning: the panel creates
        /// them once and reuses them for whichever system the camera is nearest.</summary>
        private ScanViewSystemOverviewFidsiLabel[] _fidsi;

        /// <summary>The lines the game lays over the map for the empire's trade routes, merged the way
        /// the renderer merges them (<see cref="BuildTradeRoutes"/>). Reused rather than allocated: the
        /// whole walk is redone on every rebuild.</summary>
        private readonly TradeLanes _lanes = new TradeLanes();

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
        ///
        /// Which is asked as <c>IsInGalaxyScanView</c> rather than as the raw <c>IsInScanView</c> flag,
        /// because the game already has a word for "the scan view is what is up" and it is the narrower
        /// one: the same flag is also the battle's per-ship stats overlay, the ground battle's, and the
        /// system-discovery and planet-destruction cinematics', each of which is its own event with its
        /// own screen and its own things to say (owner ruling 2026-08-30). Borrowing the game's compound
        /// keeps this page standing down from all five without a list of its own to keep in step.
        ///
        /// And not while the battle is still leaving the screen (<see cref="BattleEnding"/>), which the
        /// game's own compound does not cover.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                GuiManager gui = GuiState();
                bool scanning =
                    gui != null
                    && gui.IsInGalaxyScanView
                    && !BattleEnding()
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

        /// <summary>
        /// Whether the battle screen is still on its way off the screen, which is the one window where
        /// the scan view outlives the thing it was an overlay ON.
        ///
        /// A player who leaves the battle's Scan toggle checked is still in the game's scan MODE when the
        /// fight ends, and the game turns it off from <c>BattleScreen.OnEndHide</c> - at the END of the
        /// screen's fade-out, whereas <c>IsInBattle</c> goes false the moment the view level stops being
        /// the encounter, several frames earlier. Between the two the galaxy's own lens is genuinely up:
        /// the game shows <c>EconomyScanViewWindow</c> and this screen arrived on it, announced a lens
        /// and a title row, and was gone again - two lines about the map thrown into the middle of a
        /// battle ending.
        ///
        /// <c>Visible</c> is the answer rather than <c>Shown</c> because the fade-out is exactly the
        /// window in question and <c>Shown</c> is already false throughout it
        /// (<c>GuiPanel.Shown => (Visible &amp;&amp; !Hiding) || Showing</c>). It cannot strand anyone:
        /// <c>GuiPanel.OnEndHide</c> clears <c>Visible</c> in the same call that ran the game's auto-off,
        /// so this gate releases on the very frame the mode ends - the backstop and the gate are one
        /// event. And it delays no ordinary entry: with no battle on the screen the window is not
        /// visible at all.
        /// </summary>
        private static bool BattleEnding()
        {
            BattleScreen battle = Window<BattleScreen>();
            return battle != null && (battle.Visible || battle.Showing);
        }

        /// <summary>Whether a lens has anything of its own on the screen yet. Every lens draws a title
        /// strip, so the drawn strip is the answer.
        ///
        /// Drawn is not enough: the game switches the lens's controls on a frame AFTER it shows them, so
        /// a screen arriving the moment the strip appears reads it "unavailable" once - once, and then
        /// never again, because a live part only re-speaks on change and by then the player has heard
        /// it.</summary>
        private bool Drawn()
        {
            ScanViewWindowHeader header = DrawnHeader();
            return header != null && AgeWidgets.Operable(header.AgeTransform);
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
        }

        public override void OnPop()
        {
            _zoom.Forget();
            _hud.Forget();
            _headers = null;
            _fidsi = null;
            _descriptor = null;
        }

        public override void OnUpdate()
        {
            _hud.Update();
            AnnounceLens();
            _zoom.Update();
        }

        /// <summary>The lens has changed under the player - they zoomed, or they walked into a system -
        /// and everything on the screen now means something else. Queued, never interrupting: it is
        /// something that happened rather than an answer to a key.
        ///
        /// Said on every descriptor change, INCLUDING one whose lens name is the name just said. Three of
        /// the nine descriptors' boundaries fall inside a single name, and the game redraws the band as
        /// heavily there as anywhere else - so suppressing the repeat let the three loudest same-name
        /// steps pass in silence, which is the one thing this watcher exists to prevent (owner ruling
        /// 2026-08-17). Hearing "Trade" twice is the price of never crossing a band unannounced.</summary>
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
                if (string.IsNullOrEmpty(lens))
                {
                    return;
                }

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
            _zoom.Build(builder, "scan:zoom");

            builder.BeginStop(ContentStop);
            BuildDiplomacy(builder);
            BuildNodes(builder);
            BuildSystemOverview(builder);
            BuildSystemManagement(builder);
            BuildPlanet(builder);
            // Last of the map's content, because it is the one thing here that does not belong to the
            // live lens: the trade lines are laid over the galaxy for the whole mode, so they follow
            // whatever the lens itself is drawing rather than interrupting it.
            BuildTradeRoutes(builder);

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
            builder.AddItem(Nodes.Drawn(ControlId.For(header, "scan:title/lens"), vtable, header));
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
        ///
        /// The label draws ONE line, and that line is a toggle: switching it points the whole lens at
        /// that empire's diplomacy instead of the player's. So the row IS the toggle wherever the game
        /// will let it be switched, and a plain readout wherever it will not - which on the player's own
        /// home system is always, because there is nobody else there to watch. It was a group with the
        /// toggle inside it, and on the only label this save draws that made a row promising a child and
        /// delivering a switched-off copy of its own words (owner-reported).
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
            ControlId id = ControlId.For(node, "scan:diplomacy/" + node.GUID);
            AgeControlToggle swap = label.SwapToggle;
            AgeTransform line = AgeWidgets.Transform(swap);
            bool switchable =
                line != null && AgeWidgets.Painted(line) && AgeWidgets.Operable(line);
            NodeVtable vtable;
            if (switchable)
            {
                AgeControlToggle toggle = swap;
                vtable = GraphNodes.Checkbox(
                    () => node.LocalizedName,
                    () => toggle.State,
                    () => AgeWidgets.Toggle(toggle),
                    // Asked live as well as at build time: the game switches the line off the moment the
                    // lens is already watching somebody else, and the row must not go on offering it.
                    () => AgeWidgets.Operable(line),
                    AgeWidgets.Raw(line),
                    null,
                    () => DiplomacyLines(it)
                );
                vtable.Announcements.Add(GalaxyCoordinates.Part(node.GalaxyPosition));
                AgeWidgets.Point(vtable, toggle);
            }
            else
            {
                vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => node.LocalizedName),
                        GalaxyCoordinates.Part(node.GalaxyPosition),
                        GraphNodes.ValuePart(() => DiplomacyLines(it)),
                    },
                    // No section: the label draws ONE line of words and the readout is already all of
                    // it, so a buffer section could only say the leader's name a second time.
                };
                AgeWidgets.PointAt(vtable, label.AgeTransform);
            }

            // Synthetic: a lens row stands for the GAME NODE, not for the map label drawing it - the
            // walk over the drawn labels above is what says this node is on the screen.
            builder.AddItem(Nodes.Synthetic(id, vtable));
        }

        /// <summary>
        /// Everything the label is saying about the system, in the order it is stacked: the leader whose
        /// home this is, how the watching empire stands with them, and whether there is a fight in orbit.
        ///
        /// Only the first of the three is words. The relation is a single tinted icon and the battle is a
        /// pair of empire emblems, so both are read from the model the game drew them from - the same
        /// answer, and the same idiom as <see cref="Status"/> on a planet card.
        /// </summary>
        private static string DiplomacyLines(ScanViewDiplomacyLabel label)
        {
            MessageBuilder message = new MessageBuilder();
            message.Fragment(AgeWidgets.PaintedText(label.MainLinesContainer));
            message.Fragment(RelationText(label));
            message.Fragment(BattleText(label));
            return message.Build();
        }

        /// <summary>How the empire the lens is watching stands with the empire whose home this is, in the
        /// game's own word for that state. Drawn as one tinted icon beside the name and nothing else, and
        /// not drawn at all where the two are the same empire - which is every label in a save where the
        /// player has met nobody, so this is verified by code rather than measured.</summary>
        private static string RelationText(ScanViewDiplomacyLabel label)
        {
            try
            {
                if (!AgeWidgets.Painted(label.RelationGroup))
                {
                    return null;
                }

                ColonizedStarSystem system = label.MainColonizedStarSystem;
                DiplomacyScanViewWindow window = label.Parent;
                Empire owner = system == null ? null : system.Empire;
                Empire watching = window == null ? null : window.WatchingEmpire;
                if (owner == null || watching == null)
                {
                    return null;
                }

                GuiEmpire theirs = Gui.GuiWrapperProviderService.GetGuiEmpire(owner);
                GuiEmpire ours = Gui.GuiWrapperProviderService.GetGuiEmpire(watching);
                DiplomaticRelationState state = ours.GetRelationStateWith(theirs);
                return state == null
                    ? null
                    : AgeText.Clean(Gui.GetLocalizedTitle(state.Name));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Who is fighting in orbit, where the label is drawing its battle line - two empire
        /// emblems and no words at all, so the names come from the same fleets the label asks about
        /// (<c>ScanViewDiplomacyLabel.CollectFightingEmpires</c>: a docked fleet still alive and in an
        /// encounter). No save without a battle in it can draw the line, so this too is verified by code.
        /// </summary>
        private static string BattleText(ScanViewDiplomacyLabel label)
        {
            try
            {
                if (!AgeWidgets.Painted(label.BattleLine))
                {
                    return null;
                }

                MessageBuilder empires = new MessageBuilder();
                IList<Fleet> docked = label.GameNode.DockedEntities;
                List<int> said = new List<int>();
                for (int i = 0; docked != null && i < docked.Count; i++)
                {
                    Fleet fleet = docked[i];
                    if (fleet == null || fleet.IsDestroyed || !fleet.IsInEncounter)
                    {
                        continue;
                    }

                    Empire empire = fleet.DisplayedEmpire;
                    if (empire == null || said.Contains(empire.Index))
                    {
                        continue;
                    }

                    said.Add(empire.Index);
                    empires.ListItemForcedComma(
                        Gui.GuiWrapperProviderService.GetGuiEmpire(empire)
                            .GetLeaderName(Gui.PlayerEmpire)
                    );
                }

                string names = empires.Build();
                return string.IsNullOrEmpty(names)
                    ? ModStrings.Get(ModStrings.ScanBattleHere)
                    : ModStrings.Format(ModStrings.ScanBattle, names);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the trade and economy lenses ----

        /// <summary>
        /// One row per label the trade and economy lenses draw, which is one per node the camera is
        /// looking at and the empire can see (<c>ScanNodeLabelsWindow.RefreshLabelsVisibilityAndPosition</c>
        /// - culling first, then the empire's own visibility).
        ///
        /// Both lenses draw the same label. What was supposed to separate them is which of the label's
        /// lines the layer fades up (<c>LabelMetaModifier</c> per line, an alpha per camera layer), and
        /// measured on the drawn label that comes to almost nothing: only the trading company's income
        /// strip is faded away on the economy band. The planet circles declare an alpha of zero for the
        /// trade band and are drawn at full anyway - the label collects its modifiers in <c>Awake</c> and
        /// the circles are instantiated later, so nothing ever animates them. Both bands therefore read
        /// the same, which is what the player sees.
        ///
        /// Every line the label carries is drawn WITHOUT words: four planet circles, a trade-quality dial,
        /// a best-system star, a traitor count as a bare number. The words are all in the tooltips, and
        /// the two that matter name a CLASS - the tooltip window assembles them from the model at draw
        /// time - so reading the label's text gave the name and nothing else, and the trade figures the
        /// player asked after were not missing but never fetched (owner-reported).
        ///
        /// So the row is the system's name with the trade dossier as its tooltip - the node is pointed at
        /// the DIAL rather than at the label, because a class-backed tooltip only has words once the game
        /// has drawn it and the game draws it for the widget it is hung on - and the planets the label
        /// rings it with are its children, each with the same dossier the game gives that circle.
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
                        || !AgeWidgets.Painted(label.ContentTable)
                    )
                    {
                        continue;
                    }

                    AddNodeRow(builder, label);
                }
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the node labels threw: " + e);
            }
        }

        private static void AddNodeRow(GraphBuilder builder, ScanNodeLabel label)
        {
            ScanNodeLabel it = label;
            GameNode node = label.GameNode;
            AgeTooltip trade = TradeTooltip(label);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => NodeName(it)),
                    // The same pair the galaxy view says after the same name: the lens recolours the
                    // map, it does not move the stars (<see cref="GalaxyCoordinates"/>).
                    GalaxyCoordinates.Part(node.GalaxyPosition),
                },
                Sections = GraphNodes.Sections(
                    NodeSection.Buffer(() => NodeLines(it)),
                    GraphNodes.TooltipSection(trade)
                ),
            };
            AgeWidgets.PointAt(vtable, label.AgeTransform, trade);

            ControlId id = ControlId.For(node, "scan:node/" + node.GUID);
            if (Circles(label) == 0)
            {
                // Synthetic: the row stands for the GAME NODE; BuildNodes above, which asks each map
                // label whether it is drawn, is the honesty about its being there.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            // Synthetic for the same reason as the leaf above.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            AddCircles(builder, label);
            builder.EndGroup();
        }

        /// <summary>The dial's own tooltip, and only while the dial is drawn: the game shows it for a
        /// system that is the player's colony and hides it for everybody else's
        /// (<c>ScanNodeLabel.RefreshTradingScore</c>). It is where the trade figures live - the trading
        /// structure, the neighbours, the system's level and population, the improvement modifier and the
        /// governor (<c>PanelFeatureTradeEfficiencyFactors</c>) - none of which the label writes down.
        /// </summary>
        private static AgeTooltip TradeTooltip(ScanNodeLabel label)
        {
            try
            {
                StarSystemManagementScanViewItemRating rating = label.TradeRatingGroup;
                return rating == null || !AgeWidgets.Painted(rating.AgeTransform)
                    ? null
                    : label.TradeEfficiencyTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Every word the label draws around its name: the icons whose tooltips carry their own
        /// text - the best-system star, a blackout, the traitor count - and the trading company, which the
        /// game hangs OUTSIDE the label's content table and whose income strip it fades away on the
        /// economy band. Read painted rather than visible for exactly that reason.</summary>
        private static IList<string> NodeLines(ScanNodeLabel label)
        {
            List<string> lines = new List<string>();
            AddPainted(lines, label.ContentTable);
            AddPainted(lines, label.TradeCompanyGroup);
            return lines;
        }

        private static void AddPainted(List<string> lines, AgeTransform widget)
        {
            if (widget == null || !AgeWidgets.Painted(widget))
            {
                return;
            }

            IList<string> drawn = AgeWidgets.PaintedLines(widget);
            for (int i = 0; drawn != null && i < drawn.Count; i++)
            {
                AddLine(lines, drawn[i]);
            }
        }

        /// <summary>How many planet circles the label is ringing the star with. The table is a pool that
        /// keeps the widgets a bigger system needed, so the count is the system's own planets and the
        /// widgets are read in that order - the order they were bound in.</summary>
        private static int Circles(ScanNodeLabel label)
        {
            int count = 0;
            IList<AgeTransform> children = CircleWidgets(label);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (Circle(label, children, i) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static IList<AgeTransform> CircleWidgets(ScanNodeLabel label)
        {
            AgeTransform table = label.PlanetCirclesTable;
            return table == null || !AgeWidgets.Painted(table) ? null : table.Children;
        }

        /// <summary>The planet the Nth circle stands for, or null where the circle is a pooled leftover -
        /// one the game faded out, or one past the end of this system's planets.</summary>
        private static Planet Circle(ScanNodeLabel label, IList<AgeTransform> children, int index)
        {
            try
            {
                AgeTransform widget = children[index];
                PlanetCircleItem item =
                    widget == null ? null : widget.GetComponent<PlanetCircleItem>();
                if (item == null || !item.IsBound || !AgeWidgets.Painted(widget))
                {
                    return null;
                }

                StarSystemNode system = label.StarSystemNode;
                IList<Planet> planets = system == null ? null : system.Planets;
                return planets == null || index >= planets.Count ? null : planets[index];
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One child per planet the label draws a circle for. The circle is a coloured dot with
        /// no words, so it is named by the planet and says the dossier the game hangs on that dot - which
        /// for a planet the empire has not surveyed is the game's own sentence about not knowing, and is
        /// then all there is to say.</summary>
        private static void AddCircles(GraphBuilder builder, ScanNodeLabel label)
        {
            IList<AgeTransform> children = CircleWidgets(label);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                Planet planet = Circle(label, children, i);
                if (planet == null)
                {
                    continue;
                }

                AgeTransform widget = children[i];
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => CircleName(widget, tooltip)),
                    },
                    Sections = GraphNodes.Sections(GraphNodes.TooltipSection(tooltip)),
                };
                AgeWidgets.PointAt(vtable, widget);
                // Synthetic: the circle stands for the PLANET; CircleWidgets above, which asks the
                // table whether it is painted, is what says the circle is drawn.
                builder.AddItem(Nodes.Synthetic(
                    ControlId.For(planet, "scan:node/planet/" + planet.GUID),
                    vtable
                ));
            }
        }

        /// <summary>What the circle stands for: the planet's name, which the game keeps on the wrapper it
        /// hangs on the dot rather than anywhere on the dot itself. For an unsurveyed planet there is no
        /// wrapper and the game puts a sentence in the tooltip instead, which becomes the name - the dot
        /// says that much and no more, and the row would otherwise be nameless.</summary>
        private static string CircleName(AgeTransform widget, AgeTooltip tooltip)
        {
            string named = AgeWidgets.ItemText(widget);
            if (!string.IsNullOrEmpty(named))
            {
                return named;
            }

            AgeTooltip readable = AgeWidgets.Readable(tooltip);
            IList<string> words =
                readable == null ? null : AgeText.Lines(AgeText.Tooltip(readable));
            return words == null || words.Count == 0 ? null : words[0];
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

        // ---- the trade routes drawn over the map ----

        /// <summary>
        /// The empire's own trade routes, as the lines the scan view draws for them.
        ///
        /// The game draws these nowhere else: <c>TradeRouteRenderer</c> refreshes only when the view
        /// switches INTO the scan view (:184-190), and it draws the player's own routes and nobody
        /// else's (:213-223). So this belongs to the lens and not to the map, and it is offered under
        /// every lens because the renderer makes no distinction between them - the lines are laid over
        /// the galaxy once and stay there for as long as the mode is up.
        ///
        /// What it draws is not one line per route. Every route's path is walked pairwise and each leg
        /// merged onto the LANE it runs over, so two routes sharing a hop are one line, coloured for
        /// the traffic on it - open, blockaded, or a third colour where the lane carries both
        /// (:229-283). A per-route listing would have been a different picture: it would say the same
        /// hop three times and never say the thing the drawing is about, which is which stretch of
        /// space the empire's trade is crossing and where it is being stopped. The merge itself, and
        /// the renderer's accumulating blockade flag, are <see cref="TradeLanes"/>.
        ///
        /// One group rather than a flat run of rows: the lines have no container on screen, but an
        /// empire with a few companies draws dozens of them, and they would otherwise sit in the middle
        /// of whatever labels the live lens is drawing. An empire with no trading company at all
        /// declares nothing, which is the fixture and most of the early game.
        ///
        /// The routes are read from the department rather than from the renderer: the renderer computes
        /// its lines once on the way in and never again, so a blockade that lands while the lens is up
        /// leaves the picture stale, and reading the model keeps the words current.
        /// </summary>
        private void BuildTradeRoutes(GraphBuilder builder)
        {
            try
            {
                Empire empire = Gui.PlayerEmpire;
                DepartmentOfCommerce commerce =
                    empire == null ? null : empire.GetAgency<DepartmentOfCommerce>();
                if (commerce == null)
                {
                    return;
                }

                _lanes.Clear();
                IList<TradingCompany> companies = commerce.TradingCompanies;
                for (int i = 0; companies != null && i < companies.Count; i++)
                {
                    TradingCompany company = companies[i];
                    if (company == null)
                    {
                        continue;
                    }

                    // The two lists the game's own EnumerableTradingRoutes yields in turn, walked by
                    // index: the enumerable allocates an iterator, and this runs every frame the lens
                    // is up.
                    AddRouteLegs(company.TradingRoutes);
                    AddRouteLegs(company.ExternalTradingRoutes);
                }

                if (_lanes.Count == 0)
                {
                    return;
                }

                // Synthetic: mod-authored - the lens draws lines on the map and no list of them.
                builder.BeginGroup(Nodes.Synthetic(
                    ControlId.Structural("scan:routes"),
                    GraphNodes.Group(() => ModStrings.Get(ModStrings.ScanTradeRoutesGroup))
                ));
                for (int i = 0; i < _lanes.Count; i++)
                {
                    AddLane(builder, _lanes[i]);
                }

                builder.EndGroup();
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the trade routes threw: " + e);
            }
        }

        /// <summary>Every leg of every route in one of a company's two lists, counted onto its lane the
        /// way the renderer counts it (:225-247) - including the blockade flag accumulating along the
        /// path, and the whole-route flag a blockade at either END sets before the first leg.</summary>
        private void AddRouteLegs(IList<TradingRoute> routes)
        {
            for (int i = 0; routes != null && i < routes.Count; i++)
            {
                TradingRoute route = routes[i];
                NodePosition[] path = route == null ? null : route.Path;
                if (path == null || path.Length < 2)
                {
                    continue;
                }

                TradingRouteBlockade blockade = route.Blockade;
                TradingRouteBlockadedNodeInfo[] nodes =
                    blockade == null ? null : blockade.NodeInfo;
                bool blockaded =
                    blockade != null
                    && (blockade.IsBeingSoftBlockadedOnHQ || blockade.IsBeingSoftBlockadedOnSubsidiary);
                for (int j = 0; j < path.Length - 1; j++)
                {
                    blockaded =
                        blockaded
                        || (
                            nodes != null
                            && j < nodes.Length
                            && nodes[j] != null
                            && nodes[j].IsBeingSoftBlockaded
                        );
                    _lanes.Add(path[j].NodeIndex, path[j + 1].NodeIndex, blockaded);
                }
            }
        }

        /// <summary>One line on the screen: the two places it runs between, and the traffic the game
        /// coloured it for. Both ends are named lazily - the walk above runs every frame, and nothing
        /// but the focused row's own words is ever needed.</summary>
        private static void AddLane(GraphBuilder builder, TradeLanes.Lane lane)
        {
            TradeLanes.Lane it = lane;
            // Synthetic: a lane is read out of the trade model; the collection above is what says
            // which lanes the lens is showing.
            builder.AddItem(Nodes.Synthetic(
                ControlId.Structural("scan:routes/" + lane.Start + "-" + lane.End),
                GraphNodes.Readout(
                    () => LaneName(it),
                    () => TradeLanes.Text(it.Open, it.Blockaded),
                    null,
                    null
                )
            ));
        }

        /// <summary>What the lane runs between, in the game's own names for the two places. A node the
        /// route crosses has been revealed to the empire by the route itself
        /// (<c>DepartmentOfCommerce.RevealNodesOnTradingRoutePath</c>), so there is no unnamed end to
        /// fall back for - but an index the positioning service cannot place is left out rather than
        /// read as a number.</summary>
        private static string LaneName(TradeLanes.Lane lane)
        {
            try
            {
                IPositioningService positioning =
                    Amplitude.Unity.Framework.Services.GetService<IPositioningService>();
                if (positioning == null)
                {
                    return null;
                }

                GameNode from = positioning.GetGameNode(new NodePosition(lane.Start));
                GameNode to = positioning.GetGameNode(new NodePosition(lane.End));
                if (from == null || to == null)
                {
                    return null;
                }

                return ModStrings.Format(
                    ModStrings.ScanTradeLane,
                    from.LocalizedName,
                    to.LocalizedName
                );
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
        /// (<see cref="ZoomLadder"/>).
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

                // Synthetic: the group stands for the SYSTEM the overview is about, and its lines are
                // read out of the panel rather than off one widget.
                builder.BeginGroup(Nodes.Synthetic(id, vtable));
                ScanViewSystemOverviewFidsiLabel[] labels = Fidsi(window);
                for (int i = 0; i < labels.Length; i++)
                {
                    ScanViewSystemOverviewFidsiLabel label = labels[i];
                    if (label == null)
                    {
                        continue;
                    }

                    builder.AddItem(
                        Nodes.Drawn(
                            ControlId.For(label, "scan:system/output/" + i),
                            GraphBuilder.Label(() => AgeText.Label(label.ValueLabel)),
                            label
                        )
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
            builder.AddItem(Nodes.Drawn(ControlId.For(toggle, "scan:system/info"), vtable, toggle));
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
                // Every section the lens draws is a region, however many of them there happen to be: a
                // lone region's jump is swallowed silently, and a section that comes and goes with the
                // count is a panel that changes shape under the player (owner ruling, 2026-08-18).
                builder.SetRegion(TradeRegion);
                AddDrawnLines(builder, trade, "scan:trade");

                AddHeroPanel(builder, window);

                builder.SetRegion(PlanetsRegion);
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
                    // Synthetic: the row stands for the PLANET; the walk over the drawn planet labels
                    // above is what says it is on the screen.
                    builder.AddItem(Nodes.Synthetic(
                        ControlId.For(planet, "scan:planet/" + planet.GUID),
                        vtable
                    ));
                }

                builder.SetRegion(null);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the system management lens threw: " + e);
            }
        }

        // ---- the governor's panel ----

        /// <summary>The panel the lens draws along the bottom for a system that has a governor - drawn
        /// only where one is assigned (<c>StarSystemManagementScanViewWindow.Bind</c>), so its absence
        /// is the answer "nobody governs this system" and there is nothing to declare.</summary>
        private static bool HeroPanelDrawn(StarSystemManagementScanViewWindow window)
        {
            StarSystemManagementScanViewHeroPanel panel =
                window == null ? null : window.HeroPanel;
            return panel != null && AgeWidgets.Painted(panel.AgeTransform);
        }

        /// <summary>
        /// The governor of the system, as the lens draws them: a heading, a portrait, a dial, and the
        /// outputs they are adding to the system.
        ///
        /// One node, like the planet cards beside it: the game draws it as one small card and there is
        /// nothing on it to work, so its parts ARE its readout and the buffer they make is the card a
        /// line at a time.
        ///
        /// Two of the three things on it have no words at all, and both are named here rather than
        /// left out. The PORTRAIT is who the governor is - the one thing a sighted player reads the
        /// panel for - and the hero's name is written nowhere on the panel, so it is taken from the
        /// panel's own bound hero. The DIAL is a pie: the game fills it with the share of this
        /// governor's system skills whose effects actually apply here
        /// (<c>StarSystemManagementScanViewHeroPanel.RefreshEfficiency</c> counts them and turns the
        /// ratio into an angle), and the angle it drew is read back as the percentage it is, rather
        /// than the skill count being re-derived - the drawn angle IS the value, and re-deriving it
        /// would be a second implementation of the game's own counting rules to keep in step.
        ///
        /// Everything else is words the panel draws: the two captions and the FIDSI bonus (or the
        /// "None" the game writes where the governor adds nothing), read in drawn order.
        /// </summary>
        private static void AddHeroPanel(
            GraphBuilder builder,
            StarSystemManagementScanViewWindow window
        )
        {
            if (!HeroPanelDrawn(window))
            {
                return;
            }

            builder.SetRegion(HeroRegion);
            StarSystemManagementScanViewHeroPanel it = window.HeroPanel;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => HeroHeading(it)),
                    GraphNodes.ValuePart(() => HeroReadout(it), false),
                },
            };
            AgeWidgets.PointAt(vtable, it.AgeTransform);
            ScrollIntoView.Anchor(vtable, it.AgeTransform);
            // Synthetic: the row is composed from the hero the panel is bound to; HeroPanelDrawn()
            // above, which asks the panel whether it is painted, is the honesty about it.
            builder.AddItem(Nodes.Synthetic(ControlId.Structural("scan:hero"), vtable));
        }

        /// <summary>The panel's own caption, which is the first thing it draws.</summary>
        private static string HeroHeading(StarSystemManagementScanViewHeroPanel panel)
        {
            IList<string> lines = AgeWidgets.PaintedLines(panel.AgeTransform);
            return lines.Count == 0 ? null : lines[0];
        }

        /// <summary>Who governs here and how well, then the rest of what the panel draws - the output
        /// half's caption and its figures - in the order they are on the screen.</summary>
        private static string HeroReadout(StarSystemManagementScanViewHeroPanel panel)
        {
            MessageBuilder message = new MessageBuilder();
            message.ListItem(HeroName(panel));
            message.ListItem(HeroEfficiency(panel));
            IList<string> lines = AgeWidgets.PaintedLines(panel.AgeTransform);
            for (int i = 1; i < lines.Count; i++)
            {
                message.ListItem(lines[i]);
            }

            return message.Build();
        }

        /// <summary>The hero the panel is bound to. Held privately - the panel draws a face and never a
        /// name - so it is read through the field itself, looked up once.</summary>
        private static readonly System.Reflection.FieldInfo HeroField =
            typeof(StarSystemManagementScanViewHeroPanel).GetField(
                "guiHero",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );

        private static string HeroName(StarSystemManagementScanViewHeroPanel panel)
        {
            try
            {
                GuiHero hero = HeroField == null ? null : HeroField.GetValue(panel) as GuiHero;
                return hero == null ? null : AgeText.Clean(hero.Title);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the governor's name threw: " + e);
                return null;
            }
        }

        /// <summary>How much of the dial the game filled in, as the percentage it draws. A full circle
        /// is everything this governor knows applying here.</summary>
        private static string HeroEfficiency(StarSystemManagementScanViewHeroPanel panel)
        {
            AgePrimitiveSector sector = panel.EfficiencySector;
            if (sector == null)
            {
                return null;
            }

            return ModStrings.Format(
                ModStrings.ScanHeroEfficiency,
                Mathf.RoundToInt(sector.MaxAngle / 3.6f)
            );
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
        /// The caption label stays drawn over an EMPTY table, and the sighted player sees exactly that -
        /// a heading with nothing under it on every planet - so the reading says the same (OWNER-RATIFIED
        /// 2026-08-13: parity with the sighted experience; drawn words are never deleted). The pairs
        /// follow only where the table has them.
        /// </summary>
        private static string SynergyText(PlanetLabel_SystemManagementScanView label)
        {
            try
            {
                MessageBuilder heading = new MessageBuilder();
                heading.Fragment(Caption(label));
                AgeTransform table = label.SynergiesTable;
                if (table == null || !AgeWidgets.Visible(table))
                {
                    return heading.Build();
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

                return said == 0 ? heading.Build() : message.Build();
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
        ///
        /// The third table is not statistics at all: what is LEFT on the planet - a wreck, a ruin, the
        /// remnants of somebody else's war - each drawn as a title and a paragraph under the right-hand
        /// column, and each one a thing the scan is there to find.
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
                AgeTransform remains = window.PlanetRemainsItemsTable;
                AddCategories(builder, left, "left");
                AddCategories(builder, right, "right");
                AddRemains(builder, remains);
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

        /// <summary>How many of the things left on the planet the lens is drawing. The table is pooled
        /// and the game hides the entries belonging to the system's own overview rather than removing
        /// them (<c>PlanetRemainsItem.Refresh</c>), so this is a count of what is PAINTED and never of
        /// what the table holds.</summary>
        private static int Remains(AgeTransform table)
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            int count = 0;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (Remain(children[i]) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static PlanetRemainsItem Remain(AgeTransform widget)
        {
            PlanetRemainsItem item =
                widget == null ? null : widget.GetComponent<PlanetRemainsItem>();
            return item != null && AgeWidgets.Painted(item.AgeTransform) ? item : null;
        }

        /// <summary>One node per thing left on the planet - its name and the paragraph the lens writes
        /// under it, which is the whole of what the game says about it.</summary>
        private static void AddRemains(GraphBuilder builder, AgeTransform table)
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                PlanetRemainsItem item = Remain(children[i]);
                if (item == null)
                {
                    continue;
                }

                builder.SetRegion(RemainsRegion);
                PlanetRemainsItem it = item;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeText.Label(it.Title)),
                        GraphNodes.ValuePart(() => AgeText.Label(it.Description), false),
                    },
                };
                AgeWidgets.PointAt(vtable, item.AgeTransform);
                ScrollIntoView.Anchor(vtable, item.AgeTransform);
                // Synthetic: Remain() above, which asks each pooled item whether it is painted, is
                // what says this remains entry is really drawn.
                builder.AddItem(Nodes.Synthetic(ControlId.Structural("scan:remains/" + i), vtable));
            }
        }

        private static void AddCategories(GraphBuilder builder, AgeTransform table, string side)
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
                builder.SetRegion(key);
                PlanetStatsCategoryItem it = category;
                // The category item is the heading: its own Title is what the node says, so the widget
                // the words were read off is both where the heading is drawn and what it exists by.
                builder.AddItem(
                    Nodes.Drawn(
                        ControlId.Structural(key),
                        GraphBuilder.Label(() => AgeText.Label(it.Title), it.AgeTransform),
                        it.AgeTransform
                    )
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
                ScrollIntoView.Anchor(vtable, line.AgeTransform);
                // Synthetic: the loop above, which asks each pooled line whether it is drawn, is what
                // says this stat line is there.
                builder.AddItem(Nodes.Synthetic(ControlId.Structural(key + "/" + i), vtable));
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

        /// <summary>A read-only panel as one node per line of words it draws.</summary>
        private static void AddDrawnLines(GraphBuilder builder, IList<string> lines, string key)
        {
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                string line = lines[i];
                // Synthetic: these are LINES scraped out of a panel, not controls - there is no one
                // widget any of them is drawn by.
                builder.AddItem(
                    Nodes.Synthetic(
                        ControlId.Structural(key + "/" + i),
                        GraphBuilder.Label(() => line)
                    )
                );
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
                // Flow control: the caption groups below are walked group by group.
                || !AgeWidgets.Visible(panel.AgeTransform)
            )
            {
                return;
            }

            try
            {
                AgeControlToggle toggle = panel.CaptionsToggle;
                AgeTransform widget = AgeWidgets.Transform(toggle);
                if (widget == null)
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
                builder.AddItem(Nodes.Drawn(ControlId.For(toggle, "scan:legend/show"), vtable, toggle));

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
                // is the region the lines under it belong to, and a place the jump key can land - one
                // heading or twenty (owner ruling, 2026-08-18: a lone region's jump is swallowed
                // silently, and a section that appears with the count changes the panel's shape).
                builder.SetRegion(key);
                ScanViewCaptionGroup it = group;
                // The group widget draws the heading (its own Title is what is read), so it is the
                // evidence as well as the rectangle. It catches nothing the count above does not
                // already: a surplus group the pool retires stays Visible at alpha 1, parked past the
                // bottom of the table - measured 2026-08-27, both live groups Visible, alpha 1,
                // painted. The lens's own count is still what keeps those out.
                builder.AddItem(
                    Nodes.Drawn(
                        ControlId.Structural(key),
                        GraphBuilder.Label(() => AgeText.Label(it.Title), it.AgeTransform),
                        it.AgeTransform
                    )
                );
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
                // Same as the heading above: the item widget draws the line and is what it exists by.
                builder.AddItem(
                    Nodes.Drawn(
                        ControlId.Structural(key + "/" + i),
                        GraphBuilder.Label(() => AgeText.Label(it.Title), it.AgeTransform),
                        it.AgeTransform
                    )
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
                    // No strip drawn at all - a frame between lenses. Naming the mode is the honest
                    // answer; the panel's label is about the map's zoom layer and may name a lens that
                    // is not the one showing.
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

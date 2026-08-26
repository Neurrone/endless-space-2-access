using System;
using System.Collections.Generic;
using Amplitude;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;
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
    /// EVERY control here takes the click the game itself puts on it, Enter for Enter, including the
    /// queue line whose click cancels a construction - the game asks its own question where it wants
    /// one, and where it does not the thing is reversible by queueing it again. Nothing is wrapped in
    /// a menu of what could be done: a card's buttons and a queue line's buy-outs are child nodes,
    /// opened with right the way the galaxy page taught. What the game only offers as a DRAG - a
    /// population unit moving between planets, a queue line moving up the queue - is CARRIED: Space
    /// picks it up and Enter on the destination puts it down, the same gesture a ship gets in the
    /// fleet panel.
    ///
    /// A planet card holds far more than a control's readout can carry - its type, its traits, its
    /// anomalies, its five outputs, and the game's own sentence about why it cannot be colonized yet -
    /// so the readout is its name and what state it is in, and all the rest is in the review buffer,
    /// which is what the review buffer is for.
    ///
    /// The side panels are the part a widget tree cannot name for itself, and the four rules the
    /// hooks below follow are all consequences of that. A panel of wordless readouts is matched by
    /// GAME COMPONENT or by the owning SidePanel's own field, never by widget shape, and Special
    /// answers a hand-built cell for it. A COUNT is spoken through ModStrings.Plural off the model,
    /// never re-read from the digits drawn on the control. Transparent is for the other half: a group
    /// the game made clickable that is really a band of readouts. NAMES come from the game -
    /// AgeWidgets.TooltipTitle, Gui.GetLocalizedTitle, or a tooltip's FIRST LINE only where that line
    /// names the thing, since a data-bearing explaining sentence is a description and not a title.
    /// And every key here includes widget.name: repeated rows otherwise collide on Duplicate control
    /// id, which empties the whole screen silently.
    /// </summary>
    public sealed class SystemManagementScreen : Screen
    {
        private static readonly object PageStop = "system:page";
        private static readonly object PlanetStop = "system:planets";
        private static readonly object ConstructiblesStop = "system:constructibles";
        private static readonly object QueueStop = "system:queue";
        private static readonly object HangarStop = "system:hangar";

        /// <summary>The prefix the shared readers key this page's ids under.</summary>
        private const string SystemKeys = "system:";

        /// <summary>The clusters the game draws over every view level. They are drawn over this page
        /// too, and until they were declared here they were on the screen and out of reach.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

        /// <summary>Reused across builds rather than allocated per frame: Build runs every tick.
        /// </summary>
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<PlanetLabel_SystemManagement> _planets =
            new List<PlanetLabel_SystemManagement>();
        private readonly List<SidePanel> _panels = new List<SidePanel>();
        private readonly List<AgeTransform> _blocks = new List<AgeTransform>();

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

        /// <summary>
        /// "Heka, System management" - the system the page is showing, then the game's own word for
        /// the page (<c>%StarSystemManagementScanViewWindowTitle</c>).
        ///
        /// The page is turned without leaving it (Alt+Left/Right, and the game's own arrows beside the
        /// name), so a name that said only "System management" left the one fact the turn is FOR -
        /// which system - unspoken. The system's name is the DRAWN one, off the rename button's label
        /// (<c>ColonyInfoSidePanel.SystemTitleLabel</c>), which the game writes for an outpost as
        /// readily as for a colony. Where the panel is not drawn at all the mod's own word for the page
        /// stands alone, as it did before.
        /// </summary>
        public override string ScreenName
        {
            get
            {
                string system = SystemTitle();
                string page = AgeText.Clean(Gui.Localize(SystemManagementTitleKey));
                if (string.IsNullOrEmpty(system) || string.IsNullOrEmpty(page) || page[0] == '%')
                {
                    return ModStrings.Get(ModStrings.ScreenStarSystem);
                }

                return ModStrings.Format(ModStrings.ScreenStarSystemNamed, system, page);
            }
        }

        /// <summary>The game's own word for this page, the one its scan-view header uses.</summary>
        private const string SystemManagementTitleKey = "%StarSystemManagementScanViewWindowTitle";

        /// <summary>The system's name as the page DRAWS it. Null where the colony panel is not up -
        /// a system the player owns nothing in.</summary>
        private string SystemTitle()
        {
            try
            {
                // Its own list, not the build's: this is asked from outside a build (the screen manager
                // announcing the page, the dev dumps) and must not disturb one in progress.
                List<SidePanel> panels = new List<SidePanel>();
                SidePanels.Drawn(panels);
                for (int i = 0; i < panels.Count; i++)
                {
                    ColonyInfoSidePanel colony = panels[i] as ColonyInfoSidePanel;
                    if (colony != null)
                    {
                        return AgeText.Label(colony.SystemTitleLabel);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
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

        /// <summary>
        /// SPACE NEVER FALLS THROUGH FROM THIS PAGE (owner ruling 2026-08-26). The game's own Space
        /// here is the scan mode (<c>InputManager</c> ToggleScanView, the shortcut this page's own scan
        /// button names: "Shortcut: Space or Mouse 3"), and a keyboard player pressing Space on a planet
        /// card or a queue line means "pick this up" - a whole different view arriving instead is not an
        /// outcome that row offered. So the key is the mod's on every node of the page: a row with
        /// something to pick up carries exactly as before, and every other press is consumed and silent
        /// (no cue - the key is pressed row after row looking for what will move). Scan mode stays one
        /// Enter away, on the button the game draws for it (<c>hud:view-title/scan</c>).
        ///
        /// Asked by the claim beside the ordinary carry claim (<c>ModEntry.CarryKeyClaimed</c>) and
        /// again by the dispatch before it swallows a press nothing carried
        /// (<c>ModEntry.SwallowedCarry</c>) - a claim is settled before the press, so the swallow is
        /// never allowed to run on a stale yes. Scoped to THIS page: the scan view over it, the galaxy
        /// and every modal this page opens keep Space as the game's.
        /// </summary>
        public static bool SwallowsCarryKey()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            return navigator != null && navigator.Screen is SystemManagementScreen;
        }

        /// <summary>
        /// Ours while the camera is in a system and nothing has replaced the page. The scan
        /// overlay is the game's own X-ray of this same view level and shows a different set of things,
        /// so it is not this screen.
        ///
        /// Asked of <see cref="GalaxyViewLevels.LevelThroughTransitions"/> and latched, the way the
        /// planet page asks it, because TURNING THE PAGE re-enters this same view level with another
        /// system: the GUI's copy of the current level and the window's own Shown flag each drop for a
        /// single frame while that happens, and the screen leaving and coming back is a full focus
        /// cycle - it announced the page twice and left the cursor wherever the old system's tree had
        /// put it. The latch is dropped by the level itself going away, so leaving the page for real
        /// still ends the screen.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                if (
                    !(
                        GalaxyViewLevels.LevelThroughTransitions
                        is GalaxyViewLevel_SystemManagement
                    )
                    || GalaxyViewLevels.Scanning
                )
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
                    // The same gate <see cref="Build"/> declares on, and for the same reason: the
                    // window is bound and shown a good while before the planet cards are drawn over
                    // it, and a page that becomes ACTIVE while it can declare nothing gets its cursor
                    // seated on the first shared HUD control instead - measured 2026-08-22 as an entry
                    // landing on the view-title's scan button. Asked only until the page has arrived,
                    // so the extra walk costs nothing once it has.
                    StarSystemScreen window = Window();
                    if (window != null && window.Shown && window.StarSystemNode != null)
                    {
                        Labels(_arriving);
                        _arrived = _arriving.Count > 0;
                        _arriving.Clear();
                    }
                }

                return _arrived;
            }
            catch (Exception)
            {
                _arrived = false;
                return false;
            }
        }

        /// <summary>Whether the page has been seen bound and drawn since the view level was entered -
        /// see <see cref="IsActive"/>.</summary>
        private bool _arrived;

        /// <summary>The arrival check's own scratch list, so asking whether the page has cards yet
        /// cannot disturb a build that is holding <c>_planets</c>.</summary>
        private readonly List<PlanetLabel_SystemManagement> _arriving =
            new List<PlanetLabel_SystemManagement>();

        /// <summary>Escape is the game's: from here it takes the camera back out to the galaxy, which
        /// is the same route the page's own close button takes.</summary>
        public override bool Back()
        {
            return false;
        }

        /// <summary>The page keys walk the empire's own colonised systems, the way the game's arrows
        /// beside the system's name do (<c>StarSystemScreen.CycleStarSystemHelper</c> :180-197) - drawn
        /// for the player's own systems and switched on once there is a second one to go to
        /// (:613-627). The buttons themselves are declared beside the name as well: this is the same
        /// pair reached without walking to it.</summary>
        public override bool PagePrev()
        {
            StarSystemScreen window = Window();
            return window != null && Page(AgeWidgets.Transform(window.PreviousSystemButton));
        }

        public override bool PageNext()
        {
            StarSystemScreen window = Window();
            return window != null && Page(AgeWidgets.Transform(window.NextSystemButton));
        }

        public override void OnPush()
        {
            _hud.Baseline();
            _showing = null;
            _turnSettle = 0;
            _turnSeats = 0;
        }

        public override void OnPop()
        {
            _hud.Forget();
            _showing = null;
            _turnSettle = 0;
            _turnSeats = 0;
        }

        public override void OnUpdate()
        {
            _hud.Update();
            Turned();
        }

        /// <summary>The system the page was showing when it was last looked at - what a page turn is
        /// measured against (<see cref="Turned"/>).</summary>
        private StarSystemNode _showing;

        /// <summary>Frames to let the page turn finish before the cursor is seated at all, and then
        /// attempts left to seat it.
        ///
        /// Both halves are needed. The page turn is not one frame: the game rebinds the window to the
        /// new system, and until it has, <see cref="Build"/> is still declaring the OLD system's
        /// planets - seating on the first frame reads a row belonging to the system the player just
        /// left (measured 2026-08-22: "Raia" announced on the way to Heka). And once it has, the page
        /// still arrives in pieces, so the seat is retried rather than attempted once.</summary>
        private int _turnSettle;

        private int _turnSeats;

        /// <summary>How long a page turn takes before anything it declares is the new system's -
        /// measured 2026-08-22 as sixteen frames from the key to a rebuilt page, with the window's own
        /// bind blinking twice inside that.</summary>
        private const int TurnSettleFrames = 30;

        /// <summary>And how long the seat is then worth trying for, since the planet cards bind over
        /// several frames after that.</summary>
        private const int TurnSeatFrames = 60;

        /// <summary>
        /// The page has been turned to another system: say which one, once, and put the cursor where a
        /// fresh arrival puts it.
        ///
        /// The screen itself never leaves - the view level is re-entered with a new node and the mod's
        /// own gates now ride that out (<see cref="IsActive"/>) - so nothing else would speak, and the
        /// cursor would sit on whichever node of the OLD system's tree the graph state remembered (a
        /// figure in the colony panel, measured 2026-08-22). Both halves are exactly what the screen
        /// manager does for a page the player arrives on: the name queued, and the landing on
        /// <see cref="InitialFocusStop"/> announced when it lands.
        ///
        /// The first system seen is adopted silently: that is the arrival, and the screen manager has
        /// already announced it.
        /// </summary>
        private void Turned()
        {
            StarSystemScreen window = Window();
            StarSystemNode node = window == null ? null : window.StarSystemNode;
            if (node != null && !ReferenceEquals(node, _showing))
            {
                bool arriving = _showing == null;
                _showing = node;
                if (!arriving)
                {
                    Voice.Say(ScreenName, false);
                    _turnSettle = TurnSettleFrames;
                    _turnSeats = TurnSeatFrames;
                }
            }

            if (_turnSettle > 0)
            {
                _turnSettle--;
                return;
            }

            if (_turnSeats > 0)
            {
                _turnSeats--;
                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null && navigator.FocusStop(PlanetStop))
                {
                    _turnSeats = 0;
                }
            }
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

            // Down the screen: the empire's banners in the top-left corner and the name of the view in
            // the centre, then the page itself,
            // then the right-hand edge - a collapsed tutorial's bar and the notification icons under
            // it - and the turn controls in the bottom corner. Same order as every other view level,
            // because the game draws them in the same places whichever one is up.
            _hud.Top(builder);

            BuildPage(builder, window);

            builder.BeginStop(PlanetStop);
            builder.PushContext(ModStrings.Get(ModStrings.SystemPlanetsPanel));
            BuildPlanets(builder, window);
            builder.PopContext();

            BuildSidePanels(builder);

            // The three panels along the bottom are the same prefabs the Empire summary slides out
            // under its systems table, and they are read by the shared reader (SystemPanels); what is
            // this page's own is that all three are drawn at once, each as a stop of its own.
            StarSystemConstructiblePanel constructibles =
                window.GetComponentInChildren<StarSystemConstructiblePanel>(true);
            StarSystemQueuePanel queue = window.GetComponentInChildren<StarSystemQueuePanel>(true);
            StarSystemHangarPanel hangar = window.GetComponentInChildren<StarSystemHangarPanel>(true);
            BuildBottomPanel(
                builder,
                ConstructiblesStop,
                ModStrings.SystemConstructiblesPanel,
                constructibles == null ? null : constructibles.AgeTransform,
                () => SystemPanels.Constructibles(builder, constructibles, SystemKeys)
            );
            BuildBottomPanel(
                builder,
                QueueStop,
                ModStrings.SystemQueuePanel,
                queue == null ? null : queue.AgeTransform,
                () => SystemPanels.Queue(builder, queue, SystemKeys)
            );
            BuildBottomPanel(
                builder,
                HangarStop,
                ModStrings.SystemHangarPanel,
                hangar == null ? null : hangar.AgeTransform,
                () => SystemPanels.Hangar(builder, hangar, SystemKeys)
            );

            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.TurnLog(builder);
            _hud.Turn(builder);
        }

        /// <summary>
        /// What the game hangs on the page's own WINDOW rather than on any of its panels, drawn above
        /// the cards: the toggle between the view the player's sleepers have of a foreign colony and the
        /// view its owner has (<c>StarSystemScreen.SwitchTraitorsModeButton</c> :629, drawn only while
        /// the player has traitors in this system and there is a second colony to look at). Being drawn
        /// is what declares it - no empire without sleepers here ever meets it - and the game names it
        /// nowhere but in the sentence its own tooltip explains it with.
        ///
        /// It is a stop of its own before the planets rather than a card's child, because pressing it
        /// re-binds the WHOLE page: what every panel below is about changes.
        /// </summary>
        private void BuildPage(GraphBuilder builder, StarSystemScreen window)
        {
            _cells.Clear();
            Cells.AddControl(
                _cells,
                AgeWidgets.Transform(window.SwitchTraitorsModeButton),
                "system:traitors-mode"
            );
            if (_cells.Count == 0)
            {
                return;
            }

            builder.BeginStop(PageStop);
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>One of the three panels along the bottom, under the mod's own word for it - and
        /// under the word the panel DRAWS across its top, where the game hung the sentence saying what
        /// the panel is for on it. That caption is a row and not the panel's name: the name is what
        /// the stop is already called, and the sentence is what a name cannot carry
        /// (<see cref="Captions"/>).</summary>
        private static void BuildBottomPanel(
            GraphBuilder builder,
            object stop,
            string nameKey,
            AgeTransform panel,
            Action build
        )
        {
            builder.BeginStop(stop);
            builder.PushContext(ModStrings.Get(nameKey));
            Captions.Row(
                builder,
                AgeWidgets.ChildNamed(panel, "Header", 2),
                stop + "/header"
            );
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
                // Picking a population unit up is only offered where there is somewhere to put it
                // down, which on this page means a SECOND colony of the player's in the system - the
                // system with one colony draws the markers and offers no carry, exactly as the hangar
                // page draws ship tiles and offers none.
                bool canCarry = Settlements(window) > 1;
                for (int i = 0; i < _planets.Count; i++)
                {
                    AddPlanet(builder, _planets[i], canCarry);
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the planet cards threw: " + e);
            }
        }

        /// <summary>
        /// One planet card.
        ///
        /// ENTER IS THE CARD'S OWN CLICK, which on this page is the planet's own page. The card is an
        /// AGE overlay and carries no click of its own - the click the game answers is the one on the
        /// PLANET behind it (<c>GalaxyPlanetCursorTarget.OnCursorClick</c> :30-53, which asks for
        /// <c>GalaxyViewLevel_PlanetOverview</c> while this view level is up), and that is what
        /// <see cref="GalaxyViewLevels.OpenPlanet"/> posts. Nothing is spoken for it: the page changes
        /// and the page announces itself.
        ///
        /// Everything else the card offers is where the card draws it. The rename button beside the
        /// title and the colonize button under it are child nodes; the population the card draws as a
        /// ring of markers is a row per SLOT of that ring, in up to three bands
        /// (<see cref="AddPopulationSlots"/>), and a unit is moved to another planet by CARRYING it
        /// (Space to pick up, Enter on the other card to put down) rather than by a menu entry per unit
        /// and destination, which is the same gesture a ship gets in the fleet panel and the same drag
        /// the mouse has here.
        /// </summary>
        private void AddPlanet(
            GraphBuilder builder,
            PlanetLabel_SystemManagement label,
            bool canCarry
        )
        {
            Planet planet = label.Planet;
            if (planet == null)
            {
                return;
            }

            PlanetLabel_SystemManagement it = label;
            // The card's own status button carries the game's sentence about the state - "too hostile
            // to be colonized", and which technology would change that. It is DECLARED as the card's
            // tooltip, so the card says it has one and the buffer holds its words; what it is not is
            // announced, which is this screen's one deliberate override of the short/long rule (the
            // sentence runs to three lines and would be read out on every pass down the planets).
            AgeTransform status = StatusWidget(label);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.PlanetTitle)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlanetStatus)),
                    // An outpost's card ends in the game's own sentence about how it is getting on
                    // ("Colony in 24 Turn"), which is drawn on the card and so is spoken, not buffered.
                    GraphNodes.ValuePart(() => Drawn(it.OutpostBottomCaption)),
                },
                // The status tooltip first, then the rest of the card, which is the order the card
                // draws them in.
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(AgeWidgets.Raw(status), TooltipMode.Indicate),
                    NodeSection.Buffer(() => PlanetDetails(it))
                ),
                OnActivate = () => GalaxyViewLevels.OpenPlanet(it.Planet),
            };

            // A colony of the player's is where a carried population unit can be put down - the same
            // set of cards the game's own drag offers as targets.
            if (Settled(label) != null)
            {
                vtable.DropKind = PopulationKind;
                vtable.OnDrop = item => DropPopulation(it, item);
            }

            AgeWidgets.PointAt(vtable, status ?? label.AgeTransform);

            string key = "system:planet/" + planet.GUID;
            ControlId id = ControlId.Referenced(planet, key);
            List<CardActions.CardAction> rename = new List<CardActions.CardAction>(1);
            CardActions.AddNamedByMod(rename, label.PlanetRenameButton, ModStrings.SystemRenamePlanet);
            List<CardActions.CardAction> buttons = PlanetButtons(label);
            List<CardActions.CardAction> outpost = OutpostActions(label);
            List<Population> units = new List<Population>(4);
            List<PopulationSlots.Slot> slots = PlanetSlots(label, units);
            List<TooltipChildren.Dossier> dossiers = PlanetDossiers(label);
            if (
                rename.Count == 0
                && buttons.Count == 0
                && outpost.Count == 0
                && slots.Count == 0
                && dossiers.Count == 0
            )
            {
                builder.AddItem(id, vtable);
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            builder.BeginGroup(id, vtable);
            if (builder.IsExpanded(id))
            {
                // Down the card, in the order it is drawn: the rename button beside the title, the
                // population ring in the middle, the action buttons along the bottom - and then, as a
                // region of their own, the dossiers the card draws no words for at all.
                object outer = TooltipChildren.Actions(builder, key);
                CardActions.Emit(builder, key + "/name", rename);
                AddPopulationSlots(builder, key, label, units, slots, canCarry);
                CardActions.Emit(builder, key, buttons);
                CardActions.Emit(builder, key + "/outpost", outpost);
                TooltipChildren.Emit(builder, key, dossiers, outer);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// The dossiers a planet card carries beyond the sentence on its status button: the planet's
        /// own, and one per FIDSI figure in the strip of pips down its side.
        ///
        /// The card draws each pip as a picture and a bare number, and keeps everything about what
        /// that number MEANS - what it is called, what it is made of, what would change it - in a
        /// dossier behind the pip. The card's buffer already carries the captioned figures
        /// (<see cref="PlanetDetails"/>); this is the page behind each one.
        ///
        /// The card keeps TWO strips and swaps them (<c>FidsiScoreTable</c> for a planet nobody has
        /// settled, the <c>FidsiEnumerator</c>'s duplets once it is a colony) with the other one left
        /// bound to whatever it last showed, so the strip is taken from whichever is DRAWN and the
        /// resolver drops the pips of the hidden one.
        ///
        /// The improvement box is the third: the card draws which improvement this world has - or that
        /// one is being built, or that there is none - and the game keeps what that MEANS on a tooltip
        /// field of its own rather than on the box (<c>RefreshPlanetImprovement</c> :1335-1394), so
        /// nothing hanging off the card could ever have found it. It is either a sentence the game
        /// wrote or the improvement's own dossier, depending on which of the three states the world is
        /// in, and both read here.
        /// </summary>
        private static List<TooltipChildren.Dossier> PlanetDossiers(
            PlanetLabel_SystemManagement label
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(6);
            try
            {
                TooltipChildren.Add(found, label.PlanetTooltipFrame);
                TooltipChildren.AddInside(found, label.FidsiScoreTable);
                TooltipChildren.AddInside(
                    found,
                    label.FidsiEnumerator == null ? null : label.FidsiEnumerator.AgeTransform
                );
                AddDepositDossiers(found, label);
                if (AgeWidgets.Visible(label.ImprovementStatus))
                {
                    TooltipChildren.Add(
                        found,
                        label.ImprovementTooltip,
                        label.ImprovementStatus
                    );
                    TooltipChildren.AddPlain(
                        found,
                        label.ImprovementTooltip,
                        label.ImprovementStatus
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a planet card's dossiers threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// The page behind each deposit the world is sitting on - what the resource is for, who can
        /// work it and what is stopping them.
        ///
        /// The item draws a picture and a figure, and everything else about the resource is a dossier
        /// the renderer assembles from the wrapper it binds (<c>ResourceDepositItem.Refresh</c> :36-42
        /// sets the class, the target and the failure sentences), so the card's line
        /// (<see cref="AddDeposits"/>) says which resource and how much of it, and this is where the
        /// rest of it is read. The pooled table's retired items keep the PREVIOUS planet's wrapper on
        /// their tooltip, so each item is asked the engine's own drawing test first - the same gate
        /// the line reader uses.
        /// </summary>
        private static void AddDepositDossiers(
            List<TooltipChildren.Dossier> found,
            PlanetLabel_SystemManagement label
        )
        {
            AgeTransform group = label.ResourceDepositsGroup;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            IList<AgeTransform> children = group.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (!AgeWidgets.Paints(child))
                {
                    continue;
                }

                ResourceDepositItem item = child.GetComponent<ResourceDepositItem>();
                if (item != null)
                {
                    TooltipChildren.Add(found, item.Tooltip, child);
                }
            }
        }

        /// <summary>Which of the card's own buttons the game is drawing. Rename is emitted separately
        /// because the card draws it at the top, beside the title, and these along the bottom.</summary>
        private static List<CardActions.CardAction> PlanetButtons(PlanetLabel_SystemManagement label)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(1);
            try
            {
                CardActions.AddNamedByMod(found, label.ColonizeButton, ModStrings.SystemColonize);
                // The three the card draws along its bottom for a world that is already yours: pick a
                // specialization improvement, reduce an anomaly, terraform. The sibling EMPIRE screen
                // declares exactly these off the same prefab family (<c>EmpireScreen.CardButtons</c>)
                // and this page declared none of them, so choosing a planet's specialization was
                // unreachable from the system page by keyboard at all. Each names itself in the
                // sentence its own tooltip explains it with.
                CardActions.AddNamedByTooltip(found, label.BuildInfrastructureButton);
                CardActions.AddNamedByTooltip(found, label.ReduceAnomalyButton);
                CardActions.AddNamedByTooltip(found, label.TerraformButton);
                AddAnomalyHints(found, label);
                AddCuriosities(found, label);
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a planet card's buttons threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// The anomalies on the card, as the CONTROLS the game made them: each row's own click jumps to
        /// the technology that would let the anomaly be reduced (<c>PlanetAnomalyItem.OnHintCb</c>),
        /// which the mouse has and no node stood on. The click is wired on the ROW, not on the little
        /// hint button beside it - that one only carries the hint's state - so the row is what is
        /// declared and the button is what decides whether it would do anything.
        ///
        /// Kept declared while the row is drawn and OFFERED only while the hint is live, the same
        /// treatment every other blocked control on these cards gets: the game only fills the hint in
        /// for a world of yours whose reduction is blocked, and a row that answers "unavailable" is the
        /// truthful reading of a click that would do nothing. The anomaly's own dossier - the paragraph
        /// and the reduction prerequisites - rides along as the node's tooltip; the card's buffer keeps
        /// naming the anomalies as it always did.
        ///
        /// Painted is the gate: the table pools its items.
        /// </summary>
        private static void AddAnomalyHints(
            List<CardActions.CardAction> found,
            PlanetLabel_SystemManagement label
        )
        {
            AgeTransform table = label.PlanetAnomaliesTable;
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform row = items[i];
                PlanetAnomalyItem item = row.GetComponent<PlanetAnomalyItem>();
                if (item == null || item.HintButton == null || !AgeWidgets.Painted(row))
                {
                    continue;
                }

                PlanetAnomalyItem it = item;
                AgeTransform hint = item.HintButton.AgeTransform;
                found.Add(
                    new CardActions.CardAction
                    {
                        Widget = row,
                        Label = () => AgeWidgets.TooltipTitle(it.Tooltip),
                        Tooltip = it.Tooltip,
                        Offered = () => AgeWidgets.Hinted(hint),
                    }
                );
            }
        }

        /// <summary>
        /// The curiosities the card is drawing, each one the same wired button the map's own card
        /// carries: a wordless icon kept CLICKABLE while refused, with the reason in its own tooltip
        /// (<c>PlanetCuriosityItem.Refresh</c>). Named off the wrapper the game hangs on that tooltip,
        /// which is the only place the thing in orbit has a name.
        ///
        /// This card mixes three kinds of item into one table, so the curiosity items are picked out by
        /// their own component rather than by position; the rest of the table stays a line of the card's
        /// (<see cref="PlanetDetails"/>).
        ///
        /// Painted is the gate, as on the anomalies table above: this table is pooled too
        /// (<c>PlanetLabel_SystemManagement.RefreshPlanetCuriosities</c> :1297 <c>ReserveChildren</c>),
        /// so a card showing fewer curiosities than the one read before it keeps the surplus items
        /// <c>Visible</c> at alpha 0 - and a retired item has had its tooltip unbound, so it has no
        /// name either. Measured on Heka II, which offered one drawn curiosity and one leftover from
        /// another planet declared as a nameless "button, unavailable".
        /// </summary>
        private static void AddCuriosities(
            List<CardActions.CardAction> found,
            PlanetLabel_SystemManagement label
        )
        {
            AgeTransform table = label.PlanetCuriositiesTable;
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                if (item != null && AgeWidgets.Painted(item) && SkipCuriosities(item))
                {
                    CardActions.AddRefusable(found, item, CardActions.TitleOf(item));
                }
            }
        }

        /// <summary>
        /// What an OUTPOST's card offers, in the order the card draws it: the strip of outpost actions
        /// along the top of the outpost group, then the decolonize tick under them.
        ///
        /// The game draws an action as a tick with a price on it and its name NOWHERE - the name, what
        /// it does, how long it takes and what it costs all live in the wrapper on its own tooltip
        /// (<c>GuiOutpostAction</c>) - so that wrapper's title is what the node is called and the
        /// tooltip is the dossier behind it. An action the faction cannot have at all the game hides
        /// outright (the <c>Discard</c> failure flag, <c>OutpostActionItem.Bind</c>), so those are not
        /// here; one it is merely refusing today stays drawn and switched off, and is declared refusing
        /// with the game's own reason. Enter is the tick's own click, which starts the action, or -
        /// only on the turn it started, which is the whole of the game's cancel window - cancels it
        /// with a refund (<c>PlanetLabel_SystemManagement.OnOutpostActionSwitchCb</c> :1566).
        ///
        /// Decolonize is the same shape: Enter is its click, and the game raises its own confirmation
        /// box, which speaks through <c>MessageBoxScreen</c> like every other one. Ticked, it is
        /// already scheduled and the click unschedules it with no confirmation at all (:1587).
        ///
        /// The strip is POOLED (<c>RefreshOutpostActions</c> :988 <c>ReserveChildren</c>), so each tick
        /// is asked the drawing test rather than the visibility flag a retired one keeps: an outpost
        /// offering fewer actions than the one read before it would otherwise declare the surplus
        /// ticks, still wearing the other outpost's name.
        /// </summary>
        private static List<CardActions.CardAction> OutpostActions(
            PlanetLabel_SystemManagement label
        )
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(4);
            try
            {
                if (label.OutpostGroup == null || !AgeWidgets.Visible(label.OutpostGroup))
                {
                    return found;
                }

                AgeTransform table = label.OutpostActionsTable;
                IList<AgeTransform> items = table == null ? null : table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    OutpostActionItem item =
                        items[i] == null || !AgeWidgets.Painted(items[i])
                            ? null
                            : items[i].GetComponent<OutpostActionItem>();
                    if (item == null)
                    {
                        continue;
                    }

                    OutpostActionItem it = item;
                    CardActions.AddToggle(
                        found,
                        item.Toggle,
                        CardActions.TitleOf(item.Toggle),
                        () => OutpostActionValue(it)
                    );
                }

                CardActions.AddToggle(
                    found,
                    label.DecolonizeToggle,
                    CardActions.GameText("%PlanetDecolonizeTitle"),
                    null
                );
            }
            catch (Exception e)
            {
                Log.Warn("system: reading an outpost card's actions threw: " + e);
            }

            return found;
        }

        /// <summary>What the game writes on an outpost action: what it would cost while it is only on
        /// offer, and how many turns it has left once it is running.</summary>
        private static string OutpostActionValue(OutpostActionItem item)
        {
            try
            {
                return item.DurationGroup != null && item.DurationGroup.Visible
                    ? Drawn(item.DurationLabel)
                    : Drawn(item.CostLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Everything the card shows that the readout cannot carry: what kind of world it is, what
        /// living there is like, what has been found on it, and its five outputs. In the order the card
        /// draws them, top to bottom - under the status tooltip, which the card declares as a tooltip
        /// section of its own rather than folding it in here (a tooltip read as "details" is a tooltip
        /// nothing ever indicates).
        /// </summary>
        private static IList<string> PlanetDetails(PlanetLabel_SystemManagement label)
        {
            List<string> lines = new List<string>();
            try
            {
                AddWidgetLines(lines, label.PlanetTypeGroup);
                AddWidgetLines(lines, label.PlanetSizeGroup);
                AddWidgetLines(lines, label.PlanetGameplayTypeTable);
                AddWidgetLines(lines, label.PlanetAnomaliesTable);
                // The card puts three kinds of thing in this one table - what sort of world it is, what
                // was found on it, and the curiosities still to be looked into. The curiosities are
                // buttons and are child nodes of their own, so only the rest of the table is read here.
                AddWidgetLines(lines, label.PlanetCuriositiesTable, SkipCuriosities);
                AddDeposits(lines, label);
                AddDepletion(lines, label);
                AddWidgetLines(lines, label.ImprovementStatus);
                AddFidsi(lines, label);
                AddOutpost(lines, label);
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a planet's details threw: " + e);
            }

            return lines;
        }

        /// <summary>
        /// What the world is sitting on: one line per deposit the card is drawing, each the resource's
        /// own name bound to the figure beside it.
        ///
        /// The generic reader cannot do this one. A deposit item draws an icon and a bare amount and
        /// writes the resource's NAME nowhere on itself (<c>ResourceDepositItem.Refresh</c> :28-42 fills
        /// <c>AmountLabel</c> and leaves the prefab's <c>TitleLabel</c> to the prefabs that have one) -
        /// it keeps the name on the wrapper it hangs on its own tooltip, so a line read off the drawn
        /// text alone was the number by itself, "3" and "2" with nothing saying of what. Both of the
        /// card's shapes fill this same table (<c>PlanetLabel_SystemManagement.Refresh</c> :536-547),
        /// so a settled world and an unsettled one both read here.
        ///
        /// The table is POOLED - <c>ReserveChildren</c> + <c>RefreshChildrenIList</c> retire a surplus
        /// item by fading it to alpha 0 with <c>Visible</c> still true - and a retired item keeps the
        /// PREVIOUS planet's resource on its tooltip, so the walk asks the engine's own drawing test of
        /// each child (<see cref="AgeWidgets.Paints"/>, the same rule as
        /// <c>SidePanels.Collect</c>) rather than the visibility flag.
        /// </summary>
        private static void AddDeposits(List<string> lines, PlanetLabel_SystemManagement label)
        {
            AgeTransform group = label.ResourceDepositsGroup;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            IList<AgeTransform> children = group.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (!AgeWidgets.Paints(child))
                {
                    continue;
                }

                ResourceDepositItem item = child.GetComponent<ResourceDepositItem>();
                if (item == null)
                {
                    AddLine(lines, AgeWidgets.ItemText(child));
                    continue;
                }

                string name = Drawn(item.TitleLabel);
                if (string.IsNullOrEmpty(name))
                {
                    name = AgeWidgets.TooltipTitle(item.Tooltip);
                }

                string amount = Drawn(item.AmountLabel);
                AddLine(
                    lines,
                    string.IsNullOrEmpty(name)
                        ? amount
                        : ModStrings.Format(ModStrings.CaptionedColon, name, amount)
                );
            }
        }

        /// <summary>
        /// How worn out the world is - a mining probe's damage, or a Craver colony eating the planet it
        /// lives on. The game draws this only while the planet is being depleted or already is
        /// (<c>PlanetLabel_SystemManagement.RefreshPlanetDepletion</c> :1321-1332), so being drawn is
        /// the gate, and it writes the state and how many turns are left on the item itself with the
        /// sentence behind them in its own tooltip.
        ///
        /// A FULLY depleted planet swaps that tooltip for an assembled dossier, whose words do not
        /// exist until the tooltip is drawn - so the state line still reads and the paragraph arrives
        /// when the player looks at it, rather than being invented here.
        /// </summary>
        private static void AddDepletion(List<string> lines, PlanetLabel_SystemManagement label)
        {
            PlanetDepletionStatusItem item = label.PlanetDepletionStatusItem;
            if (item == null || !AgeWidgets.Visible(item.AgeTransform))
            {
                return;
            }

            AddLine(lines, Drawn(item.Title));
            Add(lines, AgeWidgets.TooltipLines(item.Tooltip));
        }

        /// <summary>
        /// The lines an OUTPOST's card carries that nothing else on it says: who owns it (a plain
        /// label the game only draws while the system is an outpost), when the next population unit
        /// arrives and which kind it will be - both of which the card draws as a bare number and a
        /// symbol, so the two sentences the game explains them with are what carries them - and last
        /// the help behind the progress caption, whose own words are already spoken as the card's
        /// state.
        /// </summary>
        private static void AddOutpost(List<string> lines, PlanetLabel_SystemManagement label)
        {
            if (label.OutpostGroup == null || !AgeWidgets.Visible(label.OutpostGroup))
            {
                return;
            }

            AddLine(lines, Drawn(label.OutpostOwnerLabel));
            Add(lines, AgeWidgets.TooltipLines(Tooltip(label.OutpostOwnerLabel)));

            GrowthGaugeItem growth = label.GrowthLine;
            if (growth != null)
            {
                AddLine(lines, Drawn(growth.TurnsBeforeNextPop));
                Add(lines, AgeWidgets.TooltipLines(Tooltip(growth.TurnsBeforeNextPop)));
                Add(lines, AgeWidgets.TooltipLines(Tooltip(growth.NextPopulationIcon)));
            }

            Add(lines, AgeWidgets.TooltipLines(Tooltip(label.OutpostBottomCaption)));
        }

        /// <summary>The tooltip a drawn primitive carries, whatever kind of primitive it is.</summary>
        private static AgeTooltip Tooltip(AgePrimitive primitive)
        {
            try
            {
                return primitive == null ? null : AgeWidgets.Raw(primitive.AgeTransform);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The planet's five outputs, named by the game's own property titles, in the two
        /// shapes the card draws them in. A COLONY's are written as numbers and read as numbers, off
        /// the colony's own simulation object. A world nobody has settled gets no numbers at all: the
        /// card hides that row and draws a table of rating pips instead
        /// (<c>PlanetLabel_SystemManagement.BindPlanet</c> :358-368), which the map's card does too,
        /// so the lines of both shapes are composed for both cards in <see cref="PlanetOutputs"/>.
        /// Which shape is drawn is the game's own test - whether the planet is a colony - so it is
        /// the test here.</summary>
        private static void AddFidsi(List<string> lines, PlanetLabel_SystemManagement label)
        {
            FidsiEnumerator fidsi = label.FidsiEnumerator;
            if (fidsi == null || fidsi.FidsiProperties == null)
            {
                return;
            }

            ColonizedPlanet colony = label.ColonizedPlanet;
            if (colony == null)
            {
                IList<string> ratings = PlanetOutputs.Ratings(
                    label.Planet,
                    fidsi,
                    label.FidsiParametersGuiElement
                );
                for (int i = 0; i < ratings.Count; i++)
                {
                    AddLine(lines, ratings[i]);
                }

                return;
            }

            Amplitude.Unity.Simulation.SimulationObject simulation = colony.SimulationObject;
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

        /// <summary>What the carried thing IS here, so that a population unit cannot be dropped into a
        /// fleet and a ship cannot be dropped onto a planet.</summary>
        public const string PopulationKind = "population";

        /// <summary>The colony this card is for WHOEVER owns it - the same object the card binds its
        /// population ring to (<c>PlanetLabel.BindPlanet</c> takes it straight off
        /// <c>Planet.ColonizedPlanet</c>, so an enemy outpost's card holds the enemy's colony), and so
        /// the one to read the ring's contents from. Null on a world nobody has settled.</summary>
        private static ColonizedPlanet Colony(PlanetLabel_SystemManagement label)
        {
            try
            {
                return label == null ? null : label.ColonizedPlanet;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The colony this card is for when it is the PLAYER's, or null - the card of an
        /// unsettled world, or of somebody else's colony, is neither a source nor a target.</summary>
        private static ColonizedPlanet Settled(PlanetLabel_SystemManagement label)
        {
            try
            {
                ColonizedPlanet colony = label == null ? null : label.ColonizedPlanet;
                return colony != null && colony.Empire == Gui.PlayerEmpire ? colony : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How many colonies of the player's this system has - which is how many cards a
        /// carried unit could be put down on.</summary>
        private static int Settlements(StarSystemScreen window)
        {
            try
            {
                ColonizedStarSystem system = window == null ? null : window.ColonizedStarSystem;
                if (system == null || system.Empire != Gui.PlayerEmpire)
                {
                    return 0;
                }

                return system.PlanetsColonized.Count;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// The SLOTS of a colony's population ring, and the unit filling each - the card's middle,
        /// read as the ring is drawn rather than as the model is stored.
        ///
        /// The game draws one marker per slot and says everything about a slot in its COLOUR: an
        /// ordinary place to live, a place under the overpopulation arc, a place the world's current
        /// maximum has locked. <see cref="PopulationSlots"/> is that arithmetic; this supplies its
        /// terms from the colony (<paramref name="units"/> comes back holding one entry per population
        /// unit, in <c>PopulationsByAffinity</c> order, which is the order the game's own enumerator
        /// lays the markers out in) and asks the RING whether there is one to read at all.
        ///
        /// Contents from the model, existence from the drawing. The detailed ring the markers' own
        /// tooltips hang on is only shown under a mouse (<c>PlanetLabel_SystemManagement</c> swaps it
        /// in on hover), so reading a slot's affinity off a marker would answer nothing while the
        /// player is on the keyboard - and equally, a card the game is drawing no ring on has no slots
        /// to offer, whatever the model says the planet could hold.
        ///
        /// A world NOBODY has settled gets a ring too - measured 2026-08-26, one marker per point of
        /// its maximum population on every card in the system - because the enumerator falls back to
        /// the PLANET's own figures when there is no colony
        /// (<c>PlanetPopulationEnumerator.GetPopulationOwnerData</c> :71-75) and only the ring's
        /// ENABLE flag is gated on <c>IsAvailable</c>. Those markers are all empty, none is locked and
        /// no arc is drawn over them (<see cref="PopulationSlots.BuildUnsettled"/>), so how much room
        /// a world has - the thing a colonization is decided on - is read the same way on both kinds
        /// of card.
        ///
        /// Somebody ELSE's colony - an enemy outpost sitting on a free world of a system the player
        /// owns - reads the SAME way (owner ruling 2026-08-27, replacing the deliberate skip this
        /// carried until then). The game draws that card's ring from the foreign colony and draws
        /// THEIR units in it: the label binds whatever colony the planet holds
        /// (<c>PlanetLabel.BindPlanet</c>: <c>ColonizedPlanet = Planet.ColonizedPlanet</c>), hands it
        /// to the ring unfiltered (<c>PlanetLabel_SystemManagement.Bind</c> :373) and shows that ring
        /// with no ownership test at all (<c>OnBeginShow</c> :496), so
        /// <c>PopulationEnumerator.BuildListOfGuiPopulations</c> lays out the other empire's
        /// affinities through the other empire's own <c>DepartmentOfTheInterior</c>. Mirroring what is
        /// drawn means reading it; only the two things the game refuses there are refused here - the
        /// unit cannot be picked up and the card cannot be dropped on, both of which stay gated on
        /// <see cref="Settled"/>.
        /// </summary>
        private static List<PopulationSlots.Slot> PlanetSlots(
            PlanetLabel_SystemManagement label,
            List<Population> units
        )
        {
            List<PopulationSlots.Slot> slots = new List<PopulationSlots.Slot>(8);
            try
            {
                if (DrawnMarkers(label) == 0)
                {
                    return slots;
                }

                ColonizedPlanet colony = Colony(label);
                if (colony == null)
                {
                    Planet unsettled = label.Planet;
                    if (unsettled != null)
                    {
                        PopulationSlots.BuildUnsettled(
                            unsettled.PopulationCount,
                            unsettled.MaxPopulation,
                            slots
                        );
                    }

                    return slots;
                }

                foreach (KeyValuePair<StaticString, Population> entry in colony.PopulationsByAffinity)
                {
                    Population population = entry.Value;
                    for (int i = 0; population != null && i < population.Count; i++)
                    {
                        units.Add(population);
                    }
                }

                PopulationSlots.Build(
                    units.Count,
                    colony.MaxPopulation,
                    colony.MaxPopulationUnderOverPopulation,
                    OverpopulationDrawn(colony),
                    slots
                );
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a planet's population slots threw: " + e);
            }

            return slots;
        }

        /// <summary>Whether the game would draw the overpopulation arc over this colony's ring, which
        /// is what decides whether the slots past its comfortable maximum are a band of their own -
        /// the four conditions <c>PlanetPopulationEnumeratorRadial.RefreshOverpopulation</c> puts on
        /// the sector's visibility, asked here rather than re-derived, so a mode of play where the arc
        /// means nothing (an empire that runs on honour, a system somebody else is exploiting) reads
        /// as one plain band of slots exactly as it is drawn.</summary>
        private static bool OverpopulationDrawn(ColonizedPlanet colony)
        {
            try
            {
                ColonizedStarSystem system = colony.ColonizedStarSystem;
                return system != null
                    && system.State != StarSystemState.Lost
                    && !(system is ExploitedStarSystem)
                    && !colony.Empire.CanUseHonor;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>How many markers the ring the game is DRAWING is showing. The container keeps its
        /// retired markers as invisible children (<c>PopulationEnumerator.HideAllPopulationMarkers</c>
        /// pools them without unparenting), so the visible ones are the ring - and they are in slot
        /// order, because the enumerator sets each one's sibling index to its own slot and sorts.
        /// </summary>
        private static int DrawnMarkers(PlanetLabel_SystemManagement label)
        {
            AgeTransform container = MarkerContainer(label);
            IList<AgeTransform> markers = container == null ? null : container.Children;
            int drawn = 0;
            for (int i = 0; markers != null && i < markers.Count; i++)
            {
                if (markers[i] != null && markers[i].Visible)
                {
                    drawn++;
                }
            }

            return drawn;
        }

        /// <summary>The widget the ring is drawing for one slot, which is where that slot's dossier
        /// belongs on the screen. Null where the ring and the model disagree about how many slots
        /// there are - a frame in the middle of a refresh - and the dossier then falls back to the
        /// scratch carrier's own corner.</summary>
        private static AgeTransform DrawnMarker(PlanetLabel_SystemManagement label, int index)
        {
            AgeTransform container = MarkerContainer(label);
            IList<AgeTransform> markers = container == null ? null : container.Children;
            int seen = 0;
            for (int i = 0; markers != null && i < markers.Count; i++)
            {
                if (markers[i] == null || !markers[i].Visible)
                {
                    continue;
                }

                if (seen == index)
                {
                    return markers[i];
                }

                seen++;
            }

            return null;
        }

        /// <summary>Whichever of the card's two population rings the game is drawing.</summary>
        private static AgeTransform MarkerContainer(PlanetLabel_SystemManagement label)
        {
            if (label == null)
            {
                return null;
            }

            PlanetPopulationEnumerator drawn =
                label.PlanetPopulationEnumeratorSimple != null
                && label.PlanetPopulationEnumeratorSimple.Shown
                    ? label.PlanetPopulationEnumeratorSimple
                    : label.PlanetPopulationEnumeratorFocused;
            return drawn == null || !drawn.Shown ? null : drawn.PopMarkersContainer;
        }

        /// <summary>
        /// A row per SLOT of the card's population ring, in the three bands the ring draws them in.
        ///
        /// The ring is a picture: one marker per place a unit of population can live, coloured for
        /// who is in it and for what kind of place it is. A row per AFFINITY - what this was until
        /// 2026-08-26 - said who lived on the world and nothing about how much room there was, which
        /// is the question the ring is on the card to answer. A row per slot says both, and the three
        /// colours become three REGIONS the player steps between, named in the game's own words.
        ///
        /// The bands are contiguous by construction, so each is opened once and the region and the
        /// context are closed on the way out of it.
        ///
        /// <paramref name="canCarry"/> is where a unit can be picked up, which is only where there is
        /// somewhere to put it down - and only on the player's OWN colony, because the game moves
        /// nobody else's population. One press carries ONE unit - the smallest move the game's own
        /// drag makes - and the affinity is captured then, because the row is rebuilt every frame and
        /// those people may have left the planet by the time it is dropped.
        /// </summary>
        private static void AddPopulationSlots(
            GraphBuilder builder,
            string keyPrefix,
            PlanetLabel_SystemManagement label,
            List<Population> units,
            List<PopulationSlots.Slot> slots,
            bool canCarry
        )
        {
            if (slots.Count == 0)
            {
                return;
            }

            ColonizedPlanet colony = Colony(label);
            bool carry = canCarry && Settled(label) != null;
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
                        builder.PushContext(BandName(band));
                    }

                    AddPopulationSlot(builder, keyPrefix, label, units, slot, total, colony, carry);
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

        /// <summary>
        /// One slot of the ring.
        ///
        /// What it SAYS is where it is and who is in it; which band it is in is said by the region it
        /// is read in, so no row here carries an "overpopulated" or a "locked" word of its own.
        ///
        /// What it CARRIES is the dossier the game hangs on that marker, on a carrier of this mod's
        /// own (<see cref="ScratchTooltips"/>) because the ring the player is navigating is the SIMPLE
        /// one, whose markers the game binds no tooltip to at all - only the detailed ring it swaps in
        /// under a mouse gets them (<c>PopulationMarker.Bind</c> does all of it under
        /// <c>IsDetailed</c>). The carrier is parked over the marker's own place on the ring, so the
        /// panel appears beside the picture it explains.
        ///
        /// A FILLED slot under the overpopulation arc carries two things at once - who lives there,
        /// and what having them there costs - so the dossier is the row's and the arc's sentence
        /// becomes the one child in its "Tooltips" region.
        /// </summary>
        private static void AddPopulationSlot(
            GraphBuilder builder,
            string keyPrefix,
            PlanetLabel_SystemManagement label,
            List<Population> units,
            PopulationSlots.Slot slot,
            int total,
            ColonizedPlanet colony,
            bool canCarry
        )
        {
            Population unit = slot.Unit >= 0 && slot.Unit < units.Count ? units[slot.Unit] : null;
            string key = keyPrefix + "/population/" + slot.Rank;
            int rank = slot.Rank;
            int outOf = total;
            bool empty = unit == null && slot.Kind != PopulationSlots.Band.Locked;
            // An UNSETTLED world's ring is all one band of empty slots
            // (<see cref="PopulationSlots.BuildUnsettled"/>), so the row's position in its region is
            // already its rank and saying it again in the label made every row read "Empty slot 1 of
            // 6, 1 of 6". A COLONIZED card keeps the numbered phrase: there the ring is split into
            // bands, so a row's position within its band is not its rank round the ring.
            bool vacant = colony == null && empty;
            AgeTooltip carrier = SlotCarrier(label, colony, slot, unit);
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
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(carrier, TooltipMode.Indicate)
                ),
            };

            if (carrier != null)
            {
                AgeWidgets.PointAt(vtable, carrier.AgeTransform);
            }

            if (canCarry && colony != null && unit != null)
            {
                ColonizedPlanet source = colony;
                Population held = unit;
                vtable.OnPickUp = () => Pick(source, held);
            }

            List<TooltipChildren.Dossier> nested = SlotDossiers(label, colony, slot, unit);
            if (nested.Count == 0)
            {
                builder.AddItem(ControlId.Structural(key), vtable);
                return;
            }

            ControlId id = ControlId.Structural(key);
            vtable.ControlType = ControlTypes.Group;
            builder.BeginGroup(id, vtable);
            if (builder.IsExpanded(id))
            {
                TooltipChildren.Emit(builder, key, nested, TooltipChildren.Actions(builder, key));
            }

            builder.EndGroup();
        }

        /// <summary>The sentence a slot carries BESIDE its own dossier, which is only ever the one: a
        /// filled slot under the overpopulation arc, whose row is already the population's dossier and
        /// whose arc still has something to say about it.</summary>
        private static List<TooltipChildren.Dossier> SlotDossiers(
            PlanetLabel_SystemManagement label,
            ColonizedPlanet colony,
            PopulationSlots.Slot slot,
            Population unit
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(1);
            if (unit == null || slot.Kind != PopulationSlots.Band.Overpopulation)
            {
                return found;
            }

            AgeTooltip carrier = OverpopulationCarrier(label, colony, slot.Rank);
            if (carrier != null)
            {
                TooltipChildren.AddPlain(found, carrier, carrier.AgeTransform);
            }

            return found;
        }

        /// <summary>Whichever dossier the ring hangs on this slot: the population's for a filled one,
        /// the arc's sentence for an empty one under the arc, the game's word about what would unlock
        /// it for a locked one - and nothing at all for an ordinary empty place, which the game
        /// explains nowhere either.</summary>
        private static AgeTooltip SlotCarrier(
            PlanetLabel_SystemManagement label,
            ColonizedPlanet colony,
            PopulationSlots.Slot slot,
            Population unit
        )
        {
            if (unit != null)
            {
                return PopulationCarrier(label, colony, slot.Rank, unit);
            }

            if (slot.Kind == PopulationSlots.Band.Locked)
            {
                return LockedCarrier(label, slot.Rank);
            }

            return slot.Kind == PopulationSlots.Band.Overpopulation
                ? OverpopulationCarrier(label, colony, slot.Rank)
                : null;
        }

        /// <summary>A carrier bound exactly as <c>PopulationMarker.Bind</c> binds the game's own
        /// detailed marker - the same class, the same wrapper, the same context - so the tooltip
        /// window assembles the population's own dossier for a ring that is drawing no tooltips.
        /// </summary>
        private static AgeTooltip PopulationCarrier(
            PlanetLabel_SystemManagement label,
            ColonizedPlanet colony,
            int rank,
            Population unit
        )
        {
            try
            {
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    SlotKey(label, rank),
                    SlotStamp(colony, (string)unit.Affinity, unit.Count),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiPopulation wrapper = Wrap(colony.Empire, unit);
                    carrier.Class = "Population";
                    carrier.Content = wrapper.Title;
                    carrier.Target = wrapper;
                    carrier.Context = wrapper.EmpirePopulationSimulationObject;
                }

                Park(carrier, label, rank);
                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("system: binding a population slot's dossier threw: " + e);
                return null;
            }
        }

        /// <summary>A carrier holding the sentence the game writes on the overpopulation arc's own
        /// icon (<c>PlanetPopulationEnumeratorRadial.RefreshOverpopulation</c>), which is plain text
        /// under no class - so it is bound as plain text under no class here. The game picks its
        /// singular or plural by how many slots the arc covers, and so does this.</summary>
        private static AgeTooltip OverpopulationCarrier(
            PlanetLabel_SystemManagement label,
            ColonizedPlanet colony,
            int rank
        )
        {
            try
            {
                int covered = colony.MaxPopulation - colony.MaxPopulationUnderOverPopulation;
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    SlotKey(label, rank) + "/overpopulation",
                    covered,
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    carrier.Class = string.Empty;
                    carrier.Target = null;
                    carrier.Context = null;
                    carrier.Content = Gui.Localize(
                        covered == 1 ? OverpopulationSentence : OverpopulationSentencePlural
                    );
                }

                Park(carrier, label, rank);
                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("system: binding an overpopulation slot's sentence threw: " + e);
                return null;
            }
        }

        /// <summary>A carrier bound as the game binds a locked marker: its own simple panel naming the
        /// project that would raise this world's maximum.</summary>
        private static AgeTooltip LockedCarrier(PlanetLabel_SystemManagement label, int rank)
        {
            try
            {
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    SlotKey(label, rank) + "/locked",
                    1L,
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    carrier.Class = "Simple";
                    carrier.Target = null;
                    carrier.Context = null;
                    carrier.Content = LockedSentence;
                }

                Park(carrier, label, rank);
                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("system: binding a locked slot's sentence threw: " + e);
                return null;
            }
        }

        /// <summary>Put a slot's carrier where the ring draws that slot, so the panel opens beside the
        /// marker rather than at the screen's corner. The corner is the fallback and is what
        /// <see cref="ScratchTooltips.Rebind"/> has already set, so a slot the ring is not drawing
        /// this frame simply keeps it.</summary>
        private static void Park(AgeTooltip carrier, PlanetLabel_SystemManagement label, int rank)
        {
            AgeTransform marker = DrawnMarker(label, rank - 1);
            if (marker != null)
            {
                ScratchTooltips.PlaceOver(carrier, marker);
            }
        }

        private static string SlotKey(PlanetLabel_SystemManagement label, int rank)
        {
            return "population-slot/" + label.Planet.GUID + "/" + rank;
        }

        /// <summary>What a population slot's dossier depends on: the empire's turn, and who is in the
        /// slot. Rebinding on anything less would reset the tooltip controller's countdown every
        /// frame and the panel would never finish appearing.</summary>
        private static long SlotStamp(ColonizedPlanet colony, string affinity, int count)
        {
            long stamp = 17L;
            for (int i = 0; affinity != null && i < affinity.Length; i++)
            {
                stamp = (stamp * 31L) + affinity[i];
            }

            try
            {
                Game game = Gui.Game;
                stamp = (stamp * 1000003L) + (game == null ? 0L : game.Turn);
            }
            catch (Exception) { }

            return (stamp * 97L) + count;
        }

        /// <summary>
        /// What a band of slots is called.
        ///
        /// The three words are the GAME's own, taken straight from its localization rather than given
        /// mod keys of their own - an owner ruling of 2026-08-26, and a deliberate departure from this
        /// mod's usual "every phrase it authors is a ModStrings key". The game already draws all three
        /// words for these very things, so borrowing them costs the player no new vocabulary and costs
        /// the translators nothing at all.
        /// </summary>
        private static string BandName(PopulationSlots.Band band)
        {
            string key = PopulationBandTitle;
            if (band == PopulationSlots.Band.Overpopulation)
            {
                key = OverpopulationBandTitle;
            }
            else if (band == PopulationSlots.Band.Locked)
            {
                key = LockedBandTitle;
            }

            return AgeText.Clean(Gui.Localize(key));
        }

        private const string PopulationBandTitle = "%PlanetScreenPopulationTitle";
        private const string OverpopulationBandTitle = "%HappinessOverPopulationPenalties";
        private const string LockedBandTitle = "%EconomyLockedTradingCompanySlotTitle";
        private const string OverpopulationSentence = "%PlanetLabelOverPopulationDescription";
        private const string OverpopulationSentencePlural =
            "%PlanetLabelOverPopulationDescriptionPlural";
        private const string LockedSentence = "%PopulationEnumeratorLockedDescription";

        /// <summary>The game's own word for an affinity - what its marker's tooltip is titled with.
        /// </summary>
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

        /// <summary>One unit of this population, picked up. Null where the game would not let the drag
        /// start: its own two tests are the system's and the affinity's.</summary>
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
                Log.Warn("system: picking a population unit up threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The population waiting in the spaceport, read the same way a planet card's ring is read: a
        /// row per affinity, with the count said rather than counted, and a unit of it can be picked up.
        ///
        /// The spaceport is the OTHER place this system keeps population, and the game moves it the same
        /// way - a drag out of the panel onto one of this page's planet cards, which posts
        /// <c>OrderTransferSpaceportPopulation</c> (<c>SpaceportSidePanel.StartDrag</c> :201-209,
        /// <c>IDragDropClient.ApplyDrop</c> :70-80). So it is the same carry, taken by the same planet
        /// cards, and only where it lands differs.
        ///
        /// The panel's markers are its own children rather than a container's, so this claims the
        /// enumerator itself and stops the walk descending into a row of wordless slots. An empty
        /// spaceport - and a locked slot the system's level has not paid for yet - contributes nothing,
        /// which is what the walk did before this and what a planet card with no ring does.
        /// </summary>
        private static bool SpaceportPopulations(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SpaceportSidePanel panel
        )
        {
            PopulationEnumerator markers = panel == null
                ? null
                : panel.SpaceportPopulationEnumerator;
            if (markers == null || !ReferenceEquals(widget, markers.AgeTransform))
            {
                return false;
            }

            try
            {
                Spaceport port = panel.Spaceport;
                IList<AgeTransform> slots = markers.AgeTransform.Children;
                List<Population> found = new List<Population>(2);
                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    AgeTransform slot = slots[i];
                    if (slot == null || !slot.Visible)
                    {
                        continue;
                    }

                    PopulationMarker it = slot.GetComponent<PopulationMarker>();
                    Population population =
                        it == null || it.GuiPopulation == null ? null : it.GuiPopulation.Population;
                    if (population == null || found.Contains(population))
                    {
                        continue;
                    }

                    found.Add(population);
                    Population held = population;
                    // No tooltip: the sentence the panel writes onto an occupied slot is the one its own
                    // heading already carries, and a slot the panel has not refreshed yet still holds
                    // the prefab's placeholder (measured: "This is changed by code").
                    NodeVtable vtable = GraphNodes.Readout(
                        () => PopulationName(held),
                        () => new MessageBuilder().PushQuantity(held.Count).Build(),
                        null,
                        null
                    );
                    if (port != null)
                    {
                        Spaceport source = port;
                        vtable.OnPickUp = () => PickFromSpaceport(source, held);
                    }

                    cells.Add(
                        new Cell
                        {
                            Widget = slot,
                            Id = ControlId.Referenced(
                                population,
                                keyPrefix + "spaceport/population/" + found.Count
                            ),
                            Vtable = vtable,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the spaceport's population threw: " + e);
            }

            return true;
        }

        /// <summary>One unit of the spaceport's population, picked up - the game's own two tests for
        /// starting that drag (<c>PopulationEnumerator.OnPopulationMarkerDragStarted</c> :239-252), asked
        /// of the spaceport instead of a planet.</summary>
        private static CarryItem PickFromSpaceport(Spaceport port, Population population)
        {
            try
            {
                IPopulationsManagementService populations =
                    Services.GetService<IPopulationsManagementService>();
                if (
                    population.Count <= 0
                    || !port.CanMovePopulation
                    || port.Empire != Gui.PlayerEmpire
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
                Log.Warn("system: picking a unit out of the spaceport threw: " + e);
                return null;
            }
        }

        /// <summary>The drawn spaceport panel this carried unit came OUT of, or null - which is how a
        /// drop tells the two sources apart, since what is carried is the game's own
        /// <c>Population</c> and the owner holding it is the one whose own table it is in.</summary>
        private static SpaceportSidePanel SpaceportSource(Population population)
        {
            try
            {
                StarSystemScreen screen = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<StarSystemScreen>(false)
                    : null;
                SpaceportSidePanel panel =
                    screen == null ? null : screen.GetSpaceportSidePanel();
                Spaceport port = panel == null ? null : panel.Spaceport;
                if (port == null || !panel.Shown || port.PopulationsByAffinity == null)
                {
                    return null;
                }

                Population held;
                return port.PopulationsByAffinity.TryGetValue(population.Affinity, out held)
                    && ReferenceEquals(held, population)
                    ? panel
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Put a carried population unit on this planet, the way the drag does it: the game's own
        /// <c>PopulationEnumerator.DragInfo</c> is filled in exactly as
        /// <c>PopulationEnumerator.OnPopulationMarkerDragStarted</c> fills it, the target's own
        /// <c>CanAcceptPopulationDrop</c> decides, and the SOURCE's own
        /// <c>IDragDropClient.ApplyDrop</c> posts the order - which is what keeps the sound the game
        /// plays and the exact order it builds. Which source that is decides which order: a unit off
        /// another planet's ring goes through the labels window
        /// (<c>OrderTransferPopulationFromPlanetToPlanet</c>), and a unit out of the spaceport through
        /// the spaceport panel (<c>OrderTransferSpaceportPopulation</c>) - the same two clients the
        /// game's own two drags use, rather than one order written twice here.
        ///
        /// The drag info is cleared again whatever happens: it is a static the game's own refresh
        /// reads every frame to draw a unit as already gone, and a stale one would empty a marker the
        /// player is still looking at.
        /// </summary>
        private static DropResult DropPopulation(PlanetLabel_SystemManagement label, CarryItem item)
        {
            Population population = item == null ? null : item.Cargo as Population;
            ColonizedPlanet destination = Settled(label);
            ColonizedPlanet source = population == null ? null : SourceOf(destination, population);
            SpaceportSidePanel port =
                population == null || source != null ? null : SpaceportSource(population);
            if (destination == null || (source == null && port == null))
            {
                return DropResult.Refused(null);
            }

            try
            {
                IDragDropClient client = source != null
                    ? (IDragDropClient)
                        Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false)
                    : port;
                PopulationEnumerator.PopulationDragInfo drag = PopulationEnumerator.DragInfo;
                drag.DragInProgress = true;
                if (source != null)
                {
                    drag.SourcePopulationOwner = source;
                    drag.GuiPopulation = Wrap(source.Empire, population);
                }
                else
                {
                    drag.SourcePopulationOwner = port.Spaceport;
                    drag.GuiPopulation = Wrap(port.Spaceport.Empire, population);
                }

                drag.Quantity = 1;
                drag.TransitingPopulation = new TransitingPopulation(population.Affinity, 1);
                drag.ReplacedPopulationAffinity = StaticString.Empty;
                try
                {
                    if (client == null || !label.PlanetPopulationEnumeratorFocused.CanAcceptPopulationDrop())
                    {
                        return DropResult.Refused(null);
                    }

                    client.ApplyDrop(label);
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
                Log.Warn("system: moving a population unit threw: " + e);
                return DropResult.Refused(null);
            }
        }

        /// <summary>Which planet of this system the carried unit came off. Found rather than
        /// remembered: what is carried is the game's own <c>Population</c>, and the planet holding it
        /// is the one whose own table it is in.</summary>
        private static ColonizedPlanet SourceOf(ColonizedPlanet destination, Population population)
        {
            try
            {
                ColonizedStarSystem system =
                    destination == null ? null : destination.ColonizedStarSystem;
                if (system == null || population == null)
                {
                    return null;
                }

                for (int i = 0; i < system.PlanetsColonized.Count; i++)
                {
                    ColonizedPlanet planet = system.PlanetsColonized[i];
                    if (planet == null || ReferenceEquals(planet, destination))
                    {
                        continue;
                    }

                    Population held;
                    if (
                        planet.PopulationsByAffinity.TryGetValue(population.Affinity, out held)
                        && ReferenceEquals(held, population)
                    )
                    {
                        return planet;
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's own wrapper for a population, built the way its own enumerator builds
        /// one - which is what <c>ApplyDrop</c> reads the affinity out of.</summary>
        private static GuiPopulation Wrap(Empire owner, Population population)
        {
            DepartmentOfTheInterior interior = owner.GetAgency<DepartmentOfTheInterior>();
            PopulationEmpire empire =
                interior == null
                    ? null
                    : interior.GetPopulationByAffinity(population.Affinity) as PopulationEmpire;
            return new GuiPopulation(population, empire, owner);
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
        ///
        /// The ghost pair is the one set no save here can reach - the state needs the Umbral Choir, and
        /// the Penumbra content is not installed - so it was measured by lending the two panels a real
        /// colony and showing them (2026-08-25). Every widget the game drew was declared: the growth
        /// gauge, the affinity of the next population, each population count with its parties, the
        /// panel's own explanation, the link status, and both destination buttons with their refusal
        /// reasons in the buffer. The two boxes the game hides while the link is unset
        /// (<c>GhostInfoSidePanel.Refresh</c> :89-101, :114-126) stayed hidden and undeclared, which is
        /// the same rule every other panel here is read by. What the lend cannot prove is the CONTENT a
        /// real ghost would carry, and what it leaves open is what the two stops are called
        /// (<see cref="PanelName"/>).
        /// </summary>
        private void BuildSidePanels(GraphBuilder builder)
        {
            try
            {
                SidePanels.Drawn(_panels);
                for (int i = 0; i < _panels.Count; i++)
                {
                    SidePanel panel = _panels[i];
                    builder.BeginStop("system:side/" + panel.GetType().Name);
                    builder.PushContext(PanelName(panel));
                    ColonyInfoSidePanel colony = panel as ColonyInfoSidePanel;
                    RepresentativesStarSystemSidePanel representatives =
                        panel as RepresentativesStarSystemSidePanel;
                    if (colony != null)
                    {
                        BuildColonyInfo(builder, colony);
                    }
                    else if (representatives != null)
                    {
                        BuildRepresentatives(
                            builder,
                            representatives,
                            "system:side/" + i + "/"
                        );
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

        /// <summary>What a side panel is called. The game writes no title on the ones a system draws -
        /// it marks each with an icon in its corner and explains it in that icon's tooltip - so they are
        /// named here, and anything else falls through to the shared reader's own answer
        /// (<see cref="SidePanels.Name"/>).</summary>
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

            if (panel is OutpostInfoSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemOutpostPanel);
            }

            // The hero panel is the fourth of the unlabelled boxes. Without a name of its own it fell
            // through to its header tooltip, so the stop was called "Shows information concerning the
            // Governor assigned to this star system" - a sentence, where every other stop is a word.
            if (panel is ColonyHeroSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemGovernorPanel);
            }

            // The two boxes a ghost system gets are the same kind of unlabelled box, and without a name
            // they fell through to their header sentences (measured by lending them a colony,
            // 2026-08-25). "Sanctuary" is the game's own word for a ghost colony, so both names stay in
            // its vocabulary even though the labels are the mod's (owner-approved 2026-08-25).
            if (panel is GhostPopulationSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemSanctuaryPopulationPanel);
            }

            if (panel is GhostInfoSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemSanctuaryPanel);
            }

            return SidePanels.Name(panel);
        }

        /// <summary>
        /// The colony panel, hand-modelled because it is the one side panel that is mostly controls:
        /// the system's name is a rename button, the upkeep line opens the improvements list, and the
        /// automation policy is a list to choose from.
        ///
        /// Most of what the panel can draw it draws for nobody: an Ark exploiting the system, a
        /// citadel's second garrison, a ghost's decolonize tick, a siege or a blockade, the empires
        /// that have seen through a cloak. Every one of those is declared here and gated on the game's
        /// own drawn flag - the same rule the side panels themselves are chosen by - so a save that
        /// reaches the state gets the line without anything here modelling the state.
        ///
        /// The order is the panel's own, top to bottom, and <see cref="Cells.EmitLinear"/> takes it off
        /// the rectangles rather than off the order the cells are added, so a group the game collapses
        /// takes its line with it.
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

            AddMothership(_cells, panel);
            AddSystemPaging(_cells);

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

            AddInfoIcons(_cells, panel);
            AddTemporaryEffects(_cells, panel);

            // The garrison dossier - what the defence is, how efficient it is, which troops it is made
            // of - is a tooltip the panel keeps in a field of its own and hangs on the GROUP around the
            // number, not on the number: read from the number's own transform there is no tooltip at
            // all, which is how this line came to say "240/240" and nothing else. The caption is the
            // game's own word for the value - the dossier wrapper's title, "System Garrison" (owner
            // ruled 2026-08-19, matching the citadel row). No fallback word: a tooltip yielding no
            // title leaves the bare value (owner ruled 2026-08-19, unauthorized fallbacks disallowed).
            AddReadout(
                _cells,
                panel.SecurityValue == null ? null : panel.SecurityValue.AgeTransform,
                "system:colony/security",
                () => AgeWidgets.TooltipTitle(panel.SecurityAndTroopsTooltip),
                () => AgeText.Label(panel.SecurityValue),
                panel.SecurityAndTroopsTooltip
            );
            AddCitadelManpower(_cells, panel);
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

            AddDecolonizeGhost(_cells, panel);
            AddMilitaryStatus(_cells, panel);
            AddOwnership(_cells, panel);
            AddFidsiCells(_cells, panel);
            AddResources(_cells, panel);
            AddWreckedMotherships(_cells, panel);
            AddPolicy(_cells, panel);
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// The Ark parked on this system, which a Vodyani colony IS: the panel draws its name and a
        /// button that sends it back out into the galaxy
        /// (<c>ColonyInfoSidePanel.RefreshExploited</c> :650-667), and draws neither for anybody else.
        ///
        /// The NAME is a control and not a readout - the button behind it opens the game's rename box
        /// (<c>OnMothershipRenameCb</c> :953), exactly as the system's own title does - so it is
        /// declared the same way the title is: called by the name written on it, with the ship's
        /// dossier behind it. That dossier hangs on the LABEL rather than on the button, and the
        /// button's own tooltip is a key the game's corpus has no entry for (measured:
        /// <c>%StarSystemSideRenameMothershipDescription</c> localizes to itself), so the label's is
        /// the one declared and the one the pointer is aimed at.
        ///
        /// Detach is the game's own word for its button (<c>%StarSystemSideDetachMothershipTitle</c>);
        /// what it does, and why it cannot be done today, are in its own tooltip, which the panel
        /// rewrites with the ship's refusals every refresh.
        /// </summary>
        private static void AddMothership(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgePrimitiveLabel name = panel.MothershipNameLabel;
            if (
                panel.MothershipGroup == null
                || !AgeWidgets.Visible(panel.MothershipGroup)
                || name == null
            )
            {
                return;
            }

            AgeTransform label = name.AgeTransform;
            AgeControlButton open = label.Parent == null
                ? null
                : label.Parent.AgeControl as AgeControlButton;
            AgeTooltip ship = AgeWidgets.Raw(label);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Label(name),
                () => AgeWidgets.Press(open),
                () => AgeWidgets.Operable(label),
                ship
            );
            AgeWidgets.Point(vtable, open, ship, label);
            Add(cells, label, ControlId.Referenced(label, "system:colony/mothership"), vtable);

            AgeControlButton detach = panel.DetachButton;
            AgeTransform widget = AgeWidgets.Transform(detach);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlButton it = detach;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable button = GraphNodes.Button(
                CardActions.GameText("%StarSystemSideDetachMothershipTitle"),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(widget),
                tooltip
            );
            AgeWidgets.Point(button, it);
            Add(cells, widget, ControlId.Referenced(detach, "system:colony/detach"), button);
        }

        /// <summary>
        /// The row of badges beside the system's name: that this is somebody's home system, that a
        /// trading company keeps its headquarters or a subsidiary here, and that the system is cloaked
        /// (<c>ColonyInfoSidePanel.Refresh</c> :439-483). Each is drawn only when it is true of this
        /// system, and each is one node, because each carries a sentence of its own.
        ///
        /// The game writes no caption on any of them and hangs no wrapper on their tooltips, so each is
        /// called by the sentence its own tooltip explains it with - the same naming a wordless symbol
        /// gets everywhere else in this mod. That sentence is therefore not announced a second time:
        /// it is indicated, which leaves the whole of it - including the list of empires that have
        /// seen through the cloak, which is the only place that list exists - in the review buffer.
        /// </summary>
        private static void AddInfoIcons(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AddInfoIcon(cells, panel.HomeSystemImage, "home");
            AddInfoIcon(cells, panel.TradeInfrastructuremage, "trade");
            AddInfoIcon(cells, panel.InvisibilityImage, "cloak");
        }

        private static void AddInfoIcon(List<Cell> cells, AgePrimitiveImage icon, string key)
        {
            AgeTransform widget = icon == null ? null : icon.AgeTransform;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => FirstLine(tooltip)),
                },
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(tooltip, TooltipMode.Indicate)
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            cells.Add(
                new Cell
                {
                    Widget = widget,
                    Id = ControlId.Referenced(widget, "system:colony/icon/" + key),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The buffs and curses running on this system (<c>RefreshTemporaryEffects</c> :711-736). The
        /// panel has two layouts for the same list and shows exactly one of them - a line with the
        /// effect's name and how long it has left while there are one or two, a strip of bare symbols
        /// once there are more - so whichever table is drawn is the one read, and the reading is the
        /// same either way: what the item says, and its dossier behind it.
        ///
        /// The strip's items carry no label at all (measured: the simple prefab's
        /// <c>TemporaryEffectLine.Label</c> is null), so there each effect is called by the wrapper on
        /// its own tooltip, which is where the game keeps its title.
        /// </summary>
        private static void AddTemporaryEffects(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AddTemporaryEffects(cells, panel.TemporaryEffectsLineTable, "line");
            AddTemporaryEffects(cells, panel.TemporaryEffectsSimpleItemTable, "item");
        }

        private static void AddTemporaryEffects(
            List<Cell> cells,
            AgeTransform table,
            string key
        )
        {
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                TemporaryEffectLine line =
                    item == null ? null : item.GetComponent<TemporaryEffectLine>();
                // Pooled (ColonyInfoSidePanel.cs:723 ReserveChildren): a colony with fewer temporary
                // effects than the one read before it keeps the surplus lines Visible at alpha 0,
                // still holding the other colony's words.
                if (line == null || !AgeWidgets.Paints(item))
                {
                    continue;
                }

                TemporaryEffectLine it = line;
                AddReadout(
                    cells,
                    item,
                    "system:colony/effect/" + key + "/" + i,
                    () =>
                        Drawn(it.Label)
                        ?? AgeWidgets.TooltipTitle(it.Tooltip),
                    null,
                    line.Tooltip
                );
            }
        }

        /// <summary>The second pool of troops a Hissho citadel keeps, drawn beside the system's own
        /// (<c>RefreshSecurityAndUpkeep</c> :556-564) and only where the system has a citadel. The
        /// number is a stock over a maximum and the game writes no word beside it; the word is the one
        /// on the wrapper the panel hangs on the group's tooltip - "Citadel Garrison" - which is also
        /// where the breakdown of those troops lives.</summary>
        private static void AddCitadelManpower(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeTransform group = panel.CitadelManpowerGroup;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(group);
            AgePrimitiveLabel value = panel.CitadelManpowerValue;
            AddReadout(
                cells,
                group,
                "system:colony/citadel-manpower",
                () => AgeWidgets.TooltipTitle(tooltip),
                () => AgeText.Label(value),
                tooltip
            );
        }

        /// <summary>
        /// The tick a GHOST system draws where a colony draws its upkeep: schedule this sanctuary to be
        /// abandoned at the end of the turn, or unschedule it
        /// (<c>OnDecolonizeGhostToggleCb</c> :1002-1019). It is a real two-state box - the panel reads
        /// its state back off the standing order every refresh - so it is declared as one, and Enter is
        /// its own click, which posts the order or cancels it.
        ///
        /// The game names it on the action rather than on the tick
        /// (<c>%DecolonizeGhostActionTitle</c>), and the tooltip is that action's description with the
        /// panel's own reasons for refusing appended.
        /// </summary>
        private static void AddDecolonizeGhost(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeControlToggle toggle = panel.DecolonizeGhostToggle;
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            NodeVtable vtable = GraphNodes.Checkbox(
                CardActions.GameText("%DecolonizeGhostActionTitle"),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(widget),
                AgeWidgets.Raw(widget)
            );
            // Ticking it posts an ORDER and the tick only becomes true once the department holds the
            // action, so the state read back on the keypress is the state before it - doubly so here,
            // because the game's own handler flips the box a second time on top of the click's flip
            // (<c>AgeControlToggle.HandleMouseUpOrDown</c> :211-215 flips, then dispatches;
            // <c>OnDecolonizeGhostToggleCb</c> :1004 flips again). The live value part is what says
            // what actually happened, when it happens.
            vtable.StateText = null;
            AgeWidgets.Point(vtable, it);
            Add(cells, widget, ControlId.Referenced(toggle, "system:colony/decolonize"), vtable);
        }

        /// <summary>
        /// The banner the panel puts up when something military is happening to this system - it is
        /// frozen in a time bubble, being invaded, being converted, under siege, or blockaded
        /// (<c>RefreshMilitaryStatusAndOwnership</c> :569-615). One of the five at most, and nothing
        /// at all the rest of the time.
        ///
        /// The game writes the state's own word on the banner and assembles the paragraph behind it
        /// from the descriptor doing it, so the word is the line and the paragraph is the review.
        /// </summary>
        private static void AddMilitaryStatus(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeTransform group = panel.SystemMilitaryStatusGroup;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgePrimitiveLabel label = panel.SystemMilitaryStatusLabel;
            AddReadout(
                cells,
                group,
                "system:colony/military-status",
                () => AgeText.Label(label)
            );
        }

        /// <summary>
        /// How much of this system its owner actually holds, drawn only while somebody else holds some
        /// of it (<c>RefreshMilitaryStatusAndOwnership</c> :633-646). The panel draws the percentage
        /// beside a symbol and writes no caption, so the caption is the game's own title for the
        /// property the number comes from - the same naming the five outputs above it get.
        ///
        /// The group answers a click, but only in the developers' god mode
        /// (<c>OnOwnershipGroupCb</c> :889-900), so it is a readout here rather than a button that
        /// does nothing - the same treatment the population panel's approval box gets.
        /// </summary>
        private static void AddOwnership(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeTransform group = panel.OwnershipGroup;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgePrimitiveLabel label = panel.OwnershipLabel;
            AddReadout(
                cells,
                group,
                "system:colony/ownership",
                () =>
                    AgeText.Clean(
                        Gui.GetLocalizedTitle(SimulationProperties.StarSystem.Ownership)
                    ),
                () => AgeText.Label(label),
                panel.OwnershipTooltip
            );
        }

        /// <summary>
        /// The strategics and luxuries this system is exploiting. The panel keeps the banner hidden
        /// until it has something in it (<c>ResourcesBanner_Refresh</c> :847-851), so being drawn is
        /// the gate and an empty banner contributes nothing.
        ///
        /// One row per resource, read the way the empire's own stockpile strip is read
        /// (<see cref="GlobalHud"/>): the resource's name, then what is held and what the next turn
        /// does to it, computed rather than read off the labels - the labels are animated towards
        /// their targets and a reading taken mid-slide is a number the game never displayed.
        /// </summary>
        private static void AddResources(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            ResourcesPanel banner = panel.ResourcesBanner;
            AgeTransform table = banner == null ? null : banner.ResourceItemsTable;
            if (table == null || !AgeWidgets.Visible(banner.AgeTransform))
            {
                return;
            }

            try
            {
                IList<AgeTransform> items = table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    AgeTransform widget = items[i];
                    ResourceItem item =
                        widget == null ? null : widget.GetComponent<ResourceItem>();
                    GuiLocatedResource resource =
                        item == null ? null : item.GuiLocatedResource;
                    if (resource == null || !AgeWidgets.Visible(widget))
                    {
                        continue;
                    }

                    GuiLocatedResource it = resource;
                    NodeVtable vtable = GraphNodes.Readout(
                        () => AgeText.Clean(it.Title),
                        () =>
                            GlobalHud.StockAndNet(
                                it.GetStockValueFromCache(),
                                it.GetNetValueFromCache(),
                                it.GetStockValueFromCache() < 10f ? 1 : 0
                            ),
                        null,
                        item.Tooltip
                    );
                    AgeWidgets.Point(vtable, item.Button, item.Tooltip, widget);
                    cells.Add(
                        new Cell
                        {
                            Widget = widget,
                            Id = ControlId.Referenced(
                                item,
                                "system:colony/resource/" + resource.Name
                            ),
                            Vtable = vtable,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the colony panel's resources threw: " + e);
            }
        }

        /// <summary>How many wrecked Arks are drifting in this system, which is the only thing the
        /// panel's special-features table has ever held (<c>RefreshSpecialFeatures</c> :686-709) and is
        /// drawn only where there is at least one. The count is a bare number beside a symbol; the
        /// caption is the game's own title for the property it counts, and what the wrecks are worth
        /// and who may salvage them is the sentence on its own tooltip.</summary>
        private static void AddWreckedMotherships(List<Cell> cells, ColonyInfoSidePanel panel)
        {
            AgeTransform group = panel.MothershipsGroup;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgePrimitiveLabel count = panel.MothershipsLabel;
            AddReadout(
                cells,
                group,
                "system:colony/wrecked-motherships",
                () =>
                    AgeText.Clean(
                        Gui.GetLocalizedTitle(
                            SimulationProperties.StarSystem.WreckedMothershipCount
                        )
                    ),
                () => AgeText.Label(count),
                panel.MothershipsTooltip
            );
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

        // ---- reading a panel nobody has modelled ----

        /// <summary>
        /// A panel read as it is drawn, through the shared side-panel reader
        /// (<see cref="SidePanels.Readouts"/>). The population and representative panels are all
        /// readouts and no decisions, and the panels an outpost or a ghost gets instead are the same
        /// shape, so they are all read that way rather than each having its own list of fields to keep
        /// in step with the game.
        ///
        /// The two hooks that reader takes are this page's own: <see cref="Special"/> for the readouts
        /// the shape of a widget tree cannot name, and <see cref="Transparent"/> for a group the game
        /// made clickable that is really a band of readouts.
        /// </summary>
        private void BuildReadouts(GraphBuilder builder, SidePanel panel, string keyPrefix)
        {
            _cells.Clear();
            SidePanels.Readouts(_cells, panel, keyPrefix, SpecialCell, Transparent);
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// The representatives panel, which is the one side panel the game draws as two CAPTIONED
        /// blocks: who this system sends to the senate, and how its citizens react to what happens.
        ///
        /// Both captions carry a sentence the game writes nowhere else, so both stay rows AND name the
        /// block under them - a context has no buffer, so converting them would delete the sentence.
        /// The blocks are read off the drawn layout: the sensitivity block is the group the breakdown
        /// graph is drawn in, and everything above it is the representatives block.
        /// </summary>
        private void BuildRepresentatives(
            GraphBuilder builder,
            RepresentativesStarSystemSidePanel panel,
            string keyPrefix
        )
        {
            AgeTransform sensitivity =
                panel.PoliticalSensitivityBreakdown == null
                    ? null
                    : panel.PoliticalSensitivityBreakdown.Parent;
            _blocks.Clear();
            IList<AgeTransform> children =
                panel.ContentGroup == null ? null : panel.ContentGroup.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (children[i] != null && AgeWidgets.Visible(children[i]))
                {
                    _blocks.Add(children[i]);
                }
            }

            _blocks.Sort(ByDrawnY);
            int split = _blocks.IndexOf(sensitivity);
            if (split <= 0)
            {
                BuildReadouts(builder, panel, keyPrefix);
                return;
            }

            EmitBlock(builder, panel, keyPrefix, "representatives", 0, split);
            EmitBlock(builder, panel, keyPrefix, "sensitivity", split, _blocks.Count);
        }

        private static readonly Comparison<AgeTransform> ByDrawnY = (left, right) =>
            left.GetGlobalPosition().y.CompareTo(right.GetGlobalPosition().y);

        /// <summary>One captioned block of a panel read in pieces: its own lines, one per row, under the
        /// caption the game drew over them - which is the topmost line the block produced, and is a row
        /// of the block as well as its name.</summary>
        private void EmitBlock(
            GraphBuilder builder,
            SidePanel panel,
            string keyPrefix,
            string name,
            int from,
            int to
        )
        {
            _cells.Clear();
            for (int i = from; i < to; i++)
            {
                SidePanels.Block(_cells, panel, _blocks[i], keyPrefix, SpecialCell, Transparent);
            }

            if (_cells.Count == 0)
            {
                return;
            }

            string caption = Caption(_cells);
            builder.SetRegion(keyPrefix + name);
            if (caption != null)
            {
                builder.PushContext(caption);
            }

            try
            {
                Cells.EmitLinear(builder, _cells);
            }
            finally
            {
                if (caption != null)
                {
                    builder.PopContext();
                }
            }
        }

        /// <summary>The caption a block is called by: the topmost line the game drew in it, where that
        /// line is words rather than a control. A block whose first line is a control has no caption and
        /// is named by nothing rather than by its first button.</summary>
        private static string Caption(List<Cell> cells)
        {
            Cell top = null;
            float y = 0f;
            for (int i = 0; i < cells.Count; i++)
            {
                float at = cells[i].Widget.GetGlobalPosition().y;
                if (top == null || at < y)
                {
                    top = cells[i];
                    y = at;
                }
            }

            if (top == null || AgeWidgets.Button(top.Widget) != null)
            {
                return null;
            }

            string text = AgeWidgets.TextOf(top.Widget);
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static bool SpecialCell(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            if (GovernorInformation(cells, widget, keyPrefix, panel as ColonyHeroSidePanel))
            {
                return true;
            }

            if (SpaceportPopulations(cells, widget, keyPrefix, panel as SpaceportSidePanel))
            {
                return true;
            }

            Cell special = Special(widget, keyPrefix, panel);
            if (special == null)
            {
                return false;
            }

            cells.Add(special);
            AddNestedDossiers(cells, widget, keyPrefix);
            return true;
        }

        /// <summary>
        /// The dossiers a row's own tooltip names INSIDE itself, as CHILDREN of that row.
        ///
        /// A population entry's tooltip ends by naming the political parties those people lean
        /// towards, and each name carries the party's own dossier - reachable by a mouse with one more
        /// hover and by nothing else, because the game draws one tooltip at a time
        /// (<see cref="PoliticsDossier"/>). They hang UNDER the population as a "Tooltips" region, like
        /// every other node in the game that owns dossiers beyond its own
        /// (<see cref="TooltipChildren"/>); until 2026-08-22 they were the row BELOW it instead,
        /// because this panel emits a flat list of cells and a cell could not open a subtree. It can
        /// now (<see cref="Cells.Declare"/>), so the compromise is retired.
        /// </summary>
        private static void AddNestedDossiers(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix
        )
        {
            PopulationCount population = widget.GetComponent<PopulationCount>();
            if (population == null || cells.Count == 0)
            {
                return;
            }

            List<TooltipChildren.Dossier> parties = PoliticsDossier.Parties(population.Tooltip);
            if (parties.Count == 0)
            {
                return;
            }

            Cell owner = cells[cells.Count - 1];
            owner.Dossiers = parties;
            owner.Key = keyPrefix + widget.name + "/population";
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

                if (ReferenceEquals(widget, population2.OutpostsGroup))
                {
                    return OutpostsCell(widget, population2, keyPrefix);
                }
            }

            OutpostInfoSidePanel outpost = panel as OutpostInfoSidePanel;
            if (
                outpost != null
                && outpost.GrowthSourceName != null
                && ReferenceEquals(widget, outpost.GrowthSourceName.AgeTransform)
            )
            {
                return GrowthSourceCell(widget, outpost, keyPrefix);
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

            ColonyHeroSidePanel governor = panel as ColonyHeroSidePanel;
            if (governor != null && ReferenceEquals(widget, governor.HeroPortraitGroup))
            {
                return GovernorPortraitCell(widget, governor, keyPrefix);
            }

            return null;
        }

        /// <summary>
        /// The band the governor panel draws beside the portrait: the hero's name, the symbol for their
        /// affinity, the gauge their experience is drawn in, the symbol for their class.
        ///
        /// Declared here rather than walked, because the shape of the band answers wrongly twice. The
        /// NAME is the portrait's own words - the panel writes the hero's title on both, and the portrait
        /// is where the dossier hangs (<see cref="GovernorPortraitCell"/>) - and the label carries a
        /// tooltip the panel never gives a hero to (measured: class <c>Hero</c>, target null), so the
        /// walk's line for it announced a dossier that can never draw and repeated the name under it. The
        /// two SYMBOLS are the opposite case: the game hangs a whole dossier on each and writes no word
        /// beside them, so a walk that keeps a line for having text dropped the only two things in the
        /// band the portrait does not already say. Each is named the way every wordless icon in this mod
        /// is named, by the wrapper on its own tooltip - "Imperials", "Counselor".
        ///
        /// Claiming the band means the gauge inside it is this method's to declare as well, which is why
        /// it is the one place the level line is built.
        /// </summary>
        private static bool GovernorInformation(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            ColonyHeroSidePanel panel
        )
        {
            if (panel == null || !ReferenceEquals(widget, panel.HeroInformationGroup))
            {
                return false;
            }

            GovernorSymbolCell(cells, panel.AffinityIcon, panel.AffinityTooltip, keyPrefix);
            if (
                panel.ExperienceGauge != null
                && AgeWidgets.Visible(panel.ExperienceGauge.AgeTransform)
            )
            {
                cells.Add(
                    GovernorLevelCell(panel.ExperienceGauge.AgeTransform, panel, keyPrefix)
                );
            }

            GovernorSymbolCell(cells, panel.ClassIcon, panel.ClassTooltip, keyPrefix);
            return true;
        }

        /// <summary>One of the two symbols in that band: what the wrapper on its tooltip calls it, and
        /// that tooltip's own dossier, pointed at the symbol so the game draws it.</summary>
        private static void GovernorSymbolCell(
            List<Cell> cells,
            AgePrimitiveImage icon,
            AgeTooltip tooltip,
            string keyPrefix
        )
        {
            AgeTransform widget = icon == null ? null : icon.AgeTransform;
            if (widget == null || tooltip == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTooltip tip = tooltip;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(tip)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget, tooltip);
            cells.Add(
                new Cell
                {
                    Widget = widget,
                    Id = ControlId.Referenced(widget, keyPrefix + widget.name),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The governor's portrait, once a hero holds the post. The game hangs the hero's dossier on the
        /// portrait IMAGE inside the group and leaves the clickable group itself textless and
        /// tooltipless, so the shape walk declared it as a nameless "button" - the tooltip is the only
        /// place the hero's name lives on this control, and the pointer has to be aimed at the child
        /// that carries it or the dossier never draws.
        ///
        /// The click is <c>OnInspectCb</c>, the same click the panel's own Inspect button carries: two
        /// controls, one command, both kept because the game draws both.
        /// </summary>
        private static Cell GovernorPortraitCell(
            AgeTransform widget,
            ColonyHeroSidePanel panel,
            string keyPrefix
        )
        {
            AgeTooltip tooltip = panel.HeroTooltip;
            AgeControlButton button = widget.AgeControl as AgeControlButton;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TooltipTitle(tooltip),
                () => AgeWidgets.Press(button),
                () => AgeWidgets.Operable(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, button, tooltip, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, keyPrefix + widget.name + "/governor-portrait"),
                Vtable = vtable,
            };
        }

        /// <summary>The governor's level. The gauge draws the number alone and explains what experience
        /// is on its tooltip, so the digit arrived captionless ("1, Heroes gain experience through their
        /// assignment..."); the word for it is the game's own, the one the hero cards put beside the
        /// same number.</summary>
        private static Cell GovernorLevelCell(
            AgeTransform widget,
            ColonyHeroSidePanel panel,
            string keyPrefix
        )
        {
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            AgePrimitiveLabel level = panel.LevelLabel;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => HeroCards.LevelCaption()),
                    GraphNodes.ValuePart(() => AgeText.Label(level)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, keyPrefix + widget.name + "/governor-level"),
                Vtable = vtable,
            };
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
        /// there are and never writes what they are called; the game keeps that name on the wrapper hung
        /// on the tooltip - which is on the SYMBOL inside the entry and not on the entry, so the pointer
        /// is aimed at the tooltip rather than at the row (measured: the row carries no tooltip of its
        /// own, and pointing at it left this row's review buffer with the dossier nowhere).</summary>
        private static Cell PopulationCell(
            AgeTransform widget,
            PopulationCount unit,
            string keyPrefix
        )
        {
            AgeTooltip tooltip = unit.Tooltip;
            AgePrimitiveLabel count = unit.Count;
            // The entry's own click opens the empire's population window
            // (<c>PopulationCount.OnClickCb</c>) - the same window the senate's census button opens,
            // which <see cref="PopulationScreen"/> already said these rows opened while no row here
            // declared any action at all.
            AgeTransform at = widget;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TooltipTitle(tooltip),
                () => AgeWidgets.Press(at),
                () => AgeWidgets.Operable(at),
                tooltip
            );
            vtable.Announcements.Insert(1, GraphNodes.ValuePart(() => AgeText.Label(count)));
            AgeWidgets.PointAt(vtable, widget, tooltip);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, keyPrefix + widget.name + "/population"),
                Vtable = vtable,
            };
        }

        /// <summary>
        /// Which colony is feeding this outpost. The panel's other rows are a caption and a value side
        /// by side, so the ordinary walk names them; this one is the colony's NAME alone, with the
        /// only words saying what that name is doing there sitting on the row's own tooltip - so the
        /// name is the value and the game's sentence is the row's tooltip, exactly as the rows above it
        /// read. The button beside it that changes the colony is left to the ordinary walk, which
        /// already names it from its own tooltip.
        /// </summary>
        private static Cell GrowthSourceCell(
            AgeTransform widget,
            OutpostInfoSidePanel panel,
            string keyPrefix
        )
        {
            OutpostInfoSidePanel it = panel;
            AgeTooltip tooltip = AgeWidgets.Raw(panel.ColonyGroup);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.GrowthSourceName)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, panel.ColonyGroup);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, keyPrefix + widget.name + "/growth-source"),
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
            AgeTooltip iconTooltip = AgeWidgets.Raw(
                approval.HappinessIcon == null ? null : approval.HappinessIcon.AgeTransform
            );
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ApprovalName(owner)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.HappinessValueLabel)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.HappinessStatusLabel)),
                },
                // Two tooltips on one row, in the order they are drawn: the icon's one-line gloss on
                // what Approval is, which is reviewed and not spoken (the row is walked past on the way
                // to everything below it, and its own words already say what it is), and the row's
                // renderer-assembled dossier, which is indicated by the ordinary rule.
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(iconTooltip, TooltipMode.None),
                    GraphNodes.TooltipSection(tooltip)
                ),
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
                // The kind tooltip is a single sentence and that sentence is already the row's NAME, so
                // it is reviewed and not said again; the wait's own tooltip is a second thing the panel
                // says, and reads by the ordinary rule.
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(kind, TooltipMode.None),
                    GraphNodes.TooltipSection(when)
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, keyPrefix + widget.name + "/growth"),
                Vtable = vtable,
            };
        }

        /// <summary>
        /// How many outposts this colony is feeding. The game draws the number alone beside a symbol
        /// and writes no title for the row anywhere in its corpus - the only words about it are the
        /// sentence on the row's own tooltip, which names the outposts and so belongs to the row as its
        /// detail rather than as its name. So the count is said in the mod's own counted phrase and the
        /// game's sentence follows it under the ordinary tooltip rule.
        ///
        /// The number comes from the system the panel is showing, not from the digits on the label: the
        /// label is the count already turned into text for the eye, and the model is what a phrase that
        /// has to choose a plural form needs.
        /// </summary>
        private static Cell OutpostsCell(
            AgeTransform widget,
            ColonyPopulationSidePanel panel,
            string keyPrefix
        )
        {
            ColonyPopulationSidePanel it = panel;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => OutpostsSupplied(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, keyPrefix + widget.name + "/outposts"),
                Vtable = vtable,
            };
        }

        private static string OutpostsSupplied(ColonyPopulationSidePanel panel)
        {
            try
            {
                ColonizedStarSystem system = panel == null ? null : panel.ColonizedStarSystem;
                int count = system == null ? 0 : system.OutpostMigrationDestinationSystems.Count;
                return count <= 0
                    ? null
                    : ModStrings.Plural(
                        ModStrings.SystemSupplyingOutpost,
                        ModStrings.SystemSupplyingOutposts,
                        count
                    );
            }
            catch (Exception)
            {
                return null;
            }
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
                // The graph's tooltip opens with the sentence that is already the row's NAME and then
                // says what the sensitivity is for, so announcing it would read the name twice: it is
                // indicated instead, which still tells the player there is more here and puts all of it
                // in the buffer above the bars.
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(tooltip, TooltipMode.Indicate),
                    NodeSection.Buffer(() => SensitivityDetails(it))
                ),
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

        private static IList<string> SensitivityDetails(RepresentativesStarSystemSidePanel panel)
        {
            List<string> lines = new List<string>();
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

        /// <summary>
        /// The two arrows the game draws either side of the system's name, which walk the empire's own
        /// colonised systems (<c>StarSystemScreen.CycleStarSystemHelper</c> :180-197). They are declared
        /// with the name rather than in a stop of their own because that is where the game puts them,
        /// and <see cref="Cells.EmitLinear"/> takes the reading order off the rectangles - so previous,
        /// the name, next comes out in the order the player sees.
        ///
        /// The game gives the arrows no title at all, only a sentence in each one's own tooltip, so the
        /// mod names them the way it names the planet page's pair - and each name ends with the chord
        /// that does the same thing from anywhere on the page, since the whole point of declaring the
        /// buttons is that a player who found one has found the gesture too.
        ///
        /// They belong to the colony panel, which the game binds for a colony, an outpost and a ghost
        /// alike (<c>StarSystemScreen.BindStarSystemNode</c> :555-560) - the same condition under which
        /// it draws the arrows at all.
        /// </summary>
        private static void AddSystemPaging(List<Cell> cells)
        {
            StarSystemScreen window = Window();
            if (window == null)
            {
                return;
            }

            AddSystemPage(
                cells,
                window.PreviousSystemButton,
                "system:previous",
                ModStrings.SystemPrevious,
                UiActions.PagePrev
            );
            AddSystemPage(
                cells,
                window.NextSystemButton,
                "system:next",
                ModStrings.SystemNext,
                UiActions.PageNext
            );
        }

        private static void AddSystemPage(
            List<Cell> cells,
            AgeControlButton button,
            string key,
            string nameKey,
            string actionKey
        )
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlButton it = button;
            AgeTransform host = widget;
            string named = nameKey;
            string action = actionKey;
            NodeVtable vtable = GraphNodes.Button(
                () => ChordNames.Label(ModStrings.Get(named), action, 0),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(host),
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.Point(vtable, it);
            Add(cells, widget, ControlId.Referenced(button, key), vtable);
        }

        // ---- shared ----

        private static void Add(List<Cell> cells, AgeTransform widget, ControlId id, NodeVtable vtable)
        {
            Cells.Add(cells, widget, id, vtable);
        }

        /// <summary>
        /// A line of the panel that the player reads rather than works. <paramref name="tooltip"/> is
        /// for the readouts whose tooltip the panel does NOT hang on the widget the number is drawn in -
        /// it keeps it in a field of its own and puts it on the group around the number - and it is the
        /// pointer's target too, because the game draws a tooltip for the widget that owns it and
        /// pointing at the number would draw nothing and leave the review buffer empty.
        /// </summary>
        private static void AddReadout(
            List<Cell> cells,
            AgeTransform widget,
            string key,
            Func<string> label,
            Func<string> value = null,
            AgeTooltip tooltip = null
        )
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTooltip tip = tooltip ?? AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(label) },
                Sections = GraphNodes.Sections(null, tip),
            };
            if (value != null)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(value));
            }

            AgeWidgets.PointAt(vtable, widget, tip);
            Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        private static void AddWidgetLines(
            List<string> lines,
            AgeTransform widget,
            Func<AgeTransform, bool> skip = null
        )
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            IList<AgeTransform> children = widget.Children;
            if (children == null || children.Count == 0)
            {
                AddLine(lines, AgeWidgets.ItemText(widget));
                return;
            }

            // A table of things - the traits, the anomalies - reads one line per thing, which is how it
            // is drawn and how it is reviewed. What each item SAYS, not the text on it: a findings table
            // is a row of bare icons, and reading it as text read nothing at all.
            //
            // These tables are POOLED (ReserveChildren + RefreshChildrenIList), so the CHILD is asked
            // the engine's own drawing test rather than the visibility flag a retired item keeps - the
            // same rule and the same reason as SidePanels.Collect. The entry gate above stays the
            // visibility chain, because a table that is itself fading in still has content to read.
            for (int i = 0; i < children.Count; i++)
            {
                if (AgeWidgets.Paints(children[i]) && (skip == null || !skip(children[i])))
                {
                    AddLine(lines, AgeWidgets.ItemText(children[i]));
                }
            }
        }

        /// <summary>A table item the card offers as a button of its own, and so is not a line of the
        /// card's - the curiosities the game mixes into the findings table.</summary>
        private static bool SkipCuriosities(AgeTransform item)
        {
            try
            {
                return item != null && item.GetComponent<PlanetCuriosityItem>() != null;
            }
            catch (Exception)
            {
                return false;
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
            return CardActions.FirstLine(tooltip);
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

        /// <summary>What every node this screen declares is keyed under, so the tooltip
        /// audit can tell its content from the shared heads-up display stops.</summary>
        public override string NodePrefix
        {
            get { return "system:"; }
        }
    }
}

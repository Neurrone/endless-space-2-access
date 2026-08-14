using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
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
    /// The galaxy - the page the whole game is played from - made navigable.
    ///
    /// It is not a window. What the player calls "the map" is the galaxy itself with three unrelated
    /// windows drawn over its corners, and no one of them owns the page, so this screen is assembled
    /// from all of them plus the galaxy model underneath. Being ours is therefore not "is a window
    /// up" but "is the game showing the galaxy and nothing over it": a full screen (technology,
    /// diplomacy) or a modal replaces the page and this screen stands down, while a notification or
    /// the tutorial popup merely covers a corner of it and does not - they sit on higher layers, and
    /// a covered screen keeps its cursor, so dismissing one puts the player back where they were.
    ///
    /// Tab moves between the places there are to be, in the order the corner of the screen they come
    /// from reads: what the empire is worth, where the systems are, the quest the game is tracking,
    /// what the game is asking about, and what the turn itself offers.
    ///
    /// There is no separate stop for the fleets. A fleet is not somewhere else on the screen: it is
    /// drawn AT a system or ON a lane, and that is where it is walked - as a child of the place it is
    /// standing, after that place's planets and starlanes. A list of every fleet in the empire, in a
    /// corner of its own, described a picture the map does not draw and made "where is it" a question
    /// the player had to answer from a sentence rather than from the tree they were already in. This
    /// is a DELIBERATE deviation from the game's own arrangement, approved as such: the map draws
    /// fleets as lozenges anchored to places, and the tree now says the same thing.
    ///
    /// A tutorial the player has collapsed is one of those places. Collapsing it hands the keyboard
    /// back to this page, and the bar the game leaves at the top of the right-hand edge - its title,
    /// its close button, the arrow that brings it back - is declared here, above the notification icons
    /// drawn under it, by the tutorial screen that knows how to read it.
    ///
    /// A stop exists on a frame only if the game is showing something at it. There are no placeholders:
    /// where the game draws nothing for having no fleets and nothing for having no notifications, Tab
    /// finds nothing there either, because a stop that says "nothing" is a stop the player has to walk
    /// past to learn what a glance at the screen would have told them.
    ///
    /// Everything is read from the game's own model rather than from the labels on the banners. Every
    /// number up there is animated - the dust total counts up to its new value over a second or so -
    /// so the labels are a picture of a number on its way somewhere, and the model is the number.
    ///
    /// The empire stop is laid out in the ROWS the cluster is drawn in - the strip of screen icons, the
    /// running totals under it, the research line, the stockpiles - and up and down move between them
    /// while left and right walk along one. The rows are measured rather than declared, so a stockpile
    /// strip the engine wraps onto a second line is walked as two lines without anything being told.
    ///
    /// The totals and the stockpiles are readable but not activatable: they are the only way a keyboard
    /// player learns what the empire is worth, and the game's own click handlers on them do nothing
    /// outside its debug mode. Focus still makes the game draw their tooltips, which is where the stat
    /// block behind each number lives - and where the review buffer reads it from.
    ///
    /// The screen icons ARE activatable, and none of the screens they open is navigable yet: opening
    /// one stands this screen down and leaves the player with the game's own keys until Escape closes
    /// it again. That is a worse place to be than here, and still better than not being told the screen
    /// exists.
    ///
    /// Focusing a system slides the camera across to it exactly as the game's own "show me this" routes
    /// do, so that anyone watching the screen is looking at whatever the keyboard is on. Opening one up -
    /// right arrow, the same key that opens anything else - walks what the map is drawing inside it: its
    /// planets, then the starlanes leaving it, then the fleets standing there. And it brings the camera
    /// all the way IN, because that is what makes the map draw the things the branch has just started
    /// reading out: from far off a planet is a circle with a name and a state, and from as close as the
    /// game goes it is a card with its outputs, its anomalies and everything a fleet could do to it. So
    /// the distance follows the branch rather than being a separate thing to remember - going in and
    /// looking closer are the same gesture, and closing the branch takes the camera back out again while
    /// the camera is still there to take out. ENTER still asks for the same distance on its own, because
    /// it is the game's own left click on a system, and BACKSLASH is still the map's own right click.
    ///
    /// The far end of a starlane opens the same way. A lane that leads somewhere the map has drawn the
    /// name of offers that system as a child, and opening it goes there and reads what is in it - so "what
    /// is down this line" is answered where the player is standing rather than by walking back to a list of
    /// a hundred systems.
    ///
    /// Backslash is what the map puts on a right click. On a system with fleets selected it sends them
    /// there; with nothing selected it undoes a zoom the player asked for, exactly as right-clicking
    /// the map does. On a starlane it sends the selection out onto the lane - and onto the lane a
    /// fleet is already flying, which is the game's own way of saying "stop at the next system".
    ///
    /// A starlane says where it goes only when the map draws the name of the system at the other end.
    /// The game's own galaxy model will happily hand over the name of a system nobody has ever seen -
    /// asking a node for its neighbours returns them whether or not they have been discovered - so
    /// every name spoken here is gated on the same question the map asks before it draws one.
    ///
    /// The new turn is announced without anyone being on the End Turn button, because the turn
    /// changing is the one thing on this page that happens to the player rather than being done by
    /// them.
    /// </summary>
    public sealed class GalaxyHudScreen : Screen
    {
        private static readonly object SystemStop = "galaxy:systems";

        /// <summary>The clusters the game draws over every view level - what the empire is worth, the
        /// notifications, a collapsed tutorial, the turn controls. This page is one of three that
        /// declare them.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

        /// <summary>How close the camera is looking, offered beside the name of the view because that is
        /// where the game writes what the player is looking AT. The same control the scan view carries
        /// (<see cref="ZoomLadder"/>) - here the top of its ladder crosses out of this page, into the
        /// system's own and then a planet's, which is exactly what the map's own wheel does.</summary>
        private readonly ZoomLadder _zoom = new ZoomLadder();

        /// <summary>The strip the game slides over the bottom of the map while a fleet is selected.
        /// It is drawn OVER this page rather than instead of it, so it contributes stops here rather
        /// than being a page of its own - selecting a fleet is how a player starts sending one
        /// somewhere, and the somewhere is on this map.</summary>
        private readonly FleetPanel _fleetPanel = new FleetPanel();

        // Regions - what Alt and an arrow jump between - are declared only where a stop really has
        // two halves. A stop with one region swallows the key and moves nothing, which reads as the
        // key being broken rather than as there being nowhere else to go. The empire stop declares
        // none: its parts are its rows, and up and down already walk them.
        private static readonly object OwnedSystemsRegion = "galaxy:systems/owned";
        private static readonly object OtherSystemsRegion = "galaxy:systems/other";

        /// <summary>The third of the map's halves: what the game draws OUT BETWEEN the stars - a probe
        /// drifting, a planet-killer crossing, a pin an ally has dropped. None of them stands at a place
        /// (<see cref="Drifting"/>), so none of them can hang under one, and they are jumped to as a
        /// region of their own rather than being left at the end of a list of a hundred systems.</summary>
        private static readonly object OpenSpaceRegion = "galaxy:systems/space";

        /// <summary>How far up a parent chain to look before deciding it is not a chain.</summary>
        private const int MaxAncestors = 64;

        // Reused across builds rather than allocated per frame: the galaxy is walked whole to work
        // out which systems the player can see, and Build runs every tick.
        private readonly List<StarSystemNode> _owned = new List<StarSystemNode>();
        private readonly List<StarSystemNode> _other = new List<StarSystemNode>();

        // The same, for the three things the map draws away from any star. Each holds the LABEL rather
        // than the entity, because the label is what answers "is the game drawing this".
        private readonly List<ProbeLabel> _probes = new List<ProbeLabel>();
        private readonly List<ObliteratorProjectileLabel> _projectiles =
            new List<ObliteratorProjectileLabel>();
        private readonly List<CoordinationRequestLabel> _pins =
            new List<CoordinationRequestLabel>();

        public override string Key
        {
            get { return "screen.galaxy"; }
        }

        /// <summary>Above nothing - it is the bottom of the game - and below everything drawn over it:
        /// the tutorial popup, the notifications, the confirmation box.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        /// <summary>The game has no caption for the galaxy; it is simply where you are when nothing
        /// else is open.</summary>
        public override string ScreenName
        {
            get { return ModStrings.Get(ModStrings.ScreenGalaxy); }
        }

        /// <summary>The page the player returns to after every screen, so coming back puts them on the
        /// control they left rather than at the top.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>The first of the stops, because Tab walks them forwards and stops at the end rather
        /// than coming round again: a cursor seated at the last stop would make the key look broken on
        /// the one screen a player arrives at before they have learnt anything else. It is also what the
        /// player wants read out on arriving - what the empire is worth.</summary>
        public override object InitialFocusStop
        {
            get { return GlobalHud.EmpireStop; }
        }

        /// <summary>
        /// Ours while the game is showing the galaxy from above and nothing has replaced it. "Normal
        /// view" is the game's own name for half of that: a game that is ready, not in a battle, not in
        /// the scan overlay, not watching a system be discovered or a planet destroyed. A full screen
        /// or a modal on top replaces the page rather than covering it, so those stand this screen
        /// down; a loading window means the page is on its way out.
        ///
        /// The other half is the view level. Taking the camera into a system's management page leaves
        /// "normal view" true while putting a completely different set of things in front of the
        /// player, so this page has to give the keyboard up there rather than go on describing systems
        /// that are no longer on the screen.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                GuiManager gui = GuiService();
                return gui != null
                    && gui.IsInNormalView
                    && GalaxyViewLevels.Overview
                    && !gui.IsAnyScreenVisible
                    && !gui.IsAnyModalVisible
                    && !gui.IsInLoadingWindow;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape belongs to the game: from the galaxy it raises the pause menu, and that is
        /// the route every other way out of a game goes through. The one exception is the mode the game
        /// itself left without an Escape, where the key would raise the pause menu OVER a map still
        /// waiting for a target (<see cref="CursorTargeting.EscapeIsOurs"/>).</summary>
        public override bool Back()
        {
            return CursorTargeting.EscapeIsOurs && CursorTargeting.Contextual();
        }

        /// <summary>Asked before the key is pressed, and true only in that same one case - otherwise the
        /// game must keep the Escape it answers itself, mode or no mode.</summary>
        public override bool ConsumesBack
        {
            get { return CursorTargeting.EscapeIsOurs; }
        }

        /// <summary>
        /// While the map is waiting for an order's target, backslash is the map's own right click and
        /// nothing else - the cancel for most of these modes, one waypoint back while a hacking operation
        /// is being plotted (<see cref="CursorTargeting.Contextual"/>). The same displacement Enter lives
        /// with: for as long as the mode is up, sending the selected fleets and undoing a zoom
        /// (<see cref="SystemCommand"/>) wait, exactly as they do for the mouse.
        /// </summary>
        public override bool Contextual()
        {
            return CursorTargeting.Contextual();
        }

        public override void OnPush()
        {
            _hud.Baseline();
            _fleetPanel.Baseline();
        }

        public override void OnPop()
        {
            _zoom.Forget();
            _hud.Forget();
            _fleetPanel.Forget();
            // A place the game asked to be looked at goes with the page: whatever replaced this one is
            // where the player now is, and a request answered on some later visit would move the cursor
            // for a reason nobody could remember.
            GalaxyLocate.Forget();
            _locating = null;
        }

        public override void OnUpdate()
        {
            _hud.Update();
            _fleetPanel.Update();
            _zoom.Update();
            // Before the camera is followed and before the graph is next built, so that the landing
            // and the branch it opens both happen on the frame the page arrives on.
            FollowTheGame();
            FollowCamera();
        }

        /// <summary>
        /// Keep the game's own pointer feedback pointed at whatever the map is drawing for the focused
        /// system NOW.
        ///
        /// Which tooltip a focused system asks the game to draw depends on where the camera is - the
        /// label's while the label is what the map draws, the orbital window's once the camera is in on
        /// the system (<see cref="AddSystem"/>) - and the camera moves without focus moving at all:
        /// Enter zooms, and so does opening a system up. The focus visual is committed once per focus
        /// CHANGE, so nothing would ever ask again: the tooltip stayed aimed at a label the map had
        /// meanwhile pushed off the top of the screen, and the game went on drawing the system's dossier
        /// clamped into a corner beside nothing.
        ///
        /// So the camera's own answer is watched and the visual dropped when it changes. The navigator
        /// re-commits it later in the same frame, which is why nothing flickers and no tooltip is closed
        /// and reopened (<c>ScreenManager.Tick</c> runs OnUpdate before EnsureFocus, and
        /// <c>PointerFocus.Tick</c> after both).
        /// </summary>
        private void FollowCamera()
        {
            StarSystemNode focused = GalaxyViewLevels.FocusedSystem;
            bool orbital = OrbitalWindow() != null;
            if (ReferenceEquals(focused, _cameraSystem) && orbital == _cameraOrbital)
            {
                return;
            }

            _cameraSystem = focused;
            _cameraOrbital = orbital;
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator != null)
            {
                navigator.ClearVisual();
            }
        }

        /// <summary>What the camera was showing last frame - the two things the choice of a focused
        /// system's tooltip is made from.</summary>
        private StarSystemNode _cameraSystem;

        private bool _cameraOrbital;

        /// <summary>Whether the focus visual being committed right now is the cursor having MOVED, as
        /// opposed to this page being re-entered or the visual being re-taken where it already was.
        /// Everything on this page that moves the CAMERA asks it first
        /// (<see cref="GraphNavigator.CursorMovedHere"/>).</summary>
        private static bool CursorMoved()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            return navigator != null && navigator.CursorMovedHere;
        }

        // ---- where the game has just sent the player ----

        /// <summary>The request being worked on, so that a page which needs several frames to find its
        /// answer is spending ONE budget on ONE request rather than restarting the clock.</summary>
        private GalaxyLocate.Request _locating;

        private int _locateFrames;

        /// <summary>About a second: long enough for the places to have been declared once on a page
        /// arrived at cold AND for the camera slide the game starts with the request to finish, which is
        /// when it says which of a berth's fleets it meant. Short enough that a request nothing ever
        /// answers gives up rather than seizing a later frame's cursor. It is only ever spent on an
        /// answer that can still improve - a settled one lands the frame it is found.</summary>
        private const int LocateFrames = 60;

        /// <summary>How near a point has to be to something the tree declares before it IS that thing,
        /// in the galaxy's own units. Well under the closest two systems in a galaxy ever stand (6.7
        /// measured on the fixture, 10.6 on average), so nothing is ever mistaken for its neighbour,
        /// and wide enough for the small offsets the map draws a fleet's berth at.</summary>
        private const float LocateRadius = 3f;

        /// <summary>
        /// Put the cursor where the GAME has just sent the player (<see cref="GalaxyLocate"/>) - the
        /// same landing a search makes, so the place it is buried in is opened and the cursor is left on
        /// the thing itself.
        ///
        /// Here rather than on arrival because the two ways in are the same thing: the page opens
        /// showing the place, or it was already open and the camera moved. The next idle fleet is the
        /// second kind and the military screen's locate is the first, and neither is special.
        ///
        /// A request the tree has no node for is still answered - the camera HAS moved, and a player
        /// told nothing would be left reading a place the map is no longer showing.
        /// </summary>
        private void FollowTheGame()
        {
            GalaxyLocate.Request wanted = GalaxyLocate.Peek();
            if (wanted == null)
            {
                _locating = null;
                return;
            }

            if (!ReferenceEquals(wanted, _locating))
            {
                _locating = wanted;
                _locateFrames = LocateFrames;
                _holdingAtRequest = SelectedFleet();
            }

            try
            {
                bool settled;
                ControlId id = Locate(wanted, out settled);
                // The budget is for an answer that can still get better - a page that has not declared
                // anything yet, a berth the game has not yet said which of its fleets it meant. A
                // settled answer is given now, including the settled answer that nothing on the map is
                // there.
                if (!settled && --_locateFrames > 0)
                {
                    return;
                }

                GalaxyLocate.Take();
                _locating = null;
                Land(id, wanted);
            }
            catch (Exception e)
            {
                GalaxyLocate.Take();
                _locating = null;
                Log.Warn("galaxy: landing on what the game located threw: " + e);
            }
        }

        /// <summary>Whether the map has declared anything to land on yet. A page arrived at from a save
        /// being loaded has not run <see cref="BuildSystems"/> even once.</summary>
        private bool Declaring()
        {
            return _owned.Count > 0 || _other.Count > 0;
        }

        /// <summary>Speak what the landing needs saying beyond the node itself, then send the cursor.
        /// The node's own announcement is the whole of an ordinary landing - it names the place the game
        /// went to, which is the answer to the question that was asked.</summary>
        private void Land(ControlId id, GalaxyLocate.Request wanted)
        {
            if (wanted.Quest != null)
            {
                Voice.Say(QuestLocated(wanted.Quest), false);
            }
            else if (id == null)
            {
                Voice.Say(ModStrings.Get(ModStrings.GalaxyShownOnMap), false);
            }

            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator != null)
            {
                // Nothing on the map answers for the point, so the cursor goes to the name of the view -
                // where the player is told what they are looking at - rather than staying on a place the
                // camera has left.
                navigator.FocusNode(id ?? ControlId.Structural("hud:view-title/name"));
            }
        }

        /// <summary>The quest a pin belongs to, in the game's own title for it.</summary>
        private static string QuestLocated(Quest quest)
        {
            try
            {
                return ModStrings.Format(
                    ModStrings.GalaxyQuestShownOnMap,
                    AgeText.Clean(new GuiQuest(quest).Title)
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: naming the quest the game located threw: " + e);
                return null;
            }
        }

        /// <summary>The node for what the game asked to be looked at, or null where the map draws
        /// nothing there. The thing itself first, since a request that named one is exact; the point
        /// only where it did not, or where the thing it named is not something this tree declares.
        ///
        /// <paramref name="settled"/> is false while a later frame could answer better - the whole of
        /// what the frame budget is for.</summary>
        private ControlId Locate(GalaxyLocate.Request wanted, out bool settled)
        {
            ControlId named = FromEntity(wanted.Entity);
            if (named != null)
            {
                settled = true;
                return named;
            }

            return Nearest(wanted.Position, out settled);
        }

        /// <summary>The node for a thing the game named. A fleet is where the map draws it; everything
        /// that stands at a system - the system, a colony of it, a planet in it - is that system.
        /// </summary>
        private ControlId FromEntity(IGameEntityWithGalaxyPosition entity)
        {
            if (entity == null)
            {
                return null;
            }

            Fleet fleet = entity as Fleet;
            if (fleet != null)
            {
                List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
                for (int i = 0; i < sites.Count; i++)
                {
                    if (ReferenceEquals(sites[i].Fleet, fleet))
                    {
                        return Reveal(sites[i]);
                    }
                }

                return null;
            }

            ColonizedStarSystem colony = entity as ColonizedStarSystem;
            Planet planet = entity as Planet;
            GameNode node = colony != null
                ? colony.Node
                : planet != null
                    ? planet.StarSystemNode
                    : entity as GameNode;
            return SystemId(node as StarSystemNode);
        }

        /// <summary>A system's node id, but only while this page is declaring that system: the map
        /// draws the names of the systems the player has seen, and the tree says the same
        /// (<see cref="Perceived"/>).</summary>
        private ControlId SystemId(StarSystemNode node)
        {
            return node != null && (_owned.Contains(node) || _other.Contains(node))
                ? ControlId.Structural("galaxy:system/" + node.GUID)
                : null;
        }

        /// <summary>One thing the map is drawing, and where it is drawn.</summary>
        private struct Spot
        {
            public Vector3 At;

            /// <summary>The node, for everything but a fleet.</summary>
            public ControlId Id;

            /// <summary>Which fleet site this is, or -1. A fleet's node id is not made until it wins,
            /// because making one records a branch to open.</summary>
            public int Site;
        }

        /// <summary>
        /// The nearest thing the map draws to a point, or null when the point is out in the open.
        ///
        /// The order candidates are offered in is the tie-break (<see cref="NearestPick"/>), and one tie
        /// is exact rather than coincidental: a fleet parked at a system says its position IS that
        /// system's (<c>FleetPosition</c> sets it from the node), so a request aimed at a star would
        /// otherwise be answered by whichever fleet happens to be sitting there. Places first, then.
        /// A fleet is offered at its BERTH - the slot the map draws it in, which is beside the star
        /// rather than on it - so the one call that aims at a berth (the next-idle-fleet button) still
        /// picks the fleet out.
        /// </summary>
        private ControlId Nearest(Vector3 position, out bool settled)
        {
            // A page arrived at cold has declared nothing, and "nothing is there" would be a wrong
            // answer rather than a late one.
            settled = Declaring();
            List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
            List<Spot> spots = new List<Spot>(_owned.Count + _other.Count + sites.Count);
            for (int i = 0; i < _owned.Count; i++)
            {
                Add(spots, _owned[i].GalaxyPosition, SystemId(_owned[i]), -1);
            }

            for (int i = 0; i < _other.Count; i++)
            {
                Add(spots, _other[i].GalaxyPosition, SystemId(_other[i]), -1);
            }

            for (int i = 0; i < _probes.Count; i++)
            {
                Probe probe = _probes[i].Entity as Probe;
                if (probe != null)
                {
                    Add(
                        spots,
                        probe.GalaxyPosition,
                        ControlId.Structural("galaxy:probe/" + probe.GUID),
                        -1
                    );
                }
            }

            for (int i = 0; i < _projectiles.Count; i++)
            {
                ObliteratorProjectile shot = _projectiles[i].Entity as ObliteratorProjectile;
                if (shot != null)
                {
                    Add(
                        spots,
                        shot.GalaxyPosition,
                        ControlId.Structural("galaxy:projectile/" + shot.GUID),
                        -1
                    );
                }
            }

            for (int i = 0; i < _pins.Count; i++)
            {
                CoordinationRequest pin = _pins[i].CoordinationRequest;
                if (pin != null)
                {
                    Add(spots, pin.GalaxyPosition, ControlId.Structural("galaxy:pin/" + pin.GUID), -1);
                }
            }

            for (int i = 0; i < sites.Count; i++)
            {
                Add(spots, Berth(sites[i].Fleet), null, i);
            }

            NearestPick pick = new NearestPick(LocateRadius);
            for (int i = 0; i < spots.Count; i++)
            {
                pick.Offer(i, (spots[i].At - position).sqrMagnitude);
            }

            if (!pick.Found)
            {
                return null;
            }

            Spot won = spots[pick.Index];
            if (won.Site < 0)
            {
                return won.Id;
            }

            bool holding;
            ControlId fleet = Reveal(sites[Holding(sites, won, out holding)]);
            settled &= holding;
            return fleet;
        }

        private static void Add(List<Spot> spots, Vector3 at, ControlId id, int site)
        {
            if (id != null || site >= 0)
            {
                spots.Add(new Spot { At = at, Id = id, Site = site });
            }
        }

        /// <summary>Where the map draws a fleet: its berth in the system's docking slot while it is
        /// parked there, and its own position while it is out on a lane. The two are different questions
        /// because a parked fleet's position is the STAR's, and a request aimed at the berth is a
        /// request for the fleet.</summary>
        private static Vector3 Berth(Fleet fleet)
        {
            try
            {
                IVisibleDockingSlotRepositoryService slots =
                    Amplitude.Unity.Framework.Services.GetService<IVisibleDockingSlotRepositoryService>();
                DockingSlotCursorTarget slot = slots == null
                    ? null
                    : slots.GetDockingSlotWithFleet(fleet);
                return slot == null ? (Vector3)fleet.GalaxyPosition : slot.transform.position;
            }
            catch (Exception)
            {
                return fleet.GalaxyPosition;
            }
        }

        /// <summary>
        /// Which of the fleets sharing one berth was meant.
        ///
        /// A point cannot say: every fleet parked at a system is drawn in the same slot. The game says
        /// it by SELECTING the fleet - which it does a few frames later, once the camera it started
        /// moving has arrived (<c>EndTurnWindow.SelectFleetWhenViewReady</c>), so the selection standing
        /// at the moment of the request is the previous answer and not this one. Hence
        /// <paramref name="settled"/>: while the berth holds several fleets and the game has not yet
        /// changed its mind, the first of them is a provisional answer and the budget is spent waiting
        /// for a better one. A berth with one fleet in it has nothing to wait for.
        /// </summary>
        private int Holding(List<FleetSite> sites, Spot won, out bool settled)
        {
            Fleet selected = SelectedFleet();
            int sharing = 0;
            int chosen = -1;
            for (int i = 0; i < sites.Count; i++)
            {
                if ((Berth(sites[i].Fleet) - won.At).sqrMagnitude >= 0.0001f)
                {
                    continue;
                }

                sharing++;
                if (ReferenceEquals(sites[i].Fleet, selected))
                {
                    chosen = i;
                }
            }

            settled = sharing <= 1 || (chosen >= 0 && !ReferenceEquals(selected, _holdingAtRequest));
            // The fleet the game is holding is the better guess even unsettled, which is what the
            // budget running out falls back to.
            return chosen >= 0 ? chosen : won.Site;
        }

        /// <summary>What the game's cursor was holding when the request arrived, so that a selection
        /// made in ANSWER to it can be told from the one that was already standing.</summary>
        private Fleet _holdingAtRequest;

        /// <summary>The fleet the map's own cursor is holding, or null while it is holding nothing. The
        /// cursor is half the answer: the garrison cursor is what "a fleet is selected" means to this
        /// game, and the panel's own visibility is gated on the same thing (es2-facts).</summary>
        private static Fleet SelectedFleet()
        {
            try
            {
                if (!(Gui.GetCursor() is GalaxyGarrisonCursor))
                {
                    return null;
                }

                List<Fleet> selected = FleetOrders.Selected();
                return selected.Count == 0 ? null : selected[0];
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: asking what the cursor is holding threw: " + e);
                return null;
            }
        }

        /// <summary>Down the screen, which is also the order the galaxy screen has always read in:
        /// the empire's banners across the top and the name of the view beside them, then what the map
        /// is showing, then the right-hand
        /// edge - the bar a collapsed tutorial leaves at the top of it and the notification icons
        /// under that - and the turn controls in the bottom corner.</summary>
        public override void Build(GraphBuilder builder)
        {
            ApplyPendingExpansions(builder);
            _hud.Empire(builder);
            // Under the name of the view rather than beside it: the two the game draws there are a row,
            // and left and right on the zoom are the zoom's own keys - a slider in that row would take
            // the only keys that walk it.
            if (_hud.ViewTitle(builder))
            {
                _zoom.Build(builder, "hud:view-title/zoom");
            }

            builder.BeginStop(SystemStop);
            BuildSystems(builder);

            // The selected-fleet panel, where the game draws it: over the bottom of the map, between
            // what the map shows and the clusters down its right-hand edge. Nothing at all while no
            // fleet is selected.
            _fleetPanel.Build(builder);

            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.Turn(builder);
        }

        /// <summary>
        /// Typing on the map looks through the systems AND every fleet the map is drawing, wherever
        /// each is buried.
        ///
        /// A fleet lives under the place it is standing now, and that place is usually closed - so the
        /// only thing the ordinary scope (the stop's declared controls) could find is a fleet whose
        /// system the player had already opened, which is not a search, it is a confirmation. Landing
        /// on one opens the place it is in, so the branch the player is put into is the branch they can
        /// then walk. The opening is recorded rather than done: the graph is rebuilt between this call
        /// and the focus landing, and the expansion set belongs to that rebuild.
        ///
        /// Everything the stop already declares stays searchable - this EXTENDS the ordinary scope
        /// rather than replacing it, so a planet or a starlane of an open system is still found by
        /// name.
        /// </summary>
        public override SearchScope TypeAheadScope(GraphNode focused, GraphRender render)
        {
            if (focused == null || !Equals(focused.StopKey, SystemStop))
            {
                return null;
            }

            // Only the fleets the stop has NOT already declared: an open system declares its own, and a
            // fleet offered twice would be two results with one name, which stepping the matches walks
            // through twice.
            List<FleetSite> sites = FleetIndex(Declared(render));
            if (sites.Count == 0)
            {
                return null;
            }

            SearchScope basis = SearchScope.OverStop(render, SystemStop);
            int already = basis.Count;
            List<FleetSite> found = sites;
            GalaxyHudScreen screen = this;
            return new SearchScope(
                already + found.Count,
                index =>
                    index < already
                        ? basis.TextOf(index)
                        : found[index - already].Fleet.LocalizedName,
                index =>
                    index < already ? basis.Land(index) : screen.Reveal(found[index - already])
            );
        }

        /// <summary>One fleet and the branches that have to be open before it is a node: the system it
        /// is parked at or whose lane it is flying, and - for a lane - the lane itself.</summary>
        private struct FleetSite
        {
            public Fleet Fleet;
            public ControlId System;
            public ControlId Lane;
            public string Key;
        }

        /// <summary>Every fleet the map is drawing and the place each is drawn at, minus the ones the
        /// graph already holds. Built on demand: both repositories are walked once per lane, and the
        /// only thing that ever wants this is one keystroke.</summary>
        private List<FleetSite> FleetIndex(HashSet<ControlId> declared)
        {
            List<FleetSite> sites = new List<FleetSite>();
            try
            {
                for (int i = 0; i < _owned.Count; i++)
                {
                    IndexPlace(_owned[i], sites, declared);
                }

                for (int i = 0; i < _other.Count; i++)
                {
                    IndexPlace(_other[i], sites, declared);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: indexing the fleets for a search threw: " + e);
            }

            return sites;
        }

        /// <summary>What the graph is holding right now, so that nothing is offered to a search twice.
        /// </summary>
        private static HashSet<ControlId> Declared(GraphRender render)
        {
            HashSet<ControlId> ids = new HashSet<ControlId>();
            if (render != null)
            {
                foreach (GraphNode node in render.Order)
                {
                    ids.Add(node.Id);
                }
            }

            return ids;
        }

        /// <summary>The fleets one system holds, and the ones out on each of its lanes. A lane is
        /// reached from both of its ends, so a fleet on one is indexed twice - which costs a duplicate
        /// result and is the right way round: either end is a true answer to "where is it", and
        /// dropping one would make the answer depend on which system the search happened to reach
        /// first.</summary>
        private static void IndexPlace(
            StarSystemNode node,
            List<FleetSite> sites,
            HashSet<ControlId> declared
        )
        {
            string systemKey = "galaxy:system/" + node.GUID;
            ControlId system = ControlId.Referenced(node, systemKey);
            Index(FleetPresence.FleetsAt(node), system, null, systemKey, sites, declared);
            for (int i = 0; i < node.Links.Count; i++)
            {
                Link link = node.Links[i];
                IList<Fleet> flying = FleetPresence.FleetsOn(link);
                if (flying.Count == 0)
                {
                    continue;
                }

                string laneKey = systemKey + "/lane/" + link.GUID;
                Index(
                    flying,
                    system,
                    ControlId.Referenced(link, laneKey),
                    laneKey,
                    sites,
                    declared
                );
            }
        }

        private static void Index(
            IList<Fleet> fleets,
            ControlId system,
            ControlId lane,
            string key,
            List<FleetSite> sites,
            HashSet<ControlId> declared
        )
        {
            for (int i = 0; i < fleets.Count; i++)
            {
                ControlId id = ControlId.Structural(key + "/fleet/" + fleets[i].GUID);
                if (declared.Contains(id))
                {
                    continue;
                }

                sites.Add(
                    new FleetSite
                    {
                        Fleet = fleets[i],
                        System = system,
                        Lane = lane,
                        Key = key,
                    }
                );
            }
        }

        /// <summary>Open the place a fleet is standing in and answer with the fleet itself. The opening
        /// is recorded rather than done: the expansion set belongs to the next rebuild.</summary>
        private ControlId Reveal(FleetSite site)
        {
            _pendingExpand.Add(site.System);
            if (site.Lane != null)
            {
                _pendingExpand.Add(site.Lane);
            }

            return ControlId.Structural(site.Key + "/fleet/" + site.Fleet.GUID);
        }

        /// <summary>Open the branches a search landed in. The expansion set is the engine's, and this is
        /// the one moment a screen has anything to say about it.</summary>
        private void ApplyPendingExpansions(GraphBuilder builder)
        {
            if (_pendingExpand.Count == 0)
            {
                return;
            }

            HashSet<ControlId> expansion = builder.Expansion;
            if (expansion != null)
            {
                for (int i = 0; i < _pendingExpand.Count; i++)
                {
                    expansion.Add(_pendingExpand[i]);
                }
            }

            _pendingExpand.Clear();
        }

        /// <summary>The groups a search has asked to be opened, applied on the next build.</summary>
        private readonly List<ControlId> _pendingExpand = new List<ControlId>();

        // ---- systems ----

        /// <summary>
        /// The star systems the player can see, their own colonies first.
        ///
        /// Which ones those are is the same question the map asks when it decides whether to draw a
        /// system's name: explored at least once, and either remembered or currently in sight. Asking
        /// it the same way is what keeps this list and the map showing the same galaxy.
        ///
        /// The two halves are declared as regions to jump between only while there really are two of
        /// them. On turn one there is one colony and nothing else has been seen yet, and a lone region
        /// makes Alt and an arrow swallow the key and move nothing - which sounds like the key being
        /// broken rather than like there being nowhere else to go.
        /// </summary>
        private void BuildSystems(GraphBuilder builder)
        {
            try
            {
                Empire empire = PlayerEmpire();
                Galaxy galaxy = Gui.Game == null ? null : Gui.Game.Galaxy;
                if (empire == null || galaxy == null)
                {
                    return;
                }

                _owned.Clear();
                _other.Clear();
                DepartmentOfTheInterior interior = empire.GetAgency<DepartmentOfTheInterior>();
                if (interior != null)
                {
                    foreach (ColonizedStarSystem colony in interior.ColonizedStarSystems)
                    {
                        // An empire can hold more than one thing in the same system - a colony and a
                        // ghost of it - and the system is still one place on the map.
                        if (colony.Node != null && !_owned.Contains(colony.Node))
                        {
                            _owned.Add(colony.Node);
                        }
                    }
                }

                foreach (StarSystemNode node in galaxy.StarSystemNodes)
                {
                    if (!_owned.Contains(node) && Perceived(node, empire))
                    {
                        _other.Add(node);
                    }
                }

                Drifting();
                int drifting = _probes.Count + _projectiles.Count + _pins.Count;
                // A region jump with one region to jump to swallows the key and moves nothing, which
                // sounds like the key being broken - so the map declares its halves only while it
                // really has more than one of them, whichever ones those are.
                bool split =
                    (_owned.Count > 0 ? 1 : 0)
                        + (_other.Count > 0 ? 1 : 0)
                        + (drifting > 0 ? 1 : 0)
                    > 1;
                if (split && _owned.Count > 0)
                {
                    builder.SetRegion(OwnedSystemsRegion);
                }

                // Fetched once for the whole stop rather than once per system: the labels the map
                // draws are pooled by the window, not rebuilt per frame, so one walk of the label
                // window serves every system this build declares.
                StarSystemLabel[] labels = SystemLabels();

                for (int i = 0; i < _owned.Count; i++)
                {
                    AddSystem(builder, _owned[i], empire, true, labels);
                }

                if (split && _other.Count > 0)
                {
                    builder.SetRegion(OtherSystemsRegion);
                }

                for (int i = 0; i < _other.Count; i++)
                {
                    AddSystem(builder, _other[i], empire, false, labels);
                }

                if (split && drifting > 0)
                {
                    builder.SetRegion(OpenSpaceRegion);
                }

                AddProbes(builder);
                AddProjectiles(builder);
                AddPins(builder);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the systems threw: " + e);
            }
        }

        /// <summary>The map's own rule for whether a node's name is drawn: it has been explored, and
        /// it is either remembered or in sight now. Everything this screen ever says the name of is
        /// asked this first.
        ///
        /// The exploration threshold is the CURSOR TARGET's, and the game sets it one step higher for a
        /// special node - the Academy, a quest site - than for an ordinary star system: 3 against 2
        /// (<c>GalaxySpecialNodeCursorTarget.VisibleByCurrentEmpire</c> :22-27 against
        /// <c>GalaxyStarSystemCursorTarget</c>'s :89-94, which the special one overrides). A
        /// <c>SpecialNode</c> IS a <c>StarSystemNode</c>, so one threshold for both would have named
        /// these places a whole exploration step before the map draws anything a mouse can point at.
        /// </summary>
        private static bool Perceived(GameNode node, Empire empire)
        {
            try
            {
                if ((int)node.Exploration[empire] < (node is SpecialNode ? 3 : 2))
                {
                    return false;
                }

                EntityVisibility.Layer layer = node.Visibility[empire];
                return layer == EntityVisibility.Layer.Known || (int)layer >= 3;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// One system on the map: what it is called, whether it is yours, and - once opened - what the
        /// map draws inside its label.
        ///
        /// Enter is the game's own left click on a system: it brings the camera all the way in, to the
        /// step at which the map stops drawing circles and draws a card in orbit for every planet.
        /// Backslash is the right click: with fleets selected it sends them here, and with none it puts
        /// the camera back where the zoom took it from.
        ///
        /// The page a colony of yours has of its own is on neither key. The map draws a button for it
        /// on the system's own label, beside the name, and that button is a node here - so the player
        /// reaches it the way a mouse does, by going to the thing that opens it.
        /// </summary>
        private void AddSystem(
            GraphBuilder builder,
            StarSystemNode node,
            Empire empire,
            bool owned,
            StarSystemLabel[] labels
        )
        {
            StarSystemNode it = node;
            StarSystemLabel label = LabelFor(node, labels);
            AgeTooltip tooltip = label == null ? null : label.StarTooltip;
            StarSystemLabel drawn = label;
            NodeVtable vtable = GraphNodes.Group(() => it.LocalizedName);
            // What is parked here, then everything the map writes on the label itself - the icons it
            // flanks the name with, what is being built, what is in the ground - and last the dossier
            // behind the star. The middle one is a page of detail drawn as pictures, so it is reviewed
            // rather than spoken (<see cref="SystemLabelReadout"/>).
            vtable.Sections = GraphNodes.Sections(
                NodeSection.Buffer(() => ConstellationLines(it, empire)),
                NodeSection.Buffer(() => FleetPresence.LinesAt(it)),
                NodeSection.Buffer(() => SystemLabelReadout.Lines(drawn)),
                StarDossier(it, tooltip)
            );
            if (owned)
            {
                // A system of yours is either a colony or still an OUTPOST, and the map draws the two
                // differently - so they say different words rather than both saying "colonized".
                Empire owner = empire;
                vtable.Announcements.Add(GraphNodes.ValuePart(() => OwnedState(it, owner)));
            }

            // The two numbers the label writes in front of the player: how many live there, and how
            // many of them are the player's own agents (drawn only where there is one). Everything else
            // the label says is a page of pictures and is reviewed, not spoken. Not watched - these are
            // read off widgets the map pools and re-points at other systems as the camera moves.
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => SystemLabelReadout.Population(drawn), false)
            );
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => SystemLabelReadout.Sleepers(drawn), false)
            );

            // What the map draws parked here, in the game's own count phrase. Not watched: the answer
            // costs a walk of the docking-slot repository, and a watched part walks it every frame the
            // system is focused.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetPresence.At(it), false));

            // The two clicks the map itself puts on a system, and nothing invented on top of them.
            vtable.OnActivate = () => ZoomIn(it);
            vtable.OnContextual = () => SystemCommand(it);

            // The camera goes where the cursor MOVES, so that whoever is watching the screen is looking
            // at the system being read out. On the galaxy this only slides the camera across; it does
            // not undo the zoom a system that has been opened up asked for.
            //
            // Only where the cursor moved (<see cref="GraphNavigator.CursorMovedHere"/>): coming back
            // to this page seats the cursor on the system it was left on, and panning for that would
            // fly the camera off whatever the GAME has since centred it on - a fleet the military
            // screen located, a notification's "show me this". Focus reflects where the game is
            // looking on the way in; it only moves the camera once the player moves it.
            //
            // Once the camera is all the way in, the map pushes the system's own label off the top of
            // the screen and draws a tooltip anchor on the star instead - so that is what the pointer
            // is put on, or a tooltip meant for the system would be drawn where nobody can see it.
            AgeTransform anchor = label == null ? null : label.AgeTransform;
            AgeTooltip tip = tooltip;
            vtable.OnFocusVisual = () =>
            {
                if (CursorMoved())
                {
                    GalaxyViewLevels.PanTo(it);
                }

                AgeTooltip star = OrbitalStarTooltip(it);
                if (star != null)
                {
                    PointerFocus.MoveTo(null, star, star.AgeTransform);
                }
                else if (anchor != null)
                {
                    PointerFocus.MoveTo(null, tip, anchor);
                }
            };
            vtable.OnBlurVisual = ReleasePointer;

            // Right means "tell me what is inside this", and what is inside it is whatever the map is
            // drawing there: the circles when the camera is out, the orbital cards when it is in...
            ControlId id = ControlId.Referenced(it, "galaxy:system/" + it.GUID);
            // ...except that opening a system is also the one gesture that says "this is the place I am
            // reading now", so the camera comes in on it - owner-ruled, and it is what makes the map draw
            // the very things the branch is about to read out. The bookkeeping is done by hand because
            // OnExpand is an OVERRIDE: setting it stops the engine flipping the state itself.
            HashSet<ControlId> expansion = builder.Expansion;
            ControlId group = id;
            vtable.OnExpand = () =>
            {
                if (expansion != null)
                {
                    expansion.Add(group);
                }

                // NOT ZoomIn: that is the map's left CLICK, which while a targeting mode is armed means
                // "confirm the target here". Opening a branch is not a click and must never post an
                // order. Nothing is said either - the engine already says the group opened, and the
                // camera moving is what opening it means.
                GalaxyViewLevels.ZoomTo(it);
            };
            vtable.OnCollapse = () => Collapse(expansion, group, it);
            builder.BeginGroup(id, vtable);
            // Only what is open costs anything: a galaxy of closed systems declares one node each.
            if (builder.IsExpanded(id))
            {
                AddInside(builder, "galaxy:system/" + node.GUID, node, empire, label, true, labels);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// Closing a system's branch takes the camera back out, because opening it brought the camera in -
        /// one gesture, undone by its opposite, which is what makes going in and looking closer the same
        /// key in the first place.
        ///
        /// Only while the camera is still looking at THIS system. Focus moves the camera about the map
        /// freely, so by the time a branch is closed the player may be reading somewhere else entirely,
        /// and flying the camera home from over there would move a view nobody asked about - so a collapse
        /// anywhere but here moves nothing at all.
        ///
        /// Silent, like the expansion: the engine says the group closed, and the camera going back out is
        /// what closing it MEANS rather than a second thing that happened. The bookkeeping is by hand
        /// because OnCollapse is an override - declaring it stops the engine flipping the state itself.
        /// </summary>
        private static void Collapse(
            HashSet<ControlId> expansion,
            ControlId group,
            StarSystemNode node
        )
        {
            if (expansion != null)
            {
                expansion.Remove(group);
            }

            if (ReferenceEquals(GalaxyViewLevels.FocusedSystem, node))
            {
                ZoomOut(node);
            }
        }

        /// <summary>
        /// What the map draws inside a system, as the children of whichever node is standing for that
        /// system.
        ///
        /// TWO nodes stand for a system: its own, at the root of the stop, and the far end of a lane
        /// leading to it (<see cref="AddDestination"/>). Both open onto the same things through these same
        /// builders, so what a system holds cannot come to depend on which way the player reached it.
        ///
        /// Two things differ at a lane's end, and <paramref name="root"/> is which of the two nodes this
        /// is. Its planets are keyed under the LANE rather than carrying the planet they were read from:
        /// the root's planet nodes carry the planet itself, and a second node carrying the same object
        /// would BE that node as far as the cursor is concerned - reference identity is followed before the
        /// structural key - so the copies are keyed structurally instead. And a lane's end offers no lanes
        /// of its own: one of them leads straight back here, and the tree would have no bottom.
        /// </summary>
        private static void AddInside(
            GraphBuilder builder,
            string key,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label,
            bool root,
            StarSystemLabel[] labels
        )
        {
            AddManagementView(builder, key, label);
            AddLabelButtons(builder, key, label);
            AddPlanets(builder, key, node, empire, label, root);
            AddWrecks(builder, key, node);
            if (root)
            {
                AddStarlanes(builder, key, node, empire, labels);
            }

            AddFleets(builder, key, FleetPresence.FleetsAt(node));
            AddHangars(builder, key, node);
        }

        /// <summary>What a system of the player's IS - taken from the state the game paints its label
        /// from, so the word and the picture always agree. Anything other than an outpost is the colony
        /// the word "colonized" has always meant.</summary>
        private static string OwnedState(StarSystemNode node, Empire empire)
        {
            try
            {
                return ModStrings.Get(
                    IsOutpost(node, empire)
                        ? ModStrings.GalaxySystemOutpost
                        : ModStrings.GalaxySystemColonized
                );
            }
            catch (Exception)
            {
                return ModStrings.Get(ModStrings.GalaxySystemColonized);
            }
        }

        /// <summary>The word for what this empire holds at a place, or nothing at all where it holds
        /// nothing - so that a system named anywhere in the tree reads the same way it reads at the root
        /// of the systems stop, and the word cannot drift between the two places that say it.</summary>
        private static string StateOf(GameNode node, Empire empire)
        {
            StarSystemNode system = node as StarSystemNode;
            return system != null && Held(system, empire) ? OwnedState(system, empire) : null;
        }

        /// <summary>Whether this empire holds anything at all here - the same membership test the owned
        /// half of the systems stop is built from (<see cref="BuildSystems"/>), a GHOST of a colony
        /// included, because that is what the map's label is counting.</summary>
        private static bool Held(GameNode node, Empire empire)
        {
            DepartmentOfTheInterior interior =
                empire == null ? null : empire.GetAgency<DepartmentOfTheInterior>();
            IList<ColonizedStarSystem> systems =
                interior == null ? null : interior.ColonizedStarSystems;
            for (int i = 0; systems != null && i < systems.Count; i++)
            {
                if (systems[i] != null && systems[i].Node == node)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether what this empire holds here is still an outpost. Read off the same list the
        /// stop was built from - an empire can hold a colony and a GHOST of one in the same place, and
        /// the ghost is not what the map's label is showing.</summary>
        private static bool IsOutpost(StarSystemNode node, Empire empire)
        {
            DepartmentOfTheInterior interior =
                empire == null ? null : empire.GetAgency<DepartmentOfTheInterior>();
            if (interior == null)
            {
                return false;
            }

            IList<ColonizedStarSystem> systems = interior.ColonizedStarSystems;
            for (int i = 0; systems != null && i < systems.Count; i++)
            {
                ColonizedStarSystem system = systems[i];
                if (system != null && system.Node == node && system.State != StarSystemState.Ghost)
                {
                    return system.State == StarSystemState.Outpost;
                }
            }

            return false;
        }

        /// <summary>The game's own left click on a system: the camera comes all the way in, which is
        /// also what swaps the system's planets from circles to cards. Silent here - what the camera
        /// did is reported as the rung it landed on, from the one watcher that reports every zoom
        /// change however it was made (<see cref="ZoomWatch"/>), rather than by each key that causes
        /// one saying so in words of its own.
        ///
        /// Takes any node on the map rather than a system, because the far end of a starlane is offered
        /// as a node of its own (<see cref="AddDestination"/>) and its Enter has to be this exact click
        /// rather than a second copy of it.
        ///
        /// While the game has the map in one of its TARGETING modes - launch a probe, take this system,
        /// fire the obliterator - the same left click means "confirm the target here" instead of the
        /// zoom, for the mouse as much as for us (<see cref="CursorTargeting"/>), so the mode is asked
        /// first and the camera is left alone when it answers.</summary>
        private static void ZoomIn(GameNode node)
        {
            if (CursorTargeting.ConfirmAt(node))
            {
                return;
            }

            GalaxyViewLevels.ZoomTo(node);
        }

        /// <summary>
        /// The map's own right click on a system, which is two things and never both: while the cursor
        /// is holding fleets it is where they are being sent, and while it is holding nothing it is the
        /// way back out of a zoom.
        ///
        /// Asked only when the key is pressed. Working out whether a fleet could get here is a
        /// pathfinding search per fleet, which is a thing to do on demand and never on a frame.
        /// </summary>
        private static void SystemCommand(StarSystemNode node)
        {
            List<Fleet> selected = FleetOrders.Selected();
            if (selected.Count > 0)
            {
                SendAll(SendableTo(node, selected));
                return;
            }

            // Nothing selected, nothing to unzoom: silent, like every other gesture key with nothing
            // to do here - and silent when it DOES move the camera too, because the rung it lands on
            // is announced by the watcher that reports every zoom change (<see cref="ZoomWatch"/>).
            ZoomOut(node);
        }

        /// <summary>
        /// Put the camera back out at the default view, still looking at this system. Nothing at all
        /// where the camera is already out - there is no zoom to undo.
        ///
        /// Says nothing itself, wherever it is reached from - the key on a system
        /// (<see cref="SystemCommand"/>) or closing a branch (<see cref="Collapse"/>). What the camera
        /// did is the rung it landed on, announced once by <see cref="ZoomWatch"/> however it moved.
        ///
        /// NOT the game's RestoreZoom, for the reason ZoomToStep's own doc comment records: the game
        /// restores the camera to wherever it stood BEFORE the click-zoom, which for a keyboard player is
        /// somewhere they have since navigated away from - and its hasZoomBeenForced gate makes it a
        /// talking no-op for a camera that reached orbital zoom any other way (the mouse wheel, a restore
        /// by step number). The keyboard's way out is the default view at the system in question, always.
        /// </summary>
        private static void ZoomOut(StarSystemNode node)
        {
            if (GalaxyViewLevels.ZoomStep <= GalaxyViewLevels.DefaultZoomStep)
            {
                return;
            }

            GalaxyViewLevels.ZoomToStep(node, GalaxyViewLevels.DefaultZoomStep);
        }

        /// <summary>The button the map draws on a colony's own label, beside its name - the one route
        /// into the system's page, and the one the mouse takes. Declared only while the game is drawing
        /// it and willing to act on it, which is its own answer to "is this a colony of mine".</summary>
        private static void AddManagementView(
            GraphBuilder builder,
            string key,
            StarSystemLabel label
        )
        {
            AgeTransform button = label == null ? null : label.RequestManagementViewButton;
            if (button == null || !Visible(button) || !AgeWidgets.Operable(button))
            {
                return;
            }

            AgeTransform it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(ModStrings.GalaxyOpenSystem),
                () => AgeWidgets.Press(it),
                null,
                Raw(it)
            );
            PointAt(vtable, it);
            builder.AddItem(ControlId.Structural(key + "/management"), vtable);
        }

        /// <summary>The other buttons the map draws on a system's label - the diplomacy button under the
        /// name, the two conversion buy-outs and the pirate mark beside it, the hacking beacon. Which of
        /// them exists at all depends on who lives there and what is being done to the place, so the
        /// list is whatever the game is drawing this frame; a system with none of them keeps whatever
        /// children it had. The treatment each one gets is <see cref="SystemLabelReadout.Actions"/>'s.
        /// </summary>
        private static void AddLabelButtons(GraphBuilder builder, string key, StarSystemLabel label)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(4);
            SystemLabelReadout.Actions(found, label);
            CardActions.Emit(builder, key + "/label", found);
        }

        /// <summary>
        /// The stretch of sky this system stands in, by the name the map writes across it.
        ///
        /// The map draws constellation names as labels of their own, floating over regions rather than
        /// over anything in them (<c>ConstellationLabel</c>), and the tree has no level for a region: a
        /// constellation is not a thing to walk into, it is where a system IS. So it is said on the
        /// system, off the system's own membership - which is where the label gets the name too
        /// (<c>Constellation.LocalizedName</c> is what <c>BindConstellation</c> writes into it), so the
        /// two cannot say different words.
        ///
        /// Gated on the same question the label asks before it draws at all: a constellation nobody has
        /// been into yet is nameless on the map, and naming it here would hand the player a name off the
        /// simulation that nothing on the screen is showing. Reviewed rather than spoken - it is where
        /// the system has always been, not news, and the systems stop is walked a hundred nodes at a
        /// time.
        /// </summary>
        private static IList<string> ConstellationLines(GameNode node, Empire empire)
        {
            try
            {
                Constellation constellation = node == null ? null : node.Constellation;
                if (
                    constellation == null
                    || empire == null
                    || (int)constellation.Exploration[empire] <= 0
                )
                {
                    return null;
                }

                return new string[]
                {
                    ModStrings.Format(
                        ModStrings.GalaxySystemConstellation,
                        constellation.LocalizedName
                    ),
                };
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's constellation threw: " + e);
                return null;
            }
        }

        // ---- sending the selected fleets somewhere ----
        //
        // Moving a fleet is a DRAG in this game: you pick a fleet up and drop it on a place. A drag has
        // no keyboard equivalent, so the two halves are separated - Enter on a fleet picks it up
        // (selects it), and BACKSLASH on the destination drops it, because backslash is what the map
        // itself puts a move on. Which is also the game's own model rather than a mod invention:
        // selecting a fleet changes the cursor and nothing else, the map stays live underneath, and
        // naming a destination moves everything the cursor is holding
        // (`GalaxyGarrisonCursor.GetFleetsToMove`). One fleet or five, what is spoken back says which.
        //
        // Whether anything can go at all is the pathfinder's own answer, asked when the key is pressed -
        // never per frame, and never guessed at from the map.

        /// <summary>One selected fleet and the route that would take it to the destination the key was
        /// pressed on.</summary>
        private sealed class Sendable
        {
            public Sendable(Fleet fleet, GalaxyPath path)
            {
                Fleet = fleet;
                Path = path;
            }

            public readonly Fleet Fleet;

            public readonly GalaxyPath Path;
        }

        /// <summary>Which of the selected fleets could be sent to a system. A fleet already parked
        /// there is not one of them: the game accepts the order and then does nothing about it, so
        /// counting it would turn the key's answer into a report of something that did not happen.
        /// </summary>
        private static List<Sendable> SendableTo(GameNode node, List<Fleet> fleets)
        {
            List<Sendable> found = new List<Sendable>();
            for (int i = 0; i < fleets.Count; i++)
            {
                GameNode orbit = FleetOrders.Orbit(fleets[i]);
                if (orbit != null && orbit.GUID == node.GUID)
                {
                    continue;
                }

                AddSendable(found, fleets[i], FleetOrders.PathTo(fleets[i], node));
            }

            return found;
        }

        private static List<Sendable> SendableTo(Link link, List<Fleet> fleets)
        {
            List<Sendable> found = new List<Sendable>();
            for (int i = 0; i < fleets.Count; i++)
            {
                AddSendable(found, fleets[i], FleetOrders.PathToLink(fleets[i], link));
            }

            return found;
        }

        private static void AddSendable(List<Sendable> found, Fleet fleet, GalaxyPath path)
        {
            if (path != null && FleetOrders.CanSend(fleet, path))
            {
                found.Add(new Sendable(fleet, path));
            }
        }

        /// <summary>
        /// Post one move per fleet, along the route worked out when the key was pressed - which is how
        /// the game itself does a multi-fleet drag: one order each, and each one checked again as it is
        /// posted.
        ///
        /// What went is said back, named after what would actually go: the fleet by name while there is
        /// one, and how many while there are several. Where nothing could get there the key is SILENT -
        /// the same answer as a control with no such command at all, because a refused order and an
        /// absent one are the same to the player, and this key is pressed speculatively all over the
        /// map.
        /// </summary>
        private static void SendAll(List<Sendable> sendable)
        {
            if (sendable.Count == 0)
            {
                return;
            }

            for (int i = 0; i < sendable.Count; i++)
            {
                FleetOrders.Send(sendable[i].Fleet, sendable[i].Path);
            }

            Voice.Say(
                sendable.Count == 1
                    ? ModStrings.Format(
                        ModStrings.GalaxySendFleet,
                        sendable[0].Fleet.LocalizedName
                    )
                    : ModStrings.Format(ModStrings.GalaxySendFleets, sendable.Count),
                true
            );
        }

        /// <summary>
        /// The planets the map draws inside a system's label, in the order it draws them.
        ///
        /// Which planets those are is the label's own question, asked the same way: the system has been
        /// identified, and this empire is allowed to see its planets at all. Until the system has been
        /// properly surveyed the game draws grey circles and names nothing, and neither does this - the
        /// planets are in the galaxy model the whole time, and reading their names off it would tell
        /// the player things the map is deliberately not showing them.
        ///
        /// Opening a system brings the camera all the way in, and at that distance the map stops
        /// drawing circles and draws a CARD in orbit for each planet - its name, what kind of world it
        /// is, whether it can be colonized and why not, its outputs, its anomalies, and the buttons for
        /// everything a fleet in the system could do to it. That card is what a sighted player browses
        /// a system with, so where one is drawn it is what a planet here reads from and what its own
        /// buttons hang off. Where one is not - the camera is somewhere else, or has not arrived yet -
        /// the planet falls back to the model's own thin answer rather than going silent.
        /// </summary>
        private static void AddPlanets(
            GraphBuilder builder,
            string place,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label,
            bool referenced
        )
        {
            try
            {
                if ((int)node.Exploration[empire] < 2 || !node.PlanetsVisibility[empire.Index])
                {
                    return;
                }

                AgeTransform table = label == null ? null : label.PlanetCirclesTable;
                PlanetLabel_SystemOrbital[] cards = OrbitalLabels(node);
                for (int i = 0; i < node.Planets.Count; i++)
                {
                    StarSystemNode system = node;
                    Planet planet = node.Planets[i];
                    Empire looking = empire;
                    PlanetLabel_SystemOrbital card = CardFor(planet, cards);
                    string key = place + "/planet/" + i;
                    // Carrying the planet only where this is the planet's ONE node (AddInside): a second
                    // node on the same object is the same control to the cursor.
                    ControlId id = referenced
                        ? ControlId.Referenced(planet, key)
                        : ControlId.Structural(key);
                    if (card != null)
                    {
                        // The card carries a row of buttons the game draws under it, so where the game
                        // is drawing any the planet is a level of the tree rather than a leaf: it reads
                        // as itself, and what could be done to it is one step in. Enter on the card is
                        // the card's own click - the planet's page - and nothing else, because
                        // everything else the old menu held is now drawn where the game draws it.
                        List<CardActions.CardAction> actions = OrbitalActions(card);
                        NodeVtable readout = OrbitalReadout(card);
                        if (actions.Count == 0)
                        {
                            builder.AddItem(id, readout);
                            continue;
                        }

                        readout.ControlType = ControlTypes.Group;
                        builder.BeginGroup(id, readout);
                        if (builder.IsExpanded(id))
                        {
                            CardActions.Emit(builder, key, actions);
                        }

                        builder.EndGroup();
                        continue;
                    }

                    // The circle is what the player would hover to get the planet's panel; without one
                    // the planet is still on the map, just with nothing to show under the pointer.
                    AgeTransform circle = Circle(table, i);
                    NodeVtable vtable = GraphNodes.Readout(
                        () => PlanetName(system, planet, looking),
                        () => PlanetStatus(system, planet, looking),
                        null,
                        Raw(circle)
                    );
                    if (circle != null)
                    {
                        PointAt(vtable, circle);
                    }

                    builder.AddItem(id, vtable);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's planets threw: " + e);
            }
        }

        // ---- the orbital cards ----

        private static readonly PlanetLabel_SystemOrbital[] NoCards =
            new PlanetLabel_SystemOrbital[0];

        /// <summary>The orbital cards the map is drawing right now. It draws them for ONE system - the
        /// one the camera has come in on - so a system anywhere else on the map gets none, and asking
        /// for another system's cards while this one's are up would hand out the wrong planets'
        /// widgets.</summary>
        private static PlanetLabel_SystemOrbital[] OrbitalLabels(StarSystemNode node)
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = OrbitalWindow();
                if (window == null || !ReferenceEquals(GalaxyViewLevels.FocusedSystem, node))
                {
                    return NoCards;
                }

                return window.GetComponentsInChildren<PlanetLabel_SystemOrbital>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the orbital cards threw: " + e);
                return NoCards;
            }
        }

        private static PlanetLabel_SystemOrbital CardFor(
            Planet planet,
            PlanetLabel_SystemOrbital[] cards
        )
        {
            try
            {
                for (int i = 0; i < cards.Length; i++)
                {
                    PlanetLabel_SystemOrbital card = cards[i];
                    if (
                        card != null
                        && ReferenceEquals(card.Planet, planet)
                        && Visible(card.AgeTransform)
                    )
                    {
                        return card;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>
        /// The dossier behind the star - what the system IS, in the game's own stat block.
        ///
        /// The map keeps TWO of these for one system and swaps them as the camera moves: the one on the
        /// system's label while the label is what the map draws, and the one the orbital window parks
        /// over the star once the camera is all the way in - at that distance the label is pushed off
        /// the top of the screen. Both are assembled by the tooltip window as it draws them, so only
        /// the one being drawn has any words in it at all.
        ///
        /// Which is why the section asks for whichever is up rather than remembering the label's:
        /// remembering it left the buffer of a system the player had zoomed into holding everything the
        /// LABEL says - what it is building, what is in the ground - and nothing about the system
        /// itself, while the picture on screen showed the dossier the whole time.
        /// </summary>
        private static NodeSection StarDossier(StarSystemNode node, AgeTooltip label)
        {
            AgeTooltip either = OrbitalStarTooltip(node) ?? label;
            if (either == null)
            {
                return null;
            }

            StarSystemNode it = node;
            AgeTooltip onTheLabel = label;
            return new NodeSection(
                () => StarDossierLines(it, onTheLabel),
                GraphNodes.ModeFor(either)
            );
        }

        /// <summary>Whichever of a system's two star tooltips the game is drawing. One at most can be up,
        /// so the first of them with anything to say is the one on the screen.</summary>
        private static IList<string> StarDossierLines(StarSystemNode node, AgeTooltip label)
        {
            IList<string> words = TooltipWords(OrbitalStarTooltip(node));
            return words != null && words.Count > 0 ? words : TooltipWords(label);
        }

        private static IList<string> TooltipWords(AgeTooltip tooltip)
        {
            Func<IList<string>> lines = AgeWidgets.TooltipLines(tooltip);
            return lines == null ? null : lines();
        }

        /// <summary>The tooltip the orbital window draws on a system's star, which it keeps parked over
        /// the star wherever the star is on screen. Null unless the camera is in on that system.
        /// </summary>
        private static AgeTooltip OrbitalStarTooltip(StarSystemNode node)
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = OrbitalWindow();
                if (window == null || !ReferenceEquals(GalaxyViewLevels.FocusedSystem, node))
                {
                    return null;
                }

                AgeTooltip star = window.StarTooltip;
                return star != null && star.AgeTransform != null ? star : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static PlanetLabelsWindow_SystemOrbital OrbitalWindow()
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemOrbital>(false)
                    : null;
                return window != null && window.Shown ? window : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// A planet as its orbital card reads it: the three lines the card writes - what it is called,
        /// what kind of world it is, and what the game says about colonizing it - with everything the
        /// card draws as icons and gauges in the review buffer, and its buttons one step in.
        ///
        /// Enter is the card's own click: the planet's page. It is the only thing the card itself does.
        /// </summary>
        private static NodeVtable OrbitalReadout(PlanetLabel_SystemOrbital card)
        {
            PlanetLabel_SystemOrbital it = card;
            AgeTooltip dossier = it.PlanetInfoTooltip;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.PlanetName)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlanetSizeAndType)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.ColonizeStatus)),
                    GraphNodes.ValuePart(() => OutpostTimer(it)),
                },
                OnActivate = () => GalaxyViewLevels.OpenPlanet(it.Planet),
            };
            // What the card DRAWS first, then its dossier - the paragraph the game writes about a
            // world of this kind, its size, its type. The dossier is the long panel behind the card,
            // so the readout indicates it and the buffer is where it is read.
            vtable.Sections = GraphNodes.Sections(
                NodeSection.Buffer(() => OrbitalDetails(it)),
                // The timer says a number and nothing else; the sentence the game explains it with is
                // reviewable rather than spoken, because the card already speaks the number and
                // hearing the paragraph again on every pass is what a buffer exists to avoid.
                NodeSection.Buffer(() => OutpostTimerHelp(it)),
                GraphNodes.TooltipSection(dossier)
            );
            PointAt(vtable, it.PlanetOrbitalCardContainer ?? it.AgeTransform);
            return vtable;
        }

        /// <summary>How long an outpost of ours has left before it becomes a colony - drawn on the card
        /// only while there is one.</summary>
        private static string OutpostTimer(PlanetLabel_SystemOrbital card)
        {
            try
            {
                return card.OutpostTimer != null && Visible(card.OutpostTimer.AgeTransform)
                    ? AgeText.Label(card.OutpostTimer)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the game says the outpost timer means - written into the timer's own tooltip
        /// while it is running, and emptied by the card's refresh when it is not.</summary>
        private static IList<string> OutpostTimerHelp(PlanetLabel_SystemOrbital card)
        {
            try
            {
                if (card.OutpostTimer == null || !Visible(card.OutpostTimer.AgeTransform))
                {
                    return null;
                }

                Func<IList<string>> lines = AgeWidgets.TooltipLines(card.OutpostTooltip);
                return lines == null ? null : lines();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What the card shows that its written lines do not carry, and NOTHING ELSE: the outputs it
        /// writes as numbers, the anomalies, curiosities and deposits it draws as icons, and last the
        /// long panel it shows when the card itself is hovered. In the order the card draws them.
        ///
        /// The rule this obeys is that the buffer is the card's FACE. Anything read off the game's
        /// model rather than off the card put words in the player's ear that no one looking at the
        /// screen could see: the five outputs a colony has are drawn as numbers and belong here, and
        /// the same five on a world nobody has settled are drawn as rows of pips standing for a
        /// rating, so reading the simulation's raw values for them described a card that does not
        /// exist. The game's refusal to colonize is not here either - it belongs to the BUTTON the
        /// game is refusing on, which is a child node of this card and carries it in the game's own
        /// words.
        /// </summary>
        private static IList<string> OrbitalDetails(PlanetLabel_SystemOrbital card)
        {
            List<string> lines = new List<string>();
            try
            {
                AddDecay(lines, card);
                AddFidsi(lines, card);
                AddAnomalies(lines, card);
                // The curiosities are NOT read here: each one is a button of the card's and is a child
                // node of its own (<see cref="AddCuriosities"/>). They were a line here only while the
                // line was silent - the items draw no words - and naming them off their wrappers would
                // have made the card say every curiosity twice.
                AddWidgetLines(lines, card.ResourceDepositsGroup);
                // The dossier is NOT read here: it is the card's tooltip section, declared beside
                // this one, and reading it twice is what happens when two places both remember it.
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card threw: " + e);
            }

            return lines;
        }

        /// <summary>
        /// That the planet is decaying - a world that was colonized and lost, which colonizing the
        /// system again would restore. The card says it with one wordless icon and keeps the sentence in
        /// that icon's own tooltip, with a different one per cause (a Vodyani leech, a pirate invasion,
        /// Unfallen tendrils pulled out) - and the game hides the icon outright for the Vodyani player
        /// whose own ark is the cause (<c>PlanetLabel_SystemOrbital</c> :353-381).
        ///
        /// Drawn is the gate, and it has to be: the icon's tooltip carries the general sentence from the
        /// prefab whether or not the card is showing it, so anything reading the tooltip alone would
        /// tell every player that every healthy planet had been lost.
        /// </summary>
        private static void AddDecay(List<string> lines, PlanetLabel_SystemOrbital card)
        {
            try
            {
                AgeTransform icon = card.HuntingGroundsIcon;
                if (icon == null || !Visible(icon))
                {
                    return;
                }

                Func<IList<string>> decay = AgeWidgets.TooltipLines(AgeWidgets.Raw(icon));
                IList<string> said = decay == null ? null : decay();
                for (int i = 0; said != null && i < said.Count; i++)
                {
                    AddLine(lines, said[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card's decay marker threw: " + e);
            }
        }

        /// <summary>What has been found on the planet. The card draws each anomaly as a coloured icon
        /// with no words on it at all, so the names come from the game's own wrapper for the same
        /// anomaly - the one whose title it writes wherever it does have room. Only while the card is
        /// drawing the row: the planet knows its anomalies whether or not they are on screen.</summary>
        private static void AddAnomalies(List<string> lines, PlanetLabel_SystemOrbital card)
        {
            try
            {
                Planet planet = card.Planet;
                if (planet == null || !Visible(card.PlanetAnomaliesTable))
                {
                    return;
                }

                for (int i = 0; i < planet.Anomalies.Count; i++)
                {
                    Anomaly anomaly = planet.Anomalies[i];
                    AddLine(
                        lines,
                        AgeText.Clean(
                            new GuiAnomaly(anomaly.AnomalyDefinition, planet).Title
                        )
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a planet's anomalies threw: " + e);
            }
        }

        /// <summary>
        /// The colony's five outputs, named by the game's own property titles and read off the same
        /// simulation object the card reads them from.
        ///
        /// Only where the card WRITES them, which is only for a colony: on a world nobody has settled
        /// the card hides this row and draws a table of pips instead - a rating, not a number - and
        /// the numbers behind those pips are a thing the game is deliberately not showing.
        /// </summary>
        private static void AddFidsi(List<string> lines, PlanetLabel_SystemOrbital card)
        {
            try
            {
                FidsiEnumerator fidsi = card.FidsiEnumerator;
                ColonizedPlanet colony = card.ColonizedPlanet;
                if (
                    fidsi == null
                    || fidsi.FidsiProperties == null
                    || colony == null
                    || !Visible(fidsi.AgeTransform)
                )
                {
                    return;
                }

                Amplitude.Unity.Simulation.SimulationObject simulation = colony.SimulationObject;
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

                    AddLine(
                        lines,
                        new MessageBuilder()
                            .ListItem(AgeText.Clean(Gui.GetLocalizedTitle(property.Name)))
                            .ListItem(Amount(simulation.GetPropertyValue(property.Name), false, 0))
                            .Build()
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card's outputs threw: " + e);
            }
        }

        /// <summary>Which of the card's buttons the game is drawing, in drawn order. Empty for a card
        /// the game is offering nothing on, which is what keeps such a planet a leaf of the tree rather
        /// than a branch that opens onto nothing. The treatment each one gets is
        /// <see cref="CardActions"/>'s, shared with the management page's card.</summary>
        private static List<CardActions.CardAction> OrbitalActions(PlanetLabel_SystemOrbital card)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(4);
            try
            {
                CardActions.AddNamedByMod(found, card.ColonizeButton, ModStrings.SystemColonize);
                // The two faction-specific ways of settling a world, drawn in place of Colonize for the
                // empires that have them. The game gives them no caption, so they are named by the
                // sentence their own tooltip opens with.
                CardActions.AddNamedByTooltip(found, card.VodyaniHintButton);
                CardActions.AddNamedByTooltip(found, card.UmbralChoirHintButton);
                CardActions.AddNamedByTooltip(found, card.BuyOutpostButton);
                CardActions.AddNamedByTooltip(found, card.MinorFactionButton);

                // The row of small round buttons under the card. The game draws them as bare icons and
                // hangs an assembled stat block on each, so there is no caption and no first line of
                // tooltip to name them by - but the game DOES name every one of them, on the fleet
                // action each carries out, and those are the words a player reading the manual would
                // meet. In the order the card draws them.
                CardActions.AddNamedByGame(found, card.TerraformationButton, "%InitiateTerraformPlanetFleetActionTitle");
                CardActions.AddNamedByGame(found, card.RestorationButton, "%InitiateRestorePlanetFleetActionTitle");
                CardActions.AddNamedByGame(found, card.AnomalyReductionButton, "%InitiateReduceAnomalyFleetActionTitle");
                CardActions.AddNamedByGame(found, card.MiningProbeButton, "%LaunchMiningProbeFleetActionTitle");
                CardActions.AddNamedByGame(found, card.DestroyButton, "%DestroyPlanetFleetActionTitle");

                // What has been found in orbit and not yet looked into. Each one is a button of the
                // card's like any other, drawn in a ring around it rather than in the row, so they come
                // after the row.
                AddCuriosities(found, card);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card's buttons threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// The curiosities the card is drawing - each one a button that starts an expedition on it.
        ///
        /// The game draws one wordless icon per curiosity still to be looked into, keeps it CLICKABLE
        /// while refusing, and writes the reason into its own tooltip
        /// (<c>PlanetCuriosityItem.Refresh</c>: no fleet in orbit, one already queued, not enough
        /// expedition power), which is the treatment <see cref="CardActions.AddRefusable"/> exists for -
        /// what is in orbit and why it cannot be reached yet is exactly what the player opened the card
        /// to ask. The icon has no caption at all, so the name comes off the wrapper the game hangs on
        /// its tooltip, which is where it keeps the words it would have written.
        ///
        /// The table pools its items - a card that has run out of curiosities keeps the widgets and
        /// hides them - so what is DRAWN is the gate, exactly as it is for the card's other buttons.
        /// </summary>
        private static void AddCuriosities(
            List<CardActions.CardAction> found,
            PlanetLabel_SystemOrbital card
        )
        {
            AgeTransform table = card.PlanetCuriositiesTable;
            if (table == null || !Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                if (item != null && Visible(item))
                {
                    CardActions.AddRefusable(found, item, CardActions.TitleOf(item));
                }
            }
        }

        /// <summary>
        /// The wrecked Arks drifting in this system, each one a button that starts repairing it.
        ///
        /// The game draws them where it draws the planet cards - in the ring around the star of the
        /// system the camera has come in on (<c>WreckedMothershipLabelWindow</c>, bound to the FOCUSED
        /// node), one wordless icon per wreck - so they are children of that system, after its planets,
        /// and they exist for no other system on the map.
        ///
        /// The treatment is the curiosities': the item stays clickable while the game refuses it and
        /// writes the reason into its own tooltip (<c>WreckedMothershipItem.Refresh</c>: no fleet of
        /// yours in orbit, or the action's own failure list), which is exactly what a player who found
        /// a wreck wants to hear. Pressing one posts the repair order; pressing one that is ALREADY
        /// being repaired raises the game's own confirmation box for calling it off, which speaks
        /// through the message-box screen like every other one.
        /// </summary>
        private static void AddWrecks(GraphBuilder builder, string key, StarSystemNode node)
        {
            try
            {
                WreckedMothershipLabelWindow window = WreckWindow(node);
                AgeTransform table = window == null ? null : window.CuriositiesTable;
                IList<AgeTransform> items = table == null ? null : table.Children;
                if (items == null || items.Count == 0)
                {
                    return;
                }

                List<CardActions.CardAction> found = new List<CardActions.CardAction>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    AgeTransform item = items[i];
                    if (item != null && Visible(item))
                    {
                        CardActions.AddRefusable(found, item, WreckName(item, window));
                    }
                }

                CardActions.Emit(builder, key + "/wreck", found);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's wrecked motherships threw: " + e);
            }
        }

        /// <summary>What to call one wreck: the name of the ship itself, which is what the game keeps on
        /// the wrapper it hangs on the item's tooltip, and where that is empty the words the window
        /// writes over the whole group.</summary>
        private static Func<string> WreckName(AgeTransform item, WreckedMothershipLabelWindow window)
        {
            AgeTooltip tooltip = Raw(item);
            AgePrimitiveLabel title = window.TitleLabel;
            return () =>
            {
                string named = AgeWidgets.TooltipTitle(tooltip);
                return string.IsNullOrEmpty(named) ? AgeText.Label(title) : named;
            };
        }

        /// <summary>The wreck window, but only while it is drawing THIS system's wrecks - it holds one
        /// system at a time, the one the camera has come in on, exactly as the orbital card window does
        /// (<see cref="OrbitalLabels"/>).</summary>
        private static WreckedMothershipLabelWindow WreckWindow(StarSystemNode node)
        {
            try
            {
                WreckedMothershipLabelWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<WreckedMothershipLabelWindow>(false)
                    : null;
                return window != null
                    && window.Shown
                    && ReferenceEquals(GalaxyViewLevels.FocusedSystem, node)
                    ? window
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The circle the label draws for the planet at <paramref name="index"/>, or null if
        /// the label has not been given one - the window pools its labels and grows the row as systems
        /// are discovered, so a row can be short of a planet the model already has.</summary>
        private static AgeTransform Circle(AgeTransform table, int index)
        {
            try
            {
                if (table == null || index >= table.Children.Count)
                {
                    return null;
                }

                AgeTransform circle = table.Children[index];
                return circle != null && circle.Visible ? circle : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the map calls this planet. A system the player has not surveyed shows a circle
        /// with no name on it and a panel that says only that the planet is unknown, so that word - the
        /// game's own - is the whole of what a planet in one is called here.</summary>
        private static string PlanetName(StarSystemNode system, Planet planet, Empire empire)
        {
            try
            {
                if (!Surveyed(system, empire))
                {
                    return AgeText.Clean(Gui.Localize("%PlanetStatusUnknownTitle"));
                }

                return AgeText.Clean(new GuiPlanet(planet).Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the circle is saying about the planet by the colour it is drawn in - colonized,
        /// an outpost, inhospitable - in the game's own words for each of those states. Nothing for a
        /// planet in an unsurveyed system: the name has already said it is unknown.</summary>
        private static string PlanetStatus(StarSystemNode system, Planet planet, Empire empire)
        {
            try
            {
                if (!Surveyed(system, empire))
                {
                    return null;
                }

                GuiPlanet.PlanetStatuses status = new GuiPlanet(planet).PlanetStatus;
                return AgeText.Clean(Gui.Localize("%PlanetStatus" + status + "Title"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the game has let this empire see what the planets in a system actually are.
        /// The circles switch from grey unknowns to real planets at the same threshold.</summary>
        private static bool Surveyed(StarSystemNode system, Empire empire)
        {
            return (int)system.Exploration[empire] >= (int)EntityExploration.State.Revealed;
        }

        /// <summary>
        /// The lanes leaving a system, and where each one goes.
        ///
        /// A lane says the name of the system at its far end only when the map draws that name.
        /// Everything else is a lane into the unexplored, which is what the map shows: a line running
        /// off into the dark. The galaxy model would answer either way - it holds every system's name
        /// from the first turn - so the check, not the model, is what keeps this honest.
        ///
        /// A wormhole is a different thing from a starlane and is said to be one. An empire without the
        /// technology to see them is shown none, exactly as the game's own neighbour search skips them.
        ///
        /// The game numbers no lane and the model's own order is whatever order the galaxy was
        /// generated in, so the lanes are walked - and numbered - going clockwise from north, and each
        /// one says the way it leaves. That is the mod's ordering, not the game's: a player who cannot
        /// see the lines needs the same "which one is that" the picture gives everyone else, and a
        /// number that moves between sessions would be worse than none.
        /// </summary>
        private static void AddStarlanes(
            GraphBuilder builder,
            string place,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel[] labels
        )
        {
            try
            {
                List<Lane> lanes = new List<Lane>();
                for (int i = 0; i < node.Links.Count; i++)
                {
                    Link link = node.Links[i];
                    bool wormhole = link is WormholeLink;
                    if (wormhole && !empire.HasWormholeTechnology)
                    {
                        continue;
                    }

                    if (link.Exploration[empire] == EntityExploration.State.Unrevealed)
                    {
                        continue;
                    }

                    GameNode far = ReferenceEquals(link.ExtremityNode1, node)
                        ? link.ExtremityNode2
                        : link.ExtremityNode1;
                    Lane lane = new Lane
                    {
                        Link = link,
                        Far = far,
                        Wormhole = wormhole,
                        Bearing = CompassDirections.Bearing(
                            far.GalaxyPosition.X - node.GalaxyPosition.X,
                            far.GalaxyPosition.Y - node.GalaxyPosition.Y
                        ),
                    };
                    lanes.Add(lane);
                }

                lanes.Sort(ClockwiseFromNorth);

                for (int i = 0; i < lanes.Count; i++)
                {
                    Link link = lanes[i].Link;
                    GameNode destination = lanes[i].Far;
                    int number = i + 1;
                    string direction = CompassDirections.KeyForBearing(lanes[i].Bearing);
                    bool named = Perceived(destination, empire);
                    string template = lanes[i].Wormhole
                        ? (named ? ModStrings.GalaxyWormhole : ModStrings.GalaxyWormholeUnexplored)
                        : (named ? ModStrings.GalaxyStarlane : ModStrings.GalaxyStarlaneUnexplored);
                    Func<string> text = named
                        ? (Func<string>)(
                            () =>
                                ModStrings.Format(
                                    template,
                                    number,
                                    destination.LocalizedName,
                                    ModStrings.Get(direction)
                                )
                        )
                        : () => ModStrings.Format(template, number, ModStrings.Get(direction));
                    Link lane = link;
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(text),
                            // Whatever the map is drawing out on this lane, said the way the map's own
                            // lozenge tooltip heads it. Read on focus rather than watched, for the same
                            // reason a system's is.
                            GraphNodes.ValuePart(() => FleetPresence.On(lane), false),
                        },
                        Sections = GraphNodes.Sections(() => FleetPresence.LinesOn(lane), null),
                    };
                    // A lane is a destination in its own right, not just a road to one: the game
                    // accepts a link as a move target and flies the fleet out onto it
                    // (`GalaxyGarrisonCursor.GetGalaxyPathToTargets` resolves either a node or a
                    // link), which is how a fleet is parked between two systems or pointed down a
                    // lane into the dark. So backslash - the map's own move click - sends here too.
                    //
                    // ENTER on a lane is the map's LEFT click on one, and the only thing that click
                    // does is let go of whatever the cursor is holding
                    // (`GalaxyGarrisonCursor.OnCursorClick` :88-95 changes back to the plain cursor for
                    // a click that landed on a link and nothing else). With nothing selected the click
                    // does nothing at all, and so does this - there is no action to invent.
                    Link target = link;
                    vtable.OnActivate = Deselect;
                    vtable.OnContextual = () => LaneCommand(target);

                    string key = place + "/lane/" + link.GUID;
                    // Keyed on the pair of GUIDs and NOT carrying the link as a reference: ONE lane runs
                    // between two systems, and once both ends are in the tree the same Link object backs
                    // two nodes - which are one control to the cursor, because reference identity is
                    // followed before the structural key. Measured with the fog lifted: focusing the
                    // Hir end of the Xiu-Hir lane threw the cursor across to the Xiu end on the next
                    // rebuild. Two GUIDs are stable without a reference, so nothing is lost.
                    ControlId id = ControlId.Structural(key);
                    IList<Fleet> flying = FleetPresence.FleetsOn(link);
                    // A lane into the dark with nothing flying it is a leaf: there is neither a fleet
                    // to walk nor a system to name at the far end.
                    if (flying.Count == 0 && !named)
                    {
                        builder.AddItem(id, vtable);
                        continue;
                    }

                    vtable.ControlType = ControlTypes.Group;
                    builder.BeginGroup(id, vtable);
                    if (builder.IsExpanded(id))
                    {
                        AddFleets(builder, key, flying);
                        if (named)
                        {
                            AddDestination(builder, key, destination, empire, labels);
                        }
                    }

                    builder.EndGroup();
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's starlanes threw: " + e);
            }
        }

        /// <summary>One lane leaving a system, with the way it leaves already worked out.</summary>
        private struct Lane
        {
            public Link Link;
            public GameNode Far;
            public bool Wormhole;
            public double Bearing;
        }

        private static readonly Comparison<Lane> ClockwiseFromNorth = delegate(Lane a, Lane b)
        {
            return a.Bearing.CompareTo(b.Bearing);
        };

        /// <summary>
        /// Where a lane goes, as a child of the lane - the place at its far end, offered here because
        /// that is where the player asking "what is down this line" is standing.
        ///
        /// Only where the map draws the far end's name, which is the same question the lane's own
        /// label asked: a lane into the dark has no destination to offer and stays a leaf.
        ///
        /// It says and does what the system's own node at the root of the tree says and does - the same
        /// state word, the same count phrase for what is parked there, the same two clicks: ENTER is the
        /// map's left click on that system (the camera goes in on it, <see cref="ZoomIn"/> - the very
        /// call the root node makes, so the two cannot drift apart) and BACKSLASH sends the selection
        /// all the way there, which is the distinction the game itself draws between a move ordered on a
        /// node and one ordered on a link: the lane means "fly out onto this line", the system at its end
        /// means "go there".
        ///
        /// And it OPENS, on the same key and with the same meaning as the system's own node: right brings
        /// the camera in on that system and walks what the map is drawing there - its planets, its label's
        /// buttons, what is parked in it (<see cref="AddInside"/>) - and closing it takes the camera back
        /// out again (<see cref="Collapse"/>). Which is the whole point of a lane leading somewhere
        /// explored: "what is down this line" is answered where the player is standing, without walking
        /// back to a list of a hundred systems to find the one they were just told the name of. Only the
        /// LANES are left off, or one of them would lead back here and the tree would have no bottom.
        ///
        /// Keyed structurally and NEVER on the far node, and neither is anything under it: that node is
        /// already a node of this screen at the root of the systems stop, and two nodes sharing a backing
        /// object are one control to the cursor - reference identity is followed before the structural key -
        /// so carrying the reference here would teleport the player out of the lane the moment anything
        /// rebuilt (the same trap <see cref="AddFleets"/> records).
        /// </summary>
        private static void AddDestination(
            GraphBuilder builder,
            string laneKey,
            GameNode destination,
            Empire empire,
            StarSystemLabel[] labels
        )
        {
            GameNode it = destination;
            Empire looking = empire;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () => ModStrings.Format(ModStrings.GalaxyLaneDestination, it.LocalizedName)
                    ),
                    GraphNodes.ValuePart(() => StateOf(it, looking)),
                    // Read on focus rather than watched, for the reason a system's own count phrase is:
                    // the answer costs a walk of the docking-slot repository.
                    GraphNodes.ValuePart(() => FleetPresence.At(it), false),
                },
                Sections = GraphNodes.Sections(() => FleetPresence.LinesAt(it), null),
                OnActivate = () => ZoomIn(it),
                OnContextual = () => SendTo(it),
            };

            string key = laneKey + "/destination";
            ControlId id = ControlId.Structural(key);
            StarSystemNode system = destination as StarSystemNode;
            if (system == null)
            {
                // Whatever else the galaxy hangs a lane off is not a place with planets in it.
                builder.AddItem(id, vtable);
                return;
            }

            StarSystemNode inside = system;
            HashSet<ControlId> expansion = builder.Expansion;
            vtable.ControlType = ControlTypes.Group;
            vtable.OnExpand = () =>
            {
                if (expansion != null)
                {
                    expansion.Add(id);
                }

                // The same silent zoom the system's own node opens with, and NOT ZoomIn: opening a branch
                // is not a click and must never confirm a target.
                GalaxyViewLevels.ZoomTo(inside);
            };
            vtable.OnCollapse = () => Collapse(expansion, id, inside);
            builder.BeginGroup(id, vtable);
            if (builder.IsExpanded(id))
            {
                AddInside(
                    builder,
                    key,
                    inside,
                    empire,
                    LabelFor(inside, labels),
                    false,
                    labels
                );
            }

            builder.EndGroup();
        }

        /// <summary>Send the selected fleets to a place on the map, and nothing else. The system's own
        /// node does more than this on the same key (<see cref="SystemCommand"/>): with nothing selected
        /// it undoes a zoom, which from inside a lane would move a camera the player never asked about.
        /// </summary>
        private static void SendTo(GameNode node)
        {
            SendAll(SendableTo(node, FleetOrders.Selected()));
        }

        /// <summary>
        /// Send the selection out onto a lane.
        ///
        /// Onto the lane a fleet is ALREADY flying, the game answers with the route to the next system
        /// on it (<c>GalaxyGarrisonCursor.GetGalaxyPathToLink</c> :352-361, ported in
        /// <c>FleetOrders.PathToLink</c>), which is the game's own way of saying "stop when you get
        /// there" - so a fleet is called off its long journey by asking it to fly the piece of lane it
        /// is on, and there is no cancel to invent.
        /// </summary>
        private static void LaneCommand(Link link)
        {
            List<Fleet> selected = FleetOrders.Selected();
            if (selected.Count == 0)
            {
                return;
            }

            SendAll(SendableTo(link, selected));
        }

        /// <summary>
        /// Let go of whatever the map's cursor is holding, exactly as a click on empty space or on a
        /// starlane does (<c>GalaxyGarrisonCursor.OnCursorClick</c>): the garrison cursor is swapped
        /// back for the plain one, which is also what takes the fleet panel off the screen.
        ///
        /// Nothing is said here. The panel going is what the player is being told about, and the
        /// panel's own watcher says it - one announcement, from the one place that knows.
        /// </summary>
        private static void Deselect()
        {
            try
            {
                Amplitude.Unity.View.ICursorService cursors =
                    Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
                if (cursors != null && Gui.GetCursor() is GalaxyGarrisonCursor)
                {
                    cursors.ChangeCursor(typeof(GalaxyCursor), Gui.GetCursor());
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: letting go of the selection threw: " + e);
            }
        }

        /// <summary>The on-map label carrying this system's tooltip - matched by the node reference
        /// the label was bound to, with the entity's own identity as a fallback for the rare case the
        /// window rebuilt its labels from a copy rather than the same instance this stop is holding.
        /// </summary>
        private static StarSystemLabel LabelFor(StarSystemNode node, StarSystemLabel[] labels)
        {
            try
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    if (ReferenceEquals(labels[i].StarSystemNode, node))
                    {
                        return labels[i];
                    }
                }

                for (int i = 0; i < labels.Length; i++)
                {
                    StarSystemNode candidate = labels[i].StarSystemNode;
                    if (candidate != null && candidate.GUID == node.GUID)
                    {
                        return labels[i];
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a system to its map label threw: " + e);
            }

            return null;
        }

        private static readonly StarSystemLabel[] NoLabels = new StarSystemLabel[0];

        /// <summary>Every label the map is currently drawing for a system - fetched fresh each time
        /// rather than cached across builds, because the window grows this list as the player explores
        /// more of the galaxy and a cache keyed on nothing that changes would go stale exactly when a
        /// newly-discovered system needed its tooltip.</summary>
        private static StarSystemLabel[] SystemLabels()
        {
            try
            {
                StarSystemLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<StarSystemLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoLabels
                    : window.GetComponentsInChildren<StarSystemLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the system labels threw: " + e);
                return NoLabels;
            }
        }

        // ---- fleets ----


        /// <summary>
        /// The fleets standing at one place on the map - what each is made of, where it is, and where
        /// it is going - as children of that place.
        ///
        /// Which fleets those are is never worked out here: they are the ones the map's own lozenge at
        /// this system or on this lane is holding (<see cref="FleetPresence"/>), so a fleet nobody can
        /// see is absent for the same reason it is absent from the picture, and the count the place
        /// announces and the children it opens onto are the same answer read two ways.
        ///
        /// Focus points at whichever label the map is drawing the fleet with, so the game draws the
        /// fleet's own dossier for it exactly as it would for a mouse resting there. Which label that
        /// is is the map's answer, not this screen's: a fleet in orbit is drawn by the DOCK label of
        /// the slot it is sitting in and a fleet under way by its own, and the windows that own them
        /// bind exactly one of the two at a time. A dock label covers the whole slot, so where two
        /// fleets are parked together its tooltip is the pair of them - which is what the game shows a
        /// player hovering there, and reading it as anything narrower would be describing a tooltip
        /// nobody can see.
        ///
        /// Enter SELECTS the fleet, and there is nothing else on it. Calling off a move is not here
        /// either: the game has no cancel gesture on the map, and a fleet is turned round by being sent
        /// somewhere else or stopped at the next system by being sent down the lane it is already on -
        /// both of them backslash on a PLACE, which is where the map itself puts a move. Dropping it is
        /// a deliberate deviation from what this screen used to offer, and it is approved as one.
        /// </summary>
        private static void AddFleets(GraphBuilder builder, string place, IList<Fleet> fleets)
        {
            if (fleets.Count == 0)
            {
                return;
            }

            try
            {
                // Fetched once for the whole place, like the system labels: both windows pool their
                // labels rather than rebuilding them, so one walk of each serves every fleet here.
                DockLabel[] docks = DockLabels();
                FleetLabel[] flying = FleetLabels();

                for (int i = 0; i < fleets.Count; i++)
                {
                    Fleet it = fleets[i];
                    AgeTransform lozenge = FleetLozenge(it, docks, flying);
                    NodeVtable vtable = GraphNodes.Button(
                        () => it.LocalizedName,
                        () => Select(it),
                        null,
                        Raw(lozenge)
                    );
                    vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetText(it)));
                    if (lozenge != null)
                    {
                        PointAt(vtable, lozenge);
                    }

                    // Keyed on the fleet's own identity but NOT carrying the fleet as a reference:
                    // the selected-fleet panel is declared on this same screen, and its fleet line is
                    // keyed on the garrison - which for a fleet is this very object. Two nodes sharing
                    // a backing object are ONE control to the cursor (reference identity is followed
                    // before the structural key), so the panel's line teleported the player straight
                    // back out to the map on the next rebuild. The line is the one that needs the
                    // reference - its widget is a pool slot the game rebinds - and this key is a GUID,
                    // which is stable without one.
                    builder.AddItem(ControlId.Structural(place + "/fleet/" + it.GUID), vtable);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets at a place threw: " + e);
            }
        }

        /// <summary>What a fleet is made of, what it is doing, and how far it can still go this turn.
        /// </summary>
        private static string FleetText(Fleet fleet)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                message.ListItem(ModStrings.Format(ModStrings.GalaxyFleetShips, fleet.ShipsCount));
                message.ListItem(FleetState(fleet));
                if (fleet.IsGuarding)
                {
                    message.ListItem(ModStrings.Get(ModStrings.GalaxyFleetGuarding));
                }

                message.ListItem(
                    ModStrings.Format(
                        ModStrings.GalaxyFleetMovement,
                        Amount(fleet.CurrentMovementPoints, false, 0)
                    )
                );
                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Where the fleet is: at the system it is orbiting, or on its way to the one it is headed for.
        ///
        /// That pair is the game's own question, asked the game's own way - its fleet list draws one of
        /// two icons on exactly this test and writes the name of the same node beside it. Whether the
        /// fleet is MOVING is a different question and the wrong one: a fleet that has spent its
        /// movement half way to somewhere has stopped for the turn and is still on its way, and asking
        /// about movement leaves it describing itself as nowhere at all.
        ///
        /// A node the map has not named is not named here either, exactly as a starlane running into
        /// the dark is not.
        /// </summary>
        private static string FleetState(Fleet fleet)
        {
            Empire empire = PlayerEmpire();
            GameNode orbit = FleetOrders.Orbit(fleet);
            if (orbit != null)
            {
                return empire != null && Perceived(orbit, empire)
                    ? ModStrings.Format(ModStrings.GalaxyFleetDockedAt, orbit.LocalizedName)
                    : ModStrings.Get(ModStrings.GalaxyFleetDocked);
            }

            GameNode heading = FleetOrders.Heading(fleet);
            if (heading == null)
            {
                return ModStrings.Get(ModStrings.GalaxyFleetMoving);
            }

            return empire != null && Perceived(heading, empire)
                ? ModStrings.Format(ModStrings.GalaxyFleetMovingTo, heading.LocalizedName)
                : ModStrings.Get(ModStrings.GalaxyFleetMovingUnexplored);
        }

        /// <summary>
        /// The lozenge the map is drawing this fleet on - the dock label's where the fleet is parked,
        /// its own where it is under way, and null where the map is drawing neither.
        ///
        /// The lozenge rather than the label, because the tooltip that gets DRAWN is the one on
        /// whatever the pointer is over, and both labels carry two: a CenterTooltip of their own and
        /// the lozenge's, filled from the same fleet data. Declaring the label's while the engine draws
        /// the lozenge's leaves the readout saying "has tooltip" over a review buffer that never fills,
        /// because the buffer only reads a drawn tooltip it can recognise as the one it declared.
        /// </summary>
        private static AgeTransform FleetLozenge(
            Fleet fleet,
            DockLabel[] docks,
            FleetLabel[] flying
        )
        {
            try
            {
                for (int i = 0; i < docks.Length; i++)
                {
                    DockLabel dock = docks[i];
                    if (dock.DockingSlot == null || !Visible(dock.AgeTransform))
                    {
                        continue;
                    }

                    ReadOnlyCollection<GalaxyFleet> docked = dock.DockingSlot.GalaxyFleets;
                    for (int j = 0; j < docked.Count; j++)
                    {
                        if (docked[j] != null && docked[j].Fleet.GUID == fleet.GUID)
                        {
                            return Lozenge(dock.FleetLozenge);
                        }
                    }
                }

                for (int i = 0; i < flying.Length; i++)
                {
                    FleetLabel label = flying[i];
                    if (
                        label.GalaxyFleet != null
                        && label.GalaxyFleet.Fleet.GUID == fleet.GUID
                        && Visible(label.AgeTransform)
                    )
                    {
                        return Lozenge(label.FleetLozenge);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a fleet to its map label threw: " + e);
            }

            return null;
        }

        private static readonly DockLabel[] NoDockLabels = new DockLabel[0];

        private static readonly FleetLabel[] NoFleetLabels = new FleetLabel[0];

        private static DockLabel[] DockLabels()
        {
            try
            {
                DockLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<DockLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoDockLabels
                    : window.GetComponentsInChildren<DockLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the dock labels threw: " + e);
                return NoDockLabels;
            }
        }

        private static FleetLabel[] FleetLabels()
        {
            try
            {
                FleetLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<FleetLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoFleetLabels
                    : window.GetComponentsInChildren<FleetLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the fleet labels threw: " + e);
                return NoFleetLabels;
            }
        }

        /// <summary>
        /// The system's own hangar, where the map is drawing one.
        ///
        /// A hangar is the ships a colony is holding at home. The map draws it as a lozenge of its own
        /// beside the fleet lozenges (<c>HangarLabel</c>), and the mod already counts it in what a system
        /// says is parked there - the count phrase comes from the dock label's own group, which includes
        /// it - but it was not a place the player could go, and it is the one route to the button that
        /// turns those ships into a fleet: clicking the lozenge selects the hangar garrison and swaps in
        /// the garrison cursor, which is what puts the fleet panel on the screen with Create on it.
        ///
        /// WHICH hangars are drawn is the label window's own answer, taken from the same repository it
        /// walks (<c>HangarLabelsWindow.ShowAllLabels</c> over
        /// <c>IVisibleGalaxyHangarRepositoryService</c>) with the same test it draws on
        /// (<c>ShowLabel</c>: the hangar is holding ships) - so a hangar nobody can see is absent here
        /// for the reason it is absent from the picture, and no visibility rule is reimplemented.
        ///
        /// Last among a system's children, after the fleets: it is the thing at that system that is not
        /// going anywhere.
        /// </summary>
        private static void AddHangars(GraphBuilder builder, string place, StarSystemNode node)
        {
            try
            {
                IVisibleGalaxyHangarRepositoryService repository =
                    Amplitude.Unity.Framework.Services.GetService<IVisibleGalaxyHangarRepositoryService>();
                if (repository == null)
                {
                    return;
                }

                HangarLabel[] labels = null;
                ReadOnlyCollection<GalaxyHangar> hangars = repository.GalaxyHangars;
                for (int i = 0; i < hangars.Count; i++)
                {
                    GalaxyHangar hangar = hangars[i];
                    Hangar held = hangar == null ? null : hangar.Hangar;
                    if (held == null || held.ShipsCount <= 0 || !IsAt(held, node))
                    {
                        continue;
                    }

                    if (labels == null)
                    {
                        labels = HangarLabels();
                    }

                    GalaxyHangar it = hangar;
                    AgeTransform lozenge = HangarLozenge(hangar, labels);
                    NodeVtable vtable = GraphNodes.Button(
                        () => held.LocalizedName,
                        () => SelectHangar(it),
                        null,
                        Raw(lozenge)
                    );
                    vtable.Announcements.Add(
                        GraphNodes.ValuePart(
                            () => ModStrings.Format(ModStrings.GalaxyFleetShips, held.ShipsCount)
                        )
                    );
                    if (lozenge != null)
                    {
                        PointAt(vtable, lozenge);
                    }

                    builder.AddItem(
                        ControlId.Structural(place + "/hangar/" + held.GUID),
                        vtable
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's hangar threw: " + e);
            }
        }

        /// <summary>Whether a hangar is the one belonging to this system. A hangar hangs off the node
        /// itself rather than off the colony, which is what makes it survive the colony changing hands.
        /// </summary>
        private static bool IsAt(Hangar hangar, StarSystemNode node)
        {
            try
            {
                GameNode at = hangar.GetGameNode();
                return at != null && at.GUID == node.GUID;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static readonly HangarLabel[] NoHangarLabels = new HangarLabel[0];

        private static HangarLabel[] HangarLabels()
        {
            try
            {
                HangarLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<HangarLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoHangarLabels
                    : window.GetComponentsInChildren<HangarLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the hangar labels threw: " + e);
                return NoHangarLabels;
            }
        }

        /// <summary>The lozenge the map is drawing this hangar with - the same choice
        /// <see cref="FleetLozenge"/> makes for a fleet, and for the same reason: the tooltip that gets
        /// DRAWN is the one on whatever the pointer is over, and the label carries two.</summary>
        private static AgeTransform HangarLozenge(GalaxyHangar hangar, HangarLabel[] labels)
        {
            try
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    HangarLabel label = labels[i];
                    if (
                        label != null
                        && ReferenceEquals(label.GalaxyHangar, hangar)
                        && Visible(label.AgeTransform)
                    )
                    {
                        return Lozenge(label.FleetLozenge);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a hangar to its map label threw: " + e);
            }

            return null;
        }

        /// <summary>Select the ships a colony is holding at home, exactly as clicking their lozenge does
        /// (<c>HangarLabel.OnClickCb</c>): the hangar's cursor target becomes the selection and the
        /// garrison cursor takes over, which is what draws the fleet panel and the Create button on it.
        /// Nothing is said here - the panel's own watcher says the panel opened, from the one place that
        /// knows.</summary>
        private static void SelectHangar(GalaxyHangar hangar)
        {
            try
            {
                // Nothing is selected while a targeting mode is waiting - the same rule the fleet nodes
                // follow (<see cref="Select"/>).
                if (CursorTargeting.Aiming)
                {
                    return;
                }

                Amplitude.Unity.View.ICursorService cursors =
                    Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
                if (cursors == null || hangar == null || hangar.CursorTarget == null)
                {
                    return;
                }

                cursors.Select(hangar.CursorTarget);
                cursors.ChangeCursor(typeof(GalaxyGarrisonCursor), hangar);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: selecting a hangar threw: " + e);
            }
        }

        // ---- what the map draws between the stars ----
        //
        // A fleet is somewhere: it orbits a system or flies a lane, and the game says so by storing a
        // node or a leg on it, which is why the tree can hang it under the place the map draws it. A
        // probe, an obliterator missile and an ally's pin are NOT: each one carries a bare
        // `GalaxyPosition`, a point in space with no node and no link on it, and the map draws each one
        // at that point, out where there is nothing else. So they are not children of anything - they
        // are a third half of the map, walked beside the systems and jumped to as a region of their own.
        //
        // Which of them exist is never re-derived: the game's own label windows pool one label per
        // entity and show or hide each by the empire's vision and the camera's culling
        // (`VisibleEntityLabelsWindow.RefreshLabels*`), so the drawn label IS the answer to "can this be
        // seen", and a walk of the container costs nothing per frame - a galaxy with no probes in sight
        // has an empty container and this is a null check.

        /// <summary>Gather what the map is drawing out in open space, once per build, from the windows
        /// that draw it. No array is allocated: each container's children are walked in place and only a
        /// child the game is really drawing is asked for its label.</summary>
        private void Drifting()
        {
            _probes.Clear();
            _projectiles.Clear();
            _pins.Clear();
            try
            {
                ProbeLabelsWindow probes = Window<ProbeLabelsWindow>();
                Collect(probes == null ? null : probes.LabelsContainer, _probes);
                ObliteratorProjectileLabelsWindow shots = Window<ObliteratorProjectileLabelsWindow>();
                Collect(shots == null ? null : shots.LabelsContainer, _projectiles);
                CoordinationRequestLabelsWindow pins = Window<CoordinationRequestLabelsWindow>();
                Collect(pins == null ? null : pins.RequestLabelsContainer, _pins);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading what the map draws in open space threw: " + e);
            }
        }

        private static void Collect<TLabel>(AgeTransform container, List<TLabel> found)
            where TLabel : Component
        {
            IList<AgeTransform> children = container == null ? null : container.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child == null || !Visible(child))
                {
                    continue;
                }

                TLabel label = child.GetComponent<TLabel>();
                if (label != null)
                {
                    found.Add(label);
                }
            }
        }

        private static TWindow Window<TWindow>()
            where TWindow : Amplitude.Unity.Gui.GuiWindow
        {
            try
            {
                TWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<TWindow>(false)
                    : null;
                return window != null && window.Shown ? window : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The probes drifting through the map: whose each one is, and - for the player's own - how many
        /// turns it has left before it burns out.
        ///
        /// The label is a countdown and nothing else, and the game draws that countdown only on a probe
        /// of the player's own (<c>ProbeLabel.Refresh</c>), so that is the only number here. Everything
        /// else about a probe is in the dossier the game hangs on it, which is assembled at draw time
        /// and so is reviewed rather than spoken. The owner is named from the probe itself: the map
        /// tells a sighted player whose it is by the colour of the mote it draws out there, and a colour
        /// is the one thing a reader of this tree has no way to be told.
        ///
        /// There is nothing to activate. A probe is not a thing the game lets anyone click.
        /// </summary>
        private void AddProbes(GraphBuilder builder)
        {
            for (int i = 0; i < _probes.Count; i++)
            {
                ProbeLabel it = _probes[i];
                Probe probe = it.Entity as Probe;
                if (probe == null)
                {
                    continue;
                }

                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(it.Tooltip)),
                        GraphNodes.ValuePart(() => Owner(probe.Empire), false),
                        GraphNodes.ValuePart(
                            () => Countdown(it.DurationBackground, it.DurationLabel),
                            false
                        ),
                    },
                    Sections = GraphNodes.Sections(null, it.Tooltip),
                };
                Follow(vtable, probe, it.AgeTransform, it.Tooltip);
                builder.AddItem(ControlId.Structural("galaxy:probe/" + probe.GUID), vtable);
            }
        }

        /// <summary>
        /// The obliterator missiles crossing the map - each one a system's death, in flight.
        ///
        /// The game writes the whole of it into the label's own tooltip as one sentence - which system
        /// it is aimed at and how many turns it has left - and that tooltip is plain text, so it is
        /// spoken on focus rather than only indicated. It writes it for the player's OWN missile only
        /// (<c>ObliteratorProjectileLabel.Refresh</c> clears the tooltip and hides the countdown for
        /// anyone else's), which is the game's own choice about what an empire may know and is left
        /// exactly as it stands: someone else's missile is a thing on the map with no destination
        /// attached, and inventing one from the model would tell the player something the game is
        /// deliberately not showing.
        ///
        /// The missile has no name anywhere in the game's own words, so the phrase is the mod's.
        /// </summary>
        private void AddProjectiles(GraphBuilder builder)
        {
            for (int i = 0; i < _projectiles.Count; i++)
            {
                ObliteratorProjectileLabel it = _projectiles[i];
                ObliteratorProjectile shot = it.Entity as ObliteratorProjectile;
                if (shot == null)
                {
                    continue;
                }

                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(
                            () => ModStrings.Get(ModStrings.GalaxyObliteratorProjectile)
                        ),
                        GraphNodes.ValuePart(() => Owner(shot.Empire), false),
                        GraphNodes.ValuePart(
                            () => Countdown(it.DurationBackground, it.DurationLabel),
                            false
                        ),
                    },
                    Sections = GraphNodes.Sections(null, it.Tooltip),
                };
                Follow(vtable, shot, it.AgeTransform, it.Tooltip);
                builder.AddItem(ControlId.Structural("galaxy:projectile/" + shot.GUID), vtable);
            }
        }

        /// <summary>
        /// The pins allies drop on the map to say "attack here", "defend this", "look at that".
        ///
        /// Only ever drawn in a game with allies in it, so nothing here can be tried outside one; it is
        /// modelled from what the game draws (<c>CoordinationRequestLabel</c>) and kept to that. Each
        /// pin says what KIND of request it is - the game's own word for the type - and reads back the
        /// message its owner typed on it, with the sentence naming the sender reviewable underneath.
        /// Letting go of one is the button the game draws on it, as a child, and only while it draws it.
        ///
        /// Editing the message is NOT offered: the game puts a live text field on the pin, and the mod's
        /// text editing is a screen of its own rather than a node on a map.
        /// </summary>
        private void AddPins(GraphBuilder builder)
        {
            for (int i = 0; i < _pins.Count; i++)
            {
                CoordinationRequestLabel it = _pins[i];
                CoordinationRequest request = it.CoordinationRequest;
                if (request == null)
                {
                    continue;
                }

                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => PinKind(request)),
                        GraphNodes.ValuePart(() => PinMessage(it), false),
                    },
                    Sections = GraphNodes.Sections(
                        NodeSection.Buffer(AgeWidgets.TooltipLines(it.SenderTooltip)),
                        GraphNodes.TooltipSection(it.RequestTooltip)
                    ),
                };
                Follow(vtable, request, it.AgeTransform, it.RequestTooltip);

                string key = "galaxy:pin/" + request.GUID;
                ControlId id = ControlId.Structural(key);
                AgeTransform dismiss = it.DismissButtonContainer;
                if (!Visible(dismiss))
                {
                    builder.AddItem(id, vtable);
                    continue;
                }

                vtable.ControlType = ControlTypes.Group;
                builder.BeginGroup(id, vtable);
                if (builder.IsExpanded(id))
                {
                    List<CardActions.CardAction> found = new List<CardActions.CardAction>(1);
                    CardActions.AddRefusable(found, dismiss, CardActions.NameFromTooltip(dismiss));
                    CardActions.Emit(builder, key, found);
                }

                builder.EndGroup();
            }
        }

        /// <summary>What the game calls this kind of request, in its own words.</summary>
        private static string PinKind(CoordinationRequest request)
        {
            try
            {
                return AgeText.Clean(
                    Gui.Localize(
                        "%CoordinationTools" + request.RequestType + "CoordinationRequestTitle"
                    )
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The words on the pin, as its own field is drawing them.</summary>
        private static string PinMessage(CoordinationRequestLabel label)
        {
            try
            {
                AgeControlTextField field = label.TextField;
                return field == null || !Visible(field.AgeTransform)
                    ? null
                    : AgeText.Label(field.Label);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whose a thing out in space is. A name, not a phrase: it sits beside the thing's own
        /// name the way a fleet's does, and every empire in this game has one.</summary>
        private static string Owner(Empire empire)
        {
            try
            {
                return empire == null ? null : AgeText.Clean(empire.LocalizedName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The turns a thing in flight has left, but only where the map is drawing the number:
        /// the game hides that background for anything that is not the player's own, and a hidden label
        /// still holds whatever was written on it for the last thing that needed it.</summary>
        private static string Countdown(AgeTransform background, AgePrimitiveLabel label)
        {
            return Visible(background) ? AgeText.Label(label) : null;
        }

        /// <summary>Focus follows the thing across the map, exactly as it follows a system: the camera is
        /// asked for it - the game's own "show me this" route, which takes a thing with a position rather
        /// than a place - and the pointer is put on its label so the game draws its dossier where it can
        /// be read. The camera only where the cursor MOVED, for the reason <see cref="AddSystem"/>
        /// records: a page being re-entered re-seats its cursor, and that is not a player going
        /// anywhere.</summary>
        private static void Follow(
            NodeVtable vtable,
            IGameEntityWithGalaxyPosition entity,
            AgeTransform widget,
            AgeTooltip tooltip
        )
        {
            IGameEntityWithGalaxyPosition it = entity;
            AgeTransform anchor = widget;
            AgeTooltip tip = tooltip;
            vtable.OnFocusVisual = () =>
            {
                if (CursorMoved())
                {
                    GalaxyViewLevels.PanTo(it);
                }

                if (anchor != null)
                {
                    PointerFocus.MoveTo(null, tip, anchor);
                }
            };
            vtable.OnBlurVisual = ReleasePointer;
        }

        /// <summary>
        /// Take the camera to a fleet and select it.
        ///
        /// A fleet PARKED somewhere goes through the turn window's own routine for exactly that: it
        /// knows to wait for the galaxy view to finish coming back before it hands the fleet to the
        /// cursor, and getting that order wrong leaves the selection on nothing.
        ///
        /// A fleet UNDER WAY cannot go that way, and the game's routine says so by doing nothing at
        /// all: it finds the fleet's docking slot to aim the camera at, and a fleet between two systems
        /// has none, so it falls through to handing the fleet to a window that is not shown - which
        /// stashes it for the next time the window opens (<c>FleetsScreen.SelectIdleFleet</c> :672-682)
        /// and there never is one, because opening that window is what selecting a fleet does. So a
        /// moving fleet is selected the way every other "show me this fleet" in the game does it
        /// (<c>MilitaryScreen</c>, <c>NamedShipInfoPanel</c>): hand its cursor target to the selection,
        /// swap in the garrison cursor, and ask the camera for the fleet - in that order, because the
        /// panel's own visibility is gated on that cursor.
        /// </summary>
        private static void Select(Fleet fleet)
        {
            try
            {
                // While the map is waiting for a target, a click selects NOTHING - every targeting
                // cursor turns selection off (<c>ValidateSelection</c> false) and keeps the click for
                // its own confirm. So this key is consumed and silent here rather than swapping in the
                // garrison cursor, which would cancel the mode the player is in the middle of
                // (<see cref="CursorTargeting"/>).
                if (CursorTargeting.Aiming)
                {
                    return;
                }

                // Both routes below ask the camera for the fleet through the very call the mod watches
                // for the GAME sending the player somewhere (<see cref="GalaxyLocate"/>) - but the
                // cursor is already on this fleet, because this is the player's own key on its node.
                GalaxyLocate.Suppressed = true;
                if (FleetOrders.Orbit(fleet) != null)
                {
                    EndTurnWindow window = TurnWindow();
                    if (window != null && SelectIdleFleet != null)
                    {
                        SelectIdleFleet.Invoke(window, new object[] { fleet });
                        return;
                    }
                }

                SelectOnMap(fleet);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: selecting a fleet threw: " + e);
            }
            finally
            {
                GalaxyLocate.Suppressed = false;
            }
        }

        /// <summary>Select a fleet wherever it is standing on the map, and take the camera to it.
        /// </summary>
        private static void SelectOnMap(Fleet fleet)
        {
            GalaxyFleet galaxyFleet = OnMap(fleet);
            Amplitude.Unity.View.ICursorService cursors =
                Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
            if (galaxyFleet == null || galaxyFleet.CursorTarget == null || cursors == null)
            {
                return;
            }

            cursors.Select(galaxyFleet.CursorTarget);
            cursors.ChangeCursor(typeof(GalaxyGarrisonCursor), galaxyFleet);
            Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(fleet);
        }

        /// <summary>The map's own object for a fleet - the thing that carries its cursor target. The
        /// game keeps a repository of the ones it is drawing, which is the same list its other
        /// "show me this fleet" routes look through.</summary>
        private static GalaxyFleet OnMap(Fleet fleet)
        {
            IVisibleGalaxyFleetRepositoryService repository =
                Amplitude.Unity.Framework.Services.GetService<IVisibleGalaxyFleetRepositoryService>();
            if (repository == null || fleet == null)
            {
                return null;
            }

            ReadOnlyCollection<GalaxyFleet> fleets = repository.GalaxyFleets;
            for (int i = 0; i < fleets.Count; i++)
            {
                if (fleets[i] != null && fleets[i].Fleet != null && fleets[i].Fleet.GUID == fleet.GUID)
                {
                    return fleets[i];
                }
            }

            return null;
        }

        // The window keeps its "go to this fleet" routine to itself, and it is the only place the
        // whole recipe - dock slot, camera, view level, cursor, fleet panel - is written down.
        private static readonly MethodInfo SelectIdleFleet = Member("SelectIdleFleet");

        private static MethodInfo Member(string name)
        {
            try
            {
                return typeof(EndTurnWindow).GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new Type[] { typeof(Fleet) },
                    null
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: looking up " + name + " threw: " + e);
                return null;
            }
        }

        // ---- shared ----

        private static string Amount(float value, bool signed, int decimals)
        {
            try
            {
                return Gui.FormatAmount(value, true, Gui.Rounding.Floor, signed, decimals);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Press a control the way the engine presses it: every AGE button carries the object
        /// and the method name its own mouse handler sends to, so replaying that pair runs the
        /// window's own handler with no click that could land on whatever the mouse is over.</summary>
        private static void Press(AgeControlButton button)
        {
            try
            {
                GameObject target = button.OnActivateObject;
                string method = button.OnActivateMethod;
                if (target != null && !string.IsNullOrEmpty(method))
                {
                    target.SendMessage(method, button.gameObject, SendMessageOptions.DontRequireReceiver);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: pressing a control threw: " + e);
            }
        }

        /// <summary>Make the control look hovered while the cursor is on it. The tooltip handed over
        /// here is the widget's own, class-driven or not: a class tooltip has nothing to READ off the
        /// widget but plenty to SHOW, and showing it is the whole point of this hook.</summary>
        private static void Point(NodeVtable vtable, AgeControlButton button)
        {
            AgeControlButton it = button;
            vtable.OnFocusVisual = () =>
                PointerFocus.MoveTo(it, Transform(it).AgeTooltip, it.AgeTransform);
            vtable.OnBlurVisual = ReleasePointer;
        }

        /// <summary>The same for a widget with no button under it: a running total, one of the screen
        /// icons. Nothing lights up - there is nothing there to light - and the tooltip appears, which
        /// for these is the whole of what the pointer was ever for.</summary>
        private static void PointAt(NodeVtable vtable, AgeTransform widget)
        {
            AgeTransform it = widget;
            vtable.OnFocusVisual = () => PointerFocus.MoveTo(it, Raw(it), it);
            vtable.OnBlurVisual = ReleasePointer;
        }

        private static AgeTransform Lozenge(GarrisonsLabelButton button)
        {
            try
            {
                return button == null ? null : button.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly Action ReleasePointer = PointerFocus.Release;

        private static AgeTransform Transform(AgeControl control)
        {
            try
            {
                return control == null ? null : control.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTooltip Tooltip(AgeControl control)
        {
            return Tooltip(Transform(control));
        }

        /// <summary>
        /// A tooltip only if the widget itself has anything to read.
        ///
        /// An AGE tooltip comes in two kinds. One carries its words in its content field. The other
        /// names a CLASS and a target, and the words are assembled by a renderer at the moment the
        /// tooltip is drawn - a stat block laid out from live data, which is worth reading but is not
        /// there to be read from the widget. The banners and the resource strip are all the second
        /// kind, and their content fields still hold whatever the prefab was authored with - the
        /// panel's own name, most of the time. Reading it aloud would announce "StrategicResourceBanner"
        /// under every resource, so a tooltip that names a class has nothing for a caller that wanted
        /// words off the widget. <see cref="TooltipLines"/> is how those are read instead.
        /// </summary>
        private static AgeTooltip Tooltip(AgeTransform transform)
        {
            try
            {
                return Readable(Raw(transform));
            }
            catch (Exception)
            {
                return null;
            }
        }

        // One rule for "are this tooltip's words on the widget", shared with every other screen and
        // with the mode the readout picks - a private copy of it here disagreed about the "Simple"
        // class, which is how a tooltip came to be announced from Content and reviewed from a window
        // that had not been drawn.
        private static AgeTooltip Readable(AgeTooltip tooltip)
        {
            return AgeWidgets.Readable(tooltip);
        }

        private static void AddLine(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line) && !lines.Contains(line))
            {
                lines.Add(line);
            }
        }

        private static void AddTooltip(List<string> lines, AgeTooltip tooltip)
        {
            Func<IList<string>> source = TooltipLines(tooltip);
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

        /// <summary>A table of things - anomalies, curiosities, deposits - reads one line per thing,
        /// which is how it is drawn and how it is reviewed. Each line is what the item SAYS
        /// (<see cref="AgeWidgets.ItemText"/>), which for a table of bare icons is the name off its own
        /// wrapper: reading such a table as text read nothing at all.</summary>
        private static void AddWidgetLines(List<string> lines, AgeTransform widget)
        {
            if (widget == null || !Visible(widget))
            {
                return;
            }

            IList<AgeTransform> children = widget.Children;
            if (children == null || children.Count == 0)
            {
                AddLine(lines, AgeWidgets.ItemText(widget));
                return;
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (Visible(children[i]))
                {
                    AddLine(lines, AgeWidgets.ItemText(children[i]));
                }
            }
        }

        /// <summary>The first thing a tooltip says - what a control with no caption of its own is
        /// called, in the game's words.</summary>
        /// <summary>A widget's tooltip whatever kind it is - what a caller needs to SHOW one rather
        /// than to read it.</summary>
        private static AgeTooltip Raw(AgeTransform transform)
        {
            try
            {
                return transform == null ? null : transform.AgeTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What the player would read on a tooltip, resolved when they ask to read it.
        ///
        /// A tooltip that carries its words reads straight off the widget. One that names a class has
        /// none to carry, so it is read back off the tooltip the game has DRAWN - which focus is what
        /// makes appear. That the two are the same text is then true by construction rather than by
        /// this screen reimplementing however the game assembles a stat block.
        ///
        /// It follows that these lines exist a fraction of a second after focus arrives, not with it:
        /// the game waits out its hover delay before drawing. The buffer is filled again when the
        /// tooltip lands (<see cref="PointerFocus.DrawnTooltipChanged"/>), which is well before anyone
        /// can press a key to read it.
        /// </summary>
        private static Func<IList<string>> TooltipLines(AgeTooltip tooltip)
        {
            return AgeWidgets.TooltipLines(tooltip);
        }

        // A control inside a group the window has collapsed is still marked visible itself, so the
        // chain above it is what says whether the player can see it.
        private static bool Visible(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    if (!at.Visible)
                    {
                        return false;
                    }

                    at = at.Parent;
                }

                return widget != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Enabled(AgeTransform widget)
        {
            try
            {
                return widget != null && widget.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Empire PlayerEmpire()
        {
            try
            {
                return Gui.PlayerEmpire;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static EndTurnWindow TurnWindow()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<EndTurnWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GuiManager GuiService()
        {
            try
            {
                return Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

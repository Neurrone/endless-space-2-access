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
using ES2Access.UI.Input;
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
    /// drawn AT a system or ON a lane between two of them, and either way it is walked under a SYSTEM -
    /// after that system's planets and starlanes. A fleet in orbit hangs under the system it is
    /// orbiting; a fleet under way hangs under BOTH systems its lane runs between, saying which lane it
    /// is on and which way that lane leaves, because a lane is drawn map geometry and either end is a
    /// true answer to "where is it". A fleet crossing OPEN SPACE has no drawn road and hangs under its
    /// DESTINATION alone (<see cref="AddFreeMoving"/>), or at the top of the systems list where the map
    /// has not named that destination (<see cref="AddAdrift"/>). A list of
    /// every fleet in the empire, in a corner of its own, described a picture the map does not draw and
    /// made "where is it" a question the player had to answer from a sentence rather than from the tree
    /// they were already in. This is a DELIBERATE deviation from the game's own arrangement, approved as
    /// such: the map draws fleets as lozenges anchored to places, and the tree now says the same thing.
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
    /// A STARLANE IS A LEAF, and right on one TRAVELS. A lane whose far end the map has drawn the name of
    /// answers the right arrow by putting the cursor on that system's own node at the root of the stop,
    /// opening it, and taking the camera there - so "what is down this line" is answered by going, without
    /// walking back to a list of a hundred systems. It is a rebase, not a descent: every system has exactly
    /// ONE node wherever the player reached it from, so nothing is ever read twice under two names and the
    /// tree has a bottom by construction rather than by refusing lanes a level down. Backspace is the way
    /// back: each hop is remembered, and the key puts the cursor on the very lane it was pressed on, with
    /// the camera back where it started. A system that was only opened because a lane was travelled is
    /// closed again on the way out; one the player opened themselves is left exactly as they left it.
    /// Travelling is NOT a click - it posts no order and confirms no target, for the same reason opening
    /// a branch does not.
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
        // Public because a global key names it: Control+G goes to the map from anywhere the map stop is
        // declared (ModEntry.BindKeys).
        public static readonly object SystemStop = "galaxy:systems";

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

        /// <summary>The square of galaxy the player can sweep the map with instead of walking the tree
        /// (<see cref="GalaxyInspect"/>) - a mode of this page, so it lives and dies with it.</summary>
        private readonly GalaxyInspect _inspect;

        /// <summary>"What is near me, of this kind" - a list of one kind of thing at a time, sorted by
        /// distance from wherever the player is reading (<see cref="GalaxyScanner"/>). Not a mode: its
        /// chords are live alongside the tree and alongside the inspect cursor, which is why it hangs
        /// off the page rather than replacing anything.</summary>
        private readonly GalaxyScanner _scanner;

        public GalaxyHudScreen()
        {
            _inspect = new GalaxyInspect(this);
            _scanner = new GalaxyScanner(this);
        }

        /// <summary>The map's inspect cursor, for the scanner: it measures from the cursor while the
        /// mode is up, and sends the cursor to what it found.</summary>
        internal GalaxyInspect Inspect
        {
            get { return _inspect; }
        }

        /// <summary>The obliterator missiles this empire has been SHOWN - the same list
        /// <see cref="AddProjectiles"/> declares its nodes from, so the cell, the tree and the
        /// scanner cannot disagree about which shots are in flight.</summary>
        internal IList<SightedShot> SightedProjectiles
        {
            get { return _shots; }
        }

        /// <summary>The ally pins this empire has been shown - the same list
        /// <see cref="AddPins"/> declares its nodes from.</summary>
        internal IList<SightedPin> SightedPins
        {
            get { return _sighted; }
        }

        // Regions - what Alt and an arrow jump between - are declared only where a stop really has
        // two halves. A stop with one region swallows the key and moves nothing, which reads as the
        // key being broken rather than as there being nowhere else to go. The empire stop declares
        // none: its parts are its rows, and up and down already walk them.
        //
        /// <summary>Everything the map draws AT a place - every star the player has seen, whoever owns
        /// it, in the order the map reads. One region and not two: an empire's own colonies are not a
        /// part of the picture drawn apart from the rest, they are stars in the same sky, and splitting
        /// them off made the list run north to south twice (owner ruling 2026-08-16).</summary>
        private static readonly object SystemsRegion = "galaxy:systems/stars";

        /// <summary>The map's other half: what the game draws OUT BETWEEN the stars - a probe
        /// drifting, a planet-killer crossing, a pin an ally has dropped. None of them stands at a place
        /// (<see cref="Drifting"/>), so none of them can hang under one, and they are jumped to as a
        /// region of their own rather than being left at the end of a list of a hundred systems.</summary>
        private static readonly object OpenSpaceRegion = "galaxy:systems/space";

        /// <summary>How far up a parent chain to look before deciding it is not a chain.</summary>
        private const int MaxAncestors = 64;

        // Reused across builds rather than allocated per frame: the galaxy is walked whole to work
        // out which systems the player can see, and Build runs every tick.

        /// <summary>Every star the map is naming, in the order it reads - one list, colonies and
        /// everything else together (<see cref="SystemsRegion"/>).</summary>
        private readonly List<StarSystemNode> _systems = new List<StarSystemNode>();

        /// <summary>Every star the map is DRAWING and refusing to name, in the same reading order -
        /// the places the player has been told the position of and nothing else
        /// (<see cref="AddLocated"/>). Kept out of <see cref="_systems"/> rather than marked inside
        /// it: everything that list feeds says a system's real name - the type-ahead index over the
        /// closed constellations reads <c>LocalizedName</c> straight off the node - and a place the
        /// picture is not naming must not be findable by a name the picture has never shown.</summary>
        private readonly List<StarSystemNode> _located = new List<StarSystemNode>();

        /// <summary>Which of them are the empire's own, for the one word the row says about it. Kept
        /// apart from the order rather than sorted into it: whose a star is changes what its row says,
        /// never where the row sits.</summary>
        private readonly List<StarSystemNode> _colonies = new List<StarSystemNode>();

        /// <summary>The fleets crossing open space towards somewhere the map has not named - the ones
        /// with no system to hang under (<see cref="AddAdrift"/>).</summary>
        private readonly List<Fleet> _adrift = new List<Fleet>();

        // The same, for the three things the map draws away from any star. Each holds the LABEL rather
        // than the entity, because the label is what answers "is the game drawing this".
        private readonly List<ProbeLabel> _probes = new List<ProbeLabel>();

        /// <summary>The probes with the star each one is nearest to worked out, rebuilt with the rest
        /// of the map (<see cref="Anchor"/>).</summary>
        private readonly List<DriftingProbe> _drifting = new List<DriftingProbe>();

        private readonly List<ObliteratorProjectileLabel> _projectiles =
            new List<ObliteratorProjectileLabel>();
        private readonly List<CoordinationRequestLabel> _pins =
            new List<CoordinationRequestLabel>();

        /// <summary>The missiles and the pins the player has been shown, each with the label the map
        /// happens to be drawing for it attached - or without one, which is the ordinary case at a
        /// camera position that has culled it (<see cref="Sight"/>).</summary>
        private readonly List<SightedShot> _shots = new List<SightedShot>();
        private readonly List<SightedPin> _sighted = new List<SightedPin>();

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
        /// game must keep the Escape it answers itself, mode or no mode. The mod's OWN mode is the
        /// second: the inspect cursor is a surface the game has never heard of, so leaving it must not
        /// also raise the pause menu behind it (<see cref="GalaxyInspect"/>).</summary>
        public override bool ConsumesBack
        {
            get { return GalaxyInspect.Active || CursorTargeting.EscapeIsOurs; }
        }

        /// <summary>Typing does nothing while the inspect cursor is up. Up and Down are the cell's own
        /// moves for as long as the mode holds them (<see cref="AnyKey"/>), so the keys that step a
        /// search's results never reach the search - a search opened here would be one the player
        /// could hear and not walk. The letters are still taken from the game: a stray letter must be
        /// silence, not one of the map's own hotkeys (owner's decision 2026-08-26,
        /// <see cref="GalaxyInspect"/>).</summary>
        public override bool SuspendsTypeahead
        {
            get { return GalaxyInspect.Active; }
        }

        /// <summary>Whether a stop key is the map itself - the widget the inspect cursor and the
        /// scanner are both modes OF. Every other stop on this screen keeps its own keys while either
        /// is armed (<see cref="GalaxyInspect"/>, <see cref="GalaxyScanner"/>).</summary>
        internal static bool IsMapStop(object stopKey)
        {
            return Equals(stopKey, SystemStop);
        }

        /// <summary>Whether the tree cursor is standing on the map widget AT THIS MOMENT - the one
        /// question both of the map's own key sets ask before taking anything, so that a player
        /// standing on the zoom slider or the HUD buttons keeps every key those stops answer.</summary>
        internal static bool CursorOnMap()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GraphNode node = navigator == null ? null : navigator.CurrentNode;
            return node != null && IsMapStop(node.StopKey);
        }

        /// <summary>
        /// The inspect cursor takes the keys it means before anything else on the page sees them - the
        /// arrows, Enter, Escape and the two size keys - which is what makes it a MODE rather than a
        /// set of extra commands on whatever control the tree cursor happens to be standing on. Every
        /// other key falls through untouched, and so does every key at all while the cursor is standing
        /// somewhere other than the map: the mode is a mode of the MAP widget, and the zoom slider two
        /// stops above it keeps its own arrows whether or not a cell is armed
        /// (<see cref="GalaxyInspect"/>).
        /// </summary>
        public override bool AnyKey(string actionKey)
        {
            // The inspect cursor first, because while it is up the keys it names mean the cell and
            // nothing else. The scanner's chords are none of those, so it is offered every key the
            // cursor passed on - and it answers whether or not the cursor is up, which is what makes
            // the two work together rather than one displacing the other.
            return _inspect.HandleKey(actionKey) || _scanner.HandleKey(actionKey);
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
            // A mode already armed when the page is arrived at is not one the player has just armed,
            // and seating them on it would move the cursor for something that happened elsewhere. Its
            // bearings are still remembered, because the mode ENDING under the cursor is the page's to
            // answer whoever armed it (<see cref="SeatAfterProbeMode"/>).
            _armedProbe = ArmedProbeFleet();
            _armedProbeAt = _armedProbe == null
                ? null
                : FleetOrders.Orbit(_armedProbe) as StarSystemNode;
            _armedProbeGroup = _armedProbeAt == null ? null : SystemKey(_armedProbeAt) + "/launch";
        }

        public override void OnPop()
        {
            _zoom.Forget();
            _hud.Forget();
            // The panel's release, caught here because on this path it is never handed over: the page
            // goes away with the panel still up, so the close frame the watch would have answered on
            // never happens under this screen (<see cref="_releasedAcross"/>).
            // Never over a seat the page is already owed. One of the six zoom-in actions is pressed
            // FROM the panel and can take the page away with it (the discovery cutscene), so the
            // cursor is in the panel on exactly the pop whose landing is already spoken for - and the
            // action's own target is where the player was told they were going.
            _releasedAcross = _fleetPanel.Held != null
                && CursorInFleetPanel()
                && _seatTarget == SeatTarget.None
                ? _fleetPanel.Held
                : null;
            _fleetPanel.Forget();
            // A place the game asked to be looked at goes with the page: whatever replaced this one is
            // where the player now is, and a request answered on some later visit would move the cursor
            // for a reason nobody could remember.
            GalaxyLocate.Forget();
            _locating = null;
            // The inspect cursor is a mode of THIS page: whatever replaced the map is where the player
            // now is, and a square still drawn on a map nobody is looking at would be a mode nothing
            // could end.
            _inspect.Forget();
            // A constellation name the mod was holding drawn belongs to the map that is going away.
            ConstellationLabelHold.Release();
            // Where the camera was sent goes with the page too: whatever replaced this one may have
            // moved it anywhere, so the first place read on the way back is followed afresh
            // (<see cref="FollowPlace"/>).
            _cameraPlace = null;
            _cameraIn = false;
            _armedProbe = null;
            _armedProbeAt = null;
            _armedProbeGroup = null;
            // A seat still being waited for is KEPT. Six of the game's fleet actions answer the press by
            // taking the player somewhere - and the first arrival at a system plays the discovery
            // cutscene over the map, which stands this page down for as long as it runs. Forgetting the
            // seat there is the press being silently thrown away: the player heard "moves focus to the
            // first curiosity", sat through the cutscene, and came back to a cursor that had not moved
            // (owner-reported). So the wait is SUSPENDED and resumes when the map is back.
            SuspendActionSeat();
        }

        /// <summary>
        /// The player has moved the cursor on the map themselves: a seat still waiting for the game to
        /// draw its target has been overtaken and is dropped.
        ///
        /// The navigator cancels its own outstanding landing on the same keystrokes and calls this
        /// beside it (<see cref="Screen.CancelLandings"/>). It matters more here than it looks: the seat
        /// now outlives the page losing focus, so without this a press whose target the player walked
        /// away from would still be waiting minutes later.
        /// </summary>
        public override void CancelLandings()
        {
            ForgetActionSeat();
        }

        /// <summary>Whether a landing aimed at this page should be held rather than judged - true while
        /// the game is flying the camera between view levels, when what the map draws is a frame of a
        /// transition and not the page the landing was aimed at (<see cref="Screen.LandingSuspended"/>).
        /// </summary>
        public override bool LandingSuspended
        {
            get
            {
                return GalaxyViewLevels.ChangingLevel
                    || GalaxyViewLevels.CameraSettling
                    || _settling > 0;
            }
        }

        /// <summary>Whether the map is still binding the INSIDE of a system it has just been brought
        /// into - the one camera move on this page that changes what the rows already declared SAY
        /// (<see cref="Screen.BetweenViews"/>).
        ///
        /// Deliberately not <see cref="LandingSuspended"/>. That is true for any camera move at all,
        /// and a slide across the map at one distance changes nothing a row says: measured frame by
        /// frame on 2026-08-26, a type-ahead landing on the far system Sabel at overview zoom read
        /// "Sabel, -35, -5, group, Home System, colonized, 1 Fleet, ..., collapsed, 8 of 18" on the
        /// landing frame and on every frame after it, while <see cref="_settling"/> was armed to 3 by
        /// the slide. Holding a search keystroke for that would be latency bought for nothing.
        ///
        /// Coming INSIDE is the case that is real, and it is real twice over. Right into a collapsed
        /// system at overview zoom seated the cursor on "Sabel I, group, Medium Mediterranean,
        /// Inhospitable, collapsed, 1 of 9" and the very next frame the same row read "2 of 10" - the
        /// system had grown an "Open system" button in front of it - with the card's own words still
        /// arriving eight frames later. A type-ahead landing on the world Sabel IV inside the same shut
        /// system announced "Sabel IV, Large Gas Temperate, Inhospitable, 1 curiosity, 5 of 10", a
        /// LEAF, and nine frames later the same row was "Sabel IV, group, ..., collapsed, 5 of 10" -
        /// the curiosity had become a child and the row a branch. Both readings are said once and lost.
        ///
        /// <see cref="GalaxyViewLevels.ChangingLevel"/> would have been the natural flag for it and is
        /// not: it measured FALSE across both, because the map comes inside a system by snapping the
        /// camera rather than by changing view level. So the wait is the counter below, armed where the
        /// snap is made.
        /// </summary>
        public override bool BetweenViews
        {
            get { return GalaxyViewLevels.ChangingLevel || _binding > 0; }
        }

        /// <summary>Frames still to wait for the orbital surface of a system the camera has just been
        /// snapped into (<see cref="ViewBindFrames"/>).</summary>
        private int _binding;

        /// <summary>
        /// How long the map takes to finish binding the inside of a system after the camera snaps into
        /// it - measured 2026-08-26 as eight frames from the snap for a Right-in (the last change was
        /// the orbital card's own text) and eight for a type-ahead landing on a world (the last change
        /// was the world's curiosity becoming a child). Twelve is that plus a half again, and it is a
        /// FRAME count rather than a duration for the same reason its neighbours are: what is being
        /// waited for is the game's own refresh passes over the cards, not a wall-clock animation.
        ///
        /// It is a ceiling on how long a landing inside a system can be held before it speaks, so it
        /// buys accuracy with latency and nothing else: everything visual - the camera coming in, the
        /// review buffer, the pointer - already happened on the frame the key landed.
        /// </summary>
        private const int ViewBindFrames = 12;

        /// <summary>Frames still to wait after the camera has stopped, before a landing is allowed to
        /// announce itself (<see cref="MapSettleFrames"/>).</summary>
        private int _settling;

        /// <summary>
        /// How long after the camera stops the MAP takes to catch up with it.
        ///
        /// The camera settling is not the end of the arrival: what a row SAYS depends on what the map
        /// draws at the new distance, and the orbital cards a system grows when the camera comes in
        /// bind over several frames after the flight ends - the card's own words first and the row of
        /// buttons under it after that, which is what turns a planet's row from a leaf into a group.
        /// Measured 2026-08-22 on Osulo I: at the frame the camera stopped the row read
        /// "Osulo I, Medium Mediterrane., Colonized, 2 of 8" and 300 ms later
        /// "Osulo I, group, Medium Mediterrane., Colonized, collapsed, 2 of 8". A landing announces
        /// itself once, so the earlier reading is simply lost.
        ///
        /// Twenty frames is the same wait the fleet-action seat already spends on the same widgets
        /// (<see cref="SeatAfterFleetAction"/>), for the same measured reason.
        /// </summary>
        private const int MapSettleFrames = 20;

        /// <summary>
        /// How long a landing waits after the camera has been PUT somewhere rather than flown there.
        ///
        /// The twenty frames above are two things at once: the map catching up with a camera that has
        /// stopped, and the flight's own tail. A snap has no tail, and the surface a system's rows read
        /// from is up on the frame after it - measured 2026-08-22 with a wait on the mod's own gate
        /// (<c>GalaxyViewLevels.FocusedSystem</c> plus the orbital labels window shown): 1 frame and
        /// 0 ms after a snap against 8 frames and 598 ms after the flight. Three frames is that one
        /// plus margin for the card's own refresh pass, and it is what keeps the landing inside the
        /// third of a second the whole change was for.
        ///
        /// The camera never reports itself settling on this path, so this is armed by the snap itself
        /// rather than read off the controller (<see cref="Camera"/>).
        /// </summary>
        private const int SnapSettleFrames = 3;

        /// <summary>
        /// Frames still to spend asking the map to have another look at which of its star labels it
        /// should be drawing, after the camera has been PUT somewhere rather than flown there
        /// (<see cref="GalaxyViewLevels.CatchUpLabels"/>).
        ///
        /// Its own counter and not one of the two above, because it holds nothing back: the question is
        /// asked of the game while the announcement waits on <see cref="_binding"/> or does not wait at
        /// all, and the answer it is waiting for arrives three frames after the camera lands (measured
        /// 2026-08-26: the snap into Olvaldi on frame 20 of the run, its label drawn on frame 23). Both
        /// of the page's snaps arm it - coming inside a system and sliding across to something standing
        /// in open sky - because both leave the same map behind: measured the same day, a landing on
        /// Sabel's row at overview zoom centred the camera on a system the map was drawing no label for
        /// at all.
        ///
        /// The same twelve frames the binding hold uses (<see cref="ViewBindFrames"/>), for the same
        /// reason: it is the game's own refresh passes being waited for, and the count is generous
        /// against a measurement of three.
        /// </summary>
        private int _labelCatchUp;

        public override void OnUpdate()
        {
            // First, so that everything below decides against the same answer: the map is still
            // catching up with the camera for a little while after the flight ends
            // (<see cref="MapSettleFrames"/>).
            _settling = GalaxyViewLevels.CameraSettling
                ? MapSettleFrames
                : Math.Max(0, _settling - 1);
            _binding = Math.Max(0, _binding - 1);
            if (_labelCatchUp > 0)
            {
                _labelCatchUp--;
                GalaxyViewLevels.CatchUpLabels();
            }

            _hud.Update();
            FollowSelectionEnd(_fleetPanel.Update());
            // Beside it and not before it: the two answer the same handover by different routes, and a
            // release the page itself saw is the fresher of the two.
            FollowSelectionEndAcross();
            _zoom.Update();
            _inspect.Update();
            CheckTrailSession();
            // Before the camera is followed and before the graph is next built, so that the landing
            // and the branch it opens both happen on the frame the page arrives on.
            FollowTheGame();
            // After it, because the two write the same one landing slot and this one is the player's
            // own key of a moment ago - a probe armed on the same frame the game asked to be shown
            // somewhere is still armed, and the mode is where the player has to be.
            FollowProbeArming();
            // After both, and outstanding over several frames rather than answered on one: this
            // landing waits for the camera the game is still flying into the system.
            FollowActionSeat();
            // Before the visual is followed, so that a window put right is what the pointer is then
            // aimed at, on the one frame (<see cref="ShowFocusedSystem"/>).
            ShowFocusedSystem();
            FollowCamera();
            // Re-asserted rather than done once: the labels window re-marks its culling on every
            // camera MOVE, and would take a held label away underneath a standing cursor.
            ConstellationLabelHold.Tick();
        }

        /// <summary>
        /// Where the player is put when the launch-probe mode is armed: on the first of the sixteen
        /// bearings that mode offers (<see cref="AddProbeDirections"/>), with the acting fleet's system
        /// and that group both opened to get there.
        ///
        /// The button that arms the mode is on the fleet panel, and arming takes the whole panel off the
        /// screen (the game draws it for the garrison cursor alone) - so the control the player pressed
        /// is gone and the cursor is left wherever the rebuild's reconciliation puts it. That was the
        /// acting fleet's own system only when the player happened to be standing in that branch;
        /// having walked anywhere else in between, they were left with the mode up, its one keyboard
        /// control several stops away, and nothing saying where (owner-reported). So the mode seats the
        /// cursor itself, from wherever it was.
        ///
        /// Nothing is spoken here. The landing announces itself through the same path every focus
        /// change goes through, naming the group it entered and the bearing it is on, which is the
        /// whole of what there is to say.
        ///
        /// Watched by the FLEET the mode is armed for rather than by a bare flag, so re-arming - the
        /// same fleet after a cancel, or a second fleet - seats again, and a mode that simply goes on
        /// being up seats nothing.
        /// </summary>
        private void FollowProbeArming()
        {
            try
            {
                Fleet fleet = ArmedProbeFleet();
                if (ReferenceEquals(fleet, _armedProbe))
                {
                    return;
                }

                Fleet was = _armedProbe;
                StarSystemNode wasAt = _armedProbeAt;
                string wasGroup = _armedProbeGroup;
                _armedProbe = fleet;
                _armedProbeAt = null;
                _armedProbeGroup = null;
                if (fleet == null)
                {
                    SeatAfterProbeMode(was, wasAt, wasGroup);
                    return;
                }

                StarSystemNode node = FleetOrders.Orbit(fleet) as StarSystemNode;
                if (node == null)
                {
                    return;
                }

                // The branch first, the group inside it second, the cursor last - the order the frame
                // applies them in (<see cref="Arrive"/>): both expansions belong to the build that
                // declares the bearing the cursor is being sent to.
                string place = SystemKey(node);
                _armedProbeAt = node;
                _armedProbeGroup = place + "/launch";
                OpenPlace(node);
                _pendingExpand.Add(ControlId.Structural(place + "/launch"));
                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null)
                {
                    navigator.FocusNode(ControlId.Structural(place + "/launch/0"));
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: seating the probe's launch directions threw: " + e);
            }
        }

        /// <summary>The fleet the launch-probe mode is waiting on, or null while no probe mode is up.
        /// </summary>
        private static Fleet ArmedProbeFleet()
        {
            ProbeLaunchingCursor cursor = CursorTargeting.ArmedProbe;
            return cursor == null ? null : cursor.ProbeOriginFleet;
        }

        /// <summary>
        /// Where the player is put when letting go of the selection closes the panel they were
        /// standing in: the fleet's own row in the tree, the place the selection was ABOUT.
        ///
        /// Escape with a fleet selected deselects it, and the panel's three stops vanish with the
        /// selection - so a cursor on a fleet line, a ship or an action is on a node the next build no
        /// longer declares, and reconciliation's nearest-survivor fallback walks the old order backward
        /// onto whatever row happened to precede the panel (measured 2026-08-25: the map stop's last
        /// drifting probe, a place the player never was). The fleet still has a row, so the cursor goes
        /// there - or to its system's when the fleet itself is gone (a disband).
        ///
        /// Only when the panel closed into the PLAIN map cursor, because that is what tells "the
        /// selection was let go" from the other ways the panel leaves: a targeting mode closing it
        /// seats its own cursor (<see cref="FollowProbeArming"/>), a zoom-in action flies into the
        /// system (<see cref="SeatAfterFleetAction"/>), and this must overwrite neither landing.
        ///
        /// Made from the panel OR from the fleet's own row, and from nowhere else. Selecting from the
        /// row never moved the cursor, so there is nothing to hand back - but the handover is still
        /// made, onto the row the player is already standing on, because it is how this page says "the
        /// cursor is placed here" and the camera follows a placement wherever it is made
        /// (<see cref="OnFocusVisual"/>). Without it the Escape that closed the panel left the camera
        /// on the docking slot the game had framed for the selection, with the system unfocused and
        /// its orbital cards gone, until the player pressed an arrow (owner-reported 2026-08-26). A
        /// player reading the HUD when they let go has lost nothing, is reading nothing on the map,
        /// and is left exactly where they are.
        /// </summary>
        private void FollowSelectionEnd(Fleet released)
        {
            if (released == null
                || !(Gui.GetCursor() is GalaxyCursor)
                || !(CursorInFleetPanel() || CursorAtFleetRow(released)))
            {
                return;
            }

            SeatOnFleet(released, null);
        }

        /// <summary>Whether the cursor is already standing exactly where a seat would put it - the
        /// "never left it" half of the question above, asked of the same index the seat aims with
        /// (<see cref="SeatOnFleet"/>) so the two cannot disagree about which row is the fleet's.
        /// </summary>
        private bool CursorAtFleetRow(Fleet fleet)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GraphNode standing = navigator == null ? null : navigator.CurrentNode;
            if (standing == null || standing.Id == null)
            {
                return false;
            }

            try
            {
                List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
                for (int i = 0; i < sites.Count; i++)
                {
                    if (ReferenceEquals(sites[i].Fleet, fleet))
                    {
                        return standing.Id.Equals(sites[i].Node);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: asking whether the cursor is on a fleet's own row threw: " + e);
            }

            return false;
        }

        /// <summary>Whether the cursor is standing in one of the panel's three stops - the question
        /// every one of these handovers asks, because a player who was reading the map or the HUD when
        /// the panel went has lost nothing and must be left where they are.</summary>
        private static bool CursorInFleetPanel()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GraphNode standing = navigator == null ? null : navigator.CurrentNode;
            object stop = standing == null ? null : standing.StopKey;
            return FleetPanel.ManagementStop.Equals(stop)
                || FleetPanel.ShipsStop.Equals(stop)
                || FleetPanel.ActionsStop.Equals(stop);
        }

        /// <summary>
        /// Put the cursor on a fleet's own row on the map - the place the panel that has just gone was
        /// ABOUT - or on its system's row when the fleet itself is no longer drawn (a disband, a fleet
        /// lost). Answers whether it aimed at anything.
        ///
        /// The one seat every fleet-panel handover shares: the selection being let go
        /// (<see cref="FollowSelectionEnd"/>), a targeting mode ending under the cursor
        /// (<see cref="FollowProbeArming"/>), and the panel being taken away by a screen drawn over
        /// the map (<see cref="_releasedAcross"/>).
        ///
        /// It says nothing about the camera, and none of the handovers do: seating the cursor IS a
        /// placement, and the page's one camera rule answers every placement alike
        /// (<see cref="OnFocusVisual"/>). So the camera comes back in on the place the player is left
        /// reading - or stays where the player put it by hand, which is the record's decision to make
        /// (<see cref="Showing"/>) and not a handover's.
        ///
        /// <paramref name="home"/> is the system to fall back to where the caller knows it from
        /// before the fleet went - the fleet's own orbit answers it in every other case.
        /// </summary>
        private bool SeatOnFleet(Fleet fleet, StarSystemNode home)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null || fleet == null)
            {
                return false;
            }

            try
            {
                List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
                for (int i = 0; i < sites.Count; i++)
                {
                    if (ReferenceEquals(sites[i].Fleet, fleet))
                    {
                        navigator.FocusNode(Reveal(sites[i]));
                        return true;
                    }
                }

                StarSystemNode at = home != null ? home : FleetOrders.Orbit(fleet) as StarSystemNode;
                ControlId id = SystemId(at);
                if (id == null)
                {
                    return false;
                }

                navigator.FocusNode(id);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: seating the cursor on a fleet's own row threw: " + e);
                return false;
            }
        }

        /// <summary>
        /// The fleet the panel was up for when this page was taken away with the panel still open, or
        /// null the rest of the time.
        ///
        /// A full screen drawn over the map - the military screen, the academy, anything the player
        /// opens from the HUD - pops this page, and opening one also force-swaps the map to the plain
        /// cursor and clears the fleet selection (<c>GuiManager.cs:1783-1795</c>). So the panel closes
        /// while nobody is watching: the close frame the watch would have handed the fleet over on
        /// happens with this screen off the stack, and coming back left the cursor wherever
        /// reconciliation put it - measured 2026-08-26 on an unrelated fleet's row three stops away.
        /// The release is therefore CAUGHT at the pop and answered on the first frame this page is the
        /// focused one again, which makes the trip through the screen invisible: the player comes back
        /// where letting the fleet go would have put them anyway. It is dropped unanswered when the
        /// panel came back up with the page - then nothing was taken from under the cursor.
        /// </summary>
        private Fleet _releasedAcross;

        /// <summary>Answer a release caught at the pop, on the first frame back on the page - not at
        /// the push itself, where the navigator has not yet been handed this screen and a landing
        /// asked for there would belong to the screen the player was leaving.</summary>
        private void FollowSelectionEndAcross()
        {
            Fleet released = _releasedAcross;
            _releasedAcross = null;
            if (released != null && !_fleetPanel.Available())
            {
                SeatOnFleet(released, null);
            }
        }

        /// <summary>
        /// Where the player is put when the launch-probe mode ENDS - cancelled, or spent on the last
        /// charge: back on the acting fleet's own row, the same place letting go of the selection puts
        /// them (<see cref="SeatOnFleet"/>).
        ///
        /// Only while the cursor is standing among the bearings themselves, because those are the
        /// nodes the mode's end takes away: the group is declared for as long as the mode is up and
        /// for no longer (<see cref="AddProbeDirections"/>), so a cursor inside it is about to be left
        /// on nothing and reconciliation would walk it backwards onto whatever row happened to be
        /// drawn last at that system. A cursor anywhere else is on a node that SURVIVES the mode
        /// ending, and nothing may move it - the same limit the selection-end seat keeps.
        ///
        /// Both the fleet and the group are read from what was remembered at ARMING time: by the frame
        /// the mode ends the cursor object is gone, and with it the only live route to either.
        /// </summary>
        private void SeatAfterProbeMode(Fleet was, StarSystemNode at, string group)
        {
            if (was == null || group == null)
            {
                return;
            }

            GraphNavigator navigator = ModEntry.Navigator;
            GraphNode standing = navigator == null ? null : navigator.CurrentNode;
            ControlId id = standing == null ? null : standing.Id;
            string key = id == null ? null : id.StructuralKey as string;
            if (key == null || (key != group && !key.StartsWith(group + "/")))
            {
                return;
            }

            SeatOnFleet(was, at);
        }

        /// <summary>The fleet the probe mode was armed for when it was last looked at - instance state,
        /// so it is reload-safe and each page keeps its own.</summary>
        private Fleet _armedProbe;

        /// <summary>The system that fleet was launching from, and the key of the group of bearings
        /// offered there - both remembered from the frame the mode was armed, for the frame it ends
        /// (<see cref="SeatAfterProbeMode"/>).</summary>
        private StarSystemNode _armedProbeAt;

        private string _armedProbeGroup;

        // ---- the fleet actions that only bring the camera in ----

        /// <summary>
        /// Which control INSIDE the fleet's own system a fleet action's button is really asking for.
        ///
        /// Six of the game's fleet actions order nothing when they are pressed: Colonize, Super
        /// Colonize, Destroy Planet, Expedition, Launch Mining Probe and Reclaim Mothership all just
        /// select the fleet's system and fly the camera in (<c>FleetActionButtonColonize.OnClick</c>
        /// and its four siblings; <c>FleetActionToggleReclaimMothership.OnToggle</c>), because the real
        /// order is a control the map draws once it is there - a planet's own colonize or destroy
        /// button, a curiosity in orbit, a probe site, the wreck. <see cref="None"/> is every other
        /// action: the ones that post an order themselves and the ones that arm a targeting cursor.
        /// </summary>
        public enum SeatTarget
        {
            None,
            Colonize,
            Destroy,
            Expedition,
            MiningProbe,
            Wreck,
        }

        /// <summary>Which of the six, if any, this action button is - asked of the GAME's own control
        /// class rather than of the definition name, because that is what decides the click's
        /// behaviour. Super Colonize is a subclass of Colonize and lands on the same card button, which
        /// is the game's own arrangement (<c>PlanetLabel_SystemOrbital.RefreshColonizationButton</c>
        /// drives one button from both).</summary>
        public static SeatTarget SeatTargetOf(FleetActionItem item)
        {
            try
            {
                FleetActionControl control =
                    item == null ? null : item.GetComponent<FleetActionControl>();
                if (control is FleetActionButtonColonize)
                {
                    return SeatTarget.Colonize;
                }

                if (control is FleetActionButtonDestroyPlanet)
                {
                    return SeatTarget.Destroy;
                }

                if (control is FleetActionButtonExpedition)
                {
                    return SeatTarget.Expedition;
                }

                if (control is FleetActionButtonLaunchMiningProbe)
                {
                    return SeatTarget.MiningProbe;
                }

                if (control is FleetActionToggleReclaimMothership)
                {
                    return SeatTarget.Wreck;
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a fleet action's control class threw: " + e);
            }

            return SeatTarget.None;
        }

        /// <summary>The phrase such a button appends to its own announcement, or null where it has
        /// nothing to add.</summary>
        public static string SeatPhrase(SeatTarget seat)
        {
            switch (seat)
            {
                case SeatTarget.Colonize:
                    return ModStrings.FleetsActionSeatsColonize;
                case SeatTarget.Destroy:
                    return ModStrings.FleetsActionSeatsDestroy;
                case SeatTarget.Expedition:
                    return ModStrings.FleetsActionSeatsExpedition;
                case SeatTarget.MiningProbe:
                    return ModStrings.FleetsActionSeatsProbeSite;
                case SeatTarget.Wreck:
                    return ModStrings.FleetsActionSeatsWreck;
            }

            return null;
        }

        /// <summary>
        /// One of the six has just been pressed: open the acting fleet's system and put the cursor on
        /// the control that gives the order.
        ///
        /// Asked for rather than done, and asked for over several frames: the game answers the click by
        /// flying the camera in, and the cards, the curiosities and the wrecks are all drawn by windows
        /// that bind to the system the camera ARRIVES at - none of them exists on the frame the button
        /// was pressed. So the target is looked for every frame until it is there, and the seat itself
        /// goes through the same pending-focus path every other screen-driven landing uses, because the
        /// tree re-declares itself each frame and the row only exists in the build that follows the
        /// branch being opened.
        ///
        /// The branch is opened straight away whatever happens next, so an action whose target the
        /// fixture has nothing to offer still leaves the player in the system the game flew them to.
        /// Nothing is spoken here: the landing announces itself.
        /// </summary>
        public void SeatAfterFleetAction(StarSystemNode system, SeatTarget seat)
        {
            if (system == null || seat == SeatTarget.None)
            {
                return;
            }

            _seatSystem = system;
            _seatTarget = seat;
            _seatFrames = SeatWaitFrames;
            OpenPlace(system);
        }

        /// <summary>About five seconds of frames - several times the camera's own flight into a system,
        /// and short enough that a target the game never draws stops being looked for.</summary>
        private const int SeatWaitFrames = 300;

        /// <summary>
        /// How long the answer has to STOP CHANGING before the cursor is sent to it - a third of a
        /// second.
        ///
        /// A card's buttons do not all appear on one frame: the window blanks every one of them when it
        /// binds a planet and its refresh turns back on the ones that apply, so a card that ends up
        /// drawing Colonize and a curiosity draws the curiosity alone for a frame or two first. The row
        /// id is the button's POSITION in the card's action list, so seating on that frame put the
        /// cursor on the curiosity's id - which the very next build handed to Colonize (measured
        /// 2026-08-20: the seat spoke "Signal" and the cursor was reading "Colonize" a frame later).
        /// </summary>
        private const int SeatSteadyFrames = 20;

        private StarSystemNode _seatSystem;
        private SeatTarget _seatTarget;
        private int _seatFrames;
        private ControlId _seatRow;
        private ControlId _seatGroup;
        private int _seatSteady;

        /// <summary>Per frame while a seat is outstanding: the target once the map has settled on
        /// drawing it, the planet branch it hangs in opened in the same breath (the order the build
        /// applies them in), and nothing at all once the wait has run out.</summary>
        private void FollowActionSeat()
        {
            if (_seatTarget == SeatTarget.None)
            {
                return;
            }

            try
            {
                if (GalaxyViewLevels.ChangingLevel)
                {
                    // The camera is still on its way. Nothing the map draws mid-flight is the answer -
                    // the cards belong to wherever the view is leaving - so the frames a flight takes
                    // are not the wait's to spend, and whatever the last frame settled on is dropped
                    // rather than counted towards the steady run.
                    _seatRow = null;
                    _seatSteady = 0;
                    return;
                }

                if (--_seatFrames <= 0)
                {
                    ForgetActionSeat();
                    return;
                }

                ControlId group;
                ControlId row = SeatRow(_seatSystem, _seatTarget, out group);
                if (row == null)
                {
                    _seatRow = null;
                    _seatSteady = 0;
                    return;
                }

                if (row.Equals(_seatRow))
                {
                    _seatSteady++;
                }
                else
                {
                    _seatRow = row;
                    _seatGroup = group;
                    _seatSteady = 1;
                }

                if (_seatSteady < SeatSteadyFrames)
                {
                    return;
                }

                if (_seatGroup != null)
                {
                    _pendingExpand.Add(_seatGroup);
                }

                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null)
                {
                    navigator.FocusNode(_seatRow);
                }

                ForgetActionSeat();
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: seating a fleet action's target threw: " + e);
                ForgetActionSeat();
            }
        }

        private void ForgetActionSeat()
        {
            _seatTarget = SeatTarget.None;
            _seatSystem = null;
            _seatFrames = 0;
            SuspendActionSeat();
        }

        /// <summary>Keep the wait but throw away everything it had settled on: the row it was closing in
        /// on was an index into a card the map is no longer drawing, and the run of frames it had held
        /// steady for says nothing about the map the player will come back to. What survives is the
        /// action, the system and the budget left.</summary>
        private void SuspendActionSeat()
        {
            _seatRow = null;
            _seatGroup = null;
            _seatSteady = 0;
        }

        /// <summary>
        /// The row the map is drawing for this action's target, and the branch it hangs in - both null
        /// until the game has drawn it.
        ///
        /// The index is worked out from the very list the tree builds the row from
        /// (<see cref="OrbitalActions"/>), never guessed from the order the card's buttons are
        /// declared in: which of them are drawn changes with the planet, so a fixed index would name a
        /// different button on the next world.
        /// </summary>
        private static ControlId SeatRow(StarSystemNode node, SeatTarget seat, out ControlId group)
        {
            group = null;
            if (node == null)
            {
                return null;
            }

            string place = SystemKey(node);
            if (seat == SeatTarget.Wreck)
            {
                return FirstWreckRow(node, place);
            }

            PlanetLabel_SystemOrbital[] cards = OrbitalLabels(node);
            if (cards.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < node.Planets.Count; i++)
            {
                PlanetLabel_SystemOrbital card = CardFor(node.Planets[i], cards);
                AgeTransform want = card == null ? null : SeatWidget(card, seat);
                if (want == null)
                {
                    continue;
                }

                List<CardActions.CardAction> actions = OrbitalActions(card);
                for (int j = 0; j < actions.Count; j++)
                {
                    if (!ReferenceEquals(actions[j].Widget, want))
                    {
                        continue;
                    }

                    string key = place + "/planet/" + i;
                    group = ControlId.Structural(key);
                    return ControlId.Structural(key + "/action/" + j);
                }
            }

            return null;
        }

        /// <summary>Which of the card's own controls this action is really after. A button the game is
        /// not drawing is simply not in the card's action list, so no drawn-ness test is needed here -
        /// the search below fails and the next planet is tried.</summary>
        private static AgeTransform SeatWidget(PlanetLabel_SystemOrbital card, SeatTarget seat)
        {
            switch (seat)
            {
                case SeatTarget.Colonize:
                    return AgeWidgets.Transform(card.ColonizeButton);
                case SeatTarget.Destroy:
                    return AgeWidgets.Transform(card.DestroyButton);
                case SeatTarget.MiningProbe:
                    return AgeWidgets.Transform(card.MiningProbeButton);
                case SeatTarget.Expedition:
                    return FirstCuriosity(card);
            }

            return null;
        }

        /// <summary>The first curiosity the card is drawing - PAINTED, the same gate
        /// <see cref="AddCuriosities"/> declares them by, because the ring pools its items and retires
        /// a surplus one by fading it rather than hiding it.</summary>
        private static AgeTransform FirstCuriosity(PlanetLabel_SystemOrbital card)
        {
            AgeTransform table = card.PlanetCuriositiesTable;
            if (table == null || !Visible(table))
            {
                return null;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (AgeWidgets.Painted(items[i]))
                {
                    return items[i];
                }
            }

            return null;
        }

        /// <summary>The first wreck row this system has, which is always index 0 of the group
        /// <see cref="AddWrecks"/> emits - that list holds the visible items alone, in order.</summary>
        private static ControlId FirstWreckRow(StarSystemNode node, string place)
        {
            WreckedMothershipLabelWindow window = WreckWindow(node);
            AgeTransform table = window == null ? null : window.CuriositiesTable;
            IList<AgeTransform> items = table == null ? null : table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] != null && Visible(items[i]))
                {
                    return ControlId.Structural(place + "/wreck/action/0");
                }
            }

            return null;
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
            // ...and WHICH system the orbital window's star tooltip is describing. There is one of that
            // tooltip and the window re-points it at whatever the camera is looking at, which it does a
            // few frames after the camera's own answer changes - so the pair above can be settled while
            // the widget the pointer was aimed at still holds the system the player came FROM, and it
            // stays that way for as long as the cursor stands still.
            object star = OrbitalStarSubject();
            if (
                ReferenceEquals(focused, _cameraSystem)
                && orbital == _cameraOrbital
                && ReferenceEquals(star, _cameraStar)
            )
            {
                return;
            }

            _cameraSystem = focused;
            _cameraOrbital = orbital;
            _cameraStar = star;
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator != null)
            {
                navigator.ClearVisual();
            }
        }

        /// <summary>What the camera was showing last frame - the three things the choice of a focused
        /// system's tooltip is made from.</summary>
        private StarSystemNode _cameraSystem;

        private bool _cameraOrbital;

        private object _cameraStar;

        /// <summary>
        /// Make the map draw the planets of the system the camera has come in on.
        ///
        /// The game binds its orbital labels window ONCE, as the window is shown
        /// (<c>PlanetLabelsWindow.OnBeginShow</c>), and shows it exactly when a system becomes focused
        /// after none was (<c>GuiManager</c>'s visibility pass reads
        /// <c>FocusedStarSystemNode != null</c>). With a mouse that is always how a system is reached:
        /// the camera FLIES, and part-way between two stars nothing is near enough to be focused
        /// (<c>GalaxyViewCameraController.GetGalaxyEntityToFocus</c>), so the window is hidden and
        /// shown again around every crossing and binds itself to wherever the camera ended up.
        ///
        /// The keyboard crosses in ONE frame - a landing snaps rather than flies (owner ruling
        /// 2026-08-22) - so the focus steps straight from one system to the next, the window is never
        /// hidden, and it goes on drawing the system the player came FROM. Measured 2026-08-24 on
        /// walking up into an expanded system's last child and on a type-ahead landing into another
        /// system's interior: the camera and <c>FocusedStarSystemNode</c> were both on the new system
        /// while the window still held the old one's cards, so the new system's planets had no cards at
        /// all and a world's tooltip fell back to the label circle the map parks at the top of the
        /// screen.
        ///
        /// So the invariant is asserted here every frame instead of being left to how the crossing was
        /// made: a window up over the wrong system is hidden and shown again - the game's own rebind,
        /// instant, so the cards are there on the same frame - and the system it was rebound for is
        /// remembered, so a bind that will not take cannot become a show every frame. Only ever a
        /// window the game already wants up: where it is not shown there is nothing to correct, and
        /// showing one the game means to keep hidden would draw a system over a screen.
        /// </summary>
        private void ShowFocusedSystem()
        {
            try
            {
                StarSystemNode focused = GalaxyViewLevels.FocusedSystem;
                PlanetLabelsWindow_SystemOrbital window = OrbitalWindow();
                if (focused == null || window == null || !window.Shown)
                {
                    _reboundFor = null;
                    return;
                }

                if (
                    ReferenceEquals(OrbitalSystem(), focused)
                    || ReferenceEquals(focused, _reboundFor)
                )
                {
                    return;
                }

                _reboundFor = focused;
                Gui.GuiService.HideWindow(window, true);
                Gui.GuiService.ShowWindow(window, true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: rebinding the orbital labels window threw: " + e);
            }
        }

        /// <summary>The system the last rebind was made for, so that a window which will not take the
        /// binding is left alone rather than hidden and shown on every frame.</summary>
        private StarSystemNode _reboundFor;

        /// <summary>Which system the orbital window is drawing - read off the star dossier it binds in
        /// the same call that binds its planets (<c>PlanetLabelsWindow_SystemOrbital.OnBeginShow</c>),
        /// which is the same answer without reaching for the window's own protected field.</summary>
        private static StarSystemNode OrbitalSystem()
        {
            GuiStarSystem star = OrbitalStarSubject() as GuiStarSystem;
            return star == null ? null : star.StarSystemNode;
        }

        /// <summary>What the orbital window's own star tooltip is currently about - the wrapper it is
        /// bound to, since the widget itself never changes and so says nothing about which system it
        /// is describing.</summary>
        private static object OrbitalStarSubject()
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = OrbitalWindow();
                AgeTooltip star = window == null ? null : window.StarTooltip;
                return star == null ? null : star.Target;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the focus visual being committed right now is the cursor having been
        /// PLACED - moved by the player or seated by this page - as opposed to this page being
        /// re-entered or the visual being re-taken where it already was. Everything on this page that
        /// moves the CAMERA for what is being read asks it first
        /// (<see cref="GraphNavigator.CursorMovedHere"/>).</summary>
        private static bool CursorMoved()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            return navigator != null && navigator.CursorMovedHere;
        }


        // ---- the camera follows the cursor ----

        /// <summary>
        /// THE camera rule of this page (owner ruling 2026-08-23): the camera goes where the cursor is
        /// reading, and it is one rule rather than one per kind of node.
        ///
        /// What the cursor is standing on resolves to a PLACE and to how close the player is to it -
        /// the system a row belongs to, or a thing drifting between the stars, and whether the cursor
        /// is ON that row or INSIDE it (a world, a card, a lane, a fleet, a quest marker). The camera
        /// is then asked for exactly that (<see cref="FollowPlace"/>), which does nothing at all while
        /// it is already showing it. So walking a system's children moves nothing, crossing into
        /// another system's children brings the camera in on that one, and a zoom the player made by
        /// hand is left where they put it for as long as they go on reading the same place.
        ///
        /// Only where the cursor was PLACED (<see cref="GraphNavigator.CursorMovedHere"/>): the player
        /// moved it, or a screen seated it - a handover out of the fleet panel, a landing, the answer
        /// to a reveal - all of which leave the player reading whatever they landed on and so all ask
        /// the same question. Coming back to this page re-seats the cursor where it was left, and the
        /// visual is re-taken whenever the camera changes what it draws for the focused system
        /// (<see cref="FollowCamera"/>) - flying the camera for either would take it off whatever the
        /// GAME has since centred it on, so neither is a placement.
        ///
        /// Only for the map's own stop. The HUD's stops, the view title and the zoom slider are
        /// controls of the page rather than places on it, and a page whose camera moved when the
        /// cursor reached the end-turn button would be reading somebody else's business.
        /// </summary>
        public override void OnFocusVisual(GraphNode node)
        {
            if (!CursorMoved() || node == null || !SystemStop.Equals(node.StopKey))
            {
                return;
            }

            object place;
            bool inside;
            if (Place(node, out place, out inside))
            {
                FollowPlace(place, inside);
            }
        }

        /// <summary>
        /// Where a node on the map stop stands, and whether the cursor is inside that place or on its
        /// own row.
        ///
        /// Anything the map draws OUT ON THE MAP rather than at a star comes first, wherever the tree
        /// happens to file its row (owner ruling 2026-08-26) - a probe under way, a missile in flight,
        /// an ally's pin, and a fleet away from any berth. Every one of those is its own place, with no
        /// inside: there is nothing at a bare point to come in on (the same distinction
        /// <see cref="MapLandings.Decide"/> makes).
        ///
        /// The fleet is the case that needed saying, because the tree files it under a STAR: a fleet
        /// crossing a lane has a row under each end of that lane, and one crossing open space a row
        /// under the system it is bound for - a filing that says where to LOOK for the row, not where
        /// the thing is. Resolving such a row to its system ancestor sent the camera into a star's
        /// orbital view to show a fleet that is not drawn in that picture at all. A fleet PARKED at a
        /// star really is at that star, and keeps the system-ancestor resolution and its zoom in on
        /// the berth.
        ///
        /// Otherwise the system ANCESTOR wins over anything nearer: the star's own dossier cards and a
        /// lane leaving it are things the map draws AT that system, and the place the player is reading
        /// is the system.
        /// </summary>
        private bool Place(GraphNode node, out object place, out bool inside)
        {
            place = null;
            inside = false;
            IGameEntityWithGalaxyPosition drawn = OpenSpaceThing(node == null ? null : node.Id);
            if (drawn != null)
            {
                place = drawn;
                return true;
            }

            for (GraphNode walk = node; walk != null; walk = walk.Parent)
            {
                StarSystemNode system = walk.Id == null ? null : walk.Id.Subject as StarSystemNode;
                if (system != null)
                {
                    place = system;
                    inside = !ReferenceEquals(walk, node);
                    return true;
                }
            }

            for (GraphNode walk = node; walk != null; walk = walk.Parent)
            {
                IGameEntityWithGalaxyPosition thing =
                    walk.Id == null ? null : walk.Id.Subject as IGameEntityWithGalaxyPosition;
                if (thing != null)
                {
                    place = thing;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The thing a row on this stop stands for, where the map draws that thing out on the map
        /// rather than at a star: a probe under way, an obliterator missile in flight, an ally's pin,
        /// and a fleet away from any berth - the four kinds whose rows this page keys STRUCTURALLY,
        /// so that <c>ControlId.Subject</c> is null for exactly the rows the camera most needs to
        /// name (<see cref="PositionOf"/> walks these same lists for the same reason).
        ///
        /// Resolved through the page's own indexes - the very lists the rows are declared from - and
        /// never by reading a row's KEY, which is a string this page builds and not a fact about the
        /// map. A fleet flying a lane has a row under each end of it and the index holds both, so
        /// either row answers.
        ///
        /// A fleet PARKED at a star is deliberately not one of these: the map draws it in the star's
        /// own berth, so the star is the place the player is reading and the row keeps its
        /// system-ancestor resolution.
        ///
        /// A quest marker planted out in the open - one whose <c>Node</c> is invalid, because the
        /// thing it was planted on has no node, a fleet in mid-lane - is one of these too. The mod's
        /// <c>Marker</c> is a struct and could never be a place (a boxed struct is a fresh reference
        /// every time, and the camera record compares places by reference), but the PIN inside it is
        /// a game entity of the game's own with a position that follows whatever it is stuck to, so
        /// the pin is what answers here. A marker standing at a system is not: it hangs under that
        /// system and lands as a place, exactly as a planet does.
        ///
        /// Null for every other row, which is what leaves the ordinary resolution untouched.
        /// </summary>
        private IGameEntityWithGalaxyPosition OpenSpaceThing(ControlId id)
        {
            if (id == null)
            {
                return null;
            }

            try
            {
                for (int i = 0; i < _drifting.Count; i++)
                {
                    if (id.Equals(ProbeId(_drifting[i])))
                    {
                        return _drifting[i].Probe;
                    }
                }

                for (int i = 0; i < _shots.Count; i++)
                {
                    ObliteratorProjectile shot = _shots[i].Shot;
                    if (shot != null && id.Equals(ProjectileId(shot)))
                    {
                        return shot;
                    }
                }

                for (int i = 0; i < _sighted.Count; i++)
                {
                    CoordinationRequest pin = _sighted[i].Request;
                    if (pin != null && id.Equals(PinId(pin)))
                    {
                        return pin;
                    }
                }

                List<QuestMarkers.Marker> markers = QuestMarkers.Of(PlayerEmpire());
                for (int i = 0; i < markers.Count; i++)
                {
                    if (!markers[i].Node.IsValid && id.Equals(MarkerRowId(markers[i])))
                    {
                        return markers[i].Pin;
                    }
                }

                List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
                for (int i = 0; i < sites.Count; i++)
                {
                    if (id.Equals(sites[i].Node) && Flying(sites[i].Fleet))
                    {
                        return sites[i].Fleet;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: asking what a row out on the map stands for threw: " + e);
            }

            return null;
        }

        /// <summary>
        /// The other way round: the ROW a thing the map draws out on the map has, for a landing that
        /// was handed the thing itself and needs to know where the cursor goes.
        ///
        /// This is what makes every way IN arrive the same way (owner ruling 2026-08-26). A go-to
        /// through the game's own reveal call - a mod notification's Show Location, the game's own
        /// Show Location button, a panel's locate, a table's double click - arrives naming the ENTITY
        /// (<see cref="GalaxyLocate.Request"/>), and without this the landing had to fall back on
        /// finding whatever the map draws NEAREST the point it was sent to
        /// (<see cref="Nearest"/>): right nearly always, a guess in principle, and no answer at all
        /// where two things stand within <see cref="Coincides"/> of each other.
        ///
        /// A FLEET is not here: its own branch in <see cref="FromEntity"/> answers first, because a
        /// fleet's row hangs under a system whose branch has to be opened before the cursor can be
        /// sent to it, and because a docked one is aimed at its BERTH. Everything else of this shape
        /// sits at the top of the stop with no branch to open.
        /// </summary>
        private ControlId OpenSpaceRow(IGameEntityWithGalaxyPosition thing)
        {
            if (thing == null)
            {
                return null;
            }

            try
            {
                for (int i = 0; i < _drifting.Count; i++)
                {
                    if (ReferenceEquals(_drifting[i].Probe, thing))
                    {
                        return ProbeId(_drifting[i]);
                    }
                }

                for (int i = 0; i < _shots.Count; i++)
                {
                    if (ReferenceEquals(_shots[i].Shot, thing))
                    {
                        return ProjectileId(_shots[i].Shot);
                    }
                }

                for (int i = 0; i < _sighted.Count; i++)
                {
                    if (ReferenceEquals(_sighted[i].Request, thing))
                    {
                        return PinId(_sighted[i].Request);
                    }
                }

                List<QuestMarkers.Marker> markers = QuestMarkers.Of(PlayerEmpire());
                for (int i = 0; i < markers.Count; i++)
                {
                    if (!markers[i].Node.IsValid && ReferenceEquals(markers[i].Pin, thing))
                    {
                        return MarkerRowId(markers[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: asking which row a thing out on the map has threw: " + e);
            }

            return null;
        }

        /// <summary>Whether the map draws this fleet away from any star - out on a lane, crossing open
        /// space, or stranded where a cancelled order left it. The game's own question
        /// (<c>FleetPosition.IsInOrbit</c>), and the one that decides whether the fleet is a place of
        /// its own or something the star it is parked at is showing.</summary>
        private static bool Flying(Fleet fleet)
        {
            try
            {
                return fleet != null && !fleet.IsDestroyed && !fleet.Position.IsInOrbit;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The star a fleet is parked at, asked of the fleet's own position rather than
        /// guessed from the map (<c>FleetPosition.GetOrbit</c>) - null for a fleet that is not in
        /// orbit at one.</summary>
        private static StarSystemNode Orbited(Fleet fleet)
        {
            try
            {
                return fleet == null ? null : fleet.Position.GetOrbit() as StarSystemNode;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Show the player a place - the ONE call on this page that moves the camera for the cursor,
        /// and the one record of where the camera has been sent.
        ///
        /// Three answers. A place the camera is already showing moves nothing - which is what makes a
        /// zoom-out by hand survive the rest of a system being read, and what makes the game's own
        /// "show me this" compose: by the time the cursor lands the camera is already there and this
        /// adds no second jump. Coming further IN on a system snaps rather than flies (owner ruling
        /// 2026-08-22: the flight was nine tenths of a second in which nothing could be said), and the
        /// landing's wait is armed here because the camera never reports itself flying afterwards
        /// (<see cref="SnapSettleFrames"/>). Anything else is the slide the map has always made across
        /// to a thing standing somewhere else.
        ///
        /// The camera is never taken back OUT here: stepping from a world up onto its own star is not
        /// a request to see less, and the ways out are the player's own (Backslash, closing the
        /// branch). So "already showing it" includes being further in than was asked for.
        ///
        /// <paramref name="asked"/> is an INTENT rather than a path: "take me there" said out loud - a
        /// go-to, the answer to a reveal (<see cref="Camera"/>) - as against the camera following what
        /// is being read. It moves whatever the record says, because a player who zoomed out by hand
        /// and then pressed go-to has asked to be taken there, and the record - which deliberately
        /// survives that zoom-out - would otherwise answer "already there" over a camera that is not.
        /// Everything else, the cursor being placed anywhere by anybody, comes through the rule above
        /// with the record left to decide.
        /// </summary>
        private void FollowPlace(object place, bool inside, bool asked = false)
        {
            if (place == null || (!asked && Showing(place, inside)))
            {
                return;
            }

            StarSystemNode system = place as StarSystemNode;
            try
            {
                // Every camera move the mod makes goes through the same GUI calls the GAME leads the
                // player with, so it is marked as ours or each arrow key would read as a reveal
                // (<see cref="GalaxyLocate.Suppressed"/>). PanTo marks its own.
                if (inside && system != null)
                {
                    GalaxyLocate.Suppressed = true;
                    GalaxyViewLevels.SnapTo(system);
                    _settling = SnapSettleFrames;
                    // The one move that changes what the rows already declared SAY, so it is also the
                    // one that holds a descend and an announcement (<see cref="BetweenViews"/>).
                    _binding = ViewBindFrames;
                    // A camera that is PUT somewhere leaves the map's own labels believing it never
                    // moved (<see cref="_labelCatchUp"/>).
                    _labelCatchUp = ViewBindFrames;
                    Remember(place, inside);
                    return;
                }

                IGameEntityWithGalaxyPosition entity = place as IGameEntityWithGalaxyPosition;
                if (entity != null)
                {
                    GalaxyViewLevels.PanTo(entity);
                    // It arrives AT ONCE, for the reason coming further in on a system does (owner
                    // ruling 2026-08-26, the 2026-08-22 snap ruling widened): the flight was a second
                    // of drifting in which nothing could be said, and it ended somewhere the player
                    // had already been told about. Every thing the map draws out on the map is
                    // reached this way - a probe, a missile, an ally's pin, a fleet away from its
                    // berth. The recentre is started the ordinary way and then finished the same
                    // frame; where the camera cannot be reached to finish it, the flight simply plays
                    // out and nothing breaks (<see cref="GalaxyViewLevels.Settle"/>).
                    //
                    // A slide onto a BARE POINT with nothing at it is not this: that is the inspect
                    // cell sweeping its own cursor, and it keeps its own handling
                    // (<see cref="Camera"/>).
                    GalaxyViewLevels.CenterOn(entity.GalaxyPosition, LandingDamping);
                    GalaxyViewLevels.Settle();
                    _settling = SnapSettleFrames;
                    _labelCatchUp = ViewBindFrames;
                    Remember(place, inside);
                }
            }
            finally
            {
                GalaxyLocate.Suppressed = false;
            }
        }

        /// <summary>
        /// Whether the camera is already showing this place, closely enough for what is being asked -
        /// the whole of what makes an arrow key inside a system move nothing.
        ///
        /// Believed only while nothing has moved the camera since the record was written
        /// (<see cref="GalaxyViewLevels.Moves"/>). That is the invariant this page keeps: the camera
        /// goes where the record says, or the record stops being believed. Everything else that moves
        /// the camera - the game flying to a fleet the player selected, a landing sliding across open
        /// sky, the inspect cell sweeping - counts its move there, and the count is what tells a record
        /// that still describes the picture from one that describes a picture nobody is looking at.
        /// Without it a fleet selected in its own system left the record saying "in on that system"
        /// over a camera the game had meanwhile framed on the fleet, and every later step among that
        /// system's planets was swallowed: no orbital cards, so no curiosity to act on
        /// (owner-reported 2026-08-26, measured the same day).
        /// </summary>
        private bool Showing(object place, bool inside)
        {
            return _cameraStamp == GalaxyViewLevels.Moves
                && ReferenceEquals(place, _cameraPlace)
                && (_cameraIn || !inside);
        }

        /// <summary>Write down where the camera has just been sent and how close - always AFTER the move
        /// itself, because the record is stamped with the count of moves made by anybody else
        /// (<see cref="Showing"/>) and the mod's own pan is one of the things that counts.</summary>
        private void Remember(object place, bool inside)
        {
            _cameraPlace = place;
            _cameraIn = inside;
            _cameraStamp = GalaxyViewLevels.Moves;
        }

        /// <summary>The player has closed a branch and the camera has come back out of it
        /// (<see cref="Collapse"/>): the place is still what the camera is looking at, but it is no
        /// longer INSIDE it, so opening the same system again brings the camera back in. Closing a
        /// branch the camera was never in on changes nothing.</summary>
        private void LeftPlace(StarSystemNode node)
        {
            if (ReferenceEquals(node, _cameraPlace))
            {
                _cameraIn = false;
            }
        }

        /// <summary>Where the camera has been sent and how close - the whole of what the rule above
        /// compares against. Not read off the camera: the game's own answer for "which system is the
        /// orbital view up over" (<see cref="GalaxyViewLevels.FocusedSystem"/>) is null at every zoom
        /// step but the last and lags a flight by its whole duration (measured 2026-08-23), so gating
        /// on it would re-snap after every zoom-out by hand and mis-answer mid-flight. Per page: a
        /// page that has been left knows nothing about where the next one put the camera.
        ///
        /// Written by <see cref="Remember"/> alone, and only ever by a caller that has just moved the
        /// camera itself: <see cref="FollowPlace"/> for the cursor, and the open-sky slide a landing
        /// makes (<see cref="Camera"/>). Every OTHER way the camera moves is COUNTED rather than
        /// recorded (<see cref="GalaxyViewLevels.Moves"/>), which is what keeps a record from
        /// outliving the picture it describes - including the flight the game makes for one of the six
        /// zoom-in fleet actions (<see cref="SeatAfterFleetAction"/>), where a snap onto the system the
        /// game is already flying into costs nothing and rescues a flight that never happened.
        /// </summary>
        private object _cameraPlace;

        private bool _cameraIn;

        /// <summary>The count of camera moves made by anybody else at the moment the record above was
        /// written (<see cref="Showing"/>).</summary>
        private int _cameraStamp;

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
                MapTarget target = Locate(wanted, out settled);
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
                Land(target, wanted);
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
            return _systems.Count > 0;
        }

        /// <summary>Speak what the landing needs saying beyond the node itself, then send the cursor.
        /// The node's own announcement is the whole of an ordinary landing - it names the place the game
        /// went to, which is the answer to the question that was asked.</summary>
        private void Land(MapTarget target, GalaxyLocate.Request wanted)
        {
            if (wanted.Quest != null)
            {
                Voice.Say(QuestLocated(wanted.Quest), false);
            }

            // Everything else - the cell, the cursor, the camera, and the "shown on the map" line a
            // request nothing answers gets - is the page's one landing (<see cref="GoTo"/>).
            GoTo(target, MapCamera.Auto);
        }

        /// <summary>
        /// GO AND LOOK AT THIS - the one landing on this page.
        ///
        /// Five things used to send the player somewhere on the map and each answered the same three
        /// questions for itself: does the free inspect cursor stay up, does the tree cursor move, and
        /// does the camera zoom or slide. They disagreed - the scanner jumped the CELL onto a planet,
        /// which is a thing the cell cannot read, leaving the player on a square of sky beside the
        /// world they asked for - so the rules live in one place now
        /// (<see cref="MapLandings.Decide"/>, off the engine and unit-tested) and every caller hands
        /// in a resolved <see cref="MapTarget"/>: the game's own show-location
        /// (<see cref="FollowTheGame"/>), the scanner's go-to, travelling a starlane
        /// (<see cref="Arrive"/>) and the go-to-location key.
        ///
        /// The camera moves are marked as the MOD's own (<see cref="GalaxyLocate.Suppressed"/>): the
        /// mod pans through the same calls the game leads the player with, and an unmarked pan here
        /// would come straight back round as a fresh locate request.
        ///
        /// Answers whether the cursor was sent to a node - false for a fleet the tree has no row for
        /// and for a point the map draws nothing at, both of which leave the caller to say its own
        /// piece.
        /// </summary>
        internal bool GoTo(MapTarget target, MapCamera camera)
        {
            try
            {
                MapLanding plan = MapLandings.Decide(target.Thing, GalaxyInspect.Live);
                if (plan.Unplaced || (target.Id == null && target.Select == null))
                {
                    // Owner ruling 2026-08-22: everything the game can point the player at is supposed
                    // to have a row, so a request that lands on nothing is a DEFECT to model and not a
                    // behaviour to fall back on. The camera HAS moved, so the player is told; the
                    // request is logged where the dev sweep can find it; and nothing else moves.
                    Log.Warn(
                        "galaxy go-to: nothing on the map stands at "
                            + target.At.x.ToString("F2")
                            + ", "
                            + target.At.z.ToString("F2")
                            + " - the tree has no row for what the game pointed at"
                    );
                    Voice.Say(ModStrings.Get(ModStrings.GalaxyShownOnMap), false);
                    return false;
                }

                if (plan.ExitInspect)
                {
                    GalaxyInspect.Dismiss();
                }

                if (plan.MoveCell)
                {
                    GalaxyPosition origin = GalaxyCoordinates.Origin();
                    _inspect.JumpTo(
                        MapCoordinates.Round(target.At.x - origin.X),
                        MapCoordinates.Round(target.At.z - origin.Y)
                    );
                }

                GraphNavigator navigator = ModEntry.Navigator;
                if (plan.FocusNode && target.Id != null && navigator != null)
                {
                    // Silent while the cell is what the player is reading: the tree move is felt when
                    // the mode ends, which is the whole of what it is for.
                    navigator.FocusNode(target.Id, plan.AnnounceNode);
                    if (plan.MoveCell)
                    {
                        _inspect.Reseat(target.Id, target.At);
                    }
                }

                if (target.Select != null && !plan.MoveCell)
                {
                    // The one fleet the tree has no row for: the map's own selection is this game's
                    // only "go to that fleet".
                    SelectFleet(target.Select);
                }

                Camera(target, camera, plan);
                return target.Id != null;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: going to a place on the map threw: " + e);
                return false;
            }
        }

        /// <summary>Bring the camera to what was landed on: IN on a place - through the page's one
        /// camera rule, so the landed node's own focus adds nothing on top (<see cref="FollowPlace"/>)
        /// - and a slide onto a bare point. Where the cell is driving, the cell has already slid and
        /// only a place's zoom is added on top - so the picture is the same whichever way the player is
        /// reading the map.</summary>
        private void Camera(MapTarget target, MapCamera wanted, MapLanding plan)
        {
            MapCameraMove move = plan.Camera;
            switch (wanted)
            {
                case MapCamera.Zoom:
                    move = MapCameraMove.Zoom;
                    break;
                case MapCamera.Slide:
                    move = MapCameraMove.Slide;
                    break;
                case MapCamera.None:
                    move = MapCameraMove.None;
                    break;
            }

            if (move == MapCameraMove.None)
            {
                return;
            }

            if (move == MapCameraMove.Zoom && target.System != null)
            {
                // Through the page's one camera rule (<see cref="FollowPlace"/>) rather than beside
                // it: a landing is the cursor being sent to a place, so it asks for the same thing
                // walking there asks for, and the rule then has nothing left to do when the landed
                // node's focus commits. That is also what makes the game's own show-location compose -
                // the record says the camera is already there.
                FollowPlace(target.System, true, true);
                return;
            }

            // Everything the map draws out on the map goes through that same rule, for the same
            // reason: the thing IS the place (<see cref="Place"/>), so the landing asks for exactly
            // what walking onto the row asks for, the record then says the camera is already there,
            // and the landed row's own focus has nothing left to do. It is also the route that works
            // from inside a star's orbital view, which a bare recentre does not leave.
            //
            // The target carries the thing where the landing already had it in hand; otherwise the
            // row's own id is looked up in the same indexes the rows were declared from. A DOCKED
            // fleet is not one of these - it is its star's, and falls through to the zoom above by
            // way of its row's focus.
            IGameEntityWithGalaxyPosition drawn = target.Standing;
            Fleet carried = drawn as Fleet;
            if (carried != null && !Flying(carried))
            {
                // A DOCKED fleet is its STAR's place (<see cref="Place"/>), so its landing asks the
                // camera for exactly what walking onto the row asks for - and the record then says the
                // camera is already there, which is what leaves the row's own focus nothing to do.
                //
                // Sliding onto the BERTH instead (the point the target carries, because that is where
                // the ship is DRAWN and where the inspect cell must open) put the camera somewhere the
                // row's focus moved it off again a beat later: the delayed second move the owner
                // reported from the military page's second click, and the same one every other way in
                // makes - the idle-fleet button, the named-ship panel, the fleet list - because all of
                // them end here. The berth stays the target's point; only the CAMERA is the star's.
                StarSystemNode berthed =
                    target.System != null ? target.System : Orbited(carried);
                if (berthed != null)
                {
                    FollowPlace(berthed, true, true);
                    return;
                }

                drawn = null;
            }

            if (drawn == null)
            {
                drawn = OpenSpaceThing(target.Id);
            }

            if (drawn != null)
            {
                FollowPlace(drawn, false, true);
                return;
            }

            try
            {
                // A slide across open sky is left as a slide: there is no card to wait for at the
                // other end, so it costs the announcement nothing and the picture stays readable to
                // a sighted player beside the keyboard.
                GalaxyLocate.Suppressed = true;
                GalaxyViewLevels.CenterOn(target.At, LandingDamping);
                // The camera is now on the thing that was landed on, standing at its own point, and
                // the record says so - or the slide went to a bare point, and the record is left not
                // believed (the slide counted itself: <see cref="GalaxyViewLevels.Moves"/>). Saying so
                // is what keeps the landed node's own focus from sliding the camera a second time.
                object landed = target.Id == null ? null : target.Id.Subject;
                if (landed is IGameEntityWithGalaxyPosition)
                {
                    Remember(landed, false);
                }
            }
            finally
            {
                GalaxyLocate.Suppressed = false;
            }
        }

        /// <summary>The same slide the inspect cursor moves the camera with, so an arrival looks like
        /// every other arrival on this map.</summary>
        private const float LandingDamping = 0.3f;

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

        /// <summary>
        /// What the game asked to be looked at, resolved to a target this page can land on.
        ///
        /// The QUEST marker first, where the reveal was a quest pin: the position is that marker's own
        /// (<c>ShowQuestLocation</c> picks the marker and then makes the ordinary position request with
        /// it), so the marker is found by IDENTITY among the quest's own and never by guessing which
        /// thing near the point was meant. Then the thing itself, since a request that named one is
        /// exact; then the point, and only for something standing at it.
        ///
        /// <paramref name="settled"/> is false while a later frame could answer better - the whole of
        /// what the frame budget is for.
        /// </summary>
        private MapTarget Locate(GalaxyLocate.Request wanted, out bool settled)
        {
            MapTarget marker;
            if (LocatedMarker(wanted, out marker))
            {
                settled = true;
                return marker;
            }

            MapTarget named;
            if (FromEntity(wanted.Entity, out named))
            {
                settled = true;
                return named;
            }

            return Nearest(wanted.Position, out settled);
        }

        /// <summary>The marker a quest reveal was made for, found among that quest's own markers by
        /// the position the request carries. False for every other kind of request, and for a quest
        /// whose markers the page cannot place.</summary>
        private bool LocatedMarker(GalaxyLocate.Request wanted, out MapTarget target)
        {
            target = default(MapTarget);
            if (wanted.Quest == null || !Declaring())
            {
                return false;
            }

            List<QuestMarkers.Marker> markers = QuestMarkers.Of(PlayerEmpire());
            for (int i = 0; i < markers.Count; i++)
            {
                if (
                    !ReferenceEquals(markers[i].Quest, wanted.Quest)
                    || ((Vector3)markers[i].At - wanted.Position).sqrMagnitude > CoincidesSquared
                )
                {
                    continue;
                }

                return MarkerTarget(markers[i], out target);
            }

            return false;
        }

        /// <summary>Where a marker's node hangs: under the system it stands at, or a row of its own
        /// out in the open. False where the map is not naming the system it stands at, which is the
        /// one case that has neither.</summary>
        internal bool MarkerTarget(QuestMarkers.Marker marker, out MapTarget target)
        {
            target = default(MapTarget);
            StarSystemNode at = MarkerSystem(marker);
            if (at == null)
            {
                if (!marker.Node.IsValid)
                {
                    target = MapTarget.Point(MarkerRowId(marker), marker.At);
                    return true;
                }

                return false;
            }

            // A marker standing at a system is a CHILD of it - drawn at the star, not a thing of its
            // own out on the map - so it lands the way a planet does: the free cursor ends, the branch
            // opens, and the camera comes in.
            target = MapTarget.Under(at, MarkerId(at, marker), marker.At);
            return true;
        }

        /// <summary>The same question the GAME's locate asks, for a caller inside the mod: where on
        /// this page does this thing live. Used by the scanner to send the cursor to what it found -
        /// through the page's own landing, branch opening included, rather than a second route to the
        /// same nodes.</summary>
        internal ControlId NodeFor(IGameEntityWithGalaxyPosition entity)
        {
            MapTarget target;
            return FromEntity(entity, out target) ? target.Id : null;
        }

        /// <summary>The same, as a landing target - which is what the caller sending the player there
        /// actually needs, since a fleet and a system are landed on differently.</summary>
        internal bool TargetFor(IGameEntityWithGalaxyPosition entity, out MapTarget target)
        {
            return FromEntity(entity, out target);
        }

        /// <summary>
        /// The inverse: where on the MAP the thing behind a node stands.
        ///
        /// Only the rows whose thing has a position of its own answer - a fleet, a probe, a missile, an
        /// ally's pin. A system's node carries the system as its reference and is answered by the walk
        /// that calls this (<see cref="Screens.GalaxyInspect.FocusedPlace"/>); a planet's row and a
        /// lane's have no position of their own and fall through to their system's, which is where the
        /// map draws them.
        ///
        /// It exists because those four rows are keyed STRUCTURALLY - a fleet's key names the system it
        /// is drawn at, which is what keeps two fleets of the same name apart - so there is no backing
        /// object on the id to read a position off. The ids are rebuilt here by the same code that
        /// declared them, never parsed back out of the key.
        /// </summary>
        internal bool PositionOf(ControlId id, out GalaxyPosition at)
        {
            at = default(GalaxyPosition);
            if (id == null)
            {
                return false;
            }

            try
            {
                for (int i = 0; i < _drifting.Count; i++)
                {
                    if (id.Equals(ProbeId(_drifting[i])))
                    {
                        at = _drifting[i].Probe.GalaxyPosition;
                        return true;
                    }
                }

                // The SIGHTED set, not the drawn labels: <see cref="AddProjectiles"/> declares the
                // rows from this list, so reading any other one lets a missile's position disagree
                // with the row that asked for it - a label the camera has culled is missing from
                // <c>_projectiles</c> while its row is still declared (owner ruling 2026-08-26).
                for (int i = 0; i < _shots.Count; i++)
                {
                    ObliteratorProjectile shot = _shots[i].Shot;
                    if (shot != null && id.Equals(ProjectileId(shot)))
                    {
                        at = shot.GalaxyPosition;
                        return true;
                    }
                }

                for (int i = 0; i < _sighted.Count; i++)
                {
                    CoordinationRequest pin = _sighted[i].Request;
                    if (pin != null && id.Equals(PinId(pin)))
                    {
                        at = pin.GalaxyPosition;
                        return true;
                    }
                }

                List<QuestMarkers.Marker> markers = QuestMarkers.Of(PlayerEmpire());
                for (int i = 0; i < markers.Count; i++)
                {
                    if (!markers[i].Node.IsValid && id.Equals(MarkerRowId(markers[i])))
                    {
                        at = markers[i].At;
                        return true;
                    }
                }

                List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
                for (int i = 0; i < sites.Count; i++)
                {
                    if (id.Equals(sites[i].Node))
                    {
                        at = sites[i].Fleet.GalaxyPosition;
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding where a node's own thing stands threw: " + e);
            }

            return false;
        }

        /// <summary>
        /// The landing for a thing the game NAMED.
        ///
        /// A fleet is where the map draws it - a point of its own, so the free cursor may stay up.
        /// Everything that stands at a system - the system, a colony of it, a planet in it - is that
        /// system, and the camera goes IN: the game's own locate for a planet aims at the star (the
        /// entity overload throws the entity away and keeps the position, ES2 facts), so a system is
        /// the whole of what the request said.
        ///
        /// Everything ELSE the map draws out on the map - a probe under way, a missile in flight, an
        /// ally's pin, a quest pin planted on something with no node - answers here too, by IDENTITY
        /// (<see cref="OpenSpaceRow"/>). It used to fall past this method to <see cref="Nearest"/>,
        /// which found the same row by POSITION and so was right nearly always and a guess in
        /// principle. Answering by identity is what makes the game's Show Location, a mod
        /// notification's, a panel's locate and the scanner's Alt+Home all arrive at the same place
        /// the same way (owner ruling 2026-08-26).
        /// </summary>
        private bool FromEntity(IGameEntityWithGalaxyPosition entity, out MapTarget target)
        {
            target = default(MapTarget);
            if (entity == null)
            {
                return false;
            }

            Fleet fleet = entity as Fleet;
            if (fleet != null)
            {
                List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
                for (int i = 0; i < sites.Count; i++)
                {
                    if (ReferenceEquals(sites[i].Fleet, fleet))
                    {
                        target = MapTarget.Point(Reveal(sites[i]), Berth(fleet), fleet);
                        return true;
                    }
                }

                return false;
            }

            ControlId row = OpenSpaceRow(entity);
            if (row != null)
            {
                target = MapTarget.Point(row, (Vector3)entity.GalaxyPosition, entity);
                return true;
            }

            ColonizedStarSystem colony = entity as ColonizedStarSystem;
            Planet planet = entity as Planet;
            StarSystemNode node = (
                colony != null
                    ? colony.Node
                    : planet != null
                        ? planet.StarSystemNode
                        : entity as GameNode
            ) as StarSystemNode;
            ControlId id = SystemId(node);
            if (id == null)
            {
                return false;
            }

            target = MapTarget.Place(node, id, node.GalaxyPosition);
            return true;
        }


        /// <summary>A system's node id, but only while this page is declaring that system: the map
        /// draws the names of the systems the player has seen, and the tree says the same
        /// (<see cref="Perceived"/>).</summary>
        private ControlId SystemId(StarSystemNode node)
        {
            return node != null && _systems.Contains(node)
                ? ControlId.Structural(SystemKey(node))
                : null;
        }

        /// <summary>
        /// Where a system's node hangs in the tree, as the PATH its id is
        /// (<c>galaxy:constellation/516/system/548</c>).
        ///
        /// The one place a system's key is composed. Every id under a system is built by appending to
        /// what this returns, and every landing aimed at one is built by calling it - which is what
        /// makes the constellation a real ancestor to the engine: a landing inside a collapsed
        /// constellation opens it, one level per build, by reading the ancestry out of the key
        /// (<see cref="KeyGraph.AncestorKeys"/>). A second site composing the old flat key would
        /// silently declare a node nothing could ever reach.
        ///
        /// The head CHANGES when the constellation becomes explored - the system moves out of the
        /// unexplored group and into its own - which is why a system's node carries the system itself
        /// as its reference: the cursor rides the object across the move rather than the key.
        /// </summary>
        private static string SystemKey(StarSystemNode node)
        {
            return SystemKey(node, PlayerEmpire());
        }

        private static string SystemKey(StarSystemNode node, Empire empire)
        {
            return GroupKey(node.Constellation, empire) + "/system/" + node.GUID;
        }

        /// <summary>The head of every key in one stretch of sky - the constellation's own where the
        /// player has explored it, and the one shared bucket where they have not.</summary>
        private static string GroupKey(Constellation constellation, Empire empire)
        {
            return Explored(constellation, empire)
                ? ConstellationKey + constellation.GUID
                : UnexploredKey;
        }

        private const string ConstellationKey = "galaxy:constellation/";

        /// <summary>Everything the player has not yet been shown a constellation NAME for, in one
        /// group. Not keyed per constellation: naming five buckets would say by their number how much
        /// of the galaxy is out there, which is the very thing the map is not showing.</summary>
        private const string UnexploredKey = "galaxy:constellation/unexplored";

        /// <summary>Whether the map draws this constellation's name at all - the label's own gate
        /// (<c>ConstellationLabel.ShowOrHideIfVisibleByEmpire</c>), asked the same way so the tree and
        /// the picture name the same regions. Its staleness is mirrored deliberately: the aggregate
        /// only recomputes when the game raises a node-exploration event, so on the first turns every
        /// constellation reads unexplored even the one the empire is sitting in - and the map draws no
        /// name across it either.</summary>
        private static bool Explored(Constellation constellation, Empire empire)
        {
            return ConstellationMap.Explored(constellation, empire);
        }

        /// <summary>One thing the map is drawing, and where it is drawn.</summary>
        private struct Spot
        {
            public Vector3 At;

            /// <summary>The node, for everything but a fleet.</summary>
            public ControlId Id;

            /// <summary>The system it is, where it is a place - what the camera zooms in on.</summary>
            public StarSystemNode System;

            /// <summary>Which fleet site this is, or -1. A fleet's node id is not made until it wins,
            /// because making one records a branch to open.</summary>
            public int Site;
        }

        /// <summary>
        /// The thing the map draws AT a point, or nothing where the point is out in the open.
        ///
        /// The tolerance is deliberately tight (<see cref="Coincides"/>): the question is "is this
        /// point where that thing is", not "what is nearest". Owner ruling 2026-08-22 - everything the
        /// game can point the player at is supposed to have a row of its own, so answering a point
        /// nothing stands at with the nearest system would hide the missing row behind a landing that
        /// sounds right, and the honest answer is a logged defect.
        ///
        /// The order candidates are offered in is the tie-break (<see cref="NearestPick"/>), and one tie
        /// is exact rather than coincidental: a fleet parked at a system says its position IS that
        /// system's (<c>FleetPosition</c> sets it from the node), so a request aimed at a star would
        /// otherwise be answered by whichever fleet happens to be sitting there. Places first, then.
        /// A fleet is offered at its BERTH - the slot the map draws it in, which is beside the star
        /// rather than on it - so a call aiming at a berth still picks the fleet out.
        /// </summary>
        private MapTarget Nearest(Vector3 position, out bool settled)
        {
            // A page arrived at cold has declared nothing, and "nothing is there" would be a wrong
            // answer rather than a late one.
            settled = Declaring();
            List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
            List<Spot> spots = new List<Spot>(_systems.Count + sites.Count);
            for (int i = 0; i < _systems.Count; i++)
            {
                Add(spots, _systems[i].GalaxyPosition, SystemId(_systems[i]), _systems[i], -1);
            }

            for (int i = 0; i < _drifting.Count; i++)
            {
                Add(spots, _drifting[i].Probe.GalaxyPosition, ProbeId(_drifting[i]), null, -1);
            }

            // The SIGHTED set, for the reason <see cref="PositionOf"/> reads it: a spot offered here
            // is answered with a ROW, and the rows come from this list.
            for (int i = 0; i < _shots.Count; i++)
            {
                ObliteratorProjectile shot = _shots[i].Shot;
                if (shot != null)
                {
                    Add(spots, shot.GalaxyPosition, ProjectileId(shot), null, -1);
                }
            }

            for (int i = 0; i < _sighted.Count; i++)
            {
                CoordinationRequest pin = _sighted[i].Request;
                if (pin != null)
                {
                    Add(spots, pin.GalaxyPosition, PinId(pin), null, -1);
                }
            }

            // Every quest marker out in the open is a row too, so a quest pin the reveal did not name
            // as a quest still lands on the pin rather than on the sky it hangs in.
            List<QuestMarkers.Marker> markers = QuestMarkers.Of(PlayerEmpire());
            for (int i = 0; i < markers.Count; i++)
            {
                if (!markers[i].Node.IsValid)
                {
                    Add(spots, markers[i].At, MarkerRowId(markers[i]), null, -1);
                }
            }

            for (int i = 0; i < sites.Count; i++)
            {
                Add(spots, Berth(sites[i].Fleet), null, null, i);
            }

            NearestPick pick = new NearestPick(Coincides);
            for (int i = 0; i < spots.Count; i++)
            {
                pick.Offer(i, (spots[i].At - position).sqrMagnitude);
            }

            if (!pick.Found)
            {
                return MapTarget.Nowhere(position);
            }

            Spot won = spots[pick.Index];
            if (won.Site < 0)
            {
                return won.System != null
                    ? MapTarget.Place(won.System, won.Id, won.At)
                    // A probe, a missile, an ally's pin and an open-space marker all sit at the top of
                    // the stop, so there is never a branch to open before the cursor can be sent to one.
                    : MapTarget.Point(won.Id, won.At);
            }

            bool holding;
            FleetSite site = sites[Holding(sites, won, out holding)];
            settled &= holding;
            return MapTarget.Point(Reveal(site), Berth(site.Fleet), site.Fleet);
        }

        /// <summary>
        /// How near a point has to be to something the map draws before it IS that thing, in the
        /// galaxy's own units.
        ///
        /// A coincidence, not a neighbourhood: the closest two systems in a galaxy stand 6.7 units
        /// apart (measured on the fixture; 10.6 on average), so nothing can be mistaken for its
        /// neighbour, while the small offsets the map draws a fleet's berth and a planet's orbit at
        /// still land on the thing itself.
        /// </summary>
        private const float Coincides = 1.5f;

        private const float CoincidesSquared = Coincides * Coincides;

        private static void Add(
            List<Spot> spots,
            Vector3 at,
            ControlId id,
            StarSystemNode system,
            int site
        )
        {
            if (id != null || site >= 0)
            {
                spots.Add(new Spot { At = at, Id = id, System = system, Site = site });
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
        /// game, and the panel's own visibility is gated on the same thing (ES2 facts).</summary>
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
            // The map's own ladder, handed to the cluster that names the view rather than appended
            // after it: the two are one control per row and the ladder comes first, which is the
            // cluster's ordering to make.
            _hud.ViewTitle(builder, _zoom);

            builder.BeginStop(SystemStop);
            builder.PushContext(MapContext());
            BuildSystems(builder);
            // Popped before the fleet panel, which is a stop of its own.
            builder.PopContext();

            // The selected-fleet panel, where the game draws it: over the bottom of the map, between
            // what the map shows and the clusters down its right-hand edge. Nothing at all while no
            // fleet is selected.
            _fleetPanel.Build(builder, this);

            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.TurnLog(builder);
            _hud.Turn(builder);
        }

        /// <summary>
        /// What the map calls itself while the player is in it.
        ///
        /// Ordinarily the mod's own word: the map has no caption anywhere on it - it IS the screen -
        /// so there is nothing of the game's to read (owner ruling 2026-08-19).
        ///
        /// While one of the game's targeting modes is waiting for a target, the map is not a map any
        /// more, it is the question the game is asking, and the game writes that question out in a
        /// banner across it (<c>UserInstructionsWindow</c>, the same sentence the mode was announced
        /// with - <see cref="GlobalHud.Instruction"/>). So the stop is named after it: a player who
        /// Tabs away and comes back is told what the map is waiting for instead of being told it is a
        /// map, which they know. It goes back to being a map the moment the mode ends.
        ///
        /// The rename changes the context's identity (its id is derived from parent and label), which
        /// is what makes leaving and re-entering the stop read the instruction again - the point of
        /// putting it here. The nodes UNDER it key on their own places and are untouched by it.
        /// </summary>
        private static string MapContext()
        {
            string instruction = GlobalHud.Instruction();
            return string.IsNullOrEmpty(instruction)
                ? ModStrings.Get(ModStrings.GalaxyMapPanel)
                : instruction;
        }

        /// <summary>
        /// Typing on the map looks through every system and every fleet the map is drawing, wherever
        /// each is buried.
        ///
        /// Two things are buried, one level apart. A fleet lives under the place it is standing now,
        /// and that place is usually closed; a SYSTEM lives under the stretch of sky it stands in,
        /// which the player may equally have closed. In both cases the only thing the ordinary scope
        /// (the stop's declared controls) could find is what the player had already opened, which is
        /// not a search, it is a confirmation. Landing on either opens what it is inside, so the branch
        /// the player is put into is the branch they can then walk. The opening is recorded rather than
        /// done: the graph is rebuilt between this call and the focus landing, and the expansion set
        /// belongs to that rebuild.
        ///
        /// Everything the stop already declares stays searchable - this EXTENDS the ordinary scope
        /// rather than replacing it, so a planet or a starlane of an open system is still found by
        /// name, and nothing is offered twice.
        /// </summary>
        public override SearchScope TypeAheadScope(GraphNode focused, GraphRender render)
        {
            if (focused == null || !Equals(focused.StopKey, SystemStop))
            {
                return null;
            }

            // Only what the stop has NOT already declared: an open constellation declares its systems
            // and an open system declares its fleets, and either offered twice would be two results
            // with one name, which stepping the matches walks through twice.
            HashSet<ControlId> declared = Declared(render);
            List<HiddenSystem> closed = SystemIndex(declared);
            List<FleetSite> sites = FleetIndex(declared);
            if (sites.Count == 0 && closed.Count == 0)
            {
                return null;
            }

            SearchScope basis = SearchScope.OverStop(render, SystemStop);
            int already = basis.Count;
            int stars = already + closed.Count;
            List<HiddenSystem> shut = closed;
            List<FleetSite> found = sites;
            GalaxyHudScreen screen = this;
            return new SearchScope(
                stars + found.Count,
                index =>
                    index < already
                        ? basis.TextOf(index)
                        : index < stars
                            ? shut[index - already].Node.LocalizedName
                            : found[index - stars].Fleet.LocalizedName,
                index =>
                    index < already
                        ? basis.Land(index)
                        : index < stars
                            ? screen.RevealSystem(shut[index - already])
                            : screen.Reveal(found[index - stars]),
                // Which control each result IS, with nothing opened - so that the shared scope can add
                // everything ELSE a closed branch would declare without offering these twice
                // (<see cref="SearchScope.Extend"/>).
                index =>
                    index < already
                        ? basis.IdOf(index)
                        : index < stars
                            ? shut[index - already].Id
                            : found[index - stars].Node
            );
        }

        /// <summary>One system the stop is not declaring because the stretch of sky it stands in is
        /// closed, with the group that has to be opened before its node exists.</summary>
        private struct HiddenSystem
        {
            public StarSystemNode Node;
            public ControlId Id;
            public ControlId Group;
        }

        /// <summary>Every system the map is naming that this build did not declare - the ones inside a
        /// constellation the player has closed. Built on demand, like the fleet index, and from the
        /// same list the stop itself is built from, so a search can never offer a place the tree would
        /// not hold.</summary>
        private List<HiddenSystem> SystemIndex(HashSet<ControlId> declared)
        {
            List<HiddenSystem> hidden = new List<HiddenSystem>();
            try
            {
                Empire empire = PlayerEmpire();
                for (int i = 0; i < _systems.Count; i++)
                {
                    StarSystemNode node = _systems[i];
                    ControlId id = ControlId.For(node, SystemKey(node, empire));
                    if (!declared.Contains(id))
                    {
                        hidden.Add(
                            new HiddenSystem
                            {
                                Node = node,
                                Id = id,
                                Group = GroupId(node),
                            }
                        );
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: indexing the closed systems for a search threw: " + e);
            }

            return hidden;
        }

        /// <summary>Open the stretch of sky a system is in, and answer with the system itself - the
        /// same bargain <see cref="Reveal"/> strikes one level down.</summary>
        private ControlId RevealSystem(HiddenSystem it)
        {
            if (it.Group != null)
            {
                _pendingExpand.Add(it.Group);
            }

            return it.Id;
        }

        /// <summary>One fleet, the node it is declared as, and the branch that has to be open before
        /// that node exists: the system it is parked at, or one of the two whose lane it is flying, or
        /// nothing at all for one crossing open space towards somewhere the map has not named, whose
        /// row is at the top level and needs no branch opened (<see cref="AddAdrift"/>).</summary>
        private struct FleetSite
        {
            public Fleet Fleet;
            public ControlId System;

            /// <summary>And the stretch of sky that system hangs under, which has to be open before the
            /// system itself is declared - one more level than there used to be.</summary>
            public ControlId Group;
            public ControlId Node;
        }

        /// <summary>Every fleet the map is drawing and the place each is drawn at, minus the ones the
        /// graph already holds. Built on demand: both repositories are walked once per lane, and the
        /// only thing that ever wants this is one keystroke.</summary>
        private List<FleetSite> FleetIndex(HashSet<ControlId> declared)
        {
            List<FleetSite> sites = new List<FleetSite>();
            try
            {
                Empire empire = PlayerEmpire();
                for (int i = 0; i < _systems.Count; i++)
                {
                    IndexPlace(_systems[i], empire, sites, declared);
                }

                for (int i = 0; i < _adrift.Count; i++)
                {
                    Fleet fleet = _adrift[i];
                    ControlId id = AdriftId(fleet);
                    if (!declared.Contains(id))
                    {
                        sites.Add(new FleetSite { Fleet = fleet, System = null, Node = id });
                    }
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

        /// <summary>The fleets one system holds - parked, under way on its lanes, and crossing open
        /// space TO it, all three of which hang under the system itself. A lane is reached from both of
        /// its ends, so a fleet flying one is indexed under each end: two results with one name, which
        /// is the right way round, because either end is a true answer to "where is it" and dropping one
        /// would make the answer depend on which system the search happened to reach first. A crossing
        /// of open space is indexed under its DESTINATION alone (<see cref="FreeMovingAt"/>), because
        /// that is the one end the map itself shows. Every list here is the one the BRANCH is built
        /// from - the lanes the map draws (<see cref="LanesOf"/>) and the same crossings - so the index
        /// cannot offer a fleet the branch will not hold.</summary>
        private static void IndexPlace(
            StarSystemNode node,
            Empire empire,
            List<FleetSite> sites,
            HashSet<ControlId> declared
        )
        {
            if (empire == null)
            {
                return;
            }

            string systemKey = SystemKey(node, empire);
            ControlId system = ControlId.For(node, systemKey);
            ControlId group = GroupId(node);
            Index(FleetPresence.FleetsAt(node), system, group, systemKey, sites, declared);
            List<EnRoute> flying = EnRouteOn(node, LanesOf(node, empire));
            List<Fleet> crossing = FreeMovingAt(node);
            List<Fleet> nearby = new List<Fleet>(flying.Count + crossing.Count);
            for (int i = 0; i < flying.Count; i++)
            {
                nearby.Add(flying[i].Fleet);
            }

            for (int i = 0; i < crossing.Count; i++)
            {
                nearby.Add(crossing[i]);
            }

            Index(nearby, system, group, systemKey, sites, declared);
        }

        private static void Index(
            IList<Fleet> fleets,
            ControlId system,
            ControlId group,
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
                        Group = group,
                        Node = id,
                    }
                );
            }
        }

        /// <summary>Open the system a fleet is drawn at or beside, and answer with the fleet itself. The
        /// opening is recorded rather than done: the expansion set belongs to the next rebuild. A fleet
        /// whose row is at the top level has nothing to open.</summary>
        private ControlId Reveal(FleetSite site)
        {
            if (site.Group != null)
            {
                _pendingExpand.Add(site.Group);
            }

            if (site.System != null)
            {
                _pendingExpand.Add(site.System);
            }

            return site.Node;
        }

        /// <summary>Open the branches a search landed in, and close the ones travelling has left behind.
        /// The expansion set is the engine's, and this is the one moment a screen has anything to say
        /// about it - the branches are asked for while a keystroke is being answered and applied on the
        /// build that follows, because the render they belong to is the next one.</summary>
        private void ApplyPendingExpansions(GraphBuilder builder)
        {
            if (_pendingExpand.Count == 0 && _pendingCollapse.Count == 0)
            {
                return;
            }

            HashSet<ControlId> expansion = builder.Expansion;
            if (expansion != null)
            {
                // Closings first: nothing ever asks for the same branch both ways on one frame, and
                // where something did, being asked to open it is the later word.
                for (int i = 0; i < _pendingCollapse.Count; i++)
                {
                    expansion.Remove(_pendingCollapse[i]);
                }

                for (int i = 0; i < _pendingExpand.Count; i++)
                {
                    expansion.Add(_pendingExpand[i]);
                }
            }

            _pendingExpand.Clear();
            _pendingCollapse.Clear();
        }

        /// <summary>The groups a search or a travelled lane has asked to be opened, applied on the next
        /// build.</summary>
        private readonly List<ControlId> _pendingExpand = new List<ControlId>();

        /// <summary>The groups travelling has asked to be CLOSED - a system whose branch was opened only
        /// because a lane was travelled into it, being left again.</summary>
        private readonly List<ControlId> _pendingCollapse = new List<ControlId>();

        // ---- travelling the lanes ----

        /// <summary>
        /// One hop down a starlane: where it started, the lane it was taken on, and where it arrived.
        ///
        /// <see cref="Return"/> is the LANE's own node rather than the origin system's, because that is
        /// where the player was standing when they pressed the key - coming back to the system itself
        /// would make the way back a different place from the way out, and a player retracing several
        /// hops would have to find the lane again each time.
        /// </summary>
        private struct Journey
        {
            public StarSystemNode Origin;
            public ControlId Return;
            public StarSystemNode Destination;

            /// <summary>Whether the destination's branch was opened BY this hop. A system the player had
            /// already opened themselves is theirs, and is left open when they leave.</summary>
            public bool Opened;
        }

        private readonly List<Journey> _trail = new List<Journey>();

        /// <summary>The game the trail was travelled in. A trail is a list of places, and a save loaded
        /// over this one has different places - so the whole of it dies with the session rather than
        /// pointing the player at systems out of a galaxy that no longer exists. It deliberately survives
        /// everything SHORTER than that: a trip to the research screen and back is the same map, and this
        /// page keeps its cursor across one (<see cref="KeepStateOnPop"/>), so the way back should keep
        /// too.</summary>
        private object _trailGame;

        /// <summary>Forget a trail belonging to a galaxy the player is no longer in. Cheap enough to ask
        /// every frame - it is one reference comparison - and asking every frame is what stops a stale
        /// trail ever being reachable, rather than being caught at the moment it would have been used.
        /// </summary>
        private void CheckTrailSession()
        {
            object game = Gui.Game;
            if (ReferenceEquals(game, _trailGame))
            {
                return;
            }

            _trailGame = game;
            _trail.Clear();
            // And the record of which groups have already been given a starting state: a new galaxy has
            // different constellations, and the ones it does share by key are being met for the first
            // time (<see cref="Seed"/>).
            _seeded.Clear();
        }

        /// <summary>
        /// Go where a lane leads: the cursor onto the destination system's own node at the root of the
        /// stop, its branch opened, and the camera taken there.
        ///
        /// The one node is the whole point. The destination is already declared - every system the map
        /// draws is - so this REBASES the cursor onto it rather than declaring a second copy under the
        /// lane, which is what keeps one object to one node and gives the tree a bottom without having to
        /// make the copy poorer than the original.
        ///
        /// Nothing is spoken. The landing announces itself, once, through the same path every focus
        /// change goes through, and it names the system - which is the answer to the question the key
        /// asked.
        ///
        /// NOT a click: <c>ZoomTo</c> rather than <see cref="ZoomIn"/>, so a targeting mode the game has
        /// armed is neither confirmed nor cancelled, exactly as opening a branch leaves it alone. Right is
        /// pressed speculatively all over a tree and must never post an order.
        /// </summary>
        private void Travel(
            StarSystemNode origin,
            ControlId lane,
            StarSystemNode destination,
            HashSet<ControlId> expansion
        )
        {
            try
            {
                CheckTrailSession();
                ControlId arriving = RootId(destination);
                // Asked of the engine's own expansion set, which the builder handed out when this lane
                // was declared: whether the place was ALREADY open is the whole of what decides who owns
                // its state, and the screen keeps no expansion of its own to ask instead.
                bool open = expansion != null && expansion.Contains(arriving);
                // Leaving a system this trail opened closes it again, before the hop that leaves it is
                // remembered - the trail's top is still the hop that opened the place being left.
                LeaveTravelled(origin);
                _trail.Add(
                    new Journey
                    {
                        Origin = origin,
                        Return = lane,
                        Destination = destination,
                        Opened = !open,
                    }
                );

                Arrive(arriving, destination);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: travelling a starlane threw: " + e);
            }
        }

        /// <summary>
        /// Backspace: back up the lane the player came down.
        ///
        /// The cursor goes to the LANE node it was on when the hop was taken, which means the origin has
        /// to be open for that node to exist - so it is asked to open whether or not it was left open.
        /// The camera goes back to the origin. Nothing is spoken here either: the lane announces itself
        /// on arrival, and it is the same lane, said the same way, as the one the player left.
        ///
        /// A hop into or out of somewhere the player can no longer SEE is skipped rather than taken: the
        /// fog moves, empires lose vision, and a lane node under a system the map has stopped drawing is
        /// not there to be landed on. Popping continues to the next hop that is still true, and a trail
        /// with none left is spent.
        /// </summary>
        private bool PopTrail()
        {
            CheckTrailSession();
            Empire empire = PlayerEmpire();
            while (_trail.Count > 0)
            {
                Journey hop = _trail[_trail.Count - 1];
                _trail.RemoveAt(_trail.Count - 1);
                if (
                    empire == null
                    || hop.Origin == null
                    || hop.Destination == null
                    || !Perceived(hop.Origin, empire)
                    || !Perceived(hop.Destination, empire)
                )
                {
                    continue;
                }

                if (hop.Opened)
                {
                    _pendingCollapse.Add(RootId(hop.Destination));
                }

                OpenPlace(hop.Origin);
                Arrive(hop.Return, hop.Origin);
                return true;
            }

            return false;
        }

        /// <summary>Ask for the branch, then hand the rest to the page's one landing
        /// (<see cref="GoTo"/>): the cursor, the camera, and the free cell where one is up. The
        /// expansion belongs to the next build (<see cref="ApplyPendingExpansions"/>) and the cursor to
        /// the tick after that, so the node the player lands on exists by the time they land.</summary>
        private void Arrive(ControlId id, StarSystemNode where)
        {
            OpenPlace(where);
            GoTo(MapTarget.Place(where, id, where.GalaxyPosition), MapCamera.Zoom);
        }

        /// <summary>Close a system again where this trail is the only reason it is open. The trail's last
        /// hop is the one that opened wherever the player is now, so that is the only entry worth asking
        /// about.</summary>
        private void LeaveTravelled(StarSystemNode leaving)
        {
            if (_trail.Count == 0 || leaving == null)
            {
                return;
            }

            Journey top = _trail[_trail.Count - 1];
            if (top.Opened && ReferenceEquals(top.Destination, leaving))
            {
                _pendingCollapse.Add(RootId(leaving));
            }
        }

        /// <summary>A system's own node under its constellation - the one node that stands for it, keyed
        /// exactly as <see cref="AddSystem"/> keys it. Distinct from <see cref="SystemId"/>, which asks
        /// the narrower question of whether the page is DECLARING that system at all: travelling and
        /// backing out have already established that from the map's own perception rules.</summary>
        private static ControlId RootId(StarSystemNode node)
        {
            return ControlId.For(node, SystemKey(node));
        }

        /// <summary>The group a system's node hangs under - its constellation's, or the one the
        /// unexplored ones share.</summary>
        private static ControlId GroupId(StarSystemNode node)
        {
            return ControlId.Structural(GroupKey(node.Constellation, PlayerEmpire()));
        }

        /// <summary>Ask for a system's branch to be open on the next build, and for the stretch of sky
        /// it hangs in along with it: a system inside a closed constellation is not declared at all, so
        /// opening only the system would ask for a node that does not exist yet.</summary>
        private void OpenPlace(StarSystemNode node)
        {
            _pendingExpand.Add(GroupId(node));
            _pendingExpand.Add(RootId(node));
        }

        /// <summary>
        /// Backspace on the map is the way back down the lanes that have been travelled, and it belongs to
        /// the MAP rather than to whatever node the cursor is on - the player is somewhere because of the
        /// hops they took, not because of the planet they are standing on.
        ///
        /// Only in the systems stop. The clusters round the edges of the screen keep whatever the key
        /// meant to them, because a trail of places has nothing to say about a notification icon.
        ///
        /// Consumed even with nothing to go back to, and silent then: pressing it at the start of a
        /// journey is asking for something that is simply not there, and a cue for it on a key pressed
        /// speculatively is noise - the same rule the other gesture keys keep.
        /// </summary>
        public override bool Secondary(GraphNode focused)
        {
            if (focused == null || !Equals(focused.StopKey, SystemStop))
            {
                return false;
            }

            PopTrail();
            return true;
        }

        // ---- systems ----

        /// <summary>
        /// The star systems the player can see, filed under the stretches of sky the map draws names
        /// across.
        ///
        /// Which systems those are is the same question the map asks when it decides whether to draw a
        /// system's name: explored at least once, and either remembered or currently in sight. Asking
        /// it the same way is what keeps this list and the map showing the same galaxy.
        ///
        /// The map has exactly one level above a system, and the game already draws it: the
        /// CONSTELLATION, whose name floats over the region its members are scattered through. Every
        /// node in the galaxy belongs to one, so grouping by it costs nothing to derive and gives a
        /// galaxy of forty stars a walk of five entries instead of forty
        /// (<see cref="AddConstellation"/>). A constellation the player has not explored has no name
        /// drawn across it and must not be named here either, so everything in all of those shares one
        /// group with a mod-authored caption (<see cref="AddUnexplored"/>) which goes last, having no
        /// position of its own to be sorted by.
        ///
        /// One list and not two, INSIDE all that. An empire's own colonies used to be held at the
        /// front, which made the list run north to south twice and put two systems that are neighbours
        /// on the map at opposite ends of the walk; the map itself draws no such division, and whose a
        /// star is is already the first thing its own row says (owner ruling 2026-08-16). So the only
        /// division left on this stop is between what stands AT a place and what is drawn out between
        /// the stars, which is a division the picture really has.
        ///
        /// The systems are put in the order they would be READ off the map -
        /// <see cref="ReadingOrder"/> - and so are the constellation groups, by the centroid the game
        /// itself stores for each and writes its name at. So the list runs the same way twice and the
        /// same way the pairs it speaks do. Home is not held at the front either: it sits wherever its
        /// own pair puts it. A fleet crossing open space towards somewhere the map has not named is
        /// walked into that same order by its own position (<see cref="AddAdrift"/>), because it stands
        /// at no place and there is nowhere else for it to be.
        /// </summary>
        private void BuildSystems(GraphBuilder builder)
        {
            try
            {
                Empire empire = PlayerEmpire();
                if (empire == null || !GameGalaxy.Present())
                {
                    return;
                }

                _systems.Clear();
                _located.Clear();
                _colonies.Clear();
                DepartmentOfTheInterior interior = empire.GetAgency<DepartmentOfTheInterior>();
                if (interior != null)
                {
                    foreach (ColonizedStarSystem colony in interior.ColonizedStarSystems)
                    {
                        // An empire can hold more than one thing in the same system - a colony and a
                        // ghost of it - and the system is still one place on the map.
                        if (colony.Node != null && !_colonies.Contains(colony.Node))
                        {
                            _colonies.Add(colony.Node);
                            _systems.Add(colony.Node);
                        }
                    }
                }

                foreach (StarSystemNode node in GameGalaxy.StarSystemNodes())
                {
                    if (_colonies.Contains(node))
                    {
                        continue;
                    }

                    if (Perceived(node, empire))
                    {
                        _systems.Add(node);
                    }
                    else if (MapVisibility.Located(node, empire))
                    {
                        // The map is drawing a star here and naming nothing (<see cref="AddLocated"/>).
                        _located.Add(node);
                    }
                }

                _systems.Sort(ReadingOrder);
                _located.Sort(ReadingOrder);
                FreeMovingAdrift(_systems, _adrift);
                _adrift.Sort(FleetReadingOrder);

                Drifting();
                // Every probe the map is drawing: they all sit at the top of the open-space region
                // now (<see cref="AddProbes"/>), so every one of them is a reason to declare it.
                //
                // Counted off the SIGHTED sets, which are the lists the rows themselves are declared
                // from (<see cref="AddProbes"/>, <see cref="AddProjectiles"/>, <see cref="AddPins"/>)
                // - so the region exists exactly when it will hold rows, and never over an empty
                // one. The drawn-label lists are a different question: a missile the camera has
                // culled has no label and still has a row, which would have opened a region with
                // nothing in it or left rows with no region to sit in (owner ruling 2026-08-26,
                // the same unification the two lookups above got).
                int drifting =
                    _drifting.Count + _shots.Count + _sighted.Count + OpenSpaceMarkers(empire);
                // Declared whichever halves the map has: a lone region's jump is swallowed silently,
                // which is what the key doing nothing here should sound like, and a section that
                // appears and disappears with the fleet count is a stop that changes shape under the
                // player (owner ruling, 2026-08-18).
                builder.SetRegion(SystemsRegion);

                // Fetched once for the whole stop rather than once per system: the labels the map
                // draws are pooled by the window, not rebuilt per frame, so one walk of the label
                // window serves every system this build declares.
                StarSystemLabel[] labels = SystemLabels();
                ConstellationLabel[] regions = ConstellationLabels();
                Partition(empire);

                // Two lists already in the same order, merged as they are declared: a homeless fleet
                // takes its place among the constellations rather than being parked at either end of
                // them.
                int sky = 0;
                int fleet = 0;
                while (sky < _groups.Count || fleet < _adrift.Count)
                {
                    bool takeSky =
                        fleet >= _adrift.Count
                        || (
                            sky < _groups.Count
                            && ComparePositions(
                                _groups[sky].Constellation.GalaxyPosition,
                                _adrift[fleet].GalaxyPosition
                            ) <= 0
                        );
                    if (takeSky)
                    {
                        AddConstellation(builder, _groups[sky], empire, labels, regions);
                        sky++;
                    }
                    else
                    {
                        AddAdrift(builder, _adrift[fleet]);
                        fleet++;
                    }
                }

                AddUnexplored(builder, empire, labels);

                if (drifting > 0)
                {
                    builder.SetRegion(OpenSpaceRegion);
                }

                AddProbes(builder);
                AddProjectiles(builder);
                AddPins(builder);
                // A quest pin planted on a fleet in mid-lane stands at no place at all, so it belongs
                // here with the other things drifting between the stars rather than under whichever
                // star happens to be nearest.
                AddOpenSpaceMarkers(builder, empire);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the systems threw: " + e);
            }
        }

        /// <summary>One stretch of sky the map names, and which of this build's member lists holds the
        /// systems it is showing. The members are held by INDEX into a pool of lists rather than in the
        /// struct, so the whole partition is rebuilt every frame without allocating one list per
        /// constellation per build (<see cref="Partition"/>).</summary>
        private struct SkyGroup
        {
            public Constellation Constellation;
            public int Members;
        }

        /// <summary>The constellations this build is naming, in the order they read.</summary>
        private readonly List<SkyGroup> _groups = new List<SkyGroup>();

        /// <summary>The pool <see cref="SkyGroup.Members"/> indexes: reused build after build, cleared
        /// as each is claimed, and never longer than the galaxy has constellations.</summary>
        private readonly List<List<StarSystemNode>> _members =
            new List<List<StarSystemNode>>();

        /// <summary>Everything standing in a constellation the map draws no name across, in reading
        /// order.</summary>
        private readonly List<StarSystemNode> _unexplored = new List<StarSystemNode>();

        /// <summary>
        /// Sort this build's systems into the stretches of sky they stand in.
        ///
        /// <see cref="_systems"/> and <see cref="_located"/> are each already in reading order, so
        /// walking the two of them merged and appending each place to its own group leaves every group
        /// in reading order too, and only the groups themselves need sorting.
        ///
        /// Rebuilt every frame like the rest of the stop, and allocation-free after the first galaxy:
        /// the member lists are pooled and the group list keeps its capacity across
        /// <see cref="List{T}.Clear"/>. Which constellation a node is in never changes; only whether
        /// the player has been shown its name does, and that is one array read each.
        /// </summary>
        private void Partition(Empire empire)
        {
            _groups.Clear();
            _unexplored.Clear();
            int claimed = 0;
            int named = 0;
            int drawn = 0;
            // Two lists already in the same order, merged as they are filed - the same walk the stop
            // makes over the constellations and the homeless fleets. A place the map draws without
            // naming stands in the reading order where its POSITION puts it, among the named ones,
            // because that is where a player steering by coordinates would come to it.
            while (named < _systems.Count || drawn < _located.Count)
            {
                bool takeNamed =
                    drawn >= _located.Count
                    || (
                        named < _systems.Count
                        && ComparePositions(
                            _systems[named].GalaxyPosition,
                            _located[drawn].GalaxyPosition
                        ) <= 0
                    );
                StarSystemNode node = takeNamed ? _systems[named++] : _located[drawn++];
                Constellation constellation = node.Constellation;
                if (!Explored(constellation, empire))
                {
                    _unexplored.Add(node);
                    continue;
                }

                int slot = -1;
                for (int j = 0; j < _groups.Count; j++)
                {
                    if (ReferenceEquals(_groups[j].Constellation, constellation))
                    {
                        slot = _groups[j].Members;
                        break;
                    }
                }

                if (slot < 0)
                {
                    if (claimed >= _members.Count)
                    {
                        _members.Add(new List<StarSystemNode>());
                    }

                    slot = claimed++;
                    _members[slot].Clear();
                    _groups.Add(
                        new SkyGroup { Constellation = constellation, Members = slot }
                    );
                }

                _members[slot].Add(node);
            }

            _groups.Sort(ConstellationOrder);
        }

        /// <summary>
        /// One constellation as a group node: the name the map writes across it, the game's own
        /// dossier on it as the node's tooltip, and the systems in it as its children.
        ///
        /// The label the name is read off is one the window keeps per constellation and shows for any
        /// the empire has explored. At the zoom the game is played at its alpha is nought - the picture
        /// fades constellation names out as the camera comes in - and that is deliberately not asked
        /// about: the label exists, the game keeps it bound, and its tooltip reads. What decides
        /// whether this group is named at all is the same gate the label itself uses
        /// (<see cref="Explored"/>), never how faded it happens to be at this moment.
        ///
        /// No coordinate pair (owner ruling 2026-08-20). A constellation is a REGION, and the centroid
        /// the game stores for it is where its name is written rather than a place anything stands - a
        /// pair here would be a place the player could steer to and find nothing.
        ///
        /// Closing the group takes the camera back out, exactly as closing a system does and for the
        /// same reason: it is the one gesture that means "I am done reading in there". Only while the
        /// camera is still inside THIS constellation - a player who has since read their way somewhere
        /// else has a camera that is not this group's to move. Opening moves no camera: there is
        /// nothing at a constellation's centre to fly to, and the group's own children are what opening
        /// it is for.
        /// </summary>
        private void AddConstellation(
            GraphBuilder builder,
            SkyGroup group,
            Empire empire,
            StarSystemLabel[] labels,
            ConstellationLabel[] regions
        )
        {
            Constellation it = group.Constellation;
            ConstellationLabel drawn = LabelFor(it, regions);
            AgeTooltip tooltip = drawn == null ? null : drawn.ConstellationTooltip;
            NodeVtable vtable = GraphNodes.Group(() => it.LocalizedName, tooltip: tooltip);
            AgeTooltip tip = tooltip;
            ConstellationLabel showing = drawn;
            vtable.OnFocusVisual = () =>
            {
                // The label the name and the dossier both live on is one the map CULLS at every
                // camera position the game is played at, and a hidden label draws no tooltip - so it
                // is held drawn for as long as the cursor stands here, and given back to the game the
                // moment it leaves (<see cref="ConstellationLabelHold"/>).
                ConstellationLabelHold.Hold(showing);
                if (tip != null)
                {
                    PointerFocus.MoveTo(null, tip, tip.AgeTransform);
                }
            };
            vtable.OnBlurVisual = ReleaseConstellation;

            ControlId id = ConstellationId(it);
            HashSet<ControlId> expansion = builder.Expansion;
            ControlId closing = id;
            Constellation leaving = it;
            vtable.OnCollapse = () =>
            {
                if (expansion != null)
                {
                    expansion.Remove(closing);
                }

                ZoomOutOf(leaving);
            };

            Seed(builder, id);
            // Synthetic: a constellation is a place the mod assembled from the galaxy's own model - nothing on the map is drawn as one.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                List<StarSystemNode> members = _members[group.Members];
                for (int i = 0; i < members.Count; i++)
                {
                    AddPlace(builder, members[i], empire, labels);
                }
            }

            builder.EndGroup();
        }

        /// <summary>
        /// Everything standing where the map has drawn no constellation name, in one group.
        ///
        /// One group and not one per constellation: the game DOES know which unexplored constellation
        /// each of these stands in, and saying so - even as five nameless buckets - would tell the
        /// player how the unseen half of the galaxy is divided up, which the picture does not. The
        /// caption is the mod's own for the same reason: there is no game text for a region the game is
        /// not naming.
        ///
        /// Last in the stop. It is the one entry with no position of its own - its members are
        /// scattered over the whole map - so there is no honest place for it in a walk sorted by
        /// position, and the end is where a group that is really "everything else" belongs.
        ///
        /// No tooltip, and expanding or closing it moves no camera: it stands for no place, so there is
        /// nowhere for a camera to go.
        /// </summary>
        private void AddUnexplored(GraphBuilder builder, Empire empire, StarSystemLabel[] labels)
        {
            if (_unexplored.Count == 0)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Group(
                () => ModStrings.Get(ModStrings.GalaxyConstellationUnexplored)
            );
            ControlId id = ControlId.Structural(UnexploredKey);
            Seed(builder, id);
            // Synthetic: the same, for the places the empire has not explored.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                for (int i = 0; i < _unexplored.Count; i++)
                {
                    AddPlace(builder, _unexplored[i], empire, labels);
                }
            }

            builder.EndGroup();
        }

        /// <summary>
        /// Open a group the first time this session ever declares it, and never again.
        ///
        /// A tree of constellations that arrived closed would put a level between the player and every
        /// system they used to walk straight into, which is a change to how the map READS rather than a
        /// change to what it holds. Open is therefore the inert default: the walk the player had is the
        /// walk they still have, and closing a constellation they are done with is something they can
        /// now choose. Once they have chosen, the choice is theirs - the seed never fires twice for the
        /// same group, so a group the player closed stays closed.
        ///
        /// Keyed on the structural key rather than the id so the record survives the id being rebuilt
        /// each frame, and cleared with the trail when the galaxy changes.
        /// </summary>
        private void Seed(GraphBuilder builder, ControlId id)
        {
            // Never off a search build: that build has everything open by construction
            // (<see cref="GraphBuilder.ExpandAll"/>) and it must not be what decides the tree the
            // player then walks - spending the once-ever seed there would leave a group they have
            // never seen already open.
            HashSet<ControlId> expansion = builder.ExpandAll ? null : builder.Expansion;
            if (expansion == null || !_seeded.Add(id.StructuralKey))
            {
                return;
            }

            expansion.Add(id);
        }

        /// <summary>The groups this session has already offered a starting state to.</summary>
        private readonly HashSet<object> _seeded = new HashSet<object>();

        /// <summary>Put the camera back out at the default view when a constellation's branch is
        /// closed - but only while it is a system of THIS constellation the camera is in on, which is
        /// the same test closing a system makes (<see cref="Collapse"/>) one level up. The way out is
        /// the system's own, so the camera lands exactly where collapsing that system would have put
        /// it, and a camera already out moves not at all.</summary>
        private void ZoomOutOf(Constellation constellation)
        {
            StarSystemNode inside = GalaxyViewLevels.FocusedSystem;
            if (inside != null && ReferenceEquals(inside.Constellation, constellation))
            {
                ZoomOut(inside);
                LeftPlace(inside);
            }
        }

        /// <summary>The map's own label for a constellation - matched by the constellation it was bound
        /// to, with the entity's identity as the fallback the system labels use for the same reason.
        /// </summary>
        private static ConstellationLabel LabelFor(
            Constellation constellation,
            ConstellationLabel[] labels
        )
        {
            try
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    if (ReferenceEquals(labels[i].Constellation, constellation))
                    {
                        return labels[i];
                    }
                }

                for (int i = 0; i < labels.Length; i++)
                {
                    Constellation candidate = labels[i].Constellation;
                    if (candidate != null && candidate.GUID == constellation.GUID)
                    {
                        return labels[i];
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a constellation to its map label threw: " + e);
            }

            return null;
        }

        private static readonly ConstellationLabel[] NoConstellationLabels =
            new ConstellationLabel[0];

        /// <summary>Every constellation label the window is holding, fetched fresh for the same reason
        /// the system labels are: the window instantiates one per constellation as the game meets
        /// them.</summary>
        private static ConstellationLabel[] ConstellationLabels()
        {
            try
            {
                ConstellationLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<ConstellationLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoConstellationLabels
                    : window.GetComponentsInChildren<ConstellationLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the constellation labels threw: " + e);
                return NoConstellationLabels;
            }
        }

        /// <summary>
        /// The order the things on this stop are listed in: north to south by rows a unit high, and
        /// west to east along a row.
        ///
        /// Left alone the list arrives in the galaxy's own node array order, which is whatever order
        /// generation built the nodes in - it means nothing to a player, and a list they count
        /// positions along ("7 of 23") has to have a reason for the position.
        ///
        /// The rule itself is <see cref="MapCoordinates.ReadingOrder"/>, which is engine-free and
        /// unit-tested; everything here is the adapter that turns a thing on the map into the pair of
        /// offsets it speaks.
        ///
        /// Once per build of the stop rather than per frame, through delegates held here rather than
        /// method groups at the call sites, which would allocate one per sort.
        /// </summary>
        private static readonly Comparison<StarSystemNode> ReadingOrder = CompareReadingOrder;

        private static readonly Comparison<Fleet> FleetReadingOrder = CompareFleetReadingOrder;

        /// <summary>The same rule for the stretches of sky, off the centroid the game stores for each
        /// and writes its name at - so the groups read down the map the way their members do.</summary>
        private static readonly Comparison<SkyGroup> ConstellationOrder = CompareConstellationOrder;

        private static int CompareConstellationOrder(SkyGroup left, SkyGroup right)
        {
            return ComparePositions(
                left.Constellation.GalaxyPosition,
                right.Constellation.GalaxyPosition
            );
        }

        private static int CompareReadingOrder(StarSystemNode left, StarSystemNode right)
        {
            return ComparePositions(left.GalaxyPosition, right.GalaxyPosition);
        }

        private static int CompareFleetReadingOrder(Fleet left, Fleet right)
        {
            return ComparePositions(left.GalaxyPosition, right.GalaxyPosition);
        }

        private static int ComparePositions(GalaxyPosition left, GalaxyPosition right)
        {
            double leftEast,
                leftNorth,
                rightEast,
                rightNorth;
            GalaxyCoordinates.Offsets(left, out leftEast, out leftNorth);
            GalaxyCoordinates.Offsets(right, out rightEast, out rightNorth);
            return MapCoordinates.ReadingOrder(leftEast, leftNorth, rightEast, rightNorth);
        }

        /// <summary>The map's own rule for whether a node's name is drawn: it has been explored, and
        /// it is either remembered or in sight now. Everything this screen ever says the name of is
        /// asked this first - and so is everything a ROUTE across the map names, which is why the rule
        /// itself lives in <see cref="MapVisibility"/> rather than here.</summary>
        private static bool Perceived(GameNode node, Empire empire)
        {
            return MapVisibility.Perceived(node, empire);
        }

        /// <summary>One place in a stretch of sky, as whichever row the map's own drawing of it allows:
        /// the full system row for a star the map is naming, and the bare one for a star it is only
        /// drawing (<see cref="AddLocated"/>).</summary>
        private void AddPlace(
            GraphBuilder builder,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel[] labels
        )
        {
            if (_located.Contains(node))
            {
                AddLocated(builder, node, empire);
                return;
            }

            AddSystem(builder, node, empire, _colonies.Contains(node), labels);
        }

        /// <summary>
        /// A place the map is drawing a star at and refusing to name.
        ///
        /// What the picture gives a player here is a generic body with an orbit ring round it and
        /// nothing else: no name, no real star type, no label, no dossier, and the mouse cannot even
        /// highlight it (<see cref="MapVisibility.Located"/>). So the row is the mod's own words for
        /// what it is, and then the one thing the picture really does say - WHERE it is. The system's
        /// real name is never spoken here and never indexed: the simulation knows it, the map is
        /// withholding it, and a row that leaked it would be handing the player something no sighted
        /// player can see. Two of these are told apart the way two of anything on this map are: by
        /// their coordinates.
        ///
        /// Nothing hangs under it except FLEETS, and those under exactly the gate every other fleet on
        /// the map passes (<see cref="AddFleets"/>) - because the one thing the game routinely shows at
        /// a place like this is somebody else's fleet parked there, which is often how the place came
        /// to be known at all. There is no branch at all where the map draws no fleet: an empty branch
        /// would be a row saying "there is more in here" over nothing.
        ///
        /// No planets, no lanes, no owner, no dossier and no management page: none of them is drawn,
        /// and the game refuses the click that would open them. Enter still brings the camera in, as it
        /// does on any place - the camera is the player's to point wherever they like, and what it
        /// finds there is the same generic star a mouse-driven player would fly to.
        ///
        /// Backslash is that zoom's other half and NOTHING else. On a system the map is naming it is
        /// two things - send the selection here, or come back out (<see cref="SystemCommand"/>) - and
        /// the first of them does not exist at a place like this: the mouse cannot so much as highlight
        /// the node, so there is no click a sighted player could give the order with, and offering one
        /// would be handing the keyboard a move the picture refuses. What is left is the way back out,
        /// which the row must keep: Enter zooms in, and nothing else on this page ever zooms out by
        /// itself.
        /// </summary>
        private void AddLocated(GraphBuilder builder, StarSystemNode node, Empire empire)
        {
            StarSystemNode it = node;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ModStrings.Get(ModStrings.GalaxySystemUnexplored)),
                    GalaxyCoordinates.Part(node.GalaxyPosition),
                },
            };

            // What is parked here, in the same count phrase every other place on the map uses - so the
            // number the row says and the children it opens onto stay the same answer read two ways.
            IList<Fleet> fleets = FleetPresence.FleetsAt(node);
            if (fleets.Count > 0)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetPresence.At(it), false));
            }

            // The game's own left click: the camera comes in, and nothing is selected or opened.
            vtable.OnActivate = () => ZoomIn(it);
            vtable.OnContextual = () => ZoomOut(it);

            string place = SystemKey(node, empire);
            ControlId id = ControlId.For(it, place);
            if (fleets.Count == 0)
            {
                // Synthetic: a system is a place in the galaxy model, and the walk that listed it is what says it is there.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            // A container from here down, and said to be one exactly as every other place on the map
            // that opens onto what is standing in it.
            vtable.ControlType = ControlTypes.Group;
            HashSet<ControlId> expansion = builder.Expansion;
            ControlId group = id;
            vtable.OnCollapse = () => Collapse(expansion, group, it);
            // Synthetic for the same reason as the leaf above.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                AddFleets(builder, place, fleets);
            }

            builder.EndGroup();
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
            StarSystemLabel drawn = label;
            NodeVtable vtable = GraphNodes.Group(() => it.LocalizedName);
            // Where on the map it is, straight after its name and before anything it happens to be
            // today - the pair is part of what the place is CALLED for a player steering by it
            // (<see cref="GalaxyCoordinates"/>). Taken once here rather than read per frame: a node's
            // position is fixed at galaxy generation.
            vtable.Announcements.Add(GalaxyCoordinates.Part(node.GalaxyPosition));
            // Then whose place it is - the one thing about a system a player scanning the map wants
            // before anything else, and the map draws it only as the colour it tints the name in.
            // The game's own word for the owner, its own word for a place with nobody on it, and
            // its own word for a home system (<see cref="SystemOwner"/>). Nothing at all for a
            // system of the player's, which is what "no word" has always meant on this map. Not
            // watched: ownership changes at the turn's end and the game raises its own notification
            // for it, and the answer costs a walk of the colonies standing at the node.
            Empire looking = empire;
            vtable.Announcements.Add(GraphNodes.ValuePart(() => SystemOwner(it, looking), false));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => HomeSystemWord(it, looking), false));
            // What is parked here, then everything the map writes on the label itself - the icons it
            // flanks the name with, what is being built, what is in the ground - and last the dossier
            // behind the star. The middle one is a page of detail drawn as pictures, so it is reviewed
            // rather than spoken (<see cref="SystemLabelReadout"/>).
            vtable.Sections = GraphNodes.Sections(
                // First while an order is waiting for a target, because it is what the player is here
                // to read: the game answers a mouse hovering a target with the shot's own consequences
                // and its refusals, and Enter on this node is that click
                // (<see cref="CursorTargeting.PreviewLines"/>). Silent the rest of the time, which is
                // almost always.
                NodeSection.Buffer(() => CursorTargeting.PreviewLines(it)),
                NodeSection.Buffer(() => FleetPresence.LinesAt(it)),
                // What sending the selection here would mean, turn by turn - nothing at all while no
                // fleet is selected, which is most of the time (<see cref="FleetRoute"/>).
                NodeSection.Buffer(() => FleetRoute.PreviewLines(it)),
                // How many live there. It left the SPOKEN readout when the system's own dossier became
                // a node of its own - the figure is a line of that dossier, and saying it again on the
                // way past every system on the map is the same number twice - but it stays in the
                // buffer, which is where a player reads a place they are considering rather than one
                // they are passing (owner-ruled).
                NodeSection.Buffer(() => Line(SystemLabelReadout.Population(drawn))),
                NodeSection.Buffer(() => SystemLabelReadout.Lines(drawn)),
                // What the map draws AT the place rather than on its label: how far this colony's own
                // influence reaches, the ring round a held node, the disk of a time bubble, the pins a
                // quest has planted. All four are colour and shape with no words anywhere near them.
                // The reach is reviewed rather than spoken because it is a number to plan a colony
                // with, not news - whose influence has WON the place is the spoken half
                // (<see cref="SystemInfluence"/>).
                NodeSection.Buffer(() => SystemInfluence.RadiusLines(it, empire)),
                NodeSection.Buffer(() => GuardLines(it, empire)),
                NodeSection.Buffer(() => TimeBubbleLines(it, empire)),
                NodeSection.Buffer(() => QuestMarkerLines(it, empire)),
                StarDossier(it, empire, drawn)
            );
            // What the place IS, where it is not a star system at all. Said first, because it is the
            // thing a sighted player takes in without asking: the map gives a special node a body of
            // its own (<see cref="SpecialKind"/>) while its name is a bare catalogue number that
            // gives nothing away. Not watched - a node cannot become a different phenomenon.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => SpecialKind(it), false));

            // Then whose influence is standing over the place, and who else is reaching for it - said
            // as soon as the row has finished saying what and where the place is, because between them
            // they answer "can I have this?": a system under somebody else's influence refuses a colony
            // ship and can change hands on its own, and the contest is the warning that it is about to
            // (<see cref="SystemInfluence"/>). Nothing at all for the ordinary case, a place inside its
            // own empire's circle. Not watched: influence moves at the turn's end and the game raises
            // its own notification when a system is converted, so there is nothing here for a standing
            // cursor to interrupt itself over.
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => SystemInfluence.UnderInfluence(it, empire), false)
            );
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => SystemInfluence.Contested(it, empire), false)
            );

            if (owned)
            {
                // A system of yours is either a colony or still an OUTPOST, and the map draws the two
                // differently - so they say different words rather than both saying "colonized".
                Empire owner = empire;
                vtable.Announcements.Add(GraphNodes.ValuePart(() => OwnedState(it, owner)));
            }

            // How many of the population are the player's own agents (drawn only where there is one).
            // Everything else the label says is a page of pictures and is reviewed, not spoken. Not
            // watched - it is read off a widget the map pools and re-points at other systems as the
            // camera moves.
            //
            // The population COUNT is no longer said here: the figure is one line of the system's own
            // dossier, and that dossier is a node of its own now (<see cref="TooltipChildren"/>), so
            // saying it in the readout as well would put the same number in front of the player twice
            // on the way past every system on the map - owner-ruled.
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => SystemLabelReadout.Sleepers(drawn), false)
            );

            // Whether somebody is fighting for the ground here. Spoken rather than reviewed: it is the
            // one thing on a system that changes who owns the place within a turn or two, and the map
            // says it in front of the player with an icon beside the name. Not watched - the answer
            // costs a repository lookup, and it cannot change under a standing cursor.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => GroundBattle(it, empire), false));

            // What the map draws parked here, in the game's own count phrase. Not watched: the answer
            // costs a walk of the docking-slot repository, and a watched part walks it every frame the
            // system is focused.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetPresence.At(it), false));

            // ...and how many are under way on the lanes leaving here, which is the second half of the
            // same answer: the branch opens onto both sets of fleets, so the count has to name both or
            // the number the player was told and the children they walk stop matching. Worked out from
            // the same lane list the branch is built from (<see cref="LanesOf"/>), so "nearby" means
            // exactly the lanes this system offers. Not watched, for the reason above and one more: it
            // walks the visible-fleet repository once per lane.
            Empire counting = empire;
            vtable.Announcements.Add(GraphNodes.ValuePart(() => UnderWayNearby(it, counting), false));

            // And what it would cost to send the selection here - the picture the map draws for a mouse
            // hovering over this system, in words. Silent while nothing is selected. Emphatically not
            // watched: the answer is a pathfinding search (<see cref="FleetRoute"/>).
            vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetRoute.Preview(it), false));

            // The two clicks the map itself puts on a system, and nothing invented on top of them.
            vtable.OnActivate = () => ZoomIn(it);
            vtable.OnContextual = () => SystemCommand(it);
            MoveHints(vtable);

            // The camera is not moved here: it follows the cursor by the page's one rule, which reads
            // this row as the system itself and slides the camera across to it
            // (<see cref="OnFocusVisual"/>).
            //
            // Once the camera is all the way in, the map pushes the system's own label off the top of
            // the screen and draws a tooltip anchor on the star instead - so that is what the pointer
            // is put on, or a tooltip meant for the system would be drawn where nobody can see it.
            // Unless that anchor's card is the THIN one the game binds for a system it does not own
            // (<see cref="OrbitalStarDossier"/>), in which case the fuller card wins and is drawn
            // wherever it hangs: what the row says about a system must not change with the zoom.
            //
            // Asked at the moment of aiming, through the same rule that decided what the row DECLARES
            // (<see cref="StarAim"/>): the answer depends on where the camera is and the camera moves
            // while the cursor stands still, and the orbital window's star tooltip is ONE widget it
            // re-points at whatever the camera is looking at. A widget resolved when the row was built
            // is a widget the game may have given to another system by the time the player arrives -
            // which is how a system came to be described by its neighbour's dossier.
            Empire aiming = empire;
            // Declared as well as performed: the navigator re-commits a standing cursor's pointer when
            // this answer changes, which is the same question the visual below asks.
            vtable.PointsAt = () => StarAim(it, aiming, LabelFor(it, SystemLabels()));
            vtable.OnFocusVisual = () =>
            {
                StarSystemLabel drawing = LabelFor(it, SystemLabels());
                AgeTooltip star = StarAim(it, aiming, drawing);
                if (star == null)
                {
                    return;
                }

                // The label's own tooltip is drawn under the WHOLE label rather than under the star
                // inside it; the orbital window's and the mod's own carrier stand where they are.
                bool onTheLabel =
                    drawing != null && ReferenceEquals(star, drawing.StarTooltip);
                PointerFocus.MoveTo(
                    null,
                    star,
                    onTheLabel ? drawing.AgeTransform : star.AgeTransform
                );
            };
            vtable.OnBlurVisual = ReleasePointer;

            // Right means "tell me what is inside this", and what is inside it is whatever the map is
            // drawing there: the circles when the camera is out, the orbital cards when it is in...
            string place = SystemKey(node, empire);
            ControlId id = ControlId.For(it, place);
            // ...and opening one no longer moves the camera itself: Right opens the branch AND steps
            // inside it, and the first child's own focus is what brings the camera in, through the one
            // rule (<see cref="OnFocusVisual"/>). So expansion is left to the engine and only the
            // CLOSING is an override, because coming back out is a camera move nothing else makes.
            HashSet<ControlId> expansion = builder.Expansion;
            ControlId group = id;
            vtable.OnCollapse = () => Collapse(expansion, group, it);
            // Synthetic: a place on the map, assembled from the galaxy model rather than drawn as one thing.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            // Only what is open costs anything: a galaxy of closed systems declares one node each.
            if (builder.IsExpanded(id))
            {
                object outer = TooltipChildren.Actions(builder, place);
                AddInside(builder, place, node, empire, label);
                TooltipChildren.Emit(
                    builder,
                    place,
                    SystemDossiers(node, empire, label),
                    outer
                );
            }

            builder.EndGroup();
        }

        /// <summary>One line as a buffer section's list, or nothing where there is no line.</summary>
        private static IList<string> Line(string text)
        {
            return string.IsNullOrEmpty(text) ? null : new string[] { text };
        }

        /// <summary>
        /// The dossiers the map hangs on a system beyond the ones its children already carry: the
        /// system's own stat block, and one per kind of deposit found in the ground.
        ///
        /// The star, the name and the population count all carry the SAME dossier - one wrapper, three
        /// widgets (measured on Osulo: identical <c>GuiStarSystem</c> target on all three) - so it is
        /// one node, named the way the game's own header names it ("Osulo - Niris"). Which of the two
        /// star tooltips is asked for is <see cref="StarDossier"/>'s rule: the map keeps one on the
        /// label and another over the star once the camera is in, and only the one being drawn has any
        /// words at all.
        ///
        /// Everything else on the label that carries a dossier is already a node here - the planets,
        /// the fleet lozenges, the diplomacy button - so none of them is declared twice.
        /// </summary>
        private static List<TooltipChildren.Dossier> SystemDossiers(
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(4);
            try
            {
                StarSystemNode it = node;
                Empire looking = empire;
                StarSystemLabel drawn = label;
                AgeTooltip star = StarAim(node, empire, label);
                TooltipChildren.Add(
                    found,
                    star,
                    star == null ? null : star.AgeTransform,
                    () => StarDossierLines(it, looking, drawn),
                    // The words were always asked for afresh; the AIM and the header line are asked
                    // the same way now, or the node reads a system the camera has moved on from.
                    () => StarAim(it, looking, LabelFor(it, SystemLabels()))
                );
                AddDeposits(found, node, empire, label);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's dossiers threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// One dossier per KIND of deposit in this system's ground, read off the planets rather than
        /// off the icons the label happens to be drawing.
        ///
        /// The label draws its deposit strip only from a close enough camera, so taking the list from
        /// the strip made a system's deposits reachable at one zoom and gone at another - for content
        /// the map is not withholding at all (the fog gates are the planets': everything here is under
        /// <c>MapVisibility.Perceived</c> and the branch's own expansion). The list is built exactly
        /// as <c>StarSystemLabel.RefreshDepositsLine</c> builds it - every planet's deposits in orbit
        /// order, deduped by definition name - so the order the player walks is the order the icons
        /// are drawn in.
        ///
        /// The AIM still prefers the game's own icon wherever the game is drawing one (owner ruling
        /// 2026-08-23), so a sighted player sees the tooltip appear over the deposit it belongs to;
        /// a carrier of the mod's own stands in only where there is no icon on the screen, and the
        /// words are the same either way because the tooltip window assembles them from the wrapper.
        /// A drawn item is matched to the definition it is BOUND to rather than taken by position,
        /// which is also what stops a stale binding on a culled-out label being read.
        /// </summary>
        private static void AddDeposits(
            List<TooltipChildren.Dossier> found,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            ColonizedStarSystem colony = LabelColony(node, empire);
            Empire owner = colony == null ? null : colony.Empire;
            List<ResourceDepositDefinition> kinds = DepositKinds(node);
            StarSystemNode it = node;
            Empire looking = empire;
            for (int i = 0; i < kinds.Count; i++)
            {
                ResourceDepositDefinition definition = kinds[i];
                ResourceDepositDefinition kind = definition;
                AgeTooltip tooltip = DepositAim(node, definition, label, owner);
                TooltipChildren.Add(
                    found,
                    tooltip,
                    tooltip == null ? null : tooltip.AgeTransform,
                    null,
                    // The label's deposit strip is drawn only from close enough and its items are
                    // pooled among the deposits the label is showing, so which widget carries a kind
                    // is a question about the camera - asked again every time the pointer is aimed
                    // rather than once when the node was declared.
                    () =>
                        DepositAim(
                            it,
                            kind,
                            LabelFor(it, SystemLabels()),
                            DepositOwner(it, looking)
                        )
                );
            }
        }

        /// <summary>The widget a kind of deposit's dossier is drawn through right now: the label's own
        /// icon wherever the map is drawing one for it, else a carrier of the mod's.</summary>
        private static AgeTooltip DepositAim(
            StarSystemNode node,
            ResourceDepositDefinition definition,
            StarSystemLabel label,
            Empire owner
        )
        {
            bool drawing = label != null && AgeWidgets.Painted(label.AgeTransform);
            AgeTooltip icon = drawing ? DrawnDeposit(label, definition) : null;
            return icon ?? DepositCarrier(node, definition, owner);
        }

        /// <summary>Whose colony the deposits are being read under, which is what a carrier is stamped
        /// with.</summary>
        private static Empire DepositOwner(StarSystemNode node, Empire empire)
        {
            ColonizedStarSystem colony = LabelColony(node, empire);
            return colony == null ? null : colony.Empire;
        }

        /// <summary>Every kind of deposit in a system's ground, in the order the label's strip draws
        /// them: planet by planet, deposit by deposit, one entry per definition NAME
        /// (<c>StarSystemLabel.RefreshDepositsLine</c>).</summary>
        private static List<ResourceDepositDefinition> DepositKinds(StarSystemNode node)
        {
            List<ResourceDepositDefinition> kinds = new List<ResourceDepositDefinition>(4);
            try
            {
                for (int i = 0; i < node.Planets.Count; i++)
                {
                    Planet planet = node.Planets[i];
                    for (int j = 0; j < planet.ResourceDeposits.Count; j++)
                    {
                        ResourceDepositDefinition definition = planet.ResourceDeposits[j].Definition;
                        if (definition == null || Holds(kinds, definition))
                        {
                            continue;
                        }

                        kinds.Add(definition);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: listing a system's deposits threw: " + e);
            }

            return kinds;
        }

        private static bool Holds(
            List<ResourceDepositDefinition> kinds,
            ResourceDepositDefinition definition
        )
        {
            for (int i = 0; i < kinds.Count; i++)
            {
                if (kinds[i].Name == definition.Name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The label's own icon for one kind of deposit, where it is drawing one. Found by
        /// what the icon is BOUND to, never by position: an icon the label bound for another system
        /// and has not refreshed since answers no.</summary>
        private static AgeTooltip DrawnDeposit(
            StarSystemLabel label,
            ResourceDepositDefinition definition
        )
        {
            AgeTooltip found = DrawnDeposit(label.DepositsMainTable, definition);
            return found ?? DrawnDeposit(label.DepositsSecondaryTable, definition);
        }

        private static AgeTooltip DrawnDeposit(
            AgeTransform table,
            ResourceDepositDefinition definition
        )
        {
            if (!Visible(table))
            {
                return null;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                // Content: which icon carries this deposit's sentence. The table pools its items, and a
                // retired one is faded rather than hidden while it still holds the last binding.
                if (!AgeWidgets.Painted(item))
                {
                    continue;
                }

                AgeTooltip tooltip = Raw(item);
                GuiResourceDepositGroup group =
                    tooltip == null ? null : tooltip.Target as GuiResourceDepositGroup;
                if (group != null && group.Definition != null
                    && group.Definition.Name == definition.Name)
                {
                    return tooltip;
                }
            }

            return null;
        }

        /// <summary>A carrier of the mod's own bound exactly as <c>StarSystemLabelDepositItem.Bind</c>
        /// binds the game's icon - the same class, the same wrapper, the same refusal text - so the
        /// tooltip window assembles the same panel for it.</summary>
        private static AgeTooltip DepositCarrier(
            StarSystemNode node,
            ResourceDepositDefinition definition,
            Empire owner
        )
        {
            try
            {
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "deposit/" + node.GUID + "/" + definition.Name,
                    DossierStamp(owner),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiResourceDepositGroup group = new GuiResourceDepositGroup(
                        node,
                        definition,
                        owner
                    );
                    List<FailureInfo> refusals = new List<FailureInfo>();
                    group.IsExploited(PlayerEmpire(), refusals);
                    carrier.Class = group.TooltipClass;
                    carrier.Content = Gui.FormatFailureInfos(refusals);
                    carrier.Context = null;
                    carrier.Target = group;
                }

                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: binding a deposit dossier threw: " + e);
                return null;
            }
        }

        /// <summary>What a dossier built from the simulation depends on: the turn it was read in and
        /// whose empire it was read for. Everything a deposit group or a star system counts - what is
        /// exploited, what the empire may exploit at all, who lives there - settles at the turn's end,
        /// and rebinding a carrier more often than that would restart the tooltip's own countdown
        /// every frame and it would never finish appearing.</summary>
        private static long DossierStamp(Empire owner)
        {
            try
            {
                Game game = Gui.Game;
                long stamp = game == null ? 0L : game.Turn * 1000003L;
                return (stamp * 31L) + (owner == null ? 0L : owner.Index + 1L);
            }
            catch (Exception)
            {
                return 0L;
            }
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
        private void Collapse(HashSet<ControlId> expansion, ControlId group, StarSystemNode node)
        {
            if (expansion != null)
            {
                expansion.Remove(group);
            }

            if (ReferenceEquals(GalaxyViewLevels.FocusedSystem, node))
            {
                ZoomOut(node);
            }

            // Whether or not there was a zoom to undo, the branch is shut: the camera is no longer
            // reading the inside of this system, so opening it again brings the camera back in
            // (<see cref="LeftPlace"/>). Backslash deliberately does NOT do this - a zoom-out by hand
            // is the player choosing to go on reading the same place from further off.
            LeftPlace(node);
        }

        /// <summary>
        /// What the map draws inside a system, as the children of that system's ONE node.
        ///
        /// One node, because travelling a lane rebases the cursor onto the destination's own node rather
        /// than declaring a copy of it (<see cref="AddStarlanes"/>). So there is no second way in whose
        /// contents could come to differ, no structural re-keying of everything underneath, and nothing
        /// here has to be made poorer than anything else.
        ///
        /// The fleets are in THREE groups because the map draws them at three distances: what is parked
        /// here, then what is under way on the lanes leaving here - the latter under both ends of its
        /// lane, each saying which lane it is on (<see cref="AddEnRoute"/>) - and last what is crossing
        /// the open space TOWARDS here with no lane to fly (<see cref="AddFreeMoving"/>), which hangs
        /// under this end alone.
        /// </summary>
        private void AddInside(
            GraphBuilder builder,
            string key,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            List<Lane> lanes = LanesOf(node, empire);
            AddManagementView(builder, key, node, label);
            AddLabelButtons(builder, key, label);
            AddPlanets(builder, key, node, empire, label);
            AddWrecks(builder, key, node);
            AddStarlanes(builder, key, node, empire, lanes);
            AddFleets(builder, key, FleetPresence.FleetsAt(node));
            AddEnRoute(builder, key, EnRouteOn(node, lanes));
            AddFreeMoving(builder, key, node, FreeMovingAt(node));
            // After the planets, the lanes and the fleets: a quest pin is the last thing the map draws
            // at a place, and it is a thing about the QUEST rather than about the system.
            AddQuestMarkers(builder, key, node, empire);
            AddHangars(builder, key, node);
            AddProbeDirections(builder, key, node);
        }

        /// <summary>
        /// What a place on the map is, where it is one of the galaxy's phenomena rather than a star
        /// system - a solar nebula, a collapsing star, an asteroid field. Nothing at all for an
        /// ordinary system, which needs no telling: it is what the map is made of.
        ///
        /// A special node is a <c>StarSystemNode</c> with no planets and a body of its own drawn over
        /// the star (<c>GalaxySpecialNode.UpdateVisualAccordingToExploration</c>), and its name is a
        /// bare catalogue number - so a sighted player knows what they are looking at from the picture
        /// and a keyboard player was told nothing. The kind is only ever written down in the dossier
        /// behind the star, which is a tooltip the player has to go and read.
        ///
        /// The words are the game's own - the same expression the dossier's header draws
        /// (<c>GuiSpecialNode.CategoryTitle</c>), so this cannot drift from the line the buffer
        /// already carries, and there is nothing here to translate.
        /// </summary>
        internal static string SpecialKind(StarSystemNode node)
        {
            try
            {
                SpecialNode special = node as SpecialNode;
                SpecialNodeDefinition definition =
                    special == null ? null : special.SpecialNodeDefinition;
                return definition == null ? null : Gui.GetLocalizedTitle(definition.Name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whose place this is, in the game's own words.
        ///
        /// The map answers this with COLOUR - it tints the system's name in the owner's colour - and
        /// says it in words only inside the system's own dossier, whose header is "Osulo - Niris"
        /// (<c>GuiStarSystem.Title</c>). So the word here is the one that header uses,
        /// <c>GuiEmpire.GetLeaderName</c>, which is also what already answers "Unknown Empire" for an
        /// empire the player has not met and names a minor civilization per SYSTEM rather than by its
        /// one empire object.
        ///
        /// Nothing at all for a system of the player's own: "mine" is the unmarked case on this map,
        /// and the colonized/outpost word that follows already says it is held.
        ///
        /// Gated on the colonies the player can SEE, exactly as <c>SystemInfluence</c> gates its own
        /// naming (<c>Visibility >= 1</c>): a colony the map is hiding is not named, and the answer for
        /// a node with none the player can see is the game's own "No owner" - which is what the map is
        /// showing, whatever the simulation knows.
        /// </summary>
        private static string SystemOwner(StarSystemNode node, Empire empire)
        {
            try
            {
                if (node == null || empire == null || !MapVisibility.Perceived(node, empire))
                {
                    return null;
                }

                ColonizedStarSystem owner = VisibleColony(node, empire);
                if (owner == null)
                {
                    return AgeText.Clean(Gui.Localize(NoOwnerKey));
                }

                if (ReferenceEquals(owner.Empire, empire))
                {
                    return null;
                }

                GuiEmpire wrapper = Gui.GuiWrapperProviderService.GetGuiEmpire(owner.Empire);
                return wrapper == null
                    ? null
                    : AgeText.Clean(
                        wrapper.GetLeaderName(owner.GUID, empire, false, false, false)
                    );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's owner threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The game's own word for a home system, on any empire's.
        ///
        /// Said only where the player can see a colony standing here - the same gate the owner word
        /// uses, and the reason the fog gives nothing away: <c>HomeSystemEmpireIndex</c> is set on
        /// every home system in the galaxy from the moment it is generated, so reading it ungated
        /// would tell the player which unexplored star an empire they have never met came from.
        ///
        /// The map's own icon is narrower than this - it draws one only for a MAJOR empire's home
        /// system (<c>StarSystemLabel.RefreshHomeSystemLine</c> :2272) - so a minor civilization's
        /// home, which is the whole of that civilization, would be said nowhere. Owner-ruled to say
        /// it for any empire's.
        /// </summary>
        private static string HomeSystemWord(StarSystemNode node, Empire empire)
        {
            try
            {
                if (
                    node == null
                    || empire == null
                    || !node.IsHomeSystem
                    || !MapVisibility.Perceived(node, empire)
                    || VisibleColony(node, empire) == null
                )
                {
                    return null;
                }

                // The game's own key ends in a space, because it draws it in front of something else.
                return AgeText.Clean(Gui.Localize(HomeSystemKey)).Trim();
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading whether a system is a home system threw: " + e);
                return null;
            }
        }

        private static readonly string NoOwnerKey = "%MarketplaceScreenNoOwnerTitle";

        private static readonly string HomeSystemKey = "%HomeSystemTitle";

        /// <summary>The colony standing at a node that the player is being SHOWN - the strongest claim
        /// the map is drawing there. A ghost is not one: an empire keeps a ghost of a system it has
        /// lost, and the map draws nothing for it.</summary>
        private static ColonizedStarSystem VisibleColony(StarSystemNode node, Empire empire)
        {
            IColonizedStarSystemRepositoryService colonies =
                Amplitude.Unity.Framework.Services.GetService<IColonizedStarSystemRepositoryService>();
            if (colonies == null)
            {
                return null;
            }

            ColonizedStarSystem found = null;
            foreach (ColonizedStarSystem colony in colonies.GetValues(node.NodePosition))
            {
                if (
                    colony.Empire == null
                    || colony.State == StarSystemState.Ghost
                    || (int)colony.Visibility[empire] < 1
                )
                {
                    continue;
                }

                if (ReferenceEquals(colony.Empire, empire))
                {
                    return colony;
                }

                if (found == null)
                {
                    found = colony;
                }
            }

            return found;
        }

        /// <summary>
        /// The colony a system's map LABEL binds its dossiers with -
        /// <c>StarSystemLabel.RebuildColonizedStarSystemsList</c>'s <c>MainColonizedStarSystem</c>,
        /// replicated so that a dossier the mod builds itself is named the way the label's is.
        ///
        /// Not <see cref="VisibleColony"/>: that one answers "what claim is drawn here" and counts an
        /// OUTPOST, while the label counts only a full colony - which is why Heka's dossier is called
        /// "Heka" and Osulo's "Osulo - Niris". Reading the wrong one made the same card read
        /// differently either side of a zoom, which is exactly what sourcing from data is for.
        /// </summary>
        private static ColonizedStarSystem LabelColony(StarSystemNode node, Empire empire)
        {
            IColonizedStarSystemRepositoryService colonies =
                Amplitude.Unity.Framework.Services.GetService<IColonizedStarSystemRepositoryService>();
            if (colonies == null)
            {
                return null;
            }

            ColonizedStarSystem found = null;
            foreach (ColonizedStarSystem colony in colonies.GetValues(node.NodePosition))
            {
                if (
                    (int)colony.Visibility[empire] >= 1
                    && (found == null || !ReferenceEquals(found.Empire, empire))
                    && colony.State == StarSystemState.Colony
                )
                {
                    found = colony;
                }
            }

            return found;
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
        /// Takes any node on the map rather than a system, because the map hangs lanes off things that
        /// are not systems and every one of them answers the same click.
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
        /// What the move keys do on a place a fleet could be sent to, said at the end of the node's
        /// buffer while there is a selection to send.
        ///
        /// Two lines and the same action twice: the map's move click is the Contextual action, and its
        /// off-lane variant is that action's SECOND chord rather than a wiring of its own, because the
        /// game runs one handler for both clicks and reads the physical Control inside it
        /// (<see cref="ES2Access.UI.FleetOrders"/>). So the hints name the action and the chord index,
        /// and a rebind of either chord re-words its own line.
        ///
        /// The second line is gated on the selection really being able to fly off the lanes
        /// (<see cref="ES2Access.UI.FleetOrders.AnySelectedCanFreeMove"/>): naming a chord that can
        /// only ever be refused is worse than saying nothing.
        /// </summary>
        private static void MoveHints(NodeVtable vtable)
        {
            NodeHints.Add(
                vtable,
                ModStrings.HintMoveFleetHere,
                UiActions.Contextual,
                0,
                FleetOrders.AnySelected
            );
            NodeHints.Add(
                vtable,
                ModStrings.HintFreeMovement,
                UiActions.Contextual,
                1,
                FleetOrders.AnySelectedCanFreeMove
            );
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
                List<FailureInfo> refusals = new List<FailureInfo>();
                SendAll(SendableTo(node, selected, refusals), refusals);
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

        /// <summary>
        /// The way into a system's page from the map - the button the mouse takes where the map is
        /// drawing one, and the route behind it wherever that route would really open a page.
        ///
        /// Declared while the game is drawing the button, and pressed only while the game will act on a
        /// press. Those are two different questions here: the label greys the button out on anything but
        /// a COLONY of ours (<c>StarSystemLabel</c> :1626-1648 assigns the system it enables from at
        /// :1750 only while the state is <c>Colony</c>), while the view level behind it opens for any
        /// system of ours that is not lost AND for anybody else's system we hold a traitor in
        /// (<c>GuiManager.RequestStarSystemManagementViewLevel</c> :1224-1251). So an OUTPOST is drawn a
        /// dead button over a page that would open perfectly well, and dropping the node left every
        /// colony a one-key route into its page while an outpost's had to be flown to on the zoom
        /// ladder. A system somebody else holds and we have turned somebody inside is the same story
        /// wearing a foreign flag: the button is drawn greyed there too, and the page behind it opens.
        ///
        /// The greyed-out button is therefore still declared wherever that route would really open a
        /// page (<see cref="Manageable"/>), and takes the route itself rather than pressing a button
        /// that would do nothing. Nowhere else: on somebody else's system or an empty one the same call
        /// silently degrades to centring the map (<see cref="GalaxyViewLevels.OpenSystem"/>), which is
        /// not a page and would be a node that says it opens something and does not.
        ///
        /// Being DRAWN is the other half, and it is a question about the MAP rather than about the
        /// system: a label the map is not drawing carries no button to light up, to hover or to press.
        /// It briefly stopped being asked here (2026-08-26), because a search landing inside Sabel left
        /// that system with nine children and no way into its page, permanently - the map had been
        /// snapped in on a system whose label it had never been told to draw. That was the snap's
        /// omission and not this gate's: the snap now leaves the map's labels the way a flight would
        /// have (<see cref="GalaxyViewLevels.CatchUpLabels"/>), the button is drawn on every route in,
        /// and the reading can go back to describing what is on the screen.
        /// </summary>
        private static void AddManagementView(
            GraphBuilder builder,
            string key,
            StarSystemNode node,
            StarSystemLabel label
        )
        {
            AgeTransform button = label == null ? null : label.RequestManagementViewButton;
            if (button == null || !Visible(button))
            {
                return;
            }

            if (!AgeWidgets.Operable(button) && !Manageable(node))
            {
                return;
            }

            AgeTransform it = button;
            StarSystemNode at = node;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(ModStrings.GalaxyOpenSystem),
                () => OpenManagementView(it, at),
                null,
                Raw(it)
            );
            PointAt(vtable, it);
            // SYNTHETIC on purpose, and it is the one node in this file whose nature was measured
            // rather than reasoned. The button is a real widget and declaring it DRAWN was tried
            // (2026-08-27): the map's own label prefab keeps the button Visible at alpha 0.5 while its
            // grandparent StarSystemNameLine sits at alpha 0 and settled, which is what the gate's
            // chain walk asks about - so the route into a system's page vanished at both ends of the
            // zoom ladder. Measured across the thirteen camera steps with a colony in view: at step 0
            // all 13 drawn buttons failed the chain, at step 12 both of them did, and expanding Dusay
            // through the tree - which flies the camera to step 12 itself - left the expanded system
            // with no /management node at all (DevProbe.GateDiff: onlyUngated =
            // galaxy:constellation/446/system/535/management). Being drawn is asked HERE instead, one
            // step on the button, which is the test that matches how this prefab retires a label.
            builder.AddItem(Nodes.Synthetic(ControlId.Structural(key + "/management"), vtable));
        }

        /// <summary>The map's own way into a system's page: the label's button while the game is willing
        /// to be pressed, and the request the button would have made where it is only drawn greyed
        /// out.</summary>
        private static void OpenManagementView(AgeTransform button, StarSystemNode node)
        {
            if (AgeWidgets.Operable(button))
            {
                AgeWidgets.Press(button);
                return;
            }

            GalaxyViewLevels.OpenSystem(node);
        }

        /// <summary>Whether asking for a system's management page would really open one - the game's own
        /// conditions for it, asked of the same repository it asks
        /// (<c>GuiManager.RequestStarSystemManagementViewLevel</c> :1224-1251). The node must not be
        /// blacked out; then either we hold a system here that is not lost (:1236-1239), or we hold a
        /// traitor in this system and somebody is colonized here at all (:1240-1243) - the page opens
        /// on whichever of the two answered (:1251). Neither, and the game falls through to centring
        /// the map on the node (:1246).</summary>
        private static bool Manageable(StarSystemNode node)
        {
            try
            {
                Empire empire = PlayerEmpire();
                IColonizedStarSystemRepositoryService colonies =
                    Amplitude.Unity.Framework.Services.GetService<IColonizedStarSystemRepositoryService>();
                if (node == null || empire == null || colonies == null || node.IsBlackedOut)
                {
                    return false;
                }

                ColonizedStarSystem mine;
                if (colonies.TryGetValue(empire, node.NodePosition, out mine)
                    && mine.State != StarSystemState.Lost)
                {
                    return true;
                }

                if (!node.EmpiresWithTraitors.Contains(empire))
                {
                    return false;
                }

                ColonizedStarSystem theirs;
                colonies.TryGetColony(node.NodePosition, out theirs);
                return theirs != null;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: asking whether a system's page would open threw: " + e);
                return false;
            }
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
        /// Whether the ground of this system is being fought over, and by whom.
        ///
        /// The map says it with one small picture beside the name and no words at all, so the whole
        /// phrase is the mod's. It is gated exactly the way that picture is
        /// (<c>StarSystemLabel.RefreshInvasionContextualIcon</c> :704-751): the node has to be in sight,
        /// its planets have to be visible to this empire, and some colony standing here has to be both
        /// seen and carrying the game's own invasion tag. Anything less and the map is drawing nothing,
        /// so neither is this.
        ///
        /// The attacker is the DISPLAYED one. A privateer fleet fights under somebody else's flag by
        /// design, and the game keeps the two apart on the battle itself
        /// (<c>GroundBattle.AttackerEmpire</c> against <c>DisplayedAttackerEmpire</c>): reading the real
        /// one would tell the player something the game is deliberately hiding from them. Where the
        /// repository has no battle to hand but the tag is set, the bare phrase says what the icon says
        /// - that there is a battle - and names nobody.
        /// </summary>
        private static string GroundBattle(StarSystemNode node, Empire empire)
        {
            try
            {
                if (!Invaded(node, empire))
                {
                    return null;
                }

                IGroundBattleRepositoryService battles =
                    Amplitude.Unity.Framework.Services.GetService<IGroundBattleRepositoryService>();
                GroundBattle battle =
                    battles == null ? null : battles.GetGroundBattleOnNode(node.NodePosition);
                string attacker = battle == null ? null : Owner(battle.DisplayedAttackerEmpire);
                return string.IsNullOrEmpty(attacker)
                    ? ModStrings.Get(ModStrings.GalaxySystemInvaded)
                    : ModStrings.Format(ModStrings.GalaxySystemInvadedBy, attacker);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's ground battle threw: " + e);
                return null;
            }
        }

        /// <summary>The invasion icon's own three conditions, asked of the model the icon asks
        /// them of.</summary>
        private static bool Invaded(StarSystemNode node, Empire empire)
        {
            if (
                node == null
                || empire == null
                || (int)node.Visibility[empire] < (int)EntityVisibility.Layer.Visible
                || node.PlanetsVisibility == null
                || !node.PlanetsVisibility[empire.Index]
            )
            {
                return false;
            }

            IColonizedStarSystemRepositoryService colonies =
                Amplitude.Unity.Framework.Services.GetService<IColonizedStarSystemRepositoryService>();
            if (colonies == null)
            {
                return false;
            }

            foreach (ColonizedStarSystem colony in colonies.GetValues(node.NodePosition))
            {
                if ((int)colony.Visibility[empire] > 1 && colony.IsBeingInvaded)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The time bubbles sitting on this system: what each one is, who put it there, and how long it
        /// has left.
        ///
        /// The map draws a bubble as a coloured disk over the node and writes nothing on it. The game
        /// names the bubble and says who made it inside the system's own dossier, but the turns it has
        /// left appear nowhere on the map at all, which is the one thing a player planning around it
        /// needs. So the line carries all three and the dossier's own words follow it.
        ///
        /// Gated on the bubble's visibility to this empire, which is the disk's own gate
        /// (<c>GalaxyTimeBubble.RefreshVisibility</c> :67-84 hides the object below Visible).
        /// Emphatically NOT on the effects a bubble has on the node it sits on - <c>IsLocked</c> and the
        /// movement refills are true for a bubble nobody can see, and reading them would announce a
        /// bubble the picture is withholding.
        /// </summary>
        private static IList<string> TimeBubbleLines(StarSystemNode node, Empire empire)
        {
            try
            {
                ITimeBubbleRepositoryService bubbles =
                    Amplitude.Unity.Framework.Services.GetService<ITimeBubbleRepositoryService>();
                if (node == null || empire == null || bubbles == null)
                {
                    return null;
                }

                List<string> lines = new List<string>();
                foreach (TimeBubble bubble in bubbles.GetTimeBubbles(node.NodePosition))
                {
                    if (
                        (int)bubble.Visibility[empire] < (int)EntityVisibility.Layer.Visible
                    )
                    {
                        continue;
                    }

                    lines.Add(
                        ModStrings.Format(
                            ModStrings.GalaxySystemTimeBubble,
                            AgeText.Clean(new GuiTimeBubble(bubble).Title),
                            Owner(bubble.Empire),
                            bubble.TurnRemaining
                        )
                    );
                }

                return lines;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's time bubbles threw: " + e);
                return null;
            }
        }

        // ---- quest markers ----

        /// <summary>
        /// The markers standing at one system, as lines for its review buffer - which quests have a
        /// pin here and which of them the player is tracking.
        ///
        /// The same list its own child nodes are built from (<see cref="AddQuestMarkers"/>), because
        /// the buffer says what is here and the nodes are how the player goes to it, and those two
        /// disagreeing is a place that says a quest is here and has no row for it.
        /// </summary>
        private IList<string> QuestMarkerLines(StarSystemNode node, Empire empire)
        {
            List<QuestMarkers.Marker> here = MarkersAt(node, empire);
            if (here.Count == 0)
            {
                return null;
            }

            List<string> lines = new List<string>(here.Count);
            for (int i = 0; i < here.Count; i++)
            {
                string line = QuestMarkers.Name(here[i]);
                if (!string.IsNullOrEmpty(line) && !lines.Contains(line))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>The markers the map draws AT this system, in journal order.</summary>
        private List<QuestMarkers.Marker> MarkersAt(StarSystemNode node, Empire empire)
        {
            List<QuestMarkers.Marker> here = new List<QuestMarkers.Marker>();
            if (node == null)
            {
                return here;
            }

            List<QuestMarkers.Marker> all = QuestMarkers.Of(empire);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Node.IsValid && all[i].Node == node.NodePosition)
                {
                    here.Add(all[i]);
                }
            }

            return here;
        }

        /// <summary>
        /// A quest marker as a CHILD of the system it stands at - after the planets, the lanes and the
        /// fleets, which is the order the rest of a system reads in.
        ///
        /// A marker was a buffer line and nothing else until 2026-08-22, which meant the one place the
        /// game itself offers a "go here" for - the quest pin - was somewhere the tree could not put
        /// the player. It is a leaf: no tooltip (the game hangs none on a marker), and ENTER IS INERT,
        /// because a pin is not clickable on the map either and there is no journal-opening gesture to
        /// invent. What it carries beyond its name is the step's own objective, in the game's words.
        /// </summary>
        private void AddQuestMarkers(
            GraphBuilder builder,
            string key,
            StarSystemNode node,
            Empire empire
        )
        {
            List<QuestMarkers.Marker> here = MarkersAt(node, empire);
            for (int i = 0; i < here.Count; i++)
            {
                // Synthetic: a quest marker is a game fact the map draws as a pin over the world, not as a control.
                builder.AddItem(Nodes.Synthetic(MarkerId(node, here[i]), MarkerNode(here[i])));
            }
        }

        /// <summary>Every marker planted out in the OPEN - on a fleet crossing a lane - as a row of
        /// the galaxy's own drifting region, beside the probes and the missiles, since there is no
        /// place in the tree for it to hang under.</summary>
        private void AddOpenSpaceMarkers(GraphBuilder builder, Empire empire)
        {
            List<QuestMarkers.Marker> all = QuestMarkers.Of(empire);
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].Node.IsValid)
                {
                    // Synthetic: the same, for a marker whose place the galaxy no longer holds.
                    builder.AddItem(Nodes.Synthetic(MarkerRowId(all[i]), MarkerNode(all[i])));
                }
            }
        }

        /// <summary>Whether the map is drawing any marker out in the open - what decides whether the
        /// drifting region exists at all.</summary>
        private int OpenSpaceMarkers(Empire empire)
        {
            List<QuestMarkers.Marker> all = QuestMarkers.Of(empire);
            int loose = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].Node.IsValid)
                {
                    loose++;
                }
            }

            return loose;
        }

        /// <summary>What one marker's row says: the quest it belongs to, in the tracked or the
        /// ordinary form, and where it stands; the step's objective is reviewable rather than
        /// announced.</summary>
        private static NodeVtable MarkerNode(QuestMarkers.Marker marker)
        {
            QuestMarkers.Marker it = marker;
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => QuestMarkers.Name(it)),
                    GalaxyCoordinates.Part(() => it.At),
                },
                Sections = GraphNodes.Sections(NodeSection.Buffer(() => QuestMarkers.Objective(it))),
            };
        }

        /// <summary>A marker's node under the system it stands at - keyed by the pin, so two markers
        /// of two quests at one star stay apart and neither moves when the other goes.</summary>
        private static ControlId MarkerId(StarSystemNode node, QuestMarkers.Marker marker)
        {
            return ControlId.Structural(SystemKey(node) + "/marker/" + marker.Pin.GUID);
        }

        /// <summary>A marker's own top-level row, for one standing out in the open.</summary>
        private static ControlId MarkerRowId(QuestMarkers.Marker marker)
        {
            return ControlId.Structural("galaxy:marker/" + marker.Pin.GUID);
        }

        /// <summary>The system a marker stands at, where the map is naming one - which is what decides
        /// whether the marker is a child of a place or a row out in the open.</summary>
        private StarSystemNode MarkerSystem(QuestMarkers.Marker marker)
        {
            if (!marker.Node.IsValid)
            {
                return null;
            }

            for (int i = 0; i < _systems.Count; i++)
            {
                if (_systems[i].NodePosition == marker.Node)
                {
                    return _systems[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Who is holding this node: a citadel, or a fleet standing guard over it.
        ///
        /// The map says both by painting a ring round the node in the holder's colour and nothing else,
        /// so the phrases are the mod's - and the ring's own gate is this one
        /// (<c>GalaxyNode.UpdateGuardFeedback</c> :163-197 draws it only for a node in sight). A citadel
        /// is a guard too as far as the model is concerned, so the two are said as one thing or the
        /// other, never both.
        ///
        /// The guarding empire is the DISPLAYED one, for the reason <see cref="GroundBattle"/> records:
        /// a privateer's flag is the game's own concealment and the ring is painted in the flag's
        /// colour. A citadel has no such split - it belongs to the system that built it.
        ///
        /// This is the one thing about a held node the game writes nowhere else: the system's own
        /// dossier - the tooltip behind the star - has no guard or citadel line at all (its panel
        /// features are header, description, FIDSI, population, growth, defense, time bubbles, rooting,
        /// effects, failures, relics), and its defense figure quietly folds a citadel's stock into the
        /// system's own without naming it.
        /// </summary>
        private static IList<string> GuardLines(StarSystemNode node, Empire empire)
        {
            try
            {
                if (
                    node == null
                    || empire == null
                    || (int)node.Visibility[empire] < (int)EntityVisibility.Layer.Visible
                    || !node.IsGuarded
                )
                {
                    return null;
                }

                if (node.IsGuardedByCitadel)
                {
                    string held = Owner(node.CitadelEmpire);
                    return string.IsNullOrEmpty(held)
                        ? null
                        : new string[]
                        {
                            ModStrings.Format(ModStrings.GalaxySystemCitadel, held),
                        };
                }

                string guard = Owner(GuardingEmpire(node));
                return string.IsNullOrEmpty(guard)
                    ? null
                    : new string[]
                    {
                        ModStrings.Format(ModStrings.GalaxySystemGuarded, guard),
                    };
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's guard threw: " + e);
                return null;
            }
        }

        private static Empire GuardingEmpire(GameNode node)
        {
            int index = node.GuardingDisplayedEmpireIndex;
            Empire[] empires = Gui.Game == null ? null : Gui.Game.Empires;
            return empires == null || index < 0 || index >= empires.Length
                ? null
                : empires[index];
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
        private static List<Sendable> SendableTo(
            GameNode node,
            List<Fleet> fleets,
            List<FailureInfo> refusals
        )
        {
            List<Sendable> found = new List<Sendable>();
            for (int i = 0; i < fleets.Count; i++)
            {
                GameNode orbit = FleetOrders.Orbit(fleets[i]);
                if (orbit != null && orbit.GUID == node.GUID)
                {
                    continue;
                }

                AddSendable(
                    found,
                    fleets[i],
                    FleetOrders.PathTo(fleets[i], node, refusals),
                    refusals
                );
            }

            return found;
        }

        private static List<Sendable> SendableTo(
            Link link,
            List<Fleet> fleets,
            List<FailureInfo> refusals
        )
        {
            List<Sendable> found = new List<Sendable>();
            for (int i = 0; i < fleets.Count; i++)
            {
                AddSendable(
                    found,
                    fleets[i],
                    FleetOrders.PathToLink(fleets[i], link, refusals),
                    refusals
                );
            }

            return found;
        }

        private static void AddSendable(
            List<Sendable> found,
            Fleet fleet,
            GalaxyPath path,
            List<FailureInfo> refusals
        )
        {
            if (path != null && FleetOrders.CanSend(fleet, path, refusals))
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
        /// one, and how many while there are several.
        ///
        /// Where nothing could get there, the REASONS are said instead - the game's own sentences, in
        /// the game's own words, gathered by the same searches and the same action check the mouse's
        /// drag runs (<see cref="FleetOrders"/>). This key is pressed on a destination the tree has
        /// named, with fleets already selected, so it is never a speculative press: silence there told
        /// the player nothing at all, and "the star system is frozen in time" or "the fleet cannot move
        /// right now" is the whole answer. (This reverses the silence this comment used to record -
        /// owner decision 2026-08-14.) The one case still silent is the one with nothing to explain: no
        /// route was refused because every selected fleet is already parked at the destination, which
        /// <see cref="SendableTo"/> passes over without asking the pathfinder anything.
        /// </summary>
        private static void SendAll(List<Sendable> sendable, List<FailureInfo> refusals)
        {
            if (sendable.Count == 0)
            {
                SayRefusals(refusals);
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
        /// Why nothing could go, in the game's own words. Each reason is one of the game's failure
        /// flags, turned into the same sentence the map's own failure panel shows a mouse player
        /// (<c>Gui.FormatFailureInfo</c>, which is what carries the flag's parameter - the technology
        /// a route needs - into the sentence). Several selected fleets can be refused for the same
        /// reason, and the same fleet can be refused twice over on the way to a starlane, so identical
        /// sentences are said once; the rest are read out as a list, oldest reason first, which is the
        /// order the searches asked their questions in.
        ///
        /// An ignorable flag is skipped, exactly as the game's own formatter skips it: those are the
        /// bookkeeping flags a panel uses to grey a button, not things to tell anybody.
        /// </summary>
        private static void SayRefusals(List<FailureInfo> refusals)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                List<string> said = new List<string>();
                for (int i = 0; i < refusals.Count; i++)
                {
                    FailureInfo refusal = refusals[i];
                    if (refusal == null || refusal.IsIgnorable)
                    {
                        continue;
                    }

                    string text = AgeText.Clean(Gui.FormatFailureInfo(string.Empty, refusal));
                    if (string.IsNullOrEmpty(text) || said.Contains(text))
                    {
                        continue;
                    }

                    said.Add(text);
                    message.ListItem(text);
                }

                if (said.Count > 0)
                {
                    Voice.Say(message.Build(), true);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading why a fleet could not be sent threw: " + e);
            }
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
            StarSystemLabel label
        )
        {
            try
            {
                if (!PlanetsDeclared(node, empire))
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
                    // The planet's ONE node, so it carries the planet itself and rides along with it
                    // across a rebuild. There is no second copy to collide with any more: a lane leading
                    // here rebases onto this system rather than re-declaring its insides.
                    ControlId id = ControlId.For(planet, key);
                    if (card != null)
                    {
                        // The card carries a row of buttons the game draws under it, so where the game
                        // is drawing any the planet is a level of the tree rather than a leaf: it reads
                        // as itself, and what could be done to it is one step in. Enter on the card is
                        // the card's own click - the planet's page - and nothing else, because
                        // everything else the old menu held is now drawn where the game draws it.
                        List<CardActions.CardAction> actions = OrbitalActions(card);
                        List<TooltipChildren.Dossier> dossiers = PlanetDossiers(
                            system,
                            planet,
                            card,
                            looking
                        );
                        NodeVtable readout = OrbitalReadout(card, system, looking);
                        if (actions.Count == 0 && dossiers.Count == 0)
                        {
                            // Synthetic: an orbital is read out of the system's model; the card is only what the reading came from.
                            builder.AddItem(Nodes.Synthetic(id, readout));
                            continue;
                        }

                        readout.ControlType = ControlTypes.Group;
                            // Synthetic for the same reason as the leaf above.
                        builder.BeginGroup(Nodes.Synthetic(id, readout));
                        if (builder.IsExpanded(id))
                        {
                            object outerRegion = TooltipChildren.Actions(builder, key);
                            CardActions.Emit(builder, key, actions);
                            TooltipChildren.Emit(builder, key, dossiers, outerRegion);
                        }

                        builder.EndGroup();
                        continue;
                    }

                    // No card is drawn: the camera is not in on this system. The world is still the
                    // same world, so it reads the same things the card would say - what it is called,
                    // how big and what kind, what the game says about settling it, and how many
                    // curiosities are waiting in orbit - taken from the planet rather than from a
                    // widget that is not on the screen (owner ruling 2026-08-23). What could be DONE
                    // to it stays where the game draws it, so the only children here are the
                    // dossiers, which are the world's own and not the card's
                    // (<see cref="PlanetDossiers"/>).
                    //
                    // The circle is what the player would hover to get the planet's panel; without one
                    // the pointer goes to a carrier of the mod's, which is what makes the dossier
                    // readable with the camera anywhere (<see cref="PlanetCarrier"/>).
                    AgeTransform circle = Circle(table, i);
                    AgeTooltip onTheCircle = Raw(circle);
                    AgeTooltip dossier = AgeWidgets.Draws(onTheCircle)
                        ? onTheCircle
                        : PlanetCarrier(system, planet, i, looking);
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => PlanetName(system, planet, looking)),
                            GraphNodes.ValuePart(() => PlanetSizeAndType(system, planet, looking)),
                            GraphNodes.ValuePart(() => PlanetStatus(system, planet, looking)),
                            GraphNodes.ValuePart(() => CuriosityCount(planet, looking)),
                            GraphNodes.ValuePart(() => MiningProbes.Line(planet), false),
                        },
                        Sections = GraphNodes.Sections(
                            NodeSection.Buffer(() => PlanetLines(system, planet, looking)),
                            GraphNodes.TooltipSection(dossier)
                        ),
                    };
                    if (circle != null)
                    {
                        PointAt(vtable, circle);
                    }
                    else if (dossier != null)
                    {
                        AgeWidgets.PointAt(vtable, dossier.AgeTransform);
                    }

                    List<TooltipChildren.Dossier> pages = PlanetDossiers(
                        system,
                        planet,
                        null,
                        looking
                    );
                    if (pages.Count == 0)
                    {
                        // Synthetic: a page of a system's data sheet is a level the mod invented over the game's own panels.
                        builder.AddItem(Nodes.Synthetic(id, vtable));
                        continue;
                    }

                    vtable.ControlType = ControlTypes.Group;
                    // Synthetic for the same reason as the leaf above.
                    builder.BeginGroup(Nodes.Synthetic(id, vtable));
                    if (builder.IsExpanded(id))
                    {
                        TooltipChildren.Emit(
                            builder,
                            key,
                            pages,
                            TooltipChildren.Actions(builder, key)
                        );
                    }

                    builder.EndGroup();
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
        /// The dossiers a world carries beyond its own: one per anomaly found on it, one per deposit
        /// in its ground. The card writes NAMES for these and keeps everything they mean - what an
        /// anomaly does and what would reduce it, what a deposit is worth and why it cannot be
        /// exploited - in a panel only a hover reaches.
        ///
        /// NOT the five output figures the card also draws (Planet Food production and kin), though it
        /// hangs a dossier off every one of them: those pages explain what FIDSI IS, the same five
        /// paragraphs repeated on every world in the galaxy, and the star system's own management card
        /// already declares them where a player who wants them is looking
        /// (<c>SystemManagementScreen.PlanetDossiers</c>). Owner ruling 2026-08-24 - the strip stays
        /// undeclared HERE, and the coverage audit is told so rather than reporting it
        /// (<c>CoverageAudit</c>). The figures themselves are unaffected: they are drawn numbers and
        /// the row reads them as it always did.
        ///
        /// WHICH of them exist is the PLANET's question, not the card's: the map draws a card for one
        /// system at one camera step, and what is in a world's ground is not a thing it hides at any
        /// other. Whether the game is DRAWING an icon for one decides only WHERE the panel appears -
        /// at the game's own icon while it is on the screen, at a carrier of the mod's
        /// (<see cref="ScratchTooltips"/>) where it is not, bound exactly as the game's own item binds
        /// so the window assembles the same words either way (owner ruling 2026-08-23).
        ///
        /// The drawn-icon test is PAINTED, never Visible. These tables pool their items and retire the
        /// leftovers by FADING them, so a planet with no deposits at all keeps the previous planet's
        /// items answering the engine's can-draw test with the previous planet's deposits - measured
        /// on Osulo III, which has none and still offered Hyperium and Titanium. Membership from the
        /// model is the other half of that guard: the loop only ever asks about an item the game has
        /// just bound.
        ///
        /// Behind the survey gate, which is the card's own: an unrevealed node hides the deposit
        /// group, the anomaly table and both output strips wholesale
        /// (<c>PlanetLabel_SystemOrbital.RefreshAsUnrevealedNode</c>).
        /// </summary>
        private static List<TooltipChildren.Dossier> PlanetDossiers(
            StarSystemNode system,
            Planet planet,
            PlanetLabel_SystemOrbital card,
            Empire empire
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(8);
            try
            {
                if (planet == null || !Surveyed(system, empire))
                {
                    return found;
                }

                // In the order the card draws them, which is the order its own buffer reads
                // (<see cref="OrbitalDetails"/>): what was found on the world, then what is in its
                // ground.
                AddAnomalyDossiers(found, planet, card);
                AddDepositDossiers(found, planet, card, empire);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a planet's dossiers threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// One dossier per anomaly on the world - the paragraph, the effects and what reducing it
        /// would take, none of which the card writes anywhere.
        ///
        /// The item hangs its tooltip on its ICON rather than on itself
        /// (<c>PlanetAnomalyItem.Bind</c>), so the component's own field is what is read and what is
        /// aimed at: pointing at the row draws nothing at all.
        /// </summary>
        private static void AddAnomalyDossiers(
            List<TooltipChildren.Dossier> found,
            Planet planet,
            PlanetLabel_SystemOrbital card
        )
        {
            AgeTransform table = card == null ? null : card.PlanetAnomaliesTable;
            // Content: whether the card's own icons can carry these dossiers, or whether every one of
            // them falls back to a carrier of the mod's. The table pools its items and fades a retired
            // one, so PAINTED rather than the visibility flag.
            IList<AgeTransform> items = AgeWidgets.Painted(table) ? table.Children : null;
            for (int i = 0; i < planet.Anomalies.Count; i++)
            {
                Anomaly anomaly = planet.Anomalies[i];
                AgeTooltip drawn = DrawnAnomaly(items, i);
                AgeTooltip tooltip = drawn ?? AnomalyCarrier(planet, anomaly, i);
                TooltipChildren.Add(found, tooltip);
            }
        }

        /// <summary>The card's own icon for the Nth anomaly, where it is drawing one. The table is
        /// filled from the same list in the same order (<c>RefreshPlanetAnomalies</c>), so the Nth item
        /// is the Nth anomaly - and a retired one is dropped before it can answer for a planet that no
        /// longer has it.</summary>
        private static AgeTooltip DrawnAnomaly(IList<AgeTransform> items, int index)
        {
            if (items == null || index >= items.Count)
            {
                return null;
            }

            AgeTransform item = items[index];
            // Content: whether the Nth anomaly's dossier comes off the card's own icon. A retired item
            // is faded rather than hidden and still holds the previous world's anomaly.
            if (!AgeWidgets.Painted(item))
            {
                return null;
            }

            PlanetAnomalyItem component = item.GetComponent<PlanetAnomalyItem>();
            return component == null ? Raw(item) : component.Tooltip;
        }

        private static AgeTooltip AnomalyCarrier(Planet planet, Anomaly anomaly, int index)
        {
            try
            {
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "planet-anomaly/" + planet.GUID + "/" + index,
                    Hash(anomaly.AnomalyDefinition == null
                        ? null
                        : (string)anomaly.AnomalyDefinition.Name),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiAnomaly wrapper = new GuiAnomaly(anomaly.AnomalyDefinition, planet);
                    carrier.Class = wrapper.TooltipClass;
                    carrier.Content = string.Empty;
                    carrier.Context = planet;
                    carrier.Target = wrapper;
                }

                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: binding an anomaly dossier threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// One dossier per deposit in the world's ground: what size it is, what it does per
        /// population, and the game's own reason where the empire may not exploit it yet.
        ///
        /// The list is the card's own - the colony's OWN deposits where this empire has settled the
        /// world, the planet's raw ones otherwise (<c>RefreshResourceDeposits</c>) - so the nodes and
        /// the icons agree about how many there are, and the Nth icon is the Nth deposit.
        /// </summary>
        private static void AddDepositDossiers(
            List<TooltipChildren.Dossier> found,
            Planet planet,
            PlanetLabel_SystemOrbital card,
            Empire empire
        )
        {
            AgeTransform group = card == null ? null : card.ResourceDepositsGroup;
            // Content, as at the anomalies: whether the card's own icons can carry these dossiers at
            // all, PAINTED because the group pools its items and fades the surplus.
            IList<AgeTransform> items = AgeWidgets.Painted(group) ? group.Children : null;
            ColonizedPlanet colony = planet.ColonizedPlanet;
            bool ours = colony != null && colony.Empire == empire;
            int count = ours
                ? colony.ColonizedResourceDeposits.Count
                : planet.ResourceDeposits.Count;
            for (int i = 0; i < count; i++)
            {
                AgeTooltip drawn = DrawnDepositItem(items, i);
                AgeTooltip tooltip = drawn ?? DepositItemCarrier(planet, colony, ours, i, empire);
                TooltipChildren.Add(found, tooltip);
            }
        }

        private static AgeTooltip DrawnDepositItem(IList<AgeTransform> items, int index)
        {
            if (items == null || index >= items.Count)
            {
                return null;
            }

            AgeTransform item = items[index];
            // Content: whether the Nth deposit's dossier comes off the card's own icon, same pooling
            // and same reason as the anomalies above.
            if (!AgeWidgets.Painted(item))
            {
                return null;
            }

            ResourceDepositItem component = item.GetComponent<ResourceDepositItem>();
            return component == null ? Raw(item) : component.Tooltip;
        }

        /// <summary>A carrier bound exactly as <c>ResourceDepositItem.Refresh</c> binds the game's own
        /// icon - the same class, the same wrapper, the same refusal text - so the tooltip window
        /// assembles the same panel from a widget nobody can see.</summary>
        private static AgeTooltip DepositItemCarrier(
            Planet planet,
            ColonizedPlanet colony,
            bool ours,
            int index,
            Empire empire
        )
        {
            try
            {
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "planet-deposit/" + planet.GUID + "/" + index,
                    (DossierStamp(empire) * 3L) + (ours ? 1L : 0L),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiResourceDeposit wrapper = ours
                        ? new GuiResourceDeposit(colony.ColonizedResourceDeposits[index])
                        : new GuiResourceDeposit(planet.ResourceDeposits[index]);
                    List<FailureInfo> refusals = new List<FailureInfo>();
                    wrapper.CanBeExploited(PlayerEmpire(), refusals);
                    carrier.Class = wrapper.TooltipClass;
                    carrier.Content = Gui.FormatFailureInfos(refusals);
                    carrier.Context = null;
                    carrier.Target = wrapper;
                }

                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: binding a planet deposit dossier threw: " + e);
                return null;
            }
        }

        /// <summary>A stable number for a name, for a carrier stamp that has to change when the thing
        /// behind it does rather than once a turn.</summary>
        private static long Hash(string text)
        {
            long hash = 17L;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                hash = (hash * 31L) + text[i];
            }

            return hash;
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
        private static NodeSection StarDossier(
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            AgeTooltip either = StarAim(node, empire, label);
            if (either == null)
            {
                return null;
            }

            StarSystemNode it = node;
            Empire looking = empire;
            StarSystemLabel drawn = label;
            return GraphNodes.TooltipSection(
                either,
                () => StarDossierLines(it, looking, drawn)
            );
        }

        /// <summary>
        /// Which of a system's star dossiers the pointer is put on: the one the orbital window parks
        /// over the star once the camera is in and it says the whole card
        /// (<see cref="OrbitalStarDossier"/>), else the one on the label while the map is drawing the
        /// label, else a carrier of the mod's own bound the way the label binds its
        /// (<c>StarSystemLabel.BindLabelTooltip</c>).
        ///
        /// The third case is what makes a system OFF the screen still readable - the label is culled
        /// and its binding is stale, and reading a stale binding is how a system came to describe the
        /// last place its pooled label was pointed at.
        /// </summary>
        private static AgeTooltip StarAim(
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            AgeTooltip orbital = OrbitalStarDossier(node, empire);
            if (orbital != null)
            {
                return orbital;
            }

            AgeTooltip onTheLabel = label == null ? null : label.StarTooltip;
            if (
                onTheLabel != null
                && AgeWidgets.Painted(label.AgeTransform)
                && AgeWidgets.Draws(onTheLabel)
            )
            {
                return onTheLabel;
            }

            return StarCarrier(node, empire);
        }

        /// <summary>Whichever of a system's star tooltips the game is drawing. One at most can be up,
        /// so the first of them with anything to say is the one on the screen - and the mod's own
        /// carrier is asked last, because it is the one nothing else would have drawn. The orbital
        /// window's is skipped where it would say LESS than the label's
        /// (<see cref="OrbitalStarDossier"/>), which is the same rule <see cref="StarAim"/> aims by,
        /// so what is pointed at and what is read can never be two different cards.</summary>
        private static IList<string> StarDossierLines(
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            IList<string> words = TooltipWords(OrbitalStarDossier(node, empire));
            if (words != null && words.Count > 0)
            {
                return words;
            }

            words = TooltipWords(label == null ? null : label.StarTooltip);
            return words != null && words.Count > 0
                ? words
                : TooltipWords(StarCarrier(node, empire));
        }

        /// <summary>The system's own stat block on a carrier of the mod's, bound exactly as
        /// <c>StarSystemLabel.BindLabelTooltip</c> binds the label's: the same class, the same wrapper
        /// as both target AND context, the same content string.</summary>
        private static AgeTooltip StarCarrier(StarSystemNode node, Empire empire)
        {
            try
            {
                ColonizedStarSystem colony = LabelColony(node, empire);
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "star/" + node.GUID,
                    DossierStamp(colony == null ? null : colony.Empire),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiStarSystem gui = GuiStarSystem.Instantiate(node, colony);
                    carrier.Class = gui.TooltipClass;
                    carrier.Content = gui.TooltipContent;
                    carrier.Context = gui;
                    carrier.Target = gui;
                }

                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: binding a system's own dossier threw: " + e);
                return null;
            }
        }

        private static IList<string> TooltipWords(AgeTooltip tooltip)
        {
            Func<IList<string>> lines = AgeWidgets.TooltipLines(tooltip);
            return lines == null ? null : lines();
        }

        /// <summary>
        /// The orbital window's star tooltip where it says the whole card, and nothing where it says
        /// a reduced one - so that a system's dossier is chosen by what it CONTAINS rather than by
        /// which widget the game happens to be drawing.
        ///
        /// The window binds that tooltip with the PLAYER'S OWN colony
        /// (<c>PlanetLabelsWindow_SystemOrbital.OnBeginShow</c> asks the colony repository for
        /// <c>Gui.PlayerEmpire</c> alone), so on a system somebody ELSE has colonised it carries no
        /// colony at all - and the card the tooltip window then assembles from it drops the owner out
        /// of its header ("Osulo" rather than "Osulo - Niris") and leaves the system's defence off
        /// altogether. The map's LABEL binds the same card ownership-blind (<see cref="LabelColony"/>),
        /// so the fuller card exists the whole time; zooming in on a foreign system was simply
        /// swapping it for the thinner one.
        ///
        /// Nothing here keys on the camera. The window is left shown and bound for several steps of
        /// zooming back out (measured 2026-08-25), so a rule that trusted the zoom would still be
        /// reading the thin card at label distance.
        /// </summary>
        private static AgeTooltip OrbitalStarDossier(StarSystemNode node, Empire empire)
        {
            AgeTooltip orbital = OrbitalStarTooltip(node);
            if (orbital == null)
            {
                return null;
            }

            GuiStarSystem gui = orbital.Target as GuiStarSystem;
            bool colonyless = gui == null || gui.ColonizedStarSystem == null;
            return colonyless && LabelColony(node, empire) != null ? null : orbital;
        }

        /// <summary>
        /// The tooltip the orbital window draws on a system's star, which it keeps parked over the
        /// star wherever the star is on screen. Null unless the window is describing THIS system.
        ///
        /// Which system that is, is asked of the tooltip's own binding and never of where the camera
        /// is. The window binds this tooltip once, in <c>PlanetLabelsWindow_SystemOrbital.OnBeginShow</c>,
        /// to the system that was focused THEN - and the game leaves the window shown and bound to the
        /// system the player came from while <c>FocusedStarSystemNode</c> has already moved on
        /// (measured 2026-08-24: window bound to Rigel, focused system Dusay, and it stays that way).
        /// Trusting the camera's answer therefore aimed a system's dossier at a widget carrying its
        /// neighbour's, and the game drew the neighbour's card under the player's cursor for good.
        ///
        /// Declining here costs nothing: the caller falls through to the system's own map label and
        /// then to a carrier of the mod's own, both of which describe the system that was asked about.
        /// </summary>
        private static AgeTooltip OrbitalStarTooltip(StarSystemNode node)
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = OrbitalWindow();
                AgeTooltip star = window == null ? null : window.StarTooltip;
                if (star == null || star.AgeTransform == null || !Describes(star, node))
                {
                    return null;
                }

                return star;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether a star tooltip is bound to this system - the wrapper the game put on it
        /// names the system its words will be assembled about.</summary>
        private static bool Describes(AgeTooltip star, StarSystemNode node)
        {
            GuiStarSystem gui = star.Target as GuiStarSystem;
            return gui != null && ReferenceEquals(gui.StarSystemNode, node);
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
        /// And how many curiosities are waiting in orbit, which the card draws as a ring of icons
        /// beside it: they are buttons and so live one step in with the rest, but a player walking the
        /// map would have to open every planet to find out that any exist at all. The count is what a
        /// sighted player takes off the card at a glance, so the card's own line carries it.
        ///
        /// Enter is the card's own click: the planet's page. It is the only thing the card itself does
        /// - except while the game has the map waiting for a target, where the left click means
        /// "confirm here" wherever it lands and the card is no exception (<see cref="PlanetClick"/>).
        /// </summary>
        private static NodeVtable OrbitalReadout(
            PlanetLabel_SystemOrbital card,
            StarSystemNode system,
            Empire empire
        )
        {
            PlanetLabel_SystemOrbital it = card;
            StarSystemNode place = system;
            Empire looking = empire;
            AgeTooltip dossier = it.PlanetInfoTooltip;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.PlanetName)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlanetSizeAndType)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.ColonizeStatus)),
                    GraphNodes.ValuePart(() => OutpostTimer(it)),
                    GraphNodes.ValuePart(() => CuriosityCount(it.Planet, looking)),
                    // A mining probe is a thing somebody has DONE to this planet, and the game keeps
                    // it in the dossier where only a hover finds it. Said on the row so that a rival
                    // staking a world in your own system is heard while walking past it.
                    GraphNodes.ValuePart(() => MiningProbes.Line(it.Planet), false),
                },
                OnActivate = () => PlanetClick(it.Planet, system),
            };
            // What the card DRAWS first, then its dossier - the paragraph the game writes about a
            // world of this kind, its size, its type. The dossier is the long panel behind the card,
            // so the readout indicates it and the buffer is where it is read.
            vtable.Sections = GraphNodes.Sections(
                NodeSection.Buffer(() => OrbitalDetails(it, place, looking)),
                // The timer says a number and nothing else; the sentence the game explains it with is
                // reviewable rather than spoken, because the card already speaks the number and
                // hearing the paragraph again on every pass is what a buffer exists to avoid.
                NodeSection.Buffer(() => OutpostTimerHelp(it)),
                GraphNodes.TooltipSection(dossier)
            );
            PointAt(vtable, it.PlanetOrbitalCardContainer ?? it.AgeTransform);
            return vtable;
        }

        /// <summary>
        /// The card's own left click: the planet's page - unless the game has the map waiting for a
        /// target, in which case the click is the order's confirm and nothing else, here as on every
        /// other thing the map draws (<see cref="ZoomIn"/>, <see cref="LaneClick"/>).
        ///
        /// Confirmed at the SYSTEM the card is in orbit around, because a system is what the map
        /// builds a cursor target for - a planet is drawn inside one and has none of its own - and
        /// the mouse aiming at a card at this zoom is aiming inside that system.
        ///
        /// Asked first, and the answer is yes whenever any mode is armed
        /// (<see cref="CursorTargeting.ConfirmAt(GameNode)"/>), refusals included: without that the
        /// card's Enter opened the planet's page and threw the armed mode away with it, which is a
        /// thing no click of the mouse's can do.
        /// </summary>
        private static void PlanetClick(Planet planet, GameNode system)
        {
            if (CursorTargeting.ConfirmAt(system))
            {
                return;
            }

            GalaxyViewLevels.OpenPlanet(planet);
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
        private static IList<string> OrbitalDetails(
            PlanetLabel_SystemOrbital card,
            StarSystemNode system,
            Empire empire
        )
        {
            List<string> lines = new List<string>();
            try
            {
                // The three warning icons the card draws in a row beside the status line, each one a
                // picture with its sentence in its own tooltip and nothing written on the card.
                AddIconSentence(lines, card.HuntingGroundsIcon, "decay marker");
                AddIconSentence(lines, card.OutpostCancelIcon, "outpost warning");
                AddIconSentence(
                    lines,
                    card.HauntIcon == null ? null : card.HauntIcon.AgeTransform,
                    "ghost marker"
                );
                AddFidsi(lines, card);
                AddAnomalies(lines, card, system, empire);
                // The curiosities are NOT read here: each one is a button of the card's and is a child
                // node of its own (<see cref="AddCuriosities"/>). They were a line here only while the
                // line was silent - the items draw no words - and naming them off their wrappers would
                // have made the card say every curiosity twice.
                AddWidgetLines(lines, card.ResourceDepositsGroup);
                // Last, what the map says about this world that no widget on the card writes at all
                // (<see cref="AddSignals"/>). The ghost sentence can also arrive from the icon above,
                // and <see cref="AddLine"/> drops the second copy.
                AddSignals(lines, system, card.Planet, empire);
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
        /// One of the card's wordless warning pictures, as the sentence the game keeps in its tooltip:
        /// that the planet is DECAYING (a world colonized and lost, which colonizing the system again
        /// would restore - a different sentence per cause, and hidden outright for the Vodyani player
        /// whose own ark is the cause, <c>PlanetLabel_SystemOrbital</c> :353-381); that an OUTPOST here
        /// is shrinking, starving or already scheduled for decolonization (:498-533); and that the
        /// planet hosts somebody's GHOST colony (:462-480).
        ///
        /// PAINTED is the gate, and it has to be: every one of these carries its sentence from the
        /// PREFAB whether or not the card is showing it (measured: an untouched card answers
        /// "%OutpostBeingLostDescription" and "%PlanetIsDecayingDescription" while both icons are
        /// hidden), so anything reading the tooltip alone would tell every player that every healthy
        /// planet was dying.
        /// </summary>
        private static void AddIconSentence(List<string> lines, AgeTransform icon, string what)
        {
            try
            {
                if (!AgeWidgets.Painted(icon))
                {
                    return;
                }

                Func<IList<string>> sentence = AgeWidgets.TooltipLines(AgeWidgets.Raw(icon));
                IList<string> said = sentence == null ? null : sentence();
                for (int i = 0; said != null && i < said.Count; i++)
                {
                    AddLine(lines, said[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card's " + what + " threw: " + e);
            }
        }

        /// <summary>
        /// What the map is saying about a world through pure decoration - a coloured ring on the circle
        /// at systems zoom, with no tooltip on it anywhere (measured: every per-circle feedback image
        /// carries no <c>AgeTooltip</c> at all). A juggernaut terraforming or restoring it, an anomaly
        /// being reduced, a Sanctuary standing on it, a world there is only one of. A sighted player
        /// takes these off the colours; a keyboard player could reach none of them.
        ///
        /// One buffer line each, on the planet's own row and never in its announcement (owner ruling
        /// 2026-08-23), gated on the game's own state and read from the PLANET - so a world says the
        /// same things at every zoom, exactly as its anomalies and curiosities already do.
        ///
        /// Every word is the game's: the three juggernaut sentences it writes on the in-progress
        /// buttons one zoom step in (terraformation, restoration and anomaly reduction), its own
        /// "Remaining turns:" caption, its own Sanctuary sentences, and the title its discovery card
        /// gives a unique world (<c>%PlanetScreenUniquePlanetTitle</c>, read off the unshown prefab -
        /// "Unique Planet").
        ///
        /// A mining probe is already a row VALUE (<c>MiningProbes.Line</c>) and the curiosity ring
        /// already a counted one (<see cref="CuriosityCount"/>), so neither is repeated here.
        /// </summary>
        private static void AddSignals(
            List<string> lines,
            StarSystemNode system,
            Planet planet,
            Empire empire
        )
        {
            try
            {
                if (planet == null || !Surveyed(system, empire))
                {
                    return;
                }

                AddTerraformationSignal(lines, planet);
                AddAnomalyReductionSignal(lines, planet);
                AddGhostSignal(lines, planet, empire);
                if (planet.IsUnique)
                {
                    AddLine(lines, Localize("%PlanetScreenUniquePlanetTitle"));
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a planet's map signals threw: " + e);
            }
        }

        /// <summary>
        /// A juggernaut turning the world into something else, and how long is left.
        ///
        /// Terraformation and RESTORATION are one field of the planet's and two different sentences,
        /// told apart exactly as the game tells them apart - by the tags on the terraformation being
        /// carried out (<c>InitiateRestorationEmpireActionFleetActionDefinition.CheckConstructibleTags</c>:
        /// restoration is the one tagged <c>PlanetTerraformationFromDestroyed</c>, and anything tagged
        /// <c>PlanetTerraformationOnlyViaSystem</c> is neither, which is why the map draws no button
        /// for it). State above 2 is one the game has stopped drawing at all.
        /// </summary>
        private static void AddTerraformationSignal(List<string> lines, Planet planet)
        {
            TerraformPlanetEmpireLocalAction running = planet.TerraformationInProgress;
            if (running == null || (int)running.State > 2)
            {
                return;
            }

            PlanetTerraformationDefinition definition = running.PlanetTerraformationDefinition;
            if (
                definition == null
                || definition.Tags.Contains(
                    InitiateTerraformationEmpireActionFleetActionDefinition.InvalidTag
                )
            )
            {
                return;
            }

            string leader = LeaderName(running.Empire);
            bool restoring = definition.Tags.Contains(
                InitiateTerraformationEmpireActionFleetActionDefinition.DestroyedTag
            );
            string sentence = restoring
                // The game's own call passes two arguments to a template that asks for {0} and {2}, so
                // its own drawing leaves a slot unfilled; the third argument here is the same leader
                // name the slot is asking for, and an unfilled result is dropped by Localize.
                ? Localize(
                    "%PlanetRestoreWithJuggernautInProgressDescription",
                    planet.LocalizedName,
                    leader,
                    leader
                )
                : Localize(
                    "%PlanetTerraformWithJuggernautInProgressDescription",
                    planet.LocalizedName,
                    TerraformationTarget(definition),
                    leader
                );
            AddLine(lines, Remaining(sentence, running.GetRemainingTurns()));
        }

        /// <summary>What the world is being turned INTO, in the game's own alternative title for the
        /// terraformation - the one its own card puts in that slot.</summary>
        private static string TerraformationTarget(PlanetTerraformationDefinition definition)
        {
            try
            {
                IGuiConstructible wrapper =
                    Gui.GuiWrapperProviderService.InstantiateIGuiConstructible(definition);
                return wrapper == null ? null : AgeText.Clean(Gui.Localize(wrapper.AltTitle));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A juggernaut reducing one of the world's anomalies, in the game's own sentence for
        /// the state - the one it writes onto the in-progress button a rival is shown
        /// (<c>PlanetLabel_SystemOrbital.RefreshAnomalyReductionStatus</c> :975), with the arguments in
        /// that call's own order: the planet, then the empire doing it.</summary>
        private static void AddAnomalyReductionSignal(List<string> lines, Planet planet)
        {
            ReduceAnomalyEmpireLocalAction running = planet.AnomalyReductionInProgress;
            if (running == null || (int)running.State > 2)
            {
                return;
            }

            AddLine(
                lines,
                Remaining(
                    Localize(
                        "%PlanetReduceAnomalyWithJuggernautInProgressDescription",
                        planet.LocalizedName,
                        LeaderName(running.Empire)
                    ),
                    running.GetRemainingTurns()
                )
            );
        }

        /// <summary>A Sanctuary standing on the world - the Umbral Choir's ghost colony. The gate is the
        /// card's own: the ghost exists AND this empire can see the system it belongs to
        /// (<c>PlanetLabel_SystemOrbital.RefreshPlanetInformation</c>), so a hidden one stays
        /// hidden.</summary>
        private static void AddGhostSignal(List<string> lines, Planet planet, Empire empire)
        {
            ColonizedPlanet ghost = planet.GhostColonizedPlanet;
            if (
                ghost == null
                || ghost.ColonizedStarSystem == null
                || (int)ghost.ColonizedStarSystem.Visibility[empire] < 1
            )
            {
                return;
            }

            AddLine(
                lines,
                ghost.Empire == empire
                    ? Localize("%PlanetStatusGhostDescription")
                    : Localize("%PlanetStatusGhostByDescription", LeaderName(ghost.Empire))
            );
        }

        /// <summary>An empire as the game names it to this player - the same leader name its own
        /// in-progress sentences are built with.</summary>
        private static string LeaderName(Empire empire)
        {
            try
            {
                GuiEmpire wrapper =
                    empire == null
                        ? null
                        : Gui.GuiWrapperProviderService.GetGuiEmpire(empire);
                return wrapper == null ? null : AgeText.Clean(wrapper.GetLeaderName(PlayerEmpire()));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A state sentence with the turns left after it, in the game's own caption - the same
        /// pair its in-progress buttons write into their tooltips.</summary>
        private static string Remaining(string sentence, int turns)
        {
            if (string.IsNullOrEmpty(sentence))
            {
                return null;
            }

            string caption = Localize("%PanelFeatureRemainingTurnsTitle");
            return string.IsNullOrEmpty(caption)
                ? sentence
                : new MessageBuilder()
                    .ListItem(sentence)
                    .ListItem(caption + " " + turns)
                    .Build();
        }

        /// <summary>
        /// One of the game's own phrases, filled in and cleaned - and DROPPED where the fill left a
        /// template slot standing.
        ///
        /// A phrase still holding a "{0}" is one the game has not finished writing: the map has one
        /// (its restoration sentence asks for a third argument its own call never passes), and speaking
        /// a slot marker is worse than saying nothing.
        /// </summary>
        private static string Localize(string key, params object[] arguments)
        {
            try
            {
                string text = AgeText.Clean(
                    arguments == null || arguments.Length == 0
                        ? Gui.Localize(key)
                        : Gui.Localize(key, arguments)
                );
                return Unfilled(text) ? null : text;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether a filled-in phrase still carries a slot the game never filled - "{" and a
        /// digit, which is what an unpassed argument leaves behind.</summary>
        private static bool Unfilled(string text)
        {
            for (int i = 0; text != null && i + 1 < text.Length; i++)
            {
                if (text[i] == '{' && text[i + 1] >= '0' && text[i + 1] <= '9')
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>What has been found on the planet. The card draws each anomaly as a coloured icon
        /// with no words on it at all, so the names come from the game's own wrapper for the same
        /// anomaly - the one whose title it writes wherever it does have room. Only while the card is
        /// drawing the row: the planet knows its anomalies whether or not they are on screen.</summary>
        /// <summary>What a planet's row says with no card on the screen: what has been found on the
        /// world, and what the map is signalling about it in colour alone
        /// (<see cref="AddSignals"/>). ONE list, so the dedupe that keeps a line from being said twice
        /// works across both halves.</summary>
        private static IList<string> PlanetLines(
            StarSystemNode system,
            Planet planet,
            Empire empire
        )
        {
            List<string> lines = new List<string>(4);
            IList<string> anomalies = AnomalyLines(system, planet, empire);
            for (int i = 0; anomalies != null && i < anomalies.Count; i++)
            {
                AddLine(lines, anomalies[i]);
            }

            AddSignals(lines, system, planet, empire);
            return lines;
        }

        private static void AddAnomalies(
            List<string> lines,
            PlanetLabel_SystemOrbital card,
            StarSystemNode system,
            Empire empire
        )
        {
            IList<string> found = AnomalyLines(system, card.Planet, empire);
            for (int i = 0; found != null && i < found.Count; i++)
            {
                AddLine(lines, found[i]);
            }
        }

        /// <summary>
        /// What has been found on a world, from the planet rather than from the card.
        ///
        /// The card draws the row only when the camera is in on the system, so gating on the row left
        /// a planet's anomalies readable at one zoom and gone at another. The gate that has to stay is
        /// the FOG's: a system nobody has surveyed shows grey unknowns, and the card hides this row
        /// for exactly that reason (<c>PlanetLabel_SystemOrbital.RefreshAsUnrevealedNode</c>) - which
        /// is the same threshold the scanner asks (<see cref="Surveyed"/>).
        /// </summary>
        private static IList<string> AnomalyLines(
            StarSystemNode system,
            Planet planet,
            Empire empire
        )
        {
            try
            {
                if (planet == null || !Surveyed(system, empire))
                {
                    return null;
                }

                List<string> lines = new List<string>(planet.Anomalies.Count);
                for (int i = 0; i < planet.Anomalies.Count; i++)
                {
                    Anomaly anomaly = planet.Anomalies[i];
                    AddLine(
                        lines,
                        AgeText.Clean(new GuiAnomaly(anomaly.AnomalyDefinition, planet).Title)
                    );
                }

                return lines;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a planet's anomalies threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The five outputs the card draws for a world, named by the game's own property titles and
        /// read off the same simulation object the card reads them from.
        ///
        /// The card draws them in two shapes and both are read, each in its own: a colony's are
        /// WRITTEN as numbers, and on a world nobody has settled the game hides that row and draws a
        /// table of rating pips instead (<c>PlanetLabel_SystemOrbital.RefreshFIDSI</c>), which is
        /// what <see cref="AddFidsiRatings"/> reads. Both shapes are composed in
        /// <see cref="PlanetOutputs"/>, shared with the two other cards that draw them; what is this
        /// screen's is the gate and the simulation object - the numbers come off the COLONY, and the
        /// planet's own values behind them are all zero.
        /// </summary>
        private static void AddFidsi(List<string> lines, PlanetLabel_SystemOrbital card)
        {
            try
            {
                FidsiEnumerator fidsi = card.FidsiEnumerator;
                if (fidsi == null || fidsi.FidsiProperties == null)
                {
                    return;
                }

                ColonizedPlanet colony = card.ColonizedPlanet;
                if (colony == null)
                {
                    AddFidsiRatings(lines, card, fidsi);
                    return;
                }

                if (!Visible(fidsi.AgeTransform))
                {
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
                    AddLine(lines, numbers[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card's outputs threw: " + e);
            }
        }

        /// <summary>
        /// The same five outputs on a world nobody has settled, where the card rates them instead of
        /// writing them: a row of five cells per output, as many lit as the value earns
        /// (<c>PlanetLabel_SystemOrbital.RefreshScoreLine</c>). The management page's card rates them
        /// the same way, so the lines themselves are composed in <see cref="PlanetOutputs.Ratings"/>
        /// and only the gate is this screen's.
        ///
        /// A world the empire has not surveyed gets nothing: the game hides the whole table for one
        /// (<c>RefreshAsUnrevealedNode</c>), and its own test - <c>IsNodeRevealed</c> - is the gate,
        /// alongside the card's own "am I drawing this" flag that answers for the colony case too.
        /// </summary>
        private static void AddFidsiRatings(
            List<string> lines,
            PlanetLabel_SystemOrbital card,
            FidsiEnumerator fidsi
        )
        {
            Planet planet = card.Planet;
            if (planet == null || !card.IsNodeRevealed || !Visible(card.FidsiScoreTable))
            {
                return;
            }

            IList<string> ratings = PlanetOutputs.Ratings(
                planet,
                fidsi,
                card.FidsiParametersGuiElement
            );
            for (int i = 0; i < ratings.Count; i++)
            {
                AddLine(lines, ratings[i]);
            }
        }

        /// <summary>Which of the card's buttons the game is drawing, in drawn order. Empty for a card
        /// the game is offering nothing on, which is what keeps such a planet a leaf of the tree rather
        /// than a branch that opens onto nothing. The treatment each one gets is
        /// <see cref="CardActions"/>'s, shared with the management page's card.</summary>
        /// <summary>The game's own sentence for every one of the three in-progress buttons - the same
        /// one on all three because the game itself writes the same one on all three
        /// (<c>PlanetLabel_SystemOrbital</c> :818, :898, :970). It is the LAST resort for their names
        /// now (<see cref="InProgressName"/>) and stays in every one of their dossiers.</summary>
        private const string CancelJuggernautAction =
            "%PlanetCancelJuggernautActionButtonDescription";

        /// <summary>
        /// What one of the three in-progress buttons is called: WHAT IS BEING DONE, not the fact that
        /// pressing cancels it (owner ruling 2026-08-23).
        ///
        /// A planet being terraformed while one of its anomalies is reduced draws two of these buttons
        /// at once, and the game writes the one sentence
        /// (<see cref="CancelJuggernautAction"/>) onto both - so the card offered two entries the
        /// player could not tell apart. The game does name each action, on the wrapper its own tooltip
        /// is pointing at: the terraformation's and the anomaly reduction's constructible, the
        /// restoration's fleet action (<c>PlanetLabel_SystemOrbital</c> :806-830, :885-900, :960-975 -
        /// the player-empire branch, which is the only one the collector keeps, since a rival's button
        /// is drawn switched off).
        ///
        /// Asked at SPEAK time off the tooltip the button is carrying now: the game rebinds that
        /// tooltip every refresh, and a juggernaut that finishes one action and starts another keeps
        /// the same widget. A wrapper that cannot name itself falls back to the shared sentence, which
        /// is what the button said before this rule - never to silence.
        /// </summary>
        private static Func<string> InProgressName(AgeControlButton button)
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            return () =>
            {
                string title = AgeWidgets.TooltipTitle(Raw(widget));
                return string.IsNullOrEmpty(title) ? Localize(CancelJuggernautAction) : title;
            };
        }

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
                // The way into a minor civilization's diplomacy, drawn on a world one of them holds.
                // Its own tooltip cannot name it however real the sentence in it: the game gives that
                // one a renderer CLASS (MinorFaction, drawing the faction's panel from the tooltip's
                // Target), and a class-backed tooltip is not the readable kind, so the first line comes
                // back null and the button spoke unnamed. It opens the same screen the system label's
                // diplomacy button does, so it takes the same name.
                CardActions.AddNamedByMod(found, card.MinorFactionButton, ModStrings.GalaxySystemDiplomacy);

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

                // And the same row's OTHER half: the button the game swaps in for a start button while
                // that action is already running. It is the only way to CANCEL a juggernaut's work and
                // the only place the map says how long is left, and the mod declared none of the three.
                // Named by WHAT IS BEING DONE (<see cref="InProgressName"/>); the turns left and the
                // cancel sentence ride in the node's own dossier, and the row's buffer says what is
                // happening (<see cref="AddSignals"/>). A RIVAL's is drawn switched OFF, which is
                // exactly when the shared collector drops it - a button that cannot be pressed is not
                // an action, and the row's line has already said what it would have said.
                CardActions.AddNamed(
                    found,
                    card.InProgressTerraformationButton,
                    InProgressName(card.InProgressTerraformationButton)
                );
                CardActions.AddNamed(
                    found,
                    card.InProgressRestorationButton,
                    InProgressName(card.InProgressRestorationButton)
                );
                CardActions.AddNamed(
                    found,
                    card.InProgressAnomalyReductionButton,
                    InProgressName(card.InProgressAnomalyReductionButton)
                );

                // The way into pirate diplomacy, drawn on a world whose system holds a pirate lair
                // (DLC9). The game declares the field as a plain transform and hangs a radial button on
                // it, which is why a walk of the card's BUTTON fields never found it - and it keeps the
                // widget drawn while refusing a pirate-hating empire, with the reason written into the
                // same tooltip its name comes from, which is the refusable treatment.
                CardActions.AddRefusable(
                    found,
                    card.PirateLairGroup,
                    CardActions.NameFromTooltip(card.PirateLairGroup)
                );

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
        /// FADES them, leaving them visible at alpha 0 - so what is PAINTED is the gate, which is the
        /// same question the game's own <c>GetVisibleChildrenCount</c> asks of this very table when it
        /// lays the ring out.
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
                if (AgeWidgets.Painted(item))
                {
                    CardActions.AddRefusable(found, item, CardActions.TitleOf(item));
                }
            }
        }

        /// <summary>
        /// How many curiosities are still standing in orbit, said on the planet's own line so that
        /// finding one does not mean opening every planet on the map.
        ///
        /// Counted from the PLANET, not from the ring of icons: the ring is only drawn once the camera
        /// is in on the system, so a count taken off it told the player about a world at one zoom and
        /// nothing at another. The question the count asks is exactly the one the game asks when it
        /// fills the ring (<c>GuiPlanet.GetRemainingCuriosities</c>: every curiosity this empire's
        /// detection lets it SEE), so the number and the buttons agree - and where they briefly do
        /// not, it is because the pooled ring has not caught up with the planet yet.
        /// </summary>
        private static string CuriosityCount(Planet planet, Empire empire)
        {
            try
            {
                int count = 0;
                for (int i = 0; planet != null && i < planet.Curiosities.Count; i++)
                {
                    Curiosity curiosity = planet.Curiosities[i];
                    if (curiosity != null && curiosity.CanBeSeen(empire))
                    {
                        count++;
                    }
                }

                return count == 0
                    ? null
                    : ModStrings.Plural(
                        ModStrings.GalaxyPlanetCuriosityOne,
                        ModStrings.GalaxyPlanetCuriosities,
                        count
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How big a world is and what kind it is, in the game's own template - the second
        /// line the orbital card writes (<c>PlanetLabel_SystemOrbital.RefreshPlanetInformation</c>),
        /// said here for a planet whose card is not drawn. An unsurveyed system's planets keep the
        /// game's own "unknown" word for the type, the way the card does.</summary>
        private static string PlanetSizeAndType(
            StarSystemNode system,
            Planet planet,
            Empire empire
        )
        {
            try
            {
                string size = ElementTitle(planet.Size);
                string type = Surveyed(system, empire)
                    ? ElementTitle(planet.Type)
                    : Gui.Localize("%PlanetTypeUnknownTitle");
                return string.IsNullOrEmpty(size) || string.IsNullOrEmpty(type)
                    ? null
                    : AgeText.Clean(Gui.Localize("%PlaneSizeAndTypeFormat", size, type));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A gui element's title without the engine's "cannot find" warning: <c>Gui.GetTitle</c>
        /// logs one for a missing element and the game forwards its logs to telemetry, which is not a
        /// price a readout should pay for asking.</summary>
        private static string ElementTitle(StaticString name)
        {
            try
            {
                Amplitude.Unity.Gui.GuiElement element = Gui.GetGuiElement(name);
                return element == null || string.IsNullOrEmpty(element.Title)
                    ? null
                    : Gui.Localize(element.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The planet's own dossier on a carrier of the mod's, bound exactly as the orbital
        /// card binds <c>PlanetInfoTooltip</c>: the renderer's "Planet" panel over a
        /// <c>GuiPlanet</c> where the system has been surveyed, and the game's own plain "unknown"
        /// sentence where it has not.</summary>
        private static AgeTooltip PlanetCarrier(
            StarSystemNode system,
            Planet planet,
            int orbit,
            Empire empire
        )
        {
            try
            {
                bool surveyed = Surveyed(system, empire);
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "planet/" + system.GUID + "/" + orbit,
                    (DossierStamp(empire) * 31L) + (surveyed ? 1L : 0L),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    carrier.Context = null;
                    if (surveyed)
                    {
                        carrier.Class = "Planet";
                        carrier.Content = string.Empty;
                        carrier.Target = new GuiPlanet(planet);
                    }
                    else
                    {
                        carrier.Class = string.Empty;
                        carrier.Content = "%PlanetStatusUnknownDescription";
                        carrier.Target = null;
                    }
                }

                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: binding a planet's dossier threw: " + e);
                return null;
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
        internal static string PlanetName(StarSystemNode system, Planet planet, Empire empire)
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
        /// The circles switch from grey unknowns to real planets at the same threshold. Internal
        /// because the scanner asks it too: what is found ON a planet is offered exactly where the
        /// orbital card would draw it (<see cref="GalaxyScanner"/>).</summary>
        internal static bool Surveyed(StarSystemNode system, Empire empire)
        {
            return (int)system.Exploration[empire] >= (int)EntityExploration.State.Revealed;
        }

        /// <summary>Whether this page declares nodes for a system's planets at all - the threshold at
        /// which the map shows this empire that the system HAS planets, below the survey that says
        /// what they are (<see cref="AddPlanets"/>). Asked by the scanner before it offers anything
        /// standing on a planet: a find with no node to land on is a jump that can only refuse.
        /// </summary>
        internal static bool PlanetsDeclared(StarSystemNode node, Empire empire)
        {
            return (int)node.Exploration[empire] >= 2 && node.PlanetsVisibility[empire.Index];
        }

        /// <summary>A planet's own node in the tree, keyed exactly as <see cref="AddPlanets"/> keys it
        /// - by the orbit it is in, under its system, carrying the planet itself so the cursor rides
        /// along with it across a rebuild. The ancestry is in the key, so a landing here opens the
        /// constellation and the system on the way in.</summary>
        internal static ControlId PlanetId(StarSystemNode node, int orbit)
        {
            return orbit < 0 || orbit >= node.Planets.Count
                ? null
                : ControlId.For(node.Planets[orbit], SystemKey(node) + "/planet/" + orbit);
        }

        /// <summary>A starlane's own node, keyed exactly as <see cref="AddStarlanes"/> keys it - on
        /// the pair of GUIDs, under the system the lane is being read FROM, because one lane runs
        /// between two systems and each end is its own node.</summary>
        internal static ControlId LaneId(StarSystemNode node, Link link)
        {
            return ControlId.Structural(SystemKey(node) + "/lane/" + link.GUID);
        }

        /// <summary>
        /// The lanes the map draws out of a system, in the order - and therefore with the NUMBERS -
        /// that every part of this screen names them by.
        ///
        /// ONE list, because three things have to agree about it: the lane nodes themselves, the fleets
        /// under way that are hosted under this system and say which lane they are on, and the count
        /// phrase the system announces for them. Working the list out three times is how "starlane 2"
        /// comes to mean two different lines depending on which control said it.
        ///
        /// A LANE IS OFFERED exactly while the map draws the line. The map's rule is not "has it been
        /// revealed" but an intensity taken from the link's own exploration state
        /// (<c>GalaxyLink.Refresh</c> :247-252 feeds <c>GetIntensityFromState</c> :362-372), and that
        /// intensity is ZERO - an invisible line - for Localized and Identified as well as for
        /// Unrevealed. It only lights up at PartiallyRevealed. Offering a lane the map is drawing at
        /// intensity nought is offering a road that is not on the screen, which is why the threshold
        /// here is that one and not the reveal flag it used to be (owner ruling, after the drawn-parity
        /// measurement of the unexplored-lane preview).
        ///
        /// A wormhole is a different thing from a starlane and is said to be one. An empire without the
        /// technology to see them is shown none, exactly as the game's own neighbour search skips them;
        /// beyond that a wormhole is a <c>Link</c> like any other and passes the same intensity test,
        /// because <c>GalaxyWormhole</c> draws its line through the same <c>GalaxyLink.Refresh</c>.
        ///
        /// The game numbers no lane and the model's own order is whatever order the galaxy was
        /// generated in, so the lanes are walked - and numbered - going clockwise from north, and each
        /// one says the way it leaves. That is the mod's ordering, not the game's: a player who cannot
        /// see the lines needs the same "which one is that" the picture gives everyone else, and a
        /// number that moves between sessions would be worse than none.
        /// </summary>
        internal static List<Lane> LanesOf(StarSystemNode node, Empire empire)
        {
            List<Lane> lanes = new List<Lane>();
            try
            {
                for (int i = 0; i < node.Links.Count; i++)
                {
                    Link link = node.Links[i];
                    bool wormhole = link is WormholeLink;
                    if (wormhole && !empire.HasWormholeTechnology)
                    {
                        continue;
                    }

                    if (!MapVisibility.Drawn(link, empire))
                    {
                        continue;
                    }

                    GameNode far = ReferenceEquals(link.ExtremityNode1, node)
                        ? link.ExtremityNode2
                        : link.ExtremityNode1;
                    lanes.Add(
                        new Lane
                        {
                            Link = link,
                            Far = far,
                            Wormhole = wormhole,
                            Bearing = CompassDirections.Bearing(
                                far.GalaxyPosition.X - node.GalaxyPosition.X,
                                far.GalaxyPosition.Y - node.GalaxyPosition.Y
                            ),
                        }
                    );
                }

                lanes.Sort(ClockwiseFromNorth);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: working out a system's starlanes threw: " + e);
            }

            return lanes;
        }

        /// <summary>
        /// The lanes leaving a system, as LEAVES - and right on one goes down it.
        ///
        /// A lane says the name of the system at its far end only when the map draws that name.
        /// Everything else is a lane into the unexplored, which is what the map shows: a line running
        /// off into the dark. The galaxy model would answer either way - it holds every system's name
        /// from the first turn - so the check, not the model, is what keeps this honest. A lane into the
        /// dark is a leaf with nowhere to go and answers right with nothing, silently.
        ///
        /// A NAMED lane TRAVELS (<see cref="Travel"/>): the cursor is rebased onto the destination
        /// system's own node at the root of this stop. Not a child of its own naming that system, which
        /// is what this used to be: a second node for a place that already has one breaks the
        /// one-object-one-node rule, forced its whole contents to be re-keyed structurally, and had to be
        /// made deliberately poorer than the original - no lanes of its own - or the tree would have had
        /// no bottom. Rebasing keeps one node per place and puts no bottom on the tree at all.
        ///
        /// Everything else a lane does is unchanged: what is flying it, what sending the selection here
        /// would cost, the map's own left click, and the map's own right click.
        /// </summary>
        private void AddStarlanes(
            GraphBuilder builder,
            string place,
            StarSystemNode node,
            Empire empire,
            List<Lane> lanes
        )
        {
            try
            {
                HashSet<ControlId> expansion = builder.Expansion;
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
                            // A lane is a destination in its own right, so it previews like one.
                            GraphNodes.ValuePart(() => FleetRoute.Preview(lane), false),
                        },
                        Sections = GraphNodes.Sections(
                            NodeSection.Buffer(() => FleetPresence.LinesOn(lane)),
                            NodeSection.Buffer(() => FleetRoute.PreviewLines(lane))
                        ),
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
                    // a click that landed on a link and nothing else). With a fleet selected that is
                    // exactly what Enter does here.
                    //
                    // With NOTHING selected and no mode armed the mouse's click does nothing at all,
                    // and there the keyboard is given the one thing the line is good for: going where
                    // it leads, the same hop Right already makes (`OnFollow` below). A deliberate
                    // exception to Enter-is-click-parity, owner ruling 2026-08-20 - the click being a
                    // no-op is what makes it free to take, and a lane into the dark has nowhere to go
                    // so it keeps its silence.
                    //
                    // While a targeting mode is armed that same left click means "confirm here" instead,
                    // for a line as much as for a system (<see cref="CursorTargeting"/>) - and a lane is
                    // where the probe mode is aimed by rights, since a lane running into the dark is the
                    // only way to name a direction with no system at the end of it.
                    Link target = link;
                    GameNode aim = destination;
                    vtable.OnContextual = () => LaneCommand(target);
                    MoveHints(vtable);
                    // ...and the one thing the map's LEFT click on a line does with a selection up:
                    // let go of it. Only here and on empty space - Enter on a system zooms instead.
                    NodeHints.Add(
                        vtable,
                        ModStrings.HintDeselectFleet,
                        UiActions.Activate,
                        0,
                        FleetOrders.AnySelected
                    );

                    string key = place + "/lane/" + link.GUID;
                    // Keyed on the pair of GUIDs and NOT carrying the link as a reference: ONE lane runs
                    // between two systems, and once both ends are in the tree the same Link object backs
                    // two nodes - which are one control to the cursor, because reference identity is
                    // followed before the structural key. Measured with the fog lifted: focusing the
                    // Hir end of the Xiu-Hir lane threw the cursor across to the Xiu end on the next
                    // rebuild. Two GUIDs are stable without a reference, so nothing is lost.
                    ControlId id = ControlId.Structural(key);
                    // Right on a named lane goes THERE. Wired as a follow rather than as an expansion:
                    // an empty group auto-recollapses and speaks "no details" over the very landing this
                    // makes, and a group that really declared the far system would be that system's
                    // second node (<see cref="NodeVtable.OnFollow"/>). A lane running into the dark
                    // wires nothing and answers the key with silence, as a leaf does everywhere.
                    StarSystemNode far = named ? destination as StarSystemNode : null;
                    Action travel = null;
                    if (far != null)
                    {
                        StarSystemNode from = node;
                        StarSystemNode to = far;
                        ControlId here = id;
                        GalaxyHudScreen screen = this;
                        HashSet<ControlId> expanded = expansion;
                        travel = () => screen.Travel(from, here, to, expanded);
                        vtable.OnFollow = travel;
                    }

                    // Wired after the hop, because Enter falls through to the very same hop when the
                    // click it stands for would have done nothing (<see cref="LaneClick"/>): one
                    // derivation of where this lane leads, used by both keys.
                    vtable.OnActivate = () => LaneClick(target, aim, travel);
                    // Synthetic: a lane is a link in the galaxy model - the map draws it as a line
                    // between two places, never as a control.
                    builder.AddItem(Nodes.Synthetic(id, vtable));
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's starlanes threw: " + e);
            }
        }

        /// <summary>One lane leaving a system, with the way it leaves already worked out.</summary>
        internal struct Lane
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
        /// The sixteen bearings a probe can be launched on, offered at the system the fleet holding it is
        /// standing at, and only while the map is waiting for that launch.
        ///
        /// The lanes above are where a probe is usually aimed, and they are not all of it: the order
        /// takes ANY direction that is not zero (<c>LaunchProbeFleetActionDefinition.CheckContext</c>
        /// :92-95 refuses the zero vector alone), and the game's own tutorial for this action tells the
        /// player to send the probe away from the known lanes to find a new constellation. A mouse aims
        /// at empty sky by clicking it; the keyboard had only the map's own nodes, so every direction
        /// with nothing along it was unreachable. SIXTEEN of them rather than the eight the mod
        /// describes lanes with: a lane's compass word only has to name where a line that already exists
        /// runs, while these are the whole of the map a probe can be sent into, and the arcs eight words
        /// leave between them are directions no player can ask for
        /// (<see cref="CompassDirections.KeyForBearing16"/>).
        ///
        /// Each one says what is DOWN it - the stretches of fog a probe flown that way would cross and
        /// how far the map goes before it ends (<see cref="ProbeContext"/>) - because the order cannot be
        /// recalled, cannot be aimed at anything but a direction, and is therefore chosen entirely on
        /// what each direction is worth. A sighted player reads that off the fog in a second; a bare
        /// compass word said sixteen times gives a listener nothing to choose between. The group itself
        /// is named by how far the probe will get, which is the fact all sixteen are measured against.
        ///
        /// They sit here, LAST in the system's branch, after everything the map really draws at this
        /// place: they are sixteen ways OUT of it rather than anything in it, and they are gone again the
        /// moment the mode ends. The player does not have to walk to them: arming
        /// the mode seats the cursor on the first bearing itself, opening this group and the system's
        /// branch to do it (<see cref="FollowProbeArming"/>), because the fleet panel the mode is armed
        /// from is taken off the screen by the arming and the player would otherwise be left standing
        /// wherever they happened to be. They exist only while the mode does, which is the same rule
        /// the map's own "click a target" banner is drawn by.
        /// </summary>
        private static void AddProbeDirections(
            GraphBuilder builder,
            string place,
            StarSystemNode node
        )
        {
            try
            {
                ProbeLaunchingCursor cursor = CursorTargeting.ArmedProbe;
                Fleet fleet = cursor == null ? null : cursor.ProbeOriginFleet;
                if (
                    fleet == null
                    || fleet.IsDestroyed
                    || !fleet.Position.IsInOrbit
                    || fleet.NodePosition != node.NodePosition
                )
                {
                    return;
                }

                ControlId id = ControlId.Structural(place + "/launch");
                // Synthetic: mod-authored - the probe launcher is the mod's own way of aiming a launch the map only offers to a mouse.
                builder.BeginGroup(Nodes.Synthetic(
                    id,
                    GraphNodes.Group(() => ProbeContext.GroupLabel(fleet, node))
                ));
                if (builder.IsExpanded(id))
                {
                    for (int i = 0; i < ProbeContext.Bearings; i++)
                    {
                        int index = i;
                        double bearing = ProbeContext.Bearing(i);
                        // Synthetic: mod-authored, one bearing per direction the launch can take.
                        builder.AddItem(Nodes.Synthetic(
                            ControlId.Structural(place + "/launch/" + i),
                            GraphNodes.Button(
                                () => ProbeContext.Line(fleet, node, index),
                                () => CursorTargeting.ConfirmTowards(bearing)
                            )
                        ));
                    }
                }

                builder.EndGroup();
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: offering the probe's launch directions threw: " + e);
            }
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

            List<FailureInfo> refusals = new List<FailureInfo>();
            SendAll(SendableTo(link, selected, refusals), refusals);
        }

        /// <summary>
        /// The map's own left click on a lane, and then the one thing that click never had a use for.
        ///
        /// Asked in the same first-refusal order the click on a system is asked in
        /// (<see cref="ZoomIn"/>), so the two cannot drift apart: while an order is waiting for a
        /// target the click is aimed down this line (<see cref="CursorTargeting"/>), and while the
        /// cursor is holding fleets it is the click that lets go of them (<see cref="Deselect"/>).
        ///
        /// With neither of those true the mouse's click on a lane does nothing whatsoever, and that
        /// nothing is what this last branch spends: Enter goes where the lane leads, which is the hop
        /// the descend key already makes (<paramref name="travel"/> IS the node's own
        /// <c>OnFollow</c>, so there is one derivation of the far end and not two). A deliberate
        /// exception to the otherwise absolute rule that Enter is the game's left click and invents
        /// nothing - owner ruling 2026-08-20, on the grounds that a gesture the game answers with
        /// nothing at all costs the player nothing to give away. A lane running into the dark leads
        /// nowhere the map has named, wires no hop, and so keeps the silence it had.
        /// </summary>
        private static void LaneClick(Link lane, GameNode far, Action travel)
        {
            if (CursorTargeting.ConfirmAt(lane, far))
            {
                return;
            }

            if (Deselect() || travel == null)
            {
                return;
            }

            travel();
        }

        /// <summary>
        /// Let go of whatever the map's cursor is holding, exactly as a click on empty space or on a
        /// starlane does (<c>GalaxyGarrisonCursor.OnCursorClick</c>): the garrison cursor is swapped
        /// back for the plain one, which is also what takes the fleet panel off the screen.
        ///
        /// Answers whether anything was actually let go, which is how the lane's Enter knows the
        /// click it stands for would have done nothing (<see cref="LaneClick"/>). The garrison cursor
        /// being up IS that question: it is the cursor the map wears while it is holding a selection,
        /// and the same thing the selected-fleet panel's own visibility is gated on.
        ///
        /// Nothing is said here. The panel going is what the player is being told about, and the
        /// panel's own watcher says it - one announcement, from the one place that knows.
        /// </summary>
        private static bool Deselect()
        {
            try
            {
                Amplitude.Unity.View.ICursorService cursors =
                    Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
                if (cursors == null || !(Gui.GetCursor() is GalaxyGarrisonCursor))
                {
                    return false;
                }

                cursors.ChangeCursor(typeof(GalaxyCursor), Gui.GetCursor());
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: letting go of the selection threw: " + e);
                return false;
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

        /// <summary>Every label the map is currently drawing for a system - never cached ACROSS frames,
        /// because the window grows this list as the player explores more of the galaxy and a cache
        /// keyed on nothing that changes would go stale exactly when a newly-discovered system needed
        /// its tooltip.
        ///
        /// Held for the length of ONE frame, though: the walk is a component search over every label in
        /// the galaxy and it now has several callers in a frame - the build, the focused row's aim, the
        /// focused dossier's name - which the map would otherwise pay for one at a time. Keyed on the
        /// frame number rather than invalidated by anything, so nothing has to remember to clear it.
        /// </summary>
        private static StarSystemLabel[] SystemLabels()
        {
            try
            {
                int frame = UnityEngine.Time.frameCount;
                if (_labelsFrame == frame && _labels != null)
                {
                    return _labels;
                }

                StarSystemLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<StarSystemLabelsWindow>(false)
                    : null;
                _labels =
                    window == null
                        ? NoLabels
                        : window.GetComponentsInChildren<StarSystemLabel>(true);
                _labelsFrame = frame;
                return _labels;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the system labels threw: " + e);
                return NoLabels;
            }
        }

        private static StarSystemLabel[] _labels;

        private static int _labelsFrame = -1;

        // ---- fleets ----


        /// <summary>
        /// The fleets PARKED at a system - what each is made of, where it is, and where it is going -
        /// as children of that system.
        ///
        /// Which fleets those are is never worked out here: they are the ones the map's own lozenge at
        /// this system is holding (<see cref="FleetPresence"/>), so a fleet nobody can see is absent for
        /// the same reason it is absent from the picture, and the count the place announces and the
        /// children it opens onto are the same answer read two ways. The fleets under way on this
        /// system's lanes are the other half of that count and hang here too, after these
        /// (<see cref="AddEnRoute"/>).
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
                    List<TooltipChildren.Dossier> badges;
                    NodeVtable vtable = FleetNode(it, docks, flying, out badges);
                    AddFleet(builder, place + "/fleet/" + it.GUID, vtable, badges);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets at a place threw: " + e);
            }
        }

        /// <summary>
        /// The fleets UNDER WAY on the lanes leaving a system, as children of that system - each saying
        /// which lane it is on and which way that lane leaves.
        ///
        /// A lane runs between two systems and the fleet flying it is at neither, so the map draws it
        /// out in between and the tree hangs it under BOTH ends. Either end is a true answer to "where
        /// is it", and hosting it under one alone would make the answer depend on which of the two the
        /// player happened to open - the same reasoning the fleet search index has always been built on.
        /// Under the lane node itself is what this used to be, and a lane is a leaf now: travelling one
        /// is what right means there.
        ///
        /// Keyed under the SYSTEM, with no reference carried, exactly as a parked fleet is: the two
        /// hosts' keys differ by the system in them, so the two nodes are distinct controls, and a fleet
        /// is parked or under way and never both, so neither key can collide with the other set.
        /// </summary>
        private static void AddEnRoute(GraphBuilder builder, string place, List<EnRoute> flying)
        {
            if (flying.Count == 0)
            {
                return;
            }

            try
            {
                DockLabel[] docks = DockLabels();
                FleetLabel[] labels = FleetLabels();

                for (int i = 0; i < flying.Count; i++)
                {
                    EnRoute leg = flying[i];
                    Fleet it = leg.Fleet;
                    List<TooltipChildren.Dossier> badges;
                    NodeVtable vtable = FleetNode(it, docks, labels, out badges);
                    // Straight after the name, because it answers the question the player is holding
                    // while they hear it: why is this fleet under THIS system? One whole phrase rather
                    // than a lane number glued to a compass word, and the number is the one the lane
                    // node itself announces - both come off the same list (<see cref="LanesOf"/>).
                    string template = leg.Wormhole
                        ? ModStrings.GalaxyFleetOnWormhole
                        : ModStrings.GalaxyFleetOnStarlane;
                    int number = leg.Number;
                    string direction = leg.Direction;
                    vtable.Announcements.Insert(
                        1,
                        GraphNodes.ValuePart(
                            () =>
                                ModStrings.Format(template, number, ModStrings.Get(direction)),
                            false
                        )
                    );
                    AddFleet(builder, place + "/fleet/" + it.GUID, vtable, badges);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets under way near a system threw: " + e);
            }
        }

        /// <summary>
        /// The fleets crossing the OPEN SPACE towards a system - the ones flying a leg between two
        /// nodes with no lane between them - as children of the system they are flying TO.
        ///
        /// A free-moving fleet is the one thing the map draws that the rest of this tree had no place
        /// for: it is in no docking slot and on no lane, so neither the parked list nor the lane list
        /// holds it, and it used to be reachable only by the inspect cursor and the scanner - findable
        /// by neither Tab nor the search. The rule for that is the same one every other homeless thing
        /// on this map is given: where the model gives it no place, it gets a row of its own.
        ///
        /// Under the DESTINATION only, and this is where a free mover parts company with a fleet on a
        /// lane. A lane is drawn map geometry: both of its ends are on the screen, either one is a way
        /// of finding a fleet flying it, and a sighted player reaches it from both. A crossing of open
        /// space is not drawn at all. What the picture shows is a fleet standing between the stars and,
        /// when it is selected, the path AHEAD of it - dots and numbered turn markers running to where
        /// it is going. Nothing anywhere draws or writes where a fleet set out FROM, not even for the
        /// player's own fleets (<see cref="UI.FleetRoute"/>; the game's own path starts at the node
        /// being flown towards). So a row under the source system would be the mod telling the player
        /// something the game does not tell anybody, and it was taken out on 2026-08-16.
        /// </summary>
        private static void AddFreeMoving(
            GraphBuilder builder,
            string place,
            StarSystemNode node,
            List<Fleet> arriving
        )
        {
            if (arriving.Count == 0)
            {
                return;
            }

            try
            {
                DockLabel[] docks = DockLabels();
                FleetLabel[] labels = FleetLabels();
                for (int i = 0; i < arriving.Count; i++)
                {
                    Fleet it = arriving[i];
                    List<TooltipChildren.Dossier> badges;
                    NodeVtable vtable = FleetNode(it, docks, labels, out badges);
                    // Straight after the name, in the slot the lane phrase takes on a fleet under way,
                    // and for the same reason: it answers the question the player is holding while they
                    // hear it - why is this fleet under THIS system?
                    vtable.Announcements.Insert(
                        1,
                        GraphNodes.ValuePart(
                            () =>
                                ModStrings.Format(
                                    ModStrings.GalaxyFleetFreeMovingTo,
                                    node.LocalizedName
                                ),
                            false
                        )
                    );
                    AddFleet(builder, place + "/fleet/" + it.GUID, vtable, badges);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets crossing open space near a system threw: " + e);
            }
        }

        /// <summary>
        /// A fleet crossing open space towards somewhere the map has not named, as a row of its own at
        /// the top of the tree.
        ///
        /// It has no system to hang under - its destination is not a place this tree declares - and the
        /// rule for a homeless thing on this map is the one every other one is given: a row of its own,
        /// walked into the list by its own position (<see cref="BuildSystems"/>). What it says about
        /// where it is going is the one thing that can be said, which is that the player cannot see
        /// where that is.
        /// </summary>
        private static void AddAdrift(GraphBuilder builder, Fleet it)
        {
            try
            {
                List<TooltipChildren.Dossier> badges;
                NodeVtable vtable = FleetNode(it, DockLabels(), FleetLabels(), out badges);
                // Which kind of journey it is, since the unnamed destination is all either can say
                // about where it is going: a lane running into the dark is not the same picture as a
                // fleet striking out across open space, and the map draws the one and not the other.
                string phrase = Crossing(it)
                    ? ModStrings.GalaxyFleetFreeMovingToUnexplored
                    : ModStrings.GalaxyFleetOnLaneToUnexplored;
                vtable.Announcements.Insert(
                    1,
                    GraphNodes.ValuePart(() => ModStrings.Get(phrase), false)
                );
                AddFleet(builder, AdriftKey(it), vtable, badges);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a fleet crossing open space threw: " + e);
            }
        }

        private static ControlId AdriftId(Fleet fleet)
        {
            return ControlId.Structural(AdriftKey(fleet));
        }

        /// <summary>The key that id is built from, which the declaration needs as well: a fleet with
        /// badges on its lozenge is a group, and its children are keyed under it.</summary>
        private static string AdriftKey(Fleet fleet)
        {
            return "galaxy:fleet/" + fleet.GUID;
        }

        /// <summary>Whether this fleet's current leg is a crossing of OPEN SPACE rather than a flight
        /// down a lane - the same question <see cref="FreeMovingAt"/> asks, asked again for the one row
        /// that has no system to have asked it under.</summary>
        private static bool Crossing(Fleet fleet)
        {
            try
            {
                GameNode goal;
                return CrossingOpenSpace(
                    Amplitude.Unity.Framework.Services.GetService<IPositioningService>(),
                    fleet,
                    out goal
                );
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// The fleets the map draws crossing open space towards this system.
        ///
        /// The fleets are the ones the map is DRAWING (<see cref="FleetPresence.Drawing"/>) - the fleet
        /// label window's own repository and its own vision gate - so a fleet nobody can see is absent
        /// here for the same reason it is absent from the picture, and no vision rule is re-derived.
        ///
        /// What makes a leg free movement is that its two ends have no <c>Link</c> between them: the
        /// game stores a leg as a pair of node positions rather than as a lane
        /// (<c>FleetPosition.SetMovement</c>), and a fleet flying with starlanes ignored gets a pair
        /// that no line joins. That test is also what keeps this list and <see cref="EnRouteOn"/> from
        /// both claiming the same fleet, which under one system would be a duplicate control id and
        /// would throw the whole page out of Build: a leg either has a lane or it does not.
        /// </summary>
        private static List<Fleet> FreeMovingAt(StarSystemNode node)
        {
            List<Fleet> arriving = new List<Fleet>();
            try
            {
                IPositioningService positioning =
                    Amplitude.Unity.Framework.Services.GetService<IPositioningService>();
                IList<Fleet> drawn = FleetPresence.Drawing();
                for (int i = 0; i < drawn.Count; i++)
                {
                    GameNode goal;
                    if (CrossingOpenSpace(positioning, drawn[i], out goal)
                        && ReferenceEquals(goal, node))
                    {
                        arriving.Add(drawn[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: working out what is crossing open space near a system threw: " + e);
            }

            return arriving;
        }

        /// <summary>
        /// The fleets UNDER WAY with no branch to hang under: the ones whose destination is not one of
        /// the systems this stop is declaring, because the map has never named it.
        ///
        /// Both kinds of journey, since both hang under their destination: a crossing of open space,
        /// and - since the destination-only rule was extended to them - a fleet on a starlane running
        /// into the dark. Asked of the whole drawn fleet list once per build rather than per system,
        /// and answered against the very list the rows are made from, so a fleet under way is either
        /// its destination's child or it is here, never both and never neither.
        /// </summary>
        private static void FreeMovingAdrift(List<StarSystemNode> declared, List<Fleet> adrift)
        {
            adrift.Clear();
            try
            {
                IPositioningService positioning =
                    Amplitude.Unity.Framework.Services.GetService<IPositioningService>();
                IList<Fleet> drawn = FleetPresence.Drawing();
                for (int i = 0; i < drawn.Count; i++)
                {
                    GameNode goal = GoalOf(positioning, drawn[i]);
                    if (goal != null && !Declares(declared, goal))
                    {
                        adrift.Add(drawn[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: working out what is crossing open space threw: " + e);
            }
        }

        private static bool Declares(List<StarSystemNode> declared, GameNode node)
        {
            for (int i = 0; i < declared.Count; i++)
            {
                if (ReferenceEquals(declared[i], node))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the leg a fleet is flying is a crossing of open space - it is under way and
        /// the two ends of its leg have no line between them - and if so, where it is flying to.
        /// </summary>
        private static bool CrossingOpenSpace(
            IPositioningService positioning,
            Fleet fleet,
            out GameNode goal
        )
        {
            goal = null;
            FleetPosition position = fleet.Position;
            if (positioning == null || position.IsInOrbit || !position.IsInMovement)
            {
                return false;
            }

            GameNode start = positioning.GetGameNode(position.Movement.Start);
            goal = positioning.GetGameNode(position.Movement.Goal);
            return start != null && goal != null && !Linked(start, goal);
        }

        /// <summary>Whether a line of any kind joins these two nodes - the model's own question, asked of
        /// every link the node holds rather than of the lanes the map draws, because a leg with an
        /// undrawn lane under it is still not free movement.</summary>
        private static bool Linked(GameNode node, GameNode far)
        {
            for (int i = 0; i < node.Links.Count; i++)
            {
                Link link = node.Links[i];
                if (
                    ReferenceEquals(link.ExtremityNode1, far)
                    || ReferenceEquals(link.ExtremityNode2, far)
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>One fleet as a control, wherever it is being hung - parked, on a lane, crossing open
        /// space, or adrift at the top level. Shared so that a fleet reads the same way in all four
        /// places: everything but the lane it is on is the same fleet, and that includes whether it is a
        /// control at all.
        ///
        /// A fleet the map will not let the mouse SELECT (<see cref="FleetPresence.Selectable"/> - an
        /// automated delivery fleet) is declared as TEXT rather than as a button: no role word, no
        /// action. Not an unavailable button, which would be the mod saying the game has switched
        /// something off; there is no control here at all, exactly as there is none under the pointer.
        /// Owner ruling 2026-08-16.
        ///
        /// <paramref name="badges"/> answers what the map drew ON the lozenge beside the fleet - see
        /// <see cref="FleetBadges"/> - for the caller to declare with <see cref="AddFleet"/>.</summary>
        private static NodeVtable FleetNode(
            Fleet it,
            DockLabel[] docks,
            FleetLabel[] flying,
            out List<TooltipChildren.Dossier> badges
        )
        {
            AgeTransform lozenge = FleetLozenge(it, docks, flying);
            badges = FleetBadges(lozenge);
            NodeVtable vtable = GraphNodes.Button(
                () => it.LocalizedName,
                () => Select(it),
                null,
                Raw(lozenge),
                // The rest of the journey, turn by turn, for whoever wants to know where this
                // fleet will be sleeping tonight (<see cref="FleetRoute"/>).
                () => FleetRoute.CommittedLines(it)
            );
            if (!FleetPresence.Selectable(it))
            {
                vtable.ControlType = ControlTypes.Text;
                vtable.OnActivate = null;
            }

            // Where it is on the map. Asked live rather than captured, because a fleet moves - and a
            // fleet in orbit reads the exact pair its system reads, which is the map saying the same
            // thing twice on purpose (<see cref="GalaxyCoordinates"/>).
            vtable.Announcements.Add(GalaxyCoordinates.Part(() => it.GalaxyPosition));
            // Whose it is, who is commanding it and what it is made of - the one phrase every
            // surface that names a single fleet says (<see cref="FleetPhrase"/>), so this row and a
            // scanner result and a line in the turn log describe the same fleet the same way. Its
            // ship walk is memoised on the fleet's own membership, because an announcement part is
            // asked on every frame whether anything is watching it or not.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetPhrase.Describe(it)));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetText(it)));
            // How much of the journey is left. A part of its OWN and not part of the line
            // above, because that line is WATCHED - a movement figure the game changes under
            // the player is worth saying - and the answer here is a walk of the fleet's whole
            // route, which is a thing to do when focus lands and never on a frame.
            //
            // The destination is named only where the line above has not already named it: a
            // fleet under way says "Moving to Xiu" and then how long that will take, and a
            // fleet parked with an order standing says where it is and then where it is going.
            vtable.Announcements.Add(
                GraphNodes.ValuePart(
                    () =>
                        FleetOrders.Orbit(it) == null
                            ? FleetRoute.Arrival(it)
                            : FleetRoute.Committed(it),
                    false
                )
            );
            if (lozenge != null)
            {
                PointAt(vtable, lozenge);
            }

            // Keyed by the caller on the fleet's own GUID but NOT carrying the fleet as a reference:
            // the selected-fleet panel is declared on this same screen, and its fleet line is keyed on
            // the garrison - which for a fleet is this very object. Two nodes sharing a backing object
            // are ONE control to the cursor (reference identity is followed before the structural key),
            // so the panel's line teleported the player straight back out to the map on the next
            // rebuild. The line is the one that needs the reference - its widget is a pool slot the game
            // rebinds - and a GUID key is stable without one. It is also what lets a fleet under way be
            // hosted under both ends of its lane at once.
            return vtable;
        }

        /// <summary>
        /// The badges the map draws on a fleet's lozenge: one of these ships can explore, one can found
        /// a colony, one carries a destruction module (<c>GarrisonsLabelButton.RefreshRoleIcons</c>
        /// :156-191 shows each where the garrison holds such a ship). Each is a wordless picture with a
        /// sentence of the game's own saying what it means, and the lozenge itself carries the fleet's
        /// dossier - so the fleet's node could point at only one of the four and carried none of the
        /// three.
        ///
        /// Nodes rather than lines in the fleet's buffer: three sentences merged into one paragraph is
        /// what a player cannot tell apart or step through (owner ruling: one row means a row of NODES).
        /// </summary>
        private static List<TooltipChildren.Dossier> FleetBadges(AgeTransform lozenge)
        {
            try
            {
                GarrisonsLabelButton label =
                    lozenge == null ? null : lozenge.GetComponent<GarrisonsLabelButton>();
                if (label == null)
                {
                    return null;
                }

                FleetBadgeList.Clear();
                TooltipChildren.AddPlain(FleetBadgeList, label.ExplorationShipIcon);
                TooltipChildren.AddPlain(FleetBadgeList, label.ColonyShipIcon);
                TooltipChildren.AddPlain(FleetBadgeList, label.DestructionModuleIcon);
                return FleetBadgeList;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a fleet lozenge's badges threw: " + e);
                return null;
            }
        }

        // Reused rather than allocated per fleet: this map rebuilds every frame and a galaxy holds
        // many fleets. Safe as one buffer because the caller declares it before the next fleet is
        // read, and the emit captures each dossier by value.
        private static readonly List<TooltipChildren.Dossier> FleetBadgeList =
            new List<TooltipChildren.Dossier>(3);

        /// <summary>A fleet's node as the tree declares it - a GROUP where the map drew badges on its
        /// lozenge, so each badge's sentence is a stop of its own under the fleet, and the plain leaf
        /// it has always been otherwise.</summary>
        private static void AddFleet(
            GraphBuilder builder,
            string key,
            NodeVtable vtable,
            List<TooltipChildren.Dossier> badges
        )
        {
            ControlId id = ControlId.Structural(key);
            if (badges == null || badges.Count == 0)
            {
                // Synthetic: the row stands for a thing in the galaxy model; the enumeration above is the honesty about it.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            // Synthetic for the same reason as the leaf above.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                TooltipChildren.Emit(builder, key, badges, builder.Region);
            }

            builder.EndGroup();
        }

        /// <summary>One fleet under way on one of a system's lanes, with the lane already named the way
        /// the lane node names itself.</summary>
        private struct EnRoute
        {
            public Fleet Fleet;
            public int Number;
            public string Direction;
            public bool Wormhole;
        }

        /// <summary>
        /// The fleets under way on a system's lanes, in lane order, each carrying its lane's number.
        ///
        /// A fleet is taken by the FIRST lane that claims it. Two links can run between the same pair of
        /// systems - a wormhole beside a starlane - and a fleet's leg is a pair of positions rather than
        /// a link (<see cref="FleetPresence"/>), so both would claim it; declaring it twice under one
        /// system is a duplicate control id, which throws the whole screen out of Build.
        /// </summary>
        private static List<EnRoute> EnRouteOn(StarSystemNode node, List<Lane> lanes)
        {
            List<EnRoute> flying = new List<EnRoute>();
            IPositioningService positioning =
                Amplitude.Unity.Framework.Services.GetService<IPositioningService>();
            for (int i = 0; i < lanes.Count; i++)
            {
                IList<Fleet> onLane = FleetPresence.FleetsOn(lanes[i].Link);
                for (int j = 0; j < onLane.Count; j++)
                {
                    Fleet fleet = onLane[j];
                    if (
                        Holds(flying, fleet)
                        || !Bound(positioning, fleet, lanes[i].Link, node)
                    )
                    {
                        continue;
                    }

                    flying.Add(
                        new EnRoute
                        {
                            Fleet = fleet,
                            Number = i + 1,
                            Direction = CompassDirections.KeyForBearing(lanes[i].Bearing),
                            Wormhole = lanes[i].Wormhole,
                        }
                    );
                }
            }

            return flying;
        }

        /// <summary>
        /// Whether a fleet flying <paramref name="link"/> belongs under <paramref name="node"/> - which
        /// is to say whether that is the end it is heading FOR.
        ///
        /// A lane's two ends are both on the screen, and the tree used to hang a fleet under each, on
        /// the reasoning that either is a true answer to "where is it". That reasoning was overturned
        /// for a fleet crossing open space on 2026-08-16 - the picture never shows where anything set
        /// out FROM - and it was only ever true of open space by accident: a lane fleet's source is not
        /// drawn either. What the map shows of a fleet under way is where it is and, once it is
        /// selected, the path AHEAD. So a fleet in transit hangs under the end it is arriving at, the
        /// same end a free mover hangs under, and it appears once for everyone.
        ///
        /// A fleet on a lane that is not under way at all - stopped between two stars - is heading for
        /// neither end, so there is no destination to prefer and it keeps the row under EACH end that
        /// it has always had. The rule is about a fleet IN TRANSIT; a fleet that has stopped is as much
        /// at one end's lane as at the other's.
        /// </summary>
        private static bool Bound(
            IPositioningService positioning,
            Fleet fleet,
            Link link,
            StarSystemNode node
        )
        {
            try
            {
                GameNode goal = GoalOf(positioning, fleet);
                if (
                    goal != null
                    && (
                        ReferenceEquals(goal, link.ExtremityNode1)
                        || ReferenceEquals(goal, link.ExtremityNode2)
                    )
                )
                {
                    return ReferenceEquals(goal, node);
                }

                return true;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// Where a fleet under way is heading, or null while it is parked - the ONE thing the map
        /// exposes about a fleet's journey, and the very thing this tree files a fleet in transit
        /// under (<see cref="Bound"/>, <see cref="FreeMovingAt"/>).
        ///
        /// Shared with the inspect cursor's travel key, which must not read any more of a route than
        /// this: the current leg's goal is drawn - the fleet is animated along it, and its own row
        /// says it - while the rest of a foreign fleet's plan is the simulation's and not the
        /// player's. One accessor, so that "what the map exposes" cannot drift between the two
        /// readers.
        /// </summary>
        internal static GameNode DestinationOf(Fleet fleet)
        {
            try
            {
                return GoalOf(
                    Amplitude.Unity.Framework.Services.GetService<IPositioningService>(),
                    fleet
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The node a fleet's current leg is flying to, or null while it is not under way.
        /// </summary>
        private static GameNode GoalOf(IPositioningService positioning, Fleet fleet)
        {
            FleetPosition position = fleet.Position;
            return positioning == null || position.IsInOrbit || !position.IsInMovement
                ? null
                : positioning.GetGameNode(position.Movement.Goal);
        }

        private static bool Holds(List<EnRoute> flying, Fleet fleet)
        {
            for (int i = 0; i < flying.Count; i++)
            {
                if (ReferenceEquals(flying[i].Fleet, fleet))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>How many fleets the map is drawing out in the space around this system, as a whole
        /// phrase - what is on its lanes and what is crossing open space to or from it, which together
        /// are exactly the fleets its branch opens onto. Both halves, because a count that named one of
        /// them would stop matching the children the player walks. Nothing at all where there are none -
        /// a system with nothing moving near it says nothing about it, exactly as one with nothing
        /// parked says nothing.</summary>
        private static string UnderWayNearby(StarSystemNode node, Empire empire)
        {
            try
            {
                int count =
                    EnRouteOn(node, LanesOf(node, empire)).Count + FreeMovingAt(node).Count;
                return count == 0
                    ? null
                    : ModStrings.Plural(
                        ModStrings.GalaxyFleetUnderWayNearbyOne,
                        ModStrings.GalaxyFleetsUnderWayNearby,
                        count
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a fleet is doing and how far it can still go this turn.
        ///
        /// What it is MADE of is said before this, by the fleet phrase every surface names a fleet
        /// with (<see cref="FleetPhrase.Composition"/>) - a list of what the ships are, which is what
        /// the bare total used to stand in for.
        /// </summary>
        private static string FleetText(Fleet fleet)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
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
        ///
        /// Shared with the scanner, whose fleet results carry the same two words
        /// (<c>GalaxyScanner.Fleets</c>) so a fleet found by the scanner and the same fleet met on the
        /// map say the same thing about where it is.
        /// </summary>
        internal static string FleetState(Fleet fleet)
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
        /// the lozenge's leaves the node with a review buffer that never fills,
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

                return MergedLozenge(fleet);
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

                    // Synthetic: a ship in a hangar is read out of the fleet model, which the map draws nothing for.
                    builder.AddItem(Nodes.Synthetic(
                        ControlId.Structural(place + "/hangar/" + held.GUID),
                        vtable
                    ));
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

            Anchor();
            Sight();
        }

        /// <summary>One obliterator missile the player has been shown, and the mote the map is drawing
        /// for it while the camera is on it.</summary>
        internal struct SightedShot
        {
            public ObliteratorProjectile Shot;
            public ObliteratorProjectileLabel Label;
        }

        /// <summary>One ally pin the player has been shown, and the label the map is drawing for it
        /// while the camera is on it.</summary>
        internal struct SightedPin
        {
            public CoordinationRequest Request;
            public CoordinationRequestLabel Label;
        }

        /// <summary>
        /// The missiles and the pins, from the SIMULATION under the game's own knowledge gates - the
        /// same move the probes made (<see cref="Anchor"/>) and for the same reason: the label lists
        /// are emptied by a camera cull that says nothing about what the player may know.
        ///
        /// The gates are not the same as each other and neither is invented here. A missile is a
        /// moving entity like a probe, so it is <c>Visibility[empire] >= 3</c>
        /// (<c>VisibleEntityLabel.ShowOrHideIfVisibleByEmpire</c>). A pin is not an entity anybody can
        /// see - it is a message - so its gate is the request's own
        /// <c>CoordinationRequest.IsVisible</c>, which is "not force-hidden and shared with my
        /// alliance" (<c>CoordinationRequestLabel.CanShowRequestLabel</c> asks exactly that). What is
        /// deliberately NOT asked is that method's other half, <c>ShowRequestToggle</c>: that is the
        /// player's global "draw the pins" switch, and whether a reader should obey it is a question
        /// for the owner rather than a fact about knowledge.
        /// </summary>
        private void Sight()
        {
            _shots.Clear();
            _sighted.Clear();
            try
            {
                Empire empire = Gui.PlayerEmpire;
                Game game = Gui.Game;
                Empire[] empires = game == null ? null : game.Empires;
                for (int e = 0; empires != null && e < empires.Length; e++)
                {
                    DepartmentOfDefense defense =
                        empires[e] == null ? null : empires[e].GetAgency<DepartmentOfDefense>();
                    ReadOnlyCollection<ObliteratorProjectile> shots =
                        defense == null ? null : defense.ObliteratorProjectiles;
                    for (int i = 0; shots != null && i < shots.Count; i++)
                    {
                        ObliteratorProjectile shot = shots[i];
                        if (shot != null && MapVisibility.Sighted(shot.Visibility, empire))
                        {
                            _shots.Add(
                                new SightedShot { Shot = shot, Label = LabelFor(shot) }
                            );
                        }
                    }
                }

                ICoordinationRequestRepositoryService requests =
                    Amplitude.Unity.Framework.Services
                        .GetService<ICoordinationRequestRepositoryService>();
                IEnumerable<CoordinationRequest> all = requests;
                if (all != null && empire != null)
                {
                    foreach (CoordinationRequest request in all)
                    {
                        if (request != null && request.IsVisible(empire))
                        {
                            _sighted.Add(
                                new SightedPin { Request = request, Label = LabelFor(request) }
                            );
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the missiles and pins threw: " + e);
            }
        }

        private ObliteratorProjectileLabel LabelFor(ObliteratorProjectile shot)
        {
            for (int i = 0; i < _projectiles.Count; i++)
            {
                if (_projectiles[i] != null && ReferenceEquals(_projectiles[i].Entity, shot))
                {
                    return _projectiles[i];
                }
            }

            return null;
        }

        private CoordinationRequestLabel LabelFor(CoordinationRequest request)
        {
            for (int i = 0; i < _pins.Count; i++)
            {
                if (
                    _pins[i] != null
                    && ReferenceEquals(_pins[i].CoordinationRequest, request)
                )
                {
                    return _pins[i];
                }
            }

            return null;
        }

        /// <summary>One probe the player has been shown, and the star it is drawn nearest to.</summary>
        private struct DriftingProbe
        {
            /// <summary>The mote the map is drawing for it, where the camera happens to be close
            /// enough to be drawing one - null otherwise (<see cref="Anchor"/>). Everything the row
            /// says has a source that does not need it; the label is only ever a shortcut to the
            /// dossier the game assembles at draw time.</summary>
            public ProbeLabel Label;
            public Probe Probe;

            /// <summary>The star its row measures its bearing from, or null while the map is naming no
            /// system at all to measure from. Not a parent: every probe's row is at the top of the stop
            /// (<see cref="AddProbes"/>).</summary>
            public StarSystemNode Near;
        }

        /// <summary>
        /// Which star each probe is out from, which is the question the PICTURE answers: the map draws
        /// the mote at the probe's own position (<c>VisibleEntityLabel.RefreshPositionAndSize</c> puts
        /// the label on <c>camera.WorldToScreenPoint</c> of it), and a sighted player reads where it is
        /// by seeing which star it is out from and how far. So the nearest system the map is naming is
        /// what its row measures from (<see cref="ProbeBearing"/>) - the row itself is at the top of
        /// the stop whatever the answer is (<see cref="AddProbes"/>).
        ///
        /// Nearest is asked afresh on every build rather than remembered from the launch, because the
        /// answer MIGRATES: a probe crosses about six units of galaxy a turn and the fixture's
        /// neighbouring stars are sixteen to twenty-seven apart, so one flying towards a neighbour
        /// reads from the star it launched from for two turns and from the neighbour after that -
        /// which is exactly what the picture shows.
        ///
        /// The candidates are the systems this page is DECLARING, not every system in the galaxy: a
        /// bearing measured from a star the player cannot see names a place they have never heard of.
        ///
        /// The probes themselves come from the SIMULATION rather than from the motes the map is
        /// drawing, because the mote is subject to a camera cull that has nothing to do with what the
        /// player is allowed to know: the label windows keep only the entities Unity's own
        /// <c>CullingGroup</c> reports inside the world camera's frustum
        /// (<c>GalaxyEntityCulling</c> → <c>VisibleEntityLabelsWindow.RefreshLabelsCulling</c>), and
        /// zooming out took every probe row and the whole scanner category away with it. The
        /// information gate is the OTHER test the same window makes -
        /// <c>VisibleEntityLabel.ShowOrHideIfVisibleByEmpire</c>'s <c>Visibility[empire] >= 3</c> -
        /// and that is asked here in full (<see cref="MapVisibility.Sighted"/>). So the tree says the
        /// same thing at every zoom step, and says nothing the picture would not have said had the
        /// camera been closer.
        /// </summary>
        private void Anchor()
        {
            _drifting.Clear();
            try
            {
                Empire empire = Gui.PlayerEmpire;
                Game game = Gui.Game;
                Empire[] empires = game == null ? null : game.Empires;
                for (int e = 0; empires != null && e < empires.Length; e++)
                {
                    DepartmentOfDefense defense =
                        empires[e] == null ? null : empires[e].GetAgency<DepartmentOfDefense>();
                    ReadOnlyCollection<Probe> probes = defense == null ? null : defense.Probes;
                    for (int i = 0; probes != null && i < probes.Count; i++)
                    {
                        Probe probe = probes[i];
                        if (probe == null || !MapVisibility.Sighted(probe.Visibility, empire))
                        {
                            continue;
                        }

                        _drifting.Add(
                            new DriftingProbe
                            {
                                Label = LabelFor(probe),
                                Probe = probe,
                                Near = NearestSystem(probe.GalaxyPosition),
                            }
                        );
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: placing the probes against the systems threw: " + e);
            }
        }

        /// <summary>The mote the map happens to be drawing for this probe, or null while the camera
        /// has culled it out.</summary>
        private ProbeLabel LabelFor(Probe probe)
        {
            for (int i = 0; i < _probes.Count; i++)
            {
                if (_probes[i] != null && ReferenceEquals(_probes[i].Entity, probe))
                {
                    return _probes[i];
                }
            }

            return null;
        }

        /// <summary>The declared system nearest a point on the map, with no radius: the point is one
        /// the map is drawing something at, and the nearest star is what a sighted player reads its
        /// position against however far off it is.</summary>
        private StarSystemNode NearestSystem(GalaxyPosition position)
        {
            NearestPick pick = new NearestPick(double.PositiveInfinity);
            for (int i = 0; i < _systems.Count; i++)
            {
                pick.Offer(i, GalaxyPosition.SqrDistance(_systems[i].GalaxyPosition, position));
            }

            return pick.Found ? _systems[pick.Index] : null;
        }

        /// <summary>Where a probe's node hangs: out in open space, at the top of the stop, wherever the
        /// map happens to be drawing it (owner ruling 2026-08-19). The star it is nearest to is still
        /// worked out - it is what its row measures the bearing from - but it is a thing the row SAYS,
        /// never a place the row lives under.</summary>
        private static string ProbeKey(DriftingProbe probe)
        {
            return "galaxy:probe/" + probe.Probe.GUID;
        }

        private static ControlId ProbeId(DriftingProbe probe)
        {
            return ControlId.Structural(ProbeKey(probe));
        }

        /// <summary>One travelling probe as the SCANNER needs it: what it is called, what else its row
        /// says about it, and the node that row is - the last of which only this page can work out,
        /// since a probe's key is the page's own to build.</summary>
        internal struct ScannedProbe
        {
            public Probe Probe;
            public string Name;
            public string Extra;
            public ControlId Node;
        }

        /// <summary>
        /// The probes the scanner offers, off the very list the tree's own probe rows are built from
        /// (<see cref="Anchor"/>) - so a probe the scanner finds always has the row it sends the cursor
        /// to, and neither can name a probe the other does not.
        ///
        /// The words are the row's own: the dossier's title, whose probe it is, and - for the player's
        /// own alone, because that is the only one the map draws a countdown on - how many turns it has
        /// left. Its bearing from the star it hangs under is NOT here: that is a sentence about a place
        /// the scanner is not measuring from, and the scanner has already said where the probe is in
        /// the pair and the offset the player is standing at.
        /// </summary>
        internal IList<ScannedProbe> ScannedProbes()
        {
            List<ScannedProbe> found = new List<ScannedProbe>(_drifting.Count);
            try
            {
                for (int i = 0; i < _drifting.Count; i++)
                {
                    DriftingProbe it = _drifting[i];
                    MessageBuilder extra = new MessageBuilder();
                    extra.Fragment(Owner(it.Probe.Empire));
                    string left = ProbeCountdown(it);
                    if (!string.IsNullOrEmpty(left))
                    {
                        if (extra.IsEmpty)
                        {
                            extra.Fragment(left);
                        }
                        else
                        {
                            extra.ListItemForcedComma(left);
                        }
                    }

                    found.Add(
                        new ScannedProbe
                        {
                            Probe = it.Probe,
                            Name = ProbeName(it.Probe),
                            Extra = extra.Build(),
                            Node = ProbeId(it),
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: listing the probes for the scanner threw: " + e);
            }

            return found;
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
        ///
        /// EVERY probe is here, at the top of the open-space region beside the missiles and the pins,
        /// and none of them under a star (owner ruling 2026-08-19). A probe is not going anywhere the
        /// map can name - it has neither node nor link, only a position and a heading - so hanging it
        /// under the star it happens to be nearest to buried it in a branch the player has no reason to
        /// open and made a probe the one thing on this page whose place changed while it flew. Which
        /// star it is out from is still the way a sighted player reads the mote's position, and the row
        /// still says it (<see cref="ProbeBearing"/>) - as a sentence about where it is, not as a
        /// parent.
        /// </summary>
        private void AddProbes(GraphBuilder builder)
        {
            for (int i = 0; i < _drifting.Count; i++)
            {
                // Synthetic: a drifting probe is a game fact the map draws as a mote, not as a control.
                builder.AddItem(Nodes.Synthetic(ProbeId(_drifting[i]), ProbeNode(_drifting[i])));
            }
        }

        /// <summary>What one probe's row says: whose it is, where it is, and - for the player's own -
        /// how many turns it has left before it burns out.</summary>
        private static NodeVtable ProbeNode(DriftingProbe drifting)
        {
            DriftingProbe found = drifting;
            ProbeLabel it = drifting.Label;
            Probe probe = drifting.Probe;
            StarSystemNode near = drifting.Near;
            AgeTooltip dossier = it == null ? null : it.Tooltip;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ProbeName(probe)),
                    GalaxyCoordinates.Part(() => probe.GalaxyPosition),
                    GraphNodes.ValuePart(() => Owner(probe.Empire), false),
                    GraphNodes.ValuePart(() => ProbeBearing(probe, near), false),
                    GraphNodes.ValuePart(() => ProbeCountdown(found), false),
                },
                // The dossier only exists while the game is drawing the mote: it is assembled at
                // draw time onto the label's own tooltip, so a probe the camera has culled has a row
                // with no review section rather than no row.
                Sections = GraphNodes.Sections(null, dossier),
            };
            Follow(vtable, probe, it == null ? null : it.AgeTransform, dossier);
            return vtable;
        }

        /// <summary>
        /// What a probe is called. The game gives it no name of its own; the words are the title of
        /// the wrapper it hangs the dossier on (<c>ProbeLabel.Bind</c> sets
        /// <c>Tooltip.Target = new GuiProbe(probe)</c>), which is a class title and so is the same for
        /// every probe in the game - read once and kept, since it is asked on every frame a probe row
        /// is focused and is not a thing that changes.
        /// </summary>
        private static string ProbeName(Probe probe)
        {
            try
            {
                if (_probeName == null && probe != null)
                {
                    _probeName = AgeText.Clean(new GuiProbe(probe).Title) ?? string.Empty;
                }

                return string.IsNullOrEmpty(_probeName) ? null : _probeName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string _probeName;

        /// <summary>
        /// How many turns a probe has left before it burns out.
        ///
        /// The map writes this on the mote for the player's OWN probes and nobody else's
        /// (<c>ProbeLabel.Refresh</c>: the background is shown only when the probe's empire is the
        /// player's, and the text is <c>GuiProbe.RemainingLifetime</c> with the turn icon), and both
        /// halves of that are said here - the label's own text where the map is drawing it, and the
        /// same two things composed the same way where the camera has culled the mote away. The
        /// gate is the game's, not this mod's: a foreign probe's countdown stays unsaid at every zoom.
        /// </summary>
        private static string ProbeCountdown(DriftingProbe drifting)
        {
            try
            {
                if (drifting.Label != null)
                {
                    return Countdown(
                        drifting.Label.DurationBackground,
                        drifting.Label.DurationLabel
                    );
                }

                Probe probe = drifting.Probe;
                return probe == null || !ReferenceEquals(probe.Empire, Gui.PlayerEmpire)
                    ? null
                    : AgeText.Clean(new GuiProbe(probe).RemainingLifetime + "[turn]");
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Where a probe is, said from the star it is nearest to: which way out it lies, in the same eight
        /// words a starlane's direction is said in, and how far out in turns of the probe's OWN flight.
        ///
        /// Turns rather than a bare length, because a length in galaxy units is a number nothing else in
        /// this game is ever said in, while turns are what the map's own countdown beside the mote is
        /// already counting and what every distance this mod says is already said in. The probe's speed
        /// is its own (<c>SimulationProperties.Probe.Speed</c>, the game's number, not a derived one),
        /// so "three turns out" and "four turns left" can be heard against each other: that probe will
        /// burn out about seven turns from this star.
        ///
        /// Rounded to the turn, and a probe less than half a turn out says only which way it went - a
        /// rounded zero would claim a precision the picture does not have. One that has not left at all
        /// says so, because its direction is not a direction yet.
        /// </summary>
        private static string ProbeBearing(Probe probe, StarSystemNode near)
        {
            try
            {
                if (near == null)
                {
                    return null;
                }

                double east = probe.GalaxyPosition.X - near.GalaxyPosition.X;
                double north = probe.GalaxyPosition.Y - near.GalaxyPosition.Y;
                double distance = Math.Sqrt((east * east) + (north * north));
                if (distance < 0.01)
                {
                    return ModStrings.Format(ModStrings.GalaxyProbeAt, near.LocalizedName);
                }

                string direction = ModStrings.Get(CompassDirections.DirectionKey(east, north));
                float speed = probe.Speed;
                int turns = speed > 0f ? (int)Math.Round(distance / speed) : 0;
                if (turns <= 0)
                {
                    return ModStrings.Format(
                        ModStrings.GalaxyProbeNear,
                        direction,
                        near.LocalizedName
                    );
                }

                return ModStrings.Format(
                    turns == 1 ? ModStrings.GalaxyProbeOutOne : ModStrings.GalaxyProbeOutMany,
                    direction,
                    near.LocalizedName,
                    turns
                );
            }
            catch (Exception)
            {
                return null;
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
            for (int i = 0; i < _shots.Count; i++)
            {
                ObliteratorProjectileLabel it = _shots[i].Label;
                ObliteratorProjectile shot = _shots[i].Shot;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(
                            () => ModStrings.Get(ModStrings.GalaxyObliteratorProjectile)
                        ),
                        GalaxyCoordinates.Part(() => shot.GalaxyPosition),
                        GraphNodes.ValuePart(() => Owner(shot.Empire), false),
                        GraphNodes.ValuePart(() => ShotCountdown(shot), false),
                    },
                    // The sentence the game writes into the mote's tooltip, composed from the missile
                    // itself rather than read off the mote - so a shot the camera is not looking at
                    // still says where it is aimed. Same gate, same words, same arithmetic.
                    Sections = GraphNodes.Sections(() => ShotDetails(shot), null),
                };
                Follow(
                    vtable,
                    shot,
                    it == null ? null : it.AgeTransform,
                    it == null ? null : it.Tooltip
                );
                // Synthetic: a shot in flight is read out of the battle, not off any widget.
                builder.AddItem(Nodes.Synthetic(ProjectileId(shot), vtable));
            }
        }

        /// <summary>
        /// How many turns a missile has left, by the game's own arithmetic
        /// (<c>ObliteratorProjectileLabel.Refresh</c>): the distance still to fly over its speed,
        /// rounded UP, and 99 for a missile that is somehow not moving.
        /// </summary>
        private static int ShotTurns(ObliteratorProjectile shot)
        {
            double east = shot.GalaxyPosition.X - shot.Destination.GalaxyPosition.X;
            double north = shot.GalaxyPosition.Y - shot.Destination.GalaxyPosition.Y;
            double away = Math.Sqrt((east * east) + (north * north));
            float speed = shot.Speed;
            return speed > 0f ? (int)Math.Ceiling(away / speed) : 99;
        }

        /// <summary>The countdown the map draws beside a missile - for the player's OWN missile alone,
        /// which is the game's own choice about what an empire may know.</summary>
        private static string ShotCountdown(ObliteratorProjectile shot)
        {
            try
            {
                return ReferenceEquals(shot.Empire, Gui.PlayerEmpire)
                    ? AgeText.Clean(ShotTurns(shot) + "[turn]")
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the game says about a missile in its tooltip: which system it is aimed at and
        /// how long it has. Written for the player's own missile only - somebody else's is a thing on
        /// the map with no destination attached, and inventing one from the model would tell the
        /// player something the game is deliberately not showing.</summary>
        private static IList<string> ShotDetails(ObliteratorProjectile shot)
        {
            try
            {
                if (!ReferenceEquals(shot.Empire, Gui.PlayerEmpire))
                {
                    return null;
                }

                string said = AgeText.Clean(
                    Gui.Localize(
                        "%ObliteratorProjectileLabelDescription",
                        ShotTurns(shot),
                        shot.Destination.LocalizedName
                    )
                );
                return string.IsNullOrEmpty(said) ? null : new List<string> { said };
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a missile's destination threw: " + e);
                return null;
            }
        }

        /// <summary>Where a missile's row stands in the tree - out in open space, where the map draws
        /// it. Built here rather than spelled out at each caller so the scanner's jump and the tree's
        /// declaration cannot drift apart.</summary>
        internal static ControlId ProjectileId(ObliteratorProjectile shot)
        {
            return ControlId.Structural("galaxy:projectile/" + shot.GUID);
        }

        /// <summary>Where a constellation's GROUP node stands in the tree - built here, beside the one
        /// place that declares it (<see cref="AddConstellation"/>), so the scanner's jump and the
        /// declaration cannot drift apart.</summary>
        internal static ControlId ConstellationId(Constellation constellation)
        {
            return ControlId.For(constellation, ConstellationKey + constellation.GUID);
        }

        /// <summary>The same, for an ally's pin.</summary>
        internal static ControlId PinId(CoordinationRequest request)
        {
            return ControlId.Structural(PinKey(request));
        }

        private static string PinKey(CoordinationRequest request)
        {
            return "galaxy:pin/" + request.GUID;
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
            for (int i = 0; i < _sighted.Count; i++)
            {
                CoordinationRequestLabel it = _sighted[i].Label;
                CoordinationRequest request = _sighted[i].Request;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => PinKind(request)),
                        // A pin is named by its KIND - "attack here" - so the pair is the only thing
                        // that says WHERE here is.
                        GalaxyCoordinates.Part(() => request.GalaxyPosition),
                        // The words on the pin come off the REQUEST, which is where the label reads
                        // them from too (<c>CoordinationRequestLabel.Refresh</c> assigns
                        // <c>CoordinationRequest.Message</c> into its field every refresh) - so the
                        // message is said whether or not the camera is drawing the pin, and is never
                        // the truncated form the field may have fitted to its box.
                        GraphNodes.ValuePart(() => AgeText.Clean(request.Message), false),
                    },
                    Sections = GraphNodes.Sections(() => PinDetails(request), null),
                };
                Follow(
                    vtable,
                    request,
                    it == null ? null : it.AgeTransform,
                    it == null ? null : it.RequestTooltip
                );

                string key = PinKey(request);
                ControlId id = PinId(request);
                vtable.ControlType = ControlTypes.Group;
                // Synthetic: an ally's pinned request is read out of the coordination model.
                builder.BeginGroup(Nodes.Synthetic(id, vtable));
                if (builder.IsExpanded(id))
                {
                    CoordinationRequest dismissed = request;
                    // Synthetic: mod-authored - the command that puts one of those requests away.
                    builder.AddItem(Nodes.Synthetic(
                        ControlId.Structural(key + "/dismiss"),
                        GraphNodes.Button(
                            () => PinDismissName(dismissed),
                            () => DismissPin(dismissed)
                        )
                    ));
                }

                builder.EndGroup();
            }
        }

        /// <summary>
        /// What the game explains a pin with, in its own two sentences: what this KIND of request
        /// means, and who sent it.
        ///
        /// Both are written into the label's tooltips as plain localized text
        /// (<c>CoordinationRequestLabel.SetTooltips</c>) from nothing but the request and its owner,
        /// so both are recomposed here rather than read off a tooltip that only exists while the map
        /// is drawing the pin. The sender sentence has two forms and the game picks by ownership -
        /// yours says what the pin is for, somebody else's names them and their faction.
        /// </summary>
        private static IList<string> PinDetails(CoordinationRequest request)
        {
            List<string> lines = new List<string>(2);
            try
            {
                AddLine(
                    lines,
                    AgeText.Clean(
                        Gui.Localize(
                            "%CoordinationTools" + request.RequestType
                                + "CoordinationRequestTooltip"
                        )
                    )
                );

                if (Mine(request))
                {
                    AddLine(
                        lines,
                        AgeText.Clean(
                            Gui.Localize("%CoordinationToolsSenderCoordinationRequestTooltip")
                        )
                    );
                    return lines;
                }

                GuiEmpire sender =
                    Gui.GuiWrapperProviderService.GetGuiEmpire(request.OwnerEmpire);
                string named = sender.LocalizedName
                    + " ("
                    + sender.GuiFaction.GetSymbolString(false)
                    + sender.GuiFaction.LocalizedName
                    + ")";
                AddLine(
                    lines,
                    AgeText.Clean(
                        Gui.Localize(
                            "%CoordinationToolsReceiverCoordinationRequestTooltip",
                            named
                        )
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a pin's sentences threw: " + e);
            }

            return lines;
        }

        /// <summary>Whether this pin is the player's own, which is what decides both the words the
        /// game explains it with and what letting go of it DOES.</summary>
        private static bool Mine(CoordinationRequest request)
        {
            return ReferenceEquals(Gui.PlayerEmpire, request.OwnerEmpire);
        }

        /// <summary>What the game calls the button that lets go of a pin - a different sentence for
        /// your own pin (which is deleted for everyone) and somebody else's (which is only hidden from
        /// you), because they are different acts.</summary>
        private static string PinDismissName(CoordinationRequest request)
        {
            try
            {
                return AgeText.Clean(
                    Gui.Localize(
                        Mine(request)
                            ? "%CoordinationToolsSenderDismissCoordinationRequestTooltip"
                            : "%CoordinationToolsReceiverDismissCoordinationRequestTooltip"
                    )
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Let go of a pin, by the game's own two routes rather than by pressing its button
        /// (<c>CoordinationRequestLabel.OnDismissCb</c>): your OWN pin is removed for everybody with
        /// the order that does that, and somebody else's is force-hidden for you alone and its
        /// visibility recomputed. The label's own <c>Hide()</c> is not replayed - the request raises
        /// <c>VisibilityChanged</c> and any label that exists hides itself off that, which is also
        /// what makes this work with no label at all.
        /// </summary>
        private static void DismissPin(CoordinationRequest request)
        {
            try
            {
                if (Mine(request))
                {
                    Gui.GetActivePlayerController()
                        .PostOrder(new OrderRemoveCoordinationRequest(request));
                    return;
                }

                request.SetForceHidden(true);
                request.UpdateVisiblity(Gui.PlayerEmpire);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: letting go of a pin threw: " + e);
            }
        }

        /// <summary>What the game calls this kind of request, in its own words.</summary>
        internal static string PinKind(CoordinationRequest request)
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

        /// <summary>Point at the thing the map draws out between the stars, so the game shows its dossier
        /// where it can be read. The camera is not asked for here: a drifting row IS a place to the
        /// page's one camera rule, which slides across to it exactly as it does to a star
        /// (<see cref="OnFocusVisual"/>).</summary>
        private static void Follow(
            NodeVtable vtable,
            IGameEntityWithGalaxyPosition entity,
            AgeTransform widget,
            AgeTooltip tooltip
        )
        {
            AgeTransform anchor = widget;
            AgeTooltip tip = tooltip;
            vtable.OnFocusVisual = () =>
            {
                if (anchor != null)
                {
                    PointerFocus.MoveTo(null, tip, anchor);
                }
            };
            vtable.OnBlurVisual = ReleasePointer;
        }

        /// <summary>
        /// The tooltip the map is drawing for one thing standing on it, and the widget that tooltip
        /// should be drawn under - what the pointer is aimed at for a thing the player is looking AT
        /// without standing on its node (<see cref="GalaxyInspect"/>).
        ///
        /// Asked of the THING every frame rather than remembered as a widget: the map pools its
        /// labels and re-points them at other places as the camera slides, so a widget taken when a
        /// cell was read can be drawing another star by the time the camera has arrived - and a
        /// label the map only binds once it gets there starts showing its tooltip without the player
        /// having to move the cursor again.
        ///
        /// The same widget each of these things' own node points at, because it is the same
        /// question: a system's star tooltip once the camera is in on it and its map label
        /// otherwise (<see cref="AddSystem"/>), a fleet's lozenge rather than its label
        /// (<see cref="FleetLozenge"/>), and the mote the map draws for anything out between the
        /// stars.
        ///
        /// One place it deliberately parts from the node's own aim: a system's row prefers the
        /// FULLER card where the orbital window's is the thin one (<see cref="OrbitalStarDossier"/>),
        /// and nothing here is read as words - this aim exists so that a watcher sees the tooltip
        /// appear where the player is looking, and at that zoom the orbital anchor is the only one
        /// of the two still on the screen.
        /// </summary>
        internal bool MapMark(
            IGameEntityWithGalaxyPosition thing,
            out AgeTooltip tooltip,
            out AgeTransform anchor
        )
        {
            tooltip = null;
            anchor = null;
            try
            {
                StarSystemNode place = thing as StarSystemNode;
                if (place != null)
                {
                    AgeTooltip star = OrbitalStarTooltip(place);
                    if (star != null)
                    {
                        tooltip = star;
                        anchor = star.AgeTransform;
                        return true;
                    }

                    StarSystemLabel label = LabelFor(place, SystemLabels());
                    if (label != null && Visible(label.AgeTransform))
                    {
                        tooltip = label.StarTooltip;
                        anchor = label.AgeTransform;
                    }

                    return tooltip != null;
                }

                Fleet fleet = thing as Fleet;
                if (fleet != null)
                {
                    anchor = FleetLozenge(fleet, DockLabels(), FleetLabels());
                    tooltip = Raw(anchor);
                    return tooltip != null;
                }

                Probe probe = thing as Probe;
                if (probe != null)
                {
                    ProbeLabel mote = LabelFor(probe);
                    anchor = mote == null ? null : mote.AgeTransform;
                    tooltip = mote == null ? null : mote.Tooltip;
                    return tooltip != null;
                }

                ObliteratorProjectile shot = thing as ObliteratorProjectile;
                if (shot != null)
                {
                    ObliteratorProjectileLabel mote = LabelFor(shot);
                    anchor = mote == null ? null : mote.AgeTransform;
                    tooltip = mote == null ? null : mote.Tooltip;
                    return tooltip != null;
                }

                CoordinationRequest pin = thing as CoordinationRequest;
                if (pin != null)
                {
                    CoordinationRequestLabel mote = LabelFor(pin);
                    anchor = mote == null ? null : mote.AgeTransform;
                    tooltip = mote == null ? null : mote.RequestTooltip;
                    return tooltip != null;
                }

                return false;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding what the map draws for a thing on it threw: " + e);
                tooltip = null;
                return false;
            }
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
        /// <summary>The same selection a fleet's own node makes, for the one other thing on this page
        /// that names a fleet without walking to it (<see cref="GalaxyInspect"/>).</summary>
        internal static void SelectFleet(Fleet fleet)
        {
            Select(fleet);
        }

        /// <summary>
        /// NEXT IDLE FLEET, without the detour through the fleet's berth.
        ///
        /// The game's own button flies the camera to the fleet FIRST and selects afterwards
        /// (<c>EndTurnWindow.SelectIdleFleet</c> :1387-1411: a docked fleet gets
        /// <c>RequestGalaxyOverviewViewLevel(slot.position)</c> and then a coroutine that waits for the
        /// overview level before handing the fleet to the cursor). On this page that is one camera move
        /// too many: the flight lands on the docking slot, and then the cursor arriving on the fleet's
        /// row asks for the star's own framing and the picture moves a second time. Owner-reported
        /// 2026-08-26 as "the camera centres on the fleet, before the mod's reconciliation moves it to
        /// the star system's orbital view".
        ///
        /// So the node takes the route itself, in the order the page uses for everything else: the
        /// cursor is sent to the fleet's row through the page's ONE landing
        /// (<see cref="GoTo"/>) with the camera left to the row's own focus
        /// (<see cref="FollowPlace"/>) - a docked fleet's star framed, a fleet under way slid to its own
        /// point - and only then is the fleet selected, with no camera request of its own
        /// (<see cref="SelectSeated"/>). One move, and it is the move the row would have made anyway.
        ///
        /// The game's own cycle is still what picks the fleet (<c>GetNextIdleFleet</c>, which advances
        /// the window's counter), so a keyboard press and a mouse click walk the same fleets in the same
        /// order. Answers false only where that cycle cannot be reached at all, which is the caller's
        /// signal to press the button the old way.
        /// </summary>
        internal bool GoToNextIdleFleet()
        {
            EndTurnWindow window = TurnWindow();
            if (window == null || NextIdleFleetOf == null)
            {
                return false;
            }

            try
            {
                Fleet fleet = NextIdleFleetOf.Invoke(window, null) as Fleet;
                // Nothing to go to, a mode waiting for a target (a click selects nothing while one is
                // up), or a fleet the map refuses to select: consumed and silent, exactly as the fleet's
                // own row is (<see cref="Select"/>).
                if (fleet == null || CursorTargeting.Aiming || !FleetPresence.Selectable(fleet))
                {
                    return true;
                }

                MapTarget target;
                if (TargetFor(fleet, out target))
                {
                    GoTo(target, MapCamera.None);
                }

                SelectSeated(fleet);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: going to the next idle fleet threw: " + e);
                return true;
            }
        }

        /// <summary>
        /// Select a fleet where the camera is already showing it - the selection half of
        /// <see cref="GoToNextIdleFleet"/>, with every camera request taken out.
        ///
        /// A DOCKED fleet is the pair of calls the game's own coroutine makes once its flight has
        /// arrived (<c>EndTurnWindow.SelectFleetWhenViewReady</c>): hand the berth to the cursor, which
        /// is what opens the fleet panel, then tell the panel which of the berth's fleets was meant.
        /// Measured 2026-08-26 with the camera already framing the fleet's star: the panel opened, the
        /// docking cursor swapped in, and the camera stayed bit-identical for 1.2 s - the overview wait
        /// the game's coroutine exists for is a wait for the flight this route never starts.
        ///
        /// A fleet with no berth - one under way - has no docking slot to hand over, and the game's own
        /// routine falls through to a call that only STASHES the fleet for a window that is never
        /// shown. That case keeps the map's own selection (<see cref="SelectOnMap"/>), whose camera
        /// request is aimed at the fleet the landing is already going to.
        /// </summary>
        private static void SelectSeated(Fleet fleet)
        {
            try
            {
                // The map's own selection asks the camera for the fleet through the very call the mod
                // watches for the GAME sending the player somewhere - and the player is already being
                // sent there, by the landing above (<see cref="GalaxyLocate.Suppressed"/>).
                GalaxyLocate.Suppressed = true;
                if (FleetOrders.Orbit(fleet) != null)
                {
                    Amplitude.Unity.View.ICursorService cursors =
                        Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
                    IVisibleDockingSlotRepositoryService slots =
                        Amplitude.Unity.Framework.Services.GetService<IVisibleDockingSlotRepositoryService>();
                    DockingSlotCursorTarget berth =
                        slots == null ? null : slots.GetDockingSlotWithFleet(fleet);
                    global::FleetsScreen panel = Gui.GuiServiceAvailable
                        ? Gui.GuiService.GetWindow<global::FleetsScreen>(false)
                        : null;
                    if (berth != null && cursors != null && panel != null)
                    {
                        cursors.Select(berth);
                        panel.SelectIdleFleet(fleet);
                        return;
                    }

                    if (panel != null)
                    {
                        // The game's own fall-back for a fleet it cannot find a berth for, kept so that
                        // an orbiting fleet the map is drawing no slot for behaves as the button does.
                        Log.Warn(
                            "galaxy: the next idle fleet is in orbit and the map draws no berth for it"
                                + " - falling back to the game's own hand-over"
                        );
                        panel.SelectIdleFleet(fleet);
                        return;
                    }
                }

                SelectOnMap(fleet);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: selecting the fleet the camera is already showing threw: " + e);
            }
            finally
            {
                GalaxyLocate.Suppressed = false;
            }
        }

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

                // The map's own cursor target refuses an automated fleet outright
                // (<see cref="FleetPresence.Selectable"/>), so there is nothing to hand the selection
                // to. Said here as well as at the node, because the inspect cursor's Enter and the
                // scanner's fallback both come in through this door with no node in between.
                if (!FleetPresence.Selectable(fleet))
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

        // The cycle itself - which fleet the button would have gone to, and the counter it advances, so
        // that the key and the click walk the same fleets in the same order
        // (<see cref="GoToNextIdleFleet"/>).
        private static readonly MethodInfo NextIdleFleetOf = Member("GetNextIdleFleet", Type.EmptyTypes);

        private static MethodInfo Member(string name)
        {
            return Member(name, new Type[] { typeof(Fleet) });
        }

        private static MethodInfo Member(string name, Type[] arguments)
        {
            try
            {
                return typeof(EndTurnWindow).GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    arguments,
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
            // The aim is written down beside the visual, from the same argument, because the navigator
            // re-commits a standing cursor's pointer when this answer changes - which is how a node
            // reading a widget the map POOLS follows the pool.
            vtable.PointsAt = () => Raw(Transform(it));
        }

        /// <summary>The same for a widget with no button under it: a running total, one of the screen
        /// icons. Nothing lights up - there is nothing there to light - and the tooltip appears, which
        /// for these is the whole of what the pointer was ever for.</summary>
        private static void PointAt(NodeVtable vtable, AgeTransform widget)
        {
            AgeTransform it = widget;
            vtable.OnFocusVisual = () => PointerFocus.MoveTo(it, Raw(it), it);
            vtable.OnBlurVisual = ReleasePointer;
            vtable.PointsAt = () => Raw(it);
        }

        /// <summary>
        /// The lozenge the map is drawing for a fleet whose OWN label it has folded away.
        ///
        /// Two fleets that come close enough together on screen are drawn as one marker: the map
        /// merges their labels (<c>MergedFleetLabels</c>), hides each fleet's own and binds the merged
        /// marker's button to the whole group. A fleet in that state has a hidden lozenge, so the node
        /// pointed at nothing, promised the group dossier and drew it nowhere - measured on the
        /// beginner fixture, where two fleets two turns out share a marker and neither said anything.
        ///
        /// The marker's dossier is the GROUP's, which is exactly what the mouse gets for hovering it:
        /// there is no per-fleet tooltip to be had while the map is drawing them as one thing.
        /// </summary>
        private static AgeTransform MergedLozenge(Fleet fleet)
        {
            FleetLabelsWindow window = Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<FleetLabelsWindow>(false)
                : null;
            if (window == null)
            {
                return null;
            }

            MergedFleetLabels[] merged = window.GetComponentsInChildren<MergedFleetLabels>(true);
            for (int i = 0; i < merged.Length; i++)
            {
                MergedFleetLabels group = merged[i];
                DualGarrisonsLabelButtons buttons =
                    group == null ? null : group.GarrisonsButtons;
                if (buttons == null || !Visible(group.AgeTransform))
                {
                    continue;
                }

                AgeTransform found =
                    Holding(buttons.FriendlyGarrisonsButton, fleet)
                    ?? Holding(buttons.HostileGarrisonsButton, fleet);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>The merged marker's button, where the group it is bound to holds this fleet - asked
        /// of the wrapper on its own tooltip, which is the list the game itself drew the marker
        /// from.</summary>
        private static AgeTransform Holding(GarrisonsLabelButton button, Fleet fleet)
        {
            AgeTransform widget = Lozenge(button);
            AgeTooltip tooltip = button == null ? null : button.Tooltip;
            GuiFleetGroup group = tooltip == null ? null : tooltip.Target as GuiFleetGroup;
            if (widget == null || group == null || !Visible(widget))
            {
                return null;
            }

            IList<Garrison> garrisons = group.Garrisons;
            for (int i = 0; garrisons != null && i < garrisons.Count; i++)
            {
                if (garrisons[i] != null && garrisons[i].GUID == fleet.GUID)
                {
                    return widget;
                }
            }

            return null;
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

        /// <summary>The same, for a node that also had the map drawing something for it: the pointer
        /// goes down and the constellation label the mod was holding shown goes back to the game.
        /// </summary>
        private static readonly Action ReleaseConstellation = () =>
        {
            ConstellationLabelHold.Release();
            PointerFocus.Release();
        };

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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Bookmarks;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Bookmarks;
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
    /// orbiting; a fleet under way on a lane hangs under the system it is ARRIVING at, saying which
    /// lane it is on and which way that lane leaves, because what the map shows of a fleet under way
    /// is where it is and where it is going, never where it set out from. A fleet crossing OPEN SPACE
    /// hangs under its DESTINATION the same way (<see cref="AddFreeMoving"/>), or at the top of the systems list where the map
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
    public sealed partial class GalaxyHudScreen : Screen
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

        /// <summary>The scan overlay's own furniture - its title strip, its legend, its announcement and
        /// the centre panel the System lens draws - worn by this page while the galaxy scan lens is up
        /// (<see cref="ScanLensPanels"/>).</summary>
        private readonly ScanLensPanels _lens = new ScanLensPanels();

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

        /// <summary>The ten places the player has named on this map and the keys that make and use
        /// them (<see cref="GalaxyBookmarks"/>). Like the scanner it is not a mode and hangs off the
        /// page; unlike either of the other two it also puts rows in the tree.</summary>
        private readonly GalaxyBookmarks _bookmarks;

        public GalaxyHudScreen()
        {
            _inspect = new GalaxyInspect(this);
            _scanner = new GalaxyScanner(this);
            _bookmarks = new GalaxyBookmarks(this);
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

        // Reused across builds rather than allocated per frame: the galaxy is walked whole to work
        // out which systems the player can see, and Build runs every tick.

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
        ///
        /// AND WHILE THE GALAXY SCAN LENS IS UP (owner ruling 2026-09-01). Scan view is the same map
        /// wearing a different light: the stars are where they were, the lanes run where they ran, and
        /// what the lens adds is a reading per system and its own furniture over the top. So the page
        /// keeps the keyboard rather than handing it to a second screen with a second tree - which is
        /// what makes the inspect cursor, the scanner, the bookmarks, the type-ahead and the map
        /// summary work in-mode with no copies at all. The two flags are exact complements outside a
        /// battle or a cinematic (<c>GuiManager</c> :353, :355), so there is no frame in which neither
        /// answers and the cursor is never dropped on the way in.
        ///
        /// The one thing borrowed from the page that used to own the mode is the BATTLE STANDDOWN
        /// (<see cref="ScanLensPanels.BattleEnding"/>): a player who left the battle's own Scan toggle
        /// checked is still in the game's scan mode for the frames the battle screen takes to fade,
        /// and the galaxy's lens is genuinely up underneath it. The lens's ARRIVAL gate is not in here
        /// - it governs the lens's own furniture and rows rather than the page
        /// (<see cref="Scanning"/>), because the page keeping the cursor across the mode change is the
        /// whole point of the ruling.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                GuiManager gui = GuiService();
                if (gui == null || !GalaxyViewLevels.Overview)
                {
                    return false;
                }

                bool showing =
                    gui.IsInNormalView
                    || (gui.IsInGalaxyScanView && !ScanLensPanels.BattleEnding());
                return showing
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
            return _inspect.HandleKey(actionKey)
                || _scanner.HandleKey(actionKey)
                || _bookmarks.HandleKey(actionKey);
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
            _lens.Baseline();
            _scanWas = false;
            _fleetPanel.Baseline();
            // Arrived at because the GAME took the player here, rather than because a screen drawn over
            // the map was closed: the tree has to be told which system the picture is of
            // (<see cref="FollowCentredSystem"/>). Taken on the push, so an arrival nothing answered
            // cannot be spent on some later visit.
            _arrivalWindow = ArrivalWindowFrames;
            _centreSeat = 0;
            _centreSettle = 0;
            if (GalaxyOverviewEntry.Take())
            {
                ArmCentreSeat();
            }
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
            _lens.Forget();
            _scanWas = false;
            // The lens's owner headings are this page's tree, and the engine asks about them from
            // outside it (<see cref="NoteGrouping"/>).
            ForgetGrouping();
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
            // And so does a star the map was taken in on: the page that answers a pick is this one.
            ForgetPick();
            // The inspect cursor is a mode of THIS page: whatever replaced the map is where the player
            // now is, and a square still drawn on a map nobody is looking at would be a mode nothing
            // could end.
            _inspect.Forget();
            // And a bookmark landing still waiting for a branch to open: the branch belongs to the
            // map that is going away, and a landing answered on some later visit would move the
            // cursor for a reason nobody could remember.
            _bookmarkLanding = null;
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
            // The same for a star the map was taken in on: the player has moved the cursor themselves,
            // and where they are now is where they meant to be.
            ForgetPick();
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
        /// system had grown a "Manage system" button in front of it - with the card's own words still
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
            get { return GalaxyViewLevels.ChangingLevel || _binding > 0 || _centreSeat > 0; }
        }

        /// <summary>The inspect cell is a mode of this page's map widget, and while it is DRIVING the
        /// player is reading squares rather than rows - so a cursor the tree re-seats underneath them
        /// says nothing until they leave (<see cref="Screen.SilentUnderMode"/>). Scoped to the cell
        /// being live AND on the map: parked, the player is reading an ordinary stop and every landing
        /// there speaks as usual.</summary>
        public override bool SilentUnderMode
        {
            get { return GalaxyInspect.Active; }
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
            WatchTheLens();
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
            // After it, because a request that names a place beats a picture that merely became true:
            // an outstanding "go and look at this" cancels the arrival seat outright.
            FollowCentredSystem();
            // After it, because the two write the same one landing slot and this one is the player's
            // own key of a moment ago - a probe armed on the same frame the game asked to be shown
            // somewhere is still armed, and the mode is where the player has to be.
            FollowProbeArming();
            // After both, and outstanding over several frames rather than answered on one: this
            // landing waits for the camera the game is still flying into the system.
            FollowActionSeat();
            // And beside it: a bookmark jump lands INSIDE the system it names, which is a branch that
            // has to be opened before there is anything in it to land on
            // (<see cref="FollowBookmarkLanding"/>).
            FollowBookmarkLanding();
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
        /// Nine of the game's fleet actions order nothing when they are pressed: Colonize, Super
        /// Colonize, Destroy Planet, Expedition, Launch Mining Probe and Reclaim Mothership all just
        /// select the fleet's system and fly the camera in (<c>FleetActionButtonColonize.OnClick</c>
        /// and its four siblings; <c>FleetActionToggleReclaimMothership.OnToggle</c>), and so do the
        /// juggernaut's three planet-construction toggles - Terraform, Restore and Reduce Anomaly,
        /// which share one <c>OnToggle</c> on <c>EmpireLocalActionTogglePlanetConstruction</c>
        /// (:23-38) and are told apart by the action DEFINITION each was loaded with. The reason is
        /// the same for all nine: the real order is a control the map draws once it is there - a
        /// planet's own colonize, destroy, terraform, restore or reduce-anomaly button, a curiosity in
        /// orbit, a probe site, the wreck. <see cref="None"/> is every other action: the ones that
        /// post an order themselves and the ones that arm a targeting cursor.
        ///
        /// A toggle whose work is ALREADY under way cancels it instead of zooming (the same branch in
        /// both <c>OnToggle</c>s, and for the three juggernaut actions the cancel raises a
        /// confirmation box). The seat is armed either way, exactly as it already is for Reclaim
        /// Mothership: the cancel simply leaves a target that is never drawn and the wait runs out.
        /// </summary>
        public enum SeatTarget
        {
            None,
            Colonize,
            Destroy,
            Expedition,
            MiningProbe,
            Wreck,
            Terraform,
            Restore,
            ReduceAnomaly,
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

                if (control is EmpireLocalActionTogglePlanetConstruction)
                {
                    return PlanetConstruction(control);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a fleet action's control class threw: " + e);
            }

            return SeatTarget.None;
        }

        /// <summary>
        /// Which of the juggernaut's three planet-construction actions a toggle is, asked of the
        /// action DEFINITION rather than of the control class - because the class does not answer it.
        ///
        /// Terraform and Restore share one control
        /// (<c>EmpireLocalActionTogglePlanetTerraformation</c>, whose only override is the wording of
        /// the cancel confirmation), so what tells them apart is the definition each item was loaded
        /// with - and that is the same question the CARD asks to decide which of its buttons to draw
        /// (<c>PlanetLabel_SystemOrbital.RefreshTerraformationStatus</c> /
        /// <c>RefreshRestorationStatus</c> / <c>RefreshAnomalyReductionStatus</c>, each fetching its
        /// own <c>Initiateâ€¦EmpireActionFleetActionDefinition</c>). Restoration's definition DERIVES
        /// from terraformation's, so it is tested first or every restore would read as a terraform.
        ///
        /// An unrecognised planet-construction action is <see cref="SeatTarget.None"/>: nothing is
        /// invented about which button it wants, and the camera move it makes is still followed by the
        /// page's own answer to a picked node (<see cref="GalaxyPick"/>).
        /// </summary>
        private static SeatTarget PlanetConstruction(FleetActionControl control)
        {
            EntityActionDefinition definition = control.EntityActionDefinition;
            if (definition is InitiateRestorationEmpireActionFleetActionDefinition)
            {
                return SeatTarget.Restore;
            }

            if (definition is InitiateTerraformationEmpireActionFleetActionDefinition)
            {
                return SeatTarget.Terraform;
            }

            if (definition is InitiateAnomalyReductionEmpireActionFleetActionDefinition)
            {
                return SeatTarget.ReduceAnomaly;
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
                case SeatTarget.Terraform:
                    return ModStrings.FleetsActionSeatsTerraform;
                case SeatTarget.Restore:
                    return ModStrings.FleetsActionSeatsRestore;
                case SeatTarget.ReduceAnomaly:
                    return ModStrings.FleetsActionSeatsReduceAnomaly;
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

            // The camera move this action just made was seen by the map's own watch a moment ago
            // (<see cref="GalaxyPick"/>, the same GalaxyView call a click makes). This seat names a
            // control INSIDE the system, which is the finer answer, so the pick is dropped here rather
            // than left to be stood down frame by frame - the wait below clears _seatTarget on the very
            // frame it lands, and a pick still standing would then take the cursor off it.
            GalaxyPick.Forget();
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
                // The juggernaut's three. Each card draws its own button for the action it can take
                // on that world, and the card's IN-PROGRESS button is deliberately not offered here:
                // the toggle only zooms when there is no action running, and where one IS running the
                // toggle cancels it and moves no camera at all.
                case SeatTarget.Terraform:
                    return AgeWidgets.Transform(card.TerraformationButton);
                case SeatTarget.Restore:
                    return AgeWidgets.Transform(card.RestorationButton);
                case SeatTarget.ReduceAnomaly:
                    return AgeWidgets.Transform(card.AnomalyReductionButton);
            }

            return null;
        }

        /// <summary>The first curiosity the card is drawing - PAINTED, the same gate
        /// <see cref="AddCuriosities"/> declares them by, because the ring pools its items and retires
        /// a surplus one by fading it rather than hiding it.</summary>
        private static AgeTransform FirstCuriosity(PlanetLabel_SystemOrbital card)
        {
            AgeTransform table = card.PlanetCuriositiesTable;
            if (table == null || !AgeWidgets.Visible(table))
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
                if (items[i] != null && AgeWidgets.Visible(items[i]))
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
            if (!CursorMoved() || node == null)
            {
                return;
            }

            if (Scanning && ScanLensPanels.SystemInfoStop.Equals(node.StopKey))
            {
                CentreOnScanSystem();
                return;
            }

            if (!SystemStop.Equals(node.StopKey))
            {
                return;
            }

            NoteScanSystem(node);
            object place;
            bool inside;
            if (Place(node, out place, out inside))
            {
                FollowPlace(place, inside);
            }
        }

        /// <summary>The last star the tree cursor stood on inside the map stop - kept so that stepping
        /// into the System lens's own stop can ask for THAT system rather than whichever one the camera
        /// happens to be nearest (<see cref="CentreOnScanSystem"/>).</summary>
        private StarSystemNode _scanRow;

        private void NoteScanSystem(GraphNode node)
        {
            for (GraphNode walk = node; walk != null; walk = walk.Parent)
            {
                StarSystemNode star = walk.Id == null ? null : walk.Id.Subject as StarSystemNode;
                if (star != null)
                {
                    _scanRow = star;
                    return;
                }
            }
        }

        /// <summary>
        /// TAB INTO THE SYSTEM LENS'S PANEL ASKS ABOUT THE STAR THE CURSOR WAS ON (owner ruling
        /// 2026-09-01; a judgement call the owner allowed to move the camera).
        ///
        /// The panel is bound to whichever system is nearest the middle of the screen and to nothing
        /// else - the game gives no way to point it at a system - so a player who has walked the map
        /// stop to a star and then tabs across for its figures would be read a different star's. Tabbing
        /// in is a deliberate gesture, so it slides the camera onto the row the cursor was on and the
        /// game's own choice then agrees with the player's. It is a SLIDE and never a zoom: the rung is
        /// the lens, and changing it here would change what the whole screen means.
        ///
        /// Only where the two disagree, so tabbing back and forth over the same star moves nothing.
        /// </summary>
        private void CentreOnScanSystem()
        {
            try
            {
                StarSystemNode star = _scanRow;
                if (star == null)
                {
                    return;
                }

                GalaxyPosition at = star.GalaxyPosition;
                Vector3 focus;
                if (GalaxyViewLevels.CameraTarget(out focus))
                {
                    double dx = focus.x - at.X;
                    double dy = focus.z - at.Y;
                    if (dx * dx + dy * dy < CentredEnough)
                    {
                        return;
                    }
                }

                // The game's own "show me this" pan, which slides and leaves the rung alone - and is
                // already marked as the mod's own camera move, so it does not come back round as a
                // fresh locate request.
                GalaxyViewLevels.PanTo(star);
            }
            catch (Exception e)
            {
                Log.Warn("scan: centring the lens on the focused system threw: " + e);
            }
        }

        /// <summary>How near the camera has to be to a star before it counts as already looking at it -
        /// squared galaxy units, well under the map's closest neighbour spacing.</summary>
        private const double CentredEnough = 0.25;

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
        /// crossing a lane and one crossing open space alike get a row under the system they are bound
        /// for - a filing that says where to LOOK for the row, not where
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

            // A place the PLAYER put out on the map, which the map draws nothing at. It is a point
            // like the four above and belongs with them, ahead of the ancestor walk: its row hangs in
            // a constellation for reading order and it is not a thing that constellation contains.
            BookmarkPoint bookmark = BookmarkAt(node == null ? null : node.Id);
            if (bookmark != null)
            {
                place = bookmark;
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
        /// and a fleet away from any berth - the four kinds that travel, and so the four whose rows
        /// are ANCHORED on the thing itself (<see cref="PlacedRows.Anchor"/>; <see cref="PositionOf"/>
        /// walks these same lists for the same reason).
        ///
        /// Resolved through the page's own indexes - the very lists the rows are declared from - and
        /// never by reading a row's KEY, which is a string this page builds and not a fact about the
        /// map. A fleet flying a lane has exactly one row, under the end it is arriving at
        /// (<see cref="Bound"/>), and the index holds that one.
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
            // THE ZOOM LEVEL TELLS YOU WHAT AN EXPANSION GIVES (owner ruling 2026-09-01, the graded
            // model). Below the detail band the map draws no inside for a system - no planet dots, no
            // orbital cards - so there is nothing for the camera to come in on, and the far bands stay
            // what they are for: reading the map's geometry, which lane runs where and what is standing
            // on it. A system opened at those distances therefore opens IN PLACE, and walking its lanes
            // and fleets moves the picture no closer than the player put it.
            //
            // Only for the cursor. A landing SAID OUT LOUD keeps its own framing
            // (<paramref name="asked"/>): it has already forced the band its target needs
            // (<see cref="EnsureBand"/>), and going to a place is a request to be taken there.
            if (inside && !asked && !ZoomBands.MapDetail)
            {
                inside = false;
            }

            // AND NEVER UNDER A LENS (owner ruling 2026-09-01). In scan mode the zoom ladder does not
            // choose how much is drawn, it chooses WHAT THE PICTURE MEANS - a step is a different
            // lens - so a camera move made as a side effect of reading a row would silently change the
            // subject of everything on the screen. Expansion is in place at every lens and a landing
            // slides and seats; the player's own ladder is the only thing that picks a lens. The
            // spoken landing is no exception here, which is why this is asked after
            // <paramref name="asked"/> rather than beside it.
            if (inside && Scanning)
            {
                inside = false;
            }

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
                    // Where the camera was when the inside took it in, so that closing the branch can
                    // hand it back (<see cref="CollapseZoom"/>).
                    NoteJump(system);
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

                // A bookmarked point of galaxy: the same slide the things below get, off the position
                // the player kept rather than off an entity, since there is no entity there at all.
                BookmarkPoint bookmark = place as BookmarkPoint;
                if (bookmark != null)
                {
                    GalaxyViewLevels.CenterOn(bookmark.At, LandingDamping);
                    GalaxyViewLevels.Settle();
                    _settling = SnapSettleFrames;
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

        /// <summary>
        /// Write down the view a system's inside is about to take the camera away from, so that
        /// closing the branch can give it back (owner ruling 2026-09-01: COLLAPSE ALWAYS HANDS BACK THE
        /// VIEW YOU WERE BROWSING).
        ///
        /// Only a real jump is remembered - a camera already at the orbital view is not going anywhere
        /// and has nothing to restore, and a second step among the same system's children must not
        /// overwrite the level the first one came from.
        ///
        /// Kept per system, because a player can be in and out of several: opening one at level 8 and
        /// then walking into another from the orbital view leaves the first with a view to go back to
        /// and the second with none, which is exactly what each of them should do when it is shut.
        ///
        /// An INSTANCE field, deliberately: a hot reload builds this page afresh and the memory goes
        /// with it, which is the case the fallback exists for (<see cref="CollapseZoom"/>).
        /// </summary>
        private void NoteJump(StarSystemNode system)
        {
            int level = ZoomBands.Level;
            int inside = ZoomBands.LowestLevel(BandKind.Planets, BandFidelity.Full);
            if (system == null || level < Bands.FirstLevel || level >= inside)
            {
                return;
            }

            if (!_jumpedFrom.ContainsKey(system))
            {
                _jumpedFrom[system] = level;
            }
        }

        /// <summary>
        /// Take the camera back out of a system whose branch has just been closed.
        ///
        /// The view the expansion jumped from where one was written down (<see cref="NoteJump"/>), and
        /// otherwise SPOKEN LEVEL 9 - the inspect cursor's own entry ceiling, so the two "a sane
        /// distance to be put at" cameras in the mod are one number (owner ruling 2026-09-01, revised).
        /// The fallback is not a defensive branch: a hot reload wipes the memory while the player is
        /// standing inside an open system, and the first thing they do afterwards is close it.
        ///
        /// Only while the camera is still looking INTO this system, which is the same gate the old
        /// unzoom had: focus moves the camera about the map freely, so by the time a branch is closed
        /// the player may be reading somewhere else entirely, and flying the camera home from over
        /// there would move a view nobody asked about.
        ///
        /// Silent, like the expansion: the rung it lands on is announced by the one watcher that
        /// reports every zoom change however it was made (<see cref="ZoomWatch"/>).
        /// </summary>
        private void CollapseZoom(StarSystemNode node)
        {
            if (node == null || !ReferenceEquals(GalaxyViewLevels.FocusedSystem, node))
            {
                return;
            }

            int level;
            int step = _jumpedFrom.TryGetValue(node, out level)
                ? level - 1
                : GalaxyInspect.EntryZoomCeiling;
            _jumpedFrom.Remove(node);
            GalaxyViewLevels.ZoomToStep(node, step);
        }

        /// <summary>The view each open system's inside took the camera away from
        /// (<see cref="NoteJump"/>), in spoken levels.</summary>
        private readonly Dictionary<StarSystemNode, int> _jumpedFrom =
            new Dictionary<StarSystemNode, int>();

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

        // ---- the map taken in on a star by a pointer ----

        /// <summary>Drop a pointer's pick and any seat it armed - the page it was meant for has gone
        /// away, the player has moved the cursor themselves, or something that names a finer place
        /// inside the system has taken the move over. A seat an ARRIVAL armed is left alone: only a
        /// pick's own is cancelled here.</summary>
        private void ForgetPick()
        {
            GalaxyPick.Forget();
            if (_centrePick == null)
            {
                return;
            }

            _centrePick = null;
            _centreSeat = 0;
            _centreSettle = 0;
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

        // ---- the system the picture is of ----

        /// <summary>Frames the arrival seat is given to find its answer, after the settle above: the
        /// galaxy camera has to be the live one and to have stopped. Also the CAP on how long the
        /// page's arrival announcement is held for it (<see cref="BetweenViews"/>), which is why it is
        /// not the minute-long budget a locate gets - an answer that never comes must cost the player
        /// a moment, not a silence.</summary>
        private const int CentreSeatFrames = 30;

        /// <summary>How long after the page is pushed an activation still counts as this arrival's.
        /// The view level is made current before its own activation runs, so the page can be up a
        /// frame or two before the notice arrives; past that a notice is somebody else's and is
        /// dropped rather than kept for the next visit.</summary>
        private const int ArrivalWindowFrames = 20;

        /// <summary>Frames waited before the camera is asked where it is looking. The game places it
        /// one frame AFTER the page is pushed (measured 2026-08-28: on the frame the page arrives the
        /// camera still reads the position the map had before the system's page was opened, and from
        /// the next frame on it reads the new one), so an answer taken on the arrival frame is the
        /// picture that has just been left. The page's own <see cref="ViewBindFrames"/> is the wait,
        /// and the announcement is held over it - so an arrival names the system the map is showing
        /// once, rather than the one it was showing and then this one.</summary>
        private const int ArrivalSettleFrames = ViewBindFrames;

        private int _centreSeat;

        private int _centreSettle;

        private int _arrivalWindow;

        /// <summary>Start looking for the system the map is showing, and hold what the page is about to
        /// say until the answer is in.</summary>
        private void ArmCentreSeat()
        {
            // The hold itself is <see cref="BetweenViews"/> reading _centreSeat: the page has not yet
            // decided which system it is showing, so it has nothing to say, and the hold ends on the
            // frame the answer arrives - the same frame the seat is asked for, so the arrival announces
            // the seated row once instead of the row it was restored to and then this one.
            _centreSeat = CentreSeatFrames;
            _centreSettle = ArrivalSettleFrames;
        }

        // A "has the player moved the cursor since the page arrived?" stand-down was tried here and
        // taken out again (2026-08-28). It cannot be asked at this level: loading a save while the
        // cursor stands on the map RECONCILES it - the row it was on no longer exists, so the engine
        // walks it up to a survivor - and that is a cursor move nobody made, indistinguishable from a
        // keypress. It stood the seat down on exactly the arrival that needs it most. What legitimately
        // owns the cursor on an arrival is named instead, above: a locate, a fleet-action seat, a fleet
        // panel let go. The window it would have guarded is the twelve frames of
        // <see cref="ArrivalSettleFrames"/>, during which the page is holding its own announcement
        // anyway.

        /// <summary>
        /// Make the tree's cursor describe the system the map is SHOWING, whenever the map came to be
        /// showing it for a reason of the game's own. ONE rule, two triggers: an arrival nobody asked
        /// for (<see cref="GalaxyOverviewEntry"/>) - a save being loaded, coming back out of a system's
        /// management page - and the map being taken in on a star by a POINTER
        /// (<see cref="GalaxyPick"/>, <see cref="ArmPickSeat"/>) - a click, or the wheel past its
        /// deepest step. The two differ only in how the system is NAMED: an arrival has to be asked of
        /// the camera, a pick says so itself.
        ///
        /// PASSIVE where the player is reading something else. A cursor on the HUD is left exactly
        /// where it is and the map stop's remembered position is written instead
        /// (<see cref="GraphNavigator.SeatStop"/>), so a save loads reading the empire's own summary
        /// as it always has and the FIRST landing on the map - Ctrl+G, or Tab round to it - is the
        /// centred system rather than whichever row happens to be declared first. A cursor already
        /// standing on the map is a different question: the page is arriving, so whatever it is
        /// standing on is about to be read out, and reading out a system the map is not showing is the
        /// defect. It follows, and the arrival announces it the ordinary way.
        ///
        /// Either way "already right" means the PLACE agrees, not the row: a cursor inside the centred
        /// system - on one of its planets, its lanes, a fleet parked at it - is reading that system
        /// and is left alone, which is what keeps an excursion to another screen and back from
        /// bouncing the cursor up to the star it was under.
        ///
        /// The picture is asked of the CAMERA and never of the activation's arguments, which do not
        /// answer it: the way out of a management page names the system whose page was open and then
        /// sends the camera to where it was before the page opened (<see cref="GalaxyOverviewEntry"/>
        /// has the measurements). The nearest declared system to the camera's own target is what a
        /// sighted player reads as the centre of the picture, the same rule every other place-naming
        /// on this page uses (<see cref="CentredSystem"/>).
        /// </summary>
        private void FollowCentredSystem()
        {
            if (_arrivalWindow > 0)
            {
                _arrivalWindow--;
                if (_centreSeat <= 0 && GalaxyOverviewEntry.Take())
                {
                    ArmCentreSeat();
                }
            }
            else
            {
                // An activation that reached a page which has been up all along is not an arrival.
                GalaxyOverviewEntry.Forget();
            }

            // The OTHER trigger of the same rule (owner ruling 2026-08-29): the map taken in on a star
            // by a POINTER - a left click, a click on a wreck, or the wheel scrolled in past the
            // deepest step (<see cref="GalaxyPick"/>). No page change, so no arrival window to sit
            // inside; and nothing to ask the camera either, because unlike an activation this one
            // NAMES the system it is sending the camera to. An arrival already being answered wins -
            // it is the bigger change, and it will have moved the cursor to the same kind of place.
            if (_centreSeat <= 0)
            {
                ArmPickSeat();
            }

            if (_centreSeat <= 0)
            {
                return;
            }

            // Everything that names a place of its own beats a picture that merely became true: a
            // "go and look at this" (which lands announced), the seat one of the zoom-in fleet actions
            // is owed across the page change, and a fleet panel let go across it.
            if (
                _locating != null
                || GalaxyLocate.Peek() != null
                || _seatTarget != SeatTarget.None
                || _releasedAcross != null
            )
            {
                _centreSeat = 0;
                _centrePick = null;
                return;
            }

            // The camera is placed the frame AFTER the page arrives (<see cref="ArrivalSettleFrames"/>).
            if (_centreSettle > 0)
            {
                _centreSettle--;
                return;
            }

            // Only the camera is waited for, and only until it has stopped and can say where it is
            // looking. What the TREE has declared deliberately is not waited for: a page arrived at
            // from a save being loaded has not built once - the tutorial popup has the keyboard on the
            // frames that would have built it - and the answer does not need it
            // (<see cref="CentredSystem"/>).
            // ...unless the trigger already said which system, which a pointer's pick does.
            Vector3 at;
            StarSystemNode centred = _centrePick;
            if (centred == null)
            {
                centred = GalaxyViewLevels.CameraSettling
                    || !GalaxyViewLevels.CameraTarget(out at)
                    ? null
                    : CentredSystem(at);
            }

            if (centred == null)
            {
                if (--_centreSeat <= 0)
                {
                    Vector3 last;
                    Log.Warn(
                        "galaxy: the map was arrived at and never said which system it is showing"
                            + " (settling="
                            + GalaxyViewLevels.CameraSettling
                            + " camera="
                            + GalaxyViewLevels.CameraTarget(out last)
                            + ")"
                    );
                }

                return;
            }

            _centreSeat = 0;
            _centrePick = null;
            try
            {
                SeatOnCentredSystem(centred);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: seating the tree on the system the map shows threw: " + e);
            }
        }

        /// <summary>
        /// Take a pointer's pick, if there is one, and arm the same seat on the system it names.
        ///
        /// No settle is waited out: the settle exists to let the camera be PLACED before it is asked
        /// where it is looking, and nothing is asked of the camera here. Anything the map lets a
        /// pointer zoom at that is not a star is counted and no more - there is no other row for the
        /// cursor to stand on.
        /// </summary>
        private void ArmPickSeat()
        {
            GameNode picked = GalaxyPick.Take();
            if (picked == null)
            {
                return;
            }

            _centrePick = picked as StarSystemNode;
            if (_centrePick == null)
            {
                return;
            }

            ArmCentreSeat();
            _centreSettle = 0;
        }

        /// <summary>The system a pointer's pick named, while its seat is outstanding - what makes the
        /// answer below the trigger's own rather than the camera's.</summary>
        private StarSystemNode _centrePick;

        private void SeatOnCentredSystem(StarSystemNode centred)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null)
            {
                return;
            }

            ControlId id = ControlId.For(centred, SystemKey(centred));

            GraphNode standing = navigator.CurrentNode;
            if (standing != null && IsMapStop(standing.StopKey))
            {
                object place;
                bool inside;
                if (Place(standing, out place, out inside) && ReferenceEquals(place, centred))
                {
                    return;
                }

                navigator.FocusNode(id);
                return;
            }

            // Left alone only where the remembered row is BOTH a reading of this system and a row that
            // still exists. A save being loaded takes fleets and planets away under a memory that is
            // still a path into the right system - and a stop whose memory names a row nothing declares
            // falls back to the FIRST row of the whole stop, which is how a correct-looking memory
            // still lands the player in another constellation.
            ControlId remembered = navigator.RememberedStop(SystemStop);
            GraphRender render = navigator.Render;
            bool alive = remembered != null && render != null && render.NodeAt(remembered) != null;
            if (!alive || !Reads(remembered, centred))
            {
                navigator.SeatStop(SystemStop, id);
            }
        }

        /// <summary>
        /// The system a point on the map is a picture OF - the nearest one the tree gives a row to.
        ///
        /// Asked of the galaxy rather than of <see cref="_systems"/>, and by the same gate that list is
        /// built by (a colony of the empire's, or a perceived star), because the one arrival that needs
        /// this most is the one where the page has never been built: a save being loaded pushes the
        /// page under the tutorial popup, which holds the keyboard, so nothing has declared a row yet
        /// when the picture is already on the screen.
        /// </summary>
        private static StarSystemNode CentredSystem(GalaxyPosition at)
        {
            Empire empire = PlayerEmpire();
            if (empire == null || !GameGalaxy.Present())
            {
                return null;
            }

            StarSystemNode nearest = null;
            float best = float.PositiveInfinity;
            foreach (StarSystemNode node in GameGalaxy.StarSystemNodes())
            {
                if (!Perceived(node, empire) && !Colonized(node, empire))
                {
                    continue;
                }

                float distance = GalaxyPosition.SqrDistance(node.GalaxyPosition, at);
                if (distance < best)
                {
                    best = distance;
                    nearest = node;
                }
            }

            return nearest;
        }

        /// <summary>Whether the empire holds this system - the half of the tree's own list that is not
        /// the perception gate (an outpost of ours in a system we could not otherwise see is still a
        /// row).</summary>
        private static bool Colonized(StarSystemNode node, Empire empire)
        {
            DepartmentOfTheInterior interior =
                empire == null ? null : empire.GetAgency<DepartmentOfTheInterior>();
            if (interior == null)
            {
                return false;
            }

            foreach (ColonizedStarSystem colony in interior.ColonizedStarSystems)
            {
                if (ReferenceEquals(colony.Node, node))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a remembered map position is already a reading of this system - its own row
        /// or anything the tree files under it. Asked of the KEY because the row it names need not be
        /// in the render at all: the memory outlives the build, and on a page arrived at cold there is
        /// no node to walk up from.</summary>
        private static bool Reads(ControlId remembered, StarSystemNode system)
        {
            string key = remembered == null ? null : remembered.StructuralKey as string;
            if (key == null)
            {
                return false;
            }

            string place = SystemKey(system);
            return key == place || key.StartsWith(place + "/", StringComparison.Ordinal);
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
        /// (<see cref="Arrive"/>), the go-to-location key, and a bookmark jump made with the inspect
        /// cell live (<see cref="GalaxyBookmarks"/> - the one bookmark shape this landing can express).
        ///
        /// <paramref name="reach"/> is how far the landing goes, and it decides one thing: whether the
        /// camera FRAMES the destination or merely follows the cursor there
        /// (<see cref="MapReach"/>). Travelling a starlane is the one LOCAL caller - the destination is
        /// the neighbour of the row the player is standing on - and it keeps the picture at the
        /// distance they put it.
        ///
        /// Under a live cell every one of them arrives the same way: ONLY THE CELL MOVES (owner
        /// rulings 2026-08-31). The zoom is not touched - the cell's own slide is the whole camera
        /// move - and neither is the tree cursor, so leaving the mode puts the player back on the row
        /// they armed it from rather than on whatever they last looked at through the square. That
        /// second half reverses an earlier ruling of the same day which had the landing seat the
        /// cursor underneath the cell; what died with it was a deferred seat, its camera-free mark and
        /// a re-seat call on the mode - a lever apiece, all removed rather than left inert.
        ///
        /// The camera moves are marked as the MOD's own (<see cref="GalaxyLocate.Suppressed"/>): the
        /// mod pans through the same calls the game leads the player with, and an unmarked pan here
        /// would come straight back round as a fresh locate request.
        ///
        /// Answers whether the cursor was sent to a node - false for a fleet the tree has no row for
        /// and for a point the map draws nothing at, both of which leave the caller to say its own
        /// piece.
        /// </summary>
        internal bool GoTo(
            MapTarget target,
            MapCamera camera,
            MapReach reach = MapReach.Elsewhere
        )
        {
            try
            {
                MapLanding plan = MapLandings.Decide(target.Thing, GalaxyInspect.Live, reach);
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

                // A LANDING ON SOMETHING THE LENS DOES NOT DRAW LEAVES THE LENS FIRST (owner ruling
                // 2026-09-01), and only then lands the ordinary way (<see cref="LeaveTheLens"/>).
                bool leaving = Scanning && !DrawnByTheLens(target);
                if (leaving)
                {
                    LeaveTheLens();
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
                if (plan.FocusNode)
                {
                    // Before the cursor is sent anywhere: the picture has to be drawing the kind of
                    // thing it is being sent to, or there is no row to land on.
                    EnsureBand(target, leaving);
                }

                if (plan.FocusNode && target.Id != null && navigator != null)
                {
                    navigator.FocusNode(target.Id, plan.AnnounceNode);
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

        /// <summary>
        /// A SNAP LANDING FORCES ITS TARGET'S BAND (owner ruling 2026-09-01): before the player is put
        /// on a thing, the camera comes to the nearest distance at which the map is drawing that kind
        /// of thing at all - a planet or anything hanging off one to the orbital view, a fleet to the
        /// lozenges, a system to the band that names the systems - and no closer
        /// (<see cref="Bands.LowestLevel"/>).
        ///
        /// It is not a camera preference and it ignores the caller's own
        /// (<see cref="MapCamera"/>): a band that draws nothing of the kind declares no row for it, so
        /// a landing made there would send the cursor to a node that does not exist and the player
        /// would hear nothing at all - measured at levels 1-2, where a bookmark jump moved the camera
        /// and said not a word. The camera's own framing afterwards is still the landing's
        /// (<see cref="Camera"/>).
        ///
        /// Never OUT: a player reading the orbital view who goes to a fleet is not asking to be pulled
        /// back to the fleet band. And never for a thing the mod itself put on the map - a bookmarked
        /// point is an annotation rather than a rendering and has a row at every level, so a jump to
        /// one slides exactly as it always has.
        /// </summary>
        private void EnsureBand(MapTarget target, bool leaving)
        {
            // Not under a lens: there the rung IS the lens, so forcing a band would answer "take me to
            // that system" by changing what the whole screen means (owner ruling 2026-09-01). The
            // lens's own parity filter is what makes that safe - a landing is only ever sent to a kind
            // the lens is drawing, so there is a row waiting wherever the camera already is; a landing
            // on anything else has just LEFT the lens, and then the ordinary rule applies again. The
            // caller's own answer to that is taken rather than re-asked, because the game's flag has
            // not necessarily turned over by this line.
            if (Scanning && !leaving)
            {
                return;
            }

            int level = ZoomBands.Level;
            int need = BandNeeded(target, false);
            if (level < Bands.FirstLevel || need < Bands.FirstLevel || level >= need)
            {
                return;
            }

            try
            {
                GalaxyLocate.Suppressed = true;
                GalaxyViewLevels.SetZoomHere(need - 1);
                GalaxyViewLevels.Settle();
            }
            finally
            {
                GalaxyLocate.Suppressed = false;
            }

            // The picture is at a different distance than the page's record of where it sent the
            // camera was written at (<see cref="GalaxyViewLevels.Moves"/>).
            GalaxyViewLevels.Moved();
            _settling = SnapSettleFrames;
            _binding = ViewBindFrames;
            _labelCatchUp = ViewBindFrames;
        }

        /// <summary>The nearest-out level at which this target has a row, read off the one band table.
        /// A planet and everything drawn at one needs the orbital view, where the map draws the cards
        /// those rows are; a fleet needs the lozenges; a place needs the band that names the systems;
        /// and anything else standing out on the map is drawn beside the full nameplate, so it needs
        /// the same band the nameplate does. A bookmarked point needs nothing.</summary>
        private int BandNeeded(MapTarget target, bool scanning)
        {
            switch (target.Thing)
            {
                case MapThing.PlanetBound:
                    return Bands.LowestLevel(BandKind.Planets, scanning, BandFidelity.Full);
                case MapThing.Place:
                    return Bands.LowestLevel(BandKind.Systems, scanning, BandFidelity.Name);
                case MapThing.Point:
                    if (target.Select is Fleet || target.Standing is Fleet)
                    {
                        return Bands.LowestLevel(BandKind.Fleets, scanning, BandFidelity.Full);
                    }

                    return BookmarkAt(target.Id) != null
                        ? -1
                        : Bands.LowestLevel(BandKind.OpenSpace, scanning, BandFidelity.Full);
                default:
                    return -1;
            }
        }

        /// <summary>
        /// WHETHER THE LENS IS DRAWING THIS KIND OF THING AT ALL, here.
        ///
        /// The same table and the same question the tree filters its rows with, asked of the scan
        /// ladder: a landing sent to a kind the lens hides would put the cursor on a node that does not
        /// exist and say nothing at all. It is asked of the KIND and not of the level, because "the
        /// System lens draws no planet dots" and "no lens draws a fleet" are the same fact written once
        /// (<see cref="Bands"/>).
        ///
        /// A bookmarked POINT is always drawn: it is the player's own annotation rather than a
        /// rendering, and it has a row at every rung of both ladders.
        /// </summary>
        private bool DrawnByTheLens(MapTarget target)
        {
            switch (target.Thing)
            {
                case MapThing.PlanetBound:
                    // A world has a row in the mode wherever the lens rings its star with circles -
                    // at the DOT fidelity the tree already reads it at, which is all a landing needs.
                    return ZoomBands.Shows(BandKind.Planets);
                case MapThing.Place:
                    return ZoomBands.Shows(BandKind.Systems);
                case MapThing.Point:
                    if (BookmarkAt(target.Id) != null)
                    {
                        return true;
                    }

                    return target.Select is Fleet || target.Standing is Fleet
                        ? ZoomBands.Shows(BandKind.Fleets)
                        : ZoomBands.Shows(BandKind.OpenSpace);
                default:
                    return false;
            }
        }

        /// <summary>Leave the lens through the game's own toggle - the very call its own close button
        /// makes (<c>GuiManager.ToggleScanView</c>, which refuses by itself while a modal or a ground
        /// battle is up). The page says its own name on the way out, as it does for any other exit, so
        /// the player is told the light has changed before they are told where they have been
        /// sent.</summary>
        private static void LeaveTheLens()
        {
            try
            {
                Gui.GuiGameWindowService.ToggleScanView();
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: leaving the scan lens for a landing threw: " + e);
            }
        }

        /// <summary>Bring the camera to what was landed on: IN on a place - through the page's one
        /// camera rule, so the landed node's own focus adds nothing on top (<see cref="FollowPlace"/>)
        /// - and a slide onto a bare point. Where the cell is driving, the cell has already slid and
        /// only a place's zoom is added on top - so the picture is the same whichever way the player is
        /// reading the map.
        ///
        /// WHETHER the move is made as a thing asked for out loud is the plan's
        /// (<see cref="MapLanding.Frame"/>): a landing that frames overrides the distance the player
        /// chose, and a LOCAL hop asks the camera rule for exactly what walking onto the row asks for
        /// - which at the far bands is a slide and nothing more.</summary>
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
                FollowPlace(target.System, true, plan.Frame);
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
                    FollowPlace(berthed, true, plan.Frame);
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
                FollowPlace(drawn, false, plan.Frame);
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

                // A place the PLAYER put on the map. It belongs in this list for the same reason the
                // four above do - its row is keyed structurally, so nothing on the id says where it is
                // - and it was missing from it, which is the whole of bug 2026-08-31: a caller walking
                // UP from a row that could not answer reached the row's constellation, whose subject
                // is an entity with a position of its own (the centroid the map writes the name at),
                // and armed the inspect cursor a whole stretch of sky away from the bookmark the
                // player was standing on.
                BookmarkPoint bookmark = BookmarkAt(id);
                if (bookmark != null)
                {
                    at = bookmark.At;
                    return true;
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
            bool lens = Scanning;
            if (lens)
            {
                // The lens's own strip stands where the view's name goes, and the ladder goes under
                // it: the game HIDES the panel the ordinary view-title cluster is read off while the
                // overlay is up, so the ladder needs a home of its own here - and it is needed more in
                // this mode than out of it, since the rung is what selects the lens.
                builder.BeginStop(ScanLensPanels.TitleStop);
                _lens.Title(builder);
                _zoom.Build(builder, "scan:zoom");
            }
            else
            {
                _hud.Empire(builder);
                // The map's own ladder, handed to the cluster that names the view rather than appended
                // after it: the two are one control per row and the ladder comes first, which is the
                // cluster's ordering to make.
                _hud.ViewTitle(builder, _zoom);
            }

            builder.BeginStop(SystemStop);
            builder.PushContext(MapContext());
            BuildSystems(builder);
            // Popped before the fleet panel, which is a stop of its own.
            builder.PopContext();

            if (lens)
            {
                // The one panel a lens draws over the MAP rather than round its edges: the System lens
                // inspects whichever system is nearest the middle of the screen. A stop of its own,
                // right after the map (owner ruling 2026-09-01) - it is a page about one system, and
                // inside the map stop it made the star rows themselves read as a list with a document
                // at the top of it. Its own window decides whether it is there, so no lens is named
                // here.
                _lens.SystemInformation(builder);
                // The clusters the game keeps drawing over the lens are the turn controls and nothing
                // else - it hides the banners, the pinned quest and the notification strip - and the
                // fleet panel is not drawn in the mode either.
                // The overlay's own panels, which belong to no lens at all - the hacking family and the
                // scan notifications, both a DLC's and both invisible without it.
                _lens.Hacking(builder);
                builder.BeginStop(ScanLensPanels.LegendStop);
                _lens.Legend(builder);
                _hud.Turn(builder);
                return;
            }

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
        ///
        /// The focus chord rides on the map's name only. It belongs to the word "map" - it is how the
        /// player gets back here - and gluing it onto the game's own question would make a sentence out
        /// of two voices. The suffix is static per binding, so the identity above still turns only on
        /// which of the two branches answered.
        /// </summary>
        private static string MapContext()
        {
            string instruction = GlobalHud.Instruction();
            return string.IsNullOrEmpty(instruction)
                ? ChordNames.Label(ModStrings.Get(ModStrings.GalaxyMapPanel), UiActions.FocusMap, 0)
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
        /// space TO it, all three of which hang under the system itself. A fleet flying a lane is
        /// indexed under the end it is ARRIVING at and no other (<see cref="EnRouteOn"/>), exactly as a
        /// crossing of open space is indexed under its DESTINATION alone
        /// (<see cref="FreeMovingAt"/>): that is the one end the map itself shows, and one row per
        /// fleet is one search result per fleet. Every list here is the one the BRANCH is built
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
            List<EnRoute> flying = EnRouteOn(node, empire, LanesOf(node, empire));
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

            /// <summary>A LEAP rather than a lane hop - a bookmark, the home key, the scanner's go-to.
            /// It has no destination and opened nothing: all it knows is the exact row the player was
            /// standing on and, where that row was inside a system, which system that was
            /// (<see cref="Origin"/>). The two kinds share one chronological trail, because what the
            /// player wants back is the last place they left, not the last place of a particular
            /// kind.</summary>
            public bool Leap;

            /// <summary>Where the leap started, for a row that belongs to no system - a probe, a
            /// missile, an ally's pin, a bookmarked point of space. The camera has to be told
            /// somewhere, and there is no system to name.</summary>
            public GalaxyPosition At;
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
        /// A LEAP IS ABOUT TO HAPPEN - remember where the player is standing, so Backspace can bring
        /// them back (owner-approved 2026-08-31).
        ///
        /// Three gestures throw the player across the galaxy in one press: a bookmark jump, the home
        /// jump, and the scanner's go-to. Walking back from one is not possible - there is no path
        /// through the tree from Lors to wherever they were - so the way back is remembered instead,
        /// on the SAME trail the lanes use. One trail and not two, in the order things happened,
        /// because "take me back" is a question about the last place left and not about which kind of
        /// key left it.
        ///
        /// WHICH stack it goes on is decided here, once, for every caller:
        /// <list type="bullet">
        /// <item>The inspect cell up - LIVE or PARKED - and the leap is the CELL's, so the cell's own
        /// stack takes it (<c>GalaxyInspect.PushCell</c>). Parked counts: Backspace is not reachable
        /// from another stop, but the player Tabs back and it must be there.</item>
        /// <item>Off the map stop with no cell up, and NOTHING is remembered (owner ruling): Backspace
        /// is claimed only while focus is in the map stop, and a trail entry pushed from the
        /// notifications would be a way back the player could never ask for - or worse, would pull
        /// their focus off the panel they are reading.</item>
        /// <item>Otherwise the tree trail, carrying the EXACT row the cursor was on, at whatever depth:
        /// a planet, a lane, a fleet, a dossier. That is where the player was, and a way back that
        /// landed on the system instead would be a different place from the one they left.</item>
        /// </list>
        /// </summary>
        internal void NoteLeap()
        {
            try
            {
                if (GalaxyInspect.Live)
                {
                    _inspect.PushCell();
                    return;
                }

                GraphNavigator navigator = ModEntry.Navigator;
                GraphNode focused = navigator == null ? null : navigator.CurrentNode;
                if (focused == null || !IsMapStop(focused.StopKey) || focused.Id == null)
                {
                    return;
                }

                CheckTrailSession();
                StarSystemNode system = null;
                for (GraphNode walk = focused; walk != null && system == null; walk = walk.Parent)
                {
                    system = walk.Id == null ? null : walk.Id.Subject as StarSystemNode;
                }

                GalaxyPosition at = default(GalaxyPosition);
                if (system != null)
                {
                    at = system.GalaxyPosition;
                }
                else if (!PositionOf(focused.Id, out at))
                {
                    // A row standing at no point of galaxy at all - a constellation heading, the
                    // unexplored group. A leap from one is not remembered rather than remembered
                    // wrongly. (Everything that DOES stand somewhere answers above, bookmarked points
                    // of space included - see <see cref="PositionOf"/>.)
                    return;
                }

                _trail.Add(
                    new Journey
                    {
                        Leap = true,
                        Origin = system,
                        Return = focused.Id,
                        At = at,
                    }
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: remembering where a leap started threw: " + e);
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
        ///
        /// A LEAP entry (<see cref="NoteLeap"/>) obeys the SAME invalidation rule, which is the whole
        /// point of one trail: an entry that can no longer be honoured EXACTLY is dropped and the pop
        /// carries on to the one before it (owner ruling 2026-08-31, overturning an earlier
        /// fall-back-to-the-system's-row). A leap is a promise to put the player back on the row they
        /// were standing on; landing on that row's SYSTEM instead would report success while restoring
        /// nothing they meant - the case that settled it is a leap made from an in-transit fleet's row,
        /// where the fleet has since arrived and the tree files it somewhere else entirely, so the old
        /// system is not even where the thing is any more. Better to give them the leap before it, or
        /// nothing.
        /// </summary>
        private bool PopTrail()
        {
            CheckTrailSession();
            Empire empire = PlayerEmpire();
            while (_trail.Count > 0)
            {
                Journey hop = _trail[_trail.Count - 1];
                _trail.RemoveAt(_trail.Count - 1);
                if (hop.Leap)
                {
                    if (!LeapStands(hop, empire))
                    {
                        continue;
                    }

                    ReturnToLeap(hop);
                    return true;
                }

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

        /// <summary>
        /// Put the player back where a leap started, through the page's one landing like everything
        /// else that sends them somewhere (<see cref="GoTo"/>). Always the EXACT row - an entry that
        /// could not be honoured exactly never reaches here (<see cref="LeapStands"/>).
        ///
        /// A row inside a SYSTEM is a place the camera zooms to; a row that belongs to no system - a
        /// probe, a missile, a pin, a bookmarked point - is a point, and the camera slides to it.
        /// </summary>
        private void ReturnToLeap(Journey hop)
        {
            if (hop.Origin == null)
            {
                GoTo(MapTarget.Point(hop.Return, hop.At), MapCamera.Auto);
                return;
            }

            OpenPlace(hop.Origin);
            GoTo(MapTarget.Place(hop.Origin, hop.Return, hop.At), MapCamera.Auto);
        }

        /// <summary>
        /// Whether a leap can still be undone EXACTLY - the trail's one invalidation question, asked of
        /// a leap the way the fog question is asked of a lane hop.
        ///
        /// Two ways to fail. The place has gone dark, which is the lane hops' own rule; or the row
        /// itself is no longer in the tree. Telling "gone" from "merely shut" is the whole difficulty,
        /// because a row inside a collapsed branch is absent from the render and yet perfectly
        /// restorable - the landing opens its ancestors on the way, one level per build
        /// (<c>KeyGraph.AncestorKeys</c>, the same ancestry every programmatic landing reads). So: the
        /// row itself present is valid; otherwise the DEEPEST ancestor the tree is still declaring
        /// decides - shut, and the row is inside it and will arrive; open, and the row is simply not
        /// there any more.
        ///
        /// One false answer is possible and accepted: a branch opened on this very frame has not built
        /// its children yet (<c>ReachStep.Waiting</c>), and an entry asked about in that window reads
        /// as gone. Nothing opens a branch at the moment Backspace is pressed, and the cost of the
        /// wrong answer is the leap BEFORE it - a defined behaviour - rather than a landing that lies.
        /// </summary>
        private bool LeapStands(Journey hop, Empire empire)
        {
            if (hop.Origin != null && (empire == null || !Perceived(hop.Origin, empire)))
            {
                return false;
            }

            return RowStands(hop.Return);
        }

        /// <summary>
        /// Whether a row can still be landed on: it is in the render, or it is inside a branch that is
        /// merely SHUT and will open on the way (a landing opens ancestors a level per build).
        ///
        /// The one way to tell "shut" from "gone" is the deepest ancestor the tree is still declaring:
        /// shut, and the row is inside it and will arrive; open without the row in it, and the row is
        /// not there any more. Shared by everything that has to decide whether a remembered id is
        /// still worth landing on - the Backspace trail's entries and the inspect mode's way back.
        ///
        /// One false answer is possible and accepted: a branch opened on this very frame has not built
        /// its children yet (<c>ReachStep.Waiting</c>), and a row asked about in that window reads as
        /// gone. Nothing opens a branch at the moment either caller asks.
        /// </summary>
        internal bool RowStands(ControlId id)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GraphRender render = navigator == null ? null : navigator.Render;
            if (render == null || id == null)
            {
                return false;
            }

            if (render.NodeAt(id) != null)
            {
                return true;
            }

            IList<object> keys = KeyGraph.AncestorKeys(id.StructuralKey);
            for (int i = 0; i < keys.Count; i++)
            {
                GraphNode ancestor = render.NodeAt(ControlId.Structural(keys[i]));
                if (ancestor != null)
                {
                    return !ancestor.Expanded;
                }
            }

            return false;
        }

        /// <summary>
        /// WHERE TO PUT THE PLAYER BACK when the row they left is gone - the nearest thing on the map
        /// that still STANDS somewhere, measured from the place that row stood at (owner ruling
        /// 2026-08-31).
        ///
        /// The inspect mode is the caller: it remembers both the row it was armed from and that row's
        /// PLACE, and a row can die under it - a bookmark whose slot a dedupe took, a fleet that has
        /// arrived and been re-filed. Landing on nothing would leave the player wherever the tree
        /// happened to seat them; the nearest surviving place puts them where they were LOOKING, which
        /// is what "put me back" means. The dedupe case falls out for free: the slot that replaced
        /// theirs is at distance zero and wins.
        ///
        /// **Escape alone gets this.** The Backspace trail is exact-or-drop, because it always has an
        /// earlier entry to fall to; Escape has nothing behind it, so a near miss beats nothing.
        ///
        /// Candidates are the rows that stand somewhere THEMSELVES (<see cref="RowPlace"/>) - a row
        /// that only borrows its parent's position is not one, for the same reason it cannot arm the
        /// cell. Ties go to whoever is offered first (<see cref="NearestPick"/>), which is the reading
        /// order of the stop.
        /// </summary>
        internal ControlId NearestPlacedRow(GalaxyPosition at)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GraphRender render = navigator == null ? null : navigator.Render;
            if (render == null)
            {
                return null;
            }

            NearestPick pick = new NearestPick(RestoreReach);
            List<ControlId> rows = new List<ControlId>();
            for (int i = 0; i < render.Order.Count; i++)
            {
                GraphNode node = render.Order[i];
                GalaxyPosition stands;
                if (
                    node.Id == null
                    || !SystemStop.Equals(node.StopKey)
                    || !RowPlace(node.Id, out stands)
                )
                {
                    continue;
                }

                double dx = stands.X - at.X;
                double dy = stands.Y - at.Y;
                pick.Offer(rows.Count, dx * dx + dy * dy);
                rows.Add(node.Id);
            }

            return pick.Found ? rows[pick.Index] : null;
        }

        /// <summary>
        /// Where a row stands ITSELF, or false for one that stands nowhere of its own - the shared
        /// question behind arming the cell, the leap trail's origin and the restore.
        ///
        /// The registry answers WHETHER first (<see cref="PlacedRows"/>): a kind declared as a
        /// GROUPING is refused outright however well-placed the entity behind it happens to be, which
        /// is what stops a constellation heading lending its centroid to a row that has no place. A
        /// kind the table does not name is carried by an ancestor and answers here with false, and the
        /// walk that called this goes up to its star.
        /// </summary>
        internal bool RowPlace(ControlId id, out GalaxyPosition at)
        {
            at = default(GalaxyPosition);
            PlacedRow kind = id == null ? null : PlacedRows.Of(id.StructuralKey);
            if (kind == null || kind.Refuses)
            {
                // THE DIPLOMACY BAND IS THE ONE PLACE WHERE THE HEADINGS ARE THE PICTURE
                // (<see cref="DiplomacyRowPlace"/>): there is no star row anywhere at those two rungs,
                // so the table's refusal - written for a heading that gathers rows the player can walk
                // into - would leave the survey armable from nothing at all.
                return DiplomacyRowPlace(id, out at);
            }

            StarSystemNode star = id.Subject as StarSystemNode;
            if (star != null)
            {
                at = star.GalaxyPosition;
                return true;
            }

            return PositionOf(id, out at);
        }

        /// <summary>How far the restore will look for a surviving place. Larger than the galaxy on
        /// purpose: there is no distance at which "nowhere" beats "a long way off".</summary>
        private const double RestoreReach = 10000.0;

        /// <summary>The row the inspect mode leaves the player on: the one it was armed from where that
        /// still stands, and the nearest surviving place where it does not.</summary>
        internal ControlId RestoreRow(ControlId armedFrom, GalaxyPosition at)
        {
            return RowStands(armedFrom) ? armedFrom : NearestPlacedRow(at);
        }

        /// <summary>
        /// Ask for the branch, then hand the rest to the page's one landing
        /// (<see cref="GoTo"/>): the cursor, the camera, and the free cell where one is up. The
        /// expansion belongs to the next build (<see cref="ApplyPendingExpansions"/>) and the cursor to
        /// the tick after that, so the node the player lands on exists by the time they land.
        ///
        /// A LOCAL HOP (<see cref="MapReach.Local"/>): both callers - travelling a lane and backing up
        /// the one just travelled - move the player to a neighbour of the row they are standing on,
        /// not to somewhere they asked to be shown, so the camera does what an in-place expansion of
        /// the destination would do and nothing more (owner ruling 2026-09-02). It was framing before,
        /// which took a follow at spoken level 5 down to 13 while expanding the very same system in
        /// the very same breath stayed put.
        /// </summary>
        private void Arrive(ControlId id, StarSystemNode where)
        {
            OpenPlace(where);
            GoTo(
                MapTarget.Place(where, id, where.GalaxyPosition),
                MapCamera.Zoom,
                MapReach.Local
            );
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

        // ---- bookmarks (GalaxyBookmarks owns the keys; these are the map's half) ----

        /// <summary>
        /// Take the player INSIDE a system - the landing a bookmark jump makes.
        ///
        /// The cursor goes to the system's FIRST CHILD rather than to its row, which is what brings
        /// the camera all the way in: the page's one camera rule reads a row inside a system as being
        /// in that place and snaps to it, where the system's own row is a place being looked AT from
        /// wherever the camera stands (<see cref="FollowPlace"/>). So a jump leaves the player exactly
        /// where walking in with Right would have.
        ///
        /// It cannot be done on the press. The branch is shut - possibly inside a shut constellation -
        /// so the child does not exist yet and its key is not something this page can compose: what a
        /// system's first child IS depends on what the map is drawing there
        /// (<see cref="AddInside"/>). The branch is asked to open and the landing waits for the build
        /// that opens it (<see cref="FollowBookmarkLanding"/>).
        /// </summary>
        internal void LandInside(StarSystemNode node)
        {
            if (node == null)
            {
                return;
            }

            // The band first, for the reason every other landing forces one
            // (<see cref="EnsureBand"/>): from the two furthest-out levels the map names no system, so
            // the branch this asks for would never open and the jump would move the camera and say
            // nothing - measured. Beyond that the landing keeps its own framing, which is whatever
            // walking in with Right would have given at this distance.
            MapTarget place = MapTarget.Place(node, SystemRow(node), node.GalaxyPosition);
            // And, under a lens that draws no star at all, out of the lens first - the same rule the
            // one landing follows, for the same reason (<see cref="DrawnByTheLens"/>).
            bool leaving = Scanning && !DrawnByTheLens(place);
            if (leaving)
            {
                LeaveTheLens();
            }

            EnsureBand(place, leaving);
            OpenPlace(node);
            _bookmarkLanding = SystemRow(node);
            _bookmarkLandingFrames = BookmarkLandingFrames;
        }

        /// <summary>Put the cursor on the first thing inside the system a jump named, once the build
        /// that opened the branch has declared it. A system with nothing in it - and a branch that
        /// never opens, which is the budget running out - lands on the system's own row instead, which
        /// is the honest answer to "go inside" where there is no inside.</summary>
        private void FollowBookmarkLanding()
        {
            if (_bookmarkLanding == null)
            {
                return;
            }

            GraphNavigator navigator = ModEntry.Navigator;
            GraphRender render = navigator == null ? null : navigator.Render;
            GraphNode node = render == null ? null : render.NodeAt(_bookmarkLanding);
            bool open = node != null && node.Expanded;
            bool spent = --_bookmarkLandingFrames <= 0;
            if (!open && !spent)
            {
                // The branch was asked for on the press and opens on a later build: a system that is
                // merely DECLARED is the shut row the jump was made from, and landing on it would be
                // the jump giving up one frame before it could have gone inside.
                return;
            }

            ControlId landing = _bookmarkLanding;
            _bookmarkLanding = null;
            ControlId child = open ? FirstChild(render, node) : null;
            if (navigator != null)
            {
                navigator.FocusNode(child ?? landing);
            }
        }

        /// <summary>The first thing declared inside a group in this render - declaration order is the
        /// reading order, so the first is the one an arrow key would reach first.
        ///
        /// Asked of ANCESTRY rather than of the direct parent, because a group is free to sort its
        /// children under named levels of its own - a system's are seven regions
        /// (<see cref="AddInside"/>), and a region is a pushed context, so nothing inside an opened
        /// system has the system itself for a parent any more. The direct-parent test answered
        /// nothing there, which left the one landing that reads this - the bookmark jump - on the
        /// system's row instead of inside it, and so left the camera outside.</summary>
        private static ControlId FirstChild(GraphRender render, GraphNode group)
        {
            for (int i = 0; i < render.Order.Count; i++)
            {
                GraphNode node = render.Order[i];
                for (GraphNode walk = node.Parent; walk != null; walk = walk.Parent)
                {
                    if (ReferenceEquals(walk, group))
                    {
                        return node.Id;
                    }
                }
            }

            return null;
        }

        private ControlId _bookmarkLanding;
        private int _bookmarkLandingFrames;

        /// <summary>How long a bookmark landing waits for the branch it asked for. Two builds is
        /// enough - the constellation on one, the system on the next - and twelve is the same generous
        /// count every other wait on this page uses.</summary>
        private const int BookmarkLandingFrames = 12;

        /// <summary>The system a GUID names, or null where the galaxy has no such node - what a
        /// bookmark holding a system has to be resolved through, since a bookmark outlives every
        /// object the map builds.</summary>
        internal static StarSystemNode SystemByGuid(ulong guid)
        {
            try
            {
                foreach (StarSystemNode node in GameGalaxy.StarSystemNodes())
                {
                    if ((ulong)node.GUID == guid)
                    {
                        return node;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: looking a system up by its guid threw: " + e);
            }

            return null;
        }

        /// <summary>Where the player's empire began - the game's own answer
        /// (<c>DepartmentOfTheInterior.HomeSystemNode</c>), which is why the home jump is not a
        /// bookmark and nothing about it is ever written down.</summary>
        internal static StarSystemNode HomeSystem()
        {
            try
            {
                Empire empire = PlayerEmpire();
                DepartmentOfTheInterior interior =
                    empire == null ? null : empire.GetAgency<DepartmentOfTheInterior>();
                return interior == null ? null : interior.HomeSystemNode;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the home system threw: " + e);
                return null;
            }
        }

        /// <summary>The system a slot is bookmarking, where the map is listing it - null for a slot
        /// that is a point of space, empty, or naming a system this build declares no row for (the
        /// point row that stands in for it is <see cref="BookmarkedPoint"/>).</summary>
        internal StarSystemNode BookmarkedSystem(char digit)
        {
            StarSystemNode node;
            return _bookmarkSystems.TryGetValue(digit, out node) ? node : null;
        }

        /// <summary>The row a slot's point of space is declared as, or null where the slot is a system
        /// or empty. It is the id the row WOULD have, whether or not the branch it hangs in is open -
        /// a landing opens its ancestors on the way (<c>KeyGraph.AncestorKeys</c>).</summary>
        internal ControlId BookmarkedPoint(char digit)
        {
            BookmarkPoint point;
            return _bookmarkSpots.TryGetValue(digit, out point) && point.Listed ? point.Id : null;
        }

        /// <summary>The row a SYSTEM is read as on this stop - what a jump aims the tree cursor at
        /// while the inspect cell is driving the map, where landing INSIDE it would zoom a picture the
        /// mode is deliberately leaving alone (<see cref="LandInside"/> is the other, out-of-mode
        /// landing).</summary>
        internal static ControlId SystemRow(StarSystemNode node)
        {
            return node == null ? null : ControlId.For(node, SystemKey(node));
        }

        /// <summary>
        /// This build's point bookmarks, one at a time: which slot, and where it stands. False past the
        /// end, so a caller walks it with no list of its own.
        ///
        /// The inspect cell is the caller. A point bookmark is the one thing a square of the map can
        /// hold that the map draws nothing for, so the cell cannot find it by looking at the picture
        /// the way it finds everything else - it has to ask the page which places the player has
        /// named.
        /// </summary>
        internal bool BookmarkPointAt(int index, out char digit, out GalaxyPosition at)
        {
            digit = '\0';
            at = default(GalaxyPosition);
            if (index < 0 || index >= _bookmarkPoints.Count)
            {
                return false;
            }

            digit = _bookmarkPoints[index].Digit;
            at = _bookmarkPoints[index].At;
            return true;
        }

        /// <summary>
        /// Backspace on the map is the way back from every leap the player has made across it - the lanes
        /// they have travelled and the jumps they have taken, in one chronological trail
        /// (<see cref="NoteLeap"/>) - and it belongs to the MAP rather than to whatever node the cursor
        /// is on: the player is somewhere because of the hops they took, not because of the planet they
        /// are standing on.
        ///
        /// Only in the systems stop. The clusters round the edges of the screen keep whatever the key
        /// meant to them, because a trail of places has nothing to say about a notification icon.
        ///
        /// And only with the inspect cell DOWN: while it is up the key is the cell's own way back and is
        /// taken before this is ever asked (<c>GalaxyInspect.HandleKey</c>, which <see cref="AnyKey"/>
        /// offers every key first). The two ways of reading the map keep separate trails, because a
        /// square of sky is not a row and neither could restore the other.
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

        /// <summary>The map's own rule for whether a node's name is drawn: it has been explored, and
        /// it is either remembered or in sight now. Everything this screen ever says the name of is
        /// asked this first - and so is everything a ROUTE across the map names, which is why the rule
        /// itself lives in <see cref="MapVisibility"/> rather than here.</summary>
        private static bool Perceived(GameNode node, Empire empire)
        {
            return MapVisibility.Perceived(node, empire);
        }

        /// <summary>Whose a thing out in space is. A name, not a phrase: it sits beside the thing's own
        /// name the way a fleet's does, and every empire in this game has one
        /// (<see cref="EmpireNames.Named"/>).</summary>
        private static string Owner(Empire empire)
        {
            return EmpireNames.Named(empire);
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
                    if (label != null && AgeWidgets.Visible(label.AgeTransform))
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
                    tooltip = AgeWidgets.Raw(anchor);
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
        private static readonly MethodInfo SelectIdleFleet = GameHandlers.Method(
            typeof(EndTurnWindow),
            "SelectIdleFleet",
            new Type[] { typeof(Fleet) }
        );

        // The cycle itself - which fleet the button would have gone to, and the counter it advances, so
        // that the key and the click walk the same fleets in the same order
        // (<see cref="GoToNextIdleFleet"/>).
        private static readonly MethodInfo NextIdleFleetOf = GameHandlers.Method(
            typeof(EndTurnWindow),
            "GetNextIdleFleet",
            Type.EmptyTypes
        );

        // ---- shared ----

        /// <summary>The blur for a node that also had the map drawing something for it: the pointer
        /// goes down and the constellation label the mod was holding shown goes back to the game.
        /// </summary>
        private static readonly Action ReleaseConstellation = () =>
        {
            ConstellationLabelHold.Release();
            PointerFocus.Release();
        };

        private static void AddLine(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line) && !lines.Contains(line))
            {
                lines.Add(line);
            }
        }

        /// <summary>A table of things - anomalies, curiosities, deposits - reads one line per thing,
        /// which is how it is drawn and how it is reviewed. Each line is what the item SAYS
        /// (<see cref="AgeWidgets.ItemText"/>), which for a table of bare icons is the name off its own
        /// wrapper: reading such a table as text read nothing at all.</summary>
        private static void AddWidgetLines(List<string> lines, AgeTransform widget)
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

            for (int i = 0; i < children.Count; i++)
            {
                if (AgeWidgets.Visible(children[i]))
                {
                    AddLine(lines, AgeWidgets.ItemText(children[i]));
                }
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
            return GameWindows.Of<EndTurnWindow>();
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

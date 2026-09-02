using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

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
            get { return ModStrings.ScreenGalaxy; }
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
            Nearby near = MovingNear(node, empire);
            List<EnRoute> flying = near.Flying;
            List<Fleet> crossing = near.Crossing;
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
                    // Different widget: the tooltip and its anchor are read off the label, not off the node this answers for.
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
                // Content: one buffer line per child the game is drawing, which is not a node and has no gate.
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

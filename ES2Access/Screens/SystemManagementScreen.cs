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
        /// <summary>The one stop the game's left-edge INFORMATION panels share (owner design
        /// 2026-08-29): colony info, population, representatives, governor - and whatever an outpost or
        /// a ghost system draws instead - are four things to read about one system rather than four
        /// places to work, so Tab passes them once and Alt+Up/Down steps between them by name. The
        /// spaceport is not among them: it is a work surface and keeps its own stop.</summary>
        private const string SidePanelsStop = "system:side";

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

        /// <summary>
        /// WHERE THE CURSOR IS PUT, IN THREE CASES (owner design 2026-08-29). Only the first of them
        /// is this property: a page the player has never stood on has nothing to put back, and the
        /// first thing to say about a system is what the system IS, which is the left edge's
        /// information panels - now one stop, and the first stop the page itself declares.
        ///
        /// The other two cases restore where the player WAS, and neither goes through here:
        /// coming back from the galaxy, and turning the page to another system with Alt+Left/Right,
        /// both put the cursor back on the control it was on (<see cref="Restore"/>). This property
        /// is their last fallback, for a place the new system has no equivalent of at all.
        /// </summary>
        public override object InitialFocusStop
        {
            get { return SidePanelsStop; }
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
        ///
        /// AN ICON-STRIP SCREEN ENDS THE PAGE THE WAY A MODAL DOES, and for the same reason. The
        /// empire, economy and the rest are exclusive full-screen windows: showing one hides this
        /// page's own window and its planet cards a frame or two BEFORE the mod pushes that screen
        /// (measured 2026-08-29 by a per-frame trace of Enter on the colony banner). For those frames
        /// the page was still the focused screen and still rebuilt - without its cards and without its
        /// side panels - so the node the cursor stood on no longer existed, the navigator re-seated it
        /// on the last surviving HUD control, and the state a return would restore was already that
        /// wrong seat. The player saw it as "Escape from the economy screen puts me on the empire
        /// banners". The condition is the GAME's own <c>IsAnyScreenVisible</c>, which it pairs with
        /// <c>IsAnyModalVisible</c> itself (<c>GuiManager.CanToggleScanView</c>), so the answer comes
        /// from the flag the game sets rather than from a window flag this page would have to debounce
        /// - the page turn above drops <c>Shown</c> for a single frame and must NOT end the screen.
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
                if (
                    gui == null
                    || gui.IsAnyModalVisible
                    || gui.IsInLoadingWindow
                    || gui.IsAnyScreenVisible
                )
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
                        _arrived = Whole(_arriving, _arrivingPanels);
                        _arriving.Clear();
                        _arrivingPanels.Clear();
                        if (_arrived)
                        {
                            ExpandBottomPanels(window);
                        }
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

        /// <summary>
        /// Open the three bottom panels on the way in, for whoever is looking at the screen (owner
        /// request 2026-08-29). They are the constructibles, the queue and the hangar, and the game
        /// remembers how the player last left them; collapsed, a sighted observer sees about half of
        /// each list. Nothing here is for the keyboard - the button that does this is deliberately
        /// undeclared and collapsing changes no accessible content at all
        /// (<see cref="BuildBottomPanel"/>) - so this is silent, declares nothing and speaks nothing.
        ///
        /// Driven the way the game's own button drives it, both halves together: every
        /// <c>GuiFrameExpander</c> under the window is toggled AND
        /// <c>IGuiOptionsService.ExpandSystemPanels</c> is set, exactly as
        /// <c>StarSystemScreen.OnExpandCb</c> :736-745 does, so the flag and the frames can never
        /// disagree - a mismatch would make the player's own next press appear to do nothing.
        ///
        /// ON ENTRY ONLY. It runs on the frame the page arrives - once, because <see cref="_arrived"/>
        /// latches immediately after - so a player who collapses the panels while the page is up keeps
        /// them collapsed for as long as they stay. Leaving and coming back opens them again, which is
        /// what "on entry" means. The option's persistence carries that choice out of the session, so a
        /// player who never touches the panels simply always finds them open.
        /// </summary>
        private static void ExpandBottomPanels(StarSystemScreen window)
        {
            try
            {
                IGuiOptionsService options =
                    Amplitude.Unity.Framework.Services.GetService<IGuiOptionsService>();
                if (options == null || options.ExpandSystemPanels)
                {
                    return;
                }

                GuiFrameExpander[] expanders = window.GetComponentsInChildren<GuiFrameExpander>();
                for (int i = 0; i < expanders.Length; i++)
                {
                    if (expanders[i] != null)
                    {
                        expanders[i].ToggleExpansion();
                    }
                }

                options.ExpandSystemPanels = true;
            }
            catch (Exception e)
            {
                Log.Warn("system: opening the bottom panels on arrival threw: " + e);
            }
        }

        /// <summary>Whether the page has been seen bound and drawn since the view level was entered -
        /// see <see cref="IsActive"/>.</summary>
        private bool _arrived;

        /// <summary>The arrival check's own scratch lists, so asking whether the page is whole yet
        /// cannot disturb a build that is holding <c>_planets</c> and <c>_panels</c>.</summary>
        private readonly List<PlanetLabel_SystemManagement> _arriving =
            new List<PlanetLabel_SystemManagement>();

        private readonly List<SidePanel> _arrivingPanels = new List<SidePanel>();

        /// <summary>
        /// THE PAGE AS THE PLAYER CAN USE IT: the planet cards drawn AND the left edge's panels drawn.
        /// One question, asked by the arrival latch (<see cref="IsActive"/>) and again by every
        /// <see cref="Build"/>, because arriving and leaving are the same half-drawn page seen from
        /// two sides.
        ///
        /// WHY BUILD ASKS IT TOO (root cause of the entry landing bug, measured 2026-08-29). The page
        /// is torn down in pieces on the way OUT: leaving for the galaxy, the game hid the side panels
        /// and the three bottom panels a frame before it hid the cards, and the view level - which this
        /// screen deliberately follows through transitions, so a page turn survives - was still this
        /// one throughout. So for those frames the page was the focused screen and still declared
        /// something: the shared HUD, and briefly the cards. The navigator did what it always does with
        /// a cursor whose node has gone (<c>KeyGraph.Reconcile</c>, the nearest survivor walking the
        /// previous order backward) and re-seated it on the last HUD control left - the view-title's
        /// scan button - and THAT is what the screen then remembered. The next entry restored it
        /// faithfully, so every entry landed on the scan button no matter where the player had been.
        /// Traced frame by frame: cursor on <c>system:queue/header</c> at 118 nodes, on a planet card
        /// at 50, on <c>hud:view-title/scan</c> at 31, then the screen gone.
        ///
        /// A page that declares NOTHING cannot lose the cursor: <c>KeyGraph.Rerender</c> answers false
        /// for an empty render and returns before reconciling, so the remembered position is left
        /// exactly as the player left it. Hence the gate is on the whole build rather than on the
        /// cards alone, and the arrival latch asks the same question so that the page never becomes
        /// active in a state the build would refuse to declare.
        /// </summary>
        private bool Whole(List<PlanetLabel_SystemManagement> cards, List<SidePanel> panels)
        {
            Labels(cards);
            if (cards.Count == 0)
            {
                return false;
            }

            SidePanels.Drawn(panels);
            if (panels.Count == 0)
            {
                return false;
            }

            // AND SHOWING THE SYSTEM THIS SCREEN HAS ADOPTED, with no page turn still in flight. Both
            // are the same guard as above seen from the side: a page rebound to another system is as
            // half-drawn as one still arriving, and declaring it hands the navigator a render in which
            // the cursor's own node has gone. The adoption is <see cref="Turned"/>'s, made the frame
            // the window's node changes; the settle is the frames the cards then take to catch up.
            StarSystemScreen window = Window();
            if (
                _turnSettle > 0
                || (_showing != null && window != null && !ReferenceEquals(window.StarSystemNode, _showing))
            )
            {
                return false;
            }

            // AND A RESTORE STILL OWED KEEPS THE PAGE SHUT UNTIL IT HAS BEEN ASKED FOR. The two windows
            // rebind independently and the CARDS go first: measured 2026-08-29, a page turn hid the
            // planet labels a frame or more before <c>StarSystemScreen.StarSystemNode</c> changed - so
            // neither check above had fired yet - and kept them hidden for some fifty frames afterwards.
            // A render declared inside that window is a page without the card the cursor is on, which
            // is the one thing that moves a cursor nobody touched. Only a planet key can be lost this
            // way: every other stop here is keyed system-independently and comes through the swap
            // untouched, which is why they need no seat of their own.
            //
            // It lifts the moment the seat has been ASKED FOR (<see cref="Restore"/>), because a
            // landing needs a render to land in - and by then the request is already in flight, so the
            // rebuild that follows resolves to the asked-for control rather than to a survivor.
            //
            // Bounded, because a card can also go for an honest reason (a world lost, a colony
            // planted): the freeze buys the frames a turn needs and then gives up, so nothing can wedge
            // the page shut.
            if (_placeCard >= 0 && !_placeAsked && SwappedUnderCursor())
            {
                if (_swapGuard < SwapGuardFrames)
                {
                    _swapGuard++;
                    return false;
                }
            }
            else
            {
                _swapGuard = 0;
            }

            return true;
        }

        /// <summary>Whether the cursor is on a planet card the page has stopped drawing - the shape a
        /// page turn has before anything else on the page has changed.</summary>
        private bool SwappedUnderCursor()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null || !ReferenceEquals(navigator.Screen, this))
            {
                return false;
            }

            ControlId key = navigator.FocusedKey;
            string structural = key == null ? null : key.StructuralKey as string;
            return structural != null
                && structural.StartsWith(PlanetKeyPrefix)
                && CardOf(structural) < 0;
        }

        /// <summary>Which drawn card a planet key belongs to, counting from the left, or -1 where no
        /// card on the page is that planet's.</summary>
        private int CardOf(string structural)
        {
            int cut = structural.IndexOf('/', PlanetKeyPrefix.Length);
            string guid = cut < 0
                ? structural.Substring(PlanetKeyPrefix.Length)
                : structural.Substring(PlanetKeyPrefix.Length, cut - PlanetKeyPrefix.Length);
            for (int i = 0; i < _planets.Count; i++)
            {
                Planet planet = _planets[i].Planet;
                if (planet != null && planet.GUID.ToString() == guid)
                {
                    return i;
                }
            }

            return -1;
        }

        private int _swapGuard;

        /// <summary>How long the page waits for the cards to come back before it declares one without
        /// them. Measured 2026-08-29: a page turn hid the planet labels for some fifty frames, and the
        /// settle window covers thirty of those.</summary>
        private const int SwapGuardFrames = 90;

        /// <summary>
        /// A page turn is the textbook case: the game rebinds every panel on this page to another
        /// system, and what the rows say changes under them for as long as that takes. So nothing is
        /// announced until the cursor has been put back (<see cref="Restore"/>) - otherwise the player
        /// hears whichever row the rebuild happened to leave the cursor on before hearing the row they
        /// were actually standing on (measured 2026-08-29).
        ///
        /// The window is exactly the seat's: it closes the moment the seat lands or gives up, so an
        /// ordinary move on a settled page is never held.
        /// </summary>
        public override bool BetweenViews
        {
            get { return _turnSettle > 0 || _turnSeats > 0; }
        }

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
            Remember();
            Turned();
        }

        /// <summary>
        /// Where the player is standing, written down every frame the page is WHOLE - and nowhere else,
        /// so a cursor the teardown has moved is never what gets remembered (<see cref="Whole"/>).
        ///
        /// The navigator already remembers the KEY across a leave and a page turn
        /// (<c>GraphState</c>, kept because <see cref="KeepStateOnPop"/> is true), and for every stop
        /// on this page but one that key is system-independent - "system:queue/header" means the same
        /// row of whatever system is up - so its own reconcile puts the cursor back with nothing added
        /// here. THE PLANET CARDS ARE THE EXCEPTION: their keys carry the planet's GUID
        /// (<c>system:planet/536/population/2</c>), which no other system has, so the key alone cannot
        /// name "the same place" on the page the player turned to. What CAN is the card's POSITION
        /// among the drawn cards plus everything below it in the key, and the position can only be
        /// read while the old system's cards are still there - which is here.
        /// </summary>
        private void Remember()
        {
            // NOT WHILE A SEAT IS IN FLIGHT. Between the page turning and the cursor being put back,
            // the cursor is wherever the rebuild left it and none of it is the player's doing - writing
            // that down would throw away the very place the seat is on its way to restore (measured
            // 2026-08-29: a turn taken from a planet slot forgot the slot and landed on the new
            // system's governor).
            if (_turnSettle > 0 || _turnSeats > 0)
            {
                return;
            }

            // The seat window is shut, so the next one starts having asked for nothing. Kept here
            // rather than at the window's several endings, because this is the one place that runs
            // exactly when there is no seat in flight.
            _placeAsked = false;

            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null || !ReferenceEquals(navigator.Screen, this) || _planets.Count == 0)
            {
                return;
            }

            ControlId key = navigator.FocusedKey;
            string structural = key == null ? null : key.StructuralKey as string;
            if (structural == null)
            {
                return;
            }

            if (!structural.StartsWith(PlanetKeyPrefix))
            {
                _placeCard = -1;
                _placeSuffix = null;
                return;
            }

            // "system:planet/536/population/2" -> the card showing planet 536, and "/population/2".
            // A key belonging to no card on the page is a cursor the swap has outrun, and what was
            // written down is still true of where the player is standing, so it is KEPT - zeroing it
            // here is what lost the slot on every turn (measured 2026-08-29).
            int card = CardOf(structural);
            if (card >= 0)
            {
                int cut = structural.IndexOf('/', PlanetKeyPrefix.Length);
                _placeCard = card;
                _placeSuffix = cut < 0 ? string.Empty : structural.Substring(cut);
            }
        }

        private const string PlanetKeyPrefix = "system:planet/";

        /// <summary>Which card the cursor was on, counting from the left as the page draws them, and
        /// everything in its key below the planet's own GUID - the row of the card, or empty for the
        /// card itself. -1 while the cursor is anywhere but a planet card, which is every place whose
        /// key already means the same thing on the next system.</summary>
        private int _placeCard = -1;

        private string _placeSuffix;

        /// <summary>Whether the landing this arrival or page turn wants has already been asked for, so
        /// the retry window watches one request rather than re-arming it every frame.</summary>
        private bool _placeAsked;

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
        /// The page has been turned to another system: say which one, once, and put the cursor back
        /// where the player had it (<see cref="Restore"/>).
        ///
        /// The screen itself never leaves - the view level is re-entered with a new node and the mod's
        /// own gates ride that out (<see cref="IsActive"/>) - so nothing else would speak.
        ///
        /// The first system seen is adopted silently: that is the arrival, and the screen manager has
        /// already announced it. The arrival still opens the seat window, because a re-entry restores
        /// a position too and the position may belong to a system the player is no longer looking at.
        /// </summary>
        private void Turned()
        {
            StarSystemScreen window = Window();
            StarSystemNode node = window == null ? null : window.StarSystemNode;
            if (node != null && !ReferenceEquals(node, _showing))
            {
                bool arriving = _showing == null;
                _showing = node;
                // An arrival needs no settling - the page is whole before the screen is pushed at all -
                // while a turn redraws the page under a screen that never left.
                _turnSettle = arriving ? 0 : TurnSettleFrames;
                _turnSeats = TurnSeatFrames;
                _placeAsked = false;
                if (!arriving)
                {
                    Voice.Say(ScreenName, false);
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
                Restore();
            }
        }

        /// <summary>
        /// Put the cursor back where the player was, on the page they have just arrived on or turned to.
        ///
        /// Everything but a planet card is already back: its key means the same row of whatever system
        /// is up and the navigator's own reconcile has found it (<see cref="Remember"/>). What is left
        /// is the card the cursor was on, which is asked for BY POSITION - the same card counting from
        /// the left, the same row of it - and asked for through the ordinary landing request, so a row
        /// inside a collapsed card is reached by opening the card on the way, exactly as any other
        /// programmatic landing is.
        ///
        /// The fallbacks are the owner's, in order: the planets' own first row where the new system has
        /// no such card or no such row (an outpost with fewer worlds), and then
        /// <see cref="InitialFocusStop"/>. They are spent only when the seat window has run out, so a
        /// landing that is merely still travelling is never overtaken by its own fallback.
        /// </summary>
        private void Restore()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null || !ReferenceEquals(navigator.Screen, this))
            {
                return;
            }

            // Nothing per-system to put back: every other key means the same control on this page as it
            // did on the last one, and the navigator's own reconcile has already put the cursor there.
            if (_placeCard < 0 || _placeSuffix == null)
            {
                _turnSeats = 0;
                return;
            }

            // The cards are not drawn yet - the page both arrives and turns in pieces, and the planet
            // labels are the last piece by some fifty frames. The seat window is what waits for them;
            // giving up here would spend the fallback on a page that simply had not finished.
            if (_planets.Count == 0)
            {
                if (_turnSeats == 0)
                {
                    Fallback(navigator);
                }

                return;
            }

            // Fewer worlds here than where the player came from: no amount of waiting will grow the
            // system another card, so the fallback is taken at once.
            if (_placeCard >= _planets.Count)
            {
                Fallback(navigator);
                return;
            }

            Planet planet = _planets[_placeCard].Planet;
            if (planet != null)
            {
                ControlId target = ControlId.Structural(
                    PlanetKeyPrefix + planet.GUID + _placeSuffix
                );
                if (target.Equals(navigator.FocusedKey))
                {
                    _turnSeats = 0;
                    return;
                }

                if (!_placeAsked)
                {
                    _placeAsked = true;
                    navigator.FocusNode(target);
                    return;
                }

                // The card is open and the row the player was on is not in it - this system's world has
                // fewer slots. Nothing further will produce it, so the fallback is taken on the first
                // render that proves it rather than after the whole seat window has run out.
                GraphRender render = navigator.Render;
                ControlId card = ControlId.Structural(PlanetKeyPrefix + planet.GUID);
                if (
                    render != null
                    && render.Nodes.ContainsKey(card)
                    && !render.Nodes.ContainsKey(target)
                )
                {
                    Fallback(navigator);
                    return;
                }
            }

            if (_turnSeats == 0)
            {
                Fallback(navigator);
            }
        }

        /// <summary>The place the player was is not on this page: the planets' own first row, and the
        /// page's own landing stop where even that is not there.</summary>
        private void Fallback(GraphNavigator navigator)
        {
            _turnSeats = 0;
            if (!navigator.FocusStop(PlanetStop))
            {
                navigator.FocusStop(InitialFocusStop);
            }
        }

        public override void Build(GraphBuilder builder)
        {
            StarSystemScreen window = Window();
            if (window == null)
            {
                return;
            }

            // The page arrives in pieces and leaves in pieces, and a half-drawn page declares NOTHING
            // AT ALL - see <see cref="Whole"/>, which owns the reasoning for both ends.
            if (!Whole(_planets, _panels))
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

            // WHAT THE SYSTEM IS COMES BEFORE WHAT IS IN IT (owner design 2026-08-29): the left edge's
            // information panels, then the spaceport, then the cards. Tab does not wrap, so declaration
            // order is the order the player crosses the page in, and the panels that say whose system
            // this is and how it is getting on used to sit behind every planet card.
            BuildSidePanels(builder);

            builder.BeginStop(PlanetStop);
            builder.PushContext(ModStrings.Get(ModStrings.SystemPlanetsPanel));
            BuildPlanets(builder, window);
            builder.PopContext();

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

        // THE PANEL EXPAND BUTTON IS DELIBERATELY NOT DECLARED (owner ruling 2026-08-29). Each of the
        // three bottom panels draws a PanelExpandButton down its left edge and all three run one
        // handler (StarSystemScreen.OnExpandCb :736-745): it toggles every GuiFrameExpander under the
        // window and flips IGuiOptionsService.ExpandSystemPanels. What that DOES was measured
        // (docs/planets.md): the three frames go 177 to 292 and back and the lists SCROLL rather than
        // losing rows, so the accessible tree is byte-identical in both states. It changes how much a
        // sighted player sees at once and nothing a keyboard player can perceive, so it earns no node.
        // The coverage audit is told the same thing in one place, so a later run reports the reason
        // instead of re-raising it (CoverageAudit.DeliberatelyUnworked).

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
                // down, and what THIS page offers is the game's own target list: the other planet
                // cards, and the spaceport panel whenever it is drawn
                // (<see cref="PopulationMoves.OnSystemPage"/>). Asking only about a second colony -
                // what this was until 2026-08-29 - made the carry silent on every marker of a
                // one-colony system whose port the mouse could drag into.
                bool canCarry = PopulationMoves.OnSystemPage(window);
                OpenCardBeingSeated(builder);
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
        /// A seat on its way INTO a card opens that card on the same build, so the row it is aimed at
        /// exists in the very first render this page declares.
        ///
        /// Without it the landing is one frame late, and the frame it is late by is heard: the first
        /// render is a page whose planet keys have all changed, the navigator reconciles the cursor
        /// onto a survivor and the announcer reads that survivor out before the seat arrives (measured
        /// 2026-08-29 - a turn said the new system's name, a colony-panel row, and only then the slot
        /// the player was on). Opening the card here is what the landing's own ancestor walk would do a
        /// frame later, done while the page is still being declared.
        ///
        /// Through the builder's persistent expansion set, which is the engine's own bookkeeping and
        /// the documented way for a screen to flip it, so the card stays open exactly as one the player
        /// opened by hand.
        /// </summary>
        private void OpenCardBeingSeated(GraphBuilder builder)
        {
            if (
                _turnSeats <= 0
                || _placeCard < 0
                || _placeCard >= _planets.Count
                || string.IsNullOrEmpty(_placeSuffix)
                || builder.Expansion == null
            )
            {
                return;
            }

            Planet planet = _planets[_placeCard].Planet;
            if (planet != null)
            {
                builder.Expansion.Add(ControlId.Structural(PlanetKeyPrefix + planet.GUID));
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
        /// (<see cref="AddPopulationSlots"/>), and people are moved by CARRYING them (the carry key on
        /// a slot picks up what the game's own drag would take from that marker, the activation key on
        /// another card or the spaceport puts them down) rather than by a menu entry per unit and
        /// destination, which is the same gesture a ship gets in the fleet panel and the same drag the
        /// mouse has here. The drop lives on the SLOTS and not on the card: an empty one is the plain
        /// add, an occupied one the game's swap.
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
                    GraphNodes.TooltipSection(AgeWidgets.Raw(status)),
                    NodeSection.Buffer(() => PlanetDetails(it))
                ),
                OnActivate = () => GalaxyViewLevels.OpenPlanet(it.Planet),
            };

            // THE CARD ITSELF TAKES NO DROP (owner ruling 2026-08-29). The game's mouse accepts one
            // anywhere on the card's rectangle, but a keyboard player is walking rows, and a card
            // header that also swallowed drops made two rows out of one gesture: the header and the
            // free slot under it both said "drop target" and did different things. So the drop lives
            // on the SLOTS alone (<see cref="AddPopulationSlot"/>) - an empty one is the plain add,
            // an occupied one the swap - which reaches every outcome the mouse reaches and says where
            // the people are going. A full planet then offers only its swaps, and a planet with room
            // offers its free places.
            AgeWidgets.PointAt(vtable, status ?? label.AgeTransform);

            string key = "system:planet/" + planet.GUID;
            ControlId id = ControlId.For(planet, key);
            List<CardActions.CardAction> rename = new List<CardActions.CardAction>(1);
            CardActions.AddNamedByMod(rename, label.PlanetRenameButton, ModStrings.SystemRenamePlanet);
            List<CardActions.CardAction> buttons = PlanetButtons(label);
            List<CardActions.CardAction> outpost = OutpostActions(label);
            List<Population> units = new List<Population>(4);
            Ring ring = PlanetRing(label, key);
            List<PopulationSlots.Slot> slots = RingSlots(ring, units);
            List<TooltipChildren.Dossier> dossiers = PlanetDossiers(label);
            // Flow control: whether the card is a leaf or a group. A card whose ONLY content is a
            // Sanctuary band would otherwise be declared as a leaf and the band never walked into.
            bool ghost = AgeWidgets.Visible(label.GhostGroup);
            if (
                rename.Count == 0
                && buttons.Count == 0
                && outpost.Count == 0
                && slots.Count == 0
                && dossiers.Count == 0
                && !ghost
            )
            {
                // Synthetic: the card stands for the PLANET, and the walk over the drawn planet
                // labels is what says the system is showing it.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            vtable.ControlType = ControlTypes.Group;
            // Synthetic for the same reason as the leaf above.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                // Down the card, in the order it is drawn: the rename button beside the title, the
                // population ring in the middle, the action buttons along the bottom - and then, as a
                // region of their own, the dossiers the card draws no words for at all.
                object outer = TooltipChildren.Actions(builder, key);
                CardActions.Emit(builder, key + "/name", rename);
                AddPopulationSlots(builder, ring, units, slots, canCarry);
                CardActions.Emit(builder, key, buttons);
                CardActions.Emit(builder, key + "/outpost", outpost);
                AddGhost(builder, key, label, canCarry);
                TooltipChildren.Emit(builder, key, dossiers, outer);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// The SANCTUARY band the card grows along its bottom when a ghost colony is sitting on this
        /// world (<c>PlanetLabel_SystemManagement.RefreshGhostStatus</c> :1192-1250), read in the order
        /// the game draws it: the band's own title, then the Sanctuary's population ring, then the
        /// button that turns one of its people into a sleeper.
        ///
        /// The band is drawn for a RIVAL's Sanctuary too - the group's only test is that the ghost
        /// exists and that the player can see its system (:1194) - and what a rival's draws is the
        /// title and the population figure alone: the game hides the ring, the outputs and the button
        /// for anybody else's (:1217, :1229). So everything below is gated on its own drawn flag and a
        /// rival's band simply reads shorter, with no ownership test written here.
        ///
        /// The title is the band's line and carries the figures the band draws no words for: the
        /// population count, which the game writes as a bare "3/5" beside a symbol, and the five
        /// outputs, which are the same strip of pips the card reads for the world itself
        /// (<see cref="PlanetOutputs"/>). Its own tooltip is what the game says about the Sanctuary -
        /// whose it is, and, for a rival's, how it could be got rid of.
        ///
        /// THE SANCTUARY'S RING IS HOVER-ONLY (measured 2026-08-29): the game shows
        /// <c>GhostPopulationEnumeratorFocused</c> and the outputs strip while the pointer is inside
        /// the band's own rectangle and hides them again on the way out (:648-693), and unlike the
        /// world's ring there is no simple one drawn underneath. So the slots exist exactly while the
        /// game draws them, which is the rule every other row here follows - and it works out, because
        /// landing on the band's title is what puts the pointer inside the band
        /// (<see cref="AgeWidgets.PointAt"/>), so the ring is there by the time the player steps down
        /// into it, exactly as it is there for a mouse that has hovered the band.
        ///
        /// CONTENT IS UNVERIFIED (<c>docs/planets.md</c>): a Sanctuary needs a player empire that HAS
        /// ghost systems - the Umbral Choir, a Penumbra faction chosen at new-game time - and no save
        /// in this repo is one, so the band was measured by lending the card a colony and showing the
        /// group. What that proves is the STRUCTURE - which widgets are declared, what the
        /// reader makes of each - and never what a real ghost would say in them.
        /// </summary>
        private static void AddGhost(
            GraphBuilder builder,
            string key,
            PlanetLabel_SystemManagement label,
            bool canCarry
        )
        {
            try
            {
                // Flow control: whether the band is walked at all. The group is a wired prefab field
                // and so always there; what says a Sanctuary exists is the game drawing it.
                if (!AgeWidgets.Visible(label.GhostGroup))
                {
                    return;
                }

                PlanetLabel_SystemManagement it = label;
                AgeTransform title =
                    label.GhostTitle == null ? null : label.GhostTitle.AgeTransform;
                if (title != null)
                {
                    NodeVtable vtable = GraphNodes.Readout(
                        () => AgeText.Label(it.GhostTitle),
                        null,
                        () => GhostDetails(it),
                        AgeWidgets.Raw(title)
                    );
                    AgeWidgets.PointAt(vtable, title);
                    builder.AddItem(
                        Nodes.Drawn(ControlId.For(title, key + "/ghost"), vtable, title)
                    );
                }

                Ring ring = GhostRing(label, key);
                List<Population> units = new List<Population>(4);
                List<PopulationSlots.Slot> slots = RingSlots(ring, units);
                AddPopulationSlots(builder, ring, units, slots, canCarry);

                // The one thing the band can DO, and a standard refusable card action: the game keeps
                // it drawn and switched off with its reason written into its own tooltip by the game's
                // own failure formatter (:1229-1249), so it is declared while drawn and offered while
                // the game offers it, named by the sentence that explains it.
                List<CardActions.CardAction> traitor = new List<CardActions.CardAction>(1);
                CardActions.AddRefusable(
                    traitor,
                    label.TraitorButton,
                    // Named by the SENTENCE its tooltip explains it with, not by a title: the game
                    // hangs plain content there with no wrapper and no header line, so asking for a
                    // title answered nothing and the row announced itself role-first ("button, Click
                    // to consume one population..."). Measured on the lent band, 2026-08-29 - the
                    // same treatment the card's own wordless buttons get.
                    CardActions.NameFromTooltip(label.TraitorButton)
                );
                CardActions.Emit(builder, key + "/ghost", traitor);
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a planet card's Sanctuary band threw: " + e);
            }
        }

        /// <summary>The figures the Sanctuary band draws with no words of its own: how many people live
        /// there out of how many could, and the five outputs the ghost colony is making. The count the
        /// game writes as "3/5" beside a symbol, so it is composed as the fraction it is with the
        /// game's own word for the symbol; the outputs are read exactly as the card's own strip is
        /// (<see cref="AddFidsi"/>).</summary>
        private static IList<string> GhostDetails(PlanetLabel_SystemManagement label)
        {
            List<string> lines = new List<string>(6);
            try
            {
                ColonizedPlanet ghost = label.GhostColonizedPlanet;
                if (ghost == null)
                {
                    return lines;
                }

                AgePrimitiveLabel count = label.GhostPopulationCount;
                // Content, and of a DIFFERENT widget than the node stands on: these are lines of the
                // title's buffer, so the gate never sees them and nothing else would stop a rival's
                // hidden figure being read out.
                if (count != null && AgeWidgets.Visible(count.AgeTransform))
                {
                    AddLine(
                        lines,
                        ModStrings.Format(
                            ModStrings.FractionUnit,
                            ghost.PopulationCount,
                            ghost.MaxPopulation,
                            AgeText.Clean(PopulationIcon)
                        )
                    );
                }

                // The outputs strip is HOVER-ONLY: the game shows it while the pointer is inside the
                // band and hides it again on the way out (:669-693), like the card's own detailed
                // ring. So its own drawn flag is not the question - these are BUFFER lines, which is
                // what hover-revealed content gets - and the question is whether the game ever
                // COMPUTED them, which it does for a Sanctuary of the player's own and for nobody
                // else (:1216-1222 refreshes the enumerator only there). A rival's strip keeps
                // whatever it was last bound with, so reading it would be a made-up figure.
                FidsiEnumerator fidsi = label.GhostFidsiEnumerator;
                if (fidsi == null || fidsi.FidsiProperties == null || OwnGhost(label) == null)
                {
                    return lines;
                }

                Amplitude.Unity.Simulation.SimulationObject simulation = ghost.SimulationObject;
                if (simulation == null)
                {
                    return lines;
                }

                IList<string> numbers = PlanetOutputs.Numbers(simulation, fidsi);
                for (int i = 0; i < numbers.Count; i++)
                {
                    lines.Add(numbers[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading a Sanctuary's figures threw: " + e);
            }

            return lines;
        }

        /// <summary>The symbol the game ends its Sanctuary population count with, which is the only
        /// word it writes for that figure.</summary>
        private const string PopulationIcon = "[population]";

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
                // Content: which dossiers the card offers. These become a region of the card's own
                // node, not nodes the gate ever sees.
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
        /// their tooltip, so each item is asked the gate's own drawing test at ADMISSION - the one
        /// place early enough to stop a ghost winning the dedupe.
        /// </summary>
        private static void AddDepositDossiers(
            List<TooltipChildren.Dossier> found,
            PlanetLabel_SystemManagement label
        )
        {
            AgeTransform group = label.ResourceDepositsGroup;
            // Content: whether the deposits contribute dossiers at all - they become a region of the
            // card's node rather than nodes of their own.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            IList<AgeTransform> children = group.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                // Admission, not the gate: the collector DEDUPES by tooltip
                // (<see cref="TooltipChildren.Add"/>), so a retired row still holding the previous
                // binding's deposit would swallow the drawn row that shares it, and the gate - which
                // only ever sees finished nodes - would then drop the one node the pair had left. The
                // gate's OWN test is asked, under the same flag, rather than a second opinion; the
                // shared door cannot ask it for every caller (<see cref="TooltipChildren.Admitted"/>).
                AgeTransform child = children[i];
                if (child == null || !NodeGate.StillDrawn(child))
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
        /// The table pools its items, so admission is what keeps a retired row out of the numbering.
        /// </summary>
        private static void AddAnomalyHints(
            List<CardActions.CardAction> found,
            PlanetLabel_SystemManagement label
        )
        {
            IList<AgeTransform> items = AgeWidgets.DrawnChildren(label.PlanetAnomaliesTable);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform row = items[i];
                PlanetAnomalyItem item = row == null ? null : row.GetComponent<PlanetAnomalyItem>();
                if (item == null || item.HintButton == null)
                {
                    continue;
                }

                PlanetAnomalyItem it = item;
                AgeTransform hint = item.HintButton.AgeTransform;
                // Through the collector's admission filter like every other entry: this list is
                // NUMBERED, and the table below is pooled, so a hand-built row cannot be allowed to
                // skip the one test that keeps a retired one out of the count.
                CardActions.Add(
                    found,
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
        /// Admission is the gate, as on the anomalies table above: this table is pooled too
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
            IList<AgeTransform> items = AgeWidgets.DrawnChildren(label.PlanetCuriositiesTable);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                // Drawn-ness is the collector's question, not this walk's: a curiosity the pool has
                // retired never enters the numbered list (<see cref="CardActions.AddRefusable"/>).
                AgeTransform item = items[i];
                if (item != null && SkipCuriosities(item))
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
        /// The strip is POOLED (<c>RefreshOutpostActions</c> :988 <c>ReserveChildren</c>), so a tick is
        /// admitted on the drawing test rather than on the visibility flag a retired row keeps: an
        /// outpost offering fewer actions than the one read before it would otherwise declare the
        /// surplus ticks, still wearing the other outpost's name - and renumber the real ones.
        /// </summary>
        private static List<CardActions.CardAction> OutpostActions(
            PlanetLabel_SystemManagement label
        )
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(4);
            try
            {
                // Flow control: whether the outpost's action list is collected at all - the actions
                // below are NUMBERED by their place in it.
                if (label.OutpostGroup == null || !AgeWidgets.Visible(label.OutpostGroup))
                {
                    return found;
                }

                AgeTransform table = label.OutpostActionsTable;
                IList<AgeTransform> items = table == null ? null : table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    // A row the pool has retired - faded as a ROW while its tick stays at alpha 1 -
                    // is dropped by the collector's own admission filter, which walks the tick's
                    // ancestry and so sees the faded row above it (CardActions.AddToggle).
                    OutpostActionItem item =
                        items[i] == null ? null : items[i].GetComponent<OutpostActionItem>();
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
            // Content: which lines the card is read with. Lines, not nodes - nothing here is declared.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            IList<AgeTransform> children = group.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child == null)
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
            // Content: whether the depletion state is one of the card's lines.
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
            // Content: whether the outpost's progress is among the card's lines - a colonized system
            // draws none of it.
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
        public const string PopulationKind = PopulationMoves.Kind;

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
        private static List<PopulationSlots.Slot> RingSlots(Ring ring, List<Population> units)
        {
            return PopulationMoves.Slots(
                ring.Card == null ? null : ring.Card.Planet,
                ring.Colony,
                DrawnMarkers(ring),
                units
            );
        }

        /// <summary>
        /// WHICH population ring a row is being read from. A planet card draws up to two of them - the
        /// world's own, and the Sanctuary's when a ghost colony is sitting on the same world
        /// (<c>PlanetLabel_SystemManagement.RefreshGhostStatus</c> :1192-1250) - and the game runs both
        /// through the SAME drag machinery: the same client, the same target list
        /// (<c>PlanetLabelsWindow_SystemManagement.GetPopulationDragDropTargets</c> :72 asks both
        /// enumerators), the same order. So the rows are built once and told which ring they are on.
        ///
        /// <see cref="Markers"/> is the enumerator the game is DRAWING (a card swaps between a simple
        /// ring and a detailed one) and decides the slot geometry; <see cref="Target"/> is the one whose
        /// own <c>CanAcceptPopulationDrop</c> answers a drop, which for a planet is always the focused
        /// ring whichever is drawn. <see cref="Colony"/> is whose people fill it - possibly another
        /// empire's, which reads and neither carries nor takes - and <see cref="Destination"/> is the
        /// colony a drop would land on, null wherever the game moves nobody.
        /// </summary>
        private sealed class Ring
        {
            public PlanetLabel_SystemManagement Card;
            public PlanetPopulationEnumerator Markers;
            public PlanetPopulationEnumerator Target;
            public ColonizedPlanet Colony;
            public ColonizedPlanet Destination;
            public string Key;
            public string Scratch;
        }

        /// <summary>The card's own ring - the world's population, exactly as it read before the
        /// Sanctuary band existed.</summary>
        private static Ring PlanetRing(PlanetLabel_SystemManagement label, string key)
        {
            return new Ring
            {
                Card = label,
                Markers = DrawnEnumerator(label),
                Target = label == null ? null : label.PlanetPopulationEnumeratorFocused,
                Colony = Colony(label),
                Destination = Settled(label),
                Key = key + "/population",
                Scratch = string.Empty,
            };
        }

        /// <summary>The Sanctuary's ring, which the game draws only for a ghost colony of the PLAYER's
        /// (<c>RefreshGhostStatus</c> :1217 hides the whole group for anybody else's) and binds through
        /// the card's own drag client (:375).</summary>
        private static Ring GhostRing(PlanetLabel_SystemManagement label, string key)
        {
            ColonizedPlanet ghost = OwnGhost(label);
            return new Ring
            {
                Card = label,
                Markers = label == null ? null : label.GhostPopulationEnumeratorFocused,
                Target = label == null ? null : label.GhostPopulationEnumeratorFocused,
                Colony = ghost,
                Destination = ghost,
                Key = key + "/ghost/population",
                Scratch = "ghost/",
            };
        }

        /// <summary>The Sanctuary sitting on this world when it is the PLAYER's, or null - a rival's
        /// Sanctuary draws its title and its population figure and nothing that can be worked.</summary>
        private static ColonizedPlanet OwnGhost(PlanetLabel_SystemManagement label)
        {
            try
            {
                ColonizedPlanet ghost = label == null ? null : label.GhostColonizedPlanet;
                return ghost != null && ghost.Empire == Gui.PlayerEmpire ? ghost : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How many markers the ring the game is DRAWING is showing. The container keeps its
        /// retired markers as invisible children (<c>PopulationEnumerator.HideAllPopulationMarkers</c>
        /// pools them without unparenting), so the visible ones are the ring - and they are in slot
        /// order, because the enumerator sets each one's sibling index to its own slot and sorts.
        /// </summary>
        private static int DrawnMarkers(Ring ring)
        {
            AgeTransform container = MarkerContainer(ring);
            IList<AgeTransform> markers = container == null ? null : container.Children;
            int drawn = 0;
            for (int i = 0; markers != null && i < markers.Count; i++)
            {
                if (AgeWidgets.DrawnChild(markers, i) != null)
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
        private static AgeTransform DrawnMarker(Ring ring, int index)
        {
            AgeTransform container = MarkerContainer(ring);
            IList<AgeTransform> markers = container == null ? null : container.Children;
            int seen = 0;
            for (int i = 0; markers != null && i < markers.Count; i++)
            {
                AgeTransform marker = AgeWidgets.DrawnChild(markers, i);
                if (marker == null)
                {
                    continue;
                }

                if (seen == index)
                {
                    return marker;
                }

                seen++;
            }

            return null;
        }

        /// <summary>The markers of the ring the row is being read from, or null where the game is not
        /// drawing that ring at all.</summary>
        private static AgeTransform MarkerContainer(Ring ring)
        {
            PlanetPopulationEnumerator drawn = ring == null ? null : ring.Markers;
            return drawn == null || !drawn.Shown ? null : drawn.PopMarkersContainer;
        }

        /// <summary>Whichever of the world's two rings the card is drawing - it keeps a simple one for
        /// the ordinary view and a detailed one it swaps in under a mouse.</summary>
        private static PlanetPopulationEnumerator DrawnEnumerator(
            PlanetLabel_SystemManagement label
        )
        {
            if (label == null)
            {
                return null;
            }

            return label.PlanetPopulationEnumeratorSimple != null
                && label.PlanetPopulationEnumeratorSimple.Shown
                ? label.PlanetPopulationEnumeratorSimple
                : label.PlanetPopulationEnumeratorFocused;
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
        /// nobody else's population. One press carries what the GAME's own drag would carry from that
        /// marker: itself and every marker of the same people after it round the ring
        /// (<see cref="PopulationMoves.Carried"/>), so the first Imperial of five carries five and the
        /// last carries one. What is carried is captured then, because the row is rebuilt every frame
        /// and those people may have left the planet by the time it is dropped.
        ///
        /// A FILLED slot is also a drop target, and dropping onto it is the game's SWAP: the affinity
        /// standing there is named as the one to send back the other way, which is what the mouse sets
        /// by hovering a marker mid-drag (<c>PopulationEnumerator</c> :275). A plain add is the CARD's
        /// own drop, one level up.
        /// </summary>
        private static void AddPopulationSlots(
            GraphBuilder builder,
            Ring ring,
            List<Population> units,
            List<PopulationSlots.Slot> slots,
            bool canCarry
        )
        {
            if (slots.Count == 0)
            {
                return;
            }

            string keyPrefix = ring.Key;
            bool carry = canCarry && ring.Destination != null;
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
                        builder.SetRegion(keyPrefix + "/" + band);
                        builder.PushContext(BandName(band));
                    }

                    AddPopulationSlot(builder, ring, units, slot, total, carry);
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
            Ring ring,
            List<Population> units,
            PopulationSlots.Slot slot,
            int total,
            bool canCarry
        )
        {
            PlanetLabel_SystemManagement label = ring.Card;
            ColonizedPlanet colony = ring.Colony;
            Population unit = slot.Unit >= 0 && slot.Unit < units.Count ? units[slot.Unit] : null;
            string key = ring.Key + "/" + slot.Rank;
            int rank = slot.Rank;
            int outOf = total;
            bool empty = unit == null && slot.Kind != PopulationSlots.Band.Locked;
            // An UNSETTLED world's ring is all one band of empty slots
            // (<see cref="PopulationSlots.BuildUnsettled"/>), so the row's position in its region is
            // already its rank and saying it again in the label made every row read "Empty slot 1 of
            // 6, 1 of 6". A COLONIZED card keeps the numbered phrase: there the ring is split into
            // bands, so a row's position within its band is not its rank round the ring.
            bool vacant = colony == null && empty;
            AgeTooltip carrier = SlotCarrier(ring, slot, unit);
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
                    GraphNodes.TooltipSection(carrier)
                ),
            };

            if (carrier != null)
            {
                AgeWidgets.PointAt(vtable, carrier.AgeTransform);
            }

            // EVERY slot of the player's own ring that is not LOCKED takes a drop, and which KIND of
            // drop it is is who is standing in it: an occupied slot is the game's swap (the affinity
            // there is the one sent back the other way), an empty one is the plain add the card
            // itself takes. The empty rows are not a nicety - a player carrying somebody walks the
            // ring looking for a free place to put them, so the free place is where the drop must be;
            // offering it only on the card's header was a gap the owner met at once (2026-08-29).
            // The game's add is not per-slot either way: the server places the people, and the row is
            // only where the gesture is aimed.
            if (ring.Destination != null && slot.Kind != PopulationSlots.Band.Locked)
            {
                if (canCarry && colony != null && unit != null)
                {
                    ColonizedPlanet source = colony;
                    Population held = unit;
                    int carried = PopulationMoves.Carried(units, slot.Unit);
                    vtable.OnPickUp = () => PopulationMoves.Pick(source, held, carried);
                }

                Ring on = ring;
                StaticString replaced = unit == null ? StaticString.Empty : unit.Affinity;
                vtable.DropKind = PopulationKind;
                vtable.DropAccepts = cargo => AcceptsPopulation(on, cargo);
                vtable.OnDrop = cargo => DropPopulation(on, cargo, replaced);
            }

            List<TooltipChildren.Dossier> nested = SlotDossiers(ring, slot, unit);
            if (nested.Count == 0)
            {
                // Synthetic: a slot is read out of the colony's own model - the ring draws a marker
                // per unit and nothing per empty slot - so the enumeration is the honesty here.
                builder.AddItem(Nodes.Synthetic(ControlId.Structural(key), vtable));
                return;
            }

            ControlId id = ControlId.Structural(key);
            vtable.ControlType = ControlTypes.Group;
            // Synthetic for the same reason as the leaf above.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
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
            Ring ring,
            PopulationSlots.Slot slot,
            Population unit
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(1);
            if (unit == null || slot.Kind != PopulationSlots.Band.Overpopulation)
            {
                return found;
            }

            AgeTooltip carrier = OverpopulationCarrier(ring, slot.Rank);
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
            Ring ring,
            PopulationSlots.Slot slot,
            Population unit
        )
        {
            if (unit != null)
            {
                return PopulationCarrier(ring, slot.Rank, unit);
            }

            if (slot.Kind == PopulationSlots.Band.Locked)
            {
                return LockedCarrier(ring, slot.Rank);
            }

            return slot.Kind == PopulationSlots.Band.Overpopulation
                ? OverpopulationCarrier(ring, slot.Rank)
                : null;
        }

        /// <summary>A carrier bound exactly as <c>PopulationMarker.Bind</c> binds the game's own
        /// detailed marker - the same class, the same wrapper, the same context - so the tooltip
        /// window assembles the population's own dossier for a ring that is drawing no tooltips.
        /// </summary>
        private static AgeTooltip PopulationCarrier(Ring ring, int rank, Population unit)
        {
            try
            {
                ColonizedPlanet colony = ring.Colony;
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    SlotKey(ring, rank),
                    SlotStamp(colony, (string)unit.Affinity, unit.Count),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiPopulation wrapper = PopulationMoves.Wrap(colony.Empire, unit);
                    carrier.Class = "Population";
                    carrier.Content = wrapper.Title;
                    carrier.Target = wrapper;
                    carrier.Context = wrapper.EmpirePopulationSimulationObject;
                }

                Park(carrier, ring, rank);
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
        private static AgeTooltip OverpopulationCarrier(Ring ring, int rank)
        {
            try
            {
                ColonizedPlanet colony = ring.Colony;
                int covered = colony.MaxPopulation - colony.MaxPopulationUnderOverPopulation;
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    SlotKey(ring, rank) + "/overpopulation",
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

                Park(carrier, ring, rank);
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
        private static AgeTooltip LockedCarrier(Ring ring, int rank)
        {
            try
            {
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    SlotKey(ring, rank) + "/locked",
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

                Park(carrier, ring, rank);
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
        private static void Park(AgeTooltip carrier, Ring ring, int rank)
        {
            AgeTransform marker = DrawnMarker(ring, rank - 1);
            if (marker != null)
            {
                ScratchTooltips.PlaceOver(carrier, marker);
            }
        }

        /// <summary>A carrier's own key. It carries the RING as well as the world, because a card
        /// drawing a Sanctuary draws two rings over the same planet and one key for both would hand the
        /// Sanctuary's slot 1 whatever the world's slot 1 was last bound with.</summary>
        private static string SlotKey(Ring ring, int rank)
        {
            return "population-slot/" + ring.Card.Planet.GUID + "/" + ring.Scratch + rank;
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
            return PopulationMoves.BandName(band);
        }

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

        /// <summary>
        /// Whether this card's ring would take what is being carried, right now - the game's own
        /// answer (<see cref="PopulationMoves.Accepts"/>), which is what every population drop target
        /// on this page advertises itself by.
        ///
        /// With one thing added that the game's own check cannot know: a unit coming OUT of the
        /// SPACEPORT travels by a different route from a unit coming off another planet. The
        /// spaceport's client posts a single order that clamps against the destination's own room and
        /// never swaps (<c>SpaceportSidePanel.ApplyDrop</c> :70-80), while
        /// <c>CanWelcomeSomeOfPopulation</c> accepts a FULL planet on the strength of a swap being
        /// possible - so a full planet would advertise itself to a port-sourced carry and then move
        /// nobody. The gate has to agree with the outcome, so the room is asked here for that route
        /// alone; a planet-sourced drop keeps the game's answer untouched, because there the whole
        /// carry really does move (the surplus is swapped back).
        /// </summary>
        private static bool AcceptsPopulation(Ring ring, CarryItem held)
        {
            Population population = held == null ? null : held.Cargo as Population;
            ColonizedPlanet destination = ring.Destination;
            if (
                population == null
                || destination == null
                || !PopulationMoves.Accepts(ring.Target, population, held.Quantity)
            )
            {
                return false;
            }

            return PopulationMoves.PlanetOf(population) != null
                || PopulationMoves.OntoPlanet(destination, held.Quantity) > 0;
        }

        /// <summary>
        /// The spaceport's population markers are claimed by the panel's own reader
        /// (<see cref="BuildSpaceport"/>) rather than read as ordinary cells: they are a RING like a
        /// planet card's, with bands and slots and a carry, and the flat cell walk has no way to say
        /// any of that. Claiming the enumerator here is what stops the walk descending into a row of
        /// wordless slots and declaring them twice.
        /// </summary>
        private static bool SpaceportPopulations(AgeTransform widget, SpaceportSidePanel panel)
        {
            PopulationEnumerator markers = panel == null
                ? null
                : panel.SpaceportPopulationEnumerator;
            return markers != null && ReferenceEquals(widget, markers.AgeTransform);
        }

        /// <summary>
        /// The spaceport panel: its readouts in drawn order with its population RING opened up in the
        /// middle of them, the way a planet card's ring is.
        ///
        /// The port is the OTHER place a system keeps population, and until 2026-08-29 it was read as
        /// one row per affinity with the count said - which answered who was waiting and said nothing
        /// about how much room there was, the very question the row of slots is drawn to answer, and
        /// left the empty slots (the only things a unit can be dropped INTO) undeclared entirely. So it
        /// is now modelled exactly like the planet ring: a row per drawn marker, in slot order, banded
        /// into regions named with the game's own words.
        ///
        /// The panel's other controls keep their places around it, which is why the cells are split by
        /// where the ring is drawn rather than emitted before or after it wholesale.
        /// </summary>
        private void BuildSpaceport(
            GraphBuilder builder,
            SpaceportSidePanel panel,
            string keyPrefix
        )
        {
            _cells.Clear();
            SidePanels.Readouts(_cells, panel, keyPrefix, SpecialCell, Transparent);
            List<Cell> above = new List<Cell>(_cells.Count);
            List<Cell> below = new List<Cell>(_cells.Count);
            AgeTransform ring = panel.SpaceportPopulationEnumerator == null
                ? null
                : panel.SpaceportPopulationEnumerator.AgeTransform;
            float at = ring == null ? float.MaxValue : ring.GetGlobalPosition().y;
            for (int i = 0; i < _cells.Count; i++)
            {
                Cell cell = _cells[i];
                (cell.Widget.GetGlobalPosition().y < at ? above : below).Add(cell);
            }

            Cells.EmitLinear(builder, above);
            AddSpaceportSlots(builder, keyPrefix, panel);
            Cells.EmitLinear(builder, below);
        }

        /// <summary>
        /// A row per slot of the spaceport's ring, in the order the game draws them, banded into
        /// regions exactly as a planet card's slots are - the filled and empty places under the game's
        /// own population title, the ones the system's level has not paid for yet under its own word
        /// for a locked slot.
        ///
        /// Each row is the marker the game drew, so it carries THAT marker's own tooltip - the panel
        /// writes one of three sentences onto every marker each refresh, the locked one carrying the
        /// capacity the next system level would buy (<c>SpaceportSidePanel.Refresh</c> :152-186). A
        /// marker the panel has not refreshed yet still holds the prefab's placeholder, so only the
        /// three sentences the panel writes are accepted and anything else is treated as no tooltip at
        /// all.
        ///
        /// A FILLED slot can be picked up from - carrying what the game's own drag would carry from
        /// that marker - and is a swap target. An EMPTY one is a plain drop target. A LOCKED one is
        /// neither: it is a place that does not exist yet, and the game says so in its tooltip.
        /// </summary>
        private static void AddSpaceportSlots(
            GraphBuilder builder,
            string keyPrefix,
            SpaceportSidePanel panel
        )
        {
            Spaceport port = panel.Spaceport;
            PopulationEnumerator ring = panel.SpaceportPopulationEnumerator;
            if (port == null || ring == null)
            {
                return;
            }

            List<AgeTransform> drawn = new List<AgeTransform>(4);
            List<Population> units = new List<Population>(4);
            IList<AgeTransform> children = ring.AgeTransform.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform marker = AgeWidgets.DrawnChild(children, i);
                if (marker == null)
                {
                    continue;
                }

                drawn.Add(marker);
                PopulationMarker it = marker.GetComponent<PopulationMarker>();
                units.Add(
                    it == null || it.GuiPopulation == null ? null : it.GuiPopulation.Population
                );
            }

            object outer = builder.Region;
            List<Cell> band = new List<Cell>(drawn.Count);
            bool locked = false;
            bool inBand = false;
            try
            {
                for (int i = 0; i < drawn.Count; i++)
                {
                    PopulationMarker it = drawn[i].GetComponent<PopulationMarker>();
                    bool shut = it != null && it.Locked;
                    if (inBand && shut != locked)
                    {
                        Cells.EmitLinear(builder, band);
                        band.Clear();
                        builder.PopContext();
                        inBand = false;
                    }

                    if (!inBand)
                    {
                        locked = shut;
                        inBand = true;
                        builder.SetRegion(
                            keyPrefix
                                + "spaceport/population/"
                                + (locked
                                    ? PopulationSlots.Band.Locked
                                    : PopulationSlots.Band.Population)
                        );
                        builder.PushContext(
                            BandName(
                                locked
                                    ? PopulationSlots.Band.Locked
                                    : PopulationSlots.Band.Population
                            )
                        );
                    }

                    band.Add(
                        SpaceportSlot(keyPrefix, panel, port, drawn, units, i)
                    );
                }
            }
            finally
            {
                if (inBand)
                {
                    Cells.EmitLinear(builder, band);
                    builder.PopContext();
                }

                builder.SetRegion(outer);
            }
        }

        /// <summary>One slot of the spaceport's ring.</summary>
        private static Cell SpaceportSlot(
            string keyPrefix,
            SpaceportSidePanel panel,
            Spaceport port,
            List<AgeTransform> drawn,
            List<Population> units,
            int index
        )
        {
            AgeTransform marker = drawn[index];
            PopulationMarker it = marker.GetComponent<PopulationMarker>();
            Population unit = units[index];
            bool locked = it != null && it.Locked;
            bool empty = unit == null && !locked;
            int rank = index + 1;
            int outOf = drawn.Count;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () =>
                            ModStrings.Format(
                                empty
                                    ? ModStrings.SystemPopulationSlotEmpty
                                    : ModStrings.SystemPopulationSlot,
                                rank,
                                outOf
                            )
                    ),
                    GraphNodes.ValuePart(() => unit == null ? null : PopulationName(unit)),
                },
                Sections = GraphNodes.Sections(GraphNodes.TooltipSection(SlotSentence(marker))),
            };
            AgeWidgets.PointAt(vtable, marker);

            if (unit != null)
            {
                Population held = unit;
                int carried = PopulationMoves.Carried(units, index);
                vtable.OnPickUp = () => PopulationMoves.Pick(port, held, carried);
            }

            if (!locked)
            {
                SpaceportSidePanel host = panel;
                StaticString replaced = unit == null ? StaticString.Empty : unit.Affinity;
                vtable.DropKind = PopulationKind;
                vtable.DropAccepts = cargo => PortTakes(host, cargo, unit != null);
                vtable.OnDrop = cargo => DropIntoSpaceport(host, cargo, replaced);
            }

            return new Cell
            {
                Widget = marker,
                Id = ControlId.Structural(keyPrefix + "spaceport/population/" + index),
                Vtable = vtable,
            };
        }

        /// <summary>The sentence the panel wrote onto this marker, and nothing else. The panel writes
        /// exactly one of three keys onto every marker each refresh
        /// (<c>SpaceportSidePanel.Refresh</c> :166-186); a marker it has not reached yet still carries
        /// the prefab's placeholder ("This is changed by code"), which is parked text rather than
        /// something the game is saying.</summary>
        private static AgeTooltip SlotSentence(AgeTransform marker)
        {
            AgeTooltip tooltip = marker == null ? null : marker.AgeTooltip;
            // The raw content is read to IDENTIFY the sentence, never to say it: the three keys below
            // are what the panel writes, and anything else is the prefab's placeholder. The words
            // still reach the player only through the door - the tooltip itself is handed to
            // GraphNodes.TooltipSection below, or nothing is.
            string content = tooltip == null ? null : tooltip.Content;
            return content == SpaceportSlotFilled
                || content == SpaceportSlotEmpty
                || content == SpaceportSlotLocked
                ? tooltip
                : null;
        }

        private const string SpaceportSlotFilled = "%SpacePortSelectedPopulationSlotDescription";
        private const string SpaceportSlotEmpty = "%SpacePortEmptyPopulationSlotDescription";
        private const string SpaceportSlotLocked = "%SpacePortLockedPopulationSlotDescription";

        /// <summary>
        /// Whether the spaceport would take what is being carried.
        ///
        /// The game asks no <c>CanAcceptPopulationDrop</c> of the port at all - the panel is simply put
        /// in the target list whenever it is drawn (<c>StartDrag</c> :144-148) - so the tests are the
        /// panel's own client's: the port is the player's and drawn, the unit is leaving a PLANET, and
        /// somebody would actually move. There is no port-to-port move: the order the client posts
        /// names the planet the unit comes off (<c>ApplyDrop</c> :38-44), and a unit already in the
        /// port has none.
        ///
        /// The last test is the clamp itself, asked with the very numbers the drop will use
        /// (<see cref="PopulationMoves.IntoPort"/>). A drop onto an EMPTY slot needs a free slot. A
        /// drop onto an OCCUPIED one is the swap, which frees its own slot by bouncing somebody back
        /// onto the source planet - so it works on a FULL port, but only while that planet has room to
        /// take them, and a full port plus a full source planet moves nobody at all. Asking the
        /// arithmetic rather than a hand-written "is there a free slot" is what keeps the word "drop
        /// target" and the outcome from ever disagreeing.
        /// </summary>
        private static bool PortTakes(SpaceportSidePanel panel, CarryItem held, bool swapping)
        {
            try
            {
                Population population = held == null ? null : held.Cargo as Population;
                Spaceport port = panel == null ? null : panel.Spaceport;
                ColonizedPlanet source =
                    population == null ? null : PopulationMoves.PlanetOf(population);
                if (
                    port == null
                    || !panel.Shown
                    || port.Empire != Gui.PlayerEmpire
                    || source == null
                )
                {
                    return false;
                }

                return PopulationMoves.IntoPort(port, swapping ? source : null, held.Quantity) > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Put the carried unit into the spaceport, the way the drag does it: the drag info is filled
        /// as the game's own drag start fills it and the SOURCE's client -
        /// <c>PlanetLabelsWindow_SystemManagement</c>, since the unit is coming off a planet - posts
        /// the order, which is what keeps the sound and the exact orders the game builds.
        ///
        /// A drop onto an OCCUPIED slot names that slot's affinity as the one being replaced, and the
        /// client then posts the game's own two orders: one of the replaced people back onto the source
        /// planet, then the carried people in (<c>ApplyDrop</c> :38-44).
        ///
        /// What is SAID is what really moved. The port clamps rather than refusing
        /// (<c>Spaceport.TransferPopulation</c> :191), so a carry of three into one free slot moves one
        /// and says one.
        /// </summary>
        private static DropResult DropIntoSpaceport(
            SpaceportSidePanel panel,
            CarryItem item,
            StaticString replaced
        )
        {
            Population population = item == null ? null : item.Cargo as Population;
            Spaceport port = panel == null ? null : panel.Spaceport;
            ColonizedPlanet source =
                population == null ? null : PopulationMoves.PlanetOf(population);
            if (population == null || port == null || source == null || !panel.Shown)
            {
                return DropResult.Refused(null);
            }

            try
            {
                bool swapping = !StaticString.IsNullOrEmpty(replaced);
                int moved = PopulationMoves.IntoPort(
                    port,
                    swapping ? source : null,
                    item.Quantity
                );
                if (moved <= 0)
                {
                    return DropResult.Refused(null);
                }

                IDragDropClient client =
                    Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false);
                if (client == null)
                {
                    return DropResult.Refused(null);
                }

                try
                {
                    PopulationMoves.Fill(source, population, item.Quantity, replaced, true);
                    client.ApplyDrop(panel);
                }
                finally
                {
                    PopulationMoves.Clear();
                }

                return DropResult.Done(
                    ModStrings.Format(
                        ModStrings.SystemPopulationMoved,
                        PopulationMoves.Name(population, moved),
                        ModStrings.Get(ModStrings.SystemSpaceportPanel)
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("system: moving a population unit into the spaceport threw: " + e);
                return DropResult.Refused(null);
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
        /// <paramref name="replaced"/> is the SWAP: empty for the card's own plain add, and the
        /// affinity standing in a slot for a drop onto that slot. A planet-to-planet order carries it
        /// as its <c>PopulationToRemoveFirst</c>; a drop out of the SPACEPORT ignores it, because the
        /// spaceport's own client ignores it (<c>SpaceportSidePanel.ApplyDrop</c> :70-80 posts one
        /// order and never reads the field), and mirroring what the mouse does there means mirroring
        /// that too.
        ///
        /// The drag info is cleared again whatever happens: it is a static the game's own refresh
        /// reads every frame to draw a unit as already gone, and a stale one would empty a marker the
        /// player is still looking at.
        /// </summary>
        private static DropResult DropPopulation(Ring ring, CarryItem item, StaticString replaced)
        {
            PlanetLabel_SystemManagement label = ring.Card;
            Population population = item == null ? null : item.Cargo as Population;
            ColonizedPlanet destination = ring.Destination;
            ColonizedPlanet source = population == null ? null : SourceOf(destination, population);
            SpaceportSidePanel port =
                population == null || source != null ? null : SpaceportSource(population);
            if (destination == null || (source == null && port == null))
            {
                return DropResult.Refused(null);
            }

            try
            {
                // Out of the spaceport the port clamps against the PLANET's room and never refuses
                // (Spaceport.TransferPopulation :191); planet to planet the whole carry moves, because
                // the game swaps the surplus back rather than dropping it
                // (DepartmentOfTheInterior.TransferPopulationFromPlanetToPlanet).
                int moved = source != null
                    ? item.Quantity
                    : PopulationMoves.OntoPlanet(destination, item.Quantity);
                if (moved <= 0)
                {
                    return DropResult.Refused(null);
                }

                IDragDropClient client = source != null
                    ? (IDragDropClient)
                        Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false)
                    : port;
                if (client == null)
                {
                    return DropResult.Refused(null);
                }

                try
                {
                    PopulationMoves.Fill(
                        source != null
                            ? (ICappedPopulationOwner<Population>)source
                            : port.Spaceport,
                        population,
                        item.Quantity,
                        replaced,
                        true
                    );
                    if (!ring.Target.CanAcceptPopulationDrop())
                    {
                        return DropResult.Refused(null);
                    }

                    client.ApplyDrop(label);
                }
                finally
                {
                    PopulationMoves.Clear();
                }

                return DropResult.Done(
                    ModStrings.Format(
                        ModStrings.SystemPopulationMoved,
                        PopulationMoves.Name(population, moved),
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

        /// <summary>
        /// Which planet the carried unit came off. Found rather than remembered: what is carried is
        /// the game's own <c>Population</c>, and the planet holding it is the one whose own table it
        /// is in.
        ///
        /// The destination's own system is searched first, which is the whole answer for the ordinary
        /// case and is what keeps a unit dropped back on the planet it came from a refusal rather than
        /// an order from a planet to itself. The empire-wide fall-back is for the SANCTUARY ring: a
        /// ghost colony belongs to the ghost's system and not to the one on screen, so a unit carried
        /// off it is in neither of the searched system's tables.
        /// </summary>
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

                ColonizedPlanet elsewhere = PopulationMoves.PlanetOf(population);
                return ReferenceEquals(elsewhere, destination) ? null : elsewhere;
            }
            catch (Exception)
            {
                return null;
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
                // Flow control: the kept cards are sorted by rectangle and walked in that order, so a
                // card the window is not drawing would reorder the ones it is.
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
        /// The ghost pair is the one set no save here can reach - the state needs a player empire playing
        /// the Umbral Choir, which no save in this repo does - so it was measured by lending the two
        /// panels a real colony and showing them (2026-08-25). Every widget the game drew was declared: the growth
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
            // The merged stop's own name is a pushed level, so it has to be popped before anything
            // that is not in that stop is declared - the bottom panels are declared after this and
            // read "System information, Hangar, ..." while it was left open. Tracked here and closed
            // on every exit path, including the catch, which is what the push contract asks for.
            bool merged = false;
            try
            {
                SidePanels.Drawn(_panels);
                for (int i = 0; i < _panels.Count; i++)
                {
                    SidePanel panel = _panels[i];
                    // The spaceport is a WORK surface and keeps a stop of its own; every other panel
                    // the game is drawing is a region of the merged one. Asked of the panel rather
                    // than of a list, so an outpost's or a ghost system's own set merges without
                    // being modelled.
                    if (panel is SpaceportSidePanel)
                    {
                        if (merged)
                        {
                            merged = false;
                            builder.PopContext();
                        }

                        builder.BeginStop("system:side/" + panel.GetType().Name);
                        builder.PushContext(PanelName(panel));
                        BuildPanel(builder, panel, i);
                        builder.PopContext();
                        continue;
                    }

                    if (!merged)
                    {
                        merged = true;
                        // Keyed by where the run STARTS, so the ordinary page - where every merged
                        // panel precedes the port - always answers "system:side" and the stop's
                        // remembered position survives a rebuild. A run beginning after the port
                        // would key itself apart rather than collide with it.
                        builder.BeginStop(i == 0 ? SidePanelsStop : SidePanelsStop + "/" + i);
                        builder.PushContext(ModStrings.Get(ModStrings.SystemSidePanels));
                    }

                    // The region key is the stop key this panel used to have, so a walk diff reads as
                    // "stop became region" with nothing else moved.
                    builder.SetRegion("system:side/" + panel.GetType().Name);
                    builder.PushContext(PanelName(panel));
                    BuildPanel(builder, panel, i);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("system: reading the side panels threw: " + e);
            }
            finally
            {
                if (merged)
                {
                    builder.PopContext();
                }
            }
        }

        /// <summary>One side panel's contents, under whatever stop or region the caller has opened for
        /// it. Which reader a panel gets is the panel's own type: three of them are hand-modelled and
        /// everything else goes through the shared readout walk.</summary>
        private void BuildPanel(GraphBuilder builder, SidePanel panel, int index)
        {
            // The key prefix is the panel's INDEX among the drawn panels, which is what it was before
            // the merge - so every node keeps the id it had and a remembered cursor still finds it.
            string keyPrefix = "system:side/" + index + "/";
            ColonyInfoSidePanel colony = panel as ColonyInfoSidePanel;
            if (colony != null)
            {
                BuildColonyInfo(builder, colony);
                return;
            }

            SpaceportSidePanel spaceport = panel as SpaceportSidePanel;
            if (spaceport != null)
            {
                BuildSpaceport(builder, spaceport, keyPrefix);
                return;
            }

            RepresentativesStarSystemSidePanel representatives =
                panel as RepresentativesStarSystemSidePanel;
            if (representatives != null)
            {
                BuildRepresentatives(builder, representatives, keyPrefix);
                return;
            }

            BuildReadouts(builder, panel, keyPrefix);
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

            // The spaceport panel is another of the unlabelled boxes: without a name it fell through
            // to its header icon's sentence, so the stop was called "This panel allows you to send
            // population to a colonized planet." The word is the game's own, off the panel's title.
            if (panel is SpaceportSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemSpaceportPanel);
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

            // The third of the sanctuary boxes, and the same kind of unlabelled box again: without a
            // name it fell through to its header sentence, so the stop was called "This panel shows
            // where the Ships and Populations created by this System will spawn" (measured by lending
            // it the fixture's own colony, 2026-08-29). "Sanctuary Link" is the game's own word for
            // what the panel sets - both its rows are headed with it
            // (<c>%ShipsSpawnPointTitle</c> "[ship] Sanctuary Link:",
            // <c>%PopulationsSpawnPointTitle</c>) - so the name stays in that vocabulary, as the two
            // ghost panels' do.
            if (panel is ShipsSpawnPointSidePanel)
            {
                return ModStrings.Get(ModStrings.SystemSanctuaryLinksPanel);
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

            // The banner and the little level badge in its corner are TWO of the game's buttons and go
            // to two different screens (<see cref="BannerButton"/>), so they are two rows - the badge
            // named with the game's own word for what it is, since the figure it draws has already been
            // said by the banner above it.
            AddReadout(
                _cells,
                panel.SystemBanner,
                "system:colony/banner",
                () =>
                    ModStrings.Format(
                        ModStrings.SystemLevel,
                        AgeText.Label(panel.LevelLabel)
                    ),
                null,
                null,
                BannerButton(panel, "OnSystemBannerClickCb")
            );
            AddReadout(
                _cells,
                panel.LevelGroup,
                "system:colony/level",
                CardActions.GameText(SystemLevelTitle),
                null,
                null,
                BannerButton(panel, "OnSystemLevelClickSb")
            );

            AddMothership(_cells, panel);
            AddSystemPaging(_cells);

            AgeControlButton rename = panel.RenameButton;
            // Banding input: Cells.Add takes the button without asking the gate, and its rectangle is
            // what puts it on the same row as the system's name.
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
                Add(_cells, AgeWidgets.Transform(rename), ControlId.For(rename, "system:colony/rename"), vtable);
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
            // Banding input: same door, same reason - the cell is banded by where it is drawn.
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
                Add(_cells, it, ControlId.For(it, "system:colony/improvements"), vtable);
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
            // Different widget and banding input: the cell below stands on the NAME label, which the
            // panel leaves drawn inside a mothership group it has switched off.
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
            Add(cells, label, ControlId.For(label, "system:colony/mothership"), vtable);

            AgeControlButton detach = panel.DetachButton;
            AgeTransform widget = AgeWidgets.Transform(detach);
            // Banding input: Cells.Add takes the button without asking the gate, and it bands with the
            // mothership's name above.
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
            Add(cells, widget, ControlId.For(detach, "system:colony/detach"), button);
        }

        /// <summary>
        /// The row of badges beside the system's name: that this is somebody's home system, that a
        /// trading company keeps its headquarters or a subsidiary here, and that the system is cloaked
        /// (<c>ColonyInfoSidePanel.Refresh</c> :439-483). Each is drawn only when it is true of this
        /// system, and each is one node, because each carries a sentence of its own.
        ///
        /// The game writes no caption on any of them and hangs no wrapper on their tooltips, so each is
        /// called by the sentence its own tooltip explains it with - the same naming a wordless symbol
        /// gets everywhere else in this mod. The readout then drops that opening line from the tooltip
        /// it announces, so the rest of it - including the list of empires that have seen through the
        /// cloak, which is the only place that list exists - is handed over as well as reviewable.
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
            // Banding input: the three status icons are worked into one row by their rectangles, and
            // Cells.Add takes them without asking the gate.
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
                    GraphNodes.TooltipSection(tooltip)
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            cells.Add(
                new Cell
                {
                    Widget = widget,
                    Id = ControlId.For(widget, "system:colony/icon/" + key),
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
            IList<AgeTransform> items = AgeWidgets.DrawnChildren(table);
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
            // Banding input: Cells.Add takes the group without asking the gate, and its rectangle is
            // what puts the citadel's pool beside the system's own.
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
            // Banding input: Cells.Add takes the tick without asking the gate, and it bands with
            // whatever the panel drew on its row.
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
            Add(cells, widget, ControlId.For(toggle, "system:colony/decolonize"), vtable);
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
            // Banding input: Cells.Add takes the banner without asking the gate, and the panel draws it
            // only while a status is running.
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
            // Banding input: Cells.Add takes the group without asking the gate.
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
            // Flow control, and a different widget than the cells: the BANNER is what the panel hides,
            // while the items inside it keep their own flags and are what the cells stand on.
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
                    // Banding input: Cells.Add takes each item without asking the gate, and the items
                    // are worked into the banner's row by where they are drawn.
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
                            Id = ControlId.For(
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
            // Banding input: Cells.Add takes the group without asking the gate, and the panel draws it
            // only where there is at least one wreck.
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
            // Flow control: the five outputs under the group are read one property at a time.
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
            // Banding input, and a different widget: the cell stands on the drop list, while the GROUP
            // is what the panel hides - the list inside keeps its own flag.
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
            Add(cells, widget, ControlId.For(list, "system:colony/policy"), vtable);
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

        /// <summary>
        /// One of the two buttons the panel hides inside its banner picture: the banner itself opens
        /// the empire summary at its systems list (<c>OnSystemBannerClickCb</c> :915-928), and the
        /// level badge in the banner's corner opens the economy screen at its own tab
        /// (<c>OnSystemLevelClickSb</c> :930-943). Neither carries a word or a tooltip of its own -
        /// the banner's tooltip belongs to the LEVEL, which is what the row already says - so they are
        /// found by the handler the prefab wired, as the improvements button beside them is.
        /// </summary>
        private static AgeTransform BannerButton(ColonyInfoSidePanel panel, string handler)
        {
            AgeTransform banner = panel.SystemBanner;
            if (banner == null)
            {
                return null;
            }

            AgeControlButton[] buttons = banner.GetComponentsInChildren<AgeControlButton>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].OnActivateMethod == handler)
                {
                    return buttons[i].AgeTransform;
                }
            }

            return null;
        }

        /// <summary>The game's own word for what the badge in the banner's corner is - it draws the
        /// figure and names it nowhere on the panel.</summary>
        private const string SystemLevelTitle = "%SystemLevelTitle";

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
                // Flow control: each block kept here is read as a section of its own below, and the
                // list is walked in drawn order - a block the panel is not drawing would open one over
                // nothing.
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

            EmitBlock(builder, panel, keyPrefix, 0, split);
            EmitBlock(builder, panel, keyPrefix, split, _blocks.Count);
        }

        private static readonly Comparison<AgeTransform> ByDrawnY = (left, right) =>
            left.GetGlobalPosition().y.CompareTo(right.GetGlobalPosition().y);

        /// <summary>One captioned block of a panel read in pieces: its own lines, one per row, under the
        /// caption the game drew over them - which is the topmost line the block produced, and is a row
        /// of the block as well as its name.
        ///
        /// The blocks are CONTEXTS and no longer regions of their own (owner design 2026-08-29): the
        /// side panels are one stop now and a region there is one PANEL, so a panel splitting itself
        /// into two would put five region-jumps where the design asks for four. The captions still name
        /// the blocks and are still rows, so nothing the player can hear was lost - only the region
        /// chord's stop inside this one panel.</summary>
        private void EmitBlock(
            GraphBuilder builder,
            SidePanel panel,
            string keyPrefix,
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

            if (SpaceportPopulations(widget, panel as SpaceportSidePanel))
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
            // Banding input: the cell is appended straight to the list, so the gate never sees it until
            // the bands are already drawn - and the governor's symbols share one row.
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
            // Banding input: same row, same reason - the cell is appended without the gate's question.
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
                    Id = ControlId.For(widget, keyPrefix + widget.name),
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
                Id = ControlId.For(widget, keyPrefix + widget.name + "/governor-portrait"),
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
                Id = ControlId.For(widget, keyPrefix + widget.name + "/governor-level"),
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
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, keyPrefix + widget.name + "/population"),
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
                Id = ControlId.For(widget, keyPrefix + widget.name + "/growth-source"),
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
                Id = ControlId.For(widget, keyPrefix + widget.name + "/representative"),
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
            };
            // Two hover targets on one row, in the order they are drawn: the icon.s one-line gloss on
            // what Approval is, and the row.s own dossier, which the row points at. The gloss used to be
            // a reviewed line here - words on a row the pointer never visits, which the game therefore
            // never draws - and is now an entry of its own, aimed at the icon a mouse would have
            // pointed at.
            TooltipChildren.Carried carried = TooltipChildren.Split(
                new List<AgeTooltip> { iconTooltip, tooltip }
            );
            vtable.Sections = GraphNodes.Sections(GraphNodes.TooltipSection(carried.Own));
            AgeWidgets.PointAt(vtable, widget);
            string approvalKey = keyPrefix + widget.name + "/approval";
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, approvalKey),
                Vtable = vtable,
                Dossiers = carried.Children,
                Key = approvalKey,
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
            };
            // The kind tooltip is the row.s OTHER hover target and the wait.s own is the one the row
            // points at, so the kind becomes an entry of its own rather than a reviewed line the row
            // cannot make the game draw.
            TooltipChildren.Carried carried = TooltipChildren.Split(
                new List<AgeTooltip> { kind, when }
            );
            vtable.Sections = GraphNodes.Sections(GraphNodes.TooltipSection(carried.Own));
            AgeWidgets.PointAt(vtable, widget);
            string growthKey = keyPrefix + widget.name + "/growth";
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, growthKey),
                Vtable = vtable,
                Dossiers = carried.Children,
                Key = growthKey,
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
                Id = ControlId.For(widget, keyPrefix + widget.name + "/outposts"),
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
                // A drawn-but-blank label answers null here rather than empty: one caller falls back
                // to the wrapper's title with ?? for an effect line drawn as a bare picture.
                string drawn = AgeWidgets.DrawnLabel(label);
                return string.IsNullOrEmpty(drawn) ? null : drawn;
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
                // The graph.s tooltip opens with the sentence that is already the row.s NAME and then
                // says what the sensitivity is for. It reads by its own kind, and the readout drops the
                // opening line the name has already said.
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(tooltip),
                    NodeSection.Buffer(() => SensitivityDetails(it))
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.For(widget, keyPrefix + widget.name + "/sensitivity"),
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
            // Banding input: Add below is Cells.Add, which takes the button without asking the gate.
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
            Add(cells, widget, ControlId.For(button, key), vtable);
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
            AgeTooltip tooltip = null,
            AgeTransform click = null
        )
        {
            // Banding input, as at the buttons: Add below is Cells.Add, and the panel passes labels
            // here that it draws only in some of the colony's states.
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
            // A line the game also made CLICKABLE is a button, and says so: the row is still read the
            // same way and Enter is the game's own press. Nothing is spoken for the press itself -
            // every one of these opens a screen, and the screen announces itself.
            if (click != null)
            {
                AgeTransform pressed = click;
                vtable.ControlType = ControlTypes.Button;
                vtable.Announcements.Add(
                    GraphNodes.DisabledPart(() => AgeWidgets.Operable(pressed))
                );
                vtable.OnActivate = () =>
                {
                    if (AgeWidgets.Operable(pressed))
                    {
                        AgeWidgets.Press(pressed);
                    }
                };
            }

            if (value != null)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(value));
            }

            AgeWidgets.PointAt(vtable, widget, tip);
            Add(cells, widget, ControlId.For(widget, key), vtable);
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
    }
}

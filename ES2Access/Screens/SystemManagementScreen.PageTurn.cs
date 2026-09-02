using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>Where the cursor stands when the page turns: the frame-settled arrival test, the
    /// swap guard, and the seat this page remembers and puts back after a turn or a system change.
    /// </summary>
    public sealed partial class SystemManagementScreen
    {
        /// <summary>Whether the page has been seen bound and drawn since the view level was entered -
        /// see <see cref="IsActive"/>.</summary>
        private bool _arrived;

        /// <summary>The arrival check's own scratch lists, so asking whether the page is whole yet
        /// cannot disturb a build that is holding <c>_planets</c> and <c>_panels</c>.</summary>
        private readonly List<PlanetLabel_SystemManagement> _arriving =
            new List<PlanetLabel_SystemManagement>();

        private readonly List<SidePanel> _arrivingPanels = new List<SidePanel>();

        /// <summary>What <see cref="Whole"/> answered this frame, and the two lists it filled to answer
        /// it. On the arrival frame the question is asked TWICE - once by <see cref="IsActive"/> and
        /// once by <see cref="Build"/> - and the two askers pass different scratch lists, so without
        /// this the page walked every planet card and every left-edge panel, sorted both by where they
        /// are drawn, and did it all again a moment later for an answer that could not have changed.
        /// Nothing it reads is anything the mod moves between the two calls.</summary>
        private int _wholeFrame = -1;

        private bool _wholeAnswer;

        private readonly List<PlanetLabel_SystemManagement> _wholeCards =
            new List<PlanetLabel_SystemManagement>();

        private readonly List<SidePanel> _wholePanels = new List<SidePanel>();

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
            int frame = UnityEngine.Time.frameCount;
            if (_wholeFrame == frame)
            {
                Copy(_wholeCards, cards);
                Copy(_wholePanels, panels);
                return _wholeAnswer;
            }

            _wholeAnswer = Drawing(cards, panels);
            _wholeFrame = frame;
            Copy(cards, _wholeCards);
            Copy(panels, _wholePanels);
            return _wholeAnswer;
        }

        private static void Copy<T>(List<T> from, List<T> into)
        {
            into.Clear();
            for (int i = 0; i < from.Count; i++)
            {
                into.Add(from[i]);
            }
        }

        /// <summary>The reading <see cref="Whole"/> holds for the frame. See its remarks for the
        /// question, and this method's own for the four things that have to be true.</summary>
        private bool Drawing(List<PlanetLabel_SystemManagement> cards, List<SidePanel> panels)
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
            _zoom.Forget();
            _hud.Forget();
            _showing = null;
            _turnSettle = 0;
            _turnSeats = 0;
        }

        public override void OnUpdate()
        {
            _hud.Update();
            _zoom.Update();
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
    }
}

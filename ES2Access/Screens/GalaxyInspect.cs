using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>What the square under the inspect cursor is, to a caller making something OUT of it -
    /// today the bookmark set key (<see cref="GalaxyInspect.CellPlace"/>).</summary>
    internal enum CellSubject
    {
        /// <summary>No cursor is up, so there is no square to ask about.</summary>
        None,

        /// <summary>The square holds no place at all: it stands for its own point of galaxy.</summary>
        Point,

        /// <summary>Exactly one place stands in it, and the square stands for that place.</summary>
        Place,

        /// <summary>More than one place stands in it, so the square stands for neither: which one the
        /// player meant cannot be known, and the cursor is theirs to shrink.</summary>
        Crowded,
    }

    /// <summary>
    /// INSPECT MODE: a square of galaxy the player moves about the map and hears the contents of.
    ///
    /// The tree on this page answers "what is at this place, and what is next to it" - it is a list of
    /// the things the map draws, and every step in it is a step from one THING to another. That leaves
    /// one question a sighted player answers at a glance and a reader of the tree cannot ask at all:
    /// what is OVER THERE - in that direction, that far away, whether or not anything is there. So this
    /// mode replaces the tree with a place: a cell of the map, said in the same coordinate pair
    /// everything else on the map is said in (<see cref="GalaxyCoordinates"/>), which the arrows move a
    /// whole cell at a time.
    ///
    /// The rules that make it a MAP rather than a list are in <see cref="InspectGrid"/> (odd sizes so
    /// the centre is a whole pair, half-open cells and a step of exactly the cursor's size so the cells
    /// tile). What lives here is the live half: which of the things the map is drawing fall inside the
    /// cell, where the camera goes, the square drawn on the screen, and the keys.
    ///
    /// THE KEYS ARE TAKEN AT MODE LEVEL, BUT ONLY ON THE MAP. The arrows, Enter and Escape mean the
    /// cell rather than the cursor while the mode is up AND the tree cursor is standing on the map
    /// widget (<see cref="Active"/>), and they are taken through <c>Screen.AnyKey</c> - the hook that
    /// is asked before the review chords and before navigation - which is the same displacement the map
    /// already lives with while the game has armed a targeting cursor. Escape is claimed FROM the game
    /// under the same condition, or leaving the mode would also raise the pause menu; the two size keys
    /// are claimed the same way, so the game keeps its own keypad minus (Sleep for this turn) the rest
    /// of the time. THE ONE KEY THIS MODE DOES NOT GET FIRST IS ESCAPE WHILE A TYPE-AHEAD SEARCH IS
    /// LIVE: the search is the innermost surface, so that Escape clears it and leaves the player in the
    /// mode, and the next one exits (owner ruling 2026-08-19, routed in <c>ModEntry.Dispatch</c> so
    /// every mode of this shape obeys it).
    ///
    /// OFF THE MAP THE MODE IS SUSPENDED, not ended. Tab and Shift+Tab still walk the galaxy screen's
    /// other stops while the cursor is up, and a stop that is not the map - the zoom slider above all -
    /// gets every key exactly as if this mode did not exist: its arrows adjust it, its Escape is the
    /// game's, and nothing the player presses there moves the cell. A mode that claimed the arrows
    /// screen-wide made the zoom slider unusable, which is the defect this rule exists for. The cell
    /// keeps its place, its size and its square while suspended - the square is a mark on the MAP and
    /// the map has not changed - and coming back to the map reads the cell out again
    /// (<see cref="Update"/>), so the player knows the arrows mean the cell once more.
    ///
    /// ARMING IT IS A KEY OF THE MAP TOO. Ctrl+I off the map widget is not the mod's key at all: it is
    /// not taken, nothing is said, and the tree cursor stays where it is. A mode of one widget that
    /// moved the player to that widget would be a key doing two things at once, and the player who
    /// wanted the cell can Tab to the map and press it there (owner's ruling).
    ///
    /// THE MODE CANNOT OUTLIVE THE PAGE. Anything that takes the player off the map - a screen, a
    /// modal, a system opened - pops this page, and the mode ends there with its lines released and
    /// the player TOLD. That line is spoken from the pump rather than from the pop
    /// (<see cref="Tick"/>): the page that took over announces itself with an interrupt in the same
    /// frame, and anything queued before that is thrown away.
    /// </summary>
    internal sealed partial class GalaxyInspect
    {
        /// <summary>How long the camera takes to settle on a new cell. The game's own jump between
        /// places uses the same figure (<c>GuiManager</c>), so a sweep of the map moves at the speed
        /// everything else in the game moves at.</summary>
        private const float CameraDamping = 0.3f;

        /// <summary>The CLOSEST the camera may be when the mode opens - internal rung 8 of
        /// 0=furthest..12=closest, which the ladder speaks as "Zoom level 9" (owner's ruling,
        /// 2026-08-17). A ceiling, not a framing: entering pulls a closer camera out to here so
        /// enough of the map is visible around the square, and a player who has already zoomed
        /// further OUT keeps their view. Entry only: the wheel still works inside the mode, and
        /// leaving keeps whatever the player zoomed to.
        ///
        /// Shared with the map's own collapse-restore fallback
        /// (<see cref="GalaxyHudScreen.CollapseZoom"/>, owner ruling 2026-09-01): where a branch is
        /// shut and nothing was written down about the view its expansion took the camera from, the
        /// camera goes here, so the two "a sane distance to be put at" numbers in the mod are one
        /// number.</summary>
        internal const int EntryZoomCeiling = 8;

        public GalaxyInspect(GalaxyHudScreen screen)
        {
            _screen = screen;
        }

        /// <summary>Whether the cursor is up - armed, whether or not the player is standing on the map
        /// at this moment. Static because the input layer asks it while deciding what to claim from the
        /// game, and there is exactly one map.</summary>
        public static bool Live
        {
            get { return _live; }
        }

        /// <summary>Whether the mode is DRIVING: up, and with the tree cursor on the map widget. This
        /// is what every key question asks, because a mode that took keys from the screen's other stops
        /// would leave them unusable while it was armed.</summary>
        public static bool Active
        {
            get { return _live && OnMap(); }
        }

        /// <summary>What <c>ModInput</c>'s conditional claim asks: the size keys and Escape belong to
        /// the mod only while the cursor is up and standing on the map.</summary>
        public static bool KeysClaimed()
        {
            return Active;
        }

        /// <summary>Whether the tree cursor is standing on the galaxy map widget - the stop the cell is
        /// a mode OF. Everything else on this screen (the zoom slider, the turn controls, the
        /// notifications) suspends the mode for as long as the player is standing there.</summary>
        private static bool OnMap()
        {
            return GalaxyHudScreen.CursorOnMap();
        }

        /// <summary>
        /// WHETHER THE PICTURE IS STILL THE GALAXY THIS CELL IS A CELL OF (owner ruling 2026-09-01).
        ///
        /// The mode is a square of GALAXY, and at the closest rung the game stops drawing one: the
        /// system nameplates go and per-planet orbital cards take their place, so a square of sky is
        /// no longer a thing the picture has anything to say about. That rung is asked of the band
        /// table rather than written down as a number (<see cref="Bands"/>), because "the planets have
        /// become full cards" IS the boundary, and a table that is ever re-cut moves this with it.
        ///
        /// Entering from closer is a different question and is not answered here: arming pulls the
        /// camera OUT to the ceiling first (<see cref="EntryZoomCeiling"/>), so the mode never opens
        /// past this line. What this answers is the LIVE cell being carried across it by a later zoom.
        ///
        /// Far out is not a limit at all: at the two furthest rungs the map paints territory and
        /// names nothing, and reading that painting is the one thing a sighted player can do there and
        /// a reader of the tree cannot - the survey (<see cref="Surveying"/>).
        ///
        /// A rung there is no answer for - a battle, the system-discovery view, a level the game is
        /// still flying between - keeps the mode: a gate that cannot tell what is being drawn must not
        /// take a cursor down on the strength of not knowing.
        ///
        /// UNDER A LENS the line falls at the other end of the ladder (RULED 2026-09-01: the mode tops
        /// out at spoken level 10). The map lenses are a galaxy of worlds - the lens rings each star
        /// with its planet circles - and the diplomacy band is a galaxy of painted TERRITORY, which is
        /// the survey's whole subject; the System lens turns the node labels off altogether and puts
        /// one system's panel on the screen instead, so there are no squares of galaxy left to read.
        /// Asked of the table in the same two kinds rather than written down as an 11.
        /// </summary>
        private static bool ShowsTheGalaxy()
        {
            int level = ZoomBands.Level;
            if (level < 0)
            {
                return true;
            }

            return ZoomBands.Scanning
                ? ZoomBands.Shows(BandKind.Planets) || ZoomBands.Shows(BandKind.Empires)
                : ZoomBands.Fidelity(BandKind.Planets) != BandFidelity.Full;
        }

        /// <summary>
        /// WHETHER THE CELL IS READING THE TERRITORY SURVEY (owner ruling 2026-09-01).
        ///
        /// At the two furthest rungs the map names nothing at all - it paints the empires' territory
        /// over the art and writes the constellations across it - so the question a player has there
        /// is not "what is standing here" but "whose is this". The cell answers it by saying whose
        /// territory every square is, rather than only saying so when it changes (<see cref="Influence"/>).
        ///
        /// Asked of the band table as "the picture is not naming the systems", which is the same two
        /// rungs and is the fact the survey exists because of.
        /// </summary>
        private static bool Surveying()
        {
            int level = ZoomBands.Level;
            return level >= 0 && !ZoomBands.Shows(BandKind.Systems);
        }

        /// <summary>Where the cell is, in the pair the map is spoken in - false while the mode is not
        /// up. What the SCANNER measures from while this mode owns the map: the cursor is where the
        /// player is standing, so "nearest" has to mean nearest to it.</summary>
        public bool Centre(out int x, out int y)
        {
            x = _x;
            y = _y;
            return _live;
        }

        /// <summary>
        /// WHERE THE CELL LEAPT FROM - the mode's own way back, kept apart from the tree's.
        ///
        /// The tree has a trail of the lanes and the jumps the player took while walking it
        /// (<c>GalaxyHudScreen</c>); this is the same idea for the other way of reading the map, and the
        /// two never mix. A cell that has been thrown across the galaxy by a bookmark, by the scanner or
        /// by a travel key has lost the square the player was working in, and the arrows cannot walk
        /// back across twenty units of empty space - so the leap is remembered and Backspace undoes it.
        ///
        /// Only LEAPS go on it. The plain arrows and the skip arrows are how the player MOVES, and a
        /// stack that recorded every step would be an undo history of a sweep rather than a way back
        /// from a jump.
        ///
        /// It lives and dies with the mode: entering clears it, leaving clears it, and there is nothing
        /// to restore into once the cell is gone.
        /// </summary>
        private readonly List<InspectCell> _leaps = new List<InspectCell>();

        private struct InspectCell
        {
            public int X;
            public int Y;
        }

        /// <summary>Remember the square the cell is standing on, because it is about to be thrown
        /// somewhere else. Silent, and nothing at all while the mode is down.</summary>
        public void PushCell()
        {
            if (_live)
            {
                _leaps.Add(new InspectCell { X = _x, Y = _y });
            }
        }

        /// <summary>
        /// Backspace: put the cell back where the last leap took it from.
        ///
        /// The cell's own reading is the announcement, exactly as it is for an arrow key - the player
        /// is told where they now are, which is the whole of what coming back means. The zoom is not
        /// touched, because nothing under the cell touches it (owner ruling 2026-08-31).
        ///
        /// An empty stack is TAKEN and silent, the same answer the tree's empty trail gives: the key is
        /// pressed speculatively, and a refusal for it would be a word on every press at the start of a
        /// sweep.
        /// </summary>
        private bool PopCell()
        {
            if (_leaps.Count == 0)
            {
                return true;
            }

            InspectCell was = _leaps[_leaps.Count - 1];
            _leaps.RemoveAt(_leaps.Count - 1);
            _x = was.X;
            _y = was.Y;
            Settle(true);
            return true;
        }

        /// <summary>
        /// Put the cell on a place named by something else - the scanner sending the cursor to what it
        /// found.
        ///
        /// The pair handed in is the ROUNDED one, the pair the player was just told, which is what
        /// makes the thing certain to be inside even the one-unit cursor. The landing is an arrow
        /// key's: the camera slides, the square is redrawn, and the cell is read out, because a
        /// cursor moved by another key is still the same cursor arriving somewhere.
        /// </summary>
        public bool JumpTo(int x, int y)
        {
            if (!_live)
            {
                return false;
            }

            _x = x;
            _y = y;
            Settle(true);
            return true;
        }

        /// <summary>
        /// Put the cell somewhere and say NOTHING here - the same move, for the one caller that makes
        /// the reading happen by another route.
        ///
        /// That caller is a jump made while the mode is PARKED: the player is standing on another
        /// stop, so the cell is not what they are reading. The jump puts them silently back on the map
        /// and the mode's own resume reads the new cell out (<see cref="Update"/>), which is the one
        /// utterance a jump is allowed - and it names the place they jumped to rather than the row
        /// they happened to leave the map on.
        /// </summary>
        public bool MoveTo(int x, int y)
        {
            if (!_live)
            {
                return false;
            }

            _x = x;
            _y = y;
            Show();
            return true;
        }

        /// <summary>
        /// What a bookmark set while the cell is driving the map is made OF: the square the cursor is
        /// standing on, and the one place standing in it where there is exactly one.
        ///
        /// The place is offered by the cell's OWN rule for "the one thing in here"
        /// (<see cref="Activate"/>). A square with NOTHING the tree calls a place in it - empty sky, a
        /// fleet crossing it, a lane through it - is the square's own point, which is the bookmark's
        /// default and is never wrong. A square with TWO is neither: naming one of them would be a
        /// guess, and quietly keeping the point instead would hand the player a bookmark of empty sky
        /// where they had asked for a star (owner ruling 2026-08-31 - the answer there is a refusal
        /// naming the way out, <see cref="CellSubject.Crowded"/>).
        ///
        /// <paramref name="at"/> is the cell's centre turned back into a place on the map, the same
        /// way the camera turns it (<see cref="GalaxyCoordinates.Origin"/>): the cursor is held in the
        /// pair the player is told, and a bookmark is kept in the galaxy's own coordinates.
        /// </summary>
        public CellSubject CellPlace(out GalaxyPosition at, out StarSystemNode place)
        {
            at = default(GalaxyPosition);
            place = null;
            if (!_live)
            {
                return CellSubject.None;
            }

            GalaxyPosition origin = GalaxyCoordinates.Origin();
            at = new GalaxyPosition(origin.X + _x, origin.Y + _y);
            Contents contents = Read();
            int places = contents.Places.Count + contents.Special.Count;
            if (places > 1)
            {
                return CellSubject.Crowded;
            }

            if (places == 0)
            {
                return CellSubject.Point;
            }

            place = contents.Places.Count == 1 ? contents.Places[0] : contents.Special[0];
            return CellSubject.Place;
        }

        /// <summary>
        /// Arm the cursor ON a place another mode of the map chose - the scanner sending the player to
        /// a square of sky.
        ///
        /// The one entry that is not the player's own Ctrl+I, and the one place a mode is turned on by
        /// something other than its own key (owner decision, 2026-08-21). The reason is that the thing
        /// found has no other landing: a square is not a node and the tree has no row for one, so a
        /// "go to it" that did not arm the cursor could only move the camera and say nothing.
        ///
        /// Everything else about it is Ctrl+I's - the same entry announcement, the same zoom ceiling,
        /// the same cursor to leave with Escape - except that the cell opens on the square handed in
        /// rather than on whatever the tree cursor was standing on, so the player hears the arrival
        /// once instead of hearing the entry cell and then the jump.
        /// </summary>
        public bool ArmAt(int x, int y)
        {
            if (_live || !OnMap())
            {
                return false;
            }

            Enter(true, x, y);
            return true;
        }

        /// <summary>Once per frame from the pump, after the screens have settled and before the
        /// pointer commits: where the game's own tooltip is pointed while the cell drives the map,
        /// and an ending that is waiting to be spoken.</summary>
        public static void Tick()
        {
            ObserveChordKey();
            Point();
            Ending();
        }

        /// <summary>
        /// One log line for every PHYSICAL press of the inspect chord's letter, while the dev server
        /// is up: the whole claim decision as it stood on that frame. The chord's refusals are silent
        /// by design - off the map the key is not the mod's, another modifier spoils the
        /// exact-modifier match, and the game answers Ctrl+I with nothing - so a lost press leaves no
        /// trace anywhere but here, written on the exact frame the letter went down.
        ///
        /// One cause is SETTLED and must not be "fixed" (owner's ruling, 2026-08-17): a letter whose
        /// keydown lands a frame before its modifier's - the near-simultaneous press - reads
        /// ctrl=False here and the chord does not match. That is working as intended: the matcher is
        /// exact and modifier-first, and no grace window is wanted. This observer stays because the
        /// OTHER causes (off the map, a stray modifier) are still worth a trace.
        /// </summary>
        private static void ObserveChordKey()
        {
            try
            {
                if (!DevUp() || !UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.I))
                {
                    return;
                }

                GraphNavigator navigator = ModEntry.Navigator;
                Core.UI.Graph.GraphNode node = navigator == null ? null : navigator.CurrentNode;
                Log.Info(
                    "inspect-chord: I down"
                        + " ctrl="
                        + (
                            UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl)
                            || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl)
                        )
                        + " shift="
                        + (
                            UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift)
                            || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift)
                        )
                        + " alt="
                        + (
                            UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftAlt)
                            || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightAlt)
                        )
                        + " onMap="
                        + OnMap()
                        + " node="
                        + (node == null ? "null" : node.Id + " stop=" + node.StopKey)
                        + " live="
                        + _live
                );
            }
            catch (Exception)
            {
                // A diagnostic must never cost the frame it is diagnosing.
            }
        }

        // Whether the dev server is listening, read once per load the way NotificationAudit reads it
        // (the loader owns the answer and does not publish it): a player has no use for a log line
        // about a keypress, and a dev feature that cannot prove it is wanted should not run.
        private static int _devUp = -1;

        private static bool DevUp()
        {
            if (_devUp < 0)
            {
                bool up;
                try
                {
                    Type plugin = typeof(ES2Access.Loader.ModHost).Assembly.GetType(
                        "ES2Access.Loader.LoaderPlugin"
                    );
                    UnityEngine.Object[] found =
                        plugin == null ? null : UnityEngine.Object.FindObjectsOfType(plugin);
                    object dev =
                        found == null || found.Length == 0
                            ? null
                            : HarmonyLib.AccessTools.Field(plugin, "_dev").GetValue(found[0]);
                    up =
                        dev != null
                        && HarmonyLib.AccessTools.Field(dev.GetType(), "_http").GetValue(dev) != null;
                }
                catch (Exception)
                {
                    up = false;
                }

                _devUp = up ? 1 : 0;
            }

            return _devUp == 1;
        }

        /// <summary>
        /// Keep the game's own tooltip on what the CELL is over, for as long as the cell is driving
        /// the map.
        ///
        /// The pointer is otherwise aimed by the focused control's own focus visual, and focus does
        /// not move at all while the cursor sweeps - so without this the tooltip of whatever the mode
        /// was opened from stayed drawn over an empty quarter of the galaxy, for the rest of the
        /// session (owner-reported).
        ///
        /// From the pump rather than from the page's own Update: a focus visual is re-committed
        /// during the screens tick whenever the camera changes what the map draws for the focused
        /// system (<c>GalaxyHudScreen.FollowCamera</c>), and the cell moving IS the camera
        /// moving, so a request made before that would be overwritten by it. Here it is after the
        /// screens have settled and before the pointer commits (<c>PointerFocus.Tick</c>), which is
        /// where every other visual lands.
        /// </summary>
        private static void Point()
        {
            GalaxyInspect mode = _driving;
            if (mode == null || !Active)
            {
                return;
            }

            mode.PointAtCell();
        }

        /// <summary>
        /// Speak an ending that happened during a screen change, AFTER the page that took over has
        /// announced itself.
        ///
        /// It has to WAIT for that announcement rather than merely come after the screens tick: a page
        /// arriving takes a frame or two to seat its cursor, and it announces in a BURST - the page's
        /// name, then whatever the cursor landed on - of which the last part INTERRUPTS, throwing away
        /// anything queued before it (measured: queued one frame ahead of "Star system" the line was
        /// cancelled by it; queued straight after "Star system" it was cancelled by "Planets, Raia"
        /// three frames later). So the wait is on the speech going QUIET: something else has been said
        /// and nothing more has been said for a moment. The frame budget is the other ending - a page
        /// that announces nothing at all still owes the player this line.
        /// </summary>
        private static void Ending()
        {
            if (_pending == null)
            {
                return;
            }

            PrismSpeech speech = ModEntry.Speech;
            string last = speech == null ? null : speech.LastSpoken;
            if (last != _lastHeard)
            {
                _lastHeard = last;
                _quiet = QuietFrames;
            }

            _waited--;
            bool settled = _lastHeard != _spokenWhenLeft && --_quiet <= 0;
            if (!settled && _waited > 0)
            {
                return;
            }

            string line = _pending;
            _pending = null;
            _spokenWhenLeft = null;
            _lastHeard = null;
            Voice.Say(line, false);
        }

        /// <summary>About two seconds of frames - long enough for any page in this game to arrive and
        /// say what it is, short enough that a page which says nothing does not leave the player
        /// wondering whether the mode is still up.</summary>
        private const int WaitFrames = 120;

        /// <summary>How long the speech has to have been quiet for the arriving page's burst to count
        /// as finished - a quarter of a second, which is several times the gap between the parts of
        /// one arrival.</summary>
        private const int QuietFrames = 15;

        /// <summary>Drop the mode's process-wide state - mod teardown. The square is taken off here as
        /// well as when the page is popped: it is drawn by an object of this assembly's own, and one
        /// left alive would be drawn by a behaviour whose type the next load cannot reach.</summary>
        public static void Reset()
        {
            InspectMarker.Hide();
            _live = false;
            _driving = null;
            _pending = null;
            _spokenWhenLeft = null;
            _lastHeard = null;
        }

        /// <summary>
        /// End the mode because something else is taking the map - a fleet action about to seat the
        /// cursor on the control that gives its real order, or the game arming a targeting cursor.
        /// Answers whether there was a mode to end.
        ///
        /// Two cursors on one widget is the thing this prevents: with the cell up, the arrows mean the
        /// square and Enter means the cell's landing, so a player told to "click a target" would be
        /// pressing keys that belong to something else, and a fleet action's seat would put the tree
        /// cursor somewhere the arrows no longer move. The mode's OWN exit line is said, interrupting,
        /// so it is ahead of whatever the caller says next - the game's targeting instruction, or the
        /// landing the seat makes.
        ///
        /// Unlike every other way out, this one puts NOTHING back: no cursor seat, no camera recentre,
        /// no re-announcement of where the player is standing. The caller is the reason the mode ended
        /// and it is about to decide all three - and where it decides nothing (a mode armed from a
        /// panel, the cursor left on the button that armed it) the player has not moved at all.
        /// </summary>
        public static bool Dismiss()
        {
            GalaxyInspect mode = _driving;
            if (!_live || mode == null)
            {
                return false;
            }

            mode.Leave();
            return true;
        }

        /// <summary>Take the cell down and say so, leaving the tree cursor exactly where it is.
        /// <see cref="Exit(bool, ControlId, bool)"/>'s teardown without its landing.</summary>
        private void Leave()
        {
            _live = false;
            _driving = null;
            _wasOnMap = false;
            _resume = 0;
            _leaps.Clear();
            _entry = null;
            InspectMarker.Hide();
            _aim.Clear();
            PointerFocus.Release();
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator != null)
            {
                navigator.ClearVisual();
                navigator.ReleaseBuffer();
            }

            Voice.Say(ModStrings.Get(ModStrings.GalaxyInspectExited), true);
        }

        /// <summary>
        /// One key, offered to the mode before anything else on the page sees it. True when the mode
        /// took it.
        /// </summary>
        public bool HandleKey(string actionKey)
        {
            try
            {
                if (actionKey == MapActions.Inspect)
                {
                    // A mode of the map is armed ON the map, and nowhere else: pressed while the
                    // cursor is standing on the zoom slider or the turn controls the key is not the
                    // mod's at all - it is not taken, nothing is said, and the cursor does not move.
                    if (!OnMap())
                    {
                        return false;
                    }

                    // Ctrl+I only ARMS the cursor. Pressed again while it is up it does nothing at
                    // all, silently - the same ruling as Enter on a cell with nothing in it (owner's
                    // ruling): the key is pressed speculatively in the middle of a sweep, and a
                    // toggle that dropped the cursor there would cost the player the cell they were
                    // standing on. Leaving is Escape, a landing that Enter made, or the map itself
                    // going away. The key is still TAKEN, so the page underneath never sees it.
                    if (!_live)
                    {
                        Enter();
                    }

                    return true;
                }

                // Suspended is not up: off the map widget every key below belongs to whatever the
                // player is standing on, and the mode hears none of them.
                if (!Active)
                {
                    return false;
                }

                switch (actionKey)
                {
                    case UiActions.Up:
                        return Move(0, 1);
                    case UiActions.Down:
                        return Move(0, -1);
                    case UiActions.Left:
                        return Move(-1, 0);
                    case UiActions.Right:
                        return Move(1, 0);
                    case UiActions.Activate:
                        return Activate();
                    case UiActions.Back:
                        Exit(false);
                        return true;
                    case UiActions.Secondary:
                        return PopCell();
                    case MapActions.InspectGrow:
                        return Resize(InspectGrid.Grow(_size));
                    case MapActions.InspectShrink:
                        return Resize(InspectGrid.Shrink(_size));
                    // The two coarse-step chords belong to the CELL while the cursor is driving the
                    // map: a coarse step means nothing on a map stop, and the zoom slider that the
                    // chord really adjusts is a stop of its own, where this mode is suspended and
                    // hears nothing at all.
                    case UiActions.CoarseDecrease:
                        return Skip(-1, 0);
                    case UiActions.CoarseIncrease:
                        return Skip(1, 0);
                    case MapActions.InspectSkipNorth:
                        return Skip(0, 1);
                    case MapActions.InspectSkipSouth:
                        return Skip(0, -1);
                    case MapActions.InspectFollowWest:
                        return FollowWest();
                    case MapActions.InspectFollowEast:
                        return FollowEast();
                }

                return false;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the inspect cursor threw on " + actionKey + ": " + e);
                return false;
            }
        }

        /// <summary>The page is going. The mode goes with it, and says so - queued for after whatever
        /// replaced the page has announced itself.</summary>
        public void Forget()
        {
            if (_live)
            {
                Exit(true);
            }
            else
            {
                InspectMarker.Hide();
            }
        }

        /// <summary>
        /// Per frame, from the page.
        ///
        /// The one thing that has to be noticed between keypresses is the player coming BACK to the map
        /// with the cursor still up: Tab took them to another stop, the mode went quiet there, and now
        /// the arrows mean the cell again. So the cell is read out - the same sentence a move reads, and
        /// no new words - which is how the player hears that the mode is driving again.
        ///
        /// It waits a few frames rather than speaking on the frame the focus changed: the stop being
        /// arrived at announces itself in a burst that interrupts, and a line queued into the middle of
        /// that burst is thrown away.
        ///
        /// EVERY resume speaks. A jump made while the mode is parked deliberately lands the player
        /// back on the map in silence, so that this reading is the one utterance it makes
        /// (<see cref="MoveTo"/>).
        /// </summary>
        public void Update()
        {
            if (_live && !ShowsTheGalaxy())
            {
                // The camera has gone past the map this mode reads. Every route in is the same
                // frame's answer - the slider, the mod's own zoom keys, the game's held PageUp,
                // the wheel - because the question asked here is where the camera IS and never
                // which key moved it.
                Leave();
                return;
            }

            bool onMap = Active;
            if (onMap && !_wasOnMap)
            {
                _resume = ResumeFrames;
            }
            else if (!onMap && _wasOnMap)
            {
                // Suspended. The review buffer goes back to the stop the player is now standing on -
                // the cell is not what they are reading while they are off the map, and it is put back
                // by the same reading that says the cell out loud when they return.
                GraphNavigator suspended = ModEntry.Navigator;
                if (suspended != null)
                {
                    suspended.ReleaseBuffer();
                }
            }

            _wasOnMap = onMap;
            if (_resume <= 0)
            {
                return;
            }

            if (--_resume != 0 || !onMap)
            {
                return;
            }

            Voice.Say(Look(), false);
        }

        /// <summary>How long the resume line waits for the stop the player has just landed on to finish
        /// announcing itself - a fifth of a second, several times the gap between the parts of one
        /// arrival.</summary>
        private const int ResumeFrames = 12;

        // ---- the mode ----

        private static bool _live;

        /// <summary>The mode that has the map, so the pump can point the game's own tooltip at the
        /// cell (<see cref="Point"/>). Static for the same reason the rest of the mode's state is:
        /// there is exactly one map, and the pump has no page to ask.</summary>
        private static GalaxyInspect _driving;

        private static string _pending;
        private static string _spokenWhenLeft;
        private static string _lastHeard;
        private static int _waited;
        private static int _quiet;

        private readonly GalaxyHudScreen _screen;

        /// <summary>Whether the player was standing on the map last frame, and what is left of the wait
        /// before the cell is read out again (<see cref="Update"/>).</summary>
        private bool _wasOnMap;

        private int _resume;

        /// <summary>Where the cursor is, in the pair the map is spoken in - whole units from home.
        /// </summary>
        private int _x;
        private int _y;

        /// <summary>What the cell is standing on, in the order it was read out - the things the
        /// pointer offers the game's own tooltip, best first (<see cref="PointAtCell"/>). The things
        /// themselves and not their widgets: the map pools its labels and re-points them as the
        /// camera slides.</summary>
        private readonly List<IGameEntityWithGalaxyPosition> _aim =
            new List<IGameEntityWithGalaxyPosition>();

        /// <summary>How wide the cell is. An instance field, so the size the player settled on is
        /// still there the next time they open the mode in this session.</summary>
        private int _size = InspectGrid.DefaultSize;

        /// <summary>The control the mode was opened from, so leaving it puts the player back where
        /// they were rather than wherever the tree happens to seat them.</summary>
        private ControlId _entry;

        /// <summary>Where on the map that control stands, kept so that leaving takes the CAMERA back
        /// to it as well as the cursor (<see cref="Recentre"/>).</summary>
        private GalaxyPosition _entryAt;

        // The galaxy's own extent, in the same pair the cursor is held in - worked out once per entry.
        private double _lowX;
        private double _highX;
        private double _lowY;
        private double _highY;

        private bool Enter()
        {
            return Enter(false, 0, 0);
        }

        /// <summary>The entry itself. <paramref name="placed"/> is the scanner's entry
        /// (<see cref="ArmAt"/>), which chooses the square the cell opens on; the place the player is
        /// STANDING is remembered either way, because that is where leaving puts the cursor and the
        /// camera back.</summary>
        private bool Enter(bool placed, int atX, int atY)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GalaxyPosition at;
            bool stands = FocusedPlace(navigator, out at);
            if (!stands && !placed)
            {
                // THE CURSOR IS ARMED ON A PLACE, and a row that is a heading rather than a place has
                // none to arm on (owner ruling 2026-08-31) - a constellation, and anything else that
                // groups rows without standing anywhere itself. Silently nothing, exactly as the set
                // key answers the same rows: the chord was pressed for somewhere, and there is no
                // somewhere here to refuse. Nothing is touched on the way out - not the search, not
                // the entry memo - because nothing happened.
                return false;
            }

            _entry = navigator == null ? null : navigator.FocusedKey;
            if (navigator != null)
            {
                // Whatever was being searched for, the mode is not where it applies: from here typing
                // is inert (GalaxyHudScreen.SuspendsTypeahead) and the results could not be stepped
                // anyway, so a search left standing would only outlive the mode and step the player
                // about a page they had stopped looking at. Silently, like every other search that
                // stopped applying to where the player is - the entry line is the mode's news.
                navigator.ClearSearch();
            }

            if (stands)
            {
                _entryAt = at;
                double east;
                double north;
                GalaxyCoordinates.Offsets(at, out east, out north);
                _x = MapCoordinates.Round(east);
                _y = MapCoordinates.Round(north);
            }
            else
            {
                // A square the SCANNER chose, arriving with no row under the cursor to measure from -
                // the camera goes back to home when the mode ends, the one place every player knows.
                _entryAt = GalaxyCoordinates.Origin();
                _x = 0;
                _y = 0;
            }

            if (placed)
            {
                _x = atX;
                _y = atY;
            }

            MeasureGalaxy();
            // Nothing has been crossed yet, so the first reading names the stretch of sky the cursor
            // opened in and says nothing at all where it opened between them.
            _sky = null;
            _skyKnown = false;
            _bubbles = CellInfluence.Nothing;
            _bubblesKnown = false;
            _live = true;
            _driving = this;
            // Entry announces the mode and reads the cell itself; the resume line is for coming BACK.
            _wasOnMap = true;
            _resume = 0;
            _leaps.Clear();
            // A camera closer than the ceiling is pulled out to it, so enough of the map is visible
            // around the square; one already further out is the player's own choice and stays
            // (owner's ruling - the floor guarantees visibility, it does not impose a framing).
            int zoom = GalaxyViewLevels.ZoomStep;
            if (zoom > EntryZoomCeiling)
            {
                GalaxyPosition opened = GalaxyCoordinates.Origin();
                GalaxyViewLevels.SetZoom(
                    EntryZoomCeiling,
                    new Vector3(opened.X + _x, 0f, opened.Y + _y)
                );
            }
            Voice.Say(
                new MessageBuilder()
                    .Fragment(ModStrings.Get(ModStrings.GalaxyInspectEntered))
                    .ListItemForcedComma(SizeText())
                    .Build(),
                true
            );
            Settle(false);
            return true;
        }

        /// <summary>Leave the mode. <paramref name="deferred"/> is the ending that happened during a
        /// screen change, whose line has to wait for the new page to finish announcing itself.
        /// </summary>
        private void Exit(bool deferred)
        {
            Exit(deferred, null, false);
        }

        /// <summary>
        /// EVERY way out of the mode says so (owner's ruling): the player has been driving a cell
        /// about the map, and the arrows meaning the tree again is news whether they left with Escape
        /// or by naming a thing with Enter. A select that only announced the thing it landed on left
        /// the player unable to tell a landing from a cell that happened to hold one thing.
        ///
        /// <paramref name="selected"/> is that second way out - a key of the mode sending the player
        /// somewhere rather than putting them back where they started - and it is what decides the two
        /// things that differ: whatever the cursor ends up on is announced (<paramref name="landing"/>
        /// where the tree has a row for the thing, and the stop the mode was opened from where it does
        /// not), and the camera is left on what was just landed on rather than flown back.
        /// </summary>
        private void Exit(bool deferred, ControlId landing, bool selected)
        {
            _live = false;
            _driving = null;
            _wasOnMap = false;
            _resume = 0;
            _leaps.Clear();
            InspectMarker.Hide();
            // The game's tooltip was the CELL's while the mode drove the map (Point), so the mode
            // takes its own aim down and the control the tree cursor is standing on takes the pointer
            // back. That control has to be asked for its focus visual again: focus itself never
            // moved, so nothing else on the frame would ask.
            _aim.Clear();
            PointerFocus.Release();
            GraphNavigator navigator = ModEntry.Navigator;
            // ESCAPE RESTORES: the row the mode was armed from, which nothing moved while it was up
            // (owner ruling 2026-08-31). Where that row has DIED under the player - a bookmark whose
            // slot a dedupe took, a fleet the tree has re-filed - the nearest thing still standing
            // near where it stood answers instead, because the place is what they meant. A landing
            // Enter made overrides both: that is the player choosing somewhere, not coming back.
            ControlId leaveOn = landing ?? _screen.RestoreRow(_entry, _entryAt);
            if (navigator != null)
            {
                navigator.ClearVisual();
                // The cell's reading goes out of the review buffer with the cell, and the control the
                // cursor is left on fills it again on the next frame.
                navigator.ReleaseBuffer();
                // EVERY exit says where the cursor now stands, even where it is the very stop the
                // mode was opened from: focus never moved while the mode was up, so the ordinary
                // "only when the cursor moved" rule would leave the player hearing the exit line and
                // nothing about where they are (owner-reported twice - first for the Enter landing,
                // then for Escape; an earlier revision kept Escape quiet and that was wrong).
                //
                // Promised to the ROW BEING LEFT ON rather than to "whatever comes next", because the
                // two differ in the one case that is not the ordinary one: when the armed-from row has
                // DIED, the tree has already re-seated the cursor onto some neighbour of its own
                // choosing and the restore lands somewhere else again (<see cref="GalaxyHudScreen.RestoreRow"/>).
                // A promise to "next" was spent reading that neighbour out before the real landing
                // arrived - measured, two utterances. Aimed at the target, the neighbour stays silent
                // and the exit says exactly one landing.
                if (deferred || leaveOn == null)
                {
                    navigator.AnnounceNextLanding();
                }
                else
                {
                    navigator.AnnounceLandingAt(leaveOn);
                }
            }

            // Queued behind this line, both of them: the landing above announces itself with a queued
            // line of the navigator's own, so the exit is heard first and whole.
            string line = ModStrings.Get(ModStrings.GalaxyInspectExited);
            if (deferred)
            {
                PrismSpeech speech = ModEntry.Speech;
                _spokenWhenLeft = speech == null ? null : speech.LastSpoken;
                _lastHeard = _spokenWhenLeft;
                _waited = WaitFrames;
                _quiet = QuietFrames;
                _pending = line;
            }
            else
            {
                Voice.Say(line, true);
                ControlId to = leaveOn;
                if (navigator != null && to != null)
                {
                    navigator.FocusNode(to);
                    // Only where leaving is all that happened. A landing that Enter made goes on to
                    // put the cursor somewhere else entirely, and pulling the camera back to where the
                    // mode was opened from would fly it off the very thing that was just landed on.
                    if (!selected)
                    {
                        Recentre();
                    }
                }
            }

            _entry = null;
        }

        /// <summary>
        /// Bring the camera back to the stop the mode was entered from.
        ///
        /// Leaving puts the cursor back where it was, and without this the picture stayed wherever the
        /// square had been swept to - so a player who walked the cursor sixty units out and pressed
        /// Escape was left reading a control that is not on the screen, and whoever is watching the
        /// screen beside them was looking at empty space. The tree's own focus visual would have moved
        /// the camera, but only where the CURSOR moved (a page seats its cursor without flying the
        /// camera off what the game has centred), and coming back from a mode is not a cursor move.
        ///
        /// The place is the one worked out on the way IN rather than asked of the cursor again: the
        /// cursor is put back in the same breath, and a page seats a cursor over the next frame or two.
        /// A stop that is no place at all - the HUD, the turn controls - opened the mode at home, so
        /// home is where leaving it puts the camera back.
        ///
        /// The same centring the cursor itself used, so the camera arrives the way it has been moving
        /// all along.
        /// </summary>
        private void Recentre()
        {
            GalaxyViewLevels.CenterOn(
                new Vector3(_entryAt.X, 0f, _entryAt.Y),
                CameraDamping
            );
        }

        /// <summary>Put the camera and the drawn square on the cell - the picture half of a move, with
        /// nothing said. Split out so a caller that will have the cell read out by another route can
        /// move it without speaking (<see cref="MoveTo"/>).</summary>
        private void Show()
        {
            GalaxyPosition origin = GalaxyCoordinates.Origin();
            GalaxyViewLevels.CenterOn(
                new Vector3(origin.X + _x, 0f, origin.Y + _y),
                CameraDamping
            );
            InspectMarker.Show(
                (float)(origin.X + InspectGrid.Low(_x, _size)),
                (float)(origin.X + InspectGrid.High(_x, _size)),
                (float)(origin.Y + InspectGrid.Low(_y, _size)),
                (float)(origin.Y + InspectGrid.High(_y, _size))
            );
        }

        /// <summary>Put the camera and the drawn square on the cell, then say what is in it.</summary>
        private void Settle(bool interrupt)
        {
            Show();
            // Ahead of the cell, and as its own line: it is news about the MOVE rather than about what
            // is standing here, which is the place the skip key's own count already occupies. The
            // cell's reading keeps the interrupt it was going to have; where both are said the
            // crossing takes it and the cell queues behind, so the pair is heard whole and in order.
            // The two crossings in the same breath, sky before influence: the constellation is the
            // bigger stretch of map and the border inside it is the finer news.
            bool first = true;
            string crossing = Crossing();
            if (crossing != null)
            {
                Voice.Say(crossing, interrupt);
                first = false;
            }

            IList<string> borders = Influence();
            for (int i = 0; borders != null && i < borders.Count; i++)
            {
                Voice.Say(borders[i], first && interrupt);
                first = false;
            }

            Voice.Say(Look(), first && interrupt);
        }

        /// <summary>Read the cell: the sentence it is said in, and what the game's own tooltip should
        /// be showing while the player stands here. One walk of the map for both, so what is drawn on
        /// the screen and what is in the player's ear can never be about different things.</summary>
        private string Look()
        {
            Contents contents = Read();
            Aim(contents);
            // Sampled once and handed to both readings: the fog is up to 121 lookups into the
            // empire's distance field, and the sentence and the buffer are the same cell.
            string fog = FogText();
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator != null)
            {
                navigator.OverrideBuffer(CellLines(contents, fog));
            }

            return CellText(contents, fog);
        }

        /// <summary>What the pointer offers the game's own tooltip, best first - the cell's own things
        /// in the order the cell names them.</summary>
        private void Aim(Contents contents)
        {
            _aim.Clear();
            for (int i = 0; i < contents.Places.Count; i++)
            {
                _aim.Add(contents.Places[i]);
            }

            for (int i = 0; i < contents.Special.Count; i++)
            {
                _aim.Add(contents.Special[i]);
            }

            for (int i = 0; i < contents.Fleets.Count; i++)
            {
                _aim.Add(contents.Fleets[i]);
            }

            for (int i = 0; i < contents.Probes.Count; i++)
            {
                Probe probe = contents.Probes[i].Probe;
                if (probe != null)
                {
                    _aim.Add(probe);
                }
            }

            for (int i = 0; i < contents.Projectiles.Count; i++)
            {
                _aim.Add(contents.Projectiles[i]);
            }

            for (int i = 0; i < contents.Pins.Count; i++)
            {
                _aim.Add(contents.Pins[i]);
            }
        }

        /// <summary>
        /// Point the game's own tooltip at the first thing in the cell the map has one for - which is
        /// the first thing the cell NAMES, so what a watcher sees is what the player was just told.
        /// The star system standing here wins over a fleet parked at it, and both over the motes
        /// between the stars (owner's ruling); a cell with nothing in it, or nothing the map draws a
        /// tooltip for, shows nothing at all.
        ///
        /// Walked every frame rather than resolved once when the cell moved: the camera is still
        /// sliding when the cell is read, and the label a tooltip hangs on may not be bound to this
        /// place until it arrives (<see cref="GalaxyHudScreen.MapMark"/>).
        /// </summary>
        private void PointAtCell()
        {
            for (int i = 0; i < _aim.Count; i++)
            {
                AgeTooltip tooltip;
                AgeTransform anchor;
                if (_screen.MapMark(_aim[i], out tooltip, out anchor))
                {
                    PointerFocus.MoveTo(null, tooltip, anchor);
                    return;
                }
            }

            PointerFocus.Release();
        }

        private string SizeText()
        {
            return ModStrings.Format(ModStrings.GalaxyInspectCursorSize, _size, _size);
        }

        // ---- what Enter does ----

        /// <summary>
        /// Enter names the ONE thing in the cell, where there is exactly one: the mode ends, saying
        /// so, and the cursor lands on it with the announcement that node makes for itself.
        ///
        /// A PLACE is anything the map draws in the place of a star - a star system, and equally the
        /// nebulae, dust clouds and other special nodes the galaxy is strung with, which have rows in
        /// the tree exactly as systems do. Counting only star systems here left Enter doing nothing at
        /// all on a cell holding one special node, which is a key that is claimed, taken, and then
        /// silent (owner-reported; measured on B10 6805, a Solar Nebula).
        ///
        /// A place wins over a fleet standing at it, because the fleet is a child of the place in the
        /// tree and landing on the place is a step away from it. Anything else - two places, two
        /// fleets and no place, an empty cell - does NOTHING AT ALL, silently: the key is pressed
        /// speculatively while sweeping, the cursor is right there to be shrunk, and a refusal on
        /// every empty cell of a sweep is noise (owner's ruling).
        /// </summary>
        private bool Activate()
        {
            Contents contents = Read();

            // ENTER'S ORDER IS DATA (<c>PlacedRows</c>): the tiers are walked from the top, and the
            // first one with anything in it decides. Exactly one candidate there is the landing;
            // several is the ambiguity this mode always answers with silence; a tier with nothing in
            // it hands on. What each tier CONTAINS is the only part of the order this file knows.
            for (int tier = 1; tier <= PlacedRows.Tiers; tier++)
            {
                List<IGameEntityWithGalaxyPosition> things =
                    new List<IGameEntityWithGalaxyPosition>();
                List<ControlId> rows = new List<ControlId>();
                Gather(contents, tier, things, rows);
                int found = things.Count + rows.Count;
                if (found == 0)
                {
                    continue;
                }

                if (found > 1)
                {
                    // Two stars, two fleets, a probe beside a missile: the key is pressed
                    // speculatively while sweeping and a wrong guess costs the player their place.
                    return true;
                }

                return rows.Count == 1 ? LandOnRow(rows[0]) : LandOnThing(contents, things[0]);
            }

            return true;
        }

        /// <summary>What the cell holds at one of Enter's tiers - the mapping from the cell's own
        /// lists onto the table's declarations. Things the PAGE can find a row for are gathered as
        /// entities; the two kinds whose row the cell already knows the identity of are gathered as
        /// rows.</summary>
        private void Gather(
            Contents contents,
            int tier,
            List<IGameEntityWithGalaxyPosition> things,
            List<ControlId> rows
        )
        {
            if (tier == PlacedRows.TierPlace)
            {
                for (int i = 0; i < contents.Places.Count; i++)
                {
                    things.Add(contents.Places[i]);
                }

                for (int i = 0; i < contents.Special.Count; i++)
                {
                    things.Add(contents.Special[i]);
                }

                return;
            }

            if (tier == PlacedRows.TierFleet)
            {
                for (int i = 0; i < contents.Fleets.Count; i++)
                {
                    things.Add(contents.Fleets[i]);
                }

                return;
            }

            if (tier == PlacedRows.TierMover)
            {
                // The three the map draws out between the stars, closed as data on 2026-08-31: each
                // already had a row, a position and a place in every other inventory, and was missing
                // from this one alone.
                for (int i = 0; i < contents.Probes.Count; i++)
                {
                    Probe probe = contents.Probes[i].Probe;
                    if (probe != null)
                    {
                        things.Add(probe);
                    }
                }

                for (int i = 0; i < contents.Projectiles.Count; i++)
                {
                    things.Add(contents.Projectiles[i]);
                }

                for (int i = 0; i < contents.Pins.Count; i++)
                {
                    things.Add(contents.Pins[i]);
                }

                return;
            }

            if (tier == PlacedRows.TierMarker)
            {
                for (int i = 0; i < contents.Markers.Count; i++)
                {
                    MapTarget marker;
                    if (_screen.MarkerTarget(contents.Markers[i], out marker) && marker.Id != null)
                    {
                        rows.Add(marker.Id);
                    }
                }

                return;
            }

            if (tier == PlacedRows.TierBookmark)
            {
                for (int i = 0; i < contents.Bookmarks.Count; i++)
                {
                    ControlId spot = _screen.BookmarkedPoint(contents.Bookmarks[i]);
                    if (spot != null)
                    {
                        rows.Add(spot);
                    }
                }
            }
        }

        /// <summary>Leave the mode on a row the caller already has the identity of - a quest marker's,
        /// a bookmark's.</summary>
        private bool LandOnRow(ControlId row)
        {
            Exit(false, row, true);
            return true;
        }

        /// <summary>
        /// Leave the mode on the row the PAGE has for a thing it draws.
        ///
        /// Asked of the page, and asked before the mode is taken down: the page knows where each thing
        /// it draws lives in the tree and opens the branch that holds it on the way
        /// (<c>GalaxyHudScreen.NodeFor</c> - the same landing the scanner's go-to makes).
        /// </summary>
        private bool LandOnThing(Contents contents, IGameEntityWithGalaxyPosition thing)
        {
            ControlId landing = _screen.NodeFor(thing);
            Fleet fleet = thing as Fleet;
            if (landing == null && fleet == null)
            {
                return true;
            }

            Exit(false, landing, true);
            if (landing != null)
            {
                return true;
            }

            // A fleet the tree has no row for - parked at a system the map does not name, or flying a
            // lane it does not draw. The only "go to this fleet" the game has for one is the camera and
            // the selection, and it announces nothing of its own, so the cell's own reading is said
            // again: the same answer the scanner gives for the same fleet. QUEUED behind the exit line
            // the mode has just said, which the player is owed whole.
            GalaxyHudScreen.SelectFleet(fleet);
            Voice.Say(CellText(contents, FogText()), false);
            return true;
        }

        // ---- the galaxy the cursor moves in ----

        /// <summary>
        /// How far the cursor may go, in the pair it is held in: the bounding box of every node in the
        /// galaxy, whether or not the player has seen it.
        ///
        /// Not the game's own <c>IVisibilityService.GalaxyBounds</c>, which is the fog field's rect and
        /// is scaled 2.5 times the galaxy (<c>VisibilityController.GalaxyBoundsScaleFactor</c>) - a
        /// cursor allowed out there would sweep a wilderness with nothing in it. And not the bounds of
        /// what the player has EXPLORED either: the size of the galaxy is chosen at setup and is not a
        /// secret, and an edge that moved as the fog lifted would make the same key refuse on Tuesday
        /// and work on Wednesday.
        ///
        /// The box itself comes from <see cref="GalaxyFrame.Edges"/> rather than being swept here,
        /// because a probe's bearings are measured to the SAME box: the rim a bearing names ("to the
        /// map edge at 40") is then the rim this cursor refuses at, and the two can never disagree.
        /// Home is subtracted from the game's own coordinates to reach the pair the cursor is held in -
        /// exactly what <see cref="GalaxyCoordinates.Offsets"/> does per node, and subtracting one
        /// number from every node commutes with taking their extremes.
        /// </summary>
        private void MeasureGalaxy()
        {
            _lowX = 0.0;
            _highX = 0.0;
            _lowY = 0.0;
            _highY = 0.0;
            try
            {
                double west;
                double east;
                double south;
                double north;
                if (!GalaxyFrame.Extent(out west, out east, out south, out north))
                {
                    return;
                }

                GalaxyPosition home = GalaxyCoordinates.Origin();
                _lowX = west - home.X;
                _highX = east - home.X;
                _lowY = south - home.Y;
                _highY = north - home.Y;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: measuring the galaxy for the inspect cursor threw: " + e);
            }
        }

        /// <summary>
        /// Where the cursor the player is standing on IS, if it stands anywhere on the map.
        ///
        /// The node's OWN thing first, wherever it has one - a fleet, a probe, a missile, an ally's
        /// pin. Those rows are keyed structurally (a fleet's key names the system the map draws it at),
        /// so the id carries no backing object and the page is asked instead
        /// (<see cref="GalaxyHudScreen.PositionOf"/>). Then the node itself, then the nearest ancestor
        /// that is a place: a planet or a starlane under an opened system answers with that system,
        /// which is where the map draws it. False for the clusters round the edge of the screen, which
        /// are not places at all.
        ///
        /// Shared with the scanner, which measures from the same place for the same reason: it is where
        /// the player is reading, and standing on a fleet's row means reading from that fleet rather
        /// than from the star it is filed under.
        /// </summary>
        internal static bool FocusedPlace(GraphNavigator navigator, out GalaxyPosition position)
        {
            position = default(GalaxyPosition);
            GraphNode node = navigator == null ? null : navigator.CurrentNode;
            GalaxyHudScreen screen =
                navigator == null ? null : navigator.Screen as GalaxyHudScreen;
            for (int depth = 0; node != null && depth < 16; depth++)
            {
                // The row's OWN place, decided by the registry (<c>PlacedRows</c>): a kind that
                // stands somewhere answers, a GROUPING refuses however well-placed the entity behind
                // it is, and a row carried by an ancestor says nothing and lets this walk go up to its
                // star. That last is why the walk is still a walk - a planet, a lane, a dossier and a
                // berthed fleet all arm at the star the map draws them at.
                if (screen != null && screen.RowPlace(node.Id, out position))
                {
                    return true;
                }

                node = node.Parent;
            }

            return false;
        }
    }
}

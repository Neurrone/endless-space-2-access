using System;
using System.Collections.Generic;
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
    internal sealed class GalaxyInspect
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
        /// leaving keeps whatever the player zoomed to.</summary>
        private const int EntryZoomCeiling = 8;

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
        /// </summary>
        public void Update()
        {
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

            if (--_resume == 0 && onMap)
            {
                Voice.Say(Look(), false);
            }
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

        private void Enter()
        {
            Enter(false, 0, 0);
        }

        /// <summary>The entry itself. <paramref name="placed"/> is the scanner's entry
        /// (<see cref="ArmAt"/>), which chooses the square the cell opens on; the place the player is
        /// STANDING is remembered either way, because that is where leaving puts the cursor and the
        /// camera back.</summary>
        private void Enter(bool placed, int atX, int atY)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            _entry = navigator == null ? null : navigator.FocusedKey;
            GalaxyPosition at;
            if (FocusedPlace(navigator, out at))
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
                // Home, which is where the pair "0, 0" is - the one place on this map every player
                // already knows.
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
            InspectMarker.Hide();
            // The game's tooltip was the CELL's while the mode drove the map (Point), so the mode
            // takes its own aim down and the control the tree cursor is standing on takes the pointer
            // back. That control has to be asked for its focus visual again: focus itself never
            // moved, so nothing else on the frame would ask.
            _aim.Clear();
            PointerFocus.Release();
            GraphNavigator navigator = ModEntry.Navigator;
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
                navigator.AnnounceNextLanding();
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
                ControlId to = landing ?? _entry;
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

        private bool Move(int east, int north)
        {
            int x = InspectGrid.Step(_x, _size, east);
            int y = InspectGrid.Step(_y, _size, north);
            if (!InspectGrid.InBounds(x, y, _size, _lowX, _highX, _lowY, _highY))
            {
                Voice.Say(ModStrings.Get(ModStrings.GalaxyInspectEdge), true);
                return true;
            }

            _x = x;
            _y = y;
            Settle(true);
            return true;
        }

        /// <summary>
        /// GO TO THE NEXT INTERESTING CELL in one press - the modified arrow.
        ///
        /// A sweep of this map is mostly empty: between two stars there can be twenty cells with
        /// nothing in them, each of which the plain arrow reads out as its own pair. So the modified
        /// arrow walks the same cells the plain one would, in the same steps, and stops at the first
        /// one that is not what the player is standing on (<see cref="CellSkip"/>) - which makes it
        /// "take me to the next thing over there", the question a sighted player answers by looking
        /// along the row.
        ///
        /// The number of cells passed over is said first, and only where there were any: hearing "12"
        /// is how the player knows the next thing is a long way off rather than next door, and a
        /// "skipped 0" on every ordinary step would be a word on most presses. The landing itself is
        /// an arrow key's - camera, square, and the cell read out - queued behind that line so both
        /// are heard whole.
        ///
        /// The walk re-reads every cell it passes, which is the cell scan run once per candidate. That
        /// is a keypress's cost and never a frame's, and it is what keeps the skip and the arrows
        /// telling the same story about what is in a cell.
        /// </summary>
        private bool Skip(int east, int north)
        {
            int x;
            int y;
            int skipped;
            if (
                !CellSkip.Find(
                    _x,
                    _y,
                    _size,
                    east,
                    north,
                    CellInBounds,
                    SignatureAt,
                    out x,
                    out y,
                    out skipped
                )
            )
            {
                // Not one step possible - the same answer the plain arrow gives in this position.
                Voice.Say(ModStrings.Get(ModStrings.GalaxyInspectEdge), true);
                return true;
            }

            _x = x;
            _y = y;
            if (skipped <= 0)
            {
                Settle(true);
                return true;
            }

            Voice.Say(
                ModStrings.Plural(
                    ModStrings.GalaxyInspectSkippedOne,
                    ModStrings.GalaxyInspectSkippedMany,
                    skipped
                ),
                true
            );
            Settle(false);
            return true;
        }

        private bool CellInBounds(int x, int y)
        {
            return InspectGrid.InBounds(x, y, _size, _lowX, _highX, _lowY, _highY);
        }

        /// <summary>
        /// What a cell IS, for the purpose of deciding whether the cursor should stop in it: the
        /// identity of everything its reading would name, and how much of it is fogged.
        ///
        /// Read through the mode's own cell reading rather than from a second walk of the galaxy, so
        /// that the skip can never disagree with the sentence the player hears when it lands: the
        /// cursor is moved to the candidate, the cell is read, and the cursor is put back.
        /// </summary>
        private CellSignature SignatureAt(int x, int y)
        {
            int wasX = _x;
            int wasY = _y;
            _x = x;
            _y = y;
            try
            {
                Contents contents = Read();
                List<string> things = new List<string>();
                Identify(things, "place", contents.Places);
                Identify(things, "place", contents.Special);
                Identify(things, "fleet", contents.Fleets);
                for (int i = 0; i < contents.Probes.Count; i++)
                {
                    Probe probe = contents.Probes[i].Entity as Probe;
                    if (probe != null)
                    {
                        things.Add("probe:" + probe.GUID);
                    }
                }

                Identify(things, "shot", contents.Projectiles);
                Identify(things, "pin", contents.Pins);
                for (int i = 0; i < contents.Links.Count; i++)
                {
                    things.Add("lane:" + contents.Links[i].GUID);
                }

                // Whose influence covers the cell is part of what the cell IS, exactly as the star
                // standing in it is: a border is a thing a sighted player steers by, and a skip that
                // ran straight through one would carry the cursor from deep inside an empire to deep
                // inside the next without a word.
                CellNow().Reading.Tokens(things);
                return new CellSignature(things, Fog());
            }
            finally
            {
                _x = wasX;
                _y = wasY;
            }
        }

        private static void Identify<T>(List<string> things, string kind, List<T> found)
            where T : IGameEntity
        {
            for (int i = 0; i < found.Count; i++)
            {
                things.Add(kind + ":" + found[i].GUID);
            }
        }

        /// <summary>Which of the three the cell is - the same sampling the reading says out loud,
        /// bucketed. A COUNT here would stop the cursor on every cell along the edge of the explored
        /// map, since the number of fogged squares changes by one all the way down it.</summary>
        private CellFog Fog()
        {
            int fogged = Fogged();
            if (fogged <= 0)
            {
                return CellFog.Clear;
            }

            return fogged >= InspectGrid.Squares(_size) ? CellFog.Wholly : CellFog.Partly;
        }

        // ---- travelling by what the cell holds ----

        /// <summary>
        /// TRAVEL WEST ALONG THE ONE LANE HERE - the modified left arrow.
        ///
        /// A lane is the map's own long-distance geometry, and a cell sitting on one between two stars
        /// is exactly where a player wants to ask "where does this go". So the key takes the cursor to
        /// the lane's westmost end, which is the end the cell's own sentence names FIRST - the player
        /// has just heard "Star lane from Dusay to Heka", and this is the key that goes to Dusay.
        ///
        /// It acts whenever there is no ambiguity and refuses silently otherwise: exactly one lane in
        /// the cell and it travels, anything else and the key is taken and nothing happens. Fleets in
        /// the cell make no difference to it, because a fleet has no exposed origin at all - the map
        /// draws where a fleet is going and never where it came from - so there is nothing here for
        /// the westward key to compete with.
        /// </summary>
        private bool FollowWest()
        {
            Contents contents = Read();
            if (contents.Links.Count == 1)
            {
                GoTo(LaneEnd(contents.Links[0], true));
            }

            return true;
        }

        /// <summary>
        /// TRAVEL TO WHERE THINGS HERE ARE GOING - the modified right arrow.
        ///
        /// A fleet under way wins over the lane it is riding, because a fleet is the thing the player
        /// is following and the lane is only the road: the cursor goes to the node the fleet's leg is
        /// flying to, which is the very thing the tree files that fleet under
        /// (<see cref="GalaxyHudScreen.DestinationOf"/>) and so is drawn map knowledge rather than the
        /// simulation's own plan. NOTHING is read out of a fleet's route beyond it - a foreign fleet's
        /// orders are not the player's to see, and a fleet whose destination the map does not name
        /// contributes nothing and blocks nothing.
        ///
        /// Several fleets agreeing on one place is still no ambiguity, so they travel too; fleets
        /// heading for different places are, so the key is taken and nothing happens. With no fleet
        /// destination to be had the key falls back to the lane, the mirror of the westward one: the
        /// eastmost end, the one the cell's sentence names second.
        /// </summary>
        private bool FollowEast()
        {
            Contents contents = Read();
            GameNode goal = null;
            for (int i = 0; i < contents.Fleets.Count; i++)
            {
                GameNode heading = GalaxyHudScreen.DestinationOf(contents.Fleets[i]);
                if (heading == null || !MapVisibility.Perceived(heading, Gui.PlayerEmpire))
                {
                    continue;
                }

                if (goal == null)
                {
                    goal = heading;
                }
                else if (!ReferenceEquals(goal, heading))
                {
                    // Two fleets bound for two places: there is no one answer, so there is no move.
                    return true;
                }
            }

            if (goal != null)
            {
                GoTo(goal);
            }
            else if (contents.Links.Count == 1)
            {
                GoTo(LaneEnd(contents.Links[0], false));
            }

            return true;
        }

        /// <summary>
        /// Which end of a lane the two travel keys mean, in the order the cell's own sentence names
        /// them (<see cref="LaneText"/>): the westmost end first, and where two ends stand at the same
        /// longitude the southern one.
        ///
        /// A lane with only one end named says only that end - "Star lane from Dusay going north
        /// east" - so it has a first end and no second one: the westward key travels to the named end
        /// and the eastward key has nowhere to go. Somewhere the player has never seen is not a place
        /// the cursor may be sent to.
        /// </summary>
        private static GameNode LaneEnd(Link link, bool west)
        {
            Empire empire = Gui.PlayerEmpire;
            GameNode one = link.ExtremityNode1;
            GameNode two = link.ExtremityNode2;
            bool namedOne = MapVisibility.Perceived(one, empire);
            bool namedTwo = MapVisibility.Perceived(two, empire);
            if (namedOne && namedTwo)
            {
                bool oneFirst = InspectGrid.WestmostFirst(
                    one.GalaxyPosition.X,
                    one.GalaxyPosition.Y,
                    two.GalaxyPosition.X,
                    two.GalaxyPosition.Y
                );
                return west == oneFirst ? one : two;
            }

            return west ? (namedOne ? one : namedTwo ? two : null) : null;
        }

        /// <summary>Send the cursor to a place named by the cell's own contents - the scanner's own
        /// landing, on the ROUNDED pair the player is told, which is the pair that puts the thing
        /// inside even the one-unit cursor. Nowhere to go is the silent refusal every one of these
        /// keys makes, and the mode stays up through all of them.</summary>
        private void GoTo(GameNode node)
        {
            if (node == null)
            {
                return;
            }

            double east;
            double north;
            GalaxyCoordinates.Offsets(node.GalaxyPosition, out east, out north);
            JumpTo(MapCoordinates.Round(east), MapCoordinates.Round(north));
        }

        /// <summary>Grow or shrink the cell. A size that is not a CHANGE says nothing at all - the two
        /// ends of the ladder are where this happens, and a reading that repeated the size the player
        /// is already standing on would report a move that never took place. Silence is the refusal,
        /// the same way a slider already at its end refuses (owner ruling, 2026-08-19); the key is
        /// still taken, so the page underneath never sees it.</summary>
        private bool Resize(int size)
        {
            if (size == _size)
            {
                return true;
            }

            _size = size;
            Voice.Say(SizeText(), true);
            Settle(false);
            return true;
        }

        /// <summary>
        /// WHICH STRETCH OF SKY THE CELL IS IN, said only when it has changed.
        ///
        /// The map writes a constellation's name across the middle of a region and draws no boundary
        /// anywhere; the boundary the mod derives from the region's own members
        /// (<see cref="ConstellationMap"/>) is what makes "which one am I in" a question a walked cell
        /// can answer at all. It is a fact about the WHOLE stretch, so repeating it on every cell of a
        /// crossing would be most of what a sweep said: it is news on the press that changes it and
        /// silent on every other, which is the same rule the size key already follows.
        ///
        /// There are exactly two changes. Arriving in a named region says its name. Leaving the last
        /// named region for the space between them names the region being LEFT, because the space
        /// itself has no name and "out of Herkules" is the only thing that can be said about it that
        /// the player did not already know.
        ///
        /// Only the constellations this empire has EXPLORED are in the model, so a cell in a stretch of
        /// sky the map has drawn no name across is in no region at all and this says nothing about it -
        /// the fog's own reading is the whole answer there.
        ///
        /// Entering the mode on a cell that is in no region says nothing: there is nothing to name and
        /// nothing has been left.
        /// </summary>
        private string Crossing()
        {
            try
            {
                // The classification only depends on the cell, and Settle is called for a resize and a
                // re-centre as well as for a move - so the cell it was last asked about is remembered
                // and a Settle that did not move the cursor asks nothing.
                if (_skyKnown && _skyX == _x && _skyY == _y)
                {
                    return null;
                }

                Constellation now = ConstellationMap.Classify(_x, _y);
                Constellation was = _sky;
                bool known = _skyKnown;
                _sky = now;
                _skyKnown = true;
                _skyX = _x;
                _skyY = _y;
                if (known && ReferenceEquals(now, was))
                {
                    return null;
                }

                if (now != null)
                {
                    return ModStrings.Format(
                        ModStrings.GalaxyInspectConstellation,
                        now.LocalizedName
                    );
                }

                return known && was != null
                    ? ModStrings.Format(
                        ModStrings.GalaxyInspectConstellationLeft,
                        was.LocalizedName
                    )
                    : null;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: naming the cell's constellation threw: " + e);
                return null;
            }
        }

        /// <summary>The stretch of sky the cursor was last read as standing in, and the cell that
        /// answer was taken for. Reset on entry, never on a suspend: coming back to the map re-reads
        /// the cell the player left the cursor on, and a cell that has not moved has crossed nothing.
        /// </summary>
        private Constellation _sky;
        private bool _skyKnown;
        private int _skyX;
        private int _skyY;

        /// <summary>
        /// WHOSE INFLUENCE THE CELL IS STANDING IN, said only when it has changed.
        ///
        /// The map paints influence as a coloured disk with not one word on it, and the thing a player
        /// steering by it actually needs is not the disk but its BOUNDARY: a colony ship cannot settle
        /// inside somebody else's, a system inside one can change hands without a shot, and the edge is
        /// where both of those start being true. So the crossing is the news, exactly as the
        /// constellation's is (<see cref="Crossing"/>), and it is silent on every press that does not
        /// change it.
        ///
        /// Three things can be said. A cell PROVED to be one empire's throughout is "in" theirs; one
        /// the boundary runs through - the rim of a circle, the line between two empires, or a cell the
        /// proof could not settle - is the "edge of" theirs, and several empires holding parts of one
        /// cell collapse into one line rather than one apiece. Stepping out into space nobody reaches
        /// names what was LEFT, for the same reason the constellation crossing does.
        ///
        /// The contested line rides along with all three: an empire whose circle reaches into the cell
        /// without holding any of it is the overlap the map draws as colour on colour, and it is the
        /// same sentence a system's own row says it in.
        ///
        /// The comparison is on the whole SET - who holds it, how much, and who is reaching - so a
        /// crossing that only changes the contest still speaks, and a sweep along a border does not
        /// repeat the border.
        ///
        /// A cell wholly under the fog says nothing about influence at all, and contributes nothing to
        /// the cell's identity for the skip: the fog's own reading is the whole answer there, exactly
        /// as it is for the constellation.
        /// </summary>
        private IList<string> Influence()
        {
            try
            {
                // The SIZE is part of the key where the constellation's is not: growing the cursor
                // over a rim really does take the cell from inside a circle to across its edge, and
                // that is a crossing the player made with the size key.
                if (
                    _bubblesKnown
                    && _bubblesX == _x
                    && _bubblesY == _y
                    && _bubblesSize == _size
                )
                {
                    return null;
                }

                CellInfluence now = CellNow();
                CellInfluence was = _bubbles;
                bool known = _bubblesKnown;
                _bubbles = now;
                _bubblesKnown = true;
                _bubblesX = _x;
                _bubblesY = _y;
                _bubblesSize = _size;
                if (known && now.Reading.Equals(was.Reading))
                {
                    return null;
                }

                List<string> lines = new List<string>();
                if (now.Silent)
                {
                    // Nothing here and nothing reaching for it: the only thing to say is what the
                    // cursor has just walked out of, and nothing at all where it walked out of nowhere.
                    Line(lines, known ? SystemInfluence.LeftBehind(was) : null);
                    return lines;
                }

                Line(lines, SystemInfluence.Whose(now));
                Line(lines, SystemInfluence.ContestedIn(now));
                return lines;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: naming the influence over the cell threw: " + e);
                return null;
            }
        }

        /// <summary>The influence over the cell the cursor is standing on now - the mode's own fog gate
        /// in front of it, so a square of map nobody has explored is told nothing about.</summary>
        private CellInfluence CellNow()
        {
            if (Fogged() >= InspectGrid.Squares(_size))
            {
                return CellInfluence.Nothing;
            }

            GalaxyPosition origin = GalaxyCoordinates.Origin();
            return SystemInfluence.OverCell(
                origin.X + InspectGrid.Low(_x, _size),
                origin.Y + InspectGrid.Low(_y, _size),
                origin.X + InspectGrid.High(_x, _size),
                origin.Y + InspectGrid.High(_y, _size),
                Gui.PlayerEmpire
            );
        }

        /// <summary>Whose influence the cursor was last read as standing in, and the cell that answer
        /// was taken for - the same memo the constellation crossing keeps, and for the same reason: a
        /// resize or a re-centre calls Settle without moving the cursor, and a crossing that did not
        /// happen must not speak.</summary>
        private CellInfluence _bubbles = CellInfluence.Nothing;
        private bool _bubblesKnown;
        private int _bubblesX;
        private int _bubblesY;
        private int _bubblesSize;

        /// <summary>Put the camera and the drawn square on the cell, then say what is in it.</summary>
        private void Settle(bool interrupt)
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
                Probe probe = contents.Probes[i].Entity as Probe;
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
            int places = contents.Places.Count + contents.Special.Count;
            IGameEntityWithGalaxyPosition thing = null;
            if (places == 1)
            {
                thing = contents.Places.Count == 1 ? contents.Places[0] : contents.Special[0];
            }
            else if (places == 0 && contents.Fleets.Count == 1)
            {
                thing = contents.Fleets[0];
            }

            if (thing == null)
            {
                return true;
            }

            // Asked of the PAGE, and asked before the mode is taken down: the page knows where each
            // thing it draws lives in the tree and opens the branch that holds it on the way
            // (GalaxyHudScreen.NodeFor - the same landing the scanner's "go to" makes). Reaching for
            // the system's id directly was the old way, and it could only ever answer for a system: a
            // fleet's row hangs under whichever system the map files it at, and Enter on a fleet
            // therefore ended the mode and landed on nothing at all (owner-reported).
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

        // ---- what is in the cell ----

        /// <summary>The things the map is drawing inside the cell, gathered once so the reading and
        /// Enter cannot disagree about what is there.</summary>
        private sealed class Contents
        {
            public readonly List<StarSystemNode> Places = new List<StarSystemNode>();
            public readonly List<StarSystemNode> Special = new List<StarSystemNode>();
            public readonly List<Fleet> Fleets = new List<Fleet>();
            public readonly List<ProbeLabel> Probes = new List<ProbeLabel>();
            public readonly List<ObliteratorProjectile> Projectiles =
                new List<ObliteratorProjectile>();
            public readonly List<CoordinationRequest> Pins = new List<CoordinationRequest>();
            public readonly List<string> Lanes = new List<string>();

            /// <summary>The lanes themselves, in the order the sentence names them - what the travel
            /// keys need (a lane's ends) and what tells one cell's lanes from another's when the skip
            /// compares two cells. Kept beside the sentences rather than instead of them: the reading
            /// is the same one lane list said out loud.</summary>
            public readonly List<Link> Links = new List<Link>();
        }

        /// <summary>
        /// The whole cell as one line: where it is, then what is in it.
        ///
        /// The coordinates come FIRST because they are the answer to the question the mode exists for -
        /// the player is moving about a map, and every reading has to say where they now are, whether
        /// or not anything is there. An empty cell says the pair and stops; there is no word for
        /// "empty", because hearing the pair alone IS the answer and a word on every empty cell of a
        /// sweep would be most of what the sweep said.
        /// </summary>
        private string CellText(Contents contents, string fog)
        {
            MessageBuilder message = new MessageBuilder();
            message.Fragment(MapCoordinates.Text(_x, _y, 0.0, 0.0));
            for (int i = 0; i < contents.Places.Count; i++)
            {
                Place(message, contents.Places[i]);
            }

            for (int i = 0; i < contents.Special.Count; i++)
            {
                Place(message, contents.Special[i]);
            }

            for (int i = 0; i < contents.Fleets.Count; i++)
            {
                Fleet fleet = contents.Fleets[i];
                message.ListItemForcedComma(fleet.LocalizedName);
                message.ListItemForcedComma(PairOf(fleet.GalaxyPosition));
            }

            for (int i = 0; i < contents.Probes.Count; i++)
            {
                ProbeLabel label = contents.Probes[i];
                Probe probe = label.Entity as Probe;
                // Named the way the tree's own probe node names it - off the dossier the map hangs on
                // the mote, since the game gives a probe no name of its own.
                message.ListItemForcedComma(AgeWidgets.TooltipTitle(label.Tooltip));
                if (probe != null)
                {
                    message.ListItemForcedComma(PairOf(probe.GalaxyPosition));
                }
            }

            // Projectiles then pins, which is the order the tree declares them in behind the probes
            // (GalaxyHudScreen.AddProbes/AddProjectiles/AddPins) - the same three things the map
            // draws out between the stars, heard in the same order however they are reached.
            for (int i = 0; i < contents.Projectiles.Count; i++)
            {
                ObliteratorProjectile shot = contents.Projectiles[i];
                message.ListItemForcedComma(
                    ModStrings.Get(ModStrings.GalaxyObliteratorProjectile)
                );
                message.ListItemForcedComma(PairOf(shot.GalaxyPosition));
            }

            for (int i = 0; i < contents.Pins.Count; i++)
            {
                CoordinationRequest pin = contents.Pins[i];
                message.ListItemForcedComma(GalaxyHudScreen.PinKind(pin));
                message.ListItemForcedComma(PairOf(pin.GalaxyPosition));
            }

            for (int i = 0; i < contents.Lanes.Count; i++)
            {
                message.ListItemForcedComma(contents.Lanes[i]);
            }

            message.ListItemForcedComma(fog);
            return message.Build();
        }

        /// <summary>
        /// The same cell, as the lines the player REVIEWS it by - one per thing in it, in the order
        /// the sentence names them.
        ///
        /// The sentence is one breath and says everything; the buffer is for going back over it a
        /// thing at a time, which is the whole point of a cell that can hold a dozen. So the split is
        /// per THING and not per fragment: a fleet's name and where it stands are one line, because
        /// they are one answer.
        /// </summary>
        private List<string> CellLines(Contents contents, string fog)
        {
            List<string> lines = new List<string>();
            lines.Add(MapCoordinates.Text(_x, _y, 0.0, 0.0));
            for (int i = 0; i < contents.Places.Count; i++)
            {
                Line(lines, PlaceLine(contents.Places[i]));
            }

            for (int i = 0; i < contents.Special.Count; i++)
            {
                Line(lines, PlaceLine(contents.Special[i]));
            }

            for (int i = 0; i < contents.Fleets.Count; i++)
            {
                Fleet fleet = contents.Fleets[i];
                MessageBuilder line = new MessageBuilder();
                line.Fragment(fleet.LocalizedName);
                line.ListItemForcedComma(PairOf(fleet.GalaxyPosition));
                Line(lines, line);
            }

            for (int i = 0; i < contents.Probes.Count; i++)
            {
                ProbeLabel label = contents.Probes[i];
                Probe probe = label.Entity as Probe;
                MessageBuilder line = new MessageBuilder();
                line.Fragment(AgeWidgets.TooltipTitle(label.Tooltip));
                if (probe != null)
                {
                    line.ListItemForcedComma(PairOf(probe.GalaxyPosition));
                }

                Line(lines, line);
            }

            for (int i = 0; i < contents.Projectiles.Count; i++)
            {
                ObliteratorProjectile shot = contents.Projectiles[i];
                MessageBuilder line = new MessageBuilder();
                line.Fragment(ModStrings.Get(ModStrings.GalaxyObliteratorProjectile));
                line.ListItemForcedComma(PairOf(shot.GalaxyPosition));
                Line(lines, line);
            }

            for (int i = 0; i < contents.Pins.Count; i++)
            {
                CoordinationRequest pin = contents.Pins[i];
                MessageBuilder line = new MessageBuilder();
                line.Fragment(GalaxyHudScreen.PinKind(pin));
                line.ListItemForcedComma(PairOf(pin.GalaxyPosition));
                Line(lines, line);
            }

            for (int i = 0; i < contents.Lanes.Count; i++)
            {
                lines.Add(contents.Lanes[i]);
            }

            if (fog != null)
            {
                lines.Add(fog);
            }

            return lines;
        }

        /// <summary>One place as its own line - the same three things the sentence says about it, with
        /// no comma in front, because a line does not follow anything.</summary>
        private string PlaceLine(StarSystemNode node)
        {
            MessageBuilder message = new MessageBuilder();
            message.Fragment(node.LocalizedName);
            message.ListItemForcedComma(PairOf(node.GalaxyPosition));
            message.ListItemForcedComma(GalaxyHudScreen.SpecialKind(node));
            return message.Build();
        }

        private static void Line(List<string> lines, MessageBuilder message)
        {
            Line(lines, message.Build());
        }

        private static void Line(List<string> lines, string said)
        {
            if (!string.IsNullOrEmpty(said))
            {
                lines.Add(said);
            }
        }

        /// <summary>One place in the cell: its name, where it stands, and - where the map has drawn
        /// something other than a star system - what it is. The same three things the tree's own
        /// system node says, in the same order and separated the same way.</summary>
        private void Place(MessageBuilder message, StarSystemNode node)
        {
            message.ListItemForcedComma(node.LocalizedName);
            message.ListItemForcedComma(PairOf(node.GalaxyPosition));
            message.ListItemForcedComma(GalaxyHudScreen.SpecialKind(node));
        }

        /// <summary>Where a thing in the cell stands, said only where it is not simply the cell's own
        /// centre. At the smallest cursor every reading would otherwise say the same pair twice.
        /// </summary>
        private string PairOf(GalaxyPosition position)
        {
            double east;
            double north;
            GalaxyCoordinates.Offsets(position, out east, out north);
            int x = MapCoordinates.Round(east);
            int y = MapCoordinates.Round(north);
            return x == _x && y == _y ? null : MapCoordinates.Text(east, north, 0.0, 0.0);
        }

        /// <summary>
        /// Everything the map draws inside the cell.
        ///
        /// Nothing here decides for itself what may be seen: the places are the ones the map NAMES
        /// (<see cref="MapVisibility.Perceived"/>, the label window's own gate), the fleets are the
        /// ones the fleet labels draw (<see cref="FleetPresence.Drawing"/>), the probes, obliterator
        /// missiles and ally pins are the labels the page is already holding - the very lists the
        /// tree declares its own nodes from - and a lane is offered exactly where the map paints the
        /// line (<see cref="MapVisibility.Drawn"/>). A cell that reported something the map is hiding
        /// would be handing the player the galaxy's own model instead of its picture.
        /// </summary>
        private Contents Read()
        {
            Contents contents = new Contents();
            try
            {
                Empire empire = Gui.PlayerEmpire;
                if (empire == null || !GameGalaxy.Present())
                {
                    return contents;
                }

                List<StarSystemNode> perceived = new List<StarSystemNode>();
                foreach (StarSystemNode node in GameGalaxy.StarSystemNodes())
                {
                    if (!MapVisibility.Perceived(node, empire))
                    {
                        continue;
                    }

                    perceived.Add(node);
                    if (!Holds(node.GalaxyPosition))
                    {
                        continue;
                    }

                    if (node is SpecialNode)
                    {
                        contents.Special.Add(node);
                    }
                    else
                    {
                        contents.Places.Add(node);
                    }
                }

                IList<Fleet> fleets = FleetPresence.Drawing();
                for (int i = 0; i < fleets.Count; i++)
                {
                    if (Holds(fleets[i].GalaxyPosition))
                    {
                        contents.Fleets.Add(fleets[i]);
                    }
                }

                IList<ProbeLabel> probes = _screen.DrawnProbes;
                for (int i = 0; i < probes.Count; i++)
                {
                    Probe probe = probes[i] == null ? null : probes[i].Entity as Probe;
                    if (probe != null && Holds(probe.GalaxyPosition))
                    {
                        contents.Probes.Add(probes[i]);
                    }
                }

                IList<GalaxyHudScreen.SightedShot> shots = _screen.SightedProjectiles;
                for (int i = 0; i < shots.Count; i++)
                {
                    ObliteratorProjectile shot = shots[i].Shot;
                    if (Holds(shot.GalaxyPosition))
                    {
                        contents.Projectiles.Add(shot);
                    }
                }

                IList<GalaxyHudScreen.SightedPin> pins = _screen.SightedPins;
                for (int i = 0; i < pins.Count; i++)
                {
                    CoordinationRequest pin = pins[i].Request;
                    if (Holds(pin.GalaxyPosition))
                    {
                        contents.Pins.Add(pin);
                    }
                }

                Lanes(contents, perceived, empire);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading what is in the inspect cursor threw: " + e);
            }

            return contents;
        }

        /// <summary>Whether a point on the map is inside the cell, measured from home like everything
        /// else the player is told.</summary>
        private bool Holds(GalaxyPosition position)
        {
            double east;
            double north;
            GalaxyCoordinates.Offsets(position, out east, out north);
            return InspectGrid.Holds(_x, _y, _size, east, north);
        }

        /// <summary>
        /// The starlanes that CROSS the cell - which is a different question from the lanes leaving a
        /// system in it, since a cell of empty space between two stars holds the line and neither end
        /// of it.
        ///
        /// Each lane is named once however many of its ends are in the cell, and named the same way
        /// round every time (westmost end first - <see cref="InspectGrid.WestmostFirst"/>), so the same
        /// lane heard from two neighbouring cells is heard as one lane. A lane whose far end the map
        /// has not named says which way it runs instead, exactly as that lane's own node does under the
        /// system it leaves.
        ///
        /// Two gates, not one, and the second is what the cell needs beyond what the tree needs.
        /// <see cref="MapVisibility.Drawn"/> answers for the LINK - is this line lit at all - and the
        /// tree, which hangs a lane under the system it leaves, needs no more than that. A cell is a
        /// PLACE, and a lit line is not lit along the whole of its length
        /// (<see cref="Lit"/>): the geometry runs end to end whatever anyone has explored, and the fog
        /// cuts it short.
        /// </summary>
        private void Lanes(Contents contents, List<StarSystemNode> perceived, Empire empire)
        {
            HashSet<GameEntityGUID> seen = new HashSet<GameEntityGUID>();
            for (int i = 0; i < perceived.Count; i++)
            {
                StarSystemNode node = perceived[i];
                List<Link> links = node.Links;
                for (int j = 0; j < links.Count; j++)
                {
                    Link link = links[j];
                    bool wormhole = link is WormholeLink;
                    if (wormhole && !empire.HasWormholeTechnology)
                    {
                        continue;
                    }

                    if (!MapVisibility.Drawn(link, empire) || seen.Contains(link.GUID))
                    {
                        continue;
                    }

                    double eastOne;
                    double northOne;
                    double eastTwo;
                    double northTwo;
                    GalaxyCoordinates.Offsets(
                        link.ExtremityNode1.GalaxyPosition,
                        out eastOne,
                        out northOne
                    );
                    GalaxyCoordinates.Offsets(
                        link.ExtremityNode2.GalaxyPosition,
                        out eastTwo,
                        out northTwo
                    );
                    if (
                        !InspectGrid.Crosses(
                            _x,
                            _y,
                            _size,
                            eastOne,
                            northOne,
                            eastTwo,
                            northTwo
                        )
                    )
                    {
                        continue;
                    }

                    seen.Add(link.GUID);
                    if (!Lit(eastOne, northOne, eastTwo, northTwo))
                    {
                        continue;
                    }

                    string said = LaneText(link, empire, wormhole);
                    if (said != null)
                    {
                        contents.Lanes.Add(said);
                        contents.Links.Add(link);
                    }
                }
            }
        }

        /// <summary>
        /// Whether the map is really painting this lane INSIDE the cell.
        ///
        /// <see cref="MapVisibility.Drawn"/> is the link's half of the question and stops there: the
        /// line is built between the two extremity positions the moment the link is created
        /// (<c>GalaxyWarplink.Ignite</c>) and the intensity its exploration state buys is applied to
        /// the whole of it in one colour (<c>GalaxyLink.Refresh</c> passes the SAME state for both
        /// ends). What cuts a lit line short is the FOG, which is not a property of the line at all: it
        /// is a shader over the world - <c>FOWRendererService</c> publishes the empire's own distance
        /// field as a global texture the map's materials sample - so a lane running into unexplored
        /// space is drawn as a stub out of the end the player can see and fades to nothing where the
        /// fog begins. Measured on the fixture: the Xiu-to-Procyon lane draws nothing at all at
        /// (-2, -9) while the cell there named it (owner-reported, 2026-08-20).
        ///
        /// This is the one content class the cell reads whose gate does not settle WHERE the thing is
        /// drawn. Everything else the cell names is a LABEL, and a label window decides for itself
        /// whether to draw one at all; a lane is world geometry, and the fog is drawn over it.
        ///
        /// Asked at the same granularity the cell's own fog count is asked at
        /// (<see cref="Fogged"/>) - the whole-unit squares of the cell, sampled at their centres - so a
        /// cell that tells the player it is "Unexplored" can never also name a lane crossing it. At the
        /// default one-by-one cursor that is a single lookup.
        /// </summary>
        private bool Lit(double eastOne, double northOne, double eastTwo, double northTwo)
        {
            try
            {
                IVisibilityService visibility = Services.GetService<IVisibilityService>();
                Empire empire = Gui.PlayerEmpire;
                if (visibility == null || empire == null)
                {
                    // The same way out <see cref="Fogged"/> takes: with no fog field to ask, the map
                    // is not being described as foggier than it is.
                    return true;
                }

                GalaxyPosition origin = GalaxyCoordinates.Origin();
                int half = InspectGrid.HalfWidth(_size);
                for (int east = -half; east <= half; east++)
                {
                    for (int north = -half; north <= half; north++)
                    {
                        int x = _x + east;
                        int y = _y + north;
                        if (
                            !InspectGrid.Crosses(
                                x,
                                y,
                                1,
                                eastOne,
                                northOne,
                                eastTwo,
                                northTwo
                            )
                        )
                        {
                            continue;
                        }

                        if (
                            visibility.IsExplored(
                                empire,
                                new GalaxyPosition(origin.X + x, origin.Y + y)
                            )
                        )
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Log.Warn(
                    "galaxy: asking whether the map paints a lane in the inspect cursor threw: " + e
                );
                return true;
            }
        }

        /// <summary>One lane as the sentence the cell says it in.</summary>
        private static string LaneText(Link link, Empire empire, bool wormhole)
        {
            GameNode one = link.ExtremityNode1;
            GameNode two = link.ExtremityNode2;
            bool namedOne = MapVisibility.Perceived(one, empire);
            bool namedTwo = MapVisibility.Perceived(two, empire);
            if (namedOne && namedTwo)
            {
                bool oneFirst = InspectGrid.WestmostFirst(
                    one.GalaxyPosition.X,
                    one.GalaxyPosition.Y,
                    two.GalaxyPosition.X,
                    two.GalaxyPosition.Y
                );
                GameNode west = oneFirst ? one : two;
                GameNode east = oneFirst ? two : one;
                return ModStrings.Format(
                    wormhole ? ModStrings.GalaxyInspectWormhole : ModStrings.GalaxyInspectStarlane,
                    west.LocalizedName,
                    east.LocalizedName
                );
            }

            GameNode known = namedOne ? one : namedTwo ? two : null;
            GameNode dark = namedOne ? two : one;
            if (known == null)
            {
                return null;
            }

            return ModStrings.Format(
                wormhole
                    ? ModStrings.GalaxyInspectWormholeUnexplored
                    : ModStrings.GalaxyInspectStarlaneUnexplored,
                known.LocalizedName,
                CompassDirections.Direction(
                    dark.GalaxyPosition.X - known.GalaxyPosition.X,
                    dark.GalaxyPosition.Y - known.GalaxyPosition.Y
                )
            );
        }

        // ---- the fog ----

        /// <summary>
        /// How much of the cell the map has not lifted the fog from, said as the last thing in the
        /// reading.
        ///
        /// The fog is the one thing on this map that a sighted player reads from an ABSENCE - a grey
        /// wash over a region with nothing in it - and a reader of a list of contents would hear the
        /// same silence for "nothing there" and for "nobody has ever been there", which are opposite
        /// answers to the question the mode is for. So a cell wholly under it says so, and a cell
        /// partly under it says how many of its whole-unit squares are, which is what tells the player
        /// to shrink the cursor and find the edge of what they can see.
        ///
        /// The game answers it per POINT rather than per place (<c>IVisibilityService.IsExplored</c>
        /// samples the empire's fog-of-war distance field, the very field the fog is drawn from), so
        /// the cell is sampled at the centre of each of its squares - at most 121 lookups into a byte
        /// array, and only on the frame the cursor moved.
        /// </summary>
        private string FogText()
        {
            int fogged = Fogged();
            if (fogged <= 0)
            {
                return null;
            }

            return fogged >= InspectGrid.Squares(_size)
                ? ModStrings.Get(ModStrings.GalaxyInspectFog)
                : ModStrings.Plural(
                    ModStrings.GalaxyInspectFogOne,
                    ModStrings.GalaxyInspectFogMany,
                    fogged
                );
        }

        private int Fogged()
        {
            try
            {
                IVisibilityService visibility = Services.GetService<IVisibilityService>();
                Empire empire = Gui.PlayerEmpire;
                if (visibility == null || empire == null)
                {
                    return 0;
                }

                GalaxyPosition origin = GalaxyCoordinates.Origin();
                int half = InspectGrid.HalfWidth(_size);
                int fogged = 0;
                for (int east = -half; east <= half; east++)
                {
                    for (int north = -half; north <= half; north++)
                    {
                        GalaxyPosition at = new GalaxyPosition(
                            origin.X + _x + east,
                            origin.Y + _y + north
                        );
                        if (!visibility.IsExplored(empire, at))
                        {
                            fogged++;
                        }
                    }
                }

                return fogged;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: sampling the fog inside the inspect cursor threw: " + e);
                return 0;
            }
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
        /// </summary>
        private void MeasureGalaxy()
        {
            _lowX = 0.0;
            _highX = 0.0;
            _lowY = 0.0;
            _highY = 0.0;
            try
            {
                GameNode[] nodes = GameGalaxy.GameNodes();
                if (nodes == null || nodes.Length == 0)
                {
                    return;
                }

                bool first = true;
                for (int i = 0; i < nodes.Length; i++)
                {
                    if (nodes[i] == null)
                    {
                        continue;
                    }

                    double east;
                    double north;
                    GalaxyCoordinates.Offsets(nodes[i].GalaxyPosition, out east, out north);
                    if (first)
                    {
                        _lowX = _highX = east;
                        _lowY = _highY = north;
                        first = false;
                        continue;
                    }

                    if (east < _lowX)
                    {
                        _lowX = east;
                    }
                    else if (east > _highX)
                    {
                        _highX = east;
                    }

                    if (north < _lowY)
                    {
                        _lowY = north;
                    }
                    else if (north > _highY)
                    {
                        _highY = north;
                    }
                }
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
                if (screen != null && screen.PositionOf(node.Id, out position))
                {
                    return true;
                }

                IGameEntityWithGalaxyPosition placed =
                    node.Id == null ? null : node.Id.Reference as IGameEntityWithGalaxyPosition;
                if (placed != null)
                {
                    position = placed.GalaxyPosition;
                    return true;
                }

                node = node.Parent;
            }

            return false;
        }
    }
}

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
    /// THE KEYS ARE TAKEN AT MODE LEVEL. The arrows, Enter and Escape mean the cell rather than the
    /// cursor for exactly as long as the mode is up, and they are taken through <c>Screen.AnyKey</c> -
    /// the hook that is asked before the review chords and before navigation - which is the same
    /// displacement the map already lives with while the game has armed a targeting cursor. Escape is
    /// claimed FROM the game only while the mode is live (<see cref="Live"/>), or leaving the mode
    /// would also raise the pause menu; the two size keys are claimed the same way, so the game keeps
    /// its own keypad minus (Sleep for this turn) the rest of the time.
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

        public GalaxyInspect(GalaxyHudScreen screen)
        {
            _screen = screen;
        }

        /// <summary>Whether the cursor is up. Static because the input layer asks it while deciding
        /// what to claim from the game, and there is exactly one map.</summary>
        public static bool Live
        {
            get { return _live; }
        }

        /// <summary>What <c>ModInput</c>'s conditional claim asks: the size keys and Escape belong to
        /// the mod only while the cursor is up.</summary>
        public static bool KeysClaimed()
        {
            return _live;
        }

        /// <summary>
        /// Speak an ending that happened during a screen change, AFTER the page that took over has
        /// announced itself. Called from the pump right after the screens tick.
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
        public static void Tick()
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

        /// <summary>Drop the mode's process-wide state - mod teardown. The lines themselves go back
        /// when the page is popped, which happens first.</summary>
        public static void Reset()
        {
            _live = false;
            _pending = null;
            _spokenWhenLeft = null;
            _lastHeard = null;
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

                if (!_live)
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
                        Exit(true, false);
                        return true;
                    case MapActions.InspectGrow:
                        return Resize(InspectGrid.Grow(_size));
                    case MapActions.InspectShrink:
                        return Resize(InspectGrid.Shrink(_size));
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
                Exit(true, true);
            }
            else
            {
                _outline.Clear();
            }
        }

        // ---- the mode ----

        private static bool _live;
        private static string _pending;
        private static string _spokenWhenLeft;
        private static string _lastHeard;
        private static int _waited;
        private static int _quiet;

        private readonly GalaxyHudScreen _screen;
        private readonly InspectOutline _outline = new InspectOutline();

        /// <summary>Where the cursor is, in the pair the map is spoken in - whole units from home.
        /// </summary>
        private int _x;
        private int _y;

        /// <summary>How wide the cell is. An instance field, so the size the player settled on is
        /// still there the next time they open the mode in this session.</summary>
        private int _size = InspectGrid.DefaultSize;

        /// <summary>The control the mode was opened from, so leaving it puts the player back where
        /// they were rather than wherever the tree happens to seat them.</summary>
        private ControlId _entry;

        // The galaxy's own extent, in the same pair the cursor is held in - worked out once per entry.
        private double _lowX;
        private double _highX;
        private double _lowY;
        private double _highY;

        private void Enter()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            _entry = navigator == null ? null : navigator.FocusedKey;
            GalaxyPosition at;
            if (FocusedPlace(navigator, out at))
            {
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
                _x = 0;
                _y = 0;
            }

            MeasureGalaxy();
            _live = true;
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
        private void Exit(bool announce, bool deferred)
        {
            _live = false;
            _outline.Clear();
            string line = announce ? ModStrings.Get(ModStrings.GalaxyInspectExited) : null;
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
                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null && _entry != null)
                {
                    navigator.FocusNode(_entry);
                }
            }

            _entry = null;
        }

        private bool Move(int east, int north)
        {
            int x = InspectGrid.Step(_x, _size, east);
            int y = InspectGrid.Step(_y, _size, north);
            if (!InspectGrid.InBounds(x, y, _lowX, _highX, _lowY, _highY))
            {
                Voice.Say(ModStrings.Get(ModStrings.GalaxyInspectEdge), true);
                return true;
            }

            _x = x;
            _y = y;
            Settle(true);
            return true;
        }

        private bool Resize(int size)
        {
            _size = size;
            Voice.Say(SizeText(), true);
            Settle(false);
            return true;
        }

        /// <summary>Put the camera and the drawn square on the cell, then say what is in it.</summary>
        private void Settle(bool interrupt)
        {
            GalaxyPosition origin = GalaxyCoordinates.Origin();
            GalaxyViewLevels.CenterOn(
                new Vector3(origin.X + _x, 0f, origin.Y + _y),
                CameraDamping
            );
            _outline.Draw(
                (float)(origin.X + InspectGrid.Low(_x, _size)),
                (float)(origin.X + InspectGrid.High(_x, _size)),
                (float)(origin.Y + InspectGrid.Low(_y, _size)),
                (float)(origin.Y + InspectGrid.High(_y, _size))
            );
            Voice.Say(CellText(), interrupt);
        }

        private string SizeText()
        {
            return ModStrings.Format(ModStrings.GalaxyInspectCursorSize, _size, _size);
        }

        // ---- what Enter does ----

        /// <summary>
        /// Enter names the ONE thing in the cell, where there is exactly one: the mode ends and the
        /// cursor lands on it, with the announcement that node makes for itself.
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
            if (contents.Places.Count == 1)
            {
                StarSystemNode node = contents.Places[0];
                Exit(false, false);
                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null)
                {
                    navigator.FocusNode(
                        ControlId.Structural("galaxy:system/" + node.GUID)
                    );
                }

                return true;
            }

            if (contents.Places.Count == 0 && contents.Fleets.Count == 1)
            {
                Fleet fleet = contents.Fleets[0];
                Exit(false, false);
                GalaxyHudScreen.SelectFleet(fleet);
                return true;
            }

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
        private string CellText()
        {
            MessageBuilder message = new MessageBuilder();
            message.Fragment(MapCoordinates.Text(_x, _y, 0.0, 0.0));
            Contents contents = Read();
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

            message.ListItemForcedComma(FogText());
            return message.Build();
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
                Galaxy galaxy = Gui.Game == null ? null : Gui.Game.Galaxy;
                if (empire == null || galaxy == null)
                {
                    return contents;
                }

                List<StarSystemNode> perceived = new List<StarSystemNode>();
                foreach (StarSystemNode node in galaxy.StarSystemNodes)
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

                IList<ObliteratorProjectileLabel> shots = _screen.DrawnProjectiles;
                for (int i = 0; i < shots.Count; i++)
                {
                    ObliteratorProjectile shot =
                        shots[i] == null ? null : shots[i].Entity as ObliteratorProjectile;
                    if (shot != null && Holds(shot.GalaxyPosition))
                    {
                        contents.Projectiles.Add(shot);
                    }
                }

                IList<CoordinationRequestLabel> pins = _screen.DrawnPins;
                for (int i = 0; i < pins.Count; i++)
                {
                    CoordinationRequest pin =
                        pins[i] == null ? null : pins[i].CoordinationRequest;
                    if (pin != null && Holds(pin.GalaxyPosition))
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
                    string said = LaneText(link, empire, wormhole);
                    if (said != null)
                    {
                        contents.Lanes.Add(said);
                    }
                }
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
                Galaxy galaxy = Gui.Game == null ? null : Gui.Game.Galaxy;
                GameNode[] nodes = galaxy == null ? null : galaxy.GameNodes;
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

        /// <summary>Where the cursor the player is standing on IS, if it stands anywhere on the map -
        /// the node itself, or the nearest ancestor that is a place (a planet or a starlane under an
        /// opened system answers with that system). False for the clusters round the edge of the
        /// screen, which are not places at all.</summary>
        private static bool FocusedPlace(GraphNavigator navigator, out GalaxyPosition position)
        {
            position = default(GalaxyPosition);
            GraphNode node = navigator == null ? null : navigator.CurrentNode;
            for (int depth = 0; node != null && depth < 16; depth++)
            {
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

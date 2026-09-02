using System;
using System.Collections.Generic;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>Travelling the lanes and the way back: each hop down a starlane remembered, and
    /// Backspace putting the cursor and the camera back where the key was pressed.</summary>
    public sealed partial class GalaxyHudScreen
    {
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
            // And the two other things keyed on nodes of the galaxy that has gone: the scan lens's
            // remembered row, which <see cref="CentreOnScanSystem"/> would pan the camera to, and the
            // zoom level each system was jumped from (<see cref="CollapseZoom"/>).
            _scanRow = null;
            _jumpedFrom.Clear();
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
    }
}

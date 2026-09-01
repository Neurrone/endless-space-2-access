using System.Collections.Generic;
using ES2Access.Core.Speech;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// The empire's trade routes as the scan view draws them, WOVEN INTO THE MAP the player is already
    /// walking (RULED 2026-09-01, superseding the flat list of lines this replaces): a route is not a
    /// thing of its own on the screen, it is a line drawn along star lanes between two systems, so it
    /// is heard where those places already are - the two systems it runs between say what it is to
    /// them, every system it crosses says it is crossed, and every lane it rides says it carries it.
    /// Walking the lanes is then how a route is traced, exactly as a sighted player's eye follows the
    /// line.
    ///
    /// ONE LINE PER ROUTE, never a merged summary (RULED): a lane carrying three routes names three,
    /// and a system that is the end of one and the middle of another says both things.
    ///
    /// What the game DRAWS is the merge, though, and the merge is what colours the picture: the
    /// renderer walks each route's path pairwise and merges the legs under an undirected key - the two
    /// node indices, smaller first - so several routes over one hop are ONE line, painted from one of
    /// three materials: open, blockaded, or mixed where the hop carries both
    /// (<c>TradeRouteRenderer.UpdatePlayerEmpireDependantData</c> :229-283). So the traffic word a lane
    /// says with each route is the material the renderer would paint that lane with - what the player
    /// would see - and not a per-route recomputation that would disagree with the picture.
    ///
    /// The blockade flag is the renderer's, including the part of it that looks like an accident: the
    /// flag ACCUMULATES along the path (<c>flag |=</c> :233), so once a route is blockaded at one node
    /// every later leg of that route is drawn blockaded too, and a route blockaded at either end
    /// (<c>IsBeingSoftBlockadedOnHQ</c>/<c>OnSubsidiary</c> :228) is drawn blockaded from its first leg.
    /// Copying that is the point - a readout that "corrected" it would disagree with the picture.
    ///
    /// Engine-free, so the weave, the merge and the words are unit-tested off the game.
    /// </summary>
    public sealed class TradeWeave
    {
        /// <summary>The three materials the renderer paints a lane with, in the order it asks them
        /// (:272-283): both kinds of traffic first, then blockaded, then plain.</summary>
        public enum Traffic
        {
            Open = 0,
            Blockaded = 1,
            Mixed = 2,
        }

        /// <summary>What one route is to one place on the map: either an END of it, which names the
        /// other end, or somewhere it passes through, which names both.</summary>
        public struct Tie
        {
            public int Route;

            public bool Endpoint;

            /// <summary>The far end, for an <see cref="Endpoint"/>. A waypoint reads the route's own
            /// two ends instead (<see cref="TradeWeave.this"/>).</summary>
            public int Other;

            /// <summary>Whether the game is painting THIS ROUTE blockaded - the accumulator's value
            /// where the path ends, which is the colour of its last leg.</summary>
            public bool Blockaded;
        }

        /// <summary>What one route is to one lane: it rides it, and the lane is painted whatever the
        /// traffic over it comes to.</summary>
        public struct Ride
        {
            public int Route;

            public Traffic State;
        }

        /// <summary>One route: the two places it runs between and whether the picture paints it
        /// blockaded.</summary>
        public struct Route
        {
            public int Start;

            public int End;

            public bool Blockaded;
        }

        /// <summary>One line on the screen: which routes run over it, and how many legs of them are
        /// painted each way - the renderer's own two counters (<c>LinkInfo.AddTradeRoute</c>).</summary>
        private struct LaneTraffic
        {
            public List<int> Routes;

            public int Open;

            public int Blockaded;
        }

        private readonly List<Route> _routes = new List<Route>();

        private readonly Dictionary<int, List<Tie>> _ties = new Dictionary<int, List<Tie>>();

        private readonly Dictionary<uint, LaneTraffic> _lanes = new Dictionary<uint, LaneTraffic>();

        public int Count
        {
            get { return _routes.Count; }
        }

        public Route this[int route]
        {
            get { return _routes[route]; }
        }

        /// <summary>Start again. The instance is reused rather than reallocated because the whole walk
        /// is redone every time the map is built.</summary>
        public void Clear()
        {
            _routes.Clear();
            _ties.Clear();
            _lanes.Clear();
        }

        /// <summary>
        /// Take one route, exactly as the renderer takes it: its path walked pairwise onto the lanes,
        /// with the blockade flag accumulating along the way, and its places tied to it.
        /// </summary>
        /// <param name="path">The node indices the route runs through, headquarters first.</param>
        /// <param name="blockadedAtEnds">The route's own whole-route flag: a soft blockade sitting on
        /// the headquarters or on the subsidiary, which the renderer sets BEFORE the first leg.</param>
        /// <param name="blockadedAt">Per node of the path, whether that node is being soft blockaded.
        /// Shorter than the path, or absent, where the game has not computed one.</param>
        /// <returns>The route's own number, by which its ties and rides name it back, or -1 for a path
        /// with no leg in it, which draws no line at all.</returns>
        public int Add(int[] path, bool blockadedAtEnds, bool[] blockadedAt)
        {
            if (path == null || path.Length < 2)
            {
                return -1;
            }

            int route = _routes.Count;
            int start = path[0];
            int end = path[path.Length - 1];
            _routes.Add(new Route { Start = start, End = end });
            bool blockaded = blockadedAtEnds;
            for (int i = 0; i < path.Length - 1; i++)
            {
                blockaded =
                    blockaded
                    || (blockadedAt != null && i < blockadedAt.Length && blockadedAt[i]);
                CountLeg(path[i], path[i + 1], route, blockaded);
            }

            Route it = _routes[route];
            // The colour the LAST leg was painted, which is the one the whole route is judged by: the
            // accumulator can only rise, so this is "blockaded anywhere along the way".
            it.Blockaded = blockaded;
            _routes[route] = it;
            TieTo(start, new Tie { Route = route, Endpoint = true, Other = end, Blockaded = blockaded });
            TieTo(end, new Tie { Route = route, Endpoint = true, Other = start, Blockaded = blockaded });
            for (int i = 1; i < path.Length - 1; i++)
            {
                TieTo(path[i], new Tie { Route = route, Blockaded = blockaded });
            }

            return route;
        }

        /// <summary>What the routes are to one place, or nothing where none of them touches it.</summary>
        public IList<Tie> At(int node)
        {
            List<Tie> ties;
            return _ties.TryGetValue(node, out ties) ? ties : null;
        }

        /// <summary>
        /// What rides one lane, each route with the colour the renderer would paint that lane - so two
        /// routes over one hop, one of them blockaded, both say "mixed", because mixed is the one line
        /// the player is looking at.
        ///
        /// Nothing where no route runs over it, which is every lane on a map with no trading company.
        /// </summary>
        public IList<Ride> On(int start, int end)
        {
            LaneTraffic lane;
            if (_lanes.Count == 0 || !_lanes.TryGetValue(Key(start, end), out lane))
            {
                return null;
            }

            Traffic state =
                lane.Blockaded > 0 && lane.Open > 0
                    ? Traffic.Mixed
                    : (lane.Blockaded > 0 ? Traffic.Blockaded : Traffic.Open);
            List<Ride> rides = new List<Ride>(lane.Routes.Count);
            for (int i = 0; i < lane.Routes.Count; i++)
            {
                rides.Add(new Ride { Route = lane.Routes[i], State = state });
            }

            return rides;
        }

        /// <summary>The renderer's own key: the pair of node indices with the smaller one in the low
        /// half, so a leg travelled in either direction lands on the same lane (:234-236).</summary>
        public static uint Key(int start, int end)
        {
            uint low = (uint)(start >= end ? end : start);
            uint high = (uint)(start >= end ? start : end);
            return (low & 0xFFFF) | ((high & 0xFFFF) << 16);
        }

        private void CountLeg(int start, int end, int route, bool blockaded)
        {
            uint key = Key(start, end);
            LaneTraffic lane;
            if (!_lanes.TryGetValue(key, out lane))
            {
                lane = new LaneTraffic { Routes = new List<int>(1) };
            }

            if (blockaded)
            {
                lane.Blockaded++;
            }
            else
            {
                lane.Open++;
            }

            // Counted per LEG as the renderer counts it, but named once: a route that somehow doubled
            // back over its own hop would colour the line twice and still be one route to say.
            if (!lane.Routes.Contains(route))
            {
                lane.Routes.Add(route);
            }

            _lanes[key] = lane;
        }

        private void TieTo(int node, Tie tie)
        {
            List<Tie> ties;
            if (!_ties.TryGetValue(node, out ties))
            {
                ties = new List<Tie>(1);
                _ties.Add(node, ties);
            }

            ties.Add(tie);
        }

        /// <summary>What one END of a route says: where the other end is, and whether the game is
        /// painting the line to it blockaded.</summary>
        public static string EndText(string other, bool blockaded)
        {
            return other == null
                ? null
                : ModStrings.Format(
                    blockaded
                        ? ModStrings.ScanTradeRouteToBlockaded
                        : ModStrings.ScanTradeRouteTo,
                    other
                );
        }

        /// <summary>What a place the route merely CROSSES says: whose road this is. No traffic word -
        /// the picture draws the colour on the lanes, and this is a place the line passes over.
        /// </summary>
        public static string ThroughText(string from, string to)
        {
            return from == null || to == null
                ? null
                : ModStrings.Format(ModStrings.ScanTradeRouteThrough, from, to);
        }

        /// <summary>What a LANE says about one of the routes riding it: which route, and the colour
        /// the renderer paints this line - open, blockaded, or the third material where the lane
        /// carries both.</summary>
        public static string LaneText(string from, string to, Traffic state)
        {
            if (from == null || to == null)
            {
                return null;
            }

            string template = ModStrings.ScanTradeLaneOpen;
            if (state == Traffic.Blockaded)
            {
                template = ModStrings.ScanTradeLaneBlockaded;
            }
            else if (state == Traffic.Mixed)
            {
                template = ModStrings.ScanTradeLaneMixed;
            }

            return ModStrings.Format(template, from, to);
        }
    }
}

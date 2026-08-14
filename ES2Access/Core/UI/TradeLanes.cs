using System.Collections.Generic;
using ES2Access.Core.Speech;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// The trade routes an empire is running, as the game DRAWS them: not one line per route but one
    /// line per LANE, with every route that runs over that lane counted into it.
    ///
    /// The game's renderer walks each route's path pairwise and merges the legs under an undirected
    /// key - the two node indices, smaller first - so two routes leaving the same headquarters along
    /// the same first hop are one line on the screen, and the line is painted from one of three
    /// materials: open, blockaded, or mixed where the lane carries both
    /// (<c>TradeRouteRenderer.UpdatePlayerEmpireDependantData</c> :229-283). This is that merge, so
    /// that what the player hears is the geometry the game drew rather than a second model of it.
    ///
    /// The blockade flag is the renderer's, including the part of it that looks like an accident: the
    /// flag ACCUMULATES along the path (<c>flag |=</c> :233), so once a route is blockaded at one node
    /// every later leg of that route is drawn blockaded too, and a route blockaded at either end
    /// (<c>IsBeingSoftBlockadedOnHQ</c>/<c>OnSubsidiary</c> :228) is drawn blockaded from its first leg.
    /// Copying that is the point - a readout that "corrected" it would disagree with the picture.
    ///
    /// Engine-free, so the merge and the counted phrase are unit-tested off the game.
    /// </summary>
    public sealed class TradeLanes
    {
        /// <summary>One line on the screen: the two places it runs between, and how many of the
        /// empire's routes run over it open and blockaded.</summary>
        public struct Lane
        {
            /// <summary>The node the lane was FIRST seen from, which is the orientation the renderer
            /// keeps too (<c>LinkInfo(start, end)</c> is built from the first leg that claimed the
            /// key) - the lane itself has no direction.</summary>
            public int Start;
            public int End;
            public int Open;
            public int Blockaded;

            public int Total
            {
                get { return Open + Blockaded; }
            }
        }

        private readonly List<Lane> _lanes = new List<Lane>();
        private readonly Dictionary<uint, int> _index = new Dictionary<uint, int>();

        public int Count
        {
            get { return _lanes.Count; }
        }

        public Lane this[int index]
        {
            get { return _lanes[index]; }
        }

        /// <summary>Start again. The instance is reused rather than reallocated because the whole walk
        /// is redone every time the screen is built.</summary>
        public void Clear()
        {
            _lanes.Clear();
            _index.Clear();
        }

        /// <summary>Count one leg of one route onto the lane it runs over, creating the lane the first
        /// time anything runs over it.</summary>
        public void Add(int start, int end, bool blockaded)
        {
            uint key = Key(start, end);
            int at;
            if (!_index.TryGetValue(key, out at))
            {
                at = _lanes.Count;
                _lanes.Add(new Lane { Start = start, End = end });
                _index.Add(key, at);
            }

            Lane lane = _lanes[at];
            if (blockaded)
            {
                lane.Blockaded++;
            }
            else
            {
                lane.Open++;
            }

            _lanes[at] = lane;
        }

        /// <summary>The renderer's own key: the pair of node indices with the smaller one in the low
        /// half, so a leg travelled in either direction lands on the same lane (:234-236).</summary>
        private static uint Key(int start, int end)
        {
            uint low = (uint)(start >= end ? end : start);
            uint high = (uint)(start >= end ? start : end);
            return (low & 0xFFFF) | ((high & 0xFFFF) << 16);
        }

        /// <summary>
        /// What a lane says: how many of the empire's routes run over it, and whether the game is
        /// painting it as blockaded - the three materials in words.
        ///
        /// Mixed is its own sentence rather than the blockaded one, because the line the player is
        /// looking at is its own colour: some traffic here is still getting through.
        /// </summary>
        public static string Text(int open, int blockaded)
        {
            int total = open + blockaded;
            if (total <= 0)
            {
                return null;
            }

            if (blockaded == 0)
            {
                return ModStrings.Plural(
                    ModStrings.ScanTradeRouteOne,
                    ModStrings.ScanTradeRoutes,
                    total
                );
            }

            if (open == 0)
            {
                return ModStrings.Plural(
                    ModStrings.ScanTradeRouteOneBlockaded,
                    ModStrings.ScanTradeRoutesBlockaded,
                    total
                );
            }

            return ModStrings.Format(ModStrings.ScanTradeRoutesMixed, total, blockaded);
        }
    }
}

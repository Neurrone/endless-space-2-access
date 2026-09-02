using System;
using System.Collections.Generic;
using ES2Access.Core.Map;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Which stretch of sky a PLACE is in - the question the galaxy's own model cannot answer.
    ///
    /// The game says which constellation each of its nodes belongs to and draws a name across the
    /// middle of each region, but a point out between the stars belongs to nothing: there is no
    /// boundary anywhere in the data. So the boundary is derived, once per galaxy, from the places the
    /// constellations are made of - the convex outline round each one
    /// (<see cref="ConstellationRegions"/>, engine-free and unit-tested) - and everything that has to
    /// name a stretch of empty sky asks here.
    ///
    /// Only the constellations this empire has EXPLORED are in the model, because a region the map
    /// draws no name across is a region the player has not been told exists, and answering "that is
    /// Fornax" for a point in one would say more than the picture does. The explored set is the label's
    /// own gate (<c>ConstellationLabel.ShowOrHideIfVisibleByEmpire</c>), and it only moves when the
    /// game raises an exploration event - so the model is rebuilt when that set changes and never
    /// otherwise, which is what keeps a per-frame caller free.
    ///
    /// Coordinates are the pair the map is SPOKEN in - east and north from home
    /// (<see cref="GalaxyCoordinates.Offsets"/>) - so a caller holding a spoken position can hand it
    /// straight over, and a distance out of <see cref="DistanceTo"/> is in the units the player hears.
    ///
    /// All of it is static state in this assembly, which is discarded on a hot reload;
    /// <see cref="Forget"/> is there so the game objects are let go on the way out.
    /// </summary>
    public static class ConstellationMap
    {
        private static ConstellationRegions _regions;

        /// <summary>The constellation each region index stands for - the mapping
        /// <see cref="ConstellationRegions"/> deliberately does not keep.</summary>
        private static readonly List<Constellation> _order = new List<Constellation>();

        /// <summary>Every constellation in the galaxy, gathered once per galaxy: the walk that finds
        /// them is a walk of all the map's nodes, and which constellations EXIST never changes inside
        /// one game. Only which of them are explored does, and that is five array reads.</summary>
        private static readonly List<Constellation> _all = new List<Constellation>();

        /// <summary>The galaxy the model was built for. A save loaded over this one is a different
        /// galaxy with the same class, so identity is the only honest test.</summary>
        private static object _game;

        /// <summary>What the explored set looked like when the model was built. Cheap to recompute and
        /// exact - a set of a handful of constellations - so no event subscription is needed.</summary>
        private static int _explored;

        /// <summary>Whether the outlines have been built for the current explored set at all - the
        /// separate flag exists because "nothing is explored yet" is a legitimate answer with no
        /// regions in it, and testing the region list for that would rebuild it every frame.</summary>
        private static bool _built;

        private static int _frame = -1;

        /// <summary>How many constellations the model holds.</summary>
        public static int Count
        {
            get
            {
                Resolve();
                return _order.Count;
            }
        }

        /// <summary>The constellation whose region holds this place, or null where no explored
        /// constellation's outline does.</summary>
        public static Constellation Classify(double east, double north)
        {
            Resolve();
            if (_regions == null)
            {
                return null;
            }

            int region = _regions.Classify(east, north);
            return region < 0 ? null : _order[region];
        }

        /// <summary>The same for a place the game named.</summary>
        public static Constellation Classify(GalaxyPosition at)
        {
            double east,
                north;
            GalaxyCoordinates.Offsets(at, out east, out north);
            return Classify(east, north);
        }

        /// <summary>How far a place is from one constellation's outline - zero anywhere inside it. The
        /// answer is in the units the map is spoken in. Negative for a constellation the model does not
        /// hold, which is the same answer as "not on the map".</summary>
        public static double DistanceTo(Constellation constellation, double east, double north)
        {
            Resolve();
            int region = RegionOf(constellation);
            return region < 0 ? -1 : _regions.DistanceTo(region, new MapPoint(east, north));
        }

        /// <summary>Which region index a constellation is, or -1 while the model does not hold it.
        /// </summary>
        public static int RegionOf(Constellation constellation)
        {
            Resolve();
            for (int i = 0; i < _order.Count; i++)
            {
                if (ReferenceEquals(_order[i], constellation))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The constellation a region index stands for, or null.</summary>
        public static Constellation At(int region)
        {
            Resolve();
            return region < 0 || region >= _order.Count ? null : _order[region];
        }

        /// <summary>The model checking itself against the galaxy it was built from - for a probe, not
        /// for the game. <see cref="RegionAudit.OutsideOwnHull"/> is an invariant and anything else is
        /// a defect in the geometry; the rest measure how interlocked this particular galaxy is.
        /// </summary>
        public static RegionAudit Audit()
        {
            Resolve();
            return _regions == null ? new RegionAudit() : _regions.Audit();
        }

        /// <summary>
        /// The same over EVERY constellation in the galaxy, explored or not, on a throwaway model.
        ///
        /// The live model holds only what the player has seen, which on an early turn is nothing at
        /// all - so the geometry would never be exercised against real data until somebody had played
        /// half a game. This is the oracle: the whole galaxy's outlines, audited, with nothing kept.
        /// </summary>
        public static RegionAudit AuditWholeGalaxy()
        {
            Resolve();
            ConstellationRegions regions = new ConstellationRegions();
            for (int i = 0; i < _all.Count; i++)
            {
                Feed(regions, _all[i]);
            }

            return regions.Audit();
        }

        /// <summary>Let go of the galaxy this was built from - mod teardown.</summary>
        public static void Forget()
        {
            _regions = null;
            _order.Clear();
            _all.Clear();
            _game = null;
            _explored = 0;
            _frame = -1;
        }

        /// <summary>
        /// Rebuild the model if the galaxy or the explored set has changed, and at most once a frame.
        ///
        /// The gate is a walk of the constellations reading one array element each, which is cheaper
        /// than the outlines it decides whether to rebuild by orders of magnitude - and the rebuild
        /// itself happens only on the frames an exploration event lands.
        /// </summary>
        private static void Resolve()
        {
            try
            {
                int frame = UnityEngine.Time.frameCount;
                if (frame == _frame)
                {
                    return;
                }

                _frame = frame;
                object game = Gui.Game;
                if (!ReferenceEquals(game, _game))
                {
                    _game = game;
                    _built = false;
                    _regions = null;
                    _order.Clear();
                    _all.Clear();
                    Gather(_all);
                }

                Empire empire = Gui.PlayerEmpire;
                int signature = Signature(empire);
                if (_built && signature == _explored)
                {
                    return;
                }

                _built = true;
                _explored = signature;
                _order.Clear();
                _regions = null;
                ConstellationRegions regions = new ConstellationRegions();
                for (int i = 0; i < _all.Count; i++)
                {
                    if (Explored(_all[i], empire) && Feed(regions, _all[i]))
                    {
                        _order.Add(_all[i]);
                    }
                }

                _regions = _order.Count == 0 ? null : regions;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: building the constellation outlines threw: " + e);
                _regions = null;
                _order.Clear();
                _built = true;
            }
        }

        /// <summary>One constellation's member places as a region. False where it has none, so the
        /// region indices and <see cref="_order"/> stay in step.</summary>
        private static bool Feed(ConstellationRegions regions, Constellation constellation)
        {
            IList<GameNode> nodes = constellation.GameNodes;
            if (nodes == null || nodes.Count == 0)
            {
                return false;
            }

            MapPoint[] places = new MapPoint[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                double east,
                    north;
                GalaxyCoordinates.Offsets(nodes[i].GalaxyPosition, out east, out north);
                places[i] = new MapPoint(east, north);
            }

            regions.Add(places);
            return true;
        }

        /// <summary>The galaxy's constellations, in the order the node array meets them so the same
        /// galaxy always builds the same model - which is what makes the region-overlap tiebreak
        /// (first added wins) deterministic.</summary>
        private static void Gather(List<Constellation> into)
        {
            GameNode[] nodes = GameGalaxy.GameNodes();
            for (int i = 0; nodes != null && i < nodes.Length; i++)
            {
                Constellation constellation = nodes[i].Constellation;
                if (constellation != null && !into.Contains(constellation))
                {
                    into.Add(constellation);
                }
            }
        }

        /// <summary>The gate the map's own constellation label asks before it draws its name at all
        /// (<c>ConstellationLabel.ShowOrHideIfVisibleByEmpire</c>): anything above the unexplored state
        /// shows. Mirrored exactly, staleness included - the aggregate only recomputes when the game
        /// raises a node-exploration event, so early in a game every constellation reads unexplored
        /// even where the empire is sitting in one.</summary>
        public static bool Explored(Constellation constellation, Empire empire)
        {
            try
            {
                return constellation != null
                    && empire != null
                    && constellation.Exploration != null
                    && (int)constellation.Exploration[empire]
                        > (int)EntityExploration.State.Unrevealed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int Signature(Empire empire)
        {
            int signature = _all.Count;
            for (int i = 0; i < _all.Count; i++)
            {
                signature = (signature * 397) ^ (Explored(_all[i], empire) ? 1 : 0);
            }

            return signature;
        }
    }
}

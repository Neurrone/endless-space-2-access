using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Map;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.Localization;

namespace ES2Access.UI
{
    /// <summary>
    /// What each of the sixteen launch bearings is worth, read off the galaxy the probe would fly
    /// through: how much of what the launch would reveal is already known
    /// (<see cref="ProbeFootprint"/>), how far the map goes that way, and which stretches of the way
    /// there nobody has explored (<see cref="ProbeCorridor"/>, both said by
    /// <see cref="ProbeContextText"/>).
    ///
    /// The engine half of that reading. The measuring itself is engine-free and lives in
    /// <c>Core/Map</c>; this is the part that knows where the galaxy's systems are, what the empire's
    /// probes can do, and which squares of map the empire has seen.
    ///
    /// MEMOIZED, and it has to be: the navigator recomposes the focused control's whole readout every
    /// frame to decide whether the review buffer still matches it, so a bearing's label asked
    /// naively would ray-march sixteen corridors - some hundred and fifty steps each, sampled three
    /// times across - into the fog service sixty times a second, and count sixteen footprints of a
    /// couple of hundred tiles besides. The answer only changes when the fleet moves, the fog lifts,
    /// the probe's own reach or vision changes, or the player changes language, so those are the key
    /// and everything else is an array read. <see cref="Recomputes"/> is the counter that proves it.
    /// The bearing's announcement and its buffer lines come off the SAME measurement and are memoized
    /// together, because they are one reading said two ways and asking for either must never cost the
    /// corridors twice.
    ///
    /// How far the map goes is cached harder still, and not here: <see cref="GalaxyFrame"/> measures it
    /// once per game, and a bearing ends where it leaves the FRAME the inspect cursor roams, so the rim
    /// a bearing names is a rim the player can steer to and be refused at.
    /// </summary>
    public static class ProbeContext
    {
        /// <summary>How many bearings the launch group offers - the sixteen-word compass, because a
        /// probe aimed between two of eight words could not be aimed there at all.</summary>
        public const int Bearings = 16;

        private const double Step = 360.0 / Bearings;

        private static readonly string[] _labels = new string[Bearings];
        private static readonly IList<string>[] _details = new IList<string>[Bearings];
        private static Fleet _fleet;
        private static StarSystemNode _node;
        private static int _revision = int.MinValue;
        private static float _halfWidth = float.MinValue;
        private static string _language;
        private static int _reach;
        private static int _recomputes;

        /// <summary>The bearing the launch stop at this index aims at, clockwise from north.</summary>
        public static double Bearing(int index)
        {
            return index * Step;
        }

        /// <summary>How many times the sixteen corridors have been re-measured since the mod loaded -
        /// the only way to see that the memo is holding, since a wasted re-measure shows up in no
        /// transcript and no dump (`docs/generic/performance.md`).</summary>
        public static int Recomputes
        {
            get { return _recomputes; }
        }

        /// <summary>What the launch group is called: the order named by what it can REACH, which is the
        /// one number that decides whether any bearing under it is worth taking.</summary>
        public static string GroupLabel(Fleet fleet, StarSystemNode node)
        {
            Ensure(fleet, node);
            return ModStrings.Format(ModStrings.GalaxyProbeLaunchReach, _reach);
        }

        /// <summary>What one bearing announces: the heading and the share, which is what a player
        /// walking sixteen of them chooses on.</summary>
        public static string Label(Fleet fleet, StarSystemNode node, int index)
        {
            Ensure(fleet, node);
            return _labels[index];
        }

        /// <summary>The rest of what is down that bearing, a clause per line, for the review buffer of
        /// the one bearing the player stopped on (<see cref="ProbeContextText.Lines"/>).</summary>
        public static IList<string> Lines(Fleet fleet, StarSystemNode node, int index)
        {
            Ensure(fleet, node);
            return _details[index];
        }

        /// <summary>
        /// Re-measure only if something the answer depends on has moved. The fog's own revision is the
        /// game's counter of "the visible map changed" and covers every way it can - a probe arriving,
        /// a fleet's vision sweeping, a turn passing - which is why the memo watches that rather than
        /// trying to guess at the events behind it.
        /// </summary>
        private static void Ensure(Fleet fleet, StarSystemNode node)
        {
            try
            {
                Empire empire = fleet == null ? null : fleet.Empire;
                IVisibilityService visibility = Services.GetService<IVisibilityService>();
                if (empire == null || node == null || visibility == null)
                {
                    return;
                }

                float halfWidth = empire.GetPropertyValue(
                    SimulationProperties.Empire.ProbeVisionRange
                );
                int revision = visibility.VisibilityRevision;
                string language = ModLocale.Language;
                int reach = Reach(empire);
                if (
                    ReferenceEquals(fleet, _fleet)
                    && ReferenceEquals(node, _node)
                    && revision == _revision
                    && halfWidth == _halfWidth
                    && reach == _reach
                    && language == _language
                    && _labels[0] != null
                )
                {
                    return;
                }

                _fleet = fleet;
                _node = node;
                _revision = revision;
                _halfWidth = halfWidth;
                _reach = reach;
                _language = language;
                _recomputes++;

                Measure(empire, visibility, node, halfWidth);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading what lies down the probe's bearings threw: " + e);
                Fallback();
            }
        }

        /// <summary>How far a probe gets: its own speed for its own lifetime, which is what the game's
        /// mouse-aiming cone is drawn from (<c>ProbeLaunchingCursor.LateUpdate</c> :34-36).</summary>
        private static int Reach(Empire empire)
        {
            double speed = empire.GetPropertyValue(SimulationProperties.Empire.ProbeSpeed);
            double lifetime = empire.GetPropertyValue(SimulationProperties.Empire.ProbeBaseLifetime);
            return (int)Math.Round(speed * lifetime, MidpointRounding.AwayFromZero);
        }

        private static void Measure(
            Empire empire,
            IVisibilityService visibility,
            StarSystemNode node,
            double halfWidth
        )
        {
            ConvexHull edges = GalaxyFrame.Edges();
            MapPoint origin = new MapPoint(node.GalaxyPosition.X, node.GalaxyPosition.Y);
            MapExplored explored = new Fog(empire, visibility).Explored;
            MapPoint anchor = Anchor();
            for (int i = 0; i < Bearings; i++)
            {
                double bearing = Bearing(i);
                ProbeCorridorReading reading = ProbeCorridor.Read(
                    edges,
                    origin,
                    anchor,
                    bearing,
                    halfWidth,
                    explored
                );
                ProbeFootprint footprint = ProbeFootprint.Read(
                    edges,
                    origin,
                    anchor,
                    bearing,
                    _reach,
                    halfWidth,
                    explored
                );
                _labels[i] = ProbeContextText.Label(bearing, footprint);
                _details[i] = ProbeContextText.Lines(reading, footprint);
            }
        }

        /// <summary>
        /// The one-unit lattice every fog sample is snapped to: the empire's home, which is where the
        /// spoken map coordinates count from and therefore where the inspect cursor's own tiles sit
        /// (<see cref="GalaxyCoordinates.Origin"/> answers a GalaxyPosition - X east, Y north).
        ///
        /// Sharing the lattice is the whole point: a bearing that says "unexplored 12-15" is then a
        /// claim about tiles the player can steer the inspect cursor onto and count fog in, rather
        /// than about points between them. It is a constant for the length of a game, so re-asking it
        /// per measurement costs nothing.
        /// </summary>
        private static MapPoint Anchor()
        {
            GalaxyPosition home = GalaxyCoordinates.Origin();
            return new MapPoint(home.X, home.Y);
        }

        /// <summary>What the bearings say when the galaxy could not be read at all: the direction and
        /// nothing else. A bearing with no context is still a bearing the player can launch on, and a
        /// made-up context would be worse than none.</summary>
        private static void Fallback()
        {
            for (int i = 0; i < Bearings; i++)
            {
                _labels[i] = ModStrings.Get(CompassDirections.KeyForBearing16(Bearing(i)));
                _details[i] = null;
            }
        }

        /// <summary>The one question the corridor asks of the game, bound to the empire asking it - a
        /// small object rather than a captured lambda so that the sixteen corridors of one measurement
        /// share a single allocation.</summary>
        private sealed class Fog
        {
            private readonly Empire _empire;
            private readonly IVisibilityService _visibility;

            public Fog(Empire empire, IVisibilityService visibility)
            {
                _empire = empire;
                _visibility = visibility;
            }

            public bool Explored(double east, double north)
            {
                return _visibility.IsExplored(
                    _empire,
                    new GalaxyPosition((float)east, (float)north)
                );
            }
        }
    }
}

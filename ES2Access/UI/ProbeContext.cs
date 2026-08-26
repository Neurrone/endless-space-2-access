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
    /// through: how far the map goes that way and which stretches of the way there nobody has
    /// explored (<see cref="ProbeCorridor"/>, said by <see cref="ProbeContextText"/>).
    ///
    /// The engine half of that reading. The measuring itself is engine-free and lives in
    /// <c>Core/Map</c>; this is the part that knows where the galaxy's systems are, what the empire's
    /// probes can do, and which squares of map the empire has seen.
    ///
    /// MEMOIZED, and it has to be: the navigator recomposes the focused control's whole readout every
    /// frame to decide whether the review buffer still matches it, so a bearing's label asked
    /// naively would ray-march sixteen corridors - some hundred and fifty steps each, sampled three
    /// times across - into the fog service sixty times a second. The answer only changes when the
    /// fleet moves, the fog lifts, the probe's own reach changes, or the player changes language, so
    /// those four are the key and everything else is an array read. <see cref="Recomputes"/> is the
    /// counter that proves it.
    ///
    /// The galaxy's outline is cached harder still: the systems do not move for the length of a game,
    /// so the hull is built once per game and survives every fog change under it.
    /// </summary>
    public static class ProbeContext
    {
        /// <summary>How many bearings the launch group offers - the sixteen-word compass, because a
        /// probe aimed between two of eight words could not be aimed there at all.</summary>
        public const int Bearings = 16;

        private const double Step = 360.0 / Bearings;

        private static ConvexHull _outline;
        private static object _outlineOf;

        private static readonly string[] _lines = new string[Bearings];
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

        /// <summary>One bearing said as what is down it.</summary>
        public static string Line(Fleet fleet, StarSystemNode node, int index)
        {
            Ensure(fleet, node);
            return _lines[index];
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
                if (
                    ReferenceEquals(fleet, _fleet)
                    && ReferenceEquals(node, _node)
                    && revision == _revision
                    && halfWidth == _halfWidth
                    && language == _language
                    && _lines[0] != null
                )
                {
                    return;
                }

                _fleet = fleet;
                _node = node;
                _revision = revision;
                _halfWidth = halfWidth;
                _language = language;
                _recomputes++;

                _reach = Reach(empire);
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
            ConvexHull outline = Outline();
            MapPoint origin = new MapPoint(node.GalaxyPosition.X, node.GalaxyPosition.Y);
            MapExplored explored = new Fog(empire, visibility).Explored;
            for (int i = 0; i < Bearings; i++)
            {
                double bearing = Bearing(i);
                _lines[i] = ProbeContextText.Line(
                    bearing,
                    ProbeCorridor.Read(outline, origin, bearing, halfWidth, explored)
                );
            }
        }

        /// <summary>What the bearings say when the galaxy could not be read at all: the direction and
        /// nothing else. A bearing with no context is still a bearing the player can launch on, and a
        /// made-up context would be worse than none.</summary>
        private static void Fallback()
        {
            for (int i = 0; i < Bearings; i++)
            {
                _lines[i] = ModStrings.Get(CompassDirections.KeyForBearing16(Bearing(i)));
            }
        }

        /// <summary>
        /// The outline of the galaxy, from every star system on the map whether or not the player has
        /// seen it.
        ///
        /// The rim a probe stops at is the map's, not the fog's: how big the galaxy is was chosen at
        /// setup and is no secret, and an edge that crept outwards as the fog lifted would make the
        /// same bearing answer one distance this turn and another the next.
        ///
        /// Public because it is the ONE outline of the galaxy: anything else that has to measure the
        /// map as a whole (<see cref="GalaxyOverview"/>) asks here rather than building a second hull
        /// over the same eighty-odd systems and risking a different answer.
        /// </summary>
        public static ConvexHull Outline()
        {
            object game = Gui.Game;
            if (_outline != null && ReferenceEquals(game, _outlineOf))
            {
                return _outline;
            }

            List<MapPoint> places = new List<MapPoint>();
            foreach (StarSystemNode system in GameGalaxy.StarSystemNodes())
            {
                places.Add(new MapPoint(system.GalaxyPosition.X, system.GalaxyPosition.Y));
            }

            _outline = ConvexHull.Build(places.ToArray());
            _outlineOf = game;
            return _outline;
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

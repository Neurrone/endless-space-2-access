using System;
using System.Collections.Generic;

namespace ES2Access.Core.Map
{
    /// <summary>Whether the galaxy at this place has been explored - the one thing the corridor asks
    /// of the game, so that the marching itself needs no engine present. East and north are the map's
    /// own units, the same ones <see cref="MapPoint"/> is in.</summary>
    public delegate bool MapExplored(double east, double north);

    /// <summary>A stretch of one heading nobody has explored, in whole units from where the heading
    /// starts. It runs FROM one distance TO another - "12-15" is the map between twelve and fifteen
    /// units out - so a stretch one unit long has <see cref="To"/> one greater than
    /// <see cref="From"/>, and two stretches with a single explored unit between them read as ending
    /// and starting on the same number.</summary>
    public struct UnexploredSpan
    {
        public readonly int From;
        public readonly int To;

        public UnexploredSpan(int from, int to)
        {
            From = from;
            To = to;
        }

        public override string ToString()
        {
            return From + "-" + To;
        }
    }

    /// <summary>
    /// What one heading out of a system holds: how far the map goes that way, which stretches of the
    /// FLIGHT LINE nobody has explored, and which stretches of the map ALONGSIDE it - inside the
    /// circle a probe reveals as it passes - are unexplored while the line itself is known.
    ///
    /// Three tracks rather than one, because the two facts are worth different things to the player.
    /// Fog ON the line is fog the probe flies into; fog beside it is fog the probe would merely brush.
    /// Reporting either as the other is what makes a bearing sound wrong: a fog edge running parallel
    /// just inside the corridor turned a fully known flight line into "unexplored 0 to the map edge".
    /// </summary>
    public sealed class ProbeCorridorReading
    {
        private static readonly UnexploredSpan[] None = new UnexploredSpan[0];

        internal ProbeCorridorReading(
            double bearing,
            int edge,
            IList<UnexploredSpan> spans,
            IList<UnexploredSpan> clockwise,
            IList<UnexploredSpan> counterClockwise
        )
        {
            Bearing = bearing;
            Edge = edge;
            Spans = spans ?? (IList<UnexploredSpan>)None;
            Clockwise = clockwise ?? (IList<UnexploredSpan>)None;
            CounterClockwise = counterClockwise ?? (IList<UnexploredSpan>)None;
        }

        /// <summary>A reading of the flight line alone - nothing alongside it, for a caller that is
        /// only interested in what the probe would fly through.</summary>
        internal ProbeCorridorReading(int edge, IList<UnexploredSpan> spans)
            : this(0.0, edge, spans, null, null) { }

        /// <summary>The heading this was read down, degrees clockwise from north - kept so that the
        /// two sides of the corridor can be NAMED (the clockwise side is this plus ninety) by whoever
        /// says the reading.</summary>
        public readonly double Bearing;

        /// <summary>Whole units from the origin to the rim of the map along this heading.</summary>
        public readonly int Edge;

        /// <summary>The unexplored stretches OF THE FLIGHT LINE, nearest first, never touching and
        /// never overlapping.</summary>
        public readonly IList<UnexploredSpan> Spans;

        /// <summary>Unexplored stretches a quarter circle clockwise of the heading
        /// (<see cref="Bearing"/> + 90), counted only where the line itself is explored.</summary>
        public readonly IList<UnexploredSpan> Clockwise;

        /// <summary>The same a quarter circle the other way (<see cref="Bearing"/> - 90).</summary>
        public readonly IList<UnexploredSpan> CounterClockwise;

        /// <summary>Whether the last stretch of the LINE runs off the edge of the map rather than
        /// ending in explored space - the difference between "unexplored out to the rim" and
        /// "unexplored, then known again".</summary>
        public bool ReachesEdge
        {
            get { return Spans.Count > 0 && Spans[Spans.Count - 1].To >= Edge; }
        }
    }

    /// <summary>
    /// What a probe would find if it were launched down one bearing - the question a player has to
    /// answer sixteen times before spending an order that cannot be recalled.
    ///
    /// A probe flies a straight line and reveals a circle around itself as it goes, so two different
    /// things are worth reporting: the LINE, which is where the probe actually goes, and what lies
    /// ALONGSIDE it within the circle, which is what it would uncover in passing. Each unit out is
    /// sampled three times - on the line and at both flanks - but the three answers are kept apart:
    /// the line's fog is the heading's own reading, and a flank's fog is only mentioned over stretches
    /// where the line is known, since where the line is dark the flank adds nothing a launch would
    /// change.
    ///
    /// EVERY sample is taken at the same one-unit lattice the inspect cursor tests the fog at, snapped
    /// by rounding each axis of the offset from the lattice anchor the caller hands in (home, in the
    /// game). So every number the player hears is a number they can walk to and check tile by tile,
    /// and a sliver of fog narrower than a tile can never surface as a stretch of one.
    ///
    /// Distances are whole units from the origin, counting outwards, ending at the rim of the map.
    /// Bearings are degrees clockwise from north, north being +north and east +east - the convention
    /// the mod's compass words are in.
    ///
    /// Engine-free: the galaxy is reached only through the <see cref="MapExplored"/> the caller hands
    /// in, so every span the mod can ever speak is reproducible in a test with a fabricated one.
    /// </summary>
    public static class ProbeCorridor
    {
        /// <summary>The reading down <paramref name="bearing"/>, with the map's rim taken from the
        /// outline handed in. The outline is whatever the caller calls the edge of the world - in the
        /// game it is the four corners of the box the galaxy's nodes fill, the same box the inspect
        /// cursor may roam, because nothing in the flight itself stops a probe (see
        /// <c>docs/galaxy-map.md</c>) and the frame a player can walk to is the honest one to name.
        /// A rectangle is a convex outline like any other, so the exit arithmetic is unchanged.
        /// </summary>
        public static ProbeCorridorReading Read(
            ConvexHull galaxy,
            MapPoint origin,
            MapPoint anchor,
            double bearing,
            double halfWidth,
            MapExplored explored
        )
        {
            double radians = bearing * Math.PI / 180.0;
            return Read(
                origin,
                anchor,
                bearing,
                galaxy.ExitDistance(origin, Math.Sin(radians), Math.Cos(radians)),
                halfWidth,
                explored
            );
        }

        /// <summary>
        /// The same from a rim already measured. <paramref name="exitDistance"/> is rounded ONCE,
        /// here, and everything else - the marching, the spans, the number the answer ends on - is in
        /// terms of that one whole number, so the rim a span is said to reach and the rim the sentence
        /// names can never disagree by a unit.
        ///
        /// <paramref name="anchor"/> is the lattice every sample is snapped to; only the fractional
        /// part of it matters, so any place on the same lattice - the empire's home, in the game -
        /// gives identical answers.
        ///
        /// A rim less than a unit away is legal and answers no spans at all: a system on the rim of
        /// the galaxy has headings with nothing down them, and that is a true thing to say.
        /// </summary>
        public static ProbeCorridorReading Read(
            MapPoint origin,
            MapPoint anchor,
            double bearing,
            double exitDistance,
            double halfWidth,
            MapExplored explored
        )
        {
            int edge = exitDistance <= 0
                ? 0
                : (int)Math.Round(exitDistance, MidpointRounding.AwayFromZero);

            double radians = bearing * Math.PI / 180.0;
            double stepEast = Math.Sin(radians);
            double stepNorth = Math.Cos(radians);

            // The flanks are the heading turned a quarter circle, one to each side; the one the
            // heading turns towards clockwise is the +90 side (due north's is due east).
            double flankEast = stepNorth * halfWidth;
            double flankNorth = -stepEast * halfWidth;

            Track line = new Track();
            Track clockwise = new Track();
            Track counterClockwise = new Track();
            for (int step = 0; step < edge; step++)
            {
                double east = origin.X + stepEast * step;
                double north = origin.Y + stepNorth * step;
                bool dark = !Explored(explored, anchor, east, north);
                line.At(step, dark);

                // Where the line is dark the alongside fact is redundant, and asking for it would
                // cost two fog lookups a step to produce something that must not be said.
                clockwise.At(
                    step,
                    !dark
                        && !Explored(explored, anchor, east + flankEast, north + flankNorth)
                );
                counterClockwise.At(
                    step,
                    !dark
                        && !Explored(explored, anchor, east - flankEast, north - flankNorth)
                );
            }

            return new ProbeCorridorReading(
                bearing,
                edge,
                line.Close(edge),
                clockwise.Close(edge),
                counterClockwise.Close(edge)
            );
        }

        /// <summary>The fog asked at the lattice point nearest the place in question - the identical
        /// points the inspect cursor counts fog over, so the two can never disagree.</summary>
        private static bool Explored(
            MapExplored explored,
            MapPoint anchor,
            double east,
            double north
        )
        {
            return explored(
                anchor.X + Math.Round(east - anchor.X, MidpointRounding.AwayFromZero),
                anchor.Y + Math.Round(north - anchor.Y, MidpointRounding.AwayFromZero)
            );
        }

        /// <summary>One run of samples turned into stretches: told at each step whether that step is
        /// dark, it opens a stretch on the first dark step and closes it on the first light one.
        /// </summary>
        private sealed class Track
        {
            private readonly List<UnexploredSpan> _spans = new List<UnexploredSpan>();
            private int _openedAt = -1;

            public void At(int step, bool dark)
            {
                if (dark)
                {
                    if (_openedAt < 0)
                    {
                        _openedAt = step;
                    }
                }
                else if (_openedAt >= 0)
                {
                    _spans.Add(new UnexploredSpan(_openedAt, step));
                    _openedAt = -1;
                }
            }

            public IList<UnexploredSpan> Close(int edge)
            {
                if (_openedAt >= 0)
                {
                    _spans.Add(new UnexploredSpan(_openedAt, edge));
                    _openedAt = -1;
                }

                return _spans;
            }
        }
    }
}

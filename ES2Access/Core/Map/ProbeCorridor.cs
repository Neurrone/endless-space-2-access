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

    /// <summary>What one heading out of a system holds: how far the map goes that way, and which
    /// stretches of the way there nobody has explored.</summary>
    public sealed class ProbeCorridorReading
    {
        private static readonly UnexploredSpan[] None = new UnexploredSpan[0];

        internal ProbeCorridorReading(int edge, IList<UnexploredSpan> spans)
        {
            Edge = edge;
            Spans = spans ?? (IList<UnexploredSpan>)None;
        }

        /// <summary>Whole units from the origin to the rim of the map along this heading.</summary>
        public readonly int Edge;

        /// <summary>The unexplored stretches, nearest first, never touching and never overlapping.
        /// </summary>
        public readonly IList<UnexploredSpan> Spans;

        /// <summary>Whether the last stretch runs off the edge of the map rather than ending in
        /// explored space - the difference between "unexplored out to the rim" and "unexplored, then
        /// known again".</summary>
        public bool ReachesEdge
        {
            get { return Spans.Count > 0 && Spans[Spans.Count - 1].To >= Edge; }
        }
    }

    /// <summary>
    /// What a probe would find if it were launched down one bearing - the question a player has to
    /// answer sixteen times before spending an order that cannot be recalled.
    ///
    /// A probe flies a straight line and reveals a circle around itself as it goes, so the thing worth
    /// reporting is not the line but the CORRIDOR: a stretch of the heading counts as unexplored if
    /// anything the probe would pass close to is unexplored, not merely if the exact line is. That is
    /// why each unit out is sampled three times - on the line and at both flanks - and why a sliver
    /// of EXPLORED map narrower than the corridor disappears from the answer: a probe flown through
    /// it still uncovers its flanks, so telling the player the stretch was already explored would
    /// talk them out of a launch that finds something.
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
        /// outline the galaxy's own systems make.</summary>
        public static ProbeCorridorReading Read(
            ConvexHull galaxy,
            MapPoint origin,
            double bearing,
            double halfWidth,
            MapExplored explored
        )
        {
            double radians = bearing * Math.PI / 180.0;
            return Read(
                origin,
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
        /// A rim less than a unit away is legal and answers no spans at all: a system on the rim of
        /// the galaxy has headings with nothing down them, and that is a true thing to say.
        /// </summary>
        public static ProbeCorridorReading Read(
            MapPoint origin,
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

            // The flanks are the heading turned a quarter circle, one to each side.
            double flankEast = stepNorth * halfWidth;
            double flankNorth = -stepEast * halfWidth;

            List<UnexploredSpan> spans = new List<UnexploredSpan>();
            int openedAt = -1;
            for (int step = 0; step < edge; step++)
            {
                double east = origin.X + stepEast * step;
                double north = origin.Y + stepNorth * step;
                bool dark =
                    !explored(east, north)
                    || !explored(east + flankEast, north + flankNorth)
                    || !explored(east - flankEast, north - flankNorth);

                if (dark)
                {
                    if (openedAt < 0)
                    {
                        openedAt = step;
                    }
                }
                else if (openedAt >= 0)
                {
                    spans.Add(new UnexploredSpan(openedAt, step));
                    openedAt = -1;
                }
            }

            if (openedAt >= 0)
            {
                spans.Add(new UnexploredSpan(openedAt, edge));
            }

            return new ProbeCorridorReading(edge, spans);
        }
    }
}

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
        /// (<see cref="Bearing"/> + 90), counted only where the line itself is explored, and only at
        /// tiles the probe's vision would actually reach.</summary>
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
    /// EVERY sample is taken at the same one-unit lattice the inspect cursor tests the fog at, on the
    /// lattice the anchor the caller hands in sets (home, in the game). So every number the player
    /// hears is a number they can walk to and check tile by tile, and a sliver of fog narrower than a
    /// tile can never surface as a stretch of one.
    ///
    /// A LINE sample is the tile nearest the point on the line. A FLANK sample is a tile the probe's
    /// launch would actually count as REVEALED - inside the vision circle at that step and inside the
    /// frame the caller calls the edge of the map - and of those, the outermost, which is the
    /// corridor's true edge. Two things it is deliberately not. Not "the tile nearest the vision
    /// edge", which can round outwards and put the sample half a tile's diagonal past the radius; and
    /// not a tile off the frame, which the share does not count either. Both are ways of reporting fog
    /// at map a launch would never buy, and both were doing it: this is the rule the alongside
    /// stretches and <see cref="ProbeFootprint"/>'s share are held to (2026-08-29,
    /// <c>docs/galaxy-map.md</c>), so that over the stretch the probe can actually reach, a corridor
    /// the share calls fully explored has nothing alongside it to report. Membership is
    /// <see cref="ProbeFootprint.InVision"/> and the frame's own <see cref="ConvexHull.Contains"/> -
    /// the share's two tests, not a second opinion on them - with the vision one measured against the
    /// flight LINE rather than the reach-capped segment, since the alongside stretches deliberately
    /// run on to the rim.
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
                galaxy,
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
        /// <paramref name="galaxy"/> is still wanted even though the rim is already known, because the
        /// outline decides which tiles ALONGSIDE the flight the probe would reveal, and that is a
        /// different question from how far down the flight the map goes. Null asks for no clipping at
        /// all - a caller with no outline to speak of, which in the game is nobody.
        ///
        /// <paramref name="anchor"/> is the lattice every sample is snapped to; only the fractional
        /// part of it matters, so any place on the same lattice - the empire's home, in the game -
        /// gives identical answers.
        ///
        /// A rim less than a unit away is legal and answers no spans at all: a system on the rim of
        /// the galaxy has headings with nothing down them, and that is a true thing to say.
        /// </summary>
        public static ProbeCorridorReading Read(
            ConvexHull galaxy,
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
            double flankEast = stepNorth;
            double flankNorth = -stepEast;

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
                        && FlankDark(
                            galaxy,
                            explored,
                            anchor,
                            east,
                            north,
                            flankEast,
                            flankNorth,
                            halfWidth
                        )
                );
                counterClockwise.At(
                    step,
                    !dark
                        && FlankDark(
                            galaxy,
                            explored,
                            anchor,
                            east,
                            north,
                            -flankEast,
                            -flankNorth,
                            halfWidth
                        )
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

        /// <summary>Two tiles this near to the same distance out are out equally far - the tolerance
        /// that decides a tie between them by which is nearer the step's own perpendicular rather than
        /// by an arithmetic hair.</summary>
        private const double Tie = 1e-9;

        /// <summary>
        /// Whether the map alongside the line at this step is dark, asked at the OUTERMOST tile the
        /// probe's launch would count as revealed on that side: inside the vision circle
        /// (<see cref="ProbeFootprint.InVision"/>) and inside the frame - the share's own two tests,
        /// so a tile the share counts and a tile this speaks about are the same tile. Ties go to the
        /// tile nearest the place being looked at, which is what makes a corridor whose sides run
        /// along the lattice sample the step's own tile and not a neighbour's.
        ///
        /// <paramref name="outEast"/>/<paramref name="outNorth"/> are the unit perpendicular pointing
        /// at the side being asked about, so the dot product below is the tile's distance out from the
        /// flight LINE - the line, not the reach-capped segment, because the alongside stretches run
        /// on to the rim.
        ///
        /// The search starts at the edge of the vision circle and walks IN along that perpendicular a
        /// unit at a time, taking the tiles around each place it stops at and answering with the first
        /// stop that has one it may speak about. Walking in rather than widening a box is what lets a
        /// frame cutting deep across the corridor still be answered - the outermost tile on the map
        /// may be most of the corridor's width inside the vision edge - while keeping every candidate
        /// on the step's own perpendicular. A side with no map on it at all - the frame cutting across
        /// the flight itself, which is every seaward flank of a rim system - walks in as far as the
        /// line's own tile, and that tile is explored wherever this is asked at all (the clause is
        /// only spoken where the line is light), so the side falls silent instead of inventing a
        /// stretch. A corridor with nothing on it at all says nothing for the same reason: a probe
        /// that would reveal nothing beside itself there has nothing to report.
        /// </summary>
        private static bool FlankDark(
            ConvexHull galaxy,
            MapExplored explored,
            MapPoint anchor,
            double east,
            double north,
            double outEast,
            double outNorth,
            double halfWidth
        )
        {
            for (double outAt = halfWidth; outAt > -1.0; outAt -= 1.0)
            {
                double lookEast = east + outEast * outAt;
                double lookNorth = north + outNorth * outAt;
                double centreEast =
                    anchor.X + Math.Round(lookEast - anchor.X, MidpointRounding.AwayFromZero);
                double centreNorth =
                    anchor.Y + Math.Round(lookNorth - anchor.Y, MidpointRounding.AwayFromZero);

                bool found = false;
                double outermost = 0;
                double nearest = 0;
                double sampleEast = 0;
                double sampleNorth = 0;
                for (int nudgeEast = -1; nudgeEast <= 1; nudgeEast++)
                {
                    for (int nudgeNorth = -1; nudgeNorth <= 1; nudgeNorth++)
                    {
                        double tileEast = centreEast + nudgeEast;
                        double tileNorth = centreNorth + nudgeNorth;
                        double outward =
                            (tileEast - east) * outEast + (tileNorth - north) * outNorth;
                        if (
                            !ProbeFootprint.InVision(outward * outward, halfWidth)
                            || (
                                galaxy != null
                                && !galaxy.Contains(new MapPoint(tileEast, tileNorth))
                            )
                        )
                        {
                            continue;
                        }

                        double offEast = tileEast - lookEast;
                        double offNorth = tileNorth - lookNorth;
                        double off = offEast * offEast + offNorth * offNorth;
                        if (
                            found
                            && outward <= outermost + Tie
                            && (outward < outermost - Tie || off >= nearest)
                        )
                        {
                            continue;
                        }

                        found = true;
                        outermost = outward;
                        nearest = off;
                        sampleEast = tileEast;
                        sampleNorth = tileNorth;
                    }
                }

                if (found)
                {
                    return !explored(sampleEast, sampleNorth);
                }
            }

            return false;
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

using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI
{
    /// <summary>One colony's influence circle, as the game's own resolution sees it: where it stands,
    /// how far it reaches, and whose it is. The empire is an INDEX, so nothing in here has to know what
    /// an empire is.</summary>
    public struct InfluenceSource
    {
        public double X;
        public double Y;
        public double Radius;
        public int Empire;
    }

    /// <summary>What the GAME answered when asked whose influence stands at one point of the cell.
    /// <see cref="Empire"/> is -1 for nobody's, and for a source the player is not being shown - the
    /// mod may not name what the map is not drawing, and an answer it may not name is an answer it must
    /// treat as no answer.</summary>
    public struct InfluenceAnswer
    {
        public double X;
        public double Y;
        public int Empire;
    }

    /// <summary>How much of the cell one empire holds: none of it, provably all of it, or some of it.
    /// </summary>
    public enum InfluenceCover
    {
        None,
        Whole,
        Edge,
    }

    /// <summary>
    /// WHOSE INFLUENCE STANDS OVER A CELL - the whole classification, as a set that can be compared
    /// against the last one so a sweep speaks on the crossing and not on every press.
    ///
    /// Owners are the empires holding some of the cell (one of them, where <see cref="Cover"/> is
    /// <see cref="InfluenceCover.Whole"/>); contesters are the empires whose circles reach into it
    /// without holding any sampled point of it. Both are sorted by empire index, so the same overlap
    /// reads the same way twice running and two readings of the same place compare equal.
    /// </summary>
    public sealed class InfluenceReading : IEquatable<InfluenceReading>
    {
        private static readonly int[] Nobody = new int[0];

        public InfluenceReading(InfluenceCover cover, int[] empires, int[] contesters)
        {
            Cover = cover;
            Empires = empires ?? Nobody;
            Contesters = contesters ?? Nobody;
        }

        public InfluenceCover Cover { get; private set; }

        /// <summary>The empires holding part of the cell, ascending by index.</summary>
        public int[] Empires { get; private set; }

        /// <summary>The empires reaching into the cell without holding any of it, ascending by index.
        /// </summary>
        public int[] Contesters { get; private set; }

        /// <summary>Nothing to say: nobody holds any of it and nobody is reaching for it.</summary>
        public bool Silent
        {
            get { return Cover == InfluenceCover.None && Contesters.Length == 0; }
        }

        public static readonly InfluenceReading Nothing = new InfluenceReading(
            InfluenceCover.None,
            null,
            null
        );

        /// <summary>What this classification adds to the cell's identity for the skip
        /// (<see cref="CellSignature"/>) - the empire INDEXES and the part each one plays, never a
        /// name, so the tokens are stable across languages and across a renamed empire.</summary>
        public void Tokens(IList<string> into)
        {
            string kind = Cover == InfluenceCover.Whole ? "in:" : "edge:";
            for (int i = 0; i < Empires.Length; i++)
            {
                into.Add(kind + Empires[i]);
            }

            for (int i = 0; i < Contesters.Length; i++)
            {
                into.Add("vs:" + Contesters[i]);
            }
        }

        public bool Equals(InfluenceReading other)
        {
            return other != null
                && Cover == other.Cover
                && Same(Empires, other.Empires)
                && Same(Contesters, other.Contesters);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as InfluenceReading);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Cover;
                for (int i = 0; i < Empires.Length; i++)
                {
                    hash = hash * 31 + Empires[i];
                }

                for (int i = 0; i < Contesters.Length; i++)
                {
                    hash = hash * 37 + Contesters[i];
                }

                return hash;
            }
        }

        private static bool Same(int[] one, int[] two)
        {
            if (one.Length != two.Length)
            {
                return false;
            }

            for (int i = 0; i < one.Length; i++)
            {
                if (one[i] != two[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// WHOSE INFLUENCE COVERS A SQUARE OF THE MAP, decided without ever guessing an owner.
    ///
    /// The game resolves influence one POINT at a time - every colony's field is sampled at that point
    /// and the strongest single source wins (<c>ColonizedStarSystemRepository.TryGetInfluence</c>) - so
    /// there is no question the game will answer about an AREA. A cell is an area, and the difference
    /// between standing well inside a border and standing on it is the whole reason the reading exists.
    ///
    /// So the work is split, and the split is the point of this class. Every ownership IDENTITY comes
    /// from the game, asked at points; the only thing computed here is a CERTIFICATE that the answer at
    /// those points is the answer everywhere between them. Two certificates, and both are proofs rather
    /// than heuristics:
    ///
    /// - EXACT. A circle contains a rectangle exactly when it contains all four corners (both are
    ///   convex), so where every circle reaching the cell belongs to one empire and one of them covers
    ///   the whole cell, that empire is in range at every point of it and no other empire is in range
    ///   anywhere - the game can only answer one way.
    /// - MARGIN. A source's field is <c>(1 - (d/R)^n) * R</c> inside its radius and nought outside,
    ///   which is continuous at the rim and has |f'| = n(d/R)^(n-1) &lt;= n, so it is Lipschitz-n in the
    ///   position; a max of them is Lipschitz-n too and a difference of two maxima is Lipschitz-2n.
    ///   With samples whose covering radius is h - every point of the cell within h of some sample -
    ///   a winning margin above 2n·h at every sample proves the winner cannot change between them, and
    ///   where NO other empire's circle reaches at all the rival field is nought on the whole cell and
    ///   n·h suffices.
    ///
    /// The exponent n is the game's own <c>IInfluenceService.InfluenceStrenghtPower</c>, passed in at
    /// runtime rather than written down here, because a certificate computed from the wrong exponent is
    /// a proof of nothing.
    ///
    /// And a certificate is only ever allowed to CONFIRM: it is discarded the moment any sampled answer
    /// disagrees with the empire it names, so the one thing a mistake in this arithmetic can do is drop
    /// a cell from "wholly inside" to "on the edge". There is no path by which it invents an owner.
    /// </summary>
    public static class InfluenceCell
    {
        /// <summary>How many points across the cell is asked about. Eleven, the widest the cursor
        /// itself goes and the same ceiling the fog sampling already lives with: the cost is a fixed
        /// 121 point queries however big the cell, and the covering radius - and so how thin a rim the
        /// certificate can see - scales with the cell instead.</summary>
        public const int SamplesPerSide = 11;

        /// <summary>Whether this circle covers the whole rectangle - all four corners inside it, which
        /// for two convex shapes is the whole question. The boundary counts as inside, as it does in the
        /// game's own range test (<c>!(squareMagnitude &gt; radius * radius)</c>).</summary>
        public static bool Contains(
            InfluenceSource source,
            double lowX,
            double lowY,
            double highX,
            double highY
        )
        {
            return source.Radius > 0.0
                && Inside(source, lowX, lowY)
                && Inside(source, lowX, highY)
                && Inside(source, highX, lowY)
                && Inside(source, highX, highY);
        }

        /// <summary>Whether this circle reaches the rectangle at all - the distance from its centre to
        /// the nearest point of the rectangle, which is the centre clamped into it.</summary>
        public static bool Reaches(
            InfluenceSource source,
            double lowX,
            double lowY,
            double highX,
            double highY
        )
        {
            double x = source.X < lowX ? lowX : (source.X > highX ? highX : source.X);
            double y = source.Y < lowY ? lowY : (source.Y > highY ? highY : source.Y);
            return source.Radius > 0.0 && Inside(source, x, y);
        }

        /// <summary>The game's own strength field: nought outside the hard radius, and inside it the
        /// falloff the repository computes before comparing sources
        /// (<c>ColonizedStarSystemRepository.TryGetInfluence</c> :119-121).</summary>
        public static double Strength(
            InfluenceSource source,
            double x,
            double y,
            double exponent
        )
        {
            double radius = source.Radius;
            if (radius <= 0.0)
            {
                return 0.0;
            }

            double dx = x - source.X;
            double dy = y - source.Y;
            double square = dx * dx + dy * dy;
            if (square > radius * radius)
            {
                return 0.0;
            }

            return (1.0 - Math.Pow(Math.Sqrt(square) / radius, exponent)) * radius;
        }

        /// <summary>The sample positions along one axis: the centres of <paramref name="perSide"/> equal
        /// slices, so the samples sit inside the cell rather than on its edges and every point of it is
        /// within <see cref="CoveringRadius"/> of one.</summary>
        public static double[] Axis(double low, double high, int perSide)
        {
            double[] axis = new double[perSide < 1 ? 0 : perSide];
            double step = (high - low) / perSide;
            for (int i = 0; i < axis.Length; i++)
            {
                axis[i] = low + (i + 0.5) * step;
            }

            return axis;
        }

        /// <summary>How far any point of the cell can be from the nearest sample - half the diagonal of
        /// one slice of the grid. The number the certificate's margin is measured against.</summary>
        public static double CoveringRadius(double width, double height, int perSide)
        {
            if (perSide < 1)
            {
                return double.MaxValue;
            }

            double dx = width / perSide;
            double dy = height / perSide;
            return 0.5 * Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// The cell's classification.
        ///
        /// <paramref name="answers"/> are the game's own point answers - the grid, plus the exact
        /// position of every galaxy node inside the cell, because the game's root override for a node
        /// only fires on an exact position match and a sampling that missed it would be certifying a
        /// point the game answers differently.
        ///
        /// <paramref name="gridded"/> says whether those answers include the covering grid.
        /// Where nothing reaches the cell the caller may skip the grid entirely - there is nothing to
        /// classify - and a lone node answer left over from a root override then classifies as an edge,
        /// which is the safe reading of one point known out of an area.
        /// </summary>
        public static InfluenceReading Classify(
            double lowX,
            double lowY,
            double highX,
            double highY,
            IList<InfluenceSource> sources,
            double exponent,
            IList<InfluenceAnswer> answers,
            double coveringRadius,
            bool gridded
        )
        {
            List<int> owners = new List<int>();
            if (answers != null)
            {
                for (int i = 0; i < answers.Count; i++)
                {
                    int empire = answers[i].Empire;
                    if (empire >= 0 && !owners.Contains(empire))
                    {
                        owners.Add(empire);
                    }
                }
            }

            List<int> contesters = new List<int>();
            if (sources != null)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    int empire = sources[i].Empire;
                    if (
                        empire >= 0
                        && !owners.Contains(empire)
                        && !contesters.Contains(empire)
                        && Reaches(sources[i], lowX, lowY, highX, highY)
                    )
                    {
                        contesters.Add(empire);
                    }
                }
            }

            owners.Sort();
            contesters.Sort();
            if (owners.Count == 0)
            {
                return new InfluenceReading(
                    InfluenceCover.None,
                    null,
                    contesters.ToArray()
                );
            }

            bool whole =
                owners.Count == 1
                && Certified(
                    lowX,
                    lowY,
                    highX,
                    highY,
                    sources,
                    exponent,
                    answers,
                    coveringRadius,
                    gridded,
                    owners[0]
                );
            return new InfluenceReading(
                whole ? InfluenceCover.Whole : InfluenceCover.Edge,
                owners.ToArray(),
                contesters.ToArray()
            );
        }

        /// <summary>Whether the whole cell provably belongs to <paramref name="empire"/>: the exact
        /// containment proof first, then the margin one. Both are only ever asked where every sampled
        /// answer was already this empire's, so the certificate can confirm the game and never overrule
        /// it.</summary>
        private static bool Certified(
            double lowX,
            double lowY,
            double highX,
            double highY,
            IList<InfluenceSource> sources,
            double exponent,
            IList<InfluenceAnswer> answers,
            double coveringRadius,
            bool gridded,
            int empire
        )
        {
            if (sources == null || answers == null || answers.Count == 0)
            {
                return false;
            }

            // The game has the last word. A single sample it answered differently - somebody else's,
            // or nobody's - is a point the proof would have been wrong about, so the proof goes.
            for (int i = 0; i < answers.Count; i++)
            {
                if (answers[i].Empire != empire)
                {
                    return false;
                }
            }

            bool rivalReaches = false;
            bool covers = false;
            for (int i = 0; i < sources.Count; i++)
            {
                if (!Reaches(sources[i], lowX, lowY, highX, highY))
                {
                    continue;
                }

                if (sources[i].Empire != empire)
                {
                    rivalReaches = true;
                }
                else if (Contains(sources[i], lowX, lowY, highX, highY))
                {
                    covers = true;
                }
            }

            if (covers && !rivalReaches)
            {
                return true;
            }

            if (!gridded)
            {
                return false;
            }

            double margin = (rivalReaches ? 2.0 : 1.0) * exponent * coveringRadius;
            for (int i = 0; i < answers.Count; i++)
            {
                double mine = 0.0;
                double theirs = 0.0;
                for (int s = 0; s < sources.Count; s++)
                {
                    double strength = Strength(
                        sources[s],
                        answers[i].X,
                        answers[i].Y,
                        exponent
                    );
                    if (sources[s].Empire == empire)
                    {
                        if (strength > mine)
                        {
                            mine = strength;
                        }
                    }
                    else if (strength > theirs)
                    {
                        theirs = strength;
                    }
                }

                if (mine - theirs <= margin)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Inside(InfluenceSource source, double x, double y)
        {
            double dx = x - source.X;
            double dy = y - source.Y;
            return dx * dx + dy * dy <= source.Radius * source.Radius;
        }
    }
}

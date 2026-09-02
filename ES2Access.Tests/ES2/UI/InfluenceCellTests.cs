using System.Collections.Generic;
using ES2Access.ES2.UI;
using Xunit;

namespace ES2Access.Tests.ES2.UI
{
    /// <summary>
    /// Whose influence covers a square of the map.
    ///
    /// The rule the whole class exists to keep is that a mistake in the arithmetic can only ever make
    /// the reading VAGUER: the empires are the game's own answers at points, and the only thing decided
    /// here is whether those answers hold everywhere between them. So every test below either proves a
    /// certificate that is exactly right, or proves that a certificate which could be wrong is thrown
    /// away - and none of them can produce an owner the game never named.
    ///
    /// The answers are simulated with the game's own resolution (strongest single source wins) so a
    /// certificate can be checked against the field it is about; where the point is a DISAGREEMENT, the
    /// simulated answer is overwritten by hand, which is what a root override or an unseen colony does
    /// live.
    /// </summary>
    public class InfluenceCellTests
    {
        private const double Power = 4.0;

        /// <summary>
        /// The falloff, pinned against the game's own line.
        ///
        /// <c>InfluenceCell.Strength</c> is one of the few places the mod RESTATES a game rule instead
        /// of calling it, because the repository answers whose influence stands at a point and never
        /// how strong it is there. What it restates is
        /// <c>decompiled/Assembly-CSharp/ColonizedStarSystemRepository.cs:117-121</c>:
        ///
        /// <code>
        /// if (!(squareMagnitude > maximumInfluenceRadius * maximumInfluenceRadius))
        /// {
        ///     float num3 = (float)Math.Sqrt(squareMagnitude) / maximumInfluenceRadius;
        ///     float num4 = (1f - (float)Math.Pow(num3, InfluenceStrenghtPower)) * maximumInfluenceRadius;
        /// }
        /// </code>
        ///
        /// So: nothing outside the radius (the boundary itself counts as inside, since the gate is
        /// <c>!(d² &gt; r²)</c>), the full radius at the centre, and <c>(1 - (d/r)^n)·r</c> between -
        /// which is falling, and reaches exactly nought at the rim. A source with no radius has no
        /// field at all.
        /// </summary>
        [Fact]
        public void StrengthIsTheGamesOwnFalloff()
        {
            InfluenceSource source = Source(3, 4, 10, 1);

            // The centre: d = 0, so (1 - 0^n)·r is the whole radius.
            Assert.Equal(10.0, InfluenceCell.Strength(source, 3, 4, Power), 9);

            // Halfway out along one axis: (1 - 0.5^4)·10.
            Assert.Equal(
                (1.0 - System.Math.Pow(0.5, Power)) * 10.0,
                InfluenceCell.Strength(source, 8, 4, Power),
                9
            );

            // An arbitrary point inside, computed the game's way from the square magnitude.
            double square = (6.0 * 6.0) + (8.0 * 8.0);
            Assert.Equal(
                (1.0 - System.Math.Pow(System.Math.Sqrt(square) / 10.0, Power)) * 10.0,
                InfluenceCell.Strength(source, 9, 12, Power),
                9
            );

            // The rim is inside the field (the game's gate is not-greater-than) and worth nought
            // there; a hair beyond it is outside, and outside is nought too.
            Assert.Equal(0.0, InfluenceCell.Strength(source, 13, 4, Power), 9);
            Assert.Equal(0.0, InfluenceCell.Strength(source, 13.0001, 4, Power), 9);

            // A source the game gave no radius has no field anywhere, its own centre included.
            Assert.Equal(0.0, InfluenceCell.Strength(Source(3, 4, 0, 1), 3, 4, Power), 9);
        }

        /// <summary>The exponent is the game's, not a number written down here: the same point under
        /// two different <c>InfluenceStrenghtPower</c>s answers two different strengths, so a
        /// certificate computed with the wrong one would be a proof of nothing.</summary>
        [Fact]
        public void TheFalloffExponentIsWhateverTheGamePassesIn()
        {
            InfluenceSource source = Source(0, 0, 10, 1);
            Assert.Equal(
                (1.0 - System.Math.Pow(0.5, 2.0)) * 10.0,
                InfluenceCell.Strength(source, 5, 0, 2.0),
                9
            );
            Assert.Equal(
                (1.0 - System.Math.Pow(0.5, 6.0)) * 10.0,
                InfluenceCell.Strength(source, 5, 0, 6.0),
                9
            );
        }

        [Fact]
        public void ACircleContainsARectangleExactlyWhenItHoldsEveryCorner()
        {
            InfluenceSource wide = Source(0, 0, 10, 1);
            Assert.True(InfluenceCell.Contains(wide, -1, -1, 1, 1));

            // Reaches the middle of every edge and still misses the corners: a circle of radius 5
            // against a square of side 8 centred on it, whose corners are 5.66 out.
            InfluenceSource narrow = Source(0, 0, 5, 1);
            Assert.False(InfluenceCell.Contains(narrow, -4, -4, 4, 4));
            Assert.True(InfluenceCell.Reaches(narrow, -4, -4, 4, 4));
        }

        [Fact]
        public void ACircleReachesARectangleItOnlyClips()
        {
            InfluenceSource source = Source(0, 0, 1, 1);
            // The nearest point of the square is its corner, 0.99 away - inside a radius of 1.
            Assert.True(InfluenceCell.Reaches(source, 0.7, 0.7, 2, 2));
            Assert.False(InfluenceCell.Reaches(source, 0.71, 0.71, 2, 2));
        }

        [Fact]
        public void ACellDeepInsideOneCircleIsWhollyThatEmpiresAndSaysSo()
        {
            List<InfluenceSource> sources = new List<InfluenceSource> { Source(0, 0, 6.56, 1) };
            InfluenceReading reading = Read(-0.5, -0.5, 0.5, 0.5, sources);

            Assert.Equal(InfluenceCover.Whole, reading.Cover);
            Assert.Equal(new[] { 1 }, reading.Empires);
            Assert.Empty(reading.Contesters);
        }

        [Fact]
        public void ACellStraddlingTheRimIsOnlyTheEdgeOfIt()
        {
            List<InfluenceSource> sources = new List<InfluenceSource> { Source(0, 0, 6.56, 1) };
            InfluenceReading reading = Read(6.0, -0.5, 7.0, 0.5, sources);

            Assert.Equal(InfluenceCover.Edge, reading.Cover);
            Assert.Equal(new[] { 1 }, reading.Empires);
        }

        [Fact]
        public void ACellJustInsideTheRimIsStillOnlyTheEdge()
        {
            // Wholly inside the circle by the geometry, but the field there is thin enough that the
            // margin cannot rule out the rim crossing between two samples. The exact containment proof
            // is what saves this case, and here it does not apply: the cell's far corner is outside.
            List<InfluenceSource> sources = new List<InfluenceSource> { Source(0, 0, 6.56, 1) };
            InfluenceReading reading = Read(5.6, -0.5, 6.6, 0.5, sources);

            Assert.Equal(InfluenceCover.Edge, reading.Cover);
        }

        [Fact]
        public void ContainmentCertifiesEvenWhereTheFieldIsThin()
        {
            // A cell right against the rim on the INSIDE: the strength there is a fraction of a unit,
            // far under the margin the Lipschitz bound would demand, but all four corners are inside
            // the one circle that reaches and nothing else can win a point of it.
            List<InfluenceSource> sources = new List<InfluenceSource> { Source(0, 0, 6.56, 1) };
            InfluenceReading reading = Read(5.0, -0.5, 6.0, 0.5, sources);

            Assert.Equal(InfluenceCover.Whole, reading.Cover);
        }

        [Fact]
        public void ABorderBetweenTwoEmpiresIsOneEdgeLineNamingBoth()
        {
            List<InfluenceSource> sources = new List<InfluenceSource>
            {
                Source(-5, 0, 6.0, 1),
                Source(5, 0, 6.0, 2),
            };
            InfluenceReading reading = Read(-0.5, -0.5, 0.5, 0.5, sources);

            Assert.Equal(InfluenceCover.Edge, reading.Cover);
            Assert.Equal(new[] { 1, 2 }, reading.Empires);
            Assert.Empty(reading.Contesters);
        }

        [Fact]
        public void AThinWinningMarginCostsTheCellItsCertificate()
        {
            // Well inside empire 1's circle by ownership - it wins every sample - but empire 2's rim is
            // close enough that the two fields could cross between two of them.
            List<InfluenceSource> sources = new List<InfluenceSource>
            {
                Source(-3, 0, 6.0, 1),
                Source(4.2, 0, 6.0, 2),
            };
            InfluenceReading reading = Read(-0.5, -0.5, 0.5, 0.5, sources);

            Assert.Equal(new[] { 1 }, reading.Empires);
            Assert.Equal(InfluenceCover.Edge, reading.Cover);
        }

        [Fact]
        public void OneAnswerTheGameGivesDifferentlyThrowsTheProofAway()
        {
            List<InfluenceSource> sources = new List<InfluenceSource> { Source(0, 0, 6.56, 1) };
            List<InfluenceAnswer> answers = Answers(-0.5, -0.5, 0.5, 0.5, sources);
            Assert.Equal(InfluenceCover.Whole, Classify(-0.5, -0.5, 0.5, 0.5, sources, answers).Cover);

            // The game answering nobody at one sample - which no arithmetic over these circles would
            // have predicted - must not leave the cell certified.
            InfluenceAnswer odd = answers[7];
            odd.Empire = -1;
            answers[7] = odd;

            InfluenceReading reading = Classify(-0.5, -0.5, 0.5, 0.5, sources, answers);
            Assert.Equal(InfluenceCover.Edge, reading.Cover);
            Assert.Equal(new[] { 1 }, reading.Empires);
        }

        [Fact]
        public void ARootOverrideAtOnePointMakesTheCellSomebodysEdge()
        {
            // The Unfallen root shape: nothing reaches the cell, so no grid was asked for, and the one
            // answer is the game's exact-position override at a node standing in it. One point known
            // out of an area is an edge, never an inside.
            List<InfluenceSource> sources = new List<InfluenceSource> { Source(40, 40, 3.0, 2) };
            List<InfluenceAnswer> answers = new List<InfluenceAnswer>
            {
                Answer(0.1, 0.2, 3),
            };
            InfluenceReading reading = InfluenceCell.Classify(
                -0.5,
                -0.5,
                0.5,
                0.5,
                sources,
                Power,
                answers,
                InfluenceCell.CoveringRadius(1, 1, InfluenceCell.SamplesPerSide),
                false
            );

            Assert.Equal(InfluenceCover.Edge, reading.Cover);
            Assert.Equal(new[] { 3 }, reading.Empires);
            Assert.Empty(reading.Contesters);
        }

        [Fact]
        public void SomebodyReachingWithoutHoldingAnyOfItIsAContester()
        {
            List<InfluenceSource> sources = new List<InfluenceSource>
            {
                Source(0, 0, 8.0, 1),
                Source(6, 0, 6.0, 2),
            };
            InfluenceReading reading = Read(-0.5, -0.5, 0.5, 0.5, sources);

            Assert.Equal(new[] { 1 }, reading.Empires);
            Assert.Equal(new[] { 2 }, reading.Contesters);
            Assert.False(reading.Silent);
        }

        [Fact]
        public void AnEmpireHoldingPartOfTheCellIsNeverAlsoContestingIt()
        {
            List<InfluenceSource> sources = new List<InfluenceSource>
            {
                Source(-5, 0, 6.0, 1),
                Source(5, 0, 6.0, 2),
                Source(0, 6, 6.0, 2),
            };
            InfluenceReading reading = Read(-0.5, -0.5, 0.5, 0.5, sources);

            Assert.Equal(new[] { 1, 2 }, reading.Empires);
            Assert.Empty(reading.Contesters);
        }

        [Fact]
        public void EmptySpaceIsSilentAndSaysNothingAtAll()
        {
            List<InfluenceSource> sources = new List<InfluenceSource> { Source(40, 40, 3.0, 2) };
            InfluenceReading reading = InfluenceCell.Classify(
                -0.5,
                -0.5,
                0.5,
                0.5,
                sources,
                Power,
                new List<InfluenceAnswer>(),
                InfluenceCell.CoveringRadius(1, 1, InfluenceCell.SamplesPerSide),
                false
            );

            Assert.Equal(InfluenceCover.None, reading.Cover);
            Assert.True(reading.Silent);
            Assert.Empty(Tokens(reading));
        }

        [Fact]
        public void ABiggerCursorNeedsAWiderMarginForTheSameProof()
        {
            List<InfluenceSource> sources = new List<InfluenceSource> { Source(0, 0, 6.56, 1) };
            // The same rim, read by a one-unit cell and by an eleven-unit one: the wide cell straddles
            // the rim outright, so it can only ever be an edge.
            Assert.Equal(InfluenceCover.Whole, Read(-0.5, -0.5, 0.5, 0.5, sources).Cover);
            Assert.Equal(InfluenceCover.Edge, Read(-5.5, -5.5, 5.5, 5.5, sources).Cover);
        }

        [Fact]
        public void TheTokensNameTheEmpiresAndThePartTheyPlay()
        {
            InfluenceReading whole = new InfluenceReading(
                InfluenceCover.Whole,
                new[] { 0 },
                new[] { 4 }
            );
            Assert.Equal(new[] { "in:0", "vs:4" }, Tokens(whole));

            InfluenceReading edge = new InfluenceReading(
                InfluenceCover.Edge,
                new[] { 0, 4 },
                null
            );
            Assert.Equal(new[] { "edge:0", "edge:4" }, Tokens(edge));
            Assert.NotEqual(whole, edge);
        }

        [Fact]
        public void TwoReadingsOfTheSameBorderCompareEqual()
        {
            List<InfluenceSource> sources = new List<InfluenceSource> { Source(0, 0, 6.56, 1) };
            Assert.Equal(
                Read(-0.5, -0.5, 0.5, 0.5, sources),
                Read(-0.5, -0.5, 0.5, 0.5, sources)
            );
            Assert.NotEqual(
                Read(-0.5, -0.5, 0.5, 0.5, sources),
                Read(6.0, -0.5, 7.0, 0.5, sources)
            );
        }

        [Fact]
        public void AReachIntoGroundNobodyHoldsIsItsEdgeAndNotAContest()
        {
            // The rim thinner than the sample spacing: every point query answers nobody and the
            // circle is still overhead. From inside the cell there is no held ground to contest.
            InfluenceReading contested = new InfluenceReading(
                InfluenceCover.None,
                null,
                new[] { 2, 5 }
            );
            InfluenceReading standing = contested.EdgeWhereNobodyHolds();

            Assert.Equal(InfluenceCover.Edge, standing.Cover);
            Assert.Equal(new[] { 2, 5 }, standing.Empires);
            Assert.Empty(standing.Contesters);
            Assert.False(standing.Silent);
        }

        [Fact]
        public void AContestOverHeldGroundIsLeftExactlyAsItWas()
        {
            List<InfluenceSource> sources = new List<InfluenceSource>
            {
                Source(0, 0, 8.0, 1),
                Source(6, 0, 6.0, 2),
            };
            InfluenceReading reading = Read(-0.5, -0.5, 0.5, 0.5, sources);

            Assert.Same(reading, reading.EdgeWhereNobodyHolds());
            Assert.Equal(new[] { 1 }, reading.Empires);
            Assert.Equal(new[] { 2 }, reading.Contesters);
        }

        [Fact]
        public void EmptySkyIsStillEmptySkyWhenNobodyIsReaching()
        {
            Assert.Same(
                InfluenceReading.Nothing,
                InfluenceReading.Nothing.EdgeWhereNobodyHolds()
            );
            Assert.True(InfluenceReading.Nothing.EdgeWhereNobodyHolds().Silent);
        }

        private static List<string> Tokens(InfluenceReading reading)
        {
            List<string> tokens = new List<string>();
            reading.Tokens(tokens);
            return tokens;
        }

        private static InfluenceReading Read(
            double lowX,
            double lowY,
            double highX,
            double highY,
            List<InfluenceSource> sources
        )
        {
            return Classify(
                lowX,
                lowY,
                highX,
                highY,
                sources,
                Answers(lowX, lowY, highX, highY, sources)
            );
        }

        private static InfluenceReading Classify(
            double lowX,
            double lowY,
            double highX,
            double highY,
            List<InfluenceSource> sources,
            List<InfluenceAnswer> answers
        )
        {
            return InfluenceCell.Classify(
                lowX,
                lowY,
                highX,
                highY,
                sources,
                Power,
                answers,
                InfluenceCell.CoveringRadius(
                    highX - lowX,
                    highY - lowY,
                    InfluenceCell.SamplesPerSide
                ),
                true
            );
        }

        /// <summary>The game's own resolution, simulated over the covering grid: at each point the
        /// single strongest source wins and everything out of range answers nobody.</summary>
        private static List<InfluenceAnswer> Answers(
            double lowX,
            double lowY,
            double highX,
            double highY,
            List<InfluenceSource> sources
        )
        {
            double[] xs = InfluenceCell.Axis(lowX, highX, InfluenceCell.SamplesPerSide);
            double[] ys = InfluenceCell.Axis(lowY, highY, InfluenceCell.SamplesPerSide);
            List<InfluenceAnswer> answers = new List<InfluenceAnswer>();
            for (int a = 0; a < xs.Length; a++)
            {
                for (int b = 0; b < ys.Length; b++)
                {
                    int winner = -1;
                    double best = 0.0;
                    for (int s = 0; s < sources.Count; s++)
                    {
                        if (!InfluenceCell.Reaches(sources[s], xs[a], ys[b], xs[a], ys[b]))
                        {
                            continue;
                        }

                        double strength = InfluenceCell.Strength(
                            sources[s],
                            xs[a],
                            ys[b],
                            Power
                        );
                        if (winner < 0 || strength > best)
                        {
                            winner = sources[s].Empire;
                            best = strength;
                        }
                    }

                    answers.Add(Answer(xs[a], ys[b], winner));
                }
            }

            return answers;
        }

        private static InfluenceAnswer Answer(double x, double y, int empire)
        {
            InfluenceAnswer answer = new InfluenceAnswer();
            answer.X = x;
            answer.Y = y;
            answer.Empire = empire;
            return answer;
        }

        private static InfluenceSource Source(double x, double y, double radius, int empire)
        {
            InfluenceSource source = new InfluenceSource();
            source.X = x;
            source.Y = y;
            source.Radius = radius;
            source.Empire = empire;
            return source;
        }
    }
}

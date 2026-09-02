using ES2Access.Core.Map;
using Xunit;
using static ES2Access.Tests.Map.MapFixtures;

namespace ES2Access.Tests.Map
{
    /// <summary>
    /// Which constellation a place belongs to, when the constellations are only ever given as the
    /// stars they are made of.
    ///
    /// The awkward case is the one every galaxy has: a long constellation's outline swallows a
    /// compact neighbour whole, so two regions both "hold" the same spot. The rule under test is
    /// that the stars decide — the region whose own members lie nearest — and that a region whose
    /// outline does NOT hold the place never wins however near its stars are.
    /// </summary>
    public class ConstellationRegionsTests
    {
        private static readonly double[] Square = { 0, 0, 10, 0, 10, 10, 0, 10 };

        /// <summary>A band from x=5 to x=20 across the middle of <see cref="Square"/> — the
        /// interlock: its two western stars sit inside the square's outline.</summary>
        private static readonly double[] Band = { 5, 4, 5, 6, 20, 6, 20, 4 };

        private static readonly double[] FarAway = { 30, 30, 40, 30, 40, 40, 30, 40 };

        [Fact]
        public void AddAnswersTheIndexThatNamesTheRegion()
        {
            ConstellationRegions regions = new ConstellationRegions();

            Assert.Equal(0, regions.Add(Places(Square)));
            Assert.Equal(1, regions.Add(Places(FarAway)));
            Assert.Equal(2, regions.Count);
        }

        [Fact]
        public void AplaceInOneOutlineAnswersThatRegion()
        {
            ConstellationRegions regions = new ConstellationRegions();
            int square = regions.Add(Places(Square));
            int far = regions.Add(Places(FarAway));

            Assert.Equal(square, regions.Classify(new MapPoint(2, 2)));
            Assert.Equal(far, regions.Classify(new MapPoint(35, 35)));
        }

        [Fact]
        public void APlaceInNoOutlineAnswersNoRegion()
        {
            ConstellationRegions regions = new ConstellationRegions();
            regions.Add(Places(Square));
            regions.Add(Places(FarAway));

            Assert.Equal(-1, regions.Classify(new MapPoint(100, 100)));
        }

        [Fact]
        public void AConstellationOfOneStarStillHoldsThatStar()
        {
            ConstellationRegions regions = new ConstellationRegions();
            regions.Add(Places(Square));
            int lone = regions.Add(Places(50, 50));

            Assert.Equal(lone, regions.Classify(new MapPoint(50, 50)));
            Assert.Equal(-1, regions.Classify(new MapPoint(50, 51)));
        }

        [Fact]
        public void WhereTwoOutlinesOverlapTheNearerStarsWin()
        {
            ConstellationRegions regions = new ConstellationRegions();
            regions.Add(Places(Square));
            int band = regions.Add(Places(Band));

            Assert.Equal(band, regions.Classify(new MapPoint(6, 5)));
        }

        [Fact]
        public void TheSameOverlapAnswersTheOtherRegionWhereItsOwnStarsAreNearer()
        {
            ConstellationRegions regions = new ConstellationRegions();
            // The square gains a star at (9, 5) — inside its own outline, so the shape is unchanged,
            // but now the nearest star to the eastern half of the overlap.
            int square = regions.Add(Places(0, 0, 10, 0, 10, 10, 0, 10, 9, 5));
            regions.Add(Places(Band));

            Assert.Equal(square, regions.Classify(new MapPoint(9.5, 5)));
        }

        [Fact]
        public void AnOutlineThatDoesNotHoldThePlaceNeverWinsHoweverNearItsStarsAre()
        {
            ConstellationRegions regions = new ConstellationRegions();
            int square = regions.Add(Places(Square));
            // A band just east of the square: its stars are within 0.6 of the query, the square's
            // nearest is 5 away — and it still loses, because its outline stops at x=10.
            regions.Add(Places(10.5, 4, 10.5, 6, 12, 6, 12, 4));

            Assert.Equal(square, regions.Classify(new MapPoint(9.9, 5)));
        }

        [Fact]
        public void EquallyNearStarsGoToTheRegionAddedFirst()
        {
            ConstellationRegions regions = new ConstellationRegions();
            int first = regions.Add(Places(0, 0, 10, 0, 5, 5));
            regions.Add(Places(5, 5, 10, 10, 0, 10));

            Assert.Equal(first, regions.Classify(new MapPoint(5, 5)));
        }

        [Fact]
        public void CoordinateArraysBuildTheSameRegionAsPlacesDo()
        {
            ConstellationRegions regions = new ConstellationRegions();
            int square = regions.Add(new double[] { 0, 10, 10, 0 }, new double[] { 0, 0, 10, 10 }, 4);

            Assert.Equal(square, regions.Classify(5, 5));
            Assert.Equal(-1, regions.Classify(11, 5));
        }

        [Fact]
        public void DistanceRanksTheRegionsAPlaceIsOutside()
        {
            ConstellationRegions regions = new ConstellationRegions();
            int square = regions.Add(Places(Square));
            int far = regions.Add(Places(FarAway));

            Assert.Equal(0.0, regions.DistanceTo(square, new MapPoint(5, 5)));
            Assert.Equal(3.0, regions.DistanceTo(square, new MapPoint(13, 5)), 9);
            Assert.True(regions.DistanceTo(far, new MapPoint(13, 5)) > regions.DistanceTo(square, new MapPoint(13, 5)));
        }

        [Fact]
        public void SeparateConstellationsAuditClean()
        {
            ConstellationRegions regions = new ConstellationRegions();
            regions.Add(Places(Square));
            regions.Add(Places(FarAway));

            RegionAudit audit = regions.Audit();

            Assert.Equal(2, audit.Regions);
            Assert.Equal(8, audit.Members);
            Assert.Equal(0, audit.OutsideOwnHull);
            Assert.Equal(0, audit.InsideAnotherHull);
            Assert.Equal(0, audit.ClassifiedElsewhere);
        }

        [Fact]
        public void InterlockedConstellationsKeepTheInvariantAndAreCountedAsOverlap()
        {
            ConstellationRegions regions = new ConstellationRegions();
            regions.Add(Places(Square));
            regions.Add(Places(Band));

            RegionAudit audit = regions.Audit();

            Assert.Equal(0, audit.OutsideOwnHull);
            // The band's two western stars sit inside the square's outline; nothing of the square
            // sits inside the band's.
            Assert.Equal(2, audit.InsideAnotherHull);
            // Ambiguous shapes, unambiguous stars: every star is still its own region's nearest.
            Assert.Equal(0, audit.ClassifiedElsewhere);
        }

        [Fact]
        public void TwoRegionsClaimingOneStarShowUpAsADisagreement()
        {
            ConstellationRegions regions = new ConstellationRegions();
            regions.Add(Places(0, 0, 10, 0, 5, 5));
            regions.Add(Places(5, 5, 10, 10, 0, 10));

            RegionAudit audit = regions.Audit();

            Assert.Equal(0, audit.OutsideOwnHull);
            Assert.Equal(1, audit.ClassifiedElsewhere);
            Assert.Equal(1, audit.FirstDisagreeingRegion);
            Assert.Equal(0, audit.FirstDisagreeingMember);
            Assert.Equal(0, audit.FirstDisagreeingAnswer);
        }

        [Fact]
        public void TheVerdictReadsAsOneLine()
        {
            ConstellationRegions regions = new ConstellationRegions();
            regions.Add(Places(Square));
            regions.Add(Places(Band));

            string verdict = regions.Audit().ToString();

            Assert.Contains("regions 2, members 8", verdict);
            Assert.Contains("outside own hull 0", verdict);
            Assert.Contains("inside another hull 2", verdict);
            Assert.Contains("classified elsewhere 0", verdict);
        }
    }
}

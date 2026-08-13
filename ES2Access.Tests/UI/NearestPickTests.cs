using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Turning a point the game sent the camera to back into the thing that stands there.
    ///
    /// Each rule below is a way the answer goes wrong on a map: a point out in empty space gets pinned
    /// on a star half a galaxy off, or a fleet parked at a star answers for the star itself because the
    /// two share a position exactly.
    /// </summary>
    public class NearestPickTests
    {
        [Fact]
        public void NothingOfferedIsNoAnswer()
        {
            NearestPick pick = new NearestPick(3);

            Assert.False(pick.Found);
            Assert.Equal(-1, pick.Index);
        }

        [Fact]
        public void FurtherAwayThanTheRadiusIsNoAnswer()
        {
            NearestPick pick = new NearestPick(3);

            Assert.False(pick.Offer(0, 9.1));
            Assert.False(pick.Found);
        }

        [Fact]
        public void ExactlyAtTheRadiusStillCounts()
        {
            NearestPick pick = new NearestPick(3);

            Assert.True(pick.Offer(7, 9));
            Assert.Equal(7, pick.Index);
        }

        [Fact]
        public void TheNearerOfferWins()
        {
            NearestPick pick = new NearestPick(3);
            pick.Offer(1, 4);

            Assert.True(pick.Offer(2, 1));
            Assert.Equal(2, pick.Index);
        }

        [Fact]
        public void AFurtherOfferDoesNotDisplaceTheNearestAlreadyFound()
        {
            NearestPick pick = new NearestPick(3);
            pick.Offer(1, 1);

            Assert.False(pick.Offer(2, 4));
            Assert.Equal(1, pick.Index);
        }

        [Fact]
        public void ATieGoesToWhoeverWasOfferedFirst()
        {
            NearestPick pick = new NearestPick(3);
            pick.Offer(1, 2);

            Assert.False(pick.Offer(2, 2));
            Assert.Equal(1, pick.Index);
        }
    }
}

using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Tests.Speech;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The trade routes as the scan view draws them. Every rule here is a way a readout stops agreeing
    /// with the picture: a lane counted twice because two routes crossed it in opposite directions, a
    /// blockade that stops travelling down the rest of its route, or a lane carrying both kinds of
    /// traffic being reported as simply blockaded when the game paints it a third colour.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class TradeLanesTests
    {
        public TradeLanesTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void EachLegBecomesALane()
        {
            TradeLanes lanes = new TradeLanes();
            lanes.Add(1, 2, false);
            lanes.Add(2, 3, false);

            Assert.Equal(2, lanes.Count);
            Assert.Equal(1, lanes[0].Start);
            Assert.Equal(2, lanes[0].End);
            Assert.Equal(1, lanes[0].Open);
            Assert.Equal(3, lanes[1].End);
        }

        [Fact]
        public void TwoRoutesOverOneLaneAreOneLine()
        {
            TradeLanes lanes = new TradeLanes();
            lanes.Add(1, 2, false);
            lanes.Add(1, 2, false);

            Assert.Equal(1, lanes.Count);
            Assert.Equal(2, lanes[0].Open);
        }

        [Fact]
        public void TheLaneHasNoDirection()
        {
            TradeLanes lanes = new TradeLanes();
            lanes.Add(7, 4, false);
            lanes.Add(4, 7, false);

            Assert.Equal(1, lanes.Count);
            Assert.Equal(2, lanes[0].Open);
            // The orientation is the one the first leg arrived with, which is the renderer's too.
            Assert.Equal(7, lanes[0].Start);
            Assert.Equal(4, lanes[0].End);
        }

        [Fact]
        public void OpenAndBlockadedAreCountedApartOnTheSameLane()
        {
            TradeLanes lanes = new TradeLanes();
            lanes.Add(1, 2, false);
            lanes.Add(1, 2, true);

            Assert.Equal(1, lanes.Count);
            Assert.Equal(1, lanes[0].Open);
            Assert.Equal(1, lanes[0].Blockaded);
            Assert.Equal(2, lanes[0].Total);
        }

        [Fact]
        public void ClearingStartsTheWalkAgain()
        {
            TradeLanes lanes = new TradeLanes();
            lanes.Add(1, 2, false);
            lanes.Clear();
            lanes.Add(1, 2, true);

            Assert.Equal(1, lanes.Count);
            Assert.Equal(0, lanes[0].Open);
            Assert.Equal(1, lanes[0].Blockaded);
        }

        [Fact]
        public void OneRouteSaysSoInWords()
        {
            Assert.Equal("One trade route", TradeLanes.Text(1, 0));
            Assert.Equal("3 trade routes", TradeLanes.Text(3, 0));
        }

        [Fact]
        public void AWhollyBlockadedLaneSaysSoInBothForms()
        {
            Assert.Equal("One trade route, blockaded", TradeLanes.Text(0, 1));
            Assert.Equal("2 trade routes, blockaded", TradeLanes.Text(0, 2));
        }

        [Fact]
        public void ALaneCarryingBothKeepsItsOwnSentence()
        {
            Assert.Equal("3 trade routes, 1 blockaded", TradeLanes.Text(2, 1));
        }

        [Fact]
        public void ALaneNothingRunsOverSaysNothing()
        {
            Assert.Null(TradeLanes.Text(0, 0));
        }
    }
}

using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The inspect cursor's geometry. The failure that matters here is inaudible: a cell rule that is
    /// half a unit out makes a sweep of the map skip a star or report it from two neighbouring cells,
    /// and either one sounds like a perfectly ordinary reading.
    /// </summary>
    public class InspectGridTests
    {
        [Fact]
        public void SizesClimbAndStopAtTheEndsOfTheLadder()
        {
            Assert.Equal(3, InspectGrid.Grow(1));
            Assert.Equal(5, InspectGrid.Grow(3));
            Assert.Equal(11, InspectGrid.Grow(9));
            Assert.Equal(11, InspectGrid.Grow(11));
            Assert.Equal(9, InspectGrid.Shrink(11));
            Assert.Equal(1, InspectGrid.Shrink(3));
            Assert.Equal(1, InspectGrid.Shrink(1));
        }

        [Fact]
        public void AnEvenOrOutOfRangeSizeIsBroughtBackToTheLadder()
        {
            Assert.Equal(3, InspectGrid.Clamp(4));
            Assert.Equal(1, InspectGrid.Clamp(0));
            Assert.Equal(1, InspectGrid.Clamp(-5));
            Assert.Equal(11, InspectGrid.Clamp(50));
        }

        [Fact]
        public void TheCellSpansItsWholeSizeAboutAWholeUnitCentre()
        {
            Assert.Equal(-1.5, InspectGrid.Low(0, 3));
            Assert.Equal(1.5, InspectGrid.High(0, 3));
            Assert.Equal(-0.5, InspectGrid.Low(0, 1));
            Assert.Equal(0.5, InspectGrid.High(0, 1));
            Assert.Equal(-5.5, InspectGrid.Low(0, 11));
            Assert.Equal(5.5, InspectGrid.High(0, 11));
        }

        [Fact]
        public void ABoundaryPointBelongsToExactlyOneCell()
        {
            // 1.5 is the seam between the cell at 0 and the cell at 3 for a three-unit cursor.
            Assert.False(InspectGrid.Holds(0, 0, 3, 1.5, 0.0));
            Assert.True(InspectGrid.Holds(3, 0, 3, 1.5, 0.0));
            Assert.True(InspectGrid.Holds(0, 0, 3, -1.5, 0.0));
            Assert.False(InspectGrid.Holds(-3, 0, 3, -1.5, 0.0));
        }

        [Fact]
        public void SteppingByTheCursorSizeTilesTheMap()
        {
            Assert.Equal(3, InspectGrid.Step(0, 3, 1));
            Assert.Equal(-3, InspectGrid.Step(0, 3, -1));
            Assert.Equal(11, InspectGrid.Step(0, 11, 1));
            // Every point between two neighbouring centres is in one of them and no point is in both.
            for (int tenths = -30; tenths <= 30; tenths++)
            {
                double x = tenths / 10.0;
                int inside = 0;
                for (int centre = -6; centre <= 6; centre += 3)
                {
                    if (InspectGrid.Holds(centre, 0, 3, x, 0.0))
                    {
                        inside++;
                    }
                }

                Assert.Equal(1, inside);
            }
        }

        [Fact]
        public void ALaneIsFoundWhereItCrossesACellHoldingNeitherEnd()
        {
            // The fixture's home Dusay (0, 0) to Primus (17, 21): the line passes through the cell at
            // (3, 3) without either star being in it.
            Assert.True(InspectGrid.Crosses(3, 3, 3, 0, 0, 17, 21));
            Assert.False(InspectGrid.Holds(3, 3, 3, 0, 0));
            Assert.False(InspectGrid.Holds(3, 3, 3, 17, 21));
            // And it is nowhere near a cell off to the side of it.
            Assert.False(InspectGrid.Crosses(12, 3, 3, 0, 0, 17, 21));
        }

        [Fact]
        public void ALaneWithAnEndInTheCellCrossesIt()
        {
            Assert.True(InspectGrid.Crosses(0, 0, 3, 0, 0, 17, 21));
            Assert.True(InspectGrid.Crosses(17, 21, 3, 0, 0, 17, 21));
        }

        [Fact]
        public void ALaneThatStopsShortOfTheCellDoesNotCrossIt()
        {
            Assert.False(InspectGrid.Crosses(9, 0, 3, 0.0, 0.0, 5.0, 0.0));
            Assert.True(InspectGrid.Crosses(9, 0, 3, 0.0, 0.0, 12.0, 0.0));
        }

        [Fact]
        public void TheWestmostEndIsNamedFirstAndTiesGoSouth()
        {
            Assert.True(InspectGrid.WestmostFirst(0, 0, 17, 21));
            Assert.False(InspectGrid.WestmostFirst(17, 21, 0, 0));
            Assert.True(InspectGrid.WestmostFirst(5, -2, 5, 8));
            Assert.False(InspectGrid.WestmostFirst(5, 8, 5, -2));
        }

        [Fact]
        public void TheEdgeOfTheGalaxyIsTheLastCellThatTouchesIt()
        {
            Assert.True(InspectGrid.InBounds(10, 10, 1, -10, 10, -10, 10));
            Assert.False(InspectGrid.InBounds(12, 0, 1, -10, 10, -10, 10));
            Assert.False(InspectGrid.InBounds(0, -12, 1, -10, 10, -10, 10));
        }

        /// <summary>The outermost system sits at a fractional position that ROUNDS into the boundary
        /// cell (Byrtus, north -41.6, announced in cell -42): the cell that holds it must be
        /// reachable, and the first cell wholly past the extent is the edge.</summary>
        [Fact]
        public void TheCellHoldingTheOutermostSystemIsReachable()
        {
            // South edge at -41.6: cell -42 (span -42.5..-41.5) touches it, cell -43 does not.
            Assert.True(InspectGrid.InBounds(0, -42, 1, -50, 50, -41.6, 10));
            Assert.False(InspectGrid.InBounds(0, -43, 1, -50, 50, -41.6, 10));

            // The wider cursor reaches further by the same rule: a 3-wide cell centred at -43
            // still touches -41.6 (span -44.5..-41.5).
            Assert.True(InspectGrid.InBounds(0, -43, 3, -50, 50, -41.6, 10));
            Assert.False(InspectGrid.InBounds(0, -46, 3, -50, 50, -41.6, 10));

            // Half-open on the high edge, exactly as Holds: a system AT a cell's high edge belongs
            // to the neighbour, so the neighbour is the last reachable cell, not this one plus one.
            Assert.True(InspectGrid.InBounds(0, 10, 1, -50, 50, -50, 10.0));
            Assert.False(InspectGrid.InBounds(0, 11, 1, -50, 50, -50, 10.0));
        }

        [Fact]
        public void TheSquaresACellCoversAreItsSizeSquared()
        {
            Assert.Equal(1, InspectGrid.Squares(1));
            Assert.Equal(9, InspectGrid.Squares(3));
            Assert.Equal(121, InspectGrid.Squares(11));
            Assert.Equal(0, InspectGrid.HalfWidth(1));
            Assert.Equal(1, InspectGrid.HalfWidth(3));
            Assert.Equal(5, InspectGrid.HalfWidth(11));
        }
    }
}

using ES2Access.ES2.UI;
using Xunit;

namespace ES2Access.Tests.ES2.UI
{
    /// <summary>What a colonizable world's row says about what it would produce.</summary>
    public class ScannerOutputsTests
    {
        [Theory]
        [InlineData(8f, true)]
        [InlineData(1f, true)]
        [InlineData(0.9f, false)]
        [InlineData(0f, false)]
        public void AFigureThePageWouldDrawAsZeroIsNotSaid(float value, bool said)
        {
            Assert.Equal(said, ScannerOutputs.Says(value));
        }

        [Fact]
        public void AnOutputIsTheResourcesShortNameAndTheFigure()
        {
            Assert.Equal("Food 8", ScannerOutputs.Line("Food", "8"));
        }

        [Fact]
        public void AnOutputWithHalfOfItMissingIsNoLineAtAll()
        {
            Assert.Null(ScannerOutputs.Line(null, "8"));
            Assert.Null(ScannerOutputs.Line("Food", ""));
        }
    }
}

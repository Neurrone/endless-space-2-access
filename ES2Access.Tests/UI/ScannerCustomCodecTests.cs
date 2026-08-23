using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// One slot on its way to the settings file and back. The failure this guards against is silent
    /// in the worst way: a player's categories are configured once and read for months, so a codec
    /// that loses a keyword or invents a selector would be discovered as "the scanner stopped
    /// finding things", with nothing to hear and nothing to blame.
    /// </summary>
    public class ScannerCustomCodecTests
    {
        private static ScannerCustomCategory Made(string name)
        {
            ScannerCustomCategory category = new ScannerCustomCategory(name);
            category.AddSelector(new ScannerSelector("systems", "enemy"));
            category.AddSelector(new ScannerSelector("anomalies", "AnomalyGoldRush"));
            category.AddKeyword("Tundra");
            category.AddKeyword("Sophon");
            return category;
        }

        private static void Same(ScannerCustomCategory one, ScannerCustomCategory two)
        {
            Assert.Equal(one.Name, two.Name);
            Assert.Equal(one.Selectors.Count, two.Selectors.Count);
            for (int i = 0; i < one.Selectors.Count; i++)
            {
                Assert.True(one.Selectors[i].Same(two.Selectors[i]));
            }

            Assert.Equal(one.Keywords, two.Keywords);
        }

        [Fact]
        public void AWholeCategorySurvivesTheRoundTrip()
        {
            ScannerCustomCategory category = Made("Threats");
            Same(category, ScannerCustomCodec.Decode(ScannerCustomCodec.Encode(category)));
        }

        [Theory]
        [InlineData("Enemies, mostly")]
        [InlineData("Fleets | systems")]
        [InlineData("Colon: here")]
        [InlineData("Back\\slash")]
        public void EverySeparatorTheTextItselfHoldsComesBack(string name)
        {
            ScannerCustomCategory category = Made(name);
            category.AddKeyword("a, b: c | d \\ e");
            Same(category, ScannerCustomCodec.Decode(ScannerCustomCodec.Encode(category)));
        }

        [Fact]
        public void AnEmptySlotEncodesToNothingAtAll()
        {
            Assert.Null(ScannerCustomCodec.Encode(null));
        }

        [Fact]
        public void ACategoryWithNothingInItStillCarriesItsName()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Empty");
            ScannerCustomCategory back = ScannerCustomCodec.Decode(
                ScannerCustomCodec.Encode(category)
            );
            Assert.Equal("Empty", back.Name);
            Assert.Empty(back.Selectors);
            Assert.Empty(back.Keywords);
        }

        [Fact]
        public void ALineAnOlderBuildWroteStillReads()
        {
            ScannerCustomCategory back = ScannerCustomCodec.Decode("Threats");
            Assert.Equal("Threats", back.Name);
            Assert.Empty(back.Selectors);
            Assert.Empty(back.Keywords);

            ScannerCustomCategory half = ScannerCustomCodec.Decode("Threats|systems:enemy");
            Assert.Single(half.Selectors);
            Assert.Empty(half.Keywords);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("|systems:enemy|Tundra")]
        [InlineData(null)]
        public void CorruptionIsAnEmptySlotRatherThanHalfACategory(string text)
        {
            Assert.Null(ScannerCustomCodec.Decode(text));
        }

        [Fact]
        public void AMalformedSelectorIsDroppedAndTheRestIsKept()
        {
            ScannerCustomCategory back = ScannerCustomCodec.Decode(
                "Threats|systems:enemy,rubbish,fleets:enemy|Tundra"
            );
            Assert.Equal(2, back.Selectors.Count);
            Assert.Equal("systems", back.Selectors[0].Category);
            Assert.Equal("fleets", back.Selectors[1].Category);
            Assert.Equal(new[] { "Tundra" }, back.Keywords);
        }
    }
}

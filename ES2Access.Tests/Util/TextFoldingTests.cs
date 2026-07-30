using ES2Access.Core.Util;
using Xunit;

namespace ES2Access.Tests.Util
{
    /// <summary>
    /// The fold that lets "already said" be asked of text the game wrote twice in two styles.
    ///
    /// It is the whole of the test behind the icon-substitution dedupe, so what it keeps and what it
    /// throws away is the rule rather than an implementation detail: an icon named "Over
    /// Colonization" sits against the drawn words "Over-colonization penalty", and reading both
    /// stutters. The hyphen, the capital and the spacing are exactly the differences that must not
    /// hide the repetition.
    /// </summary>
    public class TextFoldingTests
    {
        [Fact]
        public void SpellingDifferencesThatOnlyLookDifferentFoldTogether()
        {
            Assert.Equal(
                TextUtil.LettersAndDigits("Over Colonization"),
                TextUtil.LettersAndDigits("Over-colonization")
            );
        }

        [Fact]
        public void PunctuationAndMarkupAreDroppedAndLettersLowered()
        {
            Assert.Equal("38fromsystemdustproduction", TextUtil.LettersAndDigits("+38 from System Dust production"));
            Assert.Equal("revert44", TextUtil.LettersAndDigits("#REVERT#44)"));
        }

        [Fact]
        public void DigitsSurviveSoAQuantityStaysDistinguishable()
        {
            Assert.Equal("strat01titanium", TextUtil.LettersAndDigits("Strat01titanium"));
        }

        [Fact]
        public void NothingToFoldIsTheEmptyString()
        {
            Assert.Equal(string.Empty, TextUtil.LettersAndDigits(null));
            Assert.Equal(string.Empty, TextUtil.LettersAndDigits("  -  "));
        }
    }
}

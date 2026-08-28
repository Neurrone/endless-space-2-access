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

        // ---- "are these words, or only a figure" ----
        //
        // The test a drawn string has to pass before it may NAME a nested tooltip entry. Measured
        // live: a hero's mastery lines drew "0/11", "3/11", "1/10", "0/7" and a planet card's figures
        // drew "50", "30", "30", "22", "3" - two of them identical - and named four and five sibling
        // nodes that used to carry the wrapper's own names.

        [Fact]
        public void AFigureIsNotWords()
        {
            Assert.False(TextUtil.HasLetters("0/11"));
            Assert.False(TextUtil.HasLetters("50"));
            Assert.False(TextUtil.HasLetters("+38"));
            Assert.False(TextUtil.HasLetters("3 / 17"));
            Assert.False(TextUtil.HasLetters("  -  "));
        }

        /// <summary>A name that merely CONTAINS digits is still a name - the guard rejects strings with
        /// no word in them, never strings with a number in them.</summary>
        [Fact]
        public void ANameWithDigitsInItIsStillWords()
        {
            Assert.True(TextUtil.HasLetters("1st Patriots Navy"));
            Assert.True(TextUtil.HasLetters("Titanium-70"));
            Assert.True(TextUtil.HasLetters("Empire Development III"));
            Assert.True(TextUtil.HasLetters("0/11 Wit"));
        }

        [Fact]
        public void NothingAtAllIsNotWords()
        {
            Assert.False(TextUtil.HasLetters(null));
            Assert.False(TextUtil.HasLetters(string.Empty));
        }
    }
}

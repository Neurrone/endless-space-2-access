using ES2Access.Core.Util;
using Xunit;

namespace ES2Access.Tests.Util
{
    /// <summary>
    /// The whitelist that turns a faction's name into part of a file's name. Every failure here is a
    /// file the game cannot write and a set of bookmarks that silently never persists - and the
    /// inputs that break it are the ones nobody types on purpose, which is exactly why they are
    /// pinned here rather than met live.
    /// </summary>
    public class FileNameTextTests
    {
        [Fact]
        public void AnOrdinaryNameComesBackAsItself()
        {
            Assert.Equal("FactionTerransTutorial", FileNameText.Safe("FactionTerransTutorial", 48));
        }

        /// <summary>Everything that is not a letter or a digit goes - spaces, punctuation, the path
        /// separators, and the trailing dot Windows will not keep.</summary>
        [Fact]
        public void OnlyLettersAndDigitsSurvive()
        {
            Assert.Equal("MyFaction2", FileNameText.Safe("My Faction #2!", 48));
            Assert.Equal("ab", FileNameText.Safe("a/b", 48));
            Assert.Equal("ab", FileNameText.Safe("a\\b", 48));
            Assert.Equal("ab", FileNameText.Safe("a:*?\"<>|b", 48));
            Assert.Equal("name", FileNameText.Safe("name.", 48));
            Assert.Equal("name", FileNameText.Safe("  name  ", 48));
        }

        /// <summary>Letters of any script are letters. The point of the whitelist is that it needs no
        /// table of forbidden characters, so a name in another alphabet keeps working.</summary>
        [Fact]
        public void LettersOfAnyScriptAreKept()
        {
            Assert.Equal("Империя", FileNameText.Safe("Империя", 48));
            Assert.Equal("帝国7", FileNameText.Safe("帝国 7", 48));
        }

        /// <summary>Nothing left is the caller's cue to fall back to a name that needs no text.
        /// </summary>
        [Fact]
        public void ANameWithNothingKeepableComesBackEmpty()
        {
            Assert.Equal(string.Empty, FileNameText.Safe("!!! ---", 48));
            Assert.Equal(string.Empty, FileNameText.Safe("   ", 48));
            Assert.Equal(string.Empty, FileNameText.Safe(null, 48));
            Assert.Equal(string.Empty, FileNameText.Safe(string.Empty, 48));
        }

        [Fact]
        public void ItIsCutToTheCapAskedFor()
        {
            Assert.Equal("abcde", FileNameText.Safe("abcdefghij", 5));
            Assert.Equal("abcde", FileNameText.Safe("a b c d e f", 5));
            Assert.Equal(string.Empty, FileNameText.Safe("abc", 0));
            Assert.Equal(string.Empty, FileNameText.Safe("abc", -1));
        }

        /// <summary>The cut never splits a surrogate pair: half a pair is not a character, and some
        /// file systems refuse it outright. A pair costs the two chars it is written with.</summary>
        [Fact]
        public void ACutNeverSplitsASurrogatePair()
        {
            // U+20000, a letter outside the basic plane, written as two chars.
            string wide = char.ConvertFromUtf32(0x20000);
            Assert.Equal("a", FileNameText.Safe("a" + wide, 2));
            Assert.Equal("a" + wide, FileNameText.Safe("a" + wide, 3));
            Assert.Equal(wide + wide, FileNameText.Safe(wide + wide + wide, 5));
        }

        /// <summary>A lone surrogate is not a character at all and is dropped like any other
        /// non-letter, without eating the one after it.</summary>
        [Fact]
        public void ALoneSurrogateIsDropped()
        {
            Assert.Equal("ab", FileNameText.Safe("a\uD800b", 48));
            Assert.Equal("ab", FileNameText.Safe("a\uDC00b", 48));
        }
    }
}

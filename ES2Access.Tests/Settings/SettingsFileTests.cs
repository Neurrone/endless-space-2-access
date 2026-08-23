using System.Collections.Generic;
using ES2Access.Core.Settings;
using Xunit;

namespace ES2Access.Tests.Settings
{
    public class SettingsFileTests
    {
        private static SettingsFile Parse(params string[] lines)
        {
            return SettingsFile.Parse(lines);
        }

        private static string RoundTrip(string value)
        {
            SettingsFile file = new SettingsFile();
            file.Set("k", value);
            return SettingsFile.Parse(file.ToLines()).Get("k");
        }

        [Fact]
        public void AMissingKeyReadsAsNothing()
        {
            Assert.Null(new SettingsFile().Get("keys.ui.down"));
            Assert.False(new SettingsFile().Has("keys.ui.down"));
        }

        [Fact]
        public void AValueSurvivesTheRoundTrip()
        {
            Assert.Equal("ui.down: DownArrow,", RoundTrip("ui.down: DownArrow,"));
        }

        [Fact]
        public void AnEmptyValueIsAValueAndNotAnAbsence()
        {
            SettingsFile file = SettingsFile.Parse(new[] { "k =" });
            Assert.True(file.Has("k"));
            Assert.Equal(string.Empty, file.Get("k"));
        }

        [Fact]
        public void SurroundingSpacesAreNotPartOfAnOrdinaryValue()
        {
            Assert.Equal("value", Parse("  k   =   value  ").Get("k"));
        }

        [Theory]
        [InlineData(" leading")]
        [InlineData("trailing ")]
        [InlineData("with \"quotes\"")]
        [InlineData("back\\slash")]
        [InlineData("two\nlines")]
        [InlineData("\ttabbed")]
        [InlineData("\"already quoted\"")]
        public void AwkwardValuesAreQuotedAndComeBackUnchanged(string value)
        {
            Assert.Equal(value, RoundTrip(value));
        }

        [Fact]
        public void CommentsBlankLinesAndUnknownKeysSurviveAWrite()
        {
            SettingsFile file = Parse("# the mod's settings", string.Empty, "future.thing = 7", "keys.ui.down = a");
            file.Set("keys.ui.down", "b");

            Assert.Equal(
                new List<string>
                {
                    "# the mod's settings",
                    string.Empty,
                    "future.thing = 7",
                    "keys.ui.down = b",
                },
                file.ToLines()
            );
        }

        [Fact]
        public void ACommentedOutSettingStaysCommentedOut()
        {
            SettingsFile file = Parse("# keys.ui.down = a");
            Assert.False(file.Has("keys.ui.down"));
            Assert.Empty(file.Keys);
        }

        [Fact]
        public void ANewKeyIsAppended()
        {
            SettingsFile file = Parse("a = 1");
            file.Set("b", "2");
            Assert.Equal(new List<string> { "a = 1", "b = 2" }, file.ToLines());
        }

        [Fact]
        public void TheLastLineForAKeyWins()
        {
            SettingsFile file = Parse("a = 1", "a = 2");
            Assert.Equal("2", file.Get("a"));

            file.Set("a", "3");
            Assert.Equal("3", SettingsFile.Parse(file.ToLines()).Get("a"));
        }

        [Fact]
        public void RemovingAKeyTakesEveryLineOfIt()
        {
            SettingsFile file = Parse("a = 1", "b = 2", "a = 3");
            file.Remove("a");

            Assert.Equal(new List<string> { "b = 2" }, file.ToLines());
            Assert.False(file.Has("a"));
        }

        [Fact]
        public void SettingNullRemovesTheKey()
        {
            SettingsFile file = Parse("a = 1");
            file.Set("a", null);
            Assert.False(file.Has("a"));
        }

        [Fact]
        public void KeysComeBackInTheOrderTheFileHoldsThem()
        {
            Assert.Equal(new List<string> { "b", "a" }, Parse("b = 1", "# note", "a = 2").Keys);
        }

        [Fact]
        public void AValueMayItselfContainAnEqualsSign()
        {
            Assert.Equal("x=y", Parse("a = x=y").Get("a"));
        }
    }
}

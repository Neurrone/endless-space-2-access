using System;
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

        /// <summary>A file with no header gets one, at the top, above everything it already said.
        /// </summary>
        [Fact]
        public void AHeaderGoesInAtTheTop()
        {
            SettingsFile file = Parse("a = 1", "b = 2");
            file.SetHeaderComment("United Empire, Autosave, turn 26");
            Assert.Equal(
                new List<string> { "#! United Empire, Autosave, turn 26", "a = 1", "b = 2" },
                file.ToLines()
            );
            Assert.Equal("United Empire, Autosave, turn 26", file.HeaderComment);
            // The lines moved down by one; the keys must still be findable.
            Assert.Equal("1", file.Get("a"));
            Assert.Equal("2", file.Get("b"));
        }

        /// <summary>Written again it is REPLACED, never stacked: the file is rewritten on every set
        /// and a file with a hundred headers would be the bug.</summary>
        [Fact]
        public void AHeaderIsReplacedAndNeverDuplicated()
        {
            SettingsFile file = Parse("a = 1");
            file.SetHeaderComment("turn 26");
            file.SetHeaderComment("turn 27");
            file.SetHeaderComment("turn 28");
            Assert.Equal(new List<string> { "#! turn 28", "a = 1" }, file.ToLines());
        }

        /// <summary>A comment the PLAYER wrote is not ours and is never clobbered - the mark is what
        /// tells them apart. Their line stays, and the header goes in above it.</summary>
        [Fact]
        public void APlayersOwnFirstLineCommentSurvives()
        {
            SettingsFile file = Parse("# my own note", "a = 1");
            file.SetHeaderComment("turn 26");
            Assert.Equal(
                new List<string> { "#! turn 26", "# my own note", "a = 1" },
                file.ToLines()
            );

            // And doing it twice replaces only ours.
            file.SetHeaderComment("turn 27");
            Assert.Equal(
                new List<string> { "#! turn 27", "# my own note", "a = 1" },
                file.ToLines()
            );
        }

        /// <summary>The mark only means the header on the FIRST line. Further down it is somebody
        /// else's line, and it is left exactly where it is.</summary>
        [Fact]
        public void TheMarkFurtherDownIsNotTheHeader()
        {
            SettingsFile file = Parse("a = 1", "#! not the header");
            Assert.Null(file.HeaderComment);
            file.SetHeaderComment("turn 26");
            Assert.Equal(
                new List<string> { "#! turn 26", "a = 1", "#! not the header" },
                file.ToLines()
            );
        }

        /// <summary>Header in, header out: nothing else in the file moves, and a file that never had
        /// one is unchanged by taking one away.</summary>
        [Fact]
        public void AHeaderCanBeTakenAwayAgain()
        {
            SettingsFile file = Parse("# my own note", "a = 1");
            file.SetHeaderComment("turn 26");
            file.SetHeaderComment(null);
            Assert.Equal(new List<string> { "# my own note", "a = 1" }, file.ToLines());
            Assert.Equal("1", file.Get("a"));

            file.SetHeaderComment("   ");
            Assert.Equal(new List<string> { "# my own note", "a = 1" }, file.ToLines());
        }

        /// <summary>A header survives a load/save round trip as the header, and everything under it
        /// survives with it.</summary>
        [Fact]
        public void AHeaderSurvivesARoundTrip()
        {
            SettingsFile written = new SettingsFile();
            written.Set("slot1", "0,1,2");
            written.SetHeaderComment("United Empire, Autosave, turn 26");

            SettingsFile read = SettingsFile.Parse(written.ToLines());
            Assert.Equal("United Empire, Autosave, turn 26", read.HeaderComment);
            Assert.Equal("0,1,2", read.Get("slot1"));

            read.SetHeaderComment("United Empire, Autosave, turn 27");
            Assert.Equal(
                new List<string> { "#! United Empire, Autosave, turn 27", "slot1 = 0,1,2" },
                read.ToLines()
            );
        }

        /// <summary>A line break in the text would end the comment and leave the rest of it as rubbish
        /// the parser would keep forever, so it is folded to a space.</summary>
        [Fact]
        public void AHeaderIsAlwaysOneLine()
        {
            SettingsFile file = new SettingsFile();
            file.SetHeaderComment("one\r\ntwo\nthree");

            IList<string> lines = file.ToLines();
            Assert.Single(lines);
            Assert.DoesNotContain('\r', lines[0]);
            Assert.DoesNotContain('\n', lines[0]);
            Assert.StartsWith("#! ", lines[0]);

            // Every word survives, in order: the break is what is folded away, not the text.
            Assert.Equal(
                new[] { "one", "two", "three" },
                lines[0].Substring(3).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            );
        }
    }
}

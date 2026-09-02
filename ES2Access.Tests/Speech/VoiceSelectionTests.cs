using System.Collections.Generic;
using System.Linq;
using ES2Access.Core.Speech.Mac;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The pure half of the Voice setting: how a chosen voice is stored (language and name,
    /// because macOS ships one name in a dozen languages), and how the picker's list is filtered
    /// to the game's language without ever going empty.
    /// </summary>
    public class VoiceSelectionTests
    {
        private static readonly List<VoiceInfo> Voices = new List<VoiceInfo>
        {
            new VoiceInfo("v0", "Samantha", "en-US"),
            new VoiceInfo("v1", "Daniel", "en-GB"),
            new VoiceInfo("v2", "Eddy", "en-US"),
            new VoiceInfo("v3", "Eddy", "fr-FR"),
            new VoiceInfo("v4", "Anna", "de-DE"),
            new VoiceInfo("v5", "Tingting", "zh_CN"),
            new VoiceInfo("v6", "Nameless", null),
        };

        [Theory]
        [InlineData("en-US", "en")]
        [InlineData("zh_CN", "zh")]
        [InlineData("EN", "en")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void PrimaryLanguageTakesSubtagBeforeSeparator(string tag, string expected)
        {
            Assert.Equal(expected, VoiceSelection.PrimaryLanguage(tag));
        }

        [Theory]
        [InlineData("english", "en")]
        [InlineData("English", "en")]
        [InlineData("schinese", "zh")]
        [InlineData("brazilian", "pt")]
        [InlineData("latam", "es")]
        [InlineData("klingon", "")]
        [InlineData(null, "")]
        public void GamePrimaryLanguageMapsTheGamesLanguageNames(string gameLanguage, string expected)
        {
            Assert.Equal(expected, VoiceSelection.GamePrimaryLanguage(gameLanguage));
        }

        [Fact]
        public void MakeKeyRoundTripsThroughFindByKey()
        {
            foreach (VoiceInfo voice in Voices)
            {
                Assert.Same(voice, VoiceSelection.FindByKey(Voices, VoiceSelection.MakeKey(voice)));
            }
        }

        [Fact]
        public void FindByKeyDistinguishesSameNameByLanguage()
        {
            Assert.Equal("v3", VoiceSelection.FindByKey(Voices, "fr-FR|Eddy").Identifier);
            Assert.Equal("v2", VoiceSelection.FindByKey(Voices, "en-US|Eddy").Identifier);
        }

        [Fact]
        public void FindByKeyEmptyLanguageTakesFirstOfThatName()
        {
            Assert.Equal("v2", VoiceSelection.FindByKey(Voices, "|Eddy").Identifier);
        }

        [Fact]
        public void FindByKeyReturnsNullForDefaultOrUnknown()
        {
            Assert.Null(VoiceSelection.FindByKey(Voices, VoiceSelection.DefaultKey));
            Assert.Null(VoiceSelection.FindByKey(Voices, "en-US|Nobody"));
            Assert.Null(VoiceSelection.FindByKey(Voices, "de-DE|Samantha"));
            Assert.Null(VoiceSelection.FindByKey(Voices, "Eddy"));
        }

        [Fact]
        public void ForLanguageMatchesByPrimarySubtag()
        {
            Assert.Equal(
                new[] { "v0", "v1", "v2" },
                VoiceSelection.ForLanguage(Voices, "en").Select(v => v.Identifier)
            );
            Assert.Equal(
                new[] { "v5" },
                VoiceSelection.ForLanguage(Voices, "zh").Select(v => v.Identifier)
            );
        }

        [Fact]
        public void ForLanguageFallsBackToEveryVoice()
        {
            Assert.Equal(Voices.Count, VoiceSelection.ForLanguage(Voices, "th").Count);
            Assert.Equal(Voices.Count, VoiceSelection.ForLanguage(Voices, "").Count);
        }

        [Fact]
        public void DisambiguateNumbersRepeatedNames()
        {
            List<VoiceInfo> voices = new List<VoiceInfo>
            {
                new VoiceInfo("a", "Foo", "en-US"),
                new VoiceInfo("b", "Foo", "en-US"),
                new VoiceInfo("c", "Foo", "en-GB"),
                new VoiceInfo("d", "Foo", "en-US"),
            };
            List<VoiceInfo> unique = VoiceSelection.Disambiguate(voices);
            Assert.Equal(new[] { "Foo", "Foo (2)", "Foo", "Foo (3)" }, unique.Select(v => v.Name));
            Assert.Equal(new[] { "a", "b", "c", "d" }, unique.Select(v => v.Identifier));
            Assert.Equal(4, unique.Select(VoiceSelection.MakeKey).Distinct().Count());
            Assert.Same(voices[0], unique[0]);
        }
    }
}

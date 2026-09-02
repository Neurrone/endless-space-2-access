using System.Collections.Generic;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// A mod-authored phrase a screen may have been written against before the build carries it. The
    /// load-bearing rule is the SILENCE: a screen that read "screen.error" at the player would be worse
    /// than one that said nothing, so an unknown key is null here rather than the key that
    /// <see cref="ModStrings.Get"/> deliberately answers with.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class OptionalTextTests
    {
        public OptionalTextTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void APhraseTheBuildDoesNotCarryIsSilent()
        {
            Assert.Null(OptionalText.Phrase("screen.no-such-page"));
            Assert.Null(OptionalText.Phrase("screen.no-such-page", 3));
            Assert.Null(OptionalText.Phrase(null));
            Assert.Null(OptionalText.Phrase(""));

            // And the key itself never reaches the player, which is the whole point.
            Assert.Equal("screen.no-such-page", ModStrings.Get("screen.no-such-page"));
        }

        [Fact]
        public void APhraseTheBuildCarriesSpeaks()
        {
            Install("screen.error", "Error", "cursor.mode-ended", "{0} mode ended");

            Assert.Equal("Error", OptionalText.Phrase("screen.error"));
            Assert.Equal("Probe mode ended", OptionalText.Phrase("cursor.mode-ended", "Probe"));
        }

        private static void Install(params string[] pairs)
        {
            Dictionary<string, string> strings = new Dictionary<string, string>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                strings[pairs[i]] = pairs[i + 1];
            }

            ModStrings.Install(strings);
        }
    }
}

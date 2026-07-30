using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The icon table is only worth having if every key in it is a key the mod can actually
    /// speak. A table entry pointing at a string nobody shipped is worse than no entry at all:
    /// the lookup succeeds, the warn-once tripwire stays quiet, and the player hears the key
    /// itself read out in the middle of a sentence.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class IconTableTests
    {
        [Fact]
        public void EveryKeyTheTableProducesHasAnEnglishDefault()
        {
            ModStrings.Reset();
            foreach (string key in IconTable.Keys)
            {
                string english;
                Assert.True(
                    ModStrings.TryGetDefault(key, out english),
                    "icon table: no compiled-in default for '" + key + "'"
                );
                Assert.False(string.IsNullOrEmpty(english), key + " has an empty name");
            }
        }

        [Fact]
        public void EveryKeyTheTableProducesIsInTheEnglishTemplate()
        {
            SortedSet<string> shipped = new SortedSet<string>();
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(EnglishTemplate())))
            {
                foreach (JsonProperty entry in document.RootElement.EnumerateObject())
                {
                    shipped.Add(entry.Name);
                }
            }

            foreach (string key in IconTable.Keys)
            {
                Assert.True(shipped.Contains(key), "english.json: missing icon key '" + key + "'");
            }
        }

        /// <summary>The variants of one concept must land on one key - that is the whole reason
        /// the table exists rather than a rule that reads the token's spelling.</summary>
        [Fact]
        public void ColourAndSizeVariantsShareOneKey()
        {
            Assert.Equal(ModStrings.IconDust, Token("dust"));
            Assert.Equal(ModStrings.IconDust, Token("dustColored"));
            Assert.Equal(ModStrings.IconDust, Picture("FIDSIDUST"));
            Assert.Equal(ModStrings.IconDust, Picture("FIDSIDUSTLARGE"));
        }

        /// <summary>Registered with the engine and deliberately nameless: the game's own file
        /// gives these no character at all, only a colour.</summary>
        [Fact]
        public void ColourDirectivesAreKnownAndHaveNoName()
        {
            Assert.Equal(string.Empty, Token("blue-gray"));
            Assert.Equal(string.Empty, Token("red"));
        }

        [Fact]
        public void AnUnregisteredTokenIsNotInTheTable()
        {
            string key;
            Assert.False(IconTable.TryKeyForToken("Beginner", out key));
        }

        private static string Token(string token)
        {
            string key;
            Assert.True(IconTable.TryKeyForToken(token, out key), token);
            return key;
        }

        private static string Picture(string asset)
        {
            string key;
            Assert.True(IconTable.TryKeyForPicture(asset, out key), asset);
            return key;
        }

        private static string EnglishTemplate()
        {
            DirectoryInfo directory = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(
                    directory.FullName,
                    "ES2Access",
                    "locale",
                    "english.json"
                );
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("no ES2Access\\locale\\english.json above the tests");
        }
    }
}

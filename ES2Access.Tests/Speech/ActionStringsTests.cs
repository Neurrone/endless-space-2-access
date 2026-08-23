using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The names of the mod's own keyboard actions are addressed by a COMPOSED key
    /// (<c>action.ui.down.title</c>), so the sweep over shipped constants in
    /// <see cref="LocaleFileTests"/> cannot see them. This is that sweep for this family: a phrase
    /// the mod speaks and the English template does not carry is a phrase no translator is offered.
    /// </summary>
    public class ActionStringsTests
    {
        [Fact]
        public void EveryActionNameIsInTheEnglishTemplate()
        {
            SortedSet<string> shipped = new SortedSet<string>();
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(TemplatePath())))
            {
                foreach (JsonProperty entry in document.RootElement.EnumerateObject())
                {
                    shipped.Add(entry.Name);
                }
            }

            foreach (string key in ModStrings.ActionStringKeys())
            {
                Assert.True(shipped.Contains(key), "english.json: missing key '" + key + "'");
            }
        }

        [Fact]
        public void EveryActionHasBothATitleAndADescription()
        {
            SortedSet<string> keys = new SortedSet<string>(ModStrings.ActionStringKeys());
            Assert.NotEmpty(keys);
            foreach (string key in keys)
            {
                string other = key.EndsWith(".title")
                    ? key.Substring(0, key.Length - ".title".Length) + ".description"
                    : key.Substring(0, key.Length - ".description".Length) + ".title";
                Assert.True(keys.Contains(other), "no partner for '" + key + "'");
            }
        }

        [Fact]
        public void TheKeysAreComposedFromTheActionName()
        {
            Assert.Equal("action.ui.down.title", ModStrings.ActionTitleKey("ui.down"));
            Assert.Equal(
                "action.galaxy.scanNext.description",
                ModStrings.ActionDescriptionKey("galaxy.scanNext")
            );
        }

        private static string TemplatePath()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
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

            throw new FileNotFoundException("no ES2Access\\locale\\english.json above " + AppContext.BaseDirectory);
        }
    }
}

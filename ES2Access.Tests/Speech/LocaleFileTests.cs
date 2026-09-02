using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// Validates the shipped translation files against the compiled-in English strings. A
    /// community translation with a stray key or a mangled placeholder is caught here, at build
    /// time, instead of by a player hearing "5 of {1}".
    /// </summary>
    public class LocaleFileTests
    {
        private static readonly Regex Placeholder = new Regex(@"\{(\d+)(?:[,:][^}]*)?\}");

        public static IEnumerable<object[]> LocaleFiles()
        {
            foreach (string file in Directory.GetFiles(LocaleDirectory(), "*.json"))
            {
                yield return new object[] { Path.GetFileName(file) };
            }
        }

        [Fact]
        public void AtLeastOneLocaleFileIsShipped()
        {
            Assert.NotEmpty(Directory.GetFiles(LocaleDirectory(), "*.json"));
        }

        [Theory]
        [MemberData(nameof(LocaleFiles))]
        public void EveryKeyIsKnownAndKeepsTheEnglishPlaceholders(string fileName)
        {
            string path = Path.Combine(LocaleDirectory(), fileName);
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
                foreach (JsonProperty entry in document.RootElement.EnumerateObject())
                {
                    // A language with more than two number forms carries the extra ones under
                    // "<many key>.few" and "<many key>.one" (ModStrings.Plural); each is
                    // checked against the English of the key it hangs off, since that is the
                    // sentence it was written from.
                    string key = BaseKey(entry.Name);

                    string english;
                    Assert.True(
                        ModStrings.TryGetDefault(key, out english),
                        fileName + ": unknown key '" + entry.Name + "'"
                    );
                    Assert.Equal(JsonValueKind.String, entry.Value.ValueKind);
                    Assert.Equal(
                        Placeholders(english),
                        Placeholders(entry.Value.GetString())
                    );
                }
            }
        }

        /// <summary>
        /// english.json is the template every other translation is written from, so a key the mod
        /// speaks and the template does not is a phrase no translator will ever be offered.
        /// </summary>
        [Fact]
        public void TheEnglishTemplateCarriesEveryKeyTheModSpeaks()
        {
            SortedSet<string> shipped = new SortedSet<string>();
            using (
                JsonDocument document = JsonDocument.Parse(
                    File.ReadAllText(Path.Combine(LocaleDirectory(), "english.json"))
                )
            )
            {
                foreach (JsonProperty entry in document.RootElement.EnumerateObject())
                {
                    shipped.Add(entry.Name);
                }
            }

            foreach (FieldInfo field in typeof(ModStrings).GetFields(
                BindingFlags.Public | BindingFlags.Static
            ))
            {
                if (!field.IsLiteral || field.FieldType != typeof(string))
                {
                    continue;
                }

                string key = (string)field.GetRawConstantValue();
                Assert.True(
                    shipped.Contains(key),
                    "english.json: missing key '" + key + "' (ModStrings." + field.Name + ")"
                );
            }
        }

        // The key an extra counted form hangs off, or the key itself.
        private static string BaseKey(string key)
        {
            foreach (string suffix in new[] { PluralRules.FewSuffix, PluralRules.OneSuffix })
            {
                if (key.Length > suffix.Length && key.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return key.Substring(0, key.Length - suffix.Length);
                }
            }

            return key;
        }

        private static SortedSet<string> Placeholders(string template)
        {
            SortedSet<string> indexes = new SortedSet<string>();
            foreach (Match match in Placeholder.Matches(template))
            {
                indexes.Add(match.Groups[1].Value);
            }

            return indexes;
        }

        // The tests run from bin/, so walk up to the repository and find the sources' locale
        // folder rather than depending on anything being copied next to the test assembly.
        private static string LocaleDirectory()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "ES2Access", "locale");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "no ES2Access\\locale folder above " + AppContext.BaseDirectory
            );
        }
    }
}

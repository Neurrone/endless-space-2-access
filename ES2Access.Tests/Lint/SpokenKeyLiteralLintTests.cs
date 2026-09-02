using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// A <c>ModStrings</c> key is spelled ONCE, in the constant that declares it. A screen that writes
    /// the dotted key out again has made a second declaration the compiler cannot see: rename the
    /// constant or re-spell the key in <c>english.json</c> and the constant moves while the literal
    /// does not, and because these keys are asked for through <c>OptionalText</c> the mismatch is not
    /// an error but a line that quietly stops being said.
    ///
    /// The rule is every literal that is a key of <c>english.json</c> and carries a family dot -
    /// <c>"screen.battle"</c>, <c>"battle.your-fleets"</c>. The two dotless keys (<c>none</c>,
    /// <c>zoom</c>) are ordinary English words that dev output and enum switches spell for their own
    /// reasons, and flagging those would be flagging the word rather than the key.
    ///
    /// There is no allowlist. A screen has no reason to name a key it could name the constant of, and
    /// the constants are all <c>public</c>.
    /// </summary>
    public class SpokenKeyLiteralLintTests
    {
        private const string Rule =
            "A ModStrings key belongs in exactly one place: the constant that declares it."
            + " Spelling the dotted key in a screen makes a second declaration nothing keeps in step,"
            + " and a drifted key reaches the player as a line that is simply never said."
            + " Use the ModStrings constant instead.";

        private static readonly Regex Literal = new Regex("\"([^\"\\\\]*)\"");

        private static readonly Regex Declaration = new Regex(
            "const\\s+string\\s+(\\w+)\\s*=\\s*\"([^\"]*)\""
        );

        [Fact]
        public void NoSourceSpellsAKeyThatModStringsAlreadyDeclares()
        {
            Dictionary<string, string> constants = Constants();
            List<string> offenders = new List<string>();
            foreach (string file in LintSources.ModSources())
            {
                if (IsModStrings(file) || IsGenerated(file))
                {
                    continue;
                }

                string[] lines = LintSources.Lines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (LintSources.IsComment(lines[i]))
                    {
                        continue;
                    }

                    foreach (Match match in Literal.Matches(lines[i]))
                    {
                        string key = match.Groups[1].Value;
                        string constant;
                        if (!constants.TryGetValue(key, out constant))
                        {
                            continue;
                        }

                        offenders.Add(
                            file
                                + ":"
                                + (i + 1)
                                + ": \""
                                + key
                                + "\"   ->   ModStrings."
                                + constant
                        );
                    }
                }
            }

            offenders.Sort(StringComparer.Ordinal);
            Assert.True(
                offenders.Count == 0,
                Rule
                    + Environment.NewLine
                    + Environment.NewLine
                    + string.Join(Environment.NewLine + "  ", offenders.ToArray())
            );
        }

        /// <summary>The lint is only as good as the set it compares against, so an empty or unparsed
        /// english.json must fail here rather than passing everything above it.</summary>
        [Fact]
        public void EveryDottedKeyOfTheTemplateHasAConstantToPointAt()
        {
            Assert.NotEmpty(Constants());
        }

        /// <summary>Every dotted key of <c>english.json</c> that a constant declares, mapped to that
        /// constant's name. A key with no constant - the action and colour families, whose keys are
        /// composed at run time - is not something a literal could be replaced by, so it is left
        /// out.</summary>
        private static Dictionary<string, string> Constants()
        {
            Dictionary<string, string> declared = new Dictionary<string, string>(
                StringComparer.Ordinal
            );
            foreach (
                string file in Directory.GetFiles(
                    Path.Combine(LintSources.RepoRoot(), "ES2Access", "Core", "Speech"),
                    "ModStrings*.cs"
                )
            )
            {
                foreach (Match match in Declaration.Matches(File.ReadAllText(file)))
                {
                    declared[match.Groups[2].Value] = match.Groups[1].Value;
                }
            }

            Dictionary<string, string> keys = new Dictionary<string, string>(StringComparer.Ordinal);
            using (
                JsonDocument document = JsonDocument.Parse(
                    File.ReadAllText(
                        Path.Combine(
                            LintSources.RepoRoot(),
                            "ES2Access",
                            "locale",
                            "english.json"
                        )
                    )
                )
            )
            {
                foreach (JsonProperty entry in document.RootElement.EnumerateObject())
                {
                    string constant;
                    if (entry.Name.Contains(".") && declared.TryGetValue(entry.Name, out constant))
                    {
                        keys[entry.Name] = constant;
                    }
                }
            }

            return keys;
        }

        private static bool IsModStrings(string file)
        {
            return file.StartsWith("ES2Access/Core/Speech/ModStrings", StringComparison.Ordinal);
        }

        private static bool IsGenerated(string file)
        {
            return file.StartsWith("ES2Access/obj/", StringComparison.Ordinal)
                || file.StartsWith("ES2Access/bin/", StringComparison.Ordinal);
        }
    }
}

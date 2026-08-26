using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests
{
    /// <summary>
    /// docs/generic/src is a MIRROR of the mod's engine-side originals, and the generic docs are
    /// this repository's primary deliverable — a snapshot that has silently drifted teaches the
    /// next game's mod an API shape that no longer exists. sync-manifest.txt is the single source
    /// of truth for what mirrors what; both these tests and sync-generic-src.ps1 read it.
    ///
    /// The localization pair (src/localization/ModStrings.cs + english.json) is deliberately NOT
    /// mirrored: it is a trimmed example whose keys are the floor the other snapshots compile
    /// against. That floor is what the second half of this file checks.
    /// </summary>
    public class GenericSrcMirrorTests
    {
        private static readonly Regex ManifestLine = new Regex(@"^([^|]+)\|(.+)$");

        private static readonly Regex ModStringsUse = new Regex(@"\bModStrings\.(\w+)");

        private static readonly Regex PublicMember = new Regex(
            @"public\s+(?:const|static(?:\s+readonly)?)\s+[\w<>,\[\]\.]+\s+(\w+)\s*[=(]"
        );

        private static readonly Regex KeyConstant = new Regex(
            @"public\s+const\s+string\s+\w+\s*=\s*""([^""]+)"""
        );

        public static IEnumerable<object[]> Mirrors()
        {
            foreach (KeyValuePair<string, string> entry in Manifest())
            {
                yield return new object[] { entry.Key, entry.Value };
            }
        }

        [Fact]
        public void TheManifestListsEveryMirroredSnapshot()
        {
            Assert.NotEmpty(Manifest());
        }

        /// <summary>
        /// Re-sync with <c>.\sync-generic-src.ps1</c> (or check without writing with
        /// <c>-Check</c>) when this fails.
        /// </summary>
        [Theory]
        [MemberData(nameof(Mirrors))]
        public void TheSnapshotMatchesItsOriginByteForByte(string snapshot, string origin)
        {
            string snapshotPath = Path.Combine(RepoRoot(), snapshot);
            string originPath = Path.Combine(RepoRoot(), origin);

            Assert.True(File.Exists(snapshotPath), "missing snapshot: " + snapshot);
            Assert.True(File.Exists(originPath), "missing origin: " + origin);
            Assert.True(
                ByteEqual(snapshotPath, originPath),
                snapshot
                    + " has drifted from "
                    + origin
                    + "; run .\\sync-generic-src.ps1 to refresh the mirror"
            );
        }

        /// <summary>
        /// The example ModStrings is the floor the other snapshots compile against: a snapshot that
        /// speaks a key the example does not declare would not build when copied into a new game's
        /// mod, which is exactly the promise the docs make about this folder.
        /// </summary>
        [Fact]
        public void TheExampleModStringsDeclaresEveryMemberTheSnapshotsUse()
        {
            string examplePath = Path.Combine(
                RepoRoot(),
                "docs",
                "generic",
                "src",
                "localization",
                "ModStrings.cs"
            );
            string example = File.ReadAllText(examplePath);

            SortedSet<string> declared = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match match in PublicMember.Matches(example))
            {
                declared.Add(match.Groups[1].Value);
            }

            foreach (
                string file in Directory.GetFiles(
                    Path.Combine(RepoRoot(), "docs", "generic", "src"),
                    "*.cs",
                    SearchOption.AllDirectories
                )
            )
            {
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(examplePath)))
                {
                    continue;
                }

                foreach (Match match in ModStringsUse.Matches(File.ReadAllText(file)))
                {
                    string member = match.Groups[1].Value;
                    Assert.True(
                        declared.Contains(member),
                        Path.GetFileName(file)
                            + " uses ModStrings."
                            + member
                            + ", which the example src/localization/ModStrings.cs does not declare"
                    );
                }
            }
        }

        /// <summary>
        /// The example english.json is the template a new mod starts its own translations from, so
        /// a key the example table reads and the example template omits is a phrase that would
        /// degrade to its raw key on the very first run.
        /// </summary>
        [Fact]
        public void TheExampleTemplateCarriesEveryKeyTheExampleTableReads()
        {
            string localization = Path.Combine(
                RepoRoot(),
                "docs",
                "generic",
                "src",
                "localization"
            );
            string example = File.ReadAllText(Path.Combine(localization, "ModStrings.cs"));

            SortedSet<string> shipped = new SortedSet<string>(StringComparer.Ordinal);
            using (
                JsonDocument document = JsonDocument.Parse(
                    File.ReadAllText(Path.Combine(localization, "english.json"))
                )
            )
            {
                Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
                foreach (JsonProperty entry in document.RootElement.EnumerateObject())
                {
                    shipped.Add(entry.Name);
                }
            }

            foreach (Match match in KeyConstant.Matches(example))
            {
                string key = match.Groups[1].Value;
                Assert.True(
                    shipped.Contains(key),
                    "src/localization/english.json: missing key '" + key + "'"
                );
            }
        }

        private static bool ByteEqual(string left, string right)
        {
            byte[] a = File.ReadAllBytes(left);
            byte[] b = File.ReadAllBytes(right);
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static SortedDictionary<string, string> Manifest()
        {
            SortedDictionary<string, string> mappings = new SortedDictionary<string, string>(
                StringComparer.Ordinal
            );
            string path = Path.Combine(
                RepoRoot(),
                "docs",
                "generic",
                "src",
                "sync-manifest.txt"
            );

            foreach (string line in File.ReadAllLines(path))
            {
                string text = line.Trim();
                if (text.Length == 0 || text.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                Match match = ManifestLine.Match(text);
                Assert.True(match.Success, "malformed sync-manifest.txt line: " + text);
                mappings[match.Groups[1].Value.Trim()] = match.Groups[2].Value.Trim();
            }

            return mappings;
        }

        // The tests run from bin/, so walk up to the repository rather than depending on anything
        // being copied next to the test assembly.
        private static string RepoRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (
                    File.Exists(
                        Path.Combine(
                            directory.FullName,
                            "docs",
                            "generic",
                            "src",
                            "sync-manifest.txt"
                        )
                    )
                )
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "no docs\\generic\\src\\sync-manifest.txt above " + AppContext.BaseDirectory
            );
        }
    }
}

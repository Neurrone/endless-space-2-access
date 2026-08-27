using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// The machinery the three source lints share: find the mod's sources on disk, and compare what
    /// they contain against a checked-in allowlist.
    ///
    /// A lint site is identified by its FILE plus the trimmed text of the line it sits on, never by a
    /// line number - a number drifts the moment anything above it is edited, and an allowlist that
    /// churns on every unrelated edit stops being read. Identical lines in one file (the same
    /// <c>if (widget == null || !AgeWidgets.Visible(widget))</c> guarding four different walks) are
    /// counted rather than listed twice, so the count is the thing that has to move when a fifth
    /// appears.
    ///
    /// The gate is one-directional: a site the allowlist does not cover fails, a covered site that has
    /// since been deleted does not. Stale entries are harmless and worth pruning when noticed; a new
    /// site is the thing that must not slip in unremarked.
    ///
    /// Mechanical half of the remedy, once the why-comment is written:
    /// <c>ES2ACCESS_LINT_REGENERATE=1 dotnet test</c> rewrites the allowlists in place. The entry then
    /// shows up in the diff, which is the whole point of the file.
    /// </summary>
    public static class LintSources
    {
        /// <summary>Set this in the environment to rewrite every allowlist from the current tree.</summary>
        public const string RegenerateVariable = "ES2ACCESS_LINT_REGENERATE";

        /// <summary>Every <c>.cs</c> file under <c>ES2Access/</c>, as repository-relative paths with
        /// forward slashes, sorted so a regenerated allowlist has a stable order.</summary>
        public static IList<string> ModSources()
        {
            string root = Path.Combine(RepoRoot(), "ES2Access");
            List<string> relative = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                relative.Add(
                    "ES2Access/"
                        + file.Substring(root.Length + 1).Replace(Path.DirectorySeparatorChar, '/')
                );
            }

            relative.Sort(StringComparer.Ordinal);
            Assert.NotEmpty(relative);
            return relative;
        }

        public static string[] Lines(string relativePath)
        {
            return File.ReadAllLines(
                Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar))
            );
        }

        /// <summary>A line that carries no code: a <c>//</c> comment, a doc comment, or the
        /// continuation of a block comment. Prose about a widget's <c>.Visible</c> is not a test of
        /// it.</summary>
        public static bool IsComment(string line)
        {
            string text = line.Trim();
            return text.StartsWith("//", StringComparison.Ordinal)
                || text.StartsWith("*", StringComparison.Ordinal)
                || text.StartsWith("/*", StringComparison.Ordinal);
        }

        /// <summary>Reads an allowlist. Blank lines and <c>#</c> lines are ignored; every other line is
        /// <c>path | count | trimmed source line</c>, and the source text may itself contain <c>|</c>
        /// (<c>||</c> is everywhere in these conditions), so only the first two separators split.</summary>
        public static Dictionary<Site, int> Allowed(string allowlist)
        {
            Dictionary<Site, int> counts = new Dictionary<Site, int>();
            string path = AllowlistPath(allowlist);
            if (!File.Exists(path))
            {
                return counts;
            }

            foreach (string line in File.ReadAllLines(path))
            {
                string text = line.Trim();
                if (text.Length == 0 || text.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int file = text.IndexOf('|');
                Assert.True(file > 0, allowlist + ": malformed entry: " + text);
                int count = text.IndexOf('|', file + 1);
                Assert.True(count > file, allowlist + ": malformed entry: " + text);

                Site site = new Site(
                    text.Substring(0, file).Trim(),
                    text.Substring(count + 1).Trim()
                );
                counts[site] = int.Parse(text.Substring(file + 1, count - file - 1).Trim());
            }

            return counts;
        }

        /// <summary>
        /// The one assertion all three lints end in: every site found in the tree is covered by the
        /// allowlist, at least as many times as it occurs.
        /// </summary>
        public static void AssertAllowed(
            string allowlist,
            IDictionary<Site, int> found,
            string rule
        )
        {
            if (
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RegenerateVariable))
            )
            {
                Write(allowlist, found, rule);
            }

            Dictionary<Site, int> allowed = Allowed(allowlist);
            List<string> unlisted = new List<string>();
            foreach (KeyValuePair<Site, int> entry in found)
            {
                int budget;
                if (!allowed.TryGetValue(entry.Key, out budget) || budget < entry.Value)
                {
                    unlisted.Add(
                        entry.Key.File
                            + ": "
                            + entry.Key.Text
                            + (entry.Value > 1 ? "   (x" + entry.Value + ")" : string.Empty)
                    );
                }
            }

            unlisted.Sort(StringComparer.Ordinal);
            Assert.True(
                unlisted.Count == 0,
                rule
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Not in ES2Access.Tests/Lint/"
                    + allowlist
                    + ":"
                    + Environment.NewLine
                    + "  "
                    + string.Join(Environment.NewLine + "  ", unlisted.ToArray())
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Once the site carries its why-comment, re-run with "
                    + RegenerateVariable
                    + "=1 to record it."
            );
        }

        private static void Write(string allowlist, IDictionary<Site, int> found, string rule)
        {
            List<Site> sites = new List<Site>(found.Keys);
            sites.Sort(
                delegate(Site left, Site right)
                {
                    int file = StringComparer.Ordinal.Compare(left.File, right.File);
                    return file != 0
                        ? file
                        : StringComparer.Ordinal.Compare(left.Text, right.Text);
                }
            );

            List<string> lines = new List<string>();
            foreach (string sentence in rule.Split('\n'))
            {
                lines.Add("# " + sentence.Trim());
            }

            lines.Add("#");
            lines.Add("# Generated by " + RegenerateVariable + "=1; format: path | count | source line.");
            lines.Add("#");
            string previous = null;
            foreach (Site site in sites)
            {
                if (site.File != previous)
                {
                    lines.Add(string.Empty);
                    previous = site.File;
                }

                lines.Add(site.File + " | " + found[site] + " | " + site.Text);
            }

            File.WriteAllLines(AllowlistPath(allowlist), lines.ToArray());
        }

        private static string AllowlistPath(string allowlist)
        {
            return Path.Combine(RepoRoot(), "ES2Access.Tests", "Lint", allowlist);
        }

        // The tests run from bin/, so walk up to the repository rather than depending on anything
        // being copied next to the test assembly.
        public static string RepoRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (
                    File.Exists(
                        Path.Combine(directory.FullName, "ES2Access", "ES2Access.csproj")
                    )
                )
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "no ES2Access\\ES2Access.csproj above " + AppContext.BaseDirectory
            );
        }
    }

    /// <summary>A lint site: the file it is in, and the trimmed text of its line.</summary>
    public struct Site : IEquatable<Site>
    {
        public readonly string File;

        public readonly string Text;

        public Site(string file, string text)
        {
            File = file;
            Text = text;
        }

        public bool Equals(Site other)
        {
            return string.Equals(File, other.File, StringComparison.Ordinal)
                && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is Site && Equals((Site)obj);
        }

        public override int GetHashCode()
        {
            return File.GetHashCode() ^ Text.GetHashCode();
        }
    }
}

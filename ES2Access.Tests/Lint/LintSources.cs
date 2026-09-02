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
    /// The gate runs BOTH WAYS. A site the allowlist does not cover fails, and so does an allowlist
    /// entry no site answers to any more - because a stale entry is not the harmless leftover it looks
    /// like: it is a standing pre-authorisation, and the first line of code that happens to match its
    /// text again is admitted without anybody deciding. That is not hypothetical. A fossil in
    /// <c>synthetic-nodes.allow</c> outlived the walk it was written for and silently vouched for a
    /// misdeclared <c>scan:system/name</c>, which reached the player as a logged warning nobody had
    /// approved. The count is part of the entry, so an entry that allows more occurrences than the
    /// tree contains is the same fossil in miniature and fails the same way.
    ///
    /// Mechanical half of the remedy, once the why-comment is written:
    /// <c>ES2ACCESS_LINT_REGENERATE=1 dotnet test</c> rewrites the allowlists in place. The entry then
    /// shows up in the diff, which is the whole point of the file.
    /// </summary>
    public static class LintSources
    {
        /// <summary>Set this in the environment to rewrite every allowlist from the current tree.</summary>
        public const string RegenerateVariable = "ES2ACCESS_LINT_REGENERATE";

        /// <summary>Every <c>.cs</c> file under <c>ES2Access/</c> that a person wrote, as
        /// repository-relative paths with forward slashes, sorted so a regenerated allowlist has a
        /// stable order.
        ///
        /// <c>obj/</c> and <c>bin/</c> are excluded, and that is a correctness rule rather than
        /// tidiness: the generated <c>obj/Debug/ES2Access.AssemblyInfo.cs</c> exists only once the
        /// plugin has been built, so leaving it in makes what the lints see depend on whether
        /// somebody ran a build - the one thing an allowlist gate must never do.</summary>
        public static IList<string> ModSources()
        {
            string root = Path.Combine(RepoRoot(), "ES2Access");
            List<string> relative = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string path =
                    "ES2Access/"
                    + file.Substring(root.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                if (path.Contains("/obj/") || path.Contains("/bin/"))
                {
                    continue;
                }

                relative.Add(path);
            }

            relative.Sort(StringComparer.Ordinal);
            Assert.NotEmpty(relative);
            return relative;
        }

        // Every lint fact sweeps every source, so the whole tree would otherwise be read off disk
        // once per fact. The tree does not change while a test run is in flight.
        private static readonly Dictionary<string, string[]> LineCache =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        public static string[] Lines(string relativePath)
        {
            lock (LineCache)
            {
                string[] lines;
                if (!LineCache.TryGetValue(relativePath, out lines))
                {
                    lines = File.ReadAllLines(
                        Path.Combine(
                            RepoRoot(),
                            relativePath.Replace('/', Path.DirectorySeparatorChar)
                        )
                    );
                    LineCache[relativePath] = lines;
                }

                return lines;
            }
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
        /// The one assertion all the source lints end in, and it is a set EQUALITY rather than a
        /// containment: every site found in the tree is covered by the allowlist at least as many
        /// times as it occurs, and every entry of the allowlist answers to a site that is still there.
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

            // The other direction. An entry standing over nothing is a pre-authorisation waiting for
            // the next line of code to match its text, and one allowing more occurrences than the tree
            // has is the same thing for the next copy of a line that is already there.
            List<string> orphaned = new List<string>();
            foreach (KeyValuePair<Site, int> entry in allowed)
            {
                int occurrences;
                if (!found.TryGetValue(entry.Key, out occurrences))
                {
                    orphaned.Add(entry.Key.File + ": " + entry.Key.Text);
                }
                else if (entry.Value > occurrences)
                {
                    orphaned.Add(
                        entry.Key.File
                            + ": "
                            + entry.Key.Text
                            + "   (allows "
                            + entry.Value
                            + ", tree has "
                            + occurrences
                            + ")"
                    );
                }
            }

            orphaned.Sort(StringComparer.Ordinal);
            Assert.True(
                orphaned.Count == 0,
                "An allowlist entry no source site answers to is a standing pre-authorisation - the next line that matches its text is admitted without anybody deciding. Prune it."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Stale in ES2Access.Tests/Lint/"
                    + allowlist
                    + ":"
                    + Environment.NewLine
                    + "  "
                    + string.Join(Environment.NewLine + "  ", orphaned.ToArray())
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Re-run with "
                    + RegenerateVariable
                    + "=1 to rewrite the file from the tree."
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

        public static string RepoRoot()
        {
            return TestPaths.RepoRoot();
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

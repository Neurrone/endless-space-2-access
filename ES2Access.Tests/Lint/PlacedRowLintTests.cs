using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// THE CLASS-CLOSER for the row registry: every key segment the galaxy tree builds must be either
    /// DECLARED in <see cref="PlacedRows"/> or listed here as one the map carries on an ancestor.
    ///
    /// The bug this ends is not a crash. A new kind of row gets a key, a position and a place in
    /// whichever inventory the author happened to be working in, and is missing from the other three -
    /// so it arms the inspect cell but Enter on it does nothing, or Backspace remembers a leap from it
    /// and the restore will not put the player back there. That was true of point bookmarks, probes,
    /// obliterator missiles and ally pins at once, and it was found by a player rather than by a test.
    ///
    /// So a segment that is neither declared nor allowlisted fails the build, and the failure names
    /// the four questions somebody now has to answer for it: does it ARM the cell, may ENTER land on
    /// it and in what order, is a LEAP from it worth keeping, and may the player be RESTORED to it.
    /// Answering "none of them, it hangs under a star" is the allowlist entry.
    /// </summary>
    public class PlacedRowLintTests
    {
        private const string Allowlist = "placed-rows.allow";

        private const string Rule =
            "Every structural key segment the galaxy tree builds must be DECLARED in"
            + " Core/UI/PlacedRows.cs (a row that stands somewhere, or a grouping that refuses) or"
            + " allowlisted here as one the map carries on an ancestor. Deciding means answering four"
            + " questions: arms the inspect cell, enterable (and at which tier), leap-recordable,"
            + " restore-candidate. See Core/UI/PlacedRows.cs.";

        /// <summary>A key literal in the tree-building code: the segment is the word before a
        /// <c>/</c>-and-value, which is how every id in this page is composed - <c>key + "/fleet/" +
        /// guid</c>, <c>"galaxy:probe/" + guid</c>, <c>place + "/launch/0"</c>.</summary>
        private static readonly Regex Segment = new Regex(
            "\"[^\"]*?/(?<name>[a-z][a-z-]*)/",
            RegexOptions.Compiled
        );

        /// <summary>And the two head forms, where the segment follows the stop's own name directly.
        /// </summary>
        private static readonly Regex Head = new Regex(
            "\"galaxy:(?<name>[a-z][a-z-]*)/",
            RegexOptions.Compiled
        );

        [Fact]
        public void EveryKeySegmentIsDeclaredOrCarried()
        {
            Dictionary<Site, int> found = new Dictionary<Site, int>();
            foreach (string file in LintSources.ModSources())
            {
                if (!Builds(file))
                {
                    continue;
                }

                foreach (string line in LintSources.Lines(file))
                {
                    if (LintSources.IsComment(line))
                    {
                        continue;
                    }

                    foreach (string name in Segments(line))
                    {
                        if (PlacedRows.Named(name) != null)
                        {
                            continue;
                        }

                        Site site = new Site(file, name);
                        int count;
                        found[site] = found.TryGetValue(site, out count) ? count + 1 : 1;
                    }
                }
            }

            LintSources.AssertAllowed(Allowlist, found, Rule);
        }

        [Fact]
        public void TheAllowlistIsNotEmpty()
        {
            Assert.NotEmpty(LintSources.Allowed(Allowlist));
        }

        /// <summary>The lint would be worthless if its own extraction found nothing: the declared
        /// segments must be reachable from the sources it reads.</summary>
        [Fact]
        public void TheExtractionFindsDeclaredSegmentsToo()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (string file in LintSources.ModSources())
            {
                if (!Builds(file))
                {
                    continue;
                }

                foreach (string line in LintSources.Lines(file))
                {
                    if (!LintSources.IsComment(line))
                    {
                        foreach (string name in Segments(line))
                        {
                            seen.Add(name);
                        }
                    }
                }
            }

            Assert.Contains("fleet", seen);
            Assert.Contains("bookmark", seen);
            Assert.Contains("probe", seen);
        }

        private static IEnumerable<string> Segments(string line)
        {
            List<string> names = new List<string>();
            foreach (Match match in Segment.Matches(line))
            {
                names.Add(match.Groups["name"].Value);
            }

            foreach (Match match in Head.Matches(line))
            {
                names.Add(match.Groups["name"].Value);
            }

            return names;
        }

        /// <summary>The files that BUILD the galaxy tree's keys. The registry itself declares rather
        /// than builds, and lives in Core rather than under Screens/Galaxy, so it is out of this scan
        /// already.</summary>
        private static bool Builds(string file)
        {
            return file.StartsWith("ES2Access/Screens/Galaxy", StringComparison.Ordinal);
        }
    }
}

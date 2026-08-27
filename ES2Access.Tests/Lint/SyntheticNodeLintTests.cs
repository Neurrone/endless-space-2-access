using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// A <c>DrawnNode</c> hands the gate the widget that vouches for it; a <c>SyntheticNode</c> hands
    /// it nothing, and passes untested by construction. That is honest for exactly two kinds of
    /// content - something synthesized from the game's facts, and UI this mod drew itself - and it is
    /// how a row silently stops being checked for everything else. <c>Nodes.Synthetic</c> catches the
    /// one case a machine can catch (a node whose subject IS a widget), but a walk that enumerated a
    /// pooled row and then declared it synthetic keyed on the model behind it is invisible to that
    /// check: the existence claim moved to the walk, and the walk is where it now has to be argued.
    ///
    /// So every declaration is listed here, and the site says what vouches for the node - which walk
    /// found it, or which drawn test above it already asked. Adding one is a deliberate, diff-visible
    /// act.
    ///
    /// <c>Core/</c> declares synthetic nodes too and is exempt: it knows nothing of widgets, cannot
    /// run the gate's test, and its rows are guaranteed by the walk that enumerated them.
    /// </summary>
    public class SyntheticNodeLintTests
    {
        private const string Allowlist = "synthetic-nodes.allow";

        private const string Rule =
            "Synthetic asserts existence beyond the renderer - justify it: why-comment at the site, entry here; if a widget vouches for it, it is Drawn.\n"
            + "The comment names what stands behind the node: the game fact it was synthesized from, the mod UI it belongs to, or the drawn test above it that already asked whether the thing is there.";

        private static readonly Regex Declaration = new Regex(
            @"(Nodes\.Synthetic\s*\(|new\s+SyntheticNode\s*\()"
        );

        [Fact]
        public void EverySyntheticNodeDeclarationIsOnTheAllowlist()
        {
            LintSources.AssertAllowed(Allowlist, Sites(), Rule);
        }

        [Fact]
        public void TheAllowlistIsNotEmpty()
        {
            Assert.NotEmpty(LintSources.Allowed(Allowlist));
        }

        internal static Dictionary<Site, int> Sites()
        {
            Dictionary<Site, int> found = new Dictionary<Site, int>();
            foreach (string file in LintSources.ModSources())
            {
                if (Skipped(file))
                {
                    continue;
                }

                foreach (string line in LintSources.Lines(file))
                {
                    if (LintSources.IsComment(line) || !Declaration.IsMatch(line))
                    {
                        continue;
                    }

                    Site site = new Site(file, line.Trim());
                    int count;
                    found[site] = found.TryGetValue(site, out count) ? count + 1 : 1;
                }
            }

            return found;
        }

        private static bool Skipped(string file)
        {
            return file.StartsWith("ES2Access/Core/", StringComparison.Ordinal)
                || file.StartsWith("ES2Access/Dev/", StringComparison.Ordinal)
                // The factory itself - this is the line every allowlisted site goes through.
                || string.Equals(file, "ES2Access/UI/Nodes.cs", StringComparison.Ordinal);
        }
    }
}

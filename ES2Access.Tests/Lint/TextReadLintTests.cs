using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// Drawn text is read through the blessed readers - <c>AgeWidgets.PaintedText</c>,
    /// <c>AgeWidgets.PaintedLines</c> and <c>AgeWidgets.ItemText</c> for what a widget's subtree is
    /// actually painting, and <c>AgeText</c> for turning one raw AGE string into something worth
    /// saying. A screen that reaches past them for a label's <c>Text</c> gets the string that was
    /// ASSIGNED: a <c>%key</c> for a data-driven caption, colour markup and private-use glyphs for
    /// everything else, and no test at all of whether the label is one the game is still painting.
    ///
    /// The rule is bounded to the two raw shapes that actually occur in this codebase - a
    /// label-shaped expression's <c>.Text</c>/<c>.TranslatedText</c>, and a tooltip-shaped one's
    /// <c>.Content</c> - plus the text-field read, which nothing currently does and which is named
    /// here so the first one to do it is stopped. A lint that flags nothing real is noise; one that
    /// flags half the engine layer is worse.
    ///
    /// <c>UI/AgeText.cs</c> is exempt wholesale: reading the raw string is its entire job. The blessed
    /// readers live in <c>UI/AgeWidgets.cs</c> alongside code that is not a reader, so that file goes
    /// through the allowlist like any other.
    /// </summary>
    public class TextReadLintTests
    {
        private const string Allowlist = "raw-text-reads.allow";

        private const string Rule =
            "Drawn text is read through PaintedText / PaintedLines / ItemText (UI/AgeWidgets.cs), which ask what the subtree is painting, and through AgeText, which resolves keys, icon tokens and colour markup.\n"
            + "A raw .Text / .TranslatedText / .Content read bypasses both: it returns the assigned string, ellipsis-free, markup and all, from a label nothing has vouched for.\n"
            + "If the raw string really is what is wanted, say why at the site and record it here.";

        private static readonly Regex LabelText = new Regex(
            @"[Ll]abel[A-Za-z0-9_]*(\.[A-Za-z0-9_]+)*\.(Text|TranslatedText)\b"
        );

        private static readonly Regex TooltipContent = new Regex(
            @"[Tt]ooltip[A-Za-z0-9_]*(\.[A-Za-z0-9_]+)*\.Content\b"
        );

        private static readonly Regex TextField = new Regex(
            @"AgeControlText(Field|Area)[A-Za-z0-9_]*(\.[A-Za-z0-9_]+)*\.Text\b"
        );

        private static readonly Regex Assignment = new Regex(
            @"\.(Text|TranslatedText|Content)\s*(\+?=)[^=]"
        );

        [Fact]
        public void EveryRawDrawnTextReadIsOnTheAllowlist()
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
                    if (LintSources.IsComment(line) || !Reads(line))
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

        private static bool Reads(string line)
        {
            return (
                    LabelText.IsMatch(line)
                    || TooltipContent.IsMatch(line)
                    || TextField.IsMatch(line)
                )
                && !Assignment.IsMatch(line);
        }

        private static bool Skipped(string file)
        {
            return file.StartsWith("ES2Access/Core/", StringComparison.Ordinal)
                || file.StartsWith("ES2Access/ES2/", StringComparison.Ordinal)
                // The dev dumps report the raw string on purpose - that is what a dump is for.
                || file.StartsWith("ES2Access/Dev/", StringComparison.Ordinal)
                // Reading the raw string and cleaning it is this file's entire job.
                || string.Equals(file, "ES2Access/UI/AgeText.cs", StringComparison.Ordinal);
        }
    }
}

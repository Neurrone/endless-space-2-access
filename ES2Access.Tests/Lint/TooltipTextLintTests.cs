using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// A tooltip's words reach the player through ONE door, and how loudly they arrive is decided by
    /// the tooltip's own kind - never by the site that read them.
    ///
    /// The ruling (owner, 2026-08-28): a tooltip the game wrote words for is announced whole when
    /// focus lands on the control; a tooltip the game assembles on hover reaches the player through
    /// the review buffer alone. <c>GraphNodes.ModeFor</c> is the only authority, the mode-taking
    /// section constructor is the door's own, and no factory anywhere takes a mode from a caller. So
    /// the wrong loudness is no longer expressible through the door - which leaves exactly one way to
    /// get it wrong: read the tooltip's TEXT yourself and wire the string into a readout, where the
    /// kind never gets a say. Nine sites did precisely that before this stage
    /// (<c>ValuePart(() =&gt; tooltip.Content)</c> and its relatives), and each was invisible in every
    /// dump and transcript.
    ///
    /// So every tooltip-text read outside the door is written down here. Most of the entries are
    /// legitimate and audited: a control the game NAMES nowhere but in its tooltip has its label read
    /// off the first line (<c>CardActions.FirstLine</c>), and a screen that reads the words itself to
    /// re-compose them hands them back to <c>GraphNodes.TooltipSection(tooltip, lines)</c>, which
    /// still takes the loudness from the tooltip. The lint does not judge those - it cannot - it makes
    /// the NEXT one a deliberate act with a diff line and a comment.
    ///
    /// Excluded wholesale (<see cref="Door"/>): the files that DEFINE these readers and the doors that
    /// derive a section from a tooltip. Reading a tooltip's text is their entire job.
    /// </summary>
    public class TooltipTextLintTests
    {
        private const string Allowlist = "tooltip-text-reads.allow";

        private const string Rule =
            "A tooltip's text reaches the player through the door (GraphNodes.TooltipSection / Sections / SectionsFor), and its KIND decides whether it is announced or reviewed - the reading site never does.\n"
            + "Reading the words here and wiring them into a readout is the one remaining way to announce a tooltip the game assembles on hover, or to silence one it wrote a sentence for.\n"
            + "Label fallbacks and re-composed readers are legitimate and listed here; say at the site which one this is, then record it.";

        // Every reader that answers with a TOOLTIP'S OWN WORDS.
        private static readonly Regex[] Readers =
        {
            new Regex(@"[Tt]ooltip[A-Za-z0-9_]*(\.[A-Za-z0-9_]+)*\.Content\b"),
            new Regex(@"\b[Tt]ip[A-Za-z0-9_]*(\.[A-Za-z0-9_]+)*\.Content\b"),
            new Regex(@"\bFirstLine\s*\("),
            new Regex(@"\bTooltipLines\s*\("),
            new Regex(@"\bAgeText\.Tooltip\s*\("),
            new Regex(@"\bTooltipDetails\s*\("),
        };

        // Writing the game's own tooltip is not reading it (the mod fills a scratch tooltip in to make
        // the game draw one of its own).
        private static readonly Regex Assignment = new Regex(@"\.Content\s*\+?=[^=]");

        /// <summary>
        /// The door and the readers themselves, exempt because this IS the mechanism.
        ///
        /// Verified against the tree rather than taken on trust: <c>AgeText.Tooltip</c> is defined in
        /// <c>AgeText.cs</c>, <c>TooltipLines</c> in <c>AgeWidgets.cs</c>, <c>FirstLine</c> in
        /// <c>CardActions.cs</c>; <c>GraphNodes.cs</c> is the door every section comes through and
        /// <c>TooltipChildren.cs</c> the nested-entry one; <c>DrawnTooltip.cs</c> and
        /// <c>TooltipFeatures.cs</c> are how a class-backed tooltip's words are read off the drawn
        /// window at all, and carry none of these patterns today - they are listed so that the reading
        /// mechanism can grow without a lint entry.
        ///
        /// <c>Cells.cs</c> is NOT exempt, though it is shared: it CALLS the readers to caption a cell,
        /// exactly as a screen does, and its three fallbacks are on the allowlist where a fourth will
        /// show up in the diff.
        /// </summary>
        private static readonly string[] Door =
        {
            "ES2Access/UI/AgeText.cs",
            "ES2Access/UI/AgeWidgets.cs",
            "ES2Access/UI/CardActions.cs",
            "ES2Access/UI/DrawnTooltip.cs",
            "ES2Access/UI/GraphNodes.cs",
            "ES2Access/UI/TooltipChildren.cs",
            "ES2Access/UI/TooltipFeatures.cs",
        };

        [Fact]
        public void EveryTooltipTextReadOutsideTheDoorIsOnTheAllowlist()
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
                if (!Screened(file))
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
            if (Assignment.IsMatch(line))
            {
                return false;
            }

            for (int i = 0; i < Readers.Length; i++)
            {
                if (Readers[i].IsMatch(line))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The screens and the shared widget readers they build on - where a tooltip read
        /// could feed an announcement. <c>Core/</c> has no tooltips to read and <c>Dev/</c> reports
        /// them on purpose, which is what a dump is for.</summary>
        private static bool Screened(string file)
        {
            if (
                !file.StartsWith("ES2Access/Screens/", StringComparison.Ordinal)
                && !file.StartsWith("ES2Access/UI/", StringComparison.Ordinal)
            )
            {
                return false;
            }

            for (int i = 0; i < Door.Length; i++)
            {
                if (string.Equals(file, Door[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

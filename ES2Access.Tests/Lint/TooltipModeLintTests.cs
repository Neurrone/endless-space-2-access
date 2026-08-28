using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// The two ways a screen could still name a tooltip's loudness for itself, held shut.
    ///
    /// A section's mode is the door's own now: the mode-taking constructor is
    /// <c>NodeSection.Derived</c>, doc-commented as <c>GraphNodes</c>-only, and the public factories a
    /// screen can reach are <c>NodeSection.Buffer</c> (content the control DRAWS) and
    /// <c>NodeSection.Composed</c> (words the MOD composed, where loudness is decidable precisely
    /// because there is no tooltip behind them to have a kind). That is a barrier of SHAPE, not of
    /// accessibility: <c>ES2Access.dll</c> is one assembly, so <c>internal</c> stops nothing, and two
    /// spellings remain that would put a tooltip's words into a readout the tooltip's kind never
    /// agreed to -
    ///
    /// <list type="number">
    /// <item><c>NodeSection.Derived(lines, TooltipMode.Announce, …)</c> straight from a screen;</item>
    /// <item><c>NodeSection.Composed(…)</c> fed a tooltip's lines - <c>GraphNodes.TooltipDetails</c>,
    /// <c>AgeWidgets.TooltipLines</c>, <c>AgeText.Tooltip</c> - which announces a class-backed stat
    /// block whole.</item>
    /// </list>
    ///
    /// The first is named here and has no occurrence outside the door: any use fails outright. The
    /// second cannot be recognised by its argument alone (a line-at-a-time lint cannot see a reader
    /// assigned to a local two lines up), so what is gated is every <c>Composed</c> call outside the
    /// door. Its three sites today are all mod-composed prose with no tooltip anywhere near them; a
    /// fourth has to say which it is. That is the only closure a source lint can honestly make, and it
    /// costs three allowlist lines.
    ///
    /// Closing the hole at the language level needs Core in an assembly of its own with
    /// <c>InternalsVisibleTo</c> for the tests - a bigger change than the stage that opened this
    /// question, and the reason this file exists instead.
    /// </summary>
    public class TooltipModeLintTests
    {
        private const string Allowlist = "tooltip-modes.allow";

        private const string Rule =
            "A tooltip's kind decides how it reaches the player - announced whole when the game wrote its words, review buffer alone when the game assembles them on hover - and GraphNodes.ModeFor is the only authority.\n"
            + "NodeSection.Derived is the door's own constructor: a screen naming a mode is a screen that can name the wrong one.\n"
            + "NodeSection.Composed is for words the MOD composed, where there is no tooltip to have a kind; fed a tooltip's lines it announces a stat block the ruling says is buffer-only. Pass tooltips to GraphNodes.TooltipSection / Sections / SectionsFor instead.";

        private static readonly Regex[] Holes =
        {
            new Regex(@"\bNodeSection\.Derived\b"),
            new Regex(@"\bNodeSection\.Composed\s*\("),
            new Regex(@"\bComposed\s*\(.*\b(TooltipDetails|TooltipLines|AgeText\.Tooltip)\s*\("),
        };

        // The door itself: GraphNodes is where a tooltip becomes a section, and it is the only caller
        // of Derived and the only place Composed is handed a screen's own details.
        private static readonly string[] Door = { "ES2Access/UI/GraphNodes.cs" };

        [Fact]
        public void NoScreenNamesASectionsModeForItself()
        {
            LintSources.AssertAllowed(Allowlist, Sites(), Rule);
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
                    if (LintSources.IsComment(line) || !Names(line))
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

        private static bool Names(string line)
        {
            for (int i = 0; i < Holes.Length; i++)
            {
                if (Holes[i].IsMatch(line))
                {
                    return true;
                }
            }

            return false;
        }

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

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// A tooltip is TWO promises, and a site that makes one without the other is a defect no
    /// transcript, dump or speech capture can show.
    ///
    /// <see cref="ES2Access.Core.UI.Graph.NodeVtable.PointsAt"/> declares WHICH dossier a node shows -
    /// that is the reviewable half, and it is what the parity audit judges an aim by.
    /// <c>NodeVtable.OnFocusVisual</c> is what moves the pointer onto the widget, and moving the
    /// pointer is the only thing that makes the GAME draw its own tooltip. Wire the first alone and
    /// the words read back perfectly while the picture never appears - which is exactly what shipped
    /// on the hero page's four ship-fact lines (owner-reported 2026-08-28).
    ///
    /// The standard door now wires both (<c>GraphNodes.SectionsFor(vtable, …)</c>), so the ordinary
    /// caller cannot reach the gap. This lint covers the other route: a screen writing
    /// <c>PointsAt</c> straight onto a vtable. Every such site is either accompanied by a pointer
    /// move - which is the whole of the rule - or is one of the genuinely aimless cases, and either
    /// way it is written into the allowlist where the diff shows it next to the comment that
    /// justifies it.
    ///
    /// This lint does not read the comment - it cannot judge one. It makes ADDING a bare aim a
    /// deliberate act.
    /// </summary>
    public class PointerAimLintTests
    {
        private const string Allowlist = "pointer-aims.allow";

        private const string Rule =
            "A bare PointsAt assignment needs a pointer move beside it and an allowlist entry - declaring a tooltip is not raising it.\n"
            + "PointsAt says WHICH dossier the node shows; OnFocusVisual is what makes the game draw it. One without the other reviews correctly and never appears.\n"
            + "Prefer GraphNodes.SectionsFor(vtable, …) or an AgeWidgets.Point/PointAt helper, which wire both. Bare aims belong to the aimless cases only.";

        private static readonly Regex Aim = new Regex(@"\.PointsAt\s*=[^=]");

        // These ARE the mechanism: the pointing helpers themselves, the door that wires both, and
        // the dossier emitter whose carrier-less branch is the documented aimless case. The rule is
        // about the screens that call them.
        private static readonly string[] Mechanism =
        {
            "ES2Access/UI/AgeWidgets.cs",
            "ES2Access/UI/GraphNodes.cs",
            "ES2Access/UI/TooltipChildren.cs",
        };

        [Fact]
        public void EveryBarePointerAimIsOnTheAllowlist()
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
                    if (LintSources.IsComment(line) || !Aim.IsMatch(line))
                    {
                        continue;
                    }

                    Site site = new Site(file, line.Trim());
                    int seen;
                    found[site] = found.TryGetValue(site, out seen) ? seen + 1 : 1;
                }
            }

            return found;
        }

        private static bool Skipped(string file)
        {
            foreach (string mechanism in Mechanism)
            {
                if (file.Replace('\\', '/').EndsWith(mechanism))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

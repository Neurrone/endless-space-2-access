using System.Collections.Generic;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// <c>NodeGate.Enabled</c> is the dev verification lever - flip it around two dumps and diff -
    /// and nothing else. The gate consults it internally; a walk or feature that reads or writes it
    /// ships two behaviors behind a switch no player ever sees. Every reference outside the gate's
    /// own file must be allowlisted, which today means the dev probes and nobody else.
    /// </summary>
    public class GateLeverLintTests
    {
        private const string Allowlist = "gate-lever.allow";

        private const string Rule =
            "NodeGate.Enabled is the dev verification lever (flip, dump, flip, dump, diff), not a"
            + " feature switch: production code must neither read nor write it. The gate consults it"
            + " internally; dev probes restore it in a finally. See UI/NodeGate.cs.";

        [Fact]
        public void EveryGateLeverReferenceIsOnTheAllowlist()
        {
            Dictionary<Site, int> found = new Dictionary<Site, int>();
            foreach (string file in LintSources.ModSources())
            {
                if (file == "ES2Access/UI/NodeGate.cs")
                {
                    continue; // the mechanism itself
                }

                foreach (string line in LintSources.Lines(file))
                {
                    if (LintSources.IsComment(line) || !line.Contains("NodeGate.Enabled"))
                    {
                        continue;
                    }

                    Site site = new Site(file, line.Trim());
                    int count;
                    found.TryGetValue(site, out count);
                    found[site] = count + 1;
                }
            }

            LintSources.AssertAllowed(Allowlist, found, Rule);
        }

        [Fact]
        public void TheAllowlistIsNotEmpty()
        {
            Assert.NotEmpty(LintSources.Allowed(Allowlist));
        }
    }
}

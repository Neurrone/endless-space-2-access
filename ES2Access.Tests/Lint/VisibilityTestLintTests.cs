using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// Existence belongs to the gate. <c>NodeGate</c> asks, of every node the builder is about to
    /// make, whether the renderer is drawing the widget that vouches for it - and asks it of the whole
    /// ancestry, which is what the renderer itself descends. A walk that asks the same question again
    /// on its own is at best duplicating the gate and at worst disagreeing with it: the one-step test
    /// the walks used to carry is exactly what let four pooled ghosts through.
    ///
    /// So a walk-level visibility test is now the exception, and every exception says which job it is
    /// doing that the gate cannot do - flow control (whether a subtree is walked at all), a content
    /// read (which of two words a control is called by), a spoken count, a banding input (a stale
    /// rectangle merges the rows the player hears), a dedupe, a different widget than the one the node
    /// stands on, an availability test, or the whole of a synthetic node's existence test.
    ///
    /// This lint does not read the comment - it cannot judge one. It makes ADDING a test a deliberate
    /// act: a new site fails until it is written into the allowlist, where it shows up in the diff
    /// next to the comment that justifies it.
    /// </summary>
    public class VisibilityTestLintTests
    {
        private const string Allowlist = "visibility-tests.allow";

        private const string Rule =
            "A walk-level visibility test needs a why-comment and an allowlist entry - existence belongs to NodeGate; see UI/NodeGate.cs.\n"
            + "The comment says which job the test does that the gate cannot: flow control, content read, spoken count, banding input, dedupe, different widget, availability, or a Synthetic node's own existence test.\n"
            + "If it only guards that a Drawn node's widget exists, delete it - the gate already asked, of the whole ancestry.";

        private static readonly Regex Helper = new Regex(
            @"AgeWidgets\.(Visible|Paints|Painted)\s*\("
        );

        // A screen that wraps the helper in a file-private predicate of its own is still testing
        // visibility at the walk, so its call sites count too - otherwise a local shim is a way out of
        // the rule. Only files that declare such a predicate are scanned for the bare name - or, for a
        // class split across several files, any file declaring that same class: the predicate is the
        // TYPE's, and a partial's other halves call it by the same bare name.
        private static readonly Regex LocalPredicate = new Regex(
            @"private\s+static\s+bool\s+(Visible|Paints|Painted)\s*\("
        );

        // Top level in its namespace - four spaces then a modifier - so that a nested helper class
        // does not lend its name to an unrelated file that happens to reuse it.
        private static readonly Regex TypeName = new Regex(
            @"^ {4}\S[\w ]*\b(?:class|struct)\s+(\w+)"
        );

        private static readonly Regex LocalCall = new Regex(
            @"(?<![\w.])(Visible|Paints|Painted)\s*\("
        );

        // The engine's own flags, read straight. The condition test keeps this to actual decisions:
        // an assignment sets paint state rather than asking about it, and a bare read feeding a
        // dev dump or a struct is not a walk deciding what the player hears.
        private static readonly Regex RawFlag = new Regex(@"\.(Visible|Alpha)\b");

        private static readonly Regex RawAssignment = new Regex(@"\.(Visible|Alpha)\s*=[^=]");

        private static readonly Regex Condition = new Regex(
            @"(\bif\s*\(|\?|&&|\|\||\breturn\b|\bwhile\s*\()"
        );

        // These four ARE the mechanism - the helper, the gate, the widget-of-subject lookup and the
        // node factory. The rule is about the walks that call them.
        private static readonly string[] Mechanism =
        {
            "ES2Access/UI/AgeWidgets.cs",
            "ES2Access/UI/NodeGate.cs",
            "ES2Access/UI/DrawnBy.cs",
            "ES2Access/UI/Nodes.cs",
        };

        [Fact]
        public void EveryWalkLevelVisibilityTestIsOnTheAllowlist()
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
            Dictionary<string, bool> shims = Shims();
            foreach (string file in LintSources.ModSources())
            {
                if (Skipped(file))
                {
                    continue;
                }

                string[] lines = LintSources.Lines(file);
                bool local = false;
                foreach (string line in lines)
                {
                    if (LocalPredicate.IsMatch(line) || Declares(shims, line))
                    {
                        local = true;
                        break;
                    }
                }

                foreach (string line in lines)
                {
                    if (LintSources.IsComment(line) || !Tests(line, local))
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

        /// <summary>The classes that declare a visibility shim of their own, so that every file of a
        /// partial one is scanned for the bare call the shim's own file would be.</summary>
        private static Dictionary<string, bool> Shims()
        {
            Dictionary<string, bool> names = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (string file in LintSources.ModSources())
            {
                if (Skipped(file))
                {
                    continue;
                }

                string[] lines = LintSources.Lines(file);
                bool declares = false;
                foreach (string line in lines)
                {
                    if (LocalPredicate.IsMatch(line))
                    {
                        declares = true;
                        break;
                    }
                }

                if (!declares)
                {
                    continue;
                }

                foreach (string line in lines)
                {
                    Match type = TypeName.Match(line);
                    if (type.Success)
                    {
                        names[type.Groups[1].Value] = true;
                    }
                }
            }

            return names;
        }

        private static bool Declares(Dictionary<string, bool> shims, string line)
        {
            Match type = TypeName.Match(line);
            return type.Success && shims.ContainsKey(type.Groups[1].Value);
        }

        private static bool Tests(string line, bool local)
        {
            if (Helper.IsMatch(line))
            {
                return true;
            }

            if (local && LocalCall.IsMatch(line) && !LocalPredicate.IsMatch(line))
            {
                return true;
            }

            return RawFlag.IsMatch(line)
                && !RawAssignment.IsMatch(line)
                && Condition.IsMatch(line)
                // The game's own line-of-sight enum, not a widget's paint state.
                && line.IndexOf("EntityVisibility.Layer", StringComparison.Ordinal) < 0;
        }

        private static bool Skipped(string file)
        {
            if (
                file.StartsWith("ES2Access/Core/", StringComparison.Ordinal)
                // Core knows nothing of widgets; the dev audits exist to report raw paint state.
                || file.StartsWith("ES2Access/Dev/", StringComparison.Ordinal)
            )
            {
                return true;
            }

            foreach (string mechanism in Mechanism)
            {
                if (string.Equals(file, mechanism, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

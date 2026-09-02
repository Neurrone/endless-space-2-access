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
    /// This lint does not READ the comment - it cannot judge one. What it can check is that one is
    /// there: a comment somewhere in the eight lines above the test. Presence is not judgement, and it
    /// is not meant to be; it is what stops the allowlist filling up with entries nobody ever wrote a
    /// reason for, which is exactly what it had done - 104 of 527 sites carried none.
    ///
    /// Eight lines because that is the reach of the block a why-comment is normally the head of: the
    /// comment, its wrapped continuation, and the two or three lines of setup between it and the
    /// condition it is about. A doc comment on the enclosing member counts, which is deliberate - a
    /// member whose whole summary is "only while the card is drawing the row" has said it.
    ///
    /// Together with the allowlist, ADDING a test is a deliberate act twice over: the site fails until
    /// a reason is written beside it, and again until it is written into the allowlist, where it shows
    /// up in the diff next to that reason.
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

        /// <summary>How far above a test a comment may sit and still be its reason.</summary>
        private const int CommentReach = 8;

        private const string CommentRule =
            "A walk-level visibility test needs its reason written beside it, within eight lines above.\n"
            + "Say which job the test does that NodeGate cannot: flow control, content read, spoken count, banding input, dedupe, different widget, availability, or a Synthetic node's own existence test.\n"
            + "If it only guards that a Drawn node's widget exists, delete it - the gate already asked, of the whole ancestry.";

        [Fact]
        public void EveryVisibilityTestHasACommentAboveIt()
        {
            List<string> bare = new List<string>();
            Dictionary<string, bool> shims = Shims();
            foreach (string file in LintSources.ModSources())
            {
                if (Skipped(file))
                {
                    continue;
                }

                string[] lines = LintSources.Lines(file);
                bool local = Local(lines, shims);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (LintSources.IsComment(lines[i]) || !Tests(lines[i], local))
                    {
                        continue;
                    }

                    if (!Explained(lines, i))
                    {
                        bare.Add(file + ":" + (i + 1) + ": " + lines[i].Trim());
                    }
                }
            }

            bare.Sort(StringComparer.Ordinal);
            Assert.True(
                bare.Count == 0,
                CommentRule
                    + Environment.NewLine
                    + Environment.NewLine
                    + "No comment within "
                    + CommentReach
                    + " lines above:"
                    + Environment.NewLine
                    + "  "
                    + string.Join(Environment.NewLine + "  ", bare.ToArray())
            );
        }

        private static bool Explained(string[] lines, int at)
        {
            for (int i = at - 1; i >= 0 && i >= at - CommentReach; i--)
            {
                if (LintSources.IsComment(lines[i]))
                {
                    return true;
                }
            }

            return false;
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
                bool local = Local(lines, shims);
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

        /// <summary>Whether this file's bare <c>Visible(</c> calls count: it declares a visibility
        /// shim of its own, or it is another half of a partial class that does.</summary>
        private static bool Local(string[] lines, Dictionary<string, bool> shims)
        {
            foreach (string line in lines)
            {
                if (LocalPredicate.IsMatch(line) || Declares(shims, line))
                {
                    return true;
                }
            }

            return false;
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

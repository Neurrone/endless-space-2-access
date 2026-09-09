using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// A screen's <c>Build</c> runs every frame, and a scene walk from a panel root -
    /// <c>GetComponentsInChildren</c> and its five relatives, or a <c>GameObject.Find</c> over the
    /// whole scene - is O(subtree) each time. One of them per row, on a table the player never even
    /// steps into, is the shape every per-frame finding in the 2026-09-09 review had.
    ///
    /// So a walk on a path the frame can reach is the exception. Two places need no excuse: a
    /// CONSTRUCTOR, which runs once for the object it fills, and <c>UI/FrameSweep.cs</c>, which is
    /// the frame-keyed cache the other walks are supposed to be going through. Everywhere else the
    /// site is on the allowlist with a <c>// walk:</c> comment on the line above it (or at the end of
    /// its own line) saying why this one is one-time or bounded - once per window open, once per
    /// binding, behind a sweep.
    ///
    /// This lint does not READ the comment; it checks that one is there, which is what stops the
    /// allowlist filling with entries nobody wrote a reason for. Adding a walk is then deliberate
    /// twice over: the reason is written beside the code, and the entry shows up in the diff next to
    /// it.
    /// </summary>
    public class SceneWalkLintTests
    {
        private const string Allowlist = "scene-walks.allow";

        private const string Rule =
            "A scene walk on a path the frame can reach needs a why-comment and an allowlist entry - a screen's Build runs every frame and GetComponentsInChildren from a panel root is O(subtree).\n"
            + "A constructor runs once and needs neither, and UI/FrameSweep.cs is the frame-keyed cache the other walks should be going through.\n"
            + "Everywhere else, write `// walk: <why it is one-time or bounded>` on the line above the call or at the end of it, then record the site here.\n"
            + "An exception to this rule must be reported to the owner.";

        /// <summary>The six component walks plus the scene-wide lookup by name. Matched against the
        /// line with its literals blanked, so the same words inside a message or a trailing comment
        /// are not a call.</summary>
        private static readonly Regex Walk = new Regex(
            @"\b(GetComponentsInChildren|GetComponentInChildren|GetComponentInParent|GetComponentsInParent|FindObjectOfType|FindObjectsOfTypeAll)\s*[<(]"
                + @"|(?<![\w.])GameObject\.Find\s*\("
        );

        /// <summary>The comment that excuses one, and the only form of it: a reason, not a label.
        /// </summary>
        private const string Why = "// walk:";

        [Fact]
        public void EverySceneWalkIsOnTheAllowlist()
        {
            LintSources.AssertAllowed(Allowlist, Sites(), Rule);
        }

        [Fact]
        public void TheAllowlistIsNotEmpty()
        {
            Assert.NotEmpty(LintSources.Allowed(Allowlist));
        }

        [Fact]
        public void EverySceneWalkSaysWhyItIsAffordable()
        {
            List<string> bare = new List<string>();
            foreach (string file in LintSources.ModSources())
            {
                if (Skipped(file))
                {
                    continue;
                }

                string[] lines = LintSources.Lines(file);
                Structure structure = LintSources.Read(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!Walks(lines[i]) || structure.InConstructor(i))
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
                Rule
                    + Environment.NewLine
                    + Environment.NewLine
                    + "No `"
                    + Why
                    + "` comment above or on:"
                    + Environment.NewLine
                    + "  "
                    + string.Join(Environment.NewLine + "  ", bare.ToArray())
            );
        }

        private static bool Explained(string[] lines, int at)
        {
            if (lines[at].IndexOf(Why, StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            return at > 0
                && lines[at - 1].Trim().StartsWith(Why, StringComparison.Ordinal);
        }

        private static Dictionary<Site, int> Sites()
        {
            Dictionary<Site, int> found = new Dictionary<Site, int>();
            foreach (string file in LintSources.ModSources())
            {
                if (Skipped(file))
                {
                    continue;
                }

                string[] lines = LintSources.Lines(file);
                Structure structure = LintSources.Read(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!Walks(lines[i]) || structure.InConstructor(i))
                    {
                        continue;
                    }

                    Site site = new Site(file, lines[i].Trim());
                    int count;
                    found[site] = found.TryGetValue(site, out count) ? count + 1 : 1;
                }
            }

            return found;
        }

        private static bool Walks(string line)
        {
            return !LintSources.IsComment(line) && Walk.IsMatch(LintSources.Code(line));
        }

        private static bool Skipped(string file)
        {
            // The dev server's probes and audits walk the scene on purpose, from a request rather
            // than from a frame; FrameSweep and LabelSweep ARE the frame-keyed walk.
            return file.StartsWith("ES2Access/Dev/", StringComparison.Ordinal)
                || string.Equals(file, "ES2Access/UI/FrameSweep.cs", StringComparison.Ordinal);
        }
    }
}

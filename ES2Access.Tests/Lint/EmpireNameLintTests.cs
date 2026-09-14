using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// The game draws an empire's name in four different forms, and which one a surface draws is a
    /// fact about that surface, not a preference: the leader name alone, the faction symbol in front
    /// of it, "leader (faction)", and the one hand-built form a coordination request carries. A site
    /// that calls the game's namer itself picks a form by passing flags, and the flags are easy to
    /// get wrong in the quiet direction - <c>iconPrefix:false</c> silently drops a faction the label
    /// beside it is drawing, and the raw <c>LocalizedName</c> leaks a name the fog is keeping. The
    /// 2026-09-14 audit found five of those and nine sites spelling the same idea four different
    /// ways.
    ///
    /// So <c>ES2Access/UI/EmpireNames.cs</c> owns every form, one method each, documented with which
    /// game surfaces draw it; everywhere else in the mod, naming an empire is a call to that file. A
    /// site that genuinely cannot go through it is on the allowlist with a <c>// name:</c> comment
    /// saying which drawn form it matches and why.
    ///
    /// Reading a label the game has already composed is not one of these calls and needs no entry -
    /// the negotiation banners and the battle group panels inherit whatever the game put in them.
    ///
    /// This lint does not READ the comment; it checks that one is there, which is what stops the
    /// allowlist filling with entries nobody wrote a reason for.
    /// </summary>
    public class EmpireNameLintTests
    {
        private const string Allowlist = "empire-names.allow";

        private const string Rule =
            "Naming an empire goes through ES2Access/UI/EmpireNames.cs, which owns one method per form the game draws - leader name alone, faction symbol in front of it, leader-and-faction, and the coordination request's hand-built form.\n"
            + "Calling GuiEmpire's namers directly, or reading an empire's Title or LocalizedName, picks a form by flag and is how a faction gets silently dropped and a fogged name silently leaked.\n"
            + "Reading a label the game already composed is not one of these calls and needs no entry.\n"
            + "Where a site truly cannot go through the helper, write `// name: <which drawn form this matches and why>` on the line above the call or at the end of it, then record the site here.\n"
            + "An exception to this rule must be reported to the owner.";

        /// <summary>The game's two namers, the faction symbol they prefix with, and the two raw
        /// properties that are not names at all. Matched against the line with its literals blanked,
        /// so the same words inside a message or a trailing comment are not a call.
        ///
        /// The two properties are caught two ways, because a type is not in the text: on any
        /// identifier that says "empire" in its own name, and on any identifier the same file
        /// declared as a <c>GuiEmpire</c> (<see cref="Wrappers"/>). A wrapper that arrives under some
        /// third name from another file is past what a line of text can tell - the helper is what
        /// keeps those honest, not this.</summary>
        private static readonly Regex Naming = new Regex(
            @"\b(GetLeaderName|GetLeaderAndFaction|GetSymbolString)\s*\("
                + @"|\b\w*[Ee]mpire\w*\s*\.\s*(Title|LocalizedName)\b"
        );

        /// <summary>A local, parameter or field this file declares as a <c>GuiEmpire</c>.</summary>
        private static readonly Regex Declared = new Regex(@"\bGuiEmpire\s+(\w+)\b");

        /// <summary>The raw properties, asked of the names this file declared as wrappers. Null where
        /// it declared none.</summary>
        private static Regex Wrappers(string[] lines)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (LintSources.IsComment(lines[i]))
                {
                    continue;
                }

                foreach (Match match in Declared.Matches(LintSources.Code(lines[i])))
                {
                    string name = match.Groups[1].Value;
                    if (!names.Contains(name))
                    {
                        names.Add(name);
                    }
                }
            }

            if (names.Count == 0)
            {
                return null;
            }

            names.Sort(StringComparer.Ordinal);
            return new Regex(
                @"\b(" + string.Join("|", names.ToArray()) + @")\s*\.\s*(Title|LocalizedName)\b"
            );
        }

        /// <summary>The comment that excuses one, and the only form of it: a reason, not a label.
        /// </summary>
        private const string Why = "// name:";

        [Fact]
        public void EveryEmpireNamingIsOnTheAllowlist()
        {
            LintSources.AssertAllowed(Allowlist, Sites(), Rule);
        }

        /// <summary>A rule that matches nothing passes for the wrong reason, and this one is
        /// allowed to have an empty allowlist - so the guard against a broken pattern is the pattern
        /// itself, asked about the calls it exists to find.</summary>
        [Fact]
        public void TheRuleRecognisesTheCallsItIsAbout()
        {
            Regex wrappers = Wrappers(new[] { "GuiEmpire sender = Wrap();" });
            Assert.True(Names("x = w.GetLeaderName(looking);", null));
            Assert.True(Names("x = w.GetLeaderAndFaction(looking, false, false);", null));
            Assert.True(Names("x = f.GetSymbolString(false);", null));
            Assert.True(Names("x = empire.Title;", null));
            Assert.True(Names("x = guiEmpire.LocalizedName;", null));
            Assert.True(Names("x = sender.LocalizedName;", wrappers));
            Assert.False(Names("x = sender.LocalizedName;", null));
            Assert.False(Names("// GetLeaderName is what the game calls", null));
            Assert.False(Names("Log.Warn(\"GetLeaderName(\");", null));
            Assert.False(Names("x = planet.LocalizedName;", wrappers));
        }

        [Fact]
        public void EveryEmpireNamingSaysWhichFormItMatches()
        {
            List<string> bare = new List<string>();
            foreach (string file in LintSources.ModSources())
            {
                if (Skipped(file))
                {
                    continue;
                }

                string[] lines = LintSources.Lines(file);
                Regex wrappers = Wrappers(lines);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (Names(lines[i], wrappers) && !Explained(lines, i))
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

            return at > 0 && lines[at - 1].Trim().StartsWith(Why, StringComparison.Ordinal);
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
                Regex wrappers = Wrappers(lines);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!Names(lines[i], wrappers))
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

        private static bool Names(string line, Regex wrappers)
        {
            if (LintSources.IsComment(line))
            {
                return false;
            }

            string code = LintSources.Code(line);
            return Naming.IsMatch(code) || (wrappers != null && wrappers.IsMatch(code));
        }

        private static bool Skipped(string file)
        {
            // The one file whose job this is.
            return string.Equals(file, "ES2Access/UI/EmpireNames.cs", StringComparison.Ordinal);
        }
    }
}

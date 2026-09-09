using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests.Lint
{
    /// <summary>
    /// The machinery the three source lints share: find the mod's sources on disk, and compare what
    /// they contain against a checked-in allowlist.
    ///
    /// A lint site is identified by its FILE plus the trimmed text of the line it sits on, never by a
    /// line number - a number drifts the moment anything above it is edited, and an allowlist that
    /// churns on every unrelated edit stops being read. Identical lines in one file (the same
    /// <c>if (widget == null || !AgeWidgets.Visible(widget))</c> guarding four different walks) are
    /// counted rather than listed twice, so the count is the thing that has to move when a fifth
    /// appears.
    ///
    /// The gate runs BOTH WAYS. A site the allowlist does not cover fails, and so does an allowlist
    /// entry no site answers to any more - because a stale entry is not the harmless leftover it looks
    /// like: it is a standing pre-authorisation, and the first line of code that happens to match its
    /// text again is admitted without anybody deciding. That is not hypothetical. A fossil in
    /// <c>synthetic-nodes.allow</c> outlived the walk it was written for and silently vouched for a
    /// misdeclared <c>scan:system/name</c>, which reached the player as a logged warning nobody had
    /// approved. The count is part of the entry, so an entry that allows more occurrences than the
    /// tree contains is the same fossil in miniature and fails the same way.
    ///
    /// Mechanical half of the remedy, once the why-comment is written:
    /// <c>ES2ACCESS_LINT_REGENERATE=visibility-tests dotnet test</c> rewrites THAT allowlist in place.
    /// The entry then shows up in the diff, which is the whole point of the file.
    ///
    /// One list at a time, named: rewriting all eight at once means a run made for one rule silently
    /// re-blesses whatever the other seven happen to see, and the diff that is supposed to be read
    /// arrives with seven files of noise around it.
    /// </summary>
    public static class LintSources
    {
        /// <summary>Set this in the environment to the NAME of the one allowlist to rewrite from the
        /// current tree - <c>visibility-tests</c>, with or without the <c>.allow</c>. Any other value
        /// rewrites nothing, so a typo leaves every list alone rather than rewriting the wrong
        /// one.</summary>
        public const string RegenerateVariable = "ES2ACCESS_LINT_REGENERATE";

        /// <summary>Every <c>.cs</c> file under <c>ES2Access/</c> that a person wrote, as
        /// repository-relative paths with forward slashes, sorted so a regenerated allowlist has a
        /// stable order.
        ///
        /// <c>obj/</c> and <c>bin/</c> are excluded, and that is a correctness rule rather than
        /// tidiness: the generated <c>obj/Debug/ES2Access.AssemblyInfo.cs</c> exists only once the
        /// plugin has been built, so leaving it in makes what the lints see depend on whether
        /// somebody ran a build - the one thing an allowlist gate must never do.</summary>
        public static IList<string> ModSources()
        {
            string root = Path.Combine(RepoRoot(), "ES2Access");
            List<string> relative = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string path =
                    "ES2Access/"
                    + file.Substring(root.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                if (path.Contains("/obj/") || path.Contains("/bin/"))
                {
                    continue;
                }

                relative.Add(path);
            }

            relative.Sort(StringComparer.Ordinal);
            Assert.NotEmpty(relative);
            return relative;
        }

        // Every lint fact sweeps every source, so the whole tree would otherwise be read off disk
        // once per fact. The tree does not change while a test run is in flight.
        private static readonly Dictionary<string, string[]> LineCache =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        public static string[] Lines(string relativePath)
        {
            lock (LineCache)
            {
                string[] lines;
                if (!LineCache.TryGetValue(relativePath, out lines))
                {
                    lines = File.ReadAllLines(
                        Path.Combine(
                            RepoRoot(),
                            relativePath.Replace('/', Path.DirectorySeparatorChar)
                        )
                    );
                    LineCache[relativePath] = lines;
                }

                return lines;
            }
        }

        /// <summary>A line that carries no code: a <c>//</c> comment, a doc comment, or the
        /// continuation of a block comment. Prose about a widget's <c>.Visible</c> is not a test of
        /// it.</summary>
        public static bool IsComment(string line)
        {
            string text = line.Trim();
            return text.StartsWith("//", StringComparison.Ordinal)
                || text.StartsWith("*", StringComparison.Ordinal)
                || text.StartsWith("/*", StringComparison.Ordinal);
        }

        /// <summary>Reads an allowlist. Blank lines and <c>#</c> lines are ignored; every other line is
        /// <c>path | count | trimmed source line</c>, and the source text may itself contain <c>|</c>
        /// (<c>||</c> is everywhere in these conditions), so only the first two separators split.</summary>
        public static Dictionary<Site, int> Allowed(string allowlist)
        {
            Dictionary<Site, int> counts = new Dictionary<Site, int>();
            string path = AllowlistPath(allowlist);
            if (!File.Exists(path))
            {
                return counts;
            }

            foreach (string line in File.ReadAllLines(path))
            {
                string text = line.Trim();
                if (text.Length == 0 || text.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int file = text.IndexOf('|');
                Assert.True(file > 0, allowlist + ": malformed entry: " + text);
                int count = text.IndexOf('|', file + 1);
                Assert.True(count > file, allowlist + ": malformed entry: " + text);

                Site site = new Site(
                    text.Substring(0, file).Trim(),
                    text.Substring(count + 1).Trim()
                );
                counts[site] = int.Parse(text.Substring(file + 1, count - file - 1).Trim());
            }

            return counts;
        }

        /// <summary>
        /// The one assertion all the source lints end in, and it is a set EQUALITY rather than a
        /// containment: every site found in the tree is covered by the allowlist at least as many
        /// times as it occurs, and every entry of the allowlist answers to a site that is still there.
        /// </summary>
        public static void AssertAllowed(
            string allowlist,
            IDictionary<Site, int> found,
            string rule
        )
        {
            if (Regenerating(allowlist))
            {
                Write(allowlist, found, rule);
            }

            Dictionary<Site, int> allowed = Allowed(allowlist);
            List<string> unlisted = new List<string>();
            foreach (KeyValuePair<Site, int> entry in found)
            {
                int budget;
                if (!allowed.TryGetValue(entry.Key, out budget) || budget < entry.Value)
                {
                    unlisted.Add(
                        entry.Key.File
                            + ": "
                            + entry.Key.Text
                            + (entry.Value > 1 ? "   (x" + entry.Value + ")" : string.Empty)
                    );
                }
            }

            unlisted.Sort(StringComparer.Ordinal);
            Assert.True(
                unlisted.Count == 0,
                rule
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Not in ES2Access.Tests/Lint/"
                    + allowlist
                    + ":"
                    + Environment.NewLine
                    + "  "
                    + string.Join(Environment.NewLine + "  ", unlisted.ToArray())
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Once the site carries its why-comment, re-run with "
                    + RegenerateVariable
                    + "="
                    + Path.GetFileNameWithoutExtension(allowlist)
                    + " to record it."
            );

            // The other direction. An entry standing over nothing is a pre-authorisation waiting for
            // the next line of code to match its text, and one allowing more occurrences than the tree
            // has is the same thing for the next copy of a line that is already there.
            List<string> orphaned = new List<string>();
            foreach (KeyValuePair<Site, int> entry in allowed)
            {
                int occurrences;
                if (!found.TryGetValue(entry.Key, out occurrences))
                {
                    orphaned.Add(entry.Key.File + ": " + entry.Key.Text);
                }
                else if (entry.Value > occurrences)
                {
                    orphaned.Add(
                        entry.Key.File
                            + ": "
                            + entry.Key.Text
                            + "   (allows "
                            + entry.Value
                            + ", tree has "
                            + occurrences
                            + ")"
                    );
                }
            }

            orphaned.Sort(StringComparer.Ordinal);
            Assert.True(
                orphaned.Count == 0,
                "An allowlist entry no source site answers to is a standing pre-authorisation - the next line that matches its text is admitted without anybody deciding. Prune it."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Stale in ES2Access.Tests/Lint/"
                    + allowlist
                    + ":"
                    + Environment.NewLine
                    + "  "
                    + string.Join(Environment.NewLine + "  ", orphaned.ToArray())
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Re-run with "
                    + RegenerateVariable
                    + "="
                    + Path.GetFileNameWithoutExtension(allowlist)
                    + " to rewrite the file from the tree."
            );
        }

        /// <summary>Whether this run was asked to rewrite THIS list. The variable carries the list's
        /// name rather than a flag, so a regeneration is always a decision about one rule.</summary>
        private static bool Regenerating(string allowlist)
        {
            string wanted = Environment.GetEnvironmentVariable(RegenerateVariable);
            if (string.IsNullOrEmpty(wanted))
            {
                return false;
            }

            wanted = wanted.Trim();
            string bare = allowlist.EndsWith(".allow", StringComparison.OrdinalIgnoreCase)
                ? allowlist.Substring(0, allowlist.Length - ".allow".Length)
                : allowlist;
            return string.Equals(wanted, allowlist, StringComparison.OrdinalIgnoreCase)
                || string.Equals(wanted, bare, StringComparison.OrdinalIgnoreCase);
        }

        private static void Write(string allowlist, IDictionary<Site, int> found, string rule)
        {
            List<Site> sites = new List<Site>(found.Keys);
            sites.Sort(
                delegate(Site left, Site right)
                {
                    int file = StringComparer.Ordinal.Compare(left.File, right.File);
                    return file != 0
                        ? file
                        : StringComparer.Ordinal.Compare(left.Text, right.Text);
                }
            );

            List<string> lines = new List<string>();
            foreach (string sentence in rule.Split('\n'))
            {
                lines.Add("# " + sentence.Trim());
            }

            lines.Add("#");
            lines.Add(
                "# Generated by "
                    + RegenerateVariable
                    + "="
                    + Path.GetFileNameWithoutExtension(allowlist)
                    + "; format: path | count | source line."
            );
            lines.Add("#");
            string previous = null;
            foreach (Site site in sites)
            {
                if (site.File != previous)
                {
                    lines.Add(string.Empty);
                    previous = site.File;
                }

                lines.Add(site.File + " | " + found[site] + " | " + site.Text);
            }

            File.WriteAllLines(AllowlistPath(allowlist), lines.ToArray());
        }

        private static string AllowlistPath(string allowlist)
        {
            return Path.Combine(RepoRoot(), "ES2Access.Tests", "Lint", allowlist);
        }

        public static string RepoRoot()
        {
            return TestPaths.RepoRoot();
        }

        /// <summary>The line with its string and character literals blanked and its trailing
        /// <c>//</c> comment cut, so that a brace inside a message does not move the structure.
        /// Literals are blanked rather than removed so column positions survive.</summary>
        public static string Code(string line)
        {
            StringBuilder code = new StringBuilder(line.Length);
            bool inString = false;
            bool inChar = false;
            bool verbatim = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (!inString && !inChar && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    break;
                }

                if (!inString && !inChar && c == '@' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    inString = true;
                    verbatim = true;
                    code.Append(' ');
                    code.Append(' ');
                    i++;
                    continue;
                }

                if (!inString && !inChar && c == '"')
                {
                    inString = true;
                    verbatim = false;
                    code.Append('"');
                    continue;
                }

                if (inString)
                {
                    if (!verbatim && c == '\\' && i + 1 < line.Length)
                    {
                        code.Append(' ');
                        code.Append(' ');
                        i++;
                        continue;
                    }

                    if (c == '"')
                    {
                        if (verbatim && i + 1 < line.Length && line[i + 1] == '"')
                        {
                            code.Append(' ');
                            code.Append(' ');
                            i++;
                            continue;
                        }

                        inString = false;
                        code.Append('"');
                        continue;
                    }

                    code.Append(' ');
                    continue;
                }

                if (!inChar && c == '\'')
                {
                    inChar = true;
                    code.Append('\'');
                    continue;
                }

                if (inChar)
                {
                    if (c == '\\' && i + 1 < line.Length)
                    {
                        code.Append(' ');
                        code.Append(' ');
                        i++;
                        continue;
                    }

                    if (c == '\'')
                    {
                        inChar = false;
                        code.Append('\'');
                        continue;
                    }

                    code.Append(' ');
                    continue;
                }

                code.Append(c);
            }

            return code.ToString();
        }

        private static readonly Dictionary<string, Structure> StructureCache =
            new Dictionary<string, Structure>(StringComparer.Ordinal);

        /// <summary>Which type and which member every line of a file sits in - built once per file
        /// per run, like <see cref="Lines"/>.</summary>
        public static Structure Read(string relativePath)
        {
            lock (StructureCache)
            {
                Structure structure;
                if (!StructureCache.TryGetValue(relativePath, out structure))
                {
                    structure = Structure.Of(Lines(relativePath));
                    StructureCache[relativePath] = structure;
                }

                return structure;
            }
        }
    }

    /// <summary>A lint site: the file it is in, and the trimmed text of its line.</summary>
    public struct Site : IEquatable<Site>
    {
        public readonly string File;

        public readonly string Text;

        public Site(string file, string text)
        {
            File = file;
            Text = text;
        }

        public bool Equals(Site other)
        {
            return string.Equals(File, other.File, StringComparison.Ordinal)
                && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is Site && Equals((Site)obj);
        }

        public override int GetHashCode()
        {
            return File.GetHashCode() ^ Text.GetHashCode();
        }
    }

    /// <summary>
    /// Which type and which member every line of a file sits in.
    ///
    /// THE HEURISTIC. C# is not parsed here; braces are counted. Walking the file top to bottom, a
    /// running stack of open blocks is kept. A line is first attributed to the stack as it stands
    /// BEFORE that line's braces - so a member's own signature line reads as type scope, which is
    /// what it is. Then, if the line looks like a type or member declaration, it is remembered as
    /// PENDING; the next <c>{</c> opens that block, and every other <c>{</c> opens an anonymous one.
    /// A <c>}</c> closes the innermost. Pending survives lines with no braces, which is what carries
    /// a signature wrapped over three lines to the brace on the fourth.
    ///
    /// String and character literals are blanked first (<see cref="LintSources.Code"/>) so a brace
    /// inside a message counts for nothing. What this cannot see: an expression-bodied member
    /// (<c>=&gt;</c>) opens no block, so its line reads as type scope rather than as that member's
    /// body. For the constructor question that errs the safe way - such a site is asked for its
    /// why-comment rather than excused.
    /// </summary>
    public sealed class Structure
    {
        /// <summary>The enclosing member's name per line, or null at type or file scope.</summary>
        public string[] Member;

        /// <summary>The enclosing type's name per line, or null at file scope.</summary>
        public string[] Type;

        private static readonly Regex TypeDeclaration = new Regex(
            @"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|sealed|abstract|partial|readonly|unsafe|new)\s+)*(?:class|struct|interface|enum)\s+(\w+)"
        );

        // A member: modifiers, then whatever the return type is, then the name and its opening
        // parenthesis. Non-greedy up to the FIRST parenthesis, and nothing may cross an `=` or a
        // `;`, which is what keeps a field initialiser holding a call from reading as a member.
        private static readonly Regex MemberDeclaration = new Regex(
            @"^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal|static|virtual|override|abstract|sealed|async|extern|unsafe|new|partial)\s[^=;{}]*?(\w+)\s*(?:<[^<>()]*>)?\s*\("
        );

        public static Structure Of(string[] lines)
        {
            Structure structure = new Structure
            {
                Member = new string[lines.Length],
                Type = new string[lines.Length],
            };

            List<Frame> stack = new List<Frame>();
            Frame pending = null;
            for (int i = 0; i < lines.Length; i++)
            {
                Frame member = Innermost(stack, "member");
                Frame type = Innermost(stack, "type");
                structure.Member[i] = member == null ? null : member.Name;
                structure.Type[i] = type == null ? null : type.Name;

                string code = LintSources.Code(lines[i]);
                if (!LintSources.IsComment(lines[i]))
                {
                    Match declared = TypeDeclaration.Match(code);
                    if (declared.Success)
                    {
                        pending = new Frame("type", declared.Groups[1].Value);
                    }
                    else
                    {
                        Match method = MemberDeclaration.Match(code);
                        if (method.Success)
                        {
                            pending = new Frame("member", method.Groups[1].Value);
                        }
                    }
                }

                foreach (char c in code)
                {
                    if (c == '{')
                    {
                        stack.Add(pending ?? new Frame("other", null));
                        pending = null;
                    }
                    else if (c == '}' && stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }
                }
            }

            return structure;
        }

        /// <summary>Whether the line sits in a constructor: the member enclosing it is the one whose
        /// name is its type's.</summary>
        public bool InConstructor(int line)
        {
            return Member[line] != null
                && string.Equals(Member[line], Type[line], StringComparison.Ordinal);
        }

        private static Frame Innermost(List<Frame> stack, string kind)
        {
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (kind == null || stack[i].Kind == kind)
                {
                    return stack[i];
                }
            }

            return null;
        }

        private sealed class Frame
        {
            public readonly string Kind;

            public readonly string Name;

            public Frame(string kind, string name)
            {
                Kind = kind;
                Name = name;
            }
        }
    }
}

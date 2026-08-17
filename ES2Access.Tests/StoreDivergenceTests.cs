using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace ES2Access.Tests
{
    /// <summary>
    /// The Steam and GOG builds of the game differ in the game's own assemblies: the class Steam
    /// calls <c>Galaxy</c> is <c>GalaxyIngame</c> on GOG, and the GOG <c>ModdingScreen</c> declares
    /// no Steam Workshop fields at all. A member reference to a name that exists in only one build
    /// compiles cleanly against that build and fails at RUNTIME on the other — a breakage no build
    /// or test on a single machine can see. <c>UI/GameGalaxy.cs</c> and <c>UI/SteamWorkshop.cs</c>
    /// reach the divergent members by reflection and are the only files allowed to name them; this
    /// scan is what refuses a third site instead of a doc comment hoping to be read.
    /// </summary>
    public class StoreDivergenceTests
    {
        private static readonly Regex Forbidden = new Regex(
            @"\b(Galaxy|GalaxyIngame|SteamWorkshopButton|WorkshopLegalAgreementButton"
                + @"|WorkshopLegalAgreementLabel|WorkshopFilterToggle)\b"
        );

        private static readonly HashSet<string> SeamFiles = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "GameGalaxy.cs",
            "SteamWorkshop.cs",
        };

        [Fact]
        public void OnlyTheSeamFilesNameStoreDivergentMembers()
        {
            List<string> violations = new List<string>();
            foreach (string project in new[] { "ES2Access", "ES2Access.Loader" })
            {
                string root = Path.Combine(RepositoryRoot(), project);
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(RepositoryRoot(), file);
                    if (
                        SeamFiles.Contains(Path.GetFileName(file))
                        || relative.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        || relative.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                    )
                    {
                        continue;
                    }

                    string code = WithoutCommentsAndStrings(File.ReadAllText(file));
                    foreach (Match match in Forbidden.Matches(code))
                    {
                        int line = 1 + CountNewlines(code, match.Index);
                        violations.Add(relative + ":" + line + " names '" + match.Value + "'");
                    }
                }
            }

            Assert.True(
                violations.Count == 0,
                "Store-divergent names outside the reflection seams (route these through "
                    + "UI/GameGalaxy.cs or UI/SteamWorkshop.cs — see their doc comments):\n"
                    + string.Join("\n", violations)
            );
        }

        /// <summary>
        /// Blanks comments, string literals and char literals to spaces, preserving newlines so a
        /// match's line number stays true. An interpolated string is blanked whole, holes included —
        /// a forbidden name inside a hole escapes this scan, which the mod's no-inline-English
        /// convention keeps a non-case.
        /// </summary>
        private static string WithoutCommentsAndStrings(string source)
        {
            char[] text = source.ToCharArray();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    while (i < text.Length && text[i] != '\n')
                    {
                        text[i++] = ' ';
                    }
                }
                else if (c == '/' && next == '*')
                {
                    text[i++] = ' ';
                    text[i++] = ' ';
                    while (i < text.Length && !(text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/'))
                    {
                        Blank(text, i++);
                    }
                    if (i < text.Length)
                    {
                        text[i++] = ' ';
                        text[i++] = ' ';
                    }
                }
                else if ((c == '@' || c == '$') && next == '"')
                {
                    bool verbatim = c == '@';
                    text[i++] = ' ';
                    text[i++] = ' ';
                    BlankStringBody(text, ref i, verbatim);
                }
                else if (c == '"')
                {
                    text[i++] = ' ';
                    BlankStringBody(text, ref i, verbatim: false);
                }
                else if (c == '\'')
                {
                    text[i++] = ' ';
                    while (i < text.Length && text[i] != '\'')
                    {
                        if (text[i] == '\\')
                        {
                            Blank(text, i++);
                        }
                        if (i < text.Length)
                        {
                            Blank(text, i++);
                        }
                    }
                    if (i < text.Length)
                    {
                        text[i++] = ' ';
                    }
                }
                else
                {
                    i++;
                }
            }

            return new string(text);
        }

        private static void BlankStringBody(char[] text, ref int i, bool verbatim)
        {
            while (i < text.Length)
            {
                if (verbatim && text[i] == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    text[i++] = ' ';
                    text[i++] = ' ';
                }
                else if (!verbatim && text[i] == '\\')
                {
                    Blank(text, i++);
                    if (i < text.Length)
                    {
                        Blank(text, i++);
                    }
                }
                else if (text[i] == '"')
                {
                    text[i++] = ' ';
                    return;
                }
                else
                {
                    Blank(text, i++);
                }
            }
        }

        private static void Blank(char[] text, int at)
        {
            if (text[at] != '\n')
            {
                text[at] = ' ';
            }
        }

        private static int CountNewlines(string text, int before)
        {
            int count = 0;
            for (int i = 0; i < before; i++)
            {
                if (text[i] == '\n')
                {
                    count++;
                }
            }

            return count;
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ES2Access", "ES2Access.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("repository root not found above " + AppContext.BaseDirectory);
        }
    }
}

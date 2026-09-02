using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using ES2Access.Core.Speech;

namespace ES2Access.Tests.Speech
{
    /// <summary>What a scan of the mod's sources found out about its counted phrases.</summary>
    public sealed class PluralPairScan
    {
        /// <summary>The MANY key of every plural pair the mod speaks - the key a three-form language
        /// hangs its paucal form off.</summary>
        public readonly SortedSet<string> ManyKeys = new SortedSet<string>();

        /// <summary>Files calling Plural with something other than a ModStrings constant, relative to
        /// the repository root. Each one is a pair the scan cannot see, so each has to be traced by
        /// hand and listed in <see cref="PluralPairs"/>.</summary>
        public readonly SortedSet<string> IndirectSites = new SortedSet<string>();
    }

    /// <summary>
    /// Which locale keys form a counted pair, read off the call sites rather than off the key names.
    ///
    /// It has to be read off the calls: the pairs are named every way English suggests
    /// (...One/...Many, Point/Points, Line/Lines, ThisTurn/Turns), so no naming convention can find
    /// them, and a pair the scan missed is a paucal form a Polish or Russian translator is never
    /// asked for and never warned about.
    /// </summary>
    public static class PluralPairs
    {
        // Both arguments span lines at most call sites, which \s already covers.
        private static readonly Regex Call = new Regex(
            @"ModStrings\.Plural\(\s*([A-Za-z_][\w.]*)\s*,\s*([A-Za-z_][\w.]*)\s*,"
        );

        private const string Qualifier = "ModStrings.";

        /// <summary>
        /// The pairs no scan can resolve, traced by hand.
        ///
        /// SystemLabelReadout.AddShipCount takes its pair as parameters. Its only caller,
        /// AddGarrisons, passes ModStrings constants for both sides of both lozenges, so the pairs
        /// are as fixed as any literal call site - they are simply spelt one method away from the
        /// Plural call.
        /// </summary>
        private static readonly string[] Traced =
        {
            "GalaxySystemFriendlyShips",
            "GalaxySystemHostileShips",
        };

        /// <summary>The file the traced pairs live in, so that a SECOND indirect call site anywhere
        /// else fails the scan rather than quietly going uncovered.</summary>
        public const string TracedSite = "ES2Access/UI/SystemLabelReadout.cs";

        public static PluralPairScan Scan()
        {
            PluralPairScan scan = new PluralPairScan();
            string root = TranslationFiles.RepoRoot();
            string sources = Path.Combine(root, "ES2Access");
            foreach (
                string file in Directory.GetFiles(sources, "*.cs", SearchOption.AllDirectories)
            )
            {
                foreach (Match call in Call.Matches(File.ReadAllText(file)))
                {
                    string argument = call.Groups[2].Value;
                    if (!argument.StartsWith(Qualifier, StringComparison.Ordinal))
                    {
                        scan.IndirectSites.Add(Relative(root, file));
                        continue;
                    }

                    scan.ManyKeys.Add(
                        KeyOf(argument.Substring(Qualifier.Length), Relative(root, file))
                    );
                }
            }

            foreach (string field in Traced)
            {
                scan.ManyKeys.Add(KeyOf(field, TracedSite));
            }

            return scan;
        }

        private static string KeyOf(string field, string where)
        {
            FieldInfo constant = typeof(ModStrings).GetField(
                field,
                BindingFlags.Public | BindingFlags.Static
            );
            if (constant == null || !constant.IsLiteral || constant.FieldType != typeof(string))
            {
                throw new InvalidOperationException(
                    where + ": ModStrings." + field + " is not a public string constant"
                );
            }

            return (string)constant.GetRawConstantValue();
        }

        private static string Relative(string root, string file)
        {
            return file.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
        }
    }
}

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
        private readonly SortedDictionary<string, string> _pairs = new SortedDictionary<
            string,
            string
        >(StringComparer.Ordinal);

        /// <summary>Every plural pair the mod speaks, MANY key to ONE key. The MANY key is what a
        /// three-form language hangs its extra forms off; the ONE key is what says whether the pair
        /// is one whose singular sentence has no number in it.</summary>
        public IDictionary<string, string> Pairs
        {
            get { return _pairs; }
        }

        /// <summary>The MANY key of every plural pair the mod speaks.</summary>
        public ICollection<string> ManyKeys
        {
            get { return _pairs.Keys; }
        }

        /// <summary>Files calling Plural or PluralKey with something other than a ModStrings
        /// constant, relative to the repository root. Each one is a pair the scan cannot see, so each
        /// has to be traced by hand and listed in <see cref="PluralPairs"/>.</summary>
        public readonly SortedSet<string> IndirectSites = new SortedSet<string>();

        internal void Add(string oneKey, string manyKey)
        {
            _pairs[manyKey] = oneKey;
        }
    }

    /// <summary>
    /// Which locale keys form a counted pair, read off the call sites rather than off the key names.
    ///
    /// It has to be read off the calls: the pairs are named every way English suggests
    /// (...One/...Many, Point/Points, Line/Lines, ThisTurn/Turns), so no naming convention can find
    /// them, and a pair the scan missed is a form a Polish or Russian translator is never asked for
    /// and never warned about.
    /// </summary>
    public static class PluralPairs
    {
        // Both arguments span lines at most call sites, which \s already covers. PluralKey is the
        // same choice made for a caller that formats the phrase itself, so its pairs count too.
        private static readonly Regex Call = new Regex(
            @"ModStrings\.Plural(?:Key)?\(\s*([A-Za-z_][\w.]*)\s*,\s*([A-Za-z_][\w.]*)\s*,"
        );

        private const string Qualifier = "ModStrings.";

        /// <summary>One pair no scan can resolve, and the file whose call hides it.</summary>
        private sealed class TracedPair
        {
            public string Site;
            public string OneField;
            public string ManyField;
        }

        /// <summary>
        /// The pairs no scan can resolve, traced by hand.
        ///
        /// SystemLabelReadout.AddShipCount and BattleText.Counted both take their pair as
        /// parameters. Each has callers that pass ModStrings constants for both sides, so the pairs
        /// are as fixed as any literal call site - they are simply spelt one method away from the
        /// call.
        /// </summary>
        private static readonly TracedPair[] Traced =
        {
            new TracedPair
            {
                Site = "ES2Access/UI/SystemLabelReadout.cs",
                OneField = "GalaxySystemFriendlyShip",
                ManyField = "GalaxySystemFriendlyShips",
            },
            new TracedPair
            {
                Site = "ES2Access/UI/SystemLabelReadout.cs",
                OneField = "GalaxySystemHostileShip",
                ManyField = "GalaxySystemHostileShips",
            },
            new TracedPair
            {
                Site = "ES2Access/Core/Speech/BattleText.cs",
                OneField = "BattleFireMissedClause",
                ManyField = "BattleFireMissedClauseMany",
            },
        };

        /// <summary>The files the traced pairs live in, so that a FURTHER indirect call site
        /// anywhere else fails the scan rather than quietly going uncovered.</summary>
        public static IEnumerable<string> TracedSites
        {
            get
            {
                SortedSet<string> sites = new SortedSet<string>();
                foreach (TracedPair pair in Traced)
                {
                    sites.Add(pair.Site);
                }

                return sites;
            }
        }

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
                    string one = call.Groups[1].Value;
                    string many = call.Groups[2].Value;
                    if (
                        !one.StartsWith(Qualifier, StringComparison.Ordinal)
                        || !many.StartsWith(Qualifier, StringComparison.Ordinal)
                    )
                    {
                        scan.IndirectSites.Add(Relative(root, file));
                        continue;
                    }

                    string where = Relative(root, file);
                    scan.Add(
                        KeyOf(one.Substring(Qualifier.Length), where),
                        KeyOf(many.Substring(Qualifier.Length), where)
                    );
                }
            }

            foreach (TracedPair pair in Traced)
            {
                scan.Add(KeyOf(pair.OneField, pair.Site), KeyOf(pair.ManyField, pair.Site));
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

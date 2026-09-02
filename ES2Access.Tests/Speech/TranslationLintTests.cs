using System.Collections.Generic;
using System.IO;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// Applies <see cref="LocaleLint"/> to every translation the mod ships.
    ///
    /// It sits beside <see cref="LocaleFileTests"/> rather than inside it because that class is
    /// mirrored into docs/generic as the worked example of a translation validator, and these
    /// checks lean on this repository's own scaffolding - the plural-pair scan over the sources,
    /// the sources/ snapshots. The two overlap on the key and placeholder checks; that costs a
    /// couple of milliseconds and keeps the mirrored example standalone.
    ///
    /// Each test runs one lint over one file and reports every offender at once: a translator
    /// handing a file back needs the whole list, and an assertion per entry would stop at the
    /// first. Only english.json ships today, so most of these pass over a single file - the lints
    /// themselves are proven against deliberately broken input in <see cref="LocaleLintTests"/>.
    /// </summary>
    public class TranslationLintTests
    {
        public static IEnumerable<object[]> LocaleFiles()
        {
            foreach (string fileName in TranslationFiles.LocaleFileNames())
            {
                yield return new object[] { fileName };
            }
        }

        /// <summary>Every file in the folder has to be a language the game can ask for; a file named
        /// anything else is a translation nobody will ever hear.</summary>
        [Theory]
        [MemberData(nameof(LocaleFiles))]
        public void TheFileIsNamedAfterALanguageTheGameShips(string fileName)
        {
            Assert.Contains(TranslationFiles.LanguageOf(fileName), TranslationFiles.Languages);
        }

        [Theory]
        [MemberData(nameof(LocaleFiles))]
        public void TheFileIsCleanUtf8(string fileName)
        {
            IList<string> problems = LocaleLint.EncodingProblems(
                File.ReadAllBytes(Path.Combine(TranslationFiles.LocaleDirectory(), fileName))
            );

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        /// <summary>The extra counted keys a language with more than two number forms adds are keys
        /// too, and this is the check that knows which files may carry them.</summary>
        [Theory]
        [MemberData(nameof(LocaleFiles))]
        public void EveryKeyIsOneTheModSpeaksInThisLanguage(string fileName)
        {
            string language = TranslationFiles.LanguageOf(fileName);
            Dictionary<string, string> table = Table(fileName);
            List<string> problems = new List<string>();
            problems.AddRange(
                LocaleLint.UnknownKeys(
                    table.Keys,
                    DefaultKeys(),
                    TranslationFiles.HasPaucal(language)
                        || TranslationFiles.SingularCoversLargerNumbers(language)
                )
            );
            problems.AddRange(LocaleLint.PlaceholderMismatches(table, English()));

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        /// <summary>
        /// A translation is all or nothing per key: a key it leaves out falls back to English, which
        /// is a sentence in the wrong language mid-phrase rather than an obvious hole. So a shipped
        /// file answers for every key, and the way to ship a half-finished translation is not to
        /// ship it.
        /// </summary>
        [Theory]
        [MemberData(nameof(LocaleFiles))]
        public void EveryTranslationAnswersEveryKeyTheModSpeaks(string fileName)
        {
            IList<string> problems = LocaleLint.MissingKeys(DefaultKeys(), Table(fileName).Keys);

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        [Theory]
        [MemberData(nameof(LocaleFiles))]
        public void AThreeFormLanguageCarriesEveryPaucal(string fileName)
        {
            if (!TranslationFiles.HasPaucal(TranslationFiles.LanguageOf(fileName)))
            {
                return;
            }

            IList<string> problems = LocaleLint.MissingPaucals(
                PluralPairs.Scan().ManyKeys,
                Table(fileName).Keys
            );

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        /// <summary>
        /// A language whose singular covers 21 as well as 1 needs a sentence for that case wherever
        /// the pair's singular has no number in it - Russian, of the languages the game ships.
        /// Without it the mod tells a Russian player that a twenty-one turn journey arrives this
        /// turn, which is not a grammar slip but a false statement.
        /// </summary>
        [Theory]
        [MemberData(nameof(LocaleFiles))]
        public void ALanguageWhoseSingularTakesLargerNumbersCarriesTheirSentences(string fileName)
        {
            if (!TranslationFiles.SingularCoversLargerNumbers(TranslationFiles.LanguageOf(fileName)))
            {
                return;
            }

            IList<string> problems = LocaleLint.MissingSemanticSingulars(
                PluralPairs.Scan().Pairs,
                English(),
                Table(fileName).Keys
            );

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        [Theory]
        [MemberData(nameof(LocaleFiles))]
        public void TheTranslationIsInTheLanguagesOwnScript(string fileName)
        {
            IList<string> problems = LocaleLint.ScriptProblems(
                Table(fileName),
                English(),
                TranslationFiles.ScriptFor(TranslationFiles.LanguageOf(fileName))
            );

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        [Theory]
        [MemberData(nameof(LocaleFiles))]
        public void NoEntryIsStillTheEnglishText(string fileName)
        {
            if (TranslationFiles.LanguageOf(fileName) == TranslationFiles.English)
            {
                return;
            }

            IList<string> problems = LocaleLint.UntranslatedEntries(Table(fileName), English());

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        /// <summary>
        /// The staleness check. Nothing at runtime can notice that a phrase was rewritten out from
        /// under a translation - both files still hold a sentence - so every non-English file
        /// carries a snapshot of the English it was made from, and a rewritten English template
        /// fails here until someone re-checks the translation and runs
        /// <c>tools\locale\mark-translated.ps1</c>.
        /// </summary>
        [Theory]
        [MemberData(nameof(LocaleFiles))]
        public void TheTranslationRecordsTheEnglishItWasMadeFrom(string fileName)
        {
            if (TranslationFiles.LanguageOf(fileName) == TranslationFiles.English)
            {
                return;
            }

            string snapshot = TranslationFiles.SnapshotPath(
                TranslationFiles.LocaleDirectory(),
                fileName
            );
            Assert.True(
                File.Exists(snapshot),
                fileName
                    + ": no locale\\sources\\"
                    + fileName
                    + "; run tools\\locale\\mark-translated.ps1 -Language "
                    + TranslationFiles.LanguageOf(fileName)
            );

            IList<string> problems = LocaleLint.SnapshotProblems(
                TranslationFiles.ReadTable(snapshot),
                Table(fileName),
                English()
            );

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        /// <summary>
        /// The template is checked in BOTH directions. A key the mod speaks and english.json omits
        /// is a phrase no translator is ever offered; a key english.json carries and the mod does
        /// not speak is work asked of every translator for nothing, and it is invisible to a
        /// one-way check.
        /// </summary>
        [Fact]
        public void TheEnglishTemplateIsExactlyTheKeysTheModSpeaks()
        {
            SortedSet<string> shipped = new SortedSet<string>(English().Keys);
            List<string> problems = new List<string>();
            problems.AddRange(LocaleLint.MissingKeys(DefaultKeys(), shipped));
            problems.AddRange(LocaleLint.UnknownKeys(shipped, DefaultKeys(), false));

            Assert.True(problems.Count == 0, TranslationFiles.Report("english.json", problems));
        }

        /// <summary>
        /// Every counted phrase must be reachable by the paucal check, and the one call site that
        /// hides its pair behind parameters is traced by hand. A second such site would be a plural
        /// pair silently exempt from that check, so it fails here until it is traced too.
        /// </summary>
        [Fact]
        public void EveryPluralPairIsAccountedFor()
        {
            PluralPairScan scan = PluralPairs.Scan();

            // A regex that quietly stopped matching would leave an empty set and every other check
            // in this file would still pass, so name one pair found each way: an ordinary literal
            // call site, and the one traced through AddShipCount's parameters.
            Assert.Contains(ModStrings.SystemSupplyingOutposts, scan.ManyKeys);
            Assert.Contains(ModStrings.GalaxySystemFriendlyShips, scan.ManyKeys);
            Assert.True(
                scan.ManyKeys.Count >= 10,
                "only " + scan.ManyKeys.Count + " plural pairs found; the scan has stopped working"
            );

            foreach (KeyValuePair<string, string> pair in scan.Pairs)
            {
                Assert.True(ModStrings.Has(pair.Key), "no such plural key: " + pair.Key);
                Assert.True(ModStrings.Has(pair.Value), "no such plural key: " + pair.Value);
            }

            Assert.Equal(
                new SortedSet<string>(PluralPairs.TracedSites),
                new SortedSet<string>(scan.IndirectSites)
            );
        }

        private static Dictionary<string, string> Table(string fileName)
        {
            return TranslationFiles.ReadTable(
                Path.Combine(TranslationFiles.LocaleDirectory(), fileName)
            );
        }

        private static Dictionary<string, string> English()
        {
            return Table("english.json");
        }

        private static SortedSet<string> DefaultKeys()
        {
            return new SortedSet<string>(ModStrings.DefaultKeys());
        }
    }
}

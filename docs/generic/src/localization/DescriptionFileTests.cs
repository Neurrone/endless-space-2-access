using System.Collections.Generic;
using System.IO;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The same lints, over the cutscene audio descriptions.
    ///
    /// They are the mod's own prose like the string table, but they are checked against a
    /// different reference: english.json is a TEMPLATE the mod could ship without, while
    /// descriptions/english.json is the table itself - there are no compiled-in descriptions, so
    /// English is a file and every other language is checked against it.
    ///
    /// The timings are the part a translation must not touch. They were measured against the
    /// footage, not against the words, so a cue that moved has dropped or invented a line rather
    /// than re-timed one.
    /// </summary>
    public class DescriptionFileTests
    {
        public static IEnumerable<object[]> DescriptionFiles()
        {
            foreach (string fileName in TranslationFiles.DescriptionFileNames())
            {
                yield return new object[] { fileName };
            }
        }

        [Fact]
        public void TheEnglishDescriptionsAreShipped()
        {
            Assert.True(
                File.Exists(
                    Path.Combine(TranslationFiles.DescriptionsDirectory(), "english.json")
                ),
                "descriptions/english.json is the table, not a fallback; nothing describes a "
                    + "cutscene without it"
            );
        }

        [Theory]
        [MemberData(nameof(DescriptionFiles))]
        public void TheFileIsNamedAfterALanguageTheGameShips(string fileName)
        {
            Assert.Contains(TranslationFiles.LanguageOf(fileName), TranslationFiles.Languages);
        }

        [Theory]
        [MemberData(nameof(DescriptionFiles))]
        public void TheFileIsCleanUtf8(string fileName)
        {
            IList<string> problems = LocaleLint.EncodingProblems(
                File.ReadAllBytes(Path.Combine(TranslationFiles.DescriptionsDirectory(), fileName))
            );

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        /// <summary>Applies to English too: a cue out of order is spoken at the wrong moment and
        /// swallows the one it jumped, whatever language wrote it.</summary>
        [Theory]
        [MemberData(nameof(DescriptionFiles))]
        public void EveryCueSaysSomethingAndWaitsItsTurn(string fileName)
        {
            IList<string> problems = LocaleLint.CueProblems(Table(fileName));

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        [Theory]
        [MemberData(nameof(DescriptionFiles))]
        public void TheSameVideosAreDescribedAtTheSameMoments(string fileName)
        {
            IList<string> problems = LocaleLint.DescriptionShapeProblems(
                Table(fileName),
                English()
            );

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        [Theory]
        [MemberData(nameof(DescriptionFiles))]
        public void TheCuesAreInTheLanguagesOwnScript(string fileName)
        {
            IList<string> problems = LocaleLint.ScriptProblems(
                LocaleLint.Flatten(Table(fileName)),
                LocaleLint.Flatten(English()),
                TranslationFiles.ScriptFor(TranslationFiles.LanguageOf(fileName))
            );

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        [Theory]
        [MemberData(nameof(DescriptionFiles))]
        public void NoCueIsStillTheEnglishText(string fileName)
        {
            if (TranslationFiles.LanguageOf(fileName) == TranslationFiles.English)
            {
                return;
            }

            IList<string> problems = LocaleLint.UntranslatedEntries(
                LocaleLint.Flatten(Table(fileName)),
                LocaleLint.Flatten(English())
            );

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        /// <summary>Descriptions go stale the same way translations do, and are re-marked by the
        /// same script with <c>-Descriptions</c>.</summary>
        [Theory]
        [MemberData(nameof(DescriptionFiles))]
        public void TheCuesRecordTheEnglishTheyWereMadeFrom(string fileName)
        {
            if (TranslationFiles.LanguageOf(fileName) == TranslationFiles.English)
            {
                return;
            }

            string snapshot = TranslationFiles.SnapshotPath(
                TranslationFiles.DescriptionsDirectory(),
                fileName
            );
            Assert.True(
                File.Exists(snapshot),
                fileName
                    + ": no descriptions\\sources\\"
                    + fileName
                    + "; run tools\\locale\\mark-translated.ps1 -Descriptions -Language "
                    + TranslationFiles.LanguageOf(fileName)
            );

            IList<string> problems = LocaleLint.SnapshotProblems(
                LocaleLint.Flatten(TranslationFiles.ReadDescriptionSnapshot(snapshot)),
                LocaleLint.Flatten(Table(fileName)),
                LocaleLint.Flatten(English())
            );

            Assert.True(problems.Count == 0, TranslationFiles.Report(fileName, problems));
        }

        private static Dictionary<string, IList<CueRow>> Table(string fileName)
        {
            return TranslationFiles.ReadDescriptions(
                Path.Combine(TranslationFiles.DescriptionsDirectory(), fileName)
            );
        }

        private static Dictionary<string, IList<CueRow>> English()
        {
            return Table("english.json");
        }
    }
}

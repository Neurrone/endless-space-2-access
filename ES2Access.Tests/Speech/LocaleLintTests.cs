using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The lints against deliberately broken input.
    ///
    /// The mod ships English only, so every file-driven test in
    /// <see cref="LocaleFileTests"/> and <see cref="DescriptionFileTests"/> passes over files that
    /// were never wrong - a suite of checks nobody has seen fail. These are the cases that prove
    /// each check would catch something: the exact byte sequences a mishandled encoding produces,
    /// a key that went missing, an English template rewritten under a translation, a Russian file
    /// still in Latin letters.
    ///
    /// Sample text is built from code points rather than written out, so this file stays ASCII -
    /// a test for mojibake that was itself carried by non-ASCII bytes would go wrong exactly when
    /// the thing it tests for happened to the repository.
    /// </summary>
    public class LocaleLintTests
    {
        [Fact]
        public void CleanUtf8HasNothingWrongWithIt()
        {
            Assert.Empty(LocaleLint.EncodingProblems(Utf8("{ \"a\": \"b\" }")));
            // Non-ASCII that is simply correct: a Cyrillic word and a Han character.
            Assert.Empty(LocaleLint.EncodingProblems(Utf8(Cyrillic() + Han())));
        }

        [Fact]
        public void AByteOrderMarkIsReported()
        {
            byte[] bytes = Concat(new byte[] { 0xEF, 0xBB, 0xBF }, Utf8("{}"));

            Assert.Contains("byte order mark", Only(LocaleLint.EncodingProblems(bytes)));
        }

        [Fact]
        public void BytesThatAreNotUtf8AreReported()
        {
            Assert.Contains(
                "not valid UTF-8",
                Only(LocaleLint.EncodingProblems(new byte[] { 0x7B, 0xFF, 0xFE, 0x7D }))
            );
        }

        [Fact]
        public void AReplacementCharacterIsReported()
        {
            // EF BF BD is U+FFFD - what a lossy conversion leaves where a character used to be.
            byte[] bytes = Concat(Utf8("ab"), new byte[] { 0xEF, 0xBF, 0xBD });

            Assert.Contains("U+FFFD", Only(LocaleLint.EncodingProblems(bytes)));
        }

        [Fact]
        public void AC1ControlIsReported()
        {
            // C2 85 is U+0085, which is what a Windows-1252 round trip makes of an ellipsis byte.
            byte[] bytes = Concat(Utf8("ab"), new byte[] { 0xC2, 0x85 });

            Assert.Contains("C1 control U+0085", Only(LocaleLint.EncodingProblems(bytes)));
        }

        /// <summary>The classic double encoding: "e acute" written as UTF-8, read back as Latin-1,
        /// and written as UTF-8 again, which is the four bytes C3 83 C2 A9.</summary>
        [Fact]
        public void MojibakeIsReported()
        {
            byte[] bytes = Concat(Utf8("caf"), new byte[] { 0xC3, 0x83, 0xC2, 0xA9 });

            Assert.Contains("doubly encoded", Only(LocaleLint.EncodingProblems(bytes)));
        }

        [Fact]
        public void AKeyTheModDoesNotSpeakIsReported()
        {
            IList<string> problems = LocaleLint.UnknownKeys(
                new[] { "a.key", "a.typo" },
                Keys("a.key"),
                false
            );

            Assert.Contains("unknown key 'a.typo'", Only(problems));
        }

        [Fact]
        public void APaucalKeyIsAcceptedOnlyWhereTheLanguageHasOne()
        {
            Assert.Empty(
                LocaleLint.UnknownKeys(new[] { "a.many.few" }, Keys("a.many"), true)
            );
            Assert.Contains(
                "a paucal form",
                Only(LocaleLint.UnknownKeys(new[] { "a.many.few" }, Keys("a.many"), false))
            );
        }

        [Fact]
        public void APaucalOfAKeyTheModDoesNotSpeakIsStillUnknown()
        {
            Assert.Contains(
                "unknown key 'b.many.few'",
                Only(LocaleLint.UnknownKeys(new[] { "b.many.few" }, Keys("a.many"), true))
            );
        }

        [Fact]
        public void AKeyTheFileNeverAnswersIsReported()
        {
            IList<string> problems = LocaleLint.MissingKeys(
                new[] { "a.key", "b.key" },
                Keys("a.key")
            );

            Assert.Contains("missing key 'b.key'", Only(problems));
        }

        [Fact]
        public void AMissingPaucalIsReported()
        {
            Assert.Empty(LocaleLint.MissingPaucals(new[] { "a.many" }, Keys("a.many.few")));
            Assert.Contains(
                "missing paucal form 'a.many.few'",
                Only(LocaleLint.MissingPaucals(new[] { "a.many" }, Keys("a.many")))
            );
        }

        [Fact]
        public void ASingularFormKeyIsAcceptedOnlyWhereTheLanguageHasOne()
        {
            Assert.Empty(LocaleLint.UnknownKeys(new[] { "a.many.one" }, Keys("a.many"), true));
            Assert.Contains(
                "a singular form for a larger number",
                Only(LocaleLint.UnknownKeys(new[] { "a.many.one" }, Keys("a.many"), false))
            );
        }

        [Fact]
        public void ASingularFormOfAKeyTheModDoesNotSpeakIsStillUnknown()
        {
            Assert.Contains(
                "unknown key 'b.many.one'",
                Only(LocaleLint.UnknownKeys(new[] { "b.many.one" }, Keys("a.many"), true))
            );
        }

        /// <summary>
        /// The singular form is owed only where the pair's singular sentence has nowhere to put the
        /// number: "Arrives this turn" cannot stand in for twenty-one turns, while "{0} outpost"
        /// can.
        /// </summary>
        [Fact]
        public void AMissingSingularFormIsReportedOnlyForAPairThatNeedsOne()
        {
            Dictionary<string, string> english = new Dictionary<string, string>
            {
                { "a.turns", "Arrives in {0} turns" },
                { "a.this-turn", "Arrives this turn" },
                { "b.many", "{0} outposts" },
                { "b.one", "{0} outpost" },
            };
            Dictionary<string, string> pairs = new Dictionary<string, string>
            {
                { "a.turns", "a.this-turn" },
                { "b.many", "b.one" },
            };

            Assert.Contains(
                "missing singular form 'a.turns.one'",
                Only(LocaleLint.MissingSemanticSingulars(pairs, english, Keys("b.many")))
            );
            Assert.Empty(
                LocaleLint.MissingSemanticSingulars(pairs, english, Keys("a.turns.one"))
            );
        }

        [Fact]
        public void APairWhoseSingularCarriesTheNumberIsNotSemantic()
        {
            Assert.True(LocaleLint.IsSemanticPair("Arrives this turn", "Arrives in {0} turns"));
            Assert.False(LocaleLint.IsSemanticPair("{0} outpost", "{0} outposts"));
            Assert.True(
                LocaleLint.IsSemanticPair("En route to {0} this turn", "En route to {0} in {1} turns")
            );
        }

        [Fact]
        public void APlaceholderTheTranslationDroppedOrInventedIsReported()
        {
            Dictionary<string, string> english = Table("a.key", "{0} of {1}");

            Assert.Empty(
                LocaleLint.PlaceholderMismatches(Table("a.key", "{1} in {0}"), english)
            );
            Assert.Contains(
                "English takes {0}, {1}, translation takes {0}",
                Only(LocaleLint.PlaceholderMismatches(Table("a.key", "{0} only"), english))
            );
        }

        /// <summary>A paucal form is measured against its pair's English sentence; nothing else
        /// carries the placeholders it has to keep.</summary>
        [Fact]
        public void APaucalFormIsCheckedAgainstItsPairsEnglish()
        {
            Dictionary<string, string> english = Table("a.many", "{0} things");

            Assert.Empty(
                LocaleLint.PlaceholderMismatches(Table("a.many.few", "{0} rzeczy"), english)
            );
            Assert.Contains(
                "'a.many.few'",
                Only(LocaleLint.PlaceholderMismatches(Table("a.many.few", "rzeczy"), english))
            );
        }

        [Fact]
        public void AnEntryLeftInEnglishIsReported()
        {
            Dictionary<string, string> english = Table("a.key", "Colonize this planet");

            Assert.Contains(
                "still the English text",
                Only(LocaleLint.UntranslatedEntries(Table("a.key", "Colonize this planet"), english))
            );
        }

        /// <summary>Short entries are exempt: "Ctrl" is the right answer in every language.</summary>
        [Fact]
        public void AShortEntryThatMatchesEnglishIsLeftAlone()
        {
            Assert.Empty(
                LocaleLint.UntranslatedEntries(Table("a.key", "Ctrl"), Table("a.key", "Ctrl"))
            );
        }

        [Fact]
        public void ARussianEntryInLatinLettersIsReported()
        {
            Dictionary<string, string> english = Table("a.key", "Colonize this planet");

            Assert.Contains(
                "has no Cyrillic in it",
                Only(
                    LocaleLint.ScriptProblems(
                        Table("a.key", "Kolonizirovat etu planetu"),
                        english,
                        NativeScript.Cyrillic
                    )
                )
            );
            Assert.Empty(
                LocaleLint.ScriptProblems(
                    Table("a.key", Cyrillic() + " " + Cyrillic() + " " + Cyrillic()),
                    english,
                    NativeScript.Cyrillic
                )
            );
        }

        /// <summary>A Latin-script language cannot be checked this way, and is not.</summary>
        [Fact]
        public void ALatinLanguageIsNeverAccusedOfBeingInTheWrongScript()
        {
            Assert.Empty(
                LocaleLint.ScriptProblems(
                    Table("a.key", "Coloniser cette planete"),
                    Table("a.key", "Colonize this planet"),
                    NativeScript.None
                )
            );
        }

        /// <summary>
        /// The per-file half of the script check. Every entry here is one short English word, so
        /// none of them trips the per-entry rule - and a file where four fifths of the words never
        /// changed alphabet is still a file nobody translated.
        /// </summary>
        [Fact]
        public void AFileTranslatedOnlyInPatchesIsReported()
        {
            Dictionary<string, string> english = new Dictionary<string, string>();
            Dictionary<string, string> korean = new Dictionary<string, string>();
            for (int i = 0; i < 10; i++)
            {
                string key = "key." + i;
                english[key] = "button";
                korean[key] = i < 5 ? Hangul() : "button";
            }

            Assert.Contains(
                "only 5 of 10 worded entries are in Hangul",
                Only(LocaleLint.ScriptProblems(korean, english, NativeScript.Hangul))
            );
        }

        [Fact]
        public void HanCountsForBothChineseLocales()
        {
            Assert.True(LocaleLint.HasScript(Han(), NativeScript.Han));
            Assert.False(LocaleLint.HasScript("Colonize", NativeScript.Han));
            Assert.False(LocaleLint.HasScript(Cyrillic(), NativeScript.Han));
        }

        [Fact]
        public void ARewrittenEnglishTemplateMakesTheTranslationStale()
        {
            IList<string> problems = LocaleLint.SnapshotProblems(
                Table("a.key", "Colonize this planet"),
                Table("a.key", "Coloniser cette planete"),
                Table("a.key", "Colonize this world")
            );

            Assert.Contains("is stale", Only(problems));
            Assert.Contains("English now reads \"Colonize this world\"", Only(problems));
        }

        [Fact]
        public void ATranslationWithNoRecordOfItsEnglishIsReported()
        {
            IList<string> problems = LocaleLint.SnapshotProblems(
                new Dictionary<string, string>(),
                Table("a.key", "Coloniser"),
                Table("a.key", "Colonize")
            );

            Assert.Contains("no record of the English", Only(problems));
        }

        [Fact]
        public void ARecordOfSomethingNobodyTranslatedIsReported()
        {
            IList<string> problems = LocaleLint.SnapshotProblems(
                Table("a.key", "Colonize"),
                new Dictionary<string, string>(),
                Table("a.key", "Colonize")
            );

            Assert.Contains("recorded but not translated", Only(problems));
        }

        [Fact]
        public void AFreshSnapshotIsClean()
        {
            Assert.Empty(
                LocaleLint.SnapshotProblems(
                    Table("a.key", "Colonize"),
                    Table("a.key", "Coloniser"),
                    Table("a.key", "Colonize")
                )
            );
        }

        [Fact]
        public void ADescriptionThatDroppedOrMovedACueIsReported()
        {
            Dictionary<string, IList<CueRow>> english = Movie("Arctic", Cue(1, 4, "a"), Cue(4, 8, "b"));

            Assert.Empty(
                LocaleLint.DescriptionShapeProblems(
                    Movie("Arctic", Cue(1, 4, "x"), Cue(4, 8, "y")),
                    english
                )
            );
            Assert.Contains(
                "has 1 cues, English has 2",
                Only(
                    LocaleLint.DescriptionShapeProblems(Movie("Arctic", Cue(1, 4, "x")), english)
                )
            );
            Assert.Contains(
                "cue 1 runs 5-8, English runs 4-8",
                Only(
                    LocaleLint.DescriptionShapeProblems(
                        Movie("Arctic", Cue(1, 4, "x"), Cue(5, 8, "y")),
                        english
                    )
                )
            );
        }

        [Fact]
        public void ADescriptionForAVideoEnglishDoesNotCoverIsReported()
        {
            IList<string> problems = LocaleLint.DescriptionShapeProblems(
                Movie("Swamp", Cue(1, 4, "x")),
                Movie("Arctic", Cue(1, 4, "a"))
            );

            Assert.Contains("'Arctic' is described in English and not here", problems);
            Assert.Contains("'Swamp' is not a video English describes", problems);
        }

        [Fact]
        public void AnEmptyOrBackwardsCueIsReported()
        {
            Assert.Contains(
                "cue 1 has no text",
                Only(LocaleLint.CueProblems(Movie("Arctic", Cue(1, 4, "a"), Cue(4, 8, " "))))
            );
            Assert.Contains(
                "cue 1 starts at 0.5, after a cue at 1",
                Only(LocaleLint.CueProblems(Movie("Arctic", Cue(1, 4, "a"), Cue(0.5, 8, "b"))))
            );
        }

        [Fact]
        public void FlatteningLinesUpCuesWithTheirEnglish()
        {
            IDictionary<string, string> flat = LocaleLint.Flatten(
                Movie("Arctic", Cue(1, 4, "a"), Cue(4, 8, "b"))
            );

            Assert.Equal("a", flat["Arctic[0]"]);
            Assert.Equal("b", flat["Arctic[1]"]);
        }

        [Fact]
        public void APaucalKeysBaseIsThePairItBelongsTo()
        {
            Assert.Equal("a.many", LocaleLint.BaseKey("a.many.few"));
            Assert.Equal("a.many", LocaleLint.BaseKey("a.many.one"));
            Assert.Equal("a.many", LocaleLint.BaseKey("a.many"));
            Assert.False(LocaleLint.IsPaucal(".few"));
            Assert.False(LocaleLint.IsSingular(".one"));
        }

        [Fact]
        public void OnlyTokensWithLettersCountAsWords()
        {
            Assert.Equal(3, LocaleLint.Words("Colonize this planet"));
            Assert.Equal(2, LocaleLint.Words("{0} of {1} turns"));
            Assert.Equal(0, LocaleLint.Words("12 34"));
        }

        private static string Only(IList<string> problems)
        {
            Assert.NotEmpty(problems);
            return string.Join(" | ", problems);
        }

        private static SortedSet<string> Keys(params string[] keys)
        {
            return new SortedSet<string>(keys);
        }

        private static Dictionary<string, string> Table(string key, string value)
        {
            return new Dictionary<string, string> { { key, value } };
        }

        private static Dictionary<string, IList<CueRow>> Movie(string name, params CueRow[] cues)
        {
            return new Dictionary<string, IList<CueRow>> { { name, new List<CueRow>(cues) } };
        }

        private static CueRow Cue(double at, double end, string text)
        {
            return new CueRow { At = at, End = end, Text = text };
        }

        private static byte[] Utf8(string text)
        {
            return new UTF8Encoding(false).GetBytes(text);
        }

        private static byte[] Concat(byte[] first, byte[] second)
        {
            byte[] joined = new byte[first.Length + second.Length];
            first.CopyTo(joined, 0);
            second.CopyTo(joined, first.Length);
            return joined;
        }

        // "Da" in Russian, U+0414 U+0430.
        private static string Cyrillic()
        {
            return new string(new[] { (char)0x0414, (char)0x0430 });
        }

        // U+D55C U+AD6D, the Korean name for Korea.
        private static string Hangul()
        {
            return new string(new[] { (char)0xD55C, (char)0xAD6D });
        }

        // U+661F U+7403, "planet" in Chinese.
        private static string Han()
        {
            return new string(new[] { (char)0x661F, (char)0x7403 });
        }
    }
}

using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The safety-net behaviour: whatever a translation file contains, the mod still speaks
    /// something and never throws into the game's frame.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class ModStringsTests
    {
        private readonly List<string> _warnings = new List<string>();

        public ModStringsTests()
        {
            ModStrings.Reset();
            Log.Install(null, _warnings.Add, null);
        }

        [Fact]
        public void DefaultsAreEnglish()
        {
            Assert.Equal(
                "Endless Space 2 Access 1.2.3 ready",
                ModStrings.Format(ModStrings.StartupReady, "1.2.3")
            );
            Assert.Equal("5 of 20", ModStrings.Format(ModStrings.Fraction, 5, 20));
        }

        /// <summary>A collection threshold on the population overview says the whole thing - the number
        /// it takes and whether the empire is there yet - in one phrase per state, so a translator can
        /// inflect the sentence rather than being handed a number with a word glued after it.</summary>
        [Fact]
        public void CollectionThresholdsAreWholePhrases()
        {
            Assert.Equal(
                "10 population, not reached",
                ModStrings.Format(ModStrings.PopulationThresholdNotReached, "10")
            );
            Assert.Equal(
                "10 population, reached",
                ModStrings.Format(ModStrings.PopulationThresholdReached, "10")
            );
        }

        [Fact]
        public void UnknownKeyReturnsTheKeyAndWarnsOnce()
        {
            Assert.Equal("no.such.key", ModStrings.Get("no.such.key"));
            Assert.Equal("no.such.key", ModStrings.Get("no.such.key"));
            Assert.Single(_warnings);
        }

        [Fact]
        public void InstalledTranslationOverridesOnlyTheKeysItContains()
        {
            ModStrings.Install(
                new Dictionary<string, string> { { ModStrings.StartupReady, "prêt" } }
            );

            Assert.Equal("prêt", ModStrings.Get(ModStrings.StartupReady));
            Assert.Equal(", ", ModStrings.Get(ModStrings.ListSeparator));
        }

        [Fact]
        public void InstallNullRestoresTheDefaults()
        {
            ModStrings.Install(
                new Dictionary<string, string> { { ModStrings.StartupReady, "prêt" } }
            );
            ModStrings.Install(null);

            Assert.Equal(
                "Endless Space 2 Access {0} ready",
                ModStrings.Get(ModStrings.StartupReady)
            );
        }

        [Fact]
        public void InstallEmptyRestoresTheDefaults()
        {
            ModStrings.Install(
                new Dictionary<string, string> { { ModStrings.StartupReady, "prêt" } }
            );
            ModStrings.Install(new Dictionary<string, string>());

            Assert.Equal(
                "Endless Space 2 Access {0} ready",
                ModStrings.Get(ModStrings.StartupReady)
            );
        }

        [Fact]
        public void BrokenTranslationTemplateFallsBackToEnglishAndWarnsOnce()
        {
            ModStrings.Install(
                new Dictionary<string, string> { { ModStrings.Fraction, "{5} sur {1}" } }
            );

            Assert.Equal("5 of 20", ModStrings.Format(ModStrings.Fraction, 5, 20));
            Assert.Equal("3 of 4", ModStrings.Format(ModStrings.Fraction, 3, 4));
            Assert.Single(_warnings);
        }

        [Fact]
        public void FormatOfAnUnknownKeyReturnsTheKeyWithoutThrowing()
        {
            Assert.Equal("no.such.key", ModStrings.Format("no.such.key", 1, 2));
        }

        [Fact]
        public void TranslationMayDropPlaceholders()
        {
            ModStrings.Install(
                new Dictionary<string, string> { { ModStrings.Quantity, "several" } }
            );

            Assert.Equal("several", ModStrings.Format(ModStrings.Quantity, 5));
            Assert.Empty(_warnings);
        }

        [Fact]
        public void ACountedPhrasePicksTheFormItsNumberCallsFor()
        {
            Assert.Equal(
                "Supplying 1 outpost",
                ModStrings.Plural(
                    ModStrings.SystemSupplyingOutpost,
                    ModStrings.SystemSupplyingOutposts,
                    1
                )
            );
            Assert.Equal(
                "Supplying 3 outposts",
                ModStrings.Plural(
                    ModStrings.SystemSupplyingOutpost,
                    ModStrings.SystemSupplyingOutposts,
                    3
                )
            );
        }

        [Fact]
        public void ALanguageWithOneFormWritesTheSameSentenceInBoth()
        {
            ModStrings.Install(
                new Dictionary<string, string>
                {
                    { ModStrings.SystemSupplyingOutpost, "{0}個の前哨基地に供給中" },
                    { ModStrings.SystemSupplyingOutposts, "{0}個の前哨基地に供給中" },
                }
            );

            Assert.Equal(
                "1個の前哨基地に供給中",
                ModStrings.Plural(
                    ModStrings.SystemSupplyingOutpost,
                    ModStrings.SystemSupplyingOutposts,
                    1
                )
            );
            Assert.Equal(
                "4個の前哨基地に供給中",
                ModStrings.Plural(
                    ModStrings.SystemSupplyingOutpost,
                    ModStrings.SystemSupplyingOutposts,
                    4
                )
            );
        }

        /// <summary>
        /// The third form is carried by the locale file, not by a call site: a Russian translation
        /// adds "&lt;manyKey&gt;.few" and the same two-key call starts picking it up.
        /// </summary>
        [Fact]
        public void AThreeFormLanguageTakesItsPaucalFromTheLocaleFile()
        {
            InstallRussian(
                new Dictionary<string, string>
                {
                    { ModStrings.SystemSupplyingOutpost, "one {0}" },
                    { ModStrings.SystemSupplyingOutposts, "many {0}" },
                    { ModStrings.SystemSupplyingOutposts + PluralRules.FewSuffix, "few {0}" },
                }
            );

            Assert.Equal("one 1", Supplying(1));
            Assert.Equal("one 21", Supplying(21));
            Assert.Equal("few 2", Supplying(2));
            Assert.Equal("few 22", Supplying(22));
            Assert.Equal("many 5", Supplying(5));
            Assert.Equal("many 12", Supplying(12));
            Assert.Empty(_warnings);
        }

        /// <summary>A three-form language whose file has not been given the paucal yet is
        /// grammatically wrong, not silent - it hears the MANY sentence.</summary>
        [Fact]
        public void AMissingPaucalFallsBackToTheManyForm()
        {
            InstallRussian(
                new Dictionary<string, string>
                {
                    { ModStrings.SystemSupplyingOutpost, "one {0}" },
                    { ModStrings.SystemSupplyingOutposts, "many {0}" },
                }
            );

            Assert.Equal("many 2", Supplying(2));
            Assert.Equal("one 21", Supplying(21));
            Assert.Empty(_warnings);
        }

        /// <summary>A two-form language never asks for the paucal, so a ".few" key sitting in its
        /// file changes nothing.</summary>
        [Fact]
        public void ATwoFormLanguageIgnoresAPaucalKey()
        {
            ModStrings.Install(
                new Dictionary<string, string>
                {
                    { ModStrings.SystemSupplyingOutpost, "one {0}" },
                    { ModStrings.SystemSupplyingOutposts, "many {0}" },
                    { ModStrings.SystemSupplyingOutposts + PluralRules.FewSuffix, "few {0}" },
                },
                "french"
            );

            Assert.Equal("many 2", Supplying(2));
            Assert.Equal("one 0", Supplying(0));
        }

        /// <summary>The plural rule follows the LANGUAGE, so a language with no file of its own
        /// still counts its own way over the English sentences.</summary>
        [Fact]
        public void ALanguageWithNoFileKeepsItsOwnPluralRule()
        {
            ModStrings.Install(null, "russian");

            Assert.Equal("Supplying 21 outpost", Supplying(21));
            Assert.Equal("Supplying 22 outposts", Supplying(22));
        }

        private static void InstallRussian(Dictionary<string, string> table)
        {
            ModStrings.Install(table, "russian");
        }

        private static string Supplying(int count)
        {
            return ModStrings.Plural(
                ModStrings.SystemSupplyingOutpost,
                ModStrings.SystemSupplyingOutposts,
                count
            );
        }
    }
}

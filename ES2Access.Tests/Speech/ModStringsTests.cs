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
            Assert.Equal("Endless Space 2 Access ready", ModStrings.Get(ModStrings.StartupReady));
            Assert.Equal("5 of 20", ModStrings.Format(ModStrings.Fraction, 5, 20));
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

            Assert.Equal("Endless Space 2 Access ready", ModStrings.Get(ModStrings.StartupReady));
        }

        [Fact]
        public void InstallEmptyRestoresTheDefaults()
        {
            ModStrings.Install(
                new Dictionary<string, string> { { ModStrings.StartupReady, "prêt" } }
            );
            ModStrings.Install(new Dictionary<string, string>());

            Assert.Equal("Endless Space 2 Access ready", ModStrings.Get(ModStrings.StartupReady));
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
    }
}

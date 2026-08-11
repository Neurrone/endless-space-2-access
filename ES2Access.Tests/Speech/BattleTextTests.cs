using System.Collections.Generic;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// What a battle says in the mod's own words. The load-bearing rule is the SILENCE: a narrator
    /// written against a phrase the build does not carry must say nothing rather than read the key
    /// aloud, because a battle cinematic gives the player no way to go back and work out what they
    /// just heard.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class BattleTextTests
    {
        public BattleTextTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void APhraseTheBuildDoesNotCarryIsSilent()
        {
            Assert.Null(BattleText.Optional("battle.no-such-phrase"));
            Assert.Null(BattleText.Optional("battle.no-such-phrase", 3));
            Assert.Null(BattleText.Optional(null));
            Assert.Null(BattleText.Optional(""));
        }

        [Fact]
        public void APhraseTheBuildCarriesSpeaks()
        {
            Install("battle.progress", "Battle {0} percent fought");

            Assert.Equal("Battle 50 percent fought", BattleText.Optional("battle.progress", 50));
        }

        [Fact]
        public void ACountedPhraseTakesTheFormItsNumberCallsFor()
        {
            Install(
                "battle.your-ship-lost",
                "{0} of your ships lost",
                "battle.your-ships-lost",
                "{0} of your ships lost"
            );

            Assert.Equal(
                "1 of your ships lost",
                BattleText.Counted("battle.your-ship-lost", "battle.your-ships-lost", 1)
            );
            Assert.Equal(
                "4 of your ships lost",
                BattleText.Counted("battle.your-ship-lost", "battle.your-ships-lost", 4)
            );
        }

        [Fact]
        public void OneLossIsNamedAndSeveralAreCounted()
        {
            Install("battle.your-ship-lost", "Your {0} is lost", "battle.your-ships-lost", "{0} of your ships lost");

            Assert.Equal(
                "Your Vanguard is lost",
                BattleText.Losses(
                    new List<string> { "Vanguard" },
                    "battle.your-ship-lost",
                    "battle.your-ships-lost"
                )
            );
            Assert.Equal(
                "3 of your ships lost",
                BattleText.Losses(
                    new List<string> { "Vanguard", "Patrol", "Settler" },
                    "battle.your-ship-lost",
                    "battle.your-ships-lost"
                )
            );
        }

        [Fact]
        public void NothingLostSaysNothing()
        {
            Install("battle.your-ship-lost", "Your {0} is lost", "battle.your-ships-lost", "{0} of your ships lost");

            Assert.Null(BattleText.Losses(null, "battle.your-ship-lost", "battle.your-ships-lost"));
            Assert.Null(
                BattleText.Losses(
                    new List<string>(),
                    "battle.your-ship-lost",
                    "battle.your-ships-lost"
                )
            );
        }

        /// <summary>A single loss the game had no name for: there is nothing to say about it, and the
        /// count phrase would say "1 of your ships lost" for something the player can already hear
        /// exploding.</summary>
        [Fact]
        public void ASingleNamelessLossIsSilent()
        {
            Install("battle.your-ship-lost", "Your {0} is lost", "battle.your-ships-lost", "{0} of your ships lost");

            Assert.Null(
                BattleText.Losses(
                    new List<string> { null },
                    "battle.your-ship-lost",
                    "battle.your-ships-lost"
                )
            );
        }

        private static void Install(params string[] pairs)
        {
            Dictionary<string, string> strings = new Dictionary<string, string>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                strings[pairs[i]] = pairs[i + 1];
            }

            ModStrings.Install(strings);
        }
    }
}

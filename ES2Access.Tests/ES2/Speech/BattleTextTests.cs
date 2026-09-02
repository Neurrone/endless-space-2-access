using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.ES2.Speech;
using ES2Access.Tests.Speech;
using Xunit;
using static ES2Access.Tests.Speech.ModStringsFixture;

namespace ES2Access.Tests.ES2.Speech
{
    /// <summary>
    /// What a battle says in the mod's own words. Every one of these phrases is asked for optionally
    /// (<see cref="OptionalText.Phrase"/>, whose own silence rule is tested beside it), because a
    /// narrator written against a phrase the build does not carry must say nothing rather than read the
    /// key aloud: a battle cinematic gives the player no way to go back and work out what they just
    /// heard.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class BattleTextTests
    {
        public BattleTextTests()
        {
            ModStrings.Reset();
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

        /// <summary>Each shape the tallies can take picks a DIFFERENT whole sentence - the count and
        /// the damage kind are inside the clause, so there is no stem to glue them onto.</summary>
        [Theory]
        [InlineData(1, 0, 40, 0, 0, 0, "Prowler hit Endeavor: 40 energy damage")]
        [InlineData(3, 0, 40, 0, 0, 0, "Prowler hit Endeavor 3 times: 40 energy damage")]
        [InlineData(1, 0, 0, 25, 0, 0, "Prowler hit Endeavor: 25 projectile damage")]
        [InlineData(2, 0, 0, 25, 0, 0, "Prowler hit Endeavor 2 times: 25 projectile damage")]
        [InlineData(2, 0, 40, 25, 0, 0, "Prowler hit Endeavor 2 times: 40 energy damage and 25 projectile damage")]
        [InlineData(1, 0, 0, 0, 12, 0, "Prowler hit Endeavor: 12 damage")]
        [InlineData(1, 0, 40, 0, 12, 0, "Prowler hit Endeavor: 52 damage")]
        public void EachShapeOfExchangeHasItsOwnSentence(
            int hits,
            int misses,
            int energy,
            int projectile,
            int untyped,
            int absorbed,
            string expected
        )
        {
            Assert.Equal(expected, BattleText.Volley(Volley(hits, misses, energy, projectile, untyped, absorbed)));
        }

        /// <summary>Shots that missed alongside shots that landed are a clause on the same sentence,
        /// and shots that ALL missed are a sentence of their own - there is no damage figure for them
        /// to hang off.</summary>
        [Fact]
        public void MissesAreAClauseUntilThereIsNothingElseToSay()
        {
            Assert.Equal(
                "Prowler hit Endeavor 2 times: 40 energy damage, missed",
                BattleText.Volley(Volley(2, 1, 40, 0, 0, 0))
            );
            Assert.Equal(
                "Prowler hit Endeavor: 40 energy damage, missed 3 times",
                BattleText.Volley(Volley(1, 3, 40, 0, 0, 0))
            );
            Assert.Equal("Prowler missed Endeavor", BattleText.Volley(Volley(0, 1, 0, 0, 0, 0)));
            Assert.Equal(
                "Prowler missed Endeavor 4 times",
                BattleText.Volley(Volley(0, 4, 0, 0, 0, 0))
            );
        }

        /// <summary>Damage the shields ate is reported beside what got through, and REPLACES it where
        /// nothing got through at all - which is the whole point of the line: the player heard the
        /// shot land and needs telling it did nothing.</summary>
        [Fact]
        public void ShieldAbsorptionIsAClauseUntilNothingGetsThrough()
        {
            Assert.Equal(
                "Prowler hit Endeavor: 40 energy damage, 15 absorbed by shields",
                BattleText.Volley(Volley(1, 0, 40, 0, 0, 15))
            );
            Assert.Equal(
                "Prowler hit Endeavor: fully absorbed by shields",
                BattleText.Volley(Volley(1, 0, 0, 0, 0, 15))
            );
            Assert.Equal(
                "Prowler hit Endeavor 3 times: fully absorbed by shields",
                BattleText.Volley(Volley(3, 0, 0, 0, 0, 15))
            );
        }

        [Fact]
        public void AnExchangeWithNothingToNameIsSilent()
        {
            Assert.Null(BattleText.Volley(null));
            Assert.Null(
                BattleText.Volley(new FireWatch.Volley { Attacker = "Prowler", Hits = 1 })
            );
            Assert.Null(
                BattleText.Volley(new FireWatch.Volley { Target = "Endeavor", Hits = 1 })
            );
        }

        private static FireWatch.Volley Volley(
            int hits,
            int misses,
            int energy,
            int projectile,
            int untyped,
            int absorbed
        )
        {
            return new FireWatch.Volley
            {
                Attacker = "Prowler",
                Target = "Endeavor",
                Hits = hits,
                Misses = misses,
                Energy = energy,
                Projectile = projectile,
                Untyped = untyped,
                Absorbed = absorbed,
            };
        }
    }
}

using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Which subcategories a thing belongs to, and how a table of overlapping subcategories is
    /// counted. Every failure here is inaudible in the same way the cursor's are: a homeworld quietly
    /// missing from its own scope, or a nebula quietly padding "neutral", sounds exactly like a
    /// galaxy that is shaped that way.
    /// </summary>
    public class ScannerScopesTests
    {
        [Fact]
        public void EverythingIsInAll()
        {
            Assert.True(ScannerScopes.Holds(ScannerScopes.Only(), ScannerScopes.All));
            Assert.True(
                ScannerScopes.Holds(
                    ScannerScopes.Owned(ScannerScopes.Enemy),
                    ScannerScopes.All
                )
            );
            Assert.True(
                ScannerScopes.Holds(
                    ScannerScopes.System(ScannerScopes.Neutral, false, false, false),
                    ScannerScopes.All
                )
            );
        }

        [Fact]
        public void ASpecialNodeIsSpecialAndNothingElse()
        {
            int scopes = ScannerScopes.System(ScannerScopes.Neutral, true, false, false);
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.Special));
            // Not neutral: the affiliation trio is about who HOLDS a place, and nobody holds a nebula.
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Neutral));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Friendly));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Enemy));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Homeworld));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.MinorFaction));
        }

        [Fact]
        public void AMinorFactionSystemStaysNeutralAsWell()
        {
            // The affiliation trio answers how the player STANDS to whoever holds a place, and that
            // answer is still neutral. "Minor factions" is an ownership filter laid over it, not a
            // fourth affiliation - so the two scopes overlap by design and the same system is found
            // by either.
            int scopes = ScannerScopes.System(ScannerScopes.Neutral, false, false, true);
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.Neutral));
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.MinorFaction));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Special));
        }

        [Fact]
        public void AMinorFactionCapitalIsNeutralAndMinorAndHomeworldAtOnce()
        {
            int scopes = ScannerScopes.System(ScannerScopes.Neutral, false, true, true);
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.All));
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.Neutral));
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.Homeworld));
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.MinorFaction));
        }

        [Fact]
        public void AnEnemyCapitalIsBothEnemyAndHomeworld()
        {
            int scopes = ScannerScopes.System(ScannerScopes.Enemy, false, true, false);
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.Enemy));
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.Homeworld));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Special));
        }

        [Fact]
        public void AFleetIsOnlyEverItsAffiliation()
        {
            int scopes = ScannerScopes.Owned(ScannerScopes.Friendly);
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.Friendly));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Neutral));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Homeworld));
        }

        [Fact]
        public void AThingIsCountedOnceInEverySubcategoryItIsIn()
        {
            // The fixture galaxy after the taxonomy grew: twelve star systems - the player's own two,
            // one of which is their capital, and ten neutral, one of those ten a minor faction's -
            // plus one special node.
            int[] scopes = new int[13];
            scopes[0] = ScannerScopes.System(ScannerScopes.Friendly, false, true, false);
            scopes[1] = ScannerScopes.System(ScannerScopes.Friendly, false, false, false);
            for (int i = 2; i < 12; i++)
            {
                scopes[i] = ScannerScopes.System(ScannerScopes.Neutral, false, false, false);
            }

            scopes[11] = ScannerScopes.System(ScannerScopes.Neutral, false, true, true);
            scopes[12] = ScannerScopes.System(ScannerScopes.Neutral, true, false, false);

            int[] row = ScannerScopes.Tally(scopes, ScannerScopes.SystemWidth);
            Assert.Equal(13, row[ScannerScopes.All]);
            Assert.Equal(2, row[ScannerScopes.Friendly]);
            // The minor faction's system is STILL counted in neutral.
            Assert.Equal(10, row[ScannerScopes.Neutral]);
            Assert.Equal(0, row[ScannerScopes.Enemy]);
            Assert.Equal(2, row[ScannerScopes.Homeworld]);
            Assert.Equal(1, row[ScannerScopes.MinorFaction]);
            Assert.Equal(1, row[ScannerScopes.Special]);

            // "All" is the size of the category, NOT the sum of the scopes below it - the capitals
            // and the minor faction are counted twice and the special node is counted in neither
            // trio.
            Assert.NotEqual(
                row[ScannerScopes.All],
                row[ScannerScopes.Friendly]
                    + row[ScannerScopes.Neutral]
                    + row[ScannerScopes.Enemy]
            );
        }

        [Fact]
        public void ACategoryAskedOnlyWhatIsThereHasOneColumn()
        {
            int[] row = ScannerScopes.Tally(
                new int[] { ScannerScopes.Only(), ScannerScopes.Only() },
                ScannerScopes.SingleWidth
            );
            Assert.Single(row);
            Assert.Equal(2, row[ScannerScopes.All]);
        }

        [Fact]
        public void AnEmptyCategoryTalliesToZeroesRatherThanToNothing()
        {
            int[] row = ScannerScopes.Tally(null, ScannerScopes.AffiliationWidth);
            Assert.Equal(ScannerScopes.AffiliationWidth, row.Length);
            Assert.Equal(0, row[ScannerScopes.All]);
        }

        [Fact]
        public void ASettleableWorldIsInOneOfTheTwoScopesAndNotTheOther()
        {
            int free = ScannerScopes.Colonizable(false);
            int taken = ScannerScopes.Colonizable(true);
            Assert.True(ScannerScopes.Holds(free, ScannerScopes.Unoccupied));
            Assert.False(ScannerScopes.Holds(free, ScannerScopes.Occupied));
            Assert.True(ScannerScopes.Holds(taken, ScannerScopes.Occupied));
            Assert.False(ScannerScopes.Holds(taken, ScannerScopes.Unoccupied));
        }

        [Fact]
        public void TheSettleableCategoryHasNoAllColumnAndCountsEachWorldOnce()
        {
            int[] row = ScannerScopes.Tally(
                new int[]
                {
                    ScannerScopes.Colonizable(false),
                    ScannerScopes.Colonizable(false),
                    ScannerScopes.Colonizable(true),
                },
                ScannerScopes.ColonizableWidth
            );
            Assert.Equal(2, row.Length);
            Assert.Equal(2, row[ScannerScopes.Unoccupied]);
            Assert.Equal(1, row[ScannerScopes.Occupied]);
        }

        [Fact]
        public void AColumnNamedAfterAKindHoldsOnlyThingsOfThatKind()
        {
            Assert.True(ScannerScopes.HoldsKind("Garden of Eden", 1, "Garden of Eden"));
            Assert.False(ScannerScopes.HoldsKind("Garden of Eden", 1, "Zero-G Gym"));
            Assert.False(ScannerScopes.HoldsKind(null, 1, "Zero-G Gym"));
            Assert.False(ScannerScopes.HoldsKind("Garden of Eden", 1, null));
        }

        [Fact]
        public void TheAllColumnOfACategoryOfKindsHoldsEverythingWhateverTheKindsAre()
        {
            Assert.True(ScannerScopes.HoldsKind("Garden of Eden", ScannerScopes.All, "all"));
            Assert.True(ScannerScopes.HoldsKind(null, ScannerScopes.All, null));
        }

        [Fact]
        public void AnExplorableCuriosityIsInAllAndInExplorable()
        {
            int scopes = ScannerScopes.Curiosity(true, false);
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.All));
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.Explorable));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.LowExpeditionPower));
        }

        [Fact]
        public void ACuriosityRefusedForPowerIsInItsOwnColumnAndNotInExplorable()
        {
            int scopes = ScannerScopes.Curiosity(false, true);
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.All));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Explorable));
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.LowExpeditionPower));
        }

        [Fact]
        public void ACuriosityRefusedForSomethingElseIsOnlyInAll()
        {
            // A curiosity already being expedited, or one a quest has locked, is in neither column -
            // and is still findable, because "all" is what a category always has.
            int scopes = ScannerScopes.Curiosity(false, false);
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.All));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Explorable));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.LowExpeditionPower));
        }
    }
}

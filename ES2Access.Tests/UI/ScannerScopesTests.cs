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
                    ScannerScopes.System(ScannerScopes.Neutral, false, false),
                    ScannerScopes.All
                )
            );
        }

        [Fact]
        public void ASpecialNodeIsSpecialAndNothingElse()
        {
            int scopes = ScannerScopes.System(ScannerScopes.Neutral, true, false);
            Assert.True(ScannerScopes.Holds(scopes, ScannerScopes.Special));
            // Not neutral: the affiliation trio is about who HOLDS a place, and nobody holds a nebula.
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Neutral));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Friendly));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Enemy));
            Assert.False(ScannerScopes.Holds(scopes, ScannerScopes.Homeworld));
        }

        [Fact]
        public void AnEnemyCapitalIsBothEnemyAndHomeworld()
        {
            int scopes = ScannerScopes.System(ScannerScopes.Enemy, false, true);
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
            // one of which is their capital, and ten neutral - plus one special node.
            int[] scopes = new int[13];
            scopes[0] = ScannerScopes.System(ScannerScopes.Friendly, false, true);
            scopes[1] = ScannerScopes.System(ScannerScopes.Friendly, false, false);
            for (int i = 2; i < 12; i++)
            {
                scopes[i] = ScannerScopes.System(ScannerScopes.Neutral, false, false);
            }

            scopes[12] = ScannerScopes.System(ScannerScopes.Neutral, true, false);

            int[] row = ScannerScopes.Tally(scopes, ScannerScopes.SystemWidth);
            Assert.Equal(13, row[ScannerScopes.All]);
            Assert.Equal(2, row[ScannerScopes.Friendly]);
            Assert.Equal(10, row[ScannerScopes.Neutral]);
            Assert.Equal(0, row[ScannerScopes.Enemy]);
            Assert.Equal(1, row[ScannerScopes.Homeworld]);
            Assert.Equal(1, row[ScannerScopes.Special]);

            // "All" is the size of the category, NOT the sum of the scopes below it - the capital is
            // counted twice and the special node is counted in neither trio.
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
    }
}

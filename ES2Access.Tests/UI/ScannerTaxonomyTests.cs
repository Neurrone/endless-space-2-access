using System.Collections.Generic;
using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// What the settings editor offers as the columns of one built-in category. The rule that needs
    /// the test is the stale one: a selector the player saved whose column this galaxy has none of
    /// has to be offered anyway, or the scanner skips it every press and the editor shows the player
    /// nothing to untick.
    /// </summary>
    public class ScannerTaxonomyTests
    {
        private static ScannerTaxonomy Galaxy()
        {
            ScannerTaxonomy taxonomy = new ScannerTaxonomy();
            ScannerTaxonomyCategory systems = taxonomy.Add(ScannerKeys.Systems, "Systems");
            systems.Add(ScannerKeys.All, "all");
            systems.Add("enemy", "enemy");

            ScannerTaxonomyCategory luxury = taxonomy.Add(ScannerKeys.Luxury, "Luxury Resources");
            luxury.Add(ScannerKeys.All, "all");
            luxury.Add("Luxury3", "Dustciduous Trees");
            return taxonomy;
        }

        [Fact]
        public void OffersThisGalaxysColumnsInOrder()
        {
            IList<ScannerTaxonomyColumn> offer = Galaxy().Offer(ScannerKeys.Luxury, null);
            Assert.Equal(2, offer.Count);
            Assert.Equal(ScannerKeys.All, offer[0].Key);
            Assert.Equal("Dustciduous Trees", offer[1].Label);
            Assert.False(offer[1].Missing);
        }

        [Fact]
        public void OffersASavedColumnThisGalaxyHasNoneOf()
        {
            List<ScannerSelector> saved = new List<ScannerSelector>
            {
                new ScannerSelector(ScannerKeys.Luxury, "Luxury3"),
                new ScannerSelector(ScannerKeys.Luxury, "Luxury9"),
                new ScannerSelector(ScannerKeys.Systems, "enemy"),
            };

            IList<ScannerTaxonomyColumn> offer = Galaxy().Offer(ScannerKeys.Luxury, saved);
            // The galaxy's two, then the one only the player's category knows about - last, so a
            // walk of the list reads what is out there before what is left over.
            Assert.Equal(3, offer.Count);
            Assert.Equal("Luxury9", offer[2].Key);
            Assert.True(offer[2].Missing);
            Assert.Null(offer[2].Label);
            // The saved selector for ANOTHER category is not this category's business.
            Assert.DoesNotContain(offer, column => column.Key == "enemy");
        }

        [Fact]
        public void OffersNothingForACategoryThisBuildDoesNotHave()
        {
            Assert.Empty(Galaxy().Offer("nosuchcategory", null));
        }

        [Fact]
        public void LabelsAreWhatANameIsCheckedAgainst()
        {
            Assert.Equal(new[] { "Systems", "Luxury Resources" }, Galaxy().Labels());
        }
    }
}

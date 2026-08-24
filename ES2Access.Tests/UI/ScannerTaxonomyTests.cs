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

        // ---- the kinds the game's databases define (stage 6) ----

        private static ScannerTaxonomyCategory Kinds(params ScannerKind[] kinds)
        {
            ScannerTaxonomy taxonomy = new ScannerTaxonomy();
            ScannerTaxonomyCategory category = taxonomy.Add(ScannerKeys.Luxury, "Luxury Resources");
            category.Add(ScannerKeys.All, "all");
            category.AddKinds(kinds, System.StringComparer.Ordinal);
            return category;
        }

        [Fact]
        public void KindsComeOutSortedByTheWordsThePlayerHears()
        {
            IList<ScannerTaxonomyColumn> columns = Kinds(
                new ScannerKind("Luxury9", "Quadnium"),
                new ScannerKind("Luxury3", "Dustciduous Trees"),
                new ScannerKind("Luxury1", "Aluminium")
            ).Columns;

            // The written-down column first, then the kinds in the player's own alphabet.
            Assert.Equal(
                new[] { ScannerKeys.All, "Luxury1", "Luxury3", "Luxury9" },
                new[] { columns[0].Key, columns[1].Key, columns[2].Key, columns[3].Key }
            );
        }

        [Fact]
        public void OneKeyIsOneColumnHoweverManyDefinitionsShareIt()
        {
            // The curiosity database holds several definitions per displayed type, and the displayed
            // type is the column.
            IList<ScannerTaxonomyColumn> columns = Kinds(
                new ScannerKind("CuriosityTypeGuardian", "Guardian"),
                new ScannerKind("CuriosityTypeGuardian", "Guardian")
            ).Columns;

            Assert.Equal(2, columns.Count);
            Assert.Equal("CuriosityTypeGuardian", columns[1].Key);
        }

        [Fact]
        public void AKindTheLocalizerHasNoWordsForIsNotOffered()
        {
            IList<ScannerTaxonomyColumn> columns = Kinds(
                new ScannerKind("Luxury1", null),
                new ScannerKind("Luxury2", ""),
                new ScannerKind(null, "Nameless"),
                new ScannerKind("Luxury3", "Dustciduous Trees")
            ).Columns;

            Assert.Equal(2, columns.Count);
            Assert.Equal("Luxury3", columns[1].Key);
        }

        [Fact]
        public void TwoKindsDrawnTheSameKeepAStableOrder()
        {
            IList<ScannerTaxonomyColumn> columns = Kinds(
                new ScannerKind("LuxuryB", "Same"),
                new ScannerKind("LuxuryA", "Same")
            ).Columns;

            Assert.Equal("LuxuryA", columns[1].Key);
            Assert.Equal("LuxuryB", columns[2].Key);
        }

        [Fact]
        public void NoKindsAtAllLeavesTheWrittenDownColumns()
        {
            Assert.Single(Kinds().Columns);
            ScannerTaxonomy taxonomy = new ScannerTaxonomy();
            ScannerTaxonomyCategory category = taxonomy.Add(ScannerKeys.Anomalies, "Anomalies");
            category.AddKinds(null, null);
            Assert.Empty(category.Columns);
        }
    }
}

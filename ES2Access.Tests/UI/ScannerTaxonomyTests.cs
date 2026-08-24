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
        public void TwoKindsDrawnTheSameAreONEColumn()
        {
            // The game pairs a deposit with its system-wide twin and an anomaly with its reduced
            // form, and draws both with one word. The scanner's found columns are keyed by that word,
            // so two checkboxes could never have meant two things.
            IList<ScannerTaxonomyColumn> columns = Kinds(
                new ScannerKind("SystemLuxury15", "Amianthoid"),
                new ScannerKind("Luxury15", "Amianthoid")
            ).Columns;

            Assert.Equal(2, columns.Count);
            Assert.Equal("Luxury15", columns[1].Key);
            Assert.Equal(new[] { "Luxury15", "SystemLuxury15" }, columns[1].Keys);
        }

        [Fact]
        public void ACategorySavedUnderEitherTwinMeansTheOneColumn()
        {
            ScannerTaxonomyCategory category = Kinds(
                new ScannerKind("Luxury15", "Amianthoid"),
                new ScannerKind("SystemLuxury15", "Amianthoid")
            );

            Assert.Equal("Luxury15", category.Answering("SystemLuxury15").Key);
            Assert.Equal("Luxury15", category.Answering("Luxury15").Key);
            Assert.Null(category.Answering("Luxury16"));
        }

        [Fact]
        public void ASelectorSavedUnderTheOtherTwinIsNotOfferedAsStale()
        {
            ScannerTaxonomy taxonomy = new ScannerTaxonomy();
            ScannerTaxonomyCategory category = taxonomy.Add(ScannerKeys.Luxury, "Luxury Resources");
            category.Add(ScannerKeys.All, "all");
            category.AddKinds(
                new[]
                {
                    new ScannerKind("Luxury15", "Amianthoid"),
                    new ScannerKind("SystemLuxury15", "Amianthoid"),
                },
                System.StringComparer.Ordinal
            );

            IList<ScannerTaxonomyColumn> offer = taxonomy.Offer(
                ScannerKeys.Luxury,
                new List<ScannerSelector>
                {
                    new ScannerSelector(ScannerKeys.Luxury, "SystemLuxury15"),
                }
            );

            Assert.Equal(2, offer.Count);
            Assert.DoesNotContain(offer, column => column.Missing);
        }

        // ---- what a saved selector means at SCAN time (stage 7) ----

        [Fact]
        public void ASavedTwinFindsTheColumnTheOtherTwinWasFoundIn()
        {
            ScannerKindIndex index = new ScannerKindIndex(
                new[]
                {
                    new ScannerKind("PlanetAnomaly23", "Acid Rain"),
                    new ScannerKind("PlanetAnomaly23Reduced", "Acid Rain"),
                    new ScannerKind("PlanetAnomaly17", "Ashen Sky"),
                }
            );

            // The galaxy found the Reduced form; the category was saved naming the other one.
            string[] found = { "all", "Acid Rain" };
            Assert.Equal(1, index.Column("PlanetAnomaly23", found));
            Assert.Equal(1, index.Column("PlanetAnomaly23Reduced", found));
            // Nothing of that kind out there, and a key no database defines.
            Assert.Equal(-1, index.Column("PlanetAnomaly17", found));
            Assert.Equal(-1, index.Column("SomeOtherModsAnomaly", found));
            Assert.Null(index.Label("SomeOtherModsAnomaly"));
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

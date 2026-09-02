using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.ES2.UI;
using Xunit;

namespace ES2Access.Tests.ES2.UI
{
    /// <summary>
    /// What columns a custom category has this press, and which words a keyword catches. The order
    /// is the player's own list read back, and a selector this galaxy has no column for has to
    /// disappear for one press without ever being forgotten - both of them silent failures, since a
    /// missing column sounds like a category the player configured differently.
    /// </summary>
    public class ScannerCustomPlanTests
    {
        /// <summary>A galaxy that has a systems category with an "enemy" column and an anomalies
        /// category with one kind in it - and nothing else.</summary>
        private sealed class Galaxy : IScannerColumns
        {
            public readonly List<string> Asked = new List<string>();

            public bool Find(ScannerSelector selector, out int category, out int subcategory)
            {
                Asked.Add(selector.ToString());
                category = -1;
                subcategory = -1;
                if (selector.Category == "systems" && selector.Subcategory == "enemy")
                {
                    category = 0;
                    subcategory = 3;
                    return true;
                }

                if (selector.Category == "anomalies" && selector.Subcategory == "AnomalyGoldRush")
                {
                    category = 3;
                    subcategory = 1;
                    return true;
                }

                return false;
            }

            public string Label(int category, int subcategory)
            {
                return category + "/" + subcategory;
            }
        }

        private static ScannerCustomCategory Threats()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Threats");
            category.AddSelector(new ScannerSelector("systems", "enemy"));
            category.AddSelector(new ScannerSelector("luxury", "Hyperium"));
            category.AddSelector(new ScannerSelector("anomalies", "AnomalyGoldRush"));
            category.AddKeyword("Tundra");
            category.AddKeyword("Sophon");
            return category;
        }

        [Fact]
        public void AnEmptySlotPlansNothing()
        {
            Assert.Null(ScannerCustomPlan.Of(null, new Galaxy()));
        }

        [Fact]
        public void AllComesFirstThenTheSelectorsThenTheKeywordsInTheOrderTheyWereAdded()
        {
            ScannerCustomPlan plan = ScannerCustomPlan.Of(Threats(), new Galaxy());
            IList<ScannerCustomColumn> columns = plan.Columns;

            Assert.Equal("Threats", plan.Name);
            Assert.Equal(5, columns.Count);
            Assert.True(columns[0].Everything);
            Assert.Equal(ModStrings.Get(ModStrings.GalaxyScannerCustomAll), columns[0].Label);
            Assert.Equal("0/3", columns[1].Label);
            Assert.Equal("3/1", columns[2].Label);
            Assert.Equal("Tundra", columns[3].Keyword);
            Assert.Equal("Sophon", columns[4].Keyword);
        }

        [Fact]
        public void ASelectorThisGalaxyHasNoColumnForIsSkippedAndStillAsked()
        {
            Galaxy galaxy = new Galaxy();
            ScannerCustomPlan plan = ScannerCustomPlan.Of(Threats(), galaxy);

            // Three selectors were configured and this galaxy answered for two, so the plan is All,
            // those two, and the two keywords - five columns rather than six.
            Assert.Equal(5, plan.Columns.Count);
            Assert.Contains("luxury:Hyperium", galaxy.Asked);
        }

        [Fact]
        public void TheColumnNamesAreWhatTheCursorRemembersThemBy()
        {
            string[] labels = ScannerCustomPlan.Of(Threats(), new Galaxy()).Labels();
            Assert.Equal(5, labels.Length);
            Assert.Equal("0/3", labels[1]);
            Assert.Equal("Sophon", labels[4]);
        }

        [Fact]
        public void AKeywordLooksAtTheNameTheKindAndTheDetail()
        {
            Assert.True(ScannerCustomPlan.Catches("sophon", "Sophon Fleet 2", null, null));
            Assert.True(ScannerCustomPlan.Catches("gold", null, "Gold Rush", null));
            Assert.True(
                ScannerCustomPlan.Catches("tundra", "Dusay II", null, "Small Tundra, max population 8")
            );
            Assert.False(ScannerCustomPlan.Catches("vodyani", "Dusay II", "Gold Rush", "Small Tundra"));
        }

        [Fact]
        public void AKeywordIgnoresCaseAndAccents()
        {
            Assert.True(ScannerCustomPlan.Catches("SOPHON", "sophon fleet", null, null));
            Assert.True(ScannerCustomPlan.Catches("eloheem", "Elohéem Cradle", null, null));
        }

        [Fact]
        public void ABlankKeywordCatchesNothing()
        {
            Assert.False(ScannerCustomPlan.Catches("  ", "Dusay", null, null));
            Assert.False(ScannerCustomPlan.Catches(null, "Dusay", null, null));
            Assert.False(ScannerCustomPlan.Catches("Dusay", null, null, null));
        }
    }
}

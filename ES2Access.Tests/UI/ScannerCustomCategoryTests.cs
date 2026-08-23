using System;
using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// What a category the player made IS, and what the three slots will and will not hold. Every
    /// rule here is one a screen reader user cannot check: a blank name is silence, a duplicate
    /// column is two identical subcategories, and a name that collides with a built-in category is
    /// two entries of the cycle saying the same words.
    /// </summary>
    public class ScannerCustomCategoryTests
    {
        [Fact]
        public void ACategoryIsNeverNameless()
        {
            Assert.Throws<ArgumentException>(() => new ScannerCustomCategory("   "));
            Assert.Null(ScannerCustomCategory.Named(""));
            Assert.Null(ScannerCustomCategory.Named(null));
        }

        [Fact]
        public void ANameIsKeptTrimmed()
        {
            Assert.Equal("Front line", new ScannerCustomCategory("  Front line  ").Name);
        }

        [Fact]
        public void RenamingRefusesBlankAndRefusesTheNameItAlreadyHas()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Front line");
            Assert.False(category.Rename(" "));
            Assert.False(category.Rename("Front line"));
            Assert.True(category.Rename(" Rear "));
            Assert.Equal("Rear", category.Name);
        }

        [Fact]
        public void TheSameColumnIsNeverAddedTwice()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Threats");
            Assert.True(category.AddSelector(new ScannerSelector("systems", "enemy")));
            Assert.False(category.AddSelector(new ScannerSelector("systems", "enemy")));
            Assert.True(category.AddSelector(new ScannerSelector("fleets", "enemy")));
            Assert.Equal(2, category.Selectors.Count);
        }

        [Fact]
        public void AHalfNamedColumnIsNotAColumn()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Threats");
            Assert.False(category.AddSelector(new ScannerSelector("systems", "")));
            Assert.False(category.AddSelector(new ScannerSelector(null, "enemy")));
            Assert.Empty(category.Selectors);
        }

        [Fact]
        public void AKeywordIsTrimmedAndNeverBlankAndNeverTwiceInAnyCase()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Words");
            Assert.False(category.AddKeyword("  "));
            Assert.True(category.AddKeyword("  Tundra "));
            Assert.False(category.AddKeyword("tundra"));
            Assert.Equal(new[] { "Tundra" }, category.Keywords);
        }

        [Fact]
        public void AKeywordComesOutHoweverItWasSpelt()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Words");
            category.AddKeyword("Tundra");
            Assert.True(category.Has("TUNDRA"));
            Assert.True(category.RemoveKeyword(" tundra "));
            Assert.Empty(category.Keywords);
        }

        /// <summary>A keyword box on the settings page edits the word in PLACE. Its position is its
        /// column order, so re-typing it must not send it to the end of the list.</summary>
        [Fact]
        public void ARetypedKeywordKeepsItsPlaceAndCannotBecomeOneAlreadyThere()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Words");
            category.AddKeyword("Tundra");
            category.AddKeyword("Ocean");
            category.AddKeyword("Arid");

            Assert.True(category.ReplaceKeyword(1, " Jungle "));
            Assert.Equal(new[] { "Tundra", "Jungle", "Arid" }, category.Keywords);

            Assert.False(category.ReplaceKeyword(1, "arid"));
            Assert.False(category.ReplaceKeyword(1, "  "));
            Assert.False(category.ReplaceKeyword(3, "Anything"));
            Assert.Equal(new[] { "Tundra", "Jungle", "Arid" }, category.Keywords);

            // The same word in another case is the CASE being changed, which is what the column is
            // read out in - not a duplicate of itself.
            Assert.True(category.ReplaceKeyword(1, "JUNGLE"));
            Assert.Equal(new[] { "Tundra", "JUNGLE", "Arid" }, category.Keywords);
        }

        [Fact]
        public void ACopyIsIndependent()
        {
            ScannerCustomCategory category = new ScannerCustomCategory("Threats");
            category.AddSelector(new ScannerSelector("systems", "enemy"));
            category.AddKeyword("Sophon");

            ScannerCustomCategory copy = category.Copy();
            copy.Rename("Other");
            copy.AddKeyword("Vodyani");

            Assert.Equal("Threats", category.Name);
            Assert.Single(category.Keywords);
            Assert.Equal(2, copy.Keywords.Count);
        }

        [Fact]
        public void AnEmptySlotHoldsNothingAndClearingIsTheDelete()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            Assert.Null(slots.Slot(0));
            Assert.False(slots.Any);

            slots.Set(1, new ScannerCustomCategory("Threats"));
            Assert.True(slots.Any);
            Assert.Equal("Threats", slots.Slot(1).Name);

            slots.Clear(1);
            Assert.Null(slots.Slot(1));
            Assert.False(slots.Any);
        }

        [Fact]
        public void ASlotOutsideTheThreeAnswersNothingRatherThanThrowing()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            Assert.Null(slots.Slot(-1));
            Assert.Null(slots.Slot(ScannerCustomSlots.Count));
            Assert.False(slots.Set(ScannerCustomSlots.Count, new ScannerCustomCategory("X")));
        }

        [Fact]
        public void ANameTheCycleAlreadySaysIsTaken()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            slots.Set(0, new ScannerCustomCategory("Threats"));
            string[] builtIn = new string[] { "Systems", "Fleets" };

            Assert.True(slots.NameTaken(" systems ", 1, builtIn));
            Assert.True(slots.NameTaken("THREATS", 1, builtIn));
            Assert.False(slots.NameTaken("Threats", 0, builtIn));
            Assert.False(slots.NameTaken("Borders", 1, builtIn));
        }

        [Fact]
        public void ACopyOfTheSlotsIsWhatAnEditorCanThrowAway()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            slots.Set(0, new ScannerCustomCategory("Threats"));

            ScannerCustomSlots edits = slots.Copy();
            edits.Slot(0).Rename("Renamed");
            edits.Set(2, new ScannerCustomCategory("New"));

            Assert.Equal("Threats", slots.Slot(0).Name);
            Assert.Null(slots.Slot(2));
        }

        [Fact]
        public void EveryCategoryHasAStableKeyAndEveryKeyIsItsOwn()
        {
            Assert.Equal(ScannerKeys.Categories.Length, ScannerKeys.Subcategories.Length);
            for (int i = 0; i < ScannerKeys.Categories.Length; i++)
            {
                Assert.Equal(i, ScannerKeys.Category(ScannerKeys.Categories[i]));
                string[] row = ScannerKeys.Subcategories[i];
                for (int c = 0; c < row.Length; c++)
                {
                    Assert.Equal(c, ScannerKeys.Subcategory(i, row[c]));
                }
            }
        }

        [Fact]
        public void TheWrittenDownColumnsAreTheWidthsTheScopesDeclare()
        {
            Assert.Equal(
                ScannerScopes.SystemWidth,
                ScannerKeys.Subcategories[ScannerKeys.Category(ScannerKeys.Systems)].Length
            );
            Assert.Equal(
                ScannerScopes.ColonizableWidth,
                ScannerKeys.Subcategories[ScannerKeys.Category(ScannerKeys.Colonizable)].Length
            );
            Assert.Equal(
                ScannerScopes.CuriosityWidth,
                ScannerKeys.Subcategories[ScannerKeys.Category(ScannerKeys.Curiosities)].Length
            );
            Assert.Equal(
                ScannerScopes.AffiliationWidth,
                ScannerKeys.Subcategories[ScannerKeys.Category(ScannerKeys.Fleets)].Length
            );
            Assert.Equal(1, ScannerScopes.SingleWidth);
            Assert.Single(ScannerKeys.Subcategories[ScannerKeys.Category(ScannerKeys.Markers)]);
        }

        [Fact]
        public void AKeyNoBuildEverHadNamesNothing()
        {
            Assert.Equal(-1, ScannerKeys.Category("constellations"));
            Assert.Equal(-1, ScannerKeys.Subcategory(0, "AnomalySwamp"));
        }

        /// <summary>The editor's whole "has anything changed" question - what lights Apply and what
        /// makes Escape ask. Every field of a category is part of the answer, and so is the ORDER of
        /// the two lists, because that is the order the columns come out in.</summary>
        [Fact]
        public void TwoSetsOfSlotsSayTheSameThingOrTheyDoNot()
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            ScannerCustomCategory one = new ScannerCustomCategory("Watch list");
            one.AddSelector(new ScannerSelector("systems", "neutral"));
            one.AddSelector(new ScannerSelector("fleets", "enemy"));
            one.AddKeyword("Dusay");
            slots.Set(0, one);

            Assert.True(slots.Same(slots.Copy()));
            Assert.True(slots.Copy().Same(slots));
            Assert.False(slots.Same(null));
            Assert.False(slots.Same(new ScannerCustomSlots()));
            Assert.False(new ScannerCustomSlots().Same(slots));

            ScannerCustomSlots renamed = slots.Copy();
            renamed.Slot(0).Rename("Watch List");
            Assert.False(slots.Same(renamed));

            ScannerCustomSlots reordered = new ScannerCustomSlots();
            ScannerCustomCategory other = new ScannerCustomCategory("Watch list");
            other.AddSelector(new ScannerSelector("fleets", "enemy"));
            other.AddSelector(new ScannerSelector("systems", "neutral"));
            other.AddKeyword("Dusay");
            reordered.Set(0, other);
            Assert.False(slots.Same(reordered));

            ScannerCustomSlots fewer = slots.Copy();
            fewer.Slot(0).RemoveKeyword("Dusay");
            Assert.False(slots.Same(fewer));

            ScannerCustomSlots elsewhere = slots.Copy();
            elsewhere.Set(2, new ScannerCustomCategory("Second"));
            Assert.False(slots.Same(elsewhere));
        }
    }
}

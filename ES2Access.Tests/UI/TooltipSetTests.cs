using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The engine-free half of the tooltip resolver: what makes two tooltips the same tooltip.
    ///
    /// The tree walking needs the game, but this does not - and this is the part that was wrong.
    /// A resolver that dedupes by component reference reads one explanation once per CLONE (the game
    /// copies a card's tooltip onto the picture inside it), and one that compares only the content
    /// string collapses two different dossiers that share an empty content field.
    /// </summary>
    public class TooltipSetTests
    {
        [Fact]
        public void ATooltipIsNewOnceAndThenKnown()
        {
            TooltipSet seen = new TooltipSet();
            TooltipKey key = new TooltipKey("Planet", "", new object());
            Assert.True(seen.Add(key));
            Assert.False(seen.Add(key));
            Assert.True(seen.Has(key));
            Assert.Equal(1, seen.Count);
        }

        [Fact]
        public void ACloneOfTheSameTooltipIsNotASecondTooltip()
        {
            object target = new object();
            TooltipSet seen = new TooltipSet();
            Assert.True(seen.Add(new TooltipKey("Constructible", "LawP01L00", target)));

            // AgeTooltip.Copy makes a second COMPONENT with the same three fields. Reading it twice
            // is the event item's dossier said twice.
            Assert.False(seen.Add(new TooltipKey("Constructible", "LawP01L00", target)));
            Assert.Equal(1, seen.Count);
        }

        [Fact]
        public void TwoDossiersAboutDifferentThingsAreTwoTooltips()
        {
            TooltipSet seen = new TooltipSet();
            Assert.True(seen.Add(new TooltipKey("Planet", "", new object())));
            Assert.True(seen.Add(new TooltipKey("Planet", "", new object())));
            Assert.Equal(2, seen.Count);
        }

        [Fact]
        public void AClassAloneAndAContentAloneAreDifferentTooltips()
        {
            TooltipSet seen = new TooltipSet();
            Assert.True(seen.Add(new TooltipKey("StarSystem", null, null)));
            Assert.True(seen.Add(new TooltipKey(null, "Click to rename it", null)));
            Assert.Equal(2, seen.Count);
        }

        [Fact]
        public void AnUnsetFieldReadsTheSameWhetherThePrefabLeftItNullOrEmpty()
        {
            // A prefab leaves a field null and code that clears one writes "": the engine reads both
            // as nothing, so the resolver must not see two tooltips where the game sees one.
            TooltipSet seen = new TooltipSet();
            Assert.True(seen.Add(new TooltipKey(null, "The hull of this Ship", null)));
            Assert.False(seen.Add(new TooltipKey("", "The hull of this Ship", null)));
        }

        [Fact]
        public void TheFirstPlaceATooltipIsFoundIsTheOneThatIsKept()
        {
            // The resolver collects containers, then captions, then the widget's own, then its
            // pieces - so a tooltip the game hung in two of those places reads where it was drawn
            // FIRST, which is the order the player reads the row in.
            TooltipSet seen = new TooltipSet();
            TooltipKey caption = new TooltipKey("Simple", "Food", null);
            Assert.True(seen.Add(caption));
            Assert.False(seen.Add(caption));
            Assert.Equal(1, seen.Count);
        }

        [Fact]
        public void ClearingForgetsEverything()
        {
            TooltipSet seen = new TooltipSet();
            TooltipKey key = new TooltipKey("ShipHull", "", null);
            seen.Add(key);
            seen.Clear();
            Assert.Equal(0, seen.Count);
            Assert.True(seen.Add(key));
        }

        [Fact]
        public void ReachIsASetOfDirectionsThatCombine()
        {
            TooltipReach reach = TooltipReach.Own | TooltipReach.Parents | TooltipReach.Siblings;
            Assert.True((reach & TooltipReach.Own) != 0);
            Assert.True((reach & TooltipReach.Parents) != 0);
            Assert.True((reach & TooltipReach.Siblings) != 0);

            // The directions a caller did NOT ask for are the ones it cannot accidentally pick up.
            Assert.True((reach & TooltipReach.Descendants) == 0);
            Assert.True((reach & TooltipReach.ListEntry) == 0);
        }
    }
}

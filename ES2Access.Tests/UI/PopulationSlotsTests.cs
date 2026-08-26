using System.Collections.Generic;
using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Banding a colony's population ring. Two of the three bands are reachable in a save - an
    /// ordinary colony and one whose units have reached the overpopulation arc - and the LOCKED band
    /// is not: it needs a world holding more units than its own current maximum, which the fixtures
    /// never produce. So the arithmetic lives off the engine and is tested here instead.
    /// </summary>
    public class PopulationSlotsTests
    {
        private static List<PopulationSlots.Slot> Build(
            int units,
            int max,
            int safe,
            bool arc = true
        )
        {
            List<PopulationSlots.Slot> slots = new List<PopulationSlots.Slot>();
            PopulationSlots.Build(units, max, safe, arc, slots);
            return slots;
        }

        private static string Shape(List<PopulationSlots.Slot> slots)
        {
            string[] parts = new string[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                parts[i] = slots[i].Rank
                    + ":"
                    + slots[i].Kind
                    + ":"
                    + (slots[i].Unit < 0 ? "-" : slots[i].Unit.ToString());
            }

            return string.Join(" ", parts);
        }

        [Fact]
        public void OneSlotPerUnitThenOnePerEmptyPlaceUpToTheMaximum()
        {
            Assert.Equal(
                "1:Population:0 2:Population:1 3:Population:- 4:Overpopulation:- 5:Overpopulation:-",
                Shape(Build(units: 2, max: 5, safe: 3))
            );
        }

        [Fact]
        public void AUnitPastTheSafeMaximumIsDrawnInTheOverpopulationBand()
        {
            Assert.Equal(
                "1:Population:0 2:Population:1 3:Population:2 4:Population:3 5:Population:4 "
                    + "6:Overpopulation:5 7:Overpopulation:- 8:Overpopulation:-",
                Shape(Build(units: 6, max: 8, safe: 5))
            );
        }

        [Fact]
        public void WithoutTheArcTheSameRanksAreOrdinarySlots()
        {
            Assert.Equal(
                "1:Population:0 2:Population:- 3:Population:- 4:Population:- 5:Population:-",
                Shape(Build(units: 1, max: 5, safe: 3, arc: false))
            );
        }

        [Fact]
        public void SlotsPastTheMaximumAreLockedAndDrawNoUnitEvenWhereOneLivesThere()
        {
            Assert.Equal(
                "1:Population:0 2:Population:1 3:Overpopulation:2 4:Locked:- 5:Locked:-",
                Shape(Build(units: 5, max: 3, safe: 2))
            );
        }

        [Fact]
        public void TheRingIsAsLongAsWhicheverOfTheTwoListsIsLonger()
        {
            Assert.Equal(7, PopulationSlots.Total(units: 7, maxPopulation: 4));
            Assert.Equal(4, PopulationSlots.Total(units: 1, maxPopulation: 4));
            Assert.Equal(0, PopulationSlots.Total(units: 0, maxPopulation: 0));
        }

        [Fact]
        public void ASafeMaximumAtOrPastTheMaximumLeavesNoOverpopulationBand()
        {
            Assert.Equal(
                "1:Population:0 2:Population:- 3:Population:-",
                Shape(Build(units: 1, max: 3, safe: 3))
            );
            Assert.Equal(
                "1:Population:0 2:Population:- 3:Population:-",
                Shape(Build(units: 1, max: 3, safe: 9))
            );
        }

        [Fact]
        public void AWorldWithNoRoomAndNobodyOnItHasNoSlotsAtAll()
        {
            Assert.Empty(Build(units: 0, max: 0, safe: 0));
        }

        [Fact]
        public void ASafeMaximumOfZeroPutsEveryRankUnderTheArc()
        {
            Assert.Equal(
                "1:Overpopulation:0 2:Overpopulation:-",
                Shape(Build(units: 1, max: 2, safe: 0))
            );
        }

        private static List<PopulationSlots.Slot> Unsettled(int units, int max)
        {
            List<PopulationSlots.Slot> slots = new List<PopulationSlots.Slot>();
            PopulationSlots.BuildUnsettled(units, max, slots);
            return slots;
        }

        [Fact]
        public void AnUnsettledWorldDrawsOneOrdinaryEmptySlotPerPointOfRoom()
        {
            Assert.Equal(
                "1:Population:- 2:Population:- 3:Population:- 4:Population:- 5:Population:- "
                    + "6:Population:-",
                Shape(Unsettled(units: 0, max: 6))
            );
            Assert.Equal(
                "1:Population:- 2:Population:- 3:Population:-",
                Shape(Unsettled(units: 0, max: 3))
            );
        }

        [Fact]
        public void AnUnsettledWorldWithNoRoomHasNoSlotsAtAll()
        {
            Assert.Empty(Unsettled(units: 0, max: 0));
            Assert.Empty(Unsettled(units: 4, max: 4));
            Assert.Empty(Unsettled(units: 9, max: 4));
        }

        /// <summary>The ring counts its empty places out FROM whatever the planet says is already
        /// living there, so a head start shortens the ring rather than filling any of it - the ranks
        /// stay 1-upwards, because the ring the player walks starts at its first drawn marker.
        /// </summary>
        [Fact]
        public void AnUnsettledWorldsRingStartsAfterWhoeverIsAlreadyThere()
        {
            Assert.Equal("1:Population:- 2:Population:-", Shape(Unsettled(units: 2, max: 4)));
        }
    }
}

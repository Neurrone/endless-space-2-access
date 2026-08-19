using System.Collections.Generic;
using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Reading a ship's slots by the type of module they take. Four rules, none of which a dump would
    /// catch: the list is alphabetical by type, a slot that takes several types sits with the first of
    /// them, what is FITTED never moves a slot, and same-type slots keep the order the ship drew them
    /// in.
    /// </summary>
    public class SlotOrderTests
    {
        private static readonly string[] Defence = { "Defense Module" };
        private static readonly string[] Support = { "Support Module" };
        private static readonly string[] Weapon = { "Weapon Module" };

        [Fact]
        public void SlotsAreReadAlphabeticallyByTheTypeTheyTake()
        {
            List<string> slots = new List<string> { "front gun", "engine", "rear gun", "plating" };
            List<string[]> keys = new List<string[]> { Weapon, Support, Weapon, Defence };

            SlotOrder.Arrange(slots, keys);

            Assert.Equal(new[] { "plating", "engine", "front gun", "rear gun" }, slots);
        }

        [Fact]
        public void SameTypeSlotsKeepTheOrderTheShipDrewThemIn()
        {
            List<string> slots = new List<string> { "third", "first", "second" };
            List<string[]> keys = new List<string[]> { Weapon, Defence, Defence };

            SlotOrder.Arrange(slots, keys);

            Assert.Equal(new[] { "first", "second", "third" }, slots);
        }

        [Fact]
        public void ASlotTakingSeveralTypesSitsWithTheFirstOfThem()
        {
            string[] both = { "Support Module", "Defense Module" };
            SlotOrder.Alphabetical(both);
            Assert.Equal(new[] { "Defense Module", "Support Module" }, both);

            List<string> slots = new List<string> { "support bay", "defence and support", "plating" };
            List<string[]> keys = new List<string[]> { Support, both, Defence };

            SlotOrder.Arrange(slots, keys);

            // Under D, and after the slot that takes ONLY defence modules.
            Assert.Equal(new[] { "plating", "defence and support", "support bay" }, slots);
        }

        [Fact]
        public void WhatIsFittedNeverMovesASlot()
        {
            // A module whose name sorts first, in a weapons slot: the slot is still read with the
            // weapons, because the key is the slot's own type and nothing else.
            List<string> slots = new List<string> { "A-something", "engine", "plating" };
            List<string[]> keys = new List<string[]> { Weapon, Support, Defence };

            SlotOrder.Arrange(slots, keys);

            Assert.Equal(new[] { "plating", "engine", "A-something" }, slots);
        }

        [Fact]
        public void ASlotThatTakesAnythingIsReadLast()
        {
            List<string> slots = new List<string> { "any", "weapon", "defence" };
            List<string[]> keys = new List<string[]> { null, Weapon, Defence };

            SlotOrder.Arrange(slots, keys);

            Assert.Equal(new[] { "defence", "weapon", "any" }, slots);
        }

        [Fact]
        public void ATypeTheGameLeftUnnamedSortsAfterTheNamedOnes()
        {
            string[] names = { null, "Weapon Module", "", "Defense Module" };

            SlotOrder.Alphabetical(names);

            Assert.Equal(new[] { "Defense Module", "Weapon Module" }, new[] { names[0], names[1] });
            Assert.True(string.IsNullOrEmpty(names[2]) && string.IsNullOrEmpty(names[3]));
        }

        [Fact]
        public void ASlotTakingFewerTypesIsReadBeforeOneTakingThoseAndMore()
        {
            string[] defenceAndSupport = { "Defense Module", "Support Module" };
            List<string> slots = new List<string> { "defence and support", "defence" };
            List<string[]> keys = new List<string[]> { defenceAndSupport, Defence };

            SlotOrder.Arrange(slots, keys);

            Assert.Equal(new[] { "defence", "defence and support" }, slots);
        }
    }
}

using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The separation discipline in English — the output every screen in the mod is written
    /// against. These are the ported Factorio Access / Tanglebeep semantics.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class MessageBuilderTests
    {
        public MessageBuilderTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void FragmentsAreSpaceJoinedListItemsAreCommaJoined()
        {
            string msg = new MessageBuilder()
                .Fragment("the thing with")
                .ListItem("a")
                .ListItem("b")
                .ListItem("c")
                .Build();
            Assert.Equal("the thing with a, b, c", msg);
        }

        [Fact]
        public void NoLeadingSpaceOnFirstFragment()
        {
            Assert.Equal("x", new MessageBuilder().Fragment("x").Build());
        }

        [Fact]
        public void FragmentsWithinAListItemAreSpaceJoined()
        {
            string msg = new MessageBuilder()
                .ListItem("hello")
                .Fragment("world")
                .ListItem("again")
                .Build();
            Assert.Equal("hello world, again", msg);
        }

        [Fact]
        public void ForcedCommaSeparatesFromPrecedingFragment()
        {
            string msg = new MessageBuilder().Fragment("grid").ListItemForcedComma("3 by 3").Build();
            Assert.Equal("grid, 3 by 3", msg);
        }

        [Fact]
        public void NullAndEmptyFragmentsAreIgnored()
        {
            string msg = new MessageBuilder()
                .Fragment("a")
                .Fragment(null)
                .Fragment("")
                .Fragment("b")
                .Build();
            Assert.Equal("a b", msg);
        }

        [Fact]
        public void FractionReadsNumOfDenom()
        {
            Assert.Equal("5 of 20", new MessageBuilder().PushFraction(5, 20).Build());
        }

        [Fact]
        public void FractionWithUnitAppendsUnit()
        {
            Assert.Equal(
                "3 of 5 charges",
                new MessageBuilder().PushFraction(3, 5, "charges").Build()
            );
        }

        [Fact]
        public void QuantityReadsAsMultiplierAfterName()
        {
            Assert.Equal(
                "Titanium x 5",
                new MessageBuilder().Fragment("Titanium").PushQuantity(5).Build()
            );
        }

        [Theory]
        [InlineData(1)]
        [InlineData(0)]
        public void QuantityOfOneOrLessAppendsNothing(int count)
        {
            Assert.Equal(
                "Titanium",
                new MessageBuilder().Fragment("Titanium").PushQuantity(count).Build()
            );
        }

        /// <summary>The DRAG's own rule (owner ruling 2026-08-29): a cargo measured in units states
        /// its count every time, so a one-unit pick-up reads like its three-unit neighbours instead of
        /// like a different kind of answer. Same template, so a translation says the multiplier its own
        /// way either side of the rule.</summary>
        [Theory]
        [InlineData(3, "Imperials x 3")]
        [InlineData(1, "Imperials x 1")]
        [InlineData(0, "Imperials x 0")]
        public void AlwaysQuantityStatesTheCountEvenAtOne(int count, string expected)
        {
            Assert.Equal(
                expected,
                new MessageBuilder().Fragment("Imperials").PushQuantityAlways(count).Build()
            );
        }

        /// <summary>And the ordinary readout convention is untouched by it - the two live side by
        /// side, which is the whole reason the drag needed a variant rather than a change.</summary>
        [Fact]
        public void TheOrdinaryQuantityStaysSilentAtOne()
        {
            Assert.Equal(
                "Titanium",
                new MessageBuilder().Fragment("Titanium").PushQuantity(1).Build()
            );
            Assert.Equal(
                "Titanium x 1",
                new MessageBuilder().Fragment("Titanium").PushQuantityAlways(1).Build()
            );
        }

        [Fact]
        public void FractionFollowsFragmentSpacingAndListBoundaries()
        {
            // The fraction space-joins after its label; list items comma-join (except the first,
            // which space-joins to the preceding fragment) — the status-readout shape.
            string msg = new MessageBuilder()
                .Fragment("Health")
                .PushFraction(5, 20)
                .ListItem("Stamina")
                .PushFraction(8, 8)
                .ListItem("Energy")
                .PushFraction(3, 10)
                .Build();
            Assert.Equal("Health 5 of 20 Stamina 8 of 8, Energy 3 of 10", msg);
        }

        [Fact]
        public void UniformListItemFractionsCommaJoinWithNoLeadingComma()
        {
            string msg = new MessageBuilder()
                .ListItem()
                .PushFraction(5, 20, "health")
                .ListItem()
                .PushFraction(8, 8, "stamina")
                .ListItem("Level 3")
                .Build();
            Assert.Equal("5 of 20 health, 8 of 8 stamina, Level 3", msg);
        }

        [Fact]
        public void EmptyBuilderBuildsNull()
        {
            Assert.Null(new MessageBuilder().Build());
        }

        [Fact]
        public void EmptyBuilderIsEmpty()
        {
            MessageBuilder builder = new MessageBuilder();
            Assert.True(builder.IsEmpty);
            builder.Fragment("x");
            Assert.False(builder.IsEmpty);
        }

        [Fact]
        public void ReuseAfterBuildThrows()
        {
            MessageBuilder builder = new MessageBuilder().Fragment("x");
            builder.Build();
            Assert.Throws<InvalidOperationException>(() => builder.Fragment("y"));
        }

        [Fact]
        public void ExplicitSpaceFragmentThrows()
        {
            Assert.Throws<ArgumentException>(() => new MessageBuilder().Fragment(" "));
        }

        [Fact]
        public void TranslatedSeparatorsAndIdiomsReplaceTheEnglishOnes()
        {
            // A Japanese-style table: no space between fragments, "、" between list items, and the
            // denominator read before the numerator.
            InstallJapanese();

            string msg = new MessageBuilder()
                .Fragment("ニューゲーム")
                .ListItemForcedComma("ボタン")
                .ListItem()
                .PushFraction(1, 8)
                .Build();
            Assert.Equal("ニューゲーム、ボタン、8中1", msg);
        }

        [Fact]
        public void EmptyFragmentSeparatorJoinsFragmentsWithNothing()
        {
            InstallJapanese();

            string msg = new MessageBuilder()
                .Fragment("ニューゲーム")
                .ListItem("ボタン")
                .PushQuantity(3)
                .Build();
            Assert.Equal("ニューゲームボタン×3", msg);
        }

        [Fact]
        public void TranslatedFractionUnitIsUsedWhenAUnitIsGiven()
        {
            InstallJapanese();

            Assert.Equal("8中5 体力", new MessageBuilder().PushFraction(5, 8, "体力").Build());
        }

        private static void InstallJapanese()
        {
            ModStrings.Install(
                new Dictionary<string, string>
                {
                    { ModStrings.FragmentSeparator, "" },
                    { ModStrings.ListSeparator, "、" },
                    { ModStrings.Fraction, "{1}中{0}" },
                    { ModStrings.FractionUnit, "{1}中{0} {2}" },
                    { ModStrings.Quantity, "×{0}" },
                }
            );
        }
    }
}

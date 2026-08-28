using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Naming one nested tooltip entry against the set it is heard in. The engine half - what a widget
    /// draws, what a wrapper's title says - needs the game; this half is the ladders' answers and the
    /// choice between them, which is where the senate winner card's two "Industrialists" were decided.
    /// </summary>
    public class SiblingNameRuleTests
    {
        private const int Rungs = 4;

        private static Func<int, string> Ladder(params string[] answers)
        {
            string[] rungs = answers;
            return rung => rung < rungs.Length ? rungs[rung] : null;
        }

        private static IList<Func<int, string>> Set(params Func<int, string>[] ladders)
        {
            return new List<Func<int, string>>(ladders);
        }

        // The measured senate winner card (2026-08-28): the portrait group and the party-name label
        // both draw "Industrialists", and only the portrait's wrapper knows the hero's name.
        private static IList<Func<int, string>> WinnerCard()
        {
            return Set(
                Ladder("Industrialists", "Industrialists", "Dmitri Lenko", null),
                Ladder("Industrialists", "Industrialists", "Industrialists", null),
                Ladder("Established", "Established", null, "Reflects the experience gained…")
            );
        }

        [Fact]
        public void TheCollidingEntryTakesTheFirstRungThatSaysSomethingElse()
        {
            Assert.Equal("Dmitri Lenko", SiblingNameRule.Name(WinnerCard(), 0, Rungs));
        }

        [Fact]
        public void TheEntryWithNoDifferingRungKeepsTheSharedName()
        {
            // The party label really is about the party: no rung of its ladder says anything else, and
            // inventing a name is not on the ladder.
            Assert.Equal("Industrialists", SiblingNameRule.Name(WinnerCard(), 1, Rungs));
        }

        [Fact]
        public void AnEntryNobodyCollidesWithIsUntouched()
        {
            Assert.Equal("Established", SiblingNameRule.Name(WinnerCard(), 2, Rungs));
        }

        [Fact]
        public void TwoEntriesThatBothExplainTheSameThingBothKeepTheName()
        {
            // The senate's other two cards: portrait and label draw the party's word and the portrait
            // wrapper names the party too, because no hero is seated.
            IList<Func<int, string>> set = Set(
                Ladder("Pacifists", "Pacifists", "Pacifists"),
                Ladder("Pacifists", "Pacifists", "Pacifists")
            );
            Assert.Equal("Pacifists", SiblingNameRule.Name(set, 0, Rungs));
            Assert.Equal("Pacifists", SiblingNameRule.Name(set, 1, Rungs));
        }

        [Fact]
        public void CollisionIsOnTheWordsNotTheSpelling()
        {
            IList<Func<int, string>> set = Set(
                Ladder("Over-colonization", null, "Over-colonization penalty"),
                Ladder("Over colonization")
            );
            Assert.Equal("Over-colonization penalty", SiblingNameRule.Name(set, 0, Rungs));
        }

        [Fact]
        public void ARungThatAnswersTheSameWordsInAnotherSpellingIsNotADifferentAnswer()
        {
            IList<Func<int, string>> set = Set(
                Ladder("Food", null, "food!", "Food produced by this planet"),
                Ladder("Food")
            );
            Assert.Equal("Food produced by this planet", SiblingNameRule.Name(set, 0, Rungs));
        }

        [Fact]
        public void EachEntryIsDecidedAgainstItsSiblingsOwnFirstAnswer()
        {
            // Entry 0 steps down to "Dmitri Lenko"; entry 1 is still compared with what entry 0 DREW,
            // so the reading order cannot change either name.
            IList<Func<int, string>> card = WinnerCard();
            Assert.Equal("Industrialists", SiblingNameRule.Name(card, 1, Rungs));
            Assert.Equal("Dmitri Lenko", SiblingNameRule.Name(card, 0, Rungs));
            Assert.Equal("Industrialists", SiblingNameRule.Name(card, 1, Rungs));
        }

        [Fact]
        public void BlankRungsAnswerNothing()
        {
            IList<Func<int, string>> set = Set(Ladder(null, "  ", "", "Wit"), Ladder("Labor"));
            Assert.Equal("Wit", SiblingNameRule.Name(set, 0, Rungs));
        }

        [Fact]
        public void AnEntryWithNoAnswerAtAllStaysNameless()
        {
            IList<Func<int, string>> set = Set(Ladder(null, null), Ladder(null, null));
            Assert.Null(SiblingNameRule.Name(set, 0, Rungs));
        }

        [Fact]
        public void AloneInItsSetAnEntryIsItsFirstAnswer()
        {
            Assert.Equal(
                "Industrialists",
                SiblingNameRule.Name(Set(Ladder("Industrialists", null, "Dmitri Lenko")), 0, Rungs)
            );
        }

        [Fact]
        public void NoSetAndNoPlaceInItAnswerNothing()
        {
            Assert.Null(SiblingNameRule.Name(null, 0, Rungs));
            Assert.Null(SiblingNameRule.Name(Set(Ladder("Food")), 1, Rungs));
            Assert.Null(SiblingNameRule.Name(Set(Ladder("Food")), -1, Rungs));
        }

        [Fact]
        public void NullLaddersInTheSetAreJustEntriesWithNoName()
        {
            IList<Func<int, string>> set = Set(Ladder("Food"), null);
            Assert.Equal("Food", SiblingNameRule.Name(set, 0, Rungs));
            Assert.Null(SiblingNameRule.Name(set, 1, Rungs));
        }

        [Fact]
        public void ASetWithNoCollisionAsksNoRungPastTheFirstThatAnswers()
        {
            // The rungs are live widget reads; a set that needs no disambiguation must cost what naming
            // each entry alone costs.
            int asked = 0;
            Func<int, string> counted = rung =>
            {
                asked++;
                return rung == 0 ? "Food" : "Planet Food production";
            };
            IList<Func<int, string>> set = Set(counted, Ladder("Industry"));
            Assert.Equal("Food", SiblingNameRule.Name(set, 0, Rungs));
            Assert.Equal(1, asked);
        }

        [Fact]
        public void FirstIsTheLadderOnItsOwn()
        {
            Assert.Equal("Dmitri Lenko", SiblingNameRule.First(Ladder(null, "", "Dmitri Lenko"), Rungs));
            Assert.Null(SiblingNameRule.First(null, Rungs));
            Assert.Null(SiblingNameRule.First(Ladder("Food"), 0));
        }
    }
}

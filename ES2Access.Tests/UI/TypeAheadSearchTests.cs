using System;
using System.Collections.Generic;
using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Type-ahead: which tier a candidate falls into, and how the tiers merge into the results the user
    /// steps through.
    /// </summary>
    public class TypeAheadSearchTests
    {
        private static int Tier(string name, string typed)
        {
            int pos;
            return TypeAheadSearch.MatchTier(name.ToLowerInvariant(), typed.ToLowerInvariant(), out pos);
        }

        private static int Position(string name, string typed)
        {
            int pos;
            TypeAheadSearch.MatchTier(name.ToLowerInvariant(), typed.ToLowerInvariant(), out pos);
            return pos;
        }

        [Theory]
        [InlineData("Load Game", "load", 0)]   // start of string, whole word
        [InlineData("Load Game", "l", 1)]      // start of string, prefix
        [InlineData("Quick Load", "load", 2)]  // mid string, whole word
        [InlineData("Quick Loader", "load", 3)]// mid string, word prefix
        [InlineData("Reload", "load", 4)]      // substring anywhere
        [InlineData("Gas Pipeline", "ga pi", 5)] // space-delimited word-prefix abbreviation
        [InlineData("Load Game", "zzz", -1)]   // no match
        public void TiersRankFromWholeWordDownToAbbreviation(string name, string typed, int expected)
        {
            Assert.Equal(expected, Tier(name, typed));
        }

        [Fact]
        public void AWholeWordEndingAtACommaCountsAsAWholeWord()
        {
            Assert.Equal(0, Tier("Load, ready", "load"));
            Assert.Equal(2, Tier("Fleet, load, ready", "load"));
        }

        [Fact]
        public void APrefixLongerThanTheNameNeverMatches()
        {
            Assert.Equal(-1, Tier("Sol", "solar system"));
        }

        [Fact]
        public void DiacriticsAreIgnored()
        {
            Assert.Equal(0, Tier("Séance", "seance"));
            Assert.Equal(0, Tier("Œuvre", "oeuvre"));
        }

        [Fact]
        public void TheMatchPositionIsReported()
        {
            Assert.Equal(0, Position("Load Game", "load"));
            Assert.Equal(6, Position("Quick Load", "load"));
        }

        [Fact]
        public void AnAbbreviationMustStayWithinOneCommaSegment()
        {
            Assert.Equal(5, Tier("Gas Pipe", "ga pi"));
            Assert.Equal(-1, Tier("Gas, Pipe", "ga pi"));
        }

        // ---- result list ----

        // A search whose "nothing matched" feedback records -1, so the announce log reads as a sequence.
        private static TypeAheadSearch Over(List<int> announced)
        {
            TypeAheadSearch s = new TypeAheadSearch();
            s.OnNoMatch = text => announced.Add(-1);
            return s;
        }

        private static void Type(TypeAheadSearch s, string text, List<string> items, List<int> announced)
        {
            foreach (char c in text) s.AddChar(c);
            s.Search(items.Count, i => items[i], i => announced.Add(i));
        }

        [Fact]
        public void StrongerTiersComeFirstAndItemOrderBreaksTheTie()
        {
            List<string> items = new List<string> { "License", "Load Game", "DLC" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "l", items, announced);
            Assert.Equal(3, s.ResultCount);
            Assert.Equal(0, announced[0]); // License and Load Game are both tier 1; list order wins
            Assert.Equal(0, s.CurrentResultIndex);
        }

        [Fact]
        public void RepeatingALetterCyclesThroughAllOfItsMatches()
        {
            List<string> items = new List<string> { "License", "Load Game", "DLC" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "l", items, announced);
            Type(s, "l", items, announced); // "ll" collapses back to "l" and steps
            Type(s, "l", items, announced);
            Type(s, "l", items, announced); // wraps

            Assert.Equal(new[] { 0, 1, 2, 0 }, announced);
            Assert.Equal("l", s.Buffer);
        }

        [Fact]
        public void MatchesInTheNameOutrankMatchesInTheAppendedMetadata()
        {
            List<string> items = new List<string> { "Alpha, warp drive", "Warp Beacon" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "warp", items, announced);
            Assert.Equal(1, announced[0]);
            Assert.Equal(2, s.ResultCount);
        }

        [Fact]
        public void NoMatchReportsTheBufferAndLeavesNoResults()
        {
            List<string> items = new List<string> { "Alpha", "Beta" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "zz", items, announced);
            Assert.Equal(0, s.ResultCount);
            Assert.Equal(-1, s.CurrentResultIndex);
            Assert.Equal(new[] { -1 }, announced);
            Assert.True(s.IsSearchActive);
        }

        [Fact]
        public void NavigateResultsWrapsInBothDirections()
        {
            List<string> items = new List<string> { "Alpha", "Alpha two", "Beta" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "alpha", items, announced);
            s.NavigateResults(-1);
            Assert.Equal(1, s.CurrentResultIndex);
            s.NavigateResults(1);
            Assert.Equal(0, s.CurrentResultIndex);
            s.JumpToLastResult();
            Assert.Equal(1, s.CurrentResultIndex);
            s.JumpToFirstResult();
            Assert.Equal(0, s.CurrentResultIndex);
        }

        [Fact]
        public void BackspaceAndClearResetTheBuffer()
        {
            List<string> items = new List<string> { "Alpha" };
            List<int> announced = new List<int>();
            TypeAheadSearch s = Over(announced);

            Type(s, "al", items, announced);
            Assert.True(s.RemoveChar());
            Assert.Equal("a", s.Buffer);
            s.Clear();
            Assert.False(s.HasBuffer);
            Assert.False(s.IsSearchActive);
            Assert.False(s.RemoveChar());
        }
    }
}

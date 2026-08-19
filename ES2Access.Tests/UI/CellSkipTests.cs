using System.Collections.Generic;
using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The skip's walk. Every failure here is inaudible in the ordinary way of this map: a walk that
    /// compares each cell with the one before it stops in the middle of a run and sounds exactly like
    /// a cursor that found something; a walk that counts the landing cell among the ones it skipped
    /// reports a number the player cannot check against anything.
    /// </summary>
    public class CellSkipTests
    {
        // A strip of map, one cell per whole unit, laid out west to east from x = 0. Anything off the
        // strip is off the galaxy. The signature of a cell is whatever the strip holds there.
        private sealed class Strip
        {
            private readonly string[] _cells;

            public Strip(params string[] cells)
            {
                _cells = cells;
            }

            public bool InBounds(int x, int y)
            {
                return y == 0 && x >= 0 && x < _cells.Length;
            }

            public CellSignature At(int x, int y)
            {
                string here = InBounds(x, y) ? _cells[x] : null;
                List<string> things = new List<string>();
                if (!string.IsNullOrEmpty(here))
                {
                    things.Add(here);
                }

                return new CellSignature(things, CellFog.Clear);
            }
        }

        private static bool Skip(Strip strip, int from, int direction, out int to, out int skipped)
        {
            int y;
            return CellSkip.Find(
                from,
                0,
                1,
                direction,
                0,
                strip.InBounds,
                strip.At,
                out to,
                out y,
                out skipped
            );
        }

        /// <summary>The whole point of the key: a run of cells that are all the same as the one the
        /// player is standing on is crossed in one press, and the player is told how many were passed
        /// over.</summary>
        [Fact]
        public void ItLandsOnTheFirstCellThatIsNotWhatThePlayerIsStandingOn()
        {
            Strip strip = new Strip(null, null, null, "Dusay", null);
            int to;
            int skipped;
            Assert.True(Skip(strip, 0, 1, out to, out skipped));
            Assert.Equal(3, to);
            Assert.Equal(2, skipped);
        }

        /// <summary>A neighbour that already differs is one ordinary step, and says nothing about
        /// skipping - there was nothing to skip.</summary>
        [Fact]
        public void AnImmediateChangeIsOneStepAndNothingSkipped()
        {
            Strip strip = new Strip(null, "Heka", null);
            int to;
            int skipped;
            Assert.True(Skip(strip, 0, 1, out to, out skipped));
            Assert.Equal(1, to);
            Assert.Equal(0, skipped);
        }

        /// <summary>Compared with the ORIGIN and never with the cell before: a stretch holding the
        /// same star all the way is one stretch, and the cursor comes out of the far side of it rather
        /// than stopping the moment the star drops out of the cell.</summary>
        [Fact]
        public void TheComparisonIsAgainstTheOriginNotAgainstTheStepBefore()
        {
            Strip strip = new Strip("Dusay", "Dusay", "Dusay", "Heka");
            int to;
            int skipped;
            Assert.True(Skip(strip, 0, 1, out to, out skipped));
            Assert.Equal(3, to);
            Assert.Equal(2, skipped);
        }

        /// <summary>Running out of galaxy is not a refusal: the cursor goes as far as it can and stops
        /// on the last cell that was still on the map.</summary>
        [Fact]
        public void AWalkThatRunsOffTheMapLandsOnTheLastCellStillOnIt()
        {
            Strip strip = new Strip(null, null, null);
            int to;
            int skipped;
            Assert.True(Skip(strip, 0, 1, out to, out skipped));
            Assert.Equal(2, to);
            Assert.Equal(1, skipped);
        }

        /// <summary>The landing cell is not one of the ones that were skipped OVER, so a walk to the
        /// very next cell at the edge reports nothing skipped at all.</summary>
        [Fact]
        public void TheCellLandedOnIsNotCountedAmongTheOnesSkipped()
        {
            Strip strip = new Strip(null, null);
            int to;
            int skipped;
            Assert.True(Skip(strip, 0, 1, out to, out skipped));
            Assert.Equal(1, to);
            Assert.Equal(0, skipped);
        }

        /// <summary>With no step possible at all the walk refuses, and the caller says what the plain
        /// arrow says at the edge of the map.</summary>
        [Fact]
        public void NoStepPossibleIsARefusal()
        {
            Strip strip = new Strip(null, null);
            int to;
            int skipped;
            Assert.False(Skip(strip, 1, 1, out to, out skipped));
            Assert.Equal(1, to);
            Assert.Equal(0, skipped);
        }

        /// <summary>The walk steps by the CURSOR's own size, so the cells it visits are the very cells
        /// the arrows visit and a sweep can neither skip a star nor hear one twice.</summary>
        [Fact]
        public void TheWalkStepsByTheCursorsOwnSize()
        {
            List<int> asked = new List<int>();
            int to;
            int toY;
            int skipped;
            bool found = CellSkip.Find(
                0,
                0,
                5,
                1,
                0,
                (x, y) => x <= 20,
                (x, y) =>
                {
                    asked.Add(x);
                    return new CellSignature(
                        x == 10 ? new List<string> { "Heka" } : new List<string>(),
                        CellFog.Clear
                    );
                },
                out to,
                out toY,
                out skipped
            );

            Assert.True(found);
            Assert.Equal(10, to);
            Assert.Equal(1, skipped);
            Assert.Equal(new List<int> { 0, 5, 10 }, asked);
        }

        /// <summary>The fog is part of what a cell IS: crossing the edge of what the empire has
        /// explored is a stop even where both cells are empty of things.</summary>
        [Fact]
        public void TheFogIsPartOfTheSignature()
        {
            List<string> nothing = new List<string>();
            Assert.False(
                new CellSignature(nothing, CellFog.Clear).Equals(
                    new CellSignature(nothing, CellFog.Partly)
                )
            );
            Assert.False(
                new CellSignature(nothing, CellFog.Partly).Equals(
                    new CellSignature(nothing, CellFog.Wholly)
                )
            );
            Assert.True(
                new CellSignature(nothing, CellFog.Wholly).Equals(
                    new CellSignature(nothing, CellFog.Wholly)
                )
            );
        }

        /// <summary>The identity SET, in no order: the same things gathered in a different order are
        /// the same cell, and one extra thing is a different one.</summary>
        [Fact]
        public void TheThingsAreASetAndTheirOrderIsNotPartOfIt()
        {
            CellSignature one = new CellSignature(
                new List<string> { "fleet:7", "place:3" },
                CellFog.Clear
            );
            CellSignature two = new CellSignature(
                new List<string> { "place:3", "fleet:7" },
                CellFog.Clear
            );
            CellSignature three = new CellSignature(
                new List<string> { "place:3", "fleet:7", "lane:9" },
                CellFog.Clear
            );
            Assert.True(one.Equals(two));
            Assert.Equal(one.GetHashCode(), two.GetHashCode());
            Assert.False(one.Equals(three));
            Assert.Equal(2, one.Count);
        }

        /// <summary>Two cells holding DIFFERENT things of the same kind are two stops - the identity
        /// is the thing itself and never how many of them there are.</summary>
        [Fact]
        public void TwoDifferentThingsOfTheSameKindAreNotTheSameCell()
        {
            Strip strip = new Strip("fleet:1", "fleet:2");
            int to;
            int skipped;
            Assert.True(Skip(strip, 0, 1, out to, out skipped));
            Assert.Equal(1, to);
            Assert.Equal(0, skipped);
        }

        /// <summary>West as readily as east: the walk is the direction it was handed.</summary>
        [Fact]
        public void ItWalksWestAsWellAsEast()
        {
            Strip strip = new Strip("Dusay", null, null, null);
            int to;
            int skipped;
            Assert.True(Skip(strip, 3, -1, out to, out skipped));
            Assert.Equal(0, to);
            Assert.Equal(2, skipped);
        }
    }
}

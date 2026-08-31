using System.Collections.Generic;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The registry of row kinds that stand somewhere on the map. Every failure here is a row kind
    /// that works in three of the four inventories and not the fourth - which is exactly the class of
    /// bug the table was written to end, and which is invisible until a player presses the one key
    /// that consults the list nobody updated.
    /// </summary>
    public class PlacedRowsTests
    {
        [Fact]
        public void EverySegmentIsDeclaredOnce()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (PlacedRow row in PlacedRows.All)
            {
                Assert.False(string.IsNullOrEmpty(row.Segment));
                Assert.True(seen.Add(row.Segment), "declared twice: " + row.Segment);
            }

            Assert.NotEmpty(seen);
        }

        /// <summary>A grouping answers no to everything. Saying it out loud is the point of the row:
        /// the constellation heading's own entity HAS a position, and the arming used to take it.
        /// </summary>
        [Fact]
        public void AGroupingRefusesEverything()
        {
            foreach (PlacedRow row in PlacedRows.All)
            {
                if (!row.Refuses)
                {
                    continue;
                }

                Assert.False(row.Arms);
                Assert.False(row.Leap);
                Assert.False(row.Restore);
                Assert.Equal(0, row.EnterTier);
            }

            Assert.NotNull(PlacedRows.Named("constellation"));
            Assert.True(PlacedRows.Named("constellation").Refuses);
            Assert.True(PlacedRows.Named("unexplored").Refuses);
        }

        /// <summary>A row that stands somewhere arms the cell, and every one of them is somewhere the
        /// player can be put back - the two capabilities that follow from standing anywhere at all.
        /// </summary>
        [Fact]
        public void APlacedRowArmsAndCanBeRestoredTo()
        {
            int placed = 0;
            foreach (PlacedRow row in PlacedRows.All)
            {
                if (row.Refuses)
                {
                    continue;
                }

                placed++;
                Assert.True(row.Arms, row.Segment + " stands somewhere but cannot arm the cell");
                Assert.True(row.Restore, row.Segment + " stands somewhere but is not a restore target");
                Assert.True(row.Leap, row.Segment + " stands somewhere but no leap from it is kept");
            }

            Assert.True(placed >= 7);
        }

        /// <summary>Enter's tiers are a real order: every placed kind has one, and it is inside the
        /// range the walk covers - a tier past <see cref="PlacedRows.Tiers"/> would never be reached
        /// and the kind would silently stop being enterable.</summary>
        [Fact]
        public void EveryPlacedKindHasAReachableTier()
        {
            foreach (PlacedRow row in PlacedRows.All)
            {
                if (row.Refuses)
                {
                    continue;
                }

                Assert.InRange(row.EnterTier, 1, PlacedRows.Tiers);
            }
        }

        /// <summary>The order itself, as the owner ruled it: a place, then a fleet, then the smaller
        /// movers, then the game's own annotation, then the player's.</summary>
        [Fact]
        public void TheOrderIsPlaceFleetMoversMarkerBookmark()
        {
            Assert.Equal(PlacedRows.TierPlace, PlacedRows.Named("system").EnterTier);
            Assert.Equal(PlacedRows.TierFleet, PlacedRows.Named("fleet").EnterTier);
            Assert.Equal(PlacedRows.TierMover, PlacedRows.Named("probe").EnterTier);
            Assert.Equal(PlacedRows.TierMover, PlacedRows.Named("projectile").EnterTier);
            Assert.Equal(PlacedRows.TierMover, PlacedRows.Named("pin").EnterTier);
            Assert.Equal(PlacedRows.TierMarker, PlacedRows.Named("marker").EnterTier);
            Assert.Equal(PlacedRows.TierBookmark, PlacedRows.Named("bookmark").EnterTier);

            Assert.True(PlacedRows.TierPlace < PlacedRows.TierFleet);
            Assert.True(PlacedRows.TierFleet < PlacedRows.TierMover);
            Assert.True(PlacedRows.TierMover < PlacedRows.TierMarker);
            Assert.True(PlacedRows.TierMarker < PlacedRows.TierBookmark);
        }

        /// <summary>Reading a row's kind off its key - both shapes the map builds, and the two traps:
        /// the stop's name on the head, and a key whose last token is a WORD rather than an id.
        /// </summary>
        [Fact]
        public void TheSegmentIsReadOffTheKey()
        {
            Assert.Equal("probe", PlacedRows.SegmentOf("galaxy:probe/1621"));
            Assert.Equal("pin", PlacedRows.SegmentOf("galaxy:pin/903"));
            Assert.Equal("projectile", PlacedRows.SegmentOf("galaxy:projectile/44"));
            Assert.Equal("marker", PlacedRows.SegmentOf("galaxy:marker/17"));
            Assert.Equal("fleet", PlacedRows.SegmentOf("galaxy:fleet/1622"));
            Assert.Equal("system", PlacedRows.SegmentOf("galaxy:constellation/1/system/162"));
            Assert.Equal("planet", PlacedRows.SegmentOf("galaxy:constellation/1/system/162/planet/0"));
            Assert.Equal("fleet", PlacedRows.SegmentOf("galaxy:constellation/446/system/535/fleet/9"));
            Assert.Equal("bookmark", PlacedRows.SegmentOf("galaxy:constellation/1/bookmark/1"));
            Assert.Equal("bookmark", PlacedRows.SegmentOf("galaxy:bookmark/1"));
            Assert.Equal("constellation", PlacedRows.SegmentOf("galaxy:constellation/1"));
            // The bucket's key ends in a WORD, so the word is the segment - which is what gives it a
            // declaration of its own rather than passing as another constellation.
            Assert.Equal("unexplored", PlacedRows.SegmentOf("galaxy:constellation/unexplored"));
            Assert.Null(PlacedRows.SegmentOf(null));
            Assert.Null(PlacedRows.SegmentOf(string.Empty));
        }

        /// <summary>The identity anchor, kind by kind: the four that travel and the star whose key
        /// changes under it carry their entity; the game's own annotation and the player's note do
        /// not.</summary>
        [Fact]
        public void TheMovableKindsAreAnchored()
        {
            Assert.True(PlacedRows.Named("system").Anchored);
            Assert.True(PlacedRows.Named("fleet").Anchored);
            Assert.True(PlacedRows.Named("probe").Anchored);
            Assert.True(PlacedRows.Named("projectile").Anchored);
            Assert.True(PlacedRows.Named("pin").Anchored);
            Assert.False(PlacedRows.Named("marker").Anchored);
            Assert.False(PlacedRows.Named("bookmark").Anchored);
            Assert.False(PlacedRows.Named("constellation").Anchored);
            Assert.False(PlacedRows.Named("unexplored").Anchored);
        }

        /// <summary>The column is SPENT and not merely declared: the one minting call reads it, so an
        /// anchored kind cannot be built without its anchor and an unanchored one cannot pick one up.
        /// </summary>
        [Fact]
        public void AnchorCarriesTheSubjectOnlyWhereTheTableSaysSo()
        {
            object fleet = new object();
            ControlId travelling = PlacedRows.Anchor(
                fleet,
                "galaxy:constellation/446/system/535/fleet/9"
            );
            Assert.Same(fleet, travelling.Subject);
            Assert.True(travelling.SubjectMatches(fleet));

            // The same fleet re-filed under another system: a different key, still the same node to a
            // cursor that was standing on it.
            ControlId arrived = PlacedRows.Anchor(fleet, "galaxy:constellation/1/system/162/fleet/9");
            Assert.NotEqual(travelling, arrived);
            Assert.True(arrived.SubjectMatches(fleet));

            Assert.Null(PlacedRows.Anchor(new object(), "galaxy:constellation/1/bookmark/1").Subject);
            Assert.Null(PlacedRows.Anchor(new object(), "galaxy:marker/17").Subject);
            Assert.Null(PlacedRows.Anchor(new object(), "galaxy:constellation/1").Subject);
            // Carried and undeclared segments get the bare key: this is not their question.
            Assert.Null(
                PlacedRows.Anchor(new object(), "galaxy:constellation/1/system/162/planet/0").Subject
            );
            Assert.Null(PlacedRows.Anchor(null, "galaxy:probe/1621").Subject);
        }

        /// <summary>A row kind the table does not name is CARRIED by an ancestor and answers nothing
        /// itself - which is a different answer from a grouping's refusal, and the walk that arms the
        /// cell depends on the difference.</summary>
        [Fact]
        public void ACarriedRowIsNotInTheTable()
        {
            Assert.Null(PlacedRows.Of("galaxy:constellation/1/system/162/planet/0"));
            Assert.Null(PlacedRows.Of("galaxy:constellation/1/system/162/lane/636"));
            Assert.Null(PlacedRows.Of("galaxy:constellation/1/system/162/tooltip/0"));

            PlacedRow heading = PlacedRows.Of("galaxy:constellation/1");
            Assert.NotNull(heading);
            Assert.True(heading.Refuses);
        }
    }
}

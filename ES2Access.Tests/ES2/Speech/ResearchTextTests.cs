using ES2Access.Core.Speech;
using ES2Access.ES2.Speech;
using ES2Access.Tests.Speech;
using Xunit;

namespace ES2Access.Tests.ES2.Speech
{
    /// <summary>
    /// The two things the research screen has to say in its own words: what an arc between two
    /// technologies means from the end the player is standing on, and how much of a ring is done.
    ///
    /// The arcs are the interesting half. The game draws one line between two dots and expects the
    /// player to see which way it points; spoken, that is two different sentences, and getting the
    /// direction the wrong way round would tell a player a technology they have already researched is
    /// waiting on one they have not.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class ResearchTextTests
    {
        public ResearchTextTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void ACostReductionReadsFromWhicheverEndTheCursorIsOn()
        {
            Assert.Equal(
                "Reduces the cost of Xenolinguistics",
                ResearchText.Link(ResearchText.LinkKind.CostReduction, true, "Xenolinguistics")
            );
            Assert.Equal(
                "Cost reduced by Applied Casimir Effect",
                ResearchText.Link(
                    ResearchText.LinkKind.CostReduction,
                    false,
                    "Applied Casimir Effect"
                )
            );
        }

        [Fact]
        public void ADependencyReadsBothWaysAndAnExclusionOnlyOne()
        {
            Assert.Equal(
                "Unlocks Juggernaut Frames",
                ResearchText.Link(ResearchText.LinkKind.Dependency, true, "Juggernaut Frames")
            );
            Assert.Equal(
                "Unlocked by Juggernaut Hulls",
                ResearchText.Link(ResearchText.LinkKind.Dependency, false, "Juggernaut Hulls")
            );

            // An exclusion is the same fact from both dots, so it says the same thing at both.
            Assert.Equal(
                "Mutually exclusive with Nanorobotics",
                ResearchText.Link(ResearchText.LinkKind.Exclusion, true, "Nanorobotics")
            );
            Assert.Equal(
                "Mutually exclusive with Nanorobotics",
                ResearchText.Link(ResearchText.LinkKind.Exclusion, false, "Nanorobotics")
            );
        }

        [Fact]
        public void AnArcToNothingSaysNothing()
        {
            Assert.Null(ResearchText.Link(ResearchText.LinkKind.CostReduction, true, null));
            Assert.Null(ResearchText.Link(ResearchText.LinkKind.Exclusion, false, string.Empty));
        }

        /// <summary>Every arc a dot has, read one after another when the cursor lands on it - the
        /// owner's ruling that a line between two dots is worth hearing, not only worth reviewing.
        /// </summary>
        [Fact]
        public void EveryArcOfADotReadsAsOneList()
        {
            Assert.Equal(
                "Cost reduced by Xenobiology, Reduces the cost of Graviton Research, "
                    + "Mutually exclusive with Nanorobotics",
                ResearchText.Relationships(
                    new[]
                    {
                        ResearchText.Link(ResearchText.LinkKind.CostReduction, false, "Xenobiology"),
                        ResearchText.Link(
                            ResearchText.LinkKind.CostReduction,
                            true,
                            "Graviton Research"
                        ),
                        ResearchText.Link(ResearchText.LinkKind.Exclusion, true, "Nanorobotics"),
                    }
                )
            );
        }

        /// <summary>A dot with no arcs says nothing rather than a stray separator - most of the wheel
        /// is joined to nothing at all.</summary>
        [Fact]
        public void ADotJoinedToNothingSaysNothing()
        {
            Assert.Null(ResearchText.Relationships(null));
            Assert.Null(ResearchText.Relationships(new string[0]));
            Assert.Equal(
                "Unlocks Juggernaut Frames",
                ResearchText.Relationships(
                    new[]
                    {
                        ResearchText.Link(ResearchText.LinkKind.Dependency, true, "Juggernaut Frames"),
                    }
                )
            );
        }

        /// <summary>The same two facts wherever a technology is read - on its dot, and in the list
        /// of the ones the game is recommending. A turn count is not one of them: the wheel draws
        /// none anywhere, so one computed for an unqueued technology would be the mod's own
        /// invention.</summary>
        [Fact]
        public void WhatATechnologyWillTakeSaysOnlyWhatThereIsToSay()
        {
            Assert.Equal("112 Science, position 2", ResearchText.Progress("112 Science", 1));

            // Nothing queued: just the price.
            Assert.Equal("112 Science", ResearchText.Progress("112 Science", -1));

            // Already researched - the game shows no cost for it, so neither do we.
            Assert.Equal("position 1", ResearchText.Progress(null, 0));
            Assert.Null(ResearchText.Progress(null, -1));
        }

        /// <summary>
        /// The deed marker on a ring is painted in one of the four technology-state colours, and each
        /// of those colours has a word on the key panel. Which word depends on three things in a fixed
        /// order, and getting the order wrong would tell a player a race is still open after another
        /// empire has won it.
        /// </summary>
        [Fact]
        public void ADeedNobodyHasUnlockedTheStageForReadsLocked()
        {
            Assert.Equal(
                "NotAvailable",
                ResearchText.DeedStateName(false, false, ResearchText.DeedProgress.InProgress)
            );

            // Even a deed already decided says nothing while it is out of sight.
            Assert.Equal(
                "NotAvailable",
                ResearchText.DeedStateName(false, true, ResearchText.DeedProgress.Completed)
            );
        }

        [Fact]
        public void AVisibleDeedReadsTheStateItIsPaintedIn()
        {
            Assert.Equal(
                "Available",
                ResearchText.DeedStateName(true, true, ResearchText.DeedProgress.InProgress)
            );
            Assert.Equal(
                "Researched",
                ResearchText.DeedStateName(true, true, ResearchText.DeedProgress.Completed)
            );
            Assert.Equal(
                "Disabled",
                ResearchText.DeedStateName(true, true, ResearchText.DeedProgress.Failed)
            );

            // Visible because someone else unlocked the stage, but not this empire's to attempt yet:
            // the race is on and the player is not in it.
            Assert.Equal(
                "NotAvailable",
                ResearchText.DeedStateName(true, false, ResearchText.DeedProgress.InProgress)
            );

            // A completed or failed deed is decided for everyone, whoever unlocked the stage.
            Assert.Equal(
                "Researched",
                ResearchText.DeedStateName(true, false, ResearchText.DeedProgress.Completed)
            );
            Assert.Equal(
                "Disabled",
                ResearchText.DeedStateName(true, false, ResearchText.DeedProgress.Failed)
            );
        }

        [Fact]
        public void ADeedTakenBySomeoneElseNamesThem()
        {
            Assert.Equal("won by Sophons", ResearchText.DeedWinner("Sophons"));
            Assert.Null(ResearchText.DeedWinner(null));
            Assert.Null(ResearchText.DeedWinner(string.Empty));
        }
    }
}

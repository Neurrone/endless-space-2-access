using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Landing on a control that is not declared yet: finding what it hangs under from its id alone,
    /// opening the whole path to it, and giving up on one that never appears.
    /// </summary>
    public class FocusReachTests
    {
        // ---- reading the ancestry out of a key ----

        [Fact]
        public void AncestorKeysAreThePathHeadsDeepestFirst()
        {
            IList<object> keys = KeyGraph.AncestorKeys("galaxy:system/548/planet/0/action/0");
            Assert.Equal(
                new object[]
                {
                    "galaxy:system/548/planet/0/action",
                    "galaxy:system/548/planet/0",
                    "galaxy:system/548/planet",
                    "galaxy:system/548",
                    "galaxy:system",
                },
                keys
            );
        }

        [Fact]
        public void AKeyWithNoPathHasNoAncestors()
        {
            Assert.Empty(KeyGraph.AncestorKeys("hud:view-title"));
            Assert.Empty(KeyGraph.AncestorKeys(new object()));
            Assert.Empty(KeyGraph.AncestorKeys(null));
        }

        /// <summary>A level of the tree the keys do not mention - the scan lens's owner headings, whose
        /// stars keep the keys the ordinary map gives them - is named by the page and lands OUTERMOST,
        /// so a landing opens it after everything the key does say.</summary>
        [Fact]
        public void APageMayNameAGroupingTheKeysDoNotMention()
        {
            try
            {
                KeyGraph.GroupingAncestor = key =>
                    (string)key == "galaxy:constellation/1/system/162"
                        ? "galaxy:owner/none"
                        : null;

                Assert.Equal(
                    new object[]
                    {
                        "galaxy:constellation/1/system",
                        "galaxy:constellation/1",
                        "galaxy:constellation",
                        "galaxy:owner/none",
                    },
                    KeyGraph.AncestorKeys("galaxy:constellation/1/system/162")
                );

                // Asked about the path ancestors too, so a row deep inside such a member gets the same
                // heading as its star.
                Assert.Contains(
                    "galaxy:owner/none",
                    KeyGraph.AncestorKeys("galaxy:constellation/1/system/162/planet/0")
                );

                // And nothing at all for a key it does not name.
                Assert.DoesNotContain(
                    "galaxy:owner/none",
                    KeyGraph.AncestorKeys("galaxy:constellation/2/system/9")
                );
            }
            finally
            {
                KeyGraph.Reset();
            }
        }

        [Fact]
        public void ASiblingWhoseKeyStartsTheSameIsNotAnAncestor()
        {
            // "galaxy:system/5" is a raw string prefix of "galaxy:system/548/..." and must not claim it.
            GraphRender render = Renderer(b =>
            {
                b.AddItem(new SyntheticNode(Id("galaxy:system/5"), Vt("Xiu")));
                b.AddItem(new SyntheticNode(Id("galaxy:system/548"), Vt("Bao")));
            })();

            GraphNode found = KeyGraph.DeepestDeclaredAncestor(
                render,
                Id("galaxy:system/548/planet/0")
            );
            Assert.Equal("galaxy:system/548", Key(found));
        }

        [Fact]
        public void TheDEEPESTDeclaredAncestorIsTheOneFound()
        {
            GraphState state = new GraphState();
            state.Expanded.Add(Id("s"));
            GraphRender render = Renderer(
                b =>
                {
                    b.BeginGroup(new SyntheticNode(Id("s"), Vt("System")));
                    b.AddItem(new SyntheticNode(Id("s/planet/0"), Vt("Planet")));
                    b.EndGroup();
                },
                state
            )();

            Assert.Equal(
                "s/planet/0",
                Key(KeyGraph.DeepestDeclaredAncestor(render, Id("s/planet/0/action/0")))
            );
        }

        [Fact]
        public void NothingLeadingToItAnswersNull()
        {
            GraphRender render = Renderer(b => b.AddItem(new SyntheticNode(Id("a"), Vt("A"))))();
            Assert.Null(KeyGraph.DeepestDeclaredAncestor(render, Id("b/c/d")));
        }

        // ---- opening the way ----

        /// <summary>A two-level tree: a system holding planets, each planet holding actions - the shape
        /// the galaxy declares, and the shape a constellation layer adds a level above.</summary>
        private static KeyGraph Tree(GraphState state)
        {
            return new KeyGraph(
                Renderer(
                    b =>
                    {
                        b.AddItem(new SyntheticNode(Id("top"), Vt("Top")));
                        b.BeginGroup(new SyntheticNode(Id("c"), Vt("Constellation")));
                        b.BeginGroup(new SyntheticNode(Id("c/system/1"), Vt("System")));
                        b.BeginGroup(new SyntheticNode(Id("c/system/1/planet/0"), Vt("Planet")));
                        b.AddItem(new SyntheticNode(Id("c/system/1/planet/0/action/0"), Vt("Colonize")));
                        b.EndGroup();
                        b.EndGroup();
                        b.EndGroup();
                    },
                    state
                ),
                state
            );
        }

        [Fact]
        public void ReachOpensTheWholePathSoOneBuildProducesTheControl()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            ControlId target = Id("c/system/1/planet/0/action/0");

            g.Rerender();
            Assert.Equal(ReachStep.Opened, g.Reach(target));
            Assert.Contains(Id("c"), state.Expanded);
            Assert.Contains(Id("c/system/1"), state.Expanded);
            Assert.Contains(Id("c/system/1/planet/0"), state.Expanded);

            g.Rerender();
            Assert.Equal(ReachStep.Present, g.Reach(target));
            Assert.True(g.Focus(target));
        }

        /// <summary>The levels below the deepest declared ancestor are opened even when there is nothing
        /// to open at that ancestor itself: they are shut groups the moment the game draws them, and the
        /// answer is still "wait" for the ancestor's own child to appear.</summary>
        [Fact]
        public void TheLevelsBelowAnAlreadyOpenAncestorArePreOpenedToo()
        {
            GraphState state = new GraphState();
            state.Expanded.Add(Id("c"));
            KeyGraph g = Tree(state);

            g.Rerender();
            Assert.Equal(ReachStep.Waiting, g.Reach(Id("c/system/2/planet/0/action/0")));
            Assert.Contains(Id("c/system/2"), state.Expanded);
            Assert.Contains(Id("c/system/2/planet/0"), state.Expanded);
        }

        [Fact]
        public void ReachOpensThroughTheGroupsOwnExpandHook()
        {
            // The galaxy's system node overrides OnExpand (it flies the camera in as well as flipping
            // the state), and an auto-expansion must run that rather than the engine's own bookkeeping.
            GraphState state = new GraphState();
            int opened = 0;
            KeyGraph g = new KeyGraph(
                () =>
                {
                    GraphBuilder b = new GraphBuilder(state.Expanded);
                    NodeVtable header = Vt("System");
                    header.OnExpand = () =>
                    {
                        opened++;
                        state.Expanded.Add(Id("s"));
                    };
                    b.BeginGroup(new SyntheticNode(Id("s"), header));
                    b.AddItem(new SyntheticNode(Id("s/planet/0"), Vt("Planet")));
                    b.EndGroup();
                    return b.Build();
                },
                state
            );

            g.Rerender();
            Assert.Equal(ReachStep.Opened, g.Reach(Id("s/planet/0")));
            Assert.Equal(1, opened);
            g.Rerender();
            Assert.Equal(ReachStep.Present, g.Reach(Id("s/planet/0")));
        }

        /// <summary>A level pre-opened by key that turns out not to consult the expansion set - it is
        /// declared with a state of its own, or an expand hook - is simply opened the ordinary way once
        /// the build declares it, and the landing still arrives.</summary>
        [Fact]
        public void AnUndeclaredLevelThatIgnoresTheSetIsOpenedThroughItsOwnHook()
        {
            GraphState state = new GraphState();
            bool inner = false;
            KeyGraph g = new KeyGraph(
                () =>
                {
                    GraphBuilder b = new GraphBuilder(state.Expanded);
                    b.BeginGroup(new SyntheticNode(Id("s"), Vt("System")));
                    NodeVtable card = Vt("Card");
                    card.OnExpand = () => inner = true;
                    b.BeginGroup(new SyntheticNode(Id("s/card"), card), inner);
                    b.AddItem(new SyntheticNode(Id("s/card/action/0"), Vt("Colonize")));
                    b.EndGroup();
                    b.EndGroup();
                    return b.Build();
                },
                state
            );
            ControlId target = Id("s/card/action/0");

            // The system is all the first build declares, so "s/card" is pre-opened by key - which the
            // group ignores.
            g.Rerender();
            Assert.Equal(ReachStep.Opened, g.Reach(target));
            Assert.False(inner);

            g.Rerender();
            Assert.Equal(ReachStep.Opened, g.Reach(target));
            Assert.True(inner);

            g.Rerender();
            Assert.Equal(ReachStep.Present, g.Reach(target));
        }

        // ---- handing back what a landing opened by key alone ----

        [Fact]
        public void CleanUpTakesBackOnlyTheLevelsNoNodeCarries()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            FocusRequest request = new FocusRequest(Id("c/system/1/planet/0/action/0"), true);

            g.Rerender();
            Assert.Equal(ReachStep.Opened, g.Reach(request.Id, request));
            g.Rerender();
            Assert.Equal(ReachStep.Present, g.Reach(request.Id, request));

            g.ReleaseOpenedPath(request);

            // The real groups the landing opened stay open; the path heads nothing declares - "c/system",
            // and the "action" level the actions hang under - are taken back out.
            Assert.Contains(Id("c/system/1"), state.Expanded);
            Assert.Contains(Id("c/system/1/planet/0"), state.Expanded);
            Assert.DoesNotContain(Id("c/system"), state.Expanded);
            Assert.DoesNotContain(Id("c/system/1/planet/0/action"), state.Expanded);
            Assert.Null(request.OpenedPath);
        }

        [Fact]
        public void CleanUpLeavesAloneALevelThatWasAlreadyExpanded()
        {
            GraphState state = new GraphState();
            state.Expanded.Add(Id("c/system"));
            KeyGraph g = Tree(state);
            FocusRequest request = new FocusRequest(Id("c/system/1/planet/0/action/0"), true);

            g.Rerender();
            g.Reach(request.Id, request);
            g.ReleaseOpenedPath(request);

            Assert.Contains(Id("c/system"), state.Expanded);
        }

        /// <summary>A landing given up on - the budget ran out, or the player moved and cancelled it -
        /// leaves nothing of its path behind.</summary>
        [Fact]
        public void GivingUpOnALandingTakesBackTheWholePath()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            FocusRequest request = new FocusRequest(Id("c/system/1/planet/0/action/0"), true);

            g.Rerender();
            Assert.Equal(ReachStep.Opened, g.Reach(request.Id, request));
            g.ReleaseOpenedPath(request);

            // "c" opened as a group through the ordinary expansion and stays; nothing the request added
            // by key does.
            Assert.Equal(new[] { Id("c") }, state.Expanded);
        }

        [Fact]
        public void AnAncestorThatIsAlreadyOpenIsWaitedOnRatherThanReopened()
        {
            // A planet with no card is a plain row, not a group: its actions do not exist yet and there
            // is nothing to open. The answer is "wait", not "unreachable".
            GraphState state = new GraphState();
            state.Expanded.Add(Id("s"));
            KeyGraph g = new KeyGraph(
                Renderer(
                    b =>
                    {
                        b.BeginGroup(new SyntheticNode(Id("s"), Vt("System")));
                        b.AddItem(new SyntheticNode(Id("s/planet/0"), Vt("Planet")));
                        b.EndGroup();
                    },
                    state
                ),
                state
            );

            g.Rerender();
            Assert.Equal(ReachStep.Waiting, g.Reach(Id("s/planet/0/action/0")));
        }

        [Fact]
        public void AnIdNothingLeadsToIsUnreachable()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            g.Rerender();
            Assert.Equal(ReachStep.Unreachable, g.Reach(Id("elsewhere/thing/0")));
            Assert.Empty(state.Expanded);
        }

        // ---- the budget ----

        [Fact]
        public void APresentControlIsLandedOnAtOnce()
        {
            FocusRequest request = new FocusRequest(Id("a"), true);
            Assert.Equal(FocusOutcome.Land, request.Step(ReachStep.Present, 0.016));
            Assert.Equal(FocusRequest.DefaultSeconds, request.SecondsLeft);
        }

        [Fact]
        public void AnUnreachableControlIsDroppedOnTheFirstFrame()
        {
            FocusRequest request = new FocusRequest(Id("a"), true);
            Assert.Equal(FocusOutcome.Drop, request.Step(ReachStep.Unreachable, 0.016));
        }

        /// <summary>Opening is not waiting: how many builds a cascade takes is the mod's own business,
        /// and at a few builds a second a cascade charged for them would never arrive.</summary>
        [Fact]
        public void OpeningSpendsNoWaitingTime()
        {
            FocusRequest request = new FocusRequest(Id("a/b/c"), true, 1.0);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Opened, 0.4));
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Opened, 0.4));
            Assert.Equal(1.0, request.SecondsLeft);
        }

        /// <summary>A group that answers "opened" for ever - its hook declines, so the level never
        /// opens - is given up on, because nothing else would stop it.</summary>
        [Fact]
        public void OpeningIsCappedByTheLevelsTheIdNames()
        {
            FocusRequest request = new FocusRequest(Id("a/b"), true);
            Assert.Equal(3, request.OpensLeft);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Opened, 0.0));
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Opened, 0.0));
            Assert.Equal(FocusOutcome.Drop, request.Step(ReachStep.Opened, 0.0));
        }

        [Fact]
        public void WaitingSpendsSecondsAndTheyRunOut()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 1.0);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, 0.4));
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, 0.4));
            Assert.Equal(FocusOutcome.Drop, request.Step(ReachStep.Waiting, 0.4));
        }

        [Fact]
        public void ALandingStillArrivesOnTheLastOfTheBudget()
        {
            FocusRequest request = new FocusRequest(Id("a"), false, 1.0);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, 0.9));
            Assert.Equal(FocusOutcome.Land, request.Step(ReachStep.Present, 0.9));
            Assert.False(request.Announce);
        }

        // ---- suspension: the frames the request is not being worked on ----

        [Fact]
        public void ASuspendedFrameSpendsNothingOfTheBudget()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 1.0);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true, 0.4));
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true, 0.4));
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true, 0.4));
            Assert.Equal(1.0, request.SecondsLeft);
        }

        [Fact]
        public void ASuspendedRequestResumesWithTheBudgetItHad()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 1.0);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, 0.4));
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true, 0.4));
            Assert.Equal(0.6, request.SecondsLeft, 3);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, 0.4));
            Assert.Equal(FocusOutcome.Drop, request.Step(ReachStep.Waiting, 0.4));
        }

        [Fact]
        public void ASuspendedFrameDoesNotBelieveNothingLeadsThere()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 1.0);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Unreachable, true, 0.4));
            Assert.Equal(1.0, request.SecondsLeft);
            Assert.Equal(FocusOutcome.Drop, request.Step(ReachStep.Unreachable, 0.4));
        }

        /// A landing announces itself once, and what a control SAYS depends on the view it is read
        /// in - a galaxy row reads the far view's version of itself while the camera is still flying.
        /// So a suspended frame holds even a control that is already there, and spends no budget doing
        /// it (owner ruling, batch 7).
        [Fact]
        public void ASuspendedFrameHoldsEvenAControlThatIsThere()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 1.0);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Present, true, 0.4));
            Assert.Equal(1.0, request.SecondsLeft);
            Assert.Equal(FocusOutcome.Land, request.Step(ReachStep.Present, false, 0.4));
        }

        // ---- ownership: whose landing is it ----

        [Fact]
        public void ARequestRemembersTheScreenThatAskedForIt()
        {
            object screen = new object();
            FocusRequest request = new FocusRequest(Id("a"), true, screen);
            Assert.Same(screen, request.Owner);
            Assert.Equal(FocusRequest.DefaultSeconds, request.SecondsLeft);
        }

        [Fact]
        public void ARequestWithNoOwnerNamesNobody()
        {
            Assert.Null(new FocusRequest(Id("a"), true).Owner);
            Assert.Null(new FocusRequest(Id("a"), true, 5.0).Owner);
        }
    }
}

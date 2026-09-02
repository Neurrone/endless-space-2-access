using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Landing on a control that is not declared yet: finding what it hangs under from its id alone,
    /// opening one level per build until it appears, and giving up on one that never will.
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

        // ---- opening the way, one level per build ----

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
        public void ReachOpensOneLevelPerBuildUntilTheControlIsThere()
        {
            GraphState state = new GraphState();
            KeyGraph g = Tree(state);
            ControlId target = Id("c/system/1/planet/0/action/0");

            g.Rerender();
            Assert.Equal(ReachStep.Opened, g.Reach(target));
            Assert.Contains(Id("c"), state.Expanded);

            g.Rerender();
            Assert.Equal(ReachStep.Opened, g.Reach(target));
            Assert.Contains(Id("c/system/1"), state.Expanded);

            g.Rerender();
            Assert.Equal(ReachStep.Opened, g.Reach(target));
            Assert.Contains(Id("c/system/1/planet/0"), state.Expanded);

            g.Rerender();
            Assert.Equal(ReachStep.Present, g.Reach(target));
            Assert.True(g.Focus(target));
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
            Assert.Equal(FocusOutcome.Land, request.Step(ReachStep.Present));
            Assert.Equal(FocusRequest.DefaultFrames, request.FramesLeft);
        }

        [Fact]
        public void AnUnreachableControlIsDroppedOnTheFirstFrame()
        {
            FocusRequest request = new FocusRequest(Id("a"), true);
            Assert.Equal(FocusOutcome.Drop, request.Step(ReachStep.Unreachable));
        }

        [Fact]
        public void OpeningAndWaitingBothSpendTheBudgetAndItRunsOut()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 3);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Opened));
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting));
            Assert.Equal(FocusOutcome.Drop, request.Step(ReachStep.Waiting));
        }

        [Fact]
        public void ALandingStillArrivesOnTheLastFrameOfTheBudget()
        {
            FocusRequest request = new FocusRequest(Id("a"), false, 2);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Opened));
            Assert.Equal(FocusOutcome.Land, request.Step(ReachStep.Present));
            Assert.False(request.Announce);
        }

        // ---- suspension: the frames the request is not being worked on ----

        [Fact]
        public void ASuspendedFrameSpendsNothingOfTheBudget()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 3);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true));
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true));
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true));
            Assert.Equal(3, request.FramesLeft);
        }

        [Fact]
        public void ASuspendedRequestResumesWithTheBudgetItHad()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 3);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting));
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting, true));
            Assert.Equal(2, request.FramesLeft);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Waiting));
            Assert.Equal(FocusOutcome.Drop, request.Step(ReachStep.Waiting));
        }

        [Fact]
        public void ASuspendedFrameDoesNotBelieveNothingLeadsThere()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 3);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Unreachable, true));
            Assert.Equal(3, request.FramesLeft);
            Assert.Equal(FocusOutcome.Drop, request.Step(ReachStep.Unreachable));
        }

        /// A landing announces itself once, and what a control SAYS depends on the view it is read
        /// in - a galaxy row reads the far view's version of itself while the camera is still flying.
        /// So a suspended frame holds even a control that is already there, and spends no budget doing
        /// it (owner ruling, batch 7).
        [Fact]
        public void ASuspendedFrameHoldsEvenAControlThatIsThere()
        {
            FocusRequest request = new FocusRequest(Id("a"), true, 3);
            Assert.Equal(FocusOutcome.Wait, request.Step(ReachStep.Present, true));
            Assert.Equal(3, request.FramesLeft);
            Assert.Equal(FocusOutcome.Land, request.Step(ReachStep.Present, false));
        }

        // ---- ownership: whose landing is it ----

        [Fact]
        public void ARequestRemembersTheScreenThatAskedForIt()
        {
            object screen = new object();
            FocusRequest request = new FocusRequest(Id("a"), true, screen);
            Assert.Same(screen, request.Owner);
            Assert.Equal(FocusRequest.DefaultFrames, request.FramesLeft);
        }

        [Fact]
        public void ARequestWithNoOwnerNamesNobody()
        {
            Assert.Null(new FocusRequest(Id("a"), true).Owner);
            Assert.Null(new FocusRequest(Id("a"), true, 5).Owner);
        }
    }
}

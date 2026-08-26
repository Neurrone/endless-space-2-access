using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The builder's EXISTENCE gate: the injected predicate that takes "is the game really drawing
    /// this" away from every screen's own walk. The engine side of it (which widget a node stands on,
    /// and the paint test) lives outside Core and is measured against the live game; what is settled
    /// here is what the BUILDER does with a yes - and, just as much, that a builder given no predicate
    /// behaves exactly as it always did.
    /// </summary>
    public class GraphGateTests
    {
        /// <summary>Drops the named keys, as the engine's carrier gate would.</summary>
        private static Func<ControlId, NodeVtable, bool> Drop(params string[] keys)
        {
            HashSet<string> dropped = new HashSet<string>(keys);
            return (id, vtable) => dropped.Contains((string)id.StructuralKey);
        }

        private static GraphRender Declare(Func<ControlId, NodeVtable, bool> drops)
        {
            GraphBuilder b = new GraphBuilder(null, drops);
            b.StartRow("bar").AddItem(Id("a"), Vt("A")).AddItem(Id("b"), Vt("B")).EndRow();
            b.StartRow("bar").AddItem(Id("c"), Vt("C")).AddItem(Id("d"), Vt("D")).EndRow();
            return b.Build();
        }

        [Fact]
        public void WithNoPredicateNothingChanges()
        {
            GraphRender gated = Declare(null);
            GraphRender plain = Declare(null);
            Assert.Equal(4, gated.Order.Count);
            Assert.Equal("b", DestKey(Node(gated, "a"), GraphDir.Right));
            Assert.Equal("c", DestKey(Node(gated, "a"), GraphDir.Down));
            Assert.Equal(plain.Order.Count, gated.Order.Count);
        }

        [Fact]
        public void APredicateThatSaysNoDropsNothing()
        {
            GraphRender r = Declare((id, vtable) => false);
            Assert.Equal(4, r.Order.Count);
        }

        [Fact]
        public void ADroppedControlIsNeverDeclared()
        {
            GraphRender r = Declare(Drop("b"));
            Assert.Null(Node(r, "b"));
            Assert.Equal(3, r.Order.Count);
        }

        [Fact]
        public void ARowKeepsItsSurvivingCellsAndTheirWiring()
        {
            GraphRender r = Declare(Drop("b"));
            Assert.Null(DestKey(Node(r, "a"), GraphDir.Right));
            Assert.Equal("c", DestKey(Node(r, "a"), GraphDir.Down));
            Assert.Equal("d", DestKey(Node(r, "c"), GraphDir.Right));
        }

        // Dropping a row's last cell used to throw "Row cannot be empty", which BuildRender catches by
        // blanking the whole render - a table whose surplus rows all retired at once would have taken
        // the screen down with it, silently.
        [Fact]
        public void ARowTheGateEmptiedIsSuppressedRatherThanAFailure()
        {
            GraphRender r = Declare(Drop("a", "b"));
            Assert.Null(Node(r, "a"));
            Assert.Null(Node(r, "b"));
            Assert.Equal(2, r.Order.Count);
            Assert.Null(DestKey(Node(r, "c"), GraphDir.Up));
        }

        [Fact]
        public void ARowNOBODYEmptiedIsStillAFailure()
        {
            GraphBuilder b = new GraphBuilder(null, Drop("nothing here"));
            b.StartRow();
            Assert.Throws<InvalidOperationException>(() => b.EndRow());
        }

        [Fact]
        public void EverythingDroppedBuildsNothingAtAll()
        {
            Assert.Null(Declare(Drop("a", "b", "c", "d")));
        }

        // A section header the gate dropped is a section the game is not drawing, so its contents are
        // not being drawn either - and children left behind would hang off whatever parent happened to
        // be outside the group.
        [Fact]
        public void ADroppedGroupHeaderTakesItsSubtreeWithIt()
        {
            GraphBuilder b = new GraphBuilder(null, Drop("g"));
            b.AddItem(Id("before"), Vt("Before"));
            b.BeginGroup(Id("g"), Vt("Group"), true);
            b.AddItem(Id("child"), Vt("Child"));
            b.BeginGroup(Id("inner"), Vt("Inner"), true);
            b.AddItem(Id("deep"), Vt("Deep"));
            b.EndGroup();
            b.EndGroup();
            b.AddItem(Id("after"), Vt("After"));

            GraphRender r = b.Build();
            Assert.Null(Node(r, "g"));
            Assert.Null(Node(r, "child"));
            Assert.Null(Node(r, "inner"));
            Assert.Null(Node(r, "deep"));
            Assert.Equal(2, r.Order.Count);
            Assert.Equal("after", DestKey(Node(r, "before"), GraphDir.Down));
        }

        [Fact]
        public void AnExpandedGroupKeepsTheChildrenTheGateLeft()
        {
            GraphBuilder b = new GraphBuilder(null, Drop("gone"));
            b.BeginGroup(Id("g"), Vt("Group"), true);
            b.AddItem(Id("gone"), Vt("Gone"));
            b.AddItem(Id("kept"), Vt("Kept"));
            b.EndGroup();

            GraphRender r = b.Build();
            Assert.Null(Node(r, "gone"));
            Assert.Same(Node(r, "g"), Node(r, "kept").Parent);
            Assert.Equal("kept", DestKey(Node(r, "g"), GraphDir.Down));
        }

        [Fact]
        public void ADroppedRawNodeLosesTheEdgesNamingIt()
        {
            GraphBuilder b = new GraphBuilder(null, Drop("gone"));
            b.AddNode(Id("here"), Vt("Here"));
            b.AddNode(Id("gone"), Vt("Gone"));
            b.Connect(Id("here"), GraphDir.Right, Id("gone"));

            GraphRender r = b.Build();
            Assert.Null(Node(r, "gone"));
            Assert.Null(DestKey(Node(r, "here"), GraphDir.Right));
        }

        // A malformed declaration is a bug whether or not the game is drawing it, so the gate is asked
        // after the well-formedness checks and never gets to hide one.
        [Fact]
        public void AMalformedDeclarationStillThrowsEvenWhenItWouldBeDropped()
        {
            GraphBuilder b = new GraphBuilder(null, Drop("bad"));
            Assert.Throws<ArgumentException>(() => b.AddItem(Id("bad"), new NodeVtable()));
        }

        // A dropped node never existed, so it claims no id: the duplicate check is about what the
        // screen really declared.
        [Fact]
        public void ADroppedIdIsNotClaimed()
        {
            GraphBuilder b = new GraphBuilder(null, Drop("twice"));
            b.AddItem(Id("twice"), Vt("First"));
            b.AddItem(Id("twice"), Vt("Second"));
            Assert.Null(b.Build());
        }

        [Fact]
        public void PositionsAreCountedOverTheSurvivors()
        {
            GraphBuilder b = new GraphBuilder(null, Drop("two"));
            b.AddItem(Id("one"), Vt("One"));
            b.AddItem(Id("two"), Vt("Two"));
            b.AddItem(Id("three"), Vt("Three"));

            GraphRender r = b.Build();
            Assert.Equal(2, Node(r, "three").PositionIndex);
            Assert.Equal(2, Node(r, "three").PositionCount);
        }
    }
}

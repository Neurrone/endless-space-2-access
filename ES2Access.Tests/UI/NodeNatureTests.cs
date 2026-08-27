using System;
using System.Reflection;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The two node natures. A control either has something on the screen whose paint state vouches
    /// for it, or it has nothing to be asked - and which of the two it is has to be a fact the
    /// compiler holds a declaration to, because a nullable slot made "I forgot to say" and "there is
    /// nothing to say" the same value.
    /// </summary>
    public class NodeNatureTests
    {
        [Fact]
        public void ADrawnNodeCannotBeDeclaredWithoutTheWidgetThatVouchesForIt()
        {
            Assert.Throws<ArgumentNullException>(
                () => new DrawnNode(Id("a"), Vt("A"), null)
            );
        }

        [Fact]
        public void ADrawnNodeKeepsWhatItWasDeclaredWith()
        {
            object widget = new object();
            DrawnNode node = new DrawnNode(Id("a"), Vt("A"), widget);
            Assert.Same(widget, node.DrawnBy);
            Assert.Equal("a", node.Id.StructuralKey);
        }

        // Not "its evidence is null" - it has no evidence MEMBER, so no walk can read one off it and
        // no walk can forget to fill one in.
        [Fact]
        public void ASyntheticNodeHasNoEvidenceOfAnyKind()
        {
            foreach (
                MemberInfo member in typeof(SyntheticNode).GetMembers(
                    BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Instance | BindingFlags.Static
                        | BindingFlags.DeclaredOnly
                )
            )
            {
                Assert.True(
                    member is ConstructorInfo,
                    "SyntheticNode declares " + member.Name + "; it must declare nothing but a constructor"
                );
            }
        }

        [Fact]
        public void BothNaturesAreDeclarationsAndCarryIdentityAndBehaviour()
        {
            NodeDeclaration drawn = new DrawnNode(Id("a"), Vt("A"), new object());
            NodeDeclaration synthetic = new SyntheticNode(Id("b"), Vt("B"));
            Assert.Equal("a", drawn.Id.StructuralKey);
            Assert.Equal("b", synthetic.Id.StructuralKey);
            Assert.NotNull(drawn.Vtable);
            Assert.NotNull(synthetic.Vtable);
        }

        [Fact]
        public void ADeclarationRefusesAMissingIdOrVtable()
        {
            Assert.Throws<ArgumentNullException>(() => new SyntheticNode(null, Vt("A")));
            Assert.Throws<ArgumentNullException>(() => new SyntheticNode(Id("a"), null));
        }

        // The gate's own shape: it is handed the declaration, so a synthetic node is one it cannot
        // even ask about, and a drawn one is asked about the widget it was declared with.
        [Fact]
        public void TheGateSeesEachNodesNatureAndOnlyDrawnOnesCarryAWidgetToTest()
        {
            object retired = new object();
            GraphBuilder b = new GraphBuilder(
                null,
                node =>
                {
                    DrawnNode drawn = node as DrawnNode;
                    return drawn != null && ReferenceEquals(drawn.DrawnBy, retired);
                }
            );
            b.AddItem(new SyntheticNode(Id("synthetic"), Vt("Synthetic")));
            b.AddItem(new DrawnNode(Id("live"), Vt("Live"), new object()));
            b.AddItem(new DrawnNode(Id("ghost"), Vt("Ghost"), retired));

            GraphRender r = b.Build();
            Assert.NotNull(Node(r, "synthetic"));
            Assert.NotNull(Node(r, "live"));
            Assert.Null(Node(r, "ghost"));
        }

        [Fact]
        public void ABuiltNodeStillAnswersForTheDeclarationItCameFrom()
        {
            object widget = new object();
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new DrawnNode(Id("a"), Vt("A"), widget));
            GraphNode node = Node(b.Build(), "a");
            DrawnNode declared = Assert.IsType<DrawnNode>(node.Declared);
            Assert.Same(widget, declared.DrawnBy);
        }

        // ---- identity is untouched by the nature ----

        // The rename of Referenced/Reference to For/Subject must not move a single key: spoken lines,
        // review buffers and remembered cursor positions are all keyed off these.
        [Fact]
        public void KeysAndEqualityAreExactlyWhatTheyWere()
        {
            object subject = new object();
            ControlId with = ControlId.For(subject, "empire:row/7");
            ControlId without = ControlId.Structural("empire:row/7");

            Assert.Equal("empire:row/7", with.StructuralKey);
            Assert.Equal("empire:row/7".GetHashCode(), with.GetHashCode());
            Assert.Equal(with, without);
            Assert.Equal(with.GetHashCode(), without.GetHashCode());
            Assert.Same(subject, with.Subject);
            Assert.Null(without.Subject);
            Assert.True(with.SubjectMatches(subject));
            Assert.False(without.SubjectMatches(subject));

            ControlId self = ControlId.ForObject(subject);
            Assert.Same(subject, self.StructuralKey);
            Assert.Same(subject, self.Subject);
        }

        // A sheet's row keys derive from the row object's hash: the same table, declared the same way,
        // must key its cells exactly as it did before the natures existed.
        [Fact]
        public void ASheetsRowKeysAreUnchanged()
        {
            object row = new object();
            GraphBuilder b = new GraphBuilder();
            GraphSheet sheet = new GraphSheet(b, "military:");
            sheet.Region("Fleets", new[] { "Name", "Ships" });
            sheet.Row(Vt("Alpha"), row, null, () => "3");
            sheet.Finish();

            GraphRender r = b.Build();
            string expected = "military:row" + row.GetHashCode();
            Assert.NotNull(Node(r, expected + "c0"));
            Assert.NotNull(Node(r, expected + "c1"));
            Assert.Same(row, Node(r, expected + "c0").Id.Subject);
            Assert.Null(Node(r, expected + "c1").Id.Subject);
        }

        // Core cannot ask a widget anything, but it does not have to: a caller that hands over the
        // widget the game draws the row as has answered the question, and the sheet passes it along so
        // the host's gate can ask it every frame. Identity is a separate question and stays the row's
        // own object - the trait, the save, the fleet - which is what survives a re-sort.
        [Fact]
        public void ASheetDeclaresItsRowsDrawnByTheWidgetTheGameDrawsThemAs()
        {
            object row = new object();
            object widget = new object();
            GraphBuilder b = new GraphBuilder();
            GraphSheet sheet = new GraphSheet(b, "military:");
            sheet.Region("Fleets", new[] { "Name" });
            sheet.Row(Vt("Alpha"), row, widget);
            sheet.Finish();

            GraphRender r = b.Build();
            foreach (GraphNode node in r.Order)
            {
                Assert.Same(widget, Assert.IsType<DrawnNode>(node.Declared).DrawnBy);
            }

            Assert.Same(row, r.Order[0].Id.Subject);
        }

        // Handed no widget it claims none: a sheet fed rows composed out of the game's data has
        // nothing any cell of it stands on, and the walk that enumerated the rows is where the honesty
        // lives.
        [Fact]
        public void ASheetWithNoRowWidgetDeclaresItsRowsSynthetic()
        {
            object row = new object();
            GraphBuilder b = new GraphBuilder();
            GraphSheet sheet = new GraphSheet(b, "military:");
            sheet.Region("Fleets", new[] { "Name" });
            sheet.Row(Vt("Alpha"), row, null);
            sheet.Finish();

            GraphRender r = b.Build();
            foreach (GraphNode node in r.Order)
            {
                Assert.IsType<SyntheticNode>(node.Declared);
            }
        }
    }
}

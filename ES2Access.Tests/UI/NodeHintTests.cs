using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Tests.Speech;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The USAGE HINTS a control ends its review buffer with.
    ///
    /// Three things are held here and nowhere else: that a hint's chord comes from the INJECTED
    /// renderer rather than from anything written into the sentence (which is what makes a rebind
    /// re-word every hint), that the hint names a binding INDEX and not just an action (the map's
    /// off-lane move is the second chord of the same action as the ordinary move), and that the lines
    /// land at the very END of the buffer, after everything the control itself has to say.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class NodeHintTests : IDisposable
    {
        public NodeHintTests()
        {
            ModStrings.Reset();
            GraphAnnouncer.Reset();
            NodeHints.Reset();
        }

        public void Dispose()
        {
            GraphAnnouncer.Reset();
            NodeHints.Reset();
        }

        /// <summary>A stand-in for the real formatter: two chords on one action, so the binding index
        /// is visible in the output.</summary>
        private static void InstallFakeFormatter()
        {
            NodeHints.Chord = (action, index) =>
                action == "ui.contextual"
                    ? (index == 0 ? "Backslash" : index == 1 ? "Ctrl+Backslash" : null)
                    : action == "ui.alternate" && index == 0
                        ? "Ctrl+Shift+Enter"
                        : null;
        }

        private static List<string> Buffer(NodeVtable vtable)
        {
            GraphBuilder b = new GraphBuilder();
            b.AddItem(new SyntheticNode(Id("t"), vtable));
            return NodeBuffer.Lines(Node(b.Build(), "t"));
        }

        private static NodeVtable Control()
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    Part("Dusay", AnnouncementKinds.Label),
                },
                Sections = new List<NodeSection>
                {
                    new NodeSection(() => new List<string> { "Two planets" }, TooltipMode.None),
                },
            };
        }

        [Fact]
        public void AHintReadsAsItsTemplateWithTheRenderedChordInIt()
        {
            InstallFakeFormatter();
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, ModStrings.HintMoveFleetHere, "ui.contextual");

            Assert.Equal(
                new[] { "Dusay", "Two planets", "Backslash to move the fleet here" },
                Buffer(vtable)
            );
        }

        /// <summary>The hints are the LAST lines: content first, keyboard afterwards.</summary>
        [Fact]
        public void HintsComeAfterEverythingTheControlHasToSay()
        {
            InstallFakeFormatter();
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, ModStrings.HintMoveFleetHere, "ui.contextual");
            NodeHints.Add(vtable, ModStrings.HintFreeMovement, "ui.contextual", 1);

            List<string> lines = Buffer(vtable);
            Assert.Equal("Two planets", lines[lines.Count - 3]);
            Assert.Equal("Backslash to move the fleet here", lines[lines.Count - 2]);
            Assert.Equal(
                "Ctrl+Backslash to use off-lane free movement",
                lines[lines.Count - 1]
            );
        }

        /// <summary>One hint per LINE, in declared order - never joined into one sentence.</summary>
        [Fact]
        public void EachHintIsItsOwnLine()
        {
            InstallFakeFormatter();
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, ModStrings.HintQueueFirst, "ui.alternate");
            NodeHints.Add(vtable, ModStrings.HintMoveFleetHere, "ui.contextual");

            Assert.Equal(
                new[]
                {
                    "Dusay",
                    "Two planets",
                    "Ctrl+Shift+Enter to queue it first",
                    "Backslash to move the fleet here",
                },
                Buffer(vtable)
            );
        }

        /// <summary>The chord is not in the sentence: re-rendering the SAME declaration through a
        /// different formatter re-words the hint. This is the whole point of naming an action.</summary>
        [Fact]
        public void RebindingTheActionRewordsTheHint()
        {
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, ModStrings.HintQueueFirst, "ui.alternate");

            InstallFakeFormatter();
            Assert.Contains("Ctrl+Shift+Enter to queue it first", Buffer(vtable));

            NodeHints.Chord = (action, index) => "Alt+F3";
            Assert.Contains("Alt+F3 to queue it first", Buffer(vtable));
            Assert.DoesNotContain("Ctrl+Shift+Enter to queue it first", Buffer(vtable));
        }

        /// <summary>A gate that says no takes its own line away and leaves the rest alone.</summary>
        [Fact]
        public void AGatedHintIsAbsentWhileItsGateSaysNo()
        {
            InstallFakeFormatter();
            bool possible = false;
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, ModStrings.HintMoveFleetHere, "ui.contextual");
            NodeHints.Add(
                vtable,
                ModStrings.HintFreeMovement,
                "ui.contextual",
                1,
                () => possible
            );

            Assert.DoesNotContain("Ctrl+Backslash to use off-lane free movement", Buffer(vtable));
            Assert.Contains("Backslash to move the fleet here", Buffer(vtable));

            possible = true;
            Assert.Contains("Ctrl+Backslash to use off-lane free movement", Buffer(vtable));
        }

        /// <summary>A chord the renderer cannot produce - an action with no such binding - says
        /// nothing at all, rather than a sentence with a hole in it.</summary>
        [Fact]
        public void AHintWhoseChordCannotBeRenderedIsSilent()
        {
            InstallFakeFormatter();
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, ModStrings.HintDismiss, "ui.contextual", 7);

            Assert.Equal(new[] { "Dusay", "Two planets" }, Buffer(vtable));
        }

        /// <summary>With no renderer installed - boot, teardown, a test - nothing renders. Teardown
        /// safety: a stale delegate would keep calling into an assembly nobody can reach.</summary>
        [Fact]
        public void NoRendererMeansNoHints()
        {
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, ModStrings.HintQueueFirst, "ui.alternate");

            Assert.Equal(new[] { "Dusay", "Two planets" }, Buffer(vtable));
        }

        /// <summary>A gate that throws costs its own line and nothing else - a hint is the least
        /// important thing in a buffer.</summary>
        [Fact]
        public void AThrowingGateCostsOnlyItsOwnLine()
        {
            InstallFakeFormatter();
            NodeVtable vtable = Control();
            NodeHints.Add(
                vtable,
                ModStrings.HintFreeMovement,
                "ui.contextual",
                1,
                () => { throw new InvalidOperationException("no"); }
            );
            NodeHints.Add(vtable, ModStrings.HintMoveFleetHere, "ui.contextual");

            Assert.Equal(
                new[] { "Dusay", "Two planets", "Backslash to move the fleet here" },
                Buffer(vtable)
            );
        }

        /// <summary>A translation overlay re-words the sentence while the chord stays the renderer's.
        /// </summary>
        [Fact]
        public void TheSentenceComesFromTheStringTable()
        {
            InstallFakeFormatter();
            ModStrings.Install(
                new Dictionary<string, string>
                {
                    { ModStrings.HintQueueFirst, "en premier avec {0}" },
                }
            );
            NodeVtable vtable = Control();
            NodeHints.Add(vtable, ModStrings.HintQueueFirst, "ui.alternate");

            Assert.Contains("en premier avec Ctrl+Shift+Enter", Buffer(vtable));
        }
    }
}

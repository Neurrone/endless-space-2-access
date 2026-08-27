using System;
using System.Collections.Generic;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The search scope's merge: what a page declares now, plus what it WOULD declare with its
    /// branches open, with nothing offered twice.
    /// </summary>
    public class SearchScopeTests
    {
        // A page with one open group and one closed one, built twice: as the player left it, and with
        // everything forced open. That pair is exactly what the navigator hands Extend.
        private static GraphRender Build(bool expandAll)
        {
            GraphBuilder builder = new GraphBuilder(new HashSet<ControlId>());
            builder.ExpandAll = expandAll;
            builder.BeginStop("tree");
            builder.BeginGroup(new SyntheticNode(Graphs.Id("open"), Graphs.Vt("Serpens")), expanded: true);
            builder.AddItem(new SyntheticNode(Graphs.Id("open/star"), Graphs.Vt("Dusay")));
            builder.EndGroup();
            builder.BeginGroup(new SyntheticNode(Graphs.Id("shut"), Graphs.Vt("Osulo group")), expanded: false);
            builder.AddItem(new SyntheticNode(Graphs.Id("shut/star"), Graphs.Vt("Osulo")));
            builder.BeginGroup(new SyntheticNode(Graphs.Id("shut/deep"), Graphs.Vt("Osulo I")), expanded: false);
            builder.AddItem(new SyntheticNode(Graphs.Id("shut/deep/deposit"), Graphs.Vt("Antimatter")));
            builder.EndGroup();
            builder.EndGroup();
            return builder.Build();
        }

        [Fact]
        public void ACollapsedBranchDeclaresNothingUntilTheBuildIsForcedOpen()
        {
            Assert.Null(Build(false).NodeAt(Graphs.Id("shut/deep/deposit")));
            Assert.NotNull(Build(true).NodeAt(Graphs.Id("shut/deep/deposit")));
        }

        [Fact]
        public void TheMergedScopeOffersTheBuriedControlsAndNothingTwice()
        {
            GraphRender standing = Build(false);
            GraphRender deep = Build(true);
            SearchScope scope = SearchScope.Extend(
                SearchScope.OverStop(standing, "tree"),
                standing,
                deep,
                "tree",
                node => node.Id
            );

            List<string> offered = new List<string>();
            for (int i = 0; i < scope.Count; i++)
            {
                offered.Add(Convert.ToString(scope.IdOf(i).StructuralKey));
            }

            Assert.Equal(
                new[] { "open", "open/star", "shut", "shut/star", "shut/deep", "shut/deep/deposit" },
                offered
            );
            Assert.Equal("Antimatter", scope.TextOf(scope.Count - 1));
        }

        [Fact]
        public void LandingOnABuriedControlAsksTheHostToOpenItsBranches()
        {
            GraphRender standing = Build(false);
            GraphRender deep = Build(true);
            List<string> opened = new List<string>();
            SearchScope scope = SearchScope.Extend(
                SearchScope.OverStop(standing, "tree"),
                standing,
                deep,
                "tree",
                node =>
                {
                    for (GraphNode at = node.Parent; at != null; at = at.Parent)
                    {
                        if (at.Expandable)
                        {
                            opened.Add(Convert.ToString(at.Id.StructuralKey));
                        }
                    }

                    return node.Id;
                }
            );

            ControlId landed = scope.Land(scope.Count - 1);
            Assert.Equal("shut/deep/deposit", Convert.ToString(landed.StructuralKey));
            Assert.Equal(new[] { "shut/deep", "shut" }, opened);
        }

        [Fact]
        public void AScopeThatAlreadyOffersAControlKeepsItsOwnLanding()
        {
            GraphRender standing = Build(false);
            GraphRender deep = Build(true);
            int landings = 0;
            SearchScope declared = new SearchScope(
                1,
                index => "Osulo",
                index =>
                {
                    landings++;
                    return Graphs.Id("shut/star");
                },
                index => Graphs.Id("shut/star")
            );

            SearchScope scope = SearchScope.Extend(declared, standing, deep, "tree", node => node.Id);
            List<string> offered = new List<string>();
            for (int i = 0; i < scope.Count; i++)
            {
                offered.Add(Convert.ToString(scope.IdOf(i).StructuralKey));
            }

            Assert.Single(offered, "shut/star");
            scope.Land(0);
            Assert.Equal(1, landings);
        }
    }
}

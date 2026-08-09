using System;
using System.Collections.Generic;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The type-ahead glue: what a search looks through on a screen (<see cref="SearchScope"/>), and
    /// what typing, stepping and straying do to it (<see cref="TypeAhead"/>). The matching itself is
    /// <see cref="TypeAheadSearchTests"/>; nothing here needs the game.
    /// </summary>
    public class TypeAheadTests
    {
        private static GraphRender Menu()
        {
            return Renderer(b =>
            {
                b.BeginStop("left");
                b.AddItem(Id("load"), Vt("Load Game"));
                b.AddItem(Id("license"), Vt("License"));
                b.AddItem(Id("dlc"), Vt("DLC"));
                b.BeginStop("right");
                b.AddItem(Id("lore"), Vt("Lore"));
            })();
        }

        // The host's half: focus lands where the search asks and stays there, and the announcements
        // are recorded rather than spoken.
        private sealed class Landings
        {
            public readonly TypeAhead Search = new TypeAhead();
            public readonly List<string> Landed = new List<string>();
            public readonly List<string> NoMatch = new List<string>();
            public ControlId Focus;

            public Landings()
            {
                Search.OnLand = id =>
                {
                    Focus = id;
                    Landed.Add((string)id.StructuralKey);
                    return id;
                };
                Search.OnNoMatch = text => NoMatch.Add(text);
            }

            public void Type(string text, SearchScope scope)
            {
                foreach (char c in text)
                {
                    Search.Type(c, scope);
                }
            }
        }

        // ---- what a search looks through ----

        [Fact]
        public void TheDefaultScopeIsTheFocusedStopsControls()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");

            Assert.Equal(3, scope.Count);
            Assert.Equal("Load Game", scope.TextOf(0));
            Assert.Equal("DLC", scope.TextOf(2));
            Assert.Equal(Id("license"), scope.Land(1));
        }

        [Fact]
        public void AControlCanDeclareItsOwnSearchTextAndOptOutAltogether()
        {
            GraphRender render = Renderer(b =>
            {
                NodeVtable cell = Vt("12");
                cell.SearchText = () => "Alpha";
                b.AddItem(Id("cell"), cell);

                NodeVtable heading = Vt("Turn");
                heading.ExcludeFromSearch = true;
                b.AddItem(Id("heading"), heading);
            })();

            SearchScope scope = SearchScope.OverStop(render, "stop#0");

            Assert.Equal(1, scope.Count);
            Assert.Equal("Alpha", scope.TextOf(0));
        }

        [Fact]
        public void ATabularRowOffersOneResultAtItsPrimaryCell()
        {
            GraphRender render = Renderer(b =>
            {
                GraphSheet sheet = new GraphSheet(b, "t:");
                sheet.Region("Fleets", new[] { "Ships", "Move" });
                sheet.Row(Vt("Alpha"), new object(), () => "3", () => "5");
                sheet.Row(Vt("Beta"), new object(), () => "2", () => "4");
                sheet.Finish();
            })();

            SearchScope scope = SearchScope.OverStop(render, "stop#0");

            Assert.Equal(6, render.Order.Count); // two rows of three cells
            Assert.Equal(2, scope.Count);
            Assert.Equal("Alpha", scope.TextOf(0));
            Assert.Equal("Beta", scope.TextOf(1));
        }

        // ---- typing ----

        [Fact]
        public void TypingLandsOnTheBestMatchAndNarrowingMovesOn()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            Landings host = new Landings();

            host.Type("l", scope);
            Assert.Equal(new[] { "load" }, host.Landed); // list order breaks the tier tie
            Assert.Equal(3, host.Search.ResultCount);

            host.Type("i", scope);
            Assert.Equal("license", host.Landed[host.Landed.Count - 1]);
            Assert.Equal(1, host.Search.ResultCount);
            Assert.Equal("li", host.Search.Buffer);
        }

        [Fact]
        public void RepeatingTheLetterStepsThroughItsMatches()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            Landings host = new Landings();

            host.Type("lll", scope);

            Assert.Equal(new[] { "load", "license", "dlc" }, host.Landed);
        }

        [Fact]
        public void TheResultsCycleAndTheEndsAreReachable()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            Landings host = new Landings();
            host.Type("l", scope);
            host.Landed.Clear();

            host.Search.Step(1);
            host.Search.Step(-1);
            host.Search.Step(-1); // wraps past the front
            host.Search.Last();
            host.Search.First();

            Assert.Equal(new[] { "license", "load", "dlc", "dlc", "load" }, host.Landed);
        }

        [Fact]
        public void NothingMatchedSaysSoAndMovesNobody()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            Landings host = new Landings();

            host.Type("zq", scope);

            Assert.Equal(new[] { "z", "zq" }, host.NoMatch);
            Assert.Empty(host.Landed);
            Assert.True(host.Search.IsActive);
            Assert.Equal(0, host.Search.ResultCount);
            // Nothing landed, so nothing can go stale wherever focus happens to be.
            Assert.False(host.Search.Strayed(Id("lore")));
        }

        [Fact]
        public void ACharacterWithNothingToSearchIsDroppedRatherThanRemembered()
        {
            Landings host = new Landings();

            Assert.False(host.Search.Type('l', SearchScope.OverStop(Menu(), "no such stop")));
            Assert.False(host.Search.HasBuffer);
            Assert.False(host.Search.IsActive);
        }

        // ---- staleness ----

        [Fact]
        public void FocusMovingOffTheResultMakesTheSearchStale()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            Landings host = new Landings();
            host.Type("l", scope);

            Assert.False(host.Search.Strayed(host.Focus));
            Assert.True(host.Search.Strayed(Id("lore")));

            host.Search.Clear();
            Assert.False(host.Search.Strayed(Id("lore")));
            Assert.False(host.Search.IsActive);
        }

        [Fact]
        public void ALandingThatCouldNotBeReachedLeavesNothingToGoStale()
        {
            SearchScope scope = SearchScope.OverStop(Menu(), "left");
            TypeAhead search = new TypeAhead();
            search.OnLand = id => null; // the control vanished between the render and the landing

            search.Type('l', scope);

            Assert.False(search.Strayed(Id("load")));
        }

        // ---- a screen's own scope ----

        [Fact]
        public void AScreenSuppliedScopeReplacesTheDeclaredControls()
        {
            // What the technology screen will do: search items that are not declared (a collapsed
            // branch's contents), landing by opening the branch and answering with the control.
            List<string> opened = new List<string>();
            string[] items = { "Applied Casimir Effect", "Nanorobotics", "Casimir Actuators" };
            SearchScope scope = new SearchScope(
                items.Length,
                i => items[i],
                i =>
                {
                    opened.Add(items[i]);
                    return Id("tech/" + i);
                }
            );

            Landings host = new Landings();
            host.Type("cas", scope);

            // One landing per keystroke, each doing the screen's own work of reaching the item.
            Assert.Equal(3, opened.Count);
            Assert.Equal("Casimir Actuators", opened[opened.Count - 1]);
            Assert.Equal("tech/2", host.Landed[host.Landed.Count - 1]);
            Assert.Equal(2, host.Search.ResultCount); // the mid-string match is offered second
        }
    }
}

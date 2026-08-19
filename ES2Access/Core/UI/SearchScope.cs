using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// What a type-ahead search looks through on the screen the player is on, and what landing on a
    /// result means.
    ///
    /// Three things, because that is all a search needs: how many items there are, what each of them
    /// reads as, and how to reach one. <see cref="Land"/> answers with the control focus should end
    /// up on rather than moving focus itself - the navigator owns focus and announcements, so a
    /// screen supplying its own scope can do the work only it knows about (open the branch a
    /// collapsed item is buried in) and still leave the landing to the one place that speaks.
    ///
    /// <see cref="OverStop"/> is what every screen gets without declaring anything: the controls of
    /// the Tab-stop the cursor is in. A screen overrides it (<c>Screen.TypeAheadScope</c>) only when
    /// the thing the player is searching for is not declared - a tree whose collapsed branches hold
    /// most of the items.
    /// </summary>
    public sealed class SearchScope
    {
        /// <summary>How many items there are to match against.</summary>
        public readonly int Count;

        /// <summary>What item <c>i</c> reads as - the text the player is typing at.</summary>
        public readonly Func<int, string> TextOf;

        /// <summary>Bring item <c>i</c> within reach and answer with the control to put focus on,
        /// or null when it cannot be reached. Called once per result the player lands on, so a
        /// screen may do real work here.</summary>
        public readonly Func<int, ControlId> Land;

        public SearchScope(int count, Func<int, string> textOf, Func<int, ControlId> land)
        {
            Count = count;
            TextOf = textOf;
            Land = land;
        }

        /// <summary>
        /// The default scope: every control of <paramref name="stopKey"/>, in declaration order.
        ///
        /// A tabular row contributes ONE item, its primary cell (<see cref="NodeVtable.Column"/> 0):
        /// the metadata cells all search as their row's name, so without this every row would appear
        /// once per column and stepping the results would walk cells rather than rows. The exception is
        /// a cell that searches as ITSELF (<see cref="NodeVtable.SearchesAsItself"/>) - a table whose
        /// rows have no name, where each cell is a thing of its own and the filter would make seven
        /// columns of eight unreachable by typing.
        /// </summary>
        public static SearchScope OverStop(GraphRender render, object stopKey)
        {
            List<GraphNode> nodes = new List<GraphNode>();
            if (render != null)
            {
                foreach (GraphNode node in render.Order)
                {
                    NodeVtable vtable = node.Vtable;
                    if (
                        Equals(node.StopKey, stopKey)
                        && !vtable.ExcludeFromSearch
                        && (vtable.Column <= 0 || vtable.SearchesAsItself)
                    )
                    {
                        nodes.Add(node);
                    }
                }
            }

            return new SearchScope(
                nodes.Count,
                index => TextFor(nodes[index]),
                index => nodes[index].Id
            );
        }

        /// <summary>The text a control is searched by: what it declared for the purpose, else its
        /// label - the first part of what focusing it would say. A control whose text cannot be
        /// resolved is simply not matched, rather than taking the whole search down.</summary>
        public static string TextFor(GraphNode node)
        {
            if (node == null)
            {
                return null;
            }

            try
            {
                Func<string> search = node.Vtable.SearchText;
                return search != null ? search() : GraphAnnouncer.FirstPartText(node);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using ES2Access.Core.UI.Graph;
using ES2Access.Screens;
using ES2Access.UI;

namespace ES2Access.Dev
{
    /// <summary>
    /// The focused screen's whole accessible tree, in one answer: every control the player can reach,
    /// in navigation order, each reading exactly what arriving on it would say.
    ///
    /// It exists because the alternative is a conversation. Without it, finding out what a screen
    /// offers means pressing Down over and over and reading the speech back one line at a time - which
    /// costs a request per control, cannot show what is BELOW the cursor without moving it, and
    /// changes the thing being measured (focus moves, tooltips open, the game's hover follows). This
    /// answers all of it at once and changes nothing.
    ///
    /// Two properties are what make it trustworthy, and both are structural rather than promised:
    ///
    /// <list type="bullet">
    /// <item><b>It reads like navigation sounds.</b> Nothing here composes wording. The render comes
    /// from <see cref="GraphNavigator.InspectRender"/> - the same build the navigator runs - and every
    /// line comes from <see cref="GraphAnnouncer.Compose"/>, diffed against the line above it exactly
    /// as a walk down the screen would be. So a line in this dump is the sentence a player would hear
    /// arriving there from the previous control, group headings and all, rather than a second
    /// description of the same control that could drift from the first.</item>
    /// <item><b>It cannot change what it reports.</b> The render is thrown away, the cursor is only
    /// read, and no focus/blur visual hook is called. Two identical calls therefore answer
    /// identically, which is the property a caller needs before it can compare two dumps and believe
    /// the difference.</item>
    /// </list>
    ///
    /// Every node is read inside its own try/catch: the nodes resolve live game data, and one control
    /// whose getter throws must cost its own line (<c>&lt;err: ...&gt;</c>) and nothing more - a dump
    /// that dies on the first bad control is worthless precisely when it is most needed.
    ///
    /// Main-thread only. Plain text rather than JSON: it is meant to be read.
    /// </summary>
    internal static class GraphDump
    {
        /// <summary>A screen big enough to hit this is a screen whose dump nobody can read anyway, and
        /// an unbounded dump of a graph with a wiring loop would never end.</summary>
        public const int MaxLines = 800;

        private static readonly GraphDir[] Dirs =
        {
            GraphDir.Up,
            GraphDir.Down,
            GraphDir.Left,
            GraphDir.Right,
        };

        public static string Dump(bool wantEdges, bool wantBuffers)
        {
            Sink sink = new Sink();
            GraphNavigator navigator = ModEntry.Navigator;
            Screen screen = navigator == null ? null : navigator.Screen;
            if (screen == null)
            {
                sink.Line("screen: none");
                WriteStack(sink, ModEntry.Screens);
                return sink.ToString();
            }

            sink.Line("screen: " + screen.Key + " | " + (Name(screen) ?? "(unnamed)"));

            GraphRender render = null;
            try
            {
                render = navigator.InspectRender();
            }
            catch (Exception e)
            {
                sink.Line("<err: building the screen threw: " + e.Message + ">");
            }

            if (render == null || render.Nodes.Count == 0)
            {
                sink.Line("(no controls declared - the screen has nothing on it yet)");
                return sink.ToString();
            }

            WriteNodes(sink, render, navigator.FocusedKey, wantEdges, wantBuffers);
            return sink.ToString();
        }

        /// <summary>
        /// What a named screen would offer, whether or not the player is on it: the same read-only
        /// render, built off the live game without focusing anything. So "what does the planet page
        /// declare, seen from the galaxy map" costs one request instead of a walk that would move
        /// the player there - nothing is announced, no focus visual runs, and the navigator's cursor
        /// is not so much as consulted unless this screen is the focused one, in which case the
        /// answer is the same as the plain route's.
        ///
        /// A screen the game is not showing is entitled to answer nothing: its IsActive is false and
        /// its Build may find no bound window and throw. Both are reported as the body - "screen
        /// inactive" is the honest answer to the question asked, not a server error.
        ///
        /// Gated like the player's own render unless <paramref name="ungated"/>, because the question
        /// asked here is what the screen OFFERS, and a node the gate drops is not offered. The raw
        /// declared tree - what the walk said before <see cref="NodeGate"/> answered - is the explicit
        /// request, and the difference between the two answers is that screen's drops.
        /// </summary>
        public static string DumpScreen(
            Screen screen,
            bool wantEdges,
            bool wantBuffers,
            bool ungated
        )
        {
            Sink sink = new Sink();
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null)
            {
                sink.Line("screen: " + screen.Key + " | the mod's navigator is not up");
                return sink.ToString();
            }

            bool focused = ReferenceEquals(navigator.Screen, screen);
            bool active = Active(screen);
            sink.Line(
                "screen: "
                    + screen.Key
                    + " | "
                    + (Name(screen) ?? "(unnamed)")
                    + (focused ? "" : active ? " | active, not focused" : " | not active")
                    + (ungated ? " | ungated" : "")
            );

            GraphRender render;
            try
            {
                render = navigator.InspectRender(screen, !ungated);
            }
            catch (Exception e)
            {
                sink.Line(
                    (active ? "<err: building the screen threw: " : "screen inactive: building it threw ")
                        + e.GetType().Name
                        + ": "
                        + e.Message
                        + (active ? ">" : "")
                );
                return sink.ToString();
            }

            if (render == null || render.Nodes.Count == 0)
            {
                sink.Line(
                    active
                        ? "(no controls declared - the screen has nothing on it yet)"
                        : "screen inactive: it declared no controls"
                );
                return sink.ToString();
            }

            WriteNodes(sink, render, focused ? navigator.FocusedKey : null, wantEdges, wantBuffers);
            return sink.ToString();
        }

        /// <summary>Whether the screen says the game is showing it - a screen whose predicate throws
        /// is not showing, the same judgement <see cref="ScreenManager"/> makes every frame.</summary>
        private static bool Active(Screen screen)
        {
            try
            {
                return screen.IsActive();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The registered screen keys, for the answer to a key that is not one of
        /// them.</summary>
        public static string KnownScreens(ScreenManager screens)
        {
            if (screens == null)
            {
                return "(the mod is not running)";
            }

            StringBuilder keys = new StringBuilder();
            IList<Screen> registered = screens.Registered;
            for (int i = 0; i < registered.Count; i++)
            {
                if (keys.Length > 0)
                {
                    keys.Append(", ");
                }

                keys.Append(registered[i].Key);
            }

            return keys.ToString();
        }

        private static void WriteNodes(
            Sink sink,
            GraphRender render,
            ControlId focused,
            bool wantEdges,
            bool wantBuffers
        )
        {
            object stop = null;
            object region = null;
            bool first = true;
            GraphNode previous = null;

            foreach (ControlId key in Order(render))
            {
                GraphNode node = render.NodeAt(key);
                if (node == null)
                {
                    continue;
                }

                if (first || !Equals(stop, node.StopKey))
                {
                    sink.Line("-- stop: " + Describe(node.StopKey));
                    stop = node.StopKey;
                    region = null;
                }

                if (first || !Equals(region, node.RegionKey))
                {
                    if (node.RegionKey != null)
                    {
                        sink.Line("-- region: " + Describe(node.RegionKey));
                    }
                    else if (!first)
                    {
                        sink.Line("-- region: none");
                    }

                    region = node.RegionKey;
                }

                first = false;
                bool here = focused != null && focused.Equals(node.Id);
                if (!sink.Line((here ? "> " : "  ") + Text(previous, node) + "  [" + Describe(node.Id.StructuralKey) + "]"))
                {
                    return;
                }

                previous = node;

                if (wantEdges)
                {
                    WriteEdges(sink, render, node);
                }

                if (wantBuffers)
                {
                    WriteBuffer(sink, node);
                }
            }
        }

        // The traversal order: from the start node, right until stuck queueing every down - reading
        // order within a Tab-stop - then whatever the walk could not reach (later stops have no edges
        // into them) in declaration order. Exactly the order the navigator computes for itself.
        private static List<ControlId> Order(GraphRender render)
        {
            try
            {
                return KeyGraph.ComputeOrder(render);
            }
            catch (Exception)
            {
                List<ControlId> declared = new List<ControlId>();
                foreach (GraphNode node in render.Order)
                {
                    declared.Add(node.Id);
                }

                return declared;
            }
        }

        private static string Text(GraphNode from, GraphNode node)
        {
            try
            {
                string text = GraphAnnouncer.Compose(from, node);
                return string.IsNullOrEmpty(text) ? "(says nothing)" : text;
            }
            catch (Exception e)
            {
                return "<err: " + e.Message + ">";
            }
        }

        /// <summary>
        /// Where each arrow goes from here, resolved the way the navigator resolves it: a wired edge to
        /// the node it names, and where there is none, the behavior left/right FALL BACK to - a value
        /// to adjust, a group to expand, a level to ascend out of. A value to adjust wins over a wired
        /// edge, as it does in navigation, and the shadowed edge is named so it is not mistaken for
        /// missing.
        /// </summary>
        private static void WriteEdges(Sink sink, GraphRender render, GraphNode node)
        {
            for (int i = 0; i < Dirs.Length; i++)
            {
                GraphDir dir = Dirs[i];
                string line;
                try
                {
                    line = Edge(render, node, dir);
                }
                catch (Exception e)
                {
                    line = "<err: " + e.Message + ">";
                }

                if (line != null && !sink.Line("    " + Word(dir) + " -> " + line))
                {
                    return;
                }
            }
        }

        private static string Edge(GraphRender render, GraphNode node, GraphDir dir)
        {
            Transition wired;
            node.Transitions.TryGetValue(dir, out wired);
            GraphNode destination = wired == null ? null : render.NodeAt(wired.Destination);
            bool horizontal = dir == GraphDir.Left || dir == GraphDir.Right;

            if (horizontal && node.Vtable.OnAdjust != null)
            {
                string adjust = dir == GraphDir.Right ? "adjust value up" : "adjust value down";
                return destination == null
                    ? adjust
                    : adjust + " (the edge to " + Quoted(destination) + " is shadowed)";
            }

            if (destination != null)
            {
                string crossing = string.IsNullOrEmpty(wired.Label)
                    ? ""
                    : " (crossing: " + wired.Label + ")";
                return Quoted(destination) + crossing;
            }

            if (!horizontal || !KeyGraph.InTree(node))
            {
                return null;
            }

            if (dir == GraphDir.Right)
            {
                if (node.Expandable)
                {
                    if (!node.Expanded)
                    {
                        return "expand";
                    }

                    GraphNode child = FirstChild(render, node);
                    return child == null ? "nothing to descend into" : "descend to " + Quoted(child);
                }

                return "nothing to descend into";
            }

            if (node.Expandable && node.Expanded)
            {
                return "collapse";
            }

            GraphNode ancestor = Ancestor(render, node);
            return ancestor == null ? null : "ascend to " + Quoted(ancestor);
        }

        private static GraphNode FirstChild(GraphRender render, GraphNode group)
        {
            foreach (GraphNode node in render.Order)
            {
                if (ReferenceEquals(node.Parent, group))
                {
                    return node;
                }
            }

            return null;
        }

        private static GraphNode Ancestor(GraphRender render, GraphNode node)
        {
            for (GraphNode parent = node.Parent; parent != null; parent = parent.Parent)
            {
                if (parent.Focusable && render.Nodes.ContainsKey(parent.Id))
                {
                    return render.NodeAt(parent.Id);
                }
            }

            return null;
        }

        private static void WriteBuffer(Sink sink, GraphNode node)
        {
            List<string> lines;
            try
            {
                lines = GraphNavigator.BufferLines(node);
            }
            catch (Exception e)
            {
                sink.Line("    buf: <err: " + e.Message + ">");
                return;
            }

            foreach (string line in lines)
            {
                if (!sink.Line("    buf: " + line))
                {
                    return;
                }
            }
        }

        /// <summary>What the mod believes is on screen when none of it is navigable - the question a
        /// caller who got "screen: none" is actually asking.</summary>
        private static void WriteStack(Sink sink, ScreenManager screens)
        {
            if (screens == null)
            {
                sink.Line("stack: the mod is not running");
                return;
            }

            IList<Screen> stack = screens.Stack;
            if (stack.Count == 0)
            {
                sink.Line("stack: empty - no screen of ours matches what the game is showing");
                return;
            }

            sink.Line("stack (bottom first):");
            foreach (Screen screen in stack)
            {
                sink.Line("  layer " + screen.Layer + ": " + screen.Key);
            }
        }

        private static string Quoted(GraphNode node)
        {
            return "\"" + (Label(node) ?? Describe(node.Id.StructuralKey)) + "\"";
        }

        private static string Label(GraphNode node)
        {
            try
            {
                string label = GraphAnnouncer.FirstPartText(node);
                return string.IsNullOrEmpty(label) ? null : label;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Name(Screen screen)
        {
            try
            {
                return screen.ScreenName;
            }
            catch (Exception e)
            {
                return "<err: " + e.Message + ">";
            }
        }

        private static string Describe(object key)
        {
            return key == null ? "none" : key.ToString();
        }

        private static string Word(GraphDir dir)
        {
            switch (dir)
            {
                case GraphDir.Up:
                    return "up";
                case GraphDir.Down:
                    return "down";
                case GraphDir.Left:
                    return "left";
                default:
                    return "right";
            }
        }

        // Counts lines rather than characters, because the cap exists to keep the answer readable and
        // a reader counts in lines. Returns false once full, so callers stop walking.
        private sealed class Sink
        {
            private readonly StringBuilder _text = new StringBuilder();
            private int _lines;
            private bool _full;

            public bool Line(string line)
            {
                if (_lines >= MaxLines)
                {
                    _full = true;
                    return false;
                }

                _text.Append(line).Append('\n');
                _lines++;
                return true;
            }

            public override string ToString()
            {
                return _full
                    ? _text + "... (truncated at " + MaxLines + " lines)\n"
                    : _text.ToString();
            }
        }
    }
}

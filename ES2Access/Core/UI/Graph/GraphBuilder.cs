using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI.Graph
{
    /// <summary>
    /// Builds a <see cref="GraphRender"/>. Two construction styles, freely mixable in one build:
    ///
    /// <para><b>Menu mode</b> — rows of controls, wired automatically: left/right within a row, up/down
    /// between consecutive rows (two rows sharing a non-null row key get column navigation — up/down
    /// preserves the position instead of snapping to the first item; ported from Tanglebeep's
    /// MenuBuilder, itself from Factorio Access's menu.lua). Items added outside an explicit row become
    /// single-item rows (a plain vertical menu).</para>
    ///
    /// <para><b>Raw mode</b> — <see cref="AddNode"/> + <see cref="Connect"/> for arbitrary topologies.</para>
    ///
    /// Orthogonal to both: <see cref="BeginStop"/> groups nodes into Tab-stops (arrows never cross a stop;
    /// Tab cycles them), <see cref="SetRegion"/> tags nodes with a region for Ctrl+arrow jumps, and the
    /// PARENT STACK builds the presentation hierarchy: <see cref="PushContext"/> pushes a non-focusable
    /// structural level ("Difficulty settings, list" — announced when focus enters from outside), while
    /// <see cref="BeginGroup"/> pushes a focusable, EXPANDABLE group header (a tree section) whose children
    /// only emit while it's expanded — expansion state lives in the persistent set the builder is
    /// constructed with (<see cref="GraphState.Expanded"/>), so screens hold no tree state of their own.
    /// Nesting recurses; a collapsed ancestor suppresses everything beneath it.
    /// </summary>
    public sealed class GraphBuilder
    {
        private readonly HashSet<ControlId> _expansion; // persistent expanded-group set (null = all explicit)

        public GraphBuilder(HashSet<ControlId> expansion = null)
        {
            _expansion = expansion;
        }

        private sealed class Row
        {
            public readonly List<GraphNode> Items = new List<GraphNode>();
            public object Key;
            public object StopKey;
            public bool Positions = true;
        }

        private sealed class RawEdge
        {
            public ControlId From;
            public GraphDir Dir;
            public ControlId To;
            public string Label;
        }

        // Menu mode.
        private readonly List<Row> _rows = new List<Row>();
        private Row _currentRow;

        // Raw mode.
        private readonly List<GraphNode> _rawNodes = new List<GraphNode>();
        private readonly List<RawEdge> _rawEdges = new List<RawEdge>();

        // Every node in DECLARATION order regardless of mode — the render's node order (and so the
        // Tab-stop cycle) must interleave menu rows and raw nodes as the screen declared them, not
        // rows-then-raw (which would shove a sheet's stops behind later buttons).
        private readonly List<GraphNode> _declared = new List<GraphNode>();

        // The menu row each menu-mode node belongs to (null for raw nodes) — for stitching the
        // vertical gap where a stop mixes menu rows with raw content (a sheet below filter controls).
        private readonly Dictionary<GraphNode, Row> _rowOf = new Dictionary<GraphNode, Row>();

        // Shared.
        private readonly HashSet<ControlId> _ids = new HashSet<ControlId>();
        private ControlId _start;

        // Stop / region / parent state applied to nodes as they are added.
        private object _stopKey = AutoStopKey(0);
        private int _stopAuto = 1;
        private object _regionKey;

        // The keys a screen named its stops with (auto keys are not recorded) — see DeclaredStop.
        private readonly HashSet<object> _stopKeys = new HashSet<object>();

        // Per-stop Tab landings — see LandStopOn.
        private readonly Dictionary<object, ControlId> _stopLandings = new Dictionary<object, ControlId>();

        // The parent stack: structural levels (PushContext) and group headers (BeginGroup). A frame whose
        // group is collapsed suppresses every declaration beneath it (the stack stays balanced regardless).
        private sealed class ParentFrame
        {
            public GraphNode Node;      // the parent node (non-focusable context, or the group header)
            public bool Suppressed;     // this frame's subtree is swallowed (collapsed, or under a collapsed ancestor)
        }

        private readonly List<ParentFrame> _parents = new List<ParentFrame>();

        private GraphNode CurrentParent
        {
            get { return _parents.Count > 0 ? _parents[_parents.Count - 1].Node : null; }
        }

        private bool Suppressed
        {
            get { return _parents.Count > 0 && _parents[_parents.Count - 1].Suppressed; }
        }

        private static object AutoStopKey(int index)
        {
            return "stop#" + index;
        }

        // ---- stops / regions ----

        /// <summary>Start a new Tab-stop; nodes added from here belong to it. <paramref name="key"/> must be
        /// stable across rebuilds (it keys the stop's remembered position); null auto-assigns by index,
        /// which is stable when the screen builds its stops in a fixed order.</summary>
        public GraphBuilder BeginStop(object key = null)
        {
            if (_currentRow != null) throw new InvalidOperationException("Cannot begin a stop inside an open row");
            _stopKey = key ?? AutoStopKey(_stopAuto);
            _stopAuto++;
            if (key != null) _stopKeys.Add(key);
            _regionKey = null; // regions are per-stop
            return this;
        }

        /// <summary>
        /// Where Tab lands in the CURRENT stop when it has no remembered position - for a stop whose
        /// first declared node is not what the player came for: a table's rows sit under its sort-header
        /// band, and landing on "Name, button, selected" reads like a row called Name.
        ///
        /// It moves the "land on whichever alternative is in force" rule as well as the fallback: the
        /// selected node is looked for from here ONWARD, so the table still opens on the selected row
        /// and never on the sorted column's heading. Everything declared above it stays reachable with
        /// the arrow keys, which is how the player reaches a heading in the first place.
        /// </summary>
        public GraphBuilder LandStopOn(ControlId id)
        {
            if (id != null) _stopLandings[_stopKey] = id;
            return this;
        }

        /// <summary>Whether this build has already begun a stop under that key — asked by a contribution
        /// SHARED by every screen (the collapsed-tutorial bar), so that a screen which reads it among its
        /// own stops keeps the place it put it and only the screens that did not get it appended.</summary>
        public bool DeclaredStop(object key)
        {
            return key != null && _stopKeys.Contains(key);
        }

        /// <summary>Tag nodes added from here with a region (Ctrl+arrow jump target) within the current
        /// stop; null clears. Region keys must be stable across rebuilds.</summary>
        public GraphBuilder SetRegion(object key)
        {
            _regionKey = key;
            return this;
        }

        // ---- the parent stack: contexts + groups ----

        /// <summary>Push one NON-FOCUSABLE level of presentation hierarchy ("Difficulty settings",
        /// "list") onto nodes added from here — pure structure: never navigable, announced when focus
        /// enters from outside. Close with <see cref="PopContext"/>.</summary>
        public GraphBuilder PushContext(string label, string role = null, bool positions = true)
        {
            GraphNode parent = CurrentParent;
            List<NodeAnnouncement> anns = new List<NodeAnnouncement> { NodeAnnouncement.Static(label) };
            if (!string.IsNullOrEmpty(role)) anns.Add(NodeAnnouncement.Static(role));
            GraphNode node = new GraphNode
            {
                // Stable synthetic identity (label-pathed) so cross-render chain diffs match up.
                Id = ControlId.Structural("ctx:" + (parent != null ? parent.Id.StructuralKey : "") + "/" + label),
                Vtable = new NodeVtable { Announcements = anns },
                Parent = parent,
                Focusable = false,
                SuppressChildPositions = !positions,
            };
            _parents.Add(new ParentFrame { Node = node, Suppressed = Suppressed });
            return this;
        }

        public GraphBuilder PopContext()
        {
            if (_parents.Count == 0) throw new InvalidOperationException("No context/group to pop");
            _parents.RemoveAt(_parents.Count - 1);
            return this;
        }

        /// <summary>
        /// Push a FOCUSABLE, expandable group header (a tree section): the header emits as a navigable
        /// node here, and the children declared before <see cref="EndGroup"/> emit only while the group is
        /// expanded (a collapsed ancestor suppresses the whole subtree — recursion just works). Expansion
        /// state: <paramref name="expanded"/> when given (an adapter passes a retained container's state),
        /// else the persistent expansion set the builder was constructed with, else
        /// <paramref name="defaultExpanded"/>. The engine's tree operations (Right/Left) expand/collapse
        /// via the vtable's OnExpand/OnCollapse overrides when set, else by mutating the persistent set.
        /// </summary>
        public GraphBuilder BeginGroup(ControlId id, NodeVtable vtable, bool? expanded = null,
            bool defaultExpanded = false)
        {
            if (id == null) throw new ArgumentNullException("id");
            if (_currentRow != null) throw new InvalidOperationException("Cannot begin a group inside an open row");
            bool isExpanded = expanded ?? (_expansion != null ? _expansion.Contains(id) : defaultExpanded);

            GraphNode header = null;
            if (!Suppressed)
            {
                header = MakeNode(id, vtable);
                header.Expandable = true;
                header.Expanded = isExpanded;
                Row row = new Row { StopKey = _stopKey };
                row.Items.Add(header);
                _rows.Add(row);
                _rowOf[header] = row;
            }
            _parents.Add(new ParentFrame
            {
                // Suppressed subtree: keep chaining from the outer parent so the stack stays coherent.
                Node = header ?? CurrentParent,
                Suppressed = Suppressed || !isExpanded,
            });
            return this;
        }

        public GraphBuilder EndGroup()
        {
            return PopContext();
        }

        /// <summary>Whether a group id is expanded in the persistent set — for screens that must avoid
        /// even BUILDING a collapsed group's children (a lazy hierarchy whose child view models
        /// materialize on first access). Groups with an explicit expanded: argument manage their own
        /// state instead.</summary>
        public bool IsExpanded(ControlId id)
        {
            return _expansion != null && id != null && _expansion.Contains(id);
        }

        /// <summary>
        /// The persistent expansion set this builder was constructed with (null when the screen
        /// declares every group's state explicitly).
        ///
        /// For a group whose <see cref="NodeVtable.OnExpand"/>/<see cref="NodeVtable.OnCollapse"/> must
        /// ADD an effect to expanding rather than REPLACE the engine's bookkeeping — a camera that
        /// follows the player into the thing they just opened. Those hooks are overrides: setting one
        /// stops the engine flipping the state itself, so a hook that only wants a side effect flips it
        /// here and the tree keeps working.
        /// </summary>
        public HashSet<ControlId> Expansion
        {
            get { return _expansion; }
        }

        /// <summary>Focus starts here when the graph has no prior position (defaults to the first node).</summary>
        public GraphBuilder SetStart(ControlId id)
        {
            _start = id;
            return this;
        }

        // ---- menu mode ----

        /// <summary>Open a horizontal row. Rows sharing a non-null <paramref name="rowKey"/> with the row
        /// above/below get column-preserving vertical navigation.
        ///
        /// <paramref name="positions"/> false for a row whose members are COLUMNS rather than a bar of
        /// choices - a table's sort-header band, a grid line: "1 of 8" there counts the table's columns,
        /// which is not a place in a list and is not what the player is walking. Such a row says where it
        /// sits as a ROW instead (<see cref="TableRow"/>).</summary>
        public GraphBuilder StartRow(object rowKey = null, bool positions = true)
        {
            if (_currentRow != null) throw new InvalidOperationException("Cannot start a row while another is open");
            _currentRow = new Row { Key = rowKey, StopKey = _stopKey, Positions = positions };
            return this;
        }

        public GraphBuilder EndRow()
        {
            if (_currentRow == null) throw new InvalidOperationException("No row to end");
            if (_currentRow.Items.Count == 0 && !Suppressed)
                throw new InvalidOperationException("Row cannot be empty");
            if (_currentRow.Items.Count > 0) _rows.Add(_currentRow);
            _currentRow = null;
            return this;
        }

        /// <summary>Add a control — into the open row, or as its own single-item row. A no-op inside a
        /// collapsed group's subtree.</summary>
        public GraphBuilder AddItem(ControlId id, NodeVtable vtable)
        {
            if (Suppressed) return this;
            GraphNode node = MakeNode(id, vtable);
            if (_currentRow != null)
            {
                _currentRow.Items.Add(node);
                _rowOf[node] = _currentRow;
            }
            else
            {
                Row row = new Row { StopKey = _stopKey };
                row.Items.Add(node);
                _rows.Add(row);
                _rowOf[node] = row;
            }
            return this;
        }

        /// <summary>Add a read-only line (label only; no actions).</summary>
        public GraphBuilder AddLabel(ControlId id, Func<string> label)
        {
            return AddItem(id, new NodeVtable { Announcements = new[] { new NodeAnnouncement(label) } });
        }

        // ---- raw mode ----

        /// <summary>Add a node with no automatic wiring (raw mode; wire with <see cref="Connect"/>).
        /// A no-op inside a collapsed group's subtree.</summary>
        public GraphBuilder AddNode(ControlId id, NodeVtable vtable)
        {
            if (Suppressed) return this;
            _rawNodes.Add(MakeNode(id, vtable));
            return this;
        }

        /// <summary>Directed edge <paramref name="from"/> → <paramref name="to"/>, with an optional spoken
        /// transition line ("lane change"). Edges to/from undeclared nodes are dropped at build.</summary>
        public GraphBuilder Connect(ControlId from, GraphDir dir, ControlId to, string label = null)
        {
            if (from == null || to == null)
                throw new ArgumentNullException(from == null ? "from" : "to");
            _rawEdges.Add(new RawEdge { From = from, Dir = dir, To = to, Label = label });
            return this;
        }

        private GraphNode MakeNode(ControlId id, NodeVtable vtable)
        {
            if (id == null) throw new ArgumentNullException("id");
            if (vtable == null || vtable.Announcements == null || vtable.Announcements.Count == 0)
                throw new ArgumentException("A control must have at least one announcement", "vtable");
            if (!_ids.Add(id)) throw new InvalidOperationException("Duplicate control id: " + id);
            GraphNode node = new GraphNode
            {
                Id = id,
                Vtable = vtable,
                Parent = CurrentParent,
                StopKey = _stopKey,
                RegionKey = _regionKey,
            };
            _declared.Add(node);
            return node;
        }

        // ---- build ----

        /// <summary>Finalize into a render, or null when nothing was declared (treat as "closed").
        /// Menu rows and raw nodes/edges may coexist in one build (a screen mixing lists with a grid
        /// whose topology is computed): rows wire themselves; raw edges may reference any node.</summary>
        public GraphRender Build()
        {
            if (_currentRow != null) throw new InvalidOperationException("Unclosed row - call EndRow()");
            if (_rawNodes.Count == 0 && _rows.Count == 0) return null;

            GraphRender render = new GraphRender();
            foreach (GraphNode node in _declared) AddNodeTo(render, node);

            WireMenuEdges(render);
            foreach (RawEdge e in _rawEdges)
                if (render.Nodes.ContainsKey(e.From) && render.Nodes.ContainsKey(e.To))
                    render.Nodes[e.From].Transitions[e.Dir] = new Transition(e.To, e.Label);
            StitchModeBoundaries();

            render.StartKey = _start != null && render.Nodes.ContainsKey(_start)
                ? _start
                : render.Order[0].Id;
            foreach (KeyValuePair<object, ControlId> landing in _stopLandings)
                if (render.Nodes.ContainsKey(landing.Value)) render.StopLandings[landing.Key] = landing.Value;
            StampPositions();
            return render;
        }

        // Where a stop mixes MENU rows with RAW content (search/sort/filter controls above a sheet),
        // the two wiring systems don't see each other: menu auto-wiring connects only menu rows, and
        // the raw content's explicit edges stop at its own borders — leaving a vertical gap arrows
        // can't cross. Stitch it, at each mode boundary in declaration order within a stop.
        //
        // The raw side of a seam is a RUN, not a node: the consecutive raw nodes with no edge of their
        // own in the crossing direction, ending at the first that has one. For a sheet that is exactly
        // the row nearest the seam — its cells are declared together and only its interior rows wire
        // themselves vertically — and EVERY node of that run gets the edge back to the menu row,
        // because the player crosses the seam from whichever column they happen to be standing in.
        // Wiring only the run's first node (which is what this did originally) left every other column
        // answering the crossing key with silence.
        //
        // The menu side keeps a single target, and it is the run's FIRST node in both directions: for a
        // table that is the row's primary cell, whose readout is the whole row, rather than whichever
        // column happened to be declared last. Only MISSING edges are filled, so raw content that wires
        // its own seam — a paragraph a sheet was told to continue below — is never overridden, and the
        // run stops at it by construction.
        //
        // WITH ONE EXCEPTION, and it is the common one: where the menu row is the table's own heading
        // BAND, the seam is between two sets of COLUMNS, and a player standing in the third column
        // expects the third column's heading — not the first. So when both sides declare distinct
        // columns (<see cref="NodeVtable.Column"/>, stamped by the sheet and by the band), the seam is
        // paired column by column, and only a column the other side does not have falls back to the
        // single target. A bar of ordinary controls stamps no columns and so keeps the old rule exactly.
        private void StitchModeBoundaries()
        {
            Dictionary<object, List<GraphNode>> byStop = new Dictionary<object, List<GraphNode>>();
            List<object> stops = new List<object>();
            foreach (GraphNode n in _declared)
            {
                List<GraphNode> list;
                if (!byStop.TryGetValue(n.StopKey, out list))
                {
                    list = new List<GraphNode>();
                    byStop.Add(n.StopKey, list);
                    stops.Add(n.StopKey);
                }
                list.Add(n);
            }

            foreach (object stop in stops)
            {
                List<GraphNode> nodes = byStop[stop];
                for (int i = 1; i < nodes.Count; i++)
                {
                    GraphNode prev = nodes[i - 1];
                    GraphNode cur = nodes[i];
                    bool prevMenu = _rowOf.ContainsKey(prev);
                    bool curMenu = _rowOf.ContainsKey(cur);
                    if (prevMenu == curMenu) continue; // same mode — its own wiring covers it

                    if (prevMenu) // menu row above raw content: the run of raw nodes with no Up
                    {
                        if (cur.Transitions.ContainsKey(GraphDir.Up)) continue;
                        Row row = _rowOf[prev];
                        List<GraphNode> run = new List<GraphNode>();
                        for (int j = i; j < nodes.Count && !_rowOf.ContainsKey(nodes[j]); j++)
                        {
                            if (nodes[j].Transitions.ContainsKey(GraphDir.Up)) break;
                            run.Add(nodes[j]);
                        }

                        Dictionary<int, ControlId> band = ByColumn(row.Items);
                        Dictionary<int, ControlId> cells = ByColumn(run);
                        foreach (GraphNode node in run)
                            node.Transitions[GraphDir.Up] =
                                new Transition(Across(band, node, row.Items[0].Id));

                        foreach (GraphNode cell in row.Items)
                            if (!cell.Transitions.ContainsKey(GraphDir.Down))
                                cell.Transitions[GraphDir.Down] =
                                    new Transition(Across(cells, cell, cur.Id));
                    }
                    else // raw content above a menu row: the trailing run of raw nodes with no Down
                    {
                        Row row = _rowOf[cur];
                        int start = -1;
                        for (int j = i - 1; j >= 0 && !_rowOf.ContainsKey(nodes[j]); j--)
                        {
                            if (nodes[j].Transitions.ContainsKey(GraphDir.Down)) break;
                            start = j;
                        }

                        if (start < 0) continue;
                        List<GraphNode> run = nodes.GetRange(start, i - start);
                        Dictionary<int, ControlId> band = ByColumn(row.Items);
                        Dictionary<int, ControlId> cells = ByColumn(run);
                        foreach (GraphNode node in run)
                            node.Transitions[GraphDir.Down] =
                                new Transition(Across(band, node, row.Items[0].Id));
                        foreach (GraphNode cell in row.Items)
                            if (!cell.Transitions.ContainsKey(GraphDir.Up))
                                cell.Transitions[GraphDir.Up] =
                                    new Transition(Across(cells, cell, nodes[start].Id));
                    }
                }
            }
        }

        // One side of a seam indexed by the column each node sits in, or null when the nodes are not a
        // set of columns at all — a bar of ordinary controls, every one of them column 0, where pairing
        // by column would be pairing everything with the first thing. Both conditions are needed: the
        // columns must be distinct (a duplicate means the stamp is not a column number here) and at
        // least one must be non-zero (a lone control, or a run of plain nodes, is column 0 by default).
        private static Dictionary<int, ControlId> ByColumn(List<GraphNode> nodes)
        {
            Dictionary<int, ControlId> map = new Dictionary<int, ControlId>(nodes.Count);
            bool columned = false;
            foreach (GraphNode node in nodes)
            {
                int column = node.Vtable != null ? node.Vtable.Column : 0;
                if (map.ContainsKey(column)) return null;
                if (column != 0) columned = true;
                map.Add(column, node.Id);
            }

            return columned ? map : null;
        }

        // Where crossing the seam from this node lands: the same column on the other side where both
        // sides have it, else the single target the seam falls back to.
        private static ControlId Across(Dictionary<int, ControlId> other, GraphNode from, ControlId fallback)
        {
            ControlId landing;
            int column = from.Vtable != null ? from.Vtable.Column : 0;
            return other != null && other.TryGetValue(column, out landing) ? landing : fallback;
        }

        // The (parent, stop) pair a single-item row's node is positioned within. A dedicated struct
        // rather than a boxed KeyValuePair: reflection-based ValueType equality would be both slow and
        // implementation-defined on the game's Mono runtime.
        private struct SiblingKey : IEquatable<SiblingKey>
        {
            public readonly GraphNode Parent;
            public readonly object StopKey;

            public SiblingKey(GraphNode parent, object stopKey)
            {
                Parent = parent;
                StopKey = stopKey;
            }

            public bool Equals(SiblingKey other)
            {
                return ReferenceEquals(Parent, other.Parent) && Equals(StopKey, other.StopKey);
            }

            public override bool Equals(object obj)
            {
                return obj is SiblingKey && Equals((SiblingKey)obj);
            }

            public override int GetHashCode()
            {
                int h = Parent != null ? Parent.GetHashCode() : 0;
                return (h * 397) ^ (StopKey != null ? StopKey.GetHashCode() : 0);
            }
        }

        // Auto-stamp "n of m" positions: a multi-item row's members are positioned within their ROW (a
        // bar); single-item-row nodes among the siblings sharing their (parent, stop) — the vertical
        // list/tree level arrows actually traverse. Raw/grid nodes get none.
        //
        // A lone sibling normally reads no position: on a flat screen "1 of 1" is noise. The exception
        // is a level the player DESCENDS INTO — an expandable group's children. Having just pressed
        // Right into a submenu, "1 of 1" is what says the submenu holds exactly this one entry; without
        // it a one-entry flyout sounds indistinguishable from a step sideways.
        private void StampPositions()
        {
            Dictionary<SiblingKey, List<GraphNode>> groups = new Dictionary<SiblingKey, List<GraphNode>>();
            List<SiblingKey> keys = new List<SiblingKey>();
            foreach (Row row in _rows)
            {
                if (!row.Positions) continue;
                if (row.Items.Count > 1)
                {
                    Stamp(row.Items);
                    continue;
                }
                GraphNode node = row.Items[0];
                if (node.Parent != null && node.Parent.SuppressChildPositions) continue;
                SiblingKey key = new SiblingKey(node.Parent, node.StopKey);
                List<GraphNode> list;
                if (!groups.TryGetValue(key, out list))
                {
                    list = new List<GraphNode>();
                    groups.Add(key, list);
                    keys.Add(key);
                }
                list.Add(node);
            }
            foreach (SiblingKey key in keys)
                Stamp(groups[key], key.Parent != null && key.Parent.Expandable);
        }

        private static void Stamp(List<GraphNode> siblings, bool evenIfAlone = false)
        {
            if (siblings.Count < 2 && !evenIfAlone) return;
            for (int i = 0; i < siblings.Count; i++)
            {
                siblings[i].PositionIndex = i + 1;
                siblings[i].PositionCount = siblings.Count;
            }
        }

        private static void AddNodeTo(GraphRender render, GraphNode node)
        {
            render.Nodes.Add(node.Id, node);
            render.Order.Add(node);
        }

        // Left/right within a row; up/down between consecutive rows OF THE SAME STOP (arrows never cross a
        // Tab-stop). Shared non-null row keys preserve the column; otherwise vertical lands on first item.
        private void WireMenuEdges(GraphRender render)
        {
            // Segment rows in DECLARATION order: within a stop, consecutive menu rows chain vertically
            // only when no raw node was declared between them. Interleaved raw content (a sheet between
            // menu controls) BREAKS the chain — StitchModeBoundaries wires the seams. Without the break,
            // menu edges would skip straight over the raw block; the stitcher (which only fills missing
            // edges) would find the gap already bridged, leaving the block an unreachable island.
            List<List<Row>> byStop = new List<List<Row>>();
            Dictionary<object, List<Row>> openSegment = new Dictionary<object, List<Row>>(); // stop → its currently-open segment
            foreach (GraphNode node in _declared)
            {
                Row row;
                if (_rowOf.TryGetValue(node, out row))
                {
                    List<Row> seg;
                    if (!openSegment.TryGetValue(node.StopKey, out seg))
                    {
                        seg = new List<Row>();
                        openSegment.Add(node.StopKey, seg);
                        byStop.Add(seg);
                    }
                    if (seg.Count == 0 || seg[seg.Count - 1] != row) seg.Add(row);
                }
                else
                {
                    openSegment.Remove(node.StopKey); // raw node: close this stop's segment
                }
            }

            foreach (List<Row> rows in byStop)
            {
                for (int r = 0; r < rows.Count; r++)
                {
                    Row row = rows[r];
                    for (int pos = 0; pos < row.Items.Count; pos++)
                    {
                        GraphNode node = row.Items[pos];
                        if (r > 0)
                            node.Transitions[GraphDir.Up] = new Transition(VerticalTarget(row, rows[r - 1], pos));
                        if (r < rows.Count - 1)
                            node.Transitions[GraphDir.Down] = new Transition(VerticalTarget(row, rows[r + 1], pos));
                        if (pos > 0)
                            node.Transitions[GraphDir.Left] = new Transition(row.Items[pos - 1].Id);
                        if (pos < row.Items.Count - 1)
                            node.Transitions[GraphDir.Right] = new Transition(row.Items[pos + 1].Id);
                    }
                }
            }
        }

        // Where vertical navigation from position pos lands in the adjacent row: the same position when
        // the rows share a non-null key (column nav) and it exists there, else the first item.
        private static ControlId VerticalTarget(Row from, Row to, int pos)
        {
            if (from.Key != null && to.Key != null && Equals(from.Key, to.Key) && pos < to.Items.Count)
                return to.Items[pos].Id;
            return to.Items[0].Id;
        }
    }
}

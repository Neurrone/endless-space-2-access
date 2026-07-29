using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;

namespace ES2Access.Core.UI.Graph
{
    /// <summary>
    /// Composes the spoken line for a focus change by diffing the old and new focus PATHS — each node's
    /// ancestor chain (<see cref="GraphNode.Parent"/>) plus the node itself, compared by identity. Newly-
    /// entered levels read outermost-first, then the landing control: "Difficulty settings, list, Normal,
    /// radio button, selected", recursing as deep as the hierarchy goes. Sibling moves share the whole
    /// prefix and read just the control; ascends likewise; and descending from a group onto its own child
    /// re-announces nothing but the child — the group is on the child's chain AND is the from-node, so the
    /// prefix swallows it. A retained-path diff, reconstructed per render from parent pointers.
    ///
    /// Parts are joined with <see cref="ModStrings.ListSeparator"/> via <see cref="MessageBuilder"/>, so
    /// the punctuation is translatable rather than baked in here.
    ///
    /// The three injection points (<see cref="PartFilter"/>, <see cref="PositionText"/>,
    /// <see cref="ExpandedStateText"/>) are static because every node's readout flows through them and
    /// threading them per-call would touch every node factory. They are process state, so
    /// <see cref="Reset"/> exists for mod teardown and for test isolation.
    /// </summary>
    public static class GraphAnnouncer
    {
        /// <summary>The line for landing on <paramref name="to"/> having come from <paramref name="from"/>
        /// (null = from nothing: the full path reads). <paramref name="transitionLabel"/> is the crossed
        /// edge's spoken line, when it had one. Null when there is nothing to say.</summary>
        public static string Compose(GraphNode from, GraphNode to, string transitionLabel = null)
        {
            if (to == null) return null;

            List<GraphNode> toPath = PathOf(to);
            List<GraphNode> fromPath = from != null ? PathOf(from) : EmptyPath;

            // Common prefix by identity — levels we were already inside (or ON: descending from a group
            // onto its child keeps the group in the prefix) stay silent.
            int i = 0;
            while (i < fromPath.Count && i < toPath.Count && fromPath[i].Id.Equals(toPath[i].Id)) i++;

            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(transitionLabel)) parts.Add(transitionLabel);

            if (i >= toPath.Count)
            {
                // Ascended (or same node): announce just the now-innermost focus.
                string text = LeafText(to);
                if (!string.IsNullOrEmpty(text)) parts.Add(text);
            }
            else
            {
                for (int j = i; j < toPath.Count; j++)
                {
                    string text = LeafText(toPath[j]);
                    if (string.IsNullOrEmpty(text)) continue;
                    // Dedupe: a level whose label just duplicates the next level down (or the control
                    // itself — "a 'Game difficulty' section wrapping the 'Game difficulty' control").
                    if (j + 1 < toPath.Count)
                    {
                        string label = FirstPartText(toPath[j]);
                        string next = FirstPartText(toPath[j + 1]);
                        if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(next)
                            && DuplicatesNext(label, next)) continue;
                    }
                    parts.Add(text);
                }
            }

            return Join(parts);
        }

        /// <summary>The full readout for a landing with no prior focus (screen entry, focus restore).</summary>
        public static string ComposeFull(GraphNode to)
        {
            return Compose(null, to);
        }

        /// <summary>Drop every injected delegate — mod teardown, and test isolation.</summary>
        public static void Reset()
        {
            PartFilter = null;
            PositionText = null;
            ExpandedStateText = null;
        }

        private static readonly List<GraphNode> EmptyPath = new List<GraphNode>();

        // The node's path: ancestors outermost-first, then the node itself.
        private static List<GraphNode> PathOf(GraphNode node)
        {
            List<GraphNode> path = new List<GraphNode>();
            for (GraphNode n = node; n != null; n = n.Parent) path.Add(n);
            path.Reverse();
            return path;
        }

        private static string Join(List<string> parts)
        {
            MessageBuilder mb = new MessageBuilder();
            for (int i = 0; i < parts.Count; i++) mb.ListItem(parts[i]);
            return mb.Build();
        }

        /// <summary>Pluggable per-part filter — installed by the host to consult the user's announcement
        /// settings (per control type + per kind); null (tests, boot) = everything speaks. Returning false
        /// drops the part from readouts AND from the live watch.</summary>
        public static Func<ControlType, NodeAnnouncement, bool> PartFilter;

        /// <summary>
        /// A node's EFFECTIVE announcement parts: the control type's common parts (the role word) merged
        /// with the node's own — a node part overrides a common part of the same kind — sorted by the
        /// type's kind order (unknown/kindless parts append in declaration order), then filtered by the
        /// user's settings. This is the single list readouts and the live watch operate on.
        /// </summary>
        public static List<NodeAnnouncement> EffectiveAnnouncements(GraphNode node)
        {
            List<NodeAnnouncement> result = new List<NodeAnnouncement>();
            NodeVtable vt = node != null ? node.Vtable : null;
            if (vt == null) return result;
            ControlType type = vt.ControlType;

            IList<NodeAnnouncement> common = type != null && type.Common != null ? type.Common() : null;
            if (common != null)
                foreach (NodeAnnouncement c in common)
                    if (c != null && !HasKind(vt.Announcements, c.Kind)) result.Add(c);
            if (vt.Announcements != null)
                foreach (NodeAnnouncement a in vt.Announcements)
                    if (a != null) result.Add(a);

            if (type != null && type.Order != null && type.Order.Length > 0 && result.Count > 1)
            {
                // Stable: composite key = (kind's order index, declaration index) — List.Sort alone is
                // unstable and would scramble same-bucket (kindless) parts.
                List<KeyValuePair<long, NodeAnnouncement>> keyed =
                    new List<KeyValuePair<long, NodeAnnouncement>>(result.Count);
                for (int i = 0; i < result.Count; i++)
                    keyed.Add(new KeyValuePair<long, NodeAnnouncement>(
                        (long)OrderIndex(type.Order, result[i].Kind) << 32 | (uint)i, result[i]));
                keyed.Sort((x, y) => x.Key.CompareTo(y.Key));
                result.Clear();
                foreach (KeyValuePair<long, NodeAnnouncement> kv in keyed) result.Add(kv.Value);
            }

            if (PartFilter != null)
            {
                ControlType filterType = type;
                result.RemoveAll(a => !PartFilter(filterType, a));
            }
            return result;
        }

        private static bool HasKind(IList<NodeAnnouncement> anns, string kind)
        {
            if (anns == null || kind == null) return false;
            foreach (NodeAnnouncement a in anns)
                if (a != null && a.Kind == kind) return true;
            return false;
        }

        // Sort key: declared kinds by their order index; everything else after (one shared bucket, with
        // the declaration-index tie-break above keeping their relative order).
        private static int OrderIndex(string[] order, string kind)
        {
            if (kind != null)
                for (int i = 0; i < order.Length; i++)
                    if (order[i] == kind) return i;
            return order.Length;
        }

        /// <summary>A node's own readout: its effective announcement parts, resolved live, non-empty ones
        /// joined — plus, for an expandable group, its expanded/collapsed state word. The first part is
        /// the control's label, so path dedupe's prefix check applies.</summary>
        public static string LeafText(GraphNode node)
        {
            List<NodeAnnouncement> anns = EffectiveAnnouncements(node);
            List<string> parts = new List<string>(anns.Count + 2);
            // Where the tooltip starts, if the node speaks one: everything a control has to SAY comes
            // after everything it IS, so the expanded/collapsed word goes in ahead of it rather than
            // at the end ("New Game, button, collapsed, Start a new game...").
            int tooltipAt = -1;
            for (int i = 0; i < anns.Count; i++)
            {
                string t = null;
                if (anns[i] != null && anns[i].Text != null) t = anns[i].Text();
                if (string.IsNullOrEmpty(t)) continue;
                if (tooltipAt < 0 && anns[i].Kind == AnnouncementKinds.Tooltip) tooltipAt = parts.Count;
                parts.Add(t);
            }

            if (node != null && node.Expandable && !node.Vtable.SpeaksOwnExpansion && ExpandedStateText != null)
            {
                string state = ExpandedStateText(node.Expanded);
                if (!string.IsNullOrEmpty(state))
                {
                    if (tooltipAt >= 0) parts.Insert(tooltipAt, state);
                    else parts.Add(state);
                }
            }

            // The auto-stamped sibling position, unless the node carries its own (an explicit
            // position-kind part, or a composed message that already reads it). Honors the user's
            // per-kind setting.
            if (node != null && node.PositionCount > 0 && PositionText != null
                && !node.Vtable.SpeaksOwnPosition && !HasKind(node.Vtable.Announcements, AnnouncementKinds.Position)
                && (PartFilter == null || PartFilter(node.Vtable.ControlType, AutoPositionProbe)))
            {
                string pos = PositionText(node.PositionIndex, node.PositionCount);
                if (!string.IsNullOrEmpty(pos)) parts.Add(pos);
            }

            return Join(parts);
        }

        /// <summary>Pluggable "n of m" wording (localized by the host); null = no auto positions.</summary>
        public static Func<int, int, string> PositionText;

        // A stand-in part handed to the PartFilter so the user's position-kind toggle governs the
        // auto-stamped position too.
        private static readonly NodeAnnouncement AutoPositionProbe =
            new NodeAnnouncement(() => null, kind: AnnouncementKinds.Position);

        /// <summary>Pluggable expanded/collapsed wording for group headers (localized by the host);
        /// null = groups don't speak their state.</summary>
        public static Func<bool, string> ExpandedStateText;

        /// <summary>The first announcement part's text (the label) — for dedupe and search fallbacks.</summary>
        public static string FirstPartText(GraphNode node)
        {
            IList<NodeAnnouncement> anns = node != null && node.Vtable != null ? node.Vtable.Announcements : null;
            if (anns == null || anns.Count == 0) return null;
            NodeAnnouncement first = anns[0];
            return first != null && first.Text != null ? first.Text() : null;
        }

        // The next part "starts as" this label: equal, or its first list-separated segment is the label
        // (a control's readout leads with its label: "Game difficulty, menu button").
        private static bool DuplicatesNext(string label, string next)
        {
            if (!next.StartsWith(label)) return false;
            if (next.Length == label.Length) return true;
            string sep = ModStrings.Get(ModStrings.ListSeparator).TrimEnd();
            return sep.Length > 0 && next.Substring(label.Length).StartsWith(sep);
        }
    }
}

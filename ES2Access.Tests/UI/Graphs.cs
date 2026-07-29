using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;

namespace ES2Access.Tests.UI
{
    /// <summary>Shorthand for declaring test graphs — the graph engine takes plain data, so the fixtures
    /// stay readable without a game.</summary>
    internal static class Graphs
    {
        public static ControlId Id(string key)
        {
            return ControlId.Structural(key);
        }

        /// <summary>A label-only control.</summary>
        public static NodeVtable Vt(string label)
        {
            return new NodeVtable { Announcements = new[] { NodeAnnouncement.Static(label) } };
        }

        /// <summary>A control with a label plus extra parts.</summary>
        public static NodeVtable Vt(string label, params NodeAnnouncement[] extra)
        {
            List<NodeAnnouncement> anns = new List<NodeAnnouncement> { NodeAnnouncement.Static(label) };
            anns.AddRange(extra);
            return new NodeVtable { Announcements = anns };
        }

        public static NodeAnnouncement Part(string text, string kind)
        {
            return new NodeAnnouncement(() => text, false, kind);
        }

        /// <summary>A control type whose common part is a role word, in the mod's standard kind order.</summary>
        public static ControlType Type(string key, string roleWord)
        {
            return new ControlType
            {
                Key = key,
                Order = new[]
                {
                    AnnouncementKinds.Label,
                    AnnouncementKinds.Role,
                    AnnouncementKinds.Value,
                    AnnouncementKinds.Selected,
                    AnnouncementKinds.Enabled,
                    AnnouncementKinds.Tooltip,
                    AnnouncementKinds.Position,
                },
                Common = () => new NodeAnnouncement[] { Part(roleWord, AnnouncementKinds.Role) },
            };
        }

        public static GraphNode Node(GraphRender render, string key)
        {
            return render.NodeAt(Id(key));
        }

        public static ControlId Dest(GraphNode node, GraphDir dir)
        {
            Transition t;
            return node.Transitions.TryGetValue(dir, out t) && t != null ? t.Destination : null;
        }

        public static string DestKey(GraphNode node, GraphDir dir)
        {
            ControlId d = Dest(node, dir);
            return d == null ? null : (string)d.StructuralKey;
        }

        public static string Label(GraphNode node)
        {
            return node == null ? null : GraphAnnouncer.FirstPartText(node);
        }

        /// <summary>A render callback that rebuilds from <paramref name="declare"/> every time, the way a
        /// real screen does.</summary>
        public static Func<GraphRender> Renderer(Action<GraphBuilder> declare, GraphState state = null)
        {
            return () =>
            {
                GraphBuilder b = state != null ? new GraphBuilder(state.Expanded) : new GraphBuilder();
                declare(b);
                return b.Build();
            };
        }
    }
}

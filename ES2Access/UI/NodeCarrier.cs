using System;
using System.Collections.Generic;
using System.Text;
using ES2Access.Core.UI.Graph;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// The one answer to "which widget is this node standing on".
    ///
    /// A graph node carries two object-typed handles the engine can read: the game object its id was
    /// derived from (<see cref="ControlId.Reference"/>) and the tooltip its pointer is aimed at
    /// (<see cref="NodeVtable.PointsAt"/>). The carrier is the first as a widget where it is one, else
    /// the second's <c>AgeTransform</c> - a dossier node (<see cref="TooltipChildren"/>) is keyed
    /// structurally and located only by what it points at.
    ///
    /// <see cref="NodeVtable.ScrollAnchor"/> is NEVER read here. It says what a line is DRAWN AS so a
    /// panel has a rectangle to scroll to; it is not a claim that the node is that widget, and a
    /// string-keyed line borrowing its container's rect would answer for the container's paint state
    /// rather than its own.
    ///
    /// It lives in one place because two callers must never disagree about it: <see cref="NodeGate"/>
    /// drops nodes whose carrier is not painting, and <see cref="Dev.GhostAudit"/> reports the nodes
    /// that are not painting. A gate resolving a carrier the audit does not (or the reverse) would
    /// either drop what the audit calls clean or leave findings the gate claims to have removed.
    ///
    /// A node with NO carrier - a place on the map, a <see cref="Core.UI.GraphSheet"/> row keyed by a
    /// domain object - is not a failure: it is a node read off the model, there is nothing here to ask
    /// the question of, and the gate must let it through exactly as the audit counts it
    /// <c>synthetic</c>.
    /// </summary>
    public static class NodeCarrier
    {
        /// <summary>How many names deep a carrier's path is reported before it is cut off.</summary>
        private const int MaxPathSegments = 8;

        /// <summary>The widget a node's id was derived from - the control itself, the row's group, the
        /// label its words were read off - or null when the id names a model object instead.</summary>
        public static AgeTransform WidgetOf(object reference)
        {
            AgeTransform widget = reference as AgeTransform;
            if (widget != null)
            {
                return widget;
            }

            Component component = reference as Component;
            return component == null ? null : component.GetComponent<AgeTransform>();
        }

        /// <summary>Which tooltip a node's pointer goes to, as the node itself declares it. Never
        /// re-derived from the widget tree: the deepest tooltip inside a card is often decoration, and
        /// a second opinion that picked it reported a defect on screens whose pointing was right all
        /// along.</summary>
        public static AgeTooltip AimOf(NodeVtable vtable)
        {
            Func<object> at = vtable == null ? null : vtable.PointsAt;
            try
            {
                return at == null ? null : at() as AgeTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The widget a node stands on, or null when it stands on the model alone.</summary>
        public static AgeTransform Of(ControlId id, NodeVtable vtable)
        {
            AgeTransform widget = id == null ? null : WidgetOf(id.Reference);
            if (widget != null)
            {
                return widget;
            }

            AgeTooltip aimed = AimOf(vtable);
            return aimed == null ? null : aimed.AgeTransform;
        }

        /// <summary>Where a carrier sits, named from the root down - the half of a drop report or a
        /// ghost finding that says WHICH of eleven identically-keyed rows this is.</summary>
        public static string Path(AgeTransform widget)
        {
            List<string> names = new List<string>();
            try
            {
                AgeTransform at = widget;
                for (int i = 0; at != null && i < MaxPathSegments; i++)
                {
                    names.Add(at.name);
                    at = at.Parent;
                }
            }
            catch (Exception) { }

            StringBuilder path = new StringBuilder();
            for (int i = names.Count - 1; i >= 0; i--)
            {
                path.Append(names[i]);
                if (i > 0)
                {
                    path.Append('/');
                }
            }

            return path.ToString();
        }
    }
}

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
    /// A graph node carries three object-typed handles the engine can read: the widget it was
    /// DECLARED to stand on (<see cref="NodeVtable.Carrier"/>), the game object its id was derived
    /// from (<see cref="ControlId.Reference"/>) and the tooltip its pointer is aimed at
    /// (<see cref="NodeVtable.PointsAt"/>). The carrier is the first two, in that order, as a widget
    /// where it is one - what a node POINTS AT is a hover target, not a place the node stands
    /// (<see cref="Of"/>).
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

        /// <summary>
        /// The widget a node stands on, or null when it stands on the model alone.
        ///
        /// Two handles answer, in this order. FIRST the carrier the node was DECLARED with
        /// (<see cref="NodeVtable.Carrier"/>), which the shared emitters set from the widget they
        /// were reading: content the game pools is keyed structurally on purpose, so its id names no
        /// object, and without the slot every one of those nodes was ungated. SECOND the id's own
        /// reference, which is the answer wherever a node's identity IS the widget.
        ///
        /// Where a node's pointer aims is deliberately NOT a fallback: on every table prefab in this
        /// game the per-row tooltip is an invisible <c>TooltipArea</c> stretched over the row -
        /// switched off as a widget, alive as a hover target - so asking it "are you painting"
        /// answers no for a row the game is drawing. A <see cref="Core.UI.GraphSheet"/> keys its
        /// cells by the DOMAIN OBJECT, which is exactly the carrier-less case, and the aim
        /// fall-through turned that into a false drop of all 15 save names in the load/save modal, 6
        /// fleet-selection rows, 4 military rows and 2 empire rows.
        /// </summary>
        public static AgeTransform Of(ControlId id, NodeVtable vtable)
        {
            AgeTransform declared = vtable == null ? null : WidgetOf(vtable.Carrier);
            if (declared != null)
            {
                return declared;
            }

            return id == null ? null : WidgetOf(id.Reference);
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

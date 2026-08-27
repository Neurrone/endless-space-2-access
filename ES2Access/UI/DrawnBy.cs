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
    /// It is a question with two possible shapes and the node's own TYPE says which it is: a
    /// <see cref="DrawnNode"/> was declared with the widget that vouches for it, and that widget -
    /// the ONLY evidence there is - is the answer; a <see cref="SyntheticNode"/> has no evidence
    /// member at all and the answer is nothing, which is not a failure but the whole of what such a
    /// node claims.
    ///
    /// What a node is ABOUT is deliberately not consulted. <see cref="ControlId.Subject"/> answers
    /// identity, and a subject that happens to be a widget is still only an identity: reading it as
    /// evidence is how a node came to be gated by accident, and how the walks that declared no
    /// evidence at all came to look exactly like the ones that had none to declare.
    /// <see cref="NodeVtable.PointsAt"/> is not consulted either - what a node points at is a hover
    /// target, and on every table prefab in this game the per-row tooltip is an invisible
    /// <c>TooltipArea</c> stretched over the row, switched off as a widget and alive as a hover
    /// target, so asking it "are you painting" answers no for a row the game is drawing.
    /// <see cref="NodeVtable.ScrollAnchor"/> is never read here either: it says what a line is DRAWN
    /// AS so a panel has a rectangle to scroll to, not that the node IS that widget, and a
    /// string-keyed line borrowing its container's rect would answer for the container's paint state
    /// rather than its own.
    ///
    /// It lives in one place because two callers must never disagree about it: <see cref="NodeGate"/>
    /// drops nodes whose evidence is not painting, and <see cref="Dev.GhostAudit"/> reports the nodes
    /// that are not painting. A gate resolving evidence the audit does not (or the reverse) would
    /// either drop what the audit calls clean or leave findings the gate claims to have removed.
    /// </summary>
    public static class DrawnBy
    {
        /// <summary>How many names deep a widget's path is reported before it is cut off.</summary>
        private const int MaxPathSegments = 8;

        /// <summary>The widget an object-typed handle names - the control itself, the row's group, the
        /// label its words were read off - or null when the handle names a model object instead.</summary>
        public static AgeTransform WidgetOf(object handle)
        {
            AgeTransform widget = handle as AgeTransform;
            if (widget != null)
            {
                return widget;
            }

            Component component = handle as Component;
            return component == null ? null : component.GetComponent<AgeTransform>();
        }

        /// <summary>The widget a node stands on: the evidence a <see cref="DrawnNode"/> was declared
        /// with, and null for a <see cref="SyntheticNode"/>, which stands on nothing that can be
        /// asked.</summary>
        public static AgeTransform Of(NodeDeclaration node)
        {
            DrawnNode drawn = node as DrawnNode;
            return drawn == null ? null : WidgetOf(drawn.DrawnBy);
        }

        /// <summary>Where a widget sits, named from the root down - the half of a drop report or a
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

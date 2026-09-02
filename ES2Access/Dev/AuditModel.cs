using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Screens;
using ES2Access.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace ES2Access.Dev
{
    /// <summary>
    /// What the four self-checks all work in: one breach, one declared node, one way of building the
    /// declared side of a screen, and one way of writing any of it out.
    ///
    /// The checks differ in what they compare - the notification family compares a popup's paint
    /// against its render, the tooltip audit compares aims against tooltips, the coverage audit
    /// compares the engine's whole widget tree against the render, the ghost audit compares the
    /// render against the paint - but all four start from the same two nouns, and three of them used
    /// to reach into the notification audit to get them. This is where they live now, so a change to
    /// what a declared node knows about itself reaches every check at once rather than one of them.
    ///
    /// Nothing here decides whether anything is WRONG. That judgement is each check's own, and the
    /// reason this file holds none of it is that a shared judgement would quietly turn four different
    /// questions into one answer.
    /// </summary>
    internal static class AuditModel
    {
        /// <summary>One thing that does not line up, in the terms a fix needs: which node or widget,
        /// where it is drawn, and the string that broke the rule.</summary>
        internal sealed class Breach
        {
            public string Where;
            public string What;
            public string Detail;
            public Rect Rect;
            public bool HasRect;
        }

        internal sealed class Declared
        {
            public GraphNode Node;
            public AgeTransform Widget;
            public ControlId Id;
            public string Key;
            public string Region;
            public string Announcement;
            public List<string> Buffer = new List<string>();

            /// <summary>The node came from the stops every page is given rather than from the page
            /// itself - the structural answer to "whose node is this", asked of the stop it was
            /// declared into (<see cref="NotificationAudit.Contributed"/>).</summary>
            public bool Contributed;

            /// <summary>Arrival line, buffer and every CROSSING into this node together: everything
            /// arriving on it, reaching it from a neighbour, or reading it, would say. The third is
            /// not an extra - a table's column captions are spoken only as edges, so a spoken side
            /// without them cannot account for a caption the popup draws.</summary>
            public List<string> Spoken = new List<string>();
        }

        /// <summary>
        /// What the mod would say, built exactly the way <c>/gui/graph</c> builds it: the screen's own
        /// render, each node's full arrival line, each node's buffer, and each edge the player can
        /// cross INTO it. Nothing is composed here, so a difference between this and what a player
        /// hears is a difference in the navigator rather than in this file.
        ///
        /// <paramref name="ownOnly"/> narrows the answer to what this SCREEN declared, dropping the
        /// stops every page is given on top of its own (<see cref="Screens.Screen.BuildShared"/>).
        /// Every node carries the same answer as <see cref="Declared.Contributed"/> either way, for a
        /// caller that wants the whole render and still needs to know whose each node is.
        /// </summary>
        internal static List<Declared> DeclaredNodes(
            Screens.Screen screen,
            List<Breach> unlocatable,
            bool ownOnly = false
        )
        {
            List<Declared> declared = new List<Declared>();
            GraphNavigator navigator = ModEntry.Navigator;
            if (screen == null || navigator == null)
            {
                return declared;
            }

            GraphRender render = navigator.InspectRender(screen);
            if (render == null)
            {
                return declared;
            }

            Dictionary<ControlId, Declared> byId = new Dictionary<ControlId, Declared>();
            foreach (GraphNode node in render.Order)
            {
                Declared it = new Declared();
                it.Node = node;
                it.Id = node.Id;
                it.Key = Convert.ToString(node.Id.StructuralKey);

                // The screen's render is not only its own: the heads-up display contributes stops to
                // whatever screen has focus, and a minimised tutorial bar sitting up there was
                // reported as three things the popup says and nothing draws. Only what the screen
                // itself declared is measured against what the screen paints - asked of the STOP a
                // node was declared into, which is where the contribution happens, rather than of how
                // its key is spelled. A key prefix cannot answer it: the notification screen keys its
                // frame "notification:" and hands each popup variant its own prefix for the body, so
                // the spelling test threw away the whole popup and audited the six rails (measured
                // 2026-08-28 on the ground-battle report: nodes 6, every body string reported as
                // painted-but-unsaid).
                it.Contributed = Contributed(node);
                if (ownOnly && it.Contributed)
                {
                    continue;
                }

                it.Region = node.RegionKey == null ? null : node.RegionKey.ToString();
                // The widget a node was derived from. Most nodes this screen declares carry one - the
                // control, the row's group, the label the words were read off - so this is a lookup
                // rather than a search; a table cell is the exception and is resolved by
                // ResolveRowCells.
                it.Widget = DrawnBy.WidgetOf(node.Id.Subject);

                try
                {
                    it.Announcement = GraphAnnouncer.ComposeFull(node);
                }
                catch (Exception e)
                {
                    it.Announcement = null;
                    unlocatable.Add(Made(it.Widget, it.Key, "reading it threw", e.Message));
                }

                try
                {
                    it.Buffer = GraphNavigator.BufferLines(node);
                }
                catch (Exception)
                {
                    it.Buffer = new List<string>();
                }

                if (!string.IsNullOrEmpty(it.Announcement))
                {
                    it.Spoken.Add(it.Announcement);
                }

                for (int i = 0; i < it.Buffer.Count; i++)
                {
                    if (!string.IsNullOrEmpty(it.Buffer[i]))
                    {
                        it.Spoken.Add(it.Buffer[i]);
                    }
                }

                declared.Add(it);
                byId[node.Id] = it;
            }

            AddCrossings(render, byId, unlocatable);
            ResolveRowCells(declared);

            for (int i = 0; i < declared.Count; i++)
            {
                // A dossier node is located by what it POINTS at rather than by a widget of its own
                // (<see cref="Evidence"/>), so it is not one of these: reporting ten of them per opened
                // card buried the one node that really had nothing behind it.
                if (Evidence(declared[i]) == null)
                {
                    unlocatable.Add(
                        Made(null, declared[i].Key, "no widget behind this node's id", null)
                    );
                }
            }

            return declared;
        }

        /// <summary>Whether a node came from the contributions every page is given rather than from the
        /// page itself (<see cref="Screens.Screen.BuildShared"/>): the bar a collapsed tutorial leaves
        /// over whatever is on screen, and the chat panel's new-message button. Both are declared into
        /// stops of their own, which is what makes "whose node is this" a structural question with an
        /// exact answer - and the same answer on a page that declares the bar among its own stops.
        /// </summary>
        private static bool Contributed(GraphNode node)
        {
            return Screens.Screen.IsSharedStop(node.StopKey);
        }

        /// <summary>
        /// What the player hears while CROSSING into a node, added to what that node says.
        ///
        /// A table's column captions are drawn once, above the columns, and no node's readout repeats
        /// them: the sheet hangs them on the EDGES instead (<see cref="Core.UI.GraphSheet"/> labels a
        /// left/right step with the destination column's header and a vertical one with the
        /// destination row's name), and the navigator speaks a label by handing it to
        /// <see cref="GraphAnnouncer.Compose"/> as the crossed edge - the same call
        /// <c>GraphNavigator</c> makes with <c>KeyGraph.Move</c>'s transition label. So the line is
        /// composed here through that one path rather than re-derived: a caption the player hears is a
        /// caption this file accounts for, and a caption it invents is still owed an explanation by
        /// the honesty check.
        /// </summary>
        private static void AddCrossings(
            GraphRender render,
            Dictionary<ControlId, Declared> byId,
            List<Breach> unlocatable
        )
        {
            foreach (GraphNode from in render.Order)
            {
                foreach (KeyValuePair<GraphDir, Transition> edge in from.Transitions)
                {
                    Transition crossing = edge.Value;
                    if (crossing == null || string.IsNullOrEmpty(crossing.Label))
                    {
                        continue;
                    }

                    GraphNode to = render.NodeAt(crossing.Destination);
                    Declared landing;
                    if (to == null || to == from || !byId.TryGetValue(to.Id, out landing))
                    {
                        continue;
                    }

                    string line;
                    try
                    {
                        line = GraphAnnouncer.Compose(from, to, crossing.Label);
                    }
                    catch (Exception e)
                    {
                        unlocatable.Add(
                            Made(landing.Widget, landing.Key, "crossing into it threw", e.Message)
                        );
                        continue;
                    }

                    if (!string.IsNullOrEmpty(line) && !landing.Spoken.Contains(line))
                    {
                        landing.Spoken.Add(line);
                    }
                }
            }
        }

        /// <summary>
        /// The widget behind a table cell, which its id does not carry.
        ///
        /// A sheet keys its cells structurally and gives the ROW's domain object to the primary cell
        /// alone (<see cref="Core.UI.GraphSheet"/>), so a metadata cell's id resolves to nothing and the
        /// placement and tooltip answers go blind on every column but the first - which on this family
        /// meant a whole table reported unlocatable and its cells' dossiers reported as claims with
        /// nothing behind them. The sheet's own public stamps are the way back: every cell of a row
        /// carries the same <see cref="TableRow"/> object and its own
        /// <see cref="NodeVtable.Column"/>, so the row's widget is whatever the column-0 cell of the
        /// same row resolved to.
        ///
        /// It is the ROW's widget, not the cell's: a cell's own transform is closure state inside the
        /// screen's vtable and nothing public names it. That is honest for both answers here - the
        /// cell is drawn inside the row and its tooltip hangs under it - at the cost of precision in
        /// one direction only: a cell claiming a tooltip is now satisfied by any tooltip anywhere in
        /// its row.
        /// </summary>
        private static void ResolveRowCells(List<Declared> declared)
        {
            Dictionary<string, AgeTransform> rows = new Dictionary<string, AgeTransform>();
            for (int i = 0; i < declared.Count; i++)
            {
                Declared it = declared[i];
                TableRow row = RowOf(it);
                if (row == null || row.Key == null || it.Widget == null || it.Node.Vtable.Column != 0)
                {
                    continue;
                }

                rows[row.Key] = it.Widget;
            }

            for (int i = 0; i < declared.Count; i++)
            {
                Declared it = declared[i];
                TableRow row = RowOf(it);
                AgeTransform widget;
                if (
                    it.Widget == null
                    && row != null
                    && row.Key != null
                    && rows.TryGetValue(row.Key, out widget)
                )
                {
                    it.Widget = widget;
                }
            }
        }

        private static TableRow RowOf(Declared it)
        {
            return it.Node == null || it.Node.Vtable == null ? null : it.Node.Vtable.Row;
        }

        /// <summary>Which tooltip this node's pointer goes to, as the node itself declares it
        /// (<see cref="NodeVtable.PointsAt"/>). Never re-derived from the widget tree: the deepest
        /// tooltip inside a card is often decoration, and a second opinion that picked it reported a
        /// defect on screens whose pointing was right all along. The reading itself is
        /// <see cref="AgeWidgets.AimOf"/> - shared with the gate that acts on it.</summary>
        internal static AgeTooltip AimOf(Declared node)
        {
            return AgeWidgets.AimOf(node == null || node.Node == null ? null : node.Node.Vtable);
        }

        /// <summary>
        /// The widget a node's tooltip claim is about: its own where it has one, else the widget its
        /// pointer is aimed at.
        ///
        /// A DOSSIER node (<see cref="ES2Access.UI.TooltipChildren"/>) is keyed structurally and reads
        /// off no widget at all - it exists because one card carries several dossiers and only the one
        /// the pointer is on can ever be drawn - so a check that asked its widget filed every one of
        /// them as a promise with nothing behind it while the player could read it perfectly well. The
        /// aim is the same authority <see cref="Covering"/> and the misaimed check already use.
        ///
        /// Only for the tooltip answer. Placement stays blind to these on purpose: a dossier node hangs
        /// UNDER its card and takes no place in the popup's own down-the-page order, so measuring one
        /// against the card drawn beside it would report a walk that jumps around as a defect.
        ///
        /// The RULE is <see cref="DrawnBy"/>'s, so that this and the gate acting on the same answer
        /// can never disagree. <see cref="Declared.Widget"/> is preferred because it may have been
        /// enriched since (<see cref="ResolveRowCells"/> lends a table cell its row's widget), which can
        /// only find a widget the shared rule does not - the safe direction: the gate lets a node with
        /// no evidence through, and this still holds it to the paint test.
        /// </summary>
        internal static AgeTransform Evidence(Declared node)
        {
            if (node == null)
            {
                return null;
            }

            if (node.Widget != null)
            {
                return node.Widget;
            }

            return DrawnBy.Of(node.Node == null ? null : node.Node.Declared);
        }

        /// <summary>
        /// Whether this node undertakes to have something in its review buffer that only the game's
        /// own hover can put there: a <see cref="TooltipMode.Indicate"/> section whose own
        /// would-it-draw test passes right now. That test is the section's, asked here rather than
        /// re-derived, so the check and the reading cannot disagree about which tooltips are real.
        ///
        /// <see cref="TooltipMode.Announce"/> sections are not asked: their words are read straight off
        /// the tooltip and spoken, so they reach the player whether or not anything ever draws, and
        /// nothing about the aim can take them away. <see cref="TooltipMode.None"/> sections are the
        /// control's own drawn text and involve no tooltip at all.
        /// </summary>
        internal static bool Promises(Declared node)
        {
            NodeVtable vtable = node.Node != null ? node.Node.Vtable : null;
            IList<NodeSection> sections = vtable != null ? vtable.Sections : null;
            for (int i = 0; sections != null && i < sections.Count; i++)
            {
                NodeSection section = sections[i];
                if (
                    section != null
                    && section.Mode == TooltipMode.Indicate
                    && (section.Indicates == null || section.Indicates())
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The nodes whose own widget is this one, holds it, or hangs inside it - the ones
        /// a player standing anywhere near this tooltip would be on.</summary>
        internal static List<Declared> Covering(
            List<Declared> declared,
            AgeTransform owner,
            AgeTooltip tooltip = null
        )
        {
            List<Declared> covering = new List<Declared>();
            for (int i = 0; i < declared.Count; i++)
            {
                AgeTransform widget = declared[i].Widget;
                if (
                    widget != null
                    && (AgeWidgets.Under(owner, widget) || AgeWidgets.Under(widget, owner))
                )
                {
                    covering.Add(declared[i]);
                    continue;
                }

                // A DOSSIER NODE covers by its AIM, not by a widget. A node made by
                // ES2Access.UI.TooltipChildren is keyed structurally and reads off no widget of its
                // own - it exists precisely because one widget carries several dossiers and only the
                // one the pointer is on can ever be drawn - so the widget walk above misses every one
                // of them and the audit filed each as "uncovered" while the player could read it
                // perfectly well (owner ruling, batch 7). What it declares it POINTS at is the same
                // authority the misaimed check uses.
                if (tooltip != null && AgeWidgets.SameTooltip(AimOf(declared[i]), tooltip))
                {
                    covering.Add(declared[i]);
                }
            }

            return covering;
        }

        internal static bool CarriedBy(List<Declared> covering, string content)
        {
            IList<string> lines = AgeText.Lines(content);
            if (lines.Count == 0)
            {
                return true;
            }

            List<string> reduced = new List<string>();
            for (int i = 0; i < covering.Count; i++)
            {
                for (int j = 0; j < covering[i].Spoken.Count; j++)
                {
                    reduced.Add(Reduce(covering[i].Spoken[j]));
                }
            }

            return Contains(reduced, lines[0]);
        }

        /// <summary>A string reduced to the letters and digits in it, lowercased: what two readings of
        /// the same text share when one of them has been through a separator, a colour tag or a line
        /// break the other has not.</summary>
        internal static string Reduce(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            System.Text.StringBuilder reduced = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsLetterOrDigit(c))
                {
                    reduced.Append(char.ToLowerInvariant(c));
                }
            }

            return reduced.ToString();
        }

        internal static bool Contains(List<string> reduced, string piece)
        {
            string want = Reduce(piece);
            if (want.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < reduced.Count; i++)
            {
                if (reduced[i].IndexOf(want, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static Breach Made(
            AgeTransform widget,
            string where,
            string what,
            string detail
        )
        {
            Breach breach = new Breach();
            breach.Where = where;
            breach.What = what;
            breach.Detail = detail;
            try
            {
                if (widget != null)
                {
                    breach.Rect = AgeWidgets.Clipped(widget).GetGlobalPosition();
                    breach.HasRect = true;
                }
            }
            catch (Exception) { }

            return breach;
        }

        internal static string Name(AgeTransform widget)
        {
            return widget == null ? "(no widget)" : widget.name;
        }

        internal static string Excerpt(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            string one = text.Replace('\n', ' ');
            return one.Length <= 120 ? one : one.Substring(0, 117) + "...";
        }

        /// <summary>One bucket of breaches. <paramref name="max"/> caps how many are listed - zero
        /// lists them all - and a capped list ends with a <c>{"more": n}</c> entry saying how many were
        /// left out, so a caller reading a truncated answer can never mistake it for a short one.
        /// </summary>
        internal static void WriteBreaches(
            JsonTextWriter json,
            string name,
            List<Breach> breaches,
            int max = 0
        )
        {
            json.WritePropertyName(name);
            json.WriteStartArray();
            for (int i = 0; i < breaches.Count && (max <= 0 || i < max); i++)
            {
                Breach breach = breaches[i];
                json.WriteStartObject();
                json.WritePropertyName("where");
                json.WriteValue(breach.Where);
                json.WritePropertyName("what");
                json.WriteValue(breach.What);
                if (!string.IsNullOrEmpty(breach.Detail))
                {
                    json.WritePropertyName("text");
                    json.WriteValue(breach.Detail);
                }

                if (breach.HasRect)
                {
                    json.WritePropertyName("rect");
                    json.WriteStartArray();
                    json.WriteValue(Math.Round(breach.Rect.xMin));
                    json.WriteValue(Math.Round(breach.Rect.yMin));
                    json.WriteValue(Math.Round(breach.Rect.width));
                    json.WriteValue(Math.Round(breach.Rect.height));
                    json.WriteEndArray();
                }

                json.WriteEndObject();
            }

            if (max > 0 && breaches.Count > max)
            {
                json.WriteStartObject();
                json.WritePropertyName("more");
                json.WriteValue(breaches.Count - max);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

    }
}

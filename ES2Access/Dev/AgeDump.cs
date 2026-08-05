using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ES2Access.Loader.Dev;
using ES2Access.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace ES2Access.Dev
{
    /// <summary>
    /// Dumps the live AGE hierarchy as the meaning a screen reader would need, rather than as the
    /// Unity components /gui/game reports. Where GuiDump answers "what objects exist", this answers
    /// "what is on screen, what does it say, and what can the player operate" - it walks
    /// <see cref="AgeTransform.Children"/> (AGE's own runtime child list, which is authoritative and
    /// not the same as the Unity transform tree), reads captions through the game's localizer, and
    /// throws away the decorative primitives that make a raw dump unreadable.
    ///
    /// Root selection mirrors what the player can actually reach: the first visible modal window,
    /// else the visible screen, else every shown gui panel that is really on screen. Note that
    /// several out-of-game pages - the main menu among them - are plain GuiWindows rather than
    /// GuiScreens, so the shown-panels fallback is the usual case outside a running game.
    ///
    /// The top-level "windows" array says what is up at a glance: the panels the game lists as
    /// shown, with "visible" meaning visible all the way up the hierarchy (a panel can sit on that
    /// list for as long as the window above it stays hidden) and "isReady" the game's own
    /// GuiWindow.IsReady, which is the "shown, animated in, interactive" gate. After a window=
    /// lookup misses, "windows" lists the whole registry instead so the caller can see the names.
    ///
    /// Query parameters:
    ///   window=Name    dump this node instead of what the player is looking at. The name is
    ///                  matched, in order, against every registered GuiWindow, then every shown
    ///                  GuiPanel (each by its GuiWindow name, its type name or its GameObject
    ///                  name), then breadth-first against the GameObject name of every
    ///                  AgeTransform under those - so anything the dump can reach can be asked
    ///                  for by name, including a banner or a panel that is not a window at all,
    ///                  and whether or not it is currently shown. depth=, visibleOnly= and
    ///                  fields= all apply relative to the node that matched. A name nothing
    ///                  answers to is the error "no window named 'X'" - never silence - and a
    ///                  match with nothing to report answers a "note" saying which of depth= or
    ///                  visibleOnly= emptied it, so a window whose children are drawn is never
    ///                  reported as nothing.
    ///   depth=N        levels below each root (default <see cref="DefaultDepth"/>). A node sitting
    ///                  on that cutoff is reported even when it is a bare container, carrying
    ///                  "more": true - it has children this dump did not walk, and pruning it as
    ///                  decoration would report an empty tree for a window that is fully drawn.
    ///   visibleOnly=0  include hidden roots and subtrees whose AgeTransform.Visible is false
    ///                  (default 1, skip them)
    ///   fields=a,b,c   answer plain text instead of JSON: one line per widget in tree order,
    ///                  indented by depth, carrying only these fields and only where they have
    ///                  something to say (see <see cref="Fields"/>). The JSON dump of a whole window
    ///                  is thousands of lines that a reader wanting "which label sits where" has to
    ///                  wade through; this is that question answered directly.
    ///
    /// Per node: "name" (GameObject), "kind" (button/toggle/label/... from the AgeControl or
    /// AgePrimitive on the node, else "group"), "text", "tooltip", "value", "rect", "children", and
    /// "interactable" - true only when the node carries an AgeControl and it and every ancestor are
    /// both Visible and Enable, since a disabled ancestor kills a whole subtree. "visible" and
    /// "enabled" appear only when false; anything not mentioned is on. z-order occlusion is not
    /// considered. Nodes with nothing to say - no control, no text, no tooltip, no value and no
    /// surviving children - are pruned, which is what keeps the tree screen-reader sized.
    ///
    /// Main-thread only (reads live scene objects). Every per-node read is guarded: a getter that
    /// throws costs that one field, not the dump.
    /// </summary>
    internal static class AgeDump
    {
        public const int DefaultDepth = 12;

        private const int MaxNodes = 4000;
        private const int MaxTextLength = 200;
        private const int MaxRegisteredWindowsListed = 300;

        // How much of the hierarchy a window= lookup may sweep looking for a named widget. Large
        // enough to reach the leaves of every ES2 window, bounded so a lookup can never hang.
        private const int MaxSearchNodes = 20000;

        // How far under a control to look for the label that captions it. Deep enough for the
        // frame/backdrop wrappers AGE prefabs nest captions in, shallow enough not to adopt the
        // text of a whole panel.
        private const int CaptionSearchDepth = 4;

        private sealed class Budget
        {
            public int Visited;
            public bool Truncated;
        }

        /// <summary>One interpreted widget. Built in full before anything is written because
        /// pruning a node depends on whether its children survived, which a streaming writer
        /// cannot take back.</summary>
        private sealed class Node
        {
            public string Name;
            public string Kind;
            public string Text;
            public string Tooltip;
            public string Value;
            public bool Visible = true;
            public bool Enabled = true;
            public bool Interactable;
            public bool HasControl;
            public bool More;
            public bool HasRect;
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public readonly List<Node> Children = new List<Node>();

            public bool Speaks
            {
                get
                {
                    return HasControl
                        || Text != null
                        || Tooltip != null
                        || Value != null
                        || Children.Count > 0
                        || More;
                }
            }
        }

        /// <summary>One walk of the hierarchy and everything an answer needs to describe it, so the
        /// JSON dump and the plain-text projection report the same tree rather than two walks that
        /// could disagree.</summary>
        private sealed class Scan
        {
            public GuiManager Gui;
            public string Source;
            public string Error;
            public string Note;
            public string Matched;
            public readonly Budget Budget = new Budget();
            public readonly List<Node> Nodes = new List<Node>();
        }

        private static Scan Walk(string window, int depth, bool visibleOnly)
        {
            Scan scan = new Scan();
            scan.Gui = GuiService();
            List<AgeTransform> roots = Roots(scan.Gui, window, visibleOnly, scan);
            foreach (AgeTransform root in roots)
            {
                Node node = Build(root, depth, visibleOnly, true, scan.Budget);
                if (node != null)
                {
                    scan.Nodes.Add(node);
                }
            }

            if (scan.Error == null && scan.Nodes.Count == 0)
            {
                scan.Note = EmptyReason(roots, depth, visibleOnly, scan.Matched);
            }

            return scan;
        }

        /// <summary>Why an answer came back empty. A dump that found its node and still reports
        /// nothing is the dangerous case - the caller reads silence as "not drawn" - so it always
        /// says which of depth= or visibleOnly= emptied it.</summary>
        private static string EmptyReason(
            List<AgeTransform> roots,
            int depth,
            bool visibleOnly,
            string matched
        )
        {
            string subject = matched == null ? "nothing is on screen" : "'" + matched + "' matched";
            if (roots.Count == 0)
            {
                return matched == null ? "nothing is on screen to dump" : subject;
            }

            if (depth < 0)
            {
                return subject + ", but depth=" + depth + " walks nothing";
            }

            if (visibleOnly && !OnScreen(roots[0]))
            {
                return subject + ", but it is not visible; retry with visibleOnly=0";
            }

            return subject + ", but it holds no widget with anything to say at depth=" + depth;
        }

        public static string Dump(string window, int depth, bool visibleOnly)
        {
            Scan scan = Walk(window, depth, visibleOnly);

            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("source");
                json.WriteValue(scan.Source);
                if (!string.IsNullOrEmpty(window))
                {
                    json.WritePropertyName("window");
                    json.WriteValue(window);
                }

                json.WritePropertyName("depth");
                json.WriteValue(depth);
                json.WritePropertyName("visibleOnly");
                json.WriteValue(visibleOnly);
                if (scan.Error != null)
                {
                    json.WritePropertyName("error");
                    json.WriteValue(scan.Error);
                }

                if (scan.Note != null)
                {
                    json.WritePropertyName("note");
                    json.WriteValue(scan.Note);
                }

                json.WritePropertyName("windows");
                WriteWindowSummary(json, scan.Gui, scan.Error != null);

                json.WritePropertyName("roots");
                json.WriteStartArray();
                int written = 0;
                foreach (Node node in scan.Nodes)
                {
                    written += Write(json, node);
                }

                json.WriteEndArray();
                json.WritePropertyName("nodeCount");
                json.WriteValue(written);
                json.WritePropertyName("visitedCount");
                json.WriteValue(scan.Budget.Visited);
                json.WritePropertyName("truncated");
                json.WriteValue(scan.Budget.Truncated);
                json.WriteEndObject();
            });
        }

        /// <summary>The field names <c>?fields=</c> understands. A projection prints them in the
        /// order the caller asked for, and prints none of them where the node has nothing to report
        /// - an absent rect, a node with no control, a control that is enabled.</summary>
        public static readonly string[] Fields =
        {
            "name",
            "kind",
            "text",
            "tooltip",
            "rect",
            "interactable",
            "enabled",
        };

        /// <summary>The field names in a <c>?fields=</c> value, trimmed and lower-cased; empty when
        /// the caller asked for nothing.</summary>
        public static List<string> ParseFields(string raw)
        {
            List<string> fields = new List<string>();
            foreach (string part in (raw ?? string.Empty).Split(','))
            {
                string name = part.Trim().ToLowerInvariant();
                if (name.Length > 0)
                {
                    fields.Add(name);
                }
            }

            return fields;
        }

        /// <summary>The first requested field this dump cannot project, or null when all of them
        /// are known.</summary>
        public static string UnknownField(List<string> fields)
        {
            foreach (string field in fields)
            {
                if (Array.IndexOf(Fields, field) < 0)
                {
                    return field;
                }
            }

            return null;
        }

        public static string KnownFields()
        {
            return string.Join(", ", Fields);
        }

        /// <summary>The same tree as <see cref="Dump"/>, projected onto the requested fields: one
        /// line per widget in tree order, two spaces of indent per level. A node with none of the
        /// requested fields prints no line at all, so asking for text= gives back the screen's
        /// words and nothing else.</summary>
        public static string Lines(string window, int depth, bool visibleOnly, List<string> fields)
        {
            Scan scan = Walk(window, depth, visibleOnly);
            StringBuilder text = new StringBuilder();
            if (scan.Error != null)
            {
                text.Append("error: ").Append(scan.Error).Append('\n');
            }

            if (scan.Note != null)
            {
                text.Append("note: ").Append(scan.Note).Append('\n');
            }

            foreach (Node node in scan.Nodes)
            {
                Flatten(text, node, 0, fields);
            }

            if (scan.Budget.Truncated)
            {
                text.Append("(truncated at ").Append(MaxNodes).Append(" nodes)\n");
            }

            return text.ToString();
        }

        private static void Flatten(StringBuilder text, Node node, int depth, List<string> fields)
        {
            bool wrote = false;
            foreach (string field in fields)
            {
                string token = Token(node, field);
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                if (!wrote)
                {
                    text.Append(' ', depth * 2);
                    wrote = true;
                }
                else
                {
                    text.Append(' ');
                }

                text.Append(token);
            }

            if (wrote)
            {
                text.Append('\n');
            }

            foreach (Node child in node.Children)
            {
                Flatten(text, child, depth + 1, fields);
            }
        }

        private static string Token(Node node, string field)
        {
            switch (field)
            {
                case "name":
                    return node.Name;
                case "kind":
                    return node.Kind;
                case "text":
                    return node.Text == null ? null : "text=\"" + node.Text + "\"";
                case "tooltip":
                    return node.Tooltip == null ? null : "tooltip=\"" + node.Tooltip + "\"";
                case "rect":
                    return node.HasRect
                        ? "rect=["
                            + node.X
                            + ","
                            + node.Y
                            + ","
                            + node.Width
                            + ","
                            + node.Height
                            + "]"
                        : null;
                case "interactable":
                    return node.HasControl
                        ? "interactable=" + (node.Interactable ? "true" : "false")
                        : null;
                case "enabled":
                    return node.Enabled ? null : "enabled=false";
                default:
                    return null;
            }
        }

        private static GuiManager GuiService()
        {
            try
            {
                return Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // What the player is looking at, in the order the game itself would hand input to it.
        private static List<AgeTransform> Roots(
            GuiManager gui,
            string window,
            bool visibleOnly,
            Scan scan
        )
        {
            List<AgeTransform> roots = new List<AgeTransform>();
            if (gui == null)
            {
                scan.Source = "none";
                scan.Error = "the gui service is not available yet";
                return roots;
            }

            if (!string.IsNullOrEmpty(window))
            {
                scan.Source = "window";
                AgeTransform found = Locate(gui, window, scan);
                if (found == null)
                {
                    scan.Error =
                        "no window named '" + window + "'; see windows[] for what is registered";
                }
                else
                {
                    Add(roots, found);
                }

                return roots;
            }

            GuiModalWindow modal = null;
            try
            {
                modal = gui.GetFirstVisibleModalWindow();
            }
            catch (Exception) { }

            if (modal != null)
            {
                scan.Source = "modal";
                Add(roots, RootTransform(modal));
                return roots;
            }

            GuiScreen screen = null;
            try
            {
                screen = gui.VisibleScreen;
            }
            catch (Exception) { }

            if (screen != null)
            {
                scan.Source = "screen";
                Add(roots, RootTransform(screen));
                return roots;
            }

            // A panel stays on the shown list while an ancestor of it is hidden - the options tab
            // panel sits there the whole time the main menu is up - so "shown" alone is not enough
            // to conclude the player can see it.
            scan.Source = "shownPanels";
            foreach (Amplitude.Unity.Gui.GuiPanel panel in ShownPanels(gui))
            {
                AgeTransform transform = RootTransform(panel);
                if (!visibleOnly || OnScreen(transform))
                {
                    Add(roots, transform);
                }
            }

            // A shown panel nested inside another shown panel is already in the tree; listing it
            // again as a root would dump it twice.
            DropNested(roots);
            return roots;
        }

        private static void Add(List<AgeTransform> roots, AgeTransform transform)
        {
            if (transform != null && !roots.Contains(transform))
            {
                roots.Add(transform);
            }
        }

        private static void DropNested(List<AgeTransform> roots)
        {
            for (int i = roots.Count - 1; i >= 0; i--)
            {
                AgeTransform parent = Parent(roots[i]);
                while (parent != null)
                {
                    if (roots.Contains(parent))
                    {
                        roots.RemoveAt(i);
                        break;
                    }

                    parent = Parent(parent);
                }
            }
        }

        private static AgeTransform Parent(AgeTransform transform)
        {
            try
            {
                return transform.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform RootTransform(Amplitude.Unity.Gui.GuiPanel panel)
        {
            try
            {
                return panel.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Visible in its own right and under nothing hidden - the difference between a
        /// panel the game still lists as shown and one the player is actually looking at.</summary>
        private static bool OnScreen(AgeTransform transform)
        {
            try
            {
                return transform != null && transform.IsVisibleInHierarchy();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static List<Amplitude.Unity.Gui.GuiPanel> ShownPanels(GuiManager gui)
        {
            try
            {
                List<Amplitude.Unity.Gui.GuiPanel> panels = gui.ShownGuiPanels;
                if (panels != null)
                {
                    return new List<Amplitude.Unity.Gui.GuiPanel>(panels);
                }
            }
            catch (Exception) { }

            return new List<Amplitude.Unity.Gui.GuiPanel>();
        }

        // The engine keeps its window registry protected, so a lookup that does not want the
        // "unknown window" error the public GetWindow(StaticString) logs has to read it directly.
        private static List<Amplitude.Unity.Gui.GuiWindow> RegisteredWindows(GuiManager gui)
        {
            try
            {
                FieldInfo field = typeof(Amplitude.Unity.Gui.GuiManager).GetField(
                    "guiWindowsFromBackToFront",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                List<Amplitude.Unity.Gui.GuiWindow> windows =
                    field == null
                        ? null
                        : field.GetValue(gui) as List<Amplitude.Unity.Gui.GuiWindow>;
                if (windows != null)
                {
                    return new List<Amplitude.Unity.Gui.GuiWindow>(windows);
                }
            }
            catch (Exception) { }

            return new List<Amplitude.Unity.Gui.GuiWindow>();
        }

        /// <summary>The node <c>window=</c> means: a registered GuiWindow, else a shown GuiPanel
        /// (both by GuiWindow name, type name or GameObject name), else - breadth-first, so the
        /// shallowest wins - any AgeTransform under either whose GameObject carries that name. The
        /// last step is what makes a name read off an earlier dump or off windows[] answer even
        /// when it belongs to a banner rather than to a window. Visibility is not a filter here: a
        /// hidden node is still found, and visibleOnly= then decides what its dump contains.
        /// </summary>
        private static AgeTransform Locate(GuiManager gui, string wanted, Scan scan)
        {
            List<AgeTransform> searched = new List<AgeTransform>();
            foreach (Amplitude.Unity.Gui.GuiWindow window in RegisteredWindows(gui))
            {
                if (
                    Matches(WindowName(window), wanted)
                    || Matches(window.GetType().Name, wanted)
                    || Matches(GameObjectName(window), wanted)
                )
                {
                    scan.Matched = WindowName(window) ?? GameObjectName(window);
                    return RootTransform(window);
                }

                Add(searched, RootTransform(window));
            }

            foreach (Amplitude.Unity.Gui.GuiPanel panel in ShownPanels(gui))
            {
                if (
                    Matches(WindowName(panel as Amplitude.Unity.Gui.GuiWindow), wanted)
                    || Matches(panel.GetType().Name, wanted)
                    || Matches(GameObjectName(panel), wanted)
                )
                {
                    scan.Source = "panel";
                    scan.Matched = GameObjectName(panel);
                    return RootTransform(panel);
                }

                Add(searched, RootTransform(panel));
            }

            AgeTransform widget = FindNamed(searched, wanted);
            if (widget != null)
            {
                scan.Source = "widget";
                scan.Matched = GameObjectName(widget);
            }

            return widget;
        }

        // Breadth-first over every root's subtree at once, so the match closest to a root wins
        // rather than whichever root happened to be listed first.
        private static AgeTransform FindNamed(List<AgeTransform> roots, string wanted)
        {
            List<AgeTransform> level = roots;
            int visited = 0;
            while (level.Count > 0 && visited < MaxSearchNodes)
            {
                List<AgeTransform> next = new List<AgeTransform>();
                foreach (AgeTransform transform in level)
                {
                    visited++;
                    if (Matches(GameObjectName(transform), wanted))
                    {
                        return transform;
                    }

                    next.AddRange(Children(transform));
                }

                level = next;
            }

            return null;
        }

        private static bool Matches(string candidate, string wanted)
        {
            return !string.IsNullOrEmpty(candidate)
                && string.Compare(candidate, wanted, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static string WindowName(Amplitude.Unity.Gui.GuiWindow window)
        {
            try
            {
                return window.Name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string GameObjectName(Component component)
        {
            try
            {
                return component.gameObject.name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What is up at a glance: normally the panels the game says are shown, or the
        /// whole registry when a window= lookup missed and the caller needs to see the names.
        /// </summary>
        private static void WriteWindowSummary(
            JsonTextWriter json,
            GuiManager gui,
            bool listRegistered
        )
        {
            json.WriteStartArray();
            if (gui == null)
            {
                json.WriteEndArray();
                return;
            }

            if (listRegistered)
            {
                int listed = 0;
                foreach (Amplitude.Unity.Gui.GuiWindow window in RegisteredWindows(gui))
                {
                    if (listed++ >= MaxRegisteredWindowsListed)
                    {
                        break;
                    }

                    WriteWindowEntry(json, window);
                }
            }
            else
            {
                foreach (Amplitude.Unity.Gui.GuiPanel panel in ShownPanels(gui))
                {
                    WriteWindowEntry(json, panel);
                }
            }

            json.WriteEndArray();
        }

        private static void WriteWindowEntry(
            JsonTextWriter json,
            Amplitude.Unity.Gui.GuiPanel panel
        )
        {
            json.WriteStartObject();
            json.WritePropertyName("name");
            json.WriteValue(
                WindowName(panel as Amplitude.Unity.Gui.GuiWindow) ?? GameObjectName(panel)
            );
            json.WritePropertyName("type");
            json.WriteValue(panel.GetType().Name);
            json.WritePropertyName("visible");
            json.WriteValue(OnScreen(RootTransform(panel)));
            GuiWindow ready = panel as GuiWindow;
            if (ready != null)
            {
                json.WritePropertyName("isReady");
                json.WriteValue(IsReady(ready));
            }

            json.WriteEndObject();
        }

        private static bool IsReady(GuiWindow window)
        {
            try
            {
                return window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Node Build(
            AgeTransform transform,
            int depth,
            bool visibleOnly,
            bool ancestorsActive,
            Budget budget
        )
        {
            if (transform == null)
            {
                return null;
            }

            if (budget.Visited >= MaxNodes)
            {
                budget.Truncated = true;
                return null;
            }

            bool visible = Flag(transform, true);
            if (visibleOnly && !visible)
            {
                return null;
            }

            budget.Visited++;
            bool enabled = Flag(transform, false);
            AgeControl control = Control(transform);

            Node node = new Node
            {
                Name = GameObjectName(transform),
                Visible = visible,
                Enabled = enabled,
                HasControl = control != null,
                Interactable = control != null && ancestorsActive && visible && enabled,
            };

            node.Kind = Kind(transform, control);
            node.Text = control == null ? LabelText(transform) : Caption(transform);
            node.Tooltip = Tooltip(transform);
            node.Value = Value(control);
            ReadRect(transform, node);

            if (depth > 0)
            {
                bool childrenActive = ancestorsActive && visible && enabled;
                foreach (AgeTransform child in Children(transform))
                {
                    Node built = Build(child, depth - 1, visibleOnly, childrenActive, budget);
                    if (built != null)
                    {
                        node.Children.Add(built);
                    }
                }
            }
            else
            {
                // The depth cutoff, not decoration: this node has children the walk did not look
                // at, so it says nothing yet. Pruning it here empties whole windows whose top
                // layers are bare containers - which is how depth=1 on GameOverlayWindow used to
                // answer an empty tree while its banners were drawn.
                node.More = HasChildren(transform, visibleOnly);
            }

            // Decoration: a frame, quad or empty container that says nothing and holds nothing.
            return node.Speaks ? node : null;
        }

        private static bool HasChildren(AgeTransform transform, bool visibleOnly)
        {
            foreach (AgeTransform child in Children(transform))
            {
                if (!visibleOnly || Flag(child, true))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<AgeTransform> Children(AgeTransform transform)
        {
            try
            {
                return transform.Children ?? new List<AgeTransform>();
            }
            catch (Exception)
            {
                return new List<AgeTransform>();
            }
        }

        private static bool Flag(AgeTransform transform, bool wantVisible)
        {
            try
            {
                return wantVisible ? transform.Visible : transform.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static AgeControl Control(AgeTransform transform)
        {
            try
            {
                return transform.AgeControl;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgePrimitive Primitive(AgeTransform transform)
        {
            try
            {
                return transform.AgePrimitive;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ReadRect(AgeTransform transform, Node node)
        {
            try
            {
                Rect rect = transform.GetGlobalPosition();
                node.HasRect = true;
                node.X = Mathf.RoundToInt(rect.x);
                node.Y = Mathf.RoundToInt(rect.y);
                node.Width = Mathf.RoundToInt(rect.width);
                node.Height = Mathf.RoundToInt(rect.height);
            }
            catch (Exception) { }
        }

        private static string Kind(AgeTransform transform, AgeControl control)
        {
            if (control != null)
            {
                if (control is AgeControlButton)
                {
                    return "button";
                }

                if (control is AgeControlToggle)
                {
                    return "toggle";
                }

                if (control is AgeControlDropList)
                {
                    return "dropdown";
                }

                if (control is AgeControlSlider)
                {
                    return "slider";
                }

                if (control is AgeControlScrollView)
                {
                    return "scrollview";
                }

                if (control is AgeControlScrollBar)
                {
                    return "scrollbar";
                }

                if (control is AgeControlTextArea)
                {
                    return "textfield";
                }

                if (control is AgeControlKeyBindingField)
                {
                    return "keybinding";
                }

                if (control is AgeControlHoverArea)
                {
                    return "hoverarea";
                }

                return Suffix(control.GetType().Name, "AgeControl");
            }

            AgePrimitive primitive = Primitive(transform);
            if (primitive == null)
            {
                return "group";
            }

            if (primitive is AgePrimitiveLabel || primitive is AgePrimitiveCurvedLabel)
            {
                return "label";
            }

            if (primitive is AgePrimitiveHistogramBase)
            {
                return "gauge";
            }

            if (primitive is AgePrimitiveMovie)
            {
                return "movie";
            }

            if (primitive is AgePrimitiveFrame)
            {
                return "frame";
            }

            if (primitive is AgePrimitiveQuad)
            {
                return "image";
            }

            return Suffix(primitive.GetType().Name, "AgePrimitive");
        }

        private static string Suffix(string typeName, string prefix)
        {
            string tail = typeName.StartsWith(prefix)
                ? typeName.Substring(prefix.Length)
                : typeName;
            return tail.Length == 0 ? "control" : tail.ToLowerInvariant();
        }

        private static string LabelText(AgeTransform transform)
        {
            return Shorten(AgeText.Label(Primitive(transform) as AgePrimitiveLabel));
        }

        // A control's caption is the first label under it. Labels inside a nested control belong to
        // that control, so those subtrees are left alone.
        private static string Caption(AgeTransform transform)
        {
            string own = LabelText(transform);
            if (own != null)
            {
                return own;
            }

            return FindCaption(transform, CaptionSearchDepth);
        }

        private static string FindCaption(AgeTransform transform, int depth)
        {
            if (depth <= 0)
            {
                return null;
            }

            foreach (AgeTransform child in Children(transform))
            {
                if (child == null || !Flag(child, true))
                {
                    continue;
                }

                if (Control(child) != null)
                {
                    continue;
                }

                string text = LabelText(child);
                if (text != null)
                {
                    return text;
                }

                text = FindCaption(child, depth - 1);
                if (text != null)
                {
                    return text;
                }
            }

            return null;
        }

        private static string Tooltip(AgeTransform transform)
        {
            try
            {
                AgeTooltip tooltip = transform.AgeTooltip;
                return tooltip == null ? null : Clean(tooltip.Content);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Value(AgeControl control)
        {
            try
            {
                AgeControlToggle toggle = control as AgeControlToggle;
                if (toggle != null)
                {
                    return toggle.State ? "on" : "off";
                }

                AgeControlSlider slider = control as AgeControlSlider;
                if (slider != null)
                {
                    return Number(slider.CurrentValue)
                        + " of "
                        + Number(slider.MinValue)
                        + ".."
                        + Number(slider.MaxValue);
                }

                AgeControlDropList list = control as AgeControlDropList;
                if (list != null)
                {
                    int selected = list.SelectedItem;
                    string label =
                        selected >= 0 && list.LabelTable != null && selected < list.LabelTable.Count
                            ? Clean(list.LabelTable[selected])
                            : null;
                    string index = selected + " of " + list.ItemsCount;
                    return label == null ? index : label + " (" + index + ")";
                }

                AgeControlTextArea text = control as AgeControlTextArea;
                if (text != null && text.Label != null)
                {
                    return Clean(text.Label.Text);
                }
            }
            catch (Exception) { }

            return null;
        }

        private static string Number(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The spoken form of a raw AGE string (see <see cref="AgeText"/>), then made fit
        /// for a one-line-per-node JSON dump: newlines escaped rather than kept, and long bodies
        /// clipped so one verbose tooltip cannot dominate the output.</summary>
        private static string Clean(string raw)
        {
            return Shorten(AgeText.Clean(raw));
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            StringBuilder collapsed = new StringBuilder(text.Length);
            foreach (char character in text)
            {
                if (character == '\n')
                {
                    collapsed.Append("\\n");
                }
                else
                {
                    collapsed.Append(character);
                }
            }

            string result = collapsed.ToString();
            return result.Length > MaxTextLength
                ? result.Substring(0, MaxTextLength) + "..."
                : result;
        }

        private static int Write(JsonTextWriter json, Node node)
        {
            json.WriteStartObject();
            json.WritePropertyName("name");
            json.WriteValue(node.Name);
            json.WritePropertyName("kind");
            json.WriteValue(node.Kind);
            if (node.Text != null)
            {
                json.WritePropertyName("text");
                json.WriteValue(node.Text);
            }

            if (node.Tooltip != null)
            {
                json.WritePropertyName("tooltip");
                json.WriteValue(node.Tooltip);
            }

            if (node.Value != null)
            {
                json.WritePropertyName("value");
                json.WriteValue(node.Value);
            }

            if (node.HasControl)
            {
                json.WritePropertyName("interactable");
                json.WriteValue(node.Interactable);
            }

            if (!node.Visible)
            {
                json.WritePropertyName("visible");
                json.WriteValue(false);
            }

            if (!node.Enabled)
            {
                json.WritePropertyName("enabled");
                json.WriteValue(false);
            }

            if (node.HasRect)
            {
                json.WritePropertyName("rect");
                json.WriteStartArray();
                json.WriteValue(node.X);
                json.WriteValue(node.Y);
                json.WriteValue(node.Width);
                json.WriteValue(node.Height);
                json.WriteEndArray();
            }

            if (node.More)
            {
                json.WritePropertyName("more");
                json.WriteValue(true);
            }

            int written = 1;
            if (node.Children.Count > 0)
            {
                json.WritePropertyName("children");
                json.WriteStartArray();
                foreach (Node child in node.Children)
                {
                    written += Write(json, child);
                }

                json.WriteEndArray();
            }

            json.WriteEndObject();
            return written;
        }
    }
}

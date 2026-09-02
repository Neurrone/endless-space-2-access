using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ES2Access.Core.UI.Graph;
using ES2Access.Loader.Dev;
using ES2Access.Screens;
using ES2Access.UI;
using Newtonsoft.Json;
using UnityEngine;
using Breach = ES2Access.Dev.AuditModel.Breach;
using Declared = ES2Access.Dev.AuditModel.Declared;
using Screen = ES2Access.Screens.Screen;

namespace ES2Access.Dev
{
    /// <summary>
    /// What the FOCUSED screen has never declared at all - hover words and, for the first time,
    /// ACTIONS - measured against everything the engine is drawing rather than against one window.
    ///
    /// It exists because two safeguards said "clean" about a screen with six undeclared dossiers and
    /// four undeclared buttons on it (the galaxy map's orbital planet card, audited by hand
    /// 2026-08-23):
    ///
    /// <list type="number">
    /// <item><see cref="TooltipAudit"/>'s painted half needs <see cref="Screen.RootTransform"/>, and a
    /// screen whose content is the WORLD has no window to name. It answered <c>uncovered: 0</c>
    /// because it never ran that half - a blind spot shaped exactly like a pass. Here the trees come
    /// from the ENGINE (<see cref="AgeDump.LiveRoots"/>) whenever the screen names none, so the
    /// coverage question is asked of every screen.</item>
    /// <item>Nothing anywhere asked the ACTIONS question. A control the player cannot reach is
    /// invisible in speech, in a tree dump and in every tooltip check ever run: the dossier on it may
    /// be perfectly readable through a node that has no way to press it.</item>
    /// </list>
    ///
    /// Both halves judge coverage the same way the tooltip check does - a node covers a widget when
    /// its own widget is that one, holds it, or hangs inside it (<see cref="NotificationAudit.Covering"/>)
    /// - and the ACTIONS half then asks a second question the tooltip half does not: does that node
    /// declare anything that would WORK the control (any of the vtable's handlers). A node that only
    /// reads a button is reported, because a player who can hear a button and not press it has no way
    /// to find that out.
    ///
    /// The tooltip buckets this writes out (<c>uncovered</c>, <c>decoration</c> and the rest) are
    /// <see cref="TooltipAudit"/>'s own, and ONE family of drawn tooltips is deliberately missing from
    /// them: the FIDSI strips on the galaxy map's orbital planet cards, which the mod has decided not
    /// to declare (<c>TooltipAudit.DeliberatelyUndeclared</c> carries the ruling and the reason - owner
    /// ruling 2026-08-24). Nothing else is filtered, and a mis-aimed or promised-and-empty node on
    /// those same cards is still reported.
    ///
    /// <b>What it cannot see.</b> Coverage is decided from the node's own WIDGET, so a tooltip a row
    /// carries as a REVIEWED section (<see cref="GraphNodes.ReviewedTooltipSection"/> - buffer-only
    /// words, no tooltip identity on the section) reads as uncovered on whatever widget it hangs on:
    /// the sorting band's sentence on the advanced battle setup and two of that screen's three range
    /// gauges land in <c>decoration</c> while their words really are in a row's buffer. Read those
    /// buckets against the screen's declaration, not as a list of silences.
    ///
    /// Bounded and on demand: a whole-GUI walk is far too expensive to run per frame, and nothing
    /// here speaks, focuses, moves the pointer or changes what the game is showing.
    /// </summary>
    internal static class CoverageAudit
    {
        /// <summary>The live GUI is a much bigger tree than one window - deeper (a card inside a table
        /// inside a panel inside a window) and wider - so both budgets are larger than
        /// <see cref="TooltipAudit"/>'s, and hitting either is REPORTED rather than swallowed: a
        /// truncated walk that answered "0 uncovered" would be the very failure this file exists
        /// for.</summary>
        private const int MaxDepth = 32;

        private const int MaxWidgets = 60000;

        /// <summary>How many entries of each bucket are written out; the rest are counted as
        /// <c>more</c>. A finding list is read by a person, and the counts are the headline.</summary>
        private const int MaxListed = 40;

        /// <summary>One control the player may not be able to work, in the terms a fix needs.</summary>
        private sealed class Finding
        {
            public string Path;
            public string Component;
            public string Text;
            public string Tooltip;
            public bool Enabled;
            public bool Offered;
            public string Why;
            public string Handler;
            public string Covered;
            public Rect Rect;
        }

        private sealed class Result
        {
            public string Screen;
            public string ScreenName;
            public string Root;
            public string RootSource;
            public int Roots;
            public int Walked;
            public bool BudgetHit;

            public int PaintedControls;
            public int Inert;
            public int CoveredControls;

            public readonly List<Finding> Uncovered = new List<Finding>();
            public readonly List<Finding> HandlerGroups = new List<Finding>();
        }

        /// <summary>The check, on whichever of our screens is focused, as JSON.</summary>
        public static string Json(bool wholeTree)
        {
            try
            {
                ScreenManager screens = ModEntry.Screens;
                Screen screen = screens == null ? null : screens.Current;
                if (screen == null)
                {
                    return DevJson.Error("no screen of ours is focused");
                }

                string source;
                List<AgeTransform> roots = Roots(screen, wholeTree, out source);

                // Narrowing to the screen's own stops is right only when the walk is the screen's OWN
                // window: over the live tree the heads-up display is drawn too, and its nodes -
                // contributed into every page rather than declared by this one - are exactly what
                // covers it.
                TooltipAudit.Result tooltips = TooltipAudit.Check(screen, roots, source == "screen");

                Result result = new Result();
                result.Screen = screen.Key;
                result.ScreenName = tooltips.ScreenName;
                result.Root = tooltips.Root;
                result.RootSource = source;
                result.Roots = roots.Count;
                CheckActions(roots, tooltips.DeclaredNodes, result);
                return Write(result, tooltips);
            }
            catch (Exception e)
            {
                return DevJson.Error(e.Message);
            }
        }

        /// <summary>Where to walk: the screen's own window when it names one, else everything the
        /// engine is drawing. <paramref name="wholeTree"/> forces the second even on a screen that
        /// names a root - what a modal drawn over a live page needs, since the page behind it is
        /// still painted and still holds controls.</summary>
        private static List<AgeTransform> Roots(Screen screen, bool wholeTree, out string source)
        {
            AgeTransform own = null;
            if (!wholeTree)
            {
                try
                {
                    own = screen.RootTransform;
                }
                catch (Exception) { }
            }

            if (own != null)
            {
                source = "screen";
                List<AgeTransform> one = new List<AgeTransform>();
                one.Add(own);
                return one;
            }

            source = "live";
            return AgeDump.LiveRoots();
        }

        // ---- the actions half ----

        private static void CheckActions(
            List<AgeTransform> roots,
            List<Declared> declared,
            Result result
        )
        {
            int[] budget = new int[1];
            for (int i = 0; i < roots.Count; i++)
            {
                Walk(roots[i], declared, result, 0, budget);
            }

            result.Walked = budget[0];
            result.BudgetHit = budget[0] > MaxWidgets;
        }

        private static void Walk(
            AgeTransform widget,
            List<Declared> declared,
            Result result,
            int depth,
            int[] budget
        )
        {
            if (widget == null || depth > MaxDepth || budget[0]++ > MaxWidgets)
            {
                return;
            }

            AgeControl control = AgeWidgets.Control(widget);
            if (control != null)
            {
                Interactable(widget, control, declared, result);
            }
            else if (Handles(widget))
            {
                Finding finding = Made(widget, null, "a handler client with no control of its own");
                Add(result.HandlerGroups, finding);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child != null)
                {
                    Walk(child, declared, result, depth + 1, budget);
                }
            }
        }

        private static void Interactable(
            AgeTransform widget,
            AgeControl control,
            List<Declared> declared,
            Result result
        )
        {
            string kind = control.GetType().Name;
            if (!Worked(kind))
            {
                return;
            }

            result.PaintedControls++;

            // A prefab hangs a button on decoration to eat clicks; one with no method to send is not
            // an affordance and a node for it would be a dead end. Value controls do their job
            // whether or not the prefab asked to be told about it, so only the two click kinds are
            // judged this way.
            if ((kind == "AgeControlButton" || kind == "AgeControlToggle") && !Wired(control))
            {
                result.Inert++;
                return;
            }

            // Aim, not only containment: a node read off something that is not a widget at all - a
            // notification the game models, a place on the map - has no widget to be found under, and
            // its POINTER is then the only thing that says where it stands. Asked of the node's own
            // declaration, the same authority the tooltip check uses, never re-derived.
            if (DeliberatelyUnworked(widget))
            {
                result.Inert++;
                return;
            }

            List<Declared> covering = AuditModel.Covering(
                declared,
                widget,
                AgeWidgets.Raw(widget) ?? Inside(widget)
            );
            if (covering.Count == 0)
            {
                // Neither containment nor aim found a node - but a screen that reads a control off
                // the MODEL behind it (a notification the game owns, a quest pinned to the map) has
                // no widget and no pointer to be found by, and saying "no node stands here" about one
                // of those is a false alarm that costs a reader more than the finding is worth. So the
                // weakest evidence is asked last and REPORTED rather than acted on: a node whose own
                // spoken lines hold this control's drawn words is named, and the judgement is left to
                // the person reading.
                Finding orphan = Made(widget, kind, "no node stands here");
                orphan.Handler = Wiring(control);
                orphan.Covered = Says(declared, orphan.Text);
                if (orphan.Covered != null)
                {
                    orphan.Why = "no node stands here, though one says its words";
                }

                Add(result.Uncovered, orphan);
                return;
            }

            for (int i = 0; i < covering.Count; i++)
            {
                if (Acts(covering[i]))
                {
                    result.CoveredControls++;
                    return;
                }
            }

            Finding finding = Made(widget, kind, "the node here declares no action");
            finding.Handler = Wiring(control);
            finding.Covered = covering[0].Key;
            Add(result.Uncovered, finding);
        }

        /// <summary>
        /// A control the mod has DECIDED not to give a node, which is not the same thing as one it
        /// missed - the difference this audit exists to keep, and the reason a ruling is written down
        /// here rather than re-argued at every run.
        ///
        /// One entry so far. The star-system page's three bottom panels each draw a
        /// <c>PanelExpandButton</c> running <c>StarSystemScreen.OnExpandCb</c>, which resizes all three
        /// frames and remembers the size. Measured 2026-08-29: the panels' lists SCROLL rather than
        /// losing rows, so the accessible tree is byte-identical expanded and collapsed - the button
        /// moves pixels and nothing else. A control whose whole effect is how much a sighted player
        /// sees at once is not an affordance a keyboard player has been denied, so it is omitted
        /// (owner ruling 2026-08-29), and it is counted <c>inert</c> like the other controls that are
        /// not affordances rather than reported as uncovered.
        ///
        /// The bar for adding to this list is that omitting the control costs the player NOTHING they
        /// could otherwise perceive - never that declaring it would be awkward.
        /// </summary>
        private static bool DeliberatelyUnworked(AgeTransform widget)
        {
            try
            {
                return widget != null
                    && widget.name == "PanelExpandButton"
                    && widget.GetComponentInParent<StarSystemScreen>() != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The tooltip a control keeps INSIDE itself, for a control that carries none of its own.
        ///
        /// A node declared through <c>CardActions.Emit</c> is keyed structurally, so it has no widget
        /// for the containment walk to find, and its AIM is the only thing that says where it stands.
        /// Where the control's own tooltip is null that aim cannot be matched either - a planet card's
        /// anomaly row is the measured case: the click is wired on the row and the tooltip hangs on the
        /// icon inside it, so a declared, walkable node read as "no node stands here". Asking the
        /// resolver for what is inside the control answers the same tooltip the node aimed at.
        ///
        /// Only for a control with NO tooltip of its own, so a control that has one is still judged by
        /// that one alone and a node aiming at some other tooltip inside it cannot claim it.
        /// </summary>
        private static AgeTooltip Inside(AgeTransform widget)
        {
            try
            {
                List<AgeTooltip> found = new List<AgeTooltip>(2);
                AgeWidgets.EffectiveTooltips(
                    widget,
                    found,
                    ES2Access.Core.UI.TooltipReach.Own | ES2Access.Core.UI.TooltipReach.Descendants,
                    3
                );
                return found.Count == 0 ? null : found[found.Count - 1];
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The first declared node whose arrival line or buffer already holds these drawn
        /// words, or null. Evidence that the player HEARS the thing, never that they can work it.
        /// </summary>
        private static string Says(List<Declared> declared, string text)
        {
            // A word or two proves nothing - "1" is in half the position lines on any page - so the
            // evidence is only offered for text long enough that finding it somewhere else means
            // something.
            if (text == null || text.Length < 8)
            {
                return null;
            }

            for (int i = 0; i < declared.Count; i++)
            {
                List<string> reduced = new List<string>();
                for (int j = 0; j < declared[i].Spoken.Count; j++)
                {
                    reduced.Add(AuditModel.Reduce(declared[i].Spoken[j]));
                }

                if (AuditModel.Contains(reduced, text))
                {
                    return declared[i].Key;
                }
            }

            return null;
        }

        /// <summary>The control kinds a player WORKS. The rest - hover areas, drag and zoom areas,
        /// mouse capture shields, the scroll bar a scroll view owns - are the mouse's own machinery
        /// and have no keyboard affordance to lose.</summary>
        private static bool Worked(string kind)
        {
            switch (kind)
            {
                case "AgeControlButton":
                case "AgeControlButtonRadial":
                case "AgeControlToggle":
                case "AgeControlToggleRadial":
                case "AgeControlDropList":
                case "AgeControlSlider":
                case "AgeControlSliderRadial":
                case "AgeControlTextField":
                case "AgeControlTextFieldChat":
                case "AgeControlTextArea":
                case "AgeControlKeyBindingField":
                case "AgeControlScrollView":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Whether this control has anywhere to send a click. AGE wires a control to its
        /// client by NAME - a public <c>On…Method</c> string beside a <c>GameObject</c> to send it to
        /// (<c>AgeControlButton.OnActivateMethod</c>) - so one non-empty method field anywhere on the
        /// control is the whole test, and it works for every kind without listing them.</summary>
        private static bool Wired(AgeControl control)
        {
            FieldInfo[] fields = Methods(control.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                try
                {
                    string value = fields[i].GetValue(control) as string;
                    if (!string.IsNullOrEmpty(value))
                    {
                        return true;
                    }
                }
                catch (Exception) { }
            }

            return false;
        }

        /// <summary>What this control would SEND, as "field=method" - the game's own name for the
        /// job the mouse does here. It is what makes a finding judgeable without opening the
        /// decompile: a button wired to <c>OnActivateMethod</c> is an affordance, and one wired to
        /// nothing but <c>OnMouseEnterMethod</c> is a hover effect.</summary>
        private static string Wiring(AgeControl control)
        {
            StringBuilder wiring = new StringBuilder();
            FieldInfo[] fields = Methods(control.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                try
                {
                    string value = fields[i].GetValue(control) as string;
                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    if (wiring.Length > 0)
                    {
                        wiring.Append(' ');
                    }

                    wiring.Append(fields[i].Name).Append('=').Append(value);
                }
                catch (Exception) { }
            }

            return wiring.Length == 0 ? null : wiring.ToString();
        }

        private static readonly Dictionary<Type, FieldInfo[]> MethodFields =
            new Dictionary<Type, FieldInfo[]>();

        private static FieldInfo[] Methods(Type type)
        {
            FieldInfo[] cached;
            if (MethodFields.TryGetValue(type, out cached))
            {
                return cached;
            }

            List<FieldInfo> found = new List<FieldInfo>();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                if (
                    fields[i].FieldType == typeof(string)
                    && fields[i].Name.StartsWith("On")
                    && fields[i].Name.EndsWith("Method")
                )
                {
                    found.Add(fields[i]);
                }
            }

            cached = found.ToArray();
            MethodFields[type] = cached;
            return cached;
        }

        /// <summary>Whether this node declares anything that would WORK a control - any handler the
        /// navigator can run on it. Reading is not working: a node that only says what a button is
        /// leaves the player unable to press it, and nothing in the speech says so.</summary>
        private static bool Acts(Declared node)
        {
            NodeVtable vtable = node == null || node.Node == null ? null : node.Node.Vtable;
            if (vtable == null)
            {
                return false;
            }

            return vtable.OnActivate != null
                || vtable.OnSecondary != null
                || vtable.OnDoubleClick != null
                || vtable.OnAdjust != null
                || vtable.OnSelectToggle != null
                || vtable.OnSelectRange != null
                || vtable.OnFollow != null
                || vtable.OnAlternate != null
                || vtable.OnContextual != null
                || vtable.OnGoTo != null
                || vtable.OnExpand != null
                || vtable.OnCollapse != null
                || vtable.OnPickUp != null
                || vtable.OnDrop != null;
        }

        /// <summary>
        /// Whether this widget is a handler CLIENT in its own right - a component of the game's own
        /// (not an AGE one) that declares AGE's callback naming convention, a <c>void …Cb(…)</c>
        /// method of the kind an <c>On…Method</c> string names.
        ///
        /// It is a weak signal and its own bucket for that reason. The engine hands clicks to
        /// <see cref="AgeControl"/>s and to nothing else, so a group with no control on it cannot be
        /// clicked however its class is written; what this catches is the shape the hand audit
        /// reported as one (an <c>Enable</c>-gated group with a refusal tooltip), so that a judgement
        /// about it is made from a measurement rather than from a field's declared type.
        /// </summary>
        private static bool Handles(AgeTransform widget)
        {
            try
            {
                Component[] components = widget.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null || component is AgeComponent)
                    {
                        continue;
                    }

                    if (Callbacks(component.GetType()))
                    {
                        return true;
                    }
                }
            }
            catch (Exception) { }

            return false;
        }

        private static readonly Dictionary<Type, bool> HasCallbacks = new Dictionary<Type, bool>();

        private static bool Callbacks(Type type)
        {
            bool cached;
            if (HasCallbacks.TryGetValue(type, out cached))
            {
                return cached;
            }

            cached = false;
            try
            {
                MethodInfo[] methods = type.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                for (int i = 0; i < methods.Length && !cached; i++)
                {
                    cached =
                        methods[i].Name.EndsWith("Cb") && methods[i].GetParameters().Length <= 1;
                }
            }
            catch (Exception) { }

            HasCallbacks[type] = cached;
            return cached;
        }

        // ---- the answer ----

        private static void Add(List<Finding> into, Finding finding)
        {
            into.Add(finding);
        }

        private static Finding Made(AgeTransform widget, string component, string why)
        {
            Finding finding = new Finding();
            finding.Path = DrawnBy.Path(widget);
            finding.Component = component;
            finding.Why = why;
            try
            {
                finding.Text = AgeWidgets.PaintedText(widget, 3);
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                if (tooltip != null)
                {
                    string cls = tooltip.Class;
                    finding.Tooltip = string.IsNullOrEmpty(cls)
                        ? AuditModel.Excerpt(AgeText.Tooltip(tooltip))
                        : cls;
                }

                finding.Enabled = AgeWidgets.Enabled(widget);
                finding.Offered = AgeWidgets.Offered(widget);
                finding.Rect = AgeWidgets.Clipped(widget).GetGlobalPosition();
            }
            catch (Exception) { }

            return finding;
        }

        private static string Write(Result result, TooltipAudit.Result tooltips)
        {
            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("screen");
                json.WriteValue(result.Screen);
                json.WritePropertyName("title");
                json.WriteValue(result.ScreenName);
                json.WritePropertyName("rootSource");
                json.WriteValue(result.RootSource);
                json.WritePropertyName("roots");
                json.WriteValue(result.Roots);
                json.WritePropertyName("root");
                json.WriteValue(result.Root);

                json.WritePropertyName("counts");
                json.WriteStartObject();
                Count(json, "nodes", tooltips.Nodes);
                Count(json, "widgetsWalked", result.Walked);
                Count(json, "paintedTooltips", tooltips.PaintedTooltips);
                Count(json, "hiddenTooltips", tooltips.HiddenTooltips);
                Count(json, "tooltipsUncovered", tooltips.Uncovered.Count);
                Count(json, "tooltipsUnread", tooltips.Unread.Count);
                Count(json, "tooltipsDecoration", tooltips.Decoration.Count);
                Count(json, "tooltipsPromised", tooltips.Promised.Count);
                Count(json, "tooltipsMisaimed", tooltips.Misaimed.Count);
                Count(json, "tooltipsHidden", tooltips.Hidden.Count);
                Count(json, "tooltipsUndescribed", tooltips.Undescribed.Count);
                Count(json, "paintedControls", result.PaintedControls);
                Count(json, "controlsInert", result.Inert);
                Count(json, "controlsCovered", result.CoveredControls);
                Count(json, "actionsUncovered", result.Uncovered.Count);
                Count(json, "handlerGroups", result.HandlerGroups.Count);
                json.WritePropertyName("budgetHit");
                json.WriteValue(result.BudgetHit);
                json.WriteEndObject();

                // Every bucket the tooltip check fills, through its own writer: this file used to list
                // seven of the eleven by hand and reported a screen clean that the tooltip check
                // called dirty.
                tooltips.WriteBuckets(json, MaxListed);
                Findings(json, "actionsUncovered", result.Uncovered);
                Findings(json, "handlerGroups", result.HandlerGroups);
                json.WriteEndObject();
            });
        }

        private static void Count(JsonTextWriter json, string name, int value)
        {
            json.WritePropertyName(name);
            json.WriteValue(value);
        }

        private static void Findings(JsonTextWriter json, string name, List<Finding> findings)
        {
            json.WritePropertyName(name);
            json.WriteStartArray();
            for (int i = 0; i < findings.Count && i < MaxListed; i++)
            {
                Finding finding = findings[i];
                json.WriteStartObject();
                json.WritePropertyName("path");
                json.WriteValue(finding.Path);
                json.WritePropertyName("why");
                json.WriteValue(finding.Why);
                if (!string.IsNullOrEmpty(finding.Handler))
                {
                    json.WritePropertyName("handler");
                    json.WriteValue(finding.Handler);
                }

                if (!string.IsNullOrEmpty(finding.Component))
                {
                    json.WritePropertyName("component");
                    json.WriteValue(finding.Component);
                }

                if (!string.IsNullOrEmpty(finding.Text))
                {
                    json.WritePropertyName("text");
                    json.WriteValue(AuditModel.Excerpt(finding.Text));
                }

                if (!string.IsNullOrEmpty(finding.Tooltip))
                {
                    json.WritePropertyName("tooltip");
                    json.WriteValue(finding.Tooltip);
                }

                if (!string.IsNullOrEmpty(finding.Covered))
                {
                    json.WritePropertyName("node");
                    json.WriteValue(finding.Covered);
                }

                json.WritePropertyName("enabled");
                json.WriteValue(finding.Enabled);
                json.WritePropertyName("offered");
                json.WriteValue(finding.Offered);
                WriteRect(json, finding.Rect, true);
                json.WriteEndObject();
            }

            More(json, findings.Count);
            json.WriteEndArray();
        }

        private static void More(JsonTextWriter json, int total)
        {
            if (total <= MaxListed)
            {
                return;
            }

            json.WriteStartObject();
            json.WritePropertyName("more");
            json.WriteValue(total - MaxListed);
            json.WriteEndObject();
        }

        private static void WriteRect(JsonTextWriter json, Rect rect, bool has)
        {
            if (!has)
            {
                return;
            }

            json.WritePropertyName("rect");
            json.WriteStartArray();
            json.WriteValue(Math.Round(rect.xMin));
            json.WriteValue(Math.Round(rect.yMin));
            json.WriteValue(Math.Round(rect.width));
            json.WriteValue(Math.Round(rect.height));
            json.WriteEndArray();
        }
    }
}

using System;
using System.Collections.Generic;
using Amplitude;
using Amplitude.Unity.Framework;
using Amplitude.Unity.Gui;
using ES2Access.Core.UI.Graph;
using ES2Access.Loader.Dev;
using ES2Access.Screens;
using Screen = ES2Access.Screens.Screen;
using ES2Access.UI;
using Newtonsoft.Json;
using UnityEngine;
using Breach = ES2Access.Dev.NotificationAudit.Breach;
using Declared = ES2Access.Dev.NotificationAudit.Declared;

namespace ES2Access.Dev
{
    /// <summary>
    /// The tooltip half of the notification family's self-check, asked of ANY screen: what the game
    /// would draw on hover against what the mod declares and points at.
    ///
    /// It exists because the notification audit found the same defect four times on four prefabs and
    /// no other screen had anything like it. Every tooltip defect this mod has shipped was one of
    /// three shapes, all of them invisible in a transcript, in a tree dump and in speech:
    ///
    /// <list type="number">
    /// <item><b>Promised and empty</b> - a node undertakes to have a dossier in its review buffer and
    /// nothing near it would draw one, so the player walks into an empty buffer.</item>
    /// <item><b>Mis-aimed</b> - the node declares a dossier and sends the pointer at a DIFFERENT
    /// tooltip, one the game draws nothing for; the words exist and are unreachable.</item>
    /// <item><b>Uncovered</b> - the game would draw a tooltip somewhere no node stands, or the node
    /// standing there says nothing the tooltip says.</item>
    /// </list>
    ///
    /// The two sides are re-derived from different evidence - the painted side is a walk of the
    /// screen's own widget tree and knows nothing of the mod's code - which is what makes agreement
    /// worth anything. Except for the AIM: that is asked of the node's own declaration
    /// (<see cref="NodeVtable.PointsAt"/>) and never re-derived, because the deepest tooltip inside a
    /// card is routinely decoration and a second opinion reports defects on screens that are right.
    ///
    /// Three widenings over the notification version, each of which was a blind spot there:
    /// tooltips on NON-control widgets are reported in their own lower-severity bucket rather than
    /// skipped (that is exactly the caption-on-an-icon class this mod now reads); a second pass runs
    /// with the transparency gate OFF and reports what it finds as <c>hidden</c> (a subtree the game
    /// reveals on hover carries real tooltips the painted walk cannot see); and a tooltip naming a
    /// class the game has no <c>GuiTooltipDescription</c> for is reported separately as what it is -
    /// a defect in the GAME, which will draw nothing there whatever the mod does.
    ///
    /// Main-thread only, dev-only, and never on the player's path: nothing here speaks, focuses,
    /// moves the pointer or changes what the game is showing.
    /// </summary>
    internal static class TooltipAudit
    {
        /// <summary>How deep under the screen's root the painted walk looks, and how many widgets it
        /// will visit before answering anyway. A whole screen is a bigger tree than a popup.</summary>
        private const int MaxDepth = 24;

        private const int MaxWidgets = 12000;

        private sealed class Result
        {
            public string Screen;
            public string ScreenName;
            public string Root;
            public string Prefix;
            public int Nodes;
            public int PaintedTooltips;
            public int HiddenTooltips;

            /// <summary>Declares a dossier to review with nothing near it that would draw.</summary>
            public readonly List<Breach> Promised = new List<Breach>();

            /// <summary>Declares a dossier and points at a tooltip that draws nothing.</summary>
            public readonly List<Breach> Misaimed = new List<Breach>();

            /// <summary>The game would draw a tooltip on a CONTROL here and no node covers it.
            /// </summary>
            public readonly List<Breach> Uncovered = new List<Breach>();

            /// <summary>The same on a widget the player cannot work - an icon captioning a value, a
            /// block around a row. Its own bucket because it is a weaker claim: some of these are
            /// decoration the game hangs a sentence on and nobody needs.</summary>
            public readonly List<Breach> Decoration = new List<Breach>();

            /// <summary>The tooltip's own words are not in anything the node covering it carries.
            /// </summary>
            public readonly List<Breach> Unread = new List<Breach>();

            /// <summary>Found only with the transparency gate off: a carrier inside a subtree the
            /// game reveals on hover. Reported apart because the painted side cannot see it and
            /// neither can a screenshot.</summary>
            public readonly List<Breach> Hidden = new List<Breach>();

            /// <summary>A tooltip naming a class the game has no description for. NOT a mod finding -
            /// the game draws nothing for it however it is pointed at.</summary>
            public readonly List<Breach> Undescribed = new List<Breach>();

            /// <summary>The check cannot answer for this node: what it is read off is not an AGE
            /// widget at all - a place on the map, a thing the renderer draws - so there is no widget
            /// tree to ask "would anything here draw". It is not a defect and must not be counted as
            /// one: every galaxy node landed in <see cref="Promised"/> for exactly this reason, and
            /// the map's dossiers demonstrably draw. A node like this that DOES declare an aim is
            /// still judged on the aim, and only one declaring neither ends up here.</summary>
            public readonly List<Breach> Unknown = new List<Breach>();

            public int Findings
            {
                get { return Promised.Count + Misaimed.Count + Uncovered.Count + Unread.Count; }
            }
        }

        private sealed class Tip
        {
            public AgeTooltip Tooltip;
            public AgeTransform Owner;
            public bool Interactive;
        }

        /// <summary>The check, run against whichever of our screens is focused, as JSON.</summary>
        public static string Json()
        {
            try
            {
                Screen screen = TheScreen();
                if (screen == null)
                {
                    return DevJson.Error("no screen of ours is focused");
                }

                return Write(Check(screen));
            }
            catch (Exception e)
            {
                return DevJson.Error(e.Message);
            }
        }

        private static Screen TheScreen()
        {
            ScreenManager screens = ModEntry.Screens;
            return screens == null ? null : screens.Current;
        }

        private static Result Check(Screen screen)
        {
            Result result = new Result();
            result.Screen = screen.Key;
            result.ScreenName = Named(screen);
            result.Prefix = Prefix(screen);

            List<Breach> unlocatable = new List<Breach>();
            List<Declared> declared = NotificationAudit.DeclaredNodes(
                screen,
                result.Prefix,
                unlocatable
            );
            result.Nodes = declared.Count;

            CheckDeclarations(declared, result);

            AgeTransform root = Root(screen);
            result.Root = root == null ? null : root.name;
            if (root == null)
            {
                // Half a check is worth having and worth SAYING: without a root there is no painted
                // side, so the coverage answers are absent rather than clean.
                return result;
            }

            List<Tip> painted = new List<Tip>();
            Walk(root, painted, true, 0, new int[1]);
            result.PaintedTooltips = painted.Count;
            CheckCoverage(painted, declared, result, false);

            List<Tip> all = new List<Tip>();
            Walk(root, all, false, 0, new int[1]);
            List<Tip> hidden = Missing(all, painted);
            result.HiddenTooltips = hidden.Count;
            CheckCoverage(hidden, declared, result, true);
            return result;
        }

        // ---- the declaration side ----

        private static void CheckDeclarations(List<Declared> declared, Result result)
        {
            for (int i = 0; i < declared.Count; i++)
            {
                Declared node = declared[i];
                if (!NotificationAudit.Promises(node))
                {
                    continue;
                }

                AgeTooltip aimed = NotificationAudit.AimOf(node);
                if (node.Widget == null)
                {
                    // Nothing to walk: this node is read off a place on the map rather than off a
                    // widget. Its own declared AIM is the only thing that can be judged, and where it
                    // has none the honest answer is "cannot tell" - not "promises nothing".
                    if (aimed == null)
                    {
                        result.Unknown.Add(
                            NotificationAudit.Made(
                                null,
                                node.Key,
                                "declares a tooltip on something that is not a widget - cannot tell",
                                null
                            )
                        );
                        continue;
                    }
                }
                else if (!NotificationAudit.AnyDrawing(node.Widget))
                {
                    result.Promised.Add(
                        NotificationAudit.Made(
                            node.Widget,
                            node.Key,
                            "declares a tooltip to review with nothing that draws",
                            null
                        )
                    );
                    continue;
                }

                if (aimed != null && !AgeWidgets.Draws(aimed))
                {
                    result.Misaimed.Add(
                        NotificationAudit.Made(
                            node.Widget,
                            node.Key,
                            "declares a tooltip to review and points at one that draws nothing",
                            Describes(aimed) ? null : "class '" + Class(aimed) + "' has no description"
                        )
                    );
                }
            }
        }

        // ---- the painted side ----

        private static void CheckCoverage(
            List<Tip> painted,
            List<Declared> declared,
            Result result,
            bool hidden
        )
        {
            for (int i = 0; i < painted.Count; i++)
            {
                Tip tip = painted[i];
                if (!Describes(tip.Tooltip))
                {
                    result.Undescribed.Add(
                        NotificationAudit.Made(
                            tip.Owner,
                            NotificationAudit.Name(tip.Owner),
                            "the game has no description for tooltip class '"
                                + Class(tip.Tooltip)
                                + "'",
                            NotificationAudit.Excerpt(AgeText.Tooltip(tip.Tooltip))
                        )
                    );
                    continue;
                }

                List<Declared> covering = NotificationAudit.Covering(
                    declared,
                    tip.Owner,
                    tip.Tooltip
                );
                if (covering.Count == 0)
                {
                    Breach breach = NotificationAudit.Made(
                        tip.Owner,
                        NotificationAudit.Name(tip.Owner),
                        "the game would draw a tooltip here and no node covers it",
                        NotificationAudit.Excerpt(AgeText.Tooltip(tip.Tooltip))
                    );
                    (
                        hidden ? result.Hidden
                        : tip.Interactive ? result.Uncovered
                        : result.Decoration
                    ).Add(breach);
                    continue;
                }

                if (hidden)
                {
                    // A hidden carrier that IS covered says nothing: whether its words are carried
                    // depends on the game having revealed it, which it has not.
                    continue;
                }

                // Words to look for only where the words are ON the tooltip. A renderer-assembled one
                // keeps a data key in the same field, and that key is what the renderer looks its
                // dossier up by - never anything the game draws or the mod says. Asked through the
                // same helper the reader picks its mode with.
                string content = AgeText.Tooltip(AgeWidgets.Readable(tip.Tooltip));
                if (string.IsNullOrEmpty(content))
                {
                    continue;
                }

                if (!NotificationAudit.CarriedBy(covering, content))
                {
                    result.Unread.Add(
                        NotificationAudit.Made(
                            tip.Owner,
                            NotificationAudit.Name(tip.Owner) + " (" + covering[0].Key + ")",
                            "the tooltip's words are not in what that node carries",
                            NotificationAudit.Excerpt(content)
                        )
                    );
                }
            }
        }

        /// <summary>
        /// Every tooltip the screen would draw, from its own tree.
        ///
        /// <paramref name="drawnOnly"/> is the transparency gate the engine's own draw makes - a
        /// pooled row retired by alpha keeps its words and its rectangle, so a walk that ignored it
        /// would report the dead rows of every table. Turning it OFF is the second pass: the
        /// difference between the two is the set of carriers inside subtrees the game reveals on
        /// hover, which no screenshot and no ordinary walk can see.
        /// </summary>
        private static void Walk(
            AgeTransform widget,
            List<Tip> into,
            bool drawnOnly,
            int depth,
            int[] budget
        )
        {
            if (widget == null || depth > MaxDepth || budget[0]++ > MaxWidgets)
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            if (AgeWidgets.Draws(tooltip))
            {
                Tip tip = new Tip();
                tip.Tooltip = tooltip;
                tip.Owner = widget;
                tip.Interactive = AgeWidgets.Control(widget) != null;
                into.Add(tip);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child == null || !child.Visible)
                {
                    continue;
                }

                if (drawnOnly && !AgeWidgets.Paints(widget, child))
                {
                    continue;
                }

                Walk(child, into, drawnOnly, depth + 1, budget);
            }
        }

        private static List<Tip> Missing(List<Tip> all, List<Tip> painted)
        {
            List<Tip> missing = new List<Tip>();
            for (int i = 0; i < all.Count; i++)
            {
                bool seen = false;
                for (int j = 0; j < painted.Count && !seen; j++)
                {
                    seen = ReferenceEquals(all[i].Owner, painted[j].Owner);
                }

                if (!seen)
                {
                    missing.Add(all[i]);
                }
            }

            return missing;
        }

        // ---- the game's own side ----

        private static string Class(AgeTooltip tooltip)
        {
            try
            {
                string cls = tooltip == null ? null : tooltip.Class;
                return string.IsNullOrEmpty(cls) ? "Simple" : cls;
            }
            catch (Exception)
            {
                return "Simple";
            }
        }

        /// <summary>Whether the GAME has anything to draw this tooltip's class with
        /// (<c>GuiTooltipController.ReadTooltipInformation</c> looks the class up in the
        /// <c>GuiTooltipDescription</c> database and gives up when it is not there). A tooltip that
        /// fails this is a defect in the game's own data - one prefab writes a SENTENCE into the class
        /// field - and no amount of pointing at it will make it draw, so it is reported as its own
        /// thing and never as a mod finding.</summary>
        private static bool Describes(AgeTooltip tooltip)
        {
            try
            {
                IDatabase<GuiTooltipDescription> database =
                    Databases.GetDatabase<GuiTooltipDescription>();
                return database == null
                    || database.GetValue(new StaticString(Class(tooltip))) != null;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static AgeTransform Root(Screen screen)
        {
            try
            {
                return screen.RootTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Prefix(Screen screen)
        {
            try
            {
                return screen.NodePrefix;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Named(Screen screen)
        {
            try
            {
                return screen.ScreenName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Write(Result result)
        {
            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("screen");
                json.WriteValue(result.Screen);
                json.WritePropertyName("title");
                json.WriteValue(result.ScreenName);
                json.WritePropertyName("root");
                json.WriteValue(result.Root);
                json.WritePropertyName("prefix");
                json.WriteValue(result.Prefix);
                json.WritePropertyName("clean");
                json.WriteValue(result.Findings == 0);
                json.WritePropertyName("nodes");
                json.WriteValue(result.Nodes);
                json.WritePropertyName("paintedTooltips");
                json.WriteValue(result.PaintedTooltips);
                json.WritePropertyName("hiddenTooltips");
                json.WriteValue(result.HiddenTooltips);
                NotificationAudit.WriteBreaches(json, "promised", result.Promised);
                NotificationAudit.WriteBreaches(json, "misaimed", result.Misaimed);
                NotificationAudit.WriteBreaches(json, "uncovered", result.Uncovered);
                NotificationAudit.WriteBreaches(json, "decoration", result.Decoration);
                NotificationAudit.WriteBreaches(json, "unread", result.Unread);
                NotificationAudit.WriteBreaches(json, "hidden", result.Hidden);
                NotificationAudit.WriteBreaches(json, "undescribed", result.Undescribed);
                NotificationAudit.WriteBreaches(json, "unknown", result.Unknown);
                json.WriteEndObject();
            });
        }
    }
}

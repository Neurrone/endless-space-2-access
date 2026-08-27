using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Loader.Dev;
using ES2Access.Screens;
using ES2Access.UI;
using Newtonsoft.Json;
using UnityEngine;
using Breach = ES2Access.Dev.NotificationAudit.Breach;
using Declared = ES2Access.Dev.NotificationAudit.Declared;
using Screen = ES2Access.Screens.Screen;

namespace ES2Access.Dev
{
    /// <summary>
    /// The nodes the mod is offering that the game is NOT drawing - a stop the player can walk onto
    /// and stand on while the pixels under it are blank.
    ///
    /// It is the mechanical replacement for finding this bug class by screenshot. Every other check
    /// in the family starts from what the engine PAINTS and asks whether a node covers it, so a node
    /// standing on nothing is invisible to all of them: <see cref="CoverageAudit"/> never walks the
    /// widget (it does not paint), <see cref="TooltipAudit"/>'s painted half never sees it, and a
    /// tree dump prunes it because a faded label draws no text - the dump then AGREES with whatever
    /// the mod declared. This asks the question from the other end, off the declared side alone.
    ///
    /// What makes it worth a check of its own is the pooled table (see
    /// <see cref="AgeWidgets.Painted"/>): a table the game fills with <c>ReserveChildren</c> +
    /// <c>RefreshChildrenIList</c> never shrinks, and retires a surplus row by fading it to nothing
    /// while leaving it Visible and still holding the PREVIOUS binding's words. A walk gated on
    /// visibility declares those rows, and they announce a stale value with no name - measured on the
    /// star system's population strip as two buttons saying "2" and "1" from a system the player had
    /// left.
    ///
    /// <para><b>Two kinds of unpainted, and only one of them is a defect.</b> The declared side is
    /// built UNGATED (<see cref="UI.GraphNavigator.InspectRender(Screen)"/>) precisely so the check
    /// can still see what the gate takes away - which means a clean gate produces findings here by
    /// design. So each unpainted node is asked whether it survived into the render the PLAYER gets
    /// (built again through <see cref="NodeGate"/>): the ones that did not are
    /// <c>droppedByGate</c>, the gate doing its job, informational; the ones that did are
    /// <c>shippedUnpainted</c>, nodes the player can walk onto with blank pixels under them, and
    /// those are the findings. A run where every unpainted node is in the first bucket is a clean
    /// screen, not a screen with forty ghosts.</para>
    ///
    /// <para>One asymmetry to read the counts by: the gate has a second half that runs INSIDE the
    /// screen's own build (<see cref="NodeGate.StillDrawn"/>, the cell-banding path), and that half
    /// is not bypassed by an ungated builder - so cells it takes out are missing from BOTH renders
    /// and appear in neither bucket. They are counted where they always were: nowhere, because they
    /// were never declared.</para>
    ///
    /// Nothing here speaks, focuses, moves the pointer or changes what the game is showing.
    /// </summary>
    internal static class GhostAudit
    {
        /// <summary>How many findings of EACH bucket are written out; the rest are counted as
        /// <c>more</c>.</summary>
        private const int MaxListed = 10;

        /// <summary>One node standing on something the game is not drawing.</summary>
        private sealed class Finding
        {
            public string Key;
            public string Region;
            public string Path;
            public string Why;
            public string Says;
            public bool Own;
            public Rect Rect;
        }

        private sealed class Result
        {
            public string Screen;
            public string ScreenName;
            public string Prefix;

            public int Nodes;
            public int Located;
            public int Synthetic;
            public int Unlocatable;
            public int Own;
            public int Elsewhere;

            /// <summary>Node keys the SHIPPED render holds - what the gate let through. Null when the
            /// gated render could not be built, which is what <see cref="Shipped"/> reports.</summary>
            public HashSet<string> Shipped;

            /// <summary>Unpainted and withdrawn by the gate: the gate working, not a finding.</summary>
            public readonly List<Finding> Dropped = new List<Finding>();

            /// <summary>Unpainted and in the render the player gets: the defects.</summary>
            public readonly List<Finding> Unpainted = new List<Finding>();
        }

        /// <summary>The keys of the render the PLAYER gets - the same screen built through the gate.
        /// Null when there is no navigator or the build failed, which leaves every unpainted node
        /// unclassified rather than silently called a defect.</summary>
        private static HashSet<string> Shipped()
        {
            try
            {
                GraphNavigator navigator = ModEntry.Navigator;
                GraphRender render = navigator == null ? null : navigator.InspectRender();
                if (render == null)
                {
                    return null;
                }

                HashSet<string> keys = new HashSet<string>();
                for (int i = 0; i < render.Order.Count; i++)
                {
                    keys.Add(Convert.ToString(render.Order[i].Id.StructuralKey));
                }

                return keys;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The check, on whichever of our screens is focused, as JSON.</summary>
        public static string Json()
        {
            try
            {
                ScreenManager screens = ModEntry.Screens;
                Screen screen = screens == null ? null : screens.Current;
                if (screen == null)
                {
                    return DevJson.Error("no screen of ours is focused");
                }

                Result result = new Result();
                result.Screen = screen.Key;
                result.ScreenName = Named(screen);
                result.Prefix = Prefix(screen);

                List<Breach> unlocatable = new List<Breach>();

                // Everything the focused screen OFFERS, not only what it declared itself: the
                // heads-up display contributes stops to every screen and a ghost up there is just as
                // unreachable. Each finding carries whether it is the screen's own, so a run taken on
                // a screen drawn over another can be read by subtracting the ones that are not.
                List<Declared> declared = NotificationAudit.DeclaredNodes(screen, null, unlocatable);
                result.Nodes = declared.Count;
                result.Unlocatable = unlocatable.Count;
                result.Shipped = Shipped();

                for (int i = 0; i < declared.Count; i++)
                {
                    Check(declared[i], result);
                }

                return Write(result);
            }
            catch (Exception e)
            {
                return DevJson.Error(e.Message);
            }
        }

        private static void Check(Declared node, Result result)
        {
            // A node read off the MODEL rather than off a widget - a place on the map, a notification
            // the game owns, a dossier keyed structurally - has nothing to ask this question of, and
            // guessing about one would drown the findings that are real. The aim is asked as well as
            // the widget (<see cref="NotificationAudit.Evidence"/>), so a node located only by what its
            // pointer points at is still checked.
            AgeTransform widget = NotificationAudit.Evidence(node);
            if (widget == null)
            {
                result.Synthetic++;
                return;
            }

            result.Located++;
            if (AgeWidgets.Painted(widget))
            {
                return;
            }

            Finding finding = new Finding();
            finding.Key = node.Key;
            finding.Region = node.Region;
            finding.Path = DrawnBy.Path(widget);
            finding.Why = Why(widget);
            finding.Says = NotificationAudit.Excerpt(node.Announcement);
            finding.Own =
                string.IsNullOrEmpty(result.Prefix)
                || (node.Key != null && node.Key.StartsWith(result.Prefix));
            try
            {
                finding.Rect = AgeWidgets.Clipped(widget).GetGlobalPosition();
            }
            catch (Exception) { }

            // Not in the shipped render means the gate withdrew it - the whole reason the declared side
            // is built ungated. Where the gated build could not be made at all, nothing is claimed:
            // every unpainted node is reported as a defect, which is what this check said before the
            // split and is the safe direction to be wrong in.
            if (result.Shipped != null && !result.Shipped.Contains(finding.Key ?? ""))
            {
                result.Dropped.Add(finding);
                return;
            }

            if (finding.Own)
            {
                result.Own++;
            }
            else
            {
                result.Elsewhere++;
            }

            result.Unpainted.Add(finding);
        }

        /// <summary>Which test failed and where - the widget itself, or the ancestor that took it off
        /// the screen. The two answers want different fixes: a HIDDEN ancestor is a branch the window
        /// switched off and a walk that kept its rows, and a FADED one is the pooled table retiring a
        /// surplus row, which keeps its old words as well as its place.</summary>
        private static string Why(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < 64; depth++)
                {
                    if (!at.Visible)
                    {
                        return depth == 0
                            ? "not visible"
                            : "inside a branch the window hid (" + at.name + ")";
                    }

                    at = at.Parent;
                }

                at = widget;
                for (int depth = 0; at != null && depth < 64; depth++)
                {
                    if (at.Alpha <= 0f)
                    {
                        return depth == 0
                            ? "faded to nothing"
                            : "inside something faded to nothing (" + at.name + ")";
                    }

                    at = at.Parent;
                }

                return "the widget is gone";
            }
            catch (Exception e)
            {
                return "reading it threw: " + e.GetType().Name;
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

        private static string Write(Result result)
        {
            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("screen");
                json.WriteValue(result.Screen);
                json.WritePropertyName("title");
                json.WriteValue(result.ScreenName);
                json.WritePropertyName("prefix");
                json.WriteValue(result.Prefix);

                json.WritePropertyName("counts");
                json.WriteStartObject();
                Count(json, "nodes", result.Nodes);
                Count(json, "located", result.Located);
                Count(json, "synthetic", result.Synthetic);
                Count(json, "unlocatable", result.Unlocatable);
                Count(json, "droppedByGate", result.Dropped.Count);
                Count(json, "shippedUnpainted", result.Unpainted.Count);
                Count(json, "unpaintedOwn", result.Own);
                Count(json, "unpaintedElsewhere", result.Elsewhere);
                json.WriteEndObject();

                if (result.Shipped == null)
                {
                    // Said out loud, because with no gated render to compare against every unpainted
                    // node lands in shippedUnpainted and the screen would read as a disaster.
                    json.WritePropertyName("note");
                    json.WriteValue(
                        "the gated render could not be built - nothing is classified as dropped"
                    );
                }

                Findings(json, "droppedByGate", result.Dropped);
                Findings(json, "shippedUnpainted", result.Unpainted);
                json.WriteEndObject();
            });
        }

        private static void Findings(JsonTextWriter json, string name, List<Finding> findings)
        {
            json.WritePropertyName(name);
            json.WriteStartArray();
            for (int i = 0; i < findings.Count && i < MaxListed; i++)
            {
                Finding finding = findings[i];
                json.WriteStartObject();
                json.WritePropertyName("key");
                json.WriteValue(finding.Key);
                if (!string.IsNullOrEmpty(finding.Region))
                {
                    json.WritePropertyName("region");
                    json.WriteValue(finding.Region);
                }

                json.WritePropertyName("where");
                json.WriteValue(finding.Path);
                json.WritePropertyName("why");
                json.WriteValue(finding.Why);
                if (!string.IsNullOrEmpty(finding.Says))
                {
                    json.WritePropertyName("says");
                    json.WriteValue(finding.Says);
                }

                json.WritePropertyName("own");
                json.WriteValue(finding.Own);
                json.WritePropertyName("rect");
                json.WriteStartArray();
                json.WriteValue(Math.Round(finding.Rect.xMin));
                json.WriteValue(Math.Round(finding.Rect.yMin));
                json.WriteValue(Math.Round(finding.Rect.width));
                json.WriteValue(Math.Round(finding.Rect.height));
                json.WriteEndArray();
                json.WriteEndObject();
            }

            if (findings.Count > MaxListed)
            {
                json.WriteStartObject();
                json.WritePropertyName("more");
                json.WriteValue(findings.Count - MaxListed);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        private static void Count(JsonTextWriter json, string name, int value)
        {
            json.WritePropertyName(name);
            json.WriteValue(value);
        }
    }
}

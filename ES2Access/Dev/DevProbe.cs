using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Loader.Dev;
using ES2Access.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace ES2Access.Dev
{
    /// <summary>
    /// The questions a test asks over and over, each as one compile-checked call.
    ///
    /// POST /eval can reach all of this already - that is the point of the REPL - but reaching it
    /// there means writing the traversal by hand every time: a service lookup, two casts, a null
    /// check, and a string concatenation, in a language with no `using` directives, one statement per
    /// request, and a type importer that poisons an identifier for the rest of the session if a
    /// constructed generic over a game type is ever named. Every one of those questions has exactly
    /// one right answer, and every hand-written traversal is a chance to get it subtly wrong and
    /// believe the result. So they live here instead, in a file the compiler checks against the game's
    /// real API, and /eval bodies become <c>ES2Access.Dev.DevProbe.State()</c>.
    ///
    /// Everything returns JSON, and everything that can fail fails as <c>{"error": ...}</c> rather
    /// than by throwing: a probe called from a wait-loop must always answer.
    ///
    /// Main-thread only - all of it reads live game state.
    /// </summary>
    public static partial class DevProbe
    {
        /// <summary>
        /// Whether the popup on screen READS the way it DRAWS - the notification family's own
        /// self-check, run against whichever one is up (see <see cref="NotificationAudit"/>).
        ///
        /// Sixty-nine window types share one reader and no fixture reaches most of them, so the
        /// question "does this prefab break a premise of the shared reading" has to be answerable
        /// from the popup itself. Four arrays, each empty when the popup is clean: text that is
        /// painted and unspoken, words spoken that nothing draws, nodes filed in the wrong band or
        /// walked out of the drawn order, and tooltips promised or lost. The same check runs by
        /// itself on every popup the player is shown while the dev server is up, and complains to
        /// the log.
        /// </summary>
        public static string NotificationParity()
        {
            return NotificationAudit.Json();
        }

        /// <summary>
        /// Whether the FOCUSED screen's tooltips read the way the game draws them - the same
        /// comparison, asked of any screen rather than only of a popup (see
        /// <see cref="TooltipAudit"/>).
        ///
        /// Seven arrays, four of them findings: a node promising a dossier with nothing that draws
        /// (<c>promised</c>), a node pointing at a tooltip that draws nothing (<c>misaimed</c>), a
        /// tooltip the game would draw on a control that no node covers (<c>uncovered</c>) and one
        /// whose words are in nothing the covering node carries (<c>unread</c>). The other three are
        /// weaker claims kept apart on purpose: <c>decoration</c> is the same coverage question on a
        /// widget the player cannot work, <c>hidden</c> is what only the pass with the transparency
        /// gate off can see, and <c>undescribed</c> is a defect in the GAME's own data.
        ///
        /// The painted half needs the screen to say where it is drawn
        /// (<c>Screen.RootTransform</c>); a screen that does not answers the declaration-side
        /// questions only, and says so with a null <c>root</c>.
        /// </summary>
        public static string TooltipParity()
        {
            return TooltipAudit.Json();
        }

        /// <summary>
        /// What the FOCUSED screen has never declared - hover words AND actions - against everything
        /// the engine is drawing (see <see cref="CoverageAudit"/>).
        ///
        /// The widening over <see cref="TooltipParity"/> that makes it worth a second call: the
        /// painted side comes from the ENGINE's own list of drawn windows and panels whenever the
        /// screen names no <c>Screen.RootTransform</c>, so the coverage question is finally asked of
        /// the galaxy map - where a hand audit found six undeclared dossiers and four undeclared
        /// buttons on one card while the parity check said <c>clean</c>. And a second half nothing
        /// else asks: every PAINTED control the player could work that no node stands on
        /// (<c>actionsUncovered</c>, "no node stands here") or that a node stands on and cannot press
        /// ("the node here declares no action").
        ///
        /// <paramref name="wholeTree"/> forces the live-tree walk even on a screen that names its own
        /// window - what a modal drawn over a live page needs. Counts first, then the lists, each
        /// capped with a <c>more</c> entry. It walks the whole GUI: run it on demand, never in a loop.
        /// </summary>
        public static string Coverage(bool wholeTree = false)
        {
            return CoverageAudit.Json(wholeTree);
        }

        /// <summary>
        /// The other direction: nodes the FOCUSED screen is OFFERING that the game is not drawing
        /// (see <see cref="GhostAudit"/>) - a stop the player can walk onto with blank pixels under
        /// it.
        ///
        /// Every other check in the family starts from what is painted, so a node standing on
        /// nothing is invisible to all of them - and to a tree dump, which prunes a faded row for
        /// drawing no text and then AGREES with whatever the mod declared. This is the mechanical
        /// form of the crop-versus-dump comparison that used to be the only way to find one.
        ///
        /// Unpainted nodes come in two kinds and the answer separates them. <c>droppedByGate</c> is
        /// what <see cref="UI.NodeGate"/> withdrew before the player's render was built - the gate
        /// working, informational, and present at all only because the check's own build is ungated on
        /// purpose. <c>shippedUnpainted</c> is the findings: nodes that survived the gate and still
        /// stand on nothing the game is drawing. Each carries the node key, the widget path, WHY it
        /// fails (hidden branch or faded to nothing, and which ancestor), and what the node would have
        /// said - a stale value with no name is the pooled table's signature. Counts are always
        /// complete; the lists stop at ten each and say how many more there were. Nodes with no widget
        /// and no aim are counted as <c>synthetic</c> and are not findings: a place on the map or a
        /// notification the game owns has no widget to ask about.
        ///
        /// One benign false positive, not solved on purpose: a screen mid-TRANSITION fades its own
        /// window in, and for those frames every node on it is unpainted. Run it on a settled screen,
        /// or re-run it and keep what both runs report.
        /// </summary>
        public static string Ghosts()
        {
            return GhostAudit.Json();
        }

        /// <summary>
        /// What <see cref="NodeGate"/> is taking off the FOCUSED screen, right now - the same
        /// measurement as flipping <see cref="NodeGate.Enabled"/> around two <c>/gui/graph</c> dumps,
        /// in one answer and without the two dumps drifting apart between them.
        ///
        /// The screen is built twice, once through the gate and once with it off, and the answer is
        /// the two set differences of their node keys. <c>onlyUngated</c> is the drops: every node the
        /// walks declared that the gate then withdrew, which is the gate's whole visible effect on that
        /// screen. <c>onlyGated</c> must always be empty - the gate only ever removes - so anything in
        /// it is a bug in the gate or in a walk that reads <see cref="NodeGate.Enabled"/> and declares
        /// DIFFERENT content rather than less of it; report it rather than explaining it away.
        ///
        /// Counts are always complete; each list stops at fifteen keys and says how many more there
        /// were. Neither build touches the cursor, speaks, or moves the pointer - both are the
        /// read-only inspect path. The flag is restored even when a build throws.
        ///
        /// It sees the BUILDER's half of the gate and only that half. The other half runs inside the
        /// screen's own build (<see cref="NodeGate.StillDrawn"/>, the cell-banding path) and honours
        /// the flag as well - so a cell it takes out is missing from BOTH renders and shows up in
        /// neither list. An empty answer therefore means "the builder is dropping nothing here", not
        /// "nothing was dropped": what the banding path removed is in <c>GET /log?grep=NodeGate</c>,
        /// which is the same drop line either half writes.
        ///
        /// A screen mid-TRANSITION answers with everything on it, the same benign false positive
        /// <see cref="Ghosts"/> has: its window is fading and nothing on it is settled. Ask again once
        /// it has arrived.
        /// </summary>
        public static string GateDiff()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            Screens.Screen screen = navigator == null ? null : navigator.Screen;
            if (screen == null)
            {
                return Err("no screen of ours is focused");
            }

            List<string> gated;
            List<string> ungated;
            bool was = NodeGate.Enabled;
            try
            {
                NodeGate.Enabled = true;
                gated = RenderKeys(navigator.InspectRender());
                NodeGate.Enabled = false;
                ungated = RenderKeys(navigator.InspectRender(screen));
            }
            catch (Exception e)
            {
                return Err(e.Message);
            }
            finally
            {
                NodeGate.Enabled = was;
            }

            List<string> drops = Missing(ungated, gated);
            List<string> added = Missing(gated, ungated);
            return Guarded(json =>
            {
                json.WritePropertyName("screen");
                json.WriteValue(screen.Key);
                json.WritePropertyName("counts");
                json.WriteStartObject();
                json.WritePropertyName("gated");
                json.WriteValue(gated.Count);
                json.WritePropertyName("ungated");
                json.WriteValue(ungated.Count);
                json.WritePropertyName("onlyUngated");
                json.WriteValue(drops.Count);
                json.WritePropertyName("onlyGated");
                json.WriteValue(added.Count);
                json.WriteEndObject();
                Keys(json, "onlyUngated", drops);
                Keys(json, "onlyGated", added);
            });
        }

        /// <summary>How many keys a <see cref="GateDiff"/> list names before the rest are counted.</summary>
        private const int MaxGateKeys = 15;

        private static List<string> RenderKeys(GraphRender render)
        {
            List<string> keys = new List<string>();
            if (render == null)
            {
                return keys;
            }

            for (int i = 0; i < render.Order.Count; i++)
            {
                keys.Add(Convert.ToString(render.Order[i].Id.StructuralKey));
            }

            return keys;
        }

        /// <summary>The keys of <paramref name="left"/> that <paramref name="right"/> does not
        /// have.</summary>
        private static List<string> Missing(List<string> left, List<string> right)
        {
            HashSet<string> have = new HashSet<string>(right);
            List<string> only = new List<string>();
            for (int i = 0; i < left.Count; i++)
            {
                if (!have.Contains(left[i]))
                {
                    only.Add(left[i]);
                }
            }

            return only;
        }

        private static void Keys(JsonTextWriter json, string name, List<string> keys)
        {
            json.WritePropertyName(name);
            json.WriteStartArray();
            for (int i = 0; i < keys.Count && i < MaxGateKeys; i++)
            {
                json.WriteValue(keys[i]);
            }

            json.WriteEndArray();
            if (keys.Count > MaxGateKeys)
            {
                json.WritePropertyName(name + "More");
                json.WriteValue(keys.Count - MaxGateKeys);
            }
        }

        private static readonly Func<AgeTransform, AgeTransform> Itself = widget => widget;

        /// <summary>Every widget under the panel that draws something a reader would have to account
        /// for - words or a picture - skipping the branches the window is not showing, which it keeps
        /// around holding the last tooltip's text (see <see cref="DrawnTooltip"/>).</summary>
        private static void Gather(AgeTransform widget, List<AgeTransform> found, int depth)
        {
            if (depth > MaxTooltipDepth)
            {
                return;
            }

            if (
                widget.GetComponent<AgePrimitiveLabel>() != null
                || widget.GetComponent<AgePrimitiveImage>() != null
            )
            {
                found.Add(widget);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child != null)
                {
                    Gather(child, found, depth + 1);
                }
            }
        }

        private static void WritePart(JsonTextWriter json, AgeTransform widget)
        {
            json.WriteStartObject();
            try
            {
                Rect rect = widget.GetGlobalPosition();
                json.WritePropertyName("rect");
                json.WriteStartArray();
                json.WriteValue(Round(rect.xMin));
                json.WriteValue(Round(rect.yMin));
                json.WriteValue(Round(rect.width));
                json.WriteValue(Round(rect.height));
                json.WriteEndArray();

                AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
                if (label != null)
                {
                    json.WritePropertyName("raw");
                    json.WriteValue(label.Text);
                    json.WritePropertyName("spoken");
                    json.WriteValue(AgeText.Label(label));
                }

                AgePrimitiveImage image = widget.GetComponent<AgePrimitiveImage>();
                if (image != null)
                {
                    json.WritePropertyName("asset");
                    json.WriteValue(image.Texture == null ? null : image.Texture.name);
                }

                json.WritePropertyName("name");
                json.WriteValue(widget.name);
            }
            catch (Exception e)
            {
                json.WritePropertyName("error");
                json.WriteValue(e.Message);
            }

            json.WriteEndObject();
        }

        private static string Guarded(Action<JsonTextWriter> body)
        {
            try
            {
                return DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    body(json);
                    json.WriteEndObject();
                });
            }
            catch (Exception e)
            {
                return Err(e.Message);
            }
        }

        private static string Err(string message)
        {
            return DevJson.Error(message);
        }
    }
}

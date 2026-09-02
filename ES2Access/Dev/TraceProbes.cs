using System;
using ES2Access.Core.UI.Graph;
using ES2Access.Screens;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Dev
{
    /// <summary>The per-frame trace logs: where the keyboard is, and what the row under the cursor
    /// is.</summary>
    public static partial class DevProbe
    {
        /// <summary>
        /// One line in the log for every frame, naming the whole of what decides where the keyboard
        /// is: the polled stack, the screen the keys are going to, the cursor, how many nodes that
        /// screen declared this frame, and the state of the tutorial popup and the three window
        /// classes the game weighs it against.
        ///
        /// Always false, so <c>POST /wait</c> on it never finishes early and records the whole
        /// passage: a transition is frames long, and polling from outside samples between them and
        /// misses exactly the frame that moved the cursor. Read the result with
        /// <c>GET /log?grep=trace</c>.
        /// </summary>
        public static bool Trace(string tag)
        {
            try
            {
                System.Text.StringBuilder line = new System.Text.StringBuilder();
                line.Append("trace ").Append(tag).Append(" f=").Append(Time.frameCount);

                ScreenManager screens = ModEntry.Screens;
                line.Append(" stack=");
                if (screens == null)
                {
                    line.Append('?');
                }
                else
                {
                    foreach (Screens.Screen screen in screens.Stack)
                    {
                        line.Append(screen.Key).Append(':').Append(screen.Layer).Append(' ');
                    }
                }

                GraphNavigator navigator = ModEntry.Navigator;
                Screens.Screen current = navigator == null ? null : navigator.Screen;
                ControlId focused = navigator == null ? null : navigator.FocusedKey;
                line.Append(" cur=").Append(current == null ? "-" : current.Key);
                line.Append(" node=")
                    .Append(
                        focused == null ? "-" : Convert.ToString(focused.StructuralKey)
                    );
                line.Append(" nodes=")
                    .Append(navigator == null ? -1 : navigator.RenderedNodeCount);
                line.Append(" ").Append(TutorialState());
                Core.Util.Log.Info(line.ToString());
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("trace threw: " + e.Message);
            }

            return false;
        }

        /// <summary>
        /// A per-frame recording of what the FOCUSED control would say - the row trace, driven from a
        /// <c>POST /wait</c> predicate exactly as <see cref="Trace"/> is.
        ///
        /// The question it answers is how long after an arrival the page is still CHANGING what it
        /// says. A landing announces once, so anything the row gains after that frame is lost, and the
        /// wait a landing spends is only defensible against a measurement of it: run this across the
        /// arrival and count the frames from the camera stopping to the last line that differs.
        /// Always false, so the wait runs to its timeout.
        /// </summary>
        public static bool RowTrace(string tag)
        {
            try
            {
                GraphNavigator navigator = ModEntry.Navigator;
                GraphNode node = navigator == null ? null : navigator.CurrentNode;
                Core.Util.Log.Info(
                    "rowtrace "
                        + tag
                        + " f="
                        + Time.frameCount
                        + " settling="
                        + GalaxyViewLevels.CameraSettling
                        + " | "
                        + (node == null ? "-" : GraphAnnouncer.ComposeFull(node))
                );
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("rowtrace threw: " + e.Message);
            }

            return false;
        }

    }
}

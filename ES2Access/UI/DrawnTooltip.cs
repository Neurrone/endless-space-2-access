using System;
using System.Collections.Generic;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// What the tooltip on screen is SAYING, read off the tooltip as drawn.
    ///
    /// Most of the game's tooltips carry their words: a string on the widget, there to be read
    /// whether or not anything is hovering it. The interesting ones do not. A tooltip that names a
    /// CLASS carries only a class name and a target object, and the words are assembled at draw time
    /// by the tooltip window - which loads a list of little prefabs ("panel features"), hands each of
    /// them the target, and lets each write its own line from live data. A resource's tooltip is a
    /// stat block built that way; there is no string anywhere that holds it.
    ///
    /// There is no service that will assemble one on request either: the only way the text exists is
    /// for the window to build it. So this reads the window - the features it is currently showing,
    /// in the order it laid them out. Which makes what the review buffer holds equal to what is drawn
    /// by construction, rather than by a second implementation of the game's own assembly rules that
    /// would drift from it.
    ///
    /// A feature at a time, not a drawn line at a time: the window's own unit of assembly is the
    /// feature, and reading across features fused a caption from one onto a value from the next
    /// while leaving captions and values inside a feature apart. <see cref="TooltipFeatures"/> is
    /// where a feature is turned into lines, and where the fallbacks live.
    ///
    /// It follows that a class tooltip only reads while it is up, which is exactly why focus asks the
    /// game to draw the focused widget's tooltip (see <see cref="PointerFocus"/>). The window is
    /// asked which tooltip it is drawing before anything is read from it, so a stale tooltip - one
    /// still fading out from the widget focus just left - is never mistaken for this one's.
    ///
    /// Main-thread only.
    /// </summary>
    public static class DrawnTooltip
    {
        private static readonly List<TooltipFeatures.Reading> NoFeatures =
            new List<TooltipFeatures.Reading>();

        /// <summary>The lines the tooltip window is drawing for <paramref name="tooltip"/> right now,
        /// and nothing at all when it is drawing something else or nothing.</summary>
        public static IList<string> Lines(AgeTooltip tooltip)
        {
            List<string> lines = new List<string>();
            IList<TooltipFeatures.Reading> features = Features(tooltip);
            for (int i = 0; i < features.Count; i++)
            {
                lines.AddRange(features[i].Lines);
            }

            return lines;
        }

        /// <summary>
        /// The same reading, feature by feature, with the name of each feature class and of the
        /// reader that answered for it.
        ///
        /// This is what a probe asks for. A tooltip family nobody has looked at yet still reads -
        /// the fallback covers it - and that is precisely why the coverage has to be visible
        /// somewhere: "default" against a feature that lays its words out in a shape bands cannot
        /// express is the defect, and it is invisible in the lines themselves.
        /// </summary>
        public static IList<TooltipFeatures.Reading> Features(AgeTooltip tooltip)
        {
            try
            {
                if (tooltip == null)
                {
                    return NoFeatures;
                }

                GuiTooltipWindow window = Window();
                if (window == null || !ReferenceEquals(window.AgeTooltip, tooltip))
                {
                    return NoFeatures;
                }

                IList<AgeTransform> children = AgeWidgets.DrawnChildren(
                    window.PanelFeaturesTable
                );
                if (children == null)
                {
                    return NoFeatures;
                }

                List<TooltipFeatures.Reading> readings = new List<TooltipFeatures.Reading>();
                for (int i = 0; i < children.Count; i++)
                {
                    // Undrawn features are skipped rather than read, and that is load-bearing rather
                    // than tidy: the window POOLS its features instead of destroying them, so a
                    // tooltip that once showed six still has four hanging off it holding the text of
                    // whatever was hovered before. StrictVisibility is no exemption - it tells the
                    // ARRANGER to keep counting a faded child's slot, and the renderer skips it all
                    // the same (<see cref="AgeWidgets.DrawnChild"/>).
                    AgeTransform child = AgeWidgets.DrawnChild(children, i);
                    if (child != null)
                    {
                        readings.Add(TooltipFeatures.Read(child));
                    }
                }

                return readings;
            }
            catch (Exception e)
            {
                Log.Warn("tooltip: reading the drawn tooltip threw: " + e);
                return NoFeatures;
            }
        }

        /// <summary>Whether the window is drawing anything at all right now.</summary>
        public static GuiTooltipWindow Window()
        {
            GuiTooltipWindow window = Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<GuiTooltipWindow>(false)
                : null;
            return window != null && window.Shown ? window : null;
        }
    }
}

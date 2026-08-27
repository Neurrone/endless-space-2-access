using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The column of boxes the game stacks down the left edge of the screen, read off what is drawn in
    /// them.
    ///
    /// Every page that has a left column pushes its panels into the SAME window - the star system page,
    /// the research wheel, the senate, the empire summary, the economy screen - so which panels are up
    /// is a question about that one window, and it is answered by asking it rather than by each screen
    /// keeping a list of the panels it believes it opened. One stop per panel, top to bottom, in the
    /// order they are drawn.
    ///
    /// Most of these panels are readouts and no decisions, and each new page brings three or four more
    /// of them. So the contents are read from the SHAPE of the widget tree rather than modelled field
    /// by field: a group whose children are all PRIMITIVES - a number, an icon, a word - is one thing
    /// the game has drawn out of several pieces ("3" beside a population icon beside "Imperials") and
    /// reads as one line; a group that contains other GROUPS is a container, and each of those is a
    /// line of its own. Taking the outermost group with any text at all instead collapses a whole panel
    /// into one sentence, and descending to every leaf scatters a drawn line into its digits.
    ///
    /// Two escape hatches, because the shape of a tree cannot answer everything:
    /// <see cref="SpecialCells"/> hands a screen the chance to answer for a widget the shape cannot name
    /// - a bar chart, a number beside a bare symbol - and <see cref="TransparentTest"/> is for a group
    /// the game made clickable that is really a band of readouts (a box that answers a click only in the
    /// developers' god mode).
    ///
    /// The one thing read for its meaning rather than its shape is a <c>PanelFeatureEffects</c> block:
    /// the game builds it as a caption over a table of effect lines, each a separate sentence about the
    /// empire, and gluing those into one line is how "Effects:" comes to be followed by a paragraph.
    /// </summary>
    public static class SidePanels
    {
        /// <summary>A screen's answer for a widget the tree's shape cannot name. Add whatever cells the
        /// widget stands for and return true to stop the walk descending into it; false is the ordinary
        /// walk.</summary>
        public delegate bool SpecialCells(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        );

        /// <summary>Whether a group the game made clickable is really a band of readouts.</summary>
        public delegate bool TransparentTest(AgeTransform widget, SidePanel panel);

        public static SidePanelsWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<SidePanelsWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The panels the window is drawing, topmost first - which is the order the player
        /// reads them in, and is not the order the window happens to hold them in.</summary>
        public static void Drawn(List<SidePanel> into)
        {
            into.Clear();
            try
            {
                SidePanelsWindow window = Window();
                if (window == null)
                {
                    return;
                }

                SidePanel[] panels = window.GetComponentsInChildren<SidePanel>(true);
                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i] != null && AgeWidgets.Visible(panels[i].AgeTransform))
                    {
                        into.Add(panels[i]);
                    }
                }

                into.Sort(ByDrawnY);
            }
            catch (Exception e)
            {
                Log.Warn("side panels: listing the drawn panels threw: " + e);
            }
        }

        private static readonly Comparison<SidePanel> ByDrawnY = (left, right) =>
        {
            float a = left.AgeTransform.GetGlobalPosition().y;
            float b = right.AgeTransform.GetGlobalPosition().y;
            return a.CompareTo(b);
        };

        /// <summary>
        /// What a panel is called, for the panels a screen has no name of its own for.
        ///
        /// Some of these boxes DO carry a drawn heading, and where one does it is the name a sighted
        /// player reads - so it is taken first, and the walk then leaves that label out rather than
        /// reading the stop's own name back as its first line. The rest are unlabelled boxes with an
        /// icon in the corner explaining them on hover, and that sentence is a fallback and not a name:
        /// a stop is announced by its name on every Tab into it, so a panel that ends up on a whole
        /// sentence is a panel for its screen to add a word for.
        ///
        /// KNOWN GAP (roadmap): where a panel DOES draw a heading, the explanatory tooltip on its
        /// corner icon is dropped entirely - the drawn word wins as the name and the sentence is
        /// declared nowhere, so the player cannot reach it.
        /// </summary>
        public static string Name(SidePanel panel)
        {
            string drawn = AgeText.Label(TitleLabel(panel));
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            string described = CardActions.FirstLine(HeaderTooltip(panel));
            return string.IsNullOrEmpty(described) ? panel.GetType().Name : described;
        }

        /// <summary>The heading a panel draws across its own top, where it has one. The field is looked
        /// up by name because it is declared on the panels that have one rather than on the base class
        /// they share.</summary>
        private static AgePrimitiveLabel TitleLabel(SidePanel panel)
        {
            try
            {
                FieldInfo field = panel.GetType().GetField(
                    "PanelTitle",
                    BindingFlags.Instance | BindingFlags.Public
                );
                AgePrimitiveLabel label =
                    field == null ? null : field.GetValue(panel) as AgePrimitiveLabel;
                return label != null && AgeWidgets.Visible(label.AgeTransform) ? label : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTooltip HeaderTooltip(SidePanel panel)
        {
            try
            {
                AgePrimitiveImage[] images = panel.GetComponentsInChildren<AgePrimitiveImage>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    AgeTooltip tooltip = AgeWidgets.Raw(images[i].AgeTransform);
                    if (tooltip != null && AgeWidgets.Readable(tooltip) != null)
                    {
                        return tooltip;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>
        /// The same reading, for a band that is NOT one of these panels.
        ///
        /// The rules above - a group of primitives is one drawn line, a group of groups is a band of
        /// several, a clickable group is a control - are about how this game's prefabs are BUILT, not
        /// about the left edge of the screen. A screen with a band whose prefab it cannot see (the
        /// marketplace's price-and-quantity strip, a trading company's line) reads it the same way
        /// rather than guessing which label captions which number.
        /// </summary>
        public static void Content(
            List<Cell> cells,
            AgeTransform root,
            string keyPrefix,
            SpecialCells special,
            TransparentTest transparent
        )
        {
            Collect(cells, root, keyPrefix, 0, null, special, transparent, null);
        }

        /// <summary>
        /// A panel read as it is drawn: every group in it that says something becomes a line, in the rows
        /// the panel lays them out in.
        ///
        /// The drawn heading is the STOP's name and is normally not read back as a line of its own. Where
        /// the game hung an explanation of the whole panel on that heading
        /// (<c>EmpireStatusSidePanel.Refresh</c> :131-132), the heading becomes the panel's first line so
        /// that explanation is reachable: the stop's name is a spoken phrase and carries no buffer, so
        /// there is nowhere else for it to go.
        /// </summary>
        public static void Readouts(
            List<Cell> cells,
            SidePanel panel,
            string keyPrefix,
            SpecialCells special,
            TransparentTest transparent
        )
        {
            AgePrimitiveLabel title = TitleLabel(panel);
            AgeTransform titled = title == null ? null : title.AgeTransform;
            if (titled != null && Explained(AgeWidgets.Raw(titled)))
            {
                cells.Add(Cells.Readout(titled, keyPrefix + titled.name + "/title"));
            }

            Collect(cells, panel.ContentGroup, keyPrefix, 0, panel, special, transparent, titled);
        }

        /// <summary>
        /// ONE block of a panel a screen is declaring in pieces, because the game captions the blocks
        /// and the player needs to hear which one a line belongs to.
        ///
        /// <paramref name="block"/> is a child of the panel's content group, so the walk starts where
        /// <see cref="Readouts"/>' own recursion would have reached it: the same depth, the same panel,
        /// the same keys. A screen that splits a panel this way changes where the lines are ANNOUNCED,
        /// never what they are called.
        /// </summary>
        public static void Block(
            List<Cell> cells,
            SidePanel panel,
            AgeTransform block,
            string keyPrefix,
            SpecialCells special,
            TransparentTest transparent
        )
        {
            AgePrimitiveLabel title = TitleLabel(panel);
            Collect(
                cells,
                block,
                keyPrefix,
                1,
                panel,
                special,
                transparent,
                title == null ? null : title.AgeTransform
            );
        }

        /// <summary>Whether a tooltip has anything for the player: words the game wrote into it, or a
        /// class, which means the tooltip window assembles it and having content is definitional.
        /// </summary>
        private static bool Explained(AgeTooltip tooltip)
        {
            if (tooltip == null)
            {
                return false;
            }

            return AgeWidgets.Readable(tooltip) == null
                || !string.IsNullOrEmpty(CardActions.FirstLine(tooltip));
        }

        private const int MaxScrapeDepth = 6;

        private static void Collect(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            int depth,
            SidePanel panel,
            SpecialCells special,
            TransparentTest transparent,
            AgeTransform skip
        )
        {
            if (
                widget == null
                || depth > MaxScrapeDepth
                || !AgeWidgets.Visible(widget)
                || ReferenceEquals(widget, skip)
            )
            {
                return;
            }

            try
            {
                if (special != null && special(cells, widget, keyPrefix, panel))
                {
                    return;
                }

                if (Effects(cells, widget, keyPrefix, panel))
                {
                    return;
                }

                AgeControlButton button = AgeWidgets.Button(widget);
                string text = AgeWidgets.TextOf(widget);
                bool activatable =
                    button != null
                    && !string.IsNullOrEmpty(button.OnActivateMethod)
                    && (transparent == null || !transparent(widget, panel));
                if (!activatable && depth < MaxScrapeDepth && HasGroupChild(widget))
                {
                    IList<AgeTransform> children = widget.Children;
                    for (int i = 0; children != null && i < children.Count; i++)
                    {
                        // Every strip in these panels is POOLED: a colony's population strip keeps a
                        // row per population the panel has EVER shown and retires the surplus by
                        // fading it, so a walk gated on visibility declared two rows of a system the
                        // player had left as buttons saying nothing but their stale counts
                        // (<see cref="AgeWidgets.DrawnChild"/>, the same rule as <see cref="Effects"/>).
                        AgeTransform child = AgeWidgets.DrawnChild(children, i);
                        if (child == null)
                        {
                            continue;
                        }

                        Collect(
                            cells,
                            child,
                            keyPrefix,
                            depth + 1,
                            panel,
                            special,
                            transparent,
                            skip
                        );
                    }

                    return;
                }

                if (string.IsNullOrEmpty(text) && !activatable)
                {
                    return;
                }

                // Read with every tooltip the game hung on the PIECES of the line, not just the one on
                // the group: these panels caption a line with an icon and put the description on the
                // label, and the line is the only node either is reachable from.
                string key = PathKey(keyPrefix, widget, panel);
                cells.Add(
                    activatable
                        ? Cells.Control(widget, button, text, key)
                        : Cells.Readout(widget, key)
                );
            }
            catch (Exception e)
            {
                Log.Warn("side panels: reading a panel threw: " + e);
            }
        }

        /// <summary>
        /// A node key that cannot collide, by construction: the widget's index path from the panel's
        /// own root down, with the widget's name kept as a readable suffix. Two distinct widgets never
        /// share an index path, so two siblings the prefab gave the same name - the representatives
        /// panel's sensitivity legend draws two children both called "Key" - get two ids where a
        /// name-plus-depth key gave them one and the builder refused the second, taking the whole
        /// panel's walk with it (the duplicate-id throw the roadmap carried). The path is as stable
        /// across frames as the drawn layout it mirrors, which is what a key was ever stable as.
        /// </summary>
        private static string PathKey(string keyPrefix, AgeTransform widget, SidePanel panel)
        {
            string path = "";
            AgeTransform root = panel == null ? null : panel.AgeTransform;
            AgeTransform at = widget;
            int guard = 0;
            while (at != null && !ReferenceEquals(at, root) && guard++ < 16)
            {
                path = "/" + AgeWidgets.IndexInParent(at) + path;
                at = at.Parent;
            }

            return keyPrefix + "p" + path + "/" + widget.name;
        }

        /// <summary>Whether anything inside this widget is itself a container - which is what makes the
        /// widget a band of separate lines rather than one line drawn out of pieces. Counted off the
        /// engine's own drawing test (<see cref="AgeWidgets.DrawnChild"/>) so that it agrees with the walk
        /// it gates: a strip whose only remaining children are POOLED rows the game retired by fading
        /// them is not a band, it is a strip that draws nothing.</summary>
        private static bool HasGroupChild(AgeTransform widget)
        {
            IList<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child == null)
                {
                    continue;
                }

                IList<AgeTransform> grandchildren = child.Children;
                for (int j = 0; grandchildren != null && j < grandchildren.Count; j++)
                {
                    if (AgeWidgets.DrawnChild(grandchildren, j) != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The block of effect lines a panel hangs under a caption - what a government does, what a law
        /// costs the empire, what a population is worth on a planet.
        ///
        /// The table's lines are all primitives, so the shape of the tree calls it one line; but each of
        /// those lines is a separate sentence the game wrote about a separate effect, and the caption is
        /// what says they belong together. So the caption is one line and each effect is a line of its
        /// own.
        ///
        /// The table is POOLED: a panel re-bound to something with fewer effects retires the surplus
        /// lines by FADING them (<c>GuiEffectMapper.UnloadEffects</c>), which leaves them Visible and
        /// still holding the previous binding's words, so both walks ask the engine's own drawing test
        /// (<see cref="AgeWidgets.DrawnChild"/>) instead of the visibility flag.
        /// </summary>
        private static bool Effects(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            PanelFeatureEffects effects = widget.GetComponent<PanelFeatureEffects>();
            if (effects == null)
            {
                return false;
            }

            AgeTransform caption =
                effects.TitleLabel == null ? null : effects.TitleLabel.AgeTransform;
            if (caption != null && AgeWidgets.Visible(caption))
            {
                cells.Add(
                    Cells.Readout(caption, AgeWidgets.Raw(caption), PathKey(keyPrefix, caption, panel))
                );
            }

            IList<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform table = AgeWidgets.DrawnChild(children, i);
                if (table == null || ReferenceEquals(table, caption))
                {
                    continue;
                }

                IList<AgeTransform> lines = table.Children;
                for (int j = 0; lines != null && j < lines.Count; j++)
                {
                    AgeTransform line = AgeWidgets.DrawnChild(lines, j);
                    if (line == null || string.IsNullOrEmpty(AgeWidgets.TextOf(line)))
                    {
                        continue;
                    }

                    cells.Add(
                        Cells.Readout(line, AgeWidgets.Raw(line), PathKey(keyPrefix, line, panel))
                    );
                }
            }

            return true;
        }
    }
}

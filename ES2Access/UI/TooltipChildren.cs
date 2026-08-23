using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// The dossiers a node owns BEYOND its own tooltip, as child nodes of that node.
    ///
    /// One widget on this game's screens routinely carries several renderer-assembled dossiers - a
    /// system's map label carries the star's stat block and one per deposit in the ground, a planet
    /// card carries the planet's and one per FIDSI figure, a technology's dot carries one per thing
    /// it unlocks - and only ONE of them can ever be on the screen at a time (the game keeps a
    /// single tooltip window, so hovering inside a tooltip REPLACES it). Folding them all into one
    /// node's review buffer therefore both hides which is which and promises words the game will
    /// only ever draw for whichever the node happens to point at.
    ///
    /// So they become nodes: the owner turns into an expandable group whose children are two
    /// REGIONS, stepped with Alt+Up/Down. FIRST the node's own actions and structural children in
    /// the order the surface draws them (<see cref="Actions"/>), SECOND a region called "Tooltips"
    /// (<see cref="Emit"/>) holding one node per dossier. A node with no actions has only the second
    /// region, which is a lone region whose jump is silently consumed - by design.
    ///
    /// A dossier node is NOT a button: Enter on it is consumed and says nothing, because there is
    /// nothing there to do. Its name is the dossier's own header line, read off the wrapper the game
    /// hangs on the tooltip (<see cref="AgeWidgets.TooltipTitle"/>) - never a phrase this mod
    /// invented - and focusing it points the pointer at the dossier's own carrier, so the game draws
    /// that dossier in place of the parent's and the buffer holds exactly the drawn lines.
    ///
    /// What EARNS a node here: a tooltip whose words the renderer assembles
    /// (<see cref="TooltipMode.Indicate"/>), that the game would draw at all
    /// (<see cref="AgeWidgets.Draws"/> - the engine's own content-or-target test), and that names
    /// itself. A class-only tooltip a prefab hung on decoration has no target and no name, and a
    /// node for it would be an empty stop the player has to step past.
    /// </summary>
    public static class TooltipChildren
    {
        /// <summary>One dossier: what to call it, which tooltip it is, and what to point at to make
        /// the game draw it.</summary>
        public struct Dossier
        {
            /// <summary>The header line the dossier draws for itself.</summary>
            public Func<string> Name;

            /// <summary>The tooltip whose words the node carries. Null for a dossier that only
            /// exists INSIDE another tooltip's drawing, which reads through <see cref="Lines"/>
            /// instead.</summary>
            public AgeTooltip Tooltip;

            /// <summary>What the pointer is put on. For a nested dossier this is the PARENT
            /// dossier's carrier: pointing inside a drawn tooltip releases the data the inner widget
            /// was holding, so the inner tooltip draws nothing (measured 2026-08-22).</summary>
            public AgeTransform Anchor;

            /// <summary>The tooltip the pointer asks for, where that is not <see cref="Tooltip"/>.
            /// </summary>
            public AgeTooltip Aim;

            /// <summary>The words, where they are read off the game's own wrapper rather than off a
            /// drawing (a nested dossier).</summary>
            public Func<IList<string>> Lines;

            /// <summary>How the tooltip reaches the player, where the caller knows better than
            /// <see cref="GraphNodes.ModeFor"/> - which is exactly the case a prefab author's own
            /// SENTENCE is the node's name: announcing it as well would say the first line twice
            /// (<see cref="TooltipMode.None"/> leaves it in the buffer, where the rest of it is).
            /// Null asks the shared rule.</summary>
            public TooltipMode? Mode;
        }

        /// <summary>The region the node's own actions and structural children belong to - declared
        /// before the caller emits them, so that Alt+Up/Down steps between "what I can do here" and
        /// "what the game explains here". Setting it costs nothing where the node has no actions: a
        /// region nothing is tagged with does not exist.
        ///
        /// Answers the region that was in force, which the caller hands back to <see cref="Emit"/>:
        /// these two open regions INSIDE somebody else's stop, and a stop left tagged with a region
        /// of ours would put every later node of that stop in it.</summary>
        public static object Actions(GraphBuilder builder, string key)
        {
            if (builder == null)
            {
                return null;
            }

            object outer = builder.Region;
            builder.SetRegion(key + "/actions");
            return outer;
        }

        /// <summary>
        /// The "Tooltips" region: one node per dossier, in the order the surface draws them, and the
        /// stop handed back to <paramref name="outer"/> afterwards.
        ///
        /// The region's name is announced the way every other block caption is - as a context the
        /// announcer reads when focus enters it - so the player hears "Tooltips" once on the way in
        /// rather than on every dossier.
        /// </summary>
        public static void Emit(
            GraphBuilder builder,
            string key,
            IList<Dossier> dossiers,
            object outer
        )
        {
            if (builder == null)
            {
                return;
            }

            if (dossiers == null || dossiers.Count == 0)
            {
                builder.SetRegion(outer);
                return;
            }

            builder.SetRegion(key + "/tooltips");
            builder.PushContext(ModStrings.Get(ModStrings.NodeTooltipsRegion));
            try
            {
                for (int i = 0; i < dossiers.Count; i++)
                {
                    builder.AddItem(ControlId.Structural(key + "/tooltip/" + i), Node(dossiers[i]));
                }
            }
            finally
            {
                builder.PopContext();
                builder.SetRegion(outer);
            }
        }

        /// <summary>One dossier as a node. No <c>OnActivate</c>: the engine consumes Enter on a node
        /// that wires none and says nothing, which is exactly what a thing there is nothing to do to
        /// should answer.</summary>
        public static NodeVtable Node(Dossier dossier)
        {
            Dossier it = dossier;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(it.Name) },
            };

            if (it.Lines != null && it.Tooltip != null)
            {
                // A caller with a reader of its own for a dossier the game keeps TWO widgets for and
                // swaps between them (a system's star, on its map label and again over the star once
                // the camera is in): the words come from the reader, the mode from the tooltip, so
                // the section still counts as a drawn dossier to the pointer and the parity audit.
                vtable.Sections = GraphNodes.Sections(
                    new NodeSection(
                        it.Lines,
                        it.Mode.HasValue ? it.Mode.Value : GraphNodes.ModeFor(it.Tooltip)
                    )
                );
            }
            else if (it.Lines != null)
            {
                vtable.Sections = GraphNodes.Sections(NodeSection.Buffer(it.Lines));
            }
            else
            {
                vtable.Sections = GraphNodes.SectionsFor(
                    vtable,
                    new List<AgeTooltip>(1) { it.Tooltip },
                    null,
                    it.Mode
                );
            }

            AgeTooltip aim = it.Aim ?? it.Tooltip;
            AgeTransform anchor = it.Anchor ?? (aim == null ? null : aim.AgeTransform);
            if (aim != null)
            {
                vtable.PointsAt = () => aim;
                vtable.OnFocusVisual = () => PointerFocus.MoveTo(anchor, aim, anchor);
                vtable.OnBlurVisual = AgeWidgets.ReleasePointer;
            }

            return vtable;
        }

        /// <summary>
        /// Add a widget's own dossier to the list, where it has one worth a node.
        ///
        /// Silently drops anything that is not a renderer-assembled dossier the game would draw and
        /// that names itself - which is what makes it safe to hand this every widget of a strip
        /// without asking which of them the prefab bound this time.
        /// </summary>
        public static void Add(List<Dossier> into, AgeTransform widget)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            Add(into, AgeWidgets.Raw(widget), widget);
        }

        /// <summary>The same for a tooltip the caller has already resolved.
        /// <paramref name="anchor"/> is what the tooltip is drawn under, and
        /// <paramref name="lines"/> the caller's own reader where it has one.</summary>
        public static void Add(
            List<Dossier> into,
            AgeTooltip tooltip,
            AgeTransform anchor = null,
            Func<IList<string>> lines = null
        )
        {
            if (into == null || !Qualifies(tooltip))
            {
                return;
            }

            for (int i = 0; i < into.Count; i++)
            {
                // The game CLONES a tooltip onto the widgets inside a card and points several
                // widgets of one label at the same wrapper (a system's star, its name and its
                // population count all carry the one StarSystem dossier), so identity is what the
                // game would DRAW from, never the component - the same rule the tooltip resolver
                // was given in batch 1.
                if (AgeWidgets.SameTooltip(into[i].Tooltip, tooltip))
                {
                    return;
                }
            }

            AgeTooltip tip = tooltip;
            into.Add(
                new Dossier
                {
                    Name = () => AgeWidgets.TooltipTitle(tip),
                    Tooltip = tip,
                    Anchor = anchor ?? tooltip.AgeTransform,
                    Lines = lines,
                }
            );
        }

        /// <summary>
        /// A widget whose explanation the game wrote as PLAIN TEXT, as a node of its own.
        ///
        /// <see cref="Add"/> deliberately takes only renderer-assembled dossiers, because a plain
        /// sentence has somewhere cheaper to go: the owning row's buffer. That stops being true once a
        /// row carries SEVERAL of them - a census slice draws three wordless dots and a boost badge,
        /// each with a sentence of its own - because a buffer merges them into a paragraph the player
        /// cannot tell apart or step through (owner ruling: one row means a row of NODES, never one
        /// merged node).
        ///
        /// The sentence's first line is the name, and the mode is <see cref="TooltipMode.None"/> so
        /// the same words are not announced twice; the whole sentence is still in the node's buffer.
        /// PAINTED is the gate, because these badges are prefab decoration the game fades rather than
        /// hides.
        /// </summary>
        public static void AddPlain(List<Dossier> into, AgeTransform widget)
        {
            if (into == null || !AgeWidgets.Painted(widget))
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            if (
                tooltip == null
                || AgeWidgets.Readable(tooltip) == null
                || !AgeWidgets.Draws(tooltip)
            )
            {
                return;
            }

            for (int i = 0; i < into.Count; i++)
            {
                if (AgeWidgets.SameTooltip(into[i].Tooltip, tooltip))
                {
                    return;
                }
            }

            AgeTooltip tip = tooltip;
            into.Add(
                new Dossier
                {
                    Name = CardActions.NameFromTooltip(tip),
                    Tooltip = tip,
                    Anchor = widget,
                    Mode = TooltipMode.None,
                }
            );
        }

        /// <summary>Every plain-text explanation hanging INSIDE a widget, one node each, in the order
        /// the prefab lays them out - for a row that draws a strip of wordless badges.</summary>
        public static void AddPlainInside(List<Dossier> into, AgeTransform widget, int maxDepth = 4)
        {
            if (into == null || widget == null || !AgeWidgets.Painted(widget))
            {
                return;
            }

            List<AgeTooltip> found = new List<AgeTooltip>();
            AgeWidgets.EffectiveTooltips(
                widget,
                found,
                TooltipReach.Own | TooltipReach.Descendants,
                maxDepth
            );
            for (int i = 0; i < found.Count; i++)
            {
                AgeTooltip tooltip = found[i];
                AddPlain(into, tooltip == null ? null : tooltip.AgeTransform);
            }
        }

        /// <summary>Every dossier drawn INSIDE a widget, in the prefab's own order - for a card whose
        /// figures each carry one.</summary>
        public static void AddInside(List<Dossier> into, AgeTransform widget, int maxDepth = 4)
        {
            if (into == null || widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            List<AgeTooltip> found = new List<AgeTooltip>();
            AgeWidgets.EffectiveTooltips(
                widget,
                found,
                TooltipReach.Own | TooltipReach.Descendants,
                maxDepth
            );
            for (int i = 0; i < found.Count; i++)
            {
                Add(into, found[i]);
            }
        }

        /// <summary>Whether a tooltip earns a node of its own: the renderer assembles its words, the
        /// game would draw it, and it has a name to be called by.</summary>
        private static bool Qualifies(AgeTooltip tooltip)
        {
            try
            {
                return tooltip != null
                    && GraphNodes.ModeFor(tooltip) == TooltipMode.Indicate
                    && AgeWidgets.Draws(tooltip)
                    && !string.IsNullOrEmpty(AgeWidgets.TooltipTitle(tooltip));
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

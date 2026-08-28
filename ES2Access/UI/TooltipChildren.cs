using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

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
    /// nothing there to do. It is named after the widget a MOUSE would hover to raise it
    /// (<see cref="HoverName"/>) - never a phrase this mod invented - and focusing it points the
    /// pointer at the dossier's own carrier, so the game draws that dossier in place of the parent's
    /// and the buffer holds exactly the drawn lines.
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
            /// <summary>What to call the dossier: the widget a mouse would hover to raise it, in the
            /// words that widget draws (<see cref="HoverName"/>).</summary>
            public Func<string> Name;

            /// <summary>The tooltip whose words the node carries. Null for a dossier that only
            /// exists INSIDE another tooltip's drawing, which reads through <see cref="Lines"/>
            /// instead.</summary>
            public AgeTooltip Tooltip;

            /// <summary>What the pointer is put on. For a nested dossier this is the PARENT
            /// dossier's carrier: pointing inside a drawn tooltip releases the data the inner widget
            /// was holding, so the inner tooltip draws nothing (measured 2026-08-22).</summary>
            public AgeTransform Anchor;

            /// <summary>The widget the CALLER read this dossier off, where it named one - what the
            /// node's existence is then gated on
            /// (<see cref="Core.UI.Graph.DrawnNode"/>), since a dossier node is keyed by its
            /// place under the owner and its id names nothing.
            ///
            /// Deliberately not <see cref="Anchor"/>, which falls back to the tooltip's OWN transform:
            /// this game's table prefabs stretch a switched-off <c>TooltipArea</c> over a row they are
            /// drawing perfectly well, so asking that transform "are you painting" answers no for
            /// live content. Only a widget the walk itself was holding answers.</summary>
            public AgeTransform Carrier;

            /// <summary>The tooltip the pointer asks for, where that is not <see cref="Tooltip"/>.
            /// </summary>
            public AgeTooltip Aim;

            /// <summary>The words, where they are read off the game's own wrapper rather than off a
            /// drawing (a nested dossier).</summary>
            public Func<IList<string>> Lines;

            /// <summary>
            /// Which widget carries this dossier NOW, asked afresh every time the pointer is aimed
            /// and the name is read.
            ///
            /// For a dossier the game draws through a widget it swaps under the player: a strip item
            /// the game re-pools as the camera changes what it is drawing, or a tooltip the game keeps
            /// ONE of on a window and re-points at whatever the camera is looking at. The pointer is
            /// committed once per focus CHANGE and then re-asserted every frame, so a widget resolved
            /// when the node was declared goes on being aimed at after the game has given it to
            /// somebody else - and the player hears one thing described while the screen draws
            /// another.
            ///
            /// Null where the carrier is fixed for the life of the thing, which is most of them.
            /// </summary>
            public Func<AgeTooltip> LiveAim;
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
                    builder.AddItem(Stands(ControlId.Structural(key + "/tooltip/" + i), dossiers[i]));
                }
            }
            finally
            {
                builder.PopContext();
                builder.SetRegion(outer);
            }
        }

        /// <summary>
        /// One dossier as a declared node, at the one place a dossier becomes one.
        ///
        /// A dossier node is keyed by its place under its owner, so its id names nothing: what it
        /// STANDS on is the widget the caller read it off (<see cref="Dossier.Carrier"/>), and where
        /// the walk named one the node is drawn by it. A dossier collected with no carrier - one that
        /// only exists inside another tooltip's drawing, where there is no widget of its own to point
        /// at - has nothing on the screen to be asked about and says so.
        /// </summary>
        private static NodeDeclaration Stands(ControlId id, Dossier dossier)
        {
            NodeVtable vtable = Node(dossier);
            return dossier.Carrier == null
                ? (NodeDeclaration)Nodes.Synthetic(id, vtable)
                : Nodes.Drawn(id, vtable, dossier.Carrier);
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
                    GraphNodes.TooltipSection(it.Tooltip, it.Lines)
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
                    new List<AgeTooltip>(1) { it.Tooltip }
                );
            }

            AgeTooltip aim = it.Aim ?? it.Tooltip;
            AgeTransform anchor = it.Anchor ?? (aim == null ? null : aim.AgeTransform);
            if (it.LiveAim != null)
            {
                // The carrier is asked for again at every aim, and the one it was declared with is
                // the fallback: a game that has stopped drawing a widget for this dossier leaves the
                // node pointing where it always did rather than pointing at nothing.
                Func<AgeTooltip> live = it.LiveAim;
                vtable.PointsAt = () => Now(live, aim);
                vtable.OnFocusVisual = () =>
                {
                    AgeTooltip at = Now(live, aim);
                    AgeTransform under = ReferenceEquals(at, aim)
                        ? anchor
                        : (at == null ? null : at.AgeTransform);
                    if (at != null)
                    {
                        PointerFocus.MoveTo(under, at, under);
                    }
                };
                vtable.OnBlurVisual = AgeWidgets.ReleasePointer;
            }
            else if (aim != null)
            {
                vtable.PointsAt = () => aim;
                vtable.OnFocusVisual = () => PointerFocus.MoveTo(anchor, aim, anchor);
                vtable.OnBlurVisual = AgeWidgets.ReleasePointer;
            }

            return vtable;
        }

        /// <summary>Whichever widget is carrying a dossier at this moment, or the one it was declared
        /// with where the caller's own answer has run out.</summary>
        private static AgeTooltip Now(Func<AgeTooltip> live, AgeTooltip declared)
        {
            try
            {
                AgeTooltip found = live == null ? null : live();
                return found ?? declared;
            }
            catch (Exception)
            {
                return declared;
            }
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
        /// <paramref name="anchor"/> is what the tooltip is drawn under,
        /// <paramref name="lines"/> the caller's own reader where it has one, and
        /// <paramref name="live"/> the caller's answer to "which widget carries this NOW" where the
        /// game moves the dossier between widgets (<see cref="Dossier.LiveAim"/>). The tooltip passed
        /// in is still what decides whether the dossier earns a node at all, because that is a
        /// question about the frame the node is declared in.
        /// <paramref name="carrier"/> is for the caller whose drawn HOST is not what the pointer goes
        /// to - a strip scanned for the dossiers inside it (<see cref="AddInside"/>), where the host
        /// is the strip item and the pointer belongs on the tooltip's own widget; it defaults to the
        /// anchor, which is the answer everywhere else.</summary>
        public static void Add(
            List<Dossier> into,
            AgeTooltip tooltip,
            AgeTransform anchor = null,
            Func<IList<string>> lines = null,
            Func<AgeTooltip> live = null,
            AgeTransform carrier = null
        )
        {
            Collect(into, tooltip, anchor, lines, live, carrier ?? anchor);
        }

        /// <summary>
        /// A dossier the surface keeps OFF the screen until a mouse asks for it - collected with no
        /// carrier, so <see cref="Stands"/> declares it <see cref="Nodes.Synthetic"/> and the gate has
        /// nothing to ask.
        ///
        /// The pointer still goes to <paramref name="anchor"/>: the dossier is drawn from that widget
        /// and aiming anywhere else draws another thing. What is withheld is only the CLAIM that the
        /// widget vouches for the node's existence, which for a reveal-on-hover strip it does not - the
        /// keyboard player is precisely the one who never triggers the reveal, so the strip is hidden
        /// at every moment this mod would be asked about it, and a chain test would delete the content
        /// the mod exists to hand over. What says these nodes are real is the WALK that enumerated the
        /// strip's bound icons, the same guarantee every other synthetic node rests on.
        /// </summary>
        public static void AddRevealed(List<Dossier> into, AgeTooltip tooltip, AgeTransform anchor)
        {
            Collect(into, tooltip, anchor, null, null, null);
        }

        private static void Collect(
            List<Dossier> into,
            AgeTooltip tooltip,
            AgeTransform anchor,
            Func<IList<string>> lines,
            Func<AgeTooltip> live,
            AgeTransform carrier
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
            AgeTransform under = anchor ?? tooltip.AgeTransform;
            into.Add(
                new Dossier
                {
                    Name = HoverName(live, tip, under),
                    Tooltip = tip,
                    Anchor = under,
                    Carrier = carrier,
                    Lines = lines,
                    LiveAim = live,
                }
            );
        }

        /// <summary>
        /// What a dossier is CALLED: the words drawn on the widget a mouse would hover to raise it.
        ///
        /// A nested dossier is a hover the keyboard cannot make, so the name has to be the thing the
        /// player would have pointed at - "Role", "Food", the badge beside the row - and the game has
        /// already written that on the screen. The ladder is what is left when it has not:
        ///
        /// - the HOVER TARGET's own drawn words (the tooltip's own widget, read one level down so a
        ///   badge whose caption is a child label answers and a whole card does not),
        /// - the ANCHOR's, for a dossier drawn from a widget of its own that carries no words,
        /// - the wrapper's title (<see cref="AgeWidgets.TooltipTitle"/>), which is where this game
        ///   keeps the name it would have written for a picture,
        /// - the sentence's own first line, which is what a wordless badge with a plain-text
        ///   explanation had before any of this.
        ///
        /// Asked afresh every read, and through <see cref="Now"/>, for the same reason the pointer is:
        /// a widget the game has since re-pointed names another thing entirely.
        /// </summary>
        private static Func<string> HoverName(
            Func<AgeTooltip> live,
            AgeTooltip declared,
            AgeTransform anchor
        )
        {
            Func<AgeTooltip> now = live;
            AgeTooltip it = declared;
            AgeTransform under = anchor;
            return () =>
            {
                AgeTooltip tip = Now(now, it);
                string drawn = Drawn(AgeWidgets.TooltipOwner(tip));
                if (!string.IsNullOrEmpty(drawn))
                {
                    return drawn;
                }

                drawn = Drawn(under);
                if (!string.IsNullOrEmpty(drawn))
                {
                    return drawn;
                }

                string title = AgeWidgets.TooltipTitle(tip);
                return string.IsNullOrEmpty(title) ? CardActions.FirstLine(tip) : title;
            };
        }

        /// <summary>The same ladder for a caller building a <see cref="Dossier"/> by hand, so that a
        /// dossier declared anywhere is named the way one collected here is.</summary>
        public static Func<string> NameOf(AgeTooltip tooltip, AgeTransform anchor)
        {
            return HoverName(null, tooltip, anchor);
        }

        /// <summary>
        /// The tooltips a line carries BESIDES the one it points at, as nodes of their own - for a
        /// line the game drew out of several pieces, each with an explanation of its own.
        ///
        /// A node announces the ONE tooltip a hover on it would raise, which is the last one drawn
        /// (<c>GraphNodes.SectionsFor</c>). That is right, and it used to leave the icon's own sentence
        /// - "The faction of your empire", "The personality of this minor civilization determines how
        /// it reacts to your actions" - reviewable and never said. A sentence the game wrote is not a
        /// footnote: the piece that carries it becomes a node, named the way every other nested entry
        /// is (<see cref="HoverName"/>), and says its sentence when the player steps onto it. Which is
        /// the standing ruling about two hover targets - one row means a row of NODES.
        ///
        /// Only the ones the game wrote as PLAIN TEXT (<see cref="AddPlain"/>'s own test): a
        /// renderer-assembled dossier on a piece of a line has no words until it is drawn, and the
        /// pointer is on the line's own tooltip, so a node for it would promise words nothing raises.
        /// Null when there are none, which is the ordinary line.
        /// </summary>
        public static List<Dossier> Others(IList<AgeTooltip> tooltips)
        {
            List<Dossier> found = null;
            for (int i = 0; tooltips != null && i + 1 < tooltips.Count; i++)
            {
                AgeTooltip tooltip = tooltips[i];
                List<Dossier> into = found ?? new List<Dossier>(1);
                AddPlain(into, tooltip, AgeWidgets.TooltipOwner(tooltip));
                if (into.Count > 0)
                {
                    found = into;
                }
            }

            return found;
        }

        /// <summary>
        /// The renderer-assembled dossiers a line carries BESIDES the one it points at, as nodes of
        /// their own - and, in <paramref name="keeps"/>, the tooltips the LINE goes on carrying.
        ///
        /// <see cref="Others"/> is this for the ones the game wrote as PLAIN TEXT, and it leaves those
        /// on the line as well, because a sentence reads back out of a review buffer whether or not
        /// anything ever drew it. A renderer-assembled dossier does not: its words exist only while the
        /// game is DRAWING it, the pointer is on the line's own tooltip, and a non-last dossier is
        /// therefore a reviewed section that can never fill - promised on arrival and empty forever
        /// (measured on the ground report's species count, 2026-08-28: the Amoeba dossier was declared,
        /// unreachable and silent). So this one MOVES it: the dossier comes off the line's sections and
        /// becomes a child entry that aims the pointer at its own carrier, which is the standing ruling
        /// about two hover targets - one row means a row of NODES.
        ///
        /// A tooltip that does not earn a node (<see cref="Qualifies"/>) stays on the line, so a caller
        /// hands this everything it gathered and loses nothing. The LAST tooltip is always the line's:
        /// it is the one a hover would raise, and that is what the line announces.
        /// </summary>
        public static List<Dossier> Split(IList<AgeTooltip> tooltips, List<AgeTooltip> keeps)
        {
            List<Dossier> found = null;
            for (int i = 0; tooltips != null && i < tooltips.Count; i++)
            {
                AgeTooltip tooltip = tooltips[i];
                if (i + 1 < tooltips.Count)
                {
                    List<Dossier> into = found ?? new List<Dossier>(1);
                    int before = into.Count;
                    Add(into, tooltip, AgeWidgets.TooltipOwner(tooltip));
                    if (into.Count > before)
                    {
                        found = into;
                        continue;
                    }
                }

                if (keeps != null)
                {
                    keeps.Add(tooltip);
                }
            }

            return found;
        }

        /// <summary>
        /// The words a widget draws ON ITSELF - one level down, so the label inside a badge counts and
        /// the rest of the card the badge sits on does not.
        ///
        /// A figure is not words. A hero's mastery line draws "0/11" and a planet's card draws "50",
        /// and a dossier named off those is a node the player cannot tell from its four siblings -
        /// measured live, where two of one card's five figures were both "30". So a drawn string with
        /// no letter in it is not a name and the ladder falls through to the wrapper's title, which is
        /// where this game keeps the name it would have written ("Wit", "Planet Food production").
        /// A name that merely CONTAINS digits is still a name (<see cref="TextUtil.HasLetters"/>).
        /// </summary>
        private static string Drawn(AgeTransform widget)
        {
            try
            {
                // Content read, not existence: a pooled item the game retired is parked at alpha 0
                // with its previous binding's words still on it, and naming a dossier off those words
                // calls it by the last thing the widget held. The same test <see cref="AgeWidgets.ItemText"/>
                // makes for the same reason.
                if (widget == null || widget.Alpha < 0.01f)
                {
                    return null;
                }

                string drawn = AgeWidgets.PaintedText(widget, 1);
                return TextUtil.HasLetters(drawn) ? drawn : null;
            }
            catch (Exception)
            {
                return null;
            }
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
        /// The node is named after the badge the player would have hovered (<see cref="HoverName"/>)
        /// and says the whole sentence, which for a badge the game drew no caption on falls back to
        /// the sentence's own first line as the name - the line the readout's dedupe then keeps out of
        /// the sentence, so nothing is said twice and nothing is lost.
        /// Whether the game is drawing this badge is <see cref="Admitted"/>'s question, asked below on
        /// the widget the node will stand on.
        /// </summary>
        public static void AddPlain(List<Dossier> into, AgeTransform widget)
        {
            if (into != null && widget != null)
            {
                AddPlain(into, AgeWidgets.Raw(widget), widget);
            }
        }

        /// <summary>The same for a sentence the caller has already resolved: the game keeps some of
        /// these on a tooltip FIELD of its own rather than on the widget the sentence is about (the
        /// planet card's improvement box), so there is nothing to read it off.
        /// <paramref name="anchor"/> is what it is drawn under.</summary>
        public static void AddPlain(List<Dossier> into, AgeTooltip tooltip, AgeTransform anchor)
        {
            if (
                into == null
                || tooltip == null
                || AgeWidgets.Readable(tooltip) == null
                || !AgeWidgets.Draws(tooltip)
                || !Admitted(anchor)
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
                    Name = HoverName(null, tip, anchor),
                    Tooltip = tip,
                    Anchor = anchor,
                    Carrier = anchor,
                }
            );
        }

        /// <summary>Every plain-text explanation hanging INSIDE a widget, one node each, in the order
        /// the prefab lays them out - for a row that draws a strip of wordless badges. The paint test
        /// on the CONTAINER stays as flow control: it prunes the whole strip before its tooltips are
        /// resolved, and the dedupe below happens at collection, where no gate can reach.</summary>
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
        /// figures each carry one. The widget scanned is what each of them stands on
        /// (<see cref="Dossier.Carrier"/>): it is the thing the game draws or retires as a whole, and
        /// a tooltip found under it hangs off decoration whose own paint state says nothing.</summary>
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
                Add(into, found[i], null, null, null, widget);
            }
        }

        /// <summary>
        /// Whether this dossier may take a place in a collected list - the ADMISSION filter, asked of
        /// the widget the node will STAND on (<see cref="Dossier.Carrier"/>) so that it is the gate's
        /// own question about the gate's own widget, under the same flag.
        ///
        /// Asked at a collector rather than left to the gate for a reason the gate can never cover:
        /// a collector DEDUPES by tooltip while the dossiers are being COLLECTED, before any node
        /// exists. A retired widget still holding the previous binding's tooltip swallows the drawn
        /// row that shares it, and the gate then drops the one node the pair had left.
        ///
        /// Deliberately NOT applied at <see cref="Add"/>, the shared door: a caller there may be
        /// collecting off a strip the game keeps at alpha 0 on purpose and reveals only under the
        /// mouse (the technology wheel's unlock icons, <c>ResearchScreen.Unlocks</c>), and that
        /// caller's own COUNT decides whether its dot is a branch at all. Filtering those at the door
        /// turned every such dot into a leaf. The question belongs to the collector that knows what
        /// its widgets mean - here, and <c>SystemManagementScreen.AddDepositDossiers</c>.
        ///
        /// A dossier with no carrier is <see cref="Nodes.Synthetic"/> by construction and has nothing
        /// on the screen to ask about, so it passes.
        /// </summary>
        private static bool Admitted(AgeTransform carrier)
        {
            return NodeGate.StillDrawn(carrier);
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

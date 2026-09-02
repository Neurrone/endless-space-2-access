using System;
using System.Collections.Generic;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    public static partial class AgeWidgets
    {
        /// <summary>
        /// A widget's tooltip whatever kind it is - what a caller needs to SHOW one, and only that.
        ///
        /// For POINTING. A reading asks <see cref="EffectiveTooltips"/> instead, even when it wants
        /// nothing but this widget's own (<see cref="TooltipReach.Own"/>): the game hangs its
        /// explanations on the block around a row and on the icon beside a number as readily as on
        /// the widget itself, and a screen that reaches for this one to build its SECTIONS is a
        /// screen whose reach can never be widened without finding every such call again.
        /// </summary>
        public static AgeTooltip Raw(AgeTransform transform)
        {
            try
            {
                return transform == null ? null : transform.AgeTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// A tooltip only if its words are on the widget: no class at all, or the "Simple" class, which
        /// is the plain text box and renders exactly what Content says. Every other class is assembled
        /// by a renderer at draw time and its content field holds authoring leftovers, so there is
        /// nothing there to read; <see cref="TooltipLines"/> reads those off the drawn window instead.
        ///
        /// This is the SAME question <c>GraphNodes.ModeFor</c> asks to pick a tooltip's mode, and it is
        /// answered in one place: two copies of it disagreed about "Simple" for a while, which is how a
        /// row came to announce its tooltip from the widget and review it from a window that had not
        /// been drawn yet.
        /// </summary>
        public static AgeTooltip Readable(AgeTooltip tooltip)
        {
            try
            {
                if (tooltip == null)
                {
                    return null;
                }

                string cls = tooltip.Class;
                return string.IsNullOrEmpty(cls) || cls == "Simple" ? tooltip : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether the game would draw anything at all for this tooltip: it has words of its own, or a
        /// target the renderer can assemble words from.
        ///
        /// This is <c>GuiTooltipController.ReadTooltipInformation</c>'s own test, and it is the one
        /// place it is asked, because the two things that ask it must never disagree: the pointer aims
        /// at a tooltip only if it would draw (<see cref="PointerFocus"/>), and a section only counts
        /// as tooltip content to review for one that would draw (<c>NodeSection.Indicates</c>). A
        /// prefab that hangs an empty tooltip on decoration - a turn counter's, with no class content
        /// and no target - fails it, and sending the player to an empty review buffer is what taking
        /// its word for it used to cost.
        ///
        /// Asked every frame rather than when a node is declared: a widget the game has not filled in
        /// yet starts drawing the moment it is filled.
        /// </summary>
        public static bool Draws(AgeTooltip tooltip)
        {
            try
            {
                return tooltip != null
                    && (!string.IsNullOrEmpty(tooltip.Content) || tooltip.Target != null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether the game could never draw anything for this tooltip, whatever happened next: it has
        /// no words of its own, nothing for a renderer to assemble words from, and no class naming a
        /// renderer at all. Prefabs hang these on decoration - the picture inside a table line carries
        /// one with all three empty - and a reading that picks one up aims the pointer at a tooltip the
        /// game draws nothing for, while a real one hanging beside it is never shown.
        ///
        /// Not the opposite of <see cref="Draws"/>. That is the question of whether the game would draw
        /// this tooltip NOW, asked every frame, and a class-backed tooltip the game has not filled in
        /// yet answers no to it and yes the moment it is filled. This one is about a tooltip that names
        /// no renderer at all, so there is nothing left for the game to fill in.
        /// </summary>
        public static bool NeverDraws(AgeTooltip tooltip)
        {
            try
            {
                return tooltip != null
                    && string.IsNullOrEmpty(tooltip.Class)
                    && string.IsNullOrEmpty(tooltip.Content)
                    && tooltip.Target == null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>What the player would read on a tooltip, resolved when they ask to read it - off
        /// the widget when the words are there, off the drawn tooltip window when they are not.
        /// </summary>
        public static Func<IList<string>> TooltipLines(AgeTooltip tooltip)
        {
            if (tooltip == null)
            {
                return null;
            }

            AgeTooltip it = tooltip;
            return () =>
                Readable(it) != null
                    ? AgeText.ContentLines(it)
                    : DrawnTooltip.Lines(it);
        }

        /// <summary>
        /// What the player would read on a widget's own tooltip while the game is DRAWING that
        /// widget - <see cref="TooltipLines"/> resolved and asked, with the two answers a caller
        /// gathering lines has no use for spelling out: a widget carrying no tooltip, and a widget
        /// the game is not drawing.
        ///
        /// The drawn test is folded in because a tooltip outlives whatever put its widget away: an
        /// icon the panel has hidden still carries the words it was last bound with, and an
        /// unguarded read hands the player a sentence about something that is not on the screen.
        ///
        /// Never null - these lines are appended to a reading, and an undrawn widget contributes
        /// nothing rather than a hole the caller has to test for.
        /// </summary>
        public static IList<string> DrawnTooltipLines(AgeTransform widget)
        {
            try
            {
                if (widget == null || !Visible(widget))
                {
                    return NoLines;
                }

                Func<IList<string>> read = TooltipLines(Raw(widget));
                IList<string> lines = read == null ? null : read();
                return lines ?? NoLines;
            }
            catch (Exception)
            {
                return NoLines;
            }
        }

        // Fixed-length rather than an empty List, so the one instance every silent answer shares
        // cannot be added to by a caller that mistakes it for its own.
        private static readonly IList<string> NoLines = new string[0];

        /// <summary>What the game calls the thing a tooltip is about. A control drawn as a bare symbol
        /// and a number - a population unit, a party's seat count - writes no words of its own, and the
        /// wrapper the game hangs on the tooltip is where it keeps the name it would have written.
        /// </summary>
        public static string TooltipTitle(AgeTooltip tooltip)
        {
            try
            {
                GuiWrapper wrapper = tooltip == null ? null : tooltip.Target as GuiWrapper;
                return wrapper == null ? null : AgeText.Clean(wrapper.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Every tooltip the game hung inside a widget, in the order it drew them: the widget's own
        /// first, then its children's, left to right as the prefab lays them out.
        ///
        /// For a line the mod reads as ONE thing that the game built out of several pieces - an icon
        /// captioning a label, each with its own explanation - the line is the only place any of those
        /// explanations is reachable from, so it carries all of them. Asking the container alone finds
        /// nothing when the words hang on the piece inside it, and the row goes on offering a review
        /// buffer that stays empty (see <see cref="PointAt(NodeVtable, AgeTransform, AgeTooltip)"/>
        /// for the other half of that failure).
        ///
        /// A tooltip the game could never draw anything for (<see cref="NeverDraws"/>) is not one of
        /// them: callers take the LAST tooltip found here as the one to point at, and a prefab's empty
        /// decoration tooltip sitting after the real one is how a row came to promise a dossier and
        /// draw nothing.
        /// </summary>
        public static void Tooltips(AgeTransform widget, List<AgeTooltip> into, int maxDepth = 4)
        {
            EffectiveTooltips(widget, into, TooltipReach.Own | TooltipReach.Descendants, maxDepth);
        }

        /// <summary>The same gathering for a line whose pieces come out of a POOL, leaving out the
        /// pieces the game retired by fading them (<see cref="TooltipReach.Painted"/>) - the ship
        /// design costs box, where the group kept for a strategic-resource row still carries the
        /// Adamantian banner of a design that costs no strategic resource.</summary>
        public static void PaintedTooltips(
            AgeTransform widget,
            List<AgeTooltip> into,
            int maxDepth = 4
        )
        {
            EffectiveTooltips(
                widget,
                into,
                TooltipReach.Own | TooltipReach.Descendants | TooltipReach.Painted,
                maxDepth
            );
        }

        /// <summary>
        /// THE tooltip resolver: every tooltip that belongs to this widget, in the directions the
        /// caller asks for and in the order the player reads them - the block it was drawn in, the
        /// captions beside it, its own, then the pieces inside it.
        ///
        /// One resolver rather than one per screen. There were four (this file's own walk, two
        /// private copies in screens, and a per-widget <see cref="Raw"/>), they disagreed about the
        /// visibility gate, about empty decoration tooltips and about whether a clone counted twice,
        /// and every disagreement was invisible in speech: a row simply said less than the game does.
        ///
        /// <paramref name="reach"/> is opt-in per call site and defaults to nothing, because each
        /// direction is a way to pick up a tooltip that belongs to something else. See
        /// <see cref="TooltipReach"/> for what each one means; <paramref name="maxDepth"/> bounds the
        /// walking ones.
        ///
        /// Two rules the callers depend on:
        /// - <b>Identity is the (class, content, target) triple, not the component</b>
        ///   (<see cref="TooltipKey"/>): the game CLONES tooltips onto the widgets inside a card, and
        ///   a reference dedupe reads one explanation once per clone.
        /// - <b>A resolution that can return SEVERAL tooltips filters them</b> - each widget it walks
        ///   must be visible, and a tooltip the game could never draw anything for
        ///   (<see cref="NeverDraws"/>) is dropped, because callers point at the LAST one found and a
        ///   prefab's empty decoration sitting after the real one is how a row came to promise a
        ///   dossier and draw nothing. A resolution of exactly ONE (<see cref="TooltipReach.Own"/> or
        ///   <see cref="TooltipReach.ListEntry"/> alone) does not filter: there is nothing to choose
        ///   between, and the caller named the widget whose tooltip it wants.
        ///
        /// Appends, so a caller may resolve several widgets into one list; anything already in
        /// <paramref name="into"/> counts for the dedupe.
        /// </summary>
        public static void EffectiveTooltips(
            AgeTransform widget,
            List<AgeTooltip> into,
            TooltipReach reach,
            int maxDepth = 4
        )
        {
            if (widget == null || into == null || reach == TooltipReach.None)
            {
                return;
            }

            Seen.Clear();
            for (int i = 0; i < into.Count; i++)
            {
                Seen.Add(KeyOf(into[i]));
            }

            bool walks =
                (reach & (TooltipReach.Descendants | TooltipReach.Parents | TooltipReach.Siblings))
                != 0;
            if ((reach & TooltipReach.Parents) != 0)
            {
                CollectNearestAncestor(widget, into, maxDepth);
            }

            if ((reach & TooltipReach.Siblings) != 0)
            {
                CollectCaptionSiblings(widget, into);
            }

            if ((reach & TooltipReach.Descendants) != 0)
            {
                Descend(
                    widget,
                    into,
                    0,
                    maxDepth,
                    (reach & TooltipReach.Own) != 0,
                    (reach & TooltipReach.Painted) != 0
                );
                return;
            }

            if ((reach & (TooltipReach.Own | TooltipReach.ListEntry)) != 0)
            {
                Keep(Raw(widget), into, walks);
            }
        }

        /// <summary>Whether this widget draws words of its own - the raw field, not the cleaned
        /// reading, because the question is "is this a caption picture or another cell" and the
        /// answer must cost one component read.</summary>
        private static bool Writes(AgeTransform widget)
        {
            try
            {
                AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
                return label != null && !string.IsNullOrEmpty(label.Text);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether two tooltips would draw the same thing - the resolver's own identity
        /// question, for a caller holding one tooltip that has to find itself in a resolved list.
        /// </summary>
        public static bool SameTooltip(AgeTooltip a, AgeTooltip b)
        {
            return ReferenceEquals(a, b) || (a != null && b != null && KeyOf(a).Equals(KeyOf(b)));
        }

        // Reused rather than allocated per call: the resolver runs inside per-frame panel walks, and
        // nothing it calls can re-enter it.
        private static readonly TooltipSet Seen = new TooltipSet();

        private static TooltipKey KeyOf(AgeTooltip tooltip)
        {
            try
            {
                return tooltip == null
                    ? new TooltipKey(null, null, null)
                    : new TooltipKey(tooltip.Class, tooltip.Content, tooltip.Target);
            }
            catch (Exception)
            {
                return new TooltipKey(null, null, null);
            }
        }

        private static void Keep(AgeTooltip tooltip, List<AgeTooltip> into, bool filter)
        {
            if (tooltip == null || (filter && NeverDraws(tooltip)))
            {
                return;
            }

            if (Seen.Add(KeyOf(tooltip)))
            {
                into.Add(tooltip);
            }
        }

        private static void Descend(
            AgeTransform widget,
            List<AgeTooltip> into,
            int depth,
            int maxDepth,
            bool includeSelf,
            bool paintedOnly
        )
        {
            if (widget == null || depth > maxDepth || !Visible(widget))
            {
                return;
            }

            if (includeSelf)
            {
                Keep(Raw(widget), into, true);
            }

            IList<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = paintedOnly ? DrawnChild(children, i) : children[i];
                if (child != null)
                {
                    Descend(child, into, depth + 1, maxDepth, true, paintedOnly);
                }
            }
        }

        /// <summary>The tooltip on the block this widget was drawn in. The game writes these with
        /// <c>GetComponentInParent</c>, which takes the nearest ancestor carrying one and stops -
        /// so this stops there too, rather than collecting every container up to the window and
        /// hanging a panel-wide sentence on each of its figures.</summary>
        private static void CollectNearestAncestor(
            AgeTransform widget,
            List<AgeTooltip> into,
            int maxDepth
        )
        {
            AgeTransform at = widget == null ? null : widget.Parent;
            for (int depth = 0; at != null && depth < maxDepth; depth++)
            {
                AgeTooltip tooltip = Raw(at);
                if (tooltip != null && !NeverDraws(tooltip) && Visible(at))
                {
                    Keep(tooltip, into, true);
                    return;
                }

                at = at.Parent;
            }
        }

        /// <summary>
        /// The explanation the game hung on the wordless icon BESIDE this widget - the caption for a
        /// value drawn as a bare number.
        ///
        /// Only a sibling that draws no words of its own and works nothing counts. A sibling with
        /// text is another cell of the same row and carries its own explanation, which belongs to
        /// that cell and not to this one; a sibling with a control is a thing the player can operate
        /// and gets a node rather than a caption. Without that test a strip of stats drawn as
        /// several labels in one group reads every stat's sentence on every stat.
        ///
        /// Both tests are one component read on the sibling itself - never a walk of what is inside
        /// it. This runs per stat per frame inside a panel build, and the deep text reading it would
        /// otherwise do is the kind of cost that only shows up as a frame-rate complaint.
        /// </summary>
        private static void CollectCaptionSiblings(AgeTransform widget, List<AgeTooltip> into)
        {
            AgeTransform parent = widget == null ? null : widget.Parent;
            IList<AgeTransform> siblings = parent == null ? null : parent.Children;
            for (int i = 0; siblings != null && i < siblings.Count; i++)
            {
                AgeTransform sibling = siblings[i];
                if (sibling == null || ReferenceEquals(sibling, widget) || !Visible(sibling))
                {
                    continue;
                }

                AgeTooltip tooltip = Raw(sibling);
                if (tooltip == null || NeverDraws(tooltip))
                {
                    continue;
                }

                if (Control(sibling) != null || Writes(sibling))
                {
                    continue;
                }

                Keep(tooltip, into, true);
            }
        }

        /// <summary>Make a control look hovered while the cursor is on it, and show its tooltip.
        /// </summary>
        public static void Point(NodeVtable vtable, AgeControlButton button)
        {
            AgeControlButton it = button;
            vtable.OnFocusVisual = () =>
                PointerFocus.MoveTo(it, Raw(Transform(it)), Transform(it));
            vtable.OnBlurVisual = ReleasePointer;
            vtable.PointsAt = () => Raw(Transform(it));
        }

        /// <summary>The same for a control whose tooltip the game hangs somewhere other than on the
        /// button - a line of text inside a button stretched across a whole banner, a strip item whose
        /// tooltip lives on the row. <paramref name="under"/> is what the tooltip is drawn beneath.
        /// </summary>
        public static void Point(
            NodeVtable vtable,
            AgeControlButton button,
            AgeTooltip tooltip,
            AgeTransform under
        )
        {
            AgeControlButton it = button;
            AgeTooltip tip = tooltip;
            AgeTransform anchor = under;
            vtable.OnFocusVisual = () => PointerFocus.MoveTo(it, tip, anchor);
            vtable.OnBlurVisual = ReleasePointer;
            vtable.PointsAt = () => tip;
        }

        /// <summary>The same for a control the game drew as a toggle - a card in a set the player picks
        /// one of. The highlight is the toggle's own hover state rather than a button's, because a
        /// toggle has no <c>SimulateHover</c> and the button the game parks inside the card for its
        /// artwork is wired to nothing (measured: hovering it changes no pixel).</summary>
        public static void Point(NodeVtable vtable, AgeControlToggle toggle)
        {
            AgeControlToggle it = toggle;
            vtable.OnFocusVisual = () =>
                PointerFocus.MoveToToggle(it, Raw(Transform(it)), Transform(it));
            vtable.OnBlurVisual = ReleasePointer;
            vtable.PointsAt = () => Raw(Transform(it));
        }

        /// <summary>The same for a toggle whose tooltip the game hangs somewhere other than on the
        /// toggle - an action item that shows a button or a tick depending on what the action is, and
        /// keeps the one tooltip on the item that holds both.</summary>
        public static void Point(
            NodeVtable vtable,
            AgeControlToggle toggle,
            AgeTooltip tooltip,
            AgeTransform under
        )
        {
            AgeControlToggle it = toggle;
            AgeTooltip tip = tooltip;
            AgeTransform anchor = under;
            vtable.OnFocusVisual = () => PointerFocus.MoveToToggle(it, tip, anchor);
            vtable.OnBlurVisual = ReleasePointer;
            vtable.PointsAt = () => tip;
        }

        /// <summary>The same for a widget with no button under it - a readout, an icon. Nothing lights
        /// up because there is nothing there to light, and the tooltip appears, which for these is the
        /// whole of what the pointer was ever for.</summary>
        public static void PointAt(NodeVtable vtable, AgeTransform widget)
        {
            AgeTransform it = widget;
            vtable.OnFocusVisual = () => PointerFocus.MoveTo(Button(it), Raw(it), it);
            vtable.OnBlurVisual = ReleasePointer;
            vtable.PointsAt = () => Raw(it);
        }

        /// <summary>
        /// The same for a control whose tooltip the game hung somewhere OTHER than on the widget the node
        /// is read off - a population entry whose dossier is on the symbol inside it, a card row whose
        /// anomaly dossier is on the title inside it. Pass the tooltip the node was DECLARED with:
        /// pointing at the widget aims at the widget's own tooltip, so where that is null the game draws
        /// nothing at all and the review buffer stays empty - the tooltip's words only exist once it
        /// is drawn.
        ///
        /// The widget is the fallback, so a control with no tooltip anywhere is still pointed at and
        /// anything hoverable under it still lights up.
        ///
        /// It replaced three private per-screen copies of the same aim, which is why every screen
        /// with a tooltip resolved from elsewhere hands it in here rather than re-deriving one.
        /// </summary>
        public static void PointAt(NodeVtable vtable, AgeTransform widget, AgeTooltip tooltip)
        {
            PointAt(vtable, TooltipOwner(tooltip) ?? widget);
        }

        /// <summary>
        /// The same for a tooltip the game hangs on NO widget of its own - a field of a window the game
        /// fills at bind time - which is therefore not findable from the widget it is drawn under.
        ///
        /// <see cref="PointAt(NodeVtable, AgeTransform, AgeTooltip)"/> asks the widget for its OWN
        /// tooltip, which for this shape answers something else or nothing; this one aims at the tooltip
        /// it was handed and puts the pointer on <paramref name="anchor"/>, which is the widget the game
        /// drew the tooltip's own words and picture in. Reached through
        /// <see cref="GraphNodes.Aim"/>, which is what decides that a tooltip is of this shape - a
        /// caller never chooses between the two.
        /// </summary>
        public static void PointUnder(NodeVtable vtable, AgeTransform anchor, AgeTooltip tooltip)
        {
            AgeTransform under = anchor;
            AgeTooltip tip = tooltip;
            vtable.OnFocusVisual = () => PointerFocus.MoveTo(Button(under), tip, under);
            vtable.OnBlurVisual = ReleasePointer;
            vtable.PointsAt = () => tip;
        }

        /// <summary>Which tooltip a node's pointer goes to, as the node itself declares it
        /// (<see cref="NodeVtable.PointsAt"/>, written by the pointing helpers above from the same
        /// argument they aim). Never re-derived from the widget tree: the deepest tooltip inside a
        /// card is often decoration, and a second opinion that picked it reported a defect on screens
        /// whose pointing was right all along.</summary>
        public static AgeTooltip AimOf(NodeVtable vtable)
        {
            Func<object> at = vtable == null ? null : vtable.PointsAt;
            try
            {
                return at == null ? null : at() as AgeTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The widget a tooltip is hung on, which is the one the game draws it for.</summary>
        public static AgeTransform TooltipOwner(AgeTooltip tooltip)
        {
            try
            {
                return tooltip == null ? null : tooltip.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static readonly Action ReleasePointer = PointerFocus.Release;
    }
}

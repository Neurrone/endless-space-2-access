using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// The questions every screen asks of an AGE widget: can the player see it, is it refusing, what
    /// does its tooltip say, and how do I work it without a mouse.
    ///
    /// These were written once per screen until there were three of them. They are here rather than on
    /// <see cref="GraphNodes"/> because they are about the game's widget toolkit, not about how a
    /// control reads aloud.
    /// </summary>
    public static class AgeWidgets
    {
        /// <summary>How far up a parent chain to look before deciding it is not a chain.</summary>
        private const int MaxAncestors = 64;

        public static AgeTransform Transform(AgeControl control)
        {
            try
            {
                return control == null ? null : control.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether a ROOT is switched on - a screen's window, a panel, the group a walk is about to
        /// descend into. A control inside a group the window has collapsed is still marked visible
        /// itself, so the chain above it is what says whether the player can see it.
        ///
        /// Roots ONLY. Asked of a CHILD it is the wrong question twice over: it ignores alpha, so it
        /// says yes to a pooled table's retired row, and it walks the ancestry a walk descending from a
        /// trusted root has already vouched for - which reads a whole window as blank for the length of
        /// its arrival fade. A walk asks <see cref="DrawnChild"/> of the children it is stepping
        /// through, and this of the root it started from.
        /// </summary>
        public static bool Visible(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    if (!at.Visible)
                    {
                        return false;
                    }

                    at = at.Parent;
                }

                return widget != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether a row of a POOLED table is really on the screen.
        ///
        /// A table the game fills with <c>ReserveChildren</c> + <c>RefreshChildrenIList</c> never
        /// shrinks: the engine grows the pool to the largest list it has ever shown and, for a table
        /// whose <c>StrictVisibility</c> is off, retires the leftovers by setting their ALPHA to zero
        /// rather than hiding them (<c>firstpass/AgeTransform.cs:2382-2412</c>). A retired row keeps its
        /// old words, its old position and <c>Visible == true</c>, so <see cref="Visible"/> says yes to
        /// something the player cannot see - measured as 32 dead "Empty law slot" stops parked outside
        /// the laws grid and as a second effects line on a battle-tactics slot.
        ///
        /// Alpha is not inherited by the visibility test, so the ancestors are walked too: a retired
        /// BLOCK's own lines each sit at alpha 1 inside it. A fade the game is animating reads as
        /// unpainted while it is transparent, which is the same answer <see cref="Visible"/> gives for a
        /// group mid-collapse and is why this is asked of pooled tables rather than of everything.
        /// </summary>
        public static bool Painted(AgeTransform widget)
        {
            try
            {
                if (widget == null || !Visible(widget))
                {
                    return false;
                }

                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    if (at.Alpha <= 0f)
                    {
                        return false;
                    }

                    at = at.Parent;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether the RENDERER is drawing this widget - the engine's own answer, which is the
        /// early-out its render pass prunes a whole subtree with
        /// (<c>firstpass/AgeTransform.cs:1955</c>, <c>PrimitiveUpdateGUI</c>): visible, and not faded
        /// to nothing. <c>StrictVisibility</c> is no exemption - that flag only tells the ARRANGER to
        /// keep counting a faded child's slot (<c>GetVisibleChildrenCount</c>); the renderer skips it
        /// all the same. Exempting strict tables here declared the narrative event's retired choice
        /// cards - faded away under a strict table once the choice was made - as live radio buttons.
        ///
        /// This is the ONE-STEP form of <see cref="Painted"/>, for a walk that descends from a root it
        /// already trusts. Such a walk never asks the root's own alpha, which matters wherever the
        /// root is itself animating - a popup fades ITSELF in on arrival while every child stays at
        /// alpha 1, and a walk that asked the root would read the whole window as blank for the
        /// length of that animation.
        /// </summary>
        public static bool Paints(AgeTransform child)
        {
            try
            {
                return child != null && child.Visible && child.Alpha > 0f;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The child at <paramref name="index"/> of a container the walk already TRUSTS, or null where
        /// the renderer is not drawing it - the one blessed way to step through
        /// <c>AgeTransform.Children</c>.
        ///
        /// The test is <see cref="Paints"/>, one step, because that is the question a child raises: a
        /// pooled table retires a surplus row by fading it to nothing while leaving it
        /// <c>Visible</c> and still holding the previous binding's words, and a walk that asked
        /// <see cref="Visible"/> instead declared four separate ghosts before this existed. The
        /// ancestry is deliberately not re-asked: the walk entered through a root it gated on
        /// <see cref="Visible"/>, and re-asking it blanks every window that fades itself in on arrival
        /// while its children stay at alpha 1. Entry gates on ROOTS therefore stay
        /// <see cref="Visible"/>; everything below them comes through here.
        ///
        /// The INDEX is the caller's, not a compacted one, so a node keyed by its position in the
        /// drawn tree keeps that key whether or not the pool is holding ghosts either side of it; and
        /// the caller binds <c>Children</c> once and walks it, so nothing is allocated per frame.
        /// </summary>
        public static AgeTransform DrawnChild(IList<AgeTransform> children, int index)
        {
            if (children == null || index < 0 || index >= children.Count)
            {
                return null;
            }

            AgeTransform child = children[index];
            return Paints(child) ? child : null;
        }

        /// <summary>
        /// The children of a container the game is DRAWING, or null where it is not - the entry gate
        /// for a walk that then steps through them with <see cref="DrawnChild"/>.
        ///
        /// The test is the ancestry <see cref="Visible"/> and deliberately NOT <see cref="Painted"/>:
        /// a table fading IN still has content, and asking its alpha reads a whole panel as empty for
        /// the length of its arrival animation. Alpha is the child's question, and
        /// <see cref="DrawnChild"/> asks it one step at a time.
        ///
        /// The test is folded in because a table the window has put away keeps every row it last
        /// bound, each still marked visible itself - so a walk that entered ungated reads the previous
        /// binding's rows aloud, and pays a text walk per row to do it. null rather than an empty list
        /// so the caller's own loop condition is the whole of the gate.
        /// </summary>
        public static IList<AgeTransform> DrawnChildren(AgeTransform table)
        {
            try
            {
                return table == null || !Visible(table) ? null : table.Children;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool Enabled(AgeTransform widget)
        {
            try
            {
                return widget != null && widget.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether the widget and everything it sits inside are switched on, since a panel the
        /// game has disabled kills the whole subtree under it. This is the DRAWN state - right for
        /// "is the game showing this at all" and for a whole window's arrival gate. A node's
        /// availability asks <see cref="Offered"/> instead, because a button this game is refusing can
        /// still have its enable flag set.</summary>
        public static bool Operable(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    if (!at.Enable)
                    {
                        return false;
                    }

                    at = at.Parent;
                }

                return widget != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether the game is OFFERING this control - <see cref="Operable"/> plus the game's own test
        /// for a button it has left switched ON only so that clicking it can explain itself.
        ///
        /// <c>Gui.FormatButtonHint</c> (<c>Gui.cs:1150-1204</c>) answers a missing-technology failure by
        /// setting <c>Enable = true</c> and the DRAWN alpha to the engine's disabled fade, so a Ctrl+click
        /// can jump to the technology in the research tree; the control's own handler then asks
        /// <c>Gui.IsHintActive</c> (<c>Gui.cs:1249-1260</c>) first and returns without doing its job
        /// (<c>PlanetCard.OnColonizeCb</c> :764-770, <c>FleetActionButton.OnClickCb</c> :20-30). So the
        /// enable flag says available, the pixels say unavailable, and pressing it does nothing: this is
        /// the question a node's availability has to ask, and <see cref="Operable"/> alone cannot answer
        /// it. Sixteen prefabs use the trick, and where the game writes its own <c>Enable = false</c>
        /// AFTER the hint call the extra test simply agrees.
        ///
        /// Availability, never visibility: a hinted control stays DECLARED and refuses with the game's
        /// own reason (which the hint has already appended to its tooltip) rather than disappearing. So
        /// <see cref="Operable"/> keeps its narrower meaning for the gates that decide whether a widget
        /// is drawn at all, and for whole windows.
        /// </summary>
        public static bool Offered(AgeTransform widget)
        {
            try
            {
                return Operable(widget) && !Gui.IsHintActive(widget);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether this control is carrying the missing-technology hint described on
        /// <see cref="Offered"/> - the trick that makes a control read as unavailable, and the one thing
        /// such a control still DOES.</summary>
        public static bool Hinted(AgeTransform widget)
        {
            try
            {
                return Gui.IsHintActive(widget);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Run the hint's own jump: the technology screen opens focused on the technology this control
        /// is missing.
        ///
        /// The game gates it on the modifier the player is PHYSICALLY holding
        /// (<c>GuiButtonHint.ActivateHint</c> :18-34 reads <c>Input.GetKey(LeftControl)</c>), so with the
        /// modifier unheld this refuses and nothing happens - the same answer the mouse gets for a plain
        /// click, and the reason the mod replays the gesture instead of reimplementing the jump.
        /// </summary>
        public static void Locate(AgeTransform widget)
        {
            try
            {
                Gui.ActivateHint(widget);
            }
            catch (Exception e)
            {
                Log.Warn("widgets: locating a hinted technology threw: " + e);
            }
        }

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
                    ? AgeText.Lines(AgeText.Tooltip(it))
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

        /// <summary>Press a control the way the engine presses it: every AGE control carries the object
        /// and the method name its own mouse handler sends to, so replaying that pair runs the window's
        /// own handler with no click that could land on whatever the mouse is over.</summary>
        public static void Press(AgeControlButton button)
        {
            if (button == null)
            {
                return;
            }

            try
            {
                Click(Transform(button));
                Send(button.OnActivateObject, button.OnActivateMethod, button.gameObject);
            }
            catch (Exception e)
            {
                Log.Warn("widgets: pressing a control threw: " + e);
            }
        }

        /// <summary>The same for a control the game hangs on a plain transform rather than exposing as
        /// a button field.</summary>
        public static void Press(AgeTransform widget)
        {
            Press(Button(widget));
        }

        /// <summary>
        /// Press a control the way the ENGINE presses it: the control's own wiring, and then the wiring
        /// of every control it sits INSIDE.
        ///
        /// One control is ever the mouse's hit target (<c>AgeTransform.UpdateInteractivity</c>,
        /// <c>firstpass/AgeTransform.cs:3446-3502</c>), and the way a nested control's parent also acts
        /// on the same click is propagation: <c>AgeControlButton.MouseUp</c>
        /// (<c>firstpass/AgeControlButton.cs:245-270</c>) and <c>AgeControlToggle.MouseUp</c>
        /// (<c>:149-181</c>) handle the press and then call <c>base</c>, which walks to the nearest
        /// ancestor <c>AgeControl</c> and re-delivers the event to it (<c>AgeControl.MouseUp</c>
        /// <c>:170-192</c>, <c>FindParentControl</c> <c>:231-249</c>), gated on the CHILD's own
        /// <c>PropagateInteraction</c> - which defaults true (<c>firstpass/AgeControl.cs:19</c>).
        ///
        /// <see cref="Press"/> replays one control's handler and stops, which is right for a button
        /// standing on its own and WRONG wherever the game's design is the two-step: a table cell's own
        /// button records which cell was clicked (<c>GuiTableCell.OnClickCb</c> -&gt;
        /// <c>GuiTableLine.OnCellClick</c>, <c>GuiTableLine.cs:216-219</c>) and does nothing else, and
        /// what opens the panel the cell stands for is the ROW's toggle firing next
        /// (<c>GuiTableLine.OnLineSelectionCb</c> -&gt; the client's <c>OnLineSelection</c>, which reads
        /// <c>ClickedCell</c> and then clears it). Press the cell alone and the click is recorded and
        /// never acted on; press it here and the player gets the one gesture the mouse has.
        ///
        /// Two deliberate asymmetries with <see cref="Press"/>, both mirroring the engine:
        /// the click SOUND is played only for the control the player aimed at, because the engine
        /// delivers <c>MouseUp</c> to the hit target's GameObject by <c>SendMessage</c> (which reaches
        /// its <c>AgeAudio</c> too, <c>AgeManager.cs:890</c>) and reaches every ancestor by a plain C#
        /// call on the control alone; and an ancestor's activation honours its <c>UseLeftClick</c> flag,
        /// which is the test <c>HandleMouseUpOrDown</c> itself applies. A double click is never
        /// synthesized - one press is one click.
        ///
        /// A control kind with no click wiring of its own (a scroll view, a drop list) is stepped
        /// THROUGH rather than stopped at, which is again the engine: <c>AgeControl.MouseUp</c>'s
        /// default body is the propagation and nothing else.
        /// </summary>
        public static void PressPropagating(AgeControl control)
        {
            if (control == null)
            {
                return;
            }

            try
            {
                AgeControlToggle toggle = control as AgeControlToggle;
                if (toggle != null)
                {
                    Toggle(toggle);
                }
                else
                {
                    AgeControlButton button = control as AgeControlButton;
                    if (button != null)
                    {
                        Press(button);
                    }
                    else
                    {
                        Click(Transform(control));
                    }
                }

                AgeControl at = control;
                for (int depth = 0; depth < MaxAncestors; depth++)
                {
                    if (!Propagates(at))
                    {
                        return;
                    }

                    AgeControl parent = ParentControl(Transform(at));
                    if (parent == null)
                    {
                        return;
                    }

                    FireAncestor(parent);
                    at = parent;
                }
            }
            catch (Exception e)
            {
                Log.Warn("widgets: pressing a control and its ancestors threw: " + e);
            }
        }

        /// <summary>The same for a control the game hangs on a plain transform.</summary>
        public static void PressPropagating(AgeTransform widget)
        {
            PressPropagating(Control(widget));
        }

        /// <summary>
        /// Run the OTHER handler a control carries: the one its own second click inside the double-click
        /// window would run (<c>AgeControlButton.HandleMouseUpOrDown</c>,
        /// <c>firstpass/AgeControlButton.cs:336-338</c>). Nothing at all where the control was not wired
        /// for it, which is how a table that leaves its double click unwired stays a single-gesture list.
        ///
        /// It goes through the same arity-resolving dispatch <see cref="Press"/> uses, and that is not a
        /// nicety here: the engine sends this one with the sender as an argument, while the handler these
        /// tables name (<c>GuiTableLine.OnLineDoubleClickCb</c>, <c>GuiTableLine.cs:211</c>) takes none.
        /// The dispatch matches the overload the handler actually has, which is why replaying it works.
        /// </summary>
        public static void DoubleClick(AgeControlButton button)
        {
            if (button == null)
            {
                return;
            }

            try
            {
                if (!button.UseDoubleClick)
                {
                    return;
                }

                Send(button.OnDoubleClickObject, button.OnDoubleClickMethod, button.gameObject);
            }
            catch (Exception e)
            {
                Log.Warn("widgets: replaying a double click threw: " + e);
            }
        }

        /// <summary>
        /// The same for a control the game drew as a TOGGLE, which carries its own copy of the three
        /// double-click fields (<c>firstpass/AgeControlToggle.cs:19-23,207-209</c>) rather than
        /// inheriting the button's. Every list this game draws out of tiles rather than table lines
        /// picks one up this way - a ship tile's tick, an event popup's choice - so the gesture is
        /// replayed off the tick, and the handler behind it does its own selecting
        /// (<c>ShipItem.OnDoubleClickCb</c> :190-192 sets the tick itself).
        /// </summary>
        public static void DoubleClick(AgeControlToggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            try
            {
                if (!toggle.UseDoubleClick)
                {
                    return;
                }

                Send(toggle.OnDoubleClickObject, toggle.OnDoubleClickMethod, toggle.gameObject);
            }
            catch (Exception e)
            {
                Log.Warn("widgets: replaying a toggle's double click threw: " + e);
            }
        }

        /// <summary>The control sitting on a transform, whatever kind it is.</summary>
        public static AgeControl Control(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.AgeControl;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where this widget sits among its siblings - the stable half of a pooled prefab clone's
        /// identity, since a set of clones can share one name and a position in a collected list moves the
        /// moment a sibling appears or goes. The repeated-node key rule
        /// (<c>docs/dev-loop.md</c>) is built out of this.</summary>
        public static int IndexInParent(AgeTransform widget)
        {
            try
            {
                return widget == null ? 0 : widget.transform.GetSiblingIndex();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Where a widget is DRAWN, for a question about layout: itself, unless the game shows it
        /// through a scrolling window that cuts part of it off, in which case the window is.
        ///
        /// A widget the window cuts off keeps its whole rectangle, and the part that does not fit
        /// hangs invisibly across whatever is drawn above, below and beside it. The quest popup writes
        /// its lore as one 429-pixel label inside a 182-pixel viewport, so its rectangle runs off the
        /// bottom of the popup; sharing the scroll content with the objective under it, the same label
        /// can instead be scrolled so its top pokes out over the popup's title bar. Either way every
        /// layout rule answers about a shape nobody can see - the paragraph measures level with a
        /// strip and went missing from a content area that is worked out from the strips it lies
        /// between.
        ///
        /// So the test is whether the rectangle is partly SHOWING and partly CUT OFF - not whether the
        /// widget alone outsizes the viewport, which misses one that only overruns it because a
        /// sibling shares its scroll content. The answer is the scrolling window rather than the
        /// viewport inside it: that is the box the player sees the text in, and the one the game
        /// named. A widget merely SCROLLED out of sight is not clipped in this sense and keeps its own
        /// rectangle - it is one row of a list the player scrolls through, and putting every such row
        /// on the viewport's edge would stack a whole sheet at one point.
        /// </summary>
        public static AgeTransform Clipped(AgeTransform widget)
        {
            try
            {
                if (widget == null)
                {
                    return null;
                }

                Rect it = widget.GetGlobalPosition();
                AgeTransform at = widget.Parent;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    AgeControlScrollView view = at.GetComponent<AgeControlScrollView>();
                    if (view != null && view.Viewport != null)
                    {
                        Rect box = view.Viewport.GetGlobalPosition();
                        bool showing =
                            it.yMin < box.yMax - Rounding
                            && it.yMax > box.yMin + Rounding
                            && it.xMin < box.xMax - Rounding
                            && it.xMax > box.xMin + Rounding;
                        bool cutOff =
                            it.yMin < box.yMin - Rounding
                            || it.yMax > box.yMax + Rounding
                            || it.xMin < box.xMin - Rounding
                            || it.xMax > box.xMax + Rounding;
                        return showing && cutOff ? at : widget;
                    }

                    at = at.Parent;
                }

                return widget;
            }
            catch (Exception e)
            {
                Log.Warn("widgets: measuring what shows a widget threw: " + e);
                return widget;
            }
        }

        /// <summary>How far a widget may overrun the box it is shown in and still fit it: a pixel of
        /// rounding, not a line of anything.</summary>
        private const float Rounding = 1f;

        /// <summary>The heading the game wrote across the top of a <c>GuiPanel</c>, which is what a
        /// sighted player reads above its content. Found where it is drawn: these are plain panels and
        /// none of them binds the label, but every one of them names it "PanelTitle" in the prefab.
        /// </summary>
        public static string PanelTitle(AgeTransform panel)
        {
            return TextOf(ChildNamed(panel, "PanelTitle", 1));
        }

        /// <summary>
        /// A widget the game is drawing, by the name its prefab gave it - breadth first, so the
        /// outermost of two things wearing the same name wins.
        ///
        /// The last resort for a band a window draws and does not expose: several of the game's own
        /// windows name their heading groups in the prefab and bind neither the group nor the label
        /// inside it, so there is nothing to ask for them by except the name on screen.
        /// </summary>
        public static AgeTransform ChildNamed(AgeTransform widget, string name, int depth)
        {
            if (widget == null || depth < 0)
            {
                return null;
            }

            try
            {
                IList<AgeTransform> children = widget.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (child != null && child.name == name && Visible(child))
                    {
                        return child;
                    }
                }

                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform found = ChildNamed(children[i], name, depth - 1);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>
        /// The control a click on this widget would ALSO reach - the nearest control above it in the
        /// widget chain, which is <c>AgeControl.FindParentControl</c>
        /// (<c>firstpass/AgeControl.cs:231-249</c>) reproduced because the engine's own copy is
        /// protected.
        ///
        /// Public because it is the audit question for every node the mod activates: a widget whose
        /// answer here is a control carrying activation wiring is a widget the mouse works in two steps
        /// and <see cref="Press"/> works in one. Whether that ancestor exists is PREFAB data, so the
        /// answer can only be had from the running game.
        /// </summary>
        public static AgeControl ParentControl(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget == null ? null : widget.Parent;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    AgeControl control = at.AgeControl;
                    if (control != null)
                    {
                        return control;
                    }

                    at = at.Parent;
                }
            }
            catch (Exception) { }

            return null;
        }

        private static bool Propagates(AgeControl control)
        {
            try
            {
                return control != null && control.PropagateInteraction;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // An ancestor's half of the click: its own wiring, no sound (the engine reaches an ancestor by
        // a C# call on the control, so the AgeAudio on its transform never hears the press) and no
        // double-click branch.
        private static void FireAncestor(AgeControl control)
        {
            AgeControlToggle toggle = control as AgeControlToggle;
            if (toggle != null)
            {
                toggle.State = !toggle.State;
                Send(toggle.OnSwitchObject, toggle.OnSwitchMethod, toggle.gameObject);
                return;
            }

            AgeControlButton button = control as AgeControlButton;
            if (button != null && button.UseLeftClick)
            {
                Send(button.OnActivateObject, button.OnActivateMethod, button.gameObject);
            }
        }

        public static AgeControlButton Button(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<AgeControlButton>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Flip a toggle the way its own click path does: the state first, then the handler,
        /// which reads the state it now finds. Calling the handler alone acts on the stale value.
        /// </summary>
        public static void Toggle(AgeControlToggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            try
            {
                Click(Transform(toggle));
                toggle.State = !toggle.State;
                Send(toggle.OnSwitchObject, toggle.OnSwitchMethod, toggle.gameObject);
            }
            catch (Exception e)
            {
                Log.Warn("widgets: switching a toggle threw: " + e);
            }
        }

        /// <summary>
        /// Put a toggle ON and tell its handler, for a toggle the game is using as a RADIO - one of a
        /// set it settles by writing every member's state back from the one name it keeps.
        ///
        /// The engine's own click flips (<c>AgeControlToggle.HandleMouseUpOrDown</c> :211-215), so
        /// clicking the member that is already on unticks it for the frames until the panel's refresh
        /// writes it back. A mouse sees that as a blink; a live-watched Selected part reads it out. So a
        /// pick is a pick: the handler these groups are wired to only ever SETS which member is in
        /// force and never reads the state it was called with, which is what makes setting it faithful
        /// rather than a guess.
        /// </summary>
        public static void Select(AgeControlToggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            try
            {
                Click(Transform(toggle));
                toggle.State = true;
                Send(toggle.OnSwitchObject, toggle.OnSwitchMethod, toggle.gameObject);
            }
            catch (Exception e)
            {
                Log.Warn("widgets: picking a toggle threw: " + e);
            }
        }

        /// <summary>Take an entry of a drop list the way clicking it does: the list's own selection
        /// first - it is what rewrites the closed control's label - then the handler the list itself is
        /// wired to, which is what stores the answer. Every drop list in the game carries that wiring,
        /// so no caller has to know which window owns the list.</summary>
        public static void Choose(AgeControlDropList list, int index)
        {
            if (list == null)
            {
                return;
            }

            try
            {
                Click(Transform(list));
                list.SelectedItem = index;
                Send(list.OnSelectionObject, list.OnSelectionMethod, list.gameObject);
            }
            catch (Exception e)
            {
                Log.Warn("widgets: choosing a drop list entry threw: " + e);
            }
        }

        /// <summary>
        /// The sound a click makes.
        ///
        /// Replaying a widget's wired handler is not the whole of clicking it. The noise a control
        /// makes is not in the handler and not in the control either: it is an <c>AgeAudio</c>
        /// component sitting on the same transform, which the engine's mouse dispatch tells about the
        /// press (<c>AgeAudio.MouseUp</c> :191-197, posting <c>MouseUpEventID</c> through the gui audio
        /// proxy). Reaching the handler and not that component is why every control the mod worked was
        /// silent while the same control clicked with a mouse answered - measured on the main menu:
        /// every button carries an AgeAudio with a non-zero MouseUpEventID.
        ///
        /// Posted before the handler runs, because a handler is entitled to close the window the
        /// component lives on.
        /// </summary>
        private static void Click(AgeTransform widget)
        {
            try
            {
                AgeAudio audio = widget == null ? null : widget.AgeAudio;
                if (audio == null)
                {
                    return;
                }

                AgeMouseEventData click = new AgeMouseEventData { MouseButtonIndex = 0 };
                audio.MouseDown(click);
                audio.MouseUp(click);
            }
            catch (Exception e)
            {
                Log.Warn("widgets: playing a control's click threw: " + e);
            }
        }

        /// <summary>
        /// Run the handler a widget names, with the number of arguments that handler actually takes.
        ///
        /// The engine's own dispatch is <c>SendMessage(name, senderGameObject)</c>, and most of the
        /// game's handlers are written to receive it - <c>OnClickStartCb(GameObject obj = null)</c>.
        /// Some are not: the faction chooser's hull arrows are <c>OnPreviousHullCb()</c> and
        /// <c>OnNextHullCb()</c>, with no parameter at all. Unity will not deliver a one-argument
        /// SendMessage to a method that takes none, and with <c>DontRequireReceiver</c> it does not
        /// complain either - the button simply did nothing, silently, on the one path a player has.
        /// So the arity is looked up on the target's own components and the matching overload used.
        /// </summary>
        private static void Send(GameObject target, string method, GameObject sender)
        {
            if (target == null || string.IsNullOrEmpty(method))
            {
                return;
            }

            if (TakesNoArgument(target, method))
            {
                target.SendMessage(method, SendMessageOptions.DontRequireReceiver);
                return;
            }

            target.SendMessage(method, sender, SendMessageOptions.DontRequireReceiver);
        }

        // Resolved per component type and handler name and then remembered: a widget's wiring never
        // changes, and this is asked on every activation.
        private static readonly Dictionary<string, bool> NoArgument = new Dictionary<string, bool>();

        private static bool TakesNoArgument(GameObject target, string method)
        {
            try
            {
                MonoBehaviour[] components = target.GetComponents<MonoBehaviour>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        continue;
                    }

                    Type type = components[i].GetType();
                    string key = type.FullName + "." + method;
                    bool bare;
                    if (!NoArgument.TryGetValue(key, out bare))
                    {
                        // GetMethod(name, flags) THROWS on an overloaded handler
                        // (AmbiguousMatchException), and one ambiguous component must not
                        // abort the scan for its siblings - so the lookup enumerates.
                        //
                        // "No argument" means a zero-parameter overload AND NO one-parameter one.
                        // A handler written with an OPTIONAL argument
                        // (<c>OnShowLocationCb(GameObject obj = null)</c>) compiles to BOTH
                        // arities, and Unity's SendMessage resolves by NAME and then insists on the
                        // arity it found first: sending with no argument to such a pair is refused
                        // outright ("Calling function OnShowLocationCb with no parameters but the
                        // function requires 1", measured 2026-08-22 on the quest-begun popup's
                        // show-location button - the press was silently a no-op). So a name that
                        // has both is sent the sender, which is what a mouse click sends.
                        bare = false;
                        try
                        {
                            MethodInfo[] methods = type.GetMethods(
                                BindingFlags.Instance
                                    | BindingFlags.Public
                                    | BindingFlags.NonPublic
                                    | BindingFlags.FlattenHierarchy
                            );
                            bool takesOne = false;
                            for (int m = 0; m < methods.Length; m++)
                            {
                                if (methods[m].Name != method)
                                {
                                    continue;
                                }

                                int parameters = methods[m].GetParameters().Length;
                                if (parameters == 0)
                                {
                                    bare = true;
                                }
                                else if (parameters == 1)
                                {
                                    takesOne = true;
                                }
                            }

                            bare = bare && !takesOne;
                        }
                        catch (Exception e)
                        {
                            Log.Warn(
                                "widgets: reading the arity of "
                                    + key
                                    + " threw: "
                                    + e.GetType().Name
                            );
                        }

                        NoArgument[key] = bare;
                    }

                    if (bare)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("widgets: reading a handler's arity threw: " + e);
            }

            return false;
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

        /// <summary>
        /// Everything a panel the mod has NOT modelled widget by widget is showing, one line per thing
        /// it says: the text of every label it draws, and the words of every tooltip whose words are on
        /// the widget rather than composed by a renderer.
        ///
        /// For a read-only panel of a shape the mod has no model for - a lens's own overlay, an
        /// out-of-fixture variant - this is the whole reading, and it costs nothing per screen. It is
        /// deliberately NOT a substitute for modelling a panel the player has to work: it produces
        /// lines, not controls, and it says nothing about which line belongs to which control.
        ///
        /// A line is dropped when it only repeats the line before it, which is what the game's habit of
        /// drawing the same words on a group and on the label inside it would otherwise produce.
        /// </summary>
        public static IList<string> DrawnLines(AgeTransform widget, int maxDepth = 8)
        {
            List<string> lines = new List<string>();
            CollectLines(widget, lines, maxDepth, false);
            return lines;
        }

        /// <summary>The same reading for a panel whose lines the game FADES rather than hides - the scan
        /// view's map labels, where a whole line of a label is switched off for the layer the camera is on
        /// by animating its alpha to nothing and leaving it marked visible. Reading such a panel by
        /// visibility alone announces a line the player cannot see; see <see cref="Painted"/> for the same
        /// rule applied to pooled tables and <see cref="PaintedText"/> for the one-phrase form.</summary>
        public static IList<string> PaintedLines(AgeTransform widget, int maxDepth = 8)
        {
            List<string> lines = new List<string>();
            CollectLines(widget, lines, maxDepth, true);
            return lines;
        }

        private static void CollectLines(
            AgeTransform widget,
            List<string> lines,
            int depth,
            bool paintedOnly
        )
        {
            if (widget == null || depth < 0)
            {
                return;
            }

            try
            {
                if (!widget.Visible || (paintedOnly && widget.Alpha <= 0f))
                {
                    return;
                }

                AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
                Add(lines, label == null ? null : AgeText.Label(label));
                AgeTooltip tooltip = Readable(Raw(widget));
                if (tooltip != null)
                {
                    IList<string> words = AgeText.Lines(AgeText.Tooltip(tooltip));
                    for (int i = 0; words != null && i < words.Count; i++)
                    {
                        Add(lines, words[i]);
                    }
                }

                IList<AgeTransform> children = widget.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    CollectLines(children[i], lines, depth - 1, paintedOnly);
                }
            }
            catch (Exception) { }
        }

        private static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line) && (lines.Count == 0 || lines[lines.Count - 1] != line))
            {
                lines.Add(line);
            }
        }

        /// <summary>Every text a widget draws, in one phrase - the caption of a group whose words the
        /// game spreads over an icon, a number and a label. It is read off the LABELS and nothing
        /// else: an icon token inside label text is named, a standalone icon widget beside them is
        /// not read at all.
        /// </summary>
        public static string TextOf(AgeTransform widget, int maxDepth = 6)
        {
            List<string> parts = new List<string>();
            Collect(widget, parts, maxDepth);
            return Phrase(parts);
        }

        /// <summary>The same reading, for a widget whose words come out of a POOLED table: the rows the
        /// game retired by fading them to nothing are left out, so the phrase is what is on the screen.
        /// See <see cref="Painted"/> for why <see cref="TextOf"/> cannot answer this on its own, and why
        /// it is asked here rather than everywhere.</summary>
        public static string PaintedText(AgeTransform widget, int maxDepth = 6)
        {
            List<string> parts = new List<string>();
            Collect(widget, parts, maxDepth, true);
            return Phrase(parts);
        }

        /// <summary>
        /// The same reading for a widget a walk has ALREADY vouched for, asking the painted question
        /// only of the pieces BELOW it.
        ///
        /// <see cref="PaintedText"/> also asks the widget's OWN alpha, which a walk that came down
        /// through <see cref="DrawnChild"/> has already settled - and which would read a container
        /// fading ITSELF in as wordless, the failure <see cref="Paints"/> exists to avoid. What is left
        /// is the case a leaf reading hits: a group at full alpha whose only words are on a POOLED row
        /// the game retired by fading it - the ship design costs box, where the group kept for a
        /// strategic-resource row still holds "1 Adamantian" for a design that costs no strategic
        /// resource. Such a widget reads as nothing, and the caller's empty-text early-out drops it.
        /// </summary>
        public static string PaintedPartsText(AgeTransform widget, int maxDepth = 6)
        {
            List<string> parts = new List<string>();
            try
            {
                if (widget != null && widget.Visible)
                {
                    AddLabel(widget, parts);
                    IList<AgeTransform> children = widget.Children;
                    for (int i = 0; children != null && i < children.Count; i++)
                    {
                        Collect(children[i], parts, maxDepth - 1, true);
                    }
                }
            }
            catch (Exception) { }

            return Phrase(parts);
        }

        private static string Phrase(List<string> parts)
        {
            Core.Speech.MessageBuilder message = new Core.Speech.MessageBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                message.Fragment(parts[i]);
            }

            return message.Build();
        }

        /// <summary>
        /// What a label SAYS while the game is drawing it, and null where it is not.
        ///
        /// The drawn test belongs here rather than at the call site because this game hides a label
        /// without clearing it: an unguarded read answers with the words the label was last bound
        /// with, so a panel that has moved on speaks the PREVIOUS binding's figure as though it were
        /// still on the screen.
        ///
        /// null IS the not-drawn answer; a drawn blank label answers empty. A caller that only wants
        /// to know whether there is anything to say may treat the two alike, but one that wraps the
        /// value in a sentence of its own must test null - the game draws labels it has nothing to
        /// write into, and formatting an empty one says the sentence with a hole in it.
        /// </summary>
        public static string DrawnLabel(AgePrimitiveLabel label)
        {
            try
            {
                return label == null || !Visible(label.AgeTransform)
                    ? null
                    : AgeText.Label(label) ?? string.Empty;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The same answer for a label the panel shows and hides by the GROUP around it
        /// rather than by the label itself: the drawn question is asked of <paramref name="gate"/>,
        /// which is the thing the game switches, while the words come off the label inside it.
        /// </summary>
        public static string DrawnLabel(AgeTransform gate, AgePrimitiveLabel label)
        {
            try
            {
                return label == null || !Visible(gate)
                    ? null
                    : AgeText.Label(label) ?? string.Empty;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Every text a widget draws in one phrase - <see cref="TextOf"/> - but only while the game
        /// is drawing the widget, and null where it is not.
        ///
        /// <see cref="TextOf"/> asks each level's own visible flag as it descends, so what the guard
        /// adds is the ANCESTRY above the widget: a group the window has collapsed leaves the block
        /// inside it marked visible and still holding its words, and reading one ungated captions a
        /// region with the previous binding's stale heading. null is the not-drawn answer; a drawn
        /// widget with nothing written on it answers the empty phrase <see cref="TextOf"/> gives.
        /// </summary>
        public static string DrawnText(AgeTransform widget, int maxDepth = 6)
        {
            try
            {
                return widget == null || !Visible(widget) ? null : TextOf(widget, maxDepth);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The widget a label is drawn on while the game is drawing it, else null - the same
        /// question <see cref="DrawnLabel(AgePrimitiveLabel)"/> answers, for a caller whose answer is
        /// the WIDGET rather than the words on it and whose null means "the window drew no such
        /// thing".</summary>
        public static AgeTransform Drawn(AgePrimitiveLabel label)
        {
            try
            {
                AgeTransform at = label == null ? null : label.AgeTransform;
                return Visible(at) ? at : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Collect(
            AgeTransform widget,
            List<string> parts,
            int depth,
            bool paintedOnly = false
        )
        {
            if (widget == null || depth < 0)
            {
                return;
            }

            try
            {
                if (!widget.Visible || (paintedOnly && widget.Alpha <= 0f))
                {
                    return;
                }

                AddLabel(widget, parts);
                IList<AgeTransform> children = widget.Children;
                if (children == null)
                {
                    return;
                }

                for (int i = 0; i < children.Count; i++)
                {
                    Collect(children[i], parts, depth - 1, paintedOnly);
                }
            }
            catch (Exception) { }
        }

        private static void AddLabel(AgeTransform widget, List<string> parts)
        {
            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (label == null)
            {
                return;
            }

            string text = AgeText.Label(label);
            if (!string.IsNullOrEmpty(text) && !parts.Contains(text))
            {
                parts.Add(text);
            }
        }

        /// <summary>
        /// What one item of a table SAYS: the words it draws, or - for an item the game draws as a bare
        /// icon and names nowhere on itself - the title of the wrapper it hangs on its own tooltip.
        ///
        /// Tables of findings (anomalies, curiosities, resource deposits) are rows of wordless pictures,
        /// and reading them as text is silence: three panels contributed NOTHING at all until this asked
        /// the wrapper. The wrapper is where the game keeps the name it would have written, so this is
        /// the same answer <see cref="TooltipTitle"/> gives a control, extended down a couple of levels
        /// because a table item routinely hangs its tooltip on the image inside it rather than on the
        /// item.
        /// </summary>
        public static string ItemText(AgeTransform widget)
        {
            // A pooled table with StrictVisibility off retires a surplus child by parking it at
            // Alpha 0 with Visible still true (AgeTransform.RefreshChildrenIList), and the parked
            // item keeps its old wrapper on its tooltip - so an item that draws nothing must say
            // nothing, or it answers with the previous binding's name.
            if (widget == null || widget.Alpha < 0.01f)
            {
                return null;
            }

            string drawn = TextOf(widget);
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            return WrapperTitle(widget, 3);
        }

        private static string WrapperTitle(AgeTransform widget, int depth)
        {
            if (widget == null || depth < 0)
            {
                return null;
            }

            try
            {
                if (!widget.Visible || widget.Alpha < 0.01f)
                {
                    return null;
                }

                string title = TooltipTitle(Raw(widget));
                if (!string.IsNullOrEmpty(title))
                {
                    return title;
                }

                IList<AgeTransform> children = widget.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    title = WrapperTitle(children[i], depth - 1);
                    if (!string.IsNullOrEmpty(title))
                    {
                        return title;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }
    }
}

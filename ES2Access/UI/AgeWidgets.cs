using System;
using System.Collections.Generic;
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
    public static partial class AgeWidgets
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
        /// The widget under one of the handle types a screen holds INSTEAD of a control: a prefab
        /// field typed as the panel, the behaviour or the primitive it wired, which nine screens each
        /// unwrapped with their own try-wrapped one-liner.
        ///
        /// Overloads rather than one generic because the hierarchies are unrelated - <c>GuiPanel</c>,
        /// <c>GuiBehaviour</c>, <c>AgePrimitive</c> and <see cref="AgeControl"/> each declare their OWN
        /// <c>AgeTransform</c> property, so there is nothing common to constrain on. Every label,
        /// sector and image arrives through the <c>AgePrimitive</c> overload; the game's own
        /// global-namespace <c>GuiPanel</c>/<c>GuiBehaviour</c> subclasses and the components built on
        /// them (<c>BattlePowerGauge</c>) arrive through their bases.
        ///
        /// The try is not decoration: these are live scene objects, <c>AgePrimitive</c>'s getter falls
        /// back to a <c>GetComponent</c>, and a destroyed object costs the caller the widget rather
        /// than the frame.
        /// </summary>
        public static AgeTransform Transform(Amplitude.Unity.Gui.GuiPanel panel)
        {
            try
            {
                return panel == null ? null : panel.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The widget under a <c>GuiBehaviour</c> handle - see
        /// <see cref="Transform(Amplitude.Unity.Gui.GuiPanel)"/>.</summary>
        public static AgeTransform Transform(Amplitude.Unity.Gui.GuiBehaviour behaviour)
        {
            try
            {
                return behaviour == null ? null : behaviour.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The widget under a primitive - a label, a sector, an image. See
        /// <see cref="Transform(Amplitude.Unity.Gui.GuiPanel)"/>.</summary>
        public static AgeTransform Transform(AgePrimitive primitive)
        {
            try
            {
                return primitive == null ? null : primitive.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The widget one step up, or null - the guarded form of <c>AgeTransform.Parent</c>,
        /// which five screens each wrapped for themselves. A walk UP a prefab is the one place a null
        /// step is ordinary: it is how the walk finds out it has reached the top.</summary>
        public static AgeTransform Parent(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the PREFAB calls this widget - the engine object's own name, which is what a
        /// stop's id is built from where the game gives the mod nothing else to key on. "?" for a
        /// widget that is not there, so a key is always writable and two missing widgets key alike.
        /// Never spoken: this is a prefab name, not a word in anybody's language.</summary>
        public static string NameOf(AgeTransform widget)
        {
            try
            {
                return widget == null ? "?" : widget.name;
            }
            catch (Exception)
            {
                return "?";
            }
        }

        /// <summary>
        /// Whether <paramref name="ancestor"/> is <paramref name="widget"/> itself or somewhere above
        /// it in the parent chain - the "was this drawn inside that" question a dozen screens each
        /// answered with a loop of their own, under four different depth caps.
        ///
        /// Reflexive on purpose: every caller means "is it in there", and a container asked about
        /// itself is in there.
        ///
        /// The bound is this class's own <see cref="MaxAncestors"/> and is deliberately not the
        /// caller's to choose (owner ruling). A cap is a guard against a scene graph that loops, not a
        /// per-screen tuning knob - and a shallow one does not fail, it answers "not under" about a
        /// widget that is, which is how a four-level copy lost a whole panel's worth of labels.
        /// </summary>
        public static bool Under(AgeTransform widget, AgeTransform ancestor)
        {
            try
            {
                if (widget == null || ancestor == null)
                {
                    return false;
                }

                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    if (ReferenceEquals(at, ancestor))
                    {
                        return true;
                    }

                    at = at.Parent;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The child of <paramref name="ancestor"/> that <paramref name="widget"/> was drawn under -
        /// the "which card is this one in" form of <see cref="Under"/>, for a caller that needs the
        /// branch rather than the yes.
        ///
        /// null when the two are unrelated, and null when they are the SAME widget: a widget is not
        /// its own child, and a caller sorting things into a container's children has nowhere to file
        /// the container itself.
        /// </summary>
        public static AgeTransform Ancestor(AgeTransform widget, AgeTransform ancestor)
        {
            try
            {
                if (widget == null || ancestor == null)
                {
                    return null;
                }

                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    if (ReferenceEquals(at.Parent, ancestor))
                    {
                        return at;
                    }

                    at = at.Parent;
                }

                return null;
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

        /// <summary>
        /// How many of a container's children the game is really DRAWING - the same rule a walk
        /// applies, counted instead of visited.
        ///
        /// Seven screens counted this for themselves under three different drawn-tests, and the three
        /// that asked <see cref="Visible"/> over-count exactly where the number is the reading: a
        /// pooled table retires a surplus row by FADING it, leaving it visible and holding the
        /// previous binding's words, so "four planets" is spoken over a lens showing two. The
        /// container is gated with <see cref="DrawnChildren"/> and each child with
        /// <see cref="DrawnChild"/>, which makes a container the window has put away zero rather than
        /// whatever it last held.
        /// </summary>
        public static int DrawnCount(AgeTransform container)
        {
            IList<AgeTransform> children = DrawnChildren(container);
            int drawn = 0;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (DrawnChild(children, i) != null)
                {
                    drawn++;
                }
            }

            return drawn;
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

            // Asked from eighty-odd places, all of them on a build path that runs every frame, and each
            // ask is a breadth-then-depth walk of a subtree. Held for ONE frame, keyed on exactly what
            // the question was: the widget tree does not move between two asks in the same frame, and
            // it does move between frames - a prefab whose title band the game has just switched on has
            // to be found on the frame it appears. Only the OUTERMOST ask is remembered; the recursion
            // goes through Find, so the memo holds one entry per question rather than one per widget
            // the walk stepped over.
            int frame = Time.frameCount;
            if (_namedFrame != frame)
            {
                Named.Clear();
                _namedFrame = frame;
            }

            NameKey key = new NameKey(widget, name, depth);
            AgeTransform found;
            if (Named.TryGetValue(key, out found))
            {
                return found;
            }

            found = Find(widget, name, depth);
            Named[key] = found;

            // A name is the LAST resort - these widgets are found by the name their prefab gave them
            // precisely because the window binds nothing to ask for them by - so a name that resolves
            // to nothing is how a renamed prefab takes a heading away with no other symptom. Said once
            // per name per load, because the ask is per frame and a warning per frame is a cost of its
            // own (docs/generic/performance.md, "Stagger and cap everything unbounded"). It is not
            // always a defect: a panel that genuinely draws no such band answers nothing too, which is
            // why it is a warning and not an error.
            if (found == null && !Unnamed.ContainsKey(name))
            {
                Unnamed[name] = true;
                Log.Warn(
                    "widgets: nothing named \""
                        + name
                        + "\" is drawn within "
                        + depth
                        + " levels of where it was looked for - a renamed prefab reads as nothing"
                );
            }

            return found;
        }

        private static AgeTransform Find(AgeTransform widget, string name, int depth)
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
                    AgeTransform found = Find(children[i], name, depth - 1);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>What was asked for: the widget the walk started from, the prefab name, and how far
        /// down. Hand-written equality because the default one for a struct compares by reflection,
        /// which on a per-frame path costs more than the walk it is saving.</summary>
        private struct NameKey : IEquatable<NameKey>
        {
            private readonly AgeTransform _root;

            private readonly string _name;

            private readonly int _depth;

            public NameKey(AgeTransform root, string name, int depth)
            {
                _root = root;
                _name = name;
                _depth = depth;
            }

            public bool Equals(NameKey other)
            {
                return ReferenceEquals(_root, other._root)
                    && _depth == other._depth
                    && _name == other._name;
            }

            public override bool Equals(object other)
            {
                return other is NameKey && Equals((NameKey)other);
            }

            public override int GetHashCode()
            {
                int hash = _root == null ? 0 : _root.GetHashCode();
                return (hash * 31 + (_name == null ? 0 : _name.GetHashCode())) * 31 + _depth;
            }
        }

        private static readonly Dictionary<NameKey, AgeTransform> Named =
            new Dictionary<NameKey, AgeTransform>();

        private static int _namedFrame = -1;

        private static readonly Dictionary<string, bool> Unnamed = new Dictionary<string, bool>();
    }
}

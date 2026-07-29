using System;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// Brings the focused control into view when the page it lives on scrolls.
    ///
    /// A long list - the hundred-odd notification settings - is drawn through a viewport a dozen rows
    /// tall, and arrowing past the bottom of it used to leave the cursor on a row nobody could see.
    /// The player reading by ear does not care, but everyone else in the room is watching a list that
    /// has stopped following along, and the moment they take the mouse the two of you are looking at
    /// different things.
    ///
    /// Deliberately not a screen's job. Every control the mod declares already names the game object
    /// it came from, so the transform is there to be found and the scroll view is whatever ancestor of
    /// it happens to be one - which means a screen written next year scrolls correctly without
    /// knowing this exists. Screens that declare nothing scrollable simply never match.
    ///
    /// The scrolling itself is the engine's own <see cref="AgeControlScrollView.MouseWheel"/>, given
    /// the number of notches that covers the gap. Going through the wheel rather than writing the
    /// virtual area's offset directly is what keeps the scrollbar, the clamping and any notification
    /// the view sends on being scrolled exactly as they are when a hand does it. It follows the
    /// wheel's one limitation: a view scrolls along the axis it has a scrollbar for, vertical first,
    /// which is the same axis a mouse can scroll it along.
    ///
    /// Only ever scrolls when the control is actually outside the viewport, and only far enough to
    /// bring it inside, so stepping down a visible page does not drag the list along under it.
    /// </summary>
    public static class ScrollIntoView
    {
        /// <summary>How many pixels one notch of the wheel is worth; the engine's own constant.
        /// </summary>
        private const float NotchPixels = 300f;

        /// <summary>How far up a parent chain to look before deciding it is not a chain.</summary>
        private const int MaxAncestors = 64;

        /// <summary>Scroll whatever <paramref name="control"/> lives in until it can be seen. The
        /// argument is the backing game object a graph node was built from - anything the mod can get
        /// an <see cref="AgeTransform"/> out of; anything else is quietly not scrollable.</summary>
        public static void Reveal(object control)
        {
            try
            {
                AgeTransform transform = TransformOf(control);
                if (transform == null)
                {
                    return;
                }

                AgeControlScrollView view = ViewAround(transform);
                if (view == null || view.Viewport == null || view.VirtualArea == null)
                {
                    return;
                }

                Rect target = transform.GetGlobalPosition();
                Rect viewport = view.Viewport.GetGlobalPosition();
                bool vertical = view.VerticalScrollBar != null;
                float gap = vertical
                    ? Gap(target.yMin, target.yMax, viewport.yMin, viewport.yMax)
                    : Gap(target.xMin, target.xMax, viewport.xMin, viewport.xMax);
                if (gap == 0f)
                {
                    return;
                }

                float factor = vertical ? view.ScrollFactorVertical : view.ScrollFactorHorizontal;
                if (factor == 0f)
                {
                    return;
                }

                // The wheel turns notches into pixels, so the gap is handed back in notches. The view
                // clamps the result to its own range, so a gap larger than the list can scroll simply
                // scrolls to the end.
                view.MouseWheel(gap / (NotchPixels * factor));
            }
            catch (Exception e)
            {
                Log.Warn("scroll: bringing the focused control into view threw: " + e);
            }
        }

        /// <summary>
        /// How far the content has to move for the span between <paramref name="min"/> and
        /// <paramref name="max"/> to sit inside the viewport, and 0 when it already does.
        ///
        /// Positive moves the content towards the end it scrolled away from. A control taller than
        /// the viewport can never fit, so its leading edge is lined up and the rest is left hanging
        /// off - which is what reading order wants.
        /// </summary>
        private static float Gap(float min, float max, float viewMin, float viewMax)
        {
            if (min < viewMin)
            {
                return viewMin - min;
            }

            return max > viewMax ? Mathf.Max(viewMax - max, viewMin - min) : 0f;
        }

        /// <summary>The nearest scrolling ancestor, or null when nothing above this control
        /// scrolls.</summary>
        private static AgeControlScrollView ViewAround(AgeTransform transform)
        {
            int depth = 0;
            for (
                AgeTransform node = transform.Parent;
                node != null && depth++ < MaxAncestors;
                node = node.Parent
            )
            {
                AgeControlScrollView view = node.AgeControl as AgeControlScrollView;
                if (view != null)
                {
                    return view;
                }
            }

            return null;
        }

        /// <summary>The transform a node's backing object stands on. Controls name themselves
        /// differently from screen to screen - a widget, the control component on it, the row object
        /// the whole thing hangs off - so all of them are asked the same way.</summary>
        private static AgeTransform TransformOf(object control)
        {
            AgeTransform transform = control as AgeTransform;
            if (transform != null)
            {
                return transform;
            }

            AgeControl age = control as AgeControl;
            if (age != null)
            {
                return age.AgeTransform;
            }

            Component component = control as Component;
            return component == null ? null : component.GetComponent<AgeTransform>();
        }
    }
}

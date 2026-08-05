using System;
using System.Collections.Generic;
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

        /// <summary>A control inside a group the window has collapsed is still marked visible itself,
        /// so the chain above it is what says whether the player can see it.</summary>
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

        /// <summary>Whether the player could work this control: the widget is on and so is everything
        /// it sits inside, since a panel the game has disabled kills the whole subtree under it.
        /// </summary>
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

        /// <summary>A widget's tooltip whatever kind it is - what a caller needs to SHOW one rather
        /// than to read it.</summary>
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

        /// <summary>A tooltip only if its words are on the widget. One that names a CLASS is assembled
        /// by a renderer at draw time and its content field holds authoring leftovers, so there is
        /// nothing there to read; <see cref="TooltipLines"/> reads those off the drawn window instead.
        /// </summary>
        public static AgeTooltip Readable(AgeTooltip tooltip)
        {
            try
            {
                return tooltip != null && string.IsNullOrEmpty(tooltip.Class) ? tooltip : null;
            }
            catch (Exception)
            {
                return null;
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
                toggle.State = !toggle.State;
                Send(toggle.OnSwitchObject, toggle.OnSwitchMethod, toggle.gameObject);
            }
            catch (Exception e)
            {
                Log.Warn("widgets: switching a toggle threw: " + e);
            }
        }

        private static void Send(GameObject target, string method, GameObject sender)
        {
            if (target != null && !string.IsNullOrEmpty(method))
            {
                target.SendMessage(method, sender, SendMessageOptions.DontRequireReceiver);
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
        }

        /// <summary>The same for a widget with no button under it - a readout, an icon. Nothing lights
        /// up because there is nothing there to light, and the tooltip appears, which for these is the
        /// whole of what the pointer was ever for.</summary>
        public static void PointAt(NodeVtable vtable, AgeTransform widget)
        {
            AgeTransform it = widget;
            vtable.OnFocusVisual = () => PointerFocus.MoveTo(Button(it), Raw(it), it);
            vtable.OnBlurVisual = ReleasePointer;
        }

        public static readonly Action ReleasePointer = PointerFocus.Release;

        /// <summary>Every text a widget draws, in one phrase - the caption of a group whose words the
        /// game spreads over an icon, a number and a label. Icon tokens come back as their names.
        /// </summary>
        public static string TextOf(AgeTransform widget, int maxDepth = 6)
        {
            List<string> parts = new List<string>();
            Collect(widget, parts, maxDepth);
            Core.Speech.MessageBuilder message = new Core.Speech.MessageBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                message.Fragment(parts[i]);
            }

            return message.Build();
        }

        private static void Collect(AgeTransform widget, List<string> parts, int depth)
        {
            if (widget == null || depth < 0)
            {
                return;
            }

            try
            {
                if (!widget.Visible)
                {
                    return;
                }

                AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
                if (label != null)
                {
                    string text = AgeText.Label(label);
                    if (!string.IsNullOrEmpty(text) && !parts.Contains(text))
                    {
                        parts.Add(text);
                    }
                }

                IList<AgeTransform> children = widget.Children;
                if (children == null)
                {
                    return;
                }

                for (int i = 0; i < children.Count; i++)
                {
                    Collect(children[i], parts, depth - 1);
                }
            }
            catch (Exception) { }
        }
    }
}

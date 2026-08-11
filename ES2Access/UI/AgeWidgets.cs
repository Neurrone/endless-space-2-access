using System;
using System.Collections.Generic;
using System.Reflection;
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
                        MethodInfo found = type.GetMethod(
                            method,
                            BindingFlags.Instance
                                | BindingFlags.Public
                                | BindingFlags.NonPublic
                                | BindingFlags.FlattenHierarchy
                        );
                        bare = found != null && found.GetParameters().Length == 0;
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
            CollectLines(widget, lines, maxDepth);
            return lines;
        }

        private static void CollectLines(AgeTransform widget, List<string> lines, int depth)
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
                    CollectLines(children[i], lines, depth - 1);
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

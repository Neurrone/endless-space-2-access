using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    public static partial class AgeWidgets
    {
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

        /// <summary>
        /// The button the prefab wired to <paramref name="handler"/> somewhere under
        /// <paramref name="root"/>, preferring one the player can SEE and, among those, the SMALLEST.
        ///
        /// Both tie-breaks come from one measurement (the hero overview's pencils): a prefab
        /// routinely wires several buttons to a single handler - the little glyph drawn in a box's
        /// heading, and the invisible sheet stretched over the whole box behind it - and the one worth
        /// naming and pointing a cursor at is the drawn one, the smaller of two drawn ones being the
        /// glyph rather than the hit area. Eight copies of this lookup existed and five took the first
        /// match whatever its size, which is how a cursor ends up on a rectangle the player cannot see
        /// while the button beside it goes undeclared.
        ///
        /// Falls back to the first match when nothing matching is drawn, so a caller reading a window
        /// mid-fade still finds its button rather than reporting the page has none.
        /// </summary>
        public static AgeControlButton WiredTo(AgeTransform root, string handler)
        {
            try
            {
                if (root == null || string.IsNullOrEmpty(handler))
                {
                    return null;
                }

                AgeControlButton[] buttons = root.GetComponentsInChildren<AgeControlButton>(true);
                AgeControlButton first = null;
                AgeControlButton drawn = null;
                float smallest = float.MaxValue;
                for (int i = 0; buttons != null && i < buttons.Length; i++)
                {
                    AgeControlButton button = buttons[i];
                    if (button == null || button.OnActivateMethod != handler)
                    {
                        continue;
                    }

                    if (first == null)
                    {
                        first = button;
                    }

                    AgeTransform widget = Transform(button);
                    if (!Visible(widget))
                    {
                        continue;
                    }

                    float area = widget.Width * widget.Height;
                    if (area < smallest)
                    {
                        smallest = area;
                        drawn = button;
                    }
                }

                return drawn ?? first;
            }
            catch (Exception)
            {
                return null;
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
    }
}

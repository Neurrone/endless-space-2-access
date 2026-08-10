using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// The buttons a card DRAWS, as child nodes of the card.
    ///
    /// Both planet cards in this game - the orbital one on the map and the management page's - hang a
    /// row of buttons off the card, name most of them nowhere, and leave a REFUSING one clickable so
    /// that clicking it explains itself instead of acting. That treatment is the same on both, so it
    /// is written once here rather than copied: a screen says which widgets the card is drawing and
    /// what to call them, and gets the gate, the refusal, the hint split and the pointer for free.
    ///
    /// The gate is the game's own: a widget it has hidden or switched off is not offered at all
    /// (<see cref="Drawn"/>), and whether the offered one would ACT is asked per frame on the node,
    /// because a button can start refusing while the player is standing on it.
    /// </summary>
    public static class CardActions
    {
        /// <summary>One of the card's buttons and the words to call it by. <see cref="Offered"/> is
        /// what "would this act if it were pressed" means for this button - null for the usual hint
        /// test, which is how this game leaves a blocked button clickable.
        ///
        /// <see cref="Toggle"/> is set where the card drew the action as a TICK rather than a button -
        /// an outpost action that is either running or not, the decolonization the system is either
        /// scheduled for or not - and turns the node into a checkbox that says which. <see cref="Value"/>
        /// is a number written on the control itself beside its name.</summary>
        public struct CardAction
        {
            public AgeTransform Widget;
            public Func<string> Label;
            public Func<bool> Offered;
            public AgeControlToggle Toggle;
            public Func<string> Value;
        }

        /// <summary>A button named by a phrase of this mod's - for a control the game draws as a
        /// wordless icon and names nowhere.</summary>
        public static void AddNamedByMod(List<CardAction> found, AgeControl control, string modKey)
        {
            AddNamedByMod(found, AgeWidgets.Transform(control), modKey);
        }

        /// <summary>The same for a control the game hangs on a plain transform rather than exposing as
        /// a button field.</summary>
        public static void AddNamedByMod(List<CardAction> found, AgeTransform widget, string modKey)
        {
            AgeTransform at = Drawn(widget);
            if (at != null)
            {
                found.Add(new CardAction { Widget = at, Label = () => ModStrings.Get(modKey) });
            }
        }

        /// <summary>A button the game names somewhere OTHER than on the widget - on the fleet action it
        /// carries out, say - by that key.</summary>
        public static void AddNamedByGame(List<CardAction> found, AgeControl control, string gameKey)
        {
            AgeTransform at = Drawn(AgeWidgets.Transform(control));
            if (at != null)
            {
                found.Add(new CardAction { Widget = at, Label = () => Localized(gameKey) });
            }
        }

        /// <summary>
        /// A button the game keeps DRAWN while refusing it - visible but switched off, with the reason
        /// written into its own tooltip (the queue line's buy-outs). Declared whenever it is drawn and
        /// offered only while the game would dispatch it, rather than disappearing the moment the
        /// answer is no: which currencies this thing could be bought with, and why not today, is
        /// exactly what the player came to the line to find out.
        /// </summary>
        public static void AddRefusable(
            List<CardAction> found,
            AgeTransform widget,
            Func<string> label
        )
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform at = widget;
            found.Add(
                new CardAction
                {
                    Widget = at,
                    Label = label,
                    Offered = () => AgeWidgets.Operable(at),
                }
            );
        }

        /// <summary>
        /// An action the card draws as a TICK, kept whenever it is drawn and offered only while the
        /// game would dispatch it - the same "why not today" reasoning as <see cref="AddRefusable"/>,
        /// because a blocked outpost action is exactly the thing the player opened the card to ask
        /// about.
        ///
        /// <paramref name="label"/> is what to call it (these are drawn with a cost on them and no
        /// name at all) and <paramref name="value"/> the number written on it.
        /// </summary>
        public static void AddToggle(
            List<CardAction> found,
            AgeControlToggle toggle,
            Func<string> label,
            Func<string> value
        )
        {
            AgeTransform at = AgeWidgets.Transform(toggle);
            if (at == null || !AgeWidgets.Visible(at))
            {
                return;
            }

            found.Add(
                new CardAction
                {
                    Widget = at,
                    Label = label,
                    Offered = () => AgeWidgets.Operable(at),
                    Toggle = toggle,
                    Value = value,
                }
            );
        }

        /// <summary>The words the game keeps for a control on the WRAPPER hung on its tooltip - the
        /// only place an outpost action is named, since the item itself draws nothing but a cost.
        /// </summary>
        public static Func<string> TitleOf(AgeControl control)
        {
            AgeTooltip tooltip = AgeWidgets.Raw(AgeWidgets.Transform(control));
            return () => AgeWidgets.TooltipTitle(tooltip);
        }

        /// <summary>A button the game names only in the sentence its own tooltip opens with.</summary>
        public static void AddNamedByTooltip(List<CardAction> found, AgeControl control)
        {
            AgeTransform at = Drawn(AgeWidgets.Transform(control));
            if (at != null)
            {
                AgeTooltip tooltip = AgeWidgets.Raw(at);
                found.Add(new CardAction { Widget = at, Label = () => FirstLine(tooltip) });
            }
        }

        /// <summary>
        /// The collected buttons as one node each, in the order they were collected.
        ///
        /// A blocked button is NOT dropped: this game leaves one clickable and turns its click into
        /// "here is the technology you are missing", so a hinted button is declared and REFUSING - the
        /// player hears "unavailable" and the game's own sentence about why - rather than quietly not
        /// being there.
        /// </summary>
        public static void Emit(GraphBuilder builder, string keyPrefix, List<CardAction> actions)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                CardAction action = actions[i];
                AgeTransform at = action.Widget;
                AgeControlToggle toggle = action.Toggle;
                AgeTooltip tooltip = AgeWidgets.Raw(at);
                Func<bool> offered = action.Offered ?? (() => !Gui.IsHintActive(at));
                NodeVtable vtable = toggle != null
                    ? GraphNodes.Checkbox(
                        action.Label,
                        () => toggle.State,
                        () => AgeWidgets.Toggle(toggle),
                        offered,
                        tooltip,
                        null,
                        null,
                        action.Value
                    )
                    : GraphNodes.Button(
                        action.Label,
                        () => AgeWidgets.Press(at),
                        offered,
                        tooltip
                    );
                // A refusing button's tooltip ends in an instruction to a MOUSE - hold Control and
                // click to be shown the technology that is missing - which is reviewable and not
                // spoken.
                vtable.Sections = GraphNodes.HintSections(tooltip);
                // The refusal in the game's own words, for a button whose tooltip is the assembled kind
                // and so is only indicated. A button whose tooltip is plain text already says it.
                NodeAnnouncement refusal = GraphNodes.RefusalPart(tooltip, offered);
                if (refusal != null)
                {
                    vtable.Announcements.Add(refusal);
                }

                if (toggle != null)
                {
                    AgeWidgets.Point(vtable, toggle, tooltip, at);
                }
                else
                {
                    AgeWidgets.PointAt(vtable, at);
                }

                builder.AddItem(ControlId.Structural(keyPrefix + "/action/" + i), vtable);
            }
        }

        /// <summary>The transform of a button the game is really drawing and would really dispatch -
        /// null for one it has hidden or switched off.</summary>
        private static AgeTransform Drawn(AgeTransform widget)
        {
            try
            {
                return widget != null
                    && AgeWidgets.Visible(widget)
                    && AgeWidgets.Operable(widget)
                    ? widget
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A phrase of the game's, resolved when it is spoken.</summary>
        public static Func<string> GameText(string key)
        {
            return () => Localized(key);
        }

        private static string Localized(string key)
        {
            try
            {
                return AgeText.Clean(Gui.Localize(key));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string FirstLine(AgeTooltip tooltip)
        {
            try
            {
                if (AgeWidgets.Readable(tooltip) == null)
                {
                    return null;
                }

                IList<string> lines = AgeText.Lines(AgeText.Tooltip(tooltip));
                return lines != null && lines.Count > 0 ? lines[0] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

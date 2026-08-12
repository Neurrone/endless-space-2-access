using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

// The game has its own InputAction in the global namespace, and so does the mod's input layer.
using GameInput = InputAction;

namespace ES2Access.Screens
{
    /// <summary>
    /// The game's one confirmation box, made navigable. Every question the game asks - discarding
    /// unapplied options, the fifteen seconds it gives you to keep a display mode, a key already
    /// bound to something else, leaving a game - is this same window with different words in it, so
    /// making it navigable once makes all of them navigable.
    ///
    /// It floats above whatever asked the question, and it is a modal, so it sits on a layer above
    /// every ordinary screen: a screen underneath that steps aside for modals (the main menu does)
    /// simply goes quiet, and one that does not is covered.
    ///
    /// It is walked the way it is drawn, which is the same shape every popup in the game has: the
    /// question is a block of text with a row of answers under it, so the text is a control in its own
    /// right and the one focus lands on - it is what the player has to read before any button means
    /// anything - and up and down move between it and the answers while left and right walk them.
    ///
    /// The box's heading is spoken as the screen's name on arrival and the question is not, because
    /// the question is what focus is about to read: saying it as the screen's name too would say it
    /// twice. The heading is the safe half to say that way - the timeout variant rewrites its MESSAGE
    /// every frame as the seconds count down while its heading stands still, so the countdown is text
    /// that resolves whenever it is read and is watched by nothing: refocusing the question, or reading
    /// it out of the review buffer, says the seconds left at the moment the player asked.
    ///
    /// Every visible button is declared, reading the caption the game put on it, and pressing one
    /// runs the handler the game wired to it - so the answer the caller receives is the answer a
    /// mouse would have given, and the window closes itself the way it always does. A button the game
    /// wrote a tooltip on carries that; the rest carry the question, so it is re-readable from
    /// wherever the player is standing.
    /// </summary>
    public sealed class MessageBoxScreen : Screen
    {
        public override string Key
        {
            get { return "screen.message-box"; }
        }

        /// <summary>Above every ordinary screen: a modal is on top of whatever raised it, and this
        /// is the only screen that can be.</summary>
        public override int Layer
        {
            get { return 100; }
        }

        /// <summary>A question the game is waiting on: the page it was raised over, and anything still
        /// drawn beside it, are the player's again once they have answered and not before.</summary>
        public override bool AnswersOnly
        {
            get { return true; }
        }

        /// <summary>What the box is headed. Spoken on arrival, ahead of the question focus lands on,
        /// so the two together read as the box reads and neither says the other's half twice. A box
        /// the game gave no heading says only that something is standing between the player and the
        /// screen they were on.</summary>
        public override string ScreenName
        {
            get
            {
                string title = Heading();
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenMessageBox)
                    : title;
            }
        }

        /// <summary>Ours while the window is up and has finished animating in - the buttons are
        /// laid out and the captions written at the end of that animation, so anything earlier
        /// would be read from a box that has not decided what it is asking yet.</summary>
        public override bool IsActive()
        {
            MessageBoxWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape belongs to the game here: the window is an input handler and its own
        /// Exit route is what turns the key into the Cancel answer the caller is waiting for.
        /// </summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            MessageBoxWindow window = Window();
            if (window == null)
            {
                return;
            }

            List<Choice> choices = Choices(window);
            if (choices.Count == 0)
            {
                return;
            }

            // Walked in the order they are drawn rather than the order the window declares its
            // fields: the box puts Cancel on the left and the answer that goes ahead on the right, and
            // left and right have to mean what they look like.
            choices.Sort(ReadingOrder);

            // The dialog's own heading, first, where it is drawn - a node the player can go back to
            // rather than a line that only ever happened on arrival. It is still the screen's spoken
            // name as well, so arriving says it once; focus starts on the question below it, which is
            // what keeps it from being said twice.
            SettingRows.AddReadout(
                builder,
                SettingRows.TransformOf(window.TitleLabel),
                "messagebox:heading"
            );

            // Declared before the answers and outside their row, so the builder wires the row under
            // it: the question is a block of text, not one answer among them, and it takes no place
            // in their count.
            AgePrimitiveLabel message = window.MessageLabel;
            if (message != null)
            {
                ControlId id = ControlId.Referenced(message, "messagebox:question");
                builder.AddNode(
                    id,
                    new NodeVtable
                    {
                        // No role word: the question is not a control the player works, it is what
                        // they are being asked. Nothing about it is watched - the countdown variant
                        // rewrites it every frame, and a part that spoke on every change would talk
                        // over the answer the player is trying to choose.
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(Question),
                        },
                        Sections = GraphNodes.Sections(PromptLines, null),
                        OnFocusVisual = ReleasePointer,
                    }
                );
                builder.SetStart(id);
            }

            builder.StartRow();
            for (int i = 0; i < choices.Count; i++)
            {
                Choice choice = choices[i];
                AgeTooltip tooltip = TooltipOf(choice.Button);
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeText.Label(choice.Caption),
                    () => Click(choice),
                    () => Enabled(choice.Button),
                    tooltip
                );
                if (tooltip == null)
                {
                    // The question, under an answer the game said nothing else about: whichever
                    // button the player is on, the text they are answering is worth re-reading.
                    vtable.Sections = GraphNodes.Sections(PromptLines, null);
                }

                vtable.OnFocusVisual = () => PointerFocus.MoveTo(choice.Button, tooltip);
                vtable.OnBlurVisual = ReleasePointer;
                builder.AddItem(
                    ControlId.Referenced(choice.Button, "messagebox:" + choice.Key),
                    vtable
                );
            }

            builder.EndRow();
        }

        private static readonly Comparison<Choice> ReadingOrder = delegate(Choice a, Choice b)
        {
            return AgeLayout.ReadingOrder(a.Button.AgeTransform, b.Button.AgeTransform);
        };

        /// <summary>One answer the box is offering: the button, the label carrying its caption, and
        /// the window's own input route to the same answer for when the button turns out not to be
        /// wired.</summary>
        private struct Choice
        {
            public string Key;
            public AgeControlButton Button;
            public AgePrimitiveLabel Caption;
            public Action Fallback;
        }

        /// <summary>
        /// The answers the box is currently offering. The game shows a button exactly when it was
        /// given a caption for it, so a button with no caption is not an answer that is merely
        /// unavailable - it is one this question does not have.
        /// </summary>
        private static List<Choice> Choices(MessageBoxWindow window)
        {
            List<Choice> choices = new List<Choice>();
            try
            {
                Add(choices, "validate", window.ValidateButton, window.ValidateLabel, Validate);
                Add(choices, "cancel", window.CancelButton, window.CancelLabel, Cancel);
                Add(choices, "alternative", window.AlternativeButton, window.AlternativeLabel, null);
                Add(choices, "community", window.G2GButton, CaptionOf(window.G2GButton), null);
            }
            catch (Exception e)
            {
                Log.Warn("message box: reading the buttons threw: " + e);
            }

            return choices;
        }

        private static void Add(
            List<Choice> choices,
            string key,
            AgeControlButton button,
            AgePrimitiveLabel caption,
            Action fallback
        )
        {
            if (!Visible(button) || string.IsNullOrEmpty(AgeText.Label(caption)))
            {
                return;
            }

            choices.Add(
                new Choice
                {
                    Key = key,
                    Button = button,
                    Caption = caption,
                    Fallback = fallback,
                }
            );
        }

        // The community button is the one the window does not name a caption field for, so its
        // label is found where it lives: on the button.
        private static AgePrimitiveLabel CaptionOf(AgeControlButton button)
        {
            try
            {
                if (button == null || button.AgeTransform == null)
                {
                    return null;
                }

                foreach (
                    AgePrimitiveLabel label in button.AgeTransform.GetChildren<AgePrimitiveLabel>(
                        false
                    )
                )
                {
                    if (label != null)
                    {
                        return label;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("message box: reading a button's caption threw: " + e);
            }

            return null;
        }

        /// <summary>
        /// Press a button the way the engine presses it. Every AGE button carries the object and the
        /// method name its own mouse handler sends to, so replaying that pair runs the game's own
        /// answer handler - the one that calls back whoever asked the question and hides the window -
        /// with no click that could land on whatever the mouse is over. An unwired button falls back
        /// on the window's input route, which reaches the same two handlers Enter and Escape do.
        /// </summary>
        private static void Click(Choice choice)
        {
            try
            {
                AgeControlButton button = choice.Button;
                if (
                    button != null
                    && button.OnActivateObject != null
                    && !string.IsNullOrEmpty(button.OnActivateMethod)
                )
                {
                    button.OnActivateObject.SendMessage(
                        button.OnActivateMethod,
                        button.gameObject,
                        SendMessageOptions.DontRequireReceiver
                    );
                    return;
                }

                if (choice.Fallback != null)
                {
                    choice.Fallback();
                }
            }
            catch (Exception e)
            {
                Log.Warn("message box: answering " + choice.Key + " threw: " + e);
            }
        }

        private static readonly Action Validate = () => Answer(GameInput.Validate);

        private static readonly Action Cancel = () => Answer(GameInput.Exit);

        private static readonly Action ReleasePointer = PointerFocus.Release;

        private static void Answer(Amplitude.StaticString inputAction)
        {
            try
            {
                MessageBoxWindow window = Window();
                if (window != null)
                {
                    window.HandleInput(inputAction);
                }
            }
            catch (Exception e)
            {
                Log.Warn("message box: the " + inputAction + " route threw: " + e);
            }
        }

        /// <summary>
        /// The question as one spoken line, resolved every time it is asked for - the countdown
        /// variant's message is a different sentence each frame.
        ///
        /// The game wraps a long question over as many lines as the box is wide, so its line breaks
        /// are where the words ran out and not punctuation. They are joined with a space, which is the
        /// sentence the game wrote; a comma between them would read a full stop as "lost., Continue".
        /// </summary>
        private static string Question()
        {
            MessageBoxWindow window = Window();
            if (window == null)
            {
                return null;
            }

            MessageBuilder message = new MessageBuilder();
            foreach (string line in AgeText.Lines(AgeText.Label(window.MessageLabel)))
            {
                message.Fragment(line);
            }

            return message.Build();
        }

        private static string Heading()
        {
            MessageBoxWindow window = Window();
            try
            {
                return window == null ? null : AgeText.Label(window.TitleLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The question as the review buffer holds it: its heading, then its message a line
        /// at a time - the game writes a long one (a key already in use, a list of what will be
        /// lost) as exactly those lines.</summary>
        private static IList<string> PromptLines()
        {
            List<string> lines = new List<string>();
            MessageBoxWindow window = Window();
            if (window == null)
            {
                return lines;
            }

            try
            {
                string title = AgeText.Label(window.TitleLabel);
                if (!string.IsNullOrEmpty(title))
                {
                    lines.Add(title);
                }

                foreach (string line in AgeText.Lines(AgeText.Label(window.MessageLabel)))
                {
                    lines.Add(line);
                }
            }
            catch (Exception e)
            {
                Log.Warn("message box: reading the question threw: " + e);
            }

            return lines;
        }

        private static AgeTooltip TooltipOf(AgeControlButton button)
        {
            try
            {
                return button == null || button.AgeTransform == null
                    ? null
                    : button.AgeTransform.AgeTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Visible(AgeControlButton button)
        {
            try
            {
                return button != null
                    && button.AgeTransform != null
                    && button.AgeTransform.Visible;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Enabled(AgeControlButton button)
        {
            try
            {
                return button != null
                    && button.AgeTransform != null
                    && button.AgeTransform.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static MessageBoxWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<MessageBoxWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

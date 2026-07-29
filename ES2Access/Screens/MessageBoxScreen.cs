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
    /// The question is spoken as the screen's name - once, on arrival - rather than declared as a
    /// level of the hierarchy the buttons hang under. The timeout variant is why: it rewrites its
    /// message every frame as the seconds count down, and a hierarchy level whose text keeps
    /// changing is a new level every frame, which the path diff would dutifully announce every
    /// frame. Said once and then left in the review buffer, the countdown reads as the sentence it
    /// is and the player can go back over it at their own pace.
    ///
    /// Every visible button is declared, reading the caption the game put on it, and pressing one
    /// runs the handler the game wired to it - so the answer the caller receives is the answer a
    /// mouse would have given, and the window closes itself the way it always does.
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

        /// <summary>The question itself. Spoken on arrival, ahead of the button focus lands on.
        /// </summary>
        public override string ScreenName
        {
            get { return Prompt(); }
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

            List<ControlId> ids = new List<ControlId>(choices.Count);
            builder.StartRow();
            for (int i = 0; i < choices.Count; i++)
            {
                Choice choice = choices[i];
                ControlId id = ControlId.Referenced(choice.Button, "messagebox:" + choice.Key);
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeText.Label(choice.Caption),
                    () => Click(choice),
                    () => Enabled(choice.Button)
                );
                // The question, under every answer: whichever button the player is on, the text
                // they are answering is the thing worth re-reading.
                vtable.DetailLines = PromptLines;
                vtable.OnFocusVisual = () => PointerFocus.MoveTo(choice.Button, null);
                vtable.OnBlurVisual = ReleasePointer;

                ids.Add(id);
                builder.AddItem(id, vtable);
            }

            builder.EndRow();

            // The buttons sit in a row, so left and right walk them; up and down are wired to walk
            // them too, because on a two-button question no one should have to guess the axis.
            for (int i = 1; i < ids.Count; i++)
            {
                builder.Connect(ids[i - 1], GraphDir.Down, ids[i]);
                builder.Connect(ids[i], GraphDir.Up, ids[i - 1]);
            }
        }

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

        /// <summary>The whole question as one spoken line. Falls back on saying that a dialog is
        /// there, so a box that has not written its text yet still announces itself as something
        /// standing between the player and the screen they were on.</summary>
        private static string Prompt()
        {
            IList<string> lines = PromptLines();
            MessageBuilder prompt = new MessageBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                prompt.ListItem(lines[i]);
            }

            return prompt.Build() ?? ModStrings.Get(ModStrings.ScreenMessageBox);
        }

        /// <summary>The question as the review buffer holds it: its title, then its message a line
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

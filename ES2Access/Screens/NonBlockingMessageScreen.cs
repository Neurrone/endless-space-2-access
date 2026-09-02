using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

// The game has its own InputAction in the global namespace, and so does the mod's input layer.
using GameInput = InputAction;

namespace ES2Access.Screens
{
    /// <summary>
    /// The game's OTHER confirmation box - the one that does not stop the game while it waits
    /// (<c>MessageBoxNonBlockingWindow</c>; raised through <c>Gui.ShowNonBlockingMessage</c>, GuiManager
    /// :2332 / Gui:1624).
    ///
    /// It is the same shape as <see cref="MessageBoxScreen"/> and is read the same way: a question with a
    /// row of answers under it, the heading said as the screen's name, the question the thing focus lands
    /// on, left and right walking the answers. Everything the message box's own class comment argues for
    /// applies here unchanged, and the differences are only these three:
    ///
    /// - It offers exactly two answers, both named by the caller (a caller that passes no title for one
    ///   gets no button, which is the game's own way of drawing a one-answer box), so there is no button
    ///   set to work out - the window names both fields.
    /// - It can be TIMED, and a timed box counts down inside its own MESSAGE: the coroutine rewrites the
    ///   message every frame from the caller's template and the seconds left (<c>RefreshTimeout</c>), so
    ///   the countdown is already in the sentence focus reads and needs nothing of the mod's. Nothing
    ///   watches it - a gauge that announced itself under a standing cursor would talk over the answer
    ///   the player is choosing - and re-reading the question says the seconds left at the moment they
    ///   asked. When it runs out the window answers Cancel for them, which is the game's decision.
    /// - Escape is HANDLED: the window is an input handler and answers Exit with Cancel, so the key
    ///   stays the game's and the caller gets the answer a right-click would have given.
    ///
    /// Its own layer, under the blocking box: this one does not stop the game, so the game can raise the
    /// blocking box over it while it is still up, and it must not then be the screen holding the
    /// keyboard.
    /// </summary>
    public sealed class NonBlockingMessageScreen : Screen
    {
        public override string Key
        {
            get { return "screen.message-box-non-blocking"; }
        }

        /// <summary>Under the blocking message box, which can be raised over anything - including over
        /// this - and over the error box, which cannot be dismissed at all.</summary>
        public override int Layer
        {
            get { return 97; }
        }

        /// <summary>The box carries one message and the button that answers it, and nothing else: what
        /// the game keeps drawing around it belongs to the page underneath, which is where it is
        /// declared and where the player is again the moment this is answered.</summary>
        public override bool AnswersOnly
        {
            get { return true; }
        }

        /// <summary>What the box is headed, spoken ahead of the question focus lands on. A box the caller
        /// gave no heading says only that something is standing between the player and the screen they
        /// were on - the same phrase the blocking box falls back on, because it is the same sentence.
        /// </summary>
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

        public override bool IsActive()
        {
            MessageBoxNonBlockingWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: the window's own Exit route is what turns the key into the Cancel answer
        /// the caller is waiting for.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            MessageBoxNonBlockingWindow window = Window();
            if (window == null)
            {
                return;
            }

            AgePrimitiveLabel message = window.MessageLabel;
            if (message != null)
            {
                ControlId id = ControlId.For(message, "non-blocking:question");
                builder.AddNode(Nodes.Drawn(
                    id,
                    new NodeVtable
                    {
                        // No role word and nothing watched: the question is not a control the player
                        // works, and a timed one rewrites itself every frame.
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(Question),
                        },
                        Sections = GraphNodes.Sections(PromptLines, null),
                        OnFocusVisual = AgeWidgets.ReleasePointer,
                    },
                    message
                ));
                builder.SetStart(id);
            }

            // Drawn order rather than field order: the box puts Cancel on the left and the answer that
            // goes ahead on the right, and left and right have to mean what they look like.
            List<Cell> answers = new List<Cell>();
            Answer(answers, window.ValidateButton, window.ValidateLabel, "validate", Validate);
            Answer(answers, window.CancelButton, window.CancelLabel, "cancel", Cancel);
            Cells.EmitLinear(builder, answers);
        }

        /// <summary>One answer the box is offering. The window shows a button exactly when the caller
        /// named it (<c>OnBeginShow</c> sets each button's visibility from its title), so a button with no
        /// caption is not an answer that is merely unavailable - it is one this question does not
        /// have.</summary>
        private static void Answer(
            List<Cell> answers,
            AgeControlButton button,
            AgePrimitiveLabel caption,
            string key,
            Action fallback
        )
        {
            AgeTransform widget = button == null ? null : button.AgeTransform;
            if (widget == null)
            {
                return;
            }

            string name = AgeText.Label(caption);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            AgeControlButton it = button;
            Action route = fallback;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Button(
                () => name,
                () => Press(it, route),
                () => AgeWidgets.Offered(widget),
                tooltip
            );
            if (tooltip == null)
            {
                // The question, under an answer the game said nothing else about: whichever button the
                // player is on, the text they are answering is worth re-reading.
                vtable.Sections = GraphNodes.Sections(PromptLines, null);
            }

            AgeWidgets.Point(vtable, it, tooltip, widget);
            Cells.Add(answers, widget, ControlId.For(button, "non-blocking:" + key), vtable);
        }

        /// <summary>Press the button the way the engine presses it - replaying the object and method its
        /// own mouse handler sends to, so the caller receives the answer a mouse would have given. Both
        /// handlers are private on the window, which is why an unwired button falls back on the window's
        /// own input route: it reaches the same two.</summary>
        private static void Press(AgeControlButton button, Action fallback)
        {
            try
            {
                if (
                    button.OnActivateObject != null
                    && !string.IsNullOrEmpty(button.OnActivateMethod)
                )
                {
                    AgeWidgets.Press(button);
                    return;
                }
            }
            catch (Exception e)
            {
                Log.Warn("non-blocking box: pressing an answer threw: " + e);
            }

            fallback();
        }

        private static readonly Action Validate = () => Route(GameInput.Validate);

        private static readonly Action Cancel = () => Route(GameInput.Exit);

        private static void Route(Amplitude.StaticString inputAction)
        {
            try
            {
                MessageBoxNonBlockingWindow window = Window();
                if (window != null)
                {
                    window.HandleInput(inputAction);
                }
            }
            catch (Exception e)
            {
                Log.Warn("non-blocking box: the " + inputAction + " route threw: " + e);
            }
        }

        /// <summary>The question as one spoken line, resolved every time it is asked for - a timed box's
        /// message is a different sentence each frame. The game wraps a long question over as many lines
        /// as the box is wide, so its line breaks are where the words ran out and not punctuation; they
        /// are joined with a space, which is the sentence the caller wrote.</summary>
        private static string Question()
        {
            MessageBoxNonBlockingWindow window = Window();
            return window == null
                ? null
                : SettingRows.OneLine(AgeText.Label(window.MessageLabel));
        }

        /// <summary>The question as the review buffer holds it: its heading, then its message a line at a
        /// time.</summary>
        private static IList<string> PromptLines()
        {
            List<string> lines = new List<string>();
            MessageBoxNonBlockingWindow window = Window();
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
                Log.Warn("non-blocking box: reading the question threw: " + e);
            }

            return lines;
        }

        private static string Heading()
        {
            MessageBoxNonBlockingWindow window = Window();
            try
            {
                return window == null ? null : AgeText.Label(window.TitleLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static MessageBoxNonBlockingWindow Window()
        {
            return GameWindows.Of<MessageBoxNonBlockingWindow>();
        }
    }
}

using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The box the game throws up when something inside it has gone wrong: a message, a call stack, and
    /// three things the player can do about it (<c>ErrorModalWindow</c>; raised by
    /// <c>GuiManager</c>:2377 from the engine's own error handler).
    ///
    /// It is worth a screen for one reason: without one, an unsighted player's game silently stops
    /// responding. The box takes the keyboard - it is a modal above everything - and nothing about it is
    /// spoken, so the player is left pressing keys at a game that has already told them, on screen, that
    /// it is broken and is waiting to be told whether to carry on.
    ///
    /// So the MESSAGE is what focus lands on and what arriving reads, and the three buttons are walked
    /// beside it. The buttons are read off what the window draws (<see cref="WindowShape"/>) rather than
    /// out of its fields: the window names two of the three (Continue, Quit) and leaves the copy button
    /// to its own handler, and all three carry the words the prefab wrote on them.
    ///
    /// The CALL STACK is review-buffer content on the message rather than a node of its own. It is forty
    /// lines of frame names that mean nothing to a player and everything to whoever they report the
    /// crash to - so it must be readable, line by line, and must not be in the way. The copy button is
    /// the answer for actually getting it out of the game, and it puts the message, the stack and the
    /// build number on the clipboard in one go.
    ///
    /// Escape is the game's, and here that means NOTHING: <c>ErrorModalWindow</c> is not an input
    /// handler, so the box can only be left through its own buttons. That is the game's decision and it
    /// is left alone - a mod-invented Escape would dismiss an error report the player may still want.
    /// </summary>
    public sealed class ErrorScreen : Screen
    {
        /// <summary>The mod's own word for the box, since the window draws no heading at all. Optional:
        /// a build without the phrase says nothing rather than reading the key
        /// (<see cref="OptionalText"/>).</summary>
        private const string ScreenNameKey = "screen.error";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.error"; }
        }

        /// <summary>
        /// Above every ordinary screen, above the non-blocking box, and above the tutorial popup,
        /// with the confirmation box alone above it - the one thing the game can raise over anything,
        /// including over an error.
        ///
        /// Above the TUTORIAL specifically because this box is the only way out of itself: the window
        /// handles no input, so nothing but its own three buttons dismisses it, and a tutorial popup
        /// drawn over it would own the keyboard while the thing waiting to be answered sat
        /// unreachable underneath. The tutorial can be minimised and come back; an error the tutorial
        /// buries is unanswerable, and the game behind it has already stopped responding.
        /// </summary>
        public override int Layer
        {
            get { return 99; }
        }

        public override string ScreenName
        {
            get { return OptionalText.Phrase(ScreenNameKey); }
        }

        public override bool IsActive()
        {
            ErrorModalWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's - which for this window is nobody's. It handles no input, so the key
        /// falls through to whatever is underneath rather than being swallowed here.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            ErrorModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            AgePrimitiveLabel message = window.ErrorMessage;
            AgeTransform widget = message == null ? null : message.AgeTransform;
            if (widget != null && AgeWidgets.Visible(widget))
            {
                AgePrimitiveLabel it = message;
                ControlId id = ControlId.Referenced(message, "error:message");
                builder.AddNode(
                    id,
                    new NodeVtable
                    {
                        // No role word: this is not a control the player works, it is what the game is
                        // telling them. The stack sits behind it as reviewable lines.
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => AgeText.Label(it)),
                        },
                        Sections = GraphNodes.Sections(() => Report(window), null),
                        OnFocusVisual = AgeWidgets.ReleasePointer,
                    }
                );
                builder.SetStart(id);
            }

            _cells.Clear();
            WindowShape.Controls(_cells, window, "error");
            Cells.Emit(builder, _cells);
        }

        /// <summary>What the box says, as the buffer holds it: the message a line at a time, then the
        /// call stack a frame at a time. The stack is where it is because a player who has to read it out
        /// to somebody needs it in lines, and nowhere else in the box is there room for it.</summary>
        private static IList<string> Report(ErrorModalWindow window)
        {
            List<string> lines = new List<string>();
            try
            {
                foreach (string line in AgeText.Lines(AgeText.Label(window.ErrorMessage)))
                {
                    lines.Add(line);
                }

                foreach (string line in AgeText.Lines(AgeText.Label(window.CallStack)))
                {
                    lines.Add(line);
                }
            }
            catch (Exception) { }

            return lines;
        }

        private static ErrorModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<ErrorModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The little anchored question the scan view asks beside a node (<c>ContextualPromptWindow</c>).
    ///
    /// It is one window with four shapes, and the game keeps all four in its own data
    /// (<c>Public/Gui/GuiElements[ContextualPrompts].xml</c>, read into a <c>ContextualPromptGuiElement</c>):
    /// a heading, a paragraph, a strip of buttons cloned from one prefab, an optional cross, and a
    /// COMPONENT - a prefab of the prompt's own choosing dropped into the middle of it. Every one of the
    /// four is hacking (Penumbra): confirm this node as an operation's target, choose which operation to
    /// toggle, pick the operation a program goes on, and the warning listing what is over-allocating the
    /// empire's bandwidth. So the shapes are the game's, not this screen's, and the reading is written
    /// against the window rather than against any one of them.
    ///
    /// The keyboard needs this window for a reason no other window in the game has: **it has no keyboard
    /// dismissal at all**. It is a plain <c>GuiWindow</c> with no <c>HandleInput</c>, and the scan view
    /// above it answers Exit by closing its own dashboard mode and returning true
    /// (<c>ScanOverlayWindow.HandleInput</c> :145-181) - so Escape is swallowed and the prompt stays. Its
    /// three real dismissals are all mouse: the cross, a right click, and a click on the sheet behind it.
    /// Hence <see cref="Back"/>, which calls the game's own <c>OnCloseCb</c> - the very method the cross
    /// is wired to, so the client is told the prompt closed exactly as it would have been.
    ///
    /// What is READ is the shape the window drew, in two stops: what it says, and what can be pressed.
    /// The component is swept with the shared shape reading rather than modelled, because it is a prefab
    /// path in a data file - a fifth one can be added without touching C# - and because no component has
    /// ever been sighted with CONTENT in it. Three of the four exist:
    /// <c>HackingOperationValidationComponent</c> writes one line (the estimated duration, plus a warning
    /// where the route crosses a backdoor of the player's own), and
    /// <c>HackingOperationSelectorComponent</c> and <c>AllocationProvidersListComponent</c> each clone a
    /// line per operation or per allocation. Reaching one with rows in it needs a running hacking
    /// operation, which needs Penumbra AND a game far enough along to have one; when that fixture exists,
    /// re-measure whether the component's rows want a stop of their own (a list of operations to choose
    /// between is a choice, and a choice reads better alone than mixed in with Yes).
    ///
    /// The cross carries no words and no sentence, so the shared control reading drops it and it is
    /// declared here under the mod's own name. The sheet behind the prompt is left undeclared: it is a
    /// click-catcher for dismissing by clicking away, which is what Escape now does.
    /// </summary>
    public sealed class ContextualPromptScreen : Screen
    {
        private static readonly object WordsStop = "prompt:words";
        private static readonly object ControlsStop = "prompt:controls";
        private static readonly object CloseStop = "prompt:close";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.contextual-prompt"; }
        }

        /// <summary>The window sits LAST on the game's own modal stack
        /// (<c>GuiWindowsStackDefinition.xml</c> :172), which draws it over every modal in that stack,
        /// and the prompt is a question the player is being asked - so it goes above everything except
        /// the three surfaces that must stay answerable over it (the non-blocking box, the tutorial page
        /// and the two boxes).</summary>
        public override int Layer
        {
            get { return 96; }
        }

        /// <summary>What the prompt asks, where it wrote a heading. Three of the four do; the
        /// bandwidth warning writes only a paragraph, and naming the screen after that paragraph would
        /// say the whole warning twice, so the mod's own word stands in and the paragraph is read as the
        /// first thing on the page.</summary>
        public override string ScreenName
        {
            get
            {
                string title = AgeText.Label(Title());
                return string.IsNullOrEmpty(title) ? ModStrings.Get(ModStrings.PromptScreen) : title;
            }
        }

        public override object InitialFocusStop
        {
            get { return WordsStop; }
        }

        /// <summary>Escape closes the prompt, which is what the cross does. Nothing is committed by
        /// closing: every prompt's answer is a button, and a prompt dismissed leaves the cursor mode it
        /// was asked from exactly where it was.</summary>
        public override bool Back()
        {
            try
            {
                ContextualPromptWindow window = Window();
                if (window == null)
                {
                    return false;
                }

                window.OnCloseCb();
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("contextual prompt: closing threw: " + e);
                return false;
            }
        }

        public override bool ConsumesBack
        {
            get { return true; }
        }

        /// <summary>Ours while the game is drawing the prompt and it still holds the element it was
        /// bound to - <c>Unbind</c> drops that element as it hides, so the two together outlive neither
        /// the fade nor a prompt that was never bound.</summary>
        public override bool IsActive()
        {
            try
            {
                ContextualPromptWindow window = Window();
                return window != null && window.Shown && window.PromptGuiElement != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            ContextualPromptWindow window = Window();
            if (window == null || !window.Shown)
            {
                return;
            }

            try
            {
                BuildWords(builder, window);
                BuildControls(builder, window);
                BuildClose(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("contextual prompt: reading the window threw: " + e);
            }
        }

        /// <summary>What the prompt says: its paragraph, and then whatever its component wrote. The
        /// heading is the screen's name and is not declared again.</summary>
        private void BuildWords(GraphBuilder builder, ContextualPromptWindow window)
        {
            _cells.Clear();
            Cells.AddReadout(_cells, Widget(window.DescriptionLabel), "prompt:description");
            WindowShape.Readouts(_cells, window.ComponentsTable, "prompt:component");
            if (_cells.Count > 0)
            {
                builder.BeginStop(WordsStop);
                Cells.EmitLinear(builder, _cells);
            }
        }

        /// <summary>How the prompt is ANSWERED: the buttons its data named, and whatever its component
        /// made clickable. Both carry a caption or a sentence of their own, so the shared shape reading
        /// finds them. The cross is excluded and gets a stop of its own below.</summary>
        private void BuildControls(GraphBuilder builder, ContextualPromptWindow window)
        {
            _cells.Clear();
            WindowShape.Controls(_cells, window, "prompt", window.CloseButton);
            if (_cells.Count > 0)
            {
                builder.BeginStop(ControlsStop);
                Cells.EmitLinear(builder, _cells);
            }
        }

        /// <summary>
        /// The cross in the prompt's top corner, drawn only where the prompt's data asked for one
        /// (<c>ShowCloseButton</c>, and <c>OnBeginShow</c> hides it otherwise). It carries no words and
        /// no sentence, so the shared control reading drops it and it is named here.
        ///
        /// It gets a stop of its own, after the answers, for the reason the drawn order would otherwise
        /// defeat: the cross sits in the title bar ABOVE the button strip, so banding the two together
        /// puts "Close" first and lands arrival on the way OUT of a question rather than on its answer.
        /// The same shape as the close stop the system-politics modal keeps.
        /// </summary>
        private void BuildClose(GraphBuilder builder, ContextualPromptWindow window)
        {
            AgeTransform close = window.CloseButton;
            AgeControlButton button = AgeWidgets.Visible(close) ? AgeWidgets.Button(close) : null;
            if (button == null)
            {
                return;
            }

            builder.BeginStop(CloseStop);
            _cells.Clear();
            _cells.Add(
                Cells.Control(
                    close,
                    button,
                    AgeWidgets.Raw(close),
                    ModStrings.Get(ModStrings.PromptClose),
                    "prompt:close"
                )
            );
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The heading, and only where the prompt is DRAWING one. <c>Refresh</c> hides the
        /// label for a prompt whose data gave it no title but leaves the previous prompt's words sitting
        /// in it, so reading the text without the visibility test names the bandwidth warning after
        /// whatever question was asked before it (measured).</summary>
        private static AgePrimitiveLabel Title()
        {
            ContextualPromptWindow window = Window();
            AgePrimitiveLabel label = window == null ? null : window.TitleLabel;
            return label != null && AgeWidgets.Visible(label.AgeTransform) ? label : null;
        }

        private static AgeTransform Widget(AgePrimitiveLabel label)
        {
            try
            {
                return label == null || !AgeWidgets.Visible(label.AgeTransform)
                    ? null
                    : label.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static ContextualPromptWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<ContextualPromptWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

// The game has its own CreditScreen in the global namespace; this adapts it, so the two names have to
// coexist.
using GameCreditScreen = CreditScreen;

namespace ES2Access.Screens
{
    /// <summary>
    /// The credit roll (<c>CreditScreen</c>).
    ///
    /// The page is one thing: six hundred lines of text scrolling up the screen on a timer, with nothing on
    /// it to press. So it is ONE node holding the whole roll in its review buffer, and that is the model
    /// rather than a step on the way to one.
    ///
    /// Nodes are wrong here for two reasons, both measured. The roll is 598 items and the graph is rebuilt
    /// on every keypress, so declaring one node each would cost that walk per operation for a page nobody
    /// operates; and the reading a player wants is continuous prose at their own pace, which is exactly what
    /// a review buffer gives (Ctrl+Down walks it line by line, and the words never move because they are
    /// read from the items rather than from what is on screen this second).
    ///
    /// Read from the items the screen instantiated rather than from the text asset behind them: the screen
    /// builds every child up front in <c>OnBeginShow</c> and then only toggles their visibility as they
    /// scroll past (<c>SpecificUpdate</c>), so the whole roll exists from the first frame - and reading the
    /// widgets is what keeps this honest if the game ever changes what it puts in them. Each item is one of
    /// the three the game builds: a heading, a credit line (a job and a person, said as one line the way it
    /// is drawn), or a paragraph. The fourth kind is an image, and it contributes nothing.
    ///
    /// Nothing else is declared, and the shape floor is deliberately NOT run here: the page draws no
    /// captioned control at all, and scanning two thousand components for one every frame is a cost with
    /// nothing on the other side of it.
    ///
    /// The page LEAVES BY ITSELF. The roll's own coroutine calls Exit when the scroll reaches the end
    /// (<c>WaitEndOfDuration</c>), which on this build is about eight and a half minutes; Escape does the
    /// same at any time (<c>HandleInput</c> shows the main menu again).
    /// </summary>
    public sealed class CreditsScreen : MenuDestinationScreen
    {
        private static readonly object RollStop = "credits:roll";

        public override string Key
        {
            get { return "screen.credits"; }
        }

        protected override string Prefix
        {
            get { return "credits"; }
        }

        protected override string ScreenNameKey
        {
            get { return "screen.credits"; }
        }

        protected override GuiWindow Window()
        {
            return Get<GameCreditScreen>();
        }

        public override void Build(GraphBuilder builder)
        {
            GameCreditScreen window = Window() as GameCreditScreen;
            AgeTransform content = Content(window);
            if (content == null)
            {
                return;
            }

            AgeTransform roll = content;
            NodeVtable vtable = GraphNodes.Readout(
                () => ModStrings.Get(ModStrings.CreditsRoll),
                // How much there is to read, counted off the roll rather than said as a number of
                // widgets. Not watched: it is a walk of six hundred children and it never changes while
                // the page is up.
                () =>
                    ModStrings.Plural(
                        ModStrings.CreditsLine,
                        ModStrings.CreditsLines,
                        Lines(roll).Count
                    ),
                () => Lines(roll),
                null,
                false
            );
            builder.BeginStop(RollStop);
            builder.AddItem(Nodes.Drawn(ControlId.For(content, "credits:roll"), vtable, content));
        }

        /// <summary>Every line of the roll, in the order the screen laid them out.</summary>
        private static IList<string> Lines(AgeTransform content)
        {
            List<string> lines = new List<string>();
            try
            {
                IList<AgeTransform> items = content.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    string said = Item(items[i]);
                    if (!string.IsNullOrEmpty(said))
                    {
                        lines.Add(said);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("credits: reading the roll threw: " + e);
            }

            return lines;
        }

        /// <summary>One item of the roll as one line. A credit line is a job and a person drawn side by
        /// side, which is one fact and so one line; an image is nothing to say.</summary>
        private static string Item(AgeTransform item)
        {
            if (item == null)
            {
                return null;
            }

            CreditLineItem credit = item.GetComponent<CreditLineItem>();
            if (credit != null)
            {
                return new MessageBuilder()
                    .ListItem(AgeText.Label(credit.Title))
                    .ListItem(AgeText.Label(credit.Name))
                    .Build();
            }

            CreditHeaderItem heading = item.GetComponent<CreditHeaderItem>();
            if (heading != null)
            {
                return AgeText.Label(heading.HeaderTitle);
            }

            CreditParagraphItem paragraph = item.GetComponent<CreditParagraphItem>();
            return paragraph == null ? null : AgeText.Label(paragraph.ParagraphContent);
        }

        private static AgeTransform Content(GameCreditScreen window)
        {
            try
            {
                return window == null ? null : window.CreditsContent;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

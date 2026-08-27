using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// "You have won" - or lost - said once, in the box the game raises the moment a victory condition is
    /// met (<c>VictoryAchievedModalWindow</c>).
    ///
    /// Everything about it is text the game has already written: the outcome across the top ("Victory",
    /// "Defeat"), a paragraph naming who won and by which condition, and - where more than one empire or
    /// alliance qualified at once - a line per winner and per victory in a list under it. All of it is read
    /// as drawn, because the game's own sentence about how the game ended is exactly the sentence to say.
    ///
    /// Two things the player can do, and they are not equivalent: Continue puts the box away and lets the
    /// game go on (single-player only - the window hides that button in a multiplayer session), and the
    /// score screen ENDS the session and disconnects. So both are declared, named by whatever the prefab
    /// wrote on them, and the score button carries the game's own sentence for why it is refusing while a
    /// save is still being written (<c>DisableButtonIfSavingOrNotReady</c>).
    ///
    /// The outcome is what focus lands on and what arriving says, in that one place: the heading is the
    /// screen's name, and the paragraph under it is the first node - the same split the message box makes,
    /// for the same reason.
    ///
    /// Escape is the game's and here does NOTHING: <c>HandleInput</c> returns true for every action
    /// without acting. The end of a game is not something a stray key should dismiss, and that is the
    /// game's decision.
    /// </summary>
    public sealed class VictoryAchievedScreen : Screen
    {
        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.victory-achieved"; }
        }

        /// <summary>Above the notification popups the end of a turn raises and above the game menu, and
        /// below the message box alone.</summary>
        public override int Layer
        {
            get { return 49; }
        }

        /// <summary>The game's own word for how it ended.</summary>
        public override string ScreenName
        {
            get
            {
                VictoryAchievedModalWindow window = Window();
                try
                {
                    return window == null ? null : AgeText.Label(window.OutcomeTitle);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public override bool IsActive()
        {
            try
            {
                VictoryAchievedModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's, which for this window means nobody's: it answers every action by
        /// swallowing it.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            VictoryAchievedModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            _cells.Clear();
            try
            {
                Cells.AddReadout(
                    _cells,
                    Transform(window.DescriptionLabel),
                    "victory-achieved:description"
                );
                Winners(window);
            }
            catch (Exception e)
            {
                Log.Warn("victory: reading the outcome threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            if (_cells.Count > 0)
            {
                builder.SetStart(_cells[0].Id);
            }

            _cells.Clear();
            WindowShape.Controls(_cells, window, "victory-achieved");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>A line per winner and per victory, where the game drew a list at all - it draws one
        /// only when more than one empire qualified, and folds the single-winner case into the paragraph
        /// above.</summary>
        private void Winners(VictoryAchievedModalWindow window)
        {
            AgeTransform list = window.WinnerList;
            // Flow control: the list under a group the window did not draw is still full of rows, each
            // of which would be read for its text before the gate could drop it.
            if (!AgeWidgets.Visible(window.WinnerListGroup) || list == null)
            {
                return;
            }

            IList<AgeTransform> children = list.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                Cells.AddReadout(_cells, children[i], "victory-achieved:winner/" + i);
            }
        }

        private static AgeTransform Transform(AgePrimitiveLabel label)
        {
            try
            {
                return label == null ? null : label.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static VictoryAchievedModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<VictoryAchievedModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

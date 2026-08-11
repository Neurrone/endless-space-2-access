using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

// The game has its own VictoryScreen in the global namespace; this adapts it, so the two names have to
// coexist.
using GameVictoryScreen = VictoryScreen;

namespace ES2Access.Screens
{
    /// <summary>
    /// The score screen a finished game ends on - and the same screen the journal opens for a game that
    /// finished long ago (<c>VictoryScreen</c>, bound with <c>fromJournal</c> either way).
    ///
    /// It is a tab bar over a stack of panels: pick a page across the top, and one panel at a time is
    /// shown (<c>OnScreenSelectedCb</c> hides the old and shows the new). So the tabs are a one-of-N and
    /// the visible panel's contents follow underneath, which is what the eye does with it.
    ///
    /// What a panel HOLDS is not modelled here, and that is a deliberate stopping point: the score panels
    /// are graphs of every empire's score per turn, podiums, and trivia items, each a picture with its
    /// numbers in the sentences the game writes beside them. Reading them properly is a screen's worth of
    /// work for a page a player sees once per game, after the game is over. What is here instead is every
    /// LINE the panel drew, in the rows it drew them (<see cref="WindowShape.Readouts"/>) - which is the
    /// empire names, the scores and the trivia, and is enough to hear how the game went. A real model of
    /// the graphs is future work and is marked as such.
    ///
    /// Which way OUT is drawn depends on where the player came from: a game that just ended offers "back
    /// to menu" and a route into the journal, and a game opened FROM the journal offers a way back to it
    /// (<c>Bind</c> sets the three buttons' visibility). All of them are read off what is drawn, so the
    /// screen does not have to know which case it is in.
    ///
    /// Layer 0, with the main menu and the new-game lobby: this is another out-of-game page that REPLACES
    /// the menu rather than floating over it - the menu is hidden while it is up and shown again when it
    /// closes (<c>BackToPreviousMenu</c>), so the two are never both live.
    ///
    /// Escape is the game's: the window answers it by going back where the player came from, menu or
    /// journal, which is a different destination in each case and one only the game knows.
    /// </summary>
    public sealed class VictoryScreen : Screen
    {
        private static readonly object TabsStop = "victory:tabs";
        private static readonly object PanelStop = "victory:panel";
        private static readonly object ActionsStop = "victory:actions";

        /// <summary>The mod's own name for the page, since it writes no heading of its own - the outcome
        /// is drawn as artwork. Optional: a build without the phrase says nothing rather than reading the
        /// key.</summary>
        private const string ScreenNameKey = "screen.victory";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.victory"; }
        }

        /// <summary>With the main menu and the lobby: an out-of-game page that replaces the menu rather
        /// than covering it.</summary>
        public override int Layer
        {
            get { return 0; }
        }

        public override string ScreenName
        {
            get
            {
                string title = WindowShape.Title(Window());
                return string.IsNullOrEmpty(title) ? OptionalText.Phrase(ScreenNameKey) : title;
            }
        }

        /// <summary>The tabs, because they are drawn first and decide what the rest of the page is.
        /// </summary>
        public override object InitialFocusStop
        {
            get { return TabsStop; }
        }

        public override bool IsActive()
        {
            try
            {
                GameVictoryScreen window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: Exit goes back to wherever the player came from.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            GameVictoryScreen window = Window();
            if (window == null)
            {
                return;
            }

            builder.BeginStop(TabsStop);
            Tabs(builder, window);

            builder.BeginStop(PanelStop);
            Panel(builder, window);

            builder.BeginStop(ActionsStop);
            _cells.Clear();
            WindowShape.Controls(_cells, window, "victory", Tables(window));
            Cells.Emit(builder, _cells);
        }

        /// <summary>The pages across the top, as the one-of-N the game made them.</summary>
        private void Tabs(GraphBuilder builder, GameVictoryScreen window)
        {
            _cells.Clear();
            try
            {
                AgeTransform table = Tables(window);
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    Tab(children[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("victory: reading the tabs threw: " + e);
            }

            Cells.Emit(builder, _cells);
        }

        private void Tab(AgeTransform widget, int index)
        {
            AgeControlToggle toggle = Toggle(widget);
            if (toggle == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Tab(
                () => Name(at, tooltip),
                () => it.State,
                () => AgeWidgets.Offered(at),
                tooltip
            );
            vtable.OnActivate = () => AgeWidgets.Toggle(it);
            AgeWidgets.Point(vtable, it, tooltip, at);
            Cells.Add(_cells, widget, ControlId.Structural("victory:tab/" + index), vtable);
        }

        /// <summary>Everything the shown panel has written, in the rows it drew. The game shows exactly
        /// one panel, so what is here follows the tab the player picked without this having to know which
        /// panel is which.</summary>
        private void Panel(GraphBuilder builder, GameVictoryScreen window)
        {
            _cells.Clear();
            try
            {
                VictoryScreenPanel[] panels = window.Panels;
                for (int i = 0; panels != null && i < panels.Length; i++)
                {
                    AgeTransform widget = Transform(panels[i]);
                    if (AgeWidgets.Visible(widget))
                    {
                        WindowShape.Readouts(_cells, widget, "victory:panel/" + i);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("victory: reading the shown panel threw: " + e);
            }

            Cells.Emit(builder, _cells);
        }

        /// <summary>What a tab is called: the words the game drew on it, else the sentence its tooltip
        /// opens with - these are drawn as icons on some pages.</summary>
        private static string Name(AgeTransform widget, AgeTooltip tooltip)
        {
            string drawn = AgeWidgets.TextOf(widget);
            return string.IsNullOrEmpty(drawn) ? CardActions.FirstLine(tooltip) : drawn;
        }

        private static AgeTransform Tables(GameVictoryScreen window)
        {
            try
            {
                GuiRadioGroup group = window.ScreenSelection;
                return group == null ? null : group.TogglesTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeControlToggle Toggle(AgeTransform widget)
        {
            try
            {
                return widget == null
                    ? null
                    : widget.GetComponentInChildren<AgeControlToggle>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Transform(VictoryScreenPanel panel)
        {
            try
            {
                return panel == null ? null : panel.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GameVictoryScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GameVictoryScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

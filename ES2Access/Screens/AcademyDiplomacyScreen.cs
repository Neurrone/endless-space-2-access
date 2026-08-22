using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The Academy's diplomacy window: what the diplomacy page's Academy button opens, and what the
    /// Academy's own window switches to.
    ///
    /// Four bands: the relation - the state, the number with its trend, the rich tooltip listing the eight
    /// things feeding that trend, and the game's own sentence for what THIS state does to the player
    /// (<c>%AcademyDiplomacyStateEffect*</c>, one of five, :375-408) - then the Academy's own box, reused
    /// from the hero page (<see cref="SidePanels"/>), then the actions, then the two stocks.
    ///
    /// The actions are the shared band (<see cref="DiplomacyActions"/>): this window's items are the same
    /// prefab shape as the minor-faction and pirate windows'.
    ///
    /// <b>One action is a kept affordance with a dead end past it.</b> Giving the Academy a system
    /// (<c>AcademyDiplomacyGiveSystemAction.ClientStart</c> :41-53) does not act: it closes the window and
    /// puts the game into a CURSOR MODE where the next click on the galaxy map picks the system, with the
    /// instruction drawn across the screen (<c>%UserIntructionCaptionTakeSystemTitle</c>). The action is
    /// declared anyway - hiding a thing the game offers is worse than offering a step the player cannot yet
    /// finish, and the same reasoning kept the hero card's Inspect button before the inspection window was
    /// modelled - and the map-cursor half is a separate piece of work.
    ///
    /// Escape is the game's. This window and the Academy's own can never be up together: each switch hides
    /// itself first (:452-458 exits before showing the other), which is why they share a layer.
    /// </summary>
    public sealed class AcademyDiplomacyScreen : Screen
    {
        private static readonly object RelationStop = "academy-diplomacy:relation";
        private static readonly object AcademyStop = "academy-diplomacy:academy";
        private static readonly object ActionsStop = "academy-diplomacy:actions";
        private static readonly object TreasuryStop = "academy-diplomacy:treasury";

        private const string Keys = "academy-diplomacy:";

        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<DiplomacyActions.Row> _actions = new List<DiplomacyActions.Row>();

        public override string Key
        {
            get { return "screen.academy-diplomacy"; }
        }

        /// <summary>Shared with the Academy's own window, above the hero inspection window at 45.</summary>
        public override int Layer
        {
            get { return 46; }
        }

        public override string ScreenName
        {
            get { return ModStrings.Get(ModStrings.ScreenAcademyDiplomacy); }
        }

        public override object InitialFocusStop
        {
            get { return ActionsStop; }
        }

        public override bool ConsumesBack
        {
            get { return false; }
        }

        /// <summary>Arrival waits for the Academy to be bound - the window's <c>Bind</c> takes no
        /// arguments and reads the game's Academy straight out (it throws outright where the expansion that
        /// adds one is not installed), so the bound Academy is the one thing that says the window is really
        /// filled in.</summary>
        public override bool IsActive()
        {
            try
            {
                AcademyDiplomacyModalWindow window = Window();
                return window != null
                    && window.Shown
                    && window.IsReady
                    && window.Academy != null
                    && !Buried(window);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Buried(GuiModalWindow window)
        {
            try
            {
                GuiManager manager = Gui.GuiGameWindowService as GuiManager;
                GuiModalWindow top = manager == null ? null : manager.ModalOnTop;
                return top != null && !ReferenceEquals(top, window);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            AcademyDiplomacyModalWindow window = Window();
            if (window == null || window.Academy == null)
            {
                return;
            }

            BuildRelation(builder, window);
            BuildAcademy(builder, window);
            BuildActions(builder, window);
            BuildTreasury(builder, window);
        }

        private void BuildRelation(GraphBuilder builder, AcademyDiplomacyModalWindow window)
        {
            builder.BeginStop(RelationStop);
            builder.PushContext(ModStrings.Get(ModStrings.AcademyRelation));
            _cells.Clear();
            try
            {
                // The state's own word says what it is ("FRIENDLY") and the game keeps no title for
                // it anywhere, so it is a readout with the state's description behind it. The number
                // beside it says nothing at all on its own, and the game DOES title that one
                // (%AcademyRelationPointsTitle) - so it is captioned, the same ruling the minor
                // window's figures are read under (owner 2026-08-22).
                Cells.AddReadout(_cells, Of(window.RelationLabel), Keys + "relation");
                Cells.AddStat(
                    _cells,
                    window.RelationTrendLabel,
                    "%AcademyRelationPointsTitle",
                    Keys + "trend"
                );
                Cells.AddReadout(_cells, Of(window.RelationEffectsLabel), Keys + "effects");
                Cells.AddReadout(_cells, Of(window.RelationEffectNoneLabel), Keys + "no-effects");
                AgeTransform power = window.AcademyPowerGauge == null
                    ? null
                    : window.AcademyPowerGauge.AgeTransform;
                if (power != null && AgeWidgets.Visible(power))
                {
                    _cells.Add(Cells.Readout(power, AgeWidgets.Raw(power), Keys + "power"));
                }
            }
            catch (Exception e)
            {
                Log.Warn("academy diplomacy: reading the relation threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>The Academy's own box, which this window embeds rather than draws: the same
        /// <c>AcademyInfoSidePanel</c> the hero page keeps down its left edge, so it is read by the same
        /// shared reader.</summary>
        private void BuildAcademy(GraphBuilder builder, AcademyDiplomacyModalWindow window)
        {
            SidePanel panel = window.AcademyInfoSidePanel;
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(AcademyStop);
            builder.PushContext(SidePanels.Name(panel));
            _cells.Clear();
            try
            {
                SidePanels.Readouts(_cells, panel, Keys + "academy/", null, null);
            }
            catch (Exception e)
            {
                Log.Warn("academy diplomacy: reading the academy box threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>The diplomatic actions, under the word the window itself draws over them. The game
        /// captions this band "Actions" and hangs the sentence saying what it is on the GROUP around the
        /// label, so the caption is a row as well as the band's name - the same shape the minor-faction
        /// window's band is read under, with the mod's own word left as the fallback.</summary>
        private void BuildActions(GraphBuilder builder, AcademyDiplomacyModalWindow window)
        {
            builder.BeginStop(ActionsStop);
            AgeTransform title = ActionsCaption(window);
            bool named = Captions.Push(
                builder,
                title,
                Keys + "actions-title",
                Captions.Text(title) ?? ModStrings.Get(ModStrings.DiplomacyActionsBand)
            );
            _actions.Clear();
            try
            {
                AgeTransform table = window.AcademyActionsTable;
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform at = children[i];
                    DiplomacyActions.Add(
                        _actions,
                        at == null ? null : at.GetComponent<AcademyDiplomacyActionItem>()
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("academy diplomacy: reading the actions threw: " + e);
            }

            DiplomacyActions.Emit(builder, "academy-diplomacy", _actions);
            AcademyWindows.Switch(builder, window, Keys);
            Captions.Pop(builder, named);
        }

        /// <summary>The group the window draws the actions caption inside - the widget carrying both the
        /// word and the sentence, neither of which the window exposes.</summary>
        private static AgeTransform ActionsCaption(AcademyDiplomacyModalWindow window)
        {
            try
            {
                AgeTransform label = window == null
                    ? null
                    : AgeWidgets.ChildNamed(window.AgeTransform, "ActionsTitle", 4);
                return label == null ? null : label.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void BuildTreasury(GraphBuilder builder, AcademyDiplomacyModalWindow window)
        {
            builder.BeginStop(TreasuryStop);
            builder.PushContext(ModStrings.Get(ModStrings.DiplomacyTreasury));
            _cells.Clear();
            try
            {
                DiplomacyActions.Treasury(_cells, window.MoneyLabel, window.EmpirePointLabel, Keys);
            }
            catch (Exception e)
            {
                Log.Warn("academy diplomacy: reading the treasury threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        private static AgeTransform Of(AgePrimitiveLabel label)
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

        private static AcademyDiplomacyModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<AcademyDiplomacyModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

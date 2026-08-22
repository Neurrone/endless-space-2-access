using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// Talking to a minor faction: the window a system label's diplomacy button opens for a minor
    /// empire's home system, and the one the "you have met somebody" popup leads to.
    ///
    /// Four things about them, in the order the window draws them: who they are and what their two traits
    /// do; how they feel about the player and what is pushing that number up or down; what the player is
    /// getting out of the relationship; and the things the player can do about it. The pair of stock
    /// figures along the bottom is the fifth, and is the same pair every window in this family draws.
    ///
    /// The band of actions is the shared one (<see cref="DiplomacyActions"/>): this window, the pirate
    /// window and the Academy window draw three separate prefab classes of identical shape, and it is
    /// read once. Two things are particular to this window and are noted rather than special-cased: an
    /// action can HIDE the window when it takes (:536-546), which is the window closing under the cursor
    /// and is what the mod's departure gate is for; and a row can draw a quest button while that
    /// faction's quest is running, whose Enter opens the quest journal and hides this window.
    ///
    /// The gauge's own click is the developers' (<c>OnGaugeButtonCb</c> :553-566 posts relation points),
    /// so the gauge is a readout and the relation number beside it is where the value is read.
    ///
    /// The window closes ITSELF when the faction dies, becomes unknown or is assimilated (:568-578), and
    /// refuses to open at all for one that is already integrated (:168-201) - so nothing here tests those
    /// states: it follows the window.
    /// </summary>
    public sealed class MinorFactionDiplomacyScreen : Screen
    {
        private static readonly object IdentityStop = "minor:identity";
        private static readonly object RelationStop = "minor:relation";
        private static readonly object GainsStop = "minor:gains";
        private static readonly object ActionsStop = "minor:actions";
        private static readonly object TreasuryStop = "minor:treasury";

        private const string Keys = "minor:";

        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<DiplomacyActions.Row> _actions = new List<DiplomacyActions.Row>();

        public override string Key
        {
            get { return "screen.minor-diplomacy"; }
        }

        /// <summary>Over the star-system page and the galaxy map that open it; 42 is the advanced battle
        /// report, so this family starts at 43.</summary>
        public override int Layer
        {
            get { return 43; }
        }

        public override string ScreenName
        {
            get
            {
                MinorFactionDiplomacyModalWindow window = Window();
                string drawn = window == null ? null : Words(window.EmpireNameLabel);
                return string.IsNullOrEmpty(drawn)
                    ? ModStrings.Get(ModStrings.ScreenMinorDiplomacy)
                    : drawn;
            }
        }

        public override object InitialFocusStop
        {
            get { return ActionsStop; }
        }

        public override bool ConsumesBack
        {
            get { return false; }
        }

        /// <summary>Arrival waits for the faction to be bound, because that is what the window bails
        /// without; departure comes when it is unbound, or when another modal takes the screen (an
        /// action's confirmation box, the quest journal a quest row opens).</summary>
        public override bool IsActive()
        {
            try
            {
                MinorFactionDiplomacyModalWindow window = Window();
                return window != null
                    && window.Shown
                    && window.IsReady
                    && window.MinorEmpire != null
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
            MinorFactionDiplomacyModalWindow window = Window();
            if (window == null || window.MinorEmpire == null)
            {
                return;
            }

            BuildIdentity(builder, window);
            BuildRelation(builder, window);
            BuildGains(builder, window);
            BuildActions(builder, window);
            BuildTreasury(builder, window);
        }

        /// <summary>Who the faction is: its name, the paragraph the game wrote about it, its two traits
        /// with the dossiers hung on them, and the two panel features saying what its population does for
        /// an empire and how it votes.</summary>
        private void BuildIdentity(
            GraphBuilder builder,
            MinorFactionDiplomacyModalWindow window
        )
        {
            builder.BeginStop(IdentityStop);
            builder.PushContext(ModStrings.Get(ModStrings.MinorIdentity));
            _cells.Clear();
            try
            {
                Cells.AddReadout(_cells, Of(window.EmpireDescription), Keys + "description");
                Cells.AddReadout(_cells, Of(window.MajorTraitLabel), Keys + "major-trait");
                Cells.AddReadout(_cells, Of(window.MinorTraitLabel), Keys + "minor-trait");
                Feature(window.PopulationEffects, "population-effects");
                Feature(window.PopulationPoliticalOpinion, "population-opinion");
            }
            catch (Exception e)
            {
                Log.Warn("minor diplomacy: reading the faction threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        private void Feature(GuiPanelFeature feature, string key)
        {
            AgeTransform at = feature == null ? null : feature.AgeTransform;
            if (at == null || !AgeWidgets.Visible(at))
            {
                return;
            }

            SidePanels.Content(_cells, at, Keys + key + "/", null, null);
        }

        /// <summary>How the faction feels about the player: the state, the number with its per-turn trend,
        /// who they are allied to, and the modifiers pushing the number - the influence one the game draws
        /// as its own line and one line per temporary effect, plus the warning it adds while the faction is
        /// still unknown.</summary>
        private void BuildRelation(GraphBuilder builder, MinorFactionDiplomacyModalWindow window)
        {
            builder.BeginStop(RelationStop);
            builder.PushContext(ModStrings.Get(ModStrings.MinorRelation));
            _cells.Clear();
            try
            {
                Cells.AddReadout(_cells, Of(window.RelationLabel), Keys + "relation");
                Cells.AddReadout(_cells, Of(window.RelationTrendLabel), Keys + "trend");
                Cells.AddReadout(_cells, Of(window.AllyLabel), Keys + "ally");
                Cells.AddReadout(
                    _cells,
                    window.MinorRelationModifiersTitle,
                    Keys + "modifiers-title"
                );
                AgeTransform influence = window.MinorRelationInfluenceModifierLine == null
                    ? null
                    : window.MinorRelationInfluenceModifierLine.AgeTransform;
                if (influence != null && AgeWidgets.Visible(influence))
                {
                    Cells.AddReadout(_cells, influence, Keys + "influence-modifier");
                }

                AgeTransform table = window.MinorRelationModifiersTable;
                IList<AgeTransform> children = table == null || !AgeWidgets.Visible(table)
                    ? null
                    : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    Cells.AddReadout(_cells, children[i], Keys + "modifier/" + i);
                }

                Cells.AddReadout(
                    _cells,
                    window.MinorRelationModifiersUnknownWarning,
                    Keys + "modifiers-unknown"
                );
            }
            catch (Exception e)
            {
                Log.Warn("minor diplomacy: reading the relation threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>What the relationship is worth: the list of resources the game composes into one
        /// label, or its own sentence for gaining nothing yet.</summary>
        private void BuildGains(GraphBuilder builder, MinorFactionDiplomacyModalWindow window)
        {
            builder.BeginStop(GainsStop);
            builder.PushContext(ModStrings.Get(ModStrings.MinorGains));
            _cells.Clear();
            try
            {
                Cells.AddReadout(_cells, Of(window.GainedResourcesLabel), Keys + "gains");
                Cells.AddReadout(_cells, Of(window.RelationEffectNoneLabel), Keys + "no-gains");
            }
            catch (Exception e)
            {
                Log.Warn("minor diplomacy: reading the gains threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        private void BuildActions(GraphBuilder builder, MinorFactionDiplomacyModalWindow window)
        {
            builder.BeginStop(ActionsStop);
            builder.PushContext(ModStrings.Get(ModStrings.DiplomacyActionsBand));
            _actions.Clear();
            try
            {
                AgeTransform table = window.MinorEmpireInteractionsTable;
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform at = children[i];
                    DiplomacyActions.Add(
                        _actions,
                        at == null ? null : at.GetComponent<EmpireActionButtonMinorDiplomacy>()
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("minor diplomacy: reading the actions threw: " + e);
            }

            DiplomacyActions.Emit(builder, Keys.TrimEnd(':'), _actions);
            builder.PopContext();
        }

        /// <summary>The two stocks along the bottom edge, which every window in this family draws and none
        /// of them captions.</summary>
        private void BuildTreasury(GraphBuilder builder, MinorFactionDiplomacyModalWindow window)
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
                Log.Warn("minor diplomacy: reading the treasury threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        private static string Words(AgePrimitiveLabel label)
        {
            try
            {
                return label == null || !AgeWidgets.Visible(label.AgeTransform)
                    ? null
                    : AgeText.Label(label);
            }
            catch (Exception)
            {
                return null;
            }
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

        private static MinorFactionDiplomacyModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<MinorFactionDiplomacyModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where this screen is drawn, for the tooltip audit (see
        /// <see cref="ES2Access.Screens.Screen.RootTransform"/>).</summary>
        public override AgeTransform RootTransform
        {
            get { return RootOf(Window()); }
        }

        /// <summary>What every node this screen declares is keyed under, so the audit can
        /// tell its content from the shared heads-up display stops.</summary>
        public override string NodePrefix
        {
            get { return "minor"; }
        }
    }
}

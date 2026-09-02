using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// Talking to a minor faction: the window a system label's diplomacy button opens for a minor
    /// empire's home system, and the one the "you have met somebody" popup leads to.
    ///
    /// Four things about them, in the order the window draws them: who they are and what their two traits
    /// do; how they feel about the player, what that is worth and what is pushing the number up or down;
    /// the things the player can do about it; and the pair of stock figures along the bottom, which is
    /// the same pair every window in this family draws.
    ///
    /// <b>Every word over a block is the game's own.</b> The window captions all of them - "Traits",
    /// "Diplomatic Relation", "Relation Rewards", "Modifiers", "Actions" - and each names the block
    /// rather than standing in it (<see cref="Captions"/>); the two the game hung a sentence on
    /// ("Diplomatic Relation", "Modifiers", "Actions") keep a row as well, because a block's NAME has no
    /// review buffer for that sentence to live in. Nothing here is named by a phrase this mod invented.
    ///
    /// The two columns of the identity panel are declared COLUMN BY COLUMN rather than by where the game
    /// drew them: the lore paragraph is a tall block beside three short ones, so banding the panel by
    /// rectangle put the paragraph between "Political output" and the party it lists.
    ///
    /// The band of actions is the shared one (<see cref="DiplomacyActions"/>): this window, the pirate
    /// window and the Academy window draw three separate prefab classes of identical shape, and it is
    /// read once. Two things are particular to this window and are noted rather than special-cased: an
    /// action can HIDE the window when it takes (:536-546), which is the window closing under the cursor
    /// and is what the mod's departure gate is for; and a row can draw a quest button while that
    /// faction's quest is running, whose Enter opens the quest journal and hides this window.
    ///
    /// The gauge's own click is the developers' (<c>OnGaugeButtonCb</c> :553-566 posts relation points),
    /// so the gauge is a readout and the relation number beside it is where the value is read. What the
    /// gauge DOES say is what each band of it is worth, on four tooltips the prefab hangs along it
    /// (<c>GaugeTooltipsTransformList</c>, hidden outright while at war - <c>ToggleGaugeTooltips</c>
    /// :287-293); the game gives them no captions of their own, so they are read as the relation stop's
    /// "Tooltips" region, one node per band named by the sentence's own first line.
    ///
    /// The window closes ITSELF when the faction dies, becomes unknown or is assimilated (:568-578), and
    /// refuses to open at all for one that is already integrated (:168-201) - so nothing here tests those
    /// states: it follows the window.
    /// </summary>
    public sealed class MinorFactionDiplomacyScreen : Screen
    {
        private static readonly object IdentityStop = "minor:identity";
        private static readonly object RelationStop = "minor:relation";
        private static readonly object ActionsStop = "minor:actions";
        private static readonly object TreasuryStop = "minor:treasury";

        private const string Keys = "minor:";

        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<DiplomacyActions.Row> _actions = new List<DiplomacyActions.Row>();
        private readonly List<TooltipChildren.Dossier> _bands =
            new List<TooltipChildren.Dossier>();

        public override string Key
        {
            get { return ModStrings.ScreenMinorDiplomacy; }
        }

        /// <summary>Over the star-system page and the galaxy map that open it; 42 is the advanced battle
        /// report, so this family starts at 43.</summary>
        public override int Layer
        {
            get { return 43; }
        }

        /// <summary>The window's own title and then whose window it is - "Minor Civilization diplomacy,
        /// Niris". The title is what the game calls the surface and the name is which one of them is
        /// open, and a player arriving needs both. The mod's own phrase is the fallback for a window
        /// whose title the game has not drawn.</summary>
        public override string ScreenName
        {
            get
            {
                MinorFactionDiplomacyModalWindow window = Window();
                string title =
                    AgeText.Title(WindowTitleKey)
                    ?? ModStrings.Get(ModStrings.ScreenMinorDiplomacy);

                // Through the builder rather than by gluing the separator on: the title and the faction
                // it is about are two list items, and which mark joins two list items is the one thing
                // a translation owns about this sentence.
                string drawn = window == null ? null : Words(window.EmpireNameLabel);
                return new MessageBuilder().ListItem(title).ListItem(drawn).Build();
            }
        }

        public override object InitialFocusStop
        {
            get { return ActionsStop; }
        }

        /// <summary>
        /// Escape's own job, done by the mod's Back so that one key leaves every window the same way.
        ///
        /// The key is not taken FROM the game - it is handed straight back to it:
        /// <see cref="WindowShape.PressClose"/> presses the control the window itself wires its
        /// dismissal to, so whatever that costs - a confirmation, a page switch, an order the game was
        /// always going to post - is the game's own answer and not a copy of it. Claimed only while
        /// such a control is drawn; a window offering none keeps its Escape untouched.
        /// </summary>
        public override bool ConsumesBack
        {
            get { return WindowShape.CloseControl(Window()) != null; }
        }

        public override bool Back()
        {
            return WindowShape.PressClose(Window());
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
                    && !WindowShape.Buried(window);
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
            BuildActions(builder, window);
            BuildTreasury(builder, window);
        }

        // ---- the game's own captions ----

        private const string WindowTitleKey = "%MinorFactionDiplomacyModalWindowTitle";
        private const string TraitsTitleKey = "%MinorFactionDiplomacyModalWindowTraitsTitle";
        private const string RelationTitleKey = "%MinorFactionDiplomacyModalWindowRelationTitle";
        private const string RewardsTitleKey =
            "%MinorFactionDiplomacyModalWindowRelationRewardsTitle";
        private const string ActionsTitleKey = "%MinorFactionDiplomacyModalWindowActionsTitle";

        /// <summary>
        /// Who the faction is: the window's own title with the sentence explaining the whole panel, then
        /// everything under the faction's name - the paragraph the game wrote about it, its two traits
        /// with the dossiers hung on them, and the two panel features saying what its population does for
        /// a planet and how it votes.
        ///
        /// Declared column by column. The game draws the paragraph as one tall block beside three short
        /// ones, so the drawn-row banding every other panel is read by interleaves the two columns.
        /// </summary>
        private void BuildIdentity(GraphBuilder builder, MinorFactionDiplomacyModalWindow window)
        {
            builder.BeginStop(IdentityStop);
            bool named = false;
            try
            {
                // The title carries the only sentence about what this window is for, and a screen's
                // name is a spoken phrase with no buffer behind it - so it is a row, and the first
                // one, where the player lands coming back up the stops.
                _cells.Clear();
                Cells.AddReadout(_cells, Named(window, "Title", 3), Keys + "window-title");
                Cells.EmitLinear(builder, _cells);

                named = Captions.Push(builder, AgeWidgets.Transform(window.EmpireNameLabel));

                builder.SetRegion(Keys + "identity/about");
                _cells.Clear();
                Cells.AddReadout(_cells, AgeWidgets.Transform(window.EmpireDescription), Keys + "description");
                Cells.EmitLinear(builder, _cells);

                Traits(builder, window);
                Feature(builder, window.PopulationEffects, Keys + "identity/planet-effects");
                Feature(builder, window.PopulationPoliticalOpinion, Keys + "identity/opinion");
            }
            catch (Exception e)
            {
                Log.Warn("minor diplomacy: reading the faction threw: " + e);
            }

            builder.SetRegion(null);
            Captions.Pop(builder, named);
        }

        /// <summary>The faction's personality and the trait an ally absorbs - two figures the game draws
        /// as a word beside a bare icon, captioned by its own titles for them and explained twice over:
        /// the icon says what the line IS and the word's own class-backed dossier says what THAT trait
        /// does.</summary>
        private void Traits(GraphBuilder builder, MinorFactionDiplomacyModalWindow window)
        {
            builder.SetRegion(Keys + "identity/traits");
            bool named = Captions.Push(builder, null, null, AgeText.Title(TraitsTitleKey));
            _cells.Clear();
            Cells.AddStat(
                _cells,
                window.MajorTraitLabel,
                "%MinorFactionMajorTraitTitle",
                Keys + "major-trait"
            );
            Cells.AddStat(
                _cells,
                window.MinorTraitLabel,
                "%MinorFactionMinorTraitTitle",
                Keys + "minor-trait"
            );
            Cells.EmitLinear(builder, _cells);
            Captions.Pop(builder, named);
        }

        /// <summary>One of the two panel features the identity panel embeds, under the caption the
        /// feature itself draws. Its lines are read straight rather than through the shared side-panel
        /// walk, because that walk declares the caption as a line of its own and here the caption is the
        /// block's name.</summary>
        private void Feature(GraphBuilder builder, GuiPanelFeature feature, string region)
        {
            AgeTransform group = feature == null ? null : feature.AgeTransform;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTransform caption = FeatureTitle(feature);
            builder.SetRegion(region);
            bool named = Captions.Push(builder, caption, region + "/title");
            _cells.Clear();
            Lines(group, caption, region + "/");
            Cells.EmitLinear(builder, _cells);
            Captions.Pop(builder, named);
        }

        /// <summary>The caption a panel feature draws across its top, off whichever of the two shapes
        /// this window embeds declared it.</summary>
        private static AgeTransform FeatureTitle(GuiPanelFeature feature)
        {
            try
            {
                PanelFeatureEffects effects = feature as PanelFeatureEffects;
                if (effects != null)
                {
                    return effects.TitleLabel == null ? null : effects.TitleLabel.AgeTransform;
                }

                PanelFeaturePoliticalOpinion opinion = feature as PanelFeaturePoliticalOpinion;
                return opinion == null ? null : opinion.TitleLabel;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The lines a captioned block holds: the table's own children where it drew a table,
        /// the child itself where it drew a single line, and never the caption.
        ///
        /// An effects block's table is POOLED, and a shorter binding retires its surplus lines by
        /// FADING them (<c>GuiEffectMapper.UnloadEffects</c>) rather than hiding them, so the walk asks
        /// the engine's own drawing test (<see cref="AgeWidgets.Paints"/>): a line still flagged Visible
        /// at alpha 0 is the last binding's words, not this faction's.</summary>
        private void Lines(AgeTransform group, AgeTransform caption, string keyPrefix)
        {
            IList<AgeTransform> children = group.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child == null || ReferenceEquals(child, caption))
                {
                    continue;
                }

                IList<AgeTransform> lines = child.Children;
                if (lines == null || lines.Count == 0)
                {
                    Line(child, keyPrefix + i);
                    continue;
                }

                for (int j = 0; j < lines.Count; j++)
                {
                    // Whether the line is drawn is the cells' question: each carries its line, and a
                    // retired one is taken out before they are banded (<see cref="Cells"/>). The step
                    // ABOVE stays asked here - a faded container is an ancestor, which the one-step
                    // gate cannot see.
                    Line(lines[j], keyPrefix + i + "/" + j);
                }
            }
        }

        /// <summary>One line of a captioned block, with EVERY explanation the game drew inside it - a
        /// political-opinion line's party name is a label and the party's dossier hangs off the icon
        /// beside it, so a reader taking only the line's own tooltip loses the dossier.</summary>
        private void Line(AgeTransform widget, string key)
        {
            if (
                widget != null
                && AgeWidgets.Visible(widget)
                && !string.IsNullOrEmpty(AgeWidgets.TextOf(widget))
            )
            {
                _cells.Add(Cells.GatheredReadout(widget, key));
            }
        }

        /// <summary>
        /// How the faction feels about the player, under the game's own heading for the panel: the state,
        /// the number with its per-turn trend, who they are allied to, what each band of the gauge would
        /// be worth, what the relationship is paying out, and the modifiers pushing the number - the
        /// influence one the game draws as its own line and one line per temporary effect, plus the
        /// warning it adds while the faction is still unknown.
        /// </summary>
        private void BuildRelation(GraphBuilder builder, MinorFactionDiplomacyModalWindow window)
        {
            builder.BeginStop(RelationStop);
            AgeTransform title = Group(window, "RelationInfoTitle", 6);
            bool named = Captions.Push(
                builder,
                title,
                Keys + "relation-title",
                Captions.Text(title) ?? AgeText.Title(RelationTitleKey)
            );
            try
            {
                builder.SetRegion(Keys + "relation/state");
                _cells.Clear();

                // "Relation, CORDIAL": the state's own word, captioned by the game's title for it, with
                // the icon's sentence about what a relation IS and the state's own effect sentence
                // behind it.
                Cells.AddStat(
                    _cells,
                    window.RelationLabel,
                    "%MinorFactionRelationTitle",
                    Keys + "relation"
                );

                // The points and their trend, which the game writes into ONE label ("40 (+7/Turn)") and
                // gives no title anywhere - not even a description key. The caption is the mod's own
                // word for what the number IS (owner ruling 2026-08-22); before it, the line fell to
                // the shared last resort and was named by the SENTENCE the game hangs on it, which
                // explains what a relation is and never says the number.
                Cells.AddStat(
                    _cells,
                    window.RelationTrendLabel,
                    ModStrings.Get(ModStrings.MinorRelationship),
                    Keys + "trend"
                );
                Cells.AddStat(
                    _cells,
                    window.AllyLabel,
                    "%MinorFactionCurrentAllyTitle",
                    Keys + "ally"
                );
                Cells.EmitLinear(builder, _cells);

                Bands(builder, window);
                Rewards(builder, window);
                Modifiers(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("minor diplomacy: reading the relation threw: " + e);
            }

            builder.SetRegion(null);
            Captions.Pop(builder, named);
        }

        /// <summary>
        /// What each band of the relation gauge is worth. The prefab hangs one sentence per band and no
        /// caption on any of them, so they are the stop's "Tooltips" region - the uniform answer for
        /// explanations a surface offers with no words on screen.
        ///
        /// Each band is NAMED "CORDIAL (25)": the relation state the band buys, and the number of
        /// relation points where it starts. Neither half is invented and neither is hard-coded. The
        /// STATE is read off the band's own sentence key - the prefab writes
        /// <c>%DiplomaticRelationStateMinorCordialDescription</c> into the tooltip's content, and the
        /// game's own title for the same state is that key with Description swapped for Title, which is
        /// how the game itself builds these keys (state prefix + member name). The THRESHOLD is
        /// measured off the bar: the segment's left edge as a percentage of the gauge it is laid along,
        /// which is the same 0-100 scale the relation points are on (measured 2026-08-22: segments at
        /// 0/66/133/200 across a 266-wide gauge, and 33 points reading CORDIAL).
        ///
        /// The sentence itself reads by its own kind, like every other tooltip: it was the name until
        /// the states arrived, and the readout's own dedupe is what keeps the band from being said
        /// twice where the name still comes off the sentence.
        /// </summary>
        private void Bands(GraphBuilder builder, MinorFactionDiplomacyModalWindow window)
        {
            _bands.Clear();
            IList<AgeTransform> tips = window.GaugeTooltipsTransformList;
            for (int i = 0; tips != null && i < tips.Count; i++)
            {
                AgeTransform at = tips[i];
                if (at == null || !AgeWidgets.Visible(at))
                {
                    continue;
                }

                AgeTooltip tooltip = AgeWidgets.Raw(at);
                if (tooltip == null || !AgeWidgets.Draws(tooltip))
                {
                    continue;
                }

                AgeTooltip tip = tooltip;
                string band = BandName(at, tooltip);
                _bands.Add(
                    new TooltipChildren.Dossier
                    {
                        Name =
                            band != null
                                ? (Func<string>)(() => band)
                                : TooltipChildren.NameOf(tip, at),
                        Tooltip = tip,
                        Anchor = at,
                    }
                );
            }

            TooltipChildren.Emit(builder, Keys + "gauge", _bands, null);
        }

        /// <summary>One band's name - the state it buys and the points it starts at. Null where either
        /// half cannot be read, which leaves the band named by its sentence's first line as it was.
        /// </summary>
        private static string BandName(AgeTransform segment, AgeTooltip tooltip)
        {
            try
            {
                string state = BandState(tooltip);
                AgeTransform bar = segment.Parent;
                if (state == null || bar == null || bar.Width <= 0f)
                {
                    return null;
                }

                int points = Mathf.RoundToInt(segment.X / bar.Width * 100f);
                return ModStrings.Format(ModStrings.MinorBand, state, points);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's own word for the relation state a band's sentence explains, off that
        /// sentence's own key. Null where the key is not one of these or the title is missing, which is
        /// what a patch re-authoring the prefab looks like from here.</summary>
        private static string BandState(AgeTooltip tooltip)
        {
            string content = tooltip == null ? null : tooltip.Content;
            if (
                content == null
                || !content.StartsWith(BandKeyPrefix)
                || !content.EndsWith(BandKeySuffix)
            )
            {
                return null;
            }

            return AgeText.Title(
                content.Substring(0, content.Length - BandKeySuffix.Length) + "Title"
            );
        }

        private const string BandKeyPrefix = "%DiplomaticRelationState";
        private const string BandKeySuffix = "Description";

        /// <summary>What the relationship is worth: the resources the game composes into ONE label, a row
        /// per line of it, or its own sentence for gaining nothing yet.</summary>
        private void Rewards(GraphBuilder builder, MinorFactionDiplomacyModalWindow window)
        {
            builder.SetRegion(Keys + "relation/rewards");
            bool named = Captions.Push(builder, null, null, AgeText.Title(RewardsTitleKey));
            AgePrimitiveLabel label = window.GainedResourcesLabel;
            AgeTransform at = AgeWidgets.Transform(label);
            if (at != null && AgeWidgets.Visible(at))
            {
                AgePrimitiveLabel it = label;
                IList<string> lines = AgeText.Lines(AgeText.FullLabel(it));
                for (int i = 0; lines != null && i < lines.Count; i++)
                {
                    int index = i;
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => Gain(it, index)),
                        },
                    };
                    // Every line of this list is cut out of ONE drawn label, so the label is where each
                    // of them is on screen and what a viewport has to be scrolled to.
                    ScrollIntoView.Anchor(vtable, at);
                    // And that same one label is what every line of it EXISTS by: there is no widget
                    // per line to ask, the lines are text the label is drawing, so all of them share
                    // the label as their evidence and stand or fall with it together.
                    builder.AddItem(
                        Nodes.Drawn(ControlId.Structural(Keys + "gain/" + index), vtable, at)
                    );
                }
            }

            _cells.Clear();
            Cells.AddReadout(_cells, AgeWidgets.Transform(window.RelationEffectNoneLabel), Keys + "no-gains");
            Cells.EmitLinear(builder, _cells);
            Captions.Pop(builder, named);
        }

        /// <summary>One line of the payout label, resolved when the node is read rather than when it is
        /// declared - the game rewrites the whole label every time the relation moves.</summary>
        private static string Gain(AgePrimitiveLabel label, int index)
        {
            try
            {
                IList<string> lines = AgeText.Lines(AgeText.FullLabel(label));
                return lines == null || index >= lines.Count ? null : lines[index];
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What is pushing the relation, under the game's own caption for the block - which
        /// carries a sentence of its own and so is a row as well as the block's name.</summary>
        private void Modifiers(GraphBuilder builder, MinorFactionDiplomacyModalWindow window)
        {
            builder.SetRegion(Keys + "relation/modifiers");
            bool named = Captions.Push(
                builder,
                window.MinorRelationModifiersTitle,
                Keys + "modifiers-title"
            );
            _cells.Clear();
            AgeTransform influence = window.MinorRelationInfluenceModifierLine == null
                ? null
                : window.MinorRelationInfluenceModifierLine.AgeTransform;
            // Flow control: the window keeps this line wired and draws it only where influence is one
            // of the modifiers, so this is whether the band has that row at all.
            if (influence != null && AgeWidgets.Visible(influence))
            {
                Cells.AddReadout(_cells, influence, Keys + "influence-modifier");
            }

            AgeTransform table = window.MinorRelationModifiersTable;
            // Flow control: a table the window is not drawing must not be WALKED for modifiers to declare.
            IList<AgeTransform> children = table == null || !AgeWidgets.Visible(table)
                ? null
                : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                // Pooled (MinorFactionDiplomacyModalWindow.cs:430 ReserveChildren): a faction with
                // fewer modifiers than the one looked at before leaves the surplus lines faded to
                // nothing and still Visible, holding the other faction's words. Each cell carries its
                // line, and those are taken out before the cells are banded (<see cref="Cells"/>).
                Cells.AddReadout(_cells, children[i], Keys + "modifier/" + i);
            }

            Cells.AddReadout(
                _cells,
                window.MinorRelationModifiersUnknownWarning,
                Keys + "modifiers-unknown"
            );
            Cells.EmitLinear(builder, _cells);
            Captions.Pop(builder, named);
        }

        private void BuildActions(GraphBuilder builder, MinorFactionDiplomacyModalWindow window)
        {
            builder.BeginStop(ActionsStop);
            AgeTransform title = Group(window, "ActionsTitle", 4);
            bool named = Captions.Push(
                builder,
                title,
                Keys + "actions-title",
                Captions.Text(title) ?? AgeText.Title(ActionsTitleKey)
            );
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
            Captions.Pop(builder, named);
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

        /// <summary>
        /// The BLOCK a caption titles, found by the name the prefab gives the caption label inside it.
        ///
        /// Two of this window's three captions are drawn as a label inside a group and EXPLAINED on
        /// the group, so the words and the sentence sit on different widgets: asking for the group by
        /// its own prefab name ("TitleGroup", worn by three of them) answers with whichever one the
        /// walk reaches first - the faction banner, which named the relation panel "Niris" and left the
        /// relation and actions sentences with nowhere to be read (measured live 2026-08-22).
        /// </summary>
        private static AgeTransform Group(
            MinorFactionDiplomacyModalWindow window,
            string name,
            int depth
        )
        {
            AgeTransform label = Named(window, name, depth);
            return label == null ? null : label.Parent;
        }

        /// <summary>A caption the window draws but does not expose, found by the name the prefab gives
        /// it.</summary>
        private static AgeTransform Named(
            MinorFactionDiplomacyModalWindow window,
            string name,
            int depth
        )
        {
            try
            {
                return window == null
                    ? null
                    : AgeWidgets.ChildNamed(window.AgeTransform, name, depth);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Words(AgePrimitiveLabel label)
        {
            try
            {
                // Content: a label nobody is drawing holds no words of this faction's.
                return label == null || !AgeWidgets.Visible(label.AgeTransform)
                    ? null
                    : AgeText.Label(label);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static MinorFactionDiplomacyModalWindow Window()
        {
            return GameWindows.Of<MinorFactionDiplomacyModalWindow>();
        }

        /// <summary>Where this screen is drawn, for the tooltip audit (see
        /// <see cref="ES2Access.Screens.Screen.RootTransform"/>).</summary>
        public override AgeTransform RootTransform
        {
            get { return RootOf(Window()); }
        }
    }
}

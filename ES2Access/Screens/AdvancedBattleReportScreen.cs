using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The advanced battle report - how the fight actually went, phase by phase and weapon by weapon.
    ///
    /// The report popup says who won; this window says WHY, and everything it says is drawn as a picture.
    /// The phase panel is a grid of range icons and paired arcs, the damage panels are stacked coloured
    /// bars, and the numbers behind all of it live only in the sentences the game writes onto their
    /// tooltips. So this screen is those sentences, arranged the way the pictures are.
    ///
    /// The phase panel is a LIST, not a table. Each phase sentence already names its own flotilla and
    /// its own phase ("The Flotillas 2 were at Long range during phase 1, Damage repartition: 340 vs
    /// 120"), so a grid would hand the player a coordinate system to re-read words the game had already
    /// finished writing. It reads as a flat run of those sentences instead, flotilla-major - down the
    /// list is one flotilla through the battle - and a flotilla name leads its run ONLY where more than
    /// one flotilla fought, because with one the sentences name it on every line. Each sentence is kept
    /// WHOLE rather than split into the numbers inside it: the game already wrote the reading, and a
    /// line that said "Long, 340, 120" would be the mod paraphrasing. A phase the battle never reached
    /// draws no item at all and so contributes no line.
    ///
    /// The TACTICS stop is the two plans the battle was fought under, one card a side: the game's own
    /// "Selected Plan" title, the plan's name, and the effect lines the card PRINTS (always-drawn text,
    /// so spoken). Behind each is the card's own how-often-chosen sentence and the same nested entries
    /// the setup screens give the same prefab - the family badge and one range diagram per flotilla.
    ///
    /// Three things the window draws as pictures and writes down nowhere join the heading: the balance
    /// of power (the same ring, the same reading as every other battle surface,
    /// <see cref="BattleNotifications.Balance"/>), the morale bonus (the game stamps one happiness icon
    /// per holding side on EVERY fought phase, so it is one line in that side's heading rather than a
    /// repeat down the phase list), and - for the enemy, whose roster panel is a garrison with no
    /// flotilla lines to hang it on - the arena card's sentence naming the range its flotilla is optimal
    /// at. The player's side has flotilla lines, so its cards go where the flotillas already are
    /// (<see cref="BattleRosters.FlotillaExtras"/>), exactly as the advanced SETUP window hands its own
    /// in.
    ///
    /// The damage panels are the same idea a second time: a row per bar the gauge is showing, each the
    /// game's own sentence for that bar ("Damage caused by your Beam weapons: 340"), with the tactical
    /// advice the game hangs beside it kept for the review buffer rather than spoken - it is the same
    /// paragraph every battle. Absorbed damage, the missed shots the toggle folds in and the totals at the
    /// foot are all bars of the same kind and all read the same way.
    ///
    /// The two fleet toggles do not open anything of the mod's: the game slides a roster panel over the
    /// phase panel, so what this screen declares follows what is DRAWN - the phase lines while the
    /// phases are up, the roster while a roster is - and the toggles themselves are the only thing that
    /// has to be declared for the keyboard.
    ///
    /// Escape is the game's, and it is not a plain close: the window's own <c>HandleInput</c> puts the
    /// report popup back up, which is where the player came from.
    /// </summary>
    public sealed partial class AdvancedBattleReportScreen : Screen
    {
        private static readonly object HeadingStop = "battle-advanced:heading";
        private static readonly object TacticsStop = "battle-advanced:tactics";
        private static readonly object PhasesStop = "battle-advanced:phases";
        private static readonly object DamageStop = "battle-advanced:damage";
        private static readonly object ControlsStop = "battle-advanced:controls";

        private static readonly object YoursRegion = "battle-advanced:yours";
        private static readonly object TheirsRegion = "battle-advanced:theirs";

        /// <summary>The game's own titles for the things it draws as pictures.</summary>
        private const string CommandPointsTitleKey = "%ShipStatCommandPointsTitle";
        private const string MissedDamageTitleKey =
            "%AdvancedReportModalWindowShowMissedDamageTitle";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return ModStrings.ScreenAdvancedBattleReport; }
        }

        /// <summary>Over the notification popup it is opened from and returns to, and under the
        /// confirmation box that can be raised over anything.</summary>
        public override int Layer
        {
            get { return 42; }
        }

        /// <summary>The game's own word for how the battle went, which is what the window writes across
        /// its top.</summary>
        public override string ScreenName
        {
            get
            {
                try
                {
                    AdvancedEncounterReportModalWindow window = Window();
                    string title = window == null ? null : AgeText.Label(window.BattleTitle);
                    return string.IsNullOrEmpty(title)
                        ? OptionalText.Phrase(ModStrings.ScreenAdvancedBattleReport)
                        : title;
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
                AdvancedEncounterReportModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: the window answers Exit by putting the battle report back up, which is
        /// somewhere to go rather than nowhere.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            AdvancedEncounterReportModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                builder.BeginStop(HeadingStop);
                Heading(builder, window);

                builder.BeginStop(TacticsStop);
                Tactics(builder, window);

                builder.BeginStop(PhasesStop);
                Phases(builder, window);
                Rosters(builder, window);

                builder.BeginStop(DamageStop);
                Damage(builder, window);

                builder.BeginStop(ControlsStop);
                Controls(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("battle report: reading the advanced report threw: " + e);
            }
        }

        /// <summary>Who fought, how it ended, and what each side spent: the command-point line is the
        /// game's own "before, then after" sentence, which is the only place the losses in fleet capacity
        /// are written down. The ring beside the outcome is the balance of power, which the window draws
        /// as two arcs and writes down nowhere at all.</summary>
        private static void Heading(
            GraphBuilder builder,
            AdvancedEncounterReportModalWindow window
        )
        {
            BattleRows.Note(builder, window.BattleTitle, "battle-advanced/outcome");
            BattleBalance.Balance(
                builder,
                AgeWidgets.Transform(window.BattlePowerGauge),
                window.PlayerEncounterGroup,
                window.EnemyEncounterGroup,
                false,
                "battle-advanced/balance"
            );

            builder.SetRegion(YoursRegion);
            BattleRows.Leader(builder, window.PlayerBattleGroupInfoPanel, "battle-advanced/yours");
            Value(builder, window.PlayerCPLabel, CommandPointsTitleKey, "battle-advanced/your-cp");
            Morale(
                builder,
                window,
                window.PlayerEncounterGroup,
                ModStrings.BattleYourMoraleBonus,
                "battle-advanced/your-morale"
            );
            Squadrons(builder, window, true, "battle-advanced/your-squadrons");

            builder.SetRegion(TheirsRegion);
            BattleRows.Leader(builder, window.EnemyBattleGroupInfoPanel, "battle-advanced/theirs");
            Value(builder, window.EnemyCPLabel, CommandPointsTitleKey, "battle-advanced/their-cp");
            Morale(
                builder,
                window,
                window.EnemyEncounterGroup,
                ModStrings.BattleEnemyMoraleBonus,
                "battle-advanced/their-morale"
            );
            Flotillas(builder, window.EnemyFlotillaCard2DContainer, "battle-advanced/their-flotilla");
            Squadrons(builder, window, false, "battle-advanced/their-squadrons");
            builder.SetRegion(null);
        }

        /// <summary>
        /// What became of a side's fighter and bomber squadrons: how many are still flying and how many
        /// were shot down.
        ///
        /// The arena draws these as up to four icon-and-number chips per card
        /// (<c>EncounterFighterBomberCard2D</c>), and the numbers are the only place in the whole report
        /// a squadron is counted at all - the roster lists SHIPS, and a carrier's wing is not a ship.
        /// Each chip carries the game's own sentence for what it counts ("Number of operational Fighter
        /// units.", "Number of destroyed Bomber units."), so no mod phrase is needed and none is
        /// invented: the sentence names the row and the drawn number is its value.
        ///
        /// A chip whose count is zero is a group the card hides, and a card with all four hidden hides
        /// itself (<c>RefreshValues</c>) - so a battle with no carriers declares nothing here, which is
        /// this fixture and the reason the positive side is fixture-blocked. The player's side draws one
        /// card per flotilla and the enemy's one for the whole fleet
        /// (<c>EncounterPlayFlotillaCardContainer.RefreshFlotillaCards2D</c> :71-88,
        /// <c>EncounterPlayFleetCardContainer.Bind</c> :22-25), so the rows are led by the flotilla
        /// number wherever more than one card is drawing.
        /// </summary>
        private static void Squadrons(
            GraphBuilder builder,
            AdvancedEncounterReportModalWindow window,
            bool mine,
            string prefix
        )
        {
            AgeTransform group = Squadrons(window, mine);
            // Flow control: a walk of the group's cards and a COUNT of the drawn ones, both of which
            // run before any node exists for the gate to see.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            List<EncounterFighterBomberCard2D> cards = new List<EncounterFighterBomberCard2D>(3);
            List<AgeTransform> children = group.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                EncounterFighterBomberCard2D card =
                    children[i].GetComponent<EncounterFighterBomberCard2D>();
                // Spoken count: how many cards are DRAWING decides whether each row is led by the
                // flotilla it belongs to.
                if (card != null && AgeWidgets.Visible(card.AgeTransform))
                {
                    cards.Add(card);
                }
            }

            for (int i = 0; i < cards.Count; i++)
            {
                EncounterFighterBomberCard2D card = cards[i];
                string flotilla =
                    cards.Count > 1
                        ? BattleRosters.FlotillaName(Index(group, card) + 1)
                        : null;
                AgePrimitiveLabel[] counts = card.FighterBombersCounts;
                for (int j = 0; counts != null && j < counts.Length; j++)
                {
                    Squadron(builder, flotilla, counts[j], prefix + "/" + i + "/" + j);
                }
            }
        }

        /// <summary>One chip of a squadron card: the game's sentence for what it counts, and the number
        /// it drew. The sentence hangs on the GROUP holding the icon and the label, not on the label -
        /// aiming at the label would draw nothing.</summary>
        private static void Squadron(
            GraphBuilder builder,
            string flotilla,
            AgePrimitiveLabel count,
            string key
        )
        {
            AgeTransform widget = count == null ? null : count.AgeTransform;
            AgeTransform chip = widget == null ? null : widget.Parent;
            if (chip == null)
            {
                return;
            }

            AgePrimitiveLabel it = count;
            AgeTransform at = chip;
            string named = flotilla;
            AgeTooltip tooltip = AgeWidgets.Raw(chip);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    // LABEL FALLBACK: the chip is an icon and a bare number, and the sentence the game
                    // wrote for it is the only thing that says WHAT is being counted - the same rung
                    // the ordinary naming ladder would have reached. It is this row's whole name, so
                    // the door has nothing left to announce twice.
                    GraphNodes.LabelPart(
                        () =>
                            new MessageBuilder()
                                .ListItem(named)
                                // LABEL FALLBACK: see above - the chip draws an icon and a number,
                                // and this sentence is the only thing that names what it counts.
                                .ListItem(AgeText.Tooltip(tooltip))
                                .Build()
                    ),
                    GraphNodes.ValuePart(() => AgeWidgets.DrawnLabel(at, it), false),
                },
                Sections = null,
            };
            AgeWidgets.PointAt(vtable, chip);
            builder.AddItem(Nodes.Drawn(ControlId.For(count, key), vtable, chip));
        }

        /// <summary>Where a side keeps its squadron cards: the arena container that draws them, which is
        /// a flotilla container on the player's side and a fleet container on the enemy's - two types
        /// with the same field and no shared declaration of it, so both are asked.</summary>
        private static AgeTransform Squadrons(
            AdvancedEncounterReportModalWindow window,
            bool mine
        )
        {
            try
            {
                EncounterPlayScreen3D arena = window.EncounterPlayScreen3D;
                EncounterPlayContainer[] containers =
                    arena == null
                        ? null
                        : (mine
                            ? arena.PlayerEncounterPlayContainers
                            : arena.EnemyEncounterPlayContainers);
                for (int i = 0; containers != null && i < containers.Length; i++)
                {
                    EncounterPlayFlotillaCardContainer flotillas =
                        containers[i] as EncounterPlayFlotillaCardContainer;
                    if (flotillas != null && flotillas.FighterBomber2DCardGroup != null)
                    {
                        return flotillas.FighterBomber2DCardGroup;
                    }

                    EncounterPlayFleetCardContainer fleet =
                        containers[i] as EncounterPlayFleetCardContainer;
                    if (fleet != null && fleet.FighterBomber2DCardGroup != null)
                    {
                        return fleet.FighterBomber2DCardGroup;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle report: looking for the squadron cards threw: " + e);
            }

            return null;
        }

        /// <summary>Which flotilla a squadron card stands for: the position the container gave it, which
        /// is the flotilla index the card was refreshed from
        /// (<c>EncounterPlayFlotillaCardContainer.RefreshFlotillaCards2D</c> walks one list).</summary>
        private static int Index(AgeTransform group, EncounterFighterBomberCard2D card)
        {
            List<AgeTransform> children = group.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (children[i] == card.AgeTransform)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// That this side HAD the morale bonus, once, as a statement rather than a caption.
        ///
        /// The game stamps it as a happiness icon on every phase it fought, one per holding side and
        /// coloured to say WHICH - a repeat of one group-level fact, not a per-phase reading
        /// (<c>AdvancedReportPhaseItem.Refresh</c> asks the GROUP for it and draws the same answer in
        /// every column). So it is one line in the side's own heading.
        ///
        /// The game's own title for it ("Morale bonus") is not the line: read out it is a caption, and
        /// the owner heard "Morale bonus" followed by the definition and could not tell whose fleet had
        /// one (2026-08-30). The colour is what says whose, and speech has no colour; the region a row
        /// sits in is not spoken either. So the line is the mod's own sentence naming the side, and the
        /// game's definition stays behind it where it was.
        /// </summary>
        private static void Morale(
            GraphBuilder builder,
            AdvancedEncounterReportModalWindow window,
            EncounterGroup group,
            string phraseKey,
            string key
        )
        {
            // Flow control: this is the group's own property, read before anything is declared for it.
            if (!Holds(group))
            {
                return;
            }

            AgeTransform widget = MoraleIcon(window);
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            string phrase = phraseKey;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => OptionalText.Phrase(phrase)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            if (widget == null)
            {
                vtable.OnFocusVisual = AgeWidgets.ReleasePointer;
                builder.AddItem(Nodes.Synthetic(ControlId.Structural(key), vtable));
                return;
            }

            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(widget, key), vtable, widget));
        }

        private static bool Holds(EncounterGroup group)
        {
            try
            {
                return group != null
                    && group.GetPropertyValue(SimulationProperties.EncounterGroup.MoraleBonus) > 0f;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The happiness icon the phase panels draw the bonus as - the first one the window is
        /// drawing, since every fought phase draws the same one. It is where the explanation lives, and
        /// where the pointer goes so that explanation is on screen too.</summary>
        private static AgeTransform MoraleIcon(AdvancedEncounterReportModalWindow window)
        {
            try
            {
                AgeTransform container = window.AdvancedReportPhaseItemContainer;
                AdvancedReportPhaseItem[] phases =
                    container == null
                        ? null
                        : container.GetComponentsInChildren<AdvancedReportPhaseItem>(true);
                for (int i = 0; phases != null && i < phases.Length; i++)
                {
                    AgeTransform icon = AgeWidgets.Transform(
                        phases[i] == null ? null : phases[i].MoraleBonusLabel
                    );
                    // Candidate choice, not existence: every fought phase draws the same icon and the
                    // first drawn one stands for all of them.
                    if (icon != null && AgeWidgets.Visible(icon))
                    {
                        return icon;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle report: looking for the morale icon threw: " + e);
            }

            return null;
        }

        /// <summary>
        /// The arena cards for a side whose roster panel has no flotilla lines to carry them.
        ///
        /// A flotilla is drawn twice on this window too - as a line of ships in the roster panel, and as
        /// a card in the arena carrying the sentence that says which range it is optimal at and how well
        /// its ships suit that range. The player's roster draws flotilla lines, so its cards are handed
        /// to those lines (<see cref="FlotillaCards"/>); the enemy's roster is a garrison panel with no
        /// flotilla line anywhere, so its cards are read here, under the flotilla number the game
        /// numbers them by.
        /// </summary>
        private static void Flotillas(GraphBuilder builder, AgeTransform container, string prefix)
        {
            // Flow control: the cards are found by a walk of the container's children.
            if (container == null || !AgeWidgets.Visible(container))
            {
                return;
            }

            List<AgeTransform> children = container.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                EncounterPlayFlotillaCard2D card = Card(children[i]);
                AgeTransform widget = Drawn(card);
                if (widget == null)
                {
                    continue;
                }

                // The game numbers the flotillas from one where it writes them down and from zero
                // where it binds them.
                int number = card.Index + 1;
                NodeVtable vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(
                            () => BattleRosters.FlotillaName(number)
                        ),
                    },
                    Sections = GraphNodes.Sections(null, AgeWidgets.Raw(widget)),
                };
                AgeWidgets.PointAt(vtable, widget);
                builder.AddItem(
                    Nodes.Drawn(ControlId.For(card, prefix + "/" + i), vtable, card)
                );
            }
        }

        /// <summary>The same cards for the side whose roster DOES draw flotilla lines, handed to the
        /// lines rather than read on their own: the shared roster reader puts each card's sentence on
        /// the row for the flotilla it belongs to, matched by the NUMBER the line draws and never by
        /// child order - the two collections are built by different code and agreeing today is not a
        /// contract.</summary>
        private static BattleRosters.FlotillaExtras FlotillaCards(
            AdvancedEncounterReportModalWindow window
        )
        {
            AdvancedEncounterReportModalWindow it = window;
            return new BattleRosters.FlotillaExtras
            {
                Tooltip = line =>
                    AgeWidgets.Raw(Drawn(Card(it.PlayerFlotillaCard2DContainer, line))),
            };
        }

        /// <summary>The card standing for the flotilla a roster line names, by the number the line drew.
        /// Null where nothing answers to that number.</summary>
        private static EncounterPlayFlotillaCard2D Card(
            AgeTransform container,
            FlotillaLine line
        )
        {
            try
            {
                int number;
                if (
                    container == null
                    || line == null
                    || !int.TryParse(AgeText.Label(line.FlotillaIndexLabel), out number)
                )
                {
                    return null;
                }

                List<AgeTransform> children = container.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    EncounterPlayFlotillaCard2D card = Card(children[i]);
                    if (card != null && card.Index == number - 1)
                    {
                        return card;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle report: looking for a flotilla card threw: " + e);
            }

            return null;
        }

        private static EncounterPlayFlotillaCard2D Card(AgeTransform widget)
        {
            try
            {
                return widget == null
                    ? null
                    : widget.GetComponent<EncounterPlayFlotillaCard2D>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A card's own rectangle, and only while the arena is drawing that card: a flotilla
        /// the battle did not field keeps a card the window hides, and its sentence is a statement about
        /// a flotilla that was not there.</summary>
        private static AgeTransform Drawn(EncounterPlayFlotillaCard2D card)
        {
            try
            {
                AgeTransform widget = card == null ? null : card.AgeTransform;
                // Content read: the answer is handed to the shared roster reader as a TOOLTIP for
                // somebody else's row, where no gate of this screen's will ever look at it.
                return widget != null && AgeWidgets.Visible(widget) ? widget : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The two plans the battle was fought under, one card a side.
        ///
        /// A stop of its own between the outcome and the phases, because which plan each side played is
        /// the first thing that explains the rest of the window - and the two cards are the only place
        /// either plan is named. Each says the game's own title for the slot, the plan's name and the
        /// effect lines the card prints; the how-often-chosen sentence behind it and the badges drawn on
        /// it are the same hover surfaces the setup screens read off the same prefab.
        /// </summary>
        private static void Tactics(
            GraphBuilder builder,
            AdvancedEncounterReportModalWindow window
        )
        {
            // The same word the advanced SETUP window names its own hand of plans with, from the same
            // key: a stop the player lands in says what it is, and the two windows say the same thing
            // the same way.
            bool named = BattleRows.Context(builder, ModStrings.BattleTactics);

            Plan(
                builder,
                YoursRegion,
                ModStrings.BattleYourFleets,
                window.PlayerPlayCardContainer,
                "battle-advanced/your-plan"
            );
            Plan(
                builder,
                TheirsRegion,
                ModStrings.BattleEnemyFleets,
                window.EnemyPlayCardContainer,
                "battle-advanced/their-plan"
            );
            BattleRows.Close(builder, named);

            builder.SetRegion(null);
        }

        private static void Plan(
            GraphBuilder builder,
            object region,
            string nameKey,
            AgeTransform container,
            string key
        )
        {
            BattlePlayCard card = PlayCard(container);
            AgeTransform widget = card == null ? null : card.AgeTransform;
            // Flow control: a context is opened below, and the dossiers are a walk of the card's badges.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            builder.SetRegion(region);
            bool named = BattleRows.Context(builder, nameKey);

            BattlePlayCard it = card;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Clean(BattleRows.ReportPlanTitleKey)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlayTitle), false),
                    GraphNodes.ValuePart(() => BattlePlans.PlanEffects(it), false),
                },
                Sections = GraphNodes.Sections(null, card.Tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(ControlId.For(card, key), vtable, card),
                key,
                BattlePlans.PlanDossiers(it, null)
            );
            BattleRows.Close(builder, named);
        }

        /// <summary>The card the window instantiated into a side's slot - the report draws exactly one
        /// per side, permanently on the plan that side played.</summary>
        private static BattlePlayCard PlayCard(AgeTransform container)
        {
            try
            {
                return container == null
                    ? null
                    : container.GetComponentInChildren<BattlePlayCard>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The three switches and the way out. The switches are the game's own boxes; Back has
        /// no field of its own on the window, so it is found where the window drew it.</summary>
        private void Controls(GraphBuilder builder, AdvancedEncounterReportModalWindow window)
        {
            _cells.Clear();
            Checkbox(
                _cells,
                window.ShowPlayerFleetsToggle,
                ModStrings.BattleShowYourFleets,
                null,
                "battle-advanced:show-yours"
            );
            Checkbox(
                _cells,
                window.ShowEnemyFleetsToggle,
                ModStrings.BattleShowEnemyFleets,
                null,
                "battle-advanced:show-theirs"
            );
            Checkbox(
                _cells,
                window.ShowMissedDamageToggle,
                null,
                MissedDamageTitleKey,
                "battle-advanced:show-missed"
            );
            Cells.AddControl(_cells, Back(window), "battle-advanced:back");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The way out, which the window keeps in no field of its own: it is found by the
        /// HANDLER it is wired to rather than by a name in the prefab, because the handler is in the
        /// window's own code and a prefab name is a guess.</summary>
        private static AgeTransform Back(AdvancedEncounterReportModalWindow window)
        {
            return AgeWidgets.Transform(
                AgeWidgets.WiredTo(window == null ? null : window.AgeTransform, BackHandler)
            );
        }

        private const string BackHandler = "OnBackCb";

        /// <summary>A battle box (<see cref="BattleRows.Checkbox"/>) under this window's own naming
        /// rule.</summary>
        private static void Checkbox(
            List<Cell> cells,
            AgeControlToggle toggle,
            string modKey,
            string gameKey,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            BattleRows.Checkbox(cells, toggle, () => Name(widget, modKey, gameKey), key);
        }

        /// <summary>What a control is called: the words the game drew on it, else the game's own title
        /// for it, else the mod's - in that order, because the mod's word is the last resort and only
        /// exists for the two switches the game names nowhere at all.</summary>
        private static string Name(AgeTransform widget, string modKey, string gameKey)
        {
            string drawn = AgeWidgets.TextOf(widget);
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            string game = string.IsNullOrEmpty(gameKey) ? null : AgeText.Clean(gameKey);
            return string.IsNullOrEmpty(game) ? OptionalText.Phrase(modKey) : game;
        }

        /// <summary>A figure the game wrote as its own sentence ("18 &gt;&gt; 11 CP"), under the game's
        /// name for what it counts (<see cref="BattleRows.Value"/> - this window draws these two on
        /// their own rather than inside a captioned row, so there is no drawn caption to prefer). The
        /// emptiness test is this window's own: a command-point label the game left blank is a figure
        /// that has not been written yet, not a row worth a stop.</summary>
        private static void Value(
            GraphBuilder builder,
            AgePrimitiveLabel label,
            string titleKey,
            string key
        )
        {
            if (label == null || string.IsNullOrEmpty(AgeText.Label(label)))
            {
                return;
            }

            BattleRows.Value(builder, null, label, titleKey, key);
        }

        private static AdvancedEncounterReportModalWindow Window()
        {
            return GameWindows.Of<AdvancedEncounterReportModalWindow>();
        }
    }
}

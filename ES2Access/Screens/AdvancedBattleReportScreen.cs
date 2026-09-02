using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
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
    public sealed class AdvancedBattleReportScreen : Screen
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
        private const string FlotillaNameKey = "%FlotillaNameTitle";
        private const string PlanTitleKey = "%NotificationBattleReportSelectedPlayTitle";

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
                        ? BattleText.Optional(ModStrings.ScreenAdvancedBattleReport)
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
            Note(builder, window.BattleTitle, "battle-advanced/outcome");
            BattleNotifications.Balance(
                builder,
                AgeWidgets.Transform(window.BattlePowerGauge),
                window.PlayerEncounterGroup,
                window.EnemyEncounterGroup,
                false,
                "battle-advanced/balance"
            );

            builder.SetRegion(YoursRegion);
            Leader(builder, window.PlayerBattleGroupInfoPanel, "battle-advanced/yours");
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
            Leader(builder, window.EnemyBattleGroupInfoPanel, "battle-advanced/theirs");
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
                        ? AgeText.Clean(
                            Gui.Localize(FlotillaNameKey, (Index(group, card) + 1).ToString())
                        )
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
                    GraphNodes.LabelPart(() => BattleText.Optional(phrase)),
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
                string number = (card.Index + 1).ToString();
                NodeVtable vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(
                            () => AgeText.Clean(Gui.Localize(FlotillaNameKey, number))
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
            string name = BattleText.Optional(ModStrings.BattleTactics);
            bool named = !string.IsNullOrEmpty(name);
            if (named)
            {
                builder.PushContext(name, null, false);
            }

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
            if (named)
            {
                builder.PopContext();
            }

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
            string name = BattleText.Optional(nameKey);
            bool named = !string.IsNullOrEmpty(name);
            if (named)
            {
                builder.PushContext(name, null, false);
            }

            BattlePlayCard it = card;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Clean(PlanTitleKey)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlayTitle), false),
                    GraphNodes.ValuePart(() => BattleNotifications.PlanEffects(it), false),
                },
                Sections = GraphNodes.Sections(null, card.Tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(ControlId.For(card, key), vtable, card),
                key,
                BattleNotifications.PlanDossiers(it, null)
            );
            if (named)
            {
                builder.PopContext();
            }
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

        /// <summary>
        /// The phases, as the flat run of sentences they are - flotilla-major, so that reading down is
        /// one flotilla through the battle.
        ///
        /// The game lays this out the other way round, a panel per phase each holding one item per
        /// flotilla, because that is how it draws it; the items are matched into flotillas by the
        /// position the game gave each inside its phase, which is the same flotilla order in every phase
        /// (<c>AdvancedReportPhaseItem.FilterFlotillas</c> walks one list). A phase the battle never
        /// reached draws no item, so that flotilla simply has one line fewer.
        ///
        /// A flotilla is NAMED at the head of its run only where more than one fought: the game writes
        /// the flotilla into every one of these sentences, so with a single flotilla the name would be a
        /// line saying what the next line says anyway.
        /// </summary>
        private static void Phases(
            GraphBuilder builder,
            AdvancedEncounterReportModalWindow window
        )
        {
            AgeTransform container = window.AdvancedReportPhaseItemContainer;
            // Flow control: everything below is a component scrape and a COUNT of flotillas, both of
            // which run before any node exists for the gate to see.
            if (container == null || !AgeWidgets.Visible(container))
            {
                return;
            }

            AdvancedReportPhaseItem[] items = container.GetComponentsInChildren<AdvancedReportPhaseItem>(
                true
            );
            // The flotillas, in the order every phase drew them.
            List<AdvancedReportPhaseFlotillaStatItem[]> phases =
                new List<AdvancedReportPhaseFlotillaStatItem[]>();
            int rows = 0;
            for (int i = 0; i < items.Length; i++)
            {
                AdvancedReportPhaseItem phase = items[i];
                // Which phases contribute a line at all, counted before any is declared.
                if (phase == null || !AgeWidgets.Visible(phase.AgeTransform))
                {
                    continue;
                }

                AdvancedReportPhaseFlotillaStatItem[] stats = Stats(phase);
                phases.Add(stats);
                if (stats.Length > rows)
                {
                    rows = stats.Length;
                }
            }

            // How many flotillas fought - the count that decides whether the runs are named.
            int fought = 0;
            for (int row = 0; row < rows; row++)
            {
                if (FirstStat(phases, row) != null)
                {
                    fought++;
                }
            }

            for (int row = 0; row < rows; row++)
            {
                AdvancedReportPhaseFlotillaStatItem first = FirstStat(phases, row);
                if (first == null)
                {
                    continue;
                }

                if (fought > 1)
                {
                    builder.AddItem(Nodes.Synthetic(
                        ControlId.Structural("battle-advanced/flotilla/" + row),
                        Flotilla(first, row)
                    ));
                }

                for (int column = 0; column < phases.Count; column++)
                {
                    AdvancedReportPhaseFlotillaStatItem stat =
                        row < phases[column].Length ? phases[column][row] : null;
                    // A phase this flotilla was not in: the game draws no item for it, and there is
                    // no sentence to read.
                    if (stat == null || !AgeWidgets.Visible(stat.AgeTransform))
                    {
                        continue;
                    }

                    builder.AddItem(Nodes.Drawn(
                        ControlId.For(stat, "battle-advanced/phase/" + row + "/" + column),
                        Stat(stat),
                        stat
                    ));
                }
            }
        }

        private static AdvancedReportPhaseFlotillaStatItem[] Stats(AdvancedReportPhaseItem phase)
        {
            try
            {
                AgeTransform container = phase.FlotillaStatItemContainer;
                // Flow control: a phase panel the battle never reached is not scraped for its items.
                return container == null || !AgeWidgets.Visible(container)
                    ? new AdvancedReportPhaseFlotillaStatItem[0]
                    : container.GetComponentsInChildren<AdvancedReportPhaseFlotillaStatItem>(true);
            }
            catch (Exception)
            {
                return new AdvancedReportPhaseFlotillaStatItem[0];
            }
        }

        /// <summary>The first phase this flotilla was DRAWN in - what says the flotilla fought at all.
        /// The container pools its items, so a flotilla the battle never fielded still has an item in
        /// every phase: hidden, unbound, and with an empty sentence on it.</summary>
        private static AdvancedReportPhaseFlotillaStatItem FirstStat(
            List<AdvancedReportPhaseFlotillaStatItem[]> cells,
            int row
        )
        {
            for (int i = 0; i < cells.Count; i++)
            {
                AdvancedReportPhaseFlotillaStatItem stat =
                    row < cells[i].Length ? cells[i][row] : null;
                // Spoken count: what this answers is how many flotillas FOUGHT, which decides whether
                // the runs are named at all - a count taken before any node exists for the gate to see.
                if (stat != null && AgeWidgets.Visible(stat.AgeTransform))
                {
                    return stat;
                }
            }

            return null;
        }

        /// <summary>Which flotilla a run of phase lines belongs to, in the game's own numbering - the
        /// panel draws the number on the arena cards beside it rather than on the lines.</summary>
        private static NodeVtable Flotilla(AdvancedReportPhaseFlotillaStatItem stat, int row)
        {
            int number = stat == null ? row + 1 : stat.VisualFlotillaIndex;
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () => AgeText.Clean(Gui.Localize(FlotillaNameKey, number.ToString()))
                    ),
                },
                OnFocusVisual = AgeWidgets.ReleasePointer,
            };
        }

        /// <summary>What the game wrote for this flotilla in this phase, kept whole and kept in the
        /// LINES it was written in, with the pointer parked on the item so the box it came from is on
        /// screen too.</summary>
        private static NodeVtable Stat(AdvancedReportPhaseFlotillaStatItem stat)
        {
            AgeTransform widget = stat.AgeTransform;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>(),
                Sections = null,
            };
            // LABEL FALLBACK: the stat item draws a range icon and two arcs and no words at all, so the
            // sentence the game wrote for it is the only thing that could name this row - the same rung
            // the ordinary naming ladder would have reached. Its lines are the row's own parts, so the
            // door has nothing left to announce twice.
            Prose(vtable, () => AgeText.Lines(AgeText.Tooltip(tooltip)), false, null);
            AgeWidgets.PointAt(vtable, widget);
            return vtable;
        }

        /// <summary>
        /// A block of the game's own prose as a control's readout: one announcement PART per line the
        /// game DREW, so the landing stays one utterance and the review buffer steps line by line.
        ///
        /// The breaks in these are the game's own punctuation, not a box running out of width. The
        /// phase sentence is written as a statement and then its damage tally
        /// (<c>AdvancedReportPhaseFlotillaStatItem.Refresh</c> :62), with each cloaking addendum
        /// appended as a paragraph of its own (:64-78); a damage bar's title is a caption and then its
        /// critical-hit clause. Joined into one part they became one unsteppable buffer line with a
        /// newline inside it, which is neither what the box draws nor something the buffer can walk.
        ///
        /// The COUNT is read as the node is built - this screen rebuilds every frame, so it follows the
        /// text - while each part re-reads its own line as it is spoken, like every other part here.
        /// <paramref name="fallback"/> is for a block the game has not written yet, where the control
        /// still has a name of its own to fall back on.
        /// </summary>
        private static void Prose(
            NodeVtable vtable,
            Func<IList<string>> lines,
            bool named,
            Func<string> fallback
        )
        {
            int count = Read(lines).Count;
            if (count == 0)
            {
                vtable.Announcements.Add(
                    named || fallback != null
                        ? GraphNodes.LabelPart(fallback ?? (() => null))
                        : GraphNodes.ValuePart(fallback ?? (() => null), false)
                );
                return;
            }

            for (int i = 0; i < count; i++)
            {
                int at = i;
                Func<string> line = () => Line(lines, at);
                vtable.Announcements.Add(
                    named && at == 0
                        ? GraphNodes.LabelPart(line)
                        : GraphNodes.ValuePart(line, false)
                );
            }
        }

        private static IList<string> Read(Func<IList<string>> lines)
        {
            try
            {
                IList<string> read = lines == null ? null : lines();
                return read ?? new List<string>();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        /// <summary>One line of a block as it stands NOW - a line the game has since dropped answers
        /// nothing rather than the wrong line.</summary>
        private static string Line(Func<IList<string>> lines, int index)
        {
            IList<string> read = Read(lines);
            return index < read.Count ? read[index] : null;
        }

        /// <summary>The rosters the fleet toggles slide over the phase panel, while one is up. Which
        /// side's is showing is the game's decision and is read off what is drawn. Only the player's
        /// panel draws flotilla lines, so only it is handed the arena cards
        /// (<see cref="FlotillaCards"/>) - the enemy's is a garrison, and its card is read in the
        /// heading instead (<see cref="Flotillas"/>).</summary>
        private static void Rosters(
            GraphBuilder builder,
            AdvancedEncounterReportModalWindow window
        )
        {
            Roster(
                builder,
                YoursRegion,
                ModStrings.BattleYourFleets,
                AgeWidgets.Transform(window.PlayerBattleGroupReportPanel),
                "battle-advanced/yours",
                FlotillaCards(window),
                Rewarded(window)
            );
            Roster(
                builder,
                TheirsRegion,
                ModStrings.BattleEnemyFleets,
                AgeWidgets.Transform(window.EnemyBattleGroupReportPanel),
                "battle-advanced/theirs",
                null,
                null
            );
            builder.SetRegion(null);
        }

        /// <summary>
        /// The player's roster panel as the kind that carries what the battle PAID.
        ///
        /// The window's field is typed as the base panel
        /// (<c>AdvancedEncounterReportModalWindow</c> :21), but the instance it binds is the player
        /// subclass, which is the only one with a rewards table - the experience gained, the resources
        /// earned and the salvage rescued, three labels the base panel has nowhere for (verified live
        /// 2026-08-30). The enemy's panel is the enemy subclass and has none of them, which is why only
        /// this side is asked.
        /// </summary>
        private static PlayerBattleGroupReportPanel Rewarded(
            AdvancedEncounterReportModalWindow window
        )
        {
            try
            {
                return window.PlayerBattleGroupReportPanel as PlayerBattleGroupReportPanel;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Roster(
            GraphBuilder builder,
            object region,
            string nameKey,
            AgeTransform panel,
            string prefix,
            BattleRosters.FlotillaExtras extras,
            PlayerBattleGroupReportPanel rewards
        )
        {
            // Flow control: the roster under a panel the report is not drawing is a walk of its own,
            // and a region and a context would be opened around nothing.
            if (panel == null || !AgeWidgets.Visible(panel))
            {
                return;
            }

            builder.SetRegion(region);
            string name = BattleText.Optional(nameKey);
            bool named = !string.IsNullOrEmpty(name);
            if (named)
            {
                builder.PushContext(name, null, false);
            }

            BattleRosters.Roster(builder, panel, prefix, extras);
            BattleNotifications.Rewards(builder, rewards, prefix);
            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>
        /// The two damage panels, a side each: one row per bar the gauge is showing, and the total
        /// underneath.
        ///
        /// A bar the game is not showing - a weapon type that never fired, missed shots while the toggle
        /// is off - is a cell of zero height the game hides, and there is nothing to say about it.
        /// </summary>
        private static void Damage(GraphBuilder builder, AdvancedEncounterReportModalWindow window)
        {
            Gauge(
                builder,
                YoursRegion,
                ModStrings.BattleYourDamage,
                window.PlayerDamageGauge,
                window.PlayerEncounterGroup,
                window.PlayerTotalDamageLabel,
                window.PlayerTotalDamageTooltip,
                "battle-advanced/your-damage"
            );
            Gauge(
                builder,
                TheirsRegion,
                ModStrings.BattleEnemyDamage,
                window.EnemyDamageGauge,
                window.EnemyEncounterGroup,
                window.EnemyTotalDamageLabel,
                window.EnemyTotalDamageTooltip,
                "battle-advanced/their-damage"
            );
            builder.SetRegion(null);
        }

        private static void Gauge(
            GraphBuilder builder,
            object region,
            string nameKey,
            DamageGauge gauge,
            EncounterGroup group,
            AgePrimitiveLabel total,
            AgeTooltip totalTooltip,
            string prefix
        )
        {
            // Flow control: same - the four readings below each walk something, and a region and a
            // context would be opened around nothing.
            if (gauge == null || !AgeWidgets.Visible(gauge.AgeTransform))
            {
                return;
            }

            builder.SetRegion(region);
            string name = BattleText.Optional(nameKey);
            bool named = !string.IsNullOrEmpty(name);
            if (named)
            {
                builder.PushContext(name, null, false);
            }

            Bars(builder, gauge.EffectiveDamageCells, prefix + "/effective");
            Bars(builder, gauge.AbsorbedDamageCells, prefix + "/absorbed");
            Missed(builder, gauge.MissedDamageGroup, group, prefix + "/missed");
            Total(builder, total, totalTooltip, prefix + "/total");
            if (named)
            {
                builder.PopContext();
            }
        }

        private static void Bars(GraphBuilder builder, AgeTransform table, string prefix)
        {
            // Flow control: the cells are found by a component scrape, not worth running for a gauge
            // the report is not drawing.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            DamageGaugeCell[] cells = table.GetComponentsInChildren<DamageGaugeCell>(true);
            for (int i = 0; i < cells.Length; i++)
            {
                DamageGaugeCell cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                DamageGaugeCell it = cell;
                builder.AddItem(Nodes.Drawn(
                    ControlId.For(cell, prefix + "/" + i),
                    Bar(
                        cell.AgeTransform,
                        () => AgeText.Lines(Title(it.DamageData)),
                        () => Description(it.DamageData)
                    ),
                    cell
                ));
            }
        }

        /// <summary>
        /// The shots that missed, which the game hangs on the band itself rather than on a cell of its
        /// own - the same wrapper underneath, so the same sentence - plus the one thing the BAND says
        /// that the sentence does not.
        ///
        /// The game writes the count ("Missed Shots: 2") and draws the PROPORTION: the band's height is
        /// the whole gauge's times <c>1 - hitRatio</c> (<c>DamageGauge.RefreshMissedDamage</c>
        /// :229-243), so a sighted player reads "about a fifth of the shooting missed" off a picture
        /// with no number on it, and a listener told "2" cannot recover it. So the row says the share,
        /// as a percentage, and the totals stay where the game put them, which is nowhere - the same
        /// call the balance of power makes (<see cref="BattleNotifications.BalanceText"/>).
        ///
        /// It is said only while the band is DRAWN, which is what the Show Missed Shots switch governs:
        /// the game hides the group when the switch is off or nothing missed, and the gate drops this
        /// node with it.
        /// </summary>
        private static void Missed(
            GraphBuilder builder,
            AgeTransform group,
            EncounterGroup side,
            string key
        )
        {
            if (group == null)
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(group);
            EncounterGroup it = side;
            NodeVtable vtable = Bar(
                group,
                () => AgeText.Lines(Title(Data(tooltip))),
                () => Description(Data(tooltip))
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => MissedShare(it), false));
            builder.AddItem(Nodes.Drawn(ControlId.For(group, key), vtable, group));
        }

        /// <summary>
        /// What share of this side's shooting missed, in the phrase the mod has for it.
        ///
        /// Computed from the same two properties the gauge sizes its band from and summed the same way
        /// (<c>DamageGauge.Refresh</c> :80-86, :106-108): every flotilla of the side, plus its
        /// citadels, which the game counts in and a flotilla walk would leave out. The gauge keeps the
        /// answer in a private field, so this re-derives it rather than reading it - and re-derives it
        /// off the game's own inputs rather than off the band's pixel height, which is scaled by a
        /// second ratio the band shares with the rest of the gauge.
        /// </summary>
        private static string MissedShare(EncounterGroup group)
        {
            try
            {
                if (group == null)
                {
                    return null;
                }

                float shots = DamageGauge.GetFlotillasPropertyValue(
                    group,
                    SimulationProperties.Flotilla.TotalShotSent
                );
                float hits = DamageGauge.GetFlotillasPropertyValue(
                    group,
                    SimulationProperties.Flotilla.TotalHitSent
                );
                for (int i = 0; group.Citadels != null && i < group.Citadels.Count; i++)
                {
                    EncounterCitadel citadel = group.Citadels[i];
                    if (citadel == null)
                    {
                        continue;
                    }

                    shots += citadel.GetPropertyValue(SimulationProperties.Flotilla.TotalShotSent);
                    hits += citadel.GetPropertyValue(SimulationProperties.Flotilla.TotalHitSent);
                }

                if (shots <= 0f)
                {
                    return null;
                }

                return BattleText.Optional(
                    ModStrings.BattleShotsMissed,
                    UnityEngine.Mathf.RoundToInt(
                        UnityEngine.Mathf.Clamp01(1f - hits / shots) * 100f
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("battle report: reading the missed-shot share threw: " + e);
                return null;
            }
        }

        private static void Total(
            GraphBuilder builder,
            AgePrimitiveLabel total,
            AgeTooltip tooltip,
            string key
        )
        {
            AgeTransform widget = total == null ? null : total.AgeTransform;
            if (widget == null)
            {
                return;
            }

            AgePrimitiveLabel it = total;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>(),
                Sections = null,
            };
            // LABEL FALLBACK: the label draws a bare figure and an icon ("2074 [damageApplied]"), and
            // the game's name for what it counts is written only in the tooltip - so the tooltip's
            // sentence names the row, with the drawn figure as the fallback where it is not written.
            Prose(
                vtable,
                () => AgeText.Lines(AgeText.Tooltip(tooltip)),
                true,
                () => AgeText.Label(it)
            );
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(total, key), vtable, total));
        }

        /// <summary>One bar of a damage gauge: the game's own caption for it - which is a caption and,
        /// where the bar has one, a clause of its own on a second line, so it is read a part per line
        /// (<see cref="Prose"/>) - and the tactical advice it hangs beside it kept for the review buffer,
        /// since the advice is the same paragraph every battle and a player comparing eight bars does
        /// not want to hear it eight times.</summary>
        private static NodeVtable Bar(
            AgeTransform widget,
            Func<IList<string>> title,
            Func<IList<string>> advice
        )
        {
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>(),
                Sections = GraphNodes.Sections(advice, null),
            };
            Prose(vtable, title, true, null);
            AgeWidgets.PointAt(vtable, widget);
            return vtable;
        }

        private static GuiDamageData Data(AgeTooltip tooltip)
        {
            try
            {
                return tooltip == null ? null : tooltip.Target as GuiDamageData;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Title(GuiDamageData data)
        {
            try
            {
                return data == null ? null : AgeText.Clean(data.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Everything the bar's own box says under its caption: the game's tactical advice, and - where
        /// one of the two plans moved this number - the game's sentence saying which.
        ///
        /// The second one is a whole feature of the tooltip the mod would otherwise drop.
        /// <c>GuiDamageData</c> is an <c>IAffectingPlaysProvider</c> and computes the plays that touched
        /// its own properties (:99-152); the <c>DamageGaugeCell</c> tooltip class lists
        /// <c>PanelFeatureAffectedByPlay</c> among its four features (measured off the live panel
        /// definition, 2026-08-30), which renders that list as a sentence and hides itself when the list
        /// is empty. Reading only Title and Description therefore lost it on every battle where a plan
        /// modified damage. The words are the game's own two keys, and which of them applies is the
        /// game's own rule (one play named, or the first two of several).
        /// </summary>
        private static IList<string> Description(GuiDamageData data)
        {
            try
            {
                if (data == null)
                {
                    return null;
                }

                IList<string> lines = AgeText.Lines(AgeText.Clean(data.Description));
                string plays = Plays(data);
                if (!string.IsNullOrEmpty(plays))
                {
                    lines.Add(plays);
                }

                return lines;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Plays(GuiDamageData data)
        {
            List<string> names = data.AffectingPlayNames;
            if (names == null || names.Count == 0)
            {
                return null;
            }

            return AgeText.Clean(
                names.Count == 1
                    ? Gui.Localize(OnePlayKey, Gui.GetLocalizedTitle(names[0]))
                    : Gui.Localize(
                        TwoPlaysKey,
                        Gui.GetLocalizedTitle(names[0]),
                        Gui.GetLocalizedTitle(names[1])
                    )
            );
        }

        /// <summary>The game's own two sentences for "a tactic moved this number", and the same choice
        /// between them the feature makes.</summary>
        private const string OnePlayKey = "%PanelFeatureAffectedByOnePlayDescription";
        private const string TwoPlaysKey = "%PanelFeatureAffectedByTwoPlaysDescription";

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

        private static void Checkbox(
            List<Cell> cells,
            AgeControlToggle toggle,
            string modKey,
            string gameKey,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (toggle == null)
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => Name(widget, modKey, gameKey),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Offered(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, widget);
            Cells.Add(cells, widget, ControlId.For(toggle, key), vtable);
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
            return string.IsNullOrEmpty(game) ? BattleText.Optional(modKey) : game;
        }

        private static void Note(GraphBuilder builder, AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || string.IsNullOrEmpty(AgeText.Label(label)))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(label, key), vtable, label));
        }

        /// <summary>A figure the game wrote as its own sentence ("18 &gt;&gt; 11 CP"), under the game's
        /// name for what it counts.</summary>
        private static void Value(
            GraphBuilder builder,
            AgePrimitiveLabel label,
            string titleKey,
            string key
        )
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || string.IsNullOrEmpty(AgeText.Label(label)))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Clean(titleKey)),
                    GraphNodes.ValuePart(() => AgeText.Label(it), false),
                },
                Sections = GraphNodes.Sections(null, AgeWidgets.Raw(widget)),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(label, key), vtable, label));
        }

        /// <summary>Who is leading this side, and the hero commanding it where there is one.</summary>
        private static void Leader(
            GraphBuilder builder,
            BattleGroupInfoPanel panel,
            string prefix
        )
        {
            if (panel == null)
            {
                return;
            }

            Note(builder, panel.MainLeaderName, prefix + "/leader");
            AgePrimitiveImage portrait = panel.MainHeroPortrait;
            AgeTransform widget = portrait == null ? null : portrait.AgeTransform;
            if (widget == null)
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(tooltip)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(portrait, prefix + "/hero"), vtable, portrait));
        }

        private static AdvancedEncounterReportModalWindow Window()
        {
            return GameWindows.Of<AdvancedEncounterReportModalWindow>();
        }
    }
}

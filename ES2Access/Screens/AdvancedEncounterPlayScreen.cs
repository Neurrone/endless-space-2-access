using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The advanced battle setup - the window the setup popup's Advanced button opens, where a fight is
    /// planned rather than merely accepted (<c>AdvancedEncounterPlayModalWindow</c>).
    ///
    /// The setup popup offers one decision (which battle plan) as a carousel. This window is the same
    /// decision laid out as a HAND: every plan the empire has as a card of its own, the three plans this
    /// opponent has used against you recently, both sides' fleets in the flotillas they will fight in, and
    /// the sorting that decides which ship goes in which flotilla. It is where a player who wants to win a
    /// battle rather than watch one spends their time, and it is drawn almost entirely as pictures.
    ///
    /// Tab crosses it in five stops, each naming itself as focus enters: the heading, the TACTICS
    /// ("Tactics" - the three plans and nothing else), YOUR FLEET ("Your fleets" - your leader, your hero
    /// and your flotillas), THEIRS ("Enemy fleets"), the STATS ("Stats" - the four pager rows and the
    /// fighters line), and then the controls. The two fleets are walked SIDE BY SIDE like the battle
    /// popups rather than in the order the window draws them - the two decks are drawn opposite each
    /// other and the sorting sits between them - for the same reason the popups do it: the player is
    /// comparing two sides. The DECISION is lifted out ahead of both (owner ruling 2026-08-29): picking a
    /// plan is what this window is for, and burying the hand at the head of a fleet made the cost of
    /// reaching it the length of the roster.
    ///
    /// Your plans are RADIOS: each card's Enter is the card's own click, which is what tells the window
    /// which plan is in force (<c>OnClickPlayCb</c>, which ignores a click on the plan already chosen, so
    /// nothing here can be double-committed). There is no confirm - the plan takes effect as it is picked,
    /// and Start Battle is what commits the whole setup.
    ///
    /// Their three cards are READOUTS rather than radios, which is a deliberate departure from click
    /// parity: the game wires every play card the same way, so a click on one of THEIR cards runs
    /// <c>OnClickPlayCb</c> too - and that handler sets the PLAYER's plan to whichever card was clicked
    /// (<c>PlayerEncounterGroup.SetSetupPlay</c>). Reproducing that would let a player's Enter silently
    /// replace their battle plan with the enemy's while reading what the enemy tends to do. The cards are
    /// what they look like - a record of the opponent's habits - so they read as such.
    /// OWNER-RATIFIED (2026-08-12): a click that is a game bug is not given a key. Do not restore
    /// parity here; if the game ever fixes the handler to ignore enemy-card clicks, these can become
    /// plain refused controls instead.
    ///
    /// The sorting buttons are declared with the words the game explains them with, because it draws them
    /// as icons and gives them no titles at all (its localization has a description per button and no
    /// title). The rosters follow the two fleet switches: the game slides a roster panel out, so what is
    /// declared is what is DRAWN.
    ///
    /// ARRANGING THE FLEET is the other half of what this window is for, and the game draws all of it in
    /// the 3D arena: a ship is a 24-pixel chip, pinned to its flotilla by a double click and moved to
    /// another by a drag onto that flotilla's card. Neither gesture exists on the roster lines the
    /// keyboard walks, so both are given to those lines instead, each on the chord that MEANS that
    /// gesture - a ship row carries the pin on the double-click chord and is something to pick up
    /// (<see cref="Arrangeable"/>), and both a flotilla row and any ship row inside it are somewhere to
    /// put a ship down (<see cref="Destination"/>) - and the commands themselves stay the game's
    /// (<see cref="BattleShipMoves"/>). Only THIS window declares any of it: the roster reader is shared
    /// with the battle report popups, which hand in no such hooks and read exactly as they always did.
    ///
    /// The STATS are the one place this window is walked as something other than what it draws: the four
    /// switches and the one box behind them are declared as four ROWS, one per page, and standing on a row
    /// is what turns the box (<see cref="Pages"/>). The switches themselves are not declared - the list
    /// does their whole job, the same ruling the tutorial's dots and page arrows got - so the coverage
    /// audit reports four uncovered actions here by design.
    ///
    /// It reports the arena's ship chips the same way, for the same reason: each
    /// <c>FlotillaCard3DContainerLeft/ShipItem/LockButton</c> carries the double click that pins a ship,
    /// and each reads as "no node stands here" because the node carrying that command stands on the
    /// ROSTER LINE for the same ship instead (<see cref="Arrangeable"/>). One uncovered action per ship in
    /// the player's flotillas, always - measured 2026-08-29 as two on a two-ship fleet - and not one of
    /// them is an affordance the keyboard is missing. The audit matches a node to the widget it stands on,
    /// and this is the window where the thing a player works and the thing the game draws it on are two
    /// different widgets.
    ///
    /// Every figure on those pages is a coloured arc with no number written anywhere on it, and each page
    /// says what its arcs are DRAWN at rather than what the numbers behind them would be
    /// (<see cref="BattleArcs"/>): the window works the damage and range figures out from every module of
    /// every ship, and a mod-side reimplementation of that arithmetic would be free to drift from the
    /// picture the player is deciding on. The military page is the exception only in that the game already
    /// has a sentence for it (<see cref="BattleNotifications.BalanceText"/>).
    ///
    /// Escape is the game's, and it is not a plain close: <c>HandleInput</c> puts the battle-setup
    /// notification back up, which is where the player came from and where the fight is actually started.
    /// </summary>
    public sealed partial class AdvancedEncounterPlayScreen : Screen
    {
        private static readonly object HeadingStop = "advanced-play:heading";

        /// <summary>The one DECISION this window exists for - the three plans, and nothing else -
        /// is a stop of its own (owner ruling 2026-08-29), so a player who came to pick a tactic
        /// reaches it in one key and never walks a fleet to get there. It is also where Tab starts
        /// (<see cref="InitialFocusStop"/>).</summary>
        private static readonly object TacticsStop = "advanced-play:tactics";

        /// <summary>Your side's FLEET - who is leading it and the flotillas the ships are arranged
        /// into - with the plans lifted out of it into <see cref="TacticsStop"/>. It keeps the
        /// context word ("Your fleets") the split left behind, because that word names the fleet and
        /// not the hand of cards.</summary>
        private static readonly object YoursStop = "advanced-play:yours-stop";

        /// <summary>The enemy's side is a stop of its own, not the tail of the one holding your plans
        /// and flotillas (owner ruling 2026-08-29): the two sides are what this window is walked as,
        /// and Tab is the key that crosses between them. It keeps its region as well, so Alt+Up/Down
        /// still names it, and the context word ("Enemy fleets") still announces itself on the way
        /// in.</summary>
        private static readonly object TheirsStop = "advanced-play:theirs-stop";

        private static readonly object StatsStop = "advanced-play:stats";
        private static readonly object ControlsStop = "advanced-play:controls";

        private static readonly object TacticsRegion = "advanced-play:tactics-region";
        private static readonly object YoursRegion = "advanced-play:yours";
        private static readonly object TheirsRegion = "advanced-play:theirs";
        private static readonly object FiguresRegion = "advanced-play:figures";

        // The game's own titles for the things it draws as pictures.

        /// <summary>The game's own sentence for a range, which it writes as "{0} Range" over the bare
        /// name a range localizes to.</summary>
        private const string RangeTitleKey = "%AdvancedPlayFlotillaOptimalRangeTitle";

        /// <summary>A path key, so <see cref="FocusedPage"/> can read back which stats page the cursor
        /// is standing on.</summary>
        private const string StatPageKey = "advanced-play:stat/";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.advanced-battle-setup"; }
        }

        /// <summary>Over the battle-setup notification it is opened from and returns to, and under the
        /// message box.</summary>
        public override int Layer
        {
            get { return 48; }
        }

        /// <summary>What the window has written across its top.</summary>
        public override string ScreenName
        {
            get
            {
                try
                {
                    AdvancedEncounterPlayModalWindow window = Window();
                    return window == null ? null : AgeText.Label(window.WindowTitleLabel);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>The plans, because picking one is what the window is for - and since the split they
        /// are a stop that holds nothing else, so the landing is the decision itself.</summary>
        public override object InitialFocusStop
        {
            get { return TacticsStop; }
        }

        public override bool IsActive()
        {
            try
            {
                AdvancedEncounterPlayModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: Exit puts the setup popup back, which is somewhere to go rather than
        /// nowhere.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            AdvancedEncounterPlayModalWindow window = Window();
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
                builder.SetRegion(null);

                builder.BeginStop(YoursStop);
                Yours(builder, window);
                builder.SetRegion(null);

                builder.BeginStop(TheirsStop);
                Theirs(builder, window);
                builder.SetRegion(null);

                builder.BeginStop(StatsStop);
                Figures(builder, window);

                builder.BeginStop(ControlsStop);
                Controls(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: reading the window threw: " + e);
            }
        }

        /// <summary>
        /// What the window has written across its top, and where the battle will be fought: the title
        /// the window is called by, the system, the arena, and the citadel line the game only draws for
        /// an orbit one is guarding.
        ///
        /// The TITLE is a row of its own because the game hung a sentence on it - what this window is
        /// FOR, rather than what it is called ("Gives you more options for preparing your battle
        /// plan") - and a screen name is spoken on arrival and then gone. As a row it is the first
        /// thing the heading stop holds, which is where a player looking for what a window does
        /// arrives.
        ///
        /// The system and the arena are each drawn as a LABEL with a wordless icon beside it, and the
        /// game explains the pair on whichever of the two it felt like: the arena's sentence is on the
        /// icon and its label carries a tooltip the engine could never draw, the system's is the other
        /// way round. So both rows are declared the same way and <see cref="Note"/> asks the engine
        /// which of the two the game would really draw (measured 2026-08-29).
        /// </summary>
        private void Heading(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            BattleRows.NoteBeside(builder, window.WindowTitleLabel, "advanced-play/title");
            BattleRows.NoteBeside(
                builder,
                window.LocationLabel,
                "advanced-play/location",
                Beside(window.LocationLabel)
            );
            BattleRows.NoteBeside(
                builder,
                window.ArenaNameLabel,
                "advanced-play/arena",
                Beside(window.ArenaNameLabel)
            );
            BattleRows.NoteBeside(builder, window.ProtectedByCitadelLabel, "advanced-play/citadel");
        }

        /// <summary>The icon the game draws beside a heading label, inside the little box it draws the
        /// pair in: the line's SECOND hover surface, found by asking the engine which sibling carries
        /// something it would draw rather than by a name in the prefab. Null for a label the game drew
        /// alone.</summary>
        private static AgeTransform Beside(AgePrimitiveLabel label)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            IList<AgeTransform> children = Children(widget == null ? null : widget.Parent);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && child != widget && AgeWidgets.Draws(AgeWidgets.Raw(child)))
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// The decision: every plan the empire could send this fleet in with, and nothing else.
        ///
        /// The plans ARE a numbered set - how many tactics there are to choose between is what the
        /// drawn hand tells a sighted player at a glance - and now that they are alone in a level of
        /// their own, the announcer's own stamp is what says so; the context is opened with positions
        /// LEFT ON for exactly that reason, where every other context on this window suppresses them
        /// (owner ruling 2026-08-29, which is also what retired the hand-declared position part this
        /// screen used to carry).
        /// </summary>
        private void Tactics(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            builder.SetRegion(TacticsRegion);
            bool named = BattleRows.Context(builder, ModStrings.BattleTactics, true);
            try
            {
                Plans(builder, window.PlayerPlaySelectionTable, "advanced-play/plan");
            }
            finally
            {
                BattleRows.Close(builder, named);
            }
        }

        /// <summary>Your side's fleet: who is leading it and - while the switch has it out - the
        /// flotillas the ships are arranged into. The plans are a stop of their own
        /// (<see cref="Tactics"/>).</summary>
        private void Yours(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            builder.SetRegion(YoursRegion);
            bool named = BattleRows.Context(builder, ModStrings.BattleYourFleets);
            try
            {
                BattleRows.Leader(builder, window.PlayerBattleGroupInfoPanel, "advanced-play/yours");
                Roster(
                    builder,
                    window.PlayerBattleGroupSetupPanel,
                    "advanced-play/yours",
                    FlotillaCards(window)
                );
            }
            finally
            {
                BattleRows.Close(builder, named);
            }
        }

        /// <summary>Their side: who is leading it, what they have played against you recently, and - while
        /// the switch has it out - what they have brought.</summary>
        private void Theirs(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            builder.SetRegion(TheirsRegion);
            bool named = BattleRows.Context(builder, ModStrings.BattleEnemyFleets);
            try
            {
                BattleRows.Leader(builder, window.EnemyBattleGroupInfoPanel, "advanced-play/theirs");
                // Flow control: the history table is walked card by card, and the switch that hides
                // the deck hides the whole of it.
                if (AgeWidgets.Visible(window.EnemyDeckGroup))
                {
                    History(builder, window.EnemyPlaySelectionTable, "advanced-play/their-plan");
                }

                // No cards to match against: the enemy's side of this window is a fleet, drawn as one
                // garrison panel with no flotilla lines in it at all and backed by a
                // ship-card container rather than a flotilla one (measured 2026-08-29).
                Roster(builder, window.EnemyBattleGroupSetupPanel, "advanced-play/theirs", null);
            }
            finally
            {
                BattleRows.Close(builder, named);
            }
        }

        /// <summary>
        /// Every plan the empire has, as the one-of-N the window made them.
        ///
        /// Keyed on the card's POSITION rather than on the card object: the table pools its cards and
        /// re-binds them by index for each battle, so a cursor keyed on the widget would be standing on a
        /// different plan the next time a fight starts.
        ///
        /// A card is not a title: it PRINTS the effects the plan applies, and the window draws all
        /// three cards at once, each permanently on its own plan - so those lines are always-drawn text
        /// and belong in what the row says (<see cref="BattlePlans.PlanEffects(BattlePlayCard)"/>).
        /// The family badge and the three range diagrams drawn on the card are hover surfaces of their
        /// own and become child entries, exactly as they do behind the setup popup's chooser
        /// (<see cref="BattlePlans.PlanDossiers"/>), with nothing to turn first.
        /// </summary>
        private void Plans(GraphBuilder builder, AgeTransform table, string prefix)
        {
            _cells.Clear();
            IList<AgeTransform> children = Children(table);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                BattlePlayCard card = Card(widget);
                if (card == null || card.Toggle == null)
                {
                    continue;
                }

                BattlePlayCard it = card;
                AgeTransform at = widget;
                AgeTooltip tooltip = card.Tooltip;
                NodeVtable vtable = GraphNodes.Radio(
                    () => PlanName(it),
                    () => it.Toggle != null && it.Toggle.State,
                    () => AgeWidgets.Toggle(it.Toggle),
                    () => AgeWidgets.Operable(at),
                    null,
                    tooltip
                );
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(() => BattlePlans.PlanEffects(it), false)
                );
                GraphNodes.AddRefusal(vtable, tooltip, () => AgeWidgets.Operable(at));
                AgeWidgets.Point(vtable, it.Toggle, tooltip, at);
                string key = prefix + "/" + i;
                Cell cell = Cells.Add(_cells, widget, ControlId.Structural(key), vtable);
                cell.Dossiers = BattlePlans.PlanDossiers(it, null);
                cell.Key = key;
            }

            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>What the opponent has played against you recently - a record, not a choice (see the
        /// class comment). Each says which plan it was and carries the game's own card tooltip, which is
        /// where the effects and the times-used count are written.</summary>
        private void History(GraphBuilder builder, AgeTransform table, string prefix)
        {
            _cells.Clear();
            IList<AgeTransform> children = Children(table);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                BattlePlayCard card = Card(widget);
                if (card == null)
                {
                    continue;
                }

                BattlePlayCard it = card;
                NodeVtable vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeText.Clean(BattleRows.SetupPlanTitleKey)),
                        GraphNodes.ValuePart(() => PlanName(it), false),
                    },
                    Sections = GraphNodes.Sections(null, card.Tooltip),
                };
                AgeWidgets.PointAt(vtable, widget);
                Cells.Add(_cells, widget, ControlId.Structural(prefix + "/" + i), vtable);
            }

            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>What a plan is called: the game's own title for it, else the words the card drew (which
        /// it wraps to fit its box).</summary>
        private static string PlanName(BattlePlayCard card)
        {
            try
            {
                return AgeText.Label(card.PlayTitle);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Everything the player works: the two roster switches, the sorting, whether to watch, and the
        /// three ways this window can end.
        ///
        /// The sorting buttons and the way out are read off what the window drew
        /// (<see cref="Cells.AddControl"/> names a wordless button by the sentence its tooltip opens with),
        /// which is the only name the game has for them. The sorting band as a whole is switched off while
        /// there is only one flotilla to sort (<c>SortingButtonsGroup.Enable</c>), and each button reads
        /// that as being unavailable.
        /// </summary>
        private void Controls(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            _cells.Clear();
            Checkbox(
                window.ShowYourFleetsToggle,
                ModStrings.BattleShowYourFleets,
                null,
                "advanced-play:show-yours"
            );
            Checkbox(
                window.ShowEnemyFleetsToggle,
                ModStrings.BattleShowEnemyFleets,
                null,
                "advanced-play:show-theirs"
            );
            Sorting(window);
            Checkbox(
                window.WatchBattleToggle,
                null,
                BattleRows.WatchToggleTitleKey,
                "advanced-play:watch"
            );
            Command(window.StartBattleButton, BattleRows.StartTitleKey, "advanced-play:start");
            Command(window.RetreatButton, BattleRows.RetreatTitleKey, "advanced-play:retreat");
            Countdown(window, "advanced-play:timer");
            Cells.AddControl(_cells, ByHandler(window, "OnBackCb"), "advanced-play:back");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// The three ways the window can distribute ships between flotillas. Each is a wordless icon the
        /// game explains in a sentence, which is the name it gets.
        ///
        /// The BAND has a sentence of its own - what a preset is FOR, rather than what one of them does -
        /// and the game hung it on the group that CONTAINS the buttons, which no mouse can rest on
        /// without resting on a button. Nothing on this screen stands on that group, so the sentence
        /// would be words the player can never reach; it is reviewable on the first button instead. A
        /// section, not a second aimed tooltip: a control draws the one tooltip it points at, and that
        /// one is the button's own.
        /// </summary>
        private void Sorting(AdvancedEncounterPlayModalWindow window)
        {
            Band(
                Cells.AddControl(
                    _cells,
                    window.BestRangeSortingButton,
                    "advanced-play:sort-best-range"
                ),
                window
            );
            Cells.AddControl(_cells, window.OptimalSortingButton, "advanced-play:sort-optimal");
            Cells.AddControl(_cells, window.BalancedSortingButton, "advanced-play:sort-balanced");
            // The one that puts the player's own arrangement back has no field on the window, so it is
            // found the same way the way out is.
            Cells.AddControl(
                _cells,
                ByHandler(window, "OnResetSortingCb"),
                "advanced-play:sort-reset"
            );
        }

        /// <summary>The band's own sentence, written into the first button's reviewable content. It
        /// hangs on the group the window keeps the buttons IN, which the window names no field of its
        /// own for - the same reason the way out is found by its handler - so it is read one step up
        /// from the buttons group and only where the game really wrote one.</summary>
        private static void Band(Cell cell, AdvancedEncounterPlayModalWindow window)
        {
            AgeTransform buttons = window.SortingButtonsGroup;
            NodeSection band = GraphNodes.ReviewedTooltipSection(
                AgeWidgets.Raw(buttons == null ? null : buttons.Parent)
            );
            if (cell == null || band == null)
            {
                return;
            }

            List<NodeSection> sections = new List<NodeSection>(3);
            IList<NodeSection> own = cell.Vtable.Sections;
            for (int i = 0; own != null && i < own.Count; i++)
            {
                sections.Add(own[i]);
            }

            sections.Add(band);
            cell.Vtable.Sections = sections;
        }

        /// <summary>A box the player ticks, named by the mod where the game names it nowhere and by the
        /// game everywhere else (<see cref="BattleRows.Checkbox"/>).</summary>
        private void Checkbox(
            AgeControlToggle toggle,
            string modKey,
            string gameKey,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            BattleRows.Checkbox(_cells, toggle, Name(widget, modKey, gameKey, null), key);
        }

        /// <summary>A button the window drew as an icon, under the game's own title for it, refusing with
        /// the game's own reason - the retreat button carries the failure infos for a fleet that cannot
        /// run (<see cref="BattleRows.Command"/>).</summary>
        private void Command(AgeTransform widget, string titleKey, string key)
        {
            BattleRows.Command(
                _cells,
                widget,
                Name(widget, null, titleKey, AgeWidgets.Raw(widget)),
                key
            );
        }

        /// <summary>How long is left, for a battle the game is timing
        /// (<see cref="BattleRows.Countdown"/>): this window's clock is the notification's own, which
        /// it is only worth asking for once the window has one.</summary>
        private void Countdown(AdvancedEncounterPlayModalWindow window, string key)
        {
            NotificationBattleSetup notification = window.NotificationBattleSetup;
            if (notification == null)
            {
                return;
            }

            NotificationBattleSetup it = notification;
            BattleRows.Countdown(_cells, window.TimerGauge, () => it.GetTimeLeftRatio(), key);
        }

        /// <summary>A button the window keeps in no field of its own - the way out, and the reset beside
        /// the sorting: found by the HANDLER it is wired to rather than by a name in the prefab, because the
        /// handler is in the window's own code and a prefab name is a guess.</summary>
        private static AgeTransform ByHandler(
            AdvancedEncounterPlayModalWindow window,
            string method
        )
        {
            return AgeWidgets.Transform(
                AgeWidgets.WiredTo(window == null ? null : window.AgeTransform, method)
            );
        }

        /// <summary>What a control is called: the words the game drew on it, else the game's own title for
        /// it, else the sentence its tooltip opens with, else the mod's own word - in that order, because
        /// every step of it is the game's voice and the last one is a last resort.</summary>
        private static Func<string> Name(
            AgeTransform widget,
            string modKey,
            string gameKey,
            AgeTooltip tooltip
        )
        {
            AgeTransform at = widget;
            AgeTooltip tip = tooltip;
            return () =>
            {
                string drawn = AgeWidgets.TextOf(at);
                if (!string.IsNullOrEmpty(drawn))
                {
                    return drawn;
                }

                string game = string.IsNullOrEmpty(gameKey) ? null : AgeText.Clean(gameKey);
                if (!string.IsNullOrEmpty(game))
                {
                    return game;
                }

                string hinted = CardActions.FirstLine(tip);
                return string.IsNullOrEmpty(hinted) ? OptionalText.Phrase(modKey) : hinted;
            };
        }

        private static BattlePlayCard Card(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<BattlePlayCard>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IList<AgeTransform> Children(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Children;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AdvancedEncounterPlayModalWindow Window()
        {
            return GameWindows.Of<AdvancedEncounterPlayModalWindow>();
        }

        /// <summary>Where this screen is drawn, for the tooltip audit (see
        /// <see cref="ES2Access.Screens.Screen.RootTransform"/>).</summary>
        public override AgeTransform RootTransform
        {
            get { return RootOf(Window()); }
        }
    }
}

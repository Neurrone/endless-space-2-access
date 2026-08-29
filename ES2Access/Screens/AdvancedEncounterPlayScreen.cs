using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

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
    public sealed class AdvancedEncounterPlayScreen : Screen
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

        /// <summary>The game's own titles for the things it draws as pictures.</summary>
        private const string PlanTitleKey = "%NotificationBattleSetupSelectedPlayTitle";
        private const string StartTitleKey = "%NotificationBattleSetupStartButtonTitle";
        private const string RetreatTitleKey = "%NotificationBattleSetupRetreatButtonTitle";
        private const string WatchToggleTitleKey = "%NotificationBattleSetupWatchToggleTitle";

        /// <summary>The mod's own, for the things the game names nowhere - the four bands Tab crosses
        /// and the two roster switches. All are asked for optionally: a build without the phrase
        /// leaves that line out rather than reading a key.</summary>
        private const string TacticsKey = "battle.tactics";
        private const string YourFleetsKey = "battle.your-fleets";
        private const string EnemyFleetsKey = "battle.enemy-fleets";
        private const string StatsKey = "battle.stats";
        private const string ShowYourFleetsKey = "battle.show-your-fleets";
        private const string ShowEnemyFleetsKey = "battle.show-enemy-fleets";
        private const string TimeLeftKey = "battle.time-left";

        /// <summary>What the four stats pages are called, and what each of them says. The game names
        /// none of this: its four switches are wordless icons with a description apiece, and every
        /// figure on every page is a coloured arc with no number written anywhere on it.</summary>
        private const string StatsTrajectoriesKey = "battle.stats-trajectories";
        private const string StatsMilitaryKey = "battle.stats-military";
        private const string StatsDamageKey = "battle.stats-damage";
        private const string StatsRangeKey = "battle.stats-range";
        private const string FlotillaRangeKey = "battle.flotilla-range";
        private const string EnergyShareKey = "battle.energy-damage-share";
        private const string ProjectileShareKey = "battle.projectile-damage-share";
        private const string EnergyThreatKey = "battle.energy-bigger-threat";
        private const string ProjectileThreatKey = "battle.projectile-bigger-threat";
        private const string ShortRangeShareKey = "battle.short-range-share";
        private const string MediumRangeShareKey = "battle.medium-range-share";
        private const string LongRangeShareKey = "battle.long-range-share";
        private const string ShortRangeMattersKey = "battle.short-range-matters";
        private const string MediumRangeMattersKey = "battle.medium-range-matters";
        private const string LongRangeMattersKey = "battle.long-range-matters";

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
            Note(builder, window.WindowTitleLabel, "advanced-play/title");
            Note(
                builder,
                window.LocationLabel,
                "advanced-play/location",
                Beside(window.LocationLabel)
            );
            Note(
                builder,
                window.ArenaNameLabel,
                "advanced-play/arena",
                Beside(window.ArenaNameLabel)
            );
            Note(builder, window.ProtectedByCitadelLabel, "advanced-play/citadel");
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
            bool named = Context(builder, TacticsKey, true);
            try
            {
                Plans(builder, window.PlayerPlaySelectionTable, "advanced-play/plan");
            }
            finally
            {
                Close(builder, named);
            }
        }

        /// <summary>Your side's fleet: who is leading it and - while the switch has it out - the
        /// flotillas the ships are arranged into. The plans are a stop of their own
        /// (<see cref="Tactics"/>).</summary>
        private void Yours(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            builder.SetRegion(YoursRegion);
            bool named = Context(builder, YourFleetsKey);
            try
            {
                Leader(builder, window.PlayerBattleGroupInfoPanel, "advanced-play/yours");
                Roster(
                    builder,
                    window.PlayerBattleGroupSetupPanel,
                    "advanced-play/yours",
                    FlotillaCards(window)
                );
            }
            finally
            {
                Close(builder, named);
            }
        }

        /// <summary>Their side: who is leading it, what they have played against you recently, and - while
        /// the switch has it out - what they have brought.</summary>
        private void Theirs(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            builder.SetRegion(TheirsRegion);
            bool named = Context(builder, EnemyFleetsKey);
            try
            {
                Leader(builder, window.EnemyBattleGroupInfoPanel, "advanced-play/theirs");
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
                Close(builder, named);
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
        /// and belong in what the row says (<see cref="BattleNotifications.PlanEffects(BattlePlayCard)"/>).
        /// The family badge and the three range diagrams drawn on the card are hover surfaces of their
        /// own and become child entries, exactly as they do behind the setup popup's chooser
        /// (<see cref="BattleNotifications.PlanDossiers"/>), with nothing to turn first.
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
                    GraphNodes.ValuePart(() => BattleNotifications.PlanEffects(it), false)
                );
                GraphNodes.AddRefusal(vtable, tooltip, () => AgeWidgets.Operable(at));
                AgeWidgets.Point(vtable, it.Toggle, tooltip, at);
                string key = prefix + "/" + i;
                Cell cell = Cells.Add(_cells, widget, ControlId.Structural(key), vtable);
                cell.Dossiers = BattleNotifications.PlanDossiers(it, null);
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
                        GraphNodes.LabelPart(() => AgeText.Clean(PlanTitleKey)),
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

        /// <summary>One side's fleets, in the flotillas they will fight in, while the switch has the panel
        /// out. The shared roster reading answers the whole of it, plus whatever this window draws about
        /// a flotilla that the roster panel does not (<paramref name="extras"/>).</summary>
        private static void Roster(
            GraphBuilder builder,
            BattleGroupSetupPanel panel,
            string prefix,
            BattleRosters.FlotillaExtras extras
        )
        {
            BattleRosters.Roster(builder, Widget(panel), prefix, extras);
        }

        /// <summary>
        /// The other half of a flotilla, which this window is the only screen to draw.
        ///
        /// A flotilla appears twice here: as a line of ships in the roster panel, and as the card in the
        /// 3D arena the player drags ships onto. The card is where the game writes what the line never
        /// says - the sentence that says a flotilla is LOCKED and what would unlock it ("Unlocked at 5
        /// CP and 2 Ships", <c>EncounterPlayFlotillaCard3D.RefreshInfo</c> :66-101), the minimum for the
        /// one that is open ("Minimum 1 CP"), and the hover sentence naming the range it is optimal at
        /// and how well the ships suit it (<c>EncounterPlayFlotillaCard2D.Refresh</c> :123). All of it
        /// is on the player's screen already; none of it is anywhere a roster row could reach.
        ///
        /// The card is found through the game's own binding rather than by walking widget names: the 2D
        /// cards are bound with the flotilla's INDEX on them
        /// (<c>EncounterPlayFlotillaCardContainer.BindFlotillaCard2D</c>) and each holds its own 3D card
        /// and flotilla data. The line is matched to it by the NUMBER the line draws, never by child
        /// order - the two collections are built by different code and agreeing today is not a
        /// contract.
        /// </summary>
        private static BattleRosters.FlotillaExtras FlotillaCards(
            AdvancedEncounterPlayModalWindow window
        )
        {
            AdvancedEncounterPlayModalWindow it = window;
            return new BattleRosters.FlotillaExtras
            {
                Drawn = line => AgeWidgets.DrawnLabel(CommandPoints(Card(it, line))),
                Tooltip = line => AgeWidgets.Raw(Widget((GuiPanel)Card(it, line))),
                Row = (line, vtable) => Destination(it, line, vtable),
                Ship = (line, item, vtable) => Arrangeable(it, line, item, vtable),
            };
        }

        /// <summary>
        /// A flotilla row - or a ship row inside one - as somewhere to PUT A SHIP DOWN: the card's own
        /// half of the drag the arena draws, given to the lines the keyboard walks. Both rows get the
        /// same card because the game's own drop is a hit test against whichever flotilla card contains
        /// the dropped point, and a ship is drawn on its flotilla's card.
        ///
        /// The acceptance test and the drop are both the game's
        /// (<see cref="BattleShipMoves.Accepts"/>, <see cref="BattleShipMoves.Drop"/>), so the "drop
        /// target" word appears on exactly the flotillas <c>CanAddShip</c> would take the ship into -
        /// never on the one it is already in, and never on one the battle has locked - and a player
        /// who presses the key on a locked one anyway hears the game's own sentence for what would
        /// unlock it, which is written on the card and nowhere else.
        ///
        /// A row the window is drawing no card for takes nothing: the enemy's side has no flotilla
        /// cards at all, and a line whose number matches none of them is a line this screen cannot
        /// act on.
        /// </summary>
        private static void Destination(
            AdvancedEncounterPlayModalWindow window,
            FlotillaLine line,
            NodeVtable vtable
        )
        {
            EncounterPlayFlotillaCard3DInteractive card = Card3D(Card(window, line));
            if (card == null)
            {
                return;
            }

            EncounterPlayFlotillaCard3DInteractive at = card;
            FlotillaLine it = line;
            vtable.DropKind = BattleShipMoves.Kind;
            vtable.DropAccepts = held =>
                BattleShipMoves.Accepts(at, held.Cargo as EncounterPlayShipItemInteractive);
            vtable.OnDrop = held =>
                BattleShipMoves.Landed(at, held, BattleRosters.FlotillaNumber(it));
        }

        /// <summary>
        /// A ship row as something the player ARRANGES: whether it is pinned to the flotilla it is in,
        /// the carry that moves it to another, and the flotilla it is already in as somewhere to put
        /// another ship down.
        ///
        /// THE LOCK IS ON THE DOUBLE-CLICK CHORD, because that is the gesture the game puts it on: the
        /// chip in the arena is pinned by a second click and by nothing else, and every chord in this
        /// mod means the game's own gesture and nothing else (owner ruling 2026-08-29, reversing the
        /// activation-key binding of the same day). The row keeps the two state words - it says which
        /// state it is in whenever it is read, and says the new one the moment the chord turns it over -
        /// and it keeps NO role word: with Enter no longer its toggle the row is not a checkbox, and a
        /// line the player reads, drags and double-clicks is the roster's own plain line with more on
        /// it (the buffer's derived hint is what names the chord). What being locked MEANS is what the
        /// sorting buttons above do: a pinned ship is the one they leave where the player put it.
        ///
        /// Enter is left free for the DROP: while a ship is being carried, the activation key on this
        /// row lands it in the flotilla this ship is in, which is the same commit the flotilla's own
        /// line makes (<see cref="Destination"/>) - and the game's own drop is a hit test against
        /// whichever flotilla card contains the point, so a ship's card and its flotilla's card are
        /// one and the same target. With nothing held Enter on the row does nothing at all, as the
        /// chip's own single click does.
        ///
        /// The pick-up is offered wherever a chip exists, exactly as the mouse's drag is - a drag onto
        /// a flotilla that will not take the ship is how the game tells a player why not, and taking
        /// that away would take the answer with it. It is the CHIP that is carried, because the chip
        /// is what the game's own drop moves; the name is the row's, captured at pick-up like every
        /// carry's.
        /// </summary>
        private static void Arrangeable(
            AdvancedEncounterPlayModalWindow window,
            FlotillaLine line,
            BattleShipItem item,
            NodeVtable vtable
        )
        {
            EncounterShipSetup setup = BattleShipMoves.SetupOf(item);
            EncounterPlayShipItemInteractive chip = BattleShipMoves.Chip(Cards3D(window), setup);
            if (setup == null || chip == null)
            {
                return;
            }

            EncounterShipSetup ship = setup;
            EncounterPlayShipItemInteractive at = chip;
            BattleShipItem row = item;
            Func<string> state = () =>
                ModStrings.Get(
                    BattleShipMoves.Locked(ship)
                        ? ModStrings.BattleShipLockedInFlotilla
                        : ModStrings.BattleShipNotLocked
                );

            vtable.Announcements.Add(GraphNodes.ValuePart(state));
            vtable.StateText = state;
            vtable.OnDoubleClick = () => BattleShipMoves.ToggleLock(ship);
            NodeHints.Add(vtable, ModStrings.HintLockShip, UiActions.DoubleClick);
            vtable.OnPickUp = () => BattleShipMoves.Pick(at, BattleRosters.ShipName(row));
            Destination(window, line, vtable);
        }

        /// <summary>Every flotilla card the arena is drawing for the player's side, as the interactive
        /// kind that holds ships - what a chip is looked up in, and what a drop is aimed at. The 2D
        /// cards are the game's own index into them, so this walks the same container
        /// <see cref="Card"/> does.</summary>
        private static EncounterPlayFlotillaCard3DInteractive[] Cards3D(
            AdvancedEncounterPlayModalWindow window
        )
        {
            List<EncounterPlayFlotillaCard3DInteractive> cards =
                new List<EncounterPlayFlotillaCard3DInteractive>(4);
            IList<AgeTransform> children = Children(Cards(window));
            for (int i = 0; children != null && i < children.Count; i++)
            {
                EncounterPlayFlotillaCard3DInteractive card = Card3D(
                    children[i].GetComponent<EncounterPlayFlotillaCard2D>()
                );
                if (card != null)
                {
                    cards.Add(card);
                }
            }

            return cards.ToArray();
        }

        /// <summary>The card in the arena a 2D card is bound to, where it is the kind that arranges
        /// flotillas. Null for the enemy's side, whose container arranges a fleet.</summary>
        private static EncounterPlayFlotillaCard3DInteractive Card3D(
            EncounterPlayFlotillaCard2D card
        )
        {
            try
            {
                return card == null
                    ? null
                    : card.Card3D as EncounterPlayFlotillaCard3DInteractive;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The card standing for the flotilla a roster line names, by the number the line drew.
        /// Null where the window is drawing no cards, or where nothing answers to that number.</summary>
        private static EncounterPlayFlotillaCard2D Card(
            AdvancedEncounterPlayModalWindow window,
            FlotillaLine line
        )
        {
            try
            {
                int number;
                if (
                    line == null
                    || !int.TryParse(AgeText.Label(line.FlotillaIndexLabel), out number)
                )
                {
                    return null;
                }

                AgeTransform box = Cards(window);
                IList<AgeTransform> children = Children(box);
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    EncounterPlayFlotillaCard2D card =
                        children[i].GetComponent<EncounterPlayFlotillaCard2D>();
                    // The game numbers the flotillas from one where it writes them down and from zero
                    // where it binds them.
                    if (card != null && card.Index == number - 1)
                    {
                        return card;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: looking for a flotilla card threw: " + e);
            }

            return null;
        }

        /// <summary>Where the window keeps the cards: the one container among the arena's that draws
        /// flotillas. The enemy's side has a fleet container instead, which is why this is asked by TYPE
        /// rather than by position.</summary>
        private static AgeTransform Cards(AdvancedEncounterPlayModalWindow window)
        {
            EncounterPlayScreen3D arena = window == null ? null : window.EncounterPlayScreen3D;
            EncounterPlayContainer[] containers =
                arena == null ? null : arena.PlayerEncounterPlayContainers;
            for (int i = 0; containers != null && i < containers.Length; i++)
            {
                EncounterPlayFlotillaCardContainer cards =
                    containers[i] as EncounterPlayFlotillaCardContainer;
                if (cards != null)
                {
                    return cards.FlotillaCard2DContainer;
                }
            }

            return null;
        }

        /// <summary>The unlock sentence a card writes under its number - kept on the 3D card the 2D one
        /// is bound to.</summary>
        private static AgePrimitiveLabel CommandPoints(EncounterPlayFlotillaCard2D card)
        {
            try
            {
                EncounterPlayFlotillaCard3D card3d = card == null ? null : card.Card3D;
                return card3d == null ? null : card3d.CommandPointsLabel;
            }
            catch (Exception)
            {
                return null;
            }
        }


        /// <summary>
        /// The figures: the four pages of stats the window keeps behind its four switches, and the
        /// fighters line the window writes under them.
        ///
        /// Named ("Stats") like the two sides are, because Tab now stops here on its own way round and
        /// a stop the player lands in says what it is. Positions stay ON through the level - the pager
        /// rows are a list, and the place-in-list stamp is what replaced the ticked switch
        /// (<see cref="Pages"/>).
        /// </summary>
        private void Figures(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            builder.SetRegion(FiguresRegion);
            bool named = Context(builder, StatsKey, true);
            try
            {
                Pages(builder, window);
                Note(builder, window.FightersStanceRatioLabel, "advanced-play/fighters");
            }
            finally
            {
                Close(builder, named);
            }

            builder.SetRegion(null);
        }

        /// <summary>
        /// The stats as the LIST of pages they are: one row per page, and standing on a row is what
        /// turns the window's box to that page.
        ///
        /// The window draws four switches and one box, which costs the keyboard five controls to read
        /// one page. Here the pages ARE the list, exactly as the tutorial's are
        /// (<see cref="TutorialScreen"/>) and as the faction window's hulls are
        /// (<c>FactionChoiceScreen.BuildHulls</c>): up and down walk the four pages, the box follows
        /// visibly, and the switches are not declared at all because the list has taken over their
        /// whole job. Where the page number was said by the switch being ticked it is now the engine's
        /// own place-in-list stamp.
        ///
        /// Entering the stop lands on the page the window is ALREADY showing, never on row one: a
        /// landing that ignored which page is up would turn the picture out from under a player who
        /// had only come to read it. A position the player left here still outranks it, which is the
        /// order a remembered place should come in.
        ///
        /// One drawn viewer, N paged contents - evidence is the box every page is painted into, and
        /// identity is the index; per-page widgets exist but three of the four are switched off at any
        /// moment, so a row keyed on its own panel would be a row the gate drops for being the page
        /// the player is not on.
        /// </summary>
        private void Pages(GraphBuilder builder, AdvancedEncounterPlayModalWindow window)
        {
            AgeControlToggle[] toggles = window.StatsToggles;
            AgeTransform[] panels = window.StatsPanels;
            AgeTransform box = Box(panels);
            if (toggles == null || panels == null || box == null)
            {
                return;
            }

            int count = Math.Min(toggles.Length, panels.Length);
            for (int i = 0; i < count; i++)
            {
                ControlId id = ControlId.Structural(StatPageKey + i);
                builder.AddItem(Nodes.Drawn(id, Page(window, i), box));
                if (toggles[i] != null && toggles[i].State)
                {
                    builder.LandStopOn(id);
                }
            }
        }

        /// <summary>The box the window paints whichever page is showing into - the one thing on this
        /// band that is drawn whatever page that is, which is what every row stands or falls with. It
        /// is asked of the panels rather than named, because the window keeps no field for it.
        /// </summary>
        private static AgeTransform Box(AgeTransform[] panels)
        {
            for (int i = 0; panels != null && i < panels.Length; i++)
            {
                AgeTransform parent = panels[i] == null ? null : panels[i].Parent;
                if (parent != null)
                {
                    return parent;
                }
            }

            return null;
        }

        /// <summary>
        /// One page of the stats: the figures the window is drawing on it, and - asked first - the box
        /// being turned to this page in the first place.
        ///
        /// Reading the row's WORDS is where the turn happens rather than a focus hook, because that is
        /// the only thing that runs between the cursor arriving and the landing being spoken; a switch
        /// driven from the hook would read the page the player just left. It is guarded on the row
        /// being the focused one, so a graph dump or a type-ahead pass over the stop turns no pages.
        ///
        /// The page's NAME and each of its figures are announcement parts of their own
        /// (<see cref="Figures"/>). Spoken they read as the one sentence they always did - the
        /// announcer joins the parts with the same separator the composed string used - and in the
        /// review buffer they are what a multi-part row is there: one line per part, steppable
        /// (owner-reported 2026-08-29, where "Military power" ran into the balance sentence).
        ///
        /// No role word: a page is not a control the player works, it is what the window is showing
        /// them.
        /// </summary>
        private NodeVtable Page(AdvancedEncounterPlayModalWindow window, int index)
        {
            AdvancedEncounterPlayModalWindow it = window;
            int page = index;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() =>
                    {
                        Show(it, page);
                        return OptionalText.Phrase(NameKey(Which(it, page)));
                    }),
                },
            };
            Figures(vtable, window, index);
            vtable.Sections = Explanations(vtable, window, index);
            return vtable;
        }

        /// <summary>
        /// What the game says about this page, in its own words: the sentence on each diagram the page
        /// draws, and - on the first page - the sentence the window hangs on the band of switches.
        ///
        /// A page draws up to three diagrams and a control announces the ONE tooltip it points at, so
        /// the first diagram's is the aimed one and the rest are reviewed sections, the same shape the
        /// sorting band's sentence takes (<see cref="Band"/>).
        ///
        /// The page with NO diagram of its own points at the band of switches instead: the sentence
        /// the game hung there is about the list itself ("Choose which set of stats you want to see"),
        /// which is exactly what a row on a page with nothing else to explain it has to say - and
        /// pointing at it is what makes the game DRAW the words the row speaks, where before this the
        /// row said them and aimed nowhere (owner ruling 2026-08-29, off the painted-side audit). A
        /// page that has a diagram AND is the first keeps that sentence as a reviewed section, which
        /// is where it always was.
        ///
        /// A row with neither releases the pointer: there is no control under the cursor to light up,
        /// and nothing to leave a neighbour's tooltip hanging over.
        /// </summary>
        private static IList<NodeSection> Explanations(
            NodeVtable vtable,
            AdvancedEncounterPlayModalWindow window,
            int index
        )
        {
            AgeTransform[] diagrams = Diagrams(window, Panel(window, index));
            AgeTooltip band = AgeWidgets.Raw(window.StatsTogglesGroup);
            AgeTooltip aim = diagrams.Length == 0 ? band : AgeWidgets.Raw(diagrams[0]);
            List<NodeSection> sections = new List<NodeSection>(4);
            IList<NodeSection> aimed = GraphNodes.SectionsFor(vtable, aim);
            for (int i = 0; aimed != null && i < aimed.Count; i++)
            {
                sections.Add(aimed[i]);
            }

            if (aim == null)
            {
                vtable.OnFocusVisual = AgeWidgets.ReleasePointer;
            }

            for (int i = 1; i < diagrams.Length; i++)
            {
                Add(sections, GraphNodes.ReviewedTooltipSection(AgeWidgets.Raw(diagrams[i])));
            }

            if (index == 0 && diagrams.Length != 0)
            {
                Add(sections, GraphNodes.ReviewedTooltipSection(band));
            }

            return sections.Count == 0 ? null : sections;
        }

        private static void Add(List<NodeSection> sections, NodeSection section)
        {
            if (section != null)
            {
                sections.Add(section);
            }
        }

        /// <summary>Turn the box to <paramref name="index"/>'s page the way clicking its switch does,
        /// if it is not there already and if that page's row is the one the cursor is standing on. The
        /// window's own handler is what unticks the old switch, hides its panel and shows the new one,
        /// so the page is turned by the game rather than by the mod arranging panels.</summary>
        private static void Show(AdvancedEncounterPlayModalWindow window, int index)
        {
            try
            {
                AgeControlToggle[] toggles = window.StatsToggles;
                if (toggles == null || index < 0 || index >= toggles.Length)
                {
                    return;
                }

                AgeControlToggle toggle = toggles[index];
                if (toggle == null || toggle.State || FocusedPage() != index)
                {
                    return;
                }

                AgeWidgets.Select(toggle);
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: turning to the stats page under the cursor threw: " + e);
            }
        }

        /// <summary>Which page's row the cursor is on, or -1 for anywhere else.</summary>
        private static int FocusedPage()
        {
            ControlId key = ModEntry.Navigator == null ? null : ModEntry.Navigator.FocusedKey;
            string structural = key == null ? null : key.StructuralKey as string;
            if (structural == null || !structural.StartsWith(StatPageKey, StringComparison.Ordinal))
            {
                return -1;
            }

            int page;
            return int.TryParse(structural.Substring(StatPageKey.Length), out page) ? page : -1;
        }

        /// <summary>The four pages, as the things they say rather than as the order the prefab happens
        /// to lay its switches out in.</summary>
        private enum Stats
        {
            Trajectories,
            Military,
            Damage,
            Ranges,
        }

        /// <summary>Which page a row stands for, asked of what the window keeps in the panel rather
        /// than of where the panel sits in the array (<see cref="Panel"/>). A window that will not say
        /// reads as the page with no gauge of its own, which is the one the arena draws.</summary>
        private static Stats Which(AdvancedEncounterPlayModalWindow window, int index)
        {
            try
            {
                AgeTransform panel = Panel(window, index);
                if (panel != null && panel == Widget(window.BattlePowerGauge))
                {
                    return Stats.Military;
                }

                if (panel != null && panel == Parent(Widget(window.EnergyPowerGauge)))
                {
                    return Stats.Damage;
                }

                if (panel != null && panel == Parent(Widget(window.ShortRangePowerGauge)))
                {
                    return Stats.Ranges;
                }
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: asking which stats page a row is threw: " + e);
            }

            return Stats.Trajectories;
        }

        /// <summary>What a page is CALLED - the mod's own word for it, since the game draws four
        /// wordless switches and names none of them.</summary>
        private static string NameKey(Stats page)
        {
            switch (page)
            {
                case Stats.Military:
                    return StatsMilitaryKey;
                case Stats.Damage:
                    return StatsDamageKey;
                case Stats.Ranges:
                    return StatsRangeKey;
                default:
                    return StatsTrajectoriesKey;
            }
        }

        /// <summary>
        /// What a page says beyond its name: the figures it is drawing, one announcement PART each -
        /// which is what the page IS, since none of them writes a number anywhere.
        ///
        /// A part per figure rather than one composed sentence, because the two surfaces want
        /// different shapes of the same content: the announcer joins the parts into the one sentence
        /// the row always spoke, and the review buffer gives each part a line of its own to step
        /// through. A figure the window is not drawing answers null and contributes to neither.
        ///
        /// The trajectory page's figures are one per CURVE SLOT the arena holds, resolved at read time
        /// (<see cref="Curve"/>): the container pools its curves, so the slots are stable while which
        /// of them is drawn is not.
        ///
        /// What the other three pages' figures mean, none of which is written anywhere on them:
        ///
        /// - MILITARY is which side the arcs say is stronger and by how much, in the two fleets' own
        ///   names - the same sentence the battle popups read, off the game's own helper.
        /// - DAMAGE is the two rings' splits, NET OF DEFENCES: the window sizes each ring from what one
        ///   side's weapons of that type get through the other side's defences of that type
        ///   (<c>RefreshDamageStats</c> :398-413), which is why a ring can be all one side's - nothing
        ///   of the other's is getting through - and why the phrase says both halves rather than only
        ///   the one with something in it. The last figure is the window's own comparison BETWEEN the
        ///   types: whichever ring is drawn fatter is where the greater quantity of damage is, a fact
        ///   the picture states and neither ring's split does.
        /// - RANGE is the three rings' splits and then which range both fleets are most suited to,
        ///   which is what the window says by drawing that ring thickest (<c>RefreshRangeStats</c>
        ///   :375-396 sizes each from the two sides' average efficiency at that range, measured against
        ///   the other two).
        /// </summary>
        private static void Figures(
            NodeVtable vtable,
            AdvancedEncounterPlayModalWindow window,
            int index
        )
        {
            AdvancedEncounterPlayModalWindow it = window;
            switch (Which(window, index))
            {
                case Stats.Military:
                    Figure(vtable, () => BalanceText(it));
                    return;
                case Stats.Damage:
                    Figure(vtable, () => BattleArcs.Shares(it.EnergyPowerGauge, EnergyShareKey));
                    Figure(
                        vtable,
                        () => BattleArcs.Shares(it.PhysicalPowerGauge, ProjectileShareKey)
                    );
                    Figure(
                        vtable,
                        () =>
                            BattleArcs.Thickest(
                                new[] { it.EnergyPowerGauge, it.PhysicalPowerGauge },
                                new[] { EnergyThreatKey, ProjectileThreatKey }
                            )
                    );
                    return;
                case Stats.Ranges:
                    Figure(
                        vtable,
                        () => BattleArcs.Shares(it.ShortRangePowerGauge, ShortRangeShareKey)
                    );
                    Figure(
                        vtable,
                        () => BattleArcs.Shares(it.MediumRangePowerGauge, MediumRangeShareKey)
                    );
                    Figure(
                        vtable,
                        () => BattleArcs.Shares(it.LongRangePowerGauge, LongRangeShareKey)
                    );
                    Figure(
                        vtable,
                        () =>
                            BattleArcs.Thickest(
                                new[]
                                {
                                    it.ShortRangePowerGauge,
                                    it.MediumRangePowerGauge,
                                    it.LongRangePowerGauge,
                                },
                                new[]
                                {
                                    ShortRangeMattersKey,
                                    MediumRangeMattersKey,
                                    LongRangeMattersKey,
                                }
                            )
                    );
                    return;
                default:
                    AgeTransform container = Panel(window, index);
                    IList<AgeTransform> children = Children(container);
                    for (int i = 0; children != null && i < children.Count; i++)
                    {
                        int at = i;
                        Figure(vtable, () => Curve(it, container, at));
                    }

                    return;
            }
        }

        /// <summary>One figure as a part of the row's reading: never watched, because a page the
        /// player is standing on redraws its arcs as the window recomputes them and a figure that
        /// announced itself under a standing cursor would talk over the plan being chosen.</summary>
        private static void Figure(NodeVtable vtable, Func<string> text)
        {
            vtable.Announcements.Add(GraphNodes.ValuePart(text, false));
        }

        /// <summary>
        /// One of the curves the arena draws for this side, as the clause it is: which flotilla the
        /// line belongs to and the range the plan has it fighting at. Null for a slot the container is
        /// not drawing a curve in.
        ///
        /// The container's own visibility is the PAGE's state and not the curve's, so the curves are
        /// asked one step (<see cref="AgeWidgets.DrawnChild"/>) rather than through the container's
        /// gate: standing on this row is what makes the container visible, and by the time these words
        /// are composed it already is. A locked flotilla still gets a curve - the game fades it rather
        /// than dropping it - so a locked one is said, because it is on the player's screen.
        /// </summary>
        private static string Curve(
            AdvancedEncounterPlayModalWindow window,
            AgeTransform container,
            int index
        )
        {
            try
            {
                AgeTransform child = AgeWidgets.DrawnChild(Children(container), index);
                EncounterPlayTrajectoryCurve curve =
                    child == null
                        ? null
                        : child.GetComponentInChildren<EncounterPlayTrajectoryCurve>();
                return curve == null ? null : Engagement(window, curve.TrajectoryIndex);
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: reading a trajectory curve threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Which page a panel is showing, asked of what the window keeps in it rather than of where it
        /// sits in the array. The three gauge pages are the panels the window's own gauge fields are
        /// drawn in - the balance gauge IS its panel, the damage and range gauges sit inside theirs -
        /// and the remaining page is the arena's trajectory container, which the window has no field
        /// for at all (measured 2026-08-29: <c>TrajectoryContainerLeft</c>, <c>PowerBalanceGroup</c>,
        /// <c>DamageGroup</c>, <c>RangeGroup</c>, in that order). Asked this way, a prefab that
        /// reorders the switches reorders the list and nothing else.
        /// </summary>
        private static AgeTransform Panel(AdvancedEncounterPlayModalWindow window, int index)
        {
            AgeTransform[] panels = window.StatsPanels;
            return panels == null || index < 0 || index >= panels.Length ? null : panels[index];
        }

        /// <summary>The diagrams a page draws, in the order it draws them - what its sentences hang
        /// on. Never null, so a caller can count them.</summary>
        private static AgeTransform[] Diagrams(
            AdvancedEncounterPlayModalWindow window,
            AgeTransform panel
        )
        {
            AgeTransform balance = Widget(window.BattlePowerGauge);
            AgeTransform energy = Widget(window.EnergyPowerGauge);
            AgeTransform physical = Widget(window.PhysicalPowerGauge);
            AgeTransform shortRange = Widget(window.ShortRangePowerGauge);
            if (panel != null && panel == balance)
            {
                return new[] { balance };
            }

            if (panel != null && panel == Parent(energy))
            {
                return new[] { energy, physical };
            }

            if (panel != null && panel == Parent(shortRange))
            {
                return new[]
                {
                    shortRange,
                    Widget(window.MediumRangePowerGauge),
                    Widget(window.LongRangePowerGauge),
                };
            }

            // The trajectory page: the arena's curves carry no sentence of their own.
            return Nothing;
        }

        private static readonly AgeTransform[] Nothing = new AgeTransform[0];

        /// <summary>Where one flotilla will fight, under the plan the window has selected: the number
        /// the game writes the flotilla down as (one-based, as every line on this window draws it) and
        /// the range in the game's own words for it - composed exactly as the card's range diagram
        /// composes its own sentence (<c>BattlePlayCardRangeIndicator.Refresh</c> :73-76), because the
        /// bare range name localizes to "Short" and what the game shows a player is "Short Range".
        /// </summary>
        private static string Engagement(AdvancedEncounterPlayModalWindow window, int index)
        {
            try
            {
                EncounterPlayDefinition play = window.SelectedPlayerPlayDefinition;
                EncounterFlotillaDefinition[] flotillas = play == null ? null : play.Flotillas;
                if (flotillas == null || index < 0 || index >= flotillas.Length)
                {
                    return null;
                }

                string range = Convert.ToString(flotillas[index].OptimalRangeName);
                if (string.IsNullOrEmpty(range))
                {
                    return null;
                }

                return OptionalText.Phrase(
                    FlotillaRangeKey,
                    index + 1,
                    Gui.Localize(RangeTitleKey, Gui.GetLocalizedTitle(range))
                );
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: reading a flotilla's range threw: " + e);
                return null;
            }
        }

        private static AgeTransform Parent(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string BalanceText(AdvancedEncounterPlayModalWindow window)
        {
            try
            {
                return BattleNotifications.BalanceText(
                    window.PlayerEncounterGroup,
                    window.EnemyEncounterGroup,
                    true
                );
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
            Checkbox(window.ShowYourFleetsToggle, ShowYourFleetsKey, null, "advanced-play:show-yours");
            Checkbox(
                window.ShowEnemyFleetsToggle,
                ShowEnemyFleetsKey,
                null,
                "advanced-play:show-theirs"
            );
            Sorting(window);
            Checkbox(window.WatchBattleToggle, null, WatchToggleTitleKey, "advanced-play:watch");
            Command(window.StartBattleButton, StartTitleKey, "advanced-play:start");
            Command(window.RetreatButton, RetreatTitleKey, "advanced-play:retreat");
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
        /// game everywhere else.</summary>
        private void Checkbox(
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
                Name(widget, modKey, gameKey, null),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Offered(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, widget);
            Cells.Add(_cells, widget, ControlId.For(toggle, key), vtable);
        }

        /// <summary>A button the window drew as an icon, under the game's own title for it, refusing with
        /// the game's own reason - the retreat button carries the failure infos for a fleet that cannot
        /// run.</summary>
        private void Command(AgeTransform widget, string titleKey, string key)
        {
            if (widget == null)
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Offered(it);
            NodeVtable vtable = GraphNodes.Button(
                Name(it, null, titleKey, tooltip),
                () => AgeWidgets.Press(it),
                enabled,
                tooltip
            );
            GraphNodes.AddRefusal(vtable, tooltip, enabled);
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(_cells, widget, ControlId.For(widget, key), vtable);
        }

        /// <summary>How long is left, for a battle the game is timing. Never watched - a countdown that
        /// announced itself under a standing cursor would talk over the plan the player is choosing - so it
        /// is there to be asked.</summary>
        private void Countdown(AdvancedEncounterPlayModalWindow window, string key)
        {
            AgeTransform gauge = window.TimerGauge;
            NotificationBattleSetup notification = window.NotificationBattleSetup;
            if (
                gauge == null
                || notification == null
                || OptionalText.Phrase(TimeLeftKey, 0) == null
            )
            {
                return;
            }

            NotificationBattleSetup it = notification;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => TimeLeft(it)),
                },
                OnFocusVisual = AgeWidgets.ReleasePointer,
            };
            Cells.Add(_cells, gauge, ControlId.Structural(key), vtable);
        }

        private static string TimeLeft(NotificationBattleSetup notification)
        {
            try
            {
                return OptionalText.Phrase(
                    TimeLeftKey,
                    UnityEngine.Mathf.Clamp(
                        UnityEngine.Mathf.RoundToInt(notification.GetTimeLeftRatio() * 100f),
                        0,
                        100
                    )
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A button the window keeps in no field of its own - the way out, and the reset beside
        /// the sorting: found by the HANDLER it is wired to rather than by a name in the prefab, because the
        /// handler is in the window's own code and a prefab name is a guess.</summary>
        private static AgeTransform ByHandler(
            AdvancedEncounterPlayModalWindow window,
            string method
        )
        {
            try
            {
                AgeControlButton[] buttons =
                    window.AgeTransform.GetComponentsInChildren<AgeControlButton>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    AgeControlButton button = buttons[i];
                    if (
                        button != null
                        && button.OnActivateMethod == method
                        // Candidate choice, not existence: several buttons share a handler and the drawn
                        // one is the live one. The gate can only drop a node, never pick.
                        && AgeWidgets.Visible(button.AgeTransform)
                    )
                    {
                        return button.AgeTransform;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: looking for " + method + " threw: " + e);
            }

            return null;
        }

        /// <summary>Who is leading this side, and the hero commanding it where there is one - the portrait
        /// carries the hero's whole dossier, so the row indicates having one and the buffer holds it.
        /// </summary>
        private static void Leader(GraphBuilder builder, BattleGroupInfoPanel panel, string prefix)
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

        /// <summary>
        /// A line the game wrote and is showing: read as it stands, with whatever it explains itself
        /// with carried along.
        ///
        /// <paramref name="beside"/> is the wordless icon the game drew next to the label
        /// (<see cref="Beside"/>), and the two of them are TWO HOVER SURFACES: the row points at the
        /// one the engine would really draw - its own where it has one, the icon's where it has not -
        /// and whichever it is not pointing at becomes a nested entry of its own, which is what every
        /// second hover surface in the mod does. A row pointing at nothing releases the pointer rather
        /// than leaving a neighbour's tooltip standing over it.
        /// </summary>
        private static void Note(
            GraphBuilder builder,
            AgePrimitiveLabel label,
            string key,
            AgeTransform beside = null
        )
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || string.IsNullOrEmpty(AgeText.Label(label)))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            AgeTooltip own = AgeWidgets.Raw(widget);
            AgeTooltip badge = AgeWidgets.Raw(beside);
            bool drawn = AgeWidgets.Draws(own);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it)),
                },
            };
            AgeTooltip aimed = drawn ? own : badge;
            vtable.Sections = GraphNodes.SectionsFor(vtable, aimed);
            if (aimed == null)
            {
                vtable.OnFocusVisual = AgeWidgets.ReleasePointer;
            }

            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(1);
            if (drawn)
            {
                // Both kinds through one sink, exactly as the nesting sink itself asks: only one of
                // the two tests can pass for a given tooltip, and asking both is what makes the icon
                // an entry whichever kind the prefab hung on it.
                TooltipChildren.Add(dossiers, badge, beside);
                TooltipChildren.AddPlain(dossiers, badge, beside);
            }

            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(ControlId.For(label, key), vtable, label),
                key,
                dossiers
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

        /// <summary>
        /// Which band the rows that follow belong to - announced once as focus enters, so a roster is
        /// audibly yours or theirs without every row saying so. A build with no such phrase opens no
        /// level at all, which is why every caller closes with <see cref="Close"/>.
        ///
        /// <paramref name="positions"/> is off by default because most of these bands are not one
        /// numbered set: a side's leader line, its plans and its flotilla rows share a level, and a
        /// stamp across all of them would count things that are not peers. A band whose rows ARE a set
        /// (<see cref="Tactics"/>, <see cref="Figures"/>) asks for it.
        /// </summary>
        private static bool Context(GraphBuilder builder, string nameKey, bool positions = false)
        {
            string name = OptionalText.Phrase(nameKey);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            builder.PushContext(name, null, positions);
            return true;
        }

        private static void Close(GraphBuilder builder, bool opened)
        {
            if (opened)
            {
                builder.PopContext();
            }
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

        private static AgeTransform Widget(GuiPanel panel)
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

        private static AgeTransform Widget(BattlePowerGauge gauge)
        {
            try
            {
                return gauge == null ? null : gauge.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AdvancedEncounterPlayModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<AdvancedEncounterPlayModalWindow>(false)
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
    }
}

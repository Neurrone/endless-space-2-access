using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// How long a journey takes, and where it stops on the way.
    ///
    /// The game answers both questions with a PICTURE and with nothing else. It walks the route one
    /// movement point at a time and drops a dot per point, and wherever the running budget hits zero it
    /// drops a numbered marker instead and fills the budget back up
    /// (<c>PathRenderer.DisplayMovement</c> :747-848). The number on the last marker is the turn the
    /// fleet arrives on (<c>DisplayPathEnd</c> :863 stamps it one past the last boundary crossed), and
    /// where the markers SIT is where the fleet spends each night - at a system, or half way down a
    /// lane, which the game draws as a dot in empty space. No text anywhere in the game says any of it.
    ///
    /// What is said here is the marker count with ONE correction, and the correction was measured rather
    /// than read off the code: a fleet stopped part way down a lane with nothing left has a turn it
    /// cannot move in, and the renderer draws no marker for that turn because there is nowhere sensible
    /// to put one (<c>DisplayMovement</c> :765 marks the boundary only for a leg not already under way).
    /// The map therefore labels such a fleet's destination one turn early - measured in the fixture, a
    /// fleet drawn "1" at Rigel arrived the turn after next. The number said is the turn the fleet
    /// ARRIVES on, counting the turn now in progress as the first, which is the question being asked.
    ///
    /// So the walk is replayed. The arithmetic is <see cref="RouteTurns"/>, which is engine-free and
    /// unit-tested; everything here is the adapter that prices the legs and names the places, because
    /// two of the three ways of flying a leg are priced FROM the budget standing when the leg is
    /// reached - a wormhole costs whatever is left (<c>PathfindingManager</c> :271-278) and a portal the
    /// smaller of what is left and a cap (:262-270) - which is why the route cannot be priced up front.
    ///
    /// The replay follows <c>PathRenderer.RenderPath</c> :408-545 rather than the path alone: a fleet
    /// already under way is half way along a leg that is not in its path at all
    /// (<c>GoToFleetAction.TryToMoveFleet</c> asserts the path STARTS at the node it is flying towards),
    /// and a node that refills movement on arrival - a time bubble - fills the tank at the end of the
    /// leg that reaches it, which the renderer does per leg and the pathfinder honours too
    /// (<c>PathfindingAStar</c> :688-693).
    ///
    /// One deliberate departure. The renderer resets the budget to the view's record of what it was
    /// when the current leg began, for a leg already under way that is a wormhole or a portal; that
    /// record lives on the view's own fleet object and is not read here, so a fleet caught mid-wormhole
    /// is walked from the budget it has now. It costs one turn of accuracy for the frames of one
    /// animation, and never for a fleet at rest.
    ///
    /// Every entry point is a pathfinding search or a walk of one, so all of them belong to the moment a
    /// key is pressed and none of them to a frame - the same rule <see cref="FleetOrders"/> keeps. The
    /// one-frame memo below is what lets a node's readout and its review buffer ask the same question in
    /// the same frame without paying for it twice.
    /// </summary>
    public static class FleetRoute
    {
        /// <summary>A journey worked out: its turns, and the places its legs run between. Places has one
        /// more entry than there are legs - the last is the destination.</summary>
        public sealed class Route
        {
            public GalaxyPath Path;

            public List<RouteTurn> Turns;

            public GameNode[] Places;

            public bool UsesPortal;

            public bool UsesWormhole;

            /// <summary>The turn the fleet arrives on, counting the turn now in progress as one.
            /// </summary>
            public int ArrivesIn
            {
                get { return Turns == null || Turns.Count == 0 ? 0 : Turns[Turns.Count - 1].Number; }
            }
        }

        // ---- the memo ----
        //
        // A control's readout is composed EVERY FRAME while it is focused, not only when focus lands on
        // it: the review buffer is refilled from the readout's text, so the buffer can notice a control
        // changing under a standing cursor (`GraphNavigator.FillBuffer`). A part that costs a pathfinding
        // search therefore cannot simply be declared un-watched - un-watched means "not compared", not
        // "not asked". So the answer is remembered against the state it was computed from, and recomputed
        // only when that state moves: which fleets are selected, where they are, what they have left, and
        // whose turn it is. Sitting on a destination costs one search; walking a hundred of them costs
        // one each.

        private static object _target;

        private static long _stamp = long.MinValue;

        private static Route _route;

        private static string _refusal;

        /// <summary>How many pathfinding searches this class has run since the mod loaded. Nothing reads
        /// it but the dev probe, and what it is there to prove is that focusing a destination costs ONE
        /// search rather than one per frame - a difference no transcript and no dump would show.
        /// </summary>
        public static int Searches;

        /// <summary>Forget the memo. Called from teardown so a reload starts with nothing remembered.
        /// </summary>
        public static void Reset()
        {
            _target = null;
            _stamp = long.MinValue;
            _route = null;
            _refusal = null;
            _committedFleet = 0;
            _committedStamp = long.MinValue;
            _committed = null;
        }

        /// <summary>
        /// Everything about the game that could change what a route ANSWERS, in one number, read without
        /// allocating: the selection (how many, and which), where each of them is, what each has left,
        /// and the turn. Anything the pathfinder itself depends on that this misses - a border closed by
        /// somebody else, a citadel raised - moves the turn number before it can move a route.
        /// </summary>
        private static long SelectionStamp()
        {
            try
            {
                IGuiSelectedGarrisonsRepositoryService repository =
                    Services.GetService<IGuiSelectedGarrisonsRepositoryService>();
                Game game = Gui.Game;
                if (repository == null)
                {
                    return long.MinValue;
                }

                System.Collections.ObjectModel.ReadOnlyCollection<Garrison> garrisons =
                    repository.Garrisons;
                long stamp = game == null ? 0 : game.Turn * 1000003L;
                for (int i = 0; i < garrisons.Count; i++)
                {
                    Fleet fleet = garrisons[i] as Fleet;
                    if (fleet == null)
                    {
                        continue;
                    }

                    stamp = (stamp * 31L) + (long)fleet.GUID.ToUInt64(null);
                    stamp = (stamp * 31L) + (long)(fleet.CurrentMovementPoints * 1000f);
                    stamp = (stamp * 31L) + fleet.Position.NextValidNodePosition.NodeIndex;
                }

                return stamp;
            }
            catch (Exception)
            {
                return long.MinValue;
            }
        }

        /// <summary>
        /// What the selected fleets would do about a place on the map, as the map's own preview does it:
        /// the first selected fleet that produces a route is the one previewed
        /// (<c>GalaxyGarrisonCursor.ChooseFleetAndPrevisualizePath</c> :226-281), and where none of them
        /// can get there the reasons are what there is to say instead.
        ///
        /// <paramref name="target"/> is a <c>GameNode</c> or a <c>Link</c> - the two things the game
        /// accepts as a move target, and the two things the tree offers as destinations.
        /// </summary>
        private static void Ask(object target)
        {
            long stamp = SelectionStamp();
            if (stamp == _stamp && ReferenceEquals(target, _target))
            {
                return;
            }

            _stamp = stamp;
            _target = target;
            _route = null;
            _refusal = null;
            Searches++;

            try
            {
                List<Fleet> fleets = FleetOrders.Selected();
                if (fleets.Count == 0)
                {
                    return;
                }

                List<FailureInfo> refusals = new List<FailureInfo>();
                GameNode node = target as GameNode;
                Link link = target as Link;
                for (int i = 0; i < fleets.Count; i++)
                {
                    Fleet fleet = fleets[i];
                    GalaxyPath path =
                        node != null
                            ? FleetOrders.PathTo(fleet, node, refusals)
                            : FleetOrders.PathToLink(fleet, link, refusals);
                    if (path == null)
                    {
                        continue;
                    }

                    // Asked for its reasons and not for its answer, exactly as the game's own preview
                    // does: a route it will not fly today is still the route, and it is still drawn.
                    FleetOrders.CanSend(fleet, path, refusals);
                    _route = Walk(fleet, path);
                    return;
                }

                _refusal = Refusal(refusals);
            }
            catch (Exception e)
            {
                Log.Warn("fleets: previewing a route threw: " + e);
            }
        }

        /// <summary>The one line a destination gains while fleets are selected: how long the journey
        /// takes and what it costs, or why there is no journey. Null while nothing is selected, which is
        /// most of the time.</summary>
        public static string Preview(object target)
        {
            Ask(target);
            if (_route == null)
            {
                return _refusal;
            }

            MessageBuilder message = new MessageBuilder();
            message.ListItem(Cost(_route));
            AddShortcuts(message, _route);
            return message.Build();
        }

        /// <summary>The journey turn by turn, for the review buffer under a destination. One line per
        /// turn that ends somewhere with a name; a turn that ends half way down a lane is a dot on the
        /// map with nothing to call it, and is not a line.
        ///
        /// Headed, which the same lines under a FLEET are not: here the turns belong to a journey nobody
        /// has ordered yet, sitting among lines about what the place IS, and without a word saying so
        /// they read as something the place itself is doing. Under a fleet the line above has already
        /// said where that fleet is going, so the header would only repeat it - which is why the header
        /// is added here and not in <see cref="Lines"/>.</summary>
        public static IList<string> PreviewLines(object target)
        {
            Ask(target);
            IList<string> lines = Lines(_route);
            if (lines == null)
            {
                return null;
            }

            List<string> headed = new List<string>(lines.Count + 1);
            headed.Add(ModStrings.Get(ModStrings.FleetRoutePreviewHeader));
            for (int i = 0; i < lines.Count; i++)
            {
                headed.Add(lines[i]);
            }

            return headed;
        }

        /// <summary>What a fleet already under way is doing, for a surface that does NOT name where it is
        /// going. Null for a fleet with no route.</summary>
        public static string Committed(Fleet fleet)
        {
            Route route = Current(fleet);
            if (route == null)
            {
                return null;
            }

            GameNode end = route.Places[route.Places.Length - 1];
            string name = Named(end);
            MessageBuilder message = new MessageBuilder();
            message.ListItem(
                name == null
                    ? ModStrings.Plural(
                        ModStrings.FleetRouteUnexploredThisTurn,
                        ModStrings.FleetRouteUnexploredTurns,
                        route.ArrivesIn
                    )
                    : ModStrings.Format(
                        ModStrings.PluralKey(
                            ModStrings.FleetRouteToThisTurn,
                            ModStrings.FleetRouteToTurns,
                            route.ArrivesIn
                        ),
                        name,
                        route.ArrivesIn
                    )
            );
            AddShortcuts(message, route);
            return message.Build();
        }

        /// <summary>The same for a surface that has ALREADY named the destination - the map's own fleet
        /// line says "Moving to Xiu" before this is reached, and hearing the name twice in one readout
        /// is worse than hearing it once.</summary>
        public static string Arrival(Fleet fleet)
        {
            Route route = Current(fleet);
            if (route == null)
            {
                return null;
            }

            MessageBuilder message = new MessageBuilder();
            message.ListItem(
                ModStrings.Plural(
                    ModStrings.FleetRouteArrivesThisTurn,
                    ModStrings.FleetRouteArrivesTurns,
                    route.ArrivesIn
                )
            );
            AddShortcuts(message, route);
            return message.Build();
        }

        /// <summary>
        /// The turn a route gets to one of the places it runs through, counting the turn now in
        /// progress as one. Nought where the route never goes there, and nought for no route at all -
        /// which is also the answer for a foreign fleet whose path the game will not draw, because
        /// <see cref="Current"/> is the gate (<see cref="RouteShown"/>).
        ///
        /// Not <see cref="Route.ArrivesIn"/>, which is the turn the whole journey ends on. A row
        /// hanging under the system a fleet is flying towards is asking about THIS system: the fleet
        /// may pass straight through it on the way to somewhere three turns further on, and the turn
        /// it gets HERE is the one the player standing here wants. The arithmetic is
        /// <see cref="RouteTurns.ReachedOn"/>, which is engine-free and unit-tested.
        /// </summary>
        public static int ReachesIn(Route route, GameNode place)
        {
            if (route == null || route.Places == null || place == null)
            {
                return 0;
            }

            for (int i = 0; i < route.Places.Length; i++)
            {
                if (ReferenceEquals(route.Places[i], place))
                {
                    return RouteTurns.ReachedOn(route.Turns, i);
                }
            }

            return 0;
        }

        /// <summary>The remaining journey turn by turn, for the review buffer under a fleet.</summary>
        public static IList<string> CommittedLines(Fleet fleet)
        {
            return Lines(Current(fleet));
        }

        /// <summary>
        /// Whether the map would DRAW this fleet's committed route if the player selected it.
        ///
        /// Selecting a fleet is something the game lets the player do whoever owns it
        /// (<c>GalaxyFleetCursorTarget.ValidateSelection</c> :17-24 makes no owner test), but drawing
        /// the path that fleet is flying is a separate permission and a much narrower one: the cursor
        /// asks for the path and the renderer refuses it outright for somebody else's fleet unless the
        /// empire has the SeesEnemyPathfinding property AND is at war with its owner - or the owner is
        /// the pirates and the empire may attack their fleets (<c>GalaxyGarrisonCursor.RenderPath</c>
        /// :525). Nothing else in the game writes a fleet's destination down for a foreign fleet
        /// either (<c>PanelFeatureGarrisonInfoAutomatedFleet</c> :77-85 is owner-gated).
        ///
        /// So the turn-by-turn itinerary this class produces is the words for a picture, and where the
        /// picture is refused the words are too. What is left is what a sighted player still has: the
        /// fleet drawn between two stars, and the leg it is flying now, which is geometry on the screen
        /// rather than a route read out of the model.
        ///
        /// Every turn count the map's fleet rows say rides this gate, because they all reach a route
        /// through <see cref="Current"/>: a foreign fleet whose path the game will not draw gets the
        /// countless phrase and nothing about where it is going (owner ruling 2026-09-02).
        /// </summary>
        private static bool RouteShown(Fleet fleet)
        {
            try
            {
                Empire empire = Gui.PlayerEmpire as Empire;
                if (empire == null || ReferenceEquals(fleet.Empire, empire))
                {
                    return true;
                }

                if (!empire.SeesEnemyPathfinding)
                {
                    return false;
                }

                return DepartmentOfForeignAffairs.HasDiplomaticAbility(
                        fleet.Empire,
                        empire,
                        DiplomaticAbilityDefinition.Names.War
                    )
                    || (
                        fleet.Empire is PirateEmpire
                        && DepartmentOfForeignAffairs.HasDiplomaticAbility(
                            fleet.Empire,
                            empire,
                            DiplomaticAbilityDefinition.Names.AttackOwnedFleets
                        )
                    );
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Where a fleet with a route already given is going, worked out from where it is NOW -
        /// so the answer shrinks as the journey is flown, which is what a player asking half way through
        /// wants to know. Not memoised: it is asked of one fleet at a time and costs no search, only a
        /// walk of a path the game is already holding.</summary>
        public static Route Current(Fleet fleet)
        {
            if (fleet == null || !RouteShown(fleet))
            {
                return null;
            }

            // Remembered for the same reason a preview is (see "the memo" above): a fleet line's readout
            // is composed every frame it is focused, and walking a route is not a per-frame thing. The
            // stamp is what the walk depends on - which fleet, which path, what it has left, how far
            // along its current leg it is (in tenths, so an animation refreshes a few times rather than
            // sixty), and the turn.
            long stamp;
            try
            {
                Game game = Gui.Game;
                GalaxyPath path = fleet.Path;
                stamp = game == null ? 0 : game.Turn * 1000003L;
                stamp = (stamp * 31L) + (path == null ? 0 : path.PathPositions.Length);
                stamp = (stamp * 31L) + (long)(fleet.CurrentMovementPoints * 1000f);
                stamp = (stamp * 31L) + fleet.Position.NextValidNodePosition.NodeIndex;
                stamp =
                    (stamp * 31L)
                    + (
                        fleet.Position.IsInMovement
                            ? (long)(fleet.Position.Movement.Progress * 10f)
                            : -1L
                    );
            }
            catch (Exception)
            {
                stamp = long.MinValue;
            }

            ulong id = fleet.GUID.ToUInt64(null);
            if (id == _committedFleet && stamp == _committedStamp)
            {
                return _committed;
            }

            _committedFleet = id;
            _committedStamp = stamp;
            _committed = Of(fleet, fleet.Path);
            return _committed;
        }

        private static ulong _committedFleet;

        private static long _committedStamp = long.MinValue;

        private static Route _committed;

        /// <summary>The journey a fleet would fly along a route somebody has already found - the walk
        /// itself, for a caller holding both halves.</summary>
        public static Route Of(Fleet fleet, GalaxyPath path)
        {
            try
            {
                return fleet == null
                    || path == null
                    || path.PathPositions == null
                    || path.PathPositions.Length == 0
                    ? null
                    : Walk(fleet, path);
            }
            catch (Exception e)
            {
                Log.Warn("fleets: reading a fleet's journey threw: " + e);
                return null;
            }
        }

        /// <summary>The first thing the game has to say about why nothing can go there, in the game's own
        /// sentence (<c>Gui.FormatFailureInfo</c>). The bookkeeping flags a panel uses to grey a button
        /// are skipped, exactly as the game's own formatter skips them.</summary>
        public static string Refusal(List<FailureInfo> refusals)
        {
            for (int i = 0; refusals != null && i < refusals.Count; i++)
            {
                FailureInfo refusal = refusals[i];
                if (refusal == null || refusal.IsIgnorable)
                {
                    continue;
                }

                string text = AgeText.Clean(Gui.FormatFailureInfo(string.Empty, refusal));
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return null;
        }

        // ---- the replay ----

        /// <summary>The legs of a route as the renderer flies them, priced one at a time against the
        /// budget the walk has reached (<see cref="IRouteLegs"/>).</summary>
        private sealed class Legs : IRouteLegs
        {
            public NodePosition[] Positions;

            public GameNode[] Places;

            /// <summary>The settings each leg is priced under. The first leg can be one the fleet is
            /// already flying, which the game priced under its own settings and not the path's.
            /// </summary>
            public PathfindingRequestSettings[] Settings;

            public PathfindingData Data;

            public IPathfindingService Pathfinding;

            public int Count
            {
                get { return Positions.Length - 1; }
            }

            public RouteLeg Leg(int index, float currentPoints)
            {
                Data.CurrentMovementPoints = currentPoints;
                PathfindingTransitionCost cost = Pathfinding.GetTransitionCost(
                    Data,
                    Positions[index],
                    Positions[index + 1],
                    Settings[index]
                );
                GameNode arriving = Places[index + 1];
                return new RouteLeg
                {
                    Cost = cost.Cost,
                    Method =
                        (cost.Method & PathfindingTransitionMethod.Portal)
                        == PathfindingTransitionMethod.Portal
                            ? RouteMethod.Portal
                            : (
                                (cost.Method & PathfindingTransitionMethod.Wormhole)
                                == PathfindingTransitionMethod.Wormhole
                                    ? RouteMethod.Wormhole
                                    : RouteMethod.Ordinary
                            ),
                    RefillOnArrival =
                        cost.RefillMovementOnArrival
                        || (arriving != null && arriving.RefillMovementPoints),
                };
            }
        }

        private static Route Walk(Fleet fleet, GalaxyPath path)
        {
            IPathfindingService pathfinding = Services.GetService<IPathfindingService>();
            IPositioningService positioning = Services.GetService<IPositioningService>();
            if (pathfinding == null || positioning == null || path.PathPositions == null)
            {
                return null;
            }

            // The leg the fleet is half way along, which is never in the path: the path starts at the
            // node it is flying towards (`GoToFleetAction.TryToMoveFleet` asserts exactly that), and the
            // renderer draws the rest of that leg before anything else (`RenderPath` :468-492).
            Movement movement = fleet.Position.IsInMovement ? fleet.Position.Movement : null;
            bool flying =
                movement != null
                && movement.IsValid
                && !(
                    path.PathPositions.Length > 1
                    && path.PathPositions[0] == movement.Start
                    && path.PathPositions[1] == movement.Goal
                );

            int extra = flying ? 1 : 0;
            NodePosition[] positions = new NodePosition[path.PathPositions.Length + extra];
            PathfindingRequestSettings[] settings = new PathfindingRequestSettings[
                positions.Length - 1 < 0 ? 0 : positions.Length - 1
            ];
            if (flying)
            {
                positions[0] = movement.Start;
            }

            for (int i = 0; i < path.PathPositions.Length; i++)
            {
                positions[i + extra] = path.PathPositions[i];
            }

            for (int i = 0; i < settings.Length; i++)
            {
                settings[i] = flying && i == 0 ? movement.Settings : path.Settings;
            }

            GameNode[] places = new GameNode[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                places[i] = positioning.GetGameNode(positions[i]);
            }

            if (positions.Length < 2)
            {
                return null;
            }

            // A copy, never the fleet's own: GeneratePathfindingData hands back the SAME instance every
            // time, and the walk spends its movement points down to nothing.
            PathfindingData data = new PathfindingData();
            data.Copy(fleet.GeneratePathfindingData());
            float maximum = data.MaximumMovementPoints;
            float current = data.CurrentMovementPoints;
            // The place the journey starts from can itself be a time bubble, which the renderer fills
            // the tank at before it draws anything (`RenderPath` :442-467).
            if (places[0] != null && places[0].RefillMovementPoints)
            {
                current = maximum;
            }

            Legs legs = new Legs
            {
                Positions = positions,
                Places = places,
                Settings = settings,
                Data = data,
                Pathfinding = pathfinding,
            };
            Route route = new Route
            {
                Path = path,
                Places = places,
                Turns = RouteTurns.Walk(
                    legs,
                    current,
                    maximum,
                    flying ? movement.Progress : 0f,
                    -1f
                ),
            };
            for (int i = 0; i < route.Turns.Count; i++)
            {
                route.UsesPortal |= route.Turns[i].UsesPortal;
                route.UsesWormhole |= route.Turns[i].UsesWormhole;
            }

            return route;
        }

        // ---- what a route says ----

        private static string Cost(Route route)
        {
            string movement = Amount(route.Path.PathCost);

            // The two forms number their slots differently - the arriving one has no turn count in
            // it at all - so the count picks the key and the key picks the arguments.
            string key = ModStrings.PluralKey(
                ModStrings.FleetRoutePreviewThisTurn,
                ModStrings.FleetRoutePreviewTurns,
                route.ArrivesIn
            );
            return key == ModStrings.FleetRoutePreviewThisTurn
                ? ModStrings.Format(key, movement)
                : ModStrings.Format(key, route.ArrivesIn, movement);
        }

        private static void AddShortcuts(MessageBuilder message, Route route)
        {
            if (route.UsesPortal)
            {
                message.ListItem(ModStrings.Get(ModStrings.FleetRouteUsesPortal));
            }

            if (route.UsesWormhole)
            {
                message.ListItem(ModStrings.Get(ModStrings.FleetRouteUsesWormhole));
            }
        }

        /// <summary>
        /// The journey as a line per turn: the places the fleet REACHES on that turn, in the order it
        /// reaches them.
        ///
        /// Reaching, not stopping. A fleet with movement to spare flies straight through a system and
        /// spends the night past it, so a turn can pass three places and end at none of them - and the
        /// question an itinerary answers is "when does my fleet get to Hir", which the night's resting
        /// spot alone cannot say. Owner ruling; the earlier rule was where the turn ENDED, and on a
        /// measured route that produced exactly one line, the arrival.
        ///
        /// A turn that reaches nowhere at all - one spent entirely between two systems - is no line,
        /// because the map draws a numbered dot in empty space and calls it nothing, and inventing
        /// "somewhere between Hir and Xiu" would be describing a picture rather than reading one. What
        /// such a turn nonetheless DID - a portal, a wormhole - is still said, on a line of its own.
        ///
        /// The turn a line is filed under is the number the GAME is showing for it, not a count from
        /// now: an itinerary is read against the turn counter in the corner and against everything else
        /// the player has planned for turn 12, and "turn 3" meaning "three turns from whenever you last
        /// looked" is the one reading that cannot be checked against anything. Durations elsewhere in
        /// this class stay relative - "arrives in 3 turns" is the answer to a different question.
        /// </summary>
        private static IList<string> Lines(Route route)
        {
            if (route == null || route.Turns == null || route.Turns.Count == 0)
            {
                return null;
            }

            int today = DisplayedTurn();
            int destination = route.Places.Length - 1;
            List<string> lines = new List<string>(route.Turns.Count);
            for (int i = 0; i < route.Turns.Count; i++)
            {
                RouteTurn turn = route.Turns[i];
                MessageBuilder message = new MessageBuilder();
                MessageBuilder places = new MessageBuilder();
                bool arrives = false;
                int reached = 0;
                for (int j = 0; turn.Reached != null && j < turn.Reached.Count; j++)
                {
                    int at = turn.Reached[j];
                    if (at < 0 || at >= route.Places.Length)
                    {
                        continue;
                    }

                    reached++;
                    arrives |= at == destination;
                    string name = Named(route.Places[at]);
                    places.ListItem(
                        name ?? ModStrings.Get(ModStrings.FleetUnexploredSystem)
                    );
                }

                if (reached > 0)
                {
                    message.ListItem(
                        ModStrings.Format(
                            arrives
                                ? ModStrings.FleetItineraryArrival
                                : ModStrings.FleetItineraryTurn,
                            today > 0 ? today + turn.Number - 1 : turn.Number,
                            places.Build()
                        )
                    );
                }

                if (turn.UsesPortal)
                {
                    message.ListItem(ModStrings.Get(ModStrings.FleetRouteUsesPortal));
                }

                if (turn.UsesWormhole)
                {
                    message.ListItem(ModStrings.Get(ModStrings.FleetRouteUsesWormhole));
                }

                string line = message.Build();
                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }

            return lines.Count == 0 ? null : lines;
        }

        /// <summary>The turn number the game is showing in the corner. The simulation counts the turn now
        /// in progress as <c>Game.Turn</c> and the interface writes that plus one, which is the number
        /// every other turn the player reads is expressed in (the End Turn window does the same sum:
        /// <c>GlobalHud.Turn</c>). Zero where there is no game, which leaves the itinerary counting from
        /// now rather than saying a wrong absolute turn.</summary>
        internal static int DisplayedTurn()
        {
            try
            {
                Game game = Gui.Game;
                return game == null ? 0 : game.Turn + 1;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>What the map calls a place, or null where it calls it nothing: a route can run
        /// through space nobody has been into, and naming a system off the simulation would hand the
        /// player something the map is deliberately not showing them.</summary>
        public static string Named(GameNode node)
        {
            return MapVisibility.Perceived(node, Gui.PlayerEmpire) ? node.LocalizedName : null;
        }

        private static string Amount(float value)
        {
            try
            {
                return Gui.FormatAmount(value, true, Gui.Rounding.Floor, false, 1);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

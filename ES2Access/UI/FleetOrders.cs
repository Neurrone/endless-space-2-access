using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Amplitude.Unity.Framework;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Where a fleet is, where it is going, and the orders that change that - posted exactly the way
    /// the map's own cursor posts them when a player drags a fleet across the galaxy.
    ///
    /// Moving a fleet in this game is not a method call on the fleet. It is an ORDER: the game asks its
    /// pathfinder for a route, wraps the route in the move action's own context, asks the action
    /// whether it may be carried out, and posts the whole thing to the player controller, which is the
    /// only thing that may change the simulation. Every step of that is the game's own
    /// (<c>GalaxyGarrisonCursor.TryToMoveFleet</c>), and it is written out here rather than
    /// approximated, because an order assembled any other way is a move the game did not agree to.
    ///
    /// A destination is a place on the map, and the map has two kinds of them: a NODE - a star system -
    /// and a LINK - the starlane between two of them, which a fleet can be sent part way along. The
    /// game accepts both (<c>GetGalaxyPathToTargets</c> resolves whichever the mouse is over), and both
    /// are answered here, because the route to a lane is not the route to either of its ends: it is the
    /// route to the near end plus one more step out onto the lane itself.
    ///
    /// A route the game will not fly has a REASON, and the game keeps its reasons in the same currency
    /// everywhere: a list of <c>FailureInfo</c> handed down through the search and the action check, to
    /// be turned into a sentence by <c>Gui.FormatFailureInfo</c>. Every search and check here takes that
    /// list, optionally - passing null asks the cheap question ("is there a route") and passing a list
    /// asks the expensive one ("and if not, why not"), which is exactly the split the game's own cursor
    /// makes between a click and a hover preview. The expensive question re-runs the pathfinder with
    /// progressively relaxed rules to find out which rule was the one that bit; it belongs to the moment
    /// a key is pressed and never to a frame.
    ///
    /// Nothing is remembered between calls: the definitions come out of the game's own database and the
    /// routes are asked for fresh, so this is reload-safe by construction and stale by nothing. Every
    /// entry point here is a search or an order, so all of them belong to the moment a key is pressed
    /// and none of them to a frame.
    /// </summary>
    public static class FleetOrders
    {
        /// <summary>The node a docked fleet is sitting at, or null while it is under way.</summary>
        public static GameNode Orbit(Fleet fleet)
        {
            try
            {
                return fleet != null && fleet.Position.IsInOrbit ? fleet.Position.GetOrbit() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where a fleet under way is headed - the far end of the route it is flying, which is
        /// the same thing the game's own fleet list writes in its status column.</summary>
        public static GameNode Heading(Fleet fleet)
        {
            try
            {
                GalaxyPath path = fleet == null ? null : fleet.Path;
                if (path == null)
                {
                    return null;
                }

                IPositioningService positioning = Services.GetService<IPositioningService>();
                return positioning == null ? null : positioning.GetGameNode(path.Destination);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The player's own fleets that an order given now would move.
        ///
        /// Selecting a fleet does not put the game into a mode: it changes what the map's cursor is
        /// holding, and naming a destination then moves everything the cursor holds
        /// (<c>GalaxyGarrisonCursor.GetFleetsToMove</c> reads exactly this repository). So "which
        /// fleets would go" is a question with a live answer, asked of the game rather than tracked
        /// here - and a garrison that is not a fleet of the player's own is not one of them, whatever
        /// the player has clicked on.
        /// </summary>
        public static List<Fleet> Selected()
        {
            List<Fleet> found = new List<Fleet>();
            try
            {
                IGuiSelectedGarrisonsRepositoryService repository =
                    Services.GetService<IGuiSelectedGarrisonsRepositoryService>();
                Empire empire = Gui.PlayerEmpire;
                if (repository == null || empire == null)
                {
                    return found;
                }

                ReadOnlyCollection<Garrison> garrisons = repository.Garrisons;
                for (int i = 0; i < garrisons.Count; i++)
                {
                    Fleet fleet = garrisons[i] as Fleet;
                    if (fleet != null && !fleet.IsDestroyed && fleet.Empire == empire)
                    {
                        found.Add(fleet);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("fleets: reading the selected fleets threw: " + e);
            }

            return found;
        }

        /// <summary>The route this fleet would fly to a place on the map, or null where there is none.
        /// Asking is a pathfinding search, so it belongs to the moment a menu is opened and never to a
        /// frame. Hand it a list and the reasons a missing route is missing are written into it.
        /// </summary>
        public static GalaxyPath PathTo(
            Fleet fleet,
            GameNode node,
            List<FailureInfo> failureInfos = null
        )
        {
            try
            {
                return fleet == null || node == null
                    ? null
                    : PathToPosition(fleet, node.NodePosition, failureInfos);
            }
            catch (Exception e)
            {
                Log.Warn("fleets: working out a route to a system threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The route this fleet would fly to sit ON a starlane, written the way the game writes it
        /// (<c>GalaxyGarrisonCursor.GetGalaxyPathToLink</c>).
        ///
        /// A lane is not a place the pathfinder knows about, so the route is built in two halves: the
        /// ordinary route to whichever end the fleet would come in by, and then one transition out
        /// along the lane towards the other end. Which end is the near one is the fleet's own position
        /// where it is already touching the lane, and otherwise the explored end - the game will send a
        /// fleet down a lane into the dark, and it starts from the end it can see.
        ///
        /// A fleet already flying along this very lane is answered with the route to where it is going
        /// next, which is the game's way of saying "you are already doing that".
        /// </summary>
        public static GalaxyPath PathToLink(
            Fleet fleet,
            Link link,
            List<FailureInfo> failureInfos = null
        )
        {
            try
            {
                if (fleet == null || link == null || fleet.Empire == null)
                {
                    return null;
                }

                if (link.Exploration[fleet.Empire] == EntityExploration.State.Unrevealed)
                {
                    return null;
                }

                NodePosition end1 = link.ExtremityNode1.NodePosition;
                NodePosition end2 = link.ExtremityNode2.NodePosition;
                NodePosition next = fleet.Position.NextValidNodePosition;
                if (
                    fleet.Position.IsInMovement
                    && (
                        (fleet.Position.Movement.Start == end1 && fleet.Position.Movement.Goal == end2)
                        || (fleet.Position.Movement.Start == end2 && fleet.Position.Movement.Goal == end1)
                    )
                )
                {
                    return PathToPosition(fleet, next, failureInfos);
                }

                NodePosition near = NodePosition.Invalid;
                NodePosition far = NodePosition.Invalid;
                bool touching =
                    fleet.Position.IsInOrbit && (end1 == fleet.NodePosition || end2 == fleet.NodePosition);
                touching |= !fleet.Position.IsInOrbit && (end1 == next || end2 == next);
                if (touching)
                {
                    bool fromFirst = end1 == next;
                    near = fromFirst ? end1 : end2;
                    far = fromFirst ? end2 : end1;
                }
                else if ((int)link.Exploration.GetState(Gui.PlayerEmpire) <= 3)
                {
                    bool secondIsDark =
                        link.ExtremityNode2.Exploration[fleet.Empire]
                        == EntityExploration.State.Unrevealed;
                    near = secondIsDark ? end1 : end2;
                    far = secondIsDark ? end2 : end1;
                }

                if (near == NodePosition.Invalid || far == NodePosition.Invalid)
                {
                    return null;
                }

                GalaxyPath path = PathToPosition(fleet, near, failureInfos);
                IPathfindingService pathfinding = Services.GetService<IPathfindingService>();
                if (path == null || pathfinding == null)
                {
                    return null;
                }

                PathfindingRequestSettings settings = new PathfindingRequestSettings(fleet);
                float cost = pathfinding
                    .GetTransitionCost(fleet.GeneratePathfindingData(), near, far, settings)
                    .Cost;
                if (!float.IsInfinity(cost) && path.AddMovement(far, pathfinding))
                {
                    return path;
                }

                // The step OUT onto the lane is priced on its own, so its refusal is diagnosed on its
                // own too, exactly as the cursor does it (`GetGalaxyPathToLink` :413-433): a step that
                // only costs anything once closed borders are ignored is diplomacy, and everything else
                // left at infinity is a step no ordinary drive can make. Asked only when somebody wants
                // the reasons - the relaxed re-runs are the price of the answer.
                if (failureInfos == null)
                {
                    return null;
                }

                if (
                    (settings.Flags & PathfindingFlags.IgnoreCloseBorder)
                    != PathfindingFlags.IgnoreCloseBorder
                )
                {
                    cost = pathfinding
                        .GetTransitionCost(
                            fleet.GeneratePathfindingData(),
                            near,
                            far,
                            new PathfindingRequestSettings(PathfindingFlags.IgnoreCloseBorder, 0L)
                        )
                        .Cost;
                    if (!float.IsInfinity(cost))
                    {
                        FailureInfo.Add(
                            fleet.IsInvisible
                                ? FailureFlags.DiplomacyPreventsPassingBecauseRevealed
                                : FailureFlags.DiplomacyPreventsPassing,
                            failureInfos
                        );
                        return null;
                    }
                }

                FailureInfo.Add(
                    FailureFlags.NeedFreeMovement,
                    Movement.FreeMovementDescriptorName,
                    failureInfos
                );
                return null;
            }
            catch (Exception e)
            {
                Log.Warn("fleets: working out a route onto a starlane threw: " + e);
                return null;
            }
        }

        /// <summary>Whether the game would carry out this move if it were ordered - the move action's
        /// own answer, which is what decides whether the destination is offered at all. Hand it a list
        /// and a refusal writes down what refused it.</summary>
        public static bool CanSend(
            Fleet fleet,
            GalaxyPath path,
            List<FailureInfo> failureInfos = null
        )
        {
            return Context(fleet, path, failureInfos) != null;
        }

        /// <summary>Send the fleet along a route the pathfinder has already found. The order is posted
        /// and answered later - what the fleet is doing is what the fleet says it is doing a frame or
        /// two afterwards, never what this call assumed.</summary>
        public static void Send(Fleet fleet, GalaxyPath path)
        {
            try
            {
                EntityActionContext context = Context(fleet, path, null);
                if (context != null)
                {
                    PostOrder(new OrderEntityAction(fleet.Empire.Index, GoTo(), fleet, context));
                }
            }
            catch (Exception e)
            {
                Log.Warn("fleets: ordering a fleet to move threw: " + e);
            }
        }

        // There is no CancelMove here, and that is deliberate. The map has no gesture for calling a
        // move off - a mouse turns a fleet round by dragging it somewhere else, and stops it at the
        // next system by dragging it onto the piece of lane it is already flying, which the game
        // answers with the route to that system (`GetGalaxyPathToLink`'s own-movement branch, ported in
        // PathToLink). Cancelling through OrderCancelEntityAction is something else again: it strands
        // the fleet mid-lane with no path at all, which no click in this game produces.

        /// <summary>
        /// The ordinary route from where the fleet will next be to a position on the map. A system the
        /// game has frozen - one being fought over, one mid-cutscene - is no destination at all, which
        /// is the cursor's own first question before it asks the pathfinder anything.
        ///
        /// With a list to write into, this is also the game's own DIAGNOSTIC LADDER
        /// (<c>GalaxyGarrisonCursor.GetGalaxyPathToPosition</c> :456-506): a route that was not found is
        /// searched for again with one rule relaxed at a time, and whichever relaxation finds it names
        /// the rule that refused. A route that WAS found is still priced against a citadel-free galaxy,
        /// because a detour is worth saying even though the fleet can go.
        /// </summary>
        private static GalaxyPath PathToPosition(
            Fleet fleet,
            NodePosition goal,
            List<FailureInfo> failureInfos
        )
        {
            IPathfindingService pathfinding = Services.GetService<IPathfindingService>();
            IPositioningService positioning = Services.GetService<IPositioningService>();
            if (pathfinding == null || positioning == null)
            {
                return null;
            }

            NodePosition start = fleet.Position.NextValidNodePosition;
            GameNode from = positioning.GetGameNode(start);
            GameNode to = positioning.GetGameNode(goal);
            if (from == null || to == null)
            {
                return null;
            }

            if (from.IsLocked || to.IsLocked)
            {
                FailureInfo.Add(FailureFlags.SystemIsBeingFrozen, failureInfos);
                return null;
            }

            PathfindingRequestSettings settings = new PathfindingRequestSettings(fleet);
            GalaxyPath path = pathfinding.FindPath(fleet, start, goal, settings);
            if (failureInfos == null)
            {
                return path;
            }

            if (path != null)
            {
                float withoutCitadels = pathfinding.FindPathCost(
                    fleet.GeneratePathfindingData(),
                    start,
                    goal,
                    new PathfindingRequestSettings(
                        settings.Flags | PathfindingFlags.IgnoreCitadels,
                        0L
                    )
                );
                if (withoutCitadels < path.PathCost)
                {
                    FailureInfo.Add(FailureFlags.AtLeastOneCitadelCausesOtherPath, failureInfos);
                }

                return path;
            }

            if (
                goal != start
                && fleet.Position.NodePosition == NodePosition.Invalid
                && (int)from.Exploration[fleet.Empire] < 3
            )
            {
                FailureInfo.Add(FailureFlags.NextNodeUnknown, failureInfos);
                return null;
            }

            if (
                pathfinding.FindPath(
                    fleet,
                    start,
                    goal,
                    new PathfindingRequestSettings(
                        settings.Flags | PathfindingFlags.IgnoreCitadels,
                        0L
                    )
                ) != null
            )
            {
                FailureInfo.Add(FailureFlags.AtLeastOneCitadelPreventsFreeMove, failureInfos);
                return null;
            }

            if (
                (settings.Flags & PathfindingFlags.IgnoreCloseBorder)
                    != PathfindingFlags.IgnoreCloseBorder
                && pathfinding.FindPath(
                    fleet,
                    start,
                    goal,
                    new PathfindingRequestSettings(
                        settings.Flags | PathfindingFlags.IgnoreCloseBorder,
                        0L
                    )
                ) != null
            )
            {
                FailureInfo.Add(
                    fleet.IsInvisible
                        ? FailureFlags.DiplomacyPreventsPassingBecauseRevealed
                        : FailureFlags.DiplomacyPreventsPassing,
                    failureInfos
                );
                return null;
            }

            if (
                pathfinding.FindPath(
                    fleet,
                    start,
                    goal,
                    new PathfindingRequestSettings(settings.Flags | PathfindingFlags.IgnoreAll, 0L)
                ) != null
            )
            {
                FailureInfo.Add(
                    FailureFlags.CannotSeeValidPath,
                    Movement.FreeMovementDescriptorName,
                    failureInfos
                );
                return null;
            }

            FailureInfo.Add(
                FailureFlags.NeedFreeMovement,
                Movement.FreeMovementDescriptorName,
                failureInfos
            );
            return null;
        }

        /// <summary>The move action's context for a route, or null where the game would refuse it -
        /// which is the same question asked whether a destination is being offered or ordered, so it is
        /// asked in one place. The refusals the ACTION raises are the ones the pathfinder cannot know
        /// about - a fleet out of movement points, a battle already being planned on its orbit, a route
        /// the simulation has since invalidated - so the list is handed to the check the way the game's
        /// own cursor hands it (<c>GalaxyGarrisonCursor.ChooseFleetAndPrevisualizePath</c> :245), and
        /// dropping it here was dropping half the reasons.</summary>
        private static EntityActionContext Context(
            Fleet fleet,
            GalaxyPath path,
            List<FailureInfo> failureInfos
        )
        {
            try
            {
                EntityActionDefinition definition = GoTo();
                if (fleet == null || path == null || definition == null)
                {
                    return null;
                }

                EntityActionContext context = definition.BuildEntityActionContext(fleet, path);
                return context != null && definition.CanBeExecuted(fleet, context, failureInfos)
                    ? context
                    : null;
            }
            catch (Exception e)
            {
                Log.Warn("fleets: asking whether a fleet could be moved threw: " + e);
                return null;
            }
        }

        private static void PostOrder(Order order)
        {
            PlayerController controller = Gui.GetActivePlayerController();
            if (controller != null)
            {
                controller.PostOrder(order);
            }
        }

        private static EntityActionDefinition GoTo()
        {
            return EntityActionDefinition.GetEntityActionDefinition(
                GoToFleetActionDefinition.ActionDefinitionReference
            );
        }
    }
}

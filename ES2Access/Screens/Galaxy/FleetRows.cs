using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Bookmarks;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Bookmarks;
using ES2Access.UI.Input;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>The fleets under a place - in orbit, under way on a lane, crossing open space, adrift -
    /// and the hangars and garrisons drawn beside them.</summary>
    public sealed partial class GalaxyHudScreen
    {
        // ---- fleets ----


        /// <summary>
        /// The fleets PARKED at a system - what each is made of, where it is, and where it is going -
        /// as children of that system.
        ///
        /// Which fleets those are is never worked out here: they are the ones the map's own lozenge at
        /// this system is holding (<see cref="FleetPresence"/>), so a fleet nobody can see is absent for
        /// the same reason it is absent from the picture, and the count the place announces and the
        /// children it opens onto are the same answer read two ways. The fleets under way on this
        /// system's lanes are the other half of that count and hang here too, after these
        /// (<see cref="AddEnRoute"/>).
        ///
        /// Focus points at whichever label the map is drawing the fleet with, so the game draws the
        /// fleet's own dossier for it exactly as it would for a mouse resting there. Which label that
        /// is is the map's answer, not this screen's: a fleet in orbit is drawn by the DOCK label of
        /// the slot it is sitting in and a fleet under way by its own, and the windows that own them
        /// bind exactly one of the two at a time. A dock label covers the whole slot, so where two
        /// fleets are parked together its tooltip is the pair of them - which is what the game shows a
        /// player hovering there, and reading it as anything narrower would be describing a tooltip
        /// nobody can see.
        ///
        /// Enter SELECTS the fleet, and there is nothing else on it. Calling off a move is not here
        /// either: the game has no cancel gesture on the map, and a fleet is turned round by being sent
        /// somewhere else or stopped at the next system by being sent down the lane it is already on -
        /// both of them backslash on a PLACE, which is where the map itself puts a move. Dropping it is
        /// a deliberate deviation from what this screen used to offer, and it is approved as one.
        /// </summary>
        private static void AddFleets(GraphBuilder builder, string place, IList<Fleet> fleets)
        {
            if (fleets.Count == 0)
            {
                return;
            }

            try
            {
                // Fetched once for the whole place, like the system labels: both windows pool their
                // labels rather than rebuilding them, so one walk of each serves every fleet here.
                DockLabel[] docks = DockLabels();
                FleetLabel[] flying = FleetLabels();

                for (int i = 0; i < fleets.Count; i++)
                {
                    Fleet it = fleets[i];
                    List<TooltipChildren.Dossier> badges;
                    NodeVtable vtable = FleetNode(it, docks, flying, out badges);
                    AddFleet(builder, it, place + "/fleet/" + it.GUID, vtable, badges);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets at a place threw: " + e);
            }
        }

        /// <summary>
        /// The fleets UNDER WAY on the lanes leaving a system, as children of that system - each saying
        /// which lane it is on and which way that lane leaves.
        ///
        /// A lane runs between two systems and the fleet flying it is at neither, so the map draws it
        /// out in between and the tree hangs it under the end it is ARRIVING at - the same end a free
        /// mover hangs under, the only one the picture says anything about, and the ONLY host it gets
        /// (<see cref="Bound"/>). Under the lane node itself is what this used to be, and a lane is a
        /// leaf now: travelling one is what right means there.
        ///
        /// Keyed under the SYSTEM, exactly as a parked fleet is: a fleet is parked or under way and
        /// never both, so this key set cannot collide with the parked one. The row is anchored on the
        /// fleet itself (<see cref="PlacedRows.Anchor"/>), which is sound precisely because there is
        /// one row: a subject on two nodes is one control to the cursor.
        /// </summary>
        private static void AddEnRoute(GraphBuilder builder, string place, List<EnRoute> flying)
        {
            if (flying.Count == 0)
            {
                return;
            }

            try
            {
                DockLabel[] docks = DockLabels();
                FleetLabel[] labels = FleetLabels();

                for (int i = 0; i < flying.Count; i++)
                {
                    EnRoute leg = flying[i];
                    Fleet it = leg.Fleet;
                    List<TooltipChildren.Dossier> badges;
                    NodeVtable vtable = FleetNode(it, docks, labels, out badges);
                    // Straight after the name, because it answers the question the player is holding
                    // while they hear it: why is this fleet under THIS system? One whole phrase rather
                    // than a lane number glued to a compass word, and the number is the one the lane
                    // node itself announces - both come off the same list (<see cref="LanesOf"/>).
                    string template = leg.Wormhole
                        ? ModStrings.GalaxyFleetOnWormhole
                        : ModStrings.GalaxyFleetOnStarlane;
                    int number = leg.Number;
                    string direction = leg.Direction;
                    vtable.Announcements.Insert(
                        1,
                        GraphNodes.ValuePart(
                            () =>
                                ModStrings.Format(template, number, ModStrings.Get(direction)),
                            false
                        )
                    );
                    // The anchor goes on the row that is the fleet's ONLY row: a fleet in transit hangs
                    // under the end it is arriving at and nowhere else, so that row holds it and the
                    // cursor rides it in as the key changes to the destination's.
                    AddFleet(builder, it, place + "/fleet/" + it.GUID, vtable, badges);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets under way near a system threw: " + e);
            }
        }

        /// <summary>
        /// The fleets crossing the OPEN SPACE towards a system - the ones flying a leg between two
        /// nodes with no lane between them - as children of the system they are flying TO.
        ///
        /// A free-moving fleet is the one thing the map draws that the rest of this tree had no place
        /// for: it is in no docking slot and on no lane, so neither the parked list nor the lane list
        /// holds it, and it used to be reachable only by the inspect cursor and the scanner - findable
        /// by neither Tab nor the search. The rule for that is the same one every other homeless thing
        /// on this map is given: where the model gives it no place, it gets a row of its own.
        ///
        /// Under the DESTINATION only, and this is where a free mover parts company with a fleet on a
        /// lane. A lane is drawn map geometry: both of its ends are on the screen, either one is a way
        /// of finding a fleet flying it, and a sighted player reaches it from both. A crossing of open
        /// space is not drawn at all. What the picture shows is a fleet standing between the stars and,
        /// when it is selected, the path AHEAD of it - dots and numbered turn markers running to where
        /// it is going. Nothing anywhere draws or writes where a fleet set out FROM, not even for the
        /// player's own fleets (<see cref="UI.FleetRoute"/>; the game's own path starts at the node
        /// being flown towards). So a row under the source system would be the mod telling the player
        /// something the game does not tell anybody, and it was taken out on 2026-08-16.
        /// </summary>
        private static void AddFreeMoving(
            GraphBuilder builder,
            string place,
            StarSystemNode node,
            List<Fleet> arriving
        )
        {
            if (arriving.Count == 0)
            {
                return;
            }

            try
            {
                DockLabel[] docks = DockLabels();
                FleetLabel[] labels = FleetLabels();
                for (int i = 0; i < arriving.Count; i++)
                {
                    Fleet it = arriving[i];
                    List<TooltipChildren.Dossier> badges;
                    NodeVtable vtable = FleetNode(it, docks, labels, out badges);
                    // Straight after the name, in the slot the lane phrase takes on a fleet under way,
                    // and for the same reason: it answers the question the player is holding while they
                    // hear it - why is this fleet under THIS system?
                    vtable.Announcements.Insert(
                        1,
                        GraphNodes.ValuePart(
                            () =>
                                ModStrings.Format(
                                    ModStrings.GalaxyFleetFreeMovingTo,
                                    node.LocalizedName
                                ),
                            false
                        )
                    );
                    AddFleet(builder, it, place + "/fleet/" + it.GUID, vtable, badges);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets crossing open space near a system threw: " + e);
            }
        }

        /// <summary>
        /// A fleet crossing open space towards somewhere the map has not named, as a row of its own at
        /// the top of the tree.
        ///
        /// It has no system to hang under - its destination is not a place this tree declares - and the
        /// rule for a homeless thing on this map is the one every other one is given: a row of its own,
        /// walked into the list by its own position (<see cref="BuildSystems"/>). What it says about
        /// where it is going is the one thing that can be said, which is that the player cannot see
        /// where that is.
        /// </summary>
        private static void AddAdrift(GraphBuilder builder, Fleet it)
        {
            try
            {
                List<TooltipChildren.Dossier> badges;
                NodeVtable vtable = FleetNode(it, DockLabels(), FleetLabels(), out badges);
                // Which kind of journey it is, since the unnamed destination is all either can say
                // about where it is going: a lane running into the dark is not the same picture as a
                // fleet striking out across open space, and the map draws the one and not the other.
                string phrase = Crossing(it)
                    ? ModStrings.GalaxyFleetFreeMovingToUnexplored
                    : ModStrings.GalaxyFleetOnLaneToUnexplored;
                vtable.Announcements.Insert(
                    1,
                    GraphNodes.ValuePart(() => ModStrings.Get(phrase), false)
                );
                AddFleet(builder, it, AdriftKey(it), vtable, badges);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a fleet crossing open space threw: " + e);
            }
        }

        private static ControlId AdriftId(Fleet fleet)
        {
            return PlacedRows.Anchor(fleet, AdriftKey(fleet));
        }

        /// <summary>The key that id is built from, which the declaration needs as well: a fleet with
        /// badges on its lozenge is a group, and its children are keyed under it.</summary>
        private static string AdriftKey(Fleet fleet)
        {
            return "galaxy:fleet/" + fleet.GUID;
        }

        /// <summary>Whether this fleet's current leg is a crossing of OPEN SPACE rather than a flight
        /// down a lane - the same question <see cref="FreeMovingAt"/> asks, asked again for the one row
        /// that has no system to have asked it under.</summary>
        private static bool Crossing(Fleet fleet)
        {
            try
            {
                GameNode goal;
                return CrossingOpenSpace(
                    Amplitude.Unity.Framework.Services.GetService<IPositioningService>(),
                    fleet,
                    out goal
                );
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// The fleets the map draws crossing open space towards this system.
        ///
        /// The fleets are the ones the map is DRAWING (<see cref="FleetPresence.Drawing"/>) - the fleet
        /// label window's own repository and its own vision gate - so a fleet nobody can see is absent
        /// here for the same reason it is absent from the picture, and no vision rule is re-derived.
        ///
        /// What makes a leg free movement is that its two ends have no <c>Link</c> between them: the
        /// game stores a leg as a pair of node positions rather than as a lane
        /// (<c>FleetPosition.SetMovement</c>), and a fleet flying with starlanes ignored gets a pair
        /// that no line joins. That test is also what keeps this list and <see cref="EnRouteOn"/> from
        /// both claiming the same fleet, which under one system would be a duplicate control id and
        /// would throw the whole page out of Build: a leg either has a lane or it does not.
        /// </summary>
        private static List<Fleet> FreeMovingAt(StarSystemNode node)
        {
            List<Fleet> arriving = new List<Fleet>();
            try
            {
                IPositioningService positioning =
                    Amplitude.Unity.Framework.Services.GetService<IPositioningService>();
                IList<Fleet> drawn = FleetPresence.Drawing();
                for (int i = 0; i < drawn.Count; i++)
                {
                    GameNode goal;
                    if (CrossingOpenSpace(positioning, drawn[i], out goal)
                        && ReferenceEquals(goal, node))
                    {
                        arriving.Add(drawn[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: working out what is crossing open space near a system threw: " + e);
            }

            return arriving;
        }

        /// <summary>
        /// The fleets UNDER WAY with no branch to hang under: the ones whose destination is not one of
        /// the systems this stop is declaring, because the map has never named it.
        ///
        /// Both kinds of journey, since both hang under their destination: a crossing of open space,
        /// and - since the destination-only rule was extended to them - a fleet on a starlane running
        /// into the dark. Asked of the whole drawn fleet list once per build rather than per system,
        /// and answered against the very list the rows are made from, so a fleet under way is either
        /// its destination's child or it is here, never both and never neither.
        /// </summary>
        private static void FreeMovingAdrift(List<StarSystemNode> declared, List<Fleet> adrift)
        {
            adrift.Clear();
            try
            {
                IPositioningService positioning =
                    Amplitude.Unity.Framework.Services.GetService<IPositioningService>();
                IList<Fleet> drawn = FleetPresence.Drawing();
                for (int i = 0; i < drawn.Count; i++)
                {
                    GameNode goal = GoalOf(positioning, drawn[i]);
                    if (goal != null && !Declares(declared, goal))
                    {
                        adrift.Add(drawn[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: working out what is crossing open space threw: " + e);
            }
        }

        private static bool Declares(List<StarSystemNode> declared, GameNode node)
        {
            for (int i = 0; i < declared.Count; i++)
            {
                if (ReferenceEquals(declared[i], node))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the leg a fleet is flying is a crossing of open space - it is under way and
        /// the two ends of its leg have no line between them - and if so, where it is flying to.
        /// </summary>
        private static bool CrossingOpenSpace(
            IPositioningService positioning,
            Fleet fleet,
            out GameNode goal
        )
        {
            goal = null;
            FleetPosition position = fleet.Position;
            if (positioning == null || position.IsInOrbit || !position.IsInMovement)
            {
                return false;
            }

            GameNode start = positioning.GetGameNode(position.Movement.Start);
            goal = positioning.GetGameNode(position.Movement.Goal);
            return start != null && goal != null && !Linked(start, goal);
        }

        /// <summary>Whether a line of any kind joins these two nodes - the model's own question, asked of
        /// every link the node holds rather than of the lanes the map draws, because a leg with an
        /// undrawn lane under it is still not free movement.</summary>
        private static bool Linked(GameNode node, GameNode far)
        {
            for (int i = 0; i < node.Links.Count; i++)
            {
                Link link = node.Links[i];
                if (
                    ReferenceEquals(link.ExtremityNode1, far)
                    || ReferenceEquals(link.ExtremityNode2, far)
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>One fleet as a control, wherever it is being hung - parked, on a lane, crossing open
        /// space, or adrift at the top level. Shared so that a fleet reads the same way in all four
        /// places: everything but the lane it is on is the same fleet, and that includes whether it is a
        /// control at all.
        ///
        /// A fleet the map will not let the mouse SELECT (<see cref="FleetPresence.Selectable"/> - an
        /// automated delivery fleet) is declared as TEXT rather than as a button: no role word, no
        /// action. Not an unavailable button, which would be the mod saying the game has switched
        /// something off; there is no control here at all, exactly as there is none under the pointer.
        /// Owner ruling 2026-08-16.
        ///
        /// <paramref name="badges"/> answers what the map drew ON the lozenge beside the fleet - see
        /// <see cref="FleetBadges"/> - for the caller to declare with <see cref="AddFleet"/>.</summary>
        private static NodeVtable FleetNode(
            Fleet it,
            DockLabel[] docks,
            FleetLabel[] flying,
            out List<TooltipChildren.Dossier> badges
        )
        {
            AgeTransform lozenge = FleetLozenge(it, docks, flying);
            badges = FleetBadges(lozenge);
            NodeVtable vtable = GraphNodes.Button(
                () => it.LocalizedName,
                () => Select(it),
                null,
                Raw(lozenge),
                // The rest of the journey, turn by turn, for whoever wants to know where this
                // fleet will be sleeping tonight (<see cref="FleetRoute"/>).
                () => FleetRoute.CommittedLines(it)
            );
            if (!FleetPresence.Selectable(it))
            {
                vtable.ControlType = ControlTypes.Text;
                vtable.OnActivate = null;
            }

            // Where it is on the map. Asked live rather than captured, because a fleet moves - and a
            // fleet in orbit reads the exact pair its system reads, which is the map saying the same
            // thing twice on purpose (<see cref="GalaxyCoordinates"/>).
            vtable.Announcements.Add(GalaxyCoordinates.Part(() => it.GalaxyPosition));
            // Whose it is, who is commanding it and what it is made of - the one phrase every
            // surface that names a single fleet says (<see cref="FleetPhrase"/>), so this row and a
            // scanner result and a line in the turn log describe the same fleet the same way. Its
            // ship walk is memoised on the fleet's own membership, because an announcement part is
            // asked on every frame whether anything is watching it or not.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetPhrase.Describe(it)));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetText(it)));
            // How much of the journey is left. A part of its OWN and not part of the line
            // above, because that line is WATCHED - a movement figure the game changes under
            // the player is worth saying - and the answer here is a walk of the fleet's whole
            // route, which is a thing to do when focus lands and never on a frame.
            //
            // The destination is named only where the line above has not already named it: a
            // fleet under way says "Moving to Xiu" and then how long that will take, and a
            // fleet parked with an order standing says where it is and then where it is going.
            vtable.Announcements.Add(
                GraphNodes.ValuePart(
                    () =>
                        FleetOrders.Orbit(it) == null
                            ? FleetRoute.Arrival(it)
                            : FleetRoute.Committed(it),
                    false
                )
            );
            if (lozenge != null)
            {
                PointAt(vtable, lozenge);
            }

            // This row is the fleet's ONE subject-bearing node: the caller keys it on the fleet's own
            // GUID under the place it is at or heading for, and anchors it on the fleet
            // (<see cref="PlacedRows.Anchor"/>) so the cursor rides the fleet when a departure or an
            // arrival re-files it under another system. One subject per render is the invariant that
            // makes that safe - two nodes sharing a backing object are ONE control to the cursor
            // (reference identity is followed before the structural key), and the selected-fleet
            // panel's line, keyed on the garrison that IS this fleet, is therefore declared
            // structurally (<c>FleetPanel</c>). It teleported the player back out to the map on the
            // next rebuild both times that invariant was broken.
            return vtable;
        }

        /// <summary>
        /// The badges the map draws on a fleet's lozenge: one of these ships can explore, one can found
        /// a colony, one carries a destruction module (<c>GarrisonsLabelButton.RefreshRoleIcons</c>
        /// :156-191 shows each where the garrison holds such a ship). Each is a wordless picture with a
        /// sentence of the game's own saying what it means, and the lozenge itself carries the fleet's
        /// dossier - so the fleet's node could point at only one of the four and carried none of the
        /// three.
        ///
        /// Nodes rather than lines in the fleet's buffer: three sentences merged into one paragraph is
        /// what a player cannot tell apart or step through (owner ruling: one row means a row of NODES).
        /// </summary>
        private static List<TooltipChildren.Dossier> FleetBadges(AgeTransform lozenge)
        {
            try
            {
                GarrisonsLabelButton label =
                    lozenge == null ? null : lozenge.GetComponent<GarrisonsLabelButton>();
                if (label == null)
                {
                    return null;
                }

                FleetBadgeList.Clear();
                TooltipChildren.AddPlain(FleetBadgeList, label.ExplorationShipIcon);
                TooltipChildren.AddPlain(FleetBadgeList, label.ColonyShipIcon);
                TooltipChildren.AddPlain(FleetBadgeList, label.DestructionModuleIcon);
                return FleetBadgeList;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a fleet lozenge's badges threw: " + e);
                return null;
            }
        }

        // Reused rather than allocated per fleet: this map rebuilds every frame and a galaxy holds
        // many fleets. Safe as one buffer because the caller declares it before the next fleet is
        // read, and the emit captures each dossier by value.
        private static readonly List<TooltipChildren.Dossier> FleetBadgeList =
            new List<TooltipChildren.Dossier>(3);

        /// <summary>A fleet's node as the tree declares it - a GROUP where the map drew badges on its
        /// lozenge, so each badge's sentence is a stop of its own under the fleet, and the plain leaf
        /// it has always been otherwise. The fleet itself comes in because its row is ANCHORED on it
        /// (<see cref="PlacedRows.Anchor"/>): a fleet's key names the system it is at or heading for,
        /// so the key changes the moment it departs, and the cursor follows the fleet rather than the
        /// berth it left.</summary>
        private static void AddFleet(
            GraphBuilder builder,
            Fleet fleet,
            string key,
            NodeVtable vtable,
            List<TooltipChildren.Dossier> badges
        )
        {
            ControlId id = PlacedRows.Anchor(fleet, key);
            if (badges == null || badges.Count == 0)
            {
                // Synthetic: the row stands for a thing in the galaxy model; the enumeration above is the honesty about it.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            // Synthetic for the same reason as the leaf above.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                TooltipChildren.Emit(builder, key, badges, builder.Region);
            }

            builder.EndGroup();
        }

        /// <summary>One fleet under way on one of a system's lanes, with the lane already named the way
        /// the lane node names itself.</summary>
        private struct EnRoute
        {
            public Fleet Fleet;
            public int Number;
            public string Direction;
            public bool Wormhole;
        }

        /// <summary>
        /// The fleets under way on a system's lanes, in lane order, each carrying its lane's number.
        ///
        /// A fleet is taken by the FIRST lane that claims it. Two links can run between the same pair of
        /// systems - a wormhole beside a starlane - and a fleet's leg is a pair of positions rather than
        /// a link (<see cref="FleetPresence"/>), so both would claim it; declaring it twice under one
        /// system is a duplicate control id, which throws the whole screen out of Build.
        /// </summary>
        private static List<EnRoute> EnRouteOn(StarSystemNode node, List<Lane> lanes)
        {
            List<EnRoute> flying = new List<EnRoute>();
            IPositioningService positioning =
                Amplitude.Unity.Framework.Services.GetService<IPositioningService>();
            for (int i = 0; i < lanes.Count; i++)
            {
                IList<Fleet> onLane = FleetPresence.FleetsOn(lanes[i].Link);
                for (int j = 0; j < onLane.Count; j++)
                {
                    Fleet fleet = onLane[j];
                    if (Holds(flying, fleet) || !Bound(positioning, fleet, node))
                    {
                        continue;
                    }

                    flying.Add(
                        new EnRoute
                        {
                            Fleet = fleet,
                            Number = i + 1,
                            Direction = CompassDirections.KeyForBearing(lanes[i].Bearing),
                            Wormhole = lanes[i].Wormhole,
                        }
                    );
                }
            }

            return flying;
        }

        /// <summary>
        /// Whether a fleet flying <paramref name="link"/> belongs under <paramref name="node"/> - which
        /// is to say whether that is the end it is heading FOR.
        ///
        /// A lane's two ends are both on the screen, and the tree used to hang a fleet under each, on
        /// the reasoning that either is a true answer to "where is it". That reasoning was overturned
        /// for a fleet crossing open space on 2026-08-16 - the picture never shows where anything set
        /// out FROM - and it was only ever true of open space by accident: a lane fleet's source is not
        /// drawn either. What the map shows of a fleet under way is where it is and, once it is
        /// selected, the path AHEAD. So a fleet in transit hangs under the end it is arriving at, the
        /// same end a free mover hangs under, and it appears once for everyone.
        ///
        /// So the destination is the SOLE host, with no second case to fall back on: "a fleet stopped
        /// mid-lane with no destination", which this used to hang under both ends, is unreachable by
        /// the game's own rules. A fleet is only in this list because <c>FleetPresence.Between</c>
        /// put it there, and that requires <c>IsInMovement</c> plus a leg whose start and goal are the
        /// lane's two extremities. <c>IsInMovement</c> is <c>Movement.IsValid</c> - start and goal are
        /// valid <c>NodePosition</c>s and nothing more (<c>Movement.cs</c>): there is no "actively
        /// moving" sense to it, an out-of-movement-points fleet keeps its valid leg, and cancelling an
        /// order leaves start and goal intact (<see cref="UI.FleetPresence"/>). <c>GetGameNode</c> is a
        /// plain array index answering a node for any valid position (<c>Galaxy.cs</c>). So
        /// <see cref="GoalOf"/> resolves to one of the two extremities for every fleet this walk can
        /// see, and the fleet appears under that one.
        ///
        /// What is left is degenerate failure - no positioning service, or a throw - and there the
        /// fleet is simply UNLISTED rather than hosted somewhere chosen by a rule: a row built on a
        /// read that failed is worth less than no row, and there is nothing to tiebreak with.
        /// </summary>
        private static bool Bound(IPositioningService positioning, Fleet fleet, StarSystemNode node)
        {
            try
            {
                return ReferenceEquals(GoalOf(positioning, fleet), node);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Where a fleet under way is heading, or null while it is parked - the ONE thing the map
        /// exposes about a fleet's journey, and the very thing this tree files a fleet in transit
        /// under (<see cref="Bound"/>, <see cref="FreeMovingAt"/>).
        ///
        /// Shared with the inspect cursor's travel key, which must not read any more of a route than
        /// this: the current leg's goal is drawn - the fleet is animated along it, and its own row
        /// says it - while the rest of a foreign fleet's plan is the simulation's and not the
        /// player's. One accessor, so that "what the map exposes" cannot drift between the two
        /// readers.
        /// </summary>
        internal static GameNode DestinationOf(Fleet fleet)
        {
            try
            {
                return GoalOf(
                    Amplitude.Unity.Framework.Services.GetService<IPositioningService>(),
                    fleet
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The node a fleet's current leg is flying to, or null while it is not under way.
        /// </summary>
        private static GameNode GoalOf(IPositioningService positioning, Fleet fleet)
        {
            FleetPosition position = fleet.Position;
            return positioning == null || position.IsInOrbit || !position.IsInMovement
                ? null
                : positioning.GetGameNode(position.Movement.Goal);
        }

        private static bool Holds(List<EnRoute> flying, Fleet fleet)
        {
            for (int i = 0; i < flying.Count; i++)
            {
                if (ReferenceEquals(flying[i].Fleet, fleet))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>How many fleets the map is drawing out in the space around this system, as a whole
        /// phrase - what is on its lanes and what is crossing open space to or from it, which together
        /// are exactly the fleets its branch opens onto. Both halves, because a count that named one of
        /// them would stop matching the children the player walks. Nothing at all where there are none -
        /// a system with nothing moving near it says nothing about it, exactly as one with nothing
        /// parked says nothing.</summary>
        private static string UnderWayNearby(StarSystemNode node, Empire empire)
        {
            try
            {
                int count =
                    EnRouteOn(node, LanesOf(node, empire)).Count + FreeMovingAt(node).Count;
                return count == 0
                    ? null
                    : ModStrings.Plural(
                        ModStrings.GalaxyFleetUnderWayNearbyOne,
                        ModStrings.GalaxyFleetsUnderWayNearby,
                        count
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a fleet is doing and how far it can still go this turn.
        ///
        /// What it is MADE of is said before this, by the fleet phrase every surface names a fleet
        /// with (<see cref="FleetPhrase.Composition"/>) - a list of what the ships are, which is what
        /// the bare total used to stand in for.
        /// </summary>
        private static string FleetText(Fleet fleet)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                message.ListItem(FleetState(fleet));
                if (fleet.IsGuarding)
                {
                    message.ListItem(ModStrings.Get(ModStrings.GalaxyFleetGuarding));
                }

                message.ListItem(
                    ModStrings.Format(
                        ModStrings.GalaxyFleetMovement,
                        Amount(fleet.CurrentMovementPoints, false, 0)
                    )
                );
                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Where the fleet is: at the system it is orbiting, or on its way to the one it is headed for.
        ///
        /// That pair is the game's own question, asked the game's own way - its fleet list draws one of
        /// two icons on exactly this test and writes the name of the same node beside it. Whether the
        /// fleet is MOVING is a different question and the wrong one: a fleet that has spent its
        /// movement half way to somewhere has stopped for the turn and is still on its way, and asking
        /// about movement leaves it describing itself as nowhere at all.
        ///
        /// A node the map has not named is not named here either, exactly as a starlane running into
        /// the dark is not.
        ///
        /// Shared with the scanner, whose fleet results carry the same two words
        /// (<c>GalaxyScanner.Fleets</c>) so a fleet found by the scanner and the same fleet met on the
        /// map say the same thing about where it is.
        /// </summary>
        internal static string FleetState(Fleet fleet)
        {
            Empire empire = PlayerEmpire();
            GameNode orbit = FleetOrders.Orbit(fleet);
            if (orbit != null)
            {
                return empire != null && Perceived(orbit, empire)
                    ? ModStrings.Format(ModStrings.GalaxyFleetDockedAt, orbit.LocalizedName)
                    : ModStrings.Get(ModStrings.GalaxyFleetDocked);
            }

            GameNode heading = FleetOrders.Heading(fleet);
            if (heading == null)
            {
                return ModStrings.Get(ModStrings.GalaxyFleetMoving);
            }

            return empire != null && Perceived(heading, empire)
                ? ModStrings.Format(ModStrings.GalaxyFleetMovingTo, heading.LocalizedName)
                : ModStrings.Get(ModStrings.GalaxyFleetMovingUnexplored);
        }

        /// <summary>
        /// The lozenge the map is drawing this fleet on - the dock label's where the fleet is parked,
        /// its own where it is under way, and null where the map is drawing neither.
        ///
        /// The lozenge rather than the label, because the tooltip that gets DRAWN is the one on
        /// whatever the pointer is over, and both labels carry two: a CenterTooltip of their own and
        /// the lozenge's, filled from the same fleet data. Declaring the label's while the engine draws
        /// the lozenge's leaves the node with a review buffer that never fills,
        /// because the buffer only reads a drawn tooltip it can recognise as the one it declared.
        /// </summary>
        private static AgeTransform FleetLozenge(
            Fleet fleet,
            DockLabel[] docks,
            FleetLabel[] flying
        )
        {
            try
            {
                for (int i = 0; i < docks.Length; i++)
                {
                    DockLabel dock = docks[i];
                    if (dock.DockingSlot == null || !Visible(dock.AgeTransform))
                    {
                        continue;
                    }

                    ReadOnlyCollection<GalaxyFleet> docked = dock.DockingSlot.GalaxyFleets;
                    for (int j = 0; j < docked.Count; j++)
                    {
                        if (docked[j] != null && docked[j].Fleet.GUID == fleet.GUID)
                        {
                            return Lozenge(dock.FleetLozenge);
                        }
                    }
                }

                for (int i = 0; i < flying.Length; i++)
                {
                    FleetLabel label = flying[i];
                    if (
                        label.GalaxyFleet != null
                        && label.GalaxyFleet.Fleet.GUID == fleet.GUID
                        && Visible(label.AgeTransform)
                    )
                    {
                        return Lozenge(label.FleetLozenge);
                    }
                }

                return MergedLozenge(fleet);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a fleet to its map label threw: " + e);
            }

            return null;
        }

        private static readonly DockLabel[] NoDockLabels = new DockLabel[0];

        private static readonly FleetLabel[] NoFleetLabels = new FleetLabel[0];

        private static DockLabel[] DockLabels()
        {
            try
            {
                DockLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<DockLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoDockLabels
                    : window.GetComponentsInChildren<DockLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the dock labels threw: " + e);
                return NoDockLabels;
            }
        }

        private static FleetLabel[] FleetLabels()
        {
            try
            {
                FleetLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<FleetLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoFleetLabels
                    : window.GetComponentsInChildren<FleetLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the fleet labels threw: " + e);
                return NoFleetLabels;
            }
        }

        /// <summary>
        /// The system's own hangar, where the map is drawing one.
        ///
        /// A hangar is the ships a colony is holding at home. The map draws it as a lozenge of its own
        /// beside the fleet lozenges (<c>HangarLabel</c>), and the mod already counts it in what a system
        /// says is parked there - the count phrase comes from the dock label's own group, which includes
        /// it - but it was not a place the player could go, and it is the one route to the button that
        /// turns those ships into a fleet: clicking the lozenge selects the hangar garrison and swaps in
        /// the garrison cursor, which is what puts the fleet panel on the screen with Create on it.
        ///
        /// WHICH hangars are drawn is the label window's own answer, taken from the same repository it
        /// walks (<c>HangarLabelsWindow.ShowAllLabels</c> over
        /// <c>IVisibleGalaxyHangarRepositoryService</c>) with the same test it draws on
        /// (<c>ShowLabel</c>: the hangar is holding ships) - so a hangar nobody can see is absent here
        /// for the reason it is absent from the picture, and no visibility rule is reimplemented.
        ///
        /// Last among a system's children, after the fleets: it is the thing at that system that is not
        /// going anywhere.
        /// </summary>
        private static void AddHangars(GraphBuilder builder, string place, StarSystemNode node)
        {
            try
            {
                IVisibleGalaxyHangarRepositoryService repository =
                    Amplitude.Unity.Framework.Services.GetService<IVisibleGalaxyHangarRepositoryService>();
                if (repository == null)
                {
                    return;
                }

                HangarLabel[] labels = null;
                ReadOnlyCollection<GalaxyHangar> hangars = repository.GalaxyHangars;
                for (int i = 0; i < hangars.Count; i++)
                {
                    GalaxyHangar hangar = hangars[i];
                    Hangar held = hangar == null ? null : hangar.Hangar;
                    if (held == null || held.ShipsCount <= 0 || !IsAt(held, node))
                    {
                        continue;
                    }

                    if (labels == null)
                    {
                        labels = HangarLabels();
                    }

                    GalaxyHangar it = hangar;
                    AgeTransform lozenge = HangarLozenge(hangar, labels);
                    NodeVtable vtable = GraphNodes.Button(
                        () => held.LocalizedName,
                        () => SelectHangar(it),
                        null,
                        Raw(lozenge)
                    );
                    vtable.Announcements.Add(
                        GraphNodes.ValuePart(
                            () => ModStrings.Format(ModStrings.GalaxyFleetShips, held.ShipsCount)
                        )
                    );
                    if (lozenge != null)
                    {
                        PointAt(vtable, lozenge);
                    }

                    // Synthetic: a ship in a hangar is read out of the fleet model, which the map draws nothing for.
                    builder.AddItem(Nodes.Synthetic(
                        ControlId.Structural(place + "/hangar/" + held.GUID),
                        vtable
                    ));
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's hangar threw: " + e);
            }
        }

        /// <summary>Whether a hangar is the one belonging to this system. A hangar hangs off the node
        /// itself rather than off the colony, which is what makes it survive the colony changing hands.
        /// </summary>
        private static bool IsAt(Hangar hangar, StarSystemNode node)
        {
            try
            {
                GameNode at = hangar.GetGameNode();
                return at != null && at.GUID == node.GUID;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static readonly HangarLabel[] NoHangarLabels = new HangarLabel[0];

        private static HangarLabel[] HangarLabels()
        {
            try
            {
                HangarLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<HangarLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoHangarLabels
                    : window.GetComponentsInChildren<HangarLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the hangar labels threw: " + e);
                return NoHangarLabels;
            }
        }

        /// <summary>The lozenge the map is drawing this hangar with - the same choice
        /// <see cref="FleetLozenge"/> makes for a fleet, and for the same reason: the tooltip that gets
        /// DRAWN is the one on whatever the pointer is over, and the label carries two.</summary>
        private static AgeTransform HangarLozenge(GalaxyHangar hangar, HangarLabel[] labels)
        {
            try
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    HangarLabel label = labels[i];
                    if (
                        label != null
                        && ReferenceEquals(label.GalaxyHangar, hangar)
                        && Visible(label.AgeTransform)
                    )
                    {
                        return Lozenge(label.FleetLozenge);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a hangar to its map label threw: " + e);
            }

            return null;
        }

        /// <summary>Select the ships a colony is holding at home, exactly as clicking their lozenge does
        /// (<c>HangarLabel.OnClickCb</c>): the hangar's cursor target becomes the selection and the
        /// garrison cursor takes over, which is what draws the fleet panel and the Create button on it.
        /// Nothing is said here - the panel's own watcher says the panel opened, from the one place that
        /// knows.</summary>
        private static void SelectHangar(GalaxyHangar hangar)
        {
            try
            {
                // Nothing is selected while a targeting mode is waiting - the same rule the fleet nodes
                // follow (<see cref="Select"/>).
                if (CursorTargeting.Aiming)
                {
                    return;
                }

                Amplitude.Unity.View.ICursorService cursors =
                    Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
                if (cursors == null || hangar == null || hangar.CursorTarget == null)
                {
                    return;
                }

                cursors.Select(hangar.CursorTarget);
                cursors.ChangeCursor(typeof(GalaxyGarrisonCursor), hangar);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: selecting a hangar threw: " + e);
            }
        }

        /// <summary>
        /// The lozenge the map is drawing for a fleet whose OWN label it has folded away.
        ///
        /// Two fleets that come close enough together on screen are drawn as one marker: the map
        /// merges their labels (<c>MergedFleetLabels</c>), hides each fleet's own and binds the merged
        /// marker's button to the whole group. A fleet in that state has a hidden lozenge, so the node
        /// pointed at nothing, promised the group dossier and drew it nowhere - measured on the
        /// beginner fixture, where two fleets two turns out share a marker and neither said anything.
        ///
        /// The marker's dossier is the GROUP's, which is exactly what the mouse gets for hovering it:
        /// there is no per-fleet tooltip to be had while the map is drawing them as one thing.
        /// </summary>
        private static AgeTransform MergedLozenge(Fleet fleet)
        {
            FleetLabelsWindow window = Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<FleetLabelsWindow>(false)
                : null;
            if (window == null)
            {
                return null;
            }

            MergedFleetLabels[] merged = window.GetComponentsInChildren<MergedFleetLabels>(true);
            for (int i = 0; i < merged.Length; i++)
            {
                MergedFleetLabels group = merged[i];
                DualGarrisonsLabelButtons buttons =
                    group == null ? null : group.GarrisonsButtons;
                if (buttons == null || !Visible(group.AgeTransform))
                {
                    continue;
                }

                AgeTransform found =
                    Holding(buttons.FriendlyGarrisonsButton, fleet)
                    ?? Holding(buttons.HostileGarrisonsButton, fleet);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>The merged marker's button, where the group it is bound to holds this fleet - asked
        /// of the wrapper on its own tooltip, which is the list the game itself drew the marker
        /// from.</summary>
        private static AgeTransform Holding(GarrisonsLabelButton button, Fleet fleet)
        {
            AgeTransform widget = Lozenge(button);
            AgeTooltip tooltip = button == null ? null : button.Tooltip;
            GuiFleetGroup group = tooltip == null ? null : tooltip.Target as GuiFleetGroup;
            if (widget == null || group == null || !Visible(widget))
            {
                return null;
            }

            IList<Garrison> garrisons = group.Garrisons;
            for (int i = 0; garrisons != null && i < garrisons.Count; i++)
            {
                if (garrisons[i] != null && garrisons[i].GUID == fleet.GUID)
                {
                    return widget;
                }
            }

            return null;
        }

        private static AgeTransform Lozenge(GarrisonsLabelButton button)
        {
            try
            {
                return button == null ? null : button.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

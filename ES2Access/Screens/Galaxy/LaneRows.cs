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
    /// <summary>The starlanes leaving a system, and the directions a probe could be sent.</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>A starlane's own node, keyed exactly as <see cref="AddStarlanes"/> keys it - on
        /// the pair of GUIDs, under the system the lane is being read FROM, because one lane runs
        /// between two systems and each end is its own node.</summary>
        internal static ControlId LaneId(StarSystemNode node, Link link)
        {
            return ControlId.Structural(SystemKey(node) + "/lane/" + link.GUID);
        }

        /// <summary>
        /// The lanes the map draws out of a system, in the order - and therefore with the NUMBERS -
        /// that every part of this screen names them by.
        ///
        /// ONE list, because three things have to agree about it: the lane nodes themselves, the fleets
        /// under way that are hosted under this system and say which lane they are on, and the count
        /// phrase the system announces for them. Working the list out three times is how "starlane 2"
        /// comes to mean two different lines depending on which control said it.
        ///
        /// A LANE IS OFFERED exactly while the map draws the line. The map's rule is not "has it been
        /// revealed" but an intensity taken from the link's own exploration state
        /// (<c>GalaxyLink.Refresh</c> :247-252 feeds <c>GetIntensityFromState</c> :362-372), and that
        /// intensity is ZERO - an invisible line - for Localized and Identified as well as for
        /// Unrevealed. It only lights up at PartiallyRevealed. Offering a lane the map is drawing at
        /// intensity nought is offering a road that is not on the screen, which is why the threshold
        /// here is that one and not the reveal flag it used to be (owner ruling, after the drawn-parity
        /// measurement of the unexplored-lane preview).
        ///
        /// A wormhole is a different thing from a starlane and is said to be one. An empire without the
        /// technology to see them is shown none, exactly as the game's own neighbour search skips them;
        /// beyond that a wormhole is a <c>Link</c> like any other and passes the same intensity test,
        /// because <c>GalaxyWormhole</c> draws its line through the same <c>GalaxyLink.Refresh</c>.
        ///
        /// The game numbers no lane and the model's own order is whatever order the galaxy was
        /// generated in, so the lanes are walked - and numbered - going clockwise from north, and each
        /// one says the way it leaves. That is the mod's ordering, not the game's: a player who cannot
        /// see the lines needs the same "which one is that" the picture gives everyone else, and a
        /// number that moves between sessions would be worse than none.
        /// </summary>
        internal static List<Lane> LanesOf(StarSystemNode node, Empire empire)
        {
            List<Lane> lanes = new List<Lane>();
            try
            {
                for (int i = 0; i < node.Links.Count; i++)
                {
                    Link link = node.Links[i];
                    bool wormhole = link is WormholeLink;
                    if (wormhole && !empire.HasWormholeTechnology)
                    {
                        continue;
                    }

                    if (!MapVisibility.Drawn(link, empire))
                    {
                        continue;
                    }

                    GameNode far = ReferenceEquals(link.ExtremityNode1, node)
                        ? link.ExtremityNode2
                        : link.ExtremityNode1;
                    lanes.Add(
                        new Lane
                        {
                            Link = link,
                            Far = far,
                            Wormhole = wormhole,
                            Bearing = CompassDirections.Bearing(
                                far.GalaxyPosition.X - node.GalaxyPosition.X,
                                far.GalaxyPosition.Y - node.GalaxyPosition.Y
                            ),
                        }
                    );
                }

                lanes.Sort(ClockwiseFromNorth);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: working out a system's starlanes threw: " + e);
            }

            return lanes;
        }

        /// <summary>
        /// The lanes leaving a system, as LEAVES - and right on one goes down it.
        ///
        /// A lane says the name of the system at its far end only when the map draws that name.
        /// Everything else is a lane into the unexplored, which is what the map shows: a line running
        /// off into the dark. The galaxy model would answer either way - it holds every system's name
        /// from the first turn - so the check, not the model, is what keeps this honest. A lane into the
        /// dark is a leaf with nowhere to go and answers right with nothing, silently.
        ///
        /// A NAMED lane TRAVELS (<see cref="Travel"/>): the cursor is rebased onto the destination
        /// system's own node at the root of this stop. Not a child of its own naming that system, which
        /// is what this used to be: a second node for a place that already has one breaks the
        /// one-object-one-node rule, forced its whole contents to be re-keyed structurally, and had to be
        /// made deliberately poorer than the original - no lanes of its own - or the tree would have had
        /// no bottom. Rebasing keeps one node per place and puts no bottom on the tree at all.
        ///
        /// Everything else a lane does is unchanged: what is flying it, what sending the selection here
        /// would cost, the map's own left click, and the map's own right click.
        /// </summary>
        private void AddStarlanes(
            GraphBuilder builder,
            string place,
            StarSystemNode node,
            Empire empire,
            List<Lane> lanes
        )
        {
            try
            {
                HashSet<ControlId> expansion = builder.Expansion;
                for (int i = 0; i < lanes.Count; i++)
                {
                    Link link = lanes[i].Link;
                    GameNode destination = lanes[i].Far;
                    int number = i + 1;
                    string direction = CompassDirections.KeyForBearing(lanes[i].Bearing);
                    bool named = Perceived(destination, empire);
                    string template = lanes[i].Wormhole
                        ? (named ? ModStrings.GalaxyWormhole : ModStrings.GalaxyWormholeUnexplored)
                        : (named ? ModStrings.GalaxyStarlane : ModStrings.GalaxyStarlaneUnexplored);
                    Func<string> text = named
                        ? (Func<string>)(
                            () =>
                                ModStrings.Format(
                                    template,
                                    number,
                                    destination.LocalizedName,
                                    ModStrings.Get(direction)
                                )
                        )
                        : () => ModStrings.Format(template, number, ModStrings.Get(direction));
                    Link lane = link;
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(text),
                            // Whatever the map is drawing out on this lane, said the way the map's own
                            // lozenge tooltip heads it. Read on focus rather than watched, for the same
                            // reason a system's is.
                            GraphNodes.ValuePart(() => FleetPresence.On(lane), false),
                            // A lane is a destination in its own right, so it previews like one.
                            GraphNodes.ValuePart(() => FleetRoute.Preview(lane), false),
                        },
                        Sections = GraphNodes.Sections(
                            NodeSection.Buffer(() => FleetPresence.LinesOn(lane)),
                            NodeSection.Buffer(() => FleetRoute.PreviewLines(lane))
                        ),
                    };
                    // A lane is a destination in its own right, not just a road to one: the game
                    // accepts a link as a move target and flies the fleet out onto it
                    // (`GalaxyGarrisonCursor.GetGalaxyPathToTargets` resolves either a node or a
                    // link), which is how a fleet is parked between two systems or pointed down a
                    // lane into the dark. So backslash - the map's own move click - sends here too.
                    //
                    // ENTER on a lane is the map's LEFT click on one, and the only thing that click
                    // does is let go of whatever the cursor is holding
                    // (`GalaxyGarrisonCursor.OnCursorClick` :88-95 changes back to the plain cursor for
                    // a click that landed on a link and nothing else). With a fleet selected that is
                    // exactly what Enter does here.
                    //
                    // With NOTHING selected and no mode armed the mouse's click does nothing at all,
                    // and there the keyboard is given the one thing the line is good for: going where
                    // it leads, the same hop Right already makes (`OnFollow` below). A deliberate
                    // exception to Enter-is-click-parity, owner ruling 2026-08-20 - the click being a
                    // no-op is what makes it free to take, and a lane into the dark has nowhere to go
                    // so it keeps its silence.
                    //
                    // While a targeting mode is armed that same left click means "confirm here" instead,
                    // for a line as much as for a system (<see cref="CursorTargeting"/>) - and a lane is
                    // where the probe mode is aimed by rights, since a lane running into the dark is the
                    // only way to name a direction with no system at the end of it.
                    Link target = link;
                    GameNode aim = destination;
                    vtable.OnContextual = () => LaneCommand(target);
                    MoveHints(vtable);
                    // ...and the one thing the map's LEFT click on a line does with a selection up:
                    // let go of it. Only here and on empty space - Enter on a system zooms instead.
                    NodeHints.Add(
                        vtable,
                        ModStrings.HintDeselectFleet,
                        UiActions.Activate,
                        0,
                        FleetOrders.AnySelected
                    );

                    string key = place + "/lane/" + link.GUID;
                    // Keyed on the pair of GUIDs and NOT carrying the link as a reference: ONE lane runs
                    // between two systems, and once both ends are in the tree the same Link object backs
                    // two nodes - which are one control to the cursor, because reference identity is
                    // followed before the structural key. Measured with the fog lifted: focusing the
                    // Hir end of the Xiu-Hir lane threw the cursor across to the Xiu end on the next
                    // rebuild. Two GUIDs are stable without a reference, so nothing is lost.
                    ControlId id = ControlId.Structural(key);
                    // Right on a named lane goes THERE. Wired as a follow rather than as an expansion:
                    // an empty group auto-recollapses and speaks "no details" over the very landing this
                    // makes, and a group that really declared the far system would be that system's
                    // second node (<see cref="NodeVtable.OnFollow"/>). A lane running into the dark
                    // wires nothing and answers the key with silence, as a leaf does everywhere.
                    StarSystemNode far = named ? destination as StarSystemNode : null;
                    Action travel = null;
                    if (far != null)
                    {
                        StarSystemNode from = node;
                        StarSystemNode to = far;
                        ControlId here = id;
                        GalaxyHudScreen screen = this;
                        HashSet<ControlId> expanded = expansion;
                        travel = () => screen.Travel(from, here, to, expanded);
                        vtable.OnFollow = travel;
                    }

                    // Wired after the hop, because Enter falls through to the very same hop when the
                    // click it stands for would have done nothing (<see cref="LaneClick"/>): one
                    // derivation of where this lane leads, used by both keys.
                    vtable.OnActivate = () => LaneClick(target, aim, travel);
                    // Synthetic: a lane is a link in the galaxy model - the map draws it as a line
                    // between two places, never as a control.
                    builder.AddItem(Nodes.Synthetic(id, vtable));
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's starlanes threw: " + e);
            }
        }

        /// <summary>One lane leaving a system, with the way it leaves already worked out.</summary>
        internal struct Lane
        {
            public Link Link;
            public GameNode Far;
            public bool Wormhole;
            public double Bearing;
        }

        private static readonly Comparison<Lane> ClockwiseFromNorth = delegate(Lane a, Lane b)
        {
            return a.Bearing.CompareTo(b.Bearing);
        };

        /// <summary>
        /// The sixteen bearings a probe can be launched on, offered at the system the fleet holding it is
        /// standing at, and only while the map is waiting for that launch.
        ///
        /// The lanes above are where a probe is usually aimed, and they are not all of it: the order
        /// takes ANY direction that is not zero (<c>LaunchProbeFleetActionDefinition.CheckContext</c>
        /// :92-95 refuses the zero vector alone), and the game's own tutorial for this action tells the
        /// player to send the probe away from the known lanes to find a new constellation. A mouse aims
        /// at empty sky by clicking it; the keyboard had only the map's own nodes, so every direction
        /// with nothing along it was unreachable. SIXTEEN of them rather than the eight the mod
        /// describes lanes with: a lane's compass word only has to name where a line that already exists
        /// runs, while these are the whole of the map a probe can be sent into, and the arcs eight words
        /// leave between them are directions no player can ask for
        /// (<see cref="CompassDirections.KeyForBearing16"/>).
        ///
        /// Each one says what is DOWN it - the stretches of fog a probe flown that way would cross and
        /// how far the map goes before it ends (<see cref="ProbeContext"/>) - because the order cannot be
        /// recalled, cannot be aimed at anything but a direction, and is therefore chosen entirely on
        /// what each direction is worth. A sighted player reads that off the fog in a second; a bare
        /// compass word said sixteen times gives a listener nothing to choose between. The group itself
        /// is named by how far the probe will get, which is the fact all sixteen are measured against.
        ///
        /// They sit here, LAST in the system's branch, after everything the map really draws at this
        /// place: they are sixteen ways OUT of it rather than anything in it, and they are gone again the
        /// moment the mode ends. The player does not have to walk to them: arming
        /// the mode seats the cursor on the first bearing itself, opening this group and the system's
        /// branch to do it (<see cref="FollowProbeArming"/>), because the fleet panel the mode is armed
        /// from is taken off the screen by the arming and the player would otherwise be left standing
        /// wherever they happened to be. They exist only while the mode does, which is the same rule
        /// the map's own "click a target" banner is drawn by.
        /// </summary>
        private static void AddProbeDirections(
            GraphBuilder builder,
            string place,
            StarSystemNode node
        )
        {
            try
            {
                ProbeLaunchingCursor cursor = CursorTargeting.ArmedProbe;
                Fleet fleet = cursor == null ? null : cursor.ProbeOriginFleet;
                if (
                    fleet == null
                    || fleet.IsDestroyed
                    || !fleet.Position.IsInOrbit
                    || fleet.NodePosition != node.NodePosition
                )
                {
                    return;
                }

                ControlId id = ControlId.Structural(place + "/launch");
                // Synthetic: mod-authored - the probe launcher is the mod's own way of aiming a launch the map only offers to a mouse.
                builder.BeginGroup(Nodes.Synthetic(
                    id,
                    GraphNodes.Group(() => ProbeContext.GroupLabel(fleet, node))
                ));
                if (builder.IsExpanded(id))
                {
                    for (int i = 0; i < ProbeContext.Bearings; i++)
                    {
                        int index = i;
                        double bearing = ProbeContext.Bearing(i);
                        // Synthetic: mod-authored, one bearing per direction the launch can take. What
                        // it says is the heading and the share, because the player walks sixteen of
                        // these; the fog that explains the share is the buffer's, a clause per line
                        // (<see cref="ProbeContext.Lines"/>).
                        builder.AddItem(Nodes.Synthetic(
                            ControlId.Structural(place + "/launch/" + i),
                            GraphNodes.Button(
                                () => ProbeContext.Label(fleet, node, index),
                                () => CursorTargeting.ConfirmTowards(bearing),
                                details: () => ProbeContext.Lines(fleet, node, index)
                            )
                        ));
                    }
                }

                builder.EndGroup();
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: offering the probe's launch directions threw: " + e);
            }
        }

        /// <summary>
        /// Send the selection out onto a lane.
        ///
        /// Onto the lane a fleet is ALREADY flying, the game answers with the route to the next system
        /// on it (<c>GalaxyGarrisonCursor.GetGalaxyPathToLink</c> :352-361, ported in
        /// <c>FleetOrders.PathToLink</c>), which is the game's own way of saying "stop when you get
        /// there" - so a fleet is called off its long journey by asking it to fly the piece of lane it
        /// is on, and there is no cancel to invent.
        /// </summary>
        private static void LaneCommand(Link link)
        {
            List<Fleet> selected = FleetOrders.Selected();
            if (selected.Count == 0)
            {
                return;
            }

            List<FailureInfo> refusals = new List<FailureInfo>();
            SendAll(SendableTo(link, selected, refusals), refusals);
        }

        /// <summary>
        /// The map's own left click on a lane, and then the one thing that click never had a use for.
        ///
        /// Asked in the same first-refusal order the click on a system is asked in
        /// (<see cref="ZoomIn"/>), so the two cannot drift apart: while an order is waiting for a
        /// target the click is aimed down this line (<see cref="CursorTargeting"/>), and while the
        /// cursor is holding fleets it is the click that lets go of them (<see cref="Deselect"/>).
        ///
        /// With neither of those true the mouse's click on a lane does nothing whatsoever, and that
        /// nothing is what this last branch spends: Enter goes where the lane leads, which is the hop
        /// the descend key already makes (<paramref name="travel"/> IS the node's own
        /// <c>OnFollow</c>, so there is one derivation of the far end and not two). A deliberate
        /// exception to the otherwise absolute rule that Enter is the game's left click and invents
        /// nothing - owner ruling 2026-08-20, on the grounds that a gesture the game answers with
        /// nothing at all costs the player nothing to give away. A lane running into the dark leads
        /// nowhere the map has named, wires no hop, and so keeps the silence it had.
        /// </summary>
        private static void LaneClick(Link lane, GameNode far, Action travel)
        {
            if (CursorTargeting.ConfirmAt(lane, far))
            {
                return;
            }

            if (Deselect() || travel == null)
            {
                return;
            }

            travel();
        }

        /// <summary>
        /// Let go of whatever the map's cursor is holding, exactly as a click on empty space or on a
        /// starlane does (<c>GalaxyGarrisonCursor.OnCursorClick</c>): the garrison cursor is swapped
        /// back for the plain one, which is also what takes the fleet panel off the screen.
        ///
        /// Answers whether anything was actually let go, which is how the lane's Enter knows the
        /// click it stands for would have done nothing (<see cref="LaneClick"/>). The garrison cursor
        /// being up IS that question: it is the cursor the map wears while it is holding a selection,
        /// and the same thing the selected-fleet panel's own visibility is gated on.
        ///
        /// Nothing is said here. The panel going is what the player is being told about, and the
        /// panel's own watcher says it - one announcement, from the one place that knows.
        /// </summary>
        private static bool Deselect()
        {
            try
            {
                Amplitude.Unity.View.ICursorService cursors =
                    Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
                if (cursors == null || !(Gui.GetCursor() is GalaxyGarrisonCursor))
                {
                    return false;
                }

                cursors.ChangeCursor(typeof(GalaxyCursor), Gui.GetCursor());
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: letting go of the selection threw: " + e);
                return false;
            }
        }
    }
}

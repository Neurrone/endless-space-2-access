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
    /// <summary>What the map draws away from any star: quest markers, drifting probes, obliterator
    /// shots, coordination pins, and sending the selection out to one of them.</summary>
    public sealed partial class GalaxyHudScreen
    {
        // ---- quest markers ----

        /// <summary>
        /// The markers standing at one system, as lines for its review buffer - which quests have a
        /// pin here and which of them the player is tracking.
        ///
        /// The same list its own child nodes are built from (<see cref="AddQuestMarkers"/>), because
        /// the buffer says what is here and the nodes are how the player goes to it, and those two
        /// disagreeing is a place that says a quest is here and has no row for it.
        /// </summary>
        private IList<string> QuestMarkerLines(StarSystemNode node, Empire empire)
        {
            List<QuestMarkers.Marker> here = MarkersAt(node, empire);
            if (here.Count == 0)
            {
                return null;
            }

            List<string> lines = new List<string>(here.Count);
            for (int i = 0; i < here.Count; i++)
            {
                string line = QuestMarkers.Name(here[i]);
                if (!string.IsNullOrEmpty(line) && !lines.Contains(line))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>The markers the map draws AT this system, in journal order.</summary>
        private List<QuestMarkers.Marker> MarkersAt(StarSystemNode node, Empire empire)
        {
            List<QuestMarkers.Marker> here = new List<QuestMarkers.Marker>();
            if (node == null)
            {
                return here;
            }

            List<QuestMarkers.Marker> all = QuestMarkers.Of(empire);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Node.IsValid && all[i].Node == node.NodePosition)
                {
                    here.Add(all[i]);
                }
            }

            return here;
        }

        /// <summary>
        /// A quest marker as a CHILD of the system it stands at - after the planets, the lanes and the
        /// fleets, which is the order the rest of a system reads in.
        ///
        /// A marker was a buffer line and nothing else until 2026-08-22, which meant the one place the
        /// game itself offers a "go here" for - the quest pin - was somewhere the tree could not put
        /// the player. It is a leaf: no tooltip (the game hangs none on a marker), and ENTER IS INERT,
        /// because a pin is not clickable on the map either and there is no journal-opening gesture to
        /// invent. What it carries beyond its name is the step's own objective, in the game's words.
        /// </summary>
        private void AddQuestMarkers(
            GraphBuilder builder,
            string key,
            StarSystemNode node,
            Empire empire
        )
        {
            List<QuestMarkers.Marker> here = MarkersAt(node, empire);
            for (int i = 0; i < here.Count; i++)
            {
                // Synthetic: a quest marker is a game fact the map draws as a pin over the world, not as a control.
                builder.AddItem(Nodes.Synthetic(MarkerId(node, here[i]), MarkerNode(here[i])));
            }
        }

        /// <summary>Every marker planted out in the OPEN - on a fleet crossing a lane - as a row of
        /// the galaxy's own drifting region, beside the probes and the missiles, since there is no
        /// place in the tree for it to hang under.</summary>
        private void AddOpenSpaceMarkers(GraphBuilder builder, Empire empire)
        {
            List<QuestMarkers.Marker> all = QuestMarkers.Of(empire);
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].Node.IsValid)
                {
                    // Synthetic: the same, for a marker whose place the galaxy no longer holds.
                    builder.AddItem(Nodes.Synthetic(MarkerRowId(all[i]), MarkerNode(all[i])));
                }
            }
        }

        /// <summary>Whether the map is drawing any marker out in the open - what decides whether the
        /// drifting region exists at all.</summary>
        private int OpenSpaceMarkers(Empire empire)
        {
            List<QuestMarkers.Marker> all = QuestMarkers.Of(empire);
            int loose = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].Node.IsValid)
                {
                    loose++;
                }
            }

            return loose;
        }

        /// <summary>What one marker's row says: the quest it belongs to, in the tracked or the
        /// ordinary form, and where it stands; the step's objective is reviewable rather than
        /// announced.</summary>
        private static NodeVtable MarkerNode(QuestMarkers.Marker marker)
        {
            QuestMarkers.Marker it = marker;
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => QuestMarkers.Name(it)),
                    GalaxyCoordinates.Part(() => it.At),
                },
                Sections = GraphNodes.Sections(NodeSection.Buffer(() => QuestMarkers.Objective(it))),
            };
        }

        /// <summary>A marker's node under the system it stands at - keyed by the pin, so two markers
        /// of two quests at one star stay apart and neither moves when the other goes.</summary>
        private static ControlId MarkerId(StarSystemNode node, QuestMarkers.Marker marker)
        {
            return ControlId.Structural(SystemKey(node) + "/marker/" + marker.Pin.GUID);
        }

        /// <summary>A marker's own top-level row, for one standing out in the open.</summary>
        private static ControlId MarkerRowId(QuestMarkers.Marker marker)
        {
            return ControlId.Structural("galaxy:marker/" + marker.Pin.GUID);
        }

        /// <summary>The system a marker stands at, where the map is naming one - which is what decides
        /// whether the marker is a child of a place or a row out in the open.</summary>
        private StarSystemNode MarkerSystem(QuestMarkers.Marker marker)
        {
            if (!marker.Node.IsValid)
            {
                return null;
            }

            for (int i = 0; i < _systems.Count; i++)
            {
                if (_systems[i].NodePosition == marker.Node)
                {
                    return _systems[i];
                }
            }

            return null;
        }

        // ---- sending the selected fleets somewhere ----
        //
        // Moving a fleet is a DRAG in this game: you pick a fleet up and drop it on a place. A drag has
        // no keyboard equivalent, so the two halves are separated - Enter on a fleet picks it up
        // (selects it), and BACKSLASH on the destination drops it, because backslash is what the map
        // itself puts a move on. Which is also the game's own model rather than a mod invention:
        // selecting a fleet changes the cursor and nothing else, the map stays live underneath, and
        // naming a destination moves everything the cursor is holding
        // (`GalaxyGarrisonCursor.GetFleetsToMove`). One fleet or five, what is spoken back says which.
        //
        // Whether anything can go at all is the pathfinder's own answer, asked when the key is pressed -
        // never per frame, and never guessed at from the map.

        /// <summary>One selected fleet and the route that would take it to the destination the key was
        /// pressed on.</summary>
        private sealed class Sendable
        {
            public Sendable(Fleet fleet, GalaxyPath path)
            {
                Fleet = fleet;
                Path = path;
            }

            public readonly Fleet Fleet;

            public readonly GalaxyPath Path;
        }

        /// <summary>Which of the selected fleets could be sent to a system. A fleet already parked
        /// there is not one of them: the game accepts the order and then does nothing about it, so
        /// counting it would turn the key's answer into a report of something that did not happen.
        /// </summary>
        private static List<Sendable> SendableTo(
            GameNode node,
            List<Fleet> fleets,
            List<FailureInfo> refusals
        )
        {
            List<Sendable> found = new List<Sendable>();
            for (int i = 0; i < fleets.Count; i++)
            {
                GameNode orbit = FleetOrders.Orbit(fleets[i]);
                if (orbit != null && orbit.GUID == node.GUID)
                {
                    continue;
                }

                AddSendable(
                    found,
                    fleets[i],
                    FleetOrders.PathTo(fleets[i], node, refusals),
                    refusals
                );
            }

            return found;
        }

        private static List<Sendable> SendableTo(
            Link link,
            List<Fleet> fleets,
            List<FailureInfo> refusals
        )
        {
            List<Sendable> found = new List<Sendable>();
            for (int i = 0; i < fleets.Count; i++)
            {
                AddSendable(
                    found,
                    fleets[i],
                    FleetOrders.PathToLink(fleets[i], link, refusals),
                    refusals
                );
            }

            return found;
        }

        private static void AddSendable(
            List<Sendable> found,
            Fleet fleet,
            GalaxyPath path,
            List<FailureInfo> refusals
        )
        {
            if (path != null && FleetOrders.CanSend(fleet, path, refusals))
            {
                found.Add(new Sendable(fleet, path));
            }
        }

        /// <summary>
        /// Post one move per fleet, along the route worked out when the key was pressed - which is how
        /// the game itself does a multi-fleet drag: one order each, and each one checked again as it is
        /// posted.
        ///
        /// What went is said back, named after what would actually go: the fleet by name while there is
        /// one, and how many while there are several.
        ///
        /// Where nothing could get there, the REASONS are said instead - the game's own sentences, in
        /// the game's own words, gathered by the same searches and the same action check the mouse's
        /// drag runs (<see cref="FleetOrders"/>). This key is pressed on a destination the tree has
        /// named, with fleets already selected, so it is never a speculative press: silence there told
        /// the player nothing at all, and "the star system is frozen in time" or "the fleet cannot move
        /// right now" is the whole answer. (This reverses the silence this comment used to record -
        /// owner decision 2026-08-14.) The one case still silent is the one with nothing to explain: no
        /// route was refused because every selected fleet is already parked at the destination, which
        /// <see cref="SendableTo"/> passes over without asking the pathfinder anything.
        /// </summary>
        private static void SendAll(List<Sendable> sendable, List<FailureInfo> refusals)
        {
            if (sendable.Count == 0)
            {
                SayRefusals(refusals);
                return;
            }

            for (int i = 0; i < sendable.Count; i++)
            {
                FleetOrders.Send(sendable[i].Fleet, sendable[i].Path);
            }

            Voice.Say(
                sendable.Count == 1
                    ? ModStrings.Format(
                        ModStrings.GalaxySendFleet,
                        sendable[0].Fleet.LocalizedName
                    )
                    : ModStrings.Format(ModStrings.GalaxySendFleets, sendable.Count),
                true
            );
        }

        /// <summary>
        /// Why nothing could go, in the game's own words. Each reason is one of the game's failure
        /// flags, turned into the same sentence the map's own failure panel shows a mouse player
        /// (<c>Gui.FormatFailureInfo</c>, which is what carries the flag's parameter - the technology
        /// a route needs - into the sentence). Several selected fleets can be refused for the same
        /// reason, and the same fleet can be refused twice over on the way to a starlane, so identical
        /// sentences are said once; the rest are read out as a list, oldest reason first, which is the
        /// order the searches asked their questions in.
        ///
        /// An ignorable flag is skipped, exactly as the game's own formatter skips it: those are the
        /// bookkeeping flags a panel uses to grey a button, not things to tell anybody.
        /// </summary>
        private static void SayRefusals(List<FailureInfo> refusals)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                List<string> said = new List<string>();
                for (int i = 0; i < refusals.Count; i++)
                {
                    FailureInfo refusal = refusals[i];
                    if (refusal == null || refusal.IsIgnorable)
                    {
                        continue;
                    }

                    string text = AgeText.Clean(Gui.FormatFailureInfo(string.Empty, refusal));
                    if (string.IsNullOrEmpty(text) || said.Contains(text))
                    {
                        continue;
                    }

                    said.Add(text);
                    message.ListItem(text);
                }

                if (said.Count > 0)
                {
                    Voice.Say(message.Build(), true);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading why a fleet could not be sent threw: " + e);
            }
        }

        // ---- what the map draws between the stars ----
        //
        // A fleet is somewhere: it orbits a system or flies a lane, and the game says so by storing a
        // node or a leg on it, which is why the tree can hang it under the place the map draws it. A
        // probe, an obliterator missile and an ally's pin are NOT: each one carries a bare
        // `GalaxyPosition`, a point in space with no node and no link on it, and the map draws each one
        // at that point, out where there is nothing else. So they are not children of anything - they
        // are a third half of the map, walked beside the systems and jumped to as a region of their own.
        //
        // Which of them exist is never re-derived: the game's own label windows pool one label per
        // entity and show or hide each by the empire's vision and the camera's culling
        // (`VisibleEntityLabelsWindow.RefreshLabels*`), so the drawn label IS the answer to "can this be
        // seen", and a walk of the container costs nothing per frame - a galaxy with no probes in sight
        // has an empty container and this is a null check.

        /// <summary>Gather what the map is drawing out in open space, once per build, from the windows
        /// that draw it. No array is allocated: each container's children are walked in place and only a
        /// child the game is really drawing is asked for its label.</summary>
        private void Drifting()
        {
            _probes.Clear();
            _projectiles.Clear();
            _pins.Clear();
            try
            {
                ProbeLabelsWindow probes = Window<ProbeLabelsWindow>();
                Collect(probes == null ? null : probes.LabelsContainer, _probes);
                ObliteratorProjectileLabelsWindow shots = Window<ObliteratorProjectileLabelsWindow>();
                Collect(shots == null ? null : shots.LabelsContainer, _projectiles);
                CoordinationRequestLabelsWindow pins = Window<CoordinationRequestLabelsWindow>();
                Collect(pins == null ? null : pins.RequestLabelsContainer, _pins);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading what the map draws in open space threw: " + e);
            }

            Anchor();
            Sight();
        }

        /// <summary>One obliterator missile the player has been shown, and the mote the map is drawing
        /// for it while the camera is on it.</summary>
        internal struct SightedShot
        {
            public ObliteratorProjectile Shot;
            public ObliteratorProjectileLabel Label;
        }

        /// <summary>One ally pin the player has been shown, and the label the map is drawing for it
        /// while the camera is on it.</summary>
        internal struct SightedPin
        {
            public CoordinationRequest Request;
            public CoordinationRequestLabel Label;
        }

        /// <summary>
        /// The missiles and the pins, from the SIMULATION under the game's own knowledge gates - the
        /// same move the probes made (<see cref="Anchor"/>) and for the same reason: the label lists
        /// are emptied by a camera cull that says nothing about what the player may know.
        ///
        /// The gates are not the same as each other and neither is invented here. A missile is a
        /// moving entity like a probe, so it is <c>Visibility[empire] >= 3</c>
        /// (<c>VisibleEntityLabel.ShowOrHideIfVisibleByEmpire</c>). A pin is not an entity anybody can
        /// see - it is a message - so its gate is the request's own
        /// <c>CoordinationRequest.IsVisible</c>, which is "not force-hidden and shared with my
        /// alliance" (<c>CoordinationRequestLabel.CanShowRequestLabel</c> asks exactly that). What is
        /// deliberately NOT asked is that method's other half, <c>ShowRequestToggle</c>: that is the
        /// player's global "draw the pins" switch, and whether a reader should obey it is a question
        /// for the owner rather than a fact about knowledge.
        /// </summary>
        private void Sight()
        {
            _shots.Clear();
            _sighted.Clear();
            try
            {
                Empire empire = Gui.PlayerEmpire;
                Game game = Gui.Game;
                Empire[] empires = game == null ? null : game.Empires;
                for (int e = 0; empires != null && e < empires.Length; e++)
                {
                    DepartmentOfDefense defense =
                        empires[e] == null ? null : empires[e].GetAgency<DepartmentOfDefense>();
                    ReadOnlyCollection<ObliteratorProjectile> shots =
                        defense == null ? null : defense.ObliteratorProjectiles;
                    for (int i = 0; shots != null && i < shots.Count; i++)
                    {
                        ObliteratorProjectile shot = shots[i];
                        if (shot != null && MapVisibility.Sighted(shot.Visibility, empire))
                        {
                            _shots.Add(
                                new SightedShot { Shot = shot, Label = LabelFor(shot) }
                            );
                        }
                    }
                }

                ICoordinationRequestRepositoryService requests =
                    Amplitude.Unity.Framework.Services
                        .GetService<ICoordinationRequestRepositoryService>();
                IEnumerable<CoordinationRequest> all = requests;
                if (all != null && empire != null)
                {
                    foreach (CoordinationRequest request in all)
                    {
                        if (request != null && request.IsVisible(empire))
                        {
                            _sighted.Add(
                                new SightedPin { Request = request, Label = LabelFor(request) }
                            );
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the missiles and pins threw: " + e);
            }
        }

        private ObliteratorProjectileLabel LabelFor(ObliteratorProjectile shot)
        {
            for (int i = 0; i < _projectiles.Count; i++)
            {
                if (_projectiles[i] != null && ReferenceEquals(_projectiles[i].Entity, shot))
                {
                    return _projectiles[i];
                }
            }

            return null;
        }

        private CoordinationRequestLabel LabelFor(CoordinationRequest request)
        {
            for (int i = 0; i < _pins.Count; i++)
            {
                if (
                    _pins[i] != null
                    && ReferenceEquals(_pins[i].CoordinationRequest, request)
                )
                {
                    return _pins[i];
                }
            }

            return null;
        }

        /// <summary>One probe the player has been shown, and the star it is drawn nearest to.</summary>
        private struct DriftingProbe
        {
            /// <summary>The mote the map is drawing for it, where the camera happens to be close
            /// enough to be drawing one - null otherwise (<see cref="Anchor"/>). Everything the row
            /// says has a source that does not need it; the label is only ever a shortcut to the
            /// dossier the game assembles at draw time.</summary>
            public ProbeLabel Label;
            public Probe Probe;

            /// <summary>The star its row measures its bearing from, or null while the map is naming no
            /// system at all to measure from. Not a parent: every probe's row is at the top of the stop
            /// (<see cref="AddProbes"/>).</summary>
            public StarSystemNode Near;
        }

        /// <summary>
        /// Which star each probe is out from, which is the question the PICTURE answers: the map draws
        /// the mote at the probe's own position (<c>VisibleEntityLabel.RefreshPositionAndSize</c> puts
        /// the label on <c>camera.WorldToScreenPoint</c> of it), and a sighted player reads where it is
        /// by seeing which star it is out from and how far. So the nearest system the map is naming is
        /// what its row measures from (<see cref="ProbeBearing"/>) - the row itself is at the top of
        /// the stop whatever the answer is (<see cref="AddProbes"/>).
        ///
        /// Nearest is asked afresh on every build rather than remembered from the launch, because the
        /// answer MIGRATES: a probe crosses about six units of galaxy a turn and the fixture's
        /// neighbouring stars are sixteen to twenty-seven apart, so one flying towards a neighbour
        /// reads from the star it launched from for two turns and from the neighbour after that -
        /// which is exactly what the picture shows.
        ///
        /// The candidates are the systems this page is DECLARING, not every system in the galaxy: a
        /// bearing measured from a star the player cannot see names a place they have never heard of.
        ///
        /// The probes themselves come from the SIMULATION rather than from the motes the map is
        /// drawing, because the mote is subject to a camera cull that has nothing to do with what the
        /// player is allowed to know: the label windows keep only the entities Unity's own
        /// <c>CullingGroup</c> reports inside the world camera's frustum
        /// (<c>GalaxyEntityCulling</c> â†’ <c>VisibleEntityLabelsWindow.RefreshLabelsCulling</c>), and
        /// zooming out took every probe row and the whole scanner category away with it. The
        /// information gate is the OTHER test the same window makes -
        /// <c>VisibleEntityLabel.ShowOrHideIfVisibleByEmpire</c>'s <c>Visibility[empire] >= 3</c> -
        /// and that is asked here in full (<see cref="MapVisibility.Sighted"/>). So the tree says the
        /// same thing at every zoom step, and says nothing the picture would not have said had the
        /// camera been closer.
        /// </summary>
        private void Anchor()
        {
            _drifting.Clear();
            try
            {
                Empire empire = Gui.PlayerEmpire;
                Game game = Gui.Game;
                Empire[] empires = game == null ? null : game.Empires;
                for (int e = 0; empires != null && e < empires.Length; e++)
                {
                    DepartmentOfDefense defense =
                        empires[e] == null ? null : empires[e].GetAgency<DepartmentOfDefense>();
                    ReadOnlyCollection<Probe> probes = defense == null ? null : defense.Probes;
                    for (int i = 0; probes != null && i < probes.Count; i++)
                    {
                        Probe probe = probes[i];
                        if (probe == null || !MapVisibility.Sighted(probe.Visibility, empire))
                        {
                            continue;
                        }

                        _drifting.Add(
                            new DriftingProbe
                            {
                                Label = LabelFor(probe),
                                Probe = probe,
                                Near = NearestSystem(probe.GalaxyPosition),
                            }
                        );
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: placing the probes against the systems threw: " + e);
            }
        }

        /// <summary>The mote the map happens to be drawing for this probe, or null while the camera
        /// has culled it out.</summary>
        private ProbeLabel LabelFor(Probe probe)
        {
            for (int i = 0; i < _probes.Count; i++)
            {
                if (_probes[i] != null && ReferenceEquals(_probes[i].Entity, probe))
                {
                    return _probes[i];
                }
            }

            return null;
        }

        /// <summary>The declared system nearest a point on the map, with no radius: the point is one
        /// the map is drawing something at, and the nearest star is what a sighted player reads its
        /// position against however far off it is.</summary>
        private StarSystemNode NearestSystem(GalaxyPosition position)
        {
            NearestPick pick = new NearestPick(double.PositiveInfinity);
            for (int i = 0; i < _systems.Count; i++)
            {
                pick.Offer(i, GalaxyPosition.SqrDistance(_systems[i].GalaxyPosition, position));
            }

            return pick.Found ? _systems[pick.Index] : null;
        }

        /// <summary>Where a probe's node hangs: out in open space, at the top of the stop, wherever the
        /// map happens to be drawing it (owner ruling 2026-08-19). The star it is nearest to is still
        /// worked out - it is what its row measures the bearing from - but it is a thing the row SAYS,
        /// never a place the row lives under.</summary>
        private static string ProbeKey(DriftingProbe probe)
        {
            return "galaxy:probe/" + probe.Probe.GUID;
        }

        /// <summary>Anchored on the PROBE and not on the row's own struct: the struct is rebuilt every
        /// build (it is a probe plus whichever star it currently reads its bearing from), and the thing
        /// that persists across the crossing is the probe.</summary>
        private static ControlId ProbeId(DriftingProbe probe)
        {
            return PlacedRows.Anchor(probe.Probe, ProbeKey(probe));
        }

        /// <summary>One travelling probe as the SCANNER needs it: what it is called, what else its row
        /// says about it, and the node that row is - the last of which only this page can work out,
        /// since a probe's key is the page's own to build.</summary>
        internal struct ScannedProbe
        {
            public Probe Probe;
            public string Name;
            public string Extra;
            public ControlId Node;
        }

        /// <summary>
        /// The probes the scanner offers, off the very list the tree's own probe rows are built from
        /// (<see cref="Anchor"/>) - so a probe the scanner finds always has the row it sends the cursor
        /// to, and neither can name a probe the other does not.
        ///
        /// The words are the row's own: the dossier's title, whose probe it is, and - for the player's
        /// own alone, because that is the only one the map draws a countdown on - how many turns it has
        /// left. Its bearing from the star it hangs under is NOT here: that is a sentence about a place
        /// the scanner is not measuring from, and the scanner has already said where the probe is in
        /// the pair and the offset the player is standing at.
        /// </summary>
        internal IList<ScannedProbe> ScannedProbes()
        {
            List<ScannedProbe> found = new List<ScannedProbe>(_drifting.Count);
            try
            {
                for (int i = 0; i < _drifting.Count; i++)
                {
                    DriftingProbe it = _drifting[i];
                    MessageBuilder extra = new MessageBuilder();
                    extra.Fragment(Owner(it.Probe.Empire));
                    string left = ProbeCountdown(it);
                    if (!string.IsNullOrEmpty(left))
                    {
                        if (extra.IsEmpty)
                        {
                            extra.Fragment(left);
                        }
                        else
                        {
                            extra.ListItemForcedComma(left);
                        }
                    }

                    found.Add(
                        new ScannedProbe
                        {
                            Probe = it.Probe,
                            Name = ProbeName(it.Probe),
                            Extra = extra.Build(),
                            Node = ProbeId(it),
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: listing the probes for the scanner threw: " + e);
            }

            return found;
        }

        private static void Collect<TLabel>(AgeTransform container, List<TLabel> found)
            where TLabel : Component
        {
            IList<AgeTransform> children = container == null ? null : container.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child == null || !Visible(child))
                {
                    continue;
                }

                TLabel label = child.GetComponent<TLabel>();
                if (label != null)
                {
                    found.Add(label);
                }
            }
        }

        private static TWindow Window<TWindow>()
            where TWindow : Amplitude.Unity.Gui.GuiWindow
        {
            try
            {
                TWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<TWindow>(false)
                    : null;
                return window != null && window.Shown ? window : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The probes drifting through the map: whose each one is, and - for the player's own - how many
        /// turns it has left before it burns out.
        ///
        /// The label is a countdown and nothing else, and the game draws that countdown only on a probe
        /// of the player's own (<c>ProbeLabel.Refresh</c>), so that is the only number here. Everything
        /// else about a probe is in the dossier the game hangs on it, which is assembled at draw time
        /// and so is reviewed rather than spoken. The owner is named from the probe itself: the map
        /// tells a sighted player whose it is by the colour of the mote it draws out there, and a colour
        /// is the one thing a reader of this tree has no way to be told.
        ///
        /// There is nothing to activate. A probe is not a thing the game lets anyone click.
        ///
        /// EVERY probe is here, at the top of the open-space region beside the missiles and the pins,
        /// and none of them under a star (owner ruling 2026-08-19). A probe is not going anywhere the
        /// map can name - it has neither node nor link, only a position and a heading - so hanging it
        /// under the star it happens to be nearest to buried it in a branch the player has no reason to
        /// open and made a probe the one thing on this page whose place changed while it flew. Which
        /// star it is out from is still the way a sighted player reads the mote's position, and the row
        /// still says it (<see cref="ProbeBearing"/>) - as a sentence about where it is, not as a
        /// parent.
        /// </summary>
        private void AddProbes(GraphBuilder builder)
        {
            for (int i = 0; i < _drifting.Count; i++)
            {
                // Synthetic: a drifting probe is a game fact the map draws as a mote, not as a control.
                builder.AddItem(Nodes.Synthetic(ProbeId(_drifting[i]), ProbeNode(_drifting[i])));
            }
        }

        /// <summary>What one probe's row says: whose it is, where it is, and - for the player's own -
        /// how many turns it has left before it burns out.</summary>
        private static NodeVtable ProbeNode(DriftingProbe drifting)
        {
            DriftingProbe found = drifting;
            ProbeLabel it = drifting.Label;
            Probe probe = drifting.Probe;
            StarSystemNode near = drifting.Near;
            AgeTooltip dossier = it == null ? null : it.Tooltip;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ProbeName(probe)),
                    GalaxyCoordinates.Part(() => probe.GalaxyPosition),
                    GraphNodes.ValuePart(() => Owner(probe.Empire), false),
                    GraphNodes.ValuePart(() => ProbeBearing(probe, near), false),
                    GraphNodes.ValuePart(() => ProbeCountdown(found), false),
                },
                // The dossier only exists while the game is drawing the mote: it is assembled at
                // draw time onto the label's own tooltip, so a probe the camera has culled has a row
                // with no review section rather than no row.
                Sections = GraphNodes.Sections(null, dossier),
            };
            Follow(vtable, probe, it == null ? null : it.AgeTransform, dossier);
            return vtable;
        }

        /// <summary>
        /// What a probe is called. The game gives it no name of its own; the words are the title of
        /// the wrapper it hangs the dossier on (<c>ProbeLabel.Bind</c> sets
        /// <c>Tooltip.Target = new GuiProbe(probe)</c>), which is a class title and so is the same for
        /// every probe in the game - read once and kept, since it is asked on every frame a probe row
        /// is focused and is not a thing that changes.
        /// </summary>
        private static string ProbeName(Probe probe)
        {
            try
            {
                if (_probeName == null && probe != null)
                {
                    _probeName = AgeText.Clean(new GuiProbe(probe).Title) ?? string.Empty;
                }

                return string.IsNullOrEmpty(_probeName) ? null : _probeName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string _probeName;

        /// <summary>
        /// How many turns a probe has left before it burns out.
        ///
        /// The map writes this on the mote for the player's OWN probes and nobody else's
        /// (<c>ProbeLabel.Refresh</c>: the background is shown only when the probe's empire is the
        /// player's, and the text is <c>GuiProbe.RemainingLifetime</c> with the turn icon), and both
        /// halves of that are said here - the label's own text where the map is drawing it, and the
        /// same two things composed the same way where the camera has culled the mote away. The
        /// gate is the game's, not this mod's: a foreign probe's countdown stays unsaid at every zoom.
        /// </summary>
        private static string ProbeCountdown(DriftingProbe drifting)
        {
            try
            {
                if (drifting.Label != null)
                {
                    return Countdown(
                        drifting.Label.DurationBackground,
                        drifting.Label.DurationLabel
                    );
                }

                Probe probe = drifting.Probe;
                return probe == null || !ReferenceEquals(probe.Empire, Gui.PlayerEmpire)
                    ? null
                    : AgeText.Clean(new GuiProbe(probe).RemainingLifetime + "[turn]");
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Where a probe is, said from the star it is nearest to: which way out it lies, in the same eight
        /// words a starlane's direction is said in, and how far out in turns of the probe's OWN flight.
        ///
        /// Turns rather than a bare length, because a length in galaxy units is a number nothing else in
        /// this game is ever said in, while turns are what the map's own countdown beside the mote is
        /// already counting and what every distance this mod says is already said in. The probe's speed
        /// is its own (<c>SimulationProperties.Probe.Speed</c>, the game's number, not a derived one),
        /// so "three turns out" and "four turns left" can be heard against each other: that probe will
        /// burn out about seven turns from this star.
        ///
        /// Rounded to the turn, and a probe less than half a turn out says only which way it went - a
        /// rounded zero would claim a precision the picture does not have. One that has not left at all
        /// says so, because its direction is not a direction yet.
        /// </summary>
        private static string ProbeBearing(Probe probe, StarSystemNode near)
        {
            try
            {
                if (near == null)
                {
                    return null;
                }

                double east = probe.GalaxyPosition.X - near.GalaxyPosition.X;
                double north = probe.GalaxyPosition.Y - near.GalaxyPosition.Y;
                double distance = Math.Sqrt((east * east) + (north * north));
                if (distance < 0.01)
                {
                    return ModStrings.Format(ModStrings.GalaxyProbeAt, near.LocalizedName);
                }

                string direction = ModStrings.Get(CompassDirections.DirectionKey(east, north));
                float speed = probe.Speed;
                int turns = speed > 0f ? (int)Math.Round(distance / speed) : 0;
                if (turns <= 0)
                {
                    return ModStrings.Format(
                        ModStrings.GalaxyProbeNear,
                        direction,
                        near.LocalizedName
                    );
                }

                return ModStrings.Format(
                    turns == 1 ? ModStrings.GalaxyProbeOutOne : ModStrings.GalaxyProbeOutMany,
                    direction,
                    near.LocalizedName,
                    turns
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The obliterator missiles crossing the map - each one a system's death, in flight.
        ///
        /// The game writes the whole of it into the label's own tooltip as one sentence - which system
        /// it is aimed at and how many turns it has left - and that tooltip is plain text, so it is
        /// spoken on focus rather than only indicated. It writes it for the player's OWN missile only
        /// (<c>ObliteratorProjectileLabel.Refresh</c> clears the tooltip and hides the countdown for
        /// anyone else's), which is the game's own choice about what an empire may know and is left
        /// exactly as it stands: someone else's missile is a thing on the map with no destination
        /// attached, and inventing one from the model would tell the player something the game is
        /// deliberately not showing.
        ///
        /// The missile has no name anywhere in the game's own words, so the phrase is the mod's.
        /// </summary>
        private void AddProjectiles(GraphBuilder builder)
        {
            for (int i = 0; i < _shots.Count; i++)
            {
                ObliteratorProjectileLabel it = _shots[i].Label;
                ObliteratorProjectile shot = _shots[i].Shot;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(
                            () => ModStrings.Get(ModStrings.GalaxyObliteratorProjectile)
                        ),
                        GalaxyCoordinates.Part(() => shot.GalaxyPosition),
                        GraphNodes.ValuePart(() => Owner(shot.Empire), false),
                        GraphNodes.ValuePart(() => ShotCountdown(shot), false),
                    },
                    // The sentence the game writes into the mote's tooltip, composed from the missile
                    // itself rather than read off the mote - so a shot the camera is not looking at
                    // still says where it is aimed. Same gate, same words, same arithmetic.
                    Sections = GraphNodes.Sections(() => ShotDetails(shot), null),
                };
                Follow(
                    vtable,
                    shot,
                    it == null ? null : it.AgeTransform,
                    it == null ? null : it.Tooltip
                );
                // Synthetic: a shot in flight is read out of the battle, not off any widget.
                builder.AddItem(Nodes.Synthetic(ProjectileId(shot), vtable));
            }
        }

        /// <summary>
        /// How many turns a missile has left, by the game's own arithmetic
        /// (<c>ObliteratorProjectileLabel.Refresh</c>): the distance still to fly over its speed,
        /// rounded UP, and 99 for a missile that is somehow not moving.
        /// </summary>
        private static int ShotTurns(ObliteratorProjectile shot)
        {
            double east = shot.GalaxyPosition.X - shot.Destination.GalaxyPosition.X;
            double north = shot.GalaxyPosition.Y - shot.Destination.GalaxyPosition.Y;
            double away = Math.Sqrt((east * east) + (north * north));
            float speed = shot.Speed;
            return speed > 0f ? (int)Math.Ceiling(away / speed) : 99;
        }

        /// <summary>The countdown the map draws beside a missile - for the player's OWN missile alone,
        /// which is the game's own choice about what an empire may know.</summary>
        private static string ShotCountdown(ObliteratorProjectile shot)
        {
            try
            {
                return ReferenceEquals(shot.Empire, Gui.PlayerEmpire)
                    ? AgeText.Clean(ShotTurns(shot) + "[turn]")
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the game says about a missile in its tooltip: which system it is aimed at and
        /// how long it has. Written for the player's own missile only - somebody else's is a thing on
        /// the map with no destination attached, and inventing one from the model would tell the
        /// player something the game is deliberately not showing.</summary>
        private static IList<string> ShotDetails(ObliteratorProjectile shot)
        {
            try
            {
                if (!ReferenceEquals(shot.Empire, Gui.PlayerEmpire))
                {
                    return null;
                }

                string said = AgeText.Clean(
                    Gui.Localize(
                        "%ObliteratorProjectileLabelDescription",
                        ShotTurns(shot),
                        shot.Destination.LocalizedName
                    )
                );
                return string.IsNullOrEmpty(said) ? null : new List<string> { said };
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a missile's destination threw: " + e);
                return null;
            }
        }

        /// <summary>Where a missile's row stands in the tree - out in open space, where the map draws
        /// it. Built here rather than spelled out at each caller so the scanner's jump and the tree's
        /// declaration cannot drift apart.</summary>
        internal static ControlId ProjectileId(ObliteratorProjectile shot)
        {
            return PlacedRows.Anchor(shot, "galaxy:projectile/" + shot.GUID);
        }

        /// <summary>Where a constellation's GROUP node stands in the tree - built here, beside the one
        /// place that declares it (<see cref="AddConstellation"/>), so the scanner's jump and the
        /// declaration cannot drift apart.</summary>
        internal static ControlId ConstellationId(Constellation constellation)
        {
            return ControlId.For(constellation, ConstellationKey + constellation.GUID);
        }

        /// <summary>The same, for an ally's pin.</summary>
        internal static ControlId PinId(CoordinationRequest request)
        {
            return PlacedRows.Anchor(request, PinKey(request));
        }

        private static string PinKey(CoordinationRequest request)
        {
            return "galaxy:pin/" + request.GUID;
        }

        /// <summary>
        /// The pins allies drop on the map to say "attack here", "defend this", "look at that".
        ///
        /// Only ever drawn in a game with allies in it, so nothing here can be tried outside one; it is
        /// modelled from what the game draws (<c>CoordinationRequestLabel</c>) and kept to that. Each
        /// pin says what KIND of request it is - the game's own word for the type - and reads back the
        /// message its owner typed on it, with the sentence naming the sender reviewable underneath.
        /// Letting go of one is the button the game draws on it, as a child, and only while it draws it.
        ///
        /// Editing the message is NOT offered: the game puts a live text field on the pin, and the mod's
        /// text editing is a screen of its own rather than a node on a map.
        /// </summary>
        private void AddPins(GraphBuilder builder)
        {
            for (int i = 0; i < _sighted.Count; i++)
            {
                CoordinationRequestLabel it = _sighted[i].Label;
                CoordinationRequest request = _sighted[i].Request;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => PinKind(request)),
                        // A pin is named by its KIND - "attack here" - so the pair is the only thing
                        // that says WHERE here is.
                        GalaxyCoordinates.Part(() => request.GalaxyPosition),
                        // The words on the pin come off the REQUEST, which is where the label reads
                        // them from too (<c>CoordinationRequestLabel.Refresh</c> assigns
                        // <c>CoordinationRequest.Message</c> into its field every refresh) - so the
                        // message is said whether or not the camera is drawing the pin, and is never
                        // the truncated form the field may have fitted to its box.
                        GraphNodes.ValuePart(() => AgeText.Clean(request.Message), false),
                    },
                    Sections = GraphNodes.Sections(() => PinDetails(request), null),
                };
                Follow(
                    vtable,
                    request,
                    it == null ? null : it.AgeTransform,
                    it == null ? null : it.RequestTooltip
                );

                string key = PinKey(request);
                ControlId id = PinId(request);
                vtable.ControlType = ControlTypes.Group;
                // Synthetic: an ally's pinned request is read out of the coordination model.
                builder.BeginGroup(Nodes.Synthetic(id, vtable));
                if (builder.IsExpanded(id))
                {
                    CoordinationRequest dismissed = request;
                    // Synthetic: mod-authored - the command that puts one of those requests away.
                    builder.AddItem(Nodes.Synthetic(
                        ControlId.Structural(key + "/dismiss"),
                        GraphNodes.Button(
                            () => PinDismissName(dismissed),
                            () => DismissPin(dismissed)
                        )
                    ));
                }

                builder.EndGroup();
            }
        }

        /// <summary>
        /// What the game explains a pin with, in its own two sentences: what this KIND of request
        /// means, and who sent it.
        ///
        /// Both are written into the label's tooltips as plain localized text
        /// (<c>CoordinationRequestLabel.SetTooltips</c>) from nothing but the request and its owner,
        /// so both are recomposed here rather than read off a tooltip that only exists while the map
        /// is drawing the pin. The sender sentence has two forms and the game picks by ownership -
        /// yours says what the pin is for, somebody else's names them and their faction.
        /// </summary>
        private static IList<string> PinDetails(CoordinationRequest request)
        {
            List<string> lines = new List<string>(2);
            try
            {
                AddLine(
                    lines,
                    AgeText.Clean(
                        Gui.Localize(
                            "%CoordinationTools" + request.RequestType
                                + "CoordinationRequestTooltip"
                        )
                    )
                );

                if (Mine(request))
                {
                    AddLine(
                        lines,
                        AgeText.Clean(
                            Gui.Localize("%CoordinationToolsSenderCoordinationRequestTooltip")
                        )
                    );
                    return lines;
                }

                GuiEmpire sender =
                    Gui.GuiWrapperProviderService.GetGuiEmpire(request.OwnerEmpire);
                string named = sender.LocalizedName
                    + " ("
                    + sender.GuiFaction.GetSymbolString(false)
                    + sender.GuiFaction.LocalizedName
                    + ")";
                AddLine(
                    lines,
                    AgeText.Clean(
                        Gui.Localize(
                            "%CoordinationToolsReceiverCoordinationRequestTooltip",
                            named
                        )
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a pin's sentences threw: " + e);
            }

            return lines;
        }

        /// <summary>Whether this pin is the player's own, which is what decides both the words the
        /// game explains it with and what letting go of it DOES.</summary>
        private static bool Mine(CoordinationRequest request)
        {
            return ReferenceEquals(Gui.PlayerEmpire, request.OwnerEmpire);
        }

        /// <summary>What the game calls the button that lets go of a pin - a different sentence for
        /// your own pin (which is deleted for everyone) and somebody else's (which is only hidden from
        /// you), because they are different acts.</summary>
        private static string PinDismissName(CoordinationRequest request)
        {
            try
            {
                return AgeText.Clean(
                    Gui.Localize(
                        Mine(request)
                            ? "%CoordinationToolsSenderDismissCoordinationRequestTooltip"
                            : "%CoordinationToolsReceiverDismissCoordinationRequestTooltip"
                    )
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Let go of a pin, by the game's own two routes rather than by pressing its button
        /// (<c>CoordinationRequestLabel.OnDismissCb</c>): your OWN pin is removed for everybody with
        /// the order that does that, and somebody else's is force-hidden for you alone and its
        /// visibility recomputed. The label's own <c>Hide()</c> is not replayed - the request raises
        /// <c>VisibilityChanged</c> and any label that exists hides itself off that, which is also
        /// what makes this work with no label at all.
        /// </summary>
        private static void DismissPin(CoordinationRequest request)
        {
            try
            {
                if (Mine(request))
                {
                    Gui.GetActivePlayerController()
                        .PostOrder(new OrderRemoveCoordinationRequest(request));
                    return;
                }

                request.SetForceHidden(true);
                request.UpdateVisiblity(Gui.PlayerEmpire);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: letting go of a pin threw: " + e);
            }
        }

        /// <summary>What the game calls this kind of request, in its own words.</summary>
        internal static string PinKind(CoordinationRequest request)
        {
            try
            {
                return AgeText.Clean(
                    Gui.Localize(
                        "%CoordinationTools" + request.RequestType + "CoordinationRequestTitle"
                    )
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The words on the pin, as its own field is drawing them.</summary>
        private static string PinMessage(CoordinationRequestLabel label)
        {
            try
            {
                AgeControlTextField field = label.TextField;
                return field == null || !Visible(field.AgeTransform)
                    ? null
                    : AgeText.Label(field.Label);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The turns a thing in flight has left, but only where the map is drawing the number:
        /// the game hides that background for anything that is not the player's own, and a hidden label
        /// still holds whatever was written on it for the last thing that needed it.</summary>
        private static string Countdown(AgeTransform background, AgePrimitiveLabel label)
        {
            return Visible(background) ? AgeText.Label(label) : null;
        }

        /// <summary>Point at the thing the map draws out between the stars, so the game shows its dossier
        /// where it can be read. The camera is not asked for here: a drifting row IS a place to the
        /// page's one camera rule, which slides across to it exactly as it does to a star
        /// (<see cref="OnFocusVisual"/>).</summary>
        private static void Follow(
            NodeVtable vtable,
            IGameEntityWithGalaxyPosition entity,
            AgeTransform widget,
            AgeTooltip tooltip
        )
        {
            AgeTransform anchor = widget;
            AgeTooltip tip = tooltip;
            vtable.OnFocusVisual = () =>
            {
                if (anchor != null)
                {
                    PointerFocus.MoveTo(null, tip, anchor);
                }
            };
            vtable.OnBlurVisual = ReleasePointer;
        }
    }
}

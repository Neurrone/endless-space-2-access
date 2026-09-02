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
    /// <summary>A star as a row - its name, whose it is, its dossier, its deposits, and everything the
    /// map draws about the place itself.</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>One place in a stretch of sky, as whichever row the map's own drawing of it allows:
        /// the full system row for a star the map is naming, and the bare one for a star it is only
        /// drawing (<see cref="AddLocated"/>).</summary>
        private void AddPlace(
            GraphBuilder builder,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel[] labels
        )
        {
            if (_located.Contains(node))
            {
                AddLocated(builder, node, empire);
                return;
            }

            AddSystem(builder, node, empire, _colonies.Contains(node), labels);
        }

        /// <summary>
        /// A place the map is drawing a star at and refusing to name.
        ///
        /// What the picture gives a player here is a generic body with an orbit ring round it and
        /// nothing else: no name, no real star type, no label, no dossier, and the mouse cannot even
        /// highlight it (<see cref="MapVisibility.Located"/>). So the row is the mod's own words for
        /// what it is, and then the one thing the picture really does say - WHERE it is. The system's
        /// real name is never spoken here and never indexed: the simulation knows it, the map is
        /// withholding it, and a row that leaked it would be handing the player something no sighted
        /// player can see. Two of these are told apart the way two of anything on this map are: by
        /// their coordinates.
        ///
        /// Nothing hangs under it except FLEETS, and those under exactly the gate every other fleet on
        /// the map passes (<see cref="AddFleets"/>) - because the one thing the game routinely shows at
        /// a place like this is somebody else's fleet parked there, which is often how the place came
        /// to be known at all. There is no branch at all where the map draws no fleet: an empty branch
        /// would be a row saying "there is more in here" over nothing.
        ///
        /// No planets, no lanes, no owner, no dossier and no management page: none of them is drawn,
        /// and the game refuses the click that would open them. Enter still brings the camera in, as it
        /// does on any place - the camera is the player's to point wherever they like, and what it
        /// finds there is the same generic star a mouse-driven player would fly to.
        ///
        /// Backslash is that zoom's other half and NOTHING else. On a system the map is naming it is
        /// two things - send the selection here, or come back out (<see cref="SystemCommand"/>) - and
        /// the first of them does not exist at a place like this: the mouse cannot so much as highlight
        /// the node, so there is no click a sighted player could give the order with, and offering one
        /// would be handing the keyboard a move the picture refuses. What is left is the way back out,
        /// which the row must keep: Enter zooms in, and nothing else on this page ever zooms out by
        /// itself.
        /// </summary>
        private void AddLocated(GraphBuilder builder, StarSystemNode node, Empire empire)
        {
            StarSystemNode it = node;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ModStrings.Get(ModStrings.GalaxySystemUnexplored)),
                    GalaxyCoordinates.Part(node.GalaxyPosition),
                },
            };

            // What is parked here, in the same count phrase every other place on the map uses - so the
            // number the row says and the children it opens onto stay the same answer read two ways.
            IList<Fleet> fleets = _showsFleets ? FleetPresence.FleetsAt(node) : NoFleets;
            if (fleets.Count > 0)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetPresence.At(it), false));
            }

            // The game's own left click: the camera comes in, and nothing is selected or opened.
            vtable.OnActivate = () => ZoomIn(it);
            vtable.OnContextual = () => ZoomOut(it);

            string place = SystemKey(node, empire);
            ControlId id = ControlId.For(it, place);
            if (fleets.Count == 0)
            {
                // Synthetic: a system is a place in the galaxy model, and the walk that listed it is what says it is there.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            // A container from here down, and said to be one exactly as every other place on the map
            // that opens onto what is standing in it.
            vtable.ControlType = ControlTypes.Group;
            HashSet<ControlId> expansion = builder.Expansion;
            ControlId group = id;
            vtable.OnCollapse = () => Collapse(expansion, group, it);
            // Synthetic for the same reason as the leaf above.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                AddFleets(builder, place, fleets);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// One system on the map: what it is called, whether it is yours, and - once opened - what the
        /// map draws inside its label.
        ///
        /// Enter is the game's own left click on a system: it brings the camera all the way in, to the
        /// step at which the map stops drawing circles and draws a card in orbit for every planet.
        /// Backslash is the right click: with fleets selected it sends them here, and with none it puts
        /// the camera back where the zoom took it from.
        ///
        /// The page a colony of yours has of its own is on neither key. The map draws a door for it on
        /// the system's own label and that door is a node here - so the player reaches it the way a
        /// mouse does, by going to the thing that opens it. Which of the label's two doors is the node
        /// is <see cref="AddManagementDoor"/>'s question, and the answer is always exactly one.
        /// </summary>
        private void AddSystem(
            GraphBuilder builder,
            StarSystemNode node,
            Empire empire,
            bool owned,
            StarSystemLabel[] labels
        )
        {
            StarSystemNode it = node;
            StarSystemLabel label = LabelFor(node, labels);
            StarSystemLabel drawn = label;
            NodeVtable vtable = GraphNodes.Group(() => it.LocalizedName);
            // Where on the map it is, straight after its name and before anything it happens to be
            // today - the pair is part of what the place is CALLED for a player steering by it
            // (<see cref="GalaxyCoordinates"/>). Taken once here rather than read per frame: a node's
            // position is fixed at galaxy generation.
            vtable.Announcements.Add(GalaxyCoordinates.Part(node.GalaxyPosition));
            // Then whose place it is - the one thing about a system a player scanning the map wants
            // before anything else, and the map draws it only as the colour it tints the name in.
            // The game's own word for the owner, its own word for a place with nobody on it, and
            // its own word for a home system (<see cref="SystemOwner"/>). Nothing at all for a
            // system of the player's, which is what "no word" has always meant on this map. Not
            // watched: ownership changes at the turn's end and the game raises its own notification
            // for it, and the answer costs a walk of the colonies standing at the node.
            Empire looking = empire;
            vtable.Announcements.Add(GraphNodes.ValuePart(() => SystemOwner(it, looking), false));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => HomeSystemWord(it, looking), false));
            // What is parked here, then everything the map writes on the label itself - the icons it
            // flanks the name with, what is being built, what is in the ground - and last the dossier
            // behind the star. The middle one is a page of detail drawn as pictures, so it is reviewed
            // rather than spoken (<see cref="SystemLabelReadout"/>).
            vtable.Sections = GraphNodes.Sections(
                // First while an order is waiting for a target, because it is what the player is here
                // to read: the game answers a mouse hovering a target with the shot's own consequences
                // and its refusals, and Enter on this node is that click
                // (<see cref="CursorTargeting.PreviewLines"/>). Silent the rest of the time, which is
                // almost always.
                NodeSection.Buffer(() => CursorTargeting.PreviewLines(it)),
                NodeSection.Buffer(() => FleetPresence.LinesAt(it)),
                // What sending the selection here would mean, turn by turn - nothing at all while no
                // fleet is selected, which is most of the time (<see cref="FleetRoute"/>).
                NodeSection.Buffer(() => FleetRoute.PreviewLines(it)),
                // How many live there. It left the SPOKEN readout when the system's own dossier became
                // a node of its own - the figure is a line of that dossier, and saying it again on the
                // way past every system on the map is the same number twice - but it stays in the
                // buffer, which is where a player reads a place they are considering rather than one
                // they are passing (owner-ruled).
                NodeSection.Buffer(() => Line(SystemLabelReadout.Population(drawn))),
                NodeSection.Buffer(() => SystemLabelReadout.Lines(drawn)),
                // What the map draws AT the place rather than on its label: how far this colony's own
                // influence reaches, the ring round a held node, the disk of a time bubble, the pins a
                // quest has planted. All four are colour and shape with no words anywhere near them.
                // The reach is reviewed rather than spoken because it is a number to plan a colony
                // with, not news - whose influence has WON the place is the spoken half
                // (<see cref="SystemInfluence"/>).
                NodeSection.Buffer(() => SystemInfluence.RadiusLines(it, empire)),
                NodeSection.Buffer(() => GuardLines(it, empire)),
                NodeSection.Buffer(() => TimeBubbleLines(it, empire)),
                NodeSection.Buffer(() => QuestMarkerLines(it, empire)),
                StarDossier(it, empire, drawn)
            );
            // What the place IS, where it is not a star system at all. Said first, because it is the
            // thing a sighted player takes in without asking: the map gives a special node a body of
            // its own (<see cref="SpecialKind"/>) while its name is a bare catalogue number that
            // gives nothing away. Not watched - a node cannot become a different phenomenon.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => SpecialKind(it), false));

            // Then whose influence is standing over the place, and who else is reaching for it - said
            // as soon as the row has finished saying what and where the place is, because between them
            // they answer "can I have this?": a system under somebody else's influence refuses a colony
            // ship and can change hands on its own, and the contest is the warning that it is about to
            // (<see cref="SystemInfluence"/>). Nothing at all for the ordinary case, a place inside its
            // own empire's circle. Not watched: influence moves at the turn's end and the game raises
            // its own notification when a system is converted, so there is nothing here for a standing
            // cursor to interrupt itself over.
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => SystemInfluence.UnderInfluence(it, empire), false)
            );
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => SystemInfluence.Contested(it, empire), false)
            );

            if (owned)
            {
                // A system of yours is either a colony or still an OUTPOST, and the map draws the two
                // differently - so they say different words rather than both saying "colonized".
                Empire owner = empire;
                vtable.Announcements.Add(GraphNodes.ValuePart(() => OwnedState(it, owner)));
            }

            // How many of the population are the player's own agents (drawn only where there is one).
            // Everything else the label says is a page of pictures and is reviewed, not spoken. Not
            // watched - it is read off a widget the map pools and re-points at other systems as the
            // camera moves.
            //
            // The population COUNT is no longer said here: the figure is one line of the system's own
            // dossier, and that dossier is a node of its own now (<see cref="TooltipChildren"/>), so
            // saying it in the readout as well would put the same number in front of the player twice
            // on the way past every system on the map - owner-ruled.
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => SystemLabelReadout.Sleepers(drawn), false)
            );

            // Whether somebody is fighting for the ground here. Spoken rather than reviewed: it is the
            // one thing on a system that changes who owns the place within a turn or two, and the map
            // says it in front of the player with an icon beside the name. Not watched - the answer
            // costs a repository lookup, and it cannot change under a standing cursor.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => GroundBattle(it, empire), false));

            // What the map draws parked here, in the game's own count phrase. Not watched: the answer
            // costs a walk of the docking-slot repository, and a watched part walks it every frame the
            // system is focused.
            //
            // Silent from a band the map draws no fleet at: the count and the children the branch
            // opens onto are one answer read two ways, so a number said where no row can be walked to
            // would be the row promising something the picture is not showing.
            if (_showsFleets)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetPresence.At(it), false));

                // ...and how many are under way on the lanes leaving here, which is the second half of
                // the same answer: the branch opens onto both sets of fleets, so the count has to name
                // both or the number the player was told and the children they walk stop matching.
                // Worked out from the same lane list the branch is built from
                // (<see cref="LanesOf"/>), so "nearby" means exactly the lanes this system offers. Not
                // watched, for the reason above and one more: it walks the visible-fleet repository
                // once per lane.
                Empire counting = empire;
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(() => UnderWayNearby(it, counting), false)
                );
            }

            // And what it would cost to send the selection here - the picture the map draws for a mouse
            // hovering over this system, in words. Silent while nothing is selected. Emphatically not
            // watched: the answer is a pathfinding search (<see cref="FleetRoute"/>).
            vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetRoute.Preview(it), false));

            // Last of everything: the player's own note about this place
            // (<see cref="BookmarkWord"/>). It is not a fact about the system, so it comes after
            // every answer the map itself is giving about one.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => BookmarkWord(it), false));

            // The two clicks the map itself puts on a system, and nothing invented on top of them.
            vtable.OnActivate = () => ZoomIn(it);
            vtable.OnContextual = () => SystemCommand(it);
            MoveHints(vtable);

            // The camera is not moved here: it follows the cursor by the page's one rule, which reads
            // this row as the system itself and slides the camera across to it
            // (<see cref="OnFocusVisual"/>).
            //
            // Once the camera is all the way in, the map pushes the system's own label off the top of
            // the screen and draws a tooltip anchor on the star instead - so that is what the pointer
            // is put on, or a tooltip meant for the system would be drawn where nobody can see it.
            // Unless that anchor's card is the THIN one the game binds for a system it does not own
            // (<see cref="OrbitalStarDossier"/>), in which case the fuller card wins and is drawn
            // wherever it hangs: what the row says about a system must not change with the zoom.
            //
            // Asked at the moment of aiming, through the same rule that decided what the row DECLARES
            // (<see cref="StarAim"/>): the answer depends on where the camera is and the camera moves
            // while the cursor stands still, and the orbital window's star tooltip is ONE widget it
            // re-points at whatever the camera is looking at. A widget resolved when the row was built
            // is a widget the game may have given to another system by the time the player arrives -
            // which is how a system came to be described by its neighbour's dossier.
            Empire aiming = empire;
            // Declared as well as performed: the navigator re-commits a standing cursor's pointer when
            // this answer changes, which is the same question the visual below asks.
            vtable.PointsAt = () => StarAim(it, aiming, LabelFor(it, SystemLabels()));
            vtable.OnFocusVisual = () =>
            {
                StarSystemLabel drawing = LabelFor(it, SystemLabels());
                AgeTooltip star = StarAim(it, aiming, drawing);
                if (star == null)
                {
                    return;
                }

                // The label's own tooltip is drawn under the WHOLE label rather than under the star
                // inside it; the orbital window's and the mod's own carrier stand where they are.
                bool onTheLabel =
                    drawing != null && ReferenceEquals(star, drawing.StarTooltip);
                PointerFocus.MoveTo(
                    null,
                    star,
                    onTheLabel ? drawing.AgeTransform : star.AgeTransform
                );
            };
            vtable.OnBlurVisual = ReleasePointer;

            // Right means "tell me what is inside this", and what is inside it is whatever the map is
            // drawing there: the circles when the camera is out, the orbital cards when it is in...
            string place = SystemKey(node, empire);
            ControlId id = ControlId.For(it, place);
            // ...and opening one no longer moves the camera itself: Right opens the branch AND steps
            // inside it, and the first child's own focus is what brings the camera in, through the one
            // rule (<see cref="OnFocusVisual"/>). So expansion is left to the engine and only the
            // CLOSING is an override, because coming back out is a camera move nothing else makes.
            HashSet<ControlId> expansion = builder.Expansion;
            ControlId group = id;
            vtable.OnCollapse = () => Collapse(expansion, group, it);
            // Synthetic: a place on the map, assembled from the galaxy model rather than drawn as one thing.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            // Only what is open costs anything: a galaxy of closed systems declares one node each.
            if (builder.IsExpanded(id))
            {
                AddInside(builder, place, node, empire, label);
            }

            builder.EndGroup();
        }

        /// <summary>What is parked at a place from a band the map draws no fleet lozenges at.</summary>
        private static readonly Fleet[] NoFleets = new Fleet[0];

        /// <summary>One line as a buffer section's list, or nothing where there is no line.</summary>
        private static IList<string> Line(string text)
        {
            return string.IsNullOrEmpty(text) ? null : new string[] { text };
        }

        /// <summary>
        /// The dossiers the map hangs on a system beyond the ones its children already carry: the
        /// system's own stat block, and one per kind of deposit found in the ground.
        ///
        /// The star, the name and the population count all carry the SAME dossier - one wrapper, three
        /// widgets (measured on Osulo: identical <c>GuiStarSystem</c> target on all three) - so it is
        /// one node, named the way the game's own header names it ("Osulo - Niris"). Which of the two
        /// star tooltips is asked for is <see cref="StarDossier"/>'s rule: the map keeps one on the
        /// label and another over the star once the camera is in, and only the one being drawn has any
        /// words at all.
        ///
        /// Everything else on the label that carries a dossier is already a node here - the planets,
        /// the fleet lozenges, the diplomacy button - so none of them is declared twice.
        /// </summary>
        private static List<TooltipChildren.Dossier> SystemDossiers(
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(4);
            try
            {
                StarSystemNode it = node;
                Empire looking = empire;
                StarSystemLabel drawn = label;
                AgeTooltip star = StarAim(node, empire, label);
                TooltipChildren.Add(
                    found,
                    star,
                    star == null ? null : star.AgeTransform,
                    () => StarDossierLines(it, looking, drawn),
                    // The words were always asked for afresh; the AIM and the header line are asked
                    // the same way now, or the node reads a system the camera has moved on from.
                    () => StarAim(it, looking, LabelFor(it, SystemLabels()))
                );
                // The stat block behind the star is what the PLACE is, so it leads the "Details"
                // region - the first thing the player reaches asking what this system is.
                SystemLabelReadout.In(found, 0, SystemLabelReadout.Region.Details);
                // Then every picture the label is drawing, in its own order, with the deposits back in
                // the place the label draws them (<see cref="SystemLabelReadout.IconsAboveDeposits"/>).
                // Each stamps the region of the row it belongs in as it goes, and the emit reads them
                // back region by region while keying every node by its place in THIS list.
                SystemLabelReadout.IconsAboveDeposits(found, label);
                AddDeposits(found, node, empire, label);
                SystemLabelReadout.IconsBelowDeposits(found, label);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's dossiers threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// One dossier per KIND of deposit in this system's ground, read off the planets rather than
        /// off the icons the label happens to be drawing.
        ///
        /// The label draws its deposit strip only from a close enough camera, so taking the list from
        /// the strip made a system's deposits reachable at one zoom and gone at another - for content
        /// the map is not withholding at all (the fog gates are the planets': everything here is under
        /// <c>MapVisibility.Perceived</c> and the branch's own expansion). The list is built exactly
        /// as <c>StarSystemLabel.RefreshDepositsLine</c> builds it - every planet's deposits in orbit
        /// order, deduped by definition name - so the order the player walks is the order the icons
        /// are drawn in.
        ///
        /// The AIM still prefers the game's own icon wherever the game is drawing one (owner ruling
        /// 2026-08-23), so a sighted player sees the tooltip appear over the deposit it belongs to;
        /// a carrier of the mod's own stands in only where there is no icon on the screen, and the
        /// words are the same either way because the tooltip window assembles them from the wrapper.
        /// A drawn item is matched to the definition it is BOUND to rather than taken by position,
        /// which is also what stops a stale binding on a culled-out label being read.
        /// </summary>
        private static void AddDeposits(
            List<TooltipChildren.Dossier> found,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            ColonizedStarSystem colony = LabelColony(node, empire);
            Empire owner = colony == null ? null : colony.Empire;
            List<ResourceDepositDefinition> kinds = DepositKinds(node);
            StarSystemNode it = node;
            Empire looking = empire;
            for (int i = 0; i < kinds.Count; i++)
            {
                ResourceDepositDefinition definition = kinds[i];
                ResourceDepositDefinition kind = definition;
                AgeTooltip tooltip = DepositAim(node, definition, label, owner);
                int at = found.Count;
                TooltipChildren.Add(
                    found,
                    tooltip,
                    tooltip == null ? null : tooltip.AgeTransform,
                    null,
                    // The label's deposit strip is drawn only from close enough and its items are
                    // pooled among the deposits the label is showing, so which widget carries a kind
                    // is a question about the camera - asked again every time the pointer is aimed
                    // rather than once when the node was declared.
                    () =>
                        DepositAim(
                            it,
                            kind,
                            LabelFor(it, SystemLabels()),
                            DepositOwner(it, looking)
                        )
                );
                if (found.Count > at)
                {
                    ExploitedName(found, at, it, kind);
                    SystemLabelReadout.In(found, at, SystemLabelReadout.Region.Resources);
                }
            }
        }

        /// <summary>Put the state the label paints a deposit's picture in onto that deposit's own node -
        /// exploited or idle, read off the drawn icon at every read, because whether the map is drawing
        /// one at all is a question about where the camera is
        /// (<see cref="SystemLabelReadout.DepositName"/>). The naming ladder underneath is kept: it is
        /// what a sibling entry reads to find out whether the two answer to the same word.</summary>
        private static void ExploitedName(
            List<TooltipChildren.Dossier> found,
            int at,
            StarSystemNode node,
            ResourceDepositDefinition definition
        )
        {
            TooltipChildren.Dossier entry = found[at];
            Func<string> named = entry.Name;
            StarSystemNode it = node;
            ResourceDepositDefinition kind = definition;
            entry.Name = () =>
                SystemLabelReadout.DepositName(
                    named(),
                    DrawnDeposit(LabelFor(it, SystemLabels()), kind)
                );
            found[at] = entry;
        }

        /// <summary>The widget a kind of deposit's dossier is drawn through right now: the label's own
        /// icon wherever the map is drawing one for it, else a carrier of the mod's.</summary>
        private static AgeTooltip DepositAim(
            StarSystemNode node,
            ResourceDepositDefinition definition,
            StarSystemLabel label,
            Empire owner
        )
        {
            bool drawing = label != null && AgeWidgets.Painted(label.AgeTransform);
            AgeTooltip icon = drawing ? DrawnDeposit(label, definition) : null;
            return icon ?? DepositCarrier(node, definition, owner);
        }

        /// <summary>Whose colony the deposits are being read under, which is what a carrier is stamped
        /// with.</summary>
        private static Empire DepositOwner(StarSystemNode node, Empire empire)
        {
            ColonizedStarSystem colony = LabelColony(node, empire);
            return colony == null ? null : colony.Empire;
        }

        /// <summary>Every kind of deposit in a system's ground, in the order the label's strip draws
        /// them: planet by planet, deposit by deposit, one entry per definition NAME
        /// (<c>StarSystemLabel.RefreshDepositsLine</c>).</summary>
        private static List<ResourceDepositDefinition> DepositKinds(StarSystemNode node)
        {
            List<ResourceDepositDefinition> kinds = new List<ResourceDepositDefinition>(4);
            try
            {
                for (int i = 0; i < node.Planets.Count; i++)
                {
                    Planet planet = node.Planets[i];
                    for (int j = 0; j < planet.ResourceDeposits.Count; j++)
                    {
                        ResourceDepositDefinition definition = planet.ResourceDeposits[j].Definition;
                        if (definition == null || Holds(kinds, definition))
                        {
                            continue;
                        }

                        kinds.Add(definition);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: listing a system's deposits threw: " + e);
            }

            return kinds;
        }

        private static bool Holds(
            List<ResourceDepositDefinition> kinds,
            ResourceDepositDefinition definition
        )
        {
            for (int i = 0; i < kinds.Count; i++)
            {
                if (kinds[i].Name == definition.Name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The label's own icon for one kind of deposit, where it is drawing one. Found by
        /// what the icon is BOUND to, never by position: an icon the label bound for another system
        /// and has not refreshed since answers no.</summary>
        private static AgeTooltip DrawnDeposit(
            StarSystemLabel label,
            ResourceDepositDefinition definition
        )
        {
            if (label == null)
            {
                return null;
            }

            AgeTooltip found = DrawnDeposit(label.DepositsMainTable, definition);
            return found ?? DrawnDeposit(label.DepositsSecondaryTable, definition);
        }

        private static AgeTooltip DrawnDeposit(
            AgeTransform table,
            ResourceDepositDefinition definition
        )
        {
            if (!Visible(table))
            {
                return null;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                // Content: which icon carries this deposit's sentence. The table pools its items, and a
                // retired one is faded rather than hidden while it still holds the last binding.
                if (!AgeWidgets.Painted(item))
                {
                    continue;
                }

                AgeTooltip tooltip = Raw(item);
                GuiResourceDepositGroup group =
                    tooltip == null ? null : tooltip.Target as GuiResourceDepositGroup;
                if (group != null && group.Definition != null
                    && group.Definition.Name == definition.Name)
                {
                    return tooltip;
                }
            }

            return null;
        }

        /// <summary>A carrier of the mod's own bound exactly as <c>StarSystemLabelDepositItem.Bind</c>
        /// binds the game's icon - the same class, the same wrapper, the same refusal text - so the
        /// tooltip window assembles the same panel for it.</summary>
        private static AgeTooltip DepositCarrier(
            StarSystemNode node,
            ResourceDepositDefinition definition,
            Empire owner
        )
        {
            try
            {
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "deposit/" + node.GUID + "/" + definition.Name,
                    DossierStamp(owner),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiResourceDepositGroup group = new GuiResourceDepositGroup(
                        node,
                        definition,
                        owner
                    );
                    List<FailureInfo> refusals = new List<FailureInfo>();
                    group.IsExploited(PlayerEmpire(), refusals);
                    carrier.Class = group.TooltipClass;
                    carrier.Content = Gui.FormatFailureInfos(refusals);
                    carrier.Context = null;
                    carrier.Target = group;
                }

                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: binding a deposit dossier threw: " + e);
                return null;
            }
        }

        /// <summary>What a dossier built from the simulation depends on: the turn it was read in and
        /// whose empire it was read for. Everything a deposit group or a star system counts - what is
        /// exploited, what the empire may exploit at all, who lives there - settles at the turn's end,
        /// and rebinding a carrier more often than that would restart the tooltip's own countdown
        /// every frame and it would never finish appearing.</summary>
        private static long DossierStamp(Empire owner)
        {
            try
            {
                Game game = Gui.Game;
                long stamp = game == null ? 0L : game.Turn * 1000003L;
                return (stamp * 31L) + (owner == null ? 0L : owner.Index + 1L);
            }
            catch (Exception)
            {
                return 0L;
            }
        }

        /// <summary>
        /// Closing a system's branch hands back the view the expansion took the camera away from
        /// (<see cref="CollapseZoom"/>) - one gesture, undone by its opposite, which is what makes going
        /// in and looking closer the same key in the first place. Where the expansion moved nothing -
        /// the far bands, where a system opens in place - there is nothing to give back and the camera
        /// stays where the player has it.
        ///
        /// Only while the camera is still looking at THIS system. Focus moves the camera about the map
        /// freely, so by the time a branch is closed the player may be reading somewhere else entirely,
        /// and flying the camera home from over there would move a view nobody asked about - so a collapse
        /// anywhere but here moves nothing at all.
        ///
        /// Silent, like the expansion: the engine says the group closed, and the camera going back out is
        /// what closing it MEANS rather than a second thing that happened. The bookkeeping is by hand
        /// because OnCollapse is an override - declaring it stops the engine flipping the state itself.
        /// </summary>
        private void Collapse(HashSet<ControlId> expansion, ControlId group, StarSystemNode node)
        {
            if (expansion != null)
            {
                expansion.Remove(group);
            }

            CollapseZoom(node);

            // Whether or not there was a zoom to undo, the branch is shut: the camera is no longer
            // reading the inside of this system, so opening it again brings the camera back in
            // (<see cref="LeftPlace"/>). Backslash deliberately does NOT do this - a zoom-out by hand
            // is the player choosing to go on reading the same place from further off.
            LeftPlace(node);
        }

        /// <summary>
        /// What the map draws inside a system, as the children of that system's ONE node - in SEVEN
        /// NAMED REGIONS (owner design 2026-09-02).
        ///
        /// One node, because travelling a lane rebases the cursor onto the destination's own node rather
        /// than declaring a copy of it (<see cref="AddStarlanes"/>). So there is no second way in whose
        /// contents could come to differ, no structural re-keying of everything underneath, and nothing
        /// here has to be made poorer than anything else.
        ///
        /// An opened system used to be one flat run of everything the map draws there - a door, four
        /// buttons, the planets, the lanes, three sets of fleets, the quest pins - followed by a block
        /// called "Tooltips" holding a dozen icon nodes in the label's own order, so the one thing a
        /// player wanted (is anything WRONG here? what can I DO here?) was somewhere in a list of
        /// thirty. The children are now sorted into regions the player can jump by name with
        /// Alt+Up/Down, in one fixed order at every distance and in the scan view too: <b>Status</b>
        /// (what is happening here now), <b>Actions</b> (the doors), <b>Planets</b>, <b>Star lanes</b>,
        /// <b>Fleets</b>, <b>Resources</b> (what is in the ground) and <b>Details</b> (what the place
        /// permanently is). Each is a context level, so its name is said once on the way in and the
        /// "N of M" the player hears counts that region alone.
        ///
        /// A region the map is drawing nothing for does not exist: a context with no children is not a
        /// node and a region nothing is tagged with is not a jump target, so the far bands - where the
        /// map draws a name on a coloured bar and nothing else - open onto Star lanes and Fleets and
        /// say nothing about the five they have no content for.
        ///
        /// The fleets are in THREE groups because the map draws them at three distances: what is parked
        /// here, then what is under way on the lanes leaving here - the latter under the end it is
        /// arriving at, each saying which lane it is on (<see cref="AddEnRoute"/>) - and last what is
        /// crossing the open space TOWARDS here with no lane to fly (<see cref="AddFreeMoving"/>).
        /// Both of the moving groups hang under the destination alone, and the hangars follow them
        /// because a hangar is where a fleet comes from.
        /// </summary>
        private void AddInside(
            GraphBuilder builder,
            string key,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            List<Lane> lanes = LanesOf(node, empire);
            // Collected ONCE for the whole branch, in the order the label draws them, and read four
            // times: each region takes the entries stamped with its own name and keys them by their
            // place in this one list, so sorting them into regions moves no node's key.
            List<TooltipChildren.Dossier> dossiers =
                _showsDetail ? SystemDossiers(node, empire, label) : null;
            object outer = builder.Region;
            try
            {
                if (_showsDetail)
                {
                    // What is happening at the system now: the label's own marks for it, then the quest
                    // pins - which are a thing about the QUEST standing here rather than about the
                    // system, and so come last of what is going on.
                    object at = Region(builder, key, "status", ModStrings.GalaxySystemStatusRegion);
                    try
                    {
                        TooltipChildren.EmitInto(
                            builder,
                            key,
                            dossiers,
                            SystemLabelReadout.Region.Status
                        );
                        AddQuestMarkers(builder, key, node, empire);
                    }
                    finally
                    {
                        TooltipChildren.EndRegion(builder, at);
                    }

                    // Every door the map draws at the place: the way into the system's own page first,
                    // then the label's buttons, the wrecks a mouse can salvage, and the bearings a
                    // probe can be sent on.
                    at = Region(builder, key, "actions", ModStrings.GalaxySystemActionsRegion);
                    try
                    {
                        AddManagementDoor(builder, key, node, label, dossiers);
                        AddLabelButtons(builder, key, label);
                        AddWrecks(builder, key, node);
                        AddProbeDirections(builder, key, node);
                    }
                    finally
                    {
                        TooltipChildren.EndRegion(builder, at);
                    }

                    at = Region(builder, key, "planets", ModStrings.GalaxySystemPlanetsRegion);
                    try
                    {
                        AddPlanets(builder, key, node, empire, label);
                    }
                    finally
                    {
                        TooltipChildren.EndRegion(builder, at);
                    }
                }

                // The lane network is drawn at every band the systems themselves are, so a system whose
                // band draws nothing else still opens onto the roads out of it - which is what makes the
                // far bands a way of reading the map's geometry rather than an emptier version of the
                // near ones.
                object lanesRegion = Region(
                    builder,
                    key,
                    "lanes",
                    ModStrings.GalaxySystemLanesRegion
                );
                try
                {
                    AddStarlanes(builder, key, node, empire, lanes);
                }
                finally
                {
                    TooltipChildren.EndRegion(builder, lanesRegion);
                }

                if (_showsFleets)
                {
                    object fleets = Region(
                        builder,
                        key,
                        "fleets",
                        ModStrings.GalaxySystemFleetsRegion
                    );
                    try
                    {
                        AddFleets(builder, key, FleetPresence.FleetsAt(node));
                        AddEnRoute(builder, key, EnRouteOn(node, lanes));
                        AddFreeMoving(builder, key, node, FreeMovingAt(node));
                        if (_showsDetail)
                        {
                            AddHangars(builder, key, node);
                        }
                    }
                    finally
                    {
                        TooltipChildren.EndRegion(builder, fleets);
                    }
                }

                if (_showsDetail)
                {
                    object at = Region(
                        builder,
                        key,
                        "resources",
                        ModStrings.GalaxySystemResourcesRegion
                    );
                    try
                    {
                        TooltipChildren.EmitInto(
                            builder,
                            key,
                            dossiers,
                            SystemLabelReadout.Region.Resources
                        );
                    }
                    finally
                    {
                        TooltipChildren.EndRegion(builder, at);
                    }

                    at = Region(builder, key, "details", ModStrings.GalaxySystemDetailsRegion);
                    try
                    {
                        TooltipChildren.EmitInto(
                            builder,
                            key,
                            dossiers,
                            SystemLabelReadout.Region.Details
                        );
                    }
                    finally
                    {
                        TooltipChildren.EndRegion(builder, at);
                    }
                }
            }
            finally
            {
                builder.SetRegion(outer);
            }
        }

        /// <summary>One of the system row's named regions, opened - the mod's own word for the block
        /// as a context level, and a structural path under the system's key for Alt+Up/Down to jump
        /// to.</summary>
        private static object Region(GraphBuilder builder, string key, string region, string name)
        {
            return TooltipChildren.BeginRegion(builder, key, region, ModStrings.Get(name));
        }

        /// <summary>
        /// THE SYSTEM'S ONE DOOR INTO ITS OWN PAGE, never two (owner ruling 2026-09-02).
        ///
        /// The map draws two of them on a colony of the player's that has something in its queue: the
        /// button beside the name, and the construction slot below it - and the slot's click is
        /// literally the name-line button's handler (<c>StarSystemLabel.OnRequestManagementView</c>
        /// :3002). So where the label is drawing the slot, the slot's own node IS the door: it is named
        /// by what the label writes around it ("Building Interplanetary Transport Network, 3 turns"),
        /// it carries the constructible's dossier, it points where a mouse would point, and it opens the
        /// page - and a bare "Manage system" beside it would be the same door said twice, in a phrase
        /// of the mod's that says less.
        ///
        /// Asked of the CLICK the walk found on that picture rather than of the picture being drawn,
        /// so that a slot the game ever draws without wiring one still leaves the row a way in.
        /// Everywhere the slot is not drawn at all - an outpost, a foreign colony we hold a traitor in
        /// - the name-line button is the door and is declared exactly as before
        /// (<see cref="AddManagementView"/>).
        /// </summary>
        private static void AddManagementDoor(
            GraphBuilder builder,
            string key,
            StarSystemNode node,
            StarSystemLabel label,
            IList<TooltipChildren.Dossier> dossiers
        )
        {
            TooltipChildren.EmitInto(builder, key, dossiers, SystemLabelReadout.Region.Actions);
            if (!Queued(dossiers))
            {
                AddManagementView(builder, key, node, label);
            }
        }

        /// <summary>Whether the label is drawing this system a construction slot the game wired a click
        /// on - the one thing that decides which of the two doors the row offers.</summary>
        private static bool Queued(IList<TooltipChildren.Dossier> dossiers)
        {
            for (int i = 0; dossiers != null && i < dossiers.Count; i++)
            {
                TooltipChildren.Dossier it = dossiers[i];
                if (
                    it.Clicks != null
                    && Equals(it.Region, SystemLabelReadout.Region.Actions)
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What a place on the map is, where it is one of the galaxy's phenomena rather than a star
        /// system - a solar nebula, a collapsing star, an asteroid field. Nothing at all for an
        /// ordinary system, which needs no telling: it is what the map is made of.
        ///
        /// A special node is a <c>StarSystemNode</c> with no planets and a body of its own drawn over
        /// the star (<c>GalaxySpecialNode.UpdateVisualAccordingToExploration</c>), and its name is a
        /// bare catalogue number - so a sighted player knows what they are looking at from the picture
        /// and a keyboard player was told nothing. The kind is only ever written down in the dossier
        /// behind the star, which is a tooltip the player has to go and read.
        ///
        /// The words are the game's own - the same expression the dossier's header draws
        /// (<c>GuiSpecialNode.CategoryTitle</c>), so this cannot drift from the line the buffer
        /// already carries, and there is nothing here to translate.
        /// </summary>
        internal static string SpecialKind(StarSystemNode node)
        {
            try
            {
                SpecialNode special = node as SpecialNode;
                SpecialNodeDefinition definition =
                    special == null ? null : special.SpecialNodeDefinition;
                return definition == null ? null : Gui.GetLocalizedTitle(definition.Name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whose place this is, in the game's own words.
        ///
        /// The map answers this with COLOUR - it tints the system's name in the owner's colour - and
        /// says it in words only inside the system's own dossier, whose header is "Osulo - Niris"
        /// (<c>GuiStarSystem.Title</c>). So the word here is the one that header uses,
        /// <c>GuiEmpire.GetLeaderName</c>, which is also what already answers "Unknown Empire" for an
        /// empire the player has not met and names a minor civilization per SYSTEM rather than by its
        /// one empire object.
        ///
        /// Nothing at all for a system of the player's own: "mine" is the unmarked case on this map,
        /// and the colonized/outpost word that follows already says it is held.
        ///
        /// Gated on the colonies the player can SEE, exactly as <c>SystemInfluence</c> gates its own
        /// naming (<c>Visibility >= 1</c>): a colony the map is hiding is not named, and the answer for
        /// a node with none the player can see is the game's own "No owner" - which is what the map is
        /// showing, whatever the simulation knows.
        /// </summary>
        private static string SystemOwner(StarSystemNode node, Empire empire)
        {
            try
            {
                if (node == null || empire == null || !MapVisibility.Perceived(node, empire))
                {
                    return null;
                }

                ColonizedStarSystem owner = VisibleColony(node, empire);
                if (owner == null)
                {
                    return AgeText.Clean(Gui.Localize(NoOwnerKey));
                }

                if (ReferenceEquals(owner.Empire, empire))
                {
                    return null;
                }

                GuiEmpire wrapper = Gui.GuiWrapperProviderService.GetGuiEmpire(owner.Empire);
                return wrapper == null
                    ? null
                    : AgeText.Clean(
                        wrapper.GetLeaderName(owner.GUID, empire, false, false, false)
                    );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's owner threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The game's own word for a home system, on any empire's.
        ///
        /// Said only where the player can see a colony standing here - the same gate the owner word
        /// uses, and the reason the fog gives nothing away: <c>HomeSystemEmpireIndex</c> is set on
        /// every home system in the galaxy from the moment it is generated, so reading it ungated
        /// would tell the player which unexplored star an empire they have never met came from.
        ///
        /// The map's own icon is narrower than this - it draws one only for a MAJOR empire's home
        /// system (<c>StarSystemLabel.RefreshHomeSystemLine</c> :2272) - so a minor civilization's
        /// home, which is the whole of that civilization, would be said nowhere. Owner-ruled to say
        /// it for any empire's.
        /// </summary>
        private static string HomeSystemWord(StarSystemNode node, Empire empire)
        {
            try
            {
                if (
                    node == null
                    || empire == null
                    || !node.IsHomeSystem
                    || !MapVisibility.Perceived(node, empire)
                    || VisibleColony(node, empire) == null
                )
                {
                    return null;
                }

                // The game's own key ends in a space, because it draws it in front of something else.
                return AgeText.Clean(Gui.Localize(HomeSystemKey)).Trim();
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading whether a system is a home system threw: " + e);
                return null;
            }
        }

        private static readonly string NoOwnerKey = "%MarketplaceScreenNoOwnerTitle";

        private static readonly string HomeSystemKey = "%HomeSystemTitle";

        /// <summary>The colony standing at a node that the player is being SHOWN - the strongest claim
        /// the map is drawing there. A ghost is not one: an empire keeps a ghost of a system it has
        /// lost, and the map draws nothing for it.</summary>
        private static ColonizedStarSystem VisibleColony(StarSystemNode node, Empire empire)
        {
            IColonizedStarSystemRepositoryService colonies =
                Amplitude.Unity.Framework.Services.GetService<IColonizedStarSystemRepositoryService>();
            if (colonies == null)
            {
                return null;
            }

            ColonizedStarSystem found = null;
            foreach (ColonizedStarSystem colony in colonies.GetValues(node.NodePosition))
            {
                if (
                    colony.Empire == null
                    || colony.State == StarSystemState.Ghost
                    || (int)colony.Visibility[empire] < 1
                )
                {
                    continue;
                }

                if (ReferenceEquals(colony.Empire, empire))
                {
                    return colony;
                }

                if (found == null)
                {
                    found = colony;
                }
            }

            return found;
        }

        /// <summary>
        /// The colony a system's map LABEL binds its dossiers with -
        /// <c>StarSystemLabel.RebuildColonizedStarSystemsList</c>'s <c>MainColonizedStarSystem</c>,
        /// replicated so that a dossier the mod builds itself is named the way the label's is.
        ///
        /// Not <see cref="VisibleColony"/>: that one answers "what claim is drawn here" and counts an
        /// OUTPOST, while the label counts only a full colony - which is why Heka's dossier is called
        /// "Heka" and Osulo's "Osulo - Niris". Reading the wrong one made the same card read
        /// differently either side of a zoom, which is exactly what sourcing from data is for.
        /// </summary>
        private static ColonizedStarSystem LabelColony(StarSystemNode node, Empire empire)
        {
            IColonizedStarSystemRepositoryService colonies =
                Amplitude.Unity.Framework.Services.GetService<IColonizedStarSystemRepositoryService>();
            if (colonies == null)
            {
                return null;
            }

            ColonizedStarSystem found = null;
            foreach (ColonizedStarSystem colony in colonies.GetValues(node.NodePosition))
            {
                if (
                    (int)colony.Visibility[empire] >= 1
                    && (found == null || !ReferenceEquals(found.Empire, empire))
                    && colony.State == StarSystemState.Colony
                )
                {
                    found = colony;
                }
            }

            return found;
        }

        /// <summary>What a system of the player's IS - taken from the state the game paints its label
        /// from, so the word and the picture always agree. Anything other than an outpost is the colony
        /// the word "colonized" has always meant.</summary>
        private static string OwnedState(StarSystemNode node, Empire empire)
        {
            try
            {
                return ModStrings.Get(
                    IsOutpost(node, empire)
                        ? ModStrings.GalaxySystemOutpost
                        : ModStrings.GalaxySystemColonized
                );
            }
            catch (Exception)
            {
                return ModStrings.Get(ModStrings.GalaxySystemColonized);
            }
        }

        /// <summary>Whether what this empire holds here is still an outpost. Read off the same list the
        /// stop was built from - an empire can hold a colony and a GHOST of one in the same place, and
        /// the ghost is not what the map's label is showing.</summary>
        private static bool IsOutpost(StarSystemNode node, Empire empire)
        {
            DepartmentOfTheInterior interior =
                empire == null ? null : empire.GetAgency<DepartmentOfTheInterior>();
            if (interior == null)
            {
                return false;
            }

            IList<ColonizedStarSystem> systems = interior.ColonizedStarSystems;
            for (int i = 0; systems != null && i < systems.Count; i++)
            {
                ColonizedStarSystem system = systems[i];
                if (system != null && system.Node == node && system.State != StarSystemState.Ghost)
                {
                    return system.State == StarSystemState.Outpost;
                }
            }

            return false;
        }

        /// <summary>The game's own left click on a system: the camera comes all the way in, which is
        /// also what swaps the system's planets from circles to cards. Silent here - what the camera
        /// did is reported as the rung it landed on, from the one watcher that reports every zoom
        /// change however it was made (<see cref="ZoomWatch"/>), rather than by each key that causes
        /// one saying so in words of its own.
        ///
        /// Takes any node on the map rather than a system, because the map hangs lanes off things that
        /// are not systems and every one of them answers the same click.
        ///
        /// While the game has the map in one of its TARGETING modes - launch a probe, take this system,
        /// fire the obliterator - the same left click means "confirm the target here" instead of the
        /// zoom, for the mouse as much as for us (<see cref="CursorTargeting"/>), so the mode is asked
        /// first and the camera is left alone when it answers.</summary>
        private static void ZoomIn(GameNode node)
        {
            if (CursorTargeting.ConfirmAt(node))
            {
                return;
            }

            GalaxyViewLevels.ZoomTo(node);
        }

        /// <summary>
        /// What the move keys do on a place a fleet could be sent to, said at the end of the node's
        /// buffer while there is a selection to send.
        ///
        /// Two lines and the same action twice: the map's move click is the Contextual action, and its
        /// off-lane variant is that action's SECOND chord rather than a wiring of its own, because the
        /// game runs one handler for both clicks and reads the physical Control inside it
        /// (<see cref="ES2Access.UI.FleetOrders"/>). So the hints name the action and the chord index,
        /// and a rebind of either chord re-words its own line.
        ///
        /// The second line is gated on the selection really being able to fly off the lanes
        /// (<see cref="ES2Access.UI.FleetOrders.AnySelectedCanFreeMove"/>): naming a chord that can
        /// only ever be refused is worse than saying nothing.
        /// </summary>
        private static void MoveHints(NodeVtable vtable)
        {
            NodeHints.Add(
                vtable,
                ModStrings.HintMoveFleetHere,
                UiActions.Contextual,
                0,
                FleetOrders.AnySelected
            );
            NodeHints.Add(
                vtable,
                ModStrings.HintFreeMovement,
                UiActions.Contextual,
                1,
                FleetOrders.AnySelectedCanFreeMove
            );
        }

        /// <summary>
        /// The map's own right click on a system, which is two things and never both: while the cursor
        /// is holding fleets it is where they are being sent, and while it is holding nothing it is the
        /// way back out of a zoom.
        ///
        /// Asked only when the key is pressed. Working out whether a fleet could get here is a
        /// pathfinding search per fleet, which is a thing to do on demand and never on a frame.
        /// </summary>
        private static void SystemCommand(StarSystemNode node)
        {
            List<Fleet> selected = FleetOrders.Selected();
            if (selected.Count > 0)
            {
                List<FailureInfo> refusals = new List<FailureInfo>();
                SendAll(SendableTo(node, selected, refusals), refusals);
                return;
            }

            // Nothing selected, nothing to unzoom: silent, like every other gesture key with nothing
            // to do here - and silent when it DOES move the camera too, because the rung it lands on
            // is announced by the watcher that reports every zoom change (<see cref="ZoomWatch"/>).
            ZoomOut(node);
        }

        /// <summary>
        /// Put the camera back out at the default view, still looking at this system. Nothing at all
        /// where the camera is already out - there is no zoom to undo.
        ///
        /// Says nothing itself, wherever it is reached from - the key on a system
        /// (<see cref="SystemCommand"/>) or closing a branch (<see cref="Collapse"/>). What the camera
        /// did is the rung it landed on, announced once by <see cref="ZoomWatch"/> however it moved.
        ///
        /// NOT the game's RestoreZoom, for the reason ZoomToStep's own doc comment records: the game
        /// restores the camera to wherever it stood BEFORE the click-zoom, which for a keyboard player is
        /// somewhere they have since navigated away from - and its hasZoomBeenForced gate makes it a
        /// talking no-op for a camera that reached orbital zoom any other way (the mouse wheel, a restore
        /// by step number). The keyboard's way out is the default view at the system in question, always.
        /// </summary>
        private static void ZoomOut(StarSystemNode node)
        {
            if (GalaxyViewLevels.ZoomStep <= GalaxyViewLevels.DefaultZoomStep)
            {
                return;
            }

            GalaxyViewLevels.ZoomToStep(node, GalaxyViewLevels.DefaultZoomStep);
        }

        /// <summary>
        /// The way into a system's page from the map - the button the mouse takes where the map is
        /// drawing one, and the route behind it wherever that route would really open a page.
        ///
        /// Declared only where the label is NOT drawing a construction slot, which is the other door
        /// into the same page and the one the row prefers when both exist
        /// (<see cref="AddManagementDoor"/>, owner ruling 2026-09-02).
        ///
        /// Declared while the game is drawing the button, and pressed only while the game will act on a
        /// press. Those are two different questions here: the label greys the button out on anything but
        /// a COLONY of ours (<c>StarSystemLabel</c> :1626-1648 assigns the system it enables from at
        /// :1750 only while the state is <c>Colony</c>), while the view level behind it opens for any
        /// system of ours that is not lost AND for anybody else's system we hold a traitor in
        /// (<c>GuiManager.RequestStarSystemManagementViewLevel</c> :1224-1251). So an OUTPOST is drawn a
        /// dead button over a page that would open perfectly well, and dropping the node left every
        /// colony a one-key route into its page while an outpost's had to be flown to on the zoom
        /// ladder. A system somebody else holds and we have turned somebody inside is the same story
        /// wearing a foreign flag: the button is drawn greyed there too, and the page behind it opens.
        ///
        /// The greyed-out button is therefore still declared wherever that route would really open a
        /// page (<see cref="Manageable"/>), and takes the route itself rather than pressing a button
        /// that would do nothing. Nowhere else: on somebody else's system or an empty one the same call
        /// silently degrades to centring the map (<see cref="GalaxyViewLevels.OpenSystem"/>), which is
        /// not a page and would be a node that says it opens something and does not.
        ///
        /// Being DRAWN is the other half, and it is a question about the MAP rather than about the
        /// system: a label the map is not drawing carries no button to light up, to hover or to press.
        /// It briefly stopped being asked here (2026-08-26), because a search landing inside Sabel left
        /// that system with nine children and no way into its page, permanently - the map had been
        /// snapped in on a system whose label it had never been told to draw. That was the snap's
        /// omission and not this gate's: the snap now leaves the map's labels the way a flight would
        /// have (<see cref="GalaxyViewLevels.CatchUpLabels"/>), the button is drawn on every route in,
        /// and the reading can go back to describing what is on the screen.
        /// </summary>
        private static void AddManagementView(
            GraphBuilder builder,
            string key,
            StarSystemNode node,
            StarSystemLabel label
        )
        {
            AgeTransform button = label == null ? null : label.RequestManagementViewButton;
            if (button == null || !Visible(button))
            {
                return;
            }

            if (!AgeWidgets.Operable(button) && !Manageable(node))
            {
                return;
            }

            AgeTransform it = button;
            StarSystemNode at = node;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(ModStrings.GalaxyManageSystem),
                () => OpenManagementView(it, at),
                null,
                Raw(it)
            );
            PointAt(vtable, it);
            // SYNTHETIC on purpose, and it is the one node in this file whose nature was measured
            // rather than reasoned. The button is a real widget and declaring it DRAWN was tried
            // (2026-08-27): the map's own label prefab keeps the button Visible at alpha 0.5 while its
            // grandparent StarSystemNameLine sits at alpha 0 and settled, which is what the gate's
            // chain walk asks about - so the route into a system's page vanished at both ends of the
            // zoom ladder. Measured across the thirteen camera steps with a colony in view: at step 0
            // all 13 drawn buttons failed the chain, at step 12 both of them did, and expanding Dusay
            // through the tree - which flies the camera to step 12 itself - left the expanded system
            // with no /management node at all (DevProbe.GateDiff: onlyUngated =
            // galaxy:constellation/446/system/535/management). Being drawn is asked HERE instead, one
            // step on the button, which is the test that matches how this prefab retires a label.
            builder.AddItem(Nodes.Synthetic(ControlId.Structural(key + "/management"), vtable));
        }

        /// <summary>The map's own way into a system's page: the label's button while the game is willing
        /// to be pressed, and the request the button would have made where it is only drawn greyed
        /// out.</summary>
        private static void OpenManagementView(AgeTransform button, StarSystemNode node)
        {
            if (AgeWidgets.Operable(button))
            {
                AgeWidgets.Press(button);
                return;
            }

            GalaxyViewLevels.OpenSystem(node);
        }

        /// <summary>Whether asking for a system's management page would really open one - the game's own
        /// conditions for it, asked of the same repository it asks
        /// (<c>GuiManager.RequestStarSystemManagementViewLevel</c> :1224-1251). The node must not be
        /// blacked out; then either we hold a system here that is not lost (:1236-1239), or we hold a
        /// traitor in this system and somebody is colonized here at all (:1240-1243) - the page opens
        /// on whichever of the two answered (:1251). Neither, and the game falls through to centring
        /// the map on the node (:1246).</summary>
        private static bool Manageable(StarSystemNode node)
        {
            try
            {
                Empire empire = PlayerEmpire();
                IColonizedStarSystemRepositoryService colonies =
                    Amplitude.Unity.Framework.Services.GetService<IColonizedStarSystemRepositoryService>();
                if (node == null || empire == null || colonies == null || node.IsBlackedOut)
                {
                    return false;
                }

                ColonizedStarSystem mine;
                if (colonies.TryGetValue(empire, node.NodePosition, out mine)
                    && mine.State != StarSystemState.Lost)
                {
                    return true;
                }

                if (!node.EmpiresWithTraitors.Contains(empire))
                {
                    return false;
                }

                ColonizedStarSystem theirs;
                colonies.TryGetColony(node.NodePosition, out theirs);
                return theirs != null;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: asking whether a system's page would open threw: " + e);
                return false;
            }
        }

        /// <summary>The other buttons the map draws on a system's label - the diplomacy button under the
        /// name, the two conversion buy-outs and the pirate mark beside it, the hacking beacon. Which of
        /// them exists at all depends on who lives there and what is being done to the place, so the
        /// list is whatever the game is drawing this frame; a system with none of them keeps whatever
        /// children it had. The treatment each one gets is <see cref="SystemLabelReadout.Actions"/>'s.
        /// </summary>
        private static void AddLabelButtons(GraphBuilder builder, string key, StarSystemLabel label)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(4);
            SystemLabelReadout.Actions(found, label);
            CardActions.Emit(builder, key + "/label", found);
        }

        /// <summary>
        /// Whether the ground of this system is being fought over, and by whom.
        ///
        /// The map says it with one small picture beside the name and no words at all, so the whole
        /// phrase is the mod's. It is gated exactly the way that picture is
        /// (<c>StarSystemLabel.RefreshInvasionContextualIcon</c> :704-751): the node has to be in sight,
        /// its planets have to be visible to this empire, and some colony standing here has to be both
        /// seen and carrying the game's own invasion tag. Anything less and the map is drawing nothing,
        /// so neither is this.
        ///
        /// The attacker is the DISPLAYED one. A privateer fleet fights under somebody else's flag by
        /// design, and the game keeps the two apart on the battle itself
        /// (<c>GroundBattle.AttackerEmpire</c> against <c>DisplayedAttackerEmpire</c>): reading the real
        /// one would tell the player something the game is deliberately hiding from them. Where the
        /// repository has no battle to hand but the tag is set, the bare phrase says what the icon says
        /// - that there is a battle - and names nobody.
        /// </summary>
        private static string GroundBattle(StarSystemNode node, Empire empire)
        {
            try
            {
                if (!Invaded(node, empire))
                {
                    return null;
                }

                IGroundBattleRepositoryService battles =
                    Amplitude.Unity.Framework.Services.GetService<IGroundBattleRepositoryService>();
                GroundBattle battle =
                    battles == null ? null : battles.GetGroundBattleOnNode(node.NodePosition);
                string attacker = battle == null ? null : Owner(battle.DisplayedAttackerEmpire);
                return string.IsNullOrEmpty(attacker)
                    ? ModStrings.Get(ModStrings.GalaxySystemInvaded)
                    : ModStrings.Format(ModStrings.GalaxySystemInvadedBy, attacker);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's ground battle threw: " + e);
                return null;
            }
        }

        /// <summary>The invasion icon's own three conditions, asked of the model the icon asks
        /// them of.</summary>
        private static bool Invaded(StarSystemNode node, Empire empire)
        {
            if (
                node == null
                || empire == null
                || (int)node.Visibility[empire] < (int)EntityVisibility.Layer.Visible
                || node.PlanetsVisibility == null
                || !node.PlanetsVisibility[empire.Index]
            )
            {
                return false;
            }

            IColonizedStarSystemRepositoryService colonies =
                Amplitude.Unity.Framework.Services.GetService<IColonizedStarSystemRepositoryService>();
            if (colonies == null)
            {
                return false;
            }

            foreach (ColonizedStarSystem colony in colonies.GetValues(node.NodePosition))
            {
                if ((int)colony.Visibility[empire] > 1 && colony.IsBeingInvaded)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The time bubbles sitting on this system: what each one is, who put it there, and how long it
        /// has left.
        ///
        /// The map draws a bubble as a coloured disk over the node and writes nothing on it. The game
        /// names the bubble and says who made it inside the system's own dossier, but the turns it has
        /// left appear nowhere on the map at all, which is the one thing a player planning around it
        /// needs. So the line carries all three and the dossier's own words follow it.
        ///
        /// Gated on the bubble's visibility to this empire, which is the disk's own gate
        /// (<c>GalaxyTimeBubble.RefreshVisibility</c> :67-84 hides the object below Visible).
        /// Emphatically NOT on the effects a bubble has on the node it sits on - <c>IsLocked</c> and the
        /// movement refills are true for a bubble nobody can see, and reading them would announce a
        /// bubble the picture is withholding.
        /// </summary>
        private static IList<string> TimeBubbleLines(StarSystemNode node, Empire empire)
        {
            try
            {
                ITimeBubbleRepositoryService bubbles =
                    Amplitude.Unity.Framework.Services.GetService<ITimeBubbleRepositoryService>();
                if (node == null || empire == null || bubbles == null)
                {
                    return null;
                }

                List<string> lines = new List<string>();
                foreach (TimeBubble bubble in bubbles.GetTimeBubbles(node.NodePosition))
                {
                    if (
                        (int)bubble.Visibility[empire] < (int)EntityVisibility.Layer.Visible
                    )
                    {
                        continue;
                    }

                    lines.Add(
                        ModStrings.Format(
                            ModStrings.GalaxySystemTimeBubble,
                            AgeText.Clean(new GuiTimeBubble(bubble).Title),
                            Owner(bubble.Empire),
                            bubble.TurnRemaining
                        )
                    );
                }

                return lines;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's time bubbles threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Who is holding this node: a citadel, or a fleet standing guard over it.
        ///
        /// The map says both by painting a ring round the node in the holder's colour and nothing else,
        /// so the phrases are the mod's - and the ring's own gate is this one
        /// (<c>GalaxyNode.UpdateGuardFeedback</c> :163-197 draws it only for a node in sight). A citadel
        /// is a guard too as far as the model is concerned, so the two are said as one thing or the
        /// other, never both.
        ///
        /// The guarding empire is the DISPLAYED one, for the reason <see cref="GroundBattle"/> records:
        /// a privateer's flag is the game's own concealment and the ring is painted in the flag's
        /// colour. A citadel has no such split - it belongs to the system that built it.
        ///
        /// This is the one thing about a held node the game writes nowhere else: the system's own
        /// dossier - the tooltip behind the star - has no guard or citadel line at all (its panel
        /// features are header, description, FIDSI, population, growth, defense, time bubbles, rooting,
        /// effects, failures, relics), and its defense figure quietly folds a citadel's stock into the
        /// system's own without naming it.
        /// </summary>
        private static IList<string> GuardLines(StarSystemNode node, Empire empire)
        {
            try
            {
                if (
                    node == null
                    || empire == null
                    || (int)node.Visibility[empire] < (int)EntityVisibility.Layer.Visible
                    || !node.IsGuarded
                )
                {
                    return null;
                }

                if (node.IsGuardedByCitadel)
                {
                    string held = Owner(node.CitadelEmpire);
                    return string.IsNullOrEmpty(held)
                        ? null
                        : new string[]
                        {
                            ModStrings.Format(ModStrings.GalaxySystemCitadel, held),
                        };
                }

                string guard = Owner(GuardingEmpire(node));
                return string.IsNullOrEmpty(guard)
                    ? null
                    : new string[]
                    {
                        ModStrings.Format(ModStrings.GalaxySystemGuarded, guard),
                    };
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's guard threw: " + e);
                return null;
            }
        }

        private static Empire GuardingEmpire(GameNode node)
        {
            int index = node.GuardingDisplayedEmpireIndex;
            Empire[] empires = Gui.Game == null ? null : Gui.Game.Empires;
            return empires == null || index < 0 || index >= empires.Length
                ? null
                : empires[index];
        }
    }
}

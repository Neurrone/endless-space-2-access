using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.ES2.UI;
using ES2Access.UI;
using ES2Access.UI.PlanetCards;

namespace ES2Access.Screens
{
    /// <summary>The planets under a system - the orbital card the map draws for each, and everything
    /// read off it.</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>
        /// The planets the map draws inside a system's label, in the order it draws them.
        ///
        /// Which planets those are is the label's own question, asked the same way: the system has been
        /// identified, and this empire is allowed to see its planets at all. Until the system has been
        /// properly surveyed the game draws grey circles and names nothing, and neither does this - the
        /// planets are in the galaxy model the whole time, and reading their names off it would tell
        /// the player things the map is deliberately not showing them.
        ///
        /// Opening a system brings the camera all the way in, and at that distance the map stops
        /// drawing circles and draws a CARD in orbit for each planet - its name, what kind of world it
        /// is, whether it can be colonized and why not, its outputs, its anomalies, and the buttons for
        /// everything a fleet in the system could do to it. That card is what a sighted player browses
        /// a system with, so where one is drawn it is what a planet here reads from and what its own
        /// buttons hang off. Where one is not - the camera is somewhere else, or has not arrived yet -
        /// the planet falls back to the model's own thin answer rather than going silent.
        /// </summary>
        private static void AddPlanets(
            GraphBuilder builder,
            string place,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            try
            {
                if (!PlanetsDeclared(node, empire))
                {
                    return;
                }

                AgeTransform table = label == null ? null : label.PlanetCirclesTable;
                PlanetLabel_SystemOrbital[] cards = OrbitalLabels(node);
                for (int i = 0; i < node.Planets.Count; i++)
                {
                    StarSystemNode system = node;
                    Planet planet = node.Planets[i];
                    Empire looking = empire;
                    PlanetLabel_SystemOrbital card = CardFor(planet, cards);
                    string key = place + "/planet/" + i;
                    // The planet's ONE node, so it carries the planet itself and rides along with it
                    // across a rebuild. There is no second copy to collide with any more: a lane leading
                    // here rebases onto this system rather than re-declaring its insides.
                    ControlId id = ControlId.For(planet, key);
                    // A quest pin planted on the world - or on a curiosity standing on it - is a child
                    // of the world (owner ruling 2026-09-10), so it is one more reason for the row to
                    // be a level of the tree rather than a leaf.
                    bool pinned = MarksPlanet(planet, looking);
                    if (card != null)
                    {
                        AddCard(builder, key, system, planet, looking, card, pinned);
                        continue;
                    }

                    // No card is drawn: the camera is not in on this system. The world is still the
                    // same world, so it reads the same things the card would say - what it is called,
                    // how big and what kind, what the game says about settling it, and how many
                    // curiosities are waiting in orbit - taken from the planet rather than from a
                    // widget that is not on the screen (owner ruling 2026-08-23). What could be DONE
                    // to it stays where the game draws it, so the only children here are the
                    // dossiers, which are the world's own and not the card's
                    // (<see cref="PlanetDossiers"/>).
                    //
                    // The circle is what the player would hover to get the planet's panel; without one
                    // the pointer goes to a carrier of the mod's, which is what makes the dossier
                    // readable with the camera anywhere (<see cref="PlanetCarrier"/>).
                    AgeTransform circle = Circle(table, i);
                    if (ZoomBands.Fidelity(BandKind.Planets) == BandFidelity.Dot)
                    {
                        AddPlanetDot(builder, id, key, system, planet, looking, circle, pinned);
                        continue;
                    }

                    AgeTooltip onTheCircle = AgeWidgets.Raw(circle);
                    AgeTooltip dossier = AgeWidgets.Draws(onTheCircle)
                        ? onTheCircle
                        : PlanetCarrier(system, planet, i, looking);
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => PlanetName(system, planet, looking)),
                            GraphNodes.ValuePart(
                                () =>
                                    PlanetCardReader.SizeAndType(
                                        planet,
                                        Surveyed(system, looking)
                                    )
                            ),
                            GraphNodes.ValuePart(() => PlanetStatus(system, planet, looking)),
                            GraphNodes.ValuePart(() => PlanetCardReader.CuriosityCount(planet, looking)),
                            GraphNodes.ValuePart(() => MiningProbes.Line(planet), false),
                        },
                        Sections = GraphNodes.Sections(
                            NodeSection.Buffer(() => PlanetLines(system, planet, looking)),
                            GraphNodes.TooltipSection(dossier)
                        ),
                    };
                    if (circle != null)
                    {
                        AgeWidgets.PointAt(vtable, circle);
                    }
                    else if (dossier != null)
                    {
                        AgeWidgets.PointAt(vtable, dossier.AgeTransform);
                    }

                    List<TooltipChildren.Dossier> pages = PlanetDossiers(system, planet, looking);
                    if (pages.Count == 0 && !pinned)
                    {
                        // Synthetic: a page of a system's data sheet is a level the mod invented over the game's own panels.
                        builder.AddItem(Nodes.Synthetic(id, vtable));
                        continue;
                    }

                    vtable.ControlType = ControlTypes.Group;
                    // Synthetic for the same reason as the leaf above.
                    builder.BeginGroup(Nodes.Synthetic(id, vtable));
                    if (builder.IsExpanded(id))
                    {
                        TooltipChildren.Emit(
                            builder,
                            key,
                            pages,
                            TooltipChildren.Actions(builder, key)
                        );
                        AddPlanetMarkers(builder, key, planet, looking);
                    }

                    builder.EndGroup();
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's planets threw: " + e);
            }
        }

        /// <summary>
        /// A world as the map's own DOT, which is all the picture is drawing of it from the system
        /// nameplate's band (owner ruling 2026-09-01).
        ///
        /// A sighted player at that distance sees a coloured circle in the system's label, and hovering
        /// it gives the world's name and what the colour means - whether anybody has settled it,
        /// whether anybody could - and nothing else. So that is what the row says, plus the marks the
        /// circle itself is drawn with: the curiosities waiting in orbit and a mining probe standing
        /// over it. What the world PRODUCES, what has been found on it, what could be done to it and
        /// the dossiers behind all of that are the orbital card's, and the card is drawn one band
        /// closer - so they are read there and nowhere else.
        ///
        /// The SAME node as the full reading (<paramref name="id"/> is the planet's own), so a cursor
        /// standing on a world when the camera pulls back stays on that world and simply hears less.
        ///
        /// The review buffer is the circle's own tooltip where the map is drawing one - the player's
        /// hover, exactly. Never the mod's carrier, which assembles the whole planet panel: that is a
        /// truthful reading of the world and an untruthful reading of the dot.
        /// </summary>
        private static void AddPlanetDot(
            GraphBuilder builder,
            ControlId id,
            string key,
            StarSystemNode system,
            Planet planet,
            Empire empire,
            AgeTransform circle,
            bool pinned
        )
        {
            AgeTooltip drawn = AgeWidgets.Raw(circle);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => PlanetName(system, planet, empire)),
                    GraphNodes.ValuePart(() => PlanetStatus(system, planet, empire)),
                    // The two marks the dot itself is drawn with, in BOTH views: the circle carries a
                    // second ring for a world nothing else is like and a tinted one for a Sanctuary
                    // standing on it, and until now neither dot row said either (owner ruling
                    // 2026-09-01). BOTH dot prefabs wire both fields - 253 of 253 circles on each,
                    // measured 2026-09-01 (the scan labels' table and `StarSystemLabelsWindow`'s 86
                    // labels) - so unlike the curiosity ring and the probe mark below there is no
                    // mode branch to make: the ordinary prefab wires all five, the scan one only
                    // these two.
                    //
                    // Asked of the PLANET and not of the widget, under the game's own condition for
                    // drawing either mark (`PlanetCircleItem.Refresh` :118-201 puts both inside the
                    // Revealed branch, which is <see cref="Surveyed"/>). Reading the overlay's alpha
                    // instead would have made the row say different things as the camera panned - the
                    // very camera-dependence the in-mode tree was ruled free of - because a label out
                    // of frame paints none of its circles.
                    GraphNodes.ValuePart(() => UniqueMark(system, planet, empire)),
                    GraphNodes.ValuePart(() => GhostMark(system, planet, empire), false),
                    // THE MARKS ARE THE ORDINARY MAP'S, and the lens's dot does not carry them. The
                    // two prefabs are not the same widget: on every one of the 253 circles the scan
                    // labels were drawing (measured 2026-09-01) `CircleImage`, `UniquePlanetFeedback`
                    // and `GhostFeedback` are wired and `CuriosityAnimatedCircle`,
                    // `MiningProbeFeedback`, `TerraformationFeedback` and `AnomalyReductionFeedback`
                    // are all NULL - the lens's dot cannot draw a curiosity ring or a probe mark at
                    // all. So the row stops claiming them under a lens, exactly as the scan
                    // Curiosities scanner category was dropped for the same measurement.
                    GraphNodes.ValuePart(() => Scanning ? null : PlanetCardReader.CuriosityCount(planet, empire)),
                    GraphNodes.ValuePart(() => Scanning ? null : MiningProbes.Line(planet), false),
                },
            };
            if (AgeWidgets.Draws(drawn))
            {
                vtable.Sections = GraphNodes.Sections(GraphNodes.TooltipSection(drawn));
            }

            if (circle != null)
            {
                AgeWidgets.PointAt(vtable, circle);
            }

            if (!pinned)
            {
                // Synthetic: a dot in a system's label is the map's drawing of a world, not a control.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            // A quest pin planted on the world hangs under it even here, where the picture is drawing
            // the world as one coloured circle: the pin is drawn at every distance.
            vtable.ControlType = ControlTypes.Group;
            // Synthetic for the same reason as the leaf above.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                AddPlanetMarkers(builder, key, planet, empire);
            }

            builder.EndGroup();
        }

        /// <summary>The mark the circle wears for a world nothing else in the galaxy is like
        /// (<c>PlanetCircleItem.UniquePlanetFeedback</c>, shown for <c>Planet.IsUnique</c>), said in
        /// the game's own title for one - the same words the full planet reading already uses, so a
        /// world says one thing about itself at every zoom.</summary>
        private static string UniqueMark(StarSystemNode system, Planet planet, Empire empire)
        {
            return planet != null && planet.IsUnique && Surveyed(system, empire)
                ? Localize("%PlanetScreenUniquePlanetTitle")
                : null;
        }

        /// <summary>The mark the circle wears for a Sanctuary standing on the world
        /// (<c>PlanetCircleItem.GhostFeedback</c>, tinted with the ghost empire's colour), said in the
        /// game's own sentence for one - the same <see cref="GhostWord"/> the full reading uses.
        /// Fixture-blocked: no save in this project has ever held a ghost colony, so this path is
        /// code-verified only.</summary>
        private static string GhostMark(StarSystemNode system, Planet planet, Empire empire)
        {
            return Surveyed(system, empire) ? GhostWord(planet, empire) : null;
        }

        // ---- the orbital cards ----

        private static readonly PlanetLabel_SystemOrbital[] NoCards =
            new PlanetLabel_SystemOrbital[0];

        /// <summary>The orbital window's cards, walked once per frame however many rows ask. The
        /// window draws one system's planets at a time, so this is one root and one walk - and the
        /// walk is the whole prefab subtree, which was being made afresh on every build.</summary>
        private static readonly FrameSweep<PlanetLabel_SystemOrbital> OrbitalCards =
            new FrameSweep<PlanetLabel_SystemOrbital>("galaxy");

        /// <summary>The orbital cards the map is drawing right now. It draws them for ONE system - the
        /// one the camera has come in on - so a system anywhere else on the map gets none, and asking
        /// for another system's cards while this one's are up would hand out the wrong planets'
        /// widgets.</summary>
        private static PlanetLabel_SystemOrbital[] OrbitalLabels(StarSystemNode node)
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = OrbitalWindow();
                if (window == null || !ReferenceEquals(GalaxyViewLevels.FocusedSystem, node))
                {
                    return NoCards;
                }

                return OrbitalCards.Under(window);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the orbital cards threw: " + e);
                return NoCards;
            }
        }

        private static PlanetLabel_SystemOrbital CardFor(
            Planet planet,
            PlanetLabel_SystemOrbital[] cards
        )
        {
            try
            {
                for (int i = 0; i < cards.Length; i++)
                {
                    PlanetLabel_SystemOrbital card = cards[i];
                    // Different widget: picking which of the pooled cards is the one drawn for this planet.
                    if (
                        card != null
                        && ReferenceEquals(card.Planet, planet)
                        && AgeWidgets.Visible(card.AgeTransform)
                    )
                    {
                        return card;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>
        /// The dossiers a world carries WITH NO CARD ON THE SCREEN: one per anomaly found on it, one
        /// per deposit in its ground - what an anomaly does and what would reduce it, what a deposit
        /// is worth and why it cannot be exploited. Where the map IS drawing the card, every one of
        /// these comes off the card's own icons through the shared reader
        /// (<see cref="PlanetCardReader"/>) and the page hands it only a carrier for a deposit the
        /// card is drawing no icon for (<see cref="AddCard"/>).
        ///
        /// NOT the five output figures the card also draws (Planet Food production and kin), though it
        /// hangs a dossier off every one of them: those pages explain what FIDSI IS, the same five
        /// paragraphs repeated on every world in the galaxy, and the star system's own management card
        /// already declares them where a player who wants them is looking. Owner ruling 2026-08-24 -
        /// the strip stays undeclared HERE, and the coverage audit is told so rather than reporting it
        /// (<c>CoverageAudit</c>). The figures themselves are unaffected: they are drawn numbers and
        /// the row reads them as it always did.
        ///
        /// WHICH of them exist is the PLANET's question, not the card's: the map draws a card for one
        /// system at one camera step, and what is in a world's ground is not a thing it hides at any
        /// other. With no card there is no icon to hang the panel on, so each is bound onto a carrier
        /// of the mod's (<see cref="ScratchTooltips"/>) exactly as the game's own item binds, and the
        /// window assembles the same words either way (owner ruling 2026-08-23).
        ///
        /// Behind the survey gate, which is the card's own: an unrevealed node hides the deposit
        /// group, the anomaly table and both output strips wholesale
        /// (<c>PlanetLabel_SystemOrbital.RefreshAsUnrevealedNode</c>).
        /// </summary>
        private static List<TooltipChildren.Dossier> PlanetDossiers(
            StarSystemNode system,
            Planet planet,
            Empire empire
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(8);
            try
            {
                if (planet == null || !Surveyed(system, empire))
                {
                    return found;
                }

                // In the order the card draws them: what was found on the world, then what is in its
                // ground.
                AddAnomalyDossiers(found, planet);
                AddDepositDossiers(found, planet, empire);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a planet's dossiers threw: " + e);
            }

            return found;
        }

        /// <summary>One dossier per anomaly on the world - the paragraph, the effects and what
        /// reducing it would take, none of which the map writes anywhere with no card on the screen.
        /// Each goes on a carrier of the mod's, because the icon that would have carried it is drawn
        /// one camera step in; there, the anomaly is a ROW of the card's own
        /// (<c>CardActions.AddAnomalies</c>) and its page rides on that row.</summary>
        private static void AddAnomalyDossiers(List<TooltipChildren.Dossier> found, Planet planet)
        {
            for (int i = 0; i < planet.Anomalies.Count; i++)
            {
                TooltipChildren.Add(found, AnomalyCarrier(planet, planet.Anomalies[i], i));
            }
        }

        private static AgeTooltip AnomalyCarrier(Planet planet, Anomaly anomaly, int index)
        {
            try
            {
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "planet-anomaly/" + planet.GUID + "/" + index,
                    Hash(anomaly.AnomalyDefinition == null
                        ? null
                        : (string)anomaly.AnomalyDefinition.Name),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiAnomaly wrapper = new GuiAnomaly(anomaly.AnomalyDefinition, planet);
                    carrier.Class = wrapper.TooltipClass;
                    carrier.Content = string.Empty;
                    carrier.Context = planet;
                    carrier.Target = wrapper;
                }

                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: binding an anomaly dossier threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// One dossier per deposit in the world's ground: what size it is, what it does per
        /// population, and the game's own reason where the empire may not exploit it yet.
        ///
        /// The list is the card's own - the colony's OWN deposits where this empire has settled the
        /// world, the planet's raw ones otherwise (<c>RefreshResourceDeposits</c>) - so the nodes and
        /// the icons agree about how many there are, and the Nth carrier is the Nth deposit.
        /// </summary>
        private static void AddDepositDossiers(
            List<TooltipChildren.Dossier> found,
            Planet planet,
            Empire empire
        )
        {
            ColonizedPlanet colony = planet.ColonizedPlanet;
            bool ours = colony != null && colony.Empire == empire;
            int count = ours
                ? colony.ColonizedResourceDeposits.Count
                : planet.ResourceDeposits.Count;
            for (int i = 0; i < count; i++)
            {
                TooltipChildren.Add(found, DepositItemCarrier(planet, colony, ours, i, empire));
            }
        }

        /// <summary>A carrier bound exactly as <c>ResourceDepositItem.Refresh</c> binds the game's own
        /// icon - the same class, the same wrapper, the same refusal text - so the tooltip window
        /// assembles the same panel from a widget nobody can see.</summary>
        private static AgeTooltip DepositItemCarrier(
            Planet planet,
            ColonizedPlanet colony,
            bool ours,
            int index,
            Empire empire
        )
        {
            try
            {
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "planet-deposit/" + planet.GUID + "/" + index,
                    (DossierStamp(empire) * 3L) + (ours ? 1L : 0L),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiResourceDeposit wrapper = ours
                        ? new GuiResourceDeposit(colony.ColonizedResourceDeposits[index])
                        : new GuiResourceDeposit(planet.ResourceDeposits[index]);
                    List<FailureInfo> refusals = new List<FailureInfo>();
                    wrapper.CanBeExploited(PlayerEmpire(), refusals);
                    carrier.Class = wrapper.TooltipClass;
                    carrier.Content = Gui.FormatFailureInfos(refusals);
                    carrier.Context = null;
                    carrier.Target = wrapper;
                }

                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: binding a planet deposit dossier threw: " + e);
                return null;
            }
        }

        /// <summary>A stable number for a name, for a carrier stamp that has to change when the thing
        /// behind it does rather than once a turn.</summary>
        private static long Hash(string text)
        {
            long hash = 17L;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                hash = (hash * 31L) + text[i];
            }

            return hash;
        }

        private static PlanetLabelsWindow_SystemOrbital OrbitalWindow()
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemOrbital>(false)
                    : null;
                return window != null && window.Shown ? window : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// A planet as its orbital card reads it - through the reader every surface that draws a
        /// planet card shares (<see cref="PlanetCardReader"/>), which is what says what a card SAYS,
        /// what it offers and what pages hang off it.
        ///
        /// What is the PAGE's is what the map has and the other two surfaces do not: the id namespace
        /// the cursor rides on, a carrier for a deposit dossier the card is drawing no icon for, the
        /// signals the map draws as pure colour and no widget writes (<see cref="AddSignals"/>), and
        /// the quest pins planted on the world.
        ///
        /// THE CARD IS HANDED NO CLICK: Enter on a planet row does nothing on the map today, and a
        /// button that answers nothing is worse than a group that never claimed to be one.
        ///
        /// The pins are hung only where there ARE pins, because the reader makes a card with children
        /// a level of the tree: handing it a pin emitter unconditionally would turn every planet with
        /// nothing under it into a branch that opens onto nothing.
        /// </summary>
        private static void AddCard(
            GraphBuilder builder,
            string key,
            StarSystemNode system,
            Planet planet,
            Empire empire,
            PlanetLabel_SystemOrbital card,
            bool pinned
        )
        {
            StarSystemNode place = system;
            Planet world = planet;
            Empire looking = empire;
            OrbitalCardAdapter reading = new OrbitalCardAdapter(card);
            reading.Key = key;
            // The Nth deposit the card is drawing no item for - which is how the world's ground stays
            // readable while the table is still pooling up to the planet it was just rebound to. Bound
            // to the same list the card's own items are bound from, and never past its end: an index
            // the model does not have is not a deposit at all.
            ColonizedPlanet colony = planet.ColonizedPlanet;
            bool ours = colony != null && colony.Empire == empire;
            int deposits = ours
                ? colony.ColonizedResourceDeposits.Count
                : planet.ResourceDeposits.Count;
            ColonizedPlanet settled = colony;
            bool mine = ours;
            reading.DepositCarrier = index =>
                index < deposits ? DepositItemCarrier(world, settled, mine, index, looking) : null;
            reading.MapLines = () => SignalLines(place, world, looking);
            if (pinned)
            {
                string place2 = key;
                reading.AppendChildren = into => AddPlanetMarkers(into, place2, world, looking);
            }

            PlanetCardReader.Add(builder, reading);
        }

        /// <summary>The planet's own dossier on a carrier of the mod's, bound exactly as the orbital
        /// card binds <c>PlanetInfoTooltip</c>: the renderer's "Planet" panel over a
        /// <c>GuiPlanet</c> where the system has been surveyed, and the game's own plain "unknown"
        /// sentence where it has not.</summary>
        private static AgeTooltip PlanetCarrier(
            StarSystemNode system,
            Planet planet,
            int orbit,
            Empire empire
        )
        {
            try
            {
                bool surveyed = Surveyed(system, empire);
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "planet/" + system.GUID + "/" + orbit,
                    (DossierStamp(empire) * 31L) + (surveyed ? 1L : 0L),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    carrier.Context = null;
                    if (surveyed)
                    {
                        carrier.Class = "Planet";
                        carrier.Content = string.Empty;
                        carrier.Target = new GuiPlanet(planet);
                    }
                    else
                    {
                        carrier.Class = string.Empty;
                        carrier.Content = "%PlanetStatusUnknownDescription";
                        carrier.Target = null;
                    }
                }

                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: binding a planet's dossier threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The wrecked Arks drifting in this system, each one a button that starts repairing it.
        ///
        /// The game draws them where it draws the planet cards - in the ring around the star of the
        /// system the camera has come in on (<c>WreckedMothershipLabelWindow</c>, bound to the FOCUSED
        /// node), one wordless icon per wreck - so they are children of that system, after its planets,
        /// and they exist for no other system on the map.
        ///
        /// The treatment is the curiosities': the item stays clickable while the game refuses it and
        /// writes the reason into its own tooltip (<c>WreckedMothershipItem.Refresh</c>: no fleet of
        /// yours in orbit, or the action's own failure list), which is exactly what a player who found
        /// a wreck wants to hear. Pressing one posts the repair order; pressing one that is ALREADY
        /// being repaired raises the game's own confirmation box for calling it off, which speaks
        /// through the message-box screen like every other one.
        /// </summary>
        private static void AddWrecks(GraphBuilder builder, string key, StarSystemNode node)
        {
            try
            {
                WreckedMothershipLabelWindow window = WreckWindow(node);
                AgeTransform table = window == null ? null : window.CuriositiesTable;
                IList<AgeTransform> items = table == null ? null : table.Children;
                if (items == null || items.Count == 0)
                {
                    return;
                }

                List<CardActions.CardAction> found = new List<CardActions.CardAction>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    AgeTransform item = items[i];
                    // Spoken count: an item the window is not drawing contributes no action, and an action nobody collected is no node.
                    if (item != null && AgeWidgets.Visible(item))
                    {
                        CardActions.AddRefusable(found, item, WreckName(item, window));
                    }
                }

                CardActions.Emit(builder, key + "/wreck", found);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's wrecked motherships threw: " + e);
            }
        }

        /// <summary>What to call one wreck: the name of the ship itself, which is what the game keeps on
        /// the wrapper it hangs on the item's tooltip, and where that is empty the words the window
        /// writes over the whole group.</summary>
        private static Func<string> WreckName(AgeTransform item, WreckedMothershipLabelWindow window)
        {
            AgeTooltip tooltip = AgeWidgets.Raw(item);
            AgePrimitiveLabel title = window.TitleLabel;
            return () =>
            {
                string named = AgeWidgets.TooltipTitle(tooltip);
                return string.IsNullOrEmpty(named) ? AgeText.Label(title) : named;
            };
        }

        /// <summary>The wreck window, but only while it is drawing THIS system's wrecks - it holds one
        /// system at a time, the one the camera has come in on, exactly as the orbital card window does
        /// (<see cref="OrbitalLabels"/>).</summary>
        private static WreckedMothershipLabelWindow WreckWindow(StarSystemNode node)
        {
            try
            {
                WreckedMothershipLabelWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<WreckedMothershipLabelWindow>(false)
                    : null;
                return window != null
                    && window.Shown
                    && ReferenceEquals(GalaxyViewLevels.FocusedSystem, node)
                    ? window
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The circle the label draws for the planet at <paramref name="index"/>, or null if
        /// the label has not been given one - the window pools its labels and grows the row as systems
        /// are discovered, so a row can be short of a planet the model already has.</summary>
        private static AgeTransform Circle(AgeTransform table, int index)
        {
            try
            {
                if (table == null || index >= table.Children.Count)
                {
                    return null;
                }

                AgeTransform circle = table.Children[index];
                // Different widget: this is the POINTER's target and the hover surface, not the node's own.
                return circle != null && circle.Visible ? circle : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the map calls this planet. A system the player has not surveyed shows a circle
        /// with no name on it and a panel that says only that the planet is unknown, so that word - the
        /// game's own - is the whole of what a planet in one is called here.</summary>
        internal static string PlanetName(StarSystemNode system, Planet planet, Empire empire)
        {
            try
            {
                if (!Surveyed(system, empire))
                {
                    return AgeText.Clean(Gui.Localize("%PlanetStatusUnknownTitle"));
                }

                return AgeText.Clean(new GuiPlanet(planet).Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the circle is saying about the planet by the colour it is drawn in - colonized,
        /// an outpost, inhospitable - in the game's own words for each of those states. Nothing for a
        /// planet in an unsurveyed system: the name has already said it is unknown.</summary>
        private static string PlanetStatus(StarSystemNode system, Planet planet, Empire empire)
        {
            try
            {
                if (!Surveyed(system, empire))
                {
                    return null;
                }

                GuiPlanet.PlanetStatuses status = new GuiPlanet(planet).PlanetStatus;
                return AgeText.Clean(Gui.Localize("%PlanetStatus" + status + "Title"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the game has let this empire see what the planets in a system actually are.
        /// The circles switch from grey unknowns to real planets at the same threshold. Internal
        /// because the scanner asks it too: what is found ON a planet is offered exactly where the
        /// orbital card would draw it (<see cref="GalaxyScanner"/>).</summary>
        internal static bool Surveyed(StarSystemNode system, Empire empire)
        {
            return (int)system.Exploration[empire] >= (int)EntityExploration.State.Revealed;
        }

        /// <summary>Whether this page declares nodes for a system's planets at all - the threshold at
        /// which the map shows this empire that the system HAS planets, below the survey that says
        /// what they are (<see cref="AddPlanets"/>). Asked by the scanner before it offers anything
        /// standing on a planet: a find with no node to land on is a jump that can only refuse.
        /// </summary>
        internal static bool PlanetsDeclared(StarSystemNode node, Empire empire)
        {
            return (int)node.Exploration[empire] >= (int)EntityExploration.State.Identified
                && node.PlanetsVisibility[empire.Index];
        }

        /// <summary>A planet's own node in the tree, keyed exactly as <see cref="AddPlanets"/> keys it
        /// - by the orbit it is in, under its system, carrying the planet itself so the cursor rides
        /// along with it across a rebuild. The ancestry is in the key, so a landing here opens the
        /// constellation and the system on the way in.</summary>
        internal static ControlId PlanetId(StarSystemNode node, int orbit)
        {
            return orbit < 0 || orbit >= node.Planets.Count
                ? null
                : ControlId.For(node.Planets[orbit], PlanetKey(node, orbit));
        }

        /// <summary>The key that id is built from, which anything hanging UNDER a world needs as well -
        /// a quest pin planted on it (<see cref="GalaxyHudScreen.AddPlanetMarkers"/>).</summary>
        internal static string PlanetKey(StarSystemNode node, int orbit)
        {
            return SystemKey(node) + "/planet/" + orbit;
        }
    }
}

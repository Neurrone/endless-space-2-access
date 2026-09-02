using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.ES2.UI;
using ES2Access.UI;

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
                    if (card != null)
                    {
                        // The card carries a row of buttons the game draws under it, so where the game
                        // is drawing any the planet is a level of the tree rather than a leaf: it reads
                        // as itself, and what could be done to it is one step in. Enter on the card is
                        // the card's own click - the planet's page - and nothing else, because
                        // everything else the old menu held is now drawn where the game draws it.
                        List<CardActions.CardAction> actions = OrbitalActions(card);
                        List<TooltipChildren.Dossier> dossiers = PlanetDossiers(
                            system,
                            planet,
                            card,
                            looking
                        );
                        NodeVtable readout = OrbitalReadout(card, system, looking);
                        if (actions.Count == 0 && dossiers.Count == 0)
                        {
                            // Synthetic: an orbital is read out of the system's model; the card is only what the reading came from.
                            builder.AddItem(Nodes.Synthetic(id, readout));
                            continue;
                        }

                        readout.ControlType = ControlTypes.Group;
                            // Synthetic for the same reason as the leaf above.
                        builder.BeginGroup(Nodes.Synthetic(id, readout));
                        if (builder.IsExpanded(id))
                        {
                            object outerRegion = TooltipChildren.Actions(builder, key);
                            CardActions.Emit(builder, key, actions);
                            TooltipChildren.Emit(builder, key, dossiers, outerRegion);
                        }

                        builder.EndGroup();
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
                        AddPlanetDot(builder, id, system, planet, looking, circle);
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
                            GraphNodes.ValuePart(() => PlanetSizeAndType(system, planet, looking)),
                            GraphNodes.ValuePart(() => PlanetStatus(system, planet, looking)),
                            GraphNodes.ValuePart(() => CuriosityCount(planet, looking)),
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

                    List<TooltipChildren.Dossier> pages = PlanetDossiers(
                        system,
                        planet,
                        null,
                        looking
                    );
                    if (pages.Count == 0)
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
            StarSystemNode system,
            Planet planet,
            Empire empire,
            AgeTransform circle
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
                    GraphNodes.ValuePart(() => Scanning ? null : CuriosityCount(planet, empire)),
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

            // Synthetic: a dot in a system's label is the map's drawing of a world, not a control.
            builder.AddItem(Nodes.Synthetic(id, vtable));
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

                return window.GetComponentsInChildren<PlanetLabel_SystemOrbital>(true);
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
        /// The dossiers a world carries beyond its own: one per anomaly found on it, one per deposit
        /// in its ground. The card writes NAMES for these and keeps everything they mean - what an
        /// anomaly does and what would reduce it, what a deposit is worth and why it cannot be
        /// exploited - in a panel only a hover reaches.
        ///
        /// NOT the five output figures the card also draws (Planet Food production and kin), though it
        /// hangs a dossier off every one of them: those pages explain what FIDSI IS, the same five
        /// paragraphs repeated on every world in the galaxy, and the star system's own management card
        /// already declares them where a player who wants them is looking
        /// (<c>SystemManagementScreen.PlanetDossiers</c>). Owner ruling 2026-08-24 - the strip stays
        /// undeclared HERE, and the coverage audit is told so rather than reporting it
        /// (<c>CoverageAudit</c>). The figures themselves are unaffected: they are drawn numbers and
        /// the row reads them as it always did.
        ///
        /// WHICH of them exist is the PLANET's question, not the card's: the map draws a card for one
        /// system at one camera step, and what is in a world's ground is not a thing it hides at any
        /// other. Whether the game is DRAWING an icon for one decides only WHERE the panel appears -
        /// at the game's own icon while it is on the screen, at a carrier of the mod's
        /// (<see cref="ScratchTooltips"/>) where it is not, bound exactly as the game's own item binds
        /// so the window assembles the same words either way (owner ruling 2026-08-23).
        ///
        /// The drawn-icon test is PAINTED, never Visible. These tables pool their items and retire the
        /// leftovers by FADING them, so a planet with no deposits at all keeps the previous planet's
        /// items answering the engine's can-draw test with the previous planet's deposits - measured
        /// on Osulo III, which has none and still offered Hyperium and Titanium. Membership from the
        /// model is the other half of that guard: the loop only ever asks about an item the game has
        /// just bound.
        ///
        /// Behind the survey gate, which is the card's own: an unrevealed node hides the deposit
        /// group, the anomaly table and both output strips wholesale
        /// (<c>PlanetLabel_SystemOrbital.RefreshAsUnrevealedNode</c>).
        /// </summary>
        private static List<TooltipChildren.Dossier> PlanetDossiers(
            StarSystemNode system,
            Planet planet,
            PlanetLabel_SystemOrbital card,
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

                // In the order the card draws them, which is the order its own buffer reads
                // (<see cref="OrbitalDetails"/>): what was found on the world, then what is in its
                // ground.
                AddAnomalyDossiers(found, planet, card);
                AddDepositDossiers(found, planet, card, empire);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a planet's dossiers threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// One dossier per anomaly on the world - the paragraph, the effects and what reducing it
        /// would take, none of which the card writes anywhere.
        ///
        /// The item hangs its tooltip on its ICON rather than on itself
        /// (<c>PlanetAnomalyItem.Bind</c>), so the component's own field is what is read and what is
        /// aimed at: pointing at the row draws nothing at all.
        /// </summary>
        private static void AddAnomalyDossiers(
            List<TooltipChildren.Dossier> found,
            Planet planet,
            PlanetLabel_SystemOrbital card
        )
        {
            AgeTransform table = card == null ? null : card.PlanetAnomaliesTable;
            // Content: whether the card's own icons can carry these dossiers, or whether every one of
            // them falls back to a carrier of the mod's. The table pools its items and fades a retired
            // one, so PAINTED rather than the visibility flag.
            IList<AgeTransform> items = AgeWidgets.Painted(table) ? table.Children : null;
            for (int i = 0; i < planet.Anomalies.Count; i++)
            {
                Anomaly anomaly = planet.Anomalies[i];
                AgeTooltip drawn = DrawnAnomaly(items, i);
                AgeTooltip tooltip = drawn ?? AnomalyCarrier(planet, anomaly, i);
                TooltipChildren.Add(found, tooltip);
            }
        }

        /// <summary>The card's own icon for the Nth anomaly, where it is drawing one. The table is
        /// filled from the same list in the same order (<c>RefreshPlanetAnomalies</c>), so the Nth item
        /// is the Nth anomaly - and a retired one is dropped before it can answer for a planet that no
        /// longer has it.</summary>
        private static AgeTooltip DrawnAnomaly(IList<AgeTransform> items, int index)
        {
            if (items == null || index >= items.Count)
            {
                return null;
            }

            AgeTransform item = items[index];
            // Content: whether the Nth anomaly's dossier comes off the card's own icon. A retired item
            // is faded rather than hidden and still holds the previous world's anomaly.
            if (!AgeWidgets.Painted(item))
            {
                return null;
            }

            PlanetAnomalyItem component = item.GetComponent<PlanetAnomalyItem>();
            return component == null ? AgeWidgets.Raw(item) : component.Tooltip;
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
        /// the icons agree about how many there are, and the Nth icon is the Nth deposit.
        /// </summary>
        private static void AddDepositDossiers(
            List<TooltipChildren.Dossier> found,
            Planet planet,
            PlanetLabel_SystemOrbital card,
            Empire empire
        )
        {
            AgeTransform group = card == null ? null : card.ResourceDepositsGroup;
            // Content, as at the anomalies: whether the card's own icons can carry these dossiers at
            // all, PAINTED because the group pools its items and fades the surplus.
            IList<AgeTransform> items = AgeWidgets.Painted(group) ? group.Children : null;
            ColonizedPlanet colony = planet.ColonizedPlanet;
            bool ours = colony != null && colony.Empire == empire;
            int count = ours
                ? colony.ColonizedResourceDeposits.Count
                : planet.ResourceDeposits.Count;
            for (int i = 0; i < count; i++)
            {
                AgeTooltip drawn = DrawnDepositItem(items, i);
                AgeTooltip tooltip = drawn ?? DepositItemCarrier(planet, colony, ours, i, empire);
                TooltipChildren.Add(found, tooltip);
            }
        }

        private static AgeTooltip DrawnDepositItem(IList<AgeTransform> items, int index)
        {
            if (items == null || index >= items.Count)
            {
                return null;
            }

            AgeTransform item = items[index];
            // Content: whether the Nth deposit's dossier comes off the card's own icon, same pooling
            // and same reason as the anomalies above.
            if (!AgeWidgets.Painted(item))
            {
                return null;
            }

            ResourceDepositItem component = item.GetComponent<ResourceDepositItem>();
            return component == null ? AgeWidgets.Raw(item) : component.Tooltip;
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
        /// A planet as its orbital card reads it: the three lines the card writes - what it is called,
        /// what kind of world it is, and what the game says about colonizing it - with everything the
        /// card draws as icons and gauges in the review buffer, and its buttons one step in.
        ///
        /// And how many curiosities are waiting in orbit, which the card draws as a ring of icons
        /// beside it: they are buttons and so live one step in with the rest, but a player walking the
        /// map would have to open every planet to find out that any exist at all. The count is what a
        /// sighted player takes off the card at a glance, so the card's own line carries it.
        ///
        /// Enter is the card's own click: the planet's page. It is the only thing the card itself does
        /// - except while the game has the map waiting for a target, where the left click means
        /// "confirm here" wherever it lands and the card is no exception (<see cref="PlanetClick"/>).
        /// </summary>
        private static NodeVtable OrbitalReadout(
            PlanetLabel_SystemOrbital card,
            StarSystemNode system,
            Empire empire
        )
        {
            PlanetLabel_SystemOrbital it = card;
            StarSystemNode place = system;
            Empire looking = empire;
            AgeTooltip dossier = it.PlanetInfoTooltip;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.PlanetName)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlanetSizeAndType)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.ColonizeStatus)),
                    GraphNodes.ValuePart(() => OutpostTimer(it)),
                    GraphNodes.ValuePart(() => CuriosityCount(it.Planet, looking)),
                    // A mining probe is a thing somebody has DONE to this planet, and the game keeps
                    // it in the dossier where only a hover finds it. Said on the row so that a rival
                    // staking a world in your own system is heard while walking past it.
                    GraphNodes.ValuePart(() => MiningProbes.Line(it.Planet), false),
                },
                OnActivate = () => PlanetClick(it.Planet, system),
            };
            // What the card DRAWS first, then its dossier - the paragraph the game writes about a
            // world of this kind, its size, its type. The dossier is the long panel behind the card,
            // so the readout indicates it and the buffer is where it is read.
            vtable.Sections = GraphNodes.Sections(
                NodeSection.Buffer(() => OrbitalDetails(it, place, looking)),
                // The timer says a number and nothing else; the sentence the game explains it with is
                // reviewable rather than spoken, because the card already speaks the number and
                // hearing the paragraph again on every pass is what a buffer exists to avoid.
                NodeSection.Buffer(() => OutpostTimerHelp(it)),
                GraphNodes.TooltipSection(dossier)
            );
            AgeWidgets.PointAt(vtable, it.PlanetOrbitalCardContainer ?? it.AgeTransform);
            return vtable;
        }

        /// <summary>
        /// The card's own left click: the planet's page - unless the game has the map waiting for a
        /// target, in which case the click is the order's confirm and nothing else, here as on every
        /// other thing the map draws (<see cref="ZoomIn"/>, <see cref="LaneClick"/>).
        ///
        /// Confirmed at the SYSTEM the card is in orbit around, because a system is what the map
        /// builds a cursor target for - a planet is drawn inside one and has none of its own - and
        /// the mouse aiming at a card at this zoom is aiming inside that system.
        ///
        /// Asked first, and the answer is yes whenever any mode is armed
        /// (<see cref="CursorTargeting.ConfirmAt(GameNode)"/>), refusals included: without that the
        /// card's Enter opened the planet's page and threw the armed mode away with it, which is a
        /// thing no click of the mouse's can do.
        /// </summary>
        private static void PlanetClick(Planet planet, GameNode system)
        {
            if (CursorTargeting.ConfirmAt(system))
            {
                return;
            }

            GalaxyViewLevels.OpenPlanet(planet);
        }

        /// <summary>How long an outpost of ours has left before it becomes a colony - drawn on the card
        /// only while there is one.</summary>
        private static string OutpostTimer(PlanetLabel_SystemOrbital card)
        {
            try
            {
                return card.OutpostTimer != null
                    && AgeWidgets.Visible(card.OutpostTimer.AgeTransform)
                    ? AgeText.Label(card.OutpostTimer)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the game says the outpost timer means - written into the timer's own tooltip
        /// while it is running, and emptied by the card's refresh when it is not.</summary>
        private static IList<string> OutpostTimerHelp(PlanetLabel_SystemOrbital card)
        {
            try
            {
                if (
                    card.OutpostTimer == null
                    || !AgeWidgets.Visible(card.OutpostTimer.AgeTransform)
                )
                {
                    return null;
                }

                Func<IList<string>> lines = AgeWidgets.TooltipLines(card.OutpostTooltip);
                return lines == null ? null : lines();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's own sentence for every one of the three in-progress buttons - the same
        /// one on all three because the game itself writes the same one on all three
        /// (<c>PlanetLabel_SystemOrbital</c> :818, :898, :970). It is the LAST resort for their names
        /// now (<see cref="InProgressName"/>) and stays in every one of their dossiers.</summary>
        private const string CancelJuggernautAction =
            "%PlanetCancelJuggernautActionButtonDescription";

        /// <summary>
        /// What one of the three in-progress buttons is called: WHAT IS BEING DONE, not the fact that
        /// pressing cancels it (owner ruling 2026-08-23).
        ///
        /// A planet being terraformed while one of its anomalies is reduced draws two of these buttons
        /// at once, and the game writes the one sentence
        /// (<see cref="CancelJuggernautAction"/>) onto both - so the card offered two entries the
        /// player could not tell apart. The game does name each action, on the wrapper its own tooltip
        /// is pointing at: the terraformation's and the anomaly reduction's constructible, the
        /// restoration's fleet action (<c>PlanetLabel_SystemOrbital</c> :806-830, :885-900, :960-975 -
        /// the player-empire branch, which is the only one the collector keeps, since a rival's button
        /// is drawn switched off).
        ///
        /// Asked at SPEAK time off the tooltip the button is carrying now: the game rebinds that
        /// tooltip every refresh, and a juggernaut that finishes one action and starts another keeps
        /// the same widget. A wrapper that cannot name itself falls back to the shared sentence, which
        /// is what the button said before this rule - never to silence.
        /// </summary>
        private static Func<string> InProgressName(AgeControlButton button)
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            return () =>
            {
                string title = AgeWidgets.TooltipTitle(AgeWidgets.Raw(widget));
                return string.IsNullOrEmpty(title) ? Localize(CancelJuggernautAction) : title;
            };
        }

        /// <summary>Which of the card's buttons the game is drawing, in drawn order. Empty for a card
        /// the game is offering nothing on, which is what keeps such a planet a leaf of the tree rather
        /// than a branch that opens onto nothing. The treatment each one gets is
        /// <see cref="CardActions"/>'s, shared with the management page's card.</summary>
        private static List<CardActions.CardAction> OrbitalActions(PlanetLabel_SystemOrbital card)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(4);
            try
            {
                CardActions.AddNamedByMod(found, card.ColonizeButton, ModStrings.SystemColonize);
                // The two faction-specific ways of settling a world, drawn in place of Colonize for the
                // empires that have them. The game gives them no caption, so they are named by the
                // sentence their own tooltip opens with.
                CardActions.AddNamedByTooltip(found, card.VodyaniHintButton);
                CardActions.AddNamedByTooltip(found, card.UmbralChoirHintButton);
                CardActions.AddNamedByTooltip(found, card.BuyOutpostButton);
                // The way into a minor civilization's diplomacy, drawn on a world one of them holds.
                // Its own tooltip cannot name it however real the sentence in it: the game gives that
                // one a renderer CLASS (MinorFaction, drawing the faction's panel from the tooltip's
                // Target), and a class-backed tooltip is not the readable kind, so the first line comes
                // back null and the button spoke unnamed. It opens the same screen the system label's
                // diplomacy button does, so it takes the same name.
                CardActions.AddNamedByMod(found, card.MinorFactionButton, ModStrings.GalaxySystemDiplomacy);

                // The row of small round buttons under the card. The game draws them as bare icons and
                // hangs an assembled stat block on each, so there is no caption and no first line of
                // tooltip to name them by - but the game DOES name every one of them, on the fleet
                // action each carries out, and those are the words a player reading the manual would
                // meet. In the order the card draws them.
                CardActions.AddNamedByGame(found, card.TerraformationButton, "%InitiateTerraformPlanetFleetActionTitle");
                CardActions.AddNamedByGame(found, card.RestorationButton, "%InitiateRestorePlanetFleetActionTitle");
                CardActions.AddNamedByGame(found, card.AnomalyReductionButton, "%InitiateReduceAnomalyFleetActionTitle");
                CardActions.AddNamedByGame(found, card.MiningProbeButton, "%LaunchMiningProbeFleetActionTitle");
                CardActions.AddNamedByGame(found, card.DestroyButton, "%DestroyPlanetFleetActionTitle");

                // And the same row's OTHER half: the button the game swaps in for a start button while
                // that action is already running. It is the only way to CANCEL a juggernaut's work and
                // the only place the map says how long is left, and the mod declared none of the three.
                // Named by WHAT IS BEING DONE (<see cref="InProgressName"/>); the turns left and the
                // cancel sentence ride in the node's own dossier, and the row's buffer says what is
                // happening (<see cref="AddSignals"/>). A RIVAL's is drawn switched OFF, which is
                // exactly when the shared collector drops it - a button that cannot be pressed is not
                // an action, and the row's line has already said what it would have said.
                CardActions.AddNamed(
                    found,
                    card.InProgressTerraformationButton,
                    InProgressName(card.InProgressTerraformationButton)
                );
                CardActions.AddNamed(
                    found,
                    card.InProgressRestorationButton,
                    InProgressName(card.InProgressRestorationButton)
                );
                CardActions.AddNamed(
                    found,
                    card.InProgressAnomalyReductionButton,
                    InProgressName(card.InProgressAnomalyReductionButton)
                );

                // The way into pirate diplomacy, drawn on a world whose system holds a pirate lair
                // (DLC9). The game declares the field as a plain transform and hangs a radial button on
                // it, which is why a walk of the card's BUTTON fields never found it - and it keeps the
                // widget drawn while refusing a pirate-hating empire, with the reason written into the
                // same tooltip its name comes from, which is the refusable treatment.
                CardActions.AddRefusable(
                    found,
                    card.PirateLairGroup,
                    CardActions.NameFromTooltip(card.PirateLairGroup)
                );

                // What has been found in orbit and not yet looked into. Each one is a button of the
                // card's like any other, drawn in a ring around it rather than in the row, so they come
                // after the row.
                AddCuriosities(found, card);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card's buttons threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// The curiosities the card is drawing - each one a button that starts an expedition on it.
        ///
        /// The game draws one wordless icon per curiosity still to be looked into, keeps it CLICKABLE
        /// while refusing, and writes the reason into its own tooltip
        /// (<c>PlanetCuriosityItem.Refresh</c>: no fleet in orbit, one already queued, not enough
        /// expedition power), which is the treatment <see cref="CardActions.AddRefusable"/> exists for -
        /// what is in orbit and why it cannot be reached yet is exactly what the player opened the card
        /// to ask. The icon has no caption at all, so the name comes off the wrapper the game hangs on
        /// its tooltip, which is where it keeps the words it would have written.
        ///
        /// The table pools its items - a card that has run out of curiosities keeps the widgets and
        /// FADES them, leaving them visible at alpha 0 - so what is PAINTED is the gate, which is the
        /// same question the game's own <c>GetVisibleChildrenCount</c> asks of this very table when it
        /// lays the ring out.
        /// </summary>
        private static void AddCuriosities(
            List<CardActions.CardAction> found,
            PlanetLabel_SystemOrbital card
        )
        {
            AgeTransform table = card.PlanetCuriositiesTable;
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                // Spoken count: an item the table is not drawing contributes no action, and an action nobody collected is no node.
                if (AgeWidgets.Painted(item))
                {
                    CardActions.AddRefusable(found, item, CardActions.TitleOf(item));
                }
            }
        }

        /// <summary>
        /// How many curiosities are still standing in orbit, said on the planet's own line so that
        /// finding one does not mean opening every planet on the map.
        ///
        /// Counted from the PLANET, not from the ring of icons: the ring is only drawn once the camera
        /// is in on the system, so a count taken off it told the player about a world at one zoom and
        /// nothing at another. The question the count asks is exactly the one the game asks when it
        /// fills the ring (<c>GuiPlanet.GetRemainingCuriosities</c>: every curiosity this empire's
        /// detection lets it SEE), so the number and the buttons agree - and where they briefly do
        /// not, it is because the pooled ring has not caught up with the planet yet.
        /// </summary>
        private static string CuriosityCount(Planet planet, Empire empire)
        {
            try
            {
                int count = 0;
                for (int i = 0; planet != null && i < planet.Curiosities.Count; i++)
                {
                    Curiosity curiosity = planet.Curiosities[i];
                    if (curiosity != null && curiosity.CanBeSeen(empire))
                    {
                        count++;
                    }
                }

                return count == 0
                    ? null
                    : ModStrings.Plural(
                        ModStrings.GalaxyPlanetCuriosityOne,
                        ModStrings.GalaxyPlanetCuriosities,
                        count
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How big a world is and what kind it is, in the game's own template - the second
        /// line the orbital card writes (<c>PlanetLabel_SystemOrbital.RefreshPlanetInformation</c>),
        /// said here for a planet whose card is not drawn. An unsurveyed system's planets keep the
        /// game's own "unknown" word for the type, the way the card does.</summary>
        private static string PlanetSizeAndType(
            StarSystemNode system,
            Planet planet,
            Empire empire
        )
        {
            try
            {
                string size = ElementTitle(planet.Size);
                string type = Surveyed(system, empire)
                    ? ElementTitle(planet.Type)
                    : Gui.Localize("%PlanetTypeUnknownTitle");
                return string.IsNullOrEmpty(size) || string.IsNullOrEmpty(type)
                    ? null
                    : AgeText.Clean(Gui.Localize("%PlaneSizeAndTypeFormat", size, type));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A gui element's title without the engine's "cannot find" warning: <c>Gui.GetTitle</c>
        /// logs one for a missing element and the game forwards its logs to telemetry, which is not a
        /// price a readout should pay for asking.</summary>
        private static string ElementTitle(StaticString name)
        {
            try
            {
                Amplitude.Unity.Gui.GuiElement element = Gui.GetGuiElement(name);
                return element == null || string.IsNullOrEmpty(element.Title)
                    ? null
                    : Gui.Localize(element.Title);
            }
            catch (Exception)
            {
                return null;
            }
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
                : ControlId.For(node.Planets[orbit], SystemKey(node) + "/planet/" + orbit);
        }
    }
}

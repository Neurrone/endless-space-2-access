using System;
using System.Collections.Generic;
using Amplitude;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.ES2.UI;

namespace ES2Access.UI
{
    /// <summary>
    /// A population RING read as rows - the picture both planet-card pages draw and the walk that
    /// turns it into one row per place a unit of population can live.
    ///
    /// The game draws the same ring on the star system page's planet labels and on the empire page's
    /// planet cards, out of the same enumerator and the same drag machinery
    /// (<see cref="PopulationMoves"/>). It was read twice, once per page, and the two readings had
    /// drifted into different answers about the same picture: one attached a dossier per slot and the
    /// other attached none, and each spelled its keys its own way. So the walk lives here and a page
    /// supplies only what is really its own - which ring, whose people, where a drop lands, and what
    /// its own drop DOES.
    ///
    /// The rows are the slots, in the three bands the ring colours them in, and each band is a REGION
    /// named in the game's own word for it (<see cref="PopulationMoves.BandName"/>). A row says where
    /// it is and who is in it; which band it is in is said by the region it is read in, so no row
    /// carries an "overpopulated" or a "locked" word of its own.
    /// </summary>
    public static class PopulationRings
    {
        /// <summary>
        /// WHICH ring is being read, and what the page hosting it will do with a drop.
        ///
        /// <see cref="Markers"/> is the container the game is DRAWING the ring in - the slot geometry
        /// and where each slot's dossier is parked. <see cref="Colony"/> is whose people fill it,
        /// possibly another empire's, which reads and neither carries nor takes.
        /// <see cref="Destination"/> is the colony a drop would land on and is null wherever the game
        /// moves nobody, which is the one gate on both the carry and the drop.
        /// </summary>
        public sealed class Ring
        {
            /// <summary>The world the ring is drawn over. Its GUID keys the slots' scratch carriers.
            /// </summary>
            public Planet Planet;

            /// <summary>Whose people fill the ring, or null on a world nobody has settled.</summary>
            public ColonizedPlanet Colony;

            /// <summary>The colony a drop lands on, or null where the game moves nobody - a foreign
            /// colony's ring, or an unsettled world's.</summary>
            public ColonizedPlanet Destination;

            /// <summary>The container the ring's markers are drawn in.</summary>
            public AgeTransform Markers;

            /// <summary>Prefixes every id the walk declares: a band's region is
            /// <c>Key + "/" + band</c> and a slot's row is <c>Key + "/" + rank</c>.</summary>
            public string Key;

            /// <summary>The scratch-carrier namespace. One card can draw two rings over one world -
            /// the world's own and a Sanctuary's - and two pages can draw a ring over the same world,
            /// so a key naming only the world would hand one ring's slot 1 whatever another ring's
            /// slot 1 was last bound with.</summary>
            public string Scratch;

            /// <summary>Whether this ring's drop would be taken, asked of the page - the game's own
            /// answer, which every population drop target advertises itself by.</summary>
            public Func<CarryItem, bool> Accepts;

            /// <summary>The page's own drop. The second argument is the affinity standing in the slot
            /// being dropped on, which is the one the game's SWAP sends back the other way.</summary>
            public Func<CarryItem, StaticString, DropResult> Drop;
        }

        /// <summary>
        /// A row per SLOT of a ring, in the three bands the ring draws them in.
        ///
        /// The ring is a picture: one marker per place a unit of population can live, coloured for who
        /// is in it and for what kind of place it is. A row per AFFINITY - what both pages did until
        /// 2026-08-26 - said who lived on the world and nothing about how much room there was, which
        /// is the question the ring is on the card to answer. A row per slot says both, and the three
        /// colours become three REGIONS the player steps between.
        ///
        /// The bands are contiguous by construction, so each is opened once and the region and the
        /// context are closed on the way out of it.
        ///
        /// <paramref name="canCarry"/> is where the game would let a drag start off this ring; there
        /// is a carry only where there is also somewhere to put the unit down, which is the ring's own
        /// <see cref="Ring.Destination"/>. One press carries what the GAME's own drag would carry from
        /// that marker (<see cref="PopulationMoves.Carried"/>), so the first Imperial of five carries
        /// five and the last carries one. What is carried is captured then, because the row is rebuilt
        /// every frame and those people may have left the planet by the time it is dropped.
        ///
        /// A FILLED slot is also a drop target, and dropping onto it is the game's SWAP: the affinity
        /// standing there is named as the one to send back the other way, which is what the mouse sets
        /// by hovering a marker mid-drag (<c>PopulationEnumerator</c> :275). A plain add is the CARD's
        /// own drop, one level up.
        /// </summary>
        public static void Add(
            GraphBuilder builder,
            Ring ring,
            List<Population> units,
            List<PopulationSlots.Slot> slots,
            bool canCarry
        )
        {
            if (slots.Count == 0)
            {
                return;
            }

            bool carry = canCarry && ring.Destination != null;
            object outer = builder.Region;
            int total = slots.Count;
            bool inBand = false;
            PopulationSlots.Band band = PopulationSlots.Band.Population;
            try
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    PopulationSlots.Slot slot = slots[i];
                    if (!inBand || band != slot.Kind)
                    {
                        if (inBand)
                        {
                            builder.PopContext();
                        }

                        band = slot.Kind;
                        inBand = true;
                        builder.SetRegion(ring.Key + "/" + band);
                        builder.PushContext(PopulationMoves.BandName(band));
                    }

                    AddSlot(builder, ring, units, slot, total, carry);
                }
            }
            finally
            {
                if (inBand)
                {
                    builder.PopContext();
                }

                builder.SetRegion(outer);
            }
        }

        /// <summary>
        /// One slot of the ring.
        ///
        /// What it SAYS is where it is and who is in it. What it CARRIES is the dossier the game hangs
        /// on that marker, on a carrier of this mod's own (<see cref="ScratchTooltips"/>) because the
        /// ring the player is navigating binds no tooltip to its markers at all - only the detailed
        /// ring a mouse swaps in gets them (<c>PopulationMarker.Bind</c> does all of it under
        /// <c>IsDetailed</c>). The carrier is parked over the marker's own place on the ring, so the
        /// panel appears beside the picture it explains.
        ///
        /// A FILLED slot under the overpopulation arc carries two things at once - who lives there,
        /// and what having them there costs - so the dossier is the row's and the arc's sentence
        /// becomes the one child in its "Tooltips" region.
        /// </summary>
        private static void AddSlot(
            GraphBuilder builder,
            Ring ring,
            List<Population> units,
            PopulationSlots.Slot slot,
            int total,
            bool canCarry
        )
        {
            ColonizedPlanet colony = ring.Colony;
            Population unit = slot.Unit >= 0 && slot.Unit < units.Count ? units[slot.Unit] : null;
            string key = ring.Key + "/" + slot.Rank;
            int rank = slot.Rank;
            int outOf = total;
            bool empty = unit == null && slot.Kind != PopulationSlots.Band.Locked;
            // An UNSETTLED world's ring is all one band of empty slots
            // (<see cref="PopulationSlots.BuildUnsettled"/>), so the row's position in its region is
            // already its rank and saying it again in the label made every row read "Empty slot 1 of
            // 6, 1 of 6". A COLONIZED card keeps the numbered phrase: there the ring is split into
            // bands, so a row's position within its band is not its rank round the ring.
            bool vacant = colony == null && empty;
            AgeTransform marker = DrawnMarker(ring.Markers, rank - 1);
            AgeTooltip carrier = SlotCarrier(ring, slot, unit, marker);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () =>
                            vacant
                                ? ModStrings.Get(ModStrings.SystemPopulationSlotVacant)
                                : ModStrings.Format(
                                    empty
                                        ? ModStrings.SystemPopulationSlotEmpty
                                        : ModStrings.SystemPopulationSlot,
                                    rank,
                                    outOf
                                )
                    ),
                    GraphNodes.ValuePart(() => unit == null ? null : PopulationName(unit)),
                },
                Sections = GraphNodes.Sections(GraphNodes.TooltipSection(carrier)),
            };

            if (carrier != null)
            {
                AgeWidgets.PointAt(vtable, carrier.AgeTransform);
            }

            // EVERY slot of the player's own ring that is not LOCKED takes a drop, and which KIND of
            // drop it is is who is standing in it: an occupied slot is the game's swap (the affinity
            // there is the one sent back the other way), an empty one is the plain add the card
            // itself takes. The empty rows are not a nicety - a player carrying somebody walks the
            // ring looking for a free place to put them, so the free place is where the drop must be;
            // offering it only on the card's header was a gap the owner met at once (2026-08-29).
            // The game's add is not per-slot either way: the server places the people, and the row is
            // only where the gesture is aimed.
            if (ring.Destination != null && slot.Kind != PopulationSlots.Band.Locked)
            {
                if (canCarry && colony != null && unit != null)
                {
                    ColonizedPlanet source = colony;
                    Population held = unit;
                    int carried = PopulationMoves.Carried(MarkerOf(marker), units, slot.Unit);
                    vtable.OnPickUp = () => PopulationMoves.Pick(source, held, carried);
                }

                Ring on = ring;
                StaticString replaced = unit == null ? StaticString.Empty : unit.Affinity;
                vtable.DropKind = PopulationMoves.Kind;
                vtable.DropAccepts = cargo => on.Accepts(cargo);
                vtable.OnDrop = cargo => on.Drop(cargo, replaced);
            }

            List<TooltipChildren.Dossier> nested = SlotDossiers(ring, slot, unit, marker);
            if (nested.Count == 0)
            {
                // Synthetic: a slot is read out of the colony's own model - the ring draws a marker
                // per unit and nothing per empty slot - so the enumeration is the honesty here.
                builder.AddItem(Nodes.Synthetic(ControlId.Structural(key), vtable));
                return;
            }

            ControlId id = ControlId.Structural(key);
            vtable.ControlType = ControlTypes.Group;
            // Synthetic for the same reason as the leaf above.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                TooltipChildren.Emit(builder, key, nested, TooltipChildren.Actions(builder, key));
            }

            builder.EndGroup();
        }

        /// <summary>The sentence a slot carries BESIDE its own dossier, which is only ever the one: a
        /// filled slot under the overpopulation arc, whose row is already the population's dossier and
        /// whose arc still has something to say about it.</summary>
        private static List<TooltipChildren.Dossier> SlotDossiers(
            Ring ring,
            PopulationSlots.Slot slot,
            Population unit,
            AgeTransform marker
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(1);
            if (unit == null || slot.Kind != PopulationSlots.Band.Overpopulation)
            {
                return found;
            }

            AgeTooltip carrier = OverpopulationCarrier(ring, slot.Rank, marker);
            if (carrier != null)
            {
                TooltipChildren.AddPlain(found, carrier, carrier.AgeTransform);
            }

            return found;
        }

        /// <summary>Whichever dossier the ring hangs on this slot: the population's for a filled one,
        /// the arc's sentence for an empty one under the arc, the game's word about what would unlock
        /// it for a locked one - and nothing at all for an ordinary empty place, which the game
        /// explains nowhere either.</summary>
        private static AgeTooltip SlotCarrier(
            Ring ring,
            PopulationSlots.Slot slot,
            Population unit,
            AgeTransform marker
        )
        {
            if (unit != null)
            {
                return PopulationCarrier(ring, slot.Rank, unit, marker);
            }

            if (slot.Kind == PopulationSlots.Band.Locked)
            {
                return LockedCarrier(ring, slot.Rank, marker);
            }

            return slot.Kind == PopulationSlots.Band.Overpopulation
                ? OverpopulationCarrier(ring, slot.Rank, marker)
                : null;
        }

        /// <summary>A carrier bound exactly as <c>PopulationMarker.Bind</c> binds the game's own
        /// detailed marker - the same class, the same wrapper, the same context - so the tooltip
        /// window assembles the population's own dossier for a ring that is drawing no tooltips.
        /// </summary>
        private static AgeTooltip PopulationCarrier(
            Ring ring,
            int rank,
            Population unit,
            AgeTransform marker
        )
        {
            try
            {
                ColonizedPlanet colony = ring.Colony;
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    SlotKey(ring, rank),
                    SlotStamp(colony, (string)unit.Affinity, unit.Count),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiPopulation wrapper = PopulationMoves.Wrap(colony.Empire, unit);
                    carrier.Class = "Population";
                    carrier.Content = wrapper.Title;
                    carrier.Target = wrapper;
                    carrier.Context = wrapper.EmpirePopulationSimulationObject;
                }

                Park(carrier, marker);
                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("population: binding a population slot's dossier threw: " + e);
                return null;
            }
        }

        /// <summary>A carrier holding the sentence the game writes on the overpopulation arc's own
        /// icon (<c>PlanetPopulationEnumeratorRadial.RefreshOverpopulation</c>), which is plain text
        /// under no class - so it is bound as plain text under no class here. The game picks its
        /// singular or plural by how many slots the arc covers, and so does this.</summary>
        private static AgeTooltip OverpopulationCarrier(Ring ring, int rank, AgeTransform marker)
        {
            try
            {
                ColonizedPlanet colony = ring.Colony;
                int covered = colony.MaxPopulation - colony.MaxPopulationUnderOverPopulation;
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    SlotKey(ring, rank) + "/overpopulation",
                    covered,
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    carrier.Class = string.Empty;
                    carrier.Target = null;
                    carrier.Context = null;
                    carrier.Content = Gui.Localize(
                        covered == 1 ? OverpopulationSentence : OverpopulationSentencePlural
                    );
                }

                Park(carrier, marker);
                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("population: binding an overpopulation slot's sentence threw: " + e);
                return null;
            }
        }

        /// <summary>A carrier bound as the game binds a locked marker: its own simple panel naming the
        /// project that would raise this world's maximum.</summary>
        private static AgeTooltip LockedCarrier(Ring ring, int rank, AgeTransform marker)
        {
            try
            {
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    SlotKey(ring, rank) + "/locked",
                    1L,
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    carrier.Class = "Simple";
                    carrier.Target = null;
                    carrier.Context = null;
                    carrier.Content = LockedSentence;
                }

                Park(carrier, marker);
                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("population: binding a locked slot's sentence threw: " + e);
                return null;
            }
        }

        /// <summary>Put a slot's carrier where the ring draws that slot, so the panel opens beside the
        /// marker rather than at the screen's corner. The corner is the fallback and is what
        /// <see cref="ScratchTooltips.Rebind"/> has already set, so a slot the ring is not drawing
        /// this frame simply keeps it.</summary>
        private static void Park(AgeTooltip carrier, AgeTransform marker)
        {
            if (marker != null)
            {
                ScratchTooltips.PlaceOver(carrier, marker);
            }
        }

        /// <summary>A carrier's own key. It carries the RING as well as the world, because a card
        /// drawing a Sanctuary draws two rings over the same planet and one key for both would hand
        /// the Sanctuary's slot 1 whatever the world's slot 1 was last bound with.</summary>
        private static string SlotKey(Ring ring, int rank)
        {
            return "population-slot/" + ring.Planet.GUID + "/" + ring.Scratch + rank;
        }

        /// <summary>What a population slot's dossier depends on: the empire's turn, and who is in the
        /// slot. Rebinding on anything less would reset the tooltip controller's countdown every
        /// frame and the panel would never finish appearing.</summary>
        private static long SlotStamp(ColonizedPlanet colony, string affinity, int count)
        {
            long stamp = 17L;
            for (int i = 0; affinity != null && i < affinity.Length; i++)
            {
                stamp = (stamp * 31L) + affinity[i];
            }

            try
            {
                Game game = Gui.Game;
                stamp = (stamp * 1000003L) + (game == null ? 0L : game.Turn);
            }
            catch (Exception) { }

            return (stamp * 97L) + count;
        }

        private const string OverpopulationSentence = "%PlanetLabelOverPopulationDescription";
        private const string OverpopulationSentencePlural =
            "%PlanetLabelOverPopulationDescriptionPlural";
        private const string LockedSentence = "%PopulationEnumeratorLockedDescription";

        /// <summary>The game's own word for an affinity - what its marker's tooltip is titled with.
        /// </summary>
        public static string PopulationName(Population population)
        {
            try
            {
                return AgeText.Clean(Gui.GetLocalizedTitle(population.Affinity));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The colony a ring is read from when it is the PLAYER's, or null - an unsettled
        /// world's ring, or somebody else's colony, is neither a source nor a target.</summary>
        public static ColonizedPlanet Settled(ColonizedPlanet colony)
        {
            try
            {
                return colony != null && colony.Empire == Gui.PlayerEmpire ? colony : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The widget the ring is drawing for one slot, which is where that slot's dossier belongs on
        /// the screen and which marker the game's own carry count is read off. Null where the ring and
        /// the model disagree about how many slots there are - a frame in the middle of a refresh -
        /// and the dossier then falls back to the scratch carrier's own corner.
        ///
        /// The container keeps its retired markers as children
        /// (<c>PopulationEnumerator.HideAllPopulationMarkers</c> pools them without unparenting), so
        /// the DRAWN ones are the ring - and they are in slot order, because the enumerator sets each
        /// one's sibling index to its own slot and sorts. Counting them is
        /// <see cref="AgeWidgets.DrawnCount"/>, whose gate this shares, so the nth marker here is the
        /// nth of that count. Which container is being drawn is the page's own question: one page
        /// swaps between a simple ring and a detailed one, the other has a single ring.
        /// </summary>
        public static AgeTransform DrawnMarker(AgeTransform container, int index)
        {
            IList<AgeTransform> markers = AgeWidgets.DrawnChildren(container);
            int seen = 0;
            for (int i = 0; markers != null && i < markers.Count; i++)
            {
                AgeTransform marker = AgeWidgets.DrawnChild(markers, i);
                if (marker == null)
                {
                    continue;
                }

                if (seen == index)
                {
                    return marker;
                }

                seen++;
            }

            return null;
        }

        /// <summary>The game's own marker component on a drawn slot, which is what publishes the rank
        /// a drag from that slot carries.</summary>
        public static PopulationMarker MarkerOf(AgeTransform marker)
        {
            try
            {
                return marker == null ? null : marker.GetComponent<PopulationMarker>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

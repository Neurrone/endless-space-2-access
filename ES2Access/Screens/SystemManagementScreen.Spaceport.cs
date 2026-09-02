using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The spaceport panel: its own population slots, what the port will take, and where a
    /// unit dropped into it comes from and goes.</summary>
    public sealed partial class SystemManagementScreen
    {
        /// <summary>
        /// The spaceport's population markers are claimed by the panel's own reader
        /// (<see cref="BuildSpaceport"/>) rather than read as ordinary cells: they are a RING like a
        /// planet card's, with bands and slots and a carry, and the flat cell walk has no way to say
        /// any of that. Claiming the enumerator here is what stops the walk descending into a row of
        /// wordless slots and declaring them twice.
        /// </summary>
        private static bool SpaceportPopulations(AgeTransform widget, SpaceportSidePanel panel)
        {
            PopulationEnumerator markers = panel == null
                ? null
                : panel.SpaceportPopulationEnumerator;
            return markers != null && ReferenceEquals(widget, markers.AgeTransform);
        }

        /// <summary>
        /// The spaceport panel: its readouts in drawn order with its population RING opened up in the
        /// middle of them, the way a planet card's ring is.
        ///
        /// The port is the OTHER place a system keeps population, and until 2026-08-29 it was read as
        /// one row per affinity with the count said - which answered who was waiting and said nothing
        /// about how much room there was, the very question the row of slots is drawn to answer, and
        /// left the empty slots (the only things a unit can be dropped INTO) undeclared entirely. So it
        /// is now modelled exactly like the planet ring: a row per drawn marker, in slot order, banded
        /// into regions named with the game's own words.
        ///
        /// The panel's other controls keep their places around it, which is why the cells are split by
        /// where the ring is drawn rather than emitted before or after it wholesale.
        /// </summary>
        private void BuildSpaceport(
            GraphBuilder builder,
            SpaceportSidePanel panel,
            string keyPrefix
        )
        {
            _cells.Clear();
            SidePanels.Readouts(_cells, panel, keyPrefix, SpecialCell, Transparent);
            List<Cell> above = new List<Cell>(_cells.Count);
            List<Cell> below = new List<Cell>(_cells.Count);
            AgeTransform ring = panel.SpaceportPopulationEnumerator == null
                ? null
                : panel.SpaceportPopulationEnumerator.AgeTransform;
            float at = ring == null ? float.MaxValue : ring.GetGlobalPosition().y;
            for (int i = 0; i < _cells.Count; i++)
            {
                Cell cell = _cells[i];
                (cell.Widget.GetGlobalPosition().y < at ? above : below).Add(cell);
            }

            Cells.EmitLinear(builder, above);
            AddSpaceportSlots(builder, keyPrefix, panel);
            Cells.EmitLinear(builder, below);
        }

        /// <summary>
        /// A row per slot of the spaceport's ring, in the order the game draws them, banded into
        /// regions exactly as a planet card's slots are - the filled and empty places under the game's
        /// own population title, the ones the system's level has not paid for yet under its own word
        /// for a locked slot.
        ///
        /// Each row is the marker the game drew, so it carries THAT marker's own tooltip - the panel
        /// writes one of three sentences onto every marker each refresh, the locked one carrying the
        /// capacity the next system level would buy (<c>SpaceportSidePanel.Refresh</c> :152-186). A
        /// marker the panel has not refreshed yet still holds the prefab's placeholder, so only the
        /// three sentences the panel writes are accepted and anything else is treated as no tooltip at
        /// all.
        ///
        /// A FILLED slot can be picked up from - carrying what the game's own drag would carry from
        /// that marker - and is a swap target. An EMPTY one is a plain drop target. A LOCKED one is
        /// neither: it is a place that does not exist yet, and the game says so in its tooltip.
        /// </summary>
        private static void AddSpaceportSlots(
            GraphBuilder builder,
            string keyPrefix,
            SpaceportSidePanel panel
        )
        {
            Spaceport port = panel.Spaceport;
            PopulationEnumerator ring = panel.SpaceportPopulationEnumerator;
            if (port == null || ring == null)
            {
                return;
            }

            List<AgeTransform> drawn = new List<AgeTransform>(4);
            List<Population> units = new List<Population>(4);
            IList<AgeTransform> children = ring.AgeTransform.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform marker = AgeWidgets.DrawnChild(children, i);
                if (marker == null)
                {
                    continue;
                }

                drawn.Add(marker);
                PopulationMarker it = marker.GetComponent<PopulationMarker>();
                units.Add(
                    it == null || it.GuiPopulation == null ? null : it.GuiPopulation.Population
                );
            }

            object outer = builder.Region;
            List<Cell> band = new List<Cell>(drawn.Count);
            bool locked = false;
            bool inBand = false;
            try
            {
                for (int i = 0; i < drawn.Count; i++)
                {
                    PopulationMarker it = drawn[i].GetComponent<PopulationMarker>();
                    bool shut = it != null && it.Locked;
                    if (inBand && shut != locked)
                    {
                        Cells.EmitLinear(builder, band);
                        band.Clear();
                        builder.PopContext();
                        inBand = false;
                    }

                    if (!inBand)
                    {
                        locked = shut;
                        inBand = true;
                        builder.SetRegion(
                            keyPrefix
                                + "spaceport/population/"
                                + (locked
                                    ? PopulationSlots.Band.Locked
                                    : PopulationSlots.Band.Population)
                        );
                        builder.PushContext(
                            PopulationMoves.BandName(
                                locked
                                    ? PopulationSlots.Band.Locked
                                    : PopulationSlots.Band.Population
                            )
                        );
                    }

                    band.Add(
                        SpaceportSlot(keyPrefix, panel, port, drawn, units, i)
                    );
                }
            }
            finally
            {
                if (inBand)
                {
                    Cells.EmitLinear(builder, band);
                    builder.PopContext();
                }

                builder.SetRegion(outer);
            }
        }

        /// <summary>One slot of the spaceport's ring.</summary>
        private static Cell SpaceportSlot(
            string keyPrefix,
            SpaceportSidePanel panel,
            Spaceport port,
            List<AgeTransform> drawn,
            List<Population> units,
            int index
        )
        {
            AgeTransform marker = drawn[index];
            PopulationMarker it = marker.GetComponent<PopulationMarker>();
            Population unit = units[index];
            bool locked = it != null && it.Locked;
            bool empty = unit == null && !locked;
            int rank = index + 1;
            int outOf = drawn.Count;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () =>
                            ModStrings.Format(
                                empty
                                    ? ModStrings.SystemPopulationSlotEmpty
                                    : ModStrings.SystemPopulationSlot,
                                rank,
                                outOf
                            )
                    ),
                    GraphNodes.ValuePart(() => unit == null ? null : PopulationRings.PopulationName(unit)),
                },
                Sections = GraphNodes.Sections(GraphNodes.TooltipSection(SlotSentence(marker))),
            };
            AgeWidgets.PointAt(vtable, marker);

            if (unit != null)
            {
                Population held = unit;
                int carried = PopulationMoves.Carried(it, units, index);
                vtable.OnPickUp = () => PopulationMoves.Pick(port, held, carried);
            }

            if (!locked)
            {
                SpaceportSidePanel host = panel;
                StaticString replaced = unit == null ? StaticString.Empty : unit.Affinity;
                vtable.DropKind = PopulationMoves.Kind;
                vtable.DropAccepts = cargo => PortTakes(host, cargo, unit != null);
                vtable.OnDrop = cargo => DropIntoSpaceport(host, cargo, replaced);
            }

            return new Cell
            {
                Widget = marker,
                Id = ControlId.Structural(keyPrefix + "spaceport/population/" + index),
                Vtable = vtable,
            };
        }

        /// <summary>The sentence the panel wrote onto this marker, and nothing else. The panel writes
        /// exactly one of three keys onto every marker each refresh
        /// (<c>SpaceportSidePanel.Refresh</c> :166-186); a marker it has not reached yet still carries
        /// the prefab's placeholder ("This is changed by code"), which is parked text rather than
        /// something the game is saying.</summary>
        private static AgeTooltip SlotSentence(AgeTransform marker)
        {
            AgeTooltip tooltip = marker == null ? null : marker.AgeTooltip;
            // The raw content is read to IDENTIFY the sentence, never to say it: the three keys below
            // are what the panel writes, and anything else is the prefab's placeholder. The words
            // still reach the player only through the door - the tooltip itself is handed to
            // GraphNodes.TooltipSection below, or nothing is.
            string content = tooltip == null ? null : tooltip.Content;
            return content == SpaceportSlotFilled
                || content == SpaceportSlotEmpty
                || content == SpaceportSlotLocked
                ? tooltip
                : null;
        }

        private const string SpaceportSlotFilled = "%SpacePortSelectedPopulationSlotDescription";
        private const string SpaceportSlotEmpty = "%SpacePortEmptyPopulationSlotDescription";
        private const string SpaceportSlotLocked = "%SpacePortLockedPopulationSlotDescription";

        /// <summary>
        /// Whether the spaceport would take what is being carried.
        ///
        /// The game asks no <c>CanAcceptPopulationDrop</c> of the port at all - the panel is simply put
        /// in the target list whenever it is drawn (<c>StartDrag</c> :144-148) - so the tests are the
        /// panel's own client's: the port is the player's and drawn, the unit is leaving a PLANET, and
        /// somebody would actually move. There is no port-to-port move: the order the client posts
        /// names the planet the unit comes off (<c>ApplyDrop</c> :38-44), and a unit already in the
        /// port has none.
        ///
        /// The last test is the clamp itself, asked with the very numbers the drop will use
        /// (<see cref="PopulationMoves.IntoPort"/>). A drop onto an EMPTY slot needs a free slot. A
        /// drop onto an OCCUPIED one is the swap, which frees its own slot by bouncing somebody back
        /// onto the source planet - so it works on a FULL port, but only while that planet has room to
        /// take them, and a full port plus a full source planet moves nobody at all. Asking the
        /// arithmetic rather than a hand-written "is there a free slot" is what keeps the word "drop
        /// target" and the outcome from ever disagreeing.
        /// </summary>
        private static bool PortTakes(SpaceportSidePanel panel, CarryItem held, bool swapping)
        {
            try
            {
                Population population = held == null ? null : held.Cargo as Population;
                Spaceport port = panel == null ? null : panel.Spaceport;
                ColonizedPlanet source =
                    population == null ? null : PopulationMoves.PlanetOf(population);
                if (
                    port == null
                    || !panel.Shown
                    || port.Empire != Gui.PlayerEmpire
                    || source == null
                )
                {
                    return false;
                }

                return PopulationMoves.IntoPort(port, swapping ? source : null, held.Quantity) > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Put the carried unit into the spaceport, the way the drag does it: the drag info is filled
        /// as the game's own drag start fills it and the SOURCE's client -
        /// <c>PlanetLabelsWindow_SystemManagement</c>, since the unit is coming off a planet - posts
        /// the order, which is what keeps the sound and the exact orders the game builds.
        ///
        /// A drop onto an OCCUPIED slot names that slot's affinity as the one being replaced, and the
        /// client then posts the game's own two orders: one of the replaced people back onto the source
        /// planet, then the carried people in (<c>ApplyDrop</c> :38-44).
        ///
        /// What is SAID is what really moved. The port clamps rather than refusing
        /// (<c>Spaceport.TransferPopulation</c> :191), so a carry of three into one free slot moves one
        /// and says one.
        /// </summary>
        private static DropResult DropIntoSpaceport(
            SpaceportSidePanel panel,
            CarryItem item,
            StaticString replaced
        )
        {
            Population population = item == null ? null : item.Cargo as Population;
            Spaceport port = panel == null ? null : panel.Spaceport;
            ColonizedPlanet source =
                population == null ? null : PopulationMoves.PlanetOf(population);
            if (population == null || port == null || source == null || !panel.Shown)
            {
                return DropResult.Refused(null);
            }

            try
            {
                bool swapping = !StaticString.IsNullOrEmpty(replaced);
                int moved = PopulationMoves.IntoPort(
                    port,
                    swapping ? source : null,
                    item.Quantity
                );
                if (moved <= 0)
                {
                    return DropResult.Refused(null);
                }

                IDragDropClient client =
                    Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false);
                if (client == null)
                {
                    return DropResult.Refused(null);
                }

                try
                {
                    PopulationMoves.Fill(source, population, item.Quantity, replaced, true);
                    client.ApplyDrop(panel);
                }
                finally
                {
                    PopulationMoves.Clear();
                }

                return DropResult.Done(
                    ModStrings.Format(
                        ModStrings.SystemPopulationMoved,
                        PopulationMoves.Name(population, moved),
                        ModStrings.Get(ModStrings.SystemSpaceportPanel)
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("system: moving a population unit into the spaceport threw: " + e);
                return DropResult.Refused(null);
            }
        }

        /// <summary>The drawn spaceport panel this carried unit came OUT of, or null - which is how a
        /// drop tells the two sources apart, since what is carried is the game's own
        /// <c>Population</c> and the owner holding it is the one whose own table it is in.</summary>
        private static SpaceportSidePanel SpaceportSource(Population population)
        {
            try
            {
                StarSystemScreen screen = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<StarSystemScreen>(false)
                    : null;
                SpaceportSidePanel panel =
                    screen == null ? null : screen.GetSpaceportSidePanel();
                Spaceport port = panel == null ? null : panel.Spaceport;
                if (port == null || !panel.Shown || port.PopulationsByAffinity == null)
                {
                    return null;
                }

                Population held;
                return port.PopulationsByAffinity.TryGetValue(population.Affinity, out held)
                    && ReferenceEquals(held, population)
                    ? panel
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

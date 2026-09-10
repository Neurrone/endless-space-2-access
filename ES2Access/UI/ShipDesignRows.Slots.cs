using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    public static partial class ShipDesignRows
    {
        // ---- the ship's own module slots ----

        /// <summary>
        /// Where the modules go: the slots the game draws over the rendered ship, one row each.
        ///
        /// The container has no caption of its own - the slots are drawn ON the ship - so the stop gets
        /// a word of the mod's. Their drawn positions come out of the 3D model
        /// (<c>ComputeModuleSlotPosition2D</c>), so they are put in reading order and then walked as a
        /// list: they are peers of one kind and a 2D walk over where a hull happens to put its guns
        /// would be a walk over nothing.
        ///
        /// And that reading order is then GROUPED by the type of module each slot takes
        /// (<see cref="SlotOrder"/>), because where a hull's model puts its guns is not an order at all:
        /// the drawn one runs weapon, support, weapon down the ship. The drawn order survives inside one
        /// type. The remove target is not a slot and stays where it is, at the end.
        /// </summary>
        private static void BuildSlots(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            List<Cell> cells
        )
        {
            try
            {
                AgeTransform container = panel.ShipDesignSlotItemsContainer;
                // Flow control: the slots are found by a component scrape and each read for its module.
                if (container == null || !AgeWidgets.Visible(container))
                {
                    return;
                }

                cells.Clear();
                ShipDesignEditionSlotItem[] slots = Slots.Under(container);
                for (int i = 0; i < slots.Length; i++)
                {
                    AddSlot(cells, panel, slots[i], prefix, i);
                }

                if (cells.Count == 0)
                {
                    return;
                }

                builder.BeginStop(SlotsStop(prefix));
                builder.PushContext(ModStrings.Get(ModStrings.ShipDesignSlots));
                EmitGrouped(builder, cells);
                AddRemoveTarget(builder, panel, prefix);
                builder.PopContext();
            }
            catch (Exception e)
            {
                Log.Warn("ship design: reading the module slots threw: " + e);
            }
        }

        // The slots on their way to being declared: the drawn order, and the type key each one is
        // grouped under, parallel to it. Reused rather than allocated per frame, main-thread only, like
        // the statistics band's four lists above.
        private static readonly List<Cell> _slotOrder = new List<Cell>(12);
        private static readonly List<string[]> _slotTypes = new List<string[]>(12);
        private static readonly List<SlotFacing> _slotFacings = new List<SlotFacing>(12);

        /// <summary>The slots grouped by the type of module they take, keeping the drawn order inside
        /// one type - the walk a player can predict over a set the hull scattered round its
        /// model.</summary>
        private static void EmitGrouped(GraphBuilder builder, List<Cell> cells)
        {
            try
            {
                Cells.Drawn(cells, _slotOrder);
                _slotTypes.Clear();
                _slotFacings.Clear();
                for (int i = 0; i < _slotOrder.Count; i++)
                {
                    _slotTypes.Add(_slotOrder[i].Order);
                    _slotFacings.Add(_slotOrder[i].OrderFacing);
                }

                SlotOrder.Arrange(_slotOrder, _slotTypes, _slotFacings);
                EmitOrdered(builder, _slotOrder);
            }
            finally
            {
                // Nothing of the game's is held between builds, so a teardown has nothing to unhook.
                _slotOrder.Clear();
                _slotTypes.Clear();
                _slotFacings.Clear();
            }
        }

        /// <summary>
        /// One slot: what is in it, what it will take, and the markers the game draws round its edge -
        /// the module categories it is restricted to, the multiplier it applies, whether it is mirrored
        /// on the far side of the ship, whether it is a heavy mount, and the resource it costs on top of
        /// the module.
        ///
        /// Filled, it is the module's name and nothing else, and a button that takes the module out;
        /// empty, it is the word "empty" and the markers, and no action, because that is what a click on
        /// it does. Either way it is where a carried module is put down, and it says so while one is
        /// held. The markers are the SLOT's own facts, so a filled slot does not read them out where
        /// they would be mistaken for the module's - and all but the special cost it does not say at
        /// all, because the module's own tooltip, already in this node's buffer, ends with the game's
        /// own "Slot Information" section saying them (owner ruling, 2026-08-19).
        ///
        /// Keyed on the GuiSlot rather than on the item drawing it: the container pools its items and
        /// rebinds them on every refresh of the ship, so a cursor keyed on <c>Item000</c> would be
        /// standing on a different slot a frame after a module was fitted.
        /// </summary>
        private static void AddSlot(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            ShipDesignEditionSlotItem slot,
            string prefix,
            int index
        )
        {
            // Kept: cells.Count decides whether the slots stop is opened at all, and the drop target
            // the mod draws at the end of it is declared from that same decision - so a slot the
            // panel is not drawing must not make the list look non-empty.
            if (slot == null || slot.GuiSlot == null || !AgeWidgets.Visible(slot.AgeTransform))
            {
                return;
            }

            ShipDesignEditionSlotItem it = slot;
            ShipDesignEditionPanel owner = panel;
            AgeTooltip tooltip = slot.SlotTooltip ?? AgeWidgets.Raw(slot.AgeTransform);
            Func<bool> enabled = () => AgeWidgets.Operable(it.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                // A control either way, filled or empty, and its parts never come and go: the watch
                // below re-baselines instead of speaking whenever a row's part LIST changes shape, so a
                // node that dropped its role word as it emptied swallowed the very change the watch is
                // there to announce (measured - Enter on a filled slot emptied it in silence).
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    // WATCHED, unlike every other name in the mod: what a slot is called IS what is in
                    // it, and both of the things the player does to a slot - Enter to take the module
                    // out, a drop to put one in - change it under a cursor that is standing right
                    // there. Both of those are silent in the game, so the live part is the only
                    // feedback either gesture has.
                    new NodeAnnouncement(
                        () => SlotName(it),
                        live: true,
                        kind: AnnouncementKinds.Label
                    ),
                    GraphNodes.DisabledPart(enabled),
                    // What the slot ITSELF is - the categories it takes, its multiplier, what it costs -
                    // and only while it is empty. Full, those words are a statement about the SLOT read
                    // straight after the name of the MODULE in it, and they read as the module's own
                    // roles ("Titanium Slug, defence module, support module"); the module's name is the
                    // whole of what a filled slot is called, and the slot's own facts move to the review
                    // buffer below. Kept as a PART that answers null rather than a part that goes away:
                    // the live watch re-baselines when the part list changes shape, which is exactly the
                    // fill/unfill transition the watch above exists to announce.
                    // Filled, the one thing the module's own name and tooltip cannot say: which way the
                    // slot lets it shoot. The player would otherwise have to take a module out to
                    // learn where the gun under it points (owner ruling, 2026-09-10).
                    GraphNodes.ValuePart(
                        () => Filled(it) ? FacingWord(it) : SlotMarkers(it),
                        false
                    ),
                },
                Sections = GraphNodes.Sections(() => SlotDetails(it), tooltip),
                DropKind = ModuleKind,
                OnDrop = held => Drop(owner, it, held),
                // The slot's own click, empty or filled, is "take the module out"
                // (<c>OnSlotUnequipCb</c> is wired to both of the groups the slot draws): on an empty
                // one the game's handler finds nothing to take and does nothing, which is what a mouse
                // gets too. An empty slot offers nothing to pick up, and the carry key is silent there.
                OnActivate = () =>
                {
                    if (enabled())
                    {
                        AgeWidgets.Press(DrawnButton(it));
                    }
                },
                OnPickUp = () => PickSlot(it),
                // The same test the drop makes, so the word and the outcome cannot disagree: a slot the
                // carried module does not fit is not somewhere it can go.
                DropAccepts = held => Takes(owner, it, held),
            };

            AgeWidgets.PointAt(vtable, slot.AgeTransform);
            Cells.Add(
                cells,
                slot.AgeTransform,
                ControlId.For(slot.GuiSlot, prefix + "/slot/" + index),
                vtable
            );
            cells[cells.Count - 1].Order = SlotTypes(slot);
            cells[cells.Count - 1].OrderFacing = Facing(slot);
        }

        /// <summary>
        /// The types of module a slot takes, as the words the player hears for them and in the order
        /// they are compared in - what the ship's slots are grouped by (<see cref="SlotOrder"/>).
        ///
        /// The slot's own restriction list, NOT the categories table beside it: the game hides that
        /// table for a slot with four or more restrictions and the item's own drawn state says nothing
        /// about a filled slot, while the ordering has to hold for every slot on the ship whatever is in
        /// it. Answered from the definition, so it is the same answer every frame.
        /// </summary>
        private static readonly Dictionary<SlotDefinition, string[]> _typesByDefinition =
            new Dictionary<SlotDefinition, string[]>();
        private static string _typesLanguage;

        private static string[] SlotTypes(ShipDesignEditionSlotItem slot)
        {
            try
            {
                // The answer is definition data read through the language and nothing else: the
                // restriction list is built off the slot's SlotDefinition on every get
                // (decompiled/Assembly-CSharp/GuiSlot.cs:62-77), and the words are localization. So the
                // memo is keyed on the definition and emptied when the language moves - it outlives the
                // frame because neither half of it can change within a session's language.
                string language = Localization.ModLocale.Language;
                if (_typesLanguage != language)
                {
                    _typesByDefinition.Clear();
                    _typesLanguage = language;
                }

                SlotDefinition definition = slot.GuiSlot.Slot.Definition;
                string[] known;
                if (definition != null && _typesByDefinition.TryGetValue(definition, out known))
                {
                    return known;
                }

                string[] categories = slot.GuiSlot.ModuleTypeRestrictions;
                string[] names = null;
                if (categories != null && categories.Length > 0)
                {
                    names = new string[categories.Length];
                    for (int i = 0; i < categories.Length; i++)
                    {
                        names[i] = AgeText.Title(Gui.GetTitle(categories[i]));
                    }

                    SlotOrder.Alphabetical(names);
                }

                if (definition != null)
                {
                    _typesByDefinition[definition] = names;
                }

                return names;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Which way the guns in this slot can be brought to bear, read off the firing cones the slot
        /// carries - and off nothing else, because the game writes no word for the fact anywhere.
        ///
        /// A weapon fires only at a target inside one of its slot's cones and idles otherwise
        /// (<c>BattleSimulationModule_Weapon</c> :433-440), so where a hull's slot points is what a
        /// module fitted in it will actually do in a battle. Every hull the game ships draws that with
        /// two shapes: ONE cone down the nose (270 degrees wide), and a PAIR of cones out to port and
        /// starboard (120 degrees each). The community calls them a front turret and a broadside; the
        /// mod says the same, and any other shape is left unsaid rather than guessed at - which
        /// covers the handful of hulls whose weapon slots carry two forward cones, two broadside
        /// pairs, or one lone side cone. A slot no weapon can go in answers none whatever cones it
        /// has (<see cref="TakesWeapons"/>).
        ///
        /// Read from the cone data rather than from the slot's name or the hull's, because the names
        /// are a modder's free text while the cones are what the battle uses. Memoised on the
        /// definition like <see cref="SlotTypes"/>: the cones are static data, so the answer cannot
        /// change within a session, and the build path only ever does a dictionary lookup.
        /// </summary>
        private static readonly Dictionary<SlotDefinition, SlotFacing> _facingByDefinition =
            new Dictionary<SlotDefinition, SlotFacing>();

        private static SlotFacing Facing(ShipDesignEditionSlotItem slot)
        {
            try
            {
                SlotDefinition definition = slot.GuiSlot.Slot.Definition;
                if (definition == null)
                {
                    return SlotFacing.None;
                }

                SlotFacing known;
                if (_facingByDefinition.TryGetValue(definition, out known))
                {
                    return known;
                }

                SlotFacing facing = TakesWeapons(definition)
                    ? FromCones(definition.LinesOfSight)
                    : SlotFacing.None;
                _facingByDefinition[definition] = facing;
                return facing;
            }
            catch (Exception)
            {
                return SlotFacing.None;
            }
        }

        /// <summary>Whether a gun can go in this slot at all - the question the facing is an answer
        /// about.
        ///
        /// The cones are NOT that question: hulls give firing cones to defence and support slots too
        /// (the Zolya-class draws a forward cone on its defence slot and a rearward one on a support
        /// slot), and a slot that will never hold a weapon has nothing to point at anything. A slot
        /// with no restriction at all is not one either: it has no category word to say the facing in
        /// front of, and nothing in the shipped hulls is both unrestricted and coned.</summary>
        private static bool TakesWeapons(SlotDefinition definition)
        {
            ModuleCategory[] categories = definition.RestrictedModuleCategories;
            for (int i = 0; categories != null && i < categories.Length; i++)
            {
                if (categories[i] == ModuleCategory.Weapon)
                {
                    return true;
                }
            }

            return false;
        }

        private static SlotFacing FromCones(List<SlotDefinition.LineOfSight> cones)
        {
            if (cones == null)
            {
                return SlotFacing.None;
            }

            if (cones.Count == 1 && Forward(cones[0].Direction))
            {
                return SlotFacing.FrontTurret;
            }

            // Two cones facing OPPOSITE sides, which is the broadside: one cone out to a single side is
            // a shape the game does not ship and the mod has no word for.
            if (
                cones.Count == 2
                && Sideways(cones[0].Direction)
                && Sideways(cones[1].Direction)
                && cones[0].Direction.x * cones[1].Direction.x < 0f
            )
            {
                return SlotFacing.Broadside;
            }

            return SlotFacing.None;
        }

        // The hull data writes its directions as whole numbers on one axis (Z=1 for the nose, X=1 or
        // X=-1 for the sides); the tolerance is there so a direction nudged off the axis is still read
        // as the axis it is nearly on, and anything genuinely diagonal falls through to no facing.
        private static bool Forward(UnityEngine.Vector3 direction)
        {
            return direction.z > 0.9f && Math.Abs(direction.x) < 0.1f;
        }

        private static bool Sideways(UnityEngine.Vector3 direction)
        {
            return Math.Abs(direction.x) > 0.9f && Math.Abs(direction.z) < 0.1f;
        }

        /// <summary>The facing on its own, in the lower case a filled slot reads it in - straight after
        /// the name of the module standing in it, where the categories the slot takes would be a
        /// statement about the slot mistaken for one about the module.</summary>
        private static string FacingWord(ShipDesignEditionSlotItem slot)
        {
            switch (Facing(slot))
            {
                case SlotFacing.Broadside:
                    return ModStrings.Get(ModStrings.ShipDesignSlotBroadside);
                case SlotFacing.FrontTurret:
                    return ModStrings.Get(ModStrings.ShipDesignSlotFrontTurret);
                default:
                    return null;
            }
        }

        private static bool Filled(ShipDesignEditionSlotItem slot)
        {
            try
            {
                return slot.GuiSlot != null && slot.GuiSlot.GuiModule != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>What the slot is called: the module in it, or "empty".
        ///
        /// <c>GuiSlot.Title</c> answers both, but its answer for an empty slot is the game's generic
        /// title for the CategorySlot element - a phrase about slots in general, in front of the marker
        /// list that says what THIS slot takes. The state is the thing the player is walking the ship
        /// for, so the state is what the name says.</summary>
        private static string SlotName(ShipDesignEditionSlotItem slot)
        {
            try
            {
                if (slot.GuiSlot == null)
                {
                    return null;
                }

                return Filled(slot)
                    ? AgeText.Clean(slot.GuiSlot.Title)
                    : ModStrings.Get(ModStrings.ShipDesignSlotEmpty);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The markers the game draws round a slot: the module categories it accepts (icons, one per
        /// restriction), the multiplier it applies to whatever is fitted, whether it is mirrored on the
        /// far side of the ship, whether it is a heavy mount, and the resource the slot itself costs.
        ///
        /// Three of those five are drawn with no words at all - the multiplier is a row of DOTS, the
        /// pairing is one circle, and a heavy mount is not marked AT ALL (the slot is simply drawn 1.3x
        /// bigger, <c>ShipDesignBaseSlotItem.Bind</c> :21-26) - which is why reading their transforms'
        /// text answered empty and the mod said nothing about any of them. Each is read from the fact
        /// the game drew the picture FROM, and gated on the picture being drawn.
        ///
        /// Asked when the row is read rather than watched, because it walks the slot's definition.
        /// </summary>
        private static string SlotMarkers(ShipDesignEditionSlotItem slot)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                Add(message, SlotCategories(slot));
                Add(message, SlotMultiplier(slot));
                Add(message, SlotPairing(slot));
                Add(message, SlotHeavyMount(slot));
                Add(message, SlotCosts(slot));
                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The module categories the slot is restricted to, as the game's own titles for them,
        /// and only while the game is drawing their icons - with the way the slot shoots in front of
        /// them, because "a weapon slot" is two different slots on a hull and which one this is decides
        /// what a gun put in it can reach (owner ruling, 2026-09-10). The category words stay the
        /// game's own; only the phrase they are set in is the mod's.</summary>
        private static string SlotCategories(ShipDesignEditionSlotItem slot)
        {
            if (slot.SlotCategoriesTable == null || !slot.SlotCategoriesTable.Visible)
            {
                return null;
            }

            MessageBuilder message = new MessageBuilder();
            string[] categories = slot.GuiSlot.ModuleTypeRestrictions;
            for (int i = 0; categories != null && i < categories.Length; i++)
            {
                string title = AgeText.Title(Gui.GetTitle(categories[i]));
                if (title != null)
                {
                    Add(message, title);
                }
            }

            string takes = message.Build();
            if (string.IsNullOrEmpty(takes))
            {
                return null;
            }

            switch (Facing(slot))
            {
                case SlotFacing.Broadside:
                    return ModStrings.Format(ModStrings.ShipDesignSlotBroadsideTakes, takes);
                case SlotFacing.FrontTurret:
                    return ModStrings.Format(ModStrings.ShipDesignSlotFrontTurretTakes, takes);
                default:
                    return takes;
            }
        }

        /// <summary>
        /// How much of a fitted module's effect the slot applies, as the dots the game draws round the
        /// slot say it: two, three or four of them (<c>ShipDesignEditionSlotItem.Bind</c> :82-84).
        ///
        /// The DOTS are the gate rather than the number, so the mod never states a multiplier the
        /// player cannot see - and the number is the slot's own, which is what a fifth dot the game has
        /// no picture for would still be. The words are the mod's, the one deviation from the game's
        /// own in this band: see <see cref="ModStrings.ShipDesignSlotMultiplier"/>.
        /// </summary>
        private static string SlotMultiplier(ShipDesignEditionSlotItem slot)
        {
            if (
                !Drawn(slot.SlotMultiplier2)
                && !Drawn(slot.SlotMultiplier3)
                && !Drawn(slot.SlotMultiplier4)
            )
            {
                return null;
            }

            return ModStrings.Format(
                ModStrings.ShipDesignSlotMultiplier,
                Amplitude.Extensions.FloatExtensions.ToString(slot.GuiSlot.Multiplier)
            );
        }

        /// <summary>Whether fitting this slot fits the mirrored one on the far side of the ship too,
        /// in the game's own words for the fact ("Symmetrical (x2 cost)"), and only while the game is
        /// drawing its flag.
        ///
        /// The twin itself is never a row of its own: every listener slot in the hull data is hidden
        /// and <c>ShipDesignBasePanel.RefreshShipSlots</c> :222 filters those out before an item is
        /// made for them, so the pair cannot be split by the grouping (<see cref="SlotOrder"/>) - there
        /// is only ever one of it.</summary>
        private static string SlotPairing(ShipDesignEditionSlotItem slot)
        {
            return Drawn(slot.SlotPairingFlag) ? AgeText.Title("%PanelFeatureSlotSymetricalTitle") : null;
        }

        /// <summary>Whether the slot takes a heavy version of the module, in the game's own words for
        /// it ("Heavy Mount").
        ///
        /// The only marker with no picture to gate on: the game draws a heavy mount by making the slot
        /// 1.3x wider than the rest (<c>ShipDesignBaseSlotItem.Bind</c> :21-26), which is a size a
        /// sighted player reads and a dump cannot, so the slot's own flag is the gate.</summary>
        private static string SlotHeavyMount(ShipDesignEditionSlotItem slot)
        {
            return slot.GuiSlot.IsLarge ? AgeText.Title("%PanelFeatureSlotLargeTitle") : null;
        }

        /// <summary>The resource the slot itself costs on top of the module, which the game draws as a
        /// symbol beside the slot and keeps the words for on <c>GuiSlot.SpecialCost</c>.</summary>
        private static string SlotCosts(ShipDesignEditionSlotItem slot)
        {
            MessageBuilder message = new MessageBuilder();
            if (slot.SpecialCostMarker != null && slot.SpecialCostMarker.Visible)
            {
                Add(message, AgeText.Clean(slot.GuiSlot.SpecialCost));
            }

            return message.Build();
        }

        private static bool Drawn(AgeTransform marker)
        {
            // Content: the marker IS the fact - the design draws it and writes no word for it anywhere.
            return marker != null && marker.Visible;
        }

        /// <summary>
        /// What a FILLED slot has to say about itself that its name no longer does: the resource it
        /// costs on top of the module.
        ///
        /// NOT what it accepts, nor its multiplier, its pairing or its mount size, even though those
        /// are the rest of what the markers say: the module in it carries the game's own tooltip, and
        /// that tooltip already ends with a "Slot Information" section listing exactly those
        /// categories and drawing exactly those three markers (measured on the live panel; owner
        /// ruling, 2026-08-19 - a filled slot changes nothing). The tooltip is in this node's buffer
        /// too, so a line of the mod's would be the same sentence twice, one of them a paraphrase.
        ///
        /// Empty, the markers are in the readout already (<see cref="SlotMarkers"/>) and repeating them
        /// here would be the same words twice on one control.
        /// </summary>
        private static IList<string> SlotDetails(ShipDesignEditionSlotItem slot)
        {
            try
            {
                if (!Filled(slot))
                {
                    return null;
                }

                string costs = SlotCosts(slot);
                return string.IsNullOrEmpty(costs) ? null : new string[] { costs };
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Add(MessageBuilder message, string fragment)
        {
            if (!string.IsNullOrEmpty(fragment))
            {
                message.ListItem(fragment);
            }
        }

        /// <summary>Whichever of the slot's two groups the game is drawing - the picture of the module
        /// in it or the empty frame - both of which carry the same click.</summary>
        private static AgeTransform DrawnButton(ShipDesignEditionSlotItem slot)
        {
            if (slot.EquipedModuleGroup != null && slot.EquipedModuleGroup.Visible)
            {
                return AgeWidgets.Transform(slot.EquipedModuleButton) ?? slot.EquipedModuleGroup;
            }

            return slot.UnequipedModuleGroup;
        }
    }
}

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
        // ---- carrying a module ----

        private static CarryItem PickModule(ShipDesignModuleItem item)
        {
            try
            {
                return item.GuiEditionModule == null || !AgeWidgets.Operable(item.AgeTransform)
                    ? null
                    : new CarryItem(item, ModuleName(item), ModuleKind);
            }
            catch (Exception e)
            {
                Log.Warn("ship design: picking a module up threw: " + e);
                return null;
            }
        }

        private static CarryItem PickSlot(ShipDesignEditionSlotItem slot)
        {
            try
            {
                return !Filled(slot) || !AgeWidgets.Operable(slot.AgeTransform)
                    ? null
                    : new CarryItem(slot, SlotName(slot), ModuleKind);
            }
            catch (Exception e)
            {
                Log.Warn("ship design: picking a fitted module up threw: " + e);
                return null;
            }
        }

        /// <summary>The module being carried, whether it came off the list or out of another slot.
        /// </summary>
        private static GuiModule Carried(CarryItem held)
        {
            ShipDesignModuleItem tile = held == null ? null : held.Cargo as ShipDesignModuleItem;
            if (tile != null)
            {
                return tile.GuiEditionModule;
            }

            ShipDesignEditionSlotItem slot =
                held == null ? null : held.Cargo as ShipDesignEditionSlotItem;
            return slot == null || slot.GuiSlot == null ? null : slot.GuiSlot.GuiModule;
        }

        /// <summary>
        /// Whether this slot would take the carried module - the game's own test, against the design's
        /// own prerequisite context, which is the same expression <c>OnModuleItemDragStarted</c> :1512
        /// builds its valid-target list from, plus the design being editable at all.
        ///
        /// Deliberately NOT the slot's own enabled state, which answers a different question: the
        /// category strip DIMS every slot the current filter does not cover
        /// (<c>ShipDesignBaseSlotItem.RefreshCurrentFilter</c>), and the game's own drag re-enables the
        /// compatible ones for the duration of the drag (<c>RefreshForDragDrop</c> :213-221). Reading
        /// the dimming as a refusal would make a carry fail on exactly the slots a mouse drag succeeds
        /// on whenever a filter is in force.
        /// </summary>
        private static bool Takes(
            ShipDesignEditionPanel panel,
            ShipDesignEditionSlotItem slot,
            CarryItem held
        )
        {
            try
            {
                GuiModule module = Carried(held);
                if (
                    module == null
                    || panel.GuiShipDesign == null
                    || slot.GuiSlot == null
                    || panel.CurrentMode == ShipDesignEditionPanel.Mode.ReadOnly
                )
                {
                    return false;
                }

                return slot.GuiSlot.Slot.Definition.CanModuleBeBound(
                    module.ModuleDefinition,
                    panel.GuiShipDesign.ShipDesign.SlotPrerequisitesContext
                );
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Put the carried module in this slot, the way the drag does it: the game's own dragged-item
        /// record is filled in exactly as <c>OnModuleItemDragStarted</c> / <c>OnSlotItemDragStarted</c>
        /// fill it, and then the drag's own <c>ApplyDrop</c> commits - which is what keeps the
        /// swap-and-displace rules for a module coming out of another slot.
        ///
        /// Nothing here posts an order: the whole designer is a preview until the window's own Apply or
        /// Create button is pressed.
        /// </summary>
        private static DropResult Drop(
            ShipDesignEditionPanel panel,
            ShipDesignEditionSlotItem slot,
            CarryItem held
        )
        {
            try
            {
                if (ReferenceEquals(held.Cargo, slot))
                {
                    // Back into the slot it came out of: the drag ends having moved nothing, which is
                    // what putting it down on its own row means everywhere else.
                    return DropResult.Done(ModStrings.Get(ModStrings.DragCancelled));
                }

                GuiModule module = Carried(held);
                if (module == null || !Takes(panel, slot, held))
                {
                    return DropResult.Refused(null);
                }

                DragDropWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<DragDropWindow>(false)
                    : null;
                if (window == null || window.ShipDesignModuleDraggedItem == null)
                {
                    return DropResult.Refused(null);
                }

                window.ShipDesignModuleDraggedItem.SourceItem = held.Cargo as GuiBehaviour;
                window.ShipDesignModuleDraggedItem.GuiModule = module;
                ((IDragDropClient)panel).ApplyDrop(slot);
                return DropResult.Done(
                    ModStrings.Format(ModStrings.ShipDesignModuleFitted, held.Name)
                );
            }
            catch (Exception e)
            {
                Log.Warn("ship design: fitting a carried module threw: " + e);
                return DropResult.Refused(null);
            }
        }

        /// <summary>
        /// Where a module is dropped to take it off the ship - the mod's own node, at the end of the
        /// slots, and the one gesture on this panel with no widget of its own behind it.
        ///
        /// The mouse's way of removing a module is to drag it off the ship and let go over nothing
        /// (<c>ApplyDrop(null)</c> -> <c>RemoveModuleFromSlot</c>), and "nothing" is not somewhere a
        /// keyboard can aim. Declared even while nothing is being carried, because a place the player has
        /// to already know about is a place they will never find: walking to the end of the slots is how
        /// the removal announces that it exists. It says "drop target" only while a module that could
        /// actually come off is held, which is the standard indication and needs no words of its own.
        ///
        /// Not declared at all while the design is read-only: there is no gesture to explain on a page
        /// nothing can be taken off, and an inert instruction is worse than none.
        ///
        /// Keyed on the container, which the panel keeps for as long as the page is up - the slot items
        /// under it are pooled and rebound on every refresh.
        /// </summary>
        private static void AddRemoveTarget(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix
        )
        {
            if (!Editable(panel))
            {
                return;
            }

            ShipDesignEditionPanel owner = panel;
            NodeVtable vtable = new NodeVtable
            {
                // No control type, like every other line this mod draws itself: it is an instruction the
                // player reads, and a role word on it would name a widget the game never drew.
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ModStrings.Get(ModStrings.ShipDesignRemoveTarget)),
                },
                DropKind = ModuleKind,
                OnDrop = held => Remove(owner, held),
                // The same test the drop makes, so the word and the outcome cannot disagree - the
                // <see cref="Takes"/> rule, on the one target that is not a slot.
                DropAccepts = held => Removable(owner, held),
            };

            builder.AddItem(Nodes.Drawn(
                ControlId.For(
                    panel.ShipDesignSlotItemsContainer,
                    prefix + "/slot/remove-target"
                ),
                vtable,
                panel.ShipDesignSlotItemsContainer
            ));
        }

        private static bool Editable(ShipDesignEditionPanel panel)
        {
            try
            {
                return panel.GuiShipDesign != null
                    && panel.CurrentMode != ShipDesignEditionPanel.Mode.ReadOnly;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether taking the carried module off the ship is a thing that could happen here, which is
        /// what both the "drop target" word and the drop itself ask.
        ///
        /// A module carried off the LIST is the case this rules out: it was never fitted, so there is
        /// nothing to take off, and the game's own <c>ApplyDrop</c> (:345-351, which acts only with a
        /// slot on one end or the other) does nothing with it either. It is not somewhere that module
        /// can go, and it says so both ways - no drop word while it is held, and the ordinary refusal
        /// on a press.
        /// </summary>
        private static bool Removable(ShipDesignEditionPanel panel, CarryItem held)
        {
            return held != null
                && held.Cargo is ShipDesignEditionSlotItem
                && Editable(panel)
                && Carried(held) != null;
        }

        /// <summary>
        /// Take the carried module off the ship, the way letting go over nothing does it: the same
        /// dragged-item record the drag fills in, and then <c>ApplyDrop</c> with no target at all, which
        /// is what the game's own drag hands it when the pointer is over nothing it recognises.
        /// </summary>
        private static DropResult Remove(ShipDesignEditionPanel panel, CarryItem held)
        {
            try
            {
                if (!Removable(panel, held))
                {
                    return DropResult.Refused(null);
                }

                GuiModule module = Carried(held);
                DragDropWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<DragDropWindow>(false)
                    : null;
                if (window == null || window.ShipDesignModuleDraggedItem == null)
                {
                    return DropResult.Refused(null);
                }

                window.ShipDesignModuleDraggedItem.SourceItem = held.Cargo as GuiBehaviour;
                window.ShipDesignModuleDraggedItem.GuiModule = module;
                ((IDragDropClient)panel).ApplyDrop(null);
                return DropResult.Done(
                    ModStrings.Format(ModStrings.ShipDesignModuleRemoved, held.Name)
                );
            }
            catch (Exception e)
            {
                Log.Warn("ship design: removing a carried module threw: " + e);
                return DropResult.Refused(null);
            }
        }
    }
}

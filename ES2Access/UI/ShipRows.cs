using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI.Input;

namespace ES2Access.UI
{
    /// <summary>
    /// The ships half of a garrison, wherever the game draws it.
    ///
    /// <c>ShipsManagementPanel</c> is one prefab the game reuses: the star system page's hangar
    /// (<c>StarSystemHangarPanel</c>, which is nothing but this class with an extra expand button) and
    /// the fleet panel's ship list are the same panel with the same toolbar and the same ship tiles.
    /// Both screens declare it from here rather than each writing its own, which is also how each of
    /// them gets the buttons its own fixture never draws - Sell, Specialize - for free.
    ///
    /// The buttons carry no words: they are icons, and their tooltips explain rather than name them.
    /// The game does name them, in its own data, under the action each one carries out
    /// (<c>%FleetRetrofitTitle</c>, <c>%FleetScrapTitle</c>), which is where the names come from -
    /// never from a mod paraphrase.
    ///
    /// Ships are a SELECT-THEN-ACT model and stay one: a ship is picked out and the toolbar above acts
    /// on it. What the keyboard now has is the same three gestures the mouse has, because
    /// <c>ShipsManagementPanel.OnToggleShipItem</c> (:707-750) branches on the modifier the REAL
    /// keyboard is holding - so Enter, Control and Enter, and Shift and Enter all replay the tile's own
    /// click and the game reads the modifier off the player's own fingers. Nothing here copies the
    /// game's selection rules: plain is <c>SelectShipRadioMode</c>, Control is <c>SelectShip</c> on this
    /// one tile, and Shift is the run from the panel's private <c>lastClickedShipGUID</c> anchor to
    /// here, an anchor a reimplementation would have to keep in step for ever. The tile's own SECOND
    /// click is a fourth gesture and not a selection at all - it opens the ship's design - and it is
    /// wired here once for every panel that draws a tile.
    ///
    /// A tile's drawn tick is a frame BEHIND the model: <c>SelectShip</c> and <c>SelectShipRadioMode</c>
    /// write only <c>selectedShipsPerGarrison</c> and mark the panel dirty, and
    /// <c>BindGarrisonPanel</c> (:355-372) rewrites the toggles on the next refresh. So the live part a
    /// focused row watches reads the drawn tick, which is cheap and right in every settled frame, and
    /// what a row says immediately AFTER a chord is read from the model instead.
    /// </summary>
    public static class ShipRows
    {
        /// <summary>What a carried ship is, for the controls that will take one - see
        /// <see cref="CarryItem.Kind"/>.</summary>
        public const string ShipKind = "ship";

        /// <summary>
        /// The row of things that can be done to the selected ships, in drawn order.
        ///
        /// <paramref name="createsFleets"/> is whether the panel's HOST does anything with the
        /// Create-fleet button. The panel asks its host to make the fleet
        /// (<c>IGuiGarrisonsHandler.CreateFleet</c>), and the Military screen's implementation of that is
        /// an empty method body (<c>MilitaryScreen.cs:193-200</c>) - the button is drawn there, enables
        /// itself and does nothing at all when pressed. A control the game answers with silence is not
        /// declared, so that screen says false; every other host implements it.
        /// </summary>
        public static void Toolbar(
            List<Cell> cells,
            ShipsManagementPanel panel,
            string keyPrefix,
            bool createsFleets = true
        )
        {
            Button(cells, panel.SelectAllButton, "%FleetSelectAllShipsTitle", keyPrefix, "select-all");
            if (createsFleets)
            {
                Button(
                    cells,
                    panel.CreateButton,
                    "%FleetCreateFromShipsTitle",
                    keyPrefix,
                    "create-fleet"
                );
            }

            Button(cells, panel.RepairButton, "%FleetRepairTitle", keyPrefix, "repair");
            Button(cells, panel.RetrofitButton, "%FleetRetrofitTitle", keyPrefix, "retrofit");
            Button(cells, panel.ScrapButton, "%FleetScrapTitle", keyPrefix, "scrap");
            Button(cells, panel.SellButton, "%FleetSellTitle", keyPrefix, "sell");
            Button(
                cells,
                panel.SpecializeJuggernautButton,
                "%FleetSpecializeJuggernautTitle",
                keyPrefix,
                "specialize"
            );
        }

        /// <summary>Every ship tile the panel is drawing, whichever garrison panel holds it.</summary>
        public static void Ships(
            List<Cell> cells,
            ShipsManagementPanel panel,
            string keyPrefix,
            bool carryable
        )
        {
            AgeTransform table = panel == null ? null : panel.GarrisonPanelsTable;
            if (table == null)
            {
                return;
            }

            ShipItem[] ships = table.GetComponentsInChildren<ShipItem>(true);
            for (int i = 0; i < ships.Length; i++)
            {
                Ship(cells, ships[i], panel, keyPrefix, carryable);
            }
        }

        /// <summary>
        /// One ship tile. Public because the hero's own ship is drawn on its own, outside any garrison
        /// table, and reads exactly the same.
        ///
        /// <paramref name="carryable"/> is where the ship can be picked up and carried, which is only
        /// where there is somewhere to put it down: the fleet panel draws the fleet lines a ship can be
        /// moved into, and the star system page's hangar draws none, so offering the pick-up there
        /// would be offering a mode with no exit but Escape.
        /// </summary>
        public static void Ship(
            List<Cell> cells,
            ShipItem ship,
            ShipsManagementPanel panel,
            string keyPrefix,
            bool carryable
        )
        {
            if (ship == null || ship.GuiShip == null)
            {
                return;
            }

            ShipItem it = ship;
            ShipsManagementPanel owner = panel;
            AgeTooltip tooltip = AgeWidgets.Raw(ship.AgeTransform);
            bool ranged = false;
            NodeVtable vtable = GraphNodes.SelectionItem(
                () => AgeText.Label(it.Title),
                () => it.SelectionToggle != null && it.SelectionToggle.State,
                () => Selected(owner, it),
                () => AgeWidgets.Toggle(it.SelectionToggle),
                () => AgeWidgets.Operable(it.AgeTransform),
                tooltip
            );

            // All three gestures are the tile's own click. Which of the game's three selection rules
            // runs is decided by the modifier the player is physically holding, which is the whole
            // point: the mouse and the keyboard then share one anchor and one set of rules.
            vtable.OnSelectToggle = () => AgeWidgets.Toggle(it.SelectionToggle);
            vtable.OnSelectRange = () =>
            {
                ranged = true;
                AgeWidgets.Toggle(it.SelectionToggle);
            };

            // The game's own second click on a ship tile opens that ship's DESIGN
            // (<c>ShipItem.OnDoubleClickCb</c> :190-218 - read-only for a design the player may not
            // edit, and refused outright for one the game marks hidden or an inspection bypass, all of
            // which is the handler's own decision). It hangs off the tick rather than off a button,
            // and it picks the tile out itself, so there is nothing to select first.
            vtable.OnDoubleClick = () => AgeWidgets.DoubleClick(it.SelectionToggle);

            // The two selection chords, which the tile draws nothing about: both replay its click and
            // the GAME branches on the modifier still held (<c>ShipsManagementPanel</c> :713 and :738).
            NodeHints.Add(vtable, ModStrings.HintAddToSelection, UiActions.SelectToggle);
            NodeHints.Add(vtable, ModStrings.HintSelectUpToHere, UiActions.SelectRange);

            // A range changed every row between the anchor and this one, so this row's new state is not
            // the answer - what the selection now IS, is.
            Func<string> settled = vtable.StateText;
            vtable.StateText = () =>
            {
                bool range = ranged;
                ranged = false;
                string outcome = range ? SelectionText.Range(SelectedNames(owner)) : null;
                return outcome ?? (settled == null ? null : settled());
            };

            if (carryable)
            {
                vtable.OnPickUp = () => Pick(it);
            }

            AgeWidgets.PointAt(vtable, ship.AgeTransform);
            Cells.Add(
                cells,
                ship.AgeTransform,
                ControlId.For(ship, keyPrefix + "/ship/" + ship.GetInstanceID()),
                vtable
            );

            // The badge in the tile's corner says what this ship is FOR - the role's own description,
            // written onto the icon by <c>ShipItem.Bind</c> :104-115 - and the tile can only ever draw
            // ONE tooltip, so it cannot ride along with the ship's own dossier. A child node rather
            // than a node beside the tile: this is a list the player walks ship by ship, and a second
            // stop per ship would double the walk.
            List<TooltipChildren.Dossier> role = new List<TooltipChildren.Dossier>(1);
            TooltipChildren.AddPlain(
                role,
                ship.RoleIcon == null ? null : ship.RoleIcon.AgeTransform
            );
            if (role.Count > 0)
            {
                Cell tile = cells[cells.Count - 1];
                tile.Dossiers = role;
                tile.Key = keyPrefix + "/ship/" + ship.GetInstanceID();
            }
        }

        /// <summary>What the tile offers to carry: the ship itself, under the name the tile is saying
        /// at this moment. A tile the game has disabled - somebody else's ship - offers nothing.
        /// </summary>
        private static CarryItem Pick(ShipItem item)
        {
            try
            {
                global::Ship ship = item.GuiShip == null ? null : item.GuiShip.Ship;
                if (ship == null || !AgeWidgets.Operable(item.AgeTransform))
                {
                    return null;
                }

                return new CarryItem(ship, AgeText.Label(item.Title), ShipKind);
            }
            catch (Exception e)
            {
                Log.Warn("ships: picking a ship up threw: " + e);
                return null;
            }
        }

        /// <summary>Whether the panel's own model has this ship picked out - the answer a row gives
        /// straight after a chord, before the panel has redrawn its ticks.</summary>
        private static bool Selected(ShipsManagementPanel panel, ShipItem item)
        {
            try
            {
                global::Ship ship = item.GuiShip == null ? null : item.GuiShip.Ship;
                if (panel == null || ship == null)
                {
                    return item.SelectionToggle != null && item.SelectionToggle.State;
                }

                return panel.GetSelectedShips(true).Contains(ship);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The names of the picked-out ships in the order the panel DREW them, which is the
        /// order the player walked - the game's own selection list is keyed by garrison and says
        /// nothing about the row order. Asked on a keypress only.</summary>
        private static IList<string> SelectedNames(ShipsManagementPanel panel)
        {
            List<string> names = new List<string>();
            try
            {
                AgeTransform table = panel == null ? null : panel.GarrisonPanelsTable;
                if (table == null)
                {
                    return names;
                }

                List<global::Ship> selected = panel.GetSelectedShips(true);
                ShipItem[] items = table.GetComponentsInChildren<ShipItem>(true);
                for (int i = 0; i < items.Length; i++)
                {
                    ShipItem item = items[i];
                    if (
                        item == null
                        || item.GuiShip == null
                        // Content: which ship names are gathered into a phrase.
                        || !AgeWidgets.Visible(item.AgeTransform)
                        || !selected.Contains(item.GuiShip.Ship)
                    )
                    {
                        continue;
                    }

                    names.Add(AgeText.Label(item.Title));
                }
            }
            catch (Exception e)
            {
                Log.Warn("ships: reading the selection threw: " + e);
            }

            return names;
        }

        private static void Button(
            List<Cell> cells,
            AgeControlButton button,
            string titleKey,
            string keyPrefix,
            string key
        )
        {
            PanelButtons.Add(cells, button, titleKey, keyPrefix, key, false);
        }
    }
}

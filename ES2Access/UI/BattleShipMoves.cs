using System;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// The two things a player does to a SHIP while planning a battle: pin it to the flotilla it is
    /// in, and move it to another one.
    ///
    /// Both are drawn on the advanced setup's 3D arena and nowhere else. A ship is a 24-pixel chip on
    /// a flotilla card; double-clicking the chip pins it (a white glow, and nothing written down),
    /// and dragging it onto another card moves it. Neither gesture exists on the roster line the
    /// keyboard walks, so the roster line is where the mod puts them - and everything below is the
    /// bridge between the two surfaces.
    ///
    /// Nothing here declares a node or speaks: <see cref="Screens.AdvancedEncounterPlayScreen"/> does
    /// that. This is the identity that ties a roster row to a chip, and the game's own two commands.
    ///
    /// THE COMMIT IS THE GAME'S OWN FUNCTION. A move is
    /// <c>EncounterPlayFlotillaCardContainerInteractive.OnDropShipItem</c>, which hit-tests the
    /// dragged chip's centre against each card's rectangle and then does the whole of the work -
    /// the plain add, the refusal, and the juggernaut swap that is the one branch a refusal can turn
    /// into a success. So <see cref="Drop"/> does what a mouse does: it puts the chip where the
    /// player would have dropped it, asks the game, and puts the chip back if the answer was no
    /// (which is exactly what <c>EncounterPlayShipItemInteractive.OnDragCompletedCb</c> :90-105
    /// does). Not one of those rules is restated here.
    /// </summary>
    public static class BattleShipMoves
    {
        /// <summary>What the carried thing IS, so a ship being arranged into a flotilla cannot be
        /// dropped into a fleet list, a hull slot or a planet's population - and nothing else can be
        /// dropped onto a flotilla card. Its own kind rather than the fleet screen's "ship": the two
        /// carry different cargo (a <c>Ship</c> there, a battle's own setup record here) and only the
        /// kind keeps a carry started on one screen from being offered a target on the other.</summary>
        public const string Kind = "battle-ship";

        /// <summary>
        /// The battle's own record for the ship a roster row is drawing.
        ///
        /// Read off the row's wrapper rather than off <c>BattleShipItem.ShipSetup</c>, which is a
        /// public property the prefab never fills: <c>BattleShipItem.Bind</c> :30-38 sets
        /// <c>GuiBattleShip</c> and nothing else, so that field reads null on every row of every
        /// battle surface (measured 2026-08-29). The wrapper's <c>ShipData</c> IS the setup on this
        /// window - one object, held by the row and by the chip and listed in
        /// <c>FlotillaSetups[i].ShipSetups</c>, so identity is a reference and never a GUID match
        /// (measured: both rows of the fixture's flotilla resolve to <c>FlotillaSetups[1]</c>'s two
        /// entries).
        /// </summary>
        public static EncounterShipSetup SetupOf(BattleShipItem item)
        {
            try
            {
                GuiBattleShip ship = item == null ? null : item.GuiBattleShip;
                return ship == null ? null : ship.ShipData as EncounterShipSetup;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the ship is pinned to the flotilla it is in.</summary>
        public static bool Locked(EncounterShipSetup setup)
        {
            try
            {
                return setup != null && setup.LockedInFlotilla;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Pin the ship, or let it go: the game's own double click, which is one line
        /// (<c>EncounterPlayShipItemInteractive.OnDoubleClickCb</c> :112-115).
        ///
        /// The property is the whole command. Its setter pins or clears
        /// <c>PreferredFlotillaIndex</c> and raises <c>LockedInFlotillaChanged</c>
        /// (<c>EncounterShipSetup</c> :48-67), which the chip is subscribed to and answers by
        /// refreshing itself - so the white glow follows without the mod touching a tint. The
        /// <c>Refresh()</c> the game's own handler calls after the assignment is that same refresh
        /// asked for twice.
        /// </summary>
        public static void ToggleLock(EncounterShipSetup setup)
        {
            try
            {
                setup.LockedInFlotilla = !setup.LockedInFlotilla;
            }
            catch (Exception e)
            {
                Log.Warn("battle setup: locking a ship in its flotilla threw: " + e);
            }
        }

        /// <summary>Which chip on the arena is this ship - the object every command below acts on.
        /// Found by walking the cards the window is drawing, because the chips are pooled by the
        /// container and only a card knows which of them it is currently holding.</summary>
        public static EncounterPlayShipItemInteractive Chip(
            EncounterPlayFlotillaCard3DInteractive[] cards,
            EncounterShipSetup setup
        )
        {
            try
            {
                for (int i = 0; cards != null && setup != null && i < cards.Length; i++)
                {
                    EncounterPlayFlotillaCard3DInteractive card = cards[i];
                    EncounterPlayShipItem[] ships = card == null ? null : card.AllShips;
                    for (int j = 0; ships != null && j < ships.Length; j++)
                    {
                        EncounterPlayShipItemInteractive chip =
                            ships[j] as EncounterPlayShipItemInteractive;
                        if (chip != null && chip.ShipSetup == setup)
                        {
                            return chip;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle setup: looking for a ship's chip threw: " + e);
            }

            return null;
        }

        /// <summary>One press's worth: the ship the row is drawing, picked up under the name the
        /// roster reads it by. Null where the arena is not drawing a chip for it - the enemy's side, a
        /// report rather than a setup - which is what keeps the pick-up word off every roster row the
        /// player cannot rearrange.</summary>
        public static CarryItem Pick(EncounterPlayShipItemInteractive chip, string name)
        {
            return chip == null || Container(chip) == null || string.IsNullOrEmpty(name)
                ? null
                : new CarryItem(chip, name, Kind);
        }

        /// <summary>The container that owns the drop, found the way the chip's own drag-completed
        /// callback finds it (<c>EncounterPlayShipItemInteractive.Container</c> :40-50): through the
        /// card the chip is currently on. Null while the chip is on no card, or on a card whose
        /// container arranges a fleet rather than flotillas - the enemy's side of this same
        /// window.</summary>
        public static EncounterPlayFlotillaCardContainerInteractive Container(
            EncounterPlayShipItemInteractive chip
        )
        {
            try
            {
                EncounterPlayShipCard3D card = chip == null ? null : chip.Card;
                return card == null
                    ? null
                    : card.Container as EncounterPlayFlotillaCardContainerInteractive;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether this card would take this chip right now - the game's own
        /// <c>CanAddShipItem</c>, which is <c>EncounterFlotillaSetup.CanAddShip</c> and therefore
        /// answers no for a locked flotilla, for one already holding the ship, and for one the ship
        /// does not fit. A pure query, which is what a drop-target INDICATION has to be.
        ///
        /// It is deliberately the WEAKER half of what <see cref="Drop"/> can do: the juggernaut swap
        /// branch succeeds where this says no, so a juggernaut is carried to a full flotilla with no
        /// "drop target" word and the drop still works when the key is pressed (the activation key
        /// asks only the cargo's KIND - <c>CarryActions.Activate</c>). Advertising that branch would
        /// mean re-composing the container's own dispatch, including the source-card case its loop
        /// only survives because a non-juggernaut never reaches it, and the mod does not restate the
        /// game's rules.</summary>
        public static bool Accepts(
            EncounterPlayFlotillaCard3DInteractive card,
            EncounterPlayShipItemInteractive chip
        )
        {
            try
            {
                return card != null && chip != null && card.CanAddShipItem(chip);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Put the ship down on <paramref name="card"/>, through the game's own drop.
        ///
        /// <c>OnDropShipItem</c> hit-tests <c>shipItem.AgeTransform.Position.center</c> against every
        /// card's <c>CardRect</c> and acts on the first that contains it
        /// (<c>EncounterPlayFlotillaCardContainerInteractive</c> :14-70), so the mod's whole
        /// contribution is putting the chip where a mouse would have let go of it: the chip's own
        /// rectangle, moved so its centre is the target card's. Both rectangles are in the arena's
        /// own coordinates, which is why one can simply be centred on the other.
        ///
        /// On a refusal the chip goes back exactly where it was, which is the game's own repair
        /// (<c>OnDragCompletedCb</c> :90-105) and not a rule of the mod's: a refused drop must leave
        /// the picture untouched. On a success the container re-lays the cards out and the saved
        /// rectangle is stale by design.
        /// </summary>
        public static bool Drop(
            EncounterPlayFlotillaCard3DInteractive card,
            EncounterPlayShipItemInteractive chip
        )
        {
            EncounterPlayFlotillaCardContainerInteractive container = Container(chip);
            if (card == null || container == null)
            {
                return false;
            }

            Rect before = chip.AgeTransform.Position;
            try
            {
                Rect aimed = before;
                aimed.center = card.CardRect.center;
                chip.AgeTransform.Position = aimed;
                if (container.OnDropShipItem(chip))
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle setup: dropping a ship on a flotilla threw: " + e);
            }

            chip.AgeTransform.Position = before;
            return false;
        }

        /// <summary>What the mod says about a flotilla that would not take the ship: the game's own
        /// sentence for why, which it writes under the card's number and nowhere else ("Unlocked at 5
        /// CP and 2 Ships"). Only for a flotilla the battle has LOCKED - the same label on an open one
        /// says what its minimum is ("Minimum 1 CP"), which is not a reason for anything and would be
        /// a refusal explained by an unrelated fact. Null there, and the carry's own generic sentence
        /// answers instead.</summary>
        public static string Unlock(EncounterPlayFlotillaCard3D card)
        {
            try
            {
                EncounterPlayFlotillaCard3DInteractive interactive =
                    card as EncounterPlayFlotillaCard3DInteractive;
                EncounterFlotillaSetup setup =
                    interactive == null ? null : interactive.EncounterFlotillaSetup;
                return setup == null || setup.IsFlotillaValid
                    ? null
                    : AgeWidgets.DrawnLabel(card.CommandPointsLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The whole of a drop, as the carry asks for it: the game's own answer, worded
        /// either as what moved and where it went, or as the flotilla's own reason for saying no. The
        /// carry survives a refusal, so the player can walk to the next flotilla and try there.
        /// </summary>
        public static DropResult Landed(
            EncounterPlayFlotillaCard3DInteractive card,
            CarryItem held,
            string destination
        )
        {
            EncounterPlayShipItemInteractive chip =
                held == null ? null : held.Cargo as EncounterPlayShipItemInteractive;
            if (chip == null || !Drop(card, chip))
            {
                return DropResult.Refused(Unlock(card));
            }

            return DropResult.Done(
                ModStrings.Format(ModStrings.BattleShipMoved, held.Name, destination)
            );
        }
    }
}

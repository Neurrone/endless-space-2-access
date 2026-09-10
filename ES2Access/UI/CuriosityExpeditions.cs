using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// The curiosity icons a COLONY's planet card draws, and the one thing the game puts on a
    /// modified click of them: queue the expedition at the HEAD of that system's construction queue.
    ///
    /// Two of the game's own cards do it, with the same two orders in the same order
    /// (<c>PlanetCard.OnExploreCuriosity</c> :724-762 and
    /// <c>PlanetLabelsWindow_SystemManagement.OnExploreCuriosity</c> :228-271): post
    /// <c>OrderQueuePlanetCuriosityExpedition</c>, and once the ticket comes back Processed, move the
    /// construction it created to index 0 with <c>OrderMoveConstruction</c>. It has to be WIRED
    /// rather than left to the modified click's fall back, because the gesture's chord is now
    /// Ctrl+Shift+Enter and the game's handler branches on <c>Input.IsAltKeyDown()</c>
    /// (<c>docs/interaction.md</c>).
    ///
    /// Only the COLONY interaction. The same prefab is bound a second way - to a fleet in orbit
    /// (<c>PlanetCuriosityItem.CuriosityInteraction.Fleet</c>, the galaxy's orbital card), where the
    /// search happens at once and there is no queue and no head to put anything at. The item itself
    /// says which it is, so the wiring asks the widget rather than the screen and cannot be attached
    /// to the wrong card.
    /// </summary>
    public static class CuriosityExpeditions
    {
        // Reused rather than allocated per ask: the fallback below is resolved while the player
        // stands on the icon, and the list is read out again before anything else can touch it.
        private static readonly List<FailureInfo> Refusals = new List<FailureInfo>();

        /// <summary>The curiosity this widget is DRAWING - bound, whichever of the two interactions it
        /// was bound for - or null for a widget that is not a curiosity at all.</summary>
        public static PlanetCuriosityItem Curiosity(AgeTransform widget)
        {
            try
            {
                PlanetCuriosityItem item =
                    widget == null ? null : widget.GetComponent<PlanetCuriosityItem>();
                return item != null && item.GuiCuriosity != null ? item : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The same item if it is the COLONY-mode one - the card whose click queues a
        /// construction - and null for the fleet-mode one, where the search happens at once.</summary>
        public static PlanetCuriosityItem Colony(PlanetCuriosityItem item)
        {
            return item != null
                && item.Interaction == PlanetCuriosityItem.CuriosityInteraction.System
                && item.Actor as ColonizedStarSystem != null
                ? item
                : null;
        }

        /// <summary>Whether the game is refusing this curiosity BECAUSE the expedition power - the
        /// empire's, or that of the fleet in orbit - is too low: the one refusal the card turns into a
        /// padlock drawn over the icon (<c>PlanetCuriosityItem.ShowLockIfNeeded</c>).
        ///
        /// The DRAWN padlock is the answer wherever the card has one, so what the keyboard hears is
        /// what the card shows, for the price of one bool. A prefab variant that carries no Lock
        /// widget leaves nothing to read, and there the game's own refusal test is asked instead - the
        /// same question the scanner asks of the same two flags, never a re-derivation of the
        /// power.</summary>
        public static bool LowExpeditionPower(PlanetCuriosityItem item)
        {
            try
            {
                if (item == null || item.GuiCuriosity == null)
                {
                    return false;
                }

                if (item.Lock != null)
                {
                    // A DIFFERENT WIDGET from the one the node stands on: the padlock is the card's
                    // own child, shown over the icon in place of it, and whether the game turned it
                    // on is the content being read - not whether this node's icon is drawn, which
                    // the gate has already asked of the whole ancestry.
                    return item.Lock.Visible;
                }

                Refusals.Clear();
                Fleet fleet = item.Actor as Fleet;
                ColonizedStarSystem system = item.Actor as ColonizedStarSystem;
                Empire empire = Gui.PlayerEmpire;
                if (fleet != null)
                {
                    empire = fleet.Empire;
                }
                else if (system != null)
                {
                    empire = system.Empire;
                }

                item.GuiCuriosity.Curiosity.CanBeSearched(empire, fleet, Refusals);
                return LowExpeditionPower(Refusals);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether the game recorded the expedition-power refusal among these - the two flags
        /// <c>ShowLockIfNeeded</c> looks for, in the one place that knows which they are.</summary>
        public static bool LowExpeditionPower(List<FailureInfo> refusals)
        {
            for (int i = 0; i < refusals.Count; i++)
            {
                if (
                    refusals[i].Flag == FailureFlags.EmpireExpeditionPowerTooLow
                    || refusals[i].Flag == FailureFlags.FleetExpeditionPowerTooLow
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Queue this curiosity's expedition at the FRONT of its system's queue - the game's
        /// own alt-click, order for order.
        ///
        /// Through the game's own gate first: the icon stays CLICKABLE while the game refuses it (no
        /// fleet in orbit, one already queued, not enough expedition power) and writes the reason into
        /// its tooltip, and a click the mouse would not carry out must not become an order because the
        /// chord posts one directly rather than replaying the click.</summary>
        public static void QueueFirst(AgeTransform widget, PlanetCuriosityItem item)
        {
            try
            {
                if (!AgeWidgets.Offered(widget))
                {
                    return;
                }

                ColonizedStarSystem system = item.Actor as ColonizedStarSystem;
                Curiosity curiosity = item.GuiCuriosity.Curiosity;
                PlanetCuriosityExpeditionDefinition definition =
                    curiosity.GetBestPlanetCuriosityExpeditionDefinition(Gui.PlayerEmpire);
                if (system == null || definition == null)
                {
                    return;
                }

                PlayerController player = Gui.GetActivePlayerController();
                OrderQueuePlanetCuriosityExpedition order =
                    new OrderQueuePlanetCuriosityExpedition(
                        player.Empire.Index,
                        system,
                        curiosity,
                        definition
                    );
                Ticket ignored;
                player.PostOrder(order, out ignored, (sender, args) => MoveToHead(args, system));
            }
            catch (Exception e)
            {
                Log.Warn("curiosity: queueing an expedition at the head threw: " + e);
            }
        }

        private static void MoveToHead(TicketRaisedEventArgs args, ColonizedStarSystem system)
        {
            try
            {
                if (args.Result != PostOrderResponse.Processed)
                {
                    return;
                }

                OrderQueuePlanetCuriosityExpedition queued =
                    args.Order as OrderQueuePlanetCuriosityExpedition;
                PlayerController player = Gui.GetActivePlayerController();
                player.PostOrder(
                    new OrderMoveConstruction(
                        player.Empire.Index,
                        system.GUID,
                        queued.ConstructionGameEntityGUID,
                        0
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("curiosity: moving an expedition to the head threw: " + e);
            }
        }
    }
}

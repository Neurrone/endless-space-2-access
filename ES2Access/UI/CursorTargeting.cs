using System;
using System.Reflection;
using Amplitude.Unity.Framework;
using Amplitude.Unity.View;
using ES2Access.Core.Util;
using UnityEngine;
using Cursor = Amplitude.Unity.View.Cursor;

namespace ES2Access.UI
{
    /// <summary>
    /// The orders the game gives in two steps: a button puts the MAP into a targeting mode and the next
    /// left click on the map is the order's target. Launch a probe, take a system, fire the obliterator,
    /// mark a system for the pirates, call an honour action, place a time bubble, ask an ally to
    /// coordinate, start a hacking program - ten cursors, and until now the second step was a mouse
    /// click with no keyboard equivalent at all.
    ///
    /// The keyboard's second step is ENTER on the map node the player wants, because that is what the
    /// mouse's second step is: measured in every cursor class, while one of these modes is up the LEFT
    /// click means CONFIRM and nothing else - each cursor overrides <c>OnCursorClick</c> and none of them
    /// calls the plain galaxy cursor's own handler, so the click that would normally select a fleet or
    /// zoom in on a system does neither. Enter on a node is therefore the same displacement the mouse
    /// already lives with, not a new gesture and not a new key.
    ///
    /// Two shapes of confirm, and the difference is the game's, not ours:
    ///
    /// - Most cursors read the TARGET under the pointer (a <c>GalaxyStarSystemCursorTarget</c>) and hand
    ///   it to their own validation. For those, the mod hands the focused node's own cursor target to the
    ///   cursor's own click handler, so the game decides what is a legal target and what a click on it
    ///   does. Nothing about the order is re-implemented here.
    /// - Two of them - the probe and the ally coordination pin - ignore targets entirely and read the
    ///   POINTER's position on the world plane, because what they aim at is a point in empty space rather
    ///   than a thing (a probe is launched in a DIRECTION and flies until its lifetime runs out). Those
    ///   two cannot be driven through their own handler without moving the physical mouse, so the mod
    ///   posts the same order they post, aimed at the node the player is on.
    ///
    /// Escape and the right click stay the game's own way out; nothing here touches them, and the mod
    /// does not claim Escape on the galaxy.
    /// </summary>
    public static class CursorTargeting
    {
        /// <summary>
        /// Whether the map is in one of those modes, waiting for a target.
        ///
        /// Asked of the cursor the way the game asks it: <c>HasUserInstructions</c> is true for exactly
        /// the ten targeting cursors and false for the plain galaxy cursor and the fleet-selection one,
        /// and it is the same test the game shows its own "click a target" banner by
        /// (<c>GuiManager</c>:1552) and the mod already announces the mode from
        /// (<c>GlobalHud.AnnounceCursorMode</c>). A cursor added by a later patch is covered by
        /// construction: it cannot ask the player to click a target without answering this.
        /// </summary>
        public static bool Aiming
        {
            get
            {
                try
                {
                    Cursor cursor = Gui.GetCursor();
                    return cursor != null && cursor.HasUserInstructions;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Confirm the waiting order at <paramref name="node"/> - the game's own left click on that place
        /// on the map. Answers whether a mode was waiting, which is the caller's signal that Enter
        /// belonged to the mode and not to whatever the node does the rest of the time.
        ///
        /// True is also the answer when the game REFUSES the target: a system the obliterator cannot
        /// reach, a fleet with no probes left. The refusal is the game's own (each cursor asks
        /// <c>CanBeExecuted</c> for itself) and is silent, exactly as the same refused mouse click is
        /// silent, and the mode stays up so the player can pick somewhere else. What the mode's end
        /// sounds like is the one place that watches it - the HUD's cursor-mode announcement.
        /// </summary>
        public static bool ConfirmAt(GameNode node)
        {
            Cursor cursor;
            try
            {
                cursor = Gui.GetCursor();
                if (cursor == null || !cursor.HasUserInstructions || node == null)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                ProbeLaunchingCursor probe = cursor as ProbeLaunchingCursor;
                if (probe != null)
                {
                    LaunchProbe(probe, node);
                    return true;
                }

                CoordinationRequestCursor request = cursor as CoordinationRequestCursor;
                if (request != null)
                {
                    AskToCoordinate(request, node);
                    return true;
                }

                Click(cursor, TargetOf(node));
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: confirming a cursor target threw: " + e);
            }

            return true;
        }

        /// <summary>The map's own object for aiming at a node - the collider a mouse would be over. Null
        /// while the map has not built one, which is the same answer as a click on empty sky.</summary>
        private static CursorTarget TargetOf(GameNode node)
        {
            try
            {
                IGalaxyEntityFactoryService entities =
                    Services.GetService<IGalaxyEntityFactoryService>();
                GameObject entity = entities == null ? null : entities[node.GUID];
                return entity == null
                    ? null
                    : entity.GetComponent<GalaxyStarSystemCursorTarget>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The cursor's own click handler, with the node's target where the hovered ones would be.
        ///
        /// Reflected because the engine declares the handler protected and its public face
        /// (<c>ICursor</c>) is internal to the game's own assembly - and it is the whole point to call
        /// the game's handler rather than a copy of it, since that is where each mode's target rules,
        /// its refusals, its sound and its own decision about whether the mode is over now live. The
        /// engine's manager wraps this call with a selection pass which these cursors switch off
        /// themselves (<c>ValidateSelection</c> false on every one of them), so the wrapper has nothing
        /// to contribute.
        ///
        /// A null target is a click on nothing: consumed, and the mode stays.
        /// </summary>
        private static void Click(Cursor cursor, CursorTarget target)
        {
            if (target == null || Clicked == null)
            {
                return;
            }

            Clicked.Invoke(
                cursor,
                new object[] { MouseButton.Left, new CursorTarget[] { target } }
            );
        }

        private static readonly MethodInfo Clicked = ClickMethod();

        private static MethodInfo ClickMethod()
        {
            try
            {
                return typeof(Cursor).GetMethod(
                    "OnCursorClick",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(MouseButton), typeof(CursorTarget[]) },
                    null
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the engine's cursor click handler was not found: " + e);
                return null;
            }
        }

        /// <summary>
        /// Launch a probe towards the node the player is on.
        ///
        /// A probe has no target: the order carries the DIRECTION it leaves in
        /// (<c>ProbeLaunchingCursor.OnCursorClick</c> :140-153 normalises the vector from the fleet to
        /// wherever the mouse is), so aiming it means naming a place to head for, and the places a
        /// keyboard player can name are the map's own nodes. Everything else is the cursor's own
        /// sequence in the same order: ask the action whether it can be executed at all, post it, and
        /// leave the mode when the probe just spent was the last one the fleet had - which is the game's
        /// rule, not ours (:165).
        /// </summary>
        private static void LaunchProbe(ProbeLaunchingCursor cursor, GameNode node)
        {
            Fleet fleet = cursor.ProbeOriginFleet;
            if (fleet == null || fleet.IsDestroyed)
            {
                return;
            }

            Vector3 heading = ((Vector3)node.GalaxyPosition - (Vector3)fleet.GalaxyPosition).normalized;
            EntityActionDefinition definition = EntityActionDefinition.GetEntityActionDefinition(
                LaunchProbeFleetAction.ActionDefinitionReference
            );
            if (definition == null)
            {
                return;
            }

            EntityActionContext context = definition.BuildEntityActionContext(fleet, heading);
            if (context == null || !definition.CanBeExecuted(fleet, context))
            {
                return;
            }

            Gui.PlaySound(1988358484u);
            PostOrder(new OrderEntityAction(fleet.Empire.Index, definition, fleet, context));
            if (ProbesLeft(fleet) <= 1f)
            {
                cursor.SwitchToGalaxyCursor();
            }
        }

        /// <summary>How many probes the fleet is carrying, counted the way the cursor counts them: the
        /// probe stock of every ship in it, the hero's included, floored per ship.</summary>
        private static float ProbesLeft(Fleet fleet)
        {
            DepartmentOfTheTreasury treasury = fleet.Empire.GetAgency<DepartmentOfTheTreasury>();
            if (treasury == null)
            {
                return 0f;
            }

            float probes = 0f;
            System.Collections.IList ships = fleet.ShipsIncludingHero as System.Collections.IList;
            for (int i = 0; i < (ships == null ? 0 : ships.Count); i++)
            {
                Ship ship = ships[i] as Ship;
                float stock;
                if (
                    ship != null
                    && treasury.TryGetResourceStockValue(
                        ship,
                        DepartmentOfTheTreasury.Resources.ShipProbe,
                        out stock,
                        true
                    )
                )
                {
                    probes += Mathf.Floor(stock);
                }
            }

            return probes;
        }

        /// <summary>
        /// Pin an ally coordination request on the node the player is on.
        ///
        /// The other pointer-aimed mode (<c>CoordinationRequestCursor.OnCursorClick</c> :155-166): the
        /// order carries a bare galaxy position, so a node's own position is what it is given. The mode
        /// ends either way, as it does for the mouse - a pin is placed once and the cursor goes back.
        /// </summary>
        private static void AskToCoordinate(CoordinationRequestCursor cursor, GameNode node)
        {
            if (cursor.RequestType != CoordinationRequest.CoordinationRequestType.Undefined)
            {
                PostOrder(
                    new OrderCreateCoordinationRequest(
                        Gui.PlayerEmpire.Index,
                        cursor.RequestType,
                        node.GalaxyPosition,
                        string.Empty
                    )
                );
            }

            cursor.SwitchToGalaxyCursor();
        }

        // Typed on the ENGINE's order rather than the game's own subclass: the coordination request
        // derives straight from the engine's, and only the fleet-action orders sit under the game's.
        private static void PostOrder(Amplitude.Unity.Game.Orders.Order order)
        {
            PlayerController controller = Gui.GetActivePlayerController();
            if (controller != null)
            {
                controller.PostOrder(order);
            }
        }
    }
}

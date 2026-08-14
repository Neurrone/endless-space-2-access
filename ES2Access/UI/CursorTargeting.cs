using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude.Unity.Framework;
using Amplitude.Unity.View;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using UnityEngine;
using Cursor = Amplitude.Unity.View.Cursor;

namespace ES2Access.UI
{
    /// <summary>
    /// The orders the game gives in two steps: a button puts the MAP into a targeting mode and the next
    /// left click on the map is the order's target. Launch a probe, take a system, fire the obliterator,
    /// mark a system for the pirates, call an honour action, place a time bubble, ask an ally to
    /// coordinate, start a hacking program, plot a hacking operation - nine cursor classes (counted:
    /// eight declare <c>HasUserInstructions</c> and <c>EntityActionCursor</c>'s is shared by the pirate
    /// mark and the honour action), and until now the second step was a mouse
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
    /// The keyboard's way BACK out is backslash, because that is the map's own right click and the right
    /// click is what these modes answer with (<see cref="Contextual"/>). It is not always a cancel - the
    /// game gives each cursor its own meaning for it - so the key is handed to the cursor rather than
    /// wired to a cancel of ours.
    ///
    /// Escape stays the game's own way out, with one exception the game left open
    /// (<see cref="EscapeIsOurs"/>).
    /// </summary>
    public static class CursorTargeting
    {
        /// <summary>
        /// Whether the map is in one of those modes, waiting for a target.
        ///
        /// Asked of the cursor the way the game asks it: <c>HasUserInstructions</c> is true for exactly
        /// the nine targeting cursors and false for the plain galaxy cursor and the fleet-selection one,
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
        ///
        /// Something HAPPENING is not silent, though, and that is the asymmetry the two pointer-aimed
        /// modes need: the game answers a launched probe and a placed pin with a sound and a mark drawn
        /// on the map, and without a word for the thing that landed there is nothing to tell a spent
        /// probe from a refused one. The seven cursors driven through their own handler need nothing
        /// here - what a legal target does is the game's own click, sounds and all.
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

        /// <summary>
        /// The map's own RIGHT click while a mode is waiting - the way back out of every one of them, and
        /// the caller's signal that the key belonged to the mode rather than to whatever the map does with
        /// it the rest of the time (send the selected fleets, undo a zoom).
        ///
        /// What it does is the cursor's own business and deliberately not decided here: seven of the nine
        /// cancel the mode outright, a hacking operation being plotted gives back its last waypoint, and
        /// the program picker closes its prompt. All nine answer it from inside their own
        /// <c>OnCursorClick</c>, so handing the button to that one method is the whole implementation -
        /// nothing has to be re-derived when a mode's meaning for it is not a cancel.
        ///
        /// No target is passed, because none of the nine right-click branches reads one (measured in all
        /// nine): a right click on the map means the same thing wherever the pointer is standing.
        /// </summary>
        public static bool Contextual()
        {
            Cursor cursor;
            try
            {
                cursor = Gui.GetCursor();
                if (cursor == null || !cursor.HasUserInstructions)
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
                Send(cursor, MouseButton.Right, Nothing);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the waiting cursor's right click threw: " + e);
            }

            return true;
        }

        /// <summary>
        /// Whether the mod has to answer Escape for the mode that is up, which is true for exactly one
        /// cursor of the nine.
        ///
        /// Escape belongs to the game everywhere else on this page, and for eight of these modes the game
        /// really does answer it: six through <c>GuiManager.HandleInput</c>'s Exit branch (:2103-2120) and
        /// the hacking pair through the scan overlay's own handler. <c>TakeSystemCursor</c> is in neither
        /// list, so its Escape falls all the way through to the pause menu and the player is left in a
        /// mode with the pause menu over it. The mod answers there and nowhere else, and what it does is
        /// that cursor's own right click - the cancel the game did give it
        /// (<c>TakeSystemCursor.cs</c>:95-97).
        /// </summary>
        public static bool EscapeIsOurs
        {
            get
            {
                try
                {
                    return Gui.GetCursor() is TakeSystemCursor;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// What the waiting order would say about this place - the answer the game writes for a mouse
        /// HOVERING it, before anything is committed.
        ///
        /// The mouse gets a whole readout before it clicks and the keyboard was getting none of it: the
        /// obliterator's own hover writes how many turns the shot takes to arrive, which star the system
        /// would be left with and at what odds, and whether the colony is protected against obliteration
        /// at all (<c>ObliteratorFireCursor.OnCursorEnter</c> :93-141) - all of it in front of a shot
        /// that cannot be taken back. The other modes write their REFUSALS the same way ("you cannot
        /// take a system you cannot see"), which is the same question asked of a target that is no good.
        ///
        /// So the mode's own hover is replayed at the node the cursor is on and what the game wrote is
        /// read back. Not spoken: it is a page of detail about a place, and the mode's arrival is
        /// already announced - this is what the player reviews before pressing Enter.
        ///
        /// Where it is read from is the game's own event rather than the panel it draws
        /// (<c>GameOverlayTooltipPanel</c> is a subscriber like any other), so the words are had whether
        /// or not that panel is up, and they are formatted by the very call the panel formats them with
        /// (<c>Gui.FormatFailureInfos</c>) - message first, then the first reason that is not ignorable,
        /// exactly as drawn.
        ///
        /// The hover is ended again straight away, because a hover the pointer is not making is a state
        /// the game has no other way to leave: the exit puts the overlay back to empty and drops the
        /// path preview, which is what the mouse leaving the node does.
        /// </summary>
        public static IList<string> PreviewLines(GameNode node)
        {
            Cursor cursor;
            try
            {
                cursor = Gui.GetCursor();
                if (cursor == null || !cursor.HasUserInstructions || !Reads(cursor))
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }

            CursorTarget target = TargetOf(node);
            if (target == null || Entered == null || Exited == null)
            {
                return null;
            }

            IGuiService gui = Services.GetService<IGuiService>();
            if (gui == null)
            {
                return null;
            }

            CursorTarget[] targets = new CursorTarget[] { target };
            string written = null;
            try
            {
                _written = null;
                gui.OverlayFailureInfosChanged += Wrote;
                try
                {
                    Entered.Invoke(cursor, new object[] { targets });
                }
                finally
                {
                    gui.OverlayFailureInfosChanged -= Wrote;
                    written = _written;
                    _written = null;
                    Exited.Invoke(cursor, new object[] { targets });
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the waiting cursor's target threw: " + e);
                return null;
            }

            return AgeText.Lines(AgeText.Clean(written));
        }

        /// <summary>The last thing the mode wrote to the overlay while the hover was being replayed,
        /// already in the words the panel draws.</summary>
        private static string _written;

        private static void Wrote(object sender, FailureInfosEventArgs args)
        {
            if (args != null)
            {
                _written = Gui.FormatFailureInfos(args.Message, args.FailureInfos);
            }
        }

        /// <summary>
        /// Whether this mode answers a hover at all, and answering it is all it does.
        ///
        /// Asked of the cursor by construction rather than off a list of names: a mode that reports what
        /// a target would mean does it by overriding <c>OnCursorEnter</c>, so declaring one IS the
        /// question - and the two modes that aim at the POINTER rather than at a target (the probe, the
        /// ally pin) declare none, which is the same answer as "nothing to read". Measured over all
        /// nine: seven declare it, and the two that do not are exactly those two.
        ///
        /// The one exception is the hacking OPERATION, whose hover is not only a reading - it remembers
        /// the targets it was handed for its own click to use (<c>HackingOperationCursor</c> :428-436),
        /// so replaying it under the player's cursor would leave the mouse's next click aimed at
        /// somewhere the pointer is not.
        /// </summary>
        private static bool Reads(Cursor cursor)
        {
            if (cursor is HackingOperationCursor)
            {
                return false;
            }

            for (Type kind = cursor.GetType(); kind != null && kind != typeof(Cursor); kind = kind.BaseType)
            {
                if (
                    kind.GetMethod(
                        "OnCursorEnter",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                        null,
                        new Type[] { typeof(CursorTarget[]) },
                        null
                    ) != null
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>What the engine hands a click that is over nothing. Shared and never written to.
        /// </summary>
        private static readonly CursorTarget[] Nothing = new CursorTarget[0];

        /// <summary>The map's own object for aiming at a node - the collider a mouse would be over. Null
        /// while the map has not built one, which is the same answer as a click on empty sky.</summary>
        private static CursorTarget TargetOf(GameNode node)
        {
            if (node == null)
            {
                return null;
            }

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
            if (target == null)
            {
                return;
            }

            Send(cursor, MouseButton.Left, new CursorTarget[] { target });
        }

        /// <summary>One of the mouse's buttons, handed to the cursor's own handler.</summary>
        private static void Send(Cursor cursor, MouseButton button, CursorTarget[] targets)
        {
            if (Clicked == null)
            {
                return;
            }

            Clicked.Invoke(cursor, new object[] { button, targets });
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

        /// <summary>The engine's hover pair, reflected for the same reason its click is: protected on
        /// the engine's cursor, and virtual, so the mode's own override is what runs.</summary>
        private static readonly MethodInfo Entered = HoverMethod("OnCursorEnter");

        private static readonly MethodInfo Exited = HoverMethod("OnCursorExit");

        private static MethodInfo HoverMethod(string name)
        {
            try
            {
                return typeof(Cursor).GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(CursorTarget[]) },
                    null
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the engine's cursor hover handler was not found: " + e);
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
            float carried = ProbesLeft(fleet);
            Say(Launched(node, carried));
            if (carried <= 1f)
            {
                cursor.SwitchToGalaxyCursor();
            }
        }

        /// <summary>
        /// What a launch just did, in the mod's own words: the game answers one with a sound and a
        /// probe drawn leaving the fleet, and a player who cannot see the map has no way to tell that
        /// from the silent refusal of a target the order would not accept.
        ///
        /// The count is the stock MINUS the one just spent, because the order has only been posted:
        /// it is executed by the session, not by this call, so the stock read here is still the stock
        /// before the launch (which is exactly why the game's own click tests it against 1 rather than
        /// 0 to decide the mode is over - <c>ProbeLaunchingCursor.OnCursorClick</c> :154-165). A stock
        /// that could not be read at all is said as nothing rather than as a number.
        /// </summary>
        private static string Launched(GameNode node, float carried)
        {
            string place = FleetRoute.Named(node) ?? ModStrings.Get(ModStrings.FleetUnexploredSystem);
            int left = (int)carried - 1;
            if (left < 0)
            {
                return ModStrings.Format(ModStrings.GalaxyProbeLaunched, place);
            }

            return ModStrings.Format(
                left == 1 ? ModStrings.GalaxyProbeLaunchedOne : ModStrings.GalaxyProbeLaunchedMany,
                place,
                left
            );
        }

        /// <summary>
        /// How many probes the fleet is carrying, counted the way the cursor counts them: the probe
        /// stock of every ship in it, the hero's included, floored per ship. Negative where the empire
        /// has no treasury to ask or the fleet no ship list, which is not the same answer as none
        /// carried - and is what keeps a launch from claiming a made-up number.
        ///
        /// Walked as the enumerable it is. <c>Garrison.ShipsIncludingHero</c> is an
        /// <c>IEnumerable&lt;Ship&gt;</c> written as a yield iterator, so it is no kind of list at all:
        /// casting it to one answered null, silently made every fleet carry zero probes, and ended the
        /// mode after a single launch however many probes were left.
        /// </summary>
        private static float ProbesLeft(Fleet fleet)
        {
            DepartmentOfTheTreasury treasury = fleet.Empire.GetAgency<DepartmentOfTheTreasury>();
            IEnumerable<Ship> ships = fleet.ShipsIncludingHero;
            if (treasury == null || ships == null)
            {
                return -1f;
            }

            float probes = 0f;
            foreach (Ship ship in ships)
            {
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
                Say(
                    ModStrings.Format(
                        ModStrings.GalaxyCoordinationRequested,
                        FleetRoute.Named(node) ?? ModStrings.Get(ModStrings.FleetUnexploredSystem)
                    )
                );
            }

            cursor.SwitchToGalaxyCursor();
        }

        /// <summary>What the confirm just did, said out loud - interrupting, because it is the answer to
        /// a key the player has this instant pressed. The mode's own end is announced separately and
        /// queued (<c>GlobalHud.AnnounceCursorMode</c>), so a launch that spends the last probe says
        /// what it did first and that the mode is over after.</summary>
        private static void Say(string line)
        {
            Voice.Say(line, true);
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

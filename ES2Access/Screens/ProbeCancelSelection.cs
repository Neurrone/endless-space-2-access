using System;
using System.Reflection;
using ES2Access.Core.Util;
using ES2Access.UI;
using HarmonyLib;

namespace ES2Access.Screens
{
    /// <summary>
    /// Cancelling a probe launch gives the fleet panel back to the fleet that armed it.
    ///
    /// Every way out of the launch-probe mode runs one method - Escape
    /// (<c>GuiManager.cs:2105-2109</c>), the mouse's right click and the fleet being teleported
    /// (<c>ProbeLaunchingCursor.cs:177-197</c>), and the last charge being spent on a successful
    /// launch (<c>:165-174</c>) - and that method re-selects the DOCKING SLOT the fleet is parked in
    /// rather than the fleet (<c>ProbeLaunchingCursor.SwitchToGalaxyCursor</c> :55-71). Arming had
    /// hidden the panel, and hiding it empties the slot's selection
    /// (<c>FleetsScreen.OnBeginHide</c> :925-943), so the panel comes back with
    /// <c>RefreshGarrisonSelection</c> defaulting POSITIONALLY - <c>Garrisons[0]</c>, or
    /// <c>[1]</c> when <c>[0]</c> is a hangar (<c>FleetsScreen.cs:1116-1129, 1364-1382</c>). At a
    /// system holding more than one of the player's fleets the panel therefore reopens for the
    /// wrong fleet, and the player who cancelled is still thinking about the one they were
    /// commanding.
    ///
    /// So the game's own method is finished rather than worked around: the origin fleet is
    /// re-selected through the window's own public API, which repairs the MOUSE's cancel too
    /// instead of giving the keyboard a private answer (owner-preferred, <c>docs/fleets.md</c>).
    /// <c>SelectIdleFleet</c> is the call because it covers both frames this can run on: the panel
    /// is normally still hidden here, and it remembers the fleet for the showing the slot selection
    /// is about to cause (<c>FleetsScreen.cs:672-681, 910-919</c>).
    ///
    /// Only when the game really selected the slot. Its own condition for that is a visible docking
    /// slot holding the fleet, and where there is none it swaps to the plain map cursor and opens
    /// nothing - a fleet remembered there would be selected on some later, unrelated showing of the
    /// panel.
    ///
    /// The fleet is read on the way IN. Selecting the slot swaps the cursor, and swapping it
    /// deactivates the probe cursor, which nulls <c>ProbeOriginFleet</c> on its way out
    /// (<c>ProbeLaunchingCursor.cs:113-125</c>) - all of it synchronously, so a postfix asking the
    /// cursor who armed it is told "nobody" (measured 2026-08-26: the field read null straight after
    /// the call). Hence the prefix, whose whole job is to hand the fleet across.
    ///
    /// This is the game's method alone: <c>SwitchToGalaxyCursor</c> exists on no shared base, and no
    /// other targeting cursor re-selects anything on cancel (<c>ObliteratorFireCursor</c> :69-90 and
    /// the shared Exit branch both just change the cursor), so there is nothing here to generalize.
    /// </summary>
    internal static class ProbeCancelSelection
    {
        private static Harmony _harmony;

        /// <summary>Whether the patch is in place - what the teardown check reads.</summary>
        public static bool Installed
        {
            get { return _harmony != null; }
        }

        /// <summary>Who is patching the probe cursor's cancel right now (see <see cref="ModPatches"/>).
        /// </summary>
        internal static string[] Owners()
        {
            return ModPatches.Owners(Cancel(), false);
        }

        private static MethodInfo Cancel()
        {
            return AccessTools.Method(
                typeof(ProbeLaunchingCursor),
                "SwitchToGalaxyCursor",
                Type.EmptyTypes
            );
        }

        public static void Install()
        {
            Remove();

            // A unique id per load, per repo convention: a fixed one lets the UnpatchSelf of the
            // assembly a reload replaced strip this load's patches.
            Harmony harmony = new Harmony(
                "endless.space2.access.probecancel." + Guid.NewGuid().ToString("N")
            );

            try
            {
                MethodInfo cancel = Cancel();
                if (cancel == null)
                {
                    throw new MissingMethodException(
                        typeof(ProbeLaunchingCursor).FullName,
                        "SwitchToGalaxyCursor"
                    );
                }

                harmony.Patch(
                    cancel,
                    new HarmonyMethod(
                        typeof(ProbeCancelSelection).GetMethod(
                            "RememberTheActingFleet",
                            BindingFlags.Static | BindingFlags.NonPublic
                        )
                    ),
                    new HarmonyMethod(
                        typeof(ProbeCancelSelection).GetMethod(
                            "KeepTheActingFleet",
                            BindingFlags.Static | BindingFlags.NonPublic
                        )
                    )
                );
                _harmony = harmony;
            }
            catch (Exception e)
            {
                // Unpatched, a cancel at a multi-fleet system hands the panel to the slot's first
                // fleet - which is what the game has always done, and which the panel announces.
                Log.Error("the probe cancel's selection could not be patched: " + e);
                try
                {
                    harmony.UnpatchSelf();
                }
                catch (Exception undo)
                {
                    Log.Warn("and the partial patch could not be undone: " + undo.Message);
                }
            }
        }

        public static void Remove()
        {
            Harmony harmony = _harmony;
            _harmony = null;
            if (harmony == null)
            {
                return;
            }

            try
            {
                harmony.UnpatchSelf();
            }
            catch (Exception e)
            {
                Log.Error("the probe cancel's selection could not be unpatched: " + e);
            }
        }

        /// <summary>Who armed the mode, caught before the method's own work can forget it.</summary>
        private static void RememberTheActingFleet(ProbeLaunchingCursor __instance, out Fleet __state)
        {
            __state = __instance == null ? null : __instance.ProbeOriginFleet;
        }

        /// <summary>Runs after the game's own slot selection, and only decides who that slot is
        /// showing.</summary>
        private static void KeepTheActingFleet(Fleet __state)
        {
            try
            {
                Fleet fleet = __state;
                if (fleet == null || fleet.IsDestroyed)
                {
                    return;
                }

                IVisibleDockingSlotRepositoryService slots =
                    Amplitude.Unity.Framework.Services.GetService<IVisibleDockingSlotRepositoryService>();
                if (slots == null || slots.GetDockingSlotWithFleet(fleet) == null)
                {
                    // The game changed the cursor instead of selecting anything, so there is no
                    // panel coming and nothing to aim it at.
                    return;
                }

                global::FleetsScreen window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<global::FleetsScreen>(false)
                    : null;
                if (window != null)
                {
                    window.SelectIdleFleet(fleet);
                }
            }
            catch (Exception e)
            {
                Log.Warn("fleets: keeping the probe's own fleet selected threw: " + e);
            }
        }
    }
}

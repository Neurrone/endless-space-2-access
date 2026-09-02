using System;
using ES2Access.Core.Util;
using ES2Access.UI;
using HarmonyLib;

namespace ES2Access.Screens
{
    /// <summary>
    /// The galaxy map has just been ARRIVED AT from somewhere else, with nobody having asked to be
    /// taken to a particular place.
    ///
    /// Two ways in do that, and neither goes through <see cref="GalaxyLocate"/>: a save being loaded
    /// (<c>GalaxyView.ActivateAsync</c>/<c>ReactivateAsync</c> activate the overview themselves on
    /// <c>GetLocalEmpireMainSystemPosition()</c>, the empire's first colony), and coming back out of a
    /// sub-view-level such as a system's management page (<c>StarSystemScreen.HandleInput</c>'s exit
    /// branch, and the zoom slider's own way out, <c>UI.GalaxyViewLevels.StepZoom</c>). In both the
    /// game centres the camera on a system and the tree's cursor is left describing another one.
    ///
    /// Every one of them funnels through <c>GalaxyViewLevel_GalaxyOverview.ActivateAsync</c>, which is
    /// what this watches. It is an iterator method, so the prefix fires when the enumerator is BUILT -
    /// synchronously at the call, frames before the body runs - and that is deliberate: nothing here
    /// reads the arguments. WHERE the game centred is asked of the camera afterwards, by the page
    /// (<c>GalaxyHudScreen</c>), because the parameters do not answer it: the exit branch names the
    /// system whose page was open while passing <c>focusOnLastPosition = true</c>, and the camera then
    /// goes to the position it was at BEFORE the page was opened, which is a different system whenever
    /// the player paged inside the page (measured 2026-08-28: out of Heka's management page by Escape
    /// the camera lands back on Dusay; out of the same page by the zoom slider it lands on Heka).
    ///
    /// What CANNOT reach this, by construction:
    /// - the mod's own camera work. <c>SnapTo</c>/<c>ZoomTo</c>/<c>ZoomToStep</c> drive the overview
    ///   level's own <c>ZoomInOnNode</c> or the camera controller directly, and <c>CenterOn</c> (the
    ///   open-sky slide and the inspect cursor's sweep) is <c>CenterOnPoint</c> - none of them changes
    ///   the view LEVEL, so no activation happens.
    /// - a reveal made while the map is already up. <c>GuiManager.RequestGalaxyOverviewViewLevel</c>
    ///   (:1192-1200) only asks for a level change when the current level is NOT the overview; on the
    ///   overview it slides the camera with <c>CenterOnPoint</c>. That covers the mod's own
    ///   <c>PanTo</c> - which is that same call, and which marks itself
    ///   <see cref="GalaxyLocate.Suppressed"/> as well.
    /// - a reveal made from OFF the map, which does activate the overview: it is an explicit
    ///   "go and look at this" and <see cref="GalaxyLocate"/> has already captured it, so a pending
    ///   request is what this stands down for.
    /// - an excursion to another SCREEN (technology, military, the empire page). Those are windows
    ///   over the same view level; the level is never re-activated, and the page keeps the cursor the
    ///   player left (<c>GalaxyHudScreen.KeepStateOnPop</c>).
    /// </summary>
    internal static class GalaxyOverviewEntry
    {
        private static readonly ModPatch Patches = new ModPatch(
            "galaxyoverviewentry",
            "the galaxy overview's activation"
        );

        /// <summary>Set when the game has activated the galaxy overview for a reason of its own, and
        /// cleared by whoever answers it. Sticky rather than frame-stamped: a save being loaded
        /// activates the view while the loading window is still up, hundreds of frames before the page
        /// exists to answer it.</summary>
        private static bool _entered;

        public static void Install()
        {
            Patches.Install(
                patch =>
                    patch.Hook(
                        AccessTools.Method(
                            typeof(GalaxyViewLevel_GalaxyOverview),
                            "ActivateAsync",
                            new[] { typeof(bool), typeof(object[]) }
                        ),
                        "ActivateAsync",
                        typeof(GalaxyOverviewEntry),
                        "Entering",
                        null
                    )
            );
        }

        public static void Remove()
        {
            Patches.Remove();
            _entered = false;
        }

        /// <summary>Whether the map has been arrived at since this was last asked, and it is nobody's
        /// after that.</summary>
        public static bool Take()
        {
            bool entered = _entered;
            _entered = false;
            return entered;
        }

        /// <summary>Drop an arrival nobody came to answer - the page it was meant for never opened.
        /// </summary>
        public static void Forget()
        {
            _entered = false;
        }

        private static void Entering(bool active)
        {
            try
            {
                // Deactivation is the map being left, and a move the MOD is making is the mod
                // following the player rather than the game leading them (<see cref="GalaxyLocate"/>
                // documents that contract). A "go and look at this" already captured is the other
                // stand-down: that request names a place and lands the cursor announced, and a second
                // opinion about where the player is would move the cursor twice.
                if (!active || GalaxyLocate.Suppressed || GalaxyLocate.Peek() != null)
                {
                    return;
                }

                _entered = true;
            }
            catch (Exception e)
            {
                Log.Warn("noticing the map being arrived at threw: " + e);
            }
        }
    }
}

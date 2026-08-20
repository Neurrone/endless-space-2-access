using System;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// How close the game is looking, said out loud whenever it changes.
    ///
    /// The zoom is not a control the player owns. The wheel moves it, the game's own held PageUp and
    /// PageDown ramp it, a click on a system flies all the way in, opening a system's page and then a
    /// planet's are the two rungs above that - and every one of those changes what the map draws and
    /// therefore what the tree the player is walking says. A sighted player watches that happen. The
    /// mod's own ladder (<see cref="ZoomLadder"/>) reads the rung out only while the player is standing
    /// ON it, which is exactly the one case where they already know.
    ///
    /// So the rung is watched here rather than on a page: it CROSSES pages - the galaxy, the scan
    /// overlay, a system's management page, a planet's - and a watcher owned by any one of them would
    /// fall silent on the very step that changed the page. Pump-scoped, like the save spinner and the
    /// chat: it belongs to nowhere in particular because it happens everywhere.
    ///
    /// Queued, never interrupting: it accompanies whatever the press did rather than talking over it,
    /// which on a step that changes the view level is that page announcing itself.
    ///
    /// The whole per-frame cost is <c>GalaxyViewLevels.ZoomRung</c>, which is two of the engine's
    /// service lookups and a type test - the same reads the ladder's own live value already makes at
    /// 60 Hz while it is focused. Nothing is composed until something is going to be said.
    /// </summary>
    internal sealed class ZoomWatch
    {
        private readonly RungWatch _watch = new RungWatch();

        public void Tick()
        {
            try
            {
                int rungs = GalaxyViewLevels.ZoomRungs;
                if (rungs <= 0)
                {
                    // No galaxy camera at all - the main menu, a battle. The ladder itself has gone,
                    // so the next game's first rung is a first sighting and not a change.
                    _watch.Forget();
                    return;
                }

                int rung = GalaxyViewLevels.ZoomRung;
                if (!_watch.NeedsTelling(rung, GalaxyViewLevels.ChangingLevel, OnTheLadder()))
                {
                    return;
                }

                // The rung, then what the map draws there - a number says how far along the ladder the
                // camera is and nothing about what changed, and the band is the thing that changed.
                // Nothing appended on the rungs that have no band (<see cref="GalaxyViewLevels.ZoomBand"/>).
                Voice.Say(
                    new MessageBuilder()
                        .Fragment(ModStrings.Format(ModStrings.ZoomLevel, rung + 1, rungs))
                        .ListItemForcedComma(GalaxyViewLevels.ZoomBand)
                        .Build(),
                    false
                );
                _watch.Told(rung);
            }
            catch (Exception e)
            {
                Log.Warn("zoom: watching the rung threw: " + e);
            }
        }

        /// <summary>Whether the player is standing on the zoom ladder itself, whose own value reads the
        /// new rung out as it changes.</summary>
        private static bool OnTheLadder()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            ControlId focused = navigator == null ? null : navigator.FocusedKey;
            return focused != null && ZoomLadder.IsLadder(focused);
        }
    }
}

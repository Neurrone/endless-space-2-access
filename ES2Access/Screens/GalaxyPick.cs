using System;
using ES2Access.UI;
using HarmonyLib;

namespace ES2Access.Screens
{
    /// <summary>
    /// The node the map has just been taken IN on by somebody's pointer - and the count that stops the
    /// page believing a record of a picture nobody is looking at.
    ///
    /// The galaxy view has exactly two doors that aim the camera at a node the map is drawing (a third,
    /// <c>GalaxyViewLevel_GalaxyOverview.RestoreZoom</c>, takes it back out again and is counted by
    /// <see cref="Restoring"/>), and
    /// every one of the game's own zoom-without-a-page-change flows goes through one of them
    /// (<c>GalaxyView.cs</c>): <c>SelectGameNode(GalaxyNode)</c>, which either opens a colony of the
    /// player's or force-zooms, and <c>ZoomInOnNode(GalaxyNode)</c>, which force-zooms. The
    /// <c>GameNode</c> overloads of both funnel into these two (<c>SelectGameNode(GameNode)</c> →
    /// <c>SelectNode</c> → <c>ZoomInOnNode</c>), so patching the pair catches the whole census:
    /// - a LEFT CLICK on an explored star and on a wrecked mothership
    ///   (<c>GalaxyCursor.OnCursorClick</c> :150, :165),
    /// - the WHEEL scrolled in past the deepest step over a hovered star
    ///   (<c>GalaxyViewCameraController.HandleScrollwheel</c> :652). **Owner ruling 2026-08-29: that
    ///   branch is TRACKED and the mod stays in sync with it exactly as it does with a click** - held
    ///   here deliberately rather than inherited. It is the one wheel notch that is not a hand zoom:
    ///   the other twelve change how CLOSE the camera is and leave it over the same place
    ///   (<c>StartZooming</c>, uncounted, which is what the 2026-08-23 ruling keeps), while this one
    ///   JUMPS it onto whichever star the pointer is over - a different place, chosen by a pointer,
    ///   which is a click in all but the button. It arrives through the same call, so nothing here
    ///   tells the two apart and nothing needs to,
    /// - the nine fleet actions whose press only brings the camera in
    ///   (<c>FleetActionButtonColonize</c> and its siblings,
    ///   <c>EmpireLocalActionTogglePlanetConstruction.OnToggle</c>), which have a seat of their own
    ///   (<c>GalaxyHudScreen.SeatAfterFleetAction</c>) and drop this the moment it is armed.
    ///
    /// **This is also the page's MECHANISM-level answer to "which fleet actions only move the camera"**
    /// (owner ruling 2026-08-29: recognise what an action DOES rather than what it is called). An
    /// action nobody has ever named still moves the camera through one of these doors, so it is
    /// counted and its system seated like any other pointer move. The class names left in
    /// <c>GalaxyHudScreen.SeatTargetOf</c> no longer decide WHETHER a press is a camera move - only
    /// which control inside the system a press the mod does know about is really asking for.
    /// None of them passes through <c>GuiManager</c>, so none of them was ever seen by
    /// <see cref="GalaxyLocate"/>, and a mouse click left the page's camera record describing a place
    /// the camera had left (owner ruling 2026-08-26: a camera move by anybody else makes the record
    /// unbelievable, and a discrete click-to-zoom is not one of the hand-zoom exclusions).
    ///
    /// Two things are produced. The MOVE is counted (<see cref="UI.GalaxyViewLevels.Moves"/>) - the
    /// whole of the minimum fix, and enough on its own to stop the page swallowing the next attempt to
    /// come back in. The NODE is remembered for the page to seat the tree cursor on, which is the same
    /// answer every other camera move the game makes on its own now gets.
    ///
    /// What CANNOT reach this, by construction:
    /// - <c>GalaxyViewLevels.SnapTo</c>/<c>ZoomTo</c>/<c>ZoomToStep</c>, which drive the overview
    ///   LEVEL's own <c>ZoomInOnNode</c> or <c>ForceZoomingOnPosition</c> on the camera controller,
    ///   never the view's;
    /// - <c>CenterOn</c> (the open-sky slide, the inspect cell's sweep), which is
    ///   <c>CenterOnPoint</c> and counts its own move;
    /// - <c>PanTo</c> and <c>OpenSystem</c>, which are <c>GuiManager</c> calls counted and suppressed
    ///   by <see cref="GalaxyLocate"/>, and the next-idle-fleet replay, which selects through the
    ///   cursor service and the game's own fleet windows.
    /// The one mod caller that DOES reach it is the zoom ladder's deepest step in
    /// (<c>GalaxyViewLevels.StepZoom</c> → <c>EnterSystem</c>, which takes the map's own click path on
    /// purpose), and it marks itself <see cref="ByZoomKey"/>: a zoom the player made by hand is
    /// theirs to keep, which is the one exclusion the camera doctrine names
    /// (<see cref="UI.GalaxyViewLevels.Moves"/>, owner ruling 2026-08-23).
    /// </summary>
    internal static class GalaxyPick
    {
        /// <summary>Set while the MOD's own zoom ladder is stepping in through the map's click path.
        /// Unlike <see cref="GalaxyLocate.Suppressed"/> this also stands the COUNT down, because a
        /// zoom key is the one camera move the doctrine deliberately leaves uncounted.</summary>
        public static bool ByZoomKey;

        private static readonly ModPatch Patches = new ModPatch(
            "galaxypick",
            "the map's own zoom-in calls"
        );

        private static GameNode _picked;

        public static void Install()
        {
            Patches.Install(
                patch =>
                {
                    Hook(patch, "SelectGameNode");
                    Hook(patch, "ZoomInOnNode");
                    HookRestore(patch);
                }
            );
        }

        /// <summary>One of the two, patched - or logged and skipped, so a signature this game's build
        /// does not have costs the other one nothing. The <c>GalaxyNode</c> overload of each is the one
        /// wanted: it is where both of the <c>GameNode</c> ones end up.</summary>
        private static void Hook(ModPatch patch, string name)
        {
            patch.Hook(
                AccessTools.Method(typeof(GalaxyView), name, new[] { typeof(GalaxyNode) }),
                name,
                typeof(GalaxyPick),
                null,
                "Picked"
            );
        }

        /// <summary>The third door, and the only one that takes the camera BACK: the map's own undo of
        /// a forced zoom, which is what a right click on the galaxy does
        /// (<c>GalaxyCursor.OnCursorClick</c> :128, the only caller in the game). A PREFIX, because the
        /// game's method answers nothing and does nothing at all unless a zoom was forced - the same
        /// flag the click itself tests - and that flag is readable only before the restore runs.
        /// </summary>
        private static void HookRestore(ModPatch patch)
        {
            patch.Hook(
                AccessTools.Method(
                    typeof(GalaxyViewLevel_GalaxyOverview),
                    "RestoreZoom",
                    new Type[0]
                ),
                "RestoreZoom",
                typeof(GalaxyPick),
                "Restoring",
                null
            );
        }

        public static void Remove()
        {
            Patches.Remove();
            _picked = null;
            ByZoomKey = false;
        }

        /// <summary>The node the map was last taken in on, left where it is - for a page that may need
        /// several frames before it declares a row for it.</summary>
        public static GameNode Peek()
        {
            return _picked;
        }

        /// <summary>The same, and it is nobody's after that.</summary>
        public static GameNode Take()
        {
            GameNode picked = _picked;
            _picked = null;
            return picked;
        }

        /// <summary>Drop a pick nobody came to answer - the page it was meant for has gone away, or
        /// something that names its own place has taken the move over.</summary>
        public static void Forget()
        {
            _picked = null;
        }

        /// <summary>Both doors, sharing one postfix: they carry the same parameter and the same
        /// meaning, and the nested pair a single click makes (select, then zoom) records the same node
        /// twice and counts twice - which costs nothing, since only equality with the count is ever
        /// asked.</summary>
        private static void Picked(GalaxyNode galaxyNode)
        {
            try
            {
                if (ByZoomKey)
                {
                    return;
                }

                UI.GalaxyViewLevels.Moved();
                _picked = galaxyNode == null ? null : galaxyNode.Node;
            }
            catch (Exception e)
            {
                Report(e);
            }
        }

        /// <summary>
        /// The map's own way back out of a click's zoom, about to run: COUNTED, and nothing else.
        ///
        /// It is a move by somebody else in the sense that matters - the camera is put back where it
        /// stood before the zoom, which is a different PLACE and not merely a different step
        /// (<c>GalaxyViewCameraController.RestoreLastCameraParameters</c>), so a record still saying
        /// "showing that star" would be describing a picture nobody is looking at. That is what
        /// separates it from the hand-zoom exclusions, which all leave the camera over the same place.
        ///
        /// No node is remembered: a restore names nowhere to send the cursor, it takes the player back
        /// to whatever they were looking at before.
        ///
        /// Counted on the FLAG, which is exactly what the game's own right click tests. A force made
        /// while the camera was already at the closest step saves that step as the parameters to
        /// restore, so a restore with the flag set can move nothing at all (measured, ES2 facts) - and
        /// counting one of those costs a single extra re-frame onto whatever the cursor is reading,
        /// which is the cheap side of the mistake to make.
        /// </summary>
        private static void Restoring(GalaxyViewLevel_GalaxyOverview __instance)
        {
            try
            {
                if (__instance.HasZoomBeenForced)
                {
                    UI.GalaxyViewLevels.Moved();
                }
            }
            catch (Exception e)
            {
                Report(e);
            }
        }

        private static void Report(Exception e)
        {
            Patches.Report("noticing the map being taken in on a node threw", e);
        }
    }
}

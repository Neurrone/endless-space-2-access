using System;
using Amplitude.Unity.Framework;
using Amplitude.Unity.View;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// Which of the game's own views of the galaxy the player is in, and how to ask the game to look
    /// somewhere else.
    ///
    /// The map is not one page. The game moves between VIEW LEVELS over the same galaxy - the galaxy
    /// from above, one system's management page, one planet, a battle - and each is a different set of
    /// things a player can do, so each wants its own screen. A screen says which level it belongs to by
    /// asking <see cref="At{TLevel}"/> in its IsActive; a level no screen of ours claims simply leaves
    /// the player with the game's own keys, which is a worse place to be than a modelled screen and
    /// still better than a screen describing a page that is no longer showing.
    ///
    /// Nothing is remembered here. The game holds the answer in one field and keeps it up to date on
    /// its own schedule, so a tracker would only be a copy that can go stale - and a static with no
    /// state is reload-safe by construction: there is nothing for ModEntry.Stop to give back.
    ///
    /// The camera routes live here too, because in this game moving the camera IS asking for a view
    /// level: the same call that takes the player to the galaxy also centres it on something.
    /// </summary>
    public static class GalaxyViewLevels
    {
        /// <summary>
        /// How close the camera sits when the player opens a system up: all the way in.
        ///
        /// The galaxy is drawn in zoom steps and each step swaps which parts of a system the map
        /// draws. Only the LAST step reaches the orbital view - the game gives a system the keyboard
        /// focus (<c>CanFocusGalaxyEntity</c>, and with it <c>FocusedStarSystemNode</c> and the
        /// per-planet orbital cards) exactly when the step is the last one, so anything short of it
        /// leaves the player looking at a system whose planets the map is not drawing.
        ///
        /// Read from the controller rather than written down, because it is the controller's own
        /// definition of "as close as this game goes".
        /// </summary>
        public static int OrbitalZoomStep(GalaxyViewCameraController camera)
        {
            return camera == null ? 0 : camera.ZoomStepsCount - 1;
        }

        /// <summary>Which zoom step the camera is on right now, or -1 before there is a camera. The
        /// steps are the map's own ladder: what a step DRAWS changes as it climbs, and only the last
        /// one reaches the orbital view.</summary>
        public static int ZoomStep
        {
            get
            {
                GalaxyViewCameraController camera = Camera();
                return camera == null ? -1 : camera.ZoomStepCurrent;
            }
        }

        /// <summary>Whether the camera is as close as this game goes - the step at which the map draws
        /// a system's planets as cards in orbit. False when there is no camera to ask.</summary>
        public static bool AtOrbitalZoom
        {
            get
            {
                GalaxyViewCameraController camera = Camera();
                return camera != null && camera.ZoomStepCurrent >= OrbitalZoomStep(camera);
            }
        }

        /// <summary>The step the map starts a session on - where "the galaxy from above" means, when
        /// nobody remembers where the player had the camera before they came in on a system.</summary>
        public static int DefaultZoomStep
        {
            get
            {
                GalaxyViewCameraController camera = Camera();
                return camera == null ? -1 : camera.ZoomDefaultStep;
            }
        }

        /// <summary>The system the map has brought the camera in on, whose planets it is drawing
        /// orbital cards for - null whenever the camera is anywhere else.</summary>
        public static StarSystemNode FocusedSystem
        {
            get
            {
                try
                {
                    return Gui.GuiGameWindowService == null
                        ? null
                        : Gui.GuiGameWindowService.FocusedStarSystemNode;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>The view level the game is showing, or null before a game is running.</summary>
        public static GalaxyViewLevel Level
        {
            get
            {
                try
                {
                    return Gui.GuiGameWindowService == null
                        ? null
                        : Gui.GuiGameWindowService.CurrentGalaxyViewLevel;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// The view level the galaxy view itself is on, which unlike <see cref="Level"/> keeps
        /// answering while the game is moving into one.
        ///
        /// The two disagree for a handful of frames every time a level is RE-entered - and re-entering
        /// the same level with a different subject is how the game steps from one planet to the next.
        /// The GUI's answer goes null for that gap; the view's does not, because the view only
        /// reassigns it when the level actually changes. A page that has to survive its own subject
        /// being swapped under it asks this one; a page that should stand down for anything at all
        /// asks <see cref="Level"/>.
        /// </summary>
        public static GalaxyViewLevel LevelThroughTransitions
        {
            get
            {
                try
                {
                    IViewService views = Services.GetService<IViewService>();
                    GalaxyView galaxy = views == null ? null : views.CurrentView as GalaxyView;
                    return galaxy == null ? null : galaxy.GalaxyViewLevelCurrent;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>Whether the game is showing a particular view level - the question a screen's
        /// IsActive asks.</summary>
        public static bool At<TLevel>()
            where TLevel : GalaxyViewLevel
        {
            return Level is TLevel;
        }

        /// <summary>The galaxy seen from above: the map the whole game is played from.</summary>
        public static bool Overview
        {
            get { return At<GalaxyViewLevel_GalaxyOverview>(); }
        }

        /// <summary>The scan overlay - the game's own X-ray of whichever view level is up. It is not a
        /// level of its own, so it has to be asked about separately. Mirrors the view service's
        /// InScanViewMode, which is where the game itself reads it from.</summary>
        public static bool Scanning
        {
            get
            {
                try
                {
                    return Gui.GuiGameWindowService != null && Gui.GuiGameWindowService.IsInScanView;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>Take the camera to something on the map, the way every "show me where this
        /// happened" button in the game does. Already on the galaxy, this only slides the camera
        /// across and leaves the zoom where the player had it.</summary>
        public static void PanTo(IGameEntityWithGalaxyPosition entity)
        {
            try
            {
                Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(entity);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: panning to an entity threw: " + e);
            }
        }

        /// <summary>
        /// Bring the camera all the way in on a node, to <see cref="OrbitalZoomStep"/> - the step at
        /// which the map draws the system's planets as cards in orbit around its star.
        ///
        /// The game's own route first: double-clicking a system on the map calls
        /// <c>GalaxyViewLevel_GalaxyOverview.ZoomInOnNode</c>, which force-zooms AND records that the
        /// zoom was forced and on what, so the map's own way back out (right-click) keeps working
        /// afterwards. Reaching for the camera directly would leave that bookkeeping behind.
        ///
        /// Where the drawn node cannot be found - the map has not built an entity for it - the camera
        /// is asked directly, which still gets the player there.
        /// </summary>
        public static void ZoomTo(GameNode node)
        {
            try
            {
                GalaxyViewCameraController camera = Camera();
                if (camera == null || node == null)
                {
                    return;
                }

                GalaxyViewLevel_GalaxyOverview overview =
                    Level as GalaxyViewLevel_GalaxyOverview;
                GalaxyNode drawn = Drawn(node);
                if (overview != null && drawn != null)
                {
                    overview.ZoomInOnNode(drawn);
                    return;
                }

                ZoomToStep(node, OrbitalZoomStep(camera));
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: zooming on a node threw: " + e);
            }
        }

        /// <summary>
        /// Whether the camera was brought in on something by a click rather than by the player's own
        /// zooming - the map's own record of it, which is what makes its way back out possible.
        ///
        /// The overview level sets the flag in <c>ZoomInOnNode</c> and clears it when the camera is
        /// moved by anything else, so it answers "is there a zoom to undo" without anything being
        /// remembered here.
        /// </summary>
        public static bool ZoomForced
        {
            get
            {
                GalaxyViewLevel_GalaxyOverview overview = OverviewLevel();
                return overview != null && overview.HasZoomBeenForced;
            }
        }

        /// <summary>Put the camera back where it stood before the forced zoom - the game's own way out
        /// of a system it was clicked into, right-click on the map. Does nothing where no zoom was
        /// forced, which is the same answer the map gives a right-click there.</summary>
        public static void RestoreZoom()
        {
            try
            {
                GalaxyViewLevel_GalaxyOverview overview = OverviewLevel();
                if (overview != null)
                {
                    overview.RestoreZoom();
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: restoring the camera threw: " + e);
            }
        }

        /// <summary>The galaxy-overview level itself, asked through the view rather than through the
        /// GUI's copy so that it keeps answering across the frames a level change blinks.</summary>
        private static GalaxyViewLevel_GalaxyOverview OverviewLevel()
        {
            return LevelThroughTransitions as GalaxyViewLevel_GalaxyOverview;
        }

        /// <summary>
        /// Put the camera on <paramref name="node"/> at a particular zoom step - how the player comes
        /// back OUT of a system without losing the system they were reading.
        ///
        /// Through the controller's own force-zoom, which moves the camera and changes the step
        /// together. Setting the step alone swaps which layer of the map is drawn without moving the
        /// camera anywhere, which leaves the player looking at a close-up of empty space.
        ///
        /// The game's own way back out (<c>GalaxyViewLevel_GalaxyOverview.RestoreZoom</c>) is
        /// deliberately not used: it also returns the camera to wherever it was BEFORE the zoom, which
        /// for a keyboard player is somewhere they have since navigated away from. Here the step comes
        /// back and the system stays under the cursor.
        /// </summary>
        public static void ZoomToStep(GameNode node, int step)
        {
            try
            {
                GalaxyViewCameraController camera = Camera();
                if (camera == null || node == null || step < 0)
                {
                    return;
                }

                // The offset is the game's own: it frames a system slightly off-centre so that the
                // label, which is drawn to one side of the star, lands inside the screen.
                camera.ForceZoomingOnPosition(
                    Math.Min(step, OrbitalZoomStep(camera)),
                    (Vector3)node.GalaxyPosition + camera.HoveredStarSystemOffset
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: zooming a node to a step threw: " + e);
            }
        }

        /// <summary>The map's own object for a node - what the view levels and the camera take, as
        /// opposed to the model everything else talks about. Null when the map has not built one.
        /// </summary>
        private static GalaxyNode Drawn(GameNode node)
        {
            try
            {
                IGalaxyEntityFactoryService entities =
                    Services.GetService<IGalaxyEntityFactoryService>();
                GameObject entity = entities == null ? null : entities[node.GUID];
                return entity == null ? null : entity.GetComponent<GalaxyNode>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Open a system's management page - the game's own route into it, the one its label's
        /// button takes.</summary>
        public static void OpenSystem(StarSystemNode node)
        {
            try
            {
                Gui.GuiGameWindowService.RequestStarSystemManagementViewLevel(node.GUID);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: opening a system's management view threw: " + e);
            }
        }

        /// <summary>Open one planet's own page - the game's own route into it, the one a click on a
        /// planet takes from a system's management page. The view level is asked for by type and
        /// handed the galaxy's own object for the planet, which is not the planet the rest of the game
        /// talks about: the map keeps a separate entity per thing it draws, and the factory is where
        /// the two are matched up.</summary>
        public static void OpenPlanet(Planet planet)
        {
            try
            {
                IViewService views = Services.GetService<IViewService>();
                GalaxyView galaxy = views == null ? null : views.CurrentView as GalaxyView;
                IGalaxyEntityFactoryService entities =
                    Services.GetService<IGalaxyEntityFactoryService>();
                if (galaxy == null || entities == null || planet == null)
                {
                    return;
                }

                GameObject entity = entities[planet.GUID];
                AbstractGalaxyPlanet drawn =
                    entity == null ? null : entity.GetComponent<AbstractGalaxyPlanet>();
                if (drawn == null)
                {
                    return;
                }

                galaxy.RequestGalaxyViewLevelChange(typeof(GalaxyViewLevel_PlanetOverview), drawn);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: opening a planet's page threw: " + e);
            }
        }

        private static GalaxyViewCameraController Camera()
        {
            try
            {
                ICameraService service = Services.GetService<ICameraService>();
                return service == null
                    ? null
                    : service.CameraController as GalaxyViewCameraController;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

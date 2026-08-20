using System;
using Amplitude.Unity.Framework;
using Amplitude.Unity.View;
using ES2Access.Core.Speech;
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

        /// <summary>The two rungs that sit ABOVE the galaxy's own zoom ladder: a system's page and then
        /// one planet's page. The game reaches them with the same gesture that moves the zoom - the wheel
        /// at the closest step goes INTO the system, and one more notch into a planet - so as far as a
        /// player is concerned they are the top of one ladder rather than a different control.</summary>
        private const int LevelRungs = 2;

        /// <summary>
        /// How many rungs "how close am I looking" has: every zoom step of the galaxy camera, then the
        /// system's page and the planet's page above them.
        ///
        /// Zero when there is no galaxy camera at all, which is a game that has not started.
        /// </summary>
        public static int ZoomRungs
        {
            get
            {
                GalaxyViewCameraController camera = GalaxyCamera();
                return camera == null ? 0 : camera.ZoomStepsCount + LevelRungs;
            }
        }

        /// <summary>
        /// Which rung of that ladder the game is on, or -1 where the question does not apply (a battle,
        /// the system-discovery view, no game).
        ///
        /// Asked through <see cref="LevelThroughTransitions"/> rather than <see cref="Level"/> so that
        /// the answer does not blink while the game is flying between two levels.
        /// </summary>
        public static int ZoomRung
        {
            get
            {
                GalaxyViewCameraController camera = GalaxyCamera();
                if (camera == null)
                {
                    return -1;
                }

                GalaxyViewLevel level = LevelThroughTransitions;
                if (level is GalaxyViewLevel_PlanetOverview)
                {
                    return camera.ZoomStepsCount + 1;
                }

                if (level is GalaxyViewLevel_SystemManagement)
                {
                    return camera.ZoomStepsCount;
                }

                return level is GalaxyViewLevel_GalaxyOverview ? camera.ZoomStepCurrent : -1;
            }
        }

        /// <summary>
        /// Move one rung in (<paramref name="sign"/> positive) or out, the way the game's own wheel does
        /// - which is the only zoom gesture the game has for a keyboard beyond holding its PageUp and
        /// PageDown down (<c>IInputOptionsService.InputBindingsZoomIn</c>, polled while HELD by
        /// <c>GalaxyViewCameraController.CheckInputs</c>, and answered by nothing at all once the game is
        /// inside a system).
        ///
        /// A COARSE press moves to the next boundary in the map's own layer table
        /// (<c>LayerDescriptorNamesByZoomIndex</c>) rather than by a fixed number of steps: what a step
        /// is WORTH here is which layer of the map it draws, and on a 13-step ladder ten steps would be
        /// the whole range.
        ///
        /// Answers whether anything moved. False is a clamp - the galaxy's furthest out, or a planet's
        /// page with nothing closer, which are the ladder's two real ends.
        /// </summary>
        public static bool StepZoom(int sign, bool coarse)
        {
            try
            {
                GalaxyViewCameraController camera = GalaxyCamera();
                int rung = ZoomRung;
                if (camera == null || sign == 0 || rung < 0)
                {
                    return false;
                }

                int last = camera.ZoomStepsCount - 1;
                if (rung > last)
                {
                    return sign < 0 ? LeaveLevel() : GoDeeper();
                }

                if (sign > 0 && rung == last)
                {
                    return EnterSystem();
                }

                int wanted = coarse ? BandStep(camera, rung, sign) : rung + sign;
                wanted = Math.Max(0, Math.Min(last, wanted));
                if (wanted == rung)
                {
                    return false;
                }

                // The wheel's own no-hover branch recentres on wherever the camera is already looking,
                // so the step changes and the view does not slide sideways under the player.
                camera.ForceZoomingOnPosition(wanted, camera.TargetPositionCurrent);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: stepping the zoom threw: " + e);
                return false;
            }
        }

        /// <summary>Put the galaxy camera on one exact rung of its ladder, centred on
        /// <paramref name="at"/> - the same call the wheel's own no-hover branch makes, so the view
        /// arrives the way every other zoom does. For a mode that wants a KNOWN framing on entry (the
        /// inspect cursor) rather than a step from wherever the player left the wheel. Clamped to the
        /// ladder; false where there is no galaxy camera to ask.</summary>
        public static bool SetZoom(int step, Vector3 at)
        {
            try
            {
                GalaxyViewCameraController camera = GalaxyCamera();
                if (camera == null)
                {
                    return false;
                }

                int wanted = Math.Max(0, Math.Min(camera.ZoomStepsCount - 1, step));
                camera.ForceZoomingOnPosition(wanted, at);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: setting the zoom threw: " + e);
                return false;
            }
        }

        /// <summary>
        /// What the map is DRAWING at the rung the game is on, in a word - null where the question has
        /// no answer.
        ///
        /// A rung number alone says how far along a ladder the camera is and nothing about what that
        /// buys. The game's own zoom steps are grouped into LAYERS and each layer is a different map:
        /// stars become named systems, systems grow their planets. The grouping is the game's own
        /// table (<c>LayerDescriptorNamesByZoomIndex</c>, read here rather than copied so a patch that
        /// re-cuts the bands moves this with it), and the words are the mod's, because the table holds
        /// asset names and the game writes none of them anywhere a player can read.
        ///
        /// Three silences, all of them the same rule - say nothing rather than invent a name:
        /// the furthest-out step, whose layer is the painted backdrop and not a map of anything; the
        /// two rungs above the camera's own ladder, which are a system's page and a planet's and
        /// announce themselves as pages; and a layer name this mod has no word for, which is what a
        /// patch adding a band looks like from here.
        /// </summary>
        public static string ZoomBand
        {
            get
            {
                try
                {
                    GalaxyViewCameraController camera = GalaxyCamera();
                    int rung = ZoomRung;
                    string[] layers =
                        camera == null ? null : camera.LayerDescriptorNamesByZoomIndex;
                    if (layers == null || rung < 0 || rung >= layers.Length)
                    {
                        return null;
                    }

                    string key = BandKey(layers[rung]);
                    return key == null ? null : ModStrings.Get(key);
                }
                catch (Exception e)
                {
                    Log.Warn("galaxy: naming the zoom band threw: " + e);
                    return null;
                }
            }
        }

        /// <summary>The mod's word for one of the map's layers, or null for the ones with nothing to
        /// say. Measured on the shipped table: thirteen steps over seven layers, the first of them the
        /// backdrop.</summary>
        private static string BandKey(string layer)
        {
            switch (layer)
            {
                case "GalaxyMapLayer":
                    return ModStrings.ZoomBandGalaxyMap;
                case "InformativeGalaxyLayer":
                    return ModStrings.ZoomBandInformativeGalaxy;
                case "ConstellationLayer":
                    return ModStrings.ZoomBandConstellation;
                case "SystemsLayer":
                    return ModStrings.ZoomBandSystems;
                case "SystemLayer":
                    return ModStrings.ZoomBandSystem;
                case "SystemOverviewLayer":
                    return ModStrings.ZoomBandSystemOverview;
                default:
                    return null;
            }
        }

        /// <summary>The first step on either side of <paramref name="rung"/> that draws a DIFFERENT layer
        /// of the map, or the end of the ladder where there is no further boundary.</summary>
        private static int BandStep(GalaxyViewCameraController camera, int rung, int sign)
        {
            string[] layers = camera.LayerDescriptorNamesByZoomIndex;
            if (layers == null || rung >= layers.Length)
            {
                return rung + sign;
            }

            string here = layers[rung];
            for (int step = rung + sign; step >= 0 && step < layers.Length; step += sign)
            {
                if (layers[step] != here)
                {
                    return step;
                }
            }

            return sign > 0 ? camera.ZoomStepsCount - 1 : 0;
        }

        /// <summary>Into the system the camera is closest to, which is what the wheel does at the closest
        /// step: the map's own click path, so a colony of the player's opens its page and anything else
        /// is merely zoomed at.</summary>
        private static bool EnterSystem()
        {
            StarSystemNode node = FocusedSystem;
            GalaxyNode drawn = node == null ? null : Drawn(node);
            GalaxyView galaxy = GalaxyViewOf();
            if (galaxy == null || drawn == null)
            {
                return false;
            }

            galaxy.SelectGameNode(drawn);
            return true;
        }

        /// <summary>
        /// Back out of the system's or the planet's page - the same view-level change each level's own
        /// wheel makes, asked of the VIEW rather than handed to the level's camera controller.
        ///
        /// Not through <c>ICameraController.HandleInput</c>, which both of those controllers gate on
        /// <c>AgeManager.IsMouseCovered</c> and on their own camera still being bound
        /// (<c>SystemManagementCameraController.HandleInput</c> :60-75,
        /// <c>PlanetOverviewCameraController.HandleInput</c> :63-75): a physical mouse resting anywhere
        /// over the game's own interface - which a keyboard player never touches and cannot see - made
        /// the whole way back down the ladder a silent refusal, and the system controller answers false
        /// even when it acted, so a caller cannot tell the two apart. The requests below are the exact
        /// calls those handlers make once past their gates (their private
        /// <c>MoveToGalaxyOverviewViewLevel</c> / <c>MoveToSystemManagementViewLevel</c>), so the route
        /// is the game's own and the mouse has no say in it.
        /// </summary>
        private static bool LeaveLevel()
        {
            GalaxyView galaxy = GalaxyViewOf();
            if (galaxy == null)
            {
                return false;
            }

            GalaxyViewLevel_PlanetOverview planet =
                LevelThroughTransitions as GalaxyViewLevel_PlanetOverview;
            if (planet != null)
            {
                GalaxyStarSystem around =
                    planet.Planet == null ? null : planet.Planet.GalaxyStarSystem;
                if (around == null)
                {
                    return false;
                }

                galaxy.RequestGalaxyViewLevelChange(
                    typeof(GalaxyViewLevel_SystemManagement),
                    around
                );
                return true;
            }

            GalaxyViewLevel_SystemManagement system =
                LevelThroughTransitions as GalaxyViewLevel_SystemManagement;
            if (system == null || system.StarSystem == null)
            {
                return false;
            }

            // The game lets go of whatever the map's cursor had selected inside the system before it
            // leaves (the controller's own UnselectAll), or a planet stays selected under a cursor that
            // is no longer looking at it.
            UnselectAll();
            galaxy.RequestGalaxyViewLevelChange(
                typeof(GalaxyViewLevel_GalaxyOverview),
                false,
                system.StarSystem
            );
            return true;
        }

        /// <summary>Let go of everything the map's cursor is holding selected - ported from
        /// <c>SystemManagementCameraController.UnselectAll</c> (:106-123), which the game runs on its own
        /// way out of a system's page. Bounded by the count going down, as the game's own loop is.
        /// </summary>
        private static void UnselectAll()
        {
            try
            {
                ICursorTargetService cursors = Services.GetService<ICursorTargetService>();
                if (cursors == null)
                {
                    return;
                }

                while (cursors.SelectedCursorTargets.Count > 0)
                {
                    int count = cursors.SelectedCursorTargets.Count;
                    cursors.UnselectSilent(cursors.SelectedCursorTargets[0]);
                    if (count <= cursors.SelectedCursorTargets.Count)
                    {
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: unselecting the map's cursor targets threw: " + e);
            }
        }

        /// <summary>
        /// Deeper than a system's page is one PLANET - the system's first, which is the rung the ladder
        /// would otherwise stop one short of.
        ///
        /// The game's own gesture here (<c>SystemManagementCameraController.MoveToPlanetOverviewViewLevel</c>)
        /// asks which planet the POINTER is highlighting and does nothing at all when the answer is none,
        /// which for a keyboard is always. So the keyboard names a planet instead - the first the system
        /// has - through the same call a planet card's own Enter makes (<see cref="OpenPlanet"/>), and the
        /// ladder's last rung becomes reachable rather than being a clamp the value has to lie about.
        /// </summary>
        private static bool GoDeeper()
        {
            GalaxyViewLevel_SystemManagement system =
                LevelThroughTransitions as GalaxyViewLevel_SystemManagement;
            GalaxyStarSystem drawn = system == null ? null : system.StarSystem;
            StarSystemNode node = drawn == null ? null : drawn.StarSystemNode;
            if (node == null || node.Planets.Count == 0)
            {
                return false;
            }

            return OpenPlanet(node.Planets[0]);
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
                GalaxyView galaxy = GalaxyViewOf();
                return galaxy == null ? null : galaxy.GalaxyViewLevelCurrent;
            }
        }

        /// <summary>
        /// Whether the view is in the middle of moving between levels - the seconds where the camera
        /// is flying from wherever it was to wherever it is going.
        ///
        /// The view runs one transition at a time and holds it while it runs
        /// (<c>GalaxyView.GalaxyViewLevelTransitionCurrent</c>), which is the game's own answer to
        /// "is the camera still on its way". It matters to anything that measures where a thing is
        /// DRAWN: mid-flight every screen position is a frame out of date, and re-entering the same
        /// level with a different subject - how the page steps from one planet to the next - is a
        /// transition like any other even though the level never changes.
        /// </summary>
        public static bool ChangingLevel
        {
            get
            {
                GalaxyView galaxy = GalaxyViewOf();
                return galaxy != null && !galaxy.CanChangeGalaxyView;
            }
        }

        /// <summary>Whether there is a camera drawing the world right now. The game's own labels
        /// measure themselves against it, and give up outright when it is missing - which it is for
        /// part of every view change.</summary>
        public static bool CameraDrawing
        {
            get
            {
                try
                {
                    ICameraService service = Services.GetService<ICameraService>();
                    return service != null && service.Camera != null;
                }
                catch (Exception)
                {
                    return false;
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
        /// across and leaves the zoom where the player had it.
        ///
        /// Marked as the mod's own (<see cref="Screens.GalaxyLocate.Suppressed"/>): this is the camera
        /// FOLLOWING the cursor, and the very call the mod watches for the game LEADING it.</summary>
        public static void PanTo(IGameEntityWithGalaxyPosition entity)
        {
            try
            {
                Screens.GalaxyLocate.Suppressed = true;
                Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(entity);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: panning to an entity threw: " + e);
            }
            finally
            {
                Screens.GalaxyLocate.Suppressed = false;
            }
        }

        /// <summary>
        /// Slide the camera onto a bare POINT of the map - somewhere with nothing at it.
        ///
        /// The camera controller's own recentring (<c>CenterOnPoint</c>: a SmoothDamp towards the
        /// point, clamped to the galaxy by <c>ClampCameraPosition</c>), called directly rather than
        /// through <see cref="PanTo"/>. PanTo goes through the GUI's "show the player this entity",
        /// which is the very call <see cref="Screens.GalaxyLocate"/> watches for the game LEADING the
        /// player somewhere, and it needs an entity - and the inspect cursor is aimed at empty space
        /// as often as at a star.
        ///
        /// Answers whether the camera took it, which is false while the galaxy camera is not the live
        /// one (inside a system, on a planet).
        /// </summary>
        public static bool CenterOn(Vector3 point, float damping)
        {
            try
            {
                GalaxyViewCameraController camera = Camera();
                if (camera == null)
                {
                    return false;
                }

                camera.CenterOnPoint(point, damping);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: centring the camera on a point threw: " + e);
                return false;
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
        /// button takes. Where the game will not open one - a system blacked out or nobody's - the call
        /// silently degrades to centring the map on it (es2-facts), which is why this too is marked as
        /// the mod's own: the cursor is already on the system, and being sent to it again would say
        /// nothing about the page that did not open.</summary>
        public static void OpenSystem(StarSystemNode node)
        {
            try
            {
                Screens.GalaxyLocate.Suppressed = true;
                Gui.GuiGameWindowService.RequestStarSystemManagementViewLevel(node.GUID);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: opening a system's management view threw: " + e);
            }
            finally
            {
                Screens.GalaxyLocate.Suppressed = false;
            }
        }

        /// <summary>Open one planet's own page - the game's own route into it, the one a click on a
        /// planet takes from a system's management page. The view level is asked for by type and
        /// handed the galaxy's own object for the planet, which is not the planet the rest of the game
        /// talks about: the map keeps a separate entity per thing it draws, and the factory is where
        /// the two are matched up. Answers whether the page was asked for - false where the map has
        /// drawn nothing for the planet, which is the ladder's clamp.</summary>
        public static bool OpenPlanet(Planet planet)
        {
            try
            {
                GalaxyView galaxy = GalaxyViewOf();
                IGalaxyEntityFactoryService entities =
                    Services.GetService<IGalaxyEntityFactoryService>();
                if (galaxy == null || entities == null || planet == null)
                {
                    return false;
                }

                GameObject entity = entities[planet.GUID];
                AbstractGalaxyPlanet drawn =
                    entity == null ? null : entity.GetComponent<AbstractGalaxyPlanet>();
                if (drawn == null)
                {
                    return false;
                }

                galaxy.RequestGalaxyViewLevelChange(typeof(GalaxyViewLevel_PlanetOverview), drawn);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: opening a planet's page threw: " + e);
                return false;
            }
        }

        private static GalaxyViewCameraController Camera()
        {
            return Controller() as GalaxyViewCameraController;
        }

        /// <summary>Whichever camera the game is driving - one per view level, swapped as the level
        /// changes.</summary>
        private static ICameraController Controller()
        {
            try
            {
                ICameraService service = Services.GetService<ICameraService>();
                return service == null ? null : service.CameraController;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The galaxy's own camera, live or not.
        ///
        /// Inside a system or on a planet the game is driving that level's camera instead, and the galaxy
        /// camera keeps the zoom step it was left on - which is what the way back out returns to. The
        /// overview level owns the controller, so it is asked rather than remembered here.
        /// </summary>
        private static GalaxyViewCameraController GalaxyCamera()
        {
            GalaxyViewCameraController live = Camera();
            if (live != null)
            {
                return live;
            }

            try
            {
                GalaxyView galaxy = GalaxyViewOf();
                GalaxyViewLevel overview = null;
                if (
                    galaxy == null
                    || galaxy.GalaxyViewLevelsByType == null
                    || !galaxy.GalaxyViewLevelsByType.TryGetValue(
                        typeof(GalaxyViewLevel_GalaxyOverview),
                        out overview
                    )
                )
                {
                    return null;
                }

                return overview == null
                    ? null
                    : overview.CameraController as GalaxyViewCameraController;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The view the galaxy is drawn by, or null when the game is showing something else.
        /// </summary>
        private static GalaxyView GalaxyViewOf()
        {
            try
            {
                IViewService views = Services.GetService<IViewService>();
                return views == null ? null : views.CurrentView as GalaxyView;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

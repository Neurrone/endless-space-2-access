using System;
using System.Reflection;
using ES2Access.Core.UI;
using ES2Access.Core.Util;
using ES2Access.ES2.UI;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>Moving the game camera over the wheel, and the aim this screen holds while it waits
    /// for the game to arrive there.</summary>
    public sealed partial class ResearchScreen
    {
        /// <summary>Where the viewport has been asked to look, by the branch the player has just
        /// opened or the dot they have just landed on. A request rather than a move, because a player
        /// holding an arrow down walks a whole ring in the time one zoom takes, and only the last of
        /// those is worth animating to. One slot: the target that goes with the aim.</summary>
        private ResearchCamera.Aim _cameraAim;
        private TechnologyQuadrantItem _cameraQuadrant;
        private BaseTechnologyStageItem _cameraStage;
        private TechnologyItem2 _cameraTechnology;

        // ---- the camera ----

        /// <summary>
        /// Take the viewport where the player asked to look, once whatever it was doing has finished.
        ///
        /// Nothing here fights an animation: a player holding an arrow down walks a whole ring in the
        /// time one zoom takes, and a zoom started per keystroke would leave the view chasing a cursor
        /// that had already gone. The request survives until a frame with no animation running, and
        /// there is only one of it, so the last thing asked for is what is shown.
        /// </summary>
        private void MoveCamera()
        {
            try
            {
                FinishMoveToHead();
                if (_cameraAim == ResearchCamera.Aim.None)
                {
                    return;
                }

                TechnologyScreen window = Window();
                AgeTransform viewport = window == null ? null : window.Viewport;
                AgeModifierSet modifiers = viewport == null ? null : viewport.AgeFirstModifierSet;
                if (modifiers == null || modifiers.ModifiersRunning)
                {
                    return;
                }

                ResearchCamera.Aim aim = _cameraAim;
                TechnologyQuadrantItem quadrant = _cameraQuadrant;
                BaseTechnologyStageItem stage = _cameraStage;
                TechnologyItem2 item = _cameraTechnology;
                Look(ResearchCamera.Aim.None, null, null, null);

                if (aim == ResearchCamera.Aim.Overview)
                {
                    ZoomOut(window);
                }
                else if (aim == ResearchCamera.Aim.Quadrant && quadrant != null)
                {
                    // The game's own answer to a click on the quarter of the wheel: it works out
                    // where the middle of the sector is and zooms or pans there, and it plays the
                    // sound a player at the mouse would hear.
                    ((ITechnologyQuadrantClient)window).OnSectorClick(quadrant);
                }
                else if (aim == ResearchCamera.Aim.Stage && stage != null)
                {
                    ZoomAt(window, StageAim(stage));
                }
                else if (
                    aim == ResearchCamera.Aim.Technology
                    && item != null
                    && !Drawn(item)
                    && ForceZoomIn != null
                )
                {
                    ForceZoomIn.Invoke(window, new object[] { item });
                }
            }
            catch (Exception e)
            {
                Log.Warn("research: moving the camera threw: " + e);
            }
        }

        /// <summary>
        /// Where the middle of a stage's arc is, in the coordinates the screen aims its viewport with.
        ///
        /// The same polar transform the stage uses to place its own dots, with the technology's offset
        /// along the arc left out - so the point is the middle of the ring, at the angle the stage is
        /// centred on. The screen's aim points are measured from the middle of the wheel, which is
        /// where the halving comes from.
        /// </summary>
        private static Vector2 StageAim(BaseTechnologyStageItem stage)
        {
            float radius =
                stage.ViewportNormalizedSize
                * 0.5f
                * (stage.MinRadius + 0.5f * (stage.MaxRadius - stage.MinRadius));
            float angle = Mathf.Deg2Rad * (90f - stage.CenterAngle);
            return new Vector2(radius * Mathf.Cos(angle), -radius * Mathf.Sin(angle));
        }

        /// <summary>Put the aim point in the middle of the screen, the way the screen's own zoom does:
        /// from the overview that means zooming in, and from the close view it means sliding across at
        /// the zoom already in force - which is why the point is scaled by it.</summary>
        private static void ZoomAt(TechnologyScreen window, Vector2 aim)
        {
            GuiValueController zoom =
                window.Viewport == null
                    ? null
                    : window.Viewport.GetComponent<GuiValueController>();
            bool close = zoom != null && zoom.CurrentValue == TechnologyScreen.MaxZoomFactor;
            MethodInfo move = close ? SlideTo : ZoomIn;
            if (move == null)
            {
                return;
            }

            move.Invoke(
                window,
                new object[]
                {
                    close ? aim * TechnologyScreen.MaxZoomFactor : aim,
                    TechnologyScreen.FastTransitionDuration,
                }
            );
        }

        /// <summary>Back to the overview, through the game's own zoom switch - the toggle the key
        /// panel draws, which is also what tells the panel the view has changed.</summary>
        private static void ZoomOut(TechnologyScreen window)
        {
            ResearchKeySidePanel key = window == null ? null : window.ResearchKeySidePanel;
            if (key != null && key.ZoomInToggle != null && key.ZoomInToggle.State)
            {
                AgeWidgets.Toggle(key.ZoomInToggle);
            }
        }

        /// <summary>Whether the renderer is drawing this technology at all - it culls everything
        /// outside the screen, and a dot that is not drawn has no tooltip to show.</summary>
        private static bool Drawn(TechnologyItem2 item)
        {
            try
            {
                return new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height).Overlaps(
                    item.AgeTransform.GetGlobalPosition()
                );
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>The screen's own "bring this technology into view", which zooms in from the
        /// overview and pans when it is already zoomed in. Private to the game, like the queue
        /// panel's cancel handler on the star system page.</summary>
        private static readonly MethodInfo ForceZoomIn = GameHandlers.Method(
            typeof(TechnologyScreen),
            "ForceZoomIn"
        );

        /// <summary>The two halves of the screen's own "look here": zoom in on a point from the
        /// overview, slide across to it when the view is already close. Private to the game, and the
        /// only way to aim at anything the game does not itself let the player click on.</summary>
        private static readonly MethodInfo ZoomIn = GameHandlers.Method(
            typeof(TechnologyScreen),
            "DoZoomIn"
        );

        private static readonly MethodInfo SlideTo = GameHandlers.Method(
            typeof(TechnologyScreen),
            "DoTranslate"
        );
    }
}

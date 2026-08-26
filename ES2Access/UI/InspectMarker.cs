using System;
using Amplitude.Unity.Framework;
using Amplitude.Unity.View;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// The mark the inspect cursor leaves on the galaxy, for whoever is watching the screen while
    /// somebody else drives it from the keyboard: a square drawn round the cell by the MOD, in
    /// SCREEN space, over everything the game has already drawn.
    ///
    /// WHY THE MOD DRAWS IT ITSELF. Every attempt to borrow one of the game's own renderers failed on
    /// the same wall, and the failures are recorded in `docs/galaxy-map.md` so nobody spends another
    /// stage on them: this build's line material eats several units off each end of a line and ignores
    /// the width argument (a hairline that vanishes at any distance, and an overshooting frame if the
    /// ends are extended past the corners to compensate), the circle renderer accepts a circle,
    /// reports it visible and never draws it, and the quad renderer was loaded with no materials at
    /// all. All three are also drawn IN THE WORLD, which makes the mark shrink with the camera - and
    /// the case that has to work is a one-unit cell at full overview zoom, which in world space is
    /// about one pixel wide.
    ///
    /// So the square is not in the world at all. The cell's four corners are projected through the
    /// galaxy camera every frame the mode is live, and the frame is stroked in IMGUI over the result:
    ///
    /// - IMGUI COMPOSITES ABOVE THE SCENE, so nothing on the map can hide the square. `GUI.depth` is
    ///   pushed far negative so it also sits over the game's own IMGUI (lower depth draws in front).
    /// - THICKNESS AND SIZE ARE IN PIXELS, so the camera cannot thin the square away: a band of
    ///   <see cref="Thickness"/> pixels whatever the zoom, and a floor of <see cref="MinSize"/> pixels
    ///   on the square itself, so a 1x1 cell seen from the whole galaxy still gets an unmistakable
    ///   marker centred exactly on it.
    /// - THE COLOUR IS A COLOUR. Drawing our own texture means the pale cyan asked for is the pale
    ///   cyan drawn, at the alpha asked for - neither was true of the palette-indexed line shader. A
    ///   dark backing band is stroked first so the square reads against a bright nebula as well as
    ///   against empty space.
    ///
    /// The world rect is only rewritten when the CELL moves; the projection is redone every frame, so
    /// the square stays on its cell through the camera's slide and through any zoom the player makes.
    /// Cost while armed is four `WorldToScreenPoint` calls and eight textured rects a frame, and
    /// nothing whatever when it is not: <see cref="Hide"/> destroys the host object outright rather
    /// than disabling it, which is also what keeps a hot reload clean - a behaviour left alive would
    /// belong to an assembly the mod has let go of.
    /// </summary>
    internal static class InspectMarker
    {
        /// <summary>The name the host wears, so a leaked one from an earlier load can be found and
        /// destroyed even though the type that made it is gone.</summary>
        internal const string HostName = "ES2Access inspect marker";

        private static InspectMarkerDrawer _drawer;

        /// <summary>Put the square round a cell, given in the galaxy's own world coordinates (the map
        /// is drawn in x/z at y = 0). Creates the host the first time and afterwards writes four
        /// floats.</summary>
        public static void Show(float lowX, float highX, float lowY, float highY)
        {
            try
            {
                if (_drawer == null)
                {
                    Sweep();
                    GameObject host = new GameObject(HostName);
                    host.hideFlags = HideFlags.DontSave;
                    _drawer = host.AddComponent<InspectMarkerDrawer>();
                }

                _drawer.Put(lowX, highX, lowY, highY);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: drawing the inspect cursor threw: " + e);
            }
        }

        /// <summary>Take the square off the screen. Safe to call when nothing was ever drawn, and the
        /// whole of this mechanism's teardown: mode exit, page pop and mod stop all end here.</summary>
        public static void Hide()
        {
            try
            {
                if (_drawer != null)
                {
                    UnityEngine.Object.Destroy(_drawer.gameObject);
                }

                _drawer = null;
                Sweep();
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: taking the inspect cursor off the screen threw: " + e);
            }
        }

        /// <summary>Destroy any host left behind by an earlier load of this assembly. Nothing should
        /// ever be found - <see cref="Hide"/> runs from the mod's own teardown - but a behaviour whose
        /// type has been unloaded cannot be reached any other way, and one left drawing would be a
        /// square nobody can move or clear for the rest of the session.</summary>
        private static void Sweep()
        {
            GameObject stale = GameObject.Find(HostName);
            if (stale != null)
            {
                UnityEngine.Object.Destroy(stale);
            }
        }
    }

    /// <summary>The per-frame half of <see cref="InspectMarker"/>: it holds the cell in world
    /// coordinates and strokes the projected square.</summary>
    internal sealed class InspectMarkerDrawer : MonoBehaviour
    {
        /// <summary>How wide the bright band is, in pixels. Constant in SCREEN space, which is the
        /// whole point: no camera distance can thin it away.</summary>
        private const float Thickness = 3f;

        /// <summary>The smallest square that will be drawn, in pixels. A 1x1 cell at full overview zoom
        /// projects to about one pixel; grown to this about its own centre it is still exactly where
        /// the cell is, and is now something a player can see.</summary>
        private const float MinSize = 26f;

        /// <summary>Breathing room outside the cell's own edges, in pixels, so the square sits round
        /// what is in the cell rather than through it.</summary>
        private const float Pad = 3f;

        /// <summary>Pale cyan against a map of blue-white lanes and orange stars - bright enough to
        /// find, soft enough to look at.</summary>
        private static readonly Color Paint = new Color(0.62f, 0.94f, 1f, 0.85f);

        /// <summary>The band stroked under the bright one, so the square reads against a pale nebula
        /// as well as against empty space.</summary>
        private static readonly Color Backing = new Color(0f, 0.06f, 0.12f, 0.6f);

        /// <summary>Drawn far in front of everything: in IMGUI the LOWER depth is the nearer one.
        /// </summary>
        private const int Depth = -1000;

        private float _lowX;
        private float _highX;
        private float _lowY;
        private float _highY;
        private bool _placed;

        private Texture2D _pixel;
        private ICameraService _cameras;

        /// <summary>Where the cell is, in the galaxy's world coordinates.</summary>
        public void Put(float lowX, float highX, float lowY, float highY)
        {
            _lowX = lowX;
            _highX = highX;
            _lowY = lowY;
            _highY = highY;
            _placed = true;
        }

        private void Awake()
        {
            _pixel = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            _pixel.hideFlags = HideFlags.DontSave;
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnDestroy()
        {
            if (_pixel != null)
            {
                UnityEngine.Object.Destroy(_pixel);
                _pixel = null;
            }
        }

        private void OnGUI()
        {
            if (!_placed || _pixel == null || Event.current == null
                || Event.current.type != EventType.Repaint)
            {
                return;
            }

            try
            {
                Camera camera = Eye();
                if (camera == null)
                {
                    return;
                }

                Rect square;
                if (!Project(camera, out square))
                {
                    return;
                }

                int was = GUI.depth;
                Color kept = GUI.color;
                GUI.depth = Depth;
                Stroke(square, Thickness + 2f, Backing);
                Stroke(square, Thickness, Paint);
                GUI.color = kept;
                GUI.depth = was;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the inspect cursor's square threw: " + e);
                enabled = false;
            }
        }

        /// <summary>
        /// The cell's four corners as one axis-aligned rect in GUI coordinates.
        ///
        /// The camera looks down on the map from an angle, so the projected cell is a quadrilateral
        /// rather than a rectangle; its bounding box is what is drawn, which is a square round the area
        /// and never smaller than it. False where the cell is behind the camera, which cannot happen on
        /// this map but would draw a mirrored box if it did.
        /// </summary>
        private bool Project(Camera camera, out Rect square)
        {
            square = new Rect();
            Vector3 a = camera.WorldToScreenPoint(new Vector3(_lowX, 0f, _lowY));
            Vector3 b = camera.WorldToScreenPoint(new Vector3(_highX, 0f, _lowY));
            Vector3 c = camera.WorldToScreenPoint(new Vector3(_lowX, 0f, _highY));
            Vector3 d = camera.WorldToScreenPoint(new Vector3(_highX, 0f, _highY));
            if (a.z <= 0f || b.z <= 0f || c.z <= 0f || d.z <= 0f)
            {
                return false;
            }

            float left = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)) - Pad;
            float right = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)) + Pad;
            // WorldToScreenPoint measures from the BOTTOM of the window and IMGUI from the top.
            float top = (float)Screen.height
                - Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y)) - Pad;
            float bottom = (float)Screen.height
                - Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)) + Pad;

            square = Floor(left, top, right - left, bottom - top);
            return true;
        }

        /// <summary>The rect, never smaller than <see cref="MinSize"/> on either side and grown about
        /// its own centre so the mark stays exactly on the cell.</summary>
        private static Rect Floor(float x, float y, float width, float height)
        {
            if (width < MinSize)
            {
                x -= (MinSize - width) * 0.5f;
                width = MinSize;
            }

            if (height < MinSize)
            {
                y -= (MinSize - height) * 0.5f;
                height = MinSize;
            }

            return new Rect(x, y, width, height);
        }

        /// <summary>Four bands round the rect, drawn inward from its edges.</summary>
        private void Stroke(Rect square, float thickness, Color color)
        {
            GUI.color = color;
            float x = square.x - (thickness - Thickness) * 0.5f;
            float y = square.y - (thickness - Thickness) * 0.5f;
            float width = square.width + (thickness - Thickness);
            float height = square.height + (thickness - Thickness);
            GUI.DrawTexture(new Rect(x, y, width, thickness), _pixel);
            GUI.DrawTexture(new Rect(x, y + height - thickness, width, thickness), _pixel);
            GUI.DrawTexture(new Rect(x, y + thickness, thickness, height - thickness - thickness),
                _pixel);
            GUI.DrawTexture(
                new Rect(x + width - thickness, y + thickness, thickness,
                    height - thickness - thickness),
                _pixel);
        }

        /// <summary>The camera the map is drawn through. Looked up once and re-asked whenever the
        /// service has let go of it.</summary>
        private Camera Eye()
        {
            if (_cameras == null)
            {
                _cameras = Services.GetService<ICameraService>();
            }

            return _cameras == null ? null : _cameras.Camera;
        }
    }
}

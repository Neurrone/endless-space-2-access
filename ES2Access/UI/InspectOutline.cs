using System;
using Amplitude.Unity.Framework;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// The square the inspect cursor draws on the galaxy - a thick band round the cell, for whoever is
    /// watching the screen while somebody else drives it from the keyboard.
    ///
    /// THE BAND IS MADE OF NESTED HAIRLINES, which is not the obvious way to draw a thick border and is
    /// the only one this engine offers. Measured, in this order:
    ///
    /// - The line's own WIDTH is ignored by the material the map's lanes are drawn with: 0.1 and 3.0
    ///   draw the same one-pixel lane (crop evidence, two lines an inch apart on the screen and
    ///   identical). The width does reach the shader (<c>LineRendererManager</c> sets <c>_Width</c> per
    ///   line and fills <c>GPULineData.Width</c> for the instanced path), so it is the shader that
    ///   discards it, and no material index can be chosen round that: all fourteen the manager holds
    ///   are lane, trade-route, diplomacy and hacking-route shaders.
    /// - FILLED QUADS cannot be had. <c>QuadRendererManager</c> is loaded with an EMPTY material list
    ///   (measured: <c>materials.Count == 0</c>), every quad the build defines is a distance-field
    ///   NUMBER (<c>Amplitude/Galaxy/PathNumber</c>, the turn markers on a fleet's path), and
    ///   <c>QuadToRender</c> wants an atlas element - so a filled rectangle would need a shader and a
    ///   sprite the game does not have.
    /// - A CIRCLE ring is drawn by a renderer the galaxy view has switched OFF. Circles created
    ///   directly on the galaxy's own layers reported themselves visible and drew nothing, and the
    ///   reason is in the mask: every circle the game itself has live sits on <c>PlanetOrbit</c> or
    ///   <c>CurvedLine</c>, and <c>IPrimitiveMaskFilterService.GetCurrentPrimitiveMask</c> has both
    ///   cleared while the galaxy is the view. The <c>Line</c> layer is the one that is on, which is
    ///   why the lanes - and this - are on the screen at all.
    ///
    /// So the thickness is spatial rather than drawn: several hairlines a fraction of a unit apart,
    /// nested inward from the cell's edge, which read as one solid band. It scales with the cursor, so
    /// a big square gets a proportionally heavy border rather than the same hairline round more sky.
    ///
    /// AND THE SIDES OVERSHOOT THE CORNERS, which is the second thing this material forces. It eats
    /// several units off each END of a line - a lane is drawn short of the stars it joins - so a SHORT
    /// line draws nothing at all: measured, a three-unit line is invisible under every one of the
    /// fourteen materials, a sixteen-unit one draws a stub, a thirty-three-unit one draws almost
    /// whole. A square small enough to be a cursor is made of sides too short to exist, which is why
    /// the outline this replaced could not be seen at the default size. Run each side well past the
    /// corner and the eaten part falls outside the cell, so the cell's own edges are drawn whatever
    /// the cursor's size - and what somebody watching sees is a frame with crop marks running out of
    /// it, which picks the cell out of a busy map better than a closed box would.
    ///
    /// Drawn through the map's OWN line renderer (<c>ILineRendererService</c>), the one that draws
    /// every starlane, rather than through a mesh or a GameObject of the mod's. Two things come free
    /// that way: the lines are already in the galaxy's world space at the plane the map is drawn on,
    /// and moving or resizing the cursor is four field writes on data objects the renderer reads live
    /// each frame (<c>LineToRender</c> is a plain record - <c>Position0/Position1</c> are public
    /// fields), so a sweep of the map allocates nothing and re-initialises nothing.
    ///
    /// Two things about such a line cannot be invented (and a line that gets either wrong is still
    /// accepted, still reports itself <c>Visible</c>, and is simply not on the screen):
    ///
    /// - THE MATERIAL is an INDEX into a private array the renderer was loaded with
    ///   (<c>LineRendererManager.GetMaterialIndex</c> answers -1 for anything foreign), so it is
    ///   BORROWED off a line the game is already drawing - any warplink on the map.
    /// - THE COLOUR IS NOT A COLOUR. The shader reads a <c>Color32</c> as a pair of PACKED 16-bit
    ///   indices into the GPU colour palette (<c>GalaxyLink.Refresh</c> and
    ///   <c>GalaxyStarSystem</c>'s "defaultWhiteEncodedColor" both build one the same way), so
    ///   <c>(255, 255, 255, 255)</c> is not white, it is two nonsense slot numbers. The palette hands
    ///   out slots (<c>IGPUColorEvolutionService.RegisterColorSlot</c>), so the outline takes one of
    ///   its own, puts its colour in it, and gives it back on the way out.
    ///
    /// Everything taken is given back: <see cref="Clear"/> releases all four lines and frees the
    /// colour slot, and it runs on the way out of the mode, when the page goes, and at teardown. A
    /// line left behind would be drawn by a renderer the mod can no longer reach, and a leaked slot
    /// is one fewer colour the map can draw with for the rest of the session.
    /// </summary>
    internal sealed class InspectOutline
    {
        /// <summary>The width the line is created with - the same figure the game's own starlanes use.
        /// MEASURED: this material's shader ignores it (0.1, 2 and 20 all draw the same one-pixel-ish
        /// lane), which is why the band above is made of several lines rather than of one wide one.
        /// </summary>
        private const float Width = 0.1f;

        /// <summary>How heavy the band is, as a fraction of the cursor's width - so the border keeps
        /// the same visual weight as the square grows.</summary>
        private const float Weight = 0.12f;

        /// <summary>The band never thinner than this, or a one-unit cursor would be a hairline again,
        /// and never thicker, or the eleven-unit cursor's border would start to be the cursor.</summary>
        private const float Thinnest = 0.2f;
        private const float Thickest = 1.2f;

        /// <summary>How far apart the nested lines are. Small enough that they read as one band at the
        /// zooms the map is played at, and the count is capped so that a thick band costs a bounded
        /// number of lines rather than one per pixel.</summary>
        private const float Spacing = 0.05f;
        private const int FewestPerSide = 4;
        private const int MostPerSide = 16;

        /// <summary>How far past each corner a side runs, in galaxy units. Comfortably more than the
        /// few units this material eats off a line's ends, so the cell's own edge is always inside the
        /// drawn part.</summary>
        private const float Overshoot = 10f;

        /// <summary>The colour asked for. MEASURED: the drawn square comes out a pale cyan whatever is
        /// asked for here, so what the colour buys today is a VALID slot rather than a chosen hue -
        /// which is still enough, since pale cyan is nothing else the map draws. Left as a real request
        /// because it is the game's own idiom and the hook a later session would fix the hue at.
        /// </summary>
        private static readonly Color Paint = new Color(1f, 0.82f, 0.15f, 1f);

        /// <summary>The renderer context the galaxy's own links take their colour slots from
        /// (<c>GalaxyLink.ExtractRendererService</c>). A slot from any other context is a number this
        /// shader's palette does not have.</summary>
        private const int ColorContext = 5;

        private readonly LineToRender[] _lines = new LineToRender[MostPerSide * 4];
        private int _used;
        private ILineRendererService _service;
        private IGPUColorEvolutionService _colors;
        private int _slot = -1;
        private int _material = -1;
        private Color32 _encoded;

        /// <summary>Put the outline round a cell, given in the galaxy's own coordinates. Does nothing
        /// at all where the renderer or a material to borrow cannot be found, which is the same
        /// silence the mode keeps for every other thing it cannot draw.</summary>
        public void Draw(float lowX, float highX, float lowY, float highY, int size)
        {
            try
            {
                if (!Acquire())
                {
                    return;
                }

                float thickness = size * Weight;
                if (thickness < Thinnest)
                {
                    thickness = Thinnest;
                }
                else if (thickness > Thickest)
                {
                    thickness = Thickest;
                }

                int perSide = (int)(thickness / Spacing) + 1;
                if (perSide < FewestPerSide)
                {
                    perSide = FewestPerSide;
                }
                else if (perSide > MostPerSide)
                {
                    perSide = MostPerSide;
                }

                float step = thickness / (perSide - 1);
                int at = 0;
                for (int ring = 0; ring < perSide; ring++)
                {
                    float inset = ring * step;
                    float west = lowX + inset;
                    float east = highX - inset;
                    float south = lowY + inset;
                    float north = highY - inset;

                    // Four rails on the galaxy plane (y = 0 - the map is drawn in x/z and
                    // GalaxyPosition converts that way), each running Overshoot past both corners it
                    // passes through.
                    Set(
                        at++,
                        new Vector3(west - Overshoot, 0f, south),
                        new Vector3(east + Overshoot, 0f, south)
                    );
                    Set(
                        at++,
                        new Vector3(west - Overshoot, 0f, north),
                        new Vector3(east + Overshoot, 0f, north)
                    );
                    Set(
                        at++,
                        new Vector3(west, 0f, south - Overshoot),
                        new Vector3(west, 0f, north + Overshoot)
                    );
                    Set(
                        at++,
                        new Vector3(east, 0f, south - Overshoot),
                        new Vector3(east, 0f, north + Overshoot)
                    );
                }

                Release(at);
                _used = at;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: drawing the inspect cursor threw: " + e);
            }
        }

        /// <summary>Give every line back. Safe to call when nothing was ever drawn.</summary>
        public void Clear()
        {
            try
            {
                Release(0);
                _used = 0;
                if (_colors != null && _slot >= 0)
                {
                    _colors.FreeColorSlot(_slot);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: releasing the inspect cursor's lines threw: " + e);
            }
            finally
            {
                _service = null;
                _colors = null;
                _slot = -1;
                _material = -1;
            }
        }

        /// <summary>One side of the square: created and shown the first time, and afterwards two field
        /// writes on the object the renderer is already reading.</summary>
        private void Set(int index, Vector3 from, Vector3 to)
        {
            LineToRender line = _lines[index];
            if (line == null)
            {
                line = _service.CreateLine(from, to, Width, _encoded, _encoded, _material);
                _lines[index] = line;
                _service.ShowLine(line);
                return;
            }

            line.Position0 = from;
            line.Position1 = to;
        }

        /// <summary>Give back every line from <paramref name="from"/> on - the surplus after the band
        /// grew thinner, or all of them on the way out. Safe when nothing was ever drawn.</summary>
        private void Release(int from)
        {
            for (int i = from; i < _lines.Length; i++)
            {
                if (_lines[i] != null && _service != null)
                {
                    _service.ReleaseLine(_lines[i]);
                }

                _lines[i] = null;
            }
        }

        /// <summary>The renderer, a material index it will accept and a colour slot of our own. All
        /// three are looked up once and held for as long as the outline is up; a map with no starlane
        /// drawn on it has no material to borrow and gets no outline.</summary>
        private bool Acquire()
        {
            if (_service != null && _material >= 0 && _slot >= 0)
            {
                return true;
            }

            _service = Services.GetService<ILineRendererService>();
            _colors = Amplitude.Unity.Graphics.Services.GetService<IGPUColorEvolutionService>(
                ColorContext
            );
            if (_service == null || _colors == null)
            {
                return false;
            }

            GalaxyWarplink lane = UnityEngine.Object.FindObjectOfType<GalaxyWarplink>();
            LineToRender drawn = lane == null ? null : lane.Line;
            _material = drawn == null ? -1 : drawn.MaterialType;
            if (_material < 0)
            {
                return false;
            }

            if (_slot < 0)
            {
                // Registered AND written, the way the galaxy's own links do it: the palette evolves a
                // slot towards its colour, so the colour a slot is registered with is only where it
                // starts from.
                _slot = _colors.RegisterColorSlot(Paint);
                _colors.ChangeColorSlot(_slot, Paint, 1f);
            }

            // The two halves of the Color32 are two 16-bit slot numbers - the line's ends. The same
            // slot in both is a line of one flat colour.
            _encoded = new Color32(
                (byte)(_slot & 0xFF),
                (byte)((_slot >> 8) & 0xFF),
                (byte)(_slot & 0xFF),
                (byte)((_slot >> 8) & 0xFF)
            );
            return _slot >= 0;
        }
    }
}

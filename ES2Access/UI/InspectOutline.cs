using System;
using Amplitude.Unity.Framework;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// The mark the inspect cursor leaves on the galaxy - four hairlines on the cell's OWN edges and
    /// not one unit beyond them, for whoever is watching the screen while somebody else drives it from
    /// the keyboard.
    ///
    /// WHAT IS NOT DRAWN, and why, because both alternatives were tried and measured:
    ///
    /// - A SOFT RING is what this wants to be, and this engine will not draw one on this view. The
    ///   circle service is reachable (<c>ICircleRendererService</c> at renderer context ZERO, not 5 -
    ///   read off a live orbit ring's own <c>RendererContextIndex</c>; asking at 5 answers null, which
    ///   is what an earlier session mistook for the renderer being switched off), the primitive mask
    ///   is no obstacle (measured live: every bit but <c>CurvedLine</c> and <c>QuestMarker</c>, so
    ///   <c>Line</c> and <c>PlanetOrbit</c> are both on), and a circle created there reports itself
    ///   <c>Visible</c> and sits in the manager's own shown list. It still does not appear - not on
    ///   the <c>Line</c> layer, not on <c>PlanetOrbit</c> where all 444 of the game's own circles sit,
    ///   and not with a live orbit ring's exact material, colour and width lent to it. The decisive
    ///   measurement is the other direction: forcing a DRAWN orbit ring's own <c>CircleToRender.Radius</c>
    ///   from 2.8 to 12 changed nothing on the screen either, so the rings a player can see are not
    ///   being drawn from that list at all and nothing put into it can be seen. Crops at every step.
    /// - THE OVERSHOOTING FRAME that used to be here is gone by owner ruling: it ran every side ten
    ///   units past the corner so the part this material eats off a line's ends fell outside the cell,
    ///   and what that looks like is a broken crop-mark frame sprawling across the map.
    ///
    /// So the mark is honest about what the engine allows: the four edges, exactly. This material eats
    /// several units off each END of a line (measured: a three-unit line is invisible, sixteen draws a
    /// stub, thirty-three draws almost whole), so a small cursor's sides are too short to appear and
    /// nothing is drawn at all. That is accepted rather than papered over - a cursor that sprawls
    /// across the map to make itself visible is worse than one that is only heard - and it is the
    /// owner's call to revisit if a sighted onlooker needs the small sizes marked.
    ///
    /// Drawn through the map's OWN line renderer (<c>ILineRendererService</c>), the one that draws
    /// every starlane. Two things come free: the lines are already in the galaxy's world space at the
    /// plane the map is drawn on, and moving or resizing the cursor is two field writes on data
    /// objects the renderer reads live each frame (<c>LineToRender.Position0/Position1</c> are public
    /// fields), so a sweep of the map allocates nothing and re-initialises nothing.
    ///
    /// Two things about such a line cannot be invented (and a line that gets either wrong is still
    /// accepted, still reports itself <c>Visible</c>, and is simply not on the screen):
    ///
    /// - THE MATERIAL is an INDEX into a private array the renderer was loaded with
    ///   (<c>LineRendererManager.GetMaterialIndex</c> answers -1 for anything foreign), so it is
    ///   BORROWED off a line the game is already drawing - any warplink on the map.
    /// - THE COLOUR IS NOT A COLOUR. The shader reads a <c>Color32</c> as a pair of PACKED 16-bit
    ///   indices into the GPU colour palette (<c>GalaxyLink.Refresh</c> and <c>GalaxyStarSystem</c>'s
    ///   "defaultWhiteEncodedColor" both build one the same way), so the outline takes a slot of its
    ///   own, puts its colour in it, and gives it back on the way out.
    ///
    /// Everything taken is given back: <see cref="Clear"/> releases all four lines and frees the
    /// colour slot, and it runs on the way out of the mode, when the page goes, and at teardown. A
    /// line left behind would be drawn by a renderer the mod can no longer reach, and a leaked slot is
    /// one fewer colour the map can draw with for the rest of the session.
    /// </summary>
    internal sealed class InspectOutline
    {
        /// <summary>The width the line is created with - the same figure the game's own starlanes use.
        /// MEASURED: this material's shader ignores it (0.1, 2 and 20 all draw the same one-pixel-ish
        /// lane), so there is no thickness to choose and no harshness to dial down: what is drawn is
        /// the thinnest line this map has.</summary>
        private const float Width = 0.1f;

        /// <summary>The colour asked for - a soft amber against a map whose own lines are blue-white.
        /// MEASURED: the drawn line came out pale cyan whatever was asked, so what the colour buys
        /// today is a VALID slot rather than a chosen hue. Left as a real request because it is the
        /// game's own idiom and the hook a later session would fix the hue at.</summary>
        private static readonly Color Paint = new Color(1f, 0.72f, 0.28f, 0.55f);

        /// <summary>The renderer context the galaxy's own links take their colour slots from
        /// (<c>GalaxyLink.ExtractRendererService</c>). A slot from any other context is a number this
        /// shader's palette does not have.</summary>
        private const int ColorContext = 5;

        private const int Sides = 4;

        private readonly LineToRender[] _lines = new LineToRender[Sides];
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

                // Four rails on the galaxy plane (y = 0 - the map is drawn in x/z and GalaxyPosition
                // converts that way), each ending exactly on the cell's corner.
                Set(0, new Vector3(lowX, 0f, lowY), new Vector3(highX, 0f, lowY));
                Set(1, new Vector3(lowX, 0f, highY), new Vector3(highX, 0f, highY));
                Set(2, new Vector3(lowX, 0f, lowY), new Vector3(lowX, 0f, highY));
                Set(3, new Vector3(highX, 0f, lowY), new Vector3(highX, 0f, highY));
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
                for (int i = 0; i < _lines.Length; i++)
                {
                    if (_lines[i] != null && _service != null)
                    {
                        _service.ReleaseLine(_lines[i]);
                    }

                    _lines[i] = null;
                }

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

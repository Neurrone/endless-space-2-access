using System;
using System.Collections.Generic;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// A tooltip carrier this mod owns, for a dossier the game has the DATA for but is not currently
    /// drawing a widget for.
    ///
    /// Why one is needed. A renderer-assembled tooltip has no words until the tooltip window draws
    /// it, and the window draws whatever the engine's single hover pointer
    /// (<c>AgeManager.OverrolledTransform</c>) is aimed at. So reading such a dossier means aiming at
    /// SOME widget carrying it - and the game only hangs one on the screen while it is drawing the
    /// picture the dossier belongs to. A system's deposit icons are drawn only when the camera is
    /// close enough, which left a keyboard player able to reach a deposit's dossier at one zoom and
    /// not at another, for content the map is not hiding from them at all.
    ///
    /// Why one WORKS. The engine's own can-I-draw test reads two fields and a class
    /// (<c>GuiTooltipController.ReadTooltipInformation</c>: a Content string or a Target, plus the
    /// Class naming the panel-feature list) and never asks whose widget it is or whether that widget
    /// is on screen. Given the same Class, the same Target wrapper and the same Context the game's own
    /// binding would use, the window assembles byte-identical words - measured 2026-08-23 on a
    /// deposit group at a camera step where the label draws no deposit icons at all.
    ///
    /// What a carrier is. A GameObject with an <c>AgeTransform</c> and an <c>AgeTooltip</c>, parented
    /// under a live AGE screen so the tooltip controller can resolve a screen to position against -
    /// but under an intermediate GameObject that carries NO <c>AgeTransform</c>, so
    /// <c>AgeTransform.Init</c> finds no parent to register with and the carrier never joins the
    /// game's own widget tree: nothing lays it out, nothing draws it, and no walk of the window's
    /// children can trip over it.
    ///
    /// WHERE the panel then appears is a decision, because there is no widget for it to sit under:
    /// the BOTTOM-LEFT of the screen (owner ruling 2026-08-23). The carrier is parked at the screen's
    /// bottom-left corner with the anchor mode that puts the window ABOVE its anchor
    /// (<c>TOP_LEFT</c>), so the panel's own bottom edge lands on the bottom of the screen whatever
    /// height it turns out to have and nothing is ever clipped off - which parking it at the corner
    /// with the ordinary <c>BOTTOM_LEFT</c> aim would do to every panel. A carrier therefore declares
    /// its own anchor and <see cref="PointerFocus"/> leaves it alone.
    ///
    /// The aim rule these serve (owner ruling 2026-08-23): aim at the game's own widget wherever the
    /// game is drawing one, and only fall back to a carrier where it is not. Words are identical
    /// either way, so nothing the player hears changes as the camera moves.
    ///
    /// One carrier per KEY, kept for the session: the pointer, the drawn-tooltip reader and the
    /// parity audit all recognise a tooltip by reference, so a dossier that swapped carriers between
    /// frames would read as a different dossier each time. Rebinding is gated on a caller's STAMP
    /// because writing <c>Target</c> raises the engine's dirty-target edge, which resets the tooltip
    /// controller's countdown - written every frame, the tooltip would never finish appearing.
    /// </summary>
    public static class ScratchTooltips
    {
        private struct Slot
        {
            public AgeTooltip Tip;
            public long Stamp;
            public bool Bound;
        }

        private static readonly Dictionary<string, Slot> Carriers = new Dictionary<string, Slot>();
        private static GameObject _host;
        private static IAgeScreen _screen;

        /// <summary>Whether this tooltip is one of the mod's carriers - asked by
        /// <see cref="PointerFocus"/>, which re-anchors every tooltip it aims at to the widget under
        /// the cursor and must not do that to a carrier: a carrier IS its own placement
        /// (<see cref="Place"/>), and there is no widget under it to anchor to.</summary>
        public static bool Owns(AgeTooltip tooltip)
        {
            if (tooltip == null)
            {
                return false;
            }

            Dictionary<string, Slot>.Enumerator walk = Carriers.GetEnumerator();
            while (walk.MoveNext())
            {
                if (ReferenceEquals(walk.Current.Value.Tip, tooltip))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The carrier for <paramref name="key"/>, and whether the caller must (re)stamp it.
        ///
        /// <paramref name="stamp"/> is whatever the dossier's content depends on - the turn, the
        /// empire, the definition. It answers true the first time and whenever the stamp changes;
        /// answering true is the caller's permission to build the wrapper and assign
        /// Class/Content/Target/Context, and answering false is what keeps the engine's dirty-target
        /// edge quiet so the tooltip has time to draw.
        /// </summary>
        public static bool Rebind(string key, long stamp, out AgeTooltip carrier)
        {
            carrier = null;
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    return false;
                }

                Slot slot;
                bool known = Carriers.TryGetValue(key, out slot);
                if (!known || slot.Tip == null)
                {
                    slot = new Slot { Tip = Create(key) };
                    if (slot.Tip == null)
                    {
                        return false;
                    }
                }

                carrier = slot.Tip;
                Place(slot.Tip);
                bool rebind = !slot.Bound || slot.Stamp != stamp;
                if (rebind)
                {
                    slot.Stamp = stamp;
                    slot.Bound = true;
                }

                Carriers[key] = slot;
                return rebind;
            }
            catch (Exception e)
            {
                Log.Warn("tooltips: making a scratch carrier threw: " + e);
                carrier = null;
                return false;
            }
        }

        /// <summary>
        /// Park a carrier over a widget the game IS drawing, so its panel appears where the picture it
        /// explains is instead of at the screen's corner.
        ///
        /// The corner (<see cref="Place"/>) is the answer for a dossier whose picture the game is not
        /// drawing at all. A carrier standing in for one marker of a ring the game is drawing in front
        /// of the player has a place of its own, and putting the panel anywhere else asks a sighted
        /// player to look away from the thing being described.
        ///
        /// <c>TOP_LEFT</c> is the anchor the game's own population marker uses
        /// (<c>PopulationMarker.Bind</c>): the panel's bottom edge lands on the marker's top, so it
        /// grows upwards off a card that sits low on the screen rather than off the bottom of it.
        /// Call it AFTER <see cref="Rebind"/>, which re-parks every carrier at the corner.
        /// </summary>
        public static void PlaceOver(AgeTooltip carrier, AgeTransform widget)
        {
            AgeTransform rect = carrier == null ? null : carrier.AgeTransform;
            if (rect == null || widget == null)
            {
                return;
            }

            Rect at = widget.GetGlobalPosition();
            rect.X = at.x;
            rect.Y = at.y;
            rect.Width = at.width;
            rect.Height = at.height;
            carrier.Anchor = rect;
            carrier.AnchorMode = AgeTooltipAnchorMode.TOP_LEFT;
        }

        /// <summary>Every carrier goes with the mod: they are scene objects this assembly created,
        /// and one left behind after a reload would be pointed at by nothing and pointed at nothing.
        /// </summary>
        public static void Shutdown()
        {
            Carriers.Clear();
            try
            {
                if (_host != null)
                {
                    UnityEngine.Object.Destroy(_host);
                }
            }
            catch (Exception e)
            {
                Log.Warn("tooltips: dropping the scratch carriers threw: " + e);
            }

            _host = null;
            _screen = null;
        }

        /// <summary>
        /// Park a carrier at the bottom-left corner of the screen, pointing its panel UP.
        ///
        /// The engine places a tooltip window off its anchor's rect and clamps nothing
        /// (<c>GuiTooltipController.ComputeWindowPosition</c>): the ordinary aim (<c>BOTTOM_LEFT</c>)
        /// puts the window's TOP edge on the anchor's bottom, which from the corner would drop every
        /// panel off the bottom of the screen. <c>TOP_LEFT</c> is the same corner read the other way -
        /// the window's BOTTOM edge lands on the anchor - so a panel of any height sits above the
        /// corner and inside the screen.
        ///
        /// Re-asked on every rebind rather than set once, because the corner moves with the
        /// resolution and the carriers outlive a change of it. Measured on 1280x800: a deposit
        /// dossier draws at <c>0,420,240,380</c>.
        /// </summary>
        private static void Place(AgeTooltip carrier)
        {
            AgeTransform rect = carrier == null ? null : carrier.AgeTransform;
            AgeTransform root = _screen == null ? null : _screen.Root;
            if (rect == null || root == null)
            {
                return;
            }

            rect.X = 0f;
            rect.Y = root.Height;
            rect.Width = 0f;
            rect.Height = 0f;
            carrier.Anchor = rect;
            carrier.AnchorMode = AgeTooltipAnchorMode.TOP_LEFT;
        }

        private static AgeTooltip Create(string key)
        {
            GameObject host = Host();
            if (host == null)
            {
                return null;
            }

            // Built INACTIVE so both Awakes run after both components exist: AgeTransform caches its
            // AgeTooltip in Awake, and adding the tooltip to an already-live object leaves that cache
            // null - the controller then reads no tooltip at all off the transform it is aimed at.
            GameObject carrier = new GameObject("Dossier " + key);
            carrier.SetActive(false);
            carrier.transform.parent = host.transform;
            carrier.AddComponent<AgeTransform>();
            AgeTooltip tooltip = carrier.AddComponent<AgeTooltip>();
            carrier.SetActive(true);
            return tooltip;
        }

        /// <summary>The object the carriers hang under: a plain GameObject - deliberately with no
        /// <c>AgeTransform</c> - beneath the screen the game draws its own tooltip window on, so a
        /// carrier resolves the same screen the tooltip is positioned in and still has no AGE parent
        /// to register itself with.</summary>
        private static GameObject Host()
        {
            if (_host != null)
            {
                return _host;
            }

            IAgeScreen screen = TooltipScreen() ?? AnyScreen();
            if (screen == null || screen.UnityGameObject == null)
            {
                return null;
            }

            _host = new GameObject("ES2Access dossier carriers");
            _host.transform.parent = screen.UnityGameObject.transform;
            _screen = screen;
            return _host;
        }

        private static IAgeScreen TooltipScreen()
        {
            try
            {
                GuiTooltipWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GuiTooltipWindow>(false)
                    : null;
                return window == null || window.AgeTransform == null
                    ? null
                    : window.AgeTransform.Screen;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IAgeScreen AnyScreen()
        {
            try
            {
                AgeManager age = AgeManager.Instance;
                for (int i = 0; age != null && i < age.ScreenCount; i++)
                {
                    IAgeScreen screen = age.GetScreenAt(i);
                    if (screen != null && screen.UnityGameObject != null)
                    {
                        return screen;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }
    }
}

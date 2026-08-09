using System;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Makes the game look as though the mouse were resting on the control the keyboard is on: the
    /// button lights up, the menu it owns stays open, and the game's own tooltip appears for it. None
    /// of this is for the player who cannot see it - it is so that anyone else watching the screen can
    /// follow the game being played, which is the difference between playing alongside someone and
    /// playing next to them.
    ///
    /// Three separate pieces of the engine, driven the way the engine drives them itself:
    ///
    /// <list type="bullet">
    /// <item>the highlight is <c>AgeControlButton.SimulateHover</c>, which is the engine's own way of
    /// saying "look hovered" without a mouse event;</item>
    /// <item>the flyout is whatever the screen hands over as its open/close action - for the main menu,
    /// the same message the game sends itself when the pointer enters and leaves an entry;</item>
    /// <item>the tooltip is <c>AgeManager.OverrolledTransform</c>, the single field the tooltip
    /// controller watches. The controller reads it in Update and the engine recomputes it from the real
    /// cursor in LateUpdate, so it is written back at END OF FRAME, after the recompute and before the
    /// next read - the one point in the frame where a value survives to be seen.</item>
    /// </list>
    ///
    /// Requests are recorded, not performed: focus is re-committed after every rebuild and these are
    /// animations, which restart and flicker if poked twice. <see cref="Tick"/> compares what is wanted
    /// against what is showing and touches only the difference.
    ///
    /// A tooltip that is shown for the keyboard has to be moved to where the keyboard is. Most of the
    /// game's tooltips are declared <c>FREE</c>, meaning "draw me at the mouse pointer" - and the
    /// pointer is deliberately never moved, so during a keyboard session it sits wherever it was last
    /// left, often outside the window entirely, where the engine clamps the tooltip into a screen
    /// corner far from the control it describes. So while focus rests on a control its tooltip is
    /// re-declared as anchored to that control; the original anchor is put back when focus leaves,
    /// because the same tooltip belongs to the mouse again the moment the mouse returns to it.
    ///
    /// Some of what a tooltip is for cannot be read off the widget at all: a tooltip that names a CLASS
    /// has its words assembled by the renderer as it draws them. Showing it is therefore how those words
    /// come to exist, so <see cref="DrawnTooltipChanged"/> says when the drawing changed and whoever is
    /// holding the text reads it again.
    ///
    /// The real mouse still wins where it lands: move it over another control and the engine will
    /// unhover ours and close the flyout behind our back, and it stays that way until focus moves
    /// again. Keyboard focus is not defended frame by frame, because doing so would mean fighting the
    /// player's own hand.
    /// </summary>
    public static class PointerFocus
    {
        /// <summary>Where a focused control's tooltip is drawn: under it, left edges aligned. Clear of
        /// the control itself and of anything a flyout opens to its side, and one of the placements the
        /// game already uses for its own anchored tooltips.</summary>
        private const AgeTooltipAnchorMode FocusAnchorMode = AgeTooltipAnchorMode.BOTTOM_LEFT;

        private struct Spot
        {
            public AgeControlButton Button;
            public AgeControlToggle Toggle;
            public AgeTransform Hover;
            public AgeTooltip Tooltip;
            public AgeTransform Anchor;
            public object FlyoutKey;
            public Action<object, bool> Flyout;
        }

        private static Spot _wanted;
        private static Spot _showing;

        private static AgeTooltip _anchored;
        private static AgeTransform _anchorWas;
        private static AgeTooltipAnchorMode _anchorModeWas;

        private static AgeTooltip _drawn;
        private static float _drawnHeight;

        /// <summary>Told when the tooltip the game is DRAWING changes. A tooltip appears a fraction of
        /// a second after the pointer arrives and a class-driven one has no words until it is drawn, so
        /// whoever is holding those words - the review buffer - has to be told to read them again.
        /// </summary>
        public static Action DrawnTooltipChanged;

        /// <summary>Ask for <paramref name="button"/> to look hovered, with <paramref name="tooltip"/>
        /// shown for it. <paramref name="anchor"/> is what the tooltip is drawn under - the transform
        /// that hugs the visible text, which is not always the button; pass null to fall back to the
        /// transform the tooltip sits on. <paramref name="flyoutKey"/> names the menu that should be
        /// open while focus is here - sibling entries of one menu pass the same key, so stepping
        /// between them leaves it alone - and <paramref name="flyout"/> opens and closes the menu that
        /// key stands for.</summary>
        public static void MoveTo(
            AgeControlButton button,
            AgeTooltip tooltip,
            AgeTransform anchor = null,
            object flyoutKey = null,
            Action<object, bool> flyout = null
        )
        {
            _wanted = new Spot
            {
                Button = button,
                Tooltip = tooltip == null ? null : tooltip,
                Anchor = anchor,
                FlyoutKey = flyoutKey,
                Flyout = flyout,
            };
        }

        /// <summary>The same for a control the game drew as a TOGGLE - a card the player picks one of.
        /// A toggle has no <c>SimulateHover</c>: its highlight is driven by the interaction state the
        /// engine changes in <c>MouseEnter</c>, so that is what is called, which is also what makes any
        /// mouse-enter wiring the card carries run exactly as it would for a hand on the mouse.
        /// </summary>
        public static void MoveToToggle(
            AgeControlToggle toggle,
            AgeTooltip tooltip,
            AgeTransform anchor = null
        )
        {
            _wanted = new Spot
            {
                Toggle = toggle,
                Tooltip = tooltip,
                Anchor = anchor,
            };
        }

        /// <summary>Ask for the mouse to be treated as resting on <paramref name="widget"/>, which is
        /// not a button: a running total or an icon the keyboard has landed on. Nothing lights up -
        /// there is no button under it to light - but the game draws the tooltip it would draw for a
        /// pointer over it, which for a class-driven tooltip is the only place its words ever exist.
        /// </summary>
        public static void MoveTo(AgeTransform widget, AgeTooltip tooltip, AgeTransform anchor = null)
        {
            _wanted = new Spot
            {
                Hover = widget,
                Tooltip = tooltip,
                Anchor = anchor,
            };
        }

        /// <summary>Nothing should look hovered. Takes effect on the next <see cref="Tick"/>, so a blur
        /// immediately followed by a new focus never closes and reopens the same menu.</summary>
        public static void Release()
        {
            _wanted = new Spot();
        }

        /// <summary>Once per frame, after focus has settled.</summary>
        public static void Tick()
        {
            if (!ReferenceEquals(_showing.FlyoutKey, _wanted.FlyoutKey))
            {
                OpenFlyout(_showing, false);
                OpenFlyout(_wanted, true);
            }

            if (!ReferenceEquals(_showing.Button, _wanted.Button))
            {
                Hover(_showing.Button, false);
                Hover(_wanted.Button, true);
            }

            if (!ReferenceEquals(_showing.Toggle, _wanted.Toggle))
            {
                HoverToggle(_showing.Toggle, false);
                HoverToggle(_wanted.Toggle, true);
            }

            if (
                !ReferenceEquals(_showing.Tooltip, _wanted.Tooltip)
                || !ReferenceEquals(_showing.Hover, _wanted.Hover)
            )
            {
                RestoreAnchor();
                Unpoint(_showing);
                AnchorToFocus(_wanted.Tooltip, _wanted.Anchor);
            }

            _showing = _wanted;
            WatchDrawnTooltip();
        }

        /// <summary>Whether the tooltip window has changed what it is drawing since the last frame -
        /// a different tooltip, or the same one rebuilt. The height is what says "rebuilt": the window
        /// re-assembles a tooltip a few seconds in to add the detail the compact form left out, and
        /// growing is what that looks like from outside.</summary>
        private static void WatchDrawnTooltip()
        {
            AgeTooltip drawn = null;
            float height = 0f;
            try
            {
                GuiTooltipWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GuiTooltipWindow>(false)
                    : null;
                if (window != null && window.Shown && window.PanelFeaturesTable != null)
                {
                    drawn = window.AgeTooltip;
                    height = window.PanelFeaturesTable.Height;
                }
            }
            catch (Exception e)
            {
                Log.Warn("pointer: looking at the drawn tooltip threw: " + e);
            }

            if (ReferenceEquals(drawn, _drawn) && height == _drawnHeight)
            {
                return;
            }

            _drawn = drawn;
            _drawnHeight = height;
            if (DrawnTooltipChanged != null)
            {
                DrawnTooltipChanged();
            }
        }

        /// <summary>Stop the engine pointing at a tooltip this mod aimed it at. Letting go has to be
        /// said, not just stopped being said: the target is re-asserted every frame, and a control
        /// focus has left whose tooltip nothing replaced would otherwise stay on screen - drawn in the
        /// corner the engine parks a tooltip with nothing to hang under.</summary>
        private static void Unpoint(Spot spot)
        {
            AgeTransform hover = HoverTarget(spot);
            if (hover == null)
            {
                return;
            }

            try
            {
                AgeManager age = AgeManager.Instance;
                if (age != null && age.OverrolledTransform == hover)
                {
                    age.OverrolledTransform = null;
                }
            }
            catch (Exception e)
            {
                Log.Warn("pointer: releasing the tooltip target threw: " + e);
            }
        }

        /// <summary>End of frame: re-assert the tooltip target the engine cleared during its own
        /// LateUpdate, so the tooltip controller sees it on the next Update.</summary>
        public static void LateTick()
        {
            AgeTransform hover = _showing.Tooltip == null ? null : HoverTarget(_showing);
            if (hover == null)
            {
                return;
            }

            try
            {
                AgeManager age = AgeManager.Instance;
                if (age != null)
                {
                    age.OverrolledTransform = hover;
                }
            }
            catch (Exception e)
            {
                Log.Warn("pointer: pointing the tooltip at the focused control threw: " + e);
            }
        }

        /// <summary>What the engine is told the pointer is over. Named by the caller for a widget that
        /// is not a button; otherwise it is the widget the tooltip itself sits on, which is where a
        /// button's own hover target is.</summary>
        private static AgeTransform HoverTarget(Spot spot)
        {
            try
            {
                if (spot.Hover != null)
                {
                    return spot.Hover;
                }

                return spot.Tooltip == null ? null : spot.Tooltip.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Put everything back the way the game had it - the mod is going away, and a flyout
        /// left hanging open or a button left lit would outlive the code that knows why.</summary>
        public static void Shutdown()
        {
            Release();
            Tick();
            _showing = new Spot();
            _drawn = null;
            _drawnHeight = 0f;
            DrawnTooltipChanged = null;
        }

        private static void OpenFlyout(Spot spot, bool open)
        {
            if (spot.Flyout == null || spot.FlyoutKey == null)
            {
                return;
            }

            try
            {
                spot.Flyout(spot.FlyoutKey, open);
            }
            catch (Exception e)
            {
                Log.Warn("pointer: " + (open ? "opening" : "closing") + " a flyout threw: " + e);
            }
        }

        /// <summary>Point <paramref name="tooltip"/> at the control it belongs to instead of at the
        /// mouse. The anchor has to be named explicitly: an anchored tooltip that leaves it unset is
        /// given the tooltip controller's <c>DefaultAnchor</c>, a marker parked in a screen corner.
        ///
        /// The transform the tooltip sits on is the last resort rather than the obvious choice,
        /// because a clickable region is not the same shape as the thing it looks like: a main menu
        /// entry that opens onto sub-entries has its button stretched down and across to cover them,
        /// so drawing under the button would leave the tooltip floating in the space beside the
        /// flyout instead of under the words. The caller names the transform that holds the visible
        /// text, and every entry then reads the same however big its hit area is.</summary>
        private static void AnchorToFocus(AgeTooltip tooltip, AgeTransform anchor)
        {
            if (tooltip == null)
            {
                return;
            }

            try
            {
                _anchored = tooltip;
                _anchorWas = tooltip.Anchor;
                _anchorModeWas = tooltip.AnchorMode;
                tooltip.Anchor = anchor == null ? tooltip.AgeTransform : anchor;
                tooltip.AnchorMode = FocusAnchorMode;
            }
            catch (Exception e)
            {
                Log.Warn("pointer: anchoring the tooltip to the focused control threw: " + e);
            }
        }

        private static void RestoreAnchor()
        {
            AgeTooltip tooltip = _anchored;
            _anchored = null;
            if (tooltip == null)
            {
                return;
            }

            try
            {
                tooltip.Anchor = _anchorWas;
                tooltip.AnchorMode = _anchorModeWas;
            }
            catch (Exception e)
            {
                Log.Warn("pointer: putting a tooltip's anchor back threw: " + e);
            }
        }

        /// <summary>
        /// Enter and leave a toggle the way a mouse does. The cursor position is not decoration: the
        /// engine reads it back on the way out, propagating the leave to the parent control only when
        /// the cursor has left the parent too, so entering is told the middle of the card and leaving
        /// is told a point off the screen - which is where the mouse effectively is.
        /// </summary>
        private static void HoverToggle(AgeControlToggle toggle, bool hovered)
        {
            try
            {
                if (toggle == null)
                {
                    return;
                }

                if (hovered)
                {
                    toggle.MouseEnter(toggle.AgeTransform.GetGlobalPosition().center);
                }
                else
                {
                    toggle.MouseLeave(new UnityEngine.Vector2(-1f, -1f));
                }
            }
            catch (Exception e)
            {
                Log.Warn("pointer: hovering a toggle threw: " + e);
            }
        }

        private static void Hover(AgeControlButton button, bool hovered)
        {
            try
            {
                if (button != null)
                {
                    button.SimulateHover(hovered);
                }
            }
            catch (Exception e)
            {
                Log.Warn("pointer: highlighting a control threw: " + e);
            }
        }
    }
}

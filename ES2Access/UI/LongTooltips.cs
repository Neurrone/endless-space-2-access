using System;
using System.Collections.Generic;
using ES2Access.UI.Settings;

namespace ES2Access.UI
{
    /// <summary>
    /// THE WORDS OF A TOOLTIP THE GAME HAS NOT DRAWN YET, for the player who asked to hear them.
    ///
    /// A tooltip that names a CLASS is assembled by the tooltip window on hover, so at the moment
    /// focus lands on the control there is nothing to say: the words appear frames later, when the
    /// game's own hover delay expires and the window draws. That is why these tooltips reach the
    /// player through the review buffer and not the readout. With
    /// <see cref="LongTooltipSettings.Announced"/> on, the section still says nothing to the readout
    /// - it hands the readout THIS reader instead, which answers nothing until the drawing happens
    /// and the drawn words from then on. The part carrying it is live, so the navigator's live watch
    /// is what notices them arrive and speaks them, after the readout has finished.
    ///
    /// Two rules live here, and they are why this is a place rather than a lambda at the door:
    ///
    /// - ONCE PER LANDING, ON THE FIRST DRAW. The window draws a tooltip more than once while focus
    ///   sits still - the mod re-asks for one that was slow to appear, and the game re-assembles a
    ///   tooltip a few seconds in to add the detail its compact form left out (its Progressive
    ///   detail) - and a live part reading the window would say the whole block again each time. So
    ///   the first drawn words are held for as long as the mod is pointing where it pointed when it
    ///   read them, which is exactly the landing. The review buffer is unaffected: it reads the
    ///   window itself and always has whatever is drawn now.
    /// - ASKED EVERY FRAME, READ ALMOST NEVER. A live part is resolved on every frame the control is
    ///   focused, and reading the drawn tooltip means walking the window's feature table. The read
    ///   is therefore keyed on the drawn tooltip's identity, which <see cref="PointerFocus"/>
    ///   already tracks for the buffer's sake (the reference, plus the height that says "rebuilt"):
    ///   one read per drawing, not one per frame. <see cref="Looks"/> is the counter that proves it.
    ///
    /// One slot, because the player is on one control: everything else asking is answered by a
    /// reference compare against what the window is drawing, which is nothing of theirs.
    /// </summary>
    public static class LongTooltips
    {
        private static readonly string[] Nothing = new string[0];

        // What the window was drawing when this last looked, and whether that drawing has been read.
        private static AgeTooltip _drawn;
        private static float _height;
        private static bool _unread;

        // The tooltip whose first drawn words are being held, and the words.
        private static AgeTooltip _held;
        private static IList<string> _words;

        /// <summary>How many times the drawn tooltip has actually been READ - the memo's own proof,
        /// which no transcript or dump would show. It moves once per drawing, never per frame.
        /// </summary>
        public static int Looks;

        /// <summary>
        /// The late reader for a tooltip the game assembles on hover, or null when this player has
        /// not asked for those to be read - which is what leaves the section buffer-only.
        ///
        /// <paramref name="lines"/> is the section's OWN reader, so a screen that reads a dossier
        /// its own way announces the words it also reviews rather than a second reading of the same
        /// window.
        /// </summary>
        public static Func<IList<string>> Announced(AgeTooltip tooltip, Func<IList<string>> lines)
        {
            if (tooltip == null || lines == null || !LongTooltipSettings.Announced)
            {
                return null;
            }

            AgeTooltip it = tooltip;
            Func<IList<string>> read = lines;
            return () => Words(it, read);
        }

        private static IList<string> Words(AgeTooltip tooltip, Func<IList<string>> lines)
        {
            // What is held is held for THE LANDING - as long as the mod is still pointing where it
            // pointed when the words were read. Not for as long as the window happens to be drawing
            // them: the window hides and draws again while focus sits perfectly still (the mod
            // re-asks for a tooltip that was slow to appear, and the game rebuilds one it has more
            // detail for), and holding to the window's grip made each of those look like a new
            // landing and say the whole block again - four times over, measured 2026-09-03.
            if (!ReferenceEquals(PointerFocus.Wanted, _held))
            {
                _held = null;
                _words = null;
            }

            if (ReferenceEquals(_held, tooltip))
            {
                return _words;
            }

            AgeTooltip drawn = PointerFocus.Drawn;
            float height = PointerFocus.DrawnHeight;
            if (!ReferenceEquals(drawn, _drawn) || height != _height)
            {
                _drawn = drawn;
                _height = height;
                _unread = true;
            }

            if (!_unread || !ReferenceEquals(drawn, tooltip))
            {
                return Nothing;
            }

            _unread = false;
            Looks++;
            IList<string> read = lines();
            if (read == null || read.Count == 0)
            {
                return Nothing;
            }

            _held = tooltip;
            _words = read;
            return read;
        }

        /// <summary>Mod teardown: hold no tooltip and no words across a reload.</summary>
        public static void Forget()
        {
            _drawn = null;
            _height = 0f;
            _unread = false;
            _held = null;
            _words = null;
            Looks = 0;
        }
    }
}

using System;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Making the map draw a constellation's name while the player is standing on it, so that the
    /// game will draw its dossier.
    ///
    /// The name a constellation's tooltip hangs on is a label the map CULLS: the labels window asks
    /// the culling service which constellations are on screen and hides every label the answer does
    /// not name (<c>ConstellationLabelsWindow.MarkLabelsCulling</c>), and at the camera positions the
    /// game is actually played at the answer names none of them (es2-facts). A hidden label draws no
    /// tooltip however carefully the pointer is aimed at it, so the group node for a constellation
    /// declared a dossier the player could never hear.
    ///
    /// So the mod shows the one label it needs, for exactly as long as it needs it. The force is the
    /// window's own two steps - mark the label culled IN, then let it decide whether this empire may
    /// see it (<c>ShowOrHideIfVisibleByEmpire</c>, the exploration gate the tree's own grouping already
    /// mirrors) - which means a constellation the player has not explored still draws nothing.
    ///
    /// It is re-asserted every frame rather than done once, because the window re-marks the culling on
    /// every camera MOVE and would take the label away underneath a standing cursor. And it is only
    /// ever a force where the game had culled the label OUT: a player who has zoomed to the band where
    /// the map draws these names for itself is left alone, and the release then gives the label back to
    /// the game rather than hiding something the game wanted shown.
    ///
    /// The release is both halves: the label is put back the way it was found, and the window is
    /// marked dirty so its own next pass re-decides from the culling service - which is what covers the
    /// case of the game having changed its mind while the label was held.
    ///
    /// Static state in the reloadable assembly, so <see cref="Release"/> is called from mod teardown
    /// as well as from the node losing focus: a hot reload must not leave a label the game thinks it
    /// culled out drawn across the map.
    /// </summary>
    public static class ConstellationLabelHold
    {
        private static ConstellationLabel _held;

        /// <summary>Whether the label was the MOD's to show - false where the game had already culled
        /// it in, in which case nothing is forced and nothing is taken away on release.</summary>
        private static bool _forced;

        /// <summary>Draw this constellation's name for as long as it is asked for. Asking again for
        /// the label already held only re-asserts it.</summary>
        public static void Hold(ConstellationLabel label)
        {
            if (label == null)
            {
                Release();
                return;
            }

            if (!ReferenceEquals(label, _held))
            {
                Release();
                _held = label;
                _forced = !label.CulledIn;
            }

            Assert();
        }

        /// <summary>Per frame from the page that holds one, because the window re-marks its culling
        /// whenever the camera moves.</summary>
        public static void Tick()
        {
            if (_held != null)
            {
                Assert();
            }
        }

        /// <summary>Give the label back to the game.</summary>
        public static void Release()
        {
            ConstellationLabel label = _held;
            bool forced = _forced;
            _held = null;
            _forced = false;
            if (label == null)
            {
                return;
            }

            try
            {
                if (forced)
                {
                    label.CulledIn = false;
                    label.Hide(true);
                }

                ConstellationLabelsWindow window = Window();
                if (window != null)
                {
                    window.Dirty = true;
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: letting go of a constellation label threw: " + e);
            }
        }

        private static void Assert()
        {
            try
            {
                ConstellationLabelsWindow window = Window();
                if (window == null || _held.Constellation == null)
                {
                    return;
                }

                _held.CulledIn = true;
                if (!_held.Shown)
                {
                    _held.ShowOrHideIfVisibleByEmpire(window.LookingEmpire);
                    _held.Dirty = true;
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: showing a constellation label threw: " + e);
                _held = null;
                _forced = false;
            }
        }

        private static ConstellationLabelsWindow Window()
        {
            return Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<ConstellationLabelsWindow>(false)
                : null;
        }
    }
}

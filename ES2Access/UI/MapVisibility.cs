using System;

namespace ES2Access.UI
{
    /// <summary>
    /// Whether the map is showing a place at all - the one test everything that names a place on the
    /// galaxy asks before it says the name.
    ///
    /// The galaxy model answers questions the map refuses to: every node has a name and every link has
    /// two ends, discovered or not, so reading a name straight off the simulation hands the player
    /// something the picture is deliberately withholding. This is the picture's own question, asked the
    /// way the map's cursor targets ask it, and it lives on its own because a route can run through
    /// space nobody has been into (<see cref="FleetRoute"/>) just as a starlane can lead into it
    /// (<c>GalaxyHudScreen</c>).
    /// </summary>
    public static class MapVisibility
    {
        /// <summary>
        /// Whether this empire can see this place well enough for the map to name it.
        ///
        /// The threshold is HIGHER for a special node - the Academy, a quest site - than for an ordinary
        /// star system: 3 against 2 (<c>GalaxySpecialNodeCursorTarget.VisibleByCurrentEmpire</c> :22-27
        /// against <c>GalaxyStarSystemCursorTarget</c>'s :89-94, which the special one overrides). A
        /// <c>SpecialNode</c> IS a <c>StarSystemNode</c>, so one threshold for both would have named
        /// every special node one step of exploration too early.
        /// </summary>
        public static bool Perceived(GameNode node, Empire empire)
        {
            try
            {
                if (
                    node == null
                    || empire == null
                    || (int)node.Exploration[empire] < (node is SpecialNode ? 3 : 2)
                )
                {
                    return false;
                }

                EntityVisibility.Layer layer = node.Visibility[empire];
                return layer == EntityVisibility.Layer.Known || (int)layer >= 3;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

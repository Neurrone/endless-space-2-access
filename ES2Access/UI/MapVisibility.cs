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
        /// Whether the map is drawing this starlane at all.
        ///
        /// Not "has it been revealed": the map takes an INTENSITY from the link's own exploration state
        /// and paints the line with it (<c>GalaxyLink.Refresh</c> :247-252 →
        /// <c>GetIntensityFromState</c> :362-372), and that intensity is nought - an invisible line -
        /// for Localized and Identified as well as for Unrevealed. Only PartiallyRevealed and above
        /// light it up. The geometry exists either way (<c>GalaxyWarplink.Ignite</c> :12-19 builds the
        /// line between the two extremity positions the moment the link is created, whatever anyone has
        /// explored), so the line's EXISTENCE is not the question and never was.
        ///
        /// Wormholes go through the same <c>Refresh</c>, so they are asked the same thing here; whether
        /// the empire can see wormholes at all is a separate question its caller asks first.
        /// </summary>
        public static bool Drawn(Link link, Empire empire)
        {
            try
            {
                return link != null
                    && empire != null
                    && (int)link.Exploration[empire]
                        >= (int)EntityExploration.State.PartiallyRevealed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether this empire can see this place well enough for the map to name it - which is the
        /// label window's own gate, not an approximation of it: <c>StarSystemLabel</c> shows a label at
        /// exploration 2 with the node either remembered or in sight (<c>ShowOrHideIfVisibleByEmpire</c>
        /// :1514-1522) and writes <c>GameNode.LocalizedName</c> into it at the same threshold, or the
        /// literal "???" below it (<c>RefreshEmpireNameLabel</c> :1894-1921).
        ///
        /// The threshold is HIGHER for a special node - the Academy, a quest site - than for an ordinary
        /// star system: 3 against 2 (<c>GalaxySpecialNodeCursorTarget.VisibleByCurrentEmpire</c> :22-27
        /// against <c>GalaxyStarSystemCursorTarget</c>'s :89-94, which the special one overrides). A
        /// <c>SpecialNode</c> IS a <c>StarSystemNode</c>, so one threshold for both would have named
        /// every special node one step of exploration too early for the MOUSE. Note that the label
        /// window has no special-node branch of its own, so between exploration 2 and 3 the map draws a
        /// special node's name while this refuses it - deliberately unresolved, owner's call pending.
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

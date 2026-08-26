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
        /// Whether the map is drawing a star at this place at all - which is a lower bar than naming
        /// it, and the only bar a place has to clear to be somewhere the player can see.
        ///
        /// The game's own drawing gate, asked the way the galaxy's nodes ask it:
        /// <c>GalaxyNode.UpdateVisualAccordingToExploration</c> :491 turns the node's body and its
        /// collider on at exploration 1 and leaves them off below it. One above nothing is Localized -
        /// granted when a node's visibility layer is raised without anybody having seen the place (a
        /// curiosity's reveal-neighbours reward, a map traded away, a hack, a foreign fleet spotted
        /// parked at a node nobody has been to: <c>GameNode</c> :1285-1295) - and what the map draws
        /// there is a GENERIC star with an orbit ring: no name, no real star type, no tooltip and no
        /// selection (<c>GalaxyStarSystemCursorTarget</c> :89-94 refuses to highlight it).
        ///
        /// So a place that is Located and not <see cref="Perceived"/> is one the player can SEE and
        /// cannot read: it has a position on the map and nothing else the picture will give up. The
        /// exploration state is a ratchet, so this never goes back down.
        /// </summary>
        public static bool Located(GameNode node, Empire empire)
        {
            try
            {
                return node != null
                    && empire != null
                    && (int)node.Exploration[empire] >= (int)EntityExploration.State.Localized;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether this empire is being SHOWN a moving thing out in space - a probe, a missile.
        ///
        /// The one gate the game itself puts between the model and the mote it draws for one of these
        /// (<c>VisibleEntityLabel.ShowOrHideIfVisibleByEmpire</c>: <c>Visibility[empire] >= 3</c>,
        /// which is Visible). Its neighbour in that same window - whether the label survived the
        /// camera's culling group - is NOT part of this and must never be borrowed into it: culling
        /// is about where the camera is pointing, and a fact the player has been granted does not
        /// stop being granted because they zoomed out.
        /// </summary>
        public static bool Sighted(EntityVisibility visibility, Empire empire)
        {
            try
            {
                return visibility != null && empire != null && (int)visibility[empire] >= 3;
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
        /// One threshold, for a special node - a black hole, a solar nebula, an asteroid field - as
        /// much as for an ordinary star system, because NAMING and TARGETING are different questions and
        /// this is the naming one. (The Academy and quest sites are NOT special nodes: each is an
        /// ordinary <c>StarSystemNode</c> carrying the <c>WorldAcademy</c> or <c>QuestNodeTag</c> tag.)
        /// Neither of those two label methods has a special-node branch at all: the label draws a
        /// special node's name at exploration 2 like anything else's. The higher threshold that exists in the
        /// game - 3 in <c>GalaxySpecialNodeCursorTarget.VisibleByCurrentEmpire</c> :22-27, overriding
        /// <c>GalaxyStarSystemCursorTarget</c>'s :89-94 - governs whether the MOUSE can target the node,
        /// which is a separate rule this one must not borrow: taking it made the cursor refuse a name the
        /// map was drawing on screen.
        ///
        /// A place below this bar but at or above <see cref="Located"/> is one the map is DRAWING and
        /// not naming, which is a row of its own in the tree rather than nothing at all.
        /// </summary>
        public static bool Perceived(GameNode node, Empire empire)
        {
            try
            {
                if (node == null || empire == null || (int)node.Exploration[empire] < 2)
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

using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The influence a system throws over the map, and whose influence a system is standing in.
    ///
    /// The game models influence as one circle per COLONY: a radius the colony grows for itself, a
    /// strength field falling off inside it, and every node resolved to the single strongest source
    /// standing over it (<c>ColonizedStarSystemRepository.TryGetInfluence</c>). The map draws all of
    /// that as a coloured disk and writes not one word anywhere on it - not the radius, not which way
    /// it is going next turn, not whose colour the disk is - so both readings here are the mod's
    /// phrases around the game's own numbers.
    ///
    /// Three questions, because they are three different things: how far THIS colony reaches
    /// (<see cref="RadiusLines"/>, a review line, since it is a number to plan with rather than news),
    /// whose influence has WON this place (<see cref="UnderInfluence"/>, spoken, since a system under
    /// somebody else's influence cannot be colonized and can eventually change hands), and who else is
    /// reaching for it without holding it (<see cref="Contested"/>, spoken, because that is the
    /// contest in progress and the map draws it as overlapping colour).
    ///
    /// EVERYTHING here is gated on the player perceiving the node (<see cref="MapVisibility.Perceived"/>).
    /// The influence values are the SIMULATION's, global and player-blind: reading them ungated hands
    /// the player the position and reach of colonies they have never seen. The game's own disk asks
    /// the same question before it draws (<c>GalaxyStarSystem.UpdateInfluenceRange</c> :1926 hides it
    /// on <c>Node.Visibility.IsInvisible</c>).
    ///
    /// Cost: <c>TryGetInfluenceRadius</c> is a dictionary lookup on the node's own position over the
    /// colonies standing THERE (<c>ColonizedStarSystemRepository</c> :132-161), not the galaxy-wide
    /// walk its <c>TryGetInfluence</c> sibling is, and the influencer is a plain property the influence
    /// pass has already resolved (<c>GameNode.SystemWhichInfluences</c> :212). Both are cheap enough
    /// for the focused node's per-frame readout; neither is asked for a system nobody is standing on.
    /// </summary>
    public static class SystemInfluence
    {
        /// <summary>
        /// How far this system's own influence reaches, and which way it is going next turn.
        ///
        /// Only for a system that projects at all: the service answers with the strongest colony
        /// standing at the node and its radius, and an OUTPOST projects nothing (its descriptor forces
        /// <c>SystemInfluenceRadius</c> to zero), so a place still growing into a colony has no line
        /// here rather than a line saying nought.
        ///
        /// The sentence itself, and the rule about which way the radius is going, are
        /// <see cref="InfluenceText.Radius"/>'s.
        /// </summary>
        public static IList<string> RadiusLines(GameNode node, Empire empire)
        {
            try
            {
                IInfluenceService influence = Services.GetService<IInfluenceService>();
                if (node == null || influence == null || !MapVisibility.Perceived(node, empire))
                {
                    return null;
                }

                ColonizedStarSystem source;
                float radius;
                float next;
                if (
                    !influence.TryGetInfluenceRadius(
                        node.NodePosition,
                        out source,
                        out radius,
                        out next
                    )
                    || radius <= 0f
                )
                {
                    return null;
                }

                return new List<string> { InfluenceText.Radius(radius, next) };
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's influence radius threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Whose influence is standing over this place, where that is not the empire holding it.
        ///
        /// Nothing at all for the ordinary case - a colony inside its own empire's circle - because
        /// that is what a colony being where it is MEANS. What is worth a word is the mismatch: a
        /// system of yours standing in somebody else's influence (which is how a system changes hands
        /// without a shot), and an uncolonized node inside anybody's influence (which is what blocks a
        /// colony ship from settling there at all).
        ///
        /// Whose influence it is, is read as the map PAINTS it: the disk takes its colour from
        /// <c>systemWhichInfluences.Empire</c> (<c>GalaxyStarSystem.UpdateInfluenceRange</c> :1932),
        /// so that same empire is the one named and the one compared against the owners, rather than
        /// <c>InfluenceOwner</c>, which looks through an integrated minor faction to the empire that
        /// absorbed it and would name somebody whose colour is nowhere on the map.
        ///
        /// The owners are every colony standing at the node that this empire can see - an outpost
        /// included, because holding a place is holding it whether or not it has grown up yet, and a
        /// system shared with a minor faction has two of them.
        /// </summary>
        public static string UnderInfluence(GameNode node, Empire empire)
        {
            try
            {
                if (node == null || !MapVisibility.Perceived(node, empire))
                {
                    return null;
                }

                ColonizedStarSystem source = node.SystemWhichInfluences;
                Empire influencer = source == null ? null : source.Empire;
                if (influencer == null || Held(OwnersAt(node, empire), influencer))
                {
                    return null;
                }

                return ReferenceEquals(influencer, Gui.PlayerEmpire)
                    ? ModStrings.Get(ModStrings.GalaxySystemInfluencedByYou)
                    : ModStrings.Format(
                        ModStrings.GalaxySystemInfluencedBy,
                        AgeText.Clean(influencer.LocalizedName)
                    );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading whose influence a system is under threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Who else's influence REACHES this place without having won it.
        ///
        /// The winner is one empire, but a node can stand inside several circles at once, and the
        /// difference between "safely mine" and "about to be somebody else's" is exactly that overlap -
        /// which the map draws as colour on colour and never as a word. The empires already implied by
        /// the row are left out: the one that holds the node (its own reach is what being there means)
        /// and the one that has won it (the line above has just named them).
        ///
        /// Reach is the game's own test, the one its influence pass makes for every node
        /// (<c>ColonizedStarSystemRepository.TryGetInfluence</c> :111-127): the distance from here to
        /// the colony, against that colony's radius, boundary counting as inside. A colony is asked
        /// only where it is the source the game resolves AT ITS OWN NODE - two empires sharing a system
        /// have one circle between them, the stronger one, because that is the only one the game's own
        /// resolution can ever hand to anybody.
        /// </summary>
        public static string Contested(GameNode node, Empire empire)
        {
            try
            {
                IInfluenceService influence = Services.GetService<IInfluenceService>();
                IColonizedStarSystemRepositoryService colonies =
                    Services.GetService<IColonizedStarSystemRepositoryService>();
                if (
                    node == null
                    || influence == null
                    || colonies == null
                    || !MapVisibility.Perceived(node, empire)
                )
                {
                    return null;
                }

                ColonizedStarSystem won = node.SystemWhichInfluences;
                Empire winner = won == null ? null : won.Empire;
                List<Empire> owners = OwnersAt(node, empire);
                List<Empire> reaching = null;
                foreach (ColonizedStarSystem colony in colonies.GetValues())
                {
                    Empire behind = colony.Destroyed ? null : colony.Empire;
                    if (
                        behind == null
                        || ReferenceEquals(behind, winner)
                        || Held(owners, behind)
                        || (reaching != null && Held(reaching, behind))
                    )
                    {
                        continue;
                    }

                    ColonizedStarSystem source;
                    float radius;
                    if (
                        !influence.TryGetInfluenceRadius(colony.NodePosition, out source, out radius)
                        || !ReferenceEquals(source, colony)
                        || !Reaches(node, colony, radius)
                    )
                    {
                        continue;
                    }

                    if (reaching == null)
                    {
                        reaching = new List<Empire>();
                    }

                    reaching.Add(behind);
                }

                return Contesters(reaching);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading who is contesting a system's influence threw: " + e);
                return null;
            }
        }

        /// <summary>Whether this colony's circle covers that node - the game's own comparison, squared
        /// on both sides so nothing takes a square root, and the boundary inside as the game has
        /// it.</summary>
        private static bool Reaches(GameNode node, ColonizedStarSystem colony, float radius)
        {
            GameNode from = colony.Node;
            if (from == null || radius <= 0f)
            {
                return false;
            }

            return (node.GalaxyPosition - from.GalaxyPosition).SquareMagnitude <= radius * radius;
        }

        /// <summary>
        /// The contesters as one line, in the empire order the game itself lists empires in, so the
        /// same overlap is read the same way twice running.
        ///
        /// The player alone gets a sentence of their own, because "your empire" is what the player is
        /// called everywhere else in the mod. In a list they are named like anybody else: a list is
        /// read as a list of empires, and one item wearing a different kind of name is the thing that
        /// makes a list hard to follow.
        /// </summary>
        private static string Contesters(List<Empire> reaching)
        {
            if (reaching == null || reaching.Count == 0)
            {
                return null;
            }

            reaching.Sort(ByEmpireOrder);
            if (reaching.Count == 1 && ReferenceEquals(reaching[0], Gui.PlayerEmpire))
            {
                return ModStrings.Get(ModStrings.GalaxySystemInfluenceContestedYou);
            }

            List<string> names = new List<string>(reaching.Count);
            for (int i = 0; i < reaching.Count; i++)
            {
                names.Add(Named(reaching[i]));
            }

            return InfluenceText.Contested(names);
        }

        private static readonly Comparison<Empire> ByEmpireOrder = CompareEmpireOrder;

        private static int CompareEmpireOrder(Empire left, Empire right)
        {
            return left.Index.CompareTo(right.Index);
        }

        private static string Named(Empire empire)
        {
            return AgeText.Clean(empire.LocalizedName);
        }

        /// <summary>The empires holding this place. Asked through the same visibility gate the rest of
        /// the map's ownership reading uses (<c>Visibility[empire] >= 1</c>), so a colony the player has
        /// never seen can neither silence a line nor be named by one - and an OUTPOST counts, because
        /// holding a place is holding it whether or not it has grown up yet.</summary>
        private static List<Empire> OwnersAt(GameNode node, Empire empire)
        {
            List<Empire> owners = new List<Empire>();
            IColonizedStarSystemRepositoryService colonies =
                Services.GetService<IColonizedStarSystemRepositoryService>();
            if (colonies == null)
            {
                return owners;
            }

            foreach (ColonizedStarSystem colony in colonies.GetValues(node.NodePosition))
            {
                if (
                    colony.Empire != null
                    && (int)colony.Visibility[empire] >= 1
                    && colony.State != StarSystemState.Ghost
                    && !Held(owners, colony.Empire)
                )
                {
                    owners.Add(colony.Empire);
                }
            }

            return owners;
        }

        private static bool Held(List<Empire> empires, Empire empire)
        {
            for (int i = 0; i < empires.Count; i++)
            {
                if (ReferenceEquals(empires[i], empire))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

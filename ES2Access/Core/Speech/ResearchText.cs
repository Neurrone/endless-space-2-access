using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// The phrases the research screen composes for itself: what one of the arcs drawn between two
    /// technologies means, and what a stage of the wheel amounts to.
    ///
    /// Here rather than on the screen because these are the parts of that screen a translator has to
    /// be able to move words around in - an arc says something DIFFERENT depending on which end of it
    /// the player is standing on, and which end that is cannot survive being glued together from
    /// fragments. Engine-free, so the choice of phrase is testable without the game.
    /// </summary>
    public static class ResearchText
    {
        /// <summary>What kind of arc the game drew between the two technologies - its own three
        /// categories, in the game's own words on the key panel.</summary>
        public enum LinkKind
        {
            CostReduction,
            Exclusion,
            Dependency,
        }

        /// <summary>
        /// One arc, said from the focused technology's end of it. <paramref name="fromHere"/> is
        /// whether the focused technology is the arc's SOURCE, which is the whole of what makes
        /// "reduces the cost of Applied Casimir Effect" and "cost reduced by Applied Casimir Effect"
        /// two different sentences rather than one sentence with the names swapped.
        ///
        /// An exclusion reads the same from both ends, because it is the same fact from both ends.
        /// </summary>
        public static string Link(LinkKind kind, bool fromHere, string partner)
        {
            if (string.IsNullOrEmpty(partner))
            {
                return null;
            }

            if (kind == LinkKind.Exclusion)
            {
                return ModStrings.Format(ModStrings.ResearchLinkExclusive, partner);
            }

            if (kind == LinkKind.Dependency)
            {
                return ModStrings.Format(
                    fromHere ? ModStrings.ResearchLinkUnlocks : ModStrings.ResearchLinkUnlockedBy,
                    partner
                );
            }

            return ModStrings.Format(
                fromHere ? ModStrings.ResearchLinkReduces : ModStrings.ResearchLinkReducedBy,
                partner
            );
        }

        /// <summary>
        /// Every arc drawn from one technology, as the one thing a dot says about the company it
        /// keeps: each already a whole sentence from <see cref="Link"/>, read one after another in the
        /// order the wheel drew them.
        ///
        /// A list rather than a paragraph, because that is what it is - the separator is the corpus's
        /// own, so a language that lists things differently lists these differently too. Nothing to say
        /// answers nothing at all, which is how a dot with no arcs stays as short as it looks.
        /// </summary>
        public static string Relationships(IList<string> links)
        {
            MessageBuilder message = new MessageBuilder();
            for (int i = 0; links != null && i < links.Count; i++)
            {
                message.ListItem(links[i]);
            }

            return message.Build();
        }

        /// <summary>
        /// What a technology is going to take: what the game says it costs, and where it sits in the
        /// queue - the two things wherever a technology is read, on the wheel or in the list of the
        /// ones the game is recommending.
        ///
        /// No turn count, because the wheel draws none. The dot has a turns label in its prefab and
        /// the game never wires it up - measured null on all 385 items - so the only surfaces that
        /// ever show a technology's remaining turns are the research queue in the side panel and the
        /// empire banner's research line, each of which reads its own. A number computed for a
        /// technology nobody is researching is the mod saying something the screen does not.
        ///
        /// A negative <paramref name="queuePosition"/> is "the game does not say", and an empty
        /// <paramref name="costs"/> is a technology already researched, so a caller can hand over what
        /// it has and get back only what there is to say.
        /// </summary>
        public static string Progress(string costs, int queuePosition)
        {
            MessageBuilder message = new MessageBuilder();
            message.ListItem(costs);
            if (queuePosition >= 0)
            {
                message.ListItem(
                    ModStrings.Format(ModStrings.ResearchQueuePosition, queuePosition + 1)
                );
            }

            return message.Build();
        }

        /// <summary>How far a competitive deed has got, in the game's own four words.</summary>
        public enum DeedProgress
        {
            NotStarted,
            InProgress,
            Completed,
            Failed,
        }

        /// <summary>
        /// Which of the four states the game paints a deed marker in - and therefore which of its own
        /// four words ("Available", "Completed", "Failed", "Locked") the marker stands for.
        ///
        /// The game draws the deed in one of the technology-state colours and gives each of those
        /// states a word on the key panel, so the marker's colour IS its state word; this is the same
        /// decision the marker's own refresh makes, in the same order. A deed nobody has unlocked the
        /// stage for is drawn locked whatever the quest underneath is doing, which is why
        /// <paramref name="visible"/> is asked first.
        /// </summary>
        public static string DeedStateName(bool visible, bool available, DeedProgress progress)
        {
            if (visible)
            {
                if (progress == DeedProgress.InProgress && available)
                {
                    return "Available";
                }

                if (progress == DeedProgress.Completed)
                {
                    return "Researched";
                }

                if (progress == DeedProgress.Failed)
                {
                    return "Disabled";
                }
            }

            return "NotAvailable";
        }

        /// <summary>The empire that got to a deed first. The game says this by drawing their logo
        /// beside the marker, which is nothing to a player who cannot see it.</summary>
        public static string DeedWinner(string empire)
        {
            return string.IsNullOrEmpty(empire)
                ? null
                : ModStrings.Format(ModStrings.ResearchDeedWinner, empire);
        }
    }
}

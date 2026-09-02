using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;

namespace ES2Access.UI
{
    /// <summary>
    /// What a planet CARD says beyond its own readout - the tables of small items the game fills a
    /// card's middle with: what kind of world it is, what has been found on it, what it is sitting on.
    ///
    /// Two pages draw a card per planet out of two prefabs - the star system page's planet labels and
    /// the empire page's planet cards - and both fill the same shaped tables the same way
    /// (<c>ReserveChildren</c> + <c>RefreshChildrenIList</c>). They were read two different ways, and
    /// the differences were both defects on the shallower side: a pooled table retires a surplus item
    /// by fading it while leaving it <c>Visible</c>, so a whole-subtree text read announces the
    /// PREVIOUS planet's findings, and a deposit item writes its resource's NAME nowhere on itself, so
    /// a text read of one is a bare number with nothing saying of what.
    ///
    /// So both cards read here: the child that PAINTS is a line, and a deposit says its resource and
    /// its amount.
    /// </summary>
    public static class PlanetCardLines
    {
        /// <summary>
        /// One line per item a card's table is drawing, or - for a group with no children of its own,
        /// such as the type and size groups - the group's own words.
        ///
        /// What each item SAYS rather than the text on it (<see cref="AgeWidgets.ItemText"/>): a
        /// findings table is a row of bare icons, and reading it as text reads nothing at all.
        ///
        /// The entry gate is the visibility chain, because a table that is itself fading IN still has
        /// content to read; each CHILD is asked the engine's own drawing test instead, which is the
        /// question a pooled child raises - the same rule and the same reason as
        /// <c>SidePanels.Collect</c>.
        ///
        /// <paramref name="skip"/> leaves out an item the card offers as a control of its own - the
        /// curiosities the game mixes into the findings table are buttons, not lines.
        /// </summary>
        public static void Add(
            List<string> lines,
            AgeTransform table,
            Func<AgeTransform, bool> skip = null
        )
        {
            // Content: which lines the card is read with. Lines, not nodes - nothing here is declared,
            // so no gate has asked anything about this table.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> children = table.Children;
            if (children == null || children.Count == 0)
            {
                AddLine(lines, AgeWidgets.ItemText(table));
                return;
            }

            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child == null || (skip != null && skip(child)))
                {
                    continue;
                }

                AddLine(lines, ItemLine(child));
            }
        }

        /// <summary>
        /// What one item of a card's table says.
        ///
        /// A DEPOSIT is the one item that cannot say itself: <c>ResourceDepositItem.Refresh</c>
        /// (:28-42) fills <c>AmountLabel</c> and leaves the prefab's <c>TitleLabel</c> to the prefabs
        /// that have one, keeping the resource's name on the wrapper it hangs on its own tooltip. So
        /// the name is taken from the drawn title where there is one and from that wrapper where there
        /// is not, and bound to the figure beside it.
        /// </summary>
        private static string ItemLine(AgeTransform child)
        {
            ResourceDepositItem deposit = Deposit(child);
            if (deposit == null)
            {
                return AgeWidgets.ItemText(child);
            }

            string name = AgeWidgets.DrawnLabel(deposit.TitleLabel);
            if (string.IsNullOrEmpty(name))
            {
                name = AgeWidgets.TooltipTitle(deposit.Tooltip);
            }

            string amount = AgeWidgets.DrawnLabel(deposit.AmountLabel);
            return string.IsNullOrEmpty(name)
                ? amount
                : ModStrings.Format(ModStrings.CaptionedColon, name, amount);
        }

        private static ResourceDepositItem Deposit(AgeTransform child)
        {
            try
            {
                return child.GetComponent<ResourceDepositItem>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A line the card has not already said. The same words twice on one card is the game
        /// drawing one fact in two of these tables, not two facts.</summary>
        public static void AddLine(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line) && !lines.Contains(line))
            {
                lines.Add(line);
            }
        }
    }
}

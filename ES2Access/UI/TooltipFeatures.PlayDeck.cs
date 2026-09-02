using System;
using System.Collections.Generic;

namespace ES2Access.UI
{
    public static partial class TooltipFeatures
    {
        // ---- the tactics deck ----

        /// <summary>
        /// The battle tactics a deck holds, each card's three FLOTILLA rows given the flotilla they
        /// belong to.
        ///
        /// The deck's feature spawns one card per unlocked tactic (<c>PanelFeaturePlayDeck.Bind</c>
        /// reserves its children from one item prefab), and each card draws its three optimal ranges
        /// as a diagram plus a bare word - "Short", "Medium", "Long" - one under the other with
        /// nothing on the card saying which flotilla each row is. The words for both halves are the
        /// game's own and are used everywhere else it writes this pair:
        /// <c>%FlotillaNameTitle</c> ("Flotilla {0}") for the row, and
        /// <c>%AdvancedPlayFlotillaOptimalRangeTitle</c> ("{0} Range") for the word - the second being
        /// exactly what the game writes into the range diagram's own hover tooltip
        /// (<c>BattlePlayCardRangeIndicator.Refresh</c> :75), which is the only place a mouse player
        /// ever sees the two joined.
        ///
        /// A substitution like every other typed reader's: the card's title, its family icons and its
        /// effects paragraph go on reading through the ordinary banding.
        /// </summary>
        private static Dictionary<AgeTransform, Naming> PlayDeckNames(PanelFeaturePlayDeck deck)
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            AgeTransform table = deck == null ? null : deck.PlayItemsTable;
            if (table == null)
            {
                return named;
            }

            List<AgeTransform> cards = table.Children;
            for (int i = 0; i < cards.Count; i++)
            {
                AgeTransform card = cards[i];
                PanelFeaturePlayItem item =
                    card == null ? null : card.GetComponent<PanelFeaturePlayItem>();
                AgePrimitiveLabel[] ranges = item == null ? null : item.Ranges;
                for (int flotilla = 0; ranges != null && flotilla < ranges.Length; flotilla++)
                {
                    AgePrimitiveLabel label = ranges[flotilla];
                    string drawn = label == null ? null : AgeText.Label(label);
                    if (string.IsNullOrEmpty(drawn))
                    {
                        continue;
                    }

                    // Joined with a COLON, not with the space every other captioned figure takes
                    // (owner ruling 2026-08-23): both halves are whole phrases the game wrote for
                    // itself - "Flotilla 1" and "Short Range" - and run together with a space they
                    // read as one name for one thing rather than as a row and its value. The
                    // connective is the translator's (<see cref="ModStrings.CaptionedColon"/>).
                    NameText(
                        named,
                        label,
                        FlotillaRange(flotilla, Localized(FlotillaRangeKey, drawn))
                    );
                }
            }

            return named;
        }

        /// <summary>
        /// Which flotilla an optimal-range row belongs to, joined to what that row says.
        ///
        /// The game draws a battle plan's three ranges one under the other with nothing beside them
        /// saying which flotilla each is - on the deck's cards (<see cref="PlayDeckNames"/>) and on
        /// the selected-plan card of the battle-setup popup, whose three range diagrams each carry
        /// only "Short Range" and are told apart by their POSITION alone. Both read the same way, out
        /// of the same two game strings, which is why the joining lives here rather than at either
        /// call site. <paramref name="flotilla"/> is the row's index, counted from zero; the game
        /// numbers them from one.
        /// </summary>
        public static string FlotillaRange(int flotilla, string range)
        {
            return Joined(Localized(FlotillaNameKey, flotilla + 1), range);
        }

        private const string FlotillaNameKey = "%FlotillaNameTitle";
        private const string FlotillaRangeKey = "%AdvancedPlayFlotillaOptimalRangeTitle";

        /// <summary>One of the game's own templates, filled in. The raw key back where it does not
        /// resolve, which the caller then reads as itself rather than saying nothing.</summary>
        private static string Localized(string key, object argument)
        {
            try
            {
                return AgeText.Clean(Gui.Localize(key, argument));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

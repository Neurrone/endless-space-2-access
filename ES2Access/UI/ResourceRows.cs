using System;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Screens;

namespace ES2Access.UI
{
    /// <summary>
    /// What a resource item SAYS about its two numbers - the holding and what the next turn does to
    /// it - wherever the game draws its <c>ResourceItem</c> prefab: the empire banner's strip, a
    /// colony panel's banner, the economy grid, the juggernaut's strategic strip.
    ///
    /// One reader, because there is one drawn row. Four screens each grew their own and the three
    /// halves of the reading drifted apart in every direction: where the figures came from (the
    /// cache on one page, the drawn labels on another, a mix on the third), which of them was said
    /// at all, and how many decimals each got. A player comparing the same resource on two pages
    /// heard two different numbers for it.
    /// </summary>
    public static class ResourceRows
    {
        /// <summary>
        /// The item's figures as one phrase: the holding and the rate, or the rate alone, or nothing.
        ///
        /// Both figures are the GAME'S, read from the resource's own cache - never from the labels,
        /// which are animated towards their target (mid-slide text is a number the game never
        /// settled on) and, for the rate, hidden on most hosts: the empire strip and the economy
        /// grid draw only the holding and put the income in the tooltip. The income is said anyway,
        /// from the cache, because a player wants it beside the holding (owner ruling 2026-09-03);
        /// nothing here reads text the game is not drawing. The holding is said only where the item
        /// draws one - a colony banner's resources have no holding of their own, only a rate.
        ///
        /// Decimals are the item's OWN rule (<c>ResourceItem.Refresh</c> :114,124,138), asked of each
        /// figure separately as the game asks it: a tenth for a small holding of a luxury or a
        /// strategic, and only where the host bound the item to show one
        /// (<see cref="ShowsSmallValueDecimal"/>). The rule this replaced dropped both conditions, so
        /// an ordinary stock under ten was spoken as "7.5" where the screen drew "7".
        /// </summary>
        public static string Figures(ResourceItem item)
        {
            try
            {
                GuiLocatedResource resource = item == null ? null : item.GuiLocatedResource;
                if (resource == null)
                {
                    return null;
                }

                bool held = AgeWidgets.DrawnLabel(item.StockLabel) != null;
                bool tenths = ShowsSmallValueDecimal(item) && (resource.IsLuxury || resource.IsStrategic);
                float stock = resource.GetStockValueFromCache();
                float net = resource.GetNetValueFromCache();
                if (held)
                {
                    return GlobalHud.StockAndNet(
                        stock,
                        net,
                        Decimals(stock, tenths),
                        Decimals(net, tenths)
                    );
                }

                return ModStrings.Format(
                    ModStrings.SystemNetPerTurn,
                    GlobalHud.Amount(net, true, Decimals(net, tenths))
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One decimal for a small figure where the item is showing them, none otherwise -
        /// the game's own <c>value &lt; 10f</c> step.</summary>
        private static int Decimals(float value, bool tenths)
        {
            return tenths && value < 10f ? 1 : 0;
        }

        /// <summary>Whether this item was bound to write tenths at all - the host's own choice, made
        /// at <c>ResourceItem.Bind</c> (:50) and kept in a private field, so a strip that asked for
        /// whole numbers is not spoken in tenths. True where the field cannot be read: it is the
        /// game's own default for the parameter.</summary>
        private static bool ShowsSmallValueDecimal(ResourceItem item)
        {
            FieldInfo field =
                _showSmallValueDecimal
                ?? (_showSmallValueDecimal = GameHandlers.Field(
                    typeof(ResourceItem),
                    "showSmallValueDecimal"
                ));
            if (field == null)
            {
                return true;
            }

            object value = field.GetValue(item);
            return !(value is bool) || (bool)value;
        }

        private static FieldInfo _showSmallValueDecimal;
    }
}

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
        /// The item's figures as one phrase: both, the holding alone, the rate alone, or nothing.
        ///
        /// The LABELS decide which figures are said and the CACHE decides what they say. A label is
        /// animated towards its target, so its own text mid-slide is a number the game never settled
        /// on, while a label the panel is not drawing keeps whatever it was last bound with - so
        /// reading it would invent a figure that is nowhere on the screen. Every host of this prefab
        /// draws some subset: the colony banner has resources that draw the rate alone, and the
        /// juggernaut strip keeps a hidden "+0" it never shows.
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
                bool rate = AgeWidgets.DrawnLabel(item.NetLabel) != null;
                if (!held && !rate)
                {
                    return null;
                }

                bool tenths = ShowsSmallValueDecimal(item) && (resource.IsLuxury || resource.IsStrategic);
                float stock = resource.GetStockValueFromCache();
                float net = resource.GetNetValueFromCache();
                if (held && rate)
                {
                    return GlobalHud.StockAndNet(
                        stock,
                        net,
                        Decimals(stock, tenths),
                        Decimals(net, tenths)
                    );
                }

                return held
                    ? GlobalHud.Amount(stock, false, Decimals(stock, tenths))
                    : ModStrings.Format(
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

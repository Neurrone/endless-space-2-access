using System;

namespace ES2Access.UI
{
    /// <summary>
    /// What a buy-out button costs, read off the button itself.
    /// </summary>
    public static class Buyouts
    {
        /// <summary>
        /// The price the button writes on itself (<c>BuyoutButton.CostLabel</c> - the queue line's
        /// buy-outs, the research banner's), and only while the button is on offer: a refused one
        /// carries a marker there rather than a number ("x", "-") and its tooltip already names
        /// the amount that cannot be afforded.
        /// </summary>
        public static string Cost(BuyoutButton buyout)
        {
            try
            {
                return AgeWidgets.Offered(buyout.AgeTransform) && buyout.CostLabel != null
                    ? AgeText.Label(buyout.CostLabel)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

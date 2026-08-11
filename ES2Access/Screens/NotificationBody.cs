using ES2Access.Core.UI.Graph;

namespace ES2Access.Screens
{
    /// <summary>
    /// What a notification popup is handed when it writes its own body (see the Variant registry in
    /// <see cref="NotificationScreen"/>).
    ///
    /// The contract is narrow on purpose. The popup's body owns everything between the two strips: every
    /// control it added and every word it drew. What it does NOT own is the skeleton every notification
    /// shares - browsing to the next one, dismissing, putting aside, the pop-up-again box - which the
    /// screen declares in its strips exactly as it always does, above and below whatever the body wrote.
    /// Nothing is shared halfway: the generic drawn-body reading is skipped entirely for such a popup,
    /// because it would declare the same buttons a second time under different ids.
    /// </summary>
    internal sealed class NotificationBody
    {
        public GraphBuilder Builder;

        public NotificationWindow Window;

        /// <summary>The row the popup's own WORDS are declared as, where it filled its description in -
        /// what focus lands on, and what the first row of a table should hang below
        /// (<c>GraphSheet.Follows</c>). Null where the popup says nothing and its content is all it has,
        /// in which case the body names its own starting place.</summary>
        public ControlId Lead;
    }
}

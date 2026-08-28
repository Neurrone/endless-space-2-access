using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using Control = ES2Access.Screens.NotificationScreen.Control;

namespace ES2Access.Screens
{
    /// <summary>
    /// The technologies the research popup suggests when the queue has run dry - one card per branch of
    /// the technology tree, and picking one is how the player starts the next research without leaving
    /// the popup.
    ///
    /// TWO popups draw this panel and the player cannot tell them apart by what it offers: the one that
    /// reports a technology finished and finds nothing queued behind it
    /// (<c>TechnologyUnlockedNotificationWindow</c>), and the one the game raises when the queue is empty
    /// at the start of a turn (<c>TechnologyNeededNotificationWindow</c>). Neither declares the panel on a
    /// shared base, so which window is asked is the only thing that differs here - the reading, the
    /// naming and the pick-up are one and the same.
    ///
    /// The game draws a card as a picture with its words scattered AROUND it: the technology's name
    /// above the disk, the branch it belongs to below it, the cost in turns below that, and the things
    /// it would unlock as icons around the rim. Nothing is written on the click target itself, so the
    /// shared reading of a popup - which names a control from the labels it holds - finds no name for
    /// the card and reads its four labels as loose text belonging to nobody. That is what this replaces:
    /// the popup's own layout says which label is the name, which is the branch and which is the price,
    /// so the card is handed to the screen already named.
    ///
    /// One card per branch is the game's own guarantee (<c>DepartmentOfScience.ComputeSuggestedTechnologies</c>
    /// keys its answer by quadrant), so the branch beside each technology is what tells the four cards
    /// apart: choosing between them is choosing a direction.
    ///
    /// Pressing one is the game's own click and it is not a selection - <c>SuggestedTechnologiesPanel.OnToggleItem</c>
    /// posts an <c>OrderQueueResearch</c> there and then - so the card is a BUTTON, and the tick the
    /// toggle leaves behind is animation rather than a state worth telling anyone about.
    /// </summary>
    internal static class ResearchSuggestions
    {
        /// <summary>The cards the research popup is offering, in the order it laid them out.</summary>
        internal static IList<Control> Cards(NotificationWindow window)
        {
            List<Control> cards = new List<Control>();
            SuggestedTechnologiesPanel panel = Panel(window);
            AgeTransform table = panel == null ? null : panel.SuggestedTechnologiesTable;
            if (table == null || !Shown(table))
            {
                return cards;
            }

            List<AgeTransform> children = table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                TechnologyItem2 item =
                    widget == null || !Shown(widget) ? null : widget.GetComponent<TechnologyItem2>();
                if (item == null || item.Toggle == null || item.GuiTechnology == null)
                {
                    continue;
                }

                cards.Add(
                    new Control
                    {
                        Key = "suggestion/" + i,
                        Widget = widget,
                        Toggle = item.Toggle,
                        Name = Name(item),
                        Acts = true,
                        Details = Details(item),

                        // The technology's own dossier, named rather than looked for: it is the one
                        // the card carries in the buffer and so the one the pointer has to draw, and
                        // saying which tooltip that is here is what makes the drawn one and the
                        // declared one the same object rather than two answers that happen to agree.
                        Tip = item.Tooltip,
                    }
                );
            }

            return cards;
        }

        /// <summary>The suggestions panel this popup drew, whichever of the two it is. The field is
        /// public on both windows and declared on neither's base, so this is the whole of the
        /// difference between them.</summary>
        private static SuggestedTechnologiesPanel Panel(NotificationWindow window)
        {
            TechnologyUnlockedNotificationWindow completed =
                window as TechnologyUnlockedNotificationWindow;
            if (completed != null)
            {
                return completed.SuggestedTechnologiesPanel;
            }

            TechnologyNeededNotificationWindow needed =
                window as TechnologyNeededNotificationWindow;
            return needed == null ? null : needed.SuggestedTechnologiesPanel;
        }

        /// <summary>
        /// What the card says, top to bottom as the game drew it: the technology, the branch it comes
        /// from, and what it would cost - the three things the game wrote on it, read in one line
        /// because they are drawn in three places around a picture and a listener needs them together.
        ///
        /// Every word is the game's own and nothing joins them but the list separator every readout
        /// uses. The cost in particular is the DRAWN text rather than a recomputed number: the game
        /// writes it as a figure followed by a turn symbol, and the shared icon treatment is what turns
        /// that symbol into the word beside it - which is also the only thing that says "turn" here, so
        /// a card whose cost the game draws as its own "unlimited" reads exactly that.
        /// </summary>
        private static string Name(TechnologyItem2 item)
        {
            return new MessageBuilder()
                .ListItem(AgeText.Label(item.BottomSuggestionTitle))
                .ListItem(AgeText.Label(item.TitleLabel))
                .ListItem(AgeText.Label(item.TurnsLabel))
                .Build();
        }

        /// <summary>
        /// What the card carries in the review buffer, in the order the game drew it.
        ///
        /// The branch's description and the affinity note are sentences the game wrote out, and they are
        /// reviewed rather than spoken: the branch description is the same paragraph on every card in
        /// the row - it explains the branch, which the card's own name already gives - and hearing it
        /// four times while stepping across is the noise the buffer exists to keep out of the way.
        ///
        /// The technology's own dossier is the one the player came for, and it is the tooltip the
        /// pointer is aimed at, so it is the one that gets DRAWN and therefore the one with words to
        /// read. The unlock icons round the rim each carry a dossier of their own; they are declared
        /// because they are on the screen, and only one tooltip is drawn at a time, so what they hold is
        /// what the technology's own dossier already says about them.
        ///
        /// A group the game is not drawing is not declared at all - the prefab carries an affinity note
        /// on every card whether or not the technology has an affinity, and reading one off a hidden
        /// group would state something about the card that is not on the screen.
        /// </summary>
        private static IList<NodeSection> Details(TechnologyItem2 item)
        {
            List<NodeSection> sections = new List<NodeSection>(4);
            Reviewed(sections, Shown(item.BottomSuggestionGroup) ? item.BottomSuggestionTooltip : null);
            Add(sections, item.Tooltip);
            Reviewed(sections, Shown(item.AffinityGroup) ? item.AffinityTooltip : null);

            AgeTransform unlocks = item.TechnologyUnlocksContainer;
            List<AgeTransform> children = unlocks == null || !Shown(unlocks)
                ? null
                : unlocks.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (Shown(children[i]))
                {
                    Add(sections, AgeWidgets.Raw(children[i]));
                }
            }

            return sections.Count == 0 ? null : sections;
        }

        private static void Add(List<NodeSection> sections, AgeTooltip tooltip)
        {
            NodeSection section = GraphNodes.TooltipSection(tooltip);
            if (section != null)
            {
                sections.Add(section);
            }
        }

        /// <summary>One of the card.s OTHER tooltips - reviewable, never the one the card announces,
        /// which is the one the card points at.</summary>
        private static void Reviewed(List<NodeSection> sections, AgeTooltip tooltip)
        {
            NodeSection section = GraphNodes.ReviewedTooltipSection(tooltip);
            if (section != null)
            {
                sections.Add(section);
            }
        }

        private static bool Shown(AgeTransform widget)
        {
            try
            {
                return widget != null && widget.Visible;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

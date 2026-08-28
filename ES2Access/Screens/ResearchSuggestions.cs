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
                        Dossiers = Dossiers(item),

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
        /// What the card carries in the review buffer: the technology's own dossier, and nothing else.
        ///
        /// It is the one the player came for and the one the pointer is aimed at, so it is the one the
        /// game DRAWS and therefore the only one with words to read. Everything else the card explains -
        /// the branch, the affinity, each unlock round the rim - is a hover surface of its own and
        /// becomes an entry of its own (<see cref="Dossiers"/>), because a node raises what it points at
        /// and declaring the rest here promised words nothing would ever fill in.
        /// </summary>
        private static IList<NodeSection> Details(TechnologyItem2 item)
        {
            return GraphNodes.Sections(GraphNodes.TooltipSection(item.Tooltip));
        }

        /// <summary>
        /// Everything else the card explains, as nodes under it (<see cref="TooltipChildren"/>), in the
        /// order the card draws them: the branch's description above the disk, the affinity note under
        /// it, and one per thing the technology would unlock round the rim.
        ///
        /// They used to be sections of the card - the branch and the affinity reviewed, the unlocks
        /// declared like the dossier itself - and neither reading could be kept. The card points at the
        /// technology's own dossier and the game draws one tooltip at a time, so an unlock's dossier had
        /// no words for the buffer to hold and a reviewed sentence merged into a paragraph the player
        /// cannot step through. As entries each is aimed at its own icon and drawn on arrival, which is
        /// the same shape the wheel's own dots already use (<c>ResearchScreen.Unlocks</c>).
        ///
        /// The unlock icons are collected the wheel's way, with no carrier: the strip is a subtree the
        /// prefab keeps transparent until a mouse is on the card, so a gate asking whether it is painted
        /// would delete exactly the entries a keyboard player is here for. What says they are real is
        /// the walk of the container's own drawn children.
        /// </summary>
        private static List<TooltipChildren.Dossier> Dossiers(TechnologyItem2 item)
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(4);
            if (Shown(item.BottomSuggestionGroup))
            {
                TooltipChildren.AddPlain(
                    found,
                    item.BottomSuggestionTooltip,
                    item.BottomSuggestionGroup
                );
            }

            if (Shown(item.AffinityGroup))
            {
                TooltipChildren.AddPlain(found, item.AffinityTooltip, item.AffinityGroup);
            }

            AgeTransform unlocks = item.TechnologyUnlocksContainer;
            List<AgeTransform> children = unlocks == null || !Shown(unlocks)
                ? null
                : unlocks.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (Shown(children[i]))
                {
                    TooltipChildren.AddRevealed(
                        found,
                        AgeWidgets.Raw(children[i]),
                        children[i]
                    );
                }
            }

            return found;
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

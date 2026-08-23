using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// The card the game draws for a law, which is the same prefab in the two places it appears: the
    /// six slots across the senate's law panel, and the grid the laws window fills with everything the
    /// current filter matches.
    ///
    /// A card holding a law is a TOGGLE that only selects it - both windows keep the acting for a
    /// button of their own, which is the game's select-then-act model - so Enter selects and nothing
    /// else happens. In the senate a card can also be an EMPTY slot, which opens the laws window, or a
    /// LOCKED one, which refuses with the game's own sentence about what would unlock it; the laws
    /// window's grid never has either.
    ///
    /// What the card says is what it draws: the law's short title, the party whose colour it is banded
    /// in, and the game's own word for a law a party brings into power with it. The party and that word
    /// are drawn as wordless symbols, which is why they are read from the law rather than off the card.
    /// </summary>
    public static class LawCards
    {
        public static NodeVtable Vtable(LawCard card)
        {
            LawCard it = card;
            AgeTransform at = card.AgeTransform;
            AgeTooltip tooltip = card.Tooltip;
            if (card.GuiLaw == null)
            {
                // The two empty states have no words on them at all - a bare picture with a sentence on
                // hover - so their names are the mod's, and the sentence stays where the game put it.
                bool locked = !AgeWidgets.Enabled(at);
                string name = ModStrings.Get(
                    locked ? ModStrings.SenateLockedLawSlot : ModStrings.SenateEmptyLawSlot
                );
                NodeVtable empty = GraphNodes.Button(
                    () => name,
                    () => AgeWidgets.Toggle(it.Toggle),
                    () => AgeWidgets.Operable(at),
                    tooltip
                );
                AgeWidgets.Point(empty, card.Toggle, tooltip, at);
                return empty;
            }

            NodeVtable vtable = GraphNodes.Radio(
                () => AgeText.Label(it.LawShortTitle),
                () => it.Toggle != null && it.Toggle.State,
                () => AgeWidgets.Toggle(it.Toggle),
                () => AgeWidgets.Operable(at),
                null,
                tooltip
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => Party(it)));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => ForcedWord(it)));
            AgeWidgets.Point(vtable, card.Toggle, tooltip, at);
            return vtable;
        }

        /// <summary>
        /// Which party a law belongs to. The card says it with a coloured symbol and no words.
        ///
        /// Asked of the party's gui element rather than read off the wrapper's own <c>Title</c>: that
        /// one arrives with the party's SYMBOL already spliced into it, so the icon pipeline names the
        /// picture and the party is announced twice ("Politics Independent").
        /// </summary>
        private static string Party(LawCard card)
        {
            try
            {
                return card.GuiLaw == null || card.GuiLaw.GuiPolitics == null
                    ? null
                    : AgeText.Clean(Gui.GetLocalizedTitle(card.GuiLaw.GuiPolitics.Name));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's own word for a law a party brings with it into power, which the card
        /// draws as a badge in its corner.</summary>
        private static string ForcedWord(LawCard card)
        {
            try
            {
                if (
                    card.ForcedLawIcon == null
                    || !AgeWidgets.Visible(card.ForcedLawIcon.AgeTransform)
                )
                {
                    return null;
                }

                string word = AgeText.Clean(Gui.GetLocalizedTitle(LawCard.SubCategoryForcedLaw));
                return string.IsNullOrEmpty(word) || word[0] == '%' ? null : word;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Every card a table is drawing, as cells in the rows the game laid them out in.
        ///
        /// Drawing is asked as <see cref="AgeWidgets.Painted"/> rather than as visibility, because the
        /// laws window's grid is a POOL: it grows to the largest filter the player has ever looked at (37
        /// cards for "All") and the engine retires the cards a narrower filter does not need by fading
        /// them to nothing, leaving them visible, unbound and parked outside the grid. Measured on the
        /// "Available" filter: 5 drawn cards followed by 32 dead "Empty law slot" stops. The senate's own
        /// six slots are all painted and are unaffected.
        /// </summary>
        public static void Cards(List<Cell> cells, AgeTransform table, string keyPrefix)
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                LawCard card = widget == null ? null : widget.GetComponent<LawCard>();
                if (card == null || !AgeWidgets.Painted(widget))
                {
                    continue;
                }

                Cells.Add(
                    cells,
                    widget,
                    ControlId.Referenced(widget, keyPrefix + i),
                    Vtable(card)
                );

                // The badge in the card's corner says its WORD on the row already
                // (<see cref="ForcedWord"/>); the sentence behind it - that this law comes into force
                // by itself while its party is in power - lives in the badge's own tooltip and reached
                // nobody.
                List<TooltipChildren.Dossier> badge = new List<TooltipChildren.Dossier>(1);
                TooltipChildren.AddPlain(
                    badge,
                    card.ForcedLawIcon == null ? null : card.ForcedLawIcon.AgeTransform
                );
                if (badge.Count > 0)
                {
                    Cell owner = cells[cells.Count - 1];
                    owner.Dossiers = badge;
                    owner.Key = keyPrefix + i;
                }
            }
        }
    }
}

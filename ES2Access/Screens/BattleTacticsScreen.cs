using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The deck of battle tactics, built out of combat: what the Open button in the military screen's
    /// Battle Tactics box raises.
    ///
    /// Down the window: the tactics the empire has unlocked, the line telling the player to drop one
    /// below, the set they are choosing (six slots, some of them still locked), and the bottom row. The
    /// same six slots are drawn TINY and wordless in the military screen's side panel, where they are
    /// read by <see cref="MilitaryScreen"/>; here every card carries its own title.
    ///
    /// **Every gesture here is the game's DRAG, and there is nothing else.** A card answers a click with
    /// nothing at all (<c>BattlePlayCard.OnClickCb</c> :239-251 only clears its own toggle while the card
    /// is draggable), so the mod's carry is the whole of the interaction:
    ///
    /// - Space on an unlocked tactic that is not already in the set picks it up; Enter on a slot puts it
    ///   there. Both go through the game's own <c>OnApplyDrop</c>, which is what a released mouse calls,
    ///   so the window's own bookkeeping, its sound and its cost recount all happen exactly once.
    /// - Space on a filled slot picks that card up, so Enter on another slot SWAPS them - the same thing
    ///   the mouse does (<c>PlayCardDeckModalWindow.OnDeckChanged</c> :361-386 reads which of the two
    ///   sides is a deck slot and swaps, replaces or empties accordingly).
    /// - Ctrl+Enter on a filled slot takes that tactic OUT of the set. The game has no click for this
    ///   either: its only route is dragging a slot's card out and releasing it over nothing, which
    ///   reaches <c>OnApplyDrop(.., Provider, success: false)</c> and empties the slot - so this is that
    ///   call, on the chord whose meaning everywhere else is "one item out of the game's own selection",
    ///   and the deck IS the game's selection of tactics. It refuses on the last filled slot because the
    ///   game's own handler does (:376-380 keeps at least one).
    ///
    /// Nothing is committed until Confirm, which posts <c>OrderUpdatePlayDeck</c> behind the game's own
    /// confirmation box while a battle is running (:388-397); its refusals - a battle in progress, no
    /// changes, the influence cost unaffordable - are the game's own sentences on its tooltip.
    ///
    /// The two arrows beside the tactics list are the scroll view's own furniture
    /// (<c>OnCardListLeftButtonCb</c> shifts the strip by one card and says nothing); they carry no words
    /// of any kind, and a keyboard player gets the scrolling for free from the mod's scroll-into-view, so
    /// they are not declared - the same treatment every scrollbar in the mod gets.
    ///
    /// Escape is the game's: <c>GuiModalWindow</c> closes on it, which is what Close does too - except
    /// while something is carried, where the input layer gives it to the carry first.
    /// </summary>
    public sealed class BattleTacticsScreen : Screen
    {
        private static readonly object HeadingStop = "tactics:heading";
        private static readonly object AvailableStop = "tactics:available";
        private static readonly object DeckStop = "tactics:deck";
        private static readonly object ActionsStop = "tactics:actions";

        /// <summary>What the carry holds here, so a ship or a population unit can never be dropped into
        /// a tactics slot.</summary>
        internal const string TacticKind = "battle-tactic";

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.battle-tactics"; }
        }

        /// <summary>
        /// Above the military screen that opens it, below the tutorial popup that draws over it.
        ///
        /// Measured: this window lives in <c>ModalRenderer</c> (<c>AgeScreen.SortingOrder</c> 5) and the
        /// tutorial popup in <c>OverlayRenderer</c> (6) - and the game raised a tutorial page over this
        /// very window while it was being measured - so it has to sit under the tutorial's 30. The
        /// message box its Confirm can raise is at 100, well above.
        /// </summary>
        public override int Layer
        {
            get { return 22; }
        }

        /// <summary>The heading, because it is drawn first and Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return HeadingStop; }
        }

        public override bool IsActive()
        {
            try
            {
                PlayCardDeckModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape is the game's: the window closes itself, which is what Close does. While
        /// something is carried the input layer puts the carry down first and stops there.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            PlayCardDeckModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                BuildHeading(builder, window);
                BuildAvailable(builder, window);
                BuildDeck(builder, window);
                BuildActions(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("tactics: reading the window threw: " + e);
            }
        }

        private void BuildHeading(GraphBuilder builder, PlayCardDeckModalWindow window)
        {
            builder.BeginStop(HeadingStop);
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "TitleLabel", 3),
                "tactics:title"
            );
            Cells.Emit(builder, _cells);
        }

        /// <summary>The tactics the empire has: the game's own count of them, then one card each. A card
        /// already in the set is drawn switched off with the game's sentence for why
        /// (<c>BindAvailableBattlePlayCard</c> :312-329), so it stays declared and refuses.</summary>
        private void BuildAvailable(GraphBuilder builder, PlayCardDeckModalWindow window)
        {
            AgeTransform table = window.AvailablePlayCardsTable;
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            builder.BeginStop(AvailableStop);
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                window.AvailablePlayCardsCountLabel == null
                    ? null
                    : window.AvailablePlayCardsCountLabel.AgeTransform,
                "tactics:available-count"
            );
            Cells.Emit(builder, _cells);

            _cells.Clear();
            IList<AgeTransform> cards = table.Children;
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                AddCard(cards[i], "tactics:available/" + i);
            }

            Cells.Emit(builder, _cells);
        }

        /// <summary>The set itself: the line the game draws over it, its caption, then the six slots.
        /// </summary>
        private void BuildDeck(GraphBuilder builder, PlayCardDeckModalWindow window)
        {
            AgeTransform table = window.MyDeckPlayCardsTable;
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            builder.BeginStop(DeckStop);
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "DropCardBelowLabel", 3),
                "tactics:drop-hint"
            );
            Cells.AddReadout(
                _cells,
                AgeWidgets.ChildNamed(window.MyDeckGroup, "PanelTitle", 1),
                "tactics:deck-caption"
            );
            Cells.Emit(builder, _cells);

            _cells.Clear();
            IList<AgeTransform> slots = table.Children;
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                AddSlot(slots[i], "tactics:slot/" + i);
            }

            Cells.Emit(builder, _cells);
        }

        private void BuildActions(GraphBuilder builder, PlayCardDeckModalWindow window)
        {
            _cells.Clear();
            Cells.AddControl(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "BackButton", 2),
                "tactics:close"
            );
            Cells.AddControl(_cells, window.ValidateButton, "tactics:confirm");
            if (_cells.Count > 0)
            {
                builder.BeginStop(ActionsStop);
                Cells.Emit(builder, _cells);
            }
        }

        // ---- the cards ----

        /// <summary>One tactic in the list above: what it is called, what it does, and - while the set
        /// already holds it - the game's own sentence saying so. It can be picked up and nothing
        /// else.</summary>
        private void AddCard(AgeTransform widget, string key)
        {
            BattlePlayCard card = Card(widget);
            if (card == null)
            {
                return;
            }

            BattlePlayCard it = card;
            AgeTooltip tooltip = card.Tooltip ?? AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Offered(it.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => CardName(it)),
                    GraphNodes.ValuePart(() => Effects(it), false),
                    GraphNodes.DisabledPart(enabled),
                },
                Sections = GraphNodes.Sections(() => Wordless(it), tooltip),
                OnPickUp = () => Pick(it),
            };
            GraphNodes.AddRefusal(vtable, tooltip, enabled);

            AgeWidgets.PointAt(vtable, widget);
            // Keyed STRUCTURALLY, unlike the slots below, because the two halves of this window show the
            // SAME wrapper for a tactic that is already in the set: reference identity is followed before
            // the structural key, so declaring it on both made one control of them and threw the cursor
            // out of the slot it was standing in the instant anything rebuilt (measured - a drop aimed at
            // a filled slot landed on the list instead). The list's own order is the game's and does not
            // move, so it has nothing to ride along with anyway.
            Cells.Add(_cells, widget, ControlId.Structural(key), vtable);
        }

        /// <summary>
        /// One slot of the set: the tactic in it, the game's word for one still locked, or its sentence
        /// for an empty one.
        ///
        /// Its name is WATCHED, because every gesture that acts on a slot - a drop into it, a swap, a
        /// removal - changes what it holds under a cursor standing right there, and the game answers all
        /// three in silence.
        /// </summary>
        private void AddSlot(AgeTransform widget, string key)
        {
            BattlePlayCard card = Card(widget);
            if (card == null)
            {
                return;
            }

            BattlePlayCard it = card;
            AgeTooltip tooltip = card.Tooltip ?? AgeWidgets.Raw(widget);
            Func<bool> enabled = () => Status(it) != EncounterPlayDeckSlot.SlotState.Locked;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(
                        () => CardName(it),
                        live: true,
                        kind: AnnouncementKinds.Label
                    ),
                    GraphNodes.ValuePart(() => Effects(it), false),
                    GraphNodes.DisabledPart(enabled),
                },
                Sections = GraphNodes.Sections(() => Wordless(it), tooltip),
                DropKind = TacticKind,
                OnDrop = held => Drop(it, held),
                OnPickUp = () => Pick(it),
                OnSelectToggle = () => Remove(it),
            };
            GraphNodes.AddRefusal(vtable, tooltip, enabled);

            CarryState carry = ModEntry.Carry;
            if (carry != null)
            {
                // Guarded by the same test the drop makes: a LOCKED slot is not a place a tactic can go,
                // and saying "drop target" on it and then refusing the Enter is the readout promising
                // something the screen will not do.
                vtable.Announcements.Add(carry.DropTargetPart(TacticKind, enabled));
            }

            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(_cells, widget, Id(card, key), vtable);
        }

        /// <summary>Keyed on the tactic the slot is showing where there is one, so the cursor rides along
        /// when a drop or a swap moves it, and structurally for a slot with nothing in it - two empty
        /// slots have no object to tell them apart.</summary>
        private static ControlId Id(BattlePlayCard card, string key)
        {
            try
            {
                GuiBattlePlaySlot slot = card.GuiBattlePlaySlot;
                return slot == null || slot.GuiCard == null
                    ? ControlId.Structural(key)
                    : ControlId.Referenced(slot.GuiCard, key);
            }
            catch (Exception)
            {
                return ControlId.Structural(key);
            }
        }

        private static BattlePlayCard Card(AgeTransform widget)
        {
            try
            {
                BattlePlayCard card =
                    widget == null ? null : widget.GetComponent<BattlePlayCard>();
                return card != null
                    && card.IsBound
                    && card.GuiBattlePlaySlot != null
                    && AgeWidgets.Visible(widget)
                    ? card
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static EncounterPlayDeckSlot.SlotState Status(BattlePlayCard card)
        {
            try
            {
                return card.GuiBattlePlaySlot.Status;
            }
            catch (Exception)
            {
                return EncounterPlayDeckSlot.SlotState.Locked;
            }
        }

        /// <summary>What a card is called: the tactic's own title where it has one, and otherwise the
        /// word the game draws in the slot - "Locked slot" - or, failing that, the sentence it explains
        /// the empty slot with.</summary>
        private static string CardName(BattlePlayCard card)
        {
            try
            {
                GuiBattlePlaySlot slot = card.GuiBattlePlaySlot;
                string title = slot == null ? null : AgeText.Clean(slot.Title);
                if (!string.IsNullOrEmpty(title))
                {
                    return title;
                }

                string drawn = AgeWidgets.TextOf(card.AgeTransform);
                return string.IsNullOrEmpty(drawn)
                    ? CardActions.FirstLine(card.Tooltip ?? AgeWidgets.Raw(card.AgeTransform))
                    : drawn;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The two things on a card that are drawn as pictures and named nowhere in words: the range each
        /// of the ship's three flotillas fights this tactic best at - three stacked rows of bars, in
        /// flotilla order, each with its range on its own tooltip - and the family badge, whose tooltip is
        /// the paragraph about what this kind of tactic is for.
        ///
        /// Everything else on the card is already spoken: its title, and the effects paragraph it draws in
        /// full.
        /// </summary>
        private static IList<string> Wordless(BattlePlayCard card)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTransform ranges = card.FlotillaRangeIndicators;
                IList<AgeTransform> rows = ranges == null ? null : ranges.Children;
                for (int i = 0; rows != null && i < rows.Count; i++)
                {
                    if (AgeWidgets.Visible(rows[i]))
                    {
                        Add(lines, CardActions.FirstLine(AgeWidgets.Raw(rows[i])));
                    }
                }

                AgeTransform family =
                    card.FamilyIcon == null ? null : card.FamilyIcon.AgeTransform;
                if (family != null && AgeWidgets.Visible(family))
                {
                    IList<string> words = AgeWidgets.TooltipLines(AgeWidgets.Raw(family))();
                    for (int i = 0; words != null && i < words.Count; i++)
                    {
                        Add(lines, words[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("tactics: reading a card's markings threw: " + e);
            }

            return lines;
        }

        private static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                lines.Add(line);
            }
        }

        /// <summary>What the card draws under its artwork: the effects it would have in battle, which are
        /// on the screen the whole time and so part of what the card says.</summary>
        private static string Effects(BattlePlayCard card)
        {
            try
            {
                AgeTransform effects = AgeWidgets.ChildNamed(card.AgeTransform, "Effects", 4);
                return effects == null ? null : AgeWidgets.TextOf(effects);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the drag ----

        private static CarryItem Pick(BattlePlayCard card)
        {
            try
            {
                return card.MyDraggableItem == null
                    || !card.MyDraggableItem.CanDrag
                    || !AgeWidgets.Offered(card.AgeTransform)
                    ? null
                    : new CarryItem(card, CardName(card), TacticKind);
            }
            catch (Exception e)
            {
                Log.Warn("tactics: picking a tactic up threw: " + e);
                return null;
            }
        }

        /// <summary>Put the carried tactic in this slot, through the game's own drop handler - which is
        /// what decides whether this is a fill, a replacement or a swap.</summary>
        private static DropResult Drop(BattlePlayCard slot, CarryItem held)
        {
            try
            {
                BattlePlayCard dragged = held.Cargo as BattlePlayCard;
                if (dragged == null)
                {
                    return DropResult.Refused(null);
                }

                if (ReferenceEquals(dragged, slot))
                {
                    // Back into the slot it came out of: the drag ends having moved nothing, which is
                    // what putting something down on its own row means everywhere else.
                    return DropResult.Done(ModStrings.Get(ModStrings.CarryCancelled));
                }

                if (Status(slot) == EncounterPlayDeckSlot.SlotState.Locked || slot.Client == null)
                {
                    return DropResult.Refused(null);
                }

                slot.OnApplyDrop(
                    dragged.gameObject,
                    DraggableItem.DragDropItemStatus.Receiver,
                    true
                );
                return DropResult.Done(
                    ModStrings.Format(ModStrings.TacticsSlotFilled, held.Name)
                );
            }
            catch (Exception e)
            {
                Log.Warn("tactics: dropping a tactic threw: " + e);
                return DropResult.Refused(null);
            }
        }

        /// <summary>Take this slot's tactic out of the set - the game's own drag-it-out-and-let-go, which
        /// is the only route it has. Its handler keeps the last one, so the last filled slot refuses in
        /// silence the way every swallowed activation does.</summary>
        private static void Remove(BattlePlayCard slot)
        {
            try
            {
                if (
                    slot.Client == null
                    || !slot.IsDeckSlot
                    || Status(slot) != EncounterPlayDeckSlot.SlotState.Filled
                )
                {
                    return;
                }

                slot.OnApplyDrop(
                    slot.gameObject,
                    DraggableItem.DragDropItemStatus.Provider,
                    false
                );
            }
            catch (Exception e)
            {
                Log.Warn("tactics: taking a tactic out threw: " + e);
            }
        }

        private static PlayCardDeckModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<PlayCardDeckModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

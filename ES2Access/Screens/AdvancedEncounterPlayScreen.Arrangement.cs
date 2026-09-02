using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

namespace ES2Access.Screens
{
    /// <summary>How the fleets are arranged in the arena: each side's flotillas, the cards the window
    /// draws for them, and where a card can be sent.</summary>
    public sealed partial class AdvancedEncounterPlayScreen
    {
        /// <summary>One side's fleets, in the flotillas they will fight in, while the switch has the panel
        /// out. The shared roster reading answers the whole of it, plus whatever this window draws about
        /// a flotilla that the roster panel does not (<paramref name="extras"/>).</summary>
        private static void Roster(
            GraphBuilder builder,
            BattleGroupSetupPanel panel,
            string prefix,
            BattleRosters.FlotillaExtras extras
        )
        {
            BattleRosters.Roster(builder, AgeWidgets.Transform(panel), prefix, extras);
        }

        /// <summary>
        /// The other half of a flotilla, which this window is the only screen to draw.
        ///
        /// A flotilla appears twice here: as a line of ships in the roster panel, and as the card in the
        /// 3D arena the player drags ships onto. The card is where the game writes what the line never
        /// says - the sentence that says a flotilla is LOCKED and what would unlock it ("Unlocked at 5
        /// CP and 2 Ships", <c>EncounterPlayFlotillaCard3D.RefreshInfo</c> :66-101), the minimum for the
        /// one that is open ("Minimum 1 CP"), and the hover sentence naming the range it is optimal at
        /// and how well the ships suit it (<c>EncounterPlayFlotillaCard2D.Refresh</c> :123). All of it
        /// is on the player's screen already; none of it is anywhere a roster row could reach.
        ///
        /// The card is found through the game's own binding rather than by walking widget names: the 2D
        /// cards are bound with the flotilla's INDEX on them
        /// (<c>EncounterPlayFlotillaCardContainer.BindFlotillaCard2D</c>) and each holds its own 3D card
        /// and flotilla data. The line is matched to it by the NUMBER the line draws, never by child
        /// order - the two collections are built by different code and agreeing today is not a
        /// contract.
        /// </summary>
        private static BattleRosters.FlotillaExtras FlotillaCards(
            AdvancedEncounterPlayModalWindow window
        )
        {
            AdvancedEncounterPlayModalWindow it = window;
            return new BattleRosters.FlotillaExtras
            {
                Drawn = line => AgeWidgets.DrawnLabel(CommandPoints(Card(it, line))),
                Tooltip = line => AgeWidgets.Raw(AgeWidgets.Transform((GuiPanel)Card(it, line))),
                Row = (line, vtable) => Destination(it, line, vtable),
                Ship = (line, item, vtable) => Arrangeable(it, line, item, vtable),
            };
        }

        /// <summary>
        /// A flotilla row - or a ship row inside one - as somewhere to PUT A SHIP DOWN: the card's own
        /// half of the drag the arena draws, given to the lines the keyboard walks. Both rows get the
        /// same card because the game's own drop is a hit test against whichever flotilla card contains
        /// the dropped point, and a ship is drawn on its flotilla's card.
        ///
        /// The acceptance test and the drop are both the game's
        /// (<see cref="BattleShipMoves.Accepts"/>, <see cref="BattleShipMoves.Drop"/>), so the "drop
        /// target" word appears on exactly the flotillas <c>CanAddShip</c> would take the ship into -
        /// never on the one it is already in, and never on one the battle has locked - and a player
        /// who presses the key on a locked one anyway hears the game's own sentence for what would
        /// unlock it, which is written on the card and nowhere else.
        ///
        /// A row the window is drawing no card for takes nothing: the enemy's side has no flotilla
        /// cards at all, and a line whose number matches none of them is a line this screen cannot
        /// act on.
        /// </summary>
        private static void Destination(
            AdvancedEncounterPlayModalWindow window,
            FlotillaLine line,
            NodeVtable vtable
        )
        {
            EncounterPlayFlotillaCard3DInteractive card = Card3D(Card(window, line));
            if (card == null)
            {
                return;
            }

            EncounterPlayFlotillaCard3DInteractive at = card;
            FlotillaLine it = line;
            vtable.DropKind = BattleShipMoves.Kind;
            vtable.DropAccepts = held =>
                BattleShipMoves.Accepts(at, held.Cargo as EncounterPlayShipItemInteractive);
            vtable.OnDrop = held =>
                BattleShipMoves.Landed(at, held, BattleRosters.FlotillaNumber(it));
        }

        /// <summary>
        /// A ship row as something the player ARRANGES: whether it is pinned to the flotilla it is in,
        /// the carry that moves it to another, and the flotilla it is already in as somewhere to put
        /// another ship down.
        ///
        /// THE LOCK IS ON THE DOUBLE-CLICK CHORD, because that is the gesture the game puts it on: the
        /// chip in the arena is pinned by a second click and by nothing else, and every chord in this
        /// mod means the game's own gesture and nothing else (owner ruling 2026-08-29, reversing the
        /// activation-key binding of the same day). The row keeps the two state words - it says which
        /// state it is in whenever it is read, and says the new one the moment the chord turns it over -
        /// and it keeps NO role word: with Enter no longer its toggle the row is not a checkbox, and a
        /// line the player reads, drags and double-clicks is the roster's own plain line with more on
        /// it (the buffer's derived hint is what names the chord). What being locked MEANS is what the
        /// sorting buttons above do: a pinned ship is the one they leave where the player put it.
        ///
        /// Enter is left free for the DROP: while a ship is being carried, the activation key on this
        /// row lands it in the flotilla this ship is in, which is the same commit the flotilla's own
        /// line makes (<see cref="Destination"/>) - and the game's own drop is a hit test against
        /// whichever flotilla card contains the point, so a ship's card and its flotilla's card are
        /// one and the same target. With nothing held Enter on the row does nothing at all, as the
        /// chip's own single click does.
        ///
        /// The pick-up is offered wherever a chip exists, exactly as the mouse's drag is - a drag onto
        /// a flotilla that will not take the ship is how the game tells a player why not, and taking
        /// that away would take the answer with it. It is the CHIP that is carried, because the chip
        /// is what the game's own drop moves; the name is the row's, captured at pick-up like every
        /// carry's.
        /// </summary>
        private static void Arrangeable(
            AdvancedEncounterPlayModalWindow window,
            FlotillaLine line,
            BattleShipItem item,
            NodeVtable vtable
        )
        {
            EncounterShipSetup setup = BattleShipMoves.SetupOf(item);
            EncounterPlayShipItemInteractive chip = BattleShipMoves.Chip(Cards3D(window), setup);
            if (setup == null || chip == null)
            {
                return;
            }

            EncounterShipSetup ship = setup;
            EncounterPlayShipItemInteractive at = chip;
            BattleShipItem row = item;
            Func<string> state = () =>
                ModStrings.Get(
                    BattleShipMoves.Locked(ship)
                        ? ModStrings.BattleShipLockedInFlotilla
                        : ModStrings.BattleShipNotLocked
                );

            vtable.Announcements.Add(GraphNodes.ValuePart(state));
            vtable.StateText = state;
            vtable.OnDoubleClick = () => BattleShipMoves.ToggleLock(ship);
            NodeHints.Add(vtable, ModStrings.HintLockShip, UiActions.DoubleClick);
            vtable.OnPickUp = () => BattleShipMoves.Pick(at, BattleRosters.ShipName(row));
            Destination(window, line, vtable);
        }

        /// <summary>Every flotilla card the arena is drawing for the player's side, as the interactive
        /// kind that holds ships - what a chip is looked up in, and what a drop is aimed at. The 2D
        /// cards are the game's own index into them, so this walks the same container
        /// <see cref="Card"/> does.</summary>
        private static EncounterPlayFlotillaCard3DInteractive[] Cards3D(
            AdvancedEncounterPlayModalWindow window
        )
        {
            List<EncounterPlayFlotillaCard3DInteractive> cards =
                new List<EncounterPlayFlotillaCard3DInteractive>(4);
            IList<AgeTransform> children = Children(Cards(window));
            for (int i = 0; children != null && i < children.Count; i++)
            {
                EncounterPlayFlotillaCard3DInteractive card = Card3D(
                    children[i].GetComponent<EncounterPlayFlotillaCard2D>()
                );
                if (card != null)
                {
                    cards.Add(card);
                }
            }

            return cards.ToArray();
        }

        /// <summary>The card in the arena a 2D card is bound to, where it is the kind that arranges
        /// flotillas. Null for the enemy's side, whose container arranges a fleet.</summary>
        private static EncounterPlayFlotillaCard3DInteractive Card3D(
            EncounterPlayFlotillaCard2D card
        )
        {
            try
            {
                return card == null
                    ? null
                    : card.Card3D as EncounterPlayFlotillaCard3DInteractive;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The card standing for the flotilla a roster line names, by the number the line drew.
        /// Null where the window is drawing no cards, or where nothing answers to that number.</summary>
        private static EncounterPlayFlotillaCard2D Card(
            AdvancedEncounterPlayModalWindow window,
            FlotillaLine line
        )
        {
            try
            {
                int number;
                if (
                    line == null
                    || !int.TryParse(AgeText.Label(line.FlotillaIndexLabel), out number)
                )
                {
                    return null;
                }

                AgeTransform box = Cards(window);
                IList<AgeTransform> children = Children(box);
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    EncounterPlayFlotillaCard2D card =
                        children[i].GetComponent<EncounterPlayFlotillaCard2D>();
                    // The game numbers the flotillas from one where it writes them down and from zero
                    // where it binds them.
                    if (card != null && card.Index == number - 1)
                    {
                        return card;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("advanced play: looking for a flotilla card threw: " + e);
            }

            return null;
        }

        /// <summary>Where the window keeps the cards: the one container among the arena's that draws
        /// flotillas. The enemy's side has a fleet container instead, which is why this is asked by TYPE
        /// rather than by position.</summary>
        private static AgeTransform Cards(AdvancedEncounterPlayModalWindow window)
        {
            EncounterPlayScreen3D arena = window == null ? null : window.EncounterPlayScreen3D;
            EncounterPlayContainer[] containers =
                arena == null ? null : arena.PlayerEncounterPlayContainers;
            for (int i = 0; containers != null && i < containers.Length; i++)
            {
                EncounterPlayFlotillaCardContainer cards =
                    containers[i] as EncounterPlayFlotillaCardContainer;
                if (cards != null)
                {
                    return cards.FlotillaCard2DContainer;
                }
            }

            return null;
        }

        /// <summary>The unlock sentence a card writes under its number - kept on the 3D card the 2D one
        /// is bound to.</summary>
        private static AgePrimitiveLabel CommandPoints(EncounterPlayFlotillaCard2D card)
        {
            try
            {
                EncounterPlayFlotillaCard3D card3d = card == null ? null : card.Card3D;
                return card3d == null ? null : card3d.CommandPointsLabel;
            }
            catch (Exception)
            {
                return null;
            }
        }

    }
}

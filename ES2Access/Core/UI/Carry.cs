using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// Something the player has picked up and is holding: a ship being moved between fleets, a
    /// population unit being moved between planets.
    ///
    /// The NAME is captured here, at pick-up, and never re-derived. The whole point of carrying is
    /// that the thing leaves where it was: by the time it is dropped, the row it was read off may
    /// be gone, may have been recycled onto another item, or may be drawn on a screen the player
    /// has left. A name resolved at drop time is therefore a name for something else.
    /// </summary>
    public sealed class CarryItem
    {
        /// <summary>The game's own object - what a drop actually acts on, and the identity two
        /// carries are compared by.</summary>
        public readonly object Cargo;

        /// <summary>What to call it, in the game's words, as they were when it was picked up.</summary>
        public readonly string Name;

        /// <summary>Which sort of thing this is ("ship", "population"). A control takes cargo of one
        /// kind, so a ship cannot be dropped into a planet's population.</summary>
        public readonly string Kind;

        public CarryItem(object cargo, string name, string kind)
        {
            Cargo = cargo;
            Name = name;
            Kind = kind;
        }
    }

    /// <summary>
    /// What a drop target answered. A refusal is the GAME's refusal - the words its own check gives
    /// for why this cannot happen - never a rule the mod invented, and the carry survives it: the
    /// player is still holding the thing and can try somewhere else.
    /// </summary>
    public sealed class DropResult
    {
        public readonly bool Dropped;

        /// <summary>What to say. On a drop, the screen's own account of what happened (null falls
        /// back to "Dropped X"); on a refusal, the game's reason (null falls back to "X cannot go
        /// there").</summary>
        public readonly string Message;

        private DropResult(bool dropped, string message)
        {
            Dropped = dropped;
            Message = message;
        }

        /// <summary>It happened. <paramref name="message"/> is the screen's own words for it.</summary>
        public static DropResult Done(string message = null)
        {
            return new DropResult(true, message);
        }

        /// <summary>The game said no, in <paramref name="reason"/>'s words. The player keeps
        /// carrying.</summary>
        public static DropResult Refused(string reason = null)
        {
            return new DropResult(false, reason);
        }
    }

    /// <summary>
    /// What the player is carrying, if anything - the whole of the pick-up-and-drop mode.
    ///
    /// An instance rather than a static so it dies with the mod on a hot reload, and engine-free so
    /// the rules below are unit-tested off the game. The screens declare which controls can be
    /// picked up and which will take a drop (<see cref="NodeVtable.OnPickUp"/>,
    /// <see cref="NodeVtable.OnDrop"/>); everything about what a key press MEANS is decided in
    /// <see cref="CarryActions"/>, and what a control SAYS about dragging is derived from those same two
    /// declarations (<see cref="DraggablePart"/>, <see cref="DropTargetPart"/>, added to every control's
    /// readout by <c>GraphAnnouncer.EffectiveAnnouncements</c>) - no screen writes the words.
    ///
    /// The carry belongs to the screen it started on (<see cref="Owner"/>): its drop targets are
    /// there, so a player who walks off to another page is no longer carrying anything. A menu
    /// opened OVER that screen does not count as leaving it - see
    /// <see cref="ScreenChanged"/>.
    /// </summary>
    public sealed class CarryState
    {
        /// <summary>What is being carried, or null.</summary>
        public CarryItem Held { get; private set; }

        /// <summary>The screen the carry started on. Opaque here - the adapter knows what a screen
        /// is.</summary>
        public object Owner { get; private set; }

        public bool IsCarrying
        {
            get { return Held != null; }
        }

        /// <summary>Whether what is being carried is of <paramref name="kind"/> - the question a
        /// drop target asks to know whether it is one right now.</summary>
        public bool Accepts(string kind)
        {
            return Held != null && Held.Kind == kind;
        }

        public void PickUp(CarryItem item, object owner)
        {
            Held = item;
            Owner = item == null ? null : owner;
        }

        public void Clear()
        {
            Held = null;
            Owner = null;
        }

        /// <summary>
        /// The focused screen changed. <paramref name="stillOnOwnersPage"/> is the adapter's answer
        /// to "is the screen the carry started on still the page the player is on" - true while a
        /// menu or a child screen is open over it. False drops the carry, silently: the player went
        /// somewhere the thing they were holding cannot be put down.
        /// </summary>
        public void ScreenChanged(bool stillOnOwnersPage)
        {
            if (IsCarrying && !stillOnOwnersPage)
            {
                Clear();
            }
        }

        /// <summary>
        /// Whether a control would TAKE what is being held right now: the right sort of place for this
        /// cargo, and - where the screen declared a test of its own for the targets among a family that
        /// will refuse - that test too (<see cref="NodeVtable.DropAccepts"/>).
        /// </summary>
        public bool Takes(NodeVtable vtable)
        {
            return vtable != null
                && vtable.OnDrop != null
                && Accepts(vtable.DropKind)
                && (vtable.DropAccepts == null || vtable.DropAccepts(Held));
        }

        /// <summary>
        /// What a control the player could pick something up from says while NOTHING is being carried -
        /// the only announcement of the pick-up key there is.
        ///
        /// It goes quiet the moment something is held: the player is then hunting for somewhere to put
        /// that thing down, and being told the control under the cursor could also be picked up is noise.
        /// A control that is both source and target therefore says "drop target" mid-drag and nothing
        /// else.
        ///
        /// The pick-up command answers for itself whether there is anything to give right now - an empty
        /// slot, a foreign ship, a population the game will not let leave - so the word cannot promise a
        /// gesture that would do nothing. That is why the command must be a pure query
        /// (<see cref="NodeVtable.OnPickUp"/>) and why this part is NOT live: it is asked when a readout
        /// is composed, never per frame, and a word appearing on its own after a drag was cancelled
        /// would be noise on top of the gesture that already said what happened.
        /// </summary>
        public NodeAnnouncement DraggablePart(NodeVtable vtable)
        {
            if (vtable == null || vtable.OnPickUp == null)
            {
                return null;
            }

            NodeVtable it = vtable;
            return new NodeAnnouncement(
                () =>
                    Held == null && it.OnPickUp() != null
                        ? ModStrings.Get(ModStrings.CarryDraggable)
                        : null
            );
        }

        /// <summary>
        /// The state word a control that would TAKE the carried thing says while focused. Live, so it
        /// appears and disappears under a cursor left standing on the target while the player picks
        /// something up or puts it down. Says nothing when nothing compatible is being carried, which is
        /// every other moment of the game.
        /// </summary>
        public NodeAnnouncement DropTargetPart(NodeVtable vtable)
        {
            if (vtable == null || vtable.OnDrop == null)
            {
                return null;
            }

            NodeVtable it = vtable;
            return new NodeAnnouncement(
                () => Takes(it) ? ModStrings.Get(ModStrings.CarryDropTarget) : null,
                true
            );
        }
    }

    /// <summary>What a carry key press did, and what to say about it. Composed here rather than in
    /// the navigator so the whole decision - including its wording - is testable off the game.</summary>
    public sealed class CarryOutcome
    {
        /// <summary>Whether the key was ours. False means the mod has no business with it here and
        /// the game should get it.</summary>
        public readonly bool Handled;

        /// <summary>What to speak, interrupting, or null.</summary>
        public readonly string Speech;

        public CarryOutcome(bool handled, string speech)
        {
            Handled = handled;
            Speech = speech;
        }

        public static readonly CarryOutcome NotOurs = new CarryOutcome(false, null);
    }

    /// <summary>
    /// What the two carry gestures do on the control the player is standing on. One decision table,
    /// in one place, because each key means several things depending on what is being held.
    ///
    /// The carry key (<see cref="Press"/>) is the one that HOLDS things:
    ///
    ///   nothing held, control offers something  -> pick it up
    ///   something held, control offers another  -> carry that one instead
    ///   something held, control offers this one -> put it back down
    ///   something held, control offers neither  -> nothing, silently
    ///
    /// The activation key (<see cref="Activate"/>) is the one that PUTS THEM DOWN: on a control that
    /// will take what is held it drops there, through the GAME's own check, and everywhere else it
    /// was never ours - the control does its own click and the carry simply survives it. That split
    /// is what keeps a carry from being a mode the player is trapped in: normal navigation and
    /// normal activation go on working, and only a drop, a put-back, the back key or leaving the
    /// page ends it.
    ///
    /// A refused drop keeps the carry: the player hears why, still holding the thing, and can try
    /// somewhere else. A control that is neither source nor target CONSUMES the carry key while a
    /// carry is up - the carry is the mode the player is in - and answers with silence, because
    /// looking for the target is done by pressing the key along a row of controls and a cue on each
    /// of them is noise. Where nothing is being carried the carry key was never ours and the game
    /// gets it.
    /// </summary>
    public static class CarryActions
    {
        /// <summary>
        /// Whether the carry key belongs to the mod on this control: the same question the dispatch
        /// below answers, asked BEFORE the press so the game can be told to stand down from it (the
        /// key is the game's own everywhere else). Never speaks and changes nothing.
        /// </summary>
        public static bool Claims(NodeVtable vtable, CarryState carry)
        {
            if (carry != null && carry.IsCarrying)
            {
                return true;
            }

            return vtable != null && vtable.OnPickUp != null;
        }

        /// <summary>The carry key, pressed on the control <paramref name="vtable"/> describes.</summary>
        public static CarryOutcome Press(NodeVtable vtable, CarryState carry, object owner)
        {
            if (carry == null)
            {
                return CarryOutcome.NotOurs;
            }

            if (!carry.IsCarrying)
            {
                return vtable != null && vtable.OnPickUp != null
                    ? PickUp(vtable, carry, owner)
                    : CarryOutcome.NotOurs;
            }

            if (vtable != null && vtable.OnPickUp != null)
            {
                return PickUp(vtable, carry, owner);
            }

            // Held something, and this control has nothing to give - including a control that would
            // TAKE it, which is the activation key's business and not this one's. Claimed - the carry
            // is a mode and the key belongs to it - but silent: while carrying, the key is pressed on
            // control after control looking for the one that will hand over something else, and a cue
            // on each of them is noise. The carry survives, which is what the player is listening for.
            return new CarryOutcome(true, null);
        }

        /// <summary>
        /// The activation key, pressed while something is held. Handled - and a drop - only on a
        /// control that will take THIS cargo; anywhere else the answer is
        /// <see cref="CarryOutcome.NotOurs"/> and the control's own click runs, with the carry still
        /// live. That is deliberate: the player has to be able to walk a page and use it while
        /// holding something, and the destination is confirmed with the same key that confirms
        /// everything else.
        /// </summary>
        public static CarryOutcome Activate(NodeVtable vtable, CarryState carry)
        {
            if (carry == null || !carry.IsCarrying)
            {
                return CarryOutcome.NotOurs;
            }

            CarryItem held = carry.Held;
            return vtable != null && vtable.OnDrop != null && vtable.DropKind == held.Kind
                ? Drop(vtable, carry, held)
                : CarryOutcome.NotOurs;
        }

        /// <summary>Give up the carry - what the back key does while something is held. Not handled
        /// when nothing is: the key then means whatever it always meant.</summary>
        public static CarryOutcome Cancel(CarryState carry)
        {
            if (carry == null || !carry.IsCarrying)
            {
                return CarryOutcome.NotOurs;
            }

            carry.Clear();
            return new CarryOutcome(true, ModStrings.Get(ModStrings.CarryCancelled));
        }

        private static CarryOutcome PickUp(NodeVtable vtable, CarryState carry, object owner)
        {
            CarryItem item = vtable.OnPickUp();
            if (item == null)
            {
                // The control can be a source and still have nothing to give right now (an empty
                // slot, a ship the game will not release). Silent, like every other gesture key with
                // nothing to do: the key is pressed speculatively along a row, and a cue on each
                // press is noise rather than reassurance.
                return new CarryOutcome(true, null);
            }

            if (carry.IsCarrying && ReferenceEquals(carry.Held.Cargo, item.Cargo))
            {
                // Back onto the control it came from: the drag ends having moved nothing, which is
                // what the back key does too - so it says the same thing.
                carry.Clear();
                return new CarryOutcome(true, ModStrings.Get(ModStrings.CarryCancelled));
            }

            carry.PickUp(item, owner);
            return new CarryOutcome(true, ModStrings.Format(ModStrings.CarryCarrying, item.Name));
        }

        private static CarryOutcome Drop(NodeVtable vtable, CarryState carry, CarryItem held)
        {
            DropResult result = vtable.OnDrop(held);
            if (result == null || !result.Dropped)
            {
                string refusal = result == null ? null : result.Message;
                return new CarryOutcome(
                    true,
                    string.IsNullOrEmpty(refusal)
                        ? ModStrings.Format(ModStrings.CarryDropRefused, held.Name)
                        : refusal
                );
            }

            carry.Clear();
            return new CarryOutcome(
                true,
                string.IsNullOrEmpty(result.Message)
                    ? ModStrings.Format(ModStrings.CarryDropped, held.Name)
                    : result.Message
            );
        }
    }
}

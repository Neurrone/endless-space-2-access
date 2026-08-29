using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Tests.Speech;
using Xunit;
using static ES2Access.Tests.UI.Graphs;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// Picking something up and putting it down: the two decision tables in
    /// <see cref="CarryActions"/>. <see cref="CarryActions.Press"/> is the carry key, which only ever
    /// HOLDS things - pick up, swap, put back; <see cref="CarryActions.Activate"/> is the activation
    /// key, which is the only thing that DROPS. Every phrase here is the shipped English one, because
    /// what the player HEARS is the behavior - a drop that refuses silently and one that refuses in
    /// the game's words are the same code path with different evidence.
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class CarryTests
    {
        private const string Ship = "ship";

        private static NodeVtable Source(object cargo, string name, string kind = Ship)
        {
            NodeVtable vtable = Vt(name);
            vtable.OnPickUp = () => new CarryItem(cargo, name, kind);
            return vtable;
        }

        private static NodeVtable Target(DropResult answer, string kind = Ship)
        {
            NodeVtable vtable = Vt("Fleet");
            vtable.DropKind = kind;
            vtable.OnDrop = item => answer;
            return vtable;
        }

        [Fact]
        public void PickingSomethingUpAnnouncesItByTheNameItHadThen()
        {
            CarryState carry = new CarryState();
            object explorer = new object();
            CarryOutcome outcome = CarryActions.Press(
                Source(explorer, "Explorer"),
                carry,
                "galaxy"
            );

            Assert.True(outcome.Handled);
            Assert.Equal("Dragging Explorer", outcome.Speech);
            Assert.Same(explorer, carry.Held.Cargo);
            Assert.Equal("Explorer", carry.Held.Name);
            Assert.Equal("galaxy", carry.Owner);
        }

        [Fact]
        public void TheCarriedNameSurvivesTheControlItCameFrom()
        {
            CarryState carry = new CarryState();
            string drawn = "Explorer";
            NodeVtable vtable = Vt("row");
            vtable.OnPickUp = () => new CarryItem(new object(), drawn, Ship);
            CarryActions.Press(vtable, carry, "galaxy");

            // The row is recycled onto another ship, which is what actually happens to a list the
            // game re-sorts under the player.
            drawn = "Hunter";

            Assert.Equal("Explorer", carry.Held.Name);
            Assert.Equal("Cancelled drag", CarryActions.Cancel(carry).Speech);
        }

        [Fact]
        public void AControlWithNothingToGiveIsClaimedAndSilent()
        {
            CarryState carry = new CarryState();
            NodeVtable empty = Vt("Empty slot");
            empty.OnPickUp = () => null;

            CarryOutcome outcome = CarryActions.Press(empty, carry, "galaxy");

            // Claimed - the control IS a source - but silent, like every other gesture key with
            // nothing to do on the control it was pressed on.
            Assert.True(outcome.Handled);
            Assert.Null(outcome.Speech);
            Assert.False(carry.IsCarrying);
        }

        [Fact]
        public void TheKeyIsNotOursOnAControlWithNothingToCarry()
        {
            CarryState carry = new CarryState();
            Assert.False(CarryActions.Claims(Vt("Button"), carry));
            Assert.False(CarryActions.Press(Vt("Button"), carry, "galaxy").Handled);
        }

        [Fact]
        public void EverythingIsOursWhileSomethingIsBeingCarriedAndSaysNothing()
        {
            CarryState carry = new CarryState();
            carry.PickUp(new CarryItem(new object(), "Explorer", Ship), "galaxy");

            Assert.True(CarryActions.Claims(Vt("Button"), carry));
            CarryOutcome outcome = CarryActions.Press(Vt("Button"), carry, "galaxy");

            // Consumed - the carry is the mode the player is in - but silent: looking for the target
            // means pressing the key along a row of controls, and a cue on each of them is noise.
            Assert.True(outcome.Handled);
            Assert.Null(outcome.Speech);
            Assert.True(carry.IsCarrying);
        }

        [Fact]
        public void AnotherSourceSwapsWhatIsBeingCarried()
        {
            CarryState carry = new CarryState();
            object hunter = new object();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Press(Source(hunter, "Hunter"), carry, "galaxy");

            Assert.Equal("Dragging Hunter", outcome.Speech);
            Assert.Same(hunter, carry.Held.Cargo);
        }

        /// <summary>There is NO put-back (owner ruling 2026-08-29): the key pressed again on the
        /// control the thing came from picks it up again rather than ending the carry. It has to be, as
        /// soon as one source can hand over different AMOUNTS of the same thing - a population marker
        /// carries itself and every marker after it - because pressing the key on a slot is then how a
        /// player asks for that slot's amount, and a cancel there would throw the carry away instead.
        /// The back key is the only cancel.</summary>
        [Fact]
        public void TheSourceItCameFromHandsItOverAgainInsteadOfPuttingItBack()
        {
            CarryState carry = new CarryState();
            object explorer = new object();
            NodeVtable row = Source(explorer, "Explorer");
            CarryActions.Press(row, carry, "galaxy");

            CarryOutcome outcome = CarryActions.Press(row, carry, "galaxy");

            Assert.True(outcome.Handled);
            Assert.Equal("Dragging Explorer", outcome.Speech);
            Assert.True(carry.IsCarrying);
            Assert.Same(explorer, carry.Held.Cargo);
        }

        /// <summary>A re-pick on the SAME cargo can still change how much of it is held: that is the
        /// whole reason the put-back had to go.</summary>
        [Fact]
        public void ARePickOnTheSameCargoTakesTheNewAmount()
        {
            CarryState carry = new CarryState();
            object imperials = new object();
            NodeVtable first = Vt("First");
            first.OnPickUp = () => new CarryItem(imperials, "Imperials x 3", Ship, 3);
            NodeVtable last = Vt("Last");
            last.OnPickUp = () => new CarryItem(imperials, "Imperials", Ship);

            CarryActions.Press(first, carry, "system");
            Assert.Equal(3, carry.Held.Quantity);

            CarryActions.Press(last, carry, "system");
            Assert.Equal(1, carry.Held.Quantity);
            Assert.Same(imperials, carry.Held.Cargo);
        }

        /// <summary>What one press picked up is what the drop is told about, captured at pick-up like
        /// the name and for the same reason.</summary>
        [Fact]
        public void TheQuantityTravelsFromThePickUpToTheDrop()
        {
            CarryState carry = new CarryState();
            NodeVtable source = Vt("Slot");
            source.OnPickUp = () => new CarryItem(new object(), "Imperials x 3", Ship, 3);
            int dropped = 0;
            NodeVtable target = Vt("Port");
            target.DropKind = Ship;
            target.OnDrop = item =>
            {
                dropped = item.Quantity;
                return DropResult.Done();
            };

            CarryActions.Press(source, carry, "system");
            CarryActions.Activate(target, carry);

            Assert.Equal(3, dropped);
        }

        /// <summary>The pick-up announcement teaches the way out - both ways - with the chords spelled
        /// by the injected renderer rather than written into the sentence, so re-binding either gesture
        /// re-words it.</summary>
        [Fact]
        public void ThePickUpAnnouncementNamesBothWaysOutOfTheCarry()
        {
            CarryState carry = new CarryState();
            try
            {
                NodeHints.Chord = (action, index) =>
                    action == CarryState.DropAction ? "Enter" : "Backspace";

                CarryOutcome outcome = CarryActions.Press(
                    Source(new object(), "Explorer"),
                    carry,
                    "galaxy"
                );

                Assert.Equal(
                    "Dragging Explorer. Enter to drop, Backspace to cancel.",
                    outcome.Speech
                );
            }
            finally
            {
                NodeHints.Reset();
            }
        }

        /// <summary>The two DERIVED hints: what this control would hand over while nothing is held, and
        /// where what IS held can go. Never both, and neither without a renderer.</summary>
        [Fact]
        public void TheDerivedHintsFollowWhatIsHeld()
        {
            CarryState carry = new CarryState();
            NodeVtable source = Vt("Slot");
            source.OnPickUp = () => new CarryItem(new object(), "Imperials x 3", Ship, 3);
            NodeVtable target = Target(DropResult.Done());
            try
            {
                NodeHints.Chord = (action, index) =>
                    action == CarryState.DropAction ? "Enter" : "Space";

                List<string> lines = new List<string>();
                carry.HintLines(lines, source);
                Assert.Equal(new[] { "Space to drag Imperials x 3." }, lines);

                lines.Clear();
                carry.HintLines(lines, target);
                Assert.Empty(lines);

                CarryActions.Press(source, carry, "system");

                lines.Clear();
                carry.HintLines(lines, source);
                Assert.Empty(lines);

                lines.Clear();
                carry.HintLines(lines, target);
                Assert.Equal(new[] { "Enter to drop Imperials x 3." }, lines);
            }
            finally
            {
                NodeHints.Reset();
            }
        }

        /// <summary>A target whose own test says no offers no drop hint either - the hint is gated on
        /// the same <see cref="CarryState.Takes"/> the "drop target" word is, so it inherits every
        /// screen's <see cref="NodeVtable.DropAccepts"/> for free.</summary>
        [Fact]
        public void ATargetThatRefusesThisCargoOffersNoDropHint()
        {
            CarryState carry = new CarryState();
            NodeVtable target = Target(DropResult.Done());
            target.DropAccepts = held => false;
            try
            {
                NodeHints.Chord = (action, index) => "Enter";
                CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

                List<string> lines = new List<string>();
                carry.HintLines(lines, target);

                Assert.Empty(lines);
            }
            finally
            {
                NodeHints.Reset();
            }
        }

        /// <summary>
        /// Whether a drag says a COUNT is a fact about the cargo, decided by the source when it
        /// composes the name (owner ruling 2026-08-29). Population is measured in units, so it states
        /// the count every time, one included; a module, a ship, a queue line and a tactic card are
        /// single things and must never read "x 1". Both shapes go through the very same phrases,
        /// which is what this holds: the phrases interpolate the name and add nothing of their own.
        /// </summary>
        [Fact]
        public void OnlyCargoMeasuredInUnitsStatesACountInTheDragPhrases()
        {
            try
            {
                NodeHints.Chord = (action, index) =>
                    action == CarryState.DropAction
                        ? "Enter"
                        : (action == CarryState.CancelAction ? "Escape" : "Space");

                // What PopulationMoves.Name composes for a single unit: the count, always.
                CarryState units = new CarryState();
                NodeVtable people = Vt("Slot");
                people.OnPickUp = () =>
                    new CarryItem(new object(), "Imperials x 1", Ship, 1);

                Assert.Equal(
                    "Dragging Imperials x 1. Enter to drop, Escape to cancel.",
                    CarryActions.Press(people, units, "system").Speech
                );

                List<string> lines = new List<string>();
                new CarryState().HintLines(lines, people);
                Assert.Equal(new[] { "Space to drag Imperials x 1." }, lines);

                // And a single thing keeps its plain name through the identical phrases.
                CarryState single = new CarryState();
                NodeVtable module = Source(new object(), "Basic Warp Drive");

                Assert.Equal(
                    "Dragging Basic Warp Drive. Enter to drop, Escape to cancel.",
                    CarryActions.Press(module, single, "shipdesign").Speech
                );

                lines.Clear();
                new CarryState().HintLines(lines, module);
                Assert.Equal(new[] { "Space to drag Basic Warp Drive." }, lines);
            }
            finally
            {
                NodeHints.Reset();
            }
        }

        /// <summary>With no renderer at all neither hint exists, and the pick-up falls back to naming
        /// what is held: a sentence promising a chord nobody can spell says nothing.</summary>
        [Fact]
        public void WithNoChordRendererThereAreNoHintsAndNoPromisedKeys()
        {
            CarryState carry = new CarryState();
            NodeVtable source = Source(new object(), "Explorer");

            List<string> lines = new List<string>();
            carry.HintLines(lines, source);
            Assert.Empty(lines);

            Assert.Equal("Dragging Explorer", CarryActions.Press(source, carry, "galaxy").Speech);
        }

        [Fact]
        public void ADropGoesThroughTheTargetAndEndsTheCarry()
        {
            CarryState carry = new CarryState();
            object explorer = new object();
            CarryActions.Press(Source(explorer, "Explorer"), carry, "galaxy");

            CarryItem dropped = null;
            NodeVtable fleet = Vt("Second fleet");
            fleet.DropKind = Ship;
            fleet.OnDrop = item =>
            {
                dropped = item;
                return DropResult.Done("Explorer joined Second fleet");
            };

            CarryOutcome outcome = CarryActions.Activate(fleet, carry);

            Assert.Same(explorer, dropped.Cargo);
            Assert.Equal("Explorer joined Second fleet", outcome.Speech);
            Assert.False(carry.IsCarrying);
        }

        [Fact]
        public void ADropTheTargetSaysNothingAboutStillReportsItself()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Activate(Target(DropResult.Done()), carry);

            Assert.Equal("Dropped Explorer", outcome.Speech);
            Assert.False(carry.IsCarrying);
        }

        [Fact]
        public void ARefusedDropSpeaksTheGamesReasonAndKeepsCarrying()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Activate(
                Target(DropResult.Refused("The fleet is full")),
                carry
            );

            Assert.True(outcome.Handled);
            Assert.Equal("The fleet is full", outcome.Speech);
            Assert.True(carry.IsCarrying);
            Assert.Equal("Explorer", carry.Held.Name);
        }

        [Fact]
        public void AWordlessRefusalStillSaysTheDropDidNotHappen()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Activate(Target(DropResult.Refused()), carry);

            Assert.Equal("Explorer cannot go there", outcome.Speech);
            Assert.True(carry.IsCarrying);
        }

        [Fact]
        public void ATargetOnlyTakesItsOwnKindOfCargo()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            bool asked = false;
            NodeVtable planet = Vt("Homeworld");
            planet.DropKind = "population";
            planet.OnDrop = item =>
            {
                asked = true;
                return DropResult.Done();
            };

            CarryOutcome outcome = CarryActions.Activate(planet, carry);

            // Not ours: the control does its own click, exactly as it would with nothing held.
            Assert.False(asked);
            Assert.False(outcome.Handled);
            Assert.True(carry.IsCarrying);
        }

        [Fact]
        public void TheCarryKeyNeverDropsEvenOnATargetItCouldDropOn()
        {
            CarryState carry = new CarryState();
            bool asked = false;
            NodeVtable fleet = Vt("Second fleet");
            fleet.DropKind = Ship;
            fleet.OnDrop = item =>
            {
                asked = true;
                return DropResult.Done();
            };
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Press(fleet, carry, "galaxy");

            // Claimed - the carry is the mode - and silent, with the thing still held: dropping is
            // Enter's job, and Space on a target that offers nothing to pick up does nothing at all.
            Assert.False(asked);
            Assert.True(outcome.Handled);
            Assert.Null(outcome.Speech);
            Assert.True(carry.IsCarrying);
        }

        [Fact]
        public void ATargetThatIsAlsoASourceHandsOverItsOwnOnTheCarryKeyAndTakesTheDropOnEnter()
        {
            CarryState carry = new CarryState();
            object hunter = new object();
            NodeVtable both = Target(DropResult.Done());
            both.OnPickUp = () => new CarryItem(hunter, "Hunter", Ship);
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            Assert.Equal("Dragging Hunter", CarryActions.Press(both, carry, "galaxy").Speech);
            Assert.Same(hunter, carry.Held.Cargo);
            Assert.Equal("Dropped Hunter", CarryActions.Activate(both, carry).Speech);
        }

        [Fact]
        public void AQueueLineSaysWhichItemMovedAndWhereItLanded()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Applied Casimir Effect"), carry, "research");

            CarryOutcome outcome = CarryActions.Activate(
                Target(
                    DropResult.Done(
                        ModStrings.Format(
                            ModStrings.DragMovedToPosition,
                            "Applied Casimir Effect",
                            2
                        )
                    )
                ),
                carry
            );

            // The position the player will hear the line read back with, not a zero-based index.
            Assert.Equal("Moved Applied Casimir Effect to position 2", outcome.Speech);
            Assert.False(carry.IsCarrying);
        }

        [Fact]
        public void ActivationIsNeverOursWhileNothingIsHeld()
        {
            CarryState carry = new CarryState();

            Assert.False(CarryActions.Activate(Target(DropResult.Done()), carry).Handled);
            Assert.False(CarryActions.Activate(Vt("Button"), null).Handled);
        }

        [Fact]
        public void ActivatingSomethingElseLeavesTheCarryAlone()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            CarryOutcome outcome = CarryActions.Activate(Vt("Button"), carry);

            // Not ours, so the button does its own click - and the carry survives it, which is what
            // lets the player walk and use a page while holding something.
            Assert.False(outcome.Handled);
            Assert.Null(outcome.Speech);
            Assert.True(carry.IsCarrying);
        }

        [Fact]
        public void TheBackKeyIsOnlyOursWhileSomethingIsHeld()
        {
            CarryState carry = new CarryState();
            Assert.False(CarryActions.Cancel(carry).Handled);

            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");
            CarryOutcome outcome = CarryActions.Cancel(carry);

            Assert.True(outcome.Handled);
            Assert.Equal("Cancelled drag", outcome.Speech);
            Assert.False(carry.IsCarrying);
        }

        [Fact]
        public void TheLifecycleIsToldOfEveryPickUpAndOfEveryEndingThePlayerPerformed()
        {
            CarryState carry = new CarryState();
            List<string> heard = new List<string>();
            carry.Started = item => heard.Add("started " + item.Name);
            carry.Ended = item => heard.Add("ended " + item.Name);

            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");
            CarryActions.Activate(Target(DropResult.Refused("Full")), carry);
            CarryActions.Press(Source(new object(), "Hunter"), carry, "galaxy");
            CarryActions.Cancel(carry);
            CarryActions.Press(Source(new object(), "Scout"), carry, "galaxy");
            CarryActions.Activate(Target(DropResult.Done()), carry);

            Assert.Equal(
                new[]
                {
                    "started Explorer",
                    "ended Explorer",
                    "started Hunter",
                    "ended Hunter",
                    "started Scout",
                    "ended Scout",
                },
                heard
            );

            // A carry the player never ended - they walked off the page - is not one of its endings,
            // and an observer that throws costs its own effect and nothing else.
            carry.Ended = item =>
            {
                throw new InvalidOperationException("deaf");
            };
            CarryActions.Press(Source(new object(), "Ranger"), carry, "galaxy");
            carry.ScreenChanged(false);
            Assert.False(carry.IsCarrying);

            CarryActions.Press(Source(new object(), "Ranger"), carry, "galaxy");
            Assert.True(CarryActions.Cancel(carry).Handled);
        }

        [Fact]
        public void ASourceThatHandsOverNothingStartsNoCarryAndTellsNobody()
        {
            CarryState carry = new CarryState();
            int started = 0;
            carry.Started = item => started++;
            NodeVtable empty = Vt("Empty slot");
            empty.OnPickUp = () => null;

            Assert.True(CarryActions.Press(empty, carry, "galaxy").Handled);
            Assert.Equal(0, started);
        }

        [Fact]
        public void LeavingThePageDropsTheCarryButAMenuOverItDoesNot()
        {
            CarryState carry = new CarryState();
            CarryActions.Press(Source(new object(), "Explorer"), carry, "galaxy");

            carry.ScreenChanged(true);
            Assert.True(carry.IsCarrying);

            carry.ScreenChanged(false);
            Assert.False(carry.IsCarrying);
            Assert.Null(carry.Owner);
        }

        [Fact]
        public void ATargetSaysSoOnlyWhileSomethingItTakesIsBeingCarried()
        {
            CarryState carry = new CarryState();
            NodeAnnouncement part = carry.DropTargetPart(Target(DropResult.Done()));

            Assert.True(part.Live);
            Assert.Null(part.Text());

            carry.PickUp(new CarryItem(new object(), "Explorer", "population"), "galaxy");
            Assert.Null(part.Text());

            carry.PickUp(new CarryItem(new object(), "Explorer", Ship), "galaxy");
            Assert.Equal("drop target", part.Text());
        }

        [Fact]
        public void ATargetThatWouldRefuseThisCargoSaysNothingButStillRefusesInTheGamesWords()
        {
            CarryState carry = new CarryState();
            NodeVtable locked = Target(DropResult.Refused("This tactic is locked"));
            locked.DropAccepts = held => false;
            NodeAnnouncement part = carry.DropTargetPart(locked);
            carry.PickUp(new CarryItem(new object(), "Explorer", Ship), "tactics");

            Assert.Null(part.Text());

            // The drop is still the target's, so a player who presses anyway hears the game's reason
            // rather than the control's own click.
            CarryOutcome outcome = CarryActions.Activate(locked, carry);
            Assert.True(outcome.Handled);
            Assert.Equal("This tactic is locked", outcome.Speech);
        }

        [Fact]
        public void ASourceSaysDraggableOnlyWhileNothingIsCarried()
        {
            CarryState carry = new CarryState();
            NodeAnnouncement part = carry.DraggablePart(Source(new object(), "Explorer"));

            // Not live: the word is composed with the readout, and one appearing on its own after a
            // cancelled drag would be noise on top of the gesture that already said what happened.
            Assert.False(part.Live);
            Assert.Equal("draggable", part.Text());

            carry.PickUp(new CarryItem(new object(), "Hunter", Ship), "fleets");
            Assert.Null(part.Text());
        }

        [Fact]
        public void AControlWithNothingToGiveDoesNotSayDraggable()
        {
            CarryState carry = new CarryState();
            NodeVtable empty = Vt("Empty slot");
            empty.OnPickUp = () => null;

            Assert.Null(carry.DraggablePart(empty).Text());
        }

        [Fact]
        public void AControlThatIsNeitherSourceNorTargetSaysNothingAboutDragging()
        {
            CarryState carry = new CarryState();

            Assert.Null(carry.DraggablePart(Vt("Button")));
            Assert.Null(carry.DropTargetPart(Vt("Button")));
        }

        [Fact]
        public void NothingIsCarriedWithoutAState()
        {
            Assert.False(CarryActions.Claims(Vt("Button"), null));
            Assert.False(CarryActions.Press(Vt("Button"), null, "galaxy").Handled);
            Assert.False(CarryActions.Cancel(null).Handled);
        }

        [Fact]
        public void EveryCarryPhraseIsAShippedString()
        {
            string template;
            Assert.True(ModStrings.TryGetDefault(ModStrings.DragStarted, out template));
            Assert.True(ModStrings.TryGetDefault(ModStrings.DragStartedPlain, out template));
            Assert.True(ModStrings.TryGetDefault(ModStrings.DragHint, out template));
            Assert.True(ModStrings.TryGetDefault(ModStrings.DragDropHint, out template));
            Assert.True(ModStrings.TryGetDefault(ModStrings.DragDropped, out template));
            Assert.True(ModStrings.TryGetDefault(ModStrings.DragDropRefused, out template));
            Assert.True(ModStrings.TryGetDefault(ModStrings.DragCancelled, out template));
            Assert.True(ModStrings.TryGetDefault(ModStrings.DragMovedToPosition, out template));
            Assert.True(ModStrings.TryGetDefault(ModStrings.DragDropTarget, out template));
            Assert.True(ModStrings.TryGetDefault(ModStrings.DragDraggable, out template));
            Assert.True(ModStrings.TryGetDefault(ModStrings.NavNotSelected, out template));
        }
    }
}

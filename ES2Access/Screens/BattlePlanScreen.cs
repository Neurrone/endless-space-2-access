using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The battle plans the setup popup's carousel steps through, as the LIST a droplist opens.
    ///
    /// The game draws one card with an arrow either side of it, and pressing an arrow does not merely
    /// show the next plan - it CHOOSES it (<c>EncounterGroupSetup.PlayDefinition</c> moves with the
    /// card, and there is no confirm step anywhere in the popup). Declared as a band of rows, that made
    /// walking Up through the popup re-choose every plan the cursor crossed, silently
    /// (owner-reported 2026-08-29). So the popup keeps ONE closed row - the plan in force
    /// (<c>BattleNotifications.Plan</c>) - and this screen is what Enter on it opens: the same
    /// semantics as the mod's own options droplist (<see cref="DropListScreen"/>), for a carousel that
    /// has no <c>AgeControlDropList</c> behind it to hand that screen.
    ///
    /// A MOD-OWNED CHILD SCREEN (<see cref="Screen.PushChild"/>), like the chat page: nothing in the
    /// game says a list is open, the covered popup keeps its own cursor so closing puts the player back
    /// on the row they opened, and Escape is the mod's because the game cannot close a surface it knows
    /// nothing about. It has no layer for the same reason a child screen never does - the manager
    /// focuses the deepest child of the top screen.
    ///
    /// BROWSING TURNS THE CARD, which is the whole point: a sighted watcher follows the choice being
    /// considered, and each row's own tooltip and its nested entries - the family sentence, the three
    /// flotilla ranges, "You chose this in X% of battles" - are read off the card the game has just
    /// drawn, so they are true of the row saying them rather than of whatever was in force. Enter
    /// ACCEPTS what is showing (the game already has it) and Escape puts back the plan that was in
    /// force when the list opened, so browsing and leaving changes nothing.
    ///
    /// At most one row is ever expanded, and it is the row the cursor is inside
    /// (<see cref="Build"/>): the nested entries all read the ONE drawn card, so a second open row
    /// would be reading this row's card under that row's name - which is exactly the defect the
    /// one-row-plus-chooser shape exists to remove.
    /// </summary>
    public sealed class BattlePlanScreen : Screen
    {
        private static readonly object Stop = "battle-plan:list";

        /// <summary>A path key, so the nested entries below a row (<c>battle-plan/1/tooltip/0</c>)
        /// name the row they belong to and <see cref="UnderCursor"/> can read it back.</summary>
        private const string RowKey = "battle-plan/";

        private readonly BattleSetupNotificationWindow _window;

        /// <summary>The plan that was in force when the list opened - what Escape puts back, and the
        /// row that says "selected" while the player browses past it.</summary>
        private int _opened = -1;

        private BattlePlanScreen(BattleSetupNotificationWindow window)
        {
            _window = window;
        }

        /// <summary>Open the list over whatever page the player is on - which, since only the closed
        /// row's Enter reaches here, is the notification popup that drew it.</summary>
        public static void Open(BattleSetupNotificationWindow window)
        {
            if (window == null)
            {
                return;
            }

            ScreenManager screens = ModEntry.Screens;
            Screen current = screens == null ? null : screens.Current;
            if (current != null)
            {
                current.PushChild(new BattlePlanScreen(window));
            }
        }

        public override string Key
        {
            get { return "screen.battle-plan"; }
        }

        /// <summary>Never polled - a child screen is pushed and popped - but answered honestly, and
        /// asked by <see cref="OnUpdate"/>: the game switching the arrows off (this side has committed
        /// to the fight) leaves nothing here to choose.</summary>
        public override bool IsActive()
        {
            return Live();
        }

        /// <summary>A choice being made: the only things on offer are the plans and leaving.</summary>
        public override bool AnswersOnly
        {
            get { return true; }
        }

        /// <summary>The carousel's own title, in the game's words - so opening the list reads "Battle
        /// Plan" and then the plan it is currently on.</summary>
        public override string ScreenName
        {
            get { return BattlePlans.PlanTitle(); }
        }

        /// <summary>A surface the mod put up and the game cannot close.</summary>
        public override bool ConsumesBack
        {
            get { return true; }
        }

        /// <summary>Escape cancels: the card goes back to the plan that was in force when the list
        /// opened, and the player is handed back to the closed row, which then says it.</summary>
        public override bool Back()
        {
            BattlePlans.Turn(_window, _opened);
            CloseSelf();
            return true;
        }

        public override void OnPush()
        {
            _opened = BattlePlans.CurrentPlay(_window);
        }

        public override void OnPop()
        {
            PointerFocus.Release();
        }

        /// <summary>The game taking the choice away - the fight started, the popup answered - closes
        /// the list rather than leaving the player in one that can no longer act.</summary>
        public override void OnUpdate()
        {
            if (!Live())
            {
                CloseSelf();
            }
        }

        public override void Build(GraphBuilder builder)
        {
            BattleSetupNotificationWindow window = _window;
            BattlePlayCard card = BattlePlans.PlanCard(window);
            AgeTransform carrier = card == null ? null : card.AgeTransform;
            AgeTooltip tooltip = card == null ? null : card.Tooltip;
            // A window whose plan list will not answer still draws a card, and the one row that says
            // what the fleet is fighting under is worth more than an empty list.
            int count = Math.Max(1, BattlePlans.PlayCount(window));
            int under = UnderCursor();
            int chosen = _opened;

            builder.BeginStop(Stop);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                string key = RowKey + i;
                ControlId id = ControlId.Structural(key);
                if (i != under)
                {
                    // One card, one open row: see the class comment.
                    Shut(builder, id);
                }

                NodeVtable vtable = GraphNodes.Choice(
                    () =>
                    {
                        Show(window, index);
                        return BattlePlans.PlayName(window, index, card);
                    },
                    () => index == chosen,
                    () => Accept(index),
                    () => BattlePlans.Steppable(window),
                    null,
                    tooltip
                );
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(
                        () => BattlePlans.PlanEffects(window, index, card),
                        false
                    )
                );

                // One drawn viewer, N paged contents: the card is what every row stands on, and which
                // plan a row means is its index.
                TooltipChildren.Declare(
                    builder,
                    BattlePlans.Drawn(id, vtable, carrier),
                    key,
                    BattlePlans.PlanDossiers(card, () => Show(window, index))
                );
            }
        }

        /// <summary>Accept what the card is showing. The game has had this plan since the cursor
        /// arrived on the row, so there is nothing to commit - the turn below is the idempotent
        /// belt-and-braces for a row activated from anywhere else.</summary>
        private void Accept(int index)
        {
            BattlePlans.Turn(_window, index);
            CloseSelf();
        }

        /// <summary>Turn the card to this row's plan, if the cursor is standing in this row.
        ///
        /// Resolved where the row's - or one of its nested entries' - NAME is read, because that is the
        /// only thing that runs between the cursor arriving and the landing being spoken. The focus
        /// guard is what keeps a graph dump or a type-ahead pass over the list from choosing plans
        /// nobody asked for.</summary>
        private static void Show(BattleSetupNotificationWindow window, int index)
        {
            if (UnderCursor() == index)
            {
                BattlePlans.Turn(window, index);
            }
        }

        /// <summary>Which plan's row the cursor is inside - the row itself or any of its nested
        /// entries - or -1 for anywhere else.</summary>
        private static int UnderCursor()
        {
            return ModEntry.Navigator == null ? -1 : ModEntry.Navigator.FocusedIndex(RowKey);
        }

        /// <summary>Take a row out of the persistent expansion set, so the next build declares no
        /// children under it. The engine's own bookkeeping is what put it there and what puts it back
        /// when the player opens it again; this only ever REMOVES.</summary>
        private static void Shut(GraphBuilder builder, ControlId id)
        {
            HashSet<ControlId> expansion = builder.Expansion;
            if (expansion != null)
            {
                expansion.Remove(id);
            }
        }

        /// <summary>Whether there is still a choice to make here: the carousel drawn, and the game
        /// still offering its arrows.</summary>
        private bool Live()
        {
            try
            {
                // Input routing, not node existence: whether this screen is still a place the player
                // is standing in. A carousel the window has stopped drawing is a list nobody can be
                // in, and the rows below would have no card to read.
                return _window != null
                    && AgeWidgets.Visible(_window.PlayGroup)
                    && BattlePlans.Steppable(_window);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

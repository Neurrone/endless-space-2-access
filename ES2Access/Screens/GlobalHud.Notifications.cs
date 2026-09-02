using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

namespace ES2Access.Screens
{
    /// <summary>Everything the game is waiting to tell the player, as a list they can walk.</summary>
    public sealed partial class GlobalHud
    {
        // ---- notifications ----

        /// <summary>
        /// Everything the game is waiting to tell the player, as a list they can walk instead of a
        /// column of icons they would have to click. Enter opens one - the popup that appears is a
        /// screen of ours and takes over from here - and Backslash throws it away, because throwing
        /// one away is the game's OWN right click on the icon
        /// (<c>NotificationItemsWindow.HandleInput</c> :90-101). With nothing
        /// waiting the game shows an empty corner, so this stop is not there at all.
        ///
        /// What a stop here holds is what the strip holds: an icon and, on hovering it, its title.
        /// Not the notification's description - the game does not show that until the popup is opened,
        /// and opening it is what this stop's Enter is for. Putting the whole text in the buffer here
        /// made the strip a second place to read the message, one that answered before the player had
        /// asked and disagreed with the screen as drawn.
        ///
        /// The MOD's own notifications share the same list but are not drawn on the strip at all
        /// (<see cref="NotificationStrip"/>), so they are left out of here and read in
        /// <see cref="TurnLog"/> instead: this stop is what the game is showing, and that one is the
        /// log of what the game never showed.
        ///
        /// The LAST entry is "throw them all away" (<see cref="DismissAllNotifications"/>) - the
        /// gesture the game offers only as an Alt+right click on the bare triangle behind the icons,
        /// over the notifications THIS stop holds and no others: the Turn log has a button of its own
        /// and neither reaches into the other's list (owner ruling 2026-08-24). There is no key for
        /// it: it is a button, reached with the arrows and pressed with Enter (owner ruling
        /// 2026-08-23).
        /// </summary>
        public void Notifications(GraphBuilder builder)
        {
            builder.BeginStop(NotificationStop);
            // The strip is a column of bare icons with no caption over it, so the word is the mod's.
            // Popped in a finally because the walk below has an early return and a catch of its own,
            // and a level left open would take every stop declared after this one with it.
            builder.PushContext(ChordNames.Label(ModStrings.Get(ModStrings.HudNotificationsPanel), UiActions.FocusNotifications, 0));
            int count = 0;
            try
            {
                IGuiNotificationService service = Gui.GuiNotificationService;
                if (service == null)
                {
                    return;
                }

                NotificationItem[] items = NotificationItems();
                foreach (GuiNotification notification in service.GetPlayerEmpireGuiNotifications())
                {
                    if (Mine(notification) != null)
                    {
                        continue;
                    }

                    GuiNotification it = notification;
                    NodeVtable vtable = GraphNodes.Button(
                        () => AgeText.Clean(it.GetTitle()),
                        () => Open(it),
                        null,
                        null
                    );
                    vtable.OnContextual = () => Dismiss(it);
                    GoToLocation(vtable, it);
                    // The strip is bare icons: nothing on it says the row can be thrown away, and the
                    // game's own right click is the only way to do it without opening the popup first.
                    NodeHints.Add(vtable, ModStrings.HintDismiss, UiActions.Contextual);
                    vtable.Sections = GraphNodes.Sections(GraphNodes.TooltipDetails(IconTooltip(it, items)), null);
                    // Synthesized from the game's own notification list, not read off a widget: the
                    // strip's icons are pooled and the walk holds the NOTIFICATION, so there is
                    // nothing here whose paint state could vouch for the row. The enumeration is
                    // where the honesty lives - the service lists the notifications that exist.
                    builder.AddItem(
                        Nodes.Synthetic(ControlId.For(it, "hud:notification/" + count), vtable)
                    );
                    count++;
                }

                if (count > 0)
                {
                    // Keyed on the game's OWN control for the gesture - the bare triangle behind the
                    // icons, which the prefab names and the window never binds - so the cursor rides
                    // it and the coverage audit finds the node standing on it rather than reporting a
                    // drawn control nothing declares.
                    AgeTransform triangle = CloseAllTriangle();
                    NodeVtable dismissAll = GraphNodes.Button(
                        () => ModStrings.Get(ModStrings.HudDismissAllNotifications),
                        DismissAllNotifications
                    );
                    // Synthetic where the strip draws no triangle to stand on: the row is then the mod's
                    // own, over the notification list the service keeps.
                    builder.AddItem(
                        triangle == null
                            ? (NodeDeclaration)Nodes.Synthetic(
                                ControlId.Structural("hud:notification/dismiss-all"),
                                dismissAll
                            )
                            : Nodes.Drawn(
                                ControlId.For(triangle, "hud:notification/dismiss-all"),
                                dismissAll,
                                triangle
                            )
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the notifications threw: " + e);
            }
            finally
            {
                builder.PopContext();
            }
        }

        /// <summary>
        /// GO TO WHERE THIS HAPPENED, on a row whose popup is not open - a notification icon on the
        /// strip, a line of the turn log.
        ///
        /// Offered exactly where the popup would DRAW a show-location button
        /// (<see cref="NotificationScreen.DrawsShowLocation"/>): the game marks that button visible
        /// from the notification's own <c>HasLocation</c> without asking whether the prefab laid one
        /// out, and forty-one of the sixty-nine did not (ES2 facts). Offering the key where the mouse
        /// has no button would be an affordance the sighted player cannot see - and, worse, one whose
        /// handler moves nothing.
        ///
        /// What it DOES is the button's own handler minus its last line, which toggles the popup open
        /// (<see cref="NotificationScreen.GoToLocation"/>): from a closed row that toggle would open
        /// the popup instead of going anywhere.
        ///
        /// The hint comes FIRST, before "to dismiss": going somewhere is what the row is usually
        /// pressed for, and throwing it away is what is done afterwards.
        /// </summary>
        private static void GoToLocation(NodeVtable vtable, GuiNotification notification)
        {
            GuiNotification it = notification;
            if (!NotificationScreen.DrawsShowLocation(it))
            {
                return;
            }

            vtable.OnGoTo = () => NotificationScreen.GoToLocation(it);
            NodeHints.Add(
                vtable,
                ModStrings.HintGoToLocation,
                UiActions.GoToLocation,
                0,
                () => NotificationScreen.DrawsShowLocation(it)
            );
        }

        /// <summary>
        /// The mod's own notifications - the things that happened this turn and the last few that the
        /// game itself never mentions: a system revealed, a fleet arrived, somebody else's fleet
        /// sighted or lost. They live in the game's list beside the game's own and behave exactly like
        /// them (Enter opens the same popup, Backslash throws the same one away), but the game draws
        /// none of them on its strip, so they are read here rather than beside the icons the player
        /// can see.
        ///
        /// Grouped under the turn each one happened on, NEWEST TURN FIRST, because the news a player
        /// walks a log for is the news that has just landed; within a turn they keep the order they
        /// arrived in. The turn is the one stamped when the notification was made
        /// (<see cref="ModNotification.Turn"/>), so a log spanning the five turns one lives for says
        /// which day each line is from without any line having to say it itself. Each turn is a REGION
        /// as well as a spoken level, so Alt+Up/Down steps a turn at a time.
        ///
        /// No tooltip section, unlike the stop above: the strip binds a mod item's tooltip to its own
        /// title (and then deactivates the item), so a section here would be the row's own words a
        /// second time - measured 2026-08-20, the game notifications' buffers hold exactly their title
        /// for the same reason.
        ///
        /// With nothing logged the stop is not there at all, which is the rule every stop on this HUD
        /// follows. It is the one place that rule is arguable - a sighted player cannot glance at this
        /// list, because there is nothing drawn to glance at - so it is on the owner's list to settle.
        ///
        /// The LAST entry throws the whole log away (<see cref="DismissAllLogged"/>), in a region of
        /// its own after the turns; because the stop only exists while the log holds something, that
        /// button is never offered over an empty list (owner ruling 2026-08-23).
        /// </summary>
        public void TurnLog(GraphBuilder builder)
        {
            List<ModNotification> logged = Logged();
            if (logged.Count == 0)
            {
                return;
            }

            List<int> turns = new List<int>();
            for (int i = 0; i < logged.Count; i++)
            {
                if (!turns.Contains(logged[i].Turn))
                {
                    turns.Add(logged[i].Turn);
                }
            }

            turns.Sort();
            turns.Reverse();

            builder.BeginStop(TurnLogStop);
            builder.PushContext(ChordNames.Label(ModStrings.Get(ModStrings.HudTurnLogPanel), UiActions.FocusTurnLog, 0));
            try
            {
                for (int t = 0; t < turns.Count; t++)
                {
                    int turn = turns[t];
                    builder.SetRegion("hud:turn-log/turn/" + turn);
                    builder.PushContext(ModStrings.Format(ModStrings.HudTurnLogTurn, turn));
                    try
                    {
                        int within = 0;
                        for (int i = 0; i < logged.Count; i++)
                        {
                            ModNotification it = logged[i];
                            if (it.Turn != turn)
                            {
                                continue;
                            }

                            NodeVtable vtable = GraphNodes.Button(
                                () => AgeText.Clean(it.GetTitle()),
                                () => Open(it)
                            );
                            vtable.OnContextual = () => Dismiss(it);
                            GoToLocation(vtable, it);
                            NodeHints.Add(vtable, ModStrings.HintDismiss, UiActions.Contextual);
                            // Synthetic: the turn log is the mod's own record of notifications that have
                            // been and gone - the HUD draws nothing for a dismissed one.
                            builder.AddItem(Nodes.Synthetic(
                                ControlId.For(it, "hud:turn-log/" + turn + "/" + within),
                                vtable
                            ));
                            within++;
                        }
                    }
                    finally
                    {
                        builder.PopContext();
                    }
                }

                // Throw the whole log away, in a region of its own so no turn owns it and Alt+Down
                // from the last turn reaches it. Declared unconditionally here: the stop does not
                // exist at all while the log is empty (above), so there is never a button offering to
                // clear nothing.
                builder.SetRegion("hud:turn-log/dismiss-all");
                // Synthetic: mod-authored - a command over the mod's own log.
                builder.AddItem(Nodes.Synthetic(
                    ControlId.Structural("hud:turn-log/dismiss-all"),
                    GraphNodes.Button(
                        () => ModStrings.Get(ModStrings.HudDismissAllTurnLog),
                        DismissAllLogged
                    )
                ));
            }
            finally
            {
                builder.PopContext();
            }
        }

        /// <summary>The widget the game hangs its close-all on: <c>BaseTriangleBackground</c>, an
        /// <c>AgeControlButton</c> whose only wiring is <c>OnRightClickMethod=OnCloseAllCb</c>. The
        /// window exposes no field for it, so it is found by the name the prefab gives it - which is
        /// unique under that window (measured 2026-08-23). It carries no tooltip of any kind, which is
        /// why the button's name is the mod's.</summary>
        private static AgeTransform CloseAllTriangle()
        {
            try
            {
                NotificationItemsWindow window = GameWindows.Of<NotificationItemsWindow>();
                return window == null
                    ? null
                    : AgeWidgets.ChildNamed(window.AgeTransform, "BaseTriangleBackground", 3);
            }
            catch (Exception e)
            {
                Log.Warn("hud: finding the close-all triangle threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Throw away everything the GAME is waiting to tell the player, and nothing of the mod's:
        /// every notification on the strip dismissed one by one, which is the same discard Backslash
        /// makes on the row it is standing on (<see cref="Dismiss"/>).
        ///
        /// NOT the game's own <c>DismissAllGuiNotifications</c> - the call its icon strip makes for an
        /// Alt+right click on the triangle behind the icons
        /// (<c>NotificationItemsWindow.OnCloseAllCb</c> :237-245). The game keeps ONE list and the
        /// mod's own notifications live in it, so that call takes the Turn log with it. Each of these
        /// two buttons clears its own list and leaves the other standing (owner ruling 2026-08-24),
        /// and which list a notification is in is the one question <see cref="Mine"/> answers - for
        /// the strip stop, for the Turn log and for both buttons - so no two of them can disagree and
        /// nothing falls between them.
        ///
        /// That handler's other branch, Shift, only HIDES the popups that happen to be open and
        /// dismisses nothing; the mod offers the dismissing one, because that is what a strip with no
        /// popup up can be asked for.
        ///
        /// Walked over the split's own copy, since dismissing removes each one from the list it reads.
        /// </summary>
        private static void DismissAllNotifications()
        {
            try
            {
                IGuiNotificationService service = Gui.GuiNotificationService;
                if (service == null)
                {
                    return;
                }

                List<GuiNotification> theirs = OwnedNotifications.Theirs(
                    service.GetPlayerEmpireGuiNotifications(),
                    Split
                );
                for (int i = 0; i < theirs.Count; i++)
                {
                    Dismiss(theirs[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: dismissing every notification threw: " + e);
            }
        }

        /// <summary>Throw away every line of the Turn log and nothing else - the same discard Backslash
        /// makes on one row (<see cref="Dismiss"/>), over the mod's own notifications only, so the
        /// game's icon strip is left exactly as it was. Walked over a copy, since dismissing removes
        /// each one from the list this reads.</summary>
        private static void DismissAllLogged()
        {
            List<ModNotification> logged = Logged();
            for (int i = 0; i < logged.Count; i++)
            {
                Dismiss(logged[i]);
            }
        }

        /// <summary>The mod's own notification, where this notification is one of the mod's - the ONE
        /// test behind the split between the two lists the player walks. The strip stop leaves these
        /// out, the Turn log holds exactly these, each dismiss-all clears exactly one side of it
        /// (<see cref="OwnedNotifications"/>), and a minimized popup hands back to the stop this
        /// answers for (<c>NotificationScreen.ListOf</c>). Five readings, one test.</summary>
        public static ModNotification Mine(GuiNotification notification)
        {
            return notification as ModNotification;
        }

        /// <summary>The same test as a converter, held once so that splitting a list allocates
        /// nothing beyond the list it answers with.</summary>
        private static readonly Converter<GuiNotification, ModNotification> Split = Mine;

        private static readonly List<ModNotification> NoneLogged = new List<ModNotification>();

        /// <summary>Every mod notification standing in the player's list, in the list's own order. The
        /// same list the stop above walks - one list is what makes the popup's Previous/Next cross
        /// between the game's news and the mod's.</summary>
        private static List<ModNotification> Logged()
        {
            try
            {
                IGuiNotificationService service = Gui.GuiNotificationService;
                return service == null
                    ? NoneLogged
                    : OwnedNotifications.Mine(service.GetPlayerEmpireGuiNotifications(), Split);
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the turn log threw: " + e);
                return NoneLogged;
            }
        }

        /// <summary>The tooltip the strip hangs on this notification's icon - read from the icon
        /// rather than composed from the notification, so it stays whatever the game decides to put
        /// there. Today the game binds it to the notification's title, and the buffer drops a first
        /// line that only repeats the control's name, so the usual result is a buffer holding exactly
        /// the one line the strip shows.</summary>
        private static AgeTooltip IconTooltip(GuiNotification notification, NotificationItem[] items)
        {
            try
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (ReferenceEquals(items[i].GuiNotification, notification))
                    {
                        return items[i].Tootlip;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: matching a notification to its icon threw: " + e);
            }

            return null;
        }

        private static readonly NotificationItem[] NoItems = new NotificationItem[0];

        private static NotificationItem[] NotificationItems()
        {
            try
            {
                NotificationItemsWindow window = GameWindows.Of<NotificationItemsWindow>();
                return window == null
                    ? NoItems
                    : window.GetComponentsInChildren<NotificationItem>(true);
            }
            catch (Exception e)
            {
                Log.Warn("hud: finding the notification icons threw: " + e);
                return NoItems;
            }
        }

        private static void Open(GuiNotification notification)
        {
            try
            {
                Gui.GuiNotificationService.ToggleGuiNotification(notification);
            }
            catch (Exception e)
            {
                Log.Warn("hud: opening a notification threw: " + e);
            }
        }

        /// <summary>Throw a notification away. One the game will not let go of stays, silently: the
        /// key simply did nothing, which is what a key that does not apply here should do.</summary>
        private static void Dismiss(GuiNotification notification)
        {
            try
            {
                if (notification.IsDismissible)
                {
                    Gui.GuiNotificationService.DismissGuiNotification(notification);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: dismissing a notification threw: " + e);
            }
        }

    }
}

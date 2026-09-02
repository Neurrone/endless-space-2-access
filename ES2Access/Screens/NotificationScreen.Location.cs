using System;
using System.Collections.Generic;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>Going to where the notification happened.</summary>
    public sealed partial class NotificationScreen
    {
        // ---- go to where it happened ----

        /// <summary>
        /// Whether this notification's popup would DRAW a show-location button.
        ///
        /// Two questions in one, and the second is the one that bites: the game marks the button
        /// visible and enabled from <c>GuiNotification.HasLocation</c> alone
        /// (<c>NotificationWindow.OnBeginShow</c> :139-140) without asking whether the prefab laid one
        /// out, and forty-one of the sixty-nine prefabs did not - they answer with an orphan, parked
        /// at the screen's origin with no parent, which the engine never draws because rendering walks
        /// the tree (ES2 facts). So the test is the paint test, ending AT the window's own root.
        ///
        /// Asked of the notification's OWN window instance rather than of a table of type names: each
        /// notification names its window in its constructor
        /// (<c>base.NotificationWindow = Gui.GuiService.GetWindow&lt;...&gt;()</c>), so the prefab that
        /// would show it is a field read away and no list can go stale against the game's own.
        /// </summary>
        internal static bool DrawsShowLocation(GuiNotification notification)
        {
            try
            {
                NotificationWindow window = WindowOf(notification);
                if (window == null || !notification.HasLocation)
                {
                    return false;
                }

                AgeControlButton button = Button(window, ShowLocationButton);
                // The LAYOUT question, not the painted one: forty-one of the sixty-nine prefabs
                // bind a show-location button their layout never holds, an orphan with no parent
                // at all (ES2 facts), and that orphan is what has to be caught. Painted is the wrong
                // test here because a CLOSED popup draws nothing at all, so it answers false for every
                // notification on the strip - where the go-to-location key is most of the time
                // (measured 2026-08-22: the hint and the key both vanished from every strip row).
                return button != null && AgeWidgets.Under(button.AgeTransform, Root(window));
            }
            catch (Exception e)
            {
                Log.Warn("notification: asking whether it draws a show-location threw: " + e);
                return false;
            }
        }

        /// <summary>
        /// DO WHAT THE SHOW-LOCATION BUTTON DOES, without opening the popup.
        ///
        /// The button's own handler is <c>OnShowLocationCb</c>, and it ends with
        /// <c>ToggleGuiNotification</c> - which HIDES a popup that is showing and OPENS one that is
        /// not (<c>GuiNotificationManager</c> :386-406). Pressed from a strip row or a log line the
        /// popup is not showing, so calling the game's handler there would open the popup rather than
        /// go anywhere: the toggle is what is left out here, and everything before it is replayed.
        ///
        /// The window families that override the callback are answered from the NOTIFICATION rather
        /// than from the window, because the window is shared and bound to whichever notification is
        /// currently up - which kind does what is <see cref="Routes"/>. Everything else is
        /// <c>GuiNotification.ShowLocation()</c>, which each notification overrides for itself.
        /// </summary>
        internal static void GoToLocation(GuiNotification notification)
        {
            try
            {
                if (notification == null || !notification.HasLocation)
                {
                    return;
                }

                Action<GuiNotification> route = RouteOf(notification);
                if (route != null)
                {
                    route(notification);
                    return;
                }

                notification.ShowLocation();
            }
            catch (Exception e)
            {
                Log.Warn("notification: going to where it happened threw: " + e);
            }
        }

        /// <summary>
        /// The notification kinds whose Show Location is not the notification's own.
        ///
        /// A table rather than a chain of casts, walked base-type-first the way
        /// <see cref="VariantOf"/> walks its own (<see cref="RouteOf"/>): the same file already
        /// answers "what does this popup kind do" that way, and a route registered against a base
        /// notification serves every notification built on it, which a chain of casts only does by
        /// the order the casts happen to be written in.
        /// </summary>
        private static readonly Dictionary<Type, Action<GuiNotification>> Routes = new Dictionary<
            Type,
            Action<GuiNotification>
        >
        {
            // The quest's own marker, which cycles between a step's markers. Its notification answers
            // HasLocation true while overriding no ShowLocation, so the default route would move
            // nothing at all - and a quest-begun notification with no quest on it falls back to that
            // default rather than going nowhere.
            {
                typeof(NotificationQuestBegun),
                it =>
                {
                    NotificationQuestBegun quest = (NotificationQuestBegun)it;
                    if (quest.Quest == null)
                    {
                        it.ShowLocation();
                        return;
                    }

                    Gui.GuiGameWindowService.ShowQuestLocation(
                        quest.Quest,
                        quest.Quest.GetCurrentStep()
                    );
                }
            },
            // The orbit the encounter is being fought in.
            {
                typeof(NotificationBattleSetup),
                it => Orbit(((NotificationBattleSetup)it).GetEncounter())
            },
            // The node being defended, for both ground-battle popups.
            {
                typeof(NotificationGroundBattleReport),
                it => Defender(((NotificationGroundBattleReport)it).GroundBattle)
            },
            {
                typeof(NotificationGroundBattleSetup),
                it => Defender(((NotificationGroundBattleSetup)it).GroundBattle)
            },
            // The ordinary thing, and then the scan view: the hacking operation is drawn there and
            // nowhere else, so the button opens it.
            {
                typeof(NotificationDefenseHackingProgramEncountered),
                it =>
                {
                    it.ShowLocation();
                    Gui.GuiGameWindowService.ToggleScanView();
                }
            },
        };

        /// <summary>What this notification's Show Location does, its own kind first - a route
        /// registered against a base notification serves every notification built on it.</summary>
        private static Action<GuiNotification> RouteOf(GuiNotification notification)
        {
            for (
                Type type = notification.GetType();
                type != null && type != typeof(GuiNotification);
                type = type.BaseType
            )
            {
                Action<GuiNotification> route;
                if (Routes.TryGetValue(type, out route))
                {
                    return route;
                }
            }

            return null;
        }

        /// <summary>Where a space battle is: the orbit the encounter is being fought in.</summary>
        private static void Orbit(Encounter encounter)
        {
            if (encounter != null && encounter.Groups != null && encounter.Groups.Length >= 2)
            {
                Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(
                    encounter.Orbit.GalaxyPosition
                );
            }
        }

        /// <summary>Where a ground battle is: the node being defended.</summary>
        private static void Defender(GroundBattle battle)
        {
            StarSystemNode node = battle == null ? null : battle.DefenderNode;
            if (node != null)
            {
                Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(node.GalaxyPosition);
            }
        }

        /// <summary>The window a notification would be shown in - its own, named in its constructor.
        /// </summary>
        private static NotificationWindow WindowOf(GuiNotification notification)
        {
            return notification == null ? null : notification.NotificationWindow as NotificationWindow;
        }

        private static AgeTransform Root(NotificationWindow window)
        {
            try
            {
                return window.gameObject.GetComponent<AgeTransform>();
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for the window's transform threw: " + e);
                return null;
            }
        }

        /// <summary>Whether the window HOLDS this widget - whether its own tree is what the widget hangs
        /// under, which is what makes the widget part of what this popup draws rather than something the
        /// prefab wired up and left outside the layout.</summary>
        private static bool Held(NotificationWindow window, AgeTransform widget)
        {
            AgeTransform root = Root(window);
            return root != null && AgeWidgets.Under(widget, root);
        }

    }
}

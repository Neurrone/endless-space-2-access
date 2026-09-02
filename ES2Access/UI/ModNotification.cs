using System;
using System.Collections.Generic;
using Amplitude.Unity.Event;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// A notification the MOD raises, living in the game's own notification list.
    ///
    /// The game already owns everything a notification needs - an icon in the strip, a popup with a
    /// title and a description, Previous/Next between notifications, a dismiss, a Show Location
    /// button, and an end-of-turn expiry. All of it is driven off one per-empire list
    /// (<c>GuiNotificationManager</c>) and one private event-type-to-notification-type dictionary, so
    /// a mod type that joins that dictionary inherits the lot without a prefab, an XML entry or a
    /// second UI surface (<see cref="ModNotifications"/> does the joining).
    ///
    /// What a subclass has to supply is only what the game cannot know: which events it answers to
    /// and whether a given one is interesting (<see cref="Accept"/> - returning false ABORTS the
    /// creation, which is the pipeline's own veto, <c>GuiNotificationManager.RecordEventForEmpire</c>
    /// :777-780), the sentence the player hears (<see cref="Title"/>), the sentence the popup shows
    /// (<see cref="Body"/>) and the place the map should fly to (<see cref="Location"/>).
    ///
    /// Four settings differ from a game notification and each has a reason:
    /// - <c>SkipSerialization</c> is TRUE. It is the game's own opt-out
    ///   (<c>GuiNotification</c> :12,29) and the save's list writer skips any element that declares
    ///   it (<c>BinarySerializer</c> :643-664), so no mod type can ever reach a save file - which is
    ///   what makes injecting types the game has never heard of safe. A save written with mod
    ///   notifications pending loads in a vanilla game with nothing missing.
    /// - <c>Priority</c> is 100. The insert walk is ascending and every game type uses -2, -1 or 0
    ///   (<c>GuiNotificationManager.InsertNotificationBasedOnPriority</c> :822-837), so mod entries
    ///   always sort after the game's own and keep their arrival order among themselves.
    /// - <c>AutoPopUp</c> is false and its setter does nothing. The base getter asks the per-type
    ///   settings service by type name (<c>GuiNotification</c> :37-47) and there is no entry for a
    ///   mod type; more to the point, a popup stealing the screen is exactly what the mod does not
    ///   want, because arrival is already SPOKEN. The popup opens when the player asks for it.
    /// - <c>TurnsBeforeAutoDismiss</c> is <see cref="RetentionTurns"/> rather than the base's 1
    ///   (owner ruling 2026-08-20), so a turn log spans several turns. The counter is decremented
    ///   once per turn at <c>GameClientState_Turn_FinishedAndLocked</c> and the notification is
    ///   dismissed at zero (<c>GuiNotificationManager</c> :864-894).
    ///
    /// The turn is stamped at creation and the popup's description is prefixed with it, because a
    /// popup reached by Previous/Next from a game notification otherwise carries no clue which turn
    /// its news is from. The spoken title stays turn-free: at arrival the turn is always this one.
    /// </summary>
    public abstract class ModNotification : GuiNotification
    {
        /// <summary>How many turns a mod notification lives for (owner ruling 2026-08-20). The game's
        /// own default is 1 - one turn's grace - which is too short for a log the player walks.
        /// </summary>
        public const int RetentionTurns = 5;

        /// <summary>The GuiElement a mod notification borrows for its icon, its large image and its
        /// sounds. A mod type has no element of its own (the base constructor looks one up by type
        /// name and finds nothing), and a null one makes the manager log an error every time it asks
        /// for a sound tag (<c>Gui.GetSoundDefinition</c> :1851-1857). Borrowing the generic
        /// information notification's costs nothing and is the same element the shared popup window
        /// belongs to.</summary>
        private const string BorrowedElement = "NotificationInformation";

        public override bool SkipSerialization => true;

        public override int Priority => 100;

        public override bool AutoPopUp
        {
            get { return false; }
            set
            {
                // The popup draws a "pop up automatically" tick box wired straight to this setter,
                // and there is nothing here to store: a mod type has no entry in the per-type
                // settings service, and a popup stealing the screen is what this family must not do,
                // because arrival is already spoken. Asking the window to refresh puts the drawn tick
                // back where the model says it is, so the picture never keeps a value the answer
                // denies. (The tick still FLIPS under the player's hand for one frame - the toggle
                // does that itself before this runs.)
                INotificationWindow window = NotificationWindow;
                if (window != null)
                {
                    window.Dirty = true;
                }
            }
        }

        /// <summary>The turn this notification was raised on, as the popup's description states it.
        /// </summary>
        public int Turn { get; private set; }

        protected ModNotification()
        {
            TurnsBeforeAutoDismiss = RetentionTurns;
            try
            {
                Turn = CurrentTurn();
                guiElement = Gui.GetExtendedGuiElement(BorrowedElement);
                // The quiet overload: the engine LOGS an error when a window is not registered yet, and
                // the game forwards every Error to its telemetry (ES2 facts).
                NotificationWindow = Gui.GuiService.GetWindow<InformationNotificationWindow>(false);
            }
            catch (Exception e)
            {
                // A notification with no window can still be dismissed and still reads in the log;
                // throwing here would abort the game's own event dispatch.
                Log.Warn("notifications: preparing a mod notification threw: " + e);
            }
        }

        public override string GetTitle()
        {
            try
            {
                return Title() ?? string.Empty;
            }
            catch (Exception e)
            {
                Log.Warn("notifications: titling a mod notification threw: " + e);
                return string.Empty;
            }
        }

        public override string GetDescription()
        {
            try
            {
                return ModStrings.Format(ModStrings.NotificationTurnPrefix, Turn, Body() ?? string.Empty);
            }
            catch (Exception e)
            {
                Log.Warn("notifications: describing a mod notification threw: " + e);
                return string.Empty;
            }
        }

        public override bool HasLocation
        {
            get
            {
                try
                {
                    return Location() != null;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public override void ShowLocation()
        {
            try
            {
                IGameEntityWithGalaxyPosition where = Location();
                if (where != null)
                {
                    Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(where);
                }
            }
            catch (Exception e)
            {
                Log.Warn("notifications: showing a mod notification's location threw: " + e);
            }
        }

        /// <summary>
        /// The pipeline's own filter. The game builds the notification, calls this, and throws it
        /// away again if the answer is false - so everything a mod type refuses (another empire's
        /// event, a fleet that is not automated, a sighting already standing in the list) is refused
        /// here and nowhere else.
        /// </summary>
        public override bool Bind(GameEvent gameEvent)
        {
            try
            {
                if (!base.Bind(gameEvent))
                {
                    return false;
                }

                if (!Accept(gameEvent))
                {
                    return false;
                }

                return !AlreadyStanding();
            }
            catch (Exception e)
            {
                Log.Warn("notifications: binding a mod notification threw: " + e);
                return false;
            }
        }

        /// <summary>Whether this event is one this notification wants, and the moment its payload is
        /// read off - the event object is not kept, because the game does not keep it either.
        /// </summary>
        protected abstract bool Accept(GameEvent gameEvent);

        /// <summary>The sentence spoken on arrival, and the popup's heading.</summary>
        protected abstract string Title();

        /// <summary>The sentence the popup shows under the heading, before the turn is prefixed to
        /// it. Defaults to the title: for most of these the game has one thing to say and says it
        /// once.</summary>
        protected virtual string Body()
        {
            return Title();
        }

        /// <summary>Where the map goes when the player presses Show Location, or null for a
        /// notification about no particular place - which is what hides the button
        /// (<c>NotificationWindow.OnBeginShow</c> :138-140). Where an event has an origin and a
        /// destination this is the DESTINATION (owner ruling 2026-08-20).</summary>
        protected virtual IGameEntityWithGalaxyPosition Location()
        {
            return null;
        }

        /// <summary>
        /// The thing this notification is ABOUT, for the families whose event the game re-raises for
        /// the same subject - a fleet whose visibility rises from Visible to Exposed raises a second
        /// sighting of the same fleet (<c>EntityVisibility.Layer</c> has five steps and
        /// <c>Fleet.Visibility_OnLayerChanged</c> :1213-1221 fires on every one at or above Visible).
        /// A non-null subject makes a second notification of the same type about the same thing
        /// refuse itself while the first is still standing; null - the default - means every event is
        /// its own news.
        /// </summary>
        protected virtual object Subject()
        {
            return null;
        }

        private bool AlreadyStanding()
        {
            object subject = Subject();
            if (subject == null)
            {
                return false;
            }

            IGuiNotificationService service = Gui.GuiNotificationService;
            List<GuiNotification> standing =
                service == null ? null : service.GetPlayerEmpireGuiNotifications();
            if (standing == null)
            {
                return false;
            }

            Type mine = GetType();
            for (int i = 0; i < standing.Count; i++)
            {
                ModNotification other = standing[i] as ModNotification;
                if (
                    other != null
                    && other.GetType() == mine
                    && ReferenceEquals(other.Subject(), subject)
                )
                {
                    return true;
                }
            }

            return false;
        }

        // ---- what a subclass reads the world with ----

        protected static Empire Player()
        {
            return Gui.PlayerEmpire;
        }

        protected static bool IsPlayer(Amplitude.Unity.Game.Empire empire)
        {
            return empire != null && ReferenceEquals(empire, Gui.PlayerEmpire);
        }

        /// <summary>What the map calls a place - and nothing at all where the map does not name it,
        /// which is the same rule every other spoken place in the mod follows
        /// (<see cref="FleetRoute.Named"/>).</summary>
        protected static string PlaceName(GameNode node)
        {
            return node == null ? null : FleetRoute.Named(node);
        }

        /// <summary>What an empire is called, to the player - the shared answer every spoken empire
        /// name in the mod comes from (<see cref="EmpireNames.Named"/>).</summary>
        protected static string EmpireName(Amplitude.Unity.Game.Empire empire)
        {
            return EmpireNames.Named(empire);
        }

        /// <summary>The node a path's destination stands at - a path carries a position, and a
        /// position is not something the player can be told.</summary>
        protected static GameNode NodeAt(NodePosition position)
        {
            try
            {
                IPositioningService positioning =
                    Amplitude.Unity.Framework.Services.GetService<IPositioningService>();
                return positioning == null ? null : positioning.GetGameNode(position);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The turn the player is reading in the corner, which is the simulation's own count
        /// PLUS ONE - measured 2026-08-20: the HUD read "Turn 4" while <c>Game.Turn</c> was 3, and a
        /// notification stamped 3 would have named a turn nobody could see. The sum is
        /// <see cref="FleetRoute.DisplayedTurn"/>'s, shared rather than copied.</summary>
        private static int CurrentTurn()
        {
            return FleetRoute.DisplayedTurn();
        }
    }
}

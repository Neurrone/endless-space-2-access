using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Amplitude.Unity.Event;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The mod's own notifications, joined to the game's notification pipeline, and the news the
    /// player hears as each one lands.
    ///
    /// The join is one reflected entry per event type into <c>GuiNotificationManager</c>'s private
    /// <c>guiNotificationTypeByEventType</c> (built at :576-740, looked up by EXACT type at :747).
    /// From then on the game itself does the work: its own <c>IEventService.EventRaised</c>
    /// subscription catches the event, builds the mapped notification, calls
    /// <c>Bind</c> - which is where a mod type refuses events that are not for the player - inserts
    /// it by priority and raises <c>PlayerEmpireNotificationsCollectionChanged</c>. Eight of the
    /// events are the GAME's own, already flowing through the bus with no notification bound to
    /// them. The other five are the MOD's, defined below and raised through the same public
    /// <c>IEventService.Notify</c> by the watchers that notice things the game puts on no bus at all
    /// (<see cref="FleetArrivals"/>, <see cref="ForeignFleetWatch"/>, <see cref="FleetRouteWatch"/>,
    /// <see cref="InfluenceGroundWatch"/>) -
    /// so those five cost a detection point each and nothing else: one pipeline, one set of
    /// behaviours, one place the wording lives.
    ///
    /// The manager is per-GAME (its constructor's field initialiser plus <c>BindServices</c>'s
    /// unconditional <c>Add</c> calls mean a reused instance would throw on duplicate keys), so the
    /// entries are re-asserted from the pump rather than installed once: a new game, a loaded save
    /// or a hot reload all land on a manager that has never heard of the mod, and
    /// <see cref="Tick"/> notices within a frame.
    ///
    /// SPEAKING is separate from creating, and deliberately so: the collection-changed handler runs
    /// inside the game's own event dispatch and only records the line, which the pump then speaks
    /// (the mod's rule - hooks never speak). Queued, never interrupting: this is news the player did
    /// not ask for and it arrives while they are reading something they did.
    /// </summary>
    public static class ModNotifications
    {
        private static readonly Type[] EventTypes =
        {
            typeof(EventStarSystemNodeRevealed),
            typeof(EventEmpireSeen),
            typeof(EventFleetMoveStarted),
            typeof(EventSystemBesieged),
            typeof(EventSystemBlockaded),
            typeof(EventTradingBlockadeEnded),
            typeof(EventObliteratorFired),
            typeof(EventObliteratorFireObserved),
            typeof(EventModFleetArrived),
            typeof(EventModFleetStopped),
            typeof(EventModForeignFleetLost),
            typeof(EventModForeignFleetMoved),
            typeof(EventModInfluenceGroundLost),
        };

        private static readonly Type[] NotificationTypes =
        {
            typeof(SystemRevealedNotification),
            typeof(EmpireSightedNotification),
            typeof(AutomatedFleetDispatchedNotification),
            typeof(SystemBesiegedNotification),
            typeof(SystemBlockadedNotification),
            typeof(TradeBlockadeEndedNotification),
            typeof(ObliteratorFiredNotification),
            typeof(ObliteratorFireObservedNotification),
            typeof(FleetArrivedNotification),
            typeof(FleetStoppedNotification),
            typeof(ForeignFleetLostNotification),
            typeof(ForeignFleetMovedNotification),
            typeof(InfluenceGroundLostNotification),
        };

        private static GuiNotificationManager _manager;
        private static IDictionary _map;
        private static readonly List<string> _said = new List<string>(4);

        /// <summary>Whether the mappings are in place right now - what a probe asks, and what the
        /// test for a clean teardown reads.</summary>
        public static bool Installed
        {
            get { return _map != null; }
        }

        /// <summary>What the game's own dictionary currently says each of the mod's events maps to,
        /// one entry per event, the value carrying the ASSEMBLY - which is what tells a live entry
        /// from one an unfinished teardown left behind. "missing" where nothing is mapped.</summary>
        public static List<KeyValuePair<string, string>> Mapped()
        {
            List<KeyValuePair<string, string>> found =
                new List<KeyValuePair<string, string>>(EventTypes.Length);
            IDictionary map = _map;
            for (int i = 0; i < EventTypes.Length; i++)
            {
                Type mapped = map != null && map.Contains(EventTypes[i])
                    ? map[EventTypes[i]] as Type
                    : null;
                found.Add(
                    new KeyValuePair<string, string>(
                        EventTypes[i].Name,
                        mapped == null
                            ? "missing"
                            : mapped.FullName + " @ " + mapped.Assembly.GetName().Name
                    )
                );
            }

            return found;
        }

        /// <summary>
        /// Put one of the MOD's OWN events on the game's bus, so that the four things the game
        /// notices but never mentions - a fleet reaching its destination, a fleet stopped short, a
        /// foreign fleet going out of sight, a foreign fleet standing somewhere else this turn - come
        /// out the same pipeline as the eight the game already raises. The bus is the production
        /// path, and safe for these four because no game code has ever heard of the types: every
        /// subscriber that switches on an event type simply does not match (the quest manager, the
        /// one that could have been surprised, asks a set for the type and falls through -
        /// <c>QuestManager.HandleEvent</c> :5391-5418).
        ///
        /// Called from the PUMP by the watchers that detect these things, never from their Harmony
        /// hooks: the mapping is asserted first, so an event raised on the first frame of a loaded
        /// save is not thrown away by a manager the mod has not joined yet.
        /// </summary>
        public static void Raise(GameEvent gameEvent)
        {
            try
            {
                Ensure();
                IEventService service =
                    Amplitude.Unity.Framework.Services.GetService<IEventService>();
                if (service != null)
                {
                    service.Notify(gameEvent);
                }
            }
            catch (Exception e)
            {
                Log.Warn("notifications: raising a mod event threw: " + e);
            }
        }

        /// <summary>
        /// Keep the mappings and the arrival subscription pointed at whatever manager the game
        /// currently has, then speak whatever landed since the last frame.
        /// </summary>
        public static void Tick()
        {
            try
            {
                Ensure();
            }
            catch (Exception e)
            {
                Log.Warn("notifications: keeping the mod notifications installed threw: " + e);
            }

            if (_said.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _said.Count; i++)
            {
                Voice.Say(_said[i], false);
            }

            _said.Clear();
        }

        /// <summary>
        /// Hand everything back: the reflected entries come out of the game's dictionary, the
        /// subscription goes back to a service that outlives this assembly, and every mod
        /// notification still standing is dismissed - a type from an assembly nobody can reach any
        /// more, left in the manager's list, would break the next reload the first time the strip
        /// asked it for a title.
        /// </summary>
        public static void Stop()
        {
            try
            {
                DismissAll();
            }
            catch (Exception e)
            {
                Log.Warn("notifications: dismissing the mod notifications threw: " + e);
            }

            Detach();
            _said.Clear();
        }

        private static void Ensure()
        {
            GuiNotificationManager manager = Manager();
            if (manager == null)
            {
                Detach();
                return;
            }

            if (ReferenceEquals(manager, _manager) && _map != null && _map.Contains(EventTypes[0]))
            {
                return;
            }

            Detach();
            Attach(manager);
        }

        private static GuiNotificationManager Manager()
        {
            try
            {
                return Gui.GuiNotificationService as GuiNotificationManager;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Attach(GuiNotificationManager manager)
        {
            IDictionary map = MapOf(manager);
            if (map == null)
            {
                return;
            }

            for (int i = 0; i < EventTypes.Length; i++)
            {
                Type existing = map.Contains(EventTypes[i]) ? map[EventTypes[i]] as Type : null;
                if (existing == null)
                {
                    map.Add(EventTypes[i], NotificationTypes[i]);
                    continue;
                }

                if (existing == NotificationTypes[i])
                {
                    continue;
                }

                // A type of ours from a PREVIOUS load, left behind by a teardown that did not run.
                // Type identity is per assembly, so it is not our type any more and the game would
                // build notifications nobody can reach; the name is what identifies it.
                if (existing.FullName == NotificationTypes[i].FullName)
                {
                    Log.Warn(
                        "notifications: replacing a stale mapping for "
                            + EventTypes[i].Name
                            + " left by an earlier load"
                    );
                    map[EventTypes[i]] = NotificationTypes[i];
                }
            }

            _manager = manager;
            _map = map;
            manager.PlayerEmpireNotificationsCollectionChanged += Arrived;
        }

        private static void Detach()
        {
            GuiNotificationManager manager = _manager;
            IDictionary map = _map;
            _manager = null;
            _map = null;

            if (manager != null)
            {
                try
                {
                    manager.PlayerEmpireNotificationsCollectionChanged -= Arrived;
                }
                catch (Exception e)
                {
                    Log.Warn("notifications: unsubscribing from the arrivals threw: " + e);
                }
            }

            if (map == null)
            {
                return;
            }

            try
            {
                for (int i = 0; i < EventTypes.Length; i++)
                {
                    // Only ours: a key the game itself put there is never touched, however the two
                    // tables came to overlap. Matched by NAME rather than by identity so that a
                    // teardown after a failed reload still finds an earlier load's entry.
                    Type existing = map[EventTypes[i]] as Type;
                    if (existing != null && existing.FullName == NotificationTypes[i].FullName)
                    {
                        map.Remove(EventTypes[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("notifications: removing the mod mappings threw: " + e);
            }
        }

        private static IDictionary MapOf(GuiNotificationManager manager)
        {
            try
            {
                FieldInfo field = typeof(GuiNotificationManager).GetField(
                    "guiNotificationTypeByEventType",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                if (field == null)
                {
                    Log.Error(
                        "notifications: GuiNotificationManager has no guiNotificationTypeByEventType"
                            + " field; mod notifications are off"
                    );
                    return null;
                }

                return field.GetValue(manager) as IDictionary;
            }
            catch (Exception e)
            {
                Log.Error("notifications: reaching the event mapping threw: " + e);
                return null;
            }
        }

        /// <summary>Runs inside the game's own dispatch: records the line and returns. Speaking is
        /// the pump's (<see cref="Tick"/>).</summary>
        private static void Arrived(object sender, CollectionChangeEventArgs e)
        {
            try
            {
                if (e == null || e.Action != CollectionChangeAction.Add)
                {
                    return;
                }

                ModNotification arrived = e.Element as ModNotification;
                if (arrived == null)
                {
                    return;
                }

                string line = arrived.GetTitle();
                if (!string.IsNullOrEmpty(line))
                {
                    _said.Add(line);
                }
            }
            catch (Exception error)
            {
                Log.Warn("notifications: noticing a mod notification threw: " + error);
            }
        }

        /// <summary>Every mod notification the manager is holding, for ANY empire - the player's list
        /// is the only one they are meant to reach, and the private array is swept anyway so that a
        /// teardown cannot depend on that being true.</summary>
        private static void DismissAll()
        {
            GuiNotificationManager manager = _manager ?? Manager();
            if (manager == null)
            {
                return;
            }

            IGuiNotificationService service = manager;
            List<GuiNotification> mine = new List<GuiNotification>(4);
            FieldInfo field = typeof(GuiNotificationManager).GetField(
                "guiNotificationsByEmpireIndex",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            IList[] lists = field == null ? null : field.GetValue(manager) as IList[];
            if (lists != null)
            {
                for (int i = 0; i < lists.Length; i++)
                {
                    IList list = lists[i];
                    for (int j = 0; list != null && j < list.Count; j++)
                    {
                        GuiNotification found = list[j] as GuiNotification;
                        if (found != null && IsOurs(found))
                        {
                            mine.Add(found);
                        }
                    }
                }
            }
            else
            {
                List<GuiNotification> standing = service.GetPlayerEmpireGuiNotifications();
                for (int i = 0; standing != null && i < standing.Count; i++)
                {
                    if (standing[i] != null && IsOurs(standing[i]))
                    {
                        mine.Add(standing[i]);
                    }
                }
            }

            for (int i = 0; i < mine.Count; i++)
            {
                service.DismissGuiNotification(mine[i]);
            }
        }

        /// <summary>Whether this notification is one of the mod's - by type NAME, not by type, so
        /// that a teardown running after a reload has already replaced the assembly still recognises
        /// the instances the previous load left in the list.</summary>
        private static bool IsOurs(GuiNotification notification)
        {
            if (notification is ModNotification)
            {
                return true;
            }

            string name = notification.GetType().FullName;
            for (int i = 0; i < NotificationTypes.Length; i++)
            {
                if (NotificationTypes[i].FullName == name)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// A star system the player's exploration has just crossed into Revealed
    /// (<c>StarSystemNode.NotifyDiscovery</c> :1407-1411 raises it exactly once per empire, on the
    /// promotion past state 4). The coordinates are read exactly as the galaxy map reads a place's
    /// position, through the same helper, so a system announced here and the same system walked on
    /// the map cannot say different numbers.
    /// </summary>
    public sealed class SystemRevealedNotification : ModNotification
    {
        private StarSystemNode _node;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventStarSystemNodeRevealed revealed = gameEvent as EventStarSystemNodeRevealed;
            if (revealed == null || revealed.StarSystemNode == null || !IsPlayer(revealed.Empire))
            {
                return false;
            }

            _node = revealed.StarSystemNode;
            return true;
        }

        protected override string Title()
        {
            // Accept refuses an event with no node, so a null one here means the notification outlived
            // the thing it was about - which a hot reload does, by leaving an instance of the previous
            // assembly's type standing in the manager's list for the strip to ask for a title.
            // Answering with the news minus the name beats an empty row (measured 2026-08-20: one such
            // instance threw here and the row read as nothing at all).
            if (_node == null)
            {
                return ModStrings.Get(ModStrings.NotificationSystemRevealedUnknown);
            }

            string where = GalaxyCoordinates.Text(_node.GalaxyPosition);
            return where == null
                ? ModStrings.Format(ModStrings.NotificationSystemRevealedPlain, _node.LocalizedName)
                : ModStrings.Format(
                    ModStrings.NotificationSystemRevealed,
                    _node.LocalizedName,
                    where
                );
        }

        protected override string Body()
        {
            if (_node == null)
            {
                return Title();
            }

            Constellation constellation = _node.Constellation;
            return ConstellationMap.Explored(constellation, Player())
                ? ModStrings.Format(
                    ModStrings.NotificationSystemRevealedInConstellation,
                    _node.LocalizedName,
                    constellation.LocalizedName
                )
                : ModStrings.Format(ModStrings.NotificationSystemRevealedBody, _node.LocalizedName);
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return _node;
        }
    }

    /// <summary>
    /// Somebody else's fleet or colony coming into sight. The game raises one event for both
    /// (<c>Fleet.Visibility_OnLayerChanged</c> :1213-1221 and
    /// <c>ColonizedStarSystem.UpdateEmpireSeen</c> :4816-4824) and tells them apart only by what it
    /// puts in <c>Entity</c>, so one notification type answers for both and picks its wording from
    /// that.
    ///
    /// It is the one family here that has to refuse REPEATS. Visibility has five steps and the
    /// event fires on every change at or above Visible, so a fleet that becomes Exposed a moment
    /// after becoming Visible is seen twice; the subject makes the second refuse itself while the
    /// first is still in the list.
    /// </summary>
    public sealed class EmpireSightedNotification : ModNotification
    {
        private Amplitude.Unity.Game.Empire _seen;
        private Fleet _fleet;
        private ColonizedStarSystem _colony;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventEmpireSeen seen = gameEvent as EventEmpireSeen;
            if (seen == null || !IsPlayer(seen.EmpireWhoSaw))
            {
                return false;
            }

            _seen = seen.EmpireWhoHasBeenSeen;
            if (_seen == null || IsPlayer(_seen))
            {
                return false;
            }

            _fleet = seen.Entity as Fleet;
            _colony = seen.Entity as ColonizedStarSystem;
            return _fleet != null || _colony != null;
        }

        protected override object Subject()
        {
            return (object)_fleet ?? _colony;
        }

        protected override string Title()
        {
            if (_colony != null)
            {
                return ModStrings.Format(
                    ModStrings.NotificationColonySighted,
                    EmpireName(_seen),
                    _colony.LocalizedName
                );
            }

            string where = PlaceName(_fleet.GetGameNode());
            string owner = Owner();
            return where == null
                ? ModStrings.Format(ModStrings.NotificationFleetSightedNowhere, owner)
                : ModStrings.Format(ModStrings.NotificationFleetSighted, owner, where);
        }

        /// <summary>Whose fleet has been sighted, said the way every other surface says it - which way
        /// the player stands to them in front of their name (<see cref="FleetPhrase.Owned(Fleet)"/>),
        /// so "enemy Leaper (AI) fleet sighted at Heka". The bare name is the fallback for the empire
        /// the phrase cannot place, which is also the only form the colony sighting has.</summary>
        private string Owner()
        {
            return FleetPhrase.Owned(_fleet) ?? EmpireName(_seen);
        }

        protected override string Body()
        {
            if (_colony != null)
            {
                return ModStrings.Format(
                    ModStrings.NotificationColonySightedBody,
                    EmpireName(_seen),
                    _colony.LocalizedName
                );
            }

            string where = PlaceName(_fleet.GetGameNode());
            string owner = Owner();
            string named = Named();
            return where == null
                ? ModStrings.Format(ModStrings.NotificationFleetSightedBodyNowhere, owner, named)
                : ModStrings.Format(
                    ModStrings.NotificationFleetSightedBody,
                    owner,
                    named,
                    where
                );
        }

        /// <summary>
        /// The sighted fleet as the sentence's own subject - its name, then who is commanding it and
        /// what it is made of, read INSIDE the sentence rather than as sentences trailing after it
        /// (owner ruling 2026-08-26): "The enemy Leaper (AI) fleet 1st Ravaging Horde, Scavenger, was
        /// sighted at Heka."
        ///
        /// Whose it is is left out here - the sentence has already said it in its own slot, and
        /// saying it twice would be the mod stammering.
        ///
        /// The trailing comma is the far side of that appositive, and it is the mod's own list
        /// separator rather than a punctuation mark written into the code, so a language that
        /// separates a list some other way separates this too. It is only added where there IS an
        /// appositive: a fleet the player may not count and that carries no hero is nothing but its
        /// name, and a name needs no comma after it.
        /// </summary>
        private string Named()
        {
            string named = FleetPhrase.Full(_fleet, false);
            if (string.IsNullOrEmpty(named) || named == _fleet.LocalizedName)
            {
                return named;
            }

            return named + ModStrings.Get(ModStrings.ListSeparator).TrimEnd();
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            if (_colony != null)
            {
                return _colony.Node;
            }

            return _fleet == null || _fleet.IsDestroyed ? null : _fleet;
        }
    }

    /// <summary>
    /// One of the player's own automated ships setting off - the migration and delivery fleets the
    /// interior department schedules at the end of a turn, which move without anybody being told.
    /// The event is the ordinary start-of-movement one (<c>Fleet.OnGoToStart</c>, <c>Fleet</c>
    /// :1581-1585), so the filter is what makes this family: automated fleets only, and only the
    /// player's.
    /// </summary>
    public sealed class AutomatedFleetDispatchedNotification : ModNotification
    {
        private Fleet _fleet;
        private GameNode _from;
        private GameNode _to;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventFleetMoveStarted started = gameEvent as EventFleetMoveStarted;
            if (started == null || started.Fleet == null || !IsPlayer(started.Empire))
            {
                return false;
            }

            if (!started.Fleet.IsAutomated)
            {
                return false;
            }

            _fleet = started.Fleet;
            _from = _fleet.GetGameNode();
            _to = started.Path == null ? null : NodeAt(started.Path.Destination);
            return true;
        }

        protected override string Title()
        {
            string from = PlaceName(_from);
            string to = PlaceName(_to);
            string fleet = FleetPhrase.Full(_fleet);
            if (to == null)
            {
                return ModStrings.Format(ModStrings.NotificationFleetDispatchedPlain, fleet);
            }

            return from == null
                ? ModStrings.Format(ModStrings.NotificationFleetDispatchedTo, fleet, to)
                : ModStrings.Format(ModStrings.NotificationFleetDispatched, fleet, from, to);
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return _to;
        }
    }

    /// <summary>
    /// A system of the player's under siege. The event is real and carries the victim, the
    /// besieger and the system (<c>SiegeStarSystemAction</c> :97), and the game consumes it as a
    /// political effect on the population without ever telling the player it happened.
    /// </summary>
    public sealed class SystemBesiegedNotification : ModNotification
    {
        private ColonizedStarSystem _system;
        private Amplitude.Unity.Game.Empire _by;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventSystemBesieged besieged = gameEvent as EventSystemBesieged;
            if (
                besieged == null
                || besieged.ColonizedStarSystem == null
                || !IsPlayer(besieged.Empire)
            )
            {
                return false;
            }

            _system = besieged.ColonizedStarSystem;
            _by = Aggressor(besieged.PopulationEventRaisingContext, besieged.Instigator);
            return true;
        }

        /// <summary>
        /// Who is doing this, which is NOT what the event's own <c>Instigator</c> says. The siege and
        /// blockade events pass the aggressor to a two-argument base call
        /// (<c>EventSystemBesieged</c>, <c>EventSystemBlockaded</c>), and the base there is
        /// <c>EmpireEvent(Empire, params object[])</c> rather than the three-argument
        /// <c>(Empire, Empire, object[])</c> - so the aggressor lands in the unused parameter array
        /// and <c>Instigator</c> is set to the VICTIM. Measured live 2026-08-20: the title read
        /// "Xiu under siege by Neurrone" with Neurrone the victim. The population context the same
        /// constructor builds keeps the real one, so that is what is read.
        /// </summary>
        internal static Amplitude.Unity.Game.Empire Aggressor(
            PopulationEventRaisingContext context,
            Amplitude.Unity.Game.Empire instigator
        )
        {
            Amplitude.Unity.Game.Empire found = context == null ? instigator : context.Instigator;
            return IsPlayer(found) ? null : found;
        }

        protected override string Title()
        {
            string by = EmpireName(_by);
            return by == null
                ? ModStrings.Format(ModStrings.NotificationSystemBesiegedPlain, _system.LocalizedName)
                : ModStrings.Format(
                    ModStrings.NotificationSystemBesieged,
                    _system.LocalizedName,
                    by
                );
        }

        protected override string Body()
        {
            string by = EmpireName(_by);
            return by == null
                ? Title()
                : ModStrings.Format(
                    ModStrings.NotificationSystemBesiegedBody,
                    _system.LocalizedName,
                    by
                );
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return _system.Node;
        }
    }

    /// <summary>The same shape for a blockade (<c>GuardEmpireLocalAction</c> :195), which the game
    /// is equally silent about.</summary>
    public sealed class SystemBlockadedNotification : ModNotification
    {
        private ColonizedStarSystem _system;
        private Amplitude.Unity.Game.Empire _by;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventSystemBlockaded blockaded = gameEvent as EventSystemBlockaded;
            if (
                blockaded == null
                || blockaded.ColonizedStarSystem == null
                || !IsPlayer(blockaded.Empire)
            )
            {
                return false;
            }

            _system = blockaded.ColonizedStarSystem;
            _by = SystemBesiegedNotification.Aggressor(
                blockaded.PopulationEventRaisingContext,
                blockaded.Instigator
            );
            return true;
        }

        protected override string Title()
        {
            string by = EmpireName(_by);
            return by == null
                ? ModStrings.Format(
                    ModStrings.NotificationSystemBlockadedPlain,
                    _system.LocalizedName
                )
                : ModStrings.Format(
                    ModStrings.NotificationSystemBlockaded,
                    _system.LocalizedName,
                    by
                );
        }

        protected override string Body()
        {
            string by = EmpireName(_by);
            return by == null
                ? Title()
                : ModStrings.Format(
                    ModStrings.NotificationSystemBlockadedBody,
                    _system.LocalizedName,
                    by
                );
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return _system.Node;
        }
    }

    /// <summary>
    /// A trade blockade lifting. The game notifies the blockade STARTING
    /// (<c>EventTradingBlockade</c> is mapped at <c>GuiNotificationManager</c> :686) and says
    /// nothing when it ends, although the same method raises both
    /// (<c>TradingRoute.UpdateBlockade</c> :236). The event carries only the node on the route the
    /// blockade sat on, so that is what is named.
    /// </summary>
    public sealed class TradeBlockadeEndedNotification : ModNotification
    {
        private GameNode _node;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventTradingBlockadeEnded ended = gameEvent as EventTradingBlockadeEnded;
            if (ended == null || ended.GameNode == null || !IsPlayer(ended.Empire))
            {
                return false;
            }

            _node = ended.GameNode;
            return true;
        }

        protected override string Title()
        {
            string where = PlaceName(_node);
            return where == null
                ? ModStrings.Get(ModStrings.NotificationTradeBlockadeEndedPlain)
                : ModStrings.Format(ModStrings.NotificationTradeBlockadeEnded, where);
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return _node;
        }
    }

    /// <summary>
    /// The player's own Obliterator going off. The event is raised as the projectile launches
    /// (<c>FireObliteratorFleetAction</c> :86) and its node is where the FLEET is standing, not
    /// where the shot is aimed - the target only becomes news at impact, which the game already
    /// notifies through the attack report. So the sentence says where it was fired FROM, which is
    /// the only place the event knows.
    /// </summary>
    public sealed class ObliteratorFiredNotification : ModNotification
    {
        private Fleet _fleet;
        private GameNode _node;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventObliteratorFired fired = gameEvent as EventObliteratorFired;
            if (fired == null || fired.Fleet == null || !IsPlayer(fired.Empire))
            {
                return false;
            }

            _fleet = fired.Fleet;
            _node = fired.GameNode;
            return true;
        }

        protected override string Title()
        {
            string where = PlaceName(_node);
            return where == null
                ? ModStrings.Format(
                    ModStrings.NotificationObliteratorFiredPlain,
                    _fleet.LocalizedName
                )
                : ModStrings.Format(
                    ModStrings.NotificationObliteratorFired,
                    _fleet.LocalizedName,
                    where
                );
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return _node;
        }
    }

    /// <summary>
    /// Somebody else's Obliterator going off in sight of the player
    /// (<c>FireObliteratorFleetAction</c> :107 raises one per empire that can see the firing
    /// fleet's node). Here the firing empire IS known, and the node is again where the shot came
    /// from.
    /// </summary>
    public sealed class ObliteratorFireObservedNotification : ModNotification
    {
        private Amplitude.Unity.Game.Empire _by;
        private GameNode _node;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventObliteratorFireObserved observed = gameEvent as EventObliteratorFireObserved;
            if (observed == null || !IsPlayer(observed.Empire))
            {
                return false;
            }

            _by = observed.Instigator;
            _node = observed.GameNode;
            return _by != null;
        }

        protected override string Title()
        {
            string where = PlaceName(_node);
            return where == null
                ? ModStrings.Format(
                    ModStrings.NotificationObliteratorObservedPlain,
                    EmpireName(_by)
                )
                : ModStrings.Format(
                    ModStrings.NotificationObliteratorObserved,
                    EmpireName(_by),
                    where
                );
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return _node;
        }
    }

    // ---- the mod's own events, for the five things no bus event exists for ----
    //
    // Each is an EmpireEvent so that the game's own dispatch routes it to ONE empire - the player -
    // rather than offering it to every empire in the galaxy in turn
    // (GuiNotificationManager.EventService_EventRaised :847-855). They carry the payload READ AT
    // DETECTION TIME rather than the entity to re-read later: a fleet that has gone out of sight is
    // the whole point of one of them, and its name and its place must be the ones from while it
    // could still be seen.

    /// <summary>A fleet of the player's standing at the place it was sent to.</summary>
    public sealed class EventModFleetArrived : EmpireEvent
    {
        public Fleet Fleet { get; private set; }

        public GameNode Destination { get; private set; }

        public EventModFleetArrived(
            Amplitude.Unity.Game.Empire empire,
            Fleet fleet,
            GameNode destination
        )
            : base(empire)
        {
            Fleet = fleet;
            Destination = destination;
        }
    }

    /// <summary>A fleet of the player's stopped short of it - a citadel or a guarding fleet took its
    /// movement away where it stood.</summary>
    public sealed class EventModFleetStopped : EmpireEvent
    {
        public Fleet Fleet { get; private set; }

        public GameNode Where { get; private set; }

        public EventModFleetStopped(Amplitude.Unity.Game.Empire empire, Fleet fleet, GameNode where)
            : base(empire)
        {
            Fleet = fleet;
            Where = where;
        }
    }

    /// <summary>Somebody else's fleet that the player could see and now cannot.</summary>
    public sealed class EventModForeignFleetLost : EmpireEvent
    {
        public Fleet Fleet { get; private set; }

        public Amplitude.Unity.Game.Empire Owner { get; private set; }

        /// <summary>The fleet's name as it read while it could still be seen, or null.</summary>
        public string FleetName { get; private set; }

        /// <summary>Where it was standing while it could still be seen, or null for a fleet last
        /// seen out on a starlane.</summary>
        public GameNode LastSeen { get; private set; }

        public EventModForeignFleetLost(
            Amplitude.Unity.Game.Empire empire,
            Fleet fleet,
            Amplitude.Unity.Game.Empire owner,
            string fleetName,
            GameNode lastSeen
        )
            : base(empire)
        {
            Fleet = fleet;
            Owner = owner;
            FleetName = fleetName;
            LastSeen = lastSeen;
        }
    }

    /// <summary>Somebody else's fleet, already in sight, standing somewhere else when the turn came
    /// round.</summary>
    public sealed class EventModForeignFleetMoved : EmpireEvent
    {
        public Fleet Fleet { get; private set; }

        public Amplitude.Unity.Game.Empire Owner { get; private set; }

        public GameNode From { get; private set; }

        public GameNode To { get; private set; }

        public EventModForeignFleetMoved(
            Amplitude.Unity.Game.Empire empire,
            Fleet fleet,
            Amplitude.Unity.Game.Empire owner,
            GameNode from,
            GameNode to
        )
            : base(empire)
        {
            Fleet = fleet;
            Owner = owner;
            From = from;
            To = to;
        }
    }

    /// <summary>One of the player's own systems whose influence lost ground to a rival over the turn
    /// just ended (<see cref="InfluenceGroundWatch"/> is what notices). One event per system and
    /// taker, however many squares of map changed hands.</summary>
    public sealed class EventModInfluenceGroundLost : EmpireEvent
    {
        public ColonizedStarSystem System { get; private set; }

        /// <summary>The empire whose field now wins ground that was the player's.</summary>
        public Amplitude.Unity.Game.Empire Taker { get; private set; }

        public EventModInfluenceGroundLost(
            Amplitude.Unity.Game.Empire empire,
            ColonizedStarSystem system,
            Amplitude.Unity.Game.Empire taker
        )
            : base(empire)
        {
            System = system;
            Taker = taker;
        }
    }

    /// <summary>
    /// A fleet of the player's reaching where it was sent (<see cref="FleetArrivals"/> is what
    /// notices). No repeat refusal: the same fleet arriving somewhere else three turns later is news
    /// again, and a <c>Subject</c> would have refused it while the first was still standing.
    /// </summary>
    public sealed class FleetArrivedNotification : ModNotification
    {
        private Fleet _fleet;
        private GameNode _at;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventModFleetArrived arrived = gameEvent as EventModFleetArrived;
            if (arrived == null || arrived.Fleet == null || !IsPlayer(arrived.Empire))
            {
                return false;
            }

            _fleet = arrived.Fleet;
            _at = arrived.Destination;
            return true;
        }

        protected override string Title()
        {
            string where = PlaceName(_at);
            string fleet = FleetPhrase.Full(_fleet);
            return where == null
                ? ModStrings.Format(ModStrings.NotificationFleetArrivedPlain, fleet)
                : ModStrings.Format(ModStrings.NotificationFleetArrived, fleet, where);
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return (IGameEntityWithGalaxyPosition)_at
                ?? (_fleet == null || _fleet.IsDestroyed ? null : _fleet);
        }
    }

    /// <summary>
    /// A fleet of the player's stopped where it stood. The sentence is the one the mod already said
    /// for this (<c>ModStrings.FleetInterceptedAt</c>), word for word: what is new is that the news
    /// now also has a place in the log and a Show Location button, not that it reads differently.
    /// </summary>
    public sealed class FleetStoppedNotification : ModNotification
    {
        private Fleet _fleet;
        private GameNode _at;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventModFleetStopped stopped = gameEvent as EventModFleetStopped;
            if (stopped == null || stopped.Fleet == null || !IsPlayer(stopped.Empire))
            {
                return false;
            }

            _fleet = stopped.Fleet;
            _at = stopped.Where;
            return true;
        }

        protected override string Title()
        {
            string where = PlaceName(_at);
            string fleet = FleetPhrase.Full(_fleet);
            return where == null
                ? ModStrings.Format(ModStrings.FleetIntercepted, fleet)
                : ModStrings.Format(ModStrings.FleetInterceptedAt, fleet, where);
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return (IGameEntityWithGalaxyPosition)_at
                ?? (_fleet == null || _fleet.IsDestroyed ? null : _fleet);
        }
    }

    /// <summary>
    /// Somebody else's fleet going out of sight (<see cref="ForeignFleetWatch"/> is what notices).
    /// It says only that sight was lost, never WHY: the game's own downgrade does not say whether the
    /// fleet flew away, cloaked, or the thing that was watching it died, and a mod that guessed would
    /// be telling the player something nobody knows.
    ///
    /// Both the name and the place come off the event rather than off the fleet, because by the time
    /// this reads them the fleet is somewhere the player is not allowed to see.
    /// </summary>
    public sealed class ForeignFleetLostNotification : ModNotification
    {
        private Fleet _fleet;
        private Amplitude.Unity.Game.Empire _owner;
        private string _name;
        private GameNode _lastSeen;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventModForeignFleetLost lost = gameEvent as EventModForeignFleetLost;
            if (lost == null || !IsPlayer(lost.Empire) || lost.Owner == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(EmpireName(lost.Owner)))
            {
                return false;
            }

            _fleet = lost.Fleet;
            _owner = lost.Owner;
            _name = lost.FleetName;
            _lastSeen = lost.LastSeen;
            return true;
        }

        protected override string Title()
        {
            // Which way the player stands to the OWNER, which is the player's own diplomacy and not
            // something read off a fleet nobody can see any more: the fleet's own object is stale by
            // now, and its name and place come off the event for exactly that reason.
            string owner = FleetPhrase.Owned(_owner) ?? EmpireName(_owner);
            string where = PlaceName(_lastSeen);
            if (string.IsNullOrEmpty(_name))
            {
                return where == null
                    ? ModStrings.Format(
                        ModStrings.NotificationFleetLostSightUnnamedNowhere,
                        owner
                    )
                    : ModStrings.Format(
                        ModStrings.NotificationFleetLostSightUnnamed,
                        owner,
                        where
                    );
            }

            return where == null
                ? ModStrings.Format(ModStrings.NotificationFleetLostSightNowhere, owner, _name)
                : ModStrings.Format(ModStrings.NotificationFleetLostSight, owner, _name, where);
        }

        /// <summary>The place it was last seen at, and never the fleet itself: the fleet is where the
        /// player may not look, and the map would fly to it.</summary>
        protected override IGameEntityWithGalaxyPosition Location()
        {
            return _lastSeen;
        }
    }

    /// <summary>
    /// Somebody else's fleet, in sight before the turn and in sight after it, standing somewhere
    /// else. A fleet that went OUT of sight is the lost-sight family's news and never this one's, and
    /// a fleet that is simply gone belongs to the game's own battle notifications - nothing here
    /// invents a reason for an absence.
    /// </summary>
    public sealed class ForeignFleetMovedNotification : ModNotification
    {
        private Fleet _fleet;
        private Amplitude.Unity.Game.Empire _owner;
        private GameNode _from;
        private GameNode _to;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventModForeignFleetMoved moved = gameEvent as EventModForeignFleetMoved;
            if (moved == null || !IsPlayer(moved.Empire) || moved.Owner == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(EmpireName(moved.Owner)))
            {
                return false;
            }

            _fleet = moved.Fleet;
            _owner = moved.Owner;
            _from = moved.From;
            _to = moved.To;
            return PlaceName(_from) != null || PlaceName(_to) != null;
        }

        protected override string Title()
        {
            // The owner's standing, not the fleet's phrase: the fleet is only being said to have
            // MOVED, and nothing new about what it is made of has been shown.
            string owner = FleetPhrase.Owned(_owner) ?? EmpireName(_owner);
            string from = PlaceName(_from);
            string to = PlaceName(_to);
            if (to == null)
            {
                return ModStrings.Format(
                    ModStrings.NotificationForeignFleetMovedAway,
                    owner,
                    from
                );
            }

            return from == null
                ? ModStrings.Format(ModStrings.NotificationForeignFleetMovedTo, owner, to)
                : ModStrings.Format(ModStrings.NotificationForeignFleetMoved, owner, from, to);
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return (IGameEntityWithGalaxyPosition)_to
                ?? (_fleet == null || _fleet.IsDestroyed ? null : _fleet);
        }
    }

    /// <summary>
    /// A border of the player's moving the wrong way: squares that were provably inside one of their
    /// systems' influence, and that a rival's field now wins
    /// (<see cref="InfluenceGroundWatch"/> is what notices).
    ///
    /// No repeat refusal, by the owner's ruling: it is news EVERY turn it happens, because a border
    /// still moving after a turn of it moving is exactly the thing the player needs to keep hearing.
    /// The taker is never the player - a square is only lost when somebody ELSE wins a point of it -
    /// so there is no "your empire" form of the sentence.
    /// </summary>
    public sealed class InfluenceGroundLostNotification : ModNotification
    {
        private ColonizedStarSystem _system;
        private Amplitude.Unity.Game.Empire _taker;

        protected override bool Accept(GameEvent gameEvent)
        {
            EventModInfluenceGroundLost lost = gameEvent as EventModInfluenceGroundLost;
            if (
                lost == null
                || lost.System == null
                || lost.Taker == null
                || !IsPlayer(lost.Empire)
            )
            {
                return false;
            }

            if (string.IsNullOrEmpty(EmpireName(lost.Taker)))
            {
                return false;
            }

            _system = lost.System;
            _taker = lost.Taker;
            return true;
        }

        protected override string Title()
        {
            return ModStrings.Format(
                ModStrings.NotificationInfluenceGroundLost,
                _system.LocalizedName,
                EmpireName(_taker)
            );
        }

        protected override IGameEntityWithGalaxyPosition Location()
        {
            return _system.Node;
        }
    }
}

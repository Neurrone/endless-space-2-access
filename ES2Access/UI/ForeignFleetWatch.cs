using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude.Unity.Framework;
using ES2Access.Core.Util;
using HarmonyLib;

namespace ES2Access.UI
{
    /// <summary>
    /// Somebody else's fleets: the one going out of sight, and the one that was somewhere else when
    /// the turn came round. Both are things a sighted player reads off the map at a glance and the
    /// game announces to nobody.
    ///
    /// GOING OUT OF SIGHT has no bus event. The rise into sight has one - <c>EventEmpireSeen</c>,
    /// raised from the fleet's own layer handler at Visible and above (<c>Fleet</c> :1213-1221) - but
    /// the fall out of it raises nothing, so the write itself is watched:
    /// <c>EntityVisibility.SetLayer</c> (:145-175) is the single path that changes a client layer,
    /// and a prefix there sees the layer that is about to be replaced. Two things make patching the
    /// METHOD right rather than subscribing to its <c>OnLayerChanged</c>: the event is suppressible
    /// with a <c>silent</c> flag, and the SERVER's own layers are a different array entirely
    /// (<c>serverLayers</c>, written by <c>ServerSetLayer</c>) - so the refresh cycle's transient
    /// downgrade (<c>ServerPreRefreshVisibility</c> :237-247 knocks every refreshing empire back to
    /// Known before recomputing) never reaches this method at all: only the NET difference is sent
    /// to the client, as an <c>OrderUpdateVisibility</c> operation (<c>ServerPostRefreshVisibility</c>
    /// :249-262, applied at <c>GameClient</c> :5022).
    ///
    /// MOVING WHILE STILL IN SIGHT has no event either - there is nothing to fire, since nothing
    /// about the fleet changed as far as visibility is concerned. So it is a snapshot diff at the
    /// turn boundary, taken when the client says the turn has begun
    /// (<c>IEndTurnService.GameClientStateChange</c> with <c>GameClientState_Turn_Begin</c>, the
    /// game's own settled-turn signal - <c>VisibilityController</c> :1242-1250 uses the same one).
    ///
    /// The same table answers both. What it holds is what the fleet looked like WHILE IT COULD BE
    /// SEEN - its name and the place it was standing - because a fleet that has just gone out of
    /// sight is somewhere the player is not allowed to know about, and reading its position at that
    /// moment would tell them where it went. Entries are refreshed as fleets come into sight, and
    /// again at every turn boundary; a fleet no longer in sight is dropped at the next boundary,
    /// after the loss has been announced with the payload captured at the time.
    ///
    /// Everything is rebuilt from the live game (<see cref="Baseline"/>) whenever the game changes
    /// under it - a new game, a loaded save, a hot reload - so nothing is ever announced about state
    /// the mod was not there to see.
    /// </summary>
    internal static class ForeignFleetWatch
    {
        /// <summary>Visibility at or above this is "in sight" - the same step the game's own sighting
        /// event fires at (<c>Fleet.Visibility_OnLayerChanged</c> :1213-1221 tests
        /// <c>(int)e.Layer &gt;= 3</c>) and the same one the map's ship count asks for.</summary>
        private const int InSight = (int)EntityVisibility.Layer.Visible;

        private sealed class Seen
        {
            /// <summary>The fleet's name as it read while it could be seen.</summary>
            public string Name;

            /// <summary>Where it was standing, or null for one out on a starlane.</summary>
            public GameNode Where;

            /// <summary>Seen on this turn's pass.</summary>
            public bool Present;
        }

        private struct Lost
        {
            public Fleet Fleet;

            public Empire Owner;

            public string Name;

            public GameNode LastSeen;
        }

        private static Harmony _harmony;
        private static FieldInfo _entityField;
        private static IEndTurnService _endTurn;
        private static bool _turnBegan;
        private static bool _reportedFailure;

        private static readonly Dictionary<ulong, Seen> _seen = new Dictionary<ulong, Seen>();
        private static readonly List<Lost> _lost = new List<Lost>(2);

        /// <summary>Whether the visibility patch is in place - what the teardown check reads.
        /// </summary>
        public static bool Installed
        {
            get { return _harmony != null; }
        }

        /// <summary>How many foreign fleets are being remembered - what a probe asks to see that a
        /// teardown really let go.</summary>
        public static int Watching
        {
            get { return _seen.Count; }
        }

        /// <summary>Whether the turn boundary is being listened to - the other half of this watch,
        /// and the half a teardown could leave behind on a service that outlives the assembly.
        /// </summary>
        public static bool Subscribed
        {
            get { return _endTurn != null; }
        }

        /// <summary>Who is patching the visibility write right now (see <see cref="ModPatches"/>).
        /// </summary>
        internal static string[] Owners()
        {
            return ModPatches.Owners(Writing(), true);
        }

        private static MethodInfo Writing()
        {
            return AccessTools.Method(
                typeof(EntityVisibility),
                "SetLayer",
                new[]
                {
                    typeof(Empire),
                    typeof(EntityVisibility.Layer),
                    typeof(bool),
                    typeof(bool),
                }
            );
        }

        public static void Install()
        {
            Remove();

            Harmony harmony = new Harmony(
                "endless.space2.access.foreignfleets." + Guid.NewGuid().ToString("N")
            );

            try
            {
                _entityField = typeof(EntityVisibility).GetField(
                    "gameEntity",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                if (_entityField == null)
                {
                    throw new MissingFieldException(
                        typeof(EntityVisibility).FullName,
                        "gameEntity"
                    );
                }

                MethodInfo setLayer = Writing();
                if (setLayer == null)
                {
                    throw new MissingMethodException(
                        typeof(EntityVisibility).FullName,
                        "SetLayer"
                    );
                }

                harmony.Patch(
                    setLayer,
                    new HarmonyMethod(
                        typeof(ForeignFleetWatch).GetMethod(
                            "LayerChanging",
                            BindingFlags.Static | BindingFlags.NonPublic
                        )
                    )
                );
                _harmony = harmony;
            }
            catch (Exception e)
            {
                // Unpatched, a fleet going out of sight is as silent as it has always been; the
                // turn-boundary half still works.
                Log.Error("foreign fleets could not be watched: " + e);
                try
                {
                    harmony.UnpatchSelf();
                }
                catch (Exception undo)
                {
                    Log.Warn("and the partial patch could not be undone: " + undo.Message);
                }
            }
        }

        /// <summary>Hand everything back: the patch, the turn subscription to a service that outlives
        /// this assembly, and the table.</summary>
        public static void Remove()
        {
            Harmony harmony = _harmony;
            _harmony = null;
            _reportedFailure = false;
            Unsubscribe();
            _seen.Clear();
            _lost.Clear();
            _turnBegan = false;
            _entityField = null;

            if (harmony == null)
            {
                return;
            }

            try
            {
                harmony.UnpatchSelf();
            }
            catch (Exception e)
            {
                Log.Error("foreign fleets could not be unpatched: " + e);
            }
        }

        /// <summary>Start from what is on the screen now, saying nothing about any of it.</summary>
        public static void Baseline()
        {
            _seen.Clear();
            _lost.Clear();
            _turnBegan = false;
            Sweep(false);
        }

        /// <summary>
        /// Say what happened: first the fleets that went out of sight, then - once the game says the
        /// turn has begun - the ones that are standing somewhere else. Both go on the game's own bus
        /// as mod events and come back as notifications, which is what speaks them.
        /// </summary>
        public static void Tick()
        {
            try
            {
                Follow();
                Announce();
                if (!_turnBegan)
                {
                    return;
                }

                _turnBegan = false;
                Sweep(true);
            }
            catch (Exception e)
            {
                Log.Warn("foreign fleets: watching them threw: " + e);
            }
        }

        /// <summary>Keep the turn subscription pointed at the game that is actually running. A new
        /// game, a loaded save and a hot reload all arrive as a DIFFERENT service instance, and each
        /// of them is a galaxy the mod has not been watching - so the table starts over rather than
        /// comparing against a game it was not there for.</summary>
        private static void Follow()
        {
            IEndTurnService service = null;
            try
            {
                service = Services.GetService<IEndTurnService>();
            }
            catch (Exception)
            {
                service = null;
            }

            if (ReferenceEquals(service, _endTurn))
            {
                return;
            }

            Unsubscribe();
            _endTurn = service;
            if (service == null)
            {
                _seen.Clear();
                _lost.Clear();
                return;
            }

            service.GameClientStateChange += TurnStateChanged;
            Baseline();
        }

        private static void Unsubscribe()
        {
            IEndTurnService service = _endTurn;
            _endTurn = null;
            if (service == null)
            {
                return;
            }

            try
            {
                service.GameClientStateChange -= TurnStateChanged;
            }
            catch (Exception e)
            {
                Log.Warn("foreign fleets: unsubscribing from the turn threw: " + e);
            }
        }

        /// <summary>Runs inside the game's own state machine: raises a flag and returns. The diff is
        /// the pump's.</summary>
        private static void TurnStateChanged(object sender, GameClientStateChangeEventArgs e)
        {
            if (e != null && e.GameClientStateType == typeof(GameClientState_Turn_Begin))
            {
                _turnBegan = true;
            }
        }

        private static void Announce()
        {
            if (_lost.Count == 0)
            {
                return;
            }

            List<Lost> gone = new List<Lost>(_lost);
            _lost.Clear();
            Empire player = Gui.PlayerEmpire;
            if (player == null)
            {
                return;
            }

            for (int i = 0; i < gone.Count; i++)
            {
                ModNotifications.Raise(
                    new EventModForeignFleetLost(
                        player,
                        gone[i].Fleet,
                        gone[i].Owner,
                        gone[i].Name,
                        gone[i].LastSeen
                    )
                );
            }
        }

        /// <summary>
        /// Walk every foreign fleet that is in sight, tell the ones that have moved, and leave the
        /// table describing the galaxy as it stands. A fleet that is no longer in it is dropped
        /// without a word - its going out of sight has already been announced by the other half, and
        /// a fleet that is simply gone is the game's own battle news.
        ///
        /// The walk is over the EMPIRES' own fleet lists rather than over what the map is drawing.
        /// The drawn set is a view: measured 2026-08-20, the galaxy fleet repository the map's labels
        /// come from answered EMPTY for two fleets parked at a system, so a diff built on it would
        /// quietly stop noticing anything. Visibility is the model's own answer to the same question
        /// and is what the labels are gated on anyway.
        /// </summary>
        private static void Sweep(bool announce)
        {
            Empire player = Gui.PlayerEmpire;
            MajorEmpire major = player as MajorEmpire;
            Game game = Gui.Game as Game;
            if (player == null || major == null || game == null || game.Empires == null)
            {
                _seen.Clear();
                return;
            }

            foreach (KeyValuePair<ulong, Seen> pair in _seen)
            {
                pair.Value.Present = false;
            }

            for (int e = 0; e < game.Empires.Length; e++)
            {
                Empire owner = game.Empires[e];
                if (owner == null || ReferenceEquals(owner, player))
                {
                    continue;
                }

                DepartmentOfDefense defense = owner.GetAgency<DepartmentOfDefense>();
                IList<Fleet> fleets = defense == null ? null : defense.Fleets;
                for (int i = 0; fleets != null && i < fleets.Count; i++)
                {
                    Fleet fleet = fleets[i];
                    if (!Foreign(fleet, player) || !InSightOf(fleet, major))
                    {
                        continue;
                    }

                    Look(fleet, owner, player, announce);
                }
            }

            Drop();
        }

        private static void Look(Fleet fleet, Empire owner, Empire player, bool announce)
        {
            ulong key = fleet.GUID.ToUInt64(null);
            GameNode where = fleet.GetGameNode();
            Seen known;
            if (!_seen.TryGetValue(key, out known))
            {
                // Not known: a fleet that came into sight without this watch being there for it, so
                // where it is now is simply where it is. Never news.
                _seen[key] = new Seen
                {
                    Name = fleet.LocalizedName,
                    Where = where,
                    Present = true,
                };
                return;
            }

            if (announce && !Same(known.Where, where))
            {
                ModNotifications.Raise(
                    new EventModForeignFleetMoved(player, fleet, owner, known.Where, where)
                );
            }

            known.Name = fleet.LocalizedName;
            known.Where = where;
            known.Present = true;
        }

        private static void Drop()
        {
            List<ulong> gone = null;
            foreach (KeyValuePair<ulong, Seen> pair in _seen)
            {
                if (!pair.Value.Present)
                {
                    if (gone == null)
                    {
                        gone = new List<ulong>(2);
                    }

                    gone.Add(pair.Key);
                }
            }

            for (int i = 0; gone != null && i < gone.Count; i++)
            {
                _seen.Remove(gone[i]);
            }
        }

        private static bool Same(GameNode one, GameNode two)
        {
            if (one == null || two == null)
            {
                return one == null && two == null;
            }

            return one.GUID == two.GUID;
        }

        private static bool Foreign(Fleet fleet, Empire player)
        {
            return fleet != null
                && !fleet.IsDestroyed
                && fleet.Empire != null
                && !ReferenceEquals(fleet.Empire, player);
        }

        private static bool InSightOf(Fleet fleet, MajorEmpire empire)
        {
            EntityVisibility visibility = fleet.Visibility;
            return visibility != null && (int)visibility[empire] >= InSight;
        }

        /// <summary>
        /// Runs inside the game's visibility pass, for every entity and every empire: gets out on a
        /// reference comparison for anything that is not the player looking, remembers a fleet that
        /// has come into sight, and remembers the loss of one that has gone out of it. Nothing here
        /// speaks or raises anything - that is <see cref="Tick"/>'s.
        /// </summary>
        private static void LayerChanging(
            EntityVisibility __instance,
            Empire empire,
            EntityVisibility.Layer layer
        )
        {
            try
            {
                Empire player = Gui.PlayerEmpire;
                if (player == null || !ReferenceEquals(empire, player))
                {
                    return;
                }

                // Only the two steps ACROSS the sight boundary matter: the layer has five values and
                // moving between two of them on the same side of it changes nothing the player can
                // be told.
                bool nowInSight = (int)layer >= InSight;
                bool wasInSight = (int)__instance[empire] >= InSight;
                if (nowInSight == wasInSight)
                {
                    return;
                }

                Fleet fleet = _entityField == null
                    ? null
                    : _entityField.GetValue(__instance) as Fleet;
                if (!Foreign(fleet, player))
                {
                    return;
                }

                ulong key = fleet.GUID.ToUInt64(null);
                if (nowInSight)
                {
                    // Come into sight: remember it as it looks now, which is what the loss will one
                    // day be described with. The sighting itself is the game's own event
                    // (EventEmpireSeen) and is announced from there.
                    _seen[key] = new Seen
                    {
                        Name = fleet.LocalizedName,
                        Where = fleet.GetGameNode(),
                        Present = true,
                    };
                    return;
                }

                Seen known;
                bool remembered = _seen.TryGetValue(key, out known);
                _lost.Add(
                    new Lost
                    {
                        Fleet = fleet,
                        Owner = fleet.Empire,
                        Name = remembered ? known.Name : fleet.LocalizedName,
                        LastSeen = remembered ? known.Where : fleet.GetGameNode(),
                    }
                );
                _seen.Remove(key);
            }
            catch (Exception e)
            {
                // Runs inside the game's own visibility pass: say so once rather than once per
                // entity per empire.
                if (!_reportedFailure)
                {
                    _reportedFailure = true;
                    Log.Warn("foreign fleets: noticing a visibility change threw: " + e);
                }
            }
        }
    }
}

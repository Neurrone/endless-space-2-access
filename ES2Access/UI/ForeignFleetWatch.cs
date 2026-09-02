using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude.Unity.Framework;
using ES2Access.Core.UI;
using ES2Access.Core.Util;
using HarmonyLib;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// Somebody else's fleets: the one that has come into sight, the one going out of it, and the one
    /// that was somewhere else when the turn came round. All three are things a sighted player reads
    /// off the map at a glance and the game announces to nobody.
    ///
    /// COMING INTO SIGHT and GOING OUT OF IT are the same write and are watched as one.
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
    /// The game's own sighting event (<c>EventEmpireSeen</c>, <c>Fleet</c> :1213-1221) is NOT what
    /// announces a fleet, and deliberately: it fires on the server's every recomputation, so a fleet
    /// merely passing through somebody's detection range rises into sight and falls out of it again
    /// within a second, with nothing drawn on the player's screen for either step (measured
    /// 2026-09-02: one such pair a second apart, mid-lane). So every crossing goes through
    /// <see cref="SettledSight"/> first and is news only once it has held
    /// <see cref="SettleSeconds"/> seconds - and a crossing that reverses inside its own window
    /// leaves the fleet exactly where it stood, with no sighting and no loss. That covers a
    /// same-frame Visible+Known pair too, which a whole applied batch routinely contains
    /// (<c>GameClient</c> :5004-5024 applies one in a single frame).
    ///
    /// MOVING WHILE STILL IN SIGHT has no event at all - nothing about the fleet changed as far as
    /// visibility is concerned - so it is a snapshot diff taken once a turn. The turn's own signal
    /// (<c>GameClientState_Turn_Begin</c>) is too EARLY to diff on: visibility operations are held
    /// from <c>GameServerState_Turn_End</c> :55 until clients reach Turn_Main, so at Turn_Begin the
    /// client's layers are last turn's and the fleets are still animating along their lanes. The
    /// sweep is therefore only ARMED at the boundary and runs once the client is in
    /// <c>GameClientState_Turn_Main</c> and the map has stood still for
    /// <see cref="QuietSeconds"/> seconds - or, failing that, <see cref="SweepCapSeconds"/> seconds
    /// after Turn_Main, so a galaxy that never settles still gets its turn's news.
    ///
    /// The same table answers all three. What it holds is what the fleet looked like WHILE IT COULD
    /// BE SEEN - its name and the place it was standing - because a fleet that has gone out of sight
    /// is somewhere the player is not allowed to know about, and reading its position at that moment
    /// would tell them where it went. Entries outlive the loss on purpose: the last place a fleet was
    /// seen is where Show Location on its sighting line has to go, long after the fleet itself has
    /// become somewhere the map may not fly to.
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
        private const int InSightLayer = (int)EntityVisibility.Layer.Visible;

        /// <summary>How long a crossing has to hold before the player is told about it (owner ruling
        /// 2026-09-02). Longer than the server's 0.5 s batching cadence and longer than the
        /// second-apart flashes measured mid-lane, because the question is what a player could have
        /// READ off the map rather than what the wire carried.</summary>
        private const float SettleSeconds = 2f;

        /// <summary>How still the watched fleets have to be before the turn's moved-fleet diff is
        /// taken - the same length as the settle window, so every crossing in the turn's batch has
        /// already settled by the time the sweep runs.</summary>
        private const float QuietSeconds = 2f;

        /// <summary>How long the sweep waits for that stillness before taking the diff anyway. A
        /// galaxy where something is always moving would otherwise never report a turn.</summary>
        private const float SweepCapSeconds = 15f;

        /// <summary>How long after a galaxy changes under the watch it goes on adopting whatever the
        /// map is drawing, silently. A load hands the client its visibility over many seconds and
        /// by a path that is NOT the write this watch is on, so the one baseline taken the moment
        /// the game object appears sees a galaxy that is not finished arriving yet (measured
        /// 2026-09-02: the fixture's one visible pirate read Visible with no crossing ever seen).
        /// Nothing that happens in this window is news to a player who has just arrived.</summary>
        private const float AdoptSeconds = 20f;

        /// <summary>How often that adoption re-walks the fleet lists, in seconds - often enough to
        /// catch the load's own steps, rarely enough to cost nothing.</summary>
        private const float AdoptEvery = 0.25f;

        private sealed class Seen
        {
            /// <summary>The fleet itself - kept so a crossing that settles two seconds later still
            /// knows what it is about, and so a sighting line can ask whether the fleet is still
            /// drawn before offering to fly to it.</summary>
            public Fleet Fleet;

            public Empire Owner;

            /// <summary>The fleet's name as it read while it could be seen.</summary>
            public string Name;

            /// <summary>The last NODE it was observed at while in sight, or null for one only ever
            /// seen out on a starlane. Never re-read once it goes out of sight.</summary>
            public GameNode Where;

            /// <summary>Where it stood at the last settled sweep - what the next sweep's "somewhere
            /// else" is measured against.</summary>
            public GameNode SweptAt;

            /// <summary>Its position at the last quiet-window poll, so a fleet still animating along
            /// a lane holds the sweep back.</summary>
            public GalaxyPosition Position;
        }

        private static readonly ModPatch Patches = new ModPatch(
            "foreignfleets",
            "foreign fleets"
        );

        private static FieldInfo _entityField;
        private static IEndTurnService _endTurn;
        private static object _game;
        private static bool _sweepPending;
        private static bool _turnMain;
        private static float _turnMainAt;
        private static float _disturbed;
        private static float _adoptUntil;
        private static float _adopted;

        private static readonly SettledSight _settled = new SettledSight(SettleSeconds);
        private static readonly Dictionary<ulong, Seen> _seen = new Dictionary<ulong, Seen>();

        /// <summary>Whether the visibility patch is in place - what the teardown check reads.
        /// </summary>
        public static bool Installed
        {
            get { return Patches.Installed; }
        }

        /// <summary>How many foreign fleets are being remembered - what a probe asks to see that a
        /// teardown really let go.</summary>
        public static int Watching
        {
            get { return _seen.Count; }
        }

        /// <summary>How many foreign fleets the player has been TOLD are in sight - the settled
        /// answer, which is smaller than <see cref="Watching"/> by every fleet remembered only for
        /// where it was last seen.</summary>
        public static int InSight
        {
            get { return _settled.InSightCount; }
        }

        /// <summary>How many crossings are waiting out their settle window right now - what a probe
        /// reads to watch the debounce hold a flash back.</summary>
        public static int Pending
        {
            get { return _settled.PendingCount; }
        }

        /// <summary>Whether the turn's moved-fleet diff is armed and waiting for the map to settle.
        /// </summary>
        public static bool SweepPending
        {
            get { return _sweepPending; }
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

        /// <summary>Whether the player's client is drawing this fleet at this instant - the question
        /// a sighting line asks before offering to fly the map to it. The settled answer is a
        /// different question and this is deliberately not it: the button must not fly to a fleet the
        /// screen has already stopped drawing, however recently it was news.</summary>
        public static bool Drawn(Fleet fleet)
        {
            try
            {
                MajorEmpire major = Gui.PlayerEmpire as MajorEmpire;
                return major != null && Foreign(fleet, Gui.PlayerEmpire) && InSightOf(fleet, major);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The last node this fleet was observed standing at WHILE IN SIGHT, or null for one
        /// only ever seen out on a starlane - which is where Show Location goes once the fleet itself
        /// is somewhere the player may not look. Null is the honest answer and hides the button.
        /// </summary>
        public static GameNode LastSeen(Fleet fleet)
        {
            if (fleet == null)
            {
                return null;
            }

            try
            {
                Seen known;
                return _seen.TryGetValue(fleet.GUID.ToUInt64(null), out known) ? known.Where : null;
            }
            catch (Exception)
            {
                return null;
            }
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
            Patches.Install(
                patch =>
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

                    patch.Prefix(Writing(), typeof(ForeignFleetWatch), "LayerChanging");
                }
            );
        }

        /// <summary>Hand everything back: the patch, the turn subscription to a service that outlives
        /// this assembly, and the table.</summary>
        public static void Remove()
        {
            Patches.Remove();
            Unsubscribe();
            Forget();
            _game = null;
            _entityField = null;
        }

        /// <summary>Start from what is on the screen now, saying nothing about any of it - and go on
        /// starting over for <see cref="AdoptSeconds"/>, because a galaxy does not finish arriving in
        /// the frame its <c>Game</c> does (<see cref="Arriving"/>).</summary>
        public static void Baseline()
        {
            Forget();
            _adoptUntil = Now() + AdoptSeconds;
            _adopted = 0f;
            Sweep(false);
        }

        private static void Forget()
        {
            _seen.Clear();
            _settled.Reset();
            _sweepPending = false;
            _turnMain = false;
            _adoptUntil = 0f;
        }

        /// <summary>
        /// Say what happened: the crossings that have held long enough to have been seen, and - once
        /// the turn's own news has landed and the map has stopped moving - the fleets that are
        /// standing somewhere else. Both go on the game's own bus as mod events and come back as
        /// notifications, which is what speaks them.
        /// </summary>
        public static void Tick()
        {
            try
            {
                Follow();
                Arriving();
                Settle();
                Sweeping();
            }
            catch (Exception e)
            {
                Log.Warn("foreign fleets: watching them threw: " + e);
            }
        }

        /// <summary>
        /// Keep the turn subscription and the table pointed at the game that is actually running.
        ///
        /// The two are asked SEPARATELY because they change at different moments. The end-turn
        /// service is <c>GameManager</c>, a singleton that survives everything (measured 2026-09-02:
        /// the same instance either side of a save load), so it is the SUBSCRIPTION's signal and
        /// nothing else. What changes when a galaxy does is the <c>Game</c> itself - a new game, a
        /// loaded save and a reload each hand over a different instance - and that is the signal to
        /// start the table over, because everything in it describes fleets that no longer exist and
        /// is keyed by a GUID the new galaxy uses for somebody else.
        /// </summary>
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

            object game = null;
            try
            {
                game = Gui.Game;
            }
            catch (Exception)
            {
                game = null;
            }

            if (ReferenceEquals(service, _endTurn) && ReferenceEquals(game, _game))
            {
                return;
            }

            if (!ReferenceEquals(service, _endTurn))
            {
                Unsubscribe();
                _endTurn = service;
                if (service != null)
                {
                    service.GameClientStateChange += TurnStateChanged;
                }
            }

            _game = game;
            if (service == null || game == null)
            {
                Forget();
                return;
            }

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

        /// <summary>Runs inside the game's own state machine: raises a flag and returns. The turn
        /// boundary ARMS the sweep and no more - the visibility operations the diff has to see are
        /// held until clients reach Turn_Main (<c>GameServerState_Turn_End</c> :55,
        /// <c>GameServerState_Turn_AI_Begin</c> :51), and the fleets are still animating for a while
        /// after that.</summary>
        private static void TurnStateChanged(object sender, GameClientStateChangeEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (e.GameClientStateType == typeof(GameClientState_Turn_Begin))
            {
                _sweepPending = true;
                _turnMain = false;
                return;
            }

            if (e.GameClientStateType == typeof(GameClientState_Turn_Main) && !_turnMain)
            {
                _turnMain = true;
                _turnMainAt = Now();
                _disturbed = _turnMainAt;
            }
        }

        private static float Now()
        {
            return Time.realtimeSinceStartup;
        }

        /// <summary>While a galaxy is still arriving, keep taking the baseline rather than taking it
        /// once and believing it. Everything drawn is adopted in silence, which is what a baseline
        /// means, and no crossing seen inside the window is ever news - a player who has just loaded
        /// a save is not owed a report of the galaxy appearing around them.</summary>
        private static void Arriving()
        {
            float now = Now();
            if (now >= _adoptUntil || now - _adopted < AdoptEvery)
            {
                return;
            }

            _adopted = now;
            Sweep(false);
        }

        /// <summary>
        /// The crossings that have held their window, turned into news. A sighting's whole payload is
        /// read HERE and frozen into the event - the owner's standing, the fleet's name, what it is
        /// made of and where it is standing - because the line has earned exactly what the map showed
        /// at this moment and must never learn anything afterwards.
        /// </summary>
        private static void Settle()
        {
            IList<SettledSight.Change> due = _settled.Due(Now());
            if (due == null)
            {
                return;
            }

            Empire player = Gui.PlayerEmpire;
            for (int i = 0; i < due.Count; i++)
            {
                Seen known;
                if (!_seen.TryGetValue(due[i].Key, out known))
                {
                    continue;
                }

                if (player == null)
                {
                    continue;
                }

                if (due[i].InSight)
                {
                    Sighted(due[i].Key, known, player);
                }
                else
                {
                    Lost(known, player);
                }
            }
        }

        private static void Sighted(ulong key, Seen known, Empire player)
        {
            Fleet fleet = known.Fleet;
            if (!Foreign(fleet, player))
            {
                // Destroyed while its sighting was settling: there was never anything to announce,
                // and the battle is the game's own news. Forgetting it is also what stops a loss
                // being announced for a sighting nobody heard.
                _settled.Forget(key);
                return;
            }

            GameNode where = fleet.GetGameNode();
            known.Name = fleet.LocalizedName;
            known.Owner = fleet.Empire;
            known.SweptAt = where;
            known.Position = fleet.GalaxyPosition;
            if (where != null)
            {
                known.Where = where;
            }

            ModNotifications.Raise(
                new EventModForeignFleetSighted(
                    player,
                    fleet,
                    known.Owner,
                    FleetPhrase.Owned(fleet),
                    known.Name,
                    FleetPhrase.Full(fleet, false),
                    where
                )
            );
        }

        private static void Lost(Seen known, Empire player)
        {
            ModNotifications.Raise(
                new EventModForeignFleetLost(
                    player,
                    known.Fleet,
                    known.Owner,
                    known.Name,
                    known.Where
                )
            );
        }

        /// <summary>
        /// The turn's moved-fleet diff, held back until the picture the diff is about is the one the
        /// player is looking at: the client in Turn_Main, and no watched fleet crossing the sight
        /// boundary or moving for a whole quiet window. The cap is what keeps a galaxy that never
        /// stops moving from silently dropping a turn's news.
        /// </summary>
        private static void Sweeping()
        {
            if (!_sweepPending || !_turnMain)
            {
                return;
            }

            float now = Now();
            if (Stirring(now))
            {
                _disturbed = now;
            }

            if (now - _disturbed < QuietSeconds && now - _turnMainAt < SweepCapSeconds)
            {
                return;
            }

            _sweepPending = false;
            Sweep(true);
        }

        /// <summary>Whether anything the player can see has moved since the last poll - a fleet still
        /// animating along its lane is the usual answer, and movement carries on well into Turn_Main.
        /// Only fleets that are BOTH settled in sight and drawn right now are asked: where a fleet
        /// the player cannot see has got to is not the mod's business and must not steer its
        /// timing.</summary>
        private static bool Stirring(float now)
        {
            MajorEmpire major = Gui.PlayerEmpire as MajorEmpire;
            if (major == null)
            {
                return false;
            }

            bool moved = false;
            foreach (KeyValuePair<ulong, Seen> pair in _seen)
            {
                Seen known = pair.Value;
                if (
                    !_settled.InSightNow(pair.Key)
                    || !Foreign(known.Fleet, major)
                    || !InSightOf(known.Fleet, major)
                )
                {
                    continue;
                }

                GalaxyPosition at = known.Fleet.GalaxyPosition;
                if (!(at == known.Position))
                {
                    known.Position = at;
                    moved = true;
                }

                GameNode where = known.Fleet.GetGameNode();
                if (where != null)
                {
                    known.Where = where;
                }
            }

            return moved;
        }

        /// <summary>
        /// Walk every foreign fleet the player has been told about, tell the ones that have moved,
        /// and leave the table describing the galaxy as it stands. A fleet that is no longer in sight
        /// is left alone - its going out of sight is the settle window's business and may still be
        /// waiting out its own clock - and a fleet that is simply gone is the game's own battle news.
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
                Forget();
                return;
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
        }

        private static void Look(Fleet fleet, Empire owner, Empire player, bool announce)
        {
            ulong key = fleet.GUID.ToUInt64(null);
            GameNode where = fleet.GetGameNode();
            Seen known;
            if (!_seen.TryGetValue(key, out known))
            {
                known = new Seen();
                _seen[key] = known;
            }

            if (!announce)
            {
                // The baseline: every fleet in sight is one that was in sight before the mod was
                // here, so it is HELD - settled in sight, never announced - and where it is now is
                // simply where it is.
                _settled.Hold(key);
            }
            else if (_settled.Settling(key))
            {
                // A crossing still waiting out its window, which the sweep's cap can overtake. Its
                // own commit reads its payload and sets its place a moment from now; a sweep must
                // not announce a move for a fleet whose arrival has not been announced yet, nor
                // adopt one whose news is about to be told.
                return;
            }
            else if (!_settled.InSightNow(key))
            {
                // Drawn and unaccounted for: the client's layers can be restored by a path that is
                // not the write this watch is on (a load writes them straight out of the save), so a
                // fleet can be on the screen with no crossing ever seen. Adopting it silently is the
                // same answer the baseline gives - it was there before the mod was looking - and the
                // alternative is a fleet that could never afterwards be reported moving or lost.
                _settled.Hold(key);
            }
            else if (!Same(known.SweptAt, where))
            {
                ModNotifications.Raise(
                    new EventModForeignFleetMoved(player, fleet, owner, known.SweptAt, where)
                );
            }

            known.Fleet = fleet;
            known.Owner = owner;
            known.Name = fleet.LocalizedName;
            known.SweptAt = where;
            known.Position = fleet.GalaxyPosition;
            if (where != null)
            {
                known.Where = where;
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

        private static bool Foreign(Fleet fleet, Amplitude.Unity.Game.Empire player)
        {
            return fleet != null
                && !fleet.IsDestroyed
                && fleet.Empire != null
                && !ReferenceEquals(fleet.Empire, player);
        }

        private static bool InSightOf(Fleet fleet, MajorEmpire empire)
        {
            EntityVisibility visibility = fleet.Visibility;
            return visibility != null && (int)visibility[empire] >= InSightLayer;
        }

        /// <summary>
        /// Runs inside the game's visibility pass, for every entity and every empire: gets out on a
        /// reference comparison for anything that is not the player looking, and hands a crossing of
        /// the sight boundary to the settle window. Nothing here speaks, raises anything or reads a
        /// payload - a crossing is not yet news, and what a crossing that becomes news says is read
        /// at the moment it does (<see cref="Settle"/>).
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
                bool nowInSight = (int)layer >= InSightLayer;
                bool wasInSight = (int)__instance[empire] >= InSightLayer;
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
                Seen known;
                if (!_seen.TryGetValue(key, out known))
                {
                    known = new Seen();
                    _seen[key] = known;
                }

                known.Fleet = fleet;
                known.Owner = fleet.Empire;
                if (known.Name == null)
                {
                    known.Name = fleet.LocalizedName;
                }

                _settled.Note(key, nowInSight, Now());
                _disturbed = Now();
            }
            catch (Exception e)
            {
                // Runs inside the game's own visibility pass: say so once rather than once per
                // entity per empire.
                Patches.Report("foreign fleets: noticing a visibility change threw", e);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// GROUND THE PLAYER'S OWN SYSTEMS LOST THIS TURN, reported once per turn and per system.
    ///
    /// Influence is how a border moves in this game, and it moves on its own: a rival colony grows,
    /// its field climbs past yours, and squares that were inside your reach are inside theirs. That is
    /// how a system changes hands without a shot, and the game says nothing about it at all - the map
    /// simply paints a different colour over a stretch of sky the player may not be looking at.
    ///
    /// WHAT COUNTS AS LOST is two proofs and not one (owner ruling, 2026-08-21). A square is lost only
    /// where last turn it was CERTIFIED entirely the player's - <see cref="Core.UI.InfluenceCell"/>'s
    /// certificate over the game's own point answers - AND this turn at least one exact point inside
    /// it answers an empire other than the player. A border merely coming CLOSER says nothing: a tile
    /// dropping from "provably mine" to "on the edge" with nobody winning a point of it is a margin
    /// thinning, not ground changing hands, and reporting it would make the line fire on the growth of
    /// a rival colony fifty units away.
    ///
    /// ONE LINE PER (SYSTEM, TAKER), not one per square: a border moving is one piece of news however
    /// many squares it crossed, and a square is not a thing the player can go and look at.
    ///
    /// WHEN is the turn boundary the game itself calls settled -
    /// <c>IEndTurnService.GameClientStateChange</c> at <c>GameClientState_Turn_Begin</c>, the same
    /// signal <see cref="ForeignFleetWatch"/> diffs on and the same one the game's own visibility
    /// controller waits for. The flag is raised inside the state machine and the sweep is the pump's,
    /// because a hook never speaks and never walks the galaxy.
    ///
    /// THE SNAPSHOT IS IN MEMORY ONLY. A hot reload, a loaded save or a new game leaves nothing to
    /// compare against, so the first turn boundary observed after any of them is the BASELINE and
    /// reports nothing - the same rule every other watcher in the mod follows, and the reason none of
    /// them ever announces state the mod was not there for. Nothing is written to a save and nothing
    /// survives <see cref="Remove"/>.
    ///
    /// The news reaches the player as a mod notification (<see cref="InfluenceGroundLostNotification"/>)
    /// rather than as a spoken line, so that it is also a row in the turn log with a Show Location
    /// button on the system it happened to - which is what a player wants from this one, since the
    /// answer to "what do I do about it" is on the map.
    /// </summary>
    internal static class InfluenceGroundWatch
    {
        /// <summary>What one square was last turn: provably the player's, and which of the player's
        /// systems the game answered with over it. Only the certified squares are kept - a square that
        /// was already contested cannot be lost, because it was not held.</summary>
        private static readonly Dictionary<long, ColonizedStarSystem> _was =
            new Dictionary<long, ColonizedStarSystem>();

        private static IEndTurnService _endTurn;
        private static bool _turnBegan;
        private static bool _known;
        private static Empire _empire;

        /// <summary>How many squares the last sweep classified, and what the game was asked to answer
        /// it - the cost figure, read by a probe rather than guessed at.</summary>
        public static int Tiles { get; private set; }

        public static int Queries { get; private set; }

        /// <summary>How long the last sweep took, in milliseconds.</summary>
        public static long Milliseconds { get; private set; }

        /// <summary>Whether the turn boundary is being listened to - the half of this watch a teardown
        /// could leave behind on a service that outlives the assembly.</summary>
        public static bool Subscribed
        {
            get { return _endTurn != null; }
        }

        /// <summary>How many squares are being remembered - what a probe asks to see that a teardown
        /// really let go.</summary>
        public static int Watching
        {
            get { return _was.Count; }
        }

        /// <summary>Hand back the subscription and forget the galaxy. The next boundary is a baseline.
        /// </summary>
        public static void Remove()
        {
            Unsubscribe();
            Forget();
        }

        private static void Forget()
        {
            _was.Clear();
            _known = false;
            _turnBegan = false;
            _empire = null;
            Tiles = 0;
            Queries = 0;
            Milliseconds = 0;
        }

        /// <summary>Once per frame from the pump: keep the subscription pointed at the game that is
        /// running, and sweep once when the turn has come round.</summary>
        public static void Tick()
        {
            try
            {
                Follow();
                if (!_turnBegan)
                {
                    return;
                }

                _turnBegan = false;
                Sweep();
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: watching the ground the player's influence holds threw: " + e);
            }
        }

        /// <summary>Keep the turn subscription on the game that is actually running. A new game, a
        /// loaded save and a hot reload all arrive as a DIFFERENT service instance, and each is a
        /// galaxy the mod has not been watching - so the table starts over rather than comparing
        /// against one it was not there for.</summary>
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
            Forget();
            _endTurn = service;
            if (service != null)
            {
                service.GameClientStateChange += TurnStateChanged;
            }
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
                Log.Warn("galaxy: unsubscribing the influence watch from the turn threw: " + e);
            }
        }

        /// <summary>Runs inside the game's own state machine: raises a flag and returns.</summary>
        private static void TurnStateChanged(object sender, GameClientStateChangeEventArgs e)
        {
            if (e != null && e.GameClientStateType == typeof(GameClientState_Turn_Begin))
            {
                _turnBegan = true;
            }
        }

        /// <summary>One pass of the sweep, compared against the last one, and the table left describing
        /// the galaxy as it now stands.</summary>
        private static void Sweep()
        {
            Empire empire = Gui.PlayerEmpire;
            if (empire == null)
            {
                Forget();
                return;
            }

            // Another empire is another galaxy: the table indexes squares of the one the mod was
            // watching, and the first boundary of the new one is its baseline.
            if (!ReferenceEquals(empire, _empire))
            {
                _empire = empire;
                _known = false;
                _was.Clear();
            }

            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            int queries;
            List<GroundTile> now = InfluenceGround.Sweep(empire, out queries);
            clock.Stop();
            Tiles = now.Count;
            Queries = queries;
            Milliseconds = clock.ElapsedMilliseconds;

            List<Loss> lost = _known ? Diff(now) : null;
            _was.Clear();
            for (int i = 0; i < now.Count; i++)
            {
                if (now[i].Whole && now[i].Held != null)
                {
                    _was[InfluenceGround.Key(now[i].X, now[i].Y)] = now[i].Held;
                }
            }

            _known = true;
            for (int i = 0; lost != null && i < lost.Count; i++)
            {
                ModNotifications.Raise(
                    new EventModInfluenceGroundLost(empire, lost[i].System, lost[i].Taker)
                );
            }
        }

        private struct Loss
        {
            public ColonizedStarSystem System;
            public Empire Taker;
        }

        /// <summary>
        /// The squares that were provably the player's last turn and that somebody else now wins a
        /// point of, gathered into one entry per (system, taker).
        ///
        /// A square that has left the sweep entirely - the player's own circle no longer reaches it,
        /// because the colony behind it shrank or is no longer theirs - is NOT reported. The second
        /// proof cannot be made there: nothing asked the game about that square this turn, and losing
        /// a whole system is news the game already gives. The case this line exists for keeps its
        /// square either way, since a rival winning ground INSIDE your circle does not move your
        /// circle.
        /// </summary>
        private static List<Loss> Diff(List<GroundTile> now)
        {
            List<Loss> lost = null;
            for (int i = 0; i < now.Count; i++)
            {
                if (now[i].Taker == null)
                {
                    continue;
                }

                ColonizedStarSystem was;
                if (!_was.TryGetValue(InfluenceGround.Key(now[i].X, now[i].Y), out was))
                {
                    continue;
                }

                if (lost == null)
                {
                    lost = new List<Loss>(2);
                }

                bool seen = false;
                for (int j = 0; j < lost.Count; j++)
                {
                    if (
                        ReferenceEquals(lost[j].System, was)
                        && ReferenceEquals(lost[j].Taker, now[i].Taker)
                    )
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                {
                    lost.Add(new Loss { System = was, Taker = now[i].Taker });
                }
            }

            return lost;
        }
    }
}

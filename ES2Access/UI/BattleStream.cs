using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using ES2Access.Core.UI;
using ES2Access.Core.Util;
using HarmonyLib;

namespace ES2Access.UI
{
    /// <summary>
    /// What is HAPPENING in a space battle, taken off the stream the game is replaying.
    ///
    /// A watched battle is not simulated on the client. The whole fight is computed elsewhere and
    /// arrives as a report of timestamped instructions, which the client plays back against the model
    /// at the speed the player chose (<c>GalaxyEncounter.UpdateParseReport</c> :1207-1230 drains the
    /// queue, <c>ParseReportInstruction</c> :1184-1205 plays each one and recurses into its
    /// sub-instructions). Everything a sighted player sees in the arena is one of those instructions
    /// being realised: a salvo leaving a gun, a hull taking damage, a flotilla arriving.
    ///
    /// Reading them is the only way to narrate the fight AS IT HAPPENS. The model the screen watches
    /// answers "what is true now" and is enough for losses and for the phase; it cannot answer who
    /// shot whom, whether the shot landed, or what the shields ate, because those are events and not
    /// states, and by the next frame the model has forgotten them.
    ///
    /// <b>The hook site is the recursion itself</b>, not the per-type delegate table
    /// (<c>parseReportInstructionDelegates</c> :1192). The table has an entry only for the
    /// instructions the VIEW needs to animate, and it is keyed by exact type, so patching it would
    /// mean one patch per type and would still miss the types the view ignores. The recursive method
    /// sees every instruction and every sub-instruction exactly once, in play order, at the moment
    /// the clock reaches it - which is exactly the moment the player sees it. Its return value is the
    /// clock gate (false = not due yet, and the game does not play it), so the postfix reads
    /// <c>__result</c> and takes only what was really played.
    ///
    /// Nothing here speaks. The hook classifies, resolves names, and queues; the battle screen drains
    /// the queues from the per-frame pump and decides what is worth saying (repo convention: hooks set
    /// state, the pump talks).
    ///
    /// <b>What the stream turned out to carry</b>, measured on a real report (Sabel vs Pirates,
    /// 1904 instructions over four phases): 38 <c>CreateSalvo</c>, 27 <c>Hit</c>, 1578
    /// <c>UpdateProperty</c>, 101 <c>Event</c>. A MISS is <c>CreateSalvo.Miss</c> and nothing else -
    /// no <c>Attack_Miss</c> event is emitted, and a missing salvo simply never produces a
    /// <c>Hit</c> (<c>BattleSimulationSalvo.HitTarget</c> :189-202). A HIT carries the damage that
    /// got THROUGH (<c>SimulationProperties.Salvo.EffectiveDamage</c>, post-mitigation), and what the
    /// shields ate is in its own sub-instructions as <c>DamageReceivedAbsorbedByShield</c> deltas -
    /// so absorption is per-hit and measurable, and a shot stopped dead is a Hit whose damage is zero
    /// with an absorbed delta above it. The same delta is written more than once per hit (once per
    /// accounting level), so it is read as a MAXIMUM and never a sum.
    /// </summary>
    internal static class BattleStream
    {
        /// <summary>One weapon shot, with both ends already resolved to the ships a player would name
        /// them by.</summary>
        internal struct Shot
        {
            public string Attacker;

            public string Target;

            public bool Hit;

            public float Damage;

            public float Absorbed;

            public DamageKind Kind;
        }

        /// <summary>Something joining the fight after it started.</summary>
        internal struct Arrival
        {
            public string Name;

            public bool Mine;
        }

        /// <summary>A ship putting hull back on.</summary>
        internal struct Mend
        {
            public string Ship;

            public float Amount;
        }

        /// <summary>A battle effect one side put on the other, in the game's own title for it.
        /// </summary>
        internal struct Effect
        {
            public string Initiator;

            public string Name;

            public string Target;
        }

        /// <summary>A medal a ship earned during the fight.</summary>
        internal struct Award
        {
            public string Ship;

            public string Medal;
        }

        /// <summary>How much of a fight is kept while nothing is draining. The pump drains every
        /// frame, so this is only ever reached if the screen is not up at all - and then the oldest
        /// entries are the ones worth losing.</summary>
        private const int Backlog = 256;

        private static readonly StaticString AbsorbedByShield =
            SimulationProperties.EncounterShip.DamageReceivedAbsorbedByShield;
        private static readonly StaticString Health = SimulationProperties.Ship.Health;
        private static readonly StaticString WeaponTypeEnergy = "WeaponTypeEnergy";
        private static readonly StaticString WeaponTypePhysical = "WeaponTypePhysical";

        private static Harmony _harmony;
        private static readonly List<Shot> _shots = new List<Shot>(16);
        private static readonly List<Arrival> _arrivals = new List<Arrival>(2);
        private static readonly List<Mend> _mends = new List<Mend>(2);
        private static readonly List<Effect> _effects = new List<Effect>(2);
        private static readonly List<Award> _awards = new List<Award>(2);

        private static readonly Dictionary<ulong, string> _names = new Dictionary<ulong, string>();
        private static readonly Dictionary<ulong, DamageKind> _kinds =
            new Dictionary<ulong, DamageKind>();
        private static readonly Dictionary<ulong, bool> _mine = new Dictionary<ulong, bool>();
        private static Encounter _battle;

        /// <summary>Whether the patch is in place - what the teardown check reads.</summary>
        public static bool Installed
        {
            get { return _harmony != null; }
        }

        /// <summary>Who is patching the replay's instruction pump right now (see
        /// <see cref="ModPatches"/>).</summary>
        internal static string[] Owners()
        {
            return ModPatches.Owners(Playing(), false);
        }

        private static MethodInfo Playing()
        {
            return AccessTools.Method(
                typeof(GalaxyEncounter),
                "ParseReportInstruction",
                new[] { typeof(EncounterReportInstruction), typeof(double) }
            );
        }

        public static void Install()
        {
            Remove();

            // A unique id per load, per repo convention: a fixed one lets the UnpatchSelf of the
            // assembly a reload replaced strip this load's patches.
            Harmony harmony = new Harmony(
                "endless.space2.access.battlestream." + Guid.NewGuid().ToString("N")
            );

            try
            {
                MethodInfo playing = Playing();
                if (playing == null)
                {
                    throw new MissingMethodException(
                        typeof(GalaxyEncounter).FullName,
                        "ParseReportInstruction"
                    );
                }

                harmony.Patch(
                    playing,
                    null,
                    new HarmonyMethod(
                        typeof(BattleStream).GetMethod(
                            "Played",
                            BindingFlags.Static | BindingFlags.NonPublic
                        )
                    )
                );
                _harmony = harmony;
            }
            catch (Exception e)
            {
                // Unpatched, the fight is narrated exactly as far as the model reaches - the acts,
                // the phases, the losses - and the exchange of fire is silent. Worth saying loudly,
                // not worth refusing to start over.
                Log.Error("the battle replay stream could not be watched: " + e);
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

        public static void Remove()
        {
            Harmony harmony = _harmony;
            _harmony = null;
            Forget();
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
                Log.Error("the battle replay stream could not be unpatched: " + e);
            }
        }

        /// <summary>Everything queued and everything remembered about the battle it came from. Called
        /// when a fight starts, when one is watched again, and on teardown.</summary>
        public static void Forget()
        {
            _shots.Clear();
            _arrivals.Clear();
            _mends.Clear();
            _effects.Clear();
            _awards.Clear();
            _names.Clear();
            _kinds.Clear();
            _mine.Clear();
            _battle = null;
        }

        /// <summary>The shots played since the last drain, oldest first. The list is the caller's;
        /// the queue is empty afterwards.</summary>
        public static List<Shot> TakeShots()
        {
            return Take(_shots);
        }

        public static List<Arrival> TakeArrivals()
        {
            return Take(_arrivals);
        }

        public static List<Mend> TakeMends()
        {
            return Take(_mends);
        }

        public static List<Effect> TakeEffects()
        {
            return Take(_effects);
        }

        public static List<Award> TakeAwards()
        {
            return Take(_awards);
        }

        private static List<T> Take<T>(List<T> queue)
        {
            if (queue.Count == 0)
            {
                return null;
            }

            List<T> taken = new List<T>(queue);
            queue.Clear();
            return taken;
        }

        private static void Keep<T>(List<T> queue, T item)
        {
            if (queue.Count >= Backlog)
            {
                queue.RemoveAt(0);
            }

            queue.Add(item);
        }

        /// <summary>Runs inside the game's own replay pump, once per instruction the clock has
        /// reached: classifies, resolves, queues, and does nothing else.</summary>
        private static void Played(
            GalaxyEncounter __instance,
            EncounterReportInstruction instruction,
            bool __result
        )
        {
            if (!__result || instruction == null)
            {
                return;
            }

            try
            {
                Encounter battle = __instance == null ? null : __instance.Encounter;
                if (battle == null)
                {
                    return;
                }

                if (!ReferenceEquals(battle, _battle))
                {
                    // A different fight: nothing remembered about the last one still applies, and
                    // the GUIDs are another battle's.
                    Forget();
                    _battle = battle;
                }

                Classify(battle, instruction);
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading the replay stream threw: " + e);
            }
        }

        private static void Classify(Encounter battle, EncounterReportInstruction instruction)
        {
            EncounterReportInstruction_Hit hit = instruction as EncounterReportInstruction_Hit;
            if (hit != null)
            {
                Landed(
                    battle,
                    hit.WeaponEncounterGUID,
                    hit.TargetEncounterGUID,
                    hit.TheoreticalDamages,
                    Absorbed(hit)
                );
                return;
            }

            EncounterReportInstruction_CreateSalvo salvo =
                instruction as EncounterReportInstruction_CreateSalvo;
            if (salvo != null)
            {
                if (salvo.Miss)
                {
                    Missed(battle, salvo.WeaponEncounterGUID, salvo.TargetEncounterGUID);
                }

                return;
            }

            EncounterReportInstruction_CreateCitadelSalvo citadel =
                instruction as EncounterReportInstruction_CreateCitadelSalvo;
            if (citadel != null)
            {
                if (citadel.Miss)
                {
                    Missed(battle, citadel.WeaponEncounterGUID, citadel.TargetEncounterGUID);
                }

                return;
            }

            EncounterReportInstruction_Spawn spawn = instruction as EncounterReportInstruction_Spawn;
            if (spawn != null)
            {
                Arrived(battle, spawn.EntityEncounterGUID, spawn.StartTime);
                return;
            }

            EncounterReportInstruction_UpdateProperty property =
                instruction as EncounterReportInstruction_UpdateProperty;
            if (property != null)
            {
                Mended(battle, property);
                return;
            }

            EncounterReportInstruction_BattleEffect effect =
                instruction as EncounterReportInstruction_BattleEffect;
            if (effect != null)
            {
                Applied(battle, effect);
                return;
            }

            EncounterReportInstruction_Medal medal = instruction as EncounterReportInstruction_Medal;
            if (medal != null)
            {
                Earned(battle, medal);
            }
        }

        private static void Landed(
            Encounter battle,
            GameEntityGUID weapon,
            GameEntityGUID target,
            float damage,
            float absorbed
        )
        {
            string attacker = ShipName(battle, weapon);
            string hit = ShipName(battle, target);
            if (attacker == null || hit == null)
            {
                return;
            }

            Keep(
                _shots,
                new Shot
                {
                    Attacker = attacker,
                    Target = hit,
                    Hit = true,
                    Damage = damage,
                    Absorbed = absorbed,
                    Kind = Kind(battle, weapon),
                }
            );
        }

        private static void Missed(Encounter battle, GameEntityGUID weapon, GameEntityGUID target)
        {
            string attacker = ShipName(battle, weapon);
            string missed = ShipName(battle, target);
            if (attacker == null || missed == null)
            {
                return;
            }

            Keep(_shots, new Shot { Attacker = attacker, Target = missed, Hit = false });
        }

        /// <summary>
        /// What the target's shields took off this hit.
        ///
        /// The figure is not on the hit instruction - the hit carries only what got THROUGH - it is
        /// in the property updates the hit fans out into, one per level the game keeps the accounting
        /// at (measured: the same delta written twice per hit, on the section and on the ship). So
        /// they are read as a maximum: summing them would report double what the shields did.
        /// </summary>
        private static float Absorbed(EncounterReportInstruction hit)
        {
            float most = 0f;
            List<EncounterReportInstruction> parts = hit.SubInstructions;
            if (parts == null)
            {
                return 0f;
            }

            for (int i = 0; i < parts.Count; i++)
            {
                EncounterReportInstruction_UpdateProperty part =
                    parts[i] as EncounterReportInstruction_UpdateProperty;
                if (part == null || part.PropertyName != AbsorbedByShield)
                {
                    continue;
                }

                if (part.ValueDelta > most)
                {
                    most = part.ValueDelta;
                }
            }

            return most;
        }

        /// <summary>Reinforcements: a ship the stream deploys after the fight has started. Every ship
        /// in the opening line-up is spawned at time zero, which is the deployment animation and not
        /// news - so the clock is the whole test.</summary>
        private static void Arrived(Encounter battle, GameEntityGUID entity, double when)
        {
            if (when <= 0.0)
            {
                return;
            }

            string name = ShipName(battle, entity);
            if (name == null)
            {
                return;
            }

            Keep(_arrivals, new Arrival { Name = name, Mine = Mine(battle, entity) });
        }

        /// <summary>
        /// Hull going back ON, which the stream reports the same way it reports hull coming off: a
        /// Health delta, positive this time.
        ///
        /// Read only off the SECTIONS, because that is the level the stream writes hull at (measured:
        /// Health deltas arrive on ship sections and on individual modules, never on the ship) and
        /// because a module's health is part of its section's - counting both would double the
        /// repair.
        /// </summary>
        private static void Mended(
            Encounter battle,
            EncounterReportInstruction_UpdateProperty property
        )
        {
            if (property.PropertyName != Health || property.ValueDelta <= 0f)
            {
                return;
            }

            EncounterEntity entity = Entity(battle, property.EntityEncounterGUID);
            if (!(entity is EncounterShipSection))
            {
                return;
            }

            string ship = ShipName(battle, property.EntityEncounterGUID);
            if (ship == null)
            {
                return;
            }

            Keep(_mends, new Mend { Ship = ship, Amount = property.ValueDelta });
        }

        /// <summary>A battle effect, and only where the game has a title written for it: the name on
        /// the instruction is an internal one, and reading it aloud would tell the player about a
        /// data key rather than about the battle.</summary>
        private static void Applied(
            Encounter battle,
            EncounterReportInstruction_BattleEffect effect
        )
        {
            if (effect.BattleEffectState != BattleEffectState.Started)
            {
                return;
            }

            string title = Titled(effect.BattleEffectName);
            if (title == null)
            {
                return;
            }

            string initiator = ShipName(battle, effect.InitiatorEncounterGUID);
            string target = ShipName(battle, effect.TargetEncounterGUID);
            if (initiator == null || target == null)
            {
                return;
            }

            Keep(_effects, new Effect { Initiator = initiator, Name = title, Target = target });
        }

        /// <summary>A medal, under the same gate as a battle effect.</summary>
        private static void Earned(Encounter battle, EncounterReportInstruction_Medal medal)
        {
            string title = Titled(medal.MedalName);
            string ship = ShipName(battle, medal.EntityEncounterGUID);
            if (title == null || ship == null)
            {
                return;
            }

            Keep(_awards, new Award { Ship = ship, Medal = title });
        }

        /// <summary>The game's own written title for one of its named things, or null where it has
        /// none - which is the same test the report popup makes before drawing one.</summary>
        private static string Titled(StaticString name)
        {
            try
            {
                if (StaticString.IsNullOrEmpty(name))
                {
                    return null;
                }

                Amplitude.Unity.Gui.GuiElement element = Gui.GetGuiElement(name);
                string raw = element == null ? null : element.Title;
                if (string.IsNullOrEmpty(raw) || !Gui.IsLocalizationKey(raw))
                {
                    return null;
                }

                string said = AgeText.Clean(raw);
                return string.IsNullOrEmpty(said) || Gui.IsLocalizationKey(said) ? null : said;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What to call whatever this GUID is, in the ship the player would name it by.
        ///
        /// The stream names PARTS: a shot comes from a weapon module, and lands on whichever section
        /// or module the targeting picked. Neither is a thing the player is watching. So the chain is
        /// walked up to the ship that owns it and the ship is what the line says - the game's own
        /// title for it (<c>GuiShip.GetTitle</c> :419-435: the name its owner gave it, else the
        /// design's), which is the same string the battle's own rosters draw.
        ///
        /// Anything with no ship above it is left UNNAMED and its line is dropped. That is the
        /// citadel case: a station's guns are their own entity type with no title anywhere in the
        /// game's strings, and a mod-invented word for it would be the one thing this narration is
        /// not allowed to do.
        /// </summary>
        private static string ShipName(Encounter battle, GameEntityGUID guid)
        {
            ulong key = guid;
            string name;
            if (_names.TryGetValue(key, out name))
            {
                return name;
            }

            name = null;
            try
            {
                IEncounterEntity walk = Entity(battle, guid);
                for (int i = 0; i < 8 && walk != null; i++)
                {
                    EncounterShip ship = walk as EncounterShip;
                    if (ship != null)
                    {
                        name = AgeText.Clean(GuiShip.GetTitle(ship.Ship, ship.ShipDesign));
                        break;
                    }

                    walk = walk.Parent;
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: naming a ship in the replay stream threw: " + e);
            }

            _names[key] = name;
            return name;
        }

        /// <summary>Which of the two kinds of gun fired, read off the weapon module's own type flags
        /// - the same two properties the setup window's power arcs are summed from
        /// (<c>AdvancedEncounterPlayModalWindow.GetModulesPowerValuesByFleet</c> :415-450). A module
        /// that claims both or neither is <see cref="DamageKind.Unknown"/> and its line simply drops
        /// the type word.</summary>
        private static DamageKind Kind(Encounter battle, GameEntityGUID weapon)
        {
            ulong key = weapon;
            DamageKind kind;
            if (_kinds.TryGetValue(key, out kind))
            {
                return kind;
            }

            kind = DamageKind.Unknown;
            try
            {
                EncounterModule module = Entity(battle, weapon) as EncounterModule;
                if (module != null && module.Module != null)
                {
                    bool energy = module.Module.GetPropertyValue(WeaponTypeEnergy) > 0f;
                    bool physical = module.Module.GetPropertyValue(WeaponTypePhysical) > 0f;
                    if (energy != physical)
                    {
                        kind = energy ? DamageKind.Energy : DamageKind.Projectile;
                    }
                }
            }
            catch (Exception) { }

            _kinds[key] = kind;
            return kind;
        }

        /// <summary>Whether this entity is on the player's side, read off the group it belongs to
        /// rather than off any panel - the stream is model-side and has no panels.</summary>
        private static bool Mine(Encounter battle, GameEntityGUID guid)
        {
            ulong key = guid;
            bool mine;
            if (_mine.TryGetValue(key, out mine))
            {
                return mine;
            }

            mine = false;
            try
            {
                EncounterEntity entity = Entity(battle, guid);
                Empire player = Gui.PlayerEmpire;
                if (entity != null && player != null)
                {
                    EncounterGroup ours = battle.GetGroupByEmpireIndex(player.Index);
                    mine = ours != null && entity.GroupEncounterGUID == ours.EncounterEntityGUID;
                }
            }
            catch (Exception) { }

            _mine[key] = mine;
            return mine;
        }

        private static EncounterEntity Entity(Encounter battle, GameEntityGUID guid)
        {
            try
            {
                EncounterEntity entity;
                return battle.TryGetValue(guid, out entity) ? entity : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// BATTLES, in space and on the ground: the setup and report pages, the rosters and
    /// their figures, and what a battle paid or cost.
    ///
    /// Part of <see cref="ModStrings"/>; the English defaults for every key here live in the
    /// <c>Defaults</c> table with the rest.
    /// </summary>
    public static partial class ModStrings
    {
        /// <summary>The band of battle plans on the advanced setup, which Tab stops in on its own
        /// (the plans are the decision that window exists for). The game draws the hand as cards and
        /// gives the band no heading at all, so the word naming it is the mod's.</summary>
        public const string BattleTactics = "battle.tactics";

        public const string BattleYourFleets = "battle.your-fleets";
        public const string BattleEnemyFleets = "battle.enemy-fleets";

        /// <summary>The band of figures on the advanced setup - the four pages behind the wordless
        /// switches, which Tab now stops in on its own. Named for the same reason the two sides are:
        /// a stop the player lands in says what it is.</summary>
        public const string BattleStats = "battle.stats";
        public const string BattleYourTroops = "battle.your-troops";
        public const string BattleEnemyTroops = "battle.enemy-troops";
        public const string BattleYourDamage = "battle.your-damage";
        public const string BattleEnemyDamage = "battle.enemy-damage";
        public const string BattleShowYourFleets = "battle.show-your-fleets";
        public const string BattleShowEnemyFleets = "battle.show-enemy-fleets";
        /// <summary>
        /// Which side is stronger and by how much, phrased FROM the stronger side: the fleet that
        /// leads, the percentage more military power it has, and the fleet it is being compared
        /// against. The game draws this as two arcs with no figure anywhere on them, and the arcs say
        /// "one of these is bigger" rather than "350 against 172" - so the sentence says the same
        /// thing the picture does, which two bare numbers left the listener to work out.
        ///
        /// A complete phrase with all three parts, so a language that puts the comparison first or
        /// inflects the fleet names can. The percentage is what the stronger side has ON TOP of the
        /// weaker one - two equal fleets are 0% and never a refusal to speak.
        /// </summary>
        public const string BattleBalance = "battle.balance";

        /// <summary>The same sentence for the one comparison a percentage cannot make: a side with
        /// no military power left at all, which a report of a battle that wiped a fleet out is full
        /// of. Dividing by it would answer with infinity, so the wipe-out is its own phrase.</summary>
        public const string BattleBalanceAll = "battle.balance-all";

        /// <summary>The same question on the ground, where the two arcs are sized from manpower
        /// rather than from military power - a different quantity, so a different sentence.</summary>
        public const string BattleGroundBalance = "battle.ground-balance";
        public const string BattleTimeLeft = "battle.time-left";
        public const string BattleProgress = "battle.progress";
        public const string BattleYourShipLost = "battle.your-ship-lost";
        public const string BattleYourShipsLost = "battle.your-ships-lost";
        public const string BattleEnemyShipLost = "battle.enemy-ship-lost";
        public const string BattleEnemyShipsLost = "battle.enemy-ships-lost";
        public const string BattleYourFlotillaLost = "battle.your-flotilla-lost";
        public const string BattleYourFlotillasLost = "battle.your-flotillas-lost";
        public const string BattleEnemyFlotillaLost = "battle.enemy-flotilla-lost";
        public const string BattleEnemyFlotillasLost = "battle.enemy-flotillas-lost";

        // The exchange of fire, which the game draws as tracer and explosion and writes down
        // nowhere. Each is a whole sentence rather than a stem plus fragments, because the count
        // and the damage kind sit inside the clause a translation has to inflect - and there is
        // one per shape the tallies can take, so the composer chooses a form and never builds one.
        public const string BattleFireEnergy = "battle.fire-energy";
        public const string BattleFireEnergyMany = "battle.fire-energy-many";
        public const string BattleFireProjectile = "battle.fire-projectile";
        public const string BattleFireProjectileMany = "battle.fire-projectile-many";
        public const string BattleFireMixed = "battle.fire-mixed";
        public const string BattleFireMixedMany = "battle.fire-mixed-many";
        public const string BattleFirePlain = "battle.fire-plain";
        public const string BattleFirePlainMany = "battle.fire-plain-many";
        public const string BattleFireAbsorbed = "battle.fire-absorbed";
        public const string BattleFireAbsorbedMany = "battle.fire-absorbed-many";
        public const string BattleFireMissed = "battle.fire-missed";
        public const string BattleFireMissedMany = "battle.fire-missed-many";
        public const string BattleFireMissedClause = "battle.fire-missed-clause";
        public const string BattleFireMissedClauseMany = "battle.fire-missed-clause-many";
        public const string BattleFireShieldClause = "battle.fire-shield-clause";

        // What arrives, mends and is awarded mid-fight - each read off the one instruction in the
        // replay stream that reports it.
        public const string BattleReinforcements = "battle.reinforcements";
        public const string BattleEnemyReinforcements = "battle.enemy-reinforcements";
        public const string BattleRepaired = "battle.repaired";
        public const string BattleEffectApplied = "battle.effect-applied";
        public const string BattleMedalEarned = "battle.medal-earned";
        public const string GroundBattleRound = "ground-battle.round";
        public const string GroundBattleBombardment = "ground-battle.bombardment";
        public const string GroundBattleAssault = "ground-battle.assault";
        public const string GroundBattleYourTroopLost = "ground-battle.your-troop-lost";
        public const string GroundBattleYourTroopsLost = "ground-battle.your-troops-lost";
        public const string GroundBattleEnemyTroopLost = "ground-battle.enemy-troop-lost";
        public const string GroundBattleEnemyTroopsLost = "ground-battle.enemy-troops-lost";

        /// <summary>The four pages of figures the advanced battle setup keeps behind four wordless
        /// switches. The game has a description per switch and no title for any of them, so the page
        /// names are the mod's - and the pages are the list the player walks, so these are what the
        /// rows are called.</summary>
        public const string BattleStatsTrajectories = "battle.stats-trajectories";
        public const string BattleStatsMilitary = "battle.stats-military";
        public const string BattleStatsDamage = "battle.stats-damage";
        public const string BattleStatsRange = "battle.stats-range";

        /// <summary>Where one flotilla will fight, for the trajectory page: the flotilla's number and
        /// the range, the latter already in the game's own words for it. A complete phrase, so a
        /// language that puts the range first can.</summary>
        public const string BattleFlotillaRange = "battle.flotilla-range";

        /// <summary>
        /// How one of the two damage rings is split between the fleets. The window sizes these from
        /// what each side's weapons of that type get THROUGH the other side's defences of that type,
        /// which the phrase says out loud - the ring means something narrower than "damage", and a
        /// listener told a bare percentage would read it as the wrong quantity.
        ///
        /// Both halves are always said, including a zero: an unopposed ring means nothing of the other
        /// side's is getting through, which is a fact worth hearing rather than a half-sentence.
        /// </summary>
        public const string BattleEnergyDamageShare = "battle.energy-damage-share";
        public const string BattleProjectileDamageShare = "battle.projectile-damage-share";

        /// <summary>Which of the two damage rings is drawn thicker - the window's own comparison of how
        /// much damage of each type there is, which neither ring's split says. Nothing is said for two
        /// rings drawn alike.</summary>
        public const string BattleEnergyBiggerThreat = "battle.energy-bigger-threat";
        public const string BattleProjectileBiggerThreat = "battle.projectile-bigger-threat";

        /// <summary>How one of the three range rings is split between the fleets - how well each side's
        /// ships shoot at that range, averaged over the ships they are bringing.</summary>
        public const string BattleShortRangeShare = "battle.short-range-share";
        public const string BattleMediumRangeShare = "battle.medium-range-share";
        public const string BattleLongRangeShare = "battle.long-range-share";

        /// <summary>Which side had the morale bonus, for the advanced report's heading. The game draws
        /// this as a happiness icon tinted with the holder's empire colour and titles it "Morale bonus";
        /// the colour is the whole of WHOSE it is, and a title read out is a caption a listener cannot
        /// answer "mine or theirs?" from (owner-reported 2026-08-30). So the mod states the fact and
        /// names the side, and the game's own definition of the bonus stays in the review buffer behind
        /// it. Past tense: the report is written after the fight.</summary>
        public const string BattleYourMoraleBonus = "battle.your-morale-bonus";
        public const string BattleEnemyMoraleBonus = "battle.enemy-morale-bonus";

        /// <summary>How much of a side's shooting missed, for the advanced report's damage gauges. The
        /// game writes the COUNT beside the band and draws the SHARE as the band's height, with no
        /// figure on it and no total written anywhere - so the phrase says the share and no total, which
        /// is exactly what the picture says.</summary>
        public const string BattleShotsMissed = "battle.shots-missed";

        /// <summary>Which of the three range rings is drawn thickest - the range BOTH fleets are best
        /// suited to, which is where the fight is likely to be decided.</summary>
        public const string BattleShortRangeMatters = "battle.short-range-matters";
        public const string BattleMediumRangeMatters = "battle.medium-range-matters";
        public const string BattleLongRangeMatters = "battle.long-range-matters";

        /// <summary>
        /// Whether a ship is pinned to the flotilla it is in, which the advanced setup draws as a
        /// white glow on a 24-pixel chip and writes down nowhere at all.
        ///
        /// A locked ship is exempt from the sorting presets: the three buttons that redistribute the
        /// fleet leave it where the player put it. So the two words are a STATE the player sets and
        /// re-reads, not a decoration - which is why they are a control's two state words rather than
        /// one word that appears and vanishes. "Not locked" says the flotilla nothing, because a ship
        /// that is not pinned has no flotilla it belongs to: the next preset may move it anywhere.
        /// </summary>
        public const string BattleShipLockedInFlotilla = "battle.ship-locked-in-flotilla";
        public const string BattleShipNotLocked = "battle.ship-not-locked";

        /// <summary>A ship carried from one flotilla to another and put down there, in the shape every
        /// landed drop in the mod reports itself with (<c>fleets.ship-moved</c>,
        /// <c>system.population-moved</c>) - what moved, and where it went.</summary>
        public const string BattleShipMoved = "battle.ship-moved";
    }
}

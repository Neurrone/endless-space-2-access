namespace ES2Access.Core.Speech
{
    /// <summary>
    /// The SCANNER and the ZOOM ladder: the categories the lens sweeps, the rungs the
    /// camera stands on, and the words for what a sweep found.
    ///
    /// Part of <see cref="ModStrings"/>; the English defaults for every key here live in the
    /// <c>Defaults</c> table with the rest.
    /// </summary>
    public static partial class ModStrings
    {
        // The scan view. The lens names itself in the game's own words, so the only strings here are
        // for the two boxes the game draws bare and the pairing of a number with the output it counts.
        public const string ScreenScanView = "screen.scan-view";
        public const string ScanSystemInfo = "scan.system-info";
        public const string ScanLegend = "scan.legend";

        /// <summary>A figure and the output it counts, in that order (see
        /// <see cref="GalaxyScannerOutput"/>, which puts the same two the other way round).</summary>
        public const string ScanOutput = "scan.output";

        /// <summary>What the legend's own Tab stop is called (owner's word, 2026-09-01). The panel's
        /// tick carries the game's word for the box - "Caption" - and a stop needs a name of its own or
        /// Tabbing into it announces the tick and nothing about where the player has arrived.</summary>
        public const string ScanLegendStop = "scan.legend-stop";

        /// <summary>How much of a governor's dial the system management lens has filled in. The game
        /// draws the value as a pie and writes no number anywhere, and its own caption over the dial
        /// already says what is being measured, so this is the bare proportion.</summary>
        public const string ScanHeroEfficiency = "scan.hero-efficiency";

        /// <summary>How close the game is looking, wherever the mod offers it as a control - the scan
        /// view and the map. The game draws no such control - its zoom is a wheel gesture and two keys
        /// held down - so the name is the mod's.</summary>
        public const string Zoom = "zoom";

        /// <summary>How close the game is looking, said whenever it changes wherever the player is
        /// standing - the wheel, the game's own held zoom keys, a click that flies into a system, the
        /// page a system or a planet opens onto. The rung and how many there are, said on its own so
        /// that the band name below can follow it or not. The game writes nothing comparable of its
        /// own.</summary>
        public const string ZoomLevel = "zoom.level";

        // What the map is DRAWING at a rung, which is the thing a rung number does not say: the game
        // groups its thirteen zoom steps into layers and swaps the whole map at each boundary. The
        // grouping is the game's (`GalaxyViewCameraController.LayerDescriptorNamesByZoomIndex`); the
        // words are the mod's, because that table holds asset names - "InformativeGalaxyLayer" - and
        // the game shows the player none of them. Owner wordings, 2026-09-01: each names what the
        // level GIVES - the kinds of thing the map draws there and the tree therefore offers - rather
        // than which layer asset draws it, so the word and the rows the player can walk are one
        // answer. The two rungs above the camera's ladder announce themselves as pages and have none.
        public const string ZoomBandConstellations = "zoom.band.constellations";
        public const string ZoomBandSystemsAndLanes = "zoom.band.systems-and-lanes";
        public const string ZoomBandSystemsLanesFleets = "zoom.band.systems-lanes-fleets";
        public const string ZoomBandSystemDetails = "zoom.band.system-details";
        public const string ZoomBandOrbital = "zoom.band.orbital";

        /// <summary>One of the icon pairs a planet's card lines up under its ring: which of the empire's
        /// populations does well on that planet, and what they get out of it. The game draws two pictures
        /// side by side and writes no words at all.</summary>
        public const string ScanSynergy = "scan.synergy";

        /// <summary>A fight in orbit, which the diplomacy lens draws as the two sides' emblems and no
        /// words - so the empires are named from the fleets and the fact of the battle is the mod's word.
        /// The second form is for a line the game is drawing while the fleets have already gone.</summary>
        public const string ScanBattle = "scan.battle";
        public const string ScanBattleHere = "scan.battle-here";

        /// <summary>The star the diplomacy lens writes an empire's name over, which is its home system.
        /// The row's coordinate pair is the CENTRE the watching empire's intelligence has, and that is
        /// deliberately never called a home; this is the other drawing, the one the lens paints only at
        /// a major's explored home, and it is the mod's own phrase because the game writes the empire's
        /// name there and never the word for what the place is.</summary>
        public const string ScanEmpireHome = "scan.empire-home";

        /// <summary>The System lens's own panel, read as a stop of its own. The lens rings the star
        /// with six figures under no caption at all, so the region naming them is the mod's word; the
        /// rank block and the remains take the captions the game draws over them, and the fall-back
        /// here is only for a build where the rank block's caption group has been re-cut. The place is
        /// composed because the game writes an ordinal on the bar only while the system is in the first
        /// four, so a fifth-placed system was drawn a bar and no words. And the panel says when it is
        /// opened and closed, the way the fleet panel and the empire page's slide-outs do: a whole
        /// stop's worth of content arrives and goes with nothing else on the screen to hear.</summary>
        public const string ScanSystemOutputs = "scan.system-outputs";
        public const string ScanSystemRankRegion = "scan.system-rank-region";
        public const string ScanSystemRank = "scan.system-rank";

        /// <summary>The turn-by-turn curves under the rank block, as their own region. The game
        /// captions the whole block "System's Rank" and captions the curves nothing, so the table
        /// standing apart from the readouts above it needs a word the game does not supply (owner
        /// ruling 2026-09-02): the readouts keep the drawn caption and this names the table.</summary>
        public const string ScanSystemRankHistory = "scan.system-rank-history";
        public const string ScanSystemInfoShown = "scan.system-info-shown";
        public const string ScanSystemInfoHidden = "scan.system-info-hidden";

        /// <summary>The trade routes the scan view draws as lines across the map, woven into the places
        /// the lines run between and over (RULED 2026-09-01, <see cref="ES2Access.Core.UI.TradeWeave"/>):
        /// a system at either END of a route says where the route goes, a system the route merely
        /// crosses says whose road is passing through, and a star LANE says which route it carries and
        /// how THAT route is running over it - open or blockaded. One phrase per route, never a merged
        /// count. The game writes no words on any of it - the lines carry a colour and nothing else -
        /// so all five phrases are the mod's. There is no "mixed" phrase (owner ruling 2026-09-01,
        /// after playtest): the renderer has a third MATERIAL for a lane carrying both, but a lane is
        /// heard one route at a time, and the two sentences together say which route is blockaded where
        /// the colour could only say that one of them was.</summary>
        public const string ScanTradeRouteTo = "scan.trade-route-to";
        public const string ScanTradeRouteToBlockaded = "scan.trade-route-to-blockaded";
        public const string ScanTradeRouteThrough = "scan.trade-route-through";
        public const string ScanTradeLaneOpen = "scan.trade-lane-open";
        public const string ScanTradeLaneBlockaded = "scan.trade-lane-blockaded";

        // The star system management page. The panel names are the mod's, because the game draws the
        // panels as unlabelled boxes with an icon in the corner; everything a panel CONTAINS is read
        // in the game's own words.
        public const string ScreenStarSystem = "screen.star-system";

        /// <summary>The star-system page names itself after the system it is showing: the drawn system
        /// name and the game's own word for the page ("Heka, System management"), so that turning the
        /// page says which system the player is now on. A connective, not a phrase - both halves are
        /// somebody else's words.</summary>
        public const string ScreenStarSystemNamed = "screen.star-system-named";
        public const string ScreenPlanet = "screen.planet";
    }
}

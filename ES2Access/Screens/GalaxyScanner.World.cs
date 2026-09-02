using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.ES2.UI;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>What is out there: one walk of the galaxy model per press, gathering every kind the
    /// scanner knows, sorted nearest-first from where the player is reading.</summary>
    internal sealed partial class GalaxyScanner
    {
        // ---- what is out there ----

        /// <summary>Whether the band at <paramref name="level"/> lists a category - the tree's own
        /// table (<see cref="Bands"/>), asked with the level already in hand. A level with no answer
        /// (no galaxy camera) hides nothing: a filter that cannot tell what the map is showing must
        /// withhold none of it.</summary>
        private static bool Scans(int level, bool scanning, string categoryKey)
        {
            return level < 0 || Bands.Scans(level, scanning, categoryKey);
        }

        /// <summary>
        /// Everything the map is showing, in every kind the scanner knows, each list already sorted
        /// nearest-first from where the player is reading.
        ///
        /// Every list every time, not only the one being read: cycling categories has to know whether
        /// the category next door holds anything before it decides to skip it, and that answer only
        /// exists once the other lists have been built.
        /// </summary>
        private Snap Snapshot(out double east, out double north)
        {
            List<Found>[] world = new List<Found>[CategoryCount];
            for (int at = 0; at < CategoryCount; at++)
            {
                world[at] = new List<Found>();
            }

            Reference(out east, out north);
            // Which band the map is at, read once for the whole press: it is the same answer for every
            // category by construction, and each reading of it is two of the engine's service lookups.
            int band = ZoomBands.Level;
            bool lens = ZoomBands.Scanning;
            ScannerCost.Begin();
            try
            {
                Empire empire = Gui.PlayerEmpire;
                if (empire != null && GameGalaxy.Present())
                {
                    DepartmentOfForeignAffairs foreign =
                        empire.GetAgency<DepartmentOfForeignAffairs>();
                    // Only what the map is DRAWING at this distance (<see cref="ZoomBands"/>): a
                    // category the picture is not showing is not a short list, it is a list of things
                    // the player has not been shown. Asked here rather than at the cycle, so that the
                    // ring skips a band-hidden category by exactly the rule it skips an empty one -
                    // one code path, and no way for the tree and the scanner to disagree about what
                    // the map holds.
                    if (Scans(band, lens, ScannerKeys.Systems))
                    {
                        Systems(world[CategorySystems], empire, foreign);
                    }

                    // One call fills the five categories the planet dots are read out of, and they
                    // share a band in both modes, so one gate answers for all five.
                    if (Scans(band, lens, ScannerKeys.Colonizable))
                    {
                        Worlds(world, empire);
                    }

                    if (Scans(band, lens, ScannerKeys.Unexplored))
                    {
                        Unexplored(world[CategoryUnexplored], empire);
                    }

                    if (Scans(band, lens, ScannerKeys.Fleets))
                    {
                        Fleets(world[CategoryFleets], empire, foreign);
                    }

                    if (Scans(band, lens, ScannerKeys.Probes))
                    {
                        Probes(world[CategoryProbes], empire, foreign);
                    }

                    if (Scans(band, lens, ScannerKeys.Markers))
                    {
                        Markers(world[CategoryMarkers], empire);
                    }

                    if (Scans(band, lens, ScannerKeys.Pins))
                    {
                        Pins(world[CategoryPins]);
                    }

                    if (Scans(band, lens, ScannerKeys.Projectiles))
                    {
                        Projectiles(world[CategoryProjectiles]);
                    }

                    if (Scans(band, lens, ScannerKeys.Contested))
                    {
                        ContestedGround(world[CategoryContestedInfluence], empire);
                    }
                }

                // The gates above are also the cost saving; this is the guarantee. Every built-in
                // category the band hides is emptied whatever route filled it, and it happens BEFORE
                // the player's own categories are composed - so a custom slot's selectors and its
                // keywords are answered out of band-declared rows alone, with nothing extra to say
                // about it (owner ruling 2026-09-01).
                for (int at = 0; at < BuiltInCount; at++)
                {
                    if (!Scans(band, lens, ScannerKeys.Categories[at]))
                    {
                        world[at].Clear();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the scanner reading the map threw: " + e);
            }

            for (int at = 0; at < CategoryCount; at++)
            {
                Sort(world[at], east, north);
            }

            string[][] labels = Labels(world);

            // The player's own categories are built LAST, out of what the built-in ones found: a
            // selector points at a column that has to exist before it can be read, and a keyword is
            // asked of the detail those columns have already composed.
            List<Found>[][] custom = new List<Found>[CategoryCount][];
            string[] names = new string[CategoryCount];
            ScannerCustomPlan[] plans = Plans(world, labels);
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (plans[slot] == null)
                {
                    continue;
                }

                int at = Slotted(slot);
                custom[at] = CustomColumns(plans[slot], world, labels, east, north);
                labels[at] = plans[slot].Labels();
                names[at] = plans[slot].Name;
            }

            for (int at = 0; at < BuiltInCount; at++)
            {
                names[at] = ModStrings.Get(CategoryKeys[at]);
            }

            _labels = labels;
            _names = names;
            ScannerCost.End();
            return new Snap
            {
                World = world,
                Custom = custom,
                Labels = labels,
                Names = names,
                Table = Table(world, custom, labels),
            };
        }

        /// <summary>
        /// Every quest marker the game is showing this empire - the ones standing at a system, which
        /// that system's own row also mentions, AND the ones planted out in the open on a fleet
        /// crossing a lane, which have a top-level row of their own since 2026-08-22.
        ///
        /// Named by the QUEST, which is the only name a marker has (<c>QuestMarker</c> carries an
        /// instance id and a target and no words of its own), and enumerated by the one walk of the
        /// journal every surface uses (<see cref="QuestMarkers"/>) so the scanner, the system rows,
        /// the marker nodes and the inspect cell cannot disagree about which quests are being pointed
        /// at. The landing is the PAGE's, resolved here, because which node a marker has is a fact
        /// about the tree.
        /// </summary>
        private void Markers(List<Found> found, Empire empire)
        {
            List<QuestMarkers.Marker> markers = QuestMarkers.Of(empire);
            for (int i = 0; i < markers.Count; i++)
            {
                MapTarget target;
                if (!_screen.MarkerTarget(markers[i], out target))
                {
                    // A marker at a system the map is not naming: nowhere to go and nothing to say.
                    continue;
                }

                Found made = Make(
                    "marker/" + markers[i].Pin.GUID,
                    QuestMarkers.Name(markers[i]),
                    markers[i].At,
                    ScannerScopes.Only(),
                    target.System,
                    null
                );
                made.Target = target;
                made.Targeted = true;
                found.Add(made);
            }
        }

        /// <summary>
        /// EVERY SQUARE OF THE PLAYER'S OWN GROUND SOMEBODY ELSE IS WINNING - one map unit at a time,
        /// which is the resolution the inspect cursor reads the map at.
        ///
        /// Not the inspect readout's question, deliberately. That one asks "whose influence is over
        /// this cell" about wherever the player is standing, and answers for any empire; this one asks
        /// only about the player's OWN reach and only about squares inside it that a rival's field now
        /// wins (<see cref="InfluenceGround"/>). A border being pushed back is a thing to go and do
        /// something about, and a list of every contested square in the galaxy would bury it.
        ///
        /// EVERY SQUARE, no clustering: the scanner has no clustering anywhere - a system, a fleet and
        /// a missile are each their own entry however close together they stand - and a run of
        /// adjacent squares stepped through with the instance key IS how the player hears how wide the
        /// bite is. Each is named by the own system whose ground it is, so the list reads as
        /// "Near Dusay" four times over rather than as four unnamed places.
        /// </summary>
        private static void ContestedGround(List<Found> found, Empire empire)
        {
            GalaxyPosition origin = GalaxyCoordinates.Origin();
            IList<GroundTile> ground = InfluenceGround.Sweep(empire);
            for (int i = 0; i < ground.Count; i++)
            {
                GroundTile tile = ground[i];
                ColonizedStarSystem whose = tile.Held ?? tile.Reaching;
                if (tile.Taker == null || whose == null)
                {
                    continue;
                }

                Found made = Make(
                    "square/" + tile.X + "," + tile.Y,
                    ModStrings.Format(ModStrings.GalaxyScannerNear, whose.LocalizedName),
                    new GalaxyPosition(origin.X + tile.X, origin.Y + tile.Y),
                    ScannerScopes.Only(),
                    null,
                    null
                );
                made.Square = true;
                found.Add(made);
            }
        }

        /// <summary>The pins allies have dropped on the map, off the very list the tree declares its
        /// pin rows from, and named the way those rows name them - by the KIND of request, which is
        /// the only name the game gives one.</summary>
        private void Pins(List<Found> found)
        {
            IList<GalaxyHudScreen.SightedPin> pins = _screen.SightedPins;
            for (int i = 0; i < pins.Count; i++)
            {
                CoordinationRequest pin = pins[i].Request;
                ControlId row = GalaxyHudScreen.PinId(pin);
                Found made = Make(
                    Row(row),
                    GalaxyHudScreen.PinKind(pin),
                    pin.GalaxyPosition,
                    ScannerScopes.Only(),
                    null,
                    null
                );
                made.Row = row;
                found.Add(made);
            }
        }

        /// <summary>The obliterator missiles in flight, off the same list the tree's own missile rows
        /// are declared from. The mod's phrase for one, because the game has no name for it - and
        /// nothing else: where it is AIMED is a sentence the game writes for the player's own missile
        /// alone, so it stays on the row where it can be reviewed rather than being said to everyone
        /// sweeping the category.</summary>
        private void Projectiles(List<Found> found)
        {
            IList<GalaxyHudScreen.SightedShot> shots = _screen.SightedProjectiles;
            for (int i = 0; i < shots.Count; i++)
            {
                ObliteratorProjectile shot = shots[i].Shot;
                ControlId row = GalaxyHudScreen.ProjectileId(shot);
                Found made = Make(
                    Row(row),
                    ModStrings.Get(ModStrings.GalaxyObliteratorProjectile),
                    shot.GalaxyPosition,
                    ScannerScopes.Only(),
                    null,
                    null
                );
                made.Row = row;
                found.Add(made);
            }
        }

        /// <summary>The identity of a thing whose row the PAGE keys - the key it built, which is
        /// stable across a rebuild for the same reason the row is.</summary>
        private static string Row(ControlId id)
        {
            return id == null ? null : "row/" + id.StructuralKey;
        }

        /// <summary>
        /// Every probe the map is drawing a mote for - the TRAVELLING probes, and only those.
        ///
        /// The list is the page's own (<see cref="GalaxyHudScreen.ScannedProbes"/>), which is the list
        /// the tree's probe rows and the inspect cell are both built from, so the three cannot disagree
        /// about what is out there. A detection probe has no mote of its own (it is drawn on the system
        /// label it watches) and a mining probe is fixed to a planet, so neither is a thing on the map
        /// to steer towards and neither is here.
        /// </summary>
        private void Probes(
            List<Found> found,
            Empire empire,
            DepartmentOfForeignAffairs foreign
        )
        {
            IList<GalaxyHudScreen.ScannedProbe> drifting = _screen.ScannedProbes();
            for (int i = 0; i < drifting.Count; i++)
            {
                GalaxyHudScreen.ScannedProbe it = drifting[i];
                Found made = Make(
                    "probe/" + it.Probe.GUID,
                    it.Name,
                    it.Probe.GalaxyPosition,
                    ScannerScopes.Owned(Scope(it.Probe.Empire, empire, foreign)),
                    null,
                    null
                );
                made.Extra = it.Extra;
                made.Row = it.Node;
                found.Add(made);
            }
        }

        /// <summary>
        /// Every place the map is naming - the star systems and the SPECIAL nodes together, which is
        /// exactly the set the tree's own systems stop declares. The two were split before and the
        /// split was wrong: a nebula is a place the player steers to and asks the distance of like any
        /// other, and a scanner that could not find one made the tree and the scanner disagree about
        /// what is on the map.
        ///
        /// What a special node is NOT is owned, so it takes no place in the affiliation trio and
        /// belongs to "special" alone (<see cref="ScannerScopes.System"/>).
        /// </summary>
        private static void Systems(
            List<Found> found,
            Empire empire,
            DepartmentOfForeignAffairs foreign
        )
        {
            HashSet<GameEntityGUID> homes = Homes(empire);
            IColonizedStarSystemRepositoryService colonies =
                Services.GetService<IColonizedStarSystemRepositoryService>();
            foreach (StarSystemNode node in GameGalaxy.StarSystemNodes())
            {
                if (!MapVisibility.Perceived(node, empire))
                {
                    continue;
                }

                int affiliation = Mine(colonies, node, empire)
                    ? ScopeFriendly
                    : Scope(Owner(node, empire), empire, foreign);
                int scopes = ScannerScopes.System(
                    affiliation,
                    node is SpecialNode,
                    homes.Contains(node.GUID),
                    Minor(colonies, node, empire)
                );
                found.Add(
                    Make(
                        "system/" + node.GUID,
                        node.LocalizedName,
                        node.GalaxyPosition,
                        scopes,
                        node,
                        null
                    )
                );
            }
        }

        // ---- what is on the worlds ----

        /// <summary>
        /// One walk of every planet the map is showing, filling the five categories that are questions
        /// about worlds: what could be settled, and what has been found on them.
        ///
        /// ONE walk, not five. The five ask the same two questions of the same planets - is the player
        /// allowed to know what is on this world, and what is on it - and walking the galaxy five
        /// times over would be five chances for the five to disagree about which planets exist.
        ///
        /// THE GATES ARE THE DRAWN CARD'S OWN. A planet is here at all only where the tree declares a
        /// node for it (<see cref="GalaxyHudScreen.PlanetsDeclared"/>: the game is showing this empire
        /// the system's planets) and the map is naming the system
        /// (<see cref="MapVisibility.Perceived"/>) - anything else would be a scanner offering a
        /// landing that does not exist. What is ON the planet is gated once more: the anomalies, the
        /// deposits and the planet's own type appear on the card only once the system is SURVEYED
        /// (<see cref="GalaxyHudScreen.Surveyed"/>), which is the threshold the circles turn from grey
        /// unknowns into real planets at. Curiosities are the exception, and it is the game's: a
        /// curiosity is seen through its own definition's prerequisites
        /// (<c>Curiosity.CanBeSeen</c> - detection technology), never through the survey.
        /// </summary>
        private static void Worlds(List<Found>[] world, Empire empire)
        {
            List<Found> colonizable = world[CategoryColonizable];
            List<Found> anomalies = world[CategoryAnomalies];
            List<Found> curiosities = world[CategoryCuriosities];
            List<Found> luxury = world[CategoryLuxury];
            List<Found> strategic = world[CategoryStrategic];
            Dictionary<string, bool> able = new Dictionary<string, bool>();
            Dictionary<string, string> titles = new Dictionary<string, string>();
            foreach (StarSystemNode node in GameGalaxy.StarSystemNodes())
            {
                if (
                    !MapVisibility.Perceived(node, empire)
                    || !GalaxyHudScreen.PlanetsDeclared(node, empire)
                )
                {
                    continue;
                }

                bool surveyed = GalaxyHudScreen.Surveyed(node, empire);
                for (int i = 0; i < node.Planets.Count; i++)
                {
                    Planet planet = node.Planets[i];
                    string name = GalaxyHudScreen.PlanetName(node, planet, empire);
                    Curiosities(curiosities, node, planet, i, name, empire, titles);
                    if (!surveyed)
                    {
                        continue;
                    }

                    Anomalies(anomalies, node, planet, i, name, titles);
                    Deposits(luxury, strategic, node, planet, i, name, titles);
                    Colonizable(colonizable, node, planet, i, name, empire, able, titles);
                }
            }
        }

        /// <summary>What has been found on a world, one entry per KIND of anomaly - named by the
        /// game's own wrapper for it, which is what the orbital card writes wherever it has room for
        /// the words (<see cref="GalaxyHudScreen"/>'s AddAnomalies).</summary>
        private static void Anomalies(
            List<Found> found,
            StarSystemNode node,
            Planet planet,
            int orbit,
            string name,
            Dictionary<string, string> titles
        )
        {
            List<string> seen = new List<string>();
            for (int i = 0; i < planet.Anomalies.Count; i++)
            {
                AnomalyDefinition definition = planet.Anomalies[i].AnomalyDefinition;
                if (definition == null)
                {
                    continue;
                }

                string kind = AnomalyTitle(definition, planet, titles);
                if (Once(seen, kind))
                {
                        found.Add(
                        OnPlanet(
                            node,
                            planet,
                            orbit,
                            name,
                            kind,
                            definition.Name.ToString(),
                            "anomaly"
                        )
                    );
                }
            }
        }

        /// <summary>Whether this is the first of its kind on this world. The row is one per KIND and
        /// world (owner's wording, 2026-08-22) - two of one kind on one planet are one place to go
        /// to, and two rows saying the same words would also be two things the cursor could not tell
        /// apart across a rebuild.</summary>
        private static bool Once(List<string> seen, string kind)
        {
            if (seen.Contains(kind))
            {
                return false;
            }

            seen.Add(kind);
            return true;
        }

        /// <summary>The curiosities still standing on a world - the ones the game would let this
        /// empire see, which is a question about its detection technology and not about the survey
        /// (<c>GuiPlanet.GetRemainingCuriosities</c> asks exactly this of every curiosity, and the
        /// ordering it puts them in is the panel's, not ours).</summary>
        private static void Curiosities(
            List<Found> found,
            StarSystemNode node,
            Planet planet,
            int orbit,
            string name,
            Empire empire,
            Dictionary<string, string> titles
        )
        {
            List<string> seen = new List<string>();
            List<FailureInfo> refusals = new List<FailureInfo>();
            for (int i = 0; i < planet.Curiosities.Count; i++)
            {
                Curiosity curiosity = planet.Curiosities[i];
                if (curiosity == null || !curiosity.CanBeSeen(empire))
                {
                    continue;
                }

                string kind = CuriosityTitle(curiosity, titles);
                if (!Once(seen, kind))
                {
                    continue;
                }

                // Whether an expedition could be sent, and whether the ONE thing in the way is the
                // empire's expedition power - both the game's own answers, asked once per curiosity
                // per press. No fleet is named because the scanner has none: this is the question the
                // system-side expedition asks (<c>PlanetCuriosityItem</c> asks it the same way for a
                // colonized system), and it is what the padlock on the card stands for.
                refusals.Clear();
                bool explorable = curiosity.CanBeSearched(empire, null, refusals);
                Found made = OnPlanet(
                    node,
                    planet,
                    orbit,
                    name,
                    kind,
                    curiosity.CuriosityDefinition.DisplayedType.ToString(),
                    "curiosity"
                );
                made.Scopes = ScannerScopes.Curiosity(
                    explorable,
                    !explorable && LowExpeditionPower(refusals)
                );
                found.Add(made);
            }
        }

        /// <summary>Whether the game refused this curiosity for expedition power - the failure the
        /// card turns into a padlock (<c>PlanetCuriosityItem.ShowLockIfNeeded</c> looks for exactly
        /// these two flags).</summary>
        private static bool LowExpeditionPower(List<FailureInfo> refusals)
        {
            for (int i = 0; i < refusals.Count; i++)
            {
                if (
                    refusals[i].Flag == FailureFlags.EmpireExpeditionPowerTooLow
                    || refusals[i].Flag == FailureFlags.FleetExpeditionPowerTooLow
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The resources a world is sitting on, split the way the game splits them - by the
        /// TYPE of the resource each deposit relates to (<c>GuiResource.IsLuxury</c> /
        /// <c>IsStrategic</c>, which count the system-wide kinds in with their own). A deposit of
        /// neither kind is not a thing the player goes looking for and is in neither list.</summary>
        private static void Deposits(
            List<Found> luxury,
            List<Found> strategic,
            StarSystemNode node,
            Planet planet,
            int orbit,
            string name,
            Dictionary<string, string> titles
        )
        {
            List<string> seen = new List<string>();
            for (int i = 0; i < planet.ResourceDeposits.Count; i++)
            {
                ResourceDeposit deposit = planet.ResourceDeposits[i];
                ResourceDepositDefinition definition = deposit == null ? null : deposit.Definition;
                ResourceDefinition resource =
                    definition == null ? null : definition.RelatedResourceDefinition;
                if (resource == null)
                {
                    continue;
                }

                GuiResource wrapper = new GuiResource(resource);
                if (!wrapper.IsLuxury && !wrapper.IsStrategic)
                {
                    continue;
                }

                string kind = ResourceTitle(wrapper, titles);
                if (Once(seen, kind))
                {
                    (wrapper.IsStrategic ? strategic : luxury).Add(
                        OnPlanet(
                            node,
                            planet,
                            orbit,
                            name,
                            kind,
                            wrapper.Name.ToString(),
                            "deposit"
                        )
                    );
                }
            }
        }

        /// <summary>One thing found on a world: the planet is what the row is about and where the jump
        /// lands, and the KIND is which column it belongs in - and, in the column that holds every
        /// kind, the first half of what the row says.</summary>
        private static Found OnPlanet(
            StarSystemNode node,
            Planet planet,
            int orbit,
            string name,
            string kind,
            string kindKey,
            string sort
        )
        {
            Found made = Make(
                "planet/" + planet.GUID + "/" + sort + "/" + kind,
                name,
                node.GalaxyPosition,
                ScannerScopes.Only(),
                node,
                null
            );
            made.Kind = kind;
            made.KindKey = kindKey;
            made.Planet = planet;
            made.Orbit = orbit;
            return made;
        }

        /// <summary>
        /// The worlds this empire could settle, in the two senses the owner asked for (2026-08-22).
        ///
        /// UNOCCUPIED is the game's own question, asked the way the game asks it: nobody has settled
        /// this planet and this empire is both able and allowed to. That is <c>Planet.IsColonizable</c>
        /// exactly, taken apart into the two halves it is made of - the technology to settle this kind
        /// of world, and the system's own rules about who is already standing in it - so that the
        /// first half can be answered once per kind of world instead of once per world.
        ///
        /// OCCUPIED is the other half of the same sweep: somebody ELSE is already sitting on the world
        /// - an outpost or a colony, theirs or a minor faction's - and this empire's technology could
        /// settle that kind of world. Only the ABLE half is asked, deliberately: the allowed half
        /// refuses every planet in a system somebody else holds, which is exactly the set this scope
        /// is for. It is a list of worlds worth taking, by force or by influence, not a list of
        /// worlds a colony ship could be sent to today.
        /// </summary>
        private static void Colonizable(
            List<Found> found,
            StarSystemNode node,
            Planet planet,
            int orbit,
            string name,
            Empire empire,
            Dictionary<string, bool> able,
            Dictionary<string, string> titles
        )
        {
            // The half both scopes need, and the cheap half once a type has been asked about. Asking
            // it first is also what keeps the expensive half off every world of a kind this empire
            // cannot settle at all.
            if (!Able(planet, empire, able))
            {
                return;
            }

            bool occupied = planet.IsColonized;
            if (occupied)
            {
                ColonizedPlanet colony = planet.ColonizedPlanet;
                if (colony == null || ReferenceEquals(colony.Empire, empire))
                {
                    return;
                }
            }
            else
            {
                // The other half of the game's own <c>IsColonizable</c>, whose first half is the line
                // above: the system's rules about who is already standing in it.
                ScannerCost.Colonizability();
                if (!planet.IsEmpireAllowedToColonize(empire))
                {
                    return;
                }
            }

            Found made = Make(
                "planet/" + planet.GUID,
                name,
                node.GalaxyPosition,
                ScannerScopes.Colonizable(occupied),
                node,
                null
            );
            made.Planet = planet;
            made.Orbit = orbit;

            // COMPOSED HERE rather than for the one row being read (owner ruling 2026-08-23). A
            // keyword can only look at what a result SAYS, so a description composed lazily is a
            // world a search for "Tundra" would never find. The cost of every settleable world in the
            // galaxy is measured in <see cref="ScannerCost"/>.
            made.Extra = Description(planet, empire, titles);
            found.Add(made);
        }

        /// <summary>
        /// Whether this empire's technology could settle a world of this KIND at all
        /// (<c>Planet.IsEmpireAbleToColonize</c>).
        ///
        /// Memoized on the planet's type for the length of one press, which is exact rather than a
        /// nearly-right saving: the list of colonization constructibles a planet offers is rebuilt
        /// from the database by the planet's Type and nothing else
        /// (<c>Planet.RefreshColonizationConstructibles</c>), and both prerequisite checks the answer
        /// is made of are run against the EMPIRE's simulation object. So two worlds of one type
        /// cannot answer differently, and a galaxy of five hundred planets asks the question once per
        /// type instead of once per planet.
        /// </summary>
        private static bool Able(Planet planet, Empire empire, Dictionary<string, bool> memo)
        {
            string type = planet.Type.ToString();
            bool answer;
            if (memo.TryGetValue(type, out answer))
            {
                return answer;
            }

            ScannerCost.Colonizability();
            answer = planet.IsEmpireAbleToColonize(empire);
            memo[type] = answer;
            return answer;
        }

        /// <summary>
        /// Everything about a world that decides whether it is worth going to, in the order a player
        /// weighs it: what kind of world it is, what is on it, how many people it would hold, and what
        /// it would produce. Absent parts are dropped rather than said as nothing, so a barren rock
        /// reads short and a garden world reads long.
        ///
        /// The words are the GAME's throughout - its own size-and-type sentence (its key's typo
        /// included), its own names for anomalies, curiosities and resources, and its own titles for
        /// the five outputs, which are drawn as icons and so exist nowhere else on the screen.
        /// </summary>
        private static string Description(
            Planet planet,
            Empire empire,
            Dictionary<string, string> titles
        )
        {
            MessageBuilder details = new MessageBuilder();
            details.ListItem(SizeAndType(planet));
            Resources(details, planet, false, titles);
            Resources(details, planet, true, titles);
            for (int i = 0; i < planet.Anomalies.Count; i++)
            {
                AnomalyDefinition definition = planet.Anomalies[i].AnomalyDefinition;
                if (definition != null)
                {
                    details.ListItem(AnomalyTitle(definition, planet, titles));
                }
            }

            for (int i = 0; i < planet.Curiosities.Count; i++)
            {
                Curiosity curiosity = planet.Curiosities[i];
                if (curiosity != null && curiosity.CanBeSeen(empire))
                {
                    details.ListItem(CuriosityTitle(curiosity, titles));
                }
            }

            details.ListItem(
                ModStrings.Format(ModStrings.GalaxyScannerMaxPopulation, planet.MaxPopulation)
            );
            Outputs(details, planet);
            return details.Build();
        }

        /// <summary>The resources of one kind a world is sitting on, in the order the deposits stand
        /// on it. Two passes rather than one, because the two kinds are two different reasons to go
        /// there and the row keeps them apart (owner's wording, 2026-08-22: the luxuries, then the
        /// strategics).</summary>
        private static void Resources(
            MessageBuilder details,
            Planet planet,
            bool strategic,
            Dictionary<string, string> titles
        )
        {
            for (int i = 0; i < planet.ResourceDeposits.Count; i++)
            {
                ResourceDeposit deposit = planet.ResourceDeposits[i];
                ResourceDepositDefinition definition = deposit == null ? null : deposit.Definition;
                ResourceDefinition resource =
                    definition == null ? null : definition.RelatedResourceDefinition;
                if (resource == null)
                {
                    continue;
                }

                GuiResource wrapper = new GuiResource(resource);
                if (strategic ? wrapper.IsStrategic : wrapper.IsLuxury)
                {
                    details.ListItem(ResourceTitle(wrapper, titles));
                }
            }
        }

        /// <summary>
        /// What a world would produce, as the five NUMBERS the planet's own page reads off it (owner
        /// ruling, 2026-08-22) - not the pips the orbital card draws in their place.
        ///
        /// The properties are the ones the game's own enumerator binds for a world nobody has settled
        /// (<c>FidsiEnumerator.LoadPlanet</c>, the uncolonized branch), read off the planet's own
        /// simulation object. The names beside them are the SHORT resource names rather than the
        /// simulation properties' own titles, and a figure the page would draw as zero is dropped -
        /// both rules and the reasons for them in <see cref="ScannerOutputs"/>.
        /// </summary>
        private static void Outputs(MessageBuilder details, Planet planet)
        {
            for (int i = 0; i < Potential.Length; i++)
            {
                float amount = planet.GetPropertyValue(Potential[i]);
                if (!ScannerOutputs.Says(amount))
                {
                    continue;
                }

                string line = ScannerOutputs.Line(
                    ModStrings.Get(PotentialNames[i]),
                    GlobalHud.Amount(amount, false, 0)
                );
                if (line != null)
                {
                    details.ListItem(line);
                }
            }
        }

        /// <summary>The five outputs of a world nobody has settled, in the game's own order
        /// (<c>FidsiEnumerator.LoadPlanet</c>).</summary>
        private static readonly Amplitude.StaticString[] Potential = new Amplitude.StaticString[]
        {
            SimulationProperties.Planet.PlanetInitialFood,
            SimulationProperties.Planet.PlanetInitialIndustry,
            SimulationProperties.Planet.PlanetInitialDust,
            SimulationProperties.Planet.PlanetInitialScience,
            SimulationProperties.Planet.PlanetInitialPrestige,
        };

        /// <summary>What each of <see cref="Potential"/> is called, index for index - the same short
        /// words the mod already has for the icons the game draws instead of a caption. The fifth is
        /// influence: "prestige" is the simulation's old name for it and no player-facing word in the
        /// game says it.</summary>
        private static readonly string[] PotentialNames = new string[]
        {
            ModStrings.IconFood,
            ModStrings.IconIndustry,
            ModStrings.IconDust,
            ModStrings.IconScience,
            ModStrings.IconInfluence,
        };

        /// <summary>What kind of world this is, in the game's own sentence for the pair - size first,
        /// as the key's own (misspelled) name has it.</summary>
        private static string SizeAndType(Planet planet)
        {
            try
            {
                return AgeText.Clean(
                    Gui.Localize(
                        "%PlaneSizeAndTypeFormat",
                        Gui.Localize(Gui.GetTitle(planet.Size)),
                        Gui.Localize(Gui.GetTitle(planet.Type))
                    )
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The three names a kind of thing has, each memoized for the length of one press: the wrapper
        // that answers is an allocation and a database lookup, and one galaxy holds hundreds of copies
        // of a handful of kinds.
        private static string AnomalyTitle(
            AnomalyDefinition definition,
            Planet planet,
            Dictionary<string, string> titles
        )
        {
            return Titled("anomaly/" + definition.Name, titles, definition, planet);
        }

        private static string CuriosityTitle(Curiosity curiosity, Dictionary<string, string> titles)
        {
            return Titled(
                "curiosity/" + curiosity.CuriosityDefinition.DisplayedType,
                titles,
                curiosity,
                null
            );
        }

        private static string ResourceTitle(GuiResource resource, Dictionary<string, string> titles)
        {
            return Titled("resource/" + resource.Name, titles, resource, null);
        }

        /// <summary>The game's own title for a thing, asked once per kind per press.</summary>
        private static string Titled(
            string key,
            Dictionary<string, string> titles,
            object subject,
            Planet planet
        )
        {
            string title;
            if (titles.TryGetValue(key, out title))
            {
                return title;
            }

            try
            {
                AnomalyDefinition anomaly = subject as AnomalyDefinition;
                Curiosity curiosity = subject as Curiosity;
                GuiResource resource = subject as GuiResource;
                if (anomaly != null)
                {
                    title = AgeText.Clean(new GuiAnomaly(anomaly, planet).Title);
                }
                else if (curiosity != null)
                {
                    title = AgeText.Clean(new GuiCuriosity(curiosity).Title);
                }
                else if (resource != null)
                {
                    title = AgeText.Clean(resource.Title);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the scanner naming " + key + " threw: " + e);
            }

            titles[key] = title;
            return title;
        }

        // ---- the ways out ----

        /// <summary>
        /// EVERY WAY OUT OF THE KNOWN MAP: a line the map draws from a system the player has seen, to
        /// a place they have not.
        ///
        /// The lanes are the page's own (<see cref="GalaxyHudScreen.LanesOf"/>) - the same list its
        /// lane rows, its fleet legs and its count phrases are built from, so a lane is described here
        /// with the same compass word its own row under that system says. A wormhole is one of them
        /// where the empire has the technology to be shown wormholes at all, and says it is one.
        ///
        /// EACH ONE ONCE, by construction rather than by de-duplication: a lane is offered by the end
        /// the player can SEE, and the other end is by definition one they cannot, so the walk never
        /// reaches it from the far side.
        ///
        /// It is named from the system it leaves rather than the place it goes, which has no name yet
        /// - that is the whole of what makes it unexplored (owner's wording, 2026-08-22).
        /// </summary>
        private static void Unexplored(List<Found> found, Empire empire)
        {
            foreach (StarSystemNode node in GameGalaxy.StarSystemNodes())
            {
                if (!MapVisibility.Perceived(node, empire))
                {
                    continue;
                }

                List<GalaxyHudScreen.Lane> lanes = GalaxyHudScreen.LanesOf(node, empire);
                for (int i = 0; i < lanes.Count; i++)
                {
                    GalaxyHudScreen.Lane lane = lanes[i];
                    if (MapVisibility.Perceived(lane.Far, empire))
                    {
                        continue;
                    }

                    string name = ModStrings.Format(
                        lane.Wormhole
                            ? ModStrings.GalaxyScannerUnexploredWormhole
                            : ModStrings.GalaxyScannerUnexploredLane,
                        node.LocalizedName,
                        ModStrings.Get(CompassDirections.KeyForBearing(lane.Bearing))
                    );
                    Found made = Make(
                        "lane/" + lane.Link.GUID,
                        name,
                        node.GalaxyPosition,
                        ScannerScopes.Only(),
                        node,
                        null
                    );
                    made.Lane = lane.Link;
                    found.Add(made);
                }
            }
        }

        /// <summary>
        /// The home systems the player is allowed to know about.
        ///
        /// Their OWN, always: the empire knows where it started, and the game keeps the node on the
        /// interior's own agency (<c>DepartmentOfTheInterior.HomeSystemNode</c>).
        ///
        /// A foreign empire's only where the GAME reveals it, which it does in exactly one place - the
        /// diplomacy lens, which draws a circle round another major empire's home system and links to
        /// it (<c>GalaxyStarSystem.ContentForDiplomaticScanViewForHomeSystem.Update</c>). Two things
        /// have to be true for that circle to be drawn at the home system, and both are asked here.
        /// First the player's intelligence must have marked that empire's position KNOWN, which it
        /// does only once at least one of that empire's colonies is explored or in sight
        /// (<c>DepartmentOfIntelligence.RefreshEmpirePosition</c>). Second the position it knows must
        /// BE the home system's, because that same routine falls back to the empire's
        /// highest-influence visible colony when the home system is not among the ones the player can
        /// see - and in that case the lens draws its circle somewhere else, and the home system is
        /// still a secret. Asking only the first would hand the player a capital they were shown a
        /// border colony of.
        ///
        /// Minor factions are not asked at all, matching the lens, which iterates the MAJOR empires.
        /// </summary>
        private static HashSet<GameEntityGUID> Homes(Empire empire)
        {
            HashSet<GameEntityGUID> homes = new HashSet<GameEntityGUID>();
            try
            {
                StarSystemNode own = HomeOf(empire);
                if (own != null)
                {
                    homes.Add(own.GUID);
                }

                DepartmentOfIntelligence intelligence =
                    empire.GetAgency<DepartmentOfIntelligence>();
                Game game = Gui.Game;
                Empire[] empires = game == null ? null : game.Empires;
                for (int i = 0; intelligence != null && empires != null && i < empires.Length; i++)
                {
                    MajorEmpire other = empires[i] as MajorEmpire;
                    if (other == null || ReferenceEquals(other, empire))
                    {
                        continue;
                    }

                    StarSystemNode home = HomeOf(other);
                    EmpirePosition known = intelligence.GetEmpirePosition(other);
                    if (
                        home != null
                        && known != null
                        && known.Known
                        && (known.GalaxyPosition - home.GalaxyPosition).SquareMagnitude
                            <= PositionSlack
                    )
                    {
                        homes.Add(home.GUID);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the scanner asking which systems are capitals threw: " + e);
            }

            return homes;
        }

        /// <summary>How close the position the game says it knows has to be to a home system before it
        /// IS that home system - the same epsilon the game compares two of these positions with
        /// (<c>DepartmentOfIntelligence.RefreshEmpirePosition</c>).</summary>
        private const float PositionSlack = 1.401298E-45f;

        private static StarSystemNode HomeOf(Empire empire)
        {
            DepartmentOfTheInterior interior =
                empire == null ? null : empire.GetAgency<DepartmentOfTheInterior>();
            return interior == null ? null : interior.HomeSystemNode;
        }

        /// <summary>Whether this system is the player's OWN - the same claim the map's tree puts in its
        /// first region, asked of the repository's own index rather than by walking the empire's
        /// colony list into a set. It counts an outpost as yours where the label's colour does not: a
        /// place you hold is friendly whether or not it has grown into a colony yet.</summary>
        private static bool Mine(
            IColonizedStarSystemRepositoryService colonies,
            StarSystemNode node,
            Empire empire
        )
        {
            ColonizedStarSystem held;
            return colonies != null
                && empire != null
                && colonies.TryGetValue(empire, node.NodePosition, out held);
        }

        /// <summary>
        /// Whose system this is for the affiliation buckets: the strongest claim standing there that
        /// the player is being shown (<see cref="GalaxyHudScreen.VisibleColony"/>), which counts an
        /// OUTPOST. One rule, shared with the map's own rows, rather than a second copy that filed a
        /// place under "neutral" while the row beside it named the empire holding it.
        ///
        /// Deliberately the wider of the game's two rules. The narrow one is what the label PAINTS,
        /// and a player sweeping for somebody's territory is asking who is on the ground, not whose
        /// colour is on the name.
        /// </summary>
        private static Empire Owner(StarSystemNode node, Empire empire)
        {
            ColonizedStarSystem claim = GalaxyHudScreen.VisibleColony(node, empire);
            return claim == null ? null : claim.Empire;
        }

        /// <summary>
        /// Whether a minor faction lives on this system.
        ///
        /// Asked of ALL the colonies standing at the node, not of the one whose colour the label
        /// paints (<see cref="Owner"/>): a minor faction shares its system with whoever settles a
        /// planet there, and that owner rule prefers the player's own colony, so asking it would hide
        /// exactly the faction sitting in the player's own back garden - which is the one a player
        /// sweeping this scope most wants to find.
        ///
        /// The gate is the same one the ownership answer uses,
        /// <c>Visibility[empire] >= EntityVisibility.Layer.Known</c>, so
        /// nothing here names a faction the map has not shown the player.
        /// </summary>
        private static bool Minor(
            IColonizedStarSystemRepositoryService colonies,
            StarSystemNode node,
            Empire empire
        )
        {
            if (colonies == null)
            {
                return false;
            }

            foreach (ColonizedStarSystem colony in colonies.GetValues(node.NodePosition))
            {
                if (
                    (int)colony.Visibility[empire] >= (int)EntityVisibility.Layer.Known
                    && colony.Empire is MinorEmpire
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Every fleet the map draws a lozenge for, parked and under way alike - the same
        /// repository and the same visibility gate the map's own labels use.</summary>
        private static void Fleets(
            List<Found> found,
            Empire empire,
            DepartmentOfForeignAffairs foreign
        )
        {
            IList<Fleet> drawn = FleetPresence.Drawing();
            for (int i = 0; i < drawn.Count; i++)
            {
                Fleet fleet = drawn[i];
                // Whose fleet it LOOKS like, which is what the map's own count phrase asks
                // (<c>GuiFleetGroup.Empire</c>): a fleet flying somebody else's colours is that
                // somebody's until the disguise is seen through.
                Empire owner = ReferenceEquals(fleet.Empire, empire)
                    ? fleet.Empire
                    : fleet.DisplayedEmpire;
                Found made = Make(
                    "fleet/" + fleet.GUID,
                    fleet.LocalizedName,
                    fleet.GalaxyPosition,
                    ScannerScopes.Owned(Scope(owner, empire, foreign)),
                    null,
                    fleet
                );
                // Everything the map's own fleet row says about this fleet except its name, plus
                // what it is DOING - docked somewhere or under way to somewhere, in the same words
                // the tree speaks (<c>GalaxyHudScreen.FleetState</c>). Composed here rather than on
                // the press, like every other result's extra, which is also what makes it something
                // the type-ahead search can match on (owner ruling 2026-08-23).
                MessageBuilder extra = new MessageBuilder();
                extra.ListItem(FleetPhrase.Describe(fleet));
                extra.ListItem(GalaxyHudScreen.FleetState(fleet));
                made.Extra = extra.Build();
                found.Add(made);
            }
        }

        private static Found Make(
            string key,
            string name,
            GalaxyPosition at,
            int scopes,
            StarSystemNode node,
            Fleet fleet
        )
        {
            double east;
            double north;
            GalaxyCoordinates.Offsets(at, out east, out north);
            return new Found
            {
                Key = key,
                Name = name,
                At = at,
                East = east,
                North = north,
                Scopes = scopes,
                Node = node,
                Fleet = fleet,
            };
        }

        /// <summary>
        /// Which way the player stands to whoever owns a thing.
        ///
        /// The mod has ONE standing ladder and this is a reading of it
        /// (<see cref="FleetPresence.SideOf(Empire, Empire, DepartmentOfForeignAffairs)"/>, shared
        /// with the map's lozenge count phrases and the spoken fleet phrase): the game's own ladder
        /// for a major empire, cold war and all, so a cold-war neighbour's fleets file under enemy
        /// here exactly as the map calls them enemy fleets. The scanner used to keep a taxonomy of
        /// its own in which only a declared WAR made an enemy; the owner ruled that out on
        /// 2026-08-26, because a filter that disagrees with what the same thing says when it speaks
        /// makes the player learn two galaxies.
        ///
        /// What the shared ladder does NOT reproduce is the game's own -1 fallthrough, which calls
        /// every minor civilization and every peaceful pirate an enemy; those states are bucketed by
        /// their own names instead.
        ///
        /// The player's own things file with the friendly ones, which is the one place this reading
        /// differs from the four-way ladder - the affiliation filters offer three, and the owner's
        /// own fleets belong with their friends' (as at <see cref="Systems"/> too).
        /// </summary>
        private static int Scope(Empire owner, Empire empire, DepartmentOfForeignAffairs foreign)
        {
            switch (FleetPresence.SideOf(owner, empire, foreign))
            {
                case FleetPresence.Side.Player:
                case FleetPresence.Side.Friendly:
                    return ScopeFriendly;
                case FleetPresence.Side.Enemy:
                    return ScopeEnemy;
                default:
                    return ScopeNeutral;
            }
        }

        /// <summary>Nearest first, and where two things are the same distance away the one whose name
        /// comes first - so the same galaxy read twice reads the same way round. Two things of
        /// different KINDS standing on one planet are the case the name cannot separate, so the kind
        /// settles it.</summary>
        private static void Sort(List<Found> found, double east, double north)
        {
            for (int i = 0; i < found.Count; i++)
            {
                Found it = found[i];
                double sideways = it.East - east;
                double up = it.North - north;
                it.Away = Math.Sqrt(sideways * sideways + up * up);
                found[i] = it;
            }

            found.Sort(Nearer);
        }

        private static int Nearer(Found one, Found two)
        {
            int by = one.Away.CompareTo(two.Away);
            if (by != 0)
            {
                return by;
            }

            by = string.Compare(one.Name, two.Name, StringComparison.Ordinal);
            return by != 0 ? by : string.Compare(one.Kind, two.Kind, StringComparison.Ordinal);
        }

        // ---- where it measures from ----

        /// <summary>
        /// Where the player is reading the map from, in the pair everything on this map is said in.
        ///
        /// The inspect cursor first, because while it is up it IS where the player is; then whatever
        /// place the tree cursor is standing on or inside; then home, which is where the pair "0, 0"
        /// is and the one place every player already knows.
        /// </summary>
        private void Reference(out double east, out double north)
        {
            east = 0.0;
            north = 0.0;
            try
            {
                int x;
                int y;
                if (_screen.Inspect.Centre(out x, out y))
                {
                    east = x;
                    north = y;
                    return;
                }

                GalaxyPosition at;
                if (GalaxyInspect.FocusedPlace(ModEntry.Navigator, out at))
                {
                    GalaxyCoordinates.Offsets(at, out east, out north);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the scanner asking where the player is reading threw: " + e);
            }
        }
    }
}

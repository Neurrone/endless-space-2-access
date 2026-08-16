using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

namespace ES2Access.Screens
{
    /// <summary>
    /// THE SCANNER: "what is near me, of this kind, and where is it".
    ///
    /// The tree answers "what is at this place and what is next to it"; the inspect cursor
    /// (<see cref="GalaxyInspect"/>) answers "what is over there". Neither answers the question a
    /// sighted player answers by glancing at the whole map at once - where is the nearest enemy fleet,
    /// how many neutral systems are within reach, which of my own systems is furthest out. That
    /// question is a LIST, sorted by distance, of one kind of thing at a time, and this is it.
    ///
    /// IT IS NOT A MODE. There is no key that turns it on and nothing to leave: the three chords are
    /// live for exactly as long as the map is the focused page, alongside ordinary tree navigation and
    /// alongside the inspect cursor. That is what makes it usable in the middle of doing something
    /// else - the player asks where the nearest enemy is without giving up the control they were
    /// standing on. Escape means what it always meant here, and on every other page the chords are
    /// simply not offered (<c>Screen.AnyKey</c> is only asked of the focused screen).
    ///
    /// THE LISTS ARE BUILT ON THE PRESS AND THROWN AWAY. Nothing is cached between presses and nothing
    /// runs per frame: the answer depends on where the player is reading FROM, which moves with every
    /// arrow key, so a cached list would be sorted from somewhere the player has left. Rebuilding is a
    /// walk of the galaxy's nodes and one walk of the visible-fleet repository, which is what one
    /// keystroke can afford and what no frame could.
    ///
    /// WHERE IT MEASURES FROM is the place the player is reading: the inspect cursor's centre while
    /// that mode is up, otherwise whatever place on the map the tree cursor is standing on, and home
    /// when the cursor is on none (the HUD, the turn controls). So "nearest" always means nearest to
    /// what the player is looking at, and moving the inspect cursor and scanning again re-sorts the
    /// same list around the new place.
    ///
    /// WHAT IT CAN SEE is what the map draws and nothing else - the same node gate the tree and the
    /// inspect cursor ask (<see cref="MapVisibility.Perceived"/>) and the same fleet repository the
    /// map's own lozenges are drawn from (<see cref="FleetPresence.Drawing"/>). A scanner reading off
    /// the simulation would be the shortest route there is to handing the player the fog's contents.
    /// </summary>
    internal sealed class GalaxyScanner
    {
        // The taxonomy, as the two indexes the cursor is held in. Categories first: what KIND of thing
        // is being looked for. "All" is subcategory zero of every category deliberately - it is the
        // one scope that can never be empty while the category holds anything, so cycling into a
        // category always has somewhere to land.
        private const int CategorySystems = 0;
        private const int CategoryFleets = 1;
        private const int CategoryCount = 2;

        private const int ScopeAll = 0;
        private const int ScopeFriendly = 1;
        private const int ScopeNeutral = 2;
        private const int ScopeEnemy = 3;
        private const int ScopeCount = 4;

        public GalaxyScanner(GalaxyHudScreen screen)
        {
            _screen = screen;
        }

        /// <summary>
        /// What <c>ModInput</c>'s conditional claim asks: the scanner's keys are taken from the game
        /// only while the map is the focused page AND the player is physically holding a modifier.
        ///
        /// The modifier half is what leaves the game its own keyboard zoom. The galaxy camera polls
        /// PageUp and PageDown through its own matcher, which reads the key codes of its binding and
        /// ignores the binding's modifiers entirely
        /// (<c>GalaxyViewCameraController.IsInputKeyCombinationPressed</c>) - so a claim on the key
        /// itself would take the bare press as surely as the chord, and handing the bare CHORD back
        /// (<c>ModInput.LeaveToGame</c>) would not help: the combination the stand-down is asked about
        /// carries the BINDING's modifiers, which are none either way. The physical modifier is the
        /// only thing that tells the two presses apart, so it is what the claim is made of.
        /// </summary>
        public static bool KeysClaimed()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            return navigator != null
                && navigator.Screen is GalaxyHudScreen
                && KeyboardBinding.AnyModifierHeld;
        }

        /// <summary>Drop the scanner's position - mod teardown. The lists were never held.</summary>
        public void Forget()
        {
            _cursor.Forget();
            _empire = null;
        }

        /// <summary>One key, offered to the scanner after the inspect cursor has passed on it. True
        /// when the scanner took it.</summary>
        public bool HandleKey(string actionKey)
        {
            try
            {
                switch (actionKey)
                {
                    case MapActions.ScanCategoryNext:
                        return Scan(1, ScannerAnswer.Scope, Tier.Category);
                    case MapActions.ScanCategoryPrev:
                        return Scan(-1, ScannerAnswer.Scope, Tier.Category);
                    case MapActions.ScanSubcategoryNext:
                        return Scan(1, ScannerAnswer.Scope, Tier.Subcategory);
                    case MapActions.ScanSubcategoryPrev:
                        return Scan(-1, ScannerAnswer.Scope, Tier.Subcategory);
                    case MapActions.ScanNext:
                        return Scan(1, ScannerAnswer.Instance, Tier.Instance);
                    case MapActions.ScanPrev:
                        return Scan(-1, ScannerAnswer.Instance, Tier.Instance);
                    case MapActions.ScanGoTo:
                        return GoTo();
                }

                return false;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the scanner threw on " + actionKey + ": " + e);
                return true;
            }
        }

        private enum Tier
        {
            Category,
            Subcategory,
            Instance,
        }

        private readonly GalaxyHudScreen _screen;
        private readonly ScannerCursor _cursor = new ScannerCursor();

        /// <summary>The empire the cursor's position was taken under. Another game loaded is another
        /// galaxy: the position means nothing there, and the player is owed the first press saying
        /// where they now are rather than stepping off somewhere they were never told.</summary>
        private Empire _empire;

        /// <summary>One thing the scanner found, with everything the announcement needs already read
        /// off it - so the sort, the reading and the jump cannot disagree about what was found.
        /// </summary>
        private struct Found
        {
            public string Name;
            public GalaxyPosition At;

            /// <summary>How far from home, along each axis - the pair the map is spoken in, kept
            /// unrounded so the distance is measured before anything is rounded.</summary>
            public double East;
            public double North;

            /// <summary>Which way the player stands to it: one of the three scopes below "all".
            /// </summary>
            public int Scope;

            /// <summary>How far from where the player is reading, filled in when the list is sorted.
            /// </summary>
            public double Away;

            /// <summary>Whichever of the two this is. The jump needs the thing itself, not its name.
            /// </summary>
            public StarSystemNode Node;
            public Fleet Fleet;
        }

        // ---- one press ----

        /// <summary>
        /// A press of one of the three tiers: rebuild the world, move the cursor, and say what it now
        /// points at.
        ///
        /// The whole snapshot is taken before the cursor is asked anything, because the cursor's own
        /// rules - skip a scope with nothing in it, come back to the nearest thing - are questions
        /// about the counts, and the counts are what the snapshot is.
        /// </summary>
        private bool Scan(int delta, ScannerAnswer said, Tier tier)
        {
            List<Found> systems;
            List<Found> fleets;
            double east;
            double north;
            Snapshot(out systems, out fleets, out east, out north);
            int[][] counts = Counts(systems, fleets);

            ScannerAnswer answer;
            if (Rearmed() || _cursor.Arm())
            {
                answer = _cursor.Hold(counts, said);
            }
            else
            {
                switch (tier)
                {
                    case Tier.Category:
                        answer = _cursor.CycleCategory(delta, counts);
                        break;
                    case Tier.Subcategory:
                        answer = _cursor.CycleSubcategory(delta, counts);
                        break;
                    default:
                        answer = _cursor.Step(delta, counts);
                        break;
                }
            }

            Say(answer, Scoped(systems, fleets), east, north);
            return true;
        }

        /// <summary>Whether the player has gone to another game since the last press, which re-arms
        /// the scanner: the position it was holding indexed a galaxy that is not this one.</summary>
        private bool Rearmed()
        {
            Empire empire = Gui.PlayerEmpire;
            if (ReferenceEquals(empire, _empire))
            {
                return false;
            }

            _empire = empire;
            _cursor.Forget();
            _cursor.Arm();
            return true;
        }

        /// <summary>The list the cursor is currently pointing into.</summary>
        private List<Found> Scoped(List<Found> systems, List<Found> fleets)
        {
            List<Found> all = _cursor.Category == CategoryFleets ? fleets : systems;
            if (_cursor.Subcategory == ScopeAll)
            {
                return all;
            }

            List<Found> some = new List<Found>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Scope == _cursor.Subcategory)
                {
                    some.Add(all[i]);
                }
            }

            return some;
        }

        private int[][] Counts(List<Found> systems, List<Found> fleets)
        {
            int[][] counts = new int[CategoryCount][];
            counts[CategorySystems] = Row(systems);
            counts[CategoryFleets] = Row(fleets);
            return counts;
        }

        private static int[] Row(List<Found> found)
        {
            int[] row = new int[ScopeCount];
            row[ScopeAll] = found.Count;
            for (int i = 0; i < found.Count; i++)
            {
                row[found[i].Scope]++;
            }

            return row;
        }

        // ---- what it says ----

        private void Say(ScannerAnswer answer, List<Found> scope, double east, double north)
        {
            if (answer == ScannerAnswer.Empty)
            {
                Voice.Say(ModStrings.Format(ModStrings.GalaxyScannerEmpty, ScopeName()), true);
                return;
            }

            MessageBuilder message = new MessageBuilder();
            if (answer == ScannerAnswer.Scope)
            {
                message.Fragment(ScopeName());
                message.ListItemForcedComma(
                    ModStrings.Plural(
                        ModStrings.GalaxyScannerFoundOne,
                        ModStrings.GalaxyScannerFoundMany,
                        scope.Count
                    )
                );
            }

            int at = _cursor.Index;
            if (at >= 0 && at < scope.Count)
            {
                Instance(message, scope[at], at, scope.Count, east, north);
            }

            Voice.Say(message.Build(), true);
        }

        /// <summary>One thing found, said the way the map says a place: what it is called, where it is
        /// on the map, then how far away it is and which way - and last, where in the list it stands,
        /// which is what tells the player how much more there is.</summary>
        private static void Instance(
            MessageBuilder message,
            Found found,
            int index,
            int count,
            double east,
            double north
        )
        {
            // The name opens a list item only where something already stands in front of it - a
            // scope's name and count. On a press that says the instance alone it IS the beginning of
            // the sentence, and a forced comma there would start the line with one.
            if (message.IsEmpty)
            {
                message.Fragment(found.Name);
            }
            else
            {
                message.ListItemForcedComma(found.Name);
            }

            message.ListItemForcedComma(GalaxyCoordinates.Text(found.At));
            message.ListItemForcedComma(Away(found, east, north));
            message.ListItemForcedComma();
            message.PushFraction(index + 1, count);
        }

        /// <summary>How far and which way, in the galaxy's own units - the same unit the coordinate
        /// pair is in, so the two numbers are one map. A thing standing where the player is reading
        /// from has no direction to give, and says so instead of saying "0 units north".</summary>
        private static string Away(Found found, double east, double north)
        {
            double sideways = found.East - east;
            double up = found.North - north;
            int units = MapCoordinates.Round(Math.Sqrt(sideways * sideways + up * up));
            if (units == 0)
            {
                return ModStrings.Get(ModStrings.GalaxyScannerHere);
            }

            return ModStrings.Format(
                units == 1
                    ? ModStrings.GalaxyScannerDistanceOne
                    : ModStrings.GalaxyScannerDistanceMany,
                units,
                CompassDirections.Direction(sideways, up)
            );
        }

        /// <summary>The name of the scope the cursor is in - one whole phrase per scope, never an
        /// adjective glued to a noun.</summary>
        private string ScopeName()
        {
            return ModStrings.Get(ScopeKeys[_cursor.Category][_cursor.Subcategory]);
        }

        private static readonly string[][] ScopeKeys = new string[][]
        {
            new string[]
            {
                ModStrings.GalaxyScannerSystemsAll,
                ModStrings.GalaxyScannerSystemsFriendly,
                ModStrings.GalaxyScannerSystemsNeutral,
                ModStrings.GalaxyScannerSystemsEnemy,
            },
            new string[]
            {
                ModStrings.GalaxyScannerFleetsAll,
                ModStrings.GalaxyScannerFleetsFriendly,
                ModStrings.GalaxyScannerFleetsNeutral,
                ModStrings.GalaxyScannerFleetsEnemy,
            },
        };

        // ---- going there ----

        /// <summary>
        /// Go to whatever the scanner is pointing at, in whichever way the player is reading the map.
        ///
        /// With the inspect cursor up the square moves onto the thing - onto its ROUNDED pair, the one
        /// the player was just told, which is what guarantees the thing is inside even the one-unit
        /// cursor - and lands exactly as an arrow key lands: camera, outline, and the cell read out.
        /// The scanner then measures from there, because the cursor is where the player is reading.
        ///
        /// With the tree, the cursor goes to the thing's own node - a system's, or a fleet's under
        /// whichever system the map draws it at - through the page's own landing, so the branch is
        /// opened and the node makes its ordinary announcement rather than a second one invented here.
        /// </summary>
        private bool GoTo()
        {
            List<Found> systems;
            List<Found> fleets;
            double east;
            double north;
            Snapshot(out systems, out fleets, out east, out north);
            if (!Rearmed())
            {
                _cursor.Arm();
            }

            List<Found> scope = Scoped(systems, fleets);
            int at = _cursor.Index;
            if (at < 0 || at >= scope.Count)
            {
                Voice.Say(ModStrings.Format(ModStrings.GalaxyScannerEmpty, ScopeName()), true);
                return true;
            }

            Found found = scope[at];
            if (GalaxyInspect.Live)
            {
                _screen.Inspect.JumpTo(
                    MapCoordinates.Round(found.East),
                    MapCoordinates.Round(found.North)
                );
                return true;
            }

            ControlId id = _screen.NodeFor(
                found.Fleet != null ? (IGameEntityWithGalaxyPosition)found.Fleet : found.Node
            );
            GraphNavigator navigator = ModEntry.Navigator;
            if (id != null && navigator != null)
            {
                navigator.FocusNode(id);
                return true;
            }

            // A fleet the tree has NO node for. The tree hangs a fleet under the system it is parked
            // at, under both ends of the starlane it is flying, under the DESTINATION of the
            // open-space crossing it is making, or - where that destination is a place the map has
            // never named - at the top level of the systems stop. So a free mover always has a row
            // now, and what is left here is a fleet parked at a system the map does not name and a
            // fleet flying a lane the map does not draw (es2-facts): the branch that would hold it
            // does not exist.
            // The map still draws such a fleet and the scanner still finds it, so the key answers with
            // the only "go to this fleet" this game has for one: the camera and the selection, the same
            // landing the inspect cursor's Enter makes on a fleet in its cell. There is no node to
            // announce the arrival, and a jump that says nothing at all reads as a key that did
            // nothing - so the line the scanner found it with is said again, which is the whole of what
            // arriving there means.
            if (found.Fleet != null)
            {
                GalaxyHudScreen.SelectFleet(found.Fleet);
                MessageBuilder arrival = new MessageBuilder();
                Instance(arrival, found, at, scope.Count, east, north);
                Voice.Say(arrival.Build(), true);
            }

            return true;
        }

        // ---- what is out there ----

        /// <summary>
        /// Everything the map is drawing, in the two kinds the scanner knows, each already sorted
        /// nearest-first from where the player is reading.
        ///
        /// Both lists every time, not only the one being read: cycling categories has to know whether
        /// the category next door holds anything before it decides to skip it, and that answer only
        /// exists once the other list has been built.
        /// </summary>
        private void Snapshot(
            out List<Found> systems,
            out List<Found> fleets,
            out double east,
            out double north
        )
        {
            systems = new List<Found>();
            fleets = new List<Found>();
            Reference(out east, out north);
            try
            {
                Empire empire = Gui.PlayerEmpire;
                Galaxy galaxy = Gui.Game == null ? null : Gui.Game.Galaxy;
                if (empire == null || galaxy == null)
                {
                    return;
                }

                DepartmentOfForeignAffairs foreign = empire.GetAgency<DepartmentOfForeignAffairs>();
                Systems(systems, galaxy, empire, foreign);
                Fleets(fleets, empire, foreign);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the scanner reading the map threw: " + e);
            }

            Sort(systems, east, north);
            Sort(fleets, east, north);
        }

        /// <summary>
        /// The star systems the map is naming. Special nodes are left out on purpose: a nebula or an
        /// asteroid field is a phenomenon the map draws, not a place with an allegiance, and the three
        /// scopes below "all" would have nothing to say about one.
        /// </summary>
        private static void Systems(
            List<Found> found,
            Galaxy galaxy,
            Empire empire,
            DepartmentOfForeignAffairs foreign
        )
        {
            HashSet<GameEntityGUID> mine = Mine(empire);
            IColonizedStarSystemRepositoryService colonies =
                Services.GetService<IColonizedStarSystemRepositoryService>();
            foreach (StarSystemNode node in galaxy.StarSystemNodes)
            {
                if (node is SpecialNode || !MapVisibility.Perceived(node, empire))
                {
                    continue;
                }

                int scope = mine.Contains(node.GUID)
                    ? ScopeFriendly
                    : Scope(Owner(colonies, node, empire), empire, foreign);
                found.Add(Make(node.LocalizedName, node.GalaxyPosition, scope, node, null));
            }
        }

        /// <summary>The systems that are the player's OWN - the same list the map's tree puts in its
        /// first region (<c>DepartmentOfTheInterior.ColonizedStarSystems</c>), which counts an outpost
        /// as yours where the label's colour does not: a place you hold is friendly whether or not it
        /// has grown into a colony yet.</summary>
        private static HashSet<GameEntityGUID> Mine(Empire empire)
        {
            HashSet<GameEntityGUID> mine = new HashSet<GameEntityGUID>();
            DepartmentOfTheInterior interior = empire.GetAgency<DepartmentOfTheInterior>();
            if (interior == null)
            {
                return mine;
            }

            foreach (ColonizedStarSystem colony in interior.ColonizedStarSystems)
            {
                if (colony.Node != null)
                {
                    mine.Add(colony.Node.GUID);
                }
            }

            return mine;
        }

        /// <summary>
        /// Whose system this is, by the map's own rule for whose colour it paints on the label
        /// (<c>StarSystemLabel.RebuildColonizedStarSystemsList</c>): among the colonies standing at
        /// the node, the ones this empire can see at all, preferring its own, and only those that are
        /// a COLONY rather than an outpost or a ruin. A node with none has no owner and is nobody's.
        /// </summary>
        private static Empire Owner(
            IColonizedStarSystemRepositoryService colonies,
            StarSystemNode node,
            Empire empire
        )
        {
            if (colonies == null)
            {
                return null;
            }

            ColonizedStarSystem main = null;
            foreach (ColonizedStarSystem colony in colonies.GetValues(node.NodePosition))
            {
                if (
                    (int)colony.Visibility[empire] >= 1
                    && (main == null || !ReferenceEquals(main.Empire, empire))
                    && colony.State == StarSystemState.Colony
                )
                {
                    main = colony;
                }
            }

            return main == null ? null : main.Empire;
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
                found.Add(
                    Make(
                        fleet.LocalizedName,
                        fleet.GalaxyPosition,
                        Scope(owner, empire, foreign),
                        null,
                        fleet
                    )
                );
            }
        }

        private static Found Make(
            string name,
            GalaxyPosition at,
            int scope,
            StarSystemNode node,
            Fleet fleet
        )
        {
            double east;
            double north;
            GalaxyCoordinates.Offsets(at, out east, out north);
            return new Found
            {
                Name = name,
                At = at,
                East = east,
                North = north,
                Scope = scope,
                Node = node,
                Fleet = fleet,
            };
        }

        /// <summary>
        /// Which way the player stands to whoever owns a thing.
        ///
        /// Friendly is the player's own and the empires allied to them; enemy is the ones the game
        /// says they are at WAR with, plus the pirates, who never appear in a war state at all and are
        /// hostile by default all the same (their own ladder runs Aggressive to Best friend, and only
        /// a bought peace takes them off the player's back). Everything else - the minor factions, the
        /// empires not yet met, a cold war, a peace, a truce, and anything with no owner - is neutral.
        ///
        /// This is deliberately NOT the map's own three-way split, which calls a cold war and every
        /// minor faction an enemy (<c>GuiFleetGroup.Title</c> compares against a state value that is
        /// -1 for every non-major state). Owner's taxonomy: at war is the line that matters when the
        /// question being asked is "what is nearby".
        /// </summary>
        private static int Scope(Empire owner, Empire empire, DepartmentOfForeignAffairs foreign)
        {
            if (owner == null)
            {
                return ScopeNeutral;
            }

            if (ReferenceEquals(owner, empire))
            {
                return ScopeFriendly;
            }

            DiplomaticRelation relation =
                foreign == null ? null : foreign.GetDiplomaticRelation(owner);
            DiplomaticRelationState state = relation == null ? null : relation.State;
            if (owner is PirateEmpire)
            {
                return state != null && state.Name == DiplomaticRelationState.Names.Pirate.Peace
                    ? ScopeNeutral
                    : ScopeEnemy;
            }

            if (state == null)
            {
                return ScopeNeutral;
            }

            if (state.IsWarState)
            {
                return ScopeEnemy;
            }

            if (
                state.Name == DiplomaticRelationState.Names.Major.Team
                || relation.HasAbility(DiplomaticAbilityDefinition.Names.Alliance)
            )
            {
                return ScopeFriendly;
            }

            return ScopeNeutral;
        }

        /// <summary>Nearest first, and where two things are the same distance away the one whose name
        /// comes first - so the same galaxy read twice reads the same way round.</summary>
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
            return by != 0
                ? by
                : string.Compare(one.Name, two.Name, StringComparison.Ordinal);
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

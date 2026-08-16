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
        private const int CategoryProbes = 2;

        // The three that are only ever asked "what is there", after the three that are asked "whose".
        // Each has the single subcategory "all", so the subcategory key on one of them comes round to
        // where it started and says so - which is the honest answer to "what else is there".
        private const int CategoryMarkers = 3;
        private const int CategoryPins = 4;
        private const int CategoryProjectiles = 5;
        private const int CategoryCount = 6;

        private const int ScopeAll = ScannerScopes.All;
        private const int ScopeFriendly = ScannerScopes.Friendly;
        private const int ScopeNeutral = ScannerScopes.Neutral;
        private const int ScopeEnemy = ScannerScopes.Enemy;

        /// <summary>How wide each category's row of the counts table is
        /// (<see cref="ScannerScopes"/>) - and so how many subcategories its key cycles through.
        /// </summary>
        private static readonly int[] Widths = new int[]
        {
            ScannerScopes.SystemWidth,
            ScannerScopes.AffiliationWidth,
            ScannerScopes.AffiliationWidth,
            ScannerScopes.SingleWidth,
            ScannerScopes.SingleWidth,
            ScannerScopes.SingleWidth,
        };

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

            /// <summary>What else is said about this one straight after its name, already composed -
            /// a probe's owner and its burn-out countdown. Null for the kinds whose name is all the
            /// scanner has to add to the pair.</summary>
            public string Extra;

            public GalaxyPosition At;

            /// <summary>How far from home, along each axis - the pair the map is spoken in, kept
            /// unrounded so the distance is measured before anything is rounded.</summary>
            public double East;
            public double North;

            /// <summary>Which subcategories of its category this belongs to, as a set: a system can
            /// be the enemy's AND their capital, and both scopes have to find it
            /// (<see cref="ScannerScopes"/>).</summary>
            public int Scopes;

            /// <summary>How far from where the player is reading, filled in when the list is sorted.
            /// </summary>
            public double Away;

            /// <summary>Whichever of the three this is. The jump needs the thing itself, not its name.
            /// </summary>
            public StarSystemNode Node;
            public Fleet Fleet;

            /// <summary>A probe's own row in the tree, worked out when the list was built - the page
            /// keys a probe's node on the star it is nearest to, which is a question only the page can
            /// answer.</summary>
            public ControlId Row;
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
            double east;
            double north;
            List<Found>[] world = Snapshot(out east, out north);
            int[][] counts = Counts(world);

            ScannerAnswer answer;
            bool held = Rearmed() || _cursor.Arm();
            if (held)
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

            Say(answer, tier, held, Scoped(world), east, north);
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
        private List<Found> Scoped(List<Found>[] world)
        {
            int at = _cursor.Category;
            List<Found> all = at >= 0 && at < world.Length ? world[at] : world[CategorySystems];
            if (_cursor.Subcategory == ScopeAll)
            {
                return all;
            }

            List<Found> some = new List<Found>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (ScannerScopes.Holds(all[i].Scopes, _cursor.Subcategory))
                {
                    some.Add(all[i]);
                }
            }

            return some;
        }

        /// <summary>The whole world as the cursor's rules ask about it: one row per category, one
        /// column per subcategory, a thing counted once in every subcategory it belongs to. The rows
        /// are of DIFFERENT widths on purpose - what a category can be asked about is a fact about
        /// that category, and a uniform table would have to pad the three that are only ever asked
        /// "what is there" with scopes that could never hold anything.</summary>
        private static int[][] Counts(List<Found>[] world)
        {
            int[][] counts = new int[CategoryCount][];
            for (int at = 0; at < CategoryCount; at++)
            {
                counts[at] = Row(world[at], Widths[at]);
            }

            return counts;
        }

        private static int[] Row(List<Found> found, int width)
        {
            int[] scopes = new int[found.Count];
            for (int i = 0; i < found.Count; i++)
            {
                scopes[i] = found[i].Scopes;
            }

            return ScannerScopes.Tally(scopes, width);
        }

        // ---- what it says ----

        /// <summary>
        /// What a press says, which depends on WHICH key was pressed and not only on where the cursor
        /// ended up.
        ///
        /// The arming press says the scope and stops: it moved nothing, so there is nothing found to
        /// report, and the player asked where they were rather than what is there.
        ///
        /// EVERY press that MOVES reads its landing (owner ruling, 2026-08-16): moving between
        /// categories or between subcategories is never silent while there is something there. What
        /// differs between the two is only how much of the scope is named in front of it - a CATEGORY
        /// step has changed both halves of where the cursor is and says the whole scope, a
        /// SUBCATEGORY step has changed one and says that half alone - and then both say the nearest
        /// thing. Saying the subcategory and stopping made the key answer "you are in an empty place"
        /// and "you are somewhere with things in it" with the same sentence, which is the one thing a
        /// scope line must never do.
        ///
        /// NO COUNT anywhere in the scope lines (owner ruling): the instance line already ends in "N of
        /// M", so the size of the scope arrives with the first thing in it and saying it twice is words
        /// in front of the answer. The one place a number would have been the whole answer - a scope
        /// standing empty - has its own sentence instead.
        /// </summary>
        private void Say(
            ScannerAnswer answer,
            Tier tier,
            bool held,
            List<Found> scope,
            double east,
            double north
        )
        {
            if (answer == ScannerAnswer.Empty)
            {
                Voice.Say(ModStrings.Format(ModStrings.GalaxyScannerEmpty, ScopeName()), true);
                return;
            }

            if (answer == ScannerAnswer.Scope && held)
            {
                Voice.Say(ScopeName(), true);
                return;
            }

            MessageBuilder message = new MessageBuilder();
            if (answer == ScannerAnswer.Scope)
            {
                message.Fragment(
                    tier == Tier.Subcategory ? SubcategoryName() : ScopeName()
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

            message.ListItemForcedComma(found.Extra);
            message.ListItemForcedComma(GalaxyCoordinates.Text(found.At));
            message.ListItemForcedComma(Away(found, east, north));
            message.ListItemForcedComma();
            message.PushFraction(index + 1, count);
        }

        /// <summary>
        /// Which way the thing lies from where the player is reading, as the two components of the
        /// offset - "23 south", "23 south, 1 west" (<see cref="CompassDirections.Offsets"/>).
        ///
        /// The components are the difference of the two ROUNDED pairs rather than the rounded
        /// difference, because the player hears both pairs: a thing at "0, -9" heard from a place at
        /// "0, 0" has to be nine south, and a rounding taken before the subtraction could make it
        /// eight. So the arithmetic the player can do in their head always comes out.
        ///
        /// A thing standing on the pair the player is reading from has no direction to give, and says
        /// so instead of saying nothing.
        /// </summary>
        private static string Away(Found found, double east, double north)
        {
            int sideways = MapCoordinates.Round(found.East) - MapCoordinates.Round(east);
            int up = MapCoordinates.Round(found.North) - MapCoordinates.Round(north);
            return sideways == 0 && up == 0
                ? ModStrings.Get(ModStrings.GalaxyScannerHere)
                : CompassDirections.Offsets(sideways, up);
        }

        /// <summary>The scope the cursor is in, both halves: which category, then which of its
        /// subcategories. Two whole localized labels put together by a template of the language's own,
        /// never an adjective glued to a noun.</summary>
        private string ScopeName()
        {
            return ModStrings.Format(
                ModStrings.GalaxyScannerScope,
                ModStrings.Get(CategoryKeys[_cursor.Category]),
                SubcategoryName()
            );
        }

        /// <summary>The subcategory half alone - what a step of the subcategory key changed. Kept per
        /// category rather than shared, so a language can inflect it for each.</summary>
        private string SubcategoryName()
        {
            return ModStrings.Get(ScopeKeys[_cursor.Category][_cursor.Subcategory]);
        }

        private static readonly string[] CategoryKeys = new string[]
        {
            ModStrings.GalaxyScannerSystems,
            ModStrings.GalaxyScannerFleets,
            ModStrings.GalaxyScannerProbes,
            ModStrings.GalaxyScannerQuestMarkers,
            ModStrings.GalaxyScannerPins,
            ModStrings.GalaxyScannerProjectiles,
        };

        private static readonly string[][] ScopeKeys = new string[][]
        {
            new string[]
            {
                ModStrings.GalaxyScannerSystemsAll,
                ModStrings.GalaxyScannerSystemsFriendly,
                ModStrings.GalaxyScannerSystemsNeutral,
                ModStrings.GalaxyScannerSystemsEnemy,
                ModStrings.GalaxyScannerSystemsHomeworld,
                ModStrings.GalaxyScannerSystemsMinorFactions,
                ModStrings.GalaxyScannerSystemsSpecial,
            },
            new string[]
            {
                ModStrings.GalaxyScannerFleetsAll,
                ModStrings.GalaxyScannerFleetsFriendly,
                ModStrings.GalaxyScannerFleetsNeutral,
                ModStrings.GalaxyScannerFleetsEnemy,
            },
            new string[]
            {
                ModStrings.GalaxyScannerProbesAll,
                ModStrings.GalaxyScannerProbesFriendly,
                ModStrings.GalaxyScannerProbesNeutral,
                ModStrings.GalaxyScannerProbesEnemy,
            },
            new string[] { ModStrings.GalaxyScannerQuestMarkersAll },
            new string[] { ModStrings.GalaxyScannerPinsAll },
            new string[] { ModStrings.GalaxyScannerProjectilesAll },
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
            double east;
            double north;
            List<Found>[] world = Snapshot(out east, out north);
            if (!Rearmed())
            {
                _cursor.Arm();
            }

            List<Found> scope = Scoped(world);
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

            GraphNavigator navigator = ModEntry.Navigator;

            // The rows the tree keys STRUCTURALLY - a probe, an ally's pin, a missile in flight - carry
            // their own node here, because those keys are the page's to build and a probe's hangs off
            // whichever star the map draws it nearest to. Opening whatever the row hangs under is the
            // page's own reveal; there is no fallback below it, because none of the three is a thing
            // the game lets anybody select (a fleet is).
            if (found.Row != null)
            {
                _screen.RevealRow(found.Row);
                if (navigator != null)
                {
                    navigator.FocusNode(found.Row);
                }

                return true;
            }

            ControlId id = _screen.NodeFor(
                found.Fleet != null ? (IGameEntityWithGalaxyPosition)found.Fleet : found.Node
            );
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
            }

            MessageBuilder arrival = new MessageBuilder();
            Instance(arrival, found, at, scope.Count, east, north);
            Voice.Say(arrival.Build(), true);
            return true;
        }

        // ---- what is out there ----

        /// <summary>
        /// Everything the map is showing, in every kind the scanner knows, each list already sorted
        /// nearest-first from where the player is reading.
        ///
        /// Every list every time, not only the one being read: cycling categories has to know whether
        /// the category next door holds anything before it decides to skip it, and that answer only
        /// exists once the other lists have been built.
        /// </summary>
        private List<Found>[] Snapshot(out double east, out double north)
        {
            List<Found>[] world = new List<Found>[CategoryCount];
            for (int at = 0; at < CategoryCount; at++)
            {
                world[at] = new List<Found>();
            }

            Reference(out east, out north);
            try
            {
                Empire empire = Gui.PlayerEmpire;
                Galaxy galaxy = Gui.Game == null ? null : Gui.Game.Galaxy;
                if (empire != null && galaxy != null)
                {
                    DepartmentOfForeignAffairs foreign =
                        empire.GetAgency<DepartmentOfForeignAffairs>();
                    Systems(world[CategorySystems], galaxy, empire, foreign);
                    Fleets(world[CategoryFleets], empire, foreign);
                    Probes(world[CategoryProbes], empire, foreign);
                    Markers(world[CategoryMarkers]);
                    Pins(world[CategoryPins]);
                    Projectiles(world[CategoryProjectiles]);
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

            return world;
        }

        /// <summary>
        /// Every quest marker the game is showing this empire AT A SYSTEM - the ones the system's own
        /// row already mentions, gathered here so that "where are my quests" is one sweep rather than
        /// a walk of the map. A marker planted on a fleet out in a starlane is not listed at all: the
        /// scanner is a list of places to GO to, and the tree has no row for a marker that is not at a
        /// system, so that entry could only ever refuse (owner's ruling -
        /// <see cref="GalaxyHudScreen.ScannedMarkers"/>).
        ///
        /// Named by the QUEST, which is the only name a marker has (<c>QuestMarker</c> carries an
        /// instance id and a target and no words of its own), and gated by the page's own walk of the
        /// journal (<see cref="GalaxyHudScreen.ScannedMarkers"/>) so the scanner and the system rows
        /// cannot disagree about which quests are being pointed at.
        /// </summary>
        private void Markers(List<Found> found)
        {
            IList<GalaxyHudScreen.ScannedMarker> markers = _screen.ScannedMarkers();
            for (int i = 0; i < markers.Count; i++)
            {
                GalaxyHudScreen.ScannedMarker it = markers[i];
                found.Add(Make(it.Quest, it.At, ScannerScopes.Only(), it.Node, null));
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
                Found made = Make(
                    GalaxyHudScreen.PinKind(pin),
                    pin.GalaxyPosition,
                    ScannerScopes.Only(),
                    null,
                    null
                );
                made.Row = GalaxyHudScreen.PinId(pin);
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
                Found made = Make(
                    ModStrings.Get(ModStrings.GalaxyObliteratorProjectile),
                    shot.GalaxyPosition,
                    ScannerScopes.Only(),
                    null,
                    null
                );
                made.Row = GalaxyHudScreen.ProjectileId(shot);
                found.Add(made);
            }
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
            Galaxy galaxy,
            Empire empire,
            DepartmentOfForeignAffairs foreign
        )
        {
            HashSet<GameEntityGUID> mine = Mine(empire);
            HashSet<GameEntityGUID> homes = Homes(empire);
            IColonizedStarSystemRepositoryService colonies =
                Services.GetService<IColonizedStarSystemRepositoryService>();
            foreach (StarSystemNode node in galaxy.StarSystemNodes)
            {
                if (!MapVisibility.Perceived(node, empire))
                {
                    continue;
                }

                int affiliation = mine.Contains(node.GUID)
                    ? ScopeFriendly
                    : Scope(Owner(colonies, node, empire), empire, foreign);
                int scopes = ScannerScopes.System(
                    affiliation,
                    node is SpecialNode,
                    homes.Contains(node.GUID),
                    Minor(colonies, node, empire)
                );
                found.Add(Make(node.LocalizedName, node.GalaxyPosition, scopes, node, null));
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

        /// <summary>
        /// Whether a minor faction lives on this system.
        ///
        /// Asked of ALL the colonies standing at the node, not of the one whose colour the label
        /// paints (<see cref="Owner"/>): a minor faction shares its system with whoever settles a
        /// planet there, and that owner rule prefers the player's own colony, so asking it would hide
        /// exactly the faction sitting in the player's own back garden - which is the one a player
        /// sweeping this scope most wants to find.
        ///
        /// The gate is the same one the ownership answer uses, <c>Visibility[empire] >= 1</c>, so
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
                if ((int)colony.Visibility[empire] >= 1 && colony.Empire is MinorEmpire)
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
                found.Add(
                    Make(
                        fleet.LocalizedName,
                        fleet.GalaxyPosition,
                        ScannerScopes.Owned(Scope(owner, empire, foreign)),
                        null,
                        fleet
                    )
                );
            }
        }

        private static Found Make(
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

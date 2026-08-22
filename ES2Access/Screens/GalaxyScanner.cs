using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;
using UnityEngine;

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
    /// IT IS NOT A MODE, BUT IT IS A MODE OF THE MAP WIDGET. There is no key that turns it on and
    /// nothing to leave: the three chords are live for exactly as long as the tree cursor is standing
    /// on the map stop (<see cref="GalaxyHudScreen.CursorOnMap"/>), alongside ordinary tree navigation
    /// and alongside the inspect cursor. That is what makes it usable in the middle of doing something
    /// else - the player asks where the nearest enemy is without giving up the control they were
    /// standing on. Escape means what it always meant here.
    ///
    /// On the screen's OTHER stops - the zoom slider, the HUD buttons, the view title - and on every
    /// other page, the chords are unclaimed and inert: nothing is consumed, nothing is said, nothing
    /// moves. The scoping is the inspect cursor's, for the same reason (<see cref="GalaxyInspect"/>):
    /// a key set belonging to one widget must not be taken from the stops the player walks to next.
    /// What leaving the map does NOT do is reset anything - the parked scope, the per-category memory
    /// and the armed flag are all still there when the player tabs back, so the next press resumes the
    /// sweep instead of re-announcing where it stood.
    ///
    /// THE LISTS ARE BUILT ON THE PRESS AND THROWN AWAY. Nothing is cached between presses and nothing
    /// runs per frame: the answer depends on where the player is reading FROM, which moves with every
    /// arrow key, so a cached list would be sorted from somewhere the player has left. Rebuilding is a
    /// walk of the galaxy's nodes and one walk of the visible-fleet repository, which is what one
    /// keystroke can afford and what no frame could.
    ///
    /// WHAT IS REMEMBERED ACROSS A REBUILD IS AN IDENTITY, NOT AN INDEX. Half the categories now
    /// derive their subcategories from what is out there - one per kind of anomaly, of curiosity, of
    /// resource, in the language's own alphabetical order - so both the column a category was left in
    /// and the row the cursor was standing on can be at different indexes on the next press. The
    /// cursor therefore remembers the subcategory's NAME and the thing's KEY
    /// (<see cref="ScannerCursor.Reseat"/>), which is what lets a player walk the map between two
    /// presses - re-sorting every list around where they now are - and still step on from the very
    /// thing they were last told about.
    ///
    /// WHERE IT MEASURES FROM is the place the player is reading: the inspect cursor's centre while
    /// that mode is up, otherwise whatever place on the map the tree cursor is standing on, and home
    /// when the cursor is on none (the HUD, the turn controls). So "nearest" always means nearest to
    /// what the player is looking at, and moving the inspect cursor and scanning again re-sorts the
    /// same list around the new place.
    ///
    /// WHAT IT CAN SEE is what the map draws and nothing else - the same node gate the tree and the
    /// inspect cursor ask (<see cref="MapVisibility.Perceived"/>), the same fleet repository the
    /// map's own lozenges are drawn from (<see cref="FleetPresence.Drawing"/>), and for everything
    /// found on a PLANET the orbital card's own gates: the system is surveyed, the game is showing
    /// this empire the system's planets, and the planet is perceived. A scanner reading off the
    /// simulation would be the shortest route there is to handing the player the fog's contents.
    /// </summary>
    internal sealed class GalaxyScanner
    {
        // The taxonomy, as the two indexes the cursor is held in. Categories first: what KIND of thing
        // is being looked for. "All" is subcategory zero of every category that has one deliberately -
        // it is the one scope that can never be empty while the category holds anything, so cycling
        // into a category always has somewhere to land.
        //
        // The ORDER is the owner's (2026-08-22): the places first, then what can be done with them,
        // then what is out there to find, then what is moving. A player sweeping the map reads down
        // it, so the ordering is the mod's answer to "what am I most likely to be looking for".
        private const int CategorySystems = 0;

        /// <summary>Worlds this empire could settle - the ones standing free, and the ones somebody
        /// else is already sitting on that this empire's technology could take.</summary>
        private const int CategoryColonizable = 1;

        /// <summary>Every way out of the known map: a drawn lane or wormhole whose far end the player
        /// has not perceived. The one category whose things are EDGES rather than places.</summary>
        private const int CategoryUnexplored = 2;

        // The four whose subcategories are derived from what is out there rather than written down
        // here: one per kind found, in the language's own alphabetical order, behind an "all".
        private const int CategoryAnomalies = 3;
        private const int CategoryCuriosities = 4;
        private const int CategoryLuxury = 5;
        private const int CategoryStrategic = 6;

        /// <summary>Squares of the player's OWN influence that somebody else's field is winning - the
        /// one category whose things are not things at all but places, and the one whose "whose" is
        /// already settled by what the category IS (they are all the player's ground, being taken).
        /// So it has the single subcategory "all", and its affiliation question would have exactly one
        /// answer.</summary>
        private const int CategoryContestedInfluence = 7;

        // The two that are asked "whose", after everything that is asked "what is there".
        private const int CategoryFleets = 8;
        private const int CategoryProbes = 9;

        // The three that are only ever asked "what is there". Each has the single subcategory "all",
        // so the subcategory key on one of them comes round to where it started and says so - which is
        // the honest answer to "what else is there".
        private const int CategoryPins = 10;
        private const int CategoryProjectiles = 11;
        private const int CategoryMarkers = 12;

        private const int CategoryCount = 13;

        private const int ScopeAll = ScannerScopes.All;
        private const int ScopeFriendly = ScannerScopes.Friendly;
        private const int ScopeNeutral = ScannerScopes.Neutral;
        private const int ScopeEnemy = ScannerScopes.Enemy;

        public GalaxyScanner(GalaxyHudScreen screen)
        {
            _screen = screen;
        }

        /// <summary>Whether the scanner's chords mean the scanner at this moment: the tree cursor is
        /// standing on the map widget the scanner is a mode of. Everything else on this screen - the
        /// zoom slider, the HUD buttons, the view title - is a place the chords do not reach, and the
        /// state the cursor is holding survives the trip there untouched.</summary>
        public static bool Active
        {
            get { return GalaxyHudScreen.CursorOnMap(); }
        }

        /// <summary>
        /// What <c>ModInput</c>'s conditional claim asks: the scanner's keys are taken from the game
        /// only while the tree cursor is standing on the MAP WIDGET of the galaxy page AND the player
        /// is physically holding a modifier.
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
            return Active && KeyboardBinding.AnyModifierHeld;
        }

        /// <summary>Drop the scanner's position - mod teardown. The lists were never held.</summary>
        public void Forget()
        {
            _cursor.Forget();
            _empire = null;
            _labels = null;
            ScannerCost.Forget();
        }

        /// <summary>One key, offered to the scanner after the inspect cursor has passed on it. True
        /// when the scanner took it - which it never is away from the map widget, because the same
        /// question the claim asks has to be asked here too: an injected or unclaimed key still
        /// arrives, and a chord that stepped the list from the HUD button strip is the defect the
        /// scoping is for.</summary>
        public bool HandleKey(string actionKey)
        {
            if (!Active)
            {
                return false;
            }

            try
            {
                switch (actionKey)
                {
                    case MapActions.ScanCategoryNext:
                        return Scan(1, Tier.Category);
                    case MapActions.ScanCategoryPrev:
                        return Scan(-1, Tier.Category);
                    case MapActions.ScanSubcategoryNext:
                        return Scan(1, Tier.Subcategory);
                    case MapActions.ScanSubcategoryPrev:
                        return Scan(-1, Tier.Subcategory);
                    case MapActions.ScanNext:
                        return Scan(1, Tier.Instance);
                    case MapActions.ScanPrev:
                        return Scan(-1, Tier.Instance);
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
            /// a probe's owner and its burn-out countdown, a colonizable world's whole description.
            /// Null for the kinds whose name is all the scanner has to add to the pair.</summary>
            public string Extra;

            /// <summary>What KIND of thing this is, where the category's subcategories are the kinds
            /// found rather than a list written down here - the anomaly's name, the curiosity's, the
            /// resource's. It is both the column this belongs in and, in the category's "all", the
            /// first half of what the row says. Null everywhere else.</summary>
            public string Kind;

            /// <summary>What this thing IS, across a rebuild - the identity the cursor is re-seated
            /// by. Not the name: two planets can share one, and the same planet can be at a different
            /// index every press.</summary>
            public string Key;

            public GalaxyPosition At;

            /// <summary>How far from home, along each axis - the pair the map is spoken in, kept
            /// unrounded so the distance is measured before anything is rounded.</summary>
            public double East;
            public double North;

            /// <summary>Which subcategories of its category this belongs to, as a set: a system can
            /// be the enemy's AND their capital, and both scopes have to find it
            /// (<see cref="ScannerScopes"/>). Unused by the categories whose columns are kinds - those
            /// belong to exactly one, and say which in <see cref="Kind"/>.</summary>
            public int Scopes;

            /// <summary>How far from where the player is reading, filled in when the list is sorted.
            /// </summary>
            public double Away;

            /// <summary>Whichever of these this is. The jump needs the thing itself, not its name.
            /// </summary>
            public StarSystemNode Node;
            public Fleet Fleet;
            public Planet Planet;
            public Link Lane;

            /// <summary>Which orbit the planet is in - the index its node is keyed by, which is a
            /// fact about the system and not about the planet.</summary>
            public int Orbit;

            /// <summary>A probe's own row in the tree, worked out when the list was built - the page
            /// keys a probe's node on the star it is nearest to, which is a question only the page can
            /// answer.</summary>
            public ControlId Row;

            /// <summary>A landing the PAGE has already resolved for this one - a quest marker, whose
            /// node is the page's to key and whose kind depends on whether it stands at a system.
            /// </summary>
            public MapTarget Target;

            /// <summary>Whether <see cref="Target"/> is filled.</summary>
            public bool Targeted;

            /// <summary>Whether going to this one means the INSPECT CURSOR and nothing else - a square
            /// of sky, which has no node, no row and nothing to select. Every other kind has a landing
            /// in the tree; this one's landing does not exist until the cursor is armed.</summary>
            public bool Square;
        }

        /// <summary>Everything one press was decided from: the lists, the names of every column, and
        /// the counts the cursor's rules are asked about.</summary>
        private sealed class Snap
        {
            public List<Found>[] World;
            public string[][] Labels;
            public ScannerTable Table;
        }

        // ---- one press ----

        /// <summary>
        /// A press of one of the three tiers: rebuild the world, move the cursor, and say what it now
        /// points at.
        ///
        /// The whole snapshot is taken before the cursor is asked anything, because the cursor's own
        /// rules - skip a scope with nothing in it, come back to the nearest thing - are questions
        /// about the counts, and the counts are what the snapshot is.
        ///
        /// The cursor is re-seated on the thing it was standing on BEFORE the press is acted on: the
        /// list was rebuilt and re-sorted around wherever the player is now reading from, so the index
        /// the cursor is holding can point at something it was never told about.
        /// </summary>
        private bool Scan(int delta, Tier tier)
        {
            double east;
            double north;
            Snap snap = Snapshot(out east, out north);

            ScannerAnswer answer;
            bool held = Rearmed() || _cursor.Arm();
            _cursor.Settle(snap.Table);
            _cursor.Reseat(snap.Table, Keys(Scoped(snap)));
            if (held)
            {
                answer = _cursor.Hold(snap.Table);
            }
            else
            {
                switch (tier)
                {
                    case Tier.Category:
                        answer = _cursor.CycleCategory(delta, snap.Table);
                        break;
                    case Tier.Subcategory:
                        answer = _cursor.CycleSubcategory(delta, snap.Table);
                        break;
                    default:
                        answer = _cursor.Step(delta, snap.Table);
                        break;
                }
            }

            List<Found> scope = Scoped(snap);
            _cursor.Landed(Keys(scope));
            Say(answer, tier, held, scope, east, north);
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
        private List<Found> Scoped(Snap snap)
        {
            int at = _cursor.Category;
            if (at < 0 || at >= snap.World.Length)
            {
                at = CategorySystems;
            }

            List<Found> all = snap.World[at];
            string[] labels = snap.Labels[at];
            int sub = _cursor.Subcategory;
            List<Found> some = new List<Found>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (Holds(all[i], at, sub, labels))
                {
                    some.Add(all[i]);
                }
            }

            return some;
        }

        /// <summary>What the cursor re-seats itself by - the identities of a scope's things in the
        /// order they now stand in.</summary>
        private static IList<string> Keys(List<Found> scope)
        {
            string[] keys = new string[scope.Count];
            for (int i = 0; i < scope.Count; i++)
            {
                keys[i] = scope[i].Key;
            }

            return keys;
        }

        /// <summary>Whether a thing belongs in one of its category's columns. Two rules, because there
        /// are two kinds of taxonomy here: a set of memberships written down in the source, and a
        /// column per KIND found out there.</summary>
        private static bool Holds(Found found, int category, int subcategory, string[] labels)
        {
            if (!Kinds(category))
            {
                return ScannerScopes.Holds(found.Scopes, subcategory);
            }

            return ScannerScopes.HoldsKind(
                found.Kind,
                subcategory,
                subcategory >= 0 && subcategory < labels.Length ? labels[subcategory] : null
            );
        }

        /// <summary>Whether a category's subcategories are the KINDS of thing it found rather than a
        /// list of questions written down here.</summary>
        private static bool Kinds(int category)
        {
            return category == CategoryAnomalies
                || category == CategoryCuriosities
                || category == CategoryLuxury
                || category == CategoryStrategic;
        }

        /// <summary>The whole world as the cursor's rules ask about it: one row per category, one
        /// column per subcategory, a thing counted once in every subcategory it belongs to. The rows
        /// are of DIFFERENT widths on purpose - what a category can be asked about is a fact about
        /// that category, and a uniform table would have to pad the ones that are only ever asked
        /// "what is there" with scopes that could never hold anything.</summary>
        private static ScannerTable Table(List<Found>[] world, string[][] labels)
        {
            int[][] counts = new int[CategoryCount][];
            for (int at = 0; at < CategoryCount; at++)
            {
                int width = labels[at].Length;
                int[] row = new int[width];
                List<Found> found = world[at];
                for (int i = 0; i < found.Count; i++)
                {
                    for (int sub = 0; sub < width; sub++)
                    {
                        if (Holds(found[i], at, sub, labels[at]))
                        {
                            row[sub]++;
                        }
                    }
                }

                counts[at] = row;
            }

            return new ScannerTable(counts, labels);
        }

        // ---- what it says ----

        /// <summary>
        /// What a press says, which depends on WHICH key was pressed and not only on where the cursor
        /// ended up.
        ///
        /// NO PRESS THAT LANDS ON SOMETHING IS SILENT ABOUT WHAT IT LANDED ON (owner ruling,
        /// 2026-08-17). The arming press moves nothing, but it is still standing on something, so it
        /// says the whole scope AND the thing the cursor is parked on - whichever tier's key armed it.
        /// Saying the scope alone told the player which list they were in and then left the list
        /// unread, which is the same defect the subcategory tier had.
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

            MessageBuilder message = new MessageBuilder();
            if (answer == ScannerAnswer.Scope)
            {
                // The subcategory tier names the half it changed - but only when it CHANGED one. An
                // arming press changed neither half, so it names both: the player is being told where
                // the scanner already stood, and half of that is not a place.
                message.Fragment(
                    tier == Tier.Subcategory && !held ? SubcategoryName() : ScopeName()
                );
            }

            int at = _cursor.Index;
            if (at >= 0 && at < scope.Count)
            {
                Instance(
                    message,
                    Spoken(scope[at]),
                    Detail(scope[at]),
                    scope[at],
                    at,
                    scope.Count,
                    east,
                    north
                );
            }

            Voice.Say(message.Build(), true);
        }

        /// <summary>
        /// What a found thing is CALLED here, which depends on which column it is being read out of.
        ///
        /// In a category whose columns are kinds, a per-kind column has already said the kind - every
        /// row in it is one - so the row is the planet and nothing else. The "all" column has not, and
        /// a list of bare planet names there would leave the player unable to tell an anomaly from a
        /// curiosity, so the kind goes in front of it through a template of the language's own.
        /// </summary>
        private string Spoken(Found found)
        {
            return found.Kind != null && Kinds(_cursor.Category) && _cursor.Subcategory == ScopeAll
                ? ModStrings.Format(ModStrings.GalaxyScannerOnPlanet, found.Kind, found.Name)
                : found.Name;
        }

        /// <summary>
        /// What else is said about a thing straight after its name.
        ///
        /// Most kinds have it composed already - a probe's owner, its countdown. A COLONIZABLE world
        /// does not, and deliberately: its description is the longest line the scanner says and much
        /// the most expensive to compose, and exactly one row of the list is ever read out. Composing
        /// them all on the way in would build a sentence for every settleable planet in the galaxy and
        /// throw all but one away, on a key the player holds down.
        /// </summary>
        private string Detail(Found found)
        {
            if (found.Extra != null)
            {
                return found.Extra;
            }

            return _cursor.Category == CategoryColonizable && found.Planet != null
                ? Description(found.Planet, Gui.PlayerEmpire)
                : null;
        }

        /// <summary>One thing found, said the way the map says a place: what it is called, where it is
        /// on the map, then how far away it is and which way - and last, where in the list it stands,
        /// which is what tells the player how much more there is.</summary>
        private static void Instance(
            MessageBuilder message,
            string name,
            string extra,
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
                message.Fragment(name);
            }
            else
            {
                message.ListItemForcedComma(name);
            }

            message.ListItemForcedComma(extra);
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

        /// <summary>The subcategory half alone - what a step of the subcategory key changed. Read off
        /// the snapshot's own column names rather than composed again here, so the name the cursor is
        /// remembering and the name the player hears are the same string.</summary>
        private string SubcategoryName()
        {
            string label =
                _labels == null ? null : Label(_labels, _cursor.Category, _cursor.Subcategory);
            return label ?? string.Empty;
        }

        private static string Label(string[][] labels, int category, int subcategory)
        {
            if (category < 0 || category >= labels.Length)
            {
                return null;
            }

            string[] row = labels[category];
            return subcategory < 0 || subcategory >= row.Length ? null : row[subcategory];
        }

        /// <summary>The column names of the last snapshot - what the cursor's memory is keyed by and
        /// what the scope line says.</summary>
        private string[][] _labels;

        private static readonly string[] CategoryKeys = new string[]
        {
            ModStrings.GalaxyScannerSystems,
            ModStrings.GalaxyScannerColonizable,
            ModStrings.GalaxyScannerUnexplored,
            ModStrings.GalaxyScannerAnomalies,
            ModStrings.GalaxyScannerCuriosities,
            ModStrings.GalaxyScannerLuxury,
            ModStrings.GalaxyScannerStrategic,
            ModStrings.GalaxyScannerContestedInfluence,
            ModStrings.GalaxyScannerFleets,
            ModStrings.GalaxyScannerProbes,
            ModStrings.GalaxyScannerPins,
            ModStrings.GalaxyScannerProjectiles,
            ModStrings.GalaxyScannerQuestMarkers,
        };

        /// <summary>The subcategories a category has whatever is out there - the questions that are a
        /// fact about the category. The four whose columns are KINDS have only their "all" here; the
        /// rest of their row is built from what was found.</summary>
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
                ModStrings.GalaxyScannerColonizableUnoccupied,
                ModStrings.GalaxyScannerColonizableOccupied,
            },
            new string[] { ModStrings.GalaxyScannerUnexploredAll },
            new string[] { ModStrings.GalaxyScannerAnomaliesAll },
            new string[] { ModStrings.GalaxyScannerCuriositiesAll },
            new string[] { ModStrings.GalaxyScannerLuxuryAll },
            new string[] { ModStrings.GalaxyScannerStrategicAll },
            new string[] { ModStrings.GalaxyScannerContestedInfluenceAll },
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
            new string[] { ModStrings.GalaxyScannerPinsAll },
            new string[] { ModStrings.GalaxyScannerProjectilesAll },
            new string[] { ModStrings.GalaxyScannerQuestMarkersAll },
        };

        /// <summary>
        /// What every column of every category is CALLED, this press.
        ///
        /// The fixed taxonomies are localized straight out of their keys. The four whose columns are
        /// kinds are built from what was found: "all", and then one column per kind, sorted by the
        /// name the player will hear - so the list reads in the order the language puts it in rather
        /// than in whatever order the galaxy was walked.
        /// </summary>
        private static string[][] Labels(List<Found>[] world)
        {
            string[][] labels = new string[CategoryCount][];
            for (int at = 0; at < CategoryCount; at++)
            {
                string[] keys = ScopeKeys[at];
                if (!Kinds(at))
                {
                    string[] fixedNames = new string[keys.Length];
                    for (int i = 0; i < keys.Length; i++)
                    {
                        fixedNames[i] = ModStrings.Get(keys[i]);
                    }

                    labels[at] = fixedNames;
                    continue;
                }

                List<string> kinds = new List<string>();
                List<Found> found = world[at];
                for (int i = 0; i < found.Count; i++)
                {
                    string kind = found[i].Kind;
                    if (kind != null && !kinds.Contains(kind))
                    {
                        kinds.Add(kind);
                    }
                }

                kinds.Sort(StringComparer.CurrentCulture);
                string[] names = new string[kinds.Count + 1];
                names[0] = ModStrings.Get(keys[0]);
                for (int i = 0; i < kinds.Count; i++)
                {
                    names[i + 1] = kinds[i];
                }

                labels[at] = names;
            }

            return labels;
        }

        // ---- going there ----

        /// <summary>
        /// Go to whatever the scanner is pointing at.
        ///
        /// The scanner's only job here is to say WHAT it found (<see cref="Target"/>) - a place, a
        /// world, a thing standing at a bare point - and the page's one landing decides the rest:
        /// whether the free inspect cursor stays up, where the tree cursor goes, and whether the
        /// camera zooms in or slides across (<see cref="GalaxyHudScreen.GoTo"/>,
        /// <see cref="MapLandings"/>, owner ruling 2026-08-22). Two kinds are the scanner's own: a
        /// square of contested sky, which arms the free cursor because it has no node at all, and a
        /// fleet the tree has no row for, which is announced here because nothing else will.
        /// </summary>
        private bool GoTo()
        {
            double east;
            double north;
            Snap snap = Snapshot(out east, out north);
            if (!Rearmed())
            {
                _cursor.Arm();
            }

            _cursor.Settle(snap.Table);
            _cursor.Reseat(snap.Table, Keys(Scoped(snap)));
            List<Found> scope = Scoped(snap);
            _cursor.Landed(Keys(scope));
            int at = _cursor.Index;
            if (at < 0 || at >= scope.Count)
            {
                Voice.Say(ModStrings.Format(ModStrings.GalaxyScannerEmpty, ScopeName()), true);
                return true;
            }

            Found found = scope[at];

            // A SQUARE OF SKY is the one kind that turns the inspect cursor ON (owner decision,
            // 2026-08-21). Every other kind has a landing in the tree, so leaving the cursor alone
            // costs the player nothing; a square has no node, no row and nothing to select, and going
            // to one without the cursor could only move the camera and say nothing. So the mode is
            // armed by its own entry path, which announces itself exactly as Ctrl+I does and opens the
            // cell on the square rather than where the tree cursor was standing.
            if (found.Square)
            {
                int x = MapCoordinates.Round(found.East);
                int y = MapCoordinates.Round(found.North);
                if (!GalaxyInspect.Live)
                {
                    _screen.Inspect.ArmAt(x, y);
                }
                else
                {
                    _screen.Inspect.JumpTo(x, y);
                }

                return true;
            }

            // Everything else goes through the PAGE's one landing, which owns the whole decision -
            // whether the free cursor stays up, where the tree cursor goes and what the camera does
            // (<see cref="GalaxyHudScreen.GoTo"/>, <see cref="MapLandings"/>). Before 2026-08-22 this
            // method answered those questions itself and got the planet case wrong: it jumped the CELL
            // onto a world, which the cell cannot read.
            if (_screen.GoTo(Target(found), MapCamera.Auto))
            {
                return true;
            }

            // A fleet the tree has NO node for. The tree hangs a fleet under the system it is parked
            // at, under both ends of the starlane it is flying, under the DESTINATION of the
            // open-space crossing it is making, or - where that destination is a place the map has
            // never named - at the top level of the systems stop. So a free mover always has a row
            // now, and what is left here is a fleet parked at a system the map does not name and a
            // fleet flying a lane the map does not draw (es2-facts): the branch that would hold it
            // does not exist. The landing has already selected it and moved the camera - the map's own
            // "go to that fleet" - and there is no node to announce the arrival, so the line the
            // scanner found it with is said again, which is the whole of what arriving there means.
            MessageBuilder arrival = new MessageBuilder();
            Instance(arrival, Spoken(found), Detail(found), found, at, scope.Count, east, north);
            Voice.Say(arrival.Build(), true);
            return true;
        }

        /// <summary>
        /// What one result IS, as the page's landing needs it: which node the tree has for it, which
        /// kind of thing it is, and where it stands.
        ///
        /// The scanner is the one caller that knows the difference between a world and the system it
        /// orbits, so it is the one that says so; the landing never re-derives it (a landing that had
        /// to guess put the free cursor on a planet it cannot read).
        /// </summary>
        private MapTarget Target(Found found)
        {
            // A quest marker's landing is the page's own, resolved when the list was built: whether it
            // is a child of a system or a row of its own is a fact about the tree.
            if (found.Targeted)
            {
                return found.Target;
            }

            // The rows whose keys are the PAGE's to build - a probe, an ally's pin, a missile in
            // flight. All three sit at the top of the stop, so there is no branch to open first.
            if (found.Row != null)
            {
                return MapTarget.Point(found.Row, found.At);
            }

            // A PLANET and everything found ON one: drawn at the star, read from the tree and from the
            // close-up view, so the free cursor ends and the camera comes in.
            if (found.Planet != null && found.Node != null)
            {
                return MapTarget.Under(
                    found.Node,
                    GalaxyHudScreen.PlanetId(found.Node, found.Orbit),
                    found.At
                );
            }

            // A STARLANE is map geometry the cell can read - it names every lane crossing it - and its
            // spoken place is the system it leaves, so it lands like a place.
            if (found.Lane != null && found.Node != null)
            {
                return MapTarget.Place(
                    found.Node,
                    GalaxyHudScreen.LaneId(found.Node, found.Lane),
                    found.Node.GalaxyPosition
                );
            }

            MapTarget named;
            if (
                _screen.TargetFor(
                    found.Fleet != null ? (IGameEntityWithGalaxyPosition)found.Fleet : found.Node,
                    out named
                )
            )
            {
                return named;
            }

            return found.Fleet != null
                ? MapTarget.LooseFleet(found.Fleet, found.At)
                : MapTarget.Nowhere(found.At);
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
        private Snap Snapshot(out double east, out double north)
        {
            List<Found>[] world = new List<Found>[CategoryCount];
            for (int at = 0; at < CategoryCount; at++)
            {
                world[at] = new List<Found>();
            }

            Reference(out east, out north);
            ScannerCost.Begin();
            try
            {
                Empire empire = Gui.PlayerEmpire;
                if (empire != null && GameGalaxy.Present())
                {
                    DepartmentOfForeignAffairs foreign =
                        empire.GetAgency<DepartmentOfForeignAffairs>();
                    Systems(world[CategorySystems], empire, foreign);
                    Worlds(world, empire);
                    Unexplored(world[CategoryUnexplored], empire);
                    Fleets(world[CategoryFleets], empire, foreign);
                    Probes(world[CategoryProbes], empire, foreign);
                    Markers(world[CategoryMarkers], empire);
                    Pins(world[CategoryPins]);
                    Projectiles(world[CategoryProjectiles]);
                    ContestedGround(world[CategoryContestedInfluence], empire);
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

            _labels = Labels(world);
            ScannerCost.End();
            return new Snap
            {
                World = world,
                Labels = _labels,
                Table = Table(world, _labels),
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
            HashSet<GameEntityGUID> mine = Mine(empire);
            HashSet<GameEntityGUID> homes = Homes(empire);
            IColonizedStarSystemRepositoryService colonies =
                Services.GetService<IColonizedStarSystemRepositoryService>();
            foreach (StarSystemNode node in GameGalaxy.StarSystemNodes())
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
                    Colonizable(colonizable, node, planet, i, name, empire, able);
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
                    found.Add(OnPlanet(node, planet, orbit, name, kind, "anomaly"));
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
            for (int i = 0; i < planet.Curiosities.Count; i++)
            {
                Curiosity curiosity = planet.Curiosities[i];
                if (curiosity == null || !curiosity.CanBeSeen(empire))
                {
                    continue;
                }

                string kind = CuriosityTitle(curiosity, titles);
                if (Once(seen, kind))
                {
                    found.Add(OnPlanet(node, planet, orbit, name, kind, "curiosity"));
                }
            }
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
                        OnPlanet(node, planet, orbit, name, kind, "deposit")
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
            Dictionary<string, bool> able
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
        private static string Description(Planet planet, Empire empire)
        {
            MessageBuilder details = new MessageBuilder();
            details.ListItem(SizeAndType(planet));
            Dictionary<string, string> titles = new Dictionary<string, string>();
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
        /// simulation object and named by the game's titles for them, which is where those words live:
        /// the panel draws an icon beside each and writes no caption anywhere.
        /// </summary>
        private static void Outputs(MessageBuilder details, Planet planet)
        {
            for (int i = 0; i < Potential.Length; i++)
            {
                Amplitude.StaticString property = Potential[i];
                string value = GlobalHud.Amount(planet.GetPropertyValue(property), false, 0);
                if (value == null)
                {
                    continue;
                }

                details.ListItem(
                    ModStrings.Format(
                        ModStrings.GalaxyScannerOutput,
                        AgeText.Clean(Gui.GetLocalizedTitle(property)),
                        value
                    )
                );
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
        /// lane rows, its fleet legs and its count phrases are built from, so a lane is numbered here
        /// exactly as the tree numbers it, clockwise from north. A wormhole is one of them where the
        /// empire has the technology to be shown wormholes at all, and says it is one.
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
                        i + 1,
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
                        "fleet/" + fleet.GUID,
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

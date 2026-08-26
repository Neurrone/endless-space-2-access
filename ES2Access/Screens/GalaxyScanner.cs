using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;
using ES2Access.UI.Settings;
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
        //
        // BEHIND ALL OF THEM COME THE PLAYER'S OWN (owner ruling 2026-08-24): three fixed slots,
        // each empty or holding a category the player wrote (<see cref="ScannerCustomSlots"/>), and
        // the LAST thing the category cycle reaches. They went in front first and that was wrong: the
        // cursor starts at category zero, so the very first scanner press of a game - the one that
        // says where the scanner already stands rather than moving it - landed on slot one and
        // answered "none found" to a player who had never configured a slot.
        //
        // The three rows are always THERE, configured or not, so the thirteen built-in indexes never
        // move; a slot with nothing in it - unconfigured, or configured and matching nothing this
        // press - is a row of the table holding nothing and is skipped by the same rule that skips a
        // built-in category with nothing in it.
        private const int SlotCount = ScannerCustomSlots.Count;

        /// <summary>How many categories the scanner writes down for itself - and therefore the index
        /// the player's own three begin at.</summary>
        private const int BuiltInCount = 13;

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

        private const int CategoryCount = BuiltInCount + SlotCount;

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

        /// <summary>What the six QUICK keys' claim asks. They are bare keys on their own punctuation
        /// - the game binds nothing to any of them and the mod's type-ahead only ever takes letters
        /// - so the modifier half of the claim above has nothing to separate here: standing on the
        /// map widget is the whole of it.</summary>
        public static bool QuickKeysClaimed()
        {
            return Active;
        }

        /// <summary>Drop the scanner's position - mod teardown. The lists were never held.</summary>
        public void Forget()
        {
            _cursor.Forget();
            _walk.Forget();
            _empire = null;
            _labels = null;
            _names = null;
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
                    case MapActions.ScanCustom1Next:
                        return Quick(0, 1, actionKey);
                    case MapActions.ScanCustom1Prev:
                        return Quick(0, -1, actionKey);
                    case MapActions.ScanCustom2Next:
                        return Quick(1, 1, actionKey);
                    case MapActions.ScanCustom2Prev:
                        return Quick(1, -1, actionKey);
                    case MapActions.ScanCustom3Next:
                        return Quick(2, 1, actionKey);
                    case MapActions.ScanCustom3Prev:
                        return Quick(2, -1, actionKey);
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

            /// <summary>The same kind, in the GAME's own internal name for it rather than in the
            /// player's language - the anomaly definition's name, the curiosity's displayed type, the
            /// resource's name. It is never spoken and never a column label; it exists so a custom
            /// category's saved selector can name a kind and still find it after a language change
            /// (<see cref="ScannerKeys"/>). Null wherever <see cref="Kind"/> is.</summary>
            public string KindKey;

            /// <summary>Whether this row says its KIND in front of its name in the column it is being
            /// read out of. Filled in only for the copies a custom category holds, where the column a
            /// result came from is no longer the column it is being read out of; the built-in
            /// categories answer the same question from the cursor's own position.</summary>
            public bool Prefix;

            /// <summary>Which of the scanner's own categories this copy was taken out of. Filled in
            /// only for the copies a custom category holds, which is the only place a result is read
            /// somewhere other than where it was found.</summary>
            public int From;

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

            /// <summary>What each of the three slots caught, one list per column of the category the
            /// player wrote - or null for a slot that is empty, which is what tells a quick key it
            /// has nothing to answer for. The lists hold COPIES of the built-in results: a copy of a
            /// struct is the same facts, so a row of a custom category and the row it came from
            /// cannot say different things about one planet.</summary>
            public List<Found>[][] Custom;

            public string[][] Labels;

            /// <summary>What each category is CALLED this press - the localized label for a built-in
            /// one, the player's own name for a slot they filled.</summary>
            public string[] Names;

            public ScannerTable Table;
        }

        /// <summary>Whether a category is one of the player's own three slots rather than one of the
        /// scanner's own thirteen. The slots come after them, so no built-in index depends on how
        /// many slots there are.</summary>
        private static bool Custom(int category)
        {
            return category >= BuiltInCount && category < CategoryCount;
        }

        /// <summary>Which category row one of the three slots is - the question every place that
        /// addresses a slot by its own number (the quick keys, the plans) has to ask.</summary>
        private static int Slotted(int slot)
        {
            return BuiltInCount + slot;
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

        /// <summary>
        /// ONE OF THE SIX QUICK KEYS: walk the slot's whole list flat, nearest first from where the
        /// player is reading, and GO to what it lands on.
        ///
        /// It is one gesture, not three: there is no scope to choose and no separate step, so the key
        /// both names the list and moves along it, and every press takes the player somewhere. The
        /// order is taken nearest-first when the sweep begins and then FROZEN, so press after press
        /// walks 1, 2, 3 … n and wraps; the sweep ends when the PLAYER moves, which is why the walk
        /// is re-anchored on where the landing left them rather than on where they were before it.
        /// Those rules are their own engine-free thing (<see cref="ScannerWalk"/>).
        ///
        /// AN EMPTY SLOT SAYS SO AND NAMES THE KEY, never silence: pressed by a player who has not
        /// configured that slot, or who forgot which of the three they filled, a key that does
        /// nothing is indistinguishable from a mod that has stopped working. The key is named off the
        /// LIVE binding, so a player who moved it hears what they actually pressed.
        /// </summary>
        private bool Quick(int slot, int delta, string actionKey)
        {
            double east;
            double north;
            Snap snap = Snapshot(out east, out north);
            int category = Slotted(slot);
            if (snap.Custom[category] == null)
            {
                Voice.Say(
                    ModStrings.Format(ModStrings.GalaxyScannerNoCustom, Pressed(actionKey)),
                    true
                );
                return true;
            }

            Rearmed();
            _cursor.Arm();
            List<Found> nearest = snap.Custom[category][0];
            bool sweeping = _walk.Sweeping(
                slot,
                MapCoordinates.Round(east),
                MapCoordinates.Round(north)
            );
            List<Found> all = Reordered(
                nearest,
                ScannerWalk.Ordering(Keys(nearest), sweeping ? _walk.Sweep : null)
            );
            IList<string> keys = Keys(all);
            string standing =
                _cursor.Category == category && _cursor.Subcategory == ScopeAll
                    ? _cursor.ResultKey
                    : null;
            bool parked =
                nearest.Count > 0
                && standing != null
                && nearest[0].Key == standing
                && Here(nearest[0], east, north);
            int at = ScannerWalk.Land(delta, keys, standing, sweeping, parked);
            _cursor.Point(category, ScopeAll, at < 0 ? 0 : at, snap.Table);
            if (at < 0)
            {
                _cursor.Landed(keys);
                _walk.Anchor(
                    slot,
                    MapCoordinates.Round(east),
                    MapCoordinates.Round(north),
                    keys
                );
                Voice.Say(ModStrings.Format(ModStrings.GalaxyScannerEmpty, ScopeName()), true);
                return true;
            }

            _cursor.Landed(keys);
            MessageBuilder message = new MessageBuilder();
            Instance(message, Spoken(all[at]), Detail(all[at]), all[at], at, all.Count, east, north);
            Voice.Say(message.Build(), true);
            Travel(all[at], at, all.Count, east, north, false);

            // Anchored on where the landing is TAKING the player, not on where they were before it.
            // The landing is the walk moving them; measuring against the place it moved them FROM
            // made every press look like a player move, which restarted the sweep and circled the
            // same handful of nearby entries (reported 2026-08-24). Read from the entry rather than
            // from Reference() a line later, because a landing is in flight for several frames and
            // the reference still answers with the old place. The same rounded pair the player is
            // told, which is what Here() compares too.
            _walk.Anchor(
                slot,
                MapCoordinates.Round(all[at].East),
                MapCoordinates.Round(all[at].North),
                keys
            );
            return true;
        }

        /// <summary>Whether a thing stands on the very pair the player is reading from - which is
        /// what a landing leaves behind, and the half of "the player is parked on this" the walk
        /// cannot answer for itself. Rounded, because the pair the player hears is.</summary>
        private static bool Here(Found found, double east, double north)
        {
            return MapCoordinates.Round(found.East) == MapCoordinates.Round(east)
                && MapCoordinates.Round(found.North) == MapCoordinates.Round(north);
        }

        /// <summary>The chord an action is on, as the player would say it - what the empty-slot
        /// sentence names. An action nothing is bound to (which nobody can have pressed) falls back
        /// to the name the rebinding row gives it.</summary>
        private static string Pressed(string actionKey)
        {
            string chord = ChordNames.Of(ModEntry.Input, actionKey, 0);
            return string.IsNullOrEmpty(chord) ? ModBindings.Title(actionKey) : chord;
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
            _walk.Forget();
            _cursor.Arm();
            return true;
        }

        /// <summary>The list the cursor is currently pointing into.</summary>
        private List<Found> Scoped(Snap snap)
        {
            int at = _cursor.Category;
            if (at < 0 || at >= CategoryCount)
            {
                at = CategorySystems;
            }

            // A CUSTOM CATEGORY'S COLUMNS ARE LISTS, not a filter over one list. Its membership is
            // not a fact about a thing - it is a fact about which of the player's questions caught
            // it, and one thing can be caught by several of them - so the columns are built when the
            // category is and read straight back here.
            if (Custom(at))
            {
                List<Found>[] columns = snap.Custom[at];
                int column = _cursor.Subcategory;
                return columns != null && column >= 0 && column < columns.Length
                    ? columns[column]
                    : new List<Found>();
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

        /// <summary>A scope taken in another order - what a sweep in progress walks, since it keeps
        /// the order it started in rather than re-sorting from wherever the last landing put the
        /// player (<see cref="ScannerWalk.Ordering"/>). The order is a permutation, so the count the
        /// player is told is the same either way.</summary>
        private static List<Found> Reordered(List<Found> scope, int[] order)
        {
            List<Found> taken = new List<Found>(order.Length);
            for (int i = 0; i < order.Length; i++)
            {
                taken.Add(scope[order[i]]);
            }

            return taken;
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
            // The columns a category writes down for itself come first and are memberships; the ones
            // built from what was found come after them and are kinds. Most categories write down one
            // ("all"); the curiosities write down three.
            if (!Kinds(category) || subcategory < ScopeKeys[category].Length)
            {
                return ScannerScopes.Holds(found.Scopes, subcategory);
            }

            return ScannerScopes.HoldsKind(
                found.Kind,
                subcategory,
                subcategory >= 0 && subcategory < labels.Length ? labels[subcategory] : null
            );
        }

        /// <summary>
        /// EVERY COLUMN THE SETTINGS EDITOR CAN OFFER: the thirteen categories with the subcategories
        /// each writes down, and - for the four whose columns are KINDS - every kind the GAME DEFINES,
        /// sorted by the words the player hears.
        ///
        /// The kinds come from the game's own databases rather than from the galaxy being played
        /// (owner ruling 2026-08-24). A category the player is writing has to be able to ask for a
        /// luxury nobody has surveyed yet or an anomaly this map does not happen to hold - a list
        /// built from what has been FOUND could only offer the past. It also means the editor needs no
        /// galaxy at all, which is what puts the Scanner tab on the main menu.
        ///
        /// Still a SNAPSHOT taken when the settings window opens: the databases cannot change while it
        /// is up, and building this per frame would be a localizer lookup per definition sixty times a
        /// second. What the SCANNER does at scan time is unchanged - its columns are still the kinds
        /// it found out there.
        /// </summary>
        internal static ScannerTaxonomy Taxonomy()
        {
            ScannerTaxonomy taxonomy = new ScannerTaxonomy();
            for (int built = 0; built < ScannerKeys.Categories.Length; built++)
            {
                ScannerTaxonomyCategory category = taxonomy.Add(
                    ScannerKeys.Categories[built],
                    ModStrings.Get(CategoryKeys[built])
                );

                string[] keys = ScannerKeys.Subcategories[built];
                string[] labels = ScopeKeys[built];
                for (int i = 0; i < keys.Length && i < labels.Length; i++)
                {
                    category.Add(keys[i], ModStrings.Get(labels[i]));
                }

                if (Kinds(built))
                {
                    category.AddKinds(Defined(built), StringComparer.CurrentCulture);
                }
            }

            return taxonomy;
        }

        /// <summary>
        /// Every kind one of the four derived categories can hold, read off the game's datatables.
        ///
        /// Keyed exactly as the scanner keys what it finds - <c>AnomalyDefinition.Name</c>,
        /// <c>CuriosityDefinition.DisplayedType</c>, <c>ResourceDefinition.Name</c> - so a selector
        /// written here matches a column found out there. Named by the game's own title for that gui
        /// element, which is what <c>GuiAnomaly</c>, <c>GuiCuriosity</c> and <c>GuiResource</c> each
        /// resolve their own Title to.
        ///
        /// Two filters, both the scanner's own rather than this method's: the resource database holds
        /// every resource in the game and the scanner only ever surfaces DEPOSITS of luxuries and
        /// strategics (<c>GuiResource.IsLuxury</c>/<c>IsStrategic</c>, which count the system-wide
        /// kinds in with their own), so the empire-wide resources - dust, science, industry, food -
        /// are not offered; and several curiosity definitions share one displayed type, which is the
        /// column, so the duplicates collapse into it.
        /// </summary>
        private static IList<ScannerKind> Defined(int category)
        {
            List<ScannerKind> kinds = new List<ScannerKind>();
            try
            {
                if (category == CategoryAnomalies)
                {
                    AnomalyDefinition[] anomalies = Values<AnomalyDefinition>();
                    for (int i = 0; anomalies != null && i < anomalies.Length; i++)
                    {
                        kinds.Add(Kind(anomalies[i].Name));
                    }
                }
                else if (category == CategoryCuriosities)
                {
                    CuriosityDefinition[] curiosities = Values<CuriosityDefinition>();
                    for (int i = 0; curiosities != null && i < curiosities.Length; i++)
                    {
                        kinds.Add(Kind(curiosities[i].DisplayedType));
                    }
                }
                else
                {
                    ResourceDefinition[] resources = Values<ResourceDefinition>();
                    for (int i = 0; resources != null && i < resources.Length; i++)
                    {
                        GuiResource wrapper = new GuiResource(resources[i]);
                        bool wanted = category == CategoryStrategic
                            ? wrapper.IsStrategic
                            : wrapper.IsLuxury;
                        if (wanted)
                        {
                            kinds.Add(new ScannerKind(wrapper.Name.ToString(), AgeText.Clean(wrapper.Title)));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: the scanner's taxonomy reading a database threw: " + e);
            }

            return kinds;
        }

        private static ScannerKind Kind(Amplitude.StaticString name)
        {
            return new ScannerKind(name.ToString(), AgeText.Clean(Gui.GetLocalizedTitle(name)));
        }

        /// <summary>
        /// The key-to-words index one of the four derived categories is resolved through, built once
        /// and kept.
        ///
        /// It is a read of the game's DATABASES, which cannot change while the game runs, and it is
        /// what a scan asks per saved selector - so building it per press would be a localizer lookup
        /// per definition on every key. It dies with the assembly a
        /// hot reload replaces, which is the whole of its lifetime.
        /// </summary>
        private static ScannerKindIndex KindIndex(int category)
        {
            if (_kindIndex == null)
            {
                _kindIndex = new ScannerKindIndex[CategoryCount];
            }

            return _kindIndex[category]
                ?? (_kindIndex[category] = new ScannerKindIndex(Defined(category)));
        }

        private static ScannerKindIndex[] _kindIndex;

        /// <summary>One datatable's whole contents, or null where the game has not loaded it - which
        /// is the honest answer on a machine where the datatables failed, and leaves that category
        /// offering the columns it writes down for itself.</summary>
        private static T[] Values<T>() where T : DatatableElement
        {
            IDatabase<T> database = Databases.GetDatabase<T>();
            return database == null ? null : database.GetValues();
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
        private static ScannerTable Table(List<Found>[] world, List<Found>[][] custom, string[][] labels)
        {
            int[][] counts = new int[CategoryCount][];
            for (int at = 0; at < CategoryCount; at++)
            {
                if (Custom(at))
                {
                    List<Found>[] columns = custom[at];
                    int[] slot = new int[columns == null ? 0 : columns.Length];
                    for (int i = 0; i < slot.Length; i++)
                    {
                        slot[i] = columns[i].Count;
                    }

                    counts[at] = slot;
                    continue;
                }

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
            if (found.Kind == null)
            {
                return found.Name;
            }

            // A custom category's row was decided when the copy was made: the column it came OUT of
            // is not the column it is being read out of, so the question "has the kind already been
            // said" cannot be asked of where the cursor now stands.
            bool prefix = Custom(_cursor.Category)
                ? found.Prefix
                : Kinds(_cursor.Category) && _cursor.Subcategory == ScopeAll;
            return prefix
                ? ModStrings.Format(ModStrings.GalaxyScannerOnPlanet, found.Kind, found.Name)
                : found.Name;
        }

        /// <summary>
        /// What else is said about a thing straight after its name - already composed, for every kind
        /// including the settleable worlds.
        ///
        /// A colonizable world's description used to be composed LAZILY for the one row being read,
        /// because it is the longest line the scanner says. It is composed on the way in now (owner
        /// ruling 2026-08-23): a keyword can only look at what a result SAYS, and a description
        /// nobody has composed says nothing - a player searching for "Tundra" would have found no
        /// world at all. The measured cost of composing every one of them is in
        /// <see cref="ScannerCost"/>, and the owner ruled against making it opt-in.
        /// </summary>
        private static string Detail(Found found)
        {
            return found.Extra;
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
        /// offset - "23 south", "1 west, 23 south" (<see cref="CompassDirections.Offsets"/>).
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
                CategoryName(),
                SubcategoryName()
            );
        }

        /// <summary>The category half alone. Read off the snapshot rather than out of the table of
        /// keys, because one of the three at the front is called whatever the player called it.
        /// </summary>
        private string CategoryName()
        {
            int at = _cursor.Category;
            return _names != null && at >= 0 && at < _names.Length && _names[at] != null
                ? _names[at]
                : string.Empty;
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

        /// <summary>What each category was called in the last snapshot.</summary>
        private string[] _names;

        /// <summary>Where each of the six quick keys' walks stands - one walk, because all six
        /// address the same cursor and only one of them can be in flight.</summary>
        private readonly ScannerWalk _walk = new ScannerWalk();

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
        /// rest of their row is built from what was found. Colonizable worlds are the one category
        /// with no "all" column at all - unoccupied and occupied are the whole of it - and the last
        /// three (pins, projectiles, quest markers) are "all"-only.</summary>
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
            // Curiosities are the one KINDS category with fixed columns in front of the kinds (owner
            // ruling 2026-08-23): what an expedition could actually be sent to, and what is only out
            // of reach because the empire's expedition power is too low - the refusal the card draws
            // a padlock for. Both are asked of the game (<c>Curiosity.CanBeSearched</c> and the
            // failure it records), never re-derived here.
            new string[]
            {
                ModStrings.GalaxyScannerCuriositiesAll,
                ModStrings.GalaxyScannerCuriositiesExplorable,
                ModStrings.GalaxyScannerCuriositiesLowPower,
            },
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
                if (Custom(at))
                {
                    // The player's own three name their columns from what they asked for, which is
                    // not known until the categories are planned - see <see cref="Plans"/>.
                    labels[at] = NoColumns;
                    continue;
                }

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
                // The category's OWN columns first, in the order they are written down, then one per
                // kind found. Most of these categories have exactly one written down ("all"); the
                // curiosities have three.
                string[] names = new string[kinds.Count + keys.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    names[i] = ModStrings.Get(keys[i]);
                }

                for (int i = 0; i < kinds.Count; i++)
                {
                    names[keys.Length + i] = kinds[i];
                }

                labels[at] = names;
            }

            return labels;
        }

        /// <summary>A category with no columns at all - an unconfigured slot, which is a row of the
        /// table holding nothing and is therefore skipped by every cycle exactly as a built-in
        /// category with nothing in it is.</summary>
        private static readonly string[] NoColumns = new string[0];

        // ---- the player's own categories ----

        /// <summary>
        /// WHAT THE THREE SLOTS ASK FOR, this press: one plan per configured slot, its columns
        /// resolved against the galaxy as it now stands (<see cref="ScannerCustomPlan"/>). A slot
        /// standing empty plans nothing, which is what tells its quick keys to say so.
        /// </summary>
        private static ScannerCustomPlan[] Plans(List<Found>[] world, string[][] labels)
        {
            ScannerCustomPlan[] plans = new ScannerCustomPlan[SlotCount];
            ScannerCustomSlots slots = ScannerCustomSettings.Slots;
            if (!slots.Any)
            {
                return plans;
            }

            Columns columns = new Columns(world, labels);
            for (int slot = 0; slot < SlotCount; slot++)
            {
                plans[slot] = ScannerCustomPlan.Of(slots.Slot(slot), columns);
            }

            return plans;
        }

        /// <summary>
        /// EVERYTHING ONE SLOT CAUGHT, one list per column of the category the player wrote.
        ///
        /// A selector's column holds whatever its built-in column holds, shared as a struct copy - the
        /// same facts, so a row of the custom category and the row it came from can never say
        /// different things about one planet. A keyword's column holds everything in the whole
        /// scanner whose own words match it. And "all" holds each result ONCE however many of the
        /// player's questions caught it, which is the only column where the same thing could have
        /// arrived twice.
        /// </summary>
        private static List<Found>[] CustomColumns(
            ScannerCustomPlan plan,
            List<Found>[] world,
            string[][] labels,
            double east,
            double north
        )
        {
            IList<ScannerCustomColumn> plans = plan.Columns;
            List<Found>[] columns = new List<Found>[plans.Count];
            List<Found> all = new List<Found>();
            HashSet<string> seen = new HashSet<string>();
            columns[0] = all;
            for (int c = 1; c < plans.Count; c++)
            {
                ScannerCustomColumn column = plans[c];
                List<Found> caught = new List<Found>();
                if (column.Keyword != null)
                {
                    Keyword(caught, column.Keyword, world);
                }
                else
                {
                    Selected(caught, column.Category, column.Subcategory, world, labels);
                }

                Sort(caught, east, north);
                columns[c] = caught;
                for (int i = 0; i < caught.Count; i++)
                {
                    Found found = caught[i];
                    if (!seen.Add(found.Key))
                    {
                        continue;
                    }

                    // In "all" the row says what it would say in the column it came from's own
                    // "all" - the kind and then the world, where the source category's columns are
                    // kinds - because "all" has said nothing about it either.
                    found.Prefix = Kinds(found.From) && found.Kind != null;
                    all.Add(found);
                }
            }

            Sort(all, east, north);
            return columns;
        }

        /// <summary>One built-in column, as a custom category holds it.</summary>
        private static void Selected(
            List<Found> caught,
            int category,
            int subcategory,
            List<Found>[] world,
            string[][] labels
        )
        {
            List<Found> source = world[category];
            bool prefix = Kinds(category) && subcategory == ScopeAll;
            for (int i = 0; i < source.Count; i++)
            {
                if (!Holds(source[i], category, subcategory, labels[category]))
                {
                    continue;
                }

                Found found = source[i];
                found.From = category;
                found.Prefix = prefix && found.Kind != null;
                caught.Add(found);
            }
        }

        /// <summary>Everything the scanner can see whose own words match one keyword - its name, the
        /// kind of thing it is, and the detail already composed for it (owner ruling 2026-08-23).
        /// Every built-in category is asked, and no custom one: a custom category holding another's
        /// results would be the same things twice under two names.</summary>
        private static void Keyword(List<Found> caught, string keyword, List<Found>[] world)
        {
            for (int at = 0; at < BuiltInCount; at++)
            {
                List<Found> source = world[at];
                bool kinds = Kinds(at);
                for (int i = 0; i < source.Count; i++)
                {
                    if (
                        !ScannerCustomPlan.Catches(
                            keyword,
                            source[i].Name,
                            source[i].Kind,
                            source[i].Extra
                        )
                    )
                    {
                        continue;
                    }

                    Found found = source[i];
                    found.From = at;
                    found.Prefix = kinds && found.Kind != null;
                    caught.Add(found);
                }
            }
        }

        /// <summary>What a plan asks the live galaxy about its own columns: which one a saved
        /// selector names, and what it is called.</summary>
        private sealed class Columns : IScannerColumns
        {
            public Columns(List<Found>[] world, string[][] labels)
            {
                _world = world;
                _labels = labels;
            }

            public bool Find(ScannerSelector selector, out int category, out int subcategory)
            {
                category = -1;
                subcategory = -1;
                int built = ScannerKeys.Category(selector.Category);
                if (built < 0)
                {
                    return false;
                }

                category = built;
                subcategory = ScannerKeys.Subcategory(built, selector.Subcategory);
                if (subcategory >= 0)
                {
                    return true;
                }

                // Not one of the columns the category writes down, so it is a KIND - and which column
                // a kind is in is a fact about this galaxy. The definition's own name is resolved to
                // the WORDS the game draws it with (the databases know them whether or not this
                // galaxy holds any), and the column is the one carrying those words - which is what
                // the table is keyed by, and what makes a selector saved under either of two twins
                // find the one column they share.
                if (!Kinds(category))
                {
                    return false;
                }

                int column = KindIndex(category).Column(selector.Subcategory, _labels[category]);
                if (column >= 0)
                {
                    subcategory = column;
                    return true;
                }

                // A key no database of this build defines - another mod's, or a definition the game
                // dropped. Nothing can resolve its words, so the only thing left is what was found.
                List<Found> found = _world[category];
                for (int i = 0; i < found.Count; i++)
                {
                    if (found[i].KindKey != selector.Subcategory)
                    {
                        continue;
                    }

                    string[] row = _labels[category];
                    for (int c = 0; c < row.Length; c++)
                    {
                        if (row[c] == found[i].Kind)
                        {
                            subcategory = c;
                            return true;
                        }
                    }
                }

                return false;
            }

            public string Label(int category, int subcategory)
            {
                // BOTH halves: two selectors that both say "all" are two different columns, and a
                // player who hears "all" twice in one category has no way to tell them apart - nor
                // has the cursor, which remembers a column by its name.
                return ModStrings.Format(
                    ModStrings.GalaxyScannerScope,
                    ModStrings.Get(CategoryKeys[category]),
                    _labels[category][subcategory]
                );
            }

            private readonly List<Found>[] _world;
            private readonly string[][] _labels;
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

            Travel(scope[at], at, scope.Count, east, north, true);
            return true;
        }

        /// <summary>
        /// The landing itself, which is the same wherever the press came from - the go-to key, or one
        /// of the six that walk a category the player made. There is ONE landing per category and
        /// this is it; a quick key inventing a different one would make "go to the next enemy fleet"
        /// mean something else from the key than from the cycle.
        /// </summary>
        /// <param name="announce">Whether the scanner says the line for a fleet the tree has no node
        /// for. False from the quick keys, which have already said their landing.</param>
        private void Travel(
            Found found,
            int at,
            int count,
            double east,
            double north,
            bool announce
        )
        {
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

                return;
            }

            // Everything else goes through the PAGE's one landing, which owns the whole decision -
            // whether the free cursor stays up, where the tree cursor goes and what the camera does
            // (<see cref="GalaxyHudScreen.GoTo"/>, <see cref="MapLandings"/>). Before 2026-08-22 this
            // method answered those questions itself and got the planet case wrong: it jumped the CELL
            // onto a world, which the cell cannot read.
            if (_screen.GoTo(Target(found), MapCamera.Auto))
            {
                return;
            }

            // A fleet the tree has NO node for. The tree hangs a fleet under the system it is parked
            // at, under both ends of the starlane it is flying, under the DESTINATION of the
            // open-space crossing it is making, or - where that destination is a place the map has
            // never named - at the top level of the systems stop. So a free mover always has a row
            // now, and what is left here is a fleet parked at a system the map does not name and a
            // fleet flying a lane the map does not draw (ES2 facts): the branch that would hold it
            // does not exist. The landing has already selected it and moved the camera - the map's own
            // "go to that fleet" - and there is no node to announce the arrival, so the line the
            // scanner found it with is said again, which is the whole of what arriving there means.
            if (!announce)
            {
                return;
            }

            MessageBuilder arrival = new MessageBuilder();
            Instance(arrival, Spoken(found), Detail(found), found, at, count, east, north);
            Voice.Say(arrival.Build(), true);
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

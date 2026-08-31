using System;
using ES2Access.Core.Bookmarks;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Bookmarks;
using ES2Access.UI.Input;

namespace ES2Access.Screens
{
    /// <summary>
    /// MAP BOOKMARKS: ten places the player names by a digit and comes back to from anywhere on the
    /// galaxy page.
    ///
    /// The tree answers "what is here and what is next to it", the scanner "what is near me of this
    /// kind" and the inspect cursor "what is over there". None of them answers the one thing a
    /// sighted player does with a mouse and a memory of the picture: go straight back to the place
    /// they were working in ten minutes ago. That is this - and unlike the other three it is a thing
    /// the player MAKES, so the set half is as much of the feature as the jump.
    ///
    /// SETTING is a key of the MAP WIDGET (<see cref="GalaxyHudScreen.CursorOnMap"/>): a bookmark is
    /// made out of where the tree cursor is standing, and standing on the zoom slider or the turn
    /// controls it is standing nowhere on the map. Off the map the key is silently nothing - not a
    /// refusal, because the chord is pressed for a place and there is no place here to refuse.
    /// JUMPING is a key of the whole PAGE, like the go-to-a-panel chords: coming back to a place is
    /// exactly what a player reading the notifications or the turn log wants, and making them Tab to
    /// the map first would be asking them to be where they are trying to go.
    ///
    /// Both are claimed from the game only while the galaxy page is up AND the game's own scan mode
    /// is off (<see cref="KeysClaimed"/>). Under the scan lens the digits are left entirely alone.
    ///
    /// WHAT A SLOT HOLDS is decided when it is SET and resolved again when it is used
    /// (<see cref="MapBookmark"/>): a system by its GUID, so its bookmark follows it through
    /// everything that can happen to a system short of the map forgetting it exists, and anything
    /// else as the bare point of galaxy it stands at. The store and the file are
    /// <see cref="MapBookmarkStore"/>'s; what the tree DRAWS for a bookmark is
    /// <see cref="GalaxyHudScreen"/>'s.
    /// </summary>
    internal sealed class GalaxyBookmarks
    {
        private readonly GalaxyHudScreen _screen;

        public GalaxyBookmarks(GalaxyHudScreen screen)
        {
            _screen = screen;
        }

        /// <summary>What the input layer's conditional claim asks: the digit chords and Ctrl+C are the
        /// mod's while the galaxy page is the one being read and the game is not wearing its scan
        /// lens. Under the lens they stay the game's, whatever it does with them - the same
        /// deliberate hand-back the zoom band's silence under that mode is
        /// (<c>GalaxyViewLevels.Scanning</c>).</summary>
        public static bool KeysClaimed()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            return navigator != null
                && navigator.DeclaresStop(GalaxyHudScreen.SystemStop)
                && !GalaxyViewLevels.Scanning;
        }

        /// <summary>
        /// One key, offered by the page after the inspect cursor and the scanner have passed on it.
        /// True when a bookmark chord took it.
        ///
        /// The scan-mode gate is asked here as well as in the claim, because an UNCLAIMED key still
        /// reaches <c>Screen.AnyKey</c> - the same reason the scanner asks its own gate twice.
        /// </summary>
        public bool HandleKey(string actionKey)
        {
            try
            {
                if (actionKey == null || GalaxyViewLevels.Scanning)
                {
                    return false;
                }

                if (actionKey == MapActions.BookmarkHome)
                {
                    return Home();
                }

                int slot = IndexOf(MapActions.BookmarkSet, actionKey);
                if (slot >= 0)
                {
                    return Set(MapBookmarks.Digits[slot]);
                }

                slot = IndexOf(MapActions.BookmarkGoTo, actionKey);
                return slot >= 0 && Jump(MapBookmarks.Digits[slot]);
            }
            catch (Exception e)
            {
                Log.Warn("bookmarks: answering a bookmark key threw: " + e);
                return true;
            }
        }

        private static int IndexOf(string[] actions, string actionKey)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] == actionKey)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Remember where the cursor is standing, in the slot the digit names.
        ///
        /// Any row the tree files UNDER a system - a planet, a lane, a fleet in its berth, one of its
        /// dossiers - bookmarks the SYSTEM, because that is the place the player is reading; the row
        /// they happened to be on inside it is where they were looking, not where they were. Anything
        /// standing out on the map with a row of its own - a probe, a fleet crossing open space, a
        /// missile - bookmarks the POINT it stands at today, a photograph and not a leash: a bookmark
        /// that followed a fleet would take the player somewhere they never chose.
        ///
        /// A constellation, the unexplored group, and anything else that is a heading rather than a
        /// place, are silently nothing: there is no point of galaxy under them to keep.
        /// </summary>
        private bool Set(char digit)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GraphNode focused = navigator == null ? null : navigator.CurrentNode;
            if (focused == null || !GalaxyHudScreen.IsMapStop(focused.StopKey))
            {
                return true;
            }

            // While the inspect cursor is driving, the place the player is standing on is the CELL and
            // not the tree row underneath it.
            if (GalaxyInspect.Live)
            {
                return SetFromCell(digit);
            }

            StarSystemNode system = SystemOf(focused);
            if (system != null)
            {
                GalaxyPosition at = system.GalaxyPosition;
                char replaced = MapBookmarkStore.Set(
                    digit,
                    MapBookmark.OfSystem(system.GUID, at.X, at.Y)
                );
                Say(digit, system.LocalizedName, replaced);
                return true;
            }

            GalaxyPosition point;
            if (PointOf(focused, out point))
            {
                char replaced = MapBookmarkStore.Set(digit, MapBookmark.AtPoint(point.X, point.Y));
                Say(digit, GalaxyCoordinates.Text(point), replaced);
                return true;
            }

            return true;
        }

        /// <summary>
        /// The same bookmark, made out of the CELL - the square the player is reading instead of the
        /// tree row underneath it.
        ///
        /// A square holding exactly one place bookmarks that place, GUID and all, so a bookmark set
        /// from the cell and one set from the tree are the same bookmark; a square holding none keeps
        /// its own point of galaxy. Which of the two happened is audible: the line says the system's
        /// name or the pair, the same two answers the tree's own set has.
        ///
        /// A square holding TWO OR MORE places REFUSES, out loud (owner ruling 2026-08-31, wording
        /// his): nothing is stored, and the player is told the one thing that gets them what they
        /// asked for - "Shrink cursor so it contains only one system". Silently keeping the square's
        /// point instead would be the worst of the three: the player asked for a star, and would be
        /// given a piece of empty sky that says nothing about which star they meant.
        ///
        /// PARKED, this is never reached - the cursor is on another stop, and a set off the map stop
        /// is already silently nothing (owner ruling 2026-08-31: focus is not on the map, so there is
        /// no place there to keep).
        /// </summary>
        private bool SetFromCell(char digit)
        {
            GalaxyPosition at;
            StarSystemNode place;
            switch (_screen.Inspect.CellPlace(out at, out place))
            {
                case CellSubject.Place:
                    GalaxyPosition star = place.GalaxyPosition;
                    char onStar = MapBookmarkStore.Set(
                        digit,
                        MapBookmark.OfSystem(place.GUID, star.X, star.Y)
                    );
                    Say(digit, place.LocalizedName, onStar);
                    return true;

                case CellSubject.Point:
                    char onPoint = MapBookmarkStore.Set(digit, MapBookmark.AtPoint(at.X, at.Y));
                    Say(digit, GalaxyCoordinates.Text(at), onPoint);
                    return true;

                case CellSubject.Crowded:
                    Voice.Say(ModStrings.Get(ModStrings.GalaxyBookmarkShrink), true);
                    return true;

                default:
                    return true;
            }
        }

        /// <summary>
        /// What a set says: which slot, and the place it now holds - the system's name, or the pair
        /// for a bare point of space.
        ///
        /// <paramref name="replaced"/> is the slot this set TOOK the place from, where one place was
        /// already bookmarked twice over (one place, one slot -
        /// <see cref="MapBookmarks.SetAlone"/>). It gets a whole sentence of its own rather than a
        /// clause glued onto the plain one, so that a language which puts the news first can: a slot
        /// the player set has just been emptied, and hearing about it now is the difference between a
        /// bookmark that moved and one that went missing.
        /// </summary>
        private static void Say(char digit, string where, char replaced)
        {
            Voice.Say(
                replaced == '\0'
                    ? ModStrings.Format(ModStrings.GalaxyBookmarkSet, digit.ToString(), where)
                    : ModStrings.Format(
                        ModStrings.GalaxyBookmarkSetReplacing,
                        digit.ToString(),
                        where,
                        replaced.ToString()
                    ),
                true
            );
        }

        /// <summary>The system a row on the map stop belongs to, or null for a row that belongs to no
        /// system at all. Read off the row's own KEY, which is a path whose <c>/system/&lt;guid&gt;</c>
        /// segment is what the whole branch under a system is keyed beneath
        /// (<c>GalaxyHudScreen.SystemKey</c>) - the same prefix test the page's own "is this reading
        /// that system" makes, asked of the key because the row itself may be a planet, a lane or a
        /// dossier and carry no system anywhere on it.</summary>
        private static StarSystemNode SystemOf(GraphNode node)
        {
            ControlId id = node == null ? null : node.Id;
            StarSystemNode subject = id == null ? null : id.Subject as StarSystemNode;
            if (subject != null)
            {
                return subject;
            }

            string key = id == null ? null : id.StructuralKey as string;
            int at = key == null ? -1 : key.IndexOf(SystemSegment, StringComparison.Ordinal);
            if (at < 0)
            {
                return null;
            }

            string tail = key.Substring(at + SystemSegment.Length);
            int end = tail.IndexOf('/');
            string guid = end < 0 ? tail : tail.Substring(0, end);
            ulong number;
            return ulong.TryParse(guid, out number) ? GalaxyHudScreen.SystemByGuid(number) : null;
        }

        private const string SystemSegment = "/system/";

        /// <summary>
        /// Where a row that belongs to no system stands - the page's own index of the things it draws
        /// OUT ON THE MAP: a probe, a missile in flight, an ally's pin, a quest pin planted in the
        /// open, and a fleet away from any berth (<c>GalaxyHudScreen.PositionOf</c>).
        ///
        /// That index and nothing else. A row's own subject cannot stand in for it: a CONSTELLATION
        /// is an entity with a position too - the centroid the map writes its name at - so asking the
        /// subject bookmarked a point in the middle of a stretch of sky for a row that is a heading
        /// and not a place (measured 2026-08-31, which is what this list replaced).
        /// </summary>
        private bool PointOf(GraphNode node, out GalaxyPosition at)
        {
            at = default(GalaxyPosition);
            ControlId id = node == null ? null : node.Id;
            return id != null && _screen.PositionOf(id, out at);
        }

        /// <summary>
        /// Go to what a slot holds, from anywhere on the page.
        ///
        /// An empty slot is a spoken refusal and nothing else: the chord names a place the player
        /// believes they made, and silence there is indistinguishable from the key not working.
        /// </summary>
        private bool Jump(char digit)
        {
            MapBookmark bookmark;
            if (!MapBookmarkStore.Bookmarks.TryGet(digit, out bookmark))
            {
                Voice.Say(
                    ModStrings.Format(ModStrings.GalaxyBookmarkEmpty, digit.ToString()),
                    true
                );
                return true;
            }

            StarSystemNode system = _screen.BookmarkedSystem(digit);
            ControlId row = system == null ? _screen.BookmarkedPoint(digit) : null;
            GalaxyPosition at =
                system != null
                    ? system.GalaxyPosition
                    : new GalaxyPosition(bookmark.X, bookmark.Y);
            return Go(system, row, at);
        }

        /// <summary>
        /// Go to the empire's home system - the same landing, at a place the player never had to set.
        /// It is not a bookmark and consumes no slot: the game knows where home is
        /// (<c>DepartmentOfTheInterior.HomeSystemNode</c>) and nothing about it is ever written down.
        ///
        /// An empire with no home system at all - which no game the player can be in has - is taken
        /// and silent rather than refused: there is no wording for a place the game itself does not
        /// have.
        /// </summary>
        private bool Home()
        {
            StarSystemNode home = GalaxyHudScreen.HomeSystem();
            if (home == null)
            {
                return true;
            }

            return Go(home, null, home.GalaxyPosition);
        }

        /// <summary>
        /// The one landing every bookmark chord makes.
        ///
        /// It is a FOCUS landing and never a click: an armed targeting cursor is waiting for the
        /// player to press Enter somewhere, and a jump that confirmed it would send a fleet to the
        /// bookmark instead of taking the player there (the measured travel-versus-click split -
        /// <c>docs/interaction.md</c>). So jump-then-Enter is how a bookmark aims an order.
        ///
        /// With the inspect cursor LIVE this is not a landing of its own at all: it is the page's one
        /// landing (<c>GalaxyHudScreen.GoTo</c>), which moves the CELL and nothing else - not the tree
        /// cursor, not the zoom (<see cref="MapLandings.Decide"/>, owner rulings 2026-08-31). The cell
        /// reading is the one utterance, and leaving the mode later puts the player back on the row
        /// they armed it from, because nothing ever moved it. A bookmark arrives exactly as the
        /// scanner's go-to does, because it IS the scanner's go-to.
        ///
        /// PARKED is the shape that landing cannot make, and it is a difference of SPEECH: the player
        /// is on another stop, so somebody has to bring them back to the map, and a stop landing
        /// announces itself. The map stop is therefore focused SILENTLY and the mode's own resume
        /// reads the new cell - one utterance, and it names the place jumped to rather than whichever
        /// row the map stop was left on. Coming back to that stop lands on the row the mode was armed
        /// from, which is where Escape would have put them anyway, so the two agree.
        /// </summary>
        private bool Go(StarSystemNode system, ControlId row, GalaxyPosition at)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null)
            {
                return true;
            }

            // Before anything moves: a jump is a leap across the galaxy and the way back is remembered
            // for Backspace, on whichever trail the player is reading the map by
            // (<c>GalaxyHudScreen.NoteLeap</c>, which also decides that a jump made from another panel
            // remembers nothing at all).
            _screen.NoteLeap();

            if (GalaxyInspect.Active)
            {
                ControlId aim = system != null ? GalaxyHudScreen.SystemRow(system) : row;
                MapTarget target = system != null
                    ? MapTarget.Place(system, aim, at)
                    : MapTarget.Point(aim, at);
                _screen.GoTo(target, MapCamera.Auto);
                return true;
            }

            if (GalaxyInspect.Live)
            {
                double east;
                double north;
                GalaxyCoordinates.Offsets(at, out east, out north);
                navigator.FocusStop(GalaxyHudScreen.SystemStop, false);
                _screen.Inspect.MoveTo(MapCoordinates.Round(east), MapCoordinates.Round(north));
                return true;
            }

            if (system != null)
            {
                _screen.LandInside(system);
                return true;
            }

            if (row != null)
            {
                navigator.FocusNode(row);
            }

            return true;
        }
    }
}

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
                MapBookmarkStore.Set(digit, MapBookmark.OfSystem(system.GUID, at.X, at.Y));
                Say(digit, system.LocalizedName);
                return true;
            }

            GalaxyPosition point;
            if (PointOf(focused, out point))
            {
                MapBookmarkStore.Set(digit, MapBookmark.AtPoint(point.X, point.Y));
                Say(digit, GalaxyCoordinates.Text(point));
                return true;
            }

            return true;
        }

        /// <summary>
        /// The same bookmark, made out of the CELL - the square the player is reading instead of the
        /// tree row underneath it.
        ///
        /// A square holding exactly one place bookmarks that place, GUID and all, so a bookmark set
        /// from the cell and one set from the tree are the same bookmark; anything else keeps the
        /// square's own point of galaxy. Which is which is audible: the line says the system's name or
        /// the pair, the same two answers the tree's own set has.
        ///
        /// PARKED, this is never reached - the cursor is on another stop, and a set off the map stop
        /// is already silently nothing (owner ruling 2026-08-31: focus is not on the map, so there is
        /// no place there to keep).
        /// </summary>
        private bool SetFromCell(char digit)
        {
            GalaxyPosition at;
            StarSystemNode place;
            if (!_screen.Inspect.CellPlace(out at, out place))
            {
                return true;
            }

            if (place != null)
            {
                GalaxyPosition star = place.GalaxyPosition;
                MapBookmarkStore.Set(digit, MapBookmark.OfSystem(place.GUID, star.X, star.Y));
                Say(digit, place.LocalizedName);
                return true;
            }

            MapBookmarkStore.Set(digit, MapBookmark.AtPoint(at.X, at.Y));
            Say(digit, GalaxyCoordinates.Text(at));
            return true;
        }

        private static void Say(char digit, string where)
        {
            Voice.Say(
                ModStrings.Format(ModStrings.GalaxyBookmarkSet, digit.ToString(), where),
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
        /// With the inspect cursor up the cell is what the player is reading, so the cell is what
        /// moves - onto the ROUNDED pair the bookmark's place is spoken as, which is the pair that
        /// puts it inside even the one-unit cursor. The mode is never exited and the zoom is never
        /// touched. Parked off the map, the cell is moved silently and the player is put back on the
        /// map, where the mode's own resume reads the new cell out - one reading of the arrival
        /// instead of two.
        /// </summary>
        private bool Go(StarSystemNode system, ControlId row, GalaxyPosition at)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator == null)
            {
                return true;
            }

            if (GalaxyInspect.Live)
            {
                double east;
                double north;
                GalaxyCoordinates.Offsets(at, out east, out north);
                int x = MapCoordinates.Round(east);
                int y = MapCoordinates.Round(north);
                // The row the tree cursor is put on UNDER the cell: a system is seated on its own row
                // and not inside it, because going inside is what brings the camera in and the mode is
                // deliberately leaving the picture alone.
                ControlId seat = system != null ? GalaxyHudScreen.SystemRow(system) : row;
                if (GalaxyInspect.Active)
                {
                    _screen.Inspect.JumpTo(x, y);
                    Seat(navigator, seat, at, false);
                }
                else
                {
                    // The landing first and the cell second. Landing back on the map is an ordinary
                    // landing and the page's camera rule follows it, so it is aimed at the BOOKMARK's
                    // own row: aimed at whatever the map stop happened to remember, the camera went to
                    // that row's place on the way - a zoom into a system the player had not asked for
                    // (measured, stage B). The cell's own slide is still the last word, and it is
                    // SILENT: the landing has just named the place, and the mode's own resume reading
                    // the cell out said it a second time (owner ruling 2026-08-31 - a jump announces
                    // exactly once).
                    bool landed = Seat(navigator, seat, at, true);
                    _screen.Inspect.MoveTo(x, y, landed);
                }

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

        /// <summary>
        /// Put the tree cursor on the bookmark's own row underneath the cell, and tell the mode that
        /// is where it now stands.
        ///
        /// Without the second half, Escape out of the mode undid the jump: leaving puts the player
        /// back on the control the mode was ARMED from, camera and all
        /// (<see cref="GalaxyInspect.Reseat"/>), so a player who swept to a bookmark and pressed
        /// Escape was returned to wherever they had been ten minutes earlier. It is the same pairing
        /// the page's own go-to makes while the cell is up (<c>GalaxyHudScreen.GoTo</c>).
        ///
        /// <paramref name="announce"/> is the parked case, where the cursor really is being sent
        /// somewhere the player is not: that landing is the one they hear. LIVE, the cell is what they
        /// are reading and the tree move is silent, felt only when the mode ends.
        ///
        /// Answers whether the cursor was really aimed at the BOOKMARK's own row, which is what tells
        /// the parked jump whether its landing has already named the place
        /// (<see cref="GalaxyInspect.MoveTo"/>).
        /// </summary>
        private bool Seat(GraphNavigator navigator, ControlId seat, GalaxyPosition at, bool announce)
        {
            if (seat == null)
            {
                // A bookmark this build lists no row for at all. The cell has still gone there, so
                // only the seat is missing; parked, the map stop's own landing is the way back - and
                // it names wherever that stop was left, not the bookmark, so the cell keeps its voice.
                if (announce)
                {
                    navigator.FocusStop(GalaxyHudScreen.SystemStop);
                }

                return false;
            }

            navigator.FocusNode(seat, announce);
            _screen.Inspect.Reseat(seat, at);
            return true;
        }
    }
}

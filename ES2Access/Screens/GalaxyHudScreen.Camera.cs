using System;
using System.Collections.Generic;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.ES2.UI;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// THE CAMERA FOLLOWS THE CURSOR: what the map is looking at is whatever the keyboard is on.
    ///
    /// The state here - which place the camera was last taken to, whether it was taken INSIDE, and
    /// the stamp of the last move somebody else made - is read by the two concerns that also move
    /// the camera and have to know whether this one has already moved it:
    /// <see cref="Land"/> and the rest of the landing file (GalaxyHudScreen.Landing.cs) call
    /// <see cref="Remember"/> and <see cref="ForgetPick"/> after a landing so the follower does not
    /// undo it, and travelling a lane (GalaxyHudScreen.Trail.cs) reads and writes
    /// <see cref="_jumpedFrom"/> so backing out gives the zoom back. The shared fields are
    /// <c>_cameraPlace</c>, <c>_cameraIn</c>, <c>_cameraStamp</c>, <c>_cameraSystem</c>,
    /// <c>_jumpedFrom</c> and <c>_scanRow</c>.
    /// </summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>
        /// Keep the game's own pointer feedback pointed at whatever the map is drawing for the focused
        /// system NOW.
        ///
        /// Which tooltip a focused system asks the game to draw depends on where the camera is - the
        /// label's while the label is what the map draws, the orbital window's once the camera is in on
        /// the system (<see cref="AddSystem"/>) - and the camera moves without focus moving at all:
        /// Enter zooms, and so does opening a system up. The focus visual is committed once per focus
        /// CHANGE, so nothing would ever ask again: the tooltip stayed aimed at a label the map had
        /// meanwhile pushed off the top of the screen, and the game went on drawing the system's dossier
        /// clamped into a corner beside nothing.
        ///
        /// So the camera's own answer is watched and the visual dropped when it changes. The navigator
        /// re-commits it later in the same frame, which is why nothing flickers and no tooltip is closed
        /// and reopened (<c>ScreenManager.Tick</c> runs OnUpdate before EnsureFocus, and
        /// <c>PointerFocus.Tick</c> after both).
        /// </summary>
        private void FollowCamera()
        {
            StarSystemNode focused = GalaxyViewLevels.FocusedSystem;
            bool orbital = OrbitalWindow() != null;
            // ...and WHICH system the orbital window's star tooltip is describing. There is one of that
            // tooltip and the window re-points it at whatever the camera is looking at, which it does a
            // few frames after the camera's own answer changes - so the pair above can be settled while
            // the widget the pointer was aimed at still holds the system the player came FROM, and it
            // stays that way for as long as the cursor stands still.
            object star = OrbitalStarSubject();
            if (
                ReferenceEquals(focused, _cameraSystem)
                && orbital == _cameraOrbital
                && ReferenceEquals(star, _cameraStar)
            )
            {
                return;
            }

            _cameraSystem = focused;
            _cameraOrbital = orbital;
            _cameraStar = star;
            GraphNavigator navigator = ModEntry.Navigator;
            if (navigator != null)
            {
                navigator.ClearVisual();
            }
        }

        /// <summary>What the camera was showing last frame - the three things the choice of a focused
        /// system's tooltip is made from.</summary>
        private StarSystemNode _cameraSystem;

        private bool _cameraOrbital;

        private object _cameraStar;

        /// <summary>
        /// Make the map draw the planets of the system the camera has come in on.
        ///
        /// The game binds its orbital labels window ONCE, as the window is shown
        /// (<c>PlanetLabelsWindow.OnBeginShow</c>), and shows it exactly when a system becomes focused
        /// after none was (<c>GuiManager</c>'s visibility pass reads
        /// <c>FocusedStarSystemNode != null</c>). With a mouse that is always how a system is reached:
        /// the camera FLIES, and part-way between two stars nothing is near enough to be focused
        /// (<c>GalaxyViewCameraController.GetGalaxyEntityToFocus</c>), so the window is hidden and
        /// shown again around every crossing and binds itself to wherever the camera ended up.
        ///
        /// The keyboard crosses in ONE frame - a landing snaps rather than flies (owner ruling
        /// 2026-08-22) - so the focus steps straight from one system to the next, the window is never
        /// hidden, and it goes on drawing the system the player came FROM. Measured 2026-08-24 on
        /// walking up into an expanded system's last child and on a type-ahead landing into another
        /// system's interior: the camera and <c>FocusedStarSystemNode</c> were both on the new system
        /// while the window still held the old one's cards, so the new system's planets had no cards at
        /// all and a world's tooltip fell back to the label circle the map parks at the top of the
        /// screen.
        ///
        /// So the invariant is asserted here every frame instead of being left to how the crossing was
        /// made: a window up over the wrong system is hidden and shown again - the game's own rebind,
        /// instant, so the cards are there on the same frame - and the system it was rebound for is
        /// remembered, so a bind that will not take cannot become a show every frame. Only ever a
        /// window the game already wants up: where it is not shown there is nothing to correct, and
        /// showing one the game means to keep hidden would draw a system over a screen.
        /// </summary>
        private void ShowFocusedSystem()
        {
            try
            {
                StarSystemNode focused = GalaxyViewLevels.FocusedSystem;
                PlanetLabelsWindow_SystemOrbital window = OrbitalWindow();
                if (focused == null || window == null || !window.Shown)
                {
                    _reboundFor = null;
                    return;
                }

                if (
                    ReferenceEquals(OrbitalSystem(), focused)
                    || ReferenceEquals(focused, _reboundFor)
                )
                {
                    return;
                }

                _reboundFor = focused;
                Gui.GuiService.HideWindow(window, true);
                Gui.GuiService.ShowWindow(window, true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: rebinding the orbital labels window threw: " + e);
            }
        }

        /// <summary>The system the last rebind was made for, so that a window which will not take the
        /// binding is left alone rather than hidden and shown on every frame.</summary>
        private StarSystemNode _reboundFor;

        /// <summary>Which system the orbital window is drawing - read off the star dossier it binds in
        /// the same call that binds its planets (<c>PlanetLabelsWindow_SystemOrbital.OnBeginShow</c>),
        /// which is the same answer without reaching for the window's own protected field.</summary>
        private static StarSystemNode OrbitalSystem()
        {
            GuiStarSystem star = OrbitalStarSubject() as GuiStarSystem;
            return star == null ? null : star.StarSystemNode;
        }

        /// <summary>What the orbital window's own star tooltip is currently about - the wrapper it is
        /// bound to, since the widget itself never changes and so says nothing about which system it
        /// is describing.</summary>
        private static object OrbitalStarSubject()
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = OrbitalWindow();
                AgeTooltip star = window == null ? null : window.StarTooltip;
                return star == null ? null : star.Target;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the focus visual being committed right now is the cursor having been
        /// PLACED - moved by the player or seated by this page - as opposed to this page being
        /// re-entered or the visual being re-taken where it already was. Everything on this page that
        /// moves the CAMERA for what is being read asks it first
        /// (<see cref="GraphNavigator.CursorMovedHere"/>).</summary>
        private static bool CursorMoved()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            return navigator != null && navigator.CursorMovedHere;
        }

        // ---- the camera follows the cursor ----

        /// <summary>
        /// THE camera rule of this page (owner ruling 2026-08-23): the camera goes where the cursor is
        /// reading, and it is one rule rather than one per kind of node.
        ///
        /// What the cursor is standing on resolves to a PLACE and to how close the player is to it -
        /// the system a row belongs to, or a thing drifting between the stars, and whether the cursor
        /// is ON that row or INSIDE it (a world, a card, a lane, a fleet, a quest marker). The camera
        /// is then asked for exactly that (<see cref="FollowPlace"/>), which does nothing at all while
        /// it is already showing it. So walking a system's children moves nothing, crossing into
        /// another system's children brings the camera in on that one, and a zoom the player made by
        /// hand is left where they put it for as long as they go on reading the same place.
        ///
        /// Only where the cursor was PLACED (<see cref="GraphNavigator.CursorMovedHere"/>): the player
        /// moved it, or a screen seated it - a handover out of the fleet panel, a landing, the answer
        /// to a reveal - all of which leave the player reading whatever they landed on and so all ask
        /// the same question. Coming back to this page re-seats the cursor where it was left, and the
        /// visual is re-taken whenever the camera changes what it draws for the focused system
        /// (<see cref="FollowCamera"/>) - flying the camera for either would take it off whatever the
        /// GAME has since centred it on, so neither is a placement.
        ///
        /// Only for the map's own stop. The HUD's stops, the view title and the zoom slider are
        /// controls of the page rather than places on it, and a page whose camera moved when the
        /// cursor reached the end-turn button would be reading somebody else's business.
        /// </summary>
        public override void OnFocusVisual(GraphNode node)
        {
            if (!CursorMoved() || node == null)
            {
                return;
            }

            if (Scanning && ScanLensPanels.SystemInfoStop.Equals(node.StopKey))
            {
                CentreOnScanSystem();
                return;
            }

            if (!SystemStop.Equals(node.StopKey))
            {
                return;
            }

            NoteScanSystem(node);
            object place;
            bool inside;
            if (Place(node, out place, out inside))
            {
                FollowPlace(place, inside);
            }
        }

        /// <summary>The last star the tree cursor stood on inside the map stop - kept so that stepping
        /// into the System lens's own stop can ask for THAT system rather than whichever one the camera
        /// happens to be nearest (<see cref="CentreOnScanSystem"/>).</summary>
        private StarSystemNode _scanRow;

        private void NoteScanSystem(GraphNode node)
        {
            for (GraphNode walk = node; walk != null; walk = walk.Parent)
            {
                StarSystemNode star = walk.Id == null ? null : walk.Id.Subject as StarSystemNode;
                if (star != null)
                {
                    _scanRow = star;
                    return;
                }
            }
        }

        /// <summary>
        /// TAB INTO THE SYSTEM LENS'S PANEL ASKS ABOUT THE STAR THE CURSOR WAS ON (owner ruling
        /// 2026-09-01; a judgement call the owner allowed to move the camera).
        ///
        /// The panel is bound to whichever system is nearest the middle of the screen and to nothing
        /// else - the game gives no way to point it at a system - so a player who has walked the map
        /// stop to a star and then tabs across for its figures would be read a different star's. Tabbing
        /// in is a deliberate gesture, so it slides the camera onto the row the cursor was on and the
        /// game's own choice then agrees with the player's. It is a SLIDE and never a zoom: the rung is
        /// the lens, and changing it here would change what the whole screen means.
        ///
        /// Only where the two disagree, so tabbing back and forth over the same star moves nothing.
        /// </summary>
        private void CentreOnScanSystem()
        {
            try
            {
                StarSystemNode star = _scanRow;
                if (star == null)
                {
                    return;
                }

                GalaxyPosition at = star.GalaxyPosition;
                Vector3 focus;
                if (GalaxyViewLevels.CameraTarget(out focus))
                {
                    double dx = focus.x - at.X;
                    double dy = focus.z - at.Y;
                    if (dx * dx + dy * dy < CentredEnough)
                    {
                        return;
                    }
                }

                // The game's own "show me this" pan, which slides and leaves the rung alone - and is
                // already marked as the mod's own camera move, so it does not come back round as a
                // fresh locate request.
                GalaxyViewLevels.PanTo(star);
            }
            catch (Exception e)
            {
                Log.Warn("scan: centring the lens on the focused system threw: " + e);
            }
        }

        /// <summary>How near the camera has to be to a star before it counts as already looking at it -
        /// squared galaxy units, well under the map's closest neighbour spacing.</summary>
        private const double CentredEnough = 0.25;

        /// <summary>
        /// Where a node on the map stop stands, and whether the cursor is inside that place or on its
        /// own row.
        ///
        /// Anything the map draws OUT ON THE MAP rather than at a star comes first, wherever the tree
        /// happens to file its row (owner ruling 2026-08-26) - a probe under way, a missile in flight,
        /// an ally's pin, and a fleet away from any berth. Every one of those is its own place, with no
        /// inside: there is nothing at a bare point to come in on (the same distinction
        /// <see cref="MapLandings.Decide"/> makes).
        ///
        /// The fleet is the case that needed saying, because the tree files it under a STAR: a fleet
        /// crossing a lane and one crossing open space alike get a row under the system they are bound
        /// for - a filing that says where to LOOK for the row, not where
        /// the thing is. Resolving such a row to its system ancestor sent the camera into a star's
        /// orbital view to show a fleet that is not drawn in that picture at all. A fleet PARKED at a
        /// star really is at that star, and keeps the system-ancestor resolution and its zoom in on
        /// the berth.
        ///
        /// Otherwise the system ANCESTOR wins over anything nearer: the star's own dossier cards and a
        /// lane leaving it are things the map draws AT that system, and the place the player is reading
        /// is the system.
        /// </summary>
        private bool Place(GraphNode node, out object place, out bool inside)
        {
            place = null;
            inside = false;
            IGameEntityWithGalaxyPosition drawn = OpenSpaceThing(node == null ? null : node.Id);
            if (drawn != null)
            {
                place = drawn;
                return true;
            }

            // A place the PLAYER put out on the map, which the map draws nothing at. It is a point
            // like the four above and belongs with them, ahead of the ancestor walk: its row hangs in
            // a constellation for reading order and it is not a thing that constellation contains.
            BookmarkPoint bookmark = BookmarkAt(node == null ? null : node.Id);
            if (bookmark != null)
            {
                place = bookmark;
                return true;
            }

            for (GraphNode walk = node; walk != null; walk = walk.Parent)
            {
                StarSystemNode system = walk.Id == null ? null : walk.Id.Subject as StarSystemNode;
                if (system != null)
                {
                    place = system;
                    inside = !ReferenceEquals(walk, node);
                    return true;
                }
            }

            for (GraphNode walk = node; walk != null; walk = walk.Parent)
            {
                IGameEntityWithGalaxyPosition thing =
                    walk.Id == null ? null : walk.Id.Subject as IGameEntityWithGalaxyPosition;
                if (thing != null)
                {
                    place = thing;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The thing a row on this stop stands for, where the map draws that thing out on the map
        /// rather than at a star: a probe under way, an obliterator missile in flight, an ally's pin,
        /// and a fleet away from any berth - the four kinds that travel, and so the four whose rows
        /// are ANCHORED on the thing itself (<see cref="PlacedRows.Anchor"/>; <see cref="PositionOf"/>
        /// walks these same lists for the same reason).
        ///
        /// Resolved through the page's own indexes - the very lists the rows are declared from - and
        /// never by reading a row's KEY, which is a string this page builds and not a fact about the
        /// map. A fleet flying a lane has exactly one row, under the end it is arriving at
        /// (<see cref="Bound"/>), and the index holds that one.
        ///
        /// A fleet PARKED at a star is deliberately not one of these: the map draws it in the star's
        /// own berth, so the star is the place the player is reading and the row keeps its
        /// system-ancestor resolution.
        ///
        /// A quest marker planted out in the open - one whose <c>Node</c> is invalid, because the
        /// thing it was planted on has no node, a fleet in mid-lane - is one of these too. The mod's
        /// <c>Marker</c> is a struct and could never be a place (a boxed struct is a fresh reference
        /// every time, and the camera record compares places by reference), but the PIN inside it is
        /// a game entity of the game's own with a position that follows whatever it is stuck to, so
        /// the pin is what answers here. A marker standing at a system is not: it hangs under that
        /// system and lands as a place, exactly as a planet does.
        ///
        /// Null for every other row, which is what leaves the ordinary resolution untouched.
        /// </summary>
        private IGameEntityWithGalaxyPosition OpenSpaceThing(ControlId id)
        {
            if (id == null)
            {
                return null;
            }

            try
            {
                for (int i = 0; i < _drifting.Count; i++)
                {
                    if (id.Equals(ProbeId(_drifting[i])))
                    {
                        return _drifting[i].Probe;
                    }
                }

                for (int i = 0; i < _shots.Count; i++)
                {
                    ObliteratorProjectile shot = _shots[i].Shot;
                    if (shot != null && id.Equals(ProjectileId(shot)))
                    {
                        return shot;
                    }
                }

                for (int i = 0; i < _sighted.Count; i++)
                {
                    CoordinationRequest pin = _sighted[i].Request;
                    if (pin != null && id.Equals(PinId(pin)))
                    {
                        return pin;
                    }
                }

                List<QuestMarkers.Marker> markers = QuestMarkers.Of(PlayerEmpire());
                for (int i = 0; i < markers.Count; i++)
                {
                    if (!markers[i].Node.IsValid && id.Equals(MarkerRowId(markers[i])))
                    {
                        return markers[i].Pin;
                    }
                }

                List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
                for (int i = 0; i < sites.Count; i++)
                {
                    if (id.Equals(sites[i].Node) && Flying(sites[i].Fleet))
                    {
                        return sites[i].Fleet;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: asking what a row out on the map stands for threw: " + e);
            }

            return null;
        }

        /// <summary>
        /// The other way round: the ROW a thing the map draws out on the map has, for a landing that
        /// was handed the thing itself and needs to know where the cursor goes.
        ///
        /// This is what makes every way IN arrive the same way (owner ruling 2026-08-26). A go-to
        /// through the game's own reveal call - a mod notification's Show Location, the game's own
        /// Show Location button, a panel's locate, a table's double click - arrives naming the ENTITY
        /// (<see cref="GalaxyLocate.Request"/>), and without this the landing had to fall back on
        /// finding whatever the map draws NEAREST the point it was sent to
        /// (<see cref="Nearest"/>): right nearly always, a guess in principle, and no answer at all
        /// where two things stand within <see cref="Coincides"/> of each other.
        ///
        /// A FLEET is not here: its own branch in <see cref="FromEntity"/> answers first, because a
        /// fleet's row hangs under a system whose branch has to be opened before the cursor can be
        /// sent to it, and because a docked one is aimed at its BERTH. Everything else of this shape
        /// sits at the top of the stop with no branch to open.
        /// </summary>
        private ControlId OpenSpaceRow(IGameEntityWithGalaxyPosition thing)
        {
            if (thing == null)
            {
                return null;
            }

            try
            {
                for (int i = 0; i < _drifting.Count; i++)
                {
                    if (ReferenceEquals(_drifting[i].Probe, thing))
                    {
                        return ProbeId(_drifting[i]);
                    }
                }

                for (int i = 0; i < _shots.Count; i++)
                {
                    if (ReferenceEquals(_shots[i].Shot, thing))
                    {
                        return ProjectileId(_shots[i].Shot);
                    }
                }

                for (int i = 0; i < _sighted.Count; i++)
                {
                    if (ReferenceEquals(_sighted[i].Request, thing))
                    {
                        return PinId(_sighted[i].Request);
                    }
                }

                List<QuestMarkers.Marker> markers = QuestMarkers.Of(PlayerEmpire());
                for (int i = 0; i < markers.Count; i++)
                {
                    if (!markers[i].Node.IsValid && ReferenceEquals(markers[i].Pin, thing))
                    {
                        return MarkerRowId(markers[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: asking which row a thing out on the map has threw: " + e);
            }

            return null;
        }

        /// <summary>Whether the map draws this fleet away from any star - out on a lane, crossing open
        /// space, or stranded where a cancelled order left it. The game's own question
        /// (<c>FleetPosition.IsInOrbit</c>), and the one that decides whether the fleet is a place of
        /// its own or something the star it is parked at is showing.</summary>
        private static bool Flying(Fleet fleet)
        {
            try
            {
                return fleet != null && !fleet.IsDestroyed && !fleet.Position.IsInOrbit;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The star a fleet is parked at, asked of the fleet's own position rather than
        /// guessed from the map (<c>FleetPosition.GetOrbit</c>) - null for a fleet that is not in
        /// orbit at one.</summary>
        private static StarSystemNode Orbited(Fleet fleet)
        {
            try
            {
                return fleet == null ? null : fleet.Position.GetOrbit() as StarSystemNode;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Show the player a place - the ONE call on this page that moves the camera for the cursor,
        /// and the one record of where the camera has been sent.
        ///
        /// Three answers. A place the camera is already showing moves nothing - which is what makes a
        /// zoom-out by hand survive the rest of a system being read, and what makes the game's own
        /// "show me this" compose: by the time the cursor lands the camera is already there and this
        /// adds no second jump. Coming further IN on a system snaps rather than flies (owner ruling
        /// 2026-08-22: the flight was nine tenths of a second in which nothing could be said), and the
        /// landing's wait is armed here because the camera never reports itself flying afterwards
        /// (<see cref="SnapSettleFrames"/>). Anything else is the slide the map has always made across
        /// to a thing standing somewhere else.
        ///
        /// The camera is never taken back OUT here: stepping from a world up onto its own star is not
        /// a request to see less, and the ways out are the player's own (Backslash, closing the
        /// branch). So "already showing it" includes being further in than was asked for.
        ///
        /// <paramref name="asked"/> is an INTENT rather than a path: "take me there" said out loud - a
        /// go-to, the answer to a reveal (<see cref="Camera"/>) - as against the camera following what
        /// is being read. It moves whatever the record says, because a player who zoomed out by hand
        /// and then pressed go-to has asked to be taken there, and the record - which deliberately
        /// survives that zoom-out - would otherwise answer "already there" over a camera that is not.
        /// Everything else, the cursor being placed anywhere by anybody, comes through the rule above
        /// with the record left to decide.
        /// </summary>
        private void FollowPlace(object place, bool inside, bool asked = false)
        {
            // THE ZOOM LEVEL TELLS YOU WHAT AN EXPANSION GIVES (owner ruling 2026-09-01, the graded
            // model). Below the detail band the map draws no inside for a system - no planet dots, no
            // orbital cards - so there is nothing for the camera to come in on, and the far bands stay
            // what they are for: reading the map's geometry, which lane runs where and what is standing
            // on it. A system opened at those distances therefore opens IN PLACE, and walking its lanes
            // and fleets moves the picture no closer than the player put it.
            //
            // Only for the cursor. A landing SAID OUT LOUD keeps its own framing
            // (<paramref name="asked"/>): it has already forced the band its target needs
            // (<see cref="EnsureBand"/>), and going to a place is a request to be taken there.
            if (inside && !asked && !ZoomBands.MapDetail)
            {
                inside = false;
            }

            // AND NEVER UNDER A LENS (owner ruling 2026-09-01). In scan mode the zoom ladder does not
            // choose how much is drawn, it chooses WHAT THE PICTURE MEANS - a step is a different
            // lens - so a camera move made as a side effect of reading a row would silently change the
            // subject of everything on the screen. Expansion is in place at every lens and a landing
            // slides and seats; the player's own ladder is the only thing that picks a lens. The
            // spoken landing is no exception here, which is why this is asked after
            // <paramref name="asked"/> rather than beside it.
            if (inside && Scanning)
            {
                inside = false;
            }

            if (place == null || (!asked && Showing(place, inside)))
            {
                return;
            }

            StarSystemNode system = place as StarSystemNode;
            try
            {
                // Every camera move the mod makes goes through the same GUI calls the GAME leads the
                // player with, so it is marked as ours or each arrow key would read as a reveal
                // (<see cref="GalaxyLocate.Suppressed"/>). PanTo marks its own.
                if (inside && system != null)
                {
                    // Where the camera was when the inside took it in, so that closing the branch can
                    // hand it back (<see cref="CollapseZoom"/>).
                    NoteJump(system);
                    GalaxyLocate.Suppressed = true;
                    GalaxyViewLevels.SnapTo(system);
                    _settling = SnapSettleFrames;
                    // The one move that changes what the rows already declared SAY, so it is also the
                    // one that holds a descend and an announcement (<see cref="BetweenViews"/>).
                    _binding = ViewBindFrames;
                    // A camera that is PUT somewhere leaves the map's own labels believing it never
                    // moved (<see cref="_labelCatchUp"/>).
                    _labelCatchUp = ViewBindFrames;
                    Remember(place, inside);
                    return;
                }

                // A bookmarked point of galaxy: the same slide the things below get, off the position
                // the player kept rather than off an entity, since there is no entity there at all.
                BookmarkPoint bookmark = place as BookmarkPoint;
                if (bookmark != null)
                {
                    GalaxyViewLevels.CenterOn(bookmark.At, LandingDamping);
                    GalaxyViewLevels.Settle();
                    _settling = SnapSettleFrames;
                    _labelCatchUp = ViewBindFrames;
                    Remember(place, inside);
                    return;
                }

                IGameEntityWithGalaxyPosition entity = place as IGameEntityWithGalaxyPosition;
                if (entity != null)
                {
                    GalaxyViewLevels.PanTo(entity);
                    // It arrives AT ONCE, for the reason coming further in on a system does (owner
                    // ruling 2026-08-26, the 2026-08-22 snap ruling widened): the flight was a second
                    // of drifting in which nothing could be said, and it ended somewhere the player
                    // had already been told about. Every thing the map draws out on the map is
                    // reached this way - a probe, a missile, an ally's pin, a fleet away from its
                    // berth. The recentre is started the ordinary way and then finished the same
                    // frame; where the camera cannot be reached to finish it, the flight simply plays
                    // out and nothing breaks (<see cref="GalaxyViewLevels.Settle"/>).
                    //
                    // A slide onto a BARE POINT with nothing at it is not this: that is the inspect
                    // cell sweeping its own cursor, and it keeps its own handling
                    // (<see cref="Camera"/>).
                    GalaxyViewLevels.CenterOn(entity.GalaxyPosition, LandingDamping);
                    GalaxyViewLevels.Settle();
                    _settling = SnapSettleFrames;
                    _labelCatchUp = ViewBindFrames;
                    Remember(place, inside);
                }
            }
            finally
            {
                GalaxyLocate.Suppressed = false;
            }
        }

        /// <summary>
        /// Whether the camera is already showing this place, closely enough for what is being asked -
        /// the whole of what makes an arrow key inside a system move nothing.
        ///
        /// Believed only while nothing has moved the camera since the record was written
        /// (<see cref="GalaxyViewLevels.Moves"/>). That is the invariant this page keeps: the camera
        /// goes where the record says, or the record stops being believed. Everything else that moves
        /// the camera - the game flying to a fleet the player selected, a landing sliding across open
        /// sky, the inspect cell sweeping - counts its move there, and the count is what tells a record
        /// that still describes the picture from one that describes a picture nobody is looking at.
        /// Without it a fleet selected in its own system left the record saying "in on that system"
        /// over a camera the game had meanwhile framed on the fleet, and every later step among that
        /// system's planets was swallowed: no orbital cards, so no curiosity to act on
        /// (owner-reported 2026-08-26, measured the same day).
        /// </summary>
        private bool Showing(object place, bool inside)
        {
            return _cameraStamp == GalaxyViewLevels.Moves
                && ReferenceEquals(place, _cameraPlace)
                && (_cameraIn || !inside);
        }

        /// <summary>Write down where the camera has just been sent and how close - always AFTER the move
        /// itself, because the record is stamped with the count of moves made by anybody else
        /// (<see cref="Showing"/>) and the mod's own pan is one of the things that counts.</summary>
        private void Remember(object place, bool inside)
        {
            _cameraPlace = place;
            _cameraIn = inside;
            _cameraStamp = GalaxyViewLevels.Moves;
        }

        /// <summary>The player has closed a branch and the camera has come back out of it
        /// (<see cref="Collapse"/>): the place is still what the camera is looking at, but it is no
        /// longer INSIDE it, so opening the same system again brings the camera back in. Closing a
        /// branch the camera was never in on changes nothing.</summary>
        private void LeftPlace(StarSystemNode node)
        {
            if (ReferenceEquals(node, _cameraPlace))
            {
                _cameraIn = false;
            }
        }

        /// <summary>
        /// Write down the view a system's inside is about to take the camera away from, so that
        /// closing the branch can give it back (owner ruling 2026-09-01: COLLAPSE ALWAYS HANDS BACK THE
        /// VIEW YOU WERE BROWSING).
        ///
        /// Only a real jump is remembered - a camera already at the orbital view is not going anywhere
        /// and has nothing to restore, and a second step among the same system's children must not
        /// overwrite the level the first one came from.
        ///
        /// Kept per system, because a player can be in and out of several: opening one at level 8 and
        /// then walking into another from the orbital view leaves the first with a view to go back to
        /// and the second with none, which is exactly what each of them should do when it is shut.
        ///
        /// An INSTANCE field, deliberately: a hot reload builds this page afresh and the memory goes
        /// with it, which is the case the fallback exists for (<see cref="CollapseZoom"/>).
        /// </summary>
        private void NoteJump(StarSystemNode system)
        {
            int level = ZoomBands.Level;
            int inside = ZoomBands.LowestLevel(BandKind.Planets, BandFidelity.Full);
            if (system == null || level < Bands.FirstLevel || level >= inside)
            {
                return;
            }

            if (!_jumpedFrom.ContainsKey(system))
            {
                _jumpedFrom[system] = level;
            }
        }

        /// <summary>
        /// Take the camera back out of a system whose branch has just been closed.
        ///
        /// The view the expansion jumped from where one was written down (<see cref="NoteJump"/>), and
        /// otherwise SPOKEN LEVEL 9 - the inspect cursor's own entry ceiling, so the two "a sane
        /// distance to be put at" cameras in the mod are one number (owner ruling 2026-09-01, revised).
        /// The fallback is not a defensive branch: a hot reload wipes the memory while the player is
        /// standing inside an open system, and the first thing they do afterwards is close it.
        ///
        /// Only while the camera is still looking INTO this system, which is the same gate the old
        /// unzoom had: focus moves the camera about the map freely, so by the time a branch is closed
        /// the player may be reading somewhere else entirely, and flying the camera home from over
        /// there would move a view nobody asked about.
        ///
        /// Silent, like the expansion: the rung it lands on is announced by the one watcher that
        /// reports every zoom change however it was made (<see cref="ZoomWatch"/>).
        /// </summary>
        private void CollapseZoom(StarSystemNode node)
        {
            if (node == null || !ReferenceEquals(GalaxyViewLevels.FocusedSystem, node))
            {
                return;
            }

            int level;
            int step = _jumpedFrom.TryGetValue(node, out level)
                ? level - 1
                : GalaxyInspect.EntryZoomCeiling;
            _jumpedFrom.Remove(node);
            GalaxyViewLevels.ZoomToStep(node, step);
        }

        /// <summary>The view each open system's inside took the camera away from
        /// (<see cref="NoteJump"/>), in spoken levels.</summary>
        private readonly Dictionary<StarSystemNode, int> _jumpedFrom =
            new Dictionary<StarSystemNode, int>();

        /// <summary>Where the camera has been sent and how close - the whole of what the rule above
        /// compares against. Not read off the camera: the game's own answer for "which system is the
        /// orbital view up over" (<see cref="GalaxyViewLevels.FocusedSystem"/>) is null at every zoom
        /// step but the last and lags a flight by its whole duration (measured 2026-08-23), so gating
        /// on it would re-snap after every zoom-out by hand and mis-answer mid-flight. Per page: a
        /// page that has been left knows nothing about where the next one put the camera.
        ///
        /// Written by <see cref="Remember"/> alone, and only ever by a caller that has just moved the
        /// camera itself: <see cref="FollowPlace"/> for the cursor, and the open-sky slide a landing
        /// makes (<see cref="Camera"/>). Every OTHER way the camera moves is COUNTED rather than
        /// recorded (<see cref="GalaxyViewLevels.Moves"/>), which is what keeps a record from
        /// outliving the picture it describes - including the flight the game makes for one of the six
        /// zoom-in fleet actions (<see cref="SeatAfterFleetAction"/>), where a snap onto the system the
        /// game is already flying into costs nothing and rescues a flight that never happened.
        /// </summary>
        private object _cameraPlace;

        private bool _cameraIn;

        /// <summary>The count of camera moves made by anybody else at the moment the record above was
        /// written (<see cref="Showing"/>).</summary>
        private int _cameraStamp;
    }
}

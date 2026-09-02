using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.ES2.UI;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>Where the game has just sent the player: a locate request worked on over several
    /// frames until the map declares something to land on, the target it resolves to, and the
    /// camera and band that landing needs.</summary>
    public sealed partial class GalaxyHudScreen
    {
        // ---- where the game has just sent the player ----

        /// <summary>The request being worked on, so that a page which needs several frames to find its
        /// answer is spending ONE budget on ONE request rather than restarting the clock.</summary>
        private GalaxyLocate.Request _locating;

        private int _locateFrames;

        /// <summary>About a second: long enough for the places to have been declared once on a page
        /// arrived at cold AND for the camera slide the game starts with the request to finish, which is
        /// when it says which of a berth's fleets it meant. Short enough that a request nothing ever
        /// answers gives up rather than seizing a later frame's cursor. It is only ever spent on an
        /// answer that can still improve - a settled one lands the frame it is found.</summary>
        private const int LocateFrames = 60;

        /// <summary>
        /// Put the cursor where the GAME has just sent the player (<see cref="GalaxyLocate"/>) - the
        /// same landing a search makes, so the place it is buried in is opened and the cursor is left on
        /// the thing itself.
        ///
        /// Here rather than on arrival because the two ways in are the same thing: the page opens
        /// showing the place, or it was already open and the camera moved. The next idle fleet is the
        /// second kind and the military screen's locate is the first, and neither is special.
        ///
        /// A request the tree has no node for is still answered - the camera HAS moved, and a player
        /// told nothing would be left reading a place the map is no longer showing.
        /// </summary>
        private void FollowTheGame()
        {
            GalaxyLocate.Request wanted = GalaxyLocate.Peek();
            if (wanted == null)
            {
                _locating = null;
                return;
            }

            if (!ReferenceEquals(wanted, _locating))
            {
                _locating = wanted;
                _locateFrames = LocateFrames;
                _holdingAtRequest = SelectedFleet();
            }

            try
            {
                bool settled;
                MapTarget target = Locate(wanted, out settled);
                // The budget is for an answer that can still get better - a page that has not declared
                // anything yet, a berth the game has not yet said which of its fleets it meant. A
                // settled answer is given now, including the settled answer that nothing on the map is
                // there.
                if (!settled && --_locateFrames > 0)
                {
                    return;
                }

                GalaxyLocate.Take();
                _locating = null;
                Land(target, wanted);
            }
            catch (Exception e)
            {
                GalaxyLocate.Take();
                _locating = null;
                Log.Warn("galaxy: landing on what the game located threw: " + e);
            }
        }

        /// <summary>Whether the map has declared anything to land on yet. A page arrived at from a save
        /// being loaded has not run <see cref="BuildSystems"/> even once.</summary>
        private bool Declaring()
        {
            return _systems.Count > 0;
        }

        /// <summary>Speak what the landing needs saying beyond the node itself, then send the cursor.
        /// The node's own announcement is the whole of an ordinary landing - it names the place the game
        /// went to, which is the answer to the question that was asked.</summary>
        private void Land(MapTarget target, GalaxyLocate.Request wanted)
        {
            if (wanted.Quest != null)
            {
                Voice.Say(QuestLocated(wanted.Quest), false);
            }

            // Everything else - the cell, the cursor, the camera, and the "shown on the map" line a
            // request nothing answers gets - is the page's one landing (<see cref="GoTo"/>).
            GoTo(target, MapCamera.Auto);
        }

        /// <summary>
        /// GO AND LOOK AT THIS - the one landing on this page.
        ///
        /// Five things used to send the player somewhere on the map and each answered the same three
        /// questions for itself: does the free inspect cursor stay up, does the tree cursor move, and
        /// does the camera zoom or slide. They disagreed - the scanner jumped the CELL onto a planet,
        /// which is a thing the cell cannot read, leaving the player on a square of sky beside the
        /// world they asked for - so the rules live in one place now
        /// (<see cref="MapLandings.Decide"/>, off the engine and unit-tested) and every caller hands
        /// in a resolved <see cref="MapTarget"/>: the game's own show-location
        /// (<see cref="FollowTheGame"/>), the scanner's go-to, travelling a starlane
        /// (<see cref="Arrive"/>), the go-to-location key, and a bookmark jump made with the inspect
        /// cell live (<see cref="GalaxyBookmarks"/> - the one bookmark shape this landing can express).
        ///
        /// <paramref name="reach"/> is how far the landing goes, and it decides one thing: whether the
        /// camera FRAMES the destination or merely follows the cursor there
        /// (<see cref="MapReach"/>). Travelling a starlane is the one LOCAL caller - the destination is
        /// the neighbour of the row the player is standing on - and it keeps the picture at the
        /// distance they put it.
        ///
        /// Under a live cell every one of them arrives the same way: ONLY THE CELL MOVES (owner
        /// rulings 2026-08-31). The zoom is not touched - the cell's own slide is the whole camera
        /// move - and neither is the tree cursor, so leaving the mode puts the player back on the row
        /// they armed it from rather than on whatever they last looked at through the square. That
        /// second half reverses an earlier ruling of the same day which had the landing seat the
        /// cursor underneath the cell; what died with it was a deferred seat, its camera-free mark and
        /// a re-seat call on the mode - a lever apiece, all removed rather than left inert.
        ///
        /// The camera moves are marked as the MOD's own (<see cref="GalaxyLocate.Suppressed"/>): the
        /// mod pans through the same calls the game leads the player with, and an unmarked pan here
        /// would come straight back round as a fresh locate request.
        ///
        /// Answers whether the cursor was sent to a node - false for a fleet the tree has no row for
        /// and for a point the map draws nothing at, both of which leave the caller to say its own
        /// piece.
        /// </summary>
        internal bool GoTo(
            MapTarget target,
            MapCamera camera,
            MapReach reach = MapReach.Elsewhere
        )
        {
            try
            {
                MapLanding plan = MapLandings.Decide(target.Thing, GalaxyInspect.Live, reach);
                if (plan.Unplaced || (target.Id == null && target.Select == null))
                {
                    // Owner ruling 2026-08-22: everything the game can point the player at is supposed
                    // to have a row, so a request that lands on nothing is a DEFECT to model and not a
                    // behaviour to fall back on. The camera HAS moved, so the player is told; the
                    // request is logged where the dev sweep can find it; and nothing else moves.
                    Log.Warn(
                        "galaxy go-to: nothing on the map stands at "
                            + target.At.x.ToString("F2")
                            + ", "
                            + target.At.z.ToString("F2")
                            + " - the tree has no row for what the game pointed at"
                    );
                    Voice.Say(ModStrings.Get(ModStrings.GalaxyShownOnMap), false);
                    return false;
                }

                // A LANDING ON SOMETHING THE LENS DOES NOT DRAW LEAVES THE LENS FIRST (owner ruling
                // 2026-09-01), and only then lands the ordinary way (<see cref="LeaveTheLens"/>).
                bool leaving = Scanning && !DrawnByTheLens(target);
                if (leaving)
                {
                    LeaveTheLens();
                }

                if (plan.ExitInspect)
                {
                    GalaxyInspect.Dismiss();
                }

                if (plan.MoveCell)
                {
                    GalaxyPosition origin = GalaxyCoordinates.Origin();
                    _inspect.JumpTo(
                        MapCoordinates.Round(target.At.x - origin.X),
                        MapCoordinates.Round(target.At.z - origin.Y)
                    );
                }

                GraphNavigator navigator = ModEntry.Navigator;
                if (plan.FocusNode)
                {
                    // Before the cursor is sent anywhere: the picture has to be drawing the kind of
                    // thing it is being sent to, or there is no row to land on.
                    EnsureBand(target, leaving);
                }

                if (plan.FocusNode && target.Id != null && navigator != null)
                {
                    navigator.FocusNode(target.Id, plan.AnnounceNode);
                }

                if (target.Select != null && !plan.MoveCell)
                {
                    // The one fleet the tree has no row for: the map's own selection is this game's
                    // only "go to that fleet".
                    SelectFleet(target.Select);
                }

                Camera(target, camera, plan);
                return target.Id != null;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: going to a place on the map threw: " + e);
                return false;
            }
        }

        /// <summary>
        /// A SNAP LANDING FORCES ITS TARGET'S BAND (owner ruling 2026-09-01): before the player is put
        /// on a thing, the camera comes to the nearest distance at which the map is drawing that kind
        /// of thing at all - a planet or anything hanging off one to the orbital view, a fleet to the
        /// lozenges, a system to the band that names the systems - and no closer
        /// (<see cref="Bands.LowestLevel"/>).
        ///
        /// It is not a camera preference and it ignores the caller's own
        /// (<see cref="MapCamera"/>): a band that draws nothing of the kind declares no row for it, so
        /// a landing made there would send the cursor to a node that does not exist and the player
        /// would hear nothing at all - measured at levels 1-2, where a bookmark jump moved the camera
        /// and said not a word. The camera's own framing afterwards is still the landing's
        /// (<see cref="Camera"/>).
        ///
        /// Never OUT: a player reading the orbital view who goes to a fleet is not asking to be pulled
        /// back to the fleet band. And never for a thing the mod itself put on the map - a bookmarked
        /// point is an annotation rather than a rendering and has a row at every level, so a jump to
        /// one slides exactly as it always has.
        /// </summary>
        private void EnsureBand(MapTarget target, bool leaving)
        {
            // Not under a lens: there the rung IS the lens, so forcing a band would answer "take me to
            // that system" by changing what the whole screen means (owner ruling 2026-09-01). The
            // lens's own parity filter is what makes that safe - a landing is only ever sent to a kind
            // the lens is drawing, so there is a row waiting wherever the camera already is; a landing
            // on anything else has just LEFT the lens, and then the ordinary rule applies again. The
            // caller's own answer to that is taken rather than re-asked, because the game's flag has
            // not necessarily turned over by this line.
            if (Scanning && !leaving)
            {
                return;
            }

            int level = ZoomBands.Level;
            int need = BandNeeded(target, false);
            if (level < Bands.FirstLevel || need < Bands.FirstLevel || level >= need)
            {
                return;
            }

            try
            {
                GalaxyLocate.Suppressed = true;
                GalaxyViewLevels.SetZoomHere(need - 1);
                GalaxyViewLevels.Settle();
            }
            finally
            {
                GalaxyLocate.Suppressed = false;
            }

            // The picture is at a different distance than the page's record of where it sent the
            // camera was written at (<see cref="GalaxyViewLevels.Moves"/>).
            GalaxyViewLevels.Moved();
            _settling = SnapSettleFrames;
            _binding = ViewBindFrames;
            _labelCatchUp = ViewBindFrames;
        }

        /// <summary>The nearest-out level at which this target has a row, read off the one band table.
        /// A planet and everything drawn at one needs the orbital view, where the map draws the cards
        /// those rows are; a fleet needs the lozenges; a place needs the band that names the systems;
        /// and anything else standing out on the map is drawn beside the full nameplate, so it needs
        /// the same band the nameplate does. A bookmarked point needs nothing.</summary>
        private int BandNeeded(MapTarget target, bool scanning)
        {
            switch (target.Thing)
            {
                case MapThing.PlanetBound:
                    return Bands.LowestLevel(BandKind.Planets, scanning, BandFidelity.Full);
                case MapThing.Place:
                    return Bands.LowestLevel(BandKind.Systems, scanning, BandFidelity.Name);
                case MapThing.Point:
                    if (target.Select is Fleet || target.Standing is Fleet)
                    {
                        return Bands.LowestLevel(BandKind.Fleets, scanning, BandFidelity.Full);
                    }

                    return BookmarkAt(target.Id) != null
                        ? -1
                        : Bands.LowestLevel(BandKind.OpenSpace, scanning, BandFidelity.Full);
                default:
                    return -1;
            }
        }

        /// <summary>
        /// WHETHER THE LENS IS DRAWING THIS KIND OF THING AT ALL, here.
        ///
        /// The same table and the same question the tree filters its rows with, asked of the scan
        /// ladder: a landing sent to a kind the lens hides would put the cursor on a node that does not
        /// exist and say nothing at all. It is asked of the KIND and not of the level, because "the
        /// System lens draws no planet dots" and "no lens draws a fleet" are the same fact written once
        /// (<see cref="Bands"/>).
        ///
        /// A bookmarked POINT is always drawn: it is the player's own annotation rather than a
        /// rendering, and it has a row at every rung of both ladders.
        /// </summary>
        private bool DrawnByTheLens(MapTarget target)
        {
            switch (target.Thing)
            {
                case MapThing.PlanetBound:
                    // A world has a row in the mode wherever the lens rings its star with circles -
                    // at the DOT fidelity the tree already reads it at, which is all a landing needs.
                    return ZoomBands.Shows(BandKind.Planets);
                case MapThing.Place:
                    return ZoomBands.Shows(BandKind.Systems);
                case MapThing.Point:
                    if (BookmarkAt(target.Id) != null)
                    {
                        return true;
                    }

                    return target.Select is Fleet || target.Standing is Fleet
                        ? ZoomBands.Shows(BandKind.Fleets)
                        : ZoomBands.Shows(BandKind.OpenSpace);
                default:
                    return false;
            }
        }

        /// <summary>Leave the lens through the game's own toggle - the very call its own close button
        /// makes (<c>GuiManager.ToggleScanView</c>, which refuses by itself while a modal or a ground
        /// battle is up). The page says its own name on the way out, as it does for any other exit, so
        /// the player is told the light has changed before they are told where they have been
        /// sent.</summary>
        private static void LeaveTheLens()
        {
            try
            {
                Gui.GuiGameWindowService.ToggleScanView();
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: leaving the scan lens for a landing threw: " + e);
            }
        }

        /// <summary>Bring the camera to what was landed on: IN on a place - through the page's one
        /// camera rule, so the landed node's own focus adds nothing on top (<see cref="FollowPlace"/>)
        /// - and a slide onto a bare point. Where the cell is driving, the cell has already slid and
        /// only a place's zoom is added on top - so the picture is the same whichever way the player is
        /// reading the map.
        ///
        /// WHETHER the move is made as a thing asked for out loud is the plan's
        /// (<see cref="MapLanding.Frame"/>): a landing that frames overrides the distance the player
        /// chose, and a LOCAL hop asks the camera rule for exactly what walking onto the row asks for
        /// - which at the far bands is a slide and nothing more.</summary>
        private void Camera(MapTarget target, MapCamera wanted, MapLanding plan)
        {
            MapCameraMove move = plan.Camera;
            switch (wanted)
            {
                case MapCamera.Zoom:
                    move = MapCameraMove.Zoom;
                    break;
                case MapCamera.Slide:
                    move = MapCameraMove.Slide;
                    break;
                case MapCamera.None:
                    move = MapCameraMove.None;
                    break;
            }

            if (move == MapCameraMove.None)
            {
                return;
            }

            if (move == MapCameraMove.Zoom && target.System != null)
            {
                // Through the page's one camera rule (<see cref="FollowPlace"/>) rather than beside
                // it: a landing is the cursor being sent to a place, so it asks for the same thing
                // walking there asks for, and the rule then has nothing left to do when the landed
                // node's focus commits. That is also what makes the game's own show-location compose -
                // the record says the camera is already there.
                FollowPlace(target.System, true, plan.Frame);
                return;
            }

            // Everything the map draws out on the map goes through that same rule, for the same
            // reason: the thing IS the place (<see cref="Place"/>), so the landing asks for exactly
            // what walking onto the row asks for, the record then says the camera is already there,
            // and the landed row's own focus has nothing left to do. It is also the route that works
            // from inside a star's orbital view, which a bare recentre does not leave.
            //
            // The target carries the thing where the landing already had it in hand; otherwise the
            // row's own id is looked up in the same indexes the rows were declared from. A DOCKED
            // fleet is not one of these - it is its star's, and falls through to the zoom above by
            // way of its row's focus.
            IGameEntityWithGalaxyPosition drawn = target.Standing;
            Fleet carried = drawn as Fleet;
            if (carried != null && !Flying(carried))
            {
                // A DOCKED fleet is its STAR's place (<see cref="Place"/>), so its landing asks the
                // camera for exactly what walking onto the row asks for - and the record then says the
                // camera is already there, which is what leaves the row's own focus nothing to do.
                //
                // Sliding onto the BERTH instead (the point the target carries, because that is where
                // the ship is DRAWN and where the inspect cell must open) put the camera somewhere the
                // row's focus moved it off again a beat later: the delayed second move the owner
                // reported from the military page's second click, and the same one every other way in
                // makes - the idle-fleet button, the named-ship panel, the fleet list - because all of
                // them end here. The berth stays the target's point; only the CAMERA is the star's.
                StarSystemNode berthed =
                    target.System != null ? target.System : Orbited(carried);
                if (berthed != null)
                {
                    FollowPlace(berthed, true, plan.Frame);
                    return;
                }

                drawn = null;
            }

            if (drawn == null)
            {
                drawn = OpenSpaceThing(target.Id);
            }

            if (drawn != null)
            {
                FollowPlace(drawn, false, plan.Frame);
                return;
            }

            try
            {
                // A slide across open sky is left as a slide: there is no card to wait for at the
                // other end, so it costs the announcement nothing and the picture stays readable to
                // a sighted player beside the keyboard.
                GalaxyLocate.Suppressed = true;
                GalaxyViewLevels.CenterOn(target.At, LandingDamping);
                // The camera is now on the thing that was landed on, standing at its own point, and
                // the record says so - or the slide went to a bare point, and the record is left not
                // believed (the slide counted itself: <see cref="GalaxyViewLevels.Moves"/>). Saying so
                // is what keeps the landed node's own focus from sliding the camera a second time.
                object landed = target.Id == null ? null : target.Id.Subject;
                if (landed is IGameEntityWithGalaxyPosition)
                {
                    Remember(landed, false);
                }
            }
            finally
            {
                GalaxyLocate.Suppressed = false;
            }
        }

        /// <summary>The same slide the inspect cursor moves the camera with, so an arrival looks like
        /// every other arrival on this map.</summary>
        private const float LandingDamping = 0.3f;

        /// <summary>The quest a pin belongs to, in the game's own title for it.</summary>
        private static string QuestLocated(Quest quest)
        {
            try
            {
                return ModStrings.Format(
                    ModStrings.GalaxyQuestShownOnMap,
                    AgeText.Clean(new GuiQuest(quest).Title)
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: naming the quest the game located threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// What the game asked to be looked at, resolved to a target this page can land on.
        ///
        /// The QUEST marker first, where the reveal was a quest pin: the position is that marker's own
        /// (<c>ShowQuestLocation</c> picks the marker and then makes the ordinary position request with
        /// it), so the marker is found by IDENTITY among the quest's own and never by guessing which
        /// thing near the point was meant. Then the thing itself, since a request that named one is
        /// exact; then the point, and only for something standing at it.
        ///
        /// <paramref name="settled"/> is false while a later frame could answer better - the whole of
        /// what the frame budget is for.
        /// </summary>
        private MapTarget Locate(GalaxyLocate.Request wanted, out bool settled)
        {
            MapTarget marker;
            if (LocatedMarker(wanted, out marker))
            {
                settled = true;
                return marker;
            }

            MapTarget named;
            if (FromEntity(wanted.Entity, out named))
            {
                settled = true;
                return named;
            }

            return Nearest(wanted.Position, out settled);
        }

        /// <summary>The marker a quest reveal was made for, found among that quest's own markers by
        /// the position the request carries. False for every other kind of request, and for a quest
        /// whose markers the page cannot place.</summary>
        private bool LocatedMarker(GalaxyLocate.Request wanted, out MapTarget target)
        {
            target = default(MapTarget);
            if (wanted.Quest == null || !Declaring())
            {
                return false;
            }

            // The same "is this the thing the point meant" radius every other landing uses, asked
            // through the same primitive (<see cref="NearestPick"/> squares it once, so nothing here
            // squares it again). Offer answers false for anything outside the radius, so the first
            // marker it accepts is the one this walk wants.
            NearestPick within = new NearestPick(Coincides);
            List<QuestMarkers.Marker> markers = QuestMarkers.Of(PlayerEmpire());
            for (int i = 0; i < markers.Count; i++)
            {
                if (
                    ReferenceEquals(markers[i].Quest, wanted.Quest)
                    && within.Offer(i, ((Vector3)markers[i].At - wanted.Position).sqrMagnitude)
                )
                {
                    return MarkerTarget(markers[i], out target);
                }
            }

            return false;
        }

        /// <summary>Where a marker's node hangs: under the system it stands at, or a row of its own
        /// out in the open. False where the map is not naming the system it stands at, which is the
        /// one case that has neither.</summary>
        internal bool MarkerTarget(QuestMarkers.Marker marker, out MapTarget target)
        {
            target = default(MapTarget);
            StarSystemNode at = MarkerSystem(marker);
            if (at == null)
            {
                if (!marker.Node.IsValid)
                {
                    target = MapTarget.Point(MarkerRowId(marker), marker.At);
                    return true;
                }

                return false;
            }

            // A marker standing at a system is a CHILD of it - drawn at the star, not a thing of its
            // own out on the map - so it lands the way a planet does: the free cursor ends, the branch
            // opens, and the camera comes in.
            target = MapTarget.Under(at, MarkerId(at, marker), marker.At);
            return true;
        }

        /// <summary>The same question the GAME's locate asks, for a caller inside the mod: where on
        /// this page does this thing live. Used by the scanner to send the cursor to what it found -
        /// through the page's own landing, branch opening included, rather than a second route to the
        /// same nodes.</summary>
        internal ControlId NodeFor(IGameEntityWithGalaxyPosition entity)
        {
            MapTarget target;
            return FromEntity(entity, out target) ? target.Id : null;
        }

        /// <summary>The same, as a landing target - which is what the caller sending the player there
        /// actually needs, since a fleet and a system are landed on differently.</summary>
        internal bool TargetFor(IGameEntityWithGalaxyPosition entity, out MapTarget target)
        {
            return FromEntity(entity, out target);
        }

        /// <summary>
        /// The inverse: where on the MAP the thing behind a node stands.
        ///
        /// Only the rows whose thing has a position of its own answer - a fleet, a probe, a missile, an
        /// ally's pin. A system's node carries the system as its reference and is answered by the walk
        /// that calls this (<see cref="Screens.GalaxyInspect.FocusedPlace"/>); a planet's row and a
        /// lane's have no position of their own and fall through to their system's, which is where the
        /// map draws them.
        ///
        /// It exists because those four rows are keyed STRUCTURALLY - a fleet's key names the system it
        /// is drawn at, which is what keeps two fleets of the same name apart - so there is no backing
        /// object on the id to read a position off. The ids are rebuilt here by the same code that
        /// declared them, never parsed back out of the key.
        /// </summary>
        internal bool PositionOf(ControlId id, out GalaxyPosition at)
        {
            at = default(GalaxyPosition);
            if (id == null)
            {
                return false;
            }

            try
            {
                for (int i = 0; i < _drifting.Count; i++)
                {
                    if (id.Equals(ProbeId(_drifting[i])))
                    {
                        at = _drifting[i].Probe.GalaxyPosition;
                        return true;
                    }
                }

                // The SIGHTED set, not the drawn labels: <see cref="AddProjectiles"/> declares the
                // rows from this list, so reading any other one lets a missile's position disagree
                // with the row that asked for it - a label the camera has culled is missing from
                // <c>_projectiles</c> while its row is still declared (owner ruling 2026-08-26).
                for (int i = 0; i < _shots.Count; i++)
                {
                    ObliteratorProjectile shot = _shots[i].Shot;
                    if (shot != null && id.Equals(ProjectileId(shot)))
                    {
                        at = shot.GalaxyPosition;
                        return true;
                    }
                }

                for (int i = 0; i < _sighted.Count; i++)
                {
                    CoordinationRequest pin = _sighted[i].Request;
                    if (pin != null && id.Equals(PinId(pin)))
                    {
                        at = pin.GalaxyPosition;
                        return true;
                    }
                }

                List<QuestMarkers.Marker> markers = QuestMarkers.Of(PlayerEmpire());
                for (int i = 0; i < markers.Count; i++)
                {
                    if (!markers[i].Node.IsValid && id.Equals(MarkerRowId(markers[i])))
                    {
                        at = markers[i].At;
                        return true;
                    }
                }

                List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
                for (int i = 0; i < sites.Count; i++)
                {
                    if (id.Equals(sites[i].Node))
                    {
                        at = sites[i].Fleet.GalaxyPosition;
                        return true;
                    }
                }

                // A place the PLAYER put on the map. It belongs in this list for the same reason the
                // four above do - its row is keyed structurally, so nothing on the id says where it is
                // - and it was missing from it, which is the whole of bug 2026-08-31: a caller walking
                // UP from a row that could not answer reached the row's constellation, whose subject
                // is an entity with a position of its own (the centroid the map writes the name at),
                // and armed the inspect cursor a whole stretch of sky away from the bookmark the
                // player was standing on.
                BookmarkPoint bookmark = BookmarkAt(id);
                if (bookmark != null)
                {
                    at = bookmark.At;
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding where a node's own thing stands threw: " + e);
            }

            return false;
        }

        /// <summary>
        /// The landing for a thing the game NAMED.
        ///
        /// A fleet is where the map draws it - a point of its own, so the free cursor may stay up.
        /// Everything that stands at a system - the system, a colony of it, a planet in it - is that
        /// system, and the camera goes IN: the game's own locate for a planet aims at the star (the
        /// entity overload throws the entity away and keeps the position, ES2 facts), so a system is
        /// the whole of what the request said.
        ///
        /// Everything ELSE the map draws out on the map - a probe under way, a missile in flight, an
        /// ally's pin, a quest pin planted on something with no node - answers here too, by IDENTITY
        /// (<see cref="OpenSpaceRow"/>). It used to fall past this method to <see cref="Nearest"/>,
        /// which found the same row by POSITION and so was right nearly always and a guess in
        /// principle. Answering by identity is what makes the game's Show Location, a mod
        /// notification's, a panel's locate and the scanner's Alt+Home all arrive at the same place
        /// the same way (owner ruling 2026-08-26).
        /// </summary>
        private bool FromEntity(IGameEntityWithGalaxyPosition entity, out MapTarget target)
        {
            target = default(MapTarget);
            if (entity == null)
            {
                return false;
            }

            Fleet fleet = entity as Fleet;
            if (fleet != null)
            {
                List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
                for (int i = 0; i < sites.Count; i++)
                {
                    if (ReferenceEquals(sites[i].Fleet, fleet))
                    {
                        target = MapTarget.Point(Reveal(sites[i]), Berth(fleet), fleet);
                        return true;
                    }
                }

                return false;
            }

            ControlId row = OpenSpaceRow(entity);
            if (row != null)
            {
                target = MapTarget.Point(row, (Vector3)entity.GalaxyPosition, entity);
                return true;
            }

            ColonizedStarSystem colony = entity as ColonizedStarSystem;
            Planet planet = entity as Planet;
            StarSystemNode node = (
                colony != null
                    ? colony.Node
                    : planet != null
                        ? planet.StarSystemNode
                        : entity as GameNode
            ) as StarSystemNode;
            ControlId id = SystemId(node);
            if (id == null)
            {
                return false;
            }

            target = MapTarget.Place(node, id, node.GalaxyPosition);
            return true;
        }

        /// <summary>A system's node id, but only while this page is declaring that system: the map
        /// draws the names of the systems the player has seen, and the tree says the same
        /// (<see cref="Perceived"/>).</summary>
        private ControlId SystemId(StarSystemNode node)
        {
            return node != null && _namedSet.Contains(node)
                ? ControlId.Structural(SystemKey(node))
                : null;
        }

        /// <summary>
        /// Where a system's node hangs in the tree, as the PATH its id is
        /// (<c>galaxy:constellation/516/system/548</c>).
        ///
        /// The one place a system's key is composed. Every id under a system is built by appending to
        /// what this returns, and every landing aimed at one is built by calling it - which is what
        /// makes the constellation a real ancestor to the engine: a landing inside a collapsed
        /// constellation opens it, one level per build, by reading the ancestry out of the key
        /// (<see cref="KeyGraph.AncestorKeys"/>). A second site composing the old flat key would
        /// silently declare a node nothing could ever reach.
        ///
        /// The head CHANGES when the constellation becomes explored - the system moves out of the
        /// unexplored group and into its own - which is why a system's node carries the system itself
        /// as its reference: the cursor rides the object across the move rather than the key.
        /// </summary>
        private static string SystemKey(StarSystemNode node)
        {
            return SystemKey(node, PlayerEmpire());
        }

        private static string SystemKey(StarSystemNode node, Empire empire)
        {
            return GroupKey(node.Constellation, empire) + "/system/" + node.GUID;
        }

        /// <summary>The head of every key in one stretch of sky - the constellation's own where the
        /// player has explored it, and the one shared bucket where they have not.</summary>
        private static string GroupKey(Constellation constellation, Empire empire)
        {
            return Explored(constellation, empire)
                ? ConstellationKey + constellation.GUID
                : UnexploredKey;
        }

        private const string ConstellationKey = "galaxy:constellation/";

        /// <summary>Everything the player has not yet been shown a constellation NAME for, in one
        /// group. Not keyed per constellation: naming five buckets would say by their number how much
        /// of the galaxy is out there, which is the very thing the map is not showing.</summary>
        private const string UnexploredKey = "galaxy:constellation/unexplored";

        /// <summary>Whether the map draws this constellation's name at all - the label's own gate
        /// (<c>ConstellationLabel.ShowOrHideIfVisibleByEmpire</c>), asked the same way so the tree and
        /// the picture name the same regions. Its staleness is mirrored deliberately: the aggregate
        /// only recomputes when the game raises a node-exploration event, so on the first turns every
        /// constellation reads unexplored even the one the empire is sitting in - and the map draws no
        /// name across it either.</summary>
        private static bool Explored(Constellation constellation, Empire empire)
        {
            return ConstellationMap.Explored(constellation, empire);
        }

        /// <summary>One thing the map is drawing, and where it is drawn.</summary>
        private struct Spot
        {
            public Vector3 At;

            /// <summary>The node, for everything but a fleet.</summary>
            public ControlId Id;

            /// <summary>The system it is, where it is a place - what the camera zooms in on.</summary>
            public StarSystemNode System;

            /// <summary>Which fleet site this is, or -1. A fleet's node id is not made until it wins,
            /// because making one records a branch to open.</summary>
            public int Site;
        }

        /// <summary>
        /// The thing the map draws AT a point, or nothing where the point is out in the open.
        ///
        /// The tolerance is deliberately tight (<see cref="Coincides"/>): the question is "is this
        /// point where that thing is", not "what is nearest". Owner ruling 2026-08-22 - everything the
        /// game can point the player at is supposed to have a row of its own, so answering a point
        /// nothing stands at with the nearest system would hide the missing row behind a landing that
        /// sounds right, and the honest answer is a logged defect.
        ///
        /// The order candidates are offered in is the tie-break (<see cref="NearestPick"/>), and one tie
        /// is exact rather than coincidental: a fleet parked at a system says its position IS that
        /// system's (<c>FleetPosition</c> sets it from the node), so a request aimed at a star would
        /// otherwise be answered by whichever fleet happens to be sitting there. Places first, then.
        /// A fleet is offered at its BERTH - the slot the map draws it in, which is beside the star
        /// rather than on it - so a call aiming at a berth still picks the fleet out.
        /// </summary>
        private MapTarget Nearest(Vector3 position, out bool settled)
        {
            // A page arrived at cold has declared nothing, and "nothing is there" would be a wrong
            // answer rather than a late one.
            settled = Declaring();
            List<FleetSite> sites = FleetIndex(new HashSet<ControlId>());
            List<Spot> spots = new List<Spot>(_systems.Count + sites.Count);
            for (int i = 0; i < _systems.Count; i++)
            {
                Add(spots, _systems[i].GalaxyPosition, SystemId(_systems[i]), _systems[i], -1);
            }

            for (int i = 0; i < _drifting.Count; i++)
            {
                Add(spots, _drifting[i].Probe.GalaxyPosition, ProbeId(_drifting[i]), null, -1);
            }

            // The SIGHTED set, for the reason <see cref="PositionOf"/> reads it: a spot offered here
            // is answered with a ROW, and the rows come from this list.
            for (int i = 0; i < _shots.Count; i++)
            {
                ObliteratorProjectile shot = _shots[i].Shot;
                if (shot != null)
                {
                    Add(spots, shot.GalaxyPosition, ProjectileId(shot), null, -1);
                }
            }

            for (int i = 0; i < _sighted.Count; i++)
            {
                CoordinationRequest pin = _sighted[i].Request;
                if (pin != null)
                {
                    Add(spots, pin.GalaxyPosition, PinId(pin), null, -1);
                }
            }

            // Every quest marker out in the open is a row too, so a quest pin the reveal did not name
            // as a quest still lands on the pin rather than on the sky it hangs in.
            List<QuestMarkers.Marker> markers = QuestMarkers.Of(PlayerEmpire());
            for (int i = 0; i < markers.Count; i++)
            {
                if (!markers[i].Node.IsValid)
                {
                    Add(spots, markers[i].At, MarkerRowId(markers[i]), null, -1);
                }
            }

            for (int i = 0; i < sites.Count; i++)
            {
                Add(spots, Berth(sites[i].Fleet), null, null, i);
            }

            NearestPick pick = new NearestPick(Coincides);
            for (int i = 0; i < spots.Count; i++)
            {
                pick.Offer(i, (spots[i].At - position).sqrMagnitude);
            }

            if (!pick.Found)
            {
                return MapTarget.Nowhere(position);
            }

            Spot won = spots[pick.Index];
            if (won.Site < 0)
            {
                return won.System != null
                    ? MapTarget.Place(won.System, won.Id, won.At)
                    // A probe, a missile, an ally's pin and an open-space marker all sit at the top of
                    // the stop, so there is never a branch to open before the cursor can be sent to one.
                    : MapTarget.Point(won.Id, won.At);
            }

            bool holding;
            FleetSite site = sites[Holding(sites, won, out holding)];
            settled &= holding;
            return MapTarget.Point(Reveal(site), Berth(site.Fleet), site.Fleet);
        }

        /// <summary>
        /// How near a point has to be to something the map draws before it IS that thing, in the
        /// galaxy's own units.
        ///
        /// A coincidence, not a neighbourhood: the closest two systems in a galaxy stand 6.7 units
        /// apart (measured on the fixture; 10.6 on average), so nothing can be mistaken for its
        /// neighbour, while the small offsets the map draws a fleet's berth and a planet's orbit at
        /// still land on the thing itself.
        /// </summary>
        private const float Coincides = 1.5f;

        private static void Add(
            List<Spot> spots,
            Vector3 at,
            ControlId id,
            StarSystemNode system,
            int site
        )
        {
            if (id != null || site >= 0)
            {
                spots.Add(new Spot { At = at, Id = id, System = system, Site = site });
            }
        }

        /// <summary>Where the map draws a fleet: its berth in the system's docking slot while it is
        /// parked there, and its own position while it is out on a lane. The two are different questions
        /// because a parked fleet's position is the STAR's, and a request aimed at the berth is a
        /// request for the fleet.</summary>
        private static Vector3 Berth(Fleet fleet)
        {
            try
            {
                IVisibleDockingSlotRepositoryService slots =
                    Amplitude.Unity.Framework.Services.GetService<IVisibleDockingSlotRepositoryService>();
                DockingSlotCursorTarget slot = slots == null
                    ? null
                    : slots.GetDockingSlotWithFleet(fleet);
                return slot == null ? (Vector3)fleet.GalaxyPosition : slot.transform.position;
            }
            catch (Exception)
            {
                return fleet.GalaxyPosition;
            }
        }

        /// <summary>
        /// Which of the fleets sharing one berth was meant.
        ///
        /// A point cannot say: every fleet parked at a system is drawn in the same slot. The game says
        /// it by SELECTING the fleet - which it does a few frames later, once the camera it started
        /// moving has arrived (<c>EndTurnWindow.SelectFleetWhenViewReady</c>), so the selection standing
        /// at the moment of the request is the previous answer and not this one. Hence
        /// <paramref name="settled"/>: while the berth holds several fleets and the game has not yet
        /// changed its mind, the first of them is a provisional answer and the budget is spent waiting
        /// for a better one. A berth with one fleet in it has nothing to wait for.
        /// </summary>
        private int Holding(List<FleetSite> sites, Spot won, out bool settled)
        {
            Fleet selected = SelectedFleet();
            int sharing = 0;
            int chosen = -1;
            for (int i = 0; i < sites.Count; i++)
            {
                if ((Berth(sites[i].Fleet) - won.At).sqrMagnitude >= 0.0001f)
                {
                    continue;
                }

                sharing++;
                if (ReferenceEquals(sites[i].Fleet, selected))
                {
                    chosen = i;
                }
            }

            settled = sharing <= 1 || (chosen >= 0 && !ReferenceEquals(selected, _holdingAtRequest));
            // The fleet the game is holding is the better guess even unsettled, which is what the
            // budget running out falls back to.
            return chosen >= 0 ? chosen : won.Site;
        }

        /// <summary>What the game's cursor was holding when the request arrived, so that a selection
        /// made in ANSWER to it can be told from the one that was already standing.</summary>
        private Fleet _holdingAtRequest;

        /// <summary>The fleet the map's own cursor is holding, or null while it is holding nothing. The
        /// cursor is half the answer: the garrison cursor is what "a fleet is selected" means to this
        /// game, and the panel's own visibility is gated on the same thing (ES2 facts).</summary>
        private static Fleet SelectedFleet()
        {
            try
            {
                if (!(Gui.GetCursor() is GalaxyGarrisonCursor))
                {
                    return null;
                }

                List<Fleet> selected = FleetOrders.Selected();
                return selected.Count == 0 ? null : selected[0];
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: asking what the cursor is holding threw: " + e);
                return null;
            }
        }
    }
}

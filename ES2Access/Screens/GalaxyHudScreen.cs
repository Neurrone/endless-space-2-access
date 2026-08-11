using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The galaxy - the page the whole game is played from - made navigable.
    ///
    /// It is not a window. What the player calls "the map" is the galaxy itself with three unrelated
    /// windows drawn over its corners, and no one of them owns the page, so this screen is assembled
    /// from all of them plus the galaxy model underneath. Being ours is therefore not "is a window
    /// up" but "is the game showing the galaxy and nothing over it": a full screen (technology,
    /// diplomacy) or a modal replaces the page and this screen stands down, while a notification or
    /// the tutorial popup merely covers a corner of it and does not - they sit on higher layers, and
    /// a covered screen keeps its cursor, so dismissing one puts the player back where they were.
    ///
    /// Tab moves between the places there are to be, in the order the corner of the screen they come
    /// from reads: what the empire is worth, where the systems are, the quest the game is tracking,
    /// what the game is asking about, and what the turn itself offers.
    ///
    /// There is no separate stop for the fleets. A fleet is not somewhere else on the screen: it is
    /// drawn AT a system or ON a lane, and that is where it is walked - as a child of the place it is
    /// standing, after that place's planets and starlanes. A list of every fleet in the empire, in a
    /// corner of its own, described a picture the map does not draw and made "where is it" a question
    /// the player had to answer from a sentence rather than from the tree they were already in. This
    /// is a DELIBERATE deviation from the game's own arrangement, approved as such: the map draws
    /// fleets as lozenges anchored to places, and the tree now says the same thing.
    ///
    /// A tutorial the player has collapsed is one of those places. Collapsing it hands the keyboard
    /// back to this page, and the bar the game leaves at the top of the right-hand edge - its title,
    /// its close button, the arrow that brings it back - is declared here, above the notification icons
    /// drawn under it, by the tutorial screen that knows how to read it.
    ///
    /// A stop exists on a frame only if the game is showing something at it. There are no placeholders:
    /// where the game draws nothing for having no fleets and nothing for having no notifications, Tab
    /// finds nothing there either, because a stop that says "nothing" is a stop the player has to walk
    /// past to learn what a glance at the screen would have told them.
    ///
    /// Everything is read from the game's own model rather than from the labels on the banners. Every
    /// number up there is animated - the dust total counts up to its new value over a second or so -
    /// so the labels are a picture of a number on its way somewhere, and the model is the number.
    ///
    /// The empire stop is laid out in the ROWS the cluster is drawn in - the strip of screen icons, the
    /// running totals under it, the research line, the stockpiles - and up and down move between them
    /// while left and right walk along one. The rows are measured rather than declared, so a stockpile
    /// strip the engine wraps onto a second line is walked as two lines without anything being told.
    ///
    /// The totals and the stockpiles are readable but not activatable: they are the only way a keyboard
    /// player learns what the empire is worth, and the game's own click handlers on them do nothing
    /// outside its debug mode. Focus still makes the game draw their tooltips, which is where the stat
    /// block behind each number lives - and where the review buffer reads it from.
    ///
    /// The screen icons ARE activatable, and none of the screens they open is navigable yet: opening
    /// one stands this screen down and leaves the player with the game's own keys until Escape closes
    /// it again. That is a worse place to be than here, and still better than not being told the screen
    /// exists.
    ///
    /// Focusing a system moves the camera to it exactly as the game's own "show me this" routes do, so
    /// that anyone watching the screen is looking at whatever the keyboard is on. Opening a system up -
    /// right arrow, the same key that opens anything else - walks what the map is drawing inside it:
    /// its planets, then the starlanes leaving it, then the fleets standing there. It changes no
    /// distance. How close the camera stands is what ENTER on the system asks for, which is the game's
    /// own left click on it, and it decides how much there is to read: from far off a planet is a
    /// circle with a name and a state, and from as close as the game goes it is a card with its
    /// outputs, its anomalies and everything a fleet could do to it. Going in and getting closer stay
    /// different keys, so neither of those two readings is out of reach.
    ///
    /// Backslash is what the map puts on a right click. On a system with fleets selected it sends them
    /// there; with nothing selected it undoes a zoom the player asked for, exactly as right-clicking
    /// the map does. On a starlane it sends the selection out onto the lane - and onto the lane a
    /// fleet is already flying, which is the game's own way of saying "stop at the next system".
    ///
    /// A starlane says where it goes only when the map draws the name of the system at the other end.
    /// The game's own galaxy model will happily hand over the name of a system nobody has ever seen -
    /// asking a node for its neighbours returns them whether or not they have been discovered - so
    /// every name spoken here is gated on the same question the map asks before it draws one.
    ///
    /// The new turn is announced without anyone being on the End Turn button, because the turn
    /// changing is the one thing on this page that happens to the player rather than being done by
    /// them.
    /// </summary>
    public sealed class GalaxyHudScreen : Screen
    {
        private static readonly object SystemStop = "galaxy:systems";

        /// <summary>The clusters the game draws over every view level - what the empire is worth, the
        /// notifications, a collapsed tutorial, the turn controls. This page is one of three that
        /// declare them.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

        /// <summary>The strip the game slides over the bottom of the map while a fleet is selected.
        /// It is drawn OVER this page rather than instead of it, so it contributes stops here rather
        /// than being a page of its own - selecting a fleet is how a player starts sending one
        /// somewhere, and the somewhere is on this map.</summary>
        private readonly FleetPanel _fleetPanel = new FleetPanel();

        // Regions - what Alt and an arrow jump between - are declared only where a stop really has
        // two halves. A stop with one region swallows the key and moves nothing, which reads as the
        // key being broken rather than as there being nowhere else to go. The empire stop declares
        // none: its parts are its rows, and up and down already walk them.
        private static readonly object OwnedSystemsRegion = "galaxy:systems/owned";
        private static readonly object OtherSystemsRegion = "galaxy:systems/other";

        /// <summary>How far up a parent chain to look before deciding it is not a chain.</summary>
        private const int MaxAncestors = 64;

        // Reused across builds rather than allocated per frame: the galaxy is walked whole to work
        // out which systems the player can see, and Build runs every tick.
        private readonly List<StarSystemNode> _owned = new List<StarSystemNode>();
        private readonly List<StarSystemNode> _other = new List<StarSystemNode>();

        public override string Key
        {
            get { return "screen.galaxy"; }
        }

        /// <summary>Above nothing - it is the bottom of the game - and below everything drawn over it:
        /// the tutorial popup, the notifications, the confirmation box.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        /// <summary>The game has no caption for the galaxy; it is simply where you are when nothing
        /// else is open.</summary>
        public override string ScreenName
        {
            get { return ModStrings.Get(ModStrings.ScreenGalaxy); }
        }

        /// <summary>The page the player returns to after every screen, so coming back puts them on the
        /// control they left rather than at the top.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>The first of the stops, because Tab walks them forwards and stops at the end rather
        /// than coming round again: a cursor seated at the last stop would make the key look broken on
        /// the one screen a player arrives at before they have learnt anything else. It is also what the
        /// player wants read out on arriving - what the empire is worth.</summary>
        public override object InitialFocusStop
        {
            get { return GlobalHud.EmpireStop; }
        }

        /// <summary>
        /// Ours while the game is showing the galaxy from above and nothing has replaced it. "Normal
        /// view" is the game's own name for half of that: a game that is ready, not in a battle, not in
        /// the scan overlay, not watching a system be discovered or a planet destroyed. A full screen
        /// or a modal on top replaces the page rather than covering it, so those stand this screen
        /// down; a loading window means the page is on its way out.
        ///
        /// The other half is the view level. Taking the camera into a system's management page leaves
        /// "normal view" true while putting a completely different set of things in front of the
        /// player, so this page has to give the keyboard up there rather than go on describing systems
        /// that are no longer on the screen.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                GuiManager gui = GuiService();
                return gui != null
                    && gui.IsInNormalView
                    && GalaxyViewLevels.Overview
                    && !gui.IsAnyScreenVisible
                    && !gui.IsAnyModalVisible
                    && !gui.IsInLoadingWindow;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape belongs to the game: from the galaxy it raises the pause menu, and that is
        /// the route every other way out of a game goes through.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void OnPush()
        {
            _hud.Baseline();
            _fleetPanel.Baseline();
        }

        public override void OnPop()
        {
            _hud.Forget();
            _fleetPanel.Forget();
        }

        public override void OnUpdate()
        {
            _hud.Update();
            _fleetPanel.Update();
        }

        /// <summary>Down the screen, which is also the order the galaxy screen has always read in:
        /// the empire's banners across the top and the name of the view beside them, then what the map
        /// is showing, then the right-hand
        /// edge - the bar a collapsed tutorial leaves at the top of it and the notification icons
        /// under that - and the turn controls in the bottom corner.</summary>
        public override void Build(GraphBuilder builder)
        {
            ApplyPendingExpansions(builder);
            _hud.Top(builder);

            builder.BeginStop(SystemStop);
            BuildSystems(builder);

            // The selected-fleet panel, where the game draws it: over the bottom of the map, between
            // what the map shows and the clusters down its right-hand edge. Nothing at all while no
            // fleet is selected.
            _fleetPanel.Build(builder);

            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.Turn(builder);
        }

        /// <summary>
        /// Typing on the map looks through the systems AND every fleet the map is drawing, wherever
        /// each is buried.
        ///
        /// A fleet lives under the place it is standing now, and that place is usually closed - so the
        /// only thing the ordinary scope (the stop's declared controls) could find is a fleet whose
        /// system the player had already opened, which is not a search, it is a confirmation. Landing
        /// on one opens the place it is in, so the branch the player is put into is the branch they can
        /// then walk. The opening is recorded rather than done: the graph is rebuilt between this call
        /// and the focus landing, and the expansion set belongs to that rebuild.
        ///
        /// Everything the stop already declares stays searchable - this EXTENDS the ordinary scope
        /// rather than replacing it, so a planet or a starlane of an open system is still found by
        /// name.
        /// </summary>
        public override SearchScope TypeAheadScope(GraphNode focused, GraphRender render)
        {
            if (focused == null || !Equals(focused.StopKey, SystemStop))
            {
                return null;
            }

            // Only the fleets the stop has NOT already declared: an open system declares its own, and a
            // fleet offered twice would be two results with one name, which stepping the matches walks
            // through twice.
            List<FleetSite> sites = FleetIndex(Declared(render));
            if (sites.Count == 0)
            {
                return null;
            }

            SearchScope basis = SearchScope.OverStop(render, SystemStop);
            int already = basis.Count;
            List<FleetSite> found = sites;
            GalaxyHudScreen screen = this;
            return new SearchScope(
                already + found.Count,
                index =>
                    index < already
                        ? basis.TextOf(index)
                        : found[index - already].Fleet.LocalizedName,
                index =>
                    index < already ? basis.Land(index) : screen.Reveal(found[index - already])
            );
        }

        /// <summary>One fleet and the branches that have to be open before it is a node: the system it
        /// is parked at or whose lane it is flying, and - for a lane - the lane itself.</summary>
        private struct FleetSite
        {
            public Fleet Fleet;
            public ControlId System;
            public ControlId Lane;
            public string Key;
        }

        /// <summary>Every fleet the map is drawing and the place each is drawn at, minus the ones the
        /// graph already holds. Built on demand: both repositories are walked once per lane, and the
        /// only thing that ever wants this is one keystroke.</summary>
        private List<FleetSite> FleetIndex(HashSet<ControlId> declared)
        {
            List<FleetSite> sites = new List<FleetSite>();
            try
            {
                for (int i = 0; i < _owned.Count; i++)
                {
                    IndexPlace(_owned[i], sites, declared);
                }

                for (int i = 0; i < _other.Count; i++)
                {
                    IndexPlace(_other[i], sites, declared);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: indexing the fleets for a search threw: " + e);
            }

            return sites;
        }

        /// <summary>What the graph is holding right now, so that nothing is offered to a search twice.
        /// </summary>
        private static HashSet<ControlId> Declared(GraphRender render)
        {
            HashSet<ControlId> ids = new HashSet<ControlId>();
            if (render != null)
            {
                foreach (GraphNode node in render.Order)
                {
                    ids.Add(node.Id);
                }
            }

            return ids;
        }

        /// <summary>The fleets one system holds, and the ones out on each of its lanes. A lane is
        /// reached from both of its ends, so a fleet on one is indexed twice - which costs a duplicate
        /// result and is the right way round: either end is a true answer to "where is it", and
        /// dropping one would make the answer depend on which system the search happened to reach
        /// first.</summary>
        private static void IndexPlace(
            StarSystemNode node,
            List<FleetSite> sites,
            HashSet<ControlId> declared
        )
        {
            string systemKey = "galaxy:system/" + node.GUID;
            ControlId system = ControlId.Referenced(node, systemKey);
            Index(FleetPresence.FleetsAt(node), system, null, systemKey, sites, declared);
            for (int i = 0; i < node.Links.Count; i++)
            {
                Link link = node.Links[i];
                IList<Fleet> flying = FleetPresence.FleetsOn(link);
                if (flying.Count == 0)
                {
                    continue;
                }

                string laneKey = systemKey + "/lane/" + link.GUID;
                Index(
                    flying,
                    system,
                    ControlId.Referenced(link, laneKey),
                    laneKey,
                    sites,
                    declared
                );
            }
        }

        private static void Index(
            IList<Fleet> fleets,
            ControlId system,
            ControlId lane,
            string key,
            List<FleetSite> sites,
            HashSet<ControlId> declared
        )
        {
            for (int i = 0; i < fleets.Count; i++)
            {
                ControlId id = ControlId.Structural(key + "/fleet/" + fleets[i].GUID);
                if (declared.Contains(id))
                {
                    continue;
                }

                sites.Add(
                    new FleetSite
                    {
                        Fleet = fleets[i],
                        System = system,
                        Lane = lane,
                        Key = key,
                    }
                );
            }
        }

        /// <summary>Open the place a fleet is standing in and answer with the fleet itself. The opening
        /// is recorded rather than done: the expansion set belongs to the next rebuild.</summary>
        private ControlId Reveal(FleetSite site)
        {
            _pendingExpand.Add(site.System);
            if (site.Lane != null)
            {
                _pendingExpand.Add(site.Lane);
            }

            return ControlId.Structural(site.Key + "/fleet/" + site.Fleet.GUID);
        }

        /// <summary>Open the branches a search landed in. The expansion set is the engine's, and this is
        /// the one moment a screen has anything to say about it.</summary>
        private void ApplyPendingExpansions(GraphBuilder builder)
        {
            if (_pendingExpand.Count == 0)
            {
                return;
            }

            HashSet<ControlId> expansion = builder.Expansion;
            if (expansion != null)
            {
                for (int i = 0; i < _pendingExpand.Count; i++)
                {
                    expansion.Add(_pendingExpand[i]);
                }
            }

            _pendingExpand.Clear();
        }

        /// <summary>The groups a search has asked to be opened, applied on the next build.</summary>
        private readonly List<ControlId> _pendingExpand = new List<ControlId>();

        // ---- systems ----

        /// <summary>
        /// The star systems the player can see, their own colonies first.
        ///
        /// Which ones those are is the same question the map asks when it decides whether to draw a
        /// system's name: explored at least once, and either remembered or currently in sight. Asking
        /// it the same way is what keeps this list and the map showing the same galaxy.
        ///
        /// The two halves are declared as regions to jump between only while there really are two of
        /// them. On turn one there is one colony and nothing else has been seen yet, and a lone region
        /// makes Alt and an arrow swallow the key and move nothing - which sounds like the key being
        /// broken rather than like there being nowhere else to go.
        /// </summary>
        private void BuildSystems(GraphBuilder builder)
        {
            try
            {
                Empire empire = PlayerEmpire();
                Galaxy galaxy = Gui.Game == null ? null : Gui.Game.Galaxy;
                if (empire == null || galaxy == null)
                {
                    return;
                }

                _owned.Clear();
                _other.Clear();
                DepartmentOfTheInterior interior = empire.GetAgency<DepartmentOfTheInterior>();
                if (interior != null)
                {
                    foreach (ColonizedStarSystem colony in interior.ColonizedStarSystems)
                    {
                        // An empire can hold more than one thing in the same system - a colony and a
                        // ghost of it - and the system is still one place on the map.
                        if (colony.Node != null && !_owned.Contains(colony.Node))
                        {
                            _owned.Add(colony.Node);
                        }
                    }
                }

                foreach (StarSystemNode node in galaxy.StarSystemNodes)
                {
                    if (!_owned.Contains(node) && Perceived(node, empire))
                    {
                        _other.Add(node);
                    }
                }

                bool split = _owned.Count > 0 && _other.Count > 0;
                if (split)
                {
                    builder.SetRegion(OwnedSystemsRegion);
                }

                // Fetched once for the whole stop rather than once per system: the labels the map
                // draws are pooled by the window, not rebuilt per frame, so one walk of the label
                // window serves every system this build declares.
                StarSystemLabel[] labels = SystemLabels();

                for (int i = 0; i < _owned.Count; i++)
                {
                    AddSystem(builder, _owned[i], empire, true, labels);
                }

                if (split)
                {
                    builder.SetRegion(OtherSystemsRegion);
                }

                for (int i = 0; i < _other.Count; i++)
                {
                    AddSystem(builder, _other[i], empire, false, labels);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the systems threw: " + e);
            }
        }

        /// <summary>The map's own rule for whether a node's name is drawn: it has been explored, and
        /// it is either remembered or in sight now. Everything this screen ever says the name of is
        /// asked this first.</summary>
        private static bool Perceived(GameNode node, Empire empire)
        {
            try
            {
                if ((int)node.Exploration[empire] < 2)
                {
                    return false;
                }

                EntityVisibility.Layer layer = node.Visibility[empire];
                return layer == EntityVisibility.Layer.Known || (int)layer >= 3;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// One system on the map: what it is called, whether it is yours, and - once opened - what the
        /// map draws inside its label.
        ///
        /// Enter is the game's own left click on a system: it brings the camera all the way in, to the
        /// step at which the map stops drawing circles and draws a card in orbit for every planet.
        /// Backslash is the right click: with fleets selected it sends them here, and with none it puts
        /// the camera back where the zoom took it from.
        ///
        /// The page a colony of yours has of its own is on neither key. The map draws a button for it
        /// on the system's own label, beside the name, and that button is a node here - so the player
        /// reaches it the way a mouse does, by going to the thing that opens it.
        /// </summary>
        private void AddSystem(
            GraphBuilder builder,
            StarSystemNode node,
            Empire empire,
            bool owned,
            StarSystemLabel[] labels
        )
        {
            StarSystemNode it = node;
            StarSystemLabel label = LabelFor(node, labels);
            AgeTooltip tooltip = label == null ? null : label.StarTooltip;
            NodeVtable vtable = GraphNodes.Group(
                () => it.LocalizedName,
                null,
                tooltip,
                null,
                () => FleetPresence.LinesAt(it)
            );
            if (owned)
            {
                // A system of yours is either a colony or still an OUTPOST, and the map draws the two
                // differently - so they say different words rather than both saying "colonized".
                Empire owner = empire;
                vtable.Announcements.Add(GraphNodes.ValuePart(() => OwnedState(it, owner)));
            }

            // What the map draws parked here, in the game's own count phrase. Not watched: the answer
            // costs a walk of the docking-slot repository, and a watched part walks it every frame the
            // system is focused.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetPresence.At(it), false));

            // The two clicks the map itself puts on a system, and nothing invented on top of them.
            vtable.OnActivate = () => ZoomIn(it);
            vtable.OnContextual = () => SystemCommand(it);

            // The camera goes where the cursor goes, so that whoever is watching the screen is looking
            // at the system being read out. On the galaxy this only slides the camera across; it does
            // not undo the zoom a system that has been opened up asked for.
            //
            // Once the camera is all the way in, the map pushes the system's own label off the top of
            // the screen and draws a tooltip anchor on the star instead - so that is what the pointer
            // is put on, or a tooltip meant for the system would be drawn where nobody can see it.
            AgeTransform anchor = label == null ? null : label.AgeTransform;
            AgeTooltip tip = tooltip;
            vtable.OnFocusVisual = () =>
            {
                GalaxyViewLevels.PanTo(it);
                AgeTooltip star = OrbitalStarTooltip(it);
                if (star != null)
                {
                    PointerFocus.MoveTo(null, star, star.AgeTransform);
                }
                else if (anchor != null)
                {
                    PointerFocus.MoveTo(null, tip, anchor);
                }
            };
            vtable.OnBlurVisual = ReleasePointer;

            // Opening a system up moves NOTHING. Right means "tell me what is inside this", and what
            // is inside it is whatever the map is drawing at the distance the player has chosen: the
            // circles when the camera is out, the orbital cards when it is in. Making the key drag the
            // camera to the closest step took that choice away - and with it every readout that only
            // exists while the camera is out - so how close the camera stands is Enter's business, and
            // the engine keeps its own record of what is open.
            ControlId id = ControlId.Referenced(it, "galaxy:system/" + it.GUID);
            builder.BeginGroup(id, vtable);
            // Only what is open costs anything: a galaxy of closed systems declares one node each.
            if (builder.IsExpanded(id))
            {
                AddManagementView(builder, node, label);
                AddPlanets(builder, node, empire, label);
                AddStarlanes(builder, node, empire);
                AddFleets(builder, "galaxy:system/" + node.GUID, FleetPresence.FleetsAt(node));
                AddHangars(builder, "galaxy:system/" + node.GUID, node);
            }

            builder.EndGroup();
        }

        /// <summary>What a system of the player's IS - taken from the state the game paints its label
        /// from, so the word and the picture always agree. Anything other than an outpost is the colony
        /// the word "colonized" has always meant.</summary>
        private static string OwnedState(StarSystemNode node, Empire empire)
        {
            try
            {
                return ModStrings.Get(
                    IsOutpost(node, empire)
                        ? ModStrings.GalaxySystemOutpost
                        : ModStrings.GalaxySystemColonized
                );
            }
            catch (Exception)
            {
                return ModStrings.Get(ModStrings.GalaxySystemColonized);
            }
        }

        /// <summary>The word for what this empire holds at a place, or nothing at all where it holds
        /// nothing - so that a system named anywhere in the tree reads the same way it reads at the root
        /// of the systems stop, and the word cannot drift between the two places that say it.</summary>
        private static string StateOf(GameNode node, Empire empire)
        {
            StarSystemNode system = node as StarSystemNode;
            return system != null && Held(system, empire) ? OwnedState(system, empire) : null;
        }

        /// <summary>Whether this empire holds anything at all here - the same membership test the owned
        /// half of the systems stop is built from (<see cref="BuildSystems"/>), a GHOST of a colony
        /// included, because that is what the map's label is counting.</summary>
        private static bool Held(GameNode node, Empire empire)
        {
            DepartmentOfTheInterior interior =
                empire == null ? null : empire.GetAgency<DepartmentOfTheInterior>();
            IList<ColonizedStarSystem> systems =
                interior == null ? null : interior.ColonizedStarSystems;
            for (int i = 0; systems != null && i < systems.Count; i++)
            {
                if (systems[i] != null && systems[i].Node == node)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether what this empire holds here is still an outpost. Read off the same list the
        /// stop was built from - an empire can hold a colony and a GHOST of one in the same place, and
        /// the ghost is not what the map's label is showing.</summary>
        private static bool IsOutpost(StarSystemNode node, Empire empire)
        {
            DepartmentOfTheInterior interior =
                empire == null ? null : empire.GetAgency<DepartmentOfTheInterior>();
            if (interior == null)
            {
                return false;
            }

            IList<ColonizedStarSystem> systems = interior.ColonizedStarSystems;
            for (int i = 0; systems != null && i < systems.Count; i++)
            {
                ColonizedStarSystem system = systems[i];
                if (system != null && system.Node == node && system.State != StarSystemState.Ghost)
                {
                    return system.State == StarSystemState.Outpost;
                }
            }

            return false;
        }

        /// <summary>The game's own left click on a system: the camera comes all the way in, which is
        /// also what swaps the system's planets from circles to cards. Said as well as done - the key
        /// changes the whole screen and what the tree underneath reads, and a player who cannot see the
        /// camera move has nothing else to go on. What it says is what the CAMERA did, not what a menu
        /// used to be called: "Open system" is the system's own page, a different place entirely, and
        /// the two must not sound alike.
        ///
        /// Takes any node on the map rather than a system, because the far end of a starlane is offered
        /// as a node of its own (<see cref="AddDestination"/>) and its Enter has to be this exact click
        /// rather than a second copy of it.</summary>
        private static void ZoomIn(GameNode node)
        {
            GalaxyViewLevels.ZoomTo(node);
            Voice.Say(ModStrings.Get(ModStrings.GalaxyZoomedIn), true);
        }

        /// <summary>
        /// The map's own right click on a system, which is two things and never both: while the cursor
        /// is holding fleets it is where they are being sent, and while it is holding nothing it is the
        /// way back out of a zoom.
        ///
        /// Asked only when the key is pressed. Working out whether a fleet could get here is a
        /// pathfinding search per fleet, which is a thing to do on demand and never on a frame.
        /// </summary>
        private static void SystemCommand(StarSystemNode node)
        {
            List<Fleet> selected = FleetOrders.Selected();
            if (selected.Count > 0)
            {
                SendAll(SendableTo(node, selected));
                return;
            }

            // NOT the game's RestoreZoom, for the reason ZoomToStep's own doc comment records: the
            // game restores the camera to wherever it stood BEFORE the click-zoom, which for a
            // keyboard player is somewhere they have since navigated away from - and its
            // hasZoomBeenForced gate makes it a talking no-op for a camera that reached orbital
            // zoom any other way (the mouse wheel, a restore by step number). The keyboard's way
            // out is the default view at the FOCUSED system, always.
            if (GalaxyViewLevels.ZoomStep > GalaxyViewLevels.DefaultZoomStep)
            {
                GalaxyViewLevels.ZoomToStep(node, GalaxyViewLevels.DefaultZoomStep);
                Voice.Say(ModStrings.Get(ModStrings.GalaxyZoomedOut), true);
            }

            // Nothing selected, nothing to unzoom: silent, like every other gesture key with
            // nothing to do here.
        }

        /// <summary>The button the map draws on a colony's own label, beside its name - the one route
        /// into the system's page, and the one the mouse takes. Declared only while the game is drawing
        /// it and willing to act on it, which is its own answer to "is this a colony of mine".</summary>
        private static void AddManagementView(
            GraphBuilder builder,
            StarSystemNode node,
            StarSystemLabel label
        )
        {
            AgeTransform button = label == null ? null : label.RequestManagementViewButton;
            if (button == null || !Visible(button) || !AgeWidgets.Operable(button))
            {
                return;
            }

            AgeTransform it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(ModStrings.GalaxyOpenSystem),
                () => AgeWidgets.Press(it),
                null,
                Raw(it)
            );
            PointAt(vtable, it);
            builder.AddItem(
                ControlId.Structural("galaxy:system/" + node.GUID + "/management"),
                vtable
            );
        }

        // ---- sending the selected fleets somewhere ----
        //
        // Moving a fleet is a DRAG in this game: you pick a fleet up and drop it on a place. A drag has
        // no keyboard equivalent, so the two halves are separated - Enter on a fleet picks it up
        // (selects it), and BACKSLASH on the destination drops it, because backslash is what the map
        // itself puts a move on. Which is also the game's own model rather than a mod invention:
        // selecting a fleet changes the cursor and nothing else, the map stays live underneath, and
        // naming a destination moves everything the cursor is holding
        // (`GalaxyGarrisonCursor.GetFleetsToMove`). One fleet or five, what is spoken back says which.
        //
        // Whether anything can go at all is the pathfinder's own answer, asked when the key is pressed -
        // never per frame, and never guessed at from the map.

        /// <summary>One selected fleet and the route that would take it to the destination the key was
        /// pressed on.</summary>
        private sealed class Sendable
        {
            public Sendable(Fleet fleet, GalaxyPath path)
            {
                Fleet = fleet;
                Path = path;
            }

            public readonly Fleet Fleet;

            public readonly GalaxyPath Path;
        }

        /// <summary>Which of the selected fleets could be sent to a system. A fleet already parked
        /// there is not one of them: the game accepts the order and then does nothing about it, so
        /// counting it would turn the key's answer into a report of something that did not happen.
        /// </summary>
        private static List<Sendable> SendableTo(GameNode node, List<Fleet> fleets)
        {
            List<Sendable> found = new List<Sendable>();
            for (int i = 0; i < fleets.Count; i++)
            {
                GameNode orbit = FleetOrders.Orbit(fleets[i]);
                if (orbit != null && orbit.GUID == node.GUID)
                {
                    continue;
                }

                AddSendable(found, fleets[i], FleetOrders.PathTo(fleets[i], node));
            }

            return found;
        }

        private static List<Sendable> SendableTo(Link link, List<Fleet> fleets)
        {
            List<Sendable> found = new List<Sendable>();
            for (int i = 0; i < fleets.Count; i++)
            {
                AddSendable(found, fleets[i], FleetOrders.PathToLink(fleets[i], link));
            }

            return found;
        }

        private static void AddSendable(List<Sendable> found, Fleet fleet, GalaxyPath path)
        {
            if (path != null && FleetOrders.CanSend(fleet, path))
            {
                found.Add(new Sendable(fleet, path));
            }
        }

        /// <summary>
        /// Post one move per fleet, along the route worked out when the key was pressed - which is how
        /// the game itself does a multi-fleet drag: one order each, and each one checked again as it is
        /// posted.
        ///
        /// What went is said back, named after what would actually go: the fleet by name while there is
        /// one, and how many while there are several. Where nothing could get there the key is SILENT -
        /// the same answer as a control with no such command at all, because a refused order and an
        /// absent one are the same to the player, and this key is pressed speculatively all over the
        /// map.
        /// </summary>
        private static void SendAll(List<Sendable> sendable)
        {
            if (sendable.Count == 0)
            {
                return;
            }

            for (int i = 0; i < sendable.Count; i++)
            {
                FleetOrders.Send(sendable[i].Fleet, sendable[i].Path);
            }

            Voice.Say(
                sendable.Count == 1
                    ? ModStrings.Format(
                        ModStrings.GalaxySendFleet,
                        sendable[0].Fleet.LocalizedName
                    )
                    : ModStrings.Format(ModStrings.GalaxySendFleets, sendable.Count),
                true
            );
        }

        /// <summary>
        /// The planets the map draws inside a system's label, in the order it draws them.
        ///
        /// Which planets those are is the label's own question, asked the same way: the system has been
        /// identified, and this empire is allowed to see its planets at all. Until the system has been
        /// properly surveyed the game draws grey circles and names nothing, and neither does this - the
        /// planets are in the galaxy model the whole time, and reading their names off it would tell
        /// the player things the map is deliberately not showing them.
        ///
        /// Opening a system brings the camera all the way in, and at that distance the map stops
        /// drawing circles and draws a CARD in orbit for each planet - its name, what kind of world it
        /// is, whether it can be colonized and why not, its outputs, its anomalies, and the buttons for
        /// everything a fleet in the system could do to it. That card is what a sighted player browses
        /// a system with, so where one is drawn it is what a planet here reads from and what its own
        /// buttons hang off. Where one is not - the camera is somewhere else, or has not arrived yet -
        /// the planet falls back to the model's own thin answer rather than going silent.
        /// </summary>
        private static void AddPlanets(
            GraphBuilder builder,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            try
            {
                if ((int)node.Exploration[empire] < 2 || !node.PlanetsVisibility[empire.Index])
                {
                    return;
                }

                AgeTransform table = label == null ? null : label.PlanetCirclesTable;
                PlanetLabel_SystemOrbital[] cards = OrbitalLabels(node);
                for (int i = 0; i < node.Planets.Count; i++)
                {
                    StarSystemNode system = node;
                    Planet planet = node.Planets[i];
                    Empire looking = empire;
                    PlanetLabel_SystemOrbital card = CardFor(planet, cards);
                    string key = "galaxy:system/" + node.GUID + "/planet/" + i;
                    ControlId id = ControlId.Referenced(planet, key);
                    if (card != null)
                    {
                        // The card carries a row of buttons the game draws under it, so where the game
                        // is drawing any the planet is a level of the tree rather than a leaf: it reads
                        // as itself, and what could be done to it is one step in. Enter on the card is
                        // the card's own click - the planet's page - and nothing else, because
                        // everything else the old menu held is now drawn where the game draws it.
                        List<CardActions.CardAction> actions = OrbitalActions(card);
                        NodeVtable readout = OrbitalReadout(card);
                        if (actions.Count == 0)
                        {
                            builder.AddItem(id, readout);
                            continue;
                        }

                        readout.ControlType = ControlTypes.Group;
                        builder.BeginGroup(id, readout);
                        if (builder.IsExpanded(id))
                        {
                            CardActions.Emit(builder, key, actions);
                        }

                        builder.EndGroup();
                        continue;
                    }

                    // The circle is what the player would hover to get the planet's panel; without one
                    // the planet is still on the map, just with nothing to show under the pointer.
                    AgeTransform circle = Circle(table, i);
                    NodeVtable vtable = GraphNodes.Readout(
                        () => PlanetName(system, planet, looking),
                        () => PlanetStatus(system, planet, looking),
                        null,
                        Raw(circle)
                    );
                    if (circle != null)
                    {
                        PointAt(vtable, circle);
                    }

                    builder.AddItem(id, vtable);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's planets threw: " + e);
            }
        }

        // ---- the orbital cards ----

        private static readonly PlanetLabel_SystemOrbital[] NoCards =
            new PlanetLabel_SystemOrbital[0];

        /// <summary>The orbital cards the map is drawing right now. It draws them for ONE system - the
        /// one the camera has come in on - so a system anywhere else on the map gets none, and asking
        /// for another system's cards while this one's are up would hand out the wrong planets'
        /// widgets.</summary>
        private static PlanetLabel_SystemOrbital[] OrbitalLabels(StarSystemNode node)
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = OrbitalWindow();
                if (window == null || !ReferenceEquals(GalaxyViewLevels.FocusedSystem, node))
                {
                    return NoCards;
                }

                return window.GetComponentsInChildren<PlanetLabel_SystemOrbital>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the orbital cards threw: " + e);
                return NoCards;
            }
        }

        private static PlanetLabel_SystemOrbital CardFor(
            Planet planet,
            PlanetLabel_SystemOrbital[] cards
        )
        {
            try
            {
                for (int i = 0; i < cards.Length; i++)
                {
                    PlanetLabel_SystemOrbital card = cards[i];
                    if (
                        card != null
                        && ReferenceEquals(card.Planet, planet)
                        && Visible(card.AgeTransform)
                    )
                    {
                        return card;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>The tooltip the orbital window draws on a system's star, which it keeps parked over
        /// the star wherever the star is on screen. Null unless the camera is in on that system.
        /// </summary>
        private static AgeTooltip OrbitalStarTooltip(StarSystemNode node)
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = OrbitalWindow();
                if (window == null || !ReferenceEquals(GalaxyViewLevels.FocusedSystem, node))
                {
                    return null;
                }

                AgeTooltip star = window.StarTooltip;
                return star != null && star.AgeTransform != null ? star : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static PlanetLabelsWindow_SystemOrbital OrbitalWindow()
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemOrbital>(false)
                    : null;
                return window != null && window.Shown ? window : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// A planet as its orbital card reads it: the three lines the card writes - what it is called,
        /// what kind of world it is, and what the game says about colonizing it - with everything the
        /// card draws as icons and gauges in the review buffer, and its buttons one step in.
        ///
        /// Enter is the card's own click: the planet's page. It is the only thing the card itself does.
        /// </summary>
        private static NodeVtable OrbitalReadout(PlanetLabel_SystemOrbital card)
        {
            PlanetLabel_SystemOrbital it = card;
            AgeTooltip dossier = it.PlanetInfoTooltip;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.PlanetName)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.PlanetSizeAndType)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.ColonizeStatus)),
                    GraphNodes.ValuePart(() => OutpostTimer(it)),
                },
                OnActivate = () => GalaxyViewLevels.OpenPlanet(it.Planet),
            };
            // What the card DRAWS first, then its dossier - the paragraph the game writes about a
            // world of this kind, its size, its type. The dossier is the long panel behind the card,
            // so the readout indicates it and the buffer is where it is read.
            vtable.Sections = GraphNodes.Sections(
                NodeSection.Buffer(() => OrbitalDetails(it)),
                // The timer says a number and nothing else; the sentence the game explains it with is
                // reviewable rather than spoken, because the card already speaks the number and
                // hearing the paragraph again on every pass is what a buffer exists to avoid.
                NodeSection.Buffer(() => OutpostTimerHelp(it)),
                GraphNodes.TooltipSection(dossier)
            );
            PointAt(vtable, it.PlanetOrbitalCardContainer ?? it.AgeTransform);
            return vtable;
        }

        /// <summary>How long an outpost of ours has left before it becomes a colony - drawn on the card
        /// only while there is one.</summary>
        private static string OutpostTimer(PlanetLabel_SystemOrbital card)
        {
            try
            {
                return card.OutpostTimer != null && Visible(card.OutpostTimer.AgeTransform)
                    ? AgeText.Label(card.OutpostTimer)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the game says the outpost timer means - written into the timer's own tooltip
        /// while it is running, and emptied by the card's refresh when it is not.</summary>
        private static IList<string> OutpostTimerHelp(PlanetLabel_SystemOrbital card)
        {
            try
            {
                if (card.OutpostTimer == null || !Visible(card.OutpostTimer.AgeTransform))
                {
                    return null;
                }

                Func<IList<string>> lines = AgeWidgets.TooltipLines(card.OutpostTooltip);
                return lines == null ? null : lines();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What the card shows that its written lines do not carry, and NOTHING ELSE: the outputs it
        /// writes as numbers, the anomalies, curiosities and deposits it draws as icons, and last the
        /// long panel it shows when the card itself is hovered. In the order the card draws them.
        ///
        /// The rule this obeys is that the buffer is the card's FACE. Anything read off the game's
        /// model rather than off the card put words in the player's ear that no one looking at the
        /// screen could see: the five outputs a colony has are drawn as numbers and belong here, and
        /// the same five on a world nobody has settled are drawn as rows of pips standing for a
        /// rating, so reading the simulation's raw values for them described a card that does not
        /// exist. The game's refusal to colonize is not here either - it belongs to the BUTTON the
        /// game is refusing on, which is a child node of this card and carries it in the game's own
        /// words.
        /// </summary>
        private static IList<string> OrbitalDetails(PlanetLabel_SystemOrbital card)
        {
            List<string> lines = new List<string>();
            try
            {
                AddFidsi(lines, card);
                AddAnomalies(lines, card);
                AddWidgetLines(lines, card.PlanetCuriositiesTable);
                AddWidgetLines(lines, card.ResourceDepositsGroup);
                // The dossier is NOT read here: it is the card's tooltip section, declared beside
                // this one, and reading it twice is what happens when two places both remember it.
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card threw: " + e);
            }

            return lines;
        }

        /// <summary>What has been found on the planet. The card draws each anomaly as a coloured icon
        /// with no words on it at all, so the names come from the game's own wrapper for the same
        /// anomaly - the one whose title it writes wherever it does have room. Only while the card is
        /// drawing the row: the planet knows its anomalies whether or not they are on screen.</summary>
        private static void AddAnomalies(List<string> lines, PlanetLabel_SystemOrbital card)
        {
            try
            {
                Planet planet = card.Planet;
                if (planet == null || !Visible(card.PlanetAnomaliesTable))
                {
                    return;
                }

                for (int i = 0; i < planet.Anomalies.Count; i++)
                {
                    Anomaly anomaly = planet.Anomalies[i];
                    AddLine(
                        lines,
                        AgeText.Clean(
                            new GuiAnomaly(anomaly.AnomalyDefinition, planet).Title
                        )
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a planet's anomalies threw: " + e);
            }
        }

        /// <summary>
        /// The colony's five outputs, named by the game's own property titles and read off the same
        /// simulation object the card reads them from.
        ///
        /// Only where the card WRITES them, which is only for a colony: on a world nobody has settled
        /// the card hides this row and draws a table of pips instead - a rating, not a number - and
        /// the numbers behind those pips are a thing the game is deliberately not showing.
        /// </summary>
        private static void AddFidsi(List<string> lines, PlanetLabel_SystemOrbital card)
        {
            try
            {
                FidsiEnumerator fidsi = card.FidsiEnumerator;
                ColonizedPlanet colony = card.ColonizedPlanet;
                if (
                    fidsi == null
                    || fidsi.FidsiProperties == null
                    || colony == null
                    || !Visible(fidsi.AgeTransform)
                )
                {
                    return;
                }

                Amplitude.Unity.Simulation.SimulationObject simulation = colony.SimulationObject;
                if (simulation == null)
                {
                    return;
                }

                int count = Math.Min(fidsi.DisplayedProperties, fidsi.FidsiProperties.Count);
                for (int i = 0; i < count; i++)
                {
                    GuiSimulationProperty property = fidsi.FidsiProperties[i];
                    if (property == null)
                    {
                        continue;
                    }

                    AddLine(
                        lines,
                        new MessageBuilder()
                            .ListItem(AgeText.Clean(Gui.GetLocalizedTitle(property.Name)))
                            .ListItem(Amount(simulation.GetPropertyValue(property.Name), false, 0))
                            .Build()
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card's outputs threw: " + e);
            }
        }

        /// <summary>Which of the card's buttons the game is drawing, in drawn order. Empty for a card
        /// the game is offering nothing on, which is what keeps such a planet a leaf of the tree rather
        /// than a branch that opens onto nothing. The treatment each one gets is
        /// <see cref="CardActions"/>'s, shared with the management page's card.</summary>
        private static List<CardActions.CardAction> OrbitalActions(PlanetLabel_SystemOrbital card)
        {
            List<CardActions.CardAction> found = new List<CardActions.CardAction>(4);
            try
            {
                CardActions.AddNamedByMod(found, card.ColonizeButton, ModStrings.SystemColonize);
                // The two faction-specific ways of settling a world, drawn in place of Colonize for the
                // empires that have them. The game gives them no caption, so they are named by the
                // sentence their own tooltip opens with.
                CardActions.AddNamedByTooltip(found, card.VodyaniHintButton);
                CardActions.AddNamedByTooltip(found, card.UmbralChoirHintButton);
                CardActions.AddNamedByTooltip(found, card.BuyOutpostButton);
                CardActions.AddNamedByTooltip(found, card.MinorFactionButton);

                // The row of small round buttons under the card. The game draws them as bare icons and
                // hangs an assembled stat block on each, so there is no caption and no first line of
                // tooltip to name them by - but the game DOES name every one of them, on the fleet
                // action each carries out, and those are the words a player reading the manual would
                // meet. In the order the card draws them.
                CardActions.AddNamedByGame(found, card.TerraformationButton, "%InitiateTerraformPlanetFleetActionTitle");
                CardActions.AddNamedByGame(found, card.RestorationButton, "%InitiateRestorePlanetFleetActionTitle");
                CardActions.AddNamedByGame(found, card.AnomalyReductionButton, "%InitiateReduceAnomalyFleetActionTitle");
                CardActions.AddNamedByGame(found, card.MiningProbeButton, "%LaunchMiningProbeFleetActionTitle");
                CardActions.AddNamedByGame(found, card.DestroyButton, "%DestroyPlanetFleetActionTitle");

                // What has been found in orbit and not yet looked into. Each one is a button of the
                // card's like any other, drawn in a ring around it rather than in the row, so they come
                // after the row.
                AddCuriosities(found, card);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card's buttons threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// The curiosities the card is drawing - each one a button that starts an expedition on it.
        ///
        /// The game draws one wordless icon per curiosity still to be looked into, keeps it CLICKABLE
        /// while refusing, and writes the reason into its own tooltip
        /// (<c>PlanetCuriosityItem.Refresh</c>: no fleet in orbit, one already queued, not enough
        /// expedition power), which is the treatment <see cref="CardActions.AddRefusable"/> exists for -
        /// what is in orbit and why it cannot be reached yet is exactly what the player opened the card
        /// to ask. The icon has no caption at all, so the name comes off the wrapper the game hangs on
        /// its tooltip, which is where it keeps the words it would have written.
        ///
        /// The table pools its items - a card that has run out of curiosities keeps the widgets and
        /// hides them - so what is DRAWN is the gate, exactly as it is for the card's other buttons.
        /// </summary>
        private static void AddCuriosities(
            List<CardActions.CardAction> found,
            PlanetLabel_SystemOrbital card
        )
        {
            AgeTransform table = card.PlanetCuriositiesTable;
            if (table == null || !Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                if (item != null && Visible(item))
                {
                    CardActions.AddRefusable(found, item, CardActions.TitleOf(item));
                }
            }
        }

        /// <summary>The circle the label draws for the planet at <paramref name="index"/>, or null if
        /// the label has not been given one - the window pools its labels and grows the row as systems
        /// are discovered, so a row can be short of a planet the model already has.</summary>
        private static AgeTransform Circle(AgeTransform table, int index)
        {
            try
            {
                if (table == null || index >= table.Children.Count)
                {
                    return null;
                }

                AgeTransform circle = table.Children[index];
                return circle != null && circle.Visible ? circle : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the map calls this planet. A system the player has not surveyed shows a circle
        /// with no name on it and a panel that says only that the planet is unknown, so that word - the
        /// game's own - is the whole of what a planet in one is called here.</summary>
        private static string PlanetName(StarSystemNode system, Planet planet, Empire empire)
        {
            try
            {
                if (!Surveyed(system, empire))
                {
                    return AgeText.Clean(Gui.Localize("%PlanetStatusUnknownTitle"));
                }

                return AgeText.Clean(new GuiPlanet(planet).Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the circle is saying about the planet by the colour it is drawn in - colonized,
        /// an outpost, inhospitable - in the game's own words for each of those states. Nothing for a
        /// planet in an unsurveyed system: the name has already said it is unknown.</summary>
        private static string PlanetStatus(StarSystemNode system, Planet planet, Empire empire)
        {
            try
            {
                if (!Surveyed(system, empire))
                {
                    return null;
                }

                GuiPlanet.PlanetStatuses status = new GuiPlanet(planet).PlanetStatus;
                return AgeText.Clean(Gui.Localize("%PlanetStatus" + status + "Title"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the game has let this empire see what the planets in a system actually are.
        /// The circles switch from grey unknowns to real planets at the same threshold.</summary>
        private static bool Surveyed(StarSystemNode system, Empire empire)
        {
            return (int)system.Exploration[empire] >= (int)EntityExploration.State.Revealed;
        }

        /// <summary>
        /// The lanes leaving a system, and where each one goes.
        ///
        /// A lane says the name of the system at its far end only when the map draws that name.
        /// Everything else is a lane into the unexplored, which is what the map shows: a line running
        /// off into the dark. The galaxy model would answer either way - it holds every system's name
        /// from the first turn - so the check, not the model, is what keeps this honest.
        ///
        /// A wormhole is a different thing from a starlane and is said to be one. An empire without the
        /// technology to see them is shown none, exactly as the game's own neighbour search skips them.
        ///
        /// The game numbers no lane and the model's own order is whatever order the galaxy was
        /// generated in, so the lanes are walked - and numbered - going clockwise from north, and each
        /// one says the way it leaves. That is the mod's ordering, not the game's: a player who cannot
        /// see the lines needs the same "which one is that" the picture gives everyone else, and a
        /// number that moves between sessions would be worse than none.
        /// </summary>
        private static void AddStarlanes(GraphBuilder builder, StarSystemNode node, Empire empire)
        {
            try
            {
                List<Lane> lanes = new List<Lane>();
                for (int i = 0; i < node.Links.Count; i++)
                {
                    Link link = node.Links[i];
                    bool wormhole = link is WormholeLink;
                    if (wormhole && !empire.HasWormholeTechnology)
                    {
                        continue;
                    }

                    if (link.Exploration[empire] == EntityExploration.State.Unrevealed)
                    {
                        continue;
                    }

                    GameNode far = ReferenceEquals(link.ExtremityNode1, node)
                        ? link.ExtremityNode2
                        : link.ExtremityNode1;
                    Lane lane = new Lane
                    {
                        Link = link,
                        Far = far,
                        Wormhole = wormhole,
                        Bearing = CompassDirections.Bearing(
                            far.GalaxyPosition.X - node.GalaxyPosition.X,
                            far.GalaxyPosition.Y - node.GalaxyPosition.Y
                        ),
                    };
                    lanes.Add(lane);
                }

                lanes.Sort(ClockwiseFromNorth);

                for (int i = 0; i < lanes.Count; i++)
                {
                    Link link = lanes[i].Link;
                    GameNode destination = lanes[i].Far;
                    int number = i + 1;
                    string direction = CompassDirections.KeyForBearing(lanes[i].Bearing);
                    bool named = Perceived(destination, empire);
                    string template = lanes[i].Wormhole
                        ? (named ? ModStrings.GalaxyWormhole : ModStrings.GalaxyWormholeUnexplored)
                        : (named ? ModStrings.GalaxyStarlane : ModStrings.GalaxyStarlaneUnexplored);
                    Func<string> text = named
                        ? (Func<string>)(
                            () =>
                                ModStrings.Format(
                                    template,
                                    number,
                                    destination.LocalizedName,
                                    ModStrings.Get(direction)
                                )
                        )
                        : () => ModStrings.Format(template, number, ModStrings.Get(direction));
                    Link lane = link;
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(text),
                            // Whatever the map is drawing out on this lane, said the way the map's own
                            // lozenge tooltip heads it. Read on focus rather than watched, for the same
                            // reason a system's is.
                            GraphNodes.ValuePart(() => FleetPresence.On(lane), false),
                        },
                        Sections = GraphNodes.Sections(() => FleetPresence.LinesOn(lane), null),
                    };
                    // A lane is a destination in its own right, not just a road to one: the game
                    // accepts a link as a move target and flies the fleet out onto it
                    // (`GalaxyGarrisonCursor.GetGalaxyPathToTargets` resolves either a node or a
                    // link), which is how a fleet is parked between two systems or pointed down a
                    // lane into the dark. So backslash - the map's own move click - sends here too.
                    //
                    // ENTER on a lane is the map's LEFT click on one, and the only thing that click
                    // does is let go of whatever the cursor is holding
                    // (`GalaxyGarrisonCursor.OnCursorClick` :88-95 changes back to the plain cursor for
                    // a click that landed on a link and nothing else). With nothing selected the click
                    // does nothing at all, and so does this - there is no action to invent.
                    Link target = link;
                    vtable.OnActivate = Deselect;
                    vtable.OnContextual = () => LaneCommand(target);

                    string key = "galaxy:system/" + node.GUID + "/lane/" + link.GUID;
                    // Keyed on the pair of GUIDs and NOT carrying the link as a reference: ONE lane runs
                    // between two systems, and once both ends are in the tree the same Link object backs
                    // two nodes - which are one control to the cursor, because reference identity is
                    // followed before the structural key. Measured with the fog lifted: focusing the
                    // Hir end of the Xiu-Hir lane threw the cursor across to the Xiu end on the next
                    // rebuild. Two GUIDs are stable without a reference, so nothing is lost.
                    ControlId id = ControlId.Structural(key);
                    IList<Fleet> flying = FleetPresence.FleetsOn(link);
                    // A lane into the dark with nothing flying it is a leaf: there is neither a fleet
                    // to walk nor a system to name at the far end.
                    if (flying.Count == 0 && !named)
                    {
                        builder.AddItem(id, vtable);
                        continue;
                    }

                    vtable.ControlType = ControlTypes.Group;
                    builder.BeginGroup(id, vtable);
                    if (builder.IsExpanded(id))
                    {
                        AddFleets(builder, key, flying);
                        if (named)
                        {
                            AddDestination(builder, key, destination, empire);
                        }
                    }

                    builder.EndGroup();
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's starlanes threw: " + e);
            }
        }

        /// <summary>One lane leaving a system, with the way it leaves already worked out.</summary>
        private struct Lane
        {
            public Link Link;
            public GameNode Far;
            public bool Wormhole;
            public double Bearing;
        }

        private static readonly Comparison<Lane> ClockwiseFromNorth = delegate(Lane a, Lane b)
        {
            return a.Bearing.CompareTo(b.Bearing);
        };

        /// <summary>
        /// Where a lane goes, as a child of the lane - the place at its far end, offered here because
        /// that is where the player asking "what is down this line" is standing.
        ///
        /// Only where the map draws the far end's name, which is the same question the lane's own
        /// label asked: a lane into the dark has no destination to offer and stays a leaf.
        ///
        /// It says and does what the system's own node at the root of the tree says and does - the same
        /// state word, the same count phrase for what is parked there, the same two clicks: ENTER is the
        /// map's left click on that system (the camera goes in on it, <see cref="ZoomIn"/> - the very
        /// call the root node makes, so the two cannot drift apart) and BACKSLASH sends the selection
        /// all the way there, which is the distinction the game itself draws between a move ordered on a
        /// node and one ordered on a link: the lane means "fly out onto this line", the system at its end
        /// means "go there".
        ///
        /// A LEAF, deliberately: the system it names has lanes of its own, and one of them leads back
        /// here. The tree would have no bottom.
        ///
        /// Keyed structurally and NEVER on the far node: that node is already a node of this screen at
        /// the root of the systems stop, and two nodes sharing a backing object are one control to the
        /// cursor - reference identity is followed before the structural key - so carrying the reference
        /// here would teleport the player out of the lane the moment anything rebuilt (the same trap
        /// <see cref="AddFleets"/> records).
        /// </summary>
        private static void AddDestination(
            GraphBuilder builder,
            string laneKey,
            GameNode destination,
            Empire empire
        )
        {
            GameNode it = destination;
            Empire looking = empire;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () => ModStrings.Format(ModStrings.GalaxyLaneDestination, it.LocalizedName)
                    ),
                    GraphNodes.ValuePart(() => StateOf(it, looking)),
                    // Read on focus rather than watched, for the reason a system's own count phrase is:
                    // the answer costs a walk of the docking-slot repository.
                    GraphNodes.ValuePart(() => FleetPresence.At(it), false),
                },
                Sections = GraphNodes.Sections(() => FleetPresence.LinesAt(it), null),
                OnActivate = () => ZoomIn(it),
                OnContextual = () => SendTo(it),
            };
            builder.AddItem(ControlId.Structural(laneKey + "/destination"), vtable);
        }

        /// <summary>Send the selected fleets to a place on the map, and nothing else. The system's own
        /// node does more than this on the same key (<see cref="SystemCommand"/>): with nothing selected
        /// it undoes a zoom, which from inside a lane would move a camera the player never asked about.
        /// </summary>
        private static void SendTo(GameNode node)
        {
            SendAll(SendableTo(node, FleetOrders.Selected()));
        }

        /// <summary>
        /// Send the selection out onto a lane.
        ///
        /// Onto the lane a fleet is ALREADY flying, the game answers with the route to the next system
        /// on it (<c>GalaxyGarrisonCursor.GetGalaxyPathToLink</c> :352-361, ported in
        /// <c>FleetOrders.PathToLink</c>), which is the game's own way of saying "stop when you get
        /// there" - so a fleet is called off its long journey by asking it to fly the piece of lane it
        /// is on, and there is no cancel to invent.
        /// </summary>
        private static void LaneCommand(Link link)
        {
            List<Fleet> selected = FleetOrders.Selected();
            if (selected.Count == 0)
            {
                return;
            }

            SendAll(SendableTo(link, selected));
        }

        /// <summary>
        /// Let go of whatever the map's cursor is holding, exactly as a click on empty space or on a
        /// starlane does (<c>GalaxyGarrisonCursor.OnCursorClick</c>): the garrison cursor is swapped
        /// back for the plain one, which is also what takes the fleet panel off the screen.
        ///
        /// Nothing is said here. The panel going is what the player is being told about, and the
        /// panel's own watcher says it - one announcement, from the one place that knows.
        /// </summary>
        private static void Deselect()
        {
            try
            {
                Amplitude.Unity.View.ICursorService cursors =
                    Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
                if (cursors != null && Gui.GetCursor() is GalaxyGarrisonCursor)
                {
                    cursors.ChangeCursor(typeof(GalaxyCursor), Gui.GetCursor());
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: letting go of the selection threw: " + e);
            }
        }

        /// <summary>The on-map label carrying this system's tooltip - matched by the node reference
        /// the label was bound to, with the entity's own identity as a fallback for the rare case the
        /// window rebuilt its labels from a copy rather than the same instance this stop is holding.
        /// </summary>
        private static StarSystemLabel LabelFor(StarSystemNode node, StarSystemLabel[] labels)
        {
            try
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    if (ReferenceEquals(labels[i].StarSystemNode, node))
                    {
                        return labels[i];
                    }
                }

                for (int i = 0; i < labels.Length; i++)
                {
                    StarSystemNode candidate = labels[i].StarSystemNode;
                    if (candidate != null && candidate.GUID == node.GUID)
                    {
                        return labels[i];
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a system to its map label threw: " + e);
            }

            return null;
        }

        private static readonly StarSystemLabel[] NoLabels = new StarSystemLabel[0];

        /// <summary>Every label the map is currently drawing for a system - fetched fresh each time
        /// rather than cached across builds, because the window grows this list as the player explores
        /// more of the galaxy and a cache keyed on nothing that changes would go stale exactly when a
        /// newly-discovered system needed its tooltip.</summary>
        private static StarSystemLabel[] SystemLabels()
        {
            try
            {
                StarSystemLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<StarSystemLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoLabels
                    : window.GetComponentsInChildren<StarSystemLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the system labels threw: " + e);
                return NoLabels;
            }
        }

        // ---- fleets ----


        /// <summary>
        /// The fleets standing at one place on the map - what each is made of, where it is, and where
        /// it is going - as children of that place.
        ///
        /// Which fleets those are is never worked out here: they are the ones the map's own lozenge at
        /// this system or on this lane is holding (<see cref="FleetPresence"/>), so a fleet nobody can
        /// see is absent for the same reason it is absent from the picture, and the count the place
        /// announces and the children it opens onto are the same answer read two ways.
        ///
        /// Focus points at whichever label the map is drawing the fleet with, so the game draws the
        /// fleet's own dossier for it exactly as it would for a mouse resting there. Which label that
        /// is is the map's answer, not this screen's: a fleet in orbit is drawn by the DOCK label of
        /// the slot it is sitting in and a fleet under way by its own, and the windows that own them
        /// bind exactly one of the two at a time. A dock label covers the whole slot, so where two
        /// fleets are parked together its tooltip is the pair of them - which is what the game shows a
        /// player hovering there, and reading it as anything narrower would be describing a tooltip
        /// nobody can see.
        ///
        /// Enter SELECTS the fleet, and there is nothing else on it. Calling off a move is not here
        /// either: the game has no cancel gesture on the map, and a fleet is turned round by being sent
        /// somewhere else or stopped at the next system by being sent down the lane it is already on -
        /// both of them backslash on a PLACE, which is where the map itself puts a move. Dropping it is
        /// a deliberate deviation from what this screen used to offer, and it is approved as one.
        /// </summary>
        private static void AddFleets(GraphBuilder builder, string place, IList<Fleet> fleets)
        {
            if (fleets.Count == 0)
            {
                return;
            }

            try
            {
                // Fetched once for the whole place, like the system labels: both windows pool their
                // labels rather than rebuilding them, so one walk of each serves every fleet here.
                DockLabel[] docks = DockLabels();
                FleetLabel[] flying = FleetLabels();

                for (int i = 0; i < fleets.Count; i++)
                {
                    Fleet it = fleets[i];
                    AgeTransform lozenge = FleetLozenge(it, docks, flying);
                    NodeVtable vtable = GraphNodes.Button(
                        () => it.LocalizedName,
                        () => Select(it),
                        null,
                        Raw(lozenge)
                    );
                    vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetText(it)));
                    if (lozenge != null)
                    {
                        PointAt(vtable, lozenge);
                    }

                    // Keyed on the fleet's own identity but NOT carrying the fleet as a reference:
                    // the selected-fleet panel is declared on this same screen, and its fleet line is
                    // keyed on the garrison - which for a fleet is this very object. Two nodes sharing
                    // a backing object are ONE control to the cursor (reference identity is followed
                    // before the structural key), so the panel's line teleported the player straight
                    // back out to the map on the next rebuild. The line is the one that needs the
                    // reference - its widget is a pool slot the game rebinds - and this key is a GUID,
                    // which is stable without one.
                    builder.AddItem(ControlId.Structural(place + "/fleet/" + it.GUID), vtable);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets at a place threw: " + e);
            }
        }

        /// <summary>What a fleet is made of, what it is doing, and how far it can still go this turn.
        /// </summary>
        private static string FleetText(Fleet fleet)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                message.ListItem(ModStrings.Format(ModStrings.GalaxyFleetShips, fleet.ShipsCount));
                message.ListItem(FleetState(fleet));
                if (fleet.IsGuarding)
                {
                    message.ListItem(ModStrings.Get(ModStrings.GalaxyFleetGuarding));
                }

                message.ListItem(
                    ModStrings.Format(
                        ModStrings.GalaxyFleetMovement,
                        Amount(fleet.CurrentMovementPoints, false, 0)
                    )
                );
                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Where the fleet is: at the system it is orbiting, or on its way to the one it is headed for.
        ///
        /// That pair is the game's own question, asked the game's own way - its fleet list draws one of
        /// two icons on exactly this test and writes the name of the same node beside it. Whether the
        /// fleet is MOVING is a different question and the wrong one: a fleet that has spent its
        /// movement half way to somewhere has stopped for the turn and is still on its way, and asking
        /// about movement leaves it describing itself as nowhere at all.
        ///
        /// A node the map has not named is not named here either, exactly as a starlane running into
        /// the dark is not.
        /// </summary>
        private static string FleetState(Fleet fleet)
        {
            Empire empire = PlayerEmpire();
            GameNode orbit = FleetOrders.Orbit(fleet);
            if (orbit != null)
            {
                return empire != null && Perceived(orbit, empire)
                    ? ModStrings.Format(ModStrings.GalaxyFleetDockedAt, orbit.LocalizedName)
                    : ModStrings.Get(ModStrings.GalaxyFleetDocked);
            }

            GameNode heading = FleetOrders.Heading(fleet);
            if (heading == null)
            {
                return ModStrings.Get(ModStrings.GalaxyFleetMoving);
            }

            return empire != null && Perceived(heading, empire)
                ? ModStrings.Format(ModStrings.GalaxyFleetMovingTo, heading.LocalizedName)
                : ModStrings.Get(ModStrings.GalaxyFleetMovingUnexplored);
        }

        /// <summary>
        /// The lozenge the map is drawing this fleet on - the dock label's where the fleet is parked,
        /// its own where it is under way, and null where the map is drawing neither.
        ///
        /// The lozenge rather than the label, because the tooltip that gets DRAWN is the one on
        /// whatever the pointer is over, and both labels carry two: a CenterTooltip of their own and
        /// the lozenge's, filled from the same fleet data. Declaring the label's while the engine draws
        /// the lozenge's leaves the readout saying "has tooltip" over a review buffer that never fills,
        /// because the buffer only reads a drawn tooltip it can recognise as the one it declared.
        /// </summary>
        private static AgeTransform FleetLozenge(
            Fleet fleet,
            DockLabel[] docks,
            FleetLabel[] flying
        )
        {
            try
            {
                for (int i = 0; i < docks.Length; i++)
                {
                    DockLabel dock = docks[i];
                    if (dock.DockingSlot == null || !Visible(dock.AgeTransform))
                    {
                        continue;
                    }

                    ReadOnlyCollection<GalaxyFleet> docked = dock.DockingSlot.GalaxyFleets;
                    for (int j = 0; j < docked.Count; j++)
                    {
                        if (docked[j] != null && docked[j].Fleet.GUID == fleet.GUID)
                        {
                            return Lozenge(dock.FleetLozenge);
                        }
                    }
                }

                for (int i = 0; i < flying.Length; i++)
                {
                    FleetLabel label = flying[i];
                    if (
                        label.GalaxyFleet != null
                        && label.GalaxyFleet.Fleet.GUID == fleet.GUID
                        && Visible(label.AgeTransform)
                    )
                    {
                        return Lozenge(label.FleetLozenge);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a fleet to its map label threw: " + e);
            }

            return null;
        }

        private static readonly DockLabel[] NoDockLabels = new DockLabel[0];

        private static readonly FleetLabel[] NoFleetLabels = new FleetLabel[0];

        private static DockLabel[] DockLabels()
        {
            try
            {
                DockLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<DockLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoDockLabels
                    : window.GetComponentsInChildren<DockLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the dock labels threw: " + e);
                return NoDockLabels;
            }
        }

        private static FleetLabel[] FleetLabels()
        {
            try
            {
                FleetLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<FleetLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoFleetLabels
                    : window.GetComponentsInChildren<FleetLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the fleet labels threw: " + e);
                return NoFleetLabels;
            }
        }

        /// <summary>
        /// The system's own hangar, where the map is drawing one.
        ///
        /// A hangar is the ships a colony is holding at home. The map draws it as a lozenge of its own
        /// beside the fleet lozenges (<c>HangarLabel</c>), and the mod already counts it in what a system
        /// says is parked there - the count phrase comes from the dock label's own group, which includes
        /// it - but it was not a place the player could go, and it is the one route to the button that
        /// turns those ships into a fleet: clicking the lozenge selects the hangar garrison and swaps in
        /// the garrison cursor, which is what puts the fleet panel on the screen with Create on it.
        ///
        /// WHICH hangars are drawn is the label window's own answer, taken from the same repository it
        /// walks (<c>HangarLabelsWindow.ShowAllLabels</c> over
        /// <c>IVisibleGalaxyHangarRepositoryService</c>) with the same test it draws on
        /// (<c>ShowLabel</c>: the hangar is holding ships) - so a hangar nobody can see is absent here
        /// for the reason it is absent from the picture, and no visibility rule is reimplemented.
        ///
        /// Last among a system's children, after the fleets: it is the thing at that system that is not
        /// going anywhere.
        /// </summary>
        private static void AddHangars(GraphBuilder builder, string place, StarSystemNode node)
        {
            try
            {
                IVisibleGalaxyHangarRepositoryService repository =
                    Amplitude.Unity.Framework.Services.GetService<IVisibleGalaxyHangarRepositoryService>();
                if (repository == null)
                {
                    return;
                }

                HangarLabel[] labels = null;
                ReadOnlyCollection<GalaxyHangar> hangars = repository.GalaxyHangars;
                for (int i = 0; i < hangars.Count; i++)
                {
                    GalaxyHangar hangar = hangars[i];
                    Hangar held = hangar == null ? null : hangar.Hangar;
                    if (held == null || held.ShipsCount <= 0 || !IsAt(held, node))
                    {
                        continue;
                    }

                    if (labels == null)
                    {
                        labels = HangarLabels();
                    }

                    GalaxyHangar it = hangar;
                    AgeTransform lozenge = HangarLozenge(hangar, labels);
                    NodeVtable vtable = GraphNodes.Button(
                        () => held.LocalizedName,
                        () => SelectHangar(it),
                        null,
                        Raw(lozenge)
                    );
                    vtable.Announcements.Add(
                        GraphNodes.ValuePart(
                            () => ModStrings.Format(ModStrings.GalaxyFleetShips, held.ShipsCount)
                        )
                    );
                    if (lozenge != null)
                    {
                        PointAt(vtable, lozenge);
                    }

                    builder.AddItem(
                        ControlId.Structural(place + "/hangar/" + held.GUID),
                        vtable
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's hangar threw: " + e);
            }
        }

        /// <summary>Whether a hangar is the one belonging to this system. A hangar hangs off the node
        /// itself rather than off the colony, which is what makes it survive the colony changing hands.
        /// </summary>
        private static bool IsAt(Hangar hangar, StarSystemNode node)
        {
            try
            {
                GameNode at = hangar.GetGameNode();
                return at != null && at.GUID == node.GUID;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static readonly HangarLabel[] NoHangarLabels = new HangarLabel[0];

        private static HangarLabel[] HangarLabels()
        {
            try
            {
                HangarLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<HangarLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoHangarLabels
                    : window.GetComponentsInChildren<HangarLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the hangar labels threw: " + e);
                return NoHangarLabels;
            }
        }

        /// <summary>The lozenge the map is drawing this hangar with - the same choice
        /// <see cref="FleetLozenge"/> makes for a fleet, and for the same reason: the tooltip that gets
        /// DRAWN is the one on whatever the pointer is over, and the label carries two.</summary>
        private static AgeTransform HangarLozenge(GalaxyHangar hangar, HangarLabel[] labels)
        {
            try
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    HangarLabel label = labels[i];
                    if (
                        label != null
                        && ReferenceEquals(label.GalaxyHangar, hangar)
                        && Visible(label.AgeTransform)
                    )
                    {
                        return Lozenge(label.FleetLozenge);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a hangar to its map label threw: " + e);
            }

            return null;
        }

        /// <summary>Select the ships a colony is holding at home, exactly as clicking their lozenge does
        /// (<c>HangarLabel.OnClickCb</c>): the hangar's cursor target becomes the selection and the
        /// garrison cursor takes over, which is what draws the fleet panel and the Create button on it.
        /// Nothing is said here - the panel's own watcher says the panel opened, from the one place that
        /// knows.</summary>
        private static void SelectHangar(GalaxyHangar hangar)
        {
            try
            {
                Amplitude.Unity.View.ICursorService cursors =
                    Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
                if (cursors == null || hangar == null || hangar.CursorTarget == null)
                {
                    return;
                }

                cursors.Select(hangar.CursorTarget);
                cursors.ChangeCursor(typeof(GalaxyGarrisonCursor), hangar);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: selecting a hangar threw: " + e);
            }
        }

        /// <summary>
        /// Take the camera to a fleet and select it.
        ///
        /// A fleet PARKED somewhere goes through the turn window's own routine for exactly that: it
        /// knows to wait for the galaxy view to finish coming back before it hands the fleet to the
        /// cursor, and getting that order wrong leaves the selection on nothing.
        ///
        /// A fleet UNDER WAY cannot go that way, and the game's routine says so by doing nothing at
        /// all: it finds the fleet's docking slot to aim the camera at, and a fleet between two systems
        /// has none, so it falls through to handing the fleet to a window that is not shown - which
        /// stashes it for the next time the window opens (<c>FleetsScreen.SelectIdleFleet</c> :672-682)
        /// and there never is one, because opening that window is what selecting a fleet does. So a
        /// moving fleet is selected the way every other "show me this fleet" in the game does it
        /// (<c>MilitaryScreen</c>, <c>NamedShipInfoPanel</c>): hand its cursor target to the selection,
        /// swap in the garrison cursor, and ask the camera for the fleet - in that order, because the
        /// panel's own visibility is gated on that cursor.
        /// </summary>
        private static void Select(Fleet fleet)
        {
            try
            {
                if (FleetOrders.Orbit(fleet) != null)
                {
                    EndTurnWindow window = TurnWindow();
                    if (window != null && SelectIdleFleet != null)
                    {
                        SelectIdleFleet.Invoke(window, new object[] { fleet });
                        return;
                    }
                }

                SelectOnMap(fleet);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: selecting a fleet threw: " + e);
            }
        }

        /// <summary>Select a fleet wherever it is standing on the map, and take the camera to it.
        /// </summary>
        private static void SelectOnMap(Fleet fleet)
        {
            GalaxyFleet galaxyFleet = OnMap(fleet);
            Amplitude.Unity.View.ICursorService cursors =
                Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.View.ICursorService>();
            if (galaxyFleet == null || galaxyFleet.CursorTarget == null || cursors == null)
            {
                return;
            }

            cursors.Select(galaxyFleet.CursorTarget);
            cursors.ChangeCursor(typeof(GalaxyGarrisonCursor), galaxyFleet);
            Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(fleet);
        }

        /// <summary>The map's own object for a fleet - the thing that carries its cursor target. The
        /// game keeps a repository of the ones it is drawing, which is the same list its other
        /// "show me this fleet" routes look through.</summary>
        private static GalaxyFleet OnMap(Fleet fleet)
        {
            IVisibleGalaxyFleetRepositoryService repository =
                Amplitude.Unity.Framework.Services.GetService<IVisibleGalaxyFleetRepositoryService>();
            if (repository == null || fleet == null)
            {
                return null;
            }

            ReadOnlyCollection<GalaxyFleet> fleets = repository.GalaxyFleets;
            for (int i = 0; i < fleets.Count; i++)
            {
                if (fleets[i] != null && fleets[i].Fleet != null && fleets[i].Fleet.GUID == fleet.GUID)
                {
                    return fleets[i];
                }
            }

            return null;
        }

        // The window keeps its "go to this fleet" routine to itself, and it is the only place the
        // whole recipe - dock slot, camera, view level, cursor, fleet panel - is written down.
        private static readonly MethodInfo SelectIdleFleet = Member("SelectIdleFleet");

        private static MethodInfo Member(string name)
        {
            try
            {
                return typeof(EndTurnWindow).GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new Type[] { typeof(Fleet) },
                    null
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: looking up " + name + " threw: " + e);
                return null;
            }
        }

        // ---- shared ----

        private static string Amount(float value, bool signed, int decimals)
        {
            try
            {
                return Gui.FormatAmount(value, true, Gui.Rounding.Floor, signed, decimals);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Press a control the way the engine presses it: every AGE button carries the object
        /// and the method name its own mouse handler sends to, so replaying that pair runs the
        /// window's own handler with no click that could land on whatever the mouse is over.</summary>
        private static void Press(AgeControlButton button)
        {
            try
            {
                GameObject target = button.OnActivateObject;
                string method = button.OnActivateMethod;
                if (target != null && !string.IsNullOrEmpty(method))
                {
                    target.SendMessage(method, button.gameObject, SendMessageOptions.DontRequireReceiver);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: pressing a control threw: " + e);
            }
        }

        /// <summary>Make the control look hovered while the cursor is on it. The tooltip handed over
        /// here is the widget's own, class-driven or not: a class tooltip has nothing to READ off the
        /// widget but plenty to SHOW, and showing it is the whole point of this hook.</summary>
        private static void Point(NodeVtable vtable, AgeControlButton button)
        {
            AgeControlButton it = button;
            vtable.OnFocusVisual = () =>
                PointerFocus.MoveTo(it, Transform(it).AgeTooltip, it.AgeTransform);
            vtable.OnBlurVisual = ReleasePointer;
        }

        /// <summary>The same for a widget with no button under it: a running total, one of the screen
        /// icons. Nothing lights up - there is nothing there to light - and the tooltip appears, which
        /// for these is the whole of what the pointer was ever for.</summary>
        private static void PointAt(NodeVtable vtable, AgeTransform widget)
        {
            AgeTransform it = widget;
            vtable.OnFocusVisual = () => PointerFocus.MoveTo(it, Raw(it), it);
            vtable.OnBlurVisual = ReleasePointer;
        }

        private static AgeTransform Lozenge(GarrisonsLabelButton button)
        {
            try
            {
                return button == null ? null : button.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly Action ReleasePointer = PointerFocus.Release;

        private static AgeTransform Transform(AgeControl control)
        {
            try
            {
                return control == null ? null : control.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTooltip Tooltip(AgeControl control)
        {
            return Tooltip(Transform(control));
        }

        /// <summary>
        /// A tooltip only if the widget itself has anything to read.
        ///
        /// An AGE tooltip comes in two kinds. One carries its words in its content field. The other
        /// names a CLASS and a target, and the words are assembled by a renderer at the moment the
        /// tooltip is drawn - a stat block laid out from live data, which is worth reading but is not
        /// there to be read from the widget. The banners and the resource strip are all the second
        /// kind, and their content fields still hold whatever the prefab was authored with - the
        /// panel's own name, most of the time. Reading it aloud would announce "StrategicResourceBanner"
        /// under every resource, so a tooltip that names a class has nothing for a caller that wanted
        /// words off the widget. <see cref="TooltipLines"/> is how those are read instead.
        /// </summary>
        private static AgeTooltip Tooltip(AgeTransform transform)
        {
            try
            {
                return Readable(Raw(transform));
            }
            catch (Exception)
            {
                return null;
            }
        }

        // One rule for "are this tooltip's words on the widget", shared with every other screen and
        // with the mode the readout picks - a private copy of it here disagreed about the "Simple"
        // class, which is how a tooltip came to be announced from Content and reviewed from a window
        // that had not been drawn.
        private static AgeTooltip Readable(AgeTooltip tooltip)
        {
            return AgeWidgets.Readable(tooltip);
        }

        private static void AddLine(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line) && !lines.Contains(line))
            {
                lines.Add(line);
            }
        }

        private static void AddTooltip(List<string> lines, AgeTooltip tooltip)
        {
            Func<IList<string>> source = TooltipLines(tooltip);
            if (source == null)
            {
                return;
            }

            try
            {
                IList<string> from = source();
                for (int i = 0; from != null && i < from.Count; i++)
                {
                    AddLine(lines, from[i]);
                }
            }
            catch (Exception) { }
        }

        /// <summary>A table of things - anomalies, curiosities, deposits - reads one line per thing,
        /// which is how it is drawn and how it is reviewed.</summary>
        private static void AddWidgetLines(List<string> lines, AgeTransform widget)
        {
            if (widget == null || !Visible(widget))
            {
                return;
            }

            IList<AgeTransform> children = widget.Children;
            if (children == null || children.Count == 0)
            {
                AddLine(lines, AgeWidgets.TextOf(widget));
                return;
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (Visible(children[i]))
                {
                    AddLine(lines, AgeWidgets.TextOf(children[i]));
                }
            }
        }

        /// <summary>The first thing a tooltip says - what a control with no caption of its own is
        /// called, in the game's words.</summary>
        /// <summary>A widget's tooltip whatever kind it is - what a caller needs to SHOW one rather
        /// than to read it.</summary>
        private static AgeTooltip Raw(AgeTransform transform)
        {
            try
            {
                return transform == null ? null : transform.AgeTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What the player would read on a tooltip, resolved when they ask to read it.
        ///
        /// A tooltip that carries its words reads straight off the widget. One that names a class has
        /// none to carry, so it is read back off the tooltip the game has DRAWN - which focus is what
        /// makes appear. That the two are the same text is then true by construction rather than by
        /// this screen reimplementing however the game assembles a stat block.
        ///
        /// It follows that these lines exist a fraction of a second after focus arrives, not with it:
        /// the game waits out its hover delay before drawing. The buffer is filled again when the
        /// tooltip lands (<see cref="PointerFocus.DrawnTooltipChanged"/>), which is well before anyone
        /// can press a key to read it.
        /// </summary>
        private static Func<IList<string>> TooltipLines(AgeTooltip tooltip)
        {
            return AgeWidgets.TooltipLines(tooltip);
        }

        // A control inside a group the window has collapsed is still marked visible itself, so the
        // chain above it is what says whether the player can see it.
        private static bool Visible(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    if (!at.Visible)
                    {
                        return false;
                    }

                    at = at.Parent;
                }

                return widget != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Enabled(AgeTransform widget)
        {
            try
            {
                return widget != null && widget.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Empire PlayerEmpire()
        {
            try
            {
                return Gui.PlayerEmpire;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static EndTurnWindow TurnWindow()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<EndTurnWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GuiManager GuiService()
        {
            try
            {
                return Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Speech;
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
    /// from reads: what the empire is worth, where the systems are, where the fleets are, what the game
    /// is asking about, and what the turn itself offers.
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
    /// its planets, then the starlanes leaving it. It changes no distance. How close the camera stands
    /// is the player's own choice, asked for from the system's menu, and it decides how much there is
    /// to read: from far off a planet is a circle with a name and a state, and from as close as the
    /// game goes it is a card with its outputs, its anomalies and everything a fleet could do to it.
    /// A key that quietly took the camera all the way in would have made the first of those two
    /// unreachable.
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
        private static readonly object FleetStop = "galaxy:fleets";

        /// <summary>The clusters the game draws over every view level - what the empire is worth, the
        /// notifications, a collapsed tutorial, the turn controls. This page is one of three that
        /// declare them.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

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

        /// <summary>Where the camera was standing when the player last asked to be taken into a
        /// system, so that asking to come back out puts them at the same distance rather than at some
        /// default. -1 when nobody has asked, which is what makes the map's own starting step the
        /// answer instead.</summary>
        private int _stepBeforeSystemView = -1;

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
        }

        public override void OnPop()
        {
            _hud.Forget();
        }

        public override void OnUpdate()
        {
            _hud.Update();
        }

        /// <summary>Down the screen, which is also the order the galaxy screen has always read in:
        /// the empire's banners across the top, then what the map is showing, then the right-hand
        /// edge - the bar a collapsed tutorial leaves at the top of it and the notification icons
        /// under that - and the turn controls in the bottom corner.</summary>
        public override void Build(GraphBuilder builder)
        {
            _hud.Empire(builder);

            builder.BeginStop(SystemStop);
            BuildSystems(builder);

            builder.BeginStop(FleetStop);
            BuildFleets(builder);

            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.Turn(builder);
        }

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
        /// Enter is the game's own route into a colony of yours, the one the label's own button takes.
        /// A system that is not yours has nowhere to be taken to, and does nothing rather than
        /// inventing somewhere.
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
                GraphNodes.ModeFor(tooltip)
            );
            if (owned)
            {
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(() => ModStrings.Get(ModStrings.GalaxySystemColonized))
                );
            }

            GalaxyHudScreen screen = this;
            bool ours = owned;
            vtable.OnActivate = () => screen.OpenSystemMenu(it, ours);
            vtable.DetailLines = TooltipLines(tooltip);

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
            // exists while the camera is out - so where the camera goes is asked for from the menu
            // instead, and the engine keeps its own record of what is open.
            ControlId id = ControlId.Referenced(it, "galaxy:system/" + it.GUID);
            builder.BeginGroup(id, vtable);
            // Only what is open costs anything: a galaxy of closed systems declares one node each.
            if (builder.IsExpanded(id))
            {
                AddPlanets(builder, node, empire, owned, label);
                AddStarlanes(builder, node, empire);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// What can be done with a system from the map: go into its management page, and choose how
        /// close the camera stands to it.
        ///
        /// The two camera entries are one entry in two states, because the camera is either in on a
        /// system or it is not - and offering the one that would do nothing is how a menu teaches a
        /// player that half of it is decoration. Going in is the game's own double-click route; coming
        /// back out returns to the step the player was standing at before they asked to go in.
        /// </summary>
        private void OpenSystemMenu(StarSystemNode node, bool owned)
        {
            List<string> labels = new List<string>();
            List<Action> actions = new List<Action>();
            StarSystemNode it = node;
            GalaxyHudScreen screen = this;

            if (owned)
            {
                labels.Add(ModStrings.Get(ModStrings.GalaxyOpenSystem));
                actions.Add(() => GalaxyViewLevels.OpenSystem(it));
            }

            if (GalaxyViewLevels.AtOrbitalZoom)
            {
                labels.Add(ModStrings.Get(ModStrings.GalaxyReturnToGalaxyView));
                actions.Add(() => screen.ReturnToGalaxyView(it));
            }
            else
            {
                labels.Add(ModStrings.Get(ModStrings.GalaxyShowSystemView));
                actions.Add(() => screen.ShowSystemView(it));
            }

            List<Action> chosen = actions;
            ChoiceSubmenuScreen.Open(
                node.LocalizedName,
                labels,
                -1,
                index =>
                {
                    if (index >= 0 && index < chosen.Count)
                    {
                        chosen[index]();
                    }
                }
            );
        }

        private void ShowSystemView(StarSystemNode node)
        {
            _stepBeforeSystemView = GalaxyViewLevels.ZoomStep;
            GalaxyViewLevels.ZoomTo(node);
        }

        private void ReturnToGalaxyView(StarSystemNode node)
        {
            int step =
                _stepBeforeSystemView >= 0
                    ? _stepBeforeSystemView
                    : GalaxyViewLevels.DefaultZoomStep;
            _stepBeforeSystemView = -1;
            GalaxyViewLevels.ZoomToStep(node, step);
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
        /// a system with, so where one is drawn it is what a planet here reads from and what its menu
        /// offers. Where one is not - the camera is somewhere else, or has not arrived yet - the planet
        /// falls back to the model's own thin answer rather than going silent.
        /// </summary>
        private static void AddPlanets(
            GraphBuilder builder,
            StarSystemNode node,
            Empire empire,
            bool owned,
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
                    NodeVtable vtable;
                    if (card != null)
                    {
                        vtable = OrbitalReadout(card, system, owned);
                    }
                    else
                    {
                        // The circle is what the player would hover to get the planet's panel;
                        // without one the planet is still on the map, just with nothing to show
                        // under the pointer.
                        AgeTransform circle = Circle(table, i);
                        AgeTooltip tooltip = Raw(circle);
                        vtable = GraphNodes.Readout(
                            () => PlanetName(system, planet, looking),
                            () => PlanetStatus(system, planet, looking),
                            TooltipLines(tooltip),
                            tooltip
                        );
                        if (owned)
                        {
                            vtable.OnActivate = () => GalaxyViewLevels.OpenSystem(system);
                        }

                        if (circle != null)
                        {
                            PointAt(vtable, circle);
                        }
                    }

                    builder.AddItem(
                        ControlId.Referenced(
                            planet,
                            "galaxy:system/" + node.GUID + "/planet/" + i
                        ),
                        vtable
                    );
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
        /// card draws as icons and gauges in the review buffer, and its buttons in the action menu.
        /// </summary>
        private static NodeVtable OrbitalReadout(
            PlanetLabel_SystemOrbital card,
            StarSystemNode system,
            bool owned
        )
        {
            PlanetLabel_SystemOrbital it = card;
            StarSystemNode node = system;
            bool ours = owned;
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
                DetailLines = () => OrbitalDetails(it),
                OnActivate = () => OpenOrbitalMenu(it, node, ours),
            };
            // The card's own dossier - the paragraph the game writes about a world of this kind, its
            // size, its type - is the long panel behind the card, so it is indicated and read from the
            // buffer rather than said on every pass.
            NodeAnnouncement tooltipPart = GraphNodes.TooltipPart(
                GraphNodes.ModeFor(dossier),
                dossier
            );
            if (tooltipPart != null)
            {
                vtable.Announcements.Add(tooltipPart);
            }

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
        /// exist. The game's refusal to colonize is not here either - it is an answer to a question,
        /// and the place for it is the menu that asks it.
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
                AddTooltip(lines, card.PlanetInfoTooltip);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card threw: " + e);
            }

            return lines;
        }

        /// <summary>Whichever of the card's colonization buttons the game has put up and then refused
        /// to act on - they are alternatives, one per faction's way of settling a planet - because the
        /// sentence on it is where the game says why it is refusing. Null when nothing is refusing:
        /// the game leaves a blocked button visible and clickable and turns the click into "here is
        /// the technology you are missing", so being hinted is the only thing that tells the two
        /// apart.</summary>
        private static AgeTooltip ColonizeHint(PlanetLabel_SystemOrbital card)
        {
            AgeControlButton[] buttons = new AgeControlButton[]
            {
                card.ColonizeButton,
                card.BuyOutpostButton,
                card.VodyaniHintButton,
                card.UmbralChoirHintButton,
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                AgeTransform at = AgeWidgets.Transform(buttons[i]);
                if (at != null && Visible(at) && Gui.IsHintActive(at))
                {
                    return Raw(at);
                }
            }

            return null;
        }

        /// <summary>
        /// The one line the game is refusing on, in the game's own words - "Missing technology
        /// Maximized Exploitation".
        ///
        /// The hint's tooltip is assembled by the game in three known parts: the button's own
        /// description, then the failure, then - only ever for a missing technology - the sentence
        /// telling a mouse how to jump to it. The button's description names the action, which the
        /// menu entry beside it would already be named after, and the mouse instruction is for a
        /// mouse; what is left is the refusal.
        /// </summary>
        private static string ColonizeRefusal(AgeTooltip hint)
        {
            try
            {
                if (Readable(hint) == null)
                {
                    return null;
                }

                return RefusalText.Compose(
                    AgeText.Lines(AgeText.Tooltip(hint)),
                    AgeText.Clean(Gui.Localize("%MissingTechnologyClickDescription"))
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a colonize refusal threw: " + e);
                return null;
            }
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

        /// <summary>
        /// What can be done to this planet from the map, as a menu built from what the game is offering
        /// right now.
        ///
        /// Opening the planet's own page comes first, because that is what a click on the card itself
        /// does. Then the card's own buttons, each offered only when the game would really act on it:
        /// the game leaves a refusing colonize button clickable so that clicking it jumps to the
        /// technology that would unlock it, and that hint is not the action the entry is named after.
        ///
        /// The refusal itself is the last entry, and it does nothing. It is the one thing in any menu
        /// in this mod that is not an action, and it is here because it is the answer to the question
        /// the player opened the menu to ask: they came looking for Colonize, and "Missing technology
        /// Maximized Exploitation" - the game's sentence, not the mod's - is where it went. A menu that
        /// simply had no Colonize in it would have left them to guess.
        /// </summary>
        private static void OpenOrbitalMenu(
            PlanetLabel_SystemOrbital card,
            StarSystemNode system,
            bool owned
        )
        {
            List<string> labels = new List<string>();
            List<Action> actions = new List<Action>();
            List<Func<IList<string>>> details = new List<Func<IList<string>>>();
            try
            {
                Planet planet = card.Planet;
                if (planet != null)
                {
                    Planet it = planet;
                    labels.Add(ModStrings.Get(ModStrings.SystemViewPlanet));
                    details.Add(null);
                    actions.Add(() => GalaxyViewLevels.OpenPlanet(it));
                }

                if (owned)
                {
                    StarSystemNode node = system;
                    labels.Add(ModStrings.Get(ModStrings.GalaxyOpenSystem));
                    details.Add(null);
                    actions.Add(() => GalaxyViewLevels.OpenSystem(node));
                }

                AddOrbitalAction(
                    card.ColonizeButton,
                    ModStrings.Get(ModStrings.SystemColonize),
                    labels,
                    details,
                    actions
                );
                AddOrbitalAction(card.BuyOutpostButton, null, labels, details, actions);
                AddOrbitalAction(card.MinorFactionButton, null, labels, details, actions);

                // The row of small round buttons under the card. The game draws them as bare icons
                // and hangs an assembled stat block on each, so there is no caption and no first line
                // of tooltip to name them by - but the game DOES name every one of them, on the fleet
                // action each carries out, and those are the words a player reading the manual would
                // meet. In the order the card draws them.
                AddOrbitalAction(
                    card.TerraformationButton,
                    Localized("%InitiateTerraformPlanetFleetActionTitle"),
                    labels,
                    details,
                    actions
                );
                AddOrbitalAction(
                    card.RestorationButton,
                    Localized("%InitiateRestorePlanetFleetActionTitle"),
                    labels,
                    details,
                    actions
                );
                AddOrbitalAction(
                    card.AnomalyReductionButton,
                    Localized("%InitiateReduceAnomalyFleetActionTitle"),
                    labels,
                    details,
                    actions
                );
                AddOrbitalAction(
                    card.MiningProbeButton,
                    Localized("%LaunchMiningProbeFleetActionTitle"),
                    labels,
                    details,
                    actions
                );
                AddOrbitalAction(
                    card.DestroyButton,
                    Localized("%DestroyPlanetFleetActionTitle"),
                    labels,
                    details,
                    actions
                );

                AddColonizeRefusal(card, labels, details, actions);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: working out a planet's actions threw: " + e);
            }

            List<Action> chosen = actions;
            ChoiceSubmenuScreen.Open(
                AgeText.Label(card.PlanetName),
                labels,
                -1,
                index =>
                {
                    if (index >= 0 && index < chosen.Count)
                    {
                        chosen[index]();
                    }
                },
                details
            );
        }

        /// <summary>One of the card's buttons, offered only if the game would act on it. A button whose
        /// click has been turned into a hint - "here is the technology you are missing" - is not the
        /// action it looks like, and is left out; the refusal it carries is added once, at the end, as
        /// its own entry. <paramref name="name"/> is null for a button whose tooltip opens with a name
        /// worth using, which is then the game's own sentence about itself.</summary>
        private static void AddOrbitalAction(
            AgeControlButton button,
            string name,
            List<string> labels,
            List<Func<IList<string>>> details,
            List<Action> actions
        )
        {
            try
            {
                AgeTransform at = AgeWidgets.Transform(button);
                if (
                    at == null
                    || !Visible(at)
                    || !AgeWidgets.Operable(at)
                    || Gui.IsHintActive(at)
                )
                {
                    return;
                }

                AgeTooltip tooltip = Raw(at);
                string label = name ?? FirstLine(tooltip);
                if (string.IsNullOrEmpty(label))
                {
                    Log.Warn("galaxy: an orbital card button has no name to offer: " + at.name);
                    return;
                }

                AgeControlButton press = button;
                labels.Add(label);
                details.Add(TooltipLines(tooltip));
                actions.Add(() => AgeWidgets.Press(press));
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading an orbital card button threw: " + e);
            }
        }

        /// <summary>
        /// The entry that is not an action: the game's own sentence about why it will not colonize
        /// this planet, carried into the menu so that the refusal is where the request was made.
        ///
        /// Choosing it does nothing except close the menu. That is deliberate - the drawn button
        /// behind it would jump the player into the technology tree, which is a page this mod does not
        /// yet describe, so following it would be a one-way door. The whole hint, that instruction
        /// included, is in the entry's own buffer for anyone who wants it.
        /// </summary>
        private static void AddColonizeRefusal(
            PlanetLabel_SystemOrbital card,
            List<string> labels,
            List<Func<IList<string>>> details,
            List<Action> actions
        )
        {
            AgeTooltip hint = ColonizeHint(card);
            string refusal = ColonizeRefusal(hint);
            if (string.IsNullOrEmpty(refusal))
            {
                return;
            }

            labels.Add(refusal);
            details.Add(TooltipLines(hint));
            actions.Add(DoNothing);
        }

        private static readonly Action DoNothing = () => { };

        /// <summary>A phrase the game wrote, by the game's own key for it - for a control the game
        /// draws as a wordless icon and names nowhere on the screen.</summary>
        private static string Localized(string key)
        {
            try
            {
                return AgeText.Clean(Gui.Localize(key));
            }
            catch (Exception)
            {
                return null;
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
        /// </summary>
        private static void AddStarlanes(GraphBuilder builder, StarSystemNode node, Empire empire)
        {
            try
            {
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
                    GameNode destination = far;
                    bool named = Perceived(far, empire);
                    string template = wormhole
                        ? (named ? ModStrings.GalaxyWormhole : ModStrings.GalaxyWormholeUnexplored)
                        : (named ? ModStrings.GalaxyStarlane : ModStrings.GalaxyStarlaneUnexplored);
                    Func<string> text = named
                        ? (Func<string>)(
                            () => ModStrings.Format(template, destination.LocalizedName)
                        )
                        : () => ModStrings.Get(template);
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(text),
                        },
                    };
                    builder.AddItem(
                        ControlId.Referenced(
                            link,
                            "galaxy:system/" + node.GUID + "/lane/" + link.GUID
                        ),
                        vtable
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's starlanes threw: " + e);
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

        /// <summary>The player's own fleets - what each is made of and whether it is under way.
        /// Activating one takes the camera to it and selects it, which is the same thing the button
        /// that walks the idle fleets does. An empire with no fleets has no stop here: the game shows
        /// nothing for that state either.</summary>
        private static void BuildFleets(GraphBuilder builder)
        {
            try
            {
                Empire empire = PlayerEmpire();
                DepartmentOfDefense defense =
                    empire == null ? null : empire.GetAgency<DepartmentOfDefense>();
                if (defense != null)
                {
                    foreach (Fleet fleet in defense.Fleets)
                    {
                        Fleet it = fleet;
                        NodeVtable vtable = GraphNodes.Button(
                            () => it.LocalizedName,
                            () => Select(it),
                            null,
                            null
                        );
                        vtable.Announcements.Add(GraphNodes.ValuePart(() => FleetText(it)));
                        builder.AddItem(
                            ControlId.Referenced(it, "galaxy:fleet/" + it.GUID),
                            vtable
                        );
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets threw: " + e);
            }
        }

        private static string FleetText(Fleet fleet)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                message.ListItem(ModStrings.Format(ModStrings.GalaxyFleetShips, fleet.ShipsCount));
                message.ListItem(
                    fleet.IsMoving
                        ? ModStrings.Get(ModStrings.GalaxyFleetMoving)
                        : ModStrings.Format(
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

        /// <summary>Take the camera to a fleet and select it, through the window's own routine for
        /// exactly that - it knows to wait for the galaxy view to finish coming back before it hands
        /// the fleet to the cursor, and getting that order wrong leaves the selection on nothing.
        /// </summary>
        private static void Select(Fleet fleet)
        {
            try
            {
                EndTurnWindow window = TurnWindow();
                if (window != null && SelectIdleFleet != null)
                {
                    SelectIdleFleet.Invoke(window, new object[] { fleet });
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: selecting a fleet threw: " + e);
            }
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

        private static AgeTooltip Readable(AgeTooltip tooltip)
        {
            try
            {
                return tooltip != null && string.IsNullOrEmpty(tooltip.Class) ? tooltip : null;
            }
            catch (Exception)
            {
                return null;
            }
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
        private static string FirstLine(AgeTooltip tooltip)
        {
            try
            {
                if (Readable(tooltip) == null)
                {
                    return null;
                }

                IList<string> lines = AgeText.Lines(AgeText.Tooltip(tooltip));
                return lines != null && lines.Count > 0 ? lines[0] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

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
            if (tooltip == null)
            {
                return null;
            }

            AgeTooltip it = tooltip;
            return () =>
                Readable(it) != null
                    ? AgeText.Lines(AgeText.Tooltip(it))
                    : DrawnTooltip.Lines(it);
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

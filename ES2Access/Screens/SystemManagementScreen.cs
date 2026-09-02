using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// One star system, opened up: the page the game takes the player to when they enter a colony of
    /// theirs, and where a 4X game is actually played.
    ///
    /// It is not a window either. The page is the game's StarSystemScreen with the planet labels drawn
    /// over the middle of it and the side panels drawn down the left, and none of the three knows about
    /// the others - so being ours is "is the camera at the system management view level", the same
    /// question the galaxy page asks about itself, rather than "is a window up".
    ///
    /// Tab walks the page in the order it is drawn: the planets across the middle, then the panels down
    /// the left edge, then the three panels along the bottom. Which side panels there are is the game's
    /// answer, not ours: it swaps whole sets of them by what the system IS - a colony, an outpost, the
    /// ghost of one - so this screen declares a stop for each panel it finds drawn and gets the
    /// switching for free.
    ///
    /// EVERY control here takes the click the game itself puts on it, Enter for Enter, including the
    /// queue line whose click cancels a construction - the game asks its own question where it wants
    /// one, and where it does not the thing is reversible by queueing it again. Nothing is wrapped in
    /// a menu of what could be done: a card's buttons and a queue line's buy-outs are child nodes,
    /// opened with right the way the galaxy page taught. What the game only offers as a DRAG - a
    /// population unit moving between planets, a queue line moving up the queue - is CARRIED: Space
    /// picks it up and Enter on the destination puts it down, the same gesture a ship gets in the
    /// fleet panel.
    ///
    /// A planet card holds far more than a control's readout can carry - its type, its traits, its
    /// anomalies, its five outputs, and the game's own sentence about why it cannot be colonized yet -
    /// so the readout is its name and what state it is in, and all the rest is in the review buffer,
    /// which is what the review buffer is for.
    ///
    /// The side panels are the part a widget tree cannot name for itself, and the four rules the
    /// hooks below follow are all consequences of that. A panel of wordless readouts is matched by
    /// GAME COMPONENT or by the owning SidePanel's own field, never by widget shape, and Special
    /// answers a hand-built cell for it. A COUNT is spoken through ModStrings.Plural off the model,
    /// never re-read from the digits drawn on the control. Transparent is for the other half: a group
    /// the game made clickable that is really a band of readouts. NAMES come from the game -
    /// AgeWidgets.TooltipTitle, Gui.GetLocalizedTitle, or a tooltip's FIRST LINE only where that line
    /// names the thing, since a data-bearing explaining sentence is a description and not a title.
    /// And every key here includes widget.name: repeated rows otherwise collide on Duplicate control
    /// id, which empties the whole screen silently.
    /// </summary>
    public sealed partial class SystemManagementScreen : Screen
    {
        /// <summary>The one stop the game's left-edge INFORMATION panels share (owner design
        /// 2026-08-29): colony info, population, representatives, governor - and whatever an outpost or
        /// a ghost system draws instead - are four things to read about one system rather than four
        /// places to work, so Tab passes them once and Alt+Up/Down steps between them by name. The
        /// spaceport is not among them: it is a work surface and keeps its own stop.</summary>
        private const string SidePanelsStop = "system:side";

        private static readonly object PageStop = "system:page";
        private static readonly object PlanetStop = "system:planets";
        private static readonly object ConstructiblesStop = "system:constructibles";
        private static readonly object QueueStop = "system:queue";
        private static readonly object HangarStop = "system:hangar";

        /// <summary>The prefix the shared readers key this page's ids under.</summary>
        private const string SystemKeys = "system:";

        /// <summary>The clusters the game draws over every view level. They are drawn over this page
        /// too, and until they were declared here they were on the screen and out of reach.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

        /// <summary>How close the game is looking. This page IS a rung of that ladder
        /// (<see cref="GalaxyViewLevels.ZoomRung"/>), so the same control the map offers is offered
        /// here: stepping out goes back to the map, stepping in opens a planet's page.</summary>
        private readonly ZoomLadder _zoom = new ZoomLadder();

        /// <summary>Reused across builds rather than allocated per frame: Build runs every tick.
        /// </summary>
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<PlanetLabel_SystemManagement> _planets =
            new List<PlanetLabel_SystemManagement>();
        private readonly List<SidePanel> _panels = new List<SidePanel>();
        private readonly List<AgeTransform> _blocks = new List<AgeTransform>();

        public override string Key
        {
            get { return ModStrings.ScreenStarSystem; }
        }

        /// <summary>The same layer as the galaxy: it is the other half of the same map, and the two are
        /// never up together.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        /// <summary>
        /// "Heka, System management" - the system the page is showing, then the game's own word for
        /// the page (<c>%StarSystemManagementScanViewWindowTitle</c>).
        ///
        /// The page is turned without leaving it (Alt+Left/Right, and the game's own arrows beside the
        /// name), so a name that said only "System management" left the one fact the turn is FOR -
        /// which system - unspoken. The system's name is the DRAWN one, off the rename button's label
        /// (<c>ColonyInfoSidePanel.SystemTitleLabel</c>), which the game writes for an outpost as
        /// readily as for a colony. Where the panel is not drawn at all the mod's own word for the page
        /// stands alone, as it did before.
        /// </summary>
        public override string ScreenName
        {
            get
            {
                string system = SystemTitle();
                string page = AgeText.Title(SystemManagementTitleKey);
                if (string.IsNullOrEmpty(system) || page == null)
                {
                    return ModStrings.Get(ModStrings.ScreenStarSystem);
                }

                return ModStrings.Format(ModStrings.ScreenStarSystemNamed, system, page);
            }
        }

        /// <summary>The game's own word for this page, the one its scan-view header uses.</summary>
        private const string SystemManagementTitleKey = "%StarSystemManagementScanViewWindowTitle";

        /// <summary>The system's name as the page DRAWS it. Null where the colony panel is not up -
        /// a system the player owns nothing in.</summary>
        private string SystemTitle()
        {
            try
            {
                // Its own list, not the build's: this is asked from outside a build (the screen manager
                // announcing the page, the dev dumps) and must not disturb one in progress.
                List<SidePanel> panels = new List<SidePanel>();
                SidePanels.Drawn(panels);
                for (int i = 0; i < panels.Count; i++)
                {
                    ColonyInfoSidePanel colony = panels[i] as ColonyInfoSidePanel;
                    if (colony != null)
                    {
                        return AgeText.Label(colony.SystemTitleLabel);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// WHERE THE CURSOR IS PUT, IN THREE CASES (owner design 2026-08-29). Only the first of them
        /// is this property: a page the player has never stood on has nothing to put back, and the
        /// first thing to say about a system is what the system IS, which is the left edge's
        /// information panels - now one stop, and the first stop the page itself declares.
        ///
        /// The other two cases restore where the player WAS, and neither goes through here:
        /// coming back from the galaxy, and turning the page to another system with Alt+Left/Right,
        /// both put the cursor back on the control it was on (<see cref="Restore"/>). This property
        /// is their last fallback, for a place the new system has no equivalent of at all.
        /// </summary>
        public override object InitialFocusStop
        {
            get { return SidePanelsStop; }
        }

        /// <summary>The page a modal is opened FROM, so closing the improvements list or the rename box
        /// puts the cursor back on the control that opened it rather than at the top of the page.
        /// </summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>
        /// SPACE NEVER FALLS THROUGH FROM THIS PAGE (owner ruling 2026-08-26). The game's own Space
        /// here is the scan mode (<c>InputManager</c> ToggleScanView, the shortcut this page's own scan
        /// button names: "Shortcut: Space or Mouse 3"), and a keyboard player pressing Space on a planet
        /// card or a queue line means "pick this up" - a whole different view arriving instead is not an
        /// outcome that row offered. So the key is the mod's on every node of the page: a row with
        /// something to pick up carries exactly as before, and every other press is consumed and silent
        /// (no cue - the key is pressed row after row looking for what will move). Scan mode stays one
        /// Enter away, on the button the game draws for it (<c>hud:view-title/scan</c>).
        ///
        /// Asked by the claim beside the ordinary carry claim (<c>ModEntry.CarryKeyClaimed</c>) and
        /// again by the dispatch before it swallows a press nothing carried
        /// (<c>ModEntry.SwallowedCarry</c>) - a claim is settled before the press, so the swallow is
        /// never allowed to run on a stale yes. Scoped to THIS page: the scan view over it, the galaxy
        /// and every modal this page opens keep Space as the game's.
        /// </summary>
        public static bool SwallowsCarryKey()
        {
            GraphNavigator navigator = ModEntry.Navigator;
            return navigator != null && navigator.Screen is SystemManagementScreen;
        }

        /// <summary>
        /// Ours while the camera is in a system and nothing has replaced the page. The scan
        /// overlay is the game's own X-ray of this same view level and shows a different set of things,
        /// so it is not this screen.
        ///
        /// Asked of <see cref="GalaxyViewLevels.LevelThroughTransitions"/> and latched, the way the
        /// planet page asks it, because TURNING THE PAGE re-enters this same view level with another
        /// system: the GUI's copy of the current level and the window's own Shown flag each drop for a
        /// single frame while that happens, and the screen leaving and coming back is a full focus
        /// cycle - it announced the page twice and left the cursor wherever the old system's tree had
        /// put it. The latch is dropped by the level itself going away, so leaving the page for real
        /// still ends the screen.
        ///
        /// AN ICON-STRIP SCREEN ENDS THE PAGE THE WAY A MODAL DOES, and for the same reason. The
        /// empire, economy and the rest are exclusive full-screen windows: showing one hides this
        /// page's own window and its planet cards a frame or two BEFORE the mod pushes that screen
        /// (measured 2026-08-29 by a per-frame trace of Enter on the colony banner). For those frames
        /// the page was still the focused screen and still rebuilt - without its cards and without its
        /// side panels - so the node the cursor stood on no longer existed, the navigator re-seated it
        /// on the last surviving HUD control, and the state a return would restore was already that
        /// wrong seat. The player saw it as "Escape from the economy screen puts me on the empire
        /// banners". The condition is the GAME's own <c>IsAnyScreenVisible</c>, which it pairs with
        /// <c>IsAnyModalVisible</c> itself (<c>GuiManager.CanToggleScanView</c>), so the answer comes
        /// from the flag the game sets rather than from a window flag this page would have to debounce
        /// - the page turn above drops <c>Shown</c> for a single frame and must NOT end the screen.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                if (
                    !(
                        GalaxyViewLevels.LevelThroughTransitions
                        is GalaxyViewLevel_SystemManagement
                    )
                    || GalaxyViewLevels.Scanning
                )
                {
                    _arrived = false;
                    return false;
                }

                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                if (
                    gui == null
                    || gui.IsAnyModalVisible
                    || gui.IsInLoadingWindow
                    || gui.IsAnyScreenVisible
                )
                {
                    _arrived = false;
                    return false;
                }

                if (!_arrived)
                {
                    // The same gate <see cref="Build"/> declares on, and for the same reason: the
                    // window is bound and shown a good while before the planet cards are drawn over
                    // it, and a page that becomes ACTIVE while it can declare nothing gets its cursor
                    // seated on the first shared HUD control instead - measured 2026-08-22 as an entry
                    // landing on the view-title's scan button. Asked only until the page has arrived,
                    // so the extra walk costs nothing once it has.
                    StarSystemScreen window = Window();
                    if (window != null && window.Shown && window.StarSystemNode != null)
                    {
                        _arrived = Whole(_arriving, _arrivingPanels);
                        _arriving.Clear();
                        _arrivingPanels.Clear();
                        if (_arrived)
                        {
                            ExpandBottomPanels(window);
                        }
                    }
                }

                return _arrived;
            }
            catch (Exception)
            {
                _arrived = false;
                return false;
            }
        }

        /// <summary>
        /// Open the three bottom panels on the way in, for whoever is looking at the screen (owner
        /// request 2026-08-29). They are the constructibles, the queue and the hangar, and the game
        /// remembers how the player last left them; collapsed, a sighted observer sees about half of
        /// each list. Nothing here is for the keyboard - the button that does this is deliberately
        /// undeclared and collapsing changes no accessible content at all
        /// (<see cref="BuildBottomPanel"/>) - so this is silent, declares nothing and speaks nothing.
        ///
        /// Driven the way the game's own button drives it, both halves together: every
        /// <c>GuiFrameExpander</c> under the window is toggled AND
        /// <c>IGuiOptionsService.ExpandSystemPanels</c> is set, exactly as
        /// <c>StarSystemScreen.OnExpandCb</c> :736-745 does, so the flag and the frames can never
        /// disagree - a mismatch would make the player's own next press appear to do nothing.
        ///
        /// ON ENTRY ONLY. It runs on the frame the page arrives - once, because <see cref="_arrived"/>
        /// latches immediately after - so a player who collapses the panels while the page is up keeps
        /// them collapsed for as long as they stay. Leaving and coming back opens them again, which is
        /// what "on entry" means. The option's persistence carries that choice out of the session, so a
        /// player who never touches the panels simply always finds them open.
        /// </summary>
        private static void ExpandBottomPanels(StarSystemScreen window)
        {
            try
            {
                IGuiOptionsService options =
                    Amplitude.Unity.Framework.Services.GetService<IGuiOptionsService>();
                if (options == null || options.ExpandSystemPanels)
                {
                    return;
                }

                GuiFrameExpander[] expanders = window.GetComponentsInChildren<GuiFrameExpander>();
                for (int i = 0; i < expanders.Length; i++)
                {
                    if (expanders[i] != null)
                    {
                        expanders[i].ToggleExpansion();
                    }
                }

                options.ExpandSystemPanels = true;
            }
            catch (Exception e)
            {
                Log.Warn("system: opening the bottom panels on arrival threw: " + e);
            }
        }

        public override void Build(GraphBuilder builder)
        {
            StarSystemScreen window = Window();
            if (window == null)
            {
                return;
            }

            // The page arrives in pieces and leaves in pieces, and a half-drawn page declares NOTHING
            // AT ALL - see <see cref="Whole"/>, which owns the reasoning for both ends.
            if (!Whole(_planets, _panels))
            {
                return;
            }

            // Down the screen: the empire's banners in the top-left corner and the name of the view in
            // the centre, then the page itself,
            // then the right-hand edge - a collapsed tutorial's bar and the notification icons under
            // it - and the turn controls in the bottom corner. Same order as every other view level,
            // because the game draws them in the same places whichever one is up.
            _hud.Top(builder, _zoom);

            BuildPage(builder, window);

            // WHAT THE SYSTEM IS COMES BEFORE WHAT IS IN IT (owner design 2026-08-29): the left edge's
            // information panels, then the spaceport, then the cards. Tab does not wrap, so declaration
            // order is the order the player crosses the page in, and the panels that say whose system
            // this is and how it is getting on used to sit behind every planet card.
            BuildSidePanels(builder);

            builder.BeginStop(PlanetStop);
            builder.PushContext(ModStrings.Get(ModStrings.SystemPlanetsPanel));
            BuildPlanets(builder, window);
            builder.PopContext();

            // The three panels along the bottom are the same prefabs the Empire summary slides out
            // under its systems table, and they are read by the shared reader (SystemPanels); what is
            // this page's own is that all three are drawn at once, each as a stop of its own.
            StarSystemConstructiblePanel constructibles =
                window.GetComponentInChildren<StarSystemConstructiblePanel>(true);
            StarSystemQueuePanel queue = window.GetComponentInChildren<StarSystemQueuePanel>(true);
            StarSystemHangarPanel hangar = window.GetComponentInChildren<StarSystemHangarPanel>(true);
            BuildBottomPanel(
                builder,
                ConstructiblesStop,
                ModStrings.SystemConstructiblesPanel,
                constructibles == null ? null : constructibles.AgeTransform,
                () => SystemPanels.Constructibles(builder, constructibles, SystemKeys)
            );
            BuildBottomPanel(
                builder,
                QueueStop,
                ModStrings.SystemQueuePanel,
                queue == null ? null : queue.AgeTransform,
                () => SystemPanels.Queue(builder, queue, SystemKeys)
            );
            BuildBottomPanel(
                builder,
                HangarStop,
                ModStrings.SystemHangarPanel,
                hangar == null ? null : hangar.AgeTransform,
                () => SystemPanels.Hangar(builder, hangar, SystemKeys)
            );

            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.TurnLog(builder);
            _hud.Turn(builder);
        }

        /// <summary>
        /// What the game hangs on the page's own WINDOW rather than on any of its panels, drawn above
        /// the cards: the toggle between the view the player's sleepers have of a foreign colony and the
        /// view its owner has (<c>StarSystemScreen.SwitchTraitorsModeButton</c> :629, drawn only while
        /// the player has traitors in this system and there is a second colony to look at). Being drawn
        /// is what declares it - no empire without sleepers here ever meets it - and the game names it
        /// nowhere but in the sentence its own tooltip explains it with.
        ///
        /// It is a stop of its own before the planets rather than a card's child, because pressing it
        /// re-binds the WHOLE page: what every panel below is about changes.
        /// </summary>
        private void BuildPage(GraphBuilder builder, StarSystemScreen window)
        {
            _cells.Clear();
            Cells.AddControl(
                _cells,
                AgeWidgets.Transform(window.SwitchTraitorsModeButton),
                "system:traitors-mode"
            );
            if (_cells.Count == 0)
            {
                return;
            }

            builder.BeginStop(PageStop);
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>One of the three panels along the bottom, under the mod's own word for it - and
        /// under the word the panel DRAWS across its top, where the game hung the sentence saying what
        /// the panel is for on it. That caption is a row and not the panel's name: the name is what
        /// the stop is already called, and the sentence is what a name cannot carry
        /// (<see cref="Captions"/>).</summary>
        private static void BuildBottomPanel(
            GraphBuilder builder,
            object stop,
            string nameKey,
            AgeTransform panel,
            Action build
        )
        {
            builder.BeginStop(stop);
            builder.PushContext(ModStrings.Get(nameKey));
            Captions.Row(
                builder,
                AgeWidgets.ChildNamed(panel, "Header", 2),
                stop + "/header"
            );
            build();
            builder.PopContext();
        }

        // THE PANEL EXPAND BUTTON IS DELIBERATELY NOT DECLARED (owner ruling 2026-08-29). Each of the
        // three bottom panels draws a PanelExpandButton down its left edge and all three run one
        // handler (StarSystemScreen.OnExpandCb :736-745): it toggles every GuiFrameExpander under the
        // window and flips IGuiOptionsService.ExpandSystemPanels. What that DOES was measured
        // (docs/planets.md): the three frames go 177 to 292 and back and the lists SCROLL rather than
        // losing rows, so the accessible tree is byte-identical in both states. It changes how much a
        // sighted player sees at once and nothing a keyboard player can perceive, so it earns no node.
        // The coverage audit is told the same thing in one place, so a later run reports the reason
        // instead of re-raising it (CoverageAudit.DeliberatelyUnworked).

        // ---- shared ----

        private static void Add(List<Cell> cells, AgeTransform widget, ControlId id, NodeVtable vtable)
        {
            Cells.Add(cells, widget, id, vtable);
        }

        /// <summary>
        /// A line of the panel that the player reads rather than works. <paramref name="tooltip"/> is
        /// for the readouts whose tooltip the panel does NOT hang on the widget the number is drawn in -
        /// it keeps it in a field of its own and puts it on the group around the number - and it is the
        /// pointer's target too, because the game draws a tooltip for the widget that owns it and
        /// pointing at the number would draw nothing and leave the review buffer empty.
        /// </summary>
        private static void AddReadout(
            List<Cell> cells,
            AgeTransform widget,
            string key,
            Func<string> label,
            Func<string> value = null,
            AgeTooltip tooltip = null,
            AgeTransform click = null
        )
        {
            // Banding input, as at the buttons: Add below is Cells.Add, and the panel passes labels
            // here that it draws only in some of the colony's states.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTooltip tip = tooltip ?? AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(label) },
                Sections = GraphNodes.Sections(null, tip),
            };
            // A line the game also made CLICKABLE is a button, and says so: the row is still read the
            // same way and Enter is the game's own press. Nothing is spoken for the press itself -
            // every one of these opens a screen, and the screen announces itself.
            if (click != null)
            {
                AgeTransform pressed = click;
                vtable.ControlType = ControlTypes.Button;
                vtable.Announcements.Add(
                    GraphNodes.DisabledPart(() => AgeWidgets.Operable(pressed))
                );
                vtable.OnActivate = () =>
                {
                    if (AgeWidgets.Operable(pressed))
                    {
                        AgeWidgets.Press(pressed);
                    }
                };
            }

            if (value != null)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(value));
            }

            AgeWidgets.PointAt(vtable, widget, tip);
            Add(cells, widget, ControlId.For(widget, key), vtable);
        }

        /// <summary>One line per thing a card's table is drawing, the way both pages that draw a planet
        /// card read one (<see cref="PlanetCardLines.Add"/>).</summary>
        private static void AddWidgetLines(
            List<string> lines,
            AgeTransform widget,
            Func<AgeTransform, bool> skip = null
        )
        {
            PlanetCardLines.Add(lines, widget, skip);
        }

        /// <summary>A table item the card offers as a button of its own, and so is not a line of the
        /// card's - the curiosities the game mixes into the findings table.</summary>
        private static bool SkipCuriosities(AgeTransform item)
        {
            try
            {
                return item != null && item.GetComponent<PlanetCuriosityItem>() != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void AddLine(List<string> lines, string line)
        {
            PlanetCardLines.AddLine(lines, line);
        }

        private static void Add(List<string> lines, Func<IList<string>> source)
        {
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

        /// <summary>The caption written beside a control - a drop list's own name, which the game draws
        /// as a label next to it rather than on it.</summary>
        private static string LabelIn(AgeTransform group)
        {
            try
            {
                AgePrimitiveLabel[] labels = group.GetComponentsInChildren<AgePrimitiveLabel>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    string text = AgeText.Label(labels[i]);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        private static AgeTransform ChildAt(AgeTransform table, int index)
        {
            try
            {
                IList<AgeTransform> children = table.Children;
                return children != null && index < children.Count ? children[index] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static StarSystemScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<StarSystemScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

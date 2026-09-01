using System;
using System.Collections.Generic;
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
    /// The scan overlay's own furniture, wherever the player is standing under it.
    ///
    /// The scan view is a MODE rather than a place: the camera stays where it was and the game swaps
    /// every label on the map for a different set. Which set is not a choice the player makes - the
    /// zoom step picks a layer descriptor, the descriptor picks the lens - so the lens is a fact about
    /// how close the camera is, and the map underneath is the same map (owner ruling 2026-09-01,
    /// which is why the galaxy page keeps the keyboard in-mode instead of handing it to a page of its
    /// own).
    ///
    /// What the LENS adds on top of that map is the same three things at every rung: the title strip
    /// it draws where the view's name usually goes, the announcement that the lens has changed, and
    /// the legend down the left edge. Those belong to no one page - the galaxy page wears them over
    /// the map, and <see cref="ScanViewScreen"/> wears them over a system's page and a planet's - so
    /// they are a COMPONENT rather than a screen's own code, held per page and given back with it.
    /// The <c>scan:system</c> centre panel comes with them, because it is drawn by a lens window like
    /// the rest of this and not by the map.
    ///
    /// The lenses are not dispatched on. Each draws its own window and the game hides every window but
    /// the live one, so every lens is offered every frame and the DRAWN one is the only one that
    /// contributes anything: a lens this mod has never seen still gets its title read, and a lens
    /// mapped to the wrong zoom step cannot make a page describe something that is not on screen.
    /// </summary>
    public sealed class ScanLensPanels
    {
        /// <summary>The layer descriptor the player has already been told about - the DESCRIPTOR and not
        /// the lens's name, because three of the descriptor boundaries fall inside one name and the
        /// drawing changes at every one of them, so a name is no baseline. The descriptor outlives the
        /// page - the game keeps it up to date whether or not the lens is up - so arriving baselines
        /// against what is showing rather than against nothing, and the arrival announcement is not
        /// said twice.</summary>
        private string _descriptor;

        /// <summary>Whether the lens has finished showing itself since the mode was entered - the
        /// arrival gate, held until the mode ends (<see cref="Arrived"/>).</summary>
        private bool _arrived;

        /// <summary>The title strip each lens window draws for itself. The windows live for the whole
        /// session and instantiate their sections once, so these are found once per showing rather than
        /// per frame; instance state, so a hot reload takes them with the page.</summary>
        private ScanViewWindowHeader[] _headers;

        /// <summary>The six labels the system lens rings a star with. Same reasoning: the panel creates
        /// them once and reuses them for whichever system the camera is nearest.</summary>
        private ScanViewSystemOverviewFidsiLabel[] _fidsi;

        /// <summary>Scratch for reading how many captions the live lens declares, and the context its
        /// prerequisites are asked in. Reused rather than allocated, because the legend is read on every
        /// rebuild.</summary>
        private readonly List<ScanViewCaptionGroupGuiElement> _legend =
            new List<ScanViewCaptionGroupGuiElement>();

        private readonly Amplitude.Unity.Framework.PrerequisiteContext _prerequisites =
            new Amplitude.Unity.Framework.PrerequisiteContext();

        /// <summary>The stop the lens's own title strip and the page's zoom ladder sit in.</summary>
        public static readonly object TitleStop = "scan:title";

        /// <summary>The stop the legend down the left edge sits in.</summary>
        public static readonly object LegendStop = "scan:legend";

        /// <summary>
        /// Whether the battle screen is still on its way off the screen, which is the one window where
        /// the scan view outlives the thing it was an overlay ON.
        ///
        /// A player who leaves the battle's Scan toggle checked is still in the game's scan MODE when the
        /// fight ends, and the game turns it off from <c>BattleScreen.OnEndHide</c> - at the END of the
        /// screen's fade-out, whereas <c>IsInBattle</c> goes false the moment the view level stops being
        /// the encounter, several frames earlier. Between the two the galaxy's own lens is genuinely up:
        /// the game shows <c>EconomyScanViewWindow</c> and a page arriving on it announced a lens and a
        /// title row and was gone again - two lines about the map thrown into the middle of a battle
        /// ending.
        ///
        /// <c>Visible</c> is the answer rather than <c>Shown</c> because the fade-out is exactly the
        /// window in question and <c>Shown</c> is already false throughout it
        /// (<c>GuiPanel.Shown => (Visible &amp;&amp; !Hiding) || Showing</c>). It cannot strand anyone:
        /// <c>GuiPanel.OnEndHide</c> clears <c>Visible</c> in the same call that ran the game's auto-off,
        /// so this gate releases on the very frame the mode ends - the backstop and the gate are one
        /// event. And it delays no ordinary entry: with no battle on the screen the window is not
        /// visible at all.
        /// </summary>
        public static bool BattleEnding()
        {
            BattleScreen battle = Window<BattleScreen>();
            return battle != null && (battle.Visible || battle.Showing);
        }

        /// <summary>Taken when the page is pushed: the descriptor showing now is the baseline, so
        /// arriving does not announce a lens the player is already looking at twice.</summary>
        public void Baseline()
        {
            _headers = null;
            _fidsi = null;
            _arrived = false;
            _descriptor = Descriptor();
        }

        /// <summary>Given back when the page goes.</summary>
        public void Forget()
        {
            _headers = null;
            _fidsi = null;
            _arrived = false;
            _descriptor = null;
        }

        /// <summary>
        /// Whether a lens has finished showing ITSELF since the mode was entered.
        ///
        /// The game turns "normal view" off a good number of frames before it shows the lens's own
        /// windows, and for those frames the only thing over the map is the turn controls - so anything
        /// that reads the lens has to wait, or it reads a strip the game has not switched on yet and
        /// says "unavailable" once. Once the lens is up the answer stays yes until the mode ends, so
        /// the frames where the game is fading the lens back out do not take the furniture away and
        /// give it back again.
        ///
        /// Asked every frame by the page, which passes in whether the mode is on at all: the gate is
        /// released the moment it is not.
        /// </summary>
        public bool Arrived(bool scanning)
        {
            if (!scanning)
            {
                _arrived = false;
                return false;
            }

            _arrived = _arrived || Drawn();
            return _arrived;
        }

        /// <summary>Whether a lens has anything of its own on the screen yet. Every lens draws a title
        /// strip, so the drawn strip is the answer.
        ///
        /// Drawn is not enough: the game switches the lens's controls on a frame AFTER it shows them, so
        /// a page arriving the moment the strip appears reads it "unavailable" once - once, and then
        /// never again, because a live part only re-speaks on change and by then the player has heard
        /// it.</summary>
        private bool Drawn()
        {
            ScanViewWindowHeader header = DrawnHeader();
            return header != null && AgeWidgets.Operable(header.AgeTransform);
        }

        /// <summary>The lens has changed under the player - they zoomed, or they walked into a system -
        /// and everything on the screen now means something else. Queued, never interrupting: it is
        /// something that happened rather than an answer to a key.
        ///
        /// Said on every descriptor change, INCLUDING one whose lens name is the name just said. Three of
        /// the nine descriptors' boundaries fall inside a single name, and the game redraws the band as
        /// heavily there as anywhere else - so suppressing the repeat let the three loudest same-name
        /// steps pass in silence, which is the one thing this watcher exists to prevent (owner ruling
        /// 2026-08-17). Hearing "Trade" twice is the price of never crossing a band unannounced.
        ///
        /// Answers whether it SAID anything, for the page that keeps the keyboard across the mode
        /// change: entering the lens is itself news, and a page that was never pushed has no screen
        /// announcement to carry it - so the galaxy page says the lens itself on the way in, and only
        /// where this watcher has not already done so (<c>GalaxyHudScreen.OnUpdate</c>).</summary>
        public bool Announce()
        {
            try
            {
                string descriptor = Descriptor();
                if (descriptor == _descriptor)
                {
                    return false;
                }

                _descriptor = descriptor;
                string lens = Name();
                if (string.IsNullOrEmpty(lens))
                {
                    return false;
                }

                Voice.Say(lens, false);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("scan: watching the lens threw: " + e);
                return false;
            }
        }

        // ---- the title ----

        /// <summary>The strip the live lens draws where every other page draws the view's name, and it
        /// is a button for the same reason that one is: pressing it leaves.</summary>
        public void Title(GraphBuilder builder)
        {
            ScanViewWindowHeader header = DrawnHeader();
            if (header == null)
            {
                return;
            }

            ScanViewWindowHeader it = header;
            AgeControlButton button = header.Button;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Label(it.TitleLabel),
                () => AgeWidgets.Press(button),
                () => AgeWidgets.Operable(it.AgeTransform),
                AgeWidgets.Raw(it.AgeTransform)
            );
            AgeWidgets.Point(vtable, button, AgeWidgets.Raw(it.AgeTransform), it.AgeTransform);
            builder.AddItem(Nodes.Drawn(ControlId.For(header, "scan:title/lens"), vtable, header));
        }

        // ---- the system lens's centre panel ----

        /// <summary>
        /// The system lens inspects ONE system - whichever is nearest the middle of the screen
        /// (<c>StarSystemOverviewScanViewWindow.MaxDistanceToScreenCenter</c>) - so it is one node, with
        /// the six figures it rings the star with as its children and the tick that opens its longer
        /// panel beside them.
        ///
        /// The figures need no help: the game writes each one as a number and the output's own icon, and
        /// the icon has a name.
        ///
        /// It is a lens about one system and NOT about its planets - those belong to the system
        /// management lens a rung further in.
        ///
        /// What the tick reveals is read here as well, because the panel it shows is a SIBLING of the
        /// name rather than a child of it: the system's name again, the remains standing on one of its
        /// planets, and the line the game writes instead of all of it for somebody else's colony. The two
        /// rank graphs it also holds are geometry with no words at all and are not modelled (roadmap);
        /// in a save where neither they nor any remains are drawn, the tick changes nothing that can be
        /// heard, which is the truth about that save rather than a gap in the reading.
        /// </summary>
        public void SystemOverview(GraphBuilder builder)
        {
            StarSystemOverviewScanViewWindow window = Window<StarSystemOverviewScanViewWindow>();
            if (window == null || !window.Shown || !AgeWidgets.Visible(window.NodeInfoGroup))
            {
                return;
            }

            try
            {
                StarSystemOverviewScanViewWindow it = window;
                ControlId id = ControlId.Structural("scan:system");
                NodeVtable vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Group,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeText.Label(it.NodeNameLabel)),
                    },
                    Sections = GraphNodes.Sections(NodeSection.Buffer(() => SystemLines(it))),
                };
                AgeWidgets.PointAt(vtable, window.NodeInfoGroup);

                // Synthetic: the group stands for the SYSTEM the overview is about, and its lines are
                // read out of the panel rather than off one widget.
                builder.BeginGroup(Nodes.Synthetic(id, vtable));
                ScanViewSystemOverviewFidsiLabel[] labels = Fidsi(window);
                for (int i = 0; i < labels.Length; i++)
                {
                    ScanViewSystemOverviewFidsiLabel label = labels[i];
                    if (label == null)
                    {
                        continue;
                    }

                    builder.AddItem(
                        Nodes.Drawn(
                            ControlId.For(label, "scan:system/output/" + i),
                            GraphBuilder.Label(() => AgeText.Label(label.ValueLabel)),
                            label
                        )
                    );
                }

                SystemInfoToggle(builder, window);
                builder.EndGroup();
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the system lens threw: " + e);
            }
        }

        /// <summary>Everything the lens has to say about the system it is inspecting: the strip the name
        /// and the tick sit in, the line of figures above it, and the panel the tick shows - each read
        /// only while it is drawn, so what the buffer holds is what is on the screen.</summary>
        private static IList<string> SystemLines(StarSystemOverviewScanViewWindow window)
        {
            List<string> lines = new List<string>();
            AddDrawn(lines, window.NodeInfoGroup);
            AddDrawn(lines, window.TopLineTable);
            AddDrawn(lines, window.RemainsPanel.Shown ? window.RemainsPanel.AgeTransform : null);
            AddDrawn(
                lines,
                window.InfoPanel.Shown ? window.InfoPanel.InformationInaccessibleLabel.AgeTransform : null
            );
            return lines;
        }

        /// <summary>A panel's drawn words appended to <paramref name="lines"/>, and nothing at all where
        /// the panel is not on the screen.</summary>
        private static void AddDrawn(List<string> lines, AgeTransform widget)
        {
            // Flow control: whether a panel's words are read at all.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            IList<string> drawn = AgeWidgets.DrawnLines(widget);
            for (int i = 0; drawn != null && i < drawn.Count; i++)
            {
                if (!string.IsNullOrEmpty(drawn[i]))
                {
                    lines.Add(drawn[i]);
                }
            }
        }

        /// <summary>The tick beside the system's name. The game draws it as a bare box, so it is named
        /// here; what it reveals is a second panel off to one side, whose words the node's own buffer
        /// reads once it is showing (<see cref="SystemLines"/>).</summary>
        private static void SystemInfoToggle(
            GraphBuilder builder,
            StarSystemOverviewScanViewWindow window
        )
        {
            AgeControlToggle toggle = window.SystemInfoToggle;
            AgeTransform widget = AgeWidgets.Transform(toggle);
            // Flow control: whether the tick is walked at all.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => ModStrings.Get(ModStrings.ScanSystemInfo),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(widget),
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(toggle, "scan:system/info"), vtable, toggle));
        }

        // ---- the legend ----

        /// <summary>
        /// What the lens's colours and symbols mean, as the panel down the left edge lists them: a tick
        /// that folds the whole thing away, then a group per heading.
        ///
        /// The groups are declared only while the tick is on. The game leaves them in the tree when it
        /// folds the panel and squeezes them to nothing instead of hiding them, so asking whether they
        /// are visible would declare a list the player cannot see - the tick is the game's own answer to
        /// whether the legend is showing.
        ///
        /// One panel serves every lens: the live lens points it at its own captions
        /// (<c>GuiLayeredScanViewWindow</c>), which is also why nothing here says which lens it belongs
        /// to. It is not shown at all over a lens with no legend.
        /// </summary>
        public void Legend(GraphBuilder builder)
        {
            ScanOverlayWindow window = Window<ScanOverlayWindow>();
            ScanViewWindowCaptionsPanel panel = window == null ? null : window.CaptionsPanel;
            if (
                window == null
                || !window.Shown
                || panel == null
                // Flow control: the caption groups below are walked group by group.
                || !AgeWidgets.Visible(panel.AgeTransform)
            )
            {
                return;
            }

            try
            {
                AgeControlToggle toggle = panel.CaptionsToggle;
                AgeTransform widget = AgeWidgets.Transform(toggle);
                if (widget == null)
                {
                    return;
                }

                AgeControlToggle it = toggle;
                NodeVtable vtable = GraphNodes.Checkbox(
                    () => LegendName(panel),
                    () => it.State,
                    () => AgeWidgets.Toggle(it),
                    () => AgeWidgets.Operable(widget),
                    AgeWidgets.Raw(widget)
                );
                AgeWidgets.PointAt(vtable, widget);
                builder.AddItem(Nodes.Drawn(ControlId.For(toggle, "scan:legend/show"), vtable, toggle));

                if (!toggle.State)
                {
                    return;
                }

                CaptionGroups(builder, panel);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the legend threw: " + e);
            }
        }

        /// <summary>What the game writes on the tick, which is the word it uses for the legend.
        /// </summary>
        private static string LegendName(ScanViewWindowCaptionsPanel panel)
        {
            string drawn = AgeWidgets.TextOf(panel.ToggleBodyAgeTransform);
            return string.IsNullOrEmpty(drawn) ? ModStrings.Get(ModStrings.ScanLegend) : drawn;
        }

        /// <summary>
        /// The headings and their lines - as many of each as this lens HAS, which is not as many as the
        /// panel is holding.
        ///
        /// The panel is a pool. It reserves a widget per caption the lens declares and rebinds them, and
        /// it neither hides nor moves the ones a smaller lens does not need - so after a lens with three
        /// headings, a lens with one still has three in the tree, the last two carrying the previous
        /// lens's words, arranged past the bottom of the table where nothing draws them. Asking whether
        /// they are visible answers yes and declares a legend belonging to a lens the player left.
        ///
        /// So the COUNT comes from the lens's own data, filtered the way the panel filters it - the
        /// caption groups whose prerequisites hold (<c>ScanViewWindowCaptionsPanel.Refresh</c>, which is
        /// how content the player does not own drops out) - and the widgets are read in that order,
        /// because that is the order they were bound in.
        /// </summary>
        private void CaptionGroups(GraphBuilder builder, ScanViewWindowCaptionsPanel panel)
        {
            List<ScanViewCaptionGroupGuiElement> declared = Declared(panel);
            IList<AgeTransform> children =
                panel.CaptionsTable == null ? null : panel.CaptionsTable.Children;
            int groups = 0;
            for (int i = 0; children != null && i < children.Count && groups < declared.Count; i++)
            {
                ScanViewCaptionGroup group =
                    children[i] == null ? null : children[i].GetComponent<ScanViewCaptionGroup>();
                // Flow control: whether this pooled group is walked at all.
                if (group == null || !AgeWidgets.Visible(group.AgeTransform))
                {
                    continue;
                }

                ScanViewCaptionItemGuiElement[] items =
                    declared[groups].ScanViewCaptionItemGuiElements;
                string key = "scan:legend/" + i;
                // A heading the game draws over several lines is somewhere to be as well as a word: it
                // is the region the lines under it belong to, and a place the jump key can land - one
                // heading or twenty (owner ruling, 2026-08-18: a lone region's jump is swallowed
                // silently, and a section that appears with the count changes the panel's shape).
                builder.SetRegion(key);
                ScanViewCaptionGroup it = group;
                // The group widget draws the heading (its own Title is what is read), so it is the
                // evidence as well as the rectangle. It catches nothing the count above does not
                // already: a surplus group the pool retires stays Visible at alpha 1, parked past the
                // bottom of the table - measured 2026-08-27, both live groups Visible, alpha 1,
                // painted. The lens's own count is still what keeps those out.
                builder.AddItem(
                    Nodes.Drawn(
                        ControlId.Structural(key),
                        GraphBuilder.Label(() => AgeText.Label(it.Title), it.AgeTransform),
                        it.AgeTransform
                    )
                );
                CaptionItems(builder, group.ItemsTable, key, items == null ? 0 : items.Length);
                groups++;
            }

            builder.SetRegion(null);
        }

        /// <summary>The caption groups this lens declares and the player's content allows, in the order
        /// the panel binds them.</summary>
        private List<ScanViewCaptionGroupGuiElement> Declared(ScanViewWindowCaptionsPanel panel)
        {
            _legend.Clear();
            ScanViewWindowGuiElement element = panel.ScanViewGuiElement;
            ScanViewCaptionGroupGuiElement[] groups =
                element == null ? null : element.ScanViewCaptionGroupGuiElements;
            for (int i = 0; groups != null && i < groups.Length; i++)
            {
                if (Allowed(groups[i]))
                {
                    _legend.Add(groups[i]);
                }
            }

            return _legend;
        }

        private bool Allowed(ScanViewCaptionGroupGuiElement group)
        {
            Amplitude.Unity.Framework.Prerequisite[] prerequisites =
                group == null ? null : group.Prerequisites;
            for (int i = 0; prerequisites != null && i < prerequisites.Length; i++)
            {
                if (!prerequisites[i].Check(_prerequisites))
                {
                    return false;
                }
            }

            return true;
        }

        private static void CaptionItems(
            GraphBuilder builder,
            AgeTransform table,
            string key,
            int declared
        )
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            int items = 0;
            for (int i = 0; children != null && i < children.Count && items < declared; i++)
            {
                ScanViewCaptionItem item =
                    children[i] == null ? null : children[i].GetComponent<ScanViewCaptionItem>();
                // Flow control: whether this pooled line is walked at all.
                if (item == null || !AgeWidgets.Visible(item.AgeTransform))
                {
                    continue;
                }

                ScanViewCaptionItem it = item;
                // Same as the heading above: the item widget draws the line and is what it exists by.
                builder.AddItem(
                    Nodes.Drawn(
                        ControlId.Structural(key + "/" + i),
                        GraphBuilder.Label(() => AgeText.Label(it.Title), it.AgeTransform),
                        it.AgeTransform
                    )
                );
                items++;
            }
        }

        // ---- which lens ----

        /// <summary>
        /// The game's own name for the lens that is up.
        ///
        /// Read off the label the top-centre panel keeps for it. That panel is hidden in the scan view,
        /// but the game goes on writing the label from the layer service whichever mode it is in
        /// (<c>TopTitlePanel.LayerService_LayerDescriptorChanged</c>), which is what makes it the game's
        /// answer rather than a copy of the game's table. The lens's own title strip is the fallback: it
        /// is a different string - what the lens's window is called rather than what the mode is - which
        /// is exactly why arriving can say one and the title node the other without repeating itself.
        /// </summary>
        public string Name()
        {
            try
            {
                ScanViewWindowHeader header = DrawnHeader();
                if (header == null)
                {
                    // No strip drawn at all - a frame between lenses. Naming the mode is the honest
                    // answer; the panel's label is about the map's zoom layer and may name a lens that
                    // is not the one showing.
                    return ModStrings.Get(ModStrings.ScreenScanView);
                }

                GameOverlayWindow overlay = Window<GameOverlayWindow>();
                TopTitlePanel panel = overlay == null ? null : overlay.TopTitlePanel;
                string name = panel == null ? null : AgeText.Label(panel.ScanLabel);
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }

                name = AgeText.Label(header.TitleLabel);
                return string.IsNullOrEmpty(name)
                    ? ModStrings.Get(ModStrings.ScreenScanView)
                    : name;
            }
            catch (Exception)
            {
                return ModStrings.Get(ModStrings.ScreenScanView);
            }
        }

        /// <summary>Which layer of the map the camera is on, which is what decides the lens. Compared as
        /// text because that is what changing it means: the same descriptor re-applied is not a change.
        /// </summary>
        private static string Descriptor()
        {
            try
            {
                ILayerService service = Amplitude.Unity.Framework.Services.GetService<ILayerService>();
                StaticString current = service == null ? null : service.LayerDescriptorCurrent;
                return current == null ? null : current.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The title strip of the lens that is drawn. Every lens has one and the game hides all
        /// but the live one, so this is also the answer to "which lens is up" wherever one is needed.
        /// </summary>
        private ScanViewWindowHeader DrawnHeader()
        {
            ScanViewWindowHeader[] headers = Headers();
            for (int i = 0; i < headers.Length; i++)
            {
                // Synthetic node existence: which lens is up is exactly which strip the game is
                // drawing, and everything the component declares hangs off that one answer.
                if (headers[i] != null && AgeWidgets.Visible(headers[i].AgeTransform))
                {
                    return headers[i];
                }
            }

            return null;
        }

        private ScanViewWindowHeader[] Headers()
        {
            if (_headers != null)
            {
                return _headers;
            }

            List<ScanViewWindowHeader> found = new List<ScanViewWindowHeader>();
            AddHeader(found, Window<DiplomacyScanViewWindow>());
            AddHeader(found, Window<TradeScanViewWindow>());
            AddHeader(found, Window<EconomyScanViewWindow>());
            AddHeader(found, Window<StarSystemOverviewScanViewWindow>());
            AddHeader(found, Window<StarSystemManagementScanViewWindow>());
            AddHeader(found, Window<PlanetScanViewWindow>());
            _headers = found.ToArray();
            return _headers;
        }

        private static void AddHeader(List<ScanViewWindowHeader> found, Component window)
        {
            try
            {
                ScanViewWindowHeader header =
                    window == null
                        ? null
                        : window.GetComponentInChildren<ScanViewWindowHeader>(true);
                if (header != null)
                {
                    found.Add(header);
                }
            }
            catch (Exception e)
            {
                Log.Warn("scan: finding a lens title threw: " + e);
            }
        }

        private ScanViewSystemOverviewFidsiLabel[] Fidsi(StarSystemOverviewScanViewWindow window)
        {
            if (_fidsi == null)
            {
                try
                {
                    _fidsi = window.GetComponentsInChildren<ScanViewSystemOverviewFidsiLabel>(true);
                }
                catch (Exception e)
                {
                    Log.Warn("scan: finding the system outputs threw: " + e);
                    _fidsi = new ScanViewSystemOverviewFidsiLabel[0];
                }
            }

            return _fidsi;
        }

        private static TWindow Window<TWindow>()
            where TWindow : Amplitude.Unity.Gui.GuiWindow
        {
            try
            {
                return Gui.GuiServiceAvailable ? Gui.GuiService.GetWindow<TWindow>(false) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

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
    /// The scan view over a SYSTEM's page and a PLANET's page - the two rungs of the lens ladder that
    /// are not the galaxy.
    ///
    /// The scan view is a MODE rather than a place: the camera stays where it was and the game swaps
    /// every label for a different set. Which set is not a choice the player makes. The map's zoom step
    /// picks a layer descriptor (<c>GalaxyViewCameraController.LayerDescriptorNamesByZoomIndex</c>), the
    /// descriptor picks the lens, and the lens decides what the labels mean - so zooming does TWO
    /// jobs: it still changes how much is drawn, as it does everywhere, and it also SELECTS the lens.
    ///
    /// Over the GALAXY that mode is now worn by the galaxy page itself (owner ruling 2026-09-01): the
    /// lens is the same map under a different light, so the tree, the inspect cursor, the scanner, the
    /// bookmarks and the type-ahead are the same code paths rather than a second copy
    /// (<see cref="GalaxyHudScreen"/>). What is left here is the two rungs the galaxy page does not
    /// reach - the system-management lens over a system's own page, and the planet lens over a
    /// planet's - which is why this screen now asks the view LEVEL as well as the mode
    /// (<see cref="IsActive"/>).
    ///
    /// The lens's own furniture - the title strip, the announcement, the legend - belongs to no one
    /// page and is a component both wear (<see cref="ScanLensPanels"/>). The zoom ladder is this
    /// page's own, and needed here: the game's own wheel answers nothing at all once it is inside a
    /// system, so without a ladder the two page rungs would have no way back out.
    ///
    /// There is no key to get in and none to get out. Getting in is the scan button the game draws
    /// beside the view's name (GlobalHud.ViewTitle), and Escape and right-click are the game's own way
    /// out, which this screen deliberately does not consume.
    ///
    /// A battle's own Scan button enters this same game mode (<c>BattleScreen</c> calls
    /// <c>ToggleScanView</c>), but there it means something else entirely: an overlay of per-ship stats
    /// on the fight the player is already watching, not a lens over the map. So this screen stands down
    /// for the duration of a battle (<see cref="ScanLensPanels.BattleEnding"/>) and the battle screen
    /// keeps the player and the narration. The hacking dashboard and its banners ARE modelled now, as
    /// stops both pages wear (<c>ScanHacking</c> on <see cref="GalaxyHudScreen"/>); they read off
    /// drawn widgets, so a session without that content declares none of them - which is the game's
    /// own shape, since it switches all three off outright there
    /// (<c>ScanOverlayWindow.OnGameCreated</c>).
    ///
    /// <b>Why every drawn test in this file stays.</b> A lens row stands for a GAME ENTITY - a planet,
    /// a hero - and not for the label the renderer happens to be drawing it with, so almost every
    /// declaration here is <see cref="UI.Nodes.Synthetic"/> and the central gate has nothing to ask of
    /// it. The honesty about whether an entity is on the screen therefore lives in these walks: each
    /// asks the label it read the entity off whether the renderer is drawing it, and a walk that
    /// stopped asking would announce whatever the label held for the last camera position.
    /// </summary>
    public sealed class ScanViewScreen : Screen
    {
        private static readonly object ContentStop = "scan:content";

        private static readonly object TradeRegion = "scan:content/trade";
        private static readonly object PlanetsRegion = "scan:content/planets";
        private static readonly object HeroRegion = "scan:content/hero";
        private static readonly object RemainsRegion = "scan:content/remains";

        /// <summary>The clusters the game keeps drawing over the lens - which is only the turn controls;
        /// it hides the banners, the pinned quest and the notification strip.</summary>
        private readonly GlobalHud _hud = new GlobalHud();

        /// <summary>The lens's own furniture, the same component the galaxy page wears in-mode.
        /// </summary>
        private readonly ScanLensPanels _lens = new ScanLensPanels();

        /// <summary>How close the game is looking, which on this page is also WHICH LENS is being read -
        /// the same control the map itself offers (<see cref="ZoomLadder"/>).</summary>
        private readonly ZoomLadder _zoom = new ZoomLadder();

        public override string Key
        {
            get { return ModStrings.ScreenScanView; }
        }

        /// <summary>Just above the view levels it overlays and well below everything that can be raised
        /// over it. It is not one of them: the game keeps the view level underneath and merely stops
        /// calling it normal, so this screen is drawn over the galaxy, a system or a planet alike.
        /// </summary>
        public override int Layer
        {
            get { return 11; }
        }

        /// <summary>The game's own name for the lens that is up. Said on arrival, which is the whole
        /// point: the lens is what the mode MEANS.</summary>
        public override string ScreenName
        {
            get { return _lens.Name(); }
        }

        /// <summary>
        /// Ours while the game is in the scan view over a SYSTEM's or a PLANET's page and nothing has
        /// replaced it.
        ///
        /// The view level is now asked, where once it was deliberately not (owner ruling 2026-09-01):
        /// the lens over the GALAXY is the same map the galaxy page already models, so that page wears
        /// it and this one stands down there - <c>GalaxyViewLevels.Overview</c> is the whole of the
        /// division, and the two pages can never be up together because it is one question answered
        /// twice.
        ///
        /// Arriving waits for the lens to have drawn ITSELF (<see cref="ScanLensPanels.Arrived"/>).
        /// The game turns "normal view" off a good number of frames before it shows the lens's own
        /// windows, and for those frames the only thing on the screen is the turn controls - so a
        /// screen that arrived on the mode alone seated the cursor on the End Turn button and left it
        /// there, because a cursor is placed once.
        ///
        /// Which is asked as <c>IsInGalaxyScanView</c> rather than as the raw <c>IsInScanView</c> flag,
        /// because the game already has a word for "the scan view is what is up" and it is the narrower
        /// one: the same flag is also the battle's per-ship stats overlay, the ground battle's, and the
        /// system-discovery and planet-destruction cinematics', each of which is its own event with its
        /// own screen and its own things to say (owner ruling 2026-08-30). Borrowing the game's compound
        /// keeps this page standing down from all five without a list of its own to keep in step.
        ///
        /// And not while the battle is still leaving the screen
        /// (<see cref="ScanLensPanels.BattleEnding"/>), which the game's own compound does not cover.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                GuiManager gui = GuiState();
                bool scanning =
                    gui != null
                    && gui.IsInGalaxyScanView
                    && !GalaxyViewLevels.Overview
                    && !ScanLensPanels.BattleEnding()
                    && !gui.IsAnyScreenVisible
                    && !gui.IsAnyModalVisible
                    && !gui.IsInLoadingWindow;
                return _lens.Arrived(scanning);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape is the game's: it is what leaves the lens, and the mod inventing a way out of
        /// a mode the game already knows how to leave would be a second answer to one question.
        /// </summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        public override bool Back()
        {
            return false;
        }

        public override object InitialFocusStop
        {
            get { return ScanLensPanels.TitleStop; }
        }

        /// <summary>Whether a landing aimed at this page should be held rather than judged - true while
        /// the game is flying the camera between view levels, exactly as on the map
        /// (<c>GalaxyHudScreen.LandingSuspended</c>). This overlay stands on those levels too: it is the
        /// scan of a system at one rung and of the galaxy at the next, and while the flight is on what
        /// it declares describes neither. Measured 2026-09-02: a zoom step onto this page landed its
        /// seat mid-flight and read "Zoom, slider, 2 of 2" - the ladder without the rung, because the
        /// rung is a step behind until the flight ends (<see cref="ZoomLadder"/>) - and then the rung
        /// alone a moment later.</summary>
        public override bool LandingSuspended
        {
            get { return GalaxyViewLevels.ChangingLevel || GalaxyViewLevels.CameraSettling; }
        }

        public override void OnPush()
        {
            _hud.Baseline();
            _lens.Baseline();
        }

        public override void OnPop()
        {
            _zoom.Forget();
            _hud.Forget();
            _lens.Forget();
        }

        public override void OnUpdate()
        {
            _hud.Update();
            _lens.Announce();
            _zoom.Update();
        }

        /// <summary>Down the screen: the lens's own title across the top, then what the lens draws over
        /// the page, then the legend down the left edge, then the turn controls in the corner.</summary>
        public override void Build(GraphBuilder builder)
        {
            builder.BeginStop(ScanLensPanels.TitleStop);
            _lens.Title(builder);
            _zoom.Build(builder, "scan:zoom");

            builder.BeginStop(ContentStop);
            BuildSystemManagement(builder);
            BuildPlanet(builder);

            // The overlay's own panels, drawn over these two rungs as they are over the map's
            // (<see cref="ScanLensPanels.Hacking"/>): the family reads what is on the screen, so the
            // rungs where the game stops drawing one of them declare nothing for it.
            _lens.Hacking(builder);
            builder.BeginStop(ScanLensPanels.LegendStop);
            _lens.Legend(builder);

            _hud.Turn(builder);
        }

        // ---- the system management lens ----

        /// <summary>
        /// The lens over a system's own page: what makes the system worth trading with, and then one
        /// label per planet in it.
        ///
        /// Two captioned groups, so two regions. The planets' figures are the exception to reading what
        /// is drawn: the lens writes each output as a bare number beside its icon, and a spoken "16" is
        /// nothing, so the numbers are read from the planet with the output's own title in front of
        /// them - the five properties the label itself uses
        /// (<c>PlanetLabel_SystemManagementScanView.PlanetFidsiProperties</c>).
        ///
        /// A card SAYS all of itself. It is not a container the player opens - the game draws it as one
        /// thing, four items on a card the size of a stamp - so its state and its synergies are parts of
        /// its readout rather than something to go and find, and the buffer is the same content a line at
        /// a time (<see cref="CardLines"/>). Nothing on the card is left to a tooltip, because it has
        /// none: the status mark carries no <c>AgeTooltip</c> at all, on the prefab or on the data.
        /// </summary>
        private void BuildSystemManagement(GraphBuilder builder)
        {
            StarSystemManagementScanViewWindow window =
                GameWindows.Of<StarSystemManagementScanViewWindow>();
            if (window == null || !window.Shown)
            {
                return;
            }

            try
            {
                // Content: the trading lines are this system's only while the group is drawn.
                IList<string> trade = AgeWidgets.Visible(window.TradingGroup)
                    ? AgeWidgets.DrawnLines(window.TradingGroup)
                    : null;
                IList<AgeTransform> children =
                    window.PlanetLabelsGroup == null ? null : window.PlanetLabelsGroup.Children;
                // Every section the lens draws is a region, however many of them there happen to be: a
                // lone region's jump is swallowed silently, and a section that comes and goes with the
                // count is a panel that changes shape under the player (owner ruling, 2026-08-18).
                builder.SetRegion(TradeRegion);
                AddDrawnLines(builder, trade, "scan:trade");

                AddHeroPanel(builder, window);

                builder.SetRegion(PlanetsRegion);
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    PlanetLabel_SystemManagementScanView label =
                        children[i] == null
                            ? null
                            : children[i].GetComponent<PlanetLabel_SystemManagementScanView>();
                    // Flow control: a label the lens is not drawing is not one of this system's planets, and is not walked.
                    if (
                        label == null
                        || label.Planet == null
                        || !AgeWidgets.Visible(label.AgeTransform)
                    )
                    {
                        continue;
                    }

                    PlanetLabel_SystemManagementScanView it = label;
                    Planet planet = label.Planet;
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => AgeText.Label(it.PlanetTitle)),
                            GraphNodes.ValuePart(() => Outputs(planet)),
                            GraphNodes.ValuePart(() => Status(planet), false),
                            GraphNodes.ValuePart(() => SynergyText(it), false),
                        },
                        // No sections: the card has no tooltip anywhere on it and nothing it holds is
                        // hidden from the readout, so the buffer the readout itself makes - a line per
                        // part - already is the card a line at a time.
                        // The click a planet's own body takes from a system's page, which is the game's
                        // only route from here to one planet: the lens follows the level, so this is
                        // also the way from the system's planets to the planet's own data sheet.
                        OnActivate = () => GalaxyViewLevels.OpenPlanet(planet),
                    };
                    AgeWidgets.PointAt(vtable, label.AgeTransform);
                    // Synthetic: the row stands for the PLANET; the walk over the drawn planet labels
                    // above is what says it is on the screen.
                    builder.AddItem(Nodes.Synthetic(
                        ControlId.For(planet, "scan:planet/" + planet.GUID),
                        vtable
                    ));
                }

                builder.SetRegion(null);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the system management lens threw: " + e);
            }
        }

        // ---- the governor's panel ----

        /// <summary>The panel the lens draws along the bottom for a system that has a governor - drawn
        /// only where one is assigned (<c>StarSystemManagementScanViewWindow.Bind</c>), so its absence
        /// is the answer "nobody governs this system" and there is nothing to declare.</summary>
        private static bool HeroPanelDrawn(StarSystemManagementScanViewWindow window)
        {
            StarSystemManagementScanViewHeroPanel panel =
                window == null ? null : window.HeroPanel;
            return panel != null && AgeWidgets.Painted(panel.AgeTransform);
        }

        /// <summary>
        /// The governor of the system, as the lens draws them: a heading, a portrait, a dial, and the
        /// outputs they are adding to the system.
        ///
        /// One node, like the planet cards beside it: the game draws it as one small card and there is
        /// nothing on it to work, so its parts ARE its readout and the buffer they make is the card a
        /// line at a time.
        ///
        /// Two of the three things on it have no words at all, and both are named here rather than
        /// left out. The PORTRAIT is who the governor is - the one thing a sighted player reads the
        /// panel for - and the hero's name is written nowhere on the panel, so it is taken from the
        /// panel's own bound hero. The DIAL is a pie: the game fills it with the share of this
        /// governor's system skills whose effects actually apply here
        /// (<c>StarSystemManagementScanViewHeroPanel.RefreshEfficiency</c> counts them and turns the
        /// ratio into an angle), and the angle it drew is read back as the percentage it is, rather
        /// than the skill count being re-derived - the drawn angle IS the value, and re-deriving it
        /// would be a second implementation of the game's own counting rules to keep in step.
        ///
        /// Everything else is words the panel draws: the two captions and the FIDSI bonus (or the
        /// "None" the game writes where the governor adds nothing), read in drawn order.
        /// </summary>
        private static void AddHeroPanel(
            GraphBuilder builder,
            StarSystemManagementScanViewWindow window
        )
        {
            if (!HeroPanelDrawn(window))
            {
                return;
            }

            builder.SetRegion(HeroRegion);
            StarSystemManagementScanViewHeroPanel it = window.HeroPanel;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => HeroHeading(it)),
                    GraphNodes.ValuePart(() => HeroReadout(it), false),
                },
            };
            AgeWidgets.PointAt(vtable, it.AgeTransform);
            ScrollIntoView.Anchor(vtable, it.AgeTransform);
            // Synthetic: the row is composed from the hero the panel is bound to; HeroPanelDrawn()
            // above, which asks the panel whether it is painted, is the honesty about it.
            builder.AddItem(Nodes.Synthetic(ControlId.Structural("scan:hero"), vtable));
        }

        /// <summary>The panel's own caption, which is the first thing it draws.</summary>
        private static string HeroHeading(StarSystemManagementScanViewHeroPanel panel)
        {
            IList<string> lines = AgeWidgets.PaintedLines(panel.AgeTransform);
            return lines.Count == 0 ? null : lines[0];
        }

        /// <summary>Who governs here and how well, then the rest of what the panel draws - the output
        /// half's caption and its figures - in the order they are on the screen.</summary>
        private static string HeroReadout(StarSystemManagementScanViewHeroPanel panel)
        {
            MessageBuilder message = new MessageBuilder();
            message.ListItem(HeroName(panel));
            message.ListItem(HeroEfficiency(panel));
            IList<string> lines = AgeWidgets.PaintedLines(panel.AgeTransform);
            for (int i = 1; i < lines.Count; i++)
            {
                message.ListItem(lines[i]);
            }

            return message.Build();
        }

        /// <summary>The hero the panel is bound to. Held privately - the panel draws a face and never a
        /// name - so it is read through the field itself, looked up once.</summary>
        private static readonly System.Reflection.FieldInfo HeroField = GameHandlers.Field(
            typeof(StarSystemManagementScanViewHeroPanel),
            "guiHero"
        );

        private static string HeroName(StarSystemManagementScanViewHeroPanel panel)
        {
            try
            {
                GuiHero hero = HeroField == null ? null : HeroField.GetValue(panel) as GuiHero;
                return hero == null ? null : AgeText.Clean(hero.Title);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the governor's name threw: " + e);
                return null;
            }
        }

        /// <summary>How much of the dial the game filled in, as the percentage it draws. A full circle
        /// is everything this governor knows applying here.</summary>
        private static string HeroEfficiency(StarSystemManagementScanViewHeroPanel panel)
        {
            AgePrimitiveSector sector = panel.EfficiencySector;
            if (sector == null)
            {
                return null;
            }

            return ModStrings.Format(
                ModStrings.ScanHeroEfficiency,
                Mathf.RoundToInt(sector.MaxAngle / 3.6f)
            );
        }

        /// <summary>
        /// The strip of icon pairs the lens lines up under a planet's ring: one per way a population of
        /// the empire's suits that planet, drawn as the population's face beside the output it gains and
        /// captioned once above them all.
        ///
        /// Both pictures are named from the icon table - the same reading the mod gives an icon anywhere
        /// else - because the item carries no words and no tooltip of ANY kind (measured on the drawn
        /// cards: no <c>AgeTooltip</c> on the item, on either image, or on the table), so there is nothing
        /// else on the widget to read. The game's own caption ("Population synergies") heads the list.
        ///
        /// The caption label stays drawn over an EMPTY table, and the sighted player sees exactly that -
        /// a heading with nothing under it on every planet - so the reading says the same (OWNER-RATIFIED
        /// 2026-08-13: parity with the sighted experience; drawn words are never deleted). The pairs
        /// follow only where the table has them.
        /// </summary>
        private static string SynergyText(PlanetLabel_SystemManagementScanView label)
        {
            try
            {
                MessageBuilder heading = new MessageBuilder();
                heading.Fragment(Caption(label));
                AgeTransform table = label.SynergiesTable;
                if (table == null || !AgeWidgets.Visible(table))
                {
                    return heading.Build();
                }

                MessageBuilder message = new MessageBuilder();
                IList<AgeTransform> children = table.Children;
                int said = 0;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    StarSystemManagementScanViewPopulationSynergyItem item =
                        children[i] == null
                            ? null
                            : children[i].GetComponent<
                                StarSystemManagementScanViewPopulationSynergyItem
                            >();
                    // Flow control: an item the table is not drawing is not one of this population's synergies.
                    if (item == null || !AgeWidgets.Visible(item.AgeTransform))
                    {
                        continue;
                    }

                    string pair = SynergyPair(item);
                    if (string.IsNullOrEmpty(pair))
                    {
                        continue;
                    }

                    if (said == 0)
                    {
                        message.Fragment(Caption(label));
                    }

                    message.ListItemForcedComma(pair);
                    said++;
                }

                return said == 0 ? heading.Build() : message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One synergy: which population and what it gets, in the words the icon table gives the
        /// two pictures. Null when neither picture has a name, which is the icon table saying this is
        /// decoration.</summary>
        private static string SynergyPair(StarSystemManagementScanViewPopulationSynergyItem item)
        {
            string population = Picture(item.AffinityIcon);
            string output = Picture(item.FidsiIcon);
            if (string.IsNullOrEmpty(population))
            {
                return output;
            }

            return string.IsNullOrEmpty(output)
                ? population
                : ModStrings.Format(ModStrings.ScanSynergy, population, output);
        }

        private static string Picture(AgePrimitiveImage image)
        {
            Texture texture = image == null ? null : image.Image;
            return texture == null ? null : IconNames.NameForAsset(texture.name);
        }

        /// <summary>The caption the game writes over the synergies strip, read off the strip's own group -
        /// the caption belongs to the group rather than to the table, so the table alone would lose it.
        /// </summary>
        private static string Caption(PlanetLabel_SystemManagementScanView label)
        {
            AgeTransform table = label.SynergiesTable;
            AgeTransform group = table == null ? null : table.Parent;
            return group == null ? null : AgeWidgets.TextOf(group);
        }

        /// <summary>
        /// What the lens is painting the planet as, in the game's own word for that state.
        ///
        /// The card draws it as one icon and nothing else - the legend's colonized or hospitable mark, or
        /// the picture of whichever improvement stands on a colony - and the icon table names none of
        /// them. So the state comes from the model through the wrapper the game asks the same question of
        /// (<c>GuiPlanet.PlanetStatus</c>, the same expression the map's own planet circles are read by),
        /// which answers with more than the three the icons distinguish: whose colony, whose outpost,
        /// destroyed, hostile, or free to settle.
        /// </summary>
        private static string Status(Planet planet)
        {
            try
            {
                GuiPlanet.PlanetStatuses status = new GuiPlanet(planet).PlanetStatus;
                return AgeText.Clean(Gui.Localize("%PlanetStatus" + status + "Title"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a planet is putting out, as the lens rings it: each of the five raw outputs the
        /// label draws, named, and the ones it is producing none of left out - the lens hides those
        /// sectors rather than drawing a zero.</summary>
        private static string Outputs(Planet planet)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                StaticString[] properties =
                    PlanetLabel_SystemManagementScanView.PlanetFidsiProperties;
                for (int i = 0; i < properties.Length; i++)
                {
                    float value = planet.GetPropertyValue(properties[i]);
                    if (value == 0f)
                    {
                        continue;
                    }

                    message.ListItem(
                        ModStrings.Format(
                            ModStrings.ScanOutput,
                            GlobalHud.Amount(value, false, 0),
                            Gui.GetLocalizedTitle(properties[i])
                        )
                    );
                }

                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the planet lens ----

        /// <summary>
        /// The lens over a planet's own page is a data sheet and nothing else: two columns of captioned
        /// categories, each a list of "name, value, unit" lines the game composes from the planet's type
        /// and its tags. One region per category, one node per line.
        ///
        /// A line whose value is a list - what an atmosphere is made of - draws the parts in one label
        /// and the percentages in another, side by side, so the two are read together part by part.
        ///
        /// The third table is not statistics at all: what is LEFT on the planet - a wreck, a ruin, the
        /// remnants of somebody else's war - each drawn as a title and a paragraph under the right-hand
        /// column, and each one a thing the scan is there to find.
        /// </summary>
        private void BuildPlanet(GraphBuilder builder)
        {
            PlanetScanViewWindow window = GameWindows.Of<PlanetScanViewWindow>();
            if (window == null || !window.Shown)
            {
                return;
            }

            try
            {
                AgeTransform left = window.PlanetStatsCategoryItemsTableLeft;
                AgeTransform right = window.PlanetStatsCategoryItemsTableRight;
                AgeTransform remains = window.PlanetRemainsItemsTable;
                AddCategories(builder, left, "left");
                AddCategories(builder, right, "right");
                AddRemains(builder, remains);
                builder.SetRegion(null);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the planet lens threw: " + e);
            }
        }

        private static PlanetRemainsItem Remain(AgeTransform widget)
        {
            PlanetRemainsItem item =
                widget == null ? null : widget.GetComponent<PlanetRemainsItem>();
            // Different widget: the remains item the lens is drawing, which is what the pointer goes to.
            return item != null && AgeWidgets.Painted(item.AgeTransform) ? item : null;
        }

        /// <summary>One node per thing left on the planet - its name and the paragraph the lens writes
        /// under it, which is the whole of what the game says about it.</summary>
        private static void AddRemains(GraphBuilder builder, AgeTransform table)
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                PlanetRemainsItem item = Remain(children[i]);
                if (item == null)
                {
                    continue;
                }

                builder.SetRegion(RemainsRegion);
                PlanetRemainsItem it = item;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeText.Label(it.Title)),
                        GraphNodes.ValuePart(() => AgeText.Label(it.Description), false),
                    },
                };
                AgeWidgets.PointAt(vtable, item.AgeTransform);
                ScrollIntoView.Anchor(vtable, item.AgeTransform);
                // Synthetic: Remain() above, which asks each pooled item whether it is painted, is
                // what says this remains entry is really drawn.
                builder.AddItem(Nodes.Synthetic(ControlId.Structural("scan:remains/" + i), vtable));
            }
        }

        private static void AddCategories(GraphBuilder builder, AgeTransform table, string side)
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                PlanetStatsCategoryItem category =
                    children[i] == null
                        ? null
                        : children[i].GetComponent<PlanetStatsCategoryItem>();
                // Flow control: a category the table is not drawing is not one of this planet's, and is not walked.
                if (category == null || !AgeWidgets.Visible(category.AgeTransform))
                {
                    continue;
                }

                string key = "scan:stats/" + side + "/" + i;
                builder.SetRegion(key);
                PlanetStatsCategoryItem it = category;
                // The category item is the heading: its own Title is what the node says, so the widget
                // the words were read off is both where the heading is drawn and what it exists by.
                builder.AddItem(
                    Nodes.Drawn(
                        ControlId.Structural(key),
                        GraphBuilder.Label(() => AgeText.Label(it.Title), it.AgeTransform),
                        it.AgeTransform
                    )
                );
                AddStatLines(builder, category.StatLinesTable, key);
            }
        }

        private static void AddStatLines(GraphBuilder builder, AgeTransform table, string key)
        {
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                PlanetStatLine line =
                    children[i] == null ? null : children[i].GetComponent<PlanetStatLine>();
                // Flow control: a line the table is not drawing carries no figure of this category's.
                if (line == null || !AgeWidgets.Visible(line.AgeTransform))
                {
                    continue;
                }

                PlanetStatLine it = line;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeText.Label(it.Title)),
                        GraphNodes.ValuePart(() => StatValue(it)),
                    },
                };
                AgeWidgets.PointAt(vtable, line.AgeTransform);
                ScrollIntoView.Anchor(vtable, line.AgeTransform);
                // Synthetic: the loop above, which asks each pooled line whether it is drawn, is what
                // says this stat line is there.
                builder.AddItem(Nodes.Synthetic(ControlId.Structural(key + "/" + i), vtable));
            }
        }

        /// <summary>A statistic's value and its unit, and for a breakdown the parts paired with their
        /// shares: the game draws the names in one column and the numbers in another, one line each, so
        /// the Nth name belongs to the Nth number.</summary>
        private static string StatValue(PlanetStatLine line)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                IList<string> values = AgeText.Lines(AgeText.Label(line.Value));
                IList<string> names = !string.IsNullOrEmpty(AgeWidgets.DrawnLabel(line.DetailTitle))
                    ? AgeText.Lines(AgeText.Label(line.DetailTitle))
                    : null;
                for (int i = 0; values != null && i < values.Count; i++)
                {
                    string name = names != null && i < names.Count ? names[i] : null;
                    message.ListItem(
                        name == null
                            ? values[i]
                            : ModStrings.Format(ModStrings.ScanOutput, values[i], name)
                    );
                }

                if (!string.IsNullOrEmpty(AgeWidgets.DrawnLabel(line.Unit)))
                {
                    message.ListItem(AgeText.Label(line.Unit));
                }

                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A read-only panel as one node per line of words it draws.</summary>
        private static void AddDrawnLines(GraphBuilder builder, IList<string> lines, string key)
        {
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                string line = lines[i];
                // Synthetic: these are LINES scraped out of a panel, not controls - there is no one
                // widget any of them is drawn by.
                builder.AddItem(
                    Nodes.Synthetic(
                        ControlId.Structural(key + "/" + i),
                        GraphBuilder.Label(() => line)
                    )
                );
            }
        }

        private static GuiManager GuiState()
        {
            try
            {
                return Gui.GuiGameWindowService as GuiManager;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

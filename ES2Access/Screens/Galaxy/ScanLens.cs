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
    public sealed partial class ScanLensPanels
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

        /// <summary>The stop the System lens's own panel is read in - a Tab stop of its own since
        /// 2026-09-01 (<see cref="SystemInformation"/>).</summary>
        public static readonly object SystemInfoStop = "scan:system";

        /// <summary>Whether the toggle was on last time it was looked at, and whether it has been
        /// looked at since the lens arrived - the baseline that keeps entering the lens from
        /// announcing a panel the player has not touched (<see cref="WatchSystemInfo"/>).</summary>
        private bool _infoShown;

        private bool _infoKnown;

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
            _infoKnown = false;
            _descriptor = Descriptor();
        }

        /// <summary>Given back when the page goes.</summary>
        public void Forget()
        {
            _headers = null;
            _fidsi = null;
            _arrived = false;
            _infoKnown = false;
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
        /// THE SYSTEM LENS'S PANEL IS A TAB STOP OF ITS OWN (owner ruling 2026-09-01, after playtest).
        ///
        /// The lens inspects ONE system - whichever is nearest the middle of the screen
        /// (<c>StarSystemOverviewScanViewWindow.MaxDistanceToScreenCenter</c>) - and it draws a page's
        /// worth about it: the figures ringing the star, a rank per property, two curves over the whole
        /// game, and whatever is left standing on one of its worlds. That was a single collapsed group
        /// at the head of the MAP stop, which put a page inside a list of stars and left the map stop's
        /// system rows reading as though the lens said nothing about them. So the map stop keeps only
        /// what the lens paints on the map - the names and the lanes - and everything about the one
        /// system the panel is inspecting is a stop after it, in REGIONS: the outputs, the rank, and the
        /// remains.
        ///
        /// Which system it is about is the game's own choice and not the tree cursor's; entering the
        /// stop from a row about a DIFFERENT star re-centres the camera on that star first, so the
        /// answer is the one the player asked for (<c>GalaxyHudScreen.CentreOnScanSystem</c>).
        ///
        /// Each region is here exactly while the game is drawing what it reads: the outputs whenever
        /// the lens has bound a colony, the rank and the remains only while the information tick is on
        /// - and the tick's own opening and closing is announced, because a whole stop's worth of
        /// content appearing under the player is otherwise silent (<see cref="WatchSystemInfo"/>).
        /// </summary>
        public void SystemInformation(GraphBuilder builder)
        {
            StarSystemOverviewScanViewWindow window = Window<StarSystemOverviewScanViewWindow>();
            if (window == null || !window.Shown || !AgeWidgets.Visible(window.NodeInfoGroup))
            {
                return;
            }

            bool open = false;
            try
            {
                builder.BeginStop(SystemInfoStop);
                builder.PushContext(ModStrings.Get(ModStrings.ScanSystemInfo));
                open = true;
                SystemName(builder, window);
                SystemInfoToggle(builder, window);
                Outputs(builder, window);
                Rank(builder, window);
                Remains(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the system lens threw: " + e);
            }
            finally
            {
                builder.SetRegion(null);
                if (open)
                {
                    builder.PopContext();
                }
            }
        }

        /// <summary>Which system the panel is about, and everything the strip itself draws - the name,
        /// the top line, and the sentence the game writes instead of all of it for somebody else's
        /// colony - as this row's review buffer.</summary>
        private static void SystemName(
            GraphBuilder builder,
            StarSystemOverviewScanViewWindow window
        )
        {
            StarSystemOverviewScanViewWindow it = window;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.NodeNameLabel)),
                },
                Sections = GraphNodes.Sections(NodeSection.Buffer(() => SystemLines(it))),
            };
            AgeWidgets.PointAt(vtable, window.NodeInfoGroup);
            builder.AddItem(
                Nodes.Drawn(
                    ControlId.For(window.NodeInfoGroup, "scan:system/name"),
                    vtable,
                    window.NodeInfoGroup
                )
            );
        }

        /// <summary>
        /// THE SIX FIGURES THE LENS RINGS THE STAR WITH - the five outputs and how full the system is.
        ///
        /// Drawn whenever the lens has a colony of the player's in the middle of the screen, and NOT
        /// governed by the information tick: the labels are their own panel, positioned around the star
        /// rather than inside the box the tick opens (measured 2026-09-01 - the tick off, the six
        /// labels still visible and bound). So the region is always here where the lens is drawing
        /// them, and undeclared where it is not.
        ///
        /// The game writes each one as a number and the output's own icon, and the icon has a name, so
        /// nothing is composed here.
        /// </summary>
        private void Outputs(GraphBuilder builder, StarSystemOverviewScanViewWindow window)
        {
            ScanViewSystemOverviewFidsiLabel[] labels = Fidsi(window);
            int drawn = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                // Spoken count: whether the region exists at all, and a region with nothing in it is
                // a Ctrl+arrow jump that lands nowhere. The labels are a POOL the panel keeps for the
                // whole session and hides when it has no colony bound, so how many are on the screen
                // is the only answer.
                if (labels[i] != null && AgeWidgets.Visible(labels[i].AgeTransform))
                {
                    drawn++;
                }
            }

            if (drawn == 0)
            {
                return;
            }

            builder.SetRegion("scan:system/outputs");
            builder.PushContext(ModStrings.Get(ModStrings.ScanSystemOutputs));
            for (int i = 0; i < labels.Length; i++)
            {
                ScanViewSystemOverviewFidsiLabel label = labels[i];
                // Flow control: the same pool question again, per label - a hidden one still holds the
                // last system's figure.
                if (label == null || !AgeWidgets.Visible(label.AgeTransform))
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

            builder.PopContext();
            builder.SetRegion(null);
        }

        /// <summary>
        /// HOW THIS SYSTEM STANDS AGAINST THE PLAYER'S OTHERS, and how it has stood since they found it.
        ///
        /// The game draws three things here and the mod read none of them. A SENTENCE - "Overall system
        /// rank 2 of 4 known systems" - which is drawn text and is simply read
        /// (<c>ScanViewSystemGlobalRankHistogram.Bind</c> :69). A BAR per ranking property, whose height
        /// is the only place the rank lives: the game writes an ordinal on it and only while the system
        /// is in the first four (<c>ScanViewSystemEmpireRankBar.Refresh</c> :35-38), so the rank is
        /// recomposed the way the game counts it - one place per other system of the player's holding
        /// more of that property (:55-83). And TWO CURVES over the turns, which are geometry and no
        /// words at all: those become a table, because a curve read aloud is a table.
        ///
        /// ONLY THE CURVES ARE A TABLE (owner ruling 2026-09-01, playtest). The sentence and the
        /// per-property lines are READOUT ROWS standing above it - cell semantics and a place in a
        /// thirty-row column were being spoken over four lines that are not tabular at all - so they
        /// are ordinary rows of this region and the sheet holds the curves alone. Region order is the
        /// panel's own: the sentence, the properties, then the history.
        ///
        /// The region is ONE region and not two, because the game itself draws it as one: the legend
        /// lists the bars AND the systems curve under the single caption group this takes its name from
        /// (measured live - "System's Rank" over "No. of systems in my Empire", "FIDSI", "Defense",
        /// "Population", "No. of representatives"). So the readouts are declared inside the sheet's own
        /// region, which is what the <c>economy:history</c> block does with the sentence above its
        /// table, and the seam between the two kinds of row is stitched by the builder
        /// (<c>GraphBuilder.StitchModeBoundaries</c>).
        ///
        /// The whole region belongs to the panel the tick opens, so it is here exactly while that panel
        /// is drawn - and the bars and the curves are bound only for a colony of the player's own
        /// (<c>ScanViewSystemOverviewInfoPanel.Bind</c> :63-68), which is the game's own answer to
        /// "whose figures are these to see".
        /// </summary>
        private void Rank(GraphBuilder builder, StarSystemOverviewScanViewWindow window)
        {
            ScanViewSystemOverviewInfoPanel panel = window.InfoPanel;
            ColonizedStarSystem colony =
                panel == null || panel.FidsiLabelsPanel == null
                    ? null
                    : panel.FidsiLabelsPanel.ColonizedStarSystem;
            ScanViewSystemEmpireRankBarGraph bars = panel == null ? null : panel.BarGraph;
            ScanViewSystemGlobalRankHistogram curves =
                panel == null ? null : panel.GlobalRankHistogram;
            // Flow control: the whole region exists only while the game is drawing the panel it lives
            // in, with a system of the player's own bound to it.
            if (
                panel == null
                || !panel.Shown
                || colony == null
                || bars == null
                || !AgeWidgets.Visible(bars.AgeTransform)
            )
            {
                return;
            }

            // The turns are read BEFORE the region is opened because they ARE the columns: a sheet is
            // told its whole header list once, in front of its rows.
            List<string> turns = new List<string>();
            List<Func<string>> ranks = new List<Func<string>>();
            List<Func<string>> known = new List<Func<string>>();
            RankHistory(colony, turns, ranks, known);

            GraphSheet sheet = new GraphSheet(builder, "scan:system/rank/");
            sheet.Region(RankCaption(bars), TurnColumns(turns));
            RankSentence(builder, curves);
            RankProperties(builder, window, bars, colony);
            RankCurves(sheet, window, bars, turns, ranks, known);
            sheet.Finish();
            builder.SetRegion(null);
        }

        /// <summary>The game's own word for this block, off the caption group the bar graph is bound
        /// with - the same words the legend down the left edge lists it under.</summary>
        private static string RankCaption(ScanViewSystemEmpireRankBarGraph bars)
        {
            try
            {
                ScanViewCaptionGroupGuiElement group = bars.BarGraphCaptionGroup;
                string drawn = group == null ? null : AgeText.Clean(Gui.Localize(group.Title));
                return string.IsNullOrEmpty(drawn)
                    ? ModStrings.Get(ModStrings.ScanSystemRankRegion)
                    : drawn;
            }
            catch (Exception)
            {
                return ModStrings.Get(ModStrings.ScanSystemRankRegion);
            }
        }

        /// <summary>THE COLUMNS ARE THE TURNS, NEWEST FIRST (owner ruling 2026-09-02, restoring the
        /// ordering the rows-to-columns pivot lost): the turn being played is the first data column, so
        /// entering a row reaches the reading that is true NOW in one press and walking right walks
        /// back in time. The primary column names the curve rather than anything the game captions, so
        /// its own header is empty.</summary>
        private static string[] TurnColumns(List<string> turns)
        {
            string[] headers = new string[turns.Count + 1];
            for (int i = 0; i < turns.Count; i++)
            {
                headers[i + 1] = turns[i];
            }

            return headers;
        }

        /// <summary>
        /// The game's caption for the systems curve, found the way the bar graph finds its own.
        ///
        /// The block's caption group lists one item per bar plus one more, and that one more is the
        /// curve: the game itself tells the two apart by matching a bar's ranking-property name against
        /// the item names (<c>ScanViewSystemEmpireRankBarGraph.BindBar</c> :103-112), so the item that
        /// matches NO ranking property is the one left over. Asked of the data rather than written down
        /// as a localization key, because the key would silently stop matching if the group were re-cut.
        /// </summary>
        private static string KnownSystemsCaption(
            StarSystemOverviewScanViewWindow window,
            ScanViewSystemEmpireRankBarGraph bars
        )
        {
            try
            {
                ScanViewCaptionGroupGuiElement group = bars.BarGraphCaptionGroup;
                StarSystemOverviewScanViewGuiElement element = window.SystemOverviewGuiElement;
                ScanViewCaptionItemGuiElement[] items =
                    group == null ? null : group.ScanViewCaptionItemGuiElements;
                StarSystemOverviewScanViewGuiElement.EmpireRankingProperty[] properties =
                    element == null ? null : element.EmpireRankingProperties;
                for (int i = 0; items != null && properties != null && i < items.Length; i++)
                {
                    bool ranked = false;
                    for (int j = 0; !ranked && j < properties.Length; j++)
                    {
                        ranked = properties[j].Name == items[i].Name;
                    }

                    if (!ranked)
                    {
                        return AgeText.Clean(Gui.Localize(items[i].Title));
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("scan: naming the known-systems curve threw: " + e);
            }

            return null;
        }

        /// <summary>The sentence the game writes under the curves, read as it is drawn. Absent where the
        /// game hides it - a system discovered this turn has no curve and no line.</summary>
        private static void RankSentence(
            GraphBuilder builder,
            ScanViewSystemGlobalRankHistogram curves
        )
        {
            // Flow control: whether the sentence is read at all. The game hides the whole curve block
            // for a system whose axes it could not set up - a game on turn zero, a system found this
            // very turn - and the sentence is written inside that block
            // (<c>ScanViewSystemGlobalRankHistogram.Bind</c> :64-70), so an unhidden read would say
            // whatever the last system left in the label.
            AgePrimitiveLabel label =
                curves == null || !AgeWidgets.Visible(curves.AgeTransform)
                    ? null
                    : curves.GlobalRankLabel;
            if (label == null || string.IsNullOrEmpty(AgeText.Label(label)))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            builder.AddItem(
                Nodes.Drawn(
                    ControlId.Structural("scan:system/rank/sentence"),
                    GraphBuilder.Label(() => AgeText.Label(it), it.AgeTransform),
                    it.AgeTransform
                )
            );
        }

        /// <summary>One READOUT ROW per property the game ranks this system by, named with the caption
        /// the bar carries and counted the way the bar graph counts
        /// (<c>ScanViewSystemEmpireRankBarGraph.Bind</c> :55-83): the place is one more than the number
        /// of the player's OTHER systems holding more of it, out of all of theirs. A row of the region
        /// and no part of the table below it - four bars in a picture are not a grid, and reading them
        /// as one said a cell's place in a thirty-row column over every line.</summary>
        private static void RankProperties(
            GraphBuilder builder,
            StarSystemOverviewScanViewWindow window,
            ScanViewSystemEmpireRankBarGraph bars,
            ColonizedStarSystem colony
        )
        {
            ScanViewSystemEmpireRankBar[] drawn =
                bars.AgeTransform.GetComponentsInChildren<ScanViewSystemEmpireRankBar>(true);
            StarSystemOverviewScanViewGuiElement element = window.SystemOverviewGuiElement;
            StarSystemOverviewScanViewGuiElement.EmpireRankingProperty[] properties =
                element == null ? null : element.EmpireRankingProperties;
            for (int i = 0; properties != null && i < drawn.Length; i++)
            {
                ScanViewSystemEmpireRankBar bar = drawn[i];
                ScanViewCaptionItemGuiElement caption = bar == null ? null : bar.GuiElement;
                // Flow control: whether this property contributes a line. The game switches a bar OFF
                // outright where its ranking property has no caption item to be drawn under
                // (<c>ScanViewSystemEmpireRankBarGraph.BindBar</c> :113), and a line for a bar nobody
                // is shown would be a ranking the picture does not make.
                if (bar == null || caption == null || !AgeWidgets.Visible(bar.AgeTransform))
                {
                    continue;
                }

                int property = -1;
                for (int j = 0; property < 0 && j < properties.Length; j++)
                {
                    if (properties[j].Name == caption.Name)
                    {
                        property = j;
                    }
                }

                if (property < 0)
                {
                    continue;
                }

                int others;
                int place = Place(colony, properties[property].PropertyName, out others);
                string name = AgeText.Clean(Gui.Localize(caption.Title));
                string reading = ModStrings.Format(
                    ModStrings.ScanSystemRank,
                    place,
                    others + 1
                );
                builder.AddItem(
                    Nodes.Drawn(
                        ControlId.For(bar, "scan:system/rank/property/" + i),
                        new NodeVtable
                        {
                            Announcements = new List<NodeAnnouncement>
                            {
                                GraphNodes.LabelPart(() => name),
                                GraphNodes.LabelPart(() => reading),
                            },
                        },
                        bar.AgeTransform
                    )
                );
            }
        }

        /// <summary>Where this system comes among the player's own for one property, and how many of
        /// theirs there are besides it - the graph's own count, walked over the same repository it
        /// walks.</summary>
        private static int Place(
            ColonizedStarSystem colony,
            Amplitude.StaticString property,
            out int others
        )
        {
            others = 0;
            int better = 0;
            try
            {
                IColonizedStarSystemRepositoryService repository =
                    Amplitude.Unity.Framework.Services.GetService<IColonizedStarSystemRepositoryService>();
                float mine = colony.GetPropertyValue(property);
                IEnumerator<ColonizedStarSystem> walk =
                    repository == null ? null : repository.GetValues().GetEnumerator();
                while (walk != null && walk.MoveNext())
                {
                    ColonizedStarSystem other = walk.Current;
                    if (
                        other == null
                        || other.Empire != Gui.PlayerEmpire
                        || ReferenceEquals(other, colony)
                    )
                    {
                        continue;
                    }

                    others++;
                    if (other.GetPropertyValue(property) > mine)
                    {
                        better++;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("scan: ranking a system against the empire's threw: " + e);
            }

            return better + 1;
        }

        /// <summary>
        /// THE TWO CURVES AS ONE TABLE - a ROW PER CURVE and a COLUMN PER TURN (owner ruling
        /// 2026-09-01, playtest), read from the very snapshots the histogram draws them from
        /// (<c>ScanViewSystemGlobalRankHistogram</c> :139-165).
        ///
        /// One curve is how many systems the player knew that turn and the other is where this system
        /// stood among them, so they share an axis and belong in one table rather than two. A curve is
        /// a line along time, so time is the axis the walk runs along and walking right walks the
        /// curve. (The first cut had it the other way about - a row per turn - which made twenty-eight
        /// rows of two figures and no way to read either line as a line.)
        ///
        /// TIME RUNS BACKWARDS ALONG IT (owner ruling 2026-09-02): the turn being played is the FIRST
        /// column, the one before it the second, and so on back to turn one. The picture draws the
        /// oldest reading at the left, but a player asking a curve a question is asking where this
        /// system stands NOW, and that answer must not be twenty-eight presses away; walking right is
        /// then walking back through the history, which is the order the reading is wanted in.
        ///
        /// A turn before the system was ever ranked has no rank of its own and its cell is blank rather
        /// than absent, so the two curves stay under the same turn all the way across - the rule the
        /// marketplace's price history keeps for the same reason. A turn neither curve has a reading
        /// for is no column at all.
        /// </summary>
        private static void RankHistory(
            ColonizedStarSystem colony,
            List<string> turns,
            List<Func<string>> ranks,
            List<Func<string>> known
        )
        {
            try
            {
                Game game = Gui.Game as Game;
                IGameStatisticsManagementService stats =
                    Amplitude.Unity.Framework.Services.GetService<IGameStatisticsManagementService>();
                Empire player = Gui.PlayerEmpire;
                if (game == null || stats == null || player == null)
                {
                    return;
                }

                // Newest first: the loop runs back from the turn in progress, so the first column
                // added is the live reading and the last is turn one.
                for (int turn = game.Turn; turn >= 0; turn--)
                {
                    string count;
                    string rank;
                    if (turn == game.Turn)
                    {
                        // The turn in progress has no snapshot yet; the histogram appends the LIVE
                        // readings for it, and so does this.
                        count = Figure(
                            player.GetPropertyValue(SimulationProperties.Empire.KnownSystemCount)
                        );
                        rank = Ranked(colony.GetScoreRank(player.Index) + 1, count);
                    }
                    else
                    {
                        count = KnownAt(stats, player, turn);
                        rank = Ranked(RankAt(stats, player, colony, turn), count);
                    }

                    if (count == null && rank == null)
                    {
                        continue;
                    }

                    // Copied per column: a cell reads its own turn's figure, and a loop variable read
                    // later would hand every one of them the last turn's.
                    string rankHere = rank;
                    string countHere = count;
                    turns.Add(ModStrings.Format(ModStrings.HudTurnLogTurn, turn + 1));
                    ranks.Add(() => rankHere);
                    known.Add(() => countHere);
                }
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading a system's rank history threw: " + e);
            }
        }

        /// <summary>
        /// The two curves as the table's two rows, in the order the block reads: where this system
        /// stands, then how many systems there were to stand among.
        ///
        /// The systems row takes the game's own caption for its curve (the legend's "No. of systems in
        /// my Empire", found the way the bar graph finds its own). The rank curve has no caption
        /// anywhere - the game names it only inside the sentence above, which is a whole sentence and
        /// not a word - so it takes the mod's own name for this reading, the one the region falls back
        /// on when the caption group is re-cut.
        /// </summary>
        private static void RankCurves(
            GraphSheet sheet,
            StarSystemOverviewScanViewWindow window,
            ScanViewSystemEmpireRankBarGraph bars,
            List<string> turns,
            List<Func<string>> ranks,
            List<Func<string>> known
        )
        {
            // Flow control: whether there is a table at all. A game on turn zero has no curve to draw
            // and the game hides the whole block, so the readouts stand alone.
            if (turns.Count == 0)
            {
                return;
            }

            sheet.Row(
                CurveName(ModStrings.Get(ModStrings.ScanSystemRankRegion)),
                null,
                null,
                ranks.ToArray()
            );
            sheet.Row(CurveName(KnownSystemsCaption(window, bars)), null, null, known.ToArray());
        }

        private static NodeVtable CurveName(string name)
        {
            return new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(() => name) },
            };
        }

        private static string Ranked(int place, string known)
        {
            return place <= 0 || known == null
                ? null
                : ModStrings.Format(ModStrings.ScanSystemRank, place, known);
        }

        private static string Figure(float value)
        {
            return UnityEngine.Mathf.RoundToInt(value).ToString();
        }

        private static string KnownAt(
            IGameStatisticsManagementService stats,
            Empire player,
            int turn
        )
        {
            Snapshot empire;
            float value;
            Snapshot snapshot = stats.TakeSnapshot(turn);
            return snapshot != null
                && snapshot.TryGetSnapshot(player.Name, out empire)
                && empire.TryRead(SimulationProperties.Empire.KnownSystemCount, out value)
                ? Figure(value)
                : null;
        }

        /// <summary>Where this system stood on one past turn, or 0 for a turn before the game ever
        /// ranked it - which is what the curve's own start is.</summary>
        private static int RankAt(
            IGameStatisticsManagementService stats,
            Empire player,
            ColonizedStarSystem colony,
            int turn
        )
        {
            Snapshot system;
            Snapshot ranks;
            float value;
            Snapshot snapshot = stats.TakeSnapshot(turn);
            return snapshot != null
                && snapshot.TryGetSnapshot(colony.Name, out system)
                && system.TryGetSnapshot(ColonizedStarSystem.GlobalRankName, out ranks)
                && ranks.TryRead(player.Index.ToString(), out value)
                ? (int)value + 1
                : 0;
        }

        /// <summary>
        /// WHAT IS LEFT ON ONE OF THE SYSTEM'S WORLDS, where the panel draws it.
        ///
        /// The panel is a picture, a title and a description and no caption of its own, so the region
        /// takes the title the game drew - which is the block's caption in the standing rule's sense -
        /// and the description is the line under it. Shown only with the information tick on and only
        /// where something is really there (<c>StarSystemOverviewScanViewWindow.Refresh</c> :349-356).
        /// </summary>
        private static void Remains(
            GraphBuilder builder,
            StarSystemOverviewScanViewWindow window
        )
        {
            ScanViewSystemOverviewRemainsPanel panel = window.RemainsPanel;
            // Flow control: whether the region exists. The panel keeps whatever it was last bound with
            // and the game shows and hides it with the information tick, so its own drawn state is the
            // question - a region declared off the retained content would name remains on a system
            // that has none.
            if (panel == null || !panel.Shown || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            AgePrimitiveLabel title = panel.RemainsTitleLabel;
            AgePrimitiveLabel description = panel.RemainsDescriptionLabel;
            string caption = AgeText.Label(title);
            if (string.IsNullOrEmpty(caption))
            {
                return;
            }

            builder.SetRegion("scan:system/remains");
            builder.PushContext(caption);
            AgePrimitiveLabel it = description;
            if (it != null && !string.IsNullOrEmpty(AgeText.Label(it)))
            {
                builder.AddItem(
                    Nodes.Drawn(
                        ControlId.Structural("scan:system/remains/description"),
                        GraphBuilder.Label(() => AgeText.Label(it), it.AgeTransform),
                        it.AgeTransform
                    )
                );
            }

            builder.PopContext();
            builder.SetRegion(null);
        }

        /// <summary>
        /// THE PANEL THE TICK OPENS SAYS SO (owner ruling 2026-09-01).
        ///
        /// A whole Tab stop's worth of content appears and disappears under the player with nothing on
        /// the screen to hear - the same case the fleet panel and the empire page's slide-out panels
        /// announce themselves for. Baselined when the lens is entered, so arriving on a lens whose
        /// tick the player left on says nothing; asked only while the lens really has a system on the
        /// screen, so a camera drifting off every star does not read as the player closing anything.
        /// </summary>
        public void WatchSystemInfo()
        {
            try
            {
                StarSystemOverviewScanViewWindow window =
                    Window<StarSystemOverviewScanViewWindow>();
                AgeControlToggle toggle = window == null ? null : window.SystemInfoToggle;
                // Availability: whether there is a panel to be opened or closed at all. The tick is
                // drawn only where the lens has a system with something to show, and the registry it
                // reads outlives the lens - so the baseline is dropped whenever it is not on the
                // screen, and a camera drifting off every star never reads as the player closing
                // anything.
                if (
                    window == null
                    || !window.Shown
                    || toggle == null
                    || !AgeWidgets.Visible(AgeWidgets.Transform(toggle))
                )
                {
                    _infoKnown = false;
                    return;
                }

                bool shown = toggle.State;
                if (!_infoKnown)
                {
                    _infoKnown = true;
                    _infoShown = shown;
                    return;
                }

                if (shown == _infoShown)
                {
                    return;
                }

                _infoShown = shown;
                Voice.Say(
                    ModStrings.Get(
                        shown
                            ? ModStrings.ScanSystemInfoShown
                            : ModStrings.ScanSystemInfoHidden
                    ),
                    false
                );
            }
            catch (Exception e)
            {
                Log.Warn("scan: watching the system information panel threw: " + e);
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
        ///
        /// The stop NAMES itself (owner's word, 2026-09-01): the game writes "Caption" on the tick and
        /// nothing anywhere else, so Tabbing in used to announce a checkbox and leave the player to work
        /// out what they had arrived at. The word is the mod's, because the panel has no heading of its
        /// own to take one from.
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

            bool named = false;
            try
            {
                AgeControlToggle toggle = panel.CaptionsToggle;
                AgeTransform widget = AgeWidgets.Transform(toggle);
                if (widget == null)
                {
                    return;
                }

                // Pushed only once there is something to put under it: a level opened over an empty
                // stop is a name for nothing, and the pop below is what every exit path from here goes
                // out through.
                builder.PushContext(ModStrings.Get(ModStrings.ScanLegendStop));
                named = true;
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
            finally
            {
                if (named)
                {
                    builder.PopContext();
                }
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

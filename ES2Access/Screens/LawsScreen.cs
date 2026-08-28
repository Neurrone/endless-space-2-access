using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The laws window: what the senate's "Pass Laws" button opens, and what an empty law slot opens
    /// when it is pressed.
    ///
    /// Four bands, in the order they are drawn: the heading, with how many slots are left and what the
    /// empire has to spend on them; the filter strip; the grid of law cards the filter matches; and the
    /// pane that writes the selected law out in full, with the button that would enact or repeal it.
    ///
    /// The filters switch instantly - a mouse click on one rebuilds the grid there and then - so they
    /// are radios that do their job on Enter, not a selection waiting for a confirmation. The CARDS are
    /// the other way round, and that is the game's model rather than a choice of the mod's: a card's
    /// toggle only makes it the selection (<c>LawsManagementModalWindow.BindLawCard</c> :424-434 and
    /// <c>RefreshSelectedLawDetails</c> :286-338), and it is Pass or Abolish underneath that acts. Both
    /// stay declared while they refuse, carrying the game's own reasons - not enough influence, not
    /// enough political experience, no slot left.
    ///
    /// The detail pane is permanent drawn text, not a hover: the law's long title, the short title the
    /// card carries, the paragraph explaining it, its effects, its upkeep, the political experience it
    /// needs and what it costs. All of it is read as the pane's own lines, and the paragraph is walkable
    /// line by line in the review buffer.
    ///
    /// The screen is named by the heading the window draws, and that heading is ALSO declared where it
    /// is drawn - focus lands on it, so the page says what has just opened without the name having to
    /// carry it alone.
    /// </summary>
    public sealed class LawsScreen : Screen
    {
        private static readonly object HeadingStop = "laws:heading";
        private static readonly object FiltersStop = "laws:filters";
        private static readonly object CardsStop = "laws:cards";
        private static readonly object DetailStop = "laws:detail";
        private static readonly object ActionsStop = "laws:actions";

        /// <summary>The three sections of the detail pane, in the order it draws them: the law itself,
        /// the effects the game captions, and the band that would enact it. Declared whatever the pane
        /// happens to hold, so the region jump means the same thing on every law.</summary>
        private const string LawRegion = "laws:detail/law";
        private const string EffectsRegion = "laws:detail/effects";
        private const string ActionRegion = "laws:detail/action";

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.laws"; }
        }

        /// <summary>The heading the window writes over itself ("Pass Laws"), so the page announces itself
        /// by the game's own word rather than by nothing at all - it answered null before, and a screen
        /// with no name gives the player nothing to hear on arrival.</summary>
        public override string ScreenName
        {
            get
            {
                string drawn = AgeWidgets.TextOf(Title(Window()));
                return string.IsNullOrEmpty(drawn) ? null : drawn;
            }
        }

        /// <summary>
        /// Escape here is the game's own close, and the mod takes the key so its Back does the same.
        ///
        /// The button the window draws in the corner is wired to <c>OnCancelCb</c>, and
        /// <c>GuiModalWindow.OnCancelCb</c> (:102-105) is <c>HandleInput(InputAction.Exit)</c> - so
        /// pressing it IS the game's Escape, and nothing about the window's own exit is re-implemented.
        /// Claimed only while that button is drawn.
        /// </summary>
        public override bool ConsumesBack
        {
            get { return WindowShape.CloseControl(Window()) != null; }
        }

        public override bool Back()
        {
            return WindowShape.PressClose(Window());
        }

        /// <summary>
        /// The exit the window draws, and the heading it writes over itself.
        ///
        /// Both are found by NAME because the window class exposes a field for neither. The depth is
        /// what the earlier reading got wrong: the close button was looked for two levels down and sits
        /// deeper than that, so the search answered null every frame and the button was silently never
        /// declared - a modal with no way out in its own graph (owner-reported 2026-08-27). Measured
        /// 2026-08-28 rather than guessed, with headroom for a prefab that nests one level further.
        /// </summary>
        private static AgeTransform Title(LawsManagementModalWindow window)
        {
            return Named(window, "Title", 3);
        }

        private static AgeTransform Named(LawsManagementModalWindow window, string name, int depth)
        {
            try
            {
                return window == null
                    ? null
                    : AgeWidgets.ChildNamed(window.AgeTransform, name, depth);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Over the senate that opens it, above the government window it is never up with, and
        /// under the message box anything here could raise.</summary>
        public override int Layer
        {
            get { return 34; }
        }

        /// <summary>The heading, because it is drawn first and Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return HeadingStop; }
        }

        public override bool IsActive()
        {
            try
            {
                LawsManagementModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            LawsManagementModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                BuildHeading(builder, window);
                BuildFilters(builder, window);
                BuildCards(builder, window);
                BuildDetail(builder, window);
                BuildActions(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("laws: reading the window threw: " + e);
            }
        }

        /// <summary>The window's own heading, and the two numbers the game draws on the same line as
        /// the filters - how many law slots are still free, and the empire's influence.</summary>
        private void BuildHeading(GraphBuilder builder, LawsManagementModalWindow window)
        {
            builder.BeginStop(HeadingStop);
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                Title(window),
                "laws:title"
            );
            Cells.AddReadout(_cells, Widget(window.VotedLawSlotsLabel), "laws:slots-left");
            AddInfluence(_cells, window);
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>What the empire has to spend on laws, and what the next turn adds. The window draws
        /// the two numbers beside a bare symbol and captions them nowhere ("0 +9" on its own), so the
        /// caption is the game's own title for the property - the same words the banner across the top of
        /// every page names it with.</summary>
        private static void AddInfluence(List<Cell> cells, LawsManagementModalWindow window)
        {
            AgeTransform widget = Widget(window.CurrentPrestigeLabel);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeText.Clean(Gui.GetLocalizedTitle(InfluenceProperty)),
                () => AgeWidgets.TextOf(at),
                null,
                tooltip
            );
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.For(widget, "laws:influence"), vtable);
        }

        private static readonly Amplitude.StaticString InfluenceProperty =
            SimulationProperties.Empire.NetEmpireEmpirePoint;

        /// <summary>Which laws the grid shows: the ones that could be passed now, one filter per party
        /// in the senate, and all of them. Switching rebuilds the grid at once, so Enter does it.
        /// </summary>
        private void BuildFilters(GraphBuilder builder, LawsManagementModalWindow window)
        {
            builder.BeginStop(FiltersStop);
            _cells.Clear();
            AgeTransform table = window.LawFiltersTable;
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AddFilter(_cells, children[i], i);
            }

            Cells.EmitLinear(builder, _cells);
        }

        private static void AddFilter(List<Cell> cells, AgeTransform widget, int index)
        {
            LawFilter filter = widget == null ? null : widget.GetComponent<LawFilter>();
            if (filter == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            LawFilter it = filter;
            NodeVtable vtable = GraphNodes.Tab(
                () => AgeText.Label(it.TitleLabel),
                () => it.Toggle != null && it.Toggle.State,
                () => AgeWidgets.Operable(widget),
                filter.Tooltip
            );
            vtable.OnActivate = () => AgeWidgets.Toggle(it.Toggle);
            AgeWidgets.Point(vtable, filter.Toggle);
            Cells.Add(cells, widget, ControlId.For(widget, "laws:filter/" + index), vtable);
        }

        private void BuildCards(GraphBuilder builder, LawsManagementModalWindow window)
        {
            builder.BeginStop(CardsStop);
            _cells.Clear();
            LawCards.Cards(_cells, window.LawCardsTable, "laws:card/");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// Everything the window writes about the law under the cursor's selection, in the order it is
        /// drawn: the long title, the short one, the paragraph, the effects, the political experience it
        /// asks for and the upkeep it would add, then what it costs and the button that would enact it.
        ///
        /// The pane is not drawn at all until something is selected (<c>LawDetails.Visible</c>), and a
        /// stop with nothing in it does not exist that frame.
        ///
        /// The pane's three sections are regions, declared whether or not this law fills them, so the
        /// region jump lands in the same place on every law rather than moving with the content. Only
        /// the middle one has a drawn caption; the game writes none over the law itself or over the
        /// band that enacts it, so those two are keyed and nothing is said over them that the game does
        /// not say. "Effects" carries nothing on hover (measured), so it names its section instead of
        /// standing in it.
        /// </summary>
        private void BuildDetail(GraphBuilder builder, LawsManagementModalWindow window)
        {
            AgeTransform pane = window.LawDetails;
            if (pane == null || !AgeWidgets.Visible(pane))
            {
                return;
            }

            builder.BeginStop(DetailStop);
            builder.SetRegion(LawRegion);
            _cells.Clear();
            Cells.AddReadout(_cells, Widget(window.LawTitle), "laws:law-title");
            Cells.AddReadout(_cells, Widget(window.LawShortTitle), "laws:law-short-title");
            AddDescription(_cells, window);
            Cells.EmitLinear(builder, _cells);

            builder.SetRegion(EffectsRegion);
            AddEffects(builder, window.PanelFeatureEffects);

            // The upkeep total and the cost are drawn INSIDE the two blocks above them - the total in
            // the upkeep block, the cost in the button it is the price of - so they are read as part of
            // those and never declared a second time.
            builder.SetRegion(ActionRegion);
            _cells.Clear();
            AddExperience(_cells, window.PanelFeatureExperience);
            AddCurrentExperience(_cells, window.PanelFeatureExperience);
            Cells.AddReadout(_cells, Widget(window.PanelFeatureLawUpkeep), "laws:upkeep");
            AddAction(_cells, window.VoteButton, "laws:vote");
            AddAction(_cells, window.AbrogateButton, "laws:abolish");
            Cells.EmitLinear(builder, _cells);
            builder.SetRegion(null);
        }

        /// <summary>
        /// Pass or Abolish. The game draws the price INSIDE the button, above the word on it, so the
        /// button is read as its own word and then its price rather than as both run together
        /// ("Cost: 15 Influence Pass").
        ///
        /// Neither is pressed by anything but the player: each posts an order that changes the empire's
        /// laws. Both stay declared while they refuse, with the game's own reasons - and those reasons
        /// are the whole value of a button that will not go.
        /// </summary>
        private static void AddAction(List<Cell> cells, AgeControlButton button, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform word = AgeWidgets.ChildNamed(widget, "ButtonContainer", 1) ?? widget;
            AgeTransform price = AgeWidgets.ChildNamed(widget, "CostContainer", 1);
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            AgeControlButton it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TextOf(word),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(widget),
                tooltip
            );
            if (price != null)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(() => AgeWidgets.TextOf(price)));
            }

            AgeWidgets.Point(vtable, button);
            Cells.Add(cells, widget, ControlId.For(widget, key), vtable);
        }

        /// <summary>
        /// What the law asks of the party backing it: the requirement the pane writes in words
        /// ("Required Political experience: Potent Scientists"), and the ladder those words name a rung
        /// of.
        ///
        /// The adjective is only half the fact. The bar under the requirement is divided by ticks into
        /// the party's own scale - one rung per distinct standing any of that party's laws asks for -
        /// and a player who cannot see it has no way to know whether "Potent" is the second rung of
        /// three or the last of six. So the scale is the row's review content
        /// (<see cref="PoliticsExperience.Scale"/>): drawn, permanent, and about this requirement
        /// rather than about the empire, which is why it reviews rather than announces.
        /// </summary>
        private static void AddExperience(
            List<Cell> cells,
            PanelFeaturePoliticsExperiencePrerequisite feature
        )
        {
            AgeTransform widget = Widget(feature);
            if (
                widget == null
                || !AgeWidgets.Visible(widget)
                || string.IsNullOrEmpty(AgeWidgets.TextOf(widget))
            )
            {
                return;
            }

            AgeTransform at = widget;
            PanelFeaturePoliticsExperiencePrerequisite it = feature;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeWidgets.TextOf(at),
                () => null,
                () => PoliticsExperience.Scale(it),
                tooltip
            );
            AgeWidgets.PointAt(vtable, widget, tooltip);
            Cells.Add(cells, widget, ControlId.For(widget, "laws:experience"), vtable);
        }

        /// <summary>
        /// Where the party backing the law actually stands, as its own stop under the requirement.
        ///
        /// The pane draws that standing as a notch on the same bar, and only while the law is out of
        /// reach - the whole difference between a law that can be passed and one that cannot. It is a
        /// drawn thing of its own, with a position to read and a tooltip of its own, so it is a stop of
        /// its own: it reads as where it is drawn (<see cref="PoliticsExperience.Current"/>) with the
        /// game's sentence about it to review. On a law the party already qualifies for the notch is
        /// not drawn and there is no second stop at all, exactly as the pane draws it.
        /// </summary>
        private static void AddCurrentExperience(
            List<Cell> cells,
            PanelFeaturePoliticsExperiencePrerequisite feature
        )
        {
            AgeTransform marker = PoliticsExperience.Marker(feature);
            if (marker == null)
            {
                return;
            }

            PanelFeaturePoliticsExperiencePrerequisite it = feature;
            AgeTooltip note = PoliticsExperience.Note(feature);
            NodeVtable vtable = GraphNodes.Readout(
                () => PoliticsExperience.Current(it),
                () => null,
                null,
                note
            );
            AgeWidgets.PointAt(vtable, marker, note);
            Cells.Add(
                cells,
                marker,
                ControlId.For(marker, "laws:experience-current"),
                vtable
            );
        }

        /// <summary>The law's own paragraph. It is permanently drawn, so it is spoken in full, and its
        /// own lines are in the review buffer to walk.</summary>
        private static void AddDescription(List<Cell> cells, LawsManagementModalWindow window)
        {
            AgePrimitiveLabel label = window.LawDescription;
            AgeTransform widget = Widget(label);
            if (widget == null)
            {
                return;
            }

            AgePrimitiveLabel it = label;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.FullLabel(it)),
                },
                Sections = GraphNodes.Sections(
                    NodeSection.Buffer(() => AgeText.Lines(AgeText.FullLabel(it)))
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.For(widget, "laws:description"), vtable);
        }

        /// <summary>The block of effect lines under its caption - one line each, because each is a
        /// separate sentence the game wrote about a separate effect. The caption is the block's name: a
        /// caption the game leaves empty pushes nothing rather than a blank level.
        ///
        /// The table is POOLED: a law with fewer effects than the one selected before it leaves the
        /// surplus lines parked at alpha 0 still holding the previous law's words
        /// (<c>GuiEffectMapper.UnloadEffects</c>), and they are still <c>Visible</c>. Measured on
        /// "Mine's Bigger Decree", whose one effect was read as three. The BAND walk asks the engine's
        /// own drawing test for that (<see cref="AgeWidgets.DrawnChild"/>), which keeps a retired band
        /// from being walked line by line at all; a retired LINE is taken out with the rest of the
        /// cells, before they are banded (<see cref="Cells"/>).</summary>
        private void AddEffects(GraphBuilder builder, PanelFeatureEffects effects)
        {
            AgeTransform group = effects == null ? null : effects.AgeTransform;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTransform caption =
                effects.TitleLabel == null ? null : effects.TitleLabel.AgeTransform;
            string name = caption == null ? null : AgeWidgets.TextOf(caption);
            bool named = !string.IsNullOrEmpty(name);
            if (named)
            {
                builder.PushContext(name);
            }

            _cells.Clear();
            IList<AgeTransform> bands = group.Children;
            for (int i = 0; bands != null && i < bands.Count; i++)
            {
                AgeTransform band = AgeWidgets.DrawnChild(bands, i);
                if (band == null || ReferenceEquals(band, caption))
                {
                    continue;
                }

                IList<AgeTransform> lines = band.Children;
                for (int j = 0; lines != null && j < lines.Count; j++)
                {
                    // The line's own drawn-ness is the cells' question now: each carries its line,
                    // and a retired one is taken out before they are banded (<see cref="Cells"/>).
                    Cells.AddReadout(_cells, lines[j], "laws:effect/" + i + "/" + j);
                }
            }

            Cells.EmitLinear(builder, _cells);
            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>The window's own exit, which the game draws in the corner well away from
        /// everything else.</summary>
        private void BuildActions(GraphBuilder builder, LawsManagementModalWindow window)
        {
            _cells.Clear();
            Cells.AddControl(_cells, WindowShape.CloseControl(window), "laws:close");
            if (_cells.Count > 0)
            {
                builder.BeginStop(ActionsStop);
                Cells.EmitLinear(builder, _cells);
            }
        }

        private static AgeTransform Widget(AgePrimitiveLabel label)
        {
            try
            {
                return label == null ? null : label.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Widget(GuiPanelFeature feature)
        {
            try
            {
                return feature == null ? null : feature.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Widget(AgeControlButton button)
        {
            return AgeWidgets.Transform(button);
        }

        private static LawsManagementModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<LawsManagementModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

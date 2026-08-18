using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The hero page - what F6 opens: the Academy itself down the left edge, every hero the empire is
    /// employing as a strip of cards, and the band of things that can be done to the one picked.
    ///
    /// The page can be LOCKED OUT entirely (<c>AcademyScreen.IsAccessible</c> :106-114: a hero, a
    /// discovered Academy or the recruitment technology). Until then the icon that opens it is drawn
    /// disabled with the game's own sentence for what is missing, so the refusal is already read where the
    /// player presses and this screen simply never becomes active. Nothing here reproduces that test.
    ///
    /// <b>The strip is the page.</b> One card per hero in <c>departmentOfEducation.ActiveHeroes</c>
    /// (<c>Refresh</c> :207-209), and they are RADIOS: the game keeps exactly one picked
    /// (<c>RefreshHeroCard</c> :400 writes <c>Toggle.State = selectedHero == hero</c>) and the whole
    /// action band acts on that one. So Enter on a card is the card's own toggle, which is select and not
    /// yet do - the same select-then-act every list in this mod copies from the game. What a card SAYS is
    /// <see cref="HeroCards"/>', the shared reader for the one prefab family five surfaces draw; the flags
    /// this surface sets are name, definition, experience, health, skills and assignment (measured), so an
    /// Academy card reads its politics, level, masteries and posting and does not draw a description
    /// paragraph or a ship.
    ///
    /// Three things the page draws around the strip are deliberately NOT declared, all for the reason the
    /// quest journal leaves its own strip's furniture out: the left and right nav buttons and the
    /// "Hero 1/1" label beside them are the mouse's way of doing what walking the strip does and say
    /// nothing walking does not, and the <c>HeroPillsTable</c> under it is a second copy of the strip
    /// (<c>OnHeroPillCb</c> :517-523 is <c>OnHeroCardCb</c>). The card's own double click is not wired
    /// either: on this page it opens the inspection window (:510-515) and the Inspect button already
    /// does. Nor is the card's <c>OnShowAssignmentLocationCb</c> button (which this prefab draws as the
    /// whole <c>AssignmentGroup</c>, measured), because the action band draws that button too and one
    /// gesture in two places is one too many.
    ///
    /// Two clicks the game wires and answers with nothing are read as the readouts they are - the Academy
    /// level box and the unlock gauge's own bar, both wired to <c>OnAcademyLevelGaugeCb</c>, whose whole
    /// body is <c>Gui.Log</c> (<c>AcademyInfoSidePanel.cs:191-194</c>) - and so are the class-odds items,
    /// whose click only does anything in the developers' god mode
    /// (<c>HeroUnlockGaugeLineItem.OnClickCb</c> :66-74).
    ///
    /// Escape and F6 stay the game's. This is one of the icon strip's screens, drawn in an exclusive
    /// window stack - opening any other one hides this instantly - which is why they share a layer.
    /// </summary>
    public sealed class AcademyScreen : Screen
    {
        private static readonly object HeroesStop = "academy:heroes";
        private static readonly object ActionsStop = "academy:actions";

        /// <summary>The prefix the shared readers key this page's ids under.</summary>
        private const string Keys = "academy:";

        /// <summary>The two handlers on this page that answer a click with nothing at all: one logs and
        /// returns (<c>AcademyInfoSidePanel.OnAcademyLevelGaugeCb</c>), and the widget in the unlock panel
        /// wired to the same NAME reaches no method there at all.</summary>
        private const string DeadClick = "OnAcademyLevelGaugeCb";

        /// <summary>The label the hero-unlock box draws its heading in. The panel exposes no
        /// <c>PanelTitle</c>, so the heading is taken from where it is drawn - and then left out of the
        /// walk, which would otherwise read the stop's own name back as its first line.</summary>
        private const string UnlockHeading = "HeroUnlockProgressTitle";

        private readonly GlobalHud _hud = new GlobalHud();

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<SidePanel> _panels = new List<SidePanel>();
        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>The hero whose card the strip has been asked to slide into sight, and how many frames
        /// are left to do it in. One slot, overwritten: a player walking the strip at speed is only ever
        /// owed the card they stopped on.</summary>
        private Hero _reveal;

        private int _slidesLeft;

        private const int SlidePatience = 30;

        public override string Key
        {
            get { return "screen.academy"; }
        }

        /// <summary>Above the view levels it is drawn over, beside the empire summary, the senate, the
        /// economy and the military pages: the strip of icons in the corner opens all of them and the
        /// engine's window stack lets only one be up at a time.</summary>
        public override int Layer
        {
            get { return 15; }
        }

        public override string ScreenName
        {
            get
            {
                string title = ScreenTitle();
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenAcademy)
                    : title;
            }
        }

        /// <summary>The heroes, because that is what the page is for; the Academy's own boxes and the
        /// empire's banners are a Shift+Tab away.</summary>
        public override object InitialFocusStop
        {
            get { return HeroesStop; }
        }

        /// <summary>A page the player closes and comes straight back to, with the cursor where they left
        /// it.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>Escape is the game's: it closes the screen, which is what the page's own exit does
        /// too.</summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        /// <summary>
        /// Arrival gates on the page being WORKABLE, not just on no modal being up.
        ///
        /// One of these windows opens four different modals, and the renderer they are drawn by switches
        /// the whole background stack off while one is up and back on a frame or more AFTER the modal
        /// reports itself gone (measured: <c>BackgroundRenderer.Enable</c> false while the hero list is
        /// shown). Coming back on "no modal" alone lands the cursor on a page whose every control is still
        /// switched off, and the card under it says "unavailable" once, in passing.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                global::AcademyScreen window = Window();
                if (window == null || !window.Shown || !window.IsReady)
                {
                    return false;
                }

                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                return gui != null
                    && !gui.IsAnyModalVisible
                    && !gui.IsInLoadingWindow
                    && AgeWidgets.Operable(window.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void OnPush()
        {
            _hud.Baseline();
        }

        public override void OnPop()
        {
            _hud.Forget();
            _reveal = null;
        }

        public override void OnUpdate()
        {
            _hud.Update();
            SlideIntoView();
        }

        public override void Build(GraphBuilder builder)
        {
            global::AcademyScreen window = Window();
            if (window == null)
            {
                return;
            }

            // Down the screen: the empire's banners along the top, the two boxes down the left edge, the
            // strip of heroes, and the band of things that can be done to the one picked.
            _hud.Top(builder);
            BuildSidePanels(builder);
            BuildHeroes(builder, window);
            BuildActions(builder, window);
            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.Turn(builder);
        }

        // ---- the boxes down the left edge ----

        private void BuildSidePanels(GraphBuilder builder)
        {
            try
            {
                SidePanels.Drawn(_panels);
                for (int i = 0; i < _panels.Count; i++)
                {
                    SidePanel panel = _panels[i];
                    builder.BeginStop("academy:side/" + panel.GetType().Name);
                    builder.PushContext(PanelName(panel));
                    _cells.Clear();
                    SidePanels.Readouts(
                        _cells,
                        panel,
                        "academy:side/" + i + "/",
                        DrawnLines,
                        DeadOrGodModeClick
                    );
                    Cells.EmitLinear(builder, _cells);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("academy: reading the side panels threw: " + e);
            }
        }

        /// <summary>What a box down the left edge is called. Both of these write a heading and neither
        /// writes it in the field the shared reader looks in: the Academy's box draws its name over its
        /// banner, and the unlock box writes its heading into the first line of its contents.</summary>
        private static string PanelName(SidePanel panel)
        {
            AcademyInfoSidePanel info = panel as AcademyInfoSidePanel;
            if (info != null)
            {
                string drawn = AgeText.Label(info.AcademyTitle);
                return string.IsNullOrEmpty(drawn) ? SidePanels.Name(panel) : drawn;
            }

            string heading = AgeWidgets.TextOf(UnlockHeadingLabel(panel));
            return string.IsNullOrEmpty(heading) ? SidePanels.Name(panel) : heading;
        }

        private static AgeTransform UnlockHeadingLabel(SidePanel panel)
        {
            return panel is HeroUnlockSidePanel
                ? AgeWidgets.ChildNamed(panel.ContentGroup, UnlockHeading, 4)
                : null;
        }

        /// <summary>
        /// The things in these boxes the shape of the widget tree cannot answer for.
        ///
        /// - Two of the Academy box's lines are a value drawn beside a button or an icon, with the
        ///   sentence explaining the value hung on the GROUP around them (where the Academy is) or on the
        ///   ICON beside them (who owns it) rather than on the label. The shape rule reads the words and
        ///   drops the sentence, so both are read here as what they are: the value, the one sentence
        ///   anywhere in the group, and then the button where there is one.
        /// - The unlock box's HEADING group holds two labels, each with a sentence of the game's own on
        ///   it, and the shape rule would glue them into one line and drop both sentences. One line each,
        ///   and the first of them - the heading the stop is already named after - not at all.
        /// - The gauge itself is one drawn line whose words ("Locked", or a percentage) and whose
        ///   sentence - what the gauge is for, or why recruitment is shut
        ///   (<c>HeroUnlockGaugeItem.Refresh</c> :77-128) - are on different widgets, and there is a bar
        ///   drawn inside it that would otherwise split it.
        /// - A class-odds item draws an ICON and no words at all: what class it stands for, and the odds
        ///   the game formats for it (<c>%AcademySideHeroClassUnlockFormat</c>,
        ///   <c>HeroUnlockGaugeLineItem.Refresh</c> :40-48), exist only inside the dossier the tooltip
        ///   window assembles - so the name comes off the tooltip's own wrapper and the words come from
        ///   pointing at it, exactly as a hero card's mastery lines do.
        /// </summary>
        private static bool DrawnLines(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            HeroUnlockGaugeLineItem line = widget.GetComponent<HeroUnlockGaugeLineItem>();
            if (line != null)
            {
                AgeTooltip dossier = AgeWidgets.Raw(widget);
                AddLine(
                    cells,
                    widget,
                    () => AgeWidgets.TooltipTitle(dossier),
                    dossier,
                    keyPrefix + widget.name
                );
                return true;
            }

            if (InfoLine(cells, widget, keyPrefix, panel as AcademyInfoSidePanel))
            {
                return true;
            }

            HeroUnlockGaugeItem gauge = Gauge(widget);
            if (gauge == null)
            {
                return false;
            }

            if (ReferenceEquals(widget, Bar(gauge)))
            {
                cells.Add(
                    Cells.Readout(widget, AgeWidgets.Raw(widget), keyPrefix + widget.name)
                );
                return true;
            }

            return Headings(cells, widget, keyPrefix, panel);
        }

        /// <summary>The heading group of an unlock gauge: one line per label, minus the one the stop is
        /// named after. Recognised by shape rather than by a field, because the prefab exposes neither the
        /// group nor the labels in it.</summary>
        private static bool Headings(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            AgeTransform heading = UnlockHeadingLabel(panel);
            if (heading == null || !ReferenceEquals(heading.Parent, widget))
            {
                return false;
            }

            IList<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child == null || ReferenceEquals(child, heading))
                {
                    continue;
                }

                Cells.AddReadout(cells, child, keyPrefix + child.name);
            }

            return true;
        }

        /// <summary>Where the Academy is and who owns it: the value the group draws, the sentence the
        /// group or the icon beside it carries, and then the button the group ends in.</summary>
        private static bool InfoLine(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            AcademyInfoSidePanel panel
        )
        {
            if (panel == null)
            {
                return false;
            }

            bool location = ReferenceEquals(widget, panel.AcademyLocationGroup);
            if (!location && !ReferenceEquals(widget, panel.AcademyOwnerGroup))
            {
                return false;
            }

            AgeTransform value = Label(
                location ? panel.AcademyLocationValue : panel.AcademyOwnerValue
            );
            if (value != null)
            {
                AgeTransform words = value;
                AddLine(
                    cells,
                    widget,
                    () => AgeWidgets.TextOf(words),
                    Sentence(widget),
                    keyPrefix + value.name
                );
            }

            if (location)
            {
                Cells.AddControl(cells, panel.LocateSystemButton, keyPrefix + "locate");
            }

            return true;
        }

        /// <summary>The one sentence a group carries: its own, else the first one hung on something drawn
        /// inside it - which is where this panel keeps the owner line's.</summary>
        private static AgeTooltip Sentence(AgeTransform widget)
        {
            AgeTooltip own = AgeWidgets.Readable(AgeWidgets.Raw(widget));
            if (own != null)
            {
                return own;
            }

            IList<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                AgeTooltip found = child == null || !AgeWidgets.Visible(child)
                    ? null
                    : AgeWidgets.Readable(AgeWidgets.Raw(child));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>One line the mod has read itself: the words it says, the sentence explaining them, and
        /// the pointer aimed at whatever the game hangs that sentence on - which for a figure drawn as a
        /// bare icon (a class in the odds strip) is the only way its dossier is ever drawn.</summary>
        private static void AddLine(
            List<Cell> cells,
            AgeTransform host,
            Func<string> label,
            AgeTooltip tooltip,
            string key
        )
        {
            if (string.IsNullOrEmpty(label()))
            {
                return;
            }

            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(label) },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, host);
            Cells.Add(cells, host, ControlId.Referenced(host, key), vtable);
        }

        private static AgeTransform Label(AgePrimitiveLabel label)
        {
            try
            {
                return label == null || !AgeWidgets.Visible(label.AgeTransform)
                    ? null
                    : label.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether a group the game made clickable is really a band of readouts: the two boxes
        /// wired to a handler that only logs, and the class-odds items whose click needs the developers'
        /// god mode.</summary>
        private static bool DeadOrGodModeClick(AgeTransform widget, SidePanel panel)
        {
            AgeControlButton button = AgeWidgets.Button(widget);
            return button != null
                && (
                    button.OnActivateMethod == DeadClick
                    || widget.GetComponent<HeroUnlockGaugeLineItem>() != null
                );
        }

        private static HeroUnlockGaugeItem Gauge(AgeTransform widget)
        {
            try
            {
                return widget.GetComponentInParent<HeroUnlockGaugeItem>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The gauge's own bar - the widget the item hangs its explaining sentence on.</summary>
        private static AgeTransform Bar(HeroUnlockGaugeItem gauge)
        {
            try
            {
                return gauge.GaugeTooltip == null ? null : gauge.GaugeTooltip.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the heroes ----

        /// <summary>
        /// One card per hero, one per row in the order the game laid them out - left to right, then down
        /// - under the heading it wrote over them ("Heroes List"). The cards are peers of one kind, so
        /// where the strip wrapped is a fact about the table and not about the heroes.
        ///
        /// Keyed on the HERO, not the card: the table pools its cards and re-binds them by index on every
        /// refresh (<c>RefreshHeroCard</c> :380-401), so a cursor keyed on the widget would act on a
        /// different hero a frame later. Focus lands on the card the game has picked.
        /// </summary>
        private void BuildHeroes(GraphBuilder builder, global::AcademyScreen window)
        {
            builder.BeginStop(HeroesStop);
            string title = StripTitle(window);
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            _cells.Clear();
            ControlId start = null;
            try
            {
                AgeTransform table = window.HeroCardsTable;
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    ControlId id = AddCard(children[i], i);
                    if (id != null && Picked(window, children[i]))
                    {
                        start = id;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("academy: reading the hero cards threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            if (start != null)
            {
                builder.SetStart(start);
            }

            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>One hero's card. Enter is the card's own toggle, which is what the mouse clicks: the
        /// game records the choice, re-reads the action band and slides the strip so the picked card
        /// centres (<c>OnHeroCardCb</c> :497-508).</summary>
        private ControlId AddCard(AgeTransform widget, int index)
        {
            HeroDetailedCard card = Card(widget);
            Hero hero = HeroCards.Hero(card);
            if (hero == null || !Drawn(widget))
            {
                return null;
            }

            HeroDetailedCard it = card;
            AgeTransform host = widget;
            Hero who = hero;
            NodeVtable vtable = GraphNodes.Radio(
                HeroCards.Name(card),
                () => it.Toggle != null && it.Toggle.State,
                () => AgeWidgets.Toggle(it.Toggle),
                () => AgeWidgets.Operable(host)
            );
            vtable.Sections = HeroCards.Sections(card);
            AgeWidgets.Point(vtable, it.Toggle, Tooltip(card), host);
            // Landing on a card the strip has scrolled out of sight is the one thing that has to move
            // furniture, and this is where a node says how it wants to LOOK when it is focused.
            Action pointer = vtable.OnFocusVisual;
            vtable.OnFocusVisual = () =>
            {
                if (pointer != null)
                {
                    pointer();
                }

                Show(who);
            };
            ControlId id = ControlId.Referenced(hero, Keys + "card/" + index);
            Cells.Add(_cells, widget, id, vtable);
            return id;
        }

        /// <summary>Whether this is the card the game has ticked. Asked of the window rather than of the
        /// card's own toggle only here, where the answer decides where focus STARTS - the tick is
        /// rewritten on every refresh from exactly this field.</summary>
        private static bool Picked(global::AcademyScreen window, AgeTransform widget)
        {
            try
            {
                return window.SelectedHero != null
                    && ReferenceEquals(HeroCards.Hero(Card(widget)), window.SelectedHero);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- sliding the strip ----

        /// <summary>Ask the strip to bring a hero's card inside the viewport.</summary>
        private void Show(Hero hero)
        {
            _reveal = hero;
            _slidesLeft = hero == null ? 0 : SlidePatience;
        }

        /// <summary>
        /// Slide the strip until the card under the cursor is inside the viewport it is clipped by.
        ///
        /// The offset is written the way the game itself writes it when it first lays the strip out
        /// (<c>OnGameCreated</c> :157-161), with the arithmetic it centres a picked card with
        /// (<c>OnHeroCardCb</c> :500-506), and it is the ONLY thing that moves: this page has no scroll
        /// view (measured - the strip is a plain clipped group under an
        /// <c>AgeControlWheelArea</c>), so <see cref="ScrollIntoView"/> has nothing to find, and the
        /// game's own paging buttons cannot be borrowed the way the quest journal's are because these do
        /// not scroll - <c>SelectNextCard</c> (:533-552) picks the next hero, which would make walking
        /// the strip re-target the whole action band. Writing the offset moves no game state and nothing
        /// reads it but the game's own animation, which starts from wherever the strip is standing.
        ///
        /// Only when the card is actually outside the viewport, and never while the game is mid-slide of
        /// its own.
        /// </summary>
        private void SlideIntoView()
        {
            if (_reveal == null)
            {
                return;
            }

            try
            {
                global::AcademyScreen window = Window();
                HeroDetailedCard card = window == null ? null : Card(window, _reveal);
                AgeTransform table = window == null ? null : window.HeroCardsTable;
                AgeTransform viewport = window == null ? null : window.HeroCardsTableContainer;
                if (card == null || table == null || viewport == null || _slidesLeft-- <= 0)
                {
                    _reveal = null;
                    return;
                }

                Rect rect = card.AgeTransform.GetGlobalPosition();
                Rect view = viewport.GetGlobalPosition();
                if (rect.xMin >= view.xMin && rect.xMax <= view.xMax)
                {
                    _reveal = null;
                    return;
                }

                if (table.ModifiersRunning)
                {
                    return;
                }

                float upscale = AgeUtils.CurrentUpscaleFactor();
                table.PixelOffsetLeft =
                    (viewport.Width - card.AgeTransform.Width) / (2f * upscale)
                    - card.AgeTransform.X / upscale;
                _reveal = null;
            }
            catch (Exception e)
            {
                Log.Warn("academy: sliding the hero strip threw: " + e);
                _reveal = null;
            }
        }

        private static HeroDetailedCard Card(global::AcademyScreen window, Hero hero)
        {
            AgeTransform table = window.HeroCardsTable;
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                HeroDetailedCard card = Card(children[i]);
                if (card != null && ReferenceEquals(HeroCards.Hero(card), hero))
                {
                    return card;
                }
            }

            return null;
        }

        // ---- what can be done to the picked hero ----

        /// <summary>
        /// The band along the bottom, in the order the game draws it: Heal, Sell, Inspect, and then
        /// either Unassign and Show-location or the three assignment buttons, switched on whether the
        /// hero has a posting (<c>Refresh</c> :239-275). Read off the band rather than named, so what is
        /// declared is what is DRAWN and the two halves need no test of the mod's.
        ///
        /// Every refusal is the game's own sentence, written onto the button's tooltip as the page
        /// refreshes - the cost Heal cannot afford, the technology Sell is missing, what stops a hero
        /// being assigned - and the whole band is switched off while no hero is picked or the turn is not
        /// the player's (:241). Heal also carries its price IN its caption
        /// (<c>"%AcademyScreenHealButtonTitle (N [dustColored])"</c> :281), which is drawn text and so is
        /// read as the name it is.
        ///
        /// Sell is the one button the game leaves ENABLED while it is refusing, and it is read as refusing
        /// anyway - see <see cref="AgeWidgets.Offered"/>.
        /// </summary>
        private void BuildActions(GraphBuilder builder, global::AcademyScreen window)
        {
            AgeTransform band = window.HeroButtonsGroup;
            if (band == null || !AgeWidgets.Visible(band))
            {
                return;
            }

            builder.BeginStop(ActionsStop);
            builder.PushContext(ModStrings.Get(ModStrings.AcademyHeroActions));
            _cells.Clear();
            try
            {
                IList<AgeTransform> children = band.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    AgeControlButton button = child == null ? null : AgeWidgets.Button(child);
                    if (button == null || !AgeWidgets.Visible(child))
                    {
                        continue;
                    }

                    AddAction(child, button);
                }
            }
            catch (Exception e)
            {
                Log.Warn("academy: reading the hero buttons threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>One button of the band. The caption is asked live, because Heal rewrites its own with
        /// what the healing would cost.</summary>
        private void AddAction(AgeTransform widget, AgeControlButton button)
        {
            AgeTransform at = widget;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TextOf(at),
                () => AgeWidgets.Press(at),
                () => AgeWidgets.Offered(at),
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.Point(vtable, button);
            Cells.Add(
                _cells,
                widget,
                ControlId.Referenced(widget, Keys + "button/" + widget.name),
                vtable
            );
        }

        // ---- reading the window ----

        /// <summary>The heading the game writes over the strip ("Heroes List"). Not exposed as a field,
        /// so it is found where it is drawn - above the container the cards are clipped by.</summary>
        private static string StripTitle(global::AcademyScreen window)
        {
            try
            {
                AgeTransform container = window.HeroCardsTableContainer;
                AgeTransform group = container == null ? null : container.Parent;
                return AgeWidgets.TextOf(AgeWidgets.ChildNamed(group, "Title", 0));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A card the game has stopped using is left in the pool transparent rather than hidden,
        /// so alpha is half the question.</summary>
        private static bool Drawn(AgeTransform widget)
        {
            try
            {
                return AgeWidgets.Visible(widget) && widget.Alpha > 0f;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static HeroDetailedCard Card(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<HeroDetailedCard>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The dossier the game hangs on the card as a whole, and only while the card is keeping
        /// its tooltips bound at all.</summary>
        private static AgeTooltip Tooltip(HeroDetailedCard card)
        {
            try
            {
                return card.HasTooltips ? card.HeroTooltip : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ScreenTitle()
        {
            try
            {
                return AgeText.Clean(Gui.GetLocalizedTitle("AcademyScreen"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static global::AcademyScreen Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<global::AcademyScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

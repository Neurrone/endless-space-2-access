using System;
using System.Collections.Generic;
using System.Text;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The diplomacy page - what F8 opens: the player's own empire as a hologram in the middle of a ring,
    /// and every other major empire in the galaxy around it, each with a card saying where the two of them
    /// stand.
    ///
    /// <b>The ring is the page.</b> One row per other major, and one node per empire rather than per
    /// widget: the game draws each empire twice - a wedge of the ring (<c>EmpireSector</c>) and a leader
    /// card out at its rim - and the two are surfaces of the SAME object, so declaring both would make Tab
    /// pass every empire twice. The card is where all the words are, the wedge is what the mouse hovers,
    /// and the node reads the first while pointing at the second.
    ///
    /// <b>Reading order is the game's own sort, not the drawn layout</b> - a deliberate deviation from the
    /// rule that a strip is walked as it is laid out. The cards are placed by POLAR COORDINATES (an angle
    /// per empire, <c>RefreshEmpireSector</c> :656-665) so there is no left-to-right or top-to-bottom to
    /// follow: the topmost card is in the middle of the reading order and the two ends of the ring are
    /// drawn side by side. What the game does have is an order it sorted the empires INTO
    /// (<c>otherGuiEmpires.Sort(empireByStateComparer)</c> :529 - allies first, then by diplomatic state),
    /// and it is that order the wedges are created in, so <c>EmpireSectorsContainer.Children</c> IS the
    /// ring order. Reading it means allied empires are read together, which is the grouping the arcs
    /// drawn behind the ring are there to show.
    ///
    /// <b>The card's words are hidden by ALPHA, not by visibility</b> (<c>LeaderCard.ShowContextMenu</c>
    /// :224-256: the detail block sits at <c>Alpha 0</c> until the mouse hovers the wedge). Everything in
    /// it is bound and up to date regardless (<c>Bind</c> :176-211 writes it all), so the readout takes
    /// the labels as they are and does not test alpha - and focus points at the wedge's own radial button
    /// so the game fades the block in for anyone watching. With one exception: the screen raises that
    /// block ONLY for an empire the player has met that is still alive (:842-849 hovers a wedge into
    /// <c>ShowContextMenu</c> behind exactly that test, and <c>HideContextMenu</c> otherwise), so on an
    /// unmet or eliminated card those bound-but-never-drawn labels are the fog answering with facts the
    /// picture never shows - a name, a diplomatic status, a pressure figure and its trend. The gate is
    /// the game's own (<see cref="Drawn"/>) and what survives it is what a hovering mouse would see: the
    /// silhouette wedge, the game's "Unknown Empire", and its own sentence about why there is nothing to
    /// negotiate.
    ///
    /// <b>Enter negotiates</b>, which is the click the mouse makes on the card. The card's own
    /// <c>OnClickCardCb</c> (:704-710) is the safe half of that click - it sends the screen
    /// <c>OnClickEmpireSector</c> and nothing else - while the wedge's <c>OnClickSectorCb</c> (:307-332)
    /// opens with a developers' god-mode branch that POSTS PRESSURE AND WAR-EXHAUST ORDERS. The shipped
    /// prefab wires no control on the card at all (measured: zero <c>AgeControl</c> components anywhere
    /// under it), so there is no button to press: the node sends the screen the same message the card's
    /// handler sends, which is the card's click with nothing in front of it.
    ///
    /// Which empires can be negotiated with is the game's own test (<c>CanNegotiateWith</c> :344-347) and
    /// a refused empire stays in the ring, REFUSING - an unmet empire is drawn as "Unknown Empire" and is
    /// exactly the thing a player wants to know is out there. The game keeps no tooltip on the wedge at
    /// all (measured: zero <c>AgeTooltip</c> components), so the reason is its own sentence from its own
    /// localization file (<c>%DiplomacyScreenFactionIconButtonDescriptionUnknown</c> / <c>…Eliminated</c>),
    /// reproduced rather than invented.
    ///
    /// <b>Swap mode</b> re-centres the ring on somebody else's relations (the tick box says so in the
    /// game's words, and every card's footer changes from "Click to negotiate" to "Click to swap"). While
    /// it is on, Enter re-centres instead of negotiating - the game's own branch (:753-761) - and the mod
    /// declares no separate gesture for it. Escape while it is on is the GAME's: <c>HandleInput</c>
    /// :318-327 consumes Exit to leave swap mode rather than closing the page, so <c>ConsumesBack</c>
    /// stays false and the first Escape puts the ring back on the player. Every one of those re-centrings
    /// is ANNOUNCED (<see cref="WatchCenter"/>): it rewrites the whole page under the player, and the one
    /// thing that says whose ring it now is is a label in the middle they would have to go and read.
    ///
    /// Not declared, and why: the arcs behind the ring (<c>RelationStateSector</c>) draw a colour and an
    /// icon for a grouping the reading order already carries; the tribute buttons on a wedge have no click
    /// at all and exist only to make the card show a tribute block on hover, so each tribute a wedge is
    /// showing is a row under the empire - read off the item itself, and raising that block while the
    /// cursor stands on it - rather than a control; and the hologram in the middle is a model of a face.
    ///
    /// Escape and F8 stay the game's. The page can be LOCKED OUT entirely - the icon that opens it is
    /// drawn disabled with the game's own sentence ("You have not met any other empires") until an empire
    /// is met, the Academy is reachable or the pirates hold a system, and F8 is refused at the same gate
    /// (<c>ControlBanner.ToggleScreen</c> :170) - so the refusal is already read where the player presses
    /// and nothing here reproduces it.
    /// </summary>
    public sealed class DiplomacyScreen : Screen
    {
        private static readonly object EmpiresStop = "diplomacy:empires";
        private static readonly object ControlsStop = "diplomacy:controls";
        private static readonly object MetaplotStop = "diplomacy:metaplot";

        private const string Keys = "diplomacy:";

        /// <summary>The game's own sentences for an empire it will not let the player talk to. Both live
        /// in the game's localization file and nowhere in its code: the wedge carries no tooltip, so the
        /// mod says them itself rather than leaving "unavailable" unexplained.</summary>
        private const string UnknownRefusal = "%DiplomacyScreenFactionIconButtonDescriptionUnknown";

        private const string EliminatedRefusal =
            "%DiplomacyScreenFactionIconButtonDescriptionEliminated";

        /// <summary>The impact arrows the card writes INTO a reason's figure to mark its direction.
        /// Named like any other icon everywhere else in the mod; in front of a signed figure they are
        /// not a word at all (see <see cref="Paragraph"/>).</summary>
        private static readonly string[] ImpactMarkers =
        {
            "[negativeImpactWhite]",
            "[positiveImpactWhite]",
        };

        private readonly GlobalHud _hud = new GlobalHud();
        private readonly List<SidePanel> _panels = new List<SidePanel>();
        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>What each line of the pressure paragraph being read IS, figures out
        /// (<see cref="Identity"/>) - the keys one card's reason rows are named by, held for the length
        /// of that card's walk so the same line is not stripped twice per frame.</summary>
        private readonly List<string> _reasons = new List<string>();

        /// <summary>Whose ring the player has been told they are reading, as the game's own index for
        /// that empire - the identity rather than the drawn name, because an unmet empire is drawn
        /// with the same "Unknown Empire" title as every other unmet one and a watch keyed on the
        /// words would sit silent through a swap between two of them. Instance state, so a hot reload
        /// starts it over rather than inheriting a stale answer.</summary>
        private readonly StepWatch _center = new StepWatch();

        public override string Key
        {
            get { return ModStrings.ScreenDiplomacy; }
        }

        /// <summary>The eighth of the icon strip's screens, drawn over whichever view level is
        /// underneath in the same exclusive window stack as the other seven - opening any one of them
        /// hides this instantly, which is why they share a layer.</summary>
        public override int Layer
        {
            get { return 15; }
        }

        public override string ScreenName
        {
            get
            {
                string title = WindowShape.ScreenTitle("DiplomacyScreen");
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenDiplomacy)
                    : title;
            }
        }

        /// <summary>The ring, because that is what the page is for.</summary>
        public override object InitialFocusStop
        {
            get { return EmpiresStop; }
        }

        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>Escape is the game's twice over: in swap mode it puts the ring back on the player
        /// (<c>HandleInput</c> :318-327), and otherwise it closes the page.</summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        /// <summary>
        /// Arrival gates on the page being WORKABLE, the <see cref="AcademyScreen"/> predicate verbatim:
        /// the renderer these icon-strip screens share switches the whole background stack off while one
        /// of the three diplomacy modals is up and back on a frame or more after the modal reports itself
        /// gone. Coming back on "no modal" alone lands the cursor on a page whose every control is still
        /// switched off. The window's own <c>Refresh</c> agrees - it returns early while any of those
        /// modals is shown (:486-489) - so the graph would be reading a page the game has stopped
        /// updating.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                global::DiplomacyScreen window = Window();
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

        /// <summary>Opening the page is not a re-centring: the game puts the ring back on the player
        /// before it is drawn (<c>OnBeginShow</c> :429-433), so the watch starts already knowing whose
        /// ring this is and arriving says nothing. A hot reload pushes the screen afresh over a page
        /// that is already open and baselines the same way, which is what keeps a reload silent.
        /// </summary>
        public override void OnPush()
        {
            _hud.Baseline();
            _center.Baseline(Center(Window()));
        }

        public override void OnPop()
        {
            _hud.Forget();
            _center.Forget();
        }

        public override void OnUpdate()
        {
            _hud.Update();
            WatchCenter();
        }

        /// <summary>
        /// The ring has been re-centred, said passively in the game's own name for whose relations
        /// are drawn now.
        ///
        /// Watched rather than hooked one path at a time, because the game re-centres from three
        /// places and they all end in the same call: the swap click
        /// (<c>OnClickEmpireSector</c> :751-767), Escape or a right click leaving swap mode
        /// (<c>HandleInput</c> :318-327), and the tick box being turned off again
        /// (<c>OnSwitchSwapModeCb</c> :799-806) - the last two both snapping the ring back to the
        /// player, which is as much of a re-centring to a listener as swapping away was.
        ///
        /// Nothing is committed until the words for the new centre exist. <c>ChangeCenterEmpire</c>
        /// :713-734 only sets <c>Dirty</c>, and the title over the hologram is not rewritten until
        /// the refresh the GUI manager runs off that flag (<c>GuiManager.Update</c> :325-338,
        /// <c>Refresh</c> :493) - so a frame in between carries the new empire under the old name,
        /// and announcing there would name the empire the ring has just LEFT. The flag is the game's
        /// own answer to "has the page caught up", and the watermark moves only where it is down and
        /// the label has words.
        /// </summary>
        private void WatchCenter()
        {
            try
            {
                global::DiplomacyScreen window = Window();
                if (window == null)
                {
                    return;
                }

                int center = Center(window);
                if (!_center.IsNew(center) || window.Dirty)
                {
                    return;
                }

                string name = Words(window.CenterEmpireTitle);
                if (string.IsNullOrEmpty(name))
                {
                    return;
                }

                _center.Told(center);
                Voice.Say(ModStrings.Format(ModStrings.DiplomacyViewingFrom, name), false);
            }
            catch (Exception e)
            {
                Log.Warn("diplomacy: watching the ring's centre threw: " + e);
            }
        }

        /// <summary>The game's own index for the empire the ring is centred on, and -1 for a page with
        /// no centre at all - which <see cref="StepWatch"/> never announces.</summary>
        private static int Center(global::DiplomacyScreen window)
        {
            try
            {
                return window == null || window.CenterGuiEmpire == null
                    ? -1
                    : window.CenterGuiEmpire.Empire.EmpireIndex;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            global::DiplomacyScreen window = Window();
            if (window == null)
            {
                return;
            }

            _hud.Top(builder);
            BuildSidePanels(builder);
            BuildEmpires(builder, window);
            BuildControls(builder, window);
            BuildMetaplot(builder, window);
            _hud.Quest(builder);
            _hud.Tutorial(builder);
            _hud.Notifications(builder);
            _hud.TurnLog(builder);
            _hud.Turn(builder);
        }

        // ---- the boxes down the left edge ----

        /// <summary>The alliance box and the box of contextual effects, each drawn only while it has
        /// something to say (<c>Refresh</c> :551-573). Read by shape like every other side panel in this
        /// mod, which is what gets the alliance's rename button and its Leave Alliance button declared as
        /// the buttons they are - both of them the game's own click, so renaming raises the game's rename
        /// box and leaving raises the game's confirmation.</summary>
        private void BuildSidePanels(GraphBuilder builder)
        {
            try
            {
                SidePanels.Drawn(_panels);
                for (int i = 0; i < _panels.Count; i++)
                {
                    SidePanel panel = _panels[i];
                    builder.BeginStop(Keys + "side/" + panel.GetType().Name);
                    builder.PushContext(SidePanels.Name(panel));
                    _cells.Clear();
                    SidePanels.Readouts(
                        _cells,
                        panel,
                        Keys + "side/" + i + "/",
                        EffectLines,
                        null
                    );
                    Cells.EmitLinear(builder, _cells);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("diplomacy: reading the side panels threw: " + e);
            }
        }

        /// <summary>
        /// The table of effect lines a diplomacy side panel draws, which the shape of the tree cannot
        /// name.
        ///
        /// Each line is a bare label with no children of its own, so the walk's own rule - a group whose
        /// children are all primitives is ONE drawn line - glued every contextual effect the empire is
        /// under into a single sentence. The game draws that table through a scroll viewport a third its
        /// height (<c>EffectsScrollView</c>), so the one line also said more than was on the screen and
        /// there was no way to step down to the effects below the fold. One line per effect instead, in
        /// the order the table lays them out, each pointing at its own label so the navigator scrolls it
        /// into view on arrival.
        ///
        /// Matched by REFERENCE against the field the panel itself holds the table in, never by widget
        /// name: the name is the prefab's and the reference is the panel's own answer.
        /// </summary>
        private static bool EffectLines(
            List<Cell> cells,
            AgeTransform widget,
            string keyPrefix,
            SidePanel panel
        )
        {
            AgeTransform table = EffectsTable(panel);
            if (table == null || !ReferenceEquals(widget, table))
            {
                return false;
            }

            IList<AgeTransform> lines = widget.Children;
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                // The table is POOLED - a rebind with fewer effects retires the surplus lines by FADING
                // them, which leaves them Visible and still holding the previous binding's words - so the
                // gate is the engine's own drawing test and not the visibility flag, and the key is the
                // caller's index so a line keeps it whether or not a ghost is parked beside it.
                AgeTransform line = AgeWidgets.DrawnChild(lines, i);
                if (line == null || string.IsNullOrEmpty(AgeWidgets.TextOf(line)))
                {
                    continue;
                }

                cells.Add(Cells.Readout(line, AgeWidgets.Raw(line), keyPrefix + "effect/" + i));
            }

            return true;
        }

        /// <summary>Where each of the two panels keeps its table of effect lines: the contextual-effects
        /// box holds one directly (<c>DiplomacyContextualEffectsSidePanel.EffectsTable</c>), and the
        /// alliance box holds one through the mapper that fills it with the faction pact's effects
        /// (<c>DiplomacyAllianceSidePanel.FactionPactEffectMapper</c>, whose
        /// <c>EffectLinesTable</c> is filled the same pooled way by <c>GuiEffectMapper.LoadEffects</c>).
        /// </summary>
        private static AgeTransform EffectsTable(SidePanel panel)
        {
            DiplomacyContextualEffectsSidePanel contextual =
                panel as DiplomacyContextualEffectsSidePanel;
            if (contextual != null)
            {
                return contextual.EffectsTable;
            }

            DiplomacyAllianceSidePanel alliance = panel as DiplomacyAllianceSidePanel;
            GuiEffectMapper mapper = alliance == null ? null : alliance.FactionPactEffectMapper;
            return mapper == null ? null : mapper.EffectLinesTable;
        }

        // ---- the ring ----

        private void BuildEmpires(GraphBuilder builder, global::DiplomacyScreen window)
        {
            builder.BeginStop(EmpiresStop);
            builder.PushContext(ModStrings.Get(ModStrings.DiplomacyEmpires));
            try
            {
                AddCenter(builder, window);
                AgeTransform container = window.EmpireSectorsContainer;
                IList<AgeTransform> children = container == null ? null : container.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AddEmpire(builder, window, children[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("diplomacy: reading the ring threw: " + e);
            }

            builder.PopContext();
        }

        /// <summary>
        /// Whose relations the ring is showing: the leader name the game writes over the hologram in
        /// the middle of it (<c>Refresh</c> :493 sets it from <c>CenterGuiEmpire</c>).
        ///
        /// It is the only place the page says who it is centred on, and swap mode rewrites it - so
        /// without it a player who has swapped the ring onto somebody else has no way to hear whose
        /// ring they are reading. Declared as the line the game draws it as, at the head of the ring:
        /// the place to go back and re-read it, while a re-centring as it happens is announced by
        /// <see cref="WatchCenter"/>.
        /// </summary>
        private void AddCenter(GraphBuilder builder, global::DiplomacyScreen window)
        {
            AgeTransform widget =
                window.CenterEmpireTitle == null ? null : window.CenterEmpireTitle.AgeTransform;
            if (widget == null || string.IsNullOrEmpty(AgeWidgets.TextOf(widget)))
            {
                return;
            }

            Cell cell = Cells.Readout(widget, AgeWidgets.Raw(widget), Keys + "center");
            builder.AddItem(Nodes.Drawn(cell.Id, cell.Vtable, cell.Widget));
        }

        /// <summary>
        /// One empire of the ring.
        ///
        /// Keyed on the wedge's own component rather than on its index, because the game pools the
        /// wedges and re-binds them by position on every refresh - and a cursor keyed on the position
        /// would act on a different empire a frame after the ring re-sorted itself.
        ///
        /// What it says is the card, in the order the card draws it: the empire's name (which for an
        /// unmet one is the game's "Unknown Empire"), its alliance and metaplot team where it has them,
        /// the diplomatic status with the turns left in it and the pressure multiplier that status
        /// carries, what a computer-run empire thinks of the player, and the pressure or war-exhaust
        /// figure with its per-turn trend - one phrase, which is the glance a hovering mouse gets.
        ///
        /// And it OPENS (<see cref="AddCardPieces"/>): the same card a fact at a time, plus the rest of
        /// it the row has no room for. A card the game does not raise is a leaf instead - there is no
        /// card there to walk.
        /// </summary>
        private void AddEmpire(
            GraphBuilder builder,
            global::DiplomacyScreen window,
            AgeTransform widget,
            int index
        )
        {
            EmpireSector sector = widget == null ? null : widget.GetComponent<EmpireSector>();
            LeaderCard card = sector == null ? null : sector.LeaderCard;
            if (card == null || sector.InspectedGuiEmpire == null)
            {
                return;
            }

            EmpireSector it = sector;
            LeaderCard shown = card;
            global::DiplomacyScreen host = window;
            Func<bool> offered = () => Negotiable(host, it);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => Label(it, shown)),
                    GraphNodes.DisabledPart(offered),
                    GraphNodes.ValuePart(() => Drawn(it) ? Status(shown) : null),
                    GraphNodes.ValuePart(() => Alert(it)),
                    GraphNodes.ValuePart(() => Drawn(it) ? Footer(shown) : null),
                },
                OnActivate = () => Negotiate(host, it),
            };
            GraphNodes.AddRefusal(vtable, () => Refusal(host, it), offered);
            Wedge(vtable, host, it);
            ControlId id = ControlId.For(sector, Keys + "empire/" + index);
            if (!Drawn(it))
            {
                // Nothing to open: the game raises no card for an unmet or eliminated empire, so the
                // row is the silhouette, the game's "Unknown Empire" and its sentence about why there
                // is nothing to negotiate.
                builder.AddItem(Nodes.Drawn(id, vtable, sector));
                return;
            }

            // A button while the game would act on the click - open the negotiation, or re-centre the
            // ring in swap mode - and a bare group where it refuses: Enter does the game's own click
            // either way, and the row still opens on Right Arrow (the expanded/collapsed word comes
            // from being a group header, not from the role; owner ruling 2026-09-12).
            vtable.ControlType = offered() ? ControlTypes.Button : ControlTypes.Group;
            builder.BeginGroup(Nodes.Drawn(id, vtable, sector));
            if (builder.IsExpanded(id))
            {
                AddCardPieces(builder, host, it, shown, Keys + "empire/" + index + "/");
            }

            builder.EndGroup();
        }

        /// <summary>
        /// Keep this empire's wedge hovered while the cursor is anywhere on it.
        ///
        /// Pointed at the wedge, not at the card: the wedge is what the mouse hovers. The highlight
        /// alone is not enough here - the card's detail block is faded in by the SCREEN, off its own
        /// record of which empire is hovered (a coroutine, :806-863), and the engine's SimulateHover
        /// does not dispatch the wedge's mouse-enter callback. So focus sends the screen the same two
        /// messages the wedge's own handlers send (:783-804), which is what makes the words the
        /// readout is reading visible to anyone watching.
        ///
        /// Hung on the card's own pieces as well as on the empire's row, because stepping INTO a card
        /// must not fold the card being read. Moving between them sends the leave and then the hover
        /// inside one frame, and the screen settles its hover once a frame off a dirty flag
        /// (<c>UpdateHoverStatus</c> :808-869), so what it acts on is where the cursor ended up.
        /// </summary>
        private static void Wedge(
            NodeVtable vtable,
            global::DiplomacyScreen window,
            EmpireSector sector
        )
        {
            AgeWidgets.Point(
                vtable,
                sector.ButtonSector,
                null,
                AgeWidgets.Transform(sector.ButtonSector)
            );
            Action point = vtable.OnFocusVisual;
            Action unpoint = vtable.OnBlurVisual;
            vtable.OnFocusVisual = () =>
            {
                if (point != null)
                {
                    point();
                }

                Hover(window, sector, true);
            };
            vtable.OnBlurVisual = () =>
            {
                if (unpoint != null)
                {
                    unpoint();
                }

                Hover(window, sector, false);
            };
        }

        /// <summary>
        /// The card's own pieces, as rows under the empire they belong to.
        ///
        /// The row above says the middle of the card as one phrase, which is the glance a hovering
        /// mouse gets; these are for going back through it a fact at a time - and they carry the rest
        /// of the card with them, which a player could otherwise only hear whole or not at all: the
        /// paragraph of reasons behind the pressure figure, the tributes running between the two
        /// empires, and the treaties strip.
        ///
        /// In the order the card draws them, and named by the card's own words throughout - nothing
        /// here captions a block the game captioned itself. Read-only: the shipped prefab wires no
        /// control anywhere on the card (see the class summary), so the one thing to do here is Enter
        /// on the row above.
        /// </summary>
        private void AddCardPieces(
            GraphBuilder builder,
            global::DiplomacyScreen window,
            EmpireSector sector,
            LeaderCard card,
            string key
        )
        {
            AddBlock(
                builder,
                window,
                sector,
                key + "alliance",
                card.AllianceGroup,
                card.AllianceNameLabel,
                null
            );
            AddBlock(
                builder,
                window,
                sector,
                key + "metaplot",
                card.MetaplotTeamGroup,
                card.MetaplotTeamNameLabel,
                null
            );
            AddBlock(
                builder,
                window,
                sector,
                key + "status",
                card.DiplomaticStatusGroup,
                card.DiplomaticStatusLabel,
                card.DiplomaticStatusPressureLabel
            );
            AddBlock(
                builder,
                window,
                sector,
                key + "attitude",
                card.AttitudeGroup,
                card.AttitudeLabel,
                card.AttitudeTowardsLabel
            );
            AddBlock(
                builder,
                window,
                sector,
                key + "pressure",
                card.PressureGroup,
                card.PressureTitle,
                card.PressureLabel
            );
            AddGauge(builder, window, sector, key + "gauge");
            AddReasons(builder, window, sector, card, key);
            AddTributes(builder, window, sector, key);
            AddTreaties(builder, window, sector, card, key);
        }

        /// <summary>One block of the card as a row of its own - the same pairing the row above joins
        /// into its phrase (<see cref="Block"/>), said alone. A block the game has hidden is not a row;
        /// whether its labels have WORDS is asked when the row is read, like every readout here.
        /// </summary>
        private void AddBlock(
            GraphBuilder builder,
            global::DiplomacyScreen window,
            EmpireSector sector,
            string key,
            AgeTransform group,
            AgePrimitiveLabel first,
            AgePrimitiveLabel second
        )
        {
            // Flow control: a block the card is not drawing at all is not one of this card's facts.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTransform block = group;
            AgePrimitiveLabel head = first;
            AgePrimitiveLabel tail = second;
            AddPiece(
                builder,
                window,
                sector,
                key,
                AgeWidgets.Transform(first) ?? group,
                () => Phrase(block, head, tail)
            );
        }

        /// <summary>
        /// The orange arc drawn around the wedge, as a row - the one thing on this page that is a
        /// picture and nothing else (<see cref="Gauge"/> says what it reads).
        ///
        /// Declared where the game draws the arc, which is its own answer twice over:
        /// <c>BindGauges</c> :233-241 switches both halves off for an unmet or eliminated empire, and
        /// :256-259 hides a half whose two radii have met. Asked per frame like every other block of
        /// the card, so meeting an empire gives it an arc without waiting for a rebuild. Sat where the
        /// game draws it, right after the pressure trend the arc's fill is drawn from.
        /// </summary>
        private void AddGauge(
            GraphBuilder builder,
            global::DiplomacyScreen window,
            EmpireSector sector,
            string key
        )
        {
            if (!Drawn(sector) || !Arc(sector))
            {
                return;
            }

            EmpireSector wedge = sector;
            AddPiece(
                builder,
                window,
                sector,
                key,
                AgeWidgets.Transform(sector),
                () => Gauge(wedge)
            );
        }

        /// <summary>Whether the wedge has an arc drawn around it at all - either half of it. Flow
        /// control: an arc the game is not drawing is not one of this card's facts, and the pair of
        /// halves is the game's own record of whether this wedge has a gauge (<c>BindGauges</c>
        /// :233-241, :256-259).</summary>
        private static bool Arc(EmpireSector sector)
        {
            try
            {
                return AgeWidgets.Visible(AgeWidgets.Transform(sector.PositiveGaugeSector))
                    || AgeWidgets.Visible(AgeWidgets.Transform(sector.NegativeGaugeSector));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// What the arc SHOWS: how the pressure between the two empires is split, a share each, under
        /// the game's own word for whichever gauge is being drawn.
        ///
        /// The arc is the only place the page carries it - the card writes the trend in words and the
        /// standing figure nowhere. Its fill is <c>clamp01((level + 1) / 2 + trend)</c>
        /// (<c>EmpireSector.BindGauges</c> :251-256), and it is the LEVEL alone that is read here,
        /// because the trend is already the row above ("Pressure trend +0.7 / Turn"). The level itself
        /// is a signed balance the game keeps on both sides of a pair
        /// (<c>DepartmentOfForeignAffairs</c> :2384-2387 writes +n on one and -n on the other), which
        /// is what makes it a split rather than two figures - and at war the same gauge holds the war
        /// exhaustion instead (<c>GuiEmpire.GetDiplomaticGaugeLevelWith</c> :481-491), the game's own
        /// switch, and the reason the heading is read off the relation's state. Both words are the
        /// ones the negotiation screen already reads the same gauge under.
        /// </summary>
        private static string Gauge(EmpireSector sector)
        {
            try
            {
                GuiEmpire watching = sector.WatchingGuiEmpire;
                Empire inspected = sector.InspectedGuiEmpire.Empire;
                DiplomaticRelation relation = watching.DepartmentOfForeignAffairs == null
                    ? null
                    : watching.DepartmentOfForeignAffairs.GetDiplomaticRelation(inspected);
                bool war = relation != null && relation.State != null && relation.State.IsWarState;

                // The same 0-to-100 track the arc is drawn on, held inside it the way the drawing is
                // (Mathf.Clamp01): a level past either end is the gauge full or empty, not a share of
                // more than everything.
                int ours = (int)
                    Math.Round((watching.GetDiplomaticGaugeLevelWith(inspected) + 1f) * 0.5f * 100f);
                ours = Math.Max(0, Math.Min(100, ours));

                MessageBuilder message = new MessageBuilder();
                message.ListItem(
                    ModStrings.Get(
                        war ? ModStrings.IconWarMomentum : ModStrings.NegotiationPressure
                    )
                );
                message.ListItem(Share(EmpireNames.Named(watching.Empire), ours));
                message.ListItem(Share(EmpireNames.Named(inspected), 100 - ours));
                return message.Build();
            }
            catch (Exception)
            {
                // A wedge mid-rebind has no relation to ask; the rest of the card still reads.
                return null;
            }
        }

        /// <summary>One empire's share of the arc, named - nothing at all for an empire with no name
        /// to give it, which is a figure nobody could place.</summary>
        private static string Share(string name, int percent)
        {
            return string.IsNullOrEmpty(name)
                ? null
                : ModStrings.Format(ModStrings.NegotiationPressureShare, name, percent);
        }

        /// <summary>
        /// The reasons behind the pressure figure, one row each: the card writes them as a paragraph
        /// into a single label and every line of it is one reason ("+0.5 Pressure trend from diplomatic
        /// choices").
        ///
        /// Each row is named by what its reason IS rather than by where it sits in the paragraph
        /// (<see cref="Identity"/>), because the card rewrites the whole label whenever the figures move
        /// and the set of reasons grows and shrinks with the empire's diplomacy - so a cursor keyed on
        /// "the second line" would be naming a different reason the turn a reason above it appeared or
        /// went away. Where the same reason is written twice the later copies carry their ordinal, which
        /// is nothing but what keeps the keys unique.
        /// </summary>
        private void AddReasons(
            GraphBuilder builder,
            global::DiplomacyScreen window,
            EmpireSector sector,
            LeaderCard card,
            string key
        )
        {
            AgePrimitiveLabel label = card.PressureDescription;
            AgeTransform widget = AgeWidgets.Transform(label);
            if (widget == null)
            {
                return;
            }

            IList<string> lines = AgeText.Lines(Paragraph(label));
            _reasons.Clear();
            for (int i = 0; i < lines.Count; i++)
            {
                _reasons.Add(Identity(lines[i]));
            }

            for (int i = 0; i < lines.Count; i++)
            {
                string reason = _reasons[i];
                if (string.IsNullOrEmpty(reason))
                {
                    // A line with nothing in it but a figure is nothing to come back to by name.
                    continue;
                }

                int nth = 0;
                for (int j = 0; j < i; j++)
                {
                    if (_reasons[j] == reason)
                    {
                        nth++;
                    }
                }

                AgePrimitiveLabel paragraph = label;
                string named = reason;
                int at = nth;
                AddPiece(
                    builder,
                    window,
                    sector,
                    key + "reason/" + reason + (nth == 0 ? string.Empty : "/" + nth),
                    widget,
                    () => Reason(paragraph, named, at)
                );
            }
        }

        /// <summary>
        /// The paragraph of reasons as its rows read it: the card's own lines, with the impact ARROW
        /// the game writes in front of every figure taken out.
        ///
        /// The card marks each reason with the arrow for its direction
        /// (<c>"[positiveImpactWhite]" + …</c>, <c>LeaderCard</c> :493-494 and :550), and an arrow is
        /// named like any other icon everywhere else in the mod - so the line came out as
        /// "positive+0.5 Pressure trend from diplomatic choices": a word glued onto a figure that
        /// already carries its sign, and one that reads as part of the number. Dropped here rather
        /// than from the icon table, because "positive" is worth saying anywhere it is not sitting on
        /// a signed figure - the same drop the battle report makes for its manpower figure.
        ///
        /// Only where an arrow was really there: everywhere else the paragraph comes back through the
        /// shared reader untouched, which is also where the drawn gate is asked.
        /// </summary>
        private static string Paragraph(AgePrimitiveLabel label)
        {
            string drawn = Words(label);
            if (string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            string raw = null;
            try
            {
                // The assigned string, for the one thing the cleaned one cannot show any more: which
                // of these words were an icon tag before the icon table named them.
                raw = label == null ? null : label.Text;
            }
            catch (Exception) { }

            bool marked = false;
            for (int i = 0; raw != null && i < ImpactMarkers.Length; i++)
            {
                if (raw.IndexOf(ImpactMarkers[i], StringComparison.Ordinal) >= 0)
                {
                    raw = raw.Replace(ImpactMarkers[i], string.Empty);
                    marked = true;
                }
            }

            return marked ? AgeText.Clean(raw) : drawn;
        }

        /// <summary>The reason this row is about, found again by what it is when the row is read - the
        /// card rewrites the whole label whenever the figure behind it moves. Nothing at all where the
        /// paragraph no longer carries it: the row is declared afresh from the paragraph the next frame,
        /// and saying the reason that took its place would be naming somebody else's figure.</summary>
        private static string Reason(AgePrimitiveLabel label, string reason, int at)
        {
            IList<string> lines = AgeText.Lines(Paragraph(label));
            int seen = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (Identity(lines[i]) != reason)
                {
                    continue;
                }

                if (seen == at)
                {
                    return lines[i];
                }

                seen++;
            }

            return null;
        }

        /// <summary>What one line of the paragraph IS, with its figure taken out: every digit, sign and
        /// decimal separator dropped and the gaps they leave closed up, so "+0.4 Pressure trend from
        /// Score difference" and the same reason at +1.3 a few turns later answer alike. Nothing is
        /// spoken from this - it is what a row is named and found by.</summary>
        private static string Identity(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            StringBuilder text = new StringBuilder(line.Length);
            bool gap = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (char.IsDigit(c) || c == '+' || c == '-' || c == '.' || c == ',')
                {
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    gap = text.Length > 0;
                    continue;
                }

                if (gap)
                {
                    text.Append(' ');
                    gap = false;
                }

                text.Append(c);
            }

            return text.ToString();
        }

        /// <summary>
        /// What this empire is paying the player and what the player is paying it - one row per tribute
        /// block the wedge is showing, where the card draws that block.
        ///
        /// The wedge carries two tribute buttons, one per direction (<c>BindTributeItem</c> :216-221
        /// binds them as the watching empire's tribute and the inspected empire's), and the game shows
        /// one only while a tribute actually runs that way: <c>TributeItem.SetVisibility</c> counts the
        /// tributes between the two empires and composes the words only then, so the gate is its own.
        /// The buttons have no click at all - all a mouse does on one is make the card draw the tribute
        /// block IN PLACE OF its pressure trend (<c>LeaderCard.ShowTribute</c> :292-300 hides the
        /// pressure group), which is why these rows sit where that block is drawn, and why the pressure
        /// row and its reasons are not declared for the frames one of them is focused - the card is not
        /// drawing them then, and stepping off brings them back.
        /// </summary>
        private void AddTributes(
            GraphBuilder builder,
            global::DiplomacyScreen window,
            EmpireSector sector,
            string key
        )
        {
            AddTribute(builder, window, sector, key + "tribute/give", sector.WatchingTributeItem);
            AddTribute(builder, window, sector, key + "tribute/take", sector.InspectedTributeItem);
        }

        private void AddTribute(
            GraphBuilder builder,
            global::DiplomacyScreen window,
            EmpireSector sector,
            string key,
            TributeItem item
        )
        {
            // Flow control: the game shows a tribute button only while a tribute runs in that
            // direction, and an item it is not showing has composed no words to read.
            if (item == null || !AgeWidgets.Visible(item.AgeTransform))
            {
                return;
            }

            TributeItem tribute = item;
            AddPiece(
                builder,
                window,
                sector,
                key,
                item.AgeTransform,
                () => Tribute(tribute),
                tribute
            );
        }

        /// <summary>What one tribute block says: the game's own heading for the direction it runs in
        /// ("Your tribute to them:"), then the block the item itself composed - where the tribute comes
        /// from, and a line per resource with its amount per turn and the turns it has left to run.
        /// Composed when the row is read, because the item rewrites it whenever a tribute is agreed or
        /// runs out.</summary>
        private static string Tribute(TributeItem item)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                message.ListItem(
                    AgeText.Clean(
                        item.GuiElement == null ? null : Gui.Localize(item.GuiElement.Title)
                    )
                );
                IList<string> lines = AgeText.Lines(AgeText.Clean(item.TributeContentString));
                for (int i = 0; i < lines.Count; i++)
                {
                    message.ListItem(lines[i]);
                }

                return message.Build();
            }
            catch (Exception)
            {
                // An item the game has unbound; the rest of the card still reads.
                return null;
            }
        }

        /// <summary>The treaties strip: the heading the card draws over it, and the abilities it draws
        /// as bare icons underneath.</summary>
        private void AddTreaties(
            GraphBuilder builder,
            global::DiplomacyScreen window,
            EmpireSector sector,
            LeaderCard card,
            string key
        )
        {
            AgeTransform group = card.DiplomaticAbilitiesGroup;
            // Flow control: a strip the card is not drawing has no treaties to name.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            LeaderCard shown = card;
            AddPiece(builder, window, sector, key + "treaties", group, () => Abilities(shown));
        }

        /// <summary>One piece of the card: what it says, and the wedge it belongs to kept hovered while
        /// the cursor stands on it.
        ///
        /// Declared on the WEDGE, which is what the existence gate is asked about, rather than on the
        /// piece's own widget: the card's detail block sits at alpha 0 until the wedge is hovered, so a
        /// piece vouched for by its own label would be dropped for being transparent exactly while
        /// nobody is pointing at it - and landing on it is what raises it.</summary>
        private void AddPiece(
            GraphBuilder builder,
            global::DiplomacyScreen window,
            EmpireSector sector,
            string key,
            AgeTransform widget,
            Func<string> text,
            TributeItem tribute = null
        )
        {
            NodeVtable vtable = new NodeVtable
            {
                // No role word and no state: the card writes these down for the player to read, and
                // wires nothing on them to work.
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(text) },
            };
            Wedge(vtable, window, sector);
            if (tribute != null)
            {
                RaiseBlock(vtable, sector, tribute);
            }

            builder.AddItem(Nodes.Drawn(ControlId.For(widget, key), vtable, sector));
        }

        /// <summary>Make the card draw this tribute's own block while the cursor stands on the row -
        /// after the wedge's own hover, which is what raises the card at all. The pointer stays where
        /// <see cref="Wedge"/> put it: the message is what the card listens to, so aiming at the tribute
        /// button itself would buy the readout nothing and cost the wedge its highlight.</summary>
        private static void RaiseBlock(NodeVtable vtable, EmpireSector sector, TributeItem item)
        {
            EmpireSector wedge = sector;
            TributeItem tribute = item;
            Action point = vtable.OnFocusVisual;
            Action unpoint = vtable.OnBlurVisual;
            vtable.OnFocusVisual = () =>
            {
                if (point != null)
                {
                    point();
                }

                Raise(wedge, tribute, true);
            };
            vtable.OnBlurVisual = () =>
            {
                if (unpoint != null)
                {
                    unpoint();
                }

                Raise(wedge, tribute, false);
            };
        }

        /// <summary>What one block of the card says on its own - the two labels the row above joins
        /// into a longer phrase.</summary>
        private static string Phrase(
            AgeTransform group,
            AgePrimitiveLabel first,
            AgePrimitiveLabel second
        )
        {
            MessageBuilder message = new MessageBuilder();
            Block(message, group, first, second);
            return message.Build();
        }

        /// <summary>Whether the game raises this card's detail block for a mouse on the wedge - its own
        /// test, met and not eliminated (<c>OnHoverEmpireSector</c> :842-849). Asked per frame rather
        /// than at build time, so meeting an empire or watching one die changes what the card says
        /// without waiting for the page to be rebuilt.</summary>
        private static bool Drawn(EmpireSector sector)
        {
            try
            {
                return sector.IsKnown && !sector.InspectedGuiEmpire.Empire.HasBeenEliminated;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether the game would open a negotiation with this empire. In swap mode the same
        /// click re-centres the ring instead, and that is an action rather than a refusal - so the node
        /// is offered there too.</summary>
        private static bool Negotiable(global::DiplomacyScreen window, EmpireSector sector)
        {
            try
            {
                if (window.InSwapMode)
                {
                    return Drawn(sector);
                }

                return window.CanNegotiateWith(sector);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The click the card makes.
        ///
        /// Sent rather than pressed: the shipped leader-card prefab carries no control at all, so
        /// <c>LeaderCard.OnClickCardCb</c> is unreachable from a keypress, and the only widget in the
        /// ring with a click on it is the wedge - whose handler opens with a god-mode branch posting
        /// pressure and war-exhaust orders. This is the card's handler's whole body: the screen's own
        /// message, with the wedge as its argument.
        /// </summary>
        private static void Negotiate(global::DiplomacyScreen window, EmpireSector sector)
        {
            try
            {
                window.SendMessage("OnClickEmpireSector", sector);
            }
            catch (Exception e)
            {
                Log.Warn("diplomacy: opening a negotiation threw: " + e);
            }
        }

        /// <summary>Tell the screen the cursor has arrived at, or left, one wedge - the message its own
        /// mouse handlers send it (:783-804). The leave goes out from under a lowered highlight
        /// (<see cref="Unhovered"/>), which is what makes the screen listen to it.</summary>
        private static void Hover(
            global::DiplomacyScreen window,
            EmpireSector sector,
            bool arriving
        )
        {
            try
            {
                if (arriving)
                {
                    window.SendMessage("OnHoverEmpireSector", sector);
                    return;
                }

                global::DiplomacyScreen host = window;
                EmpireSector wedge = sector;
                Unhovered(sector, () => host.SendMessage("OnLeaveEmpireSector", wedge));
            }
            catch (Exception)
            {
                // A wedge the game has already unbound; the readout is unaffected.
            }
        }

        /// <summary>Show, or put away, the card's block for one tribute - the message the tribute
        /// button's own mouse handlers send the wedge (<c>TributeItem.OnMouseEnter</c> /
        /// <c>OnMouseLeave</c>). The wedge is what listens: <c>OnTributeItemEnter</c> :355-362 sends the
        /// screen its own hover and then tells the card to draw the block, and
        /// <c>OnTributeItemLeave</c> :364-371 ends in the screen's <c>OnLeaveEmpireSector</c> as well -
        /// so that leave needs the same lowered highlight every leave here does.</summary>
        private static void Raise(EmpireSector sector, TributeItem item, bool arriving)
        {
            try
            {
                if (arriving)
                {
                    sector.SendMessage("OnTributeItemEnter", item);
                    return;
                }

                EmpireSector wedge = sector;
                Unhovered(sector, () => wedge.SendMessage("OnTributeItemLeave"));
            }
            catch (Exception)
            {
                // A wedge the game has already unbound; the readout is unaffected.
            }
        }

        /// <summary>
        /// Send a leave with the wedge's own highlight put down for the length of it.
        ///
        /// Every leave here needs it. The screen
        /// ignores a leave while the wedge is still hovered (<c>OnLeaveEmpireSector</c> :790-796 tests
        /// <c>EmpireSector.Hovered</c>), which is right for a hand still on the mouse - but the mod is
        /// what this wedge looks hovered BY, and it lets go a frame late: <c>EmpireSector.Hovered</c>
        /// reads <c>ButtonRadial</c>, the prefab wires that same control into <c>ButtonSector</c> as
        /// well (measured, every wedge), and the pointer a node aims there is released at the bottom of
        /// the NEXT frame (<see cref="PointerFocus"/>, whose release is deferred so that a blur
        /// followed by a focus never flickers). So a leave sent from a blur found the mod's own
        /// highlight standing in for a hand on the mouse, the screen kept its record of which empire is
        /// hovered, and nothing ever sent a second leave - the card stayed raised over whatever the
        /// player went to next, a popup included (measured 2026-09-11: stepping off the ring left
        /// <c>HoveredGuiEmpire</c> naming the empire and its card at full alpha).
        ///
        /// Down and straight back up, rather than released: the highlight belongs to
        /// <see cref="PointerFocus"/>, which is still showing it and takes it down itself once focus is
        /// somewhere else - and taking it down here for good would darken the wedge while the player
        /// walks the very card it raises.
        /// </summary>
        private static void Unhovered(EmpireSector sector, Action leave)
        {
            AgeControlButtonRadial button = sector.ButtonSector;
            bool lit = button != null && button.Hovered;
            if (lit)
            {
                button.SimulateHover(false);
            }

            try
            {
                leave();
            }
            finally
            {
                if (lit)
                {
                    button.SimulateHover(true);
                }
            }
        }

        /// <summary>Why the game will not talk to this empire, in its own words - unmet, eliminated, or
        /// (while the ring is centred on somebody else) not the player's business to negotiate.</summary>
        private static string Refusal(global::DiplomacyScreen window, EmpireSector sector)
        {
            try
            {
                if (!sector.IsKnown)
                {
                    return AgeText.Clean(Gui.Localize(UnknownRefusal));
                }

                if (sector.InspectedGuiEmpire.Empire.HasBeenEliminated)
                {
                    return AgeText.Clean(Gui.Localize(EliminatedRefusal));
                }

                return window.CenterGuiEmpire != null
                    && window.CenterGuiEmpire.Empire != Gui.PlayerEmpire
                    ? ModStrings.Get(ModStrings.DiplomacyNotYourRing)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The empire's name, said the way the game titles its own dossier for it: the leader and the
        /// faction it leads ("Kappa (AI) (Riftborn)" - <c>GuiEmpire.GetLeaderAndFaction</c> :361-397,
        /// which is what <c>NegotiationEmpireInfoPanel</c> :75 writes over the dossier, asked from the
        /// watching empire exactly as that panel asks it).
        ///
        /// The faction rides along because the WEDGE draws it and the name label does not: the faction
        /// icon on the ring and the leader's hologram behind it are the glance a sighted player gets,
        /// and the card writes only the leader's name (owner ruling 2026-09-11). The fog gate is the
        /// same one the row already honours - <c>IsKnownByLookingPlayer</c>, inside the game's own
        /// composition - so an unmet empire still answers the game's "Unknown Empire" and nothing here
        /// leaks a name the fog is keeping. The card's own label is the fallback, for a wrapper mid-
        /// rebind that answers nothing at all.
        /// </summary>
        private static string Label(EmpireSector sector, LeaderCard card)
        {
            try
            {
                string named = AgeText.Clean(
                    sector.InspectedGuiEmpire.GetLeaderAndFaction(
                        sector.WatchingGuiEmpire.Empire,
                        false,
                        false
                    )
                );
                if (!string.IsNullOrEmpty(named))
                {
                    return named;
                }
            }
            catch (Exception)
            {
                // A wrapper the game has unbound; the card's own label still names the empire.
            }

            return Words(card.EmpireNameLabel);
        }

        /// <summary>The middle of the card, one phrase: alliance, metaplot team, diplomatic status with
        /// its turn count and pressure multiplier, attitude towards the player, and the pressure or
        /// war-exhaust figure with its trend.</summary>
        private static string Status(LeaderCard card)
        {
            // A block of the card is one fact and reads as one item; the two labels inside a block are
            // pieces of that fact ("COLD WAR (5 turns)" and the multiplier it carries) and read joined.
            MessageBuilder message = new MessageBuilder();
            Block(message, card.AllianceGroup, card.AllianceNameLabel, null);
            Block(message, card.MetaplotTeamGroup, card.MetaplotTeamNameLabel, null);
            Block(
                message,
                card.DiplomaticStatusGroup,
                card.DiplomaticStatusLabel,
                card.DiplomaticStatusPressureLabel
            );
            Block(message, card.AttitudeGroup, card.AttitudeLabel, card.AttitudeTowardsLabel);
            Block(message, card.PressureGroup, card.PressureTitle, card.PressureLabel);
            return message.Build();
        }

        /// <summary>The badge the game paints on a wedge with nothing written on it at all
        /// (<c>RefreshContextualAlertMarker</c> :224-231): either a truce can be forced now or terms the
        /// player has not seen have become available with this empire. A wordless badge needs words.
        /// </summary>
        private static string Alert(EmpireSector sector)
        {
            try
            {
                return sector.ContextualAlertMarker != null
                    // Content: whether the badge's words join the empire's reading.
                    && AgeWidgets.Visible(sector.ContextualAlertMarker)
                    ? ModStrings.Get(ModStrings.DiplomacyNewOptions)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's own footer on the card - "Click to negotiate", or "Click to swap" while
        /// swap mode is on, which is how the player is told what Enter does here now.</summary>
        private static string Footer(LeaderCard card)
        {
            return Words(card.ClickLabel);
        }

        /// <summary>What this empire's abilities are, off the MODEL the card was bound from: the strip
        /// draws them as icons and the card asks for no tooltips on them
        /// (<c>BindDiplomaticAbility</c> :670-678 passes <c>hasTooltip: false</c>), so the widgets say
        /// nothing at all. Named by the game's own title for a diplomatic ability, under the heading
        /// the card draws over the strip.</summary>
        private static string Abilities(LeaderCard card)
        {
            try
            {
                if (
                    card.WatchingGuiEmpire == null
                    || card.InspectedGuiEmpire == null
                    // Content: whether the treaty lines are this card's to say.
                    || !AgeWidgets.Visible(card.DiplomaticAbilitiesGroup)
                )
                {
                    return null;
                }

                MessageBuilder message = new MessageBuilder();
                // The heading the strip is drawn under ("Treaties"): a label inside the group rather
                // than a field on the card.
                message.ListItem(AgeWidgets.TextOf(card.DiplomaticAbilitiesGroup));
                System.Collections.IList abilities = card.WatchingGuiEmpire.GetDiplomaticAbilities(
                    card.InspectedGuiEmpire.Empire,
                    true
                );
                for (int i = 0; abilities != null && i < abilities.Count; i++)
                {
                    DiplomaticAbility ability = abilities[i] as DiplomaticAbility;
                    if (ability == null)
                    {
                        continue;
                    }

                    message.ListItem();
                    message.Fragment(
                        AgeText.Clean(Gui.GetLocalizedTitle("DiplomaticAbility" + ability.Name))
                    );
                }

                return message.Build();
            }
            catch (Exception)
            {
                // A card mid-rebind has no model to ask; the rest of the card still reads.
                return null;
            }
        }

        // ---- the controls the page draws for itself ----

        /// <summary>The tick box that turns swap mode on, and the two buttons that leave for another
        /// window: the Academy and the pirates. Both are drawn only where the content they lead to
        /// exists at all (<c>Refresh</c> :605-610 hides them without the expansion that adds them), and
        /// the Academy's is left DRAWN while refusing with the game's own sentence for what is
        /// missing.</summary>
        private void BuildControls(GraphBuilder builder, global::DiplomacyScreen window)
        {
            builder.BeginStop(ControlsStop);
            builder.PushContext(ModStrings.Get(ModStrings.DiplomacyControls));
            _cells.Clear();
            try
            {
                AddSwapMode(window);
                AddScreenButton(window.AcademyScreenButton, "academy");
                AddScreenButton(window.PiratesScreenButton, "pirates");
            }
            catch (Exception e)
            {
                Log.Warn("diplomacy: reading the page's controls threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        private void AddSwapMode(global::DiplomacyScreen window)
        {
            AgeControlToggle toggle = window.SwapModeToggle;
            AgeTransform at = AgeWidgets.Transform(toggle);
            if (at == null)
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTransform host = at;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeWidgets.TextOf(host),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Offered(host),
                AgeWidgets.Raw(host)
            );
            AgeWidgets.Point(vtable, it, AgeWidgets.Raw(host), host);
            Cells.Add(_cells, at, ControlId.For(at, Keys + "swap"), vtable);
        }

        /// <summary>One of the two "go to that window instead" buttons. Named by the sentence the game
        /// writes on it, because the button itself is a bare icon.</summary>
        private void AddScreenButton(AgeTransform widget, string key)
        {
            if (widget == null)
            {
                return;
            }

            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            NodeVtable vtable = GraphNodes.Button(
                CardActions.NameFromTooltip(tooltip),
                () => AgeWidgets.Press(at),
                () => AgeWidgets.Offered(at),
                tooltip
            );
            AgeWidgets.PointAt(vtable, at);
            Cells.Add(_cells, at, ControlId.For(at, Keys + key), vtable);
        }

        /// <summary>The board of teams a metaplot minigame draws over the ring, while one is running.
        /// Read one team per row: they are peers of one kind and the wrap points are the board's
        /// layout, not columns of anything.</summary>
        private void BuildMetaplot(GraphBuilder builder, global::DiplomacyScreen window)
        {
            AgeTransform panel = window.MetaplotTeamsPanel;
            // Flow control: a stop would be opened around nothing, and the side-panel reading descends
            // the whole board.
            if (panel == null || !AgeWidgets.Visible(panel))
            {
                return;
            }

            builder.BeginStop(MetaplotStop);
            _cells.Clear();
            try
            {
                SidePanels.Content(_cells, panel, Keys + "metaplot/", null, null);
            }
            catch (Exception e)
            {
                Log.Warn("diplomacy: reading the metaplot panel threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
        }

        // ---- reading widgets ----

        /// <summary>One block of the card as one item of the readout: nothing at all where the game has
        /// hidden the block, and the block's two labels joined where it draws both.</summary>
        private static void Block(
            MessageBuilder message,
            AgeTransform group,
            AgePrimitiveLabel first,
            AgePrimitiveLabel second
        )
        {
            // Content: whether this pair of words joins the sentence being built.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            string head = Words(first);
            string tail = Words(second);
            if (string.IsNullOrEmpty(head) && string.IsNullOrEmpty(tail))
            {
                return;
            }

            message.ListItem();
            message.Fragment(head);
            message.Fragment(tail);
        }

        /// <summary>What a label says, or nothing at all for one the game has hidden.</summary>
        private static string Words(AgePrimitiveLabel label)
        {
            try
            {
                return AgeWidgets.DrawnLabel(label);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static global::DiplomacyScreen Window()
        {
            try
            {
                return GameWindows.Of<global::DiplomacyScreen>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

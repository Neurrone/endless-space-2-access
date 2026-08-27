using System;
using System.Collections.Generic;
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
    /// at all and exist only to make the card show a tribute block on hover, so their words go in the
    /// card's review buffer; and the hologram in the middle is a model of a face.
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

        private readonly GlobalHud _hud = new GlobalHud();
        private readonly List<SidePanel> _panels = new List<SidePanel>();
        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>Whose ring the player has been told they are reading, as the game's own index for
        /// that empire - the identity rather than the drawn name, because an unmet empire is drawn
        /// with the same "Unknown Empire" title as every other unmet one and a watch keyed on the
        /// words would sit silent through a swap between two of them. Instance state, so a hot reload
        /// starts it over rather than inheriting a stale answer.</summary>
        private readonly StepWatch _center = new StepWatch();

        public override string Key
        {
            get { return "screen.diplomacy"; }
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
                string title = ScreenTitle();
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
                    SidePanels.Readouts(_cells, panel, Keys + "side/" + i + "/", null, null);
                    Cells.EmitLinear(builder, _cells);
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("diplomacy: reading the side panels threw: " + e);
            }
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
            if (
                widget == null
                || !AgeWidgets.Visible(widget)
                || string.IsNullOrEmpty(AgeWidgets.TextOf(widget))
            )
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
        /// figure with its per-turn trend. The reasons behind that figure, the abilities strip and the
        /// two tribute blocks are review-buffer content: the reasons are a paragraph, and the abilities
        /// are drawn as bare icons the card deliberately gives no tooltips
        /// (<c>BindDiplomaticAbility</c> :670-678 passes <c>hasTooltip: false</c>), so they are read from
        /// the model the card was built from instead.
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
            if (card == null || sector.InspectedGuiEmpire == null || !AgeWidgets.Visible(widget))
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
                    GraphNodes.LabelPart(() => Label(shown)),
                    GraphNodes.DisabledPart(offered),
                    GraphNodes.ValuePart(() => Drawn(it) ? Status(shown) : null),
                    GraphNodes.ValuePart(() => Alert(it)),
                    GraphNodes.ValuePart(() => Drawn(it) ? Footer(shown) : null),
                },
                Sections = GraphNodes.Sections(() => Drawn(it) ? Detail(shown) : null, null),
                OnActivate = () => Negotiate(host, it),
            };
            GraphNodes.AddRefusal(vtable, () => Refusal(host, it), offered);
            // Pointed at the wedge, not at the card: the wedge is what the mouse hovers. The highlight
            // alone is not enough here - the card's detail block is faded in by the SCREEN, off its own
            // record of which empire is hovered (a coroutine, :806-863), and the engine's SimulateHover
            // does not dispatch the wedge's mouse-enter callback. So focus sends the screen the same two
            // messages the wedge's own handlers send (:783-804), which is what makes the words the
            // readout is reading visible to anyone watching.
            AgeWidgets.Point(vtable, it.ButtonSector, null, AgeWidgets.Transform(it.ButtonSector));
            Action point = vtable.OnFocusVisual;
            Action unpoint = vtable.OnBlurVisual;
            vtable.OnFocusVisual = () =>
            {
                if (point != null)
                {
                    point();
                }

                Hover(host, it, true);
            };
            vtable.OnBlurVisual = () =>
            {
                if (unpoint != null)
                {
                    unpoint();
                }

                Hover(host, it, false);
            };
            builder.AddItem(Nodes.Drawn(ControlId.For(sector, Keys + "empire/" + index), vtable, sector));
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
        /// mouse handlers send it. Leaving is the screen's business to ignore while the real mouse is
        /// still on the wedge, and it does (<c>OnLeaveEmpireSector</c> :801-804 tests
        /// <c>EmpireSector.Hovered</c>), so nothing here fights the player's hand.</summary>
        private static void Hover(
            global::DiplomacyScreen window,
            EmpireSector sector,
            bool arriving
        )
        {
            try
            {
                window.SendMessage(
                    arriving ? "OnHoverEmpireSector" : "OnLeaveEmpireSector",
                    sector
                );
            }
            catch (Exception)
            {
                // A wedge the game has already unbound; the readout is unaffected.
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

        /// <summary>The empire's name as the card draws it - which is the game's "Unknown Empire" for one
        /// the player has not met, so nothing here leaks a name the fog is keeping.</summary>
        private static string Label(LeaderCard card)
        {
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

        /// <summary>The rest of the card: the paragraph of reasons behind the pressure figure, the
        /// abilities the strip draws as bare icons, and the tribute blocks the game only fills in while
        /// the mouse is on one of the two tribute buttons.</summary>
        private static IList<string> Detail(LeaderCard card)
        {
            List<string> said = new List<string>(8);
            Add(said, Words(card.PressureDescription));
            Abilities(said, card);
            Add(said, Words(card.TributeLabel));
            Add(said, Words(card.TributeDescription));
            return said;
        }

        /// <summary>What this empire's abilities are, off the MODEL the card was bound from: the strip
        /// draws them as icons and the card asks for no tooltips on them, so the widgets say nothing at
        /// all. Named by the game's own title for a diplomatic ability.</summary>
        private static void Abilities(List<string> said, LeaderCard card)
        {
            try
            {
                if (
                    card.WatchingGuiEmpire == null
                    || card.InspectedGuiEmpire == null
                    || !AgeWidgets.Visible(card.DiplomaticAbilitiesGroup)
                )
                {
                    return;
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

                Add(said, message.Build());
            }
            catch (Exception)
            {
                // A card mid-rebind has no model to ask; the rest of the buffer still stands.
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
            if (at == null || !AgeWidgets.Visible(at))
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
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            NodeVtable vtable = GraphNodes.Button(
                CardActions.NameFromTooltip(tooltip),
                () => AgeWidgets.Press(at),
                () => AgeWidgets.Offered(at),
                tooltip,
                TooltipMode.None
            );
            GraphNodes.AddRefusal(vtable, tooltip, () => AgeWidgets.Offered(at));
            AgeWidgets.PointAt(vtable, at);
            Cells.Add(_cells, at, ControlId.For(at, Keys + key), vtable);
        }

        /// <summary>The board of teams a metaplot minigame draws over the ring, while one is running.
        /// Read one team per row: they are peers of one kind and the wrap points are the board's
        /// layout, not columns of anything.</summary>
        private void BuildMetaplot(GraphBuilder builder, global::DiplomacyScreen window)
        {
            AgeTransform panel = window.MetaplotTeamsPanel;
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

        private static void Add(List<string> said, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (string line in AgeText.Lines(text))
            {
                said.Add(line);
            }
        }

        /// <summary>What a label says, or nothing at all for one the game has hidden.</summary>
        private static string Words(AgePrimitiveLabel label)
        {
            try
            {
                return label == null || !AgeWidgets.Visible(label.AgeTransform)
                    ? null
                    : AgeText.Label(label);
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
                return AgeText.Clean(Gui.GetLocalizedTitle("DiplomacyScreen"));
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
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<global::DiplomacyScreen>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

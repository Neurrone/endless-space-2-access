using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

namespace ES2Access.Screens
{
    /// <summary>The turn corner: what the turn itself offers, and the multiplayer half of the
    /// cluster.</summary>
    public sealed partial class GlobalHud
    {
        // ---- the turn ----

        /// <summary>What the turn itself offers: end it, move everything that was told to move, walk
        /// to the next fleet with nothing to do, and open the game menu.</summary>
        public void Turn(GraphBuilder builder)
        {
            EndTurnWindow window = TurnWindow();
            // Flow control: everything the turn offers is read under this, and a stop with nothing in
            // it is a Tab press that lands nowhere.
            if (window == null || !AgeWidgets.Visible(window.AgeTransform))
            {
                return;
            }

            List<Cell> found = new List<Cell>();
            EndTurnWindow it = window;
            AgeControlButton endTurn = window.EndTurnButton;
            // Banding input: AddCell appends without the gate's question, and the corner's controls are
            // worked into rows by where they are drawn.
            if (AgeWidgets.Visible(AgeWidgets.Transform(endTurn)))
            {
                NodeVtable vtable = GraphNodes.Button(
                    () => EndTurnLabel(it),
                    () => AgeWidgets.Press(endTurn),
                    () => CanEndTurn(it)
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(() => TurnText(it)));
                vtable.Sections = GraphNodes.Sections(() => EndTurnReason(it), null);
                AgeWidgets.Point(vtable, endTurn);
                found.Add(
                    new Cell
                    {
                        Widget = AgeWidgets.Transform(endTurn),
                        Id = ControlId.For(endTurn, "hud:end-turn"),
                        Vtable = vtable,
                    }
                );
            }

            AddTurnButton(
                found,
                window.ApplyMovementsButton,
                "apply-movements",
                ModStrings.GalaxyApplyMovements,
                null,
                chordAction: UiActions.ApplyMovements
            );
            AddTurnButton(
                found,
                window.NextIdleFleetButton,
                "next-idle-fleet",
                ModStrings.GalaxyNextIdleFleet,
                IdleFleetsText,
                NextIdleFleet,
                UiActions.NextIdleFleet
            );
            AddTurnButton(found, window.GameMenuButton, "game-menu", ModStrings.GalaxyGameMenu, null);
            AddPendingNotifications(found, window.PendingNotificationButton);
            AddRequestToggle(found, window.RequestToggle);
            AddSync(found, window);
            AddPlayers(found, window);
            AddTimers(found, window);
            AddRealTimeClock(found, window);

            // One control per row: the cluster's members are peers of one kind - things to do with the
            // turn - and which of them the game drew beside which is a fact about the corner they are
            // packed into, not about what they are. Up and down walk the lot.
            builder.BeginStop(TurnStop);
            for (int i = 0; i < found.Count; i++)
            {
                builder.AddItem(Nodes.Drawn(found[i].Id, found[i].Vtable, found[i].Widget));
            }
        }

        /// <summary><paramref name="activate"/> is for the one button whose click the mod does better
        /// than a press of it would (<see cref="NextIdleFleet"/>); everything else on the turn cluster
        /// presses the game's own control, which is what keeps a button the mod knows nothing about
        /// working. <paramref name="chordAction"/> ends the name with the chord that presses the
        /// button from anywhere, the way <see cref="EndTurnLabel"/> does for end turn - a key nothing
        /// names is a key nobody finds - and follows a rebind because it is read from the live
        /// binding on every render.</summary>
        private void AddTurnButton(
            List<Cell> found,
            AgeControlButton button,
            string key,
            string nameKey,
            Func<string> value,
            Action<AgeControlButton> activate = null,
            string chordAction = null
        )
        {
            // Banding input: same corner, same door - AddCell takes the button without asking the gate.
            if (!AgeWidgets.Visible(AgeWidgets.Transform(button)))
            {
                return;
            }

            AgeControlButton it = button;
            Action<AgeControlButton> act = activate;
            string chord = chordAction;
            NodeVtable vtable = GraphNodes.Button(
                () =>
                    chord == null
                        ? ModStrings.Get(nameKey)
                        : ChordNames.Label(ModStrings.Get(nameKey), chord),
                () =>
                {
                    if (act == null)
                    {
                        AgeWidgets.Press(it);
                        return;
                    }

                    act(it);
                },
                () => AgeWidgets.Enabled(AgeWidgets.Transform(it)),
                AgeWidgets.Readable(AgeWidgets.Raw(AgeWidgets.Transform(it)))
            );
            if (value != null)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(value));
            }

            AgeWidgets.Point(vtable, it);
            found.Add(
                new Cell
                {
                    Widget = AgeWidgets.Transform(it),
                    Id = ControlId.For(it, "hud:" + key),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The way back to the notifications the scan view is holding up.
        ///
        /// The scan view suppresses every notification pop-up while it is open
        /// (<c>GuiManager.CanShowNotifications</c> :1584), and this button is how the game offers the
        /// player the ones that queued up behind it: a click is <c>ToggleScanView</c>
        /// (<c>EndTurnWindow.OnPendingNotificationCb</c> :1368-1371), which leaves the scan view and lets
        /// the pop-ups arrive.
        ///
        /// It lives in the window's <c>ScanViewGroup</c>, so normal view never draws it whatever starts
        /// its fade - and the game does FADE it rather than hide it (modifiers started on a notification
        /// arriving :1708-1714 and on the turn being ended in scan view :1684-1690, run backwards when
        /// the turn validates :1692-1706, reset on every view switch :1678-1682). A faded-out control is
        /// still <c>Visible</c>, so the node exists only while the player can actually SEE it
        /// (<see cref="AgeWidgets.Painted"/>). Nothing announces its arrival: it is there to be found,
        /// not to interrupt.
        ///
        /// The game writes no caption on it - a bare icon whose tooltip is a sentence about what a click
        /// would do - so the name is the mod's.
        /// </summary>
        private void AddPendingNotifications(List<Cell> found, AgeTransform button)
        {
            // Kept although the cell carries this widget: the gate counts an ANIMATING alpha as drawn,
            // which is right for a window fading itself in and wrong here - this control is faded both
            // ways by the game as its own state, so its own disappearance would keep it declared for
            // the length of the fade. PAINTED is the stricter test it needs.
            if (!AgeWidgets.Painted(button))
            {
                return;
            }

            AgeTransform at = button;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(ModStrings.GalaxyPendingNotifications),
                () => AgeWidgets.Press(at),
                () => AgeWidgets.Enabled(at),
                AgeWidgets.Readable(AgeWidgets.Raw(at))
            );
            AgeWidgets.PointAt(vtable, at);
            found.Add(
                new Cell
                {
                    Widget = at,
                    Id = ControlId.For(at, "hud:pending-notifications"),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The switch tucked in beside the turn controls that shows what an ALLIANCE is coordinating: the
        /// requests allies pin on the map, and the panel they are sent from - the game opens the list and
        /// flips ping visibility together on one click
        /// (<c>EndTurnWindow.OnToggleRequestCb</c> :1337-1354).
        ///
        /// It is drawn on every game and switched off for an empire in no alliance, with the game's own
        /// sentence for why on its tooltip (<c>RequestToggleTooltipContent</c> :555-570) - which is the
        /// whole reason to declare it while it refuses: a control nobody can find is a feature nobody
        /// knows exists. The game writes no caption for it anywhere (a bare icon, whose tooltip is a
        /// sentence about what a click would do rather than a name), so the name is the mod's.
        /// </summary>
        private void AddRequestToggle(List<Cell> found, AgeControlToggle toggle)
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            // Banding input: AddCell appends without the gate's question, and the toggle bands with the
            // rest of the corner.
            if (!AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Offered(at);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => ModStrings.Get(ModStrings.GalaxyAllianceRequests),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                enabled,
                tooltip
            );
            GraphNodes.AddRefusal(vtable, tooltip, enabled);
            AgeWidgets.Point(vtable, it, tooltip, widget);
            found.Add(
                new Cell
                {
                    Widget = widget,
                    Id = ControlId.For(toggle, "hud:alliance-requests"),
                    Vtable = vtable,
                }
            );
        }

        // ---- the multiplayer half of the turn cluster ----

        /// <summary>
        /// Whether the game is still in step with the other players, and the host's way out when it is
        /// not.
        ///
        /// The game draws the state as a tinted icon and puts the whole of its meaning on a tooltip
        /// (<c>EndTurnWindow.RefreshSyncState</c> :1254-1269 hangs the <c>SyncStatus&lt;state&gt;</c>
        /// element's description there), so the sentence is what this row SAYS rather than something
        /// hanging off it. The group is drawn only outside single player (:734), which is what keeps
        /// every line here absent from a solo game.
        ///
        /// The button beside it returns everybody to the lobby to reload the last auto-save
        /// (<c>OnDesyncStatusClickCb</c> :1318-1321) and is switched on only for the host, and only on a
        /// checksum mismatch - so it is declared while refusing, like every other button the mod
        /// declares: knowing the way out exists is the point.
        /// </summary>
        private void AddSync(List<Cell> found, EndTurnWindow window)
        {
            AgeTransform group = window.SyncGroup;
            // Banding input: AddCell appends without the gate's question, and the game draws this group
            // only for the host on a checksum mismatch.
            if (!AgeWidgets.Visible(group))
            {
                return;
            }

            EndTurnWindow it = window;
            // The sync state exists only as the words on the group's own tooltip, so the tooltip is
            // what the row declares and the readout says them by the ordinary rule - rather than the
            // row copying them into a value of its own and the buffer holding them twice.
            NodeVtable vtable = GraphNodes.Readout(
                () => ModStrings.Get(ModStrings.GalaxySyncState),
                () => null,
                null,
                it.SyncTooltip
            );
            AgeWidgets.PointAt(vtable, group);
            found.Add(
                new Cell
                {
                    Widget = group,
                    Id = ControlId.For(group, "hud:sync"),
                    Vtable = vtable,
                }
            );

            AddTurnButton(found, window.DesyncButton, "desync", ModStrings.GalaxyReturnToLobby, null);
        }

        /// <summary>
        /// Where the other players are in their turn: how many are still playing, and a line each for
        /// what the game says about them.
        ///
        /// Read off the ring of slots the game draws around the End Turn button - which is drawn in
        /// multiplayer only (:735) and, unlike the players list, is NOT gated on where the mouse is
        /// (<c>EndTurnWindow.SpecificUpdate</c> :906-921 shows that list only while the physical cursor
        /// is inside the button, and the mod moves no cursor). Each slot already carries the game's own
        /// sentence about its player - leader and faction, then the state word
        /// (<c>CompetitorOrbitalSlot.Refresh</c> :45-68) - so nothing here recomputes a player state.
        ///
        /// One row rather than one per player: the cluster is a handful of buttons in the corner of the
        /// screen, and eight more stops in it would be walked past on every pass. The per-player lines
        /// are the row's reviewable content.
        /// </summary>
        private void AddPlayers(List<Cell> found, EndTurnWindow window)
        {
            AgeTransform ring = window.CompetitorsCircularTable;
            // Banding input, and a different widget: the ring is what a single-player game does not
            // draw, while the one cell below stands on it and is read for every player's line.
            if (!AgeWidgets.Visible(ring))
            {
                return;
            }

            EndTurnWindow it = window;
            NodeVtable vtable = GraphNodes.Readout(
                () => ModStrings.Get(ModStrings.GalaxyPlayers),
                () => PlayersText(it),
                () => PlayerLines(it),
                null,
                // The count changes as players end their turn, and the watch below is what announces
                // that wherever the player is standing; a watched value would say it twice here.
                false
            );
            found.Add(
                new Cell
                {
                    Widget = ring,
                    Id = ControlId.For(ring, "hud:players"),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The clocks a multiplayer game can be running: the whole game's, which the game writes as a
        /// label, and the current turn's, which it draws as arcs around the End Turn button with no
        /// number written anywhere.
        ///
        /// Neither value is watched. Both change every second, and a value that re-announces itself
        /// under the cursor would talk over everything else the player is doing; asked for, they are
        /// current.
        /// </summary>
        private void AddTimers(List<Cell> found, EndTurnWindow window)
        {
            EndTurnWindow it = window;
            AgeTransform global = window.GlobalTimerLabel == null
                ? null
                : window.GlobalTimerLabel.AgeTransform;
            // Banding input: AddCell appends without the gate's question, and the game draws the timers
            // only in a game that runs one.
            if (AgeWidgets.Visible(global))
            {
                NodeVtable vtable = GraphNodes.Readout(
                    () => ModStrings.Get(ModStrings.GalaxyGlobalTimer),
                    () => OneLine(AgeText.Label(it.GlobalTimerLabel)),
                    null,
                    null,
                    false
                );
                found.Add(
                    new Cell
                    {
                        Widget = global,
                        Id = ControlId.For(global, "hud:global-timer"),
                        Vtable = vtable,
                    }
                );
            }

            AgeTransform arc = window.CommonTimerArc == null
                ? null
                : window.CommonTimerArc.AgeTransform;
            if (arc == null || TimerSeconds(window) < 0)
            {
                return;
            }

            NodeVtable turnTimer = GraphNodes.Readout(
                () => ModStrings.Get(TimerNameKey(it)),
                () => ModStrings.Format(ModStrings.GalaxyTimerSeconds, TimerSeconds(it)),
                null,
                null,
                false
            );
            found.Add(
                new Cell
                {
                    Widget = arc,
                    Id = ControlId.For(arc, "hud:turn-timer"),
                    Vtable = turnTimer,
                }
            );
        }

        /// <summary>
        /// The wall clock the game can draw above the End Turn button - the real time of day, not
        /// anything about the game.
        ///
        /// The player switches it on in the options ("Display In-Game Clock") and picks its format
        /// there, and the game writes the label once a minute from <c>DateTime.Now</c>
        /// (<c>EndTurnWindow.UpdateRealTimeClockCoroutine</c> :946-969). The row is exactly the label:
        /// no arithmetic, no format of the mod's, so a player who chose 24-hour time hears 24-hour time.
        ///
        /// Declared only while the game is drawing it - the option on, no global timer in the way, and
        /// no save in flight, which is one flag the window itself computes
        /// (<c>Refresh</c> :880) - and not watched, for the same reason as the timers above.
        /// </summary>
        private void AddRealTimeClock(List<Cell> found, EndTurnWindow window)
        {
            AgeTransform clock = window.RealTimeClockLabel == null
                ? null
                : window.RealTimeClockLabel.AgeTransform;
            // Banding input: same door as the timers - AddCell takes the clock without asking the gate.
            if (!AgeWidgets.Visible(clock))
            {
                return;
            }

            EndTurnWindow it = window;
            NodeVtable vtable = GraphNodes.Readout(
                () => ModStrings.Get(ModStrings.GalaxyRealTimeClock),
                () => OneLine(AgeText.Label(it.RealTimeClockLabel)),
                null,
                null,
                false
            );
            found.Add(
                new Cell
                {
                    Widget = clock,
                    Id = ControlId.For(clock, "hud:real-time-clock"),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// How many players have not ended their turn.
        ///
        /// RE-DERIVES a private list: the game counts exactly this into <c>EndTurnWindow.unreadySlots</c>
        /// on every refresh (:859-873) and keeps it to itself, so the only way to have the figure is to
        /// count the same slots the same way - the ring's children whose unready icon is showing, under
        /// the same gate the game puts the whole count behind (<c>CompetitorsCircularTable.Visible</c>,
        /// which the game sets false for a single-player session at :735). The gate is the widget's own
        /// flag rather than whether it is really on screen, because that is the flag the game's own test
        /// reads: asking a stricter question would make the two counts disagree in exactly the frames a
        /// wait is being announced. -1 is no ring at all, which is every single-player game.
        /// </summary>
        private static int PlayersPlaying(EndTurnWindow window)
        {
            try
            {
                AgeTransform ring = window == null ? null : window.CompetitorsCircularTable;
                // Spoken count: this figure is said as "N still playing", and -1 is how the caller
                // hears that there is no ring to count - which is every single-player game.
                if (ring == null || !ring.Visible)
                {
                    return -1;
                }

                IList<AgeTransform> slots = ring.Children;
                int playing = 0;
                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    CompetitorOrbitalSlot slot = Slot(slots[i]);
                    // Spoken count: the icon IS the fact counted - how many empires have not ended their turn.
                    if (slot != null && slot.UnreadyIcon != null && slot.UnreadyIcon.Visible)
                    {
                        playing++;
                    }
                }

                return playing;
            }
            catch (Exception e)
            {
                Log.Warn("hud: counting the players still playing threw: " + e);
                return -1;
            }
        }

        private static string PlayersText(EndTurnWindow window)
        {
            int playing = PlayersPlaying(window);
            if (playing < 0)
            {
                return null;
            }

            return playing == 0
                ? ModStrings.Get(ModStrings.GalaxyPlayersAllReady)
                : ModStrings.Plural(
                    ModStrings.GalaxyPlayerPlaying,
                    ModStrings.GalaxyPlayersPlaying,
                    playing
                );
        }

        /// <summary>A line per player, in the game's own words: leader and faction, then where they are
        /// in their turn - and, for a human who is not the local player, the whisper instruction the
        /// game appends to the same tooltip, which is reviewable rather than spoken.</summary>
        private static IList<string> PlayerLines(EndTurnWindow window)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTransform ring = window == null ? null : window.CompetitorsCircularTable;
                // Content: which lines the players' row is reviewed with. Lines, not nodes - the ring
                // declares one cell and these are what it says.
                if (!AgeWidgets.Visible(ring))
                {
                    return lines;
                }

                IList<AgeTransform> slots = ring.Children;
                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    CompetitorOrbitalSlot slot = Slot(slots[i]);
                    if (slot == null)
                    {
                        continue;
                    }

                    foreach (string line in AgeText.Lines(AgeText.Tooltip(slot.Tooltip)))
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the player states threw: " + e);
            }

            return lines;
        }

        private static CompetitorOrbitalSlot Slot(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<CompetitorOrbitalSlot>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// How long the running turn timer has left, in whole seconds, or -1 while no timer is running.
        ///
        /// The window draws the three timers as ARCS with no number on them and keeps the end time and
        /// the kind of timer in private fields (:157-163, written from the timer service's own event
        /// :1520-1530), so there is nothing on screen to read and the fields are the only source. The
        /// same expression the window uses: end time minus the game's clock (:1071).
        /// </summary>
        private static int TimerSeconds(EndTurnWindow window)
        {
            try
            {
                if (window == null || TimerKind(window) == GameTimerType.None)
                {
                    return -1;
                }

                FieldInfo field = TimerField("currentTimerEndTime", ref _timerEnd);
                if (field == null)
                {
                    return -1;
                }

                double left = (double)field.GetValue(window) - global::Game.Time;
                return left <= 0.0 ? -1 : (int)left;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>Which of the three clocks is running, so the row can name itself: the turn's own
        /// timer, the overtime the previous turns banked, or the shortened one the last player left in
        /// the turn is given.</summary>
        private static GameTimerType TimerKind(EndTurnWindow window)
        {
            try
            {
                FieldInfo field = TimerField("currentTimerType", ref _timerKind);
                return field == null
                    ? GameTimerType.None
                    : (GameTimerType)field.GetValue(window);
            }
            catch (Exception)
            {
                return GameTimerType.None;
            }
        }

        private static string TimerNameKey(EndTurnWindow window)
        {
            switch (TimerKind(window))
            {
                case GameTimerType.Overtime:
                    return ModStrings.GalaxyOvertimeTimer;
                case GameTimerType.LastPlayer:
                    return ModStrings.GalaxyLastPlayerTimer;
                default:
                    return ModStrings.GalaxyTurnTimer;
            }
        }

        /// <summary>The window field of that name, looked up once and remembered: these are read every
        /// frame the clock is running, and the lookup itself is
        /// <see cref="GameHandlers.Field"/>.</summary>
        private static FieldInfo TimerField(string name, ref FieldInfo cache)
        {
            return cache = cache ?? GameHandlers.Field(typeof(EndTurnWindow), name);
        }

        /// <summary>The button's own caption, which the game writes over two lines and rewrites while
        /// a turn is being processed - so it says what the button is doing, not only what it is.
        ///
        /// It ends with the chord that ends the turn from anywhere (<see cref="UiActions.EndTurn"/>),
        /// because the one control every turn passes through is the one worth being able to reach
        /// without walking to it, and a key nothing names is a key nobody finds.</summary>
        private static string EndTurnLabel(EndTurnWindow window)
        {
            string caption = OneLine(AgeText.Label(window.EndTurnTitle));
            return ChordNames.Label(
                string.IsNullOrEmpty(caption)
                    ? ModStrings.Get(ModStrings.GalaxyEndTurn)
                    : caption,
                UiActions.EndTurn,
                0
            );
        }

        /// <summary>
        /// End the turn the way the game's own end-turn SHORTCUT does: by ASKING it to
        /// (<c>EndTurnWindow.HandleInput(InputAction.EndTurn)</c> :637-654, which is public and is the
        /// path the game itself takes for a key). Its three gates, the armed cursor put back to the
        /// plain galaxy one - an order still waiting for a target would otherwise eat the turn - and the
        /// session's own TryToEndTurn are all the game's, and none of them is restated here. The button
        /// is not pressed: the shortcut path is what a key is.
        ///
        /// It answers false for exactly the turn it will not end, and that is the refusal: the key
        /// speaks the END-TURN NODE's own reading - its name, which turn it is and "unavailable" -
        /// because the player pressing this from the far side of the page cannot see the button greying
        /// out. That reading is taken out of the graph rather than composed again here, so the key and
        /// the button can never say different things; the game's sentence about WHY stays where the
        /// button keeps it, in the review buffer (<see cref="EndTurnReason"/>). Success says nothing at
        /// all, exactly as pressing the button says nothing.
        ///
        /// False from THIS method means the key was not this page's business (no turn controls drawn),
        /// which is what leaves the press alone.
        /// </summary>
        public static bool EndTurnByKey()
        {
            EndTurnWindow window = TurnWindow();
            // Flow control: false is how the caller hears that the key was not this page's business,
            // which is what leaves the press alone.
            if (window == null || !AgeWidgets.Visible(window.AgeTransform))
            {
                return false;
            }

            bool ended = false;
            try
            {
                ended = window.HandleInput(InputAction.EndTurn);
            }
            catch (Exception e)
            {
                Log.Warn("hud: ending the turn from the keyboard threw: " + e);
            }

            if (!ended)
            {
                SpeakTurnRefusal("hud:end-turn");
            }

            return true;
        }

        /// <summary>
        /// Go to the next fleet with nothing to do, from anywhere the turn controls are drawn
        /// (`docs/interaction.md`, Control+Alt+F). The act is the one the mod already does better than a
        /// press of the button would (<see cref="NextIdleFleet"/>), so the key and the button's own Enter
        /// are the same route - including the galaxy page's single-camera-move version of it.
        ///
        /// A refusal speaks the BUTTON's own reading out of the graph - its name, how many fleets are
        /// idle and "unavailable" - for the reason the end-turn key does it: the player pressing this from
        /// the far side of the page cannot see the button greyed out, and a global key silent both when it
        /// works and when it refuses is unreadable. Success says nothing, exactly as pressing the button
        /// says nothing; the arrival announces itself.
        ///
        /// False means the key was not this page's business (no turn controls drawn), which is what
        /// leaves the press alone.
        /// </summary>
        public static bool NextIdleFleetByKey()
        {
            EndTurnWindow window = TurnWindow();
            // Flow control: false is how the caller hears that the key was not this page's business.
            if (window == null || !AgeWidgets.Visible(window.AgeTransform))
            {
                return false;
            }

            // Flow control, and availability: the key is only this page's business where the button is
            // drawn at all, and the refusal below reads that same button's own switched-off state.
            AgeTransform button = AgeWidgets.Transform(window.NextIdleFleetButton);
            if (button == null || !AgeWidgets.Visible(button))
            {
                return false;
            }

            // The game switches this button off exactly while nothing is idle
            // (`EndTurnWindow.UpdateIdleFleetsCollectionAndButton` :1038), which is the same question the
            // node beside it answers, so the key and the button can never disagree.
            if (!AgeWidgets.Enabled(button))
            {
                SpeakTurnRefusal("hud:next-idle-fleet");
                return true;
            }

            NextIdleFleet(window.NextIdleFleetButton);
            return true;
        }

        /// <summary>
        /// Order everything that was told to move to make its move, from anywhere the turn controls are
        /// drawn (`docs/interaction.md`, Control+Alt+A). The act is the button's own click and nothing
        /// more - <c>EndTurnWindow.OnApplyMovementsCb</c> :1356-1361 posts one
        /// <c>OrderMoveIdleFleets</c> and touches no cursor, no selection and no camera - so the key
        /// replays the press rather than doing anything of its own.
        ///
        /// A refusal speaks the BUTTON's own reading out of the graph, for the reason the two keys
        /// beside it do: a player pressing this from the far side of the page cannot see the button
        /// greyed out. Success says nothing, exactly as pressing the button says nothing.
        ///
        /// False means the key was not this page's business (no turn controls drawn), which is what
        /// leaves the press alone.
        /// </summary>
        public static bool ApplyMovementsByKey()
        {
            EndTurnWindow window = TurnWindow();
            // Flow control: false is how the caller hears that the key was not this page's business.
            if (window == null || !AgeWidgets.Visible(window.AgeTransform))
            {
                return false;
            }

            // Flow control, and availability: the key is only this page's business where the button is
            // drawn at all, and the refusal below reads that same button's own switched-off state.
            AgeTransform button = AgeWidgets.Transform(window.ApplyMovementsButton);
            if (button == null || !AgeWidgets.Visible(button))
            {
                return false;
            }

            // The game switches this button off unless the turn can be ended AND something is actually
            // waiting to move (`EndTurnWindow.UpdateApplyMovementsButton` :1006-1016), which is the same
            // question the node beside it answers, so the key and the button can never disagree.
            if (!AgeWidgets.Enabled(button))
            {
                SpeakTurnRefusal("hud:apply-movements");
                return true;
            }

            AgeWidgets.Press(window.ApplyMovementsButton);
            return true;
        }

        /// <summary>What one of the turn corner's controls says about itself right now, read out of the
        /// graph rather than composed again here - the refusal the player would have heard by walking to
        /// it.</summary>
        private static void SpeakTurnRefusal(string structuralKey)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GraphRender render = navigator == null ? null : navigator.Render;
            GraphNode node =
                render == null ? null : render.NodeAt(ControlId.Structural(structuralKey));
            if (node != null)
            {
                Voice.Say(GraphAnnouncer.LeafText(node), true);
            }
        }

        /// <summary>Which turn it is. Read from the turn service rather than from the label beside the
        /// button, which the game writes as an icon token followed by the number.</summary>
        private static string TurnText(EndTurnWindow window)
        {
            int turn = Turn(window);
            return turn < 0 ? null : ModStrings.Format(ModStrings.GalaxyTurn, turn);
        }

        /// <summary>
        /// The three gates the game's own end-turn shortcut passes, in its own order: nothing is in
        /// the way, the tutorial is not holding the turn back, and the session will accept it.
        /// </summary>
        private static bool CanEndTurn(EndTurnWindow window)
        {
            try
            {
                if (!Gui.GuiGameWindowService.CanEndTurnByShortcut)
                {
                    return false;
                }

                if (window.EndTurnDisabler != null && window.EndTurnDisabler.IsTargetDisabled())
                {
                    return false;
                }

                return window.EndTurnService != null
                    && window.EndTurnService.Target != null
                    && window.EndTurnService.Target.CanEndTurn();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Why the button is refusing, when the game says. It hangs no tooltip on this one
        /// button, but the tutorial holding an element back is a thing the game has words for and puts
        /// on every other element it holds back, so those are the words used here.</summary>
        private static IList<string> EndTurnReason(EndTurnWindow window)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTooltip tooltip = AgeWidgets.Readable(
                    AgeWidgets.Raw(AgeWidgets.Transform(window.EndTurnButton))
                );
                foreach (string line in AgeText.Lines(AgeText.Tooltip(tooltip)))
                {
                    lines.Add(line);
                }

                if (window.EndTurnDisabler != null && window.EndTurnDisabler.IsTargetDisabled())
                {
                    string reason = AgeText.Clean("%TutorialDisabledElementDescription");
                    if (!string.IsNullOrEmpty(reason))
                    {
                        lines.Add(reason);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the end-turn reason threw: " + e);
            }

            return lines;
        }

        /// <summary>
        /// Go to the next fleet with nothing to do - the galaxy page's own route where the player is on
        /// the galaxy page, and the game's button everywhere else.
        ///
        /// The turn cluster is drawn on every page in the game, and only the galaxy page has a map to
        /// land a cursor on; on any other page the button's own behaviour (fly the camera to the fleet,
        /// select it, and let the galaxy page pick the arrival up) is still the right one and is left
        /// alone. On the galaxy page it costs a second camera move, which is what the page's own route
        /// takes out (<see cref="GalaxyHudScreen.GoToNextIdleFleet"/>).
        /// </summary>
        private static void NextIdleFleet(AgeControlButton button)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GalaxyHudScreen galaxy = navigator == null ? null : navigator.Screen as GalaxyHudScreen;
            if (galaxy == null || !galaxy.GoToNextIdleFleet())
            {
                AgeWidgets.Press(button);
            }
        }

        /// <summary>How many fleets are waiting to be given something to do, counted the way the
        /// button beside it counts them.</summary>
        private string IdleFleetsText()
        {
            try
            {
                Empire empire = Gui.PlayerEmpire;
                if (empire == null)
                {
                    return null;
                }

                global::FleetsScreen.GetIdleFleets(empire, ref _idleFleets);
                return ModStrings.Format(ModStrings.GalaxyIdleFleets, _idleFleets.Count);
            }
            catch (Exception)
            {
                return null;
            }
        }

    }
}

using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// Dealing with the pirates: the window the diplomacy page's pirate button opens, and the one a
    /// pirate-held system's diplomacy button opens.
    ///
    /// Five bands, in the order it draws them: how powerful the pirates have become; where the player
    /// stands with them, with the thresholds the standing moves between; what the next pirate fleet will
    /// be; the things the player can do; and the two stocks along the bottom.
    ///
    /// The next-fleet band ends in a tick box that slides a panel of detail out under itself
    /// (<c>OnToggleNextFleetInfoCb</c> :529-538): the command points and movement of the fleet, and a tile
    /// per ship in it. It is declared as the checkbox it is, and the panel it opens is declared while it is
    /// open - by visibility, like everything else here, so nothing has to know which state the box was
    /// left in.
    ///
    /// A reinforcement threshold draws only its ORDINAL on a circle and explains itself in a tooltip the
    /// renderer assembles (<c>RefreshReinforcementsThresholdItem</c> :464-479 sets the class and the
    /// target), so what it says is composed here from the costs behind the track and the pointer is
    /// aimed at the circle, where that tooltip actually hangs; where there are none, the game draws its
    /// own "no reinforcements" label instead and that is what the band holds.
    ///
    /// The band of actions is the shared one (<see cref="DiplomacyActions"/>) - this window's action items
    /// are the same prefab shape as the minor-faction window's and the Academy's.
    /// </summary>
    public sealed class PirateDiplomacyScreen : Screen
    {
        private static readonly object PowerStop = "pirate:power";
        private static readonly object StandingStop = "pirate:standing";
        private static readonly object FleetStop = "pirate:next-fleet";
        private static readonly object ActionsStop = "pirate:actions";
        private static readonly object TreasuryStop = "pirate:treasury";

        private const string Keys = "pirate:";

        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<DiplomacyActions.Row> _actions = new List<DiplomacyActions.Row>();

        public override string Key
        {
            get { return ModStrings.ScreenPirateDiplomacy; }
        }

        /// <summary>Beside the minor-faction window, which it can never be up with - both are exclusive
        /// modals over the same pages.</summary>
        public override int Layer
        {
            get { return 44; }
        }

        public override string ScreenName
        {
            get { return ModStrings.Get(ModStrings.ScreenPirateDiplomacy); }
        }

        public override object InitialFocusStop
        {
            get { return ActionsStop; }
        }

        /// <summary>
        /// Escape's own job, done by the mod's Back so that one key leaves every window the same way.
        ///
        /// The key is not taken FROM the game - it is handed straight back to it:
        /// <see cref="WindowShape.PressClose"/> presses the control the window itself wires its
        /// dismissal to, so whatever that costs - a confirmation, a page switch, an order the game was
        /// always going to post - is the game's own answer and not a copy of it. Claimed only while
        /// such a control is drawn; a window offering none keeps its Escape untouched.
        /// </summary>
        public override bool ConsumesBack
        {
            get { return WindowShape.CloseControl(Window()) != null; }
        }

        public override bool Back()
        {
            return WindowShape.PressClose(Window());
        }

        public override bool IsActive()
        {
            try
            {
                PirateDiplomacyModalWindow window = Window();
                return window != null
                    && window.Shown
                    && window.IsReady
                    && window.PirateEmpire != null
                    && !WindowShape.Buried(window);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            PirateDiplomacyModalWindow window = Window();
            if (window == null || window.PirateEmpire == null)
            {
                return;
            }

            BuildPower(builder, window);
            BuildStanding(builder, window);
            BuildNextFleet(builder, window);
            BuildActions(builder, window);
            BuildTreasury(builder, window);
        }

        /// <summary>The pirates' power level, drawn on a circular gauge with the level written across it.
        /// The gauge is wired to a click that does nothing but exist, so it is read as the readout it is.
        ///
        /// The window's own title comes first, outside the band's name: it carries the only sentence
        /// saying what this whole window is for, and a screen's name is a spoken phrase with no buffer
        /// behind it, so the title is a row - the first one, where the player lands coming back up the
        /// stops (the same shape the minor-faction window's identity band opens with).
        /// </summary>
        private void BuildPower(GraphBuilder builder, PirateDiplomacyModalWindow window)
        {
            builder.BeginStop(PowerStop);
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "Title", 3),
                Keys + "window-title"
            );
            Cells.EmitLinear(builder, _cells);

            builder.PushContext(ModStrings.Get(ModStrings.PiratePower));
            _cells.Clear();
            try
            {
                AgeTransform gauge = window.PiratePowerGauge == null
                    ? null
                    : window.PiratePowerGauge.AgeTransform;
                if (gauge != null)
                {
                    _cells.Add(Cells.Readout(gauge, AgeWidgets.Raw(gauge), Keys + "power"));
                }
            }
            catch (Exception e)
            {
                Log.Warn("pirate diplomacy: reading the power gauge threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>
        /// Where the player stands: the icon whose tooltip is the only place the game explains what the
        /// pirate relation IS, and the state's own word with the sentence for what that state does (and, at
        /// peace, the turns the peace has left, which the game appends to the word itself).
        ///
        /// Named widget by widget rather than read as a band, because reading the group by shape declares
        /// the state label twice - once as the band's line and once as its own (measured). The markers along
        /// the bar are not declared at all: the game gives them no words and no tooltip, only a position
        /// (<c>RefreshStandingGaugeItem</c> :450-461 sets nothing but <c>PercentRight</c>), and the number
        /// they mark off is not drawn anywhere either - the cursor's place on the bar IS the number, so
        /// there is nothing to read that the state word does not already say.
        /// </summary>
        private void BuildStanding(GraphBuilder builder, PirateDiplomacyModalWindow window)
        {
            AgeTransform group = window.StandingGroup;
            // Flow control: a stop and a context would be opened around nothing, and the icon below is
            // found by a named search through the group.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            builder.BeginStop(StandingStop);
            builder.PushContext(ModStrings.Get(ModStrings.PirateStanding));
            _cells.Clear();
            try
            {
                AgeTransform icon = AgeWidgets.ChildNamed(group, "StandingIcon", 3);
                AgeTooltip what = AgeWidgets.Raw(icon);
                if (icon != null && what != null)
                {
                    // A bare icon: the sentence on it is the only name it has, so it is the label, and
                    // the readout drops that line from the tooltip it goes on to announce.
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(CardActions.NameFromTooltip(what)),
                        },
                        Sections = GraphNodes.Sections(null, what),
                    };
                    AgeWidgets.PointAt(vtable, icon);
                    Cells.Add(
                        _cells,
                        icon,
                        ControlId.For(icon, Keys + "standing-icon"),
                        vtable
                    );
                }

                Cells.AddReadout(_cells, AgeWidgets.Transform(window.StandingLabel), Keys + "standing");
            }
            catch (Exception e)
            {
                Log.Warn("pirate diplomacy: reading the standing threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>What is coming: when, how strong, what extra firepower the pirates have banked, and -
        /// while the tick box has it open - the fleet's command points, its speed and the ships in
        /// it.</summary>
        private void BuildNextFleet(GraphBuilder builder, PirateDiplomacyModalWindow window)
        {
            AgeTransform group = window.NextFleetGroup;
            // Flow control: a stop and a context would be opened around nothing, and every reading
            // under it walks a widget of its own.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            builder.BeginStop(FleetStop);
            builder.PushContext(ModStrings.Get(ModStrings.PirateNextFleet));
            _cells.Clear();
            try
            {
                Cells.AddReadout(_cells, AgeWidgets.Transform(window.NextFleetCooldownLabel), Keys + "cooldown");
                Line(window.NextFleetHealthLabel, "%ShipStatHealthTitle", "health");
                Line(
                    window.NextFleetOffenseLabel,
                    "%ShipStatOffensiveMilitaryPowerTitle",
                    "offense"
                );
                Line(
                    window.NextFleetDefenseLabel,
                    "%ShipStatDefensiveMilitaryPowerTitle",
                    "defense"
                );
                Thresholds(window);
                AddToggle(window.NextFleetInfoToggle);
                Detail(window);
            }
            catch (Exception e)
            {
                Log.Warn("pirate diplomacy: reading the next fleet threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>
        /// One of the five figures the window draws for the fleet that is coming.
        ///
        /// Each is a number and an icon in a group, and the group carries the sentence saying what the
        /// number is of ("The total Health of all Ships in the next Pirate Fleet" - measured on the
        /// prefab, which puts the tooltip on the group and not on the icon inside it). That sentence is a
        /// gloss, not a name: these are the same six ship statistics the design panels draw, so the
        /// caption is the game's own <c>%ShipStat…Title</c> for each.
        /// </summary>
        private void Line(AgePrimitiveLabel label, string titleKey, string key)
        {
            Cells.AddStat(_cells, label, titleKey, Keys + key);
        }

        /// <summary>
        /// The firepower the pirates have banked towards their next fleet: the caption the game draws
        /// over the track, then the marks strung along it - or, where the fleet has no reinforcements
        /// to earn at all, the game's own label saying so.
        ///
        /// The caption is a row rather than the band's name because the sentence explaining what the
        /// track measures hangs on the LABEL itself, not on the group around it, and nothing else here
        /// would carry it. The marks are the shared circle track (<see cref="ThresholdTracks"/>); what
        /// each one SAYS is worked out here, because the window overwrites every circle's figure with
        /// the mark's ordinal (<c>RefreshReinforcementsThresholdItem</c> :473) and shows the distance
        /// only as a bar filling behind the circle.
        /// </summary>
        private void Thresholds(PirateDiplomacyModalWindow window)
        {
            AgeTransform none = window.NoReinforcementsLabel;
            if (none != null)
            {
                Cells.AddReadout(_cells, none, Keys + "no-reinforcements");
            }

            Cells.AddReadout(
                _cells,
                AgeWidgets.ChildNamed(window.NextFleetGroup, "ReinforcementsTitle", 3),
                Keys + "reinforcements-title"
            );

            IList<AgeTransform> children = AgeWidgets.DrawnChildren(
                window.ReinforcementsThresholdsTable
            );
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform at = children[i];
                if (at == null)
                {
                    continue;
                }

                ThresholdTracks.Add(_cells, at, Mark(at, i), Keys + "threshold/" + i);
            }
        }

        /// <summary>
        /// What one mark on the firepower track says: which mark it is, and either that the pirates
        /// have banked it or how far along its own stretch of the track they are.
        ///
        /// Both halves are READ OFF the widgets the game filled, never recomputed from the costs. The
        /// bar snaps at both ends (<c>ThresholdItem.Bind</c> :26-49): a fraction at or below the
        /// item's own <c>minRatio</c> draws empty and one at or above <c>1 - minRatio</c> draws full,
        /// so arithmetic over the same stock and costs announced a figure the bar was not drawing near
        /// either end of every mark. The filled length is the same <c>PercentRight</c>/
        /// <c>PercentLeft</c> pair <see cref="NegotiationScreen"/> reads its own notches from, and the
        /// circle is the game's own "this one is banked": full alpha once the mark is reached and 0.3
        /// while it is still filling (:51).
        ///
        /// The number is the reading, not the arithmetic behind it: the raw stock and cost are the
        /// game's own bookkeeping and are never spoken (owner ruling 2026-08-30).
        ///
        /// Nothing at all where the item carries no <c>ThresholdItem</c>. That cannot be met on a
        /// drawn track - the game dereferences the component for every circle it binds (:470) - and a
        /// mark with no bar to read has no distance to give.
        /// </summary>
        private static string Mark(AgeTransform widget, int index)
        {
            ThresholdItem item = widget == null ? null : widget.GetComponent<ThresholdItem>();
            if (item == null)
            {
                return null;
            }

            string drawn = AgeWidgets.TextOf(widget);
            string ordinal = string.IsNullOrEmpty(drawn) ? (index + 1).ToString() : drawn;
            // Content read: the circle's alpha is the game's own word for "banked", not a
            // question about whether the mark is on the screen.
            if (item.Circle != null && item.Circle.Alpha >= 1f)
            {
                return ModStrings.Format(ModStrings.PirateThresholdReached, ordinal);
            }

            // Content read: the two bars are one length drawn twice - the filled part measured from
            // the right, the remainder from the left - and which of them is drawn is what says which
            // one carries the figure, the game hiding the filled one where there is nothing to fill
            // (:32-35).
            float filled = AgeWidgets.Paints(item.ProgressCurrent)
                ? item.ProgressCurrent.PercentRight
                : (item.ProgressOverall == null ? 0f : item.ProgressOverall.PercentLeft);
            return ModStrings.Format(
                ModStrings.PirateThresholdProgress,
                ordinal,
                (int)Math.Round(filled)
            );
        }

        private void AddToggle(AgeControlToggle toggle)
        {
            AgeTransform at = AgeWidgets.Transform(toggle);
            if (at == null)
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            NodeVtable vtable = GraphNodes.Checkbox(
                CardActions.NameFromTooltip(tooltip),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Offered(at),
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, at);
            Cells.Add(_cells, at, ControlId.For(at, Keys + "fleet-detail"), vtable);
        }

        /// <summary>The panel the tick box slides out, while it is out: the fleet's command points and
        /// speed, and a tile per ship the pirates would field.</summary>
        private void Detail(PirateDiplomacyModalWindow window)
        {
            GuiPanel panel = window.NextFleetInfoPanel;
            AgeTransform at = panel == null ? null : panel.AgeTransform;
            // Flow control: the slide-out panel's whole reading, including a tile per ship.
            if (at == null || !panel.Shown || !AgeWidgets.Visible(at))
            {
                return;
            }

            // The same shape as the three totals above.
            Line(
                window.NextFleetCommandPointsLabel,
                "%ShipStatCommandPointsTitle",
                "command-points"
            );
            Line(window.NextFleetMovementLabel, "%ShipStatMovementTitle", "movement");
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(
                window.NextFleetEstimatedShipsTable
            );
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform tile = children[i];
                if (tile == null)
                {
                    continue;
                }

                AgeTooltip tooltip = AgeWidgets.Raw(tile);
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => Named(tile, tooltip)),
                    },
                    Sections = GraphNodes.Sections(null, tooltip),
                };
                AgeWidgets.PointAt(vtable, tile);
                Cells.Add(_cells, tile, ControlId.For(tile, Keys + "ship/" + i), vtable);
            }
        }

        /// <summary>The diplomatic actions, under the word the window itself draws over them. The game
        /// captions this band "Actions" and hangs the sentence saying what it is on the GROUP around the
        /// label, so the caption is a row as well as the band's name - the same shape the minor-faction
        /// window's band is read under, with the mod's own word left as the fallback.</summary>
        private void BuildActions(GraphBuilder builder, PirateDiplomacyModalWindow window)
        {
            builder.BeginStop(ActionsStop);
            AgeTransform title = ActionsCaption(window);
            bool named = Captions.Push(
                builder,
                title,
                Keys + "actions-title",
                Captions.Text(title) ?? ModStrings.Get(ModStrings.DiplomacyActionsBand)
            );
            _actions.Clear();
            try
            {
                AgeTransform table = window.PirateActionsTable;
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform at = children[i];
                    DiplomacyActions.Add(
                        _actions,
                        at == null ? null : at.GetComponent<PirateDiplomacyActionItem>()
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("pirate diplomacy: reading the actions threw: " + e);
            }

            DiplomacyActions.Emit(builder, Keys.TrimEnd(':'), _actions);
            Captions.Pop(builder, named);
            WindowShape.Close(builder, window, Keys);
        }

        /// <summary>The group the window draws the actions caption inside - the widget carrying both the
        /// word and the sentence, neither of which the window exposes.</summary>
        private static AgeTransform ActionsCaption(PirateDiplomacyModalWindow window)
        {
            try
            {
                AgeTransform label = window == null
                    ? null
                    : AgeWidgets.ChildNamed(window.AgeTransform, "ActionsTitle", 4);
                return label == null ? null : label.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void BuildTreasury(GraphBuilder builder, PirateDiplomacyModalWindow window)
        {
            builder.BeginStop(TreasuryStop);
            builder.PushContext(ModStrings.Get(ModStrings.DiplomacyTreasury));
            _cells.Clear();
            try
            {
                DiplomacyActions.Treasury(_cells, window.MoneyLabel, window.EmpirePointLabel, Keys);
            }
            catch (Exception e)
            {
                Log.Warn("pirate diplomacy: reading the treasury threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>What a control is called: the words it draws, else the title the game gave its
        /// tooltip, else that tooltip's own first line (<see cref="CardActions.FirstLine"/>, which is
        /// where the words-are-really-on-the-widget test lives).</summary>
        private static string Named(AgeTransform widget, AgeTooltip tooltip)
        {
            string drawn = AgeWidgets.TextOf(widget);
            return string.IsNullOrEmpty(drawn)
                ? AgeWidgets.TooltipTitle(tooltip) ?? CardActions.FirstLine(tooltip)
                : drawn;
        }

        private static PirateDiplomacyModalWindow Window()
        {
            return GameWindows.Of<PirateDiplomacyModalWindow>();
        }
    }
}

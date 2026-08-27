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
    /// The window the game opens whenever it needs the player to pick one of their own FLEETS - which
    /// fleet a hero joins, which one an honour action is aimed at. It is the twin of the system picker
    /// (<see cref="SystemSelectionScreen"/>) and is built the same way, for the same reason: it is a
    /// GENERIC window whose callers each bring their own filter and their own reason for refusing a
    /// fleet, and none of that is written down here. What is modelled is what the window DRAWS.
    ///
    /// Three stops in drawn order - the sort headers, the table of fleets, then the button band - and the
    /// first two are <see cref="TableSheet"/>, where the whole reading of a <c>GuiTable</c> lives. This
    /// window binds the SAME <c>FleetListTable</c> column set the Military screen's own fleet list does,
    /// so the columns, the headings and the crossed edges are already right.
    ///
    /// Two differences from that screen, both the window's own:
    ///
    /// - A fleet the CALLER will not accept arrives in the window's <c>invalidObjectsList</c> and is drawn
    ///   with its line switched off (<c>Refresh</c> :119-144), which the sheet declares REFUSING with the
    ///   caller's own sentence. Fleets the game will not offer at all - a trade company's master fleet -
    ///   are left out of the list before it ever reaches the table (:126-129), so they are absent rather
    ///   than refused.
    /// - Confirming is the button, and only the button. The window also commits on a double click
    ///   (<c>OnLineDoubleClick</c> :181-188) and that gesture is deliberately not offered: a single
    ///   keystroke that both picked a fleet and handed it over would make every pass down the list a
    ///   decision. Enter on a row is the row's own click, which picks it and enables Validate.
    ///
    /// The refusal for Validate is written onto the window's own <c>ValidateTooltip</c> rather than onto
    /// the button (<c>ProcessSelection</c> :157-179), which is why that button's tooltip is named here
    /// instead of read off its transform.
    ///
    /// Escape is the game's. This is a plain <c>GuiModalWindow</c> with no <c>HandleInput</c>, so Exit
    /// hides it and commits nothing.
    /// </summary>
    public sealed class FleetSelectionScreen : Screen
    {
        private static readonly object LinesStop = "fleetsel:lines";
        private static readonly object ActionsStop = "fleetsel:actions";

        private readonly TableSheet _table;

        public FleetSelectionScreen()
        {
            _table = new TableSheet("fleetsel:", FleetOf);
            _table.RowName = FleetName;
        }

        public override string Key
        {
            get { return "screen.fleet-selection"; }
        }

        /// <summary>Over the Academy page that opens it and the galaxy underneath, and below everything
        /// this window can raise over itself.</summary>
        public override int Layer
        {
            get { return 26; }
        }

        /// <summary>What the window has written across its top. It does not expose the label, so it is
        /// found where it is drawn; the mod's own word covers the frames before it is written.</summary>
        public override string ScreenName
        {
            get
            {
                string title = Title(Window());
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenFleetSelection)
                    : title;
            }
        }

        /// <summary>The table, because it is drawn first and Tab does not wrap - its headings are the
        /// row above its first line, not a stop of their own.</summary>
        public override object InitialFocusStop
        {
            get { return LinesStop; }
        }

        public override bool IsActive()
        {
            try
            {
                FleetSelectionModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's own: Exit hides the window and commits nothing.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            FleetSelectionModalWindow window = Window();
            GuiTable table = Table(window);
            if (table == null)
            {
                return;
            }

            builder.BeginStop(LinesStop);
            _table.Headers(builder, table);
            _table.Rows(builder, table, Title(window));

            builder.BeginStop(ActionsStop);
            BuildActions(builder, window);
        }

        /// <summary>Cancel and Confirm, taken from the band they share rather than named: the window
        /// exposes Validate and leaves Cancel as its sibling, and reading the band keeps them in the order
        /// they are drawn in - one per row, the reading every bar of buttons gets. Validate is disabled
        /// until a fleet the caller accepts is picked, which is what makes it read unavailable with the
        /// caller's own sentence for why.</summary>
        private void BuildActions(GraphBuilder builder, FleetSelectionModalWindow window)
        {
            AgeTransform validate = ValidateTransform(window);
            AgeTransform band = validate == null ? null : validate.Parent;
            List<AgeTransform> buttons = new List<AgeTransform>();
            try
            {
                IList<AgeTransform> children = band == null ? null : band.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (child != null && AgeWidgets.Button(child) != null)
                    {
                        buttons.Add(child);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("fleet selection: reading the button band threw: " + e);
            }

            if (buttons.Count == 0)
            {
                return;
            }

            AgeTooltip refusal = ValidateTooltip(window);
            for (int i = 0; i < buttons.Count; i++)
            {
                AgeTransform button = buttons[i];
                AgeTooltip tooltip = ReferenceEquals(button, validate)
                    ? refusal
                    : AgeWidgets.Raw(button);
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeWidgets.TextOf(button),
                    () => AgeWidgets.Press(button),
                    () => AgeWidgets.Operable(button),
                    tooltip
                );
                AgeWidgets.Point(vtable, AgeWidgets.Button(button));
                builder.AddItem(Nodes.Drawn(
                    ControlId.For(button, "fleetsel:button/" + NameOf(button)),
                    vtable,
                    button
                ));
            }
        }

        // ---- reading the window ----

        /// <summary>The window's own title, found where it is drawn: the class exposes its table and its
        /// Validate button and nothing else.</summary>
        private static string Title(FleetSelectionModalWindow window)
        {
            try
            {
                if (window == null)
                {
                    return null;
                }

                AgePrimitiveLabel[] labels =
                    window.GetComponentsInChildren<AgePrimitiveLabel>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] != null && labels[i].name == "WindowTitle")
                    {
                        return AgeText.Label(labels[i]);
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>The fleet a row stands for. The wrapper the table binds is rebuilt on every refresh,
        /// so it is the fleet underneath it that identifies the row.</summary>
        private static Fleet FleetOf(GuiTableLine line)
        {
            try
            {
                GuiGarrison wrapper = line == null ? null : line.Data as GuiGarrison;
                return wrapper == null ? null : wrapper.Fleet;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the row is called when the name column draws nothing - the fleet's own name.
        /// </summary>
        private static string FleetName(GuiTableLine line)
        {
            try
            {
                GuiGarrison wrapper = line == null ? null : line.Data as GuiGarrison;
                return wrapper == null ? null : AgeText.Clean(wrapper.LocalizedName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string NameOf(AgeTransform widget)
        {
            try
            {
                return widget.name;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static AgeTransform ValidateTransform(FleetSelectionModalWindow window)
        {
            try
            {
                return window == null || window.ValidateButton == null
                    ? null
                    : window.ValidateButton.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTooltip ValidateTooltip(FleetSelectionModalWindow window)
        {
            try
            {
                return window == null ? null : window.ValidateTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GuiTable Table(FleetSelectionModalWindow window)
        {
            try
            {
                return window == null ? null : window.GuiTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static FleetSelectionModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<FleetSelectionModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

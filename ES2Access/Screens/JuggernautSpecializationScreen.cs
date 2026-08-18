using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// Turning a Behemoth into an Obliterator, a Battleship or a Citadel
    /// (<c>JuggernautSpecializationModalWindow</c>) - Supremacy's one irreversible ship decision, and the
    /// only modal in the game the mod already declares the OPENER for ("Specialize", <see cref="ShipRows"/>
    /// :78-83, the game's own <c>%FleetSpecializeJuggernautTitle</c>) while having no screen for the window
    /// it raises. Pressing that button without this would leave the keyboard in a window nothing speaks
    /// for, on top of a page the engine has switched off underneath (<c>GuiModalWindow.OnBeginShow</c>
    /// disables every screen behind an exclusive modal).
    ///
    /// Supremacy (DLC16) is not installed here, but the window does NOT need it to be read: its three
    /// cards come out of the entity-action database, which loads every <c>*_DLC*</c> datatable whatever
    /// the empire owns (measured - <c>GuiSpecializations.Count == 3</c> with all four expansions
    /// unowned), and <c>Bind</c> takes any <c>Ship</c>. So the window was bound to a real ship and walked
    /// with real cards, real costs and the game's real failure sentences, which settled the three
    /// questions the earlier floor left open:
    ///
    /// - a card is ONE row. The four pieces the prefab draws - title, paragraph, cost line, failure line -
    ///   are a single tall panel a sighted player reads top to bottom after the title tells them which
    ///   card it is, so the title is the row and the rest is its buffer.
    /// - the strategic resources strip writes NO name on any of its six rows, only an icon and two
    ///   figures, so it is named from each item's own binding rather than from the drawn row
    ///   (<see cref="AddResource"/>) - reading it as drawn gave six identical "0"s.
    /// - the missing-technology hint on a blocked card (<c>GuiButtonHint</c>) stays UNDECLARED, which is
    ///   the same ruling <see cref="DiplomacyActions"/> already made for the hint button the diplomacy
    ///   rows draw: its whole job is <c>Gui.ActivateHint</c>, which closes the window and points a mouse
    ///   at the technology tree. The card's own failure lines already end with the game's sentence about
    ///   it ("Hold Control+Click to locate this technology in the technology tree"), so nothing is lost.
    ///
    /// What still waits for the DLC is only what a BEHEMOTH would change: a card the empire can actually
    /// take (every card refuses for a missing technology here, so a selected card, an enabled Confirm and
    /// the confirmation box behind it are code-verified only), and the specialize button's own route into
    /// the window from the ship toolbar.
    ///
    /// The resource items are READOUTS rather than controls even though the prefab wires a click to each:
    /// <c>ResourceItem.OnClickCb</c> does nothing outside the developers' god mode, and a click the game
    /// answers with silence is not a control (the same call the military screen's manpower box makes).
    ///
    /// Escape is the game's, verified from its code rather than assumed: this window adds no
    /// <c>HandleInput</c> of its own, so <c>GuiModalWindow.HandleInput</c> answers Exit by hiding it. The
    /// confirmation the Confirm button raises is the game's own message box, which the mod already models
    /// (<see cref="MessageBoxScreen"/>), and it sits above this at layer 100.
    /// </summary>
    public sealed class JuggernautSpecializationScreen : Screen
    {
        private static readonly object HeadingStop = "juggernaut:heading";
        private static readonly object CardsStop = "juggernaut:cards";
        private static readonly object ResourcesStop = "juggernaut:resources";
        private static readonly object ControlsStop = "juggernaut:controls";

        /// <summary>The game's own heading for the window, for the frames before it has drawn one.
        /// </summary>
        private const string TitleKey = "%JuggernautSpecializationModalWindowTitle";

        /// <summary>The name the prefab gives the paragraph under the heading - what specializing costs
        /// the ship. The window keeps it in no field of its own, so it is found where it is drawn, the
        /// same way the heading is (<see cref="WindowShape.Title"/>).</summary>
        private const string SubtitleName = "WindowSubTitle";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.juggernaut-specialization"; }
        }

        /// <summary>Over the military screen (15) and the selected-fleet panel's own picker (26), both of
        /// which can raise it, and under the government modal (33) and the message box its Confirm button
        /// opens.</summary>
        public override int Layer
        {
            get { return 29; }
        }

        public override string ScreenName
        {
            get
            {
                string title = WindowShape.Title(Window());
                return string.IsNullOrEmpty(title) ? AgeText.Clean(TitleKey) : title;
            }
        }

        /// <summary>The cards, because choosing one is the whole window.</summary>
        public override object InitialFocusStop
        {
            get { return CardsStop; }
        }

        public override bool IsActive()
        {
            try
            {
                JuggernautSpecializationModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: <c>GuiModalWindow.HandleInput</c> hides the window on Exit.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            JuggernautSpecializationModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                BuildHeading(builder, window);
                BuildCards(builder, window);
                BuildResources(builder, window);
                BuildControls(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("juggernaut specialization: reading the window threw: " + e);
            }
        }

        /// <summary>What specializing does to the ship - the paragraph the window draws under its
        /// heading. The heading itself is the screen's name and is not declared again.</summary>
        private void BuildHeading(
            GraphBuilder builder,
            JuggernautSpecializationModalWindow window
        )
        {
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, SubtitleName, 4),
                "juggernaut:subtitle"
            );
            if (_cells.Count > 0)
            {
                builder.BeginStop(HeadingStop);
                Cells.EmitLinear(builder, _cells);
            }
        }

        /// <summary>
        /// The three specializations, as the one-of-three the window keeps them.
        ///
        /// Radios rather than boxes because that is what the window does with them: picking one unticks
        /// the previous (<c>SelectSpecialization</c>), and a set of tick boxes would tell the player they
        /// may have several. Keyed on the card's POSITION rather than on the widget, because the table
        /// pools its cards and re-binds them by index every time the window opens.
        ///
        /// The card's own click is the one gesture: <c>OnToggleCb</c> reports the choice to the window,
        /// and the DOUBLE click is the shortcut that picks and confirms in one
        /// (<c>OnDoubleClickSpecialization</c>) - so Ctrl+Alt+Enter is the game's own second click here
        /// and is left to the shared chord rather than reproduced.
        /// </summary>
        private void BuildCards(GraphBuilder builder, JuggernautSpecializationModalWindow window)
        {
            AgeTransform table = window.SpecializationCardTable;
            IList<AgeTransform> children = Children(table);
            if (children == null || children.Count == 0)
            {
                return;
            }

            builder.BeginStop(CardsStop);
            _cells.Clear();
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                JuggernautSpecializationActionCard card = Card(widget);
                if (
                    card == null
                    || card.GuiSpecialization == null
                    || card.SelectionToggle == null
                    || !AgeWidgets.Visible(widget)
                )
                {
                    continue;
                }

                JuggernautSpecializationActionCard it = card;
                AgeTransform at = widget;
                AgeTooltip tooltip = card.Tooltip;
                Func<bool> offered = () => AgeWidgets.Operable(AgeWidgets.Transform(it.SelectionToggle));
                NodeVtable vtable = GraphNodes.Radio(
                    () => AgeText.Label(it.Title),
                    () => it.SelectionToggle != null && it.SelectionToggle.State,
                    () => AgeWidgets.Toggle(it.SelectionToggle),
                    offered,
                    () => CardLines(it),
                    tooltip
                );
                GraphNodes.AddRefusal(vtable, tooltip, offered);
                AgeWidgets.Point(vtable, it.SelectionToggle, tooltip, at);
                Cells.Add(
                    _cells,
                    widget,
                    ControlId.Structural("juggernaut:card/" + i),
                    vtable
                );
            }

            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The rest of what a card draws, in the order it is drawn: what the specialization does,
        /// what it costs, and the game's own sentence for why this one cannot be had. Reviewable rather
        /// than announced - the card's name is what a walk down the list should say.</summary>
        private static IList<string> CardLines(JuggernautSpecializationActionCard card)
        {
            List<string> lines = new List<string>();
            try
            {
                AddLines(lines, card.Description);
                if (AgeWidgets.Visible(card.CostLine))
                {
                    AddLines(lines, card.Cost);
                }

                AddLines(lines, card.Failure);
            }
            catch (Exception)
            {
                return lines;
            }

            return lines;
        }

        private static void AddLines(List<string> into, AgePrimitiveLabel label)
        {
            if (label == null || !AgeWidgets.Visible(label.AgeTransform))
            {
                return;
            }

            IList<string> lines = AgeText.Lines(AgeText.FullLabel(label));
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                {
                    into.Add(lines[i]);
                }
            }
        }

        /// <summary>What the empire has to pay with: its dust, its manpower, and a stock-and-net pair per
        /// strategic resource. Readouts, because the click the prefab wires to a resource does nothing
        /// outside god mode (see the class comment).</summary>
        private void BuildResources(
            GraphBuilder builder,
            JuggernautSpecializationModalWindow window
        )
        {
            AgeTransform group = window.EmpireResourcesGroup;
            if (!AgeWidgets.Visible(group))
            {
                return;
            }

            builder.BeginStop(ResourcesStop);
            _cells.Clear();
            Cells.AddReadout(_cells, Widget(window.EmpireMoneyLabel), "juggernaut:money");
            Cells.AddReadout(_cells, Widget(window.EmpireManpowerLabel), "juggernaut:manpower");
            IList<AgeTransform> items = Children(ResourcesTable(window));
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AddResource(_cells, items[i], i);
            }

            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// One strategic resource the empire could pay a specialization with.
        ///
        /// The strip draws an icon, a holding and a per-turn figure and writes NO name anywhere on the
        /// row - six of them side by side read as six zeroes - so the name comes off the item's own
        /// binding (<c>ResourceItem.GuiLocatedResource</c>), which is where the strip got the icon from.
        /// The two figures are joined by the same phrasing the empire banner reads its stocks with, so
        /// the second is heard as a rate rather than as a second holding.
        ///
        /// A resource the empire holds none of is DIMMED rather than dropped (measured: alpha 0.3 on all
        /// six at turn one), and it stays declared - "we have no Antimatter" is exactly what a player
        /// weighing a cost line reading "50 Antimatter" needs to hear.
        /// </summary>
        private static void AddResource(List<Cell> cells, AgeTransform widget, int index)
        {
            ResourceItem item = widget == null ? null : widget.GetComponent<ResourceItem>();
            if (item == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            GuiLocatedResource resource = item.GuiLocatedResource;
            if (resource == null)
            {
                // Before the panel has bound, and for a resource the game left unbound: the drawn
                // figure alone, rather than a node named after nothing.
                Cells.AddReadout(cells, widget, "juggernaut:resource/" + index);
                return;
            }

            ResourceItem it = item;
            GuiLocatedResource located = resource;
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeText.Clean(located.Title),
                () => StockAndNet(it.StockLabel, it.NetLabel),
                null,
                item.Tooltip
            );
            AgeWidgets.Point(vtable, item.Button, item.Tooltip, widget);
            Cells.Add(
                cells,
                widget,
                ControlId.Referenced(item, "juggernaut:resource/" + located.Name),
                vtable
            );
        }

        /// <summary>A holding and what the next turn does to it, as the game drew the two numbers - or
        /// just the holding, which is all this strip draws. The item keeps a per-turn label with a real
        /// figure in it ("+0") and leaves it HIDDEN here (measured), so the figure has to be gated on
        /// the label being drawn and not on it being non-empty, or the reading invents a rate that is
        /// nowhere on screen.</summary>
        private static string StockAndNet(AgePrimitiveLabel stock, AgePrimitiveLabel net)
        {
            string held = AgeText.Label(stock);
            string rate =
                net != null && AgeWidgets.Visible(net.AgeTransform) ? AgeText.Label(net) : null;
            if (string.IsNullOrEmpty(rate))
            {
                return held;
            }

            return ModStrings.Format(ModStrings.GalaxyStockAndNet, held, rate);
        }

        /// <summary>The strip of strategic resources, off the panel the window binds rather than by
        /// prefab name.</summary>
        private static AgeTransform ResourcesTable(JuggernautSpecializationModalWindow window)
        {
            try
            {
                ResourcesPanel panel = window.EmpireStrategicResourcesPanel;
                return panel == null ? null : panel.ResourceItemsTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The two ways the window ends, read off what it drew: Confirm, which raises the game's
        /// own confirmation, and Cancel. Both carry captions, so the shared shape reading finds them; the
        /// cards and the resources are excluded because they are declared above under names of their own.
        /// </summary>
        private void BuildControls(
            GraphBuilder builder,
            JuggernautSpecializationModalWindow window
        )
        {
            _cells.Clear();
            WindowShape.Controls(
                _cells,
                window,
                "juggernaut",
                window.SpecializationCardTable,
                ResourcesTable(window)
            );
            if (_cells.Count > 0)
            {
                builder.BeginStop(ControlsStop);
                Cells.EmitLinear(builder, _cells);
            }
        }

        private static JuggernautSpecializationActionCard Card(AgeTransform widget)
        {
            try
            {
                return widget == null
                    ? null
                    : widget.GetComponent<JuggernautSpecializationActionCard>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IList<AgeTransform> Children(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Children;
            }
            catch (Exception)
            {
                return null;
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

        private static JuggernautSpecializationModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<JuggernautSpecializationModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

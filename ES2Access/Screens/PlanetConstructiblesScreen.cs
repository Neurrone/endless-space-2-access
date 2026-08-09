using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The short list a planet card puts up when you ask a Behemoth to work on the planet: which type
    /// to terraform it into, or which of its anomalies to reduce.
    ///
    /// It is the second half of two of the card's buttons. Pressing Terraform or Reduce Anomaly does
    /// not order anything - it slides this panel out from under the card, and the ORDER is placed by
    /// picking a line in it (<c>PlanetConstructiblePanel.OnClickConstructibleItem</c>,
    /// PlanetConstructiblePanel.cs:314-357). Without a screen here, a player who chose one of those two
    /// entries from the card's menu would hear nothing at all and be sitting in front of a list they
    /// could not reach.
    ///
    /// It is a PANEL, not a window: the game parks it inside
    /// <c>PlanetLabelsWindow_SystemOrbital.ConstructiblePanel</c>
    /// (PlanetLabelsWindow_SystemOrbital.cs:11, shown at :124-161) and moves it every frame to stay
    /// under the card's button row. So it is polled like any other page, on its own layer above the
    /// galaxy it is drawn over, and the galaxy keeps its cursor underneath while it is up.
    ///
    /// Escape is claimed. Nothing in the game closes this panel with a key - it is dismissed by
    /// clicking its button again, or it closes itself once an order has been placed - so Escape would
    /// otherwise sail past it into the pause menu while the panel stayed on screen. Closing goes
    /// through the game's own route, the message every one of its close paths sends to the window that
    /// owns it.
    ///
    /// No fixture can draw it. Both openers need a Behemoth in the system with the matching fleet
    /// action available (<c>PlanetLabel_SystemOrbital.RefreshTerraformationStatus</c> :785-856,
    /// <c>RefreshAnomalyReductionStatus</c> :940-1010, both of which return before making the button
    /// visible when no fleet offers the action), and turn 1 has neither. What is verifiable offline is
    /// that the screen registers and that its predicate reads false everywhere the fixture can go.
    /// </summary>
    public sealed class PlanetConstructiblesScreen : Screen
    {
        /// <summary>Reused across builds rather than allocated per frame: Build runs every tick.
        /// </summary>
        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.planet-constructibles"; }
        }

        /// <summary>Above the galaxy it is drawn over - it covers part of it and takes the keyboard -
        /// and below the tutorial popup and everything else that can appear on top of both.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The panel's own heading, which the game writes to say which of the two questions it
        /// is asking - "Terraform to", "Reduce Anomaly".</summary>
        public override string ScreenName
        {
            get
            {
                PlanetConstructiblePanel panel = Panel();
                return panel == null ? null : AgeText.Label(panel.TitleLabel);
            }
        }

        /// <summary>Ours while the game is drawing the panel and it still has the planet it was opened
        /// for. The planet is what the panel drops last when it is dismissed
        /// (<c>PlanetConstructiblePanel.OnEndHide</c> → <c>UnbindPlanet</c>), so it outlives the
        /// shown flag through the fade and keeps the screen from blinking out mid-transition.</summary>
        public override bool IsActive()
        {
            try
            {
                PlanetConstructiblePanel panel = Panel();
                return panel != null
                    && panel.Planet != null
                    && panel.Shown
                    && AgeWidgets.Visible(panel.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape dismisses the list, leaving the card it belongs to alone.</summary>
        public override bool Back()
        {
            Close();
            return true;
        }

        public override bool ConsumesBack
        {
            get { return true; }
        }

        public override void Build(GraphBuilder builder)
        {
            PlanetConstructiblePanel panel = Panel();
            if (panel == null || panel.ConstructibleTable == null)
            {
                return;
            }

            _cells.Clear();
            try
            {
                StarSystemConstructibleItem[] items =
                    panel.ConstructibleTable.GetComponentsInChildren<StarSystemConstructibleItem>(
                        true
                    );
                for (int i = 0; i < items.Length; i++)
                {
                    Add(_cells, items[i]);
                }
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("planet constructibles: reading the list threw: " + e);
                return;
            }

            // In the order they are drawn, which for a table the game arranges is top to bottom.
            foreach (List<Cell> row in AgeLayout.Rows(_cells, CellWidget))
            {
                for (int i = 0; i < row.Count; i++)
                {
                    builder.StartRow();
                    builder.AddItem(row[i].Id, row[i].Vtable);
                    builder.EndRow();
                }
            }
        }

        private sealed class Cell
        {
            public AgeTransform Widget;
            public ControlId Id;
            public NodeVtable Vtable;
        }

        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

        /// <summary>One line of the list: what would be built, what it would cost and how long it
        /// would take - the tooltip the game hangs on the line - and the game's own reasons where it
        /// is refusing. Choosing it replays the line's own click, which is what places the order.
        /// </summary>
        private static void Add(List<Cell> cells, StarSystemConstructibleItem item)
        {
            if (item == null || !AgeWidgets.Visible(item.AgeTransform))
            {
                return;
            }

            IGuiConstructible constructible = item.GuiConstructible;
            if (constructible == null)
            {
                return;
            }

            StarSystemConstructibleItem it = item;
            AgeTooltip tooltip = AgeWidgets.Raw(item.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => Name(it)),
                    GraphNodes.DisabledPart(() => AgeWidgets.Operable(it.AgeTransform)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
                OnActivate = () => Choose(it),
            };
            AgeWidgets.PointAt(vtable, item.AgeTransform);
            cells.Add(
                new Cell
                {
                    Widget = item.AgeTransform,
                    Id = ControlId.Referenced(
                        item,
                        "planet-constructible/" + constructible.Name
                    ),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>The line's full name. The panel clips its caption to the width of the card's button
        /// row and truncates what will not fit, so the name comes from what the line is FOR.</summary>
        private static string Name(StarSystemConstructibleItem item)
        {
            try
            {
                return AgeText.Clean(Gui.Localize(item.GuiConstructible.Title));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Place the order the way a click on the line places it - the button's own handler,
        /// which is where the game builds the action context and posts the order.</summary>
        private static void Choose(StarSystemConstructibleItem item)
        {
            try
            {
                if (AgeWidgets.Operable(item.AgeTransform))
                {
                    AgeWidgets.Press(item.Button);
                }
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("planet constructibles: choosing a line threw: " + e);
            }
        }

        /// <summary>Dismiss the panel through the game's own route: the message its owner listens for,
        /// which every one of the panel's own close paths sends (PlanetConstructiblePanel.cs:336, 367,
        /// 373, 378, 383).</summary>
        private static void Close()
        {
            try
            {
                PlanetConstructiblePanel panel = Panel();
                if (panel != null && panel.Client != null)
                {
                    panel.Client.SendMessage(
                        "OnCloseConstructiblePanel",
                        UnityEngine.SendMessageOptions.DontRequireReceiver
                    );
                }
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("planet constructibles: closing the panel threw: " + e);
            }
        }

        private static PlanetConstructiblePanel Panel()
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemOrbital>(false)
                    : null;
                return window == null ? null : window.ConstructiblePanel;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
